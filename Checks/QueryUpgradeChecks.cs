using StardewGallery;

internal static class QueryUpgradeChecks
{
    internal static void Run()
    {
        var parser = new ConditionParser(key => key.Split('/'), text => text.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        GalleryEvent Story(string id, string npc, string location, string key) => new(new(new("Data/Events/" + location, id), location, key,
            "none/0 0/farmer 1 1 2/speak Sam hello/end", new([], []), "d", "s"), new(OwnershipKind.Direct, [new(npc, 500)]))
            { Kind = StoryKind.Heart, RelatedNpcNames = [npc] };
        var sam = Story("1", "Sam", "Town", "1/e 2111294/f Sam 500/Season summer");
        var mountain = Story("2", "Abigail", "Mountain", "2/f Abigail 1000");
        var catalog = new GalleryCatalog([], [sam, mountain], []);
        PrerequisiteRule[] rules =
        [
            new("invite", ["DayEnding"], "PLAYER_HEARTS Current Emily 8", false, true, ["AddMail Current EmilyCamping", "MarkEventSeen Current 2111294"]),
            new("cancel", ["DayStarted"], "", false, false, ["MarkEventSeen 1 marker false", "MarkEventSeen All marker", "MarkEventSeen Any invalid"]),
            new("existing", ["Manual"], "", false, false, ["MarkEventSeen Current 1"])
        ];
        var prereqs = PrerequisiteCatalog.Build(rules, catalog, text => text.Split(' '), parser, text => text.Split('/'));
        Check(prereqs.Count == 2 && prereqs.Single(p => p.EventId == "2111294").Dependents.Single() == sam, "references join to real marker rules");
        var marker = prereqs.Single(p => p.EventId == "marker");
        Check(marker.Sources.Count == 2 && !marker.Sources[0].SetsMarker && marker.Sources[0].Player == "Host"
            && marker.Sources[1].SetsMarker, "cancel and set retain order and target; invalid selectors excluded");
        Check(prereqs.All(p => p.EventId != "1"), "existing story is not duplicated as marker-only entry");
        var repeated = PrerequisiteCatalog.Build([new("same", ["DayEnding"], "", false, false, []),
            new("SAME", ["DayEnding"], "", false, false, ["MarkEventSeen Current actual"])], catalog, text => text.Split(' '), parser, text => text.Split('/'));
        Check(repeated.Single().EventId == "actual", "empty rule does not consume a duplicate ID");
        var keyGate = Story("55134261", "Lance", "FarmHouse", "55134261/i Golden_Key") with { Kind = StoryKind.Internal, ClassificationReason = "internal-offscreen-marker" };
        var keyStory = Story("55134259", "Lance", "Cavern", "55134259/e 55134261");
        var stepCatalog = new GalleryCatalog([], [keyStory], [keyGate, keyGate with { Resolved = keyGate.Resolved with { Identity = new("Data/Events/Town", "unreferenced") } }]);
        var steps = PrerequisiteCatalog.Build([], stepCatalog, text => text.Split(' '), parser, text => text.Split('/'));
        Check(steps.Single().EventId == "55134261" && steps[0].Dependents.Single() == keyStory, "referenced item gate records its own seen ID; unrelated internal flow stays hidden");
        catalog = catalog with { Prerequisites = prereqs };
        Check(StoryDependencyLookup.Find(catalog, "2111294").Prerequisite is not null && !StoryDependencyLookup.Find(catalog, "2111294").Missing, "missing prerequisite now resolves");
        var index = new StorySearchIndex(catalog, e => e.LocationName == "Mountain" ? "山区" : "小镇", n => n == "Sam" ? "山姆" : "阿比盖尔",
            identity => identity.EventId == "1" ? "雨天约会" : null, completed: new HashSet<string> { "1", "2111294" }, parser: parser,
            conditionState: row => row.EventId == "1" ? ConditionTruth.True : ConditionTruth.Unknown);
        Check(index.Search("山", new QueryFilter { Kind = QueryKind.Heart, Npc = "Sam" }).Single().Event == sam, "exact NPC avoids location substring collisions");
        Check(index.Search("山", new QueryFilter { Location = "Data/Events/Mountain" }).Single().Event == mountain, "exact location avoids NPC substring collisions");
        Check(index.Search("雨天约会").Single().EventId == "1" && index.Search("1").First().EventId == "1", "alias and original ID searchable, exact ID first");
        Check(index.Search("", new QueryFilter { Kind = QueryKind.Prerequisite, Completion = QueryCompletion.Complete }).Single().EventId == "2111294", "marker completion uses seen state");
        Check(index.Search("", new QueryFilter { Conditions = QueryConditionState.Met }).Single().EventId == "1", "unknown never passes met filter");
        Check(index.Search("", new QueryFilter { Season = "winter" }).Single().EventId == "2", "season restriction and unrestricted story");
        Check(index.Search("", new QueryFilter { MinimumHearts = 3 }).Single().EventId == "2", "heart requirement filter");
        Check(index.Search("2111294").Single().Prerequisite is not null && index.Search("").Count == 4, "all events includes stories and prerequisites");
        foreach (string query in new[] { "PLAYER_HEARTS Current Emily 8", "PLAYER_FRIENDSHIP_POINTS Current Emily 2502", "PLAYER_NPC_RELATIONSHIP Current Emily Dating Engaged Married",
            "PLAYER_HAS_SEEN_EVENT Current 2123243", "WEATHER Woods Sun", "PLAYER_HAS_MAIL Current letter Received", "!PLAYER_HAS_CONVERSATION_TOPIC Current ElliottGone1" })
        {
            Check(SafeGameQuery.TryParse(query, out var clauses), "known prerequisite query accepted");
            var context = new ConditionEvaluationContext(null, null, null, null, null, null, null, null, null, null, null, null, null, null, null)
                { Details = new() { QueryFacts = clauses.ToDictionary(SafeGameQuery.FactKey, _ => (bool?)true) } };
            Check(SafeGameQuery.Evaluate(clauses, context) == !query.StartsWith('!'), "query fact negation applied once");
            Check(SafeGameQuery.Evaluate(clauses, context with { Details = new() }) is null, "missing facts remain unknown");
        }
        Check(!SafeGameQuery.TryParse("CustomAuthor.UnsafeQuery foo", out _), "unknown third-party query is not invoked");
        Check(Enum.GetValues<QueryKind>().Length == 4 && new QueryFilter().Kind == QueryKind.All, "one all-events option includes every query category");
        Check(GalleryNameText.IsMissing("(no translation:Name.JunimoJade)")
            && GalleryNameText.IsMissing("{{i18n:missing}}"), "translation diagnostics are not player-facing names");
        Check(GalleryLocationName.Resolve("Custom_TestRoom", "(no translation:TestRoom.Name)", _ => "测试房间") == "测试房间", "missing display name uses translation fallback");
        Check(GalleryLocationName.Resolve("Custom_TestRoom", "(no translation:TestRoom.Name)", _ => null) == "Test Room", "missing locale still has a readable fallback");
        var rainy = new WeatherCondition(WeatherKind.Rainy, "rainy", ConditionSource.LegacyEventPrecondition, "w rainy", false);
        var dry = rainy with { Kind = WeatherKind.Sunny, WeatherId = "sunny" };
        foreach (string weather in new[] { "Rain", "Storm", "GreenRain" })
            Check(QueryWeather.Matches(rainy, weather) == true && QueryWeather.Matches(dry, weather) == false, "rain includes storms and green rain");
        foreach (string weather in new[] { "Sun", "Snow", "Wind" })
            Check(QueryWeather.Matches(dry, weather) == true && QueryWeather.Matches(rainy, weather) == false, "legacy sunny means not raining");
        Check(QueryWeather.Matches(rainy with { Kind = WeatherKind.Custom, WeatherId = "CustomBlizzard" }, "CustomBlizzard") == true,
            "custom weather keeps exact identity");
        var clones = sam with { RelatedNpcNames = ["Sam", "Sam·", "SpriteOnly"] };
        var otherMap = clones with { Resolved = clones.Resolved with { Identity = new("Data/Events/CopyTown", "1") } };
        var grouped = new StorySearchIndex(new([], [clones, otherMap], []), _ => "小镇", _ => "山姆",
            characterKey: (_, id) => id.StartsWith("Sam") ? "Sam" : null, locationKey: _ => "Data/Events/Town");
        Check(grouped.Rows.All(row => row.Npcs.SequenceEqual(["Sam"]) && row.Characters == "山姆"), "actor projection removes sprite-only choices and duplicate aliases");
        Check(grouped.Search("", new QueryFilter { Location = "Data/Events/Town" }).Count == 2
            && grouped.Rows.Select(row => row.Identity).Distinct().Count() == 2, "one location filter retains independent underlying event identities");
        var sameName = new StorySearchIndex(new([], [clones, otherMap], []), _ => "同名地点", _ => "Sam");
        string groupKey = sameName.Rows[0].LocationKey;
        Check(sameName.Rows.Select(row => row.LocationKey).Distinct().Count() == 1
            && sameName.Search("", new QueryFilter { Location = groupKey }).Count == 2, "identical location labels form one selectable group across assets");
        Check(sameName.Search("", location: otherMap.AssetName).Single().Event == otherMap, "explicit asset lookup stays exact after display grouping");
        var farmhouse = new Dictionary<string, string> { ["558291/y 3/H"] = "grandpa scene", ["558292/e 558291"] = "re-evaluation" };
        Check(NativeGlobalEventCopies.IsCopy("Data/Events/Cellar8", "558291/y 3/H", "grandpa scene", farmhouse), "native global grandpa copy is not a cellar story");
        Check(!NativeGlobalEventCopies.IsCopy("Data\\Events\\FarmHouse", "558291/y 3/H", "grandpa scene", farmhouse), "original farmhouse global event retained");
        Check(!NativeGlobalEventCopies.IsCopy("Data/Events/Town", "558291/y 3/H", "modded scene", farmhouse)
            && !NativeGlobalEventCopies.IsCopy("Data/Events/Town", "558291/other condition", "grandpa scene", farmhouse)
            && !NativeGlobalEventCopies.IsCopy("Data/Events/Town", "another/y 3/H", "grandpa scene", farmhouse), "different script, condition, and ID are not discarded");
        CheckCanonicalHeartFilters(parser);
        CheckEnvironmentFilters(parser);
        Console.WriteLine("Query upgrade checks passed.");
    }

    private static void CheckEnvironmentFilters(ConditionParser parser)
    {
        string allSpringDays = string.Join(' ', Enumerable.Range(1, 28).Select(day => $"spring {day}"));
        (string Id, string Condition)[] fixtures =
        [
            ("spring-date", "G SEASON_DAY spring 5"),
            ("several-dates", "G SEASON_DAY spring 5 summer 8"),
            ("not-one-date", "!G SEASON_DAY spring 5"),
            ("not-spring", "G !SEASON_DAY " + allSpringDays),
            ("double-negative", "!G !SEASON_DAY spring 5"),
            ("different-day", "u 6/G SEASON_DAY spring 5"),
            ("exclude-only-day", "u 5/G !SEASON_DAY spring 5"),
            ("rain", "G WEATHER Here Rain"),
            ("not-rain", "!G WEATHER Here Rain"),
            ("inner-not-rain", "G !WEATHER Here Rain"),
            ("double-rain", "!G !WEATHER Here Rain"),
            ("target-weather", "G WEATHER Target Rain"),
            ("named-weather", "G WEATHER Town Rain"),
            ("other-location", "G WEATHER IslandWest Rain"),
            ("custom-weather", "G WEATHER Here Author.Blizzard"),
            ("exact-weather-id", "G WEATHER Here Rainy"),
            ("spring-rain", "G SEASON_DAY spring 5, WEATHER Here Rain"),
            ("not-spring-rain", "!G SEASON_DAY spring 5, WEATHER Here Rain"),
            ("fixed-not-spring-rain", "u 5/!G SEASON_DAY spring 5, WEATHER Here Rain"),
            ("unknown-positive", "G WEATHER Here Rain, PLAYER_HAS_MAIL Current letter"),
            ("unknown-negative", "!G WEATHER Here Rain, PLAYER_HAS_MAIL Current letter"),
            ("unsupported", "G WEATHER Here Rain, Author.Unsafe arg"),
            ("time", "t 900 1200"),
            ("not-time", "!t 900 1200"),
            ("unsupported-time", "G TIME 900 1200"),
            ("legacy", "Season spring/w rainy"),
            ("unrestricted", "")
        ];
        GalleryEvent[] stories = fixtures.Select(fixture => new GalleryEvent(
            new(new("Data/Events/Town", fixture.Id), "Town", fixture.Id + "/" + fixture.Condition,
                "none/0 0/farmer 1 1 2/message hello/end", new([], []), "d", "s"), new(OwnershipKind.Excluded, []))).ToArray();
        StorySearchIndex index = new(new([], stories, []), _ => "Translated Town", name => name, parser: parser);
        bool Has(string id, QueryFilter filter) => index.Search("", filter).Any(row => row.EventId == id);
        QueryFilter spring = new() { Season = "spring" }, winter = new() { Season = "winter" };
        QueryFilter rain = new() { Weather = "Rain" }, sun = new() { Weather = "Sun" };
        Check(Has("spring-date", spring) && !Has("spring-date", winter), "GSQ date cannot appear in an incompatible season");
        Check(Has("several-dates", spring) && Has("several-dates", new() { Season = "summer" }) && !Has("several-dates", winter),
            "GSQ date alternatives project to each allowed season");
        Check(Has("not-one-date", spring) && Has("not-one-date", winter) && !Has("not-spring", spring) && Has("not-spring", winter),
            "negating a date does not negate its whole season unless every day is excluded");
        Check(Has("double-negative", spring) && !Has("double-negative", winter), "date query applies both negations once");
        Check(!Has("different-day", spring) && !Has("exclude-only-day", spring) && Has("exclude-only-day", winter),
            "legacy day and GSQ dates share the same candidate date");
        foreach (string id in new[] { "rain", "target-weather", "named-weather", "double-rain" })
            Check(Has(id, rain) && !Has(id, sun), id + " uses the candidate event weather");
        foreach (string id in new[] { "not-rain", "inner-not-rain" })
            Check(!Has(id, rain) && Has(id, sun), id + " excludes rainy candidates");
        Check(Has("other-location", rain) && Has("other-location", sun), "another location's weather stays unknown");
        Check(Has("custom-weather", new() { Weather = "author.blizzard" }) && !Has("custom-weather", rain),
            "GSQ custom weather retains exact identity and query comparison semantics");
        Check(!Has("exact-weather-id", rain), "GSQ weather IDs do not borrow legacy rainy aliases");
        Check(Has("spring-rain", spring with { Weather = "Rain" }) && !Has("spring-rain", spring with { Weather = "Sun" })
            && !Has("spring-rain", winter with { Weather = "Rain" }), "compound date and weather constraints apply together");
        Check(Has("not-spring-rain", spring with { Weather = "Rain" }), "negated conjunction can match on another day in the same season");
        Check(!Has("fixed-not-spring-rain", spring with { Weather = "Rain" }) && Has("fixed-not-spring-rain", spring with { Weather = "Sun" })
            && Has("fixed-not-spring-rain", winter with { Weather = "Rain" }), "outer negation covers the whole conjunction");
        Check(!Has("unknown-positive", sun) && Has("unknown-positive", rain) && Has("unknown-negative", rain),
            "unknown state does not become a proven match inside a negated conjunction");
        Check(Has("unsupported", sun) && Has("unsupported-time", new() { Time = 800 }), "unsupported GSQ remains unknown without running delegates");
        Check(Has("time", new() { Time = 900 }) && Has("time", new() { Time = 1200 }) && !Has("time", new() { Time = 1210 })
            && !Has("not-time", new() { Time = 900 }) && Has("not-time", new() { Time = 1210 }), "legacy time boundaries and negation remain intact");
        Check(Has("legacy", spring with { Weather = "GreenRain" }) && !Has("legacy", winter) && Has("unrestricted", winter with { Weather = "Sun" }),
            "existing legacy and unrestricted environment filters remain compatible");
    }

    private static void CheckCanonicalHeartFilters(ConditionParser parser)
    {
        string[] Split(string value) => value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        GalleryCatalogBuilder builder = new(key => key.Split('/'), script => script.Split('|'), Split, Split, () => null);
        string[] keys = ["legacy/f Marlon 500", "hearts/G PLAYER_HEARTS Current Marlon 2",
            "points/G PLAYER_FRIENDSHIP_POINTS Current Marlon 500", "multiple/f Marlon 500 Sam 2000",
            "merged/f Marlon 250 MarlonFay 750", "hidden/f SpriteOnly 3000"];
        const string script = "none|0 0|farmer 1 1 2|message hello|end";
        ResolvedEvent[] sources = keys.Select(key => new ResolvedEvent(new("Data/Events/Town", key.Split('/')[0]), "Town", key,
            script, new([script], []), "definition", "script")).ToArray();
        GalleryCatalog catalog = builder.Build([], sources).Catalog;
        StorySearchIndex index = new(catalog, entry => entry.LocationName, name => name, parser: parser,
            characterKey: (entry, name) => entry?.AssetName == "Data/Events/Town"
                ? name switch { "Marlon" => "MarlonFay", "SpriteOnly" => null, _ => name } : name);
        Check(index.Search("", new QueryFilter { Npc = "MarlonFay" }).Count == 5,
            "canonical character filter includes every Marlon relationship");
        Check(index.Search("legacy", new QueryFilter { Npc = "MarlonFay", MinimumHearts = 2 }).Count == 1,
            "adding a heart minimum retains the canonical Marlon alias match");
        Check(index.Search("", new QueryFilter { Npc = "MarlonFay", MinimumHearts = 2, MaximumHearts = 2 })
            .Select(row => row.EventId).SequenceEqual(["hearts", "legacy", "multiple", "points"]),
            "canonical heart range includes native queries and excludes unrelated NPC thresholds");
        Check(index.Search("", new QueryFilter { Npc = "MarlonFay", MinimumHearts = 3, MaximumHearts = 3 }).Single().EventId == "merged",
            "multiple raw aliases use the maximum threshold for the same canonical NPC");
        Check(index.Search("", new QueryFilter { Npc = "Sam", MinimumHearts = 8 }).Single().EventId == "multiple"
            && index.Search("", new QueryFilter { Npc = "MarlonFay", MinimumHearts = 8 }).Count == 0,
            "selected canonical NPC does not borrow another subject's heart gate");
        Check(index.Search("", new QueryFilter { MinimumHearts = 12 }).Count == 0,
            "characters rejected by the canonical projection do not supply heart filters");
        StorySearchRow legacy = index.Search("legacy").Single();
        Check(ReferenceEquals(legacy.Event!.Resolved, sources[0]) && legacy.Identity == new EventIdentity("Data/Events/Town", "legacy")
            && legacy.Event.EventKey == "legacy/f Marlon 500" && legacy.Event.Script == script
            && legacy.Requirements.OfType<FriendshipCondition>().Single().Requirements.Single().Npc == "Marlon",
            "canonical evidence leaves event identity, raw key, script and parsed requirements intact");
    }

    private static void Check(bool value, string label) { if (!value) throw new InvalidOperationException(label); }
}
