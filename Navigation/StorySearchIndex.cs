namespace StardewGallery;

internal sealed record StorySearchRow(GalleryEvent? Event, string Location, string Characters)
{
    internal PrerequisiteEvent? Prerequisite { get; init; }
    internal EventIdentity Identity => Event?.Resolved.Identity ?? Prerequisite!.Identity;
    internal string EventId => Event?.EventId ?? Prerequisite!.EventId;
    internal string Title { get; init; } = "";
    internal StoryQuerySource Source { get; init; } = StoryQuerySource.Unknown;
    internal string SourceLabel { get; init; } = "";
    internal IReadOnlyList<string> Npcs { get; init; } = [];
    internal string LocationKey { get; init; } = "";
    internal IReadOnlyList<ConditionExpression> Requirements { get; init; } = [];
    internal IReadOnlyList<RelationshipEvidence> Relationships { get; init; } = [];
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
        Func<GalleryEvent?, string, string?>? characterKey = null, Func<GalleryEvent, string>? locationKey = null,
        Func<string, string>? sourceText = null)
    {
        this.completed = completed ?? new HashSet<string>();
        this.conditionState = conditionState;
        string? CanonicalNpc(GalleryEvent? entry, string id) => characterKey is null ? id : characterKey(entry, id);
        string[] Npcs(GalleryEvent? entry, IEnumerable<string> ids) => ids.Select(id => CanonicalNpc(entry, id))
            .Where(id => id is not null).Cast<string>().Distinct(StringComparer.Ordinal).ToArray();
        var projected = catalog.StoryEntries.DistinctBy(entry => entry.Resolved.Identity)
            .Select(entry =>
            {
                IReadOnlyList<ConditionExpression> requirements = parser?.ParseRawKey(entry.EventKey).Conditions ?? [];
                return new StorySearchRow(entry, locationName(entry), string.Join(", ", Npcs(entry, entry.RelatedNpcNames).Select(characterName)))
                {
                    Title = names?.Invoke(entry.Resolved.Identity) ?? entry.EventId,
                    Npcs = Npcs(entry, entry.RelatedNpcNames), LocationKey = locationKey?.Invoke(entry) ?? entry.AssetName,
                    Requirements = requirements,
                    Relationships = ConditionRelationshipEvidence.Read(requirements)
                        .Select(evidence => CanonicalNpc(entry, evidence.Npc) is string npc ? evidence with { Npc = npc } : null)
                        .OfType<RelationshipEvidence>().ToArray()
                };
            })
            .Concat(catalog.Prerequisites.Select(entry =>
            {
                IReadOnlyList<string> npcs = Npcs(null, entry.RelatedNpcNames);
                return new StorySearchRow(null, "-", string.Join(", ", npcs.Select(characterName)))
                { Prerequisite = entry, Npcs = npcs, Title = names?.Invoke(entry.Identity) ?? entry.EventId };
            }))
            .OrderBy(row => row.EventId, StringComparer.Ordinal).ThenBy(row => row.Identity.AssetName, StringComparer.OrdinalIgnoreCase).ToArray();
        var locationGroups = projected.Where(row => row.Event is not null && !GalleryNameText.IsMissing(row.Location) && row.Location != "-")
            .GroupBy(row => row.Location.Trim(), StringComparer.CurrentCulture)
            .ToDictionary(group => group.Key, group => group.Select(row => row.LocationKey).OrderBy(key => key, StringComparer.OrdinalIgnoreCase).First(), StringComparer.CurrentCulture);
        rows = projected.Select(row =>
        {
            catalog.Origins.TryGetValue(row.Identity, out EventOriginMatch? origin);
            StoryQuerySource source = StoryQuerySource.From(origin);
            return row with { Source = source, SourceLabel = source.Label(sourceText) };
        }).Select(row => row.Event is not null && locationGroups.TryGetValue(row.Location.Trim(), out string? groupKey)
            ? row with { LocationKey = groupKey } : row).ToArray();
    }

    internal IReadOnlyList<StorySearchRow> Search(string text, StoryKind? kind = null, string? location = null)
        => Search(text, new QueryFilter { Kind = kind switch { StoryKind.Heart => QueryKind.Heart, StoryKind.Ordinary => QueryKind.Ordinary,
            _ => QueryKind.All } }).Where(row => location is null || string.Equals(row.Event?.AssetName, location, StringComparison.OrdinalIgnoreCase)).ToArray();

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
            && (filter.Source is null || string.Equals(row.Source.Key, filter.Source, StringComparison.OrdinalIgnoreCase))
            && (filter.Completion == QueryCompletion.Any || completed.Contains(row.EventId) == (filter.Completion == QueryCompletion.Complete))
            && MatchesRequirements(row, filter)
            && (filter.Conditions == QueryConditionState.Any || Truth(row) == (filter.Conditions switch
                { QueryConditionState.Met => ConditionTruth.True, QueryConditionState.Unmet => ConditionTruth.False, _ => ConditionTruth.Unknown }))
            && (query.Length == 0 || row.EventId.Contains(query, StringComparison.OrdinalIgnoreCase)
                || row.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || row.Identity.AssetName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || row.Location.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || row.Characters.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || row.SourceLabel.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || row.Source.ModId?.Contains(query, StringComparison.OrdinalIgnoreCase) == true
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
        if (!MatchesEnvironment(row, filter)) return false;
        if (filter.MinimumHearts is not null || filter.MaximumHearts is not null)
        {
            int? points = row.Relationships.Where(requirement => filter.Npc is null || requirement.Npc == filter.Npc)
                .Select(requirement => requirement.MinimumPoints).DefaultIfEmpty(null).Max();
            if (points is null) return false;
            int hearts = (int)Math.Ceiling(points.Value / 250d);
            if (filter.MinimumHearts is int min && hearts < min || filter.MaximumHearts is int max && hearts > max) return false;
        }
        return true;
    }

    private static bool MatchesEnvironment(StorySearchRow row, QueryFilter filter)
    {
        if (filter.Season is null && filter.Weather is null && filter.Time is null) return true;
        Dictionary<NativeQueryCondition, IReadOnlyList<SafeQueryClause>> queries = [];
        Dictionary<string, bool?> weatherFacts = [];
        foreach (NativeQueryCondition query in row.Requirements.OfType<NativeQueryCondition>())
        {
            // The same restricted parser/evaluator used by condition details. Unsupported
            // queries stay unknown; indexing never invokes registered GSQ delegates.
            if (!SafeGameQuery.TryParse(query.Query, out var clauses)) continue;
            queries[query] = clauses;
            if (filter.Weather is not null)
                foreach (SafeQueryClause clause in clauses.Where(value => value.Name == "WEATHER"))
                {
                    string location = clause.Arguments[0];
                    // At the event entrance Here and Target are the event location. A
                    // weather requirement elsewhere cannot constrain this location.
                    if (location.Equals("Here", StringComparison.OrdinalIgnoreCase)
                        || location.Equals("Target", StringComparison.OrdinalIgnoreCase)
                        || location.Equals(row.Event?.LocationName, StringComparison.OrdinalIgnoreCase))
                        weatherFacts[SafeGameQuery.FactKey(clause)] = clause.Arguments.Skip(1)
                            .Contains(QueryWeather.Normalize(filter.Weather), StringComparer.OrdinalIgnoreCase);
                }
        }

        // SEASON_DAY alternatives must be checked against a shared date. In particular,
        // excluding spring 5 still permits other spring days, and !(date && weather)
        // must negate the whole conjunction, not each projected constraint.
        bool hasDates = queries.Values.Any(clauses => clauses.Any(clause => clause.Name == "SEASON_DAY"));
        string?[] seasons = filter.Season is not null ? [filter.Season]
            : hasDates ? ["spring", "summer", "fall", "winter"] : [null];
        ConditionEvaluationContext context = new(null, null, null, filter.Time, filter.Weather,
            null, null, null, null, null, null, null, null, null, null)
            { Details = new() { QueryFacts = weatherFacts } };
        foreach (string? season in seasons)
            for (int day = 1; day <= (hasDates ? 28 : 1); day++)
            {
                context = context with { Season = season, DayOfMonth = hasDates ? day : null };
                bool possible = true;
                foreach (ConditionExpression requirement in row.Requirements)
                {
                    bool? matches = requirement switch
                    {
                        SeasonCondition value when season is not null => value.Seasons.Contains(season, StringComparer.OrdinalIgnoreCase),
                        DayOfMonthCondition value when hasDates => value.Days.Contains(day),
                        TimeCondition value when filter.Time is int time => time >= value.Min && time <= value.Max,
                        WeatherCondition value when filter.Weather is not null => QueryWeather.Matches(value, filter.Weather),
                        NativeQueryCondition value when queries.TryGetValue(value, out var clauses) => SafeGameQuery.Evaluate(clauses, context),
                        _ => null
                    };
                    if (matches is not null && matches.Value == requirement.Negated)
                    {
                        possible = false;
                        break;
                    }
                }
                // Unknown is not a confirmed conflict with the user's filter.
                if (possible) return true;
            }
        return false;
    }
}
