# Phase 7 Historical Execution Trace Schema

This document is the Phase 7A contract for the Phase 7B pure domain implementation. It is runtime-agnostic and uses only BCL types.

## 1. Ownership

```text
ObservedVariant
  owns frozen content and PlaybackHash

HistoricalEventRecord
  owns occurrence time/profile/variant association

HistoricalExecutionContext
  is an optional immutable one-to-one child of a HistoricalEventRecord
```

The context is never looked up by EventId alone. `PlaybackHash` is the minimum content binding.

## 2. Proposed domain records

```csharp
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

internal sealed record HistoricalExecutionContext(
    int SchemaVersion,
    string PlaybackHash,
    ExecutionTraceCompletion Completion,
    ExecutionTraceEndReason EndReason,
    ExecutionTraceCoverageSummary Coverage,
    ExecutionTraceProvenance Provenance,
    IReadOnlyList<AutomaticDecision> AutomaticDecisions,
    IReadOnlyList<PlayerChoiceDecision> PlayerChoices,
    IReadOnlyList<ExecutionTraceIssue> Issues
);
```

`Missing` and `Invalid` are not persisted as fake contexts. They are load states:

```csharp
internal enum HistoricalExecutionContextState
{
    Missing,
    EmptyComplete,
    Complete,
    Partial,
    Invalid
}

internal sealed record HistoricalExecutionContextLoad(
    HistoricalExecutionContextState State,
    HistoricalExecutionContext? Context,
    ExecutionContextInvalidReason? InvalidReason
);
```

## 3. Segment identity

```csharp
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

internal sealed record ScriptSourceIdentity(
    ScriptSourceKind Kind,
    string? AssetName,
    string? Key
);

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
```

Rules:

- Root has `Source.Kind = RootPlayback` and `EnteredBy = null`.
- Non-root segments require `EnteredBy`.
- `PathHash` is SHA-256 over length-framed schema marker, parent path (or PlaybackHash for root), segment kind, source identity, entry command site, selected target, and resulting parsed command-list hash.
- `CommandListHash` is SHA-256 over length-framed parsed command strings in order.
- Asset names are normalized like `EventIdentity` before hashing.
- Full uppercase 64-hex hashes are persisted. Prefixes are diagnostics only.

This supports root, `fork`, `switchEvent`, translation-backed replacement, `quickQuestion` insertion, duplicate commands, nested replacements, and changed current content.

## 4. Decision locator and ordering

```csharp
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
```

- `CommandHash`: SHA-256 of exact parsed command text.
- `CommandOrdinal`: location in this segment; hint plus duplicate discriminator, never sole identity.
- `Occurrence`: zero-based execution count for this logical site.
- `Sequence`: stored on every decision and shared across automatic/player arrays.

Replay merges both arrays by Sequence and consumes exactly the next expected decision. It never searches ahead. Complete traces require unique contiguous sequences from zero.

## 5. Automatic decisions

```csharp
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
```

Validation:

- Sequence is nonnegative.
- `PlayerChoiceDerived` requires an earlier player-choice sequence.
- Other causality values must not reference a choice.
- `ReplaceCommands` requires a replacement segment.
- `ContinueCurrentSegment` must not carry a replacement segment.
- `SelectAlternative` requires nonnegative SelectedIndex.
- `Unknown` causality prevents OutcomeAware/Exact capability.

Default replay forces Autonomous and RandomDerived results. PlayerChoiceDerived is verified but follows the new choice in interactive mode. Exact mode replays its causal recorded choice, then verifies the result.

## 6. Player choices and response identity

```csharp
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
```

Matching priority:

1. Unique authored logical key (`$r` NPC response).
2. Exact option-set hash + valid original index.
3. Unique selected-text hash only when replay locale equals capture locale.
4. No match.

Generated ordinal keys from event `question`/`quickQuestion` are not considered authored. Translated text is never the sole cross-locale key. Ambiguous matches fail.

Option-set hash is SHA-256 of ordered, length-framed logical key kind/key plus exact displayed option text. This guards ordinal reuse when content differs.

## 7. Issues and completion

```csharp
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
```

Persist only stable detail codes, not absolute paths, full save state, or unrelated personal data.

Completion rules:

- `EmptyComplete`: NaturalComplete, zero decisions, automatic and choice coverage Complete, opaque NoneObserved.
- `Complete`: NaturalComplete, one or more decisions, all decision sequences valid. Coverage may still determine capability.
- `Partial`: skipped/interrupted/failure/overflow, or incomplete coverage.
- Missing: no child row; legacy records.
- Invalid: malformed JSON, unsupported future schema, invalid hash, binding mismatch, sequence/locator/result validation failure, or mirrored column disagreement.

## 8. Capability and runtime fidelity

```csharp
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
```

Capability rules:

```text
Missing/Invalid/Partial/binding mismatch
-> ContentOnly

Complete or EmptyComplete
+ automatic coverage Complete
+ opaque NoneObserved
+ no Unknown automatic decisions
-> OutcomeAware

OutcomeAware
+ player-choice coverage Complete
+ every recorded response identity valid
-> ExactCapable
```

An event with no choices is ExactCapable only if choice instrumentation coverage is explicitly Complete. EmptyComplete can be ExactCapable. A Phase 7C automatic-only collector must set player-choice coverage NotCaptured and therefore cannot claim ExactCapable.

Runtime fidelity is calculated after matching/applying decisions; config alone never selects it.

## 9. Binding

Hard invariant:

```text
context.PlaybackHash == selected ObservedVariant.Playback.PlaybackHash
```

An optional repository association to full `ObservedVariantKey` is provided by the record foreign key. PlaybackHash remains inside JSON so exported/lazy payloads are self-validating.

Same EventId never authorizes trace reuse. A Sophia-present trace cannot apply to Sophia-absent content.

## 10. JSON example

```json
{
  "schemaVersion": 1,
  "playbackHash": "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
  "completion": "Complete",
  "endReason": "NaturalComplete",
  "coverage": {
    "automaticDecisions": "Complete",
    "playerChoices": "Complete",
    "opaqueDecisions": "NoneObserved"
  },
  "provenance": {
    "gameVersion": "1.6.15.24356",
    "modVersion": "1.0.0",
    "locale": "zh"
  },
  "automaticDecisions": [
    {
      "sequence": 1,
      "locator": {
        "segment": {
          "kind": "Root",
          "pathHash": "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB",
          "commandListHash": "CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC",
          "source": { "kind": "RootPlayback", "assetName": null, "key": null },
          "enteredBy": null
        },
        "kind": "Fork",
        "commandHash": "DDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDD",
        "commandOrdinal": 31,
        "occurrence": 0
      },
      "causality": "PlayerChoiceDerived",
      "causedByPlayerChoiceSequence": 0,
      "result": {
        "outcome": "ReplaceCommands",
        "stableResultId": "choseToExplain",
        "selectedIndex": null,
        "replacementSegment": {
          "kind": "ForkReplacement",
          "pathHash": "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE",
          "commandListHash": "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF",
          "source": { "kind": "EventAssetEntry", "assetName": "Data/Events/HaleyHouse", "key": "choseToExplain" },
          "enteredBy": {
            "parentSegmentPathHash": "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB",
            "commandHash": "DDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDD",
            "commandOrdinal": 31,
            "occurrence": 0,
            "selectedTarget": "choseToExplain"
          }
        }
      }
    }
  ],
  "playerChoices": [
    {
      "sequence": 0,
      "locator": {
        "segment": {
          "kind": "Root",
          "pathHash": "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB",
          "commandListHash": "CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC",
          "source": { "kind": "RootPlayback", "assetName": null, "key": null },
          "enteredBy": null
        },
        "kind": "NativeQuestion",
        "commandHash": "1111111111111111111111111111111111111111111111111111111111111111",
        "commandOrdinal": 30,
        "occurrence": 0
      },
      "response": {
        "kind": "GeneratedOrdinal",
        "nativeKey": "1",
        "originalIndex": 1,
        "optionCount": 2,
        "optionSetHash": "2222222222222222222222222222222222222222222222222222222222222222",
        "selectedTextHash": "3333333333333333333333333333333333333333333333333333333333333333",
        "captureLocale": "zh"
      }
    }
  ],
  "issues": []
}
```

## 11. Serialization contract

- Payload schema version: 1.
- UTF-8 JSON; camelCase properties; enum names as strings.
- Full hashes only.
- Unknown additional properties are ignored for same-version forward-compatible additions.
- `schemaVersion > supported` returns Invalid/FutureSchema; no partial interpretation.
- Null/blank payload returns Missing.
- Malformed or invalid payload returns Invalid and never throws into profile loading.
- Encoding validates before serialization.
- Maximum 256 KiB UTF-8 payload and 512 combined decisions.
- JSON depth maximum 64; segment depth maximum 64.

The DB mirrors `schema_version` and `completion_status` for cheap metadata. A mirror mismatch makes only that context Invalid.

## 12. Mismatch and degradation

- PlaybackHash mismatch: reject context; ContentOnly.
- Locator mismatch: do not search ahead; mark Degraded or stop if native continuation is unsafe.
- Autonomous result mismatch after application: no OutcomeAware/Exact claim.
- Exact response missing/ambiguous: do not choose another option; leave UI interactive and mark Degraded.
- Unsupported custom decision: ContentOnly unless an adapter covered it at capture and replay.
- Missing handler: explicit MissingCommandHandler; degrade or fail.
- Partial trace: do not apply a prefix while claiming historical outcome. Replay content-only.
- Replay-time diagnostics are ephemeral and never update the natural trace.

## 13. Corruption and legacy semantics

- No child context row = Missing, not EmptyComplete.
- Legacy observed variants and compatibility snapshots remain playable as frozen content.
- One malformed context must not suppress its HistoricalEventRecord or ObservedVariant.
- Never backfill old records with empty or guessed traces.
- Future schema remains intact on disk and is treated as unavailable by the old runtime.

## 14. Data minimization and estimates

Persist:

- schema version, PlaybackHash;
- completion/end/coverage;
- game/mod version and locale;
- decision locators/results and stable issue codes.

Do not persist full save, full inventory, friendship map, chat, unrelated names, absolute paths, mouse input, skip timing, speed, or tick-by-tick state.

Expected typical payload: 0.5-5 KiB. Complex branch-heavy event: tens of KiB. Hard proposal: 256 KiB and 512 decisions. Capture is O(decisions), not O(ticks x world state).

## 15. Phase 7B pure tests

1. execution-context JSON roundtrip;
2. Missing != EmptyComplete;
3. Partial != Complete;
4. malformed payload -> Invalid;
5. future schema -> Invalid/FutureSchema;
6. DecisionLocator equality;
7. duplicate command distinction by ordinal/occurrence;
8. ScriptSegmentIdentity equality and transition differences;
9. global sequence ordering and gap rejection;
10. PlaybackHash mismatch rejection;
11. response matching priority and ambiguity rejection;
12. automatic-only -> OutcomeAware, not ExactCapable;
13. complete choices -> ExactCapable;
14. legacy/Missing -> ContentOnly;
15. EmptyComplete semantics and capability;
16. malformed automatic result/cause rejection;
17. full 64-hex hash persistence;
18. opaque/unknown decision capability degradation;
19. entry/payload size limits;
20. same EventId does not bypass PlaybackHash binding.
