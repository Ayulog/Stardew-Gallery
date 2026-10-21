using System.Reflection;
using System.Runtime.Loader;

string[] modes = ["--assembly", "--replay", "--replay-effects", "--replay-weather", "--replay-speed", "--controller-input", "--keyboard-input", "--gallery-drafts", "--conditions", "--save-data"];
if (args is not [_, var selected, _] || !Directory.Exists(args[0]) || !modes.Contains(selected))
    throw new ArgumentException("Pass the SMAPI game directory, then one of " + string.Join(", ", modes)
        + " followed by the built mod DLL; also supply -p:GamePath when building.");
string game = Path.GetFullPath(args[0]);
AssemblyLoadContext.Default.Resolving += (context, name) =>
{
    foreach (string folder in new[] { game, Path.Combine(game, "smapi-internal") })
    {
        string path = Path.Combine(folder, name.Name + ".dll");
        if (File.Exists(path)) return context.LoadFromAssemblyPath(path);
    }
    return null;
};
Assembly LoadMod() => AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(args[2]));
switch (selected)
{
    case "--assembly": AssemblyLoadChecks.Run(AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(game, "StardewModdingAPI.dll")), game, args[2]); break;
    case "--replay": ReplaySnapshotChecks.Run(LoadMod(), game); ReplayLifecycleChecks.Run(LoadMod()); break;
    case "--replay-effects": ReplayEffectChecks.Run(LoadMod(), game); PhotoCaptureChecks.Run(LoadMod()); break;
    case "--replay-weather": ReplayWeatherChecks.Run(LoadMod(), game); break;
    case "--replay-speed": ReplaySpeedChecks.Run(LoadMod()); break;
    case "--controller-input": ControllerInputChecks.Run(LoadMod()); break;
    case "--keyboard-input": ControllerInputChecks.RunKeyboard(LoadMod()); break;
    case "--gallery-drafts": GalleryDraftChecks.Run(LoadMod(), game); break;
    case "--conditions": ConditionParityChecks.Run(LoadMod()); break;
    case "--save-data": SaveDataChecks.Run(LoadMod()); break;
}

public class FakeProxy : DispatchProxy
{
    public Dictionary<string,object?> Values { get; } = new();
    public List<string> Messages { get; } = new();
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (Values.TryGetValue(method!.Name, out object? value)) return value;
        if (method.Name is "LogAsMod" or "Log" or "LogOnce") { Messages.Add(args?[0]?.ToString() ?? ""); return null; }
        return method.ReturnType.IsValueType ? Activator.CreateInstance(method.ReturnType) : null;
    }
}
