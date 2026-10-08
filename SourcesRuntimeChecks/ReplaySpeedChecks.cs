using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

/// <summary>Exercises the built speed patches through real Event.Update and native recursive commands.</summary>
internal static class ReplaySpeedChecks
{
    private static readonly List<GameTime> eventTimes = new();
    private static GameTime? dialogueTime;
    private static readonly InvalidOperationException expectedFailure = new("Expected nested update failure.");
    private static bool throwInNestedUpdate;
    private static bool throwInLaterFinalizer;
    private static bool skipNestedUpdate;
    private static int assertions;
    private static int failures;

    internal static void Run(Assembly mod)
    {
        Type coordinatorType = mod.GetType("StardewGallery.ReplayCoordinator", true)!;
        Type patchesType = mod.GetType("StardewGallery.ReplaySpeedPatches", true)!;
        object coordinator = FormatterServices.GetUninitializedObject(coordinatorType);
        const string modId = "Gallery.ReplaySpeedChecks";
        var fixture = new Harmony(modId + ".Fixture");
        var production = new Harmony(modId);
        var input = new GameTime(TimeSpan.FromSeconds(9), TimeSpan.FromMilliseconds(16), true);
        Console.WriteLine($"Replay speed: built mod {mod.GetName().Version}; game {typeof(Event).Assembly.GetName().Version}; Harmony {typeof(Harmony).Assembly.GetName().Version}.");
        try
        {
            // The headless fixture skips actor/graphics updates, while Event.Update, command
            // dispatch, native jump/showFrame recursion and the production patches remain real.
            fixture.Patch(AccessTools.Method(typeof(Event), "UpdateBeforeNextCommand"),
                prefix: new HarmonyMethod(typeof(ReplaySpeedChecks), nameof(ObserveEventTime)));
            fixture.Patch(AccessTools.Method(typeof(EventContext), "LogErrorAndSkip"), prefix: new HarmonyMethod(typeof(ReplaySpeedChecks), nameof(RejectCommandError)));
            fixture.Patch(AccessTools.Method(typeof(Event), nameof(Event.Update)),
                prefix: new HarmonyMethod(typeof(ReplaySpeedChecks), nameof(SkipNestedPrefix)) { priority = Priority.First });
            fixture.Patch(AccessTools.Method(typeof(Event), nameof(Event.Update)),
                prefix: new HarmonyMethod(typeof(ReplaySpeedChecks), nameof(ThrowFromNestedPrefix)) { priority = Priority.Last });
            // Let failures escape the native event error UI in this headless process.
            fixture.Patch(AccessTools.Method(typeof(Event), nameof(Event.LogErrorAndHalt), new[] { typeof(Exception) }),
                prefix: new HarmonyMethod(typeof(ReplaySpeedChecks), nameof(RethrowEventError)));
            fixture.Patch(AccessTools.Method(typeof(DialogueBox), nameof(DialogueBox.update)),
                prefix: new HarmonyMethod(typeof(ReplaySpeedChecks), nameof(ObserveDialogueTime)) { priority = Priority.Last });

            Game1.game1 = (Game1)FormatterServices.GetUninitializedObject(typeof(Game1));
            FieldInfo screenFade = AccessTools.Field(typeof(Game1), "screenFade");
            screenFade.SetValue(null, FormatterServices.GetUninitializedObject(screenFade.FieldType));
            Game1.currentLocation = new GameLocation();
            // Event construction creates a festival content manager; no content is needed here.
            var scene = (Event)FormatterServices.GetUninitializedObject(typeof(Event));
            scene.farmerActors = new List<Farmer>();
            GC.SuppressFinalize(scene);
            Game1.currentLocation.currentEvent = scene;
            SetCoordinator("snapshot", FormatterServices.GetUninitializedObject(AccessTools.Field(coordinatorType, "snapshot").FieldType));
            SetCoordinator("observed", true);
            SetCoordinator("activeReplayEvent", scene);
            AccessTools.Method(typeof(Event), "SetupEventCommandsIfNeeded").Invoke(null, null);
            if (!Event.TryGetEventCommandHandler("jump", out _) || !Event.TryGetEventCommandHandler("showFrame", out _))
                throw new InvalidOperationException("Native recursive command registration is missing from the fixture.");
            var registry = DispatchProxy.Create<IModRegistry, AssemblyLoadProbeProxy>();
            ((AssemblyLoadProbeProxy)(object)registry).Values["get_ModID"] = modId;
            var helper = DispatchProxy.Create<IModHelper, AssemblyLoadProbeProxy>();
            ((AssemblyLoadProbeProxy)(object)helper).Values["get_ModRegistry"] = registry;
            AccessTools.Method(patchesType, "Apply").Invoke(null, new[] { helper, coordinator });

            foreach ((int multiplier, int expectedMs) in new[] { (1, 16), (2, 32), (4, 64) })
            {
                SetCoordinator("speedMultiplier", multiplier);
                RunFrame();
                CheckTimes(expectedMs, $"{multiplier}x native jump/showFrame recursion scales elapsed time once");
                Check(scene.CurrentCommand == 3, $"{multiplier}x native recursive commands both advance exactly once");
                Check(eventTimes.All(time => time.TotalGameTime == input.TotalGameTime && time.IsRunningSlowly)
                    && input.ElapsedGameTime.TotalMilliseconds == 16, $"{multiplier}x retains total time, slow flag and caller input");
                RunFrame();
                CheckTimes(expectedMs, $"{multiplier}x next outer frame still scales once");
            }

            SetCoordinator("speedMultiplier", 4);
            throwInNestedUpdate = true;
            Exception? escaped = null;
            try { RunFrame(); }
            catch (Exception error) { escaped = error; }
            finally { throwInNestedUpdate = false; }
            Check(ReferenceEquals(escaped, expectedFailure), "A nested Harmony prefix failure escapes without being suppressed");
            RunFrame();
            CheckTimes(64, "Nested failure releases all update scopes before the next frame");

            skipNestedUpdate = true;
            try { RunFrame(); }
            finally { skipNestedUpdate = false; }
            Check(eventTimes.Count == 1 && eventTimes[0].ElapsedGameTime.TotalMilliseconds == 64 && scene.CurrentCommand == 2,
                "An earlier prefix can skip the nested update without advancing its native command");
            RunFrame();
            CheckTimes(64, "A skipped production prefix does not release another update scope");

            // Harmony retries previously run finalizers if a later finalizer fails.
            fixture.Patch(AccessTools.Method(typeof(Event), nameof(Event.Update)),
                finalizer: new HarmonyMethod(typeof(ReplaySpeedChecks), nameof(ThrowFromLaterFinalizer)) { priority = Priority.Last });
            throwInLaterFinalizer = true;
            escaped = null;
            try { RunFrame(); }
            catch (Exception error) { escaped = error; }
            finally { throwInLaterFinalizer = false; }
            Check(ReferenceEquals(escaped, expectedFailure), "A later finalizer failure escapes the update");
            Check((int)AccessTools.Field(patchesType, "eventUpdateDepth").GetValue(null)! == 0,
                "A replayed finalizer releases its update scope only once");
            RunFrame();
            CheckTimes(64, "A later finalizer failure preserves next-frame acceleration");

            var dialogue = (DialogueBox)FormatterServices.GetUninitializedObject(typeof(DialogueBox));
            dialogue.responses = Array.Empty<Response>();
            AccessTools.Field(typeof(Game1), "_activeClickableMenu").SetValue(null, dialogue);
            RunFrame();
            CheckTimes(64, "Ordinary dialogue retains 4x event speed");
            CheckDialogue(64, "Ordinary dialogue text update retains 4x speed");
            dialogue.isQuestion = true;
            RunFrame();
            CheckTimes(16, "A question pauses accelerated event updates");
            CheckDialogue(16, "Question dialogue updates remain 1x");
            dialogue.isQuestion = false;
            dialogue.responses = new[] { new Response("yes", "Yes") };
            RunFrame();
            CheckTimes(16, "Responses pause accelerated event updates even without the question flag");
            CheckDialogue(16, "Response dialogue updates remain 1x");
            dialogue.responses = Array.Empty<Response>();
            AccessTools.Field(typeof(Game1), "_activeClickableMenu").SetValue(null, null);

            foreach (string transition in new[] { "fadeToBlackAlpha", "globalFade", "nonWarpFade", "locationRequest" })
            {
                PropertyInfo? property = AccessTools.Property(typeof(Game1), transition);
                FieldInfo? field = AccessTools.Field(typeof(Game1), transition);
                object? previous = property is not null ? property.GetValue(null) : field!.GetValue(null);
                Action<object?> set = value => { if (property is not null) property.SetValue(null, value); else field!.SetValue(null, value); };
                object blocking = transition == "fadeToBlackAlpha" ? 0.5f
                    : transition == "locationRequest" ? FormatterServices.GetUninitializedObject(property?.PropertyType ?? field!.FieldType) : true;
                set(blocking);
                RunFrame();
                CheckTimes(16, transition + " pauses accelerated event updates");
                AccessTools.Field(typeof(Game1), "_activeClickableMenu").SetValue(null, dialogue);
                RunFrame();
                CheckTimes(64, transition + " preserves the existing ordinary-dialogue exception");
                AccessTools.Field(typeof(Game1), "_activeClickableMenu").SetValue(null, null);
                set(previous);
            }

            SetCoordinator("restoring", true);
            RunFrame();
            CheckTimes(16, "Restoration keeps event updates at 1x");
            SetCoordinator("restoring", false);
            SetCoordinator("observed", false);
            RunFrame();
            CheckTimes(16, "An unobserved replay keeps event updates at 1x");
            SetCoordinator("observed", true);
            SetCoordinator("snapshot", null);
            RunFrame();
            CheckTimes(16, "Inactive replay keeps native recursive event updates at 1x");

            void RunFrame()
            {
                eventTimes.Clear();
                scene.ReplaceAllCommands("unused", "jump farmer", "showFrame farmer 0", "pause 1000");
                scene.CurrentCommand = 1;
                scene.Update(Game1.currentLocation, input);
            }
            void CheckDialogue(int expectedMs, string message)
            {
                dialogueTime = null;
                dialogue.update(input);
                Check(dialogueTime?.ElapsedGameTime.TotalMilliseconds == expectedMs, message);
            }
        }
        finally
        {
            production.UnpatchAll(production.Id);
            fixture.UnpatchAll(fixture.Id);
        }
        if (failures != 0) throw new InvalidOperationException($"{failures} of {assertions} replay speed checks failed.");
        Console.WriteLine($"All {assertions} replay speed checks passed. No game launch, Mod.Entry, save access or installation.");

        void SetCoordinator(string name, object? value) => AccessTools.Field(coordinatorType, name).SetValue(coordinator, value);
    }

    private static bool ObserveEventTime(GameTime time, ref bool __result)
    {
        eventTimes.Add(time);
        __result = true;
        return false;
    }

    private static bool SkipNestedPrefix(Event __instance) => !skipNestedUpdate || __instance.CurrentCommand != 2;

    private static void ThrowFromNestedPrefix(Event __instance)
    {
        if (throwInNestedUpdate && __instance.CurrentCommand == 2)
            throw expectedFailure;
    }

    private static void ThrowFromLaterFinalizer()
    {
        if (!throwInLaterFinalizer)
            return;
        throwInLaterFinalizer = false;
        throw expectedFailure;
    }

    private static void RethrowEventError(Exception e) => throw e;
    private static void RejectCommandError(string error) => throw new InvalidOperationException("Unexpected native command error: " + error);

    private static bool ObserveDialogueTime(GameTime time)
    {
        dialogueTime = time;
        return false;
    }

    private static void CheckTimes(int expectedMs, string message)
    {
        double[] actual = eventTimes.Select(time => time.ElapsedGameTime.TotalMilliseconds).ToArray();
        Check(actual.Length == 3 && actual.All(ms => ms == expectedMs), $"{message}: [{string.Join(", ", actual)}] ms, expected [{expectedMs}, {expectedMs}, {expectedMs}]");
    }

    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition) failures++;
        Console.WriteLine((condition ? "PASS: " : "FAIL: ") + message);
    }
}
