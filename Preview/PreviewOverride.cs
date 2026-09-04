namespace StardewGallery;

internal enum PreviewOverrideKind
{
    Friendship,
    EventSeen,
    Mail,
    Season,
    DayOfMonth,
    Year,
    Time,
    Weather,
    Dating,
    Spouse,
    Roommate,
    WorldState
}

internal sealed record PreviewOverride(
    PreviewOverrideKind Kind,
    string? Key,
    int? Value,
    string? Target
);

internal sealed record PreviewWarning(
    string LocalizationKey,
    IReadOnlyDictionary<string, string> Arguments,
    string RawDetail
);

internal sealed record PreviewWarnings(IReadOnlyList<PreviewWarning> Items);
