namespace StardewGallery;

internal sealed record EventSourceDisplay(string Summary, string Status, IReadOnlyList<string> Details);

/// <summary>A fork/switchEvent sub-script key and the static declarations matched for it.</summary>
internal sealed record EventBranchOrigin(string AssetName, string Key, EventOriginMatch Origin);

/// <summary>Player-facing original-definition text from static file matching; never alters gallery permissions.</summary>
internal static class EventOriginPresentation
{
    internal static EventSourceDisplay Build(ResolvedEvent resolved, EventOriginMatch origin, Func<string, object?, string> translate,
        IReadOnlyList<EventBranchOrigin>? branches = null)
    {
        string Text(string key, object? args = null) => translate(key, args);
        string Actor(EventSourceActor actor) => ActorText(actor, translate);
        bool incomplete = origin.Status == EventOriginMatchStatus.Incomplete;
        string provider = origin.Provider is { } owner ? owner.IsGameBase ? Text("source.game") : owner.Name
            : Text(origin.Ambiguous ? "source.origin-ambiguous" : "source.unknown");
        string status = Text(origin.Provider is { IsGameBase: true } ? "source.origin-vanilla"
            : incomplete ? "source.origin-incomplete"
            : origin.Provider is not null ? "source.origin-matched"
            : origin.Ambiguous ? "source.origin-ambiguous" : "source.origin-missing");
        List<string> details = [Text("source.origin-provider", new { provider = origin.Provider is { } actor ? Actor(actor) : provider }), status];
        details.Add(incomplete ? Text("source.origin-incomplete-help", new { packs = string.Join(", ", origin.UnreadDependencies.Select(pack => pack.Name)) })
            : Text(origin.Ambiguous ? "source.origin-ambiguous-help" : origin.Provider is null ? "source.origin-unknown-help" : "source.origin-help"));
        EventDefinitionCandidate[] files = origin.Candidates.DistinctBy(candidate => (candidate.Actor.UniqueId, candidate.RelativePath)).ToArray();
        // With a provider, other packs declaring the same asset and ID are listed apart; their versions may not apply now.
        bool Provides(EventDefinitionCandidate candidate) => origin.Provider is not { } known
            || StringComparer.OrdinalIgnoreCase.Equals(candidate.Actor.UniqueId, known.UniqueId);
        AddFiles("source.origin-candidates", files.Where(Provides));
        AddFiles("source.origin-others", files.Where(candidate => !Provides(candidate)));
        details.Add(Text("source.asset", new { asset = resolved.AssetName }));
        details.Add(Text("source.key", new { key = resolved.RawEventKey }));
        bool listedBranches = AddBranches(details, branches ?? [], translate);
        details.Add(Text(listedBranches ? "source.scope-branches" : "source.scope"));
        return new EventSourceDisplay(Text("source.summary", new { provider }), status, details.AsReadOnly());

        void AddFiles(string heading, IEnumerable<EventDefinitionCandidate> candidates)
        {
            EventDefinitionCandidate[] listed = candidates.ToArray();
            if (listed.Length == 0) return;
            details.Add(Text(heading));
            foreach (EventDefinitionCandidate candidate in listed)
                details.Add(Text("source.origin-file", new { provider = candidate.Actor.Name, file = candidate.RelativePath }));
        }
    }

    /// <summary>
    /// Lines describing who declares a Data/TriggerActions rule: the provider (or why there is none), every other
    /// declarer, and packs that only edit the rule. Declarations don't prove which version is active now.
    /// </summary>
    internal static IReadOnlyList<string> BuildRule(EventOriginMatch origin, Func<string, object?, string> translate)
    {
        string Text(string key, object? args = null) => translate(key, args);
        List<string> lines = [Text("prerequisite.rule-provider", new { provider = ProviderText(origin, translate) })];
        if (origin.Status == EventOriginMatchStatus.Incomplete)
            lines.Add(Text("source.origin-incomplete-help", new { packs = string.Join(", ", origin.UnreadDependencies.Select(pack => pack.Name)) }));
        string others = Packs(OtherDeclarers(origin), translate);
        if (others.Length > 0) lines.Add(Text("prerequisite.rule-others", new { packs = others }));
        string editors = Packs(origin.Candidates.Where(candidate => candidate.IsModification).Select(candidate => candidate.Actor), translate);
        if (editors.Length > 0) lines.Add(Text("prerequisite.rule-editors", new { packs = editors }));
        return lines;
    }

    /// <summary>Each fork/switchEvent key once, with the event asset its sub-script is read from.</summary>
    internal static IReadOnlyList<(string Asset, string Key)> BranchAssets(ResolvedEvent resolved)
        => resolved.Fragments.BranchKeys
            .Select(branch => (Asset: branch.OtherLocation is { } location ? "Data/Events/" + location : resolved.AssetName, branch.Key))
            .DistinctBy(branch => (branch.Asset.ToLowerInvariant(), branch.Key)).ToArray();

    /// <summary>Lists each fork/switchEvent sub-script's provider, unread-dependency note, and other declarers.</summary>
    private static bool AddBranches(List<string> details, IReadOnlyList<EventBranchOrigin> branches, Func<string, object?, string> translate)
    {
        if (branches.Count == 0) return false;
        string Text(string key, object? args = null) => translate(key, args);
        details.Add(Text("source.branches"));
        foreach (EventBranchOrigin branch in branches)
        {
            EventOriginMatch origin = branch.Origin;
            details.Add(Text("source.branch", new { key = branch.Key, asset = branch.AssetName, provider = ProviderText(origin, translate) }));
            if (origin.Status == EventOriginMatchStatus.Incomplete)
                details.Add(Text("source.origin-incomplete-help", new { packs = string.Join(", ", origin.UnreadDependencies.Select(pack => pack.Name)) }));
            string others = Packs(OtherDeclarers(origin), translate);
            if (others.Length > 0) details.Add(Text("source.branch-others", new { packs = others }));
        }
        return true;
    }

    private static IEnumerable<EventSourceActor> OtherDeclarers(EventOriginMatch origin)
        => origin.Candidates.Where(candidate => !candidate.IsModification && (origin.Provider is not { } provider
                || !StringComparer.OrdinalIgnoreCase.Equals(candidate.Actor.UniqueId, provider.UniqueId)))
            .Select(candidate => candidate.Actor);

    private static string ProviderText(EventOriginMatch origin, Func<string, object?, string> translate)
    {
        string text = origin.Provider is { } provider ? ActorText(provider, translate)
            : translate(origin.Ambiguous ? "source.origin-ambiguous" : "source.unknown", null);
        return origin.Status == EventOriginMatchStatus.Incomplete ? text + " / " + translate("source.origin-incomplete", null) : text;
    }

    private static string Packs(IEnumerable<EventSourceActor> actors, Func<string, object?, string> translate)
        => string.Join(", ", actors.DistinctBy(actor => actor.UniqueId, StringComparer.OrdinalIgnoreCase).Select(actor => ActorText(actor, translate)));

    private static string ActorText(EventSourceActor actor, Func<string, object?, string> translate) => actor.IsGameBase ? translate("source.game", null)
        : translate("source.actor", new { name = actor.Name, id = actor.UniqueId, version = actor.Version ?? translate("source.version-unknown", null) });
}
