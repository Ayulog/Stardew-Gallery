using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.GameData.Characters;
using StardewValley.Menus;

namespace StardewGallery;

internal sealed class GalleryCharacterMenu : IClickableMenu
{
    private const int VisibleRows = 4;
    private const int BackComponentId = 1000;
    private const int TooltipTextWidth = 600;
    private const int TooltipMaxLines = 10;
    private readonly GalleryCharacter character;
    private readonly ITranslationHelper i18n;
    private readonly Texture2D background;
    private readonly Texture2D scene;
    private readonly Action back;
    private readonly Action<GalleryEvent, PreviewState?, int> replay;
    private readonly PreviewPlanner planner;
    private readonly Func<bool> isUnlocked;
    private readonly List<GalleryEvent> events;
    private int scroll;
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
    private readonly int preferredReplayComponentId;
    private bool pendingInitialSnap = true;
    private AnimatedSprite? previewSprite;
    private string? hoverTooltip;

    internal GalleryCharacterMenu(GalleryCharacter character, GalleryCatalog catalog, ITranslationHelper i18n,
        Texture2D background, Texture2D scene, Func<bool> isUnlocked, Action back,
        PreviewPlanner planner,
        Action<GalleryEvent, PreviewState?, int> replay,
        int initialScroll = 0, string? initialFocusIdentity = null)
        : base(0, 0, GalleryMenu.MenuWidth, GalleryMenu.MenuHeight, true)
    {
        this.character = character;
        this.i18n = i18n;
        this.background = background;
        this.scene = scene;
        this.isUnlocked = isUnlocked;
        this.back = back;
        this.planner = planner;
        this.replay = replay;
        events = catalog.Events
            .Where(entry => entry.Ownership.Owners.Any(owner => owner.Name == character.Name))
            .OrderBy(entry => entry.Ownership.Owners.First(owner => owner.Name == character.Name).FriendshipPoints ?? int.MaxValue)
            .ThenBy(entry => entry.EventId, StringComparer.Ordinal)
            .ToList();
        scroll = initialScroll;
        int focusIndex = initialFocusIdentity is null ? -1 : events.FindIndex(entry => entry.Identity == initialFocusIdentity);
        preferredReplayComponentId = GalleryUiRules.PreferredReplayRow(focusIndex, scroll, VisibleRows);
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
        scroll = Math.Clamp(scroll + (direction < 0 ? 1 : -1), 0, Math.Max(0, events.Count - VisibleRows));
        UpdateScrollbar();
        BuildClickableComponents();
        Game1.playSound("shiny4");
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        (x, y) = ToLogical(x, y);
        if (scrollThumb.Contains(x, y))
        {
            dragging = true;
            dragOffset = y - scrollThumb.Y;
        }
        else if (scrollTrack.Contains(x, y))
        {
            scroll += y < scrollThumb.Y ? -VisibleRows : VisibleRows;
            UpdateScrollbar();
            BuildClickableComponents();
        }
        else if (backBounds.Contains(x, y))
        {
            Return();
            return;
        }
        else
        {
            for (int row = 0; row < VisibleRows && scroll + row < events.Count; row++)
            {
                Rectangle bounds = R(775, 140 + row * 170, 705, 155);
                Rectangle primaryButton = new(bounds.Right - 185, bounds.Bottom - 62, 155, 48);
                GalleryEvent entry = events[scroll + row];
                EventConditionStatus status = Analyze(entry);
                EventCardState card = EventCardStateResolver.Resolve(
                    status.IsCurrentlyAvailable,
                    Game1.player.eventsSeen.Contains(entry.EventId),
                    isUnlocked());
                if (!primaryButton.Contains(x, y))
                    continue;
                if (card.Unlocked)
                {
                    replay(entry, null, scroll);
                    return;
                }
                bool canPreview = status.Capability is PreviewCapability.PreviewSupported or PreviewCapability.PreviewPartiallySupported;
                if (!canPreview)
                {
                    Game1.addHUDMessage(new HUDMessage(i18n.Get("preview.not-available"), HUDMessage.error_type));
                    return;
                }
                replay(entry, planner.Plan(entry, RuntimeStateReader.Capture()).Suggestion, scroll);
                return;
            }
        }
        base.receiveLeftClick(x, y, playSound);
    }

    public override void leftClickHeld(int x, int y)
    {
        (x, y) = ToLogical(x, y);
        int maximum = Math.Max(0, events.Count - VisibleRows);
        if (!dragging || maximum == 0)
            return;
        int travel = scrollTrack.Height - scrollThumb.Height;
        scroll = (int)Math.Round(Math.Clamp(y - dragOffset - scrollTrack.Y, 0, travel) / (double)travel * maximum);
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
        base.receiveGamePadButton(button);
    }

    public override void snapToDefaultClickableComponent()
    {
        currentlySnappedComponent = allClickableComponents?.FirstOrDefault(component => component.myID == preferredReplayComponentId)
            ?? allClickableComponents?.FirstOrDefault(component => component.myID == BackComponentId);
        snapCursorToCurrentSnappedComponent();
    }

    public override void draw(SpriteBatch b)
    {
        EnsureLayout();
        b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height), Color.Black * .45f);
        hoverTooltip = null;
        GalleryMenu.BeginScaled(b, menuScale, drawOffsetX, drawOffsetY);
        DrawPhoto(b);
        b.Draw(background, new Rectangle(xPositionOnScreen, yPositionOnScreen, width, height), Color.White);
        DrawPageTitle(b, i18n.Get("detail.title"), R(895, 68, 530, 56));
        DrawInformation(b);
        for (int row = 0; row < VisibleRows && scroll + row < events.Count; row++)
            DrawEvent(b, events[scroll + row], R(775, 140 + row * 170, 705, 155));
        UpdateScrollbar();
        if (events.Count > VisibleRows)
            GalleryMenu.DrawScrollbar(b, scrollThumb);
        GalleryMenu.DrawButton(b, backBounds, i18n.Get("detail.back"));
        upperRightCloseButton?.draw(b);
        GalleryMenu.EndScaled(b);
        if (hoverTooltip is not null)
            IClickableMenu.drawHoverText(b, WrapTooltip(hoverTooltip, Game1.smallFont, TooltipTextWidth, TooltipMaxLines), Game1.smallFont);
        drawMouse(b);
    }

    private void DrawPhoto(SpriteBatch b)
    {
        Rectangle photo = R(240, 120, 380, 270);
        b.Draw(scene, photo, Color.White);
        previewSprite ??= Game1.getCharacterFromName(character.Name)?.Sprite?.Clone();
        if (previewSprite?.Texture is not null)
        {
            switch ((int)(Game1.currentGameTime.TotalGameTime.TotalMilliseconds / 1800) % 4)
            {
                case 0: previewSprite.AnimateDown(Game1.currentGameTime); break;
                case 1: previewSprite.AnimateLeft(Game1.currentGameTime); break;
                case 2: previewSprite.AnimateUp(Game1.currentGameTime); break;
                default: previewSprite.AnimateRight(Game1.currentGameTime); break;
            }
            float scale = Math.Min(4f, Math.Min(photo.Width * .65f / previewSprite.SpriteWidth, photo.Height * .72f / previewSprite.SpriteHeight));
            Vector2 size = new(previewSprite.SpriteWidth * scale, previewSprite.SpriteHeight * scale);
            int groundY = photo.Y + (int)Math.Round(photo.Height * .76f);
            Vector2 position = new(photo.Center.X - size.X / 2, groundY - size.Y);
            previewSprite.drawShadow(b, position, scale, .45f);
            previewSprite.draw(b, position, .9f, 0, 0, Color.White, false, scale);
        }
    }

    private void DrawInformation(SpriteBatch b)
    {
        Friendship? friendship = Game1.player.friendshipData.GetValueOrDefault(character.Name);
        NPC.TryGetData(character.Name, out CharacterData? data);
        string birthday = data?.BirthSeason is null ? "—" : i18n.Get("detail.birthday-value", new { season = LocalizeSeason(data.BirthSeason.Value.ToString()), day = data.BirthDay });
        string relationship = friendship is null ? i18n.Get("status.none") : i18n.Get($"status.{friendship.Status.ToString().ToLowerInvariant()}");
        GalleryMenu.DrawCentered(b, character.DisplayName, R(195, 432, 445, 48));
        DrawHearts(b, R(195, 495, 445, 48), friendship?.Points ?? 0, data?.CanBeRomanced == true);
        string[] lines =
        [
            i18n.Get("detail.birthday", new { birthday }),
            i18n.Get("detail.gifts", new { count = friendship?.GiftsThisWeek ?? 0, today = friendship?.GiftsToday > 0 ? i18n.Get("common.yes") : i18n.Get("common.no") }),
            i18n.Get(friendship?.TalkedToToday == true ? "detail.talked" : "detail.not-talked"),
            i18n.Get("detail.seen", new { seen = events.Count(entry => Game1.player.eventsSeen.Contains(entry.EventId)), total = events.Count, relationship })
        ];
        for (int i = 0; i < lines.Length; i++)
            GalleryMenu.DrawCentered(b, lines[i], R(195, 558 + i * 63, 445, 48));
    }

    private void DrawEvent(SpriteBatch b, GalleryEvent entry, Rectangle row)
    {
        EventOwner owner = entry.Ownership.Owners.First(value => value.Name == character.Name);
        string heart = owner.FriendshipPoints is int points ? i18n.Get("event.hearts", new { hearts = (int)Math.Ceiling(points / 250d) }) : i18n.Get("event.unspecified");
        GalleryMenu.DrawLeftFitted(b, $"{heart} · ID {entry.EventId}", new Rectangle(row.X + 25, row.Y + 10, row.Width - 245, 40));
        string location = Game1.getLocationFromName(entry.LocationName)?.DisplayName ?? entry.LocationName;
        EventConditionStatus status = Analyze(entry);
        string fullSummaries = i18n.Get("event.location-conditions", new { location, conditions = FormatConditions(entry) });
        string summary = WrapAndTruncate(fullSummaries, Game1.smallFont, row.Width - 235, maxLines: 2, out bool truncated);
        b.DrawString(Game1.smallFont, summary, new Vector2(row.X + 25, row.Y + 58), Game1.textColor);
        EventCardState card = EventCardStateResolver.Resolve(
            status.IsCurrentlyAvailable,
            Game1.player.eventsSeen.Contains(entry.EventId),
            isUnlocked());
        Color statusColor = card.Unlocked ? new Color(20, 110, 40) : new Color(150, 20, 20);
        DrawStatusLabel(b, i18n.Get(card.StatusKey), new Vector2(row.Right - 190, row.Y + 22), statusColor);
        GalleryMenu.DrawButton(b, new Rectangle(row.Right - 185, row.Bottom - 62, 155, 48), i18n.Get(card.ButtonKey));

        if (truncated)
        {
            Rectangle summaryRegion = new(row.X + 25, row.Y + 58, row.Width - 235, 48);
            (int hx, int hy) = GetMouseLogical();
            if (summaryRegion.Contains(hx, hy))
                hoverTooltip = fullSummaries;
        }
    }

    private static string WrapAndTruncate(string text, SpriteFont font, int width, int maxLines, out bool truncated)
    {
        truncated = false;
        if (string.IsNullOrEmpty(text))
            return text;
        string wrapped = Game1.parseText(text, font, width);
        string[] lines = wrapped.Split('\n');
        if (lines.Length <= maxLines)
            return wrapped;
        truncated = true;
        return TruncateWrappedLines(lines, maxLines, font, width);
    }

    private static string WrapTooltip(string text, SpriteFont font, int width, int maxLines)
    {
        if (string.IsNullOrEmpty(text))
            return text;
        string wrapped = Game1.parseText(text, font, width);
        string[] lines = wrapped.Split('\n');
        return lines.Length <= maxLines ? wrapped : TruncateWrappedLines(lines, maxLines, font, width);
    }

    private static string TruncateWrappedLines(string[] lines, int maxLines, SpriteFont font, int width)
    {
        string[] kept = lines[..maxLines];
        string last = kept[^1];
        int ellipsisWidth = (int)font.MeasureString("…").X;
        while ((int)font.MeasureString(last).X + ellipsisWidth > width && last.Length > 0)
            last = last[..^1];
        string prefix = string.Join('\n', kept[..^1]);
        string final = last + "…";
        return prefix.Length > 0 ? prefix + "\n" + final : final;
    }

    private EventConditionStatus Analyze(GalleryEvent entry)
        => planner.Analyze(entry, RuntimeStateReader.Capture());

    private static void DrawStatusLabel(SpriteBatch b, string text, Vector2 position, Color color)
    {
        // Subtle 1px dark shadow for readability without a white outline/glow/badge.
        Vector2 shadowOffset = new(1, 1);
        b.DrawString(Game1.smallFont, text, position + shadowOffset, new Color(60, 40, 20) * 0.45f);
        b.DrawString(Game1.smallFont, text, position, color);
    }

    private static void DrawHearts(SpriteBatch b, Rectangle bounds, int points, bool canBeRomanced)
    {
        int capacity = GalleryUiRules.HeartCapacity(canBeRomanced);
        int filled = GalleryUiRules.FilledHearts(points, capacity);
        const int size = 28;
        int x = bounds.Center.X - capacity * size / 2;
        int y = bounds.Center.Y - 12;
        for (int i = 0; i < capacity; i++)
            b.Draw(Game1.mouseCursors, new Vector2(x + i * size, y), new Rectangle(i < filled ? 211 : 218, 428, 7, 6), Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, .88f);
    }

    private string FormatConditions(GalleryEvent entry)
    {
        List<string> result = [];
        foreach (string condition in Event.SplitPreconditions(entry.EventKey).Skip(1))
        {
            string[] tokens = condition.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
                continue;
            string text = tokens[0] switch
            {
                "f" when tokens.Length >= 3 && int.TryParse(tokens[2], out int points) => i18n.Get("condition.hearts", new { npc = NPC.GetDisplayName(tokens[1]), hearts = (int)Math.Ceiling(points / 250d) }),
                "e" when tokens.Length >= 2 => i18n.Get("condition.seen", new { id = tokens[1] }),
                "t" when tokens.Length >= 3 => i18n.Get("condition.time", new { from = FormatTime(tokens[1]), to = FormatTime(tokens[2]) }),
                "w" when tokens.Length >= 2 => i18n.Get("condition.weather", new { weather = Translate("weather", tokens[1]) }),
                "z" when tokens.Length >= 2 => i18n.Get("condition.season-not", new { season = Translate("season", tokens[1]) }),
                "y" when tokens.Length >= 2 => i18n.Get("condition.year", new { year = tokens[1] }),
                "d" when tokens.Length >= 2 => i18n.Get("condition.day", new { day = tokens[1] }),
                "p" when tokens.Length >= 2 => i18n.Get("condition.present", new { npc = NPC.GetDisplayName(tokens[1]) }),
                "M" or "m" when tokens.Length >= 2 => i18n.Get("condition.mail", new { id = tokens[1] }),
                _ => i18n.Get("condition.other")
            };
            if (!result.Contains(text, StringComparer.CurrentCulture))
                result.Add(text);
        }
        return string.Join(i18n.Get("condition.separator"), result);
    }

    private string Translate(string group, string value)
    {
        string key = $"{group}.{value.ToLowerInvariant()}";
        string translated = i18n.Get(key);
        return translated == key ? value : translated;
    }

    private static string FormatTime(string raw) => int.TryParse(raw, out int value) ? $"{value / 100:00}:{value % 100:00}" : raw;

    private string LocalizeSeason(string season)
    {
        string key = $"season.{season.ToLowerInvariant()}";
        string translated = i18n.Get(key);
        return translated == key ? season : translated;
    }

    private void Return()
    {
        Game1.playSound("bigDeSelect");
        back();
    }

    internal void HandleControllerBack() => Return();

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
        initializeUpperRightCloseButton();
        scrollTrack = R(1508, 180, 24, 600);
        backBounds = R(360, 842, 280, 52);
        UpdateScrollbar();
        BuildClickableComponents();
    }

    private void EnsureLayout()
    {
        if (GalleryLayout.Changed(viewportWidth, viewportHeight, Game1.uiViewport.Width, Game1.uiViewport.Height))
            RecalculateLayout();
    }

    private void UpdateScrollbar()
    {
        int maximum = Math.Max(0, events.Count - VisibleRows);
        scroll = Math.Clamp(scroll, 0, maximum);
        int height = 40;
        int travel = scrollTrack.Height - height;
        int y = maximum == 0 ? scrollTrack.Y : scrollTrack.Y + (int)Math.Round(travel * scroll / (double)maximum);
        scrollThumb = new Rectangle(scrollTrack.X, y, scrollTrack.Width, height);
    }

    private void BuildClickableComponents()
    {
        int previousId = currentlySnappedComponent?.myID ?? -1;
        allClickableComponents = [];
        int visible = Math.Min(VisibleRows, Math.Max(0, events.Count - scroll));
        ClickableComponent backComponent = new(ToScreen(backBounds), "back")
        {
            myID = BackComponentId,
            rightNeighborID = visible > 0 ? 0 : -1,
            upNeighborID = visible > 0 ? visible - 1 : -1
        };
        allClickableComponents.Add(backComponent);
        for (int row = 0; row < visible; row++)
        {
            Rectangle bounds = R(775, 140 + row * 170, 705, 155);
            ClickableComponent actionComponent = new(ToScreen(new Rectangle(bounds.Right - 185, bounds.Bottom - 62, 155, 48)), $"action-{row}")
            {
                myID = row,
                leftNeighborID = BackComponentId,
                upNeighborID = row > 0 ? row - 1 : BackComponentId,
                downNeighborID = row + 1 < visible ? row + 1 : BackComponentId
            };
            allClickableComponents.Add(actionComponent);
        }
        if (upperRightCloseButton is not null)
        {
            upperRightCloseButton.leftNeighborID = visible > 0 ? 0 : BackComponentId;
            upperRightCloseButton.downNeighborID = visible > 0 ? 0 : BackComponentId;
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

    private Rectangle R(int x, int y, int w, int h) => new(xPositionOnScreen + x, yPositionOnScreen + y, w, h);

    private Rectangle ToScreen(Rectangle bounds) => GalleryMenu.ScaleRectangle(bounds, menuScale, drawOffsetX, drawOffsetY);

    private (int X, int Y) ToLogical(int x, int y)
        => ((int)Math.Round((x - drawOffsetX) / menuScale), (int)Math.Round((y - drawOffsetY) / menuScale));

    private (int X, int Y) GetMouseLogical()
        => ToLogical(Game1.getMouseX(true), Game1.getMouseY(true));

    private static void DrawPageTitle(SpriteBatch b, string title, Rectangle bounds)
    {
        int y = bounds.Center.Y - SpriteText.getHeightOfString(title) / 2;
        if (LocalizedContentManager.CurrentLanguageCode == LocalizedContentManager.LanguageCode.zh && title.Length <= 8)
        {
            const int gap = 12;
            int width = title.Sum(character => SpriteText.getWidthOfString(character.ToString())) + gap * (title.Length - 1);
            int x = bounds.Center.X - width / 2;
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
