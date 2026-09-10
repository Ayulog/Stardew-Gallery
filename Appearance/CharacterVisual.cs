using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace StardewGallery.Appearance;

internal enum CharacterVisual { Portrait, Sprite }

internal interface ICharacterAppearance
{
    // Preparation may load content; drawing only consumes prepared resources.
    void Prepare(string name, CharacterVisual visual);
    void Draw(SpriteBatch batch, string name, CharacterVisual visual, Rectangle bounds, Color tint);
}
