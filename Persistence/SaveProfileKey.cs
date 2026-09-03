namespace StardewGallery;

internal readonly record struct SaveProfileKey(
    ulong FarmUniqueId,
    long PlayerUniqueId
)
{
    internal long StoredFarmUniqueId => unchecked((long)FarmUniqueId);

    internal static ulong RestoreFarmUniqueId(long stored) => unchecked((ulong)stored);
}
