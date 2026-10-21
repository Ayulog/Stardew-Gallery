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

/// <summary>
/// Built menu behavior under Game1's controller-to-mouse dispatch contract (mode --controller-input), and typing on a
/// physical keyboard through SMAPI's real input state (mode --keyboard-input), without a graphics device.
/// </summary>
internal static class ControllerInputChecks
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static Assembly mod = null!;
    private static Point cursor;
    private static IClickableMenu? active;
    private static int backs, opened, assertions, failures;

    internal static void Run(Assembly assembly) => WithFixture(assembly, "Controller", () =>
    {
        foreach (bool snappy in new[] { true, false })
        {
            Game1.options.snappyMenus = snappy;
            SourceDetails(snappy);
            Filters(snappy);
            Photos(snappy);
            ReplayConfirmation(snappy);
            SearchEnter(snappy);
            SteamKeyboard(snappy);
        }
    });

    internal static void RunKeyboard(Assembly assembly) => WithFixture(assembly, "Keyboard typing", KeyboardTyping);

    private static void WithFixture(Assembly assembly, string name, Action checks)
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
            harmony.Patch(Type("GalleryApplication").GetMethod("OpenPrerequisite", Flags)!, prefix: Hook(nameof(Open)));
            PropertyInfo dispatcher = typeof(Game1).GetProperty("keyboardDispatcher", Flags)!;
            dispatcher.SetValue(null, FormatterServices.GetUninitializedObject(dispatcher.PropertyType));
            // One screen, as SMAPI's Context.IsSplitScreen reads it.
            GameRunner.instance = (GameRunner)FormatterServices.GetUninitializedObject(typeof(GameRunner));
            GameRunner.instance.gameInstances = [Game1.game1];
            checks();
        }
        finally { harmony.UnpatchAll(harmony.Id); active = null; }
        Console.WriteLine($"{name} runtime checks: {assertions - failures}/{assertions} passed; {failures} failed.");
        if (failures != 0) throw new InvalidOperationException(name + " input regressions failed.");
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
        Call(menu, "Focus", snappy ? 104 : 100);
        cursor = menu.allClickableComponents.Single(component => component.myID == 104).bounds.Center;
        Press(menu, Buttons.A);
        Check(Equals(Get(state, "FilterPicker"), "state"), snappy, "one A opens the progress picker without selecting a row");
        Press(menu, Buttons.X);
        Check(Get(state, "FilterPicker") is null, snappy, "native X returns from the progress picker once");
        Call(menu, "Focus", 104); cursor = menu.currentlySnappedComponent.bounds.Center;
        menu.receiveKeyPress(Keys.Enter);
        Check(Equals(Get(state, "FilterPicker"), "state"), snappy, "Enter still opens the focused filter row");
        menu.receiveGamePadButton(Buttons.B);
        Call(menu, "Focus", 104); cursor = menu.currentlySnappedComponent.bounds.Center;
        menu.receiveLeftClick(cursor.X, cursor.Y);
        Check(Equals(Get(state, "FilterPicker"), "state"), snappy, "mouse click still opens the progress picker once");
        menu.receiveGamePadButton(Buttons.B);
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
        Check(menu.currentlySnappedComponent.myID == 104, snappy, "shoulder navigation still reaches the final filter row");
        // Game1.CheckGamepadMode repopulates components by reflection when input switches to a controller.
        int components = menu.allClickableComponents.Count;
        menu.populateClickableComponentList();
        Check(menu.allClickableComponents.Count == components && menu.allClickableComponents.Any(component => component.myID == 104),
            snappy, "switching to a controller keeps the filter page's own rows");
        Game1.options.menuButton = [new InputButton(Keys.E)];
        int before = backs; Press(menu, Buttons.Y);
        Check(backs == before + 1, snappy, "Y (the menu key) returns one page instead of closing the gallery");
        Game1.options.menuButton = [];
    }

    private static void ReplayConfirmation(bool snappy)
    {
        var dialog = (ConfirmationDialog)FormatterServices.GetUninitializedObject(Type("GalleryReplayConfirmationDialog"));
        int confirmed = 0, cancelled = 0;
        dialog.okButton = new ClickableTextureComponent(new Rectangle(500, 600, 64, 64), null, Rectangle.Empty, 1f) { myID = 101 };
        dialog.cancelButton = new ClickableTextureComponent(new Rectangle(600, 600, 64, 64), null, Rectangle.Empty, 1f) { myID = 102 };
        // Like ReplayService's callbacks, both choices close the dialog.
        Set(dialog, "onConfirm", (ConfirmationDialog.behavior)(_ => { confirmed++; active = null; }));
        Set(dialog, "onCancel", (ConfirmationDialog.behavior)(_ => { cancelled++; active = null; }));
        dialog.allClickableComponents = [dialog.okButton, dialog.cancelButton];
        dialog.currentlySnappedComponent = dialog.cancelButton;
        Set(dialog, "active", true);
        Set(dialog, "viewportWidth", Game1.uiViewport.Width); Set(dialog, "viewportHeight", Game1.uiViewport.Height);
        // The default focus is Cancel; a free cursor may point elsewhere.
        cursor = (snappy ? dialog.cancelButton : dialog.okButton).bounds.Center;
        Press(dialog, Buttons.A);
        Check(snappy ? cancelled == 1 && confirmed == 0 : confirmed == 1 && cancelled == 0, snappy,
            snappy ? "A activates the focused Cancel once" : "free-cursor A confirms the button under the cursor, not the default focus");
    }

    /// <summary>The query page with three results; its search box holds "1" and is focused, not selected.</summary>
    private static (IClickableMenu Menu, TextBox Search) QueryWithResults()
    {
        var menu = (IClickableMenu)FormatterServices.GetUninitializedObject(Type("GalleryQueryMenu"));
        var search = (TextBox)FormatterServices.GetUninitializedObject(Type("GalleryTextBox"));
        search.Text = "1";
        Set(menu, "context", View()); Set(menu, "state", Page("Query")); Set(menu, "search", search);
        Set(menu, "lastSearch", "1"); Set(menu, "scale", 1f);
        var rows = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(Type("StorySearchRow")))!;
        for (int index = 0; index < 3; index++)
        {
            object row = New("StorySearchRow", null, "Town", "Abigail");
            Set(row, "Prerequisite", New("PrerequisiteEvent", "fixture-" + index, Array.CreateInstance(Type("MarkerAction"), 0),
                Array.CreateInstance(Type("GalleryEvent"), 0), Array.CreateInstance(Type("GalleryEvent"), 0)));
            rows.Add(row);
        }
        Set(menu, "rows", rows);
        Call(menu, "BuildComponents"); Call(menu, "Focus", 1000);
        return (menu, search);
    }

    /// <summary>Enter in the search box (or OK on the on-screen keyboard) shows the results; it doesn't open one.</summary>
    private static void SearchEnter(bool snappy)
    {
        (IClickableMenu menu, TextBox search) = QueryWithResults();
        search.Selected = true;
        int before = opened;
        Call(menu, "FinishSearch");
        Check(opened == before && !search.Selected && menu.currentlySnappedComponent?.myID == 2000, snappy,
            "Enter in the search box shows the results with the first focused, without opening it");
        active = menu;
        menu.receiveKeyPress(Keys.Enter);
        Check(opened == before + 1, snappy, "a second Enter opens the focused result");
    }

    /// <summary>
    /// Steam's keyboard can't open here (Steam isn't running for this process), so the game's keyboard is used. The
    /// closing is simulated: its text replaces the field, and Enter waits until the controller's buttons are up.
    /// </summary>
    private static void SteamKeyboard(bool snappy)
    {
        Type steam = Type("SteamTextInput");
        Set(steam, "unavailable", false); Set(steam, "target", null); Set(steam, "enterPending", false);
        string? reason = null;
        Set(steam, "Log", (Action<string, LogLevel>)((message, _) => reason = message));
        var box = (TextBox)FormatterServices.GetUninitializedObject(Type("GalleryTextBox"));
        int enters = 0;
        box.OnEnterPressed += _ => enters++;
        box.Text = "ab";
        box.Selected = true;
        Check(Equals(Call(steam, "TryShow", box), false) && Equals(Get(steam, "unavailable"), true) && Get(steam, "target") is null, snappy,
            "without Steam the field falls back to the game's keyboard (" + reason + ")");
        Check(Equals(Call(steam, "TryShow", box), false), snappy, "later fields skip Steam without trying again");

        void Close(string? text) { Set(steam, "target", box); Call(steam, "Closed", text); }
        Func<SButton, bool> holding = button => button == SButton.ControllerA, released = _ => false;
        Close("阿比盖尔");
        Call(steam, "Update", holding);
        Check(box.Text == "阿比盖尔" && enters == 0 && box.Selected, snappy, "submitted text replaces the field; Enter waits while A is held");
        Call(steam, "Update", released);
        Check(enters == 1 && Get(steam, "target") is null, snappy, "Enter follows once the buttons are up, once");
        Call(steam, "Update", released);
        Check(enters == 1, snappy, "no further Enter");
        Close(null);
        Call(steam, "Update", released);
        Check(box.Text == "阿比盖尔" && box.Selected && enters == 1 && Get(steam, "target") is null, snappy,
            "cancelling keeps the field selected with its text, like the game's keyboard");
        box.Selected = false;
        Close("other");
        Call(steam, "Update", released);
        Check(box.Text == "阿比盖尔" && enters == 1, snappy, "text for a field that is no longer selected is dropped");
        Set(steam, "unavailable", false); Set(steam, "target", box);
        Check(Equals(Call(steam, "TryShow", box), false) && Get(steam, "target") is null && Equals(Get(steam, "unavailable"), true), snappy,
            "a keyboard that never closed isn't relied on again");
    }

    /// <summary>
    /// Typing on a physical keyboard, frame by frame in the order SMAPI 4.5.2 and the game run it (both decompiled):
    /// SMAPI's real SInputState.TrueUpdate reads the keys the game captured the frame before and raises input events;
    /// UpdateTicking runs GalleryTyping; the game captures this frame's keys, its keyboard dispatcher delivers the text
    /// typed this frame (Poll), and then it hands new key presses, as SMAPI shows them to the game, to the menu.
    /// </summary>
    private static void KeyboardTyping()
    {
        Type inputType = typeof(IModHelper).Assembly.GetType("StardewModdingAPI.Framework.Input.SInputState", true)!;
        var input = (InputState)Activator.CreateInstance(inputType, nonPublic: true)!;
        Game1.input = input;
        FieldInfo captured = typeof(InputState).GetField("_currentKeyboardState", Flags)!;
        MethodInfo read = inputType.GetMethod("TrueUpdate", Flags)!;
        MethodInfo state = inputType.GetMethod("GetState", Flags, null, [typeof(SButton)], null)!;
        MethodInfo typing = Type("GalleryTyping").GetMethod("Update", Flags)!;
        object keys = New("GalleryTypingKeys");
        string? clipboard = null;
        bool hide = true;
        Keys[] gameSaw = [];
        (IClickableMenu menu, TextBox search) = QueryWithResults();
        search.textLimit = -1;
        search.OnEnterPressed += _ => Call(menu, "FinishSearch");

        void Frame(Keys[] down, Action? typed = null)
        {
            Game1.ticks++;
            try { read.Invoke(input, null); }
            // TrueUpdate sets the key states first; only the cursor tile after them needs a player.
            catch (TargetInvocationException error) when (error.InnerException?.StackTrace?.Contains("GetCursorPosition") == true) { }
            Keys[] gameSees = input.GetKeyboardState().GetPressedKeys();
            if (hide)
                typing.Invoke(null, [keys, menu, (Func<Keys[]>)(() => down), (Func<string?>)(() => clipboard)]);
            captured.SetValue(input, new KeyboardState(down));
            typed?.Invoke();
            foreach (Keys key in gameSees.Except(gameSaw))
                if (ReferenceEquals(active, menu)) menu.receiveKeyPress(key);
            gameSaw = gameSees;
        }
        string Smapi(Keys key) => state.Invoke(input, [key.ToSButton()])!.ToString()!;
        // Select the field, then let one frame pass: keys already held on the first typing frame are only hidden.
        void Start() { Frame([]); Frame([]); Call(menu, "Focus", 1000); search.Selected = true; active = menu; Frame([]); }

        Start();
        Frame([Keys.K]); Frame([Keys.K]);
        Check(Smapi(Keys.K) == "None" && !input.GetKeyboardState().IsKeyDown(Keys.K),
            "a letter typed into the field reaches neither SMAPI's input state (other mods' keybinds) nor the game");
        Frame([]); Call(menu, "DeselectSearch"); Frame([Keys.K]); Frame([Keys.K]);
        Check(Smapi(Keys.K) == "Pressed", "once typing ended and it was released, the key reaches SMAPI as a shortcut again");

        Start();
        int before = opened;
        Frame([Keys.Enter], () => search.RecieveCommandInput('\r'));
        Frame([Keys.Enter]);
        Check(opened == before && !search.Selected && menu.currentlySnappedComponent?.myID == 2000 && Smapi(Keys.Enter) == "None",
            "Enter on a physical keyboard lists the results and doesn't also open the first one a frame later");
        Frame([]); Frame([Keys.Enter]); Frame([Keys.Enter]);
        Check(opened == before + 1, "a second Enter opens the focused result");

        hide = false;
        Start();
        before = opened;
        Frame([Keys.Enter], () => search.RecieveCommandInput('\r'));
        Frame([Keys.Enter]);
        Check(opened == before + 1, "control: without hiding, the Enter that ended typing also opens the first result");
        hide = true;

        Start();
        int backsBefore = backs;
        Frame([Keys.Escape]); Frame([Keys.Escape]);
        Check(!search.Selected && backs == backsBefore && Smapi(Keys.Escape) == "None", "Escape ends typing and doesn't also leave the page");

        Start();
        clipboard = "\u963f\u6bd4";
        Frame([Keys.LeftControl]); Frame([Keys.LeftControl, Keys.V]); Frame([Keys.LeftControl, Keys.V]);
        Check(search.Text == "1\u963f\u6bd4" && Smapi(Keys.LeftControl) == "None" && Smapi(Keys.V) == "None",
            "Ctrl+V pastes once, and other mods see neither key");
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
    private static bool Open() { opened++; active = null; return false; }
    private static bool Image(ref Texture2D __result) { __result = (Texture2D)FormatterServices.GetUninitializedObject(typeof(Texture2D)); return false; }
    private static bool TimeLabel(int __0, ref string __result) { __result = __0.ToString(); return false; }
    private static bool False(ref bool __result) { __result = false; return false; }
    private static bool Skip() => false;
    private static HarmonyMethod Hook(string name) => new(typeof(ControllerInputChecks), name);
    private static object Page(string page) => New("GalleryPageState", Enum.Parse(Type("GalleryPage"), page));
    private static Type Type(string name) => mod.GetType("StardewGallery." + name, true)!;
    private static object New(string name, params object?[] args) => Activator.CreateInstance(Type(name), Flags, null, args, null)!;
    private static object? Call(object target, string name, params object?[] args) => (target as Type ?? target.GetType()).GetMethods(Flags)
        .Single(method => method.Name == name && method.GetParameters().Length == args.Length).Invoke(target is Type ? null : target, args);
    // A Type target means the type's static members.
    private static object? Get(object target, string name)
    {
        object? instance = target is Type ? null : target;
        for (Type? type = target as Type ?? target.GetType(); type is not null; type = type.BaseType)
        {
            if (type.GetProperty(name, Flags) is { } property) return property.GetValue(instance);
            if (type.GetField(name, Flags) is { } field) return field.GetValue(instance);
        }
        throw new MissingMemberException(target.GetType().Name, name);
    }
    private static void Set(object target, string name, object? value)
    {
        object? instance = target is Type ? null : target;
        for (Type? type = target as Type ?? target.GetType(); type is not null; type = type.BaseType)
        {
            if (type.GetProperty(name, Flags) is { CanWrite: true } property) { property.SetValue(instance, value); return; }
            if ((type.GetField(name, Flags) ?? type.GetField("<" + name + ">k__BackingField", Flags)) is { } field) { field.SetValue(instance, value); return; }
        }
        throw new MissingMemberException(target.GetType().Name, name);
    }
    private static void Check(bool passed, bool snappy, string message) => Check(passed, (snappy ? "snapped: " : "free cursor: ") + message);
    private static void Check(bool passed, string message)
    {
        assertions++; if (!passed) failures++;
        Console.WriteLine((passed ? "PASS: " : "FAIL: ") + message);
    }
}
