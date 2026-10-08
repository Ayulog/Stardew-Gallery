using System.Reflection;
using System.Runtime.Loader;
using System.Runtime.Serialization;
using HarmonyLib;

/// <summary>Checks real native weather fields and production restoration without graphics or a save.</summary>
internal static class ReplayWeatherChecks
{
    private const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    internal static void Run(Assembly mod, string gameDirectory)
    {
        Assembly game = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(gameDirectory, "Stardew Valley.dll"));
        Type game1 = game.GetType("StardewValley.Game1", true)!;
        Type scopeType = mod.GetType("StardewGallery.ReplaySceneEnvironmentScope", true)!;
        Type snapshotType = scopeType.GetNestedType("WeatherSnapshot", BindingFlags.NonPublic)!;
        MethodInfo capture = snapshotType.GetMethod("Capture", Members)!;
        MethodInfo weatherFor = snapshotType.GetMethod("For", Members)!;
        MethodInfo setWeather = snapshotType.GetMethod("Apply", Members)!;
        MethodInfo sync = scopeType.GetMethod("SyncDefaultWeather", Members)!;
        FieldInfo root = game1.GetField("netWorldState")!;
        object? oldRoot = root.GetValue(null);
        var harmony = new Harmony("StardewGallery.ReplayWeatherChecks");
        int checks = 0;
        try
        {
            // Rendering/audio refresh needs a live game window; weather writes and restoration stay real.
            harmony.Patch(scopeType.GetMethod("RefreshWeather", Members)!,
                prefix: new HarmonyMethod(typeof(ReplayWeatherChecks), nameof(SkipRendering)));
            object world = Activator.CreateInstance(game.GetType("StardewValley.Network.NetWorldState", true)!)!;
            root.SetValue(null, Activator.CreateInstance(root.FieldType, world));
            object weatherMap = world.GetType().GetField("locationWeather", Members)!.GetValue(world)!;
            weatherMap.GetType().GetProperty("Item")!.SetValue(weatherMap, Activator.CreateInstance(game.GetType("StardewValley.Network.LocationWeather", true)!), ["Default"]);
            object weather = world.GetType().GetMethod("GetWeatherForLocation")!.Invoke(world, ["Default"])!;
            foreach (string original in new[] { "Sun", "GreenRain" })
            foreach (string target in new[] { "Sun", "Rain", "Storm", "Snow", "Wind", "GreenRain" })
            {
                object before = weatherFor.Invoke(null, [original])!;
                setWeather.Invoke(before, [weather]);
                sync.Invoke(null, ["Default", before]);
                object scope = FormatterServices.GetUninitializedObject(scopeType);
                scopeType.GetField("contextId", Members)!.SetValue(scope, "Default");
                scopeType.GetField("originalWeather", Members)!.SetValue(scope, capture.Invoke(null, [weather]));
                scopeType.GetMethod("ApplyWeather", Members)!.Invoke(scope, [target]);
                CheckState(weatherFor.Invoke(null, [target])!, $"Apply {original} -> {target}");
                scopeType.GetMethod("Restore", Members)!.Invoke(scope, null);
                CheckState(before, $"Restore {target} -> {original}");
                scopeType.GetMethod("Restore", Members)!.Invoke(scope, null);
                CheckState(before, "Repeated restore remains stable");
            }
            Console.WriteLine($"Replay weather checks passed ({checks} assertions). No game launch or save access.");

            void CheckState(object expected, string message)
            {
                bool matches = expected.Equals(capture.Invoke(null, [weather]));
                foreach (string flag in new[] { "IsRaining", "IsSnowing", "IsLightning", "IsDebrisWeather", "IsGreenRain" })
                {
                    string global = char.ToLowerInvariant(flag[0]) + flag[1..];
                    matches &= Equals(snapshotType.GetProperty(flag)!.GetValue(expected), game1.GetProperty(global, Members)?.GetValue(null) ?? game1.GetField(global, Members)!.GetValue(null));
                }
                checks++;
                if (!matches) throw new InvalidOperationException(message);
                Console.WriteLine("PASS " + message);
            }
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
            root.SetValue(null, oldRoot);
        }
    }
    private static bool SkipRendering() => false;
}
