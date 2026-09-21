using StardewModdingAPI;
using StardewValley;

namespace StardewGallery;

/// <summary>Developer-facing file-origin lookup and optional runtime edit evidence; does not alter gallery permissions.</summary>
internal sealed class EventSourceDiagnostics(EventDefinitionSources definitions, SmapiEventSourceObserver observer, IMonitor monitor)
{
    internal void Run(string command, string[] args)
    {
        if (args.Length == 0 || args is ["status"] || args is ["rescan"])
        {
            if (args is ["rescan"]) definitions.Reset();
            definitions.EnsureScanned();
            monitor.Log(definitions.Status + " " + observer.DiagnosticStatus
                + " Usage: gallery_event_source <location> <event-id> | rescan", LogLevel.Info);
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
            // The observer's Lookup only checks existing evidence for this returned instance/script.
            List<EventSourceInfo> sources = events
                .Where(pair => EventKey.TryGetId(pair.Key, out string id) && id == args[1])
                .Select(pair => observer.Lookup(events, assetName, pair.Key, pair.Value)).ToList();
            if (sources.Count == 0)
            {
                monitor.Log("No current raw key matches event " + args[1] + " in " + assetName, LogLevel.Info);
                return;
            }
            EventOriginMatch origin = definitions.Read(assetName, args[1]);
            monitor.Log($"Original definition: {origin.Provider?.Name ?? origin.Status.ToString()} ({origin.Provider?.UniqueId ?? "unresolved"}); file candidates: {origin.Candidates.Count}", LogLevel.Info);
            foreach (var candidate in origin.Candidates.DistinctBy(candidate => (candidate.Actor.UniqueId, candidate.RelativePath)))
                monitor.Log($"  {candidate.Actor.UniqueId}: {candidate.RelativePath}", LogLevel.Info);
            foreach (EventSourceInfo source in sources)
            {
                string provider = source.Provider is { } actor ? $"{actor.Name} ({actor.UniqueId}, {actor.Version ?? "game"})" : "Unknown";
                monitor.Log($"{assetName} :: {source.RawEventKey}\nProvider: {provider}\nEvidence: {source.Status}; "
                    + $"load {source.LoadId}, definition {source.DefinitionGeneration}; {source.Reason ?? "observed main definition"}", LogLevel.Info);
                foreach (EventSourceMutation mutation in source.Mutations)
                    monitor.Log($"  {mutation.Sequence}: {mutation.Kind} by {mutation.Actor?.UniqueId ?? "Unknown"}"
                        + $" (executor {mutation.Executor?.UniqueId ?? "none"}, generation {mutation.DefinitionGeneration}, failed={mutation.Failed})", LogLevel.Info);
            }
            GalleryDiagnostics.Write("event-source-latest.json", new
            {
                Scope = "Original definition file matches by asset and event ID; runtime changes are supplemental.",
                DefinitionIndex = definitions.Status,
                Origin = origin,
                Observer = observer.DiagnosticStatus,
                Location = location.NameOrUniqueName,
                Sources = sources
            }, monitor);
            monitor.Log("Source evidence written to " + Path.Combine(GalleryDiagnostics.DirectoryPath, "event-source-latest.json"), LogLevel.Info);
        }
        catch (Exception error)
        {
            monitor.Log("Event source query failed: " + error.Message, LogLevel.Warn);
        }
    }
}
