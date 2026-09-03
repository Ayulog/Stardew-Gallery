namespace StardewGallery;

internal sealed record EventPlayback(
    EventIdentity Identity,
    string LocationName,
    string RootScript
)
{
    internal string AssetName => Identity.AssetName;

    internal string EventId => Identity.EventId;

    internal static EventPlayback ForCurrent(ResolvedEvent resolved)
        => new(resolved.Identity, resolved.LocationName, resolved.ResolvedScript);

    internal static EventPlayback ForHistorical(WatchedEventSnapshot snapshot)
        => new(snapshot.Identity, snapshot.LocationName, snapshot.RootScript);
}
