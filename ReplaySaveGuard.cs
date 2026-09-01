using System.Collections;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;

namespace StardewGallery;

internal static class ReplaySaveGuard
{
    private static ReplayCoordinator replay = null!;
    private static IMonitor monitor = null!;
    private static ITranslationHelper i18n = null!;

    internal static void Apply(IModHelper helper, IMonitor log, ReplayCoordinator coordinator)
    {
        replay = coordinator;
        monitor = log;
        i18n = helper.Translation;
        new Harmony(helper.ModRegistry.ModID).Patch(
            AccessTools.Method(typeof(SaveGame), "getSaveEnumerator"),
            prefix: new HarmonyMethod(typeof(ReplaySaveGuard), nameof(BeforeSave)));
    }

    private static bool BeforeSave(ref IEnumerator<int> __result)
    {
        if (!replay.IsActive)
            return true;
        monitor.Log("已阻止回放期间的存档请求。", LogLevel.Warn);
        Game1.addHUDMessage(new HUDMessage(i18n.Get("replay.save-blocked"), HUDMessage.error_type));
        __result = Enumerable.Empty<int>().GetEnumerator();
        return false;
    }
}
