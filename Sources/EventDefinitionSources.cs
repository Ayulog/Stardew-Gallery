using System.Diagnostics;
using Microsoft.Xna.Framework.Content;
using StardewModdingAPI;
using StardewValley;

namespace StardewGallery;

/// <summary>Indexes installed event definitions on demand, independently of runtime tracing.</summary>
internal sealed class EventDefinitionSources(IModHelper helper, IMonitor monitor)
{
    private EventDefinitionIndex? index;
    private ModProvenanceService? provenance;
    private readonly HashSet<string> gameAssets = new(StringComparer.OrdinalIgnoreCase);
    // The scanners keep each pack's results until a file it read changes, so asset invalidations don't rescan every pack.
    // SMAPI matches mod file paths case-insensitively by default on Linux and Android (UseCaseInsensitivePaths).
    private ContentPackEventScanner contentPacks = new(ReadCompiledDefinitions, NormalizeJson, IgnoreCase);
    private AliveNpcEventScanner aliveNpcs = new(NormalizeJson, IgnoreCase);
    private static bool IgnoreCase => !OperatingSystem.IsWindows();

    internal ModProvenanceService? Provenance
    {
        get
        {
            EnsureScanned();
            return provenance;
        }
    }

    internal string Status => index is null ? "Event definition files have not been scanned yet."
        : $"Event definition index: {index.Count} declarations; {gameAssets.Count} vanilla assets checked.";

    /// <param name="full">Also forget every pack's cached scan, as for an explicit rescan.</param>
    internal void Reset(bool full = false)
    {
        index = null;
        provenance = null;
        gameAssets.Clear();
        if (!full) return;
        contentPacks = new(ReadCompiledDefinitions, NormalizeJson, IgnoreCase);
        aliveNpcs = new(NormalizeJson, IgnoreCase);
    }

    internal EventOriginMatch ReadCharacter(string characterId)
    {
        EnsureScanned();
        ReadGameCharacters();
        return index!.MatchCharacter(characterId);
    }

    internal EventOriginMatch Read(string assetName, string eventId)
    {
        EnsureScanned();
        ReadGameDefinitions(assetName);
        // Stardew 1.6.15 merges Trailer definitions into Trailer_Big when no direct key exists.
        if (EventAssetName.Normalize(assetName).Equals("Data/Events/Trailer_Big", StringComparison.OrdinalIgnoreCase))
            ReadGameDefinitions("Data/Events/Trailer");
        return index!.Match(assetName, eventId);
    }

    internal EventOriginMatch ReadExactKey(string assetName, string rawKey)
    {
        EnsureScanned();
        ReadGameDefinitions(assetName);
        if (EventAssetName.Normalize(assetName).Equals("Data/Events/Trailer_Big", StringComparison.OrdinalIgnoreCase))
            ReadGameDefinitions("Data/Events/Trailer");
        return index!.MatchExactKey(assetName, rawKey);
    }

    internal void EnsureScanned()
    {
        if (index is not null) return;
        index = new EventDefinitionIndex();
        var clock = Stopwatch.StartNew();
        int packs = 0, files = 0, issues = 0;
        try
        {
            IModInfo[] allMods = helper.ModRegistry.GetAll().ToArray();
            IModInfo[] loaded = allMods.Where(mod =>
                string.Equals(mod.Manifest.ContentPackFor?.UniqueID, "Pathoschild.ContentPatcher", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mod.Manifest.ContentPackFor?.UniqueID, AliveNpcEventScanner.FrameworkId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(mod.Manifest.UniqueID, AliveNpcEventScanner.FrameworkId, StringComparison.OrdinalIgnoreCase)).ToArray();
            // The public registry supplies loaded identities, not directories. Find their manifests
            // under the launch-configured Mods root; never access SMAPI's private ModMetadata.
            string? root = ModDirectoryDiscovery.ResolveRoot(Constants.GamePath, helper.DirectoryPath,
                Environment.GetCommandLineArgs(), Environment.GetEnvironmentVariable("SMAPI_MODS_PATH"));
            ModDirectoryDiscoveryResult? discovered = null;
            if (root is not null)
            {
                var allIdentities = allMods.Select(mod => new ModDirectoryIdentity(mod.Manifest.UniqueID,
                    mod.Manifest.Version.ToString(), mod.Manifest.ContentPackFor?.UniqueID, mod.Manifest.EntryDll)).ToArray();
                discovered = ModDirectoryDiscovery.Discover(root, allIdentities, NormalizeJson);
                issues += discovered.Issues.Count;
                foreach (EventDefinitionScanIssue issue in discovered.Issues.Take(5))
                    monitor.Log($"Event mod directory scan {issue.RelativePath}: {issue.Reason}", LogLevel.Debug);
            }

            List<ModProvenanceManifest> provenanceManifests = new(allMods.Length);
            foreach (IModInfo mod in allMods)
            {
                string? contentPackFor = mod.Manifest.ContentPackFor?.UniqueID;
                var deps = mod.Manifest.Dependencies.Select(dependency => dependency.UniqueID).ToList();
                if (contentPackFor is not null) deps.Add(contentPackFor);
                var actor = new EventSourceActor(mod.Manifest.UniqueID, mod.Manifest.Name, mod.Manifest.Version.ToString());
                index.RegisterPack(new EventDefinitionPack(actor, string.Empty, deps));

                string? parentDirName = null;
                if (root is not null && discovered is not null && discovered.Directories.TryGetValue(mod.Manifest.UniqueID, out string? modDir))
                {
                    string rel = Path.GetRelativePath(root, modDir).Replace('\\', '/').TrimEnd('/');
                    string? parent = Path.GetDirectoryName(rel)?.Replace('\\', '/');
                    if (!string.IsNullOrWhiteSpace(parent) && parent != ".")
                        parentDirName = parent;
                }
                provenanceManifests.Add(new ModProvenanceManifest(
                    mod.Manifest.UniqueID,
                    mod.Manifest.Name,
                    mod.Manifest.Author,
                    contentPackFor,
                    mod.Manifest.Dependencies.Select(d => d.UniqueID).ToArray(),
                    parentDirName));
            }
            provenance = new ModProvenanceService(provenanceManifests);
            ReadGameCharacters();

            if (root is null || discovered is null)
            {
                monitor.Log("Event definition scan could not locate the configured Mods folder containing this mod. File sources remain unavailable.", LogLevel.Warn);
                return;
            }
            foreach (IModInfo mod in loaded)
            {
                var actor = new EventSourceActor(mod.Manifest.UniqueID, mod.Manifest.Name, mod.Manifest.Version.ToString());
                if (!discovered.Directories.TryGetValue(mod.Manifest.UniqueID, out string? directory))
                {
                    index.MarkIncomplete(actor);
                    continue;
                }
                try
                {
                    string? contentPackFor = mod.Manifest.ContentPackFor?.UniqueID;
                    var dependencies = mod.Manifest.Dependencies.Select(dependency => dependency.UniqueID).ToList();
                    if (contentPackFor is not null) dependencies.Add(contentPackFor);
                    var pack = new EventDefinitionPack(actor, directory, dependencies);
                    EventDefinitionScanResult result = string.Equals(contentPackFor, "Pathoschild.ContentPatcher", StringComparison.OrdinalIgnoreCase)
                        ? contentPacks.Scan(pack, index) : aliveNpcs.Scan(pack, index, contentPackFor);
                    packs++;
                    files += result.FilesRead;
                    issues += result.Issues.Count;
                    // A path Content Patcher can't load either hides nothing; anything else may.
                    if (result.Issues.Any(issue => issue.Kind != ScanIssueKind.Missing)) index.MarkIncomplete(actor);
                    // Unreadable files really hide this pack's events; the rest are expected limits of a static scan.
                    foreach (var group in result.Issues.GroupBy(issue => issue.Kind))
                        foreach (EventDefinitionScanIssue issue in group.Take(3))
                            monitor.Log($"Event definition scan [{actor.UniqueId}] {issue.RelativePath}: {issue.Reason}", group.Key switch
                            {
                                ScanIssueKind.Unreadable => LogLevel.Warn,
                                ScanIssueKind.Limitation => LogLevel.Debug,
                                _ => LogLevel.Trace
                            });
                }
                catch (Exception error)
                {
                    issues++;
                    index.MarkIncomplete(actor);
                    monitor.Log($"Event definition scan skipped {mod.Manifest.UniqueID}: {error}", LogLevel.Warn);
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
        Newtonsoft.Json.Linq.JToken token = Newtonsoft.Json.Linq.JToken.ReadFrom(reader, new Newtonsoft.Json.Linq.JsonLoadSettings
        {
            CommentHandling = Newtonsoft.Json.Linq.CommentHandling.Ignore
        });
        // Newtonsoft reads an empty array slot ("[a,,b]") as undefined, which strict JSON can't express; Content Patcher
        // sees no value there, so write null instead of losing the whole file.
        if (token is Newtonsoft.Json.Linq.JContainer container)
            foreach (Newtonsoft.Json.Linq.JValue value in container.Descendants().OfType<Newtonsoft.Json.Linq.JValue>()
                .Where(value => value.Type == Newtonsoft.Json.Linq.JTokenType.Undefined).ToArray())
                value.Replace(Newtonsoft.Json.Linq.JValue.CreateNull());
        return token.ToString();
    }

    private static IReadOnlyDictionary<string, string>? ReadCompiledDefinitions(string directory, string relativePath)
    {
        using var content = new ContentManager(Game1.content.ServiceProvider, directory);
        return content.Load<Dictionary<string, string>>(Path.ChangeExtension(relativePath, null));
    }

    private void ReadGameDefinitions(string assetName)
    {
        string normalized = EventAssetName.Normalize(assetName);
        if (EventDefinitionIndex.IsTriggerActionAsset(normalized))
        {
            ReadGameTriggerActions();
            return;
        }
        if (!normalized.StartsWith("Data/Events/", StringComparison.OrdinalIgnoreCase)
            || normalized.Split('/').Any(part => part is ".." or ".") || gameAssets.Contains(normalized)) return;
        // Resolve the actual on-disk casing (e.g. "Data/Events/beach" -> "Data/Events/Beach" on Linux/Steam Deck)
        // and never record a missing lookup in gameAssets, so a wrong-case miss can't block the real vanilla asset.
        if (PackFiles.ResolveContentAsset(Constants.ContentPath, normalized) is not { } actualAsset
            || !gameAssets.Add(actualAsset)) return;
        // Use MonoGame's plain reader, not Game1.content/SMAPI, so an edited cache is never
        // mistaken for the original game definition. Store identities/hashes, not scripts.
        try
        {
            using var content = new ContentManager(Game1.content.ServiceProvider, Constants.ContentPath);
            Dictionary<string, string> definitions = content.Load<Dictionary<string, string>>(actualAsset);
            foreach (var definition in definitions)
                index!.Add(new EventDefinitionCandidate(EventSourceActor.GameBase, actualAsset, definition.Key,
                    actualAsset + ".xnb", EventHashes.RootScript(definition.Value)));
        }
        catch (Exception error)
        {
            // Allow a later attempt if the game content reader wasn't available yet.
            gameAssets.Remove(actualAsset);
            monitor.Log($"Vanilla event definitions unavailable for {actualAsset}: {error.Message}", LogLevel.Debug);
        }
    }

    /// <summary>Index the original game's Data/TriggerActions rule IDs, read with the plain MonoGame reader like events.</summary>
    private void ReadGameTriggerActions()
    {
        const string asset = EventDefinitionIndex.TriggerActionAsset;
        if (gameAssets.Contains(asset)
            || PackFiles.ResolveContentAsset(Constants.ContentPath, asset) is not { } actualAsset
            || !gameAssets.Add(actualAsset)) return;
        try
        {
            using var content = new ContentManager(Game1.content.ServiceProvider, Constants.ContentPath);
            List<StardewValley.GameData.TriggerActionData> rules = content.Load<List<StardewValley.GameData.TriggerActionData>>(actualAsset);
            foreach (var rule in rules.Where(rule => !string.IsNullOrWhiteSpace(rule.Id)))
                index!.Add(new EventDefinitionCandidate(EventSourceActor.GameBase, asset, rule.Id, actualAsset + ".xnb",
                    EventHashes.RootScript(string.Join("\0", new[] { rule.Trigger, rule.Condition, rule.Action }
                        .Concat(rule.Actions ?? []).Select(value => value ?? string.Empty)))));
        }
        catch (Exception error)
        {
            gameAssets.Remove(actualAsset);
            monitor.Log($"Vanilla trigger action definitions unavailable: {error.Message}", LogLevel.Debug);
        }
    }

    /// <summary>Index the original game's Data/Characters keys, read with the plain MonoGame reader for Tier 1 Vanilla locking.</summary>
    private void ReadGameCharacters()
    {
        const string asset = EventDefinitionIndex.CharacterAsset;
        if (index is null || gameAssets.Contains(asset)
            || PackFiles.ResolveContentAsset(Constants.ContentPath, asset) is not { } actualAsset
            || !gameAssets.Add(actualAsset)) return;
        try
        {
            using var content = new ContentManager(Game1.content.ServiceProvider, Constants.ContentPath);
            Dictionary<string, StardewValley.GameData.Characters.CharacterData> characters =
                content.Load<Dictionary<string, StardewValley.GameData.Characters.CharacterData>>(actualAsset);
            foreach (string id in characters.Keys)
                index.AddCharacter(new CharacterDefinitionCandidate(EventSourceActor.GameBase, id, actualAsset + ".xnb"));
        }
        catch (Exception error)
        {
            gameAssets.Remove(actualAsset);
            monitor.Log($"Vanilla character definitions unavailable: {error.Message}", LogLevel.Debug);
        }
    }
}