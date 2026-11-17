using System.Text.Json;
using StardewGallery;

/// <summary>
/// Pure in-memory checks for Data/TriggerActions providers and branch sub-script sources. Never writes or deletes
/// files: content-pack scans read only the repository's static fixtures under Checks/Fixtures/SourceStage1.
/// </summary>
internal static class SourceStage1Checks
{
    private static int checks;

    internal static void Run()
    {
        checks = 0;
        Declarations();
        IndexRules();
        ScanFixtures();
        PrerequisiteSources();
        RulePresentation();
        BranchFragments();
        BranchPresentation();
        Console.WriteLine($"Source stage 1 checks passed: {checks}");
    }

    private static void Declarations()
    {
        IReadOnlyList<TriggerActionDeclaration> Edit(string json, List<string>? problems = null)
        {
            using JsonDocument document = JsonDocument.Parse(json);
            return TriggerActionDeclarations.ReadEdit(document.RootElement, problems);
        }
        var entries = Edit("""{ "Entries": { "A": { "Trigger": "DayStarted" }, "#0": { "Id": "A2", "Trigger": "DayEnding" }, "Gone": null, "Bad": "text" } }""");
        Check(entries.Select(e => (e.RawId, e.IsModification)).SequenceEqual([("A", false), ("A2", false)]),
            "Entries objects accept both key-as-Id and object-Id forms; null deletes and scalar values are ignored");
        var emptyTargetField = Edit("""{ "TargetField": [], "Entries": { "RootRule": { "Id": "RootRule" } } }""");
        Check(emptyTargetField.Single() is { RawId: "RootRule", IsModification: false },
            "An empty TargetField targets the root list and still reads top-level declarations");
        var fieldsAndMoves = Edit("""{ "Fields": { "B": { "Condition": "TRUE" } }, "MoveEntries": [ { "ID": "C", "ToPosition": "Top" } ] }""");
        Check(fieldsAndMoves.Select(e => (e.RawId, e.IsModification)).SequenceEqual([("B", true), ("C", true)]),
            "Fields and MoveEntries only modify or reorder existing rules, never declaring original providers");
        var targetField = Edit("""{ "TargetField": [ "D", "Actions" ], "Entries": { "#0": "AddMail Current X" }, "Fields": { "E": {} } }""");
        Check(targetField.Count == 1 && targetField[0] is { RawId: "D", IsModification: true }, "TargetField edits inside one existing rule; its Entries are not rule declarations");
        var text = Edit("""{ "TextOperations": [ { "Operation": "Append", "Target": [ "Entries", "F", "Condition" ] }, { "Target": [ "Other" ] } ] }""");
        Check(text.Count == 1 && text[0] is { RawId: "F", IsModification: true }, "TextOperations on an entry are modifications");
        List<string> problems = [];
        var list = Edit("""{ "Entries": [ { "Id": "G" }, { "Trigger": "DayStarted" } ] }""", problems);
        Check(list.Single().RawId == "G" && problems.Count == 1, "A list of entries is keyed by Id; an entry without a static Id is reported");
        problems.Clear();
        Edit("""{ "Entries": { "#0": { "Trigger": "DayStarted" } } }""", problems);
        Check(problems.Count == 1, "An index-keyed object entry without an inner Id is reported as a scan limitation");
        using JsonDocument load = JsonDocument.Parse("""[ { "Id": "H", "Trigger": "DayStarted" }, { "id": "I" } ]""");
        Check(TriggerActionDeclarations.ReadLoad(load.RootElement).Select(value => value.RawId).SequenceEqual(["H", "I"]),
            "A Load list declares each entry by its (case-insensitive) Id field");
        using JsonDocument editMapFile = JsonDocument.Parse("""{ "MapRule": { "Id": "MapRule", "Trigger": "DayStarted" }, "#0": { "Id": "Indexed" }, "Gone": null }""");
        Check(TriggerActionDeclarations.ReadEditFile(editMapFile.RootElement).Select(value => (value.RawId, value.IsModification))
            .SequenceEqual([("MapRule", false), ("Indexed", false)]),
            "An EditData FromFile entry map declares rules directly without an outer Entries wrapper");
        using JsonDocument editListFile = JsonDocument.Parse("""[ { "Id": "ListRule", "Trigger": "DayStarted" } ]""");
        Check(TriggerActionDeclarations.ReadEditFile(editListFile.RootElement).Single() is { RawId: "ListRule", IsModification: false },
            "An EditData FromFile entry list declares rules directly");
        using JsonDocument wrappedEditFile = JsonDocument.Parse("""{ "Entries": { "Wrapped": { "Id": "Wrapped" } }, "Fields": { "Edited": {} } }""");
        Check(TriggerActionDeclarations.ReadEditFile(wrappedEditFile.RootElement).Select(value => (value.RawId, value.IsModification))
            .SequenceEqual([("Wrapped", false), ("Edited", true)]),
            "Patch-shaped EditData FromFile wrappers are still tolerated");
        problems.Clear();
        Edit("""{ "TargetField": "D" }""", problems);
        Check(problems.Count == 1, "A TargetField that names no entry is reported as a scan limitation");
    }

    private static void IndexRules()
    {
        Check(EventDefinitionIndex.IsIndexedAsset("Data\\TriggerActions") && EventDefinitionIndex.IsIndexedAsset("Data/Events/Town")
            && !EventDefinitionIndex.IsIndexedAsset("Data/Mail") && !EventDefinitionIndex.IsEventAsset("Data/TriggerActions"),
            "The index accepts Data/TriggerActions beside event assets; event-only checks stay unchanged");
        Check(EventDefinitionIndex.GetEntryId("Data/TriggerActions", " Mod/Rule ") == "Mod/Rule"
            && EventDefinitionIndex.GetEntryId("Data/Events/Town", "42/f Abigail 500") == "42", "Rule IDs are kept whole; event keys still use their first entry");
        var original = new EventSourceActor("Fixture.Rules", "Rules");
        var editor = new EventSourceActor("Fixture.Editor", "Editor");
        var overrider = new EventSourceActor("Fixture.Override", "Override");
        var index = new EventDefinitionIndex();
        index.RegisterPack(new(original, "rules", []));
        index.RegisterPack(new(editor, "editor", []));
        index.RegisterPack(new(overrider, "override", ["Fixture.Rules"]));
        EventDefinitionCandidate Rule(EventSourceActor actor, string id, bool edit = false)
            => new(actor, "Data/TriggerActions", id, "content.json", EventHashes.RootScript(id)) { IsModification = edit };
        index.Add(Rule(original, "Rule.A"));
        index.Add(Rule(editor, "Rule.A", edit: true));
        index.Add(Rule(overrider, "rule.a"));
        index.Add(Rule(editor, "Rule.B", edit: true));
        index.Add(new(original, "Data/Events/Town", "Rule.A/f Abigail 500", "content.json", "hash"));
        EventOriginMatch a = index.Match("Data/TriggerActions", "rule.a");
        Check(a.Status == EventOriginMatchStatus.Identified && a.Provider == original && a.Candidates.Count == 3,
            "Case-different overrides and unrelated editors group with the original rule declaration without stealing authorship");
        EventOriginMatch b = index.Match("Data/TriggerActions", "Rule.B");
        Check(b.Status == EventOriginMatchStatus.Unknown && b.Provider is null && b.Candidates.Single().IsModification,
            "An edit without any declaration leaves the provider unknown");
        Check(index.Match("Data/Events/Town", "Rule.A").Candidates.Single().AssetName == "Data/Events/Town"
            && index.Match("Data/Events/Town", "rule.a").Status == EventOriginMatchStatus.Unknown,
            "Asset plus ID identity keeps events and rules apart; event IDs stay case-sensitive");
        index.Add(new(EventSourceActor.GameBase, "Data/TriggerActions", "Rule.B", "Data/TriggerActions.xnb", "hash"));
        Check(index.Match("Data/TriggerActions", "Rule.B").Provider!.IsGameBase, "A vanilla rule declaration names the base game");

        var incompleteIndex = new EventDefinitionIndex();
        incompleteIndex.RegisterPack(new(original, "rules", []));
        incompleteIndex.RegisterPack(new(overrider, "override", ["Fixture.Rules"]));
        incompleteIndex.MarkIncomplete(original);
        incompleteIndex.Add(Rule(overrider, "Rule.MissingInBase"));
        EventOriginMatch degraded = incompleteIndex.Match("Data/TriggerActions", "Rule.MissingInBase");
        Check(degraded.Status == EventOriginMatchStatus.Incomplete && degraded.Provider == overrider
            && degraded.UnreadDependencies.Single() == original,
            "An override whose depended-on pack was not fully read degrades to Incomplete rather than confirmed");
    }

    private static void ScanFixtures()
    {
        string root = FixtureRoot();
        var rulesActor = new EventSourceActor("Fixture.Rules", "Rules Pack", "1.0.0");
        var overrideActor = new EventSourceActor("Fixture.Override", "Override Pack", "1.0.0");
        var independentActor = new EventSourceActor("Fixture.Independent", "Independent Pack", "1.0.0");
        var index = new EventDefinitionIndex();
        var scanner = new ContentPackEventScanner();
        EventDefinitionScanResult rules = scanner.Scan(new(rulesActor, Path.Combine(root, "RulesPack"), ["Pathoschild.ContentPatcher"]), index);
        EventDefinitionScanResult overrides = scanner.Scan(new(overrideActor, Path.Combine(root, "OverridePack"), ["Pathoschild.ContentPatcher", "Fixture.Rules"]), index);
        EventDefinitionScanResult independent = scanner.Scan(new(independentActor, Path.Combine(root, "IndependentPack"), ["Pathoschild.ContentPatcher"]), index);
        Check(rules.Issues.Count == 0 && overrides.Issues.Count == 0,
            "Static rule declarations (and dynamic-token Fields edits) produce no incomplete issues: " + string.Join("; ", rules.Issues.Concat(overrides.Issues).Select(issue => issue.Reason)));
        Check(independent.Issues.Any(issue => issue.Reason == "Unresolved dynamic token in trigger action ID"),
            "A rule declaration ID with an unresolved token is reported instead of silently dropped");
        index.Add(new(EventSourceActor.GameBase, "Data/TriggerActions", "Vanilla_Rule", "Data/TriggerActions.xnb", "hash"));

        EventOriginMatch marker = index.Match("Data/TriggerActions", "Fixture.Rules_SetMarker");
        Check(marker.Status == EventOriginMatchStatus.Identified && marker.Provider == rulesActor,
            "{{ModId}} expands in rule IDs and an override that depends on the original never becomes its provider");
        Check(marker.Candidates.Any(candidate => candidate.Actor == overrideActor && !candidate.IsModification)
            && marker.Candidates.Any(candidate => candidate.Actor == overrideActor && candidate.IsModification),
            "The override is listed both as another declarer and as an editor");
        EventOriginMatch shared = index.Match("Data/TriggerActions", "Shared_Rule");
        Check(shared.Status == EventOriginMatchStatus.Ambiguous && shared.Provider is null
            && shared.Candidates.Any(candidate => candidate.Actor == rulesActor && candidate.RelativePath == "data/more.json")
            && shared.Candidates.Any(candidate => candidate.Actor == independentActor && candidate.RelativePath == "data/rules.json"),
            "Included EditData files and Load lists declare rules; independent declarers stay ambiguous");
        Check(shared.Candidates.Any(candidate => candidate.Actor == overrideActor && candidate.IsModification)
            && !shared.Candidates.Any(candidate => candidate.Actor == overrideActor && !candidate.IsModification),
            "TargetField edits never count as a declaration");
        EventOriginMatch vanilla = index.Match("Data/TriggerActions", "Vanilla_Rule");
        Check(vanilla.Provider!.IsGameBase && vanilla.Candidates.Count(candidate => candidate.Actor == rulesActor && candidate.IsModification) == 2,
            "Fields and MoveEntries on a vanilla rule are recorded as modifications while keeping the base game as provider");
        Check(index.Match("Data/TriggerActions", "Fixture.Rules_Deleted").Status == EventOriginMatchStatus.Unknown,
            "A null entry deletes rather than declares");
        Check(index.Match("Data/TriggerActions", "Independent_Rule").Provider == independentActor, "A Load list names its own rules' provider");
        Check(index.Match("Data/TriggerActions", "FromFile_MapRule").Provider == rulesActor
            && index.Match("Data/TriggerActions", "FromFile_IndexedRule").Provider == rulesActor
            && index.Match("Data/TriggerActions", "FromFile_ListRule").Provider == rulesActor,
            "EditData FromFile map/list declarations name the original pack rather than an inline override pack");
        Check(index.Match("Data/TriggerActions", "FromFile_Removed").Status == EventOriginMatchStatus.Unknown
            && index.Match("Data/TriggerActions", "Subfield_NotARootRule").Status == EventOriginMatchStatus.Unknown,
            "Deleted FromFile entries and TargetField-scoped FromFile subfields never declare root rules");
        Check(index.Match("Data/Events/Town", "910001").Provider == rulesActor, "Event declarations in the same pack are unaffected");

        // Exact dictionary-key lookup for fork/switchEvent branches vs first-segment EventId lookup for root events.
        EventOriginMatch branchA = index.MatchExactKey("Data/Events/Town", "Fixture.Rules/BranchA");
        EventOriginMatch branchB = index.MatchExactKey("Data/Events/Town", "Fixture.Rules/BranchB");
        Check(branchA.Provider == rulesActor && branchA.Candidates.Single().RawEventKey == "Fixture.Rules/BranchA"
            && branchB.Provider == rulesActor && branchB.Candidates.Single().RawEventKey == "Fixture.Rules/BranchB",
            "Namespaced branch keys containing '/' match by exact dictionary key without colliding on their prefix");
        Check(index.MatchExactKey("Data/Events/Town", "-1/HiddenBranch").Provider == rulesActor
            && index.Match("Data/Events/Town", "-1").Status == EventOriginMatchStatus.Unknown,
            "A '-1/...' key cannot play as a root event but remains indexable as an exact branch target");
        Check(index.Match("Data/Events/Town", "sharedKey").Status == EventOriginMatchStatus.Ambiguous
            && index.MatchExactKey("Data/Events/Town", "sharedKey").Provider == independentActor
            && index.MatchExactKey("Data/Events/Town", "sharedKey").Candidates.Single().RawEventKey == "sharedKey",
            "A root event with preconditions ('sharedKey/f ...') never pollutes exact branch key lookup ('sharedKey')");

        // Trailer_Big -> Trailer fallback and Incomplete propagation also hold for exact branch keys.
        var trailerIndex = new EventDefinitionIndex();
        trailerIndex.RegisterPack(new(rulesActor, "rules", []));
        trailerIndex.RegisterPack(new(overrideActor, "override", ["Fixture.Rules"]));
        trailerIndex.Add(new(rulesActor, "Data/Events/Trailer", "trailerBranch", "events.json", "hash"));
        Check(trailerIndex.MatchExactKey("Data/Events/Trailer_Big", "trailerBranch").Provider == rulesActor,
            "Exact branch lookup preserves the Trailer -> Trailer_Big fallback");
        trailerIndex.MarkIncomplete(rulesActor);
        trailerIndex.Add(new(overrideActor, "Data/Events/Town", "incompleteBranch", "events.json", "hash"));
        Check(trailerIndex.MatchExactKey("Data/Events/Town", "incompleteBranch").Status == EventOriginMatchStatus.Incomplete,
            "Exact branch lookup degrades to Incomplete when a depended-on pack was not fully read");

        // Case-insensitive Content asset resolution for Linux / Steam Deck lowercase changeLocation targets.
        string rulesRoot = Path.Combine(root, "RulesPack");
        Check(PackFiles.ResolveContentAsset(rulesRoot, "DATA/more", ".json") == "data/more"
            && PackFiles.ResolveContentAsset(rulesRoot, "DATA/FROMFILE-MAP", ".json") == "data/fromfile-map"
            && PackFiles.ResolveContentAsset(rulesRoot, "data/missing", ".json") is null
            && PackFiles.ResolveContentAsset(rulesRoot, "../OverridePack/content", ".json") is null,
            "Content asset resolution returns actual on-disk casing, null for missing files, and rejects '..' traversal");
    }

    private static void PrerequisiteSources()
    {
        var a = new EventSourceActor("Fixture.A", "Pack A");
        var b = new EventSourceActor("Fixture.B", "Pack B");
        MarkerAction Marker(string rule) => new(rule, "Current", true, ["DayStarted"], "", false, true, []);
        GalleryEvent Story(string id) => new(new(new("Data/Events/Town", id), "Town", id + "/f Abigail 500", "scene", new([], []), "d", "s"),
            new(OwnershipKind.Direct, [new("Abigail", 500)])) { RelatedNpcNames = ["Abigail"] };
        var single = new PrerequisiteEvent("p1", [Marker("A.1"), Marker("A.2")], [], []);
        var mixed = new PrerequisiteEvent("p2", [Marker("A.1"), Marker("B.1")], [], []);
        var partial = new PrerequisiteEvent("p3", [Marker("A.1"), Marker("Missing")], [], []);
        var vanilla = new PrerequisiteEvent("p4", [Marker("Game.1")], [], []);
        var none = new PrerequisiteEvent("p5", [], [Story("50")], []);
        var origins = new Dictionary<string, EventOriginMatch>(StringComparer.OrdinalIgnoreCase)
        {
            ["A.1"] = new(a, [], EventOriginMatchStatus.Identified),
            ["A.2"] = new(a, [], EventOriginMatchStatus.Identified),
            ["B.1"] = new(b, [], EventOriginMatchStatus.Identified),
            ["Game.1"] = new(EventSourceActor.GameBase, [], EventOriginMatchStatus.Identified)
        };
        Check(StorySearchIndex.PrerequisiteSource(single, origins).Key == StoryQuerySource.ModKey("Fixture.A"), "Rules that agree name their provider");
        Check(StorySearchIndex.PrerequisiteSource(mixed, origins).Key == StoryQuerySource.AmbiguousKey, "Rules from different providers are ambiguous");
        Check(StorySearchIndex.PrerequisiteSource(partial, origins).Key == StoryQuerySource.UnknownKey, "Any unresolved rule keeps the source unknown");
        Check(StorySearchIndex.PrerequisiteSource(vanilla, origins).Key == StoryQuerySource.GameKey, "Vanilla rules name the base game");
        Check(StorySearchIndex.PrerequisiteSource(none, origins).Key == StoryQuerySource.UnknownKey, "Without rules the source is not guessed from internal flows");
        Check(StorySearchIndex.PrerequisiteSource(single, new Dictionary<string, EventOriginMatch>
            { ["A.1"] = new(a, [], EventOriginMatchStatus.Incomplete), ["A.2"] = new(a, [], EventOriginMatchStatus.Identified) }).Key == StoryQuerySource.UnknownKey,
            "Incomplete rule evidence is not a confirmed source");

        var catalog = new GalleryCatalog([], [], []) { Prerequisites = [single, mixed, partial, vanilla, none], RuleOrigins = origins };
        var search = new StorySearchIndex(catalog, _ => "-", name => name, sourceText: key => key);
        Check(search.Search("", new QueryFilter { Source = StoryQuerySource.ModKey("fixture.a"), Kind = QueryKind.Prerequisite }).Single().EventId == "p1",
            "Prerequisite rows filter by their rules' provider");
        Check(search.Search("Pack A").Single().EventId == "p1" && search.Search("fixture.a").Single().EventId == "p1",
            "Prerequisite rows are searchable by provider name and mod ID");
        Check(search.Search("", new QueryFilter { Source = StoryQuerySource.UnknownKey }).Select(row => row.EventId).SequenceEqual(["p3", "p5"])
            && search.Search("", new QueryFilter { Source = StoryQuerySource.AmbiguousKey }).Single().EventId == "p2"
            && search.Search("", new QueryFilter { Source = StoryQuerySource.GameKey }).Single().EventId == "p4",
            "Unknown, ambiguous and base game prerequisite sources stay separate");
    }

    private static void RulePresentation()
    {
        var a = new EventSourceActor("Fixture.A", "Pack A", "1.0");
        var b = new EventSourceActor("Fixture.B", "Pack B");
        var c = new EventSourceActor("Fixture.C", "Pack C", "2.0");
        EventDefinitionCandidate Rule(EventSourceActor actor, bool edit = false)
            => new(actor, "Data/TriggerActions", "Rule", "content.json", "hash") { IsModification = edit };
        IReadOnlyList<string> lines = EventOriginPresentation.BuildRule(
            new(a, [Rule(a), Rule(b), Rule(c, edit: true)], EventOriginMatchStatus.Identified), Translate);
        Check(lines[0] == "prerequisite.rule-provider:provider=source.actor:name=Pack A;id=Fixture.A;version=1.0", "The rule provider is named with ID and version");
        Check(lines.Any(line => line.StartsWith("prerequisite.rule-others:") && line.Contains("Fixture.B") && !line.Contains("Fixture.A") && !line.Contains("Fixture.C")),
            "Other declarers are listed apart from the provider and editors");
        Check(lines.Any(line => line.StartsWith("prerequisite.rule-editors:") && line.Contains("Fixture.C") && line.Contains("version=2.0")),
            "Packs that only edit the rule are listed as editors");
        lines = EventOriginPresentation.BuildRule(new(null, [Rule(a), Rule(b)], EventOriginMatchStatus.Ambiguous), Translate);
        Check(lines[0] == "prerequisite.rule-provider:provider=source.origin-ambiguous" && lines.Any(line => line.Contains("Fixture.A") && line.Contains("Fixture.B")),
            "Ambiguous rules name no provider and list every declarer");
        lines = EventOriginPresentation.BuildRule(new(b, [Rule(b)], EventOriginMatchStatus.Incomplete) { UnreadDependencies = [a] }, Translate);
        Check(lines[0].EndsWith(" / source.origin-incomplete") && lines.Contains("source.origin-incomplete-help:packs=Pack A"),
            "Incomplete rule evidence is marked and names the unread pack");
        lines = EventOriginPresentation.BuildRule(EventOriginMatch.Unknown, Translate);
        Check(lines.SequenceEqual(["prerequisite.rule-provider:provider=source.unknown"]), "An undeclared rule is unknown, never assumed vanilla");
        lines = EventOriginPresentation.BuildRule(new(EventSourceActor.GameBase, [Rule(EventSourceActor.GameBase)], EventOriginMatchStatus.Identified), Translate);
        Check(lines.SequenceEqual(["prerequisite.rule-provider:provider=source.game"]), "A vanilla rule is labeled as base game content");
    }

    private static void BranchFragments()
    {
        var events = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Town"] = new Dictionary<string, string> { ["branchA"] = "speak Abigail \"A\"/fork branchA", ["branchB"] = "end" },
            ["Beach"] = new Dictionary<string, string> { ["beachBranch"] = "end" }
        };
        EventFragments fragments = EventFragmentCollector.Collect(
            "fork branchA/switchEvent branchB/fork someRequirement Strings\\Events:Line true/fork ghost/changeLocation Beach/switchEvent beachBranch",
            "Town", location => events.GetValueOrDefault(location), script => script.Split('/'), command => command.Split(' '),
            _ => "end");
        Check(fragments.BranchKeys.Select(branch => branch.Key).SequenceEqual(["branchA", "branchB", "ghost", "beachBranch"]),
            "fork and switchEvent keys are recorded once each; translation forks are not event-asset branches");
        Check(fragments.BranchKeys.Single(branch => branch.Key == "ghost").Found == false
            && fragments.BranchKeys.Single(branch => branch.Key == "beachBranch").OtherLocation == "Beach"
            && fragments.BranchKeys.Single(branch => branch.Key == "branchA").OtherLocation is null,
            "Branch references keep their location and whether the current asset supplies them");
        Check(new EventFragments([], []).BranchKeys.Count == 0, "Fragments built without branch data stay valid");
        var resolved = new ResolvedEvent(new EventIdentity("Data/Events/Town", "42"), "Town", "42/f Abigail 500", "script", fragments, "d", "s");
        Check(EventOriginPresentation.BranchAssets(resolved).SequenceEqual([("Data/Events/Town", "branchA"), ("Data/Events/Town", "branchB"),
            ("Data/Events/Town", "ghost"), ("Data/Events/Beach", "beachBranch")]), "Same-location branches use the event's own asset; others their location's asset");
    }

    private static void BranchPresentation()
    {
        var actor = new EventSourceActor("Fixture.Story", "Story Pack", "1.0");
        var other = new EventSourceActor("Fixture.Other", "Other Pack", "3.0");
        var basePack = new EventSourceActor("Fixture.Base", "Base Pack", "1.0");
        EventDefinitionCandidate Event(EventSourceActor owner, string key) => new(owner, "Data/Events/Town", key, "events.json", "hash");
        var resolved = new ResolvedEvent(new EventIdentity("Data/Events/Town", "42"), "Town", "42/f Abigail 500", "script", new([], []), "d", "s");
        var main = new EventOriginMatch(actor, [Event(actor, "42/f Abigail 500")], EventOriginMatchStatus.Identified);
        EventBranchOrigin[] branches =
        [
            new("Data/Events/Town", "same", new(actor, [Event(actor, "same")], EventOriginMatchStatus.Identified)),
            new("Data/Events/Town", "overridden", new(actor, [Event(actor, "overridden"), Event(other, "overridden")], EventOriginMatchStatus.Identified)),
            new("Data/Events/Town", "added", new(other, [Event(other, "added")], EventOriginMatchStatus.Identified)),
            new("Data/Events/Town", "partial", new(other, [Event(other, "partial")], EventOriginMatchStatus.Incomplete) { UnreadDependencies = [basePack] }),
            new("Data/Events/Beach", "nowhere", EventOriginMatch.Unknown)
        ];
        EventSourceDisplay display = EventOriginPresentation.Build(resolved, main, Translate, branches);
        List<string> details = display.Details.ToList();
        int heading = details.IndexOf("source.branches");
        Check(heading > details.FindIndex(line => line.StartsWith("source.key:")) && details.Last() == "source.scope-branches" && !details.Contains("source.scope"),
            "Branch sources follow the main event details and switch the scope note");
        Check(details.Any(line => line.StartsWith("source.branch:key=same;asset=Data/Events/Town;") && line.Contains("name=Story Pack")),
            "Branches declared by the main provider list their key, asset and provider");
        int overridden = details.FindIndex(line => line.StartsWith("source.branch:key=overridden;"));
        Check(overridden > heading && details[overridden].Contains("name=Story Pack") && details[overridden + 1].StartsWith("source.branch-others:")
            && details[overridden + 1].Contains("Fixture.Other"), "A branch another pack also declares lists its provider and the other declarer");
        Check(details.Any(line => line.StartsWith("source.branch:key=added;") && line.Contains("name=Other Pack")),
            "A branch added by another pack names that pack");
        int partial = details.FindIndex(line => line.StartsWith("source.branch:key=partial;"));
        Check(partial > heading && details[partial].EndsWith(" / source.origin-incomplete")
            && details[partial + 1] == "source.origin-incomplete-help:packs=Base Pack",
            "An incomplete branch marks its status and names the unread dependency pack");
        Check(details.Any(line => line.StartsWith("source.branch:key=nowhere;asset=Data/Events/Beach;provider=source.unknown")),
            "An undeclared branch is shown as unknown with its asset");
        display = EventOriginPresentation.Build(resolved, main, Translate, []);
        Check(display.Details.Contains("source.scope") && !display.Details.Contains("source.branches"), "Without branches the main-event scope note is kept");
    }

    private static string FixtureRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            foreach (string candidate in new[] { Path.Combine(directory.FullName, "Fixtures", "SourceStage1"), Path.Combine(directory.FullName, "Checks", "Fixtures", "SourceStage1") })
                if (Directory.Exists(candidate)) return candidate;
        throw new InvalidOperationException("Read-only fixture folder Checks/Fixtures/SourceStage1 was not found.");
    }

    private static string Translate(string key, object? args) => args is null ? key
        : key + ":" + string.Join(";", args.GetType().GetProperties().Select(property => property.Name + "=" + property.GetValue(args)));

    private static void Check(bool condition, string reason)
    {
        checks++;
        if (!condition) throw new InvalidOperationException(reason);
    }
}
