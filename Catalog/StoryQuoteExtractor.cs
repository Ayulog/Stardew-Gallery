using System.Text;

namespace StardewGallery;

/// <summary>
/// Statically extracts the protagonist's (or first non-player speaking actor's) first spoken line from an event,
/// stripping Stardew Valley 1.6.15 dialogue control tags while strictly hiding quotes for locked unseen stories.
/// </summary>
internal static class StoryQuoteExtractor
{
    internal readonly record struct ExtractedSegment(string Text, bool IsNarration);

    // Exact 18 tokens from StardewValley.Dialogue.percentTokens (1.6.15), plus %noturn.
    private static readonly string[] PercentTokens =
    [
        "%adj", "%noun", "%place", "%spouse", "%name", "%firstnameletter",
        "%time", "%band", "%book", "%pet", "%farm", "%favorite",
        "%fork", "%year", "%kid1", "%kid2", "%revealtaste", "%season",
        "%noturn"
    ];

    internal static string? GetVisibleQuote(
        GalleryEvent entry,
        bool seen,
        bool unlockAll,
        bool parallelBranchUnlocked,
        Func<string, string[]> parseCommands,
        Func<string, string[]> splitArguments,
        Func<string, string?>? loadTranslation = null,
        string? farmerName = null,
        bool farmerIsMale = true,
        string? spouseName = null)
    {
        // Anti-spoiler invariant: locked unseen stories must never expose dialogue text.
        if (!ReplayAccessPolicy.IsUnlockedForViewing(entry.Kind, seen, unlockAll, parallelBranchUnlocked))
            return null;

        HashSet<string> owners = entry.Ownership.Owners
            .Select(owner => owner.Name)
            .ToHashSet(StringComparer.Ordinal);

        if (owners.Count > 0
            && FindFirstLine(entry.Resolved, actor => owners.Contains(actor), parseCommands, splitArguments, loadTranslation, farmerName, farmerIsMale, spouseName) is string ownerQuote)
            return ownerQuote;

        return FindFirstLine(entry.Resolved, _ => true, parseCommands, splitArguments, loadTranslation, farmerName, farmerIsMale, spouseName);
    }

    private static string? FindFirstLine(
        ResolvedEvent entry,
        Func<string, bool> acceptSpeaker,
        Func<string, string[]> parseCommands,
        Func<string, string[]> splitArguments,
        Func<string, string?>? loadTranslation,
        string? farmerName,
        bool farmerIsMale,
        string? spouseName)
    {
        string? fallbackNarrationLine = null;
        string? fallbackPunctuationLine = null;
        HashSet<string> visitedScripts = new(StringComparer.Ordinal);
        IEnumerable<string> scripts = new[] { entry.ResolvedScript }.Concat(entry.Fragments.Scripts);

        foreach (string script in scripts)
        {
            bool isRoot = ReferenceEquals(script, entry.ResolvedScript) || script == entry.ResolvedScript;
            if (!visitedScripts.Add(script))
                continue;
            foreach (string command in EventCommandEnumerator.EnumerateCommands(script, skipHeader: isRoot, parseCommands))
            {
                string[] args = splitArguments(command);
                if (args.Length < 3)
                    continue;
                bool isSpeak = args[0].Equals("speak", StringComparison.OrdinalIgnoreCase);
                bool isSplitSpeak = !isSpeak && args[0].Equals("splitSpeak", StringComparison.OrdinalIgnoreCase);
                if (!isSpeak && !isSplitSpeak)
                    continue;

                string speaker = NormalizeSpeaker(args[1], spouseName);
                if (string.IsNullOrWhiteSpace(speaker)
                    || speaker.Equals("farmer", StringComparison.OrdinalIgnoreCase)
                    || speaker.Equals("otherFarmers", StringComparison.OrdinalIgnoreCase)
                    || !acceptSpeaker(speaker))
                    continue;

                IEnumerable<string> candidates = isSplitSpeak
                    ? args[2].Split('~', StringSplitOptions.RemoveEmptyEntries).Select(part => part.Trim())
                    : [args[2]];

                foreach (string rawCandidate in candidates)
                {
                    string? resolved = ParallelBranchAnalyzer.ResolveIfTranslation(rawCandidate, loadTranslation, out bool unresolved);
                    if (unresolved || string.IsNullOrWhiteSpace(resolved))
                        continue;
                    foreach (ExtractedSegment segment in ExtractSegments(resolved, farmerName, farmerIsMale))
                    {
                        if (segment.Text.Any(char.IsLetterOrDigit))
                        {
                            if (!segment.IsNarration)
                                return segment.Text;
                            fallbackNarrationLine ??= segment.Text;
                        }
                        else
                        {
                            fallbackPunctuationLine ??= segment.Text;
                        }
                    }
                }
            }
        }

        return fallbackNarrationLine ?? fallbackPunctuationLine;
    }

    internal static IEnumerable<string> ExtractSpokenSegments(string masterString, string? farmerName = null, bool farmerIsMale = true)
        => ExtractSegments(masterString, farmerName, farmerIsMale).Select(segment => segment.Text);

    internal static IEnumerable<ExtractedSegment> ExtractSegments(string masterString, string? farmerName = null, bool farmerIsMale = true)
    {
        if (string.IsNullOrWhiteSpace(masterString))
            yield break;

        string primary = masterString.Split("||", StringSplitOptions.None)[0];
        primary = ApplyGenderBlocks(primary, farmerIsMale);
        string[] segments = primary.Split('#');

        for (int i = 0; i < segments.Length; i++)
        {
            string seg = segments[i];
            if (string.IsNullOrWhiteSpace(seg))
                continue;

            string trimmedStart = seg.TrimStart();
            if (trimmedStart.StartsWith('$'))
            {
                string[] tokens = trimmedStart.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                string head = tokens.Length > 0 ? tokens[0] : "";

                if (head is "$b" or "$e" or "$k" or "$q" or "$v" or "$action" or "$t")
                    continue;

                if (head == "$r")
                {
                    // In Dialogue.parseDialogueString, $r consumes the following segment as a player response option.
                    i++;
                    continue;
                }

                if (head == "$p")
                {
                    if (i + 1 < segments.Length)
                    {
                        i++;
                        string branch = PickFirstNonEmptyPipeBranch(segments[i]);
                        foreach (ExtractedSegment nested in ExtractSegments(branch, farmerName, farmerIsMale))
                            yield return nested;
                    }
                    continue;
                }

                if (head is "$d" or "$query")
                {
                    string remainder = i + 1 < segments.Length ? string.Join("#", segments.Skip(i + 1)) : "";
                    if (remainder.Length > 0)
                    {
                        string branch = PickFirstNonEmptyPipeBranch(remainder);
                        foreach (ExtractedSegment nested in ExtractSegments(branch, farmerName, farmerIsMale))
                            yield return nested;
                    }
                    yield break;
                }

                if (head is "$1" or "$c")
                {
                    if (i + 1 < segments.Length)
                    {
                        ExtractedSegment cleaned = CleanSpokenSegment(segments[i + 1], farmerName, farmerIsMale);
                        if (cleaned.Text.Length > 0)
                            yield return cleaned;
                    }
                    // Advance by 1 so the loop's i++ visits segments[i + 2] ($e, skipped) and then segments[i + 3]
                    // when segments[i + 1] had no letters/digits or was narration.
                    i += 1;
                    continue;
                }

                if (head == "$y")
                {
                    int firstQuote = trimmedStart.IndexOf('\'');
                    int lastQuote = trimmedStart.LastIndexOf('\'');
                    if (firstQuote >= 0 && lastQuote > firstQuote)
                    {
                        string prompt = trimmedStart[(firstQuote + 1)..lastQuote].Split('_')[0];
                        ExtractedSegment cleanedPrompt = CleanSpokenSegment(prompt, farmerName, farmerIsMale);
                        if (cleanedPrompt.Text.Length > 0)
                            yield return cleanedPrompt;
                    }
                    continue;
                }
            }

            ExtractedSegment line = CleanSpokenSegment(seg, farmerName, farmerIsMale);
            if (line.Text.Length > 0)
                yield return line;
        }
    }

    internal static string CleanSpokenLine(string segment, string? farmerName = null, bool farmerIsMale = true)
        => CleanSpokenSegment(segment, farmerName, farmerIsMale).Text;

    internal static ExtractedSegment CleanSpokenSegment(string segment, string? farmerName = null, bool farmerIsMale = true)
    {
        string text = ApplyGenderBlocks(segment, farmerIsMale);
        text = ApplyLineGenderSwitch(text, farmerIsMale);
        text = text.Replace("%fork", "", StringComparison.Ordinal)
                   .Replace("%noturn", "", StringComparison.Ordinal);

        // Dialogue.prepareCurrentDialogueForDisplay (1.6.15): a leading '%' that does not start any Dialogue.percentTokens
        // marks portrait-less narration text and strips that single '%'.
        bool isNarration = false;
        string trimmedStart = text.TrimStart();
        if (trimmedStart.StartsWith('%') && !StartsWithPercentToken(trimmedStart))
        {
            isNarration = true;
            text = trimmedStart[1..];
        }

        if (!string.IsNullOrWhiteSpace(farmerName))
            text = text.Replace("@", farmerName, StringComparison.Ordinal);
        else if (text.Contains('@'))
            text = StripBareFarmerPlaceholder(text);

        text = StripEmotionTokens(text);
        text = text.Trim().Trim('{', '}').Trim();
        if (text.StartsWith('>'))
            text = text[1..].TrimStart();

        return new ExtractedSegment(CollapseWhitespace(text), isNarration);
    }

    private static bool StartsWithPercentToken(string value)
    {
        foreach (string token in PercentTokens)
            if (value.StartsWith(token, StringComparison.Ordinal))
                return true;
        return false;
    }

    private static string ApplyGenderBlocks(string text, bool farmerIsMale)
    {
        int searchFrom = 0;
        while (true)
        {
            int open = text.IndexOf("${", searchFrom, StringComparison.Ordinal);
            if (open < 0)
                return text;
            int close = text.IndexOf("}$", open + 2, StringComparison.Ordinal);
            if (close < 0)
                return text;
            string inner = text[(open + 2)..close];
            char sep = inner.Contains('¦') ? '¦' : '^';
            string[] parts = inner.Split(sep);
            string chosen = !farmerIsMale && parts.Length > 1 && parts[1].Length > 0 ? parts[1] : parts[0];
            text = text[..open] + chosen + text[(close + 2)..];
            searchFrom = open + chosen.Length;
        }
    }

    private static string ApplyLineGenderSwitch(string text, bool farmerIsMale)
    {
        int split = text.IndexOf('^');
        if (split < 0)
            split = text.IndexOf('¦');
        if (split < 0)
            return text;
        string male = text[..split];
        string female = text[(split + 1)..];
        string chosen = farmerIsMale ? male : female;
        return string.IsNullOrWhiteSpace(chosen) ? (farmerIsMale ? female : male) : chosen;
    }

    private static string StripEmotionTokens(string text)
    {
        foreach (string named in new[] { "$neutral", "$h", "$s", "$u", "$l", "$a", "$b", "$e", "$k" })
            text = text.Replace(named, "", StringComparison.Ordinal);

        StringBuilder sb = new(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '$' && i + 1 < text.Length && char.IsDigit(text[i + 1]))
            {
                i++;
                while (i + 1 < text.Length && char.IsDigit(text[i + 1]))
                    i++;
                continue;
            }
            sb.Append(text[i]);
        }
        return sb.ToString();
    }

    private static string StripBareFarmerPlaceholder(string text)
    {
        // Clean ", @" or "@, " neatly when no live farmer name is supplied.
        return text.Replace(", @", "", StringComparison.Ordinal)
                   .Replace("，@", "", StringComparison.Ordinal)
                   .Replace("@, ", "", StringComparison.Ordinal)
                   .Replace("@，", "", StringComparison.Ordinal)
                   .Replace(" @", "", StringComparison.Ordinal)
                   .Replace("@", "", StringComparison.Ordinal);
    }

    private static string PickFirstNonEmptyPipeBranch(string value)
    {
        foreach (string branch in value.Split('|'))
            if (!string.IsNullOrWhiteSpace(branch))
                return branch;
        return "";
    }

    private static string CollapseWhitespace(string text)
    {
        if (text.Length == 0)
            return "";
        StringBuilder sb = new(text.Length);
        bool previousSpace = false;
        foreach (char ch in text)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!previousSpace)
                    sb.Append(' ');
                previousSpace = true;
            }
            else
            {
                sb.Append(ch);
                previousSpace = false;
            }
        }
        return sb.ToString().Trim();
    }

    private static string NormalizeSpeaker(string actor, string? spouseName)
    {
        string trimmed = actor.TrimEnd('?');
        return trimmed.Equals("spouse", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(spouseName)
            ? spouseName
            : trimmed;
    }
}
