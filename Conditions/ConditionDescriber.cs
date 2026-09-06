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
internal sealed record ConditionTextSpec(string LocalizationKey, IReadOnlyDictionary<string, ConditionTextValue> Arguments, string RawFallback, bool Negated, string? NaturalNegativeKey = null);

internal static class ConditionDescriber
{
    internal static ConditionTextSpec Describe(ConditionExpression c) => c switch
    {
        SawEventCondition x => Named(x, "condition.seen", ("id", List(x.EventIds.Select(Id)))),
        MissingPetCondition x => Named(x, x.PetType is null ? "condition.missing-pet" : "condition.missing-pet-type", ("type", Text(x.PetType ?? ""))),
        IsHostCondition x => Named(x, "condition.is-host"),
        MailCondition x => Named(x, x.Scope switch { ConditionPlayerScope.HostPlayer => "condition.mail-host", ConditionPlayerScope.HostOrLocal => "condition.mail-host-or-local", _ => "condition.mail" }, ("id", Id(x.MailId))),
        WorldStateCondition x => Named(x, "condition.world-state", ("id", Id(x.Id))),
        EarnedMoneyCondition x => Named(x, "condition.earned-money", ("amount", Num(x.Minimum))),
        HasMoneyCondition x => Named(x, "condition.has-money", ("amount", Num(x.Minimum))),
        FreeInventorySlotsCondition x => Named(x, "condition.free-slots", ("count", Num(x.Minimum))),
        CommunityCenterOrWarehouseDoneCondition x => Named(x, "condition.community-complete"),
        DatingCondition x => Named(x, "condition.dating", ("npc", Npc(x.Npc))),
        DaysPlayedCondition x => Named(x, "condition.daysplayed", ("min", Num(x.Threshold + 1))),
        JojaBundlesDoneCondition x => Named(x, "condition.joja-complete"),
        FriendshipCondition x => Named(x, "condition.friendship", ("requirements", new FriendshipRequirementsTextValue(x.Requirements))),
        FestivalDayCondition x => Named(x, "condition.festival-day", negative: "condition.not-festival-day"),
        RandomCondition x => Named(x, "condition.random", ("chance", Num(x.Probability * 100))),
        ShippedCondition x => Named(x, "condition.shipped", ("requirements", new ShippedRequirementsTextValue(x.Requirements))),
        SawSecretNoteCondition x => Named(x, "condition.secret-note", ("id", Num(x.NoteId))),
        ChoseDialogueAnswersCondition x => Named(x, "condition.dialogue-answers", ("ids", List(x.AnswerIds.Select(Id)))),
        GoldenWalnutsCondition x => Named(x, "condition.walnuts", ("count", Num(x.Minimum))),
        InUpgradedHouseCondition x => Named(x, "condition.house-level", ("level", Num(x.MinimumLevel))),
        TimeCondition x => Named(x, "condition.time", ("from", new TimeTextValue(x.Min)), ("to", new TimeTextValue(x.Max))),
        WeatherCondition { Kind: WeatherKind.Rainy } x => Named(x, "condition.raining", negative: "condition.not-raining"),
        WeatherCondition { Kind: WeatherKind.Sunny } x => Named(x, "condition.not-raining", negative: "condition.raining"),
        WeatherCondition x => Named(x, "condition.weather", ("weather", Text(x.WeatherId))),
        DayOfWeekCondition x => Named(x, "condition.weekday", ("days", List(x.Days.Select(d => Term("weekday", d.ToString()))))),
        SpouseCondition x => Named(x, "condition.spouse", ("npc", Npc(x.Npc))),
        RoommateCondition x => Named(x, "condition.roommate"),
        NpcVisibleCondition x => Named(x, x.CurrentLocationOnly ? "condition.npc-visible-here" : "condition.npc-visible", ("npc", Npc(x.Npc))),
        SeasonCondition x => Named(x, "condition.season", ("seasons", List(x.Seasons.Select(s => Term("season", s))))),
        SpouseBedCondition x => Named(x, "condition.spouse-bed"),
        ReachedMineBottomCondition x => Named(x, "condition.mine-bottom", ("count", Num(x.Minimum))),
        YearCondition x => Named(x, x.DesiredYear == 1 ? "condition.year-one" : "condition.year", ("year", Num(x.DesiredYear))),
        GenderCondition x => Named(x, "condition.gender", ("gender", Term("gender", x.Gender))),
        HasItemCondition x => Named(x, "condition.has-item", ("item", new ItemTextValue(x.ItemId))),
        TileCondition x => Named(x, "condition.tile", ("positions", List(x.Positions.Select(p => Text($"{p.X},{p.Y}"))))),
        ActiveDialogueEventCondition x => Named(x, "condition.dialogue-event", ("id", Id(x.Id))),
        DayOfMonthCondition x => Named(x, "condition.day", ("day", List(x.Days.Select(d => Num(d))))),
        UpcomingFestivalCondition x => NamedNegative(x, "condition.upcoming-festival", "condition.no-upcoming-festival", ("days", Num(x.Days))),
        NativeQueryCondition x => Named(x, "condition.native-query", ("query", Text(x.Query))),
        SkillCondition x => Named(x, "condition.skill", ("skill", Term("skill", x.Skill)), ("level", Num(x.MinimumLevel))),
        LegacySendMailCondition x => Named(x with { Negated = false }, "condition.legacy-send-mail", ("id", Id(x.MailId))),
        OpaqueCondition x => Named(x with { Negated = false }, x.Kind != OpaqueConditionKind.UnknownType ? "condition.malformed" : "condition.unsupported", ("raw", Text(x.RawSegment))),
        _ => Named(c, "condition.unsupported", ("raw", Text(c.RawSegment)))
    };

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

    internal static string FormatGap(ConditionExpression condition, string value, Func<string, IReadOnlyDictionary<string, string>, string> translate, ConditionDisplayResolver resolver) => condition switch
    {
        FriendshipCondition when int.TryParse(value, out int points) => FormatFriendship(points, translate),
        TimeCondition when int.TryParse(value, out int time) => resolver.Time(time),
        SeasonCondition => string.Join(", ", value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(season => resolver.Term("season", season))),
        WeatherCondition leaf when value == leaf.WeatherId && leaf.Kind != WeatherKind.Custom
            => translate(leaf.Kind == WeatherKind.Rainy ? "condition.raining" : "condition.not-raining", new Dictionary<string, string>()),
        WeatherCondition => value,
        _ => value
    };

    private static string ResolveItem(string id, ConditionDisplayResolver resolver)
    {
        string? name = resolver.Item(id);
        return string.IsNullOrWhiteSpace(name) ? id : name;
    }

    private static string FormatFriendship(int points, Func<string, IReadOnlyDictionary<string, string>, string> translate)
        => points % 250 == 0
            ? translate("condition.hearts-value", new Dictionary<string, string> { ["hearts"] = (points / 250).ToString(CultureInfo.CurrentCulture) })
            : translate("condition.points-value", new Dictionary<string, string> { ["points"] = points.ToString(CultureInfo.CurrentCulture) });

    private static string FormatValue(ConditionTextValue value, ConditionDisplayResolver resolver, Func<string, IReadOnlyDictionary<string, string>, string> translate) => value switch
    {
        PlainTextValue x => x.Value,
        NumberTextValue x => x.Value.ToString("0.##", CultureInfo.CurrentCulture),
        NpcTextValue x => resolver.Npc(x.Name),
        ItemTextValue x => ResolveItem(x.Id, resolver),
        TimeTextValue x => resolver.Time(x.Value),
        TermTextValue x => resolver.Term(x.Group, x.Value),
        ListTextValue x => string.Join(", ", x.Values.Select(item => FormatValue(item, resolver, translate))),
        FriendshipRequirementsTextValue x => string.Join(", ", x.Values.Select(item => $"{resolver.Npc(item.Npc)} ≥ {FormatFriendship(item.Points, translate)}")),
        ShippedRequirementsTextValue x => string.Join(", ", x.Values.Select(item => $"{ResolveItem(item.ItemId, resolver)} × {item.Count}")),
        _ => ""
    };
}
