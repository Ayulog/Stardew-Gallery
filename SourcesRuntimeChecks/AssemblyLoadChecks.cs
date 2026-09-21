using System.Reflection;
using System.Security.Cryptography;
using System.Runtime.Loader;
using System.Text.Json;

/// <summary>Loads a built mod through the installed SMAPI compatibility checker without running its entry point.</summary>
internal static class AssemblyLoadChecks
{
    internal static void Run(Assembly smapi, string gameDirectory, string assemblyPath)
    {
        string game = Path.GetFullPath(gameDirectory);
        string internalFiles = Path.Combine(game, "smapi-internal");
        // The checks project copies referenced SMAPI DLLs beside its executable. Its internal
        // Constants.GamePath therefore points there; supply the actual installation paths below.
        string jsonPath = Path.Combine(internalFiles, "Newtonsoft.Json.dll");
        Assembly jsonAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(jsonPath);
        Console.WriteLine($"JSON dependency: {jsonAssembly.Location}; {jsonAssembly.GetName().Version}");
        string dll = Path.GetFullPath(assemblyPath);
        if (!File.Exists(dll)) throw new FileNotFoundException("Mod assembly to validate was not found.", dll);
        Type loaderType = smapi.GetType("StardewModdingAPI.Framework.ModLoading.AssemblyLoader", true)!;
        ConstructorInfo ctor = loaderType.GetConstructors().Single();
        Type platformType = ctor.GetParameters()[0].ParameterType;
        object monitor = typeof(DispatchProxy).GetMethod("Create")!.MakeGenericMethod(smapi.GetType("StardewModdingAPI.IMonitor", true)!, typeof(AssemblyLoadProbeProxy)).Invoke(null, null)!;
        object metadata = typeof(DispatchProxy).GetMethod("Create")!.MakeGenericMethod(smapi.GetType("StardewModdingAPI.Framework.IModMetadata", true)!, typeof(AssemblyLoadProbeProxy)).Invoke(null, null)!;
        var metadataProxy = (AssemblyLoadProbeProxy)metadata;
        metadataProxy.Values["get_DisplayName"] = "Isolated Gallery compatibility check";
        metadataProxy.Values["get_DirectoryPath"] = Path.GetDirectoryName(dll);

        Console.WriteLine($"SMAPI {smapi.GetName().Version}; CLR {Environment.Version}; target SHA256 {Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(dll)))}");
        Console.WriteLine("Real installed AssemblyLoader; rewriteMods=true; paranoidMode=false; assumeCompatible=false; Mod.Entry is never invoked.");
        using IDisposable loader = (IDisposable)ctor.Invoke(new[] { Enum.Parse(platformType, "Windows"), monitor, false, true, true });
        object resolver = loaderType.GetField("AssemblyDefinitionResolver", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(loader)!;
        MethodInfo addSearchDirectory = resolver.GetType().GetMethod("TryAddSearchDirectory")!;
        addSearchDirectory.Invoke(resolver, new object[] { game });
        addSearchDirectory.Invoke(resolver, new object[] { internalFiles });
        Assembly? loaded = null;
        try
        {
            loaded = (Assembly)loaderType.GetMethod("Load")!.Invoke(loader, new object[] { metadata, new FileInfo(dll), false })!;
        }
        catch (TargetInvocationException error) { throw new InvalidOperationException("SMAPI rejected the mod assembly.", error.GetBaseException()); }
        Console.WriteLine($"PASS: mod DLL accepted by actual SMAPI loader ({loaded!.FullName}); warnings={metadataProxy.WarningValue}.");
        Type originSources = loaded.GetType("StardewGallery.EventDefinitionSources", true)!;
        MethodInfo normalize = originSources.GetMethod("NormalizeJson", BindingFlags.NonPublic | BindingFlags.Static)!;
        string Normalize(string source) => (string)normalize.Invoke(null, new object[] { source })!;
        string permissive = "// leading comment\n{ 123: 'first\nsecond', // event script\n 'when': '2026-09-21T11:00:00Z', }";
        using (JsonDocument parsed = JsonDocument.Parse(Normalize(permissive)))
        {
            if (parsed.RootElement.GetProperty("123").GetString() != "first\nsecond")
                throw new InvalidOperationException("Permissive JSON event script value changed.");
            if (parsed.RootElement.GetProperty("when").GetString() != "2026-09-21T11:00:00Z")
                throw new InvalidOperationException("Date-looking JSON value changed.");
        }
        Console.WriteLine("PASS: rewritten NormalizeJson preserves single quotes, unquoted numeric keys, literal multiline scripts and date-looking strings with leading comments/trailing commas.");
        using (JsonDocument parsed = JsonDocument.Parse(Normalize("/* leading comment */ [ { Changes: [ { Action: 'EditData', Target: 'Data/Events/Town', Entries: { 456: 'pause 100/end' } } ] } ]")))
        {
            if (parsed.RootElement[0].GetProperty("Changes")[0].GetProperty("Entries").GetProperty("456").GetString() != "pause 100/end")
                throw new InvalidOperationException("Array-root Changes definitions changed.");
        }
        Console.WriteLine("PASS: rewritten NormalizeJson accepts array-root Changes definitions and produces strict JSON.");

    }
}
public class AssemblyLoadProbeProxy : DispatchProxy
{
    public Dictionary<string, object?> Values { get; } = new();
    public List<string> Messages { get; } = new();
    public long WarningValue { get; private set; }
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method!.Name == "SetWarning") { WarningValue |= Convert.ToInt64(args![0]); return this; }
        if (method.Name == "RemoveWarning") { WarningValue &= ~Convert.ToInt64(args![0]); return this; }
        if (method.Name == "get_Warnings") return Enum.ToObject(method.ReturnType, WarningValue);
        if (Values.TryGetValue(method.Name, out object? value)) return value;
        if (method.Name is "LogAsMod" or "Log" or "LogOnce")
        {
            string message = args?[0]?.ToString() ?? "";
            Messages.Add(message);
            Console.WriteLine(message);
            return null;
        }
        return method.ReturnType.IsValueType ? Activator.CreateInstance(method.ReturnType) : null;
    }
}