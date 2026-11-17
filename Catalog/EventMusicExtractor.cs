namespace StardewGallery;

/// <summary>
/// Statically extracts an event's cutscene music cue ID from its root header slot or first playMusic command,
/// matching Stardew Valley 1.6.15's Event.InitializeEvent and Event.DefaultCommands.PlayMusic rules.
/// </summary>
internal static class EventMusicExtractor
{
    internal static string? ExtractCueId(
        ResolvedEvent entry,
        Func<string, string[]> parseCommands,
        Func<string, string[]> splitArguments,
        IReadOnlySet<string>? dialogueAnswers = null)
    {
        string[] root = parseCommands(entry.ResolvedScript);
        if (root.Length >= 3 && NormalizeCue(root[0], dialogueAnswers) is string headerCue)
            return headerCue;

        HashSet<string> visited = new(StringComparer.Ordinal);
        IEnumerable<string> scripts = new[] { entry.ResolvedScript }.Concat(entry.Fragments.Scripts);
        foreach (string script in scripts)
        {
            bool isRoot = ReferenceEquals(script, entry.ResolvedScript) || script == entry.ResolvedScript;
            if (!visited.Add(script))
                continue;
            foreach (string command in EventCommandEnumerator.EnumerateCommands(script, skipHeader: isRoot, parseCommands))
            {
                string[] args = splitArguments(command);
                if (args.Length >= 2
                    && args[0].Equals("playMusic", StringComparison.OrdinalIgnoreCase)
                    && NormalizeCue(string.Join(' ', args.Skip(1)), dialogueAnswers) is string commandCue)
                    return commandCue;
            }
        }

        return null;
    }

    internal static string? NormalizeCue(string? rawCue, IReadOnlySet<string>? dialogueAnswers = null)
    {
        if (string.IsNullOrWhiteSpace(rawCue))
            return null;
        string cue = rawCue.Trim();
        if (cue.Length == 0
            || cue == "-1"
            || cue.Equals("none", StringComparison.OrdinalIgnoreCase)
            || cue.Equals("continue", StringComparison.OrdinalIgnoreCase)
            || cue.Equals("turn_off", StringComparison.OrdinalIgnoreCase)
            || cue.Contains("pause", StringComparison.OrdinalIgnoreCase)
            || cue.EndsWith("_ambient", StringComparison.OrdinalIgnoreCase))
            return null;

        // Event.DefaultCommands.PlayMusic resolves samBand from Sam's 2-heart genre question (77/78/79).
        if (cue.Equals("samBand", StringComparison.Ordinal))
        {
            if (dialogueAnswers?.Contains("78") == true) return "shimmeringbastion";
            if (dialogueAnswers?.Contains("79") == true) return "honkytonky";
            if (dialogueAnswers?.Contains("77") == true) return "heavy";
            return "poppy";
        }

        return cue;
    }

    internal static string FormatTrackTitle(string? cueId, Func<string, string?>? resolveSongTitle, string noneLabel)
    {
        if (string.IsNullOrWhiteSpace(cueId))
            return noneLabel;
        string? resolved = resolveSongTitle?.Invoke(cueId);
        return string.IsNullOrWhiteSpace(resolved) ? cueId : resolved.Trim();
    }
}
