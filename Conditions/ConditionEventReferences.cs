namespace StardewGallery;

internal static class ConditionEventReferences
{
    /// <summary>Every event ID a condition mentions, including "not seen" requirements; used for navigation links.</summary>
    internal static IReadOnlyList<string> Read(ConditionExpression expression) => expression switch
    {
        SawEventCondition seen => Ids(seen.EventIds),
        ConditionSet set => set.Conditions.SelectMany(Read).Distinct(StringComparer.Ordinal).ToArray(),
        NativeQueryCondition query when SafeGameQuery.TryParse(query.Query, out var clauses) => Ids(SeenEventIds(clauses)),
        _ => []
    };

    /// <summary>Event IDs that must already be seen for the condition to hold; "not seen" references aren't dependencies.</summary>
    internal static IReadOnlyList<string> Required(ConditionExpression expression) => expression switch
    {
        SawEventCondition { Negated: false } seen => Ids(seen.EventIds),
        ConditionSet set => set.Conditions.SelectMany(Required).Distinct(StringComparer.Ordinal).ToArray(),
        // A negated query requires its clauses only when it is a single negated clause ("!G !PLAYER_HAS_SEEN_EVENT ...").
        NativeQueryCondition query when SafeGameQuery.TryParse(query.Query, out var clauses) => Ids(SeenEventIds(query.Negated
            ? clauses.Count == 1 && clauses[0].Negated ? clauses : []
            : clauses.Where(clause => !clause.Negated))),
        _ => []
    };

    private static IEnumerable<string> SeenEventIds(IEnumerable<SafeQueryClause> clauses)
        => clauses.Where(clause => clause.Name == "PLAYER_HAS_SEEN_EVENT").SelectMany(clause => clause.Arguments.Skip(1));

    private static string[] Ids(IEnumerable<string> ids) => ids.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToArray();
}
