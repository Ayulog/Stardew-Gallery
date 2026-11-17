using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;

namespace StardewGallery;

internal sealed class GalleryCharacterMenu : IClickableMenu
{
    private const int BackComponentId = 1000;
    private const int CardComponentBase = 2000;
    private const int PageSize = GallerySpreadLayout.EventColumns * GallerySpreadLayout.EventVisibleRows;

    private sealed record AlbumCardItem(EventIdentity Identity, GalleryEvent? Story, WeddingDateRecord? WeddingDate)
    {
        internal bool IsWedding => Story is null;
    }

    private readonly GalleryViewContext context;
    private readonly GalleryPageState state;
    private readonly GalleryCharacter character;
    private readonly ITranslationHelper i18n;
    private readonly Texture2D background;
    private readonly Texture2D thumbnail;
    private readonly GalleryPhotos photos;
    private readonly IReadOnlyList<GalleryEvent> events;
    private readonly IReadOnlyList<AlbumCardItem> items;
    private readonly string[] cardTitles;
    private readonly string[] cardLabels;
    private readonly float cardLabelScale;
    private readonly GalleryCharacterPanel leftPanel;
    private readonly int preferredComponentId;
    private int currentPage;
    private bool keyboardFocus;
    private Point lastMouse;
    private Rectangle backBounds;
    private int viewportWidth;
    private int viewportHeight;
    private float menuScale = 1f;
    private int drawOffsetX;
    private int drawOffsetY;
    private bool pendingInitialSnap = true;
    private EventCardFocus? lastCardFocus;

    internal GalleryCharacterMenu(GalleryViewContext context, GalleryPageState state)
        : base(0, 0, GalleryDrawing.MenuWidth, GalleryDrawing.MenuHeight, true)
    {
        this.context = context;
        this.state = state;
        character = context.Catalog.Characters.First(c => c.Name == state.CharacterName);
        i18n = context.I18n;
        background = context.Textures.Album;
        thumbnail = context.Textures.Thumbnail;
        photos = context.Photos;
        events = context.Catalog.HeartEventsFor(character.Name);

        List<AlbumCardItem> builtItems = [];
        EventIdentity weddingId = WeddingMemento.IdentityFor(character.Name);
        EventPhotoSet weddingPhotos = photos.Read(weddingId);
        if (weddingPhotos.Photos.Count > 0 || weddingPhotos.WeddingDate is not null)
            builtItems.Add(new AlbumCardItem(weddingId, null, weddingPhotos.WeddingDate));
        foreach (GalleryEvent ev in events)
            builtItems.Add(new AlbumCardItem(ev.Resolved.Identity, ev, null));
        items = builtItems;

        Rectangle plaqueText = new(0, 0, GallerySpreadLayout.EventCardBounds(0).Width - 24 - GalleryUiSkin.TitlePlaqueCap * 2, 24);
        cardTitles = items.Select(FormatItemTitle).ToArray();
        float titleHeight = cardTitles.Select(title => Game1.smallFont.MeasureString(title).Y)
            .Append(Game1.smallFont.LineSpacing).Max();
        cardLabelScale = Math.Min(0.86f, plaqueText.Height / Math.Max(1f, titleHeight));
        cardLabels = cardTitles.Select(title => GalleryTextFit.Ellipsize(title, plaqueText.Width / Math.Max(0.4f, cardLabelScale),
            text => Game1.smallFont.MeasureString(text).X)).ToArray();

        // Left panel uses only heart events so Wedding Memento never alters story-node denominators.
        leftPanel = new GalleryCharacterPanel(character, events, i18n, context.Textures.Scene, context.Appearance, context.Catalog);
        leftPanel.Prepare();

        int focusIndex = EventCardFocus.TryFromComponentId(state.Focus, CardComponentBase, items.Count, out EventCardFocus restored)
            ? restored.EventIndex : -1;
        currentPage = focusIndex >= 0
            ? GalleryPagination.PageForIndex(focusIndex, items.Count, PageSize)
            : GalleryPagination.ClampPage(state.Scroll, items.Count, PageSize);

        preferredComponentId = state.Focus is BackComponentId or GalleryBookChrome.PrevPageComponentId or GalleryBookChrome.NextPageComponentId
            || state.Focus >= GalleryBookChrome.RibbonTabBaseId && state.Focus < GalleryBookChrome.RibbonTabBaseId + GallerySpreadLayout.RibbonTabCount
            ? state.Focus
            : items.Count > 0
                ? new EventCardFocus(Math.Max(0, focusIndex), EventCardAction.Details).GetComponentId(CardComponentBase)
                : BackComponentId;

        RecalculateLayout();
        currentlySnappedComponent = allClickableComponents.FirstOrDefault(component => component.myID == preferredComponentId)
            ?? currentlySnappedComponent;
        SaveState();
        SnapForGamepad();
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

    public override void update(GameTime time)
    {
        base.update(time);
        leftPanel.Prepare();
        if (pendingInitialSnap && Game1.options.snappyMenus && Game1.options.gamepadControls)
        {
            pendingInitialSnap = false;
            snapToDefaultClickableComponent();
        }
        SaveState();
    }

    public override void receiveScrollWheelAction(int direction) => TurnPage(direction < 0 ? 1 : -1);

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        (x, y) = ToLogical(x, y);
        if (upperRightCloseButton?.bounds.Contains(x, y) == true)
        {
            SaveState();
            context.Navigation.Close();
            return;
        }
        if (GalleryBookChrome.TryClickRibbonTab(x, y, GalleryBookTab.Villagers, context.Navigation, () => SaveState(), albumPage: true))
            return;
        if (backBounds.Contains(x, y))
        {
            Return();
            return;
        }
        if (Bounds(GallerySpreadLayout.RightPagePrevBounds).Contains(x, y))
        {
            TurnPage(-1);
            return;
        }
        if (Bounds(GallerySpreadLayout.RightPageNextBounds).Contains(x, y))
        {
            TurnPage(1);
            return;
        }

        int first = GalleryPagination.FirstIndex(currentPage, items.Count, PageSize);
        int visible = GalleryPagination.VisibleCount(currentPage, items.Count, PageSize);
        for (int slot = 0; slot < visible; slot++)
        {
            // Any click on a card opens the story details (the wedding keepsake opens its photos).
            if (CardHitBounds(slot).Contains(x, y))
            {
                OpenItem(first + slot, EventCardAction.Details);
                return;
            }
        }
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
            keyboardFocus = true;
            TurnPage(key == Keys.PageUp ? -1 : 1);
            return;
        }
        if (key == Keys.Enter)
        {
            keyboardFocus = true;
            ActivateComponent(currentlySnappedComponent?.myID ?? BackComponentId);
            return;
        }
        if (key is Keys.Up or Keys.Down or Keys.Left or Keys.Right)
        {
            keyboardFocus = true;
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
        // A/X use Game1's native controller-to-mouse dispatch while areGamePadControlsImplemented is false;
        // handling Buttons.A here as well would activate page arrows or cards twice in one frame.
        if (button is Buttons.LeftShoulder or Buttons.RightShoulder)
        {
            TurnPage(button == Buttons.LeftShoulder ? -1 : 1);
            return;
        }
        base.receiveGamePadButton(button);
    }

    private bool ActivateComponent(int id)
    {
        if (id == BackComponentId)
        {
            Return();
            return true;
        }
        if (id == GalleryBookChrome.PrevPageComponentId)
        {
            TurnPage(-1);
            return true;
        }
        if (id == GalleryBookChrome.NextPageComponentId)
        {
            TurnPage(1);
            return true;
        }
        if (id >= GalleryBookChrome.RibbonTabBaseId && id < GalleryBookChrome.RibbonTabBaseId + GallerySpreadLayout.RibbonTabCount)
        {
            SaveState();
            Game1.playSound("shwip");
            context.Navigation.SwitchTab((GalleryBookTab)(id - GalleryBookChrome.RibbonTabBaseId));
            return true;
        }
        if (EventCardFocus.TryFromComponentId(id, CardComponentBase, items.Count, out EventCardFocus focus))
        {
            OpenItem(focus.EventIndex, focus.Action);
            return true;
        }
        return false;
    }

    public override void applyMovementKey(int direction)
    {
        if (direction < 0 || direction > 3)
            return;
        int id = currentlySnappedComponent?.myID ?? BackComponentId;

        if (id >= GalleryBookChrome.RibbonTabBaseId && id < GalleryBookChrome.RibbonTabBaseId + GallerySpreadLayout.RibbonTabCount)
        {
            int tab = id - GalleryBookChrome.RibbonTabBaseId;
            if (direction == 3 && tab > 0) FocusId(GalleryBookChrome.RibbonTabBaseId + tab - 1);
            else if (direction == 1 && tab + 1 < GallerySpreadLayout.RibbonTabCount) FocusId(GalleryBookChrome.RibbonTabBaseId + tab + 1);
            else if (direction == 1) FocusId(BackComponentId);
            else if (direction == 0)
            {
                if (items.Count > 0)
                    FocusPageSlot(0, EventCardAction.Details);
                else
                    FocusId(BackComponentId);
            }
            return;
        }

        if (id is BackComponentId or GalleryBookChrome.PrevPageComponentId or GalleryBookChrome.NextPageComponentId)
        {
            int[] footer = [BackComponentId, GalleryBookChrome.PrevPageComponentId, GalleryBookChrome.NextPageComponentId];
            int idx = Array.IndexOf(footer, id);
            if (direction == 3)
            {
                if (idx > 0) FocusId(footer[idx - 1]);
                else FocusId(GalleryBookChrome.RibbonTabBaseId + 2);
            }
            else if (direction == 1 && idx + 1 < footer.Length)
                FocusId(footer[idx + 1]);
            else if (direction == 0 && items.Count > 0)
            {
                int first = GalleryPagination.FirstIndex(currentPage, items.Count, PageSize);
                int visible = GalleryPagination.VisibleCount(currentPage, items.Count, PageSize);
                FocusCard(lastCardFocus is { } remembered && remembered.EventIndex >= first && remembered.EventIndex < first + visible
                    ? remembered
                    : new EventCardFocus(first + visible - 1, EventCardAction.Details));
            }
            return;
        }

        if (!EventCardFocus.TryFromComponentId(id, CardComponentBase, items.Count, out EventCardFocus focus))
            return;

        int pageStart = GalleryPagination.FirstIndex(currentPage, items.Count, PageSize);
        int pageVisible = GalleryPagination.VisibleCount(currentPage, items.Count, PageSize);
        int localIndex = focus.EventIndex - pageStart;
        if (localIndex < 0 || localIndex >= pageVisible)
        {
            FocusPageSlot(0, EventCardAction.Details);
            return;
        }

        int columns = GalleryUiRules.EventColumns;
        int col = localIndex % columns;
        lastCardFocus = focus;
        switch (direction)
        {
            case 3:
                if (col > 0) FocusPageSlot(localIndex - 1, EventCardAction.Details);
                else FocusId(GalleryBookChrome.RibbonTabBaseId + Math.Min(localIndex / columns, 2));
                break;
            case 1:
                if (col + 1 < columns && localIndex + 1 < pageVisible) FocusPageSlot(localIndex + 1, EventCardAction.Details);
                break;
            case 0:
                if (localIndex >= columns) FocusPageSlot(localIndex - columns, EventCardAction.Details);
                break;
            case 2:
                if (localIndex + columns < pageVisible) FocusPageSlot(localIndex + columns, EventCardAction.Details);
                else FocusId(col == 0 ? BackComponentId : GalleryBookChrome.PrevPageComponentId);
                break;
        }
    }

    private void FocusPageSlot(int slot, EventCardAction action)
    {
        int first = GalleryPagination.FirstIndex(currentPage, items.Count, PageSize);
        int visible = GalleryPagination.VisibleCount(currentPage, items.Count, PageSize);
        if (visible <= 0)
        {
            FocusId(BackComponentId);
            return;
        }
        int clampedSlot = Math.Clamp(slot, 0, visible - 1);
        FocusCard(new EventCardFocus(first + clampedSlot, action));
    }

    private void FocusCard(EventCardFocus focus)
    {
        if (items.Count == 0)
            return;
        int index = Math.Clamp(focus.EventIndex, 0, items.Count - 1);
        const EventCardAction action = EventCardAction.Details;
        EventCardFocus target = new(index, action);
        lastCardFocus = target;
        currentPage = GalleryPagination.PageForIndex(index, items.Count, PageSize);
        BuildClickableComponents();
        currentlySnappedComponent = allClickableComponents.FirstOrDefault(component => component.myID == target.GetComponentId(CardComponentBase))
            ?? currentlySnappedComponent;
        SnapCurrent();
    }

    private void FocusId(int componentId)
    {
        currentlySnappedComponent = allClickableComponents.FirstOrDefault(component => component.myID == componentId)
            ?? currentlySnappedComponent;
        SnapCurrent();
    }

    private void TurnPage(int delta)
    {
        var (nextPage, changed) = GalleryPagination.Turn(currentPage, delta, items.Count, PageSize);
        if (!changed)
            return;
        currentPage = nextPage;
        int first = GalleryPagination.FirstIndex(currentPage, items.Count, PageSize);
        int visible = GalleryPagination.VisibleCount(currentPage, items.Count, PageSize);
        if (lastCardFocus is { } prev && visible > 0)
        {
            int preservedSlot = Math.Clamp(prev.EventIndex % PageSize, 0, visible - 1);
            int nextIndex = first + preservedSlot;
            lastCardFocus = new EventCardFocus(nextIndex, EventCardAction.Details);
            if (EventCardFocus.TryFromComponentId(currentlySnappedComponent?.myID ?? -1, CardComponentBase, items.Count, out _))
                currentlySnappedComponent = null;
        }
        BuildClickableComponents();
        Game1.playSound("shwip");
    }

    // Game1 repopulates components by reflection when input switches to a controller; rebuild the gallery's own instead.
    public override void populateClickableComponentList() => BuildClickableComponents();

    public override void snapToDefaultClickableComponent()
    {
        currentlySnappedComponent = allClickableComponents?.FirstOrDefault(component => component.myID == preferredComponentId)
            ?? allClickableComponents?.FirstOrDefault(component => component.myID >= CardComponentBase && component.myID < GalleryBookChrome.RibbonTabBaseId)
            ?? allClickableComponents?.FirstOrDefault(component => component.myID == BackComponentId);
        SnapCurrent();
    }

    public override void draw(SpriteBatch b)
    {
        EnsureLayout();
        b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height), Color.Black * .45f);
        (int mouseX, int mouseY) = ToLogical(Game1.getMouseX(true), Game1.getMouseY(true));
        bool showFocus = Game1.options.snappyMenus && Game1.options.gamepadControls || keyboardFocus;
        int focusedId = currentlySnappedComponent?.myID ?? -1;

        GalleryDrawing.BeginScaled(b, menuScale, drawOffsetX, drawOffsetY);
        leftPanel.DrawPhoto(b);
        b.Draw(background, new Rectangle(0, 0, width, height), Color.White);
        GalleryBookChrome.DrawRibbonTabs(b, GalleryBookTab.Villagers, focusedId, showFocus, new Point(mouseX, mouseY), i18n, out string? hoverTooltip, albumPage: true);
        GalleryBookChrome.DrawPageTitle(b, i18n.Get("album.title", new { name = character.DisplayName }), menuScale, drawOffsetX, drawOffsetY);
        leftPanel.DrawInformation(b);

        int first = GalleryPagination.FirstIndex(currentPage, items.Count, PageSize);
        int visible = GalleryPagination.VisibleCount(currentPage, items.Count, PageSize);
        for (int slot = 0; slot < visible; slot++)
        {
            int index = first + slot;
            bool cardHovered = CardHitBounds(slot).Contains(mouseX, mouseY);
            bool cardFocused = showFocus
                && EventCardFocus.TryFromComponentId(focusedId, CardComponentBase, items.Count, out EventCardFocus f)
                && f.EventIndex == index;
            DrawItem(b, items[index], slot, index, cardHovered || cardFocused);
            if (cardHovered || cardFocused)
            {
                string? cardTip = BuildCardTooltip(items[index], index);
                if (cardTip is not null)
                    hoverTooltip = cardTip;
            }
        }

        bool backHot = backBounds.Contains(mouseX, mouseY) || showFocus && focusedId == BackComponentId;
        GalleryDrawing.DrawButton(b, backBounds, i18n.Get("nav.back"), backHot);

        GalleryBookChrome.DrawPager(
            b,
            GallerySpreadLayout.RightPagePrevBounds,
            GallerySpreadLayout.RightPageIndicatorBounds,
            GallerySpreadLayout.RightPageNextBounds,
            currentPage,
            items.Count,
            PageSize,
            focusedId,
            showFocus,
            new Point(mouseX, mouseY));

        upperRightCloseButton?.draw(b);
        GalleryDrawing.EndScaled(b);

        if (hoverTooltip is not null)
        {
            drawHoverText(b,
                Game1.parseText(hoverTooltip, Game1.smallFont, Math.Min(620, Math.Max(200, Game1.uiViewport.Width - 80))),
                Game1.smallFont);
        }
        drawMouse(b);
    }

    private string FormatItemTitle(AlbumCardItem item)
    {
        if (item.IsWedding)
        {
            if (item.WeddingDate is { } date)
            {
                string seasonKey = "season." + (WeddingDateRecord.NormalizeSeason(date.Season) ?? "spring");
                return i18n.Get("wedding.album-card-dated", new { year = date.Year, season = i18n.Get(seasonKey), day = date.DayOfMonth });
            }
            return i18n.Get("wedding.album-card");
        }
        return context.CardTitle(item.Story!, character.Name);
    }

    private string? BuildCardTooltip(AlbumCardItem item, int index)
    {
        List<string> lines = [cardTitles[index]];
        if (item.IsWedding)
        {
            lines.Add(i18n.Get("wedding.card-hint"));
            return string.Join("\n", lines);
        }

        GalleryEvent entry = item.Story!;
        EncounterClue clue = context.EventClue(entry);
        if (clue == EncounterClue.AvailableToday)
            lines.Add(i18n.Get("clue.available-today") + ": " + i18n.Get("clue.available-today-hint"));
        else if (clue == EncounterClue.ReadyForTiming)
            lines.Add(i18n.Get("clue.ready-for-timing") + ": " + i18n.Get("clue.ready-for-timing-hint"));

        // Anti-spoiler invariant: StoryQuote strictly returns null unless seen, Unlock All, or opened parallel If-route.
        if (context.StoryQuote(entry) is { Length: > 0 } quote)
            lines.Add(i18n.Get("event-detail.quote", new { quote }));

        return lines.Count == 1 && cardLabels[index] == cardTitles[index] ? null : string.Join("\n", lines);
    }

    /// <summary>Card = diamond title plaque above a framed 16:9 photo (or sealed envelope), status pill and favourite ribbon.</summary>
    private void DrawItem(SpriteBatch b, AlbumCardItem item, int slot, int index, bool hot)
    {
        Rectangle card = Bounds(GallerySpreadLayout.EventCardBounds(slot));
        Rectangle photo = Bounds(GallerySpreadLayout.EventCardThumbnailBounds(slot));
        Texture2D? cover = photos.Cover(item.Identity);
        GalleryEvent? entry = item.Story;
        bool unlocked = entry is null || context.IsStoryUnlocked(entry);
        bool parallel = entry is not null && context.IsParallelBranch(entry);
        bool seen = entry is not null && Game1.player?.eventsSeen.Contains(entry.EventId) == true;

        if (cover is not null)
            GalleryPhotos.DrawCover(b, cover, photo, unlocked ? Color.White : new Color(215, 210, 198));
        else if (!seen && entry is not null)
            GalleryBookChrome.DrawLockedStoryEnvelope(b, photo, context.CardPrimaryLead(entry, character.Name), context.Locations.Get(entry.LocationName), parallel && unlocked);
        else
            GalleryPhotos.DrawCover(b, thumbnail, photo, Color.White);

        GalleryUiSkin.DrawCardFrame(b, photo, hot);
        GalleryUiSkin.DrawTitlePlaque(b, new Rectangle(card.X + 12, card.Y + 4, card.Width - 24, 30), cardLabels[index], hot);

        // Status pill on the photo's top-left corner.
        Point pillAt = new(photo.X + 8, photo.Y + 8);
        if (item.IsWedding)
            GalleryUiSkin.DrawPill(b, pillAt, 26, i18n.Get("pill.wedding"), UiPill.Amber, UiBadge.Rings, maxWidth: 180);
        else if (entry is not null)
        {
            EncounterClue clue = context.EventClue(entry);
            if (clue == EncounterClue.AvailableToday)
                GalleryUiSkin.DrawPill(b, pillAt, 26, i18n.Get("clue.available-today"), UiPill.Amber, maxWidth: 180);
            else if (clue == EncounterClue.ReadyForTiming)
                GalleryUiSkin.DrawPill(b, pillAt, 26, i18n.Get("clue.ready-for-timing"), UiPill.Wood, UiBadge.ClueReady, maxWidth: 180);
            else if (parallel)
                GalleryUiSkin.DrawPill(b, pillAt, 26, i18n.Get("pill.parallel"), UiPill.Amber, UiBadge.Branch, maxWidth: 180);
            else if (!unlocked)
                GalleryUiSkin.DrawPill(b, pillAt, 26, i18n.Get("pill.locked"), UiPill.Wood, maxWidth: 180);
            else if (!seen)
                GalleryUiSkin.DrawPill(b, pillAt, 26, i18n.Get("pill.unseen"), UiPill.Wood, maxWidth: 180);
        }

        if (!item.IsWedding && context.IsFavorite(item.Identity))
            GalleryUiSkin.DrawFavoriteRibbon(b, photo);
    }

    /// <summary>Clickable area of a whole album card (title plaque + framed photo).</summary>
    private static Rectangle CardHitBounds(int slot)
        => Bounds(GallerySpreadLayout.EventCardBounds(slot));

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
        initializeUpperRightCloseButton();
        GalleryBookChrome.PositionCloseButton(upperRightCloseButton, menuScale, drawOffsetX, drawOffsetY, viewportWidth, viewportHeight);
        backBounds = Bounds(GallerySpreadLayout.AlbumBackBounds);
        BuildClickableComponents();
    }

    private void EnsureLayout()
    {
        if (GalleryLayout.Changed(viewportWidth, viewportHeight, Game1.uiViewport.Width, Game1.uiViewport.Height))
            RecalculateLayout();
    }

    private void BuildClickableComponents()
    {
        int previousId = currentlySnappedComponent?.myID ?? -1;
        currentPage = GalleryPagination.ClampPage(currentPage, items.Count, PageSize);
        int first = GalleryPagination.FirstIndex(currentPage, items.Count, PageSize);
        int visible = GalleryPagination.VisibleCount(currentPage, items.Count, PageSize);

        allClickableComponents = [new ClickableComponent(ToScreen(backBounds), "back") { myID = BackComponentId }];
        GalleryBookChrome.AddRibbonTabComponents(allClickableComponents, ToScreen, BackComponentId, albumPage: true,
            upNeighborId: visible > 0 ? new EventCardFocus(first, EventCardAction.Details).GetComponentId(CardComponentBase) : BackComponentId);

        allClickableComponents.Add(new ClickableComponent(ToScreen(Bounds(GallerySpreadLayout.RightPagePrevBounds)), "prev-page")
        {
            myID = GalleryBookChrome.PrevPageComponentId
        });
        allClickableComponents.Add(new ClickableComponent(ToScreen(Bounds(GallerySpreadLayout.RightPageNextBounds)), "next-page")
        {
            myID = GalleryBookChrome.NextPageComponentId
        });

        for (int slot = 0; slot < visible; slot++)
        {
            int index = first + slot;
            allClickableComponents.Add(new ClickableComponent(ToScreen(CardHitBounds(slot)), $"card-{index}")
            {
                myID = new EventCardFocus(index, EventCardAction.Details).GetComponentId(CardComponentBase)
            });
        }

        currentlySnappedComponent = allClickableComponents.FirstOrDefault(component => component.myID == previousId)
            ?? VisibleFocusFallback(visible)
            ?? allClickableComponents[0];
        if (EventCardFocus.TryFromComponentId(currentlySnappedComponent.myID, CardComponentBase, items.Count, out EventCardFocus current))
            lastCardFocus = current;
        SnapCurrent();
    }

    private ClickableComponent? VisibleFocusFallback(int visible)
    {
        if (visible <= 0)
            return null;
        int first = GalleryPagination.FirstIndex(currentPage, items.Count, PageSize);
        EventCardFocus target = lastCardFocus is { } remembered && remembered.EventIndex >= first && remembered.EventIndex < first + visible
            ? remembered
            : new EventCardFocus(first, EventCardAction.Details);
        return allClickableComponents.FirstOrDefault(component => component.myID == target.GetComponentId(CardComponentBase));
    }

    private bool IsReplayAvailable(AlbumCardItem item)
        => !item.IsWedding && item.Story is { } entry && context.ReplayAccess(entry).Allowed;

    private void SaveState(int? focus = null)
    {
        state.Scroll = currentPage;
        state.Focus = focus ?? currentlySnappedComponent?.myID ?? -1;
    }

    private void SnapCurrent()
    {
        SaveState();
        if (Game1.options.snappyMenus && Game1.options.gamepadControls)
            snapCursorToCurrentSnappedComponent();
    }

    private void OpenItem(int index, EventCardAction action)
    {
        AlbumCardItem item = items[index];
        SaveState(new EventCardFocus(index, action).GetComponentId(CardComponentBase));
        if (item.IsWedding)
        {
            context.Navigation.OpenPhotos(item.Identity);
            return;
        }
        GalleryEvent entry = item.Story!;
        if (action == EventCardAction.Details)
            context.Navigation.OpenEvent(entry.Resolved.Identity);
        else if (IsReplayAvailable(item))
            context.Navigation.Replay(entry.Resolved.Identity);
    }

    private static Rectangle Bounds((int X, int Y, int Width, int Height) bounds)
        => new(bounds.X, bounds.Y, bounds.Width, bounds.Height);

    private void Return()
    {
        Game1.playSound("bigDeSelect");
        SaveState();
        context.Navigation.Back();
    }

    private void SnapForGamepad()
    {
        if (Game1.options.snappyMenus && Game1.options.gamepadControls)
            snapToDefaultClickableComponent();
    }

    private Rectangle ToScreen(Rectangle bounds) => GalleryDrawing.ScaleRectangle(bounds, menuScale, drawOffsetX, drawOffsetY);
    private (int X, int Y) ToLogical(int x, int y) => ((int)Math.Round((x - drawOffsetX) / menuScale), (int)Math.Round((y - drawOffsetY) / menuScale));
}
