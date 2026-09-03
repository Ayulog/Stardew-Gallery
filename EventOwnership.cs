namespace StardewGallery;

internal enum OwnershipKind
{
    Direct,
    Inherited,
    Inferred,
    Excluded
}

internal sealed record EventOwner(string Name, int? FriendshipPoints);

internal sealed record EventOwnership(
    OwnershipKind Kind,
    IReadOnlyList<EventOwner> Owners,
    string? ExclusionReason = null
);

internal sealed record EventEvidence(
    EventIdentity Identity,
    string EventId,
    IReadOnlyDictionary<string, int> FriendshipRequirements,
    IReadOnlyList<string> PrerequisiteEventIds,
    IReadOnlySet<string> Actors,
    IReadOnlyDictionary<string, int> DialogueCounts
);

internal static class OwnershipResolver
{
    internal static IReadOnlyDictionary<EventIdentity, EventOwnership> Resolve(
        IReadOnlyList<EventEvidence> events,
        IReadOnlySet<string> eligibleCharacters)
    {
        Dictionary<EventIdentity, EventOwnership> result = [];
        Dictionary<string, List<EventEvidence>> byId = events
            .GroupBy(entry => entry.EventId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

        foreach (EventEvidence entry in events.Where(entry => entry.FriendshipRequirements.Count > 0))
        {
            List<EventOwner> owners = entry.FriendshipRequirements
                .Where(pair => eligibleCharacters.Contains(pair.Key))
                .Select(pair => new EventOwner(pair.Key, pair.Value))
                .ToList();
            if (owners.Count > 1)
            {
                int maximum = owners.Max(owner => entry.DialogueCounts.GetValueOrDefault(owner.Name));
                List<EventOwner> speakingOwners = owners
                    .Where(owner => maximum > 0 && entry.DialogueCounts.GetValueOrDefault(owner.Name) == maximum)
                    .ToList();
                if (speakingOwners.Count == 1)
                    owners = speakingOwners;
            }
            result[entry.Identity] = owners.Count > 0
                ? new EventOwnership(OwnershipKind.Direct, owners)
                : new EventOwnership(OwnershipKind.Excluded, [], "friendship-subject-not-eligible");
        }

        while (result.Count < events.Count)
        {
            bool changed = false;
            foreach (EventEvidence entry in events.Where(entry => !result.ContainsKey(entry.Identity)))
            {
                if (!TryGetSinglePrevious(entry, byId, out EventEvidence? previousEntry)
                    || !result.TryGetValue(previousEntry.Identity, out EventOwnership? previous)
                    || previous.Kind == OwnershipKind.Excluded)
                    continue;

                result[entry.Identity] = new EventOwnership(OwnershipKind.Inherited, previous.Owners);
                changed = true;
            }
            if (changed)
                continue;

            List<EventEvidence> inferable = events.Where(entry =>
                !result.ContainsKey(entry.Identity)
                && (!TryGetSinglePrevious(entry, byId, out EventEvidence? previous)
                    || result.TryGetValue(previous.Identity, out EventOwnership? owner) && owner.Kind == OwnershipKind.Excluded)
            ).ToList();

            if (inferable.Count == 0)
            {
                foreach (EventEvidence entry in events.Where(entry => !result.ContainsKey(entry.Identity)))
                    result[entry.Identity] = new EventOwnership(OwnershipKind.Excluded, [], "ownership-dependency-cycle");
                break;
            }

            foreach (EventEvidence entry in inferable)
                result[entry.Identity] = Infer(entry, eligibleCharacters);
        }

        return result;
    }

    private static bool TryGetSinglePrevious(
        EventEvidence entry,
        IReadOnlyDictionary<string, List<EventEvidence>> byId,
        out EventEvidence previous)
    {
        string[] ids = entry.PrerequisiteEventIds.Distinct(StringComparer.Ordinal).ToArray();
        if (ids.Length == 1 && byId.TryGetValue(ids[0], out List<EventEvidence>? matches) && matches.Count == 1)
        {
            previous = matches[0];
            return true;
        }

        previous = null!;
        return false;
    }

    private static EventOwnership Infer(EventEvidence entry, IReadOnlySet<string> eligibleCharacters)
    {
        Dictionary<string, int> counts = entry.DialogueCounts
            .Where(pair => pair.Value > 0 && entry.Actors.Contains(pair.Key) && eligibleCharacters.Contains(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        if (counts.Count == 0)
            return new EventOwnership(OwnershipKind.Excluded, [], "no-eligible-speaking-actor");

        int maximum = counts.Values.Max();
        return new EventOwnership(
            OwnershipKind.Inferred,
            counts.Where(pair => pair.Value == maximum).Select(pair => new EventOwner(pair.Key, null)).ToList()
        );
    }
}
