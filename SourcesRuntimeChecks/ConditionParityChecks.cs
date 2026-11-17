using System.Reflection;
using HarmonyLib;
using StardewValley;
using StardewValley.Network;

/// <summary>
/// Compares the built gallery's precondition reading with the real game code (1.6.15) on one headless player:
/// event IDs, the single '!' prefix, quoted GSQ text, multi-season lists, D/R/O and WEATHER Here.
/// </summary>
internal static class ConditionParityChecks
{
    private const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static Assembly mod = null!;
    private static object parser = null!, evaluator = null!;
    private static int assertions, failures;

    internal static void Run(Assembly assembly)
    {
        mod = assembly;
        var harmony = new Harmony("Gallery.ConditionParityChecks");
        try
        {
            // Only graphics-dependent initialization is skipped; the precondition handlers and player data are real.
            foreach (MethodInfo method in new[]
            {
                typeof(StardewValley.BellsAndWhistles.PlayerStatusList).GetMethod("AddSpriteDefinition")!,
                typeof(Farmer).GetMethod("farmerInit", Members)!,
                typeof(AnimatedSprite).GetMethod("LoadTexture")!
            })
                harmony.Patch(method, prefix: new HarmonyMethod(typeof(ConditionParityChecks), nameof(Skip)));
            Farmer farmer = new();
            typeof(Game1).GetProperty("player", Members)!.SetValue(null, farmer);
            Game1.game1 = (Game1)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Game1));
            var world = new NetWorldState();
            typeof(Game1).GetField("netWorldState")!.SetValue(null, Activator.CreateInstance(typeof(Game1).GetField("netWorldState")!.FieldType, world));
            object weatherMap = typeof(NetWorldState).GetField("locationWeather", Members)!.GetValue(world)!;
            foreach ((string context, string weather, bool raining) in new[] { ("Default", "Sun", false), ("Island", "Rain", true) })
                weatherMap.GetType().GetProperty("Item")!.SetValue(weatherMap, new LocationWeather { Weather = weather, IsRaining = raining }, [context]);
            GameLocation town = Location("Default"), island = Location("Island");
            Game1.currentLocation = town;
            parser = Call(Type("ConditionProduction"), "CreateParser", (Func<string, string[]>)Event.SplitPreconditions, (Func<string, string[]>)ArgUtility.SplitBySpaceQuoteAware)!;
            evaluator = Call(Type("ConditionProduction"), "CreateEvaluator", new object?[] { null })!;

            EventIds();
            foreach (Season season in Enum.GetValues<Season>())
            {
                Game1.season = season;
                foreach (string segment in new[] { "Season spring summer", "Season summer spring", "!Season spring summer", "z spring summer",
                    "z spring summer fall", "NotSeason winter spring", "Season Summer", "Season spring nonsense" })
                    Same(segment, town);
            }
            Game1.season = Season.Summer;
            foreach (string segment in new[] { "!!Season summer", "! Season summer", "!!z spring", "!!D Shane" })
                Malformed(segment, town);

            foreach ((string npc, FriendshipStatus status) in new[] { ("Haley", FriendshipStatus.Dating), ("Shane", FriendshipStatus.Engaged),
                ("Emily", FriendshipStatus.Married), ("Leah", FriendshipStatus.Friendly), ("Penny", FriendshipStatus.Divorced) })
                farmer.friendshipData[npc] = new Friendship { Points = 2000, Status = status };
            farmer.spouse = "Emily";
            foreach (string segment in new[] { "D Haley", "D Shane", "D Emily", "!D Emily", "D Leah", "D Penny", "Dating Nobody",
                "R", "Rf", "O Emily", "o Emily", "O Shane" })
                Same(segment, town);
            farmer.friendshipData["Krobus"] = new Friendship { Points = 2500, Status = FriendshipStatus.Engaged, RoommateMarriage = true };
            farmer.spouse = "Krobus";
            foreach (string segment in new[] { "R", "!R", "Rf", "D Krobus", "O Krobus" })
                Same(segment, town);

            // The game re-quotes a quoted argument containing spaces, which then names no known query.
            foreach (string segment in new[] { "G PLAYER_HEARTS Current Emily 8", "G \"PLAYER_HEARTS Current Emily 8\"", "G PLAYER_HAS_MAIL Current \"a b\"" })
            {
                object condition = Parse(segment);
                string query = (string)Get(condition, "Query")!;
                Check(query == ArgUtility.UnsplitQuoteAware(ArgUtility.SplitBySpaceQuoteAware(segment), ' ', 1), $"'{segment}' keeps the game's query text");
            }
            Same("G PLAYER_HEARTS Current Emily 8", town);
            Same("G !PLAYER_HEARTS Current Emily 9", town);
            Check(Truth("G \"PLAYER_HEARTS Current Emily 8\"", town) == "Unknown" && !Native("G \"PLAYER_HEARTS Current Emily 8\"", town),
                "fully quoted GSQ, which the game rejects, is unknown rather than met");

            // w reads the event location's weather context, not the player's.
            foreach (string segment in new[] { "w rainy", "w sunny", "w Rain", "w rain", "Weather Sun" })
                Same(segment, island);
            // Event preconditions run after the player enters the event location, so Here is that location.
            foreach (string segment in new[] { "G WEATHER Here Rain", "G WEATHER Target Rain", "G WEATHER Here Sun" })
            {
                string gallery = Truth(segment, island);
                Game1.currentLocation = island;
                bool game = Native(segment, island);
                Game1.currentLocation = town;
                Check(gallery == (game ? "True" : "False"), $"'{segment}' at the island matches the game after entering it (gallery {gallery}, game {game})");
            }
            GameStateQueries(harmony, farmer, world, town, island);
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
            Game1.currentLocation = null;
        }
        Console.WriteLine($"Condition parity checks: {assertions - failures}/{assertions} passed; {failures} failed.");
        if (failures != 0) throw new InvalidOperationException("Condition parity regressions failed.");
    }

    private static void EventIds()
    {
        MethodInfo tryGetId = Type("EventKey").GetMethod("TryGetId", Members)!;
        foreach (string key in new[] { "123/f Abigail 500", "123 /f Abigail 500", " 123/f Abigail 500", "/123/f Abigail 500",
            "\"mod/event\"/f Abigail 500", "mod.event/id/condition", "-1/f Abigail 500", "lone" })
        {
            string[] split = Event.SplitPreconditions(key);
            string gameId = split.Length > 0 ? split[0] : "";
            bool plays = !string.IsNullOrEmpty(gameId) && gameId != "-1";
            object?[] args = [key, null];
            bool read = (bool)tryGetId.Invoke(null, args)!;
            Check(read == plays && (!plays || (string)args[1]! == gameId), $"event ID of '{key}' matches checkEventPrecondition ('{gameId}')");
        }
    }

    private static void Same(string segment, GameLocation location)
    {
        bool game = Native(segment, location);
        string gallery = Truth(segment, location);
        Check(gallery == (game ? "True" : "False"), $"{Game1.currentSeason}: '{segment}' gallery {gallery}, game {game}");
    }

    private static void Malformed(string segment, GameLocation location)
        => Check(Truth(segment, location) == "Unknown" && !Native(segment, location), $"'{segment}' names no precondition: unknown in the gallery, false in the game");

    private static bool Native(string segment, GameLocation location) => Event.CheckPrecondition(location, "GalleryParity", segment);

    /// <summary>The production flow of GalleryConditionPresentation.Build for one condition at the event location.</summary>
    private static string Truth(string segment, GameLocation location)
    {
        object condition = Parse(segment);
        object set = parser.GetType().GetMethod("Parse", Members)!.Invoke(parser, [new[] { segment }])!;
        object state = Call(Type("RuntimeStateReader"), "ForLocation", Call(Type("RuntimeStateReader"), "Capture"), location)!;
        state.GetType().GetProperty("Details", Members)!.SetValue(state, Call(Type("ConditionStateReader"), "Capture", set, location));
        object context = state.GetType().GetMethod("ToConditionContext", Members)!.Invoke(state, null)!;
        object evaluation = evaluator.GetType().GetMethod("Evaluate", Members)!.Invoke(evaluator, [condition, context])!;
        return Get(evaluation, "Truth")!.ToString()!;
    }

    private static object Parse(string segment) => parser.GetType().GetMethod("ParseSegment", Members)!.Invoke(parser, [segment])!;

    private static GameLocation Location(string context)
    {
        GameLocation location = new();
        typeof(GameLocation).GetField("locationContextId", Members)!.SetValue(location, context);
        return location;
    }

    private static void GameStateQueries(Harmony harmony, Farmer farmer, NetWorldState world, GameLocation town, GameLocation island)
    {
        // 1. Quote-aware clause and argument splitting parity with 1.6.15 ArgUtility / GameStateQuery.
        Type splitType = Type("GameQuerySplit");
        foreach (string raw in new[]
        {
            "PLAYER_HAS_MAIL Current \"a b\", !SEASON winter",
            "PLAYER_HAS_MAIL Current \"letter, one\", PLAYER_HAS_SEEN_EVENT Current \"evt \\\"alpha\\\"\"",
            "PLAYER_HAS_COOKING_RECIPE Current \"Fried Egg\", TIME 900 1800",
            "\"PLAYER_HEARTS Current Emily 8\""
        })
        {
            string[] galleryClauses = (string[])Call(splitType, "Clauses", raw)!;
            string[] gameClauses = GameStateQuery.SplitRaw(raw);
            Check(galleryClauses.SequenceEqual(gameClauses), $"GameQuerySplit.Clauses matches GameStateQuery.SplitRaw for '{raw}'");
            foreach (string clause in gameClauses)
            {
                string[] galleryArgs = (string[])Call(splitType, "Arguments", clause)!;
                string[] gameArgs = ArgUtility.SplitBySpaceQuoteAware(clause);
                Check(galleryArgs.SequenceEqual(gameArgs), $"GameQuerySplit.Arguments matches ArgUtility.SplitBySpaceQuoteAware for '{clause}'");
            }
        }

        // 2. Populate deterministic player/world state and compare every supported 1.6.15 GSQ against Event.CheckPrecondition.
        harmony.Patch(typeof(Game1).GetProperty("temporaryContent")!.GetGetMethod()!,
            prefix: new HarmonyMethod(typeof(ConditionParityChecks), nameof(Skip)));
        harmony.Patch(typeof(DataLoader).GetMethod("Festivals_FestivalDates")!,
            prefix: new HarmonyMethod(typeof(ConditionParityChecks), nameof(FakeFestivalDates)));
        farmer.stats.DaysPlayed = 40;
        Game1.year = 2;
        Game1.season = Season.Summer;
        Game1.dayOfMonth = 10; // Wednesday (10 % 7 == 3), day before summer11 festival
        Game1.timeOfDay = 1430;
        Game1.uniqueIDForThisGame = 1234567UL;
        Game1.worldStateIDs = ["ccBoilerRoom", "state with space"];
        typeof(Game1).GetField("_locations", Members)!.SetValue(Game1.game1, new List<GameLocation> { town, island });
        town.name.Value = "Town";
        island.name.Value = "IslandSouth";
        typeof(GameLocation).GetField("seasonOverride", Members)!.SetValue(town, new Lazy<Season?>(() => null));
        typeof(GameLocation).GetField("seasonOverride", Members)!.SetValue(island, new Lazy<Season?>(Season.Winter));
        world.GoldenWalnuts = 15;
        world.ActivePassiveFestivals.Add("TroutDerby");

        farmer.Gender = Gender.Female;
        farmer.team.SetIndividualMoney(farmer, 2500);
        farmer.team.totalMoneyEarned.Value = 50000;
        farmer.QiGems = 12;
        farmer.farmingLevel.Value = 6;
        farmer.miningLevel.Value = 4;
        farmer.foragingLevel.Value = 5;
        farmer.fishingLevel.Value = 7;
        farmer.combatLevel.Value = 8;
        farmer.luckLevel.Value = 2;
        typeof(StardewValley.Buffs.BuffManager).GetField("Dirty", Members)!.SetValue(farmer.buffs, false);
        object combinedEffects = typeof(StardewValley.Buffs.BuffManager).GetField("CombinedEffects", Members)!.GetValue(farmer.buffs)!;
        ((StardewValley.Buffs.BuffEffects)combinedEffects).FarmingLevel.Value = 1; // FarmingLevel = 7, Base = 6
        farmer.eventsSeen.Add("99001");
        farmer.eventsSeen.Add("mod event spaced");
        farmer.mailReceived.Add("a b");
        farmer.mailReceived.Add("a \" b");
        farmer.mailbox.Add("inBox");
        farmer.mailForTomorrow.Add("nextDay");
        farmer.activeDialogueEvents["topicOne"] = 3;
        farmer.triggerActionsRun.Add("ruleOne");
        farmer.DialogueQuestionsAnswered.Add("ans_yes");
        farmer.DialogueQuestionsAnswered.Add("ans spaced");
        farmer.craftingRecipes["Chest"] = 1;
        farmer.craftingRecipes["Big Chest"] = 0;
        farmer.cookingRecipes["Fried Egg"] = 1;
        farmer.achievements.Add(5);
        farmer.secretNotesSeen.Add(10);
        farmer.locationsVisited.Add("Town");
        farmer.locationsVisited.Add("Secret Woods");
        farmer.stats.Set("monstersKilled", 42);
        farmer.stats.Set("custom stat", 7);
        farmer.stats.specificMonstersKilled["Green Slime"] = 12;
        farmer.stats.specificMonstersKilled["Bat"] = 5;
        farmer.friendshipData["Mr. Qi"] = new Friendship { Points = 1000, Status = FriendshipStatus.Friendly };
        farmer.houseUpgradeLevel.Value = 2;
        farmer.HasTownKey = true;
        farmer.songsHeard.Add("spring1");
        farmer.team.completedSpecialOrders.Add("Robin2");
        StardewValley.Locations.MineShaft.lowestLevelReached = 45;
        world.LostBooksFound = 21;
        world.IslandVisitors.Add("Gus");
        world.MuseumPieces.Add(new Microsoft.Xna.Framework.Vector2(1, 1), "102");
        world.MuseumPieces.Add(new Microsoft.Xna.Framework.Vector2(2, 1), "103");
        Game1.otherFarmers = new();
        var farm = new Farm { name = { Value = "Farm" } };
        var farmHouse = (StardewValley.Locations.FarmHouse)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(StardewValley.Locations.FarmHouse));
        GameLocation templateLocation = new();
        foreach (FieldInfo field in typeof(GameLocation).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            field.SetValue(farmHouse, field.GetValue(templateLocation));
        farmHouse.name.Value = "FarmHouse";
        farmer.homeLocation.Value = "FarmHouse";
        var islandNorth = new StardewValley.Locations.IslandNorth { name = { Value = "IslandNorth" }, bridgeFixed = { Value = true } };
        var allLocations = (List<GameLocation>)typeof(Game1).GetField("_locations", Members)!.GetValue(Game1.game1)!;
        allLocations.AddRange([farm, farmHouse, islandNorth]);

        string[] segments =
        [
            "G TRUE", "G !TRUE", "G FALSE", "G !FALSE",
            "G SEASON summer fall", "G SEASON spring winter", "G !SEASON winter",
            "G LOCATION_SEASON Target summer", "G LOCATION_SEASON IslandSouth winter", "G LOCATION_SEASON MissingMap summer",
            "G SEASON_DAY spring 1 summer 10", "G SEASON_DAY summer 11",
            "G YEAR 2", "G YEAR 3", "G YEAR 1 2", "G YEAR 1 1",
            "G DAY_OF_MONTH 10 20", "G DAY_OF_MONTH even", "G DAY_OF_MONTH odd",
            "G DAY_OF_WEEK Wed Fri", "G DAY_OF_WEEK Wednesday", "G DAY_OF_WEEK Thu", "G DAY_OF_WEEK 3 10", "G DAY_OF_WEEK 4",
            "G TIME 1200", "G TIME 1600", "G TIME 1200 1600", "G TIME 600 1200",
            "G DAYS_PLAYED 40", "G DAYS_PLAYED 41", "G DAYS_PLAYED 10 40", "G DAYS_PLAYED 10 39",
            "G WEATHER Town Sun", "G WEATHER IslandSouth Rain", "G WEATHER MissingMap Sun",
            "G IS_GREEN_RAIN_DAY", "G !IS_GREEN_RAIN_DAY",
            "G IS_FESTIVAL_DAY", "G IS_FESTIVAL_DAY any 1", "G !IS_FESTIVAL_DAY any 0",
            "G IS_PASSIVE_FESTIVAL_TODAY TroutDerby", "G IS_PASSIVE_FESTIVAL_TODAY NightMarket",
            "G WORLD_STATE_ID ccBoilerRoom", "G WORLD_STATE_ID \"state with space\"", "G WORLD_STATE_ID missingState",
            "G BUILDINGS_CONSTRUCTED All", "G !BUILDINGS_CONSTRUCTED Town Coop 1", "G BUILDINGS_CONSTRUCTED Town All 0 2",
            "G PLAYER_HEARTS Current Emily 8", "G PLAYER_HEARTS Current Any 10", "G PLAYER_HEARTS Current Any 11",
            "G PLAYER_FRIENDSHIP_POINTS Current \"Mr. Qi\" 1000 1000", "G PLAYER_FRIENDSHIP_POINTS Current \"Mr. Qi\" 1001",
            "G PLAYER_NPC_RELATIONSHIP Current Emily Married", "G PLAYER_NPC_RELATIONSHIP Current Krobus Roommate",
            "G PLAYER_NPC_RELATIONSHIP Current Any Engaged", "G PLAYER_NPC_RELATIONSHIP Current Penny Divorced",
            "G PLAYER_HAS_SEEN_EVENT Current 99001", "G PLAYER_HAS_SEEN_EVENT Current \"mod event spaced\"", "G PLAYER_HAS_SEEN_EVENT Current missing",
            "G PLAYER_HAS_MAIL Current \"a b\"", "G PLAYER_HAS_MAIL Current \"a \\\" b\" Received",
            "G PLAYER_HAS_MAIL Current inBox Mailbox", "G PLAYER_HAS_MAIL Current inBox Received",
            "G PLAYER_HAS_MAIL Current nextDay Tomorrow", "G PLAYER_HAS_MAIL Current nextDay Any",
            "G PLAYER_HAS_CONVERSATION_TOPIC Current topicA topicOne", "G PLAYER_HAS_CONVERSATION_TOPIC Current topicMissing",
            "G PLAYER_HAS_RUN_TRIGGER_ACTION Current ruleOne", "G !PLAYER_HAS_RUN_TRIGGER_ACTION Current ruleTwo",
            "G PLAYER_HAS_DIALOGUE_ANSWER Current ans_no ans_yes", "G PLAYER_HAS_DIALOGUE_ANSWER Current \"ans spaced\"", "G PLAYER_HAS_DIALOGUE_ANSWER Current ans_no",
            "G PLAYER_HAS_ITEM Current (O)73 10 20", "G PLAYER_HAS_ITEM Current 73 16",
            "G PLAYER_HAS_ITEM Current (O)858 12 12", "G PLAYER_HAS_ITEM Current (O)74 1",
            "G PLAYER_HAS_CRAFTING_RECIPE Current Chest", "G PLAYER_HAS_CRAFTING_RECIPE Current Big Chest", "G PLAYER_HAS_CRAFTING_RECIPE Current \"Big Chest\"", "G PLAYER_HAS_CRAFTING_RECIPE Current Furnace",
            "G PLAYER_HAS_COOKING_RECIPE Current Fried Egg", "G PLAYER_HAS_COOKING_RECIPE Current \"Fried Egg\"", "G PLAYER_HAS_COOKING_RECIPE Current Salad",
            "G PLAYER_HAS_ACHIEVEMENT Current 5", "G PLAYER_HAS_ACHIEVEMENT Current 6",
            "G PLAYER_HAS_SECRET_NOTE Current 10", "G PLAYER_HAS_SECRET_NOTE Current 11",
            "G PLAYER_VISITED_LOCATION Current Beach \"Secret Woods\"", "G PLAYER_VISITED_LOCATION Current Desert",
            "G PLAYER_GENDER Current Female", "G PLAYER_GENDER Current Male",
            "G PLAYER_CURRENT_MONEY Current 2500", "G PLAYER_CURRENT_MONEY Current 2501", "G PLAYER_CURRENT_MONEY Current 1000 2500",
            "G PLAYER_MONEY_EARNED Current 50000", "G PLAYER_MONEY_EARNED Current 50001",
            "G PLAYER_FARMING_LEVEL Current 7", "G PLAYER_FARMING_LEVEL Current 8", "G PLAYER_BASE_FARMING_LEVEL Current 6 6", "G PLAYER_BASE_FARMING_LEVEL Current 7",
            "G PLAYER_MINING_LEVEL Current 4", "G PLAYER_BASE_MINING_LEVEL Current 5",
            "G PLAYER_FORAGING_LEVEL Current 5", "G PLAYER_BASE_FORAGING_LEVEL Current 5",
            "G PLAYER_FISHING_LEVEL Current 7", "G PLAYER_BASE_FISHING_LEVEL Current 8",
            "G PLAYER_COMBAT_LEVEL Current 8", "G PLAYER_BASE_COMBAT_LEVEL Current 8",
            "G PLAYER_LUCK_LEVEL Current 2", "G PLAYER_BASE_LUCK_LEVEL Current 2 3",
            "G PLAYER_STAT Current monstersKilled 42 50", "G PLAYER_STAT Current \"custom stat\" 7 7", "G PLAYER_STAT Current monstersKilled 43",
            "G IS_COMMUNITY_CENTER_COMPLETE", "G IS_JOJA_MART_COMPLETE",
            "G IS_ISLAND_NORTH_BRIDGE_FIXED", "G !IS_ISLAND_NORTH_BRIDGE_FIXED",
            "G FOUND_ALL_LOST_BOOKS", "G IS_MULTIPLAYER", "G !IS_MULTIPLAYER", "G IS_HOST",
            "G IS_VISITING_ISLAND Gus", "G IS_VISITING_ISLAND Willy",
            "G MUSEUM_DONATIONS 2", "G MUSEUM_DONATIONS 3", "G MUSEUM_DONATIONS 1 2",
            "G MINE_LOWEST_LEVEL_REACHED 40", "G MINE_LOWEST_LEVEL_REACHED 50", "G MINE_LOWEST_LEVEL_REACHED 40 45",
            "G PLAYER_FARMHOUSE_UPGRADE Current 2", "G PLAYER_FARMHOUSE_UPGRADE Current 3", "G PLAYER_FARMHOUSE_UPGRADE Current 1 2",
            "G PLAYER_HAS_MET Current Nobody Emily", "G PLAYER_HAS_MET Current Nobody",
            "G PLAYER_HAS_PET Current", "G !PLAYER_HAS_PET Current",
            "G PLAYER_HAS_CHILDREN Current", "G PLAYER_HAS_CHILDREN Current 0 0", "G !PLAYER_HAS_CHILDREN Current",
            "G PLAYER_HAS_TOWN_KEY Current", "G !PLAYER_HAS_TOWN_KEY Current",
            "G PLAYER_SPECIAL_ORDER_COMPLETE Current Missing Robin2", "G PLAYER_SPECIAL_ORDER_COMPLETE Current Missing",
            "G PLAYER_SPECIAL_ORDER_ACTIVE Current Missing",
            "G PLAYER_HAS_HEARD_SONG Current winter1 spring1", "G PLAYER_HAS_HEARD_SONG Current winter1",
            "G PLAYER_KILLED_MONSTERS Current \"Green Slime\" 12", "G PLAYER_KILLED_MONSTERS Current \"Green Slime\" Bat 17 20",
            "G PLAYER_KILLED_MONSTERS Current \"Green Slime\" 13", "G PLAYER_KILLED_MONSTERS Current Bat",
            "G ANY \"SEASON winter\" \"SEASON summer, TIME 1200 1600\"",
            "G ANY \"SEASON winter\" \"YEAR 5\"",
            "G !ANY \"SEASON winter\" \"YEAR 5\"",
            "G ANY \"PLAYER_HAS_SEEN_EVENT Current missing\" \"ANY \\\"YEAR 5\\\" \\\"PLAYER_HAS_SEEN_EVENT Current 99001\\\"\"",
            "G SEASON summer, TIME 1200 1600, PLAYER_HAS_MAIL Current \"a b\" , !PLAYER_HAS_SEEN_EVENT Current neverSeen"
        ];
        foreach (string segment in segments)
            Same(segment, town);
        Check(Native("G !IS_MULTIPLAYER", town) && Native("G !PLAYER_HAS_PET Current", town) && Native("G PLAYER_HAS_CHILDREN Current 0 0", town),
            "headless world evaluates !IS_MULTIPLAYER, !PLAYER_HAS_PET, and PLAYER_HAS_CHILDREN 0..0 to true without exception fallback");
        var pet = (StardewValley.Characters.Pet)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(StardewValley.Characters.Pet));
        var child = (StardewValley.Characters.Child)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(StardewValley.Characters.Child));
        farmHouse.characters.Add(pet);
        farmHouse.characters.Add(child);
        Same("G PLAYER_HAS_PET Current", town);
        Same("G PLAYER_HAS_CHILDREN Current", town);
        Same("G PLAYER_HAS_CHILDREN Current 1 1", town);
        Same("G PLAYER_HAS_CHILDREN Current 2", town);

        // Also verify IS_COMMUNITY_CENTER_COMPLETE vs JojaMember distinction against real 1.6.15 DefaultResolvers.
        foreach (string ccMail in new[] { "ccBoilerRoom", "ccCraftsRoom", "ccPantry", "ccFishTank", "ccVault", "ccBulletin" })
            farmer.mailReceived.Add(ccMail);
        Same("G IS_COMMUNITY_CENTER_COMPLETE", town);
        farmer.mailReceived.Add("JojaMember");
        Same("G IS_COMMUNITY_CENTER_COMPLETE", town);
        Same("G IS_JOJA_MART_COMPLETE", town);
        // UnsplitQuoteAware glues an unspaced comma onto a quoted argument ("a b,"), which 1.6.15 rejects as malformed.
        Malformed("G PLAYER_HAS_MAIL Current \"a b\", !PLAYER_HAS_SEEN_EVENT Current neverSeen", town);
    }

    private static bool FakeFestivalDates(ref Dictionary<string, string> __result)
    {
        __result = new Dictionary<string, string>(StringComparer.Ordinal) { ["summer11"] = "Luau" };
        return false;
    }

    private static bool Skip() => false;
    private static Type Type(string name) => mod.GetType("StardewGallery." + name, true)!;
    private static object? Call(Type type, string name, params object?[] args)
        => type.GetMethods(Members).Single(method => method.Name == name && method.GetParameters().Length == args.Length).Invoke(null, args);
    private static object? Get(object target, string name) => target.GetType().GetProperty(name, Members)!.GetValue(target);
    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition) failures++;
        Console.WriteLine((condition ? "PASS " : "FAIL ") + message);
    }
}
