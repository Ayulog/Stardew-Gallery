using System.Reflection;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Delegates;

namespace StardewGallery;

internal static class ExecutionTraceObserver
{
    private static WatchedEventHistory history = null!;
    private static Func<bool> replayActive = null!;
    private static FieldInfo previousAnswerField = null!;
    private static bool enabled;

    internal static void Apply(IModHelper helper, WatchedEventHistory watchedHistory, Func<bool> isReplayActive)
    {
        history = watchedHistory;
        replayActive = isReplayActive;
        previousAnswerField = AccessTools.Field(typeof(Event), "previousAnswerChoice")
            ?? throw new MissingFieldException(typeof(Event).FullName, "previousAnswerChoice");

        MethodInfo dispatch = AccessTools.Method(typeof(Event), nameof(Event.tryEventCommand))
            ?? throw new MissingMethodException(typeof(Event).FullName, nameof(Event.tryEventCommand));
        MethodInfo replace = AccessTools.Method(typeof(Event), nameof(Event.ReplaceAllCommands))
            ?? throw new MissingMethodException(typeof(Event).FullName, nameof(Event.ReplaceAllCommands));
        MethodInfo answer = AccessTools.Method(typeof(Event), nameof(Event.answerDialogue))
            ?? throw new MissingMethodException(typeof(Event).FullName, nameof(Event.answerDialogue));
        MethodInfo exit = AccessTools.Method(typeof(Event), nameof(Event.exitEvent))
            ?? throw new MissingMethodException(typeof(Event).FullName, nameof(Event.exitEvent));

        Harmony harmony = new(helper.ModRegistry.ModID);
        harmony.Patch(dispatch,
            prefix: new HarmonyMethod(typeof(ExecutionTraceObserver), nameof(BeforeCommand)),
            postfix: new HarmonyMethod(typeof(ExecutionTraceObserver), nameof(AfterCommand)),
            finalizer: new HarmonyMethod(typeof(ExecutionTraceObserver), nameof(FinalizeCommand)));
        harmony.Patch(replace,
            prefix: new HarmonyMethod(typeof(ExecutionTraceObserver), nameof(BeforeReplaceAllCommands)));
        harmony.Patch(answer,
            prefix: new HarmonyMethod(typeof(ExecutionTraceObserver), nameof(BeforeAnswer)),
            postfix: new HarmonyMethod(typeof(ExecutionTraceObserver), nameof(AfterAnswer)),
            finalizer: new HarmonyMethod(typeof(ExecutionTraceObserver), nameof(FinalizeAnswer)));
        harmony.Patch(exit,
            postfix: new HarmonyMethod(typeof(ExecutionTraceObserver), nameof(AfterExitEvent)));

        string owner = helper.ModRegistry.ModID;
        if (!HasPatch(dispatch, owner) || !HasPatch(replace, owner) || !HasPatch(answer, owner) || !HasPatch(exit, owner))
            throw new InvalidOperationException("Execution trace Harmony patch verification failed.");
        enabled = true;
        watchedHistory.EnableExecutionTraceCapture();
    }

    private static void BeforeCommand(Event __instance, GameLocation location, string[] args,
        out EventCommandObserverState? __state)
    {
        __state = null;
        if (!enabled || !NaturalExecutionTraceRules.CanObserve(replayActive(), ReferenceEquals(__instance, Game1.CurrentEvent)))
            return;
        try
        {
            string inputName = args.Length > 0 ? args[0] : "";
            bool resolvedName = Event.TryResolveCommandName(inputName, out string commandName);
            bool resolvedHandler = Event.TryGetEventCommandHandler(inputName, out EventCommandDelegate handler);
            if (!resolvedName)
                commandName = inputName;
            MethodInfo? method = resolvedHandler ? handler.Method : null;
            bool nativeHandler = method?.DeclaringType == typeof(Event.DefaultCommands)
                && method.Module.Assembly == typeof(Event).Assembly;
            string provenance = method is null
                ? "missing"
                : $"{method.Module.Assembly.GetName().Name}:{method.DeclaringType?.FullName}.{method.Name}";
            __state = history.BeforeEventCommand(
                __instance,
                args,
                commandName,
                provenance,
                resolvedHandler,
                nativeHandler,
                nativeHandler && commandName.Equals(nameof(Event.DefaultCommands.Fork), StringComparison.OrdinalIgnoreCase),
                nativeHandler && commandName.Equals(nameof(Event.DefaultCommands.SwitchEvent), StringComparison.OrdinalIgnoreCase),
                location.Name,
                ReadPreviousAnswer(__instance));
        }
        catch
        {
            TryMarkFailure(__instance, "command-prefix-failure");
        }
    }

    private static void AfterCommand(Event __instance, EventCommandObserverState? __state)
    {
        if (!enabled || __state is null)
            return;
        try
        {
            history.AfterEventCommand(__instance, __state, ReadPreviousAnswer(__instance));
        }
        catch
        {
            TryMarkFailure(__instance, "command-postfix-failure");
        }
    }

    private static Exception? FinalizeCommand(Event __instance, EventCommandObserverState? __state, Exception? __exception)
    {
        if (enabled && __exception is not null)
        {
            try
            {
                history.AbandonEventCommand(__instance, __state, "native-command-exception");
            }
            catch
            {
                TryMarkFailure(__instance, "command-finalizer-failure");
            }
        }
        return __exception;
    }

    private static void BeforeReplaceAllCommands(Event __instance, string[] commands)
    {
        if (!enabled || !NaturalExecutionTraceRules.CanObserve(replayActive(), ReferenceEquals(__instance, Game1.CurrentEvent)))
            return;
        try
        {
            history.ObserveCommandReplacement(__instance, commands);
        }
        catch
        {
            TryMarkFailure(__instance, "replacement-observer-failure");
        }
    }

    private static void BeforeAnswer(Event __instance, string questionKey, int answerChoice,
        out EventAnswerObserverState? __state)
    {
        __state = null;
        if (!enabled || !NaturalExecutionTraceRules.CanObserve(replayActive(), ReferenceEquals(__instance, Game1.CurrentEvent)))
            return;
        try
        {
            __state = history.BeforeEventAnswer(
                __instance,
                questionKey,
                answerChoice,
                ReadPreviousAnswer(__instance));
        }
        catch
        {
            TryMarkFailure(__instance, "answer-prefix-failure");
        }
    }

    private static void AfterAnswer(Event __instance, EventAnswerObserverState? __state)
    {
        if (!enabled || __state is null)
            return;
        try
        {
            history.AfterEventAnswer(__instance, __state, ReadPreviousAnswer(__instance));
        }
        catch
        {
            TryMarkFailure(__instance, "answer-postfix-failure");
        }
    }

    private static Exception? FinalizeAnswer(Event __instance, EventAnswerObserverState? __state, Exception? __exception)
    {
        if (enabled && __exception is not null)
            TryMarkFailure(__instance, "native-answer-exception");
        return __exception;
    }

    private static void AfterExitEvent(Event __instance)
    {
        if (!enabled || !NaturalExecutionTraceRules.CanObserve(replayActive(), ReferenceEquals(__instance, Game1.CurrentEvent)))
            return;
        try
        {
            history.ObserveNaturalEventExit(__instance, __instance.skipped);
        }
        catch
        {
            TryMarkFailure(__instance, "exit-observer-failure");
        }
    }

    private static int ReadPreviousAnswer(Event @event)
        => previousAnswerField.GetValue(@event) is int value ? value : -1;

    private static bool HasPatch(MethodInfo method, string owner)
    {
        Patches? patches = Harmony.GetPatchInfo(method);
        return patches is not null && (patches.Prefixes.Any(value => value.owner == owner)
            || patches.Postfixes.Any(value => value.owner == owner)
            || patches.Finalizers.Any(value => value.owner == owner));
    }

    private static void TryMarkFailure(Event @event, string detailCode)
    {
        try
        {
            history.MarkExecutionObserverFailure(@event, detailCode);
        }
        catch
        {
            // Capture must never affect the native Event engine.
        }
    }
}
