namespace StardewGallery;

internal enum StoryKind { Heart, Ordinary, Internal }

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
    public StoryKind Kind { get; init; } = StoryKind.Ordinary;
    public IReadOnlyList<string> RelatedNpcNames { get; init; } = Ownership.Owners.Select(owner => owner.Name).ToArray();
    public string? ClassificationReason { get; init; }
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
)
{
    internal IEnumerable<GalleryEvent> AllEntries => Events.Concat(ExcludedEvents);
    internal IEnumerable<GalleryEvent> StoryEntries => AllEntries.Where(entry => entry.Kind != StoryKind.Internal);
    internal GalleryEvent? Find(EventIdentity identity) => AllEntries.FirstOrDefault(entry => entry.Resolved.Identity == identity);
    internal IReadOnlyList<GalleryEvent> HeartEventsFor(string name) => Events.Where(entry => entry.Kind == StoryKind.Heart
        && entry.Ownership.Owners.Any(owner => owner.Name == name)).OrderBy(entry => entry.Ownership.Owners.First(owner => owner.Name == name).FriendshipPoints ?? int.MaxValue)
        .ThenBy(entry => entry.EventId, StringComparer.Ordinal).ToArray();
}
