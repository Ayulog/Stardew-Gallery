using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using StardewGallery;

internal static class GallerySpreadChecks
{
    // Keep tuples inline: the net6.0 JSON generator crashes when scanning C# 12 tuple aliases.
    [ModuleInitializer]
    internal static void Run()
    {
        (int X, int Y, int Width, int Height) canvas = (0, 0, GallerySpreadLayout.LogicalWidth, GallerySpreadLayout.LogicalHeight);
        Check(canvas == (0, 0, 1672, 941), "Logical spread dimensions changed.");
        Contains(canvas, GallerySpreadLayout.LeftPageBounds, "Left page");
        Contains(canvas, GallerySpreadLayout.RightPageBounds, "Right page");
        Contains(canvas, GallerySpreadLayout.SpreadTitleBounds, "Spread title plaque");
        Disjoint("Pages", GallerySpreadLayout.LeftPageBounds, GallerySpreadLayout.RightPageBounds);

        // Bottom-left chapter bookmarks (both symmetric-spread Y=780 and album/detail Y=808 variants).
        Check(GallerySpreadLayout.RibbonTabCount == 3, "Expected three bottom-left ribbon bookmarks.");
        foreach (bool albumPage in new[] { false, true })
        {
            List<(int X, int Y, int Width, int Height)> plaques = [];
            List<(int X, int Y, int Width, int Height)> activeTails = [];
            List<(int X, int Y, int Width, int Height)> inactiveTails = [];
            for (int tab = 0; tab < GallerySpreadLayout.RibbonTabCount; tab++)
            {
                var plaque = GallerySpreadLayout.RibbonTabBounds(tab, albumPage);
                var activeTail = GallerySpreadLayout.RibbonTailBounds(tab, active: true, albumPage);
                var inactiveTail = GallerySpreadLayout.RibbonTailBounds(tab, active: false, albumPage);
                Contains(canvas, plaque, $"Ribbon plaque {tab} (album={albumPage})");
                Contains(canvas, activeTail, $"Active ribbon tail {tab} (album={albumPage})");
                Contains(canvas, inactiveTail, $"Inactive ribbon tail {tab} (album={albumPage})");
                Disjoint($"Ribbon plaque {tab} and right page", plaque, GallerySpreadLayout.RightPageBounds);
                Check(activeTail.Height > inactiveTail.Height && activeTail.Y + activeTail.Height > inactiveTail.Y + inactiveTail.Height,
                    "Active ribbon tail must hang lower than inactive peek tail.");
                plaques.Add(plaque);
                activeTails.Add(activeTail);
                inactiveTails.Add(inactiveTail);
            }
            Disjoint($"Three bottom-left ribbon plaques (album={albumPage})", plaques.ToArray());
            Disjoint($"Three active ribbon tails (album={albumPage})", activeTails.ToArray());
            Disjoint($"Three inactive ribbon tails (album={albumPage})", inactiveTails.ToArray());
        }

        // Left-page scenic portrait + 6 info rows + bottom-left album ribbons.
        Check(GallerySpreadLayout.PortraitBounds == (240, 120, 380, 270), "Portrait bounds changed.");
        Check(GallerySpreadLayout.LeftRowCount == 6, "Expected six left rows.");
        List<(int X, int Y, int Width, int Height)> leftRegions = [GallerySpreadLayout.PortraitBounds];
        for (int row = 0; row < 6; row++)
        {
            var bounds = GallerySpreadLayout.LeftRowBounds(row);
            Check(bounds == (195, 432 + row * 63, 445, 48), $"Left row {row} moved.");
            leftRegions.Add(bounds);
        }
        for (int tab = 0; tab < GallerySpreadLayout.RibbonTabCount; tab++)
            leftRegions.Add(GallerySpreadLayout.RibbonTabBounds(tab, albumPage: true));
        foreach (var bounds in leftRegions)
            Contains(GallerySpreadLayout.LeftPageBounds, bounds, "Left-page content");
        Disjoint("Portrait, six left rows, and album ribbon plaques", leftRegions.ToArray());

        // Symmetric Villager Overview spread: left 3x3 + right 3x3 around spine X = 836.
        Check(GallerySpreadLayout.HomeVolumeBounds == (948, 118, 520, 48), "Home volume selector bounds changed.");
        Check(GallerySpreadLayout.HomeVolumeBounds.X > 862, "Home volume selector crossed spine.");
        Contains(GallerySpreadLayout.RightPageBounds, GallerySpreadLayout.HomeVolumeBounds, "Home volume selector inside right page");
        List<(int X, int Y, int Width, int Height)> homeRegions =
        [
            GallerySpreadLayout.SpreadTitleBounds,
            GallerySpreadLayout.HomeSearchBounds,
            GallerySpreadLayout.HomeVolumeBounds,
            GallerySpreadLayout.HomeUnlockBounds,
            GallerySpreadLayout.SpreadPrevBounds,
            GallerySpreadLayout.SpreadIndicatorBounds,
            GallerySpreadLayout.SpreadNextBounds
        ];
        for (int tab = 0; tab < GallerySpreadLayout.RibbonTabCount; tab++)
            homeRegions.Add(GallerySpreadLayout.RibbonTabBounds(tab, albumPage: false));
        for (int slot = 0; slot < 18; slot++)
        {
            var portrait = GallerySpreadLayout.HomePortraitBounds(slot);
            var card = GallerySpreadLayout.HomeCardBounds(slot);
            Contains(card, portrait, $"Home slot {slot} portrait inside card");
            if (slot < 9)
                Check(card.X + card.Width < 810, $"Left-page villager card {slot} crossed spine.");
            else
                Check(card.X > 862, $"Right-page villager card {slot} crossed spine.");
            homeRegions.Add(card);
        }
        foreach (var bounds in homeRegions)
            Contains(canvas, bounds, "Home spread region");
        Disjoint("Home spread search, 18 cards, ribbons, pager and unlock", homeRegions.ToArray());

        // Symmetric Story Index spread: left search + 5 filter bars + clue bar + count + reset; right 5 story cards + pager.
        Check(GallerySpreadLayout.IndexRowsPerPage == 5, "Story Index expects 5 cards per page.");
        List<(int X, int Y, int Width, int Height)> indexRegions =
        [
            GallerySpreadLayout.SpreadTitleBounds,
            GallerySpreadLayout.IndexSearchBounds,
            GallerySpreadLayout.IndexClueBounds,
            GallerySpreadLayout.IndexCountBounds,
            GallerySpreadLayout.IndexResetBounds,
            GallerySpreadLayout.IndexPrevBounds,
            GallerySpreadLayout.IndexIndicatorBounds,
            GallerySpreadLayout.IndexNextBounds
        ];
        for (int i = 0; i < 5; i++)
            indexRegions.Add(GallerySpreadLayout.IndexFilterBounds(i));
        for (int slot = 0; slot < GallerySpreadLayout.IndexRowsPerPage; slot++)
        {
            var row = GallerySpreadLayout.IndexRowBounds(slot);
            Check(row.X > 862, $"Index story card {slot} crossed spine.");
            indexRegions.Add(row);
        }
        for (int tab = 0; tab < GallerySpreadLayout.RibbonTabCount; tab++)
            indexRegions.Add(GallerySpreadLayout.RibbonTabBounds(tab, albumPage: false));
        foreach (var bounds in indexRegions)
            Contains(canvas, bounds, "Story Index region");
        Disjoint("Story Index controls, 5 cards, ribbons and pager", indexRegions.ToArray());

        // Symmetric Treasured Moments spread: 2x2 left + 2x2 right Polaroid keepsakes + centered right-page pager.
        List<(int X, int Y, int Width, int Height)> momentRegions =
        [
            GallerySpreadLayout.SpreadTitleBounds,
            GallerySpreadLayout.IndexPrevBounds,
            GallerySpreadLayout.IndexIndicatorBounds,
            GallerySpreadLayout.IndexNextBounds
        ];
        for (int tab = 0; tab < GallerySpreadLayout.RibbonTabCount; tab++)
            momentRegions.Add(GallerySpreadLayout.RibbonTabBounds(tab, albumPage: false));
        for (int slot = 0; slot < 8; slot++)
        {
            var photo = GallerySpreadLayout.MomentPhotoBounds(slot);
            var fullCard = (photo.X - 16, photo.Y - 48, photo.Width + 32, 211);
            if (slot < 4)
                Check(fullCard.Item1 + fullCard.Item3 < 810, $"Left-page moment card {slot} crossed spine.");
            else
                Check(fullCard.Item1 > 862, $"Right-page moment card {slot} crossed spine.");
            momentRegions.Add(fullCard);
        }
        foreach (var bounds in momentRegions)
            Contains(canvas, bounds, "Moments region");
        Disjoint("Moments 8 keepsake cards, ribbons, pager and back", momentRegions.ToArray());

        // Character Album right page (2x3 cards + back + pager).
        Check(GallerySpreadLayout.EventColumns == 2 && GallerySpreadLayout.EventVisibleRows == 3,
            "Expected a two-column, three-row album.");
        List<(int X, int Y, int Width, int Height)> albumRegions = [GallerySpreadLayout.TitleBounds, GallerySpreadLayout.FooterBounds];
        for (int slot = 0; slot < 6; slot++)
        {
            var card = GallerySpreadLayout.EventCardBounds(slot);
            var header = GallerySpreadLayout.EventCardHeaderBounds(slot);
            var details = GallerySpreadLayout.EventCardDetailsBounds(slot);
            var thumbnail = GallerySpreadLayout.EventCardThumbnailBounds(slot);
            Contains(card, header, $"Card {slot} header");
            Contains(card, details, $"Card {slot} details");
            Contains(card, thumbnail, $"Card {slot} thumbnail");
            Disjoint($"Card {slot} children", header, details, thumbnail);
            albumRegions.Add(card);
        }
        foreach (var bounds in albumRegions)
            Contains(bounds == GallerySpreadLayout.FooterBounds ? canvas : GallerySpreadLayout.RightPageBounds, bounds, "Album content");
        Disjoint("Album title, footer and all six cards", albumRegions.ToArray());

        Contains(GallerySpreadLayout.FooterBounds, GallerySpreadLayout.AlbumBackBounds, "Album back button");
        Contains(GallerySpreadLayout.FooterBounds, GallerySpreadLayout.RightPagePrevBounds, "Right-page prev arrow");
        Contains(GallerySpreadLayout.FooterBounds, GallerySpreadLayout.RightPageIndicatorBounds, "Right-page indicator");
        Contains(GallerySpreadLayout.FooterBounds, GallerySpreadLayout.RightPageNextBounds, "Right-page next arrow");
        Disjoint("Right-page footer back button and pager",
            GallerySpreadLayout.AlbumBackBounds,
            GallerySpreadLayout.RightPagePrevBounds,
            GallerySpreadLayout.RightPageIndicatorBounds,
            GallerySpreadLayout.RightPageNextBounds);

        // Event Detail (Option C): metadata lines + Favorite/Rename inside DetailMetadataBounds; wide Back/Replay in FooterBounds.
        Contains(GallerySpreadLayout.DetailHeaderBounds, GallerySpreadLayout.DetailMetadataBounds, "Detail metadata");
        Contains(GallerySpreadLayout.DetailHeaderBounds, GallerySpreadLayout.DetailThumbnailBounds, "Detail thumbnail");
        Disjoint("Detail header children", GallerySpreadLayout.DetailMetadataBounds, GallerySpreadLayout.DetailThumbnailBounds);
        Contains(GallerySpreadLayout.DetailMetadataBounds, GallerySpreadLayout.DetailEventIdBounds, "Detail event ID");
        Contains(GallerySpreadLayout.DetailMetadataBounds, GallerySpreadLayout.DetailLocationBounds, "Detail location");
        Contains(GallerySpreadLayout.DetailMetadataBounds, GallerySpreadLayout.DetailSourceBounds, "Detail source");
        Contains(GallerySpreadLayout.DetailMetadataBounds, GallerySpreadLayout.DetailFavoriteBounds, "Detail favorite button");
        Contains(GallerySpreadLayout.DetailMetadataBounds, GallerySpreadLayout.DetailRenameBounds, "Detail rename button");
        Disjoint("Metadata lines and action buttons",
            GallerySpreadLayout.DetailEventIdBounds,
            GallerySpreadLayout.DetailLocationBounds,
            GallerySpreadLayout.DetailSourceBounds,
            GallerySpreadLayout.DetailFavoriteBounds,
            GallerySpreadLayout.DetailRenameBounds);
        Check(GallerySpreadLayout.DetailSourceBounds.Y >= GallerySpreadLayout.DetailLocationBounds.Y + GallerySpreadLayout.DetailLocationBounds.Height,
            "Source entry must follow the event location.");
        (int X, int Y, int Width, int Height)[] detailRegions = [GallerySpreadLayout.TitleBounds, GallerySpreadLayout.DetailHeaderBounds,
            GallerySpreadLayout.ConditionHeadingBounds, GallerySpreadLayout.ConditionViewportBounds,
            GallerySpreadLayout.FooterBounds, GallerySpreadLayout.DetailScrollTrackBounds];
        foreach (var bounds in detailRegions)
            Contains(bounds == GallerySpreadLayout.FooterBounds ? canvas : GallerySpreadLayout.RightPageBounds, bounds, "Detail content");
        Check(GallerySpreadLayout.FooterBounds.Y + GallerySpreadLayout.FooterBounds.Height < GallerySpreadLayout.BookBottom,
            "Enlarged footer must remain inside the book, below content.");
        Disjoint("Detail title, header, condition heading, viewport, footer and track", detailRegions);
        Contains(GallerySpreadLayout.FooterBounds, GallerySpreadLayout.DetailBackBounds, "Detail back button");
        Contains(GallerySpreadLayout.FooterBounds, GallerySpreadLayout.DetailReplayBounds, "Detail replay button");
        Disjoint("Detail footer buttons", GallerySpreadLayout.DetailBackBounds, GallerySpreadLayout.DetailReplayBounds);
        Check(GallerySpreadLayout.FooterBaseline >= GallerySpreadLayout.FooterBounds.Y
            && GallerySpreadLayout.FooterBaseline < GallerySpreadLayout.FooterBounds.Y + GallerySpreadLayout.FooterBounds.Height,
            "Footer baseline escaped footer.");
        Check(GallerySpreadLayout.DetailScrollTrackBounds.X == GallerySpreadLayout.ScrollbarX,
            "Detail local scrollbar track lost alignment.");

        foreach (int invalid in new[] { int.MinValue, -1, 6, int.MaxValue })
        {
            ThrowsOutOfRange(() => GallerySpreadLayout.LeftRowBounds(invalid), "row");
            ThrowsOutOfRange(() => GallerySpreadLayout.EventCardBounds(invalid), "slot");
            ThrowsOutOfRange(() => GallerySpreadLayout.EventCardHeaderBounds(invalid), "slot");
            ThrowsOutOfRange(() => GallerySpreadLayout.EventCardDetailsBounds(invalid), "slot");
            ThrowsOutOfRange(() => GallerySpreadLayout.EventCardThumbnailBounds(invalid), "slot");
        }
        foreach (int invalidTab in new[] { int.MinValue, -1, 3, int.MaxValue })
            ThrowsOutOfRange(() => GallerySpreadLayout.RibbonTabBounds(invalidTab), "tab");
        foreach (int invalidHome in new[] { int.MinValue, -1, 18, int.MaxValue })
        {
            ThrowsOutOfRange(() => GallerySpreadLayout.HomePortraitBounds(invalidHome), "slot");
            ThrowsOutOfRange(() => GallerySpreadLayout.HomeCardBounds(invalidHome), "slot");
        }
        foreach (int invalidFive in new[] { int.MinValue, -1, 5, int.MaxValue })
        {
            ThrowsOutOfRange(() => GallerySpreadLayout.IndexFilterBounds(invalidFive), "row");
            ThrowsOutOfRange(() => GallerySpreadLayout.IndexRowBounds(invalidFive), "slot");
        }
        foreach (int invalidMoment in new[] { int.MinValue, -1, 8, int.MaxValue })
            ThrowsOutOfRange(() => GallerySpreadLayout.MomentPhotoBounds(invalidMoment), "slot");

        Check(GallerySpreadLayout.IconSize == 16, "Icon size changed.");
        Check(GallerySpreadLayout.ConditionCheckSource == (0, 0, 16, 16), "Check atlas source changed.");
        Check(GallerySpreadLayout.ConditionCrossSource == (16, 0, 16, 16), "Cross atlas source changed.");
        Check(GallerySpreadLayout.ConditionQuestionSource == (32, 0, 16, 16), "Unknown must use the question atlas cell.");
        Check(GallerySpreadLayout.ReplayGlyphSource == (0, 0, 16, 16), "Replay source changed.");
        Check(GallerySpreadLayout.GoldStarSource == (346, 400, 8, 8), "Native gold star source changed.");
        Check(GallerySpreadLayout.HeartFullSource == (211, 428, 7, 6), "Native heart source changed.");
        Check(GallerySpreadLayout.PagePrevSource == (352, 495, 12, 11), "Native prev-page arrow source changed.");
        Check(GallerySpreadLayout.PageNextSource == (365, 495, 12, 11), "Native next-page arrow source changed.");
        (int X, int Y, int Width, int Height)[] statusSources = [GallerySpreadLayout.ConditionCheckSource, GallerySpreadLayout.ConditionCrossSource,
            GallerySpreadLayout.ConditionQuestionSource];
        foreach (var source in statusSources)
            Contains((0, 0, 48, 16), source, "Status atlas source");
        Disjoint("Status atlas cells", statusSources);
        Contains((0, 0, 16, 16), GallerySpreadLayout.ReplayGlyphSource, "Replay glyph source");

        // Like the locale checks, resolve from the executable rather than the caller's working directory.
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root is not null && !(File.Exists(Path.Combine(root.FullName, "StardewGallery.csproj"))
            && File.Exists(Path.Combine(root.FullName, "manifest.json"))))
            root = root.Parent;
        Check(root is not null, "Could not locate the Stardew Gallery repository root.");
        (string Actual, string Expected, int Width, int Height)[] assets =
        [
            ("assets/GalleryHome.png", "assets/GalleryHome.png", 1672, 941),
            (GalleryUiAssets.EventAlbum, "assets/GalleryEventAlbum-v3.png", 1672, 941),
            (GalleryUiAssets.EventDetail, "assets/GalleryEventDetail-v3.png", 1672, 941),
            (GalleryUiAssets.ConditionStatusIcons, "assets/ConditionStatusIcons.png", 48, 16),
            (GalleryUiAssets.ReplayGlyph, "assets/ReplayGlyph.png", 16, 16),
            (GalleryUiAssets.EventSlotFrame, "assets/EventSlotFrameOverlay.png", 345, 205),
            (GalleryUiAssets.ScrollbarTrack, "assets/ScrollbarTrack.png", 24, 640),
            (EventThumbnailAsset.Placeholder, "assets/EventPlaceholder.png", 640, 360),
            ("assets/CardLockedEnvelope.png", "assets/CardLockedEnvelope.png", 530, 298),
            ("assets/CardParallelEnvelope.png", "assets/CardParallelEnvelope.png", 530, 298),
            ("assets/UiRibbons.png", "assets/UiRibbons.png", 336, 132),
            ("assets/UiTabPlaques.png", "assets/UiTabPlaques.png", 124, 64),
            ("assets/UiWoodButtons.png", "assets/UiWoodButtons.png", 116, 156),
            ("assets/UiPagerArrows.png", "assets/UiPagerArrows.png", 264, 44),
            ("assets/UiSearchBox.png", "assets/UiSearchBox.png", 330, 96),
            ("assets/UiFilterRow.png", "assets/UiFilterRow.png", 378, 104),
            ("assets/UiClueRow.png", "assets/UiClueRow.png", 364, 116),
            ("assets/UiStoryRow.png", "assets/UiStoryRow.png", 446, 224),
            ("assets/UiCheckbox.png", "assets/UiCheckbox.png", 56, 28),
            ("assets/UiBadges.png", "assets/UiBadges.png", 224, 32),
            ("assets/UiPills.png", "assets/UiPills.png", 104, 72),
            ("assets/UiHudIcons.png", "assets/UiHudIcons.png", 128, 32),
            ("assets/UiFocusCorners.png", "assets/UiFocusCorners.png", 56, 14),
            ("assets/UiCardFrame.png", "assets/UiCardFrame.png", 297, 177),
            ("assets/UiTitlePlaque.png", "assets/UiTitlePlaque.png", 220, 30),
            ("assets/UiFavoriteRibbon.png", "assets/UiFavoriteRibbon.png", 24, 40),
            ("assets/Scenic-Spring-Day.png", "assets/Scenic-Spring-Day.png", 96, 68),
            ("assets/Scenic-Spring-Dusk.png", "assets/Scenic-Spring-Dusk.png", 96, 68),
            ("assets/Scenic-Spring-Night.png", "assets/Scenic-Spring-Night.png", 96, 68),
            ("assets/Scenic-Summer-Day.png", "assets/Scenic-Summer-Day.png", 96, 68),
            ("assets/Scenic-Summer-Dusk.png", "assets/Scenic-Summer-Dusk.png", 96, 68),
            ("assets/Scenic-Summer-Night.png", "assets/Scenic-Summer-Night.png", 96, 68),
            ("assets/Scenic-Fall-Day.png", "assets/Scenic-Fall-Day.png", 96, 68),
            ("assets/Scenic-Fall-Dusk.png", "assets/Scenic-Fall-Dusk.png", 96, 68),
            ("assets/Scenic-Fall-Night.png", "assets/Scenic-Fall-Night.png", 96, 68),
            ("assets/Scenic-Winter-Day.png", "assets/Scenic-Winter-Day.png", 96, 68),
            ("assets/Scenic-Winter-Dusk.png", "assets/Scenic-Winter-Dusk.png", 96, 68),
            ("assets/Scenic-Winter-Night.png", "assets/Scenic-Winter-Night.png", 96, 68)
        ];
        foreach (var asset in assets)
        {
            Check(asset.Actual == asset.Expected, $"Asset path changed: {asset.Actual}.");
            string path = Path.Combine(root!.FullName, asset.Actual);
            Check(File.Exists(path), $"Missing asset: {asset.Actual}.");
            using BinaryReader reader = new(File.OpenRead(path));
            byte[] header = reader.ReadBytes(24);
            Check(header.Length == 24, $"Truncated PNG header: {asset.Actual}.");
            Check(header.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
                $"Invalid PNG signature: {asset.Actual}.");
            Check(BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(8, 4)) == 13
                && header.AsSpan(12, 4).SequenceEqual(new byte[] { 73, 72, 68, 82 }),
                $"Missing leading PNG IHDR: {asset.Actual}.");
            uint width = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(16, 4));
            uint height = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(20, 4));
            Check(width == asset.Width && height == asset.Height,
                $"{asset.Actual}: expected {asset.Width}x{asset.Height}, got {width}x{height}.");
        }

        string[] retiredScenes =
        [
            "assets/CharacterScene-day-v2.png",
            "assets/CharacterScene-Spring-Day.png",
            "assets/CharacterScene-Summer-Day.png",
            "assets/CharacterScene-Fall-Dusk.png",
            "assets/CharacterScene-Winter-Day.png",
            "assets/CharacterScene-Night-Clear.png",
            "assets/CharacterScene-Winter-Night.png"
        ];
        foreach (string retired in retiredScenes)
            Check(!File.Exists(Path.Combine(root!.FullName, retired)), $"Retired scene asset must be deleted: {retired}.");

        Console.WriteLine($"Gallery spread checks passed (bottom-left ribbons, five spreads, Option C detail regions, and {assets.Length} PNG headers).");
    }

    private static void Contains((int X, int Y, int Width, int Height) parent, (int X, int Y, int Width, int Height) child, string context)
    {
        Check(parent.Width > 0 && parent.Height > 0 && child.Width > 0 && child.Height > 0
            && child.X >= parent.X && child.Y >= parent.Y
            && (long)child.X + child.Width <= (long)parent.X + parent.Width
            && (long)child.Y + child.Height <= (long)parent.Y + parent.Height,
            $"{context}: {child} is not contained in {parent}.");
    }

    private static void Disjoint(string context, params (int X, int Y, int Width, int Height)[] bounds)
    {
        for (int i = 0; i < bounds.Length; i++)
        {
            for (int j = i + 1; j < bounds.Length; j++)
            {
                var a = bounds[i];
                var b = bounds[j];
                // Right and bottom edges are exclusive; touching edges do not overlap.
                Check((long)a.X + a.Width <= b.X || (long)b.X + b.Width <= a.X
                    || (long)a.Y + a.Height <= b.Y || (long)b.Y + b.Height <= a.Y,
                    $"{context}: regions {i} {a} and {j} {b} overlap.");
            }
        }
    }

    private static void ThrowsOutOfRange(Action action, string parameter)
    {
        try { action(); }
        catch (ArgumentOutOfRangeException ex)
        {
            Check(ex.ParamName == parameter, $"Expected invalid {parameter}, got {ex.ParamName}.");
            return;
        }
        throw new InvalidOperationException($"Expected ArgumentOutOfRangeException for {parameter}.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
