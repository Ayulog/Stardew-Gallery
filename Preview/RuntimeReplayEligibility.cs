using StardewValley;

namespace StardewGallery;

internal static class RuntimeReplayEligibility
{
    private static readonly OrdinaryReplayPolicy Policy = new(raw => Event.ParseCommands(raw), ArgUtility.SplitBySpaceQuoteAware);

    internal static GalleryCatalog Analyze(GalleryCatalog catalog)
    {
        // The default constructor only initializes the native command registry; it does not run a scene.
        _ = new Event();
        Dictionary<string, GameLocation> locations = ExistingLocations();
        GalleryEvent Analyze(GalleryEvent entry)
        {
            ReplayCompatibility compatibility;
            try
            {
                compatibility = OrdinaryReplayPolicy.Context(entry.Resolved);
                if (compatibility.Supported && entry.Ownership.Kind == OwnershipKind.Excluded)
                    compatibility = Policy.Check(entry.Resolved, NativeCommand, locations.ContainsKey, ActorAvailable);
            }
            catch (Exception error) { compatibility = new("replay.script-unsupported", error.GetType().Name); }
            return entry with { OrdinaryReplaySupported = entry.Ownership.Kind == OwnershipKind.Excluded && compatibility.Supported,
                ReplayUnavailableReason = compatibility.ReasonKey, ReplayDiagnostic = compatibility.Detail };
        }
        var events = catalog.Events.Select(Analyze).ToArray();
        var other = catalog.ExcludedEvents.Select(Analyze).ToArray();
        return catalog with { Events = events, ExcludedEvents = other, Groups = GalleryEventGroup.Build(events.Concat(other)) };
    }

    internal static ReplayCompatibility CheckNow(GalleryEvent entry)
    {
        try { return CheckCurrent(entry); }
        catch (Exception error) { return new("replay.content-changed", error.GetType().Name); }
    }

    private static ReplayCompatibility CheckCurrent(GalleryEvent entry)
    {
        ReplayCompatibility context = OrdinaryReplayPolicy.Context(entry.Resolved);
        if (!context.Supported) return context;
        Dictionary<string, GameLocation> locations = ExistingLocations();
        if (!locations.ContainsKey(entry.LocationName)) return new("replay.context-missing");
        if (entry.Ownership.Kind == OwnershipKind.Excluded)
        {
            GameLocation location = locations[entry.LocationName];
            if (!location.TryGetLocationEvents(out string asset, out Dictionary<string, string> events)
                || new EventIdentity(asset, entry.EventId) != entry.Resolved.Identity
                || !events.TryGetValue(entry.EventKey, out string? script) || script != entry.Script)
                return new("replay.content-changed");
            EventFragments fragments = EventFragmentCollector.Collect(script, location.Name,
                name =>
                {
                    string? key = ObservedEventAssets.Normalize("Data/Events/" + name);
                    return key is not null && Game1.content.DoesAssetExist<Dictionary<string, string>>(key)
                        ? Game1.content.Load<Dictionary<string, string>>(key) : null;
                }, raw => Event.ParseCommands(raw), ArgUtility.SplitBySpaceQuoteAware, key => Game1.content.LoadStringReturnNullIfNotFound(key));
            if (fragments.MissingKeys.Count > 0 || !fragments.Scripts.SequenceEqual(entry.Fragments.Scripts))
                return new("replay.content-changed");
        }
        return entry.Ownership.Kind == OwnershipKind.Excluded
            ? Policy.Check(entry.Resolved, NativeCommand, locations.ContainsKey, ActorAvailable) : new(null);
    }

    internal static Dictionary<string, GameLocation> ExistingLocations()
    {
        Dictionary<string, GameLocation> locations = new(StringComparer.OrdinalIgnoreCase);
        Utility.ForEachLocation(location => { locations.TryAdd(location.NameOrUniqueName, location); return true; }, includeInteriors: true, includeGenerated: false);
        return locations;
    }
    private static bool NativeCommand(string name) => Event.TryGetEventCommandHandler(name, out var handler)
        && handler.GetInvocationList().Length == 1 && handler.Method.DeclaringType == typeof(Event.DefaultCommands);
    private static bool ActorAvailable(string name)
    {
        if (name is "cat" or "dog" or "pet") return true;
        string? target = name == "spouse" ? Game1.player.spouse : name;
        return !string.IsNullOrWhiteSpace(target) && Game1.getCharacterFromName(target) is not null;
    }
}
