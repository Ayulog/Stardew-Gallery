using System.Globalization;

namespace StardewGallery;

internal sealed record SafeQueryClause(string Name, string[] Arguments, bool Negated);

// A deliberately restricted, flat GSQ grammar. It never resolves or calls registered delegates.
internal static class SafeGameQuery
{
    internal static bool TryParse(string text, out IReadOnlyList<SafeQueryClause> clauses)
    {
        List<SafeQueryClause> parsed = [];
        clauses = parsed;
        if (string.IsNullOrWhiteSpace(text) || text.IndexOfAny(['"', '\\']) >= 0)
            return false;
        foreach (string segment in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] tokens = segment.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0) return false;
            bool negated = tokens[0].StartsWith('!');
            string name = (negated ? tokens[0][1..] : tokens[0]).ToUpperInvariant();
            string[] args = tokens[1..];
            bool valid = name switch
            {
                "IS_PASSIVE_FESTIVAL_TODAY" => args.Length == 1,
                "SEASON_DAY" => args.Length > 0 && args.Length % 2 == 0
                    && Enumerable.Range(0, args.Length / 2).All(index => IsSeason(args[index * 2]) && Int(args[index * 2 + 1], out _)),
                "PLAYER_STAT" => args.Length is 3 or 4 && IsPlayer(args[0]) && Int(args[2], out _)
                    && (args.Length == 3 || Int(args[3], out _)),
                _ => false
            };
            if (!valid) { clauses = []; return false; }
            parsed.Add(new(name, args, negated));
        }
        return parsed.Count > 0;
    }

    internal static bool? Evaluate(IReadOnlyList<SafeQueryClause> clauses, ConditionEvaluationContext context)
    {
        bool missing = false;
        foreach (SafeQueryClause clause in clauses)
        {
            string[] a = clause.Arguments;
            bool? matches = clause.Name switch
            {
                "IS_PASSIVE_FESTIVAL_TODAY" => context.Details?.PassiveFestivals?.Contains(a[0]),
                "SEASON_DAY" => context.Season is not null && context.DayOfMonth is int day
                    ? Enumerable.Range(0, a.Length / 2).Any(index => a[index * 2].Equals(context.Season, StringComparison.OrdinalIgnoreCase)
                        && ParseInt(a[index * 2 + 1]) == day) : null,
                "PLAYER_STAT" => EvaluateStat(a, context.Details),
                _ => null
            };
            if (clause.Negated && matches is not null) matches = !matches.Value;
            if (matches == false) return false;
            missing |= matches is null;
        }
        return missing ? null : true;
    }

    private static bool? EvaluateStat(string[] a, ConditionReadState? state)
    {
        if (state?.PlayerStats is not { } stats || !stats.TryGetValue(StatKey(a[0], a[1]), out var values))
            return null;
        int min = ParseInt(a[2]), max = a.Length == 4 ? ParseInt(a[3]) : int.MaxValue;
        bool Matches(uint value) => value >= min && value <= max;
        return a[0].Equals("All", StringComparison.OrdinalIgnoreCase) ? values.All(Matches) : values.Any(Matches);
    }

    internal static string StatKey(string player, string stat) => player.ToUpperInvariant() + ":" + stat;
    private static bool IsPlayer(string value) => value.ToUpperInvariant() is "CURRENT" or "TARGET" or "HOST" or "ANY" or "ALL" || long.TryParse(value, out _);
    private static bool IsSeason(string value) => value.ToLowerInvariant() is "spring" or "summer" or "fall" or "winter";
    private static bool Int(string text, out int value) => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    internal static int ParseInt(string text) => int.Parse(text, CultureInfo.InvariantCulture);
}
