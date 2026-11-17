using System.Buffers.Binary;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StardewGallery;

internal sealed record EventPhoto(string Id, DateTimeOffset CapturedAt, int Width, int Height);
internal sealed record EventPhotoSet(IReadOnlyList<EventPhoto> Photos, string? CoverId, bool ReadOnly = false, WeddingDateRecord? WeddingDate = null);

internal sealed class EventPhotoStore
{
    /// <summary>Earlier catalogs kept per event in <c>archive/</c>; removed photos there are never deleted.</summary>
    internal const int MaxIndexBackups = 5;

    private readonly string rootPath;
    private readonly string profileFolderName;
    private readonly string directory;
    private readonly Action<string> warn;
    private readonly IStorageFileSystem fs;
    private readonly Dictionary<EventIdentity, EventPhotoSet> sets = [];
    private readonly Dictionary<EventIdentity, string> directories = [];
    private List<EventIdentity>? scannedWeddings;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    internal EventPhotoStore(string root, SaveProfileKey profile, Action<string> warn)
        : this(root, profile, warn, PhysicalStorageFileSystem.Instance) { }

    internal EventPhotoStore(string root, SaveProfileKey profile, Action<string> warn, IStorageFileSystem fileSystem)
    {
        rootPath = Path.GetFullPath(root);
        profileFolderName = profile.FarmUniqueId.ToString("X16", CultureInfo.InvariantCulture) + "-" + profile.PlayerUniqueId.ToString("X16", CultureInfo.InvariantCulture);
        directory = Path.Combine(rootPath, profileFolderName);
        this.warn = warn;
        fs = fileSystem;
    }

    internal string RootDirectory => rootPath;
    internal string ProfileDirectory => directory;

    internal string RelativeSubdirectory(EventIdentity identity)
        => "event-photos/" + profileFolderName + "/" + EventFolderHash(identity);

    internal EventPhotoSet Read(EventIdentity identity)
    {
        if (sets.TryGetValue(identity, out EventPhotoSet? value))
            return value;
        string path = Path.Combine(EventDirectory(identity), "index.json");
        if (!fs.FileExists(path))
            return sets[identity] = new([], null);
        try { return sets[identity] = ReadDocument(path, identity); }
        catch (NotSupportedException error)
        {
            warn(error.Message);
            return sets[identity] = new([], null, ReadOnly: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            warn($"Photo index unreadable: {error.Message}");
            string archive = Path.Combine(EventDirectory(identity), "archive");
            if (fs.DirectoryExists(archive))
                foreach (string backup in fs.EnumerateFiles(archive, "index-*.json").OrderByDescending(Path.GetFileName))
                {
                    try { return sets[identity] = ReadDocument(backup, identity); }
                    catch (Exception backupError) when (backupError is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or NotSupportedException) { }
                }
            // Preserve an unreadable catalog until the player repairs it; never overwrite unknown data.
            return sets[identity] = new([], null, ReadOnly: true);
        }
    }

    internal IReadOnlyList<(EventIdentity Identity, EventPhotoSet Photos)> ReadWeddingMemories()
    {
        if (scannedWeddings is null)
        {
            HashSet<EventIdentity> found = [];
            foreach ((EventIdentity key, EventPhotoSet cached) in sets)
            {
                if (WeddingMemento.IsWeddingIdentity(key) && (cached.Photos.Count > 0 || cached.WeddingDate is not null))
                    found.Add(key);
            }
            if (fs.DirectoryExists(directory))
            {
                foreach (string subDir in fs.EnumerateDirectories(directory))
                {
                    if (TryDiscoverWeddingIdentity(subDir, out EventIdentity weddingId))
                    {
                        EventPhotoSet loaded = Read(weddingId);
                        if (loaded.Photos.Count > 0 || loaded.WeddingDate is not null)
                            found.Add(weddingId);
                    }
                }
            }
            scannedWeddings = found
                .OrderBy(id => id.StorageEventId, StringComparer.Ordinal)
                .ToList();
        }

        return scannedWeddings
            .Select(id => (id, Read(id)))
            .Where(pair => pair.Item2.Photos.Count > 0 || pair.Item2.WeddingDate is not null)
            .ToArray();
    }

    internal EventPhoto Add(EventIdentity identity, byte[] image, byte[] thumbnail)
        => Add(identity, image, thumbnail, weddingDate: null);

    internal EventPhoto Add(EventIdentity identity, byte[] image, byte[] thumbnail, WeddingDateRecord? weddingDate)
    {
        EventPhotoSet old = Writable(identity);
        WeddingDateRecord? effectiveWedding = weddingDate?.Normalized() ?? old.WeddingDate;
        if (effectiveWedding is { IsValid: false })
            throw new ArgumentException("Wedding date metadata is invalid.", nameof(weddingDate));

        var dimensions = PngSize(image);
        PngSize(thumbnail);
        string id = Guid.NewGuid().ToString("N");
        EventPhoto photo = new(id, DateTimeOffset.UtcNow, dimensions.Width, dimensions.Height);
        fs.CreateDirectory(EventDirectory(identity));
        string imagePath = ImagePath(identity, id), thumbnailPath = ImagePath(identity, id, thumbnail: true);
        try
        {
            fs.WriteNewFlushed(imagePath, image);
            fs.WriteNewFlushed(thumbnailPath, thumbnail);
            Commit(identity, new(old.Photos.Append(photo).ToArray(), old.Photos.Count == 0 ? id : old.CoverId, WeddingDate: effectiveWedding));
            if (WeddingMemento.IsWeddingIdentity(identity) && scannedWeddings is not null && !scannedWeddings.Contains(identity))
                scannedWeddings.Add(identity);
        }
        catch
        {
            // An uncommitted photo has no catalog entry; don't leave its new files behind.
            foreach (string path in new[] { imagePath, thumbnailPath })
                try { if (fs.FileExists(path)) fs.DeleteFile(path); } catch { }
            throw;
        }
        return photo;
    }

    internal void SetCover(EventIdentity identity, string? id)
    {
        EventPhotoSet old = Writable(identity);
        if (id is not null && !old.Photos.Any(photo => photo.Id == id))
            throw new ArgumentException("Photo isn't in this event.", nameof(id));
        if (id is not null)
        {
            using Stream image = fs.OpenRead(ImagePath(identity, id));
            ValidatePng(image);
        }
        Commit(identity, old with { CoverId = id });
    }

    internal void Archive(EventIdentity identity, string id)
    {
        EventPhotoSet old = Writable(identity);
        if (!old.Photos.Any(photo => photo.Id == id))
            throw new ArgumentException("Photo isn't in this event.", nameof(id));
        // Commit the new catalog first. A failed move can only leave an unreferenced image, never a broken selected cover.
        Commit(identity, old with
        {
            Photos = old.Photos.Where(photo => photo.Id != id).ToArray(),
            CoverId = old.CoverId == id ? null : old.CoverId
        });
        string archive = Path.Combine(EventDirectory(identity), "archive", id + "-" + Guid.NewGuid().ToString("N"));
        fs.CreateDirectory(archive);
        foreach (bool thumbnail in new[] { false, true })
        {
            string image = ImagePath(identity, id, thumbnail);
            if (fs.FileExists(image))
                fs.MoveFile(image, Path.Combine(archive, Path.GetFileName(image)));
        }
    }

    /// <summary>
    /// Move a retired entry's photos into its current identity. Images are copied before the merged catalog is
    /// committed, and only then is the old catalog retired; photo IDs are unique, so an interrupted move resumes
    /// without duplicates. The old images stay on disk.
    /// </summary>
    internal bool Adopt(EventIdentity current, EventIdentity former)
    {
        string formerIndex = Path.Combine(EventDirectory(former), "index.json");
        if (current == former || !fs.FileExists(formerIndex))
            return false;
        EventPhotoSet source = Read(former), target = Read(current);
        if (source.ReadOnly || target.ReadOnly)
            return false;
        EventPhoto[] added = source.Photos.Where(photo => target.Photos.All(existing => existing.Id != photo.Id)).ToArray();
        if (added.Length > 0)
        {
            List<string> copied = [];
            try
            {
                fs.CreateDirectory(EventDirectory(current));
                foreach (EventPhoto photo in added)
                foreach (bool thumbnail in new[] { false, true })
                {
                    string from = ImagePath(former, photo.Id, thumbnail), to = ImagePath(current, photo.Id, thumbnail);
                    if (!fs.FileExists(from) || fs.FileExists(to))
                        continue;
                    fs.CopyFile(from, to);
                    copied.Add(to);
                }
                Commit(current, new(target.Photos.Concat(added).ToArray(), target.CoverId ?? source.CoverId, WeddingDate: target.WeddingDate ?? source.WeddingDate));
            }
            catch
            {
                foreach (string path in copied)
                    try { fs.DeleteFile(path); } catch { }
                throw;
            }
        }
        // Retire the old catalog so photos archived later don't return from it.
        string archive = Path.Combine(EventDirectory(former), "archive");
        fs.CreateDirectory(archive);
        fs.MoveFile(formerIndex, Path.Combine(archive, $"index-adopted-{DateTime.UtcNow:yyyyMMddHHmmssfffffff}-{Guid.NewGuid():N}.json"));
        sets.Remove(former);
        return true;
    }

    internal string ImagePath(EventIdentity identity, string id, bool thumbnail = false)
    {
        if (!Guid.TryParseExact(id, "N", out _))
            throw new ArgumentException("Invalid photo ID.", nameof(id));
        return Path.Combine(EventDirectory(identity), id + (thumbnail ? ".thumb.png" : ".png"));
    }

    // Covers are looked up while drawing, so hash each event's folder name once.
    internal string EventDirectory(EventIdentity identity)
    {
        if (!directories.TryGetValue(identity, out string? path))
            directories[identity] = path = Path.Combine(directory, EventFolderHash(identity));
        return path;
    }

    private static string EventFolderHash(EventIdentity identity)
        => EventHashes.RootScript(JsonSerializer.Serialize(new[] { identity.AssetName.ToUpperInvariant(), identity.StorageEventId }));

    internal static (int Width, int Height) ValidatePng(Stream image)
    {
        if (image.Length > 64 * 1024 * 1024)
            throw new InvalidDataException("Photo file is too large.");
        Span<byte> header = stackalloc byte[24];
        int offset = 0;
        while (offset < header.Length)
        {
            int read = image.Read(header[offset..]);
            if (read == 0)
                throw new InvalidDataException("Truncated PNG header.");
            offset += read;
        }
        return PngSize(header);
    }

    internal static (int Width, int Height) PngSize(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 24 || !bytes[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
            || !bytes.Slice(12, 4).SequenceEqual("IHDR"u8))
            throw new InvalidDataException("Invalid PNG header.");
        int width = BinaryPrimitives.ReadInt32BigEndian(bytes.Slice(16, 4));
        int height = BinaryPrimitives.ReadInt32BigEndian(bytes.Slice(20, 4));
        if (width <= 0 || height <= 0 || width > 8192 || height > 8192 || (long)width * height > 16_777_216)
            throw new InvalidDataException("Unsupported photo dimensions.");
        return (width, height);
    }

    private EventPhotoSet Writable(EventIdentity identity)
    {
        EventPhotoSet set = Read(identity);
        if (set.ReadOnly)
            throw new InvalidOperationException("Photo catalog is read-only; see the SMAPI log.");
        return set;
    }

    private void Commit(EventIdentity identity, EventPhotoSet set)
    {
        string folder = EventDirectory(identity);
        string archive = Path.Combine(folder, "archive");
        fs.CreateDirectory(archive);
        string path = Path.Combine(folder, "index.json");
        string temporary = Path.Combine(folder, Guid.NewGuid().ToString("N") + ".tmp");
        fs.WriteNewFlushed(temporary, JsonSerializer.SerializeToUtf8Bytes(new PhotoDocument
        {
            AssetName = identity.AssetName,
            EventId = identity.StorageEventId,
            CoverId = set.CoverId,
            WeddingDate = set.WeddingDate,
            Photos = set.Photos.ToList()
        }, JsonOptions));
        if (fs.FileExists(path))
        {
            fs.ReplaceFile(temporary, path, Path.Combine(archive, $"index-{DateTime.UtcNow:yyyyMMddHHmmssfffffff}-{Guid.NewGuid():N}.json"));
            TrimIndexBackups(archive);
        }
        else
            fs.MoveFile(temporary, path);
        sets[identity] = set;
    }

    private void TrimIndexBackups(string archive)
    {
        try
        {
            // Timestamped names sort by age; a catalog retired by adoption is kept.
            foreach (string old in fs.EnumerateFiles(archive, "index-*.json")
                .Where(path => !Path.GetFileName(path).StartsWith("index-adopted-", StringComparison.Ordinal))
                .OrderByDescending(Path.GetFileName, StringComparer.Ordinal).Skip(MaxIndexBackups))
                fs.DeleteFile(old);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { warn("Old photo catalog backups could not be removed: " + error.Message); }
    }

    private EventPhotoSet ReadDocument(string path, EventIdentity identity)
    {
        if (fs.GetFileLength(path) > 4 * 1024 * 1024)
            throw new InvalidDataException("Photo catalog is too large.");
        PhotoDocument document = JsonSerializer.Deserialize<PhotoDocument>(fs.ReadAllText(path)) ?? throw new InvalidDataException("Empty photo catalog.");
        if (document.Version != 1)
            throw new NotSupportedException($"Unsupported photo catalog version {document.Version}; left untouched.");
        if (new EventIdentity(document.AssetName, document.EventId) != identity || document.Photos is null
            || document.Photos.Any(photo => photo is null || !Guid.TryParseExact(photo.Id, "N", out _) || photo.Width <= 0 || photo.Height <= 0)
            || document.Photos.Select(photo => photo.Id).Distinct().Count() != document.Photos.Count
            || document.CoverId is not null && !document.Photos.Any(photo => photo.Id == document.CoverId)
            || document.WeddingDate is { IsValid: false })
            throw new InvalidDataException("Photo catalog identity or entries are invalid.");
        return new(document.Photos, document.CoverId, WeddingDate: document.WeddingDate?.Normalized());
    }

    private bool TryDiscoverWeddingIdentity(string subDir, out EventIdentity identity)
    {
        string indexPath = Path.Combine(subDir, "index.json");
        if (fs.FileExists(indexPath))
        {
            try
            {
                if (TryPeekWeddingIdentity(indexPath, out identity))
                    return true;
                return false;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
            {
                string archive = Path.Combine(subDir, "archive");
                if (fs.DirectoryExists(archive))
                {
                    foreach (string backup in fs.EnumerateFiles(archive, "index-*.json").OrderByDescending(Path.GetFileName))
                    {
                        try
                        {
                            if (TryPeekWeddingIdentity(backup, out identity))
                                return true;
                        }
                        catch { }
                    }
                }
            }
        }
        identity = default;
        return false;
    }

    private bool TryPeekWeddingIdentity(string path, out EventIdentity identity)
    {
        identity = default;
        if (fs.GetFileLength(path) > 4 * 1024 * 1024)
            return false;
        PhotoDocument? document = JsonSerializer.Deserialize<PhotoDocument>(fs.ReadAllText(path));
        if (document is null || document.Version != 1)
            return false;
        EventIdentity candidate = new(document.AssetName, document.EventId);
        if (!WeddingMemento.IsWeddingIdentity(candidate))
            return false;
        identity = candidate;
        return true;
    }

    private sealed class PhotoDocument
    {
        public int Version { get; set; } = 1;
        public string AssetName { get; set; } = "";
        public string EventId { get; set; } = "";
        public string? CoverId { get; set; }
        public WeddingDateRecord? WeddingDate { get; set; }
        public List<EventPhoto> Photos { get; set; } = [];
    }
}
