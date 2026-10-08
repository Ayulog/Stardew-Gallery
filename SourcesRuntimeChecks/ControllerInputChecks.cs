using System.Collections;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

/// <summary>Built menu behavior under Game1's controller-to-mouse dispatch contract, without a graphics device.</summary>
internal static class ControllerInputChecks
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static Assembly mod = null!;
    private static Point cursor;
    private static IClickableMenu? active;
    private static int backs, assertions, failures;

    internal static void Run(Assembly assembly)
    {
        mod = assembly;
        var harmony = new Harmony("Gallery.ControllerInputChecks");
        try
        {
            Game1.game1 = (Game1)FormatterServices.GetUninitializedObject(typeof(Game1));
            Game1.options = (Options)FormatterServices.GetUninitializedObject(typeof(Options));
            Game1.options.gamepadControls = true;
            foreach (FieldInfo field in typeof(Options).GetFields(Flags).Where(field => field.FieldType == typeof(InputButton[])))
                field.SetValue(Game1.options, Array.Empty<InputButton>());
            Game1.options.actionButton = [new InputButton(Keys.X)]; Game1.options.useToolButton = [new InputButton(Keys.C)];
            foreach (MethodInfo sound in typeof(Game1).GetMethods(Flags).Where(method => method.Name == "playSound"))
                harmony.Patch(sound, prefix: Hook(nameof(Skip)));
            harmony.Patch(typeof(Game1).GetMethod("getTimeOfDayString", Flags)!, prefix: Hook(nameof(TimeLabel)));
            harmony.Patch(typeof(IClickableMenu).GetMethod("snapCursorToCurrentSnappedComponent", Flags)!, prefix: Hook(nameof(Snap)));
            harmony.Patch(Type("GalleryToolMenu").GetConstructors(Flags).Single(ctor => !ctor.IsStatic), prefix: Hook(nameof(ToolShell)));
            harmony.Patch(Type("GalleryToolMenu").GetMethod("RefreshRows", Flags)!, prefix: Hook(nameof(ToolRows)));
            harmony.Patch(Type("GalleryEventDetailMenu").GetMethod("RecalculateLayout", Flags)!, prefix: Hook(nameof(Skip)));
            harmony.Patch(Type("GalleryEventDetailMenu").GetMethod("CanReplay", Flags)!, prefix: Hook(nameof(False)));
            harmony.Patch(Type("GalleryApplication").GetMethod("Back", Flags)!, prefix: Hook(nameof(Back)));
            harmony.Patch(Type("GalleryPhotos").GetMethod("Image", Flags)!, prefix: Hook(nameof(Image)));
            foreach (bool snappy in new[] { true, false })
            {
                Game1.options.snappyMenus = snappy;
                SourceDetails(snappy);
                Filters(snappy);
                Photos(snappy);
            }
        }
        finally { harmony.UnpatchAll(harmony.Id); active = null; }
        Console.WriteLine($"Controller runtime checks: {assertions - failures}/{assertions} passed; {failures} failed.");
        if (failures != 0) throw new InvalidOperationException("Controller input regressions failed.");
    }

    private static void SourceDetails(bool snappy)
    {
        object state = Page("Detail");
        var menu = (IClickableMenu)FormatterServices.GetUninitializedObject(Type("GalleryEventDetailMenu"));
        Set(menu, "state", state); Set(menu, "context", View()); Set(menu, "menuScale", 1f);
        Set(menu, "referenceLinks", Activator.CreateInstance(Type("GalleryEventDetailMenu").GetField("referenceLinks", Flags)!.FieldType));
        Set(menu, "backBounds", new Rectangle(700, 810, 300, 72));
        Call(menu, "BuildClickableComponents");
        Call(menu, "FocusComponent", snappy ? 1004 : 1000);
        // Free cursor intentionally differs from stored focus.
        cursor = menu.allClickableComponents.Single(component => component.myID == 1004).bounds.Center;
        Press(menu, Buttons.A);
        Check(Equals(Get(state, "SourceExpanded"), true), snappy, "one A opens source details and leaves them open");
        Press(menu, Buttons.A);
        Check(Equals(Get(state, "SourceExpanded"), false), snappy, "next A closes source details once");
        menu.receiveLeftClick(cursor.X, cursor.Y);
        Check(Equals(Get(state, "SourceExpanded"), true), snappy, "mouse source click still toggles once");
        menu.receiveKeyPress(Keys.Enter);
        Check(Equals(Get(state, "SourceExpanded"), false), snappy, "Enter still activates focused source");
        int before = backs; Press(menu, Buttons.X);
        Check(backs == before + 1, snappy, "native X right-click returns once from details");
    }

    private static void Filters(bool snappy)
    {
        object state = Page("Filters");
        var menu = (IClickableMenu)New("GalleryFilterMenu", View(), state);
        Call(menu, "Focus", snappy ? 106 : 100);
        cursor = menu.allClickableComponents.Single(component => component.myID == 106).bounds.Center;
        Press(menu, Buttons.A);
        Check(Equals(Get(state, "FilterExpanded"), true), snappy, "one A expands More filters and leaves them expanded");
        Press(menu, Buttons.A);
        Check(Equals(Get(state, "FilterExpanded"), false), snappy, "next A collapses More filters once");
        menu.receiveKeyPress(Keys.Enter);
        Check(Equals(Get(state, "FilterExpanded"), true), snappy, "Enter still expands the focused filter row");
        menu.receiveLeftClick(cursor.X, cursor.Y);
        Check(Equals(Get(state, "FilterExpanded"), false), snappy, "mouse click still collapses More once");
        Call(menu, "Focus", 101);
        cursor = menu.currentlySnappedComponent.bounds.Center;
        Press(menu, Buttons.A);
        Check(Equals(Get(state, "FilterPicker"), "npc"), snappy, "one A opens the NPC picker without selecting a row");
        Press(menu, Buttons.X);
        Check(Get(state, "FilterPicker") is null, snappy, "native X returns from picker to filters");
        Call(menu, "Focus", 101); cursor = menu.currentlySnappedComponent.bounds.Center;
        Press(menu, Buttons.A); menu.receiveGamePadButton(Buttons.B);
        Check(Get(state, "FilterPicker") is null, snappy, "B still returns from picker to filters");
        menu.receiveGamePadButton(Buttons.RightShoulder);
        Check(menu.currentlySnappedComponent.myID == 106, snappy, "shoulder navigation still reaches the final collapsed row");
    }

    private static void Photos(bool snappy)
    {
        string root = Path.Combine(Path.GetTempPath(), "GalleryControllerChecks-" + Guid.NewGuid().ToString("N"));
        try
        {
            object profile = New("SaveProfileKey", 123UL, 456L);
            Action<string> warn = message => throw new InvalidOperationException(message);
            object store = New("EventPhotoStore", root, profile, warn);
            object identity = New("EventIdentity", "Data/Events/Town", "controller-fixture");
            byte[] png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a9d8AAAAASUVORK5CYII=");
            object[] photos = Enumerable.Range(0, 3).Select(_ => Call(store, "Add", identity, png, png)!).ToArray();
            object manager = New("GalleryPhotos", DispatchProxy.Create<IModHelper, FakeProxy>(), DispatchProxy.Create<IMonitor, FakeProxy>(), (Action<string, bool>)((message, _) => throw new InvalidOperationException(message)));
            Set(manager, "store", store);
            object state = Page("Photos"); Set(state, "Event", identity);
            object view = View(); Set(view, "Photos", manager);
            var menu = (IClickableMenu)FormatterServices.GetUninitializedObject(Type("GalleryPhotoMenu"));
            Set(menu, "context", view); Set(menu, "state", state); Set(menu, "photos", manager);
            Set(menu, "identity", identity); Set(menu, "i18n", Get(view, "I18n")); Set(menu, "scale", 1f);
            Set(menu, "set", Call(store, "Read", identity)); Call(menu, "BuildComponents"); Call(menu, "Focus", snappy ? 103 : 100);
            cursor = menu.allClickableComponents.Single(component => component.myID == 103).bounds.Center;
            Press(menu, Buttons.A);
            object persisted = Call(New("EventPhotoStore", root, profile, warn), "Read", identity)!;
            Check(((IEnumerable)Get(persisted, "Photos")!).Cast<object>().Count() == 2, snappy, "one A archives exactly one of three photos in the real store");
            string first = (string)Call(store, "ImagePath", identity, Get(photos[0], "Id"), false)!;
            string second = (string)Call(store, "ImagePath", identity, Get(photos[1], "Id"), false)!;
            Check(!File.Exists(first) && File.Exists(second) && Directory.EnumerateFiles(root, Path.GetFileName(first), SearchOption.AllDirectories).Count() == 1,
                snappy, "first photo is archived and next original remains untouched");
            Call(menu, "Focus", 101); cursor = menu.currentlySnappedComponent.bounds.Center;
            int before = backs; Press(menu, Buttons.A);
            persisted = Call(New("EventPhotoStore", root, profile, warn), "Read", identity)!;
            Check(Equals(Get(persisted, "CoverId"), Get(photos[1], "Id")) && backs == before && ReferenceEquals(active, menu), snappy,
                "one A sets the cover without following changed focus to Back");
            Press(menu, Buttons.X);
            Check(backs == before + 1, snappy, "native X still returns from photos");
            before = backs; menu.receiveGamePadButton(Buttons.B);
            Check(backs == before + 1, snappy, "B still returns from photos");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    // Reproduce the fresh-button branches of Game1.updateActiveMenu (game 1.6.15.24356):
    // direct button dispatch, conditional A/X mouse emulation, then native key mapping.
    // Rendering, physical devices, repeat timers and Game1's surrounding game loop are not exercised.
    private static void Press(IClickableMenu menu, Buttons button)
    {
        active = menu;
        menu.receiveGamePadButton(button);
        if (!ReferenceEquals(active, menu)) return;
        if (!menu.areGamePadControlsImplemented())
        {
            if (button == Buttons.A) menu.receiveLeftClick(cursor.X, cursor.Y);
            else if (button == Buttons.X) menu.receiveRightClick(cursor.X, cursor.Y);
        }
        if (ReferenceEquals(active, menu)) menu.receiveKeyPress(Utility.mapGamePadButtonToKey(button));
    }

    private static object View()
    {
        object view = FormatterServices.GetUninitializedObject(Type("GalleryViewContext"));
        Set(view, "Catalog", New("GalleryCatalog", Array.CreateInstance(Type("GalleryCharacter"), 0), Array.CreateInstance(Type("GalleryEvent"), 0), Array.CreateInstance(Type("GalleryEvent"), 0)));
        ITranslationHelper translation = DispatchProxy.Create<ITranslationHelper, DraftTranslation>(); Set(view, "I18n", translation);
        object characters = FormatterServices.GetUninitializedObject(Type("GalleryCharacterNames"));
        Set(characters, "names", new Dictionary<string, string>()); Set(characters, "aliases", new Dictionary<string, string>()); Set(characters, "i18n", translation); Set(view, "Characters", characters);
        object locations = New("GalleryLocationNames", translation); Set(locations, "aliases", new Dictionary<string, string>()); Set(view, "Locations", locations);
        Set(view, "Navigation", FormatterServices.GetUninitializedObject(Type("GalleryApplication")));
        return view;
    }
    private static bool ToolShell(object __instance, object[] __args)
    {
        Set(__instance, "Context", __args[0]); Set(__instance, "State", __args[1]); Set(__instance, "Scale", 1f);
        Set(__instance, "Rows", Activator.CreateInstance(typeof(List<>).MakeGenericType(Type("GalleryToolRow"))));
        Set(__instance, "rowBounds", new List<Rectangle>());
        ((IClickableMenu)__instance).upperRightCloseButton = new ClickableTextureComponent(new Rectangle(1120, 20, 48, 48), null, Rectangle.Empty, 1f);
        return false;
    }
    private static bool ToolRows(object __instance, int __0)
    {
        // Replace font measurement only; real component building, focus, activation and draft rebuilding remain active.
        var rows = (IList)Get(__instance, "Rows")!;
        var bounds = (List<Rectangle>)Get(__instance, "rowBounds")!; bounds.Clear();
        for (int i = 0; i < rows.Count; i++) bounds.Add(new Rectangle(52, 148 + i * 58, 1050, 58));
        Set(__instance, "contentHeight", rows.Count * 58); Call(__instance, "Focus", __0); return false;
    }
    private static bool Snap(IClickableMenu __instance) { cursor = __instance.currentlySnappedComponent?.bounds.Center ?? cursor; return false; }
    private static bool Back() { backs++; active = null; return false; }
    private static bool Image(ref Texture2D __result) { __result = (Texture2D)FormatterServices.GetUninitializedObject(typeof(Texture2D)); return false; }
    private static bool TimeLabel(int __0, ref string __result) { __result = __0.ToString(); return false; }
    private static bool False(ref bool __result) { __result = false; return false; }
    private static bool Skip() => false;
    private static HarmonyMethod Hook(string name) => new(typeof(ControllerInputChecks), name);
    private static object Page(string page) => New("GalleryPageState", Enum.Parse(Type("GalleryPage"), page));
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
    private static void Check(bool passed, bool snappy, string message)
    {
        assertions++; if (!passed) failures++;
        Console.WriteLine((passed ? "PASS: " : "FAIL: ") + (snappy ? "snapped: " : "free cursor: ") + message);
    }
}
