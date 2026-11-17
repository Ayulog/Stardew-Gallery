using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace StardewGallery;

internal static class ReplaySpeedPatches
{
    private static ReplayCoordinator replay = null!;
    [ThreadStatic]
    private static int eventUpdateDepth;

    internal static void Apply(IModHelper helper, ReplayCoordinator coordinator)
    {
        replay = coordinator;
        Harmony harmony = new(helper.ModRegistry.ModID);
        System.Reflection.MethodInfo eventUpdate = AccessTools.Method(typeof(Event), nameof(Event.Update));
        System.Reflection.MethodInfo dialogueUpdate = AccessTools.Method(typeof(DialogueBox), nameof(DialogueBox.update));
        System.Reflection.MethodInfo dialogueDraw = AccessTools.Method(typeof(DialogueBox), nameof(DialogueBox.draw), [typeof(SpriteBatch)])
            ?? throw new MissingMethodException(nameof(DialogueBox), nameof(DialogueBox.draw));
        System.Reflection.MethodInfo dialogueLeftClick = AccessTools.Method(typeof(DialogueBox), nameof(DialogueBox.receiveLeftClick), [typeof(int), typeof(int), typeof(bool)])
            ?? throw new MissingMethodException(nameof(DialogueBox), nameof(DialogueBox.receiveLeftClick));
        System.Reflection.MethodInfo dialogueRightClick = AccessTools.Method(typeof(DialogueBox), nameof(DialogueBox.receiveRightClick), [typeof(int), typeof(int), typeof(bool)])
            ?? throw new MissingMethodException(nameof(DialogueBox), nameof(DialogueBox.receiveRightClick));
        System.Reflection.MethodInfo dialogueKeyPress = AccessTools.Method(typeof(DialogueBox), nameof(DialogueBox.receiveKeyPress), [typeof(Keys)])
            ?? throw new MissingMethodException(nameof(DialogueBox), nameof(DialogueBox.receiveKeyPress));
        System.Reflection.MethodInfo eventDrawAfterMap = AccessTools.Method(typeof(Event), nameof(Event.drawAfterMap), [typeof(SpriteBatch)])
            ?? throw new MissingMethodException(nameof(Event), nameof(Event.drawAfterMap));
        harmony.Patch(eventUpdate,
            prefix: new HarmonyMethod(typeof(ReplaySpeedPatches), nameof(BeforeEventUpdate)),
            finalizer: new HarmonyMethod(typeof(ReplaySpeedPatches), nameof(AfterEventUpdate)));
        harmony.Patch(dialogueUpdate,
            prefix: new HarmonyMethod(typeof(ReplaySpeedPatches), nameof(BeforeDialogueUpdate)));
        harmony.Patch(dialogueDraw,
            prefix: new HarmonyMethod(typeof(ReplaySpeedPatches), nameof(BeforeDialogueDraw)));
        HarmonyMethod dialogueInputPrefix = new(typeof(ReplaySpeedPatches), nameof(BeforeDialogueInput));
        harmony.Patch(dialogueLeftClick, prefix: dialogueInputPrefix);
        harmony.Patch(dialogueRightClick, prefix: dialogueInputPrefix);
        harmony.Patch(dialogueKeyPress, prefix: dialogueInputPrefix);
        harmony.Patch(eventDrawAfterMap,
            prefix: new HarmonyMethod(typeof(ReplaySpeedPatches), nameof(BeforeEventDrawAfterMap)),
            finalizer: new HarmonyMethod(typeof(ReplaySpeedPatches), nameof(AfterEventDrawAfterMap)));
        string owner = helper.ModRegistry.ModID;
        if (Harmony.GetPatchInfo(eventUpdate)?.Prefixes.Any(patch => patch.owner == owner) != true
            || Harmony.GetPatchInfo(eventUpdate)?.Finalizers.Any(patch => patch.owner == owner) != true
            || Harmony.GetPatchInfo(dialogueUpdate)?.Prefixes.Any(patch => patch.owner == owner) != true
            || Harmony.GetPatchInfo(dialogueDraw)?.Prefixes.Any(patch => patch.owner == owner) != true
            || Harmony.GetPatchInfo(dialogueLeftClick)?.Prefixes.Any(patch => patch.owner == owner) != true
            || Harmony.GetPatchInfo(dialogueRightClick)?.Prefixes.Any(patch => patch.owner == owner) != true
            || Harmony.GetPatchInfo(dialogueKeyPress)?.Prefixes.Any(patch => patch.owner == owner) != true
            || Harmony.GetPatchInfo(eventDrawAfterMap)?.Prefixes.Any(patch => patch.owner == owner) != true)
            throw new InvalidOperationException("倍速与定格 Harmony 补丁验证失败。");
    }

    internal static bool IsChoice(DialogueBox dialogue) => dialogue.isQuestion || dialogue.responses.Length > 0;

    private static bool BeforeEventUpdate(Event __instance, ref GameTime time, out bool __state)
    {
        if (replay.IsScenePaused && replay.OwnsEvent(__instance))
        {
            __state = false;
            return false;
        }
        // Native commands such as jump and showFrame synchronously call Update again
        // with this already-scaled time. Only the outer call applies the multiplier.
        __state = true;
        if (eventUpdateDepth++ == 0)
            Scale(ref time, replay.EffectiveSpeedMultiplier);
        return true;
    }

    private static void AfterEventUpdate(ref bool __state)
    {
        // A finalizer also runs when a command/patch throws. A skipped prefix has no
        // matching entry, so it must not release another call's depth.
        if (__state)
        {
            __state = false;
            eventUpdateDepth--;
        }
    }

    private static bool BeforeDialogueUpdate(DialogueBox __instance, ref GameTime time)
    {
        if (replay.ShouldFreezeDialogue)
            return false;
        if (!IsChoice(__instance))
            Scale(ref time, replay.EffectiveSpeedMultiplier);
        return true;
    }

    private static bool BeforeDialogueDraw() => !replay.ShouldHideSceneUi;

    private static bool BeforeDialogueInput() => !replay.ShouldHideSceneUi;

    private static void BeforeEventDrawAfterMap(Event __instance, out bool? __state)
    {
        __state = replay.ShouldHideSceneUi && replay.OwnsEvent(__instance) ? __instance.skippable : null;
        if (__state.HasValue)
            __instance.skippable = false;
    }

    private static void AfterEventDrawAfterMap(Event __instance, bool? __state)
    {
        if (__state.HasValue)
            __instance.skippable = __state.Value;
    }

    private static void Scale(ref GameTime time, int multiplier)
    {
        if (multiplier > 1)
            time = new GameTime(time.TotalGameTime, TimeSpan.FromTicks(time.ElapsedGameTime.Ticks * multiplier), time.IsRunningSlowly);
    }
}
