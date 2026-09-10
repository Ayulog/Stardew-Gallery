using StardewGallery.Appearance;

internal static class AppearanceChecks
{
    internal static void Run()
    {
        int checks = 0;
        void Check(bool ok, string name) { checks++; if (!ok) throw new InvalidOperationException(name); }
        var large = new PortraitFrame(null, Width: 1000, Height: 1000);
        Check(large.TryRegion(2000, 7000, out var first) && first == (0, 0, 1000, 1000), "Mud portrait frame");
        Check(!large.TryRegion(128, 256, out _), "Reject stale high-resolution metadata");
        Check(!new PortraitFrame(null, int.MaxValue, 1, int.MaxValue, 10).TryRegion(2000, 7000, out _), "Reject overflow");
        Check(new PortraitFrame(null, 1000, 2000, 1000, 1000).TryRegion(2000, 7000, out var crop) && crop == (1000, 2000, 1000, 1000), "Explicit portrait region");
        Check(!new PortraitFrame(null, Width: 0).TryRegion(128, 128, out _), "Reject empty frames");
        Check(!new PortraitFrame(null, Alpha: float.NaN).TryRegion(128, 128, out _), "Reject non-finite alpha");
        var scaled = PortraitureRegion.Create(8, 64, 64, null);
        Check(scaled?.Width == 512 && scaled.Height == 512, "Portraiture uses declared scale");
        var forced = PortraitureRegion.Create(8, 64, 64, (512, 128, 256, 400));
        Check(forced?.TryRegion(1024, 1024, out var forcedRegion) == true && forcedRegion == (512, 128, 256, 400), "Forced source overrides scale");
        Check(PortraitureRegion.Create(float.NaN, 64, 64, null) is null, "Reject Portraiture NaN scale");
        Check(PortraitureRegion.Create(float.MaxValue, 64, 64, null) is null, "Reject Portraiture scale overflow");
        Check(PortraitureRegion.Create(0, 64, 64, null) is null, "Reject zero Portraiture scale");
        Check(PortraitureRegion.Create(8, 64, 64, (-1, 0, 64, 64)) is null, "Reject invalid forced coordinates");
        var fit = AppearanceGeometry.Fit(10, 20, 96, 96, 1000, 2000);
        Check(fit == (34, 20, 48, 96), "Fit tall portrait without stretching");
        Dictionary<string, DialoguePortraitEntry> entries = new(StringComparer.Ordinal)
        {
            ["default"] = new(null, false, new(null)),
            ["Haley"] = new(null, false, large),
            ["Leah"] = new("Haley", false, null),
            ["Leah_Summer"] = new(null, false, new("Portraits/Summer", Width: 512, Height: 512)),
            ["LoopA"] = new("LoopB", false, null), ["LoopB"] = new("LoopA", false, null)
        };
        Check(DialoguePortraitSelection.Resolve(entries, ["Leah"]) == large, "CopyFrom shared portrait");
        Check(DialoguePortraitSelection.Resolve(entries, ["Leah_Summer", "Leah"])?.Width == 512, "Current appearance precedes base");
        Check(DialoguePortraitSelection.Resolve(entries, ["Missing"])?.Width == 64, "Missing NPC uses default");
        Check(DialoguePortraitSelection.Resolve(entries, ["LoopA"]) is null, "CopyFrom cycle terminates");
        entries["Leah"] = new("Haley", false, new(null, Width: 128, Height: 256));
        Check(DialoguePortraitSelection.Resolve(entries, ["Leah"])?.Width == 128, "Portrait override is whole-component");
        entries["Leah_Summer"] = new(null, true, large);
        Check(DialoguePortraitSelection.Resolve(entries, ["Leah_Summer", "Leah"])?.Width == 128, "Disabled entry falls through");
        entries["Leah_Summer"] = new(null, false, large, "MOD_QUERY arg");
        Check(DialoguePortraitSelection.Resolve(entries, ["Leah_Summer", "Leah"]) is null, "Unknown condition does not select a portrait");
        foreach (var frame in new[] { (16, 32), (16, 24), (16, 16) })
        foreach (var size in new[] { (380, 270), (190, 135) })
        foreach (var profile in new[] { new DetailedSprite(), new DetailedSprite(24, 78), new DetailedSprite(36, 106) })
        {
            var p = AppearanceGeometry.SpritePlacement(20, 40, size.Item1, size.Item2, frame.Item1, frame.Item2, profile);
            float left = p.X - profile.OriginX * p.Width / (4 * frame.Item1);
            float top = p.Y - (frame.Item2 <= 16 ? 96 : profile.OriginY) * p.Height / (4 * frame.Item2);
            Check(left >= 20 && left + p.Width <= 20 + size.Item1 && top >= 40 && top + p.Height <= 40 + size.Item2, "Scale Up final sprite stays inside photo");
            Check(Math.Abs(left + p.Width / 2 - (20 + size.Item1 / 2f)) < .01f, "Scale Up sprite remains centered");
        }
        var normal = AppearanceGeometry.SpritePlacement(240, 120, 380, 270, 16, 32, null);
        Check(normal.Scale == 4 && normal.X == 398 && normal.Y == 197, "Preserve native panel placement");
        Console.WriteLine($"Appearance checks passed: {checks}");
    }
}
