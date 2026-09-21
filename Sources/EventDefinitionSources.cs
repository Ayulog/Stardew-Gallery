using System.Diagnostics;
using Microsoft.Xna.Framework.Content;
using StardewModdingAPI;
using StardewValley;

namespace StardewGallery;

/// <summary>Indexes installed event definitions on demand, independently of runtime tracing.</summary>
internal sealed class EventDefinitionSources(IModHelper helper, IMonitor monitor)
{
    private EventDefinitionIndex? index;
    private readonly HashSet<string> gameAssets = new(StringComparer.OrdinalIgnoreCase);

    internal string Status => index is null ? "Event definition files have not been scanned yet."
        : $"Event definition index: {index.Count} declarations; {gameAssets.Count} vanilla assets checked.";

    internal void Reset()
    {
        index = null;
        gameAssets.Clear();
    }

    internal EventOriginMatch Read(string assetName, string eventId)
    {
        EnsureScanned();
        ReadGameDefinitions(assetName);
        // Stardew 1.6.15 merges Trailer definitions into Trailer_Big when no direct key exists.
        if (EventSourceScope.NormalizeAssetName(assetName).Equals("Data/Events/Trailer_Big", StringComparison.OrdinalIgnoreCase))
            ReadGameDefinitions("Data/Events/Trailer");
        return index!.Match(assetName, eventId);
    }

    internal void EnsureScanned()
    {
        if (index is not null) return;
        index = new EventDefinitionIndex();
        var clock = Stopwatch.StartNew();
        int packs = 0, files = 0, issues = 0;
        try
        {
            IModInfo[] loaded = helper.ModRegistry.GetAll().Where(mod =>
                string.Equals(mod.Manifest.ContentPackFor?.UniqueID, "Pathoschild.ContentPatcher", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mod.Manifest.ContentPackFor?.UniqueID, AliveNpcEventScanner.FrameworkId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(mod.Manifest.UniqueID, AliveNpcEventScanner.FrameworkId, StringComparison.OrdinalIgnoreCase)).ToArray();
            // The public registry supplies loaded identities, not directories. Find their manifests
            // under the launch-configured Mods root; never access SMAPI's private ModMetadata.
            string? root = ModDirectoryDiscovery.ResolveRoot(Constants.GamePath, helper.DirectoryPath,
                Environment.GetCommandLineArgs(), Environment.GetEnvironmentVariable("SMAPI_MODS_PATH"));
            if (root is null)
            {
                monitor.Log("Event definition scan could not locate the configured Mods folder containing this mod. File sources remain unavailable.", LogLevel.Warn);
                return;
            }
            var identities = loaded.Select(mod => new ModDirectoryIdentity(mod.Manifest.UniqueID,
                mod.Manifest.Version.ToString(), mod.Manifest.ContentPackFor?.UniqueID, mod.Manifest.EntryDll)).ToArray();
            ModDirectoryDiscoveryResult discovered = ModDirectoryDiscovery.Discover(root, identities, NormalizeJson);
            issues += discovered.Issues.Count;
            foreach (EventDefinitionScanIssue issue in discovered.Issues.Take(5))
                monitor.Log($"Event mod directory scan {issue.RelativePath}: {issue.Reason}", LogLevel.Debug);
            var scanner = new ContentPackEventScanner(ReadCompiledDefinitions, NormalizeJson);
            var aliveScanner = new AliveNpcEventScanner(NormalizeJson);
            foreach (IModInfo mod in loaded)
            {
                if (!discovered.Directories.TryGetValue(mod.Manifest.UniqueID, out string? directory)) continue;
                try
                {
                    string? contentPackFor = mod.Manifest.ContentPackFor?.UniqueID;
                    var actor = new EventSourceActor(mod.Manifest.UniqueID, mod.Manifest.Name, mod.Manifest.Version.ToString());
                    var dependencies = mod.Manifest.Dependencies.Select(dependency => dependency.UniqueID).ToList();
                    if (contentPackFor is not null) dependencies.Add(contentPackFor);
                    var pack = new EventDefinitionPack(actor, directory, dependencies);
                    EventDefinitionScanResult result = string.Equals(contentPackFor, "Pathoschild.ContentPatcher", StringComparison.OrdinalIgnoreCase)
                        ? scanner.Scan(pack, index) : aliveScanner.Scan(pack, index, contentPackFor);
                    packs++;
                    files += result.FilesRead;
                    issues += result.Issues.Count;
                    foreach (EventDefinitionScanIssue issue in result.Issues.Take(3))
                        monitor.Log($"Event definition scan [{actor.UniqueId}] {issue.RelativePath}: {issue.Reason}", LogLevel.Debug);
                }
                catch (Exception error)
                {
                    issues++;
                    monitor.Log($"Event definition scan skipped {mod.Manifest.UniqueID}: {error.Message}", LogLevel.Debug);
                }
            }
            monitor.Log($"Event definition scan: {packs}/{loaded.Length} loaded event-capable mods/packs, {files} files, {index.Count} definitions, {issues} unresolved paths/files ({clock.ElapsedMilliseconds} ms).",
                packs < loaded.Length ? LogLevel.Warn : LogLevel.Info);
        }
        catch (Exception error)
        {
            monitor.Log($"Event definition file scan unavailable: {error.Message}", LogLevel.Warn);
        }
    }

    private static string NormalizeJson(string json)
    {
        // Use the same permissive JSON dialect as SMAPI/Content Patcher (including single
        // quotes, multiline strings and unquoted numeric event keys), without a new package.
        using var reader = new Newtonsoft.Json.JsonTextReader(new StringReader(json))
        {
            DateParseHandling = Newtonsoft.Json.DateParseHandling.None
        };
        // Keep the parameterless call compatible with SMAPI's assembly method resolver.
        return Newtonsoft.Json.Linq.JToken.ReadFrom(reader, new Newtonsoft.Json.Linq.JsonLoadSettings
        {
            CommentHandling = Newtonsoft.Json.Linq.CommentHandling.Ignore
        }).ToString();
    }

    private static IReadOnlyDictionary<string, string>? ReadCompiledDefinitions(string directory, string relativePath)
    {
        using var content = new ContentManager(Game1.content.ServiceProvider, directory);
        return content.Load<Dictionary<string, string>>(Path.ChangeExtension(relativePath, null));
    }

    private void ReadGameDefinitions(string assetName)
    {
        string normalized = EventSourceScope.NormalizeAssetName(assetName);
        if (!normalized.StartsWith("Data/Events/", StringComparison.OrdinalIgnoreCase)
            || normalized.Split('/').Any(part => part is ".." or ".") || !gameAssets.Add(normalized)) return;
        // Use MonoGame's plain reader, not Game1.content/SMAPI, so an edited cache is never
        // mistaken for the original game definition. Store identities/hashes, not scripts.
        string path = Path.GetFullPath(Path.Combine(Constants.ContentPath, normalized + ".xnb"));
        string root = Path.GetFullPath(Constants.ContentPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(path)) return;
        try
        {
            using var content = new ContentManager(Game1.content.ServiceProvider, Constants.ContentPath);
            Dictionary<string, string> definitions = content.Load<Dictionary<string, string>>(normalized);
            foreach (var definition in definitions)
                index!.Add(new EventDefinitionCandidate(EventSourceActor.GameBase, normalized, definition.Key,
                    normalized + ".xnb", EventHashes.RootScript(definition.Value)));
        }
        catch (Exception error)
        {
            // Allow a later attempt if the game content reader wasn't available yet.
            gameAssets.Remove(normalized);
            monitor.Log($"Vanilla event definitions unavailable for {normalized}: {error.Message}", LogLevel.Debug);
        }
    }
}