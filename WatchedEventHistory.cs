using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace StardewGallery;

internal sealed class WatchedEventHistory(IMonitor monitor, Func<bool> debugDiagnostics)
{
    private readonly Dictionary<EventIdentity, Dictionary<ObservedVariantKey, WatchedEventSnapshot>> entries = [];
    private Event? observedEvent;
    private WatchedEventSnapshot? pendingSnapshot;
    private LegacyHistoryStore? legacyStore;
    private HistoryRepository? repository;
    private bool sqliteDegraded;

    internal void AttachPersistence(LegacyHistoryStore store, HistoryRepository? repo)
    {
        legacyStore = store;
        repository = repo;
        sqliteDegraded = false;
    }

    internal void DetachPersistence()
    {
        legacyStore = null;
        repository = null;
        sqliteDegraded = false;
    }

    internal void Load()
    {
        entries.Clear();
        observedEvent = null;
        pendingSnapshot = null;
        try
        {
            IReadOnlyList<WatchedEventSnapshot> saved = legacyStore?.Load() ?? [];
            if (repository is not null && !sqliteDegraded)
            {
                try
                {
                    repository.ImportLegacy(saved);
                }
                catch (Exception error)
                {
                    sqliteDegraded = true;
                    monitor.Log($"SQLite 历史迁移失败，本会话降级为 legacy：{error.Message}", LogLevel.Error);
                }
            }
            foreach (WatchedEventSnapshot snapshot in saved)
                Add(snapshot);
        }
        catch (Exception error)
        {
            monitor.Log($"已观看事件版本记录无法读取，本次不会覆盖原记录：{error.Message}", LogLevel.Error);
        }
    }

    internal void Clear()
    {
        entries.Clear();
        observedEvent = null;
        pendingSnapshot = null;
    }

    internal IReadOnlyList<WatchedEventSnapshot> Get(EventIdentity identity)
        => CollapseForCompatibility(identity);

    internal IReadOnlyList<WatchedEventSnapshot> Get(GalleryEvent entry)
        => Get(entry.Resolved.Identity);

    // UI compatibility projection: collapse same EventIdentity + PlaybackHash into one snapshot,
    // selecting the most recently observed (by LastWatchedAt) representative. Sorting stays
    // LastWatchedAt descending. This is NOT domain dedup — full ObservedVariantKey variants are kept.
    private IReadOnlyList<WatchedEventSnapshot> CollapseForCompatibility(EventIdentity identity)
    {
        if (!entries.TryGetValue(identity, out Dictionary<ObservedVariantKey, WatchedEventSnapshot>? variants))
            return [];
        List<WatchedEventSnapshot> collapsed = [];
        foreach ((string playbackHash, WatchedEventSnapshot latest) in variants.Values
            .GroupBy(snapshot => snapshot.Fingerprint, StringComparer.Ordinal)
            .Select(group => (group.Key, group.OrderByDescending(snapshot => snapshot.LastWatchedAt).First())))
            collapsed.Add(latest);
        collapsed.Sort((left, right) => right.LastWatchedAt.CompareTo(left.LastWatchedAt));
        return collapsed;
    }

    internal void Update(bool replayActive)
    {
        Event? current = Game1.CurrentEvent;
        if (replayActive)
        {
            observedEvent = null;
            pendingSnapshot = null;
            return;
        }
        if (ReferenceEquals(current, observedEvent))
            return;

        CommitPending();
        if (current is null)
        {
            observedEvent = null;
            return;
        }

        observedEvent = current;
        if (!TryCapture(current, out WatchedEventSnapshot? snapshot, out string? reason))
        {
            if (debugDiagnostics() && reason is not null)
                monitor.Log($"未记录自然事件版本：{reason}", LogLevel.Debug);
            return;
        }
        pendingSnapshot = snapshot;
    }

    private bool TryCapture(Event current, out WatchedEventSnapshot? snapshot, out string? reason)
    {
        snapshot = null;
        reason = null;
        string assetName = current.fromAssetName;
        string eventId = current.id;
        GameLocation? location = Game1.currentLocation;
        if (location is null || string.IsNullOrWhiteSpace(assetName) || string.IsNullOrWhiteSpace(eventId)
            || !Game1.content.DoesAssetExist<Dictionary<string, string>>(assetName))
            return false;

        Dictionary<string, string> asset = Game1.content.Load<Dictionary<string, string>>(assetName);
        List<KeyValuePair<string, string>> matching = asset
            .Where(pair => EventKey.TryGetId(pair.Key, out string id) && id == eventId
                && Event.ParseCommands(pair.Value).SequenceEqual(current.eventCommands))
            .ToList();
        if (matching.Count == 0)
        {
            reason = $"地点={location.NameOrUniqueName}，事件={eventId} 的实际命令与当前资产候选均不一致。";
            return false;
        }

        KeyValuePair<string, string> match;
        if (matching.Count == 1)
        {
            match = matching[0];
        }
        else
        {
            List<string> candidateRawKeys = matching.Select(pair => pair.Key).ToList();
            if (!ObservedVariantSelector.TrySelect(candidateRawKeys,
                key => location.checkEventPrecondition(key, check_seen: false), out int selectedIndex))
            {
                reason = $"地点={location.NameOrUniqueName}，事件={eventId} 存在多个相同脚本的候选，且无法根据当前状态确认实际定义。";
                return false;
            }
            match = matching[selectedIndex];
        }

        Dictionary<string, Dictionary<string, string>> eventAssets = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> translations = new(StringComparer.Ordinal);
        if (!CollectFragments(match.Value, location.Name, eventAssets, translations))
        {
            reason = $"地点={location.NameOrUniqueName}，事件={eventId} 存在无法读取的事件片段。";
            return false;
        }
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> fingerprintAssets = eventAssets
            .ToDictionary(pair => pair.Key, pair => (IReadOnlyDictionary<string, string>)pair.Value, StringComparer.OrdinalIgnoreCase);
        string fingerprint = EventKey.GetSnapshotFingerprint(match.Value, fingerprintAssets, translations);
        DateTimeOffset now = DateTimeOffset.Now;
        snapshot = new WatchedEventSnapshot(location.NameOrUniqueName, assetName, eventId, match.Key, match.Value,
            eventAssets, translations, LocalizedContentManager.CurrentLanguageCode.ToString(), fingerprint, now, now);
        return true;
    }

    private void CommitPending()
    {
        WatchedEventSnapshot? snapshot = pendingSnapshot;
        pendingSnapshot = null;
        if (snapshot is null || !Game1.player.eventsSeen.Contains(snapshot.EventId))
            return;

        if (repository is not null && !sqliteDegraded)
        {
            try
            {
                LegacyHistoryProjection projection = LegacyHistoryAdapter.From(snapshot);
                repository.UpsertObservation(projection.Variant, projection.Observation);
            }
            catch (Exception error)
            {
                sqliteDegraded = true;
                monitor.Log($"SQLite 写入失败，本会话降级为 legacy：{error.Message}", LogLevel.Error);
            }
        }

        bool isNew = Add(snapshot);
        if (legacyStore is not null)
        {
            bool ok = legacyStore.TrySave(SnapshotList());
            if (!ok)
                monitor.Log("legacy watched-event-versions 写入失败（不影响当前会话）。", LogLevel.Debug);
        }
        if (isNew && debugDiagnostics())
            monitor.Log($"已记录完整观看事件版本：地点={snapshot.LocationName}，事件={snapshot.EventId}，指纹={snapshot.Fingerprint[..12]}。", LogLevel.Debug);
    }

    private IReadOnlyList<WatchedEventSnapshot> SnapshotList()
        => entries.Values.SelectMany(value => value.Values).ToList();

    private static bool CollectFragments(string rootScript, string rootLocation,
        Dictionary<string, Dictionary<string, string>> eventAssets, Dictionary<string, string> translations)
    {
        List<(string Script, string Location)> pending = [(rootScript, rootLocation)];
        HashSet<string> visited = new(StringComparer.Ordinal);
        for (int index = 0; index < pending.Count; index++)
        {
            (string script, string location) = pending[index];
            foreach (string command in Event.ParseCommands(script))
            {
                string[] args = ArgUtility.SplitBySpaceQuoteAware(command);
                if (args.Length > 1 && args[0].Equals("changeLocation", StringComparison.OrdinalIgnoreCase))
                    location = args[1];
                if (!TryGetReference(args, out string key, out bool translation))
                    continue;

                string identity = translation ? "T:" + key : $"E:{location}:{key}";
                if (!visited.Add(identity))
                    continue;
                if (translation)
                {
                    string? translated = Game1.content.LoadStringReturnNullIfNotFound(key);
                    if (translated is null)
                        return false;
                    translations[key] = translated;
                    pending.Add((translated, location));
                    continue;
                }

                GameLocation? target = Game1.getLocationFromName(location);
                string assetName = "Data\\Events\\" + (target?.Name ?? location);
                if (!Game1.content.DoesAssetExist<Dictionary<string, string>>(assetName))
                    return false;
                Dictionary<string, string> data = Game1.content.Load<Dictionary<string, string>>(assetName);
                if (!data.TryGetValue(key, out string? value))
                    return false;
                if (!eventAssets.TryGetValue(assetName, out Dictionary<string, string>? captured))
                    eventAssets[assetName] = captured = new Dictionary<string, string>(StringComparer.Ordinal);
                captured[key] = value;
                pending.Add((value, location));
            }
        }
        return true;
    }

    private static bool TryGetReference(string[] args, out string key, out bool translation)
    {
        key = "";
        translation = false;
        if (args.Length > 1 && args[0].Equals("switchEvent", StringComparison.OrdinalIgnoreCase))
        {
            key = args[1];
            return true;
        }
        if (args.Length <= 1 || !args[0].Equals("fork", StringComparison.OrdinalIgnoreCase))
            return false;
        key = args.Length > 2 ? args[2] : args[1];
        translation = args.Length > 3 && bool.TryParse(args[3], out bool value) && value;
        return true;
    }

    private bool Add(WatchedEventSnapshot snapshot)
    {
        EventIdentity identity = snapshot.Identity;
        if (!entries.TryGetValue(identity, out Dictionary<ObservedVariantKey, WatchedEventSnapshot>? variants))
            entries[identity] = variants = [];

        ObservedVariantKey key = new(
            identity,
            EventHashes.RootDefinition(snapshot.EventKey, snapshot.RootScript),
            snapshot.Fingerprint);

        if (variants.TryGetValue(key, out WatchedEventSnapshot? existing))
            variants[key] = snapshot with { FirstWatchedAt = existing.FirstWatchedAt, LastWatchedAt = snapshot.LastWatchedAt };
        else
            variants[key] = snapshot;

        return existing is null;
    }
}

internal sealed class HistoricalReplayAssets(IModHelper helper)
{
    private WatchedEventSnapshot? active;

    internal void OnAssetRequested(AssetRequestedEventArgs e)
    {
        WatchedEventSnapshot? snapshot = active;
        if (snapshot is null)
            return;
        Dictionary<string, string>? eventEntries = snapshot.EventAssets
            .FirstOrDefault(pair => e.NameWithoutLocale.IsEquivalentTo(pair.Key)).Value;
        if (eventEntries is not null)
            e.Edit(asset => Copy(eventEntries, asset.AsDictionary<string, string>().Data), AssetEditPriority.Late);
        foreach ((string translationKey, string value) in snapshot.Translations)
        {
            int separator = translationKey.LastIndexOf(':');
            if (separator <= 0 || !e.NameWithoutLocale.IsEquivalentTo(translationKey[..separator]))
                continue;
            string key = translationKey[(separator + 1)..];
            e.Edit(asset => asset.AsDictionary<string, string>().Data[key] = value, AssetEditPriority.Late);
        }
    }

    internal void Activate(WatchedEventSnapshot snapshot)
    {
        active = snapshot;
        Invalidate(snapshot);
    }

    internal void Clear()
    {
        WatchedEventSnapshot? previous = active;
        active = null;
        if (previous is not null)
            Invalidate(previous);
    }

    private void Invalidate(WatchedEventSnapshot snapshot)
    {
        foreach (string asset in snapshot.EventAssets.Keys)
            helper.GameContent.InvalidateCache(asset);
        foreach (string key in snapshot.Translations.Keys)
        {
            int separator = key.LastIndexOf(':');
            if (separator > 0)
                helper.GameContent.InvalidateCache(key[..separator]);
        }
    }

    private static void Copy(IReadOnlyDictionary<string, string> source, IDictionary<string, string> target)
    {
        foreach ((string key, string value) in source)
            target[key] = value;
    }
}
