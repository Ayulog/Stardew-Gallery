namespace StardewGallery;

internal sealed record LegacyHistoryProjection(
    ObservedVariant Variant,
    VariantObservationSummary Observation,
    KnownSeenEvidence Seen
);

internal static class LegacyHistoryAdapter
{
    internal static LegacyHistoryProjection From(WatchedEventSnapshot snapshot)
    {
        EventIdentity identity = new(snapshot.AssetName, snapshot.EventId);
        string rootScriptHash = EventHashes.RootScript(snapshot.RootScript);
        string rootDefinitionHash = EventHashes.RootDefinition(snapshot.EventKey, snapshot.RootScript);
        HistoricalPlaybackBundle playback = new(
            snapshot.RootScript,
            CopyAssets(snapshot.EventAssets),
            CopyStrings(snapshot.Translations),
            snapshot.Locale,
            snapshot.Fingerprint);

        ObservedVariantKey key = new(identity, rootDefinitionHash, snapshot.Fingerprint);
        ObservedVariant variant = new(key, snapshot.EventKey, rootScriptHash, playback);
        VariantObservationSummary observation = new(
            key,
            snapshot.FirstWatchedAt,
            snapshot.LastWatchedAt,
            snapshot.LocationName,
            snapshot.Locale);
        KnownSeenEvidence seen = new(identity.EventId, identity, KnownSeenSource.LegacyCapturedVariant);
        return new LegacyHistoryProjection(variant, observation, seen);
    }

    private static IReadOnlyDictionary<string, Dictionary<string, string>> CopyAssets(
        IReadOnlyDictionary<string, Dictionary<string, string>> source)
    {
        Dictionary<string, Dictionary<string, string>> result = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string asset, Dictionary<string, string> entries) in source)
        {
            Dictionary<string, string> copy = new(StringComparer.Ordinal);
            foreach ((string key, string value) in entries)
                copy[key] = value;
            result[asset] = copy;
        }
        return result;
    }

    private static IReadOnlyDictionary<string, string> CopyStrings(
        IReadOnlyDictionary<string, string> source)
    {
        Dictionary<string, string> result = new(StringComparer.Ordinal);
        foreach ((string key, string value) in source)
            result[key] = value;
        return result;
    }
}
