namespace StardewGallery;

internal enum EventSourceDisplayState { Ready, Disabled, RestartRequired, Unavailable }

internal sealed record EventSourceDisplay(string Summary, string Status, IReadOnlyList<string> Details)
{
    // Runtime completeness and observed edits, separate from provider and file-origin claims.
    internal IReadOnlyList<string> EvidenceDetails { get; init; } = [];
}

/// <summary>Player-facing source text; private adapter errors remain in the SMAPI log.</summary>
internal static class EventSourcePresentation
{
    internal static EventSourceDisplay Build(EventSourceInfo? info, EventSourceDisplayState state,
        Func<string, object?, string> translate)
    {
        string Text(string key, object? args = null) => translate(key, args);
        string Actor(EventSourceActor actor) => actor.IsGameBase ? Text("source.game")
            : Text("source.actor", new { name = actor.Name, id = actor.UniqueId, version = actor.Version ?? Text("source.version-unknown") });
        List<string> details = new();
        List<string> evidence = new();
        void AddEvidence(string line) { details.Add(line); evidence.Add(line); }
        string status;
        string provider;
        if (state != EventSourceDisplayState.Ready)
        {
            string key = state switch
            {
                EventSourceDisplayState.Disabled => "disabled",
                EventSourceDisplayState.RestartRequired => "restart",
                _ => "unavailable"
            };
            provider = status = Text("source." + key);
            details.Add(Text("source." + key + "-help"));
        }
        else
        {
            // Never present an observed provider as the source of a different current script.
            bool matches = info?.ScriptMatches == true && !info.IsDeleted;
            EventSourceActor? actor = matches ? info?.Provider : null;
            provider = actor is null ? Text("source.unknown") : actor.IsGameBase ? Text("source.game") : actor.Name;
            status = Text(info?.Status == EventSourceStatus.Complete && matches ? "source.complete"
                : info?.Status == EventSourceStatus.Partial ? "source.partial" : "source.missing");
            details.Add(Text("source.provider", new { provider = actor is null ? Text("source.unknown") : Actor(actor) }));
            AddEvidence(status);
            if (info?.Status != EventSourceStatus.Complete || !matches)
                AddEvidence(Text(info?.Status == EventSourceStatus.Partial ? "source.partial-help" : "source.missing-help"));
            if (matches && info?.ProviderExecutor is { } executor && executor.UniqueId != actor?.UniqueId)
                details.Add(Text("source.executor", new { executor = Actor(executor) }));
            AddEvidence(Text("source.scope"));
            if (info is not null)
            {
                AddEvidence(Text("source.modifications"));
                if (info.Mutations.Count == 0) AddEvidence(Text("source.no-modifications"));
                foreach (EventSourceMutation mutation in info.Mutations)
                {
                    string kind = Text(mutation.Kind switch
                    {
                        EventSourceMutationKind.Add => "source.kind-add",
                        EventSourceMutationKind.Delete => "source.kind-delete",
                        _ => "source.kind-change"
                    });
                    AddEvidence(Text("source.change", new
                    {
                        sequence = mutation.Sequence,
                        kind,
                        actor = mutation.Actor is null ? Text("source.unknown") : Actor(mutation.Actor)
                    }));
                    if (mutation.Executor is { } editingFramework && editingFramework.UniqueId != mutation.Actor?.UniqueId)
                        AddEvidence(Text("source.executor", new { executor = Actor(editingFramework) }));
                    if (mutation.Failed) AddEvidence(Text("source.failed"));
                }
                details.Add(Text("source.asset", new { asset = info.Scope.AssetName }));
                details.Add(Text("source.key", new { key = info.RawEventKey }));
                details.Add(Text("source.locale", new { locale = info.Scope.Locale.Length == 0 ? Text("source.locale-default") : info.Scope.Locale }));
                if (info.LoadId > 0)
                    details.Add(Text("source.generation", new { load = info.LoadId, generation = info.DefinitionGeneration }));
            }
        }
        return new EventSourceDisplay(Text("source.summary", new { provider }), status, details.AsReadOnly()) { EvidenceDetails = evidence.AsReadOnly() };
    }
}
