using System.Text.Json;

namespace StardewGallery;

internal sealed record ModDirectoryIdentity(string UniqueId, string Version, string? ContentPackFor, string? EntryDll);
internal sealed record ModDirectoryDiscoveryResult(IReadOnlyDictionary<string, string> Directories, IReadOnlyList<EventDefinitionScanIssue> Issues);

/// <summary>Locates loaded mods using public startup inputs and disk manifests, without SMAPI internals.</summary>
internal static class ModDirectoryDiscovery
{
    private const int MaxDepth = 32;
    private const int MaxEntries = 32768;
    private const int MaxManifestBytes = 1024 * 1024;
    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>Reproduces SMAPI 4.5.2's documented mods-path precedence; never guesses an ancestor folder.</summary>
    internal static string? ResolveRoot(string gameDirectory, string ownDirectory, IReadOnlyList<string> commandLine, string? environmentPath)
    {
        try
        {
            if (!Path.IsPathFullyQualified(gameDirectory) || !Path.IsPathFullyQualified(ownDirectory)) return null;
            string? raw = null;
            for (int i = commandLine.Count - 1; i >= 0; i--)
            {
                if (commandLine[i] != "--mods-path") continue;
                if (i + 1 >= commandLine.Count) return null;
                raw = commandLine[i + 1];
                break;
            }
            if (string.IsNullOrWhiteSpace(raw)) raw = environmentPath;
            string root = NormalizePath(Path.Combine(gameDirectory, string.IsNullOrWhiteSpace(raw) ? "Mods" : raw));
            string own = NormalizePath(ownDirectory);
            return IsVolumeRoot(root) || !IsWithin(root, own) ? null : root;
        }
        catch (Exception error) when (IsReadFailure(error)) { return null; }
    }

    internal static ModDirectoryDiscoveryResult Discover(string root, IReadOnlyList<ModDirectoryIdentity> loaded, Func<string, string>? normalizeJson = null)
    {
        var directories = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var issues = new List<EventDefinitionScanIssue>();
        var expected = loaded.GroupBy(mod => mod.UniqueId.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() == 1).ToDictionary(group => group.Key, group => group.Single(), StringComparer.OrdinalIgnoreCase);
        var foundIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var duplicateIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int entries = 0;
        long manifestBytes = 0;
        bool exhausted = false;
        try
        {
            if (!Path.IsPathFullyQualified(root)) throw new ArgumentException("The mods directory must be absolute.");
            root = NormalizePath(root);
            if (IsVolumeRoot(root)) throw new ArgumentException("A filesystem root cannot be scanned as a mods directory.");
            if (!Directory.Exists(root)) throw new DirectoryNotFoundException("The configured mods directory does not exist.");
            Visit(root, root, 0, new HashSet<string>(PathComparer));
        }
        catch (Exception error) when (IsReadFailure(error)) { issues.Add(new(".", error.Message)); }
        return new(directories, issues);

        void Issue(string path, string reason) => issues.Add(new(Path.GetRelativePath(root, path), reason));

        // SMAPI follows directory links, and managers such as Stardrop deploy mods as junctions or symbolic links.
        // Paths stay under the configured root; real targets are tracked only to stop link loops.
        void Visit(string directory, string parentReal, int depth, HashSet<string> ancestors)
        {
            if (exhausted) return;
            if (depth > MaxDepth) { Issue(directory, "Directory discovery depth limit reached."); return; }
            if (depth > 0 && (!IsWithin(root, directory) || IsIgnoredDirectory(Path.GetFileName(directory)))) return;
            string real;
            try { real = RealPath(directory, depth == 0 ? directory : Path.Combine(parentReal, Path.GetFileName(directory))); }
            catch (Exception error) when (IsReadFailure(error)) { Issue(directory, error.Message); return; }
            if (!ancestors.Add(real)) { Issue(directory, "A directory link loops back to a parent folder."); return; }
            try
            {
                string? manifest = null;
                var children = new List<string>();
                foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
                {
                    if (++entries > MaxEntries)
                    {
                        exhausted = true;
                        directories.Clear();
                        Issue(directory, "Directory discovery entry limit reached; incomplete matches were discarded.");
                        return;
                    }
                    FileAttributes attributes = File.GetAttributes(entry);
                    if ((attributes & FileAttributes.Directory) != 0) children.Add(entry);
                    else if (Path.GetFileName(entry).Equals("manifest.json", StringComparison.OrdinalIgnoreCase))
                    {
                        if (manifest is not null) { Issue(directory, "Multiple manifest filenames are ambiguous."); return; }
                        manifest = entry;
                    }
                }
                // SMAPI's root is a container. Inside it, a manifest ends descent so personal data,
                // assets, and backup manifests nested inside installed mods are never searched.
                if (depth > 0 && manifest is not null) { ReadManifest(directory, manifest); return; }
                foreach (string child in children.OrderBy(path => path, StringComparer.Ordinal)) Visit(child, real, depth + 1, ancestors);
            }
            catch (Exception error) when (IsReadFailure(error)) { Issue(directory, error.Message); }
            finally { ancestors.Remove(real); }
        }

        void ReadManifest(string directory, string path)
        {
            try
            {
                var file = new FileInfo(path);
                // Measure a linked manifest by its target, which is what gets read.
                if ((file.Attributes & FileAttributes.ReparsePoint) != 0 && file.ResolveLinkTarget(returnFinalTarget: true) is FileInfo target) file = target;
                if (file.Length > MaxManifestBytes) { Issue(path, "Manifest exceeds the size limit."); return; }
                if ((manifestBytes += file.Length) > 64L * 1024 * 1024)
                {
                    exhausted = true;
                    directories.Clear();
                    Issue(path, "Manifest byte limit reached; incomplete matches were discarded.");
                    return;
                }
                string json = File.ReadAllText(path);
                if (normalizeJson is not null) json = normalizeJson(json);
                using JsonDocument document = JsonDocument.Parse(json.TrimStart('\uFEFF'), new JsonDocumentOptions
                { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
                JsonElement data = document.RootElement;
                string? id = StringProperty(data, "UniqueID")?.Trim();
                if (id is null || !expected.TryGetValue(id, out ModDirectoryIdentity? identity)) return;
                // A duplicate cannot be resolved from public registry metadata, even if one copy's
                // version differs. Never select a path according to discovery order.
                if (!foundIds.Add(id))
                {
                    directories.Remove(id);
                    if (duplicateIds.Add(id)) Issue(path, $"Multiple installed manifests claim loaded mod '{id}'; no directory was selected.");
                    return;
                }
                string? contentPackFor = Property(data, "ContentPackFor", out JsonElement pack) ? StringProperty(pack, "UniqueID") : null;
                string? entryDll = StringProperty(data, "EntryDll");
                string? version = StringProperty(data, "Version");
                if (!SameOptional(contentPackFor, identity.ContentPackFor)
                    || !SameOptional(entryDll?.Replace('\\', '/'), identity.EntryDll?.Replace('\\', '/'))
                    || !SameVersion(version, identity.Version))
                { Issue(path, $"Manifest identity differs from loaded metadata for '{id}'."); return; }
                directories[id] = directory;
            }
            catch (Exception error) when (IsReadFailure(error) || error is JsonException || normalizeJson is not null)
            { Issue(path, $"Manifest could not be read: {error.Message}"); }
        }
    }

    private static bool SameVersion(string? actual, string expected)
    {
        if (actual is null) return false;
        string Normalize(string value)
        {
            value = value.Trim();
            int suffixAt = value.IndexOfAny(new[] { '-', '+' });
            string suffix = suffixAt < 0 ? "" : value[suffixAt..];
            string core = suffixAt < 0 ? value : value[..suffixAt];
            string[] parts = core.Split('.');
            if (parts.Length is < 1 or > 4 || parts.Any(part => !int.TryParse(part, out int number) || number < 0)) return value;
            return string.Join(".", parts.Select(part => int.Parse(part)).Concat(Enumerable.Repeat(0, 4 - parts.Length))) + suffix;
        }
        return Normalize(actual).Equals(Normalize(expected), StringComparison.OrdinalIgnoreCase);
    }
    private static bool SameOptional(string? left, string? right) => string.Equals(left?.Trim() ?? "", right?.Trim() ?? "", StringComparison.OrdinalIgnoreCase);
    private static bool Property(JsonElement value, string name, out JsonElement found)
    {
        if (value.ValueKind == JsonValueKind.Object)
            foreach (JsonProperty property in value.EnumerateObject())
                if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) { found = property.Value; return true; }
        found = default;
        return false;
    }
    private static string? StringProperty(JsonElement value, string name) => Property(value, name, out JsonElement property) && property.ValueKind == JsonValueKind.String ? property.GetString() : null;
    private static string NormalizePath(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    private static bool IsVolumeRoot(string path) => path.Equals(Path.GetPathRoot(path), PathComparison);
    private static bool IsWithin(string root, string path) => path.StartsWith(root + Path.DirectorySeparatorChar, PathComparison);
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    /// <summary>A link's final target, or <paramref name="fallback"/> (the real parent path plus this name) for an ordinary folder.</summary>
    private static string RealPath(string directory, string fallback)
        => (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0 && new DirectoryInfo(directory).ResolveLinkTarget(returnFinalTarget: true) is { } target
            ? NormalizePath(target.FullName) : fallback;
    private static bool IsIgnoredDirectory(string name) => name.StartsWith('.')
        || name.Equals("__MACOSX", StringComparison.OrdinalIgnoreCase)
        || name.Equals("__folder_managed_by_vortex", StringComparison.OrdinalIgnoreCase)
        || name.Equals("mcs", StringComparison.OrdinalIgnoreCase);
    private static bool IsReadFailure(Exception error) => error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.Security.SecurityException;
}