namespace StardewGallery;

internal sealed record ResolvedEventGroup(
    ResolvedEvent Current,
    IReadOnlyList<ResolvedEvent> Candidates
)
{
    internal EventIdentity Identity => Current.Identity;
}

internal sealed class ResolvedEventIndex
{
    private readonly IReadOnlyDictionary<EventIdentity, ResolvedEventGroup> byIdentity;

    private ResolvedEventIndex(IReadOnlyList<ResolvedEventGroup> groups)
    {
        Groups = Array.AsReadOnly(groups.ToArray());
        CurrentEvents = Array.AsReadOnly(Groups.Select(group => group.Current).ToArray());
        byIdentity = Groups.ToDictionary(group => group.Identity);
    }

    internal IReadOnlyList<ResolvedEventGroup> Groups { get; }

    internal IReadOnlyList<ResolvedEvent> CurrentEvents { get; }

    internal bool TryGetGroup(EventIdentity identity, out ResolvedEventGroup group)
    {
        if (byIdentity.TryGetValue(identity, out ResolvedEventGroup? found))
        {
            group = found;
            return true;
        }
        group = null!;
        return false;
    }

    internal bool TryGetCurrent(EventIdentity identity, out ResolvedEvent resolved)
    {
        if (TryGetGroup(identity, out ResolvedEventGroup group))
        {
            resolved = group.Current;
            return true;
        }
        resolved = null!;
        return false;
    }

    internal IReadOnlyList<ResolvedEvent> GetCandidates(EventIdentity identity)
        => TryGetGroup(identity, out ResolvedEventGroup group) ? group.Candidates : Array.Empty<ResolvedEvent>();

    internal static bool MatchesCurrentState(string? preconditionResult)
        => !string.IsNullOrEmpty(preconditionResult) && preconditionResult != "-1";

    internal static ResolvedEventIndex ReadCurrent(
        IEventAssetSourceCatalog assets,
        ResolvedEventReader reader)
    {
        List<ResolvedEventCandidate> candidates = [];
        assets.VisitCurrent(source => candidates.AddRange(reader.Read(source)));
        return Build(candidates);
    }

    internal static ResolvedEventIndex Build(IReadOnlyList<ResolvedEventCandidate> candidates)
    {
        Dictionary<EventIdentity, List<ResolvedEventCandidate>> grouped = [];
        List<EventIdentity> identityOrder = [];
        foreach (ResolvedEventCandidate candidate in candidates)
        {
            EventIdentity identity = candidate.Resolved.Identity;
            if (!grouped.TryGetValue(identity, out List<ResolvedEventCandidate>? matches))
            {
                grouped[identity] = matches = [];
                identityOrder.Add(identity);
            }
            if (!matches.Any(match =>
                match.Resolved.RawEventKey == candidate.Resolved.RawEventKey
                && match.Resolved.ResolvedScript == candidate.Resolved.ResolvedScript))
                matches.Add(candidate);
        }

        List<ResolvedEventGroup> groups = [];
        foreach (EventIdentity identity in identityOrder)
        {
            List<ResolvedEventCandidate> matches = grouped[identity];
            int selectedIndex = EventKey.SelectVariantIndex(matches.Count, index =>
            {
                try
                {
                    return MatchesCurrentState(matches[index].CheckPrecondition());
                }
                catch
                {
                    return false;
                }
            });
            ResolvedEvent[] resolved = matches.Select(match => match.Resolved).ToArray();
            groups.Add(new ResolvedEventGroup(resolved[selectedIndex], Array.AsReadOnly(resolved)));
        }
        return new ResolvedEventIndex(groups);
    }
}
