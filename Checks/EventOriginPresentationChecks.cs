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
        Console.WriteLine($"Event origin presentation checks passed: {checks}");
    }
}