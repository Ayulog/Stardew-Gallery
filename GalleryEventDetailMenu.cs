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
        GalleryEvent entry,
        IReadOnlyList<ConditionDisplayItem> conditions,
        ITranslationHelper i18n,
        Texture2D background,
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
        this.canReplay = canReplay;
        this.back = back;
        this.replay = replay;
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
        if (button == Buttons.DPadUp)
        {
            ScrollBy(-ScrollStep);
            return;
        }
        if (button == Buttons.DPadDown)
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
        b.Draw(background, new Rectangle(xPositionOnScreen, yPositionOnScreen, width, height), Color.White);
        SpriteText.drawStringHorizontallyCenteredAt(b, i18n.Get("event-detail.title"), width / 2, 48, maxWidth: 700);
        DrawMetadata(b);
        DrawWrapped(b, i18n.Get("event-detail.analysis-note"), 180, 210, 1300, new Color(90, 70, 45));
        IClickableMenu.drawTextureBox(b, contentBounds.X - 16, contentBounds.Y - 16, contentBounds.Width + 32, contentBounds.Height + 32, Color.White);

        BeginContentClip(b);
        DrawConditions(b);
        EndContentClip(b);

        if (MaxScroll > 0)
            GalleryMenu.DrawScrollbar(b, scrollThumb);
        GalleryMenu.DrawButton(b, backBounds, i18n.Get("event-detail.back"));
        if (canReplay())
            GalleryMenu.DrawButton(b, replayBounds, i18n.Get("event.replay"));
        upperRightCloseButton?.draw(b);
        GalleryMenu.EndScaled(b);
        drawMouse(b);
    }

    private void DrawMetadata(SpriteBatch b)
    {
        EventOwner owner = entry.Ownership.Owners.First(value => value.Name == character.Name);
        string hearts = owner.FriendshipPoints is int points
            ? i18n.Get("event.hearts", new { hearts = (int)Math.Ceiling(points / 250d) })
            : i18n.Get("event.unspecified");
        string location = Game1.getLocationFromName(entry.LocationName)?.DisplayName ?? entry.LocationName;
        string status = i18n.Get(canReplay() ? "event.state-unlocked" : "event.state-locked");
        string[] left =
        [
            character.DisplayName,
            i18n.Get("event-detail.event-id", new { id = entry.EventId }),
            i18n.Get("event-detail.location", new { location })
        ];
        string[] right =
        [
            i18n.Get("event-detail.hearts", new { hearts }),
            i18n.Get("event-detail.status", new { status }),
            i18n.Get("event-detail.asset", new { asset = entry.AssetName })
        ];
        for (int i = 0; i < left.Length; i++)
        {
            b.DrawString(Game1.smallFont, left[i], new Vector2(190, 105 + i * 32), Game1.textColor);
            b.DrawString(Game1.smallFont, right[i], new Vector2(850, 105 + i * 32), Game1.textColor);
        }
    }

    private int MeasureContent()
    {
        int height = LineHeight(i18n.Get("event-detail.requirements"), contentBounds.Width - 36) + 18;
        if (conditions.Count == 0)
            return height + LineHeight(i18n.Get("condition.none"), contentBounds.Width - 72) + 36;
        return height + conditions.Sum(MeasureBlock);
    }

    private int MeasureBlock(ConditionDisplayItem item)
    {
        int width = contentBounds.Width - 72;
        int height = 30;
        foreach (string line in DetailLines(item))
            height += LineHeight(line, width) + 8;
        return height + 12;
    }

    private void DrawConditions(SpriteBatch b)
    {
        int y = contentBounds.Y - scroll;
        b.DrawString(Game1.smallFont, i18n.Get("event-detail.requirements"), new Vector2(contentBounds.X + 10, y), Game1.textColor);
        y += LineHeight(i18n.Get("event-detail.requirements"), contentBounds.Width - 36) + 18;
        if (conditions.Count == 0)
        {
            DrawWrapped(b, i18n.Get("condition.none"), contentBounds.X + 28, y + 12, contentBounds.Width - 72, Game1.textColor);
            return;
        }
        foreach (ConditionDisplayItem item in conditions)
        {
            int height = MeasureBlock(item);
            IReadOnlyList<string> lines = DetailLines(item);
            Rectangle block = new(contentBounds.X + 10, y, contentBounds.Width - 20, height - 8);
            IClickableMenu.drawTextureBox(b, block.X, block.Y, block.Width, block.Height, Color.White);
            int textY = y + 15;
            for (int index = 0; index < lines.Count; index++)
            {
                string line = lines[index];
                Color color = index == 0
                    ? item.Evaluation.Knowledge != ConditionKnowledge.Known ? new Color(150, 105, 20)
                    : item.Evaluation.Truth == ConditionTruth.True ? new Color(20, 110, 40) : new Color(150, 20, 20)
                    : Game1.textColor;
                textY += DrawWrapped(b, line, block.X + 18, textY, block.Width - 36, color) + 8;
            }
            y += height;
        }
    }

    private IReadOnlyList<string> DetailLines(ConditionDisplayItem item)
    {
        string state = item.Evaluation.Knowledge != ConditionKnowledge.Known || item.Evaluation.Truth == ConditionTruth.Unknown
            ? i18n.Get("event-detail.state.unknown")
            : i18n.Get(item.Evaluation.Truth == ConditionTruth.True ? "event-detail.state.met" : "event-detail.state.missing");
        List<string> lines =
        [
            state,
            i18n.Get("event-detail.description", new { description = item.Description })
        ];
        if (item.GapSubject is not null)
            lines.Add(i18n.Get("event-detail.gap-subject", new { subject = item.GapSubject }));
        if (item.CurrentValue is not null)
            lines.Add(i18n.Get("event-detail.current", new { current = item.CurrentValue }));
        if (item.RequiredValue is not null)
            lines.Add(i18n.Get("event-detail.required", new { required = item.RequiredValue }));
        if (item.Evaluation.Knowledge != ConditionKnowledge.Known)
            lines.Add(i18n.Get(item.Evaluation.Knowledge switch
            {
                ConditionKnowledge.MissingData => "event-detail.unknown.missing-data",
                ConditionKnowledge.Invalid => "event-detail.unknown.invalid",
                ConditionKnowledge.Error => "event-detail.unknown.error",
                _ => "event-detail.unknown.unsupported"
            }));
        lines.Add(i18n.Get("event-detail.source", new { source = i18n.Get(item.Expression.Source switch
        {
            ConditionSource.LegacyEventPrecondition => "event-detail.source.event",
            ConditionSource.GameStateQuery => "event-detail.source.gsq",
            ConditionSource.OpaqueEventPrecondition => "event-detail.source.opaque",
            _ => "event-detail.source.synthetic"
        }) }));
        lines.Add(i18n.Get("event-detail.raw", new { raw = item.Expression.RawSegment }));
        return lines;
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
        xPositionOnScreen = 0;
        yPositionOnScreen = 0;
        menuScale = (float)GalleryLayout.ScaleToFit(Game1.uiViewport.Width, Game1.uiViewport.Height, width, height, 24);
        drawOffsetX = (int)Math.Round((Game1.uiViewport.Width - width * menuScale) / 2f);
        drawOffsetY = (int)Math.Round((Game1.uiViewport.Height - height * menuScale) / 2f);
        viewportWidth = Game1.uiViewport.Width;
        viewportHeight = Game1.uiViewport.Height;
        contentBounds = new Rectangle(175, 275, 1300, 520);
        scrollTrack = new Rectangle(1510, 275, 24, 520);
        backBounds = new Rectangle(350, 842, 280, 52);
        replayBounds = new Rectangle(1042, 842, 280, 52);
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
        allClickableComponents =
        [
            new ClickableComponent(ToScreen(backBounds), "back") { myID = BackComponentId, rightNeighborID = canReplay() ? ReplayComponentId : -1 }
        ];
        if (canReplay())
            allClickableComponents.Add(new ClickableComponent(ToScreen(replayBounds), "replay") { myID = ReplayComponentId, leftNeighborID = BackComponentId });
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

    private (int X, int Y) ToLogical(int x, int y)
        => ((int)Math.Round((x - drawOffsetX) / menuScale), (int)Math.Round((y - drawOffsetY) / menuScale));
}
