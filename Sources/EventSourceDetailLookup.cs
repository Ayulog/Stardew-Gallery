namespace StardewGallery;

internal static class EventSourceDetailLookup
{
    internal static EventSourceInfo Read(ResolvedEvent resolved, string actualAsset,
        IReadOnlyDictionary<string, string> dictionary,
        Func<IReadOnlyDictionary<string, string>, string, string, string, EventSourceInfo> lookup)
    {
        // A catalog snapshot can outlive an invalidated asset. Match the actual current key AND script
        // before asking the index about it; otherwise an old snapshot could display stale provenance.
        if (!StringComparer.OrdinalIgnoreCase.Equals(EventSourceScope.NormalizeAssetName(actualAsset), resolved.AssetName)
            || !dictionary.TryGetValue(resolved.RawEventKey, out string? script)
            || !StringComparer.Ordinal.Equals(script, resolved.ResolvedScript))
            return Unknown(resolved);
        return lookup(dictionary, actualAsset, resolved.RawEventKey, script);
    }

    internal static EventSourceInfo Unknown(ResolvedEvent resolved)
        => new(new EventSourceScope(resolved.AssetName, "", 0, "unobserved"), resolved.RawEventKey,
            resolved.RootScriptHash, 0, 0, null, null, Array.Empty<EventSourceMutation>(),
            EventSourceStatus.Unknown, null, ScriptMatches: false);
}
