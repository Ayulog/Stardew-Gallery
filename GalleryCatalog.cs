namespace StardewGallery;

internal sealed record GalleryCharacter(
    string Name,
    string DisplayName,
    bool IsMet,
    int FriendshipPoints
);

internal sealed record GalleryEvent(
    ResolvedEvent Resolved,
    EventOwnership Ownership
)
{
    public string Identity => Resolved.Identity.StorageKey;

    public string LocationName => Resolved.LocationName;

    public string AssetName => Resolved.AssetName;

    public string EventId => Resolved.EventId;

    public string EventKey => Resolved.RawEventKey;

    public string Script => Resolved.ResolvedScript;

    public EventFragments Fragments => Resolved.Fragments;
}

internal sealed record GalleryCatalog(
    IReadOnlyList<GalleryCharacter> Characters,
    IReadOnlyList<GalleryEvent> Events,
    IReadOnlyList<GalleryEvent> ExcludedEvents
);
