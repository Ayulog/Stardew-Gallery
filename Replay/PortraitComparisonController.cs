using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace StardewGallery;

internal sealed class PortraitComparisonController(IMonitor monitor, IModHelper helper) : IDisposable
{
    private readonly PortraitComparisonStateMachine stateMachine = new();
    private readonly Dictionary<string, Texture2D?> textureCache = new(StringComparer.OrdinalIgnoreCase);
    private ContentManager? vanillaContent;
    private SButton pressedButton = SButton.None;

    internal PortraitCompareMode CurrentMode => stateMachine.Mode;
    internal bool IsHolding => stateMachine.IsHolding;
    internal SButton PressedButton => pressedButton;

    internal bool CanCompareDialogue(DialogueBox? dialogue)
        => TryGetSpeaker(dialogue, out _);

    internal bool HasVanillaPortraitForSpeaker(DialogueBox? dialogue)
        => TryGetSpeaker(dialogue, out NPC? speaker)
            && speaker is not null
            && TryResolveVanillaPortrait(speaker, out _);

    internal void BeginPress(double nowMs, SButton button = SButton.None)
    {
        if (!stateMachine.IsHolding)
            pressedButton = button;
        stateMachine.BeginPress(nowMs);
    }

    internal bool UpdateHold(double nowMs)
        => stateMachine.UpdateHold(nowMs);

    internal PortraitCompareMode EndPress()
        => EndPress(out _);

    internal PortraitCompareMode EndPress(out bool changedMode)
    {
        pressedButton = SButton.None;
        return stateMachine.EndPress(out changedMode);
    }

    internal void CancelHold()
    {
        pressedButton = SButton.None;
        stateMachine.CancelHold();
    }

    internal void SyncActiveDialogue(DialogueBox? dialogue)
    {
        string? contextKey = TryGetSpeaker(dialogue, out NPC? speaker) && speaker is not null
            ? $"{speaker.Name}\u001f{speaker.Portrait?.Name ?? string.Empty}\u001f{speaker.LastAppearanceId ?? string.Empty}"
            : null;
        stateMachine.SyncDialogueContext(contextKey);
        if (!stateMachine.IsHolding)
            pressedButton = SButton.None;
    }

    internal PortraitCompareMode EffectiveModeFor(DialogueBox dialogue, out Texture2D? vanillaTexture)
    {
        vanillaTexture = null;
        PortraitCompareMode mode = stateMachine.Mode;
        if (mode == PortraitCompareMode.Modded
            || dialogue.transitioning
            || !TryGetSpeaker(dialogue, out NPC? speaker)
            || speaker is null
            || !TryResolveVanillaPortrait(speaker, out vanillaTexture)
            || vanillaTexture is null
            || vanillaTexture.IsDisposed)
        {
            vanillaTexture = null;
            return PortraitCompareMode.Modded;
        }
        return mode;
    }

    internal bool TryResolveVanillaPortrait(NPC speaker, out Texture2D? texture)
    {
        texture = null;
        if (speaker is null || string.IsNullOrWhiteSpace(speaker.Name))
            return false;

        string? currentAsset = speaker.Portrait?.Name;
        string? textureName = ReadTextureNameForCharacter(speaker.Name);
        string? appearanceId = speaker.LastAppearanceId;
        string cacheKey = $"{currentAsset ?? string.Empty}\u001f{speaker.Name}\u001f{textureName ?? string.Empty}\u001f{appearanceId ?? string.Empty}";

        if (textureCache.TryGetValue(cacheKey, out Texture2D? cached))
        {
            if (cached is null)
                return false;
            if (!cached.IsDisposed)
            {
                texture = cached;
                return true;
            }
            textureCache.Remove(cacheKey);
        }

        string? resolvedAsset = PortraitComparisonRules.ResolveVanillaAsset(
            currentAsset,
            speaker.Name,
            candidate => PackFiles.ResolveContentAsset(Constants.ContentPath, candidate, ".xnb"),
            textureName,
            appearanceId);

        if (resolvedAsset is null)
        {
            textureCache[cacheKey] = null;
            return false;
        }

        try
        {
            vanillaContent ??= new ContentManager(Game1.content.ServiceProvider, Constants.ContentPath);
            Texture2D loaded = vanillaContent.Load<Texture2D>(resolvedAsset);
            if (loaded is null || loaded.IsDisposed)
            {
                textureCache[cacheKey] = null;
                return false;
            }
            textureCache[cacheKey] = loaded;
            texture = loaded;
            return true;
        }
        catch (Exception error)
        {
            monitor.Log($"未能读取原版立绘资源 {resolvedAsset}：{error.Message}", LogLevel.Debug);
            textureCache[cacheKey] = null;
            return false;
        }
    }

    internal void ShowNoVanillaHint()
        => Game1.addHUDMessage(new HUDMessage(helper.Translation.Get("replay.portrait-no-vanilla"), HUDMessage.newQuest_type));

    internal void Reset()
    {
        stateMachine.Reset();
        pressedButton = SButton.None;
        textureCache.Clear();
        ContentManager? content = vanillaContent;
        vanillaContent = null;
        if (content is not null)
        {
            try { content.Dispose(); }
            catch (Exception error) { monitor.Log($"未能读取或释放原版立绘 ContentManager：{error.Message}", LogLevel.Debug); }
        }
    }

    public void Dispose() => Reset();

    private static bool TryGetSpeaker(DialogueBox? dialogue, out NPC? speaker)
    {
        speaker = null;
        if (dialogue is null
            || dialogue.characterDialogue?.speaker is not { } candidate
            || string.IsNullOrWhiteSpace(candidate.Name)
            || (!dialogue.isPortraitBox() && !ReplaySpeedPatches.IsOriginalPortraitBox(dialogue)))
        {
            return false;
        }

        speaker = candidate;
        return true;
    }

    private static string? ReadTextureNameForCharacter(string characterName)
    {
        try
        {
            return NPC.getTextureNameForCharacter(characterName);
        }
        catch
        {
            // Headless checks or uninitialized character data still resolve via speaker.Name.
            return null;
        }
    }
}
