namespace StardewGallery;

internal sealed record StorySearchRow(GalleryEvent? Event, string Location, string Characters)
{
    internal PrerequisiteEvent? Prerequisite { get; init; }
    internal EventIdentity Identity => Event?.Resolved.Identity ?? Prerequisite!.Identity;
    internal string EventId => Event?.EventId ?? Prerequisite!.EventId;
    internal string Title { get; init; } = "";
    internal IReadOnlyList<string> Npcs { get; init; } = [];
    internal string LocationKey { get; init; } = "";
    internal IReadOnlyList<ConditionExpression> Requirements { get; init; } = [];
}

internal sealed class StorySearchIndex
{
    private readonly IReadOnlyList<StorySearchRow> rows;
    private readonly IReadOnlySet<string> completed;
    private readonly Func<StorySearchRow, ConditionTruth>? conditionState;
    private readonly Dictionary<EventIdentity, ConditionTruth> conditionCache = [];
    internal IReadOnlyList<StorySearchRow> Rows => rows;

    internal StorySearchIndex(GalleryCatalog catalog, Func<GalleryEvent, string> locationName, Func<string, string> characterName,
        Func<EventIdentity, string?>? names = null,
        IReadOnlySet<string>? completed = null, ConditionParser? parser = null,
        Func<StorySearchRow, ConditionTruth>? conditionState = null,
        Func<GalleryEvent?, string, string?>? characterKey = null, Func<GalleryEvent, string>? locationKey = null)
    {
        this.completed = completed ?? new HashSet<string>();
        this.conditionState = conditionState;
        string[] Npcs(GalleryEvent? entry, IEnumerable<string> ids) => ids.Select(id => characterKey is null ? id : characterKey(entry, id))
            .Where(id => id is not null).Cast<string>().Distinct(StringComparer.Ordinal).ToArray();
        rows = catalog.StoryEntries.DistinctBy(entry => entry.Resolved.Identity)
            .Select(entry => new StorySearchRow(entry, locationName(entry), string.Join(", ", Npcs(entry, entry.RelatedNpcNames).Select(characterName)))
            {
                Title = names?.Invoke(entry.Resolved.Identity) ?? entry.EventId,
                Npcs = Npcs(entry, entry.RelatedNpcNames), LocationKey = locationKey?.Invoke(entry) ?? entry.AssetName,
                Requirements = parser?.ParseRawKey(entry.EventKey).Conditions ?? []
            })
            .Concat(catalog.Prerequisites.Select(entry =>
            {
                IReadOnlyList<string> npcs = Npcs(null, entry.RelatedNpcNames);
                return new StorySearchRow(null, "-", string.Join(", ", npcs.Select(characterName)))
                { Prerequisite = entry, Npcs = npcs, Title = names?.Invoke(entry.Identity) ?? entry.EventId };
            }))
            .OrderBy(row => row.EventId, StringComparer.Ordinal).ThenBy(row => row.Identity.AssetName, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    internal IReadOnlyList<StorySearchRow> Search(string text, StoryKind? kind = null, string? location = null)
        => Search(text, new QueryFilter { Kind = kind switch { StoryKind.Heart => QueryKind.Heart, StoryKind.Ordinary => QueryKind.Ordinary,
            _ => QueryKind.All }, Location = location });

    internal IReadOnlyList<StorySearchRow> Search(string text, QueryFilter filter)
    {
        string query = text.Trim();
        return rows.Where(row => (filter.Kind switch
            {
                QueryKind.Heart => row.Event?.Kind == StoryKind.Heart,
                QueryKind.Ordinary => row.Event?.Kind == StoryKind.Ordinary,
                QueryKind.Prerequisite => row.Prerequisite is not null,
                _ => true
            })
            && (filter.Location is null || string.Equals(row.LocationKey, filter.Location, StringComparison.OrdinalIgnoreCase))
            && (filter.Npc is null || row.Npcs.Contains(filter.Npc, StringComparer.Ordinal))
            && (filter.Completion == QueryCompletion.Any || completed.Contains(row.EventId) == (filter.Completion == QueryCompletion.Complete))
            && MatchesRequirements(row, filter)
            && (filter.Conditions == QueryConditionState.Any || Truth(row) == (filter.Conditions switch
                { QueryConditionState.Met => ConditionTruth.True, QueryConditionState.Unmet => ConditionTruth.False, _ => ConditionTruth.Unknown }))
            && (query.Length == 0 || row.EventId.Contains(query, StringComparison.OrdinalIgnoreCase)
                || row.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || row.Identity.AssetName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || row.Location.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || row.Characters.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || row.Npcs.Any(name => name.Contains(query, StringComparison.OrdinalIgnoreCase))))
            .OrderByDescending(row => query.Length > 0 && (row.EventId == query || row.Title.Equals(query, StringComparison.CurrentCultureIgnoreCase))).ToArray();
    }

    private ConditionTruth Truth(StorySearchRow row)
    {
        if (!conditionCache.TryGetValue(row.Identity, out ConditionTruth truth))
            conditionCache[row.Identity] = truth = conditionState?.Invoke(row) ?? ConditionTruth.Unknown;
        return truth;
    }

    private static bool MatchesRequirements(StorySearchRow row, QueryFilter filter)
    {
        if (row.Prerequisite is not null && (filter.Season is not null || filter.Weather is not null || filter.Time is not null
            || filter.MinimumHearts is not null || filter.MaximumHearts is not null)) return false;
        foreach (ConditionExpression requirement in row.Requirements)
        {
            if (filter.Weather is not null && requirement is NativeQueryCondition query && !query.Negated
                && SafeGameQuery.TryParse(query.Query, out var clauses))
                foreach (var clause in clauses.Where(clause => clause.Name == "WEATHER"))
                    if (clause.Arguments.Skip(1).Select(QueryWeather.Normalize).Contains(QueryWeather.Normalize(filter.Weather)) == clause.Negated) return false;
            bool? matches = requirement switch
            {
                SeasonCondition season when filter.Season is not null => season.Seasons.Contains(filter.Season, StringComparer.OrdinalIgnoreCase),
                TimeCondition time when filter.Time is int value => value >= time.Min && value <= time.Max,
                WeatherCondition weather when filter.Weather is not null => QueryWeather.Matches(weather, filter.Weather),
                _ => null
            };
            if (matches is not null && matches.Value == requirement.Negated) return false;
        }
        if (filter.MinimumHearts is not null || filter.MaximumHearts is not null)
        {
            int? points = row.Requirements.OfType<FriendshipCondition>().Where(condition => !condition.Negated)
                .SelectMany(condition => condition.Requirements).Where(requirement => filter.Npc is null || requirement.Npc == filter.Npc)
                .Select(requirement => (int?)requirement.Points).DefaultIfEmpty(null).Max();
            if (points is null) return false;
            int hearts = (int)Math.Ceiling(points.Value / 250d);
            if (filter.MinimumHearts is int min && hearts < min || filter.MaximumHearts is int max && hearts > max) return false;
        }
        return true;
    }
}
