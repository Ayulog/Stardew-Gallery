namespace StardewGallery;

internal sealed record GalleryCharacter(
    string Name,
    string DisplayName,
    bool IsMet,
    int FriendshipPoints
);

internal sealed record GalleryEvent(
    string Identity,
    string LocationName,
    string AssetName,
    string EventId,
    string EventKey,
    string Script,
    EventFragments Fragments,
    EventOwnership Ownership
);

internal sealed record GalleryCatalog(
    IReadOnlyList<GalleryCharacter> Characters,
    IReadOnlyList<GalleryEvent> Events,
    IReadOnlyList<GalleryEvent> ExcludedEvents
);
