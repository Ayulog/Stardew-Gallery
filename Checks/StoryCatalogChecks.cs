using StardewGallery;

internal static class StoryCatalogChecks
{
    internal static void Run()
    {
        GalleryCatalogBuilder builder = new(key => key.Split('/'), script => script.Split('|'), Split, Split, () => "Abigail");
        GalleryCharacter[] characters = [new("Abigail", "Abigail", true, 2000), new("Lewis", "Lewis", true, 500)];
        ResolvedEvent[] source =
        [
            Entry("heart/f Abigail 500", "none|0 0|Abigail 1 1 2|speak Abigail hello|end"),
            Entry("short/f Abigail 250", "none|0 0|Abigail 1 1 2|emote Abigail 20|end"),
            Entry("silent/f Abigail 3500", "none|0 0|Abigail 1 1 2|animate Abigail false true 500 0 1|move Abigail 1 0 2|end"),
            Entry("ordinary", "none|0 0|Lewis 1 1 2|speak Lewis hello|end"),
            Entry("zero/f Lewis 0", "none|0 0|Lewis 1 1 2|speak Lewis hello|end"),
            Entry("negative/!f Lewis 500", "none|0 0|Lewis 1 1 2|speak Lewis hello|end"),
            Entry("negative-points/f Lewis -1", "none|0 0|Lewis 1 1 2|speak Lewis hello|end"),
            Entry("malformed/Friendship Lewis 500 Abigail", "none|0 0|Lewis 1 1 2|speak Lewis hello|end"),
            Entry("canonical/Friendship Abigail 1000", "none|0 0|Abigail 1 1 2|speak Abigail hello|end"),
            Entry("festival/F Abigail 500", "none|0 0|Abigail 1 1 2|speak Abigail hello|end"),
            Entry("mail/f Abigail 2000", "none|-500 -500|farmer 1 1 2|mail invitation|pause 10000|end"),
            Entry("marker/f Abigail 2000", "none|-500 -500|Abigail 1 1 2|pause 1|end"),
            Entry("queued", "none|-500 -500|farmer 1 1 2|pause 50|end dialogue Lewis later"),
            Entry("transition", "none|-500 -500|farmer 1 1 2|changeLocation Town|warp farmer 1 1|end"),
            Entry("visible-mail", "none|0 0|Lewis 1 1 2|mail letter|speak Lewis hello|end"),
            Entry("unknown", "none|-500 -500|farmer 1 1 2|Custom.ShowScene test|end"),
            Entry("incomplete", "none|-500 -500|farmer 1 1 2|switchEvent missing|end", missing: ["missing"]),
            Entry("fragment", "none|-500 -500|farmer 1 1 2|fork scene|end", fragments: ["speak Lewis hello|end"]),
            Entry("fragment-heart/f Abigail 500", "none|-500 -500|spouse 1 1 2|fork scene|end", fragments: ["speak spouse hello|end"]),
            Entry("continuation/e heart", "none|0 0|Abigail 1 1 2|speak Abigail again|end"),
            Entry("silent-continuation/e heart", "none|0 0|Abigail 1 1 2|emote Abigail 20|end"),
            Entry("empty-continuation/e heart", "none|0 0|Abigail 1 1 2|pause 100|end"),
            Entry("unknown-continuation/e heart", "none|0 0|Abigail 1 1 2|Custom.ShowScene test|end"),
            Entry("incomplete-continuation/e heart", "none|0 0|Abigail 1 1 2|speak Abigail again|switchEvent missing|end", missing: ["missing"]),
            Entry("optional-actor/f Abigail 500", "none|0 0|Abigail? 1 1 2|speak Abigail? hello|end"),
            Entry("unrelated/e heart", "none|0 0|Lewis 1 1 2|speak Lewis news|end"),
            Entry("ordinary-child/e ordinary", "none|0 0|Lewis 1 1 2|speak Lewis again|end"),
            Entry("internal-child/e mail", "none|0 0|Abigail 1 1 2|speak Abigail again|end"),
            Entry("internal-continuation/e heart", "none|-500 -500|Abigail 1 1 2|mail invitation|end"),
            Entry("multiple/e heart ordinary", "none|0 0|Abigail 1 1 2|speak Abigail hello|end"),
            Entry("case/e HEART", "none|0 0|Abigail 1 1 2|speak Abigail hello|end"),
            Entry("cycle-a/e cycle-b", "none|0 0|Abigail 1 1 2|speak Abigail hello|end"),
            Entry("cycle-b/e cycle-a", "none|0 0|Abigail 1 1 2|speak Abigail hello|end"),
            Entry("duplicate/f Abigail 500", "none|0 0|Abigail 1 1 2|speak Abigail hello|end"),
            Entry("duplicate/f Lewis 500", "none|0 0|Lewis 1 1 2|speak Lewis hello|end", location: "Beach"),
            Entry("ambiguous/e duplicate", "none|0 0|Abigail 1 1 2|speak Abigail hello|end"),
            Entry("nonsocial/f Unknown 500", "none|0 0|Unknown 1 1 2|speak Unknown hello|end"),
            Entry("dating/D Abigail", "none|0 0|Abigail 1 1 2|speak Abigail hello|end"),
            Entry("spouse/O Abigail", "none|0 0|Abigail 1 1 2|speak Abigail hello|end"),
            Entry("typed-dating/Dating Unknown", "none|0 0|Unknown 1 1 2|speak Unknown hello|end"),
            Entry("typed-spouse/Spouse Unknown", "none|0 0|farmer 1 1 2|message letter|end"),
            Entry("negative-dating/!D Abigail", "none|0 0|Abigail 1 1 2|speak Abigail hello|end"),
            Entry("negative-spouse/!Spouse Abigail", "none|0 0|Abigail 1 1 2|speak Abigail hello|end"),
            Entry("not-spouse/o Abigail", "none|0 0|Abigail 1 1 2|speak Abigail hello|end"),
            Entry("weekday/d Abigail", "none|0 0|Abigail 1 1 2|speak Abigail hello|end"),
            Entry("invalid-dating/D Abigail Lewis", "none|0 0|Abigail 1 1 2|speak Abigail hello|end"),
            Entry("internal-spouse/O Abigail", "none|-500 -500|farmer 1 1 2|mail spouse-letter|end"),
            Entry("unlisted-continuation/e nonsocial", "none|0 0|Unknown 1 1 2|speak Unknown hello|end"),
            Entry("dating-continuation/e dating", "none|0 0|Abigail 1 1 2|speak Abigail hello|end"),
            Entry("visible-timing", "none|0 0|farmer 1 1 2|pause 50|end"),
            Entry("visible-long-timing", "none|0 0|farmer 1 1 2|pause 600000|fade|end"),
            Entry("ordinary-emote", "none|0 0|Abigail 1 1 2|emote Abigail 20|end"),
            Entry("ordinary-animation", "none|0 0|Abigail 1 1 2|animate Abigail false true 50 0 1|end"),
            Entry("double-negative-spouse/!NotSpouse Abigail", "none|0 0|Abigail 1 1 2|speak Abigail hello|end"),
            Entry("double-negative-dating/!!D Abigail", "none|0 0|Abigail 1 1 2|speak Abigail hello|end"),
            Entry("not-spouse-canonical/NotSpouse Abigail", "none|0 0|Abigail 1 1 2|speak Abigail hello|end"),
            Entry("generic-roommate/Roommate", "none|0 0|Abigail 1 1 2|speak Abigail hello|end"),
            Entry("spouse-bed/B", "none|0 0|Abigail 1 1 2|speak Abigail hello|end"),
            Entry("named-romance-scene", "none|0 0|Abigail 1 1 2|speak Abigail hello|end")
        ];
        GalleryCatalogBuildResult result = builder.Build(characters, source);
        GalleryCatalog catalog = result.Catalog;
        GalleryEvent Get(string id) => catalog.Find(new EventIdentity("Data/Events/Town", id))!;
        foreach (string id in new[] { "heart", "short", "silent", "canonical", "fragment-heart", "continuation", "silent-continuation", "optional-actor", "nonsocial", "dating", "spouse", "typed-dating", "typed-spouse", "unlisted-continuation", "dating-continuation", "double-negative-spouse", "double-negative-dating" })
            Check(Get(id).Kind == StoryKind.Heart, id + " is a heart story");
        foreach (string id in new[] { "ordinary", "zero", "negative", "negative-points", "malformed", "festival", "visible-mail", "unknown", "incomplete", "fragment", "unrelated", "ordinary-child", "internal-child", "multiple", "case", "cycle-a", "cycle-b", "ambiguous", "unknown-continuation", "incomplete-continuation", "negative-dating", "negative-spouse", "not-spouse", "weekday", "invalid-dating", "ordinary-emote", "ordinary-animation", "not-spouse-canonical", "generic-roommate", "spouse-bed", "named-romance-scene" })
            Check(Get(id).Kind == StoryKind.Ordinary, id + " stays searchable outside the album");
        foreach (string id in new[] { "mail", "marker", "queued", "transition", "internal-continuation", "internal-spouse", "empty-continuation", "visible-timing", "visible-long-timing" })
        {
            Check(Get(id).Kind == StoryKind.Internal, id + " is an internal flow");
            Check(!catalog.StoryEntries.Contains(Get(id)) && catalog.ExcludedEvents.Contains(Get(id)), id + " is hidden but retained");
        }
        Check(catalog.AllEntries.Count() == source.Length && result.AnalyzedEvents.Count == source.Length, "no source entries discarded");
        Check(catalog.Events.All(entry => entry.Kind == StoryKind.Heart), "Events exposes only heart stories");
        Check(catalog.ExcludedEvents.All(entry => entry.Kind != StoryKind.Heart), "Excluded exposes ordinary and internal records");
        Check(Get("ordinary").RelatedNpcNames.Contains("Lewis") && Get("fragment").RelatedNpcNames.Contains("Lewis"), "ordinary NPC search includes actors and fragment speakers");
        Check(!Get("unrelated").RelatedNpcNames.Contains("Abigail"), "prerequisite ownership does not fabricate an NPC relationship");
        Check(Get("optional-actor").RelatedNpcNames.SequenceEqual(["Abigail"]), "optional actor suffix is normalized");
        Check(Get("nonsocial").RelatedNpcNames.Contains("Unknown"), "query metadata is not restricted to social roster");
        Check(catalog.Characters.All(character => character.Name != "Unknown") && catalog.HeartEventsFor("Unknown").Count == 4,
            "heart semantics and ownership survive roster absence without fabricating album characters");
        Check(Get("typed-spouse").Ownership.Owners.Single().FriendshipPoints is null, "dating/spouse does not invent a numeric heart threshold");
        Check(Get("fragment-heart").RelatedNpcNames.Contains("Abigail") && !Get("fragment-heart").RelatedNpcNames.Contains("spouse"), "spouse placeholder is resolved");
        Check(Get("continuation").Ownership.Kind == OwnershipKind.Inherited && Get("continuation").Ownership.Owners.Single().FriendshipPoints == 500, "trusted continuation retains heart ordering");
        Check(catalog.HeartEventsFor("Abigail").All(entry => entry.Kind == StoryKind.Heart) && catalog.HeartEventsFor("Abigail")[0].EventId == "short", "public album lookup filters and sorts by heart threshold");
        Check(catalog.Find(new EventIdentity("data\\events\\beach", "duplicate"))?.Ownership.Owners.Single().Name == "Lewis", "typed lookup distinguishes locations and normalizes asset spelling");
        Check(catalog.Find(new EventIdentity("Data/Events/Town", "absent")) is null, "missing identity has no fabricated entry");
        Check(catalog.AllEntries.All(entry => !string.IsNullOrEmpty(entry.ClassificationReason)), "classification has diagnostic evidence");
        GalleryCatalog ordinaryOnly = builder.Build(characters, [source.Single(entry => entry.EventId == "ordinary")]).Catalog;
        Check(ordinaryOnly.Characters.Count == 0 && ordinaryOnly.Events.Count == 0 && ordinaryOnly.StoryEntries.Count() == 1, "ordinary social dialogue never creates a main-album character");
        Check(new GalleryEvent(source[0], Get("heart").Ownership).Kind == StoryKind.Ordinary, "ownership alone never defaults to Heart");
        GalleryCatalogBuilder slashBuilder = new(key => key.Split('/'), script => script.Split('/'), Split, Split, () => null);
        GalleryEvent visibleFlow = slashBuilder.Build(characters, [Entry("visible-flow", "none/0 0/farmer 1 1 2/pause 50/end")]).AnalyzedEvents.Single();
        Check(visibleFlow.Kind == StoryKind.Internal && visibleFlow.ClassificationReason == "internal-timing-only",
            "exact visible-camera pause/end fixture is internal without duration or offscreen heuristics");
        Console.WriteLine("Story catalog checks passed.");
    }

    private static string[] Split(string value) => value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    private static ResolvedEvent Entry(string key, string script, string location = "Town", string[]? fragments = null, string[]? missing = null)
        => new(new EventIdentity("Data/Events/" + location, key.Split('/')[0]), location, key, script,
            new EventFragments(new[] { script }.Concat(fragments ?? []).ToArray(), missing ?? []), "definition", "script");
    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("Story catalog: " + message);
    }
}
