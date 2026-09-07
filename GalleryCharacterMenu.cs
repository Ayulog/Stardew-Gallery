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
    private const int ReplayComponentBase = 2000;
    private const int DetailsComponentBase = 3000;
    private readonly GalleryCharacter character;
    private readonly ITranslationHelper i18n;
    private readonly Texture2D background;
    private readonly Texture2D thumbnail;
    private readonly Action back;
    private readonly Action<GalleryEvent, int> replay;
    private readonly Action<GalleryEvent, int, IReadOnlyList<ConditionDisplayItem>> details;
    private readonly Func<bool> isUnlocked;
    private readonly List<GalleryEvent> events;
    private readonly Dictionary<EventIdentity, IReadOnlyList<ConditionDisplayItem>> conditionItems;
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

    internal GalleryCharacterMenu(GalleryCharacter character, GalleryCatalog catalog, ITranslationHelper i18n,
        Texture2D background, Texture2D scene, Texture2D thumbnail, Func<bool> isUnlocked, Action back,
        Action<GalleryEvent, int> replay,
        Action<GalleryEvent, int, IReadOnlyList<ConditionDisplayItem>> details,
        int initialScroll = 0, string? initialFocusIdentity = null)
        : base(0, 0, GalleryMenu.MenuWidth, GalleryMenu.MenuHeight, true)
    {
        this.character = character;
        this.i18n = i18n;
        this.background = background;
        this.thumbnail = thumbnail;
        this.isUnlocked = isUnlocked;
        this.back = back;
        this.replay = replay;
        this.details = details;
        events = EventsFor(character, catalog);
        CurrentStateSnapshot sharedState = RuntimeStateReader.Capture();
        ConditionPresentationBuilder presentation = new(
            ConditionProduction.CreateParser(Event.SplitPreconditions, ArgUtility.SplitBySpaceQuoteAware),
            ConditionProduction.CreateEvaluator(null),
            (key, arguments) => i18n.Get(key, arguments),
            new ConditionDisplayResolver(NPC.GetDisplayName, id => ItemRegistry.GetData(id)?.DisplayName, Translate, Game1.getTimeOfDayString));
        conditionItems = events.ToDictionary(
            entry => entry.Resolved.Identity,
            entry => presentation.Build(entry.EventKey, RuntimeStateReader.ForLocation(sharedState, Game1.getLocationFromName(entry.LocationName))));
        leftPanel = new GalleryCharacterPanel(character, events, i18n, scene);

        int focusIndex = initialFocusIdentity is null ? -1 : events.FindIndex(entry => entry.Identity == initialFocusIdentity);
        (scrollRow, _) = GalleryUiRules.ResolveReturnPosition(
            focusIndex, initialScroll, GalleryUiRules.EventColumns, GalleryUiRules.EventVisibleRows, events.Count);
        bool replayAvailable = focusIndex >= 0 && IsReplayAvailable(events[focusIndex]);
        preferredComponentId = focusIndex < 0 ? DetailsComponentBase : (replayAvailable ? ReplayComponentBase : DetailsComponentBase) + focusIndex;
        RecalculateLayout();
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
        if (!pendingInitialSnap || !Game1.options.snappyMenus || !Game1.options.gamepadControls)
            return;
        pendingInitialSnap = false;
        snapToDefaultClickableComponent();
    }

    public override void receiveScrollWheelAction(int direction)
    {
        ScrollBy(direction < 0 ? 1 : -1);
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
        int visible = Math.Min(GalleryUiRules.EventColumns * GalleryUiRules.EventVisibleRows, events.Count - first);
        for (int slot = 0; slot < visible; slot++)
        {
            GalleryEvent entry = events[first + slot];
            Rectangle card = Card(slot);
            if (DetailsBounds(card).Contains(x, y))
            {
                details(entry, scrollRow, conditionItems[entry.Resolved.Identity]);
                return;
            }
            if (IsReplayAvailable(entry) && ThumbnailBounds(card).Contains(x, y))
            {
                replay(entry, scrollRow);
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
        if ((button is Buttons.DPadDown or Buttons.LeftThumbstickDown) && ScrollController(1)
            || (button is Buttons.DPadUp or Buttons.LeftThumbstickUp) && ScrollController(-1))
            return;
        base.receiveGamePadButton(button);
    }

    public override void snapToDefaultClickableComponent()
    {
        currentlySnappedComponent = allClickableComponents?.FirstOrDefault(component => component.myID == preferredComponentId)
            ?? allClickableComponents?.FirstOrDefault(component => component.myID >= DetailsComponentBase)
            ?? allClickableComponents?.FirstOrDefault(component => component.myID == BackComponentId);
        snapCursorToCurrentSnappedComponent();
    }

    public override void draw(SpriteBatch b)
    {
        EnsureLayout();
        b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height), Color.Black * .45f);
        GalleryMenu.BeginScaled(b, menuScale, drawOffsetX, drawOffsetY);
        leftPanel.DrawPhoto(b);
        b.Draw(background, new Rectangle(0, 0, width, height), Color.White);
        DrawPageTitle(b, i18n.Get("detail.title"), new Rectangle(895, 68, 530, 56));
        leftPanel.DrawInformation(b);

        int first = scrollRow * GalleryUiRules.EventColumns;
        int visible = Math.Min(GalleryUiRules.EventColumns * GalleryUiRules.EventVisibleRows, events.Count - first);
        for (int slot = 0; slot < visible; slot++)
            DrawEvent(b, events[first + slot], Card(slot));
        if (MaxScroll > 0)
            GalleryMenu.DrawScrollbar(b, scrollThumb);
        GalleryMenu.DrawButton(b, backBounds, i18n.Get("detail.back"));
        upperRightCloseButton?.draw(b);
        GalleryMenu.EndScaled(b);
        drawMouse(b);
    }

    private void DrawEvent(SpriteBatch b, GalleryEvent entry, Rectangle card)
    {
        IClickableMenu.drawTextureBox(b, card.X, card.Y, card.Width, card.Height, Color.White);
        EventOwner owner = entry.Ownership.Owners.First(value => value.Name == character.Name);
        string hearts = owner.FriendshipPoints is int points
            ? i18n.Get("event.hearts", new { hearts = (int)Math.Ceiling(points / 250d) })
            : i18n.Get("event.unspecified");
        GalleryMenu.DrawLeftFitted(b, $"{hearts} · ID {entry.EventId}", new Rectangle(card.X + 16, card.Y + 10, card.Width - 132, 34));
        GalleryMenu.DrawLeftFitted(b, i18n.Get("event.details-short"), DetailsBounds(card));

        Rectangle image = ThumbnailBounds(card);
        Color tint = IsReplayAvailable(entry) ? Color.White : Color.Gray * .72f;
        b.Draw(thumbnail, image, null, tint, 0f, Vector2.Zero, SpriteEffects.None, .88f);
        if (IsReplayAvailable(entry))
            GalleryMenu.DrawCentered(b, "▶", new Rectangle(image.Right - 42, image.Bottom - 36, 34, 30));
    }

    private bool ScrollController(int direction)
    {
        int id = currentlySnappedComponent?.myID ?? -1;
        int baseId = id >= DetailsComponentBase ? DetailsComponentBase : id >= ReplayComponentBase ? ReplayComponentBase : -1;
        if (baseId < 0)
            return false;
        int index = id - baseId;
        if ((direction > 0 && baseId == DetailsComponentBase && IsReplayAvailable(events[index]))
            || (direction < 0 && baseId == ReplayComponentBase))
            return false;
        int visibleRow = index / GalleryUiRules.EventColumns - scrollRow;
        if (direction > 0 && (visibleRow < GalleryUiRules.EventVisibleRows - 1 || scrollRow >= MaxScroll)
            || direction < 0 && (visibleRow > 0 || scrollRow <= 0))
            return false;
        int target = index + direction * GalleryUiRules.EventColumns;
        if (target < 0 || target >= events.Count)
            return false;
        scrollRow += direction;
        UpdateScrollbar();
        BuildClickableComponents();
        currentlySnappedComponent = allClickableComponents.FirstOrDefault(component => component.myID == baseId + target)
            ?? allClickableComponents.FirstOrDefault(component => component.myID == DetailsComponentBase + target);
        snapCursorToCurrentSnappedComponent();
        return true;
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
        initializeUpperRightCloseButton();
        scrollTrack = new Rectangle(1508, 180, 24, 600);
        backBounds = new Rectangle(360, 842, 280, 52);
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
        int visible = Math.Min(GalleryUiRules.EventColumns * GalleryUiRules.EventVisibleRows, events.Count - first);
        for (int slot = 0; slot < visible; slot++)
        {
            int index = first + slot;
            (int row, int column) = GalleryUiRules.EventCardPosition(slot);
            int leftIndex = column > 0 ? index - 1 : -1;
            int rightIndex = column + 1 < GalleryUiRules.EventColumns && index + 1 < events.Count ? index + 1 : -1;
            int upIndex = row > 0 ? index - GalleryUiRules.EventColumns : -1;
            int downIndex = row + 1 < GalleryUiRules.EventVisibleRows && index + GalleryUiRules.EventColumns < events.Count ? index + GalleryUiRules.EventColumns : -1;
            Rectangle card = Card(slot);
            allClickableComponents.Add(new ClickableComponent(ToScreen(DetailsBounds(card)), $"details-{index}")
            {
                myID = DetailsComponentBase + index,
                leftNeighborID = leftIndex >= 0 ? DetailsComponentBase + leftIndex : BackComponentId,
                rightNeighborID = rightIndex >= 0 ? DetailsComponentBase + rightIndex : -1,
                upNeighborID = upIndex >= 0 ? DetailsComponentBase + upIndex : -1,
                downNeighborID = IsReplayAvailable(events[index])
                    ? ReplayComponentBase + index
                    : downIndex >= 0 ? DetailsComponentBase + downIndex : BackComponentId
            });
            if (!IsReplayAvailable(events[index]))
                continue;
            allClickableComponents.Add(new ClickableComponent(ToScreen(ThumbnailBounds(card)), $"replay-{index}")
            {
                myID = ReplayComponentBase + index,
                leftNeighborID = leftIndex >= 0 ? ReplayOrDetails(leftIndex) : BackComponentId,
                rightNeighborID = rightIndex >= 0 ? ReplayOrDetails(rightIndex) : -1,
                upNeighborID = DetailsComponentBase + index,
                downNeighborID = downIndex >= 0 ? ReplayOrDetails(downIndex) : BackComponentId
            });
        }
        currentlySnappedComponent = allClickableComponents.FirstOrDefault(component => component.myID == previousId);
    }

    private bool IsReplayAvailable(GalleryEvent entry) => EventCardStateResolver.Resolve(
        Game1.player.eventsSeen.Contains(entry.EventId), isUnlocked()).Unlocked;

    private int ReplayOrDetails(int index) => (IsReplayAvailable(events[index]) ? ReplayComponentBase : DetailsComponentBase) + index;

    internal static List<GalleryEvent> EventsFor(GalleryCharacter character, GalleryCatalog catalog) => catalog.Events
        .Where(entry => entry.Ownership.Owners.Any(owner => owner.Name == character.Name))
        .OrderBy(entry => entry.Ownership.Owners.First(owner => owner.Name == character.Name).FriendshipPoints ?? int.MaxValue)
        .ThenBy(entry => entry.EventId, StringComparer.Ordinal)
        .ToList();

    private static Rectangle Card(int slot)
    {
        (int x, int y, int cardWidth, int cardHeight) = GalleryUiRules.EventCardBounds(slot);
        return new Rectangle(x, y, cardWidth, cardHeight);
    }

    private static Rectangle DetailsBounds(Rectangle card) => new(card.Right - 112, card.Y + 8, 98, 36);
    private static Rectangle ThumbnailBounds(Rectangle card) => new(card.X + 40, card.Y + 50, card.Width - 80, 149);

    private string Translate(string group, string value)
    {
        string key = $"{group}.{value.ToLowerInvariant()}";
        string translated = i18n.Get(key);
        return translated == key ? value : translated;
    }

    private void Return()
    {
        Game1.playSound("bigDeSelect");
        back();
    }

    internal void HandleControllerBack() => Return();

    private void SnapForGamepad()
    {
        if (Game1.options.snappyMenus && Game1.options.gamepadControls)
            snapToDefaultClickableComponent();
    }

    private Rectangle ToScreen(Rectangle bounds) => GalleryMenu.ScaleRectangle(bounds, menuScale, drawOffsetX, drawOffsetY);
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
