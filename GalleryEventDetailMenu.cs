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
    private const int PhotosComponentId = 1002;
    private const int ReferenceComponentId = 5000;
    private const int ReferenceHeight = 42;
    private const int ScrollStep = 60;
    private const int RowPadding = 12;
    private const int StatusSize = GallerySpreadLayout.IconSize * 3;
    private const int StatusGap = 16;
    private static readonly RasterizerState ClipRasterizer = new() { ScissorTestEnable = true };
    private sealed record ReferenceItem(string EventId, GalleryEvent? Target, string Label, string? Message);
    private readonly GalleryCharacter? character;
    private readonly GalleryEvent entry;
    private readonly IReadOnlyList<ConditionDisplayItem> conditions;
    private readonly IReadOnlyDictionary<ConditionDisplayItem, IReadOnlyList<ReferenceItem>> conditionReferences;
    private readonly ITranslationHelper i18n;
    private readonly Texture2D background;
    private readonly Texture2D thumbnail;
    private readonly GalleryPhotos photos;
    private readonly Texture2D statusIcons;
    private readonly Texture2D scrollbarTrackTexture;
    private readonly GalleryCharacterPanel? leftPanel;
    private readonly Func<bool> canReplay;
    private readonly Action back;
    private readonly Action replay;
    private readonly Action<GalleryEvent> followReference;
    private readonly string backLabelKey;
    private readonly List<(ReferenceItem Item, Rectangle Bounds)> referenceLinks = [];
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
        GalleryCharacter? character,
        GalleryCatalog catalog,
        GalleryEvent entry,
        IReadOnlyList<ConditionDisplayItem> conditions,
        ITranslationHelper i18n,
        Texture2D background,
        Texture2D scene,
        Texture2D thumbnail,
        Texture2D statusIcons,
        Texture2D scrollbarTrackTexture,
        Func<bool> canReplay,
        Action back,
        Action replay,
        GalleryPhotos photos,
        Action<GalleryEvent> followReference,
        Func<string, IReadOnlyList<GalleryEvent>> resolveReference,
        string backLabelKey)
        : base(0, 0, GalleryMenu.MenuWidth, GalleryMenu.MenuHeight, true)
    {
        this.character = character;
        this.entry = entry;
        this.conditions = conditions;
        conditionReferences = conditions.Distinct().ToDictionary(item => item, item => (IReadOnlyList<ReferenceItem>)GalleryEventNavigation.References(item.Expression)
            .SelectMany(id =>
            {
                IReadOnlyList<GalleryEvent> targets = resolveReference(id);
                return targets.Count == 0
                    ? new[] { new ReferenceItem(id, null, "ID " + id, i18n.Get("nav.unavailable", new { id }).ToString()) }
                    : targets.Select(target => new ReferenceItem(id, target, "ID " + id + " >", targets.Count > 1
                        ? GalleryConditionPresentation.Location(target, i18n) + " | " + target.AssetName : null));
            }).ToArray());
        this.i18n = i18n;
        this.background = background;
        this.thumbnail = thumbnail;
        this.statusIcons = statusIcons;
        this.scrollbarTrackTexture = scrollbarTrackTexture;
        this.canReplay = canReplay;
        this.back = back;
        this.replay = replay;
        this.photos = photos;
        this.followReference = followReference;
        this.backLabelKey = backLabelKey;
        leftPanel = character is null ? null : new GalleryCharacterPanel(character, GalleryCharacterMenu.EventsFor(character, catalog), i18n, scene);
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
        if (contentBounds.Contains(x, y))
        {
            for (int index = 0; index < referenceLinks.Count; index++)
                if (ReferenceBounds(index).Contains(x, y))
                {
                    FocusReference(index);
                    if (referenceLinks[index].Item.Target is GalleryEvent target)
                        followReference(target);
                    return;
                }
        }
        if (upperRightCloseButton?.bounds.Contains(x, y) == true || backBounds.Contains(x, y))
        {
            Return();
            return;
        }
        if (canReplay() && replayBounds.Contains(x, y))
        {
            currentlySnappedComponent = allClickableComponents.First(component => component.myID == ReplayComponentId);
            replay();
            return;
        }
        if (Bounds(GallerySpreadLayout.DetailThumbnailBounds).Contains(x, y))
        {
            OpenPhotos();
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
        BuildClickableComponents();
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
        if (key is Keys.PageUp or Keys.PageDown)
        {
            ScrollBy(key == Keys.PageUp ? -contentBounds.Height : contentBounds.Height);
            return;
        }
        if (key == Keys.Enter)
        {
            ActivateFocused();
            return;
        }
        if (key is Keys.Up or Keys.Right or Keys.Down or Keys.Left)
        {
            applyMovementKey(key switch { Keys.Up => 0, Keys.Right => 1, Keys.Down => 2, _ => 3 });
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
            ActivateFocused();
            return;
        }
        if (button is Buttons.LeftShoulder or Buttons.RightShoulder)
        {
            ScrollBy(button == Buttons.LeftShoulder ? -contentBounds.Height : contentBounds.Height);
            return;
        }
        base.receiveGamePadButton(button);
    }

    private void ActivateFocused()
    {
        int reference = (currentlySnappedComponent?.myID ?? -1) - ReferenceComponentId;
        if (reference >= 0 && reference < referenceLinks.Count)
        {
            if (referenceLinks[reference].Item.Target is GalleryEvent target)
                followReference(target);
        }
        else if (currentlySnappedComponent?.myID == PhotosComponentId)
            OpenPhotos();
        else if (currentlySnappedComponent?.myID == ReplayComponentId && canReplay())
            replay();
        else if (currentlySnappedComponent?.myID == BackComponentId)
            Return();
    }

    public override void applyMovementKey(int direction)
    {
        if (referenceLinks.Count > 0 && direction is 0 or 2)
        {
            int current = currentlySnappedComponent?.myID ?? BackComponentId;
            int reference = current - ReferenceComponentId;
            int target = direction == 0
                ? (reference >= 0 ? reference - 1 : current == PhotosComponentId ? -1 : referenceLinks.Count - 1)
                : (reference >= 0 ? reference + 1 : current == PhotosComponentId ? 0 : referenceLinks.Count);
            if (target >= 0 && target < referenceLinks.Count)
                FocusReference(target);
            else
            {
                ScrollBy(target < 0 ? -MaxScroll : MaxScroll);
                currentlySnappedComponent = allClickableComponents.First(component => component.myID ==
                    (target < 0 ? PhotosComponentId : canReplay() ? ReplayComponentId : BackComponentId));
                snapCursorToCurrentSnappedComponent();
            }
            return;
        }
        if (direction == 0 && scroll == 0 && currentlySnappedComponent?.myID != PhotosComponentId)
        {
            currentlySnappedComponent = allClickableComponents.First(component => component.myID == PhotosComponentId);
            snapCursorToCurrentSnappedComponent();
            return;
        }
        if (direction == 2 && currentlySnappedComponent?.myID == PhotosComponentId)
        {
            currentlySnappedComponent = allClickableComponents.First(component => component.myID == (canReplay() ? ReplayComponentId : BackComponentId));
            snapCursorToCurrentSnappedComponent();
            return;
        }
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

    internal void RestoreNavigationFocus()
    {
        EnsureLayout();
        BuildClickableComponents();
    }

    public override void snapToDefaultClickableComponent()
    {
        currentlySnappedComponent = allClickableComponents?.FirstOrDefault(component => component.myID == (canReplay() ? ReplayComponentId : BackComponentId));
        snapCursorToCurrentSnappedComponent();
    }

    public override void draw(SpriteBatch b)
    {
        EnsureLayout();
        b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height), Color.Black * .45f);
        GalleryMenu.BeginScaled(b, menuScale, drawOffsetX, drawOffsetY);
        if (leftPanel is not null)
            leftPanel.DrawPhoto(b);
        else
            GalleryPhotos.DrawCover(b, photos.Cover(entry.Resolved.Identity) ?? thumbnail, Bounds(GallerySpreadLayout.PortraitBounds), Color.White);
        GalleryPhotos.DrawCover(b, photos.Cover(entry.Resolved.Identity) ?? thumbnail, Bounds(GallerySpreadLayout.DetailThumbnailBounds), Color.White);
        b.Draw(background, new Rectangle(0, 0, width, height), Color.White);
        GalleryMenu.DrawScrollbarTrack(b, scrollbarTrackTexture, scrollTrack);
        if (leftPanel is not null)
            leftPanel.DrawInformation(b);
        else
            DrawOtherEventInformation(b);
        DrawHeader(b);
        Rectangle photoBounds = Bounds(GallerySpreadLayout.DetailThumbnailBounds);
        b.Draw(Game1.mouseCursors2, new Rectangle(photoBounds.Right - 58, photoBounds.Bottom - 52, 54, 48),
            new Rectangle(72, 31, 18, 16), Focused(PhotosComponentId) ? new Color(255, 240, 160) : Color.White);
        BeginContentClip(b);
        DrawConditions(b);
        EndContentClip(b);
        if (MaxScroll > 0)
            GalleryMenu.DrawScrollbar(b, scrollThumb);
        if (canReplay())
            GalleryMenu.DrawButton(b, replayBounds, i18n.Get("event.replay"));
        GalleryMenu.DrawButton(b, backBounds, i18n.Get(backLabelKey));
        upperRightCloseButton?.draw(b);
        GalleryMenu.EndScaled(b);
        if (ToScreen(photoBounds).Contains(Game1.getMouseX(true), Game1.getMouseY(true)))
            IClickableMenu.drawHoverText(b, i18n.Get("photo.title"), Game1.smallFont);
        drawMouse(b);
    }

    private void DrawHeader(SpriteBatch b)
    {
        Rectangle title = Bounds(GallerySpreadLayout.TitleBounds);
        SpriteText.drawStringHorizontallyCenteredAt(b, i18n.Get("event-detail.title"), title.Center.X, title.Y + 8, maxWidth: title.Width - 24);
        EventOwner? owner = entry.Ownership.Owners.FirstOrDefault(value => value.Name == character?.Name);
        string hearts = owner?.FriendshipPoints is int points
            ? i18n.Get("event.hearts", new { hearts = (int)Math.Ceiling(points / 250d) })
            : i18n.Get("event.unspecified");
        DrawHeaderText(b, owner is null ? "ID " + entry.EventId : $"{hearts} · ID {entry.EventId}", Bounds(GallerySpreadLayout.DetailEventIdBounds));
        string location = GalleryLocationName.Resolve(entry.LocationName, Game1.getLocationFromName(entry.LocationName)?.DisplayName,
            key => i18n.Get(key).HasValue() ? i18n.Get(key).ToString() : null);
        DrawHeaderText(b, i18n.Get("event-detail.location", new { location }), Bounds(GallerySpreadLayout.DetailLocationBounds), wrap: true);
        DrawHeaderText(b, i18n.Get("event-detail.requirements"), Bounds(GallerySpreadLayout.ConditionHeadingBounds));
    }

    private void DrawOtherEventInformation(SpriteBatch b)
    {
        string[] lines = [i18n.Get("nav.other-event"), "ID " + entry.EventId,
            GalleryConditionPresentation.Location(entry, i18n), i18n.Get("nav.read-only")];
        for (int row = 0; row < lines.Length; row++)
            GalleryMenu.DrawCentered(b, lines[row], Bounds(GallerySpreadLayout.LeftRowBounds(row)));
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
        => MeasureRowBody(item) + conditionReferences[item].Sum(MeasureReference);

    private int MeasureReference(ReferenceItem item)
        => ReferenceHeight + (item.Message is null ? 0 : LineHeight(item.Message, RowTextWidth - 16) + 8);

    private int MeasureRowBody(ConditionDisplayItem item)
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
            DrawStatus(b, ConditionRowPresentation.Status(item.Evaluation), new Rectangle(row.Right - RowPadding - StatusSize, row.Y + MeasureRowBody(item) / 2 - StatusSize / 2, StatusSize, StatusSize));
            y += height;
        }
        for (int index = 0; index < referenceLinks.Count; index++)
        {
            Rectangle bounds = ReferenceBounds(index);
            if (!bounds.Intersects(contentBounds))
                continue;
            bool hovered = ToScreen(Rectangle.Intersect(bounds, contentBounds)).Contains(Game1.getMouseX(true), Game1.getMouseY(true));
            if (hovered || Focused(ReferenceComponentId + index))
                b.Draw(Game1.staminaRect, bounds, new Color(255, 240, 165) * .7f);
            ReferenceItem item = referenceLinks[index].Item;
            GalleryMenu.DrawLeftFitted(b, item.Label, new Rectangle(bounds.X + 8, bounds.Y + 2, bounds.Width - 16, ReferenceHeight - 8));
            if (item.Message is not null)
                DrawWrapped(b, item.Message, bounds.X + 8, bounds.Y + ReferenceHeight, bounds.Width - 16, GallerySpreadDrawing.Ink);
            b.Draw(Game1.staminaRect, new Rectangle(bounds.X + 8, bounds.Bottom - 4, bounds.Width - 16, 1), GallerySpreadDrawing.LightInk);
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
        referenceLinks.Clear();
        int rowY = contentBounds.Y;
        foreach (ConditionDisplayItem item in conditions)
        {
            int linkY = rowY + MeasureRowBody(item);
            foreach (ReferenceItem reference in conditionReferences[item])
            {
                int referenceHeight = MeasureReference(reference);
                referenceLinks.Add((reference, new Rectangle(contentBounds.X + RowPadding, linkY, RowTextWidth, referenceHeight)));
                linkY += referenceHeight;
            }
            rowY = linkY;
        }
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
        BuildClickableComponents();
    }

    private Rectangle ReferenceBounds(int index)
    {
        Rectangle bounds = referenceLinks[index].Bounds;
        bounds.Y -= scroll;
        return bounds;
    }

    private void FocusReference(int index)
    {
        Rectangle bounds = ReferenceBounds(index);
        ScrollBy(bounds.Top < contentBounds.Top ? bounds.Top - contentBounds.Top
            : bounds.Bottom > contentBounds.Bottom ? bounds.Bottom - contentBounds.Bottom : 0);
        currentlySnappedComponent = allClickableComponents.First(component => component.myID == ReferenceComponentId + index);
        if (Game1.options.snappyMenus && Game1.options.gamepadControls)
            snapCursorToCurrentSnappedComponent();
    }

    private void UpdateScrollbar()
    {
        const int thumbHeight = 40;
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
        allClickableComponents.Add(new ClickableComponent(ToScreen(Bounds(GallerySpreadLayout.DetailThumbnailBounds)), "photos") { myID = PhotosComponentId });
        for (int index = 0; index < referenceLinks.Count; index++)
        {
            Rectangle visible = Rectangle.Intersect(ReferenceBounds(index), contentBounds);
            if (visible.Width > 0 && visible.Height > 0)
                allClickableComponents.Add(new ClickableComponent(ToScreen(visible), referenceLinks[index].Item.EventId) { myID = ReferenceComponentId + index });
        }
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

    private void OpenPhotos()
    {
        Game1.activeClickableMenu = new GalleryPhotoMenu(photos, entry.Resolved.Identity, i18n, () =>
        {
            Game1.activeClickableMenu = this;
            currentlySnappedComponent = allClickableComponents.First(component => component.myID == PhotosComponentId);
            if (Game1.options.snappyMenus && Game1.options.gamepadControls)
                snapCursorToCurrentSnappedComponent();
        });
    }

    private static Rectangle Bounds((int X, int Y, int Width, int Height) bounds) => new(bounds.X, bounds.Y, bounds.Width, bounds.Height);
    private Rectangle ToScreen(Rectangle bounds) => GalleryMenu.ScaleRectangle(bounds, menuScale, drawOffsetX, drawOffsetY);
    private bool Focused(int id) => Game1.options.snappyMenus && Game1.options.gamepadControls && currentlySnappedComponent?.myID == id;
    private (int X, int Y) ToLogical(int x, int y) => ((int)Math.Round((x - drawOffsetX) / menuScale), (int)Math.Round((y - drawOffsetY) / menuScale));
}
