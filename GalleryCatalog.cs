namespace StardewGallery;

internal sealed record GalleryCharacter(
    string Name,
    string DisplayName,
    bool IsMet,
    int FriendshipPoints
)
{
    public bool IsSocial { get; init; } = true;
}

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

    private IReadOnlyList<string>? relatedNpcNames;
    public IReadOnlyList<string> RelatedNpcNames
    {
        get => relatedNpcNames ?? Ownership.Owners.Select(owner => owner.Name).ToArray();
        init => relatedNpcNames = value;
    }
    private IReadOnlyList<string>? heartNpcNames;
    public IReadOnlyList<string> HeartNpcNames
    {
        get => heartNpcNames ?? Ownership.Owners.Where(owner => owner.FriendshipPoints > 0).Select(owner => owner.Name).ToArray();
        init => heartNpcNames = value;
    }

    public bool IsFlow { get; init; }
    public bool OrdinaryReplaySupported { get; init; }
    public string? ReplayUnavailableReason { get; init; }
    public string? ReplayDiagnostic { get; init; }
}

internal sealed record GalleryCatalog(
    IReadOnlyList<GalleryCharacter> Characters,
    IReadOnlyList<GalleryEvent> Events,
    IReadOnlyList<GalleryEvent> ExcludedEvents
)
{
    public IReadOnlyList<GalleryEventGroup> Groups { get; init; } = [];
}
