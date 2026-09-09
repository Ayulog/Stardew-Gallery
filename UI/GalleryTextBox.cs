using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace StardewGallery;

internal sealed class GalleryTextBox : TextBox
{
    private readonly Texture2D texture;
    internal GalleryTextBox() : base(Game1.content.Load<Texture2D>("LooseSprites\\textBox"), null, Game1.smallFont, Game1.textColor)
        => texture = Game1.content.Load<Texture2D>("LooseSprites\\textBox");

    public override void Draw(SpriteBatch b, bool drawShadow = true)
    {
        int[] sx = [0, 16, texture.Width - 16, texture.Width], sy = [0, 8, texture.Height - 8, texture.Height];
        int[] dx = [X, X + 16, X + Width - 16, X + Width], dy = [Y, Y + 8, Y + Height - 8, Y + Height];
        for (int row = 0; row < 3; row++)
        for (int col = 0; col < 3; col++)
            b.Draw(texture, new Rectangle(dx[col], dy[row], dx[col + 1] - dx[col], dy[row + 1] - dy[row]),
                new Rectangle(sx[col], sy[row], sx[col + 1] - sx[col], sy[row + 1] - sy[row]), Color.White);
        string text = Text;
        float scale = Math.Min(1f, (Height - 20f) / Math.Max(1f, Game1.smallFont.MeasureString(text.Length == 0 ? "Ag" : text).Y));
        float available = (Width - 40f) / scale;
        if (Game1.smallFont.MeasureString(text).X > available)
        {
            int[] starts = StringInfo.ParseCombiningCharacters(text);
            int low = 0, high = starts.Length;
            while (low < high)
            {
                int middle = (low + high) / 2;
                if (Game1.smallFont.MeasureString(text[starts[middle]..]).X <= available) high = middle; else low = middle + 1;
            }
            text = low == starts.Length ? "" : text[starts[low]..];
        }
        Vector2 size = Game1.smallFont.MeasureString(text) * scale;
        b.DrawString(Game1.smallFont, text, new Vector2(X + 16, Y + (Height - size.Y) / 2), Game1.textColor,
            0, Vector2.Zero, scale, SpriteEffects.None, 0);
        if (Selected && Game1.currentGameTime.TotalGameTime.TotalMilliseconds % 1000 >= 500)
            b.Draw(Game1.staminaRect, new Rectangle(X + 18 + (int)size.X, Y + 10, 3, Height - 20), Game1.textColor);
    }
}
