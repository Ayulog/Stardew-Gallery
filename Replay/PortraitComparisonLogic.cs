namespace StardewGallery;

internal enum PortraitCompareMode
{
    Modded = 0,
    Vanilla = 1,
    Split = 2
}

internal static class PortraitComparisonRules
{
    internal const int CellSize = 64;
    internal const double HoldThresholdMs = 220.0;
    private const string PortraitsPrefix = "Portraits/";

    /// <summary>
    /// Normalizes a portrait asset path (e.g. "Portraits\\Abigail_Winter.xnb" -> "Portraits/Abigail_Winter").
    /// Returns empty string when null or whitespace.
    /// </summary>
    internal static string NormalizeAssetPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        string normalized = path.Trim().Replace('\\', '/');
        while (normalized.StartsWith('/'))
            normalized = normalized[1..];
        if (normalized.EndsWith(".xnb", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[..^4];
        return normalized;
    }

    /// <summary>
    /// Builds ordered unpatched vanilla portrait candidate asset paths:
    /// 1) current appearance asset when it starts with "Portraits/" (e.g. "Portraits/Abigail_Winter");
    /// 2) "Portraits/&lt;Base&gt;_&lt;LastAppearanceId&gt;" for seasonal/beach/resort variants;
    /// 3) "Portraits/&lt;TextureName&gt;" from NPC.getTextureNameForCharacter;
    /// 4) base character asset "Portraits/&lt;CharacterName&gt;";
    /// 5) underscore-trimmed fallback if both textureName and characterName were omitted.
    /// Deduplicates case-insensitively while preserving priority order.
    /// </summary>
    internal static IReadOnlyList<string> BuildVanillaAssetCandidates(
        string? currentPortraitAssetName,
        string? characterName,
        string? textureName = null,
        string? lastAppearanceId = null)
    {
        List<string> candidates = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        void AddCandidate(string? candidate)
        {
            if (!string.IsNullOrWhiteSpace(candidate) && seen.Add(candidate))
                candidates.Add(candidate);
        }

        static string ExtractVanillaLeaf(string? raw)
        {
            string normalized = NormalizeAssetPath(raw);
            if (normalized.Length == 0)
                return string.Empty;
            if (normalized.StartsWith(PortraitsPrefix, StringComparison.OrdinalIgnoreCase))
                return normalized[PortraitsPrefix.Length..];
            return normalized.Contains('/') ? string.Empty : normalized;
        }

        string normalizedCurrent = NormalizeAssetPath(currentPortraitAssetName);
        if (normalizedCurrent.StartsWith(PortraitsPrefix, StringComparison.OrdinalIgnoreCase)
            && normalizedCurrent.Length > PortraitsPrefix.Length)
        {
            AddCandidate(normalizedCurrent);
        }

        string textureLeaf = ExtractVanillaLeaf(textureName);
        string characterLeaf = ExtractVanillaLeaf(characterName);
        string appearance = string.IsNullOrWhiteSpace(lastAppearanceId) ? string.Empty : lastAppearanceId.Trim();

        if (appearance.Length > 0)
        {
            if (textureLeaf.Length > 0)
                AddCandidate($"{PortraitsPrefix}{textureLeaf}_{appearance}");
            if (characterLeaf.Length > 0)
                AddCandidate($"{PortraitsPrefix}{characterLeaf}_{appearance}");
        }

        if (textureLeaf.Length > 0)
            AddCandidate(PortraitsPrefix + textureLeaf);

        if (characterLeaf.Length > 0)
            AddCandidate(PortraitsPrefix + characterLeaf);

        if (textureLeaf.Length == 0 && characterLeaf.Length == 0
            && normalizedCurrent.StartsWith(PortraitsPrefix, StringComparison.OrdinalIgnoreCase))
        {
            string leaf = normalizedCurrent[PortraitsPrefix.Length..];
            int underscore = leaf.LastIndexOf('_');
            if (underscore > 0)
                AddCandidate(PortraitsPrefix + leaf[..underscore]);
        }

        return candidates;
    }

    /// <summary>
    /// Iterates BuildVanillaAssetCandidates and returns the first asset resolved by <paramref name="resolveAsset"/>.
    /// </summary>
    internal static string? ResolveVanillaAsset(
        string? currentPortraitAssetName,
        string? characterName,
        Func<string, string?> resolveAsset,
        string? textureName = null,
        string? lastAppearanceId = null)
    {
        ArgumentNullException.ThrowIfNull(resolveAsset);

        foreach (string candidate in BuildVanillaAssetCandidates(currentPortraitAssetName, characterName, textureName, lastAppearanceId))
        {
            if (resolveAsset(candidate) is { Length: > 0 } resolved)
                return resolved;
        }
        return null;
    }

    /// <summary>
    /// Computes total 64x64 portrait frames in a vanilla portrait sheet: (w / 64) * (h / 64).
    /// Returns 0 when either dimension is under 64px.
    /// </summary>
    internal static int CountSheetFrames(int textureWidth, int textureHeight)
    {
        if (textureWidth < CellSize || textureHeight < CellSize)
            return 0;

        int cols = textureWidth / CellSize;
        int rows = textureHeight / CellSize;
        long total = (long)cols * rows;
        return total > int.MaxValue ? int.MaxValue : (int)total;
    }

    /// <summary>
    /// Clamps requestedFrame to [0, totalFrames - 1]; any negative or out-of-range frame safely falls back to 0.
    /// </summary>
    internal static int ClampPortraitFrame(int textureWidth, int textureHeight, int requestedFrame)
    {
        int totalFrames = CountSheetFrames(textureWidth, textureHeight);
        if (totalFrames <= 0 || requestedFrame < 0 || requestedFrame >= totalFrames)
            return 0;
        return requestedFrame;
    }

    /// <summary>
    /// Computes the 64x64 source rectangle for <paramref name="requestedFrame"/>, falling back to frame 0 when
    /// out of range. Returns false only when the texture is smaller than 64x64.
    /// </summary>
    internal static bool TryGetPortraitSourceRegion(
        int textureWidth,
        int textureHeight,
        int requestedFrame,
        out (int X, int Y, int Width, int Height) sourceRect)
    {
        if (textureWidth < CellSize || textureHeight < CellSize)
        {
            sourceRect = default;
            return false;
        }

        int cols = textureWidth / CellSize;
        int frame = ClampPortraitFrame(textureWidth, textureHeight, requestedFrame);
        sourceRect = ((frame % cols) * CellSize, (frame / cols) * CellSize, CellSize, CellSize);
        return true;
    }

    /// <summary>
    /// Computes the vertical split axis X coordinate along the vanilla portrait centerline (boxX + boxWidth - 224)
    /// when the dialogue box is wide enough for the 448px portrait compartment, falling back to viewportWidth / 2
    /// and clamping inside [1, Math.Max(1, viewportWidth - 1)].
    /// </summary>
    internal static int ComputeSplitX(int boxX, int boxWidth, int viewportWidth)
    {
        int maxSplit = Math.Max(1, viewportWidth - 1);
        int rawSplit = boxWidth >= 448 ? boxX + boxWidth - 224 : viewportWidth / 2;
        return Math.Clamp(rawSplit, 1, maxSplit);
    }
}

internal sealed class PortraitComparisonStateMachine
{
    internal const double HoldThresholdMs = PortraitComparisonRules.HoldThresholdMs;

    private PortraitCompareMode preHoldMode = PortraitCompareMode.Modded;
    private double holdStartMs;
    private string? currentDialogueKey;

    internal PortraitCompareMode Mode { get; private set; } = PortraitCompareMode.Modded;
    internal bool IsHolding { get; private set; }
    internal bool HoldTriggeredSplit { get; private set; }

    internal void BeginPress(double nowMs)
    {
        if (IsHolding)
            return;
        preHoldMode = Mode;
        holdStartMs = nowMs;
        IsHolding = true;
        HoldTriggeredSplit = false;
    }

    /// <summary>Returns true on the tick the hold crosses &gt;220 ms and enters Split mode.</summary>
    internal bool UpdateHold(double nowMs)
    {
        if (!IsHolding || HoldTriggeredSplit)
            return false;
        if (nowMs - holdStartMs > HoldThresholdMs)
        {
            HoldTriggeredSplit = true;
            Mode = PortraitCompareMode.Split;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Ends the active press: &gt;220 ms hold keeps Split active on screen hands-free;
    /// short press from Split returns to Modded; short press otherwise toggles Modded &lt;-&gt; Vanilla.
    /// </summary>
    internal PortraitCompareMode EndPress() => EndPress(out _);

    internal PortraitCompareMode EndPress(out bool changedMode)
    {
        if (!IsHolding)
        {
            changedMode = false;
            return Mode;
        }
        IsHolding = false;
        if (HoldTriggeredSplit)
        {
            HoldTriggeredSplit = false;
            Mode = PortraitCompareMode.Split;
            changedMode = false;
        }
        else
        {
            Mode = preHoldMode switch
            {
                PortraitCompareMode.Modded => PortraitCompareMode.Vanilla,
                PortraitCompareMode.Vanilla => PortraitCompareMode.Modded,
                _ => PortraitCompareMode.Modded
            };
            changedMode = true;
        }
        return Mode;
    }

    /// <summary>Cancels any in-progress hold without reverting an already-sticky Split mode.</summary>
    internal void CancelHold()
    {
        if (!IsHolding)
            return;
        IsHolding = false;
        HoldTriggeredSplit = false;
    }

    /// <summary>Resets mode and active hold when switching to a different dialogue/speaker or closing dialogue.</summary>
    internal void SyncDialogueContext(string? dialogueKey)
    {
        if (string.Equals(currentDialogueKey, dialogueKey, StringComparison.Ordinal))
            return;
        currentDialogueKey = dialogueKey;
        IsHolding = false;
        HoldTriggeredSplit = false;
        Mode = PortraitCompareMode.Modded;
        preHoldMode = PortraitCompareMode.Modded;
    }

    internal void Reset()
    {
        currentDialogueKey = null;
        IsHolding = false;
        HoldTriggeredSplit = false;
        Mode = PortraitCompareMode.Modded;
        preHoldMode = PortraitCompareMode.Modded;
    }
}
