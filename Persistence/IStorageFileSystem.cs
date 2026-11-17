using System.IO.Enumeration;
using System.Text;

namespace StardewGallery;

internal interface IStorageFileSystem
{
    bool FileExists(string path);
    bool DirectoryExists(string path);
    void CreateDirectory(string path);
    byte[] ReadAllBytes(string path);
    string ReadAllText(string path);
    Stream OpenRead(string path);
    long GetFileLength(string path);
    DateTime GetLastWriteTimeUtc(string path);
    void WriteNewFlushed(string path, byte[] bytes);
    void ReplaceFile(string sourceFileName, string destinationFileName, string destinationBackupFileName);
    void MoveFile(string sourceFileName, string destinationFileName);
    void CopyFile(string sourceFileName, string destinationFileName);
    void DeleteFile(string path);
    IEnumerable<string> EnumerateFiles(string directory, string searchPattern);
    IEnumerable<string> EnumerateDirectories(string directory);
}

internal sealed class PhysicalStorageFileSystem : IStorageFileSystem
{
    internal static PhysicalStorageFileSystem Instance { get; } = new();

    private PhysicalStorageFileSystem() { }

    public bool FileExists(string path) => File.Exists(path);
    public bool DirectoryExists(string path) => Directory.Exists(path);
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);
    public byte[] ReadAllBytes(string path) => File.ReadAllBytes(path);
    public string ReadAllText(string path) => File.ReadAllText(path, Encoding.UTF8);
    public Stream OpenRead(string path) => File.OpenRead(path);
    public long GetFileLength(string path) => new FileInfo(path).Length;
    public DateTime GetLastWriteTimeUtc(string path) => File.GetLastWriteTimeUtc(path);

    public void WriteNewFlushed(string path, byte[] bytes)
    {
        using FileStream file = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        file.Write(bytes, 0, bytes.Length);
        file.Flush(flushToDisk: true);
    }

    public void ReplaceFile(string sourceFileName, string destinationFileName, string destinationBackupFileName)
        => File.Replace(sourceFileName, destinationFileName, destinationBackupFileName);

    public void MoveFile(string sourceFileName, string destinationFileName)
        => File.Move(sourceFileName, destinationFileName);

    public void CopyFile(string sourceFileName, string destinationFileName)
        => File.Copy(sourceFileName, destinationFileName);

    public void DeleteFile(string path)
        => File.Delete(path);

    public IEnumerable<string> EnumerateFiles(string directory, string searchPattern)
        => Directory.Exists(directory) ? Directory.EnumerateFiles(directory, searchPattern) : [];

    public IEnumerable<string> EnumerateDirectories(string directory)
        => Directory.Exists(directory) ? Directory.EnumerateDirectories(directory) : [];
}

/// <summary>
/// Deterministic in-memory <see cref="IStorageFileSystem"/> for isolated checks without touching real disk directories.
/// </summary>
internal sealed class InMemoryStorageFileSystem : IStorageFileSystem
{
    private sealed record Entry(byte[] Bytes, DateTime LastWriteUtc);

    private readonly Dictionary<string, Entry> files = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> directories = new(StringComparer.OrdinalIgnoreCase);
    private long tickOffset;

    private static string Norm(string path) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private DateTime NextClock() => new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddTicks(++tickOffset);

    internal void OverwriteRaw(string path, byte[] bytes)
    {
        string full = Norm(path);
        string? parent = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(parent))
            CreateDirectory(parent);
        files[full] = new Entry(bytes.ToArray(), NextClock());
    }

    public bool FileExists(string path) => files.ContainsKey(Norm(path));

    public bool DirectoryExists(string path)
    {
        string full = Norm(path);
        return directories.Contains(full)
            || files.Keys.Any(f => f.StartsWith(full + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
    }

    public void CreateDirectory(string path)
    {
        for (string? cur = Norm(path); !string.IsNullOrEmpty(cur); cur = Path.GetDirectoryName(cur))
        {
            directories.Add(cur);
            if (Path.GetPathRoot(cur)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Equals(cur, StringComparison.OrdinalIgnoreCase) == true)
                break;
        }
    }

    public byte[] ReadAllBytes(string path)
        => files.TryGetValue(Norm(path), out Entry? entry)
            ? entry.Bytes.ToArray()
            : throw new FileNotFoundException("Missing in-memory file.", path);

    public string ReadAllText(string path) => Encoding.UTF8.GetString(ReadAllBytes(path));

    public Stream OpenRead(string path) => new MemoryStream(ReadAllBytes(path), writable: false);

    public long GetFileLength(string path) => ReadAllBytes(path).LongLength;

    public DateTime GetLastWriteTimeUtc(string path)
        => files.TryGetValue(Norm(path), out Entry? entry) ? entry.LastWriteUtc : DateTime.MinValue;

    public void WriteNewFlushed(string path, byte[] bytes)
    {
        string full = Norm(path);
        if (files.ContainsKey(full))
            throw new IOException("File already exists: " + full);
        string? parent = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(parent))
            CreateDirectory(parent);
        files[full] = new Entry(bytes.ToArray(), NextClock());
    }

    public void ReplaceFile(string sourceFileName, string destinationFileName, string destinationBackupFileName)
    {
        string src = Norm(sourceFileName), dst = Norm(destinationFileName), bak = Norm(destinationBackupFileName);
        if (!files.TryGetValue(src, out Entry? srcEntry))
            throw new FileNotFoundException("Missing source file.", sourceFileName);
        if (!files.TryGetValue(dst, out Entry? dstEntry))
            throw new FileNotFoundException("Missing destination file.", destinationFileName);
        string? bakParent = Path.GetDirectoryName(bak);
        if (!string.IsNullOrEmpty(bakParent))
            CreateDirectory(bakParent);
        files[bak] = dstEntry with { LastWriteUtc = NextClock() };
        files[dst] = srcEntry with { LastWriteUtc = NextClock() };
        files.Remove(src);
    }

    public void MoveFile(string sourceFileName, string destinationFileName)
    {
        string src = Norm(sourceFileName), dst = Norm(destinationFileName);
        if (!files.TryGetValue(src, out Entry? srcEntry))
            throw new FileNotFoundException("Missing source file.", sourceFileName);
        if (files.ContainsKey(dst))
            throw new IOException("Destination file already exists: " + dst);
        string? dstParent = Path.GetDirectoryName(dst);
        if (!string.IsNullOrEmpty(dstParent))
            CreateDirectory(dstParent);
        files[dst] = srcEntry with { LastWriteUtc = NextClock() };
        files.Remove(src);
    }

    public void CopyFile(string sourceFileName, string destinationFileName)
    {
        string src = Norm(sourceFileName), dst = Norm(destinationFileName);
        if (!files.TryGetValue(src, out Entry? srcEntry))
            throw new FileNotFoundException("Missing source file.", sourceFileName);
        if (files.ContainsKey(dst))
            throw new IOException("Destination file already exists: " + dst);
        string? dstParent = Path.GetDirectoryName(dst);
        if (!string.IsNullOrEmpty(dstParent))
            CreateDirectory(dstParent);
        files[dst] = new Entry(srcEntry.Bytes.ToArray(), NextClock());
    }

    public void DeleteFile(string path) => files.Remove(Norm(path));

    public IEnumerable<string> EnumerateFiles(string directory, string searchPattern)
    {
        string dir = Norm(directory);
        string prefix = dir + Path.DirectorySeparatorChar;
        return files.Keys
            .Where(f => f.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && !f[prefix.Length..].Contains(Path.DirectorySeparatorChar)
                && FileSystemName.MatchesSimpleExpression(searchPattern, Path.GetFileName(f), ignoreCase: true))
            .ToArray();
    }

    public IEnumerable<string> EnumerateDirectories(string directory)
    {
        string dir = Norm(directory);
        string prefix = dir + Path.DirectorySeparatorChar;
        HashSet<string> children = new(StringComparer.OrdinalIgnoreCase);
        foreach (string item in directories.Concat(files.Keys))
        {
            if (!item.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;
            string rest = item[prefix.Length..];
            int sep = rest.IndexOf(Path.DirectorySeparatorChar);
            if (sep > 0)
                children.Add(prefix + rest[..sep]);
            else if (directories.Contains(item))
                children.Add(item);
        }
        return children.ToArray();
    }
}
