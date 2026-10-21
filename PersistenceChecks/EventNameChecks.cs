using StardewGallery;

internal static class EventNameChecks
{
    internal static void Run(string root)
    {
        string directory = Path.Combine(root, "names-" + Guid.NewGuid().ToString("N"));
        List<string> warnings = [];
        var store = new EventNameStore(directory, warnings.Add);
        var town = new EventIdentity("Data/Events/Town", "same-id");
        var beach = new EventIdentity("Data/Events/Beach", "same-id");
        Check(store.Set(town, "中文 自定义名称") && store.Set(beach, "Another name"), "write independent same-ID names");
        var reloaded = new EventNameStore(directory, warnings.Add);
        Check(reloaded.Get(new("data/events/town", "same-id")) == "中文 自定义名称" && reloaded.Get(beach) == "Another name", "names survive reload and asset case normalization");
        Check(reloaded.Set(town, "") && reloaded.Get(town) is null && reloaded.Get(beach) == "Another name", "restore only one title");
        string path = Path.Combine(directory, "event-names.json");
        string broken = "{\"Version\":999,\"Names\":{}}";
        File.WriteAllText(path, broken);
        var future = new EventNameStore(directory, warnings.Add);
        Check(!future.Set(town, "overwrite") && File.ReadAllText(path) == broken, "future metadata never overwritten");
        string blocked = Path.Combine(root, "names-file-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(blocked, "occupied");
        var failed = new EventNameStore(blocked, warnings.Add);
        Check(!failed.Set(town, "lost") && failed.Get(town) is null, "failed write does not commit memory state");
        Check(EventNameStore.Clean("hello\nworld\u2028next") == "hello world next", "names stay single line");

        // 2.10.0 merges each Trailer_Big copy of a trailer event into the Trailer entry.
        string merged = Path.Combine(root, "names-" + Guid.NewGuid().ToString("N"));
        var current = new EventIdentity("Data/Events/Trailer", "35");
        var former = new EventIdentity("Data/Events/Trailer_Big", "35");
        var names = new EventNameStore(merged, warnings.Add);
        Check(names.Set(former, "Penny's book") && names.Adopt(current, former), "former name adopted");
        var adopted = new EventNameStore(merged, warnings.Add);
        Check(adopted.Get(current) == "Penny's book" && adopted.Get(former) is null, "name moves to the current identity");
        Check(adopted.Set(current, "") && !adopted.Adopt(current, former) && adopted.Get(current) is null, "a cleared name does not return");
        Check(adopted.Set(former, "Old") && adopted.Set(current, "Mine") && !adopted.Adopt(current, former)
            && adopted.Get(current) == "Mine" && adopted.Get(former) == "Old", "an existing current name wins and the former one is kept");
        Check(adopted.Get(new("Data/Events/Trailer", "35 ")) == "Mine", "a trailing-space game ID reads the name saved under the trimmed ID");

        // Backups are capped; a damaged file falls back to the newest one and is archived, not overwritten, by the next save.
        string recovering = Path.Combine(root, "names-" + Guid.NewGuid().ToString("N"));
        var writer = new EventNameStore(recovering, warnings.Add);
        int saves = EventNameStore.MaxBackups + 5;
        for (int i = 0; i < saves; i++) Check(writer.Set(town, "name " + i), "repeated saves succeed");
        string archive = Path.Combine(recovering, "archive");
        Check(Directory.EnumerateFiles(archive, "event-names-*.json").Count() == EventNameStore.MaxBackups, "backups are capped");
        Check(!Directory.EnumerateFiles(recovering, "*.pending").Any(), "no pending files are left behind");
        string damaged = "{ \"Version\": 1, \"Names\": { broken";
        File.WriteAllText(Path.Combine(recovering, "event-names.json"), damaged);
        warnings.Clear();
        var restored = new EventNameStore(recovering, warnings.Add);
        Check(restored.Get(town) == "name " + (saves - 2) && warnings.Single().Contains("restored the backup"), "a damaged file falls back to the newest backup");
        Check(restored.Set(beach, "after restore") && new EventNameStore(recovering, warnings.Add).Get(town) == "name " + (saves - 2),
            "the restored names can be saved again");
        Check(Directory.EnumerateFiles(archive, "event-names-*.json").Any(path => File.ReadAllText(path) == damaged)
            && Directory.EnumerateFiles(archive, "event-names-*.json").Count() == EventNameStore.MaxBackups, "the damaged file is kept as the newest backup");
        string unreadable = Path.Combine(root, "names-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(unreadable);
        File.WriteAllText(Path.Combine(unreadable, "event-names.json"), damaged);
        var stuck = new EventNameStore(unreadable, warnings.Add);
        Check(stuck.Get(town) is null && !stuck.Set(town, "overwrite") && File.ReadAllText(Path.Combine(unreadable, "event-names.json")) == damaged,
            "without a usable backup a damaged file stays read-only and untouched");
        Console.WriteLine("Event name persistence checks passed.");
    }
    private static void Check(bool value, string label) { if (!value) throw new InvalidOperationException(label); }
}
