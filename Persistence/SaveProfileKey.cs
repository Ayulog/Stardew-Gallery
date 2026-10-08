namespace StardewGallery;

internal readonly record struct SaveProfileKey(
    ulong FarmUniqueId,
    long PlayerUniqueId
);
