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
    internal const int RibbonTabCount = 3;
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
    internal static (int X, int Y, int Width, int Height) SpreadTitleBounds => (620, 32, 432, 60);

    internal const int RibbonPlaqueYSpread = 780;
    internal const int RibbonPlaqueYAlbum = 808;
    internal const int RibbonInactiveTop = 858;
    internal const int RibbonInactiveBottom = 906;
    internal const int RibbonActiveBottom = 935;

    /// <summary>
    /// The label plaque of one bottom-left bookmark: on the symmetric spreads (home, story index, moments) it sits at
    /// <c>Y = 780</c>; on the album/detail pages below the six left-page information rows at <c>Y = 808</c>.
    /// </summary>
    internal static (int X, int Y, int Width, int Height) RibbonTabBounds(int tab, bool albumPage = false)
    {
        if ((uint)tab >= RibbonTabCount)
            throw new ArgumentOutOfRangeException(nameof(tab));
        return (180 + tab * 138, albumPage ? RibbonPlaqueYAlbum : RibbonPlaqueYSpread, 130, 34);
    }

    /// <summary>
    /// The visible ribbon tail under a plaque: the active chapter's ribbon hangs from just under its plaque out of
    /// the book bottom; inactive ribbons only peek out of the page-stack gap.
    /// </summary>
    internal static (int X, int Y, int Width, int Height) RibbonTailBounds(int tab, bool active, bool albumPage = false)
    {
        var plaque = RibbonTabBounds(tab, albumPage);
        int x = plaque.X + plaque.Width / 2 - 28;
        int top = active ? plaque.Y + plaque.Height - 4 : RibbonInactiveTop;
        int bottom = active ? RibbonActiveBottom : RibbonInactiveBottom;
        return (x, top, 56, bottom - top);
    }

    // Villager overview: left 3x3 + right 3x3 portraits around the spine at X = 836.
    internal const int HomePortraitSize = 108;
    internal static (int X, int Y, int Width, int Height) HomePortraitBounds(int slot)
    {
        if ((uint)slot >= 18)
            throw new ArgumentOutOfRangeException(nameof(slot));
        int page = slot / 9, local = slot % 9;
        int[] xs = page == 0 ? [196, 376, 556] : [948, 1128, 1308];
        return (xs[local % 3], 188 + local / 3 * 204, HomePortraitSize, HomePortraitSize);
    }

    internal static (int X, int Y, int Width, int Height) HomeCardBounds(int slot)
    {
        var portrait = HomePortraitBounds(slot);
        return (portrait.X - 30, portrait.Y - 8, portrait.Width + 60, 168);
    }

    internal static (int X, int Y, int Width, int Height) HomeSearchBounds => (168, 118, 390, 48);
    internal static (int X, int Y, int Width, int Height) HomeUnlockBounds => (1236, 770, 248, 48);

    // Story index (symmetric spread).
    internal static (int X, int Y, int Width, int Height) IndexSearchBounds => (168, 148, 568, 48);
    internal static (int X, int Y, int Width, int Height) IndexFilterBounds(int row)
    {
        if ((uint)row >= 5)
            throw new ArgumentOutOfRangeException(nameof(row));
        return (168, 264 + row * 68, 568, 52);
    }
    internal static (int X, int Y, int Width, int Height) IndexClueBounds => (168, 610, 568, 58);
    internal static (int X, int Y, int Width, int Height) IndexCountBounds => (168, 692, 350, 46);
    internal static (int X, int Y, int Width, int Height) IndexResetBounds => (536, 692, 200, 46);
    internal const int IndexRowsPerPage = 5;
    internal static (int X, int Y, int Width, int Height) IndexRowBounds(int slot)
    {
        if ((uint)slot >= IndexRowsPerPage)
            throw new ArgumentOutOfRangeException(nameof(slot));
        return (892, 126 + slot * 128, 612, 112);
    }

    // Treasured moments: 2x2 on each page; each card = title plaque + framed 16:9 photo.
    internal static (int X, int Y, int Width, int Height) MomentPhotoBounds(int slot)
    {
        if ((uint)slot >= 8)
            throw new ArgumentOutOfRangeException(nameof(slot));
        int page = slot / 4, local = slot % 4;
        int x = (page == 0 ? 168 : 920) + local % 2 * 312;
        int y = 196 + local / 2 * 280;
        return (x, y, 265, 149);
    }

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

    internal static (int X, int Y, int Width, int Height) DetailHeaderBounds => (755, 140, 710, 150);
    internal static (int X, int Y, int Width, int Height) DetailMetadataBounds => (755, 140, 425, 150);
    internal static (int X, int Y, int Width, int Height) DetailEventIdBounds => (755, 140, 425, 32);
    internal static (int X, int Y, int Width, int Height) DetailLocationBounds => (755, 173, 425, 32);
    internal static (int X, int Y, int Width, int Height) DetailSourceBounds => (755, 206, 425, 34);
    internal static (int X, int Y, int Width, int Height) DetailThumbnailBounds => (1200, 140, 265, 149);
    internal static (int X, int Y, int Width, int Height) ConditionHeadingBounds => (755, 310, 710, 40);
    internal static (int X, int Y, int Width, int Height) ConditionViewportBounds => (755, 365, 710, 420);
    internal static (int X, int Y, int Width, int Height) DetailScrollTrackBounds => (ScrollbarX, 365, 24, 420);
    internal static (int X, int Y, int Width, int Height) FooterBounds => (735, 802, 764, 86);
    internal static (int X, int Y, int Width, int Height) BackButtonBounds => (735, 810, 368, 72);
    internal static (int X, int Y, int Width, int Height) ReplayButtonBounds => (1131, 810, 368, 72);

    // Whole-page book pager on the album's right-page footer, right of the back button.
    internal static (int X, int Y, int Width, int Height) RightPagePrevBounds => (1300, 818, 44, 44);
    internal static (int X, int Y, int Width, int Height) RightPageIndicatorBounds => (1346, 818, 104, 44);
    internal static (int X, int Y, int Width, int Height) RightPageNextBounds => (1452, 818, 44, 44);
    internal static (int X, int Y, int Width, int Height) AlbumBackBounds => (735, 814, 196, 52);

    // Whole-page book pager on the symmetric spreads (Villager Overview, Story Index, Treasured Moments).
    internal static (int X, int Y, int Width, int Height) SpreadPrevBounds => (925, 772, 44, 44);
    internal static (int X, int Y, int Width, int Height) SpreadIndicatorBounds => (973, 772, 154, 44);
    internal static (int X, int Y, int Width, int Height) SpreadNextBounds => (1131, 772, 44, 44);
    internal static (int X, int Y, int Width, int Height) IndexPrevBounds => (1068, 772, 44, 44);
    internal static (int X, int Y, int Width, int Height) IndexIndicatorBounds => (1116, 772, 172, 44);
    internal static (int X, int Y, int Width, int Height) IndexNextBounds => (1292, 772, 44, 44);
    internal static (int X, int Y, int Width, int Height) SpreadBackBounds => (1236, 770, 248, 48);

    // Event detail (option C): favourite / rename inside metadata header, big back / replay footer buttons.
    internal static (int X, int Y, int Width, int Height) DetailFavoriteBounds => (755, 246, 204, 42);
    internal static (int X, int Y, int Width, int Height) DetailRenameBounds => (971, 246, 209, 42);
    internal static (int X, int Y, int Width, int Height) DetailBackBounds => (755, 812, 340, 56);
    internal static (int X, int Y, int Width, int Height) DetailReplayBounds => (1125, 812, 340, 56);

    // Atlas order is check, cross, question. Unknown must not use the cross.
    internal static (int X, int Y, int Width, int Height) ConditionCheckSource => (0, 0, IconSize, IconSize);
    internal static (int X, int Y, int Width, int Height) ConditionCrossSource => (16, 0, IconSize, IconSize);
    internal static (int X, int Y, int Width, int Height) ConditionQuestionSource => (32, 0, IconSize, IconSize);
    internal static (int X, int Y, int Width, int Height) ReplayGlyphSource => (0, 0, IconSize, IconSize);

    // Verified 1.6.15 Game1.mouseCursors native icon regions.
    internal static (int X, int Y, int Width, int Height) GoldStarSource => (346, 400, 8, 8);
    internal static (int X, int Y, int Width, int Height) HeartFullSource => (211, 428, 7, 6);
    internal static (int X, int Y, int Width, int Height) PagePrevSource => (352, 495, 12, 11);
    internal static (int X, int Y, int Width, int Height) PageNextSource => (365, 495, 12, 11);
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
