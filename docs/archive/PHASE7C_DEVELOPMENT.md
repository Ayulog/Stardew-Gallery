# Phase 7C Development Report

Date: 2026-09-04

## 1. Scope and base

- Branch: `phase7/passive-execution-capture`
- Base: `aea934999b87d76caa3cc1191de2f22686c52a1c`
- Phase 7A/7B contracts were read before implementation.
- Scope: passive natural-event decision observation, trace construction/diagnostics, one occurrence per natural execution, optional execution-context persistence, SQLite v1 -> v2 migration.
- No historical decision forcing, GSQ override, explicit-choice replay, GMCM/UI, PreviewState, StateInjector, Event engine replacement, Phase 7D, or Phase 8 work.

## 2. Native hook points

Installed Stardew 1.6.15 signatures:

```csharp
public virtual void Event.tryEventCommand(GameLocation location, GameTime time, string[] args)
public void Event.ReplaceAllCommands(params string[] commands)
public void Event.answerDialogue(string questionKey, int answerChoice)
public void Event.exitEvent()
```

`ExecutionTraceObserver` applies:

- dispatcher prefix/postfix/finalizer;
- `ReplaceAllCommands` prefix;
- answer prefix/postfix/finalizer for diagnostics only;
- exit postfix for natural-completion evidence.

Every callback contains its own errors. Finalizers return the original native exception unchanged. Observer code never modifies arguments, command arrays, decisions, handlers, or native state.

Startup verifies all four Harmony targets. Failure disables only execution trace capture; normal event execution, existing history, and replay remain available.

## 3. Capture architecture and ownership

```text
Natural Event instance
-> ExecutionTraceObserver
-> WatchedEventHistory natural admission
-> NaturalExecutionTraceCapture (exact Event reference)
-> NaturalExecutionTraceBuilder
-> HistoricalExecutionContext
-> HistoricalEventRecord
-> HistoryRepository
```

`WatchedEventHistory` remains the natural occurrence owner. The dispatcher prefix can start observation before the first command runs, avoiding the previous UpdateTicked first-command race. It reuses the existing exact script/asset candidate resolution and frozen `WatchedEventSnapshot`.

Session scope is the exact `Event` reference. A different event finalizes the previous trace as Interrupted. Replay is excluded by the explicit `ReplayCoordinator.IsActive` marker and an exact `Game1.CurrentEvent` check. ReturnedToTitle finalizes as QuitToTitle before SQLite detaches.

## 4. Root segment and runtime locator

At admission:

```text
root command list = defensive copy of Event.eventCommands
CommandListHash   = HistoricalExecutionContextRules.HashCommandList
PathHash          = HashRootPath(PlaybackHash, CommandListHash)
Kind              = Root
Source            = RootPlayback
EnteredBy         = null
```

At a semantic decision:

```text
current segment
+ exact Event.GetCurrentCommand() hash
+ CurrentCommand ordinal
+ committed occurrence counter
```

Occurrence advances only when a decision/transition commits. Tick retries of the same presentation command do not advance it. Duplicate identical commands at different ordinals remain distinct.

## 5. Segment transition detection

`ReplaceAllCommands` is the authoritative destructive-replacement marker. The marker is attributed only to an active dispatcher frame for the same Event. An unattributed replacement marks capture Partial instead of silently keeping an obsolete segment.

Child identity binds:

- PlaybackHash;
- parent segment path;
- entry command hash/ordinal/occurrence;
- target/source;
- resulting command-list hash.

Depth is capped at 64. `quickQuestion` answer-time insertion is observed as `ChoiceInsertion` for diagnostics/segment tracking, not as a persisted player choice.

Known native command-list mutations that do not use `ReplaceAllCommands` are checked through pre/post hashes (`MineDeath`, `HospitalDeath`, `GrandpaEvaluation`, `GrandpaEvaluation2`, and `SpecificTemporarySprite`). Custom handlers are also hashed. Unmarked mutation fails closed as Partial.

## 6. Fork and switchEvent capture

Native `fork` handler provenance is verified against `Event.DefaultCommands.Fork` from Stardew's assembly.

- required-ID `fork <requiredId> <target>`: `Autonomous` (native reads local mail/answered-question IDs);
- single-key `fork <target>`: `Unknown` in 7C, since it reads `specialEventVariable1` and may be choice-derived;
- replacement -> `ReplaceCommands` + child segment;
- fallthrough -> `ContinueCurrentSegment`.

No result is forced.

Native `switchEvent` creates a `SwitchEventReplacement` segment but no `AutomaticDecision`, since it is an unconditional transition.

Custom/changed handlers continue running. If they replace commands, the trace records an Opaque/Unknown decision, marks unsupported opaque coverage, and cannot become OutcomeAware.

## 7. Event question diagnostics

`Event.answerDialogue` records diagnostics only:

- `questionKey`;
- selected index;
- current command/segment;
- `specialEventVariable1` before/after;
- private `previousAnswerChoice` before/after;
- command-list hash before/after;
- choice-insertion segment if the list changes.

No `PlayerChoiceDecision` is persisted. Player-choice coverage remains `NotCaptured`; formal choice capture remains Phase 7E.

## 8. Completion and degradation

- NaturalComplete: `exitEvent` observed without `skipped`.
- Skipped: `exitEvent` observed with `skipped`.
- Interrupted: Event reference disappears/changes without exit.
- QuitToTitle: title return before persistence detaches.
- ExternalTermination: replay starts over an active admission/session cleanup.
- CaptureFailure: observer/mutation/handler tracking cannot remain reliable.
- TraceLimitExceeded: 512 semantic entries, 64 segment depth, or 256-KiB encoded JSON.

NaturalComplete with no automatic decisions is `EmptyComplete`, even though choice coverage remains NotCaptured. It is OutcomeAware, not ExactCapable.

Skipped/interrupted/quit/failure/overflow traces are Partial. Phase 7C retains the occurrence with Partial context where repository/profile lifetime permits. Replay cleanup does not create a natural occurrence.

Capture and diagnostics failures never cancel native gameplay.

## 9. Trace limits and performance

- `MaxTraceEntries = 512`.
- `MaxExecutionJsonBytes = 256 KiB`.
- `MaxSegmentDepth = 64`.
- Diagnostic entries use the same cap.

On final JSON overflow, decisions are truncated by binary search to the largest valid Partial/TraceLimitExceeded context. No JSON is serialized per tick; serialization occurs once at finalization. Presentation retries do not hash whole command lists. Command-list hashes occur for admission, known transitions, custom handlers, known native mutators, and answer diagnostics.

Runtime diagnostic fields report command observer callbacks, answer callbacks, persisted decisions, and encoded byte size. Actual representative-event callback counts/sizes require manual runtime acceptance.

## 10. Diagnostics

When `DebugDiagnostics` is enabled, the latest finalized natural trace is written to:

```text
Constants.DataPath/StardewGallery/diagnostics/historical-execution-latest.json
```

It contains EventIdentity, raw key, root-definition/playback hashes, root segment, command/answer callback counts, semantic decision/transition entries, handler provenance, fork targets/results, answer state transitions, completion/coverage/issues, and execution JSON bytes. It contains no full save snapshot.

Two Sophia traces can be compared by the ordered diagnostic entries. The first differing segment path, command site, answer state, fork target/result, handler provenance, or issue is the first observable divergence.

## 11. SQLite v2

`GallerySchema.CurrentVersion = 2` adds:

```sql
historical_execution_contexts (
    context_pk INTEGER PRIMARY KEY,
    record_fk INTEGER NOT NULL UNIQUE
              REFERENCES historical_event_records(record_pk) ON DELETE CASCADE,
    schema_version INTEGER NOT NULL,
    completion_status TEXT NOT NULL,
    execution_json TEXT NOT NULL
)
```

Migration is transactional. Validation covers core columns, required uniqueness, FK/cascade, `events.asset_name` collation, context schema, and `foreign_key_check`. Failed validation/DDL leaves v1 intact. Current malformed v2 and future schema are rejected without overwrite.

Old records receive no fabricated context.

## 12. Occurrence/context persistence

`HistoryRepository.AddNaturalOccurrence` writes event, variant, summary, and append-only occurrence in one transaction. It validates that variant/summary/record keys agree.

Context is validated/encoded and PlaybackHash-bound before SQL. Invalid/mismatched context is omitted while the truthful occurrence persists ContentOnly. Context insertion uses a savepoint; child failure rolls back only the child and preserves the occurrence. Core occurrence failure rolls back event/variant/summary/record.

`LoadHistoricalOccurrencesForProfile` returns chronological occurrence rows plus independently decoded context state. Malformed payload/mirror affects only that record. Multiple natural watches of one variant remain multiple rows.

Legacy imports still create only observed variants/summaries; they do not fabricate occurrences or traces.

## 13. Phase 7B runtime-driven correction

`EmptyComplete` now permits `PlayerChoices = NotCaptured` when automatic coverage is complete and there are no decisions/issues. This was necessary for Phase 7C automatic-only instrumentation. Capability remains OutcomeAware, never ExactCapable.

## 14. Autonomous fixture

Search of installed mod event JSON found a real SVE required-ID fork fixture:

- Asset: `Data/Events/ArchaeologyHouse`
- EventId: `1848481`
- Source: SVE `code/NPCs/Elliott.json`
- Root key: `1848481/f Elliott 2000/t 1300 1900/n elliottReading`
- Script: `.../fork 958699 mysteryBook/fork 958700 romanceBook/speak Elliott ...`
- State: `dialogueQuestionsAnswered` IDs `958699` (mystery) and `958700` (romance).
- Fallback: neither ID -> inline sci-fi script.

This is a real, statically loadable native required-ID fork. Both outcomes were not run during this implementation session, so P7C-12 remains PENDING.

## 15. Sophia finding

Sophia `Data/Events/HaleyHouse`, EventId `195012`, remains a trace-diff fixture. Static evidence still shows player-variable forks following explicit questions. The new diagnostic captures exactly the requested answer/fork/segment state, but no natural marriage/estrangement runs were executed in this session.

Classification: **B. divergence narrowed but not fully identified.**

- Last identical point: not available until two runtime diagnostics are produced.
- First differing observable: not available until State X and State Y natural runs.
- Remaining boundary: the first differing diagnostic entry or native behavior after the shared root/answer/fork sequence.
- Next experiment: capture two natural `195012` runs with DebugDiagnostics, preserve both JSON files, and compare ordered entries plus the first visible outcome difference.

## 16. Automated tests

Core checks cover:

- natural/replay classification;
- root segment construction;
- fork false/true and autonomous/unknown causality;
- switch transition not decision;
- duplicate/repeated occurrence and global sequence;
- presentation retry suppression;
- opaque replacement and unmarked mutation degradation;
- answer diagnostics/choice insertion without player trace;
- no-decision EmptyComplete;
- skipped/interrupted/quit/failure/overflow lifecycle;
- trace entry and JSON byte caps.

Persistence checks cover:

- empty/populated v1 -> v2;
- data preservation/no fabricated context;
- repeat open;
- DDL and post-DDL validation rollback;
- malformed current v2 and future schema rejection;
- occurrence + context, occurrence without context, binding rejection;
- multiple occurrences;
- malformed child/mirror isolation;
- context FK cascade;
- child failure preserves occurrence;
- core failure transaction rollback.

## 17. Manual acceptance status

No in-game runtime acceptance was performed in this implementation environment.

| ID | Status | Reason |
| --- | --- | --- |
| P7C-1 no-op observer | PENDING | needs natural in-game run |
| P7C-2 no-decision event | AUTOMATED PASS / runtime PENDING | builder semantics proven |
| P7C-3 fork false | AUTOMATED PASS / runtime PENDING | native fixture not run |
| P7C-4 fork true | AUTOMATED PASS / runtime PENDING | native fixture not run |
| P7C-5 switchEvent | AUTOMATED PASS / runtime PENDING | SVE fixture not run |
| P7C-6 replay exclusion | AUTOMATED PASS / runtime PENDING | explicit predicate proven |
| P7C-7 reload persistence | AUTOMATED PASS / runtime PENDING | repository reload proven |
| P7C-8 failure/overflow | AUTOMATED PASS / runtime PENDING | injected pure/SQL failures proven |
| P7C-9 Sophia marriage | PENDING | no natural run |
| P7C-10 Sophia estrangement | PENDING | no natural run |
| P7C-11 Sophia divergence | PENDING | requires P7C-9/10 JSON |
| P7C-12 autonomous fixture | PENDING | Elliott candidate found, states not run |

## 18. Phase 7D gate

```text
PHASE 7D GATE

passive capture semantics unchanged?: STATIC/PURE EVIDENCE YES; runtime not proven
event scoping proven?: AUTOMATED YES; runtime pending
locator unambiguous?: AUTOMATED YES; runtime pending
automatic decision source proven?: static Elliott fixture YES; both outcomes not captured
replay exclusion proven?: AUTOMATED YES; runtime pending

unresolved blockers:
- no-op natural observer smoke not run
- persistence reload not verified in a live profile
- Elliott required-ID fork true/false not captured
- Sophia X/Y divergence not located

recommendation:
run P7C-1 through P7C-12 manual matrix and inspect diagnostics before branch forcing

NOT READY
```

Even after manual acceptance, Phase 7D must start only on human instruction.

## 19. Explicit non-goals confirmed

- No branch forcing or historical decision replay.
- No GSQ override or temporary historical state injection.
- No explicit player-choice persistence or auto-selection.
- No GMCM/history UI.
- No PreviewState/PreviewPlan/StateInjector.
- No ReplayBackup/ReplaySnapshot/EventLauncher behavior change.
- No full-save historical snapshots.
- Replay does not create natural history.
- Phase 7D and Phase 8 not started.
