namespace StardewGallery;

/// <summary>
/// Sparse hypothetical state. Only fields that a preview wishes to override are set.
/// This is never a full save snapshot.
/// </summary>
internal sealed record PreviewState(
    string? Season = null,
    int? DayOfMonth = null,
    int? Year = null,
    int? Time = null,
    string? Weather = null,
    IReadOnlyDictionary<string, int>? Friendship = null,
    IReadOnlySet<string>? EventsSeen = null,
    IReadOnlySet<string>? Mail = null,
    IReadOnlySet<string>? Dating = null,
    IReadOnlySet<string>? Spouse = null,
    bool? Roommate = null,
    IReadOnlySet<string>? WorldState = null
);

/// <summary>
/// The immutable decision of what would need to change temporarily for one event.
/// Describes overrides; it does not apply any of them.
/// </summary>
internal sealed record PreviewPlan(
    EventIdentity Identity,
    EventPlayback Playback,
    PreviewCapability Capability,
    PreviewState Suggestion,
    IReadOnlyList<PreviewOverride> Overrides,
    IReadOnlyList<string> UnsupportedRequirements,
    IReadOnlyList<PreviewWarning> Warnings
);

/// <summary>
/// Status of a single event against the current save, for readable presentation.
/// </summary>
internal sealed record EventConditionStatus(
    bool IsCurrentlyAvailable,
    int RequiredCount,
    int MissingCount,
    int UnknownCount,
    PreviewCapability Capability,
    IReadOnlyList<string> MissingSummaries,
    IReadOnlyList<string> UnknownSummaries,
    IReadOnlyList<string> ReadableRequired
);
