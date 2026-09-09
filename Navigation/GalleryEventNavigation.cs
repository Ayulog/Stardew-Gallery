namespace StardewGallery;

internal static class GalleryEventNavigation
{
    internal static IReadOnlyList<string> References(ConditionExpression expression) => expression switch
    {
        SawEventCondition seen => seen.EventIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToArray(),
        ConditionSet set => set.Conditions.SelectMany(References).Distinct(StringComparer.Ordinal).ToArray(),
        _ => []
    };

    internal static IReadOnlyList<GalleryEvent> Resolve(GalleryCatalog catalog, string eventId)
        => StoryDependencyLookup.Find(catalog, eventId).Stories;

    internal static IReadOnlyList<GalleryEvent> SearchOtherIds(GalleryCatalog catalog, string text)
    {
        string query = text.Trim();
        return query.Length == 0 ? [] : catalog.ExcludedEvents
            .Where(entry => entry.Kind != StoryKind.Internal && entry.EventId.Contains(query, StringComparison.OrdinalIgnoreCase))
            .DistinctBy(entry => entry.Resolved.Identity).ToArray();
    }

    internal static GalleryCharacter? Owner(GalleryCatalog catalog, GalleryEvent entry, string? preferredName = null)
    {
        var owners = entry.Ownership.Owners.Select(owner => owner.Name).ToHashSet(StringComparer.Ordinal);
        return catalog.Characters.Where(character => owners.Contains(character.Name))
            .OrderByDescending(character => character.Name == preferredName)
            .ThenByDescending(character => character.IsMet)
            .ThenBy(character => character.Name, StringComparer.Ordinal).FirstOrDefault();
    }
}
