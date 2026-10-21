using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using StardewValley;

/// <summary>Drives the built coordinator's real update loop without graphics, a save or Mod.Entry.</summary>
internal static class ReplayLifecycleChecks
{
    private static int failSafes;

    internal static void Run(Assembly mod)
    {
        Type coordinatorType = mod.GetType("StardewGallery.ReplayCoordinator", true)!;
        Type snapshotType = mod.GetType("StardewGallery.ReplaySnapshot", true)!;
        var harmony = new Harmony("StardewGallery.ReplayLifecycleChecks");
        int assertions = 0;
        void Check(bool condition, string message)
        {
            assertions++;
            if (!condition) throw new InvalidOperationException(message);
            Console.WriteLine("PASS " + message);
        }
        try
        {
            // The rollback itself (backup copy and SaveGame.Load) is outside this fixture; count requests instead.
            harmony.Patch(AccessTools.Method(coordinatorType, "FailSafe"), prefix: new HarmonyMethod(typeof(ReplayLifecycleChecks), nameof(CountFailSafe)));
            object coordinator = FormatterServices.GetUninitializedObject(coordinatorType);
            AccessTools.Field(coordinatorType, "snapshot").SetValue(coordinator, FormatterServices.GetUninitializedObject(snapshotType));
            AccessTools.Field(coordinatorType, "restoring").SetValue(coordinator, true);
            // A warp home that cannot complete while the unfocused game is paused.
            Game1.locationRequest = (LocationRequest)FormatterServices.GetUninitializedObject(typeof(LocationRequest));
            typeof(Game1).GetField("screenFade", BindingFlags.NonPublic | BindingFlags.Static)!
                .SetValue(null, new StardewValley.BellsAndWhistles.ScreenFade(() => true, () => { }));
            Game1.ticks = 1000;
            // Older builds have no game-tick gate; tolerate that so a negative control can run.
            AccessTools.Field(coordinatorType, "lastGameTick")?.SetValue(coordinator, Game1.ticks);
            MethodInfo update = AccessTools.Method(coordinatorType, "Update");
            for (int i = 0; i < 5000; i++) update.Invoke(coordinator, null);
            Check(failSafes == 0, "UpdateTicked while the game is paused never counts toward the restore timeout");
            for (int i = 0; i < 899; i++) { Game1.ticks++; update.Invoke(coordinator, null); }
            Check(failSafes == 0, "a pending restore waits for 900 advancing game ticks");
            Game1.ticks++; update.Invoke(coordinator, null);
            Check(failSafes == 1, "a genuinely stuck restore still falls back once after 900 game ticks");
            Console.WriteLine($"Replay lifecycle checks passed ({assertions} assertions).");
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
            Game1.locationRequest = null;
        }
    }

    private static bool CountFailSafe(Exception __0)
    {
        failSafes++;
        Console.WriteLine("FailSafe requested: " + __0.GetType().Name + ": " + __0.Message);
        return false;
    }
}
