namespace StardewGallery;

internal sealed record EventSourceDisplay(string Summary, string Status, IReadOnlyList<string> Details);

/// <summary>Player-facing original-definition text from static file matching; never alters gallery permissions.</summary>
internal static class EventOriginPresentation
{
    internal static EventSourceDisplay Build(ResolvedEvent resolved, EventOriginMatch origin, Func<string, object?, string> translate)
    {
        string Text(string key, object? args = null) => translate(key, args);
        string Actor(EventSourceActor actor) => actor.IsGameBase ? Text("source.game")
            : Text("source.actor", new { name = actor.Name, id = actor.UniqueId, version = actor.Version ?? Text("source.version-unknown") });
        bool incomplete = origin.Status == EventOriginMatchStatus.Incomplete;
        string provider = origin.Provider is { } owner ? owner.IsGameBase ? Text("source.game") : owner.Name
            : Text(origin.Ambiguous ? "source.origin-ambiguous" : "source.unknown");
        string status = Text(origin.Provider is { IsGameBase: true } ? "source.origin-vanilla"
            : incomplete ? "source.origin-incomplete"
            : origin.Provider is not null ? "source.origin-matched"
            : origin.Ambiguous ? "source.origin-ambiguous" : "source.origin-missing");
        List<string> details = [Text("source.origin-provider", new { provider = origin.Provider is { } actor ? Actor(actor) : provider }), status];
        details.Add(incomplete ? Text("source.origin-incomplete-help", new { packs = string.Join(", ", origin.UnreadDependencies.Select(pack => pack.Name)) })
            : Text(origin.Ambiguous ? "source.origin-ambiguous-help" : origin.Provider is null ? "source.origin-unknown-help" : "source.origin-help"));
        EventDefinitionCandidate[] files = origin.Candidates.DistinctBy(candidate => (candidate.Actor.UniqueId, candidate.RelativePath)).ToArray();
        // With a provider, other packs declaring the same asset and ID are listed apart; their versions may not apply now.
        bool Provides(EventDefinitionCandidate candidate) => origin.Provider is not { } known
            || StringComparer.OrdinalIgnoreCase.Equals(candidate.Actor.UniqueId, known.UniqueId);
        AddFiles("source.origin-candidates", files.Where(Provides));
        AddFiles("source.origin-others", files.Where(candidate => !Provides(candidate)));
        details.Add(Text("source.asset", new { asset = resolved.AssetName }));
        details.Add(Text("source.key", new { key = resolved.RawEventKey }));
        details.Add(Text("source.scope"));
        return new EventSourceDisplay(Text("source.summary", new { provider }), status, details.AsReadOnly());

        void AddFiles(string heading, IEnumerable<EventDefinitionCandidate> candidates)
        {
            EventDefinitionCandidate[] listed = candidates.ToArray();
            if (listed.Length == 0) return;
            details.Add(Text(heading));
            foreach (EventDefinitionCandidate candidate in listed)
                details.Add(Text("source.origin-file", new { provider = candidate.Actor.Name, file = candidate.RelativePath }));
        }
    }
}
