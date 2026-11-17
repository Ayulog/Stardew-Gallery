namespace StardewGallery;

internal readonly record struct EventFragmentTarget(string NormalizedLocation, string Key, bool IsTranslation)
{
    internal static string NormalizeLocation(string? location, bool isTranslation = false)
        => isTranslation || string.IsNullOrWhiteSpace(location) ? "" : location.Trim().ToLowerInvariant();

    internal static EventFragmentTarget Create(string? location, string key, bool isTranslation = false)
        => new(NormalizeLocation(location, isTranslation), key, isTranslation);
}

internal sealed record EventFragments(
    IReadOnlyList<string> Scripts,
    IReadOnlyList<string> MissingKeys,
    IReadOnlyList<EventBranchReference>? Branches = null,
    IReadOnlyDictionary<string, string>? ScriptsByKey = null,
    IReadOnlyDictionary<EventFragmentTarget, string>? ResolvedTargets = null,
    IReadOnlySet<EventFragmentTarget>? MissingTargets = null
)
{
    /// <summary>fork/switchEvent keys read from event assets (translation forks excluded), in visiting order.</summary>
    internal IReadOnlyList<EventBranchReference> BranchKeys => Branches ?? [];
}

/// <summary>A branch sub-script key. <paramref name="OtherLocation"/> is null for the root event's own location.</summary>
internal sealed record EventBranchReference(string Key, string? OtherLocation, bool Found);

internal static class EventCommandEnumerator
{
    internal static IEnumerable<string> EnumerateCommands(
        string script,
        bool skipHeader,
        Func<string, string[]> parseCommands)
    {
        string[] commands = parseCommands(script);
        int start = skipHeader && commands.Length >= 3 ? 3 : 0;
        for (int index = start; index < commands.Length; index++)
        {
            foreach (string expanded in ExpandCommand(commands[index]))
                yield return expanded;
        }
    }

    internal static bool TryGetQuickQuestionBranches(string rawCommand, out List<string[]> branches)
    {
        branches = [];
        if (string.IsNullOrWhiteSpace(rawCommand))
            return false;
        string trimmed = rawCommand.TrimStart();
        int space = trimmed.IndexOf(' ');
        if (space <= 0 || !trimmed.AsSpan(0, space).Equals("quickQuestion", StringComparison.OrdinalIgnoreCase))
            return false;
        // Event.DefaultCommands.QuickQuestion and Event.answerDialogue (1.6.15) split cmd[(IndexOf(' ') + 1)..]
        // on "(break)" (index 0 = #prompt#answers; indices 1..N = '\'-delimited sub-command scripts).
        string[] parts = trimmed[(space + 1)..].Split("(break)", StringSplitOptions.None);
        for (int i = 1; i < parts.Length; i++)
        {
            string[] subCommands = parts[i]
                .Split('\\', StringSplitOptions.None)
                .Where(sub => !string.IsNullOrWhiteSpace(sub))
                .ToArray();
            if (subCommands.Length > 0)
                branches.Add(subCommands);
        }
        return true;
    }

    private static IEnumerable<string> ExpandCommand(string command)
    {
        if (TryGetQuickQuestionBranches(command, out List<string[]> branches))
        {
            foreach (string[] branch in branches)
                foreach (string sub in branch)
                    foreach (string nested in ExpandCommand(sub))
                        yield return nested;
            yield break;
        }
        if (!string.IsNullOrWhiteSpace(command))
            yield return command;
    }
}

internal static class EventFragmentCollector
{
    internal static EventFragments Collect(
        string rootScript,
        string rootLocation,
        Func<string, IReadOnlyDictionary<string, string>?> loadLocationEvents,
        Func<string, string[]> parseCommands,
        Func<string, string[]> parseArguments,
        Func<string, string?> loadTranslation)
    {
        List<(string Script, string Location)> pending = [(rootScript, rootLocation)];
        List<string> scripts = [];
        List<string> missing = [];
        List<EventBranchReference> branches = [];
        Dictionary<string, string> scriptsByKey = new(StringComparer.Ordinal);
        Dictionary<EventFragmentTarget, string> resolvedTargets = new();
        HashSet<EventFragmentTarget> missingTargets = new();
        HashSet<EventFragmentTarget> visited = new();

        void WalkCommands(IEnumerable<string> commands, ref string currentLocation)
        {
            foreach (string command in commands)
            {
                if (EventCommandEnumerator.TryGetQuickQuestionBranches(command, out List<string[]> qqBranches))
                {
                    foreach (string[] branch in qqBranches)
                    {
                        string branchLocation = currentLocation;
                        WalkCommands(branch, ref branchLocation);
                    }
                    continue;
                }
                string[] args = parseArguments(command);
                if (args.Length > 1 && args[0].Equals("changeLocation", StringComparison.OrdinalIgnoreCase))
                    currentLocation = args[1];
                if (!TryGetReference(args, out string key, out bool translation))
                    continue;
                EventFragmentTarget target = EventFragmentTarget.Create(currentLocation, key, translation);
                if (!visited.Add(target))
                    continue;

                string? next = translation ? TryLoadTranslation(loadTranslation, key) : loadLocationEvents(currentLocation)?.GetValueOrDefault(key);
                if (!translation)
                    branches.Add(new EventBranchReference(key,
                        currentLocation.Equals(rootLocation, StringComparison.OrdinalIgnoreCase) ? null : currentLocation, next is not null));
                if (next is null)
                {
                    missing.Add(key);
                    missingTargets.Add(target);
                }
                else
                {
                    scriptsByKey.TryAdd(key, next);
                    resolvedTargets[target] = next;
                    pending.Add((next, currentLocation));
                }
            }
        }

        for (int index = 0; index < pending.Count; index++)
        {
            (string script, string location) = pending[index];
            scripts.Add(script);
            WalkCommands(parseCommands(script), ref location);
        }

        return new EventFragments(scripts, missing, branches, scriptsByKey, resolvedTargets, missingTargets);
    }

    private static bool TryGetReference(string[] args, out string key, out bool translation)
    {
        key = "";
        translation = false;
        if (args.Length > 1 && args[0].Equals("switchEvent", StringComparison.OrdinalIgnoreCase))
        {
            key = args[1];
            return true;
        }
        if (args.Length <= 1 || !args[0].Equals("fork", StringComparison.OrdinalIgnoreCase))
            return false;

        key = args.Length > 2 ? args[2] : args[1];
        translation = args.Length > 3 && bool.TryParse(args[3], out bool value) && value;
        return true;
    }

    private static string? TryLoadTranslation(Func<string, string?> loadTranslation, string key)
    {
        if (string.IsNullOrWhiteSpace(key) || !key.Contains(':'))
            return null;
        try
        {
            return loadTranslation(key);
        }
        catch
        {
            return null;
        }
    }
}
