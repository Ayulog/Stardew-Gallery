namespace StardewGallery;

/// <summary>
/// Resolves files inside one installed pack the way SMAPI 4.5.2 reads them. The pack folder itself may be a
/// directory link, as Stardrop and similar managers deploy mods; links inside the pack are not followed.
/// With <paramref name="ignoreCase"/> (SMAPI's UseCaseInsensitivePaths, its default on Linux and Android),
/// relative paths match regardless of case, and entries that differ only by case are ambiguous.
/// </summary>
internal sealed class PackFiles(string directory, bool ignoreCase)
{
    private const int MaxDepth = 32;
    private const int MaxEntries = 65536;
    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    private readonly Dictionary<string, FileStamp> observed = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    private Dictionary<string, string?>? index;

    internal string Root { get; } = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));

    /// <summary>Every path looked up so far, with its state then; a later difference means the scan is out of date.</summary>
    internal IReadOnlyList<FileStamp> Observed => observed.Values.ToArray();

    /// <summary>Resolve a pack-relative file or directory, or say why it can't be read.</summary>
    internal string? Resolve(string relativePath, out string? problem)
    {
        problem = null;
        try
        {
            string normalized = relativePath.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
            if (Path.IsPathRooted(normalized)) { problem = "Absolute paths are not allowed"; return null; }
            string path = Path.GetFullPath(Path.Combine(Root, normalized));
            if (!path.StartsWith(Root + Path.DirectorySeparatorChar, PathComparison)) { problem = "Path leaves the content pack"; return null; }
            if (ignoreCase && Index().TryGetValue(Path.GetRelativePath(Root, path), out string? actual))
            {
                if (actual is null) { problem = "Path matches several entries that differ only by case"; return null; }
                path = Path.Combine(Root, actual);
            }
            Observe(path);
            // Check the target and each folder below the pack root; links at or above the root are how mods get deployed.
            for (string? current = path; current is not null && !current.Equals(Root, PathComparison); current = Path.GetDirectoryName(current))
            {
                if (!File.Exists(current) && !Directory.Exists(current)) { problem = "Referenced file or directory is missing"; return null; }
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) { problem = "Links inside a content pack are not followed"; return null; }
            }
            return path;
        }
        catch (Exception error) when (IsReadFailure(error)) { problem = Describe(error); return null; }
    }

    /// <summary>Record a file or folder whose later appearance, removal or change should trigger a rescan.</summary>
    internal void Observe(string path) => observed.TryAdd(path, FileStamp.Of(path));

    internal static string Describe(Exception error)
    {
        string message = error.Message.Split('\n')[0].Trim();
        return message.Length == 0 ? error.GetType().Name : $"{error.GetType().Name}: {(message.Length > 240 ? message[..240] + "…" : message)}";
    }

    internal static bool IsReadFailure(Exception error) => error is IOException or UnauthorizedAccessException
        or ArgumentException or NotSupportedException or System.Security.SecurityException;

    private Dictionary<string, string?> Index()
    {
        if (index is not null) return index;
        index = new(StringComparer.OrdinalIgnoreCase);
        int entries = 0;
        try { Visit(Root, 0); }
        catch (Exception error) when (IsReadFailure(error)) { }
        return index;

        void Visit(string folder, int depth)
        {
            foreach (string entry in Directory.EnumerateFileSystemEntries(folder))
            {
                if (++entries > MaxEntries) return;
                string relative = Path.GetRelativePath(Root, entry);
                index[relative] = index.TryGetValue(relative, out string? known) && known != relative ? null : relative;
                FileAttributes attributes = File.GetAttributes(entry);
                if (depth < MaxDepth && (attributes & FileAttributes.Directory) != 0 && (attributes & FileAttributes.ReparsePoint) == 0)
                    Visit(entry, depth + 1);
            }
        }
    }
}

/// <summary>A file or folder's state when a scan read it: missing, or its size and last write time.</summary>
internal sealed record FileStamp(string Path, bool Exists, long Length, DateTime LastWriteUtc)
{
    internal static FileStamp Of(string path)
    {
        try
        {
            var file = new FileInfo(path);
            if (file.Exists) return new(path, true, file.Length, file.LastWriteTimeUtc);
            var folder = new DirectoryInfo(path);
            return folder.Exists ? new(path, true, -1, folder.LastWriteTimeUtc) : new(path, false, 0, default);
        }
        catch (Exception error) when (PackFiles.IsReadFailure(error)) { return new(path, false, -2, default); }
    }

    internal bool IsCurrent() => Of(Path) == this;
}
