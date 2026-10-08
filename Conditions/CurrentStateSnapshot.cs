namespace StardewGallery;

internal sealed record CurrentStateSnapshot(
    string? Season,
    string? Weather,
    int? DayOfMonth,
    int? Year,
    int? Time,
    int? DaysPlayed,
    IReadOnlyDictionary<string, int>? Friendship,
    IReadOnlySet<string>? EventsSeen,
    IReadOnlySet<string>? LocalMail,
    IReadOnlySet<string>? HostMail,
    IReadOnlySet<string>? HostOrLocalMail,
    IReadOnlySet<string>? Dating,
    IReadOnlySet<string>? Spouse,
    bool? Roommate,
    IReadOnlySet<string>? WorldState,
    bool? IsRaining = null
)
{
    internal ConditionReadState? Details { get; init; }

    internal CurrentStateSnapshot ForLocation(string? weather, bool? isRaining)
        => this with { Weather = weather, IsRaining = isRaining };

    internal ConditionEvaluationContext ToConditionContext()
        => new(
            Season,
            DayOfMonth,
            Year,
            Time,
            Weather,
            Friendship,
            EventsSeen,
            LocalMail,
            HostMail,
            HostOrLocalMail,
            Dating,
            Spouse,
            Roommate,
            DaysPlayed,
            WorldState,
            IsRaining) { Details = Details };
}
