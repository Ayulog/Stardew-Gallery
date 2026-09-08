namespace StardewGallery;

internal sealed record ResolvedEventGroup(
    ResolvedEvent Current,
    IReadOnlyList<ResolvedEvent> Candidates,
    ResolvedEventSelectionSource SelectionSource
)
{
    internal EventIdentity Identity => Current.Identity;
}

internal enum ResolvedEventSelectionSource
{
    NativeMatch,
    IndeterminateFallback,
    NoMatchFallback
}

internal sealed class ResolvedEventCandidateCache(Func<IReadOnlyList<ResolvedEventCandidate>> loadCandidates)
{
    private IReadOnlyList<ResolvedEventCandidate>? candidates;
    private int revision;

    internal ResolvedEventIndex GetCurrent()
    {
        if (candidates is not null) return ResolvedEventIndex.Build(candidates);
        int startedAt = revision;
        IReadOnlyList<ResolvedEventCandidate> loaded = loadCandidates();
        if (revision == startedAt) candidates = loaded;
        return ResolvedEventIndex.Build(loaded);
    }

    internal void Invalidate() { revision++; candidates = null; }
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

    internal static ResolvedEventIndex ReadCurrent(
        IEventAssetSourceCatalog assets,
        ResolvedEventReader reader)
        => Build(ReadCurrentCandidates(assets, reader));

    internal static IReadOnlyList<ResolvedEventCandidate> ReadCurrentCandidates(
        IEventAssetSourceCatalog assets,
        ResolvedEventReader reader)
    {
        List<ResolvedEventCandidate> candidates = [];
        assets.VisitCurrent(source => candidates.AddRange(reader.Read(source)));
        return candidates;
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
            int selectedIndex = 0;
            ResolvedEventSelectionSource selectionSource = ResolvedEventSelectionSource.NoMatchFallback;
            for (int index = 0; index < matches.Count; index++)
            {
                NativePreconditionProbeStatus status = matches[index].ProbePrecondition().Status;
                if (status == NativePreconditionProbeStatus.NotMatched)
                    continue;
                selectedIndex = index;
                if (status == NativePreconditionProbeStatus.Matched)
                {
                    selectionSource = ResolvedEventSelectionSource.NativeMatch;
                    break;
                }
                selectionSource = ResolvedEventSelectionSource.IndeterminateFallback;
                break;
            }
            ResolvedEvent[] resolved = matches.Select(match => match.Resolved).ToArray();
            groups.Add(new ResolvedEventGroup(resolved[selectedIndex], Array.AsReadOnly(resolved), selectionSource));
        }
        return new ResolvedEventIndex(groups);
    }
}
