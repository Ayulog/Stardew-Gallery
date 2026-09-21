using System.Text.Json;

namespace StardewGallery;

/// <summary>
/// Reads AliveNpcs schema-1 scene declarations and the scene directories declared by its owned
/// content packs. This does not search arbitrary C# mod JSON, run modes, or inspect saved scene state.
/// </summary>
internal sealed class AliveNpcEventScanner
{
    internal const string FrameworkId = "Lucas.AliveNpcs";
    private const int MaxFiles = 2048;
    private const int MaxDepth = 32;
    private const long MaxFileBytes = 16 * 1024 * 1024;
    private const long MaxTotalBytes = 128 * 1024 * 1024;
    private readonly Func<string, string>? normalizeJson;
    private readonly Dictionary<string, ScanCache> scans = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    private static readonly JsonDocumentOptions JsonOptions = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

    internal AliveNpcEventScanner(Func<string, string>? normalizeJson = null) => this.normalizeJson = normalizeJson;

    internal EventDefinitionScanResult Scan(EventDefinitionPack pack, EventDefinitionIndex index, string? contentPackFor = null)
    {
        bool isCore = pack.Actor.UniqueId.Equals(FrameworkId, StringComparison.OrdinalIgnoreCase);
        if (!isCore && !string.Equals(contentPackFor, FrameworkId, StringComparison.OrdinalIgnoreCase))
            return new(0, 0, Array.Empty<EventDefinitionScanIssue>());
        index.RegisterPack(pack);
        string key = pack.Actor.UniqueId + "\0" + pack.DirectoryPath + "\0" + isCore;
        if (!scans.TryGetValue(key, out ScanCache? scan))
        {
            scan = Read(pack, isCore);
            scans.Add(key, scan);
        }
        foreach (EventDefinitionCandidate candidate in scan.Candidates) index.Add(candidate);
        return scan.Result;
    }

    private ScanCache Read(EventDefinitionPack pack, bool isCore)
    {
        List<EventDefinitionScanIssue> issues = new();
        HashSet<EventDefinitionCandidate> candidates = new();
        HashSet<string> directories = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        int filesRead = 0, entriesVisited = 0;
        long bytesRead = 0;
        string root = string.Empty;
        void Issue(string path, string reason)
        {
            if (issues.Count < 100) issues.Add(new(path.Replace('\\', '/'), reason));
        }
        try
        {
            root = Path.GetFullPath(pack.DirectoryPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (isCore) ScanDirectory("assets/scene_events");
            else
            {
                const string entry = "alive-npcs-ec.json";
                if (SafePath(entry, out string entryPath))
                {
                    using JsonDocument? document = ReadJson(entryPath, entry);
                    if (document is not null && Property(document.RootElement, "modes", out JsonElement modes) && modes.ValueKind == JsonValueKind.Array)
                        foreach (JsonElement mode in modes.EnumerateArray())
                            if (Property(mode, "sceneEventDirectories", out JsonElement paths) && paths.ValueKind == JsonValueKind.Array)
                                foreach (JsonElement path in paths.EnumerateArray())
                                    if (path.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(path.GetString())) ScanDirectory(path.GetString()!);
                }
            }
        }
        catch (Exception ex) when (IsReadFailure(ex)) { Issue(isCore ? "assets/scene_events" : "alive-npcs-ec.json", ex.GetType().Name); }
        return new(new(filesRead, candidates.Count, issues.AsReadOnly()), candidates.ToArray());

        void ScanDirectory(string relative)
        {
            if (SafePath(relative, out string path) && Directory.Exists(path)) ReadDirectory(path, 0);
        }

        bool SafePath(string relative, out string fullPath)
        {
            fullPath = string.Empty;
            try
            {
                string normalized = relative.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
                if (Path.IsPathRooted(normalized)) { Issue(relative, "Absolute paths are not allowed"); return false; }
                fullPath = Path.GetFullPath(Path.Combine(root, normalized));
                if (!fullPath.StartsWith(root + Path.DirectorySeparatorChar, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                { Issue(relative, "Path leaves the content pack"); return false; }
                for (string? path = fullPath; path is not null; path = Path.GetDirectoryName(path))
                    if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                    { Issue(relative, "Reparse points are not followed"); return false; }
                return true;
            }
            catch (Exception ex) when (IsReadFailure(ex)) { Issue(relative, ex.GetType().Name); return false; }
        }

        void ReadDirectory(string directory, int depth)
        {
            string relativeDirectory = Path.GetRelativePath(root, directory);
            if (depth > MaxDepth) { Issue(relativeDirectory, "Scene directory depth limit exceeded"); return; }
            if (!directories.Add(directory)) return;
            try
            {
                foreach (string path in Directory.EnumerateFileSystemEntries(directory).Take(MaxFiles + 1).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
                {
                    if (++entriesVisited > MaxFiles) { Issue(relativeDirectory, "Scene entry count limit exceeded"); return; }
                    string relative = Path.GetRelativePath(root, path);
                    try
                    {
                        FileAttributes attributes = File.GetAttributes(path);
                        if ((attributes & FileAttributes.ReparsePoint) != 0) { Issue(relative, "Reparse points are not followed"); continue; }
                        if ((attributes & FileAttributes.Directory) != 0) ReadDirectory(path, depth + 1);
                        else if (Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase)) ReadScene(path, relative);
                    }
                    catch (Exception ex) when (IsReadFailure(ex)) { Issue(relative, ex.GetType().Name); }
                }
            }
            catch (Exception ex) when (IsReadFailure(ex)) { Issue(relativeDirectory, ex.GetType().Name); }
        }

        JsonDocument? ReadJson(string path, string relative)
        {
            try
            {
                long length = new FileInfo(path).Length;
                if (length > MaxFileBytes || bytesRead + length > MaxTotalBytes) { Issue(relative, "Scene JSON size limit exceeded"); return null; }
                bytesRead += length;
                filesRead++;
                using var reader = new StreamReader(path, detectEncodingFromByteOrderMarks: true);
                string json = reader.ReadToEnd();
                if (normalizeJson is not null)
                {
                    try { json = normalizeJson(json); }
                    catch (Exception ex) { Issue(relative, ex.GetType().Name); return null; }
                }
                return JsonDocument.Parse(json, JsonOptions);
            }
            catch (Exception ex) when (IsReadFailure(ex)) { Issue(relative, ex.GetType().Name); return null; }
        }

        void ReadScene(string path, string relative)
        {
            using JsonDocument? document = ReadJson(path, relative);
            if (document is null) return;
            JsonElement scene = document.RootElement;
            if (!Property(scene, "schemaVersion", out JsonElement version) || version.ValueKind != JsonValueKind.Number
                || !version.TryGetInt32(out int schemaVersion) || schemaVersion != 1) return;
            if (!StringProperty(scene, "id", out _) || !StringProperty(scene, "location", out string location)
                || !StringProperty(scene, "eventKey", out string eventKey) || !Property(scene, "scene", out JsonElement definition)) return;
            bool hasScript = StringProperty(definition, "script", out string script);
            bool hasReference = StringProperty(definition, "scriptKey", out string scriptKey);
            if (!hasScript && !hasReference) return;
            // Schema 1 stores a game location name. A localization reference is a complete declaration,
            // but is not a resolved script: preserve its key separately and never invent a script hash.
            if (location.Contains('/') || location.Contains('\\') || location.Contains("{{", StringComparison.Ordinal)
                || EventDefinitionIndex.GetEventId(eventKey).Length == 0 || EventDefinitionIndex.GetEventId(eventKey).Contains("{{", StringComparison.Ordinal)) return;
            candidates.Add(new(pack.Actor, "Data/Events/" + location.Trim(), eventKey.Trim(), relative.Replace('\\', '/'),
                hasScript ? EventHashes.RootScript(script) : string.Empty, hasReference ? scriptKey : null));
        }
    }

    private static bool StringProperty(JsonElement element, string name, out string value)
    {
        if (Property(element, name, out JsonElement property) && property.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(property.GetString())) { value = property.GetString()!; return true; }
        value = string.Empty;
        return false;
    }
    private static bool Property(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
            foreach (JsonProperty property in element.EnumerateObject())
                if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) { value = property.Value; return true; }
        value = default;
        return false;
    }
    private static bool IsReadFailure(Exception ex) => ex is IOException or UnauthorizedAccessException
        or JsonException or ArgumentException or NotSupportedException or System.Security.SecurityException;
    private sealed record ScanCache(EventDefinitionScanResult Result, IReadOnlyList<EventDefinitionCandidate> Candidates);
}
