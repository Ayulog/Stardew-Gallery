using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using StardewModdingAPI;

namespace StardewGallery;

/// <summary>
/// Opt-in SMAPI 4.5.2 prototype. Observe only non-generic boundaries: patching a closed
/// LoadExact/ApplyEditors method changes the generic context of shared reference-type code.
/// </summary>
internal sealed class SmapiEventSourceObserver
{
    private sealed record WrappedOperation(Delegate Wrapper);
    private sealed class ManagerState(long id)
    {
        internal readonly long Id = id;

    }
    private sealed record Binding(EventSourceScope Scope, long LoadId, long Session, int ScreenId, bool Ambiguous = false);
    private sealed record PendingEdit(Dictionary<string, string> Before, Dictionary<string, string>? After, EventSourceActor? Actor,
        EventSourceActor? Executor, bool Failed);
    private sealed class Request(IAssetName name, long session, long revision, int screen)
    {
        internal readonly IAssetName Name = name;
        internal readonly long Session = session;
        internal readonly long Revision = revision;
        internal readonly int Screen = screen;
        internal EventSourceLoad? Load;
        internal object? CandidateData;
        internal EventSourceActor? CandidateActor;
        internal EventSourceActor? CandidateExecutor;
        internal IAssetData? LastAsset;
        internal WeakReference<object>? Manager;
        internal WeakReference<object>? RetainedData;
        internal PendingEdit? Pending;
        internal bool Broken;
    }


    private static SmapiEventSourceObserver? current;
    private readonly EventSourceIndex index = new();
    private readonly ConditionalWeakTable<object, WrappedOperation> wrapped = new();
    private ConditionalWeakTable<object, ManagerState> managers = new();
    private ConditionalWeakTable<object, Request> requests = new();
    private ConditionalWeakTable<object, Request> assetRequests = new();
    private ConditionalWeakTable<object, List<Request>> inFlight = new();
    private readonly ConditionalWeakTable<object, Binding> bindings = new();
    private readonly Dictionary<string, long> revisions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<WeakReference<Request>>> pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly IMonitor monitor;
    private readonly Harmony harmony;
    private readonly object gate = new();
    private long nextManagerId;
    private long session;
    private bool reportedFailure;
    private Type managerType = null!;
    private FieldInfo cacheField = null!;
    private FieldInfo loadingField = null!;
    private PropertyInfo cacheItem = null!;
    private MethodInfo cacheContains = null!;
    private PropertyInfo loads = null!, edits = null!, getData = null!, applyEdit = null!;
    private PropertyInfo loadMod = null!, loadOnBehalfOf = null!, editMod = null!, editOnBehalfOf = null!;

    internal bool Enabled { get; private set; }
    internal string Status { get; private set; } = "Disabled (EnableEventSourceDiagnostics=false).";

    internal SmapiEventSourceObserver(string modId, IMonitor monitor)
    {
        this.monitor = monitor;
        harmony = new Harmony(modId + ".EventSources");
    }

    internal void Start()
    {
        try
        {
            Assembly smapi = typeof(IModHelper).Assembly;
            if (smapi.GetName().Version != new Version(4, 5, 2, 0))
                throw new NotSupportedException($"Expected SMAPI 4.5.2.0, got {smapi.GetName().Version}.");
            managerType = RequireType(smapi, "StardewModdingAPI.Framework.ContentManagers.GameContentManager");
            Type baseManager = RequireType(smapi, "StardewModdingAPI.Framework.ContentManagers.BaseContentManager");
            Type coordinator = RequireType(smapi, "StardewModdingAPI.Framework.ContentCoordinator");
            Type group = RequireType(smapi, "StardewModdingAPI.Framework.Content.AssetOperationGroup");
            Type loader = RequireType(smapi, "StardewModdingAPI.Framework.Content.AssetLoadOperation");
            Type editor = RequireType(smapi, "StardewModdingAPI.Framework.Content.AssetEditOperation");
            Type assetData = RequireType(smapi, "StardewModdingAPI.Framework.Content.AssetDataForObject");
            Type reflector = RequireType(smapi, "StardewModdingAPI.Framework.Reflection.Reflector");


            loads = RequireProperty(group, "LoadOperations", typeof(List<>).MakeGenericType(loader));
            edits = RequireProperty(group, "EditOperations", typeof(List<>).MakeGenericType(editor));
            getData = RequireProperty(loader, "GetData", typeof(Func<IAssetInfo, object>), writable: true);
            applyEdit = RequireProperty(editor, "ApplyEdit", typeof(Action<IAssetData>), writable: true);
            loadMod = RequireProperty(loader, "Mod");
            loadOnBehalfOf = RequireProperty(loader, "OnBehalfOf");
            editMod = RequireProperty(editor, "Mod");
            editOnBehalfOf = RequireProperty(editor, "OnBehalfOf");
            cacheField = baseManager.GetField("Cache", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new NotSupportedException("SMAPI content cache field missing.");
            loadingField = managerType.GetField("AssetsBeingLoaded", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new NotSupportedException("SMAPI loading context field missing.");
            if (!typeof(HashSet<string>).IsAssignableFrom(loadingField.FieldType))
                throw new NotSupportedException("SMAPI loading context changed.");
            cacheItem = RequireProperty(cacheField.FieldType, "Item", typeof(object));
            cacheContains = RequireMethod(cacheField.FieldType, "ContainsKey", typeof(string));
            MethodInfo operations = RequireMethod(coordinator, "GetAssetOperations", typeof(IAssetInfo));

            ConstructorInfo constructor = assetData.GetConstructor(new[] { typeof(IAssetInfo), typeof(object), typeof(Func<string, string>), reflector, typeof(Action<object>) })
                ?? throw new NotSupportedException("SMAPI accepted asset constructor changed.");
            if (operations.ReturnType != group || cacheContains.ReturnType != typeof(bool))
                throw new NotSupportedException("SMAPI content return signatures changed.");
            current = this;
            Patch(operations, postfix: nameof(AfterOperations));
            Patch(constructor, postfix: nameof(AfterAssetConstructed));

            Enabled = true;
            Status = "Observing main Data/Events definitions (experimental, SMAPI 4.5.2).";
            monitor.Log(Status, LogLevel.Debug);
        }
        catch (Exception error)
        {
            Enabled = false;
            try { harmony.UnpatchAll(harmony.Id); } catch { /* Disabled wrappers remain pass-through. */ }
            index.Clear();
            Status = "Unavailable: " + error.Message;
            Report(error);
        }
    }

    internal string DiagnosticStatus
    {
        get
        {
            lock (gate)
                return $"{Status} Observed assets: {index.ObservedAssetCount}; session: {session}.";
        }
    }

    internal void OnSaveLoaded()
    {
        // Event assets are already loaded before SaveLoaded. Clearing here loses their
        // evidence permanently on cache hits, since SMAPI doesn't raise AssetReady again.
        // ReturnedToTitle starts the next session; asset invalidation rejects stale data.
        if (Enabled)
            monitor.Log("Retained event source evidence after save load. " + DiagnosticStatus, LogLevel.Debug);
    }
    internal void Clear()
    {
        lock (gate)
        {
            session++;
            index.Clear();
            revisions.Clear();
            pending.Clear();
            managers = new();
            requests = new();
            assetRequests = new();
            inFlight = new();
        }
    }

    internal void Invalidate(IEnumerable<IAssetName> names)
    {
        lock (gate)
            foreach (IAssetName name in names)
                if (IsTarget(name))
                {
                    string key = Normalize(name.BaseName);
                    revisions[key] = Revision(name) + 1;
                    index.InvalidateAsset(key);
                    foreach (var pair in pending.ToArray())
                    {
                        foreach (WeakReference<Request> reference in pair.Value.ToArray())
                            if (reference.TryGetTarget(out Request? request)
                                && StringComparer.OrdinalIgnoreCase.Equals(Normalize(request.Name.BaseName), key))
                            {
                                Discard(request);
                                pair.Value.Remove(reference);
                            }
                        if (pair.Value.Count == 0) pending.Remove(pair.Key);
                    }
                }
    }

    /// <summary>Never loads or invalidates content; requires evidence for this exact dictionary instance.</summary>
    internal EventSourceInfo Lookup(IReadOnlyDictionary<string, string> dictionary, string assetName, string rawKey, string script)
    {
        lock (gate)
        {
            if (Enabled && bindings.TryGetValue(dictionary, out Binding? binding)
                && !binding.Ambiguous && binding.Session == session && binding.ScreenId == Context.ScreenId
                && StringComparer.OrdinalIgnoreCase.Equals(binding.Scope.AssetName, Normalize(assetName)))
            {
                EventSourceInfo found = index.Lookup(binding.Scope, rawKey, script);
                if (found.LoadId == binding.LoadId) return found;
            }
            return index.Lookup(new EventSourceScope(assetName, "", Context.ScreenId, "unobserved"), rawKey, script);
        }
    }

    private void Patch(MethodBase method, string? prefix = null, string? postfix = null, string? finalizer = null)
    {
        HarmonyMethod? Hook(string? name) => name is null ? null : new HarmonyMethod(typeof(SmapiEventSourceObserver), name);
        harmony.Patch(method, prefix: Hook(prefix), postfix: Hook(postfix), finalizer: Hook(finalizer));
        if (Harmony.GetPatchInfo(method)?.Owners.Contains(harmony.Id) != true)
            throw new InvalidOperationException("Source observation hook missing: " + method.Name);
    }

    private static void AfterOperations(IAssetInfo info, object? __result)
    {
        SmapiEventSourceObserver? observer = current;
        if (observer?.Enabled != true || !IsTarget(info.Name)) return;
        observer.Safe(() =>
        {
            lock (observer.gate)
            {
                // Existence checks may also reach here. No source load is begun or published yet.
                observer.requests.GetValue(info, _ => new Request(info.Name, observer.session, observer.Revision(info.Name), Context.ScreenId));
                if (__result is not null) observer.Wrap(__result);
            }
        });
    }

    private static void AfterAssetConstructed(object __instance, IAssetInfo info, object data, Func<string, string> getNormalizedPath)
    {
        SmapiEventSourceObserver? observer = current;
        if (observer?.Enabled != true || !IsTarget(info.Name)) return;
        observer.Safe(() =>
        {
            lock (observer.gate)
            {
                // In 4.5.2 this delegate targets the actual content manager. This constructor
                // is called only after a loader is accepted, raw load succeeds, or an edit is rejected.
                object? manager = getNormalizedPath.Target;
                if (manager is null || !observer.managerType.IsInstanceOfType(manager)
                    || data is not Dictionary<string, string> dictionary
                    || !observer.requests.TryGetValue(info, out Request? request) || !observer.IsCurrent(request)) return;
                ManagerState state = observer.managers.GetValue(manager, _ => new ManagerState(++observer.nextManagerId));
                if (request.Load is null)
                {
                    var scope = new EventSourceScope(info.Name.BaseName, info.Name.LocaleCode ?? "", request.Screen,
                        $"{request.Session}:{state.Id}");
                    request.Load = observer.index.BeginLoad(scope);
                    bool supplied = ReferenceEquals(request.CandidateData, data);
                    request.Load.RecordBaseline(dictionary, supplied ? request.CandidateActor : EventSourceActor.GameBase,
                        supplied ? request.CandidateExecutor : null);
                    request.CandidateData = null;
                    request.Manager = new WeakReference<object>(manager);
                    if (!observer.pending.TryGetValue(info.Name.Name, out List<WeakReference<Request>>? list))
                        observer.pending[info.Name.Name] = list = new();
                    list.RemoveAll(reference => !reference.TryGetTarget(out Request? old) || !observer.IsCurrent(old));
                    list.Add(new WeakReference<Request>(request));
                }
                else Flush(request, dictionary); // SMAPI reverted an invalid editor replacement.
                request.LastAsset = (IAssetData)__instance;
                observer.assetRequests.Add(__instance, request);
                observer.Retain(request, dictionary);
            }
        });
    }

    /// <summary>Public AssetReady handler. Reads caches only; never loads an asset to obtain evidence.</summary>
    internal void OnAssetReady(IAssetName assetName)
    {
        if (!Enabled || !IsTarget(assetName)) return;
        Safe(() =>
        {
            lock (gate)
            {
                if (!pending.TryGetValue(assetName.Name, out List<WeakReference<Request>>? list)) return;
                foreach (WeakReference<Request> reference in list.ToArray())
                {
                    if (!reference.TryGetTarget(out Request? request) || !IsCurrent(request)
                        || request.Manager?.TryGetTarget(out object? manager) != true || manager is null)
                    {
                        if (request is not null) Discard(request);
                        list.Remove(reference);
                        continue;
                    }
                    // A same-name nested raw load can raise AssetReady while an outer edit is still running.
                    if (((HashSet<string>)loadingField.GetValue(manager)!).Contains(assetName.Name)) continue;
                    list.Remove(reference);
                    object? data = Cached(manager, assetName);
                    if (request.Broken || request.Load is null || data is not Dictionary<string, string> dictionary
                        || !ReferenceEquals(data, request.LastAsset?.Data))
                    {
                        Discard(request); // uncached temporary loads have no final cache evidence
                        continue;
                    }
                    Flush(request); // use saved editor output, never later AssetReady changes
                    if (request.Load.Complete(dictionary))
                    {
                        bool ambiguous = bindings.TryGetValue(data, out Binding? existing)
                            && existing.Session == session && (existing.Ambiguous || existing.Scope != request.Load.Scope);
                        bindings.Remove(data);
                        bindings.Add(data, new Binding(request.Load.Scope, request.Load.LoadId, request.Session, request.Screen, ambiguous));
                    }
                    Release(request);
                    request.LastAsset = null;
                }
                if (list.Count == 0) pending.Remove(assetName.Name);
            }
        });
    }
    // A dictionary that is still in the content pipeline keeps its transient request alive.
    // Ephemeron keys avoid extending the lifetime of an otherwise unreachable failed load.
    private void Retain(Request request, object data)
    {
        Release(request);
        inFlight.GetValue(data, _ => new List<Request>()).Add(request);
        request.RetainedData = new WeakReference<object>(data);
    }

    private void Release(Request request)
    {
        if (request.RetainedData?.TryGetTarget(out object? data) == true
            && inFlight.TryGetValue(data, out List<Request>? retained))
        {
            retained.Remove(request);
            if (retained.Count == 0) inFlight.Remove(data);
        }
        request.RetainedData = null;
    }

    private void Discard(Request request)
    {
        request.Load?.Abort();
        Release(request);
        request.LastAsset = null;
        request.Pending = null;
        request.CandidateData = null;
    }

    /// <summary>Public UpdateTicked cleanup for loads which failed before AssetReady.</summary>
    internal void PruneIncomplete()
    {
        if (!Enabled) return;
        Safe(() =>
        {
            lock (gate)
            {
                foreach (var pair in pending.ToArray())
                {
                    foreach (WeakReference<Request> reference in pair.Value.ToArray())
                    {
                        if (!reference.TryGetTarget(out Request? request))
                        {
                            pair.Value.Remove(reference);
                            continue;
                        }
                        if (IsCurrent(request) && request.Manager?.TryGetTarget(out object? manager) == true
                            && manager is not null && ((HashSet<string>)loadingField.GetValue(manager)!).Contains(request.Name.Name))
                            continue;
                        Discard(request);
                        pair.Value.Remove(reference);
                    }
                    if (pair.Value.Count == 0) pending.Remove(pair.Key);
                }
            }
        });
    }
    private object? Cached(object manager, IAssetName name)
    {
        object cache = cacheField.GetValue(manager)!;
        return (bool)cacheContains.Invoke(cache, new object[] { name.Name })! ? cacheItem.GetValue(cache, new object[] { name.Name }) : null;
    }

    private void Wrap(object group)
    {
        foreach (object operation in (IEnumerable)loads.GetValue(group)!)
        {
            var original = (Func<IAssetInfo, object>)getData.GetValue(operation)!;
            if (wrapped.TryGetValue(operation, out WrappedOperation? previous) && ReferenceEquals(previous.Wrapper, original)) continue;
            EventSourceActor? executor = Actor(loadMod.GetValue(operation));
            EventSourceActor? actor = Actor(loadOnBehalfOf.GetValue(operation)) ?? executor;
            Func<IAssetInfo, object> wrapper = info =>
            {
                object result = original(info); // exactly once; exceptions remain SMAPI's responsibility
                Safe(() =>
                {
                    lock (gate)
                    {
                        if (!Enabled || !requests.TryGetValue(info, out Request? request) || !IsCurrent(request)) return;
                        request.CandidateData = result;
                        request.CandidateActor = actor;
                        request.CandidateExecutor = executor;
                    }
                });
                return result;
            };
            getData.SetValue(operation, wrapper);
            wrapped.Remove(operation);
            wrapped.Add(operation, new WrappedOperation(wrapper));
        }
        foreach (object operation in (IEnumerable)edits.GetValue(group)!)
        {
            var original = (Action<IAssetData>)applyEdit.GetValue(operation)!;
            if (wrapped.TryGetValue(operation, out WrappedOperation? previous) && ReferenceEquals(previous.Wrapper, original)) continue;
            EventSourceActor? executor = Actor(editMod.GetValue(operation));
            EventSourceActor? actor = Actor(editOnBehalfOf.GetValue(operation)) ?? executor;
            Action<IAssetData> wrapper = asset =>
            {
                Request? request = null;
                Dictionary<string, string>? before = null;
                Safe(() =>
                {
                    lock (gate)
                    {
                        if (!Enabled || !assetRequests.TryGetValue(asset, out request) || !IsCurrent(request) || request.Broken) return;
                        Flush(request);
                        if (asset.Data is Dictionary<string, string> dictionary) before = new(dictionary, StringComparer.Ordinal);
                        request.LastAsset = asset;
                    }
                }, request);
                bool failed = true;
                try
                {
                    original(asset);
                    failed = false;
                }
                finally
                {
                    if (before is not null && request is not null)
                        Safe(() =>
                        {
                            lock (gate)
                            {
                                if (!IsCurrent(request) || request.Broken)
                                {
                                    Discard(request);
                                    return;
                                }
                                var after = asset.Data as Dictionary<string, string>;
                                request.Pending = new PendingEdit(before,
                                    after is not null ? new(after, StringComparer.Ordinal) : null,
                                    actor, executor, failed);
                                if (after is not null) Retain(request, after);
                            }
                        }, request);
                }
            };
            applyEdit.SetValue(operation, wrapper);
            wrapped.Remove(operation);
            wrapped.Add(operation, new WrappedOperation(wrapper));
        }
    }

    private static void Flush(Request request, Dictionary<string, string>? revertedData = null)
    {
        if (request.Pending is not { } pending || request.Load is null) return;
        request.Pending = null;
        if ((revertedData ?? pending.After) is { } after)
            request.Load.ObserveEdit(pending.Before, after, pending.Actor, pending.Executor, pending.Failed);
        else request.Load.MarkPartial("Editor result is not a supported string dictionary.");
    }

    private long Revision(IAssetName name) => revisions.GetValueOrDefault(Normalize(name.BaseName));
    private bool IsCurrent(Request request)
        => Enabled && request.Session == session && request.Revision == Revision(request.Name) && request.Screen == Context.ScreenId;
    private static EventSourceActor? Actor(object? metadata)
        => metadata is IModInfo mod ? new EventSourceActor(mod.Manifest.UniqueID, mod.Manifest.Name, mod.Manifest.Version.ToString()) : null;
    private static string Normalize(string name) => name.Replace('\\', '/');
    private static bool IsTarget(IAssetName name)
        => Normalize(name.BaseName).StartsWith("Data/Events/", StringComparison.OrdinalIgnoreCase);

    private void Safe(Action action, Request? request = null)
    {
        try { action(); }
        catch (Exception error)
        {
            if (request is not null) request.Broken = true;
            // A failed observation could miss an operation. Disable all evidence, keeping wrappers pass-through.
            Enabled = false;
            index.Clear();
            lock (gate)
            {
                pending.Clear();
                inFlight = new();
                requests = new();
                assetRequests = new();
            }
            Status = "Unavailable: " + error.Message;
            Report(error);
        }
    }

    private void Report(Exception error)
    {
        if (reportedFailure) return;
        reportedFailure = true;
        try { monitor.Log("Event source observation unavailable; gallery/replay remain available. " + error, LogLevel.Warn); }
        catch { /* Logging must not change the resource pipeline. */ }
    }

    private static Type RequireType(Assembly assembly, string name) => assembly.GetType(name, throwOnError: true)!;
    private static PropertyInfo RequireProperty(Type type, string name, Type? expected = null, bool writable = false)
    {
        PropertyInfo? property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property?.GetMethod is null || expected is not null && property.PropertyType != expected || writable && property.SetMethod is null)
            throw new NotSupportedException("Unsupported SMAPI member: " + type.Name + "." + name);
        return property;
    }
    private static MethodInfo RequireMethod(Type type, string name, params Type[] parameters)
    {
        MethodInfo[] matches = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(method => method.Name == name && !method.IsGenericMethod
                && method.GetParameters().Select(parameter => parameter.ParameterType).SequenceEqual(parameters)).ToArray();
        return matches.Length == 1 ? matches[0] : throw new NotSupportedException("Unsupported SMAPI method: " + type.Name + "." + name);
    }
}
