namespace StardewGallery;

internal sealed record PrerequisiteRule(string Id, IReadOnlyList<string> Triggers, string Condition,
    bool HostOnly, bool Once, IReadOnlyList<string> Actions, string? SkipCondition = null);

internal sealed record MarkerAction(string RuleId, string Player, bool SetsMarker, IReadOnlyList<string> Triggers,
    string Condition, bool HostOnly, bool Once, IReadOnlyList<string> MailIds, string? SkipCondition = null);

internal sealed record PrerequisiteEvent(string EventId, IReadOnlyList<MarkerAction> Sources,
    IReadOnlyList<GalleryEvent> InternalSources, IReadOnlyList<GalleryEvent> Dependents)
{
    internal EventIdentity Identity => new("Data/TriggerActions", EventId);
    internal IReadOnlyList<string> RelatedNpcNames { get; init; } = [];
}

internal static class PrerequisiteCatalog
{
    internal static IReadOnlyList<PrerequisiteEvent> Build(IEnumerable<PrerequisiteRule> rules,
        GalleryCatalog catalog, Func<string, string[]> splitArguments, ConditionParser parser, Func<string, string[]> parseCommands)
    {
        Dictionary<string, List<MarkerAction>> markers = new(StringComparer.Ordinal);
        foreach (PrerequisiteRule rule in rules.Where(rule => rule.Actions.Any(action => !string.IsNullOrWhiteSpace(action)))
            .DistinctBy(rule => rule.Id, StringComparer.OrdinalIgnoreCase))
        {
            string[][] actions = rule.Actions.Select(splitArguments).ToArray();
            string[] mail = actions.Where(a => a.Length >= 3 && a[0].Equals("AddMail", StringComparison.OrdinalIgnoreCase))
                .Select(a => a[2]).Distinct(StringComparer.Ordinal).ToArray();
            foreach (string[] a in actions)
            {
                if (a.Length < 3 || !a[0].Equals("MarkEventSeen", StringComparison.OrdinalIgnoreCase)
                    || string.IsNullOrWhiteSpace(a[2]) || a.Length >= 4 && a[3].Length > 0 && !bool.TryParse(a[3], out _)) continue;
                string? player = a[1].ToUpperInvariant() switch { "CURRENT" or "0" => "Current", "HOST" or "1" => "Host", "ALL" or "2" => "All", _ => null };
                if (player is null) continue;
                if (!markers.TryGetValue(a[2], out var sources)) markers[a[2]] = sources = [];
                sources.Add(new(rule.Id, player, a.Length == 3 || a[3].Length == 0 || bool.Parse(a[3]), rule.Triggers,
                    rule.Condition, rule.HostOnly, rule.Once, mail, rule.SkipCondition));
            }
        }
        var dependencies = catalog.StoryEntries.SelectMany(story => ConditionEventReferences.Read(parser.ParseRawKey(story.EventKey))
                .Select(id => (Id: id, Story: story)))
            .GroupBy(pair => pair.Id, StringComparer.Ordinal).ToDictionary(group => group.Key,
                group => group.Select(pair => pair.Story).DistinctBy(story => story.Resolved.Identity).ToArray(), StringComparer.Ordinal);
        // Only state-changing internal dependencies are searchable; pure timing/transition records remain hidden.
        var internalSources = catalog.ExcludedEvents.Where(story => story.Kind == StoryKind.Internal
                && dependencies.ContainsKey(story.EventId)
                && (story.ClassificationReason == "internal-offscreen-marker" && parser.ParseRawKey(story.EventKey).Conditions.Count > 0
                    || story.ClassificationReason == "internal-state-or-transition-only" && parseCommands(story.Script).Select(splitArguments)
                        .Any(args => args.Length > 0 && args[0].ToLowerInvariant()
                            is "mail" or "mailtomorrow" or "addmailreceived" or "addquest" or "completequest" or "seteventseen" or "setworldstate")))
            .GroupBy(story => story.EventId, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        HashSet<string> stories = catalog.StoryEntries.Select(entry => entry.EventId).ToHashSet(StringComparer.Ordinal);
        var result = markers.Keys.Concat(internalSources.Keys).Distinct(StringComparer.Ordinal)
            .Where(id => !stories.Contains(id))
            .Select(id => new PrerequisiteEvent(id, markers.TryGetValue(id, out var sources) ? sources : [],
                internalSources.GetValueOrDefault(id) ?? [], dependencies.GetValueOrDefault(id) ?? []))
            .OrderBy(entry => entry.EventId, StringComparer.Ordinal).ToArray();
        Dictionary<string, HashSet<string>> npcs = result.ToDictionary(entry => entry.EventId,
            entry => entry.Dependents.Concat(entry.InternalSources).SelectMany(story => story.RelatedNpcNames).ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);
        var clausesById = result.ToDictionary(entry => entry.EventId, entry => entry.Sources
            .SelectMany(source => SafeGameQuery.TryParse(source.Condition, out var clauses) ? clauses : []).ToArray(), StringComparer.Ordinal);
        foreach (var entry in result)
        foreach (var clause in clausesById[entry.EventId])
        {
            if (clause.Name is "PLAYER_HEARTS" or "PLAYER_FRIENDSHIP_POINTS" or "PLAYER_NPC_RELATIONSHIP"
                && !clause.Arguments[1].Equals("Any", StringComparison.OrdinalIgnoreCase)) npcs[entry.EventId].Add(clause.Arguments[1]);
            if (clause.Name == "PLAYER_HAS_SEEN_EVENT")
                foreach (var story in catalog.StoryEntries.Where(story => clause.Arguments.Skip(1).Contains(story.EventId, StringComparer.Ordinal)))
                    npcs[entry.EventId].UnionWith(story.RelatedNpcNames);
        }
        bool changed;
        do
        {
            changed = false;
            foreach (var entry in result)
            {
                int before = npcs[entry.EventId].Count;
                foreach (var clause in clausesById[entry.EventId])
                {
                    var upstream = clause.Name == "PLAYER_HAS_MAIL" ? result.Where(other => other.Sources.Any(source => source.MailIds.Contains(clause.Arguments[1])))
                        : clause.Name == "PLAYER_HAS_SEEN_EVENT" ? result.Where(other => clause.Arguments.Skip(1).Contains(other.EventId)) : [];
                    foreach (var other in upstream) npcs[entry.EventId].UnionWith(npcs[other.EventId]);
                }
                changed |= before != npcs[entry.EventId].Count;
            }
        } while (changed);
        return result.Select(entry => entry with { RelatedNpcNames = npcs[entry.EventId].OrderBy(name => name, StringComparer.Ordinal).ToArray() }).ToArray();
    }
}
