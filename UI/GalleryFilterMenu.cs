namespace StardewGallery;

internal sealed class GalleryFilterMenu : GalleryToolMenu
{
    private sealed record Choice(string? Id, string Label);
    private readonly StorySearchIndex index;
    private QueryFilter draft { get => State.Filter; set => State.Filter = value; }
    private string? picker { get => State.FilterPicker; set => State.FilterPicker = value; }
    private int returnFocus { get => State.FilterReturnFocus; set => State.FilterReturnFocus = value; }
    private readonly List<string> fields = [];
    protected override string Title => Context.I18n.Get(picker is null ? "filter.title" : "filter." + picker);
    protected override string? ApplyLabel => picker is null ? Context.I18n.Get("filter.apply").ToString() : null;
    protected override string? ExtraLabel => Context.I18n.Get(picker is null ? "filter.reset" : "filter.clear");
    internal GalleryFilterMenu(GalleryViewContext context, GalleryPageState state) : base(context, state)
    {
        index = new StorySearchIndex(context.Catalog, e => context.Locations.Get(e.LocationName), context.Characters.Get,
            characterKey: (entry, id) => context.Characters.Key(id, entry), locationKey: e => context.Locations.Key(e.LocationName), sourceText: T);
        Rebuild(state.Focus >= 0 ? state.Focus : RowId);
    }
    private string T(string key) => Context.I18n.Get(key);
    private void Rebuild(int focus = RowId)
    {
        Rows.Clear(); fields.Clear();
        if (picker is not null)
        {
            string field = picker;
            foreach (Choice choice in Choices(field)) Rows.Add(new(choice.Label, () => { Set(field, choice.Id); picker = null; Rebuild(returnFocus); }));
        }
        else
        {
            fields.AddRange(["kind", "npc", "location", "source", "state"]);
            foreach (string field in fields)
            {
                string value = Choices(field).FirstOrDefault(choice => SameId(field, choice.Id, Value(field)))?.Label ?? T("filter.any");
                Rows.Add(new(T("filter." + field) + ": " + value, () =>
                {
                    returnFocus = RowId + fields.IndexOf(field); picker = field;
                    int selected = Array.FindIndex(Choices(field), choice => SameId(field, choice.Id, Value(field)));
                    Rebuild(RowId + Math.Max(0, selected));
                }));
            }
        }
        RefreshRows(focus);
    }
    protected override void Adjust(int direction)
    {
        if (picker is not null || FocusId < RowId || FocusId - RowId >= fields.Count) return;
        string field = fields[FocusId - RowId];
        if (field is "npc" or "location" or "source") { Activate(FocusId); return; }
        var choices = Choices(field); int selected = Array.FindIndex(choices, choice => SameId(field, choice.Id, Value(field)));
        Set(field, choices[Math.Clamp(selected + direction, 0, choices.Length - 1)].Id); Rebuild(FocusId);
    }
    protected override void Apply() => Context.Navigation.ApplyFilters(draft);
    protected override void Extra()
    {
        if (picker is not null) { Set(picker, null); picker = null; Rebuild(returnFocus); }
        else { draft = new(); Rebuild(); }
    }
    public override void HandleControllerBack()
    {
        if (picker is not null) { picker = null; Rebuild(returnFocus); } else base.HandleControllerBack();
    }
    private string? Value(string field) => field switch
    {
        "kind" => ((int)draft.Kind).ToString(), "npc" => draft.Npc, "location" => draft.Location, "source" => draft.Source,
        "state" => ((int)draft.Completion).ToString(), _ => null
    };
    private void Set(string field, string? value)
    {
        int? number = int.TryParse(value, out int parsed) ? parsed : null;
        draft = field switch
        {
            "kind" => draft with { Kind = (QueryKind)(number ?? 0) }, "npc" => draft with { Npc = value },
            "location" => draft with { Location = value }, "source" => draft with { Source = value },
            "state" => draft with { Completion = (QueryCompletion)(number ?? 0) }, _ => draft
        };
    }
    private Choice[] Choices(string field)
    {
        Choice any = new(null, T("filter.any"));
        IEnumerable<Choice> values = field switch
        {
            "kind" => new[] { "query.kind-all", "query.kind-heart", "query.kind-ordinary", "query.kind-prerequisite" }
                .Select((key, i) => new Choice(i.ToString(), T(key))),
            "state" => new[] { "filter.any", "filter.done", "filter.pending" }.Select((key, i) => new Choice(i.ToString(), T(key))),
            "npc" => new[] { any }.Concat(index.Rows.SelectMany(row => row.Npcs).Distinct(StringComparer.Ordinal)
                .Select(id => new Choice(id, Context.Characters.Get(id))).OrderBy(choice => choice.Label, StringComparer.CurrentCulture)),
            "location" => new[] { any }.Concat(index.Rows.Where(row => row.Event is not null).DistinctBy(row => row.LocationKey, StringComparer.OrdinalIgnoreCase)
                .Select(row => new Choice(row.LocationKey, row.Location)).OrderBy(choice => choice.Label, StringComparer.CurrentCulture)),
            "source" => SourceChoices(any),
            _ => []
        };
        var choices = values.ToList();
        // Keep an unavailable active value visible, just as the source picker does. It remains
        // an exact filter until the player clears it, and naturally rejoins the list if restored.
        if (field is "npc" or "location" && Value(field) is { } selected && !choices.Any(choice => SameId(field, choice.Id, selected)))
        {
            string label = field == "npc" ? Context.Characters.Get(selected)
                : Context.Locations.Get(selected.StartsWith("Data/Events/", StringComparison.OrdinalIgnoreCase) ? selected[12..] : selected);
            choices.Add(new(selected, label));
        }
        return choices.ToArray();
    }
    private static bool SameId(string field, string? left, string? right)
        => string.Equals(left, right, field is "location" or "source" ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    private IEnumerable<Choice> SourceChoices(Choice any)
    {
        var choices = index.Rows.Select(row => row.Source).DistinctBy(source => source.Key, StringComparer.OrdinalIgnoreCase)
            .Select(source => new Choice(source.Key, source.Label(T))).ToList();
        // A refreshed catalog may remove all events from a selected source. Preserve that selection
        // visibly, so an empty result cannot appear to have the unrestricted "Any" source filter.
        if (draft.Source is { } selected && !choices.Any(choice => string.Equals(choice.Id, selected, StringComparison.OrdinalIgnoreCase)))
        {
            string label = selected switch
            {
                StoryQuerySource.GameKey => T("source.game"),
                StoryQuerySource.AmbiguousKey => T("source.origin-ambiguous"),
                StoryQuerySource.UnknownKey => T("source.unknown"),
                _ => selected.StartsWith("mod:", StringComparison.OrdinalIgnoreCase) ? selected[4..] : selected
            };
            choices.Add(new(selected, label));
        }
        return new[] { any }.Concat(choices.OrderBy(choice => choice.Id == StoryQuerySource.GameKey ? 0
            : choice.Id?.StartsWith("mod:", StringComparison.OrdinalIgnoreCase) == true ? 1 : 2)
            .ThenBy(choice => choice.Label, StringComparer.CurrentCulture));
    }
}
