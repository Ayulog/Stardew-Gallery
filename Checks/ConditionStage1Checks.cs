using System.Text.Json;
using StardewGallery;

/// <summary>Pure in-memory checks for the read-only 1.6.15 GSQ subset and GSQ-derived replay scene environment.</summary>
internal static class ConditionStage1Checks
{
    internal static void Run(Func<string, string[]> splitArguments)
    {
        ConditionParser parser = new(
            key => GameQuerySplit.SplitQuoteAware(key, '/', StringSplitOptions.RemoveEmptyEntries, keepQuotesAndEscapes: true),
            GameQuerySplit.Arguments);
        ConditionEvaluator evaluator = new();

        CheckQuotes(parser, evaluator);
        CheckAllQueries(parser, evaluator);
        CheckReferencesAndRelationships(parser);
        CheckReplaySceneEnvironment(parser);
    }

    private static void CheckQuotes(ConditionParser parser, ConditionEvaluator evaluator)
    {
        // Comma inside quotes stays one clause; escaped quotes stay inside the argument.
        Check(SafeGameQuery.TryParse("PLAYER_HAS_MAIL Current \"letter, one\", PLAYER_HAS_SEEN_EVENT Current \"evt \\\"alpha\\\"\"", out var clauses)
            && clauses.Count == 2
            && clauses[0].Arguments.SequenceEqual(["Current", "letter, one"])
            && clauses[1].Arguments.SequenceEqual(["Current", "evt \"alpha\""]),
            "quote-aware GSQ clause and argument splitting");

        // Precondition G re-quotes spaced arguments; an odd raw quote count from \" inside quotes stays valid.
        ConditionExpression escapedMail = parser.ParseSegment("G PLAYER_HAS_MAIL Current \"a \\\" b\"");
        Check(escapedMail is NativeQueryCondition { Query: "PLAYER_HAS_MAIL Current \"a \\\" b\"" }
            && SafeGameQuery.TryParse(((NativeQueryCondition)escapedMail).Query, out var mailClauses)
            && mailClauses.Single().Arguments[1] == "a \" b",
            "escaped quote inside G precondition argument");

        // A fully quoted GSQ names no known query in 1.6.15 and must remain Unknown rather than met.
        ConditionExpression fullyQuoted = parser.ParseSegment("G \"PLAYER_HEARTS Current Emily 8\"");
        ConditionEvaluationContext rich = FullContext([]);
        Check(!SafeGameQuery.TryParse(((NativeQueryCondition)fullyQuoted).Query, out _)
            && evaluator.Evaluate(fullyQuoted, rich).Truth == ConditionTruth.Unknown,
            "fully quoted GSQ remains unknown");

        // Unclosed quotes are rejected both at precondition level and in direct GSQ strings.
        Check(parser.ParseSegment("G PLAYER_HAS_MAIL Current \"unclosed") is OpaqueCondition { Kind: OpaqueConditionKind.MalformedSyntax },
            "unclosed quote in precondition is malformed");
        Check(!SafeGameQuery.TryParse("PLAYER_HAS_MAIL Current \"unclosed", out _)
            && !SafeGameQuery.TryParse("SEASON spring, PLAYER_HAS_MAIL Current \"unclosed", out _),
            "unclosed quote in raw GSQ is rejected");
    }

    private static void CheckAllQueries(ConditionParser parser, ConditionEvaluator evaluator)
    {
        (string Query, bool Expected, bool FactValue)[] cases =
        [
            ("TRUE", true, true),
            ("!TRUE", false, true),
            ("FALSE", false, false),
            ("!FALSE", true, false),
            ("SEASON spring summer", true, true),
            ("SEASON fall winter", false, false),
            ("LOCATION_SEASON Target spring winter", true, true),
            ("LOCATION_SEASON Town fall", false, false),
            ("SEASON_DAY winter 25 spring 12", true, true),
            ("SEASON_DAY spring 13", false, false),
            ("YEAR 2", true, true),
            ("YEAR 3", false, false),
            ("YEAR 1 2", true, true),
            ("YEAR 1 1", false, false),
            ("DAY_OF_MONTH 5 12 20", true, true),
            ("DAY_OF_MONTH even", true, true),
            ("DAY_OF_MONTH odd", false, false),
            ("DAY_OF_WEEK Tue Fri", true, true),
            ("DAY_OF_WEEK Friday", true, true),
            ("DAY_OF_WEEK Tuesday", false, false),
            ("DAY_OF_WEEK 2 9", false, false),
            ("DAY_OF_WEEK 5 12", true, true),
            ("TIME 900", true, true),
            ("TIME 1500", false, false),
            ("TIME 900 1400", true, true),
            ("TIME 600 1100", false, false),
            ("DAYS_PLAYED 40", true, true),
            ("DAYS_PLAYED 41", false, false),
            ("DAYS_PLAYED 10 40", true, true),
            ("DAYS_PLAYED 10 39", false, false),
            ("WEATHER Here Rain Storm", true, true),
            ("WEATHER Target Sun", false, false),
            ("IS_GREEN_RAIN_DAY", true, true),
            ("IS_FESTIVAL_DAY", false, false),
            ("IS_FESTIVAL_DAY Here 1", true, true),
            ("IS_PASSIVE_FESTIVAL_TODAY NightMarket", true, true),
            ("IS_PASSIVE_FESTIVAL_TODAY DesertFestival", false, false),
            ("WORLD_STATE_ID ccBoilerRoom \"custom state\"", true, true),
            ("BUILDINGS_CONSTRUCTED All", true, true),
            ("BUILDINGS_CONSTRUCTED Farm Coop 1 4 true", true, true),
            ("PLAYER_HEARTS Current Emily 8", true, true),
            ("PLAYER_HEARTS Current Any 4 10", true, true),
            ("PLAYER_HEARTS Current AnyDateable 8", false, false),
            ("PLAYER_FRIENDSHIP_POINTS Host \"Mr. Qi\" 500 2500", true, true),
            ("PLAYER_NPC_RELATIONSHIP Current Emily Dating Engaged Married", true, true),
            ("PLAYER_NPC_RELATIONSHIP All Any Roommate", false, false),
            ("PLAYER_HAS_SEEN_EVENT Current 100 \"Mod Event 2\"", true, true),
            ("PLAYER_HAS_MAIL Current \"letter id\" Received", true, true),
            ("PLAYER_HAS_CONVERSATION_TOPIC Any topicA topicB", true, true),
            ("PLAYER_HAS_RUN_TRIGGER_ACTION Host action1", true, true),
            ("PLAYER_HAS_DIALOGUE_ANSWER Current ans1 ans2", true, true),
            ("PLAYER_HAS_ITEM Current (O)74 1 5", true, true),
            ("PLAYER_HAS_CRAFTING_RECIPE Current Chest", true, true),
            ("PLAYER_HAS_CRAFTING_RECIPE Current \"Big Chest\"", true, true),
            ("PLAYER_HAS_COOKING_RECIPE Current Fried Egg", true, true),
            ("PLAYER_HAS_COOKING_RECIPE Current \"Baked Fish\"", false, false),
            ("PLAYER_HAS_ACHIEVEMENT Current 5", true, true),
            ("PLAYER_HAS_SECRET_NOTE Current 10", true, true),
            ("PLAYER_VISITED_LOCATION Current Town Woods", true, true),
            ("PLAYER_GENDER Current Female", true, true),
            ("PLAYER_CURRENT_MONEY Current 500 10000", true, true),
            ("PLAYER_MONEY_EARNED All 50000", true, true),
            ("PLAYER_FARMING_LEVEL Current 5 10", true, true),
            ("PLAYER_MINING_LEVEL Current 4", true, true),
            ("PLAYER_FORAGING_LEVEL Current 3", true, true),
            ("PLAYER_FISHING_LEVEL Current 6", true, true),
            ("PLAYER_COMBAT_LEVEL Current 7", true, true),
            ("PLAYER_LUCK_LEVEL Current 1", false, false),
            ("PLAYER_BASE_FARMING_LEVEL Current 5", true, true),
            ("PLAYER_BASE_MINING_LEVEL Current 4", true, true),
            ("PLAYER_BASE_FORAGING_LEVEL Current 3", true, true),
            ("PLAYER_BASE_FISHING_LEVEL Current 6", true, true),
            ("PLAYER_BASE_COMBAT_LEVEL Current 7", true, true),
            ("PLAYER_BASE_LUCK_LEVEL Current 0 0", true, true),
            ("PLAYER_STAT Current \"steps taken\" 100 500", true, true),
            ("IS_COMMUNITY_CENTER_COMPLETE", true, true),
            ("IS_JOJA_MART_COMPLETE", false, false),
            ("IS_ISLAND_NORTH_BRIDGE_FIXED", true, true),
            ("FOUND_ALL_LOST_BOOKS", true, true),
            ("IS_MULTIPLAYER", false, false),
            ("!IS_MULTIPLAYER", true, false),
            ("IS_HOST", true, true),
            ("IS_VISITING_ISLAND Gus", true, true),
            ("MUSEUM_DONATIONS 10", true, true),
            ("MUSEUM_DONATIONS 5 20 Arch Minerals", true, true),
            ("MUSEUM_DONATIONS 3 Arch", true, true),
            ("MINE_LOWEST_LEVEL_REACHED 40 120", true, true),
            ("PLAYER_FARMHOUSE_UPGRADE Current 2 3", true, true),
            ("PLAYER_HAS_MET Current Abigail Sebastian", true, true),
            ("PLAYER_HAS_PET Current", true, true),
            ("PLAYER_HAS_CHILDREN Current", true, true),
            ("PLAYER_HAS_CHILDREN Current 1 2", true, true),
            ("PLAYER_HAS_TOWN_KEY Current", true, true),
            ("PLAYER_SPECIAL_ORDER_COMPLETE Current Robin2 QiChallenge", true, true),
            ("PLAYER_SPECIAL_ORDER_ACTIVE Current Clint", false, false),
            ("PLAYER_HAS_HEARD_SONG Current spring1", true, true),
            ("PLAYER_KILLED_MONSTERS Current \"Green Slime\" 10 50", true, true),
            ("PLAYER_KILLED_MONSTERS Current Bat", true, true),
            ("ANY \"SEASON fall\" \"SEASON spring, TIME 1000 1400\"", true, true),
            ("!ANY \"SEASON fall\" \"YEAR 5\"", true, true),
            ("ANY \"SEASON fall\" \"ANY \\\"YEAR 5\\\" \\\"SEASON spring\\\"\"", true, true),
            ("SEASON spring, TIME 1000 1400, !PLAYER_HAS_DIALOGUE_ANSWER Current wrongAnswer", true, false)
        ];

        string i18nDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../i18n"));
        var en = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(i18nDir, "default.json")))!;
        var zh = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(i18nDir, "zh.json")))!;
        ConditionDisplayResolver resolver = new(npc => npc, item => item, (group, value) =>
        {
            string key = (group == "relationship" ? "status" : group) + "." + value.ToLowerInvariant();
            return en.TryGetValue(key, out string? found) ? found : value;
        }, time => time.ToString());
        string Translate(Dictionary<string, string> dict, string key, IReadOnlyDictionary<string, string> args)
        {
            Check(dict.TryGetValue(key, out string? template), "missing i18n key: " + key);
            string text = template!;
            foreach (var (k, v) in args) text = text.Replace("{{" + k + "}}", v);
            Check(!text.Contains("{{", StringComparison.Ordinal), $"unresolved placeholder in {key}: {text}");
            return text;
        }

        ConditionEvaluationContext empty = new(null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);
        foreach (var (query, expected, factValue) in cases)
        {
            ConditionExpression expr = parser.ParseSegment("G " + query);
            string rebuilt = ((NativeQueryCondition)expr).Query;
            Check(SafeGameQuery.TryParse(rebuilt, out var clauses), "SafeGameQuery should parse: " + query);
            Dictionary<string, bool?> facts = SafeGameQuery.FlattenLeaves(clauses)
                .Where(c => !SafeGameQuery.UsesSnapshot(c.Name))
                .DistinctBy(SafeGameQuery.FactKey)
                .ToDictionary(SafeGameQuery.FactKey, _ => (bool?)factValue);
            ConditionEvaluationContext ctx = FullContext(facts);
            ConditionTruth expectedTruth = expected ? ConditionTruth.True : ConditionTruth.False;
            ConditionTruth invertedTruth = expected ? ConditionTruth.False : ConditionTruth.True;
            Check(evaluator.Evaluate(expr, ctx).Truth == expectedTruth, "GSQ truth mismatch: " + query);
            Check(evaluator.Evaluate(expr with { Negated = true }, ctx).Truth == invertedTruth, "negated GSQ truth mismatch: " + query);
            if (SafeGameQuery.FlattenLeaves(clauses).All(c => c.Name is not ("TRUE" or "FALSE")))
                Check(evaluator.Evaluate(expr, empty).Knowledge == ConditionKnowledge.MissingData, "missing data not reported: " + query);

            ConditionTextSpec spec = ConditionDescriber.Describe(expr);
            Check(!string.IsNullOrWhiteSpace(ConditionTextFormatter.Format(spec, (k, a) => Translate(en, k, a), resolver)), "en format: " + query);
            Check(!string.IsNullOrWhiteSpace(ConditionTextFormatter.Format(spec, (k, a) => Translate(zh, k, a), resolver)), "zh format: " + query);
        }

        // Read-only boundaries and explicit unknown reasons.
        (string Query, string ReasonKey)[] unsupported =
        [
            ("RANDOM 0.25", "event-detail.unknown.random"),
            ("SYNCED_RANDOM day key 0.5", "event-detail.unknown.random"),
            ("SEASON spring, SYNCED_CHOICE day key 1 2 1", "event-detail.unknown.random"),
            ("Author.CustomModQuery Current 1", "event-detail.unknown.query-custom"),
            ("SEASON spring, OtherMod.Flag true", "event-detail.unknown.query-custom"),
            ("ITEM_ID Target (O)74", "event-detail.unknown.query-unsupported"),
            ("IS_EVENT 123", "event-detail.unknown.query-unsupported"),
            ("EVENT_ID 123", "event-detail.unknown.query-unsupported"),
            ("PLAYER_HEARTS Current Emily nope", "event-detail.unknown.query-unsupported"),
            ("\"PLAYER_HEARTS Current Emily 8\"", "event-detail.unknown.invalid"),
            ("!!SEASON spring", "event-detail.unknown.invalid"),
            ("!", "event-detail.unknown.invalid")
        ];
        foreach (var (query, reasonKey) in unsupported)
        {
            ConditionExpression expr = parser.ParseSegment("G " + query);
            Check(!SafeGameQuery.TryParse(((NativeQueryCondition)expr).Query, out _), "must not parse unsafe/unsupported query: " + query);
            ConditionEvaluation eval = evaluator.Evaluate(expr, FullContext([]));
            Check(eval.Truth == ConditionTruth.Unknown, "must stay unknown: " + query);
            ConditionDisplayItem item = new(expr, eval, query, query, null, null, null);
            Check(ConditionRowPresentation.UnknownReasonKey(item) == reasonKey, $"unknown reason for '{query}' should be {reasonKey}");
        }
        // Raw unclosed-quote GSQ (e.g. from Data/TriggerActions Condition) also reports invalid rather than custom mod query.
        NativeQueryCondition rawUnclosed = new("PLAYER_HAS_MAIL Current \"unclosed", ConditionSource.GameStateQuery, "PLAYER_HAS_MAIL Current \"unclosed", false);
        ConditionEvaluation rawEval = evaluator.Evaluate(rawUnclosed, FullContext([]));
        Check(ConditionRowPresentation.UnknownReasonKey(new(rawUnclosed, rawEval, "", "", null, null, null)) == "event-detail.unknown.invalid",
            "unclosed quote in raw GSQ reports invalid syntax");
    }

    private static void CheckReferencesAndRelationships(ConditionParser parser)
    {
        ConditionSet set = parser.ParseRawKey("900/G SEASON spring, PLAYER_HEARTS Current \"Mr. Qi\" 4, PLAYER_HAS_SEEN_EVENT Current \"evt 1\" 200, !PLAYER_HAS_SEEN_EVENT Current 300");
        RelationshipEvidence single = ConditionRelationshipEvidence.Read(set.Conditions).Single();
        Check(single == new RelationshipEvidence("Mr. Qi", 1000), "quoted NPC heart threshold in multi-clause GSQ");
        Check(ConditionEventReferences.Required(set).SequenceEqual(["evt 1", "200"]), "positive GSQ seen-event dependencies with quotes");
        Check(ConditionEventReferences.Read(set).SequenceEqual(["evt 1", "200", "300"]), "all GSQ seen-event navigation links");

        ConditionSet anyHeart = parser.ParseRawKey("901/G PLAYER_HEARTS Current Any 8/G PLAYER_HEARTS Current AnyDateable 8");
        Check(!ConditionRelationshipEvidence.Read(anyHeart.Conditions).Any(), "Any/AnyDateable heart queries do not claim an NPC album");

        // Recursive ANY event-reference extraction + depth cap (max depth 4) + Tile (X, Y) coordinates.
        ConditionSet anyRefs = parser.ParseRawKey("902/G ANY \"PLAYER_HAS_SEEN_EVENT Current 418172\" \"PLAYER_HAS_SEEN_EVENT Current 963113\" \"!PLAYER_HAS_SEEN_EVENT Current 777\"");
        Check(ConditionEventReferences.Read(anyRefs).SequenceEqual(["418172", "963113", "777"]), "ANY extracts all inner PLAYER_HAS_SEEN_EVENT navigation links");
        Check(ConditionEventReferences.Required(anyRefs).SequenceEqual(["418172", "963113"]), "ANY extracts positive inner PLAYER_HAS_SEEN_EVENT dependencies");

        static string WrapAny(string inner) => $"ANY \"{inner.Replace("\"", "\\\"")}\"";
        string depth4 = WrapAny(WrapAny(WrapAny(WrapAny("SEASON spring"))));
        string depth5 = WrapAny(depth4);
        Check(SafeGameQuery.TryParse(depth4, out _), "ANY up to depth 4 is accepted");
        Check(!SafeGameQuery.TryParse(depth5, out _), "ANY exceeding depth 4 is rejected safely");

        ConditionExpression tileExpr = parser.ParseSegment("a 12 15 13 15");
        Check(tileExpr is TileCondition { Positions.Count: 2 }, "a X Y parses TileCondition");
        ConditionTextSpec tileSpec = ConditionDescriber.Describe(tileExpr);
        Check(tileSpec.LocalizationKey == "condition.tile"
            && tileSpec.Arguments.TryGetValue("positions", out var posVal)
            && posVal is ListTextValue { Values.Count: 2 },
            "TileCondition formats explicit (X, Y) tile coordinates via condition.tile");
    }

    private static void CheckReplaySceneEnvironment(ConditionParser parser)
    {
        // Current environment already in allowed GSQ sets/ranges is kept.
        ReplaySceneEnvironment kept = ReplaySceneEnvironmentResolver.Resolve(
            parser.ParseRawKey("10/G SEASON spring fall, TIME 1000 2000, WEATHER Here Rain Storm").Conditions,
            currentTime: 1450, currentWeather: "Storm", currentSeason: "fall", eventLocation: "Town");
        Check(kept is { Season: "fall", Time: 1450, Weather: "Storm", Warning: null }, "GSQ scene keeps matching season, time and weather");

        // Outside allowed sets/ranges -> picks first supported vanilla value.
        ReplaySceneEnvironment switched = ReplaySceneEnvironmentResolver.Resolve(
            parser.ParseRawKey("11/G LOCATION_SEASON Target summer winter/G TIME 1800/G WEATHER Town Wind Rain").Conditions,
            currentTime: 900, currentWeather: "Sun", currentSeason: "spring", eventLocation: "Town");
        Check(switched is { Season: "summer", Time: 1800, Weather: "Wind", Warning: null }, "GSQ scene picks first allowed season, min time and weather");

        // SEASON_DAY extracts allowed seasons; WEATHER/LOCATION_SEASON for another map is ignored; negated clauses are ignored.
        ReplaySceneEnvironment scoped = ReplaySceneEnvironmentResolver.Resolve(
            parser.ParseRawKey("12/G SEASON_DAY fall 15 winter 1, WEATHER IslandSouth Rain, !SEASON spring, TIME 1900 2200").Conditions,
            currentTime: 800, currentWeather: "Sun", currentSeason: "spring", eventLocation: "Town");
        Check(scoped is { Season: "fall", Time: 1900, Weather: null, Warning: null }, "SEASON_DAY season extracted and foreign location weather ignored");

        // Intersection of legacy and GSQ requirements, and conflict fallback without crashing.
        ReplaySceneEnvironment intersected = ReplaySceneEnvironmentResolver.Resolve(
            parser.ParseRawKey("13/w rainy/G WEATHER Target Storm GreenRain/t 1200 2000/G TIME 1700 2300").Conditions,
            currentTime: 900, currentWeather: "Sun", currentSeason: "summer", eventLocation: "Town");
        Check(intersected is { Weather: "Storm", Time: 1700, Warning: null }, "legacy + GSQ weather/time intersection");

        ReplaySceneEnvironment conflict = ReplaySceneEnvironmentResolver.Resolve(
            parser.ParseRawKey("14/Season spring/G SEASON winter/G WEATHER Here CustomModWeather").Conditions,
            currentTime: 900, currentWeather: "Sun", currentSeason: "summer", eventLocation: "Town");
        Check(conflict.Season is null && conflict.Weather is null && conflict.Warning is not null, "conflicting season and custom GSQ weather warn safely");

        // An unsupported custom weather combined with a vanilla weather keeps current weather instead of forcing Rain.
        ReplaySceneEnvironment customLegacyConflict = ReplaySceneEnvironmentResolver.Resolve(
            parser.ParseRawKey("15/w CustomWeather/w rainy").Conditions,
            currentTime: 900, currentWeather: "Sun", currentSeason: "summer", eventLocation: "Town");
        Check(customLegacyConflict.Weather is null && customLegacyConflict.Warning is not null,
            "custom legacy weather + rainy keeps current weather unchanged");
        ReplaySceneEnvironment customGsqConflict = ReplaySceneEnvironmentResolver.Resolve(
            parser.ParseRawKey("16/G WEATHER Here Festival/G WEATHER Here Rain/G LOCATION_SEASON Target CustomSeason/Season summer").Conditions,
            currentTime: 900, currentWeather: "Sun", currentSeason: "spring", eventLocation: "Town");
        Check(customGsqConflict.Weather is null && customGsqConflict.Season is null && customGsqConflict.Warning is not null,
            "custom GSQ weather/season conflict keeps current scene unchanged");

        // Positive environment clauses inside a mixed G (alongside RANDOM or unlisted vanilla GSQ) still extract scene hints,
        // while condition evaluation itself keeps the mixed G as Unknown.
        ConditionSet mixedGsq = parser.ParseRawKey("17/G SEASON winter, IS_EVENT 123/G WEATHER Here Rain, RANDOM 0.25, TIME 1800 2200");
        ConditionEvaluator evaluator = new();
        Check(mixedGsq.Conditions.All(c => evaluator.Evaluate(c, FullContext([])).Truth == ConditionTruth.Unknown),
            "mixed G with unlisted or random clause still evaluates as Unknown");
        ReplaySceneEnvironment mixedScene = ReplaySceneEnvironmentResolver.Resolve(
            mixedGsq.Conditions, currentTime: 900, currentWeather: "Sun", currentSeason: "spring", eventLocation: "Town");
        Check(mixedScene is { Season: "winter", Time: 1800, Weather: "Rain", Warning: null },
            "positive SEASON/TIME/WEATHER clauses inside mixed G still extract replay scene environment");
    }

    private static ConditionEvaluationContext FullContext(Dictionary<string, bool?> facts)
        => new(
            Season: "spring",
            DayOfMonth: 12,
            Year: 2,
            Time: 1230,
            Weather: "Rain",
            Friendship: new Dictionary<string, int>(),
            EventsSeen: new HashSet<string>(),
            LocalMail: new HashSet<string>(),
            HostMail: new HashSet<string>(),
            HostOrLocalMail: new HashSet<string>(),
            Dating: new HashSet<string>(),
            Spouse: new HashSet<string>(),
            Roommate: false,
            DaysPlayed: 40,
            WorldState: new HashSet<string>(),
            IsRaining: true)
        {
            Details = new ConditionReadState
            {
                Weekday = DayOfWeek.Friday,
                PassiveFestivals = new HashSet<string>(StringComparer.Ordinal) { "NightMarket" },
                PlayerStats = new Dictionary<string, IReadOnlyList<uint>>(StringComparer.Ordinal)
                {
                    [SafeGameQuery.StatKey("Current", "steps taken")] = [250u]
                },
                QueryFacts = facts
            }
        };

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Stage 1 condition check failed: " + message);
    }
}
