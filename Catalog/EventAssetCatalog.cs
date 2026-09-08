using StardewValley;

namespace StardewGallery;

internal sealed class EventAssetCatalog : IEventAssetSourceCatalog
{
    internal ObservedEventAssets Observed { get; } = new();
    internal List<string> Supplemental { get; } = [];
    internal List<string> Unavailable { get; } = [];
    private readonly NativePreconditionProbe probe = new(Event.SplitPreconditions, ArgUtility.SplitBySpaceQuoteAware);
    public void VisitCurrent(Action<EventAssetSource> visit)
    {
        Supplemental.Clear(); Unavailable.Clear();
        HashSet<string> visited = new(StringComparer.OrdinalIgnoreCase);
        Utility.ForEachLocation(location =>
        {
            try
            {
                if (!location.TryGetLocationEvents(out string assetName, out Dictionary<string, string> events))
                    return true;
                visited.Add(assetName.Replace('\\', '/'));

                List<EventAssetDefinition> definitions = [];
                foreach ((string key, string script) in events)
                    definitions.Add(new EventAssetDefinition(key, script));

                visit(new EventAssetSource(
                    AssetName: assetName,
                    LaunchLocationName: location.NameOrUniqueName,
                    FragmentRootLocationName: location.Name,
                    Definitions: definitions,
                    LoadLocationEvents: name => LoadLocationEvents(name, location, events),
                    ProbePrecondition: key => probe.Check(key, candidate => location.checkEventPrecondition(candidate, check_seen: false), Game1.dedicatedServer is not null)
                ));
                return true;
            }
            catch (Exception error)
            {
                Unavailable.Add(location.NameOrUniqueName + ": " + error.GetType().Name);
                return true;
            }
        }, includeInteriors: true, includeGenerated: false);
        // AssetReady may add names while loading fragments. Drain the observed set on this normal read path.
        while (Observed.Names.FirstOrDefault(name => !visited.Contains(name)) is { } assetName)
        {
            visited.Add(assetName);
            try
            {
                if (!Game1.content.DoesAssetExist<Dictionary<string, string>>(assetName))
                {
                    Unavailable.Add(assetName);
                    continue;
                }
                Dictionary<string, string> events = Game1.content.Load<Dictionary<string, string>>(assetName);
                string root = assetName["Data/Events/".Length..];
                visit(new EventAssetSource(assetName, "", root,
                    events.Select(pair => new EventAssetDefinition(pair.Key, pair.Value)).ToArray(),
                    name => name.Equals(root, StringComparison.OrdinalIgnoreCase) ? events : LoadSupplementalFragments(name),
                    _ => new NativePreconditionProbeResult(NativePreconditionProbeStatus.NotSafelyEvaluated, "missing-location-context"))
                    { HasLocationContext = false });
                Supplemental.Add(assetName);
            }
            catch (Exception error)
            {
                Unavailable.Add(assetName + ": " + error.GetType().Name);
            }
        }
    }

    private static IReadOnlyDictionary<string, string>? LoadSupplementalFragments(string locationName)
    {
        string? assetName = ObservedEventAssets.Normalize("Data/Events/" + locationName);
        return assetName is not null && Game1.content.DoesAssetExist<Dictionary<string, string>>(assetName)
            ? Game1.content.Load<Dictionary<string, string>>(assetName) : null;
    }

    private static IReadOnlyDictionary<string, string>? LoadLocationEvents(
        string locationName,
        GameLocation rootLocation,
        IReadOnlyDictionary<string, string> rootEvents)
    {
        if (locationName.Equals(rootLocation.Name, StringComparison.OrdinalIgnoreCase)
            || locationName.Equals(rootLocation.NameOrUniqueName, StringComparison.OrdinalIgnoreCase))
            return rootEvents;

        GameLocation? location = Game1.getLocationFromName(locationName);
        string assetName = "Data\\Events\\" + (location?.Name ?? locationName);
        return Game1.content.DoesAssetExist<Dictionary<string, string>>(assetName)
            ? Game1.content.Load<Dictionary<string, string>>(assetName)
            : null;
    }
}
