using StardewValley;

namespace StardewGallery;

internal sealed class EventAssetCatalog : IEventAssetSourceCatalog
{
    private readonly NativePreconditionProbe probe = new(Event.SplitPreconditions, ArgUtility.SplitBySpaceQuoteAware);

    /// <summary>Town shows Pam's upgraded house, whose door leads to Trailer_Big, once the host has this mail (Town.MakeMapModifications).</summary>
    internal static bool PamHouseUpgraded => Game1.MasterPlayer?.mailReceived.Contains("pamHouseUpgrade") == true;

    public void VisitCurrent(Action<EventAssetSource> visit)
    {
        var farmhouseEvents = Game1.content.Load<Dictionary<string, string>>("Data/Events/FarmHouse");
        IReadOnlySet<string> trailerCopies = NativeGlobalEventCopies.TrailerCopyKeys(
            LoadEvents(NativeGlobalEventCopies.TrailerAsset), LoadEvents(NativeGlobalEventCopies.TrailerBigAsset));
        bool upgraded = PamHouseUpgraded;
        Utility.ForEachLocation(location =>
        {
            if (!location.TryGetLocationEvents(out string assetName, out Dictionary<string, string> events))
                return true;

            List<EventAssetDefinition> definitions = [], trailerDefinitions = [];
            foreach ((string key, string script) in events)
            {
                if (NativeGlobalEventCopies.IsCopy(assetName, key, script, farmhouseEvents))
                    continue;
                switch (NativeGlobalEventCopies.PlaceTrailerEvent(location.Name, key, trailerCopies, upgraded))
                {
                    case TrailerEventPlacement.Listed: definitions.Add(new EventAssetDefinition(key, script)); break;
                    case TrailerEventPlacement.ListedAsTrailer: trailerDefinitions.Add(new EventAssetDefinition(key, script)); break;
                }
            }

            visit(Source(assetName, location, definitions, events));
            // Trailer_Big plays its merged copies with rewritten scripts, but they keep the Trailer identity.
            if (trailerDefinitions.Count > 0)
                visit(Source(NativeGlobalEventCopies.TrailerAsset, location, trailerDefinitions, events));
            return true;
        }, includeInteriors: true, includeGenerated: false);
    }

    private EventAssetSource Source(string assetName, GameLocation location, IReadOnlyList<EventAssetDefinition> definitions, Dictionary<string, string> events)
        => new(
            AssetName: assetName,
            LaunchLocationName: location.NameOrUniqueName,
            FragmentRootLocationName: location.Name,
            Definitions: definitions,
            LoadLocationEvents: name => LoadLocationEvents(name, location, events),
            ProbePrecondition: key => probe.Check(key, candidate => location.checkEventPrecondition(candidate, check_seen: false), Game1.dedicatedServer is not null)
        );

    private static Dictionary<string, string>? LoadEvents(string assetName)
        => Game1.content.DoesAssetExist<Dictionary<string, string>>(assetName) ? Game1.content.Load<Dictionary<string, string>>(assetName) : null;

    private static IReadOnlyDictionary<string, string>? LoadLocationEvents(
        string locationName,
        GameLocation rootLocation,
        IReadOnlyDictionary<string, string> rootEvents)
    {
        if (locationName.Equals(rootLocation.Name, StringComparison.OrdinalIgnoreCase)
            || locationName.Equals(rootLocation.NameOrUniqueName, StringComparison.OrdinalIgnoreCase))
            return rootEvents;

        GameLocation? location = Game1.getLocationFromName(locationName);
        return LoadEvents("Data\\Events\\" + (location?.Name ?? locationName));
    }
}
