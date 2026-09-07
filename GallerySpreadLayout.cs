using System;

namespace StardewGallery;

// Logical texture coordinates; callers own viewport scaling and XNA conversion.
internal static class GallerySpreadLayout
{
    internal const int LogicalWidth = 1672;
    internal const int LogicalHeight = 941;
    internal const int LeftRowCount = 6;
    internal const int EventColumns = 2;
    internal const int EventVisibleRows = 3;
    internal const int ScrollbarX = 1508;
    internal const int FooterBaseline = 862;
    internal const int IconSize = 16;
    internal const int BookLeft = 20;
    internal const int BookRight = 1652;
    internal const int BookTop = 22;
    internal const int BookBottom = 924;
    internal const int SpineX = 686;

    internal static (int X, int Y, int Width, int Height) LeftPageBounds => (120, 64, 565, 786);
    internal static (int X, int Y, int Width, int Height) PortraitBounds => (240, 120, 380, 270);
    internal static (int X, int Y, int Width, int Height) RightPageBounds => (715, 64, 835, 786);
    internal static (int X, int Y, int Width, int Height) TitleBounds => (895, 68, 530, 56);

    internal static (int X, int Y, int Width, int Height) LeftRowBounds(int row)
    {
        if ((uint)row >= LeftRowCount)
            throw new ArgumentOutOfRangeException(nameof(row));
        return (195, 432 + row * 63, 445, 48);
    }

    internal static (int X, int Y, int Width, int Height) EventCardBounds(int slot)
    {
        if ((uint)slot >= EventColumns * EventVisibleRows)
            throw new ArgumentOutOfRangeException(nameof(slot));
        return (755 + slot % EventColumns * 365, 140 + slot / EventColumns * 225, 345, 205);
    }

    internal static (int X, int Y, int Width, int Height) EventCardHeaderBounds(int slot)
    {
        var card = EventCardBounds(slot);
        return (card.X + 16, card.Y + 10, card.Width - 132, 34);
    }

    internal static (int X, int Y, int Width, int Height) EventCardDetailsBounds(int slot)
    {
        var card = EventCardBounds(slot);
        return (card.X + card.Width - 112, card.Y + 8, 98, 36);
    }

    internal static (int X, int Y, int Width, int Height) EventCardThumbnailBounds(int slot)
    {
        var card = EventCardBounds(slot);
        return (card.X + 40, card.Y + 50, 265, 149);
    }

    internal static (int X, int Y, int Width, int Height) AlbumScrollTrackBounds => (ScrollbarX, 180, 24, 600);
    internal static (int X, int Y, int Width, int Height) DetailHeaderBounds => (755, 140, 710, 150);
    internal static (int X, int Y, int Width, int Height) DetailMetadataBounds => (755, 140, 425, 150);
    internal static (int X, int Y, int Width, int Height) DetailEventIdBounds => (755, 140, 425, 42);
    internal static (int X, int Y, int Width, int Height) DetailLocationBounds => (755, 190, 425, 92);
    internal static (int X, int Y, int Width, int Height) DetailThumbnailBounds => (1200, 140, 265, 149);
    internal static (int X, int Y, int Width, int Height) ConditionHeadingBounds => (755, 310, 710, 40);
    internal static (int X, int Y, int Width, int Height) ConditionViewportBounds => (755, 365, 710, 420);
    internal static (int X, int Y, int Width, int Height) DetailScrollTrackBounds => (ScrollbarX, 365, 24, 420);
    internal static (int X, int Y, int Width, int Height) FooterBounds => (735, 802, 764, 86);
    internal static (int X, int Y, int Width, int Height) BackButtonBounds => (735, 810, 368, 72);
    internal static (int X, int Y, int Width, int Height) ReplayButtonBounds => (1131, 810, 368, 72);

    // Atlas order is check, cross, question. Unknown must not use the cross.
    internal static (int X, int Y, int Width, int Height) ConditionCheckSource => (0, 0, IconSize, IconSize);
    internal static (int X, int Y, int Width, int Height) ConditionCrossSource => (16, 0, IconSize, IconSize);
    internal static (int X, int Y, int Width, int Height) ConditionQuestionSource => (32, 0, IconSize, IconSize);
    internal static (int X, int Y, int Width, int Height) ReplayGlyphSource => (0, 0, IconSize, IconSize);
}

internal static class GalleryUiAssets
{
    internal const string EventAlbum = "assets/GalleryEventAlbum-v3.png";
    internal const string EventDetail = "assets/GalleryEventDetail-v3.png";
    internal const string ConditionStatusIcons = "assets/ConditionStatusIcons.png";
    internal const string ReplayGlyph = "assets/ReplayGlyph.png";
    internal const string EventSlotFrame = "assets/EventSlotFrameOverlay.png";
    internal const string ScrollbarTrack = "assets/ScrollbarTrack.png";
}
