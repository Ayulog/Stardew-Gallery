namespace StardewGallery;

internal sealed record GalleryCatalogVisibilityResult(GalleryCatalog Catalog,
    IReadOnlySet<string> ReservedStoryIds, int HiddenEvents);

/// <summary>Projects complete analysis into visible events without changing classification or game data.</summary>
internal static class GalleryCatalogVisibility
{
    internal static GalleryCatalogVisibilityResult Apply(GalleryCatalog catalog,
        Func<EventIdentity, EventOriginMatch> originFor, Func<EventOriginMatch, bool> shouldExclude)
    {
        var origins = catalog.AllEntries.DistinctBy(entry => entry.Resolved.Identity)
            .ToDictionary(entry => entry.Resolved.Identity, entry => originFor(entry.Resolved.Identity));
        var hidden = origins.Where(pair => shouldExclude(pair.Value)).Select(pair => pair.Key).ToHashSet();
        var hearts = catalog.Events.Where(entry => !hidden.Contains(entry.Resolved.Identity)).ToArray();
        var other = catalog.ExcludedEvents.Where(entry => !hidden.Contains(entry.Resolved.Identity)).ToArray();
        var owners = hearts.SelectMany(entry => entry.Ownership.Owners).Select(owner => owner.Name).ToHashSet(StringComparer.Ordinal);
        // Preserve existing story IDs so hidden stories cannot reappear as synthetic markers.
        // Asset-specific visibility itself never uses only an event ID.
        var reserved = catalog.StoryEntries.Select(entry => entry.EventId).ToHashSet(StringComparer.Ordinal);
        var visible = catalog with
        {
            Events = hearts,
            ExcludedEvents = other,
            Characters = catalog.Characters.Where(character => owners.Contains(character.Name)).ToArray(),
            Origins = origins.Where(pair => !hidden.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value)
        };
        return new(visible, reserved, hidden.Count);
    }
}
