using System.Text;
using StardewGallery;

internal static class EventDefinitionChecks
{
    internal static void Run()
    {
        int checks = 0;
        void Check(bool condition, string message) { checks++; if (!condition) throw new InvalidOperationException(message); }
        var original = new EventSourceActor("Fixture.Original", "Original", "1");
        var translation = new EventSourceActor("Fixture.Translation", "Translation", "1");
        var unrelated = new EventSourceActor("Fixture.Other", "Other", "1");
        EventDefinitionCandidate Candidate(EventSourceActor actor, string asset = "Data/Events/Town", string key = "42/f Abigail 500")
            => new(actor, asset, key, "events.json", EventHashes.RootScript("complete/script"));
        var index = new EventDefinitionIndex();
        index.Add(Candidate(original));
        Check(index.Match("data\\events\\town", "42").Provider == original, "Asset normalization and event ID match a definition");
        Check(index.Match("Data/Events/Beach", "42").Status == EventOriginMatchStatus.Unknown, "Same ID in another asset has no inherited provider");
        index.Add(Candidate(original, key: "42/f Abigail 1500"));
        Check(index.Match("Data/Events/Town", "42").Provider == original && index.Match("Data/Events/Town", "42").Candidates.Count == 2,
            "Changed conditions or script do not erase the original pack declaration");
        index.RegisterPack(new(translation, "unused", new[] { original.UniqueId }));
        index.Add(Candidate(translation));
        Check(index.Match("Data/Events/Town", "42").Provider == original, "An optional or required original-pack dependency identifies a translation override");
        var bridge = new EventSourceActor("Fixture.Bridge", "Bridge");
        index.RegisterPack(new(bridge, "unused", new[] { original.UniqueId }));
        index.RegisterPack(new(translation, "unused", new[] { bridge.UniqueId }));
        Check(index.Match("Data/Events/Town", "42").Provider == original, "Transitive declared dependency identifies the original candidate");
        index.Add(Candidate(unrelated));
        Check(index.Match("Data/Events/Town", "42").Ambiguous && index.Match("Data/Events/Town", "42").Provider is null,
            "Unrelated full definitions remain ambiguous instead of taking scan order");
        index.Add(Candidate(EventSourceActor.GameBase));
        Check(index.Match("Data/Events/Town", "42").Provider!.IsGameBase, "A vanilla definition has priority over later mod overrides");
        index.Add(Candidate(original, "Data/Events/Beach"));
        Check(index.Match("Data/Events/Beach", "42").Provider == original, "A separate mod event with the same ID retains its asset identity");
        var cyclic = new EventDefinitionIndex();
        cyclic.RegisterPack(new(original, "unused", new[] { translation.UniqueId }));
        cyclic.RegisterPack(new(translation, "unused", new[] { original.UniqueId }));
        cyclic.Add(Candidate(original)); cyclic.Add(Candidate(translation));
        Check(cyclic.Match("Data/Events/Town", "42").Ambiguous, "Circular dependencies cannot manufacture a unique origin");

        cyclic.Add(Candidate(unrelated));
        Check(cyclic.Match("Data/Events/Town", "42").Ambiguous, "A dependency cycle cannot eliminate its candidates and leave an unrelated pack falsely unique");

        var trailer = new EventDefinitionIndex();
        trailer.Add(Candidate(EventSourceActor.GameBase, "Data/Events/Trailer", "35/f Penny 1000"));
        trailer.Add(Candidate(EventSourceActor.GameBase, "Data/Events/Trailer", "36/f Penny 1500"));
        trailer.Add(Candidate(original, "Data/Events/Trailer", "37"));
        Check(trailer.Match("Data/Events/Trailer_Big", "35").Provider!.IsGameBase
            && trailer.Match("Data/Events/Trailer_Big", "36").Provider!.IsGameBase, "The two verified native trailer copies retain their source definitions");
        Check(trailer.Match("Data/Events/Trailer_Big", "35").Candidates.Single().AssetName == "Data/Events/Trailer", "Native copy evidence retains the actual original asset path");
        Check(trailer.Match("Data/Events/Trailer_Big", "37").Provider == original
            && trailer.Match("Data/Events/Trailer_Big", "38").Status == EventOriginMatchStatus.Unknown
            && trailer.Match("Data/Events/Town", "35").Status == EventOriginMatchStatus.Unknown, "Only the upgraded-trailer asset inherits declared Trailer IDs; unrelated locations and absent IDs stay unknown");
        trailer.Add(Candidate(original, "Data/Events/Trailer_Big", "35"));
        Check(trailer.Match("Data/Events/Trailer_Big", "35").Provider == original, "An explicitly declared upgraded-trailer definition takes precedence over the native copy fallback");

        string fixture = Path.Combine(Path.GetTempPath(), "StardewGallery-DefinitionChecks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixture);
        try
        {
            string packPath = Path.Combine(fixture, "pack");
            void Write(string relative, string text, bool bom = false)
            {
                string path = Path.Combine(packPath, relative.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, text, new UTF8Encoding(bom));
            }
            Write("content.json", """
                {
                  // Comments, trailing commas, and BOMs are allowed in real packs.
                  "ConfigSchema": { "Folder": { "Default": "wrong" }, "Map": { "Default": "Beach" } },
                  "DynamicTokens": [
                    { "Name": "Branch", "Value": "one", "When": { "Season": "spring" } },
                    { "Name": "Branch", "Value": "two", "When": { "Season": "summer" } }
                  ],
                  "Changes": [
                    { "Action": "Include", "FromFile": "data/start.json, data/start.json" },
                    { "Action": "Include", "FromFile": "{{Folder}}/{{Branch}}.json" },
                    { "Action": "Include", "FromFile": "bad.json, ../outside.json" },
                    { "Action": "EditData", "Target": "Data/Events/{{UnknownMap}}", "Entries": { "never": "script" } },
                    { "Action": "EditData", "Target": "Data/Events/{{Map}}", "Entries": { "{{ModId}}.Event/{{MissingCondition}}": "script" } },
                    { "Action": "EditData", "Target": "Data/Events/Town", "Entries": { "{{UnresolvedId}}/f A 1": "script", "gone": null } },
                    { "Action": "Load", "Target": "Data/Events/LoadMap", "FromFile": "events/{{TargetWithoutPath}}.json" },
                    { "Action": "EditData", "Target": "Data/Events/EditMap", "FromFile": "events/edit.json" },
                    { "Action": "Load", "Target": "Data/Events/Binary,Data/Events/BinaryCopy", "FromFile": "events/test.xnb" },
                  ],
                }
                """, true);
            Write("config.json", """{ "Folder": "branches" }""");
            Write("data/start.json", """
                { "Changes": [
                  { "Action": "Include", "FromFile": "data/deeper/child.json" },
                  { "Action": "EditData", "Target": "Data/Events/Town, Data/Events/Forest", "Entries": { "101/f Abigail {{UnknownValue}}": "full/script", "102": null, "103": { "Text": "not a script" } } },
                  { "Action": "EditData", "Target": "Data/Events/Town", "Fields": { "201": { "field": "replacement" } }, "TextOperations": [{ "Target": ["Entries", "202"], "Operation": "Append", "Value": "text" }] }
                ] }
                """);
            Write("data/deeper/child.json", """
                { "Changes": [
                  { "Action": "Include", "FromFile": "content.json" },
                  { "Action": "EditData", "Target": "Data/Events/Town", "Entries": { "104": "child" } }
                ] }
                """);
            Write("branches/one.json", """{ "Changes": [{ "Action": "EditData", "Target": "Data/Events/Town", "Entries": { "105": "branch one" } }] }""");
            Write("branches/two.json", """{ "Changes": [{ "Action": "EditData", "Target": "Data/Events/Town", "Entries": { "106": "branch two" } }] }""");
            Write("events/LoadMap.json", """{ "107/condition": "loaded script", "108": null }""");
            Write("events/edit.json", """{ "Entries": { "109": "imported edit" }, "Fields": { "110": "not full" } }""");
            Write("bad.json", "{ not valid JSON");
            Write("unused.json", """{ "Changes": [{ "Action": "EditData", "Target": "Data/Events/Town", "Entries": { "unused": "must not be found" } }] }""");
            Write("events/test.xnb", "placeholder for injected reader");
            File.WriteAllText(Path.Combine(fixture, "outside.json"), """{ "Changes": [{ "Action": "EditData", "Target": "Data/Events/Town", "Entries": { "escape": "must not be found" } }] }""");
            int binaryReads = 0;
            var scanner = new ContentPackEventScanner((directory, relative) =>
            {
                binaryReads++;
                Check(directory == packPath && relative == "events/test.xnb", "Compiled reader receives only a safe pack-relative event file");
                return new Dictionary<string, string> { ["111"] = "compiled/script" };
            });
            var scanned = new EventDefinitionIndex();
            var pack = new EventDefinitionPack(original, packPath, Array.Empty<string>());
            EventDefinitionScanResult result = scanner.Scan(pack, scanned);
            Check(scanned.Match("Data/Events/Town", "101").Provider == original, "A literal event ID survives unknown condition tokens");
            Check(scanned.Match("Data/Events/Forest", "101").Provider == original, "Comma-separated targets produce independent asset evidence");
            Check(scanned.Match("Data/Events/Town", "104").Provider == original, "Multi-level Include paths resolve from the content-pack root");
            Check(scanned.Match("Data/Events/Town", "105").Provider == original && scanned.Match("Data/Events/Town", "106").Provider == original,
                "All literal DynamicToken branches are followed without evaluating When");
            Check(scanned.Match("Data/Events/Beach", original.UniqueId + ".Event").Provider == original, "Config defaults and ModId resolve stable asset and ID tokens");
            Check(scanned.Match("Data/Events/LoadMap", "107").Provider == original, "Load FromFile resolves TargetWithoutPath");
            Check(scanned.Match("Data/Events/EditMap", "109").Provider == original, "EditData FromFile reads full Entries definitions");
            Check(scanned.Match("Data/Events/Binary", "111").Provider == original && scanned.Match("Data/Events/BinaryCopy", "111").Provider == original,
                "The injected binary dictionary reader supports compiled event Load declarations");
            Check(binaryReads == 1, "A repeated binary FromFile is read once per content pack");
            Check(scanned.Match("Data/Events/Town", "101").Candidates.Count == 1, "Repeated and cyclic Includes do not duplicate evidence");
            Check(scanned.Match("Data/Events/Town", "102").Status == EventOriginMatchStatus.Unknown
                && scanned.Match("Data/Events/Town", "103").Status == EventOriginMatchStatus.Unknown, "Deletions and partial object values are not event definitions");
            Check(scanned.Match("Data/Events/Town", "201").Status == EventOriginMatchStatus.Unknown
                && scanned.Match("Data/Events/Town", "202").Status == EventOriginMatchStatus.Unknown, "Fields and TextOperations do not make a pack the original author");
            Check(scanned.Match("Data/Events/Town", "unused").Status == EventOriginMatchStatus.Unknown, "Unreferenced JSON files are never indexed");
            Check(scanned.Match("Data/Events/Town", "escape").Status == EventOriginMatchStatus.Unknown, "An Include cannot escape the content-pack directory");
            Check(!scanned.AssetNames.Any(asset => asset.Contains("{{", StringComparison.Ordinal)), "Unknown target tokens never become wildcard assets");
            Check(result.Issues.Any(issue => issue.Reason == "JsonReaderException" || issue.Reason == "JsonException")
                && result.Issues.Any(issue => issue.Reason == "Path leaves the content pack"), "Malformed and unsafe files have explicit nonfatal scan issues");
            Check(result.DefinitionsAdded == scanned.Count && result.FilesRead == 10, "Statistics count distinct successfully opened files and declarations");
            EventDefinitionCandidate found = scanned.Match("Data/Events/Town", "104").Candidates.Single();
            Check(found.RelativePath == "data/deeper/child.json" && found.ScriptHash == EventHashes.RootScript("child"), "Evidence stores a relative path and hash, not retained script text");
            string multilinePath = Path.Combine(fixture, "multiline");
            Directory.CreateDirectory(multilinePath);
            string multilineJson = """
                {
                  /* Comment with unmatched " and a literal tab <TAB>
                     and another comment line; these are not string content. */
                  "Changes": [{ "Action": "EditData", "Target": "Data/Events/Town", "Entries": {
                    "301": "literal first
                second<TAB>third",
                    // A quoted " fragment in this comment must not affect the following string.
                    "302": "escaped first\nsecond\tthird",
                    "303": "quoted \"text\" with // and /* markers
                continues",
                    /* Comment after normalization begins, with an unmatched "
                       and a literal tab <TAB> */
                    "304": "backslash \\n remains literal",
                    "305": "carriage<CR>return",
                    "306": "control<CONTROL>character"
                  } }]
                }
                """.Replace("<TAB>", "\t").Replace("<CR>", "\r").Replace("<CONTROL>", "\u0001");
            File.WriteAllText(Path.Combine(multilinePath, "content.json"), multilineJson);
            var multiline = new EventDefinitionIndex();
            EventDefinitionScanResult multilineResult = new ContentPackEventScanner().Scan(new(original, multilinePath, Array.Empty<string>()), multiline);
            Check(multilineResult.Issues.Count == 0 && multiline.Count == 6, "CP literal multiline strings parse while comment quotes remain ignored");
            string Hash(string id) => multiline.Match("Data/Events/Town", id).Candidates.Single().ScriptHash;
            string nativeLine = multilineJson.Contains("first\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            Check(Hash("301") == EventHashes.RootScript("literal first" + nativeLine + "second\tthird"), "Literal newlines and tabs preserve their decoded script content");
            Check(Hash("302") == EventHashes.RootScript("escaped first\nsecond\tthird"), "Already escaped newlines and tabs are not double escaped");
            Check(Hash("303") == EventHashes.RootScript("quoted \"text\" with // and /* markers" + nativeLine + "continues"), "Escaped quotes and comment-like text inside strings retain their meaning");
            Check(Hash("304") == EventHashes.RootScript("backslash \\n remains literal"), "A backslash escaped in JSON does not turn a following n into a line break");
            Check(Hash("305") == EventHashes.RootScript("carriage\rreturn") && Hash("306") == EventHashes.RootScript("control\u0001character"), "Literal carriage returns and other JSON control characters are normalized without losing content");
            string binaryFailurePath = Path.Combine(fixture, "binary-failure");
            Directory.CreateDirectory(binaryFailurePath);
            File.WriteAllText(Path.Combine(binaryFailurePath, "bad.xnb"), "invalid dictionary");
            File.WriteAllText(Path.Combine(binaryFailurePath, "content.json"), """
                { "Changes": [
                  { "Action": "EditData", "Target": "Data/Events/Town", "Entries": { "401": "before" } },
                  { "Action": "Load", "Target": "Data/Events/Town", "FromFile": "bad.xnb" },
                  { "Action": "Load", "Target": "Characters/{{UnknownNpc}}", "FromFile": "not-an-event.xnb" },
                  { "Action": "EditData", "Target": "Data/Events/Town", "Entries": { "402": "after" } }
                ] }
                """);
            var binaryFailureIndex = new EventDefinitionIndex();
            EventDefinitionScanResult binaryFailure = new ContentPackEventScanner((_, _) => throw new FormatException("reader-specific exception"))
                .Scan(new(original, binaryFailurePath, Array.Empty<string>()), binaryFailureIndex);
            Check(binaryFailureIndex.Match("Data/Events/Town", "401").Provider == original
                && binaryFailureIndex.Match("Data/Events/Town", "402").Provider == original, "An injected binary-reader exception cannot discard preceding or following definitions");
            Check(binaryFailure.Issues.Count == 1 && binaryFailure.Issues[0].Reason == nameof(FormatException),
                "Binary-reader failures are isolated while obviously unrelated dynamic targets create no scan warning");
            string normalizationPath = Path.Combine(fixture, "normalization");
            Directory.CreateDirectory(normalizationPath);
            File.WriteAllText(Path.Combine(normalizationPath, "content.json"), """
                { "Changes": [
                  { "Action": "Include", "FromFile": "dialect.json, failing.json" },
                  { "Action": "EditData", "Target": "Data/Events/Town", "Entries": { "502": "survives" } }
                ] }
                """);
            File.WriteAllText(Path.Combine(normalizationPath, "dialect.json"), "DIALECT_FIXTURE");
            File.WriteAllText(Path.Combine(normalizationPath, "failing.json"), "FAILING_FIXTURE");
            int normalizationCalls = 0;
            var normalizingScanner = new ContentPackEventScanner(normalizeJson: text =>
            {
                normalizationCalls++;
                if (text == "FAILING_FIXTURE") throw new FormatException("injected dialect parse failure");
                return text == "DIALECT_FIXTURE"
                    ? """{ "Changes": [{ "Action": "EditData", "Target": "Data/Events/Town", "Entries": { "501": "normalized" } }] }"""
                    : text;
            });
            var normalizedIndex = new EventDefinitionIndex();
            var normalizedPack = new EventDefinitionPack(original, normalizationPath, Array.Empty<string>());
            EventDefinitionScanResult normalizationResult = normalizingScanner.Scan(normalizedPack, normalizedIndex);
            Check(normalizedIndex.Match("Data/Events/Town", "501").Provider == original && normalizedIndex.Match("Data/Events/Town", "502").Provider == original,
                "An injected JSON dialect normalizer supplies definitions and isolates a failing include");
            Check(normalizationResult.Issues.Count == 1 && normalizationResult.Issues[0].RelativePath == "failing.json"
                && normalizationResult.Issues[0].Reason == nameof(FormatException), "Dialect parser errors identify only the failing file");
            normalizingScanner.Scan(normalizedPack, normalizedIndex);
            Check(normalizationCalls == 3, "Each file is normalized once and subsequent scans reuse hashed declarations");
            string alivePath = Path.Combine(fixture, "alive");
            string aliveScenes = Path.Combine(alivePath, "assets", "scene_events");
            Directory.CreateDirectory(Path.Combine(aliveScenes, "experimental"));
            string Scene(string id, string location, bool enabled = true) => System.Text.Json.JsonSerializer.Serialize(new
            {
                schemaVersion = 1, id = "fixture." + id, enabled, location, eventKey = id,
                scene = new { scriptKey = "scene.script." + id, script = "scene/script/" + id }
            });
            File.WriteAllText(Path.Combine(aliveScenes, "first.json"), Scene("601", "Town"));
            File.WriteAllText(Path.Combine(aliveScenes, "experimental", "disabled.json"), Scene("602", "Farm", enabled: false));
            File.WriteAllText(Path.Combine(aliveScenes, "reference.json"), """{ "schemaVersion": 1, "id": "fixture.reference", "location": "Town", "eventKey": "607", "scene": { "script": "", "scriptKey": "fixture.reference.script" } }""");
            File.WriteAllText(Path.Combine(aliveScenes, "invalid.json"), "not JSON");
            File.WriteAllText(Path.Combine(aliveScenes, "unsupported.json"), """{ "schemaVersion": "future", "location": "Town", "eventKey": "603", "scene": { "script": "not supported" } }""");
            File.WriteAllText(Path.Combine(aliveScenes, "partial.json"), """{ "schemaVersion": 1, "location": "Town", "eventKey": "604", "scene": { "branches": { "changed": "partial" } } }""");
            File.WriteAllText(Path.Combine(alivePath, "unrelated.json"), Scene("605", "Town"));
            var aliveActor = new EventSourceActor(AliveNpcEventScanner.FrameworkId, "AliveNpcs", "1.6.2");
            var alivePack = new EventDefinitionPack(aliveActor, alivePath, Array.Empty<string>());
            var aliveIndex = new EventDefinitionIndex();
            var aliveScanner = new AliveNpcEventScanner();
            EventDefinitionScanResult aliveResult = aliveScanner.Scan(alivePack, aliveIndex);
            Check(aliveIndex.Match("Data/Events/Town", "601").Provider == aliveActor, "The supported AliveNpcs schema maps its location and eventKey to an event definition");
            Check(aliveIndex.Match("Data/Events/Farm", "602").Provider == aliveActor, "Nested and disabled AliveNpcs declarations remain static source candidates");
            Check(aliveIndex.Match("Data/Events/Town", "603").Status == EventOriginMatchStatus.Unknown
                && aliveIndex.Match("Data/Events/Town", "604").Status == EventOriginMatchStatus.Unknown
                && aliveIndex.Match("Data/Events/Town", "605").Status == EventOriginMatchStatus.Unknown,
                "Unsupported schemas, partial scenes and JSON outside the framework scene folder are excluded");
            Check(aliveResult.DefinitionsAdded == 3 && aliveResult.Issues.Count == 1, "Malformed AliveNpcs scene files do not block valid declarations");
            Check(aliveIndex.Match("Data/Events/Farm", "602").Candidates.Single().RelativePath == "assets/scene_events/experimental/disabled.json",
                "AliveNpcs evidence retains the nested source file path");
            Check(aliveScanner.Scan(new(unrelated, alivePath, Array.Empty<string>()), new()).DefinitionsAdded == 0,
                "An unrelated C# mod cannot be attributed using the AliveNpcs schema");
            EventDefinitionCandidate reference = aliveIndex.Match("Data/Events/Town", "607").Candidates.Single();
            Check(reference.ScriptHash.Length == 0 && reference.ScriptReference == "fixture.reference.script", "A localized AliveNpcs declaration preserves its reference without inventing a resolved script hash");
            string ownedPath = Path.Combine(fixture, "alive-owned");
            Directory.CreateDirectory(Path.Combine(ownedPath, "scenes", "mayor"));
            File.WriteAllText(Path.Combine(ownedPath, "scenes", "mayor", "intro.json"), Scene("606", "Farm"));
            File.WriteAllText(Path.Combine(ownedPath, "alive-npcs-ec.json"), """
                { "schemaVersion": 1, "packageId": "Fixture.AliveOwned", "modes": [
                  { "id": "mayor", "sceneEventDirectories": ["scenes/mayor", "scenes/mayor", "../outside-scenes"] }
                ] }
                """);
            Directory.CreateDirectory(Path.Combine(ownedPath, "scenes", "unused"));
            File.WriteAllText(Path.Combine(ownedPath, "scenes", "unused", "not-loaded.json"), Scene("608", "Town"));
            var ownedActor = new EventSourceActor("Fixture.AliveOwned", "Owned scene pack");
            var ownedPack = new EventDefinitionPack(ownedActor, ownedPath, new[] { AliveNpcEventScanner.FrameworkId });
            Check(aliveScanner.Scan(ownedPack, aliveIndex, AliveNpcEventScanner.FrameworkId).DefinitionsAdded == 1
                && aliveIndex.Match("Data/Events/Farm", "606").Provider == ownedActor, "A declared AliveNpcs content pack retains its own identity for scenes under its confirmed root");
            Check(aliveIndex.Match("Data/Events/Town", "608").Status == EventOriginMatchStatus.Unknown
                && aliveScanner.Scan(ownedPack, aliveIndex, AliveNpcEventScanner.FrameworkId).Issues.Any(issue => issue.Reason == "Path leaves the content pack"),
                "AliveNpcs content packs follow only declared scene directories and reject directory escapes");
            Check(aliveScanner.Scan(ownedPack, new(), "Unrelated.Framework").DefinitionsAdded == 0,
                "Owned scene discovery requires the exact ContentPackFor framework ID");
            File.WriteAllText(Path.Combine(aliveScenes, "first.json"), "changed after scan");
            Check(ReferenceEquals(aliveScanner.Scan(alivePack, aliveIndex), aliveResult), "AliveNpcs scans are cached without retaining scene scripts");
            Write("content.json", "not JSON any longer");
            var second = new EventDefinitionIndex();
            Check(ReferenceEquals(scanner.Scan(pack, second), result) && second.Count == scanned.Count && binaryReads == 1,
                "Subsequent lookups reuse a one-time file scan and can populate a fresh index");
            string brokenPath = Path.Combine(fixture, "broken");
            Directory.CreateDirectory(brokenPath);
            File.WriteAllText(Path.Combine(brokenPath, "content.json"), "invalid");
            EventDefinitionScanResult broken = scanner.Scan(new(unrelated, brokenPath, Array.Empty<string>()), scanned);
            Check(broken.DefinitionsAdded == 0 && broken.Issues.Count > 0 && scanned.Match("Data/Events/Town", "104").Provider == original,
                "A broken pack cannot discard definitions from another pack");
        }
        finally { Directory.Delete(fixture, recursive: true); }
        Console.WriteLine($"Event definition checks passed: {checks}");
    }
}
