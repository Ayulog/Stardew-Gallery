using StardewGallery;

internal static class GalleryDraftStateChecks
{
    internal static void Run()
    {
        GalleryPageHistory history = new(); history.Reset();
        GalleryPageState query = new(GalleryPage.Query)
        { Filter = new() { Npc = "Abigail", Location = "Data/Events/Town" }, Scroll = 5, Focus = 2002, SearchText = "123" };
        history.Open(query);
        GalleryPageState edit = new(GalleryPage.Filters) { Filter = query.Filter };
        history.Open(edit);
        edit.Filter = edit.Filter with { Npc = "Leah", Season = "winter" };
        Check(query.Filter.Npc == "Abigail" && query.Filter.Season is null, "editing leaves the applied query unchanged");
        Check(history.Back() && ReferenceEquals(history.Current, query) && query.Filter.Npc == "Abigail"
            && query.Scroll == 5 && query.Focus == 2002, "cancel preserves the original query, scroll and focus");
        GalleryPageState reopened = new(GalleryPage.Filters) { Filter = query.Filter };
        history.Open(reopened);
        Check(reopened.Filter.Npc == "Abigail" && reopened.Filter.Season is null, "cancelled draft does not reappear on reopening");
        reopened.Filter = reopened.Filter with { Location = "Data/Events/Unavailable", Season = "winter" };
        Check(history.ApplyFilters(reopened.Filter) && ReferenceEquals(history.Current, query)
            && query.Filter.Location == "Data/Events/Unavailable" && query.Filter.Season == "winter", "apply commits the exact draft, including a visibly retained unavailable value");
        Check(query.Scroll == 0 && query.Focus == -1 && query.SearchText == "123", "apply resets result navigation while preserving search text");
        Check(!history.ApplyFilters(new()) && query.Filter.Season == "winter", "an apply outside the editor cannot overwrite the query");
        history.Open(new(GalleryPage.Filters) { Filter = query.Filter with { Season = "summer" } });
        history.Reset();
        Check(history.Current?.Page == GalleryPage.Home && history.Count == 1, "closing and reopening the gallery discards editor history");
        Console.WriteLine("Gallery draft navigation checks passed (7 assertions).");
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Gallery drafts: " + message);
    }
}
