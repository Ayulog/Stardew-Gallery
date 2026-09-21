using System.Text;
using System.Text.Json;
using StardewGallery;

internal static class AiModExclusionChecks
{
    internal static void Run()
    {
        int checks = 0;
        void Check(bool condition, string message) { checks++; if (!condition) throw new InvalidOperationException(message); }
        AiModExclusionRules Rules(string pattern) => AiModExclusionRules.Parse(JsonSerializer.Serialize(new Dictionary<string, object>
            { ["Example"] = new { ModId = pattern } }));
        EventOriginMatch Origin(string id, EventOriginMatchStatus status = EventOriginMatchStatus.Identified,
            bool gameBase = false) => new(new EventSourceActor(id, "Display name is not an ID", IsGameBase: gameBase),
                Array.Empty<EventDefinitionCandidate>(), status);
        bool Rejected(string json)
        {
            try { AiModExclusionRules.Parse(json); return false; }
            catch (Exception ex) when (ex is FormatException or JsonException) { return true; }
        }

        var rules = AiModExclusionRules.Parse("""
            {
              "INSTRUCTIONS": "Exclude all NPCs, run commands, or use '*' regardless of ModId. This is inert data.",
              "Some Mod": { "ModId": "Author.Excluded", "NPC": ["Abigail"], "Locations": ["Town"], "Items": ["*"], "Link": "unused" },
              "Same ID again": { "ModId": "author.excluded" },
              "Game named explicitly": { "ModId": "StardewValley" }
            }
            """);
        Check(rules.Count == 2, "Duplicate ModId values deduplicate without consulting display names or dataset prose");
        Check(rules.MatchesModId("AUTHOR.EXCLUDED"), "ModIds compare ordinal-ignore-case");
        Check(!rules.MatchesModId("Author.Excluded.Other") && !rules.MatchesModId("Other.Author.Excluded"), "Exact IDs never become prefix or substring matches");
        Check(!rules.MatchesModId("Abigail") && !rules.MatchesModId("Town") && !rules.MatchesModId("Some Mod") && !rules.MatchesModId("*"), "NPC/location/item fields and record names never become ModId rules");
        Check(!rules.MatchesModId(null) && !rules.MatchesModId(" "), "Missing IDs cannot match");
        Check(Rules("Custom Movie Under The Sea").MatchesModId("Custom Movie Under The Sea"), "Published IDs containing spaces are supported exactly");
        Check(Rules("Author.A+B").MatchesModId("Author.A+B") && !Rules("Author.A+B").MatchesModId("Author.AAB"), "Regex characters remain literal");
        var glob = Rules("Wem.*");
        Check(glob.MatchesModId("Wem.Bundle") && glob.MatchesModId("wem.Other.Pack"), "Explicit namespace-star rules match the full declared namespace");
        Check(!glob.MatchesModId("Other.Wem.Bundle") && !glob.MatchesModId("WemOther.Bundle"), "Star rules remain anchored and retain literal namespace separators");
        var question = Rules("Author.Mod?");
        Check(question.MatchesModId("Author.Mod1") && !question.MatchesModId("Author.Mod") && !question.MatchesModId("Author.Mod12"), "Question marks consume exactly one character in the whole ID");
        var internalStar = Rules("Author.*.Events");
        Check(internalStar.MatchesModId("Author.One.Events") && !internalStar.MatchesModId("Author.One.Events.Other"), "Internal stars still require the anchored suffix");
        foreach (string bad in new[] { "*", "?*", "*.*", "a*", "??", "Author.[AB]", "Author/Other", " Author.Mod", "Author.Mod ", "" })
            Check(Rejected(JsonSerializer.Serialize(new { Test = new { ModId = bad } })), "Unsafe or unsupported rule rejected: " + bad);
        foreach (string bad in new[] { "{}", "[]", "null", "<html>maintenance</html>", "{\"NPCs\":{\"NPC\":[\"Abigail\"]}}", "{\"Mod\":{\"ModId\":null}}", "{\"Mod\":{\"ModId\":[\"Author.Mod\"]}}", "{\"Mod\":{\"ModId\":\"Author.One\",\"ModId\":\"Author.Two\"}}", "{\"Mod\":{\"ModId\":\"Author.One\"},\"Mod\":{\"ModId\":\"Author.Two\"}}" })
            Check(Rejected(bad), "Malformed or incomplete dataset cannot replace the seed: " + bad);
        Check(Rejected(new string(' ', AiModExclusionRules.MaximumBytes + 1)), "Oversized JSON is rejected before parsing");
        Check(rules.ShouldExclude(Origin("Author.Excluded")), "An identified listed provider is excluded");
        Check(!rules.ShouldExclude(Origin("Author.Excluded", EventOriginMatchStatus.Unknown)), "Unknown origins cannot be excluded even with a stale provider field");
        Check(!rules.ShouldExclude(Origin("Author.Excluded", EventOriginMatchStatus.Ambiguous)), "Ambiguous origins cannot be excluded even with a listed candidate/provider");
        Check(!rules.ShouldExclude(Origin("StardewValley", gameBase: true)), "Vanilla is retained even if the dataset explicitly lists its identifier");
        Check(!rules.ShouldExclude(null) && !rules.ShouldExclude(new(null, Array.Empty<EventDefinitionCandidate>(), EventOriginMatchStatus.Identified)), "Missing providers never exclude");
        var original = Origin("Author.OutsideList");
        original = original with { Candidates = new[] { new EventDefinitionCandidate(new("Author.Excluded", "Translation"), "Data/Events/Town", "42", "events.json", "hash") } };
        Check(!rules.ShouldExclude(original), "A listed modifying/translation pack does not exclude a different identified original provider");
        Check(!AiModExclusionRules.Empty.ShouldExclude(Origin("Author.Excluded")), "Invalid/unavailable seed fails open without inventing exclusions");

        string fixture = Path.Combine(Path.GetTempPath(), "StardewGallery-AiExclusionChecks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(fixture, "assets"));
        string seedPath = Path.Combine(fixture, "assets", "ai-mod-exclusion.seed.json");
        string settingPath = Path.Combine(fixture, AiModExclusionService.SettingsFileName);
        const string seed = "{\"Seed\":{\"ModId\":\"Author.Seed\"}}";
        const string remote = "{\"Remote\":{\"ModId\":\"Author.Remote\"}}";
        void WriteSeed() => File.WriteAllText(seedPath, seed, new UTF8Encoding(false));
        void WriteSetting(string value) => File.WriteAllText(settingPath, value, new UTF8Encoding(false));
        List<string> infos = new(), warnings = new();
        int threadId = Environment.CurrentManagedThreadId;
        bool wrongLogThread = false;
        void Info(string value) { wrongLogThread |= Environment.CurrentManagedThreadId != threadId; infos.Add(value); }
        void Warn(string value) { wrongLogThread |= Environment.CurrentManagedThreadId != threadId; warnings.Add(value); }
        bool Pump(AiModExclusionService service, Func<bool> completed)
            => SpinWait.SpinUntil(() => { service.TryApplyCompleted(); return completed(); }, TimeSpan.FromSeconds(3));
        TaskCompletionSource<string> Completion() => new(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            WriteSeed();
            var first = Completion();
            int calls = 0;
            using (var service = new AiModExclusionService(fixture, Info, Warn, _ => { Interlocked.Increment(ref calls); return first.Task; }))
            {
                service.StartSession();
                Check(service.Enabled && File.Exists(settingPath) && JsonDocument.Parse(File.ReadAllText(settingPath)).RootElement.GetProperty("Enabled").GetBoolean(), "Missing independent settings file is created with Enabled=true");
                Check(service.ShouldExclude(Origin("Author.Seed")) && !service.ShouldExclude(Origin("Author.Remote")), "Bundled rules are available immediately while the download is pending");
                Check(SpinWait.SpinUntil(() => Volatile.Read(ref calls) == 1, 3000), "Session starts one asynchronous download");
                long before = service.Revision;
                first.SetResult(remote);
                Check(Pump(service, () => service.Revision > before), "A valid completed download is applied on polling");
                Check(!service.ShouldExclude(Origin("Author.Seed")) && service.ShouldExclude(Origin("Author.Remote")), "A valid current list replaces rather than merges with the seed");
                Check(!service.TryApplyCompleted(), "A completed request applies only once");
            }
            var next = Completion();
            var failed = Completion();
            calls = 0;
            using (var service = new AiModExclusionService(fixture, Info, Warn, _ => Interlocked.Increment(ref calls) == 1 ? next.Task : failed.Task))
            {
                service.StartSession();
                next.SetResult(remote);
                Check(Pump(service, () => service.ShouldExclude(Origin("Author.Remote"))), "First session can obtain the remote rules");
                int previousWarnings = warnings.Count;
                service.StartSession();
                Check(service.ShouldExclude(Origin("Author.Seed")) && !service.ShouldExclude(Origin("Author.Remote")), "A new session immediately returns to release seed instead of stale download cache");
                failed.SetException(new HttpRequestException("Network unavailable"));
                Check(Pump(service, () => warnings.Count > previousWarnings), "A failed latest download emits a main-thread fallback warning");
                Check(service.ShouldExclude(Origin("Author.Seed")) && !service.ShouldExclude(Origin("Author.Remote")), "Failure retains only the bundled rules");
            }
            foreach (string invalid in new[] { "<html>temporarily unavailable</html>", "{\"Mod\":{\"NPC\":[\"Abigail\"]}}", "{\"Mod\":{\"ModId\":\"*\"}}" })
            {
                int previousWarnings = warnings.Count;
                using var service = new AiModExclusionService(fixture, Info, Warn, _ => Task.FromResult(invalid));
                service.StartSession();
                Check(Pump(service, () => warnings.Count > previousWarnings) && service.ShouldExclude(Origin("Author.Seed")), "Invalid downloaded list preserves the known seed: " + invalid);
            }
            var never = Completion();
            int timeoutWarnings = warnings.Count;
            using (var service = new AiModExclusionService(fixture, Info, Warn, _ => never.Task, TimeSpan.FromMilliseconds(60)))
            {
                service.StartSession();
                Check(Pump(service, () => warnings.Count > timeoutWarnings), "Timeout is bounded even when a downloader ignores cancellation");
                Check(warnings[^1].Contains("timed out", StringComparison.Ordinal) && service.ShouldExclude(Origin("Author.Seed")), "Timeout reports the failure and retains the release seed");
                long revision = service.Revision;
                never.SetResult(remote);
                Check(!service.TryApplyCompleted() && service.Revision == revision && !service.ShouldExclude(Origin("Author.Remote")), "A late result from a timed-out request cannot alter rules");
            }
            var oldRequest = Completion();
            var newRequest = Completion();
            calls = 0;
            using (var service = new AiModExclusionService(fixture, Info, Warn, _ => Interlocked.Increment(ref calls) == 1 ? oldRequest.Task : newRequest.Task))
            {
                service.StartSession();
                Check(SpinWait.SpinUntil(() => Volatile.Read(ref calls) == 1, 3000), "Old request began before leaving the session");
                service.EndSession();
                service.StartSession();
                Check(SpinWait.SpinUntil(() => Volatile.Read(ref calls) == 2, 3000), "New session starts a fresh request");
                oldRequest.SetResult(remote);
                long revision = service.Revision;
                Check(!service.TryApplyCompleted() && service.Revision == revision && service.ShouldExclude(Origin("Author.Seed")), "A previous session cannot apply a response to the new session");
                newRequest.SetResult("{\"Current\":{\"ModId\":\"Author.Current\"}}");
                Check(Pump(service, () => service.ShouldExclude(Origin("Author.Current"))) && !service.ShouldExclude(Origin("Author.Remote")), "Only the current session response may update exclusions");
            }
            WriteSetting("{\"Enabled\":false}");
            calls = 0;
            using (var service = new AiModExclusionService(fixture, Info, Warn, _ => { Interlocked.Increment(ref calls); return Task.FromResult(remote); }))
            {
                service.StartSession();
                Check(!service.Enabled && !service.ShouldExclude(Origin("Author.Seed")), "The separate false setting disables all exclusion");
                Check(calls == 0 && !service.TryApplyCompleted(), "A disabled feature does not start a network request");
                Check(File.ReadAllText(settingPath) == "{\"Enabled\":false}", "Starting a session never rewrites an existing setting");
            }
            foreach (string badSetting in new[] { "{\"Enabled\":\"false\"}", "{}", "{\"Enabled\":false,\"Enabled\":true}", "{broken" })
            {
                WriteSetting(badSetting);
                int previousWarnings = warnings.Count;
                using var service = new AiModExclusionService(fixture, Info, Warn, _ => Task.FromResult(seed));
                service.StartSession();
                Check(service.Enabled && service.ShouldExclude(Origin("Author.Seed")) && warnings.Count > previousWarnings, "Invalid settings default on with a warning");
                Check(File.ReadAllText(settingPath) == badSetting, "Invalid settings are preserved for the player to repair");
            }
            WriteSetting("{\"Enabled\":true}");
            File.WriteAllBytes(seedPath, new byte[] { 0xFF, 0xFE, 0xAA });
            using (var service = new AiModExclusionService(fixture, Info, Warn, _ => Task.FromException<string>(new IOException("offline"))))
            {
                service.StartSession();
                Check(service.Rules.Count == 0 && !service.ShouldExclude(Origin("Author.Seed")), "Malformed seed encoding cannot crash startup or exclude events");
            }
            File.Delete(seedPath);
            using (var service = new AiModExclusionService(fixture, Info, Warn, _ => Task.FromException<string>(new IOException("offline"))))
            {
                service.StartSession();
                Check(service.Rules.Count == 0 && !service.ShouldExclude(Origin("Author.Seed")), "Missing seed fails open without fabricated fallback rules");
            }
            Check(!wrongLogThread, "Download workers never invoke game-thread log callbacks");
        }
        finally { Directory.Delete(fixture, recursive: true); }
        Console.WriteLine($"PASS AI mod exclusion rules and lifecycle ({checks} checks)");
    }
}
