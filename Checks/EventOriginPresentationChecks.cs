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
        EventSourceDisplay display = EventOriginPresentation.Build(resolved, origin, Translate);
        Check(display.Summary.Contains("Story Pack") && display.Status == "source.origin-matched", "Matched original definition is the summary provider");
        Check(display.Details.Any(line => line.Contains("events/story.json")), "Matched definition file is listed");
        Check(display.Details.Any(line => line.Contains("42/f Abigail 1000")) && display.Details.Contains("source.scope"),
            "Details keep the current raw key and the main-event scope note");

        var vanilla = new EventOriginMatch(EventSourceActor.GameBase, [candidate with { Actor = EventSourceActor.GameBase }], EventOriginMatchStatus.Identified);
        display = EventOriginPresentation.Build(resolved, vanilla, Translate);
        Check(display.Summary.Contains("source.game") && display.Status == "source.origin-vanilla", "Base game definitions are labeled as game content");

        var ambiguous = new EventOriginMatch(null, [candidate, candidate with { Actor = new EventSourceActor("Fixture.Other", "Other Pack") }], EventOriginMatchStatus.Ambiguous);
        display = EventOriginPresentation.Build(resolved, ambiguous, Translate);
        Check(display.Summary.Contains("source.origin-ambiguous") && display.Details.Contains("source.origin-ambiguous-help"),
            "Ambiguous declarations never pick a single provider");
        Check(display.Details.Any(line => line.Contains("Story Pack")) && display.Details.Any(line => line.Contains("Other Pack")),
            "Every ambiguous candidate stays visible");

        var overrider = new EventSourceActor("Fixture.Override", "Override Pack", "2.0");
        var withOthers = new EventOriginMatch(actor, [candidate, candidate with { Actor = overrider, RelativePath = "patches/override.json" }], EventOriginMatchStatus.Identified);
        display = EventOriginPresentation.Build(resolved, withOthers, Translate);
        int others = display.Details.ToList().IndexOf("source.origin-others");
        Check(others > display.Details.ToList().IndexOf("source.origin-candidates") && display.Details[others + 1].Contains("patches/override.json")
            && !display.Details.Take(others).Any(line => line.Contains("Override Pack")), "Other declarers are listed apart from the provider's own files");
        display = EventOriginPresentation.Build(resolved, origin, Translate);
        Check(!display.Details.Contains("source.origin-others"), "No other-declarers heading without other packs");

        var unread = new EventSourceActor("Fixture.Base", "Base Pack", "1.0");
        var incomplete = new EventOriginMatch(actor, [candidate], EventOriginMatchStatus.Incomplete) { UnreadDependencies = [unread] };
        display = EventOriginPresentation.Build(resolved, incomplete, Translate);
        Check(display.Summary.Contains("Story Pack") && display.Status == "source.origin-incomplete"
            && display.Details.Contains("source.origin-incomplete-help:packs=Base Pack"), "Incomplete evidence names the provider found and the pack that wasn't fully read");

        display = EventOriginPresentation.Build(resolved, EventOriginMatch.Unknown, Translate);
        Check(display.Summary.Contains("source.unknown") && display.Status == "source.origin-missing"
            && display.Details.Contains("source.origin-unknown-help"), "Missing definitions are unknown, never assumed vanilla");
        Console.WriteLine($"Event origin presentation checks passed: {checks}");
    }
}
