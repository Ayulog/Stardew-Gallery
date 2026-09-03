namespace StardewGallery;

internal sealed class ConditionParser(
    Func<string, string[]> splitPreconditions,
    Func<string, string[]> splitArguments)
{
    internal ConditionSet ParseRawKey(string rawKey)
        => Parse(splitPreconditions(rawKey).Skip(1).ToArray());

    internal ConditionSet Parse(IReadOnlyList<string> rawSegments)
    {
        List<ConditionExpression> conditions = [];
        foreach (string segment in rawSegments)
            conditions.Add(ParseSegment(segment));
        return new ConditionSet(conditions);
    }

    internal ConditionExpression ParseSegment(string rawSegment)
    {
        string segment = rawSegment.Trim();
        bool negated = false;
        while (segment.StartsWith('!'))
        {
            negated = !negated;
            segment = segment[1..].TrimStart();
        }
        if (segment.Length == 0)
            return new OpaqueCondition(ConditionSource.OpaqueEventPrecondition, rawSegment, negated);
        string[] tokens = splitArguments(segment);
        if (tokens.Length == 0)
            return new OpaqueCondition(ConditionSource.OpaqueEventPrecondition, rawSegment, negated);
        string head = tokens[0];
        string[] arguments = tokens.Skip(1).ToArray();

        switch (head)
        {
            case "f":
                return ParseFriendship(arguments, rawSegment, negated);
            case "e":
                return ParseSawEvent(arguments, rawSegment, negated);
            case "k":
                return ParseSawEvent(arguments, rawSegment, negated ^ true);
            case "n":
                return ParseMail(arguments, rawSegment, negated, ConditionPlayerScope.LocalPlayer);
            case "l":
                return ParseMail(arguments, rawSegment, negated ^ true, ConditionPlayerScope.LocalPlayer);
            case "t":
                return ParseTime(arguments, rawSegment, negated);
            case "w":
                return ParseWeather(arguments, rawSegment, negated);
            case "y":
                return ParseYear(arguments, rawSegment, negated);
            case "u":
                return ParseDayOfMonth(arguments, rawSegment, negated);
            case "z":
                return ParseSeason(arguments, rawSegment, negated ^ true);
            case "j":
                return ParseDaysPlayed(arguments, rawSegment, negated);
            case "D":
                return ParseDating(arguments, rawSegment, negated);
            case "O":
                return ParseSpouse(arguments, rawSegment, negated);
            case "o":
                return ParseSpouse(arguments, rawSegment, negated ^ true);
            case "R":
                return ParseRoommate(arguments, rawSegment, negated);
            case "G":
                return ParseNativeQuery(arguments, rawSegment, negated);
        }

        switch (head.ToUpperInvariant())
        {
            case "FRIENDSHIP":
                return ParseFriendship(arguments, rawSegment, negated);
            case "SAWEVENT":
                return ParseSawEvent(arguments, rawSegment, negated);
            case "LOCALMAIL":
                return ParseMail(arguments, rawSegment, negated, ConditionPlayerScope.LocalPlayer);
            case "HOSTMAIL":
                return ParseMail(arguments, rawSegment, negated, ConditionPlayerScope.HostPlayer);
            case "HOSTORLOCALMAIL":
                return ParseMail(arguments, rawSegment, negated, ConditionPlayerScope.HostOrLocal);
            case "TIME":
                return ParseTime(arguments, rawSegment, negated);
            case "WEATHER":
                return ParseWeather(arguments, rawSegment, negated);
            case "YEAR":
                return ParseYear(arguments, rawSegment, negated);
            case "DAYOFMONTH":
                return ParseDayOfMonth(arguments, rawSegment, negated);
            case "SEASON":
                return ParseSeason(arguments, rawSegment, negated);
            case "DAYSPLAYED":
                return ParseDaysPlayed(arguments, rawSegment, negated);
            case "DATING":
                return ParseDating(arguments, rawSegment, negated);
            case "SPOUSE":
                return ParseSpouse(arguments, rawSegment, negated);
            case "ROOMMATE":
                return ParseRoommate(arguments, rawSegment, negated);
            case "GAMESTATEQUERY":
                return ParseNativeQuery(arguments, rawSegment, negated);
            case "WORLDSTATE":
                return ParseWorldState(arguments, rawSegment, negated);
            case "NOTSEASON":
                return ParseSeason(arguments, rawSegment, negated ^ true);
            case "NOTSAWEVENT":
                return ParseSawEvent(arguments, rawSegment, negated ^ true);
            case "NOTLOCALMAIL":
                return ParseMail(arguments, rawSegment, negated ^ true, ConditionPlayerScope.LocalPlayer);
            case "NOTHOSTMAIL":
                return ParseMail(arguments, rawSegment, negated ^ true, ConditionPlayerScope.HostPlayer);
            case "NOTHOSTORLOCALMAIL":
                return ParseMail(arguments, rawSegment, negated ^ true, ConditionPlayerScope.HostOrLocal);
            case "NOTSPOUSE":
                return ParseSpouse(arguments, rawSegment, negated ^ true);
            case "NOTROOMMATE":
                return ParseRoommate(arguments, rawSegment, negated ^ true);
            default:
                return Opaque(rawSegment, negated);
        }
    }

    private static ConditionExpression ParseFriendship(string[] arguments, string rawSegment, bool negated)
        => arguments.Length == 2 && int.TryParse(arguments[1], out int points)
            ? new FriendshipCondition(arguments[0], points, ConditionPlayerScope.LocalPlayer, ConditionSource.LegacyEventPrecondition, rawSegment, negated)
            : new OpaqueCondition(ConditionSource.OpaqueEventPrecondition, rawSegment, negated);

    private static ConditionExpression ParseSawEvent(string[] arguments, string rawSegment, bool negated)
        => arguments.Length == 1
            ? new SawEventCondition(arguments[0], ConditionSource.LegacyEventPrecondition, rawSegment, negated)
            : new OpaqueCondition(ConditionSource.OpaqueEventPrecondition, rawSegment, negated);

    private static ConditionExpression ParseMail(string[] arguments, string rawSegment, bool negated, ConditionPlayerScope scope)
        => arguments.Length == 1
            ? new MailCondition(arguments[0], scope, ConditionSource.LegacyEventPrecondition, rawSegment, negated)
            : new OpaqueCondition(ConditionSource.OpaqueEventPrecondition, rawSegment, negated);

    private static ConditionExpression ParseTime(string[] arguments, string rawSegment, bool negated)
        => arguments.Length == 2 && int.TryParse(arguments[0], out int min) && int.TryParse(arguments[1], out int max)
            ? new TimeCondition(min, max, ConditionSource.LegacyEventPrecondition, rawSegment, negated)
            : new OpaqueCondition(ConditionSource.OpaqueEventPrecondition, rawSegment, negated);

    private static ConditionExpression ParseWeather(string[] arguments, string rawSegment, bool negated)
        => arguments.Length == 1
            ? new WeatherCondition(arguments[0], ConditionSource.LegacyEventPrecondition, rawSegment, negated)
            : new OpaqueCondition(ConditionSource.OpaqueEventPrecondition, rawSegment, negated);

    private static ConditionExpression ParseYear(string[] arguments, string rawSegment, bool negated)
        => arguments.Length == 1 && int.TryParse(arguments[0], out int year)
            ? new YearCondition(year, ConditionSource.LegacyEventPrecondition, rawSegment, negated)
            : new OpaqueCondition(ConditionSource.OpaqueEventPrecondition, rawSegment, negated);

    private static ConditionExpression ParseDayOfMonth(string[] arguments, string rawSegment, bool negated)
        => arguments.Length >= 1 && arguments.All(value => int.TryParse(value, out int day) && day >= 1 && day <= 28)
            ? new DayOfMonthCondition(arguments.Select(int.Parse).ToList(), ConditionSource.LegacyEventPrecondition, rawSegment, negated)
            : new OpaqueCondition(ConditionSource.OpaqueEventPrecondition, rawSegment, negated);

    private static ConditionExpression ParseSeason(string[] arguments, string rawSegment, bool negated)
        => arguments.Length >= 1
            ? new SeasonCondition(arguments.ToList(), ConditionSource.LegacyEventPrecondition, rawSegment, negated)
            : new OpaqueCondition(ConditionSource.OpaqueEventPrecondition, rawSegment, negated);

    private static ConditionExpression ParseDaysPlayed(string[] arguments, string rawSegment, bool negated)
        => arguments.Length == 1 && int.TryParse(arguments[0], out int minDays)
            ? new DaysPlayedCondition(minDays, ConditionPlayerScope.HostPlayer, ConditionSource.LegacyEventPrecondition, rawSegment, negated)
            : new OpaqueCondition(ConditionSource.OpaqueEventPrecondition, rawSegment, negated);

    private static ConditionExpression ParseDating(string[] arguments, string rawSegment, bool negated)
        => arguments.Length == 1
            ? new DatingCondition(arguments[0], ConditionSource.LegacyEventPrecondition, rawSegment, negated)
            : new OpaqueCondition(ConditionSource.OpaqueEventPrecondition, rawSegment, negated);

    private static ConditionExpression ParseSpouse(string[] arguments, string rawSegment, bool negated)
        => arguments.Length == 1
            ? new SpouseCondition(arguments[0], ConditionSource.LegacyEventPrecondition, rawSegment, negated)
            : new OpaqueCondition(ConditionSource.OpaqueEventPrecondition, rawSegment, negated);

    private static ConditionExpression ParseRoommate(string[] arguments, string rawSegment, bool negated)
        => arguments.Length == 0
            ? new RoommateCondition(ConditionSource.LegacyEventPrecondition, rawSegment, negated)
            : new OpaqueCondition(ConditionSource.OpaqueEventPrecondition, rawSegment, negated);

    private static ConditionExpression ParseWorldState(string[] arguments, string rawSegment, bool negated)
        => arguments.Length == 1
            ? new WorldStateCondition(arguments[0], ConditionSource.LegacyEventPrecondition, rawSegment, negated)
            : new OpaqueCondition(ConditionSource.OpaqueEventPrecondition, rawSegment, negated);

    private static ConditionExpression ParseNativeQuery(string[] arguments, string rawSegment, bool negated)
        => arguments.Length >= 1
            ? new NativeQueryCondition(string.Join(' ', arguments), ConditionSource.GameStateQuery, rawSegment, negated)
            : new OpaqueCondition(ConditionSource.OpaqueEventPrecondition, rawSegment, negated);

    private static OpaqueCondition Opaque(string rawSegment, bool negated)
        => new(ConditionSource.OpaqueEventPrecondition, rawSegment, negated);
}
