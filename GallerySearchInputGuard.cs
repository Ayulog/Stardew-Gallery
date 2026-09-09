using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;

namespace StardewGallery;

internal static class GallerySearchInputGuard
{
    private static readonly ConditionalWeakTable<object, GalleryKeyboardCapture> Captures = new();
    private static PropertyInfo buttonStates = null!;
    private static IInputHelper input = null!;
    private static IMonitor monitor = null!;

    internal static bool Ready { get; private set; }

    internal static void Apply(IModHelper helper, IMonitor log)
    {
        input = helper.Input;
        monitor = log;
        Type type = typeof(IModHelper).Assembly.GetType("StardewModdingAPI.Framework.Input.SInputState", throwOnError: true)!;
        MethodInfo update = AccessTools.DeclaredMethod(type, "TrueUpdate", Type.EmptyTypes)
            ?? throw new MissingMethodException(type.FullName, "TrueUpdate");
        buttonStates = type.GetProperty("ButtonStates") ?? throw new MissingMemberException(type.FullName, "ButtonStates");
        if (!typeof(IDictionary<SButton, SButtonState>).IsAssignableFrom(buttonStates.PropertyType))
            throw new InvalidOperationException("Unsupported SMAPI ButtonStates contract.");
        new Harmony("sjt38.StardewGallery.SearchInput").Patch(update,
            postfix: new HarmonyMethod(typeof(GallerySearchInputGuard), nameof(AfterInputUpdate)));
        Ready = true;
    }

    private static void AfterInputUpdate(object __instance)
    {
        IGallerySearchMenu? home = Game1.activeClickableMenu as IGallerySearchMenu;
        bool editing = Ready && home?.IsSearchSelected == true;
        Captures.TryGetValue(__instance, out GalleryKeyboardCapture? capture);
        if (!editing && capture?.HasCapturedKeys != true)
            return;
        capture ??= Captures.GetValue(__instance, _ => new GalleryKeyboardCapture());
        try
        {
            var states = (IDictionary<SButton, SButtonState>)buttonStates.GetValue(__instance)!;
            HashSet<int> down = states.Where(pair => pair.Key.TryGetKeyboard(out _) && pair.Value.IsDown())
                .Select(pair => (int)pair.Key).ToHashSet();
            bool escape = editing && states.TryGetValue(SButton.Escape, out SButtonState escapeState) && escapeState == SButtonState.Pressed;
            KeyboardState keyboard = ((InputState)__instance).GetKeyboardState();
            bool paste = keyboard.IsKeyDown(Keys.V) && (keyboard.IsKeyDown(Keys.LeftControl)
                || keyboard.IsKeyDown(Keys.RightControl) || keyboard.IsKeyDown(Keys.LeftWindows) || keyboard.IsKeyDown(Keys.RightWindows));
            foreach (int code in capture.Update(editing, down))
            {
                SButton button = (SButton)code;
                states.Remove(button);
                bool nativePasteKey = editing && (button is SButton.LeftControl or SButton.RightControl
                    or SButton.LeftWindows or SButton.RightWindows || paste && button == SButton.V);
                if (!nativePasteKey)
                    input.Suppress(button);
            }
            if (escape)
                home!.DeselectSearch();
        }
        catch (Exception error)
        {
            Ready = false;
            capture.Reset();
            home?.DeselectSearch();
            monitor.Log($"Search input isolation failed; search is disabled: {error}", LogLevel.Error);
        }
    }
}
