namespace StardewGallery;

/// <summary>An installed content pack and its declared dependencies, including optional ones.</summary>
internal sealed record EventDefinitionPack(EventSourceActor Actor, string DirectoryPath, IReadOnlyList<string> Dependencies);

/// <summary>
/// A complete event or trigger-action definition declared in a reachable content-pack file. A modification
/// (Fields, TargetField or TextOperations on an existing Data/TriggerActions entry) is listed as evidence but
/// never counts as a declaration, so a pack that only edits an entry can't become its provider.
/// </summary>
internal sealed record EventDefinitionCandidate(EventSourceActor Actor, string AssetName, string RawEventKey,
    string RelativePath, string ScriptHash, string? ScriptReference = null)
{
    internal bool IsModification { get; init; }
}

/// <summary>Incomplete: one provider was found, but a pack it builds on couldn't be read fully and may hold the original.</summary>
internal enum EventOriginMatchStatus { Unknown, Identified, Ambiguous, Incomplete }

internal sealed record EventOriginMatch(EventSourceActor? Provider, IReadOnlyList<EventDefinitionCandidate> Candidates,
    EventOriginMatchStatus Status)
{
    internal static EventOriginMatch Unknown { get; } = new(null, Array.Empty<EventDefinitionCandidate>(), EventOriginMatchStatus.Unknown);
    internal bool Ambiguous => Status == EventOriginMatchStatus.Ambiguous;
    /// <summary>For <see cref="EventOriginMatchStatus.Incomplete"/>: the depended-on packs whose files weren't all read.</summary>
    internal IReadOnlyList<EventSourceActor> UnreadDependencies { get; init; } = Array.Empty<EventSourceActor>();
}

/// <summary>
/// Matches original definition declarations by asset and event ID (or exact dictionary key for fork/switchEvent
/// sub-scripts). Conditions and script changes do not erase a declaration; declarations do not prove that a Content
/// Patcher condition is currently active.
/// </summary>
internal sealed class EventDefinitionIndex
{
    private readonly Dictionary<EventIdentity, List<EventDefinitionCandidate>> definitions = new();
    private readonly Dictionary<(string Asset, string Key), List<EventDefinitionCandidate>> exactDefinitions = new(ExactAssetKeyComparer.Instance);
    private readonly Dictionary<string, HashSet<string>> dependencies = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<EventDefinitionCandidate> seen = new();
    private readonly Dictionary<string, EventSourceActor> incomplete = new(StringComparer.OrdinalIgnoreCase);

    internal int Count => seen.Count;
    internal IReadOnlyCollection<string> AssetNames => definitions.Keys.Select(key => key.AssetName)
        .Concat(exactDefinitions.Keys.Select(key => key.Asset))
        .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    internal void RegisterPack(EventDefinitionPack pack)
        => dependencies[pack.Actor.UniqueId] = new HashSet<string>(pack.Dependencies, StringComparer.OrdinalIgnoreCase);

    /// <summary>Record a loaded pack whose declarations may be missing: not found on disk, or some files not read.</summary>
    internal void MarkIncomplete(EventSourceActor actor) => incomplete.TryAdd(actor.UniqueId, actor);

    internal void Add(EventDefinitionCandidate candidate)
    {
        string asset = EventAssetName.Normalize(candidate.AssetName);
        if (!IsIndexedAsset(asset)) return;
        string id = GetEntryId(asset, candidate.RawEventKey);
        bool validEntry = id.Length > 0 && !id.Contains("{{", StringComparison.Ordinal);
        bool validExact = IsEventAsset(asset) && HasExactBranchKey(candidate.RawEventKey);
        if (!validEntry && !validExact) return;
        candidate = candidate with { AssetName = asset };
        if (!seen.Add(candidate)) return;
        if (validEntry)
        {
            var identity = IdentityKey(asset, id);
            if (!definitions.TryGetValue(identity, out List<EventDefinitionCandidate>? values))
                definitions.Add(identity, values = new());
            values.Add(candidate);
        }
        if (validExact)
        {
            var exactKey = (asset, candidate.RawEventKey);
            if (!exactDefinitions.TryGetValue(exactKey, out List<EventDefinitionCandidate>? exactValues))
                exactDefinitions.Add(exactKey, exactValues = new());
            exactValues.Add(candidate);
        }
    }

    internal EventOriginMatch Match(string assetName, string eventId)
    {
        var identity = IdentityKey(assetName, eventId);
        if (!definitions.TryGetValue(identity, out List<EventDefinitionCandidate>? values))
        {
            // GameLocation merges the original trailer event dictionary into the upgraded trailer.
            // Preserve the actual source asset in the evidence; no other location uses this fallback.
            bool copiedTrailerEvent = identity.AssetName.Equals("Data/Events/Trailer_Big", StringComparison.OrdinalIgnoreCase);
            if (!copiedTrailerEvent || !definitions.TryGetValue(new EventIdentity("Data/Events/Trailer", identity.EventId), out values))
                return EventOriginMatch.Unknown;
        }
        return ResolveMatch(values);
    }

    /// <summary>
    /// Match a fork/switchEvent sub-script by exact Data/Events dictionary key (Ordinal, as Event.Fork and
    /// Event.SwitchEvent look up location event dictionaries in 1.6.15), so namespaced keys containing '/'
    /// stay distinct and root events with preconditions ("foo/f ...") don't masquerade as branch "foo".
    /// </summary>
    internal EventOriginMatch MatchExactKey(string assetName, string rawKey)
    {
        string asset = EventAssetName.Normalize(assetName);
        string key = rawKey ?? string.Empty;
        if (!exactDefinitions.TryGetValue((asset, key), out List<EventDefinitionCandidate>? values))
        {
            bool copiedTrailerEvent = asset.Equals("Data/Events/Trailer_Big", StringComparison.OrdinalIgnoreCase);
            if (!copiedTrailerEvent || !exactDefinitions.TryGetValue(("Data/Events/Trailer", key), out values))
                return EventOriginMatch.Unknown;
        }
        return ResolveMatch(values);
    }

    private EventOriginMatch ResolveMatch(List<EventDefinitionCandidate> values)
    {
        EventDefinitionCandidate[] evidence = values.OrderBy(value => value.Actor.UniqueId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(value => value.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(value => value.RawEventKey, StringComparer.Ordinal).ToArray();
        // Edits of an existing entry stay visible as evidence but never decide the provider.
        EventDefinitionCandidate[] declarations = evidence.Where(value => !value.IsModification).ToArray();
        if (declarations.Length == 0) return new(null, evidence, EventOriginMatchStatus.Unknown);
        EventSourceActor? vanilla = declarations.FirstOrDefault(value => value.Actor.IsGameBase)?.Actor;
        if (vanilla is not null) return new(vanilla, evidence, EventOriginMatchStatus.Identified);
        EventSourceActor[] actors = declarations.Select(value => value.Actor)
            .DistinctBy(actor => actor.UniqueId, StringComparer.OrdinalIgnoreCase).ToArray();
        // A translation/compatibility pack which declares the original pack as a dependency is
        // evidence of an override. Mere scan order and framework dependency imply no authorship.
        EventSourceActor[] roots = actors.Where(actor => !actors.Any(other =>
            !StringComparer.OrdinalIgnoreCase.Equals(actor.UniqueId, other.UniqueId)
            && DependsOn(actor.UniqueId, other.UniqueId) && !DependsOn(other.UniqueId, actor.UniqueId))).ToArray();
        if (roots.Length != 1) return new(null, evidence, EventOriginMatchStatus.Ambiguous);
        // An override found alone may stand in for an original whose declaration couldn't be read.
        EventSourceActor[] unread = incomplete.Values.Where(pack => DependsOn(roots[0].UniqueId, pack.UniqueId)).ToArray();
        return unread.Length == 0 ? new(roots[0], evidence, EventOriginMatchStatus.Identified)
            : new(roots[0], evidence, EventOriginMatchStatus.Incomplete) { UnreadDependencies = unread };
    }

    internal static string GetEventId(string rawEventKey)
        => EventKey.TryGetId(rawEventKey, out string id) ? id : string.Empty;

    internal static bool HasExactBranchKey(string rawEventKey)
        => !string.IsNullOrWhiteSpace(rawEventKey) && !rawEventKey.Contains("{{", StringComparison.Ordinal);

    internal static bool HasIndexableEventKey(string rawEventKey)
    {
        string id = GetEventId(rawEventKey);
        return id.Length > 0 && !id.Contains("{{", StringComparison.Ordinal) || HasExactBranchKey(rawEventKey);
    }

    /// <summary>The asset this index uses for Data/TriggerActions rules, keyed by their Id field.</summary>
    internal const string TriggerActionAsset = "Data/TriggerActions";

    /// <summary>An event key's ID (first '/' entry), or a trigger-action rule ID as written.</summary>
    internal static string GetEntryId(string assetName, string rawKey)
        => IsTriggerActionAsset(assetName) ? (rawKey ?? string.Empty).Trim() : GetEventId(rawKey);

    internal static bool IsTriggerActionAsset(string assetName)
        => EventAssetName.Normalize(assetName).Equals(TriggerActionAsset, StringComparison.OrdinalIgnoreCase);

    internal static bool IsIndexedAsset(string assetName) => IsEventAsset(assetName) || IsTriggerActionAsset(assetName);

    /// <summary>
    /// Content Patcher and PrerequisiteCatalog match Data/TriggerActions rule IDs case-insensitively, so an
    /// override differing only in letter case still groups with the original declaration instead of standing alone.
    /// </summary>
    private static EventIdentity IdentityKey(string assetName, string entryId)
    {
        string asset = EventAssetName.Normalize(assetName);
        return new EventIdentity(asset, IsTriggerActionAsset(asset) ? (entryId ?? string.Empty).Trim().ToUpperInvariant() : entryId);
    }

    internal static bool IsEventAsset(string assetName)
        => assetName.StartsWith("Data/Events/", StringComparison.OrdinalIgnoreCase)
            && assetName.Length > "Data/Events/".Length && !assetName.Contains("{{", StringComparison.Ordinal);

    private bool DependsOn(string actorId, string otherId)
    {
        HashSet<string> visited = new(StringComparer.OrdinalIgnoreCase);
        Stack<string> pending = new();
        pending.Push(actorId);
        while (pending.Count > 0)
        {
            string current = pending.Pop();
            if (!visited.Add(current) || !dependencies.TryGetValue(current, out HashSet<string>? required)) continue;
            foreach (string dependency in required)
            {
                if (StringComparer.OrdinalIgnoreCase.Equals(dependency, otherId)) return true;
                pending.Push(dependency);
            }
        }
        return false;
    }

    private sealed class ExactAssetKeyComparer : IEqualityComparer<(string Asset, string Key)>
    {
        internal static ExactAssetKeyComparer Instance { get; } = new();
        public bool Equals((string Asset, string Key) x, (string Asset, string Key) y)
            => StringComparer.OrdinalIgnoreCase.Equals(x.Asset, y.Asset) && StringComparer.Ordinal.Equals(x.Key, y.Key);
        public int GetHashCode((string Asset, string Key) obj)
            => HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Asset), StringComparer.Ordinal.GetHashCode(obj.Key));
    }
}
