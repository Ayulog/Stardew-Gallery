namespace StardewGallery;

internal static class NaturalExecutionTraceRules
{
    internal static bool CanObserve(bool replayActive, bool isCurrentEvent)
        => !replayActive && isCurrentEvent;
}

internal sealed record ExecutionTraceDiagnosticEntry(
    int DiagnosticOrder,
    long? Sequence,
    string Kind,
    string SegmentPathHash,
    string CommandHash,
    int CommandOrdinal,
    int Occurrence,
    string CommandName,
    string HandlerProvenance,
    string? Target,
    string? Outcome,
    string? ReplacementSegmentPathHash,
    string? QuestionKey,
    int? SelectedIndex,
    bool? SpecialVariableBefore,
    bool? SpecialVariableAfter,
    int? PreviousAnswerBefore,
    int? PreviousAnswerAfter,
    string? DetailCode
);

internal sealed record NaturalExecutionTraceDiagnostic(
    string AssetName,
    string EventId,
    string RawEventKey,
    string RootDefinitionHash,
    string PlaybackHash,
    ScriptSegmentIdentity RootSegment,
    int CommandObserverCallbacks,
    int AnswerObserverCallbacks,
    bool DiagnosticsTruncated,
    IReadOnlyList<ExecutionTraceDiagnosticEntry> Entries,
    HistoricalExecutionContext Context,
    int ExecutionJsonBytes
);

internal sealed record NaturalExecutionTraceResult(
    HistoricalExecutionContext Context,
    NaturalExecutionTraceDiagnostic Diagnostic
);

internal sealed class CommandDispatchObservation
{
    internal ScriptSegmentIdentity Segment { get; }
    internal string CommandText { get; }
    internal string CommandHash { get; }
    internal int CommandOrdinal { get; }
    internal int Occurrence { get; }
    internal string[] Arguments { get; }
    internal string CommandName { get; }
    internal string HandlerProvenance { get; }
    internal bool HandlerResolved { get; }
    internal bool NativeFork { get; }
    internal bool NativeSwitchEvent { get; }
    internal bool IsFestival { get; }
    internal string LocationName { get; }
    internal bool SpecialVariableBefore { get; }
    internal int PreviousAnswerBefore { get; }
    internal int BeforeCommand { get; }
    internal string? BeforeCommandListHash { get; }
    internal IReadOnlyList<string>? ReplacementCommands { get; private set; }
    internal int ReplacementCount { get; private set; }

    internal CommandDispatchObservation(
        ScriptSegmentIdentity segment,
        string commandText,
        int commandOrdinal,
        int occurrence,
        string[] arguments,
        string commandName,
        string handlerProvenance,
        bool handlerResolved,
        bool nativeFork,
        bool nativeSwitchEvent,
        bool isFestival,
        string locationName,
        bool specialVariableBefore,
        int previousAnswerBefore,
        IReadOnlyList<string> beforeCommands,
        bool hashBeforeCommands)
    {
        Segment = segment;
        CommandText = commandText;
        CommandHash = HistoricalExecutionContextRules.HashCommand(commandText);
        CommandOrdinal = commandOrdinal;
        Occurrence = occurrence;
        Arguments = arguments.ToArray();
        CommandName = commandName;
        HandlerProvenance = handlerProvenance;
        HandlerResolved = handlerResolved;
        NativeFork = nativeFork;
        NativeSwitchEvent = nativeSwitchEvent;
        IsFestival = isFestival;
        LocationName = locationName;
        SpecialVariableBefore = specialVariableBefore;
        PreviousAnswerBefore = previousAnswerBefore;
        BeforeCommand = commandOrdinal;
        BeforeCommandListHash = hashBeforeCommands
            ? HistoricalExecutionContextRules.HashCommandList(beforeCommands)
            : null;
    }

    internal void ObserveReplacement(IReadOnlyList<string> commands)
    {
        ReplacementCount++;
        ReplacementCommands = commands.ToArray();
    }
}

internal sealed record AnswerDialogueObservation(
    ScriptSegmentIdentity Segment,
    string CommandText,
    string CommandHash,
    int CommandOrdinal,
    int Occurrence,
    string QuestionKey,
    int SelectedIndex,
    bool SpecialVariableBefore,
    int PreviousAnswerBefore,
    string BeforeCommandListHash
);

internal sealed class NaturalExecutionTraceBuilder
{
    private readonly EventIdentity identity;
    private readonly string rawEventKey;
    private readonly string rootDefinitionHash;
    private readonly string playbackHash;
    private readonly string locale;
    private readonly string gameVersion;
    private readonly string modVersion;
    private readonly Dictionary<string, int> occurrences = new(StringComparer.Ordinal);
    private readonly List<AutomaticDecision> automaticDecisions = [];
    private readonly List<ExecutionTraceIssue> issues = [];
    private readonly List<ExecutionTraceDiagnosticEntry> diagnostics = [];
    private int semanticEntries;
    private bool captureStopped;
    private ExecutionTraceEndReason? forcedEndReason;
    private ExecutionTraceCoverage automaticCoverage = ExecutionTraceCoverage.Complete;
    private OpaqueDecisionCoverage opaqueCoverage = OpaqueDecisionCoverage.NoneObserved;
    private CommandDispatchObservation? activeCommand;
    private bool finished;
    private int segmentDepth;

    internal ScriptSegmentIdentity RootSegment { get; }
    internal ScriptSegmentIdentity CurrentSegment { get; private set; }
    internal int CommandObserverCallbacks { get; private set; }
    internal int AnswerObserverCallbacks { get; private set; }
    internal bool DiagnosticsTruncated { get; private set; }

    internal NaturalExecutionTraceBuilder(
        EventIdentity identity,
        string rawEventKey,
        string rootDefinitionHash,
        string playbackHash,
        IReadOnlyList<string> rootCommands,
        string locale,
        string gameVersion,
        string modVersion)
    {
        this.identity = identity;
        this.rawEventKey = rawEventKey;
        this.rootDefinitionHash = rootDefinitionHash;
        this.playbackHash = playbackHash;
        this.locale = locale;
        this.gameVersion = gameVersion;
        this.modVersion = modVersion;
        string commandListHash = HistoricalExecutionContextRules.HashCommandList(rootCommands);
        RootSegment = new ScriptSegmentIdentity(
            ScriptSegmentKind.Root,
            HistoricalExecutionContextRules.HashRootPath(playbackHash, commandListHash),
            commandListHash,
            new ScriptSourceIdentity(ScriptSourceKind.RootPlayback, null, null),
            null);
        CurrentSegment = RootSegment;
    }

    internal CommandDispatchObservation? BeginCommand(
        string commandText,
        int commandOrdinal,
        string[] arguments,
        string commandName,
        string handlerProvenance,
        bool handlerResolved,
        bool handlerIsNative,
        bool nativeFork,
        bool nativeSwitchEvent,
        bool isFestival,
        string locationName,
        bool specialVariableBefore,
        int previousAnswerBefore,
        IReadOnlyList<string> beforeCommands)
    {
        CommandObserverCallbacks++;
        if (captureStopped || finished)
            return null;
        if (activeCommand is not null)
        {
            StopCapture(ExecutionTraceEndReason.CaptureFailure, ExecutionTraceIssueKind.CaptureFailure, "nested-command-dispatch");
            return null;
        }

        string hash = HistoricalExecutionContextRules.HashCommand(commandText);
        int occurrence = PeekOccurrence(CurrentSegment, commandName, hash, commandOrdinal);
        activeCommand = new CommandDispatchObservation(
            CurrentSegment,
            commandText,
            commandOrdinal,
            occurrence,
            arguments,
            commandName,
            handlerProvenance,
            handlerResolved,
            nativeFork,
            nativeSwitchEvent,
            isFestival,
            locationName,
            specialVariableBefore,
            previousAnswerBefore,
            beforeCommands,
            hashBeforeCommands: !handlerIsNative || MayMutateCommandList(commandName));
        return activeCommand;
    }

    internal void ObserveReplacement(CommandDispatchObservation observation, IReadOnlyList<string> commands)
    {
        if (!ReferenceEquals(activeCommand, observation) || captureStopped)
            return;
        observation.ObserveReplacement(commands);
    }

    internal void EndCommand(
        CommandDispatchObservation? observation,
        int afterCommand,
        IReadOnlyList<string> afterCommands,
        bool commandArrayReferenceChanged,
        bool specialVariableAfter,
        int previousAnswerAfter)
    {
        if (observation is null || !ReferenceEquals(activeCommand, observation))
            return;
        activeCommand = null;
        if (captureStopped || finished)
            return;
        if (observation.ReplacementCount > 1)
        {
            StopCapture(ExecutionTraceEndReason.CaptureFailure, ExecutionTraceIssueKind.UnsupportedDecision, "multiple-command-replacements");
            return;
        }

        bool forkCommand = observation.CommandName.Equals("Fork", StringComparison.OrdinalIgnoreCase);
        bool switchCommand = observation.CommandName.Equals("SwitchEvent", StringComparison.OrdinalIgnoreCase);
        if (observation.ReplacementCommands is IReadOnlyList<string> replacement)
        {
            if (!TryReserveSemanticEntry() || !TryEnterChildSegment())
                return;
            CommitOccurrence(observation);
            ScriptSegmentKind kind = observation.NativeFork
                ? ScriptSegmentKind.ForkReplacement
                : observation.NativeSwitchEvent
                    ? ScriptSegmentKind.SwitchEventReplacement
                    : ScriptSegmentKind.DynamicReplacement;
            string? target = GetTarget(observation, forkCommand);
            ScriptSourceIdentity source = GetSource(observation, target, forkCommand || switchCommand);
            ScriptSegmentIdentity child = CreateChild(observation, kind, source, target, replacement);

            if (observation.NativeFork)
            {
                AddForkDecision(observation, target, AutomaticDecisionOutcome.ReplaceCommands, child);
                AddDiagnostic(observation, "fork", automaticDecisions[^1].Sequence, target, "ReplaceCommands", child,
                    specialVariableAfter, previousAnswerAfter, null);
            }
            else if (observation.NativeSwitchEvent)
            {
                AddDiagnostic(observation, "switchEvent", null, target, "ReplaceCommands", child,
                    specialVariableAfter, previousAnswerAfter, null);
            }
            else
            {
                MarkOpaque();
                AutomaticDecision decision = new(
                    automaticDecisions.Count,
                    Locator(observation, ExecutionDecisionKind.Opaque),
                    AutomaticDecisionCausality.Unknown,
                    null,
                    new AutomaticDecisionResult(AutomaticDecisionOutcome.ReplaceCommands, target, null, child));
                automaticDecisions.Add(decision);
                AddDiagnostic(observation, "opaqueReplacement", decision.Sequence, target, "ReplaceCommands", child,
                    specialVariableAfter, previousAnswerAfter, "unsupported-custom-replacement");
            }
            CurrentSegment = child;
            segmentDepth++;
            return;
        }

        bool unmarkedMutation = commandArrayReferenceChanged;
        if (!unmarkedMutation && observation.BeforeCommandListHash is string beforeHash)
            unmarkedMutation = !StringComparer.Ordinal.Equals(beforeHash,
                HistoricalExecutionContextRules.HashCommandList(afterCommands));
        if (unmarkedMutation)
        {
            StopCapture(ExecutionTraceEndReason.CaptureFailure, ExecutionTraceIssueKind.UnsupportedDecision, "unmarked-command-list-mutation");
            AddDiagnostic(observation, "opaqueMutation", null, null, null, null,
                specialVariableAfter, previousAnswerAfter, "unmarked-command-list-mutation");
            return;
        }

        if (!observation.HandlerResolved)
        {
            StopCapture(ExecutionTraceEndReason.CaptureFailure, ExecutionTraceIssueKind.MissingCommandHandler, "missing-command-handler");
            AddDiagnostic(observation, "missingHandler", null, null, null, null,
                specialVariableAfter, previousAnswerAfter, "missing-command-handler");
            return;
        }

        if (forkCommand && afterCommand != observation.BeforeCommand)
        {
            if (!TryReserveSemanticEntry())
                return;
            CommitOccurrence(observation);
            string? target = GetTarget(observation, forkCommand: true);
            if (observation.NativeFork)
            {
                AddForkDecision(observation, target, AutomaticDecisionOutcome.ContinueCurrentSegment, null);
                AddDiagnostic(observation, "fork", automaticDecisions[^1].Sequence, target, "ContinueCurrentSegment", null,
                    specialVariableAfter, previousAnswerAfter, null);
            }
            else
            {
                MarkOpaque();
                AutomaticDecision decision = new(
                    automaticDecisions.Count,
                    Locator(observation, ExecutionDecisionKind.Opaque),
                    AutomaticDecisionCausality.Unknown,
                    null,
                    new AutomaticDecisionResult(AutomaticDecisionOutcome.ContinueCurrentSegment, target, null, null));
                automaticDecisions.Add(decision);
                AddDiagnostic(observation, "opaqueFork", decision.Sequence, target, "ContinueCurrentSegment", null,
                    specialVariableAfter, previousAnswerAfter, "non-native-fork-handler");
            }
            return;
        }

        if (switchCommand && afterCommand != observation.BeforeCommand)
        {
            StopCapture(ExecutionTraceEndReason.CaptureFailure, ExecutionTraceIssueKind.CaptureFailure, "switch-without-replacement");
            AddDiagnostic(observation, "switchEvent", null, GetTarget(observation, forkCommand: false), null, null,
                specialVariableAfter, previousAnswerAfter, "switch-without-replacement");
            return;
        }

        if (observation.CommandName.Equals("Question", StringComparison.OrdinalIgnoreCase)
            || observation.CommandName.Equals("QuickQuestion", StringComparison.OrdinalIgnoreCase))
        {
            AddDiagnostic(observation, "question", null, null, "Presented", null,
                specialVariableAfter, previousAnswerAfter, null);
        }
    }

    internal void AbandonCommand(CommandDispatchObservation? observation, string detailCode)
    {
        if (observation is null || !ReferenceEquals(activeCommand, observation))
            return;
        activeCommand = null;
        StopCapture(ExecutionTraceEndReason.CaptureFailure, ExecutionTraceIssueKind.CaptureFailure, detailCode);
    }

    internal AnswerDialogueObservation? BeginAnswer(
        string questionKey,
        int selectedIndex,
        string commandText,
        int commandOrdinal,
        IReadOnlyList<string> commands,
        bool specialVariableBefore,
        int previousAnswerBefore)
    {
        AnswerObserverCallbacks++;
        if (captureStopped || finished)
            return null;
        string commandHash = HistoricalExecutionContextRules.HashCommand(commandText);
        return new AnswerDialogueObservation(
            CurrentSegment,
            commandText,
            commandHash,
            commandOrdinal,
            PeekOccurrence(CurrentSegment, "answer", commandHash, commandOrdinal),
            questionKey,
            selectedIndex,
            specialVariableBefore,
            previousAnswerBefore,
            HistoricalExecutionContextRules.HashCommandList(commands));
    }

    internal void EndAnswer(
        AnswerDialogueObservation? observation,
        IReadOnlyList<string> afterCommands,
        bool specialVariableAfter,
        int previousAnswerAfter)
    {
        if (observation is null || captureStopped || finished)
            return;
        string afterHash = HistoricalExecutionContextRules.HashCommandList(afterCommands);
        ScriptSegmentIdentity? child = null;
        if (!StringComparer.Ordinal.Equals(observation.BeforeCommandListHash, afterHash))
        {
            if (!TryReserveSemanticEntry() || !TryEnterChildSegment())
                return;
            CommitOccurrence(observation.Segment, "answer", observation.CommandHash, observation.CommandOrdinal);
            SegmentEntryIdentity entry = new(
                observation.Segment.PathHash,
                observation.CommandHash,
                observation.CommandOrdinal,
                observation.Occurrence,
                observation.SelectedIndex.ToString(System.Globalization.CultureInfo.InvariantCulture));
            ScriptSourceIdentity source = new(ScriptSourceKind.InlineChoiceLogic, null, observation.QuestionKey);
            child = new ScriptSegmentIdentity(
                ScriptSegmentKind.ChoiceInsertion,
                HistoricalExecutionContextRules.HashChildPath(playbackHash, ScriptSegmentKind.ChoiceInsertion, source, entry, afterHash),
                afterHash,
                source,
                entry);
            CurrentSegment = child;
            segmentDepth++;
        }
        AddAnswerDiagnostic(observation, child, specialVariableAfter, previousAnswerAfter);
    }

    internal void MarkObserverFailure(string detailCode)
        => StopCapture(ExecutionTraceEndReason.CaptureFailure, ExecutionTraceIssueKind.CaptureFailure, detailCode);

    internal NaturalExecutionTraceResult Finish(ExecutionTraceEndReason requestedEndReason)
    {
        if (finished)
            throw new InvalidOperationException("Execution trace was already finalized.");
        finished = true;
        activeCommand = null;

        ExecutionTraceEndReason endReason = forcedEndReason ?? requestedEndReason;
        bool completeCoverage = automaticCoverage == ExecutionTraceCoverage.Complete
            && opaqueCoverage == OpaqueDecisionCoverage.NoneObserved;
        ExecutionTraceCompletion completion = endReason == ExecutionTraceEndReason.NaturalComplete && completeCoverage
            ? automaticDecisions.Count == 0
                ? ExecutionTraceCompletion.EmptyComplete
                : ExecutionTraceCompletion.Complete
            : ExecutionTraceCompletion.Partial;
        HistoricalExecutionContext context = BuildContext(
            completion,
            endReason,
            automaticDecisions,
            automaticCoverage,
            opaqueCoverage,
            issues);
        string payload;
        if (!HistoricalExecutionContextCodec.TryEncode(context, out payload))
        {
            bool valid = HistoricalExecutionContextRules.TryValidate(context, out _);
            AddIssue(
                valid ? ExecutionTraceIssueKind.TraceLimitExceeded : ExecutionTraceIssueKind.CaptureFailure,
                valid ? "execution-json-limit" : "execution-context-invalid");
            DiagnosticsTruncated = true;
            ExecutionTraceEndReason degradedReason = valid
                ? ExecutionTraceEndReason.TraceLimitExceeded
                : ExecutionTraceEndReason.CaptureFailure;
            int low = 0;
            int high = automaticDecisions.Count;
            HistoricalExecutionContext? best = null;
            string bestPayload = "";
            while (low <= high)
            {
                int count = low + (high - low) / 2;
                HistoricalExecutionContext candidate = BuildContext(
                    ExecutionTraceCompletion.Partial,
                    degradedReason,
                    automaticDecisions.Take(count).ToArray(),
                    ExecutionTraceCoverage.Incomplete,
                    opaqueCoverage,
                    issues);
                if (HistoricalExecutionContextCodec.TryEncode(candidate, out string candidatePayload))
                {
                    best = candidate;
                    bestPayload = candidatePayload;
                    low = count + 1;
                }
                else
                {
                    high = count - 1;
                }
            }
            context = best ?? BuildContext(
                ExecutionTraceCompletion.Partial,
                ExecutionTraceEndReason.CaptureFailure,
                [],
                ExecutionTraceCoverage.Incomplete,
                OpaqueDecisionCoverage.DetectionUnavailable,
                [new ExecutionTraceIssue(ExecutionTraceIssueKind.CaptureFailure, null, "execution-context-unencodable")]);
            payload = bestPayload;
        }
        int bytes = payload.Length == 0 ? -1 : System.Text.Encoding.UTF8.GetByteCount(payload);
        NaturalExecutionTraceDiagnostic diagnostic = new(
            identity.AssetName,
            identity.EventId,
            rawEventKey,
            rootDefinitionHash,
            playbackHash,
            RootSegment,
            CommandObserverCallbacks,
            AnswerObserverCallbacks,
            DiagnosticsTruncated,
            diagnostics.ToArray(),
            context,
            bytes);
        return new NaturalExecutionTraceResult(context, diagnostic);
    }

    private HistoricalExecutionContext BuildContext(
        ExecutionTraceCompletion completion,
        ExecutionTraceEndReason endReason,
        IReadOnlyList<AutomaticDecision> decisions,
        ExecutionTraceCoverage decisionCoverage,
        OpaqueDecisionCoverage opaque,
        IReadOnlyList<ExecutionTraceIssue> traceIssues)
        => new(
            HistoricalExecutionContextRules.CurrentSchemaVersion,
            playbackHash,
            completion,
            endReason,
            new ExecutionTraceCoverageSummary(
                decisionCoverage,
                ExecutionTraceCoverage.NotCaptured,
                opaque),
            new ExecutionTraceProvenance(gameVersion, modVersion, locale),
            decisions,
            [],
            traceIssues);

    private ScriptSegmentIdentity CreateChild(
        CommandDispatchObservation observation,
        ScriptSegmentKind kind,
        ScriptSourceIdentity source,
        string? target,
        IReadOnlyList<string> commands)
    {
        string commandListHash = HistoricalExecutionContextRules.HashCommandList(commands);
        SegmentEntryIdentity entry = new(
            observation.Segment.PathHash,
            observation.CommandHash,
            observation.CommandOrdinal,
            observation.Occurrence,
            target);
        return new ScriptSegmentIdentity(
            kind,
            HistoricalExecutionContextRules.HashChildPath(playbackHash, kind, source, entry, commandListHash),
            commandListHash,
            source,
            entry);
    }

    private void AddForkDecision(
        CommandDispatchObservation observation,
        string? target,
        AutomaticDecisionOutcome outcome,
        ScriptSegmentIdentity? replacement)
    {
        AutomaticDecisionCausality causality = observation.Arguments.Length > 2
            ? AutomaticDecisionCausality.Autonomous
            : AutomaticDecisionCausality.Unknown;
        automaticDecisions.Add(new AutomaticDecision(
            automaticDecisions.Count,
            Locator(observation, ExecutionDecisionKind.Fork),
            causality,
            null,
            new AutomaticDecisionResult(outcome, target, null, replacement)));
    }

    private static DecisionLocator Locator(CommandDispatchObservation observation, ExecutionDecisionKind kind)
        => new(observation.Segment, kind, observation.CommandHash, observation.CommandOrdinal, observation.Occurrence);

    private ScriptSourceIdentity GetSource(CommandDispatchObservation observation, string? target, bool knownTransition)
    {
        if (!knownTransition || string.IsNullOrWhiteSpace(target))
            return new ScriptSourceIdentity(ScriptSourceKind.Dynamic, null, observation.CommandName);
        bool translation = observation.Arguments.Length > 3
            && bool.TryParse(observation.Arguments[3], out bool translated)
            && translated;
        if (translation)
            return new ScriptSourceIdentity(ScriptSourceKind.TranslationKey, null, target);
        if (observation.IsFestival)
            return new ScriptSourceIdentity(ScriptSourceKind.FestivalField, null, target);
        return new ScriptSourceIdentity(ScriptSourceKind.EventAssetEntry, "Data/Events/" + observation.LocationName, target);
    }

    private static string? GetTarget(CommandDispatchObservation observation, bool forkCommand)
    {
        if (!forkCommand)
            return observation.Arguments.Length > 1 ? observation.Arguments[1] : null;
        return observation.Arguments.Length > 2
            ? observation.Arguments[2]
            : observation.Arguments.Length > 1
                ? observation.Arguments[1]
                : null;
    }

    private static bool MayMutateCommandList(string commandName)
        => commandName.Equals("MineDeath", StringComparison.OrdinalIgnoreCase)
            || commandName.Equals("HospitalDeath", StringComparison.OrdinalIgnoreCase)
            || commandName.Equals("GrandpaEvaluation", StringComparison.OrdinalIgnoreCase)
            || commandName.Equals("GrandpaEvaluation2", StringComparison.OrdinalIgnoreCase)
            || commandName.Equals("SpecificTemporarySprite", StringComparison.OrdinalIgnoreCase);

    private int PeekOccurrence(ScriptSegmentIdentity segment, string commandName, string commandHash, int commandOrdinal)
    {
        string key = $"{segment.PathHash}\0{commandName}\0{commandHash}\0{commandOrdinal}";
        return occurrences.GetValueOrDefault(key);
    }

    private void CommitOccurrence(CommandDispatchObservation observation)
        => CommitOccurrence(observation.Segment, observation.CommandName, observation.CommandHash, observation.CommandOrdinal);

    private void CommitOccurrence(ScriptSegmentIdentity segment, string commandName, string commandHash, int commandOrdinal)
    {
        string key = $"{segment.PathHash}\0{commandName}\0{commandHash}\0{commandOrdinal}";
        occurrences[key] = occurrences.GetValueOrDefault(key) + 1;
    }

    private bool TryReserveSemanticEntry()
    {
        if (semanticEntries < HistoricalExecutionContextRules.MaxTraceEntries)
        {
            semanticEntries++;
            return true;
        }
        StopCapture(ExecutionTraceEndReason.TraceLimitExceeded, ExecutionTraceIssueKind.TraceLimitExceeded, "trace-entry-limit");
        return false;
    }

    private bool TryEnterChildSegment()
    {
        if (segmentDepth < HistoricalExecutionContextRules.MaxSegmentDepth)
            return true;
        StopCapture(ExecutionTraceEndReason.TraceLimitExceeded, ExecutionTraceIssueKind.TraceLimitExceeded, "segment-depth-limit");
        return false;
    }

    private void MarkOpaque()
    {
        automaticCoverage = ExecutionTraceCoverage.Incomplete;
        opaqueCoverage = OpaqueDecisionCoverage.UnsupportedObserved;
        AddIssue(ExecutionTraceIssueKind.UnsupportedDecision, "unsupported-opaque-decision");
    }

    private void StopCapture(ExecutionTraceEndReason reason, ExecutionTraceIssueKind issue, string detailCode)
    {
        if (captureStopped)
            return;
        captureStopped = true;
        forcedEndReason = reason;
        automaticCoverage = ExecutionTraceCoverage.Incomplete;
        AddIssue(issue, detailCode);
    }

    private void AddIssue(ExecutionTraceIssueKind kind, string detailCode)
    {
        if (issues.Any(issue => issue.Kind == kind && StringComparer.Ordinal.Equals(issue.DetailCode, detailCode)))
            return;
        issues.Add(new ExecutionTraceIssue(kind, null, detailCode));
    }

    private void AddDiagnostic(
        CommandDispatchObservation observation,
        string kind,
        long? sequence,
        string? target,
        string? outcome,
        ScriptSegmentIdentity? replacement,
        bool specialAfter,
        int previousAfter,
        string? detailCode)
    {
        if (diagnostics.Count >= HistoricalExecutionContextRules.MaxTraceEntries)
        {
            DiagnosticsTruncated = true;
            return;
        }
        diagnostics.Add(new ExecutionTraceDiagnosticEntry(
            diagnostics.Count,
            sequence,
            kind,
            observation.Segment.PathHash,
            observation.CommandHash,
            observation.CommandOrdinal,
            observation.Occurrence,
            observation.CommandName,
            observation.HandlerProvenance,
            target,
            outcome,
            replacement?.PathHash,
            null,
            null,
            observation.SpecialVariableBefore,
            specialAfter,
            observation.PreviousAnswerBefore,
            previousAfter,
            detailCode));
    }

    private void AddAnswerDiagnostic(
        AnswerDialogueObservation observation,
        ScriptSegmentIdentity? replacement,
        bool specialAfter,
        int previousAfter)
    {
        if (diagnostics.Count >= HistoricalExecutionContextRules.MaxTraceEntries)
        {
            DiagnosticsTruncated = true;
            return;
        }
        diagnostics.Add(new ExecutionTraceDiagnosticEntry(
            diagnostics.Count,
            null,
            "answerDiagnostic",
            observation.Segment.PathHash,
            observation.CommandHash,
            observation.CommandOrdinal,
            observation.Occurrence,
            "AnswerDialogue",
            "StardewValley.Event.answerDialogue",
            observation.SelectedIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
            replacement is null ? "StateOnly" : "ChoiceInsertion",
            replacement?.PathHash,
            observation.QuestionKey,
            observation.SelectedIndex,
            observation.SpecialVariableBefore,
            specialAfter,
            observation.PreviousAnswerBefore,
            previousAfter,
            null));
    }
}
