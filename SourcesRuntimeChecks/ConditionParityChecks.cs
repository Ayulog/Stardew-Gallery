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
