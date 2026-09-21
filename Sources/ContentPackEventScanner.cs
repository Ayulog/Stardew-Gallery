using System.Text.Json;
using System.Text.RegularExpressions;

namespace StardewGallery;

internal sealed record EventDefinitionScanIssue(string RelativePath, string Reason);
internal sealed record EventDefinitionScanResult(int FilesRead, int DefinitionsAdded, IReadOnlyList<EventDefinitionScanIssue> Issues);

/// <summary>
/// Reads only declarations reachable from content.json. It does not execute patches, evaluate When,
/// inspect unrelated JSON files, or retain event scripts after hashing them.
/// </summary>
internal sealed class ContentPackEventScanner
{
    private readonly Dictionary<string, CachedScan> scans = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    private readonly Func<string, string, IReadOnlyDictionary<string, string>?>? readCompiledEvents;
    private readonly Func<string, string>? normalizeJson;

    internal ContentPackEventScanner(Func<string, string, IReadOnlyDictionary<string, string>?>? readCompiledEvents = null,
        Func<string, string>? normalizeJson = null)
    {
        this.readCompiledEvents = readCompiledEvents;
        this.normalizeJson = normalizeJson;
    }
    internal EventDefinitionScanResult Scan(EventDefinitionPack pack, EventDefinitionIndex index)
    {
        index.RegisterPack(pack);
        string cacheKey = pack.Actor.UniqueId + "\0" + pack.DirectoryPath;
        if (!scans.TryGetValue(cacheKey, out CachedScan? scan))
        {
            using var reader = new PackReader(pack, readCompiledEvents, normalizeJson);
            scan = reader.Read();
            scans.Add(cacheKey, scan);
        }
        foreach (EventDefinitionCandidate candidate in scan.Candidates) index.Add(candidate);
        return scan.Result;
    }

    private sealed record CachedScan(EventDefinitionScanResult Result, IReadOnlyList<EventDefinitionCandidate> Candidates);

    private sealed class PackReader : IDisposable
    {
        private const int MaxDepth = 32;
        private const int MaxFiles = 2048;
        private const int MaxExpansions = 128;
        private const long MaxFileBytes = 16 * 1024 * 1024;
        private const long MaxTotalBytes = 128 * 1024 * 1024;
        private static readonly StringComparer PathComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        private static readonly Regex TokenPattern = new(@"\{\{\s*([^{}]+?)\s*\}\}", RegexOptions.CultureInvariant);
        private static readonly JsonDocumentOptions JsonOptions = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
        private readonly EventDefinitionPack pack;
        private readonly Func<string, string, IReadOnlyDictionary<string, string>?>? readCompiledEvents;
        private readonly Func<string, string>? normalizeJson;
        private readonly Dictionary<string, IReadOnlyDictionary<string, string>?> compiledFiles = new(PathComparer);
        private readonly Dictionary<string, JsonDocument?> documents = new(PathComparer);
        private readonly Dictionary<string, List<string>> tokens = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> visitedIncludes = new(PathComparer);
        private readonly HashSet<EventDefinitionCandidate> candidates = new();
        private readonly List<EventDefinitionScanIssue> issues = new();
        private readonly HashSet<string> issueKeys = new(StringComparer.Ordinal);
        private string root = string.Empty;
        private long bytesRead;
        private int filesRead;

        internal PackReader(EventDefinitionPack pack, Func<string, string, IReadOnlyDictionary<string, string>?>? readCompiledEvents, Func<string, string>? normalizeJson)
        {
            this.pack = pack;
            this.readCompiledEvents = readCompiledEvents;
            this.normalizeJson = normalizeJson;
        }

        internal CachedScan Read()
        {
            try
            {
                root = Path.GetFullPath(pack.DirectoryPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                tokens["ModId"] = new() { pack.Actor.UniqueId };
                JsonDocument? content = ReadJson("content.json");
                if (content is not null)
                {
                    ReadTokens(content.RootElement);
                    ReadChanges("content.json", 0);
                }
            }
            catch (Exception ex) when (IsReadFailure(ex)) { Issue("content.json", ex.GetType().Name); }
            EventDefinitionCandidate[] result = candidates.ToArray();
            return new(new(filesRead, result.Length, issues.AsReadOnly()), result);
        }

        private void ReadTokens(JsonElement content)
        {
            if (Property(content, "ConfigSchema", out JsonElement schema) && schema.ValueKind == JsonValueKind.Object)
                foreach (JsonProperty setting in schema.EnumerateObject())
                    if (Property(setting.Value, "Default", out JsonElement value)) SetToken(setting.Name, value);
            // Content Patcher writes this file only when a pack has configurable settings.
            if (File.Exists(Path.Combine(root, "config.json")))
            {
                JsonDocument? config = ReadJson("config.json");
                if (config?.RootElement.ValueKind == JsonValueKind.Object)
                    foreach (JsonProperty setting in config.RootElement.EnumerateObject()) SetToken(setting.Name, setting.Value);
            }
            if (Property(content, "DynamicTokens", out JsonElement dynamicTokens) && dynamicTokens.ValueKind == JsonValueKind.Array)
                foreach (JsonElement token in dynamicTokens.EnumerateArray())
                    if (Property(token, "Name", out JsonElement name) && name.ValueKind == JsonValueKind.String
                        && Property(token, "Value", out JsonElement value))
                    {
                        string key = name.GetString()!;
                        if (!tokens.TryGetValue(key, out List<string>? options)) tokens.Add(key, options = new());
                        // Every literal branch is a declaration candidate; its When is deliberately not evaluated.
                        options.AddRange(Values(value));
                    }
        }

        private void SetToken(string name, JsonElement value)
        {
            string[] values = Values(value).ToArray();
            if (values.Length > 0) tokens[name] = values.ToList();
        }

        private static IEnumerable<string> Values(JsonElement value)
        {
            switch (value.ValueKind)
            {
                case JsonValueKind.String: yield return value.GetString()!; break;
                case JsonValueKind.True: yield return "true"; break;
                case JsonValueKind.False: yield return "false"; break;
                case JsonValueKind.Number: yield return value.GetRawText(); break;
                case JsonValueKind.Array:
                    foreach (JsonElement element in value.EnumerateArray())
                        foreach (string text in Values(element)) yield return text;
                    break;
            }
        }

        private void ReadChanges(string relativePath, int depth)
        {
            if (depth > MaxDepth) { Issue(relativePath, "Include depth limit exceeded"); return; }
            string? path = ResolvePath(relativePath);
            if (path is null || !visitedIncludes.Add(path)) return;
            JsonDocument? document = ReadJson(relativePath);
            if (document is null) return;
            JsonElement changes = document.RootElement;
            if (changes.ValueKind == JsonValueKind.Object && !Property(changes, "Changes", out changes)) return;
            if (changes.ValueKind != JsonValueKind.Array) return;
            foreach (JsonElement change in changes.EnumerateArray())
            {
                if (!Property(change, "Action", out JsonElement action) || action.ValueKind != JsonValueKind.String) continue;
                string actionName = action.GetString()!;
                if (actionName.Equals("Include", StringComparison.OrdinalIgnoreCase))
                {
                    if (Property(change, "FromFile", out JsonElement from) && from.ValueKind == JsonValueKind.String)
                        foreach (string included in ExpandList(from.GetString()!, null, false, relativePath))
                            ReadChanges(included, depth + 1);
                    continue;
                }
                bool isLoad = actionName.Equals("Load", StringComparison.OrdinalIgnoreCase);
                if (!isLoad && !actionName.Equals("EditData", StringComparison.OrdinalIgnoreCase)) continue;
                if (!Property(change, "Target", out JsonElement target) || target.ValueKind != JsonValueKind.String) continue;
                if (!CouldContainEventTarget(target.GetString()!)) continue;
                foreach (string asset in ExpandList(target.GetString()!, null, false, relativePath)
                    .Select(EventSourceScope.NormalizeAssetName).Where(EventDefinitionIndex.IsEventAsset))
                {
                    if (!isLoad && Property(change, "Entries", out JsonElement entries)) ReadEntries(entries, asset, relativePath);
                    if (Property(change, "FromFile", out JsonElement from) && from.ValueKind == JsonValueKind.String)
                        foreach (string dataFile in ExpandList(from.GetString()!, asset, false, relativePath))
                        {
                            if (isLoad && Path.GetExtension(dataFile).Equals(".xnb", StringComparison.OrdinalIgnoreCase))
                            {
                                ReadCompiledEntries(dataFile, asset);
                                continue;
                            }
                            if (!Path.GetExtension(dataFile).Equals(".json", StringComparison.OrdinalIgnoreCase))
                            { Issue(dataFile, "Binary event data is not statically scanned"); continue; }
                            JsonDocument? data = ReadJson(dataFile);
                            if (data is null) continue;
                            JsonElement values = data.RootElement;
                            // Load supplies a plain dictionary; tolerate EditData patch-shaped files as well.
                            if (!isLoad && Property(values, "Entries", out JsonElement importedEntries)) values = importedEntries;
                            ReadEntries(values, asset, dataFile);
                        }
                }
            }
        }

        private void ReadCompiledEntries(string relativePath, string asset)
        {
            string? path = ResolvePath(relativePath);
            if (path is null) return;
            if (!compiledFiles.TryGetValue(path, out IReadOnlyDictionary<string, string>? values))
            {
                if (documents.Count + compiledFiles.Count >= MaxFiles) { Issue(relativePath, "File count limit exceeded"); return; }
                compiledFiles[path] = null;
                if (readCompiledEvents is null) { Issue(relativePath, "Binary event reader is unavailable"); return; }
                try
                {
                    long length = new FileInfo(path).Length;
                    if (length > MaxFileBytes || bytesRead + length > MaxTotalBytes)
                    { Issue(relativePath, "Event data size limit exceeded"); return; }
                    bytesRead += length;
                    filesRead++;
                    // The game-specific reader can throw a framework exception which this pure module
                    // cannot reference. Limit isolation to this one callback and continue other files.
                    try { values = readCompiledEvents(root, NormalizeRelative(relativePath)); }
                    catch (Exception ex) { Issue(relativePath, ex.GetType().Name); return; }
                    compiledFiles[path] = values;
                    if (values is null) Issue(relativePath, "Binary event dictionary could not be read");
                }
                catch (Exception ex) when (IsReadFailure(ex) || ex is InvalidOperationException)
                { Issue(relativePath, ex.GetType().Name); return; }
            }
            if (values is not null)
                foreach (KeyValuePair<string, string> entry in values)
                    AddEntry(entry.Key, entry.Value, asset, relativePath);
        }

        private void ReadEntries(JsonElement entries, string asset, string relativePath)
        {
            if (entries.ValueKind != JsonValueKind.Object) return;
            foreach (JsonProperty entry in entries.EnumerateObject())
            {
                // Fields/TextOperations/null delete or modify an existing event and are not full definitions.
                if (entry.Value.ValueKind != JsonValueKind.String) continue;
                AddEntry(entry.Name, entry.Value.GetString()!, asset, relativePath);
            }
        }

        private void AddEntry(string rawKey, string script, string asset, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(script)) return;
            foreach (string key in Expand(rawKey, asset, true, relativePath))
            {
                string id = EventDefinitionIndex.GetEventId(key);
                if (id.Length == 0 || id.Contains("{{", StringComparison.Ordinal)) continue;
                candidates.Add(new(pack.Actor, asset, key, NormalizeRelative(relativePath), EventHashes.RootScript(script)));
            }
        }

        private IEnumerable<string> ExpandList(string text, string? target, bool preserveUnknown, string evidence)
            => Expand(text, target, preserveUnknown, evidence).SelectMany(value => value.Split(','))
                .Select(value => value.Trim()).Where(value => value.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase);

        private IReadOnlyList<string> Expand(string text, string? target, bool preserveUnknown, string evidence)
        {
            List<string> result = ExpandCore(text, target, preserveUnknown, new(StringComparer.OrdinalIgnoreCase), 0);
            if (result.Count >= MaxExpansions) Issue(evidence, "Token expansion limit reached");
            if (result.Count == 0 && text.Contains("{{", StringComparison.Ordinal))
                Issue(evidence, "Unresolved dynamic token in event target or referenced path");
            return result.Distinct(StringComparer.Ordinal).Take(MaxExpansions).ToArray();
        }

        private List<string> ExpandCore(string text, string? target, bool preserveUnknown, HashSet<string> chain, int depth)
        {
            if (depth > MaxDepth) return new();
            MatchCollection matches = TokenPattern.Matches(text);
            if (matches.Count == 0) return text.Contains("{{", StringComparison.Ordinal) && !preserveUnknown ? new() : new() { text };
            List<string> results = new() { string.Empty };
            int offset = 0;
            foreach (Match match in matches)
            {
                string key = match.Groups[1].Value.Trim();
                string prefix = text.Substring(offset, match.Index - offset);
                List<string> options = new();
                if (key.Equals("Target", StringComparison.OrdinalIgnoreCase) && target is not null) options.Add(target);
                else if (key.Equals("TargetWithoutPath", StringComparison.OrdinalIgnoreCase) && target is not null) options.Add(target.Split('/').Last());
                else if (tokens.TryGetValue(key, out List<string>? rawOptions) && chain.Add(key))
                {
                    foreach (string raw in rawOptions)
                    {
                        options.AddRange(ExpandCore(raw, target, preserveUnknown, chain, depth + 1));
                        if (options.Count >= MaxExpansions) break;
                    }
                    chain.Remove(key);
                }
                if (options.Count == 0)
                {
                    if (!preserveUnknown) return new();
                    options.Add(match.Value);
                }
                results = results.SelectMany(prior => options.Take(MaxExpansions).Select(option => prior + prefix + option)).Take(MaxExpansions).ToList();
                offset = match.Index + match.Length;
            }
            return results.Select(value => value + text[offset..]).ToList();
        }

        private JsonDocument? ReadJson(string relativePath)
        {
            string? path = ResolvePath(relativePath);
            if (path is null) return null;
            if (documents.TryGetValue(path, out JsonDocument? cached)) return cached;
            if (documents.Count + compiledFiles.Count >= MaxFiles) { Issue(relativePath, "File count limit exceeded"); return null; }
            documents[path] = null;
            try
            {
                long length = new FileInfo(path).Length;
                if (length > MaxFileBytes || bytesRead + length > MaxTotalBytes)
                { Issue(relativePath, "JSON size limit exceeded"); return null; }
                bytesRead += length;
                filesRead++;
                // StreamReader accepts UTF-8 BOMs, which are common in manually edited content packs.
                using var reader = new StreamReader(path, detectEncodingFromByteOrderMarks: true);
                string json = reader.ReadToEnd();
                if (normalizeJson is not null)
                {
                    // Runtime supplies Content Patcher's own JSON dialect reader (Newtonsoft), including
                    // single-quoted strings and unquoted property names, without coupling pure checks to it.
                    try { json = normalizeJson(json); }
                    catch (Exception ex) { Issue(relativePath, ex.GetType().Name); return null; }
                }
                else json = EscapeLiteralStringControls(json);
                JsonDocument document = JsonDocument.Parse(json, JsonOptions);
                documents[path] = document;
                return document;
            }
            catch (Exception ex) when (IsReadFailure(ex)) { Issue(relativePath, ex.GetType().Name); return null; }
        }

        /// <summary>
        /// Content Patcher's JSON reader accepts literal line breaks/tabs inside strings. Normalize only
        /// those characters for System.Text.Json; comments, escape sequences and file contents stay intact.
        /// </summary>
        private static string EscapeLiteralStringControls(string text)
        {
            System.Text.StringBuilder? normalized = null;
            bool inString = false, escaped = false, lineComment = false, blockComment = false;
            for (int i = 0; i < text.Length; i++)
            {
                char current = text[i];
                if (lineComment)
                {
                    if (current is '\r' or '\n') lineComment = false;
                }
                else if (blockComment)
                {
                    if (current == '*' && i + 1 < text.Length && text[i + 1] == '/')
                    {
                        normalized?.Append(current).Append(text[++i]);
                        if (normalized is null) i++;
                        blockComment = false;
                        continue;
                    }
                }
                else if (inString)
                {
                    if (escaped) escaped = false;
                    else if (current == '\\') escaped = true;
                    else if (current == '"') inString = false;
                    else if (current < 0x20)
                    {
                        normalized ??= new System.Text.StringBuilder(text.Length + 16).Append(text, 0, i);
                        normalized.Append("\\u").Append(((int)current).ToString("x4", System.Globalization.CultureInfo.InvariantCulture));
                        continue;
                    }
                }
                else if (current == '"') inString = true;
                else if (current == '/' && i + 1 < text.Length)
                {
                    if (text[i + 1] is '/' or '*')
                    {
                        lineComment = text[i + 1] == '/';
                        blockComment = !lineComment;
                        normalized?.Append(current);
                        i++;
                        normalized?.Append(text[i]);
                        continue;
                    }
                }
                normalized?.Append(current);
            }
            return normalized?.ToString() ?? text;
        }
        private string? ResolvePath(string relativePath)
        {
            try
            {
                string normalized = relativePath.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
                if (Path.IsPathRooted(normalized)) { Issue(relativePath, "Absolute paths are not allowed"); return null; }
                string path = Path.GetFullPath(Path.Combine(root, normalized));
                if (!path.StartsWith(root + Path.DirectorySeparatorChar, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                { Issue(relativePath, "Path leaves the content pack"); return null; }
                // Check the file and each ancestor, including the pack itself, before reading anything.
                for (string? current = path; current is not null; current = Path.GetDirectoryName(current))
                {
                    if (!File.Exists(current) && !Directory.Exists(current))
                    { Issue(relativePath, "Referenced file or directory is missing"); return null; }
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    { Issue(relativePath, "Reparse points are not followed"); return null; }
                }
                return path;
            }
            catch (Exception ex) when (IsReadFailure(ex)) { Issue(relativePath, ex.GetType().Name); return null; }
        }

        private static bool CouldContainEventTarget(string rawTarget)
        {
            const string eventPrefix = "Data/Events/";
            foreach (string target in rawTarget.Replace('\\', '/').Split(',').Select(value => value.Trim()))
            {
                int tokenStart = target.IndexOf("{{", StringComparison.Ordinal);
                string knownPrefix = tokenStart >= 0 ? target[..tokenStart] : target;
                if (knownPrefix.StartsWith(eventPrefix, StringComparison.OrdinalIgnoreCase)
                    || (tokenStart >= 0 && eventPrefix.StartsWith(knownPrefix, StringComparison.OrdinalIgnoreCase))) return true;
            }
            return false;
        }
        private static string NormalizeRelative(string relativePath) => relativePath.Replace('\\', '/');
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
        private void Issue(string path, string reason)
        {
            if (issues.Count < 100 && issueKeys.Add(path + "\0" + reason)) issues.Add(new(NormalizeRelative(path), reason));
        }
        public void Dispose()
        {
            foreach (JsonDocument? document in documents.Values) document?.Dispose();
        }
    }
}
