using System.Globalization;

namespace StardewGallery;

internal sealed record SafeQueryClause(string Name, string[] Arguments, bool Negated, IReadOnlyList<SafeQueryClause>[]? SubQueries = null);

/// <summary>
/// The read-only, deterministic subset of Stardew Valley 1.6.15 built-in game state queries.
/// Splitting follows GameStateQuery.Parse (quote-aware, a single leading '!' negates, names are case-insensitive);
/// argument checks follow each DefaultResolvers method. It never resolves or calls registered query delegates:
/// random, third-party, unlisted built-in and malformed queries are rejected as a whole and stay unknown.
/// </summary>
internal static class SafeGameQuery
{
    internal const int MaxAnyDepth = 4;
    private static readonly StringComparer IgnoreCase = StringComparer.OrdinalIgnoreCase;

    /// <summary>1.6.15 queries whose result depends on a random draw.</summary>
    private static readonly HashSet<string> RandomQueries = new(IgnoreCase) { "RANDOM", "SYNCED_CHOICE", "SYNCED_RANDOM", "SYNCED_SUMMER_RAIN_RANDOM" };

    /// <summary>Every query and alias DefaultResolvers registers in 1.6.15, used only to explain why a query is unknown.</summary>
    private static readonly HashSet<string> VanillaQueries = new(IgnoreCase)
    {
        "ANY", "DATE_RANGE", "SEASON_DAY", "DAY_OF_MONTH", "DAY_OF_WEEK", "DAYS_PLAYED", "IS_GREEN_RAIN_DAY", "IS_FESTIVAL_DAY",
        "IS_PASSIVE_FESTIVAL_OPEN", "IS_PASSIVE_FESTIVAL_TODAY", "SEASON", "YEAR", "TIME", "IS_EVENT", "EVENT_ID", "CAN_BUILD_CABIN",
        "CAN_BUILD_FOR_CABINS", "BUILDINGS_CONSTRUCTED", "FARM_CAVE", "FARM_NAME", "FARM_TYPE", "FOUND_ALL_LOST_BOOKS",
        "HAS_TARGET_LOCATION", "IS_COMMUNITY_CENTER_COMPLETE", "IS_CUSTOM_FARM_TYPE", "IS_HOST", "IS_ISLAND_NORTH_BRIDGE_FIXED",
        "IS_JOJA_MART_COMPLETE", "IS_MULTIPLAYER", "IS_VISITING_ISLAND", "LOCATION_ACCESSIBLE", "LOCATION_CONTEXT",
        "LOCATION_HAS_CUSTOM_FIELD", "LOCATION_IS_INDOORS", "LOCATION_IS_OUTDOORS", "LOCATION_IS_MINES", "LOCATION_IS_SKULL_CAVE",
        "LOCATION_NAME", "LOCATION_UNIQUE_NAME", "LOCATION_SEASON", "MUSEUM_DONATIONS", "WEATHER", "WORLD_STATE_FIELD", "WORLD_STATE_ID",
        "MINE_LOWEST_LEVEL_REACHED", "PLAYER_BASE_COMBAT_LEVEL", "PLAYER_BASE_FARMING_LEVEL", "PLAYER_BASE_FISHING_LEVEL",
        "PLAYER_BASE_FORAGING_LEVEL", "PLAYER_BASE_LUCK_LEVEL", "PLAYER_BASE_MINING_LEVEL", "PLAYER_COMBAT_LEVEL", "PLAYER_FARMING_LEVEL",
        "PLAYER_FISHING_LEVEL", "PLAYER_FORAGING_LEVEL", "PLAYER_LUCK_LEVEL", "PLAYER_MINING_LEVEL", "PLAYER_CURRENT_MONEY",
        "PLAYER_FARMHOUSE_UPGRADE", "PLAYER_GENDER", "PLAYER_HAS_ACHIEVEMENT", "PLAYER_HAS_ALL_ACHIEVEMENTS", "PLAYER_HAS_BUFF",
        "PLAYER_HAS_CAUGHT_FISH", "PLAYER_HAS_CONVERSATION_TOPIC", "PLAYER_HAS_CRAFTING_RECIPE", "PLAYER_HAS_COOKING_RECIPE",
        "PLAYER_HAS_DIALOGUE_ANSWER", "PLAYER_HAS_HEARD_SONG", "PLAYER_HAS_ITEM", "PLAYER_HAS_MAIL", "PLAYER_HAS_PROFESSION",
        "PLAYER_HAS_RUN_TRIGGER_ACTION", "PLAYER_HAS_SECRET_NOTE", "PLAYER_HAS_SEEN_EVENT", "PLAYER_HAS_TOWN_KEY",
        "PLAYER_HAS_TRASH_CAN_LEVEL", "PLAYER_HAS_TRINKET", "PLAYER_LOCATION_CONTEXT", "PLAYER_LOCATION_NAME",
        "PLAYER_LOCATION_UNIQUE_NAME", "PLAYER_MOD_DATA", "PLAYER_MONEY_EARNED", "PLAYER_SHIPPED_BASIC_ITEM",
        "PLAYER_SPECIAL_ORDER_ACTIVE", "PLAYER_SPECIAL_ORDER_RULE_ACTIVE", "PLAYER_SPECIAL_ORDER_COMPLETE", "PLAYER_KILLED_MONSTERS",
        "PLAYER_STAT", "PLAYER_VISITED_LOCATION", "PLAYER_FRIENDSHIP_POINTS", "PLAYER_HAS_CHILDREN", "PLAYER_HAS_PET", "PLAYER_HEARTS",
        "PLAYER_HAS_MET", "PLAYER_NPC_RELATIONSHIP", "PLAYER_PLAYER_RELATIONSHIP", "PLAYER_PREFERRED_PET", "RANDOM", "SYNCED_CHOICE",
        "SYNCED_RANDOM", "SYNCED_SUMMER_RAIN_RANDOM", "ITEM_CONTEXT_TAG", "ITEM_CATEGORY", "ITEM_HAS_EXPLICIT_OBJECT_CATEGORY",
        "ITEM_ID", "ITEM_ID_PREFIX", "ITEM_NUMERIC_ID", "ITEM_OBJECT_TYPE", "ITEM_PRICE", "ITEM_QUALITY", "ITEM_STACK", "ITEM_TYPE",
        "ITEM_EDIBILITY", "TRUE", "FALSE"
    };

    /// <summary>Skill level queries and the skill they read; the _BASE variants ignore buffs.</summary>
    internal static readonly IReadOnlyDictionary<string, (string Skill, bool Base)> SkillQueries = new Dictionary<string, (string, bool)>(IgnoreCase)
    {
        ["PLAYER_FARMING_LEVEL"] = ("farming", false), ["PLAYER_MINING_LEVEL"] = ("mining", false),
        ["PLAYER_FORAGING_LEVEL"] = ("foraging", false), ["PLAYER_FISHING_LEVEL"] = ("fishing", false),
        ["PLAYER_COMBAT_LEVEL"] = ("combat", false), ["PLAYER_LUCK_LEVEL"] = ("luck", false),
        ["PLAYER_BASE_FARMING_LEVEL"] = ("farming", true), ["PLAYER_BASE_MINING_LEVEL"] = ("mining", true),
        ["PLAYER_BASE_FORAGING_LEVEL"] = ("foraging", true), ["PLAYER_BASE_FISHING_LEVEL"] = ("fishing", true),
        ["PLAYER_BASE_COMBAT_LEVEL"] = ("combat", true), ["PLAYER_BASE_LUCK_LEVEL"] = ("luck", true)
    };

    private static readonly string[] RelationshipTypes = ["friendly", "roommate", "dating", "engaged", "married", "divorced"];
    private static readonly string[] MailTypes = ["any", "received", "mailbox", "tomorrow"];

    internal static bool TryParse(string text, out IReadOnlyList<SafeQueryClause> clauses)
        => TryParseDepth(text, 0, out clauses);

    private static bool TryParseDepth(string text, int depth, out IReadOnlyList<SafeQueryClause> clauses)
    {
        List<SafeQueryClause> parsed = [];
        clauses = parsed;
        if (depth > MaxAnyDepth || string.IsNullOrWhiteSpace(text) || GameQuerySplit.HasUnclosedQuote(text, ','))
            return false;
        foreach (string segment in GameQuerySplit.Clauses(text))
        {
            if (GameQuerySplit.HasUnclosedQuote(segment, ' ') || !TryParseClause(segment, depth, out SafeQueryClause? clause))
            {
                clauses = [];
                return false;
            }
            parsed.Add(clause!);
        }
        return parsed.Count > 0;
    }

    /// <summary>The i18n key explaining why a query can't be evaluated safely, or null when it can.</summary>
    internal static string? UnknownReasonKey(string text)
    {
        if (TryParse(text, out _))
            return null;
        List<string> names = [];
        if (!CollectClauseNames(text, 0, names))
            return "event-detail.unknown.invalid";
        if (names.Any(RandomQueries.Contains))
            return "event-detail.unknown.random";
        if (names.Any(name => !VanillaQueries.Contains(name)))
            return "event-detail.unknown.query-custom";
        return "event-detail.unknown.query-unsupported";
    }

    private static bool CollectClauseNames(string text, int depth, List<string> names)
    {
        if (depth > MaxAnyDepth || string.IsNullOrWhiteSpace(text) || GameQuerySplit.HasUnclosedQuote(text, ','))
            return false;
        string[] segments = GameQuerySplit.Clauses(text);
        if (segments.Length == 0)
            return false;
        foreach (string segment in segments)
        {
            if (GameQuerySplit.HasUnclosedQuote(segment, ' '))
                return false;
            string[] tokens = GameQuerySplit.Arguments(segment);
            if (tokens.Length == 0 || tokens.Any(string.IsNullOrWhiteSpace))
                return false;
            string name = tokens[0].StartsWith('!') ? tokens[0][1..] : tokens[0];
            // A fully quoted query ("PLAYER_HEARTS ...") has spaces in tokens[0]; "!!SEASON" still starts with '!'.
            if (name.Length == 0 || name.StartsWith('!') || name.Any(char.IsWhiteSpace))
                return false;
            names.Add(name);
            if (name.Equals("ANY", StringComparison.OrdinalIgnoreCase) && tokens.Length >= 2 && depth < MaxAnyDepth)
            {
                for (int i = 1; i < tokens.Length; i++)
                    if (!CollectClauseNames(tokens[i], depth + 1, names))
                        return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Supported clauses inside a well-quoted query string, skipping unlisted sibling clauses (such as RANDOM or
    /// IS_EVENT) so replay scene extraction still reads positive SEASON/TIME/WEATHER clauses.
    /// Never used for condition truth evaluation, which requires every clause via <see cref="TryParse"/>.
    /// </summary>
    internal static IReadOnlyList<SafeQueryClause> ReadSupportedClauses(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || GameQuerySplit.HasUnclosedQuote(text, ','))
            return [];
        string[] segments = GameQuerySplit.Clauses(text);
        if (segments.Any(segment => GameQuerySplit.HasUnclosedQuote(segment, ' ')))
            return [];
        List<SafeQueryClause> clauses = [];
        foreach (string segment in segments)
            if (TryParseClause(segment, 0, out SafeQueryClause? clause))
                clauses.Add(clause!);
        return clauses;
    }

    private static bool TryParseClause(string segment, int depth, out SafeQueryClause? clause)
    {
        clause = null;
        string[] tokens = GameQuerySplit.Arguments(segment);
        if (tokens.Length == 0 || tokens.Any(string.IsNullOrWhiteSpace))
            return false;
        // GameStateQuery.Parse strips one '!'; "!!NAME" names no query.
        bool negated = tokens[0].StartsWith('!');
        string name = (negated ? tokens[0][1..] : tokens[0]).ToUpperInvariant();
        string[] a = tokens[1..];
        if (name == "ANY")
        {
            if (a.Length == 0 || depth >= MaxAnyDepth)
                return false;
            IReadOnlyList<SafeQueryClause>[] subQueries = new IReadOnlyList<SafeQueryClause>[a.Length];
            for (int i = 0; i < a.Length; i++)
            {
                if (!TryParseDepth(a[i], depth + 1, out IReadOnlyList<SafeQueryClause> subClauses))
                    return false;
                subQueries[i] = subClauses;
            }
            clause = new(name, a, negated, subQueries);
            return true;
        }
        if (!IsValid(name, a))
            return false;
        clause = new(name, a, negated);
        return true;
    }

    private static bool IsValid(string name, string[] a)
    {
        if (SkillQueries.ContainsKey(name))
            return a.Length is 2 or 3 && IsPlayer(a[0]) && Int(a[1]) && (a.Length == 2 || Int(a[2]));
        return name switch
        {
            "TRUE" or "FALSE" or "IS_GREEN_RAIN_DAY" or "IS_COMMUNITY_CENTER_COMPLETE" or "IS_JOJA_MART_COMPLETE"
                or "IS_ISLAND_NORTH_BRIDGE_FIXED" or "FOUND_ALL_LOST_BOOKS" or "IS_MULTIPLAYER" or "IS_HOST" => a.Length == 0,
            "SEASON" => a.Length > 0 && a.All(IsSeason),
            "LOCATION_SEASON" or "WEATHER" => a.Length >= 2,
            "SEASON_DAY" => a.Length > 0 && a.Length % 2 == 0
                && Enumerable.Range(0, a.Length / 2).All(index => IsSeason(a[index * 2]) && Int(a[index * 2 + 1])),
            "YEAR" or "TIME" or "DAYS_PLAYED" or "MINE_LOWEST_LEVEL_REACHED" => a.Length is 1 or 2 && a.All(Int),
            "DAY_OF_MONTH" => a.Length > 0 && a.All(value => Int(value) || value.Equals("even", StringComparison.OrdinalIgnoreCase)
                || value.Equals("odd", StringComparison.OrdinalIgnoreCase)),
            "DAY_OF_WEEK" => a.Length > 0 && a.All(value => TryGetDayOfWeek(value, out _)),
            "IS_FESTIVAL_DAY" => a.Length <= 2 && (a.Length < 2 || Int(a[1])),
            "IS_PASSIVE_FESTIVAL_TODAY" or "IS_VISITING_ISLAND" => a.Length == 1,
            "WORLD_STATE_ID" => a.Length >= 1,
            "MUSEUM_DONATIONS" => a.Length >= 1 && Int(a[0]),
            "BUILDINGS_CONSTRUCTED" => a.Length is >= 1 and <= 5 && (a.Length < 3 || Int(a[2])) && (a.Length < 4 || Int(a[3]))
                && (a.Length < 5 || bool.TryParse(a[4], out _)),
            "PLAYER_HEARTS" or "PLAYER_FRIENDSHIP_POINTS" => a.Length is 3 or 4 && IsPlayer(a[0]) && Int(a[2]) && (a.Length == 3 || Int(a[3])),
            "PLAYER_NPC_RELATIONSHIP" => a.Length >= 3 && IsPlayer(a[0])
                && a.Skip(2).All(value => RelationshipTypes.Contains(value.ToLowerInvariant())),
            "PLAYER_HAS_SEEN_EVENT" or "PLAYER_HAS_CONVERSATION_TOPIC" or "PLAYER_HAS_RUN_TRIGGER_ACTION" or "PLAYER_HAS_DIALOGUE_ANSWER"
                or "PLAYER_VISITED_LOCATION" or "PLAYER_HAS_CRAFTING_RECIPE" or "PLAYER_HAS_COOKING_RECIPE"
                or "PLAYER_HAS_MET" or "PLAYER_SPECIAL_ORDER_COMPLETE" or "PLAYER_SPECIAL_ORDER_ACTIVE"
                or "PLAYER_HAS_HEARD_SONG" => a.Length >= 2 && IsPlayer(a[0]),
            "PLAYER_HAS_PET" or "PLAYER_HAS_TOWN_KEY" => a.Length == 1 && IsPlayer(a[0]),
            "PLAYER_HAS_CHILDREN" => a.Length is >= 1 and <= 3 && IsPlayer(a[0]) && (a.Length < 2 || Int(a[1])) && (a.Length < 3 || Int(a[2])),
            "PLAYER_FARMHOUSE_UPGRADE" => a.Length is 2 or 3 && IsPlayer(a[0]) && Int(a[1]) && (a.Length == 2 || Int(a[2])),
            "PLAYER_KILLED_MONSTERS" => ValidKilledMonsters(a),
            "PLAYER_HAS_MAIL" => a.Length is 2 or 3 && IsPlayer(a[0]) && (a.Length == 2 || MailTypes.Contains(a[2].ToLowerInvariant())),
            "PLAYER_HAS_ITEM" => a.Length is >= 2 and <= 4 && IsPlayer(a[0]) && (a.Length < 3 || Int(a[2])) && (a.Length < 4 || Int(a[3])),
            "PLAYER_HAS_ACHIEVEMENT" or "PLAYER_HAS_SECRET_NOTE" => a.Length == 2 && IsPlayer(a[0]) && Int(a[1]),
            "PLAYER_GENDER" => a.Length == 2 && IsPlayer(a[0]),
            "PLAYER_CURRENT_MONEY" or "PLAYER_MONEY_EARNED" => a.Length is 2 or 3 && IsPlayer(a[0]) && Int(a[1]) && (a.Length == 2 || Int(a[2])),
            "PLAYER_STAT" => a.Length is 3 or 4 && IsPlayer(a[0]) && Int(a[2]) && (a.Length == 3 || Int(a[3])),
            _ => false
        };
    }

    private static bool ValidKilledMonsters(string[] a)
    {
        if (a.Length < 2 || !IsPlayer(a[0]))
            return false;
        int index = 1;
        while (index < a.Length && !Int(a[index]))
            index++;
        if (index < 2)
            return false;
        if (index == a.Length)
            return true;
        return a.Length <= index + 2 && (a.Length == index + 1 || Int(a[index + 1]));
    }

    /// <summary>Queries answered from the shared date/time snapshot or sub-queries rather than from per-event facts.</summary>
    internal static bool UsesSnapshot(string name) => name is "ANY" or "TRUE" or "FALSE" or "SEASON" or "SEASON_DAY" or "YEAR" or "TIME"
        or "DAYS_PLAYED" or "DAY_OF_MONTH" or "DAY_OF_WEEK" or "IS_PASSIVE_FESTIVAL_TODAY" or "PLAYER_STAT";

    internal static IEnumerable<SafeQueryClause> FlattenLeaves(IEnumerable<SafeQueryClause> clauses)
    {
        foreach (SafeQueryClause clause in clauses)
        {
            if (clause.SubQueries is { } subQueries)
            {
                foreach (IReadOnlyList<SafeQueryClause> sub in subQueries)
                    foreach (SafeQueryClause leaf in FlattenLeaves(sub))
                        yield return leaf;
            }
            else
                yield return clause;
        }
    }

    internal static bool? Evaluate(IReadOnlyList<SafeQueryClause> clauses, ConditionEvaluationContext context)
    {
        bool missing = false;
        foreach (SafeQueryClause clause in clauses)
        {
            bool? matches = EvaluateClause(clause, context);
            if (clause.Negated && matches is not null) matches = !matches.Value;
            if (matches == false) return false;
            missing |= matches is null;
        }
        return missing ? null : true;
    }

    internal static bool? EvaluateClause(SafeQueryClause clause, ConditionEvaluationContext context)
    {
        if (clause.Name == "ANY" && clause.SubQueries is { } subQueries)
        {
            bool anyUnknown = false;
            foreach (IReadOnlyList<SafeQueryClause> sub in subQueries)
            {
                bool? subResult = Evaluate(sub, context);
                if (subResult == true)
                    return true;
                anyUnknown |= subResult is null;
            }
            return anyUnknown ? null : false;
        }
        string[] a = clause.Arguments;
        return clause.Name switch
        {
            "TRUE" => true,
            "FALSE" => false,
            "SEASON" => context.Season is { } season ? a.Any(value => value.Equals(season, StringComparison.OrdinalIgnoreCase)) : null,
            "SEASON_DAY" => context.Season is not null && context.DayOfMonth is int day
                ? Enumerable.Range(0, a.Length / 2).Any(index => a[index * 2].Equals(context.Season, StringComparison.OrdinalIgnoreCase)
                    && ParseInt(a[index * 2 + 1]) == day) : null,
            "YEAR" => InRange(context.Year, a, 0),
            "TIME" => InRange(context.Time, a, 0),
            "DAYS_PLAYED" => InRange(context.DaysPlayed, a, 0),
            "DAY_OF_MONTH" => context.DayOfMonth is int dayOfMonth ? a.Any(value => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int wanted)
                ? wanted == dayOfMonth
                : value.Equals("even", StringComparison.OrdinalIgnoreCase) ? dayOfMonth % 2 == 0 : dayOfMonth % 2 == 1) : null,
            "DAY_OF_WEEK" => context.Details?.Weekday is { } weekday
                ? a.Any(value => TryGetDayOfWeek(value, out DayOfWeek wanted) && wanted == weekday) : null,
            "IS_PASSIVE_FESTIVAL_TODAY" => context.Details?.PassiveFestivals?.Contains(a[0]),
            "PLAYER_STAT" => EvaluateStat(a, context.Details),
            _ => context.Details?.QueryFacts?.GetValueOrDefault(FactKey(clause))
        };
    }

    /// <summary>Inclusive [min, max] like the game's TryGetInt + TryGetOptionalInt(defaultValue: int.MaxValue).</summary>
    private static bool? InRange(long? value, string[] a, int index)
        => value is long current ? current >= ParseInt(a[index]) && current <= OptionalMax(a, index + 1) : null;

    internal static int OptionalMax(string[] a, int index) => a.Length > index ? ParseInt(a[index]) : int.MaxValue;

    private static bool? EvaluateStat(string[] a, ConditionReadState? state)
    {
        if (state?.PlayerStats is not { } stats || !stats.TryGetValue(StatKey(a[0], a[1]), out var values))
            return null;
        long min = ParseInt(a[2]), max = OptionalMax(a, 3);
        bool Matches(uint value) => value >= min && value <= max;
        return a[0].Equals("All", StringComparison.OrdinalIgnoreCase) ? values.All(Matches) : values.Any(Matches);
    }

    /// <summary>WorldDate.TryGetDayOfWeekFor (1.6.15): integer day (mod 7) or case-insensitive short/full English weekday.</summary>
    internal static bool TryGetDayOfWeek(string value, out DayOfWeek day)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int numeric))
        {
            day = (DayOfWeek)(numeric % 7);
            return true;
        }
        day = default;
        string[] names = ["sun", "mon", "tue", "wed", "thu", "fri", "sat"];
        string lower = value.ToLowerInvariant();
        for (int index = 0; index < names.Length; index++)
            if (lower == names[index] || lower == ((DayOfWeek)index).ToString().ToLowerInvariant())
            {
                day = (DayOfWeek)index;
                return true;
            }
        return false;
    }

    internal static string StatKey(string player, string stat) => player.ToUpperInvariant() + ":" + stat;
    internal static string FactKey(SafeQueryClause clause) => clause.Name + "\u001f" + string.Join("\u001f", clause.Arguments);
    internal static bool IsPlayer(string value) => value.ToUpperInvariant() is "CURRENT" or "TARGET" or "HOST" or "ANY" or "ALL" || long.TryParse(value, out _);
    internal static bool IsSeason(string value) => value.ToLowerInvariant() is "spring" or "summer" or "fall" or "winter";
    /// <summary>The game's location keys: Here is the player's location, Target the checked (event) location.</summary>
    internal static bool IsContextLocation(string value) => value.Equals("Here", StringComparison.OrdinalIgnoreCase) || value.Equals("Target", StringComparison.OrdinalIgnoreCase);
    internal static bool IsInt(string text) => Int(text);
    private static bool Int(string text) => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
    internal static int ParseInt(string text) => int.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture);
}
