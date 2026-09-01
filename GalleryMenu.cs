using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;

namespace StardewGallery;

internal sealed class GalleryMenu : IClickableMenu
{
    internal const int MenuWidth = 1672;
    internal const int MenuHeight = 941;
    private const int Columns = 6;
    private const int VisibleRows = 3;
    private const int SearchComponentId = 1000;
    private const int UnlockComponentId = 1001;
    private const int YesComponentId = 1002;
    private const int NoComponentId = 1003;
    private readonly GalleryCatalog catalog;
    private readonly ITranslationHelper i18n;
    private readonly Texture2D background;
    private readonly Texture2D detailBackground;
    private readonly Texture2D scene;
    private readonly Func<bool> isUnlocked;
    private readonly Action toggleUnlock;
    private readonly Func<string, IReadOnlyList<WatchedEventSnapshot>> watchedVersions;
    private readonly Action<GalleryCharacter, GalleryEvent, WatchedEventSnapshot?, int> replay;
    private readonly TextBox search;
    private List<GalleryCharacter> filtered = [];
    private int scrollRow;
    private bool dragging;
    private int dragOffset;
    private bool confirming;
    private Rectangle searchBounds;
    private Rectangle unlockBounds;
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

    internal bool IsSearchSelected => search.Selected;
    internal bool IsConfirming => confirming;

    internal GalleryMenu(
        GalleryCatalog catalog,
        ITranslationHelper i18n,
        Texture2D background,
        Texture2D detailBackground,
        Texture2D scene,
        Func<bool> isUnlocked,
        Action toggleUnlock,
        Func<string, IReadOnlyList<WatchedEventSnapshot>> watchedVersions,
        Action<GalleryCharacter, GalleryEvent, WatchedEventSnapshot?, int> replay)
        : base(0, 0, MenuWidth, MenuHeight, true)
    {
        this.catalog = catalog;
        this.i18n = i18n;
        this.background = background;
        this.detailBackground = detailBackground;
        this.scene = scene;
        this.isUnlocked = isUnlocked;
        this.toggleUnlock = toggleUnlock;
        this.watchedVersions = watchedVersions;
        this.replay = replay;
        search = new TextBox(Game1.content.Load<Texture2D>("LooseSprites\\textBox"), null, Game1.smallFont, Game1.textColor);
        search.OnEnterPressed += _ => OpenFirstMatch();
        RecalculateLayout();
        RefreshFilter();
        SnapForGamepad();
    }

    public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
    {
        base.gameWindowSizeChanged(oldBounds, newBounds);
        RecalculateLayout();
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        (x, y) = ToLogical(x, y);
        if (confirming)
        {
            if (yesBounds.Contains(x, y))
            {
                toggleUnlock();
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
            search.SelectMe();
            return;
        }
        DeselectSearch();

        int first = scrollRow * Columns;
        for (int slot = 0; slot < Columns * VisibleRows && first + slot < filtered.Count; slot++)
        {
            if (!Card(slot).Contains(x, y))
                continue;
            GalleryCharacter character = filtered[first + slot];
            if (!character.IsMet && !isUnlocked())
                return;
            Game1.playSound("smallSelect");
            Game1.activeClickableMenu = new GalleryCharacterMenu(character, catalog, i18n, detailBackground, scene,
                isUnlocked,
                () => Game1.activeClickableMenu = new GalleryMenu(catalog, i18n, background, detailBackground, scene, isUnlocked, toggleUnlock, watchedVersions, replay),
                watchedVersions,
                (entry, version, scroll) => replay(character, entry, version, scroll));
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
        if (key == Keys.Escape && search.Selected)
        {
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
        if (confirming)
        {
            confirming = false;
            BuildClickableComponents();
            SnapForGamepad();
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
        SpriteText.drawStringHorizontallyCenteredAt(b, i18n.Get("home.title"), xPositionOnScreen + width / 2, yPositionOnScreen + 40, maxWidth: 430);
        DrawButton(b, unlockBounds, i18n.Get(isUnlocked() ? "menu.restore-locks" : "menu.unlock-all"));
        search.Draw(b);
        if (search.Text.Length == 0 && !search.Selected)
            b.DrawString(Game1.smallFont, i18n.Get("home.search"), new Vector2(searchBounds.X + 12, searchBounds.Y + 10), Color.Gray);

        int first = scrollRow * Columns;
        for (int slot = 0; slot < Columns * VisibleRows && first + slot < filtered.Count; slot++)
            DrawCharacter(b, Card(slot), filtered[first + slot]);
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
        searchBounds = R(305, 150, 430, 48);
        search.X = searchBounds.X;
        search.Y = searchBounds.Y;
        search.Width = searchBounds.Width;
        unlockBounds = R(1180, 40, 270, 54);
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
        string textureName = NPC.getTextureNameForCharacter(character.Name);
        string asset = $"Portraits\\{textureName}";
        if (Game1.content.DoesAssetExist<Texture2D>(asset))
        {
            Texture2D portrait = Game1.content.Load<Texture2D>(asset);
            Rectangle source = new(0, 0, Math.Min(64, portrait.Width), Math.Min(64, portrait.Height));
            b.Draw(portrait, new Rectangle(card.Center.X - 48, card.Y, 96, 96), source, known ? Color.White : Color.Black * .82f);
        }
        DrawCentered(b, GalleryUiRules.DisplayName(character.DisplayName, character.IsMet, isUnlocked()), new Rectangle(card.X, card.Y + 100, card.Width, 32));
        int count = catalog.Events.Count(entry => entry.Ownership.Owners.Any(owner => owner.Name == character.Name));
        DrawCentered(b, $"♥ {count}", new Rectangle(card.X, card.Y + 132, card.Width, 28));
    }

    private void RefreshFilter()
    {
        string previous = string.Join('\u001f', filtered.Select(character => character.Name));
        string query = search.Text.Trim();
        filtered = catalog.Characters
            .Where(character => query.Length == 0
                || character.DisplayName.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || character.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || catalog.Events.Any(entry => entry.EventId.Contains(query, StringComparison.OrdinalIgnoreCase)
                    && entry.Ownership.Owners.Any(owner => owner.Name == character.Name)))
            .OrderBy(character => character.DisplayName, Comparer<string>.Create(CompareDisplayNames))
            .ToList();
        UpdateScrollbar();
        if (previous != string.Join('\u001f', filtered.Select(character => character.Name)))
            BuildClickableComponents();
    }

    internal void OpenFirstMatch()
    {
        GalleryCharacter? character = filtered.FirstOrDefault();
        if (character is null || !character.IsMet && !isUnlocked())
            return;
        Game1.activeClickableMenu = new GalleryCharacterMenu(character, catalog, i18n, detailBackground, scene,
            isUnlocked,
            () => Game1.activeClickableMenu = new GalleryMenu(catalog, i18n, background, detailBackground, scene, isUnlocked, toggleUnlock, watchedVersions, replay),
            watchedVersions,
            (entry, version, scroll) => replay(character, entry, version, scroll));
    }

    private int MaxScroll => Math.Max(0, (filtered.Count + Columns - 1) / Columns - VisibleRows);

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
            searchComponent = new ClickableComponent(ToScreen(searchBounds), "search")
            {
                myID = SearchComponentId,
                rightNeighborID = UnlockComponentId,
                downNeighborID = 0
            };
            unlockComponent = new ClickableComponent(ToScreen(unlockBounds), "unlock")
            {
                myID = UnlockComponentId,
                leftNeighborID = SearchComponentId,
                rightNeighborID = upperRightCloseButton?.myID ?? -1,
                upNeighborID = upperRightCloseButton?.myID ?? -1,
                downNeighborID = Math.Min(3, Math.Max(0, filtered.Count - 1))
            };
            allClickableComponents.Add(searchComponent);
            allClickableComponents.Add(unlockComponent);
            int visible = Math.Min(Columns * VisibleRows, Math.Max(0, filtered.Count - scrollRow * Columns));
            for (int slot = 0; slot < visible; slot++)
            {
                int col = slot % Columns;
                int row = slot / Columns;
                ClickableComponent card = new(ToScreen(Card(slot)), $"character-{slot}")
                {
                    myID = slot,
                    leftNeighborID = col > 0 ? slot - 1 : -1,
                    rightNeighborID = col < Columns - 1 && slot + 1 < visible ? slot + 1 : -1,
                    upNeighborID = row > 0 ? slot - Columns : col < 3 ? SearchComponentId : UnlockComponentId,
                    downNeighborID = slot + Columns < visible ? slot + Columns : -1
                };
                cardComponents.Add(card);
                allClickableComponents.Add(card);
            }
        }
        if (upperRightCloseButton is not null)
        {
            upperRightCloseButton.leftNeighborID = confirming ? YesComponentId : UnlockComponentId;
            upperRightCloseButton.downNeighborID = confirming ? YesComponentId : cardComponents.FirstOrDefault()?.myID ?? UnlockComponentId;
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

    private static int CompareDisplayNames(string left, string right)
    {
        CultureInfo culture = LocalizedContentManager.CurrentLanguageCode == LocalizedContentManager.LanguageCode.zh
            ? CultureInfo.GetCultureInfo("zh-CN")
            : CultureInfo.GetCultureInfo("en-US");
        return culture.CompareInfo.Compare(left, right, CompareOptions.IgnoreCase | CompareOptions.IgnoreWidth);
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
        DrawCentered(b, text, bounds);
    }

    internal static void DrawScrollbar(SpriteBatch b, Rectangle thumb) =>
        b.Draw(Game1.mouseCursors, thumb, new Rectangle(435, 463, 6, 10), Color.White);

    internal static void DrawCentered(SpriteBatch b, string text, Rectangle bounds)
    {
        Vector2 size = Game1.smallFont.MeasureString(text);
        float scale = Math.Min(1f, bounds.Width / Math.Max(1f, size.X));
        b.DrawString(Game1.smallFont, text, new Vector2(bounds.Center.X - size.X * scale / 2, bounds.Center.Y - size.Y * scale / 2), Game1.textColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    internal static void DrawLeftFitted(SpriteBatch b, string text, Rectangle bounds)
    {
        Vector2 size = Game1.smallFont.MeasureString(text);
        float scale = Math.Min(1f, bounds.Width / Math.Max(1f, size.X));
        b.DrawString(Game1.smallFont, text, new Vector2(bounds.X, bounds.Center.Y - size.Y * scale / 2), Game1.textColor,
            0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }
}
