namespace StardewGallery;

internal sealed record GalleryCatalogBuildResult(
    GalleryCatalog Catalog,
    IReadOnlyList<GalleryEvent> AnalyzedEvents
);

internal sealed class GalleryCatalogBuilder(
    Func<string, string[]> splitPreconditions,
    Func<string, string[]> parseCommands,
    Func<string, string[]> splitArguments,
    Func<string, string[]> splitPositions,
    Func<string?> getSpouse,
    Func<string, string?>? loadTranslation = null)
{
    private readonly ConditionParser conditionParser = new(splitPreconditions, splitArguments);

    internal GalleryCatalogBuildResult Build(
        IReadOnlyList<GalleryCharacter> characters,
        IReadOnlyList<ResolvedEvent> currentEvents)
    {
        Dictionary<EventIdentity, EventEvidence> evidence = currentEvents.ToDictionary(entry => entry.Identity, ParseEvidence);
        IReadOnlyDictionary<EventIdentity, EventOwnership> ownership = OwnershipResolver.Resolve(
            evidence.Values.ToList(),
            characters.Select(character => character.Name)
                .Concat(evidence.Values.SelectMany(entry => entry.FriendshipRequirements.Keys))
                .ToHashSet(StringComparer.Ordinal)
        );
        Dictionary<EventIdentity, GalleryEvent> classified = currentEvents.ToDictionary(entry => entry.Identity, entry =>
        {
            EventEvidence facts = evidence[entry.Identity];
            string? internalReason = InternalFlowReason(entry);
            EventOwnership eventOwnership = facts.FriendshipRequirements.Count == 0 && facts.RelationshipNpcNames.Count > 0
                ? new EventOwnership(OwnershipKind.Direct, facts.RelationshipNpcNames.Select(name => new EventOwner(name, null)).ToArray())
                : ownership[entry.Identity];
            bool directHeart = facts.FriendshipRequirements.Count > 0 || facts.RelationshipNpcNames.Count > 0;
            bool isController = internalReason is null && IsControllerEvent(entry);
            return new GalleryEvent(entry, eventOwnership)
            {
                Kind = internalReason is not null ? StoryKind.Internal : directHeart ? StoryKind.Heart : StoryKind.Ordinary,
                IsControllerEvent = isController,
                RelatedNpcNames = facts.Actors.Concat(facts.DialogueCounts.Keys).Concat(facts.FriendshipRequirements.Keys)
                    .Concat(facts.RelationshipNpcNames)
                    .Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal).ToArray(),
                ClassificationReason = internalReason ?? (isController ? "controller-no-dialogue-or-visible-stage-action"
                    : facts.FriendshipRequirements.Count > 0 ? "positive-friendship"
                    : facts.RelationshipNpcNames.Count > 0 ? "positive-relationship" : "no-confirmed-heart-relationship")
            };
        });

        Dictionary<string, List<EventEvidence>> byId = evidence.Values.GroupBy(entry => entry.EventId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
        // A social owner is not proof of a heart story. Only continue a classified heart story,
        // with one unambiguous prerequisite and a returning subject in this scene.
        bool changed;
        do
        {
            changed = false;
            foreach (EventEvidence facts in evidence.Values)
            {
                GalleryEvent entry = classified[facts.Identity];
                string[] previousIds = facts.PrerequisiteEventIds.Distinct(StringComparer.Ordinal).ToArray();
                if (entry.Kind != StoryKind.Ordinary || entry.IsControllerEvent || facts.FriendshipRequirements.Count > 0
                    || previousIds.Length != 1 || !byId.TryGetValue(previousIds[0], out List<EventEvidence>? matches)
                    || matches.Count != 1 || classified[matches[0].Identity] is not { Kind: StoryKind.Heart, IsControllerEvent: false } previous
                    || entry.Fragments.MissingKeys.Count > 0)
                    continue;

                EventOwner[] returningOwners = previous.Ownership.Owners
                    .Where(owner => HasStoryParticipation(entry.Resolved, owner.Name, facts)).ToArray();
                if (returningOwners.Length == 0)
                    continue;
                classified[facts.Identity] = entry with
                {
                    Kind = StoryKind.Heart,
                    Ownership = new EventOwnership(OwnershipKind.Inherited, returningOwners),
                    ClassificationReason = "single-heart-continuation"
                };
                changed = true;
            }
        } while (changed);
        List<GalleryEvent> analyzedEvents = currentEvents
            .Select(resolved => classified[resolved.Identity])
            .ToList();
        List<GalleryEvent> includedEvents = analyzedEvents
            .Where(entry => entry.Kind == StoryKind.Heart && !entry.IsControllerEvent)
            .ToList();
        HashSet<string> galleryNames = includedEvents
            .SelectMany(entry => entry.Ownership.Owners)
            .Select(owner => owner.Name)
            .ToHashSet(StringComparer.Ordinal);
        List<GalleryCharacter> galleryCharacters = characters
            .Where(character => galleryNames.Contains(character.Name))
            .ToList();
        IReadOnlyDictionary<EventIdentity, string> parallelGroups = ParallelBranchAnalyzer.Analyze(
            includedEvents,
            currentEvents,
            conditionParser,
            parseCommands,
            splitArguments,
            loadTranslation
        );
        GalleryCatalog catalog = new(
            galleryCharacters,
            includedEvents,
            analyzedEvents.Where(entry => entry.Kind != StoryKind.Heart || entry.IsControllerEvent).ToList()
        )
        {
            ParallelGroups = parallelGroups
        };
        return new GalleryCatalogBuildResult(catalog, analyzedEvents);
    }

    private EventEvidence ParseEvidence(ResolvedEvent entry)
    {
        Dictionary<string, int> friendship = new(StringComparer.Ordinal);
        HashSet<string> relationships = new(StringComparer.Ordinal);
        IReadOnlyList<ConditionExpression> conditions = conditionParser.ParseRawKey(entry.RawEventKey).Conditions;
        foreach (RelationshipEvidence relationship in ConditionRelationshipEvidence.Read(conditions))
        {
            if (relationship.MinimumPoints is int points)
                friendship[relationship.Npc] = Math.Max(friendship.GetValueOrDefault(relationship.Npc), points);
            else
                relationships.Add(relationship.Npc);
        }
        List<string> prerequisites = conditions.OfType<SawEventCondition>().Where(condition => !condition.Negated)
            .SelectMany(condition => condition.EventIds).ToList();

        string[] rootCommands = parseCommands(entry.ResolvedScript);
        HashSet<string> actors = new(StringComparer.Ordinal);
        if (rootCommands.Length > 2)
        {
            string[] positions = splitPositions(rootCommands[2]);
            for (int i = 0; i + 3 < positions.Length; i += 4)
            {
                string? actor = ResolveActor(positions[i]);
                if (!string.IsNullOrWhiteSpace(actor) && actor != "farmer" && actor != "otherFarmers")
                    actors.Add(actor);
            }
        }

        Dictionary<string, int> dialogue = new(StringComparer.Ordinal);
        foreach (string command in Commands(entry))
        {
            string[] args = splitArguments(command);
            if (args.Length > 1 && (args[0].Equals("speak", StringComparison.OrdinalIgnoreCase)
                || args[0].Equals("splitSpeak", StringComparison.OrdinalIgnoreCase)
                || args[0].Equals("textAboveHead", StringComparison.OrdinalIgnoreCase)))
            {
                string speaker = ResolveActor(args[1].TrimEnd('?'));
                dialogue[speaker] = dialogue.GetValueOrDefault(speaker) + 1;
            }
            if (args.Length > 1 && (args[0].Equals("addActor", StringComparison.OrdinalIgnoreCase)
                || args[0].Equals("loadActor", StringComparison.OrdinalIgnoreCase)))
                actors.Add(ResolveActor(args[1]));
            if (args.Length > 2 && args[0].Equals("end", StringComparison.OrdinalIgnoreCase)
                && args[1].Equals("dialogue", StringComparison.OrdinalIgnoreCase))
                actors.Add(ResolveActor(args[2]));
        }
        actors.RemoveWhere(name => string.IsNullOrWhiteSpace(name) || name is "farmer" or "otherFarmers" or "spouse");
        dialogue.Remove("farmer");
        dialogue.Remove("otherFarmers");
        dialogue.Remove("spouse");

        return new EventEvidence(entry.Identity, entry.EventId, friendship, prerequisites, actors, dialogue)
        {
            RelationshipNpcNames = relationships.OrderBy(name => name, StringComparer.Ordinal).ToArray()
        };
    }

    private string ResolveActor(string name)
    {
        name = name.TrimEnd('?');
        return name == "spouse" ? getSpouse() ?? name : name;
    }

    private bool HasStoryParticipation(ResolvedEvent entry, string name, EventEvidence facts)
    {
        if (facts.DialogueCounts.ContainsKey(name))
            return true;
        return Commands(entry).Select(splitArguments).Any(args => args.Length > 1
            && ResolveActor(args[1]) == name
            && args[0].ToLowerInvariant() is "move" or "advancedmove" or "animate" or "emote" or "showframe" or "jump" or "shake");
    }

    private IEnumerable<string> Commands(ResolvedEvent entry)
    {
        foreach (string command in parseCommands(entry.ResolvedScript).Skip(3))
            yield return command;
        foreach (string fragment in entry.Fragments.Scripts.Where(script => script != entry.ResolvedScript))
            foreach (string command in parseCommands(fragment))
                yield return command;
    }

    internal bool IsInternalFlow(ResolvedEvent entry) => InternalFlowReason(entry) is not null;

    /// <summary>
    /// Dual predicate for silent background controller scripts (e.g., mod state-setters / offscreen triggers):
    /// true ONLY when there are (a) NO dialogue/message/question commands AND (b) NO visible non-blackout stage
    /// action commands across root and reachable fork/switchEvent fragments.
    /// </summary>
    internal bool IsControllerEvent(ResolvedEvent entry)
    {
        if (entry.Fragments.MissingKeys.Count > 0)
            return false;
        string[] root = parseCommands(entry.ResolvedScript);
        if (root.Length < 3)
            return false;

        bool rootOffscreen = IsOffscreen(splitPositions(root[1]));
        var scriptsByKey = entry.Fragments.ScriptsByKey;
        Queue<(string Script, bool Offscreen, bool BlackedOut, bool SkipHeader)> pending = new();
        HashSet<(string Script, bool Offscreen, bool BlackedOut, bool SkipHeader)> visited = new();
        HashSet<string> visitedScripts = new(StringComparer.Ordinal);

        void Enqueue(string script, bool offscreen, bool blackedOut, bool skipHeader)
        {
            var state = (script, offscreen, blackedOut, skipHeader);
            if (visited.Add(state))
            {
                pending.Enqueue(state);
                visitedScripts.Add(script);
            }
        }

        Enqueue(entry.ResolvedScript, rootOffscreen, blackedOut: false, skipHeader: true);
        if (scriptsByKey is null || scriptsByKey.Count == 0)
        {
            foreach (string fragment in entry.Fragments.Scripts.Where(s => s != entry.ResolvedScript))
                Enqueue(fragment, offscreen: false, blackedOut: false, skipHeader: false);
        }

        while (pending.Count > 0)
        {
            (string script, bool offscreen, bool blackedOut, bool skipHeader) = pending.Dequeue();
            string[] commands = parseCommands(script);
            int start = skipHeader ? 3 : 0;
            for (int i = start; i < commands.Length; i++)
            {
                string raw = commands[i];
                if (EventCommandEnumerator.TryGetQuickQuestionBranches(raw, out _))
                    return false;

                string[] args = splitArguments(raw);
                if (args.Length == 0)
                    continue;

                string head = args[0].ToLowerInvariant();
                if (head is "speak" or "splitspeak" or "message" or "question" or "quickquestion" or "textabovehead")
                    return false;

                if (head == "globalfade")
                {
                    blackedOut = true;
                    continue;
                }
                if (head == "globalfadetoclear")
                {
                    blackedOut = false;
                    continue;
                }
                if (head == "fade")
                {
                    blackedOut = !(args.Length > 1 && args[1].Equals("unfade", StringComparison.OrdinalIgnoreCase));
                    continue;
                }
                if (head == "viewport")
                {
                    if (args.Length >= 3 && int.TryParse(args[1], out int vx) && int.TryParse(args[2], out int vy))
                        offscreen = vx < 0 && vy < 0;
                    continue;
                }
                if (head is "fork" or "switchevent" && args.Length > 1)
                {
                    string key = head == "switchevent" ? args[1] : (args.Length > 2 ? args[2] : args[1]);
                    if (scriptsByKey is not null && scriptsByKey.TryGetValue(key, out string? frag))
                        Enqueue(frag, offscreen, blackedOut, skipHeader: false);
                    if (head == "switchevent")
                        break;
                    continue;
                }

                if (!offscreen && !blackedOut && head is "move" or "advancedmove" or "animate" or "emote"
                    or "showframe" or "jump" or "shake" or "specifictemporarysprite" or "addtemporaryactor")
                    return false;
            }
        }

        // Defensive check: if any fragment script wasn't reached by key lookup, inspect it conservatively.
        foreach (string unvisited in entry.Fragments.Scripts)
        {
            if (visitedScripts.Contains(unvisited))
                continue;
            foreach (string raw in parseCommands(unvisited))
            {
                if (EventCommandEnumerator.TryGetQuickQuestionBranches(raw, out _))
                    return false;
                string[] args = splitArguments(raw);
                if (args.Length == 0) continue;
                string head = args[0].ToLowerInvariant();
                if (head is "speak" or "splitspeak" or "message" or "question" or "quickquestion" or "textabovehead")
                    return false;
                if (head is "move" or "advancedmove" or "animate" or "emote" or "showframe" or "jump" or "shake"
                    or "specifictemporarysprite" or "addtemporaryactor")
                    return false;
            }
        }

        return true;
    }

    private string? InternalFlowReason(ResolvedEvent entry)
    {
        if (entry.Fragments.MissingKeys.Count > 0)
            return null;
        string[] root = parseCommands(entry.ResolvedScript);
        if (root.Length < 3)
            return null;
        bool hiddenStage = IsOffscreen(splitPositions(root[1]));
        bool effect = false;
        bool ends = false;
        foreach (string command in Commands(entry))
        {
            string[] args = splitArguments(command);
            if (args.Length == 0)
                continue;
            string head = args[0].ToLowerInvariant();
            if (head == "end")
            {
                ends = true;
                // 'end dialogue' queues later NPC dialogue; it does not speak in this event.
                if (args.Length > 1 && args[1].ToLowerInvariant() is not ("dialogue" or "invisible" or "position" or "warphome" or "warpout" or "bed"))
                    return null;
                effect |= args.Length > 1;
            }
            else if (head == "viewport")
            {
                if (!IsOffscreen(args.Skip(1).ToArray()))
                    return null;
            }
            else if (head is "mail" or "addmailreceived" or "removemailreceived" or "mailtomorrow"
                or "addquest" or "removequest" or "completequest" or "addconversationtopic"
                or "addcraftingrecipe" or "addcookingrecipe" or "setworldstate"
                or "seteventseen" or "removeeventseen" or "addspecialorder" or "removespecialorder"
                or "changelocation")
                effect = true;
            else if (head == "warp" && args.Length > 1 && args[1] == "farmer")
                effect = true;
            else if (head is "pause" or "skippable" or "broadcastevent"
                or "fade" or "globalfade" or "playmusic" or "stopmusic"
                or "fork" or "switchevent")
                continue;
            else
                return null;
        }
        return ends
            ? effect ? "internal-state-or-transition-only" : hiddenStage ? "internal-offscreen-marker" : "internal-timing-only"
            : null;
    }

    private static bool IsOffscreen(string[] coordinates) => coordinates.Length >= 2
        && int.TryParse(coordinates[0], out int x) && int.TryParse(coordinates[1], out int y)
        && x < 0 && y < 0;
}
