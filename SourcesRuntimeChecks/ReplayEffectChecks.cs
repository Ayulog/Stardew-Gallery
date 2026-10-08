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
            Console.WriteLine("Replay effect checks passed (7 assertions). No Mod.Entry, game launch or player save access.");
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
    private static bool GrandpaScore(ref int __result) { __result = 12; return false; }
    private static bool GetFarm(ref object __result) { __result = farm!; return false; }
    private static bool SkipPresentation() => false;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Console.WriteLine("PASS: " + message);
    }
}
