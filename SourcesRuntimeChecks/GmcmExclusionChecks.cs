using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;
using HarmonyLib;
using StardewModdingAPI;

/// <summary>Exercises the built GMCM callbacks and exclusion refresh without starting Mod.Entry or a game loop.</summary>
internal static class GmcmExclusionChecks
{
    private static bool busy;
    private static int refreshes;
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    internal static void Run(Assembly mod)
    {
        int assertions = 0;
        void Check(bool condition, string message)
        {
            assertions++;
            if (!condition) throw new InvalidOperationException(message);
            Console.WriteLine("PASS: " + message);
        }
        string root = Path.Combine(Path.GetTempPath(), "Gallery-GmcmChecks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "assets"));
        File.WriteAllText(Path.Combine(root, "assets", "ai-mod-exclusion.seed.json"), "{\"Seed\":{\"ModId\":\"Author.Seed\"}}");
        Type Type(string name) => mod.GetType("StardewGallery." + name, true)!;
        object Proxy(Type type) => typeof(DispatchProxy).GetMethod("Create")!.MakeGenericMethod(type, typeof(GmcmBoundaryProbe)).Invoke(null, null)!;
        var harmony = new Harmony("Gallery.GmcmExclusionChecks");
        IDisposable? service = null;
        try
        {
            object api = Proxy(Type("IGenericModConfigMenuApi"));
            var probe = (GmcmBoundaryProbe)api;
            object registry = Proxy(typeof(IModRegistry));
            ((GmcmBoundaryProbe)registry).Values["GetApi"] = api;
            object helper = Proxy(typeof(IModHelper));
            var helperProbe = (GmcmBoundaryProbe)helper;
            helperProbe.Values["get_ModRegistry"] = registry;
            helperProbe.ConfigPath = Path.Combine(root, "config.json");
            var entry = (Mod)Activator.CreateInstance(Type("ModEntry"), nonPublic: true)!;
            typeof(Mod).GetProperty("Helper")!.SetValue(entry, helper);
            typeof(Mod).GetProperty("Monitor")!.SetValue(entry, Proxy(typeof(IMonitor)));
            typeof(Mod).GetProperty("ModManifest")!.SetValue(entry, Proxy(typeof(IManifest)));

            var transfer = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            int calls = 0;
            Func<CancellationToken, Task<string>> download = _ => { Interlocked.Increment(ref calls); return transfer.Task; };
            service = (IDisposable)Activator.CreateInstance(Type("AiModExclusionService"), Flags, null,
                new object?[] { root, (Action<string>)(_ => { }), (Action<string>)(_ => { }), download, null }, null)!;
            Set("eventExclusions", service);
            Set("replayService", FormatterServices.GetUninitializedObject(Type("ReplayService")));
            Set("application", FormatterServices.GetUninitializedObject(Type("GalleryApplication")));
            harmony.Patch(Type("ReplayService").GetProperty("IsBusy", Flags)!.GetMethod!,
                prefix: new HarmonyMethod(typeof(GmcmExclusionChecks), nameof(IsBusy)));
            harmony.Patch(Type("GalleryApplication").GetMethod("RefreshCatalog", Flags)!,
                prefix: new HarmonyMethod(typeof(GmcmExclusionChecks), nameof(Refresh)));

            Call("RegisterGmcm");
            var option = probe.Options.SingleOrDefault(item => item.FieldId == "EnableAiModExclusion");
            Check(option is not null, "GMCM registers the AI exclusion option");
            Check(!option!.GetValue(), "GMCM's initial AI exclusion value defaults off");
            Check(probe.Reset is not null && probe.Save is not null, "GMCM receives reset and save callbacks");

            option.SetValue(true);
            Call("UpdateEventExclusions");
            Check(!Enabled() && calls == 0 && refreshes == 0 && !File.Exists(helperProbe.ConfigPath),
                "An unsaved GMCM edit does not download, filter, refresh or persist");
            Call("StartEventExclusions");
            Check(!Enabled() && calls == 0, "Reopening a session ignores an unsaved enabled draft");
            probe.Save!();
            Check(Saved(), "Saving persists the opt-in in standard config.json");
            busy = true;
            Call("UpdateEventExclusions");
            Check(!Enabled() && calls == 0 && refreshes == 0, "A saved change waits while replay or confirmation is busy");
            busy = false;
            Call("UpdateEventExclusions");
            Check(Enabled() && refreshes == 1, "Saved opt-in activates filtering and refreshes the gallery on the next safe tick");
            Check(SpinWait.SpinUntil(() => Volatile.Read(ref calls) == 1, 3000), "Saved opt-in begins one asynchronous list download");

            probe.Save!();
            Call("UpdateEventExclusions");
            Check(calls == 1 && refreshes == 1, "Saving unchanged settings does not restart downloads or refresh");
            transfer.SetResult("{\"Remote\":{\"ModId\":\"Author.Remote\"}}");
            Check(SpinWait.SpinUntil(() => { Call("UpdateEventExclusions"); return refreshes == 2; }, 3000),
                "A downloaded rule change still refreshes through the production update path");

            option.SetValue(false);
            probe.Save!();
            option.SetValue(true); // An edit after Save must not overwrite the saved pending choice.
            Call("UpdateEventExclusions");
            Check(!Saved() && !Enabled() && refreshes == 3,
                "The saved disable restores visibility even if a later draft is enabled");
            probe.Reset!();
            Check(!option.GetValue(), "GMCM reset returns the option to its default-off value");
            probe.Save!();
            Call("UpdateEventExclusions");
            Check(!Enabled() && calls == 1 && refreshes == 3, "Saving reset settings stays disabled without extra work");
            Check(!File.Exists(Path.Combine(root, "ai-mod-exclusion.json")), "GMCM never creates the retired standalone JSON");

            option.SetValue(true);
            probe.Save!();
            Call("UpdateEventExclusions");
            Check(Enabled() && SpinWait.SpinUntil(() => Volatile.Read(ref calls) == 2, 3000), "GMCM can re-enable after a disable");
            probe.Reset!();
            probe.Save!();
            Call("UpdateEventExclusions");
            Check(!Enabled() && !Saved(), "Saving Reset disables an enabled service");

            ((GmcmBoundaryProbe)registry).Values["GetApi"] = null;
            Call("RegisterGmcm");
            Check(!Enabled(), "Missing optional GMCM leaves the mod usable with standard config");
            Console.WriteLine($"All {assertions} GMCM exclusion checks passed. No game launch, Mod.Entry, save access or installation.");

            bool Enabled() => (bool)Type("AiModExclusionService").GetProperty("Enabled", Flags)!.GetValue(service)!;
            bool Saved() { using var json = JsonDocument.Parse(File.ReadAllText(helperProbe.ConfigPath!)); return json.RootElement.GetProperty("EnableAiModExclusion").GetBoolean(); }
            void Set(string name, object value) => Type("ModEntry").GetField(name, Flags)!.SetValue(entry, value);
            void Call(string name) => Type("ModEntry").GetMethod(name, Flags)!.Invoke(entry, null);
        }
        finally
        {
            busy = false;
            service?.Dispose();
            harmony.UnpatchAll(harmony.Id);
            Directory.Delete(root, recursive: true);
        }
    }

    private static bool IsBusy(ref bool __result) { __result = busy; return false; }
    private static bool Refresh() { refreshes++; return false; }
}

public class GmcmBoundaryProbe : DispatchProxy
{
    public sealed record BoolOption(string? FieldId, Func<bool> GetValue, Action<bool> SetValue);
    public Dictionary<string, object?> Values { get; } = new();
    public List<BoolOption> Options { get; } = new();
    public Action? Reset { get; private set; }
    public Action? Save { get; private set; }
    public string? ConfigPath { get; set; }

    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (Values.TryGetValue(method!.Name, out object? result)) return result;
        if (method.Name == "Register") { Reset = (Action)args![1]!; Save = (Action)args[2]!; }
        if (method.Name == "AddBoolOption") Options.Add(new((string?)args![5], (Func<bool>)args[1]!, (Action<bool>)args[2]!));
        if (method.Name == "WriteConfig")
            File.WriteAllText(ConfigPath!, JsonSerializer.Serialize(args![0], args[0]!.GetType()));
        return method.ReturnType != typeof(void) && method.ReturnType.IsValueType ? Activator.CreateInstance(method.ReturnType) : null;
    }
}
