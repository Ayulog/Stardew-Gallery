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
    private readonly GalleryViewContext context;
    private readonly GalleryPageState state;
    private readonly GalleryCharacter character;
    private readonly ITranslationHelper i18n;
    private readonly Texture2D background;
    private readonly Texture2D thumbnail;
    private readonly GalleryPhotos photos;
    private readonly Texture2D replayGlyph;
    private readonly Texture2D slotFrame;
    private readonly Texture2D scrollbarTrackTexture;
    private readonly IReadOnlyList<GalleryEvent> events;
    private readonly GalleryCharacterPanel leftPanel;
    private readonly int preferredComponentId;
    private int scrollRow;
    private bool dragging;
    private int dragOffset;
    private Rectangle scrollTrack;
    private Rectangle scrollThumb;
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
        character = context.Catalog.Characters.First(character => character.Name == state.CharacterName);
        i18n = context.I18n;
        background = context.Textures.Album;
        thumbnail = context.Textures.Thumbnail;
        photos = context.Photos;
        replayGlyph = context.Textures.Replay;
        slotFrame = context.Textures.SlotFrame;
        scrollbarTrackTexture = context.Textures.Scrollbar;
        events = context.Catalog.HeartEventsFor(character.Name);
        leftPanel = new GalleryCharacterPanel(character, events, i18n, context.Textures.Scene);
        int focusIndex = EventCardFocus.TryFromComponentId(state.Focus, CardComponentBase, events.Count, out EventCardFocus restored)
            ? restored.EventIndex : -1;
        (scrollRow, _) = GalleryUiRules.ResolveReturnPosition(
            focusIndex, state.Scroll, GalleryUiRules.EventColumns, GalleryUiRules.EventVisibleRows, events.Count);
        bool replayAvailable = focusIndex >= 0 && IsReplayAvailable(events[focusIndex]);
        preferredComponentId = state.Focus == BackComponentId ? BackComponentId : new EventCardFocus(Math.Max(0, focusIndex), replayAvailable && restored.Action == EventCardAction.Replay
            ? EventCardAction.Replay : EventCardAction.Details)
            .GetComponentId(CardComponentBase);
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

    public override void update(GameTime time)
    {
        base.update(time);
        if (pendingInitialSnap && Game1.options.snappyMenus && Game1.options.gamepadControls)
        {
            pendingInitialSnap = false;
            snapToDefaultClickableComponent();
        }
        SaveState();
    }

    public override void receiveScrollWheelAction(int direction)
    {
        ScrollBy(direction < 0 ? 1 : -1);
        Game1.playSound("shiny4");
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        (x, y) = ToLogical(x, y);
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
        if (scrollThumb.Contains(x, y) && MaxScroll > 0)
        {
            dragging = true;
            dragOffset = y - scrollThumb.Y;
            return;
        }
        if (scrollTrack.Contains(x, y) && MaxScroll > 0)
        {
            ScrollBy(y < scrollThumb.Y ? -GalleryUiRules.EventVisibleRows : GalleryUiRules.EventVisibleRows);
            return;
        }

        int first = scrollRow * GalleryUiRules.EventColumns;
        int visible = GalleryUiRules.VisibleEventCount(events.Count, first);
        for (int slot = 0; slot < visible; slot++)
        {
            GalleryEvent entry = events[first + slot];
            if (DetailsBounds(slot).Contains(x, y))
            {
                OpenEvent(first + slot, EventCardAction.Details);
                return;
            }
            if (IsReplayAvailable(entry) && Bounds(GallerySpreadLayout.EventCardThumbnailBounds(slot)).Contains(x, y))
            {
                OpenEvent(first + slot, EventCardAction.Replay);
                return;
            }
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
        if (Game1.options.snappyMenus && Game1.options.gamepadControls)
            SnapCurrent();
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
            int id = currentlySnappedComponent?.myID ?? -1;
            if (id == BackComponentId)
            {
                Return();
                return;
            }
            if (EventCardFocus.TryFromComponentId(id, CardComponentBase, events.Count, out EventCardFocus focus))
            {
                OpenEvent(focus.EventIndex, focus.Action);
                return;
            }
        }
        base.receiveGamePadButton(button);
    }

    // Native Snappy movement has one owner. Details/Replay transitions and scrolling
    // are resolved together, so a lower-row Details press cannot be stolen by scroll.
    public override void applyMovementKey(int direction)
    {
        if (direction < 0 || direction > 3)
            return;
        int id = currentlySnappedComponent?.myID ?? BackComponentId;
        if (!EventCardFocus.TryFromComponentId(id, CardComponentBase, events.Count, out EventCardFocus focus))
        {
            if (events.Count > 0 && direction is 0 or 1)
                FocusCard(lastCardFocus ?? new EventCardFocus(scrollRow * 2, EventCardAction.Details));
            return;
        }
        if (direction == 3 && focus.EventIndex % GalleryUiRules.EventColumns == 0
            || direction == 2 && focus.EventIndex + 2 >= events.Count
                && (focus.Action == EventCardAction.Replay || !IsReplayAvailable(events[focus.EventIndex])))
        {
            lastCardFocus = focus;
            currentlySnappedComponent = allClickableComponents.First(component => component.myID == BackComponentId);
            SnapCurrent();
            return;
        }
        var next = focus.Navigate((EventCardDirection)direction, events.Count, index => IsReplayAvailable(events[index]), scrollRow);
        if (next.Focus is EventCardFocus target)
            FocusCard(target);
    }

    private void FocusCard(EventCardFocus focus)
    {
        var normalized = focus.Normalize(events.Count, index => IsReplayAvailable(events[index]), scrollRow);
        if (normalized.Focus is not EventCardFocus target)
            return;
        lastCardFocus = target;
        scrollRow = normalized.ScrollRow;
        UpdateScrollbar();
        BuildClickableComponents();
        currentlySnappedComponent = allClickableComponents.FirstOrDefault(component => component.myID == target.GetComponentId(CardComponentBase));
        SnapCurrent();
    }

    public override void snapToDefaultClickableComponent()
    {
        currentlySnappedComponent = allClickableComponents?.FirstOrDefault(component => component.myID == preferredComponentId)
            ?? allClickableComponents?.FirstOrDefault(component => component.myID >= CardComponentBase)
            ?? allClickableComponents?.FirstOrDefault(component => component.myID == BackComponentId);
        SnapCurrent();
    }

    public override void draw(SpriteBatch b)
    {
        EnsureLayout();
        b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height), Color.Black * .45f);
        GalleryDrawing.BeginScaled(b, menuScale, drawOffsetX, drawOffsetY);
        leftPanel.DrawPhoto(b);
        b.Draw(background, new Rectangle(0, 0, width, height), Color.White);
        GalleryDrawing.DrawScrollbarTrack(b, scrollbarTrackTexture, scrollTrack);
        DrawAlbumSlots(b);
        DrawPageTitle(b, i18n.Get("detail.title"), Bounds(GallerySpreadLayout.TitleBounds));
        leftPanel.DrawInformation(b);

        int first = scrollRow * GalleryUiRules.EventColumns;
        int visible = GalleryUiRules.VisibleEventCount(events.Count, first);
        for (int slot = 0; slot < visible; slot++)
            DrawEvent(b, events[first + slot], slot, first + slot);
        if (MaxScroll > 0)
            GalleryDrawing.DrawScrollbar(b, scrollThumb);
        GalleryDrawing.DrawButton(b, backBounds, i18n.Get("detail.back"));
        upperRightCloseButton?.draw(b);
        GalleryDrawing.EndScaled(b);
        drawMouse(b);
    }

    private void DrawAlbumSlots(SpriteBatch b)
    {
        int first = scrollRow * GalleryUiRules.EventColumns;
        int visible = GalleryUiRules.VisibleEventCount(events.Count, first);
        for (int slot = 0; slot < visible; slot++)
        {
            GalleryPhotos.DrawCover(b, photos.Cover(events[first + slot].Resolved.Identity) ?? thumbnail,
                Bounds(GallerySpreadLayout.EventCardThumbnailBounds(slot)),
                IsReplayAvailable(events[first + slot]) ? Color.White : new Color(185, 180, 167));
            b.Draw(slotFrame, Bounds(GallerySpreadLayout.EventCardBounds(slot)), Color.White);
        }
    }

    private void DrawEvent(SpriteBatch b, GalleryEvent entry, int slot, int index)
    {
        EventOwner owner = entry.Ownership.Owners.First(value => value.Name == character.Name);
        string hearts = owner.FriendshipPoints is int points
            ? i18n.Get("event.hearts", new { hearts = (int)Math.Ceiling(points / 250d) })
            : i18n.Get("event.unspecified");
        GalleryDrawing.DrawLeftFitted(b, hearts + GalleryDrawing.TextSeparator + $"ID {entry.EventId}", Bounds(GallerySpreadLayout.EventCardHeaderBounds(slot)));
        Rectangle detailWell = Bounds(GallerySpreadLayout.EventCardDetailsBounds(slot));
        string label = i18n.Get("event.details-short");
        Vector2 labelSize = Game1.smallFont.MeasureString(label);
        float labelScale = Math.Min(1f, Math.Min((detailWell.Width - 8) / Math.Max(1, labelSize.X), (detailWell.Height - 8) / Math.Max(1, labelSize.Y)));
        bool detailsFocused = Highlighted(DetailsBounds(slot), new EventCardFocus(index, EventCardAction.Details).GetComponentId(CardComponentBase));
        Vector2 labelPosition = new(detailWell.Right - 4 - labelSize.X * labelScale, detailWell.Center.Y - labelSize.Y * labelScale / 2);
        b.DrawString(Game1.smallFont, label, labelPosition, detailsFocused ? new Color(155, 99, 32) : GallerySpreadDrawing.Ink,
            0, Vector2.Zero, labelScale, SpriteEffects.None, 0);
        if (detailsFocused)
            b.Draw(Game1.staminaRect, new Rectangle((int)labelPosition.X, (int)(labelPosition.Y + labelSize.Y * labelScale), (int)(labelSize.X * labelScale), 2), GallerySpreadDrawing.LightInk);

        Rectangle image = Bounds(GallerySpreadLayout.EventCardThumbnailBounds(slot));
        bool unlocked = IsReplayAvailable(entry);
        bool replayFocused = unlocked && Highlighted(image, new EventCardFocus(index, EventCardAction.Replay).GetComponentId(CardComponentBase));
        if (unlocked)
            b.Draw(replayGlyph, new Rectangle(image.Right - 52, image.Bottom - 52, 48, 48), replayFocused ? Color.White : Color.White * .8f);
        if (replayFocused)
        {
            Color ink = GallerySpreadDrawing.LightInk;
            foreach (Point corner in new[] { new Point(image.X, image.Y), new Point(image.Right - 12, image.Y), new Point(image.X, image.Bottom - 2), new Point(image.Right - 12, image.Bottom - 2) })
                b.Draw(Game1.staminaRect, new Rectangle(corner.X, corner.Y, 12, 2), ink);
        }
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
        initializeUpperRightCloseButton();
        scrollTrack = Bounds(GallerySpreadLayout.AlbumScrollTrackBounds);
        backBounds = Bounds(GallerySpreadLayout.BackButtonBounds);
        UpdateScrollbar();
        BuildClickableComponents();
    }

    private void EnsureLayout()
    {
        if (GalleryLayout.Changed(viewportWidth, viewportHeight, Game1.uiViewport.Width, Game1.uiViewport.Height))
            RecalculateLayout();
    }

    private int MaxScroll => Math.Max(0, (events.Count + GalleryUiRules.EventColumns - 1) / GalleryUiRules.EventColumns - GalleryUiRules.EventVisibleRows);

    private void ScrollBy(int rows)
    {
        scrollRow = Math.Clamp(scrollRow + rows, 0, MaxScroll);
        UpdateScrollbar();
        BuildClickableComponents();
    }

    private void UpdateScrollbar()
    {
        scrollRow = Math.Clamp(scrollRow, 0, MaxScroll);
        const int thumbHeight = 40;
        int travel = scrollTrack.Height - thumbHeight;
        int y = MaxScroll == 0 ? scrollTrack.Y : scrollTrack.Y + (int)Math.Round(travel * scrollRow / (double)MaxScroll);
        scrollThumb = new Rectangle(scrollTrack.X, y, scrollTrack.Width, thumbHeight);
    }

    private void BuildClickableComponents()
    {
        int previousId = currentlySnappedComponent?.myID ?? -1;
        allClickableComponents = [new ClickableComponent(ToScreen(backBounds), "back") { myID = BackComponentId }];
        int first = scrollRow * GalleryUiRules.EventColumns;
        int visible = GalleryUiRules.VisibleEventCount(events.Count, first);
        for (int slot = 0; slot < visible; slot++)
        {
            int index = first + slot;
            allClickableComponents.Add(new ClickableComponent(ToScreen(DetailsBounds(slot)), $"details-{index}")
            {
                myID = new EventCardFocus(index, EventCardAction.Details).GetComponentId(CardComponentBase)
            });
            if (!IsReplayAvailable(events[index]))
                continue;
            allClickableComponents.Add(new ClickableComponent(ToScreen(Bounds(GallerySpreadLayout.EventCardThumbnailBounds(slot))), $"replay-{index}")
            {
                myID = new EventCardFocus(index, EventCardAction.Replay).GetComponentId(CardComponentBase)
            });
        }
        currentlySnappedComponent = allClickableComponents.FirstOrDefault(component => component.myID == previousId)
            ?? VisibleFocusFallback(previousId, visible)
            ?? allClickableComponents[0];
        if (EventCardFocus.TryFromComponentId(currentlySnappedComponent.myID, CardComponentBase, events.Count, out EventCardFocus current))
            lastCardFocus = current;
        else if (lastCardFocus is EventCardFocus remembered)
            lastCardFocus = remembered.ClampToViewport(events.Count, index => IsReplayAvailable(events[index]), scrollRow);
        SaveState();
        if (!dragging && Game1.options.snappyMenus && Game1.options.gamepadControls)
            SnapCurrent();
    }

    private ClickableComponent? VisibleFocusFallback(int previousId, int visible)
    {
        if (visible <= 0)
            return null;
        EventCardFocus.TryFromComponentId(previousId, CardComponentBase, events.Count, out EventCardFocus previous);
        EventCardFocus? target = previous.ClampToViewport(events.Count, index => IsReplayAvailable(events[index]), scrollRow);
        return target is EventCardFocus focus
            ? allClickableComponents.FirstOrDefault(component => component.myID == focus.GetComponentId(CardComponentBase)) : null;
    }

    private bool IsReplayAvailable(GalleryEvent entry) => context.ReplayAccess(entry).Allowed;

    private void SaveState(int? focus = null)
    {
        state.Scroll = scrollRow;
        state.Focus = focus ?? currentlySnappedComponent?.myID ?? -1;
    }

    private void SnapCurrent()
    {
        SaveState();
        snapCursorToCurrentSnappedComponent();
    }

    private void OpenEvent(int index, EventCardAction action)
    {
        GalleryEvent entry = events[index];
        SaveState(new EventCardFocus(index, action).GetComponentId(CardComponentBase));
        if (action == EventCardAction.Details)
            context.Navigation.OpenEvent(entry.Resolved.Identity);
        else if (IsReplayAvailable(entry))
            context.Navigation.Replay(entry.Resolved.Identity);
    }

    private Rectangle DetailsBounds(int slot)
    {
        Rectangle well = Bounds(GallerySpreadLayout.EventCardDetailsBounds(slot));
        Vector2 size = Game1.smallFont.MeasureString(i18n.Get("event.details-short"));
        float scale = Math.Min(1f, Math.Min((well.Width - 8) / Math.Max(1, size.X), (well.Height - 8) / Math.Max(1, size.Y)));
        int width = (int)Math.Ceiling(size.X * scale) + 8;
        int height = (int)Math.Ceiling(size.Y * scale) + 8;
        return new Rectangle(well.Right - width, well.Center.Y - height / 2, width, height);
    }

    private static Rectangle Bounds((int X, int Y, int Width, int Height) bounds)
        => new(bounds.X, bounds.Y, bounds.Width, bounds.Height);

    private bool Highlighted(Rectangle bounds, int componentId)
    {
        (int x, int y) = ToLogical(Game1.getMouseX(true), Game1.getMouseY(true));
        return bounds.Contains(x, y) || (Game1.options.snappyMenus && Game1.options.gamepadControls
            && currentlySnappedComponent?.myID == componentId);
    }

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

    private static void DrawPageTitle(SpriteBatch b, string title, Rectangle bounds)
    {
        int y = bounds.Center.Y - SpriteText.getHeightOfString(title) / 2;
        if (LocalizedContentManager.CurrentLanguageCode == LocalizedContentManager.LanguageCode.zh && title.Length <= 8)
        {
            const int gap = 12;
            int textWidth = title.Sum(character => SpriteText.getWidthOfString(character.ToString())) + gap * (title.Length - 1);
            int x = bounds.Center.X - textWidth / 2;
            foreach (char character in title)
            {
                string glyph = character.ToString();
                SpriteText.drawString(b, glyph, x, y);
                x += SpriteText.getWidthOfString(glyph) + gap;
            }
            return;
        }
        SpriteText.drawStringHorizontallyCenteredAt(b, title, bounds.Center.X, y, maxWidth: bounds.Width);
    }
}
