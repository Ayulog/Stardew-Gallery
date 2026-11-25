using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace StardewGallery;

internal enum UiButtonState { Normal = 0, Hover = 1, Disabled = 2 }
internal enum UiBadge { AvailableToday = 0, ClueReady = 1, Star = 2, HeartFull = 3, HeartEmpty = 4, Rings = 5, Branch = 6 }
internal enum UiPill { Amber = 0, Crimson = 1, Wood = 2 }
internal enum UiHudIcon { Camera = 0, Pause = 1, FastForward = 2, Music = 3 }

/// <summary>
/// Pixel-art skin sliced from the 2026-11-17 UI art pack. Every texture is optional: when a texture is missing
/// (headless checks, damaged install) each helper falls back to the previous vanilla drawing, so the gallery
/// always stays usable. Source rectangles describe the atlas layout written by the slicing script.
/// </summary>
internal static class GalleryUiSkin
{
    internal const string RibbonsAsset = "assets/UiRibbons.png";
    internal const string TabPlaquesAsset = "assets/UiTabPlaques.png";
    internal const string WoodButtonsAsset = "assets/UiWoodButtons.png";
    internal const string PagerArrowsAsset = "assets/UiPagerArrows.png";
    internal const string SearchBoxAsset = "assets/UiSearchBox.png";
    internal const string FilterRowAsset = "assets/UiFilterRow.png";
    internal const string ClueRowAsset = "assets/UiClueRow.png";
    internal const string StoryRowAsset = "assets/UiStoryRow.png";
    internal const string CheckboxAsset = "assets/UiCheckbox.png";
    internal const string BadgesAsset = "assets/UiBadges.png";
    internal const string PillsAsset = "assets/UiPills.png";
    internal const string HudIconsAsset = "assets/UiHudIcons.png";
    internal const string FocusCornersAsset = "assets/UiFocusCorners.png";
    internal const string CardFrameAsset = "assets/UiCardFrame.png";
    internal const string TitlePlaqueAsset = "assets/UiTitlePlaque.png";
    internal const string FavoriteRibbonAsset = "assets/UiFavoriteRibbon.png";
    internal const string LockedEnvelopeAsset = "assets/CardLockedEnvelope.png";
    internal const string ParallelEnvelopeAsset = "assets/CardParallelEnvelope.png";

    internal static readonly string[] AllAssets =
    [
        RibbonsAsset, TabPlaquesAsset, WoodButtonsAsset, PagerArrowsAsset, SearchBoxAsset, FilterRowAsset, ClueRowAsset, StoryRowAsset,
        CheckboxAsset, BadgesAsset, PillsAsset, HudIconsAsset, FocusCornersAsset, CardFrameAsset, TitlePlaqueAsset, FavoriteRibbonAsset,
        LockedEnvelopeAsset, ParallelEnvelopeAsset
    ];

    // Atlas cell sizes (see the slicer script): keep in sync with Checks/GallerySpreadChecks.cs.
    internal const int RibbonCellWidth = 56, RibbonCellHeight = 132;
    internal const int TabPlaqueWidth = 124, TabPlaqueHeight = 32;
    internal const int WoodButtonWidth = 116, WoodButtonHeight = 52, WoodButtonCap = 22;
    internal const int PagerCell = 44;
    internal const int SearchWidth = 330, SearchHeight = 48, SearchLeftCap = 56, SearchRightCap = 14;
    internal const int FilterWidth = 378, FilterHeight = 52, FilterLeftCap = 14, FilterRightCap = 40;
    internal const int ClueWidth = 364, ClueHeight = 58, ClueCap = 26;
    internal const int StoryRowWidth = 446, StoryRowHeight = 112, StoryRowLeftCap = 152, StoryRowRightCap = 16;
    internal const int CheckboxCell = 28;
    internal const int BadgeCell = 32;
    internal const int PillWidth = 104, PillHeight = 24, PillCap = 12;
    internal const int HudCell = 32;
    internal const int CornerCell = 14;
    internal const int CardFrameWidth = 297, CardFrameHeight = 177, CardFrameInsetX = 16, CardFrameInsetY = 14;
    internal const int TitlePlaqueWidth = 220, TitlePlaqueHeight = 30, TitlePlaqueCap = 26;

    /// <summary>Inside a story-row card, where the small cover photo sits (relative to the row's top-left, at 112 px height).</summary>
    internal static Rectangle StoryRowThumbnail(Rectangle row) => new(row.X + 22, row.Y + 23, 114, 68);

    private static readonly Dictionary<string, Texture2D?> textures = new(StringComparer.Ordinal);

    internal static bool IsLoaded => textures.Count > 0;

    internal static void Load(Func<string, Texture2D> load, Action<string>? warn = null)
    {
        foreach (string asset in AllAssets)
        {
            try { textures[asset] = load(asset); }
            catch (Exception error)
            {
                textures[asset] = null;
                warn?.Invoke($"UI skin texture {asset} could not be loaded; using the plain fallback: {error.Message}");
            }
        }
    }

    internal static Texture2D? Get(string asset) => textures.TryGetValue(asset, out Texture2D? texture) ? texture : null;

    // ---------------------------------------------------------------- drawing helpers

    /// <summary>Draw a horizontal three-slice of the source row at any width; caps scale with the height.</summary>
    internal static void DrawThreeSlice(SpriteBatch b, Texture2D texture, Rectangle source, Rectangle dest, int leftCap, int rightCap, Color color)
    {
        float k = dest.Height / (float)source.Height;
        int l = Math.Min(dest.Width / 2, (int)Math.Round(leftCap * k));
        int r = Math.Min(dest.Width - l, (int)Math.Round(rightCap * k));
        b.Draw(texture, new Rectangle(dest.X, dest.Y, l, dest.Height), new Rectangle(source.X, source.Y, leftCap, source.Height), color);
        int middle = dest.Width - l - r;
        if (middle > 0)
            b.Draw(texture, new Rectangle(dest.X + l, dest.Y, middle, dest.Height),
                new Rectangle(source.X + leftCap, source.Y, source.Width - leftCap - rightCap, source.Height), color);
        b.Draw(texture, new Rectangle(dest.Right - r, dest.Y, r, dest.Height), new Rectangle(source.Right - rightCap, source.Y, rightCap, source.Height), color);
    }

    internal static void DrawWoodButton(SpriteBatch b, Rectangle bounds, string text, UiButtonState state, float opacity = 1f, UiHudIcon? leadingIcon = null)
    {
        Texture2D? texture = Get(WoodButtonsAsset);
        if (texture is null)
        {
            IClickableMenu.drawTextureBox(b, bounds.X, bounds.Y, bounds.Width, bounds.Height, Color.White * opacity * (state == UiButtonState.Disabled ? 0.6f : 1f));
            GalleryDrawing.DrawCentered(b, text, GalleryDrawing.Inset(bounds, 14, 2), Game1.textColor * opacity);
            return;
        }
        Rectangle source = new(0, (int)state * WoodButtonHeight, WoodButtonWidth, WoodButtonHeight);
        DrawThreeSlice(b, texture, source, bounds, WoodButtonCap, WoodButtonCap, Color.White * opacity);
        Color ink = state == UiButtonState.Disabled ? new Color(214, 186, 150) : new Color(62, 32, 12);
        int capInset = Math.Max(10, (int)Math.Round(WoodButtonCap * bounds.Height / (float)WoodButtonHeight) - 5);
        int availW = Math.Max(16, bounds.Width - capInset * 2);
        int availH = Math.Max(bounds.Height - 2, bounds.Height >= 34 ? Game1.smallFont.LineSpacing : bounds.Height - 2);
        int iconSize = leadingIcon is not null && HasHudIcons ? Math.Clamp(bounds.Height - 22, 18, 28) : 0;
        int iconGap = iconSize > 0 ? 8 : 0;
        string safeText = GalleryDrawing.SanitizeForFont(Game1.smallFont, text);
        Vector2 textSize = GalleryDrawing.MeasureStringSafe(Game1.smallFont, safeText);
        float scale = GalleryTextFit.Scale(textSize.X, textSize.Y, Math.Max(12, availW - iconSize - iconGap), availH);
        float totalW = iconSize + iconGap + textSize.X * scale;
        float startX = bounds.Center.X - totalW / 2f;
        if (leadingIcon is { } hudIcon && iconSize > 0)
        {
            DrawHudIcon(b, hudIcon, new Rectangle((int)Math.Round(startX), bounds.Center.Y - iconSize / 2, iconSize, iconSize), ink * opacity);
            startX += iconSize + iconGap;
        }
        GalleryDrawing.DrawStringSafe(b, Game1.smallFont, safeText, new Vector2(startX, bounds.Center.Y - textSize.Y * scale / 2f), ink * opacity, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    internal static void DrawPagerArrow(SpriteBatch b, Rectangle bounds, bool next, UiButtonState state)
    {
        Texture2D? texture = Get(PagerArrowsAsset);
        if (texture is null)
        {
            var src = next ? GallerySpreadLayout.PageNextSource : GallerySpreadLayout.PagePrevSource;
            b.Draw(Game1.mouseCursors, bounds, new Rectangle(src.X, src.Y, src.Width, src.Height),
                state == UiButtonState.Disabled ? Color.Gray * 0.45f : Color.White);
            return;
        }
        int cell = (next ? 3 : 0) + (int)state;
        b.Draw(texture, bounds, new Rectangle(cell * PagerCell, 0, PagerCell, PagerCell), Color.White);
    }

    internal static void DrawSearchBox(SpriteBatch b, Rectangle bounds, bool focused)
    {
        Texture2D? texture = Get(SearchBoxAsset);
        if (texture is null)
        {
            IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(383, 373, 18, 18), bounds.X, bounds.Y, bounds.Width, bounds.Height,
                focused ? new Color(255, 240, 190) : Color.White, 4f, false);
            return;
        }
        DrawThreeSlice(b, texture, new Rectangle(0, focused ? SearchHeight : 0, SearchWidth, SearchHeight), bounds, SearchLeftCap, SearchRightCap, Color.White);
    }

    /// <summary>Where search text starts: after the drawn magnifier.</summary>
    internal static Rectangle SearchTextBounds(Rectangle bounds)
    {
        int left = (int)Math.Round(SearchLeftCap * bounds.Height / (float)SearchHeight);
        return new Rectangle(bounds.X + left, bounds.Y, bounds.Width - left - 12, bounds.Height);
    }

    internal static void DrawFilterRow(SpriteBatch b, Rectangle bounds, bool hot)
    {
        Texture2D? texture = Get(FilterRowAsset);
        if (texture is null)
        {
            b.Draw(Game1.staminaRect, bounds, hot ? new Color(255, 240, 165) * 0.78f : new Color(228, 206, 166) * 0.45f);
            return;
        }
        DrawThreeSlice(b, texture, new Rectangle(0, hot ? FilterHeight : 0, FilterWidth, FilterHeight), bounds, FilterLeftCap, FilterRightCap, Color.White);
    }

    internal static int FilterRowArrowWidth(Rectangle bounds) => (int)Math.Round(FilterRightCap * bounds.Height / (float)FilterHeight);

    internal static void DrawClueRow(SpriteBatch b, Rectangle bounds, bool hot)
    {
        Texture2D? texture = Get(ClueRowAsset);
        if (texture is null)
        {
            b.Draw(Game1.staminaRect, bounds, hot ? new Color(244, 214, 132) * 0.7f : new Color(228, 206, 166) * 0.45f);
            return;
        }
        DrawThreeSlice(b, texture, new Rectangle(0, hot ? ClueHeight : 0, ClueWidth, ClueHeight), bounds, ClueCap, ClueCap, Color.White);
    }

    internal static void DrawCheckbox(SpriteBatch b, Rectangle bounds, bool checkedState)
    {
        Texture2D? texture = Get(CheckboxAsset);
        if (texture is null)
        {
            b.Draw(Game1.mouseCursors, bounds, checkedState ? OptionsCheckbox.sourceRectChecked : OptionsCheckbox.sourceRectUnchecked, Color.White);
            return;
        }
        b.Draw(texture, bounds, new Rectangle(checkedState ? CheckboxCell : 0, 0, CheckboxCell, CheckboxCell), Color.White);
    }

    internal static void DrawStoryRow(SpriteBatch b, Rectangle bounds, bool hot)
    {
        Texture2D? texture = Get(StoryRowAsset);
        if (texture is null)
        {
            b.Draw(Game1.staminaRect, bounds, hot ? new Color(255, 240, 165) * 0.72f : new Color(252, 240, 214) * 0.42f);
            return;
        }
        DrawThreeSlice(b, texture, new Rectangle(0, hot ? StoryRowHeight : 0, StoryRowWidth, StoryRowHeight), bounds, StoryRowLeftCap, StoryRowRightCap, Color.White);
    }

    internal static void DrawBadge(SpriteBatch b, UiBadge badge, Rectangle bounds)
    {
        Texture2D? texture = Get(BadgesAsset);
        if (texture is null)
        {
            switch (badge)
            {
                case UiBadge.Star:
                    b.Draw(Game1.mouseCursors, bounds, GalleryBookChrome.ToRect(GallerySpreadLayout.GoldStarSource), Color.White);
                    break;
                case UiBadge.HeartFull:
                    b.Draw(Game1.mouseCursors, bounds, new Rectangle(211, 428, 7, 6), Color.White);
                    break;
                case UiBadge.HeartEmpty:
                    b.Draw(Game1.mouseCursors, bounds, new Rectangle(218, 428, 7, 6), Color.White);
                    break;
                case UiBadge.AvailableToday:
                    GalleryBookChrome.DrawClueBadge(b, bounds, EncounterClue.AvailableToday);
                    break;
                case UiBadge.ClueReady:
                    GalleryBookChrome.DrawClueBadge(b, bounds, EncounterClue.ReadyForTiming);
                    break;
            }
            return;
        }
        b.Draw(texture, bounds, new Rectangle((int)badge * BadgeCell, 0, BadgeCell, BadgeCell), Color.White);
    }

    internal static UiBadge? ClueBadge(EncounterClue clue) => clue switch
    {
        EncounterClue.AvailableToday => UiBadge.AvailableToday,
        EncounterClue.ReadyForTiming => UiBadge.ClueReady,
        _ => null
    };

    /// <summary>Status pill with an optional leading icon; returns the drawn bounds (width fits the text).</summary>
    internal static Rectangle DrawPill(SpriteBatch b, Point origin, int height, string text, UiPill color, UiBadge? icon = null,
        bool alignRight = false, int maxWidth = 260)
    {
        int effectiveHeight = Math.Max(26, height);
        float textScale = Math.Min(0.9f, (effectiveHeight - 1f) / Math.Max(1f, Game1.smallFont.LineSpacing));
        int iconSize = icon is null ? 0 : effectiveHeight - 6;
        int pad = Math.Max(10, effectiveHeight / 2 - 2);
        string safeText = GalleryDrawing.SanitizeForFont(Game1.smallFont, text);
        string fitted = GalleryTextFit.Ellipsize(safeText, Math.Max(20, maxWidth - pad * 2 - iconSize) / textScale, value => GalleryDrawing.MeasureStringSafe(Game1.smallFont, value).X);
        Vector2 size = GalleryDrawing.MeasureStringSafe(Game1.smallFont, fitted) * textScale;
        int width = Math.Min(maxWidth, (int)Math.Ceiling(size.X) + pad * 2 + (iconSize > 0 ? iconSize + 4 : 0));
        Rectangle bounds = new(alignRight ? origin.X - width : origin.X, origin.Y, width, effectiveHeight);
        Texture2D? texture = Get(PillsAsset);
        if (texture is null)
            b.Draw(Game1.staminaRect, bounds, color switch { UiPill.Amber => new Color(232, 168, 60), UiPill.Crimson => new Color(150, 36, 36), _ => new Color(120, 80, 54) });
        else
            DrawThreeSlice(b, texture, new Rectangle(0, (int)color * PillHeight, PillWidth, PillHeight), bounds, PillCap, PillCap, Color.White);
        int x = bounds.X + pad;
        if (icon is { } badge)
        {
            DrawBadge(b, badge, new Rectangle(x - 2, bounds.Y + 3, iconSize, iconSize));
            x += iconSize + 4;
        }
        Color ink = color == UiPill.Amber ? new Color(70, 34, 6) : new Color(255, 236, 200);
        GalleryDrawing.DrawStringSafe(b, Game1.smallFont, fitted, new Vector2(x, bounds.Center.Y - size.Y / 2f), ink, 0f, Vector2.Zero, textScale, SpriteEffects.None, 0f);
        return bounds;
    }

    internal static void DrawHudIcon(SpriteBatch b, UiHudIcon icon, Rectangle bounds, Color color)
    {
        Texture2D? texture = Get(HudIconsAsset);
        if (texture is null)
        {
            if (icon == UiHudIcon.Camera)
                b.Draw(Game1.mouseCursors2, bounds, new Rectangle(72, 31, 18, 16), color);
            return;
        }
        b.Draw(texture, bounds, new Rectangle((int)icon * HudCell, 0, HudCell, HudCell), color);
    }

    internal static bool HasHudIcons => Get(HudIconsAsset) is not null;

    /// <summary>Four gold L-shaped corners around a focused portrait.</summary>
    internal static void DrawFocusCorners(SpriteBatch b, Rectangle bounds, int size = CornerCell)
    {
        Texture2D? texture = Get(FocusCornersAsset);
        if (texture is null)
        {
            GalleryBookChrome.DrawBorder(b, bounds, new Color(225, 172, 52), 3);
            return;
        }
        b.Draw(texture, new Rectangle(bounds.X, bounds.Y, size, size), new Rectangle(0, 0, CornerCell, CornerCell), Color.White);
        b.Draw(texture, new Rectangle(bounds.Right - size, bounds.Y, size, size), new Rectangle(CornerCell, 0, CornerCell, CornerCell), Color.White);
        b.Draw(texture, new Rectangle(bounds.X, bounds.Bottom - size, size, size), new Rectangle(CornerCell * 2, 0, CornerCell, CornerCell), Color.White);
        b.Draw(texture, new Rectangle(bounds.Right - size, bounds.Bottom - size, size, size), new Rectangle(CornerCell * 3, 0, CornerCell, CornerCell), Color.White);
    }

    /// <summary>Red-leather riveted photo frame around a 16:9 photo rectangle.</summary>
    internal static void DrawCardFrame(SpriteBatch b, Rectangle photo, bool focused)
    {
        Texture2D? texture = Get(CardFrameAsset);
        if (texture is null)
        {
            GalleryBookChrome.DrawBorder(b, GalleryDrawing.Inset(photo, -4, -4), focused ? new Color(225, 172, 52) : new Color(160, 110, 60), 3);
            return;
        }
        float kx = photo.Width / 265f, ky = photo.Height / 149f;
        Rectangle dest = new(photo.X - (int)Math.Round(CardFrameInsetX * kx), photo.Y - (int)Math.Round(CardFrameInsetY * ky),
            (int)Math.Round(CardFrameWidth * kx), (int)Math.Round(CardFrameHeight * ky));
        b.Draw(texture, dest, focused ? new Color(255, 248, 210) : Color.White);
        if (focused)
            DrawFocusCorners(b, GalleryDrawing.Inset(dest, -6, -6), 14);
    }

    /// <summary>Diamond-riveted title plaque above a card; text is centred and fitted.</summary>
    internal static void DrawTitlePlaque(SpriteBatch b, Rectangle bounds, string text, bool hot)
    {
        Texture2D? texture = Get(TitlePlaqueAsset);
        if (texture is null)
            b.Draw(Game1.staminaRect, bounds, hot ? new Color(255, 236, 170) : new Color(240, 206, 140));
        else
            DrawThreeSlice(b, texture, new Rectangle(0, 0, TitlePlaqueWidth, TitlePlaqueHeight), bounds, TitlePlaqueCap, TitlePlaqueCap,
                hot ? new Color(255, 250, 225) : Color.White);
        int cap = Math.Max(14, (int)Math.Round(TitlePlaqueCap * bounds.Height / (float)TitlePlaqueHeight) - 4);
        Rectangle textArea = new(bounds.X + cap, bounds.Y, Math.Max(24, bounds.Width - cap * 2), bounds.Height);
        string safeText = GalleryDrawing.SanitizeForFont(Game1.smallFont, text);
        Vector2 rawTitle = GalleryDrawing.MeasureStringSafe(Game1.smallFont, safeText);
        float scale = Math.Min(0.95f, Math.Max(0.76f, Math.Min(textArea.Width / Math.Max(1f, rawTitle.X), (bounds.Height + 2f) / Math.Max(1f, rawTitle.Y))));
        string fitted = GalleryTextFit.Ellipsize(safeText, textArea.Width / scale, value => GalleryDrawing.MeasureStringSafe(Game1.smallFont, value).X);
        Vector2 size = GalleryDrawing.MeasureStringSafe(Game1.smallFont, fitted) * scale;
        GalleryDrawing.DrawStringSafe(b, Game1.smallFont, fitted, new Vector2(textArea.Center.X - size.X / 2f, textArea.Center.Y - size.Y / 2f),
            new Color(70, 38, 14), 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    internal static void DrawFavoriteRibbon(SpriteBatch b, Rectangle photo)
    {
        Texture2D? texture = Get(FavoriteRibbonAsset);
        Rectangle dest = new(photo.Right - 34, photo.Y - 4, 24, 40);
        if (texture is null)
        {
            b.Draw(Game1.mouseCursors, new Rectangle(dest.X, dest.Y + 8, 21, 18), new Rectangle(211, 428, 7, 6), Color.White);
            return;
        }
        b.Draw(texture, dest, Color.White);
    }

    /// <summary>Locked (red seal) or unseen parallel-route (gold seal) envelope art; returns false without art.</summary>
    internal static bool DrawEnvelope(SpriteBatch b, Rectangle bounds, bool parallel)
    {
        Texture2D? texture = Get(parallel ? ParallelEnvelopeAsset : LockedEnvelopeAsset) ?? Get(LockedEnvelopeAsset);
        if (texture is null)
            return false;
        b.Draw(texture, bounds, Color.White);
        return true;
    }

    internal static void DrawTabPlaque(SpriteBatch b, Rectangle bounds, string text, bool active, bool hot)
    {
        Texture2D? texture = Get(TabPlaquesAsset);
        if (active)
            b.Draw(Game1.staminaRect, new Rectangle(bounds.X + 3, bounds.Bottom, bounds.Width - 3, 3), Color.Black * 0.28f);
        if (texture is null)
            b.Draw(Game1.staminaRect, bounds, active ? new Color(248, 214, 150) : new Color(236, 210, 166) * 0.8f);
        else
            DrawThreeSlice(b, texture, new Rectangle(0, active ? TabPlaqueHeight : 0, TabPlaqueWidth, TabPlaqueHeight), bounds, 10, 10,
                active ? Color.White : hot ? new Color(255, 248, 228) : new Color(246, 236, 216));
        Rectangle area = GalleryDrawing.Inset(bounds, 7, 0);
        string safeText = GalleryDrawing.SanitizeForFont(Game1.smallFont, text);
        Vector2 raw = GalleryDrawing.MeasureStringSafe(Game1.smallFont, safeText);
        float naturalScale = Math.Min(active ? 0.96f : 0.9f, Math.Min(area.Width / Math.Max(1f, raw.X), (bounds.Height + 2f) / Math.Max(1f, raw.Y)));
        float scale = Math.Max(0.74f, naturalScale);
        string fitted = GalleryTextFit.Ellipsize(safeText, area.Width / scale, value => GalleryDrawing.MeasureStringSafe(Game1.smallFont, value).X);
        Vector2 size = GalleryDrawing.MeasureStringSafe(Game1.smallFont, fitted) * scale;
        Color ink = active ? new Color(70, 30, 10) : hot ? new Color(110, 66, 30) : new Color(130, 92, 56);
        GalleryDrawing.DrawStringSafe(b, Game1.smallFont, fitted, new Vector2(area.Center.X - size.X / 2f, area.Center.Y - size.Y / 2f), ink, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    /// <summary>Draw the bottom part of one ribbon cell so the swallow-tail is never distorted.</summary>
    internal static void DrawRibbonTail(SpriteBatch b, int tab, bool active, Rectangle tail, Color fallback)
    {
        Texture2D? texture = Get(RibbonsAsset);
        if (texture is null)
        {
            b.Draw(Game1.staminaRect, tail, fallback);
            return;
        }
        int length = Math.Min(RibbonCellHeight, tail.Height);
        Rectangle source = new((tab * 2 + (active ? 1 : 0)) * RibbonCellWidth, RibbonCellHeight - length, RibbonCellWidth, length);
        b.Draw(texture, new Rectangle(tail.X, tail.Bottom - length, tail.Width, length), source, Color.White);
        if (!active)
            b.Draw(Game1.staminaRect, new Rectangle(tail.X + 3, tail.Y, tail.Width - 6, 3), Color.Black * 0.35f);
    }
}
