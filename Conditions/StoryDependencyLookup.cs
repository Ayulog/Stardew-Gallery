namespace StardewGallery;

internal sealed record StoryDependencyResult(IReadOnlyList<GalleryEvent> Stories, bool HasInternalStep)
{
    internal bool Missing => Stories.Count == 0 && !HasInternalStep && Prerequisite is null;
    internal IReadOnlyList<GalleryEvent> InternalSteps { get; init; } = [];
    internal PrerequisiteEvent? Prerequisite { get; init; }
}

internal static class StoryDependencyLookup
{
    internal static StoryDependencyResult Find(GalleryCatalog catalog, string eventId)
    {
        GalleryEvent[] matches = catalog.AllEntries.Where(entry => entry.EventId.Equals(eventId, StringComparison.Ordinal))
            .DistinctBy(entry => entry.Resolved.Identity).ToArray();
        return new(matches.Where(entry => entry.Kind != StoryKind.Internal).ToArray(), matches.Any(entry => entry.Kind == StoryKind.Internal))
            { InternalSteps = matches.Where(entry => entry.Kind == StoryKind.Internal).ToArray(), Prerequisite = catalog.FindPrerequisite(eventId) };
    }
}
