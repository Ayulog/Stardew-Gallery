using StardewGallery;

internal static class SourceQueryChecks
{
    internal static void Run()
    {
        GalleryEvent Story(string asset, string id, StoryKind kind = StoryKind.Heart) => new(
            new(new(asset, id), "Town", id + "/f Abigail 500", "scene", new([], []), "d", "s"), new(OwnershipKind.Direct, [new("Abigail", 500)]))
            { Kind = kind, RelatedNpcNames = ["Abigail"] };
        var vanilla = Story("Data/Events/Town", "1");
        var first = Story("Data/Events/Town", "2");
        var second = Story("Data/Events/Beach", "2", StoryKind.Ordinary);
        var unknown = Story("Data/Events/Town", "3");
        var ambiguous = Story("Data/Events/Town", "4");
        var malformed = Story("Data/Events/Town", "5");
        var candidateOnly = Story("Data/Events/Town", "6");
        EventSourceActor providerA = new("Author.First", "Same name");
        EventSourceActor providerB = new("Author.Second", "Same name");
        EventOriginMatch Identified(EventSourceActor actor) => new(actor, [], EventOriginMatchStatus.Identified);
        EventDefinitionCandidate Candidate(EventSourceActor actor, GalleryEvent story) => new(actor, story.AssetName, story.EventKey, "events.json", "hash");
        var prerequisite = new PrerequisiteEvent("marker", [], [], [first]);
        var catalog = new GalleryCatalog([], [vanilla, first, second, unknown, ambiguous, malformed, candidateOnly], [])
        {
            Prerequisites = [prerequisite],
            Origins = new Dictionary<EventIdentity, EventOriginMatch>
            {
                [vanilla.Resolved.Identity] = Identified(EventSourceActor.GameBase),
                [first.Resolved.Identity] = Identified(providerA),
                [second.Resolved.Identity] = Identified(providerB),
                [ambiguous.Resolved.Identity] = new(null, [Candidate(providerA, ambiguous), Candidate(providerB, ambiguous)], EventOriginMatchStatus.Ambiguous),
                [malformed.Resolved.Identity] = new(null, [], EventOriginMatchStatus.Identified),
                [candidateOnly.Resolved.Identity] = new(providerA, [Candidate(providerA, candidateOnly)], EventOriginMatchStatus.Unknown)
            }
        };
        string Translate(string key) => key switch { "source.game" => "原版", "source.unknown" => "未知", "source.origin-ambiguous" => "多个候选来源", _ => key };
        var index = new StorySearchIndex(catalog, story => story.AssetName.EndsWith("Beach") ? "海滩" : "小镇", _ => "阿比盖尔",
            completed: new HashSet<string> { "2" },
            parser: new ConditionParser(key => key.Split('/'), text => text.Split(' ', StringSplitOptions.RemoveEmptyEntries)), sourceText: Translate);
        IReadOnlyList<StorySearchRow> BySource(string key) => index.Search("", new QueryFilter { Source = key });
        Check(BySource(StoryQuerySource.GameKey).Single().Event == vanilla, "base game is an explicit independent source");
        Check(BySource(StoryQuerySource.ModKey("Author.First")).Single().Event == first
            && BySource(StoryQuerySource.ModKey("author.second")).Single().Event == second, "same-name mods filter by exact case-insensitive unique ID and retain asset identity");
        Check(BySource(StoryQuerySource.ModKey("Author")).Count == 0, "source filter never uses mod ID substrings");
        Check(index.Rows.Single(row => row.Event == first).SourceLabel == "Same name (Author.First)"
            && index.Rows.Single(row => row.Event == second).SourceLabel == "Same name (Author.Second)", "display labels distinguish same-name mods");
        Check(index.Search("same NAME").Count == 2 && index.Search("AUTHOR.SECOND").Single().Event == second,
            "free text searches source names and IDs");
        Check(index.Search("原版").Single().Event == vanilla && index.Search("未知").Count == 4, "localized source categories are searchable");
        Check(BySource(StoryQuerySource.AmbiguousKey).Single().Event == ambiguous
            && BySource(StoryQuerySource.UnknownKey).Count == 4, "ambiguous and unknown remain separate; inconsistent evidence stays unresolved");
        Check(index.Rows.Single(row => row.Prerequisite is not null).Source.Key == StoryQuerySource.UnknownKey,
            "synthetic prerequisite source is not guessed from related stories");
        Check(index.Search("Same name", new QueryFilter { Source = StoryQuerySource.ModKey("Author.Second"), Kind = QueryKind.Ordinary,
            Npc = "Abigail", Location = "Data/Events/Beach", Completion = QueryCompletion.Complete, MinimumHearts = 2 }).Single().Event == second,
            "source composes with text, kind, character, location, progress and requirements");
        Check(index.Search("", new QueryFilter { Source = StoryQuerySource.ModKey("Author.Second"), Kind = QueryKind.Heart }).Count == 0,
            "source does not weaken a conflicting kind filter");
        Check(index.Search("").Count == 8, "unfiltered query retains all eligible sources");
        Console.WriteLine("Source query checks passed.");
    }

    private static void Check(bool value, string label) { if (!value) throw new InvalidOperationException(label); }
}