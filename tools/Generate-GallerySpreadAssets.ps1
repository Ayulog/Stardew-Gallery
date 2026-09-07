# Requires PowerShell 7 on Windows; System.Drawing is supplied by the runtime.
# Reads only this repository's owned art and shared BCL layout. No game assets.
[CmdletBinding()]
param([switch] $VerifyOnly)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Add-Type -AssemblyName System.Drawing.Common
$layout = [System.IO.File]::ReadAllText((Join-Path $root 'GallerySpreadLayout.cs'))
$generator = @'

public static class GallerySpreadAssetGenerator
{
    private static readonly System.Drawing.Color Ink = System.Drawing.Color.FromArgb(111, 79, 49);
    private static readonly System.Drawing.Color LightInk = System.Drawing.Color.FromArgb(181, 143, 94);
    private static readonly System.Drawing.Color Highlight = System.Drawing.Color.FromArgb(255, 227, 175);

    public static void Run(string root, bool verifyOnly)
    {
        using var source = new System.Drawing.Bitmap(System.IO.Path.Combine(root, "assets/GalleryDetail-alpha-v2.png"));
        Require(source.Width == GallerySpreadLayout.LogicalWidth && source.Height == GallerySpreadLayout.LogicalHeight, "Source dimensions changed.");
        ValidateLayout();
        using var paper = MakePaper(source);
        using var album = (System.Drawing.Bitmap)paper.Clone();
        for (int slot = 0; slot < GallerySpreadLayout.EventColumns * GallerySpreadLayout.EventVisibleRows; slot++)
        {
            Opening(album, GallerySpreadLayout.EventCardThumbnailBounds(slot));
            Corners(album, GallerySpreadLayout.EventCardBounds(slot), 15);
            Corners(album, GallerySpreadLayout.EventCardThumbnailBounds(slot), 10);
        }
        Track(album, source, GallerySpreadLayout.AlbumScrollTrackBounds);
        Footer(album);

        using var detail = (System.Drawing.Bitmap)paper.Clone();
        Opening(detail, GallerySpreadLayout.DetailThumbnailBounds);
        Corners(detail, GallerySpreadLayout.DetailThumbnailBounds, 10);
        var header = GallerySpreadLayout.DetailHeaderBounds;
        var heading = GallerySpreadLayout.ConditionHeadingBounds;
        Rule(detail, header.X, header.Y + header.Height + 10, header.Width);
        Rule(detail, heading.X, heading.Y + heading.Height + 5, heading.Width);
        Track(detail, source, GallerySpreadLayout.DetailScrollTrackBounds);
        Footer(detail);

        using var icons = new System.Drawing.Bitmap(48, 16, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        Glyph(icons, GallerySpreadLayout.ConditionCheckSource.X, new[] {
            "................", "................", "................", "...........##...",
            "..........###...", ".........###....", "........###.....", "...##..###......",
            "...######.......", "....####........", ".....##.........", "................",
            "................", "................", "................", "................"
        }, System.Drawing.Color.FromArgb(111, 143, 88));
        Glyph(icons, GallerySpreadLayout.ConditionCrossSource.X, new[] {
            "................", "................", "................", "...##.....##....",
            "...###...###....", "....###.###.....", ".....#####......", "......###.......",
            ".....#####......", "....###.###.....", "...###...###....", "...##.....##....",
            "................", "................", "................", "................"
        }, System.Drawing.Color.FromArgb(178, 99, 84));
        Glyph(icons, GallerySpreadLayout.ConditionQuestionSource.X, new[] {
            "................", "................", ".....#####......", "....#######.....",
            "....##...##.....", ".........##.....", "........###.....", ".......###......",
            "......###.......", "......##........", "................", "......##........",
            "......##........", "................", "................", "................"
        }, System.Drawing.Color.FromArgb(188, 150, 69));
        using var replay = new System.Drawing.Bitmap(16, 16, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        Glyph(replay, 0, new[] {
            "................", "................", "....#...........", "....###.........",
            "....#####.......", "....#######.....", "....########....", "....#########...",
            "....#########...", "....########....", "....#######.....", "....#####.......",
            "....###.........", "....#...........", "................", "................"
        }, System.Drawing.Color.FromArgb(145, 103, 63));
        using var placeholder = MakePlaceholder();

        VerifyLeft(source, album);
        VerifyLeft(source, detail);
        for (int slot = 0; slot < GallerySpreadLayout.EventColumns * GallerySpreadLayout.EventVisibleRows; slot++)
            VerifyOpening(album, GallerySpreadLayout.EventCardThumbnailBounds(slot), "album thumbnail " + slot);
        VerifyOpening(detail, GallerySpreadLayout.DetailThumbnailBounds, "detail thumbnail");
        VerifyPaperContinuity(paper);
        Require(detail.GetPixel(755, 590).ToArgb() == paper.GetPixel(755, 590).ToArgb(), "Detail contains an album corner.");
        Store(root, GalleryUiAssets.EventAlbum, album, verifyOnly);
        Store(root, GalleryUiAssets.EventDetail, detail, verifyOnly);
        Store(root, GalleryUiAssets.ConditionStatusIcons, icons, verifyOnly);
        Store(root, GalleryUiAssets.ReplayGlyph, replay, verifyOnly);
        Store(root, "assets/EventPlaceholder.png", placeholder, verifyOnly);
        System.Console.WriteLine("PASS: layout containment, six rows/cards, exact left-page RGBA, portrait alpha mask, separate detail art, opaque 640x360 scenic placeholder, five PNG round-trip pixels.");
    }

    private static System.Drawing.Bitmap MakePlaceholder()
    {
        // Original 160x90 pixel composition; integer 4x enlargement, no filtering.
        using var scene = new System.Drawing.Bitmap(160, 90, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var graphics = System.Drawing.Graphics.FromImage(scene);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
        var sky = System.Drawing.Color.FromArgb(202, 216, 181);
        var cloud = System.Drawing.Color.FromArgb(235, 230, 196);
        var sun = System.Drawing.Color.FromArgb(245, 224, 162);
        var distant = System.Drawing.Color.FromArgb(153, 180, 153);
        var ridge = System.Drawing.Color.FromArgb(120, 157, 137);
        var teal = System.Drawing.Color.FromArgb(80, 129, 115);
        var darkTeal = System.Drawing.Color.FromArgb(54, 99, 89);
        var leaf = System.Drawing.Color.FromArgb(107, 145, 101);
        var lightLeaf = System.Drawing.Color.FromArgb(147, 170, 113);
        var meadow = System.Drawing.Color.FromArgb(167, 183, 121);
        var grass = System.Drawing.Color.FromArgb(129, 157, 103);
        var straw = System.Drawing.Color.FromArgb(199, 197, 137);
        var bank = System.Drawing.Color.FromArgb(217, 204, 153);
        var water = System.Drawing.Color.FromArgb(125, 174, 160);
        var ripple = System.Drawing.Color.FromArgb(180, 207, 179);
        var wood = System.Drawing.Color.FromArgb(112, 106, 75);

        void Rect(System.Drawing.Color color, int x, int y, int width, int height)
        {
            using var brush = new System.Drawing.SolidBrush(color);
            graphics.FillRectangle(brush, x, y, width, height);
        }
        void Shape(System.Drawing.Color color, params int[] xy)
        {
            var points = new System.Drawing.Point[xy.Length / 2];
            for (int i = 0; i < points.Length; i++) points[i] = new System.Drawing.Point(xy[i * 2], xy[i * 2 + 1]);
            using var brush = new System.Drawing.SolidBrush(color);
            graphics.FillPolygon(brush, points);
        }
        void Pine(int x, int y, int size, System.Drawing.Color shade, System.Drawing.Color light)
        {
            Rect(wood, x - 1, y - 3, 2, 6);
            Shape(shade, x, y - size, x + 2, y - size, x + 2, y - size + 4,
                x + 5, y - size + 8, x + 3, y - size + 8, x + 7, y - size / 2,
                x + 5, y - size / 2, x + 10, y - 2, x + 10, y, x - 10, y,
                x - 10, y - 2, x - 5, y - size / 2, x - 7, y - size / 2,
                x - 3, y - size + 8, x - 5, y - size + 8, x - 2, y - size + 4, x - 2, y - size + 2);
            Shape(light, x, y - size + 3, x, y - size + 10, x - 4, y - size + 10);
            Shape(light, x - 1, y - size / 2 - 3, x - 1, y - size / 2 + 3, x - 6, y - size / 2 + 3);
            Rect(light, x - 7, y - 3, 5, 2);
        }

        graphics.Clear(sky);
        Rect(sun, 109, 10, 8, 12);
        Rect(sun, 107, 12, 12, 8);
        Rect(cloud, 19, 15, 18, 3);
        Rect(cloud, 23, 12, 9, 3);
        Rect(cloud, 15, 18, 28, 2);
        Rect(cloud, 70, 8, 16, 2);
        Rect(cloud, 75, 6, 7, 2);
        Rect(cloud, 124, 25, 27, 2);
        Rect(cloud, 132, 22, 11, 3);

        Shape(distant, 0, 37, 12, 37, 12, 34, 22, 34, 22, 31, 31, 31, 31, 28,
            39, 28, 39, 25, 44, 25, 44, 28, 50, 28, 50, 31, 57, 31, 57, 34,
            66, 34, 66, 38, 87, 38, 87, 35, 99, 35, 99, 32, 107, 32, 107, 30,
            116, 30, 116, 33, 126, 33, 126, 36, 144, 36, 144, 34, 160, 34, 160, 60, 0, 60);
        Shape(ridge, 0, 44, 16, 44, 16, 41, 29, 41, 29, 39, 42, 39, 42, 41,
            53, 41, 53, 44, 65, 44, 65, 47, 88, 47, 88, 44, 103, 44, 103, 41,
            119, 41, 119, 39, 138, 39, 138, 42, 160, 42, 160, 67, 0, 67);
        for (int i = 0; i < 15; i++)
        {
            int x = i * 12 - 4;
            int y = 49 + (i * 7 % 5);
            Shape(teal, x, y, x + 2, y - 5 - i % 3, x + 4, y - 2, x + 6, y - 7,
                x + 9, y - 2, x + 12, y, x + 12, y + 8, x, y + 8);
        }
        Shape(meadow, 0, 54, 22, 54, 22, 56, 45, 56, 45, 58, 74, 58,
            89, 53, 107, 53, 107, 51, 129, 51, 129, 53, 160, 53, 160, 90, 0, 90);
        Shape(straw, 0, 67, 22, 67, 22, 65, 43, 65, 43, 67, 65, 67,
            65, 70, 43, 70, 43, 72, 13, 72, 13, 74, 0, 74);
        Shape(grass, 105, 62, 124, 62, 124, 60, 148, 60, 148, 58, 160, 58,
            160, 90, 114, 90, 114, 83, 100, 83, 100, 71, 105, 71);
        Shape(bank, 82, 54, 88, 54, 80, 59, 76, 62, 78, 64, 93, 67, 99, 71,
            99, 75, 89, 81, 80, 85, 81, 90, 47, 90, 53, 85, 70, 79, 83, 74,
            84, 71, 70, 67, 68, 63, 73, 59);
        Shape(water, 84, 54, 87, 54, 77, 61, 74, 63, 79, 66, 92, 69, 95, 72,
            94, 75, 84, 80, 74, 86, 74, 90, 55, 90, 61, 85, 78, 78, 87, 74,
            88, 72, 83, 69, 72, 66, 71, 63, 75, 59);
        foreach (var r in new[] { (79, 58, 4), (74, 63, 3), (83, 70, 8), (87, 73, 5),
            (78, 78, 7), (70, 82, 8), (62, 87, 10) })
            Rect(ripple, r.Item1, r.Item2, r.Item3, 1);

        Pine(7, 65, 36, darkTeal, teal);
        Pine(24, 62, 28, teal, ridge);
        Pine(39, 66, 24, teal, ridge);
        Pine(15, 74, 33, darkTeal, teal);
        Pine(153, 65, 31, darkTeal, teal);
        Pine(137, 65, 24, teal, ridge);

        Rect(wood, 118, 59, 3, 15);
        Shape(wood, 119, 68, 112, 61, 114, 61, 121, 66, 126, 60, 128, 60, 121, 70);
        Rect(leaf, 106, 48, 27, 12);
        Rect(leaf, 110, 43, 18, 20);
        Rect(lightLeaf, 110, 45, 12, 7);
        Rect(lightLeaf, 106, 50, 7, 5);
        Rect(lightLeaf, 115, 42, 8, 3);
        Rect(teal, 123, 55, 10, 5);
        Rect(teal, 115, 60, 13, 3);
        Rect(straw, 115, 46, 4, 2);

        Shape(leaf, 0, 83, 11, 83, 11, 80, 23, 80, 23, 83, 33, 83, 33, 87,
            44, 87, 44, 90, 0, 90);
        Shape(leaf, 119, 90, 119, 86, 131, 86, 131, 82, 145, 82, 145, 80, 160, 80, 160, 90);
        for (int i = 0; i < 65; i++)
        {
            int x = (i * 43 + 5) % 160;
            int y = 58 + (i * 17 % 32);
            if (scene.GetPixel(x, y).ToArgb() != meadow.ToArgb()
                && scene.GetPixel(x, y).ToArgb() != grass.ToArgb()) continue;
            Rect(i % 3 == 0 ? straw : leaf, x, y, 2, 1);
            if (i % 5 == 0)
            {
                Rect(leaf, x, y - 1, 1, 2);
                Rect(cloud, x, y - 2, 1, 1);
            }
        }

        graphics.Flush();
        var result = new System.Drawing.Bitmap(640, 360, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        for (int y = 0; y < result.Height; y++)
        for (int x = 0; x < result.Width; x++)
        {
            var pixel = scene.GetPixel(x / 4, y / 4);
            Require(pixel.A == 255, "Placeholder must be full-bleed and opaque.");
            result.SetPixel(x, y, pixel);
        }
        return result;
    }

    private static System.Drawing.Bitmap MakePaper(System.Drawing.Bitmap source)
    {
        var result = (System.Drawing.Bitmap)source.Clone();
        // Mirror a clean parchment patch from our own source. Feather only outside
        // the old frames; retain the title, binding, page edges and whole left page.
        for (int y = 128; y < 840; y++)
        for (int x = 735; x < 1550; x++)
        {
            int tx = (x - 735) % 1280;
            int ty = (y - 128) % 200;
            var tile = source.GetPixel(806 + (tx < 640 ? tx : 1279 - tx), 170 + (ty < 100 ? ty : 199 - ty));
            var old = source.GetPixel(x, y);
            int weight = Math.Min(10, Math.Min(Math.Min(x - 735, 1549 - x), Math.Min(y - 128, 839 - y)));
            int r = (old.R * (10 - weight) + tile.R * weight) / 10;
            int g = (old.G * (10 - weight) + tile.G * weight) / 10;
            int b = (old.B * (10 - weight) + tile.B * weight) / 10;
            result.SetPixel(x, y, System.Drawing.Color.FromArgb(255, r, g, b));
        }
        return result;
    }

    private static void Opening(System.Drawing.Bitmap image, (int X, int Y, int Width, int Height) bounds)
    {
        for (int y = bounds.Y; y < bounds.Y + bounds.Height; y++)
        for (int x = bounds.X; x < bounds.X + bounds.Width; x++)
            image.SetPixel(x, y, System.Drawing.Color.Transparent);
    }

    private static void Corners(System.Drawing.Bitmap image, (int X, int Y, int Width, int Height) bounds, int length)
    {
        foreach (int dx in new[] { 0, 1 })
        foreach (int dy in new[] { 0, 1 })
        {
            int x = bounds.X + dx * (bounds.Width - 1);
            int y = bounds.Y + dy * (bounds.Height - 1);
            int sx = dx == 0 ? 1 : -1;
            int sy = dy == 0 ? 1 : -1;
            for (int i = 0; i < length; i++)
            {
                image.SetPixel(x + sx * i, y, Ink);
                image.SetPixel(x, y + sy * i, Ink);
                image.SetPixel(x + sx * i, y + sy, LightInk);
                image.SetPixel(x + sx, y + sy * i, LightInk);
                if (i > 1)
                {
                    image.SetPixel(x + sx * i, y + sy * 2, Highlight);
                    image.SetPixel(x + sx * 2, y + sy * i, Highlight);
                }
            }
        }
    }

    private static void Rule(System.Drawing.Bitmap image, int x, int y, int width)
    {
        for (int i = 0; i < width; i++)
        {
            image.SetPixel(x + i, y, LightInk);
            image.SetPixel(x + i, y + 1, Highlight);
        }
    }

    private static void Track(System.Drawing.Bitmap image, System.Drawing.Bitmap source, (int X, int Y, int Width, int Height) bounds)
    {
        for (int y = bounds.Y; y < bounds.Y + bounds.Height; y++)
        for (int x = 0; x < bounds.Width; x++)
            image.SetPixel(bounds.X + x, y, source.GetPixel(1534 + x, 146 + (y - bounds.Y) % 640));
    }

    private static void VerifyOpening(System.Drawing.Bitmap image, (int X, int Y, int Width, int Height) bounds, string name)
    {
        int transparent = 0;
        for (int y = bounds.Y; y < bounds.Y + bounds.Height; y++)
        for (int x = bounds.X; x < bounds.X + bounds.Width; x++)
            if (image.GetPixel(x, y).A == 0) transparent++;
        Require(transparent > bounds.Width * bounds.Height * .8, name + " opening is not mostly transparent.");
        Require(image.GetPixel(bounds.X, bounds.Y).A != 0 && image.GetPixel(bounds.X + bounds.Width - 1, bounds.Y + bounds.Height - 1).A != 0,
            name + " frame corners are missing.");
    }

    private static void VerifyPaperContinuity(System.Drawing.Bitmap paper)
    {
        long difference = 0;
        int samples = 0;
        for (int y = 138; y < 830; y += 7)
        foreach (int x in new[] { 734, 735, 1549, 1550 })
        {
            var a = paper.GetPixel(x, y);
            var b = paper.GetPixel(x + (x is 734 or 1549 ? 1 : -1), y);
            difference += Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B);
            samples += 3;
        }
        Require(difference / (double)samples < 12, "Clean-paper patch edge color discontinuity is too large.");
    }

    private static void Footer(System.Drawing.Bitmap image)
    {
        var footer = GallerySpreadLayout.FooterBounds;
        Rule(image, footer.X, footer.Y - 6, footer.Width);
    }

    private static void Glyph(System.Drawing.Bitmap image, int offset, string[] mask, System.Drawing.Color fill)
    {
        Require(mask.Length == 16, "Glyph height must be 16.");
        for (int y = 0; y < 16; y++)
        {
            Require(mask[y].Length == 16, "Glyph width must be 16.");
            for (int x = 0; x < 16; x++)
            {
                if (mask[y][x] != '#') continue;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    if (Math.Abs(dx) + Math.Abs(dy) <= 1 && x + dx >= 0 && x + dx < 16 && y + dy >= 0 && y + dy < 16)
                        image.SetPixel(offset + x + dx, y + dy, Ink);
            }
        }
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 16; x++)
            if (mask[y][x] == '#') image.SetPixel(offset + x, y, fill);
    }

    private static void VerifyLeft(System.Drawing.Bitmap source, System.Drawing.Bitmap image)
    {
        for (int y = 0; y < source.Height; y++)
        for (int x = 0; x < 700; x++)
            Require(source.GetPixel(x, y).ToArgb() == image.GetPixel(x, y).ToArgb(), "Left-page pixels changed.");
        var portrait = GallerySpreadLayout.PortraitBounds;
        int transparent = 0;
        for (int y = portrait.Y; y < portrait.Y + portrait.Height; y++)
        for (int x = portrait.X; x < portrait.X + portrait.Width; x++)
        {
            Require(source.GetPixel(x, y).A == image.GetPixel(x, y).A, "Portrait alpha mask changed.");
            if (image.GetPixel(x, y).A == 0) transparent++;
        }
        Require(transparent > portrait.Width * portrait.Height * 0.8, "Portrait opening is not transparent.");
        System.Console.WriteLine("Portrait: " + transparent + " fully transparent pixels; frame overlay preserved.");
    }

    private static void ValidateLayout()
    {
        var page = GallerySpreadLayout.RightPageBounds;
        Require(Contains(page, GallerySpreadLayout.TitleBounds), "Title outside page.");
        for (int row = 0; row < GallerySpreadLayout.LeftRowCount; row++)
            Require(Contains(GallerySpreadLayout.LeftPageBounds, GallerySpreadLayout.LeftRowBounds(row)), "Left row outside page.");
        for (int slot = 0; slot < 6; slot++)
        {
            var card = GallerySpreadLayout.EventCardBounds(slot);
            Require(Contains(page, card), "Card outside page.");
            Require(Contains(card, GallerySpreadLayout.EventCardHeaderBounds(slot)), "Card header outside card.");
            Require(Contains(card, GallerySpreadLayout.EventCardDetailsBounds(slot)), "Details outside card.");
            Require(Contains(card, GallerySpreadLayout.EventCardThumbnailBounds(slot)), "Thumbnail outside card.");
            Require(!Overlaps(GallerySpreadLayout.EventCardHeaderBounds(slot), GallerySpreadLayout.EventCardDetailsBounds(slot)), "Card header overlaps details.");
            Require(!Overlaps(GallerySpreadLayout.EventCardThumbnailBounds(slot), GallerySpreadLayout.EventCardHeaderBounds(slot))
                && !Overlaps(GallerySpreadLayout.EventCardThumbnailBounds(slot), GallerySpreadLayout.EventCardDetailsBounds(slot)), "Thumbnail overlaps card actions.");
            Require(!Overlaps(card, GallerySpreadLayout.AlbumScrollTrackBounds) && !Overlaps(card, GallerySpreadLayout.FooterBounds), "Card overlaps track/footer.");
            for (int other = 0; other < slot; other++)
                Require(!Overlaps(card, GallerySpreadLayout.EventCardBounds(other)), "Cards overlap.");
        }
        Require(Contains(GallerySpreadLayout.DetailHeaderBounds, GallerySpreadLayout.DetailMetadataBounds), "Metadata outside header.");
        Require(Contains(GallerySpreadLayout.DetailMetadataBounds, GallerySpreadLayout.DetailEventIdBounds), "Event ID outside metadata.");
        Require(Contains(GallerySpreadLayout.DetailMetadataBounds, GallerySpreadLayout.DetailLocationBounds), "Location outside metadata.");
        Require(!Overlaps(GallerySpreadLayout.DetailEventIdBounds, GallerySpreadLayout.DetailLocationBounds), "Metadata rows overlap.");
        Require(Contains(GallerySpreadLayout.DetailHeaderBounds, GallerySpreadLayout.DetailThumbnailBounds), "Thumbnail outside header.");
        Require(!Overlaps(GallerySpreadLayout.DetailMetadataBounds, GallerySpreadLayout.DetailThumbnailBounds), "Metadata overlaps thumbnail.");
        var regions = new[] { GallerySpreadLayout.DetailHeaderBounds, GallerySpreadLayout.ConditionHeadingBounds,
            GallerySpreadLayout.ConditionViewportBounds, GallerySpreadLayout.DetailScrollTrackBounds, GallerySpreadLayout.FooterBounds };
        for (int i = 0; i < regions.Length; i++)
        {
            Require(Contains(page, regions[i]), "Detail region outside page.");
            for (int j = 0; j < i; j++) Require(!Overlaps(regions[i], regions[j]), "Detail regions overlap.");
        }
        Require(Contains(GallerySpreadLayout.FooterBounds, GallerySpreadLayout.BackButtonBounds)
            && Contains(GallerySpreadLayout.FooterBounds, GallerySpreadLayout.ReplayButtonBounds), "Action outside footer.");
        Require(!Overlaps(GallerySpreadLayout.BackButtonBounds, GallerySpreadLayout.ReplayButtonBounds), "Footer actions overlap.");
    }

    private static bool Contains((int X, int Y, int Width, int Height) outer, (int X, int Y, int Width, int Height) inner)
        => inner.Width > 0 && inner.Height > 0 && inner.X >= outer.X && inner.Y >= outer.Y
            && inner.X + inner.Width <= outer.X + outer.Width && inner.Y + inner.Height <= outer.Y + outer.Height;

    private static bool Overlaps((int X, int Y, int Width, int Height) a, (int X, int Y, int Width, int Height) b)
        => a.X < b.X + b.Width && b.X < a.X + a.Width && a.Y < b.Y + b.Height && b.Y < a.Y + a.Height;

    private static void Store(string root, string relativePath, System.Drawing.Bitmap expected, bool verifyOnly)
    {
        string path = System.IO.Path.Combine(root, relativePath);
        if (!verifyOnly)
        {
            using var encoded = new System.IO.MemoryStream();
            expected.Save(encoded, System.Drawing.Imaging.ImageFormat.Png);
            System.IO.File.WriteAllBytes(path, encoded.ToArray());
        }
        using var actual = new System.Drawing.Bitmap(path);
        Require(actual.Width == expected.Width && actual.Height == expected.Height, "Incorrect PNG dimensions: " + relativePath);
        for (int y = 0; y < actual.Height; y++)
        for (int x = 0; x < actual.Width; x++)
            Require(actual.GetPixel(x, y).ToArgb() == expected.GetPixel(x, y).ToArgb(), "PNG pixel mismatch: " + relativePath);
        System.Console.WriteLine((verifyOnly ? "Verified " : "Generated ") + relativePath + " (" + actual.Width + "x" + actual.Height + ")");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
'@
$references = @('System.Drawing.Common', 'System.Drawing.Primitives', 'System.Console')
foreach ($assembly in [System.Drawing.Bitmap].Assembly.GetReferencedAssemblies()) {
    if ($assembly.Name.StartsWith('System.Private.Windows.')) {
        $references += [System.Reflection.Assembly]::Load($assembly).Location
    }
}
Add-Type -TypeDefinition ($layout + $generator) -ReferencedAssemblies $references
[StardewGallery.GallerySpreadAssetGenerator]::Run($root, $VerifyOnly.IsPresent)
