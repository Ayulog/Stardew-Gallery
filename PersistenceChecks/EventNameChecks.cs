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
        Console.WriteLine("Event name persistence checks passed.");
    }
    private static void Check(bool value, string label) { if (!value) throw new InvalidOperationException(label); }
}
