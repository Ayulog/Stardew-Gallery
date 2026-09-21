namespace StardewGallery;

/// <summary>Stores completed observations only; load tokens prevent stale completion after invalidation.</summary>
internal sealed class EventSourceIndex
{
    private readonly object sync = new();
    private readonly Dictionary<EventSourceScope, long> latestLoads = new();
    private readonly Dictionary<EventSourceScope, IReadOnlyDictionary<string, EventSourceInfo>> snapshots = new();
    private long nextLoadId;

    internal int ObservedAssetCount
    {
        get { lock (sync) return snapshots.Count; }
    }
    internal EventSourceLoad BeginLoad(EventSourceScope scope)
    {
        lock (sync)
        {
            long id = ++nextLoadId;
            latestLoads[scope] = id;
            snapshots.Remove(scope);
            return new EventSourceLoad(this, scope, id);
        }
    }

    internal EventSourceInfo Lookup(EventSourceScope scope, string rawKey, string script)
    {
        string hash = EventHashes.RootScript(script);
        lock (sync)
        {
            if (!snapshots.TryGetValue(scope, out var snapshot) || !snapshot.TryGetValue(rawKey, out var info))
                return new(scope, rawKey, hash, 0, 0, null, null, Array.Empty<EventSourceMutation>(),
                    EventSourceStatus.Unknown, "No completed observation for this asset, language, context and raw key.", ScriptMatches: false);

            if (!info.IsDeleted && StringComparer.Ordinal.Equals(info.ScriptHash, hash))
                return info;

            // A cached dictionary or GameLocation may have been changed outside the observed pipeline.
            // Keep the audit trail, but don't assign its provider to an unverified current definition.
            return info with
            {
                Provider = null,
                ProviderExecutor = null,
                Status = EventSourceStatus.Partial,
                Reason = "Current script does not match the completed observation; its source is unverified.",
                ScriptMatches = false
            };
        }
    }

    internal void InvalidateAsset(string assetName)
    {
        string normalized = EventSourceScope.NormalizeAssetName(assetName);
        lock (sync)
        {
            foreach (var scope in latestLoads.Keys.Where(scope => StringComparer.OrdinalIgnoreCase.Equals(scope.AssetName, normalized)).ToArray())
            {
                latestLoads.Remove(scope);
                snapshots.Remove(scope);
            }
        }
    }

    internal void Clear()
    {
        lock (sync)
        {
            latestLoads.Clear();
            snapshots.Clear();
        }
    }

    internal object Sync => sync;
    internal bool IsCurrent(EventSourceLoad load) => latestLoads.TryGetValue(load.Scope, out long id) && id == load.LoadId;

    internal bool Publish(EventSourceLoad load, IReadOnlyDictionary<string, EventSourceInfo> entries)
    {
        if (!IsCurrent(load))
            return false;
        snapshots[load.Scope] = entries;
        return true;
    }
}

/// <summary>Transient fingerprint state for one load. Does not retain scripts or operation delegates.</summary>
internal sealed class EventSourceLoad
{
    private sealed class Entry
    {
        internal string? Hash;
        internal int Generation;
        internal EventSourceActor? Provider;
        internal EventSourceActor? ProviderExecutor;
        internal readonly List<EventSourceMutation> Mutations = new();
    }

    private readonly EventSourceIndex owner;
    private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);
    private readonly List<string> partialReasons = new();
    private bool hasBaseline;
    private bool finished;
    private long sequence;

    internal EventSourceLoad(EventSourceIndex owner, EventSourceScope scope, long loadId)
    {
        this.owner = owner;
        Scope = scope;
        LoadId = loadId;
    }

    internal EventSourceScope Scope { get; }
    internal long LoadId { get; }

    /// <summary>Call only after SMAPI accepts the loader's result, or verifies the raw game baseline.</summary>
    internal void RecordBaseline(IReadOnlyDictionary<string, string> values, EventSourceActor? provider,
        EventSourceActor? executor = null, bool fullyObserved = true)
    {
        lock (owner.Sync)
        {
            if (!CanObserve())
                return;
            var hashes = Fingerprints(values);
            if (hasBaseline)
            {
                AddReason("The load baseline was observed more than once.");
                Reconcile(hashes);
                return;
            }
            hasBaseline = true;
            if (!fullyObserved)
                AddReason("The complete load baseline was not observed.");
            if (provider is null)
                AddReason("The baseline provider is unknown.");
            foreach (var pair in hashes)
            {
                entries[pair.Key] = new Entry
                {
                    Hash = pair.Value,
                    Generation = 1,
                    Provider = fullyObserved || provider?.IsGameBase != true ? provider : null,
                    ProviderExecutor = executor
                };
            }
        }
    }

    /// <summary>The caller supplies a copied before snapshot and actual remaining content after execution.</summary>
    internal void ObserveEdit(IReadOnlyDictionary<string, string> before, IReadOnlyDictionary<string, string> after,
        EventSourceActor? actor, EventSourceActor? executor = null, bool failed = false)
    {
        lock (owner.Sync)
        {
            if (!CanObserve())
                return;
            var beforeHashes = Fingerprints(before);
            var afterHashes = Fingerprints(after);
            if (!hasBaseline)
            {
                RecordBaseline(before, null, fullyObserved: false);
            }
            Reconcile(beforeHashes);
            if (failed)
                AddReason("An observed editor failed; only its remaining differences are recorded.");
            if (actor is null && (beforeHashes.Count != afterHashes.Count || beforeHashes.Any(pair => !afterHashes.TryGetValue(pair.Key, out string? hash) || !StringComparer.Ordinal.Equals(pair.Value, hash))))
                AddReason("An editor identity was unavailable.");
            ApplyDifferences(afterHashes, actor, executor, failed, unknownGap: false);
        }
    }

    internal void MarkPartial(string reason)
    {
        lock (owner.Sync)
        {
            if (CanObserve())
                AddReason(reason);
        }
    }

    internal bool Complete(IReadOnlyDictionary<string, string> finalValues)
    {
        lock (owner.Sync)
        {
            if (!CanObserve())
                return false;
            if (!hasBaseline)
                RecordBaseline(finalValues, null, fullyObserved: false);
            Reconcile(Fingerprints(finalValues));
            string? reason = partialReasons.Count == 0 ? null : string.Join(" ", partialReasons);
            Dictionary<string, EventSourceInfo> snapshot = new(StringComparer.Ordinal);
            foreach (var pair in entries)
            {
                var entry = pair.Value;
                bool hasEvidence = entry.Provider is not null || entry.Mutations.Any(change => change.Actor is not null);
                EventSourceStatus status = !hasEvidence ? EventSourceStatus.Unknown
                    : partialReasons.Count > 0 || entry.Provider is null ? EventSourceStatus.Partial
                    : EventSourceStatus.Complete;
                snapshot[pair.Key] = new(Scope, pair.Key, entry.Hash ?? string.Empty, LoadId, entry.Generation,
                    entry.Provider, entry.ProviderExecutor, Array.AsReadOnly(entry.Mutations.ToArray()), status, reason, entry.Hash is null);
            }
            finished = true;
            return owner.Publish(this, snapshot);
        }
    }

    internal void Abort()
    {
        lock (owner.Sync)
        {
            finished = true;
            entries.Clear();
        }
    }

    private bool CanObserve() => !finished && owner.IsCurrent(this);

    private void Reconcile(IReadOnlyDictionary<string, string> actual)
    {
        if (actual.Count == entries.Values.Count(entry => entry.Hash is not null)
            && actual.All(pair => entries.TryGetValue(pair.Key, out var entry) && StringComparer.Ordinal.Equals(entry.Hash, pair.Value)))
            return;
        AddReason("Unobserved content changes were found between operation boundaries.");
        ApplyDifferences(actual, null, null, failed: false, unknownGap: true);
    }

    private void ApplyDifferences(IReadOnlyDictionary<string, string> after, EventSourceActor? actor,
        EventSourceActor? executor, bool failed, bool unknownGap)
    {
        // Stable key order makes multi-key operations auditable; Sequence tracks actual operation order.
        long operationSequence = ++sequence;
        foreach (string rawKey in entries.Keys.Concat(after.Keys).Distinct(StringComparer.Ordinal).OrderBy(key => key, StringComparer.Ordinal))
        {
            entries.TryGetValue(rawKey, out var entry);
            string? beforeHash = entry?.Hash;
            string? afterHash = after.TryGetValue(rawKey, out string? value) ? value : null;
            if (StringComparer.Ordinal.Equals(beforeHash, afterHash))
                continue;
            if (entry is null)
                entries[rawKey] = entry = new Entry();
            EventSourceMutationKind kind = beforeHash is null ? EventSourceMutationKind.Add
                : afterHash is null ? EventSourceMutationKind.Delete : EventSourceMutationKind.Change;
            EventSourceActor? priorProvider = beforeHash is null ? null : entry.Provider;
            EventSourceActor? priorExecutor = beforeHash is null ? null : entry.ProviderExecutor;
            if (kind == EventSourceMutationKind.Add)
            {
                entry.Generation++;
                entry.Provider = actor;
                entry.ProviderExecutor = executor;
            }
            else if (unknownGap)
            {
                // A gap could hide a delete/re-add; the old provider isn't established for the new content.
                entry.Provider = null;
                entry.ProviderExecutor = null;
            }
            entry.Mutations.Add(new(operationSequence, kind, actor, executor, entry.Generation, beforeHash, afterHash, failed, priorProvider, priorExecutor));
            entry.Hash = afterHash;
        }
    }

    private void AddReason(string reason)
    {
        if (!partialReasons.Contains(reason, StringComparer.Ordinal))
            partialReasons.Add(reason);
    }

    private static Dictionary<string, string> Fingerprints(IReadOnlyDictionary<string, string> values)
        => values.ToDictionary(pair => pair.Key, pair => EventHashes.RootScript(pair.Value), StringComparer.Ordinal);
}
