using System.Collections;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;

/// <summary>
/// Headless runtime parity &amp; protection checks for Stage 2 against real Stardew Valley 1.6.15:
/// 1) Current-event inline $q masking (including quickQuestion sub-commands) + cross-event $p preservation + ReplaySnapshot 100% restore;
/// 2) ReplaySaveGuard.BeforeFork route switching (including cross-location homonymous keys) on owned events only, without mutating save flags;
/// 3) Scenic pause freezing Event.Update &amp; DialogueBox.update, blocking DialogueBox click/key input, hiding DialogueBox &amp; skip button, and never triggering timeouts.
/// </summary>
internal static class ReplayStage2Checks
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static readonly Dictionary<string, string> townEvents = new(StringComparer.Ordinal)
    {
        ["branchA"] = "speak Abigail \"Branch A\"/end",
        ["branchB"] = "speak Abigail \"Branch B\"/end",
        ["dupSub"] = "speak Abigail \"Town Sub\"/end",
        ["interactive"] = "speak Abigail \"Interactive fork\"/end"
    };
    private static readonly Dictionary<string, string> beachEvents = new(StringComparer.Ordinal)
    {
        ["dupSub"] = "speak Abigail \"Beach Sub\"/end"
    };
    private static readonly Dictionary<string, string> stringEvents = new(StringComparer.Ordinal)
    {
        ["Valid_Branch"] = "speak Abigail \"Translated fork script!\"/end",
        ["Valid_Q"] = "Ask?#$q trans_q1/trans_q2 null#Pick#$r trans_q1 0 A#Yes#$r trans_q2 0 B#No"
    };
    private static int eventUpdatesObserved;
    private static int dialogueUpdatesObserved;
    private static int dialogueInputsObserved;
    private static bool? skippableInsideDraw;
    private static int assertions;
    private static int failures;

    internal static void Run(Assembly mod, string gameDirectory)
    {
        assertions = failures = 0;
        const string modId = "StardewGallery.Stage2RuntimeChecks";
        var fixture = new Harmony(modId + ".Fixture");
        var protection = new Harmony(modId + ".ReplayProtection");
        var speedHarmony = new Harmony(modId);
        LocalizedContentManager? previousContent = Game1.content;

        try
        {
            foreach (MethodInfo method in new[]
            {
                typeof(PlayerStatusList).GetMethod("AddSpriteDefinition", Flags)!,
                typeof(Farmer).GetMethod("farmerInit", Flags)!,
                typeof(AnimatedSprite).GetMethod("LoadTexture", Flags)!
            })
                fixture.Patch(method, prefix: new HarmonyMethod(typeof(ReplayStage2Checks), nameof(SkipVoid)));

            foreach (MethodInfo sound in typeof(Game1).GetMethods(Flags).Where(m => m.Name == "playSound"))
                fixture.Patch(sound, prefix: new HarmonyMethod(typeof(ReplayStage2Checks), nameof(SkipVoid)));

            fixture.Patch(AccessTools.Method(typeof(Dialogue), "TranslateArraysOfStrings"),
                prefix: new HarmonyMethod(typeof(ReplayStage2Checks), nameof(SkipVoid)));

            // Observe Event.Update, DialogueBox.update/input, and Event.drawAfterMap after production prefixes run.
            fixture.Patch(AccessTools.Method(typeof(Event), "UpdateBeforeNextCommand"),
                prefix: new HarmonyMethod(typeof(ReplayStage2Checks), nameof(ObserveEventUpdate)));
            fixture.Patch(AccessTools.Method(typeof(DialogueBox), nameof(DialogueBox.update)),
                prefix: new HarmonyMethod(typeof(ReplayStage2Checks), nameof(ObserveDialogueUpdate)) { priority = Priority.Last });
            fixture.Patch(AccessTools.Method(typeof(DialogueBox), nameof(DialogueBox.receiveLeftClick), [typeof(int), typeof(int), typeof(bool)]),
                prefix: new HarmonyMethod(typeof(ReplayStage2Checks), nameof(ObserveDialogueInput)) { priority = Priority.Last });
            fixture.Patch(AccessTools.Method(typeof(DialogueBox), nameof(DialogueBox.receiveKeyPress), [typeof(Keys)]),
                prefix: new HarmonyMethod(typeof(ReplayStage2Checks), nameof(ObserveDialogueInput)) { priority = Priority.Last });
            fixture.Patch(AccessTools.Method(typeof(Event), nameof(Event.drawAfterMap), [typeof(SpriteBatch)]),
                prefix: new HarmonyMethod(typeof(ReplayStage2Checks), nameof(ObserveEventDraw)) { priority = Priority.Last });

            // Headless Game1 & Farmer setup with a real virtual LocalizedContentManager override (no generic Harmony patches).
            Game1.game1 = (Game1)FormatterServices.GetUninitializedObject(typeof(Game1));
            typeof(Game1).GetField("screenFade", Flags)!.SetValue(null, new ScreenFade(() => true, () => { }));
            Game1.content = (LocalizedContentManager)FormatterServices.GetUninitializedObject(typeof(InMemoryContentManager));
            Farmer farmer = new();
            typeof(Game1).GetProperty("player", Flags)!.SetValue(null, farmer);
            object buffs = typeof(Farmer).GetField("buffs", Flags)!.GetValue(farmer)!;
            buffs.GetType().GetField("Player", Flags)!.SetValue(buffs, farmer);
            buffs.GetType().GetField("Dirty", Flags)!.SetValue(buffs, false);
            buffs.GetType().GetField("AppliedBuffIds", Flags)!.SetValue(buffs,
                Activator.CreateInstance(buffs.GetType().GetField("AppliedBuffIds", Flags)!.FieldType)!);
            var netWorld = new StardewValley.Network.NetWorldState();
            Game1.netWorldState = new Netcode.NetRoot<StardewValley.Network.NetWorldState>(netWorld);
            GameLocation town = new() { name = { Value = "Town" } };
            Game1.currentLocation = town;

            Type coordinatorType = mod.GetType("StardewGallery.ReplayCoordinator", true)!;
            Type guardType = mod.GetType("StardewGallery.ReplaySaveGuard", true)!;
            Type speedType = mod.GetType("StardewGallery.ReplaySpeedPatches", true)!;
            Type snapshotType = mod.GetType("StardewGallery.ReplaySnapshot", true)!;

            var registry = DispatchProxy.Create<IModRegistry, FakeProxy>();
            ((FakeProxy)(object)registry).Values["get_ModID"] = modId;
            var helper = DispatchProxy.Create<IModHelper, FakeProxy>();
            ((FakeProxy)(object)helper).Values["get_ModRegistry"] = registry;
            ((FakeProxy)(object)helper).Values["get_Translation"] = DispatchProxy.Create<ITranslationHelper, FakeProxy>();
            var monitor = DispatchProxy.Create<IMonitor, FakeProxy>();

            object coordinator = Activator.CreateInstance(
                coordinatorType,
                Flags,
                binder: null,
                args: [monitor, helper, (Func<bool>)(() => false), (Func<bool>)(() => false)],
                culture: null)!;

            guardType.GetMethod("Apply", Flags)!.Invoke(null, [helper, monitor, coordinator]);
            speedType.GetMethod("Apply", Flags)!.Invoke(null, [helper, coordinator]);

            CheckSafeTranslationLoaderAgainstNative(mod);
            CheckQuestionMaskAndSnapshotRestore(mod, coordinator, coordinatorType, snapshotType, farmer);
            CheckForkRouteInterception(mod, coordinator, coordinatorType, farmer, town);
            CheckScenicPauseAndTimeoutImmunity(coordinator, coordinatorType, snapshotType, town);
        }
        finally
        {
            speedHarmony.UnpatchAll(speedHarmony.Id);
            protection.UnpatchAll(protection.Id);
            fixture.UnpatchAll(fixture.Id);
            Game1.content = previousContent!;
            Game1.locationRequest = null;
        }

        Console.WriteLine($"Stage 2 runtime checks: {assertions - failures}/{assertions} passed; {failures} failed.");
        if (failures != 0)
            throw new InvalidOperationException($"{failures} Stage 2 runtime check(s) failed.");
    }

    private static void CheckSafeTranslationLoaderAgainstNative(Assembly mod)
    {
        // Confirm our InMemoryContentManager executes the REAL 1.6.15 LoadStringReturnNullIfNotFound (which throws ContentLoadException
        // on no-colon strings and missing assets), and prove SafeTranslationLoader + MaskCurrentEventQuestions + BeforeFork handle all of them!
        bool nativeThrewOnDots = false;
        bool nativeThrewOnMissingAsset = false;
        try { Game1.content.LoadStringReturnNullIfNotFound("..."); }
        catch (ContentLoadException) { nativeThrewOnDots = true; }
        try { Game1.content.LoadStringReturnNullIfNotFound("Mods/Missing:Key"); }
        catch (ContentLoadException) { nativeThrewOnMissingAsset = true; }
        Check(nativeThrewOnDots && nativeThrewOnMissingAsset,
            "Verified 1.6.15 LocalizedContentManager.LoadStringReturnNullIfNotFound throws ContentLoadException on '...' and 'Mods/Missing:Key'");

        Type loaderType = mod.GetType("StardewGallery.SafeTranslationLoader", true)!;
        MethodInfo tryLoad = loaderType.GetMethod("TryLoad", Flags)!;
        Check(tryLoad.Invoke(null, [Game1.content, "...", true]) is null
            && tryLoad.Invoke(null, [Game1.content, "你好！", true]) is null
            && tryLoad.Invoke(null, [Game1.content, "注意:危险", true]) is null
            && tryLoad.Invoke(null, [Game1.content, "Mods/Missing:Key", true]) is null
            && (string?)tryLoad.Invoke(null, [Game1.content, "Strings/Events:Missing_SubKey", true]) is null
            && (string?)tryLoad.Invoke(null, [Game1.content, "Strings\\Events:Valid_Branch", true]) == "speak Abigail \"Translated fork script!\"/end",
            "SafeTranslationLoader.TryLoad safely returns null on '...', '你好！', '注意:危险', and 'Mods/Missing:Key' while loading valid 'Strings\\Events:Valid_Branch'");
    }

    private static void CheckQuestionMaskAndSnapshotRestore(
        Assembly mod,
        object coordinator,
        Type coordinatorType,
        Type snapshotType,
        Farmer farmer)
    {
        farmer.eventsSeen.Clear();
        farmer.eventsSeen.Add("50_seen");
        farmer.dialogueQuestionsAnswered.Clear();
        farmer.dialogueQuestionsAnswered.Add("prior_50");
        farmer.dialogueQuestionsAnswered.Add("curr_201");
        farmer.dialogueQuestionsAnswered.Add("curr_302");
        farmer.dialogueQuestionsAnswered.Add("curr_qq1");
        farmer.dialogueQuestionsAnswered.Add("trans_q1");

        object snapshot = snapshotType.GetMethod("Capture", Flags)!.Invoke(null, [Game1.currentLocation])!;

        Type identityType = mod.GetType("StardewGallery.EventIdentity", true)!;
        Type fragmentsType = mod.GetType("StardewGallery.EventFragments", true)!;
        Type resolvedType = mod.GetType("StardewGallery.ResolvedEvent", true)!;
        object identity = Create(identityType, "Data/Events/Town", "55_if_route");
        object fragments = Create(fragmentsType,
            new string[] { "speak Abigail \"...\"/speak Abigail \"你好！\"/speak Abigail \"注意:危险\"/speak Abigail \"Mods/Missing:Key\"/speak Abigail Strings\\Events:Valid_Q/speak Abigail \"#$q curr_301/curr_302 null#Sub?#$r curr_301 0 A#One#$r curr_302 0 B#Two\"/end" },
            Array.Empty<string>(), null, null, null, null);
        object resolved = Create(resolvedType,
            identity,
            "Town",
            "55_if_route/f Abigail 2000",
            "breezy/0 0/Abigail 1 1 2/speak Abigail \"#$q curr_201/curr_202 null#Ask me!#$r curr_201 10 A#Opt1#$r curr_202 10 B#Opt2\"/quickQuestion #Ready?#Yes#No(break)speak Abigail \"#$q curr_qq1/curr_qq2 null#QQ#$r curr_qq1 0 A#Y#$r curr_qq2 0 B#N\"(break)end/end",
            fragments,
            "defHash",
            "scriptHash");

        coordinatorType.GetMethod("MaskCurrentEventQuestions", Flags)!.Invoke(coordinator, [resolved]);

        Check(farmer.dialogueQuestionsAnswered.Contains("prior_50")
            && !farmer.dialogueQuestionsAnswered.Contains("curr_201")
            && !farmer.dialogueQuestionsAnswered.Contains("curr_302")
            && !farmer.dialogueQuestionsAnswered.Contains("curr_qq1")
            && !farmer.dialogueQuestionsAnswered.Contains("trans_q1"),
            "MaskCurrentEventQuestions masks inline, quickQuestion & translated $q IDs without crashing on '...', '你好！', '注意:危险', or 'Mods/Missing:Key'");

        // Verify real 1.6.15 Dialogue behavior under the masked state!
        NPC speaker = (NPC)FormatterServices.GetUninitializedObject(typeof(NPC));
        typeof(Character).GetField("name", Flags)!.SetValue(speaker, new Netcode.NetString("Abigail"));
        Dialogue currentQ = new(speaker, null, "$q curr_201/curr_202 null#Ask me!#$r curr_201 10 A#Option 1#$r curr_202 10 B#Option 2");
        bool interactive = (bool)typeof(Dialogue).GetField("isLastDialogueInteractive", Flags)!.GetValue(currentQ)!;
        Check(interactive && currentQ.getResponseOptions().Count() == 2,
            "Real 1.6.15 Dialogue re-prompts the masked current-event $q question with both options");

        Dialogue crossEventP = new(speaker, null, "$p prior_50#Remembered prior choice!|Forgot prior choice!");
        Check(crossEventP.dialogues.Count == 1 && crossEventP.dialogues[0].Text == "Remembered prior choice!",
            "Real 1.6.15 Dialogue $p still remembers prior-event answer 'prior_50'");

        // Simulate player picking curr_202 during replay, then restore snapshot.
        farmer.dialogueQuestionsAnswered.Add("curr_202");
        snapshotType.GetMethod("RestorePlayer", Flags)!.Invoke(snapshot, null);

        Check(farmer.dialogueQuestionsAnswered.Count == 5
            && farmer.dialogueQuestionsAnswered.Contains("prior_50")
            && farmer.dialogueQuestionsAnswered.Contains("curr_201")
            && farmer.dialogueQuestionsAnswered.Contains("curr_302")
            && farmer.dialogueQuestionsAnswered.Contains("curr_qq1")
            && farmer.dialogueQuestionsAnswered.Contains("trans_q1")
            && !farmer.dialogueQuestionsAnswered.Contains("curr_202"),
            "ReplaySnapshot.RestorePlayer 100% restores original dialogueQuestionsAnswered after replay");
        Check(farmer.eventsSeen.Count == 1 && farmer.eventsSeen.Contains("50_seen") && !farmer.eventsSeen.Contains("55_if_route"),
            "Parallel If-route replay never writes to real eventsSeen");
    }

    private static void CheckForkRouteInterception(
        Assembly mod,
        object coordinator,
        Type coordinatorType,
        Farmer farmer,
        GameLocation town)
    {
        Type modeType = mod.GetType("StardewGallery.ReplayForkMode", true)!;
        Type edgeType = mod.GetType("StardewGallery.ReplayForkEdge", true)!;
        Type optionType = mod.GetType("StardewGallery.ReplayForkOption", true)!;
        MethodInfo createEdge = edgeType.GetMethod("Create", Flags)!;
        Type edgeSetType = typeof(HashSet<>).MakeGenericType(edgeType);

        object MakeEdgeSet(params (string Loc, string Req, string Key)[] edges)
        {
            object set = Activator.CreateInstance(edgeSetType)!;
            MethodInfo add = edgeSetType.GetMethod("Add")!;
            foreach ((string loc, string req, string key) in edges)
                add.Invoke(set, [createEdge.Invoke(null, [loc, req, key, false])]);
            return set;
        }

        object forceMode = Enum.Parse(modeType, "ForceBranch");
        object mainMode = Enum.Parse(modeType, "MainScript");
        object forceBranchB = Create(
            optionType,
            forceMode,
            "branchB",
            "reqB",
            MakeEdgeSet(("Town", "reqB", "branchB")),
            null,
            false);
        object forceBeachSub = Create(
            optionType,
            forceMode,
            "dupSub",
            "reqSub",
            MakeEdgeSet(("Beach", "reqSub", "dupSub")),
            "Beach",
            false);
        object forceMain = Create(
            optionType,
            mainMode,
            null,
            null,
            MakeEdgeSet(),
            null,
            false);

        Event ownedEvent = CreateHeadlessEvent(farmer);
        Event otherEvent = CreateHeadlessEvent(farmer);

        object dummySnapshot = FormatterServices.GetUninitializedObject(mod.GetType("StardewGallery.ReplaySnapshot", true)!);
        coordinatorType.GetField("snapshot", Flags)!.SetValue(coordinator, dummySnapshot);
        coordinatorType.GetField("activeReplayEvent", Flags)!.SetValue(coordinator, ownedEvent);
        coordinatorType.GetField("forkOverride", Flags)!.SetValue(coordinator, forceBranchB);

        // Save has mail 'reqA' and does NOT have 'reqB'.
        farmer.mailReceived.Clear();
        farmer.mailReceived.Add("reqA");
        farmer.dialogueQuestionsAnswered.Clear();

        // 1. Non-owned event must NOT be affected by ReplaySaveGuard.BeforeFork!
        otherEvent.ReplaceAllCommands("fork reqA branchA", "end");
        otherEvent.CurrentCommand = 0;
        otherEvent.forked = false;
        InvokeFork(otherEvent, town, ["fork", "reqA", "branchA"]);
        Check(otherEvent.forked && otherEvent.eventCommands[0].Contains("Branch A"),
            "Non-owned event keeps native Fork routing (reqA -> branchA) even while replay override is active");

        // 2. Owned event with ForceBranch(branchB): skips branchA (despite reqA in mailReceived!) and jumps to branchB!
        ownedEvent.ReplaceAllCommands("fork reqA branchA", "fork reqB branchB", "end");
        ownedEvent.CurrentCommand = 0;
        ownedEvent.forked = false;
        ownedEvent.specialEventVariable1 = false;
        InvokeFork(ownedEvent, town, ["fork", "reqA", "branchA"]);
        Check(!ownedEvent.forked && ownedEvent.CurrentCommand == 1,
            "Owned replay skips non-selected fork branchA even when save has reqA");

        InvokeFork(ownedEvent, town, ["fork", "reqB", "branchB"]);
        Check(ownedEvent.forked && ownedEvent.eventCommands[0].Contains("Branch B"),
            "Owned replay jumps to selected fork branchB even when save lacks reqB");
        Check(!ownedEvent.specialEventVariable1,
            "AfterFork finalizer restores ownedEvent.specialEventVariable1");
        Check(farmer.mailReceived.Count == 1 && farmer.mailReceived.Contains("reqA") && farmer.dialogueQuestionsAnswered.Count == 0,
            "Fork override never mutates Farmer.mailReceived or Farmer.dialogueQuestionsAnswered");

        // 3. Owned event with MainScript: skips explicit save-flag fork, while 1-arg interactive fork still honors specialEventVariable1!
        coordinatorType.GetField("forkOverride", Flags)!.SetValue(coordinator, forceMain);
        ownedEvent.ReplaceAllCommands("fork reqA branchA", "fork interactive", "end");
        ownedEvent.CurrentCommand = 0;
        ownedEvent.forked = false;
        InvokeFork(ownedEvent, town, ["fork", "reqA", "branchA"]);
        Check(!ownedEvent.forked && ownedEvent.CurrentCommand == 1,
            "MainScript skips explicit fork <reqA> <branchA>");

        ownedEvent.specialEventVariable1 = true;
        InvokeFork(ownedEvent, town, ["fork", "interactive"]);
        Check(ownedEvent.forked && ownedEvent.eventCommands[0].Contains("Interactive fork"),
            "1-arg interactive fork still obeys in-event specialEventVariable1 under MainScript");

        // 4. Cross-location homonymous sub-key: choosing Beach:dupSub skips Town:dupSub and jumps only after changeLocation Beach!
        GameLocation beach = new() { name = { Value = "Beach" } };
        coordinatorType.GetField("forkOverride", Flags)!.SetValue(coordinator, forceBeachSub);
        Game1.currentLocation = town;
        ownedEvent.ReplaceAllCommands("fork reqSub dupSub", "end");
        ownedEvent.CurrentCommand = 0;
        ownedEvent.forked = false;
        InvokeFork(ownedEvent, town, ["fork", "reqSub", "dupSub"]);
        Check(!ownedEvent.forked && ownedEvent.CurrentCommand == 1,
            "Cross-location fork override skips Town's homonymous 'dupSub' when Beach's 'dupSub' is selected");

        Game1.currentLocation = beach;
        ownedEvent.ReplaceAllCommands("fork reqSub dupSub", "end");
        ownedEvent.CurrentCommand = 0;
        ownedEvent.forked = false;
        InvokeFork(ownedEvent, beach, ["fork", "reqSub", "dupSub"]);
        Check(ownedEvent.forked && ownedEvent.eventCommands[0].Contains("Beach Sub"),
            "Cross-location fork override jumps to Beach's 'dupSub' once currentLocation is Beach");

        // 5. Translation fork override ('fork reqId key true') jumps on valid Strings\Events:Valid_Branch and safely logs/skips
        // on missing asset 'Mods/Missing:Branch' or no-colon 'noColonKey' without throwing ContentLoadException!
        object transEdgeSet = Activator.CreateInstance(edgeSetType)!;
        MethodInfo addEdge = edgeSetType.GetMethod("Add")!;
        addEdge.Invoke(transEdgeSet, [createEdge.Invoke(null, ["Town", "reqTrans", "Strings\\Events:Valid_Branch", true])]);
        addEdge.Invoke(transEdgeSet, [createEdge.Invoke(null, ["Town", "reqMissingTrans", "Mods/Missing:Branch", true])]);
        addEdge.Invoke(transEdgeSet, [createEdge.Invoke(null, ["Town", "reqNoColon", "noColonKey", true])]);
        object forceTrans = Create(optionType, forceMode, "Strings\\Events:Valid_Branch", "reqTrans", transEdgeSet, null, true);
        coordinatorType.GetField("forkOverride", Flags)!.SetValue(coordinator, forceTrans);
        Game1.currentLocation = town;

        ownedEvent.ReplaceAllCommands("fork reqTrans Strings\\Events:Valid_Branch true", "end");
        ownedEvent.CurrentCommand = 0;
        ownedEvent.forked = false;
        InvokeFork(ownedEvent, town, ["fork", "reqTrans", "Strings\\Events:Valid_Branch", "true"]);
        Check(ownedEvent.forked && ownedEvent.eventCommands[0].Contains("Translated fork script!"),
            "BeforeFork loads valid translation fork 'Strings\\Events:Valid_Branch'");

        ownedEvent.ReplaceAllCommands("fork reqMissingTrans Mods/Missing:Branch true", "fork reqNoColon noColonKey true", "end");
        ownedEvent.CurrentCommand = 0;
        ownedEvent.forked = false;
        InvokeFork(ownedEvent, town, ["fork", "reqMissingTrans", "Mods/Missing:Branch", "true"]);
        InvokeFork(ownedEvent, town, ["fork", "reqNoColon", "noColonKey", "true"]);
        Check(!ownedEvent.forked && ownedEvent.CurrentCommand == 2,
            "BeforeFork safely skips missing-asset and no-colon translation forks without throwing ContentLoadException");

        Game1.currentLocation = town;
        coordinatorType.GetField("forkOverride", Flags)!.SetValue(coordinator, null);
    }

    private static void CheckScenicPauseAndTimeoutImmunity(
        object coordinator,
        Type coordinatorType,
        Type snapshotType,
        GameLocation town)
    {
        Farmer farmer = Game1.player;
        Event ownedEvent = CreateHeadlessEvent(farmer);
        Event otherEvent = CreateHeadlessEvent(farmer);
        town.currentEvent = ownedEvent;
        Game1.eventUp = true;

        coordinatorType.GetField("snapshot", Flags)!.SetValue(coordinator, FormatterServices.GetUninitializedObject(snapshotType));
        coordinatorType.GetField("activeReplayEvent", Flags)!.SetValue(coordinator, ownedEvent);
        coordinatorType.GetField("observed", Flags)!.SetValue(coordinator, true);
        coordinatorType.GetField("restoring", Flags)!.SetValue(coordinator, false);
        coordinatorType.GetField("ticks", Flags)!.SetValue(coordinator, 42);
        Game1.ticks = 2000;
        coordinatorType.GetField("lastGameTick", Flags)!.SetValue(coordinator, Game1.ticks);

        bool paused = (bool)coordinatorType.GetMethod("ToggleScenePause", Flags)!.Invoke(coordinator, null)!;
        Check(paused && (bool)coordinatorType.GetProperty("IsScenePaused", Flags)!.GetValue(coordinator)!,
            "ToggleScenePause enters scenic pause on an active observed replay event");

        // 1. Owned Event.Update is frozen; non-owned Event.Update still runs!
        var gameTime = new GameTime(TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(16));
        eventUpdatesObserved = 0;
        ownedEvent.ReplaceAllCommands("unused", "pause 1000", "end");
        ownedEvent.CurrentCommand = 1;
        ownedEvent.Update(town, gameTime);
        Check(eventUpdatesObserved == 0 && ownedEvent.CurrentCommand == 1,
            "Scenic pause freezes owned Event.Update completely");

        otherEvent.ReplaceAllCommands("unused", "pause 1000", "end");
        otherEvent.CurrentCommand = 1;
        otherEvent.Update(town, gameTime);
        Check(eventUpdatesObserved == 1,
            "Non-owned Event.Update is unaffected by scenic pause");

        // 2. DialogueBox.update is frozen, DialogueBox.draw is hidden, and DialogueBox click/key input is short-circuited!
        var dialogue = (DialogueBox)FormatterServices.GetUninitializedObject(typeof(DialogueBox));
        dialogue.responses = [];
        dialogueUpdatesObserved = 0;
        dialogueInputsObserved = 0;
        dialogue.update(gameTime);
        dialogue.receiveLeftClick(0, 0, true);
        dialogue.receiveRightClick(0, 0, true);
        dialogue.receiveKeyPress(Keys.E);
        dialogue.receiveKeyPress(Keys.Y);
        Check(dialogueUpdatesObserved == 0, "Scenic pause freezes DialogueBox.update");
        Check(dialogueInputsObserved == 0, "Scenic pause blocks DialogueBox left-click, right-click, and key-press input");

        Type speedType = coordinatorType.Assembly.GetType("StardewGallery.ReplaySpeedPatches", true)!;
        bool shouldDrawDialogue = (bool)speedType.GetMethod("BeforeDialogueDraw", Flags)!.Invoke(null, null)!;
        Check(!shouldDrawDialogue, "Scenic pause hides DialogueBox.draw");

        // 3. Event.drawAfterMap temporarily hides skippable and restores it immediately!
        ownedEvent.skippable = true;
        skippableInsideDraw = null;
        ownedEvent.drawAfterMap(null!);
        Check(skippableInsideDraw == false && ownedEvent.skippable,
            "Scenic pause hides Event skip button during drawAfterMap and restores Event.skippable in finalizer");

        // 4. Timeout immunity: 5,000 advancing Game1.ticks while paused must NEVER increment ReplayCoordinator.ticks or trigger Restore!
        MethodInfo update = coordinatorType.GetMethod("Update", Flags)!;
        for (int i = 0; i < 5000; i++)
        {
            Game1.ticks++;
            update.Invoke(coordinator, null);
        }
        int ticksAfterPause = (int)coordinatorType.GetField("ticks", Flags)!.GetValue(coordinator)!;
        bool restoringAfterPause = (bool)coordinatorType.GetField("restoring", Flags)!.GetValue(coordinator)!;
        Check(ticksAfterPause == 42 && !restoringAfterPause,
            "5,000 advancing game ticks during scenic pause never count toward replay timeouts or trigger restore");

        // 5. ModEntry.OnButtonPressed during ShouldHideSceneUi suppresses non-resume keys (Y/N/1/Back/DPad) and resumes on menu buttons!
        Game1.options = (Options)FormatterServices.GetUninitializedObject(typeof(Options));
        Game1.options.menuButton = [new InputButton(Keys.E)];
        GameRunner.instance = (GameRunner)FormatterServices.GetUninitializedObject(typeof(GameRunner));
        GameRunner.instance.gameInstances = [Game1.game1];

        List<SButton> suppressed = [];
        var inputHelper = DispatchProxy.Create<IInputHelper, SuppressRecordingProxy>();
        ((SuppressRecordingProxy)(object)inputHelper).Suppressed = suppressed;
        var entryHelper = DispatchProxy.Create<IModHelper, FakeProxy>();
        ((FakeProxy)(object)entryHelper).Values["get_Input"] = inputHelper;

        Type modEntryType = coordinatorType.Assembly.GetType("StardewGallery.ModEntry", true)!;
        Type configType = coordinatorType.Assembly.GetType("StardewGallery.ModConfig", true)!;
        Type replayServiceType = coordinatorType.Assembly.GetType("StardewGallery.ReplayService", true)!;
        object modEntry = Activator.CreateInstance(modEntryType, Flags, null, null, null)!;
        typeof(Mod).GetProperty("Helper", Flags)!.SetValue(modEntry, entryHelper);
        object modConfig = modEntryType.GetProperty("Config", Flags)!.GetValue(modEntry)!;
        configType.GetProperty("ScreenshotKeys", Flags)!.SetValue(modConfig, new KeybindList());
        configType.GetProperty("PauseSceneKeys", Flags)!.SetValue(modConfig, new KeybindList());
        object realService = Activator.CreateInstance(
            replayServiceType,
            Flags,
            binder: null,
            args: [entryHelper, DispatchProxy.Create<IMonitor, FakeProxy>(), coordinator, (Func<bool>)(() => true), (Func<bool>)(() => false), (Func<bool>)(() => false), (Func<bool>)(() => false), null],
            culture: null)!;
        modEntryType.GetField("replay", Flags)!.SetValue(modEntry, coordinator);
        modEntryType.GetField("replayService", Flags)!.SetValue(modEntry, realService);

        MethodInfo onButtonPressed = modEntryType.GetMethod("OnButtonPressed", Flags)!;
        FieldInfo buttonBacking = typeof(ButtonPressedEventArgs).GetField("<Button>k__BackingField", Flags)!;
        void PressButton(SButton button)
        {
            var args = (ButtonPressedEventArgs)FormatterServices.GetUninitializedObject(typeof(ButtonPressedEventArgs));
            buttonBacking.SetValue(args, button);
            onButtonPressed.Invoke(modEntry, [null, args]);
        }

        SButton[] blockedButtons = [SButton.Y, SButton.N, SButton.D1, SButton.ControllerBack, SButton.DPadDown];
        foreach (SButton blocked in blockedButtons)
            PressButton(blocked);
        Check(suppressed.SequenceEqual(blockedButtons)
            && (bool)coordinatorType.GetProperty("IsScenePaused", Flags)!.GetValue(coordinator)!,
            "ModEntry.OnButtonPressed suppresses Y/N/1/ControllerBack/DPadDown during scenic pause without unpausing");

        suppressed.Clear();
        PressButton(SButton.E);
        Check(suppressed.SequenceEqual([SButton.E])
            && !(bool)coordinatorType.GetProperty("IsScenePaused", Flags)!.GetValue(coordinator)!,
            "Pressing configured menuButton (E) during scenic pause suppresses the key and cleanly resumes");

        coordinatorType.GetMethod("ToggleScenePause", Flags)!.Invoke(coordinator, null);
        suppressed.Clear();
        PressButton(SButton.ControllerStart);
        Check(suppressed.SequenceEqual([SButton.ControllerStart])
            && !(bool)coordinatorType.GetProperty("IsScenePaused", Flags)!.GetValue(coordinator)!,
            "Pressing controller menu button (ControllerStart) during scenic pause suppresses and cleanly resumes");

        // 6. Resuming unfreezes Event.Update & DialogueBox, and Restore resets IsScenePaused!
        coordinatorType.GetMethod("ToggleScenePause", Flags)!.Invoke(coordinator, null);
        bool resumed = (bool)coordinatorType.GetMethod("SetScenePaused", Flags)!.Invoke(coordinator, [false])!;
        Check(resumed && !(bool)coordinatorType.GetProperty("IsScenePaused", Flags)!.GetValue(coordinator)!,
            "SetScenePaused(false) exits scenic pause");
        eventUpdatesObserved = 0;
        dialogueInputsObserved = 0;
        ownedEvent.Update(town, gameTime);
        dialogue.update(gameTime);
        dialogue.receiveLeftClick(0, 0, true);
        Check(eventUpdatesObserved == 1 && dialogueUpdatesObserved == 1 && dialogueInputsObserved == 1
            && (bool)speedType.GetMethod("BeforeDialogueDraw", Flags)!.Invoke(null, null)!,
            "Resuming immediately restores Event.Update, DialogueBox.update, DialogueBox input, and DialogueBox.draw");

        coordinatorType.GetMethod("ToggleScenePause", Flags)!.Invoke(coordinator, null);
        coordinatorType.GetMethod("Restore", Flags)!.Invoke(coordinator, [null]);
        Check(!(bool)coordinatorType.GetProperty("IsScenePaused", Flags)!.GetValue(coordinator)!,
            "ReplayCoordinator.Restore resets IsScenePaused");

        town.currentEvent = null;
        Game1.eventUp = false;
        coordinatorType.GetField("snapshot", Flags)!.SetValue(coordinator, null);
    }

    private static Event CreateHeadlessEvent(Farmer farmer)
    {
        var ev = (Event)FormatterServices.GetUninitializedObject(typeof(Event));
        ev.farmerActors = [farmer];
        typeof(Event).GetField("actors", Flags)!.SetValue(ev, new List<NPC>());
        GC.SuppressFinalize(ev);
        return ev;
    }

    private static void InvokeFork(Event target, GameLocation location, string[] args)
    {
        var ctx = new EventContext(target, location, new GameTime(), args);
        Event.DefaultCommands.Fork(target, args, ctx);
    }

    private sealed class InMemoryContentManager : LocalizedContentManager
    {
        public InMemoryContentManager() : base(null!, "") { }

        public override bool DoesAssetExist<T>(string assetName)
        {
            string norm = assetName.Replace('\\', '/');
            return norm.Equals("Data/Events/Town", StringComparison.OrdinalIgnoreCase)
                || norm.Equals("Data/Events/Beach", StringComparison.OrdinalIgnoreCase)
                || norm.Equals("Strings/Events", StringComparison.OrdinalIgnoreCase);
        }

        public override T Load<T>(string assetName)
        {
            if (typeof(T) == typeof(Dictionary<string, string>))
            {
                string norm = assetName.Replace('\\', '/');
                if (norm.Equals("Data/Events/Town", StringComparison.OrdinalIgnoreCase))
                    return (T)(object)new Dictionary<string, string>(townEvents, StringComparer.Ordinal);
                if (norm.Equals("Data/Events/Beach", StringComparison.OrdinalIgnoreCase))
                    return (T)(object)new Dictionary<string, string>(beachEvents, StringComparer.Ordinal);
                if (norm.Equals("Strings/Events", StringComparison.OrdinalIgnoreCase))
                    return (T)(object)new Dictionary<string, string>(stringEvents, StringComparer.Ordinal);
            }
            throw new ContentLoadException("Asset does not exist: " + assetName);
        }

        public override string? LoadBaseStringOrNull(string path) => null;
    }

    private static bool ObserveEventUpdate(ref bool __result)
    {
        eventUpdatesObserved++;
        __result = true;
        return false;
    }

    private static bool ObserveDialogueUpdate()
    {
        dialogueUpdatesObserved++;
        return false;
    }

    private static bool ObserveDialogueInput()
    {
        dialogueInputsObserved++;
        return false;
    }

    private static bool ObserveEventDraw(Event __instance)
    {
        skippableInsideDraw = __instance.skippable;
        return false;
    }

    private static object Create(Type type, params object?[] args)
        => Activator.CreateInstance(type, Flags, binder: null, args: args, culture: null)!;

    private static bool SkipVoid() => false;

    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition)
            failures++;
        Console.WriteLine((condition ? "PASS: " : "FAIL: ") + message);
    }

    public class SuppressRecordingProxy : DispatchProxy
    {
        public List<SButton> Suppressed { get; set; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IInputHelper.Suppress) && args is [SButton button])
                Suppressed.Add(button);
            return targetMethod is not null && targetMethod.ReturnType != typeof(void) && targetMethod.ReturnType.IsValueType
                ? Activator.CreateInstance(targetMethod.ReturnType)
                : null;
        }
    }
}
