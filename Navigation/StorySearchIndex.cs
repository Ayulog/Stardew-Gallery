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
}

internal sealed class StorySearchIndex
{
    private readonly IReadOnlyList<StorySearchRow> rows;
    private readonly IReadOnlySet<string> completed;
    private readonly PinyinTable? pinyin;
    internal IReadOnlyList<StorySearchRow> Rows => rows;

    /// <param name="pinyin">With a table, Latin letters also find Chinese titles, locations and characters by pinyin.</param>
    internal StorySearchIndex(GalleryCatalog catalog, Func<GalleryEvent, string> locationName, Func<string, string> characterName,
        Func<EventIdentity, string?>? names = null, IReadOnlySet<string>? completed = null,
        Func<GalleryEvent?, string, string?>? characterKey = null, Func<GalleryEvent, string>? locationKey = null,
        Func<string, string>? sourceText = null, PinyinTable? pinyin = null)
    {
        this.completed = completed ?? new HashSet<string>();
        this.pinyin = pinyin;
        string? CanonicalNpc(GalleryEvent? entry, string id) => characterKey is null ? id : characterKey(entry, id);
        string[] Npcs(GalleryEvent? entry, IEnumerable<string> ids) => ids.Select(id => CanonicalNpc(entry, id))
            .Where(id => id is not null).Cast<string>().Distinct(StringComparer.Ordinal).ToArray();
        var projected = catalog.StoryEntries.DistinctBy(entry => entry.Resolved.Identity)
            .Select(entry => new StorySearchRow(entry, locationName(entry), string.Join(", ", Npcs(entry, entry.RelatedNpcNames).Select(characterName)))
            {
                Title = names?.Invoke(entry.Resolved.Identity) ?? entry.EventId,
                Npcs = Npcs(entry, entry.RelatedNpcNames), LocationKey = locationKey?.Invoke(entry) ?? entry.AssetName
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
        string? spelling = pinyin is null ? null : PinyinTable.Spelling(query);
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
            && (query.Length == 0 || row.EventId.Contains(query, StringComparison.OrdinalIgnoreCase)
                || row.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || row.Identity.AssetName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || row.Location.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || row.Characters.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || row.SourceLabel.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || row.Source.ModId?.Contains(query, StringComparison.OrdinalIgnoreCase) == true
                || row.Npcs.Any(name => name.Contains(query, StringComparison.OrdinalIgnoreCase))
                || spelling is not null && (pinyin!.Matches(row.Title, spelling) || pinyin.Matches(row.Location, spelling)
                    || pinyin.Matches(row.Characters, spelling))))
            .OrderByDescending(row => query.Length > 0 && (row.EventId == query || row.Title.Equals(query, StringComparison.CurrentCultureIgnoreCase))).ToArray();
    }
}
