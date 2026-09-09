using StardewGallery;

internal static class StoryQueryChecks
{
    internal static void Run()
    {
        GalleryEvent Entry(string asset, string id, StoryKind kind) => new(new(new(asset, id), "Town", id, "scene", new(["scene"], []), "d", "s"), new(OwnershipKind.Excluded, []))
            { Kind = kind, RelatedNpcNames = ["Abigail"] };
        GalleryEvent heart = Entry("Data/Events/Town", "101", StoryKind.Heart);
        GalleryEvent other = Entry("Data/Events/Beach", "102", StoryKind.Ordinary);
        GalleryEvent flow = Entry("Data/Events/Farm", "103", StoryKind.Internal);
        GalleryEvent duplicateSource = other with { Resolved = other.Resolved with { Identity = new("Data/Events/Forest", "102") } };
        GalleryCatalog catalog = new([], [heart], [other, flow, duplicateSource]);
        StorySearchIndex query = new(catalog, entry => entry.AssetName.EndsWith("Beach") ? "海边 Beach" : "小镇 Town", _ => "阿比盖尔 Abigail");
        Check(query.Search("").Count == 3, "internal flows hidden from browse");
        Check(query.Search("103").Count == 0, "internal flows hidden from exact search");
        Check(query.Search("阿比").Count == 3, "localized NPC query");
        Check(query.Search(" Abigail ", StoryKind.Ordinary).Count == 2, "query/filter does not promote ordinary to collection");
        Check(query.Search("海边 Beach").Single().Event == other, "multiword localized location");
        Check(query.Search("102").Count == 2 && query.Search("102", location: "Data/Events/Forest").Single().Event == duplicateSource, "exact source retained");
        StoryDependencyResult step = StoryDependencyLookup.Find(catalog, "103");
        Check(step.HasInternalStep && !step.Missing && step.Stories.Count == 0 && step.InternalSteps.Single() == flow,
            "internal prerequisite retains conditions for inline explanation without becoming a card");
        Check(StoryDependencyLookup.Find(catalog, "999").Missing, "unloaded prerequisite distinct from hidden workflow");
        Check(StoryDependencyLookup.Find(catalog, "102").Stories.Count == 2, "all plot prerequisite sources preserved");
        Console.WriteLine("Story query and dependency checks passed.");
    }
    private static void Check(bool value, string label) { if (!value) throw new InvalidOperationException(label); }
}
