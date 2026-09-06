using System.Globalization;

namespace StardewGallery;

internal sealed class ConditionParser(Func<string, string[]> splitPreconditions, Func<string, string[]> splitArguments)
{
    private static readonly IReadOnlyDictionary<string, (string Name, bool Negated)> Aliases =
        new Dictionary<string, (string, bool)>(StringComparer.Ordinal)
        {
            ["e"] = ("SawEvent", false), ["h"] = ("MissingPet", false), ["H"] = ("IsHost", false),
            ["Hn"] = ("HostMail", false), ["Hl"] = ("HostMail", true), ["*"] = ("WorldState", false),
            ["*n"] = ("HostOrLocalMail", false), ["*l"] = ("HostOrLocalMail", true),
            ["m"] = ("EarnedMoney", false), ["M"] = ("HasMoney", false), ["c"] = ("FreeInventorySlots", false),
            ["C"] = ("CommunityCenterOrWarehouseDone", false), ["X"] = ("CommunityCenterOrWarehouseDone", true),
            ["D"] = ("Dating", false), ["j"] = ("DaysPlayed", false), ["J"] = ("JojaBundlesDone", false),
            ["f"] = ("Friendship", false), ["F"] = ("FestivalDay", true), ["r"] = ("Random", false),
            ["s"] = ("Shipped", false), ["S"] = ("SawSecretNote", false), ["q"] = ("ChoseDialogueAnswers", false),
            ["n"] = ("LocalMail", false), ["N"] = ("GoldenWalnuts", false), ["l"] = ("LocalMail", true),
            ["L"] = ("InUpgradedHouse", false), ["t"] = ("Time", false), ["w"] = ("Weather", false),
            ["d"] = ("DayOfWeek", true), ["O"] = ("Spouse", false), ["o"] = ("Spouse", true),
            ["R"] = ("Roommate", false), ["Rf"] = ("Roommate", true), ["v"] = ("NpcVisible", false),
            ["p"] = ("NpcVisibleHere", false), ["z"] = ("Season", true), ["B"] = ("SpouseBed", false),
            ["b"] = ("ReachedMineBottom", false), ["y"] = ("Year", false), ["g"] = ("Gender", false),
            ["i"] = ("HasItem", false), ["k"] = ("SawEvent", true), ["a"] = ("Tile", false),
            ["A"] = ("ActiveDialogueEvent", true), ["u"] = ("DayOfMonth", false), ["U"] = ("UpcomingFestival", true),
            ["G"] = ("GameStateQuery", false), ["x"] = ("SendMail", false)
        };

    private static readonly IReadOnlyDictionary<string, string> CanonicalNames = new[]
    {
        "SawEvent", "MissingPet", "IsHost", "HostMail", "WorldState", "HostOrLocalMail", "EarnedMoney", "HasMoney",
        "FreeInventorySlots", "CommunityCenterOrWarehouseDone", "Dating", "DaysPlayed", "JojaBundlesDone", "Friendship",
        "FestivalDay", "Random", "Shipped", "SawSecretNote", "ChoseDialogueAnswers", "LocalMail", "GoldenWalnuts",
        "InUpgradedHouse", "Time", "Weather", "DayOfWeek", "Spouse", "Roommate", "NpcVisible", "NpcVisibleHere",
        "Season", "SpouseBed", "ReachedMineBottom", "Year", "Gender", "HasItem", "Tile", "ActiveDialogueEvent",
        "DayOfMonth", "UpcomingFestival", "GameStateQuery", "Skill"
    }.ToDictionary(name => name, StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyDictionary<string, string> NegativeCanonicalNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["NotHostMail"] = "HostMail", ["NotHostOrLocalMail"] = "HostOrLocalMail",
            ["NotCommunityCenterOrWarehouseDone"] = "CommunityCenterOrWarehouseDone",
            ["NotFestivalDay"] = "FestivalDay", ["NotLocalMail"] = "LocalMail",
            ["NotDayOfWeek"] = "DayOfWeek", ["NotSpouse"] = "Spouse", ["NotRoommate"] = "Roommate",
            ["NotSeason"] = "Season", ["NotSawEvent"] = "SawEvent",
            ["NotActiveDialogueEvent"] = "ActiveDialogueEvent", ["NotUpcomingFestival"] = "UpcomingFestival"
        };

    internal static IReadOnlyCollection<string> SupportedCanonicalNames => CanonicalNames.Values.ToArray();
    internal static IReadOnlyDictionary<string, (string Name, bool Negated)> SupportedAliases => Aliases;

    internal ConditionSet ParseRawKey(string rawKey) => Parse(splitPreconditions(rawKey).Skip(1).ToArray());
    internal string? CheckReadOnly(string rawKey, Func<string, string?> check)
    {
        // Native SendMail preconditions mutate mail and seen-event state, even during catalog selection.
        if (ParseRawKey(rawKey).Conditions.Any(c => c is LegacySendMailCondition
            or OpaqueCondition { KnownConditionName: "SendMail" }))
            return null;
        return check(rawKey);
    }
    internal ConditionSet Parse(IReadOnlyList<string> rawSegments) => new(rawSegments.Select(ParseSegment).ToList());

    internal ConditionExpression ParseSegment(string rawSegment)
    {
        string segment = rawSegment.Trim();
        bool negated = false;
        while (segment.StartsWith('!'))
        {
            negated = !negated;
            segment = segment[1..].TrimStart();
        }
        if (segment.Length == 0 || segment.Count(c => c == '"') % 2 != 0)
            return new OpaqueCondition(OpaqueConditionKind.MalformedSyntax, null, ConditionSource.OpaqueEventPrecondition, rawSegment, negated);
        string[] tokens = splitArguments(segment);
        if (tokens.Length == 0)
            return new OpaqueCondition(OpaqueConditionKind.MalformedSyntax, null, ConditionSource.OpaqueEventPrecondition, rawSegment, negated);

        string head = tokens[0];
        string canonical;
        if (Aliases.TryGetValue(head, out var alias))
        {
            canonical = alias.Name;
            negated ^= alias.Negated;
        }
        else if (NegativeCanonicalNames.TryGetValue(head, out canonical!))
            negated = !negated;
        else if (head.Equals("SendMail", StringComparison.OrdinalIgnoreCase))
            canonical = "SendMail";
        else if (!CanonicalNames.TryGetValue(head, out canonical!))
            return Unknown(rawSegment, negated);

        string[] args = tokens.Skip(1).ToArray();
        ConditionSource source = canonical == "GameStateQuery" ? ConditionSource.GameStateQuery : ConditionSource.LegacyEventPrecondition;
        ConditionExpression? parsed = args.Any(string.IsNullOrWhiteSpace) ? null : ParseCanonical(canonical, args, source, rawSegment, negated);
        return parsed ?? new OpaqueCondition(OpaqueConditionKind.MalformedKnown, canonical, ConditionSource.OpaqueEventPrecondition, rawSegment, negated);
    }

    private static ConditionExpression? ParseCanonical(string name, string[] a, ConditionSource s, string raw, bool n) => name switch
    {
        "SendMail" when a.Length == 1 || a.Length == 2 && bool.TryParse(a[1], out _) => new LegacySendMailCondition(a[0], a.Length == 2 && bool.Parse(a[1]), s, raw, n),
        "SawEvent" when a.Length > 0 => new SawEventCondition(a, s, raw, n),
        "MissingPet" when a.Length <= 1 => new MissingPetCondition(a.FirstOrDefault(), s, raw, n),
        "IsHost" when a.Length == 0 => new IsHostCondition(s, raw, n),
        "HostMail" when a.Length == 1 => new MailCondition(a[0], ConditionPlayerScope.HostPlayer, s, raw, n),
        "WorldState" when a.Length == 1 => new WorldStateCondition(a[0], s, raw, n),
        "HostOrLocalMail" when a.Length == 1 => new MailCondition(a[0], ConditionPlayerScope.HostOrLocal, s, raw, n),
        "EarnedMoney" when Int(a, 0, out int earned) => new EarnedMoneyCondition(earned, s, raw, n),
        "HasMoney" when Int(a, 0, out int money) => new HasMoneyCondition(money, s, raw, n),
        "FreeInventorySlots" when Int(a, 0, out int slots) => new FreeInventorySlotsCondition(slots, s, raw, n),
        "CommunityCenterOrWarehouseDone" when a.Length == 0 => new CommunityCenterOrWarehouseDoneCondition(s, raw, n),
        "Dating" when a.Length == 1 => new DatingCondition(a[0], s, raw, n),
        "DaysPlayed" when Int(a, 0, out int daysPlayed) => new DaysPlayedCondition(daysPlayed, ConditionPlayerScope.HostPlayer, s, raw, n),
        "JojaBundlesDone" when a.Length == 0 => new JojaBundlesDoneCondition(s, raw, n),
        "Friendship" when Pairs(a, out List<FriendshipRequirement>? friends) => new FriendshipCondition(friends, ConditionPlayerScope.LocalPlayer, s, raw, n),
        "FestivalDay" when a.Length == 0 => new FestivalDayCondition(s, raw, n),
        "Random" when Float(a, out float probability) => new RandomCondition(probability, s, raw, n),
        "Shipped" when Pairs(a, out List<ShippedRequirement>? shipped) => new ShippedCondition(shipped, s, raw, n),
        "SawSecretNote" when Int(a, 0, out int note) => new SawSecretNoteCondition(note, s, raw, n),
        "ChoseDialogueAnswers" when a.Length > 0 => new ChoseDialogueAnswersCondition(a, s, raw, n),
        "LocalMail" when a.Length == 1 => new MailCondition(a[0], ConditionPlayerScope.LocalPlayer, s, raw, n),
        "GoldenWalnuts" when Int(a, 0, out int walnuts) => new GoldenWalnutsCondition(walnuts, s, raw, n),
        "InUpgradedHouse" when OptionalInt(a, 2, out int level) => new InUpgradedHouseCondition(level, s, raw, n),
        "Time" when a.Length == 2 && TryInt(a[0], out int min) && TryInt(a[1], out int max) => new TimeCondition(min, max, s, raw, n),
        "Weather" when a.Length == 1 => new WeatherCondition(a[0] == "rainy" ? WeatherKind.Rainy : a[0] == "sunny" ? WeatherKind.Sunny : WeatherKind.Custom, a[0], s, raw, n),
        "DayOfWeek" when Days(a, out List<DayOfWeek>? weekdays) => new DayOfWeekCondition(weekdays, s, raw, n),
        "Spouse" when a.Length == 1 => new SpouseCondition(a[0], s, raw, n),
        "Roommate" when a.Length == 0 => new RoommateCondition(s, raw, n),
        "NpcVisible" when a.Length == 1 => new NpcVisibleCondition(a[0], false, s, raw, n),
        "NpcVisibleHere" when a.Length == 1 => new NpcVisibleCondition(a[0], true, s, raw, n),
        "Season" when Seasons(a) => new SeasonCondition(a, s, raw, n),
        "SpouseBed" when a.Length == 0 => new SpouseBedCondition(s, raw, n),
        "ReachedMineBottom" when OptionalInt(a, 1, out int bottoms) => new ReachedMineBottomCondition(bottoms, s, raw, n),
        "Year" when Int(a, 0, out int year) => new YearCondition(year, s, raw, n),
        "Gender" when a.Length == 1 && (a[0].Equals("male", StringComparison.OrdinalIgnoreCase) || a[0].Equals("female", StringComparison.OrdinalIgnoreCase)) => new GenderCondition(a[0], s, raw, n),
        "HasItem" when a.Length == 1 => new HasItemCondition(a[0], s, raw, n),
        "Tile" when Tiles(a, out List<TilePosition>? tiles) => new TileCondition(tiles, s, raw, n),
        "ActiveDialogueEvent" when a.Length == 1 => new ActiveDialogueEventCondition(a[0], s, raw, n),
        "DayOfMonth" when a.Length > 0 && a.All(value => TryInt(value, out _)) => new DayOfMonthCondition(a.Select(ParseInt).ToList(), s, raw, n),
        "UpcomingFestival" when Int(a, 0, out int festivalDays) => new UpcomingFestivalCondition(festivalDays, s, raw, n),
        "GameStateQuery" when a.Length > 0 => new NativeQueryCondition(string.Join(' ', a), s, raw, n),
        "Skill" when a.Length == 2 && TryInt(a[1], out int skillLevel) => new SkillCondition(a[0], skillLevel, s, raw, n),
        _ => null
    };

    private static bool Int(string[] a, int index, out int value) { value = 0; return a.Length == index + 1 && TryInt(a[index], out value); }
    private static bool OptionalInt(string[] a, int fallback, out int value) { value = fallback; return a.Length == 0 || Int(a, 0, out value); }
    private static bool Float(string[] a, out float value) { value = 0; return a.Length == 1 && float.TryParse(a[0], NumberStyles.Float, CultureInfo.InvariantCulture, out value); }
    private static bool TryInt(string value, out int result) => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
    private static int ParseInt(string value) => int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
    private static bool Seasons(string[] a) => a.Length > 0 && a.All(value => value.Equals("spring", StringComparison.OrdinalIgnoreCase) || value.Equals("summer", StringComparison.OrdinalIgnoreCase) || value.Equals("fall", StringComparison.OrdinalIgnoreCase) || value.Equals("winter", StringComparison.OrdinalIgnoreCase));
    private static bool Days(string[] a, out List<DayOfWeek> values) { values = []; string[] names = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"]; foreach (string value in a) { int index = Array.FindIndex(names, name => name.Equals(value, StringComparison.OrdinalIgnoreCase) || ((DayOfWeek)Array.IndexOf(names, name)).ToString().Equals(value, StringComparison.OrdinalIgnoreCase)); if (index < 0) return false; values.Add((DayOfWeek)index); } return values.Count > 0; }
    private static bool Tiles(string[] a, out List<TilePosition> values) { values = []; if (a.Length == 0 || a.Length % 2 != 0) return false; for (int i = 0; i < a.Length; i += 2) { if (!TryInt(a[i], out int x) || !TryInt(a[i + 1], out int y)) return false; values.Add(new(x, y)); } return true; }
    private static bool Pairs(string[] a, out List<FriendshipRequirement> values) { values = []; if (a.Length == 0 || a.Length % 2 != 0) return false; for (int i = 0; i < a.Length; i += 2) { if (!TryInt(a[i + 1], out int number)) return false; values.Add(new(a[i], number)); } return true; }
    private static bool Pairs(string[] a, out List<ShippedRequirement> values) { values = []; if (a.Length == 0 || a.Length % 2 != 0) return false; for (int i = 0; i < a.Length; i += 2) { if (!TryInt(a[i + 1], out int number)) return false; values.Add(new(a[i], number)); } return true; }
    private static OpaqueCondition Unknown(string raw, bool negated) => new(OpaqueConditionKind.UnknownType, null, ConditionSource.OpaqueEventPrecondition, raw, negated);
}
