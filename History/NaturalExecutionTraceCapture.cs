using StardewValley;

namespace StardewGallery;

internal sealed record EventCommandObserverState(
    CommandDispatchObservation Observation,
    string[] BeforeCommandArray
);

internal sealed record EventAnswerObserverState(
    AnswerDialogueObservation Observation,
    string[] BeforeCommandArray
);

internal sealed class NaturalExecutionTraceCapture(string modVersion)
{
    private sealed class Session(Event @event, NaturalExecutionTraceBuilder builder)
    {
        internal Event Event { get; } = @event;
        internal NaturalExecutionTraceBuilder Builder { get; } = builder;
        internal CommandDispatchObservation? ActiveCommand { get; set; }
    }

    private Session? active;

    internal bool Enabled { get; private set; }

    internal void Enable() => Enabled = true;

    internal void Start(Event @event, WatchedEventSnapshot snapshot)
    {
        if (!Enabled)
            return;
        if (active is not null && !ReferenceEquals(active.Event, @event))
            active = null;
        if (active is not null)
            return;

        ObservedVariantKey key = new(
            snapshot.Identity,
            EventHashes.RootDefinition(snapshot.EventKey, snapshot.RootScript),
            snapshot.Fingerprint);
        NaturalExecutionTraceBuilder builder = new(
            snapshot.Identity,
            snapshot.EventKey,
            key.RootDefinitionHash,
            key.PlaybackHash,
            @event.eventCommands,
            snapshot.Locale,
            typeof(Game1).Assembly.GetName().Version?.ToString() ?? "unknown",
            modVersion);
        active = new Session(@event, builder);
    }

    internal EventCommandObserverState? BeforeCommand(
        Event @event,
        string[] arguments,
        string commandName,
        string handlerProvenance,
        bool handlerResolved,
        bool handlerIsNative,
        bool nativeFork,
        bool nativeSwitchEvent,
        string locationName,
        int previousAnswer)
    {
        Session? session = active;
        if (session is null || !ReferenceEquals(session.Event, @event))
            return null;
        string command = @event.GetCurrentCommand() ?? "";
        CommandDispatchObservation? observation = session.Builder.BeginCommand(
            command,
            @event.CurrentCommand,
            arguments,
            commandName,
            handlerProvenance,
            handlerResolved,
            handlerIsNative,
            nativeFork,
            nativeSwitchEvent,
            @event.isFestival,
            locationName,
            @event.specialEventVariable1,
            previousAnswer,
            @event.eventCommands);
        session.ActiveCommand = observation;
        return observation is null ? null : new EventCommandObserverState(observation, @event.eventCommands);
    }

    internal void ObserveReplacement(Event @event, IReadOnlyList<string> commands)
    {
        Session? session = active;
        if (session is null || !ReferenceEquals(session.Event, @event))
            return;
        if (session.ActiveCommand is CommandDispatchObservation observation)
            session.Builder.ObserveReplacement(observation, commands);
        else
            session.Builder.MarkObserverFailure("unattributed-command-replacement");
    }

    internal void AfterCommand(Event @event, EventCommandObserverState? state, int previousAnswer)
    {
        Session? session = active;
        if (state is null || session is null || !ReferenceEquals(session.Event, @event))
            return;
        session.Builder.EndCommand(
            state.Observation,
            @event.CurrentCommand,
            @event.eventCommands,
            !ReferenceEquals(state.BeforeCommandArray, @event.eventCommands),
            @event.specialEventVariable1,
            previousAnswer);
        session.ActiveCommand = null;
    }

    internal void AbandonCommand(Event @event, EventCommandObserverState? state, string detailCode)
    {
        Session? session = active;
        if (session is null || !ReferenceEquals(session.Event, @event))
            return;
        session.Builder.AbandonCommand(state?.Observation, detailCode);
        session.ActiveCommand = null;
    }

    internal EventAnswerObserverState? BeforeAnswer(
        Event @event,
        string questionKey,
        int selectedIndex,
        int previousAnswer)
    {
        Session? session = active;
        if (session is null || !ReferenceEquals(session.Event, @event))
            return null;
        string command = @event.GetCurrentCommand() ?? "";
        AnswerDialogueObservation? observation = session.Builder.BeginAnswer(
            questionKey,
            selectedIndex,
            command,
            @event.CurrentCommand,
            @event.eventCommands,
            @event.specialEventVariable1,
            previousAnswer);
        return observation is null ? null : new EventAnswerObserverState(observation, @event.eventCommands);
    }

    internal void AfterAnswer(Event @event, EventAnswerObserverState? state, int previousAnswer)
    {
        Session? session = active;
        if (state is null || session is null || !ReferenceEquals(session.Event, @event))
            return;
        session.Builder.EndAnswer(
            state.Observation,
            @event.eventCommands,
            @event.specialEventVariable1,
            previousAnswer);
    }

    internal void MarkObserverFailure(Event @event, string detailCode)
    {
        if (active is Session session && ReferenceEquals(session.Event, @event))
            session.Builder.MarkObserverFailure(detailCode);
    }

    internal NaturalExecutionTraceResult? Finish(Event? @event, ExecutionTraceEndReason endReason)
    {
        Session? session = active;
        active = null;
        if (session is null || @event is null || !ReferenceEquals(session.Event, @event))
            return null;
        return session.Builder.Finish(endReason);
    }

    internal void Clear() => active = null;
}
