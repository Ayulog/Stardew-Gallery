using System.Collections;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using StardewModdingAPI;

/// <summary>Runs built menu code with graphics-only construction/layout skipped. No game loop or saves.</summary>
internal static class GalleryDraftChecks
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static Assembly mod = null!;
    private static int assertions, failures;
    private static object nextCatalog = null!;
    private static object? activeMenu;

    internal static void Run(Assembly assembly, string gameDirectory)
    {
        mod = assembly;
        Assembly game = Assembly.LoadFrom(Path.Combine(gameDirectory, "Stardew Valley.dll"));
        Type game1 = game.GetType("StardewValley.Game1", true)!;
        var harmony = new Harmony("Gallery.DraftChecks");
        try
        {
            harmony.Patch(Type("GalleryToolMenu").GetConstructors(Flags).Single(ctor => !ctor.IsStatic), prefix: new HarmonyMethod(typeof(GalleryDraftChecks), nameof(SkipToolGraphics)));
            harmony.Patch(Type("GalleryToolMenu").GetMethod("RefreshRows", Flags)!, prefix: new HarmonyMethod(typeof(GalleryDraftChecks), nameof(SkipGraphics)));
            harmony.Patch(Type("GalleryTextBox").GetConstructors(Flags).Single(ctor => !ctor.IsStatic), prefix: new HarmonyMethod(typeof(GalleryDraftChecks), nameof(SkipGraphics)));
            game1.GetField("game1", Flags)!.SetValue(null, FormatterServices.GetUninitializedObject(game1));
            var dispatcher = game1.GetProperty("keyboardDispatcher", Flags)!;
            dispatcher.SetValue(null, FormatterServices.GetUninitializedObject(dispatcher.PropertyType));
            harmony.Patch(game1.GetProperty("activeClickableMenu", Flags)!.GetMethod!, prefix: new HarmonyMethod(typeof(GalleryDraftChecks), nameof(GetActiveMenu)));
            harmony.Patch(game1.GetProperty("activeClickableMenu", Flags)!.SetMethod!, prefix: new HarmonyMethod(typeof(GalleryDraftChecks), nameof(SetActiveMenu)));
            harmony.Patch(typeof(Context).GetProperty("IsWorldReady", Flags)!.GetMethod!, prefix: new HarmonyMethod(typeof(GalleryDraftChecks), nameof(WorldReady)));
            harmony.Patch(Type("GalleryCatalogCache").GetMethod("Get", Flags)!, prefix: new HarmonyMethod(typeof(GalleryDraftChecks), nameof(CatalogFixture)));
            harmony.Patch(Type("GalleryCharacterNames").GetConstructors(Flags).Single(ctor => !ctor.IsStatic), prefix: new HarmonyMethod(typeof(GalleryDraftChecks), nameof(CharacterFixture)));
            // The destination query is only a navigation sentinel; its rendering/search UI is outside this fixture.
            harmony.Patch(Type("GalleryQueryMenu").GetConstructors(Flags).Single(ctor => !ctor.IsStatic), prefix: new HarmonyMethod(typeof(GalleryDraftChecks), nameof(SkipGraphics)));
            harmony.Patch(Type("GalleryQueryMenu").GetMethod("DeselectSearch", Flags)!, prefix: new HarmonyMethod(typeof(GalleryDraftChecks), nameof(SkipGraphics)));
            FilterRefresh();
            MissingSelections();
            RenameRefresh();
            ApplicationRefresh(game1);
        }
        finally { harmony.UnpatchAll(harmony.Id); }
        Console.WriteLine($"Gallery draft runtime checks: {assertions - failures}/{assertions} passed; {failures} failed.");
        if (failures != 0) throw new InvalidOperationException("Gallery draft runtime regressions failed.");
    }

    private static void FilterRefresh()
    {
        object state = Page("Filters");
        object original = Get(state, "Filter")!;
        object view = View();
        object first = New("GalleryFilterMenu", view, state);
        Call(first, "Set", "npc", "Abigail");
        Call(first, "Set", "location", "Data/Events/Town");
        Call(first, "Set", "season", "winter");
        Set(first, "more", true); Set(first, "picker", "location"); Set(first, "returnFocus", 102);
        object refreshed = New("GalleryFilterMenu", view, state);
        Check(Equals(Call(refreshed, "Value", "npc"), "Abigail") && Equals(Call(refreshed, "Value", "location"), "Data/Events/Town")
            && Equals(Call(refreshed, "Value", "season"), "winter"), "unapplied filter edits survive actual menu reconstruction");
        Check(Equals(Get(refreshed, "more"), true) && Equals(Get(refreshed, "picker"), "location") && Equals(Get(refreshed, "returnFocus"), 102),
            "expanded filters and nested picker survive menu reconstruction");
        Check(Get(original, "Npc") is null && Get(original, "Location") is null && Get(original, "Season") is null,
            "editing and refreshing never mutates the applied query filter");
        Set(refreshed, "picker", null); Set(refreshed, "more", false);
        Call(refreshed, "Extra");
        object reset = New("GalleryFilterMenu", view, state);
        Check(Call(reset, "Value", "npc") is null && Call(reset, "Value", "season") is null,
            "reset remains a draft through the next refresh");
    }

    private static void MissingSelections()
    {
        object state = Page("Filters");
        object view = View();
        object menu = New("GalleryFilterMenu", view, state);
        Call(menu, "Set", "npc", "Abigail"); Call(menu, "Set", "location", "Data/Events/Town");
        foreach (var pair in new[] { (Field: "npc", Id: "Abigail"), (Field: "location", Id: "Data/Events/Town") })
        {
            object[] choices = Choices(menu, pair.Field);
            object? selected = choices.FirstOrDefault(choice => Equals(Get(choice, "Id"), pair.Id));
            Check(selected is not null && !Equals(Get(selected, "Label"), "filter.any"), "removed " + pair.Field + " remains visibly selected, not Any");
        }
        Call(menu, "Rebuild", 100);
        Check(RowTexts(menu).Contains("filter.npc: Abigail") && RowTexts(menu).Contains("filter.location: Town"),
            "actual filter summary shows the removed NPC/location labels instead of Any");
        object row = New("StorySearchRow", FormatterServices.GetUninitializedObject(Type("GalleryEvent")), "Town", "Abigail");
        Set(row, "Npcs", new[] { "Abigail" }); Set(row, "LocationKey", "Data/Events/Town");
        Array rows = Array.CreateInstance(Type("StorySearchRow"), 1); rows.SetValue(row, 0);
        Set(Get(menu, "index")!, "rows", rows);
        Check(Choices(menu, "npc").Count(choice => Equals(Get(choice, "Id"), "Abigail")) == 1
            && Choices(menu, "location").Count(choice => Equals(Get(choice, "Id"), "Data/Events/Town")) == 1,
            "restored NPC/location options have one choice each");
        Call(menu, "Set", "location", "data/events/town"); Call(menu, "Rebuild", 100);
        Check(Choices(menu, "location").Length == 2 && RowTexts(menu).Contains("filter.location: Town"),
            "restored location case differences do not duplicate or disguise the active filter");
        Call(menu, "Set", "location", "Data/Events/Town");
        Set(menu, "picker", "npc"); Call(menu, "Extra");
        Check(Call(menu, "Value", "npc") is null && Equals(Call(menu, "Value", "location"), "Data/Events/Town"), "clear removes only the selected field");
    }

    private static void RenameRefresh()
    {
        object state = Page("Rename"); Set(state, "Event", New("EventIdentity", "Data/Events/Town", "123"));
        object view = View();
        object first = New("GalleryRenameMenu", view, state);
        Set(Get(first, "input")!, "Text", "my unsaved title");
        Call(first, "DeselectSearch"); // GalleryApplication.RefreshCatalog releases input before replacing the menu.
        object refreshed = New("GalleryRenameMenu", view, state);
        Check(Equals(Get(Get(refreshed, "input")!, "Text"), "my unsaved title"), "rename text survives release/reconstruction without saving");
        Set(Get(refreshed, "input")!, "Text", ""); Call(refreshed, "DeselectSearch");
        object empty = New("GalleryRenameMenu", view, state);
        Check(Equals(Get(Get(empty, "input")!, "Text"), ""), "intentional empty rename draft survives refresh");
    }

    private static void ApplicationRefresh(Type game1)
    {
        string directory = Path.Combine(Path.GetTempPath(), "GalleryDraftChecks-" + Guid.NewGuid().ToString("N"));
        try
        {
            object names = New("EventNameStore", directory, (Action<string>)(message => throw new InvalidOperationException(message)));
            object identity = New("EventIdentity", "Data/Events/Town", "123");
            Call(names, "Set", identity, "saved title");
            object view = View(); Set(view, "Names", names);
            object catalog = FormatterServices.GetUninitializedObject(Type("GalleryCatalogCache"));
            object replay = FormatterServices.GetUninitializedObject(Type("ReplayService"));
            IModHelper helper = DispatchProxy.Create<IModHelper, FakeProxy>(); ((FakeProxy)(object)helper).Values["get_Translation"] = Get(view, "I18n");
            IMonitor monitor = DispatchProxy.Create<IMonitor, FakeProxy>();
            object app = New("GalleryApplication", helper, monitor, catalog, null, replay, (Func<bool>)(() => false), (Action)(() => { }), null, null);
            Set(app, "view", view); Set(app, "names", names); Set(view, "Navigation", app);
            nextCatalog = Get(view, "Catalog")!; // Fresh rules remove the target; empty live catalog is deliberate.
            object history = Get(app, "history")!; Call(history, "Reset");
            object query = Page("Query"); Set(query, "Scroll", 6); Set(query, "Focus", 2003); Call(history, "Open", query);
            object detail = Page("Detail"); Set(detail, "Event", identity); Call(history, "Open", detail);
            object rename = Page("Rename"); Set(rename, "Event", identity); Call(history, "Open", rename);
            object menu = New("GalleryRenameMenu", view, rename);
            Set(Get(menu, "input")!, "Text", "draft after exclusion");
            game1.GetProperty("activeClickableMenu", Flags)!.SetValue(null, menu);
            Call(app, "RefreshCatalog");
            object? refreshed = game1.GetProperty("activeClickableMenu", Flags)!.GetValue(null);
            bool retained = refreshed?.GetType() == Type("GalleryRenameMenu");
            Check(retained && Equals(Get(Get(refreshed!, "input")!, "Text"), "draft after exclusion"),
                "actual application refresh retains the rename editor even when its event is excluded");
            Check(ReferenceEquals(Get(Get(app, "view")!, "Catalog"), nextCatalog) && Call(nextCatalog, "Find", identity) is null,
                "the live catalog applies exclusion immediately while the name editor remains open");
            Check(Equals(Call(names, "Get", identity), "saved title"), "refresh never writes the rename draft to storage");
            if (retained)
            {
                Call(refreshed!, "HandleControllerBack");
                Check(ReferenceEquals(Get(history, "Current"), query) && Equals(Get(query, "Scroll"), 6),
                    "cancel skips excluded detail and restores the existing query without saving");
                object fresh = Page("Rename"); Set(fresh, "Event", identity); Call(history, "Open", fresh);
                object editor = New("GalleryRenameMenu", Get(app, "view"), fresh);
                Check(Equals(Get(Get(editor, "input")!, "Text"), "saved title"), "cancelled rename draft is absent from a fresh editor");
                game1.GetProperty("activeClickableMenu", Flags)!.SetValue(null, editor);
                Set(Get(editor, "input")!, "Text", "confirmed title"); Call(editor, "Apply");
                Check(Equals(Call(names, "Get", identity), "confirmed title") && ReferenceEquals(Get(history, "Current"), query),
                    "explicit Save writes the edited title and returns through normal navigation");
                fresh = Page("Rename"); Set(fresh, "Event", identity); Call(history, "Open", fresh);
                editor = New("GalleryRenameMenu", Get(app, "view"), fresh);
                game1.GetProperty("activeClickableMenu", Flags)!.SetValue(null, editor);
                Call(editor, "Extra");
                Check(Call(names, "Get", identity) is null && ReferenceEquals(Get(history, "Current"), query),
                    "Restore Default still removes the stored name only on explicit confirmation");
            }
            Check(((FakeProxy)(object)monitor).Messages.Count == 0, "application refresh completes without error fallback");
        }
        finally
        {
            game1.GetProperty("activeClickableMenu", Flags)!.SetValue(null, null);
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
    private static bool GetActiveMenu(ref object? __result) { __result = activeMenu; return false; }
    private static bool SetActiveMenu(object? __0) { activeMenu = __0; return false; }
    private static bool WorldReady(ref bool __result) { __result = true; return false; }
    private static bool CatalogFixture(ref object __result) { __result = nextCatalog; return false; }
    private static bool CharacterFixture(object __instance, object[] __args)
    {
        Set(__instance, "names", new Dictionary<string, string> { ["Abigail"] = "Abigail" });
        Set(__instance, "aliases", new Dictionary<string, string>()); Set(__instance, "i18n", __args[1]);
        return false;
    }
    private static object View()
    {
        object view = FormatterServices.GetUninitializedObject(Type("GalleryViewContext"));
        object catalog = New("GalleryCatalog", Array.CreateInstance(Type("GalleryCharacter"), 0), Array.CreateInstance(Type("GalleryEvent"), 0), Array.CreateInstance(Type("GalleryEvent"), 0));
        Set(view, "Catalog", catalog);
        ITranslationHelper translation = DispatchProxy.Create<ITranslationHelper, DraftTranslation>(); Set(view, "I18n", translation);
        object characters = FormatterServices.GetUninitializedObject(Type("GalleryCharacterNames"));
        Set(characters, "names", new Dictionary<string, string> { ["Abigail"] = "Abigail" });
        Set(characters, "aliases", new Dictionary<string, string>()); Set(characters, "i18n", translation); Set(view, "Characters", characters);
        object locations = New("GalleryLocationNames", translation);
        Set(locations, "aliases", new Dictionary<string, string>()); Set(locations, "cache", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Town"] = "Town" });
        Set(view, "Locations", locations);
        return view;
    }
    private static object Page(string page) => New("GalleryPageState", Enum.Parse(Type("GalleryPage"), page));
    private static string[] RowTexts(object menu) => ((IEnumerable)Get(menu, "Rows")!).Cast<object>().Select(row => (string)Get(row, "Text")!).ToArray();
    private static object[] Choices(object menu, string field) => ((IEnumerable)Call(menu, "Choices", field)!).Cast<object>().ToArray();
    private static Type Type(string name) => mod.GetType("StardewGallery." + name, true)!;
    private static object New(string name, params object?[] args) => Activator.CreateInstance(Type(name), Flags, null, args, null)!;
    private static object? Call(object target, string name, params object?[] args) => target.GetType().GetMethods(Flags).Single(method => method.Name == name && method.GetParameters().Length == args.Length).Invoke(target, args);
    private static object? Get(object target, string name)
    {
        for (Type? type = target.GetType(); type is not null; type = type.BaseType)
        {
            if (type.GetProperty(name, Flags) is { } property) return property.GetValue(target);
            if (type.GetField(name, Flags) is { } field) return field.GetValue(target);
        }
        throw new MissingMemberException(target.GetType().Name, name);
    }
    private static void Set(object target, string name, object? value)
    {
        for (Type? type = target.GetType(); type is not null; type = type.BaseType)
        {
            if (type.GetProperty(name, Flags) is { CanWrite: true } property) { property.SetValue(target, value); return; }
            if ((type.GetField(name, Flags) ?? type.GetField("<" + name + ">k__BackingField", Flags)) is { } field) { field.SetValue(target, value); return; }
        }
        throw new MissingMemberException(target.GetType().Name, name);
    }
    private static bool SkipGraphics() => false;
    private static bool SkipToolGraphics(object __instance, object[] __args)
    {
        Set(__instance, "Context", __args[0]); Set(__instance, "State", __args[1]);
        Set(__instance, "Rows", Activator.CreateInstance(typeof(List<>).MakeGenericType(Type("GalleryToolRow"))));
        return false;
    }
    private static void Check(bool passed, string message)
    {
        assertions++; if (!passed) failures++;
        Console.WriteLine((passed ? "PASS: " : "FAIL: ") + message);
    }
}

public class DraftTranslation : DispatchProxy
{
    protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch
    {
        "Get" => Activator.CreateInstance(typeof(Translation), BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, new object?[] { "en", args![0], ((string)args[0]!).StartsWith("character.", StringComparison.Ordinal) ? null : args[0] }, null),
        "get_Locale" => "en",
        _ => method.ReturnType.IsValueType ? Activator.CreateInstance(method.ReturnType) : null
    };
}
