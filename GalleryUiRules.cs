namespace StardewGallery;

internal static class GalleryUiRules
{
    internal const int EventColumns = 2;
    internal const int EventVisibleRows = 3;

    internal static string DisplayName(string actualName, bool isMet, bool unlocked)
        => isMet || unlocked ? actualName : "???";

    internal static int HeartCapacity(bool canBeRomanced) => canBeRomanced ? 14 : 10;

    internal static int FilledHearts(int friendshipPoints, int capacity) =>
        Math.Clamp(friendshipPoints / 250, 0, capacity);

    internal static (int Row, int Column) EventCardPosition(int index)
        => (index / EventColumns, index % EventColumns);

    internal static (int X, int Y, int Width, int Height) EventCardBounds(int visibleIndex)
    {
        (int row, int column) = EventCardPosition(visibleIndex);
        return (755 + column * 365, 140 + row * 225, 345, 205);
    }

    internal static EventCardInteraction EventCardInteraction(bool unlocked)
        => new(CanReplay: unlocked, CanViewDetails: true);

    internal static int PreferredReplayRow(int selectedIndex, int scroll, int visibleRows)
        => selectedIndex >= scroll && selectedIndex < scroll + visibleRows ? selectedIndex - scroll : 0;

    internal static bool ShouldCloseFromShortcut(bool shortcutPressed, bool searchSelected)
        => shortcutPressed && !searchSelected;

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

internal sealed record EventCardInteraction(bool CanReplay, bool CanViewDetails);

internal static class EventThumbnailAsset
{
    internal const string Placeholder = "assets/EventPlaceholder.png";
    internal static string For(EventIdentity? identity = null) => Placeholder;
}
