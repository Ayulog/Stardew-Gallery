using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;

namespace StardewGallery;

internal static class GalleryDrawing
{
    private sealed class FontCharacterCache(int count, HashSet<char> chars)
    {
        internal int Count { get; } = count;
        internal HashSet<char> Chars { get; } = chars;
    }

    private static readonly ConditionalWeakTable<SpriteFont, FontCharacterCache> FontCharCaches = new();

    internal const int MenuWidth = GallerySpreadLayout.LogicalWidth;
    internal const int MenuHeight = GallerySpreadLayout.LogicalHeight;
    internal static string TextSeparator => LocalizedContentManager.CurrentLanguageCode == LocalizedContentManager.LanguageCode.ko ? " / " : " · ";

    internal static string SanitizeForFont(SpriteFont? font, string? text)
    {
        if (string.IsNullOrEmpty(text))
            return text ?? string.Empty;
        if (font is null || font.DefaultCharacter is not null || font.Characters is not { Count: > 0 } chars)
            return text;

        FontCharacterCache cache = FontCharCaches.GetValue(font, static f =>
        {
            IReadOnlyCollection<char>? fontChars = f.Characters;
            return fontChars is null
                ? new FontCharacterCache(0, [])
                : new FontCharacterCache(fontChars.Count, new HashSet<char>(fontChars));
        });
        if (cache.Count != chars.Count)
        {
            cache = new FontCharacterCache(chars.Count, new HashSet<char>(chars));
            FontCharCaches.AddOrUpdate(font, cache);
        }
        return cache.Chars.Count == 0
            ? text
            : GalleryTextFit.SanitizeForCharacterSet(text, font.DefaultCharacter, cache.Chars.Contains);
    }

    internal static Vector2 MeasureStringSafe(SpriteFont font, string? text)
        => font.MeasureString(SanitizeForFont(font, text));

    internal static string ParseTextSafe(string? text, SpriteFont font, int width)
        => Game1.parseText(SanitizeForFont(font, text), font, width);

    internal static void DrawStringSafe(SpriteBatch b, SpriteFont font, string? text, Vector2 position, Color color)
        => b.DrawString(font, SanitizeForFont(font, text), position, color);

    internal static void DrawStringSafe(
        SpriteBatch b,
        SpriteFont font,
        string? text,
        Vector2 position,
        Color color,
        float rotation,
        Vector2 origin,
        float scale,
        SpriteEffects effects,
        float layerDepth)
        => b.DrawString(font, SanitizeForFont(font, text), position, color, rotation, origin, scale, effects, layerDepth);

    internal static void DrawHoverTextSafe(SpriteBatch b, string? text, SpriteFont font)
        => IClickableMenu.drawHoverText(b, SanitizeForFont(font, text), font);

    internal static Rectangle Inset(Rectangle bounds, int horizontal = 12, int vertical = 0)
        => new(bounds.X + horizontal, bounds.Y + vertical, Math.Max(0, bounds.Width - horizontal * 2), Math.Max(0, bounds.Height - vertical * 2));

    internal static Rectangle ScaleRectangle(Rectangle bounds, float scale, int offsetX, int offsetY) => new(
        offsetX + (int)Math.Round(bounds.X * scale), offsetY + (int)Math.Round(bounds.Y * scale),
        (int)Math.Round(bounds.Width * scale), (int)Math.Round(bounds.Height * scale));

    internal static void BeginScaled(SpriteBatch b, float scale, int offsetX, int offsetY)
    {
        b.End();
        Matrix transform = Matrix.CreateScale(scale) * Matrix.CreateTranslation(offsetX, offsetY, 0f);
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, transform);
    }

    internal static void EndScaled(SpriteBatch b)
    {
        b.End();
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
    }

    /// <summary>
    /// Draws a single-line <see cref="SpriteText"/> title centered inside <paramref name="bounds"/> without hitting
    /// <see cref="SpriteText.drawString"/>'s <c>x + width &gt; Viewport.Width</c> forced line-wrap bug on right-page
    /// plaques at high UI zoom: translates the <see cref="SpriteBatch"/> matrix to the title origin and renders at
    /// <c>(x: 0, y: 0, width: 999999)</c>, then restores the caller's <see cref="BeginScaled"/> transform.
    /// </summary>
    internal static void DrawBookTitle(
        SpriteBatch b,
        string title,
        Rectangle bounds,
        float menuScale,
        int offsetX,
        int offsetY,
        Color? color = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            return;

        int maxDeviceWidth = Math.Max(640, (Game1.graphics?.GraphicsDevice?.Viewport.Width ?? 1920) - 32);
        string text = SpriteText.getWidthOfString(title, 999999) <= maxDeviceWidth
            ? title
            : GalleryTextFit.Ellipsize(title, maxDeviceWidth, s => SpriteText.getWidthOfString(s, 999999));

        int rawWidth = Math.Max(1, SpriteText.getWidthOfString(text, 999999));
        int rawHeight = Math.Max(1, SpriteText.getHeightOfString(text, 999999));
        float availW = Math.Max(24f, bounds.Width - 16f);
        float availH = Math.Max(16f, bounds.Height - 4f);
        float titleScale = Math.Min(1f, Math.Min(availW / rawWidth, availH / rawHeight));
        float logicalX = bounds.X + (bounds.Width - rawWidth * titleScale) / 2f;
        float logicalY = bounds.Y + (bounds.Height - rawHeight * titleScale) / 2f;

        b.End();
        Matrix titleMatrix = Matrix.CreateScale(titleScale)
            * Matrix.CreateTranslation(logicalX, logicalY, 0f)
            * Matrix.CreateScale(menuScale)
            * Matrix.CreateTranslation(offsetX, offsetY, 0f);
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, titleMatrix);
        SpriteText.drawString(b, text, 0, 0, width: 999999, color: color);
        BeginScaled(b, menuScale, offsetX, offsetY);
    }

    internal static void DrawButton(SpriteBatch b, Rectangle bounds, string text, float opacity = 1f)
        => GalleryUiSkin.DrawWoodButton(b, bounds, text, UiButtonState.Normal, opacity);

    internal static void DrawButton(SpriteBatch b, Rectangle bounds, string text, bool hot, bool enabled = true, float opacity = 1f)
        => GalleryUiSkin.DrawWoodButton(b, bounds, text, !enabled ? UiButtonState.Disabled : hot ? UiButtonState.Hover : UiButtonState.Normal, opacity);

    internal static void DrawScrollbar(SpriteBatch b, Rectangle thumb)
        => b.Draw(Game1.mouseCursors, thumb, new Rectangle(435, 463, 6, 10), Color.White);

    internal static void DrawScrollbarTrack(SpriteBatch b, Texture2D texture, Rectangle bounds)
        => b.Draw(texture, bounds, new Rectangle(0, 0, bounds.Width, bounds.Height), Color.White);

    internal static void DrawCentered(SpriteBatch b, string text, Rectangle bounds, Color? color = null)
    {
        string safe = SanitizeForFont(Game1.smallFont, text);
        Vector2 size = Game1.smallFont.MeasureString(safe);
        float scale = GalleryTextFit.Scale(size.X, size.Y, bounds.Width, bounds.Height);
        b.DrawString(Game1.smallFont, safe, new Vector2(bounds.Center.X - size.X * scale / 2, bounds.Center.Y - size.Y * scale / 2),
            color ?? Game1.textColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    internal static void DrawLeftFitted(SpriteBatch b, string text, Rectangle bounds, Color? color = null)
    {
        string safe = SanitizeForFont(Game1.smallFont, text);
        Vector2 size = Game1.smallFont.MeasureString(safe);
        float scale = GalleryTextFit.Scale(size.X, size.Y, bounds.Width, bounds.Height);
        b.DrawString(Game1.smallFont, safe, new Vector2(bounds.X, bounds.Center.Y - size.Y * scale / 2),
            color ?? Game1.textColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    internal static void DrawEllipsized(SpriteBatch b, string text, Rectangle bounds, float scale)
    {
        string safe = SanitizeForFont(Game1.smallFont, text);
        string label = GalleryTextFit.Ellipsize(safe, bounds.Width / scale, value => Game1.smallFont.MeasureString(value).X);
        Vector2 size = Game1.smallFont.MeasureString(label) * scale;
        b.DrawString(Game1.smallFont, label, new Vector2(bounds.X, bounds.Center.Y - size.Y / 2), Game1.textColor,
            0, Vector2.Zero, scale, SpriteEffects.None, 0);
    }
}
