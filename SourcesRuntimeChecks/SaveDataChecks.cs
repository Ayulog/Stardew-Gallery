using System.Reflection;
using System.Runtime.Serialization;
using StardewModdingAPI;
using StardewValley;

/// <summary>
/// Drives the built ModEntry's unlock read/toggle on a headless host and a remote farmhand. SMAPI's save-data helper
/// throws for a remote farmhand, so the fake helper fails the check if it is called there.
/// </summary>
internal static class SaveDataChecks
{
    private const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static int assertions;

    internal static void Run(Assembly mod)
    {
        void Check(bool condition, string message)
        {
            assertions++;
            if (!condition) throw new InvalidOperationException(message);
            Console.WriteLine("PASS " + message);
        }
        Type entryType = mod.GetType("StardewGallery.ModEntry", true)!;
        var entry = (Mod)Activator.CreateInstance(entryType, nonPublic: true)!;
        IModHelper helper = DispatchProxy.Create<IModHelper, FakeProxy>();
        IDataHelper data = DispatchProxy.Create<IDataHelper, SaveData>();
        var saved = (SaveData)(object)data;
        ((FakeProxy)(object)helper).Values["get_Data"] = data;
        typeof(Mod).GetProperty("Helper")!.SetValue(entry, helper);
        typeof(Mod).GetProperty("Monitor")!.SetValue(entry, DispatchProxy.Create<IMonitor, FakeProxy>());
        entryType.GetField("replay", Members)!.SetValue(entry, FormatterServices.GetUninitializedObject(mod.GetType("StardewGallery.ReplayCoordinator", true)!));
        Game1.game1 = (Game1)FormatterServices.GetUninitializedObject(typeof(Game1));
        GameRunner.instance = (GameRunner)FormatterServices.GetUninitializedObject(typeof(GameRunner));
        GameRunner.instance.gameInstances = [Game1.game1];
        typeof(Context).GetProperty(nameof(Context.IsWorldReady))!.SetValue(null, true);
        MethodInfo read = entryType.GetMethod("ReadUnlock", Members)!, toggle = entryType.GetMethod("ToggleUnlock", Members)!;
        object unlock = entryType.GetField("unlockAll", Members)!.GetValue(entry)!;
        PropertyInfo value = unlock.GetType().GetProperty("Value")!;
        byte mode = Game1.multiplayerMode;
        try
        {
            Game1.multiplayerMode = Game1.multiplayerClient;
            Check(!Context.IsOnHostComputer && !(bool)read.Invoke(entry, null)! && saved.Calls.Count == 0, "A remote farmhand reads no save data and starts locked");
            toggle.Invoke(entry, null);
            Check((bool)value.GetValue(unlock)! && saved.Calls.Count == 0, "A remote farmhand's unlock works for the session without writing save data");
            toggle.Invoke(entry, null);
            Check(!(bool)value.GetValue(unlock)!, "A remote farmhand can lock again");

            Game1.multiplayerMode = Game1.singlePlayer;
            saved.Fail = false;
            toggle.Invoke(entry, null);
            Check((bool)value.GetValue(unlock)! && saved.Calls.SequenceEqual(["WriteSaveData:gallery-state"]), "The host saves the unlock under the original key");
            saved.Calls.Clear();
            Check(!(bool)read.Invoke(entry, null)! && saved.Calls.SequenceEqual(["ReadSaveData:gallery-state"]), "The host reads the unlock from its save");
            saved.Fail = true;
            toggle.Invoke(entry, null);
            Check(!(bool)value.GetValue(unlock)! && !(bool)read.Invoke(entry, null)!, "A save-data error from SMAPI is logged, not thrown");
        }
        finally
        {
            Game1.multiplayerMode = mode;
            typeof(Context).GetProperty(nameof(Context.IsWorldReady))!.SetValue(null, false);
        }
        Console.WriteLine($"Save data checks passed ({assertions} assertions).");
    }

    public class SaveData : DispatchProxy
    {
        public List<string> Calls { get; } = [];
        public bool Fail { get; set; } = true;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            Calls.Add(method!.Name + ":" + args?[0]);
            if (Fail) throw new InvalidOperationException("Can't use save data here (fixture).");
            return null;
        }
    }
}
