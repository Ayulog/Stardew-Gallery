using System.Globalization;

namespace StardewGallery;

internal abstract record ConditionTextValue;
internal sealed record PlainTextValue(string Value) : ConditionTextValue;
internal sealed record NumberTextValue(double Value) : ConditionTextValue;
internal sealed record NpcTextValue(string Name) : ConditionTextValue;
internal sealed record ItemTextValue(string Id) : ConditionTextValue;
internal sealed record TimeTextValue(int Value) : ConditionTextValue;
internal sealed record TermTextValue(string Group, string Value) : ConditionTextValue;
internal sealed record ListTextValue(IReadOnlyList<ConditionTextValue> Values) : ConditionTextValue;
internal sealed record FriendshipRequirementsTextValue(IReadOnlyList<FriendshipRequirement> Values) : ConditionTextValue;
internal sealed record ShippedRequirementsTextValue(IReadOnlyList<ShippedRequirement> Values) : ConditionTextValue;
internal sealed record CalendarDateTextValue(string Season, int Day) : ConditionTextValue;
internal sealed record NestedConditionTextValue(ConditionTextSpec Spec) : ConditionTextValue;
internal sealed record ConditionTextSpec(string LocalizationKey, IReadOnlyDictionary<string, ConditionTextValue> Arguments, string RawFallback, bool Negated, string? NaturalNegativeKey = null);

internal static class ConditionDescriber
{
    internal static ConditionTextSpec Describe(ConditionExpression c, string? eventLocation = null) => c switch
    {
        SawEventCondition x => NamedNegative(x, "condition.seen", "condition.seen-not", ("id", List(x.EventIds.Select(Id)))),
        MissingPetCondition x => Named(x, x.PetType is null ? "condition.missing-pet" : "condition.missing-pet-type", ("type", Text(x.PetType ?? ""))),
        IsHostCondition x => Named(x, "condition.is-host"),
        MailCondition x => NamedNegative(x, x.Scope switch { ConditionPlayerScope.HostPlayer => "condition.mail-host", ConditionPlayerScope.HostOrLocal => "condition.mail-host-or-local", _ => "condition.mail" }, x.Scope switch { ConditionPlayerScope.HostPlayer => "condition.mail-host-not", ConditionPlayerScope.HostOrLocal => "condition.mail-host-or-local-not", _ => "condition.mail-not" }, ("id", Id(x.MailId))),
        WorldStateCondition x => NamedNegative(x, "condition.world-state", "condition.world-state-not", ("id", Id(x.Id))),
        EarnedMoneyCondition x => Named(x, "condition.earned-money", ("amount", Num(x.Minimum))),
        HasMoneyCondition x => Named(x, "condition.has-money", ("amount", Num(x.Minimum))),
        FreeInventorySlotsCondition x => Named(x, "condition.free-slots", ("count", Num(x.Minimum))),
        CommunityCenterOrWarehouseDoneCondition x => Named(x, "condition.community-complete"),
        DatingCondition x => NamedNegative(x, "condition.dating", "condition.dating-not", ("npc", Npc(x.Npc))),
        DaysPlayedCondition x => Named(x, "condition.daysplayed", ("min", Num(x.Threshold + 1))),
        JojaBundlesDoneCondition x => Named(x, "condition.joja-complete"),
        FriendshipCondition x => NamedNegative(x, "condition.friendship", "condition.friendship-not", ("requirements", new FriendshipRequirementsTextValue(x.Requirements))),
        FestivalDayCondition x => Named(x, "condition.festival-day", negative: "condition.not-festival-day"),
        RandomCondition x => Named(x, "condition.random", ("chance", Num(x.Probability * 100))),
        ShippedCondition x => Named(x, "condition.shipped", ("requirements", new ShippedRequirementsTextValue(x.Requirements))),
        SawSecretNoteCondition x => Named(x, "condition.secret-note", ("id", Num(x.NoteId))),
        ChoseDialogueAnswersCondition x => Named(x, "condition.designated-dialogue-choice"),
        GoldenWalnutsCondition x => Named(x, "condition.walnuts", ("count", Num(x.Minimum))),
        InUpgradedHouseCondition x => NamedNegative(x, "condition.house-level", "condition.house-level-not", ("level", Num(x.MinimumLevel))),
        TimeCondition x => NamedNegative(x, "condition.time", "condition.time-not", ("from", new TimeTextValue(x.Min)), ("to", new TimeTextValue(x.Max))),
        WeatherCondition { Kind: WeatherKind.Rainy } x => Named(x, "condition.raining", negative: "condition.not-raining"),
        WeatherCondition { Kind: WeatherKind.Sunny } x => Named(x, "condition.sunny", negative: "condition.not-sunny"),
        WeatherCondition x => NamedNegative(x, "condition.weather", "condition.weather-not", ("weather", Term("weather", NormalizeWeather(x.WeatherId)))),
        DayOfWeekCondition x => DescribeWeekday(x),
        SpouseCondition x => NamedNegative(x, "condition.spouse", "condition.spouse-not", ("npc", Npc(x.Npc))),
        RoommateCondition x => NamedNegative(x, "condition.roommate-with", "condition.roommate-not", ("npc", Npc("Krobus"))),
        NpcVisibleCondition { CurrentLocationOnly: false } x => NamedNegative(x, "condition.npc-visible", "condition.npc-not-visible", ("npc", Npc(x.Npc))),
        NpcVisibleCondition x => NamedNegative(x, "condition.npc-at-location", "condition.npc-not-at-location", ("npc", Npc(x.Npc)), ("location", Text(eventLocation ?? "event location"))),
        SeasonCondition x => DescribeSeason(x),
        SpouseBedCondition x => Named(x, "condition.spouse-bed"),
        ReachedMineBottomCondition x => Named(x, "condition.mine-bottom", ("count", Num(x.Minimum))),
        YearCondition x => Named(x, x.DesiredYear == 1 ? "condition.year-one" : "condition.year", ("year", Num(x.DesiredYear))),
        GenderCondition x => Named(x, "condition.gender", ("gender", Term("gender", x.Gender))),
        HasItemCondition x => Named(x, "condition.has-item", ("item", new ItemTextValue(x.ItemId))),
        TileCondition x => Named(x, "condition.designated-area"),
        ActiveDialogueEventCondition x => NamedNegative(x, "condition.dialogue-event", "condition.dialogue-event-not", ("id", Id(x.Id))),
        DayOfMonthCondition x => Named(x, "condition.day", ("day", List(x.Days.Select(d => Num(d))))),
        UpcomingFestivalCondition x => NamedNegative(x, "condition.upcoming-festival", "condition.no-upcoming-festival", ("days", Num(x.Days))),
        NativeQueryCondition x => DescribeQuery(x),
        SkillCondition x => Named(x, "condition.skill", ("skill", Term("skill", x.Skill)), ("level", Num(x.MinimumLevel))),
        LegacySendMailCondition x => Named(x with { Negated = false }, "condition.special-mail"),
        OpaqueCondition x => Named(x with { Negated = false }, x.Kind != OpaqueConditionKind.UnknownType ? "condition.unrecognized" : "condition.custom"),
        _ => Named(c with { Negated = false }, "condition.custom")
    };

    private static ConditionTextSpec DescribeQuery(NativeQueryCondition condition)
    {
        if (!SafeGameQuery.TryParse(condition.Query, out var clauses))
            return Named(condition, "condition.native-query", ("query", Text(condition.Query)));
        List<ConditionTextSpec> specs = [];
        foreach (SafeQueryClause clause in clauses)
        {
            string[] a = clause.Arguments;
            ConditionExpression source = condition with { Negated = clause.Negated };
            specs.Add(clause.Name switch
            {
                "TRUE" => Named(source, "condition.always"),
                "FALSE" => Named(source, "condition.never"),
                "PLAYER_HEARTS" or "PLAYER_FRIENDSHIP_POINTS" => a.Length == 3 ? Named(source,
                    clause.Name == "PLAYER_HEARTS" ? "condition.query-hearts" : "condition.query-points",
                    ("player", Term("player", a[0])), ("npc", Npc(a[1])), ("min", Num(SafeGameQuery.ParseInt(a[2]))))
                    : Named(source, clause.Name == "PLAYER_HEARTS" ? "condition.query-hearts-range" : "condition.query-points-range",
                    ("player", Term("player", a[0])), ("npc", Npc(a[1])), ("min", Num(SafeGameQuery.ParseInt(a[2]))), ("max", Num(SafeGameQuery.ParseInt(a[3])))),
                "PLAYER_NPC_RELATIONSHIP" => Named(source, "condition.query-relationship", ("player", Term("player", a[0])),
                    ("npc", Npc(a[1])), ("states", List(a.Skip(2).Select(value => Term("relationship", value))))),
                "PLAYER_HAS_SEEN_EVENT" => Named(source, "condition.query-seen", ("player", Term("player", a[0])), ("ids", List(a.Skip(1).Select(Id)))),
                "PLAYER_HAS_CONVERSATION_TOPIC" => Named(source, "condition.query-topic", ("player", Term("player", a[0])), ("ids", List(a.Skip(1).Select(Id)))),
                "PLAYER_HAS_RUN_TRIGGER_ACTION" => Named(source, "condition.query-run", ("player", Term("player", a[0])), ("ids", List(a.Skip(1).Select(Id)))),
                "PLAYER_HAS_MAIL" => Named(source, "condition.query-mail", ("player", Term("player", a[0])), ("id", Id(a[1])),
                    ("state", Term("mail-state", a.Length > 2 ? a[2] : "any"))),
                "WEATHER" => Named(source, "condition.query-weather", ("location", Term("location", a[0])),
                    ("weather", List(a.Skip(1).Select(value => Term("weather", NormalizeWeather(value)))))),
                "IS_PASSIVE_FESTIVAL_TODAY" => Named(source, "condition.passive-festival", ("festival", Term("festival", a[0]))),
                "SEASON_DAY" => Named(source, "condition.season-dates", ("dates", List(Enumerable.Range(0, a.Length / 2)
                    .Select(index => new CalendarDateTextValue(a[index * 2], SafeGameQuery.ParseInt(a[index * 2 + 1])))))),
                _ => a.Length == 3
                    ? Named(source, "condition.player-stat-min", ("player", Term("player", a[0])), ("stat", Term("stat", a[1])), ("min", Num(SafeGameQuery.ParseInt(a[2]))))
                    : Named(source, "condition.player-stat-range", ("player", Term("player", a[0])), ("stat", Term("stat", a[1])),
                        ("min", Num(SafeGameQuery.ParseInt(a[2]))), ("max", Num(SafeGameQuery.ParseInt(a[3]))))
            });
        }
        return specs.Count == 1 ? specs[0] with { Negated = specs[0].Negated ^ condition.Negated }
            : Named(condition, "condition.query-all", ("conditions", List(specs.Select(spec => new NestedConditionTextValue(spec)))));
    }

    private static ConditionTextSpec DescribeWeekday(DayOfWeekCondition condition)
    {
        DayOfWeek[] remaining = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
            DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday }.Except(condition.Days).ToArray();
        if (condition.Negated && remaining.Length > 0 && remaining.Length < condition.Days.Distinct().Count())
            return Named(condition with { Negated = false }, "condition.weekday", ("days", List(remaining.Select(day => Term("weekday", day.ToString())))));
        return NamedNegative(condition, "condition.weekday", "condition.weekday-not", ("days", List(condition.Days.Select(day => Term("weekday", day.ToString())))));
    }

    private static ConditionTextSpec DescribeSeason(SeasonCondition condition)
    {
        string[] remaining = new[] { "spring", "summer", "fall", "winter" }.Except(condition.Seasons, StringComparer.OrdinalIgnoreCase).ToArray();
        if (condition.Negated && remaining.Length > 0 && remaining.Length < condition.Seasons.Distinct(StringComparer.OrdinalIgnoreCase).Count())
            return Named(condition with { Negated = false }, "condition.season", ("seasons", List(remaining.Select(season => Term("season", season)))));
        return NamedNegative(condition, "condition.season", "condition.season-not", ("seasons", List(condition.Seasons.Select(season => Term("season", season)))));
    }

    private static ConditionTextSpec Named(ConditionExpression c, string key, params (string Key, ConditionTextValue Value)[] args) => Named(c, key, args, null);
    private static ConditionTextSpec NamedNegative(ConditionExpression c, string key, string negative, params (string Key, ConditionTextValue Value)[] args) => Named(c, key, args, negative);
    private static ConditionTextSpec Named(ConditionExpression c, string key, (string Key, ConditionTextValue Value)[] args, string? negative)
        => new(key, args.ToDictionary(x => x.Key, x => x.Value), c.RawSegment, c.Negated, negative);
    private static ConditionTextSpec Named(ConditionExpression c, string key, string? negative = null)
        => new(key, new Dictionary<string, ConditionTextValue>(), c.RawSegment, c.Negated, negative);
    private static PlainTextValue Text(string value) => new(value);
    private static PlainTextValue Id(string value) => new(value);
    private static NumberTextValue Num(double value) => new(value);
    private static NpcTextValue Npc(string value) => new(value);
    private static TermTextValue Term(string group, string value) => new(group, value);
    private static ListTextValue List(IEnumerable<ConditionTextValue> values) => new(values.ToList());
    internal static string NormalizeWeather(string value) => value.ToLowerInvariant() switch
    {
        "sun" => "sunny", "rain" => "rainy", "storm" => "stormy", "snow" => "snowy", "debris" => "wind", _ => value
    };
}

internal sealed record ConditionDisplayResolver(Func<string, string> Npc, Func<string, string?> Item, Func<string, string, string> Term, Func<int, string> Time);

internal static class ConditionTextFormatter
{
    internal static string Format(ConditionTextSpec spec, Func<string, IReadOnlyDictionary<string, string>, string> translate, ConditionDisplayResolver resolver)
    {
        Dictionary<string, string> args = spec.Arguments.ToDictionary(pair => pair.Key, pair => FormatValue(pair.Value, resolver, translate));
        string key = spec.Negated && spec.NaturalNegativeKey is not null ? spec.NaturalNegativeKey : spec.LocalizationKey;
        string text = translate(key, args);
        return spec.Negated && spec.NaturalNegativeKey is null
            ? translate("condition.not", new Dictionary<string, string> { ["condition"] = text })
            : text;
    }

    internal static string FormatGap(ConditionExpression condition, string value, Func<string, IReadOnlyDictionary<string, string>, string> translate,
        ConditionDisplayResolver resolver, bool friendshipCurrent = false) => condition switch
    {
        FriendshipCondition when int.TryParse(value, out int points) => friendshipCurrent
            ? FormatFriendshipCurrent(points, translate)
            : FormatFriendship(points, translate),
        TimeCondition when TryParseTimeRange(value, out int from, out int to) => $"{resolver.Time(from)} – {resolver.Time(to)}",
        TimeCondition when int.TryParse(value, out int time) => resolver.Time(time),
        SeasonCondition => string.Join(", ", value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(season => resolver.Term("season", season))),
        WeatherCondition leaf when value == leaf.WeatherId && leaf.Kind != WeatherKind.Custom
            => translate(leaf.Kind == WeatherKind.Rainy ? "condition.raining" : "condition.not-raining", new Dictionary<string, string>()),
        WeatherCondition => resolver.Term("weather", ConditionDescriber.NormalizeWeather(value)),
        _ => value
    };

    private static bool TryParseTimeRange(string value, out int from, out int to)
    {
        from = 0;
        to = 0;
        string[] parts = value.Split("..", StringSplitOptions.None);
        return parts.Length == 2 && int.TryParse(parts[0], out from) && int.TryParse(parts[1], out to);
    }

    private static string ResolveItem(string id, ConditionDisplayResolver resolver)
    {
        string? name = resolver.Item(id);
        return string.IsNullOrWhiteSpace(name) ? id : name;
    }

    private static string FormatFriendship(int points, Func<string, IReadOnlyDictionary<string, string>, string> translate)
        => translate("condition.hearts-value", new Dictionary<string, string>
        {
            ["hearts"] = (points / 250d).ToString("0.###", CultureInfo.CurrentCulture)
        });

    internal static string FormatFriendshipCurrent(int points, Func<string, IReadOnlyDictionary<string, string>, string> translate)
        => translate("condition.hearts-value", new Dictionary<string, string>
        {
            ["hearts"] = (points / 250d).ToString("0.#", CultureInfo.CurrentCulture)
        });

    private static string FormatValue(ConditionTextValue value, ConditionDisplayResolver resolver, Func<string, IReadOnlyDictionary<string, string>, string> translate) => value switch
    {
        PlainTextValue x => x.Value,
        NumberTextValue x => x.Value.ToString("0.##", CultureInfo.CurrentCulture),
        NpcTextValue x => resolver.Npc(x.Name),
        ItemTextValue x => ResolveItem(x.Id, resolver),
        TimeTextValue x => resolver.Time(x.Value),
        TermTextValue x => resolver.Term(x.Group, x.Value),
        CalendarDateTextValue x => translate("condition.season-date", new Dictionary<string, string>
        {
            ["season"] = resolver.Term("season", x.Season), ["day"] = x.Day.ToString(CultureInfo.CurrentCulture)
        }),
        NestedConditionTextValue x => Format(x.Spec, translate, resolver),
        ListTextValue x => string.Join(", ", x.Values.Select(item => FormatValue(item, resolver, translate))),
        FriendshipRequirementsTextValue x => string.Join(", ", x.Values.Select(item => $"{resolver.Npc(item.Npc)} ≥ {FormatFriendship(item.Points, translate)}")),
        ShippedRequirementsTextValue x => string.Join(", ", x.Values.Select(item => $"{ResolveItem(item.ItemId, resolver)} × {item.Count}")),
        _ => ""
    };
}
