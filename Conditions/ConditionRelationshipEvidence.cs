namespace StardewGallery;

internal sealed record RelationshipEvidence(string Npc, int? MinimumPoints);

// Shared classification/filter evidence; reading a condition never executes a query.
internal static class ConditionRelationshipEvidence
{
    internal static IEnumerable<RelationshipEvidence> Read(IEnumerable<ConditionExpression> conditions)
    {
        foreach (ConditionExpression condition in conditions)
        {
            if (condition is NativeQueryCondition query)
            {
                // Negating a conjunction does not prove any individual positive clause.
                if (!SafeGameQuery.TryParse(query.Query, out var clauses)
                    || query.Negated && clauses.Count != 1)
                    continue;
                foreach (SafeQueryClause clause in clauses)
                    if (ReadQuery(clause, query.Negated) is { } evidence)
                        yield return evidence;
                continue;
            }
            if (condition.Negated)
                continue;
            switch (condition)
            {
                case FriendshipCondition { Scope: ConditionPlayerScope.LocalPlayer } friends:
                    foreach (FriendshipRequirement requirement in friends.Requirements.Where(value => value.Points > 0))
                        yield return new(requirement.Npc, requirement.Points);
                    break;
                case DatingCondition dating:
                    yield return new(dating.Npc, null);
                    break;
                case SpouseCondition spouse:
                    yield return new(spouse.Npc, null);
                    break;
            }
        }
    }

    private static RelationshipEvidence? ReadQuery(SafeQueryClause clause, bool outerNegated)
    {
        string[] args = clause.Arguments;
        // Current/Target use the local farmer in SafeQueryStateReader. Other selectors
        // cannot be projected into a local-player album or heart threshold without state.
        if (clause.Negated != outerNegated || args.Length < 3
            || args[0].ToUpperInvariant() is not ("CURRENT" or "TARGET")
            || args[1].ToUpperInvariant() is "ANY" or "ANYDATEABLE")
            return null;
        if (clause.Name is "PLAYER_HEARTS" or "PLAYER_FRIENDSHIP_POINTS")
        {
            int minimum = SafeGameQuery.ParseInt(args[2]);
            if (minimum <= 0 || args.Length == 4 && SafeGameQuery.ParseInt(args[3]) < minimum)
                return null;
            long points = clause.Name == "PLAYER_HEARTS" ? minimum * 250L : minimum;
            return points <= int.MaxValue ? new(args[1], (int)points) : null;
        }
        // The states are alternatives: Friendly or Divorced would not prove romance.
        return clause.Name == "PLAYER_NPC_RELATIONSHIP"
            && args.Skip(2).All(value => value.ToUpperInvariant() is "DATING" or "ENGAGED" or "MARRIED" or "ROOMMATE")
                ? new(args[1], null) : null;
    }
}
