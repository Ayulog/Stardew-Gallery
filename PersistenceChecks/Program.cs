using StardewGallery;

string tempRoot = Path.Combine(Path.GetTempPath(), "sg-persist-checks-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(tempRoot);
try
{
    EventPhotoChecks.Run();
    EventNameChecks.Run(tempRoot);

    Check(new SaveProfileKey(0, 0) == new SaveProfileKey(0, 0), "same farm/player equal");
    Check(new SaveProfileKey(1, 2) != new SaveProfileKey(1, 3), "same farm diff player differ");
    Check(new SaveProfileKey(1, 2) != new SaveProfileKey(2, 2), "diff farm same player differ");
    Check(new SaveProfileKey(ulong.MaxValue, long.MaxValue).FarmUniqueId == ulong.MaxValue,
        "farm profile keeps full unsigned identifier");

    Check(ReplayBackupRetention.Retain([]).Count == 0, "stale 0 keep 0");
    Check(ReplayBackupRetention.Retain(["A"]).Count == 1, "stale 1 keep 1");
    Check(ReplayBackupRetention.Retain(["A", "B"]).Count == 2, "stale 2 keep 2");
    Check(ReplayBackupRetention.Retain(["D", "C", "B", "A"]).SequenceEqual(["D", "C"]), "stale 4 keep newest 2");
    Check(ReplayBackupRetention.Discard(["D", "C", "B", "A"]).SequenceEqual(["B", "A"]), "discard old");
    Console.WriteLine("Stardew Gallery persistence checks passed.");
}
finally
{
    Directory.Delete(tempRoot, recursive: true);
}

static void Check(bool condition, string message)
{
    if (!condition)
        throw new Exception($"Check failed: {message}");
}
