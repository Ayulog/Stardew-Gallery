# Requires PowerShell 7 on Windows; System.Drawing is supplied by the runtime.
# Reads and rewrites only this repository's own art in assets/. No game assets.
#
# Run with -Check to report without writing.
# - Derives the small tab-button icon and the portrait placeholder from assets/GalleryTabIcon-horizontal-v5.png.
# - Re-encodes PNGs losslessly (smaller zlib streams, better row filters). Every other chunk is kept as is, and
#   the decoded pixels are compared before anything is written. Files with C2PA content credentials (caBX) are
#   left byte-for-byte unchanged, because the credential is bound to the file's bytes.
[CmdletBinding()]
param([switch] $Check)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Add-Type -AssemblyName System.Drawing.Common
$source = @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;

public static class GalleryPngOptimizer
{
    private static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
    private static readonly uint[] CrcTable = Enumerable.Range(0, 256).Select(n =>
    {
        uint c = (uint)n;
        for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
        return c;
    }).ToArray();

    private sealed record Chunk(string Type, byte[] Data);
    private sealed record Image(int Width, int Height, byte ColorType, int Channels, byte[] Pixels);

    /// <summary>Losslessly re-encode one PNG; returns a one-line report.</summary>
    public static string Optimize(string path, bool write)
    {
        byte[] original = File.ReadAllBytes(path);
        List<Chunk> chunks = Parse(original);
        if (chunks.Any(chunk => chunk.Type == "caBX"))
            return $"Kept (content credentials): {Path.GetFileName(path)} {original.Length:N0} bytes";
        Image image = Decode(chunks);
        byte[] encoded = Encode(chunks, image);
        Image check = Decode(Parse(encoded));
        if (check.Width != image.Width || check.Height != image.Height || check.ColorType != image.ColorType || !check.Pixels.AsSpan().SequenceEqual(image.Pixels))
            throw new InvalidOperationException("Re-encoded pixels differ: " + path);
        if (encoded.Length >= original.Length)
            return $"Kept (already small): {Path.GetFileName(path)} {original.Length:N0} bytes";
        if (write) File.WriteAllBytes(path, encoded);
        return $"{(write ? "Optimized" : "Would optimize")}: {Path.GetFileName(path)} {original.Length:N0} -> {encoded.Length:N0} bytes";
    }

    /// <summary>Resize owned art with high-quality filtering, then write it as an optimized PNG.</summary>
    public static string Derive(string sourcePath, string targetPath, int width, int height, bool write)
    {
        using var sourceImage = new Bitmap(sourcePath);
        using var target = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(target))
        using (var attributes = new ImageAttributes())
        {
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            attributes.SetWrapMode(WrapMode.TileFlipXY);
            graphics.DrawImage(sourceImage, new Rectangle(0, 0, width, height), 0, 0, sourceImage.Width, sourceImage.Height, GraphicsUnit.Pixel, attributes);
        }
        var data = target.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        byte[] bgra = new byte[width * height * 4];
        try
        {
            for (int y = 0; y < height; y++)
                System.Runtime.InteropServices.Marshal.Copy(data.Scan0 + y * data.Stride, bgra, y * width * 4, width * 4);
        }
        finally { target.UnlockBits(data); }
        byte[] rgba = new byte[bgra.Length];
        for (int i = 0; i < bgra.Length; i += 4)
        {
            rgba[i] = bgra[i + 2]; rgba[i + 1] = bgra[i + 1]; rgba[i + 2] = bgra[i]; rgba[i + 3] = bgra[i + 3];
        }
        var header = new Chunk("IHDR", Header(width, height, 6));
        byte[] encoded = Encode(new List<Chunk> { header, new("IEND", Array.Empty<byte>()) }, new Image(width, height, 6, 4, rgba));
        bool same = File.Exists(targetPath) && File.ReadAllBytes(targetPath).AsSpan().SequenceEqual(encoded);
        if (write && !same) File.WriteAllBytes(targetPath, encoded);
        return $"{(same ? "Up to date" : write ? "Derived" : "Would derive")}: {Path.GetFileName(targetPath)} {width}x{height}, {encoded.Length:N0} bytes";
    }

    private static List<Chunk> Parse(byte[] file)
    {
        if (file.Length < 8 || !file.AsSpan(0, 8).SequenceEqual(Signature)) throw new InvalidDataException("Not a PNG file.");
        var chunks = new List<Chunk>();
        for (int offset = 8; offset < file.Length;)
        {
            int length = (int)ReadUInt32(file, offset);
            string type = System.Text.Encoding.ASCII.GetString(file, offset + 4, 4);
            chunks.Add(new Chunk(type, file.AsSpan(offset + 8, length).ToArray()));
            offset += 12 + length;
            if (type == "IEND") break;
        }
        if (chunks.Count == 0 || chunks[0].Type != "IHDR") throw new InvalidDataException("Missing IHDR.");
        return chunks;
    }

    private static Image Decode(List<Chunk> chunks)
    {
        byte[] header = chunks[0].Data;
        int width = (int)ReadUInt32(header, 0), height = (int)ReadUInt32(header, 4);
        byte depth = header[8], colorType = header[9], interlace = header[12];
        int channels = colorType switch { 0 => 1, 2 => 3, 4 => 2, 6 => 4, _ => 0 };
        if (depth != 8 || interlace != 0 || channels == 0) throw new NotSupportedException("Only non-interlaced 8-bit gray, RGB and RGBA PNGs are supported.");
        using var compressed = new MemoryStream();
        foreach (Chunk chunk in chunks.Where(chunk => chunk.Type == "IDAT")) compressed.Write(chunk.Data);
        compressed.Position = 0;
        using var inflater = new ZLibStream(compressed, CompressionMode.Decompress);
        using var raw = new MemoryStream();
        inflater.CopyTo(raw);
        byte[] filtered = raw.ToArray();
        int stride = width * channels;
        if (filtered.Length != (stride + 1) * height) throw new InvalidDataException("Unexpected image data length.");
        byte[] pixels = new byte[stride * height];
        for (int y = 0; y < height; y++)
        {
            byte filter = filtered[y * (stride + 1)];
            for (int x = 0; x < stride; x++)
            {
                int a = x >= channels ? pixels[y * stride + x - channels] : 0;
                int b = y > 0 ? pixels[(y - 1) * stride + x] : 0;
                int c = x >= channels && y > 0 ? pixels[(y - 1) * stride + x - channels] : 0;
                int value = filtered[y * (stride + 1) + 1 + x];
                pixels[y * stride + x] = (byte)(filter switch
                {
                    0 => value,
                    1 => value + a,
                    2 => value + b,
                    3 => value + (a + b) / 2,
                    4 => value + Paeth(a, b, c),
                    _ => throw new InvalidDataException("Unknown row filter.")
                });
            }
        }
        return new Image(width, height, colorType, channels, pixels);
    }

    private static byte[] Encode(List<Chunk> chunks, Image image)
    {
        int stride = image.Width * image.Channels;
        // Try the per-row minimum-sum heuristic and each single filter; keep the smallest stream.
        byte[] best = null;
        foreach (int strategy in new[] { -1, 0, 1, 2, 3, 4 })
        {
            byte[] filtered = new byte[(stride + 1) * image.Height];
            byte[] row = new byte[stride];
            for (int y = 0; y < image.Height; y++)
            {
                int chosen = strategy;
                if (strategy < 0)
                {
                    long bestSum = long.MaxValue;
                    for (int filter = 0; filter <= 4; filter++)
                    {
                        FilterRow(image, y, filter, row);
                        long sum = 0;
                        foreach (byte value in row) sum += value < 128 ? value : 256 - value;
                        if (sum < bestSum) { bestSum = sum; chosen = filter; }
                    }
                }
                FilterRow(image, y, chosen, row);
                filtered[y * (stride + 1)] = (byte)chosen;
                Buffer.BlockCopy(row, 0, filtered, y * (stride + 1) + 1, stride);
            }
            using var output = new MemoryStream();
            using (var deflater = new ZLibStream(output, CompressionLevel.SmallestSize, leaveOpen: true)) deflater.Write(filtered);
            byte[] stream = output.ToArray();
            if (best is null || stream.Length < best.Length) best = stream;
        }
        using var file = new MemoryStream();
        file.Write(Signature);
        bool wroteData = false;
        foreach (Chunk chunk in chunks)
        {
            if (chunk.Type == "IDAT")
            {
                if (!wroteData) WriteChunk(file, "IDAT", best);
                wroteData = true;
                continue;
            }
            if (chunk.Type == "IEND" && !wroteData) { WriteChunk(file, "IDAT", best); wroteData = true; }
            WriteChunk(file, chunk.Type, chunk.Data);
        }
        return file.ToArray();
    }

    private static void FilterRow(Image image, int y, int filter, byte[] row)
    {
        int stride = image.Width * image.Channels, channels = image.Channels;
        byte[] p = image.Pixels;
        for (int x = 0; x < stride; x++)
        {
            int value = p[y * stride + x];
            int a = x >= channels ? p[y * stride + x - channels] : 0;
            int b = y > 0 ? p[(y - 1) * stride + x] : 0;
            int c = x >= channels && y > 0 ? p[(y - 1) * stride + x - channels] : 0;
            row[x] = (byte)(filter switch { 0 => value, 1 => value - a, 2 => value - b, 3 => value - (a + b) / 2, _ => value - Paeth(a, b, c) });
        }
    }

    private static int Paeth(int a, int b, int c)
    {
        int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    private static byte[] Header(int width, int height, byte colorType)
    {
        byte[] header = new byte[13];
        WriteUInt32(header, 0, (uint)width); WriteUInt32(header, 4, (uint)height);
        header[8] = 8; header[9] = colorType;
        return header;
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        byte[] head = new byte[8];
        WriteUInt32(head, 0, (uint)data.Length);
        System.Text.Encoding.ASCII.GetBytes(type, 0, 4, head, 4);
        stream.Write(head);
        stream.Write(data);
        uint crc = 0xFFFFFFFFu;
        foreach (byte value in head.AsSpan(4, 4)) crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
        foreach (byte value in data) crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
        byte[] tail = new byte[4];
        WriteUInt32(tail, 0, crc ^ 0xFFFFFFFFu);
        stream.Write(tail);
    }

    private static uint ReadUInt32(byte[] data, int offset) => (uint)(data[offset] << 24 | data[offset + 1] << 16 | data[offset + 2] << 8 | data[offset + 3]);
    private static void WriteUInt32(byte[] data, int offset, uint value)
    {
        data[offset] = (byte)(value >> 24); data[offset + 1] = (byte)(value >> 16); data[offset + 2] = (byte)(value >> 8); data[offset + 3] = (byte)value;
    }
}
'@
$references = @('System.Drawing.Common', 'System.Drawing.Primitives', 'System.IO.Compression', 'System.Runtime.InteropServices', 'System.Linq', 'System.Collections', 'System.Memory')
foreach ($assembly in [System.Drawing.Bitmap].Assembly.GetReferencedAssemblies()) {
    if ($assembly.Name.StartsWith('System.Private.Windows.')) {
        $references += [System.Reflection.Assembly]::Load($assembly).Location
    }
}
Add-Type -TypeDefinition $source -ReferencedAssemblies $references

$write = -not $Check.IsPresent
$icon = Join-Path $root 'assets/GalleryTabIcon-horizontal-v5.png'
# The Collections-page button is drawn at 52x40 UI pixels; twice that stays sharp at high UI scales.
[GalleryPngOptimizer]::Derive($icon, (Join-Path $root 'assets/GalleryTabIcon-104x80.png'), 104, 80, $write)
# The portrait placeholder fills slots up to about 380x270 menu pixels.
[GalleryPngOptimizer]::Derive($icon, (Join-Path $root 'assets/GalleryTabIcon-640x490.png'), 640, 490, $write)
# The source icon (excluded from the package in StardewGallery.csproj) and the icons derived above are skipped.
$sources = @('GalleryTabIcon-horizontal-v5.png', 'GalleryTabIcon-104x80.png', 'GalleryTabIcon-640x490.png')
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $root 'assets') -Filter '*.png' -File | Sort-Object Name) {
    if ($file.Name -in $sources) { continue }
    [GalleryPngOptimizer]::Optimize($file.FullName, $write)
}
