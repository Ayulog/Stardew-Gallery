using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewGallery.Appearance;

namespace StardewGallery;

internal sealed record GalleryTextures(Texture2D Home, Texture2D Album, Texture2D Detail, Texture2D Scene,
    Texture2D Thumbnail, Texture2D Replay, Texture2D SlotFrame, Texture2D Scrollbar, Texture2D ConditionIcons);

internal sealed record GalleryViewContext(GalleryCatalog Catalog, ITranslationHelper I18n, GalleryTextures Textures,
    GalleryPhotos Photos, IGalleryNavigation Navigation, Func<bool> IsUnlocked, Func<GalleryEvent, ReplayAccess> ReplayAccess)
{
    internal ICharacterAppearance Appearance { get; init; } = null!;
    internal EventNameStore? Names { get; init; }
    internal GalleryLocationNames Locations { get; } = new(I18n);
    internal GalleryCharacterNames Characters { get; } = new(Catalog, I18n);
    internal string Name(EventIdentity identity) => Names?.Get(identity) ?? identity.EventId;
}
