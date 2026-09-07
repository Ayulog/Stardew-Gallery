using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace StardewGallery;

internal static class GallerySpreadDrawing
{
    internal static readonly Color Ink = new(111, 79, 49);
    internal static readonly Color LightInk = new(181, 143, 94);

    internal static void DrawScrollbar(SpriteBatch b, Rectangle thumb)
    {
        Rectangle body = new(thumb.Center.X - 7, thumb.Y, 14, thumb.Height);
        b.Draw(Game1.staminaRect, body, Ink);
        b.Draw(Game1.staminaRect, new Rectangle(body.X + 2, body.Y + 2, body.Width - 4, body.Height - 4), LightInk);
        b.Draw(Game1.staminaRect, new Rectangle(body.X + 3, body.Y + 3, 2, body.Height - 6), new Color(255, 227, 175));
        for (int y = body.Center.Y - 4; y <= body.Center.Y + 4; y += 4)
            b.Draw(Game1.staminaRect, new Rectangle(body.X + 4, y, body.Width - 8, 1), Ink);
    }

    internal static void DrawFooterTab(SpriteBatch b, Rectangle bounds, string text, bool highlighted, Texture2D? glyph = null)
    {
        b.Draw(Game1.staminaRect, bounds, new Color(255, 239, 204) * (highlighted ? .8f : .35f));
        if (highlighted)
            b.Draw(Game1.staminaRect, new Rectangle(bounds.X, bounds.Bottom - 2, bounds.Width, 2), LightInk);

        const int glyphSize = GallerySpreadLayout.IconSize * 2;
        int glyphSpace = glyph is null ? 0 : glyphSize + 8;
        Vector2 size = Game1.smallFont.MeasureString(text);
        float scale = Math.Min(1f, Math.Min((bounds.Width - 24 - glyphSpace) / Math.Max(1f, size.X), (bounds.Height - 8) / Math.Max(1f, size.Y)));
        float x = bounds.Center.X - (size.X * scale + glyphSpace) / 2f;
        if (glyph is not null)
        {
            var source = GallerySpreadLayout.ReplayGlyphSource;
            b.Draw(glyph, new Rectangle((int)x, bounds.Center.Y - glyphSize / 2, glyphSize, glyphSize),
                new Rectangle(source.X, source.Y, source.Width, source.Height), Color.White);
        }
        b.DrawString(Game1.smallFont, text, new Vector2(x + glyphSpace, bounds.Center.Y - size.Y * scale / 2f), Ink,
            0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }
}
