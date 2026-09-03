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
    Func<string, string?> CheckPrecondition
);

internal interface IEventAssetSourceCatalog
{
    void VisitCurrent(Action<EventAssetSource> visit);
}
