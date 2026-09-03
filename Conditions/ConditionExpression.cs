namespace StardewGallery;

internal abstract record ConditionExpression(
    ConditionSource Source,
    string RawSegment,
    bool Negated
);

internal sealed record ConditionSet(
    IReadOnlyList<ConditionExpression> Conditions
) : ConditionExpression(ConditionSource.Synthetic, "", false);

internal sealed record SeasonCondition(
    IReadOnlyList<string> Seasons,
    ConditionSource Source,
    string RawSegment,
    bool Negated
) : ConditionExpression(Source, RawSegment, Negated);

internal sealed record DayOfMonthCondition(
    IReadOnlyList<int> Days,
    ConditionSource Source,
    string RawSegment,
    bool Negated
) : ConditionExpression(Source, RawSegment, Negated);

internal sealed record YearCondition(
    int Min,
    ConditionSource Source,
    string RawSegment,
    bool Negated
) : ConditionExpression(Source, RawSegment, Negated);

internal sealed record TimeCondition(
    int? Min,
    int? Max,
    ConditionSource Source,
    string RawSegment,
    bool Negated
) : ConditionExpression(Source, RawSegment, Negated);

internal sealed record WeatherCondition(
    string Weather,
    ConditionSource Source,
    string RawSegment,
    bool Negated
) : ConditionExpression(Source, RawSegment, Negated);

internal sealed record FriendshipCondition(
    string Npc,
    int Points,
    ConditionPlayerScope Scope,
    ConditionSource Source,
    string RawSegment,
    bool Negated
) : ConditionExpression(Source, RawSegment, Negated);

internal sealed record SawEventCondition(
    string EventId,
    ConditionSource Source,
    string RawSegment,
    bool Negated
) : ConditionExpression(Source, RawSegment, Negated);

internal sealed record MailCondition(
    string MailId,
    ConditionPlayerScope Scope,
    ConditionSource Source,
    string RawSegment,
    bool Negated
) : ConditionExpression(Source, RawSegment, Negated);

internal sealed record DatingCondition(
    string Npc,
    ConditionSource Source,
    string RawSegment,
    bool Negated
) : ConditionExpression(Source, RawSegment, Negated);

internal sealed record SpouseCondition(
    string Npc,
    ConditionSource Source,
    string RawSegment,
    bool Negated
) : ConditionExpression(Source, RawSegment, Negated);

internal sealed record RoommateCondition(
    ConditionSource Source,
    string RawSegment,
    bool Negated
) : ConditionExpression(Source, RawSegment, Negated);

internal sealed record DaysPlayedCondition(
    int Min,
    int? Max,
    ConditionPlayerScope Scope,
    ConditionSource Source,
    string RawSegment,
    bool Negated
) : ConditionExpression(Source, RawSegment, Negated);

internal sealed record WorldStateCondition(
    string Id,
    ConditionSource Source,
    string RawSegment,
    bool Negated
) : ConditionExpression(Source, RawSegment, Negated);

internal sealed record NativeQueryCondition(
    string Query,
    ConditionPlayerScope Scope,
    ConditionSource Source,
    string RawSegment,
    bool Negated
) : ConditionExpression(Source, RawSegment, Negated);

internal sealed record OpaqueCondition(
    ConditionSource Source,
    string RawSegment,
    bool Negated
) : ConditionExpression(Source, RawSegment, Negated);
