namespace StardewGallery;

/// <summary>Original definition matching is independent of optional current-load edit evidence.</summary>
internal static class EventOriginPresentation
{
    internal static EventSourceDisplay Build(ResolvedEvent resolved, EventOriginMatch origin,
        EventSourceInfo? observed, EventSourceDisplayState tracingState, Func<string, object?, string> translate)
    {
        string Text(string key, object? args = null) => translate(key, args);
        string Actor(EventSourceActor actor) => actor.IsGameBase ? Text("source.game")
            : Text("source.actor", new { name = actor.Name, id = actor.UniqueId, version = actor.Version ?? Text("source.version-unknown") });
        EventSourceDisplay runtime = EventSourcePresentation.Build(observed, tracingState, translate);
        string provider = origin.Provider is { } owner ? owner.IsGameBase ? Text("source.game") : owner.Name
            : Text(origin.Ambiguous ? "source.origin-ambiguous" : "source.unknown");
        string status = Text(origin.Provider is { IsGameBase: true } ? "source.origin-vanilla"
            : origin.Provider is not null ? "source.origin-matched"
            : origin.Ambiguous ? "source.origin-ambiguous" : "source.origin-missing");
        List<string> details = [Text("source.origin-provider", new { provider = origin.Provider is { } actor ? Actor(actor) : provider }), status];
        details.Add(Text(origin.Ambiguous ? "source.origin-ambiguous-help"
            : origin.Provider is null ? "source.origin-unknown-help" : "source.origin-help"));
        if (origin.Candidates.Count > 0)
        {
            details.Add(Text("source.origin-candidates"));
            foreach (EventDefinitionCandidate candidate in origin.Candidates
                .DistinctBy(candidate => (candidate.Actor.UniqueId, candidate.RelativePath)))
                details.Add(Text("source.origin-file", new { provider = candidate.Actor.Name, file = candidate.RelativePath }));
        }
        details.Add(Text("source.asset", new { asset = resolved.AssetName }));
        details.Add(Text("source.key", new { key = resolved.RawEventKey }));
        details.Add(Text("source.origin-modifications-help"));
        if (tracingState == EventSourceDisplayState.Ready && observed is { ScriptMatches: true, IsDeleted: false } && observed.Status != EventSourceStatus.Unknown)
        {
            if (origin.Status == EventOriginMatchStatus.Unknown && observed.Provider is not null)
            {
                // Keep the runtime fallback visibly separate; it isn't a file-origin conclusion.
                details.Add(Text("source.origin-runtime"));
                provider = observed.Provider.IsGameBase ? Text("source.game") : observed.Provider.Name;
                status = Text("source.origin-runtime");
            }
            if (origin.Status == EventOriginMatchStatus.Unknown)
                details.AddRange(runtime.Details);
            else
                details.AddRange(runtime.EvidenceDetails);
        }
        else
        {
            details.Add(Text("source.modifications"));
            details.Add(Text(tracingState switch
            {
                EventSourceDisplayState.Disabled => "source.disabled-help",
                EventSourceDisplayState.RestartRequired => "source.restart-help",
                EventSourceDisplayState.Unavailable => "source.unavailable-help",
                _ => "source.missing-help"
            }));
        }
        return new EventSourceDisplay(Text("source.summary", new { provider }), status, details.AsReadOnly());
    }
}