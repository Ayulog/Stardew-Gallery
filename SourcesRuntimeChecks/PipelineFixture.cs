using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Serialization;

internal sealed class PipelineFixture
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private readonly Assembly smapi;
    private readonly object manager;
    private readonly object core;
    private readonly object eventManager;
    private readonly IList loaders;
    private readonly IList editors;
    private readonly object metadata;
    private readonly Dictionary<string, object> cache = new(StringComparer.OrdinalIgnoreCase);
    internal int LoaderCalls { get; private set; }
    internal int EditorCalls { get; private set; }
    internal int ReadyCalls { get; private set; }
    internal bool LoadingDuringReady { get; private set; }
    internal object Monitor { get; }
    internal object Manager => manager;
    internal HashSet<string> Loading { get; }
    internal Action<object>? Ready { get; set; }
    internal Action<object>? Edit { get; set; }
    internal Func<object> LoadData { get; set; } = () => new Dictionary<string,string>{{"1", "loader"}};

    internal PipelineFixture(Assembly smapi)
    {
        this.smapi = smapi;
        Monitor = Proxy("IMonitor");
        metadata = Proxy("Framework.IModMetadata");
        object manifest = Proxy("IManifest");
        ((FakeProxy)manifest).Values["get_UniqueID"] = "RuntimeChecks.Fixture";
        ((FakeProxy)manifest).Values["get_Name"] = "Runtime fixture";
((FakeProxy)manifest).Values["get_Version"] = Activator.CreateInstance(Find("SemanticVersion"), "1.0.0");
        ((FakeProxy)metadata).Values["get_Manifest"] = manifest;
        ((FakeProxy)metadata).Values["get_Monitor"] = Monitor;
        ((FakeProxy)metadata).Values["get_DisplayName"] = "Runtime fixture";
        Type managerType = Find("Framework.ContentManagers.GameContentManager");
        manager = FormatterServices.GetUninitializedObject(managerType);
        GC.SuppressFinalize(manager); // Fixture does not construct disposable SMAPI services.
        Set(manager, "Monitor", Monitor);
        Set(manager, "Cache", Make("Framework.Content.ContentCache", cache));
        Set(manager, "BaseDisposableReferences", new List<IDisposable>());
Set(manager, "BaseLoadProxyCache", new Dictionary<Type,object>());
for (Type? type = managerType; type is not null; type = type.BaseType)
    if (type.FullName == "Microsoft.Xna.Framework.Content.ContentManager")
        type.GetFields(Instance | BindingFlags.DeclaredOnly).Single(field => field.FieldType == typeof(Dictionary<string,object>)).SetValue(manager, cache);
        object loading = Make("Framework.Utilities.ContextHash`1", typeof(string));
        Loading = (HashSet<string>)loading;
        Set(manager, "AssetsBeingLoaded", loading);
        managerType.GetField("IsFirstLoad", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, false);
        core = FormatterServices.GetUninitializedObject(Find("Framework.SCore"));
        eventManager = Find("Framework.Events.EventManager").GetConstructors().Single().Invoke(new object?[]{null});
        Set(core, "EventManager", eventManager);
        FieldInfo loadedField = managerType.GetField("OnAssetLoaded", Instance)!;
        loadedField.SetValue(manager, MakeDelegate(loadedField.FieldType, values =>
        {
            core.GetType().GetMethod("OnAssetLoaded", Instance)!.Invoke(core, values);
            return null;
        }));
        object assetReady = eventManager.GetType().GetField("AssetReady", Instance)!.GetValue(eventManager)!;
        MethodInfo add = assetReady.GetType().GetMethod("Add")!;
        Delegate readyDelegate = MakeDelegate(add.GetParameters()[0].ParameterType, values =>
        {
            ReadyCalls++;
            object name = values[1]!.GetType().GetProperty("Name")!.GetValue(values[1])!;
            LoadingDuringReady = Loading.Contains((string)name.GetType().GetProperty("Name")!.GetValue(name)!);
            Ready?.Invoke(name);
            return null;
        });
        add.Invoke(assetReady, new[] { readyDelegate, metadata });
        Type loaderType = Find("Framework.Content.AssetLoadOperation");
        Type editorType = Find("Framework.Content.AssetEditOperation");
        loaders = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(loaderType))!;
        editors = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(editorType))!;
        ConstructorInfo loaderCtor = loaderType.GetConstructors().Single(ctor => ctor.GetParameters().Length == 4);
        loaders.Add(loaderCtor.Invoke(new object?[]{metadata, null, Enum.ToObject(loaderCtor.GetParameters()[2].ParameterType, 0), MakeDelegate(loaderCtor.GetParameters()[3].ParameterType, _ => { LoaderCalls++; return LoadData(); })}));
        ConstructorInfo editorCtor = editorType.GetConstructors().Single(ctor => ctor.GetParameters().Length == 4);
        editors.Add(editorCtor.Invoke(new object?[]{metadata, Enum.ToObject(editorCtor.GetParameters()[1].ParameterType, 0), null, MakeDelegate(editorCtor.GetParameters()[3].ParameterType, values => { EditorCalls++; Edit?.Invoke(values[0]!); return null; })}));
        object group = Make("Framework.Content.AssetOperationGroup", loaders, editors);
        Type coordinatorType = Find("Framework.ContentCoordinator");
        object coordinator = FormatterServices.GetUninitializedObject(coordinatorType);
        Set(coordinator, "ManagedPrefix", "SMAPI");
        FieldInfo tickCache = coordinatorType.GetField("AssetOperationsByKey", Instance)!;
        tickCache.SetValue(coordinator, Activator.CreateInstance(tickCache.FieldType));
        FieldInfo request = coordinatorType.GetField("RequestAssetOperations", Instance)!;
        request.SetValue(coordinator, MakeDelegate(request.FieldType, _ => group));
        Set(manager, "Coordinator", coordinator);
    }

    internal void UseGameBaseFiles(string contentDirectory)
    {
        loaders.Clear();
        editors.Clear();
        Set(manager, "_rootDirectory", contentDirectory);
        Set(manager, "serviceProvider", new EmptyServices());
        Set(manager, "disposableAssets", new List<IDisposable>());
    }
    private sealed class EmptyServices : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
    internal object LocaleName(string key, string locale) => Make("Framework.Content.AssetName", key, locale, null);
    internal object LoadNamed(Type type, object name, bool useCache = true) => manager.GetType().GetMethod("LoadExact", Instance)!.MakeGenericMethod(type).Invoke(manager, new[] { name, (object)useCache })!;
    internal object Name(string key) => Make("Framework.Content.AssetName", key, null, null);
    internal void SetIdentity(string uniqueId)
    {
        var manifest = (FakeProxy)((FakeProxy)metadata).Values["get_Manifest"]!;
        manifest.Values["get_UniqueID"] = uniqueId;
        manifest.Values["get_Name"] = uniqueId;
    }
    internal void SetLoaderPack(string uniqueId)
    {
        object pack = Proxy("Framework.IModMetadata");
        object manifest = Proxy("IManifest");
        ((FakeProxy)manifest).Values["get_UniqueID"] = uniqueId;
        ((FakeProxy)manifest).Values["get_Name"] = uniqueId;
        ((FakeProxy)manifest).Values["get_Version"] = Activator.CreateInstance(Find("SemanticVersion"), "1.0.0");
        ((FakeProxy)pack).Values["get_Manifest"] = manifest;
        loaders[0]!.GetType().GetProperty("OnBehalfOf")!.SetValue(loaders[0], pack);
    }
    internal void FailAfterCache(bool fail)
        => Set(manager, "BaseDisposableReferences", fail ? null! : new List<IDisposable>());
    internal object Load(Type type, string key, bool useCache = true)
    {
        object name = Make("Framework.Content.AssetName", key, null, null);
        return manager.GetType().GetMethod("LoadExact", Instance)!.MakeGenericMethod(type).Invoke(manager, new[] { name, (object)useCache })!;
    }
    internal void Invalidate(string key) => cache.Remove(key);
    internal object? Cached(string key) => cache.GetValueOrDefault(key);
    internal static object Data(object asset) => asset.GetType().GetProperty("Data")!.GetValue(asset)!;
    internal static void Replace(object asset, object? data) => asset.GetType().GetProperty("Data")!.SetValue(asset, data);
    private Type Find(string name) => smapi.GetType("StardewModdingAPI." + name) ?? Assembly.Load("SMAPI.Toolkit.CoreInterfaces").GetType("StardewModdingAPI." + name, true)!;
    private object Proxy(string name) => typeof(DispatchProxy).GetMethod("Create")!.MakeGenericMethod(Find(name), typeof(FakeProxy)).Invoke(null, null)!;
    private object Make(string name, params object?[] values)
    {
        Type type = Find(name);
        if (type.IsGenericTypeDefinition) return Activator.CreateInstance(type.MakeGenericType((Type)values[0]!))!;
        return Activator.CreateInstance(type, values)!;
    }
    private static void Set(object target, string name, object value)
    {
        for (Type? type = target.GetType(); type is not null; type = type.BaseType)
        {
            FieldInfo? field = type.GetField(name, Instance | BindingFlags.DeclaredOnly);
            if (field is null) continue;
            field.SetValue(target, value);
            return;
        }
        throw new MissingFieldException(target.GetType().FullName, name);
    }
    private static Delegate MakeDelegate(Type type, Func<object?[], object?> callback)
    {
        MethodInfo invoke = type.GetMethod("Invoke")!;
        var parameters = invoke.GetParameters().Select(parameter => Expression.Parameter(parameter.ParameterType)).ToArray();
        Expression call = Expression.Invoke(Expression.Constant(callback), Expression.NewArrayInit(typeof(object), parameters.Select(parameter => Expression.Convert(parameter, typeof(object)))));
        Expression body = invoke.ReturnType == typeof(void) ? Expression.Block(call, Expression.Empty()) : Expression.Convert(call, invoke.ReturnType);
        return Expression.Lambda(type, body, parameters).Compile();
    }
}
