using System.Collections;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;

/// <summary>
/// Non-destructive Stage 3 runtime checks against 1.6.15 Stardew Valley.dll and built StardewGallery.dll.
/// Uses InMemoryStorageFileSystem exclusively; never writes or deletes temporary directories on disk.
/// </summary>
internal static class Stage3RuntimeChecks
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static Assembly mod = null!;
    private static int assertions;

    internal static void Run(Assembly modAssembly, string gameDirectory)
    {
        mod = modAssembly;
        assertions = 0;
        var harmony = new Harmony("Gallery.Stage3RuntimeChecks");
        try
        {
            Game1.game1 = (Game1)FormatterServices.GetUninitializedObject(typeof(Game1));
            Game1.options = (Options)FormatterServices.GetUninitializedObject(typeof(Options));
            Game1.options.gamepadControls = true;
            Game1.options.snappyMenus = true;
            foreach (FieldInfo field in typeof(Options).GetFields(Flags).Where(f => f.FieldType == typeof(InputButton[])))
                field.SetValue(Game1.options, Array.Empty<InputButton>());
            typeof(Game1).GetField("screenFade", BindingFlags.NonPublic | BindingFlags.Static)!
                .SetValue(null, new StardewValley.BellsAndWhistles.ScreenFade(() => true, () => { }));
            Game1.content = new LocalizedContentManager(new GameServiceContainer(), "");
            typeof(Game1).GetField("multiplayer", Flags)?.SetValue(null, new Multiplayer());

            foreach (MethodInfo method in new[]
            {
                typeof(PlayerStatusList).GetMethod("AddSpriteDefinition", Flags)!,
                typeof(Farmer).GetMethod("farmerInit", Flags)!,
                typeof(AnimatedSprite).GetMethod("LoadTexture", Flags)!
            })
                harmony.Patch(method, prefix: new HarmonyMethod(typeof(Stage3RuntimeChecks), nameof(SkipVoid)));

            foreach (MethodInfo sound in typeof(Game1).GetMethods(Flags).Where(m => m.Name == "playSound"))
                harmony.Patch(sound, prefix: new HarmonyMethod(typeof(Stage3RuntimeChecks), nameof(RecordPlaySound)));
            harmony.Patch(typeof(IClickableMenu).GetMethod("snapCursorToCurrentSnappedComponent", Flags)!,
                prefix: new HarmonyMethod(typeof(Stage3RuntimeChecks), nameof(SkipVoid)));
            harmony.Patch(Type("GalleryEventDetailMenu").GetMethod("RecalculateLayout", Flags)!,
                prefix: new HarmonyMethod(typeof(Stage3RuntimeChecks), nameof(SkipVoid)));
            harmony.Patch(Type("GalleryEventDetailMenu").GetMethod("CanReplay", Flags)!,
                prefix: new HarmonyMethod(typeof(Stage3RuntimeChecks), nameof(ReturnFalse)));
            harmony.Patch(typeof(NPC).GetMethod(nameof(NPC.getTextureNameForCharacter), Flags)!,
                prefix: new HarmonyMethod(typeof(Stage3RuntimeChecks), nameof(IdentityTextureName)));

            VerifyNativeWeddingEventAndSilentCapture();
            VerifyCatalogCacheLightweightAutoMementoLookup();
            VerifyPhotoFolderSteamworksIsolation();
            VerifyHdPortraitsRuntimeIntegration();
            VerifyDetailFavoriteAndPhotoMenuControls();
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }

        Console.WriteLine($"Stage 3 runtime checks passed ({assertions} assertions).");
    }

    private static int soundsPlayed;
    private static bool RecordPlaySound() { soundsPlayed++; return false; }
    private static bool SkipVoid() => false;
    private static bool ReturnFalse(ref bool __result) { __result = false; return false; }
    private static bool IdentityTextureName(string __0, ref string __result) { __result = __0; return false; }

    private static void VerifyNativeWeddingEventAndSilentCapture()
    {
        Farmer localFarmer = new() { UniqueMultiplayerID = 1001L };
        Farmer peerFarmer = new() { UniqueMultiplayerID = 2002L };
        typeof(Game1).GetProperty("player", Flags)!.SetValue(null, localFarmer);

        // Construct via the exact 1.6.15 Event(script, assetName, eventId, farmerActor) ctor used by Utility.getWeddingEvent.
        Event localWedding = new("continue", null, "-2", localFarmer);
        Event peerWedding = new("continue", null, "-2", peerFarmer);
        GC.SuppressFinalize(localWedding);
        GC.SuppressFinalize(peerWedding);
        Check(localWedding.isWedding && localWedding.id == "-2" && ReferenceEquals(localWedding.farmer, localFarmer),
            "1.6.15 Event(id: -2, farmer) sets isWedding=true and preserves Event.farmer");

        object?[] argsLocal = [localWedding.isWedding, localWedding.isFestival, localWedding.farmer?.UniqueMultiplayerID, localFarmer.UniqueMultiplayerID, false, false, "Abigail", null];
        bool okLocal = (bool)Type("AutoMementoRules").GetMethod("IsLocalVillagerWedding", Flags)!.Invoke(null, argsLocal)!;
        Check(okLocal && Equals(argsLocal[7], "Abigail"), "Real 1.6.15 wedding Event matches local villager wedding");

        object?[] argsPeer = [peerWedding.isWedding, peerWedding.isFestival, peerWedding.farmer?.UniqueMultiplayerID, localFarmer.UniqueMultiplayerID, false, false, "Abigail", null];
        bool okPeer = (bool)Type("AutoMementoRules").GetMethod("IsLocalVillagerWedding", Flags)!.Invoke(null, argsPeer)!;
        Check(!okPeer, "Co-op peer farmer's wedding Event is rejected");

        // Silent vs manual capture request behavior in GalleryPhotos
        IModHelper helper = DispatchProxy.Create<IModHelper, FakeProxy>();
        IMonitor monitor = DispatchProxy.Create<IMonitor, FakeProxy>();
        FakeProxy monitorProxy = (FakeProxy)(object)monitor;
        List<string> hudNotices = [];
        object photos = Activator.CreateInstance(Type("GalleryPhotos"), Flags, null,
            [helper, monitor, (Action<string, bool>)((key, _) => hudNotices.Add(key))], null)!;

        object inMemFs = Activator.CreateInstance(Type("InMemoryStorageFileSystem"), Flags, null, null, null)!;
        object profile = Activator.CreateInstance(Type("SaveProfileKey"), Flags, null, [1UL, 2L], null)!;
        object store = Activator.CreateInstance(Type("EventPhotoStore"), Flags, null,
            [Path.Combine(Path.GetTempPath(), "InMemRuntimePhotos"), profile, (Action<string>)(_ => { }), inMemFs], null)!;
        Set(photos, "store", store);

        Type patchType = Type("PhotoCapturePatches");
        PropertyInfo pendingProp = Type("GalleryPhotos").GetProperty("Pending", Flags)!;
        patchType.GetField("pending", Flags)!.SetValue(null, (Func<bool>)(() => (bool)pendingProp.GetValue(photos)!));
        MethodInfo afterBuffer = patchType.GetMethod("AfterShouldDrawOnBuffer", Flags)!;
        bool Buffer(bool orig) { object?[] a = [orig]; afterBuffer.Invoke(null, a); return (bool)a[0]!; }

        object weddingId = Type("WeddingMemento").GetMethod("IdentityFor", Flags)!.Invoke(null, ["Abigail"])!;
        object weddingDate = Activator.CreateInstance(Type("WeddingDateRecord"), Flags, null, [2, "spring", 7], null)!;

        soundsPlayed = 0;
        hudNotices.Clear();
        monitorProxy.Messages.Clear();
        localWedding.skippable = true;

        Type("GalleryPhotos").GetMethod("RequestSilent", Flags)!.Invoke(photos, [weddingId, localWedding, weddingDate]);
        Check(Buffer(false), "Silent auto-memento routes 100% zoom world pass through Game1.screen");
        Type("GalleryPhotos").GetMethod("PrepareCapture", Flags)!.Invoke(photos, [localWedding]);
        Check(!localWedding.skippable, "Silent auto-memento hides skip button for captured world frame");
        Type("GalleryPhotos").GetMethod("Capture", Flags)!.Invoke(photos, [null, localWedding]);
        Check(localWedding.skippable && !(bool)pendingProp.GetValue(photos)!,
            "Silent auto-memento restores skip button and clears pending request");
        Check(hudNotices.Count == 0 && soundsPlayed == 0 && monitorProxy.Messages.Count == 1,
            "Silent auto-memento suppresses HUD banners and cameraNoise while logging once on headless skip");

        // Upgrading a pending silent request via manual shutter Request restores normal feedback
        Type("GalleryPhotos").GetMethod("RequestSilent", Flags)!.Invoke(photos, [weddingId, localWedding, weddingDate]);
        Type("GalleryPhotos").GetMethod("Request", Flags)!.Invoke(photos, [weddingId, localWedding]);
        Type("GalleryPhotos").GetMethod("Capture", Flags)!.Invoke(photos, [null, localWedding]);
        Check(hudNotices.Contains("photo.failed"), "Manual shutter upgrades pending silent request so player receives feedback");
    }

    private static void VerifyCatalogCacheLightweightAutoMementoLookup()
    {
        LocalizedContentManager? oldContent = Game1.content;
        var countingContent = new CountingEventContentManager();
        Game1.content = countingContent;
        try
        {
            IMonitor monitor = DispatchProxy.Create<IMonitor, FakeProxy>();
            FakeProxy monitorProxy = (FakeProxy)(object)monitor;
            int originScans = 0;

            Type identityType = Type("EventIdentity");
            Type originMatchType = Type("EventOriginMatch");
            object unknownOrigin = originMatchType.GetProperty("Unknown", Flags)!.GetValue(null)!;
            Delegate originDelegate = typeof(OriginCounter)
                .GetMethod(nameof(OriginCounter.Invoke), Flags)!
                .MakeGenericMethod(identityType, originMatchType)
                .CreateDelegate(typeof(Func<,>).MakeGenericType(identityType, originMatchType),
                    new OriginCounter(() => { originScans++; return unknownOrigin; }));

            object cache = Activator.CreateInstance(Type("GalleryCatalogCache"), Flags, null,
                [monitor, (Func<bool>)(() => false), originDelegate], null)!;
            MethodInfo resolveMethod = Type("GalleryCatalogCache").GetMethod("ResolveCapturableStoryIdentity", Flags)!;

            // 1. Cold start (latest == null): must load ONLY Data/Events/SeedShop, 0 origin scans, 0 scan logs!
            countingContent.RequestedAssets.Clear();
            monitorProxy.Messages.Clear();
            object? resolvedStory = resolveMethod.Invoke(cache, ["Data\\Events\\SeedShop", "SeedShop", "1"]);
            object expectedSeedShop = Activator.CreateInstance(identityType, Flags, null, ["Data/Events/SeedShop", "1"], null)!;
            Check(Equals(resolvedStory, expectedSeedShop)
                && countingContent.RequestedAssets.Count == 1
                && countingContent.RequestedAssets[0] == "Data/Events/SeedShop"
                && originScans == 0
                && monitorProxy.Messages.Count == 0,
                "Cold-start ResolveCapturableStoryIdentity loads only single location asset with 0 pack scans and 0 scan logs");

            object? resolvedInternal = resolveMethod.Invoke(cache, ["Data/Events/SeedShop", "SeedShop", "999"]);
            Check(resolvedInternal is null && originScans == 0 && monitorProxy.Messages.Count == 0,
                "Cold-start ResolveCapturableStoryIdentity rejects Internal event without full catalog build");

            // 2. Warm cache (latest != null): even when live player friendship/spouse differs from cached CatalogInputs,
            // ResolveCapturableStoryIdentity reuses latest with 0 asset loads, 0 rebuilds, and 0 scan logs!
            FieldInfo latestField = Type("GalleryCatalogCache").GetField("latest", Flags)!;
            Type inputsType = Type("GalleryCatalogCache").GetNestedType("CatalogInputs", Flags)!;
            object emptyEvents = Array.CreateInstance(Type("ResolvedEvent"), 0);
            object emptyChars = Array.CreateInstance(Type("GalleryCharacter"), 0);
            object cachedInputs = Activator.CreateInstance(inputsType, Flags, null, [emptyEvents, emptyChars, "OldSpouse"], null)!;

            object emptyFrags = Activator.CreateInstance(Type("EventFragments"), Flags, null,
                [Array.Empty<string>(), Array.Empty<string>(), null, null, null, null], null)!;
            object resolved = Activator.CreateInstance(Type("ResolvedEvent"), Flags, null,
                [expectedSeedShop, "SeedShop", "1/f Abigail 500", "none/64 15/farmer 64 15 2/speak Abigail \"Hi\"/end", emptyFrags, "H1", "H2"], null)!;
            object ownership = Activator.CreateInstance(Type("EventOwnership"), Flags, null,
                [Enum.Parse(Type("OwnershipKind"), "Direct"), Array.CreateInstance(Type("EventOwner"), 0), null], null)!;
            object galleryEv = Activator.CreateInstance(Type("GalleryEvent"), Flags, null, [resolved, ownership], null)!;
            Set(galleryEv, "Kind", Enum.Parse(Type("StoryKind"), "Heart"));
            Array heartArr = Array.CreateInstance(Type("GalleryEvent"), 1);
            heartArr.SetValue(galleryEv, 0);
            object warmCatalog = Activator.CreateInstance(Type("GalleryCatalog"), Flags, null,
                [emptyChars, heartArr, Array.CreateInstance(Type("GalleryEvent"), 0)], null)!;

            object tuple = Activator.CreateInstance(latestField.FieldType.GetGenericArguments()[0], [cachedInputs, warmCatalog])!;
            latestField.SetValue(cache, tuple);
            Type("GalleryCatalogCache").GetField("candidatesForUpgradedTrailer", Flags)!.SetValue(cache, false);

            countingContent.RequestedAssets.Clear();
            monitorProxy.Messages.Clear();
            object? warmResolved = resolveMethod.Invoke(cache, ["Data/Events/SeedShop", "SeedShop", "1"]);
            Check(Equals(warmResolved, expectedSeedShop)
                && countingContent.RequestedAssets.Count == 0
                && originScans == 0
                && monitorProxy.Messages.Count == 0,
                "Warm ResolveCapturableStoryIdentity reuses cached catalog across friendship/spouse changes with 0 asset loads and 0 scan logs");
        }
        finally
        {
            Game1.content = oldContent!;
        }
    }

    private static void VerifyPhotoFolderSteamworksIsolation()
    {
        MethodInfo handheld = Type("PhotoFolderOpener").GetMethod("IsHandheldOrOverlayMode", Flags)!;
        bool result = (bool)handheld.Invoke(null, null)!;
        Check(!result || result, "PhotoFolderOpener.IsHandheldOrOverlayMode executes safely without initialized Steamworks");
    }

    private static void VerifyHdPortraitsRuntimeIntegration()
    {
        Type appearanceType = mod.GetType("StardewGallery.Appearance.CharacterAppearance", true)!;
        Type visualEnum = mod.GetType("StardewGallery.Appearance.CharacterVisual", true)!;
        object portraitKind = Enum.Parse(visualEnum, "Portrait");

        Texture2D placeholder = MakeStubTexture(64, 64, "Placeholder");
        Texture2D vanillaSheet = MakeStubTexture(128, 256, "Portraits/Abigail");
        Texture2D hdSheet = MakeStubTexture(512, 512, "Mods/HDPortraits/Sheets/Abigail");
        Texture2D haleyHdSheet = MakeStubTexture(1024, 1024, "Portraits/Haley");

        var content = DispatchProxy.Create<IGameContentHelper, ContentStubProxy>();
        var contentStub = (ContentStubProxy)(object)content;
        var registry = DispatchProxy.Create<IModRegistry, RegistryStubProxy>();
        var registryStub = (RegistryStubProxy)(object)registry;
        registryStub.LoadedMods.Add("tlitookilakin.HDPortraits");
        registryStub.LoadedMods.Add("Mangupix.DialogueDisplayFrameworkContinued");

        IMonitor monitor = DispatchProxy.Create<IMonitor, FakeProxy>();
        FakeProxy monitorProxy = (FakeProxy)(object)monitor;

        // DDFC has "default" 64x64 AND explicit "Haley" 512x512; HDPortraits has "Abigail" 256x256 animated 2x2 AND "Haley" 256x256.
        contentStub.Assets["aedenthorn.DialogueDisplayFramework/dictionary"] = new Dictionary<string, object>
        {
            ["default"] = new FakeDdfcEntry { Portrait = new FakeDdfcPortrait { W = 64, H = 64 } },
            ["Haley"] = new FakeDdfcEntry { Portrait = new FakeDdfcPortrait { W = 512, H = 512 } }
        };
        contentStub.Assets["Portraits/Abigail"] = vanillaSheet;
        contentStub.Assets["Portraits/Haley"] = haleyHdSheet;
        contentStub.Assets["Mods/HDPortraits/Abigail"] = new FakeHdModel
        {
            Size = 256,
            Portrait = "Mods/HDPortraits/Sheets/Abigail",
            Animation = new FakeHdAnimation { HFrames = 2, VFrames = 2, Delays = [100, 100, 100, 100] }
        };
        contentStub.Assets["Mods/HDPortraits/Sheets/Abigail"] = hdSheet;
        contentStub.Assets["Mods/HDPortraits/Haley"] = new FakeHdModel { Size = 256 };

        object appearance = Activator.CreateInstance(appearanceType, Flags, null, [content, registry, monitor, placeholder], null)!;
        Set(appearance, "npcs", new Dictionary<string, NPC>(StringComparer.Ordinal));
        Set(appearance, "npcsReadAt", Environment.TickCount64 + 60_000L);

        // Asset invalidation check
        MethodInfo usesAsset = appearanceType.GetMethod("UsesAsset", Flags)!;
        Check((bool)usesAsset.Invoke(appearance, [MakeAssetName("Mods/HDPortraits")])!
            && (bool)usesAsset.Invoke(appearance, [MakeAssetName("Mods/HDPortraits/Abigail")])!
            && !(bool)usesAsset.Invoke(appearance, [MakeAssetName("Maps/Town")])!,
            "CharacterAppearance.UsesAsset tracks Mods/HDPortraits and Mods/HDPortraits/*");

        Game1.currentGameTime = new GameTime(TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(16));
        contentStub.LoadCounts.Clear();
        appearanceType.GetMethod("Prepare", Flags)!.Invoke(appearance, ["Abigail", portraitKind]);
        object frame0 = ReadCachedPortraitFrame(appearance, "Abigail", portraitKind)!;
        Check(Equals(Get(frame0, "Width"), 256) && Equals(Get(frame0, "X"), 0) && Equals(Get(frame0, "Y"), 0),
            "HD Portraits 256px entry overrides DDFC generic 'default' 64px entry");

        // Advance time to frame 1 (150ms) and frame 2 (250ms): must animate without reloading HD sheet or metadata!
        Game1.currentGameTime = new GameTime(TimeSpan.FromMilliseconds(150), TimeSpan.FromMilliseconds(16));
        appearanceType.GetMethod("Prepare", Flags)!.Invoke(appearance, ["Abigail", portraitKind]);
        object frame1 = ReadCachedPortraitFrame(appearance, "Abigail", portraitKind)!;
        Game1.currentGameTime = new GameTime(TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(16));
        appearanceType.GetMethod("Prepare", Flags)!.Invoke(appearance, ["Abigail", portraitKind]);
        object frame2 = ReadCachedPortraitFrame(appearance, "Abigail", portraitKind)!;
        Check(Equals(Get(frame1, "X"), 256) && Equals(Get(frame1, "Y"), 0)
            && Equals(Get(frame2, "X"), 0) && Equals(Get(frame2, "Y"), 256)
            && contentStub.LoadCounts.GetValueOrDefault("Mods/HDPortraits/Sheets/Abigail") == 1
            && contentStub.LoadCounts.GetValueOrDefault("Mods/HDPortraits/Abigail") == 1,
            "Animated HD Portrait advances frames without redundant HD texture or metadata loads");

        // Explicit DDFC character entry ("Haley" 512px) takes precedence over HD Portraits ("Haley" 256px) without double cropping
        appearanceType.GetMethod("Prepare", Flags)!.Invoke(appearance, ["Haley", portraitKind]);
        object haleyFrame = ReadCachedPortraitFrame(appearance, "Haley", portraitKind)!;
        Check(Equals(Get(haleyFrame, "Width"), 512) && Equals(Get(haleyFrame, "Height"), 512),
            "Explicit character DDFC entry wins without double-cropping HD Portraits");

        // Out-of-bounds HD Portrait metadata on a 128x256 vanilla sheet falls back to 64px game sheet
        appearanceType.GetMethod("Invalidate", Flags)!.Invoke(appearance, null);
        Set(appearance, "npcs", new Dictionary<string, NPC>(StringComparer.Ordinal));
        Set(appearance, "npcsReadAt", Environment.TickCount64 + 60_000L);
        contentStub.Assets["Mods/HDPortraits/Abigail"] = new FakeHdModel { Size = 1024 };
        appearanceType.GetMethod("Prepare", Flags)!.Invoke(appearance, ["Abigail", portraitKind]);
        object fallbackFrame = ReadCachedPortraitFrame(appearance, "Abigail", portraitKind)!;
        Check(Equals(Get(fallbackFrame, "Width"), 64) && Equals(Get(fallbackFrame, "Height"), 64),
            "Oversized HD Portraits metadata falls back cleanly to 64px game sheet");

        // Malformed negative Size logs once with "(HD Portraits)" attribution and schedules 2000ms retry
        appearanceType.GetMethod("Invalidate", Flags)!.Invoke(appearance, null);
        Set(appearance, "npcs", new Dictionary<string, NPC>(StringComparer.Ordinal));
        Set(appearance, "npcsReadAt", Environment.TickCount64 + 60_000L);
        monitorProxy.Messages.Clear();
        contentStub.Assets["Mods/HDPortraits/Abigail"] = new FakeHdModel { Size = -10 };
        Game1.currentGameTime = new GameTime(TimeSpan.FromMilliseconds(1000), TimeSpan.FromMilliseconds(16));
        appearanceType.GetMethod("Prepare", Flags)!.Invoke(appearance, ["Abigail", portraitKind]);
        appearanceType.GetMethod("Prepare", Flags)!.Invoke(appearance, ["Abigail", portraitKind]);
        Check(monitorProxy.Messages.Count == 1
            && monitorProxy.Messages[0].Contains("(HD Portraits)", StringComparison.Ordinal)
            && !monitorProxy.Messages[0].Contains("(DDFC)", StringComparison.Ordinal),
            "Invalid HD Portraits metadata attributes rate-limited warning to (HD Portraits) instead of (DDFC)");

        // When HD Portraits is unconfigured (default 64px) and DDFC default fails to load texture, log attributes to (DDFC)
        appearanceType.GetMethod("Invalidate", Flags)!.Invoke(appearance, null);
        Set(appearance, "npcs", new Dictionary<string, NPC>(StringComparer.Ordinal));
        Set(appearance, "npcsReadAt", Environment.TickCount64 + 60_000L);
        monitorProxy.Messages.Clear();
        contentStub.Assets["Mods/HDPortraits/Abigail"] = new FakeHdModel { Size = 64 };
        contentStub.Assets["aedenthorn.DialogueDisplayFramework/dictionary"] = new Dictionary<string, object>
        {
            ["default"] = new FakeDdfcEntry { Portrait = new FakeDdfcPortrait { TexturePath = "Portraits/MissingDdfcSheet", W = 64, H = 64 } }
        };
        Game1.currentGameTime = new GameTime(TimeSpan.FromMilliseconds(4000), TimeSpan.FromMilliseconds(16));
        appearanceType.GetMethod("Prepare", Flags)!.Invoke(appearance, ["Abigail", portraitKind]);
        Check(monitorProxy.Messages.Count == 1
            && monitorProxy.Messages[0].Contains("(DDFC)", StringComparison.Ordinal)
            && !monitorProxy.Messages[0].Contains("(HD Portraits)", StringComparison.Ordinal),
            "DDFC fallback failure attributes warning to (DDFC) after unconfigured HD Portraits pass-through");
    }

    private static void VerifyDetailFavoriteAndPhotoMenuControls()
    {
        object inMemFs = Activator.CreateInstance(Type("InMemoryStorageFileSystem"), Flags, null, null, null)!;
        object favStore = Activator.CreateInstance(Type("FavoriteEventStore"), Flags, null,
            [Path.Combine(Path.GetTempPath(), "InMemFavs"), (Action<string>)(_ => { }), inMemFs], null)!;

        object identity = Activator.CreateInstance(Type("EventIdentity"), Flags, null, ["Data/Events/SeedShop", "1"], null)!;
        object emptyFrags = Activator.CreateInstance(Type("EventFragments"), Flags, null,
            [Array.Empty<string>(), Array.Empty<string>(), null, null, null, null], null)!;
        object resolved = Activator.CreateInstance(Type("ResolvedEvent"), Flags, null,
            [identity, "SeedShop", "1/f Abigail 500", "none/64 15/farmer 64 15 2/end", emptyFrags, "H1", "H2"], null)!;
        object ownerArr = Array.CreateInstance(Type("EventOwner"), 0);
        object directKind = Enum.Parse(Type("OwnershipKind"), "Direct");
        object ownership = Activator.CreateInstance(Type("EventOwnership"), Flags, null, [directKind, ownerArr, null], null)!;
        object heartKind = Enum.Parse(Type("StoryKind"), "Heart");
        object galleryEvent = Activator.CreateInstance(Type("GalleryEvent"), Flags, null, [resolved, ownership], null)!;
        Set(galleryEvent, "Kind", heartKind);

        object view = FormatterServices.GetUninitializedObject(Type("GalleryViewContext"));
        ITranslationHelper i18n = DispatchProxy.Create<ITranslationHelper, DraftTranslation>();
        Set(view, "I18n", i18n);
        Set(view, "Favorites", favStore);
        object characters = FormatterServices.GetUninitializedObject(Type("GalleryCharacterNames"));
        Set(characters, "names", new Dictionary<string, string> { ["Abigail"] = "Abigail" });
        Set(characters, "aliases", new Dictionary<string, string>());
        Set(characters, "i18n", i18n);
        Set(view, "Characters", characters);

        // Detail menu Favorite toggle & footer focus navigation
        object detailState = Activator.CreateInstance(Type("GalleryPageState"), Flags, null, [Enum.Parse(Type("GalleryPage"), "Detail")], null)!;
        var detailMenu = (IClickableMenu)FormatterServices.GetUninitializedObject(Type("GalleryEventDetailMenu"));
        Set(detailMenu, "context", view);
        Set(detailMenu, "state", detailState);
        Set(detailMenu, "entry", galleryEvent);
        Set(detailMenu, "i18n", i18n);
        Set(detailMenu, "menuScale", 1f);
        Set(detailMenu, "backBounds", new Rectangle(735, 810, 368, 72));
        Set(detailMenu, "referenceLinks", Activator.CreateInstance(Type("GalleryEventDetailMenu").GetField("referenceLinks", Flags)!.FieldType));

        Type("GalleryEventDetailMenu").GetMethod("BuildClickableComponents", Flags)!.Invoke(detailMenu, null);
        Check(detailMenu.allClickableComponents.Any(c => c.myID == 1006), "GalleryEventDetailMenu registers Favorite button component 1006");
        Type("GalleryEventDetailMenu").GetMethod("FocusComponent", Flags)!.Invoke(detailMenu, [1006]);
        detailMenu.receiveKeyPress(Keys.Enter);
        Check((bool)Type("FavoriteEventStore").GetMethod("IsFavorite", Flags)!.Invoke(favStore, [identity])!,
            "Pressing Enter on focused Favorite button marks event as favorite");
        ClickableComponent favComp = detailMenu.allClickableComponents.Single(c => c.myID == 1006);
        detailMenu.receiveLeftClick(favComp.bounds.Center.X, favComp.bounds.Center.Y);
        Check(!(bool)Type("FavoriteEventStore").GetMethod("IsFavorite", Flags)!.Invoke(favStore, [identity])!,
            "Clicking Favorite button again unfavorites the event");
        detailMenu.applyMovementKey(1); // Right from 1006 -> 1003 (Rename)
        Check(detailMenu.currentlySnappedComponent?.myID == 1003, "Right arrow navigates from Favorite (1006) to Rename (1003)");

        // Photo menu Wedding Memento header + Open Local Folder button (104)
        object profile = Activator.CreateInstance(Type("SaveProfileKey"), Flags, null, [9UL, 9L], null)!;
        object photoStore = Activator.CreateInstance(Type("EventPhotoStore"), Flags, null,
            [Path.Combine(Path.GetTempPath(), "InMemWeddingPhotos"), profile, (Action<string>)(_ => { }), inMemFs], null)!;
        object weddingId = Type("WeddingMemento").GetMethod("IdentityFor", Flags)!.Invoke(null, ["Abigail"])!;
        object weddingDate = Activator.CreateInstance(Type("WeddingDateRecord"), Flags, null, [3, "summer", 12], null)!;
        byte[] png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a9d8AAAAASUVORK5CYII=");
        Type("EventPhotoStore").GetMethods(Flags).Single(m => m.Name == "Add" && m.GetParameters().Length == 4)
            .Invoke(photoStore, [weddingId, png, png, weddingDate]);

        IModHelper helper = DispatchProxy.Create<IModHelper, FakeProxy>();
        IMonitor monitor = DispatchProxy.Create<IMonitor, FakeProxy>();
        object photosMgr = Activator.CreateInstance(Type("GalleryPhotos"), Flags, null,
            [helper, monitor, (Action<string, bool>)((_, _) => { })], null)!;
        Set(photosMgr, "store", photoStore);
        Set(view, "Photos", photosMgr);

        object photoState = Activator.CreateInstance(Type("GalleryPageState"), Flags, null, [Enum.Parse(Type("GalleryPage"), "Photos")], null)!;
        Set(photoState, "Event", weddingId);
        var photoMenu = (IClickableMenu)FormatterServices.GetUninitializedObject(Type("GalleryPhotoMenu"));
        Set(photoMenu, "context", view);
        Set(photoMenu, "state", photoState);
        Set(photoMenu, "photos", photosMgr);
        Set(photoMenu, "identity", weddingId);
        Set(photoMenu, "i18n", i18n);
        Set(photoMenu, "scale", 1f);
        Set(photoMenu, "set", Type("EventPhotoStore").GetMethod("Read", Flags)!.Invoke(photoStore, [weddingId]));

        Type("GalleryPhotoMenu").GetMethod("BuildComponents", Flags)!.Invoke(photoMenu, null);
        Check(photoMenu.allClickableComponents.Any(c => c.myID == 104), "GalleryPhotoMenu registers Open Local Folder component 104");
        string headerTitle = (string)Type("GalleryPhotoMenu").GetMethod("HeaderTitle", Flags)!.Invoke(photoMenu, null)!;
        string headerSub = (string)Type("GalleryPhotoMenu").GetMethod("HeaderSubtitle", Flags)!.Invoke(photoMenu, null)!;
        string relSubdir = (string)Type("GalleryPhotos").GetMethod("DisplaySubdirectory", Flags)!.Invoke(photosMgr, [weddingId])!;
        Check(headerTitle == "wedding.title" && headerSub == "wedding.subtitle" && relSubdir.StartsWith("event-photos/", StringComparison.Ordinal),
            "GalleryPhotoMenu formats wedding title/subtitle and exposes relative event-photos subdirectory");

        Type("GalleryPhotoMenu").GetMethod("Focus", Flags)!.Invoke(photoMenu, [103]);
        photoMenu.applyMovementKey(1); // Right from 103 (Remove) -> 104 (Open Folder)
        Check(photoMenu.currentlySnappedComponent?.myID == 104, "Controller/keyboard Right navigates from Remove (103) to Open Folder (104)");
    }

    private static object? ReadCachedPortraitFrame(object appearance, string name, object kind)
    {
        var dict = (IDictionary)Get(appearance, "visuals")!;
        foreach (DictionaryEntry entry in dict)
        {
            if (Equals(entry.Key?.GetType().GetField("Item1")?.GetValue(entry.Key), name)
                && Equals(entry.Key?.GetType().GetField("Item2")?.GetValue(entry.Key), kind))
            {
                return Get(entry.Value!, "Portrait");
            }
        }
        return null;
    }

    private static Texture2D MakeStubTexture(int width, int height, string assetName)
    {
        var tex = (Texture2D)FormatterServices.GetUninitializedObject(typeof(Texture2D));
        Set(tex, "width", width);
        Set(tex, "height", height);
        tex.Name = assetName;
        return tex;
    }

    private static IAssetName MakeAssetName(string raw)
    {
        Type assetNameType = typeof(IAssetName).Assembly.GetType("StardewModdingAPI.Framework.Content.AssetName", true)!;
        MethodInfo parse = assetNameType.GetMethod("Parse", Flags)!;
        return (IAssetName)parse.Invoke(null, [raw, (Func<string, LocalizedContentManager.LanguageCode?>)(_ => null)])!;
    }

    private static Type Type(string name) => mod.GetType("StardewGallery." + name, true)!;

    private static object? Get(object target, string name)
    {
        for (Type? t = target.GetType(); t is not null; t = t.BaseType)
        {
            if (t.GetProperty(name, Flags) is { } p) return p.GetValue(target);
            if (t.GetField(name, Flags) is { } f) return f.GetValue(target);
        }
        throw new MissingMemberException(target.GetType().Name, name);
    }

    private static void Set(object target, string name, object? value)
    {
        for (Type? t = target.GetType(); t is not null; t = t.BaseType)
        {
            if (t.GetProperty(name, Flags) is { CanWrite: true } p) { p.SetValue(target, value); return; }
            if ((t.GetField(name, Flags) ?? t.GetField("<" + name + ">k__BackingField", Flags)) is { } f) { f.SetValue(target, value); return; }
        }
        throw new MissingMemberException(target.GetType().Name, name);
    }

    private static void Check(bool ok, string message)
    {
        assertions++;
        if (!ok)
            throw new InvalidOperationException("Stage 3 runtime check failed: " + message);
        Console.WriteLine("PASS " + message);
    }

    public sealed class FakeHdModel
    {
        public int Size { get; set; } = 64;
        public string? Portrait { get; set; }
        public FakeHdAnimation? Animation { get; set; }
    }

    public sealed class FakeHdAnimation
    {
        public int HFrames { get; set; } = 1;
        public int VFrames { get; set; } = 1;
        public int[] Delays { get; set; } = [];
    }

    public sealed class FakeDdfcEntry
    {
        public string? CopyFrom { get; set; }
        public bool Disabled { get; set; }
        public FakeDdfcPortrait? Portrait { get; set; }
        public string? Condition { get; set; }
    }

    public sealed class FakeDdfcPortrait
    {
        public string? TexturePath { get; set; }
        public int X { get; set; } = -1;
        public int Y { get; set; } = -1;
        public int W { get; set; } = 64;
        public int H { get; set; } = 64;
        public float Alpha { get; set; } = 1f;
        public bool Disabled { get; set; }
    }

    private sealed class OriginCounter(Func<object> callback)
    {
        public TResult Invoke<TIdentity, TResult>(TIdentity _) => (TResult)callback();
    }

    private sealed class CountingEventContentManager : LocalizedContentManager
    {
        public List<string> RequestedAssets { get; } = [];

        public CountingEventContentManager() : base(new GameServiceContainer(), "") { }

        public override bool DoesAssetExist<T>(string assetName)
        {
            string norm = assetName.Replace('\\', '/');
            return norm.Equals("Data/Events/SeedShop", StringComparison.OrdinalIgnoreCase);
        }

        public override T Load<T>(string assetName)
        {
            string norm = assetName.Replace('\\', '/');
            RequestedAssets.Add(norm);
            if (typeof(T) == typeof(Dictionary<string, string>)
                && norm.Equals("Data/Events/SeedShop", StringComparison.OrdinalIgnoreCase))
            {
                return (T)(object)new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["1/f Abigail 500"] = "none/64 15/farmer 64 15 2 Abigail 65 15 3/speak Abigail \"Hi!\"/end",
                    ["999/t 600 700"] = "none/-100 -100/farmer 5 5 2/mail flagOnly/end"
                };
            }
            throw new ContentLoadException("Unexpected asset load: " + norm);
        }
    }
}

public class ContentStubProxy : DispatchProxy
{
    public Dictionary<string, object> Assets { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> LoadCounts { get; } = new(StringComparer.OrdinalIgnoreCase);

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod?.Name == "Load" && args is [string key, ..])
        {
            string normalized = key.Replace('\\', '/');
            if (Assets.TryGetValue(normalized, out object? found))
            {
                LoadCounts[normalized] = LoadCounts.GetValueOrDefault(normalized) + 1;
                return found;
            }
            throw new ContentLoadException("Missing test asset: " + normalized);
        }
        return targetMethod!.ReturnType != typeof(void) && targetMethod.ReturnType.IsValueType
            ? Activator.CreateInstance(targetMethod.ReturnType)
            : null;
    }
}

public class RegistryStubProxy : DispatchProxy
{
    public HashSet<string> LoadedMods { get; } = new(StringComparer.OrdinalIgnoreCase);

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod?.Name == "IsLoaded" && args is [string id])
            return LoadedMods.Contains(id);
        return targetMethod!.ReturnType != typeof(void) && targetMethod.ReturnType.IsValueType
            ? Activator.CreateInstance(targetMethod.ReturnType)
            : null;
    }
}
