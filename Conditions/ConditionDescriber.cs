namespace StardewGallery;

internal sealed record ReadableCondition(
    string? LocalizationKey,
    IReadOnlyDictionary<string, string> Arguments,
    string? RawFallback,
    bool Negated
);

internal static class ConditionDescriber
{
    private static readonly IReadOnlyDictionary<string, string> EmptyArguments =
        new Dictionary<string, string>();

    internal static ReadableCondition Describe(ConditionExpression condition)
    {
        return condition switch
        {
            SeasonCondition leaf => Named(leaf, "condition.season",
                ("seasons", string.Join(' ', leaf.Seasons))),
            DayOfMonthCondition leaf => Named(leaf, "condition.day",
                ("day", string.Join(',', leaf.Days))),
            YearCondition leaf => Named(leaf, "condition.year",
                ("year", leaf.Min.ToString())),
            TimeCondition leaf => Named(leaf, "condition.time",
                ("from", leaf.Min is null ? "?" : leaf.Min.Value.ToString()),
                ("to", leaf.Max is null ? "?" : leaf.Max.Value.ToString())),
            WeatherCondition leaf => Named(leaf, "condition.weather",
                ("weather", leaf.Weather)),
            FriendshipCondition leaf => Named(leaf, "condition.hearts",
                ("npc", leaf.Npc),
                ("points", leaf.Points.ToString()),
                ("hearts", ((int)Math.Ceiling(leaf.Points / 250d)).ToString())),
            SawEventCondition leaf => Named(leaf, "condition.seen",
                ("id", leaf.EventId)),
            MailCondition leaf => Named(leaf, "condition.mail",
                ("id", leaf.MailId)),
            DatingCondition leaf => Named(leaf, "condition.dating", ("npc", leaf.Npc)),
            SpouseCondition leaf => Named(leaf, "condition.spouse", ("npc", leaf.Npc)),
            RoommateCondition leaf => Named(leaf, "condition.roommate"),
            DaysPlayedCondition leaf => Named(leaf, "condition.daysplayed",
                ("min", leaf.Min.ToString())),
            WorldStateCondition leaf => Named(leaf, "condition.world-state", ("id", leaf.Id)),
            NativeQueryCondition leaf => Named(leaf, "condition.native-query", ("query", leaf.Query)),
            OpaqueCondition leaf => Named(leaf, "condition.unsupported", ("raw", leaf.RawSegment)),
            _ => RawFallback(condition)
        };
    }

    private static ReadableCondition Named(
        ConditionExpression leaf,
        string key,
        params (string Key, string Value)[] arguments)
    {
        Dictionary<string, string> map = new();
        foreach ((string argumentKey, string value) in arguments)
            map[argumentKey] = value;
        return new ReadableCondition(key, map, leaf.RawSegment, leaf.Negated);
    }

    private static ReadableCondition RawFallback(ConditionExpression leaf)
        => new(null, EmptyArguments, leaf.RawSegment, leaf.Negated);
}
