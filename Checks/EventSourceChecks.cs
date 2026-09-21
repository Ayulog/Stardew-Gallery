using StardewGallery;

internal static class EventSourceChecks
{
    internal static void Run()
    {
        int checks = 0;
        void Check(bool condition, string name) { checks++; if (!condition) throw new InvalidOperationException(name); }
        Dictionary<string, string> Data(params (string Key, string Script)[] pairs)
            => pairs.ToDictionary(pair => pair.Key, pair => pair.Script, StringComparer.Ordinal);
        EventSourceActor a = new("Example.A", "Content Pack A", "1.0");
        EventSourceActor b = new("Example.B", "Translation B", "2.0");
        EventSourceActor c = new("Example.C", "Compatibility C", "3.0");
        EventSourceActor framework = new("Pathoschild.ContentPatcher", "Content Patcher", "2.9.1");
        EventSourceScope town = new("Data/Events/Town", "en", 0, "save:1");
        EventSourceIndex index = new();

        Check(town == new EventSourceScope(" data\\events\\town ", "EN", 0, "save:1"), "Asset separators and asset/locale casing normalize");
        Check(default(EventSourceScope) == new EventSourceScope("", "", 0, ""), "Default scopes have consistent equality");
        Check(default(EventSourceScope).GetHashCode() == new EventSourceScope("", "", 0, "").GetHashCode(), "Default scope hash follows equality");
        Check(index.Lookup(town, "1", "vanilla").Status == EventSourceStatus.Unknown, "Missing evidence is never assumed vanilla");

        var baseline = Data(("1/f Abigail 500", "game"));
        var load = index.BeginLoad(town);
        load.RecordBaseline(baseline, EventSourceActor.GameBase);
        Check(index.Lookup(town, "1/f Abigail 500", "game").Status == EventSourceStatus.Unknown, "A baseline isn't published before successful completion");
        Check(load.Complete(baseline), "A fully observed game load publishes");
        var game = index.Lookup(town, "1/f Abigail 500", "game");
        Check(game.Status == EventSourceStatus.Complete && game.Provider?.IsGameBase == true, "Observed raw baseline is game base");
        Check(game.ScriptHash == EventHashes.RootScript("game") && game.ScriptHash.Length == 64, "Evidence stores SHA256 of the exact script");
        Check(game.Mutations.Count == 0, "The baseline isn't an editor mutation");

        // An accepted dictionary loader is the provider even if its text matches the game.
        load = index.BeginLoad(town);
        load.RecordBaseline(baseline, a, framework);
        Check(load.Complete(baseline), "An accepted mod loader publishes");
        var loaded = index.Lookup(town, "1/f Abigail 500", "game");
        Check(loaded.Provider == a && loaded.ProviderExecutor == framework && loaded.Status == EventSourceStatus.Complete, "Loader package and executing framework remain separate");

        load = index.BeginLoad(town);
        var empty = Data();
        var first = Data(("123/f Abigail 500", "introduced by A"));
        var translated = Data(("123/f Abigail 500", "changed by B"));
        var overwritten = Data(("123/f Abigail 500", "overwritten by C"));
        load.RecordBaseline(empty, EventSourceActor.GameBase);
        load.ObserveEdit(empty, first, a, framework);
        load.ObserveEdit(first, translated, b, framework);
        load.ObserveEdit(translated, overwritten, c);
        Check(load.Complete(overwritten), "A/B/C edit chain publishes");
        var chain = index.Lookup(town, "123/f Abigail 500", "overwritten by C");
        Check(chain.Provider == a && chain.ProviderExecutor == framework, "Changes and overwrites preserve the first introduced provider");
        Check(chain.Mutations.Select(item => item.Actor).SequenceEqual(new[] { a, b, c }), "The observed edit chain preserves execution order");
        Check(chain.Mutations.Select(item => item.Kind).SequenceEqual(new[] { EventSourceMutationKind.Add, EventSourceMutationKind.Change, EventSourceMutationKind.Change }), "Addition and later overwrites have distinct mutation kinds");
        Check(chain.Mutations.Select(item => item.Sequence).SequenceEqual(new long[] { 1, 2, 3 }), "Operation order is recorded explicitly");
        Check(chain.Mutations[1].BeforeHash == EventHashes.RootScript("introduced by A") && chain.Mutations[2].AfterHash == EventHashes.RootScript("overwritten by C"), "Chain records before/after fingerprints");
        Check(chain.DefinitionGeneration == 1 && chain.Status == EventSourceStatus.Complete, "Initial added definition is generation one");

        load = index.BeginLoad(town);
        load.RecordBaseline(first, a, framework);
        load.ObserveEdit(first, empty, b);
        load.ObserveEdit(empty, overwritten, c);
        Check(load.Complete(overwritten), "Deletion/readdition publishes");
        var readded = index.Lookup(town, "123/f Abigail 500", "overwritten by C");
        Check(readded.Mutations[0].PriorProvider == a && readded.Mutations[0].PriorProviderExecutor == framework, "Old generation retains the accepted loader identity after deletion/readdition");
        Check(readded.Provider == c && readded.DefinitionGeneration == 2, "Readded definition has a new provider and generation");
        Check(readded.Mutations[0].Kind == EventSourceMutationKind.Delete && readded.Mutations[0].DefinitionGeneration == 1, "Deletion remains tied to the old generation");
        Check(readded.Mutations[1].Kind == EventSourceMutationKind.Add && readded.Mutations[1].DefinitionGeneration == 2, "Readdition is auditable after deletion");

        load = index.BeginLoad(town);
        load.RecordBaseline(first, a);
        load.ObserveEdit(first, new Dictionary<string, string>(first), b);
        load.ObserveEdit(first, new Dictionary<string, string>(first), null);
        Check(load.Complete(first), "Same-value load publishes");
        Check(index.Lookup(town, "123/f Abigail 500", "introduced by A").Status == EventSourceStatus.Complete, "No-op editor with missing identity cannot reduce complete provenance");
        Check(index.Lookup(town, "123/f Abigail 500", "introduced by A").Mutations.Count == 0, "Same-value writes and group-local revert don't create a modifier");

        load = index.BeginLoad(town);
        load.RecordBaseline(first, a);
        load.ObserveEdit(first, translated, b, framework, failed: true);
        Check(load.Complete(translated), "Residual content after an editor error can be published");
        var failed = index.Lookup(town, "123/f Abigail 500", "changed by B");
        Check(failed.Status == EventSourceStatus.Partial && failed.Mutations.Single().Failed, "Editor error retains actual residual changes with partial evidence");
        Check(failed.Provider == a && failed.Mutations.Single().Actor == b, "A failed editor isn't mistaken for a rejected loader");

        EventSourceScope[] isolatedScopes =
        {
            town,
            new("Data/Events/Beach", "en", 0, "save:1"),
            new("Data/Events/Town", "zh", 0, "save:1"),
            new("Data/Events/Town", "en", 1, "save:1"),
            new("Data/Events/Town", "en", 0, "save:2")
        };
        foreach (var scope in isolatedScopes)
        {
            load = index.BeginLoad(scope);
            var before = Data(("123/f Abigail 500", scope.AssetName + scope.Locale + scope.ScreenId + scope.ContextId), ("123/f Abigail 1000", "other condition"));
            var after = Data(("123/f Abigail 500", scope.AssetName + scope.Locale + scope.ScreenId + scope.ContextId), ("123/f Abigail 1000", "second condition changed"));
            load.RecordBaseline(before, a);
            load.ObserveEdit(before, after, b);
            Check(load.Complete(after), "Independent scope publishes");
        }
        foreach (var scope in isolatedScopes)
        {
            var untouched = index.Lookup(scope, "123/f Abigail 500", scope.AssetName + scope.Locale + scope.ScreenId + scope.ContextId);
            Check(untouched.Status == EventSourceStatus.Complete && untouched.Mutations.Count == 0, "Different assets/languages/screens/save contexts retain independent evidence");
            Check(index.Lookup(scope, "123/f Abigail 1000", "second condition changed").Mutations.Single().Actor == b, "Same ID with different complete condition keys cannot share modifications");
            Check(index.Lookup(scope, "123/F Abigail 1000", "second condition changed").Status == EventSourceStatus.Unknown, "Raw event keys remain case sensitive");
        }

        var old = index.BeginLoad(town);
        old.RecordBaseline(first, a);
        var newer = index.BeginLoad(town);
        newer.RecordBaseline(translated, b);
        Check(newer.Complete(translated) && !old.Complete(first), "Out-of-order completion cannot replace newer evidence");
        Check(index.Lookup(town, "123/f Abigail 500", "changed by B").Provider == b, "Latest load remains the published provider");
        Check(!newer.Complete(translated), "A load publishes at most once");

        var inFlight = index.BeginLoad(town);
        inFlight.RecordBaseline(first, a);
        index.InvalidateAsset("data\\events\\town");
        Check(!inFlight.Complete(first), "Asset invalidation revokes in-flight load publication");
        Check(index.Lookup(town, "123/f Abigail 500", "introduced by A").Status == EventSourceStatus.Unknown, "Invalidated evidence is unavailable");
        Check(index.Lookup(isolatedScopes[2], "123/f Abigail 1000", "second condition changed").Status == EventSourceStatus.Unknown, "Asset invalidation removes all locale/context variants");
        Check(index.Lookup(isolatedScopes[1], "123/f Abigail 1000", "second condition changed").Status == EventSourceStatus.Complete, "Asset invalidation preserves other assets");
        inFlight = index.BeginLoad(town);
        inFlight.RecordBaseline(first, a);
        index.Clear();
        Check(!inFlight.Complete(first), "Clear revokes in-flight loads across save/title/locale boundaries");

        load = index.BeginLoad(town);
        load.RecordBaseline(first, EventSourceActor.GameBase);
        Check(load.Complete(translated), "Final content mismatch is retained as partial observation");
        var mismatch = index.Lookup(town, "123/f Abigail 500", "changed by B");
        Check(mismatch.Status != EventSourceStatus.Complete && mismatch.Provider is null, "Unobserved final change cannot keep a game-base attribution");
        Check(mismatch.Mutations.Single().Actor is null, "Unobserved changes aren't blamed on the last known editor");

        load = index.BeginLoad(town);
        load.RecordBaseline(first, a);
        load.ObserveEdit(translated, overwritten, c);
        Check(load.Complete(overwritten), "A gap before an editor preserves the subsequent observable edit");
        var gap = index.Lookup(town, "123/f Abigail 500", "overwritten by C");
        Check(gap.Provider is null && gap.Status == EventSourceStatus.Partial && gap.Mutations.Count == 2, "Before-boundary gaps invalidate provider while retaining known later modification");
        Check(gap.Mutations[0].Actor is null && gap.Mutations[1].Actor == c, "Unobserved mutation precedes the real observed editor");
        var bypass = index.Lookup(town, "123/f Abigail 500", "changed after publication");
        Check(bypass.Status == EventSourceStatus.Partial && bypass.Provider is null && !bypass.ScriptMatches, "Post-load direct cache mutation cannot receive stale provenance");
        Check(index.Lookup(town, "123/f Abigail 500", "overwritten by C").ScriptMatches, "A mismatched query doesn't corrupt the original observation");

        load = index.BeginLoad(town);
        load.RecordBaseline(first, EventSourceActor.GameBase, fullyObserved: false);
        Check(load.Complete(first), "Incomplete baseline can complete without making a vanilla claim");
        Check(index.Lookup(town, "123/f Abigail 500", "introduced by A").Provider is null, "Late observation cannot assert game-base origin");
        load = index.BeginLoad(town);
        load.ObserveEdit(first, translated, b);
        Check(load.Complete(translated), "An observed editor without baseline still supplies partial evidence");
        var late = index.Lookup(town, "123/f Abigail 500", "changed by B");
        Check(late.Provider is null && late.Mutations.Single().Actor == b && late.Status == EventSourceStatus.Partial, "Late attachment records known editor without guessing initial provider");
        load = index.BeginLoad(town);
        load.RecordBaseline(first, a);
        load.Abort();
        Check(!load.Complete(first) && index.Lookup(town, "123/f Abigail 500", "introduced by A").Status == EventSourceStatus.Unknown, "Failed overall loads aren't published");

        // Caller dictionaries are read-only inputs and are never retained.
        var supplied = Data(("raw", "before"));
        load = index.BeginLoad(town);
        load.RecordBaseline(supplied, a);
        supplied["raw"] = "bypass";
        Check(load.Complete(supplied), "Copied fingerprints detect mutation of the original baseline dictionary");
        Check(index.Lookup(town, "raw", "bypass").Provider is null && supplied["raw"] == "bypass", "Observations preserve caller data and don't retain mutable baseline aliases");

        Console.WriteLine($"Event source checks passed: {checks}");
    }
}
