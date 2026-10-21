using StardewModdingAPI;
using StardewValley;

namespace StardewGallery;

/// <summary>Developer-facing file-origin lookup; does not alter gallery permissions.</summary>
internal sealed class EventSourceDiagnostics(EventDefinitionSources definitions, IMonitor monitor, Action rescanned)
{
    internal void Run(string command, string[] args)
    {
        if (args.Length == 0 || args is ["status"] || args is ["rescan"])
        {
            if (args is ["rescan"])
            {
                definitions.Reset(full: true);
                rescanned();
            }
            definitions.EnsureScanned();
            monitor.Log(definitions.Status + " Usage: gallery_event_source <location> <event-id> | rescan", LogLevel.Info);
            return;
        }
        if (args.Length != 2 || !Context.IsWorldReady)
        {
            monitor.Log("Load a test save, then use: gallery_event_source <location> <event-id>", LogLevel.Info);
            return;
        }
        try
        {
            GameLocation? location = Game1.getLocationFromName(args[0]);
            if (location is null || !location.TryGetLocationEvents(out string assetName, out Dictionary<string, string> events))
            {
                monitor.Log("No current location event dictionary was found for " + args[0], LogLevel.Info);
                return;
            }
            // Use the game's normal resolved content, including its native transformations.
            string[] keys = events.Keys.Where(key => EventKey.TryGetId(key, out string id) && id == args[1]).ToArray();
            if (keys.Length == 0)
            {
                monitor.Log("No current raw key matches event " + args[1] + " in " + assetName, LogLevel.Info);
                return;
            }
            EventOriginMatch origin = definitions.Read(assetName, args[1]);
            monitor.Log($"Original definition: {origin.Provider?.Name ?? origin.Status.ToString()} ({origin.Provider?.UniqueId ?? "unresolved"}); file candidates: {origin.Candidates.Count}", LogLevel.Info);
            foreach (var candidate in origin.Candidates.DistinctBy(candidate => (candidate.Actor.UniqueId, candidate.RelativePath)))
                monitor.Log($"  {candidate.Actor.UniqueId}: {candidate.RelativePath}", LogLevel.Info);
            foreach (string key in keys)
                monitor.Log($"{assetName} :: {key}", LogLevel.Info);
            GalleryDiagnostics.Write("event-source-latest.json", new
            {
                Scope = "Original definition file matches by asset and event ID.",
                DefinitionIndex = definitions.Status,
                Origin = origin,
                Location = location.NameOrUniqueName,
                Keys = keys
            }, monitor);
            monitor.Log("Source evidence written to " + Path.Combine(GalleryDiagnostics.DirectoryPath, "event-source-latest.json"), LogLevel.Info);
        }
        catch (Exception error)
        {
            monitor.Log("Event source query failed: " + error.Message, LogLevel.Warn);
        }
    }
}
