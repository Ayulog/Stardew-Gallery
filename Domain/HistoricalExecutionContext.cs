namespace StardewGallery;

internal enum ExecutionTraceCompletion
{
    EmptyComplete,
    Complete,
    Partial
}

internal enum ExecutionTraceEndReason
{
    NaturalComplete,
    Skipped,
    Interrupted,
    QuitToTitle,
    ExternalTermination,
    CaptureFailure,
    TraceLimitExceeded,
    Unknown
}

internal enum ExecutionTraceCoverage
{
    NotCaptured,
    Complete,
    Incomplete
}

internal enum OpaqueDecisionCoverage
{
    NoneObserved,
    UnsupportedObserved,
    DetectionUnavailable
}

internal sealed record ExecutionTraceCoverageSummary(
    ExecutionTraceCoverage AutomaticDecisions,
    ExecutionTraceCoverage PlayerChoices,
    OpaqueDecisionCoverage OpaqueDecisions
);

internal sealed record ExecutionTraceProvenance(
    string GameVersion,
    string ModVersion,
    string Locale
);

internal enum ScriptSegmentKind
{
    Root,
    ForkReplacement,
    SwitchEventReplacement,
    ChoiceInsertion,
    DynamicReplacement
}

internal enum ScriptSourceKind
{
    RootPlayback,
    EventAssetEntry,
    TranslationKey,
    FestivalField,
    InlineChoiceLogic,
    Dynamic
}

internal readonly struct ScriptSourceIdentity : IEquatable<ScriptSourceIdentity>
{
    private readonly string? assetName;
    private readonly string? key;

    public ScriptSourceKind Kind { get; }
    public string? AssetName => assetName;
    public string? Key => key;

    [System.Text.Json.Serialization.JsonConstructor]
    public ScriptSourceIdentity(ScriptSourceKind kind, string? assetName, string? key)
    {
        Kind = kind;
        this.assetName = assetName?.Replace('\\', '/').Trim();
        this.key = key?.Trim();
    }

    public bool Equals(ScriptSourceIdentity other)
        => Kind == other.Kind
            && StringComparer.OrdinalIgnoreCase.Equals(AssetName, other.AssetName)
            && StringComparer.Ordinal.Equals(Key, other.Key);

    public override bool Equals(object? obj) => obj is ScriptSourceIdentity other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(
        Kind,
        AssetName is null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(AssetName),
        Key is null ? 0 : StringComparer.Ordinal.GetHashCode(Key));

    public static bool operator ==(ScriptSourceIdentity left, ScriptSourceIdentity right) => left.Equals(right);
    public static bool operator !=(ScriptSourceIdentity left, ScriptSourceIdentity right) => !left.Equals(right);
}

internal sealed record SegmentEntryIdentity(
    string ParentSegmentPathHash,
    string CommandHash,
    int CommandOrdinal,
    int Occurrence,
    string? SelectedTarget
);

internal sealed record ScriptSegmentIdentity(
    ScriptSegmentKind Kind,
    string PathHash,
    string CommandListHash,
    ScriptSourceIdentity Source,
    SegmentEntryIdentity? EnteredBy
);

internal enum ExecutionDecisionKind
{
    Fork,
    NativeQuestion,
    QuickQuestion,
    DialogueResponse,
    RandomRoute,
    StateConditional,
    Opaque
}

internal sealed record DecisionLocator(
    ScriptSegmentIdentity Segment,
    ExecutionDecisionKind Kind,
    string CommandHash,
    int CommandOrdinal,
    int Occurrence
);

internal enum AutomaticDecisionCausality
{
    Autonomous,
    PlayerChoiceDerived,
    RandomDerived,
    Unknown
}

internal enum AutomaticDecisionOutcome
{
    ContinueCurrentSegment,
    ReplaceCommands,
    SelectAlternative
}

internal sealed record AutomaticDecisionResult(
    AutomaticDecisionOutcome Outcome,
    string? StableResultId,
    int? SelectedIndex,
    ScriptSegmentIdentity? ReplacementSegment
);

internal sealed record AutomaticDecision(
    long Sequence,
    DecisionLocator Locator,
    AutomaticDecisionCausality Causality,
    long? CausedByPlayerChoiceSequence,
    AutomaticDecisionResult Result
);

internal enum ResponseIdentityKind
{
    AuthoredKey,
    GeneratedOrdinal,
    IndexOnly
}

internal sealed record ResponseIdentity(
    ResponseIdentityKind Kind,
    string? NativeKey,
    int OriginalIndex,
    int OptionCount,
    string OptionSetHash,
    string? SelectedTextHash,
    string? CaptureLocale
);

internal sealed record PlayerChoiceDecision(
    long Sequence,
    DecisionLocator Locator,
    ResponseIdentity Response
);

internal enum ExecutionTraceIssueKind
{
    CaptureFailure,
    TraceLimitExceeded,
    UnsupportedDecision,
    MissingCommandHandler,
    Interrupted,
    BindingMismatch,
    LocatorMismatch,
    ResponseMismatch
}

internal sealed record ExecutionTraceIssue(
    ExecutionTraceIssueKind Kind,
    long? Sequence,
    string? DetailCode
);

internal sealed class HistoricalExecutionContext : IEquatable<HistoricalExecutionContext>
{
    public int SchemaVersion { get; }
    public string PlaybackHash { get; }
    public ExecutionTraceCompletion Completion { get; }
    public ExecutionTraceEndReason EndReason { get; }
    public ExecutionTraceCoverageSummary Coverage { get; }
    public ExecutionTraceProvenance Provenance { get; }
    public IReadOnlyList<AutomaticDecision> AutomaticDecisions { get; }
    public IReadOnlyList<PlayerChoiceDecision> PlayerChoices { get; }
    public IReadOnlyList<ExecutionTraceIssue> Issues { get; }

    [System.Text.Json.Serialization.JsonConstructor]
    public HistoricalExecutionContext(
        int schemaVersion,
        string playbackHash,
        ExecutionTraceCompletion completion,
        ExecutionTraceEndReason endReason,
        ExecutionTraceCoverageSummary coverage,
        ExecutionTraceProvenance provenance,
        IReadOnlyList<AutomaticDecision> automaticDecisions,
        IReadOnlyList<PlayerChoiceDecision> playerChoices,
        IReadOnlyList<ExecutionTraceIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(automaticDecisions);
        ArgumentNullException.ThrowIfNull(playerChoices);
        ArgumentNullException.ThrowIfNull(issues);
        SchemaVersion = schemaVersion;
        PlaybackHash = playbackHash;
        Completion = completion;
        EndReason = endReason;
        Coverage = coverage;
        Provenance = provenance;
        AutomaticDecisions = Array.AsReadOnly(automaticDecisions.ToArray());
        PlayerChoices = Array.AsReadOnly(playerChoices.ToArray());
        Issues = Array.AsReadOnly(issues.ToArray());
    }

    public bool Equals(HistoricalExecutionContext? other)
        => other is not null
            && SchemaVersion == other.SchemaVersion
            && StringComparer.Ordinal.Equals(PlaybackHash, other.PlaybackHash)
            && Completion == other.Completion
            && EndReason == other.EndReason
            && Equals(Coverage, other.Coverage)
            && Equals(Provenance, other.Provenance)
            && AutomaticDecisions.SequenceEqual(other.AutomaticDecisions)
            && PlayerChoices.SequenceEqual(other.PlayerChoices)
            && Issues.SequenceEqual(other.Issues);

    public override bool Equals(object? obj) => Equals(obj as HistoricalExecutionContext);

    public override int GetHashCode()
    {
        HashCode hash = new();
        hash.Add(SchemaVersion);
        hash.Add(PlaybackHash, StringComparer.Ordinal);
        hash.Add(Completion);
        hash.Add(EndReason);
        hash.Add(Coverage);
        hash.Add(Provenance);
        foreach (AutomaticDecision decision in AutomaticDecisions)
            hash.Add(decision);
        foreach (PlayerChoiceDecision choice in PlayerChoices)
            hash.Add(choice);
        foreach (ExecutionTraceIssue issue in Issues)
            hash.Add(issue);
        return hash.ToHashCode();
    }
}

internal enum HistoricalExecutionContextState
{
    Missing,
    EmptyComplete,
    Complete,
    Partial,
    Invalid
}

internal enum ExecutionContextInvalidReason
{
    MalformedPayload,
    FutureSchema,
    UnsupportedSchema,
    InvalidModel,
    PlaybackMismatch,
    PayloadTooLarge
}

internal sealed record HistoricalExecutionContextLoad(
    HistoricalExecutionContextState State,
    HistoricalExecutionContext? Context,
    ExecutionContextInvalidReason? InvalidReason
);

internal enum HistoricalReplayCapability
{
    ContentOnly,
    OutcomeAware,
    ExactCapable
}

internal enum HistoricalReplayFidelity
{
    Exact,
    AutomaticBranchesPreserved,
    InteractiveContentOnly,
    Degraded,
    Failed
}

internal sealed record ReplayResponseOption(
    ResponseIdentityKind KeyKind,
    string? NativeKey,
    string TextHash
);

internal enum ResponseMatchKind
{
    None,
    AuthoredKey,
    OptionSetAndIndex,
    SameLocaleText
}

internal sealed record ResponseMatchResult(
    bool Matched,
    int Index,
    ResponseMatchKind Kind
)
{
    internal static ResponseMatchResult NoMatch { get; } = new(false, -1, ResponseMatchKind.None);
}
