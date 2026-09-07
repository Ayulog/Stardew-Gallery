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
    private static readonly RasterizerState ClipRasterizer = new() { ScissorTestEnable = true };
    private readonly GalleryCharacter character;
    private readonly GalleryEvent entry;
    private readonly IReadOnlyList<ConditionDisplayItem> conditions;
    private readonly ITranslationHelper i18n;
    private readonly Texture2D background;
    private readonly Texture2D thumbnail;
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
        if (button is Buttons.DPadUp or Buttons.LeftThumbstickUp)
        {
            ScrollBy(-ScrollStep);
            return;
        }
        if (button is Buttons.DPadDown or Buttons.LeftThumbstickDown)
        {
            ScrollBy(ScrollStep);
            return;
        }
        base.receiveGamePadButton(button);
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
        IClickableMenu.drawTextureBox(b, contentBounds.X - 10, contentBounds.Y - 10, contentBounds.Width + 20, contentBounds.Height + 20, Color.White);
        BeginContentClip(b);
        DrawConditions(b);
        EndContentClip(b);
        if (MaxScroll > 0)
            GalleryMenu.DrawScrollbar(b, scrollThumb);
        if (canReplay())
            GalleryMenu.DrawButton(b, replayBounds, i18n.Get("event.replay"));
        GalleryMenu.DrawButton(b, backBounds, i18n.Get("event-detail.back"));
        upperRightCloseButton?.draw(b);
        GalleryMenu.EndScaled(b);
        drawMouse(b);
    }

    private void DrawHeader(SpriteBatch b)
    {
        SpriteText.drawStringHorizontallyCenteredAt(b, i18n.Get("event-detail.title"), 1125, 52, maxWidth: 620);
        EventOwner owner = entry.Ownership.Owners.First(value => value.Name == character.Name);
        string hearts = owner.FriendshipPoints is int points
            ? i18n.Get("event.hearts", new { hearts = (int)Math.Ceiling(points / 250d) })
            : i18n.Get("event.unspecified");
        GalleryMenu.DrawLeftFitted(b, $"{hearts} · ID {entry.EventId}", new Rectangle(785, 112, 650, 38));
        string location = Game1.getLocationFromName(entry.LocationName)?.DisplayName ?? entry.LocationName;
        b.DrawString(Game1.smallFont, i18n.Get("event-detail.location", new { location }), new Vector2(785, 153), new Color(90, 70, 45));
        b.Draw(thumbnail, new Rectangle(1000, 190, 266, 150), Color.White);
    }

    private int MeasureContent()
    {
        int height = LineHeight(i18n.Get("event-detail.requirements"), contentBounds.Width - 40) + 16;
        if (conditions.Count == 0)
            return height + LineHeight(i18n.Get("condition.none"), contentBounds.Width - 60) + 24;
        return height + conditions.Sum(MeasureRow);
    }

    private int MeasureRow(ConditionDisplayItem item)
    {
        const int textWidth = 610;
        int height = LineHeight(RowText(item), textWidth) + 28;
        string? reasonKey = ConditionRowPresentation.UnknownReasonKey(item);
        if (reasonKey is not null)
            height += LineHeight(i18n.Get(reasonKey), textWidth) + 6;
        return Math.Max(58, height) + 10;
    }

    private void DrawConditions(SpriteBatch b)
    {
        int y = contentBounds.Y - scroll;
        b.DrawString(Game1.smallFont, i18n.Get("event-detail.requirements"), new Vector2(contentBounds.X + 10, y), Game1.textColor);
        y += LineHeight(i18n.Get("event-detail.requirements"), contentBounds.Width - 40) + 16;
        if (conditions.Count == 0)
        {
            DrawWrapped(b, i18n.Get("condition.none"), contentBounds.X + 18, y + 10, contentBounds.Width - 60, Game1.textColor);
            return;
        }
        foreach (ConditionDisplayItem item in conditions)
        {
            int height = MeasureRow(item);
            Rectangle row = new(contentBounds.X + 6, y, contentBounds.Width - 12, height - 8);
            IClickableMenu.drawTextureBox(b, row.X, row.Y, row.Width, row.Height, Color.White);
            string text = RowText(item);
            int textHeight = LineHeight(text, row.Width - 82);
            int textY = row.Y + 13;
            DrawWrapped(b, text, row.X + 16, textY, row.Width - 82, Game1.textColor);
            string? reasonKey = ConditionRowPresentation.UnknownReasonKey(item);
            if (reasonKey is not null)
                DrawWrapped(b, i18n.Get(reasonKey), row.X + 16, textY + textHeight + 6, row.Width - 82, new Color(125, 90, 35));
            DrawStatus(b, ConditionRowPresentation.Status(item.Evaluation), new Rectangle(row.Right - 58, row.Y, 42, row.Height));
            y += height;
        }
    }

    private string RowText(ConditionDisplayItem item)
        => ConditionRowPresentation.Text(item, (key, arguments) => i18n.Get(key, arguments));

    private static void DrawStatus(SpriteBatch b, ConditionStatusIcon status, Rectangle bounds)
    {
        (string glyph, Color color) = status switch
        {
            ConditionStatusIcon.Check => ("✓", new Color(20, 120, 45)),
            ConditionStatusIcon.Cross => ("✗", new Color(170, 35, 35)),
            _ => ("?", new Color(145, 100, 20))
        };
        Vector2 size = Game1.smallFont.MeasureString(glyph);
        b.DrawString(Game1.smallFont, glyph, new Vector2(bounds.Center.X - size.X / 2, bounds.Center.Y - size.Y / 2), color);
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
        contentBounds = new Rectangle(755, 365, 720, 420);
        scrollTrack = new Rectangle(1508, 365, 24, 420);
        replayBounds = new Rectangle(795, 842, 280, 52);
        backBounds = new Rectangle(1160, 842, 280, 52);
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
        allClickableComponents = [new ClickableComponent(ToScreen(backBounds), "back") { myID = BackComponentId, leftNeighborID = canReplay() ? ReplayComponentId : -1 }];
        if (canReplay())
            allClickableComponents.Add(new ClickableComponent(ToScreen(replayBounds), "replay") { myID = ReplayComponentId, rightNeighborID = BackComponentId });
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

    private Rectangle ToScreen(Rectangle bounds) => GalleryMenu.ScaleRectangle(bounds, menuScale, drawOffsetX, drawOffsetY);
    private (int X, int Y) ToLogical(int x, int y) => ((int)Math.Round((x - drawOffsetX) / menuScale), (int)Math.Round((y - drawOffsetY) / menuScale));
}
