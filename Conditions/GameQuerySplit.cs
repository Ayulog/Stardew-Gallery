namespace StardewGallery;

/// <summary>
/// A line-by-line port of ArgUtility.SplitQuoteAware (Stardew Valley 1.6.15), so pure modules split game state
/// queries exactly like GameStateQuery.SplitRaw (',' keeping quotes) and SplitBySpaceQuoteAware (' ' removing them).
/// SourcesRuntimeChecks compares it with the game's own method.
/// </summary>
internal static class GameQuerySplit
{
    /// <summary>GameStateQuery.SplitRaw: comma-separated clauses, trimmed, empty ones removed, quotes and escapes kept.</summary>
    internal static string[] Clauses(string query)
        => SplitQuoteAware(query, ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries, keepQuotesAndEscapes: true);

    /// <summary>ArgUtility.SplitBySpaceQuoteAware: space-separated arguments with quotes and escapes resolved.</summary>
    internal static string[] Arguments(string clause)
        => SplitQuoteAware(clause, ' ', StringSplitOptions.RemoveEmptyEntries, keepQuotesAndEscapes: false, out _);

    internal static bool HasUnclosedQuote(string input, char delimiter)
    {
        SplitQuoteAware(input, delimiter, StringSplitOptions.None, keepQuotesAndEscapes: false, out bool unclosed);
        return unclosed;
    }

    internal static string[] SplitQuoteAware(string input, char delimiter, StringSplitOptions splitOptions, bool keepQuotesAndEscapes)
        => SplitQuoteAware(input, delimiter, splitOptions, keepQuotesAndEscapes, out _);

    private static string[] SplitQuoteAware(string input, char delimiter, StringSplitOptions splitOptions, bool keepQuotesAndEscapes, out bool unclosedQuote)
    {
        unclosedQuote = false;
        if (string.IsNullOrEmpty(input))
            return [];
        if (!input.Contains('"'))
            return input.Split(delimiter, splitOptions);
        bool trim = false;
        if (splitOptions.HasFlag(StringSplitOptions.TrimEntries))
        {
            trim = true;
            splitOptions &= ~StringSplitOptions.TrimEntries;
        }
        bool removeEmpty = splitOptions.HasFlag(StringSplitOptions.RemoveEmptyEntries);
        string[] parts = input.Split('"');
        List<string> result = new(parts.Length * 4);
        bool inQuotes = true;
        bool previousEndedWithDelimiter = true;
        string? previous = null;
        int last = parts.Length - 1;
        for (int i = 0; i <= last; i++)
        {
            inQuotes = !inQuotes;
            string part = parts[i];
            bool replaceLast = false, appendToLast = false;
            bool endsWithDelimiter = part.EndsWith(delimiter);
            if (keepQuotesAndEscapes && i != 0)
                part = "\"" + part;
            if (!previousEndedWithDelimiter)
            {
                if (previous!.EndsWith('\\'))
                {
                    // An escaped quote: undo the quote toggle and continue the previous value.
                    part = keepQuotesAndEscapes ? previous + part : previous[..^1] + "\"" + part;
                    inQuotes = !inQuotes;
                    replaceLast = true;
                }
                else if (inQuotes || !part.StartsWith(delimiter))
                    appendToLast = true;
                else
                    part = part[1..];
            }
            if (result.Count == 0)
            {
                replaceLast = false;
                appendToLast = false;
            }
            if (inQuotes)
            {
                if (replaceLast)
                    result[^1] = part;
                else if (appendToLast)
                {
                    result[^1] += part;
                    part = result[^1];
                }
                else
                    result.Add(part);
                previous = part;
                previousEndedWithDelimiter = false;
                continue;
            }
            if (endsWithDelimiter && !removeEmpty && i != last && part.Length > 0)
                part = part[..^1];
            string[] split = part.Split(delimiter, splitOptions);
            if (split.Length == 0 || split.Length == 1 && endsWithDelimiter && split[0] == string.Empty)
                previous = string.Empty;
            else
            {
                if (replaceLast)
                {
                    result.RemoveAt(result.Count - 1);
                    result.AddRange(split);
                }
                else if (appendToLast)
                {
                    result[^1] += split[0];
                    if (split.Length > 1)
                        result.AddRange(new ArraySegment<string>(split, 1, split.Length - 1));
                }
                else
                    result.AddRange(split);
                previous = split[^1];
            }
            previousEndedWithDelimiter = endsWithDelimiter;
        }
        unclosedQuote = inQuotes;
        if (trim)
        {
            for (int i = result.Count - 1; i >= 0; i--)
            {
                result[i] = result[i].Trim();
                if (removeEmpty && result[i].Length == 0)
                    result.RemoveAt(i);
            }
        }
        return result.ToArray();
    }
}
