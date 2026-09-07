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
        Disjoint("Pages", GallerySpreadLayout.LeftPageBounds, GallerySpreadLayout.RightPageBounds);
        Check(GallerySpreadLayout.PortraitBounds == (240, 120, 380, 270), "Portrait bounds changed.");
        Check(GallerySpreadLayout.LeftRowCount == 6, "Expected six left rows.");
        List<(int X, int Y, int Width, int Height)> leftRegions = [GallerySpreadLayout.PortraitBounds];
        for (int row = 0; row < 6; row++)
        {
            var bounds = GallerySpreadLayout.LeftRowBounds(row);
            Check(bounds == (195, 432 + row * 63, 445, 48), $"Left row {row} moved.");
            leftRegions.Add(bounds);
        }
        foreach (var bounds in leftRegions)
            Contains(GallerySpreadLayout.LeftPageBounds, bounds, "Left-page content");
        Disjoint("Portrait and left rows", leftRegions.ToArray());

        Check(GallerySpreadLayout.EventColumns == 2 && GallerySpreadLayout.EventVisibleRows == 3,
            "Expected a two-column, three-row album.");
        List<(int X, int Y, int Width, int Height)> albumRegions = [GallerySpreadLayout.TitleBounds, GallerySpreadLayout.AlbumScrollTrackBounds,
            GallerySpreadLayout.FooterBounds];
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
            Contains(GallerySpreadLayout.RightPageBounds, bounds, "Album content");
        Disjoint("Album title, track, footer and all six cards", albumRegions.ToArray());

        // Header and metadata are parents, not siblings of the regions they contain.
        Contains(GallerySpreadLayout.DetailHeaderBounds, GallerySpreadLayout.DetailMetadataBounds, "Detail metadata");
        Contains(GallerySpreadLayout.DetailHeaderBounds, GallerySpreadLayout.DetailThumbnailBounds, "Detail thumbnail");
        Disjoint("Detail header children", GallerySpreadLayout.DetailMetadataBounds, GallerySpreadLayout.DetailThumbnailBounds);
        Contains(GallerySpreadLayout.DetailMetadataBounds, GallerySpreadLayout.DetailEventIdBounds, "Detail event ID");
        Contains(GallerySpreadLayout.DetailMetadataBounds, GallerySpreadLayout.DetailLocationBounds, "Detail location");
        Disjoint("Metadata children", GallerySpreadLayout.DetailEventIdBounds, GallerySpreadLayout.DetailLocationBounds);
        (int X, int Y, int Width, int Height)[] detailRegions = [GallerySpreadLayout.TitleBounds, GallerySpreadLayout.DetailHeaderBounds,
            GallerySpreadLayout.ConditionHeadingBounds, GallerySpreadLayout.ConditionViewportBounds,
            GallerySpreadLayout.FooterBounds, GallerySpreadLayout.DetailScrollTrackBounds];
        foreach (var bounds in detailRegions)
            Contains(GallerySpreadLayout.RightPageBounds, bounds, "Detail content");
        Disjoint("Detail title, header, condition heading, viewport, footer and track", detailRegions);
        Contains(GallerySpreadLayout.FooterBounds, GallerySpreadLayout.BackButtonBounds, "Back button");
        Contains(GallerySpreadLayout.FooterBounds, GallerySpreadLayout.ReplayButtonBounds, "Replay button");
        Disjoint("Footer buttons", GallerySpreadLayout.BackButtonBounds, GallerySpreadLayout.ReplayButtonBounds);
        Check(GallerySpreadLayout.FooterBaseline >= GallerySpreadLayout.FooterBounds.Y
            && GallerySpreadLayout.FooterBaseline < GallerySpreadLayout.FooterBounds.Y + GallerySpreadLayout.FooterBounds.Height,
            "Footer baseline escaped footer.");
        Check(GallerySpreadLayout.AlbumScrollTrackBounds.X == GallerySpreadLayout.ScrollbarX
            && GallerySpreadLayout.DetailScrollTrackBounds.X == GallerySpreadLayout.ScrollbarX,
            "Scrollbar tracks lost shared alignment.");

        foreach (int invalid in new[] { int.MinValue, -1, 6, int.MaxValue })
        {
            ThrowsOutOfRange(() => GallerySpreadLayout.LeftRowBounds(invalid), "row");
            ThrowsOutOfRange(() => GallerySpreadLayout.EventCardBounds(invalid), "slot");
            ThrowsOutOfRange(() => GallerySpreadLayout.EventCardHeaderBounds(invalid), "slot");
            ThrowsOutOfRange(() => GallerySpreadLayout.EventCardDetailsBounds(invalid), "slot");
            ThrowsOutOfRange(() => GallerySpreadLayout.EventCardThumbnailBounds(invalid), "slot");
        }

        Check(GallerySpreadLayout.IconSize == 16, "Icon size changed.");
        Check(GallerySpreadLayout.ConditionCheckSource == (0, 0, 16, 16), "Check atlas source changed.");
        Check(GallerySpreadLayout.ConditionCrossSource == (16, 0, 16, 16), "Cross atlas source changed.");
        Check(GallerySpreadLayout.ConditionQuestionSource == (32, 0, 16, 16), "Unknown must use the question atlas cell.");
        Check(GallerySpreadLayout.ReplayGlyphSource == (0, 0, 16, 16), "Replay source changed.");
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
            (GalleryUiAssets.EventAlbum, "assets/GalleryEventAlbum-v3.png", 1672, 941),
            (GalleryUiAssets.EventDetail, "assets/GalleryEventDetail-v3.png", 1672, 941),
            (GalleryUiAssets.ConditionStatusIcons, "assets/ConditionStatusIcons.png", 48, 16),
            (GalleryUiAssets.ReplayGlyph, "assets/ReplayGlyph.png", 16, 16),
            (GalleryUiAssets.EventSlotFrame, "assets/EventSlotFrameOverlay.png", 345, 205),
            (GalleryUiAssets.ScrollbarTrack, "assets/ScrollbarTrack.png", 24, 640),
            (EventThumbnailAsset.Placeholder, "assets/EventPlaceholder.png", 640, 360)
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

        Console.WriteLine("Gallery spread checks passed (six cards, detail regions, fixed left panel, atlas and seven PNG headers).");
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
