using StardewGallery;

internal static class ConditionCoverageChecks
{
    internal static void Run(Func<string, string[]> splitArguments)
    {
        ConditionParser parser = new(key => key.Split('/'), splitArguments);
        ConditionEvaluator evaluator = new();
        ConditionEvaluationContext absent = new(null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);
        ConditionReadState state = new()
        {
            Weekday = DayOfWeek.Tuesday, IsHost = true, EarnedMoney = 100000, Money = 300,
            FreeSlots = 2, CommunityComplete = true, JojaComplete = false, HasPet = false, PetPreference = "Cat",
            Gender = "female", Walnuts = 30, MineBottoms = 2, SecretNotes = new HashSet<int> { -7 },
            DialogueAnswers = new HashSet<string> { "a", "b" }, ActiveDialogues = new HashSet<string> { "active" },
            Shipped = new Dictionary<string, int> { ["24"] = 5, ["188"] = 2 },
            Items = new Dictionary<string, bool> { ["(O)24"] = true, ["missing"] = false },
            Skills = new Dictionary<string, int?> { ["Fishing"] = 1, ["ModSkill"] = null },
            VisibleNpcs = new Dictionary<string, bool> { ["Abigail"] = true, ["Hidden"] = false },
            NpcsAtLocation = new HashSet<string>(), IsFarmHouse = true, HouseUpgrade = 2, SpouseBed = false,
            FestivalDates = new HashSet<string> { "spring13", "summer1" }, PassiveFestivals = new HashSet<string> { "SquidFest" },
            EntryTile = new(4, 7),
            PlayerStats = new Dictionary<string, IReadOnlyList<uint>>
            {
                ["ANY:monstersKilled"] = new uint[] { 5, 1000 }, ["ALL:monstersKilled"] = new uint[] { 5, 1000 },
                ["HOST:monstersKilled"] = new uint[] { 1000 }, ["CURRENT:monstersKilled"] = new uint[] { 5 }
            }
        };
        ConditionEvaluationContext context = absent with { Season = "spring", DayOfMonth = 12, Details = state };
        (string Raw, bool Matches)[] cases =
        [
            ("d Mon Wed Thu Fri Sat Sun", true), ("DayOfWeek Tue Fri", true), ("DayOfWeek Mon", false),
            ("H", true), ("m 100000", true), ("m 100001", false), ("M 301", false), ("c 2", true), ("c 3", false),
            ("C", true), ("X", false), ("J", false), ("h Cat", true), ("h Dog", false), ("g female", true), ("g male", false),
            ("N 30", true), ("N 31", false), ("b 2", true), ("b 3", false), ("S -7", true), ("S 7", false),
            ("q a b", true), ("q a missing", false), ("A active", false), ("A absent", true),
            ("s 24 5 188 2", true), ("s 24 5 188 3", false), ("s missing 0", false),
            ("i (O)24", true), ("i missing", false), ("Skill Fishing 1", true), ("!Skill Fishing 1", false),
            ("v Abigail", true), ("v Hidden", false), ("p Abigail", false), ("L", true), ("L 3", false), ("B", false),
            ("F", true), ("U 1", true), ("U 2", false), ("UpcomingFestival 0", false),
            ("a 1 1 4 7", true), ("a 7 4", false)
        ];
        foreach ((string raw, bool expected) in cases)
        {
            ConditionExpression condition = parser.ParseSegment(raw);
            ConditionEvaluation result = evaluator.Evaluate(condition, context);
            Check(result.Knowledge == ConditionKnowledge.Known && result.Truth == (expected ? ConditionTruth.True : ConditionTruth.False), raw);
            result = evaluator.Evaluate(condition with { Negated = !condition.Negated }, context);
            Check(result.Knowledge == ConditionKnowledge.Known && result.Truth == (expected ? ConditionTruth.False : ConditionTruth.True), "negation: " + raw);
            if (raw != "UpcomingFestival 0")
                Check(evaluator.Evaluate(condition, absent).Knowledge == ConditionKnowledge.MissingData, "missing context: " + raw);
        }
        ConditionEvaluation Eval(string raw, ConditionReadState extra) => evaluator.Evaluate(parser.ParseSegment(raw), context with { Details = extra });
        Check(Eval("h", state with { HasPet = true, PetPreference = null }).Truth == ConditionTruth.False, "existing pet doesn't need a preference");
        Check(Eval("h Cat", state with { HasPet = false, PetPreference = null }).Knowledge == ConditionKnowledge.MissingData, "unknown pet preference");
        Check(Eval("L -1", state with { IsFarmHouse = false, HouseUpgrade = null }).Truth == ConditionTruth.False, "ordinary map never matches house condition");
        Check(Eval("L", state with { IsFarmHouse = null }).Knowledge == ConditionKnowledge.MissingData, "unloaded house is not a known failure");
        Check(Eval("p Abigail", state with { NpcsAtLocation = new HashSet<string> { "Abigail" } }).Truth == ConditionTruth.True, "NPC on target map");
        Check(Eval("p Abigail", state with { NpcsAtLocation = null }).Knowledge == ConditionKnowledge.MissingData, "missing target differs from empty map");
        Check(Eval("a 4 7", state with { EntryTile = null }).Knowledge == ConditionKnowledge.MissingData, "remote coordinate needs entry context");
        Check(Eval("Skill ModSkill 1", state).Knowledge == ConditionKnowledge.Unsupported, "unmapped skill is not zero");
        Check(Eval("i uncaptured", state).Knowledge == ConditionKnowledge.MissingData, "uncaptured item is not absent");
        Check(Eval("M 100", state with { Errors = new HashSet<string> { "M 100" } }).Knowledge == ConditionKnowledge.Error, "read failure is visible");
        Check(evaluator.Evaluate(parser.ParseSegment("U 2"), context with { DayOfMonth = 28 }).Truth == ConditionTruth.False, "upcoming includes next season first day");
        Check(evaluator.Evaluate(parser.ParseSegment("U 1"), context with { DayOfMonth = 28 }).Truth == ConditionTruth.True, "window excludes following day");
        Check(evaluator.Evaluate(parser.ParseSegment("F"), context with { DayOfMonth = 13 }).Truth == ConditionTruth.False, "today active festival");
        Check(Eval("U 2147483647", state with { FestivalDates = new HashSet<string> { "winter1" } }).Truth == ConditionTruth.True,
            "long windows retain 1.6.15 next-season semantics and finish in bounded time");

        (string Query, bool Matches)[] queries =
        [
            ("IS_PASSIVE_FESTIVAL_TODAY SquidFest", true),
            ("!IS_PASSIVE_FESTIVAL_TODAY SquidFest", false),
            ("!IS_PASSIVE_FESTIVAL_TODAY TroutDerby, !SEASON_DAY summer 17 summer 18 summer 19", true),
            ("SEASON_DAY winter 1 spring 12", true),
            ("SEASON_DAY spring 13", false),
            ("PLAYER_STAT Any monstersKilled 1000", true),
            ("PLAYER_STAT All monstersKilled 1000", false),
            ("PLAYER_STAT Host monstersKilled 999 1000", true),
            ("PLAYER_STAT Current monstersKilled 5 5", true),
            ("PLAYER_STAT Current monstersKilled 6", false)
        ];
        foreach ((string query, bool expected) in queries)
        {
            ConditionExpression leaf = parser.ParseSegment("G " + query);
            Check(SafeGameQuery.TryParse(((NativeQueryCondition)leaf).Query, out _), "supported query: " + query);
            Check(evaluator.Evaluate(leaf, context).Truth == (expected ? ConditionTruth.True : ConditionTruth.False), query);
            Check(evaluator.Evaluate(leaf with { Negated = true }, context).Truth == (expected ? ConditionTruth.False : ConditionTruth.True), "outer NOT: " + query);
            Check(evaluator.Evaluate(leaf, absent).Knowledge == ConditionKnowledge.MissingData, "query missing data: " + query);
        }
        foreach (string invalid in new[]
        {
            "RANDOM 0.5", "ANY \"SEASON_DAY spring 12\" \"Mod.Unsafe\"",
            "IS_PASSIVE_FESTIVAL_TODAY SquidFest, Mod.Unsafe", "!!SEASON_DAY spring 12",
            "SEASON_DAY spring", "SEASON_DAY banana 12", "PLAYER_STAT Any monstersKilled nope",
            "PLAYER_STAT Other monstersKilled 1", "IS_PASSIVE_FESTIVAL_TODAY SquidFest extra",
            "PLAYER_STAT Any \"monstersKilled 1\"", "SEASON_DAY spring 12 trailing"
        })
        {
            Check(!SafeGameQuery.TryParse(invalid, out _), "whole unsupported query rejected: " + invalid);
            Check(evaluator.Evaluate(parser.ParseSegment("G " + invalid), context).Truth == ConditionTruth.Unknown, "no partial truth: " + invalid);
        }
        Check(((NativeQueryCondition)parser.ParseSegment("G PLAYER_STAT Any \"monstersKilled 1\"")).Query.Contains('"'), "keep inner GSQ quotes to prevent reinterpretation");
        Check(evaluator.Evaluate(parser.ParseSegment("r 0.05"), context).Truth == ConditionTruth.Unknown, "no random draw");
        Check(evaluator.Evaluate(parser.ParseSegment("x letter"), context).Truth == ConditionTruth.Unknown, "mail action is not a predicate");
        NativePreconditionProbe probe = new(key => key.Split('/'), splitArguments);
        int nativeCalls = 0;
        string Native(string _) { nativeCalls++; return "matched"; }
        Check(probe.Check("id/H", Native, dedicatedServer: true).Status == NativePreconditionProbeStatus.NotSafelyEvaluated
            && nativeCalls == 0, "dedicated host precondition cannot write its checked marker");
        Check(probe.Check("id/!IsHost", Native, dedicatedServer: true).Status == NativePreconditionProbeStatus.NotSafelyEvaluated
            && nativeCalls == 0, "negated host precondition also blocked on dedicated server");
        Check(probe.Check("id/H", Native).Status == NativePreconditionProbeStatus.Matched && nativeCalls == 1, "normal host probe path retained");
        Console.WriteLine("Condition 2.4.0 coverage checks passed.");
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException("2.4.0: " + message);
    }
}
