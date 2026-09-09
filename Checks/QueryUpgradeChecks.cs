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
        Console.WriteLine("Query upgrade checks passed.");
    }
    private static void Check(bool value, string label) { if (!value) throw new InvalidOperationException(label); }
}
