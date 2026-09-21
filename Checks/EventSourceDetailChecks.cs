using System.Text.Json;
using StardewGallery;

internal static class EventSourceDetailChecks
{
    internal static void Run()
    {
        int checks = 0;
        void Check(bool condition, string message) { checks++; if (!condition) throw new InvalidOperationException(message); }
        string Render(string key, object? args)
        {
            string text = key;
            if (args is not null)
                text += ": " + string.Join("; ", args.GetType().GetProperties().Select(property => property.Name + "=" + property.GetValue(args)));
            return text;
        }
        var provider = new EventSourceActor("Fixture.StoryPack", "Story Pack", "1.0");
        var framework = new EventSourceActor("Pathoschild.ContentPatcher", "Content Patcher", "2.9.1");
        var translator = new EventSourceActor("Fixture.Translation", "Translation Pack", "2.0");
        var info = new EventSourceInfo(new EventSourceScope("Data/Events/Town", "zh-CN", 0, "test"), "1/f Abigail 500",
            EventHashes.RootScript("script"), 2, 1, provider, framework,
            new[] { new EventSourceMutation(1, EventSourceMutationKind.Change, translator, framework, 1, "before", "after", true) },
            EventSourceStatus.Partial, "Private adapter exception text must not appear in the product UI");
        var display = EventSourcePresentation.Build(info, EventSourceDisplayState.Ready, Render);
        Check(display.Summary.Contains("Story Pack") && !display.Summary.Contains("Pathoschild"), "Summary describes the provider, not its executing framework");
        Check(display.Status == "source.partial", "Partial evidence remains visibly incomplete");
        Check(display.Details.Any(line => line.Contains("Fixture.Translation")) && display.Details.Any(line => line == "source.failed"), "Ordered modifier identity and retained-error warning are exposed");
        Check(display.Details.Any(line => line.Contains("Pathoschild.ContentPatcher")), "Framework identity is available in expanded details");
        Check(!display.Details.Any(line => line.Contains("Private adapter exception")), "Internal errors aren't injected into player-facing source details");
        Check(display.Details.Any(line => line.Contains("1/f Abigail 500")) && display.Details.Any(line => line.Contains("zh-CN")), "Details preserve raw key and actual asset locale");
        Check(display.Details.Contains("source.scope"), "Main-definition scope does not claim verified branch provenance");
        display = EventSourcePresentation.Build(info with { Status = EventSourceStatus.Complete, Provider = EventSourceActor.GameBase, ProviderExecutor = null, Mutations = Array.Empty<EventSourceMutation>() }, EventSourceDisplayState.Ready, Render);
        Check(display.Summary.Contains("source.game") && display.Status == "source.complete", "Observed game baseline is labeled game content");
        display = EventSourcePresentation.Build(info with { ScriptMatches = false }, EventSourceDisplayState.Ready, Render);
        Check(display.Summary.Contains("source.unknown") && !display.Summary.Contains("Story Pack"), "Mismatched current script cannot display the earlier provider");
        foreach (var state in new[] { EventSourceDisplayState.Disabled, EventSourceDisplayState.RestartRequired, EventSourceDisplayState.Unavailable })
        {
            display = EventSourcePresentation.Build(info, state, Render);
            Check(!display.Summary.Contains("Story Pack") && !display.Details.Any(line => line.Contains("Fixture.StoryPack")), "Runtime state suppresses irrelevant prior evidence: " + state);
        }
        display = EventSourcePresentation.Build(null, EventSourceDisplayState.Ready, Render);
        Check(display.Summary.Contains("source.unknown") && display.Status == "source.missing", "Missing evidence is not assumed vanilla");

        var resolved = new ResolvedEvent(new EventIdentity("Data/Events/Town", "1"), "Town", info.RawEventKey, "script",
            new EventFragments([], []), EventHashes.RootDefinition(info.RawEventKey, "script"), EventHashes.RootScript("script"));
        var dictionary = new Dictionary<string, string> { [info.RawEventKey] = "script", ["1/f Abigail 1000"] = "other variant" };
        int calls = 0;
        EventSourceInfo Lookup(IReadOnlyDictionary<string, string> values, string asset, string key, string script)
        {
            calls++;
            Check(ReferenceEquals(values, dictionary) && key == info.RawEventKey && script == "script", "Lookup receives the actual dictionary and exact selected variant");
            return info;
        }
        Check(EventSourceDetailLookup.Read(resolved, "data\\events\\town", dictionary, Lookup) == info && calls == 1, "Normalized matching asset reads observed evidence");
        Check(EventSourceDetailLookup.Read(resolved, "Data/Events/Beach", dictionary, Lookup).Status == EventSourceStatus.Unknown && calls == 1, "Same ID in another asset cannot share provenance");
        dictionary[info.RawEventKey] = "changed after catalog scan";
        Check(EventSourceDetailLookup.Read(resolved, resolved.AssetName, dictionary, Lookup).Status == EventSourceStatus.Unknown && calls == 1, "Stale catalog script never asks index for an old matching fingerprint");
        dictionary.Remove(info.RawEventKey);
        Check(EventSourceDetailLookup.Read(resolved, resolved.AssetName, dictionary, Lookup).Status == EventSourceStatus.Unknown && calls == 1, "A missing raw key cannot fall back to another condition variant");
        Console.WriteLine($"Event source detail checks passed: {checks}");
    }
}
