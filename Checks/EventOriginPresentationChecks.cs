using StardewGallery;

internal static class EventOriginPresentationChecks
{
    internal static void Run()
    {
        int checks = 0;
        void Check(bool condition, string reason) { checks++; if (!condition) throw new InvalidOperationException(reason); }
        string Translate(string key, object? args) => args is null ? key : key + ":" + string.Join(";", args.GetType().GetProperties().Select(property => property.Name + "=" + property.GetValue(args)));
        var actor = new EventSourceActor("Fixture.Story", "Story Pack", "1.0");
        var candidate = new EventDefinitionCandidate(actor, "Data/Events/Town", "42/f Abigail 500", "events/story.json", EventHashes.RootScript("original"));
        var resolved = new ResolvedEvent(new EventIdentity("Data/Events/Town", "42"), "Town", "42/f Abigail 1000", "changed by another mod",
            new EventFragments([], []), "definition-hash", EventHashes.RootScript("changed by another mod"));
        var origin = new EventOriginMatch(actor, [candidate], EventOriginMatchStatus.Identified);
        foreach (var state in new[] { EventSourceDisplayState.Disabled, EventSourceDisplayState.Unavailable, EventSourceDisplayState.RestartRequired, EventSourceDisplayState.Ready })
        {
            EventSourceDisplay display = EventOriginPresentation.Build(resolved, origin, null, state, Translate);
            Check(display.Summary.Contains("Story Pack") && display.Status == "source.origin-matched", "Original definition remains visible independently of tracing state: " + state);
            Check(display.Details.Any(line => line.Contains("events/story.json")), "Matched definition file is visible when tracing is unavailable");
        }
        var observed = new EventSourceInfo(new EventSourceScope(resolved.AssetName, "zh-CN", 0, "test"), resolved.RawEventKey,
            resolved.RootScriptHash, 1, 1, new EventSourceActor("Fixture.Translation", "Translation Pack"), null, [], EventSourceStatus.Complete, null);
        var knownDisplay = EventOriginPresentation.Build(resolved, origin, observed, EventSourceDisplayState.Ready, Translate);
        Check(knownDisplay.Summary.Contains("Story Pack") && !knownDisplay.Summary.Contains("Translation"), "Later runtime provider never replaces matched original definition");
        var ambiguous = new EventOriginMatch(null, [candidate, candidate with { Actor = new EventSourceActor("Fixture.Other", "Other Pack") }], EventOriginMatchStatus.Ambiguous);
        var ambiguousDisplay = EventOriginPresentation.Build(resolved, ambiguous, observed, EventSourceDisplayState.Ready, Translate);
        Check(ambiguousDisplay.Summary.Contains("source.origin-ambiguous") && !ambiguousDisplay.Summary.Contains("Translation"), "Observed last provider cannot resolve ambiguous original files");
        var unknown = new EventOriginMatch(null, [], EventOriginMatchStatus.Unknown);
        var fallback = EventOriginPresentation.Build(resolved, unknown, observed, EventSourceDisplayState.Ready, Translate);
        Check(fallback.Summary.Contains("Translation Pack") && fallback.Status == "source.origin-runtime", "Runtime-only fallback is explicitly identified as observed rather than file origin");
        var mismatch = EventOriginPresentation.Build(resolved, unknown, observed with { ScriptMatches = false }, EventSourceDisplayState.Ready, Translate);
        Check(!mismatch.Summary.Contains("Translation Pack"), "Stale runtime evidence cannot become a fallback original provider");
        foreach (EventOriginMatch matchedOrigin in new[] { origin, ambiguous, unknown })
        {
            EventSourceDisplay partial = EventOriginPresentation.Build(resolved, matchedOrigin,
                observed with { Status = EventSourceStatus.Partial, Reason = "private failure" }, EventSourceDisplayState.Ready, Translate);
            Check(partial.Details.Contains("source.partial") && partial.Details.Contains("source.partial-help"),
                "Incomplete runtime evidence stays visible with original origin " + matchedOrigin.Status);
            Check(partial.Details.Contains("source.no-modifications") && !partial.Details.Any(line => line.Contains("private failure")),
                "Zero observed mutations neither hide incompleteness nor expose private errors");
        }
        Check(knownDisplay.Details.Contains("source.complete") && !knownDisplay.Details.Contains("source.partial-help"),
            "Complete runtime evidence is shown independently of matched original source");
        foreach (EventSourceInfo stale in new[] { observed with { ScriptMatches = false }, observed with { IsDeleted = true }, observed with { Status = EventSourceStatus.Unknown } })
        {
            EventSourceDisplay staleDisplay = EventOriginPresentation.Build(resolved, origin, stale, EventSourceDisplayState.Ready, Translate);
            Check(staleDisplay.Details.Contains("source.missing-help") && !staleDisplay.Details.Contains("source.complete")
                && !staleDisplay.Details.Contains("source.no-modifications"), "Stale/deleted/unknown evidence never claims complete unchanged content");
        }
        foreach (EventSourceDisplayState state in new[] { EventSourceDisplayState.Disabled, EventSourceDisplayState.RestartRequired, EventSourceDisplayState.Unavailable })
        {
            EventSourceDisplay inactive = EventOriginPresentation.Build(resolved, origin, observed with { Status = EventSourceStatus.Partial }, state, Translate);
            Check(!inactive.Details.Contains("source.partial") && !inactive.Details.Contains("source.no-modifications"),
                "Disabled tracing doesn't reuse prior runtime evidence: " + state);
        }
        string CollidingTranslate(string key, object? args) => key is "source.asset" or "source.modifications" ? "same translated label" : Translate(key, args);
        EventSourceDisplay collision = EventOriginPresentation.Build(resolved, origin,
            observed with { Status = EventSourceStatus.Partial }, EventSourceDisplayState.Ready, CollidingTranslate);
        Check(collision.Details.Contains("source.partial-help") && collision.Details.Contains("source.no-modifications"),
            "Runtime evidence structure does not depend on translated label equality");
        Console.WriteLine($"Event origin presentation checks passed: {checks}");
    }
}