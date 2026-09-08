using StardewGallery;

internal static class GalleryDirectoryChecks
{
    internal static void Run()
    {
        ObservedEventAssets observed = new();
        Check(observed.Observe("Data\\Events\\FuturePlace"), "event asset observed");
        Check(!observed.Observe("data/events/futureplace"), "case and separators deduplicate");
        foreach (string bad in new[] { "Data/Characters", "Data/Events", "Data/Events/", "Data/Events/../Other", "Data/Events//x", "Data/Events/C:/x" })
            Check(!observed.Observe(bad), "reject unrelated or invalid asset path");
        observed.Clear(); Check(observed.Names.Count == 0, "title reset removes previous save observations");
        int loads = 0;
        ResolvedEventCandidateCache? cache = null;
        cache = new(() => { loads++; if (loads == 1) cache!.Invalidate(); return []; });
        cache.GetCurrent(); cache.GetCurrent(); cache.GetCurrent();
        Check(loads == 2, "invalidation during a content load is retained for the next read");
        GalleryCatalogBuilder builder = new(key => key.Split('/'), script => script.Split('/'), args => args.Split(' ', StringSplitOptions.RemoveEmptyEntries), args => args.Split(' ', StringSplitOptions.RemoveEmptyEntries), () => null);
        GalleryCharacter abigail = new("Abigail", "阿比盖尔", true, 1000);
        GalleryCharacter wizard = new("Wizard", "法师", true, 0);
        GalleryCharacter visitor = new("Visitor", "訪問者", true, 0) { IsSocial = false };
        List<ResolvedEvent> input = [
            Entry("Town", "heart/f Abigail 500", "music/0 0/Abigail 0 0 2 Wizard 1 0 2/speak Abigail hello/speak Wizard hello/end"),
            Entry("Town", "ordinary", "music/0 0/Visitor 0 0 2 Abigail 1 0 2/speak Visitor hello/end"),
            Entry("Town", "background", "music/0 0/Visitor 0 0 2/pause 500/end"),
            Entry("Town", "follow/e ordinary", "music/0 0/farmer 0 0 2/message hello/end"),
            Entry("Unused", "supplement/f Abigail 1000", "music/0 0/Abigail 0 0 2/speak Abigail hello/end") with { HasLocationContext = false },
            Entry("Town", "cycle-a/e cycle-b", "music/0 0/farmer 0 0 2/message hello/end"),
            Entry("Town", "cycle-b/e cycle-a", "music/0 0/farmer 0 0 2/message hello/end") ];
        GalleryCatalog old = builder.Build([abigail, wizard], input.Where(entry => entry.HasLocationContext).ToArray()).Catalog;
        GalleryCatalog expanded = builder.Build([abigail, wizard], input, name => name == "Visitor" ? visitor : null).Catalog;
        GalleryCatalog brokenProfile = builder.Build([abigail, wizard], input, _ => throw new InvalidOperationException("broken optional NPC data")).Catalog;
        Check(brokenProfile.Events.Count == old.Events.Count && brokenProfile.ExcludedEvents.Count > 0, "bad optional NPC metadata leaves other entries readable");
        Check(expanded.Events.Select(entry => entry.Identity).SequenceEqual(old.Events.Select(entry => entry.Identity)), "display profiles and supplements do not change replay list");
        GalleryEvent Find(string id) => expanded.Events.Concat(expanded.ExcludedEvents).Single(entry => entry.EventId == id);
        Check(Find("heart").RelatedNpcNames.SequenceEqual(["Abigail", "Wizard"]), "both speaking NPCs associated");
        Check(Find("ordinary").RelatedNpcNames.SequenceEqual(["Visitor"]), "speaking non-social profile included, silent actor excluded");
        Check(!expanded.Characters.Single(npc => npc.Name == "Visitor").IsSocial, "non-social metadata retained");
        Check(Find("background").RelatedNpcNames.Count == 0 && Find("background").IsFlow, "actor placement alone does not establish ownership");
        Check(Find("follow").RelatedNpcNames.SequenceEqual(["Visitor"]), "unambiguous prerequisite inherits display association");
        Check(Find("cycle-a").RelatedNpcNames.Count == 0, "unresolved cycle terminates");
        Check(!GalleryEventNavigation.IsReplayListed(expanded, Find("supplement")), "contextless entry cannot replay");
        var copies = Enumerable.Range(0, 363).Select(i => new GalleryEvent(Entry("Place" + i, "shared", "music/0 0/farmer 0 0 2/message hello/end"), new(OwnershipKind.Excluded, []))).ToArray();
        var grouped = GalleryEventGroup.Build(copies);
        Check(grouped.Count == 1 && grouped[0].Sources.Count == 363, "same content folds while retaining every source identity");
        Check(GalleryEventNavigation.Resolve(new([], [], copies), "shared").Count == 363, "precise reference resolution remains unfurled");
        GalleryEvent differentFragment = copies[0] with { Resolved = copies[0].Resolved with { Identity = new("Data/Events/Different", "shared"), Fragments = new([copies[0].Script, "speak Visitor changed"], []) } };
        Check(GalleryEventGroup.Build(copies.Append(differentFragment)).Count == 2, "different referenced content remains separate");
        GalleryEvent missing = copies[0] with { Resolved = copies[0].Resolved with { Identity = new("Data/Events/Missing", "shared"), Fragments = new([copies[0].Script], ["unknown"]) } };
        Check(GalleryEventGroup.Build(copies.Append(missing)).Count == 2, "missing fragments cannot prove equivalence");
        var ordinary = expanded.Groups.Single(group => group.Representative.EventId == "ordinary");
        Check(ordinary.Matches(GalleryEventCategory.Ordinary, "Visitor", null, "訪問", _ => "訪問者", _ => "城镇"), "Unicode NPC search");
        Check(!ordinary.Matches(GalleryEventCategory.Heart, "Visitor", null, "", _ => "", _ => ""), "ordinary is not a heart event");
        Check(!ordinary.Matches(GalleryEventCategory.All, null, "Data/Events/Beach", "", _ => "", _ => ""), "location scope filters exact asset");
        var flow = expanded.Groups.Single(group => group.Representative.EventId == "background");
        Check(!flow.Matches(GalleryEventCategory.All, null, null, "", _ => "", _ => ""), "flow hidden from default list");
        Check(flow.Matches(GalleryEventCategory.All, null, null, "background", _ => "", _ => ""), "flow discoverable by ID");
        Check(flow.Matches(GalleryEventCategory.Flow, null, null, "", _ => "", _ => ""), "explicit flow filter");
        Console.WriteLine("Gallery 2.5.0 directory checks passed: associations, replay boundary, grouping, search and observed assets.");
    }
    private static ResolvedEvent Entry(string place, string key, string script) => new(new("Data/Events/" + place, key.Split('/')[0]), place, key, script, new([script], []), EventHashes.RootDefinition(key, script), EventHashes.RootScript(script));
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException("2.5.0: " + message); }
}
