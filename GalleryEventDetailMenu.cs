using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;

namespace StardewGallery;

internal sealed class GalleryEventDetailMenu : IClickableMenu
{
    private const int BackComponentId = 1000;
    private const int ReplayComponentId = 1001;
    private const int ScrollStep = 60;
    private const int RowPadding = 12;
    private const int StatusSize = GallerySpreadLayout.IconSize * 3;
    private const int StatusGap = 16;
    private static readonly RasterizerState ClipRasterizer = new() { ScissorTestEnable = true };
    private readonly GalleryCharacter character;
    private readonly GalleryEvent entry;
    private readonly IReadOnlyList<ConditionDisplayItem> conditions;
    private readonly ITranslationHelper i18n;
    private readonly Texture2D background;
    private readonly Texture2D thumbnail;
    private readonly Texture2D statusIcons;
    private readonly Texture2D replayGlyph;
    private readonly GalleryCharacterPanel leftPanel;
    private readonly Func<bool> canReplay;
    private readonly Action back;
    private readonly Action replay;
    private Rectangle contentBounds;
    private Rectangle scrollTrack;
    private Rectangle scrollThumb;
    private Rectangle backBounds;
    private Rectangle replayBounds;
    private int contentHeight;
    private int scroll;
    private bool dragging;
    private int dragOffset;
    private int viewportWidth;
    private int viewportHeight;
    private float menuScale = 1f;
    private int drawOffsetX;
    private int drawOffsetY;

    internal GalleryEventDetailMenu(
        GalleryCharacter character,
        GalleryCatalog catalog,
        GalleryEvent entry,
        IReadOnlyList<ConditionDisplayItem> conditions,
        ITranslationHelper i18n,
        Texture2D background,
        Texture2D scene,
        Texture2D thumbnail,
        Texture2D statusIcons,
        Texture2D replayGlyph,
        Func<bool> canReplay,
        Action back,
        Action replay)
        : base(0, 0, GalleryMenu.MenuWidth, GalleryMenu.MenuHeight, true)
    {
        this.character = character;
        this.entry = entry;
        this.conditions = conditions;
        this.i18n = i18n;
        this.background = background;
        this.thumbnail = thumbnail;
        this.statusIcons = statusIcons;
        this.replayGlyph = replayGlyph;
        this.canReplay = canReplay;
        this.back = back;
        this.replay = replay;
        leftPanel = new GalleryCharacterPanel(character, GalleryCharacterMenu.EventsFor(character, catalog), i18n, scene);
        RecalculateLayout();
        SnapForGamepad();
    }

    public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
    {
        base.gameWindowSizeChanged(oldBounds, newBounds);
        RecalculateLayout();
    }

    public override void receiveScrollWheelAction(int direction)
    {
        ScrollBy(direction < 0 ? ScrollStep : -ScrollStep);
        Game1.playSound("shiny4");
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        (x, y) = ToLogical(x, y);
        if (upperRightCloseButton?.bounds.Contains(x, y) == true || backBounds.Contains(x, y))
        {
            Return();
            return;
        }
        if (canReplay() && replayBounds.Contains(x, y))
        {
            replay();
            return;
        }
        if (scrollThumb.Contains(x, y) && MaxScroll > 0)
        {
            dragging = true;
            dragOffset = y - scrollThumb.Y;
            return;
        }
        if (scrollTrack.Contains(x, y) && MaxScroll > 0)
            ScrollBy(y < scrollThumb.Y ? -contentBounds.Height : contentBounds.Height);
    }

    public override void leftClickHeld(int x, int y)
    {
        (x, y) = ToLogical(x, y);
        if (!dragging || MaxScroll == 0)
            return;
        int travel = scrollTrack.Height - scrollThumb.Height;
        scroll = (int)Math.Round(Math.Clamp(y - dragOffset - scrollTrack.Y, 0, travel) / (double)travel * MaxScroll);
        UpdateScrollbar();
    }

    public override void releaseLeftClick(int x, int y)
    {
        dragging = false;
        base.releaseLeftClick(x, y);
    }

    public override void receiveRightClick(int x, int y, bool playSound = true) => Return();

    public override void receiveKeyPress(Keys key)
    {
        if (key == Keys.Escape)
        {
            Return();
            return;
        }
        base.receiveKeyPress(key);
    }

    public override void receiveGamePadButton(Buttons button)
    {
        if (button == Buttons.B)
        {
            Return();
            return;
        }
        if (button == Buttons.A && Game1.options.snappyMenus)
        {
            if (currentlySnappedComponent?.myID == ReplayComponentId && canReplay())
                replay();
            else if (currentlySnappedComponent?.myID == BackComponentId)
                Return();
            return;
        }
        base.receiveGamePadButton(button);
    }

    public override void applyMovementKey(int direction)
    {
        if (direction is 0 or 2)
            ScrollBy(direction == 0 ? -ScrollStep : ScrollStep);
        else if (direction is 1 or 3)
        {
            int target = direction == 1 && canReplay() ? ReplayComponentId : BackComponentId;
            currentlySnappedComponent = allClickableComponents.FirstOrDefault(component => component.myID == target);
            snapCursorToCurrentSnappedComponent();
        }
    }

    internal void HandleControllerBack() => Return();

    public override void snapToDefaultClickableComponent()
    {
        currentlySnappedComponent = allClickableComponents?.FirstOrDefault(component => component.myID == (canReplay() ? ReplayComponentId : BackComponentId));
        snapCursorToCurrentSnappedComponent();
    }

    public override void draw(SpriteBatch b)
    {
        EnsureLayout();
        contentHeight = MeasureContent();
        scroll = Math.Clamp(scroll, 0, MaxScroll);
        UpdateScrollbar();
        b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height), Color.Black * .45f);
        GalleryMenu.BeginScaled(b, menuScale, drawOffsetX, drawOffsetY);
        leftPanel.DrawPhoto(b);
        b.Draw(background, new Rectangle(0, 0, width, height), Color.White);
        leftPanel.DrawInformation(b);
        DrawHeader(b);
        BeginContentClip(b);
        DrawConditions(b);
        EndContentClip(b);
        if (MaxScroll > 0)
            GallerySpreadDrawing.DrawScrollbar(b, scrollThumb);
        (int mouseX, int mouseY) = ToLogical(Game1.getMouseX(true), Game1.getMouseY(true));
        if (canReplay())
            GallerySpreadDrawing.DrawFooterTab(b, replayBounds, i18n.Get("event.replay"), replayBounds.Contains(mouseX, mouseY) || Focused(ReplayComponentId), replayGlyph);
        GallerySpreadDrawing.DrawFooterTab(b, backBounds, i18n.Get("event-detail.back"), backBounds.Contains(mouseX, mouseY) || Focused(BackComponentId));
        upperRightCloseButton?.draw(b);
        GalleryMenu.EndScaled(b);
        drawMouse(b);
    }

    private void DrawHeader(SpriteBatch b)
    {
        Rectangle title = Bounds(GallerySpreadLayout.TitleBounds);
        SpriteText.drawStringHorizontallyCenteredAt(b, i18n.Get("event-detail.title"), title.Center.X, title.Y + 8, maxWidth: title.Width - 24);
        EventOwner owner = entry.Ownership.Owners.First(value => value.Name == character.Name);
        string hearts = owner.FriendshipPoints is int points
            ? i18n.Get("event.hearts", new { hearts = (int)Math.Ceiling(points / 250d) })
            : i18n.Get("event.unspecified");
        DrawHeaderText(b, $"{hearts} · ID {entry.EventId}", Bounds(GallerySpreadLayout.DetailEventIdBounds));
        string location = Game1.getLocationFromName(entry.LocationName)?.DisplayName ?? entry.LocationName;
        DrawHeaderText(b, i18n.Get("event-detail.location", new { location }), Bounds(GallerySpreadLayout.DetailLocationBounds), wrap: true);
        b.Draw(thumbnail, Bounds(GallerySpreadLayout.DetailThumbnailBounds), Color.White);
        DrawHeaderText(b, i18n.Get("event-detail.requirements"), Bounds(GallerySpreadLayout.ConditionHeadingBounds));
    }

    private static void DrawHeaderText(SpriteBatch b, string text, Rectangle bounds, bool wrap = false)
    {
        if (wrap)
            text = Game1.parseText(text, Game1.smallFont, bounds.Width);
        Vector2 size = Game1.smallFont.MeasureString(text);
        float scale = Math.Min(1f, Math.Min(bounds.Width / Math.Max(1f, size.X), bounds.Height / Math.Max(1f, size.Y)));
        b.DrawString(Game1.smallFont, text, new Vector2(bounds.X, wrap ? bounds.Y : bounds.Center.Y - size.Y * scale / 2f), GallerySpreadDrawing.Ink,
            0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    // Both measurement and drawing reserve the same padding, icon and gap.
    private int RowTextWidth => contentBounds.Width - RowPadding * 2 - StatusSize - StatusGap;

    private int MeasureContent()
    {
        if (conditions.Count == 0)
            return LineHeight(i18n.Get("condition.none"), RowTextWidth) + RowPadding * 2;
        return conditions.Sum(MeasureRow);
    }

    private int MeasureRow(ConditionDisplayItem item)
    {
        int height = LineHeight(RowText(item), RowTextWidth);
        string? reasonKey = ConditionRowPresentation.UnknownReasonKey(item);
        if (reasonKey is not null)
            height += LineHeight(i18n.Get(reasonKey), RowTextWidth) + 6;
        return Math.Max(StatusSize, height) + RowPadding * 2;
    }

    private void DrawConditions(SpriteBatch b)
    {
        int y = contentBounds.Y - scroll;
        if (conditions.Count == 0)
        {
            DrawWrapped(b, i18n.Get("condition.none"), contentBounds.X + RowPadding, y + RowPadding, RowTextWidth, Game1.textColor);
            return;
        }
        foreach (ConditionDisplayItem item in conditions)
        {
            int height = MeasureRow(item);
            Rectangle row = new(contentBounds.X, y, contentBounds.Width, height);
            if (row.Top >= contentBounds.Bottom)
                break;
            if (row.Bottom <= contentBounds.Top)
            {
                y += height;
                continue;
            }
            if (y > contentBounds.Y - scroll)
                b.Draw(Game1.staminaRect, new Rectangle(row.X, row.Y, row.Width, 1), GallerySpreadDrawing.LightInk);
            string text = RowText(item);
            int textY = row.Y + RowPadding;
            int textHeight = DrawWrapped(b, text, row.X + RowPadding, textY, RowTextWidth, Game1.textColor);
            string? reasonKey = ConditionRowPresentation.UnknownReasonKey(item);
            if (reasonKey is not null)
                DrawWrapped(b, i18n.Get(reasonKey), row.X + RowPadding, textY + textHeight + 6, RowTextWidth, new Color(125, 90, 35));
            DrawStatus(b, ConditionRowPresentation.Status(item.Evaluation), new Rectangle(row.Right - RowPadding - StatusSize, row.Center.Y - StatusSize / 2, StatusSize, StatusSize));
            y += height;
        }
    }

    private string RowText(ConditionDisplayItem item)
        => ConditionRowPresentation.Text(item, (key, arguments) => i18n.Get(key, arguments));

    private void DrawStatus(SpriteBatch b, ConditionStatusIcon status, Rectangle bounds)
    {
        Rectangle source = Bounds(status switch
        {
            ConditionStatusIcon.Check => GallerySpreadLayout.ConditionCheckSource,
            ConditionStatusIcon.Cross => GallerySpreadLayout.ConditionCrossSource,
            _ => GallerySpreadLayout.ConditionQuestionSource
        });
        b.Draw(statusIcons, bounds, source, Color.White);
    }

    private static int DrawWrapped(SpriteBatch b, string text, int x, int y, int width, Color color)
    {
        string wrapped = Game1.parseText(text, Game1.smallFont, width);
        b.DrawString(Game1.smallFont, wrapped, new Vector2(x, y), color);
        return wrapped.Split('\n').Length * Game1.smallFont.LineSpacing;
    }

    private static int LineHeight(string text, int width)
        => Game1.parseText(text, Game1.smallFont, width).Split('\n').Length * Game1.smallFont.LineSpacing;

    private void BeginContentClip(SpriteBatch b)
    {
        b.End();
        b.GraphicsDevice.ScissorRectangle = ToScreen(contentBounds);
        Matrix transform = Matrix.CreateScale(menuScale) * Matrix.CreateTranslation(drawOffsetX, drawOffsetY, 0f);
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, ClipRasterizer, null, transform);
    }

    private void EndContentClip(SpriteBatch b)
    {
        b.End();
        b.GraphicsDevice.ScissorRectangle = new Rectangle(0, 0, b.GraphicsDevice.PresentationParameters.BackBufferWidth, b.GraphicsDevice.PresentationParameters.BackBufferHeight);
        Matrix transform = Matrix.CreateScale(menuScale) * Matrix.CreateTranslation(drawOffsetX, drawOffsetY, 0f);
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, transform);
    }

    private void RecalculateLayout()
    {
        width = GalleryMenu.MenuWidth;
        height = GalleryMenu.MenuHeight;
        xPositionOnScreen = yPositionOnScreen = 0;
        menuScale = (float)GalleryLayout.ScaleToFit(Game1.uiViewport.Width, Game1.uiViewport.Height, width, height, 24);
        drawOffsetX = (int)Math.Round((Game1.uiViewport.Width - width * menuScale) / 2f);
        drawOffsetY = (int)Math.Round((Game1.uiViewport.Height - height * menuScale) / 2f);
        viewportWidth = Game1.uiViewport.Width;
        viewportHeight = Game1.uiViewport.Height;
        contentBounds = Bounds(GallerySpreadLayout.ConditionViewportBounds);
        scrollTrack = Bounds(GallerySpreadLayout.DetailScrollTrackBounds);
        replayBounds = Bounds(GallerySpreadLayout.ReplayButtonBounds);
        backBounds = Bounds(GallerySpreadLayout.BackButtonBounds);
        initializeUpperRightCloseButton();
        contentHeight = MeasureContent();
        scroll = Math.Clamp(scroll, 0, MaxScroll);
        UpdateScrollbar();
        BuildClickableComponents();
    }

    private void EnsureLayout()
    {
        if (GalleryLayout.Changed(viewportWidth, viewportHeight, Game1.uiViewport.Width, Game1.uiViewport.Height))
            RecalculateLayout();
    }

    private int MaxScroll => Math.Max(0, contentHeight - contentBounds.Height);

    private void ScrollBy(int amount)
    {
        scroll = Math.Clamp(scroll + amount, 0, MaxScroll);
        UpdateScrollbar();
    }

    private void UpdateScrollbar()
    {
        int thumbHeight = MaxScroll == 0 ? 40 : Math.Max(40, contentBounds.Height * contentBounds.Height / contentHeight);
        int travel = scrollTrack.Height - thumbHeight;
        int y = MaxScroll == 0 ? scrollTrack.Y : scrollTrack.Y + (int)Math.Round(travel * scroll / (double)MaxScroll);
        scrollThumb = new Rectangle(scrollTrack.X, y, scrollTrack.Width, thumbHeight);
    }

    private void BuildClickableComponents()
    {
        int previous = currentlySnappedComponent?.myID ?? (canReplay() ? ReplayComponentId : BackComponentId);
        allClickableComponents = [new ClickableComponent(ToScreen(backBounds), "back") { myID = BackComponentId, rightNeighborID = canReplay() ? ReplayComponentId : -1 }];
        if (canReplay())
            allClickableComponents.Add(new ClickableComponent(ToScreen(replayBounds), "replay") { myID = ReplayComponentId, leftNeighborID = BackComponentId });
        currentlySnappedComponent = allClickableComponents.FirstOrDefault(component => component.myID == previous) ?? allClickableComponents[0];
        if (Game1.options.snappyMenus && Game1.options.gamepadControls)
            snapCursorToCurrentSnappedComponent();
    }

    private void SnapForGamepad()
    {
        if (Game1.options.snappyMenus && Game1.options.gamepadControls)
            snapToDefaultClickableComponent();
    }

    private void Return()
    {
        Game1.playSound("bigDeSelect");
        back();
    }

    private static Rectangle Bounds((int X, int Y, int Width, int Height) bounds) => new(bounds.X, bounds.Y, bounds.Width, bounds.Height);
    private Rectangle ToScreen(Rectangle bounds) => GalleryMenu.ScaleRectangle(bounds, menuScale, drawOffsetX, drawOffsetY);
    private bool Focused(int id) => Game1.options.snappyMenus && Game1.options.gamepadControls && currentlySnappedComponent?.myID == id;
    private (int X, int Y) ToLogical(int x, int y) => ((int)Math.Round((x - drawOffsetX) / menuScale), (int)Math.Round((y - drawOffsetY) / menuScale));
}
