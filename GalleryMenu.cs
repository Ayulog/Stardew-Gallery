using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using static StardewGallery.GalleryDrawing;
using StardewGallery.Appearance;

namespace StardewGallery;

internal sealed class GalleryMenu : IClickableMenu, IGallerySearchMenu
{
    private const int Columns = 6;
    private const int VisibleRows = 3;
    private const int PageSize = Columns * VisibleRows;
    private const int SearchComponentId = 1000;
    private const int UnlockComponentId = 1001;
    private const int YesComponentId = 1002;
    private const int NoComponentId = 1003;
    private readonly GalleryViewContext context;
    private readonly GalleryPageState state;
    private readonly GalleryCatalog catalog;
    private readonly ITranslationHelper i18n;
    private readonly Texture2D background;
    private readonly Func<bool> isUnlocked;
    private readonly TextBox search;
    private readonly GallerySearchFilter searchFilter = new();
    private string validatedSearchText = "";
    private List<GalleryCharacter> filtered = [];
    private int ResultCount => filtered.Count;
    private int currentPage;
    private bool confirming;
    private bool keyboardFocus;
    private Point lastMouse;
    private Rectangle searchBounds;
    private Rectangle unlockBounds;
    private Rectangle yesBounds;
    private Rectangle noBounds;
    private int viewportWidth;
    private int viewportHeight;
    private float menuScale = 1f;
    private int drawOffsetX;
    private int drawOffsetY;
    private readonly List<ClickableComponent> cardComponents = [];
    private ClickableComponent? searchComponent;
    private ClickableComponent? unlockComponent;

    public bool IsSearchSelected => search.Selected;
    internal bool IsConfirming => confirming;

    internal GalleryMenu(GalleryViewContext context, GalleryPageState state)
        : base(0, 0, MenuWidth, MenuHeight, true)
    {
        this.context = context;
        this.state = state;
        HashSet<string> heartOwners = context.Catalog.Events
            .Where(entry => entry.Kind == StoryKind.Heart)
            .SelectMany(entry => entry.Ownership.Owners.Select(owner => owner.Name))
            .ToHashSet(StringComparer.Ordinal);
        catalog = new GalleryCatalog(
            context.Catalog.Characters.Where(character => heartOwners.Contains(character.Name)).ToArray(),
            context.Catalog.Events.Where(entry => entry.Kind == StoryKind.Heart).ToArray(),
            []);
        i18n = context.I18n;
        background = context.Textures.Home;
        isUnlocked = context.IsUnlocked;
        search = new GalleryTextBox { DrawFrame = GalleryUiSkin.Get(GalleryUiSkin.SearchBoxAsset) is null };
        search.OnEnterPressed += _ => FinishSearch();
        search.Text = state.SearchText;
        RecalculateLayout();
        RefreshFilter();
        currentPage = GalleryPagination.ClampPage(state.Scroll, ResultCount, PageSize);
        PreparePortraits();
        BuildClickableComponents();
        currentlySnappedComponent = allClickableComponents.FirstOrDefault(component => component.myID == state.Focus)
            ?? cardComponents.FirstOrDefault() ?? searchComponent;
        if (Game1.options.snappyMenus && Game1.options.gamepadControls)
            snapCursorToCurrentSnappedComponent();
    }

    public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
    {
        base.gameWindowSizeChanged(oldBounds, newBounds);
        RecalculateLayout();
    }

    public override void performHoverAction(int x, int y)
    {
        if (lastMouse != new Point(x, y))
        {
            if (lastMouse != default) keyboardFocus = false;
            lastMouse = new Point(x, y);
        }
        base.performHoverAction(x, y);
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        (x, y) = ToLogical(x, y);
        if (upperRightCloseButton?.bounds.Contains(x, y) == true)
        {
            SaveState();
            DeselectSearch();
            context.Navigation.Close();
            return;
        }
        if (confirming)
        {
            if (yesBounds.Contains(x, y))
            {
                SaveState();
                context.Navigation.ToggleUnlock();
                confirming = false;
                BuildClickableComponents();
                SnapForGamepad();
                Game1.playSound("coin");
            }
            else if (noBounds.Contains(x, y))
            {
                confirming = false;
                BuildClickableComponents();
                SnapForGamepad();
                Game1.playSound("bigDeSelect");
            }
            return;
        }

        if (GalleryBookChrome.TryClickRibbonTab(x, y, GalleryBookTab.Villagers, context.Navigation, () =>
            {
                SaveState();
                DeselectSearch();
            }))
        {
            return;
        }

        if (unlockBounds.Contains(x, y))
        {
            confirming = true;
            DeselectSearch();
            BuildClickableComponents();
            SnapForGamepad();
            Game1.playSound("smallSelect");
            return;
        }
        if (searchBounds.Contains(x, y))
        {
            GalleryTextBox.SelectForInput(search);
            return;
        }
        DeselectSearch();

        if (GalleryBookChrome.ToRect(GallerySpreadLayout.SpreadPrevBounds).Contains(x, y))
        {
            TurnPage(-1);
            return;
        }
        if (GalleryBookChrome.ToRect(GallerySpreadLayout.SpreadNextBounds).Contains(x, y))
        {
            TurnPage(1);
            return;
        }

        int first = GalleryPagination.FirstIndex(currentPage, ResultCount, PageSize);
        int visible = GalleryPagination.VisibleCount(currentPage, ResultCount, PageSize);
        for (int slot = 0; slot < visible; slot++)
        {
            if (!Card(slot).Contains(x, y))
                continue;
            GalleryCharacter character = filtered[first + slot];
            if (!character.IsMet && !isUnlocked())
                return;
            Game1.playSound("smallSelect");
            SaveState(slot);
            context.Navigation.OpenCharacter(character.Name);
            return;
        }
    }

    public override void receiveScrollWheelAction(int direction) => TurnPage(direction < 0 ? 1 : -1);

    public override void receiveKeyPress(Keys key)
    {
        if (search.Selected)
        {
            if (key == Keys.Escape)
                DeselectSearch();
            return;
        }
        if (key == Keys.Escape || Game1.options.menuButton.Any(binding => binding.key == key))
        {
            HandleControllerBack();
            return;
        }
        if (key is Keys.PageUp or Keys.PageDown)
        {
            keyboardFocus = true;
            TurnPage(key == Keys.PageUp ? -1 : 1);
            return;
        }
        if (key is Keys.Up or Keys.Right or Keys.Down or Keys.Left)
        {
            keyboardFocus = true;
            applyMovementKey(key switch { Keys.Up => 0, Keys.Right => 1, Keys.Down => 2, _ => 3 });
            RefreshFilter();
            return;
        }
        if (key == Keys.Enter)
        {
            keyboardFocus = true;
            RefreshFilter();
            ActivateFocused();
            return;
        }
        base.receiveKeyPress(key);
        RefreshFilter();
    }

    public void HandleControllerBack()
    {
        if (search.Selected)
        {
            DeselectSearch();
            Game1.playSound("bigDeSelect");
            return;
        }
        if (confirming)
        {
            confirming = false;
            BuildClickableComponents();
            SnapForGamepad();
            Game1.playSound("bigDeSelect");
            return;
        }
        SaveState();
        context.Navigation.Back();
        Game1.playSound("bigDeSelect");
    }

    public override void update(GameTime time)
    {
        base.update(time);
        RefreshFilter();
        PreparePortraits();
        SaveState();
    }

    public override void receiveRightClick(int x, int y, bool playSound = true) => HandleControllerBack();

    public override void receiveGamePadButton(Buttons button)
    {
        if (button == Buttons.B)
        {
            HandleControllerBack();
            return;
        }
        if (!confirming && button is Buttons.LeftShoulder or Buttons.RightShoulder)
        {
            DeselectSearch();
            TurnPage(button == Buttons.LeftShoulder ? -1 : 1);
            return;
        }
        base.receiveGamePadButton(button);
    }

    // Game1 repopulates components by reflection when input switches to a controller; rebuild the gallery's own instead.
    public override void populateClickableComponentList() => BuildClickableComponents();

    public override void snapCursorToCurrentSnappedComponent()
    {
        if (Game1.options.snappyMenus && Game1.options.gamepadControls)
            base.snapCursorToCurrentSnappedComponent();
    }

    public override void snapToDefaultClickableComponent()
    {
        currentlySnappedComponent = confirming
            ? allClickableComponents?.FirstOrDefault(component => component.myID == YesComponentId)
            : cardComponents.FirstOrDefault() ?? searchComponent;
        snapCursorToCurrentSnappedComponent();
    }

    public override void draw(SpriteBatch b)
    {
        EnsureLayout();
        b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height), Color.Black * .45f);
        if (confirming)
            b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height), Color.Black * .7f);

        (int mouseX, int mouseY) = ToLogical(Game1.getMouseX(true), Game1.getMouseY(true));
        bool showFocus = Game1.options.snappyMenus && Game1.options.gamepadControls || keyboardFocus;
        int focusedId = currentlySnappedComponent?.myID ?? -1;

        BeginScaled(b, menuScale, drawOffsetX, drawOffsetY);
        GalleryBookChrome.DrawCleanHomeSpread(b, background, width, height);
        GalleryBookChrome.DrawRibbonTabs(b, GalleryBookTab.Villagers, focusedId, showFocus && !confirming, new Point(mouseX, mouseY), i18n, out string? hoverTooltip);
        GalleryBookChrome.DrawSpreadTitle(b, i18n.Get("home.title"), menuScale, drawOffsetX, drawOffsetY);

        bool unlockHot = !confirming && (unlockBounds.Contains(mouseX, mouseY) || showFocus && focusedId == UnlockComponentId);
        DrawButton(b, unlockBounds, i18n.Get(isUnlocked() ? "menu.restore-locks" : "menu.unlock-all"), unlockHot);

        bool searchHot = search.Selected || searchBounds.Contains(mouseX, mouseY) || showFocus && !confirming && focusedId == SearchComponentId;
        GalleryUiSkin.DrawSearchBox(b, searchBounds, searchHot);
        search.Draw(b);
        if (search.Text.Length == 0 && !search.Selected)
            DrawLeftFitted(b, i18n.Get("home.search"), GalleryDrawing.Inset(GalleryUiSkin.SearchTextBounds(searchBounds), 4, 10), GallerySpreadDrawing.LightInk);

        int first = GalleryPagination.FirstIndex(currentPage, ResultCount, PageSize);
        int visible = GalleryPagination.VisibleCount(currentPage, ResultCount, PageSize);
        for (int slot = 0; slot < visible; slot++)
        {
            Rectangle card = Card(slot);
            GalleryCharacter character = filtered[first + slot];
            bool hot = !confirming && (card.Contains(mouseX, mouseY) || showFocus && focusedId == slot);
            DrawCharacter(b, slot, character, hot);
            if (hot)
            {
                string? cardTip = CharacterTooltip(character);
                if (cardTip is not null)
                    hoverTooltip = cardTip;
            }
        }
        if (ResultCount == 0 && search.Text.Trim().Length > 0)
            DrawCentered(b, Game1.parseText(i18n.Get("query.home-empty"), Game1.smallFont, 540), R(185, 290, 480, 180));

        GalleryBookChrome.DrawPager(
            b,
            GallerySpreadLayout.SpreadPrevBounds,
            GallerySpreadLayout.SpreadIndicatorBounds,
            GallerySpreadLayout.SpreadNextBounds,
            currentPage,
            ResultCount,
            PageSize,
            focusedId,
            showFocus && !confirming,
            confirming ? null : new Point(mouseX, mouseY));

        upperRightCloseButton?.draw(b);
        if (confirming)
            DrawConfirmation(b, mouseX, mouseY, showFocus, focusedId);
        EndScaled(b);
        if (!confirming && hoverTooltip is not null)
            drawHoverText(b, Game1.parseText(hoverTooltip, Game1.smallFont, Math.Max(200, Math.Min(520, viewportWidth - 80))), Game1.smallFont);
        drawMouse(b);
    }

    protected override void cleanupBeforeExit()
    {
        DeselectSearch();
        base.cleanupBeforeExit();
    }

    private void TurnPage(int delta)
    {
        var (nextPage, changed) = GalleryPagination.Turn(currentPage, delta, ResultCount, PageSize);
        if (!changed)
            return;
        currentPage = nextPage;
        PreparePortraits();
        BuildClickableComponents();
        if (Game1.options.snappyMenus && Game1.options.gamepadControls)
            snapCursorToCurrentSnappedComponent();
        Game1.playSound("shwip");
    }

    private void RecalculateLayout()
    {
        width = MenuWidth;
        height = MenuHeight;
        xPositionOnScreen = 0;
        yPositionOnScreen = 0;
        menuScale = (float)GalleryLayout.ScaleToFit(Game1.uiViewport.Width, Game1.uiViewport.Height, width, height, 24);
        drawOffsetX = (int)Math.Round((Game1.uiViewport.Width - width * menuScale) / 2f);
        drawOffsetY = (int)Math.Round((Game1.uiViewport.Height - height * menuScale) / 2f);
        viewportWidth = Game1.uiViewport.Width;
        viewportHeight = Game1.uiViewport.Height;
        searchBounds = GalleryBookChrome.ToRect(GallerySpreadLayout.HomeSearchBounds);
        Rectangle textArea = GalleryUiSkin.SearchTextBounds(searchBounds);
        search.X = textArea.X - 12;
        search.Y = searchBounds.Y;
        search.Width = textArea.Width + 12;
        search.Height = searchBounds.Height;
        unlockBounds = GalleryBookChrome.ToRect(GallerySpreadLayout.HomeUnlockBounds);
        yesBounds = R(650, 540, 160, 56);
        noBounds = R(860, 540, 160, 56);
        initializeUpperRightCloseButton();
        GalleryBookChrome.PositionCloseButton(upperRightCloseButton, menuScale, drawOffsetX, drawOffsetY, viewportWidth, viewportHeight);
        BuildClickableComponents();
    }

    private static Rectangle Card(int slot) => GalleryBookChrome.ToRect(GallerySpreadLayout.HomeCardBounds(slot));

    /// <summary>Grid position of a page slot: columns 0-2 on the left page, 3-5 on the right page.</summary>
    private static (int Column, int Row) GridOf(int slot) => slot < 9 ? (slot % 3, slot / 3) : (3 + (slot - 9) % 3, (slot - 9) / 3);

    private static int SlotAt(int column, int row) => column < 3 ? row * 3 + column : 9 + row * 3 + column - 3;

    private void DrawCharacter(SpriteBatch b, int slot, GalleryCharacter character, bool hot)
    {
        bool known = character.IsMet || isUnlocked();
        Rectangle portraitBounds = GalleryBookChrome.ToRect(GallerySpreadLayout.HomePortraitBounds(slot));
        Rectangle card = Card(slot);

        context.Appearance.Draw(b, character.Name, CharacterVisual.Portrait,
            portraitBounds, known ? Color.White : new Color(92, 60, 36) * 0.92f);
        if (hot)
            GalleryUiSkin.DrawFocusCorners(b, GalleryDrawing.Inset(portraitBounds, -8, -8), 16);
        string name = known ? character.DisplayName : i18n.Get("home.unmet").ToString();
        DrawCentered(b, name, new Rectangle(card.X, portraitBounds.Bottom + 4, card.Width, 28), known ? Game1.textColor : GallerySpreadDrawing.LightInk);

        if (!known)
            return;

        StoryNodeProgress progress = context.CharacterProgress(character.Name);
        string progressLabel = $"{progress.CollectedNodes} / {progress.TotalNodes}";
        Rectangle progressBounds = new(card.X + 20, portraitBounds.Bottom + 32, card.Width - 40, 24);
        DrawCentered(b, progressLabel, progressBounds, GallerySpreadDrawing.Ink);
        if (progress.IsFullyCollected)
        {
            Vector2 size = Game1.smallFont.MeasureString(progressLabel);
            float scale = GalleryTextFit.Scale(size.X, size.Y, progressBounds.Width, progressBounds.Height);
            int textLeft = progressBounds.Center.X - (int)(size.X * scale / 2);
            GalleryBookChrome.DrawGoldStarBadge(b, new Rectangle(textLeft - 26, progressBounds.Center.Y - 11, 22, 22));
        }

        EncounterClue clue = context.CharacterClue(character.Name);
        if (GalleryUiSkin.ClueBadge(clue) is { } badge)
            GalleryUiSkin.DrawBadge(b, badge, new Rectangle(portraitBounds.Right - 12, portraitBounds.Y - 12, 30, 30));
    }

    private string? CharacterTooltip(GalleryCharacter character)
    {
        if (!character.IsMet && !isUnlocked())
            return null;

        List<string> lines = [];
        StoryNodeProgress progress = context.CharacterProgress(character.Name);
        if (progress.IsFullyCollected)
            lines.Add(i18n.Get("home.full-collection", new { name = character.DisplayName }));

        EncounterClue clue = context.CharacterClue(character.Name);
        if (clue == EncounterClue.AvailableToday)
            lines.Add(i18n.Get("clue.available-today-hint"));
        else if (clue == EncounterClue.ReadyForTiming)
            lines.Add(i18n.Get("clue.ready-for-timing-hint"));

        return lines.Count > 0 ? string.Join("\n", lines) : null;
    }

    private void PreparePortraits()
    {
        int first = GalleryPagination.FirstIndex(currentPage, ResultCount, PageSize);
        int visible = GalleryPagination.VisibleCount(currentPage, ResultCount, PageSize);
        for (int i = first; i < first + visible; i++)
            context.Appearance.Prepare(filtered[i].Name, CharacterVisual.Portrait);
    }

    private void RefreshFilter()
    {
        if (search.Text != validatedSearchText)
        {
            search.Text = GallerySearchInput.CleanText(search.Text);
            validatedSearchText = search.Text;
        }
        bool changed = searchFilter.Update(catalog, search.Text, i18n.Locale,
            LocalizedContentManager.CurrentLanguageCode == LocalizedContentManager.LanguageCode.zh, context.Pinyin);
        filtered = searchFilter.Results;
        if (!changed)
            return;
        currentPage = 0;
        PreparePortraits();
        BuildClickableComponents();
    }

    public void FinishSearch()
    {
        RefreshFilter();
        DeselectSearch();
        // Show the matching characters with the first one focused; a second Enter or A opens it.
        currentlySnappedComponent = cardComponents.FirstOrDefault() ?? searchComponent;
        if (Game1.options.snappyMenus && Game1.options.gamepadControls)
            snapCursorToCurrentSnappedComponent();
    }

    private void ActivateFocused()
    {
        int id = currentlySnappedComponent?.myID ?? -1;
        if (confirming)
        {
            if (id == YesComponentId)
            {
                SaveState();
                context.Navigation.ToggleUnlock();
                confirming = false;
                BuildClickableComponents();
                Game1.playSound("coin");
            }
            else if (id == NoComponentId)
            {
                confirming = false;
                BuildClickableComponents();
                Game1.playSound("bigDeSelect");
            }
            return;
        }

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
        if (id == UnlockComponentId)
        {
            confirming = true;
            DeselectSearch();
            BuildClickableComponents();
            Game1.playSound("smallSelect");
            return;
        }
        if (id == SearchComponentId)
        {
            GalleryTextBox.SelectForInput(search);
            return;
        }

        int slot = id is >= 0 and < PageSize ? id : 0;
        int index = GalleryPagination.FirstIndex(currentPage, ResultCount, PageSize) + slot;
        if (index >= ResultCount)
            return;
        GalleryCharacter character = filtered[index];
        if (!character.IsMet && !isUnlocked())
            return;
        SaveState(slot);
        DeselectSearch();
        context.Navigation.OpenCharacter(character.Name);
    }

    private void SaveState(int? focus = null)
    {
        state.SearchText = search.Text;
        state.Scroll = currentPage;
        state.Focus = focus ?? currentlySnappedComponent?.myID ?? -1;
    }

    private void BuildClickableComponents()
    {
        int previousId = currentlySnappedComponent?.myID ?? -1;
        allClickableComponents = [];
        cardComponents.Clear();
        if (confirming)
        {
            ClickableComponent yes = new(ToScreen(yesBounds), "yes") { myID = YesComponentId, rightNeighborID = NoComponentId, fullyImmutable = true };
            ClickableComponent no = new(ToScreen(noBounds), "no") { myID = NoComponentId, leftNeighborID = YesComponentId, fullyImmutable = true };
            allClickableComponents.Add(yes);
            allClickableComponents.Add(no);
        }
        else
        {
            currentPage = GalleryPagination.ClampPage(currentPage, ResultCount, PageSize);
            int visible = GalleryPagination.VisibleCount(currentPage, ResultCount, PageSize);
            int SlotOrNone(int column, int row)
            {
                if (column < 0 || column > 5 || row < 0 || row > 2) return -1;
                int s = SlotAt(column, row);
                return s < visible ? s : -1;
            }
            // Lowest visible card in a column, used when moving up from the ribbons, pager or unlock button.
            int LowestIn(int column)
            {
                for (int row = 2; row >= 0; row--)
                    if (SlotOrNone(column, row) is >= 0 and var s) return s;
                return visible > 0 ? (column < 3 ? 0 : Math.Min(9, visible - 1)) : SearchComponentId;
            }

            GalleryBookChrome.AddRibbonTabComponents(allClickableComponents, ToScreen, GalleryBookChrome.PrevPageComponentId,
                upNeighborId: LowestIn(0));
            foreach (ClickableComponent ribbon in allClickableComponents)
                ribbon.upNeighborID = LowestIn(ribbon.myID - GalleryBookChrome.RibbonTabBaseId);

            searchComponent = new ClickableComponent(ToScreen(searchBounds), "search")
            {
                myID = SearchComponentId,
                rightNeighborID = visible > 9 ? 9 : upperRightCloseButton?.myID ?? -1,
                downNeighborID = visible > 0 ? 0 : GalleryBookChrome.RibbonTabBaseId,
                fullyImmutable = true
            };
            unlockComponent = new ClickableComponent(ToScreen(unlockBounds), "unlock")
            {
                myID = UnlockComponentId,
                leftNeighborID = GalleryBookChrome.NextPageComponentId,
                rightNeighborID = -1,
                upNeighborID = LowestIn(5),
                downNeighborID = -1,
                fullyImmutable = true
            };
            allClickableComponents.Add(searchComponent);
            allClickableComponents.Add(unlockComponent);
            allClickableComponents.Add(new ClickableComponent(ToScreen(GalleryBookChrome.ToRect(GallerySpreadLayout.SpreadPrevBounds)), "prev-page")
            {
                myID = GalleryBookChrome.PrevPageComponentId,
                upNeighborID = LowestIn(3),
                leftNeighborID = GalleryBookChrome.RibbonTabBaseId + 2,
                rightNeighborID = GalleryBookChrome.NextPageComponentId,
                fullyImmutable = true
            });
            allClickableComponents.Add(new ClickableComponent(ToScreen(GalleryBookChrome.ToRect(GallerySpreadLayout.SpreadNextBounds)), "next-page")
            {
                myID = GalleryBookChrome.NextPageComponentId,
                upNeighborID = LowestIn(4),
                leftNeighborID = GalleryBookChrome.PrevPageComponentId,
                rightNeighborID = UnlockComponentId,
                fullyImmutable = true
            });

            for (int slot = 0; slot < visible; slot++)
            {
                (int col, int row) = GridOf(slot);
                int left = SlotOrNone(col - 1, row);
                if (left < 0 && col == 3) left = SlotOrNone(2, row) is >= 0 and var l ? l : LowestIn(2);
                int right = SlotOrNone(col + 1, row);
                if (right < 0 && col == 2) right = SlotOrNone(3, row) is >= 0 and var r ? r : (visible > 9 ? 9 : -1);
                int down = SlotOrNone(col, row + 1);
                if (down < 0)
                    down = col < 3 ? GalleryBookChrome.RibbonTabBaseId + col : col == 3 ? GalleryBookChrome.PrevPageComponentId
                        : col == 4 ? GalleryBookChrome.NextPageComponentId : UnlockComponentId;
                int up = SlotOrNone(col, row - 1);
                if (up < 0)
                    up = SearchComponentId;
                ClickableComponent card = new(ToScreen(Card(slot)), $"character-{slot}")
                {
                    myID = slot,
                    leftNeighborID = left,
                    rightNeighborID = right,
                    upNeighborID = up,
                    downNeighborID = down,
                    fullyImmutable = true
                };
                cardComponents.Add(card);
                allClickableComponents.Add(card);
            }
        }
        if (upperRightCloseButton is not null)
        {
            upperRightCloseButton.leftNeighborID = confirming ? YesComponentId : SearchComponentId;
            upperRightCloseButton.downNeighborID = confirming ? YesComponentId : cardComponents.ElementAtOrDefault(9)?.myID ?? UnlockComponentId;
            allClickableComponents.Add(new ClickableComponent(ToScreen(upperRightCloseButton.bounds), upperRightCloseButton.name)
            {
                myID = upperRightCloseButton.myID,
                leftNeighborID = upperRightCloseButton.leftNeighborID,
                downNeighborID = upperRightCloseButton.downNeighborID,
                fullyImmutable = true
            });
        }
        currentlySnappedComponent = allClickableComponents.FirstOrDefault(component => component.myID == previousId)
            ?? cardComponents.FirstOrDefault()
            ?? searchComponent;
    }

    private void SnapForGamepad()
    {
        if (Game1.options.snappyMenus && Game1.options.gamepadControls)
            snapToDefaultClickableComponent();
    }

    public void DeselectSearch()
    {
        search.Selected = false;
        if (Game1.keyboardDispatcher.Subscriber == search)
            Game1.keyboardDispatcher.Subscriber = null;
        currentlySnappedComponent = searchComponent;
        if (Game1.options.snappyMenus && Game1.options.gamepadControls)
            snapCursorToCurrentSnappedComponent();
    }

    private void EnsureLayout()
    {
        if (GalleryLayout.Changed(viewportWidth, viewportHeight, Game1.uiViewport.Width, Game1.uiViewport.Height))
            RecalculateLayout();
    }

    private void DrawConfirmation(SpriteBatch b, int mouseX, int mouseY, bool showFocus, int focusedId)
    {
        Rectangle panel = R(460, 330, 750, 300);
        IClickableMenu.drawTextureBox(b, panel.X, panel.Y, panel.Width, panel.Height, Color.White);
        string message = Game1.parseText(i18n.Get(isUnlocked() ? "confirm.restore" : "confirm.unlock"), Game1.smallFont, panel.Width - 100);
        b.DrawString(Game1.smallFont, message, new Vector2(panel.X + 50, panel.Y + 55), Game1.textColor);
        DrawButton(b, yesBounds, i18n.Get("common.yes"), yesBounds.Contains(mouseX, mouseY) || showFocus && focusedId == YesComponentId);
        DrawButton(b, noBounds, i18n.Get("common.no"), noBounds.Contains(mouseX, mouseY) || showFocus && focusedId == NoComponentId);
    }

    private Rectangle R(int x, int y, int w, int h) => new(xPositionOnScreen + x, yPositionOnScreen + y, w, h);

    private Rectangle ToScreen(Rectangle bounds) => ScaleRectangle(bounds, menuScale, drawOffsetX, drawOffsetY);

    private (int X, int Y) ToLogical(int x, int y)
        => ((int)Math.Round((x - drawOffsetX) / menuScale), (int)Math.Round((y - drawOffsetY) / menuScale));
}
