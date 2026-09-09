namespace StardewGallery;

internal static class ConditionEventReferences
{
    internal static IReadOnlyList<string> Read(ConditionExpression expression) => expression switch
    {
        SawEventCondition seen => seen.EventIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToArray(),
        ConditionSet set => set.Conditions.SelectMany(Read).Distinct(StringComparer.Ordinal).ToArray(),
        NativeQueryCondition query when SafeGameQuery.TryParse(query.Query, out var clauses) => clauses
            .Where(clause => clause.Name == "PLAYER_HAS_SEEN_EVENT").SelectMany(clause => clause.Arguments.Skip(1)).Distinct(StringComparer.Ordinal).ToArray(),
        _ => []
    };
}
