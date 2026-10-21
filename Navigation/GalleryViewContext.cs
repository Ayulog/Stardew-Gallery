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
    /// <summary>Set for Chinese: searches typed in Latin letters also match Chinese names by pinyin.</summary>
    internal PinyinTable? Pinyin { get; init; }
    internal Func<ResolvedEvent, EventSourceDisplay> EventSource { get; init; } = resolved =>
        EventOriginPresentation.Build(resolved, EventOriginMatch.Unknown, (key, args) => I18n.Get(key, args));
    internal GalleryLocationNames Locations { get; } = new(I18n);
    internal GalleryCharacterNames Characters { get; } = new(Catalog, I18n);
    internal string Name(EventIdentity identity) => Names?.Get(identity) ?? identity.EventId;

    private (int NamesRevision, StorySearchIndex Index, float RowScale)? queryIndex;

    /// <summary>
    /// The query page is rebuilt on every visit; keep its index and row text scale for this view
    /// (one catalog and locale), and rebuild them after a custom name changes.
    /// </summary>
    internal (StorySearchIndex Index, float RowScale) QueryIndex(Func<(StorySearchIndex Index, float RowScale)> build)
    {
        int revision = Names?.Revision ?? 0;
        if (queryIndex is { } cached && cached.NamesRevision == revision)
            return (cached.Index, cached.RowScale);
        (StorySearchIndex index, float rowScale) = build();
        queryIndex = (revision, index, rowScale);
        return (index, rowScale);
    }
}
