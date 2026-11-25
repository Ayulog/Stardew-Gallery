using System.Globalization;

namespace StardewGallery;

internal abstract record ConditionTextValue;
internal sealed record PlainTextValue(string Value) : ConditionTextValue;
internal sealed record NumberTextValue(double Value) : ConditionTextValue;
internal sealed record NpcTextValue(string Name) : ConditionTextValue;
internal sealed record ItemTextValue(string Id) : ConditionTextValue;
internal sealed record MailTextValue(string MailId) : ConditionTextValue;
internal sealed record RecipeTextValue(string Name, bool IsCooking) : ConditionTextValue;
internal sealed record BuildingTextValue(string Id) : ConditionTextValue;
internal sealed record MonsterTextValue(string Id) : ConditionTextValue;
internal sealed record QuestTextValue(string Id) : ConditionTextValue;
internal sealed record TimeTextValue(int Value) : ConditionTextValue;
internal sealed record TermTextValue(string Group, string Value) : ConditionTextValue;
internal sealed record ListTextValue(IReadOnlyList<ConditionTextValue> Values) : ConditionTextValue;
internal sealed record FriendshipRequirementsTextValue(IReadOnlyList<FriendshipRequirement> Values) : ConditionTextValue;
internal sealed record ShippedRequirementsTextValue(IReadOnlyList<ShippedRequirement> Values) : ConditionTextValue;
internal sealed record CalendarDateTextValue(string Season, int Day) : ConditionTextValue;
/// <summary>An inclusive count range; a missing or int.MaxValue maximum means "at least".</summary>
internal sealed record RangeTextValue(long Min, long? Max) : ConditionTextValue;
internal sealed record NestedConditionTextValue(ConditionTextSpec Spec) : ConditionTextValue;
internal sealed record ConditionTextSpec(string LocalizationKey, IReadOnlyDictionary<string, ConditionTextValue> Arguments, string RawFallback, bool Negated, string? NaturalNegativeKey = null);

internal static class ConditionDescriber
{
    internal static ConditionTextSpec Describe(ConditionExpression c, string? eventLocation = null) => c switch
    {
        SawEventCondition x => NamedNegative(x, "condition.seen", "condition.seen-not", ("id", List(x.EventIds.Select(Id)))),
        MissingPetCondition x => Named(x, x.PetType is null ? "condition.missing-pet" : "condition.missing-pet-type", ("type", Text(x.PetType ?? ""))),
        IsHostCondition x => Named(x, "condition.is-host"),
        MailCondition x => NamedNegative(x, x.Scope switch { ConditionPlayerScope.HostPlayer => "condition.mail-host", ConditionPlayerScope.HostOrLocal => "condition.mail-host-or-local", _ => "condition.mail" }, x.Scope switch { ConditionPlayerScope.HostPlayer => "condition.mail-host-not", ConditionPlayerScope.HostOrLocal => "condition.mail-host-or-local-not", _ => "condition.mail-not" }, ("id", new MailTextValue(x.MailId))),
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
        TileCondition x => Named(x, "condition.tile", ("positions", List(x.Positions.Select(p => Text($"({p.X}, {p.Y})"))))),
        ActiveDialogueEventCondition x => NamedNegative(x, "condition.dialogue-event", "condition.dialogue-event-not", ("id", Id(x.Id))),
        DayOfMonthCondition x => Named(x, "condition.day", ("day", List(x.Days.Select(d => Num(d))))),
        UpcomingFestivalCondition x => NamedNegative(x, "condition.upcoming-festival", "condition.no-upcoming-festival", ("days", Num(x.Days))),
        NativeQueryCondition x => DescribeQuery(x),
        SkillCondition x => Named(x, "condition.skill", ("skill", Term("skill", x.Skill)), ("level", Num(x.MinimumLevel))),
        LegacySendMailCondition x => Named(x with { Negated = false }, "condition.special-mail", ("id", new MailTextValue(x.MailId))),
        QuestCondition x => Named(x, "condition.quest", ("quest", new QuestTextValue(x.QuestId))),
        OpaqueCondition x => Named(x with { Negated = false }, x.Kind != OpaqueConditionKind.UnknownType ? "condition.unrecognized" : "condition.custom"),
        _ => Named(c with { Negated = false }, "condition.custom")
    };

    private static ConditionTextSpec DescribeQuery(NativeQueryCondition condition)
        => SafeGameQuery.TryParse(condition.Query, out var clauses)
            ? DescribeClauses(clauses, condition)
            : Named(condition, "condition.native-query", ("query", Text(StripCustomQueryPrefixes(condition.Query))));

    internal static string StripCustomQueryPrefixes(string query)
    {
        if (string.IsNullOrWhiteSpace(query) || GameQuerySplit.HasUnclosedQuote(query, ','))
            return query;
        string[] clauses = GameQuerySplit.Clauses(query);
        if (clauses.Length == 0)
            return query;
        for (int i = 0; i < clauses.Length; i++)
        {
            string clause = clauses[i];
            string trimmed = clause.TrimStart();
            if (trimmed.Length == 0 || trimmed.StartsWith('"'))
                continue;
            int ws = 0;
            while (ws < trimmed.Length && !char.IsWhiteSpace(trimmed[ws]))
                ws++;
            string head = trimmed[..ws];
            string tail = trimmed[ws..];
            bool neg = head.StartsWith('!');
            string name = neg ? head[1..] : head;
            if (name.Length == 0 || name.StartsWith('!'))
                continue;
            if (name.Contains('/'))
            {
                int slash = name.LastIndexOf('/');
                if (slash > 0 && slash < name.Length - 1)
                    name = name[(slash + 1)..];
            }
            else if (name.Contains('.'))
            {
                int lastDot = name.LastIndexOf('.');
                int underscore = name.IndexOf('_', lastDot + 1);
                if (underscore > lastDot && underscore < name.Length - 1)
                    name = name[(underscore + 1)..];
            }
            clauses[i] = (neg ? "!" : "") + name + tail;
        }
        return string.Join(", ", clauses);
    }

    private static ConditionTextSpec DescribeClauses(IReadOnlyList<SafeQueryClause> clauses, ConditionExpression source)
    {
        List<ConditionTextSpec> specs = [];
        foreach (SafeQueryClause clause in clauses)
            specs.Add(DescribeClause(clause, source with { Negated = clause.Negated }));
        return specs.Count == 1 ? specs[0] with { Negated = specs[0].Negated ^ source.Negated }
            : Named(source, "condition.query-all", ("conditions", List(specs.Select(spec => new NestedConditionTextValue(spec)))));
    }

    private static ConditionTextSpec DescribeClause(SafeQueryClause clause, ConditionExpression source)
    {
        if (clause.Name == "ANY" && clause.SubQueries is { } subQueries)
            return NamedNegative(source, "condition.query-any", "condition.query-none",
                ("conditions", List(subQueries.Select(sub => new NestedConditionTextValue(DescribeClauses(sub, source with { Negated = false }))))));
        string[] a = clause.Arguments;
        if (SafeGameQuery.SkillQueries.TryGetValue(clause.Name, out var skill))
            return Named(source, skill.Base ? "condition.query-skill-base" : "condition.query-skill",
                ("player", Player(a[0])), ("skill", Term("skill", skill.Skill)), ("range", Range(a, 1)));
        return clause.Name switch
        {
            "TRUE" => Named(source, "condition.always"),
            "FALSE" => Named(source, "condition.never"),
            "IS_COMMUNITY_CENTER_COMPLETE" => Named(source, "condition.query-cc-complete"),
            "IS_JOJA_MART_COMPLETE" => Named(source, "condition.joja-complete"),
            "IS_ISLAND_NORTH_BRIDGE_FIXED" => Named(source, "condition.query-island-bridge"),
            "FOUND_ALL_LOST_BOOKS" => Named(source, "condition.query-all-lost-books"),
            "IS_MULTIPLAYER" => NamedNegative(source, "condition.query-multiplayer", "condition.query-singleplayer"),
            "IS_HOST" => Named(source, "condition.is-host"),
            "IS_VISITING_ISLAND" => Named(source, "condition.query-visiting-island", ("npc", Npc(a[0]))),
            "MUSEUM_DONATIONS" => DescribeMuseumDonations(a, source),
            "MINE_LOWEST_LEVEL_REACHED" => Named(source, "condition.query-mine-level", ("range", Range(a, 0))),
            "SEASON" => NamedNegative(source, "condition.season", "condition.season-not", ("seasons", List(a.Select(value => Term("season", value.ToLowerInvariant()))))),
            "LOCATION_SEASON" => Named(source, "condition.query-location-season", ("location", QueryLocation(a[0])),
                ("seasons", List(a.Skip(1).Select(value => Term("season", value.ToLowerInvariant()))))),
            "SEASON_DAY" => Named(source, "condition.season-dates", ("dates", List(Enumerable.Range(0, a.Length / 2)
                .Select(index => new CalendarDateTextValue(a[index * 2].ToLowerInvariant(), SafeGameQuery.ParseInt(a[index * 2 + 1])))))),
            "YEAR" => a.Length == 1
                ? Named(source, "condition.year", ("year", Num(SafeGameQuery.ParseInt(a[0]))))
                : Named(source, "condition.query-year-range", ("min", Num(SafeGameQuery.ParseInt(a[0]))), ("max", Num(SafeGameQuery.ParseInt(a[1])))),
            "TIME" => a.Length == 1
                ? Named(source, "condition.query-time-from", ("from", new TimeTextValue(SafeGameQuery.ParseInt(a[0]))))
                : NamedNegative(source, "condition.time", "condition.time-not", ("from", new TimeTextValue(SafeGameQuery.ParseInt(a[0]))),
                    ("to", new TimeTextValue(SafeGameQuery.ParseInt(a[1])))),
            "DAYS_PLAYED" => a.Length == 1
                ? Named(source, "condition.daysplayed", ("min", Num(SafeGameQuery.ParseInt(a[0]))))
                : Named(source, "condition.query-days-played-range", ("min", Num(SafeGameQuery.ParseInt(a[0]))), ("max", Num(SafeGameQuery.ParseInt(a[1])))),
            "DAY_OF_MONTH" => Named(source, "condition.day", ("day", List(a.Select(value => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int day)
                ? (ConditionTextValue)Num(day) : Term("day", value.ToLowerInvariant()))))),
            "DAY_OF_WEEK" => NamedNegative(source, "condition.weekday", "condition.weekday-not", ("days", List(a.Select(value =>
                SafeGameQuery.TryGetDayOfWeek(value, out DayOfWeek day) && Enum.IsDefined(day) ? (ConditionTextValue)Term("weekday", day.ToString()) : Text(value))))),
            "WEATHER" => Named(source, "condition.query-weather", ("location", QueryLocation(a[0])),
                ("weather", List(a.Skip(1).Select(value => Term("weather", NormalizeWeather(value)))))),
            "IS_GREEN_RAIN_DAY" => Named(source, "condition.query-green-rain"),
            "IS_FESTIVAL_DAY" => a.Length == 0 || a.Length == 1 && a[0].Equals("any", StringComparison.OrdinalIgnoreCase)
                    || a.Length == 2 && a[0].Equals("any", StringComparison.OrdinalIgnoreCase) && SafeGameQuery.ParseInt(a[1]) == 0
                ? Named(source, "condition.festival-day", negative: "condition.not-festival-day")
                : Named(source, "condition.query-festival", ("context", a[0].Equals("any", StringComparison.OrdinalIgnoreCase) ? Term("location-key", "all")
                    : QueryLocation(a[0])), ("days", Num(a.Length > 1 ? SafeGameQuery.ParseInt(a[1]) : 0))),
            "IS_PASSIVE_FESTIVAL_TODAY" => Named(source, "condition.passive-festival", ("festival", Term("festival", a[0]))),
            "WORLD_STATE_ID" => NamedNegative(source, "condition.world-state", "condition.world-state-not", ("id", List(a.Select(Id)))),
            "BUILDINGS_CONSTRUCTED" => Named(source, a.Length > 4 && bool.Parse(a[4]) ? "condition.query-buildings-pending" : "condition.query-buildings",
                ("location", a[0].Equals("All", StringComparison.OrdinalIgnoreCase) ? Term("location-key", "all") : QueryLocation(a[0])),
                ("building", a.Length < 2 || a[1].Equals("All", StringComparison.OrdinalIgnoreCase) ? Term("building", "all") : new BuildingTextValue(a[1])),
                ("count", a.Length > 2 ? Range(a, 2) : new RangeTextValue(1, null))),
            "PLAYER_HEARTS" or "PLAYER_FRIENDSHIP_POINTS" => a.Length == 3 ? Named(source,
                clause.Name == "PLAYER_HEARTS" ? "condition.query-hearts" : "condition.query-points",
                ("player", Player(a[0])), ("npc", QueryNpc(a[1])), ("min", Num(SafeGameQuery.ParseInt(a[2]))))
                : Named(source, clause.Name == "PLAYER_HEARTS" ? "condition.query-hearts-range" : "condition.query-points-range",
                ("player", Player(a[0])), ("npc", QueryNpc(a[1])), ("min", Num(SafeGameQuery.ParseInt(a[2]))), ("max", Num(SafeGameQuery.ParseInt(a[3])))),
            "PLAYER_NPC_RELATIONSHIP" => Named(source, "condition.query-relationship", ("player", Player(a[0])),
                ("npc", QueryNpc(a[1])), ("states", List(a.Skip(2).Select(value => Term("relationship", value))))),
            "PLAYER_HAS_SEEN_EVENT" => Named(source, "condition.query-seen", ("player", Player(a[0])), ("ids", List(a.Skip(1).Select(Id)))),
            "PLAYER_HAS_CONVERSATION_TOPIC" => Named(source, "condition.query-topic", ("player", Player(a[0])), ("ids", List(a.Skip(1).Select(Id)))),
            "PLAYER_HAS_RUN_TRIGGER_ACTION" => Named(source, "condition.query-run", ("player", Player(a[0])), ("ids", List(a.Skip(1).Select(Id)))),
            "PLAYER_HAS_DIALOGUE_ANSWER" => Named(source, "condition.query-dialogue-answer", ("player", Player(a[0])), ("ids", List(a.Skip(1).Select(Id)))),
            "PLAYER_VISITED_LOCATION" => Named(source, "condition.query-visited", ("player", Player(a[0])), ("locations", List(a.Skip(1).Select(QueryLocation)))),
            "PLAYER_HAS_MAIL" => Named(source, "condition.query-mail", ("player", Player(a[0])), ("id", new MailTextValue(a[1])),
                ("state", Term("mail-state", a.Length > 2 ? a[2] : "any"))),
            "PLAYER_HAS_ITEM" => Named(source, "condition.query-item", ("player", Player(a[0])), ("item", new ItemTextValue(a[1])),
                ("count", a.Length > 2 ? Range(a, 2) : new RangeTextValue(1, null))),
            "PLAYER_HAS_CRAFTING_RECIPE" => Named(source, "condition.query-crafting-recipe", ("player", Player(a[0])), ("recipe", new RecipeTextValue(string.Join(' ', a.Skip(1)), false))),
            "PLAYER_HAS_COOKING_RECIPE" => Named(source, "condition.query-cooking-recipe", ("player", Player(a[0])), ("recipe", new RecipeTextValue(string.Join(' ', a.Skip(1)), true))),
            "PLAYER_HAS_ACHIEVEMENT" => Named(source, "condition.query-achievement", ("player", Player(a[0])), ("id", Num(SafeGameQuery.ParseInt(a[1])))),
            "PLAYER_HAS_SECRET_NOTE" => Named(source, "condition.query-secret-note", ("player", Player(a[0])), ("id", Num(SafeGameQuery.ParseInt(a[1])))),
            "PLAYER_GENDER" => Named(source, "condition.query-gender", ("player", Player(a[0])),
                ("gender", Term("gender", a[1].Equals("Male", StringComparison.OrdinalIgnoreCase) ? "male" : "female"))),
            "PLAYER_CURRENT_MONEY" => Named(source, "condition.query-money", ("player", Player(a[0])), ("range", Range(a, 1))),
            "PLAYER_MONEY_EARNED" => Named(source, "condition.query-money-earned", ("player", Player(a[0])), ("range", Range(a, 1))),
            "PLAYER_FARMHOUSE_UPGRADE" => Named(source, "condition.query-farmhouse-upgrade", ("player", Player(a[0])), ("range", Range(a, 1))),
            "PLAYER_HAS_MET" => Named(source, "condition.query-met", ("player", Player(a[0])), ("npcs", List(a.Skip(1).Select(Npc)))),
            "PLAYER_HAS_PET" => Named(source, "condition.query-has-pet", ("player", Player(a[0]))),
            "PLAYER_HAS_CHILDREN" => Named(source, "condition.query-children", ("player", Player(a[0])),
                ("count", a.Length > 1 ? Range(a, 1) : new RangeTextValue(1, null))),
            "PLAYER_HAS_TOWN_KEY" => Named(source, "condition.query-town-key", ("player", Player(a[0]))),
            "PLAYER_SPECIAL_ORDER_COMPLETE" => Named(source, "condition.query-special-order-complete", ("player", Player(a[0])), ("ids", List(a.Skip(1).Select(Id)))),
            "PLAYER_SPECIAL_ORDER_ACTIVE" => Named(source, "condition.query-special-order-active", ("player", Player(a[0])), ("ids", List(a.Skip(1).Select(Id)))),
            "PLAYER_HAS_HEARD_SONG" => Named(source, "condition.query-heard-song", ("player", Player(a[0])), ("ids", List(a.Skip(1).Select(Id)))),
            "PLAYER_KILLED_MONSTERS" => DescribeKilledMonsters(a, source),
            _ => a.Length == 3
                ? Named(source, "condition.player-stat-min", ("player", Player(a[0])), ("stat", Term("stat", a[1])), ("min", Num(SafeGameQuery.ParseInt(a[2]))))
                : Named(source, "condition.player-stat-range", ("player", Player(a[0])), ("stat", Term("stat", a[1])),
                    ("min", Num(SafeGameQuery.ParseInt(a[2]))), ("max", Num(SafeGameQuery.ParseInt(a[3]))))
        };
    }

    private static ConditionTextSpec DescribeMuseumDonations(string[] a, ConditionExpression source)
    {
        bool hasMax = a.Length >= 2 && SafeGameQuery.IsInt(a[1]);
        RangeTextValue count = new(SafeGameQuery.ParseInt(a[0]), hasMax ? SafeGameQuery.ParseInt(a[1]) : null);
        int typeStart = hasMax ? 2 : 1;
        return typeStart >= a.Length
            ? Named(source, "condition.query-museum-donations", ("count", count))
            : Named(source, "condition.query-museum-donations-type",
                ("types", List(a.Skip(typeStart).Select(Text))), ("count", count));
    }

    private static ConditionTextSpec DescribeKilledMonsters(string[] a, ConditionExpression source)
    {
        string[] monsters = a.Skip(1).TakeWhile(value => !SafeGameQuery.IsInt(value)).ToArray();
        int countIndex = 1 + monsters.Length;
        RangeTextValue count = countIndex < a.Length ? Range(a, countIndex) : new RangeTextValue(1, null);
        return Named(source, "condition.query-killed-monsters",
            ("player", Player(a[0])), ("monsters", List(monsters.Select(id => (ConditionTextValue)new MonsterTextValue(id)))), ("count", count));
    }

    private static TermTextValue Player(string value) => Term("player", value);
    private static ConditionTextValue QueryNpc(string value) => value.ToUpperInvariant() is "ANY" or "ANYDATEABLE" ? Term("npc-key", value.ToLowerInvariant()) : Npc(value);
    private static ConditionTextValue QueryLocation(string value) => SafeGameQuery.IsContextLocation(value) ? Term("location-key", value.ToLowerInvariant()) : Term("location", value);
    private static RangeTextValue Range(string[] a, int index) => new(SafeGameQuery.ParseInt(a[index]), a.Length > index + 1 ? SafeGameQuery.ParseInt(a[index + 1]) : null);

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
        // The game checks only the first listed season, so later seasons are named as ignored rather than allowed.
        string first = condition.Seasons[0];
        string[] ignored = condition.Seasons.Skip(1).Where(season => !season.Equals(first, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return ignored.Length == 0
            ? NamedNegative(condition, "condition.season", "condition.season-not", ("seasons", List([Term("season", first)])))
            : NamedNegative(condition, "condition.season-first-only", "condition.season-first-only-not",
                ("seasons", List([Term("season", first)])), ("ignored", List(ignored.Select(season => Term("season", season)))));
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

    internal static MailDisplayInfo ExtractMailInfo(string? rawMailEntry, Func<string, string>? parseText = null)
    {
        if (rawMailEntry is null)
            return new MailDisplayInfo(MailLookupKind.HiddenStoryFlag);
        int marker = rawMailEntry.IndexOf("[#]", StringComparison.Ordinal);
        if (marker < 0)
            return new MailDisplayInfo(MailLookupKind.MailNoTitle);
        string rawTitle = rawMailEntry[(marker + 3)..].Trim();
        if (rawTitle.Length == 0)
            return new MailDisplayInfo(MailLookupKind.MailNoTitle);
        string parsed = (parseText is not null ? parseText(rawTitle) : rawTitle).Trim();
        return parsed.Length == 0
            ? new MailDisplayInfo(MailLookupKind.MailNoTitle)
            : new MailDisplayInfo(MailLookupKind.MailTitled, parsed);
    }

    internal static string? ExtractRecipeDisplayName(string? rawRecipeEntry, bool isCooking, Func<string, string?>? resolveItem = null)
    {
        if (string.IsNullOrWhiteSpace(rawRecipeEntry))
            return null;
        string[] fields = rawRecipeEntry.Split('/');
        int displayIndex = isCooking ? 4 : 5;
        if (fields.Length > displayIndex && !string.IsNullOrWhiteSpace(fields[displayIndex]))
            return fields[displayIndex].Trim();
        if (fields.Length > 2 && !string.IsNullOrWhiteSpace(fields[2]) && resolveItem is not null)
        {
            string outputToken = fields[2].Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
            if (outputToken.Length == 0)
                return null;
            if (!isCooking && fields.Length > 3 && fields[3].Trim().Equals("true", StringComparison.OrdinalIgnoreCase) && !outputToken.StartsWith('('))
                outputToken = "(BC)" + outputToken;
            string? resolved = resolveItem(outputToken);
            if (!string.IsNullOrWhiteSpace(resolved))
                return resolved;
        }
        return null;
    }

    internal static string ExtractMonsterDisplayName(string? rawMonsterEntry, string fallbackId)
    {
        if (string.IsNullOrWhiteSpace(rawMonsterEntry))
            return fallbackId;
        string[] fields = rawMonsterEntry.Split('/');
        return fields.Length > 14 && !string.IsNullOrWhiteSpace(fields[14])
            ? fields[14].Trim()
            : fallbackId;
    }

    internal static string? ExtractQuestTitle(string? rawQuestEntry)
    {
        if (string.IsNullOrWhiteSpace(rawQuestEntry))
            return null;
        string[] fields = rawQuestEntry.Split('/');
        return fields.Length > 1 && !string.IsNullOrWhiteSpace(fields[1])
            ? fields[1].Trim()
            : null;
    }
}

internal enum MailLookupKind { Unknown, MailNoTitle, MailTitled, HiddenStoryFlag }
internal readonly record struct MailDisplayInfo(MailLookupKind Kind, string? Title = null);

internal sealed record ConditionDisplayResolver(Func<string, string> Npc, Func<string, string?> Item, Func<string, string, string> Term, Func<int, string> Time)
{
    internal Func<string, MailDisplayInfo>? Mail { get; init; }
    internal Func<string, bool, string?>? Recipe { get; init; }
    internal Func<string, string?>? Building { get; init; }
    internal Func<string, string?>? Monster { get; init; }
    internal Func<string, string?>? Quest { get; init; }
}

internal static class ConditionTextFormatter
{
    internal static string Format(ConditionTextSpec spec, Func<string, IReadOnlyDictionary<string, string>, string> translate, ConditionDisplayResolver resolver)
    {
        if (resolver.Mail is { } mailLookup && spec.Arguments.TryGetValue("id", out ConditionTextValue? mailArg) && mailArg is MailTextValue mailVal)
        {
            MailDisplayInfo info = mailLookup(mailVal.MailId);
            if (spec.LocalizationKey == "condition.special-mail")
            {
                if (info.Kind == MailLookupKind.MailTitled && !string.IsNullOrWhiteSpace(info.Title))
                    return translate("condition.legacy-send-mail", new Dictionary<string, string> { ["id"] = info.Title });
                if (info.Kind == MailLookupKind.MailNoTitle)
                    return translate("condition.legacy-send-mail", new Dictionary<string, string> { ["id"] = mailVal.MailId });
                return translate("condition.special-mail", new Dictionary<string, string>());
            }
            if (spec.LocalizationKey is "condition.mail" or "condition.mail-host" or "condition.mail-host-or-local")
            {
                if (info.Kind == MailLookupKind.HiddenStoryFlag)
                    return translate(spec.Negated ? "condition.story-flag-not" : "condition.story-flag",
                        new Dictionary<string, string> { ["id"] = mailVal.MailId });
                if (info.Kind == MailLookupKind.MailTitled && !string.IsNullOrWhiteSpace(info.Title))
                    return translate(spec.Negated ? "condition.mail-titled-not" : "condition.mail-titled",
                        new Dictionary<string, string> { ["title"] = info.Title });
            }
            if (spec.LocalizationKey == "condition.query-mail")
            {
                if (info.Kind == MailLookupKind.HiddenStoryFlag)
                    return translate(spec.Negated ? "condition.story-flag-not" : "condition.story-flag",
                        new Dictionary<string, string> { ["id"] = mailVal.MailId });
                if (info.Kind == MailLookupKind.MailTitled && !string.IsNullOrWhiteSpace(info.Title))
                {
                    bool isDefaultPlayer = spec.Arguments.TryGetValue("player", out ConditionTextValue? pVal)
                        && pVal is TermTextValue pTerm && pTerm.Value.ToUpperInvariant() is "CURRENT" or "TARGET";
                    bool isDefaultState = spec.Arguments.TryGetValue("state", out ConditionTextValue? sVal)
                        && sVal is TermTextValue sTerm && sTerm.Value.ToLowerInvariant() is "any" or "received";
                    if (isDefaultPlayer && isDefaultState)
                        return translate(spec.Negated ? "condition.mail-titled-not" : "condition.mail-titled",
                            new Dictionary<string, string> { ["title"] = info.Title });

                    Dictionary<string, string> queryArgs = spec.Arguments.ToDictionary(
                        pair => pair.Key,
                        pair => pair.Key == "id" ? info.Title : FormatValue(pair.Value, resolver, translate));
                    string queryText = translate(spec.LocalizationKey, queryArgs);
                    return spec.Negated
                        ? translate("condition.not", new Dictionary<string, string> { ["condition"] = queryText })
                        : queryText;
                }
            }
        }

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

    private static string ResolveItem(string id, ConditionDisplayResolver resolver, Func<string, IReadOnlyDictionary<string, string>, string> translate)
    {
        string? name = resolver.Item(id);
        return string.IsNullOrWhiteSpace(name)
            ? translate("item.unknown", new Dictionary<string, string> { ["id"] = id })
            : name;
    }

    private static string ResolveOptional(string? resolved, string fallback)
        => string.IsNullOrWhiteSpace(resolved) ? fallback : resolved;

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
        ItemTextValue x => ResolveItem(x.Id, resolver, translate),
        MailTextValue x => x.MailId,
        RecipeTextValue x => ResolveOptional(resolver.Recipe?.Invoke(x.Name, x.IsCooking), x.Name),
        BuildingTextValue x => ResolveOptional(resolver.Building?.Invoke(x.Id), x.Id),
        MonsterTextValue x => ResolveOptional(resolver.Monster?.Invoke(x.Id), x.Id),
        QuestTextValue x => ResolveOptional(resolver.Quest?.Invoke(x.Id), x.Id),
        TimeTextValue x => resolver.Time(x.Value),
        TermTextValue x => resolver.Term(x.Group, x.Value),
        CalendarDateTextValue x => translate("condition.season-date", new Dictionary<string, string>
        {
            ["season"] = resolver.Term("season", x.Season), ["day"] = x.Day.ToString(CultureInfo.CurrentCulture)
        }),
        NestedConditionTextValue x => Format(x.Spec, translate, resolver),
        RangeTextValue x => x.Max is not long max || max == int.MaxValue
            ? translate("condition.range-min", new Dictionary<string, string> { ["min"] = x.Min.ToString(CultureInfo.CurrentCulture) })
            : max == x.Min ? translate("condition.range-exact", new Dictionary<string, string> { ["value"] = max.ToString(CultureInfo.CurrentCulture) })
            : translate("condition.range-between", new Dictionary<string, string>
                { ["min"] = x.Min.ToString(CultureInfo.CurrentCulture), ["max"] = max.ToString(CultureInfo.CurrentCulture) }),
        ListTextValue x => string.Join(", ", x.Values.Select(item => FormatValue(item, resolver, translate))),
        FriendshipRequirementsTextValue x => string.Join(", ", x.Values.Select(item => $"{resolver.Npc(item.Npc)} ≥ {FormatFriendship(item.Points, translate)}")),
        ShippedRequirementsTextValue x => string.Join(", ", x.Values.Select(item => $"{ResolveItem(item.ItemId, resolver, translate)} × {item.Count}")),
        _ => ""
    };
}
