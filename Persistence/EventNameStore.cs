using System.Globalization;
using System.Text.Json;

namespace StardewGallery;

internal sealed class EventNameStore(string directory, Action<string> warn)
{
    /// <summary>Earlier versions of the names file kept in <c>archive/</c>; older ones are removed after each save.</summary>
    internal const int MaxBackups = 5;
    private sealed record Document(int Version, Dictionary<string, string> Names);
    private Dictionary<string, string>? names;
    private bool readOnly;
    private string FilePath => Path.Combine(directory, "event-names.json");
    private string ArchivePath => Path.Combine(directory, "archive");
    internal int Revision { get; private set; }
    internal string? Get(EventIdentity identity) { Load(); return names!.GetValueOrDefault(Key(identity)); }
    private static string Key(EventIdentity identity) => identity.AssetName.ToUpperInvariant() + "\u001f" + identity.StorageEventId;
    internal static string Clean(string text)
    {
        text = GallerySearchInput.CleanText(text).Trim();
        int[] elements = StringInfo.ParseCombiningCharacters(text);
        return elements.Length <= 100 ? text : text[..elements[100]];
    }
    internal bool Set(EventIdentity identity, string text)
    {
        Load();
        if (readOnly) return false;
        var next = new Dictionary<string, string>(names!, StringComparer.Ordinal);
        text = Clean(text);
        if (text.Length == 0) next.Remove(Key(identity)); else next[Key(identity)] = text;
        return Write(next);
    }
    /// <summary>Move a retired entry's name to its current identity, unless the current identity already has one.</summary>
    internal bool Adopt(EventIdentity current, EventIdentity former)
    {
        Load();
        if (readOnly || names!.ContainsKey(Key(current)) || !names.TryGetValue(Key(former), out string? name)) return false;
        var next = new Dictionary<string, string>(names, StringComparer.Ordinal) { [Key(current)] = name };
        next.Remove(Key(former));
        return Write(next);
    }
    private bool Write(Dictionary<string, string> next)
    {
        string pending = Path.Combine(directory, "event-names-" + Guid.NewGuid().ToString("N") + ".pending");
        try
        {
            Directory.CreateDirectory(directory);
            // Flush to disk before the replace, so a crash can't leave an empty file in place of the names.
            using (var file = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(file, new Document(1, next));
                file.Flush(flushToDisk: true);
            }
            if (File.Exists(FilePath))
            {
                Directory.CreateDirectory(ArchivePath);
                File.Replace(pending, FilePath, Path.Combine(ArchivePath, $"event-names-{DateTime.UtcNow:yyyyMMddHHmmssfffffff}-{Guid.NewGuid():N}.json"));
                TrimBackups();
            }
            else File.Move(pending, FilePath);
            names = next; Revision++; return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            try { if (File.Exists(pending)) File.Delete(pending); } catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException) { }
            warn("Event names could not be saved: " + error.Message); return false;
        }
    }
    private void TrimBackups()
    {
        try
        {
            foreach (FileInfo old in Backups().Skip(MaxBackups)) old.Delete();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { warn("Old event name backups could not be removed: " + error.Message); }
    }
    /// <summary>Backups, newest first. Older backups have random names, so order by when they were written.</summary>
    private IEnumerable<FileInfo> Backups() => Directory.Exists(ArchivePath)
        ? new DirectoryInfo(ArchivePath).EnumerateFiles("event-names-*.json").OrderByDescending(file => file.LastWriteTimeUtc).ThenByDescending(file => file.Name, StringComparer.Ordinal)
        : [];
    private void Load()
    {
        if (names is not null) return;
        names = new(StringComparer.Ordinal);
        if (!File.Exists(FilePath)) return;
        try { names = Read(FilePath); }
        catch (NotSupportedException error) { readOnly = true; warn("Event names are read-only: " + error.Message); }
        catch (Exception error) when (error is JsonException or InvalidDataException)
        {
            // Restore the newest readable backup; the damaged file is archived by the next save, never overwritten unseen.
            foreach (FileInfo backup in Backups())
            {
                try
                {
                    names = Read(backup.FullName);
                    warn($"Event names file was damaged ({error.Message}); restored the backup {backup.Name}.");
                    return;
                }
                catch (Exception backupError) when (backupError is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or NotSupportedException) { }
            }
            readOnly = true; warn("Event names are read-only: " + error.Message);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { readOnly = true; warn("Event names are read-only: " + error.Message); }
    }
    private static Dictionary<string, string> Read(string path)
    {
        Document doc = JsonSerializer.Deserialize<Document>(File.ReadAllText(path)) ?? throw new InvalidDataException("Empty names document.");
        if (doc.Version != 1) throw new NotSupportedException($"Unsupported names document version {doc.Version}; left untouched.");
        if (doc.Names is null || doc.Names.Any(pair => pair.Key is null || pair.Value is null)) throw new InvalidDataException("Names document has no valid entries.");
        return new(doc.Names, StringComparer.Ordinal);
    }
}
