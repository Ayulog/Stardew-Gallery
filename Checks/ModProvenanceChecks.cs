using System.Text.Json;
using System.Text.RegularExpressions;
using StardewGallery;

internal static class ModProvenanceChecks
{
    private static readonly string[] M3aI18nKeys =
    [
        "home.volume-all",
        "home.volume-vanilla",
        "home.volume-label",
        "query.kind-controller",
        "pill.controller",
        "replay.controller-unavailable",
        "config.show-controller.name",
        "config.show-controller.tooltip"
    ];

    internal static void Run(bool memoryOnly = false)
    {
        int checks = 0;
        void Check(bool condition, string message)
        {
            checks++;
            if (!condition)
                throw new InvalidOperationException("Mod provenance check failed: " + message);
        }

        VerifyCleanModName(Check);
        VerifySubPackMergingAndAntiSwallow(Check);
        VerifyThreeTierProvenance(Check);
        if (!memoryOnly)
            VerifyCharacterDataScanning(Check);
        VerifyControllerEventDualPredicate(Check);
        VerifyStorySearchVolumeGrouping(Check);
        VerifyM3aI18nKeys(Check);

        Console.WriteLine($"Mod provenance & M3a checks passed ({checks} assertions).");
    }

    private static void VerifyCleanModName(Action<bool, string> check)
    {
        check(ModProvenanceService.CleanModName("[CP] Stardew Valley Expanded") == "Stardew Valley Expanded",
            "strips leading [CP] tag");
        check(ModProvenanceService.CleanModName("[CP][NPC][JA] East Scarp (SMAPI component) (1.6.15)") == "East Scarp",
            "strips stacked leading tags and stacked trailing suffixes");
        check(ModProvenanceService.CleanModName("Ridgeside Village - C#") == "Ridgeside Village",
            "strips trailing - C#");
        check(ModProvenanceService.CleanModName("Ridgeside Village C#") == "Ridgeside Village",
            "strips trailing C#");
        check(ModProvenanceService.CleanModName("[FTM] 东斯卡普附加包 SMAPI 组件 (CP)") == "东斯卡普附加包",
            "preserves original mod language while stripping Chinese/English technical suffixes");
        check(ModProvenanceService.CleanModName("[STF][BL][CC][AT][MFM][SAAT][TMXL] Cool Mod (Content Patcher)") == "Cool Mod",
            "strips all supported loader tags and (Content Patcher)");
        check(ModProvenanceService.CleanModName("[CP] Foo - (1.6.15)") == "Foo",
            "strips trailing - (1.6.15) without leaving dangling hyphen");
        check(ModProvenanceService.CleanModName("[CP] Foo - (SMAPI component)") == "Foo",
            "strips trailing - (SMAPI component) without leaving dangling hyphen");
        check(ModProvenanceService.CleanModName("  [CP]  ") == "[CP]",
            "tag-only name falls back safely to raw trimmed name");
        check(ModProvenanceService.CleanModName("   ", "Author.Fallback") == "Author.Fallback",
            "empty name falls back to fallbackId");
    }

    private static void VerifySubPackMergingAndAntiSwallow(Action<bool, string> check)
    {
        var manifests = new ModProvenanceManifest[]
        {
            new("FlashShifter.SVECode", "Stardew Valley Expanded Code", Author: "FlashShifter", ParentDirName: "Stardew Valley Expanded"),
            new("FlashShifter.StardewValleyExpandedCP", "[CP] Stardew Valley Expanded", Author: "FlashShifter",
                ContentPackFor: "Pathoschild.ContentPatcher", Dependencies: ["FlashShifter.SVECode"], ParentDirName: "Stardew Valley Expanded"),
            new("FlashShifter.SVEFTM", "[FTM] Stardew Valley Expanded", Author: "FlashShifter",
                ContentPackFor: "Esca.FarmTypeManager", ParentDirName: "Stardew Valley Expanded"),
            new("LemurKat.EastScarp", "[CP] East Scarp", Author: "LemurKat", ContentPackFor: "Pathoschild.ContentPatcher"),
            new("Arknir.Lavril", "[CP] Lavril", Author: "Arknir", ContentPackFor: "Pathoschild.ContentPatcher",
                Dependencies: ["LemurKat.EastScarp"]),
            new("SameAuthor.First", "Standalone Alpha", Author: "SharedAuthor", ParentDirName: null),
            new("SameAuthor.Second", "Standalone Beta", Author: "SharedAuthor", ParentDirName: null),
            new("Different.First", "Shared Title", Author: "AuthorOne"),
            new("Different.Second", "Shared Title", Author: "AuthorTwo"),
            new("Bridge.First", "[CP] Bridge Title", Author: "AuthorOne"),
            new("Bridge.Middle", "[FTM] Bridge Title", Author: null),
            new("Bridge.MiddleEmpty", "[JA] Bridge Title", Author: "   "),
            new("Bridge.Second", "Bridge Title", Author: "AuthorTwo")
        };

        var service = new ModProvenanceService(manifests);

        ShelfVolume sveCp = service.VolumeForActor(new("FlashShifter.StardewValleyExpandedCP", "[CP] Stardew Valley Expanded"));
        ShelfVolume sveCode = service.VolumeForActor(new("FlashShifter.SVECode", "Stardew Valley Expanded Code"));
        ShelfVolume sveFtm = service.VolumeForActor(new("FlashShifter.SVEFTM", "[FTM] Stardew Valley Expanded"));
        check(sveCp == sveCode && sveCp == sveFtm && sveCp.DisplayName == "Stardew Valley Expanded"
            && sveCp.Key == StoryQuerySource.ModKey("FlashShifter.StardewValleyExpandedCP"),
            "SVE CP + C# + FTM sub-packs merge into canonical CP pack's cleaned name");

        ShelfVolume eastScarp = service.VolumeForActor(new("LemurKat.EastScarp", "[CP] East Scarp"));
        ShelfVolume lavril = service.VolumeForActor(new("Arknir.Lavril", "[CP] Lavril"));
        check(lavril != eastScarp && lavril.DisplayName == "Lavril" && eastScarp.DisplayName == "East Scarp",
            "independent NPC addon [CP] Lavril depending on East Scarp is NEVER swallowed");

        ShelfVolume standaloneA = service.VolumeForActor(new("SameAuthor.First", "Standalone Alpha"));
        ShelfVolume standaloneB = service.VolumeForActor(new("SameAuthor.Second", "Standalone Beta"));
        check(standaloneA != standaloneB,
            "same-author standalone mods in Mods/ root without dependency or shared parent folder stay separate");

        ShelfVolume diffA = service.VolumeForActor(new("Different.First", "Shared Title"));
        ShelfVolume diffB = service.VolumeForActor(new("Different.Second", "Shared Title"));
        check(diffA != diffB,
            "mods with conflicting non-empty authors NEVER merge even with identical cleaned names");

        ShelfVolume bridgeA = service.VolumeForActor(new("Bridge.First", "[CP] Bridge Title"));
        ShelfVolume bridgeB = service.VolumeForActor(new("Bridge.Second", "Bridge Title"));
        check(bridgeA != bridgeB,
            "empty-author pack with matching cleaned name NEVER bridges conflicting non-empty authors into one volume");
    }

    private static void VerifyThreeTierProvenance(Action<bool, string> check)
    {
        var sveCode = new EventSourceActor("FlashShifter.SVECode", "Stardew Valley Expanded Code", "1.15");
        var sveCp = new EventSourceActor("FlashShifter.StardewValleyExpandedCP", "[CP] Stardew Valley Expanded", "1.15");
        var sveZh = new EventSourceActor("Translator.SVEZH", "SVE Chinese Translation", "1.0");
        var lavril = new EventSourceActor("Arknir.Lavril", "[CP] Lavril", "1.0");

        var service = new ModProvenanceService(
        [
            new(sveCode.UniqueId, sveCode.Name, Author: "FlashShifter"),
            new(sveCp.UniqueId, sveCp.Name, Author: "FlashShifter", ContentPackFor: "Pathoschild.ContentPatcher", Dependencies: [sveCode.UniqueId]),
            new(sveZh.UniqueId, sveZh.Name, Author: "Translator", ContentPackFor: "Pathoschild.ContentPatcher", Dependencies: [sveCp.UniqueId]),
            new(lavril.UniqueId, lavril.Name, Author: "Arknir", ContentPackFor: "Pathoschild.ContentPatcher")
        ]);

        var index = new EventDefinitionIndex();
        index.RegisterPack(new(sveCp, "sve", [sveCode.UniqueId]));
        index.RegisterPack(new(sveZh, "zh", [sveCp.UniqueId]));
        index.RegisterPack(new(lavril, "lavril", []));

        // Tier 1: Vanilla lock even when a mod edits/replaces the same character or event.
        index.AddCharacter(new(EventSourceActor.GameBase, "Abigail", "Data/Characters.xnb"));
        index.AddCharacter(new(sveCp, "Abigail", "characters/abigail.json"));
        index.AddCharacter(new(sveCp, "Sophia", "characters/sophia.json"));
        index.AddCharacter(new(sveZh, "Sophia", "i18n/sophia.json"));
        index.AddCharacter(new(lavril, "Lavril", "characters/lavril.json"));

        index.Add(new(EventSourceActor.GameBase, "Data/Events/Town", "1", "Data/Events/Town.xnb", "h1"));
        index.Add(new(sveCp, "Data/Events/Town", "1", "events/override.json", "h2"));
        index.Add(new(sveCp, "Data/Events/Town", "9001", "events/sophia.json", "h3"));
        index.Add(new(sveZh, "Data/Events/Town", "9001", "events/sophia_zh.json", "h4"));

        // Tier 1 verification
        ShelfVolume? abigailVol = service.ResolveCharacterVolume("Abigail", index.MatchCharacter("Abigail"));
        check(abigailVol == ShelfVolume.Vanilla, "Tier 1 locks vanilla Data/Characters.xnb villager to Vanilla");

        // Tier 3 verification (translation patch folds into root creator)
        ShelfVolume? sophiaVol = service.ResolveCharacterVolume("Sophia", index.MatchCharacter("Sophia"));
        check(sophiaVol is { IsVanilla: false, DisplayName: "Stardew Valley Expanded" },
            "Tier 2/3 resolves Sophia via Data/Characters and folds translation patch to SVE");

        ResolvedEvent MakeResolved(string id) => new(
            new("Data/Events/Town", id), "Town", id + "/f Sophia 500", "none|0 0|Sophia 1 1 2|speak Sophia hi|end",
            new([], []), "d", "s");

        GalleryEvent vanillaEv = new(MakeResolved("1"), new(OwnershipKind.Direct, [new("Abigail", 500)])) { Kind = StoryKind.Heart, RelatedNpcNames = ["Abigail"] };
        GalleryEvent modCsharpEv = new(MakeResolved("8001"), new(OwnershipKind.Direct, [new("Sophia", 500)])) { Kind = StoryKind.Heart, RelatedNpcNames = ["Abigail", "Sophia"] };
        GalleryEvent vanillaOnlyCsharpEv = new(MakeResolved("8002"), new(OwnershipKind.Direct, [new("Abigail", 500)])) { Kind = StoryKind.Heart, RelatedNpcNames = ["Abigail"] };
        GalleryEvent crossoverCsharpEv = new(MakeResolved("8003"), new(OwnershipKind.Direct, [new("Sophia", 500), new("Lavril", 500)]))
            { Kind = StoryKind.Heart, RelatedNpcNames = ["Sophia", "Lavril"] };
        GalleryEvent translatedEv = new(MakeResolved("9001"), new(OwnershipKind.Direct, [new("Sophia", 500)])) { Kind = StoryKind.Heart, RelatedNpcNames = ["Sophia"] };

        check(service.ResolveEventVolume(vanillaEv, index.Match("Data/Events/Town", "1"), index.MatchCharacter) == ShelfVolume.Vanilla,
            "Tier 1 locks vanilla event to Vanilla");
        check(service.ResolveEventVolume(modCsharpEv, EventOriginMatch.Unknown, index.MatchCharacter)?.DisplayName == "Stardew Valley Expanded",
            "Tier 2 inherits SVE volume for C# event with Abigail + Sophia");
        check(service.ResolveEventVolume(vanillaOnlyCsharpEv, EventOriginMatch.Unknown, index.MatchCharacter) is null,
            "Tier 2 never mislabels a C# event with only vanilla NPCs as Vanilla");
        check(service.ResolveEventVolume(crossoverCsharpEv, EventOriginMatch.Unknown, index.MatchCharacter) is null,
            "Tier 2 stays unknown when a C# event spans two distinct mod volumes");

        EventOriginMatch translatedMatch = index.Match("Data/Events/Town", "9001");
        check(service.ResolveEventVolume(translatedEv, translatedMatch, index.MatchCharacter)?.DisplayName == "Stardew Valley Expanded",
            "Tier 3 folds translation patch event into root pack");
        EventSourceDisplay detail = EventOriginPresentation.Build(translatedEv.Resolved, translatedMatch, (k, a) =>
            a is null ? k : $"{k}:{string.Join(",", a.GetType().GetProperties().Select(p => $"{p.Name}={p.GetValue(a)}"))}");
        check(detail.Details.Any(line => line.Contains("events/sophia_zh.json", StringComparison.Ordinal))
            && detail.Details.Any(line => line.Contains("events/sophia.json", StringComparison.Ordinal)),
            "EventOriginPresentation detail view preserves complete raw files and declarers");
    }

    private static void VerifyCharacterDataScanning(Action<bool, string> check)
    {
        string temp = Path.Combine(Path.GetTempPath(), "StardewGallery-CharScan-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            Directory.CreateDirectory(Path.Combine(temp, "data"));
            File.WriteAllText(Path.Combine(temp, "content.json"), """
                {
                  "Changes": [
                    { "Action": "EditData", "Target": "Data/Characters", "Entries": { "Sophia": { "DisplayName": "Sophia" }, "Deleted": null } },
                    { "Action": "EditData", "Target": "Data/Characters", "Fields": { "Abigail": { "Age": "Adult" } } },
                    { "Action": "EditData", "Target": "Data/Characters", "FromFile": "data/chars.json" },
                    { "Action": "EditData", "Target": "Data/Characters", "FromFile": "data/fields_only.json" },
                    { "Action": "Load", "Target": "Data/Characters", "FromFile": "data/loaded_chars.json" },
                    { "Action": "EditData", "Target": "Data/Events/Town", "Entries": { "777/f Sophia 500": "none/0 0/Sophia 1 1 2/speak Sophia hi/end" } }
                  ]
                }
                """);
            File.WriteAllText(Path.Combine(temp, "data", "chars.json"), """
                {
                  "Entries": {
                    "Victor": { "DisplayName": "Victor" }
                  }
                }
                """);
            File.WriteAllText(Path.Combine(temp, "data", "fields_only.json"), """
                {
                  "Fields": {
                    "Abigail": { "Age": "Adult" }
                  },
                  "When": {
                    "Season": "spring"
                  }
                }
                """);
            File.WriteAllText(Path.Combine(temp, "data", "loaded_chars.json"), """
                {
                  "LoadedNpc": { "DisplayName": "LoadedNpc" }
                }
                """);

            var actor = new EventSourceActor("FlashShifter.SVE", "[CP] Stardew Valley Expanded", "1.15");
            var scanner = new ContentPackEventScanner();
            var index = new EventDefinitionIndex();
            EventDefinitionScanResult result = scanner.Scan(new(actor, temp, ["Pathoschild.ContentPatcher"]), index);

            check(index.Count == 1 && result.DefinitionsAdded == 1,
                "Data/Characters scanning does not alter EventDefinitionIndex.Count or DefinitionsAdded");
            check(!index.AssetNames.Any(a => a.Equals("Data/Characters", StringComparison.OrdinalIgnoreCase)),
                "Data/Characters is not mixed into Event/TriggerAction AssetNames");
            check(index.MatchCharacter("Sophia").Provider == actor && index.MatchCharacter("Victor").Provider == actor
                && index.MatchCharacter("LoadedNpc").Provider == actor,
                "Data/Characters Entries, EditData FromFile Entries, and Load FromFile root dictionary are indexed");
            check(index.MatchCharacter("Deleted").Status == EventOriginMatchStatus.Unknown
                && index.MatchCharacter("Abigail").Status == EventOriginMatchStatus.Unknown
                && index.MatchCharacter("Fields").Status == EventOriginMatchStatus.Unknown
                && index.MatchCharacter("When").Status == EventOriginMatchStatus.Unknown,
                "null deletions, Fields edits, and EditData FromFile without Entries never claim Data/Characters ownership");

            // Cache replay on fresh index
            var secondIndex = new EventDefinitionIndex();
            scanner.Scan(new(actor, temp, ["Pathoschild.ContentPatcher"]), secondIndex);
            check(secondIndex.MatchCharacter("Sophia").Provider == actor && secondIndex.MatchCharacter("Victor").Provider == actor,
                "Cached scan replays CharacterCandidates into fresh index");
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }

    private static void VerifyControllerEventDualPredicate(Action<bool, string> check)
    {
        string[] Split(string s) => s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var builder = new GalleryCatalogBuilder(k => k.Split('/'), s => s.Split('|'), Split, Split, () => null);
        GalleryCharacter[] chars = [new("Abigail", "Abigail", true, 1000)];

        ResolvedEvent Make(string id, string key, string script, IReadOnlyDictionary<string, string>? byKey = null, string[]? extra = null)
        {
            List<string> scripts = [script];
            if (extra is not null) scripts.AddRange(extra);
            if (byKey is not null) scripts.AddRange(byKey.Values);
            return new ResolvedEvent(new("Data/Events/Town", id), "Town", key, script,
                new EventFragments(scripts, [], null, byKey), "d", "s");
        }

        ResolvedEvent[] fixtures =
        [
            // True controller events (Set 1 empty AND no visible non-blackout Set 2)
            Make("ctrl-custom", "ctrl-custom/f Abigail 500", "none|0 0|Abigail 1 1 2|Custom.RunTrigger foo|end"),
            Make("ctrl-blackout-move", "ctrl-blackout-move/f Abigail 500", "none|0 0|Abigail 1 1 2|globalFade|move Abigail 1 0 2|Custom.Flag|end"),
            Make("ctrl-offscreen-emote", "ctrl-offscreen-emote", "none|-500 -500|Abigail 1 1 2|emote Abigail 20|Custom.Flag|end"),
            Make("ctrl-fade-sprite", "ctrl-fade-sprite", "none|0 0|farmer 1 1 2|fade|specificTemporarySprite test|Custom.Flag|end"),
            // Visible silent cutscenes (MUST NOT be marked IsControllerEvent!)
            Make("silent-anim", "silent-anim/f Abigail 500", "none|0 0|Abigail 1 1 2|animate Abigail false true 500 0 1|end"),
            Make("silent-junimo", "silent-junimo", "none|0 0|farmer 1 1 2|specificTemporarySprite junimo|end"),
            Make("silent-unfade", "silent-unfade", "none|0 0|Abigail 1 1 2|globalFade|globalFadeToClear|emote Abigail 20|end"),
            Make("silent-fade-unfade", "silent-fade-unfade", "none|0 0|Abigail 1 1 2|fade|fade unfade|jump Abigail|end"),
            // Dialogue even during blackout/offscreen (MUST NOT be marked IsControllerEvent!)
            Make("blackout-speak", "blackout-speak/f Abigail 500", "none|-500 -500|Abigail 1 1 2|globalFade|speak Abigail hello|end"),
            Make("quick-question", "quick-question", "none|-500 -500|farmer 1 1 2|quickQuestion #Prompt#Yes#No(break)pause 10(break)pause 10|end"),
            // Branch visibility isolation: fork before globalFade must see visible action!
            Make("fork-visible", "fork-visible", "none|0 0|Abigail 1 1 2|fork visibleBranch|globalFade|Custom.Flag|end",
                new Dictionary<string, string> { ["visibleBranch"] = "emote Abigail 20|end" }),
            // Branch blackout isolation: fork after globalFade stays blacked out
            Make("fork-blackout", "fork-blackout", "none|0 0|Abigail 1 1 2|globalFade|fork hiddenBranch|end",
                new Dictionary<string, string> { ["hiddenBranch"] = "emote Abigail 20|Custom.Flag|end" }),
            // Real heart event for Abigail
            Make("real-heart", "real-heart/f Abigail 500", "none|0 0|Abigail 1 1 2|speak Abigail hi|end")
        ];

        GalleryCatalog catalog = builder.Build(chars, fixtures).Catalog;
        GalleryEvent Find(string id) => catalog.Find(new("Data/Events/Town", id))!;

        foreach (string id in new[] { "ctrl-custom", "ctrl-blackout-move", "ctrl-offscreen-emote", "ctrl-fade-sprite", "fork-blackout" })
            check(Find(id).IsControllerEvent, id + " must be classified as IsControllerEvent");

        foreach (string id in new[] { "silent-anim", "silent-junimo", "silent-unfade", "silent-fade-unfade", "blackout-speak", "quick-question", "fork-visible", "real-heart" })
            check(!Find(id).IsControllerEvent, id + " must NOT be misclassified as IsControllerEvent");

        // Denominator & Catalog isolation
        check(catalog.Events.All(e => !e.IsControllerEvent), "Catalog.Events excludes controller events");
        check(catalog.HeartEventsFor("Abigail").Count == 3
            && catalog.HeartEventsFor("Abigail").Select(e => e.EventId).OrderBy(x => x).SequenceEqual(["blackout-speak", "real-heart", "silent-anim"]),
            "HeartEventsFor excludes controller events even if precondition had f Abigail 500");

        StoryNodeProgress progress = CharacterStoryProgress.Measure(catalog.HeartEventsFor("Abigail"), catalog, ["ctrl-custom", "real-heart"]);
        check(progress.CollectedNodes == 1 && progress.TotalNodes == 3,
            "CharacterStoryProgress.Measure ignores controller events");

        // EncounterClue isolation
        var parser = new ConditionParser(k => k.Split('/'), Split);
        var evaluator = new ConditionEvaluator();
        var snapshot = new CurrentStateSnapshot(
            Season: "spring",
            Weather: "Sun",
            DayOfMonth: 1,
            Year: 1,
            Time: 600,
            DaysPlayed: 1,
            Friendship: new Dictionary<string, int> { ["Abigail"] = 1000 },
            EventsSeen: new HashSet<string>(),
            LocalMail: new HashSet<string>(),
            HostMail: new HashSet<string>(),
            HostOrLocalMail: new HashSet<string>(),
            Dating: new HashSet<string>(),
            Spouse: new HashSet<string>(),
            Roommate: false,
            WorldState: new HashSet<string>(),
            IsRaining: false);
        check(EncounterClueClassifier.Classify(Find("ctrl-custom"), parser.ParseRawKey(Find("ctrl-custom").EventKey), snapshot, evaluator) == EncounterClue.None,
            "EncounterClueClassifier returns None for controller events");

        // AutoMemento isolation
        check(AutoMementoRules.ResolveLiveStoryIdentity(catalog, "Data/Events/Town", "Town", "ctrl-custom") is null,
            "AutoMementoRules excludes controller events");

        // StorySearchIndex ShowControllerEvents toggle
        var hiddenIndex = new StorySearchIndex(catalog, e => e.LocationName, n => n, includeControllerEvents: false);
        check(hiddenIndex.Search("", new QueryFilter { Kind = QueryKind.All }).All(r => r.Event?.IsControllerEvent != true),
            "StorySearchIndex hides controller events when includeControllerEvents=false");

        var visibleIndex = new StorySearchIndex(catalog, e => e.LocationName, n => n, includeControllerEvents: true);
        IReadOnlyList<StorySearchRow> ctrlRows = visibleIndex.Search("", new QueryFilter { Kind = QueryKind.Controller });
        check(ctrlRows.Count == 5 && ctrlRows.All(r => r.Event?.IsControllerEvent == true),
            "StorySearchIndex exposes controller events under QueryKind.Controller when includeControllerEvents=true");
        check(visibleIndex.Search("", new QueryFilter { Kind = QueryKind.Heart }).All(r => r.Event?.IsControllerEvent != true)
            && visibleIndex.Search("", new QueryFilter { Kind = QueryKind.Ordinary }).All(r => r.Event?.IsControllerEvent != true),
            "QueryKind.Heart and QueryKind.Ordinary exclude controller events even when ShowControllerEvents=true");
    }

    private static void VerifyStorySearchVolumeGrouping(Action<bool, string> check)
    {
        var vol = new ShelfVolume(StoryQuerySource.ModKey("FlashShifter.SVE"), "Stardew Valley Expanded");
        GalleryEvent MakeEv(string id) => new(
            new(new("Data/Events/Town", id), "Town", id + "/f Sophia 500", "none/0 0/Sophia 1 1 2/speak Sophia hi/end", new([], []), "d", "s"),
            new(OwnershipKind.Direct, [new("Sophia", 500)]))
        { Kind = StoryKind.Heart, RelatedNpcNames = ["Sophia"] };

        var ev1 = MakeEv("1");
        var ev2 = MakeEv("2");
        var prereq = new PrerequisiteEvent("p1", [new("r1", "Current", true, ["DayStarted"], "", false, true, []),
            new("r2", "Current", true, ["DayStarted"], "", false, true, [])], [], [ev1]);

        var catalog = new GalleryCatalog([], [ev1, ev2], [])
        {
            Prerequisites = [prereq],
            EventVolumes = new Dictionary<EventIdentity, ShelfVolume>
            {
                [ev1.Resolved.Identity] = vol,
                [ev2.Resolved.Identity] = vol
            },
            RuleVolumes = new Dictionary<string, ShelfVolume>(StringComparer.OrdinalIgnoreCase)
            {
                ["r1"] = vol,
                ["r2"] = vol
            }
        };

        var index = new StorySearchIndex(catalog, _ => "Town", n => n);
        check(index.Search("", new QueryFilter { Source = vol.Key }).Count == 3,
            "StorySearchIndex groups merged story events and prerequisite rules under one clean ShelfVolume");
        check(index.Rows.All(r => r.SourceLabel == "Stardew Valley Expanded"),
            "Merged StoryQuerySource displays clean ShelfVolume name without duplicate mod ID suffix when unique");
    }

    private static void VerifyM3aI18nKeys(Action<bool, string> check)
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root is not null && !(File.Exists(Path.Combine(root.FullName, "StardewGallery.csproj"))
            && File.Exists(Path.Combine(root.FullName, "manifest.json"))))
            root = root.Parent;
        if (root is null)
            return;

        string i18nDir = Path.Combine(root.FullName, "i18n");
        string[] files = Directory.GetFiles(i18nDir, "*.json");
        check(files.Length == 12, "all 12 i18n files exist");
        Regex placeholderRegex = new(@"\{\{\s*(\w+)\s*\}\}");
        foreach (string file in files)
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file))!;
            foreach (string key in M3aI18nKeys)
            {
                check(dict.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value),
                    $"{Path.GetFileName(file)} contains non-empty {key}");
            }
            string[] tokens = placeholderRegex.Matches(dict["home.volume-label"]).Select(m => m.Groups[1].Value).ToArray();
            check(tokens.SequenceEqual(["volume"]), $"{Path.GetFileName(file)} home.volume-label preserves {{{{volume}}}} placeholder");
        }
    }
}
