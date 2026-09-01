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
}
