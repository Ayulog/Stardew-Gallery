namespace StardewGallery;

/// <summary>An installed content pack and its declared dependencies, including optional ones.</summary>
internal sealed record EventDefinitionPack(EventSourceActor Actor, string DirectoryPath, IReadOnlyList<string> Dependencies);

/// <summary>A complete event definition declared in a reachable content-pack file.</summary>
internal sealed record EventDefinitionCandidate(EventSourceActor Actor, string AssetName, string RawEventKey,
    string RelativePath, string ScriptHash, string? ScriptReference = null);

internal enum EventOriginMatchStatus { Unknown, Identified, Ambiguous }

internal sealed record EventOriginMatch(EventSourceActor? Provider, IReadOnlyList<EventDefinitionCandidate> Candidates,
    EventOriginMatchStatus Status)
{
    internal bool Ambiguous => Status == EventOriginMatchStatus.Ambiguous;
}

/// <summary>
/// Matches original definition declarations by asset and event ID. Conditions and script changes do not
/// erase a declaration; declarations do not prove that a Content Patcher condition is currently active.
/// </summary>
internal sealed class EventDefinitionIndex
{
    private readonly Dictionary<EventIdentity, List<EventDefinitionCandidate>> definitions = new();
    private readonly Dictionary<string, HashSet<string>> dependencies = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<EventDefinitionCandidate> seen = new();

    internal int Count => seen.Count;
    internal IReadOnlyCollection<string> AssetNames => definitions.Keys.Select(key => key.AssetName)
        .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    internal void RegisterPack(EventDefinitionPack pack)
        => dependencies[pack.Actor.UniqueId] = new HashSet<string>(pack.Dependencies, StringComparer.OrdinalIgnoreCase);

    internal void Add(EventDefinitionCandidate candidate)
    {
        string asset = EventSourceScope.NormalizeAssetName(candidate.AssetName);
        string id = GetEventId(candidate.RawEventKey);
        if (!IsEventAsset(asset) || id.Length == 0 || id.Contains("{{", StringComparison.Ordinal)) return;
        candidate = candidate with { AssetName = asset };
        if (!seen.Add(candidate)) return;
        var identity = new EventIdentity(asset, id);
        if (!definitions.TryGetValue(identity, out List<EventDefinitionCandidate>? values))
            definitions.Add(identity, values = new());
        values.Add(candidate);
    }

    internal EventOriginMatch Match(string assetName, string eventId)
    {
        var identity = new EventIdentity(assetName, eventId);
        if (!definitions.TryGetValue(identity, out List<EventDefinitionCandidate>? values))
        {
            // GameLocation merges the original trailer event dictionary into the upgraded trailer.
            // Preserve the actual source asset in the evidence; no other location uses this fallback.
            bool copiedTrailerEvent = identity.AssetName.Equals("Data/Events/Trailer_Big", StringComparison.OrdinalIgnoreCase);
            if (!copiedTrailerEvent || !definitions.TryGetValue(new EventIdentity("Data/Events/Trailer", identity.EventId), out values))
                return new(null, Array.Empty<EventDefinitionCandidate>(), EventOriginMatchStatus.Unknown);
        }
        EventDefinitionCandidate[] evidence = values.OrderBy(value => value.Actor.UniqueId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(value => value.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(value => value.RawEventKey, StringComparer.Ordinal).ToArray();
        EventSourceActor? vanilla = evidence.FirstOrDefault(value => value.Actor.IsGameBase)?.Actor;
        if (vanilla is not null) return new(vanilla, evidence, EventOriginMatchStatus.Identified);
        EventSourceActor[] actors = evidence.Select(value => value.Actor)
            .DistinctBy(actor => actor.UniqueId, StringComparer.OrdinalIgnoreCase).ToArray();
        // A translation/compatibility pack which declares the original pack as a dependency is
        // evidence of an override. Mere scan order and framework dependency imply no authorship.
        EventSourceActor[] roots = actors.Where(actor => !actors.Any(other =>
            !StringComparer.OrdinalIgnoreCase.Equals(actor.UniqueId, other.UniqueId)
            && DependsOn(actor.UniqueId, other.UniqueId) && !DependsOn(other.UniqueId, actor.UniqueId))).ToArray();
        if (roots.Length == 1) return new(roots[0], evidence, EventOriginMatchStatus.Identified);
        return new(null, evidence, EventOriginMatchStatus.Ambiguous);
    }

    internal static string GetEventId(string rawEventKey)
        => (rawEventKey ?? string.Empty).Split('/')[0].Trim();

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
}
