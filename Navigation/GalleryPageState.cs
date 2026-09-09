namespace StardewGallery;

internal enum GalleryPage { Home, Album, Query, Detail, Photos }

internal sealed class GalleryPageState(GalleryPage page)
{
    internal GalleryPage Page { get; } = page;
    internal string? CharacterName { get; init; }
    internal EventIdentity? Event { get; init; }
    internal string SearchText { get; set; } = "";
    internal int Scroll { get; set; }
    internal int Focus { get; set; } = -1;
    internal string? LocationFilter { get; set; }
    internal StoryKind? KindFilter { get; set; }
}

internal sealed class GalleryPageHistory
{
    private readonly List<GalleryPageState> pages = [];
    internal GalleryPageState? Current => pages.LastOrDefault();
    internal int Count => pages.Count;
    internal void Reset() { pages.Clear(); pages.Add(new(GalleryPage.Home)); }
    internal void Open(GalleryPageState target)
    {
        int previous = target.Event is null ? -1 : pages.FindIndex(page => page.Page == target.Page && page.Event == target.Event);
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
    internal bool Back()
    {
        if (pages.Count == 0) return false;
        pages.RemoveAt(pages.Count - 1);
        return pages.Count > 0;
    }
}
