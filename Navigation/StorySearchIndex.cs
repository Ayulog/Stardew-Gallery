namespace StardewGallery;

internal sealed record StorySearchRow(GalleryEvent Event, string Location, string Characters);

internal sealed class StorySearchIndex
{
    private readonly IReadOnlyList<StorySearchRow> rows;

    internal StorySearchIndex(GalleryCatalog catalog, Func<GalleryEvent, string> locationName, Func<string, string> characterName)
    {
        rows = catalog.StoryEntries.DistinctBy(entry => entry.Resolved.Identity)
            .Select(entry => new StorySearchRow(entry, locationName(entry), string.Join(", ", entry.RelatedNpcNames.Select(characterName))))
            .OrderBy(row => row.Event.EventId, StringComparer.Ordinal).ThenBy(row => row.Event.AssetName, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    internal IReadOnlyList<StorySearchRow> Search(string text, StoryKind? kind = null, string? location = null)
    {
        string query = text.Trim();
        return rows.Where(row => (kind is null || row.Event.Kind == kind)
            && (location is null || string.Equals(row.Event.AssetName, location, StringComparison.OrdinalIgnoreCase))
            && (query.Length == 0 || row.Event.EventId.Contains(query, StringComparison.OrdinalIgnoreCase)
                || row.Event.AssetName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || row.Location.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || row.Characters.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || row.Event.RelatedNpcNames.Any(name => name.Contains(query, StringComparison.OrdinalIgnoreCase)))).ToArray();
    }
}
