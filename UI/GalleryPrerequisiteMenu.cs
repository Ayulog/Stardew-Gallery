using StardewValley;

namespace StardewGallery;

internal sealed class GalleryPrerequisiteMenu : GalleryToolMenu
{
    private readonly PrerequisiteEvent entry;
    protected override string Title => Context.I18n.Get("query.kind-prerequisite");
    protected override string? ExtraLabel => Context.I18n.Get("name.edit");
    protected override void Extra() => Context.Navigation.Rename(entry.Identity);
    internal GalleryPrerequisiteMenu(GalleryViewContext context, GalleryPageState state) : base(context, state)
    {
        entry = context.Catalog.FindPrerequisite(state.PrerequisiteId!) ?? throw new ArgumentException("Missing prerequisite.");
        Rows.Add(new(PrerequisitePresentation.Title(entry, context) + "  (ID " + entry.EventId + ")"));
        bool obtained = Game1.player.eventsSeen.Contains(entry.EventId);
        Rows.Add(new(context.I18n.Get(obtained ? "prerequisite.complete" : "prerequisite.incomplete"),
            Status: obtained ? ConditionStatusIcon.Check : ConditionStatusIcon.Cross));
        Rows.Add(new(context.I18n.Get("query.source", new { source = context.SourceLabel(context.Sources.Get(entry.Identity)) })));
        foreach (MarkerAction source in entry.Sources)
        {
            Rows.Add(new(context.I18n.Get(source.SetsMarker ? "prerequisite.set" : "prerequisite.remove", new { rule = source.RuleId })));
            Rows.Add(new(context.I18n.Get("prerequisite.trigger", new { triggers = string.Join(", ", source.Triggers.Select(trigger => Term("trigger", trigger))), player = Term("player", source.Player) })));
            if (source.HostOnly) Rows.Add(new(context.I18n.Get("prerequisite.host"), Status: Game1.IsMasterGame ? ConditionStatusIcon.Check : ConditionStatusIcon.Unknown));
            Rows.Add(new(context.I18n.Get("prerequisite.not-run"), Status: Game1.player.triggerActionsRun.Contains(source.RuleId)
                ? ConditionStatusIcon.Cross : source.HostOnly && !Game1.IsMasterGame ? ConditionStatusIcon.Unknown : ConditionStatusIcon.Check));
            if (source.SkipCondition is not null)
            {
                Rows.Add(new(context.I18n.Get("prerequisite.skip")));
                AddConditions(source.SkipCondition, source.HostOnly && !Game1.IsMasterGame);
            }
            Rows.Add(new(context.I18n.Get("event-detail.requirements")));
            AddConditions(source.Condition, source.HostOnly && !Game1.IsMasterGame);
            if (source.MailIds.Count > 0) Rows.Add(new(context.I18n.Get("prerequisite.mail", new { ids = string.Join(", ", source.MailIds) })));
        }
        foreach (GalleryEvent flow in entry.InternalSources)
        {
            Rows.Add(new(context.Locations.Get(flow.LocationName)));
            foreach (var condition in GalleryConditionPresentation.Build(flow, context.I18n)) AddCondition(condition);
        }
        foreach (GalleryEvent story in entry.Dependents)
            Rows.Add(new(context.I18n.Get("prerequisite.next", new { name = context.Name(story.Resolved.Identity), location = context.Locations.Get(story.LocationName) }),
                () => context.Navigation.OpenEvent(story.Resolved.Identity)));
        RefreshRows(state.Focus >= 0 ? state.Focus : RowId);
    }
    private string Term(string group, string value)
    {
        var text = Context.I18n.Get(group + "." + value.ToLowerInvariant());
        return text.HasValue() ? text.ToString() : value;
    }
    private void AddConditions(string raw, bool unknown)
    {
        var rows = GalleryConditionPresentation.Query(raw, Context.I18n);
        if (rows.Count == 0) Rows.Add(new(Context.I18n.Get("condition.none"), Status: unknown ? ConditionStatusIcon.Unknown : ConditionStatusIcon.Check));
        foreach (var condition in rows) AddCondition(condition, unknown);
    }
    private void AddCondition(ConditionDisplayItem condition, bool unknown = false)
    {
        Rows.Add(new(condition.Description.Replace("≥", ">=").Replace("≤", "<="),
            Status: unknown ? ConditionStatusIcon.Unknown : ConditionRowPresentation.Status(condition.Evaluation)));
        IEnumerable<string> refs = GalleryEventNavigation.References(condition.Expression);
        if (condition.Expression is NativeQueryCondition query && SafeGameQuery.TryParse(query.Query, out var clauses))
            refs = refs.Concat(clauses.Where(clause => clause.Name == "PLAYER_HAS_SEEN_EVENT").SelectMany(clause => clause.Arguments.Skip(1)));
        foreach (string id in refs.Distinct())
        {
            var dependency = StoryDependencyLookup.Find(Context.Catalog, id);
            if (dependency.Prerequisite is not null) Rows.Add(new("ID " + id, () => Context.Navigation.OpenPrerequisite(id)));
            foreach (var story in dependency.Stories) Rows.Add(new(Context.Name(story.Resolved.Identity) + " / " + Context.Locations.Get(story.LocationName), () => Context.Navigation.OpenEvent(story.Resolved.Identity)));
        }
    }
}
