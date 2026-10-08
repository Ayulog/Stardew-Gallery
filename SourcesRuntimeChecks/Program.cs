using System.Reflection;
using System.Runtime.Loader;
using HarmonyLib;
using StardewGallery;
using StardewModdingAPI;

bool checkGmcm = args is [_, "--gmcm-exclusion", _];
bool checkAssembly = args is [_, "--assembly", _];
bool checkReplay = args is [_, "--replay", _];
bool checkReplayEffects = args is [_, "--replay-effects", _];
bool checkReplayWeather = args is [_, "--replay-weather", _];
bool checkReplaySpeed = args is [_, "--replay-speed", _];
bool checkController = args is [_, "--controller-input", _];
bool checkGalleryDrafts = args is [_, "--gallery-drafts", _];
if (args.Length < 1 || !Directory.Exists(args[0])
    || !(args.Length == 1 || args is [_, "--generic-probe"] || checkGmcm || checkAssembly || checkReplay || checkReplayEffects || checkReplayWeather || checkReplaySpeed || checkGalleryDrafts || checkController))
    throw new ArgumentException("Pass the SMAPI game directory, optionally followed by --gmcm-exclusion <mod-dll> or --generic-probe or --assembly <mod-dll> or --replay <mod-dll> or --replay-effects <mod-dll> or --replay-weather <mod-dll> or --replay-speed <mod-dll> or --gallery-drafts <mod-dll> or --controller-input <mod-dll>; also supply -p:GamePath when building.");
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
if (checkGmcm) GmcmExclusionChecks.Run(AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(args[2])));
else if (checkAssembly) AssemblyLoadChecks.Run(AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(game, "StardewModdingAPI.dll")), game, args[2]);
else if (checkReplay) ReplaySnapshotChecks.Run(AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(args[2])), game);
else if (checkReplayEffects) ReplayEffectChecks.Run(AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(args[2])), game);
else if (checkReplayWeather) ReplayWeatherChecks.Run(AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(args[2])), game);
else if (checkReplaySpeed) ReplaySpeedChecks.Run(AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(args[2])));
else if (checkController) ControllerInputChecks.Run(AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(args[2])));
else if (checkGalleryDrafts) GalleryDraftChecks.Run(AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(args[2])), game);
else if (args.Length == 2) GenericSharingProbe.Run();
else RuntimeChecks.Run(AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(game, "StardewModdingAPI.dll")), game);

internal static class RuntimeChecks
{
    private static int assertions;
    internal static void Run(Assembly smapi, string gameDirectory)
    {
        Console.WriteLine($"SMAPI {smapi.GetName().Version}; Harmony {typeof(Harmony).Assembly.GetName().Version}; CLR {Environment.Version}");
        var fixture = new PipelineFixture(smapi);
        var observer = new SmapiEventSourceObserver("RuntimeChecks.Fixture", (IMonitor)fixture.Monitor);
        observer.Start();
        Check(observer.Enabled, "Observer starts against installed SMAPI: " + observer.Status);
        var localizedFixture = new PipelineFixture(smapi);
        localizedFixture.SetLoaderPack("RuntimeChecks.LocalizedPack");
        localizedFixture.Ready = name => observer.OnAssetReady((IAssetName)name);
        object localizedName = localizedFixture.LocaleName("Data/Events/LocalizedChecks", "zh-CN");
        var localizedResult = (Dictionary<string,string>)localizedFixture.LoadNamed(typeof(Dictionary<string,string>), localizedName);
        EventSourceInfo localizedFound = observer.Lookup(localizedResult, "Data/Events/LocalizedChecks", "1", localizedResult["1"]);
        Check(localizedFound.Status == EventSourceStatus.Complete && localizedFound.Scope.Locale == "zh-CN" && localizedFound.Provider?.UniqueId == "RuntimeChecks.LocalizedPack" && localizedFound.ProviderExecutor?.UniqueId == "RuntimeChecks.Fixture", "Real zh-CN AssetReady.Name preserves locale, content pack and executing framework");
        observer.OnSaveLoaded();
        var afterSave = (Dictionary<string,string>)localizedFixture.LoadNamed(typeof(Dictionary<string,string>), localizedName);
        EventSourceInfo afterSaveInfo = observer.Lookup(afterSave, "Data/Events/LocalizedChecks", "1", afterSave["1"]);
        Check(ReferenceEquals(localizedResult, afterSave) && localizedFixture.LoaderCalls == 1 && localizedFixture.ReadyCalls == 1 && afterSaveInfo.Status == EventSourceStatus.Complete && afterSaveInfo.LoadId == localizedFound.LoadId, "SaveLoaded retains already-cached locale evidence without rerunning resource callbacks");
        observer.Clear();
        var localizedCached = (Dictionary<string,string>)localizedFixture.LoadNamed(typeof(Dictionary<string,string>), localizedName);
        Check(ReferenceEquals(localizedResult, localizedCached) && localizedFixture.ReadyCalls == 1 && localizedFixture.LoaderCalls == 1, "After Clear localized cache hit never raises another AssetReady or loader");
        Check(observer.Lookup(localizedCached, "Data/Events/LocalizedChecks", "1", localizedCached["1"]).Status == EventSourceStatus.Unknown, "Session clear rejects still-cached prior-session localized evidence");
        observer.Invalidate(new[] { (IAssetName)localizedName });
        localizedFixture.Invalidate(((IAssetName)localizedName).Name);
        localizedResult = (Dictionary<string,string>)localizedFixture.LoadNamed(typeof(Dictionary<string,string>), localizedName);
        Check(observer.Lookup(localizedResult, "Data/Events/LocalizedChecks", "1", localizedResult["1"]).Status == EventSourceStatus.Complete && localizedFixture.LoaderCalls == 2 && localizedFixture.ReadyCalls == 2,
            "Natural localized invalidation and reload recover evidence after a session clear");
        var rawFixture = new PipelineFixture(smapi);
        rawFixture.UseGameBaseFiles(Path.Combine(gameDirectory, "Content"));
        rawFixture.Ready = name => observer.OnAssetReady((IAssetName)name);
        var rawName = rawFixture.LocaleName("Data/Events/Town", "zh-CN");
        var rawResult = (Dictionary<string,string>)rawFixture.LoadNamed(typeof(Dictionary<string,string>), rawName);
        var rawFirst = rawResult.First();
        EventSourceInfo rawFound = observer.Lookup(rawResult, "Data/Events/Town", rawFirst.Key, rawFirst.Value);
        Check(rawResult.Count > 0 && rawFound.Status == EventSourceStatus.Complete && rawFound.Provider == EventSourceActor.GameBase && rawFound.Scope.Locale == "zh-CN", "Real Chinese game XNB RawLoad baseline identifies game provider");
        observer.OnSaveLoaded();
        var rawCached = (Dictionary<string,string>)rawFixture.LoadNamed(typeof(Dictionary<string,string>), rawName);
        Check(ReferenceEquals(rawResult, rawCached) && rawFixture.LoaderCalls == 0 && rawFixture.ReadyCalls == 1
            && observer.Lookup(rawCached, "Data/Events/Town", rawFirst.Key, rawFirst.Value).Provider == EventSourceActor.GameBase,
            "SaveLoaded preserves actual cached Chinese game events without reloading XNB");
        const string key = "Data/Events/SourceChecks";
        fixture.Ready = name => observer.OnAssetReady((IAssetName)name);
        fixture.Edit = asset => ((Dictionary<string,string>)PipelineFixture.Data(asset))["1"] = "edited";
        var result = (Dictionary<string,string>)fixture.Load(typeof(Dictionary<string,string>), key);
        EventSourceInfo found = observer.Lookup(result, key, "1", result["1"]);
        Check(found.Status == EventSourceStatus.Complete && found.Provider?.UniqueId == "RuntimeChecks.Fixture", "Real LoadExact publishes accepted loader provenance");
        Check(found.Mutations.Count == 1 && found.Mutations[0].Kind == EventSourceMutationKind.Change, "Real editor delta is observed once");
        Check(fixture.LoaderCalls == 1 && fixture.EditorCalls == 1 && fixture.ReadyCalls == 1 && !fixture.LoadingDuringReady, "Real delegates execute once and AssetReady follows load context exit");
        Check(ReferenceEquals(result, fixture.Cached(key)), "Observed dictionary is actual cache value");
        var cached = fixture.Load(typeof(Dictionary<string,string>), key);
        Check(ReferenceEquals(result, cached) && fixture.LoaderCalls == 1 && fixture.EditorCalls == 1, "Cache hit does not replay callbacks");
        fixture.Invalidate(key);
        result = (Dictionary<string,string>)fixture.Load(typeof(object), key);
        found = observer.Lookup(result, key, "1", result["1"]);
        Check(found.Status == EventSourceStatus.Complete && found.Mutations.Count == 1, "Object request retains concrete dictionary semantics");
        Check(fixture.LoaderCalls == 2 && fixture.EditorCalls == 2, "Cached operation wrappers stay idempotent");
        fixture.Invalidate(key);
        fixture.Edit = asset => { ((Dictionary<string,string>)PipelineFixture.Data(asset))["1"] = "partial editor"; throw new InvalidOperationException("Expected fixture editor failure."); };
        result = (Dictionary<string,string>)fixture.Load(typeof(object), key);
        found = observer.Lookup(result, key, "1", result["1"]);
        Check(result["1"] == "partial editor" && found.Mutations.Single().Failed, "SMAPI retained partial mutation is attributed with failure flag");
        fixture.Invalidate(key);
        fixture.Edit = asset => { ((Dictionary<string,string>)PipelineFixture.Data(asset))["1"] = "retained original"; PipelineFixture.Replace(asset, "incompatible"); };
        result = (Dictionary<string,string>)fixture.Load(typeof(object), key);
        found = observer.Lookup(result, key, "1", result["1"]);
        Check(result["1"] == "retained original" && found.ScriptMatches && found.Mutations.Count == 1, "Invalid replacement rollback preserves accepted mutation evidence");
        fixture.Invalidate(key);
        fixture.Edit = asset => PipelineFixture.Replace(asset, new Dictionary<string,string>{{"1", "replacement"}});
        result = (Dictionary<string,string>)fixture.Load(typeof(object), key);
        Check(observer.Lookup(result, key, "1", result["1"]).Status == EventSourceStatus.Complete, "Valid dictionary replacement is bound to final cache instance");
        fixture.Invalidate(key);
        fixture.Edit = asset => ((Dictionary<string,string>)PipelineFixture.Data(asset))["1"] = "editor output";
        fixture.Ready = name => { ((Dictionary<string,string>)fixture.Cached(key)!)["1"] = "outside mutation"; observer.OnAssetReady((IAssetName)name); };
        result = (Dictionary<string,string>)fixture.Load(typeof(object), key);
        found = observer.Lookup(result, key, "1", result["1"]);
        Check(found.Status == EventSourceStatus.Partial && found.Provider is null, "Earlier AssetReady mutation is not attributed to editor");
        Check(found.Mutations.First().AfterHash == EventHashes.RootScript("editor output") && found.Mutations.Last().Actor is null, "Editor audit preserves snapshot from its own callback");
        fixture.Invalidate(key);
        fixture.Ready = name => observer.OnAssetReady((IAssetName)name);
        result = (Dictionary<string,string>)fixture.Load(typeof(object), key);
        result["1"] = "after ready";
        found = observer.Lookup(result, key, "1", result["1"]);
        Check(found.Status == EventSourceStatus.Partial && found.Provider is null && !found.ScriptMatches, "Later direct cache mutation fails fingerprint lookup");
        fixture.Invalidate(key);
        result = (Dictionary<string,string>)fixture.Load(typeof(object), key, useCache:false);
        Check(observer.Lookup(result, key, "1", result["1"]).Status == EventSourceStatus.Unknown, "Uncached temporary load stays unknown");
        fixture.Invalidate(key);
        result = (Dictionary<string,string>)fixture.Load(typeof(object), key);
        fixture.Invalidate(key);
fixture.Ready = name => { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); observer.OnAssetReady((IAssetName)name); };
result = (Dictionary<string,string>)fixture.Load(typeof(object), key);
Check(observer.Lookup(result, key, "1", result["1"]).Status == EventSourceStatus.Complete, "In-flight evidence survives GC before AssetReady");
fixture.Invalidate(key);
fixture.Edit = asset => PipelineFixture.Replace(asset, new Dictionary<string,string>{{"1", "new dictionary before GC"}});
result = (Dictionary<string,string>)fixture.Load(typeof(object), key);
Check(observer.Lookup(result, key, "1", result["1"]).Status == EventSourceStatus.Complete, "Replacement dictionary retains observation through pre-ready GC");
observer.Invalidate(new[] { (IAssetName)fixture.Name(key) });
Check(observer.Lookup(result, key, "1", result["1"]).Status == EventSourceStatus.Unknown, "Public asset invalidation rejects stale completed evidence");
fixture.Invalidate(key);
fixture.FailAfterCache(true);
int readyBeforeFailure = fixture.ReadyCalls;
try { fixture.Load(typeof(object), key); throw new InvalidOperationException("Fixture should fail during tracking."); }
catch (TargetInvocationException error) when (error.GetBaseException() is NullReferenceException) { }
Check(fixture.Cached(key) is Dictionary<string,string> && fixture.ReadyCalls == readyBeforeFailure, "Fixture failure occurs after cache insertion and before AssetReady");
observer.PruneIncomplete();
fixture.FailAfterCache(false);
result = (Dictionary<string,string>)fixture.Cached(key)!;
Check(observer.Lookup(result, key, "1", result["1"]).Status == EventSourceStatus.Unknown && PendingCount(observer) == 0, "Tick cleanup releases incomplete cached load without publishing evidence");
var packFixture = new PipelineFixture(smapi);
packFixture.SetLoaderPack("RuntimeChecks.ContentPack");
packFixture.Ready = name => observer.OnAssetReady((IAssetName)name);
var packResult = (Dictionary<string,string>)packFixture.Load(typeof(object), "Data/Events/ContentPack");
found = observer.Lookup(packResult, "Data/Events/ContentPack", "1", packResult["1"]);
Check(found.Provider?.UniqueId == "RuntimeChecks.ContentPack" && found.ProviderExecutor?.UniqueId == "RuntimeChecks.Fixture", "SMAPI OnBehalfOf identifies pack separately from executing loader");
var nestedFixture = new PipelineFixture(smapi);
nestedFixture.Ready = name => observer.OnAssetReady((IAssetName)name);
Dictionary<string,string>? innerResult = null;
EventSourceStatus statusInsideOuter = EventSourceStatus.Complete;
nestedFixture.Edit = asset =>
{
    IAssetData typed = (IAssetData)asset;
    if (typed.Name.Name == "Data/Events/Outer")
    {
        observer.OnAssetReady(typed.Name); // premature same-name completion while the real outer load is active
        var outer = (Dictionary<string,string>)typed.Data;
        statusInsideOuter = observer.Lookup(outer, typed.Name.Name, "1", outer["1"]).Status;
        innerResult = (Dictionary<string,string>)nestedFixture.Load(typeof(object), "Data/Events/Inner");
    }
    ((Dictionary<string,string>)typed.Data)["1"] = typed.Name.Name;
};
var outerResult = (Dictionary<string,string>)nestedFixture.Load(typeof(object), "Data/Events/Outer");
Check(statusInsideOuter == EventSourceStatus.Unknown, "Same-name premature AssetReady cannot publish active outer load");
Check(observer.Lookup(outerResult, "Data/Events/Outer", "1", outerResult["1"]).Status == EventSourceStatus.Complete
    && observer.Lookup(innerResult!, "Data/Events/Inner", "1", innerResult!["1"]).Status == EventSourceStatus.Complete,
    "Nested actual LoadExact requests preserve independent completed evidence");
var shared = new Dictionary<string,string>{{"1", "shared definition"}};
var first = new PipelineFixture(smapi);
first.SetIdentity("RuntimeChecks.First");
first.LoadData = () => shared;
first.Ready = name => observer.OnAssetReady((IAssetName)name);
first.Load(typeof(object), "Data/Events/Shared");
Check(observer.Lookup(shared, "Data/Events/Shared", "1", shared["1"]).Provider?.UniqueId == "RuntimeChecks.First", "First shared dictionary context is identifiable");
var second = new PipelineFixture(smapi);
second.SetIdentity("RuntimeChecks.Second");
second.LoadData = () => shared;
second.Ready = name => observer.OnAssetReady((IAssetName)name);
second.Load(typeof(object), "Data/Events/Shared");
Check(observer.Lookup(shared, "Data/Events/Shared", "1", shared["1"]).Status == EventSourceStatus.Unknown, "Shared dictionary across manager scopes stays ambiguous instead of overwriting source");
Check(PendingCount(observer) == 0 && RetainedCount(observer) == 0, "Completed and aborted requests release transient retention");
fixture.Invalidate(key);
fixture.Edit = asset => observer.Invalidate(new[] { ((IAssetData)asset).Name });
result = (Dictionary<string,string>)fixture.Load(typeof(object), key);
Check(observer.Lookup(result, key, "1", result["1"]).Status == EventSourceStatus.Unknown && RetainedCount(observer) == 0,
    "Invalidation inside editor cannot recreate orphaned retention");
fixture.Invalidate(key);
fixture.Edit = _ => observer.Clear();
result = (Dictionary<string,string>)fixture.Load(typeof(object), key);
Check(observer.Lookup(result, key, "1", result["1"]).Status == EventSourceStatus.Unknown && RetainedCount(observer) == 0,
    "Clear inside editor cannot recreate prior-session retention");
observer.Clear();
        Check(observer.Lookup(result, key, "1", result["1"]).Status == EventSourceStatus.Unknown, "Clear rejects prior-session dictionary evidence");
        Console.WriteLine($"PASS: {assertions} real SMAPI pipeline assertions.");
    }
    private static int RetainedCount(SmapiEventSourceObserver observer)
    {
        var retained = (System.Collections.IEnumerable)typeof(SmapiEventSourceObserver).GetField("inFlight", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(observer)!;
        return retained.Cast<object>().Count();
    }
    private static int PendingCount(SmapiEventSourceObserver observer)
    {
        var pending = (System.Collections.IDictionary)typeof(SmapiEventSourceObserver).GetField("pending", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(observer)!;
        return pending.Count;
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("FAIL: " + message);
        assertions++;
        Console.WriteLine("PASS: " + message);
    }
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
