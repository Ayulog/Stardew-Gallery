using StardewValley;

namespace StardewGallery;

internal sealed class GalleryFilterMenu : GalleryToolMenu
{
    private sealed record Choice(string? Id, string Label);
    private readonly StorySearchIndex index;
    private QueryFilter draft;
    private bool more;
    private string? picker;
    private int returnFocus;
    private readonly List<string> fields = [];
    protected override string Title => Context.I18n.Get(picker is null ? "filter.title" : "filter." + picker);
    protected override string? ApplyLabel => picker is null ? Context.I18n.Get("filter.apply").ToString() : null;
    protected override string? ExtraLabel => Context.I18n.Get(picker is null ? "filter.reset" : "filter.clear");
    internal GalleryFilterMenu(GalleryViewContext context, GalleryPageState state) : base(context, state)
    {
        draft = state.Filter;
        index = new StorySearchIndex(context.Catalog, e => context.Locations.Get(e.LocationName), context.Characters.Get,
            parser: new ConditionParser(Event.SplitPreconditions, ArgUtility.SplitBySpaceQuoteAware),
            characterKey: (entry, id) => context.Characters.Key(id, entry), locationKey: e => context.Locations.Key(e.LocationName));
        Rebuild();
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
            fields.AddRange(["kind", "npc", "location", "state", "conditions", "more"]);
            if (more) fields.AddRange(["season", "weather", "time", "min", "max"]);
            foreach (string field in fields)
            {
                if (field == "more") { Rows.Add(new(T(more ? "filter.less" : "filter.more"), () => { more = !more; Rebuild(RowId + fields.IndexOf("more")); })); continue; }
                string value = Choices(field).FirstOrDefault(choice => choice.Id == Value(field))?.Label ?? T("filter.any");
                Rows.Add(new(T("filter." + field) + ": " + value, () =>
                {
                    returnFocus = RowId + fields.IndexOf(field); picker = field;
                    int selected = Array.FindIndex(Choices(field), choice => choice.Id == Value(field));
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
        if (field is "npc" or "location" or "more") { Activate(FocusId); return; }
        var choices = Choices(field); int selected = Array.FindIndex(choices, choice => choice.Id == Value(field));
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
        "kind" => ((int)draft.Kind).ToString(), "npc" => draft.Npc, "location" => draft.Location,
        "state" => ((int)draft.Completion).ToString(), "conditions" => ((int)draft.Conditions).ToString(),
        "season" => draft.Season, "weather" => draft.Weather, "time" => draft.Time?.ToString(),
        "min" => draft.MinimumHearts?.ToString(), "max" => draft.MaximumHearts?.ToString(), _ => null
    };
    private void Set(string field, string? value)
    {
        int? number = int.TryParse(value, out int parsed) ? parsed : null;
        draft = field switch
        {
            "kind" => draft with { Kind = (QueryKind)(number ?? 0) }, "npc" => draft with { Npc = value },
            "location" => draft with { Location = value },
            "state" => draft with { Completion = (QueryCompletion)(number ?? 0) }, "conditions" => draft with { Conditions = (QueryConditionState)(number ?? 0) },
            "season" => draft with { Season = value }, "weather" => draft with { Weather = value }, "time" => draft with { Time = number },
            "min" => draft with { MinimumHearts = number, MaximumHearts = number > draft.MaximumHearts ? number : draft.MaximumHearts },
            "max" => draft with { MaximumHearts = number, MinimumHearts = number < draft.MinimumHearts ? number : draft.MinimumHearts }, _ => draft
        };
    }
    private Choice[] Choices(string field)
    {
        Choice any = new(null, T("filter.any"));
        IEnumerable<Choice> values = field switch
        {
            "kind" => new[] { "query.kind-all", "query.kind-heart", "query.kind-ordinary", "query.kind-prerequisite" }
                .Select((key, i) => new Choice(i.ToString(), T(key))),
            "state" => new[] { "filter.any", draft.Kind == QueryKind.All ? "filter.done" : draft.Kind == QueryKind.Prerequisite ? "prerequisite.complete" : "filter.seen",
                draft.Kind == QueryKind.All ? "filter.pending" : draft.Kind == QueryKind.Prerequisite ? "prerequisite.incomplete" : "filter.unseen" }.Select((key, i) => new Choice(i.ToString(), T(key))),
            "conditions" => new[] { "filter.any", "filter.met", "filter.unmet", "filter.unknown" }.Select((key, i) => new Choice(i.ToString(), T(key))),
            "npc" => new[] { any }.Concat(index.Rows.SelectMany(row => row.Npcs).Distinct(StringComparer.Ordinal)
                .Select(id => new Choice(id, Context.Characters.Get(id))).OrderBy(choice => choice.Label, StringComparer.CurrentCulture)),
            "location" => new[] { any }.Concat(index.Rows.Where(row => row.Event is not null).DistinctBy(row => row.LocationKey, StringComparer.OrdinalIgnoreCase)
                .Select(row => new Choice(row.LocationKey, row.Location)).OrderBy(choice => choice.Label, StringComparer.CurrentCulture)),
            "season" => new[] { any }.Concat(new[] { "spring", "summer", "fall", "winter" }.Select(id => new Choice(id, T("season." + id)))),
            "weather" => new[] { any }.Concat(QueryWeather.Standard.Concat(index.Rows.SelectMany(row => row.Requirements).SelectMany(QueryWeather.Mentioned))
                .Distinct(StringComparer.Ordinal).Select(id => new Choice(id, WeatherName(id)))),
            "time" => new[] { any }.Concat(Enumerable.Range(6, 21).Select(hour => new Choice((hour * 100).ToString(), Game1.getTimeOfDayString(hour * 100)))),
            "min" or "max" => new[] { any }.Concat(Enumerable.Range(0, 15).Select(hearts => new Choice(hearts.ToString(), Context.I18n.Get("event.hearts", new { hearts }).ToString()))),
            _ => []
        };
        return values.ToArray();
    }
    private string WeatherName(string id)
    {
        var text = Context.I18n.Get("weather." + ConditionDescriber.NormalizeWeather(id).ToLowerInvariant());
        return text.HasValue() ? text.ToString() : GalleryLocationName.Resolve(id, null, _ => null);
    }
}
