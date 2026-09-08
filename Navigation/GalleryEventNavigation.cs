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
        => catalog.Events.Concat(catalog.ExcludedEvents).Where(entry => string.Equals(entry.EventId, eventId, StringComparison.Ordinal)).DistinctBy(entry => entry.Resolved.Identity).ToArray();

    internal static IReadOnlyList<GalleryEvent> SearchOtherIds(GalleryCatalog catalog, string text)
    {
        string query = text.Trim();
        return query.Length == 0 ? [] : catalog.ExcludedEvents
            .Where(entry => entry.EventId.Contains(query, StringComparison.OrdinalIgnoreCase))
            .DistinctBy(entry => entry.Resolved.Identity).ToArray();
    }

    internal static bool IsReplayListed(GalleryCatalog catalog, GalleryEvent entry)
        => catalog.Events.Any(candidate => candidate.Resolved.Identity == entry.Resolved.Identity);

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
