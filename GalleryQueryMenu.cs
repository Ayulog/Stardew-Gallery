using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace StardewGallery;

internal sealed class GalleryQueryMenu : IClickableMenu, IGallerySearchMenu
{
    private const int LogicalWidth = GalleryDrawing.MenuWidth, LogicalHeight = GalleryDrawing.MenuHeight;
    private const int VisibleRows = GallerySpreadLayout.IndexRowsPerPage;
    private const int SearchId = 1000, ResetFilterId = 1002, FilterBase = 1100, FilterCount = 6, RowBase = 2000;
    private const int DropdownMaxVisible = 7, DropdownItemHeight = 44;

    private static readonly string[] FilterFields = ["kind", "npc", "location", "source", "state", "encounter"];

    private sealed record FilterChoice(string? Id, string Label);

    private readonly GalleryViewContext context;
    private readonly GalleryPageState state;
    private readonly StorySearchIndex index;
    private readonly TextBox search;
    private readonly float rowTextScale;
    private IReadOnlyList<StorySearchRow> rows = [];
    private string lastSearch = "";
    private int scroll; // Whole-page index on the right page (0-based)
    private float scale;
    private int offsetX, offsetY, viewportWidth, viewportHeight;
    private bool keyboardFocus;
    private Point lastMouse;

    // In-book parchment dropdown overlay state for filter rows 0..4.
    private bool dropdownOpen;
    private int dropdownFilterRow = -1;
    private FilterChoice[] dropdownAllChoices = [];
    private FilterChoice[] dropdownChoices = [];
    private int dropdownScroll;
    private int dropdownSelectedIndex;
    private bool dropdownSearchFocused;
    private bool draggingDropdownScroll;
    private TextBox? dropdownSearch;
    private string lastDropdownSearch = "";

    public bool IsSearchSelected => search.Selected || dropdownSearch?.Selected == true;
    internal bool IsDropdownOpen => dropdownOpen && dropdownChoices is not null && dropdownFilterRow is >= 0 and < 5;
    internal int OpenDropdownRow => IsDropdownOpen ? dropdownFilterRow : -1;

    private static Rectangle SearchBounds => GalleryBookChrome.ToRect(GallerySpreadLayout.IndexSearchBounds);
    private static Rectangle FilterRowBounds(int i) => i == 5
        ? GalleryBookChrome.ToRect(GallerySpreadLayout.IndexClueBounds)
        : GalleryBookChrome.ToRect(GallerySpreadLayout.IndexFilterBounds(i));
    private static Rectangle CountBannerBounds => GalleryBookChrome.ToRect(GallerySpreadLayout.IndexCountBounds);
    private static Rectangle ResetBounds => GalleryBookChrome.ToRect(GallerySpreadLayout.IndexResetBounds);
    private static Rectangle RowBounds(int slot) => GalleryBookChrome.ToRect(GallerySpreadLayout.IndexRowBounds(slot));

    internal GalleryQueryMenu(GalleryViewContext context, GalleryPageState state)
        : base(0, 0, LogicalWidth, LogicalHeight, true)
    {
        this.context = context;
        this.state = state;
        (index, rowTextScale) = context.QueryIndex(() =>
        {
            var built = new StorySearchIndex(
                context.Catalog,
                entry => context.Locations.Get(entry.LocationName),
                context.Characters.Get,
                identity => context.Catalog.FindPrerequisite(identity.EventId) is { } prerequisite && prerequisite.Identity == identity
                    ? PrerequisitePresentation.Title(prerequisite, context) : context.Name(identity),
                Game1.player.eventsSeen.ToHashSet(StringComparer.Ordinal),
                (entry, id) => context.Characters.Key(id, entry),
                entry => context.Locations.Key(entry.LocationName),
                key => context.I18n.Get(key),
                context.Pinyin,
                defaultStoryTitle: entry => context.CardTitle(entry),
                clueFor: context.EventClue);
            return (built, Math.Min(1f, 30f / built.Rows.Select(row => Game1.smallFont.MeasureString(row.DisplayTitle + row.Characters + row.Location).Y)
                .Append((float)Game1.smallFont.LineSpacing).Max()));
        });
        search = new GalleryTextBox
        {
            DrawFrame = GalleryUiSkin.Get(GalleryUiSkin.SearchBoxAsset) is null,
            Height = SearchBounds.Height
        };
        search.OnEnterPressed += _ => FinishSearch();
        search.Text = GallerySearchInput.CleanText(state.SearchText);
        lastSearch = search.Text;
        if (state.KindFilter is StoryKind.Heart or StoryKind.Ordinary)
            state.Filter = state.Filter with { Kind = state.KindFilter == StoryKind.Heart ? QueryKind.Heart : QueryKind.Ordinary };
        if (state.LocationFilter is not null)
            state.Filter = state.Filter with { Location = state.LocationFilter };
        state.KindFilter = null;
        state.LocationFilter = null;
        rows = index.Search(search.Text, state.Filter);

        int initialFocus = state.Focus;
        scroll = initialFocus >= RowBase && initialFocus < GalleryBookChrome.RibbonTabBaseId && rows.Count > 0
            ? GalleryPagination.PageForIndex((initialFocus - RowBase) / 2, rows.Count, VisibleRows)
            : GalleryPagination.ClampPage(state.Scroll, rows.Count, VisibleRows);

        Layout();
        int firstIndex = GalleryPagination.FirstIndex(scroll, rows.Count, VisibleRows);
        Focus(initialFocus >= 0 ? initialFocus : rows.Count > 0 ? RowBase + firstIndex * 2 : SearchId);
    }

    public override void update(GameTime time)
    {
        base.update(time);
        RefreshSearch();
        RefreshDropdownSearch();
        SaveState();
    }

    private void RefreshSearch(bool force = false)
    {
        search.Text = GallerySearchInput.CleanText(search.Text);
        if (!force && lastSearch == search.Text)
            return;
        lastSearch = search.Text;
        rows = index.Search(search.Text, state.Filter);
        scroll = 0;
        BuildComponents();
    }

    private void RefreshDropdownSearch(bool force = false)
    {
        if (!IsDropdownOpen || dropdownSearch is null)
            return;
        dropdownSearch.Text = GallerySearchInput.CleanText(dropdownSearch.Text);
        if (!force && lastDropdownSearch == dropdownSearch.Text)
            return;
        lastDropdownSearch = dropdownSearch.Text;
        FilterDropdownChoices();
    }

    public void DeselectSearch()
    {
        search.Selected = false;
        if (Game1.keyboardDispatcher.Subscriber == search)
            Game1.keyboardDispatcher.Subscriber = null;
        if (dropdownSearch is not null)
        {
            dropdownSearch.Selected = false;
            if (Game1.keyboardDispatcher.Subscriber == dropdownSearch)
                Game1.keyboardDispatcher.Subscriber = null;
        }
    }

    public void FinishSearch()
    {
        if (IsDropdownOpen && dropdownSearch?.Selected == true)
        {
            RefreshDropdownSearch(force: true);
            dropdownSearch.Selected = false;
            if (Game1.keyboardDispatcher.Subscriber == dropdownSearch)
                Game1.keyboardDispatcher.Subscriber = null;
            dropdownSearchFocused = false;
            dropdownSelectedIndex = dropdownChoices.Length > 1 ? 1 : 0;
            EnsureDropdownVisible();
            return;
        }
        RefreshSearch();
        DeselectSearch();
        // Show the results with the first one focused; a second Enter or A opens it.
        keyboardFocus = true;
        if (rows.Count > 0) FocusRow(0);
        else Focus(SearchId);
    }

    public void HandleControllerBack()
    {
        if (IsDropdownOpen)
        {
            CloseDropdown(playSound: true);
            return;
        }
        if (search.Selected)
        {
            DeselectSearch();
            Focus(SearchId);
            Game1.playSound("bigDeSelect");
        }
        else
            Return();
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        (x, y) = ToLogical(x, y);
        if (upperRightCloseButton?.bounds.Contains(x, y) == true)
        {
            CloseDropdown(playSound: false);
            SaveState();
            DeselectSearch();
            context.Navigation.Close();
            return;
        }

        if (IsDropdownOpen)
        {
            HandleDropdownLeftClick(x, y);
            return;
        }

        if (GalleryBookChrome.TryClickRibbonTab(x, y, GalleryBookTab.StoryIndex, context.Navigation, () =>
            {
                SaveState();
                DeselectSearch();
            }))
        {
            return;
        }
        if (SearchBounds.Contains(x, y))
        {
            GalleryTextBox.SelectForInput(search);
            Focus(SearchId);
            return;
        }
        DeselectSearch();
        if (state.Filter != new QueryFilter() && ResetBounds.Contains(x, y))
        {
            state.Filter = new();
            RefreshSearch(force: true);
            Focus(FilterBase);
            Game1.playSound("smallSelect");
            return;
        }
        for (int i = 0; i < FilterCount; i++)
        {
            if (FilterRowBounds(i).Contains(x, y))
            {
                Focus(FilterBase + i);
                ActivateFilterRow(i);
                return;
            }
        }
        if (GalleryBookChrome.ToRect(GallerySpreadLayout.IndexPrevBounds).Contains(x, y))
        {
            TurnPage(-1);
            return;
        }
        if (GalleryBookChrome.ToRect(GallerySpreadLayout.IndexNextBounds).Contains(x, y))
        {
            TurnPage(1);
            return;
        }

        int first = GalleryPagination.FirstIndex(scroll, rows.Count, VisibleRows);
        int visible = GalleryPagination.VisibleCount(scroll, rows.Count, VisibleRows);
        for (int slot = 0; slot < visible; slot++)
        {
            if (RowBounds(slot).Contains(x, y))
            {
                OpenRow(first + slot);
                return;
            }
        }
    }

    public override void leftClickHeld(int x, int y)
    {
        base.leftClickHeld(x, y);
        if (!IsDropdownOpen || !draggingDropdownScroll)
            return;
        (_, int ly) = ToLogical(x, y);
        ScrollDropdownToMouseY(ly);
    }

    public override void releaseLeftClick(int x, int y)
    {
        base.releaseLeftClick(x, y);
        draggingDropdownScroll = false;
    }

    public override void receiveRightClick(int x, int y, bool playSound = true) => HandleControllerBack();

    public override void receiveScrollWheelAction(int direction)
    {
        if (IsDropdownOpen)
        {
            int maxScroll = Math.Max(0, dropdownChoices.Length - DropdownMaxVisible);
            int next = Math.Clamp(dropdownScroll + (direction < 0 ? 1 : -1), 0, maxScroll);
            if (next != dropdownScroll)
            {
                dropdownScroll = next;
                Game1.playSound("shiny4");
            }
            return;
        }
        TurnPage(direction < 0 ? 1 : -1);
    }

    public override void performHoverAction(int x, int y)
    {
        if (lastMouse != new Point(x, y))
        {
            if (lastMouse != default) keyboardFocus = false;
            lastMouse = new Point(x, y);
            if (IsDropdownOpen)
            {
                (int lx, int ly) = ToLogical(x, y);
                var layout = ComputeDropdownLayout();
                int visCount = Math.Min(DropdownMaxVisible, dropdownChoices.Length);
                for (int i = 0; i < visCount; i++)
                {
                    Rectangle itemRect = DropdownItemRect(layout, i);
                    if (itemRect.Contains(lx, ly))
                    {
                        dropdownSelectedIndex = dropdownScroll + i;
                        dropdownSearchFocused = false;
                        break;
                    }
                }
            }
        }
        base.performHoverAction(x, y);
    }

    public override void receiveKeyPress(Keys key)
    {
        if (IsDropdownOpen)
        {
            if (dropdownSearch?.Selected == true)
            {
                if (key == Keys.Escape)
                    CloseDropdown(playSound: true);
                else if (key == Keys.Down && dropdownChoices.Length > 0)
                {
                    DeselectSearch();
                    dropdownSearchFocused = false;
                    dropdownSelectedIndex = Math.Clamp(dropdownSelectedIndex, 0, dropdownChoices.Length - 1);
                    EnsureDropdownVisible();
                }
                return;
            }
            if (key == Keys.Escape || Game1.options.menuButton.Any(binding => binding.key == key))
            {
                CloseDropdown(playSound: true);
                return;
            }
            if (key is Keys.Up or Keys.Down or Keys.PageUp or Keys.PageDown or Keys.Enter)
            {
                keyboardFocus = true;
                HandleDropdownKey(key);
                return;
            }
            return;
        }

        if (search.Selected)
        {
            if (key == Keys.Escape) DeselectSearch();
            return;
        }
        if (key is Keys.Up or Keys.Right or Keys.Down or Keys.Left or Keys.Enter or Keys.PageUp or Keys.PageDown) keyboardFocus = true;
        if (key == Keys.Escape || Game1.options.menuButton.Any(binding => binding.key == key)) { Return(); return; }
        if (key is Keys.PageUp or Keys.PageDown) { TurnPage(key == Keys.PageUp ? -1 : 1); return; }
        if (key == Keys.Enter) { Activate(currentlySnappedComponent?.myID ?? SearchId); return; }
        if (key is Keys.Up or Keys.Right or Keys.Down or Keys.Left)
        {
            applyMovementKey(key switch { Keys.Up => 0, Keys.Right => 1, Keys.Down => 2, _ => 3 }); return;
        }
        base.receiveKeyPress(key);
    }

    public override void receiveGamePadButton(Buttons button)
    {
        if (IsDropdownOpen)
        {
            if (button is Buttons.B or Buttons.Y)
            {
                CloseDropdown(playSound: true);
                return;
            }
            if (button == Buttons.A)
            {
                ActivateDropdownSelection();
                return;
            }
            if (button is Buttons.LeftShoulder or Buttons.RightShoulder)
            {
                MoveDropdownSelection(button == Buttons.LeftShoulder ? -DropdownMaxVisible : DropdownMaxVisible);
                return;
            }
            return;
        }

        if (button == Buttons.Y)
        {
            DeselectSearch();
            int focused = currentlySnappedComponent?.myID ?? FilterBase;
            int row = focused >= FilterBase && focused < FilterBase + 5 ? focused - FilterBase : 0;
            Focus(FilterBase + row);
            OpenDropdown(row);
            return;
        }
        if (button == Buttons.B) { HandleControllerBack(); return; }
        if (button is Buttons.LeftShoulder or Buttons.RightShoulder)
        {
            DeselectSearch();
            TurnPage(button == Buttons.LeftShoulder ? -1 : 1);
            return;
        }
        base.receiveGamePadButton(button);
    }

    private void Activate(int id)
    {
        if (id >= GalleryBookChrome.RibbonTabBaseId && id < GalleryBookChrome.RibbonTabBaseId + GallerySpreadLayout.RibbonTabCount)
        {
            SaveState();
            DeselectSearch();
            Game1.playSound("shwip");
            context.Navigation.SwitchTab((GalleryBookTab)(id - GalleryBookChrome.RibbonTabBaseId));
            return;
        }
        if (id == GalleryBookChrome.PrevPageComponentId) { TurnPage(-1); return; }
        if (id == GalleryBookChrome.NextPageComponentId) { TurnPage(1); return; }
        if (id >= RowBase) { OpenRow((id - RowBase) / 2); return; }
        if (id >= FilterBase && id < FilterBase + FilterCount) { ActivateFilterRow(id - FilterBase); return; }
        if (id == SearchId) { GalleryTextBox.SelectForInput(search); return; }
        if (id == ResetFilterId && state.Filter != new QueryFilter())
        {
            state.Filter = new();
            RefreshSearch(force: true);
            Focus(FilterBase);
            Game1.playSound("smallSelect");
        }
    }

    internal void ActivateFilterRow(int filterIndex)
    {
        if (filterIndex == 5)
        {
            StepInlineFilter(5, 1);
            return;
        }
        if (dropdownFilterRow == filterIndex)
        {
            CloseDropdown(playSound: true);
            return;
        }
        OpenDropdown(filterIndex);
    }

    internal void OpenDropdown(int filterIndex)
    {
        if (index is null || filterIndex is < 0 or >= 5)
            return;
        DeselectSearch();
        dropdownOpen = true;
        dropdownFilterRow = filterIndex;
        string field = FilterFields[filterIndex];
        dropdownAllChoices = ChoicesFor(field);
        dropdownChoices = dropdownAllChoices;
        lastDropdownSearch = "";
        draggingDropdownScroll = false;
        dropdownSearchFocused = false;

        if (dropdownAllChoices.Length > 8)
        {
            dropdownSearch ??= CreateDropdownSearchBox();
            dropdownSearch.Text = "";
        }
        else
        {
            dropdownSearch = null;
        }

        string? currentVal = CurrentFilterValue(field);
        int currentIdx = Array.FindIndex(dropdownChoices, c => SameFilterId(field, c.Id, currentVal));
        dropdownSelectedIndex = Math.Max(0, currentIdx);
        dropdownScroll = 0;
        EnsureDropdownVisible();
        UpdateDropdownSearchBounds();
        Game1.playSound("shwip");
    }

    internal void CloseDropdown(bool playSound)
    {
        if (!IsDropdownOpen)
            return;
        int row = dropdownFilterRow;
        DeselectSearch();
        dropdownOpen = false;
        dropdownFilterRow = -1;
        dropdownAllChoices = [];
        dropdownChoices = [];
        dropdownSearchFocused = false;
        draggingDropdownScroll = false;
        Focus(FilterBase + row);
        if (playSound)
            Game1.playSound("bigDeSelect");
    }

    private TextBox CreateDropdownSearchBox()
    {
        var box = new GalleryTextBox
        {
            DrawFrame = GalleryUiSkin.Get(GalleryUiSkin.SearchBoxAsset) is null,
            Height = 44
        };
        box.OnEnterPressed += _ => FinishSearch();
        return box;
    }

    private void FilterDropdownChoices()
    {
        if (dropdownAllChoices.Length == 0)
        {
            dropdownChoices = [];
            return;
        }
        string query = dropdownSearch?.Text.Trim() ?? "";
        if (query.Length == 0)
        {
            dropdownChoices = dropdownAllChoices;
        }
        else
        {
            string? spelling = context.Pinyin is null ? null : PinyinTable.Spelling(query);
            List<FilterChoice> filtered = [dropdownAllChoices[0]]; // First option is always "Any" (不限)
            for (int i = 1; i < dropdownAllChoices.Length; i++)
            {
                FilterChoice c = dropdownAllChoices[i];
                if (c.Label.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || c.Id?.Contains(query, StringComparison.OrdinalIgnoreCase) == true
                    || spelling is not null && context.Pinyin!.Matches(c.Label, spelling))
                {
                    filtered.Add(c);
                }
            }
            dropdownChoices = filtered.ToArray();
        }
        dropdownScroll = 0;
        dropdownSelectedIndex = dropdownChoices.Length > 1 && query.Length > 0 ? 1 : 0;
    }

    private void HandleDropdownKey(Keys key)
    {
        if (key == Keys.Enter)
        {
            ActivateDropdownSelection();
            return;
        }
        if (key == Keys.Up)
        {
            if (dropdownSearch is not null && !dropdownSearchFocused && dropdownSelectedIndex <= 0)
            {
                dropdownSearchFocused = true;
                Game1.playSound("shiny4");
            }
            else if (!dropdownSearchFocused)
                MoveDropdownSelection(-1);
        }
        else if (key == Keys.Down)
        {
            if (dropdownSearchFocused)
            {
                dropdownSearchFocused = false;
                dropdownSelectedIndex = 0;
                EnsureDropdownVisible();
                Game1.playSound("shiny4");
            }
            else
                MoveDropdownSelection(1);
        }
        else if (key is Keys.PageUp or Keys.PageDown)
        {
            dropdownSearchFocused = false;
            MoveDropdownSelection(key == Keys.PageUp ? -DropdownMaxVisible : DropdownMaxVisible);
        }
    }

    private void MoveDropdownSelection(int delta)
    {
        if (dropdownChoices.Length == 0)
            return;
        int next = Math.Clamp(dropdownSelectedIndex + delta, 0, dropdownChoices.Length - 1);
        if (next != dropdownSelectedIndex)
        {
            dropdownSelectedIndex = next;
            EnsureDropdownVisible();
            Game1.playSound("shiny4");
        }
    }

    private void EnsureDropdownVisible()
    {
        int maxScroll = Math.Max(0, dropdownChoices.Length - DropdownMaxVisible);
        if (dropdownSelectedIndex < dropdownScroll)
            dropdownScroll = dropdownSelectedIndex;
        else if (dropdownSelectedIndex >= dropdownScroll + DropdownMaxVisible)
            dropdownScroll = dropdownSelectedIndex - DropdownMaxVisible + 1;
        dropdownScroll = Math.Clamp(dropdownScroll, 0, maxScroll);
    }

    private void ActivateDropdownSelection()
    {
        if (dropdownSearchFocused && dropdownSearch is not null)
        {
            UpdateDropdownSearchBounds();
            GalleryTextBox.SelectForInput(dropdownSearch);
            return;
        }
        if (dropdownSelectedIndex >= 0 && dropdownSelectedIndex < dropdownChoices.Length)
            SelectDropdownChoice(dropdownSelectedIndex);
    }

    private void SelectDropdownChoice(int choiceIndex)
    {
        if (!IsDropdownOpen || choiceIndex < 0 || choiceIndex >= dropdownChoices.Length)
            return;
        int row = dropdownFilterRow;
        string field = FilterFields[row];
        ApplyInlineFilterValue(field, dropdownChoices[choiceIndex].Id);
        CloseDropdown(playSound: false);
        RefreshSearch(force: true);
        Focus(FilterBase + row);
        Game1.playSound("smallSelect");
    }

    private void HandleDropdownLeftClick(int x, int y)
    {
        Rectangle anchor = FilterRowBounds(dropdownFilterRow);
        if (anchor.Contains(x, y))
        {
            CloseDropdown(playSound: true);
            return;
        }

        var layout = ComputeDropdownLayout();
        if (!layout.Popup.Contains(x, y))
        {
            CloseDropdown(playSound: true);
            return;
        }

        if (dropdownSearch is not null && layout.Search.Contains(x, y))
        {
            dropdownSearchFocused = true;
            UpdateDropdownSearchBounds();
            GalleryTextBox.SelectForInput(dropdownSearch);
            return;
        }

        DeselectSearch();
        dropdownSearchFocused = false;

        if (layout.HasScroll && layout.ScrollTrack.Contains(x, y))
        {
            draggingDropdownScroll = true;
            ScrollDropdownToMouseY(y);
            return;
        }

        int visCount = Math.Min(DropdownMaxVisible, dropdownChoices.Length);
        for (int i = 0; i < visCount; i++)
        {
            if (DropdownItemRect(layout, i).Contains(x, y))
            {
                SelectDropdownChoice(dropdownScroll + i);
                return;
            }
        }
    }

    private void ScrollDropdownToMouseY(int logicalY)
    {
        var layout = ComputeDropdownLayout();
        int maxScroll = Math.Max(0, dropdownChoices.Length - DropdownMaxVisible);
        if (!layout.HasScroll || maxScroll <= 0 || layout.ScrollTrack.Height <= layout.ScrollThumbHeight)
            return;
        float ratio = Math.Clamp((logicalY - layout.ScrollTrack.Y - layout.ScrollThumbHeight / 2f)
            / Math.Max(1f, layout.ScrollTrack.Height - layout.ScrollThumbHeight), 0f, 1f);
        dropdownScroll = (int)Math.Round(ratio * maxScroll);
    }

    private readonly record struct DropdownLayout(
        Rectangle Popup,
        Rectangle Search,
        int ListTop,
        int ItemWidth,
        bool HasScroll,
        Rectangle ScrollTrack,
        int ScrollThumbHeight);

    private DropdownLayout ComputeDropdownLayout()
    {
        Rectangle anchor = FilterRowBounds(Math.Clamp(dropdownFilterRow, 0, 4));
        bool hasSearch = dropdownSearch is not null;
        int visCount = Math.Clamp(dropdownChoices.Length, 1, DropdownMaxVisible);
        int listHeight = visCount * DropdownItemHeight;
        int searchBlock = hasSearch ? 54 : 0;
        int popupHeight = 24 + searchBlock + listHeight;

        int preferredY = anchor.Bottom + 4;
        if (preferredY + popupHeight > 844)
        {
            int aboveY = anchor.Y - 4 - popupHeight;
            preferredY = aboveY >= 112 ? aboveY : Math.Max(112, 844 - popupHeight);
        }

        Rectangle popup = new(anchor.X, preferredY, anchor.Width, popupHeight);
        Rectangle searchRect = hasSearch ? new Rectangle(popup.X + 14, popup.Y + 12, popup.Width - 28, 44) : Rectangle.Empty;
        int listTop = popup.Y + 12 + searchBlock;
        bool hasScroll = dropdownChoices.Length > DropdownMaxVisible;
        int itemWidth = popup.Width - 28 - (hasScroll ? 22 : 0);
        Rectangle scrollTrack = hasScroll ? new Rectangle(popup.Right - 28, listTop + 2, 14, Math.Max(24, listHeight - 4)) : Rectangle.Empty;
        int thumbHeight = hasScroll
            ? Math.Max(28, (int)Math.Round(scrollTrack.Height * (DropdownMaxVisible / (float)dropdownChoices.Length)))
            : 0;
        return new DropdownLayout(popup, searchRect, listTop, itemWidth, hasScroll, scrollTrack, thumbHeight);
    }

    private static Rectangle DropdownItemRect(DropdownLayout layout, int slot)
        => new(layout.Popup.X + 14, layout.ListTop + slot * DropdownItemHeight, layout.ItemWidth, DropdownItemHeight - 2);

    private void UpdateDropdownSearchBounds()
    {
        if (!IsDropdownOpen || dropdownSearch is null)
            return;
        var layout = ComputeDropdownLayout();
        Rectangle textArea = GalleryUiSkin.SearchTextBounds(layout.Search);
        dropdownSearch.X = textArea.X - 12;
        dropdownSearch.Y = layout.Search.Y;
        dropdownSearch.Width = textArea.Width + 12;
        dropdownSearch.Height = layout.Search.Height;
    }

    private void StepInlineFilter(int filterIndex, int delta)
    {
        if (index is null)
            return;
        string field = FilterFields[filterIndex];
        if (field == "encounter")
        {
            state.Filter = state.Filter with { EncounterOnly = !state.Filter.EncounterOnly };
            RefreshSearch(force: true);
            Focus(FilterBase + filterIndex);
            Game1.playSound("smallSelect");
            return;
        }

        FilterChoice[] choices = ChoicesFor(field);
        if (choices.Length <= 1)
            return;
        string? currentVal = CurrentFilterValue(field);
        int selected = Array.FindIndex(choices, c => SameFilterId(field, c.Id, currentVal));
        int next = ((selected < 0 ? 0 : selected) + delta % choices.Length + choices.Length) % choices.Length;
        ApplyInlineFilterValue(field, choices[next].Id);
        RefreshSearch(force: true);
        Focus(FilterBase + filterIndex);
        Game1.playSound("smallSelect");
    }

    public override void applyMovementKey(int direction)
    {
        if (IsDropdownOpen)
        {
            if (dropdownSearch?.Selected == true || direction is < 0 or > 3)
                return;
            if (direction == 0)
                HandleDropdownKey(Keys.Up);
            else if (direction == 2)
                HandleDropdownKey(Keys.Down);
            return;
        }

        if (search.Selected || direction is < 0 or > 3) return;
        int id = currentlySnappedComponent?.myID ?? SearchId;

        if (id >= GalleryBookChrome.RibbonTabBaseId && id < GalleryBookChrome.RibbonTabBaseId + GallerySpreadLayout.RibbonTabCount)
        {
            int tab = id - GalleryBookChrome.RibbonTabBaseId;
            if (direction == 3 && tab > 0) Focus(GalleryBookChrome.RibbonTabBaseId + tab - 1);
            else if (direction == 1 && tab + 1 < GallerySpreadLayout.RibbonTabCount) Focus(GalleryBookChrome.RibbonTabBaseId + tab + 1);
            else if (direction == 1) Focus(GalleryBookChrome.PrevPageComponentId);
            else if (direction == 0) Focus(state.Filter != new QueryFilter() ? ResetFilterId : FilterBase + FilterCount - 1);
            return;
        }

        if (id is GalleryBookChrome.PrevPageComponentId or GalleryBookChrome.NextPageComponentId)
        {
            if (direction == 3)
                Focus(id == GalleryBookChrome.NextPageComponentId ? GalleryBookChrome.PrevPageComponentId
                    : state.Filter != new QueryFilter() ? ResetFilterId : GalleryBookChrome.RibbonTabBaseId + 2);
            else if (direction == 1 && id == GalleryBookChrome.PrevPageComponentId)
                Focus(GalleryBookChrome.NextPageComponentId);
            else if (direction == 0 && rows.Count > 0)
            {
                int first = GalleryPagination.FirstIndex(scroll, rows.Count, VisibleRows);
                int vis = GalleryPagination.VisibleCount(scroll, rows.Count, VisibleRows);
                FocusRow(first + vis - 1);
            }
            return;
        }

        if (id >= RowBase)
        {
            int row = (id - RowBase) / 2;
            int first = GalleryPagination.FirstIndex(scroll, rows.Count, VisibleRows);
            int vis = GalleryPagination.VisibleCount(scroll, rows.Count, VisibleRows);
            int localSlot = row - first;

            if (direction is 0 or 2)
            {
                int nextSlot = localSlot + (direction == 0 ? -1 : 1);
                if (nextSlot < 0) Focus(SearchId);
                else if (nextSlot >= vis) Focus(GalleryBookChrome.PrevPageComponentId);
                else FocusRow(first + nextSlot);
            }
            else if (direction == 3)
            {
                Focus(localSlot <= 0 ? SearchId : FilterBase + Math.Clamp(localSlot, 0, FilterCount - 1));
            }
            return;
        }

        if (id == SearchId)
        {
            if (direction == 1 && rows.Count > 0) FocusRow(GalleryPagination.FirstIndex(scroll, rows.Count, VisibleRows));
            else if (direction == 2) Focus(FilterBase);
            return;
        }

        if (id >= FilterBase && id < FilterBase + FilterCount)
        {
            int fIdx = id - FilterBase;
            if (direction == 0) Focus(fIdx == 0 ? SearchId : FilterBase + fIdx - 1);
            else if (direction == 2)
            {
                if (fIdx + 1 < FilterCount) Focus(FilterBase + fIdx + 1);
                else Focus(state.Filter != new QueryFilter() ? ResetFilterId : GalleryBookChrome.RibbonTabBaseId + 1);
            }
            else if (direction == 3) StepInlineFilter(fIdx, -1);
            else if (direction == 1)
            {
                if (rows.Count > 0)
                {
                    int first = GalleryPagination.FirstIndex(scroll, rows.Count, VisibleRows);
                    int vis = GalleryPagination.VisibleCount(scroll, rows.Count, VisibleRows);
                    FocusRow(first + Math.Clamp(fIdx, 0, vis - 1));
                }
                else
                {
                    StepInlineFilter(fIdx, 1);
                }
            }
            return;
        }

        if (id == ResetFilterId)
        {
            if (direction == 0) Focus(FilterBase + FilterCount - 1);
            else if (direction == 2) Focus(GalleryBookChrome.RibbonTabBaseId + 2);
            else if (direction == 3) Focus(GalleryBookChrome.RibbonTabBaseId + 1);
            else if (direction == 1) Focus(GalleryBookChrome.PrevPageComponentId);
        }
    }

    private void FocusRow(int row)
    {
        if (rows.Count == 0)
        {
            Focus(SearchId);
            return;
        }
        int clampedRow = Math.Clamp(row, 0, rows.Count - 1);
        scroll = GalleryPagination.PageForIndex(clampedRow, rows.Count, VisibleRows);
        BuildComponents();
        Focus(RowBase + clampedRow * 2);
    }

    private void Focus(int id)
    {
        currentlySnappedComponent = allClickableComponents.FirstOrDefault(component => component.myID == id)
            ?? allClickableComponents.FirstOrDefault(component => component.myID == SearchId);
        SaveState();
        if (Game1.options.snappyMenus && Game1.options.gamepadControls)
            snapCursorToCurrentSnappedComponent();
    }

    // Game1 repopulates components by reflection when input switches to a controller; rebuild the gallery's own instead.
    public override void populateClickableComponentList() => BuildComponents();

    public override void snapToDefaultClickableComponent()
        => Focus(rows.Count == 0 ? SearchId : RowBase + GalleryPagination.FirstIndex(scroll, rows.Count, VisibleRows) * 2);

    private void OpenRow(int row)
    {
        if (row < 0 || row >= rows.Count) return;
        StorySearchRow entry = rows[row];
        Focus(RowBase + row * 2);
        SaveState();
        DeselectSearch();
        if (entry.Prerequisite is not null) context.Navigation.OpenPrerequisite(entry.EventId);
        else context.Navigation.OpenEvent(entry.Identity);
    }

    private void TurnPage(int delta)
    {
        var (nextPage, changed) = GalleryPagination.Turn(scroll, delta, rows.Count, VisibleRows);
        if (!changed)
            return;
        int prevId = currentlySnappedComponent?.myID ?? SearchId;
        scroll = nextPage;
        BuildComponents();
        if (prevId >= RowBase && prevId < GalleryBookChrome.RibbonTabBaseId && rows.Count > 0)
        {
            int first = GalleryPagination.FirstIndex(scroll, rows.Count, VisibleRows);
            Focus(RowBase + first * 2);
        }
        Game1.playSound("shwip");
    }

    private void SaveState()
    {
        state.SearchText = search.Text;
        state.Scroll = scroll;
        state.Focus = currentlySnappedComponent?.myID ?? SearchId;
    }

    private void Return()
    {
        SaveState(); DeselectSearch(); context.Navigation.Back(); Game1.playSound("bigDeSelect");
    }

    protected override void cleanupBeforeExit()
    {
        SaveState(); DeselectSearch(); base.cleanupBeforeExit();
    }

    public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
    {
        base.gameWindowSizeChanged(oldBounds, newBounds); Layout();
    }

    private void Layout()
    {
        width = LogicalWidth; height = LogicalHeight; xPositionOnScreen = yPositionOnScreen = 0;
        viewportWidth = Game1.uiViewport.Width; viewportHeight = Game1.uiViewport.Height;
        scale = (float)GalleryLayout.ScaleToFit(viewportWidth, viewportHeight, width, height, 24);
        offsetX = (int)Math.Round((viewportWidth - width * scale) / 2); offsetY = (int)Math.Round((viewportHeight - height * scale) / 2);
        Rectangle searchText = GalleryUiSkin.SearchTextBounds(SearchBounds);
        search.X = searchText.X - 12;
        search.Y = SearchBounds.Y;
        search.Width = searchText.Width + 12;
        search.Height = SearchBounds.Height;
        UpdateDropdownSearchBounds();
        initializeUpperRightCloseButton();
        GalleryBookChrome.PositionCloseButton(upperRightCloseButton, scale, offsetX, offsetY, viewportWidth, viewportHeight);
        BuildComponents();
    }

    private void BuildComponents()
    {
        int previous = currentlySnappedComponent?.myID ?? SearchId;
        scroll = GalleryPagination.ClampPage(scroll, rows.Count, VisibleRows);
        int first = GalleryPagination.FirstIndex(scroll, rows.Count, VisibleRows);
        int visible = GalleryPagination.VisibleCount(scroll, rows.Count, VisibleRows);

        allClickableComponents =
        [
            new(ToScreen(SearchBounds), "search") { myID = SearchId }
        ];
        GalleryBookChrome.AddRibbonTabComponents(allClickableComponents, ToScreen, GalleryBookChrome.PrevPageComponentId,
            upNeighborId: FilterBase + FilterCount - 1);

        for (int i = 0; i < FilterCount; i++)
            allClickableComponents.Add(new(ToScreen(FilterRowBounds(i)), $"filter-{FilterFields[i]}") { myID = FilterBase + i });

        if (state.Filter != new QueryFilter())
            allClickableComponents.Add(new(ToScreen(ResetBounds), "reset-filters") { myID = ResetFilterId });

        allClickableComponents.Add(new(ToScreen(GalleryBookChrome.ToRect(GallerySpreadLayout.IndexPrevBounds)), "prev-page")
        {
            myID = GalleryBookChrome.PrevPageComponentId
        });
        allClickableComponents.Add(new(ToScreen(GalleryBookChrome.ToRect(GallerySpreadLayout.IndexNextBounds)), "next-page")
        {
            myID = GalleryBookChrome.NextPageComponentId
        });

        for (int slot = 0; slot < visible; slot++)
        {
            int rowIndex = first + slot;
            allClickableComponents.Add(new(ToScreen(RowBounds(slot)), "event") { myID = RowBase + rowIndex * 2 });
        }

        int fallback = previous >= RowBase && previous < GalleryBookChrome.RibbonTabBaseId && visible > 0
            ? RowBase + Math.Clamp((previous - RowBase) / 2, first, first + visible - 1) * 2
            : SearchId;
        currentlySnappedComponent = allClickableComponents.FirstOrDefault(component => component.myID == previous)
            ?? allClickableComponents.FirstOrDefault(component => component.myID == fallback)
            ?? allClickableComponents.FirstOrDefault(component => component.myID == SearchId);
        SaveState();
    }

    public override void draw(SpriteBatch b)
    {
        if (viewportWidth != Game1.uiViewport.Width || viewportHeight != Game1.uiViewport.Height) Layout();
        b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height), Color.Black * .45f);

        (int mouseX, int mouseY) = ToLogical(Game1.getMouseX(true), Game1.getMouseY(true));
        bool showFocus = Game1.options.snappyMenus && Game1.options.gamepadControls || keyboardFocus;
        int focusedId = currentlySnappedComponent?.myID ?? SearchId;
        bool overlayActive = IsDropdownOpen;

        GalleryDrawing.BeginScaled(b, scale, offsetX, offsetY);
        GalleryBookChrome.DrawCleanHomeSpread(b, context.Textures.Home, width, height);
        GalleryBookChrome.DrawRibbonTabs(b, GalleryBookTab.StoryIndex, focusedId, showFocus && !overlayActive,
            overlayActive ? new Point(-999, -999) : new Point(mouseX, mouseY), context.I18n, out string? tooltip);
        GalleryBookChrome.DrawSpreadTitle(b, context.I18n.Get("tab.index"), scale, offsetX, offsetY);

        // Left page: Search + 5 dropdown filter bars + 1 clue checkbox bar + Result count + Reset button
        GalleryDrawing.DrawLeftFitted(b, context.I18n.Get("query.open"), new Rectangle(SearchBounds.X + 4, SearchBounds.Y - 32, SearchBounds.Width - 8, 26), GallerySpreadDrawing.Ink);
        bool searchHot = !overlayActive && (search.Selected || SearchBounds.Contains(mouseX, mouseY) || showFocus && focusedId == SearchId);
        GalleryUiSkin.DrawSearchBox(b, SearchBounds, searchHot);
        search.Draw(b);
        if (search.Text.Length == 0 && !search.Selected)
            GalleryDrawing.DrawLeftFitted(b, context.I18n.Get("query.search"),
                GalleryDrawing.Inset(GalleryUiSkin.SearchTextBounds(SearchBounds), 4, 10), GallerySpreadDrawing.LightInk);

        GalleryDrawing.DrawLeftFitted(b, context.I18n.Get("filter.title"), new Rectangle(SearchBounds.X + 4, 226, SearchBounds.Width - 8, 28), GallerySpreadDrawing.Ink);

        for (int i = 0; i < 5; i++)
        {
            Rectangle rowRect = FilterRowBounds(i);
            string field = FilterFields[i];
            bool rowHovered = !overlayActive && rowRect.Contains(mouseX, mouseY) || dropdownFilterRow == i;
            bool rowFocused = showFocus && !overlayActive && focusedId == FilterBase + i;
            GalleryUiSkin.DrawFilterRow(b, rowRect, rowHovered || rowFocused);
            if (rowFocused)
                GalleryUiSkin.DrawFocusCorners(b, GalleryDrawing.Inset(rowRect, -4, -4), 12);

            int arrowW = GalleryUiSkin.FilterRowArrowWidth(rowRect);
            string fieldLabel = context.I18n.Get("filter." + field);
            string valLabel = FormatInlineFilterValue(field);
            float fScale = Math.Min(0.92f, (rowRect.Height - 10f) / Game1.smallFont.LineSpacing);
            int labelW = Math.Min(210, (int)Math.Ceiling(Game1.smallFont.MeasureString(fieldLabel).X * fScale) + 20);
            GalleryDrawing.DrawEllipsized(b, fieldLabel, new Rectangle(rowRect.X + 18, rowRect.Y, labelW - 18, rowRect.Height), fScale);
            Rectangle valArea = new(rowRect.X + labelW + 8, rowRect.Y, Math.Max(40, rowRect.Width - labelW - arrowW - 18), rowRect.Height);
            string fittedVal = GalleryTextFit.Ellipsize(valLabel, valArea.Width / fScale, s => Game1.smallFont.MeasureString(s).X);
            Vector2 valSize = Game1.smallFont.MeasureString(fittedVal) * fScale;
            b.DrawString(Game1.smallFont, fittedVal, new Vector2(valArea.Right - valSize.X, valArea.Center.Y - valSize.Y / 2f),
                GallerySpreadDrawing.Ink, 0f, Vector2.Zero, fScale, SpriteEffects.None, 0f);
        }

        // Row 5: highlighted clue checkbox bar
        Rectangle clueRect = FilterRowBounds(5);
        bool clueHovered = !overlayActive && clueRect.Contains(mouseX, mouseY);
        bool clueFocused = showFocus && !overlayActive && focusedId == FilterBase + 5;
        GalleryUiSkin.DrawClueRow(b, clueRect, clueHovered || clueFocused || state.Filter.EncounterOnly);
        Rectangle checkRect = new(clueRect.X + 20, clueRect.Center.Y - 14, 28, 28);
        GalleryUiSkin.DrawCheckbox(b, checkRect, state.Filter.EncounterOnly);
        GalleryDrawing.DrawEllipsized(b, context.I18n.Get("filter.encounter-only"),
            new Rectangle(checkRect.Right + 14, clueRect.Y, clueRect.Width - 74, clueRect.Height),
            Math.Min(0.92f, (clueRect.Height - 12f) / Game1.smallFont.LineSpacing));
        if (clueFocused)
            GalleryUiSkin.DrawFocusCorners(b, GalleryDrawing.Inset(clueRect, -4, -4), 12);

        GalleryDrawing.DrawLeftFitted(b, context.I18n.Get("query.count", new { count = rows.Count }), CountBannerBounds, GallerySpreadDrawing.Ink);

        bool hasActiveFilter = state.Filter != new QueryFilter();
        bool resetHot = !overlayActive && hasActiveFilter && (ResetBounds.Contains(mouseX, mouseY) || showFocus && focusedId == ResetFilterId);
        GalleryUiSkin.DrawWoodButton(b, ResetBounds, context.I18n.Get("filter.reset"),
            !hasActiveFilter ? UiButtonState.Disabled : resetHot ? UiButtonState.Hover : UiButtonState.Normal);
        if (showFocus && !overlayActive && focusedId == ResetFilterId)
            GalleryUiSkin.DrawFocusCorners(b, GalleryDrawing.Inset(ResetBounds, -4, -4), 12);

        // Right page: 5 paged story journal cards
        int first = GalleryPagination.FirstIndex(scroll, rows.Count, VisibleRows);
        int visible = GalleryPagination.VisibleCount(scroll, rows.Count, VisibleRows);
        for (int slot = 0; slot < visible; slot++)
        {
            int rowIndex = first + slot;
            StorySearchRow row = rows[rowIndex];
            GalleryEvent? entry = row.Event;
            Rectangle bounds = RowBounds(slot);
            bool hovered = !overlayActive && bounds.Contains(mouseX, mouseY);
            bool focused = showFocus && !overlayActive && focusedId >= RowBase && focusedId < GalleryBookChrome.RibbonTabBaseId
                && (focusedId - RowBase) / 2 == rowIndex;

            GalleryUiSkin.DrawStoryRow(b, bounds, hovered || focused);
            if (focused)
                GalleryUiSkin.DrawFocusCorners(b, GalleryDrawing.Inset(bounds, -4, -4), 12);

            Rectangle thumbRect = GalleryUiSkin.StoryRowThumbnail(bounds);
            bool seen = Game1.player?.eventsSeen.Contains(row.EventId) == true;
            if (entry is not null)
            {
                bool unlocked = context.IsStoryUnlocked(entry);
                bool parallel = context.IsParallelBranch(entry);
                Texture2D? cover = context.Photos.Cover(entry.Resolved.Identity);
                if (cover is not null)
                    GalleryPhotos.DrawCover(b, cover, thumbRect, unlocked ? Color.White : new Color(215, 210, 198));
                else if (!seen)
                    GalleryBookChrome.DrawLockedStoryEnvelope(b, thumbRect, context.CardPrimaryLead(entry), row.Location, parallel && unlocked);
                else
                    GalleryPhotos.DrawCover(b, context.Textures.Thumbnail, thumbRect, Color.White);
            }
            else
            {
                GalleryBookChrome.DrawLockedStoryEnvelope(b, thumbRect, $"ID {row.EventId}", "", parallel: seen);
            }

            (string pillText, UiPill pillColor, UiBadge? pillIcon) = ResolveRowPill(row, entry, seen);
            Rectangle pillBounds = GalleryUiSkin.DrawPill(b, new Point(bounds.Right - 18, bounds.Center.Y - 13), 26,
                pillText, pillColor, pillIcon, alignRight: true, maxWidth: 164);

            int textLeft = thumbRect.Right + 16;
            int textWidth = Math.Max(110, pillBounds.X - 12 - textLeft);
            string primaryTitle = string.IsNullOrWhiteSpace(row.DisplayTitle) ? row.Title : row.DisplayTitle;
            GalleryDrawing.DrawEllipsized(b, primaryTitle,
                new Rectangle(textLeft, bounds.Y + 20, textWidth, 34), Math.Min(0.94f, rowTextScale));

            string kindText = context.I18n.Get(entry is null ? "query.kind-prerequisite" : entry.Kind == StoryKind.Heart ? "query.kind-heart" : "query.kind-ordinary");
            string charPart = row.Characters.Length == 0 ? "" : row.Characters + GalleryDrawing.TextSeparator;
            string locPart = row.Location is "" or "-" ? "" : GalleryDrawing.TextSeparator + row.Location;
            string metaLine = charPart + kindText + locPart;
            GalleryDrawing.DrawEllipsized(b, metaLine,
                new Rectangle(textLeft, bounds.Y + 58, textWidth, 28), Math.Min(0.84f, rowTextScale));

            if (hovered || focused)
            {
                List<string> tipLines =
                [
                    primaryTitle,
                    $"ID {row.EventId}",
                    row.Characters.Length == 0 ? kindText : $"{kindText} · {row.Characters}",
                    $"{context.I18n.Get("filter.location")}: {row.Location}",
                    $"{context.I18n.Get("filter.source")}: {row.SourceLabel}"
                ];
                if (row.Clue == EncounterClue.AvailableToday)
                    tipLines.Add(context.I18n.Get("clue.available-today") + ": " + context.I18n.Get("clue.available-today-hint"));
                else if (row.Clue == EncounterClue.ReadyForTiming)
                    tipLines.Add(context.I18n.Get("clue.ready-for-timing") + ": " + context.I18n.Get("clue.ready-for-timing-hint"));
                if (entry is not null && context.StoryQuote(entry) is { Length: > 0 } quote)
                    tipLines.Add(context.I18n.Get("event-detail.quote", new { quote }));
                tooltip = string.Join("\n", tipLines);
            }
        }

        if (rows.Count == 0)
            GalleryDrawing.DrawCentered(b, Game1.parseText(context.I18n.Get("nav.no-results"), Game1.smallFont, 560), new Rectangle(892, 340, 612, 180));

        GalleryBookChrome.DrawPager(
            b,
            GallerySpreadLayout.IndexPrevBounds,
            GallerySpreadLayout.IndexIndicatorBounds,
            GallerySpreadLayout.IndexNextBounds,
            scroll,
            rows.Count,
            VisibleRows,
            focusedId,
            showFocus && !overlayActive,
            overlayActive ? null : new Point(mouseX, mouseY));

        if (overlayActive)
            DrawDropdownOverlay(b, mouseX, mouseY, showFocus);

        upperRightCloseButton?.draw(b);
        GalleryDrawing.EndScaled(b);
        if (!overlayActive && tooltip is not null)
            drawHoverText(b, Game1.parseText(tooltip, Game1.smallFont, Math.Min(620, Math.Max(200, viewportWidth - 80))), Game1.smallFont);
        drawMouse(b);
    }

    private void DrawDropdownOverlay(SpriteBatch b, int mouseX, int mouseY, bool showFocus)
    {
        var layout = ComputeDropdownLayout();
        Rectangle popup = layout.Popup;
        string field = FilterFields[dropdownFilterRow];
        string? activeValue = CurrentFilterValue(field);

        // Soft drop shadow + dark walnut frame + brass trim + warm cream parchment sheet
        b.Draw(Game1.staminaRect, new Rectangle(popup.X + 6, popup.Y + 8, popup.Width, popup.Height), Color.Black * 0.34f);
        b.Draw(Game1.staminaRect, popup, new Color(76, 42, 20));
        b.Draw(Game1.staminaRect, GalleryDrawing.Inset(popup, 4, 4), new Color(188, 142, 78));
        b.Draw(Game1.staminaRect, GalleryDrawing.Inset(popup, 7, 7), new Color(247, 234, 206));
        GalleryUiSkin.DrawFocusCorners(b, GalleryDrawing.Inset(popup, 2, 2), 12);

        if (dropdownSearch is not null)
        {
            UpdateDropdownSearchBounds();
            bool dsHot = dropdownSearch.Selected || dropdownSearchFocused || layout.Search.Contains(mouseX, mouseY);
            GalleryUiSkin.DrawSearchBox(b, layout.Search, dsHot);
            dropdownSearch.Draw(b);
            if (dropdownSearch.Text.Length == 0 && !dropdownSearch.Selected)
                GalleryDrawing.DrawLeftFitted(b, context.I18n.Get("home.search"),
                    GalleryDrawing.Inset(GalleryUiSkin.SearchTextBounds(layout.Search), 4, 8), GallerySpreadDrawing.LightInk);
        }

        int visCount = Math.Min(DropdownMaxVisible, dropdownChoices.Length);
        float itemScale = Math.Min(0.92f, 32f / Math.Max(1f, Game1.smallFont.LineSpacing));
        for (int i = 0; i < visCount; i++)
        {
            int choiceIdx = dropdownScroll + i;
            FilterChoice choice = dropdownChoices[choiceIdx];
            Rectangle itemRect = DropdownItemRect(layout, i);
            bool isCurrent = SameFilterId(field, choice.Id, activeValue);
            bool isSelected = !dropdownSearchFocused && choiceIdx == dropdownSelectedIndex;
            bool isHovered = itemRect.Contains(mouseX, mouseY);

            if (isSelected || isHovered)
                b.Draw(Game1.staminaRect, itemRect, new Color(232, 194, 124) * 0.82f);
            else if (isCurrent)
                b.Draw(Game1.staminaRect, itemRect, new Color(228, 208, 168) * 0.55f);

            if (i + 1 < visCount)
                b.Draw(Game1.staminaRect, new Rectangle(itemRect.X + 8, itemRect.Bottom, itemRect.Width - 16, 1), new Color(204, 178, 138) * 0.55f);

            if (showFocus && isSelected)
                GalleryUiSkin.DrawFocusCorners(b, GalleryDrawing.Inset(itemRect, 1, 1), 10);

            int textX = itemRect.X + 16;
            int textW = itemRect.Width - (isCurrent ? 56 : 28);
            GalleryDrawing.DrawEllipsized(b, choice.Label, new Rectangle(textX, itemRect.Y, Math.Max(40, textW), itemRect.Height), itemScale);

            if (isCurrent)
            {
                b.Draw(context.Textures.ConditionIcons,
                    new Rectangle(itemRect.Right - 32, itemRect.Center.Y - 10, 20, 20),
                    GalleryBookChrome.ToRect(GallerySpreadLayout.ConditionCheckSource),
                    Color.White);
            }
        }

        if (layout.HasScroll)
        {
            Rectangle track = layout.ScrollTrack;
            b.Draw(Game1.staminaRect, track, new Color(196, 168, 126));
            int maxScroll = Math.Max(1, dropdownChoices.Length - DropdownMaxVisible);
            int thumbY = track.Y + (int)Math.Round((track.Height - layout.ScrollThumbHeight) * (dropdownScroll / (float)maxScroll));
            GalleryDrawing.DrawScrollbar(b, new Rectangle(track.X + 1, thumbY, track.Width - 2, layout.ScrollThumbHeight));
        }
    }

    private (string Text, UiPill Color, UiBadge? Icon) ResolveRowPill(StorySearchRow row, GalleryEvent? entry, bool seen)
    {
        if (entry is null)
            return seen
                ? (context.I18n.Get("filter.done"), UiPill.Wood, null)
                : (context.I18n.Get("filter.pending"), UiPill.Wood, null);

        if (row.Clue == EncounterClue.AvailableToday)
            return (context.I18n.Get("clue.available-today"), UiPill.Amber, UiBadge.AvailableToday);
        if (row.Clue == EncounterClue.ReadyForTiming)
            return (context.I18n.Get("clue.ready-for-timing"), UiPill.Wood, UiBadge.ClueReady);
        if (context.IsFavorite(entry.Resolved.Identity))
            return (context.I18n.Get("pill.favorited"), UiPill.Amber, UiBadge.HeartFull);
        if (context.IsParallelBranch(entry))
            return (context.I18n.Get("pill.parallel"), UiPill.Amber, UiBadge.Branch);
        if (seen)
            return (context.I18n.Get("pill.seen"), UiPill.Wood, null);
        if (!context.IsStoryUnlocked(entry))
            return (context.I18n.Get("pill.locked"), UiPill.Wood, null);
        return (context.I18n.Get("pill.unseen"), UiPill.Wood, null);
    }

    private string FormatInlineFilterValue(string field)
    {
        if (field == "encounter")
            return context.I18n.Get(state.Filter.EncounterOnly ? "filter.encounter-on" : "filter.any");
        if (index is null)
            return context.I18n.Get("filter.any");
        string? current = CurrentFilterValue(field);
        return ChoicesFor(field).FirstOrDefault(c => SameFilterId(field, c.Id, current))?.Label ?? context.I18n.Get("filter.any");
    }

    private string? CurrentFilterValue(string field) => field switch
    {
        "kind" => ((int)state.Filter.Kind).ToString(),
        "npc" => state.Filter.Npc,
        "location" => state.Filter.Location,
        "source" => state.Filter.Source,
        "state" => ((int)state.Filter.Completion).ToString(),
        "encounter" => state.Filter.EncounterOnly ? "1" : "0",
        _ => null
    };

    private void ApplyInlineFilterValue(string field, string? value)
    {
        int? number = int.TryParse(value, out int parsed) ? parsed : null;
        state.Filter = field switch
        {
            "kind" => state.Filter with { Kind = (QueryKind)(number ?? 0) },
            "npc" => state.Filter with { Npc = value },
            "location" => state.Filter with { Location = value },
            "source" => state.Filter with { Source = value },
            "state" => state.Filter with { Completion = (QueryCompletion)(number ?? 0) },
            "encounter" => state.Filter with { EncounterOnly = number == 1 },
            _ => state.Filter
        };
    }

    private FilterChoice[] ChoicesFor(string field)
    {
        string T(string key) => context.I18n.Get(key);
        FilterChoice any = new(null, T("filter.any"));
        IEnumerable<FilterChoice> values = field switch
        {
            "kind" => new[] { "filter.any", "query.kind-heart", "query.kind-ordinary", "query.kind-prerequisite" }
                .Select((key, i) => new FilterChoice(i.ToString(), T(key))),
            "state" => new[] { "filter.any", "filter.done", "filter.pending" }
                .Select((key, i) => new FilterChoice(i.ToString(), T(key))),
            "npc" => new[] { any }.Concat(index.Rows.SelectMany(r => r.Npcs).Distinct(StringComparer.Ordinal)
                .Select(id => new FilterChoice(id, context.Characters.Get(id))).OrderBy(c => c.Label, StringComparer.CurrentCulture)),
            "location" => new[] { any }.Concat(index.Rows.Where(r => r.Event is not null).DistinctBy(r => r.LocationKey, StringComparer.OrdinalIgnoreCase)
                .Select(r => new FilterChoice(r.LocationKey, r.Location)).OrderBy(c => c.Label, StringComparer.CurrentCulture)),
            "source" => new[] { any }.Concat(index.Rows.Select(r => r.Source).DistinctBy(s => s.Key, StringComparer.OrdinalIgnoreCase)
                .Select(s => new FilterChoice(s.Key, s.Label(T)))
                .OrderBy(c => c.Id == StoryQuerySource.GameKey ? 0 : c.Id?.StartsWith("mod:", StringComparison.OrdinalIgnoreCase) == true ? 1 : 2)
                .ThenBy(c => c.Label, StringComparer.CurrentCulture)),
            _ => []
        };
        var list = values.ToList();
        if (field is "npc" or "location" or "source" && CurrentFilterValue(field) is { } selected
            && !list.Any(c => SameFilterId(field, c.Id, selected)))
        {
            string label = field == "npc" ? context.Characters.Get(selected)
                : field == "location" ? context.Locations.Get(selected.StartsWith("Data/Events/", StringComparison.OrdinalIgnoreCase) ? selected[12..] : selected)
                : selected;
            list.Add(new FilterChoice(selected, label));
        }
        return list.ToArray();
    }

    private static bool SameFilterId(string field, string? left, string? right)
        => string.Equals(left, right, field is "location" or "source" ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private Rectangle ToScreen(Rectangle bounds) => GalleryDrawing.ScaleRectangle(bounds, scale, offsetX, offsetY);
    private (int X, int Y) ToLogical(int x, int y) => ((int)Math.Round((x - offsetX) / scale), (int)Math.Round((y - offsetY) / scale));
}
