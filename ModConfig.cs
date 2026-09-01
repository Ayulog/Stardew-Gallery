using StardewModdingAPI;
using StardewModdingAPI.Utilities;

namespace StardewGallery;

internal sealed class ModConfig
{
    public bool AutoAdvanceDialogue { get; set; }

    public bool ShowRollbackWarning { get; set; } = true;

    public bool DebugDiagnostics { get; set; }

    public KeybindList GalleryKeys { get; set; } = new(SButton.G);

    public KeybindList ReplaySpeedKeys { get; set; } = new(SButton.RightShoulder);
}
