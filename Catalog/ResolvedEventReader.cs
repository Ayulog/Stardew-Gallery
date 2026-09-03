namespace StardewGallery;

internal sealed record ResolvedEventCandidate(
    ResolvedEvent Resolved,
    Func<string?> CheckPrecondition
);

internal sealed class ResolvedEventReader(
    Func<string, string, bool> isValidLocationEvent,
    Func<string, string[]> parseCommands,
    Func<string, string[]> parseArguments,
    Func<string, string?> loadTranslation)
{
    internal IReadOnlyList<ResolvedEventCandidate> Read(EventAssetSource source)
    {
        List<ResolvedEventCandidate> result = [];
        foreach (EventAssetDefinition definition in source.Definitions)
        {
            string key = definition.RawEventKey;
            string script = definition.Script;
            if (!isValidLocationEvent(key, script) || !EventKey.TryGetId(key, out string id))
                continue;
            if (EventKey.IsPlaceholderScript(script))
                continue;

            EventFragments fragments = EventFragmentCollector.Collect(
                script,
                source.FragmentRootLocationName,
                source.LoadLocationEvents,
                parseCommands,
                parseArguments,
                loadTranslation
            );
            ResolvedEvent resolved = new(
                Identity: new EventIdentity(source.AssetName, id),
                LocationName: source.LaunchLocationName,
                RawEventKey: key,
                ResolvedScript: script,
                Fragments: fragments,
                RootDefinitionHash: EventHashes.RootDefinition(key, script),
                RootScriptHash: EventHashes.RootScript(script)
            );
            result.Add(new ResolvedEventCandidate(resolved, () => source.CheckPrecondition(key)));
        }
        return result;
    }
}
