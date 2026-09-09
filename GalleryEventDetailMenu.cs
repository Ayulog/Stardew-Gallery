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
    private readonly GalleryViewContext context;
    private readonly GalleryPageState state;
    private readonly GalleryCharacter? character;
    private readonly GalleryEvent entry;
    private readonly IReadOnlyList<ConditionDisplayItem> conditions;
    private readonly IReadOnlyDictionary<ConditionDisplayItem, IReadOnlyList<ReferenceItem>> conditionReferences;
    private readonly IReadOnlyDictionary<ConditionDisplayItem, string> internalNotes;
    private readonly ITranslationHelper i18n;
    private readonly Texture2D background;
    private readonly Texture2D thumbnail;
    private readonly GalleryPhotos photos;
    private readonly Texture2D statusIcons;
    private readonly Texture2D scrollbarTrackTexture;
    private readonly GalleryCharacterPanel? leftPanel;
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

    internal GalleryEventDetailMenu(GalleryViewContext context, GalleryPageState state, IReadOnlyList<ConditionDisplayItem> conditions)
        : base(0, 0, GalleryDrawing.MenuWidth, GalleryDrawing.MenuHeight, true)
    {
        this.context = context;
        this.state = state;
        entry = state.Event is EventIdentity identity ? context.Catalog.Find(identity)
            ?? throw new ArgumentException("The event is no longer in the catalog.", nameof(state))
            : throw new ArgumentException("An event identity is required.", nameof(state));
        character = entry.Kind == StoryKind.Heart ? GalleryEventNavigation.Owner(context.Catalog, entry, state.CharacterName) : null;
        this.conditions = conditions;
        i18n = context.I18n;
        var references = conditions.Distinct().ToDictionary(item => item, item => GalleryEventNavigation.References(item.Expression)
            .Select(id => (Id: id, Result: StoryDependencyLookup.Find(context.Catalog, id))).ToArray());
        internalNotes = references.ToDictionary(pair => pair.Key, pair => string.Join("\n", pair.Value
            .Where(reference => reference.Result.HasInternalStep)
            .Select(reference => GalleryConditionPresentation.InternalStep(reference.Result, reference.Id, i18n))));
        conditionReferences = conditions.Distinct().ToDictionary(item => item, item => (IReadOnlyList<ReferenceItem>)GalleryEventNavigation.References(item.Expression)
            .SelectMany(id =>
            {
                StoryDependencyResult result = references[item].First(reference => reference.Id == id).Result;
                IReadOnlyList<GalleryEvent> targets = result.Stories;
                return result.Missing
                    ? new[] { new ReferenceItem(id, null, "ID " + id, i18n.Get("nav.unavailable", new { id }).ToString()) }
                    : targets.Select(target => new ReferenceItem(id, target, "ID " + id + " >", targets.Count > 1
                        ? GalleryConditionPresentation.Location(target, i18n) + " | " + target.AssetName : null));
            }).ToArray());
        background = context.Textures.Detail;
        thumbnail = context.Textures.Thumbnail;
        statusIcons = context.Textures.ConditionIcons;
        scrollbarTrackTexture = context.Textures.Scrollbar;
        photos = context.Photos;
        leftPanel = character is null ? null : new GalleryCharacterPanel(character, context.Catalog.HeartEventsFor(character.Name), i18n, context.Textures.Scene);
        scroll = state.Scroll;
        int initialFocus = state.Focus;
        RecalculateLayout();
        currentlySnappedComponent = allClickableComponents.FirstOrDefault(component => component.myID == initialFocus)
            ?? currentlySnappedComponent;
        SaveState();
        if (Game1.options.snappyMenus && Game1.options.gamepadControls)
            SnapCurrent();
    }

    public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
    {
        base.gameWindowSizeChanged(oldBounds, newBounds);
        RecalculateLayout();
    }

    public override void update(GameTime time)
    {
        base.update(time);
        SaveState();
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
                        FollowReference(target);
                    return;
                }
        }
        if (upperRightCloseButton?.bounds.Contains(x, y) == true)
        {
            SaveState();
            context.Navigation.Close();
            return;
        }
        if (backBounds.Contains(x, y))
        {
            Return();
            return;
        }
        if (CanReplay() && replayBounds.Contains(x, y))
        {
            currentlySnappedComponent = allClickableComponents.First(component => component.myID == ReplayComponentId);
            Replay();
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
        if (key == Keys.Escape || Game1.options.menuButton.Any(binding => binding.key == key))
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
                FollowReference(target);
        }
        else if (currentlySnappedComponent?.myID == PhotosComponentId)
            OpenPhotos();
        else if (currentlySnappedComponent?.myID == ReplayComponentId && CanReplay())
            Replay();
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
                    (target < 0 ? PhotosComponentId : CanReplay() ? ReplayComponentId : BackComponentId));
                SnapCurrent();
            }
            return;
        }
        if (direction == 0 && scroll == 0 && currentlySnappedComponent?.myID != PhotosComponentId)
        {
            currentlySnappedComponent = allClickableComponents.First(component => component.myID == PhotosComponentId);
            SnapCurrent();
            return;
        }
        if (direction == 2 && currentlySnappedComponent?.myID == PhotosComponentId)
        {
            currentlySnappedComponent = allClickableComponents.First(component => component.myID == (CanReplay() ? ReplayComponentId : BackComponentId));
            SnapCurrent();
            return;
        }
        if (direction is 0 or 2)
            ScrollBy(direction == 0 ? -ScrollStep : ScrollStep);
        else if (direction is 1 or 3)
        {
            int target = direction == 1 && CanReplay() ? ReplayComponentId : BackComponentId;
            currentlySnappedComponent = allClickableComponents.FirstOrDefault(component => component.myID == target);
            SnapCurrent();
        }
    }

    public override void snapToDefaultClickableComponent()
    {
        currentlySnappedComponent = allClickableComponents?.FirstOrDefault(component => component.myID == state.Focus)
            ?? allClickableComponents?.FirstOrDefault(component => component.myID == (CanReplay() ? ReplayComponentId : BackComponentId));
        SnapCurrent();
    }

    public override void draw(SpriteBatch b)
    {
        EnsureLayout();
        b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height), Color.Black * .45f);
        GalleryDrawing.BeginScaled(b, menuScale, drawOffsetX, drawOffsetY);
        if (leftPanel is not null)
            leftPanel.DrawPhoto(b);
        else
            GalleryPhotos.DrawCover(b, photos.Cover(entry.Resolved.Identity) ?? thumbnail, Bounds(GallerySpreadLayout.PortraitBounds), Color.White);
        GalleryPhotos.DrawCover(b, photos.Cover(entry.Resolved.Identity) ?? thumbnail, Bounds(GallerySpreadLayout.DetailThumbnailBounds), Color.White);
        b.Draw(background, new Rectangle(0, 0, width, height), Color.White);
        GalleryDrawing.DrawScrollbarTrack(b, scrollbarTrackTexture, scrollTrack);
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
            GalleryDrawing.DrawScrollbar(b, scrollThumb);
        if (CanReplay())
            GalleryDrawing.DrawButton(b, replayBounds, i18n.Get("event.replay"));
        GalleryDrawing.DrawButton(b, backBounds, i18n.Get("nav.back"));
        upperRightCloseButton?.draw(b);
        GalleryDrawing.EndScaled(b);
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
        DrawHeaderText(b, owner is null ? "ID " + entry.EventId : hearts + GalleryDrawing.TextSeparator + $"ID {entry.EventId}", Bounds(GallerySpreadLayout.DetailEventIdBounds));
        string location = GalleryLocationName.Resolve(entry.LocationName, Game1.getLocationFromName(entry.LocationName)?.DisplayName,
            key => i18n.Get(key).HasValue() ? i18n.Get(key).ToString() : null);
        DrawHeaderText(b, i18n.Get("event-detail.location", new { location }), Bounds(GallerySpreadLayout.DetailLocationBounds), wrap: true);
        DrawHeaderText(b, i18n.Get("event-detail.requirements"), Bounds(GallerySpreadLayout.ConditionHeadingBounds));
    }

    private void DrawOtherEventInformation(SpriteBatch b)
    {
        ReplayAccess access = context.ReplayAccess(entry);
        string[] lines = [i18n.Get(entry.Kind == StoryKind.Heart ? "query.kind-heart" : "query.kind-ordinary"), "ID " + entry.EventId,
            GalleryConditionPresentation.Location(entry, i18n),
            i18n.Get("query.source", new { source = entry.AssetName }),
            access.Allowed ? i18n.Get("event.replay") : access.ReasonKey is null ? "-" : i18n.Get(access.ReasonKey)];
        for (int row = 0; row < lines.Length; row++)
            GalleryDrawing.DrawCentered(b, lines[row], GalleryDrawing.Inset(Bounds(GallerySpreadLayout.LeftRowBounds(row)), 40));
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
        if (internalNotes[item].Length > 0)
            height += LineHeight(internalNotes[item], RowTextWidth) + 6;
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
                textHeight += 6 + DrawWrapped(b, i18n.Get(reasonKey), row.X + RowPadding, textY + textHeight + 6, RowTextWidth, new Color(125, 90, 35));
            if (internalNotes[item].Length > 0)
                DrawWrapped(b, internalNotes[item], row.X + RowPadding, textY + textHeight + 6, RowTextWidth, GallerySpreadDrawing.Ink);
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
            GalleryDrawing.DrawLeftFitted(b, item.Label, new Rectangle(bounds.X + 8, bounds.Y + 2, bounds.Width - 16, ReferenceHeight - 8));
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
        width = GalleryDrawing.MenuWidth;
        height = GalleryDrawing.MenuHeight;
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
            SnapCurrent();
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
        int previous = currentlySnappedComponent?.myID ?? (CanReplay() ? ReplayComponentId : BackComponentId);
        allClickableComponents = [new ClickableComponent(ToScreen(backBounds), "back") { myID = BackComponentId, rightNeighborID = CanReplay() ? ReplayComponentId : -1 }];
        if (CanReplay())
            allClickableComponents.Add(new ClickableComponent(ToScreen(replayBounds), "replay") { myID = ReplayComponentId, leftNeighborID = BackComponentId });
        allClickableComponents.Add(new ClickableComponent(ToScreen(Bounds(GallerySpreadLayout.DetailThumbnailBounds)), "photos") { myID = PhotosComponentId });
        for (int index = 0; index < referenceLinks.Count; index++)
        {
            Rectangle visible = Rectangle.Intersect(ReferenceBounds(index), contentBounds);
            if (visible.Width > 0 && visible.Height > 0)
                allClickableComponents.Add(new ClickableComponent(ToScreen(visible), referenceLinks[index].Item.EventId) { myID = ReferenceComponentId + index });
        }
        currentlySnappedComponent = allClickableComponents.FirstOrDefault(component => component.myID == previous) ?? allClickableComponents[0];
        SaveState();
        if (Game1.options.snappyMenus && Game1.options.gamepadControls)
            SnapCurrent();
    }

    private void Return()
    {
        Game1.playSound("bigDeSelect");
        SaveState();
        context.Navigation.Back();
    }

    private void OpenPhotos()
    {
        SaveState(PhotosComponentId);
        context.Navigation.OpenPhotos(entry.Resolved.Identity);
    }

    private bool CanReplay() => context.ReplayAccess(entry).Allowed;

    private void SaveState(int? focus = null)
    {
        state.Scroll = scroll;
        state.Focus = focus ?? currentlySnappedComponent?.myID ?? -1;
    }

    private void SnapCurrent()
    {
        SaveState();
        snapCursorToCurrentSnappedComponent();
    }

    private void Replay()
    {
        SaveState(ReplayComponentId);
        context.Navigation.Replay(entry.Resolved.Identity);
    }

    private void FollowReference(GalleryEvent target)
    {
        SaveState();
        context.Navigation.OpenEvent(target.Resolved.Identity);
    }

    private static Rectangle Bounds((int X, int Y, int Width, int Height) bounds) => new(bounds.X, bounds.Y, bounds.Width, bounds.Height);
    private Rectangle ToScreen(Rectangle bounds) => GalleryDrawing.ScaleRectangle(bounds, menuScale, drawOffsetX, drawOffsetY);
    private bool Focused(int id) => Game1.options.snappyMenus && Game1.options.gamepadControls && currentlySnappedComponent?.myID == id;
    private (int X, int Y) ToLogical(int x, int y) => ((int)Math.Round((x - drawOffsetX) / menuScale), (int)Math.Round((y - drawOffsetY) / menuScale));
}
