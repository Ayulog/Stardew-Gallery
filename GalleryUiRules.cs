namespace StardewGallery;

internal static class GalleryUiRules
{
    internal static string DisplayName(string actualName, bool isMet, bool unlocked)
        => isMet || unlocked ? actualName : "???";

    internal static int HeartCapacity(bool canBeRomanced) => canBeRomanced ? 14 : 10;

    internal static int FilledHearts(int friendshipPoints, int capacity) =>
        Math.Clamp(friendshipPoints / 250, 0, capacity);

    internal static int PreferredReplayRow(int selectedIndex, int scroll, int visibleRows)
        => selectedIndex >= scroll && selectedIndex < scroll + visibleRows ? selectedIndex - scroll : 0;

    internal static (int ScrollRow, int VisibleSlot) ResolveReturnPosition(
        int characterIndex, int oldScrollRow, int columns, int visibleRows, int itemCount)
    {
        int maxScroll = Math.Max(0, (itemCount + columns - 1) / columns - visibleRows);
        if (characterIndex < 0 || characterIndex >= itemCount)
            return (Math.Clamp(oldScrollRow, 0, maxScroll), -1);

        int targetRow = characterIndex / columns;
        int minScroll = Math.Max(0, targetRow - visibleRows + 1);
        int maxScrollForTarget = Math.Min(maxScroll, targetRow);
        int desired = oldScrollRow;
        if (desired < minScroll)
            desired = minScroll;
        else if (desired > maxScrollForTarget)
            desired = maxScrollForTarget;
        int visibleSlot = characterIndex - desired * columns;
        return (desired, visibleSlot);
    }
}
