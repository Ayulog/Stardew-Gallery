using StardewGallery;

internal static class GalleryNavigationChecks
{
    internal static void Run()
    {
        GalleryEvent town = Entry("Town", "123");
        GalleryEvent beach = Entry("Beach", "123");
        GalleryEvent locked = Entry("Forest", "456");
        GalleryEvent excluded = Entry("Town", "missing");
        GalleryCharacter abigail = new("Abigail", "阿比盖尔", true, 0);
        GalleryCatalog catalog = new([abigail], [town, beach, locked, town], [excluded]);
        Check(GalleryEventNavigation.Resolve(catalog, "123").Count == 2, "ambiguous ID keeps both assets, removes duplicate identity");
        Check(GalleryEventNavigation.Resolve(catalog, "missing").Count == 0, "excluded catalog entries cannot be followed");
        Check(GalleryEventNavigation.Resolve(catalog, "456").Single() == locked, "locked event remains readable without unlocking");
        Check(GalleryEventNavigation.Resolve(catalog, "12").Count == 0, "references are exact IDs");
        Check(GalleryEventNavigation.Search(catalog, " 123 ", Location).Count == 2, "search trims input and preserves ambiguity");
        Check(GalleryEventNavigation.Search(catalog, "阿比", Location).Count == 3, "localized NPC search");
        Check(GalleryEventNavigation.Search(catalog, "ABIGAIL", Location).Count == 3, "internal NPC search");
        Check(GalleryEventNavigation.Search(catalog, "海滩", Location).Single() == beach, "localized location search");
        Check(GalleryEventNavigation.Search(catalog, "Beach", Location).Single() == beach, "internal location search");
        Check(GalleryEventNavigation.Search(catalog, "  ", Location).Count == 0, "empty event query");
        Check(GalleryEventNavigation.Owner(catalog, beach) == abigail, "target owner comes from target ownership");

        SawEventCondition any = new(["123", "456", "123"], ConditionSource.LegacyEventPrecondition, "e unrelated raw text", false);
        SawEventCondition negated = any with { Negated = true };
        Check(GalleryEventNavigation.References(any).SequenceEqual(["123", "456"]), "structured ANY IDs, distinct and ordered");
        Check(GalleryEventNavigation.References(new ConditionSet([any, negated])).SequenceEqual(["123", "456"]), "nested and negated links");
        Check(any.EventIds.Count == 3 && negated.Negated, "navigation does not mutate conditions");

        GalleryNavigationTrail<object> trail = new();
        object a = trail.Open(town.Resolved.Identity, () => new());
        object b = trail.Open(beach.Resolved.Identity, () => new());
        Check(trail.Count == 2 && !ReferenceEquals(a, b), "same ID different asset is a different view");
        Check(ReferenceEquals(trail.Open(town.Resolved.Identity, () => throw new Exception("must reuse view")), a) && trail.Count == 1,
            "cycle returns existing source with its state, truncating descendants");
        trail.Open(beach.Resolved.Identity, () => b);
        try { trail.Open(locked.Resolved.Identity, () => throw new InvalidOperationException()); } catch (InvalidOperationException) { }
        Check(trail.Count == 2 && ReferenceEquals(trail.Current, b), "failed target construction preserves source");
        Check(ReferenceEquals(trail.Back(), a) && trail.Back() is null && trail.Count == 0, "back unwinds then returns root");
        Console.WriteLine("Gallery 2.3.0 navigation checks passed.");
    }

    private static string Location(GalleryEvent entry) => entry.LocationName == "Beach" ? "海滩" : entry.LocationName;
    private static GalleryEvent Entry(string location, string id) => new(
        new ResolvedEvent(new EventIdentity("Data/Events/" + location, id), location, id + "/f Abigail 2000", "end",
            new EventFragments([], []), "definition", "script"),
        new EventOwnership(OwnershipKind.Direct, [new("Abigail", 2000)]));
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("2.3.0: " + message);
    }
}
