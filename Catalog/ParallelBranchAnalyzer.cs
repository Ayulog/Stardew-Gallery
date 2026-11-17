namespace StardewGallery;

internal readonly record struct StoryNodeProgress(int CollectedNodes, int TotalNodes)
{
    internal bool IsComplete => TotalNodes > 0 && CollectedNodes >= TotalNodes;
    internal bool IsFullyCollected => IsComplete;
}

internal static class CharacterStoryProgress
{
    internal static StoryNodeProgress Measure(
        IReadOnlyList<GalleryEvent> heartEvents,
        GalleryCatalog catalog,
        IEnumerable<string>? eventsSeen)
        => Measure(heartEvents, catalog.ParallelGroups, eventsSeen);

    internal static StoryNodeProgress Measure(
        IReadOnlyList<GalleryEvent> heartEvents,
        IReadOnlyDictionary<EventIdentity, string> parallelGroups,
        IEnumerable<string>? eventsSeen)
    {
        Func<string, bool>? isSeen = eventsSeen switch
        {
            null => null,
            ISet<string> set => set.Contains,
            IReadOnlySet<string> readOnlySet => readOnlySet.Contains,
            ICollection<string> collection => collection.Contains,
            _ => eventsSeen.ToHashSet(StringComparer.Ordinal).Contains
        };
        int collected = 0;
        int total = 0;
        HashSet<string> visitedGroups = new(StringComparer.Ordinal);
        Dictionary<string, List<GalleryEvent>> grouped = new(StringComparer.Ordinal);

        foreach (GalleryEvent entry in heartEvents)
        {
            if (entry.Kind != StoryKind.Heart)
                continue;
            if (parallelGroups.TryGetValue(entry.Resolved.Identity, out string? groupId))
            {
                if (!grouped.TryGetValue(groupId, out List<GalleryEvent>? members))
                    grouped[groupId] = members = [];
                members.Add(entry);
            }
        }

        foreach (GalleryEvent entry in heartEvents)
        {
            if (entry.Kind != StoryKind.Heart)
                continue;
            if (parallelGroups.TryGetValue(entry.Resolved.Identity, out string? groupId)
                && grouped.TryGetValue(groupId, out List<GalleryEvent>? members)
                && members.Count > 1)
            {
                if (!visitedGroups.Add(groupId))
                    continue;
                total++;
                if (isSeen is not null && members.Any(member => isSeen(member.EventId)))
                    collected++;
            }
            else
            {
                total++;
                if (isSeen?.Invoke(entry.EventId) == true)
                    collected++;
            }
        }

        return new StoryNodeProgress(collected, total);
    }
}

/// <summary>
/// Statically identifies mutually exclusive parallel heart-event groups (If-routes) for the same character(s).
/// Groups events only when a permanent single-playthrough contradiction is proven from declared preconditions
/// and loaded question prompts—never by guessing from heart level or location alone.
/// </summary>
internal static class ParallelBranchAnalyzer
{
    private enum RomanceStage
    {
        None = 0,
        Dating = 1,
        MarriedOrRoommate = 2
    }

    private sealed record EventConditionFacts(
        GalleryEvent Entry,
        string OwnerKey,
        int MaxOwnerPoints,
        RomanceStage RomanceStage,
        HashSet<string> SingleRequiredEvents,
        HashSet<string> ForbiddenEvents,
        List<HashSet<string>> PositiveAnswerDisjunctions,
        HashSet<string> ForbiddenAnswers);

    internal static IReadOnlyDictionary<EventIdentity, string> Analyze(
        IReadOnlyList<GalleryEvent> heartEvents,
        IReadOnlyList<ResolvedEvent> allEvents,
        ConditionParser conditionParser,
        Func<string, string[]> parseCommands,
        Func<string, string[]> splitArguments,
        Func<string, string?>? loadTranslation = null)
    {
        if (heartEvents.Count < 2)
            return new Dictionary<EventIdentity, string>();

        Dictionary<string, int> promptByAnswer = ReadExclusiveQuestionPrompts(
            allEvents, parseCommands, splitArguments, loadTranslation);

        Dictionary<EventIdentity, EventConditionFacts> factsByIdentity = new();
        Dictionary<string, List<EventConditionFacts>> byEventId = new(StringComparer.Ordinal);
        foreach (GalleryEvent entry in heartEvents)
        {
            if (entry.Kind != StoryKind.Heart || entry.Ownership.Owners.Count == 0)
                continue;
            EventConditionFacts facts = ReadFacts(entry, conditionParser);
            factsByIdentity[entry.Resolved.Identity] = facts;
            if (!byEventId.TryGetValue(entry.EventId, out List<EventConditionFacts>? list))
                byEventId[entry.EventId] = list = [];
            list.Add(facts);
        }

        Dictionary<string, int> totalCountByEventId = new(StringComparer.Ordinal);
        foreach (ResolvedEvent resolved in allEvents)
            totalCountByEventId[resolved.EventId] = totalCountByEventId.GetValueOrDefault(resolved.EventId) + 1;

        (Dictionary<string, int?> maxHeartTierByEventId, Dictionary<string, int?> minHeartTierByEventId) =
            ReadEventTiers(allEvents, conditionParser);
        (Dictionary<string, RomanceStage> maxRomanceByEventId, Dictionary<string, RomanceStage> minRomanceByEventId) =
            ReadEventRomanceStages(allEvents, conditionParser);

        // Propagate guaranteed prerequisite progression stages along unambiguous SingleRequiredEvents so
        // continuations (e.g. '1402/e 1401' or '56/e 54') retain their upstream heart/romance tier.
        bool stageUpdated;
        do
        {
            stageUpdated = false;
            foreach (EventIdentity id in factsByIdentity.Keys.ToArray())
            {
                EventConditionFacts current = factsByIdentity[id];
                int effectivePoints = current.MaxOwnerPoints;
                RomanceStage effectiveRomance = current.RomanceStage;

                foreach (string prereqId in current.SingleRequiredEvents)
                {
                    if (prereqId == current.Entry.EventId)
                        continue;
                    if (totalCountByEventId.GetValueOrDefault(prereqId) == 1
                        && byEventId.TryGetValue(prereqId, out List<EventConditionFacts>? heartParents)
                        && heartParents.Count == 1
                        && heartParents[0].OwnerKey == current.OwnerKey)
                    {
                        effectivePoints = Math.Max(effectivePoints, heartParents[0].MaxOwnerPoints);
                        if (heartParents[0].RomanceStage > effectiveRomance)
                            effectiveRomance = heartParents[0].RomanceStage;
                    }
                    if (minHeartTierByEventId.TryGetValue(prereqId, out int? guaranteedPoints) && guaranteedPoints is int reqPoints)
                        effectivePoints = Math.Max(effectivePoints, reqPoints);
                    if (minRomanceByEventId.TryGetValue(prereqId, out RomanceStage guaranteedRomance) && guaranteedRomance > effectiveRomance)
                        effectiveRomance = guaranteedRomance;
                }

                if (effectivePoints != current.MaxOwnerPoints || effectiveRomance != current.RomanceStage)
                {
                    EventConditionFacts updated = current with
                    {
                        MaxOwnerPoints = effectivePoints,
                        RomanceStage = effectiveRomance
                    };
                    factsByIdentity[id] = updated;
                    List<EventConditionFacts> bucketList = byEventId[current.Entry.EventId];
                    int idx = bucketList.FindIndex(item => item.Entry.Resolved.Identity == id);
                    if (idx >= 0)
                        bucketList[idx] = updated;
                    stageUpdated = true;
                }
            }
        } while (stageUpdated);

        // Seed direct pairwise permanent exclusions across heart events sharing the exact same (OwnerKey, MaxOwnerPoints, RomanceStage).
        HashSet<(EventIdentity Left, EventIdentity Right)> exclusivePairs = [];
        EventConditionFacts[] allFacts = factsByIdentity.Values
            .OrderBy(item => item.Entry.EventId, StringComparer.Ordinal)
            .ThenBy(item => item.Entry.AssetName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        for (int i = 0; i < allFacts.Length; i++)
        {
            for (int j = i + 1; j < allFacts.Length; j++)
            {
                EventConditionFacts a = allFacts[i];
                EventConditionFacts b = allFacts[j];
                if (!SameStage(a, b) || a.Entry.EventId == b.Entry.EventId)
                    continue;
                if (DirectlyExclusive(a, b, promptByAnswer, maxHeartTierByEventId, maxRomanceByEventId))
                    AddPair(exclusivePairs, a.Entry.Resolved.Identity, b.Entry.Resolved.Identity);
            }
        }

        // Fixed-point propagation for same-stage continuations of mutually exclusive prerequisites:
        // if A unconditionally requires X and B unconditionally requires Y, and (X, Y) are exclusive, so are (A, B).
        bool changed;
        do
        {
            changed = false;
            for (int i = 0; i < allFacts.Length; i++)
            {
                for (int j = i + 1; j < allFacts.Length; j++)
                {
                    EventConditionFacts a = allFacts[i];
                    EventConditionFacts b = allFacts[j];
                    if (!SameStage(a, b) || a.Entry.EventId == b.Entry.EventId
                        || HasPair(exclusivePairs, a.Entry.Resolved.Identity, b.Entry.Resolved.Identity))
                        continue;
                    if (TransitivelyExclusive(a, b, byEventId, totalCountByEventId, exclusivePairs))
                    {
                        AddPair(exclusivePairs, a.Entry.Resolved.Identity, b.Entry.Resolved.Identity);
                        changed = true;
                    }
                }
            }
        } while (changed);

        // ExclusiveDepth accumulates +1 ONLY along same-owner prerequisites that themselves participate in exclusivePairs,
        // separating upstream choice pairs from same-tier downstream continuations without splitting siblings where only one
        // side declares an earlier linear heart event (e.g. 'e 50' vs '!e 50', or 'e 73/q 77' vs 'q 78').
        HashSet<EventIdentity> exclusiveMembers = [];
        foreach ((EventIdentity left, EventIdentity right) in exclusivePairs)
        {
            exclusiveMembers.Add(left);
            exclusiveMembers.Add(right);
        }

        Dictionary<EventIdentity, int> exclusiveDepths = new();
        int GetExclusiveDepth(EventConditionFacts item, HashSet<EventIdentity> path)
        {
            if (exclusiveDepths.TryGetValue(item.Entry.Resolved.Identity, out int cached))
                return cached;
            if (!path.Add(item.Entry.Resolved.Identity))
                return 0;
            int depth = 0;
            foreach (string prereqId in item.SingleRequiredEvents)
            {
                if (totalCountByEventId.GetValueOrDefault(prereqId) != 1
                    || !byEventId.TryGetValue(prereqId, out List<EventConditionFacts>? parents)
                    || parents.Count != 1)
                    continue;
                EventConditionFacts parent = parents[0];
                if (parent.OwnerKey != item.OwnerKey)
                    continue;
                int step = exclusiveMembers.Contains(parent.Entry.Resolved.Identity) ? 1 : 0;
                depth = Math.Max(depth, step + GetExclusiveDepth(parent, path));
            }
            path.Remove(item.Entry.Resolved.Identity);
            exclusiveDepths[item.Entry.Resolved.Identity] = depth;
            return depth;
        }
        foreach (EventConditionFacts item in allFacts)
            GetExclusiveDepth(item, []);

        // Partition each (OwnerKey, MaxOwnerPoints, RomanceStage, ExclusiveDepth) bucket into disjoint cliques of size >= 2.
        Dictionary<EventIdentity, string> groups = new();
        foreach (var bucket in allFacts.GroupBy(item => (
            item.OwnerKey,
            item.MaxOwnerPoints,
            item.RomanceStage,
            Depth: exclusiveDepths[item.Entry.Resolved.Identity])))
        {
            EventConditionFacts[] candidates = bucket.ToArray();
            if (candidates.Length < 2)
                continue;
            HashSet<EventIdentity> assigned = [];
            for (int i = 0; i < candidates.Length; i++)
            {
                EventIdentity seed = candidates[i].Entry.Resolved.Identity;
                if (assigned.Contains(seed))
                    continue;
                List<EventConditionFacts> clique = [candidates[i]];
                for (int j = i + 1; j < candidates.Length; j++)
                {
                    EventIdentity candidate = candidates[j].Entry.Resolved.Identity;
                    if (assigned.Contains(candidate))
                        continue;
                    if (clique.All(member => HasPair(exclusivePairs, member.Entry.Resolved.Identity, candidate)))
                        clique.Add(candidates[j]);
                }
                if (clique.Count < 2)
                    continue;
                string groupId = $"{bucket.Key.OwnerKey}#{bucket.Key.MaxOwnerPoints}#{(int)bucket.Key.RomanceStage}#{bucket.Key.Depth}:"
                    + string.Join("|", clique.Select(member => member.Entry.Identity).OrderBy(id => id, StringComparer.Ordinal));
                foreach (EventConditionFacts member in clique)
                {
                    assigned.Add(member.Entry.Resolved.Identity);
                    groups[member.Entry.Resolved.Identity] = groupId;
                }
            }
        }

        return groups;
    }

    private static bool SameStage(EventConditionFacts a, EventConditionFacts b)
        => a.OwnerKey == b.OwnerKey
            && a.MaxOwnerPoints == b.MaxOwnerPoints
            && a.RomanceStage == b.RomanceStage;

    private static bool DirectlyExclusive(
        EventConditionFacts a,
        EventConditionFacts b,
        IReadOnlyDictionary<string, int> promptByAnswer,
        IReadOnlyDictionary<string, int?> maxHeartTierByEventId,
        IReadOnlyDictionary<string, RomanceStage> maxRomanceByEventId)
    {
        // 1. Bidirectional mutual exclusion by each other's event ID (!SawEvent(B) && !SawEvent(A)).
        if (a.ForbiddenEvents.Contains(b.Entry.EventId) && b.ForbiddenEvents.Contains(a.Entry.EventId))
            return true;

        // 2. Explicit positive vs negative SawEvent on a third event X (e X vs !e X / k X / !PLAYER_HAS_SEEN_EVENT X),
        // provided X is not a higher-progression event than the negative branch (which would allow B -> X -> A).
        if (HasOpposingSawEvent(a, b, maxHeartTierByEventId, maxRomanceByEventId)
            || HasOpposingSawEvent(b, a, maxHeartTierByEventId, maxRomanceByEventId))
            return true;

        // 3. Explicit positive vs negative requirement on the same dialogue answer ID.
        if (HasOpposingDialogueAnswer(a, b) || HasOpposingDialogueAnswer(b, a))
            return true;

        // 4. Disjoint answer sets drawn from the exact same single-choice $q prompt.
        foreach (HashSet<string> setA in a.PositiveAnswerDisjunctions)
        {
            if (!TryGetSharedPrompt(setA, promptByAnswer, out int promptA))
                continue;
            foreach (HashSet<string> setB in b.PositiveAnswerDisjunctions)
            {
                if (TryGetSharedPrompt(setB, promptByAnswer, out int promptB)
                    && promptA == promptB
                    && !setA.Overlaps(setB))
                    return true;
            }
        }

        return false;
    }

    private static bool HasOpposingSawEvent(
        EventConditionFacts positive,
        EventConditionFacts negative,
        IReadOnlyDictionary<string, int?> maxHeartTierByEventId,
        IReadOnlyDictionary<string, RomanceStage> maxRomanceByEventId)
    {
        if (!SameStage(positive, negative))
            return false;
        foreach (string eventId in positive.SingleRequiredEvents)
        {
            if (eventId == positive.Entry.EventId || eventId == negative.Entry.EventId)
                continue;
            if (!negative.ForbiddenEvents.Contains(eventId))
                continue;
            // Reject sequential early-guard patterns such as 4H (!e 14H) vs 14H_Part2 (e 14H),
            // where event X requires higher friendship or romance status than the negative event.
            if (maxRomanceByEventId.GetValueOrDefault(eventId) > negative.RomanceStage)
                continue;
            if (maxHeartTierByEventId.TryGetValue(eventId, out int? xPoints)
                && xPoints is int requiredByX
                && requiredByX > negative.MaxOwnerPoints)
                continue;
            return true;
        }
        return false;
    }

    private static bool HasOpposingDialogueAnswer(EventConditionFacts positive, EventConditionFacts negative)
        => positive.PositiveAnswerDisjunctions.Any(disjunction =>
            disjunction.Count > 0 && disjunction.All(negative.ForbiddenAnswers.Contains));

    private static bool TryGetSharedPrompt(
        HashSet<string> answers,
        IReadOnlyDictionary<string, int> promptByAnswer,
        out int promptId)
    {
        promptId = -1;
        if (answers.Count == 0)
            return false;
        foreach (string answer in answers)
        {
            if (!promptByAnswer.TryGetValue(answer, out int candidate) || candidate < 0)
                return false;
            if (promptId < 0)
                promptId = candidate;
            else if (promptId != candidate)
                return false;
        }
        return promptId >= 0;
    }

    private static bool TransitivelyExclusive(
        EventConditionFacts a,
        EventConditionFacts b,
        IReadOnlyDictionary<string, List<EventConditionFacts>> byEventId,
        IReadOnlyDictionary<string, int> totalCountByEventId,
        HashSet<(EventIdentity Left, EventIdentity Right)> exclusivePairs)
    {
        if (!SameStage(a, b))
            return false;
        foreach (string reqA in a.SingleRequiredEvents)
        {
            if (reqA == b.Entry.EventId
                || totalCountByEventId.GetValueOrDefault(reqA) != 1
                || !byEventId.TryGetValue(reqA, out List<EventConditionFacts>? parentsA)
                || parentsA.Count != 1)
                continue;
            foreach (string reqB in b.SingleRequiredEvents)
            {
                if (reqB == a.Entry.EventId || reqA == reqB
                    || totalCountByEventId.GetValueOrDefault(reqB) != 1
                    || !byEventId.TryGetValue(reqB, out List<EventConditionFacts>? parentsB)
                    || parentsB.Count != 1)
                    continue;
                if (HasPair(exclusivePairs, parentsA[0].Entry.Resolved.Identity, parentsB[0].Entry.Resolved.Identity))
                    return true;
            }
        }
        return false;
    }

    private static EventConditionFacts ReadFacts(GalleryEvent entry, ConditionParser parser)
    {
        string ownerKey = string.Join("&", entry.Ownership.Owners.Select(owner => owner.Name).OrderBy(name => name, StringComparer.Ordinal));
        int maxPoints = entry.Ownership.Owners.Max(owner => owner.FriendshipPoints ?? 0);
        RomanceStage romanceStage = RomanceStage.None;
        HashSet<string> singleRequiredEvents = new(StringComparer.Ordinal);
        HashSet<string> forbiddenEvents = new(StringComparer.Ordinal);
        List<HashSet<string>> positiveAnswers = [];
        HashSet<string> forbiddenAnswers = new(StringComparer.Ordinal);

        foreach (ConditionExpression expr in parser.ParseRawKey(entry.EventKey).Conditions)
        {
            RomanceStage exprRomance = ReadConditionRomanceStage(expr);
            if (exprRomance > romanceStage)
                romanceStage = exprRomance;

            switch (expr)
            {
                case SawEventCondition { Negated: false } saw when saw.EventIds.Distinct(StringComparer.Ordinal).Count() == 1:
                    singleRequiredEvents.Add(saw.EventIds[0]);
                    break;
                case SawEventCondition { Negated: true } notSaw:
                    // Negated SawEvent (k / !e / NotSawEvent) requires that NONE of the listed events has been seen.
                    foreach (string id in notSaw.EventIds)
                        forbiddenEvents.Add(id);
                    break;
                case ChoseDialogueAnswersCondition { Negated: false } chose:
                    // Legacy 'q id1 id2' requires ALL listed answer IDs, so each one is individually required.
                    foreach (string id in chose.AnswerIds.Where(id => !string.IsNullOrWhiteSpace(id) && id != "-1"))
                        positiveAnswers.Add(new HashSet<string>(StringComparer.Ordinal) { id });
                    break;
                case ChoseDialogueAnswersCondition { Negated: true } notChose when notChose.AnswerIds.Distinct(StringComparer.Ordinal).Count() == 1:
                    forbiddenAnswers.Add(notChose.AnswerIds[0]);
                    break;
                case NativeQueryCondition query when SafeGameQuery.TryParse(query.Query, out var clauses):
                    ReadQueryFacts(query.Negated, clauses, singleRequiredEvents, forbiddenEvents, positiveAnswers, forbiddenAnswers);
                    break;
            }
        }

        return new EventConditionFacts(entry, ownerKey, maxPoints, romanceStage, singleRequiredEvents, forbiddenEvents, positiveAnswers, forbiddenAnswers);
    }

    private static RomanceStage ReadConditionRomanceStage(ConditionExpression expr)
    {
        switch (expr)
        {
            case DatingCondition { Negated: false }:
                return RomanceStage.Dating;
            case SpouseCondition { Negated: false } or RoommateCondition { Negated: false } or SpouseBedCondition { Negated: false }:
                return RomanceStage.MarriedOrRoommate;
            case NativeQueryCondition query when SafeGameQuery.TryParse(query.Query, out var clauses):
            {
                if (query.Negated && clauses.Count != 1)
                    return RomanceStage.None;
                RomanceStage stage = RomanceStage.None;
                foreach (SafeQueryClause clause in clauses)
                {
                    bool negated = clause.Negated ^ query.Negated;
                    string[] a = clause.Arguments;
                    if (negated || clause.Name != "PLAYER_NPC_RELATIONSHIP" || a.Length < 3 || !IsLocalPlayerSelector(a[0]))
                        continue;
                    string[] rels = a.Skip(2).ToArray();
                    if (!rels.All(IsRomanceRelationship))
                        continue;
                    RomanceStage clauseStage = rels.Any(rel => rel.Equals("dating", StringComparison.OrdinalIgnoreCase))
                        ? RomanceStage.Dating
                        : RomanceStage.MarriedOrRoommate;
                    if (clauseStage > stage)
                        stage = clauseStage;
                }
                return stage;
            }
            default:
                return RomanceStage.None;
        }
    }

    private static bool IsRomanceRelationship(string value)
        => value.Equals("dating", StringComparison.OrdinalIgnoreCase)
            || value.Equals("engaged", StringComparison.OrdinalIgnoreCase)
            || value.Equals("married", StringComparison.OrdinalIgnoreCase)
            || value.Equals("roommate", StringComparison.OrdinalIgnoreCase);

    private static void ReadQueryFacts(
        bool outerNegated,
        IReadOnlyList<SafeQueryClause> clauses,
        HashSet<string> singleRequiredEvents,
        HashSet<string> forbiddenEvents,
        List<HashSet<string>> positiveAnswers,
        HashSet<string> forbiddenAnswers)
    {
        if (outerNegated && clauses.Count != 1)
            return;
        foreach (SafeQueryClause clause in clauses)
        {
            bool negated = clause.Negated ^ outerNegated;
            string[] a = clause.Arguments;
            if (a.Length < 2 || !IsLocalPlayerSelector(a[0]))
                continue;
            if (clause.Name == "PLAYER_HAS_SEEN_EVENT")
            {
                string[] ids = a.Skip(1).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToArray();
                if (!negated && ids.Length == 1)
                    singleRequiredEvents.Add(ids[0]);
                else if (negated)
                    foreach (string id in ids)
                        forbiddenEvents.Add(id);
            }
            else if (clause.Name == "PLAYER_HAS_DIALOGUE_ANSWER")
            {
                string[] ids = a.Skip(1).Where(id => !string.IsNullOrWhiteSpace(id) && id != "-1").Distinct(StringComparer.Ordinal).ToArray();
                if (!negated && ids.Length > 0)
                    positiveAnswers.Add(ids.ToHashSet(StringComparer.Ordinal));
                else if (negated)
                    foreach (string id in ids)
                        forbiddenAnswers.Add(id);
            }
        }
    }

    /// <summary>
    /// Maps each single-choice dialogue answer ID to the unique $q prompt index that offers it.
    /// Any answer ID offered by multiple distinct $q prompts or toggled by a 'questionAnswered' command is marked -1 (ambiguous).
    /// </summary>
    private static Dictionary<string, int> ReadExclusiveQuestionPrompts(
        IReadOnlyList<ResolvedEvent> allEvents,
        Func<string, string[]> parseCommands,
        Func<string, string[]> splitArguments,
        Func<string, string?>? loadTranslation)
    {
        Dictionary<string, int> answerToPrompt = new(StringComparer.Ordinal);
        HashSet<string> visitedScripts = new(StringComparer.Ordinal);
        int nextPromptId = 0;

        void RecordGroup(HashSet<string> group)
        {
            if (group.Count < 2)
                return;
            int promptId = nextPromptId++;
            foreach (string id in group)
            {
                if (!answerToPrompt.TryAdd(id, promptId))
                    answerToPrompt[id] = -1;
            }
        }

        void InspectDialogue(string rawDialogue)
        {
            if (string.IsNullOrWhiteSpace(rawDialogue))
                return;
            foreach (string variant in rawDialogue.Split("||", StringSplitOptions.None))
            {
                string[] segments = variant.Split('#');
                HashSet<string>? activeGroup = null;
                for (int i = 0; i < segments.Length; i++)
                {
                    string seg = segments[i].TrimStart();
                    if (!seg.StartsWith('$'))
                        continue;
                    string[] tokens = seg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (tokens.Length == 0)
                        continue;
                    if (tokens[0] == "$q")
                    {
                        if (activeGroup is not null)
                            RecordGroup(activeGroup);
                        activeGroup = new HashSet<string>(StringComparer.Ordinal);
                        if (tokens.Length >= 2)
                        {
                            foreach (string qId in tokens[1].Split('/', StringSplitOptions.RemoveEmptyEntries))
                                if (qId != "-1")
                                    activeGroup.Add(qId);
                        }
                    }
                    else if (tokens[0] == "$r" && tokens.Length >= 2)
                    {
                        string rId = tokens[1];
                        if (rId != "-1")
                        {
                            activeGroup ??= new HashSet<string>(StringComparer.Ordinal);
                            activeGroup.Add(rId);
                        }
                        i++; // Skip response text segment.
                    }
                }
                if (activeGroup is not null)
                    RecordGroup(activeGroup);
            }
        }

        foreach (ResolvedEvent resolved in allEvents)
        {
            IEnumerable<string> scripts = new[] { resolved.ResolvedScript }.Concat(resolved.Fragments.Scripts);
            foreach (string script in scripts)
            {
                bool isRoot = ReferenceEquals(script, resolved.ResolvedScript) || script == resolved.ResolvedScript;
                if (!visitedScripts.Add(script))
                    continue;
                foreach (string command in EventCommandEnumerator.EnumerateCommands(script, skipHeader: isRoot, parseCommands))
                {
                    string[] args = splitArguments(command);
                    if (args.Length < 2)
                        continue;
                    if (args[0].Equals("questionAnswered", StringComparison.OrdinalIgnoreCase))
                    {
                        answerToPrompt[args[1]] = -1;
                        continue;
                    }
                    foreach (string text in DialogueTextsFromCommand(args, loadTranslation))
                        InspectDialogue(text);
                }
            }
        }

        return answerToPrompt;
    }

    internal static IEnumerable<string> DialogueTextsFromCommand(string[] args, Func<string, string?>? loadTranslation)
    {
        if (args.Length >= 3 && args[0].Equals("speak", StringComparison.OrdinalIgnoreCase))
        {
            yield return ResolveIfTranslation(args[2], loadTranslation, out bool unresolved) is string text && !unresolved ? text : "";
        }
        else if (args.Length >= 3 && args[0].Equals("splitSpeak", StringComparison.OrdinalIgnoreCase))
        {
            foreach (string part in args[2].Split('~', StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmed = part.Trim();
                if (ResolveIfTranslation(trimmed, loadTranslation, out bool unresolved) is string text && !unresolved)
                    yield return text;
            }
        }
        else if (args.Length >= 2 && args[0].Equals("message", StringComparison.OrdinalIgnoreCase))
        {
            if (ResolveIfTranslation(args[1], loadTranslation, out bool unresolved) is string text && !unresolved)
                yield return text;
        }
    }

    internal static bool HasTranslationKeySyntax(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Contains(' ') || raw.Contains('#'))
            return false;
        int colon = raw.IndexOf(':');
        return colon > 0 && colon < raw.Length - 1 && raw[colon + 1] is not ('/' or '\\');
    }

    internal static string? ResolveIfTranslation(string raw, Func<string, string?>? loadTranslation, out bool unresolvedKey)
    {
        unresolvedKey = false;
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        bool validKeySyntax = HasTranslationKeySyntax(raw);
        int colon = validKeySyntax ? raw.IndexOf(':') : -1;
        bool looksLikeAssetKey = validKeySyntax && raw.AsSpan(0, colon).IndexOfAny('/', '\\') >= 0;
        if (validKeySyntax && loadTranslation is not null)
        {
            try
            {
                string? loaded = loadTranslation(raw);
                if (loaded is not null)
                    return loaded;
            }
            catch
            {
                // Missing or non-dictionary assets in LocalizedContentManager throw ContentLoadException;
                // fall through to looksLikeAssetKey classification below.
            }
        }
        if (looksLikeAssetKey)
        {
            unresolvedKey = true;
            return null;
        }
        return raw;
    }

    private static (Dictionary<string, int?> MaxByEventId, Dictionary<string, int?> MinByEventId) ReadEventTiers(
        IReadOnlyList<ResolvedEvent> allEvents,
        ConditionParser parser)
    {
        Dictionary<string, int?> maxTiers = new(StringComparer.Ordinal);
        Dictionary<string, int?> minTiers = new(StringComparer.Ordinal);
        foreach (ResolvedEvent entry in allEvents)
        {
            int? points = ConditionRelationshipEvidence.Read(parser.ParseRawKey(entry.RawEventKey).Conditions)
                .Select(rel => rel.MinimumPoints)
                .Where(val => val.HasValue)
                .Max();
            if (!maxTiers.TryGetValue(entry.EventId, out int? existingMax) || (points ?? -1) > (existingMax ?? -1))
                maxTiers[entry.EventId] = points;
            if (!minTiers.TryGetValue(entry.EventId, out int? existingMin) || (points ?? -1) < (existingMin ?? -1))
                minTiers[entry.EventId] = points;
        }
        return (maxTiers, minTiers);
    }

    private static (Dictionary<string, RomanceStage> MaxByEventId, Dictionary<string, RomanceStage> MinByEventId) ReadEventRomanceStages(
        IReadOnlyList<ResolvedEvent> allEvents,
        ConditionParser parser)
    {
        Dictionary<string, RomanceStage> maxGates = new(StringComparer.Ordinal);
        Dictionary<string, RomanceStage> minGates = new(StringComparer.Ordinal);
        foreach (ResolvedEvent entry in allEvents)
        {
            RomanceStage stage = RomanceStage.None;
            foreach (ConditionExpression expr in parser.ParseRawKey(entry.RawEventKey).Conditions)
            {
                RomanceStage candidate = ReadConditionRomanceStage(expr);
                if (candidate > stage)
                    stage = candidate;
            }
            if (!maxGates.TryGetValue(entry.EventId, out RomanceStage existingMax) || stage > existingMax)
                maxGates[entry.EventId] = stage;
            if (!minGates.TryGetValue(entry.EventId, out RomanceStage existingMin) || stage < existingMin)
                minGates[entry.EventId] = stage;
        }
        return (maxGates, minGates);
    }

    private static bool IsLocalPlayerSelector(string selector)
        => selector.Equals("Current", StringComparison.OrdinalIgnoreCase)
            || selector.Equals("Target", StringComparison.OrdinalIgnoreCase);

    private static void AddPair(HashSet<(EventIdentity Left, EventIdentity Right)> pairs, EventIdentity a, EventIdentity b)
        => pairs.Add(string.CompareOrdinal(a.StorageKey, b.StorageKey) <= 0 ? (a, b) : (b, a));

    private static bool HasPair(HashSet<(EventIdentity Left, EventIdentity Right)> pairs, EventIdentity a, EventIdentity b)
        => pairs.Contains(string.CompareOrdinal(a.StorageKey, b.StorageKey) <= 0 ? (a, b) : (b, a));
}
