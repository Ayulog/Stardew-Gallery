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
        => catalog.Events.Where(entry => string.Equals(entry.EventId, eventId, StringComparison.Ordinal)).DistinctBy(entry => entry.Resolved.Identity).ToArray();

    internal static IReadOnlyList<GalleryEvent> Search(GalleryCatalog catalog, string text, Func<GalleryEvent, string> location)
    {
        string query = text.Trim();
        if (query.Length == 0)
            return [];
        var characters = catalog.Characters.ToDictionary(character => character.Name, StringComparer.Ordinal);
        return catalog.Events.Where(entry => entry.EventId.Contains(query, StringComparison.OrdinalIgnoreCase)
                || entry.LocationName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || location(entry).Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || entry.Ownership.Owners.Any(owner => owner.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || characters.TryGetValue(owner.Name, out GalleryCharacter? character)
                        && character.DisplayName.Contains(query, StringComparison.CurrentCultureIgnoreCase)))
            .DistinctBy(entry => entry.Resolved.Identity)
            .OrderByDescending(entry => string.Equals(entry.EventId, query, StringComparison.Ordinal))
            .ThenBy(entry => entry.EventId, StringComparer.Ordinal)
            .ThenBy(entry => entry.AssetName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
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

internal sealed class GalleryNavigationTrail<T> where T : class
{
    private readonly List<(EventIdentity Identity, T View)> views = [];
    internal T? Current => views.Count == 0 ? null : views[^1].View;
    internal int Count => views.Count;

    internal T Open(EventIdentity identity, Func<T> create)
    {
        int index = views.FindIndex(item => item.Identity == identity);
        if (index >= 0)
        {
            views.RemoveRange(index + 1, views.Count - index - 1);
            return views[index].View;
        }
        T view = create();
        views.Add((identity, view));
        return view;
    }

    internal T? Back()
    {
        if (views.Count > 0)
            views.RemoveAt(views.Count - 1);
        return Current;
    }
}
