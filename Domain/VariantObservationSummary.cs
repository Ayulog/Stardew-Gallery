namespace StardewGallery;

internal sealed record VariantObservationSummary(
    ObservedVariantKey Variant,
    DateTimeOffset FirstObservedAt,
    DateTimeOffset LastObservedAt,
    string? LastObservedLocationName,
    string? LastObservedLocale
);
