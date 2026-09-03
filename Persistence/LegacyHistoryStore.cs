using System.IO.Compression;
using System.Text.Json;
using StardewModdingAPI;

namespace StardewGallery;

internal sealed class LegacyHistoryStore(IModHelper helper)
{
    internal const string SaveKey = "watched-event-versions";

    internal IReadOnlyList<WatchedEventSnapshot> Load()
    {
        string? payload = helper.Data.ReadSaveData<string>(SaveKey);
        if (string.IsNullOrWhiteSpace(payload))
            return [];
        using MemoryStream source = new(Convert.FromBase64String(payload));
        using GZipStream gzip = new(source, CompressionMode.Decompress);
        List<WatchedEventSnapshot>? saved = JsonSerializer.Deserialize<List<WatchedEventSnapshot>>(gzip);
        return saved ?? [];
    }

    internal bool TrySave(IReadOnlyList<WatchedEventSnapshot> snapshots)
    {
        try
        {
            using MemoryStream target = new();
            using (GZipStream gzip = new(target, CompressionLevel.SmallestSize, leaveOpen: true))
                JsonSerializer.Serialize(gzip, snapshots);
            helper.Data.WriteSaveData(SaveKey, Convert.ToBase64String(target.ToArray()));
            return true;
        }
        catch
        {
            return false;
        }
    }
}
