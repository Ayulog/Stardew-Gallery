using System.IO.Compression;
using System.Text.Json;

namespace StardewGallery;

internal static class LegacyHistoryCodec
{
    internal static bool TryDecode(string? payload, out IReadOnlyList<WatchedEventSnapshot> snapshots)
    {
        snapshots = [];
        try
        {
            if (string.IsNullOrWhiteSpace(payload))
                return true;
            using MemoryStream source = new(Convert.FromBase64String(payload));
            using GZipStream gzip = new(source, CompressionMode.Decompress);
            List<WatchedEventSnapshot>? saved = JsonSerializer.Deserialize<List<WatchedEventSnapshot>>(gzip);
            snapshots = saved ?? [];
            return true;
        }
        catch
        {
            snapshots = [];
            return false;
        }
    }

    internal static bool TryEncode(IReadOnlyList<WatchedEventSnapshot> snapshots, out string payload)
    {
        payload = "";
        try
        {
            using MemoryStream target = new();
            using (GZipStream gzip = new(target, CompressionLevel.SmallestSize, leaveOpen: true))
                JsonSerializer.Serialize(gzip, snapshots);
            payload = Convert.ToBase64String(target.ToArray());
            return true;
        }
        catch
        {
            payload = "";
            return false;
        }
    }
}
