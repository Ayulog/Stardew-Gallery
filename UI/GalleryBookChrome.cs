using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;

namespace StardewGallery;

/// <summary>
/// Shared visual chrome and interaction helpers for the leather-bound three-tab journal spread:
/// bottom-left ribbon bookmarks with label plaques, whole-page book turners, sealed-envelope cards and status badges.
/// Art comes from <see cref="GalleryUiSkin"/>; every helper keeps a plain fallback when the art is unavailable.
/// </summary>
internal static class GalleryBookChrome
{
    internal const int RibbonTabBaseId = 9100;
    internal const int PrevPageComponentId = 9200;
    internal const int NextPageComponentId = 9201;

    private static readonly (Color Fallback, string Key)[] RibbonStyles =
    [
        (new Color(196, 68, 60), "tab.villagers"),
        (new Color(64, 134, 88), "tab.index"),
        (new Color(208, 146, 44), "tab.moments")
    ];

    internal static void DrawCleanHomeSpread(SpriteBatch b, Texture2D homeTexture, int width, int height)
        => b.Draw(homeTexture, new Rectangle(0, 0, width, height), Color.White);

    internal static void DrawSpreadTitle(SpriteBatch b, string title, float menuScale = 1f, int offsetX = 0, int offsetY = 0)
        => GalleryDrawing.DrawBookTitle(b, title, ToRect(GallerySpreadLayout.SpreadTitleBounds), menuScale, offsetX, offsetY);

    internal static void DrawPageTitle(SpriteBatch b, string title, float menuScale = 1f, int offsetX = 0, int offsetY = 0)
        => GalleryDrawing.DrawBookTitle(b, title, ToRect(GallerySpreadLayout.TitleBounds), menuScale, offsetX, offsetY);

    /// <summary>
    /// Places <paramref name="closeButton"/> cleanly outside the leather book's ornate top-right brass corner
    /// (<c>X = 1612..1658, Y = 16..58</c>) when the viewport margin permits, or on the clean inner parchment
    /// margin on ultra-tight 4:3 viewports, always clamped inside the visible viewport.
    /// </summary>
    internal static void PositionCloseButton(
        ClickableTextureComponent? closeButton,
        float menuScale,
        int offsetX,
        int offsetY,
        int viewportWidth,
        int viewportHeight)
    {
        if (closeButton is null)
            return;
        const int size = 48;
        float safeScale = Math.Max(0.25f, menuScale);
        int maxLogicalX = (int)Math.Floor((viewportWidth - 6 - offsetX) / safeScale) - size;
        int minLogicalY = (int)Math.Ceiling((6 - offsetY) / safeScale);
        int maxLogicalY = (int)Math.Floor((viewportHeight - 6 - offsetY) / safeScale) - size;

        int x = maxLogicalX >= 1656 ? Math.Min(1662, maxLogicalX) : Math.Min(1548, Math.Max(0, maxLogicalX));
        int y = maxLogicalX >= 1656 ? 12 : 48;
        if (maxLogicalY >= minLogicalY)
            y = Math.Clamp(y, Math.Max(0, minLogicalY), maxLogicalY);
        closeButton.bounds = new Rectangle(x, y, size, size);
    }

    private static Rectangle PlaqueRect(int tab, bool albumPage) => ToRect(GallerySpreadLayout.RibbonTabBounds(tab, albumPage));

    private static Rectangle HitRect(int tab, bool albumPage, bool active)
        => Rectangle.Union(PlaqueRect(tab, albumPage), ToRect(GallerySpreadLayout.RibbonTailBounds(tab, active, albumPage)));

    internal static void AddRibbonTabComponents(
        List<ClickableComponent> components,
        Func<Rectangle, Rectangle> toScreen,
        int rightNeighborId,
        bool albumPage = false,
        int upNeighborId = -1)
    {
        for (int tab = 0; tab < GallerySpreadLayout.RibbonTabCount; tab++)
        {
            components.Add(new ClickableComponent(toScreen(PlaqueRect(tab, albumPage)), $"ribbon-{tab}")
            {
                myID = RibbonTabBaseId + tab,
                leftNeighborID = tab > 0 ? RibbonTabBaseId + tab - 1 : -1,
                rightNeighborID = tab + 1 < GallerySpreadLayout.RibbonTabCount ? RibbonTabBaseId + tab + 1 : rightNeighborId,
                upNeighborID = upNeighborId,
                downNeighborID = -1,
                fullyImmutable = true
            });
        }
    }

    internal static bool TryClickRibbonTab(int logicalX, int logicalY, GalleryBookTab currentTab, IGalleryNavigation navigation,
        Action? beforeSwitch = null, bool albumPage = false)
    {
        for (int i = 0; i < GallerySpreadLayout.RibbonTabCount; i++)
        {
            if (!HitRect(i, albumPage, (int)currentTab == i).Contains(logicalX, logicalY))
                continue;
            beforeSwitch?.Invoke();
            Game1.playSound("shwip");
            navigation.SwitchTab((GalleryBookTab)i);
            return true;
        }
        return false;
    }

    internal static void DrawRibbonTabs(
        SpriteBatch b,
        GalleryBookTab activeTab,
        int focusedComponentId,
        bool showControllerFocus,
        Point logicalCursor,
        ITranslationHelper i18n,
        out string? hoverTooltip,
        bool albumPage = false)
    {
        hoverTooltip = null;
        for (int i = 0; i < GallerySpreadLayout.RibbonTabCount; i++)
        {
            bool active = (int)activeTab == i;
            bool focused = showControllerFocus && focusedComponentId == RibbonTabBaseId + i;
            bool hovered = HitRect(i, albumPage, active).Contains(logicalCursor);
            Rectangle plaque = PlaqueRect(i, albumPage);
            GalleryUiSkin.DrawRibbonTail(b, i, active, ToRect(GallerySpreadLayout.RibbonTailBounds(i, active, albumPage)), RibbonStyles[i].Fallback);
            GalleryUiSkin.DrawTabPlaque(b, plaque, i18n.Get(RibbonStyles[i].Key), active, hovered || focused);
            if (focused)
                GalleryUiSkin.DrawFocusCorners(b, GalleryDrawing.Inset(plaque, -5, -5), 10);
            if ((hovered || focused) && !active)
                hoverTooltip = i18n.Get(RibbonStyles[i].Key);
        }
    }

    internal static void DrawPager(
        SpriteBatch b,
        (int X, int Y, int Width, int Height) prevTuple,
        (int X, int Y, int Width, int Height) indicatorTuple,
        (int X, int Y, int Width, int Height) nextTuple,
        int currentPage,
        int itemCount,
        int pageSize,
        int focusedComponentId,
        bool showFocus,
        Point? logicalCursor = null)
    {
        int totalPages = GalleryPagination.TotalPages(itemCount, pageSize);
        int clamped = GalleryPagination.ClampPage(currentPage, itemCount, pageSize);
        Rectangle prev = ToRect(prevTuple);
        Rectangle indicator = ToRect(indicatorTuple);
        Rectangle next = ToRect(nextTuple);

        bool canPrev = clamped > 0;
        bool canNext = clamped + 1 < totalPages;
        bool prevHot = showFocus && focusedComponentId == PrevPageComponentId || logicalCursor is { } c1 && prev.Contains(c1);
        bool nextHot = showFocus && focusedComponentId == NextPageComponentId || logicalCursor is { } c2 && next.Contains(c2);
        GalleryUiSkin.DrawPagerArrow(b, prev, false, !canPrev ? UiButtonState.Disabled : prevHot ? UiButtonState.Hover : UiButtonState.Normal);
        GalleryUiSkin.DrawPagerArrow(b, next, true, !canNext ? UiButtonState.Disabled : nextHot ? UiButtonState.Hover : UiButtonState.Normal);
        GalleryDrawing.DrawCentered(b, $"{clamped + 1} / {totalPages}", indicator, GallerySpreadDrawing.Ink);

        if (showFocus && focusedComponentId == PrevPageComponentId)
            GalleryUiSkin.DrawFocusCorners(b, GalleryDrawing.Inset(prev, -4, -4), 10);
        else if (showFocus && focusedComponentId == NextPageComponentId)
            GalleryUiSkin.DrawFocusCorners(b, GalleryDrawing.Inset(next, -4, -4), 10);
    }

    /// <summary>Unseen story cover: the sealed envelope art (red seal, or gold for an unseen parallel route).</summary>
    internal static void DrawLockedStoryEnvelope(
        SpriteBatch b,
        Rectangle bounds,
        string primaryClue,
        string locationClue,
        bool parallel = false)
    {
        if (GalleryUiSkin.DrawEnvelope(b, bounds, parallel))
            return;

        // Fallback without art: plain parchment card with a wax-red seal dot and the clue text.
        b.Draw(Game1.staminaRect, bounds, new Color(230, 210, 174));
        DrawBorder(b, GalleryDrawing.Inset(bounds, 6, 6), new Color(188, 156, 114), 2);
        int size = Math.Min(36, bounds.Height / 3);
        b.Draw(Game1.staminaRect, new Rectangle(bounds.Center.X - size / 2, bounds.Center.Y - size / 2 - 8, size, size),
            parallel ? new Color(214, 160, 44) : new Color(156, 44, 42));
        string clueLine = string.IsNullOrWhiteSpace(locationClue) || locationClue == "-" ? primaryClue : primaryClue + GalleryDrawing.TextSeparator + locationClue;
        GalleryDrawing.DrawCentered(b, clueLine, new Rectangle(bounds.X + 8, bounds.Bottom - 32, bounds.Width - 16, 26), GallerySpreadDrawing.Ink);
    }

    internal static void DrawClueBadge(SpriteBatch b, Rectangle bounds, EncounterClue clue)
    {
        if (clue == EncounterClue.None)
            return;
        if (GalleryUiSkin.Get(GalleryUiSkin.BadgesAsset) is not null)
        {
            GalleryUiSkin.DrawBadge(b, clue == EncounterClue.AvailableToday ? UiBadge.AvailableToday : UiBadge.ClueReady, bounds);
            return;
        }
        if (clue == EncounterClue.AvailableToday)
        {
            b.Draw(Game1.staminaRect, bounds, new Color(238, 178, 38));
            DrawBorder(b, bounds, new Color(255, 240, 156), 2);
            int cx = bounds.Center.X - 2;
            b.Draw(Game1.staminaRect, new Rectangle(cx, bounds.Y + 5, 4, bounds.Height - 15), new Color(78, 42, 12));
            b.Draw(Game1.staminaRect, new Rectangle(cx, bounds.Bottom - 7, 4, 4), new Color(78, 42, 12));
        }
        else
        {
            b.Draw(Game1.staminaRect, bounds, new Color(214, 188, 138));
            DrawBorder(b, bounds, new Color(142, 108, 66), 2);
        }
    }

    internal static void DrawGoldStarBadge(SpriteBatch b, Rectangle destination)
        => GalleryUiSkin.DrawBadge(b, UiBadge.Star, destination);

    internal static void DrawFavoriteHeartBadge(SpriteBatch b, Rectangle destination)
        => GalleryUiSkin.DrawBadge(b, UiBadge.HeartFull, destination);

    internal static void DrawBorder(SpriteBatch b, Rectangle bounds, Color color, int thickness = 3)
    {
        b.Draw(Game1.staminaRect, new Rectangle(bounds.X, bounds.Y, bounds.Width, thickness), color);
        b.Draw(Game1.staminaRect, new Rectangle(bounds.X, bounds.Bottom - thickness, bounds.Width, thickness), color);
        b.Draw(Game1.staminaRect, new Rectangle(bounds.X, bounds.Y, thickness, bounds.Height), color);
        b.Draw(Game1.staminaRect, new Rectangle(bounds.Right - thickness, bounds.Y, thickness, bounds.Height), color);
    }

    internal static Rectangle ToRect((int X, int Y, int Width, int Height) tuple)
        => new(tuple.X, tuple.Y, tuple.Width, tuple.Height);
}
