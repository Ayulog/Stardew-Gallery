namespace StardewGallery;

internal sealed record HistoricalEventRecord(
    ObservedVariantKey Variant,
    DateTimeOffset WatchedAt,
    string? LocationName,
    string? Locale
)
{
    internal EventIdentity Identity => Variant.Identity;
}
