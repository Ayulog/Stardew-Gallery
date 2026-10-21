namespace StardewGallery;

internal static class PrerequisitePresentation
{
    internal static string Title(PrerequisiteEvent entry, GalleryViewContext context)
    {
        if (context.Names?.Get(entry.Identity) is string name) return name;
        string key = "prerequisite.name." + entry.EventId;
        return context.I18n.Get(key).HasValue() ? context.I18n.Get(key).ToString() : entry.EventId;
    }
}
