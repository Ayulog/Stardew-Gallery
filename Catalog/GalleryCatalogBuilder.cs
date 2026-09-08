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
    Func<string?> getSpouse)
{
    internal GalleryCatalogBuildResult Build(
        IReadOnlyList<GalleryCharacter> characters,
        IReadOnlyList<ResolvedEvent> currentEvents,
        Func<string, GalleryCharacter?>? resolveDisplayCharacter = null)
    {
        List<EventEvidence> evidence = currentEvents.Select(ParseEvidence).ToList();
        HashSet<EventIdentity> contextual = currentEvents.Where(entry => entry.HasLocationContext).Select(entry => entry.Identity).ToHashSet();
        Dictionary<EventIdentity, EventEvidence> evidenceByIdentity = evidence.ToDictionary(item => item.Identity);
        IReadOnlyDictionary<EventIdentity, EventOwnership> ownership = OwnershipResolver.Resolve(
            evidence.Where(item => contextual.Contains(item.Identity)).ToList(),
            characters.Select(character => character.Name).ToHashSet(StringComparer.Ordinal)
        );
        Dictionary<string, GalleryCharacter> profiles = characters.ToDictionary(character => character.Name, StringComparer.Ordinal);
        HashSet<string> checkedNames = new(profiles.Keys, StringComparer.Ordinal);
        foreach (string name in evidence.SelectMany(item => item.FriendshipRequirements.Keys.Concat(item.DialogueCounts.Keys)).Distinct(StringComparer.Ordinal))
        {
            if (!checkedNames.Add(name)) continue;
            try
            {
                if (resolveDisplayCharacter?.Invoke(name) is { } profile) profiles.TryAdd(name, profile);
            }
            catch
            {
                // Optional display metadata must not prevent the remaining catalog from opening.
            }
        }
        List<GalleryEvent> analyzedEvents = currentEvents
            .Select(resolved => new GalleryEvent(resolved, ownership.GetValueOrDefault(resolved.Identity)
                ?? new EventOwnership(OwnershipKind.Excluded, [], "missing-location-context")))
            .ToList();
        Dictionary<EventIdentity, HashSet<string>> associations = [];
        foreach (EventEvidence item in evidence)
        {
            // Dialogue identifies participation; actor placement alone does not establish a subject.
            associations[item.Identity] = item.FriendshipRequirements.Keys.Concat(item.DialogueCounts.Keys)
                .Concat(ownership.GetValueOrDefault(item.Identity)?.Owners.Select(owner => owner.Name) ?? [])
                .Where(profiles.ContainsKey).ToHashSet(StringComparer.Ordinal);
        }
        var byId = evidence.GroupBy(item => item.EventId, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        bool changed;
        do
        {
            changed = false;
            foreach (EventEvidence item in evidence.Where(item => associations[item.Identity].Count == 0))
            {
                string[] prior = item.PrerequisiteEventIds.Distinct(StringComparer.Ordinal).ToArray();
                if (prior.Length == 1 && byId.TryGetValue(prior[0], out var previous) && previous.Length == 1
                    && associations[previous[0].Identity].Count > 0)
                {
                    associations[item.Identity].UnionWith(associations[previous[0].Identity]);
                    changed = true;
                }
            }
        } while (changed);
        analyzedEvents = analyzedEvents.Select(entry => entry with
        {
            RelatedNpcNames = associations[entry.Resolved.Identity].OrderBy(name => name, StringComparer.Ordinal).ToArray(),
            HeartNpcNames = evidenceByIdentity[entry.Resolved.Identity].FriendshipRequirements.Where(pair => pair.Value > 0).Select(pair => pair.Key)
                .Concat(entry.Ownership.Owners.Where(owner => owner.FriendshipPoints > 0).Select(owner => owner.Name)).Distinct(StringComparer.Ordinal).ToArray(),
            IsFlow = IsFlow(entry.Resolved)
        }).ToList();
        List<GalleryEvent> includedEvents = analyzedEvents
            .Where(entry => entry.Ownership.Kind != OwnershipKind.Excluded)
            .ToList();
        HashSet<string> galleryNames = analyzedEvents
            .SelectMany(entry => entry.RelatedNpcNames)
            .ToHashSet(StringComparer.Ordinal);
        List<GalleryCharacter> galleryCharacters = profiles.Values
            .Where(character => galleryNames.Contains(character.Name))
            .ToList();
        GalleryCatalog catalog = new(
            galleryCharacters,
            includedEvents,
            analyzedEvents.Where(entry => entry.Ownership.Kind == OwnershipKind.Excluded).ToList()
        ) { Groups = GalleryEventGroup.Build(analyzedEvents) };
        return new GalleryCatalogBuildResult(catalog, analyzedEvents);
    }

    private bool IsFlow(ResolvedEvent entry)
    {
        if (entry.Fragments.MissingKeys.Count > 0) return false;
        string[] root = parseCommands(entry.ResolvedScript);
        if (root.Length <= 3) return false;
        return root.Skip(3).Concat(entry.Fragments.Scripts.Skip(1).SelectMany(parseCommands))
            .All(command => splitArguments(command).FirstOrDefault()?.ToLowerInvariant() is "pause" or "end");
    }

    private EventEvidence ParseEvidence(ResolvedEvent entry)
    {
        Dictionary<string, int> friendship = new(StringComparer.Ordinal);
        List<string> prerequisites = [];
        foreach (string condition in splitPreconditions(entry.RawEventKey).Skip(1))
        {
            string[] args = splitArguments(condition);
            if (args.Length == 0 || args[0].StartsWith('!'))
                continue;

            if (args[0] == "f" || args[0].Equals("Friendship", StringComparison.OrdinalIgnoreCase))
            {
                for (int i = 1; i + 1 < args.Length; i += 2)
                {
                    if (int.TryParse(args[i + 1], out int points))
                        friendship[args[i]] = Math.Max(friendship.GetValueOrDefault(args[i]), points);
                }
            }
            else if (args[0] == "e" || args[0].Equals("SawEvent", StringComparison.OrdinalIgnoreCase))
                prerequisites.AddRange(args.Skip(1));
        }

        string[] rootCommands = parseCommands(entry.ResolvedScript);
        HashSet<string> actors = new(StringComparer.Ordinal);
        if (rootCommands.Length > 2)
        {
            string[] positions = splitPositions(rootCommands[2]);
            for (int i = 0; i + 3 < positions.Length; i += 4)
            {
                string? actor = positions[i] == "spouse" ? getSpouse() : positions[i];
                if (!string.IsNullOrWhiteSpace(actor) && actor != "farmer" && actor != "otherFarmers")
                    actors.Add(actor);
            }
        }

        Dictionary<string, int> dialogue = new(StringComparer.Ordinal);
        foreach (string command in entry.Fragments.Scripts.SelectMany(parseCommands).Skip(3))
        {
            string[] args = splitArguments(command);
            if (args.Length > 1 && args[0].Equals("speak", StringComparison.OrdinalIgnoreCase))
            {
                string speaker = args[1].TrimEnd('?');
                dialogue[speaker] = dialogue.GetValueOrDefault(speaker) + 1;
            }
        }

        return new EventEvidence(entry.Identity, entry.EventId, friendship, prerequisites, actors, dialogue);
    }
}
