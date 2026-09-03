namespace StardewGallery;

internal sealed record ConditionGap(
    ConditionGapKind Kind,
    string? Target = null,
    string? Current = null,
    string? Detail = null
);
