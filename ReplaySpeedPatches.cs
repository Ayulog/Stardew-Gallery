using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace StardewGallery;

internal static class ReplaySpeedPatches
{
    private static ReplayCoordinator replay = null!;

    internal static void Apply(IModHelper helper, ReplayCoordinator coordinator)
    {
        replay = coordinator;
        Harmony harmony = new(helper.ModRegistry.ModID);
        System.Reflection.MethodInfo eventUpdate = AccessTools.Method(typeof(Event), nameof(Event.Update));
        System.Reflection.MethodInfo dialogueUpdate = AccessTools.Method(typeof(DialogueBox), nameof(DialogueBox.update));
        harmony.Patch(eventUpdate,
            prefix: new HarmonyMethod(typeof(ReplaySpeedPatches), nameof(BeforeEventUpdate)));
        harmony.Patch(dialogueUpdate,
            prefix: new HarmonyMethod(typeof(ReplaySpeedPatches), nameof(BeforeDialogueUpdate)));
        string owner = helper.ModRegistry.ModID;
        if (Harmony.GetPatchInfo(eventUpdate)?.Prefixes.Any(patch => patch.owner == owner) != true
            || Harmony.GetPatchInfo(dialogueUpdate)?.Prefixes.Any(patch => patch.owner == owner) != true)
            throw new InvalidOperationException("倍速 Harmony 补丁验证失败。");
    }

    internal static bool IsChoice(DialogueBox dialogue) => dialogue.isQuestion || dialogue.responses.Length > 0;

    private static void BeforeEventUpdate(ref GameTime time) => Scale(ref time, replay.EffectiveSpeedMultiplier);

    private static void BeforeDialogueUpdate(DialogueBox __instance, ref GameTime time)
    {
        if (!IsChoice(__instance))
            Scale(ref time, replay.EffectiveSpeedMultiplier);
    }

    private static void Scale(ref GameTime time, int multiplier)
    {
        if (multiplier > 1)
            time = new GameTime(time.TotalGameTime, TimeSpan.FromTicks(time.ElapsedGameTime.Ticks * multiplier), time.IsRunningSlowly);
    }
}
