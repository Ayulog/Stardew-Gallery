using StardewValley;

namespace StardewGallery;

internal sealed class EventAssetCatalog : IEventAssetSourceCatalog
{
    public void VisitCurrent(Action<EventAssetSource> visit)
    {
        Utility.ForEachLocation(location =>
        {
            if (!location.TryGetLocationEvents(out string assetName, out Dictionary<string, string> events))
                return true;

            List<EventAssetDefinition> definitions = [];
            foreach ((string key, string script) in events)
                definitions.Add(new EventAssetDefinition(key, script));

            visit(new EventAssetSource(
                AssetName: assetName,
                LaunchLocationName: location.NameOrUniqueName,
                FragmentRootLocationName: location.Name,
                Definitions: definitions,
                LoadLocationEvents: name => LoadLocationEvents(name, location, events),
                CheckPrecondition: key => location.checkEventPrecondition(key, check_seen: false)
            ));
            return true;
        }, includeInteriors: true, includeGenerated: false);
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
