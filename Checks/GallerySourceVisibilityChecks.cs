using StardewGallery;

internal static class GallerySourceVisibilityChecks
{
    internal static void Run()
    {
        int count = 0;
        void Check(bool condition, string message) { count++; if (!condition) throw new InvalidOperationException(message); }
        GalleryEvent Event(string asset, string id, StoryKind kind, string npc = "Shared", string? key = null) =>
            new(new(new(asset, id), asset[(asset.LastIndexOf('/') + 1)..], key ?? id, "none/-100 -100/farmer 0 0 2/mail marker/end",
                new([], []), "data", "script"), new(OwnershipKind.Direct, [new(npc, 500)]))
            { Kind = kind, RelatedNpcNames = [npc], ClassificationReason = kind == StoryKind.Internal ? "internal-state-or-transition-only" : "positive-friendship" };
        var blocked = new EventSourceActor("Blocked.Events", "Same display name");
        var allowed = new EventSourceActor("Allowed.Events", "Same display name");
        EventOriginMatch Origin(EventSourceActor actor) => new(actor, [], EventOriginMatchStatus.Identified);
        var rules = AiModExclusionRules.Parse("{\"Blocked\":{\"ModId\":\"Blocked.Events\"},\"Base\":{\"ModId\":\"StardewValley\"}}");
        GalleryEvent blockedHeart = Event("Data/Events/ModTown", "101", StoryKind.Heart);
        GalleryEvent vanillaSameId = Event("Data/Events/Town", "101", StoryKind.Heart);
        GalleryEvent blockedOnly = Event("Data/Events/ModTown", "102", StoryKind.Heart, "OnlyBlocked");
        GalleryEvent kept = Event("Data/Events/Town", "105", StoryKind.Ordinary, key: "105/e 900");
        GalleryEvent flow = Event("Data/Events/ModTown", "900", StoryKind.Internal);
        GalleryEvent unknown = Event("Data/Events/Town", "106", StoryKind.Ordinary);
        GalleryEvent ambiguous = Event("Data/Events/Town", "107", StoryKind.Ordinary);
        GalleryCatalog raw = new([new("Shared", "Shared", true, 1000), new("OnlyBlocked", "Only blocked", true, 1000)],
            [blockedHeart, vanillaSameId, blockedOnly], [kept, flow, unknown, ambiguous]);
        var origins = new Dictionary<EventIdentity, EventOriginMatch>
        {
            [blockedHeart.Resolved.Identity] = Origin(blocked), [blockedOnly.Resolved.Identity] = Origin(blocked),
            [flow.Resolved.Identity] = Origin(blocked), [vanillaSameId.Resolved.Identity] = Origin(EventSourceActor.GameBase),
            [kept.Resolved.Identity] = new(allowed, [new(blocked, kept.AssetName, kept.EventKey, "override.json", "hash")], EventOriginMatchStatus.Identified),
            [unknown.Resolved.Identity] = new(null, [], EventOriginMatchStatus.Unknown),
            [ambiguous.Resolved.Identity] = new(null, [new(blocked, ambiguous.AssetName, ambiguous.EventKey, "events.json", "hash")], EventOriginMatchStatus.Ambiguous)
        };
        GalleryCatalogVisibilityResult filtered = GalleryCatalogVisibility.Apply(raw, identity => origins[identity], rules.ShouldExclude);
        GalleryCatalog visible = filtered.Catalog;
        Check(filtered.HiddenEvents == 3, "Only identified blocked-provider events are removed");
        Check(visible.Find(blockedHeart.Resolved.Identity) is null && visible.Find(vanillaSameId.Resolved.Identity) == vanillaSameId,
            "Same event ID in different assets does not remove the vanilla event");
        Check(visible.Characters.Count == 1 && visible.Characters[0].Name == "Shared", "Mixed-source character survives; character with only hidden heart stories is omitted");
        Check(visible.Find(kept.Resolved.Identity) == kept && kept.Kind == StoryKind.Ordinary,
            "An override candidate and equal display name cannot remove or reclassify an allowed provider");
        Check(visible.Find(unknown.Resolved.Identity) == unknown && visible.Find(ambiguous.Resolved.Identity) == ambiguous,
            "Unknown and ambiguous origins remain visible");
        Check(visible.Find(flow.Resolved.Identity) is null && visible.Origins.Count == 4, "Hidden internal source is absent from navigation and visible-source metadata");
        Check(raw.AllEntries.Count() == 7 && raw.Characters.Count == 2, "Filtering is a projection and does not mutate original analysis");
        var markerRules = new[]
        {
            new PrerequisiteRule("hidden-story-marker", ["DayStarted"], "", false, true, ["MarkEventSeen Current 102"]),
            new PrerequisiteRule("independent-marker", ["DayStarted"], "", false, true, ["MarkEventSeen Current 991"])
        };
        string[] Split(string value) => value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var parser = new ConditionParser(value => value.Split('/'), Split);
        var prerequisites = PrerequisiteCatalog.Build(markerRules, visible, Split, parser, value => value.Split('/'), filtered.ReservedStoryIds);
        Check(prerequisites.All(entry => entry.EventId != "102"), "A hidden story cannot reappear as a synthetic MarkEventSeen prerequisite");
        Check(prerequisites.All(entry => entry.EventId != "900"), "Hidden internal-only prerequisites do not leak through query or source details");
        Check(prerequisites.Any(entry => entry.EventId == "991"), "Unattributed independent trigger markers are preserved");
        Check(StoryDependencyLookup.Find(visible, "900").InternalSteps.Count == 0, "Inline dependency lookup cannot disclose hidden internal sources");
        var disabled = GalleryCatalogVisibility.Apply(raw, identity => origins[identity], _ => false);
        Check(disabled.HiddenEvents == 0 && disabled.Catalog.AllEntries.Count() == 7 && disabled.Catalog.Characters.Count == 2,
            "Disabling the rule restores the full catalog without changing game data");
        Console.WriteLine($"Gallery source visibility checks passed: {count}");
    }
}
