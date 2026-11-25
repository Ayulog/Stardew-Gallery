using System.Reflection;
using System.Runtime.CompilerServices;
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
    private static RasterizerState? clipRasterizer;
    private static bool reversePatchesReady;
    [ThreadStatic]
    private static int eventUpdateDepth;
    [ThreadStatic]
    private static bool isDrawingVanilla;
    [ThreadStatic]
    private static bool inSplitModdedPass;
    [ThreadStatic]
    private static Dialogue? activeVanillaDialogue;
    [ThreadStatic]
    private static Texture2D? activeVanillaTexture;

    private static RasterizerState ClipRasterizer => clipRasterizer ??= new RasterizerState { ScissorTestEnable = true };

    internal static void Apply(IModHelper helper, ReplayCoordinator coordinator)
    {
        replay = coordinator;
        Harmony harmony = new(helper.ModRegistry.ModID);
        MethodInfo eventUpdate = AccessTools.Method(typeof(Event), nameof(Event.Update));
        MethodInfo dialogueUpdate = AccessTools.Method(typeof(DialogueBox), nameof(DialogueBox.update));
        MethodInfo dialogueDraw = AccessTools.Method(typeof(DialogueBox), nameof(DialogueBox.draw), [typeof(SpriteBatch)])
            ?? throw new MissingMethodException(nameof(DialogueBox), nameof(DialogueBox.draw));
        MethodInfo dialogueDrawBox = AccessTools.Method(typeof(DialogueBox), nameof(DialogueBox.drawBox), [typeof(SpriteBatch), typeof(int), typeof(int), typeof(int), typeof(int)])
            ?? throw new MissingMethodException(nameof(DialogueBox), nameof(DialogueBox.drawBox));
        MethodInfo dialogueDrawPortrait = AccessTools.Method(typeof(DialogueBox), nameof(DialogueBox.drawPortrait), [typeof(SpriteBatch)])
            ?? throw new MissingMethodException(nameof(DialogueBox), nameof(DialogueBox.drawPortrait));
        MethodInfo dialogueIsPortraitBox = AccessTools.Method(typeof(DialogueBox), nameof(DialogueBox.isPortraitBox), Type.EmptyTypes)
            ?? throw new MissingMethodException(nameof(DialogueBox), nameof(DialogueBox.isPortraitBox));
        MethodInfo dialogueGetPortraitIndex = AccessTools.Method(typeof(Dialogue), nameof(Dialogue.getPortraitIndex), Type.EmptyTypes)
            ?? throw new MissingMethodException(nameof(Dialogue), nameof(Dialogue.getPortraitIndex));
        MethodInfo dialogueLeftClick = AccessTools.Method(typeof(DialogueBox), nameof(DialogueBox.receiveLeftClick), [typeof(int), typeof(int), typeof(bool)])
            ?? throw new MissingMethodException(nameof(DialogueBox), nameof(DialogueBox.receiveLeftClick));
        MethodInfo dialogueRightClick = AccessTools.Method(typeof(DialogueBox), nameof(DialogueBox.receiveRightClick), [typeof(int), typeof(int), typeof(bool)])
            ?? throw new MissingMethodException(nameof(DialogueBox), nameof(DialogueBox.receiveRightClick));
        MethodInfo dialogueKeyPress = AccessTools.Method(typeof(DialogueBox), nameof(DialogueBox.receiveKeyPress), [typeof(Keys)])
            ?? throw new MissingMethodException(nameof(DialogueBox), nameof(DialogueBox.receiveKeyPress));
        MethodInfo eventDrawAfterMap = AccessTools.Method(typeof(Event), nameof(Event.drawAfterMap), [typeof(SpriteBatch)])
            ?? throw new MissingMethodException(nameof(Event), nameof(Event.drawAfterMap));

        harmony.CreateReversePatcher(dialogueDraw, new HarmonyMethod(typeof(ReplaySpeedPatches), nameof(OriginalDialogueDraw))).Patch(HarmonyReversePatchType.Original);
        harmony.CreateReversePatcher(dialogueDrawBox, new HarmonyMethod(typeof(ReplaySpeedPatches), nameof(OriginalDialogueDrawBox))).Patch(HarmonyReversePatchType.Original);
        harmony.CreateReversePatcher(dialogueDrawPortrait, new HarmonyMethod(typeof(ReplaySpeedPatches), nameof(OriginalDialogueDrawPortrait))).Patch(HarmonyReversePatchType.Original);
        harmony.CreateReversePatcher(dialogueIsPortraitBox, new HarmonyMethod(typeof(ReplaySpeedPatches), nameof(OriginalDialogueIsPortraitBox))).Patch(HarmonyReversePatchType.Original);
        reversePatchesReady = true;

        harmony.Patch(eventUpdate,
            prefix: new HarmonyMethod(typeof(ReplaySpeedPatches), nameof(BeforeEventUpdate)),
            finalizer: new HarmonyMethod(typeof(ReplaySpeedPatches), nameof(AfterEventUpdate)));
        harmony.Patch(dialogueUpdate,
            prefix: new HarmonyMethod(typeof(ReplaySpeedPatches), nameof(BeforeDialogueUpdate)));
        harmony.Patch(dialogueDraw,
            prefix: new HarmonyMethod(typeof(ReplaySpeedPatches), nameof(BeforeDialogueDrawComparison)) { priority = Priority.First });
        harmony.Patch(dialogueIsPortraitBox,
            prefix: new HarmonyMethod(typeof(ReplaySpeedPatches), nameof(BeforeDialogueIsPortraitBox)) { priority = Priority.First });
        harmony.Patch(dialogueDrawBox,
            prefix: new HarmonyMethod(typeof(ReplaySpeedPatches), nameof(BeforeDialogueDrawBox)) { priority = Priority.First });
        harmony.Patch(dialogueDrawPortrait,
            prefix: new HarmonyMethod(typeof(ReplaySpeedPatches), nameof(BeforeDialogueDrawPortrait)) { priority = Priority.First });
        harmony.Patch(dialogueGetPortraitIndex,
            postfix: new HarmonyMethod(typeof(ReplaySpeedPatches), nameof(AfterDialogueGetPortraitIndex)));
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
            || Harmony.GetPatchInfo(dialogueIsPortraitBox)?.Prefixes.Any(patch => patch.owner == owner) != true
            || Harmony.GetPatchInfo(dialogueDrawBox)?.Prefixes.Any(patch => patch.owner == owner) != true
            || Harmony.GetPatchInfo(dialogueDrawPortrait)?.Prefixes.Any(patch => patch.owner == owner) != true
            || Harmony.GetPatchInfo(dialogueGetPortraitIndex)?.Postfixes.Any(patch => patch.owner == owner) != true
            || Harmony.GetPatchInfo(dialogueLeftClick)?.Prefixes.Any(patch => patch.owner == owner) != true
            || Harmony.GetPatchInfo(dialogueRightClick)?.Prefixes.Any(patch => patch.owner == owner) != true
            || Harmony.GetPatchInfo(dialogueKeyPress)?.Prefixes.Any(patch => patch.owner == owner) != true
            || Harmony.GetPatchInfo(eventDrawAfterMap)?.Prefixes.Any(patch => patch.owner == owner) != true)
            throw new InvalidOperationException("倍速、定格与立绘对比 Harmony 补丁验证失败。");
    }

    internal static bool IsChoice(DialogueBox dialogue) => dialogue.isQuestion || (dialogue.responses?.Length ?? 0) > 0;

    internal static bool IsOriginalPortraitBox(DialogueBox dialogue)
    {
        if (reversePatchesReady)
        {
            try
            {
                return OriginalDialogueIsPortraitBox(dialogue);
            }
            catch
            {
                // Fall back to direct field check if invoked in a headless test without reverse patch.
            }
        }
        return dialogue.characterDialogue?.speaker?.Portrait is not null
            && dialogue.characterDialogue.showPortrait
            && dialogue.width >= 448;
    }

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

    // Kept with 0 parameters for SourcesRuntimeChecks reflection compatibility.
    private static bool BeforeDialogueDraw() => !replay.ShouldHideSceneUi;

    [HarmonyPriority(Priority.First)]
    private static bool BeforeDialogueDrawComparison(DialogueBox __instance, SpriteBatch b)
    {
        if (replay.ShouldHideSceneUi)
            return false;
        if (inSplitModdedPass || !replay.IsActive)
            return true;

        PortraitCompareMode mode = replay.PortraitComparison.EffectiveModeFor(__instance, out Texture2D? vanillaTex);
        if (mode == PortraitCompareMode.Modded || vanillaTex is null)
            return true;

        if (mode == PortraitCompareMode.Vanilla)
        {
            DrawVanillaDialogue(__instance, b, vanillaTex);
            return false;
        }

        DrawSplitDialogue(__instance, b, vanillaTex);
        return false;
    }

    [HarmonyPriority(Priority.First)]
    private static bool BeforeDialogueIsPortraitBox(DialogueBox __instance, ref bool __result)
    {
        if (!isDrawingVanilla)
            return true;
        __result = OriginalDialogueIsPortraitBox(__instance);
        return false;
    }

    [HarmonyPriority(Priority.First)]
    private static bool BeforeDialogueDrawBox(DialogueBox __instance, SpriteBatch b, int xPos, int yPos, int boxWidth, int boxHeight)
    {
        if (!isDrawingVanilla)
            return true;
        OriginalDialogueDrawBox(__instance, b, xPos, yPos, boxWidth, boxHeight);
        return false;
    }

    [HarmonyPriority(Priority.First)]
    private static bool BeforeDialogueDrawPortrait(DialogueBox __instance, SpriteBatch b)
    {
        if (!isDrawingVanilla)
            return true;
        OriginalDialogueDrawPortrait(__instance, b);
        return false;
    }

    private static void AfterDialogueGetPortraitIndex(Dialogue __instance, ref int __result)
    {
        if (!isDrawingVanilla || activeVanillaTexture is null || !ReferenceEquals(__instance, activeVanillaDialogue))
            return;
        __result = PortraitComparisonRules.ClampPortraitFrame(activeVanillaTexture.Width, activeVanillaTexture.Height, __result);
    }

    private static void DrawVanillaDialogue(DialogueBox dialogue, SpriteBatch b, Texture2D vanillaTexture)
    {
        Dialogue? cd = dialogue.characterDialogue;
        if (cd is null)
            return;

        Texture2D? prevOverride = cd.overridePortrait;
        Dialogue? prevDialogue = activeVanillaDialogue;
        Texture2D? prevTexture = activeVanillaTexture;
        bool prevDrawing = isDrawingVanilla;
        try
        {
            cd.overridePortrait = vanillaTexture;
            activeVanillaDialogue = cd;
            activeVanillaTexture = vanillaTexture;
            isDrawingVanilla = true;
            OriginalDialogueDraw(dialogue, b);
        }
        finally
        {
            isDrawingVanilla = prevDrawing;
            activeVanillaDialogue = prevDialogue;
            activeVanillaTexture = prevTexture;
            cd.overridePortrait = prevOverride;
        }
    }

    private static void DrawSplitDialogue(DialogueBox dialogue, SpriteBatch b, Texture2D vanillaTexture)
    {
        Rectangle viewportBounds = b.GraphicsDevice.Viewport.Bounds;
        if (viewportBounds.Width <= 1 || viewportBounds.Height <= 0)
        {
            DrawVanillaDialogue(dialogue, b, vanillaTexture);
            return;
        }

        int splitX = PortraitComparisonRules.ComputeSplitX(dialogue.x, dialogue.width, viewportBounds.Width);
        Rectangle oldScissor = b.GraphicsDevice.ScissorRectangle;
        Rectangle leftClip = Rectangle.Intersect(new Rectangle(0, 0, splitX, viewportBounds.Height), viewportBounds);
        Rectangle rightClip = Rectangle.Intersect(new Rectangle(splitX, 0, Math.Max(0, viewportBounds.Width - splitX), viewportBounds.Height), viewportBounds);

        bool batchOpen = true;
        bool prevSplitPass = inSplitModdedPass;
        try
        {
            b.End();
            batchOpen = false;

            if (rightClip.Width > 0 && rightClip.Height > 0)
            {
                b.GraphicsDevice.ScissorRectangle = rightClip;
                b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, ClipRasterizer);
                batchOpen = true;
                inSplitModdedPass = true;
                dialogue.draw(b);
                inSplitModdedPass = prevSplitPass;
                b.End();
                batchOpen = false;
            }

            if (leftClip.Width > 0 && leftClip.Height > 0)
            {
                b.GraphicsDevice.ScissorRectangle = leftClip;
                b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, ClipRasterizer);
                batchOpen = true;
                DrawVanillaDialogue(dialogue, b, vanillaTexture);
                b.End();
                batchOpen = false;
            }
        }
        finally
        {
            inSplitModdedPass = prevSplitPass;
            if (batchOpen)
            {
                try { b.End(); }
                catch { }
            }
            b.GraphicsDevice.ScissorRectangle = oldScissor;
            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
        }

        int dividerY = dialogue.height > 0 ? Math.Max(0, dialogue.y) : Math.Max(0, viewportBounds.Height - 448);
        int dividerBottom = dialogue.height > 0 ? Math.Min(viewportBounds.Height, dialogue.y + dialogue.height) : viewportBounds.Height;
        int dividerHeight = Math.Max(1, dividerBottom - dividerY);
        b.Draw(Game1.staminaRect, new Rectangle(splitX - 1, dividerY, 2, dividerHeight), new Color(255, 215, 64));
        dialogue.drawMouse(b);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void OriginalDialogueDraw(DialogueBox __instance, SpriteBatch b)
        => throw new NotImplementedException();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void OriginalDialogueDrawBox(DialogueBox __instance, SpriteBatch b, int xPos, int yPos, int boxWidth, int boxHeight)
        => throw new NotImplementedException();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void OriginalDialogueDrawPortrait(DialogueBox __instance, SpriteBatch b)
        => throw new NotImplementedException();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool OriginalDialogueIsPortraitBox(DialogueBox __instance)
        => throw new NotImplementedException();

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
