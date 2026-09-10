using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;
using static StardewGallery.GalleryDrawing;
using StardewGallery.Appearance;

namespace StardewGallery;

internal sealed class GalleryMenu : IClickableMenu, IGallerySearchMenu
{
    private const int Columns = 6;
    private const int VisibleRows = 3;
    private const int SearchComponentId = 1000;
    private const int UnlockComponentId = 1001;
    private const int YesComponentId = 1002;
    private const int NoComponentId = 1003;
    private const int QueryComponentId = 1004;
    private readonly GalleryViewContext context;
    private readonly GalleryPageState state;
    private readonly GalleryCatalog catalog;
    private readonly ITranslationHelper i18n;
    private readonly Texture2D background;
    private readonly Texture2D scrollbarTrackTexture;
    private readonly Func<bool> isUnlocked;
    private readonly TextBox search;
    private readonly GallerySearchFilter searchFilter = new();
    private readonly Dictionary<string, int> heartCounts;
    private string validatedSearchText = "";
    private List<GalleryCharacter> filtered = [];
    private int ResultCount => filtered.Count;
    private int scrollRow;
    private bool dragging;
    private int dragOffset;
    private bool confirming;
    private Rectangle searchBounds;
    private Rectangle unlockBounds;
    private Rectangle queryBounds;
    private Rectangle scrollTrack;
    private Rectangle scrollThumb;
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
        heartCounts = context.Catalog.Events.Where(entry => entry.Kind == StoryKind.Heart).SelectMany(entry => entry.Ownership.Owners.Select(owner => owner.Name))
            .GroupBy(name => name, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        catalog = new GalleryCatalog(context.Catalog.Characters.Where(character => heartCounts.GetValueOrDefault(character.Name) > 0).ToArray(),
            context.Catalog.Events.Where(entry => entry.Kind == StoryKind.Heart).ToArray(), []);
        i18n = context.I18n;
        background = context.Textures.Home;
        scrollbarTrackTexture = context.Textures.Scrollbar;
        isUnlocked = context.IsUnlocked;
        search = new GalleryTextBox();
        search.OnEnterPressed += _ => OpenFirstMatch();
        search.Text = state.SearchText;
        RecalculateLayout();
        RefreshFilter();
        scrollRow = Math.Clamp(state.Scroll, 0, MaxScroll);
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

        if (queryBounds.Contains(x, y))
        {
            SaveState(QueryComponentId);
            DeselectSearch();
            context.Navigation.OpenQuery();
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
            if (GallerySearchInputGuard.Ready)
                search.SelectMe();
            return;
        }
        DeselectSearch();

        int first = scrollRow * Columns;
        for (int slot = 0; slot < Columns * VisibleRows && first + slot < ResultCount; slot++)
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

        if (scrollThumb.Contains(x, y))
        {
            dragging = true;
            dragOffset = y - scrollThumb.Y;
        }
        else if (scrollTrack.Contains(x, y))
        {
            scrollRow += y < scrollThumb.Y ? -VisibleRows : VisibleRows;
            UpdateScrollbar();
            BuildClickableComponents();
        }
    }

    public override void leftClickHeld(int x, int y)
    {
        (x, y) = ToLogical(x, y);
        if (!dragging || MaxScroll == 0)
            return;
        int travel = scrollTrack.Height - scrollThumb.Height;
        scrollRow = (int)Math.Round(Math.Clamp(y - dragOffset - scrollTrack.Y, 0, travel) / (double)travel * MaxScroll);
        UpdateScrollbar();
        BuildClickableComponents();
    }

    public override void releaseLeftClick(int x, int y)
    {
        dragging = false;
        base.releaseLeftClick(x, y);
    }

    public override void receiveScrollWheelAction(int direction)
    {
        scrollRow = Math.Clamp(scrollRow + (direction < 0 ? 1 : -1), 0, MaxScroll);
        UpdateScrollbar();
        BuildClickableComponents();
        Game1.playSound("shiny4");
    }

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
        base.receiveKeyPress(key);
        RefreshFilter();
        if (key == Keys.Enter)
            OpenFirstMatch();
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
        if (button == Buttons.B) HandleControllerBack();
        else base.receiveGamePadButton(button);
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
        BeginScaled(b, menuScale, drawOffsetX, drawOffsetY);
        b.Draw(background, new Rectangle(xPositionOnScreen, yPositionOnScreen, width, height), Color.White);
        DrawScrollbarTrack(b, scrollbarTrackTexture, scrollTrack);
        SpriteText.drawStringHorizontallyCenteredAt(b, i18n.Get("home.title"), xPositionOnScreen + width / 2, yPositionOnScreen + 40, maxWidth: 430);
        DrawButton(b, unlockBounds, i18n.Get(isUnlocked() ? "menu.restore-locks" : "menu.unlock-all"));
        DrawButton(b, queryBounds, i18n.Get("query.open"));
        search.Draw(b);
        if (search.Text.Length == 0 && !search.Selected)
            DrawLeftFitted(b, i18n.Get(GallerySearchInputGuard.Ready ? "home.search" : "home.search-unavailable"),
                new Rectangle(searchBounds.X + 12, searchBounds.Y + 8, searchBounds.Width - 24, searchBounds.Height - 16));

        int first = scrollRow * Columns;
        for (int slot = 0; slot < Columns * VisibleRows && first + slot < ResultCount; slot++)
            DrawCharacter(b, Card(slot), filtered[first + slot]);
        if (ResultCount == 0 && search.Text.Trim().Length > 0)
            DrawCentered(b, Game1.parseText(i18n.Get("query.home-empty"), Game1.smallFont, 540), R(200, 290, 540, 180));
        UpdateScrollbar();
        if (MaxScroll > 0)
            DrawScrollbar(b, scrollThumb);
        upperRightCloseButton?.draw(b);
        if (confirming)
            DrawConfirmation(b);
        EndScaled(b);
        drawMouse(b);
    }

    protected override void cleanupBeforeExit()
    {
        DeselectSearch();
        base.cleanupBeforeExit();
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
        var footerButton = GallerySpreadLayout.BackButtonBounds;
        searchBounds = R(268, 136, footerButton.Width, 48);
        search.X = searchBounds.X;
        search.Y = searchBounds.Y;
        search.Width = searchBounds.Width;
        search.Height = searchBounds.Height;
        queryBounds = R(938, 818, 260, 58);
        unlockBounds = R(1220, 818, queryBounds.Width, queryBounds.Height);
        scrollTrack = R(1534, 146, 24, 640);
        yesBounds = R(650, 540, 160, 56);
        noBounds = R(860, 540, 160, 56);
        initializeUpperRightCloseButton();
        UpdateScrollbar();
        BuildClickableComponents();
    }

    private Rectangle Card(int slot)
    {
        int col = slot % Columns;
        int row = slot / Columns;
        int x = col < 3 ? 192 + col * 190 : 938 + (col - 3) * 190;
        return R(x, 230 + row * 190, 140, 165);
    }

    private void DrawCharacter(SpriteBatch b, Rectangle card, GalleryCharacter character)
    {
        bool known = character.IsMet || isUnlocked();
        context.Appearance.Draw(b, character.Name, CharacterVisual.Portrait,
            new Rectangle(card.Center.X - 48, card.Y, 96, 96), known ? Color.White : Color.Black * .82f);
        DrawCentered(b, GalleryUiRules.DisplayName(character.DisplayName, character.IsMet, isUnlocked()), new Rectangle(card.X, card.Y + 100, card.Width, 32));
        string count = heartCounts.GetValueOrDefault(character.Name).ToString();
        float countWidth = Math.Min(90, Game1.smallFont.MeasureString(count).X);
        int left = card.Center.X - (int)(countWidth + 30) / 2;
        b.Draw(Game1.mouseCursors, new Rectangle(left, card.Y + 135, 24, 21), new Rectangle(211, 428, 7, 6), Color.White);
        DrawLeftFitted(b, count, new Rectangle(left + 30, card.Y + 132, (int)Math.Ceiling(countWidth), 28));
    }

    private void PreparePortraits()
    {
        int first = scrollRow * Columns;
        for (int i = first; i < Math.Min(ResultCount, first + Columns * VisibleRows); i++)
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
            LocalizedContentManager.CurrentLanguageCode == LocalizedContentManager.LanguageCode.zh);
        filtered = searchFilter.Results;
        if (!changed)
            return;
        scrollRow = 0;
        UpdateScrollbar();
        BuildClickableComponents();
    }

    public void OpenFirstMatch()
    {
        RefreshFilter();
        GalleryCharacter? character = filtered.FirstOrDefault();
        if (character is null || !character.IsMet && !isUnlocked())
            return;
        scrollRow = 0;
        SaveState(0);
        DeselectSearch();
        context.Navigation.OpenCharacter(character.Name);
    }

    private int MaxScroll => Math.Max(0, (ResultCount + Columns - 1) / Columns - VisibleRows);

    private void SaveState(int? focus = null)
    {
        state.SearchText = search.Text;
        state.Scroll = scrollRow;
        state.Focus = focus ?? currentlySnappedComponent?.myID ?? -1;
    }

    private void UpdateScrollbar()
    {
        scrollRow = Math.Clamp(scrollRow, 0, MaxScroll);
        int height = 40;
        int travel = scrollTrack.Height - height;
        int y = MaxScroll == 0 ? scrollTrack.Y : scrollTrack.Y + (int)Math.Round(travel * scrollRow / (double)MaxScroll);
        scrollThumb = new Rectangle(scrollTrack.X, y, scrollTrack.Width, height);
    }

    private void BuildClickableComponents()
    {
        int previousId = currentlySnappedComponent?.myID ?? -1;
        allClickableComponents = [];
        cardComponents.Clear();
        if (confirming)
        {
            ClickableComponent yes = new(ToScreen(yesBounds), "yes") { myID = YesComponentId, rightNeighborID = NoComponentId };
            ClickableComponent no = new(ToScreen(noBounds), "no") { myID = NoComponentId, leftNeighborID = YesComponentId };
            allClickableComponents.Add(yes);
            allClickableComponents.Add(no);
        }
        else
        {
            int visible = Math.Min(Columns * VisibleRows, Math.Max(0, ResultCount - scrollRow * Columns));
            int LastVisibleInColumn(int column) => visible == 0 ? SearchComponentId
                : column >= visible ? visible - 1 : column + (visible - 1 - column) / Columns * Columns;
            searchComponent = new ClickableComponent(ToScreen(searchBounds), "search")
            {
                myID = SearchComponentId,
                rightNeighborID = upperRightCloseButton?.myID ?? -1,
                downNeighborID = visible > 0 ? 0 : QueryComponentId
            };
            unlockComponent = new ClickableComponent(ToScreen(unlockBounds), "unlock")
            {
                myID = UnlockComponentId,
                leftNeighborID = QueryComponentId,
                rightNeighborID = upperRightCloseButton?.myID ?? -1,
                upNeighborID = LastVisibleInColumn(5),
                downNeighborID = -1
            };
            allClickableComponents.Add(searchComponent);
            allClickableComponents.Add(unlockComponent);
            allClickableComponents.Add(new ClickableComponent(ToScreen(queryBounds), "query")
            {
                myID = QueryComponentId, upNeighborID = LastVisibleInColumn(3),
                leftNeighborID = SearchComponentId, rightNeighborID = UnlockComponentId
            });
            for (int slot = 0; slot < visible; slot++)
            {
                int col = slot % Columns;
                int row = slot / Columns;
                ClickableComponent card = new(ToScreen(Card(slot)), $"character-{slot}")
                {
                    myID = slot,
                    leftNeighborID = col > 0 ? slot - 1 : -1,
                    rightNeighborID = col < Columns - 1 && slot + 1 < visible ? slot + 1 : -1,
                    upNeighborID = row > 0 ? slot - Columns : SearchComponentId,
                    downNeighborID = slot + Columns < visible ? slot + Columns : col >= 4 ? UnlockComponentId : QueryComponentId
                };
                cardComponents.Add(card);
                allClickableComponents.Add(card);
            }
        }
        if (upperRightCloseButton is not null)
        {
            upperRightCloseButton.leftNeighborID = confirming ? YesComponentId : SearchComponentId;
            upperRightCloseButton.downNeighborID = confirming ? YesComponentId : cardComponents.ElementAtOrDefault(5)?.myID ?? UnlockComponentId;
            allClickableComponents.Add(new ClickableComponent(ToScreen(upperRightCloseButton.bounds), upperRightCloseButton.name)
            {
                myID = upperRightCloseButton.myID,
                leftNeighborID = upperRightCloseButton.leftNeighborID,
                downNeighborID = upperRightCloseButton.downNeighborID
            });
        }
        currentlySnappedComponent = allClickableComponents.FirstOrDefault(component => component.myID == previousId);
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

    private void DrawConfirmation(SpriteBatch b)
    {
        Rectangle panel = R(460, 330, 750, 300);
        IClickableMenu.drawTextureBox(b, panel.X, panel.Y, panel.Width, panel.Height, Color.White);
        string message = Game1.parseText(i18n.Get(isUnlocked() ? "confirm.restore" : "confirm.unlock"), Game1.smallFont, panel.Width - 100);
        b.DrawString(Game1.smallFont, message, new Vector2(panel.X + 50, panel.Y + 55), Game1.textColor);
        DrawButton(b, yesBounds, i18n.Get("common.yes"));
        DrawButton(b, noBounds, i18n.Get("common.no"));
    }

    private Rectangle R(int x, int y, int w, int h) => new(xPositionOnScreen + x, yPositionOnScreen + y, w, h);

    private Rectangle ToScreen(Rectangle bounds) => ScaleRectangle(bounds, menuScale, drawOffsetX, drawOffsetY);

    private (int X, int Y) ToLogical(int x, int y)
        => ((int)Math.Round((x - drawOffsetX) / menuScale), (int)Math.Round((y - drawOffsetY) / menuScale));

}
