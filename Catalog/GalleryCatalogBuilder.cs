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
        IReadOnlyList<ResolvedEvent> currentEvents)
    {
        IReadOnlyDictionary<EventIdentity, EventOwnership> ownership = OwnershipResolver.Resolve(
            currentEvents.Select(ParseEvidence).ToList(),
            characters.Select(character => character.Name).ToHashSet(StringComparer.Ordinal)
        );
        List<GalleryEvent> analyzedEvents = currentEvents
            .Select(resolved => new GalleryEvent(resolved, ownership[resolved.Identity]))
            .ToList();
        List<GalleryEvent> includedEvents = analyzedEvents
            .Where(entry => entry.Ownership.Kind != OwnershipKind.Excluded)
            .ToList();
        HashSet<string> galleryNames = includedEvents
            .SelectMany(entry => entry.Ownership.Owners)
            .Select(owner => owner.Name)
            .ToHashSet(StringComparer.Ordinal);
        List<GalleryCharacter> galleryCharacters = characters
            .Where(character => galleryNames.Contains(character.Name))
            .ToList();
        GalleryCatalog catalog = new(
            galleryCharacters,
            includedEvents,
            analyzedEvents.Where(entry => entry.Ownership.Kind == OwnershipKind.Excluded).ToList()
        );
        return new GalleryCatalogBuildResult(catalog, analyzedEvents);
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
