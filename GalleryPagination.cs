namespace StardewGallery;

/// <summary>
/// Pure whole-page book pagination math shared by Villager Overview, Character Album, Story Index, and Treasured Moments.
/// </summary>
internal static class GalleryPagination
{
    internal static int TotalPages(int itemCount, int pageSize)
    {
        if (pageSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(pageSize));
        if (itemCount <= 0)
            return 1;
        return (itemCount + pageSize - 1) / pageSize;
    }

    internal static int ClampPage(int page, int itemCount, int pageSize)
        => Math.Clamp(page, 0, TotalPages(itemCount, pageSize) - 1);

    internal static int FirstIndex(int page, int itemCount, int pageSize)
        => ClampPage(page, itemCount, pageSize) * pageSize;

    internal static int VisibleCount(int page, int itemCount, int pageSize)
    {
        if (pageSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(pageSize));
        if (itemCount <= 0)
            return 0;
        int first = FirstIndex(page, itemCount, pageSize);
        return Math.Clamp(itemCount - first, 0, pageSize);
    }

    internal static int PageForIndex(int itemIndex, int itemCount, int pageSize)
    {
        if (pageSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(pageSize));
        if (itemCount <= 0 || itemIndex <= 0)
            return 0;
        int clampedIndex = Math.Clamp(itemIndex, 0, itemCount - 1);
        return clampedIndex / pageSize;
    }

    internal static (int Page, bool Changed) Turn(int currentPage, int delta, int itemCount, int pageSize)
    {
        int clampedCurrent = ClampPage(currentPage, itemCount, pageSize);
        int target = ClampPage(clampedCurrent + delta, itemCount, pageSize);
        return (target, target != clampedCurrent);
    }

    internal static string Label(int currentPage, int itemCount, int pageSize)
        => $"{ClampPage(currentPage, itemCount, pageSize) + 1} / {TotalPages(itemCount, pageSize)}";
}
