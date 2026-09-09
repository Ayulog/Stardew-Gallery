using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;

namespace StardewGallery;

internal sealed class GalleryQueryMenu : IClickableMenu, IGallerySearchMenu
{
    private const int LogicalWidth = 1440, LogicalHeight = 850;
    private const int VisibleRows = 8, RowHeight = 68;
    private const int SearchId = 1000, BackId = 1001, LocationId = 1002, KindBase = 1100, RowBase = 2000;
    private readonly GalleryViewContext context;
    private readonly GalleryPageState state;
    private readonly StorySearchIndex index;
    private readonly TextBox search;
    private readonly float rowTextScale;
    private IReadOnlyList<StorySearchRow> rows = [];
    private string lastSearch = "";
    private int scroll;
    private float scale;
    private int offsetX, offsetY, viewportWidth, viewportHeight;
    private bool dragging;
    private int dragOffset;
    private Rectangle scrollThumb;
    private static Rectangle SearchBounds => new(48, 92, 976, 72);
    private static Rectangle BackBounds => new(48, 784, 280, 48);
    private static Rectangle LocationBounds => new(888, 784, 480, 48);
    private static Rectangle ScrollTrack => new(1384, 214, 24, VisibleRows * RowHeight);
    private static Rectangle FilterBounds => new(1060, 96, 308, 64);
    private static Rectangle RowBounds(int slot) => new(48, 214 + slot * RowHeight, 1320, RowHeight);
    private static Rectangle ReplayBounds(int slot) => new(1304, 224 + slot * RowHeight, 48, 48);
    private int MaxScroll => Math.Max(0, rows.Count - VisibleRows);
    public bool IsSearchSelected => search.Selected;

    internal GalleryQueryMenu(GalleryViewContext context, GalleryPageState state)
        : base(0, 0, LogicalWidth, LogicalHeight, true)
    {
        this.context = context;
        this.state = state;
        index = new StorySearchIndex(context.Catalog, entry => context.Locations.Get(entry.LocationName), context.Characters.Get,
            identity => context.Catalog.FindPrerequisite(identity.EventId) is { } prerequisite && prerequisite.Identity == identity
                ? PrerequisitePresentation.Title(prerequisite, context) : context.Name(identity),
            Game1.player.eventsSeen.ToHashSet(StringComparer.Ordinal), new ConditionParser(Event.SplitPreconditions, ArgUtility.SplitBySpaceQuoteAware),
            row => row.Event is { } story ? PrerequisitePresentation.Truth(GalleryConditionPresentation.Build(story, context.I18n, locationName: context.Locations.Get(story.LocationName)))
                : PrerequisitePresentation.Truth(row.Prerequisite!, context.I18n), (entry, id) => context.Characters.Key(id, entry), entry => context.Locations.Key(entry.LocationName));
        search = new GalleryTextBox { Height = SearchBounds.Height };
        rowTextScale = Math.Min(1f, 29f / index.Rows.Select(row => Game1.smallFont.MeasureString(row.Title + row.Characters + row.Location).Y)
            .Append((float)Game1.smallFont.LineSpacing).Max());
        search.OnEnterPressed += _ => OpenFirstMatch();
        search.Text = GallerySearchInput.CleanText(state.SearchText);
        lastSearch = search.Text;
        if (state.KindFilter is StoryKind.Heart or StoryKind.Ordinary)
            state.Filter = state.Filter with { Kind = state.KindFilter == StoryKind.Heart ? QueryKind.Heart : QueryKind.Ordinary };
        if (state.LocationFilter is not null) state.Filter = state.Filter with { Location = state.LocationFilter };
        state.KindFilter = null; state.LocationFilter = null;
        rows = index.Search(search.Text, state.Filter);
        scroll = Math.Clamp(state.Scroll, 0, MaxScroll);
        int initialFocus = state.Focus;
        Layout();
        Focus(initialFocus >= 0 ? initialFocus : rows.Count > 0 ? RowBase + scroll * 2 : SearchId);
    }

    public override void update(GameTime time)
    {
        base.update(time);
        RefreshSearch();
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

    public void DeselectSearch()
    {
        search.Selected = false;
        if (Game1.keyboardDispatcher.Subscriber == search)
            Game1.keyboardDispatcher.Subscriber = null;
    }

    public void OpenFirstMatch()
    {
        RefreshSearch();
        if (rows.Count == 0)
            return;
        scroll = 0;
        OpenRow(0, replay: false);
    }

    public void HandleControllerBack()
    {
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
            SaveState(); DeselectSearch(); context.Navigation.Close(); return;
        }
        if (SearchBounds.Contains(x, y))
        {
            if (GallerySearchInputGuard.Ready)
                search.SelectMe();
            Focus(SearchId);
            return;
        }
        DeselectSearch();
        if (BackBounds.Contains(x, y)) { Return(); return; }
        if (FilterBounds.Contains(x, y)) { OpenFilters(); return; }
        if (state.Filter != new QueryFilter() && LocationBounds.Contains(x, y))
        {
            state.Filter = new(); RefreshSearch(force: true); Focus(KindBase); return;
        }
        if (MaxScroll > 0 && scrollThumb.Contains(x, y))
        {
            dragging = true; dragOffset = y - scrollThumb.Y; return;
        }
        if (MaxScroll > 0 && ScrollTrack.Contains(x, y))
        {
            ScrollBy(y < scrollThumb.Y ? -VisibleRows : VisibleRows); return;
        }
        for (int slot = 0; slot < VisibleRows && scroll + slot < rows.Count; slot++)
        {
            if (ReplayBounds(slot).Contains(x, y)) { OpenRow(scroll + slot, replay: true); return; }
            if (RowBounds(slot).Contains(x, y)) { OpenRow(scroll + slot, replay: false); return; }
        }
    }

    public override void receiveRightClick(int x, int y, bool playSound = true) => HandleControllerBack();
    public override void receiveScrollWheelAction(int direction) => ScrollBy(direction < 0 ? 1 : -1);
    public override void leftClickHeld(int x, int y)
    {
        if (!dragging || MaxScroll == 0) return;
        (_, y) = ToLogical(x, y);
        int travel = ScrollTrack.Height - scrollThumb.Height;
        int target = (int)Math.Round(Math.Clamp(y - dragOffset - ScrollTrack.Y, 0, travel) / (double)travel * MaxScroll);
        ScrollBy(target - scroll);
    }
    public override void releaseLeftClick(int x, int y) => dragging = false;

    public override void receiveKeyPress(Keys key)
    {
        if (search.Selected)
        {
            if (key == Keys.Escape) DeselectSearch();
            return;
        }
        if (key == Keys.Escape || Game1.options.menuButton.Any(binding => binding.key == key)) { Return(); return; }
        if (key is Keys.PageUp or Keys.PageDown) { ScrollBy(key == Keys.PageUp ? -VisibleRows : VisibleRows); return; }
        if (key == Keys.Enter) { Activate(currentlySnappedComponent?.myID ?? SearchId); return; }
        if (key is Keys.Up or Keys.Right or Keys.Down or Keys.Left)
        {
            applyMovementKey(key switch { Keys.Up => 0, Keys.Right => 1, Keys.Down => 2, _ => 3 }); return;
        }
        base.receiveKeyPress(key);
    }

    public override void receiveGamePadButton(Buttons button)
    {
        if (button == Buttons.Y) { DeselectSearch(); OpenFilters(); return; }
        if (button == Buttons.B) { HandleControllerBack(); return; }
        if (button == Buttons.A) { Activate(currentlySnappedComponent?.myID ?? SearchId); return; }
        if (button is Buttons.LeftShoulder or Buttons.RightShoulder)
        {
            ScrollBy(button == Buttons.LeftShoulder ? -VisibleRows : VisibleRows); return;
        }
        base.receiveGamePadButton(button);
    }

    private void Activate(int id)
    {
        if (id >= RowBase) { OpenRow((id - RowBase) / 2, (id - RowBase) % 2 == 1); return; }
        if (id == KindBase) { DeselectSearch(); OpenFilters(); return; }
        if (id == SearchId) { if (GallerySearchInputGuard.Ready) search.SelectMe(); return; }
        if (id == BackId) { HandleControllerBack(); return; }
        if (id == LocationId) { state.Filter = new(); RefreshSearch(force: true); Focus(KindBase); }
    }

    public override void applyMovementKey(int direction)
    {
        if (search.Selected || direction is < 0 or > 3) return;
        int id = currentlySnappedComponent?.myID ?? SearchId;
        if (id >= RowBase)
        {
            int row = (id - RowBase) / 2;
            bool replay = (id - RowBase) % 2 == 1;
            if (direction is 0 or 2)
            {
                int target = row + (direction == 0 ? -1 : 1);
                if (target < 0) Focus(SearchId);
                else if (target >= rows.Count) Focus(BackId);
                else FocusRow(target, replay);
            }
            else if (direction == 1 && CanReplay(rows[row])) FocusRow(row, true);
            else if (direction == 3) FocusRow(row, false);
            return;
        }
        if (id == BackId || id == LocationId)
        {
            if (direction == 0 && rows.Count > 0) FocusRow(Math.Min(rows.Count - 1, scroll + VisibleRows - 1), false);
            else if (direction == 1 && state.Filter != new QueryFilter()) Focus(LocationId);
            else if (direction == 3) Focus(BackId);
            return;
        }
        if (direction == 2) { if (rows.Count > 0) FocusRow(scroll, false); else Focus(BackId); return; }
        if (direction == 1) Focus(KindBase);
        else if (direction == 3) Focus(SearchId);
    }

    private void FocusRow(int row, bool replay)
    {
        scroll = Math.Clamp(row < scroll ? row : row >= scroll + VisibleRows ? row - VisibleRows + 1 : scroll, 0, MaxScroll);
        BuildComponents();
        Focus(RowBase + row * 2 + (replay && CanReplay(rows[row]) ? 1 : 0));
    }

    private void Focus(int id)
    {
        currentlySnappedComponent = allClickableComponents.FirstOrDefault(component => component.myID == id)
            ?? allClickableComponents.First(component => component.myID == SearchId);
        SaveState();
        if (!dragging && Game1.options.snappyMenus && Game1.options.gamepadControls)
            snapCursorToCurrentSnappedComponent();
    }

    public override void snapToDefaultClickableComponent() => Focus(rows.Count == 0 ? SearchId : RowBase + scroll * 2);

    private void OpenFilters()
    {
        SaveState(); DeselectSearch(); context.Navigation.OpenFilters();
    }
    private bool CanReplay(StorySearchRow row) => row.Event is { } story && context.ReplayAccess(story).Allowed;

    private void OpenRow(int row, bool replay)
    {
        if (row < 0 || row >= rows.Count) return;
        StorySearchRow entry = rows[row];
        if (replay && !CanReplay(entry)) return;
        Focus(RowBase + row * 2 + (replay ? 1 : 0));
        SaveState();
        DeselectSearch();
        if (entry.Prerequisite is not null) context.Navigation.OpenPrerequisite(entry.EventId);
        else if (replay) context.Navigation.Replay(entry.Identity);
        else context.Navigation.OpenEvent(entry.Identity);
    }

    private void ScrollBy(int amount)
    {
        scroll = Math.Clamp(scroll + amount, 0, MaxScroll);
        BuildComponents();
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
        search.X = SearchBounds.X; search.Y = SearchBounds.Y; search.Width = SearchBounds.Width;
        initializeUpperRightCloseButton();
        BuildComponents();
    }

    private void BuildComponents()
    {
        int previous = currentlySnappedComponent?.myID ?? SearchId;
        int thumbHeight = Math.Max(40, ScrollTrack.Height * VisibleRows / Math.Max(VisibleRows, rows.Count));
        scrollThumb = new Rectangle(ScrollTrack.X, ScrollTrack.Y + (MaxScroll == 0 ? 0
            : (int)Math.Round((ScrollTrack.Height - thumbHeight) * scroll / (double)MaxScroll)), ScrollTrack.Width, thumbHeight);
        allClickableComponents = [new(ToScreen(SearchBounds), "search") { myID = SearchId }, new(ToScreen(BackBounds), "back") { myID = BackId }];
        allClickableComponents.Add(new(ToScreen(FilterBounds), "filters") { myID = KindBase });
        if (state.Filter != new QueryFilter())
            allClickableComponents.Add(new(ToScreen(LocationBounds), "location") { myID = LocationId });
        for (int slot = 0; slot < VisibleRows && scroll + slot < rows.Count; slot++)
        {
            Rectangle detail = RowBounds(slot); detail.Width -= 76;
            allClickableComponents.Add(new(ToScreen(detail), "event") { myID = RowBase + (scroll + slot) * 2 });
            if (CanReplay(rows[scroll + slot]))
                allClickableComponents.Add(new(ToScreen(ReplayBounds(slot)), "replay") { myID = RowBase + (scroll + slot) * 2 + 1 });
        }
        int fallback = previous >= RowBase && rows.Count > 0 ? RowBase + Math.Clamp((previous - RowBase) / 2, scroll, Math.Min(rows.Count - 1, scroll + VisibleRows - 1)) * 2 : SearchId;
        currentlySnappedComponent = allClickableComponents.FirstOrDefault(component => component.myID == previous)
            ?? allClickableComponents.First(component => component.myID == fallback);
        SaveState();
    }

    public override void draw(SpriteBatch b)
    {
        if (viewportWidth != Game1.uiViewport.Width || viewportHeight != Game1.uiViewport.Height) Layout();
        b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height), Color.Black * .55f);
        GalleryDrawing.BeginScaled(b, scale, offsetX, offsetY);
        drawTextureBox(b, 0, 0, width, height, Color.White);
        SpriteText.drawStringHorizontallyCenteredAt(b, context.I18n.Get("query.title"), width / 2, 30, maxWidth: 900);
        search.Draw(b);
        if (search.Text.Length == 0 && !search.Selected)
            GalleryDrawing.DrawLeftFitted(b, context.I18n.Get(GallerySearchInputGuard.Ready ? "query.search" : "home.search-unavailable"), GalleryDrawing.Inset(SearchBounds, 12, 8));
        GalleryDrawing.DrawButton(b, FilterBounds, context.I18n.Get("filter.title"));
        if (state.Filter != new QueryFilter() || currentlySnappedComponent?.myID == KindBase)
            b.Draw(Game1.staminaRect, new Rectangle(FilterBounds.X + 20, FilterBounds.Bottom - 7, FilterBounds.Width - 40, 3), new Color(66, 112, 76));
        Column(b, "query.column-event", 158, 382);
        Column(b, "query.column-npc", 568, 332);
        Column(b, "query.column-location", 928, 352);
        string? tooltip = null;
        (int mouseX, int mouseY) = ToLogical(Game1.getMouseX(true), Game1.getMouseY(true));
        for (int slot = 0; slot < VisibleRows && scroll + slot < rows.Count; slot++)
        {
            StorySearchRow row = rows[scroll + slot];
            GalleryEvent? entry = row.Event;
            Rectangle bounds = RowBounds(slot);
            bool hovered = bounds.Contains(mouseX, mouseY);
            bool focused = Game1.options.snappyMenus && Game1.options.gamepadControls
                && (currentlySnappedComponent?.myID - RowBase) / 2 == scroll + slot;
            b.Draw(Game1.staminaRect, bounds, hovered || focused ? new Color(204, 221, 187) * .65f
                : slot % 2 == 0 ? Color.White * .2f : new Color(101, 85, 68) * .07f);
            if (entry is not null) GalleryPhotos.DrawCover(b, context.Photos.Cover(entry.Resolved.Identity) ?? context.Textures.Thumbnail,
                new Rectangle(58, bounds.Y + 9, 90, 50), Color.White);
            else
            {
                var icon = Game1.player.eventsSeen.Contains(row.EventId) ? GallerySpreadLayout.ConditionCheckSource : GallerySpreadLayout.ConditionCrossSource;
                b.Draw(context.Textures.ConditionIcons, new Rectangle(80, bounds.Y + 14, 40, 40), new Rectangle(icon.X, icon.Y, icon.Width, icon.Height), Color.White);
            }
            GalleryDrawing.DrawEllipsized(b, row.Title, new Rectangle(158, bounds.Y + 6, 382, 29), rowTextScale);
            GalleryDrawing.DrawLeftFitted(b, context.I18n.Get(entry is null ? "query.kind-prerequisite" : entry.Kind == StoryKind.Heart ? "query.kind-heart" : "query.kind-ordinary"),
                new Rectangle(158, bounds.Y + 37, 240, 23), GallerySpreadDrawing.Ink);
            GalleryDrawing.DrawEllipsized(b, row.Characters.Length == 0 ? "-" : row.Characters, new Rectangle(568, bounds.Y + 8, 332, 52), rowTextScale);
            GalleryDrawing.DrawEllipsized(b, row.Location, new Rectangle(928, bounds.Y + 8, 352, 52), rowTextScale);
            ReplayAccess? access = entry is null ? null : context.ReplayAccess(entry);
            if (access is not null) b.Draw(context.Textures.Replay, ReplayBounds(slot), access.Allowed ? Color.White : Color.Gray * .5f);
            if (access is not null && ReplayBounds(slot).Contains(mouseX, mouseY))
                tooltip = context.I18n.Get(access.Allowed ? "event.replay" : access.ReasonKey ?? "event.locked");
            else if (hovered)
                tooltip = $"{row.Title}\nID {row.EventId}\n{row.Characters}\n{row.Location}";
        }
        if (rows.Count == 0)
            GalleryDrawing.DrawCentered(b, context.I18n.Get("nav.no-results"), new Rectangle(240, 370, 960, 160));
        if (MaxScroll > 0)
        {
            GalleryDrawing.DrawScrollbarTrack(b, context.Textures.Scrollbar, ScrollTrack);
            GalleryDrawing.DrawScrollbar(b, scrollThumb);
        }
        GalleryDrawing.DrawButton(b, BackBounds, context.I18n.Get("nav.back"));
        GalleryDrawing.DrawCentered(b, context.I18n.Get("query.count", new { count = rows.Count }), new Rectangle(352, 784, 492, 48));
        if (state.Filter != new QueryFilter())
            GalleryDrawing.DrawButton(b, LocationBounds, context.I18n.Get("filter.reset"));
        upperRightCloseButton?.draw(b);
        GalleryDrawing.EndScaled(b);
        if (tooltip is not null) drawHoverText(b, Game1.parseText(tooltip, Game1.smallFont, 620), Game1.smallFont);
        drawMouse(b);
    }

    private void Column(SpriteBatch b, string key, int x, int columnWidth)
        => GalleryDrawing.DrawLeftFitted(b, context.I18n.Get(key), new Rectangle(x, 172, columnWidth, 34));
    private Rectangle ToScreen(Rectangle bounds) => GalleryDrawing.ScaleRectangle(bounds, scale, offsetX, offsetY);
    private (int X, int Y) ToLogical(int x, int y) => ((int)Math.Round((x - offsetX) / scale), (int)Math.Round((y - offsetY) / scale));
}
