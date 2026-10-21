using System.Reflection;
using System.Runtime.Loader;
using System.Runtime.Serialization;
using HarmonyLib;

/// <summary>Exercises the built replay guard against the installed native GrandpaCandles handler.</summary>
internal static class ReplayEffectChecks
{
    private static object? farm;

    internal static void Run(Assembly mod, string gameDirectory)
    {
        Assembly game = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(gameDirectory, "Stardew Valley.dll"));
        Type eventType = game.GetType("StardewValley.Event", true)!;
        Type farmType = game.GetType("StardewValley.Farm", true)!;
        Type gameType = game.GetType("StardewValley.Game1", true)!;
        Type utility = game.GetType("StardewValley.Utility", true)!;
        Type commands = eventType.GetNestedType("DefaultCommands", BindingFlags.Public)!;
        Type coordinatorType = mod.GetType("StardewGallery.ReplayCoordinator", true)!;
        Type guardType = mod.GetType("StardewGallery.ReplaySaveGuard", true)!;
        const string modId = "StardewGallery.ReplayEffectChecks";
        var fixture = new Harmony(modId + ".Fixture");
        var protection = new Harmony(modId + ".ReplayProtection");
        object coordinator = FormatterServices.GetUninitializedObject(coordinatorType);
        object ownedEvent = FormatterServices.GetUninitializedObject(eventType);
        object otherEvent = FormatterServices.GetUninitializedObject(eventType);
        FieldInfo snapshot = AccessTools.Field(coordinatorType, "snapshot");
        FieldInfo active = AccessTools.Field(coordinatorType, "activeReplayEvent");
        PropertyInfo currentCommand = eventType.GetProperty("CurrentCommand")!;
        FieldInfo memory = eventType.GetField("isMemory")!;
        MethodInfo handler = commands.GetMethod("GrandpaCandles")!;
        FieldInfo content = gameType.GetField("content")!;
        object? previousContent = content.GetValue(null);
        FieldInfo grandpaScore = farmType.GetField("grandpaScore")!;
        object score = Activator.CreateInstance(grandpaScore.FieldType)!;

        PropertyInfo scoreValue = grandpaScore.FieldType.GetProperty("Value")!;
        Console.WriteLine($"Replay effects: built mod {mod.GetName().Version}; game {game.GetName().Version}; real native GrandpaCandles and ReplaySaveGuard.Apply.");
        try
        {
            // Farm's static initializer loads a texture. Provide an in-memory content manager
            // and bypass only that graphics dependency, never the command under test.
            Type texture = game.GetType("StardewValley.Game1", true)!.GetField("mouseCursors")!.FieldType;
            MethodInfo loadTexture = content.FieldType.GetMethods().Single(m => m.Name == "Load" && m.IsGenericMethodDefinition && m.GetParameters().Length == 1).MakeGenericMethod(texture);
            fixture.Patch(loadTexture, prefix: new HarmonyMethod(typeof(ReplayEffectChecks), nameof(SkipPresentation)));
            content.SetValue(null, FormatterServices.GetUninitializedObject(content.FieldType));
            farm = FormatterServices.GetUninitializedObject(farmType);
            grandpaScore.SetValue(farm, score);

            // Score calculation reads the full world, while these other calls require maps,
            // audio and graphics. Keep the handler, its NetInt write and candle conversion real.
            Patch(utility, "getGrandpaScore", nameof(GrandpaScore));
            Patch(gameType, "getFarm", nameof(GetFarm));
            Patch(farmType, "addGrandpaCandles", nameof(SkipPresentation));
            Patch(game.GetType("StardewValley.DelayedAction", true)!, "playSoundAfterDelay", nameof(SkipPresentation));

            Assembly smapi = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(gameDirectory, "StardewModdingAPI.dll"));
            object registry = Proxy(smapi.GetType("StardewModdingAPI.IModRegistry", true)!);
            ((AssemblyLoadProbeProxy)registry).Values["get_ModID"] = modId;
            object helper = Proxy(smapi.GetType("StardewModdingAPI.IModHelper", true)!);
            ((AssemblyLoadProbeProxy)helper).Values["get_ModRegistry"] = registry;
            ((AssemblyLoadProbeProxy)helper).Values["get_Translation"] = Proxy(smapi.GetType("StardewModdingAPI.ITranslationHelper", true)!);
            object monitor = Proxy(smapi.GetType("StardewModdingAPI.IMonitor", true)!);
            AccessTools.Method(guardType, "Apply").Invoke(null, new[] { helper, monitor, coordinator });
            Check((bool)AccessTools.Property(guardType, "IsReady").GetValue(null)!, "ReplaySaveGuard.Apply installed the production protection.");

            // A retained event reference must not be blocked outside an active replay.
            active.SetValue(coordinator, ownedEvent);
            Execute(ownedEvent, initialScore: 1, initialCommand: 7);
            Check(ReadScore() == 4 && ReadCommand(ownedEvent) == 8, "Non-replay native GrandpaCandles changes score and advances the command.");

            snapshot.SetValue(coordinator, FormatterServices.GetUninitializedObject(snapshot.FieldType));
            memory.SetValue(ownedEvent, true);
            Execute(otherEvent, initialScore: 2, initialCommand: 11);
            Check(ReadScore() == 4 && ReadCommand(otherEvent) == 12, "An event not owned by the active replay retains native scoring.");

            Execute(ownedEvent, initialScore: 1, initialCommand: 17);
            Check(ReadScore() == 1, $"Owned replay GrandpaCandles preserves Farm.grandpaScore (actual {ReadScore()}, expected 1).");
            Check(ReadCommand(ownedEvent) == 18, "Suppressed GrandpaCandles advances CurrentCommand exactly once.");
            Check((bool)memory.GetValue(ownedEvent)!, "Suppression preserves the replay memory flag.");
            Check(Harmony.GetPatchInfo(handler)?.Prefixes.Any(p => p.owner == protection.Id && p.PatchMethod.DeclaringType == guardType && p.PatchMethod.Name == "BeforeEffectCommand") == true,
                "GrandpaCandles uses the existing production BeforeEffectCommand protection.");
            int assertions = 7 + CheckWorldGuards(game, mod, coordinator, ownedEvent, otherEvent, protection.Id, fixture, guardType, commands);
            Console.WriteLine($"Replay effect checks passed ({assertions} assertions). No Mod.Entry, game launch or player save access.");
        }
        finally
        {
            protection.UnpatchAll(protection.Id);
            fixture.UnpatchAll(fixture.Id);
            content.SetValue(null, previousContent);
            farm = null;
        }

        void Patch(Type type, string method, string prefix) => fixture.Patch(AccessTools.Method(type, method), prefix: new HarmonyMethod(typeof(ReplayEffectChecks), prefix));
        void Execute(object target, int initialScore, int initialCommand)
        {
            scoreValue.SetValue(score, initialScore);
            currentCommand.SetValue(target, initialCommand);
            handler.Invoke(null, new object?[] { target, new[] { "grandpaCandles" }, null });
        }
        int ReadScore() => (int)scoreValue.GetValue(score)!;
        int ReadCommand(object target) => (int)currentCommand.GetValue(target)!;
    }

    private static object Proxy(Type type) => typeof(DispatchProxy).GetMethod("Create")!.MakeGenericMethod(type, typeof(AssemblyLoadProbeProxy)).Invoke(null, null)!;

    private static int checkActions, openedDoors;

    /// <summary>Door-only DoAction, blocked world triggers and suppressed location event checks during an owned replay.</summary>
    private static int CheckWorldGuards(Assembly game, Assembly mod, object coordinator, object ownedEvent, object otherEvent,
        string protectionId, Harmony fixture, Type guardType, Type commands)
    {
        int count = 0;
        void Assert(bool condition, string message) { count++; Check(condition, message); }
        Type coordinatorType = coordinator.GetType();
        Type locationType = game.GetType("StardewValley.GameLocation", true)!;
        Type contextType = game.GetType("StardewValley.EventContext", true)!;
        PropertyInfo currentCommand = ownedEvent.GetType().GetProperty("CurrentCommand")!;
        FieldInfo locationEvent = locationType.GetField("currentEvent")!;
        // checkAction and openDoor need maps, audio and graphics; count them instead.
        fixture.Patch(AccessTools.Method(locationType, "checkAction"), prefix: new HarmonyMethod(typeof(ReplayEffectChecks), nameof(CountCheckAction)));
        fixture.Patch(AccessTools.Method(locationType, "openDoor"), prefix: new HarmonyMethod(typeof(ReplayEffectChecks), nameof(CountOpenDoor)));
        object location = Activator.CreateInstance(locationType)!;
        object doors = locationType.GetField("interiorDoors")!.GetValue(location)!;
        doors.GetType().GetProperty("Item", [typeof(Microsoft.Xna.Framework.Point)])!.SetValue(doors, false, [new Microsoft.Xna.Framework.Point(3, 4)]);
        MethodInfo doAction = commands.GetMethod("DoAction")!;
        void DoAction(object target, int x, int y, int command)
        {
            string[] args = ["doAction", x.ToString(), y.ToString()];
            currentCommand.SetValue(target, command);
            doAction.Invoke(null, [target, args, Activator.CreateInstance(contextType, target, location, new Microsoft.Xna.Framework.GameTime(), args)]);
        }
        checkActions = openedDoors = 0;
        DoAction(ownedEvent, 3, 4, 20);
        Assert(openedDoors == 1 && checkActions == 0 && (int)currentCommand.GetValue(ownedEvent)! == 21,
            "Owned replay doAction on an interior door only opens the door and advances once.");
        DoAction(ownedEvent, 9, 9, 30);
        Assert(openedDoors == 1 && checkActions == 0 && (int)currentCommand.GetValue(ownedEvent)! == 31,
            "Owned replay doAction elsewhere never runs native checkAction.");
        MethodInfo beforeDoAction = AccessTools.Method(guardType, "BeforeDoAction");
        string[] otherArgs = ["doAction", "3", "4"];
        Assert((bool)beforeDoAction.Invoke(null, [otherEvent, otherArgs, Activator.CreateInstance(contextType, otherEvent, location, new Microsoft.Xna.Framework.GameTime(), otherArgs)])!,
            "Events not owned by the replay keep native doAction.");

        MethodInfo beforeCheck = AccessTools.Method(guardType, "BeforeCheckForEvents");
        locationEvent.SetValue(location, null);
        Assert(!(bool)beforeCheck.Invoke(null, [location])!, "Active replay suppresses location event and festival checks while warping.");
        locationEvent.SetValue(location, ownedEvent);
        Assert((bool)beforeCheck.Invoke(null, [location])!, "The replay's own scheduled event still starts through checkForEvents.");
        MethodInfo raise = game.GetType("StardewValley.Triggers.TriggerActionManager", true)!.GetMethod("Raise")!;
        Assert(Harmony.GetPatchInfo(raise)?.Prefixes.Any(p => p.owner == protectionId && p.PatchMethod.Name == "BeforeMutation") == true
            && !(bool)AccessTools.Method(guardType, "BeforeMutation").Invoke(null, null)!, "Active replay blocks automatic trigger actions.");
        Assert(Harmony.GetPatchInfo(commands.GetMethod("GrandpaEvaluation")!)?.Prefixes.Any(p => p.owner == protectionId) != true
            && Harmony.GetPatchInfo(commands.GetMethod("GrandpaEvaluation2")!)?.Prefixes.Any(p => p.owner == protectionId) != true,
            "Grandpa evaluation dialogue is no longer suppressed.");
        AccessTools.Field(coordinatorType, "snapshot").SetValue(coordinator, null);
        locationEvent.SetValue(location, null);
        Assert((bool)beforeCheck.Invoke(null, [location])! && (bool)AccessTools.Method(guardType, "BeforeMutation").Invoke(null, null)!,
            "Without an active replay, location events and trigger actions stay native.");
        return count;
    }

    private static bool CountCheckAction(ref bool __result) { checkActions++; __result = false; return false; }
    private static bool CountOpenDoor() { openedDoors++; return false; }
    private static bool GrandpaScore(ref int __result) { __result = 12; return false; }
    private static bool GetFarm(ref object __result) { __result = farm!; return false; }
    private static bool SkipPresentation() => false;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Console.WriteLine("PASS: " + message);
    }
}
