using StardewGallery;

/// <summary>
/// Pure in-memory M2 checks covering unpatched vanilla portrait asset candidate resolution,
/// 64x64 portrait sheet frame counting &amp; clamping, split-compare axis math,
/// short-press vs &gt;220ms hold comparison state machine transitions, and M2 i18n key presence.
/// </summary>
internal static class PortraitComparisonChecks
{
    private static int assertions;

    internal static void Run()
    {
        assertions = 0;
        CheckAssetCandidatesAndResolution();
        CheckFrameCountingAndClamping();
        CheckComputeSplitX();
        CheckStateMachineTransitions();
        CheckM2LocaleKeysPresent();
        Console.WriteLine($"Portrait comparison checks passed ({assertions} assertions).");
    }

    private static void CheckAssetCandidatesAndResolution()
    {
        Check(PortraitComparisonRules.NormalizeAssetPath("  /Portraits\\Abigail_Winter.xnb ") == "Portraits/Abigail_Winter",
            "NormalizeAssetPath strips leading slash, normalizes separators, and removes .xnb");
        Check(PortraitComparisonRules.NormalizeAssetPath(null) == string.Empty
            && PortraitComparisonRules.NormalizeAssetPath("   ") == string.Empty,
            "NormalizeAssetPath handles null and whitespace");

        IReadOnlyList<string> winter = PortraitComparisonRules.BuildVanillaAssetCandidates(
            "Portraits\\Abigail_Winter.xnb", "Abigail", "Abigail", "Winter");
        Check(winter.SequenceEqual(["Portraits/Abigail_Winter", "Portraits/Abigail"]),
            "Winter variant builds seasonal candidate before base and deduplicates case-insensitively");

        IReadOnlyList<string> beach = PortraitComparisonRules.BuildVanillaAssetCandidates(
            "Portraits/Abigail_Beach", "Abigail", "Abigail", "Winter");
        Check(beach.SequenceEqual(["Portraits/Abigail_Beach", "Portraits/Abigail_Winter", "Portraits/Abigail"]),
            "Current portrait asset takes priority over LastAppearanceId and base asset");

        IReadOnlyList<string> appearanceOnly = PortraitComparisonRules.BuildVanillaAssetCandidates(
            "Mods/HdPortraits/Abigail_Winter", "Abigail", "Abigail", "Winter");
        Check(appearanceOnly.SequenceEqual(["Portraits/Abigail_Winter", "Portraits/Abigail"]),
            "Non-vanilla current asset falls back to Portraits/<Base>_<Appearance> and base Portraits/<Name>");

        IReadOnlyList<string> hdOnly = PortraitComparisonRules.BuildVanillaAssetCandidates(
            "Mods/HdPortraits/Abigail_Winter", "Abigail");
        Check(hdOnly.SequenceEqual(["Portraits/Abigail"]),
            "Non-Portraits asset without appearance falls back to Portraits/<CharacterName>");

        IReadOnlyList<string> dedup = PortraitComparisonRules.BuildVanillaAssetCandidates(
            "Portraits/abigail", "Abigail", "Portraits/Abigail");
        Check(dedup.Count == 1 && dedup[0] == "Portraits/abigail",
            "Duplicate candidates across arguments collapse case-insensitively");

        IReadOnlyList<string> suffixTrim = PortraitComparisonRules.BuildVanillaAssetCandidates(
            "Portraits/Abigail_Winter", null);
        Check(suffixTrim.SequenceEqual(["Portraits/Abigail_Winter", "Portraits/Abigail"]),
            "Underscore suffix fallback trims when characterName and textureName are omitted");

        IReadOnlyList<string> empty = PortraitComparisonRules.BuildVanillaAssetCandidates(null, null);
        Check(empty.Count == 0, "Null inputs yield empty candidate list");

        HashSet<string> vanillaContent = new(StringComparer.OrdinalIgnoreCase)
        {
            "Portraits/Abigail",
            "Portraits/Abigail_Beach"
        };
        string? Resolver(string candidate) => vanillaContent.FirstOrDefault(v => string.Equals(v, candidate, StringComparison.OrdinalIgnoreCase));

        Check(PortraitComparisonRules.ResolveVanillaAsset("Portraits/Abigail_Winter", "Abigail", Resolver, "Abigail", "Winter") == "Portraits/Abigail",
            "Missing seasonal .xnb safely falls back to base Portraits/Abigail");
        Check(PortraitComparisonRules.ResolveVanillaAsset("Portraits/Abigail_Beach", "Abigail", Resolver) == "Portraits/Abigail_Beach",
            "Existing seasonal/beach .xnb resolves directly");
        Check(PortraitComparisonRules.ResolveVanillaAsset("Mods/CustomPack/Mateo", "Mateo", Resolver, "Mods/CustomPack/Mateo") is null,
            "Custom NPC with no vanilla .xnb resolves to null");
        Check(PortraitComparisonRules.ResolveVanillaAsset(null, null, Resolver) is null,
            "Empty resolution yields null");
    }

    private static void CheckFrameCountingAndClamping()
    {
        Check(PortraitComparisonRules.CountSheetFrames(128, 256) == 8, "128x256 sheet holds 8 64x64 frames");
        Check(PortraitComparisonRules.CountSheetFrames(128, 320) == 10, "128x320 seasonal sheet holds 10 frames");
        Check(PortraitComparisonRules.CountSheetFrames(64, 64) == 1, "64x64 sheet holds 1 frame");
        Check(PortraitComparisonRules.CountSheetFrames(0, 0) == 0
            && PortraitComparisonRules.CountSheetFrames(63, 64) == 0
            && PortraitComparisonRules.CountSheetFrames(64, 63) == 0
            && PortraitComparisonRules.CountSheetFrames(-64, 128) == 0,
            "Sheets smaller than 64x64 report 0 frames");

        for (int frame = 0; frame < 8; frame++)
            Check(PortraitComparisonRules.ClampPortraitFrame(128, 256, frame) == frame, $"Valid frame {frame} preserved");

        Check(PortraitComparisonRules.ClampPortraitFrame(128, 256, -1) == 0
            && PortraitComparisonRules.ClampPortraitFrame(128, 256, int.MinValue) == 0
            && PortraitComparisonRules.ClampPortraitFrame(128, 256, 8) == 0
            && PortraitComparisonRules.ClampPortraitFrame(128, 256, 99) == 0
            && PortraitComparisonRules.ClampPortraitFrame(128, 256, int.MaxValue) == 0,
            "Negative and out-of-range frames clamp safely to frame 0");

        Check(PortraitComparisonRules.ClampPortraitFrame(64, 64, 0) == 0
            && PortraitComparisonRules.ClampPortraitFrame(64, 64, 1) == 0,
            "Single-frame 64x64 sheet clamps frame 1 to 0");

        Check(PortraitComparisonRules.TryGetPortraitSourceRegion(128, 256, 5, out var rect5)
            && rect5 == (64, 128, 64, 64),
            "Frame 5 in 2x4 sheet maps to (64, 128, 64, 64)");
        Check(PortraitComparisonRules.TryGetPortraitSourceRegion(128, 256, 9, out var rectClamped)
            && rectClamped == (0, 0, 64, 64),
            "Out-of-range frame 9 falls back to (0, 0, 64, 64)");
        Check(PortraitComparisonRules.TryGetPortraitSourceRegion(128, 320, 9, out var rect9)
            && rect9 == (64, 256, 64, 64),
            "Frame 9 in 2x5 seasonal sheet maps to (64, 256, 64, 64)");
        Check(!PortraitComparisonRules.TryGetPortraitSourceRegion(63, 64, 0, out _),
            "Sub-64px sheet returns false for TryGetPortraitSourceRegion");
    }

    private static void CheckComputeSplitX()
    {
        Check(PortraitComparisonRules.ComputeSplitX(360, 1200, 1920) == 1336,
            "1200px wide dialogue box splits along vanilla portrait centerline (boxX + boxWidth - 224)");
        Check(PortraitComparisonRules.ComputeSplitX(0, 448, 1280) == 224,
            "Minimum 448px box splits at boxX + 448 - 224");
        Check(PortraitComparisonRules.ComputeSplitX(100, 300, 1280) == 640,
            "Boxes narrower than 448px fall back to viewportWidth / 2");
        Check(PortraitComparisonRules.ComputeSplitX(-500, 500, 800) == 1,
            "Off-screen left split clamps to 1");
        Check(PortraitComparisonRules.ComputeSplitX(700, 500, 800) == 799,
            "Off-screen right split clamps to viewportWidth - 1");
        Check(PortraitComparisonRules.ComputeSplitX(0, 400, 1) == 1,
            "Tiny viewport safely clamps to 1");
    }

    private static void CheckStateMachineTransitions()
    {
        var sm = new PortraitComparisonStateMachine();
        Check(sm.Mode == PortraitCompareMode.Modded && !sm.IsHolding && !sm.HoldTriggeredSplit,
            "State machine starts in Modded with no active hold");

        // Short press (exact 220 ms boundary is NOT > 220 ms) -> toggles Modded -> Vanilla
        sm.BeginPress(100.0);
        Check(sm.IsHolding && !sm.UpdateHold(320.0) && sm.Mode == PortraitCompareMode.Modded,
            "Exact 220 ms does not trigger Split");
        Check(sm.EndPress(out bool changed1) == PortraitCompareMode.Vanilla && changed1 && sm.Mode == PortraitCompareMode.Vanilla && !sm.IsHolding,
            "Short press release switches Modded -> Vanilla and reports changedMode=true");

        // Second short press -> toggles Vanilla -> Modded
        sm.BeginPress(400.0);
        Check(!sm.UpdateHold(500.0), "100 ms hold does not trigger Split");
        Check(sm.EndPress(out bool changed2) == PortraitCompareMode.Modded && changed2 && sm.Mode == PortraitCompareMode.Modded,
            "Second short press switches Vanilla -> Modded");

        // Long hold (> 220 ms) from Modded -> Split, and releasing keeps Split sticky on screen!
        sm.BeginPress(1000.0);
        Check(sm.UpdateHold(1221.0) && sm.HoldTriggeredSplit && sm.Mode == PortraitCompareMode.Split,
            "Hold > 220 ms enters Split mode");
        Check(!sm.UpdateHold(1500.0), "Subsequent hold ticks return false once Split is already active");
        Check(sm.EndPress(out bool changedHoldRelease) == PortraitCompareMode.Split && !changedHoldRelease
            && sm.Mode == PortraitCompareMode.Split && !sm.IsHolding && !sm.HoldTriggeredSplit,
            "Releasing >220 ms hold keeps Split sticky on screen without double-toggling");

        // Short press from sticky Split -> returns to Modded
        sm.BeginPress(1600.0);
        Check(!sm.UpdateHold(1750.0) && sm.Mode == PortraitCompareMode.Split,
            "Short tap while in Split keeps Split until release");
        Check(sm.EndPress(out bool changedExitSplit) == PortraitCompareMode.Modded && changedExitSplit && sm.Mode == PortraitCompareMode.Modded,
            "Short press from sticky Split returns cleanly to Modded");

        // Long hold from Vanilla -> Split (sticky), then short press returns to Modded
        sm.BeginPress(2000.0);
        Check(sm.EndPress() == PortraitCompareMode.Vanilla, "Short press switches back to Vanilla");
        sm.BeginPress(2500.0);
        sm.BeginPress(2600.0); // Duplicate BeginPress while holding must be ignored
        Check(!sm.UpdateHold(2710.0), "Duplicate BeginPress must not reset holdStartMs");
        Check(sm.UpdateHold(2721.0) && sm.Mode == PortraitCompareMode.Split, "Hold > 220 ms from Vanilla enters Split mode");
        Check(sm.EndPress(out bool changedVanillaHold) == PortraitCompareMode.Split && !changedVanillaHold && sm.Mode == PortraitCompareMode.Split,
            "Releasing hold from Vanilla keeps sticky Split active");
        sm.BeginPress(2800.0);
        Check(sm.EndPress() == PortraitCompareMode.Modded, "Short press after sticky Split returns to Modded");

        // CancelHold clears hold flags while preserving sticky Split if already entered
        sm.BeginPress(3000.0);
        Check(sm.UpdateHold(3300.0) && sm.Mode == PortraitCompareMode.Split, "Hold > 220 ms enters Split before CancelHold");
        sm.CancelHold();
        Check(!sm.IsHolding && !sm.HoldTriggeredSplit && sm.Mode == PortraitCompareMode.Split,
            "CancelHold clears hold flags while preserving sticky Split");
        sm.BeginPress(3400.0);
        sm.EndPress();
        Check(sm.Mode == PortraitCompareMode.Modded, "Short tap exits preserved Split back to Modded");

        // SyncDialogueContext resets only when dialogue key changes
        sm.SyncDialogueContext("Abigail");
        Check(sm.Mode == PortraitCompareMode.Modded, "Switching to Abigail resets mode to Modded");
        sm.BeginPress(4000.0);
        Check(sm.EndPress() == PortraitCompareMode.Vanilla, "Short press toggles Abigail to Vanilla");
        sm.SyncDialogueContext("Abigail");
        Check(sm.Mode == PortraitCompareMode.Vanilla, "Same dialogue key preserves current mode");
        sm.SyncDialogueContext("Lewis");
        Check(sm.Mode == PortraitCompareMode.Modded && !sm.IsHolding, "Switching speaker resets to Modded");

        sm.BeginPress(5000.0);
        Check(sm.UpdateHold(5300.0) && sm.Mode == PortraitCompareMode.Split, "Hold > 220 ms enters Split before closing dialogue");
        sm.SyncDialogueContext(null);
        Check(sm.Mode == PortraitCompareMode.Modded && !sm.IsHolding && !sm.HoldTriggeredSplit,
            "Closing dialogue resets state machine to Modded");

        sm.BeginPress(6000.0);
        sm.EndPress();
        sm.Reset();
        Check(sm.Mode == PortraitCompareMode.Modded && !sm.IsHolding && !sm.HoldTriggeredSplit,
            "Reset clears all state");
    }

    private static void CheckM2LocaleKeysPresent()
    {
        string? i18nDir = null;
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, "i18n");
            if (File.Exists(Path.Combine(candidate, "default.json")))
            {
                i18nDir = candidate;
                break;
            }
        }
        if (i18nDir is null)
            return;

        string[] localeFiles = Directory.GetFiles(i18nDir, "*.json");
        Check(localeFiles.Length == 12, "All 12 official locale files present");

        Dictionary<string, string> defaultDoc = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(Path.Combine(i18nDir, "default.json")))!;
        Check(defaultDoc.Count == 643, $"default.json contains expected 643 keys (actual {defaultDoc.Count})");

        string[] requiredKeys =
        [
            "replay.portrait-vanilla",
            "replay.portrait-modded",
            "replay.portrait-split",
            "replay.portrait-no-vanilla",
            "config.portrait-key.name",
            "config.portrait-key.tooltip",
            "config.auto-dialogue.name",
            "config.auto-dialogue.tooltip"
        ];
        string[] removedKeys =
        [
            "replay.cinema-enter",
            "replay.cinema-exit",
            "config.cinema-key.name",
            "config.cinema-key.tooltip",
            "config.cinema-default.name",
            "config.cinema-default.tooltip",
            "config.cinema-pace.name",
            "config.cinema-pace.tooltip",
            "scenic.weather-sync",
            "scenic.weather-preview",
            "scenic.underground-calm"
        ];

        foreach (string localeFile in localeFiles)
        {
            Dictionary<string, string> doc = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(
                File.ReadAllText(localeFile))!;
            string fileName = Path.GetFileName(localeFile);
            Check(doc.Count == 643, $"{fileName} contains 643 keys (actual {doc.Count})");
            Check(doc.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(defaultDoc.Keys),
                $"{fileName} keys match default.json");
            foreach (string key in requiredKeys)
                Check(doc.TryGetValue(key, out string? val) && !string.IsNullOrWhiteSpace(val),
                    $"{fileName} contains non-empty key {key}");
            foreach (string removed in removedKeys)
                Check(!doc.ContainsKey(removed), $"{fileName} does not contain removed key {removed}");
        }
    }

    private static void Check(bool condition, string message, [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
    {
        assertions++;
        if (!condition)
            throw new Exception($"Portrait comparison check failed (line {line}): {message}");
    }
}
