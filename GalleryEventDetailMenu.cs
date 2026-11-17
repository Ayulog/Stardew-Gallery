using System.Globalization;
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
    private bool keyboardFocus;
    private Point lastMouse;
    private const int BackComponentId = 1000;
    private const int ReplayComponentId = 1001;
    private const int PhotosComponentId = 1002;
    private const int RenameComponentId = 1003;
    private const int SourceComponentId = 1004;
    private const int ForkRouteComponentId = 1005;
    private const int FavoriteComponentId = 1006;
    private static Rectangle FavoriteBounds => Bounds(GallerySpreadLayout.DetailFavoriteBounds);
    private static Rectangle RenameBounds => Bounds(GallerySpreadLayout.DetailRenameBounds);
    private static Rectangle ForkRouteBounds => new(1045, 308, 420, 42);
    private const int ReferenceComponentId = 5000;
    private const int ReferenceHeight = 42;
    private const int ScrollStep = 60;
    private const int RowPadding = 12;
    private const int StatusSize = GallerySpreadLayout.IconSize * 3;
    private const int StatusGap = 16;
    private static readonly RasterizerState ClipRasterizer = new() { ScissorTestEnable = true };
    private sealed record ReferenceItem(string EventId, GalleryEvent? Target, string Label, string? Message, string? PrerequisiteId = null);
    private readonly GalleryViewContext context;
    private readonly GalleryPageState state;
    private readonly GalleryCharacter? character;
    private readonly GalleryEvent entry;
    private readonly IReadOnlyList<ConditionDisplayItem> conditions;
    private readonly string sourceSummary;
    private readonly string sourceStatus;
    private readonly IReadOnlyList<string> sourceParagraphs;
    private readonly string bgmSummary = "";
    private readonly string? storyQuote;
    private readonly bool isParallelBranch;
    private readonly IReadOnlyList<ReplayForkOption>? forkOptions;
    private int forkOptionIndex;
    private IReadOnlyList<string> wrappedSourceParagraphs = [];
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
        // Resolve once when opening the page; drawing and scrolling never load event assets.
        EventSourceDisplay source = context.EventSource(entry.Resolved);
        sourceSummary = source.Summary;
        sourceStatus = source.Status;
        sourceParagraphs = source.Details.Where(text => !string.IsNullOrWhiteSpace(text)).ToArray();
        isParallelBranch = context.IsParallelBranch(entry);
        bgmSummary = context.MusicTitle(entry);
        storyQuote = context.StoryQuote(entry);
        forkOptions = ReplayBranchControl.ReadForkOptions(
            entry.Resolved,
            raw => Event.ParseCommands(raw),
            cmd => ArgUtility.SplitBySpaceQuoteAware(cmd));
        forkOptionIndex = forkOptions.Count > 0 ? Math.Clamp(state.ForkOptionIndex, 0, forkOptions.Count - 1) : 0;
        var references = conditions.Distinct().ToDictionary(item => item, item => GalleryEventNavigation.References(item.Expression)
            .Select(id => (Id: id, Result: StoryDependencyLookup.Find(context.Catalog, id))).ToArray());
        internalNotes = references.ToDictionary(pair => pair.Key, pair => string.Join("\n", pair.Value
            .Where(reference => reference.Result.HasInternalStep && reference.Result.Prerequisite is null)
            .Select(reference => GalleryConditionPresentation.InternalStep(reference.Result, reference.Id, i18n))));
        conditionReferences = conditions.Distinct().ToDictionary(item => item, item => (IReadOnlyList<ReferenceItem>)GalleryEventNavigation.References(item.Expression)
            .SelectMany(id =>
            {
                StoryDependencyResult result = references[item].First(reference => reference.Id == id).Result;
                IReadOnlyList<GalleryEvent> targets = result.Stories;
                if (result.Prerequisite is { } prerequisite)
                {
                    string prereqTitle = PrerequisitePresentation.Title(prerequisite, context);
                    string prereqDisplay = prereqTitle == prerequisite.EventId ? "ID " + id : $"{prereqTitle} (ID {id})";
                    return new[] { new ReferenceItem(id, null, $"{i18n.Get("query.kind-prerequisite")}: {prereqDisplay}  >", null, id) };
                }
                return result.Missing
                    ? new[] { new ReferenceItem(id, null, "ID " + id, i18n.Get("nav.unavailable", new { id }).ToString()) }
                    : targets.Select(target =>
                    {
                        string storyLabel = $"{context.CardTitle(target)} (ID {id})";
                        if (targets.Count > 1)
                            storyLabel += $" · {GalleryConditionPresentation.Location(target, i18n)}";
                        return new ReferenceItem(id, target, $"{i18n.Get("nav.other-event")}: {storyLabel}  >", null);
                    });
            }).ToArray());
        background = context.Textures.Detail;
        thumbnail = context.Textures.Thumbnail;
        statusIcons = context.Textures.ConditionIcons;
        scrollbarTrackTexture = context.Textures.Scrollbar;
        photos = context.Photos;
        leftPanel = character is null ? null : new GalleryCharacterPanel(character, context.Catalog.HeartEventsFor(character.Name), i18n, context.Textures.Scene, context.Appearance, context.Catalog);
        leftPanel?.Prepare();
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
        leftPanel?.Prepare();
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
                    FollowItem(referenceLinks[index].Item);
                    return;
                }
        }
        if (upperRightCloseButton?.bounds.Contains(x, y) == true)
        {
            SaveState();
            context.Navigation.Close();
            return;
        }
        if (GalleryBookChrome.TryClickRibbonTab(x, y, state.OriginTab, context.Navigation, () => SaveState(), albumPage: true))
            return;
        if (backBounds.Contains(x, y))
        {
            Return();
            return;
        }
        if (FavoriteBounds.Contains(x, y)) { FocusComponent(FavoriteComponentId); ToggleFavorite(); return; }
        if (RenameBounds.Contains(x, y)) { Rename(); return; }
        if (Bounds(GallerySpreadLayout.DetailSourceBounds).Contains(x, y)) { ToggleSource(); return; }
        if (HasForkSelector() && ForkRouteBounds.Contains(x, y))
        {
            FocusComponent(ForkRouteComponentId);
            StepForkRoute(1);
            return;
        }
        if (CanReplay() && replayBounds.Contains(x, y))
        {
            currentlySnappedComponent = allClickableComponents.FirstOrDefault(component => component.myID == ReplayComponentId) ?? currentlySnappedComponent;
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

    // Keyboard navigation shows its focus until the mouse moves again.
    public override void performHoverAction(int x, int y)
    {
        if (lastMouse != new Point(x, y))
        {
            if (lastMouse != default) keyboardFocus = false;
            lastMouse = new Point(x, y);
        }
        base.performHoverAction(x, y);
    }

    public override void receiveKeyPress(Keys key)
    {
        if (key is Keys.Up or Keys.Right or Keys.Down or Keys.Left or Keys.Enter) keyboardFocus = true;
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
        // A/X use Game1's mouse emulation; handling A here would activate twice.
        if (button is Buttons.LeftShoulder or Buttons.RightShoulder)
        {
            ScrollBy(button == Buttons.LeftShoulder ? -contentBounds.Height : contentBounds.Height);
            return;
        }
        base.receiveGamePadButton(button);
    }

    private void ActivateFocused()
    {
        int focusedId = currentlySnappedComponent?.myID ?? -1;
        if (focusedId >= GalleryBookChrome.RibbonTabBaseId && focusedId < GalleryBookChrome.RibbonTabBaseId + GallerySpreadLayout.RibbonTabCount)
        {
            SaveState();
            Game1.playSound("shwip");
            context.Navigation.SwitchTab((GalleryBookTab)(focusedId - GalleryBookChrome.RibbonTabBaseId));
            return;
        }
        int reference = focusedId - ReferenceComponentId;
        if (reference >= 0 && reference < referenceLinks.Count)
        {
            FollowItem(referenceLinks[reference].Item);
        }
        else if (currentlySnappedComponent?.myID == SourceComponentId) ToggleSource();
        else if (currentlySnappedComponent?.myID == ForkRouteComponentId && HasForkSelector()) StepForkRoute(1);
        else if (currentlySnappedComponent?.myID == FavoriteComponentId) ToggleFavorite();
        else if (currentlySnappedComponent?.myID == RenameComponentId) Rename();
        else if (currentlySnappedComponent?.myID == PhotosComponentId)
            OpenPhotos();
        else if (currentlySnappedComponent?.myID == ReplayComponentId && CanReplay())
            Replay();
        else if (currentlySnappedComponent?.myID == BackComponentId)
            Return();
    }

    public override void applyMovementKey(int direction)
    {
        if (direction is < 0 or > 3)
            return;
        int current = currentlySnappedComponent?.myID ?? BackComponentId;
        if (current >= GalleryBookChrome.RibbonTabBaseId && current < GalleryBookChrome.RibbonTabBaseId + GallerySpreadLayout.RibbonTabCount)
        {
            int tab = current - GalleryBookChrome.RibbonTabBaseId;
            if (direction == 3 && tab > 0) FocusComponent(GalleryBookChrome.RibbonTabBaseId + tab - 1);
            else if (direction == 1 && tab + 1 < GallerySpreadLayout.RibbonTabCount) FocusComponent(GalleryBookChrome.RibbonTabBaseId + tab + 1);
            else if (direction == 1) FocusComponent(BackComponentId);
            else if (direction == 0) FocusComponent(FavoriteComponentId);
            return;
        }

        if (current == SourceComponentId)
        {
            if (direction == 1) FocusComponent(PhotosComponentId);
            else if (direction == 3) FocusComponent(GalleryBookChrome.RibbonTabBaseId + (int)state.OriginTab);
            else if (direction == 2) FocusComponent(FavoriteComponentId);
            return;
        }

        if (current is FavoriteComponentId or RenameComponentId)
        {
            if (direction == 0) FocusComponent(SourceComponentId);
            else if (direction == 3)
                FocusComponent(current == RenameComponentId ? FavoriteComponentId : GalleryBookChrome.RibbonTabBaseId + (int)state.OriginTab);
            else if (direction == 1)
                FocusComponent(current == FavoriteComponentId ? RenameComponentId : PhotosComponentId);
            else if (direction == 2)
            {
                if (HasForkSelector()) FocusComponent(ForkRouteComponentId);
                else if (referenceLinks.Count > 0) FocusReference(0);
                else FocusComponent(CanReplay() ? ReplayComponentId : BackComponentId);
            }
            return;
        }

        if (current == PhotosComponentId)
        {
            if (direction == 3) FocusComponent(RenameComponentId);
            else if (direction == 2)
            {
                if (HasForkSelector()) FocusComponent(ForkRouteComponentId);
                else if (referenceLinks.Count > 0) FocusReference(0);
                else FocusComponent(CanReplay() ? ReplayComponentId : BackComponentId);
            }
            return;
        }

        if (current == ForkRouteComponentId && HasForkSelector())
        {
            if (direction is 1 or 3) StepForkRoute(direction == 1 ? 1 : -1);
            else if (direction == 0) FocusComponent(FavoriteComponentId);
            else if (referenceLinks.Count > 0) FocusReference(0);
            else FocusComponent(CanReplay() ? ReplayComponentId : BackComponentId);
            return;
        }

        if (current is BackComponentId or ReplayComponentId)
        {
            if (direction == 3)
                FocusComponent(current == ReplayComponentId ? BackComponentId : GalleryBookChrome.RibbonTabBaseId + 2);
            else if (direction == 1 && current == BackComponentId && CanReplay())
                FocusComponent(ReplayComponentId);
            else if (direction == 0)
            {
                if (referenceLinks.Count > 0) FocusReference(referenceLinks.Count - 1);
                else if (scroll > 0) ScrollBy(-ScrollStep);
                else FocusComponent(HasForkSelector() ? ForkRouteComponentId : FavoriteComponentId);
            }
            else if (direction == 2 && scroll < MaxScroll)
                ScrollBy(ScrollStep);
            return;
        }

        if (referenceLinks.Count > 0)
        {
            int reference = current - ReferenceComponentId;
            int target = reference >= 0 ? reference + (direction == 0 ? -1 : 1)
                : direction == 0 ? referenceLinks.Count - 1 : referenceLinks.Count;
            if (target >= 0 && target < referenceLinks.Count) FocusReference(target);
            else
            {
                ScrollBy(target < 0 ? -MaxScroll : MaxScroll);
                FocusComponent(target < 0
                    ? (HasForkSelector() ? ForkRouteComponentId : FavoriteComponentId)
                    : CanReplay() ? ReplayComponentId : BackComponentId);
            }
            return;
        }
        if (direction == 0 && scroll == 0) FocusComponent(HasForkSelector() ? ForkRouteComponentId : FavoriteComponentId);
        else ScrollBy(direction == 0 ? -ScrollStep : ScrollStep);
    }

    // Game1 repopulates components by reflection when input switches to a controller; rebuild the gallery's own instead.
    public override void populateClickableComponentList() => BuildClickableComponents();

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
        Texture2D? coverTex = photos.Cover(entry.Resolved.Identity);
        bool unlockedForViewing = context.IsStoryUnlocked(entry);
        bool seen = Game1.player?.eventsSeen.Contains(entry.EventId) == true;
        string leadClue = context.CardPrimaryLead(entry, character?.Name);
        string locClue = context.Locations.Get(entry.LocationName);
        Rectangle portraitBackdrop = GalleryDrawing.Inset(Bounds(GallerySpreadLayout.PortraitBounds), -4, -4);
        if (leftPanel is not null)
            leftPanel.DrawPhoto(b);
        else if (coverTex is not null)
            GalleryPhotos.DrawCover(b, coverTex, portraitBackdrop, unlockedForViewing ? Color.White : new Color(215, 210, 198));
        else if (!seen)
            GalleryBookChrome.DrawLockedStoryEnvelope(b, portraitBackdrop, leadClue, locClue, isParallelBranch && unlockedForViewing);
        else
            GalleryPhotos.DrawCover(b, thumbnail, portraitBackdrop, Color.White);

        Rectangle photoBounds = Bounds(GallerySpreadLayout.DetailThumbnailBounds);
        if (coverTex is not null)
            GalleryPhotos.DrawCover(b, coverTex, photoBounds, unlockedForViewing ? Color.White : new Color(215, 210, 198));
        else if (!seen)
            GalleryBookChrome.DrawLockedStoryEnvelope(b, photoBounds, leadClue, locClue, isParallelBranch && unlockedForViewing);
        else
            GalleryPhotos.DrawCover(b, thumbnail, photoBounds, Color.White);

        b.Draw(background, new Rectangle(0, 0, width, height), Color.White);
        (int logicalMouseX, int logicalMouseY) = ToLogical(Game1.getMouseX(true), Game1.getMouseY(true));
        bool showFocus = Game1.options.snappyMenus && Game1.options.gamepadControls || keyboardFocus;
        GalleryBookChrome.DrawRibbonTabs(b, state.OriginTab, currentlySnappedComponent?.myID ?? -1, showFocus,
            new Point(logicalMouseX, logicalMouseY), i18n, out string? ribbonHover, albumPage: true);
        GalleryDrawing.DrawScrollbarTrack(b, scrollbarTrackTexture, scrollTrack);
        if (leftPanel is not null)
            leftPanel.DrawInformation(b);
        else
            DrawOtherEventInformation(b);
        DrawHeader(b);

        bool photoHot = photoBounds.Contains(logicalMouseX, logicalMouseY) || Focused(PhotosComponentId);
        Rectangle camBox = new(photoBounds.Right - 42, photoBounds.Bottom - 40, 36, 34);
        b.Draw(Game1.fadeToBlackRect, camBox, Color.Black * 0.45f);
        GalleryUiSkin.DrawHudIcon(b, UiHudIcon.Camera, new Rectangle(camBox.X + 2, camBox.Y + 1, 32, 32),
            photoHot ? new Color(255, 244, 185) : Color.White);
        if (Focused(PhotosComponentId))
            GalleryUiSkin.DrawFocusCorners(b, GalleryDrawing.Inset(photoBounds, -4, -4), 12);

        BeginContentClip(b);
        if (state.SourceExpanded) DrawSourceDetails(b);
        else DrawConditions(b);
        EndContentClip(b);
        if (MaxScroll > 0)
            GalleryDrawing.DrawScrollbar(b, scrollThumb);

        ReplayAccess replayAccess = context.ReplayAccess(entry);
        bool canReplay = replayAccess.Allowed;
        bool replayHot = canReplay && (replayBounds.Contains(logicalMouseX, logicalMouseY) || Focused(ReplayComponentId));
        GalleryUiSkin.DrawWoodButton(b, replayBounds, i18n.Get("event.replay"),
            !canReplay ? UiButtonState.Disabled : replayHot ? UiButtonState.Hover : UiButtonState.Normal,
            leadingIcon: UiHudIcon.FastForward);
        if (Focused(ReplayComponentId))
            GalleryUiSkin.DrawFocusCorners(b, GalleryDrawing.Inset(replayBounds, -4, -4), 12);

        bool backHot = backBounds.Contains(logicalMouseX, logicalMouseY) || Focused(BackComponentId);
        GalleryDrawing.DrawButton(b, backBounds, i18n.Get("nav.back"), backHot);
        if (Focused(BackComponentId))
            GalleryUiSkin.DrawFocusCorners(b, GalleryDrawing.Inset(backBounds, -4, -4), 12);

        bool favorited = context.IsFavorite(entry.Resolved.Identity);
        bool favHot = FavoriteBounds.Contains(logicalMouseX, logicalMouseY) || Focused(FavoriteComponentId);
        DrawFavoriteWoodButton(b, FavoriteBounds, favorited, favHot);
        if (Focused(FavoriteComponentId))
            GalleryUiSkin.DrawFocusCorners(b, GalleryDrawing.Inset(FavoriteBounds, -4, -4), 10);

        bool renameHot = RenameBounds.Contains(logicalMouseX, logicalMouseY) || Focused(RenameComponentId);
        GalleryDrawing.DrawButton(b, RenameBounds, i18n.Get("name.edit"), renameHot);
        if (Focused(RenameComponentId))
            GalleryUiSkin.DrawFocusCorners(b, GalleryDrawing.Inset(RenameBounds, -4, -4), 10);

        upperRightCloseButton?.draw(b);
        GalleryDrawing.EndScaled(b);
        if (ribbonHover is not null)
            IClickableMenu.drawHoverText(b, ribbonHover, Game1.smallFont);
        else if (!canReplay && replayBounds.Contains(logicalMouseX, logicalMouseY))
            IClickableMenu.drawHoverText(b, Game1.parseText(i18n.Get(replayAccess.ReasonKey ?? "event.locked"), Game1.smallFont,
                Math.Max(160, Math.Min(520, viewportWidth - 80))), Game1.smallFont);
        else if (SourceHovered() || Focused(SourceComponentId))
            IClickableMenu.drawHoverText(b, Game1.parseText(state.SourceExpanded ? i18n.Get("source.collapse")
                : sourceSummary + "\n" + sourceStatus + "\n" + i18n.Get("source.expand"), Game1.smallFont,
                Math.Max(160, Math.Min(520, viewportWidth - 80))), Game1.smallFont);
        else if (HasForkSelector() && (ForkRouteHovered() || Focused(ForkRouteComponentId)))
            IClickableMenu.drawHoverText(b, Game1.parseText(i18n.Get("replay.route-tooltip", new { detail = CurrentForkDetail() }),
                Game1.smallFont, Math.Max(160, Math.Min(520, viewportWidth - 80))), Game1.smallFont);
        else if (Bounds(GallerySpreadLayout.DetailEventIdBounds).Contains(logicalMouseX, logicalMouseY)
            || leftPanel is null && Bounds(GallerySpreadLayout.LeftRowBounds(1)).Contains(logicalMouseX, logicalMouseY))
            IClickableMenu.drawHoverText(b, Game1.parseText(context.CardTitle(entry, character?.Name) + "\n" + i18n.Get("event-detail.event-id", new { id = entry.EventId }),
                Game1.smallFont, Math.Max(160, Math.Min(560, viewportWidth - 80))), Game1.smallFont);
        else if (ToScreen(Bounds(GallerySpreadLayout.DetailLocationBounds)).Contains(Game1.getMouseX(true), Game1.getMouseY(true)))
            IClickableMenu.drawHoverText(b, Game1.parseText(LocationAndMusicLine(), Game1.smallFont,
                Math.Max(160, Math.Min(520, viewportWidth - 80))), Game1.smallFont);
        else if (ToScreen(photoBounds).Contains(Game1.getMouseX(true), Game1.getMouseY(true)))
            IClickableMenu.drawHoverText(b, i18n.Get("photo.title"), Game1.smallFont);
        drawMouse(b);
    }

    private void DrawFavoriteWoodButton(SpriteBatch b, Rectangle bounds, bool favorited, bool hot)
    {
        GalleryUiSkin.DrawWoodButton(b, bounds, "", hot ? UiButtonState.Hover : UiButtonState.Normal);
        string text = i18n.Get(favorited ? "favorite.active" : "favorite.add");
        int iconSize = 20;
        float textScale = Math.Min(0.95f, (bounds.Height - 2f) / Math.Max(1f, Game1.smallFont.LineSpacing));
        int innerWidth = bounds.Width - 24;
        string fitted = GalleryTextFit.Ellipsize(text, Math.Max(20, innerWidth - iconSize - 6) / textScale, s => Game1.smallFont.MeasureString(s).X);
        Vector2 size = Game1.smallFont.MeasureString(fitted) * textScale;
        int totalW = iconSize + 6 + (int)Math.Ceiling(size.X);
        int startX = bounds.Center.X - totalW / 2;
        GalleryUiSkin.DrawBadge(b, favorited ? UiBadge.HeartFull : UiBadge.HeartEmpty,
            new Rectangle(startX, bounds.Center.Y - iconSize / 2, iconSize, iconSize));
        b.DrawString(Game1.smallFont, fitted, new Vector2(startX + iconSize + 6, bounds.Center.Y - size.Y / 2f),
            new Color(62, 32, 12), 0f, Vector2.Zero, textScale, SpriteEffects.None, 0f);
    }

    private void DrawHeader(SpriteBatch b)
    {
        GalleryBookChrome.DrawPageTitle(b, i18n.Get("event-detail.title"), menuScale, drawOffsetX, drawOffsetY);
        string titleText = context.CardTitle(entry, character?.Name) + GalleryDrawing.TextSeparator + "ID " + entry.EventId;
        DrawHeaderLine(b, titleText, Bounds(GallerySpreadLayout.DetailEventIdBounds));
        DrawHeaderLine(b, LocationAndMusicLine(), Bounds(GallerySpreadLayout.DetailLocationBounds));
        Rectangle sourceBounds = Bounds(GallerySpreadLayout.DetailSourceBounds);
        if (SourceHovered() || Focused(SourceComponentId))
            b.Draw(Game1.staminaRect, sourceBounds, new Color(255, 240, 165) * .7f);
        Rectangle sourceTextBounds = new(sourceBounds.X + 8, sourceBounds.Y, sourceBounds.Width - 40, sourceBounds.Height);
        float sourceScale = Math.Min(1f, (sourceTextBounds.Height - 8f) / Game1.smallFont.LineSpacing);
        GalleryDrawing.DrawEllipsized(b, sourceSummary, sourceTextBounds, sourceScale);
        GalleryDrawing.DrawCentered(b, state.SourceExpanded ? "-" : "+", new Rectangle(sourceBounds.Right - 30, sourceBounds.Y, 28, sourceBounds.Height));
        b.Draw(Game1.staminaRect, new Rectangle(sourceBounds.X + 8, sourceBounds.Bottom - 3, sourceBounds.Width - 16, 1), GallerySpreadDrawing.LightInk);
        Rectangle headingBounds = Bounds(GallerySpreadLayout.ConditionHeadingBounds);
        if (HasForkSelector())
        {
            DrawHeaderText(b, i18n.Get("event-detail.requirements"),
                new Rectangle(headingBounds.X, headingBounds.Y, Math.Max(140, ForkRouteBounds.X - headingBounds.X - 12), headingBounds.Height));
            DrawForkRouteSelector(b);
        }
        else
        {
            DrawHeaderText(b, i18n.Get(state.SourceExpanded ? "source.details-title" : "event-detail.requirements"), headingBounds);
        }
    }

    private string LocationAndMusicLine()
    {
        string location = context.Locations.Get(entry.LocationName);
        return i18n.Get("event-detail.location", new { location })
            + GalleryDrawing.TextSeparator
            + i18n.Get("event-detail.bgm", new { bgm = bgmSummary });
    }

    private static void DrawHeaderLine(SpriteBatch b, string text, Rectangle bounds)
    {
        float scale = Math.Min(1f, (bounds.Height - 6f) / Game1.smallFont.LineSpacing);
        GalleryDrawing.DrawEllipsized(b, text, bounds, scale);
    }

    private void DrawForkRouteSelector(SpriteBatch b)
    {
        Rectangle bounds = ForkRouteBounds;
        bool active = ForkRouteHovered() || Focused(ForkRouteComponentId);
        GalleryUiSkin.DrawClueRow(b, bounds, active);
        if (Focused(ForkRouteComponentId))
            GalleryUiSkin.DrawFocusCorners(b, GalleryDrawing.Inset(bounds, -4, -4), 10);
        string label = "< " + i18n.Get("replay.route", new { route = CurrentForkShortLabel() }) + " >";
        float scale = Math.Min(0.92f, (bounds.Height - 10f) / Game1.smallFont.LineSpacing);
        GalleryDrawing.DrawCentered(b, label, new Rectangle(bounds.X + 16, bounds.Y + 4, bounds.Width - 32, bounds.Height - 8), GallerySpreadDrawing.Ink);
    }

    private void DrawOtherEventInformation(SpriteBatch b)
    {
        ReplayAccess access = context.ReplayAccess(entry);
        bool seen = Game1.player?.eventsSeen.Contains(entry.EventId) == true;
        string statusLine = access.Allowed
            ? i18n.Get(seen ? "pill.seen" : "event.replay")
            : i18n.Get(access.ReasonKey ?? "event.locked");
        string[] lines =
        [
            i18n.Get(entry.Kind == StoryKind.Heart ? "query.kind-heart" : "query.kind-ordinary"),
            i18n.Get("event-detail.event-id", new { id = entry.EventId }),
            i18n.Get("event-detail.location", new { location = GalleryConditionPresentation.Location(entry, i18n) }),
            sourceSummary,
            i18n.Get("event-detail.bgm", new { bgm = bgmSummary }),
            statusLine
        ];
        float rowScale = Math.Min(0.9f, 34f / Math.Max(1f, Game1.smallFont.LineSpacing));
        for (int row = 0; row < lines.Length; row++)
        {
            Rectangle area = GalleryDrawing.Inset(Bounds(GallerySpreadLayout.LeftRowBounds(row)), 34, 4);
            string fitted = GalleryTextFit.Ellipsize(lines[row], area.Width / rowScale, s => Game1.smallFont.MeasureString(s).X);
            Vector2 size = Game1.smallFont.MeasureString(fitted) * rowScale;
            b.DrawString(Game1.smallFont, fitted, new Vector2(area.Center.X - size.X / 2f, area.Center.Y - size.Y / 2f),
                GallerySpreadDrawing.Ink, 0f, Vector2.Zero, rowScale, SpriteEffects.None, 0f);
        }
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
        if (state.SourceExpanded)
            return wrappedSourceParagraphs.Sum(SourceParagraphHeight);
        int lead = LeadBannerHeight();
        if (conditions.Count == 0)
            return lead + LineHeight(i18n.Get("condition.none"), RowTextWidth) + RowPadding * 2;
        return lead + conditions.Sum(MeasureRow);
    }

    private int LeadBannerHeight()
    {
        if (state.SourceExpanded)
            return 0;
        int total = 0;
        int bannerWidth = contentBounds.Width - RowPadding * 2;
        if (isParallelBranch)
            total += LineHeight(i18n.Get("event.parallel-branch-note"), bannerWidth) + RowPadding * 2;
        if (!string.IsNullOrWhiteSpace(storyQuote))
            total += LineHeight(i18n.Get("event-detail.quote", new { quote = storyQuote }), bannerWidth) + RowPadding * 2;
        return total;
    }

    private int SourceTextWidth => contentBounds.Width - RowPadding * 2;

    private void DrawSourceDetails(SpriteBatch b)
    {
        int y = contentBounds.Y - scroll;
        foreach (string text in wrappedSourceParagraphs)
        {
            int height = SourceParagraphHeight(text);
            if (y >= contentBounds.Bottom) break;
            if (y + height > contentBounds.Top)
            {
                if (y > contentBounds.Y - scroll)
                    b.Draw(Game1.staminaRect, new Rectangle(contentBounds.X, y, contentBounds.Width, 1), GallerySpreadDrawing.LightInk);
                b.DrawString(Game1.smallFont, text, new Vector2(contentBounds.X + RowPadding, y + RowPadding), Game1.textColor);
            }
            y += height;
        }
    }

    private static int SourceParagraphHeight(string text)
        => text.Split('\n').Length * Game1.smallFont.LineSpacing + RowPadding * 2;

    // Normal word wrapping can leave a mod ID or full event key wider than the viewport.
    // Split those lines on Unicode text elements so no part of the identifier is clipped.
    private static string WrapSourceText(string text, int width)
    {
        List<string> lines = [];
        foreach (string line in Game1.parseText(text, Game1.smallFont, width).Replace("\r", "").Split('\n'))
        {
            if (line.Length == 0) { lines.Add(""); continue; }
            int[] starts = StringInfo.ParseCombiningCharacters(line);
            int offset = 0;
            while (offset < starts.Length)
            {
                int low = offset + 1, high = starts.Length, fit = low;
                while (low <= high)
                {
                    int middle = low + (high - low) / 2;
                    int end = middle == starts.Length ? line.Length : starts[middle];
                    if (Game1.smallFont.MeasureString(line[starts[offset]..end]).X <= width)
                    { fit = middle; low = middle + 1; }
                    else high = middle - 1;
                }
                lines.Add(line[starts[offset]..(fit == starts.Length ? line.Length : starts[fit])]);
                offset = fit;
            }
        }
        return string.Join("\n", lines);
    }

    // Wrapping measures CJK text character by character, and every row is measured each frame to find the visible
    // ones. Texts and widths can't change while the page is open, so wrap and measure each once.
    private readonly Dictionary<(string Text, int Width), string> wraps = [];
    private readonly Dictionary<ConditionDisplayItem, int> rowHeights = [];
    private readonly Dictionary<ConditionDisplayItem, string> rowTexts = [];

    private string Wrap(string text, int width)
    {
        if (!wraps.TryGetValue((text, width), out string? wrapped))
            wraps[(text, width)] = wrapped = Game1.parseText(text, Game1.smallFont, width);
        return wrapped;
    }

    private int MeasureRow(ConditionDisplayItem item)
    {
        if (!rowHeights.TryGetValue(item, out int height))
            rowHeights[item] = height = MeasureRowBody(item) + conditionReferences[item].Sum(MeasureReference);
        return height;
    }

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
        int bannerWidth = contentBounds.Width - RowPadding * 2;
        if (isParallelBranch)
        {
            string parallelNote = i18n.Get("event.parallel-branch-note");
            int noteHeight = DrawWrapped(b, parallelNote, contentBounds.X + RowPadding, y + RowPadding, bannerWidth, new Color(115, 76, 28));
            y += noteHeight + RowPadding * 2;
            b.Draw(Game1.staminaRect, new Rectangle(contentBounds.X, y - 1, contentBounds.Width, 1), GallerySpreadDrawing.LightInk);
        }
        if (!string.IsNullOrWhiteSpace(storyQuote))
        {
            string formattedQuote = i18n.Get("event-detail.quote", new { quote = storyQuote });
            int quoteHeight = DrawWrapped(b, formattedQuote, contentBounds.X + RowPadding, y + RowPadding, bannerWidth, GallerySpreadDrawing.Ink);
            y += quoteHeight + RowPadding * 2;
            b.Draw(Game1.staminaRect, new Rectangle(contentBounds.X, y - 1, contentBounds.Width, 1), GallerySpreadDrawing.LightInk);
        }
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
            bool focused = Focused(ReferenceComponentId + index);
            ReferenceItem item = referenceLinks[index].Item;
            bool clickable = item.Target is not null || item.PrerequisiteId is not null;
            Rectangle bar = new(bounds.X + 4, bounds.Y + 2, bounds.Width - 8, ReferenceHeight - 4);
            b.Draw(Game1.staminaRect, bar, (hovered || focused) && clickable
                ? new Color(246, 218, 146) * 0.85f
                : new Color(236, 220, 186) * 0.65f);
            GalleryBookChrome.DrawBorder(b, bar, new Color(188, 152, 102) * 0.75f, 1);
            if (focused)
                GalleryUiSkin.DrawFocusCorners(b, GalleryDrawing.Inset(bar, -2, -2), 8);
            float refScale = Math.Min(0.88f, (bar.Height - 6f) / Math.Max(1f, Game1.smallFont.LineSpacing));
            GalleryDrawing.DrawEllipsized(b, item.Label, new Rectangle(bar.X + 10, bar.Y, bar.Width - 20, bar.Height), refScale);
            if (item.Message is not null)
                DrawWrapped(b, item.Message, bounds.X + 8, bounds.Y + ReferenceHeight, bounds.Width - 16, GallerySpreadDrawing.Ink);
        }
    }

    private string RowText(ConditionDisplayItem item)
    {
        if (!rowTexts.TryGetValue(item, out string? text))
            rowTexts[item] = text = ConditionRowPresentation.Text(item, (key, arguments) => i18n.Get(key, arguments));
        return text;
    }

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

    private int DrawWrapped(SpriteBatch b, string text, int x, int y, int width, Color color)
    {
        string wrapped = Wrap(text, width);
        b.DrawString(Game1.smallFont, wrapped, new Vector2(x, y), color);
        return wrapped.Split('\n').Length * Game1.smallFont.LineSpacing;
    }

    private int LineHeight(string text, int width)
        => Wrap(text, width).Split('\n').Length * Game1.smallFont.LineSpacing;

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
        replayBounds = Bounds(GallerySpreadLayout.DetailReplayBounds);
        backBounds = Bounds(GallerySpreadLayout.DetailBackBounds);
        initializeUpperRightCloseButton();
        GalleryBookChrome.PositionCloseButton(upperRightCloseButton, menuScale, drawOffsetX, drawOffsetY, viewportWidth, viewportHeight);
        wrappedSourceParagraphs = sourceParagraphs.Select(text => WrapSourceText(text, SourceTextWidth)).ToArray();
        contentHeight = MeasureContent();
        referenceLinks.Clear();
        int rowY = contentBounds.Y + LeadBannerHeight();
        foreach (ConditionDisplayItem item in state.SourceExpanded ? Array.Empty<ConditionDisplayItem>() : conditions)
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
        currentlySnappedComponent = allClickableComponents.FirstOrDefault(component => component.myID == ReferenceComponentId + index) ?? currentlySnappedComponent;
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
        allClickableComponents = [new ClickableComponent(ToScreen(backBounds), "back") { myID = BackComponentId, leftNeighborID = GalleryBookChrome.RibbonTabBaseId + 2, rightNeighborID = CanReplay() ? ReplayComponentId : -1 }];
        if (CanReplay())
            allClickableComponents.Add(new ClickableComponent(ToScreen(replayBounds), "replay") { myID = ReplayComponentId, leftNeighborID = BackComponentId });
        allClickableComponents.Add(new ClickableComponent(ToScreen(Bounds(GallerySpreadLayout.DetailThumbnailBounds)), "photos") { myID = PhotosComponentId, leftNeighborID = RenameComponentId });
        allClickableComponents.Add(new ClickableComponent(ToScreen(FavoriteBounds), "favorite") { myID = FavoriteComponentId, upNeighborID = SourceComponentId, rightNeighborID = RenameComponentId });
        allClickableComponents.Add(new ClickableComponent(ToScreen(RenameBounds), "rename") { myID = RenameComponentId, upNeighborID = SourceComponentId, leftNeighborID = FavoriteComponentId, rightNeighborID = PhotosComponentId });
        allClickableComponents.Add(new ClickableComponent(ToScreen(Bounds(GallerySpreadLayout.DetailSourceBounds)), "source") { myID = SourceComponentId, downNeighborID = FavoriteComponentId, rightNeighborID = PhotosComponentId });
        GalleryBookChrome.AddRibbonTabComponents(allClickableComponents, ToScreen, BackComponentId, albumPage: true, upNeighborId: FavoriteComponentId);
        if (HasForkSelector())
            allClickableComponents.Add(new ClickableComponent(ToScreen(ForkRouteBounds), "fork-route") { myID = ForkRouteComponentId, upNeighborID = FavoriteComponentId });
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

    private void ToggleSource()
    {
        SaveState(SourceComponentId);
        state.SourceExpanded = !state.SourceExpanded;
        scroll = state.SourceExpanded ? state.SourceScroll : state.ConditionScroll;
        dragging = false;
        RecalculateLayout();
        FocusComponent(SourceComponentId);
        Game1.playSound("smallSelect");
    }

    private void FocusComponent(int id)
    {
        currentlySnappedComponent = allClickableComponents.FirstOrDefault(component => component.myID == id)
            ?? allClickableComponents.FirstOrDefault(component => component.myID == BackComponentId) ?? currentlySnappedComponent;
        SaveState();
        if (Game1.options.snappyMenus && Game1.options.gamepadControls) SnapCurrent();
    }

    private bool SourceHovered() => ToScreen(Bounds(GallerySpreadLayout.DetailSourceBounds)).Contains(Game1.getMouseX(true), Game1.getMouseY(true));
    private bool ForkRouteHovered() => HasForkSelector() && ToScreen(ForkRouteBounds).Contains(Game1.getMouseX(true), Game1.getMouseY(true));
    private bool HasForkSelector() => forkOptions is { Count: > 1 } && !state.SourceExpanded && CanReplay();

    private void StepForkRoute(int delta)
    {
        if (forkOptions is not { Count: > 1 })
            return;
        forkOptionIndex = (forkOptionIndex + delta % forkOptions.Count + forkOptions.Count) % forkOptions.Count;
        SaveState(currentlySnappedComponent?.myID);
        Game1.playSound("smallSelect");
    }

    private ReplayForkOption CurrentForkOption()
        => forkOptions is { Count: > 0 } ? forkOptions[Math.Clamp(forkOptionIndex, 0, forkOptions.Count - 1)] : ReplayForkOption.FollowSave;

    private string CurrentForkShortLabel()
    {
        ReplayForkOption option = CurrentForkOption();
        return option.Mode switch
        {
            ReplayForkMode.MainScript => i18n.Get("replay.route-main"),
            ReplayForkMode.ForceBranch => i18n.Get("replay.route-branch", new { branch = FormatBranchDisplayName(option, includeRequiredIdWhenShared: true) }),
            _ => i18n.Get("replay.route-follow-save")
        };
    }

    private string CurrentForkDetail()
    {
        ReplayForkOption option = CurrentForkOption();
        return option.Mode switch
        {
            ReplayForkMode.MainScript => i18n.Get("replay.route-main"),
            ReplayForkMode.ForceBranch => i18n.Get("replay.route-branch-detail", new
            {
                branch = FormatBranchDisplayName(option, includeRequiredIdWhenShared: false),
                id = option.RequiredId ?? "-"
            }),
            _ => i18n.Get("replay.route-follow-save")
        };
    }

    private string FormatBranchDisplayName(ReplayForkOption option, bool includeRequiredIdWhenShared)
    {
        string baseKey = option.TargetBranchKey ?? "";
        string qualified = option.TargetLocation is { Length: > 0 } loc ? $"{loc}:{baseKey}" : baseKey;
        if (!includeRequiredIdWhenShared || forkOptions is not { Count: > 1 })
            return qualified;
        bool duplicateLabel = forkOptions.Count(candidate =>
            candidate.Mode == ReplayForkMode.ForceBranch
            && candidate.TargetBranchKey == option.TargetBranchKey
            && string.Equals(candidate.TargetLocation, option.TargetLocation, StringComparison.OrdinalIgnoreCase)) > 1;
        return duplicateLabel && option.RequiredId is { Length: > 0 } req ? $"{qualified} ({req})" : qualified;
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
        if (state.SourceExpanded) state.SourceScroll = scroll;
        else state.ConditionScroll = scroll;
        state.ForkOptionIndex = forkOptionIndex;
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
        context.Navigation.Replay(entry.Resolved.Identity, HasForkSelector() ? CurrentForkOption() : null);
    }

    private void FollowReference(GalleryEvent target)
    {
        SaveState();
        context.Navigation.OpenEvent(target.Resolved.Identity);
    }
    private void FollowItem(ReferenceItem item)
    {
        if (item.Target is { } target) FollowReference(target);
        else if (item.PrerequisiteId is { } id) { SaveState(); context.Navigation.OpenPrerequisite(id); }
    }
    private void Rename() { SaveState(RenameComponentId); context.Navigation.Rename(entry.Resolved.Identity); }

    private void ToggleFavorite()
    {
        if (entry is null || context?.Favorites is not { } store)
            return;
        if (store.IsReadOnly || !store.Toggle(entry.Resolved.Identity))
        {
            Game1.addHUDMessage(new HUDMessage(i18n.Get("favorite.readonly"), HUDMessage.error_type));
            return;
        }
        Game1.playSound("smallSelect");
    }

    private static Rectangle Bounds((int X, int Y, int Width, int Height) bounds) => new(bounds.X, bounds.Y, bounds.Width, bounds.Height);
    private Rectangle ToScreen(Rectangle bounds) => GalleryDrawing.ScaleRectangle(bounds, menuScale, drawOffsetX, drawOffsetY);
    private bool Focused(int id) => (Game1.options.snappyMenus && Game1.options.gamepadControls || keyboardFocus) && currentlySnappedComponent?.myID == id;
    private (int X, int Y) ToLogical(int x, int y) => ((int)Math.Round((x - drawOffsetX) / menuScale), (int)Math.Round((y - drawOffsetY) / menuScale));
}
