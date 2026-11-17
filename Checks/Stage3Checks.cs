using System.Text;
using StardewGallery;

internal static class Stage3Checks
{
    private static readonly byte[] ValidPng1x1 = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a9d8AAAAASUVORK5CYII=");

    internal static void Run()
    {
        int assertions = 0;
        void Check(bool ok, string label)
        {
            assertions++;
            if (!ok)
                throw new InvalidOperationException("Stage3 check failed: " + label);
        }

        VerifyFavoriteStore(Check);
        VerifyWeddingMementoStoreAndPermissions(Check);
        VerifyAutoMementoRulesAndGate(Check);
        VerifyPhotoFolderRules(Check);
        VerifyTranslationParityIfAvailable(Check);

        Console.WriteLine($"Stage 3 pure checks passed ({assertions} assertions).");
    }

    private static void VerifyFavoriteStore(Action<bool, string> check)
    {
        var fs = new InMemoryStorageFileSystem();
        string root = Path.Combine(Path.GetTempPath(), "InMemoryGalleryUserData");
        List<string> warnings = [];

        var store = new FavoriteEventStore(root, warnings.Add, fs);
        EventIdentity town1 = new("Data\\Events\\Town", " 101 ");
        EventIdentity town1Canonical = new("Data/Events/Town", "101");
        EventIdentity beach2 = new("Data/Events/Beach", "202");

        check(!store.IsFavorite(town1) && store.All().Count == 0, "Empty store starts with no favorites");
        check(store.Toggle(town1) && store.IsFavorite(town1Canonical) && store.Revision == 1, "Toggle normalizes identity and increments revision");
        check(store.Set(beach2, true) && store.All().Count == 2, "Second favorite stored");
        check(!store.Set(WeddingMemento.IdentityFor("Abigail"), true) && !store.IsFavorite(WeddingMemento.IdentityFor("Abigail")),
            "Wedding memento identities are excluded from FavoriteEventStore");

        // Exercise >5 writes to verify atomic replacement and 5-backup cap in virtual archive/.
        for (int i = 0; i < 7; i++)
        {
            EventIdentity temp = new("Data/Events/Forest", (300 + i).ToString());
            check(store.Set(temp, true), $"Write favorite #{i}");
        }
        string archiveDir = Path.Combine(root, "archive");
        int backupCount = fs.EnumerateFiles(archiveDir, "favorite-events-*.json").Count();
        check(backupCount == FavoriteEventStore.MaxBackups, $"Favorite backups capped at {FavoriteEventStore.MaxBackups} (got {backupCount})");

        // Reload from clean instance
        var reloaded = new FavoriteEventStore(root, warnings.Add, fs);
        check(reloaded.IsFavorite(town1Canonical) && reloaded.IsFavorite(beach2) && !reloaded.IsReadOnly,
            "Reloaded store reads persisted favorites cleanly");

        // Corrupt main file AND corrupt newest backup -> must cascade to second-newest valid backup!
        string mainFile = Path.Combine(root, "favorite-events.json");
        string newestBackup = fs.EnumerateFiles(archiveDir, "favorite-events-*.json")
            .OrderByDescending(p => fs.GetLastWriteTimeUtc(p))
            .ThenByDescending(Path.GetFileName, StringComparer.Ordinal)
            .First();
        fs.OverwriteRaw(mainFile, Encoding.UTF8.GetBytes("{corrupted-json"));
        fs.OverwriteRaw(newestBackup, Encoding.UTF8.GetBytes("{\"Version\":1,\"Favorites\":[{\"AssetName\":\"\",\"EventId\":\"\"}]}"));

        warnings.Clear();
        var recovered = new FavoriteEventStore(root, warnings.Add, fs);
        check(recovered.IsFavorite(town1Canonical) && !recovered.IsReadOnly && warnings.Count == 1,
            "Corrupted favorite-events.json falls back past damaged backup to newest valid backup");

        // Future schema version protection: must enter ReadOnly and leave Version 2 file byte-for-byte untouched!
        byte[] futureBytes = Encoding.UTF8.GetBytes("{\"Version\":2,\"Favorites\":[{\"AssetName\":\"Data/Events/Town\",\"EventId\":\"999\"}],\"ExtraField\":true}");
        fs.OverwriteRaw(mainFile, futureBytes);
        warnings.Clear();
        var futureStore = new FavoriteEventStore(root, warnings.Add, fs);
        check(futureStore.IsReadOnly && futureStore.All().Count == 0, "Future schema version enters read-only mode");
        check(!futureStore.Toggle(town1Canonical) && !futureStore.Set(beach2, true), "Read-only store blocks mutations");
        check(fs.ReadAllBytes(mainFile).AsSpan().SequenceEqual(futureBytes), "Future schema file stays byte-for-byte untouched");

        // Adoption of retired Trailer_Big copies
        var adoptFs = new InMemoryStorageFileSystem();
        var adoptStore = new FavoriteEventStore(root, warnings.Add, adoptFs);
        EventIdentity formerTrailer = new("Data\\Events\\Trailer_Big", "503180");
        EventIdentity currentTrailer = new("Data/Events/Trailer", "503180");
        check(adoptStore.Set(formerTrailer, true), "Seed retired Trailer_Big favorite");
        check(adoptStore.Adopt(currentTrailer, formerTrailer), "Adopt moves favorite from Trailer_Big to Trailer");
        check(adoptStore.IsFavorite(currentTrailer) && !adoptStore.IsFavorite(formerTrailer), "Adopted favorite now lives under Trailer");
    }

    private static void VerifyWeddingMementoStoreAndPermissions(Action<bool, string> check)
    {
        var fs = new InMemoryStorageFileSystem();
        string root = Path.Combine(Path.GetTempPath(), "InMemoryGalleryPhotos");
        List<string> warnings = [];
        SaveProfileKey profile = new(0x1234UL, 0x5678L);
        var store = new EventPhotoStore(root, profile, warnings.Add, fs);

        EventIdentity normalStory = new("Data/Events/SeedShop", "1");
        store.Add(normalStory, ValidPng1x1, ValidPng1x1);
        string normalIndexJson = fs.ReadAllText(Path.Combine(store.EventDirectory(normalStory), "index.json"));
        check(!normalIndexJson.Contains("WeddingDate", StringComparison.Ordinal), "Normal event index.json omits WeddingDate for backward compatibility");

        EventIdentity abigailWedding = WeddingMemento.IdentityFor(" Abigail ");
        check(WeddingMemento.IsWeddingIdentity(abigailWedding) && WeddingMemento.TryGetSpouseName(abigailWedding) == "Abigail",
            "Wedding identity normalizes spouse name");

        EventPhoto firstWeddingPhoto = store.Add(abigailWedding, ValidPng1x1, ValidPng1x1, new WeddingDateRecord(2, "Fall", 14));
        EventPhoto secondWeddingPhoto = store.Add(abigailWedding, ValidPng1x1, ValidPng1x1);

        var freshStore = new EventPhotoStore(root, profile, warnings.Add, fs);
        var discovered = freshStore.ReadWeddingMemories();
        check(discovered.Count == 1 && discovered[0].Identity == abigailWedding, "ReadWeddingMemories discovers spouse wedding and ignores normal events");
        check(discovered[0].Photos.WeddingDate == new WeddingDateRecord(2, "fall", 14), "WeddingDate normalizes season and persists across reload");
        check(discovered[0].Photos.CoverId == firstWeddingPhoto.Id && discovered[0].Photos.Photos.Count == 2, "Initial wedding capture becomes cover");

        // Cover switch, reset, and archive on Wedding Memento
        freshStore.SetCover(abigailWedding, secondWeddingPhoto.Id);
        check(freshStore.Read(abigailWedding).CoverId == secondWeddingPhoto.Id, "Wedding memento supports SetCover");
        freshStore.SetCover(abigailWedding, null);
        check(freshStore.Read(abigailWedding).CoverId is null, "Wedding memento supports restoring default cover");
        freshStore.Archive(abigailWedding, firstWeddingPhoto.Id);
        check(freshStore.Read(abigailWedding).Photos.Count == 1 && freshStore.Read(abigailWedding).WeddingDate == new WeddingDateRecord(2, "fall", 14),
            "Archiving a wedding photo preserves WeddingDate metadata");

        // Corrupt wedding index.json on disk -> ReadWeddingMemories must still discover and recover from archive backup!
        string weddingIndex = Path.Combine(freshStore.EventDirectory(abigailWedding), "index.json");
        fs.OverwriteRaw(weddingIndex, Encoding.UTF8.GetBytes("{truncated"));
        var backupRecoveredStore = new EventPhotoStore(root, profile, warnings.Add, fs);
        var recoveredWeddings = backupRecoveredStore.ReadWeddingMemories();
        check(recoveredWeddings.Count == 1 && recoveredWeddings[0].Photos.WeddingDate == new WeddingDateRecord(2, "fall", 14),
            "Corrupted wedding index.json recovers via archive backup during ReadWeddingMemories");

        // ReplayAccessPolicy explicitly blocks wedding mementos even under unlockAll
        ReplayAccess weddingAccess = ReplayAccessPolicy.Evaluate(
            StoryKind.Heart,
            seen: true,
            unlockAll: true,
            ordinaryEnabled: true,
            isWedding: true);
        check(!weddingAccess.Allowed && weddingAccess.ReasonKey == "replay.wedding-unavailable"
            && ReplayAccessPolicy.EvaluateWedding() == weddingAccess,
            "Wedding mementos are strictly non-replayable");

        // Denominator check: Wedding/Abigail never inflates Abigail's HeartEventsFor count for unmarried or married saves
        GalleryCatalog catalog = BuildTestCatalog();
        check(catalog.HeartEventsFor("Abigail").Count == 1 && catalog.Find(abigailWedding) is null,
            "Wedding memento never enters catalog heart-event denominators");
    }

    private static void VerifyAutoMementoRulesAndGate(Action<bool, string> check)
    {
        GalleryCatalog catalog = BuildTestCatalog();

        check(AutoMementoRules.ResolveLiveStoryIdentity(catalog, "Data\\Events\\SeedShop", "SeedShop", "1")
            == new EventIdentity("Data/Events/SeedShop", "1"), "Direct fromAssetName resolves Heart story");
        check(AutoMementoRules.ResolveLiveStoryIdentity(catalog, null, "Town", "200")
            == new EventIdentity("Data/Events/Town", "200"), "Fallback currentLocationName resolves Ordinary story");
        check(AutoMementoRules.ResolveLiveStoryIdentity(catalog, "Data\\Events\\Trailer_Big", "Trailer_Big", "503180")
            == new EventIdentity("Data/Events/Trailer", "503180"), "Merged Trailer_Big event resolves to Data/Events/Trailer");
        check(AutoMementoRules.ResolveLiveStoryIdentity(catalog, "Data\\Events\\Mountain", "Mountain", "558291")
            == new EventIdentity("Data/Events/FarmHouse", "558291"), "Grandpa global copy 558291 resolves to Data/Events/FarmHouse");
        check(AutoMementoRules.ResolveLiveStoryIdentity(catalog, "Data/Events/Town", "Town", "999") is null,
            "Internal transition events are excluded from auto-memento");
        check(AutoMementoRules.ResolveLiveStoryIdentity(catalog, "Data/Events/Town", "Town", "-2") is null,
            "Synthetic -2 ID is excluded from normal story lookup");

        // Cold-start single-asset resolver: loads only targeted asset(s) without scanning unrelated locations
        GalleryCatalogBuilder singleBuilder = new(
            key => key.Split('/'),
            raw => raw.Split('/'),
            cmd => cmd.Split(' ', StringSplitOptions.RemoveEmptyEntries),
            pos => pos.Split(' ', StringSplitOptions.RemoveEmptyEntries),
            () => null);
        Dictionary<string, IReadOnlyDictionary<string, string>> mockAssets = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Data/Events/SeedShop"] = new Dictionary<string, string>
            {
                ["1/f Abigail 500"] = "none/64 15/farmer 64 15 2 Abigail 65 15 3/speak Abigail \"Hi!\"/end",
                ["999/t 600 700"] = "none/-100 -100/farmer 5 5 2/mail flagOnly/end"
            },
            ["Data/Events/Trailer"] = new Dictionary<string, string>
            {
                ["503180/t 900 1800"] = "none/10 10/farmer 10 10 2/speak Pam \"Welcome.\"/end"
            },
            ["Data/Events/Trailer_Big"] = new Dictionary<string, string>
            {
                ["999999/t 900 1800"] = "none/10 10/farmer 10 10 2/speak Penny \"Upgraded home!\"/end"
            },
            ["Data/Events/FarmHouse"] = new Dictionary<string, string>
            {
                ["558291/y 3"] = "none/10 10/farmer 10 10 2/speak Grandpa \"Well done.\"/end"
            },
            ["Data/Events/Mountain"] = new Dictionary<string, string>
            {
                ["558291/y 3"] = "none/10 10/farmer 10 10 2/speak Grandpa \"Well done.\"/end"
            }
        };
        List<string> loadedSingleAssets = [];
        IReadOnlyDictionary<string, string>? LoadSingle(string asset)
        {
            string norm = asset.Replace('\\', '/');
            if (norm.Equals("Data/Events/UnrelatedHeavyPack", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Cold-start auto-memento must never scan unrelated event assets!");
            loadedSingleAssets.Add(norm);
            return mockAssets.GetValueOrDefault(norm);
        }
        EventIdentity? ResolveCold(string? asset, string? loc, string id, bool upgraded = true)
            => AutoMementoRules.ResolveSingleAssetStoryIdentity(
                asset, loc, id, upgraded, LoadSingle,
                (_, _) => true, singleBuilder.IsInternalFlow,
                raw => raw.Split('/'),
                cmd => cmd.Split(' ', StringSplitOptions.RemoveEmptyEntries),
                _ => null);

        loadedSingleAssets.Clear();
        check(ResolveCold("Data\\Events\\SeedShop", "SeedShop", "1") == new EventIdentity("Data/Events/SeedShop", "1")
            && loadedSingleAssets.Count == 1 && loadedSingleAssets[0] == "Data/Events/SeedShop",
            "Cold-start single-asset resolver loads only Data/Events/SeedShop (1 asset load)");
        check(ResolveCold("Data/Events/SeedShop", "SeedShop", "999") is null,
            "Cold-start single-asset resolver excludes Internal transition script");
        check(ResolveCold("Data\\Events\\Trailer_Big", "Trailer_Big", "503180", upgraded: true) == new EventIdentity("Data/Events/Trailer", "503180"),
            "Cold-start single-asset resolver maps merged Trailer_Big event 503180 to Data/Events/Trailer");
        check(ResolveCold("Data\\Events\\Trailer_Big", "Trailer_Big", "999999", upgraded: true) == new EventIdentity("Data/Events/Trailer_Big", "999999"),
            "Cold-start single-asset resolver keeps Trailer_Big-native event under Data/Events/Trailer_Big");
        check(ResolveCold("Data\\Events\\Mountain", "Mountain", "558291") == new EventIdentity("Data/Events/FarmHouse", "558291"),
            "Cold-start single-asset resolver maps Grandpa global copy 558291 to Data/Events/FarmHouse");

        check(AutoMementoRules.IsLocalVillagerWedding(true, false, 42L, 42L, false, false, "Haley", out string spouse) && spouse == "Haley",
            "Local player's villager wedding is accepted");
        check(!AutoMementoRules.IsLocalVillagerWedding(true, false, 99L, 42L, false, false, "Haley", out _),
            "Co-op peer farmer's wedding is rejected");
        check(!AutoMementoRules.IsLocalVillagerWedding(true, false, 42L, 42L, hasRoommate: true, false, "Krobus", out _),
            "Roommate move-in is rejected");
        check(!AutoMementoRules.IsLocalVillagerWedding(true, false, 42L, 42L, false, hasFarmerSpouse: true, "OtherPlayer", out _),
            "Player-to-player wedding is rejected");

        check(AutoMementoRules.IsTransitionClear(false, false, 0f, false, false, false), "Clear post-GlobalFadeToClear state passes");
        check(!AutoMementoRules.IsTransitionClear(false, false, 0f, false, fadeToBlack: true, false), "Active FadeScreenToBlack at alpha 0 blocks");
        check(!AutoMementoRules.IsTransitionClear(false, isWarping: true, 0f, false, false, false), "Active warp blocks");

        check(AutoMementoRules.IsStageViewportVisible(-448, -92, 1920, 1080, 1024, 896),
            "Centered small indoor room with negative margin is visible");
        check(!AutoMementoRules.IsStageViewportVisible(-6400, -6400, 1920, 1080, 1024, 896),
            "Hidden offscreen viewport -100 -100 is rejected");
        check(!AutoMementoRules.IsStageViewportVisible(-64000, -64000, 1920, 1080, null, null),
            "Hidden offscreen viewport -1000 -1000 without map bounds is rejected");

        var gate = new AutoMementoGate();
        object eventInstance = new();
        // Fading frames reset settled counter
        for (int i = 0; i < 5; i++)
            check(!gate.ShouldRequestCapture(eventInstance, true, true, false, 0, transitionClear: true, stageVisible: true, dialogueUp: false), "Pre-fade settle");
        check(!gate.ShouldRequestCapture(eventInstance, true, true, false, 0, transitionClear: false, stageVisible: true, dialogueUp: true), "Mid-scene fade resets counter");
        check(gate.SettledTicks == 0, "SettledTicks reset to 0 on transition");

        // Now settle for MinPostFadeTicks with DialogueBox open -> triggers once!
        bool fired = false;
        for (int i = 0; i < AutoMementoGate.MinPostFadeTicks; i++)
        {
            fired = gate.ShouldRequestCapture(eventInstance, true, true, false, 0, transitionClear: true, stageVisible: true, dialogueUp: true);
            if (i < AutoMementoGate.MinPostFadeTicks - 1)
                check(!fired, $"Waits until tick {AutoMementoGate.MinPostFadeTicks}");
        }
        check(fired, "Triggers silent capture on settled frame with dialogue up");

        // Even if capture failed (existingPhotoCount remains 0), same Event instance MUST NOT retry-loop!
        for (int i = 0; i < 60; i++)
            check(!gate.ShouldRequestCapture(eventInstance, true, true, false, 0, transitionClear: true, stageVisible: true, dialogueUp: true), "Never retry-loops same Event instance");

        // Dialogue-free cutscene triggers at FallbackSettleTicks on a new Event instance
        object wordlessEvent = new();
        bool wordlessFired = false;
        for (int i = 0; i < AutoMementoGate.FallbackSettleTicks; i++)
            wordlessFired = gate.ShouldRequestCapture(wordlessEvent, true, true, false, 0, transitionClear: true, stageVisible: true, dialogueUp: false);
        check(wordlessFired, "Wordless cutscene triggers once at FallbackSettleTicks");

        // Event that already has photos never triggers
        object alreadyCapturedEvent = new();
        for (int i = 0; i < 50; i++)
            check(!gate.ShouldRequestCapture(alreadyCapturedEvent, true, true, false, existingPhotoCount: 1, transitionClear: true, stageVisible: true, dialogueUp: true),
                "Existing photo skips auto-memento");
    }

    private static void VerifyPhotoFolderRules(Action<bool, string> check)
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "GalleryFolderRuleRoot", "event-photos"));
        string profile = Path.Combine(root, "0000000000000001-0000000000000002");
        string eventDir = Path.Combine(profile, "ABCDEF");
        string traversal = Path.Combine(root, "..", "saves");
        string siblingPrefix = root + "-other";

        check(PhotoFolderRules.IsContainedOrEqual(eventDir, root), "Event subdirectory is contained in root");
        check(PhotoFolderRules.IsContainedOrEqual(root, root), "Root itself is accepted");
        check(!PhotoFolderRules.IsContainedOrEqual(traversal, root), "Parent traversal .. is rejected");
        check(!PhotoFolderRules.IsContainedOrEqual(siblingPrefix, root), "Sibling prefix directory is rejected");

        HashSet<string> existing = new(StringComparer.OrdinalIgnoreCase) { root, profile };
        check(PhotoFolderRules.ResolveExistingTargetDirectory(root, profile, eventDir, existing.Contains) == profile,
            "Falls back from missing eventDir to existing profileDir");
        existing.Remove(profile);
        check(PhotoFolderRules.ResolveExistingTargetDirectory(root, profile, eventDir, existing.Contains) == root,
            "Falls back from missing profileDir to existing rootDir");
        check(PhotoFolderRules.ResolveExistingTargetDirectory(root, traversal, traversal, existing.Contains) == root,
            "Traversal paths are ignored and fall back safely to rootDir");

        string trickyPath = Path.Combine(root, "folder with spaces & 'quotes'");
        var winPsi = PhotoFolderRules.BuildStartInfo(trickyPath, isWindows: true, isMacOS: false, isLinux: false)!;
        check(!winPsi.UseShellExecute && winPsi.FileName == "explorer.exe"
            && winPsi.ArgumentList.Count == 1 && winPsi.ArgumentList[0] == trickyPath && string.IsNullOrEmpty(winPsi.Arguments),
            "Windows ProcessStartInfo uses explorer.exe with ArgumentList and UseShellExecute=false");

        var macPsi = PhotoFolderRules.BuildStartInfo(trickyPath, isWindows: false, isMacOS: true, isLinux: false)!;
        check(!macPsi.UseShellExecute && macPsi.FileName == "/usr/bin/open" && macPsi.ArgumentList.Single() == trickyPath,
            "macOS ProcessStartInfo uses /usr/bin/open with ArgumentList");

        var linuxPsi = PhotoFolderRules.BuildStartInfo(trickyPath, isWindows: false, isMacOS: false, isLinux: true)!;
        check(!linuxPsi.UseShellExecute && linuxPsi.FileName == "xdg-open" && linuxPsi.ArgumentList.Single() == trickyPath,
            "Linux ProcessStartInfo uses xdg-open with ArgumentList");

        check(PhotoFolderRules.IsLinuxHandheldEnvironment("gamescope", null)
            && PhotoFolderRules.IsLinuxHandheldEnvironment(null, "1")
            && !PhotoFolderRules.IsLinuxHandheldEnvironment("KDE", "0")
            && !PhotoFolderRules.IsLinuxHandheldEnvironment("KDE", "1"),
            "Linux handheld / gamescope environment detection allows Steam Deck KDE Desktop Mode");

        check(!PhotoFolderRules.ShouldUseHandheldFallback(isLinux: true, xdgCurrentDesktop: "KDE", steamDeckEnv: "1", isSteamBigPicture: false, isSteamOnDeck: true)
            && !PhotoFolderRules.ShouldUseHandheldFallback(isLinux: true, xdgCurrentDesktop: "GNOME", steamDeckEnv: "1", isSteamBigPicture: false, isSteamOnDeck: true),
            "Steam Deck in KDE/GNOME Desktop Mode (Big Picture off) opens folder normally instead of handheld fallback");
        check(PhotoFolderRules.ShouldUseHandheldFallback(isLinux: true, xdgCurrentDesktop: "gamescope", steamDeckEnv: "1", isSteamBigPicture: false, isSteamOnDeck: true)
            && PhotoFolderRules.ShouldUseHandheldFallback(isLinux: true, xdgCurrentDesktop: "", steamDeckEnv: "1", isSteamBigPicture: false, isSteamOnDeck: true)
            && PhotoFolderRules.ShouldUseHandheldFallback(isLinux: true, xdgCurrentDesktop: "KDE", steamDeckEnv: "1", isSteamBigPicture: true, isSteamOnDeck: true)
            && PhotoFolderRules.ShouldUseHandheldFallback(isLinux: false, xdgCurrentDesktop: null, steamDeckEnv: null, isSteamBigPicture: true, isSteamOnDeck: false)
            && !PhotoFolderRules.ShouldUseHandheldFallback(isLinux: false, xdgCurrentDesktop: null, steamDeckEnv: "1", isSteamBigPicture: false, isSteamOnDeck: true),
            "ShouldUseHandheldFallback triggers only in Big Picture or gamescope / sessionless Deck Game Mode");
    }

    private static void VerifyTranslationParityIfAvailable(Action<bool, string> check)
    {
        string? dir = FindI18nDirectory();
        if (dir is null)
            return;

        string[] files = Directory.GetFiles(dir, "*.json");
        check(files.Length == 12, $"All 12 i18n JSON files present ({files.Length})");
        var defaultDoc = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(dir, "default.json")))!;
        string[] requiredStage3Keys =
        [
            "config.auto-photo.name", "config.auto-photo.tooltip",
            "favorite.add", "favorite.active", "favorite.readonly",
            "photo.open-folder", "photo.folder-path", "photo.folder-opened", "photo.folder-fallback", "photo.folder-handheld",
            "wedding.title", "wedding.subtitle", "replay.wedding-unavailable"
        ];
        foreach (string key in requiredStage3Keys)
            check(defaultDoc.ContainsKey(key), $"default.json contains Stage 3 key {key}");

        foreach (string file in files)
        {
            var doc = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file))!;
            check(doc.Count == defaultDoc.Count && defaultDoc.Keys.All(doc.ContainsKey),
                $"Locale {Path.GetFileName(file)} matches default.json key set");
        }
    }

    private static string? FindI18nDirectory()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, "i18n");
            if (File.Exists(Path.Combine(candidate, "default.json")))
                return candidate;
        }
        return null;
    }

    private static GalleryCatalog BuildTestCatalog()
    {
        EventFragments empty = new([], []);
        GalleryEvent Make(string asset, string loc, string key, StoryKind kind, EventOwner[] owners)
        {
            EventKey.TryGetId(key, out string id);
            ResolvedEvent resolved = new(
                new EventIdentity(asset, id),
                loc,
                key,
                "none/64 15/farmer 64 15 2/skippable/end",
                empty,
                EventHashes.RootDefinition(key, "s"),
                EventHashes.RootScript("s"));
            EventOwnership ownership = new(
                kind == StoryKind.Heart ? OwnershipKind.Direct : OwnershipKind.Excluded,
                owners,
                kind != StoryKind.Heart ? "non-heart" : null);
            return new GalleryEvent(resolved, ownership) { Kind = kind };
        }

        GalleryCharacter abigail = new("Abigail", "Abigail", true, 2000);
        GalleryEvent heart = Make("Data/Events/SeedShop", "SeedShop", "1/f Abigail 500", StoryKind.Heart, [new EventOwner("Abigail", 500)]);
        GalleryEvent ordinary = Make("Data/Events/Town", "Town", "200/t 600 1800", StoryKind.Ordinary, []);
        GalleryEvent trailer = Make("Data/Events/Trailer", "Trailer_Big", "503180/t 900 1800", StoryKind.Ordinary, []);
        GalleryEvent grandpa = Make("Data/Events/FarmHouse", "FarmHouse", "558291/y 3", StoryKind.Ordinary, []);
        GalleryEvent internalStep = Make("Data/Events/Town", "Town", "999/t 600 700", StoryKind.Internal, []);

        return new GalleryCatalog(
            [abigail],
            [heart],
            [ordinary, trailer, grandpa, internalStep]);
    }
}
