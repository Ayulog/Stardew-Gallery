using System.Text.Json;

namespace StardewGallery;

/// <summary>
/// Cross-save local persistence for favorited story identities in <c>user-data/favorite-events.json</c>.
/// Matches <see cref="EventNameStore"/> durability invariants: flushed temporary write + atomic replace,
/// automatic recovery from the newest valid backup (capped at 5), and read-only protection on newer schema versions.
/// </summary>
internal sealed class FavoriteEventStore
{
    internal const int MaxBackups = 5;

    private sealed record FavoriteRecord(string AssetName, string EventId);
    private sealed record Document(int Version, List<FavoriteRecord> Favorites);

    private readonly string directory;
    private readonly Action<string> warn;
    private readonly IStorageFileSystem fs;
    private Dictionary<EventIdentity, EventIdentity>? favorites;
    private bool readOnly;

    internal FavoriteEventStore(string directory, Action<string> warn)
        : this(directory, warn, PhysicalStorageFileSystem.Instance) { }

    internal FavoriteEventStore(string directory, Action<string> warn, IStorageFileSystem fileSystem)
    {
        this.directory = directory;
        this.warn = warn;
        fs = fileSystem;
    }

    private string FilePath => Path.Combine(directory, "favorite-events.json");
    private string ArchivePath => Path.Combine(directory, "archive");

    internal int Revision { get; private set; }
    internal bool IsReadOnly { get { Load(); return readOnly; } }

    internal bool IsFavorite(EventIdentity identity)
    {
        Load();
        return !WeddingMemento.IsWeddingIdentity(identity) && favorites!.ContainsKey(Canonical(identity));
    }

    internal IReadOnlyList<EventIdentity> All()
    {
        Load();
        return favorites!.Values
            .OrderBy(item => item.AssetName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.StorageEventId, StringComparer.Ordinal)
            .ToArray();
    }

    internal bool Toggle(EventIdentity identity)
    {
        Load();
        return Set(identity, !favorites!.ContainsKey(Canonical(identity)));
    }

    internal bool Set(EventIdentity identity, bool favorite)
    {
        Load();
        if (readOnly || WeddingMemento.IsWeddingIdentity(identity) || string.IsNullOrWhiteSpace(identity.AssetName) || identity.StorageEventId.Length == 0)
            return false;

        EventIdentity normalized = Canonical(identity);
        bool currentlySet = favorites!.ContainsKey(normalized);
        if (currentlySet == favorite)
            return true;

        var next = new Dictionary<EventIdentity, EventIdentity>(favorites);
        if (favorite)
            next[normalized] = normalized;
        else
            next.Remove(normalized);
        return Write(next);
    }

    /// <summary>Carry a retired identity's favorite mark onto its current merged identity.</summary>
    internal bool Adopt(EventIdentity current, EventIdentity former)
    {
        Load();
        EventIdentity normCurrent = Canonical(current);
        EventIdentity normFormer = Canonical(former);
        if (readOnly || normCurrent == normFormer || !favorites!.ContainsKey(normFormer))
            return false;

        var next = new Dictionary<EventIdentity, EventIdentity>(favorites);
        next.Remove(normFormer);
        next[normCurrent] = normCurrent;
        return Write(next);
    }

    private static EventIdentity Canonical(EventIdentity identity)
        => new(identity.AssetName, identity.StorageEventId);

    private bool Write(Dictionary<EventIdentity, EventIdentity> next)
    {
        string pending = Path.Combine(directory, "favorite-events-" + Guid.NewGuid().ToString("N") + ".pending");
        try
        {
            fs.CreateDirectory(directory);
            FavoriteRecord[] records = next.Values
                .OrderBy(item => item.AssetName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.StorageEventId, StringComparer.Ordinal)
                .Select(item => new FavoriteRecord(item.AssetName, item.StorageEventId))
                .ToArray();
            byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new Document(1, records.ToList()));
            fs.WriteNewFlushed(pending, payload);

            if (fs.FileExists(FilePath))
            {
                fs.CreateDirectory(ArchivePath);
                string backup = Path.Combine(ArchivePath, $"favorite-events-{DateTime.UtcNow:yyyyMMddHHmmssfffffff}-{Guid.NewGuid():N}.json");
                fs.ReplaceFile(pending, FilePath, backup);
                TrimBackups();
            }
            else
            {
                fs.MoveFile(pending, FilePath);
            }

            favorites = next;
            Revision++;
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            try { if (fs.FileExists(pending)) fs.DeleteFile(pending); }
            catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException) { }
            warn("Favorite events could not be saved: " + error.Message);
            return false;
        }
    }

    private void TrimBackups()
    {
        try
        {
            foreach (string old in Backups().Skip(MaxBackups))
                fs.DeleteFile(old);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            warn("Old favorite event backups could not be removed: " + error.Message);
        }
    }

    private IEnumerable<string> Backups()
        => fs.EnumerateFiles(ArchivePath, "favorite-events-*.json")
            .OrderByDescending(path => fs.GetLastWriteTimeUtc(path))
            .ThenByDescending(Path.GetFileName, StringComparer.Ordinal);

    private void Load()
    {
        if (favorites is not null)
            return;
        favorites = [];
        if (!fs.FileExists(FilePath))
            return;
        try
        {
            favorites = Read(FilePath);
        }
        catch (NotSupportedException error)
        {
            readOnly = true;
            warn("Favorite events are read-only: " + error.Message);
        }
        catch (Exception error) when (error is JsonException or InvalidDataException)
        {
            foreach (string backup in Backups())
            {
                try
                {
                    favorites = Read(backup);
                    warn($"Favorite events file was damaged ({error.Message}); restored the backup {Path.GetFileName(backup)}.");
                    return;
                }
                catch (Exception backupError) when (backupError is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or NotSupportedException) { }
            }
            readOnly = true;
            warn("Favorite events are read-only: " + error.Message);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            readOnly = true;
            warn("Favorite events are read-only: " + error.Message);
        }
    }

    private Dictionary<EventIdentity, EventIdentity> Read(string path)
    {
        if (fs.GetFileLength(path) > 4 * 1024 * 1024)
            throw new InvalidDataException("Favorite events document is too large.");
        Document doc = JsonSerializer.Deserialize<Document>(fs.ReadAllText(path))
            ?? throw new InvalidDataException("Empty favorite events document.");
        if (doc.Version != 1)
            throw new NotSupportedException($"Unsupported favorite events document version {doc.Version}; left untouched.");
        if (doc.Favorites is null)
            throw new InvalidDataException("Favorite events document has no Favorites list.");

        Dictionary<EventIdentity, EventIdentity> result = [];
        foreach (FavoriteRecord record in doc.Favorites)
        {
            if (record is null || string.IsNullOrWhiteSpace(record.AssetName) || string.IsNullOrWhiteSpace(record.EventId))
                throw new InvalidDataException("Favorite events document contains an invalid identity.");
            EventIdentity normalized = new(record.AssetName, record.EventId.Trim());
            if (WeddingMemento.IsWeddingIdentity(normalized))
                continue;
            result[normalized] = normalized;
        }
        return result;
    }
}
