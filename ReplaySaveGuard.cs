using System.Collections;
using System.Reflection;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;

namespace StardewGallery;

internal static class ReplaySaveGuard
{
    private static ReplayCoordinator replay = null!;
    private static IMonitor monitor = null!;
    private static ITranslationHelper i18n = null!;
    private static readonly HashSet<string> warnedCommands = new(StringComparer.OrdinalIgnoreCase);
    internal static bool IsReady { get; private set; }

    internal static void Apply(IModHelper helper, IMonitor log, ReplayCoordinator coordinator)
    {
        replay = coordinator;
        monitor = log;
        i18n = helper.Translation;
        IsReady = false;
        Harmony harmony = new(helper.ModRegistry.ModID + ".ReplayProtection");
        void Patch(Type type, string method, string prefix, Type[]? parameters = null)
        {
            MethodInfo target = AccessTools.Method(type, method, parameters)
                ?? throw new MissingMethodException(type.FullName, method);
            harmony.Patch(target, prefix: new HarmonyMethod(typeof(ReplaySaveGuard), prefix));
        }
        try
        {
            Patch(typeof(SaveGame), "getSaveEnumerator", nameof(BeforeSave));
            Patch(typeof(Farmer), nameof(Farmer.NotifyQuests), nameof(BeforeNotifyQuests));
            Patch(typeof(StardewValley.Quests.Quest), "questComplete", nameof(BeforeMutation));
            Patch(typeof(Farmer), "set_Money", nameof(BeforeMoney));
            Patch(typeof(Farmer), "set_totalMoneyEarned", nameof(BeforeTotalMoney));
            Patch(typeof(Game1), "getAchievement", nameof(BeforeMutation));
            Patch(typeof(Stats), "checkForMoneyAchievements", nameof(BeforeMutation));
            Patch(typeof(Stats), "set_IndividualMoneyEarned", nameof(BeforeMutation));
            Patch(typeof(Farmer), "changeFriendship", nameof(BeforeMutation));
            Patch(typeof(Event), nameof(Event.endBehaviors), nameof(BeforeEnd), [typeof(string[]), typeof(GameLocation)]);
            Patch(typeof(Event), nameof(Event.skipEvent), nameof(BeforeSkip));
            Patch(typeof(Event), nameof(Event.exitEvent), nameof(BeforeExit));
            Patch(typeof(Event), nameof(Event.tryEventCommand), nameof(BeforeCommand));
            Patch(typeof(Event.DefaultCommands), nameof(Event.DefaultCommands.ChangePortrait), nameof(BeforePortrait));
            harmony.Patch(AccessTools.Method(typeof(Event.DefaultCommands), nameof(Event.DefaultCommands.AddConversationTopic)),
                prefix: new HarmonyMethod(typeof(ReplaySaveGuard), nameof(BeforeTopic)),
                finalizer: new HarmonyMethod(typeof(ReplaySaveGuard), nameof(AfterTopic)));
            foreach (string command in ReplayEffectPolicy.SuppressedNativeHandlers)
                Patch(typeof(Event.DefaultCommands), command, nameof(BeforeEffectCommand));
            IsReady = true;
        }
        catch
        {
            harmony.UnpatchAll(harmony.Id);
            throw;
        }
    }

    private static bool BeforeMutation() => !replay.IsActive;

    private static bool BeforeNotifyQuests(ref bool __result)
    {
        if (!replay.IsActive) return true;
        __result = false;
        return false;
    }

    private static bool BeforeMoney(Farmer __instance, int value)
    {
        if (!replay.IsActive || !ReferenceEquals(__instance, Game1.player)) return true;
        __instance._money = value;
        return false;
    }

    private static bool BeforeTotalMoney(Farmer __instance, uint value)
    {
        if (!replay.IsActive) return true;
        // Direct income setters can award external achievements before a snapshot can roll back.
        return false;
    }

    private static bool BeforeEffectCommand(Event __0)
    {
        if (!replay.OwnsEvent(__0)) return true;
        __0.CurrentCommand++;
        return false;
    }

    private static void BeforeTopic(Event __0, out bool? __state)
    {
        __state = replay.OwnsEvent(__0) ? __0.isMemory : null;
        // Keep native temporary-topic semantics; the enclosing event remains a memory.
        if (__state.HasValue) __0.isMemory = false;
    }

    private static void AfterTopic(Event __0, bool? __state)
    {
        if (__state.HasValue) __0.isMemory = __state.Value;
    }

    private static bool BeforeEnd(Event __instance)
    {
        if (!replay.OwnsEvent(__instance)) return true;
        __instance.exitEvent();
        return false;
    }

    private static bool BeforeSkip(Event __instance)
    {
        if (!replay.OwnsEvent(__instance)) return true;
        // Native skip executes reward actions and ID-specific world mutations.
        if (__instance.playerControlSequence)
            __instance.EndPlayerControlSequence();
        foreach (NPC actor in __instance.actors) actor.Halt();
        __instance.farmer.Halt();
        Game1.activeClickableMenu = null;
        Game1.dialogueUp = false;
        Game1.dialogueTyping = false;
        Game1.pauseTime = 0;
        __instance.exitEvent();
        return false;
    }

    private static void BeforeExit(Event __instance)
    {
        if (!replay.OwnsEvent(__instance)) return;
        __instance.markEventSeen = false;
        __instance.isFestival = false;
        __instance.onEventFinished = null;
        __instance.exitLocation = null;
        // This native exit ID unconditionally spawns Leo, even for isMemory events.
        if (__instance.id == "1039573") __instance.id = "-1";
    }

    private static bool BeforePortrait(Event __0, string[] __1)
    {
        if (!replay.OwnsEvent(__0) || __1.Length < 2 || __0.getActorByName(__1[1]) is not null) return true;
        // The native fallback would mutate a world NPC outside the event cast.
        __0.CurrentCommand++;
        return false;
    }

    private static void BeforeCommand(Event __instance, string[] __2)
    {
        if (!replay.OwnsEvent(__instance) || __2.Length == 0) return;
        string command = __2[0];
        if (Event.TryGetEventCommandHandler(command, out var handler)
            && handler.GetInvocationList().All(value => value.Method.DeclaringType == typeof(Event.DefaultCommands))) return;
        if (warnedCommands.Add(command))
            monitor.Log($"Replay executes extension command '{command}'. Native state protection cannot roll back third-party files, callbacks, or private state.", LogLevel.Warn);
    }

    private static bool BeforeSave(ref IEnumerator<int> __result)
    {
        if (!replay.IsActive)
            return true;
        monitor.Log("已阻止回放期间的存档请求。", LogLevel.Warn);
        Game1.addHUDMessage(new HUDMessage(i18n.Get("replay.save-blocked"), HUDMessage.error_type));
        __result = Enumerable.Empty<int>().GetEnumerator();
        return false;
    }
}
