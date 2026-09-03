namespace StardewGallery;

internal sealed record ConditionEvaluationContext(
    string? Season,
    int? DayOfMonth,
    int? Year,
    int? Time,
    string? Weather,
    IReadOnlyDictionary<string, int>? Friendship,
    IReadOnlySet<string>? EventsSeen,
    IReadOnlySet<string>? LocalMail,
    IReadOnlySet<string>? HostMail,
    IReadOnlySet<string>? HostOrLocalMail,
    IReadOnlySet<string>? Dating,
    IReadOnlySet<string>? Spouse,
    bool? Roommate,
    int? DaysPlayed,
    IReadOnlySet<string>? WorldState
);
