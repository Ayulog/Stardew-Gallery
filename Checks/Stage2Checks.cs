using StardewGallery;

internal static class Stage2Checks
{
    private static int passed;

    internal static void Run(Func<string, string[]> splitArgs)
    {
        passed = 0;
        CheckParallelBranches(splitArgs);
        CheckEncounterClues(splitArgs);
        CheckStoryQuotes(splitArgs);
        CheckMusicExtraction(splitArgs);
        CheckReplayBranchControl(splitArgs);
        CheckTheaterHud();
        Console.WriteLine($"Stage 2 checks passed ({passed} assertions: parallel If-routes, encounter clues, anti-spoiler quotes, BGM, $q/fork sandbox, HUD fade).");
    }

    private static void CheckParallelBranches(Func<string, string[]> splitArgs)
    {
        Dictionary<string, string> translations = new(StringComparer.Ordinal)
        {
            ["Strings\\Events:Leah_2H_Prompt"] = "Should I host an art show or sell online?#$q 501/502 null#Well?#$r 501 30 Ans1#Host a town art show#$r 502 30 Ans2#Create an art website"
        };

        // Mirrors real 1.6.15 LocalizedContentManager.LoadStringReturnNullIfNotFound:
        // throws when ':' is absent (parseStringPath) or when the asset before ':' does not exist!
        string? ThrowingLoadTranslation(string path)
        {
            int colon = path.IndexOf(':');
            if (colon < 0)
                throw new InvalidOperationException("parseStringPath called without colon: " + path);
            string asset = path[..colon].Replace('/', '\\');
            if (!asset.Equals("Strings\\Events", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Missing string dictionary asset: " + asset);
            return translations.GetValueOrDefault(path);
        }

        GalleryCatalogBuilder builder = new(
            key => key.Split('/'),
            script => script.Split('|'),
            splitArgs,
            pos => pos.Split(' ', StringSplitOptions.RemoveEmptyEntries),
            () => null,
            ThrowingLoadTranslation);

        GalleryCharacter[] roster =
        [
            new("Leah", "Leah", true, 2500),
            new("Abigail", "Abigail", true, 2500),
            new("Sam", "Sam", true, 2500),
            new("Haley", "Haley", true, 2500)
        ];

        ResolvedEvent[] events =
        [
            // Leah: 2H event declares $q 501/502 via translation key; two 8H routes + two transitive 10H continuations!
            Event("LeahHouse", "50/f Leah 500", "breezy|0 0|Leah 1 1 2|speak Leah Strings\\Events:Leah_2H_Prompt|end"),
            Event("Town", "54/f Leah 2000/q 501", "jaunty|0 0|Leah 2 2 2|speak Leah \"Welcome to my art show!$h\"|end"),
            Event("LeahHouse", "55/f Leah 2000/G PLAYER_HAS_DIALOGUE_ANSWER Current 502", "breezy|0 0|Leah 2 2 2|speak Leah \"Look at my new website!$h\"|end"),
            Event("Forest", "56/e 54", "echos|0 0|Leah 3 3 2|speak Leah \"Everyone loved the town show.\"|end"),
            Event("Forest", "57/e 55", "echos|0 0|Leah 3 3 2|speak Leah \"Orders are coming in online.\"|end"),

            // Abigail: own 2H event 800, mutual negative !SawEvent pair (801 vs 802), positive/negative SawEvent pair
            // on same-character event 800 (803/e 800 vs 804/!e 800), and sequential early-guard trap (401/!e 1401 vs 1401/1402).
            Event("SeedShop", "800/f Abigail 500", "none|0 0|Abigail 1 1 2|speak Abigail \"2H intro\"|end"),
            Event("Town", "801/f Abigail 2000/k 802", "playful|0 0|Abigail 1 1 2|speak Abigail \"Route A\"|end"),
            Event("Mine", "802/f Abigail 2000/!e 801", "echos|0 0|Abigail 1 1 2|speak Abigail \"Route B\"|end"),
            Event("SeedShop", "803/f Abigail 1500/e 800", "jaunty|0 0|Abigail 1 1 2|speak Abigail \"Saw own 2H\"|end"),
            Event("SeedShop", "804/f Abigail 1500/G !PLAYER_HAS_SEEN_EVENT Current 800", "jaunty|0 0|Abigail 1 1 2|speak Abigail \"Missed own 2H\"|end"),
            Event("SeedShop", "401/f Abigail 1000/!e 1401", "none|0 0|Abigail 1 1 2|speak Abigail \"Early 4H\"|end"),
            Event("Backwoods", "1401/f Abigail 3500/O Abigail", "sadpiano|0 0|Abigail 1 1 2|speak Abigail \"14H Spouse\"|end"),
            Event("FarmHouse", "1402/O Abigail/e 1401", "musicboxsong|0 0|Abigail 1 1 2|speak Abigail \"14H Part 2\"|end"),

            // Sam: 3-way inline $q 77/78/79 where 751 also declares 'e 73' while 752/753 omit 'e 73',
            // plus ambiguous reused answer ID 999 not grouped!
            Event("SamHouse", "73/f Sam 500", "samPractice|0 0|Sam 1 1 2|speak Sam \"What kind of music?#$q 77/78/79 null#Pick one:#$r 77 10 A#Rock#$r 78 10 B#Electronic#$r 79 10 C#Country\"|speak Sam \"#$q 999/998 null#Test#$r 999 0 X#Opt1#$r 998 0 Y#Opt2\"|end"),
            Event("Town", "74/f Sam 750", "none|0 0|Sam 1 1 2|speak Sam \"#$q 999/997 null#Reused#$r 999 0 X#Opt1#$r 997 0 Z#Opt3\"|end"),
            Event("BusStop", "751/f Sam 2000/e 73/q 77", "heavy|0 0|Sam 1 1 2|speak Sam \"Rock show!\"|end"),
            Event("BusStop", "752/f Sam 2000/q 78", "shimmeringbastion|0 0|Sam 1 1 2|speak Sam \"Synth show!\"|end"),
            Event("BusStop", "753/f Sam 2000/q 79", "honkytonky|0 0|Sam 1 1 2|speak Sam \"Country show!\"|end"),
            Event("Beach", "760/f Sam 1000/q 999", "none|0 0|Sam 1 1 2|speak Sam \"Ambiguous A\"|end"),
            Event("Beach", "761/f Sam 1000/q 998", "none|0 0|Sam 1 1 2|speak Sam \"Ambiguous B\"|end"),
            // Wiki-style '$q -1 null' event prompt with distinct non-'-1' $r IDs:
            Event("SamHouse", "770/f Sam 1250", "none|0 0|Sam 1 1 2|speak Sam \"#$q -1 null#Pick:#$r mod_solo 0 A#Solo#$r mod_duet 0 B#Duet\"|end"),
            Event("Town", "771/f Sam 2250/q mod_solo", "none|0 0|Sam 1 1 2|speak Sam \"Solo!\"|end"),
            Event("Town", "772/f Sam 2250/q mod_duet", "none|0 0|Sam 1 1 2|speak Sam \"Duet!\"|end"),

            // Haley: upstream $q ansA/ansB inside a quickQuestion (break) block drives BOTH an 8H pair (900/903)
            // and a 10H+Dating pair (901/902) without 'e 8H', interleaved in ID order; plus ordinary event 200
            // referenced by 2H (!e 200) vs 10H+D (e 200) which must NEVER cross-tier group!
            Event("Town", "200", "none|0 0|Lewis 1 1 2|speak Lewis \"Town notice\"|end"),
            Event("HaleyHouse", "5000/f Haley 500", "none|0 0|Haley 1 1 2|quickQuestion #Photo style?# Outdoors#Studio(break)speak Haley \"#$q haley_ansA/haley_ansB null#Confirm?#$r haley_ansA 10 A#Outdoors#$r haley_ansB 10 B#Studio\"(break)end|end"),
            Event("Forest", "900/f Haley 2000/q haley_ansA", "breezy|0 0|Haley 1 1 2|speak Haley \"8H Outdoors\"|end"),
            Event("HaleyHouse", "901/f Haley 2500/D Haley/q haley_ansB", "musicboxsong|0 0|Haley 1 1 2|speak Haley \"10H Studio Romance\"|end"),
            Event("HaleyHouse", "902/f Haley 2500/D Haley/q haley_ansA", "musicboxsong|0 0|Haley 1 1 2|speak Haley \"10H Outdoors Romance\"|end"),
            Event("HaleyHouse", "903/f Haley 2000/q haley_ansB", "breezy|0 0|Haley 1 1 2|speak Haley \"8H Studio\"|end"),
            Event("HaleyHouse", "201/f Haley 500/!e 200", "none|0 0|Haley 1 1 2|speak Haley \"...\"|speak Haley \"你好！\"|speak Haley \"注意:危险\"|speak Haley \"Mods/Missing:Key\"|end"),
            Event("Beach", "1001/f Haley 2500/D Haley/e 200", "none|0 0|Haley 1 1 2|speak Haley \"10H after 200\"|end"),

            // #16 Defect 2A & 2B regressions: cross-map homonymous EventIds (ordinary BusStop event sharing ID with Town heart event)
            // 2A: BusStop '9000' (no hearts, scanned first) + Town '9000' (2000 pts) must keep minTier['9000'] == null regardless of order,
            // so '9010/f Abigail 1000/e 9000/q dupA' stays at 1000 and still pairs with '9011/f Abigail 1000/q dupB'!
            Event("SeedShop", "8999/f Abigail 500", "none|0 0|Abigail 1 1 2|speak Abigail \"#$q dupA/dupB null#Pick#$r dupA 0 A#1#$r dupB 0 B#2\"|speak Abigail \"#$q amb1/amb2 null#Pick2#$r amb1 0 A#1#$r amb2 0 B#2\"|end"),
            Event("BusStop", "9000/t 600 1200", "none|0 0|Lewis 1 1 2|speak Lewis \"Bus notice\"|end"),
            Event("Town", "9000/f Abigail 2000", "none|0 0|Abigail 1 1 2|speak Abigail \"Town 9000\"|end"),
            Event("SeedShop", "9010/f Abigail 1000/e 9000/q dupA", "none|0 0|Abigail 1 1 2|speak Abigail \"4H A\"|end"),
            Event("SeedShop", "9011/f Abigail 1000/q dupB", "none|0 0|Abigail 1 1 2|speak Abigail \"4H B\"|end"),
            // 2B: Town '9100' (q amb1) and Town '9101' (q amb2) are exclusive, but BusStop ALSO has ordinary event '9100',
            // so 'e 9100' is NOT unambiguous to Town:9100 and downstream 9200/9201 must NOT be falsely marked TransitivelyExclusive!
            Event("BusStop", "9100/t 600 1200", "none|0 0|Lewis 1 1 2|speak Lewis \"Ordinary 9100\"|end"),
            Event("Town", "9100/f Abigail 500/q amb1", "none|0 0|Abigail 1 1 2|speak Abigail \"Amb 1\"|end"),
            Event("Town", "9101/f Abigail 500/q amb2", "none|0 0|Abigail 1 1 2|speak Abigail \"Amb 2\"|end"),
            Event("Mine", "9200/f Abigail 1500/e 9100", "none|0 0|Abigail 1 1 2|speak Abigail \"Cont 1\"|end"),
            Event("Mine", "9201/f Abigail 1500/e 9101", "none|0 0|Abigail 1 1 2|speak Abigail \"Cont 2\"|end")
        ];

        GalleryCatalog catalog = builder.Build(roster, events).Catalog;
        GalleryEvent Find(string loc, string id) => catalog.Find(new EventIdentity("Data/Events/" + loc, id))!;

        // Leah: 50 (standalone) + {54, 55} (node 2) + {56, 57} (node 3) = 3 total nodes across 5 heart events!
        GalleryEvent leah54 = Find("Town", "54");
        GalleryEvent leah55 = Find("LeahHouse", "55");
        GalleryEvent leah56 = Find("Forest", "56");
        GalleryEvent leah57 = Find("Forest", "57");
        Assert(catalog.ParallelPeers(leah54.Resolved.Identity).Single().EventId == "55", "Leah 54 and 55 form an 8H parallel group");
        Assert(catalog.ParallelPeers(leah56.Resolved.Identity).Single().EventId == "57", "Leah 56 and 57 form a separate 10H transitive parallel group");

        HashSet<string> leahSeenMid = ["50", "54"];
        Assert(catalog.IsParallelBranchUnlocked(leah55, leahSeenMid), "Seeing Leah 54 unlocks missed sibling 55 as an If-route");
        Assert(!catalog.IsParallelBranchUnlocked(leah54, leahSeenMid), "Already seen event 54 is seen, not an If-route");
        Assert(!catalog.IsParallelBranchUnlocked(leah57, leahSeenMid), "Seeing 8H event 54 must NOT prematurely unlock 10H event 57");

        IReadOnlyList<GalleryEvent> leahHearts = catalog.HeartEventsFor("Leah");
        Assert(leahHearts.Count == 5, "Leah has 5 heart entries");
        StoryNodeProgress midProgress = CharacterStoryProgress.Measure(leahHearts, catalog, leahSeenMid);
        Assert(midProgress == new StoryNodeProgress(2, 3) && !midProgress.IsComplete, "Leah mid-playthrough progress is 2/3 nodes");

        HashSet<string> leahSinglePlaythroughDone = ["50", "54", "56"];
        StoryNodeProgress fullProgress = CharacterStoryProgress.Measure(leahHearts, catalog, leahSinglePlaythroughDone);
        Assert(fullProgress == new StoryNodeProgress(3, 3) && fullProgress.IsComplete, "Single-playthrough Leah achieves 3/3 full node completion");

        // Abigail: {801, 802} grouped, {803, 804} grouped, while {401, 1401, 1402} remain standalone!
        Assert(catalog.ParallelPeers(Find("Town", "801").Resolved.Identity).Single().EventId == "802", "Mutual !SawEvent pair 801/802 grouped");
        Assert(catalog.ParallelPeers(Find("SeedShop", "803").Resolved.Identity).Single().EventId == "804", "Positive/negative SawEvent pair 803/804 grouped");
        Assert(catalog.ParallelPeers(Find("SeedShop", "401").Resolved.Identity).Count == 0
            && catalog.ParallelPeers(Find("Backwoods", "1401").Resolved.Identity).Count == 0
            && catalog.ParallelPeers(Find("FarmHouse", "1402").Resolved.Identity).Count == 0,
            "Sequential early-guard 4H (!e 14H) vs 14H Spouse vs 14H Part 2 is never falsely grouped");

        // Sam: 3-way clique {751, 752, 753} = 1 node; ambiguous reused answer 999 is not grouped!
        Assert(catalog.ParallelPeers(Find("BusStop", "751").Resolved.Identity).Select(e => e.EventId).OrderBy(x => x).SequenceEqual(["752", "753"]),
            "3-way $q choice forms a 3-event clique");
        Assert(catalog.ParallelPeers(Find("Beach", "760").Resolved.Identity).Count == 0, "Answer ID offered in multiple $q prompts is not guessed as exclusive");
        Assert(catalog.ParallelPeers(Find("Town", "771").Resolved.Identity).Single().EventId == "772",
            "Event $q -1 header with distinct non-'-1' $r IDs groups downstream routes");
        HashSet<string> samSeen = ["73", "74", "752", "760", "761", "770", "771"];
        Assert(catalog.IsParallelBranchUnlocked(Find("BusStop", "751"), samSeen)
            && catalog.IsParallelBranchUnlocked(Find("BusStop", "753"), samSeen), "Seeing 1 of 3 parallel branches unlocks both missed siblings");
        Assert(CharacterStoryProgress.Measure(catalog.HeartEventsFor("Sam"), catalog, samSeen) == new StoryNodeProgress(7, 7),
            "3-way and $q -1 parallel groups each count as 1 node in character progress");

        // Haley cross-tier regression: 8H {900, 903} and 10H+D {901, 902} form strictly separate cliques;
        // seeing 8H 900 NEVER unlocks 10H+D 901/902, and 2H (!e 200) vs 10H+D (e 200) NEVER pair!
        Assert(catalog.ParallelPeers(Find("Forest", "900").Resolved.Identity).Single().EventId == "903",
            "8H Haley 900 pairs exclusively with 8H 903 despite interleaved 10H+D IDs");
        Assert(catalog.ParallelPeers(Find("HaleyHouse", "901").Resolved.Identity).Single().EventId == "902",
            "10H+D Haley 901 pairs exclusively with 10H+D 902");
        HashSet<string> haleySeen8H = ["5000", "900"];
        Assert(catalog.IsParallelBranchUnlocked(Find("HaleyHouse", "903"), haleySeen8H)
            && !catalog.IsParallelBranchUnlocked(Find("HaleyHouse", "901"), haleySeen8H)
            && !catalog.IsParallelBranchUnlocked(Find("HaleyHouse", "902"), haleySeen8H),
            "Seeing 8H 900 unlocks missed 8H 903 without prematurely unlocking 10H+Dating 901/902");
        Assert(StoryQuoteExtractor.GetVisibleQuote(Find("HaleyHouse", "901"), seen: false, unlockAll: false,
            parallelBranchUnlocked: catalog.IsParallelBranchUnlocked(Find("HaleyHouse", "901"), haleySeen8H),
            s => s.Split('|'), splitArgs) is null,
            "10H+Dating 901 StoryQuote remains hidden when only 8H 900 has been seen");
        Assert(catalog.ParallelPeers(Find("HaleyHouse", "201").Resolved.Identity).Count == 0
            && catalog.ParallelPeers(Find("Beach", "1001").Resolved.Identity).Count == 0,
            "2H (!e 200) and 10H+D (e 200) on an ordinary prerequisite never cross-tier group");

        // #16 Defect 2A & 2B assertions (verified in both forward and reversed allEvents scan orders!):
        GalleryCatalog reversedCatalog = builder.Build(roster, events.Reverse().ToArray()).Catalog;
        Assert(catalog.ParallelPeers(Find("SeedShop", "9010").Resolved.Identity).Single().EventId == "9011"
            && reversedCatalog.ParallelPeers(new EventIdentity("Data/Events/SeedShop", "9010")).Single().EventId == "9011",
            "Cross-map nullable minTiers stays null regardless of scan order so 9010/9011 remain same-tier peers");
        Assert(catalog.ParallelPeers(Find("Town", "9100").Resolved.Identity).Single().EventId == "9101",
            "Direct $q pair Town:9100 and Town:9101 still group");
        Assert(catalog.ParallelPeers(Find("Mine", "9200").Resolved.Identity).Count == 0
            && reversedCatalog.ParallelPeers(new EventIdentity("Data/Events/Mine", "9200")).Count == 0,
            "Ambiguous cross-map EventId '9100' (shared with BusStop ordinary event) blocks false transitive exclusivity on 9200/9201");

        // ReplayAccessPolicy centralized permissions:
        Assert(ReplayAccessPolicy.Evaluate(StoryKind.Heart, seen: false, unlockAll: false, ordinaryEnabled: false, parallelBranchUnlocked: true).Allowed,
            "ReplayAccessPolicy allows an opened parallel heart branch without global Unlock All");
        Assert(!ReplayAccessPolicy.Evaluate(StoryKind.Heart, seen: false, unlockAll: false, ordinaryEnabled: false, parallelBranchUnlocked: false).Allowed,
            "Unseen non-parallel heart event stays locked");
        Assert(!ReplayAccessPolicy.Evaluate(StoryKind.Ordinary, seen: false, unlockAll: false, ordinaryEnabled: true, parallelBranchUnlocked: true).Allowed,
            "Ordinary stories cannot unlock through heart parallel-branch flag");
        Assert(!ReplayAccessPolicy.Evaluate(StoryKind.Internal, seen: true, unlockAll: true, ordinaryEnabled: true, parallelBranchUnlocked: true).Allowed,
            "Internal flows never become replayable");
        Assert(!ReplayAccessPolicy.Evaluate(StoryKind.Heart, seen: false, unlockAll: false, ordinaryEnabled: false, multiplayer: true, parallelBranchUnlocked: true).Allowed,
            "Multiplayer restriction still overrides parallel-branch replay");
    }

    private static void CheckEncounterClues(Func<string, string[]> splitArgs)
    {
        ConditionParser parser = new(key => key.Split('/'), splitArgs);
        ConditionEvaluator evaluator = new();

        CurrentStateSnapshot baseState = new(
            Season: "spring",
            Weather: "Sun",
            DayOfMonth: 10,
            Year: 1,
            Time: 700,
            DaysPlayed: 10,
            Friendship: new Dictionary<string, int>(StringComparer.Ordinal) { ["Abigail"] = 2000 },
            EventsSeen: new HashSet<string>(StringComparer.Ordinal) { "10" },
            LocalMail: new HashSet<string>(StringComparer.Ordinal) { "guildMember" },
            HostMail: new HashSet<string>(StringComparer.Ordinal),
            HostOrLocalMail: new HashSet<string>(StringComparer.Ordinal) { "guildMember" },
            Dating: new HashSet<string>(StringComparer.Ordinal),
            Spouse: new HashSet<string>(StringComparer.Ordinal),
            Roommate: false,
            WorldState: new HashSet<string>(StringComparer.Ordinal),
            IsRaining: false)
        {
            Details = new ConditionReadState
            {
                Weekday = DayOfWeek.Wednesday,
                VisibleNpcs = new Dictionary<string, bool>(StringComparer.Ordinal) { ["Abigail"] = false },
                NpcsAtLocation = new HashSet<string>(StringComparer.Ordinal),
                EntryTile = null,
                FestivalDates = new HashSet<string>(StringComparer.Ordinal) { "spring13" },
                PassiveFestivals = new HashSet<string>(StringComparer.Ordinal),
                QueryFacts = new Dictionary<string, bool?>(StringComparer.Ordinal)
                {
                    [SafeGameQuery.FactKey(new("PLAYER_HEARTS", ["Current", "Abigail", "8"], false))] = true,
                    [SafeGameQuery.FactKey(new("PLAYER_HEARTS", ["Current", "Abigail", "10"], false))] = false,
                    [SafeGameQuery.FactKey(new("WEATHER", ["Target", "Rain"], false))] = false,
                    [SafeGameQuery.FactKey(new("WEATHER", ["Target", "Sun"], false))] = true
                }
            }
        };

        EncounterClue ClassifyKey(string rawKey, StoryKind kind = StoryKind.Heart, bool parallel = false, bool locationAvailable = true)
        {
            EventKey.TryGetId(rawKey, out string id);
            GalleryEvent entry = new(
                new ResolvedEvent(new EventIdentity("Data/Events/Town", id), "Town", rawKey, "none|0 0|Abigail 1 1 2|speak Abigail Hi|end",
                    new EventFragments([], []), "d", "s"),
                new EventOwnership(OwnershipKind.Direct, [new("Abigail", 2000)]))
            { Kind = kind };
            return EncounterClueClassifier.Classify(entry, parser.ParseRawKey(rawKey), baseState, evaluator, parallel, locationAvailable);
        }

        // Seen, opened If-route, Internal, missing location -> None
        Assert(ClassifyKey("10/f Abigail 500") == EncounterClue.None, "Already seen story yields EncounterClue.None");
        Assert(ClassifyKey("99/f Abigail 2000", parallel: true) == EncounterClue.None, "Opened If-route yields EncounterClue.None");
        Assert(ClassifyKey("99/f Abigail 2000", kind: StoryKind.Internal) == EncounterClue.None, "Internal flow yields EncounterClue.None");
        Assert(ClassifyKey("99/f Abigail 2000", locationAvailable: false) == EncounterClue.None, "Unavailable target location yields EncounterClue.None");

        // Unknown / random / custom GSQ -> None
        Assert(ClassifyKey("100/f Abigail 2000/r 0.5") == EncounterClue.None, "Random precondition yields EncounterClue.None");
        Assert(ClassifyKey("101/f Abigail 2000/G CustomMod.Condition 1") == EncounterClue.None, "Custom GSQ yields EncounterClue.None");
        Assert(ClassifyKey("102/f Abigail 2000/UnknownLegacyFlag") == EncounterClue.None, "Unknown precondition yields EncounterClue.None");

        // Unmet long-term progression gates -> None
        Assert(ClassifyKey("103/f Abigail 2500/w sunny") == EncounterClue.None, "Unmet friendship yields EncounterClue.None");
        Assert(ClassifyKey("104/f Abigail 2000/e 999") == EncounterClue.None, "Unmet prerequisite event yields EncounterClue.None");
        Assert(ClassifyKey("105/f Abigail 2000/n missingMail") == EncounterClue.None, "Unmet mail yields EncounterClue.None");
        Assert(ClassifyKey("106/f Abigail 2000/y 2") == EncounterClue.None, "Unmet year yields EncounterClue.None");
        Assert(ClassifyKey("107/G SEASON spring, PLAYER_HEARTS Current Abigail 10") == EncounterClue.None, "Mixed GSQ with unmet hearts yields EncounterClue.None");

        // Met long-term gates + mismatching calendar/weather -> ReadyForTiming
        Assert(ClassifyKey("201/f Abigail 2000/e 10/w rainy/t 900 1700") == EncounterClue.ReadyForTiming, "Rainy requirement on sunny day yields ReadyForTiming");
        Assert(ClassifyKey("202/f Abigail 2000/z spring") == EncounterClue.ReadyForTiming, "NotSeason spring in spring yields ReadyForTiming");
        Assert(ClassifyKey("203/f Abigail 2000/d Wed") == EncounterClue.ReadyForTiming, "NotDayOfWeek Wed on Wednesday yields ReadyForTiming");
        Assert(ClassifyKey("204/f Abigail 2000/u 15") == EncounterClue.ReadyForTiming, "Wrong day of month yields ReadyForTiming");
        Assert(ClassifyKey("205/G PLAYER_HEARTS Current Abigail 8, SEASON winter") == EncounterClue.ReadyForTiming, "GSQ met hearts + wrong season yields ReadyForTiming");
        Assert(ClassifyKey("206/G PLAYER_HEARTS Current Abigail 8, WEATHER Target Rain") == EncounterClue.ReadyForTiming, "GSQ met hearts + wrong weather yields ReadyForTiming");

        // Met long-term gates + matching daily environment + only transient Time / NpcVisibleHere / Tile pending -> AvailableToday
        Assert(ClassifyKey("301/f Abigail 2000/e 10/n guildMember/w sunny/t 1200 1800/p Abigail/a 12 18") == EncounterClue.AvailableToday,
            "Pending Time + NpcVisibleHere + Tile (outside location) with met long-term & weather yields AvailableToday");
        Assert(ClassifyKey("302/G PLAYER_HEARTS Current Abigail 8, SEASON spring, WEATHER Target Sun, TIME 1800 2200") == EncounterClue.AvailableToday,
            "GSQ met long-term & daily environment with pending TIME yields AvailableToday");
        Assert(ClassifyKey("303") == EncounterClue.AvailableToday, "Unseen story with no preconditions is AvailableToday");

        // Defect #2 regressions: world-wide 'v <Npc>' unmet -> None; passed daily time window -> ReadyForTiming; impossible time -> None!
        Assert(ClassifyKey("304/f Abigail 2000/v Abigail") == EncounterClue.None,
            "World-wide 'v <Npc>' when NPC is invisible/absent in world yields None, never AvailableToday");
        CurrentStateSnapshot lateNightState = baseState with { Time = 2030 };
        EncounterClue ClassifyAt(CurrentStateSnapshot snapshot, string rawKey)
        {
            EventKey.TryGetId(rawKey, out string id);
            GalleryEvent entry = new(
                new ResolvedEvent(new EventIdentity("Data/Events/Town", id), "Town", rawKey, "none|0 0|Abigail 1 1 2|speak Abigail Hi|end",
                    new EventFragments([], []), "d", "s"),
                new EventOwnership(OwnershipKind.Direct, [new("Abigail", 2000)])) { Kind = StoryKind.Heart };
            return EncounterClueClassifier.Classify(entry, parser.ParseRawKey(rawKey), snapshot, evaluator);
        }
        Assert(ClassifyAt(lateNightState, "305/f Abigail 2000/t 900 1700") == EncounterClue.ReadyForTiming,
            "Passed legacy time window (t 900 1700 at 20:30) degrades to ReadyForTiming");
        Assert(ClassifyAt(lateNightState, "306/G PLAYER_HEARTS Current Abigail 8, TIME 900 1700") == EncounterClue.ReadyForTiming,
            "Passed GSQ TIME window (TIME 900 1700 at 20:30) degrades to ReadyForTiming");
        CurrentStateSnapshot afternoonState = baseState with { Time = 1500 };
        Assert(ClassifyAt(afternoonState, "307/f Abigail 2000/!t 1200 2600") == EncounterClue.ReadyForTiming
            && ClassifyAt(afternoonState, "308/!G TIME 1200 2600") == EncounterClue.ReadyForTiming,
            "Negated time blocking remainder of day (!t / !G TIME 1200 2600 at 15:00) yields ReadyForTiming");
        Assert(ClassifyAt(afternoonState, "309/f Abigail 2000/!t 1200 1800") == EncounterClue.AvailableToday,
            "Negated time ending before 2600 (!t 1200 1800 at 15:00) remains AvailableToday");
        Assert(ClassifyAt(baseState, "310/t 1800 900") == EncounterClue.None
            && ClassifyAt(baseState, "311/t 100 500") == EncounterClue.None
            && ClassifyAt(baseState, "312/t 2700 2800") == EncounterClue.None
            && ClassifyAt(baseState, "313/!t 600 2600") == EncounterClue.None
            && ClassifyAt(baseState, "314/!G TIME 600 2600") == EncounterClue.None,
            "Permanently impossible positive or negated time intervals yield EncounterClue.None");
    }

    private static void CheckStoryQuotes(Func<string, string[]> splitArgs)
    {
        Dictionary<string, string> translations = new(StringComparer.Ordinal)
        {
            ["Strings\\Events:Emily_Dance"] = "$q 21/22 null#What did you think of my dance?$h#$r 21 30 A#Loved it!#$r 22 -10 B#Weird."
        };

        GalleryEvent heart = new(
            new ResolvedEvent(
                new EventIdentity("Data/Events/HaleyHouse", "2123243"),
                "HaleyHouse",
                "2123243/f Emily 2000",
                "emilyDance|0 0|Emily 5 5 2 Haley 6 5 2|speak Haley \"What is she doing?$u\"|speak Emily \"...$s\"|speak Emily \"Hi, @! ${Glad you came, guy^Glad you came, gal}$!$h#$b#Watch this!$12#$e#\"|end",
                new EventFragments([], []),
                "d", "s"),
            new EventOwnership(OwnershipKind.Direct, [new("Emily", 2000)])) { Kind = StoryKind.Heart };

        // Anti-spoiler check: locked unseen story MUST return null!
        Assert(StoryQuoteExtractor.GetVisibleQuote(heart, seen: false, unlockAll: false, parallelBranchUnlocked: false,
            s => s.Split('|'), splitArgs) is null, "Locked unseen story strictly hides StoryQuote");

        // Unlocked via seen, parallel If-route, or unlockAll:
        string? maleQuote = StoryQuoteExtractor.GetVisibleQuote(heart, seen: true, unlockAll: false, parallelBranchUnlocked: false,
            s => s.Split('|'), splitArgs, farmerName: "Leo", farmerIsMale: true);
        Assert(maleQuote == "Hi, Leo! Glad you came, guy!", $"Owner speaker prioritized, skips '...' for real sentence, strips $h/$b/$12/$e and applies male block (got '{maleQuote}')");

        string? femaleIfQuote = StoryQuoteExtractor.GetVisibleQuote(heart, seen: false, unlockAll: false, parallelBranchUnlocked: true,
            s => s.Split('|'), splitArgs, farmerName: "Robin", farmerIsMale: false);
        Assert(femaleIfQuote == "Hi, Robin! Glad you came, gal!", "Opened If-route exposes quote and applies female gender switch");

        // Translation reference + $q/$r stripping (never picks player's $r response option!):
        GalleryEvent translated = new(
            new ResolvedEvent(
                new EventIdentity("Data/Events/HaleyHouse", "2123244"),
                "HaleyHouse",
                "2123244/f Emily 2000",
                "emilyDance|0 0|Emily 5 5 2|speak Emily Strings\\Events:Emily_Dance|end",
                new EventFragments([], []),
                "d", "s"),
            new EventOwnership(OwnershipKind.Direct, [new("Emily", 2000)])) { Kind = StoryKind.Heart };
        string? qQuote = StoryQuoteExtractor.GetVisibleQuote(translated, seen: false, unlockAll: true, parallelBranchUnlocked: false,
            s => s.Split('|'), splitArgs, key => translations.GetValueOrDefault(key));
        Assert(qQuote == "What did you think of my dance?", $"Translation key resolved and $q/$r/$h stripped cleanly (got '{qQuote}')");

        // Missing translation key is skipped instead of leaking raw asset path:
        GalleryEvent missingTrans = new(
            new ResolvedEvent(
                new EventIdentity("Data/Events/Town", "900"),
                "Town",
                "900",
                "none|0 0|Lewis 1 1 2|speak Lewis Strings\\Events:Missing_Key|splitSpeak Lewis \"First split$a~Second split\"|end",
                new EventFragments([], []),
                "d", "s"),
            new EventOwnership(OwnershipKind.Excluded, [])) { Kind = StoryKind.Ordinary };
        string? splitQuote = StoryQuoteExtractor.GetVisibleQuote(missingTrans, seen: true, unlockAll: false, parallelBranchUnlocked: false,
            s => s.Split('|'), splitArgs, _ => null);
        Assert(splitQuote == "First split", $"Unresolved translation key skipped and splitSpeak extracted (got '{splitQuote}')");

        // $p conditional stripping:
        string pClean = StoryQuoteExtractor.ExtractSpokenSegments("$p 50#You remembered!$l|Oh, hello there!$u#$k").First();
        Assert(pClean == "You remembered!", $"$p conditional extracts spoken text cleanly (got '{pClean}')");

        // Defect #4 regressions: $v skip, $1 '...' fallback to subsequent text, leading '%' narration strip & priority,
        // Mods/...:Key unresolved filter, and quickQuestion (break) quote extraction!
        string vClean = StoryQuoteExtractor.ExtractSpokenSegments("$v 8888 0#Real line after $v!$h").First();
        Assert(vClean == "Real line after $v!", $"$v sub-event command skipped cleanly (got '{vClean}')");

        GalleryEvent dollarOneEvent = new(
            Event("Town", "910/f Abigail 1000", "none|0 0|Abigail 1 1 2|speak Abigail \"$1 mailSeen#...#$e#Subsequent line!$h\"|end"),
            new EventOwnership(OwnershipKind.Direct, [new("Abigail", 1000)])) { Kind = StoryKind.Heart };
        string? dollarOneQuote = StoryQuoteExtractor.GetVisibleQuote(dollarOneEvent, seen: true, unlockAll: false, parallelBranchUnlocked: false,
            s => s.Split('|'), splitArgs);
        Assert(dollarOneQuote == "Subsequent line!", $"$1 skips punctuation-only first segment to reach $e subsequent line (got '{dollarOneQuote}')");

        GalleryEvent narrationThenSpeech = new(
            Event("Town", "911/f Abigail 1000", "none|0 0|Abigail 1 1 2|speak Abigail \"%Abigail stares silently into the rain.\"|speak Abigail \"Mods/Author.Mod/Strings:MissingKey\"|speak Abigail \"Oh, hi there!\"|end"),
            new EventOwnership(OwnershipKind.Direct, [new("Abigail", 1000)])) { Kind = StoryKind.Heart };
        string? prioritizedSpeech = StoryQuoteExtractor.GetVisibleQuote(narrationThenSpeech, seen: true, unlockAll: false, parallelBranchUnlocked: false,
            s => s.Split('|'), splitArgs, _ => null);
        Assert(prioritizedSpeech == "Oh, hi there!", $"Spoken line prioritized over '%' narration and unresolved Mods/...:Key skipped (got '{prioritizedSpeech}')");

        GalleryEvent narrationOnly = new(
            Event("Town", "912/f Abigail 1000", "none|0 0|Abigail 1 1 2|speak Abigail \"%Abigail nods quietly.\"|end"),
            new EventOwnership(OwnershipKind.Direct, [new("Abigail", 1000)])) { Kind = StoryKind.Heart };
        string? strippedNarration = StoryQuoteExtractor.GetVisibleQuote(narrationOnly, seen: true, unlockAll: false, parallelBranchUnlocked: false,
            s => s.Split('|'), splitArgs);
        Assert(strippedNarration == "Abigail nods quietly.", $"Solo '%' narration strips leading '%' cleanly (got '{strippedNarration}')");

        Assert(StoryQuoteExtractor.CleanSpokenLine("%farm Farm looks peaceful.") == "%farm Farm looks peaceful.",
            "Dialogue.percentTokens (%farm) are never mistaken for narration '%'");

        GalleryEvent qqQuoteEvent = new(
            Event("Town", "913/f Abigail 1000", "none|0 0|Abigail 1 1 2|quickQuestion #Ready?#Yes#No(break)speak Abigail \"Let's head into the mines!$h\"\\playMusic Upper_Ambient(break)end|end"),
            new EventOwnership(OwnershipKind.Direct, [new("Abigail", 1000)])) { Kind = StoryKind.Heart };
        string? qqQuote = StoryQuoteExtractor.GetVisibleQuote(qqQuoteEvent, seen: true, unlockAll: false, parallelBranchUnlocked: false,
            s => s.Split('|'), splitArgs);
        Assert(qqQuote == "Let's head into the mines!", $"quickQuestion (break) speak line extracted (got '{qqQuote}')");

        // #16 Defect 1 regressions: throwing LoadStringReturnNullIfNotFound simulation on "...", "你好！", "注意:危险", "Mods/Missing:Key"!
        string? ThrowLikeNativeLoadString(string key)
        {
            int c = key.IndexOf(':');
            if (c < 0)
                throw new InvalidOperationException("parseStringPath called without colon: " + key);
            string asset = key[..c].Replace('/', '\\');
            if (!asset.Equals("Strings\\Events", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("ContentLoadException simulated for missing asset: " + asset);
            return translations.GetValueOrDefault(key);
        }

        GalleryEvent dotsOnly = new(
            Event("Town", "914/f Abigail 1000", "none|0 0|Abigail 1 1 2|speak Abigail \"...\"|end"),
            new EventOwnership(OwnershipKind.Direct, [new("Abigail", 1000)])) { Kind = StoryKind.Heart };
        Assert(StoryQuoteExtractor.GetVisibleQuote(dotsOnly, seen: true, unlockAll: false, parallelBranchUnlocked: false,
            s => s.Split('|'), splitArgs, ThrowLikeNativeLoadString) == "...",
            "No-colon '...' never triggers parseStringPath exception and returns '...'");

        GalleryEvent zhNoSpace = new(
            Event("Town", "915/f Abigail 1000", "none|0 0|Abigail 1 1 2|speak Abigail \"你好！\"|end"),
            new EventOwnership(OwnershipKind.Direct, [new("Abigail", 1000)])) { Kind = StoryKind.Heart };
        Assert(StoryQuoteExtractor.GetVisibleQuote(zhNoSpace, seen: true, unlockAll: false, parallelBranchUnlocked: false,
            s => s.Split('|'), splitArgs, ThrowLikeNativeLoadString) == "你好！",
            "No-space Chinese dialogue '你好！' never triggers parseStringPath exception");

        GalleryEvent zhWithColon = new(
            Event("Town", "916/f Abigail 1000", "none|0 0|Abigail 1 1 2|speak Abigail \"注意:危险\"|end"),
            new EventOwnership(OwnershipKind.Direct, [new("Abigail", 1000)])) { Kind = StoryKind.Heart };
        Assert(StoryQuoteExtractor.GetVisibleQuote(zhWithColon, seen: true, unlockAll: false, parallelBranchUnlocked: false,
            s => s.Split('|'), splitArgs, ThrowLikeNativeLoadString) == "注意:危险",
            "Colon-bearing literal '注意:危险' survives throwing Load<Dictionary> and returns literal text");

        GalleryEvent missingModTrans = new(
            Event("Town", "917/f Abigail 1000", "none|0 0|Abigail 1 1 2|speak Abigail \"Mods/Missing:Key\"|speak Abigail \"Fallback!\"|end"),
            new EventOwnership(OwnershipKind.Direct, [new("Abigail", 1000)])) { Kind = StoryKind.Heart };
        Assert(StoryQuoteExtractor.GetVisibleQuote(missingModTrans, seen: true, unlockAll: false, parallelBranchUnlocked: false,
            s => s.Split('|'), splitArgs, ThrowLikeNativeLoadString) == "Fallback!",
            "Missing mod asset 'Mods/Missing:Key' survives throwing Load<Dictionary> and is skipped without leaking key");

        EventFragments brokenTransFork = EventFragmentCollector.Collect(
            "none|0 0|Abigail 1 1 2|fork req1 noColonKey true|fork req2 Mods/Missing:Branch true",
            "Town", _ => new Dictionary<string, string>(), s => s.Split('|'), splitArgs, ThrowLikeNativeLoadString);
        Assert(brokenTransFork.MissingKeys.SequenceEqual(["noColonKey", "Mods/Missing:Branch"]),
            "EventFragmentCollector safely catches throwing translation fork keys and records them as missing");
    }

    private static void CheckMusicExtraction(Func<string, string[]> splitArgs)
    {
        ResolvedEvent headerSong = Event("Town", "1/f Leah 500", "breezy|0 0|Leah 1 1 2|playMusic sadpiano|end");
        Assert(EventMusicExtractor.ExtractCueId(headerSong, s => s.Split('|'), splitArgs) == "breezy", "Root music slot takes precedence when valid");

        ResolvedEvent fallbackPlayMusic = Event("Town", "2/f Leah 500", "continue|0 0|Leah 1 1 2|pause 500|playMusic MusicBoxSong|end");
        Assert(EventMusicExtractor.ExtractCueId(fallbackPlayMusic, s => s.Split('|'), splitArgs) == "MusicBoxSong",
            "'continue' root slot falls back to first playMusic command");

        ResolvedEvent qqMusic = Event("Town", "2b/f Leah 500", "none|0 0|Leah 1 1 2|quickQuestion #Play?#Yes#No(break)playMusic jaunty(break)end|end");
        Assert(EventMusicExtractor.ExtractCueId(qqMusic, s => s.Split('|'), splitArgs) == "jaunty",
            "playMusic nested inside quickQuestion (break) is extracted");

        ResolvedEvent samBand = Event("BusStop", "75/f Sam 2000", "none|0 0|Sam 1 1 2|playMusic samBand|end");
        Assert(EventMusicExtractor.ExtractCueId(samBand, s => s.Split('|'), splitArgs, new HashSet<string> { "78" }) == "shimmeringbastion"
            && EventMusicExtractor.ExtractCueId(samBand, s => s.Split('|'), splitArgs, new HashSet<string> { "79" }) == "honkytonky"
            && EventMusicExtractor.ExtractCueId(samBand, s => s.Split('|'), splitArgs, new HashSet<string> { "77" }) == "heavy"
            && EventMusicExtractor.ExtractCueId(samBand, s => s.Split('|'), splitArgs, new HashSet<string>()) == "poppy",
            "samBand resolves to exact 1.6.15 genre cues");

        ResolvedEvent silent = Event("Town", "3/f Leah 500", "none|0 0|Leah 1 1 2|playMusic rain_ambient|end");
        Assert(EventMusicExtractor.ExtractCueId(silent, s => s.Split('|'), splitArgs) is null, "none + ambient cue yields null BGM");
        Assert(EventMusicExtractor.FormatTrackTitle(null, _ => "Ignored", "No Music") == "No Music", "Null cue formats to localized none label");
        Assert(EventMusicExtractor.FormatTrackTitle("echos", cue => cue == "echos" ? "Echoes" : cue, "No Music") == "Echoes", "Mapped cue formats to jukebox title");
    }

    private static void CheckReplayBranchControl(Func<string, string[]> splitArgs)
    {
        Dictionary<string, string> translations = new(StringComparer.Ordinal)
        {
            ["Strings\\Events:SubQ"] = "Question?#$q 301/302 null#Ask#$r 301 0 A#Yes#$r 302 0 B#No"
        };
        Dictionary<string, string> scriptsByKey = new(StringComparer.Ordinal)
        {
            ["branchA"] = "speak Abigail Strings\\Events:SubQ|fork reqNested branchNested|end",
            ["branchNested"] = "speak Abigail \"Nested!$h\"|end",
            ["branchB"] = "speak Abigail \"#$q -1 null#Repeatable#$r -1 0 R#Ok\"|end",
            ["interactive"] = "speak Abigail \"Interactive\"|end"
        };
        ResolvedEvent complex = new(
            new EventIdentity("Data/Events/Town", "500"),
            "Town",
            "500/f Abigail 2000",
            "none|0 0|Abigail 1 1 2|speak Abigail \"#$q 201/202 null#Root#$r 201 10 A#One#$r 202 10 B#Two\"|quickQuestion #Ask?#Yes#No(break)speak Abigail \"#$q 401/402 null#QQ#$r 401 0 A#Y#$r 402 0 B#N\"(break)end|question fork1 \"#Left#Right\"|fork interactive|fork reqA branchA|fork reqB branchB|fork reqMissing missingKey|end",
            new EventFragments(
                [scriptsByKey["branchA"], scriptsByKey["branchNested"], scriptsByKey["branchB"], scriptsByKey["interactive"]],
                ["missingKey"],
                [
                    new("interactive", null, true),
                    new("branchA", null, true),
                    new("branchNested", null, true),
                    new("branchB", null, true),
                    new("missingKey", null, false)
                ],
                scriptsByKey),
            "d", "s");

        // 1. Inline non-"-1" $q/$r ID collection & selective masking (including quickQuestion sub-commands!):
        HashSet<string> inlineIds = ReplayBranchControl.CollectInlineQuestionIds(
            complex, s => s.Split('|'), splitArgs, key => translations.GetValueOrDefault(key));
        Assert(inlineIds.OrderBy(x => x).SequenceEqual(["201", "202", "301", "302", "401", "402"]),
            "Collects root, quickQuestion, fragment & translated non-'-1' $q/$r IDs while excluding '-1'");

        HashSet<string> liveAnswers = new(StringComparer.Ordinal) { "50_prior_event", "201", "302", "401", "999_other_npc" };
        int removed = ReplayBranchControl.MaskCurrentEventAnswers(liveAnswers, inlineIds);
        Assert(removed == 3 && liveAnswers.OrderBy(x => x).SequenceEqual(["50_prior_event", "999_other_npc"]),
            "Masks only current-event $q IDs while preserving prior-event answers");

        // 2. Intra-event fork option discovery & nested decision logic:
        IReadOnlyList<ReplayForkOption> options = ReplayBranchControl.ReadForkOptions(complex, s => s.Split('|'), splitArgs);
        // Expected: FollowSave, branchA, branchB, branchNested (BFS order: depth-1 forks before depth-2 nested!), MainScript.
        // Note: 1-arg 'fork interactive' and missing 'missingKey' are excluded!
        Assert(options.Count == 5
            && options[0].Mode == ReplayForkMode.FollowSave
            && options[1] is { Mode: ReplayForkMode.ForceBranch, TargetBranchKey: "branchA", RequiredId: "reqA" }
            && options[2] is { Mode: ReplayForkMode.ForceBranch, TargetBranchKey: "branchB", RequiredId: "reqB" }
            && options[3] is { Mode: ReplayForkMode.ForceBranch, TargetBranchKey: "branchNested", RequiredId: "reqNested" }
            && options[4].Mode == ReplayForkMode.MainScript,
            "ReadForkOptions builds FollowSave, valid explicit branches in BFS order, and MainScript while skipping 1-arg and missing forks");

        ReplayForkOption chooseNested = options[3];
        Assert(ReplayBranchControl.DecideFork(chooseNested, "Town", "reqA", "branchA") == ForkInterceptDecision.ForceJump
            && ReplayBranchControl.DecideFork(chooseNested, "Town", "reqNested", "branchNested") == ForkInterceptDecision.ForceJump
            && ReplayBranchControl.DecideFork(chooseNested, "Town", "reqB", "branchB") == ForkInterceptDecision.ForceSkip,
            "Choosing nested branch jumps through parent branchA into branchNested and skips sibling branchB");

        ReplayForkOption chooseBranchAOnly = options[1];
        Assert(ReplayBranchControl.DecideFork(chooseBranchAOnly, "Town", "reqA", "branchA") == ForkInterceptDecision.ForceJump
            && ReplayBranchControl.DecideFork(chooseBranchAOnly, "Town", "reqNested", "branchNested") == ForkInterceptDecision.ForceSkip,
            "Choosing parent branchA jumps to branchA and skips nested branchNested");

        Assert(ReplayBranchControl.DecideFork(chooseNested, "Town", null, "interactive") == ForkInterceptDecision.Native
            && ReplayBranchControl.DecideFork(ReplayForkOption.MainScript, "Town", null, "interactive") == ForkInterceptDecision.Native,
            "1-arg interactive fork always keeps native specialEventVariable1 behavior");
        Assert(ReplayBranchControl.DecideFork(ReplayForkOption.MainScript, "Town", "reqA", "branchA") == ForkInterceptDecision.ForceSkip,
            "MainScript skips explicit save-flag forks");

        // 3. Defect #3A regression: Diamond switchEvent shared tail must NOT pollute branchC with branchA!
        Dictionary<string, IReadOnlyDictionary<string, string>> diamondAssets = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Town"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["branchA"] = "speak Abigail \"In branchA\"|switchEvent sharedTail",
                ["sharedTail"] = "fork reqC branchC|end",
                ["branchC"] = "speak Abigail \"In branchC\"|end"
            }
        };
        string diamondRoot = "none|0 0|Abigail 1 1 2|fork reqA branchA|switchEvent sharedTail";
        EventFragments diamondFragments = EventFragmentCollector.Collect(
            diamondRoot, "Town", loc => diamondAssets.GetValueOrDefault(loc), s => s.Split('|'), splitArgs, _ => null);
        ResolvedEvent diamondEvent = new(new EventIdentity("Data/Events/Town", "501"), "Town", "501", diamondRoot, diamondFragments, "d", "s");
        IReadOnlyList<ReplayForkOption> diamondOptions = ReplayBranchControl.ReadForkOptions(diamondEvent, s => s.Split('|'), splitArgs);
        ReplayForkOption chooseC = diamondOptions.Single(o => o.TargetBranchKey == "branchC");
        Assert(ReplayBranchControl.DecideFork(chooseC, "Town", "reqA", "branchA") == ForkInterceptDecision.ForceSkip
            && ReplayBranchControl.DecideFork(chooseC, "Town", "reqC", "branchC") == ForkInterceptDecision.ForceJump,
            "Diamond switchEvent sharedTail reaches branchC directly without forcing unrelated branchA");

        // 4. Defect #3B regression: Cross-location changeLocation missing/duplicate sub-keys & same-key multi-condition forks!
        Dictionary<string, IReadOnlyDictionary<string, string>> multiLocAssets = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Town"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["sharedSub"] = "speak Abigail \"Shared in Town\"|end",
                ["dupSub"] = "speak Abigail \"Town dupSub\"|end"
            },
            ["Beach"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["onlyInBeach"] = "speak Abigail \"Valid in Beach\"|end",
                ["dupSub"] = "speak Abigail \"Beach dupSub\"|fork reqBeachNested beachNested|end",
                ["beachNested"] = "speak Abigail \"Beach nested\"|end"
            }
        };
        string multiLocRoot = "none|0 0|Abigail 1 1 2|fork req1 sharedSub|fork req2 sharedSub|fork reqBroken onlyInBeach|fork reqTown dupSub|changeLocation Beach|fork reqValid onlyInBeach|fork reqBeach dupSub|end";
        EventFragments multiLocFragments = EventFragmentCollector.Collect(
            multiLocRoot, "Town", loc => multiLocAssets.GetValueOrDefault(loc), s => s.Split('|'), splitArgs, _ => null);
        ResolvedEvent multiLocEvent = new(new EventIdentity("Data/Events/Town", "502"), "Town", "502", multiLocRoot, multiLocFragments, "d", "s");
        IReadOnlyList<ReplayForkOption> multiLocOptions = ReplayBranchControl.ReadForkOptions(multiLocEvent, s => s.Split('|'), splitArgs);

        Assert(multiLocOptions.Any(o => o.TargetBranchKey == "onlyInBeach" && o.RequiredId == "reqValid" && o.TargetLocation == "Beach")
            && !multiLocOptions.Any(o => o.RequiredId == "reqBroken"),
            "Missing key in Town does not discard valid homonymous key after changeLocation Beach");

        ReplayForkOption chooseReq2Shared = multiLocOptions.Single(o => o.TargetBranchKey == "sharedSub" && o.RequiredId == "req2");
        Assert(ReplayBranchControl.DecideFork(chooseReq2Shared, "Town", "req1", "sharedSub") == ForkInterceptDecision.ForceSkip
            && ReplayBranchControl.DecideFork(chooseReq2Shared, "Town", "req2", "sharedSub") == ForkInterceptDecision.ForceJump,
            "Same branchKey under different requiredIds intercepts only the chosen (Location, RequiredId, BranchKey) edge");

        ReplayForkOption chooseBeachNested = multiLocOptions.Single(o => o.TargetBranchKey == "beachNested");
        Assert(ReplayBranchControl.DecideFork(chooseBeachNested, "Town", "reqTown", "dupSub") == ForkInterceptDecision.ForceSkip
            && ReplayBranchControl.DecideFork(chooseBeachNested, "Beach", "reqBeach", "dupSub") == ForkInterceptDecision.ForceJump
            && ReplayBranchControl.DecideFork(chooseBeachNested, "Beach", "reqBeachNested", "beachNested") == ForkInterceptDecision.ForceJump,
            "Cross-location homonymous dupSub skips Town's dupSub and jumps through Beach's dupSub into beachNested");
    }

    private static void CheckTheaterHud()
    {
        TheaterHudState hud = new();
        Assert(hud.Opacity == 1f && hud.IsVisible, "HUD starts visible");
        hud.Tick(100, 200); // initialize mouse
        for (int i = 0; i < TheaterHudState.IdleDelayTicks; i++)
            hud.Tick(100, 200);
        Assert(hud.Opacity == 1f, "HUD remains at full opacity through 3.0s idle threshold");

        for (int i = 0; i < TheaterHudState.FadeDurationTicks / 2; i++)
            hud.Tick(100, 200);
        Assert(hud.Opacity is > 0.4f and < 0.6f, $"HUD smoothly interpolates mid-fade (opacity={hud.Opacity:0.00})");

        for (int i = 0; i < TheaterHudState.FadeDurationTicks; i++)
            hud.Tick(100, 200);
        Assert(hud.Opacity == 0f && !hud.IsVisible, "HUD fades completely to 0 after idle + fade window");

        hud.Tick(104, 200); // mouse moved!
        Assert(hud.Opacity == 1f && hud.IsVisible, "Mouse movement immediately restores HUD opacity to 1.0");

        for (int i = 0; i < TheaterHudState.IdleDelayTicks + TheaterHudState.FadeDurationTicks; i++)
            hud.Tick(104, 200);
        Assert(hud.Opacity == 0f, "HUD fades out again on idle");
        hud.NotifyActivity(); // theater shortcut pressed!
        Assert(hud.Opacity == 1f && hud.IsVisible, "Theater key activity immediately restores HUD opacity to 1.0");
    }

    private static ResolvedEvent Event(string location, string rawKey, string script)
    {
        EventKey.TryGetId(rawKey, out string id);
        return new ResolvedEvent(
            new EventIdentity("Data/Events/" + location, id),
            location,
            rawKey,
            script,
            new EventFragments([], []),
            EventHashes.RootDefinition(rawKey, script),
            EventHashes.RootScript(script));
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("Stage 2 check failed: " + message);
        passed++;
    }
}
