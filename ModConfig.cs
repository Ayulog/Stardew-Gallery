using StardewModdingAPI;
using StardewModdingAPI.Utilities;

namespace StardewGallery;

internal sealed class ModConfig
{
    public bool AutoAdvanceDialogue { get; set; }

    public bool ShowRollbackWarning { get; set; } = true;

    public bool DebugDiagnostics { get; set; }

    /// <summary>Optional runtime modification tracing; file-origin matching is always available. Requires restart and SMAPI 4.5.2.</summary>
    public bool EnableEventSourceDiagnostics { get; set; }

    public bool EnableOrdinaryEventReplay { get; set; }

    /// <summary>Opt-in original-provider filtering using the public AI Mod Exclusion list.</summary>
    public bool EnableAiModExclusion { get; set; }

    public KeybindList GalleryKeys { get; set; } = new(SButton.G);

    public KeybindList ReplaySpeedKeys { get; set; } = new(SButton.RightShoulder);

    public KeybindList ScreenshotKeys { get; set; } = KeybindList.Parse("F8, LeftShoulder");
}
