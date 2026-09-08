using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;

namespace StardewGallery;

internal sealed class GalleryMenu : IClickableMenu, IGallerySearchMenu
{
    internal const int MenuWidth = 1672;
    internal const int MenuHeight = 941;
    private const int Columns = 6;
    private const int VisibleRows = 3;
    private const int SearchComponentId = 1000;
    private const int DirectoryComponentId = 1001;
    private readonly GalleryCatalog catalog;
    private readonly ITranslationHelper i18n;
    private readonly Texture2D background;
    private readonly Texture2D eventThumbnail;
    private readonly Texture2D scrollbarTrackTexture;
    private readonly GalleryPhotos photos;
    private readonly Action<GalleryEvent, Action> openOtherEvent;
    private readonly Func<bool> isUnlocked;
    private readonly Action<GalleryCharacter?, string, GalleryEventGroup?, Action> browse;
    private readonly TextBox search;
    private readonly GallerySearchFilter searchFilter = new();
    private readonly Dictionary<string, int> eventCounts;
    private string validatedSearchText = "";
    private List<GalleryCharacter> filtered = [];
    private IReadOnlyList<GalleryEventGroup> otherEvents = [];
    private IReadOnlyList<string> otherLocations = [];
    private string? otherSearchText;
    private string? otherSearchLocale;
    private int ResultCount => filtered.Count + otherEvents.Count;
    private int scrollRow;
    private bool dragging;
    private int dragOffset;
    private Rectangle searchBounds;
    private Rectangle directoryBounds;
    private Rectangle scrollTrack;
    private Rectangle scrollThumb;
    private int viewportWidth;
    private int viewportHeight;
    private float menuScale = 1f;
    private int drawOffsetX;
    private int drawOffsetY;
    private readonly List<ClickableComponent> cardComponents = [];
    private ClickableComponent? searchComponent;
    private ClickableComponent? directoryComponent;

    internal bool IsSearchSelected => search.Selected;
    bool IGallerySearchMenu.IsSearchSelected => IsSearchSelected;
    void IGallerySearchMenu.DeselectSearch() => DeselectSearch();
    void IGallerySearchMenu.OpenFirstMatch() => OpenFirstMatch();
    void IGallerySearchMenu.HandleControllerBack() => HandleControllerBack();

    internal GalleryMenu(
        GalleryCatalog catalog,
        ITranslationHelper i18n,
        Texture2D background,
        Texture2D eventThumbnail,
        Texture2D scrollbarTrackTexture,
        Func<bool> isUnlocked,
        GalleryPhotos photos,
        Action<GalleryEvent, Action> openOtherEvent,
        Action<GalleryCharacter?, string, GalleryEventGroup?, Action> browse,
        string initialSearchText = "",
        int initialScrollRow = 0,
        string? initialFocusCharacterName = null)
        : base(0, 0, MenuWidth, MenuHeight, true)
    {
        this.catalog = catalog;
        this.i18n = i18n;
        this.background = background;
        this.eventThumbnail = eventThumbnail;
        this.scrollbarTrackTexture = scrollbarTrackTexture;
        this.isUnlocked = isUnlocked;
        this.browse = browse;
        this.photos = photos;
        this.openOtherEvent = openOtherEvent;
        var groups = catalog.Groups.Count > 0 ? catalog.Groups : GalleryEventGroup.Build(catalog.Events.Concat(catalog.ExcludedEvents));
        eventCounts = groups.Where(group => !group.IsFlow).SelectMany(group => group.NpcNames)
            .GroupBy(name => name, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        search = new TextBox(Game1.content.Load<Texture2D>("LooseSprites\\textBox"), null, Game1.smallFont, Game1.textColor);
        search.OnEnterPressed += _ => OpenFirstMatch();
        search.Text = initialSearchText;
        RecalculateLayout();
        RefreshFilter();
        RestoreReturnPosition(initialScrollRow, initialFocusCharacterName);
    }

    private void RestoreReturnPosition(int oldScrollRow, string? focusCharacterName)
    {
        scrollRow = Math.Clamp(oldScrollRow, 0, MaxScroll);
        if (focusCharacterName is null)
        {
            BuildClickableComponents();
            SnapForGamepad();
            return;
        }
        int characterIndex = filtered.FindIndex(character => character.Name == focusCharacterName);
        if (characterIndex < 0)
        {
            BuildClickableComponents();
            SnapForGamepad();
            return;
        }
        (int restoredScroll, int visibleSlot) = GalleryUiRules.ResolveReturnPosition(
            characterIndex, oldScrollRow, Columns, VisibleRows, ResultCount);
        scrollRow = restoredScroll;
        BuildClickableComponents();
        if (visibleSlot >= 0)
        {
            currentlySnappedComponent = cardComponents.FirstOrDefault(component => component.myID == visibleSlot);
            if (Game1.options.snappyMenus && Game1.options.gamepadControls)
                snapCursorToCurrentSnappedComponent();
        }
        else
        {
            SnapForGamepad();
        }
    }

    public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
    {
        base.gameWindowSizeChanged(oldBounds, newBounds);
        RecalculateLayout();
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        (x, y) = ToLogical(x, y);
        if (directoryBounds.Contains(x, y))
        {
            DeselectSearch();
            browse(null, "", null, () => Restore(DirectoryComponentId));
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
            if (first + slot >= filtered.Count)
            {
                OpenOtherEvent(first + slot - filtered.Count, slot);
                return;
            }
            GalleryCharacter character = filtered[first + slot];
            if (!character.IsMet && !isUnlocked())
                return;
            Game1.playSound("smallSelect");
            int returnSlot = slot;
            browse(character, search.Text, null, () => Restore(returnSlot));
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
        base.receiveLeftClick(x, y, playSound);
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
        base.receiveKeyPress(key);
        RefreshFilter();
        if (key == Keys.Enter)
            OpenFirstMatch();
    }

    internal void HandleControllerBack()
    {
        if (search.Selected)
        {
            DeselectSearch();
            Game1.playSound("bigDeSelect");
            return;
        }
        Game1.activeClickableMenu = null;
        Game1.playSound("bigDeSelect");
    }

    public override void update(GameTime time)
    {
        base.update(time);
        RefreshFilter();
    }

    public override void snapToDefaultClickableComponent()
    {
        currentlySnappedComponent = cardComponents.FirstOrDefault() ?? searchComponent;
        snapCursorToCurrentSnappedComponent();
    }

    public override void draw(SpriteBatch b)
    {
        EnsureLayout();
        b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height), Color.Black * .45f);
        BeginScaled(b, menuScale, drawOffsetX, drawOffsetY);
        b.Draw(background, new Rectangle(xPositionOnScreen, yPositionOnScreen, width, height), Color.White);
        DrawScrollbarTrack(b, scrollbarTrackTexture, scrollTrack);
        SpriteText.drawStringHorizontallyCenteredAt(b, i18n.Get("home.title"), xPositionOnScreen + width / 2, yPositionOnScreen + 40, maxWidth: 430);
        DrawPageButton(b, directoryBounds, i18n.Get("directory.title"));
        search.Draw(b);
        if (search.Text.Length == 0 && !search.Selected)
            DrawLeftFitted(b, i18n.Get(GallerySearchInputGuard.Ready ? "home.search" : "home.search-unavailable"),
                new Rectangle(searchBounds.X + 12, searchBounds.Y + 8, searchBounds.Width - 24, searchBounds.Height - 16));

        int first = scrollRow * Columns;
        for (int slot = 0; slot < Columns * VisibleRows && first + slot < ResultCount; slot++)
        {
            if (first + slot < filtered.Count)
                DrawCharacter(b, Card(slot), filtered[first + slot]);
            else
            {
                GalleryEvent entry = otherEvents[first + slot - filtered.Count].Representative;
                Rectangle bounds = Card(slot);
                GalleryPhotos.DrawCover(b, photos.Cover(entry.Resolved.Identity) ?? eventThumbnail, new Rectangle(bounds.X, bounds.Y + 8, bounds.Width, 79), Color.White);
                DrawCentered(b, "ID " + entry.EventId, new Rectangle(bounds.X, bounds.Y + 100, bounds.Width, 32));
                DrawCentered(b, otherLocations[first + slot - filtered.Count], new Rectangle(bounds.X, bounds.Y + 132, bounds.Width, 28));
            }
        }
        if (ResultCount == 0 && search.Text.Trim().Length > 0)
            DrawCentered(b, Game1.parseText(i18n.Get("nav.no-results"), Game1.smallFont, 540), R(200, 290, 540, 180));
        UpdateScrollbar();
        if (MaxScroll > 0)
            DrawScrollbar(b, scrollThumb);
        upperRightCloseButton?.draw(b);
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
        directoryBounds = R(1014, 124, footerButton.Width, footerButton.Height);
        scrollTrack = R(1534, 146, 24, 640);
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
        string textureName = NPC.getTextureNameForCharacter(character.Name);
        string asset = $"Portraits\\{textureName}";
        if (Game1.content.DoesAssetExist<Texture2D>(asset))
        {
            Texture2D portrait = Game1.content.Load<Texture2D>(asset);
            Rectangle source = new(0, 0, Math.Min(64, portrait.Width), Math.Min(64, portrait.Height));
            b.Draw(portrait, new Rectangle(card.Center.X - 48, card.Y, 96, 96), source, known ? Color.White : Color.Black * .82f);
        }
        DrawCentered(b, GalleryUiRules.DisplayName(character.DisplayName, character.IsMet, isUnlocked()), new Rectangle(card.X, card.Y + 100, card.Width, 32));
        int count = eventCounts.GetValueOrDefault(character.Name);
        DrawCentered(b, i18n.Get("directory.count", new { count }), new Rectangle(card.X, card.Y + 132, card.Width, 28));
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
        bool otherChanged = otherSearchText != search.Text || otherSearchLocale != i18n.Locale;
        if (!changed && !otherChanged)
            return;
        if (otherChanged)
        {
            otherEvents = GalleryEventGroup.Build(GalleryEventNavigation.SearchOtherIds(catalog, search.Text)
                .Where(entry => entry.RelatedNpcNames.Count == 0));
            otherLocations = otherEvents.Select(group => group.Sources.Count > 1 ? i18n.Get("directory.sources", new { count = group.Sources.Count }).ToString()
                : GalleryConditionPresentation.Location(group.Representative, i18n)).ToArray();
            otherSearchText = search.Text;
            otherSearchLocale = i18n.Locale;
        }
        UpdateScrollbar();
        BuildClickableComponents();
    }

    internal void OpenFirstMatch()
    {
        RefreshFilter();
        if (filtered.Count == 0 && otherEvents.Count > 0)
        {
            scrollRow = 0;
            OpenOtherEvent(0, 0);
            return;
        }
        GalleryCharacter? character = filtered.FirstOrDefault();
        if (character is null || !character.IsMet && !isUnlocked())
            return;
        scrollRow = 0;
        DeselectSearch();
        browse(character, search.Text, null, () => Restore(0));
    }

    private int MaxScroll => Math.Max(0, (ResultCount + Columns - 1) / Columns - VisibleRows);

    private void OpenOtherEvent(int index, int slot)
    {
        DeselectSearch();
        GalleryEventGroup group = otherEvents[index];
        if (group.Sources.Count == 1) openOtherEvent(group.Representative, () => Restore(slot));
        else browse(null, search.Text, group, () => Restore(slot));
    }

    private void Restore(int componentId)
    {
        Game1.activeClickableMenu = this;
        RecalculateLayout();
        currentlySnappedComponent = allClickableComponents.FirstOrDefault(component => component.myID == componentId) ?? searchComponent;
        if (Game1.options.snappyMenus && Game1.options.gamepadControls) snapCursorToCurrentSnappedComponent();
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
        {
            searchComponent = new ClickableComponent(ToScreen(searchBounds), "search")
            {
                myID = SearchComponentId,
                rightNeighborID = DirectoryComponentId,
                downNeighborID = 0
            };
            directoryComponent = new ClickableComponent(ToScreen(directoryBounds), "directory")
            {
                myID = DirectoryComponentId,
                leftNeighborID = SearchComponentId,
                rightNeighborID = upperRightCloseButton?.myID ?? -1,
                upNeighborID = upperRightCloseButton?.myID ?? -1,
                downNeighborID = Math.Min(3, Math.Max(0, ResultCount - 1))
            };
            allClickableComponents.Add(searchComponent);
            allClickableComponents.Add(directoryComponent);
            int visible = Math.Min(Columns * VisibleRows, Math.Max(0, ResultCount - scrollRow * Columns));
            for (int slot = 0; slot < visible; slot++)
            {
                int col = slot % Columns;
                int row = slot / Columns;
                ClickableComponent card = new(ToScreen(Card(slot)), $"character-{slot}")
                {
                    myID = slot,
                    leftNeighborID = col > 0 ? slot - 1 : -1,
                    rightNeighborID = col < Columns - 1 && slot + 1 < visible ? slot + 1 : -1,
                    upNeighborID = row > 0 ? slot - Columns : col < 3 ? SearchComponentId : DirectoryComponentId,
                    downNeighborID = slot + Columns < visible ? slot + Columns : -1
                };
                cardComponents.Add(card);
                allClickableComponents.Add(card);
            }
        }
        if (upperRightCloseButton is not null)
        {
            upperRightCloseButton.leftNeighborID = DirectoryComponentId;
            upperRightCloseButton.downNeighborID = cardComponents.FirstOrDefault()?.myID ?? DirectoryComponentId;
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

    internal void DeselectSearch()
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

    private Rectangle R(int x, int y, int w, int h) => new(xPositionOnScreen + x, yPositionOnScreen + y, w, h);

    private Rectangle ToScreen(Rectangle bounds) => ScaleRectangle(bounds, menuScale, drawOffsetX, drawOffsetY);

    private (int X, int Y) ToLogical(int x, int y)
        => ((int)Math.Round((x - drawOffsetX) / menuScale), (int)Math.Round((y - drawOffsetY) / menuScale));

    internal static Rectangle ScaleRectangle(Rectangle bounds, float scale, int offsetX, int offsetY) => new(
        offsetX + (int)Math.Round(bounds.X * scale),
        offsetY + (int)Math.Round(bounds.Y * scale),
        (int)Math.Round(bounds.Width * scale),
        (int)Math.Round(bounds.Height * scale));

    internal static void BeginScaled(SpriteBatch b, float scale, int offsetX, int offsetY)
    {
        b.End();
        Matrix transform = Matrix.CreateScale(scale) * Matrix.CreateTranslation(offsetX, offsetY, 0f);
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, transform);
    }

    internal static void EndScaled(SpriteBatch b)
    {
        b.End();
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
    }

    internal static void DrawButton(SpriteBatch b, Rectangle bounds, string text)
    {
        IClickableMenu.drawTextureBox(b, bounds.X, bounds.Y, bounds.Width, bounds.Height, Color.White);
        DrawCentered(b, text, new Rectangle(bounds.X + 20, bounds.Y + 8, bounds.Width - 40, bounds.Height - 16));
    }

    internal static void DrawScrollbar(SpriteBatch b, Rectangle thumb) =>
        b.Draw(Game1.mouseCursors, thumb, new Rectangle(435, 463, 6, 10), Color.White);

    internal static void DrawPageButton(SpriteBatch b, Rectangle bounds, string text)
    {
        int x = bounds.X, y = bounds.Y, right = bounds.Right;
        Color edge = new Color(133, 84, 34) * .8f, light = new Color(255, 220, 151) * .85f;
        void Line(int atX, int atY, int width, int height, Color color) => b.Draw(Game1.staminaRect, new Rectangle(atX, atY, width, height), color);
        // An outline leaves the original page texture intact, including its local shading.
        Line(x + 18, y + 5, bounds.Width - 36, 2, edge); Line(x + 18, y + 7, bounds.Width - 36, 1, light);
        Line(x + 18, bounds.Bottom - 6, bounds.Width - 36, 2, edge); Line(x + 18, bounds.Bottom - 4, bounds.Width - 36, 1, light);
        Line(x + 2, y + 21, 2, bounds.Height - 42, edge); Line(right - 3, y + 21, 2, bounds.Height - 42, edge);
        for (int i = 0; i < 16; i++)
        {
            Line(x + 2 + i, y + 20 - i, 2, 2, edge); Line(right - 18 + i, y + 5 + i, 2, 2, edge);
            Line(x + 2 + i, bounds.Bottom - 21 + i, 2, 2, edge); Line(right - 18 + i, bounds.Bottom - 6 - i, 2, 2, edge);
        }
        foreach (int center in new[] { x + 22, right - 23 })
            for (int row = -9; row <= 9; row++)
            {
                int half = Math.Max(0, 6 - Math.Abs(row) * 6 / 9);
                Line(center - half, bounds.Center.Y + row, half * 2 + 1, 1, edge);
                if (half > 1) Line(center - half + 1, bounds.Center.Y + row, half * 2 - 1, 1, light);
            }
        DrawCentered(b, text, new Rectangle(x + 36, y + 14, bounds.Width - 72, bounds.Height - 28));
    }

    internal static void DrawScrollbarTrack(SpriteBatch b, Texture2D texture, Rectangle bounds) =>
        b.Draw(texture, bounds, new Rectangle(0, 0, bounds.Width, bounds.Height), Color.White);

    internal static void DrawCentered(SpriteBatch b, string text, Rectangle bounds)
    {
        Vector2 size = Game1.smallFont.MeasureString(text);
        float scale = GalleryTextFit.Scale(size.X, size.Y, bounds.Width, bounds.Height);
        b.DrawString(Game1.smallFont, text, new Vector2(bounds.Center.X - size.X * scale / 2, bounds.Center.Y - size.Y * scale / 2), Game1.textColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    internal static void DrawLeftFitted(SpriteBatch b, string text, Rectangle bounds)
    {
        Vector2 size = Game1.smallFont.MeasureString(text);
        float scale = GalleryTextFit.Scale(size.X, size.Y, bounds.Width, bounds.Height);
        b.DrawString(Game1.smallFont, text, new Vector2(bounds.X, bounds.Center.Y - size.Y * scale / 2), Game1.textColor,
            0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }
}
