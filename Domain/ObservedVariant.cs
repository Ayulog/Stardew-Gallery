namespace StardewGallery;

internal sealed record ObservedVariant(
    ObservedVariantKey Key,
    string RawEventKey,
    string RootScriptHash,
    HistoricalPlaybackBundle Playback
);
