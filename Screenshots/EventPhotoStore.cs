using System.Buffers.Binary;
using System.Globalization;
using System.Text.Json;

namespace StardewGallery;

internal sealed record EventPhoto(string Id, DateTimeOffset CapturedAt, int Width, int Height);
internal sealed record EventPhotoSet(IReadOnlyList<EventPhoto> Photos, string? CoverId, bool ReadOnly = false);

internal sealed class EventPhotoStore(string root, SaveProfileKey profile, Action<string> warn)
{
    private readonly string directory = Path.Combine(Path.GetFullPath(root),
        profile.FarmUniqueId.ToString("X16", CultureInfo.InvariantCulture) + "-" + profile.PlayerUniqueId.ToString("X16", CultureInfo.InvariantCulture));
    private readonly Dictionary<EventIdentity, EventPhotoSet> sets = [];
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    internal EventPhotoSet Read(EventIdentity identity)
    {
        if (sets.TryGetValue(identity, out EventPhotoSet? value))
            return value;
        string path = Path.Combine(EventDirectory(identity), "index.json");
        if (!File.Exists(path))
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
            if (Directory.Exists(archive))
                foreach (string backup in Directory.EnumerateFiles(archive, "index-*.json").OrderByDescending(Path.GetFileName))
                {
                    try { return sets[identity] = ReadDocument(backup, identity); }
                    catch (Exception backupError) when (backupError is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or NotSupportedException) { }
                }
            // Preserve an unreadable catalog until the player repairs it; never overwrite unknown data.
            return sets[identity] = new([], null, ReadOnly: true);
        }
    }

    internal EventPhoto Add(EventIdentity identity, byte[] image, byte[] thumbnail)
    {
        EventPhotoSet old = Writable(identity);
        var dimensions = PngSize(image);
        PngSize(thumbnail);
        string id = Guid.NewGuid().ToString("N");
        EventPhoto photo = new(id, DateTimeOffset.UtcNow, dimensions.Width, dimensions.Height);
        Directory.CreateDirectory(EventDirectory(identity));
        WriteNew(ImagePath(identity, id), image);
        WriteNew(ImagePath(identity, id, thumbnail: true), thumbnail);
        Commit(identity, new(old.Photos.Append(photo).ToArray(), old.Photos.Count == 0 ? id : old.CoverId));
        return photo;
    }

    internal void SetCover(EventIdentity identity, string? id)
    {
        EventPhotoSet old = Writable(identity);
        if (id is not null && !old.Photos.Any(photo => photo.Id == id))
            throw new ArgumentException("Photo isn't in this event.", nameof(id));
        if (id is not null)
        {
            using FileStream image = File.OpenRead(ImagePath(identity, id));
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
        Commit(identity, new(old.Photos.Where(photo => photo.Id != id).ToArray(), old.CoverId == id ? null : old.CoverId));
        string archive = Path.Combine(EventDirectory(identity), "archive", id + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(archive);
        foreach (bool thumbnail in new[] { false, true })
        {
            string image = ImagePath(identity, id, thumbnail);
            if (File.Exists(image))
                File.Move(image, Path.Combine(archive, Path.GetFileName(image)));
        }
    }

    internal string ImagePath(EventIdentity identity, string id, bool thumbnail = false)
    {
        if (!Guid.TryParseExact(id, "N", out _))
            throw new ArgumentException("Invalid photo ID.", nameof(id));
        return Path.Combine(EventDirectory(identity), id + (thumbnail ? ".thumb.png" : ".png"));
    }

    internal string EventDirectory(EventIdentity identity)
        => Path.Combine(directory, EventHashes.RootScript(JsonSerializer.Serialize(new[] { identity.AssetName.ToUpperInvariant(), identity.EventId })));

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
        Directory.CreateDirectory(archive);
        string path = Path.Combine(folder, "index.json");
        string temporary = Path.Combine(folder, Guid.NewGuid().ToString("N") + ".tmp");
        WriteNew(temporary, JsonSerializer.SerializeToUtf8Bytes(new PhotoDocument
        {
            AssetName = identity.AssetName, EventId = identity.EventId, CoverId = set.CoverId, Photos = set.Photos.ToList()
        }, JsonOptions));
        if (File.Exists(path))
            File.Replace(temporary, path, Path.Combine(archive, $"index-{DateTime.UtcNow:yyyyMMddHHmmssfffffff}-{Guid.NewGuid():N}.json"));
        else
            File.Move(temporary, path);
        sets[identity] = set;
    }

    private static void WriteNew(string path, byte[] bytes)
    {
        using FileStream file = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        file.Write(bytes);
        file.Flush(flushToDisk: true);
    }

    private static EventPhotoSet ReadDocument(string path, EventIdentity identity)
    {
        if (new FileInfo(path).Length > 4 * 1024 * 1024)
            throw new InvalidDataException("Photo catalog is too large.");
        PhotoDocument document = JsonSerializer.Deserialize<PhotoDocument>(File.ReadAllText(path)) ?? throw new InvalidDataException("Empty photo catalog.");
        if (document.Version != 1)
            throw new NotSupportedException($"Unsupported photo catalog version {document.Version}; left untouched.");
        if (new EventIdentity(document.AssetName, document.EventId) != identity || document.Photos is null
            || document.Photos.Any(photo => photo is null || !Guid.TryParseExact(photo.Id, "N", out _) || photo.Width <= 0 || photo.Height <= 0)
            || document.Photos.Select(photo => photo.Id).Distinct().Count() != document.Photos.Count
            || document.CoverId is not null && !document.Photos.Any(photo => photo.Id == document.CoverId))
            throw new InvalidDataException("Photo catalog identity or entries are invalid.");
        return new(document.Photos, document.CoverId);
    }

    private sealed class PhotoDocument
    {
        public int Version { get; set; } = 1;
        public string AssetName { get; set; } = "";
        public string EventId { get; set; } = "";
        public string? CoverId { get; set; }
        public List<EventPhoto> Photos { get; set; } = [];
    }
}
