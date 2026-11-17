using System.Collections;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;

/// <summary>
/// Non-destructive Stage 4 runtime checks against 1.6.15 <c>Stardew Valley.dll</c> and built <c>StardewGallery.dll</c>:
/// standing idle character sprite, real <see cref="GameMenu"/> / <see cref="SocialPage"/> hotkey context shortcut and
/// seamless scroll/focus restoration (zero UI injection), three-ribbon book switching, whole-page book turning with
/// <c>shwip</c> sound and focus/page persistence, pinned spouse Wedding Memento card in <c>GalleryCharacterMenu</c>,
/// inline <c>EncounterOnly</c> filter in <c>GalleryQueryMenu</c>, and <c>GalleryMomentsMenu</c> Polaroid showcase.
/// Uses <c>InMemoryStorageFileSystem</c> exclusively; never creates or deletes temporary disk directories.
/// </summary>
internal static class Stage4RuntimeChecks
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static Assembly mod = null!;
    private static int assertions;
    private static Point cursor;
    private static IClickableMenu? activeMenuField;
    private static object? currentCatalogFixture;
    private static readonly List<string> playedSounds = [];

    internal static void Run(Assembly modAssembly, string gameDirectory)
    {
        mod = modAssembly;
        assertions = 0;
        playedSounds.Clear();
        var harmony = new Harmony("Gallery.Stage4RuntimeChecks");
        try
        {
            Game1.game1 = (Game1)FormatterServices.GetUninitializedObject(typeof(Game1));
            Game1.options = (Options)FormatterServices.GetUninitializedObject(typeof(Options));
            Game1.options.gamepadControls = true;
            Game1.options.snappyMenus = true;
            foreach (FieldInfo field in typeof(Options).GetFields(Flags).Where(f => f.FieldType == typeof(InputButton[])))
                field.SetValue(Game1.options, Array.Empty<InputButton>());
            Game1.options.actionButton = [new InputButton(Keys.X)];
            Game1.options.useToolButton = [new InputButton(Keys.C)];
            Game1.uiViewport = new xTile.Dimensions.Rectangle(0, 0, 1920, 1080);

            var smallFont = (SpriteFont)FormatterServices.GetUninitializedObject(typeof(SpriteFont));
            smallFont.LineSpacing = 32;
            Game1.smallFont = smallFont;
            Game1.dialogueFont = smallFont;

            PropertyInfo dispatcherProp = typeof(Game1).GetProperty("keyboardDispatcher", Flags)!;
            dispatcherProp.SetValue(null, FormatterServices.GetUninitializedObject(dispatcherProp.PropertyType));
            GameRunner.instance = (GameRunner)FormatterServices.GetUninitializedObject(typeof(GameRunner));
            GameRunner.instance.gameInstances = [Game1.game1];
            Game1.content = new LocalizedContentManager(new GameServiceContainer(), Path.Combine(gameDirectory, "Content"));
            typeof(Game1).GetField("multiplayer", Flags)?.SetValue(null, new Multiplayer());
            typeof(Game1).GetField("_locations", Flags)?.SetValue(Game1.game1, new List<GameLocation>());

            foreach (MethodInfo m in new[]
            {
                typeof(PlayerStatusList).GetMethod("AddSpriteDefinition", Flags)!,
                typeof(Farmer).GetMethod("farmerInit", Flags)!,
                typeof(AnimatedSprite).GetMethod("LoadTexture", Flags)!
            })
                harmony.Patch(m, prefix: new HarmonyMethod(typeof(Stage4RuntimeChecks), nameof(SkipVoid)));

            harmony.Patch(typeof(SpriteFont).GetMethod("MeasureString", [typeof(string)])!,
                prefix: new HarmonyMethod(typeof(Stage4RuntimeChecks), nameof(MeasureFakeString)));
            foreach (MethodInfo parse in typeof(Game1).GetMethods(Flags).Where(m => m.Name == "parseText"))
                harmony.Patch(parse, prefix: new HarmonyMethod(typeof(Stage4RuntimeChecks), nameof(IdentityParseText)));
            foreach (MethodInfo sound in typeof(Game1).GetMethods(Flags).Where(m => m.Name == "playSound"))
                harmony.Patch(sound, prefix: new HarmonyMethod(typeof(Stage4RuntimeChecks), nameof(RecordSound)));

            harmony.Patch(typeof(Game1).GetProperty("activeClickableMenu", Flags)!.GetMethod!,
                prefix: new HarmonyMethod(typeof(Stage4RuntimeChecks), nameof(GetActiveMenu)));
            harmony.Patch(typeof(Game1).GetProperty("activeClickableMenu", Flags)!.SetMethod!,
                prefix: new HarmonyMethod(typeof(Stage4RuntimeChecks), nameof(SetActiveMenu)));
            harmony.Patch(typeof(Context).GetProperty("IsWorldReady", Flags)!.GetMethod!,
                prefix: new HarmonyMethod(typeof(Stage4RuntimeChecks), nameof(ReturnTrue)));
            harmony.Patch(typeof(Game1).GetMethod("getMouseX", [typeof(bool)])!,
                prefix: new HarmonyMethod(typeof(Stage4RuntimeChecks), nameof(GetMouseX)));
            harmony.Patch(typeof(Game1).GetMethod("getMouseY", [typeof(bool)])!,
                prefix: new HarmonyMethod(typeof(Stage4RuntimeChecks), nameof(GetMouseY)));
            harmony.Patch(typeof(IClickableMenu).GetMethod("snapCursorToCurrentSnappedComponent", Flags)!,
                prefix: new HarmonyMethod(typeof(Stage4RuntimeChecks), nameof(SnapCursor)));
            harmony.Patch(typeof(SocialPage).GetMethod("snapCursorToCurrentSnappedComponent", Flags)!,
                prefix: new HarmonyMethod(typeof(Stage4RuntimeChecks), nameof(SnapCursor)));
            harmony.Patch(Type("GalleryTextBox").GetConstructors(Flags).Single(c => !c.IsStatic),
                prefix: new HarmonyMethod(typeof(Stage4RuntimeChecks), nameof(SkipVoid)));
            harmony.Patch(Type("GalleryCharacterNames").GetConstructors(Flags).Single(c => !c.IsStatic),
                prefix: new HarmonyMethod(typeof(Stage4RuntimeChecks), nameof(InitCharacterNames)));
            harmony.Patch(Type("GalleryCatalogCache").GetMethod("Get", Flags)!,
                prefix: new HarmonyMethod(typeof(Stage4RuntimeChecks), nameof(GetCatalogFixture)));
            harmony.Patch(Type("GalleryConditionPresentation").GetMethod("Build", Flags)!,
                prefix: new HarmonyMethod(typeof(Stage4RuntimeChecks), nameof(EmptyConditions)));

            Farmer player = new() { UniqueMultiplayerID = 9001L, Name = "Tester", stats = new Stats() };
            player.eventsSeen.Add("90101");
            typeof(Game1).GetProperty("player", Flags)!.SetValue(null, player);

            VerifyStandingIdleSprite();
            VerifyHomeKeyboardNavigationWithoutGamepad();
            VerifySocialPageShortcutAndSeamlessReturn();
            foreach (bool snappy in new[] { true, false })
            {
                Game1.options.snappyMenus = snappy;
                VerifyBookSpreadsPaginationAndMoments(snappy);
            }
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
            activeMenuField = null;
        }

        Console.WriteLine($"Stage 4 runtime checks passed ({assertions} assertions).");
    }

    private static void VerifyStandingIdleSprite()
    {
        AnimatedSprite sprite = new("Characters\\Abigail", 7, 16, 32);
        MethodInfo standingIdle = Type("Appearance.CharacterAppearance").GetMethod("SetStandingIdle", Flags)!;
        foreach (double ms in new[] { 0d, 1800d, 3600d, 5400d, 7200d })
        {
            Game1.currentGameTime = new GameTime(TimeSpan.FromMilliseconds(ms), TimeSpan.FromMilliseconds(16));
            standingIdle.Invoke(null, [sprite]);
            Check(sprite.currentFrame == 0 && sprite.CurrentAnimation is null,
                $"SetStandingIdle fallback keeps sprite at front-facing frame 0 at {ms}ms");
        }

        MethodInfo stroll = Type("Appearance.AppearanceGeometry").GetMethod("ScenicStroll", Flags)!;
        var walkRight = ((int Frame, float OffsetX))stroll.Invoke(null, ["", 2500d, 64, 256, 16, 32, 28f])!;
        var rightPause = ((int Frame, float OffsetX))stroll.Invoke(null, ["", 4500d, 64, 256, 16, 32, 28f])!;
        var walkLeft = ((int Frame, float OffsetX))stroll.Invoke(null, ["", 7000d, 64, 256, 16, 32, 28f])!;
        var unsafeSheet = ((int Frame, float OffsetX))stroll.Invoke(null, ["Abigail", 2500d, 32, 64, 16, 32, 28f])!;
        Check(walkRight.Frame is >= 4 and <= 7 && walkRight.OffsetX > 0f,
            "ScenicStroll plays row-1 walk-right frames (4..7) while moving right");
        Check(rightPause.Frame == 0 && Math.Abs(rightPause.OffsetX - 28f) < 0.01f,
            "ScenicStroll pauses facing player (frame 0) at right turnaround");
        Check(walkLeft.Frame is >= 12 and <= 15 && walkLeft.OffsetX < 28f,
            "ScenicStroll plays row-3 walk-left frames (12..15) while moving left");
        Check(unsafeSheet == (0, 0f),
            "ScenicStroll falls back safely to frame 0 with 0 offset on non-4x4 sheets");
    }

    private static void VerifyHomeKeyboardNavigationWithoutGamepad()
    {
        var (app, _, _, _, _) = BuildApplicationFixture(unlockAll: true);
        bool prevGamepad = Game1.options.gamepadControls;
        bool prevSnappy = Game1.options.snappyMenus;
        try
        {
            Game1.options.gamepadControls = false;
            Game1.options.snappyMenus = true;
            cursor = new Point(50, 50);

            Call(app, "Open");
            Check(activeMenuField?.GetType().Name == "GalleryMenu", "keyboard mode: Open() starts on GalleryMenu");
            IClickableMenu home = activeMenuField!;
            home.performHoverAction(50, 50); // seed lastMouse at stationary physical cursor
            Check(home.currentlySnappedComponent?.myID == 0, "keyboard mode: initial card focus is slot 0");

            home.receiveKeyPress(Keys.Right);
            Check(home.currentlySnappedComponent?.myID == 1
                && Equals(Get(home, "keyboardFocus"), true)
                && cursor == new Point(50, 50),
                "keyboard mode: Keys.Right moves focus 0 -> 1 without teleporting physical mouse cursor");

            // Next frame hover with stationary mouse must NOT clear keyboardFocus!
            home.performHoverAction(50, 50);
            Check(Equals(Get(home, "keyboardFocus"), true),
                "keyboard mode: stationary mouse performHoverAction preserves keyboardFocus");

            home.receiveKeyPress(Keys.Down);
            Check(home.currentlySnappedComponent?.myID == 4 && Equals(Get(home, "keyboardFocus"), true),
                "keyboard mode: Keys.Down moves focus 1 -> 4 inside left 3x3 page");

            home.receiveKeyPress(Keys.Down);
            home.receiveKeyPress(Keys.Down);
            Check(home.currentlySnappedComponent?.myID == 9101 && Equals(Get(home, "keyboardFocus"), true),
                "keyboard mode: Keys.Down from bottom row reaches bottom-left ribbon bookmark 9101");

            home.receiveKeyPress(Keys.Left);
            Check(home.currentlySnappedComponent?.myID == 9100 && Equals(Get(home, "keyboardFocus"), true),
                "keyboard mode: Keys.Left moves across bottom-left ribbons 9101 -> 9100");

            home.receiveKeyPress(Keys.Up);
            ClickableComponent slot6 = home.allClickableComponents.Single(c => c.myID == 6);
            Check(home.currentlySnappedComponent?.myID == 6 && slot6.downNeighborID == 9100,
                "keyboard mode: Keys.Up returns to slot 6 while fullyImmutable preserves slot 6's downNeighborID=9100");

            // Verify crossing the center spine between left 3x3 (slot 8) and right 3x3 (slot 15)
            home.receiveKeyPress(Keys.Right);
            home.receiveKeyPress(Keys.Right);
            home.receiveKeyPress(Keys.Right);
            Check(home.currentlySnappedComponent?.myID == 15,
                "keyboard mode: Keys.Right crosses center spine cleanly from left page slot 8 to right page slot 15");
            home.receiveKeyPress(Keys.Left);
            Check(home.currentlySnappedComponent?.myID == 8,
                "keyboard mode: Keys.Left crosses center spine back from right page slot 15 to left page slot 8");
        }
        finally
        {
            Game1.options.gamepadControls = prevGamepad;
            Game1.options.snappyMenus = prevSnappy;
        }
    }

    private static void VerifySocialPageShortcutAndSeamlessReturn()
    {
        var (app, _, _, _, monProxy) = BuildApplicationFixture(unlockAll: false);

        // Build a real 1.6.15 GameMenu + SocialPage scrolled to slotPosition = 3 (8 entries, 5 visible slots).
        var gameMenu = (GameMenu)FormatterServices.GetUninitializedObject(typeof(GameMenu));
        var socialPage = (SocialPage)FormatterServices.GetUninitializedObject(typeof(SocialPage));
        gameMenu.pages = [];
        for (int t = 0; t <= GameMenu.socialTab; t++)
            gameMenu.pages.Add(t == GameMenu.socialTab ? socialPage : new DummyMenu());
        gameMenu.currentTab = GameMenu.socialTab;

        typeof(SocialPage).GetField("SocialEntries", Flags)!.SetValue(socialPage, new List<SocialPage.SocialEntry>());
        typeof(SocialPage).GetField("characterSlots", Flags)!.SetValue(socialPage, new List<ClickableTextureComponent>());
        socialPage.allClickableComponents = [];

        (string Name, bool IsPlayer, bool IsMet)[] rows =
        [
            ("Tester", true, true),
            ("Abigail", false, true),
            ("Sam", false, true),
            ("Sebastian", false, true),
            ("Leo", false, false),
            ("Haley", false, true),
            ("Emily", false, true),
            ("Maru", false, true)
        ];

        for (int i = 0; i < rows.Length; i++)
        {
            var entry = (SocialPage.SocialEntry)FormatterServices.GetUninitializedObject(typeof(SocialPage.SocialEntry));
            typeof(SocialPage.SocialEntry).GetField("InternalName", Flags)!.SetValue(entry, rows[i].Name);
            typeof(SocialPage.SocialEntry).GetField("DisplayName", Flags)!.SetValue(entry, rows[i].Name);
            typeof(SocialPage.SocialEntry).GetField("IsPlayer", Flags)!.SetValue(entry, rows[i].IsPlayer);
            typeof(SocialPage.SocialEntry).GetField("IsChild", Flags)!.SetValue(entry, false);
            typeof(SocialPage.SocialEntry).GetField("IsMet", Flags)!.SetValue(entry, rows[i].IsMet);
            socialPage.SocialEntries.Add(entry);

            Rectangle rowRect = i is >= 3 and < 8
                ? new Rectangle(400, 200 + (i - 3) * 112, 880, 104)
                : new Rectangle(400, -1000, 880, 104);
            var slot = new ClickableTextureComponent(rowRect, null, Rectangle.Empty, 1f) { myID = i };
            socialPage.characterSlots.Add(slot);
            socialPage.allClickableComponents.Add(slot);
        }

        typeof(SocialPage).GetField("slotPosition", Flags)!.SetValue(socialPage, 3);
        int originalComponentCount = socialPage.allClickableComponents.Count;
        int originalSlotCount = socialPage.characterSlots.Count;

        // 1) Controller snapped mode on Sebastian (index 3, scrolled to slotPosition = 3)
        Game1.options.snappyMenus = true;
        Game1.options.gamepadControls = true;
        socialPage.currentlySnappedComponent = socialPage.characterSlots[3];
        cursor = new Point(10, 10); // mouse far away; controller focus must win
        activeMenuField = gameMenu;

        Type bridgeType = Type("SocialPageBridge");
        object?[] inspectArgs = [null, null, null];
        bool onSocial = (bool)bridgeType.GetMethod("TryInspectActiveMenu", Flags)!.Invoke(null, inspectArgs)!;
        Check(onSocial && Equals(inspectArgs[2], "Sebastian"),
            "SocialPageBridge detects controller-snapped Sebastian at scrolled slotPosition=3");
        Check(socialPage.allClickableComponents.Count == originalComponentCount && socialPage.characterSlots.Count == originalSlotCount,
            "SocialPageBridge injects zero UI components into vanilla SocialPage");

        // Also verify SocialPage clue badge hit-testing & morning encounter hint truncation!
        Type clueEnum = Type("EncounterClue");
        IDictionary cluesByNpc = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(typeof(string), clueEnum))!;
        cluesByNpc["Sebastian"] = Enum.Parse(clueEnum, "AvailableToday");
        cluesByNpc["Leo"] = Enum.Parse(clueEnum, "AvailableToday"); // Leo IsMet == false -> must NOT get a badge!
        cluesByNpc["Haley"] = Enum.Parse(clueEnum, "ReadyForTiming");
        IList visibleBadges = (IList)bridgeType.GetMethod("CollectVisibleBadges", Flags)!.Invoke(null, [socialPage, cluesByNpc])!;
        Check(visibleBadges.Count == 2,
            "SocialPageBridge emits badges only for visible met villagers (Sebastian & Haley, excluding unmet Leo)");
        Rectangle sebBadgeRect = (Rectangle)Get(visibleBadges[0]!, "BadgeBounds")!;
        object?[] hitArgs = [socialPage, cluesByNpc, sebBadgeRect.Center.X, sebBadgeRect.Center.Y, null];
        bool badgeHit = (bool)bridgeType.GetMethod("TryHitBadge", Flags)!.Invoke(null, hitArgs)!;
        Check(badgeHit && Equals(hitArgs[4], "Sebastian"),
            "SocialPageBridge.TryHitBadge hits Sebastian's pixel clue badge accurately");

        MethodInfo fmtMorning = bridgeType.GetMethod("FormatMorningEncounterHint", Flags)!;
        string? twoHint = (string?)fmtMorning.Invoke(null, [new[] { "阿比盖尔", "塞巴斯蒂安" }, "、",
            (Func<string, object, string>)((k, a) => $"{k}:{a.GetType().GetProperty("names")!.GetValue(a)}")]);
        string? fiveHint = (string?)fmtMorning.Invoke(null, [new[] { "阿比盖尔", "塞巴斯蒂安", "山姆", "海莉", "艾米丽" }, "、",
            (Func<string, object, string>)((k, a) => $"{k}:{a.GetType().GetProperty("names")!.GetValue(a)}/{a.GetType().GetProperty("count")!.GetValue(a)}")]);
        Check(twoHint == "clue.morning-hint:阿比盖尔、塞巴斯蒂安" && fiveHint == "clue.morning-hint-many:阿比盖尔、塞巴斯蒂安/5",
            "Morning encounter hint lists up to 3 names and truncates >3 names cleanly");

        Action restoreCallback = (Action)bridgeType.GetMethod("CreateRestoreCallback", Flags)!.Invoke(null, [gameMenu, socialPage])!;
        Call(app, "OpenFromSocialPage", "Sebastian", restoreCallback);
        if (monProxy.Messages.Count > 0)
            foreach (string msg in monProxy.Messages) Console.WriteLine("MONITOR: " + msg);
        Check(activeMenuField?.GetType().Name == "GalleryCharacterMenu"
            && Equals(Get(Get(activeMenuField!, "character")!, "Name"), "Sebastian"),
            "pressing Gallery key on SocialPage opens Sebastian's GalleryCharacterMenu directly");

        // Drill into Detail and press Back twice: first returns to Sebastian's Album, second restores SocialPage!
        Call(app, "OpenEvent", New("EventIdentity", "Data/Events/Mountain", "92001"));
        Check(activeMenuField?.GetType().Name == "GalleryEventDetailMenu", "drilled down from Sebastian Album to Detail");

        activeMenuField!.receiveGamePadButton(Buttons.B);
        Check(activeMenuField?.GetType().Name == "GalleryCharacterMenu", "first B returns from Detail to Sebastian Album");

        activeMenuField!.receiveGamePadButton(Buttons.B);
        Check(ReferenceEquals(activeMenuField, gameMenu)
            && gameMenu.currentTab == GameMenu.socialTab
            && (int)typeof(SocialPage).GetField("slotPosition", Flags)!.GetValue(socialPage)! == 3
            && ReferenceEquals(socialPage.currentlySnappedComponent, socialPage.characterSlots[3]),
            "second B seamlessly restores original GameMenu SocialPage, slotPosition=3, and controller focus");

        // 2) Free-cursor mouse hover mode on Sebastian (snappyMenus = false)
        Game1.options.snappyMenus = false;
        cursor = socialPage.characterSlots[3].bounds.Center;
        inspectArgs = [null, null, null];
        onSocial = (bool)bridgeType.GetMethod("TryInspectActiveMenu", Flags)!.Invoke(null, inspectArgs)!;
        Check(onSocial && Equals(inspectArgs[2], "Sebastian"),
            "SocialPageBridge detects mouse-hovered Sebastian when snappyMenus is off");

        restoreCallback = (Action)bridgeType.GetMethod("CreateRestoreCallback", Flags)!.Invoke(null, [gameMenu, socialPage])!;
        Call(app, "OpenFromSocialPage", "Sebastian", restoreCallback);
        Check(activeMenuField?.GetType().Name == "GalleryCharacterMenu", "mouse hover SocialPage shortcut opens Sebastian Album");
        Call(app, "Back");
        Check(ReferenceEquals(activeMenuField, gameMenu)
            && (int)typeof(SocialPage).GetField("slotPosition", Flags)!.GetValue(socialPage)! == 3,
            "Back restores SocialPage and scroll position in mouse mode");

        // 3) Unmet villager Leo (IsMet = false, unlockAll = false) never leaks locked CharacterMenu
        cursor = socialPage.characterSlots[4].bounds.Center;
        inspectArgs = [null, null, null];
        onSocial = (bool)bridgeType.GetMethod("TryInspectActiveMenu", Flags)!.Invoke(null, inspectArgs)!;
        Check(onSocial && Equals(inspectArgs[2], "Leo"), "SocialPageBridge reads hovered unmet slot name");
        restoreCallback = (Action)bridgeType.GetMethod("CreateRestoreCallback", Flags)!.Invoke(null, [gameMenu, socialPage])!;
        Call(app, "OpenFromSocialPage", "Leo", restoreCallback);
        Check(activeMenuField?.GetType().Name == "GalleryMenu",
            "unmet locked villager does not open GalleryCharacterMenu; falls back safely to Home");
        Call(app, "Back");
        Check(ReferenceEquals(activeMenuField, gameMenu), "Back from fallback Home also restores SocialPage");
    }

    private static void VerifyBookSpreadsPaginationAndMoments(bool snappy)
    {
        var (app, photoStore, favStore, nameStore, _) = BuildApplicationFixture(unlockAll: true);
        string mode = snappy ? "snapped: " : "free-cursor: ";

        // Seed 1 Wedding Memento for Abigail + custom name on 90102 + 9 favorite stories (90101..90107 + 92001 + 93001)
        byte[] png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a9d8AAAAASUVORK5CYII=");
        object weddingId = Call(Type("WeddingMemento"), "IdentityFor", "Abigail")!;
        object weddingDate = New("WeddingDateRecord", 2, "spring", 14);
        Type("EventPhotoStore").GetMethods(Flags).Single(m => m.Name == "Add" && m.GetParameters().Length == 4)
            .Invoke(photoStore, [weddingId, png, png, weddingDate]);

        Set(nameStore, "names", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["DATA/EVENTS/TOWN\u001f90102"] = "雨夜合奏"
        });
        for (int i = 1; i <= 7; i++)
            Call(favStore, "Toggle", New("EventIdentity", "Data/Events/Town", $"9010{i}"));
        Call(favStore, "Toggle", New("EventIdentity", "Data/Events/Mountain", "92001"));

        // 1) Open Home (22 villagers -> 2 pages: 18 + 4)
        Call(app, "Open");
        Check(activeMenuField?.GetType().Name == "GalleryMenu", mode + "Open() starts on GalleryMenu");
        playedSounds.Clear();
        activeMenuField!.receiveGamePadButton(Buttons.RightShoulder);
        Check(Equals(Get(activeMenuField, "currentPage"), 1) && playedSounds.Contains("shwip"),
            mode + "Home RB turns to page 2 and plays shwip");
        activeMenuField.receiveGamePadButton(Buttons.LeftShoulder);
        Check(Equals(Get(activeMenuField, "currentPage"), 0), mode + "Home LB turns back to page 1");

        // 2) Open Abigail's Album (1 pinned Wedding Memento + 13 Heart events = 14 cards -> 3 pages: 6 + 6 + 2)
        Call(app, "OpenCharacter", "Abigail");
        Check(activeMenuField?.GetType().Name == "GalleryCharacterMenu", mode + "opened Abigail GalleryCharacterMenu");
        var albumItems = ((IEnumerable)Get(activeMenuField!, "items")!).Cast<object>().ToArray();
        string[] cardTitles = (string[])Get(activeMenuField!, "cardTitles")!;
        Check(albumItems.Length == 14 && Equals(Get(albumItems[0], "IsWedding"), true),
            mode + "Abigail's Wedding Memento card is pinned at slot 0 ahead of 13 heart stories (3 pages)");
        Check(!(bool)Call(activeMenuField!, "IsReplayAvailable", albumItems[0])!,
            mode + "pinned Wedding Memento card is non-replayable");
        Check(cardTitles[2] == "雨夜合奏" && cardTitles.Skip(1).All(t => !t.Contains("901")),
            mode + "story card titles show custom name or Hearts · Location with zero raw EventId");

        // Verify wedding card does NOT inflate Abigail's heart node denominator (still 13!)
        object view = Get(activeMenuField!, "context")!;
        object abigailProgress = Call(view, "CharacterProgress", "Abigail")!;
        Check(Equals(Get(abigailProgress, "TotalNodes"), 13) && Equals(Get(abigailProgress, "CollectedNodes"), 1),
            mode + "Wedding Memento is excluded from Abigail's heart node denominator (1 / 13)");

        // Clicking Wedding Memento card opens GalleryPhotoMenu; physical keyboard arrows also work while gamepadControls=true!
        activeMenuField!.receiveKeyPress(Keys.Right);
        Check(activeMenuField!.currentlySnappedComponent?.myID == 2002,
            mode + "physical keyboard Right in GalleryCharacterMenu moves focus 2000 -> 2002 even when gamepadControls=true");
        activeMenuField.receiveKeyPress(Keys.Left);

        ClickableComponent weddingDetailsComp = activeMenuField!.allClickableComponents.Single(c => c.myID == 2000);
        activeMenuField.receiveLeftClick(weddingDetailsComp.bounds.Center.X, weddingDetailsComp.bounds.Center.Y);
        Check(activeMenuField?.GetType().Name == "GalleryPhotoMenu",
            mode + "clicking pinned Wedding Memento card opens GalleryPhotoMenu");
        activeMenuField!.receiveKeyPress(Keys.Down);
        Check(activeMenuField!.currentlySnappedComponent?.myID is >= 100 and <= 104,
            mode + "physical keyboard Down in GalleryPhotoMenu reaches footer buttons even when gamepadControls=true");
        activeMenuField!.receiveGamePadButton(Buttons.B);
        Check(activeMenuField?.GetType().Name == "GalleryCharacterMenu", mode + "B returns from Wedding photos to Album");

        // Verify single controller Buttons.A press on NextPage (9201) & PrevPage (9200) under full Game1.updateActiveMenu dispatch
        // flips EXACTLY 1 page (0 -> 1 -> 2 -> 1) and plays shwip once per press, never skipping page 1!
        Call(activeMenuField!, "FocusId", 9201);
        cursor = activeMenuField!.allClickableComponents.Single(c => c.myID == 9201).bounds.Center;
        playedSounds.Clear();
        PressControllerButton(activeMenuField!, Buttons.A);
        Check(Equals(Get(activeMenuField!, "currentPage"), 1) && playedSounds.Count(s => s == "shwip") == 1,
            mode + "single controller A on NextPage (9201) advances 1 page (0 -> 1) and plays shwip once");

        cursor = activeMenuField!.allClickableComponents.Single(c => c.myID == 9201).bounds.Center;
        PressControllerButton(activeMenuField!, Buttons.A);
        Check(Equals(Get(activeMenuField!, "currentPage"), 2) && playedSounds.Count(s => s == "shwip") == 2,
            mode + "second controller A on NextPage (9201) advances 1 page (1 -> 2)");

        Call(activeMenuField!, "FocusId", 9200);
        cursor = activeMenuField!.allClickableComponents.Single(c => c.myID == 9200).bounds.Center;
        PressControllerButton(activeMenuField!, Buttons.A);
        Check(Equals(Get(activeMenuField!, "currentPage"), 1) && playedSounds.Count(s => s == "shwip") == 3,
            mode + "single controller A on PrevPage (9200) steps back 1 page (2 -> 1) without skipping to 0");

        // On page 2 (index 1), open card 7 (index 7 -> detail id 2014) to Detail, and press B!
        ClickableComponent page2Card = activeMenuField!.allClickableComponents.Single(c => c.myID == 2014);
        activeMenuField.receiveLeftClick(page2Card.bounds.Center.X, page2Card.bounds.Center.Y);
        Check(activeMenuField?.GetType().Name == "GalleryEventDetailMenu", mode + "opened page-2 story Detail");
        activeMenuField!.receiveGamePadButton(Buttons.B);
        Check(activeMenuField?.GetType().Name == "GalleryCharacterMenu"
            && Equals(Get(activeMenuField!, "currentPage"), 1)
            && activeMenuField!.currentlySnappedComponent?.myID == 2014,
            mode + "returning from Detail restores Album page 2 and exact focused card 2014");

        // 3) Click Side Ribbon 2 (9101 -> Story Index / GalleryQueryMenu)
        ClickableComponent indexRibbon = activeMenuField!.allClickableComponents.Single(c => c.myID == 9101);
        activeMenuField.receiveLeftClick(indexRibbon.bounds.Center.X, indexRibbon.bounds.Center.Y);
        Check(activeMenuField?.GetType().Name == "GalleryQueryMenu",
            mode + "clicking side ribbon 2 switches to book-spread GalleryQueryMenu");

        int totalRows = ((ICollection)Get(activeMenuField!, "rows")!).Count;
        Check(totalRows > 6, mode + "Story Index has >6 rows spanning multiple book pages");
        activeMenuField!.receiveGamePadButton(Buttons.RightShoulder);
        Check(Equals(Get(activeMenuField!, "scroll"), 1), mode + "Story Index RB turns to page 2");
        activeMenuField!.receiveGamePadButton(Buttons.LeftShoulder);
        Check(Equals(Get(activeMenuField!, "scroll"), 0), mode + "Story Index LB turns back to page 1");

        // Seed session clues in view and toggle inline EncounterOnly filter (component 1105)
        var clueDict = new Dictionary<object, object>();
        Type clueEnum = Type("EncounterClue");
        object availableToday = Enum.Parse(clueEnum, "AvailableToday");
        object readyForTiming = Enum.Parse(clueEnum, "ReadyForTiming");
        object noneClue = Enum.Parse(clueEnum, "None");
        object viewCtx = Get(activeMenuField, "context")!;
        // Rebuild queryIndex with deterministic clues by setting eventClues and clearing cached queryIndex
        IDictionary eventCluesMap = (IDictionary)Activator.CreateInstance(
            typeof(Dictionary<,>).MakeGenericType(Type("EventIdentity"), clueEnum))!;
        foreach (object ev in (IEnumerable)Get(Get(viewCtx, "Catalog")!, "StoryEntries")!)
        {
            object id = Get(Get(ev, "Resolved")!, "Identity")!;
            string eid = (string)Get(ev, "EventId")!;
            eventCluesMap[id] = eid == "90102" ? availableToday : eid == "92001" ? readyForTiming : noneClue;
        }
        Set(viewCtx, "eventClues", eventCluesMap);
        Set(viewCtx, "queryIndex", null);
        // Re-open StoryIndex tab so index picks up the deterministic clues
        Call(app, "OpenMoments");
        Call(app, "SwitchTab", Enum.Parse(Type("GalleryBookTab"), "StoryIndex"));
        Check(activeMenuField?.GetType().Name == "GalleryQueryMenu", mode + "re-opened StoryIndex with seeded clues");

        ClickableComponent encounterRow = activeMenuField!.allClickableComponents.Single(c => c.myID == 1105);
        activeMenuField.receiveLeftClick(encounterRow.bounds.Center.X, encounterRow.bounds.Center.Y);
        int encounterCount = ((ICollection)Get(activeMenuField, "rows")!).Count;
        Check(encounterCount == 2,
            mode + "clicking inline Encounter Clues filter (1105) filters Story Index down to the 2 AvailableToday/ReadyForTiming stories");

        // Verify in-book dropdown overlay on NPC filter row (1101) without opening legacy GalleryFilterMenu!
        ClickableComponent npcFilterRow = activeMenuField.allClickableComponents.Single(c => c.myID == 1101);
        activeMenuField.receiveLeftClick(npcFilterRow.bounds.Center.X, npcFilterRow.bounds.Center.Y);
        Check(activeMenuField.GetType().Name == "GalleryQueryMenu" && Equals(Get(activeMenuField, "IsDropdownOpen"), true)
            && Get(activeMenuField, "dropdownSearch") is not null,
            mode + "clicking NPC filter row opens in-book parchment dropdown with embedded search box (>8 NPCs)");
        activeMenuField.receiveGamePadButton(Buttons.B);
        Check(activeMenuField.GetType().Name == "GalleryQueryMenu" && Equals(Get(activeMenuField, "IsDropdownOpen"), false),
            mode + "pressing B closes dropdown overlay without leaving GalleryQueryMenu");
        // Reset EncounterOnly before switching to Moments
        ClickableComponent resetFilterBtn = activeMenuField.allClickableComponents.Single(c => c.myID == 1002);
        activeMenuField.receiveLeftClick(resetFilterBtn.bounds.Center.X, resetFilterBtn.bounds.Center.Y);

        // 4) Click Side Ribbon 3 (9102 -> Treasured Moments / GalleryMomentsMenu: 1 Wedding + 8 Favorites = 9 items -> 2 pages: 8 + 1)
        ClickableComponent momentsRibbon = activeMenuField.allClickableComponents.Single(c => c.myID == 9102);
        activeMenuField.receiveLeftClick(momentsRibbon.bounds.Center.X, momentsRibbon.bounds.Center.Y);
        Check(activeMenuField?.GetType().Name == "GalleryMomentsMenu",
            mode + "clicking side ribbon 3 switches to GalleryMomentsMenu");
        int momentCount = ((ICollection)Get(activeMenuField!, "items")!).Count;
        Check(momentCount == 9, mode + "Treasured Moments combines 1 Wedding Memento + 8 Favorite stories (9 Polaroid cards)");

        // Verify left bottom row <-> bottom-left ribbons and centered right pager <-> right bottom row column-preserving Up/Down
        Call(activeMenuField!, "Focus", 2004); // slot 2 (left page col 0, row 1)
        activeMenuField!.receiveKeyPress(Keys.Down);
        Check(activeMenuField!.currentlySnappedComponent?.myID == 9100,
            mode + "Moments Down from left bottom-left card (2004) enters Ribbon 0 (9100)");
        activeMenuField.receiveKeyPress(Keys.Up);
        Check(activeMenuField.currentlySnappedComponent?.myID == 2004,
            mode + "Moments Up from Ribbon 0 (9100) returns to left bottom-left card (2004) without jumping to top row");

        Call(activeMenuField, "Focus", 9200); // PrevPage on right page
        activeMenuField.receiveKeyPress(Keys.Up);
        Check(activeMenuField.currentlySnappedComponent?.myID == 2012,
            mode + "Moments Up from PrevPage (9200) focuses right-page col 2 bottom card slot 6 (2012)");
        Call(activeMenuField, "Focus", 9201); // NextPage on right page
        activeMenuField.receiveKeyPress(Keys.Up);
        Check(activeMenuField.currentlySnappedComponent?.myID == 2014,
            mode + "Moments Up from NextPage (9201) focuses right-page col 3 bottom card slot 7 (2014)");

        playedSounds.Clear();
        activeMenuField!.receiveGamePadButton(Buttons.RightShoulder);
        Check(Equals(Get(activeMenuField!, "currentPage"), 1) && playedSounds.Contains("shwip"),
            mode + "Moments RB flips from page 1 (8 cards) to page 2 (9th card) with shwip");

        ClickableComponent ninthCard = activeMenuField!.allClickableComponents.Single(c => c.myID == 2016);
        activeMenuField.receiveLeftClick(ninthCard.bounds.Center.X, ninthCard.bounds.Center.Y);
        Check(activeMenuField?.GetType().Name == "GalleryEventDetailMenu"
            && Equals(Get(Get(activeMenuField!, "state")!, "OriginTab")?.ToString(), "Moments"),
            mode + "opening 9th Moment card opens Detail with Moments ribbon highlighted");
        activeMenuField!.receiveGamePadButton(Buttons.B);
        Check(activeMenuField?.GetType().Name == "GalleryMomentsMenu" && Equals(Get(activeMenuField!, "currentPage"), 1),
            mode + "B from Detail returns to Moments page 2");
        activeMenuField!.receiveGamePadButton(Buttons.B);
        Check(activeMenuField?.GetType().Name == "GalleryMenu",
            mode + "B from Moments returns to Villager Overview Home instead of exiting the gallery");
    }

    private static (object App, object PhotoStore, object FavStore, object NameStore, FakeProxy MonitorProxy) BuildApplicationFixture(bool unlockAll)
    {
        string[] npcNames =
        [
            "Abigail", "Sebastian", "Sam", "Haley", "Emily", "Maru", "Leah", "Penny", "Alex", "Elliott",
            "Harvey", "Shane", "Leo", "Krobus", "Linus", "Robin", "Demetrius", "Caroline", "Pierre", "Gus",
            "Clint", "Willy", "Evelyn"
        ];

        var chars = Array.CreateInstance(Type("GalleryCharacter"), npcNames.Length);
        var heartEventsList = new List<object>();

        for (int i = 0; i < npcNames.Length; i++)
        {
            string name = npcNames[i];
            bool met = name != "Leo";
            chars.SetValue(New("GalleryCharacter", name, name, met, met ? 1000 : 0), i);

            if (name == "Abigail")
            {
                for (int e = 1; e <= 13; e++)
                    heartEventsList.Add(MakeGalleryEvent($"901{e:D2}", "Town", $"901{e:D2}/f Abigail {e * 200}", name, e * 200));
            }
            else
            {
                string id = name == "Sebastian" ? "92001" : $"93{i:D3}";
                string loc = name == "Sebastian" ? "Mountain" : "Town";
                heartEventsList.Add(MakeGalleryEvent(id, loc, $"{id}/f {name} 500", name, 500));
            }
        }

        var eventsArr = Array.CreateInstance(Type("GalleryEvent"), heartEventsList.Count);
        for (int i = 0; i < heartEventsList.Count; i++)
            eventsArr.SetValue(heartEventsList[i], i);

        object catalogSnapshot = New("GalleryCatalog", chars, eventsArr, Array.CreateInstance(Type("GalleryEvent"), 0));

        IMonitor monitor = DispatchProxy.Create<IMonitor, FakeProxy>();
        IModHelper helper = DispatchProxy.Create<IModHelper, FakeProxy>();
        ITranslationHelper i18n = DispatchProxy.Create<ITranslationHelper, DraftTranslation>();
        ((FakeProxy)(object)helper).Values["get_Translation"] = i18n;
        ((FakeProxy)(object)helper).Values["get_DirectoryPath"] = "";

        object catalogCache = FormatterServices.GetUninitializedObject(Type("GalleryCatalogCache"));
        currentCatalogFixture = catalogSnapshot;

        object inMemFs = New("InMemoryStorageFileSystem");
        object profile = New("SaveProfileKey", 77UL, 9001L);
        string virtualRoot = Path.Combine(Path.GetTempPath(), "InMemStage4-" + Guid.NewGuid().ToString("N"));
        object photoStore = New("EventPhotoStore", Path.Combine(virtualRoot, "photos"), profile, (Action<string>)(_ => { }), inMemFs);
        object photos = New("GalleryPhotos", helper, monitor, (Action<string, bool>)((_, _) => { }));
        Set(photos, "store", photoStore);

        object favStore = New("FavoriteEventStore", Path.Combine(virtualRoot, "user-data"), (Action<string>)(_ => { }), inMemFs);
        object nameStore = New("EventNameStore", Path.Combine(virtualRoot, "user-data"), (Action<string>)(_ => { }));
        Set(nameStore, "names", new Dictionary<string, string>(StringComparer.Ordinal));

        object coordinator = FormatterServices.GetUninitializedObject(Type("ReplayCoordinator"));
        object replayService = New("ReplayService", helper, monitor, coordinator,
            (Func<bool>)(() => true), (Func<bool>)(() => false), (Func<bool>)(() => unlockAll), (Func<bool>)(() => false), null);

        object appearance = typeof(DispatchProxy).GetMethods()
            .Single(m => m.Name == "Create" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0)
            .MakeGenericMethod(Type("Appearance.ICharacterAppearance"), typeof(FakeProxy))
            .Invoke(null, null)!;
        Texture2D dummyTex = (Texture2D)FormatterServices.GetUninitializedObject(typeof(Texture2D));
        object textures = New("GalleryTextures", dummyTex, dummyTex, dummyTex, dummyTex, dummyTex, dummyTex, dummyTex, dummyTex, dummyTex);

        var resolvedParam = System.Linq.Expressions.Expression.Parameter(Type("ResolvedEvent"), "resolved");
        object sampleDisplay = New("EventSourceDisplay", "Vanilla", "Confirmed", Array.Empty<string>());
        Delegate sourceDetails = System.Linq.Expressions.Expression.Lambda(
            typeof(Func<,>).MakeGenericType(Type("ResolvedEvent"), Type("EventSourceDisplay")),
            System.Linq.Expressions.Expression.Constant(sampleDisplay, Type("EventSourceDisplay")),
            resolvedParam).Compile();

        object app = New("GalleryApplication", helper, monitor, catalogCache, photos, replayService,
            (Func<bool>)(() => unlockAll), (Action)(() => { }), appearance, sourceDetails, nameStore, favStore);
        Set(app, "textures", textures);

        return (app, photoStore, favStore, nameStore, (FakeProxy)(object)monitor);
    }

    private static object MakeGalleryEvent(string id, string location, string rawKey, string npc, int points)
    {
        object identity = New("EventIdentity", "Data/Events/" + location, id);
        string script = $"none/0 0/{npc} 5 5 2 farmer 5 6 0/speak {npc} \"Hello {id}\"/end";
        object fragments = New("EventFragments", new[] { script }, Array.Empty<string>(), null, null, null, null);
        object resolved = New("ResolvedEvent", identity, location, rawKey, script, fragments, "def-" + id, "scr-" + id);
        var owners = Array.CreateInstance(Type("EventOwner"), 1);
        owners.SetValue(New("EventOwner", npc, (int?)points), 0);
        object ownership = New("EventOwnership", Enum.Parse(Type("OwnershipKind"), "Direct"), owners, "direct");
        object ev = New("GalleryEvent", resolved, ownership);
        Set(ev, "Kind", Enum.Parse(Type("StoryKind"), "Heart"));
        Set(ev, "RelatedNpcNames", new[] { npc });
        return ev;
    }

    private static bool SkipVoid() => false;
    private static bool ReturnTrue(ref bool __result) { __result = true; return false; }
    private static bool GetCatalogFixture(ref object __result) { __result = currentCatalogFixture!; return false; }
    private static bool EmptyConditions(ref object __result)
    {
        __result = Array.CreateInstance(Type("ConditionDisplayItem"), 0);
        return false;
    }
    private static bool GetActiveMenu(ref IClickableMenu? __result) { __result = activeMenuField; return false; }
    private static bool SetActiveMenu(IClickableMenu? __0) { activeMenuField = __0; return false; }
    private static bool GetMouseX(ref int __result) { __result = cursor.X; return false; }
    private static bool GetMouseY(ref int __result) { __result = cursor.Y; return false; }
    private static bool SnapCursor(IClickableMenu __instance)
    {
        if (__instance.currentlySnappedComponent is { } c)
            cursor = c.bounds.Center;
        return false;
    }
    private static bool MeasureFakeString(string? __0, ref Vector2 __result)
    {
        __result = new Vector2((__0?.Length ?? 0) * 10f, 28f);
        return false;
    }
    private static bool IdentityParseText(string? __0, ref string __result)
    {
        __result = __0 ?? "";
        return false;
    }
    private static bool RecordSound(string __0)
    {
        playedSounds.Add(__0);
        return false;
    }
    private static bool InitCharacterNames(object __instance, object[] __args)
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (object ch in (IEnumerable)Get(__args[0], "Characters")!)
        {
            string n = (string)Get(ch, "Name")!;
            names[n] = (string)Get(ch, "DisplayName")!;
        }
        Set(__instance, "names", names);
        Set(__instance, "aliases", new Dictionary<string, string>(StringComparer.Ordinal));
        Set(__instance, "sceneAliases", Activator.CreateInstance(
            typeof(Dictionary<,>).MakeGenericType(Type("EventIdentity"), typeof(Dictionary<string, string>))));
        Set(__instance, "i18n", __args[1]);
        return false;
    }

    private static Type Type(string name) => mod.GetType("StardewGallery." + name, true)!;
    private static object New(string name, params object?[] args) => Activator.CreateInstance(Type(name), Flags, null, args, null)!;
    private static object? Call(object target, string name, params object?[] args)
    {
        Type t = target as Type ?? target.GetType();
        MethodInfo method = t.GetMethods(Flags).Single(m => m.Name == name && m.GetParameters().Length == args.Length);
        return method.Invoke(target is Type ? null : target, args);
    }
    private static object? Get(object target, string name)
    {
        object? inst = target is Type ? null : target;
        for (Type? t = target as Type ?? target.GetType(); t is not null; t = t.BaseType)
        {
            if (t.GetProperty(name, Flags) is { } p) return p.GetValue(inst);
            if (t.GetField(name, Flags) is { } f) return f.GetValue(inst);
        }
        throw new MissingMemberException(target.GetType().Name, name);
    }
    private static void Set(object target, string name, object? value)
    {
        object? inst = target is Type ? null : target;
        for (Type? t = target as Type ?? target.GetType(); t is not null; t = t.BaseType)
        {
            if (t.GetProperty(name, Flags) is { CanWrite: true } p) { p.SetValue(inst, value); return; }
            if ((t.GetField(name, Flags) ?? t.GetField("<" + name + ">k__BackingField", Flags)) is { } f) { f.SetValue(inst, value); return; }
        }
        throw new MissingMemberException(target.GetType().Name, name);
    }
    // Exact 1.6.15 Game1.updateActiveMenu fresh controller button dispatch contract:
    // receiveGamePadButton -> conditional A/X mouse emulation -> mapGamePadButtonToKey receiveKeyPress.
    private static void PressControllerButton(IClickableMenu menu, Buttons button)
    {
        activeMenuField = menu;
        menu.receiveGamePadButton(button);
        if (!ReferenceEquals(activeMenuField, menu))
            return;
        if (!menu.areGamePadControlsImplemented())
        {
            if (button == Buttons.A)
                menu.receiveLeftClick(cursor.X, cursor.Y);
            else if (button == Buttons.X)
                menu.receiveRightClick(cursor.X, cursor.Y);
        }
        if (ReferenceEquals(activeMenuField, menu))
            menu.receiveKeyPress(Utility.mapGamePadButtonToKey(button));
    }

    private sealed class DummyMenu() : IClickableMenu(0, 0, 100, 100, false);

    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition)
            throw new InvalidOperationException("Stage4RuntimeChecks failed: " + message);
    }
}
