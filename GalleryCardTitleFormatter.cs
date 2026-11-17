namespace StardewGallery;

/// <summary>
/// Pure card-title and locked-envelope clue formatter that replaces bare raw <c>EventId</c> labels with
/// <c>Hearts/Stage · Location</c> while preserving custom player names and parallel If-route markers.
/// </summary>
internal static class GalleryCardTitleFormatter
{
    internal static string FormatStoryCardTitle(
        GalleryEvent entry,
        string? customName,
        string locationLabel,
        string separator,
        bool isParallelBranch,
        string? preferredOwnerName,
        Func<int, string> formatHearts,
        Func<string, string> translateKey)
    {
        string parallelSuffix = isParallelBranch ? separator + translateKey("event.parallel-branch") : "";
        if (!string.IsNullOrWhiteSpace(customName))
            return customName.Trim() + parallelSuffix;

        string stageOrKind = FormatPrimaryLead(entry, preferredOwnerName, formatHearts, translateKey);
        if (string.IsNullOrWhiteSpace(locationLabel) || locationLabel == "-")
            return stageOrKind + parallelSuffix;

        return stageOrKind + separator + locationLabel + parallelSuffix;
    }

    internal static string FormatPrimaryLead(
        GalleryEvent entry,
        string? preferredOwnerName,
        Func<int, string> formatHearts,
        Func<string, string> translateKey)
    {
        if (entry.Kind != StoryKind.Heart)
            return translateKey("query.kind-ordinary");

        EventOwner? owner = preferredOwnerName is { Length: > 0 }
            ? entry.Ownership.Owners.FirstOrDefault(candidate => candidate.Name == preferredOwnerName)
                ?? entry.Ownership.Owners.FirstOrDefault()
            : entry.Ownership.Owners.FirstOrDefault();

        if (owner?.FriendshipPoints is int points && points > 0)
            return formatHearts((int)Math.Ceiling(points / 250d));

        if (InferRelationshipStageKey(entry.EventKey, owner?.Name ?? preferredOwnerName) is { } stageKey)
            return translateKey(stageKey);

        return translateKey("query.kind-heart");
    }

    internal static string? InferRelationshipStageKey(string? rawEventKey, string? npcName = null)
    {
        if (string.IsNullOrWhiteSpace(rawEventKey))
            return null;

        string[] segments = GameQuerySplit.SplitQuoteAware(
            rawEventKey, '/', StringSplitOptions.RemoveEmptyEntries, keepQuotesAndEscapes: true);
        for (int i = 1; i < segments.Length; i++)
        {
            string seg = segments[i].Trim();
            if (seg.Length == 0 || seg[0] == '!' || GameQuerySplit.HasUnclosedQuote(seg, ' '))
                continue;

            string[] tokens = GameQuerySplit.Arguments(seg);
            if (tokens.Length == 0)
                continue;

            string head = tokens[0];
            if ((head == "O" || head.Equals("Spouse", StringComparison.OrdinalIgnoreCase))
                && tokens.Length >= 2
                && (npcName is null || string.Equals(tokens[1], npcName, StringComparison.OrdinalIgnoreCase)))
            {
                return "status.married";
            }

            if ((head == "D" || head.Equals("Dating", StringComparison.OrdinalIgnoreCase))
                && tokens.Length >= 2
                && (npcName is null || string.Equals(tokens[1], npcName, StringComparison.OrdinalIgnoreCase)))
            {
                return "status.dating";
            }

            if (head == "R" || head.Equals("Roommate", StringComparison.OrdinalIgnoreCase))
                return "status.roommate";

            if ((head == "G" || head.Equals("GameStateQuery", StringComparison.OrdinalIgnoreCase)) && tokens.Length >= 2)
            {
                string queryText = UnsplitQuoteAware(tokens.Skip(1));
                if (InferFromGsq(queryText, npcName) is { } gsqStage)
                    return gsqStage;
            }
        }

        return null;
    }

    private static string? InferFromGsq(string queryText, string? npcName)
    {
        if (string.IsNullOrWhiteSpace(queryText) || GameQuerySplit.HasUnclosedQuote(queryText, ','))
            return null;

        foreach (string rawClause in GameQuerySplit.Clauses(queryText))
        {
            string clause = rawClause.Trim();
            if (clause.Length == 0 || clause[0] == '!' || GameQuerySplit.HasUnclosedQuote(clause, ' '))
                continue;

            string[] args = GameQuerySplit.Arguments(clause);
            if (args.Length == 0)
                continue;

            string name = args[0];
            if (name.Equals("PLAYER_NPC_RELATIONSHIP", StringComparison.OrdinalIgnoreCase) && args.Length >= 4)
            {
                string npcToken = args[2];
                if (npcToken.Equals("Any", StringComparison.OrdinalIgnoreCase)
                    || npcToken.Equals("AnyDateable", StringComparison.OrdinalIgnoreCase))
                {
                    if (npcName is not null)
                        continue;
                }
                else if (npcName is not null && !string.Equals(npcToken, npcName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (MatchRelationshipStageTypes(args.Skip(3)) is { } stage)
                    return stage;
            }
            else if (name.Equals("PLAYER_NPC_RELATIONSHIP_ANY", StringComparison.OrdinalIgnoreCase)
                && args.Length >= 3
                && npcName is null)
            {
                if (MatchRelationshipStageTypes(args.Skip(2)) is { } stage)
                    return stage;
            }
        }

        return null;
    }

    private static string? MatchRelationshipStageTypes(IEnumerable<string> rawTypes)
    {
        string[] types = rawTypes.ToArray();
        if (types.Length == 0)
            return null;

        // Every alternative must be a positive romantic/roommate relationship stage (never Friendly or Divorced).
        foreach (string type in types)
        {
            if (!type.Equals("Married", StringComparison.OrdinalIgnoreCase)
                && !type.Equals("Engaged", StringComparison.OrdinalIgnoreCase)
                && !type.Equals("Dating", StringComparison.OrdinalIgnoreCase)
                && !type.Equals("Roommate", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
        }

        if (types.Any(t => t.Equals("Married", StringComparison.OrdinalIgnoreCase)))
            return "status.married";
        if (types.Any(t => t.Equals("Engaged", StringComparison.OrdinalIgnoreCase)))
            return "status.engaged";
        if (types.Any(t => t.Equals("Dating", StringComparison.OrdinalIgnoreCase)))
            return "status.dating";
        return "status.roommate";
    }

    // Matches Preconditions.GameStateQuery (1.6.15) ArgUtility.UnsplitQuoteAware(args, ' ', 1).
    private static string UnsplitQuoteAware(IEnumerable<string> values)
        => string.Join(' ', values.Select(value =>
        {
            string escaped = value.Replace("\"", "\\\"");
            return escaped.Contains(' ') ? "\"" + escaped + "\"" : escaped;
        }));
}
