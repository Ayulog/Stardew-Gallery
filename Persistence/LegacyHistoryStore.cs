using StardewModdingAPI;

namespace StardewGallery;

internal sealed class LegacyHistoryStore(IModHelper helper)
{
    internal const string SaveKey = "watched-event-versions";

    internal bool TryLoad(out IReadOnlyList<WatchedEventSnapshot> snapshots)
    {
        snapshots = [];
        try
        {
            string? payload = helper.Data.ReadSaveData<string>(SaveKey);
            return LegacyHistoryCodec.TryDecode(payload, out snapshots);
        }
        catch
        {
            snapshots = [];
            return false;
        }
    }

    internal bool TrySave(IReadOnlyList<WatchedEventSnapshot> snapshots)
    {
        if (!LegacyHistoryCodec.TryEncode(snapshots, out string payload))
            return false;
        try
        {
            helper.Data.WriteSaveData(SaveKey, payload);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
