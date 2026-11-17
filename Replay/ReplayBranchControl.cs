namespace StardewGallery;

internal enum ReplayForkMode
{
    FollowSave,
    ForceBranch,
    MainScript
}

internal enum ForkInterceptDecision
{
    Native,
    ForceJump,
    ForceSkip
}

internal readonly record struct ReplayForkEdge(
    string NormalizedLocation,
    string RequiredId,
    string BranchKey,
    bool IsTranslation)
{
    internal static ReplayForkEdge Create(string? location, string requiredId, string branchKey, bool isTranslation = false)
        => new(EventFragmentTarget.NormalizeLocation(location, isTranslation), requiredId, branchKey, isTranslation);
}

internal sealed record ReplayForkOption(
    ReplayForkMode Mode,
    string? TargetBranchKey,
    string? RequiredId,
    IReadOnlySet<ReplayForkEdge> ActiveEdges,
    string? TargetLocation = null,
    bool IsTranslation = false)
{
    internal static ReplayForkOption FollowSave { get; } =
        new(ReplayForkMode.FollowSave, null, null, new HashSet<ReplayForkEdge>());

    internal static ReplayForkOption MainScript { get; } =
        new(ReplayForkMode.MainScript, null, null, new HashSet<ReplayForkEdge>());
}

/// <summary>
/// Pure static analysis and sandbox decision rules for:
/// 1) temporary masking of current-event inline non-"-1" $q/$r dialogue answer IDs during replay;
/// 2) intra-event explicit 'fork &lt;requiredId&gt; &lt;branchKey&gt;' route discovery and override decisions.
/// </summary>
internal static class ReplayBranchControl
{
    private sealed record WorkItem(
        string Script,
        string Location,
        bool IsRoot,
        IReadOnlyList<ReplayForkEdge> Path);

    internal static HashSet<string> CollectInlineQuestionIds(
        ResolvedEvent entry,
        Func<string, string[]> parseCommands,
        Func<string, string[]> splitArguments,
        Func<string, string?>? loadTranslation = null)
    {
        HashSet<string> ids = new(StringComparer.Ordinal);
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
                if (args.Length < 2)
                    continue;
                foreach (string dialogue in ParallelBranchAnalyzer.DialogueTextsFromCommand(args, loadTranslation))
                    CollectFromDialogue(dialogue, ids);
            }
        }

        return ids;
    }

    internal static void CollectFromDialogue(string rawDialogue, ISet<string> destination)
    {
        if (string.IsNullOrWhiteSpace(rawDialogue))
            return;
        foreach (string variant in rawDialogue.Split("||", StringSplitOptions.None))
        {
            string[] segments = variant.Split('#');
            for (int i = 0; i < segments.Length; i++)
            {
                string seg = segments[i].TrimStart();
                if (!seg.StartsWith('$'))
                    continue;
                string[] tokens = seg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length < 2)
                    continue;
                if (tokens[0] == "$q")
                {
                    string[] qIds = tokens[1].Split('/', StringSplitOptions.RemoveEmptyEntries);
                    // Dialogue.parseDialogueString only skips a $q prompt when qIds[0] != "-1".
                    if (qIds.Length > 0 && qIds[0] != "-1")
                    {
                        foreach (string id in qIds)
                            if (id != "-1")
                                destination.Add(id);
                    }
                }
                else if (tokens[0] == "$r")
                {
                    if (tokens[1] != "-1")
                        destination.Add(tokens[1]);
                    i++; // Skip response text segment.
                }
            }
        }
    }

    /// <summary>
    /// Removes only the replayed event's own inline question IDs from the live answer set,
    /// leaving prior events' answer IDs intact so cross-event $p and fork checks remain faithful.
    /// </summary>
    internal static int MaskCurrentEventAnswers(ISet<string> liveAnswers, IReadOnlySet<string> currentEventIds)
    {
        int removed = 0;
        foreach (string id in currentEventIds)
            if (id != "-1" && liveAnswers.Remove(id))
                removed++;
        return removed;
    }

    internal static IReadOnlyList<ReplayForkOption> ReadForkOptions(
        ResolvedEvent entry,
        Func<string, string[]> parseCommands,
        Func<string, string[]> splitArguments)
    {
        List<ReplayForkOption> branches = [];
        HashSet<ReplayForkEdge> seenEdges = new();
        HashSet<(string Script, string NormalizedLocation)> settled = new();
        Queue<WorkItem> zeroCost = new();
        Queue<WorkItem> nextDepth = new();

        zeroCost.Enqueue(new WorkItem(entry.ResolvedScript, entry.LocationName, IsRoot: true, []));

        if (entry.Fragments.ResolvedTargets is null && entry.Fragments.ScriptsByKey is null)
        {
            foreach (string fragment in entry.Fragments.Scripts)
                zeroCost.Enqueue(new WorkItem(fragment, entry.LocationName, IsRoot: false, []));
        }

        void ScanCommands(IEnumerable<string> commands, ref string currentLocation, IReadOnlyList<ReplayForkEdge> activeForkPath)
        {
            foreach (string command in commands)
            {
                if (EventCommandEnumerator.TryGetQuickQuestionBranches(command, out List<string[]> qqBranches))
                {
                    foreach (string[] branch in qqBranches)
                    {
                        string branchLocation = currentLocation;
                        ScanCommands(branch, ref branchLocation, activeForkPath);
                    }
                    continue;
                }

                string[] args = splitArguments(command);
                if (args.Length < 2)
                    continue;

                if (args[0].Equals("changeLocation", StringComparison.OrdinalIgnoreCase))
                {
                    currentLocation = args[1];
                    continue;
                }

                if (args[0].Equals("switchEvent", StringComparison.OrdinalIgnoreCase))
                {
                    EventFragmentTarget switchTarget = EventFragmentTarget.Create(currentLocation, args[1], isTranslation: false);
                    if (!IsTargetMissing(entry.Fragments, switchTarget)
                        && TryResolveTargetScript(entry.Fragments, switchTarget) is string switchedScript)
                    {
                        zeroCost.Enqueue(new WorkItem(switchedScript, currentLocation, IsRoot: false, activeForkPath));
                    }
                    continue;
                }

                if (!args[0].Equals("fork", StringComparison.OrdinalIgnoreCase))
                    continue;

                // 1-arg 'fork <branchKey>' uses Event.specialEventVariable1 from an in-event question, not a save flag.
                if (args.Length < 3 || string.IsNullOrWhiteSpace(args[1]) || string.IsNullOrWhiteSpace(args[2]))
                {
                    EventFragmentTarget oneArgTarget = EventFragmentTarget.Create(currentLocation, args[1], isTranslation: false);
                    if (!IsTargetMissing(entry.Fragments, oneArgTarget)
                        && TryResolveTargetScript(entry.Fragments, oneArgTarget) is string oneArgScript)
                    {
                        zeroCost.Enqueue(new WorkItem(oneArgScript, currentLocation, IsRoot: false, activeForkPath));
                    }
                    continue;
                }

                string requiredId = args[1];
                string branchKey = args[2];
                bool isTranslation = args.Length > 3 && bool.TryParse(args[3], out bool transVal) && transVal;
                EventFragmentTarget target = EventFragmentTarget.Create(currentLocation, branchKey, isTranslation);
                if (IsTargetMissing(entry.Fragments, target))
                    continue;

                ReplayForkEdge edge = ReplayForkEdge.Create(currentLocation, requiredId, branchKey, isTranslation);
                List<ReplayForkEdge> nextPath = [.. activeForkPath, edge];
                if (seenEdges.Add(edge))
                {
                    string? targetLocation = isTranslation || currentLocation.Equals(entry.LocationName, StringComparison.OrdinalIgnoreCase)
                        ? null
                        : currentLocation;
                    branches.Add(new ReplayForkOption(
                        ReplayForkMode.ForceBranch,
                        branchKey,
                        requiredId,
                        nextPath.ToHashSet(),
                        targetLocation,
                        isTranslation));
                }

                if (TryResolveTargetScript(entry.Fragments, target) is string branchScript)
                    nextDepth.Enqueue(new WorkItem(branchScript, currentLocation, IsRoot: false, nextPath));
            }
        }

        // 0-1 BFS: exhaust all 0-fork transitions (switchEvent / 1-arg fork) at the current fork depth before
        // advancing to forkDepth + 1, ensuring shared switchEvent tails never inherit an unrelated branch's fork path.
        while (zeroCost.Count > 0 || nextDepth.Count > 0)
        {
            if (zeroCost.Count == 0)
            {
                while (nextDepth.TryDequeue(out WorkItem? promoted))
                    zeroCost.Enqueue(promoted);
            }

            WorkItem item = zeroCost.Dequeue();
            string normStartLocation = EventFragmentTarget.NormalizeLocation(item.Location);
            if (!settled.Add((item.Script, normStartLocation)))
                continue;

            string[] parsed = parseCommands(item.Script);
            IEnumerable<string> body = item.IsRoot && parsed.Length >= 3 ? parsed.Skip(3) : parsed;
            string currentLocation = item.Location;
            ScanCommands(body, ref currentLocation, item.Path);
        }

        if (branches.Count == 0)
            return [];

        List<ReplayForkOption> options = [ReplayForkOption.FollowSave, .. branches, ReplayForkOption.MainScript];
        return options;
    }

    internal static ForkInterceptDecision DecideFork(
        ReplayForkOption? selected,
        string? currentLocation,
        string? requiredId,
        string? newKey,
        bool isTranslation = false)
    {
        if (selected is null || selected.Mode == ReplayForkMode.FollowSave)
            return ForkInterceptDecision.Native;

        // 1-arg 'fork <newKey>' (where Event.DefaultCommands.Fork shifts requiredId -> newKey and sets requiredId = null)
        // is driven by in-event interactive questions via specialEventVariable1, so always keep native behavior.
        if (newKey is null || requiredId is null)
            return ForkInterceptDecision.Native;

        if (selected.Mode != ReplayForkMode.ForceBranch)
            return ForkInterceptDecision.ForceSkip;

        ReplayForkEdge candidate = ReplayForkEdge.Create(currentLocation, requiredId, newKey, isTranslation);
        return selected.ActiveEdges.Contains(candidate)
            ? ForkInterceptDecision.ForceJump
            : ForkInterceptDecision.ForceSkip;
    }

    private static bool IsTargetMissing(EventFragments fragments, EventFragmentTarget target)
    {
        if (fragments.MissingTargets is not null)
            return fragments.MissingTargets.Contains(target);
        return fragments.MissingKeys.Contains(target.Key, StringComparer.Ordinal);
    }

    private static string? TryResolveTargetScript(EventFragments fragments, EventFragmentTarget target)
    {
        if (fragments.ResolvedTargets is not null && fragments.ResolvedTargets.TryGetValue(target, out string? resolved))
            return resolved;
        if (fragments.ScriptsByKey is not null && fragments.ScriptsByKey.TryGetValue(target.Key, out string? byKey))
            return byKey;
        return null;
    }
}
