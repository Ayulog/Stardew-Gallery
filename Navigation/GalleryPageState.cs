namespace StardewGallery;

internal enum GalleryPage { Home, Album, Query, Detail, Photos, Prerequisite, Rename, Filters }

internal sealed class GalleryPageState(GalleryPage page)
{
    internal GalleryPage Page { get; } = page;
    internal string? CharacterName { get; init; }
    internal EventIdentity? Event { get; init; }
    internal string SearchText { get; set; } = "";
    internal int Scroll { get; set; }
    internal bool SourceExpanded { get; set; }
    internal int ConditionScroll { get; set; }
    internal int SourceScroll { get; set; }
    internal int Focus { get; set; } = -1;
    internal string? LocationFilter { get; set; }
    internal StoryKind? KindFilter { get; set; }
    internal string? PrerequisiteId { get; init; }
    // Filter is applied on Query pages and is an isolated draft on Filters pages.
    internal QueryFilter Filter { get; set; } = new();
    internal bool FilterExpanded { get; set; }
    internal string? FilterPicker { get; set; }
    internal int FilterReturnFocus { get; set; }
    // Null means the editor has not loaded its initial value; empty is an intentional draft.
    internal string? RenameText { get; set; }
}

internal sealed class GalleryPageHistory
{
    private readonly List<GalleryPageState> pages = [];
    internal GalleryPageState? Current => pages.LastOrDefault();
    internal int Count => pages.Count;
    internal void Reset() { pages.Clear(); pages.Add(new(GalleryPage.Home)); }
    internal void Open(GalleryPageState target)
    {
        int previous = target.Event is not null ? pages.FindIndex(page => page.Page == target.Page && page.Event == target.Event)
            : target.PrerequisiteId is not null ? pages.FindIndex(page => page.Page == target.Page && page.PrerequisiteId == target.PrerequisiteId) : -1;
        if (previous >= 0) pages.RemoveRange(previous + 1, pages.Count - previous - 1);
        else pages.Add(target);
    }
    internal void Open(GalleryPageState target, Action show)
    {
        GalleryPageState[] previous = pages.ToArray();
        Open(target);
        try { show(); }
        catch { pages.Clear(); pages.AddRange(previous); throw; }
    }
    internal bool ApplyFilters(QueryFilter filter)
    {
        if (Current?.Page != GalleryPage.Filters || pages.Count < 2 || pages[^2] is not { Page: GalleryPage.Query } query) return false;
        pages.RemoveAt(pages.Count - 1);
        query.Filter = filter; query.Scroll = 0; query.Focus = -1;
        return true;
    }
    internal bool Back()
    {
        if (pages.Count == 0) return false;
        pages.RemoveAt(pages.Count - 1);
        return pages.Count > 0;
    }
}
