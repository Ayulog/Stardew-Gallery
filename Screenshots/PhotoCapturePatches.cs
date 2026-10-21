using HarmonyLib;
using StardewModdingAPI;
using StardewValley;

namespace StardewGallery;

/// <summary>
/// At 100% zoom outside split-screen, Stardew Valley 1.6.15 draws the world straight to the back buffer,
/// so RenderedWorld exposes only SMAPI's empty UI target. For the single frame with a pending photo,
/// route the world through Game1.screen, the same path the game uses for other zoom levels.
/// </summary>
internal static class PhotoCapturePatches
{
    private static Func<bool> pending = () => false;

    /// <param name="hasPendingPhoto">Whether the screen being drawn has a photo request.</param>
    internal static void Apply(IModHelper helper, Func<bool> hasPendingPhoto)
    {
        pending = hasPendingPhoto;
        new Harmony(helper.ModRegistry.ModID + ".PhotoCapture").Patch(
            AccessTools.Method(typeof(Game1), nameof(Game1.ShouldDrawOnBuffer)) ?? throw new MissingMethodException(nameof(Game1), nameof(Game1.ShouldDrawOnBuffer)),
            postfix: new HarmonyMethod(typeof(PhotoCapturePatches), nameof(AfterShouldDrawOnBuffer)));
    }

    private static void AfterShouldDrawOnBuffer(ref bool __result)
    {
        if (!__result && pending())
            __result = true;
    }
}
