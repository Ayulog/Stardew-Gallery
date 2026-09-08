namespace StardewGallery;

internal sealed record EventAssetDefinition(
    string RawEventKey,
    string Script
);

internal sealed record EventAssetSource(
    string AssetName,
    string LaunchLocationName,
    string FragmentRootLocationName,
    IReadOnlyList<EventAssetDefinition> Definitions,
    Func<string, IReadOnlyDictionary<string, string>?> LoadLocationEvents,
    Func<string, NativePreconditionProbeResult> ProbePrecondition
)
{
    public bool HasLocationContext { get; init; } = true;
}

internal interface IEventAssetSourceCatalog
{
    void VisitCurrent(Action<EventAssetSource> visit);
}
