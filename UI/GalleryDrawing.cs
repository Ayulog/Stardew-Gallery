using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace StardewGallery;

internal static class GalleryDrawing
{
    internal const int MenuWidth = GallerySpreadLayout.LogicalWidth;
    internal const int MenuHeight = GallerySpreadLayout.LogicalHeight;
    internal static string TextSeparator => LocalizedContentManager.CurrentLanguageCode == LocalizedContentManager.LanguageCode.ko ? " / " : " · ";

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

    internal static void DrawButton(SpriteBatch b, Rectangle bounds, string text)
    {
        IClickableMenu.drawTextureBox(b, bounds.X, bounds.Y, bounds.Width, bounds.Height, Color.White);
        DrawCentered(b, text, Inset(bounds, 20, 8));
    }

    internal static void DrawScrollbar(SpriteBatch b, Rectangle thumb)
        => b.Draw(Game1.mouseCursors, thumb, new Rectangle(435, 463, 6, 10), Color.White);

    internal static void DrawScrollbarTrack(SpriteBatch b, Texture2D texture, Rectangle bounds)
        => b.Draw(texture, bounds, new Rectangle(0, 0, bounds.Width, bounds.Height), Color.White);

    internal static void DrawCentered(SpriteBatch b, string text, Rectangle bounds, Color? color = null)
    {
        Vector2 size = Game1.smallFont.MeasureString(text);
        float scale = GalleryTextFit.Scale(size.X, size.Y, bounds.Width, bounds.Height);
        b.DrawString(Game1.smallFont, text, new Vector2(bounds.Center.X - size.X * scale / 2, bounds.Center.Y - size.Y * scale / 2),
            color ?? Game1.textColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    internal static void DrawLeftFitted(SpriteBatch b, string text, Rectangle bounds, Color? color = null)
    {
        Vector2 size = Game1.smallFont.MeasureString(text);
        float scale = GalleryTextFit.Scale(size.X, size.Y, bounds.Width, bounds.Height);
        b.DrawString(Game1.smallFont, text, new Vector2(bounds.X, bounds.Center.Y - size.Y * scale / 2),
            color ?? Game1.textColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }
}
