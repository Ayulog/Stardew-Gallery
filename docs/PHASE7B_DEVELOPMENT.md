# Phase 7B Development Report

Date: 2026-09-04

## Scope

Phase 7B implements only the pure HistoricalExecutionContext domain contract, validation, versioned JSON codec, capability calculation, response matching, hashing helpers, and BCL tests.

No Harmony/runtime hook, natural trace capture, branch replay override, SQLite migration, production history write, ReplayCoordinator/EventLauncher change, GMCM/UI, PreviewState, StateInjector, Phase 7C, or Phase 8 work is included.

## Implemented types

`Domain/HistoricalExecutionContext.cs` adds:

- completion/end/coverage/provenance types;
- `ScriptSegmentIdentity`, `ScriptSourceIdentity`, and `SegmentEntryIdentity`;
- `DecisionLocator` and automatic-decision result/causality types;
- player response identity and replay response matching result types;
- `HistoricalExecutionContext` with defensive collection copies and structural equality/hash semantics;
- load state (`Missing`, `EmptyComplete`, `Complete`, `Partial`, `Invalid`);
- capability (`ContentOnly`, `OutcomeAware`, `ExactCapable`);
- future runtime fidelity (`Exact`, `AutomaticBranchesPreserved`, `InteractiveContentOnly`, `Degraded`, `Failed`).

`History/HistoricalExecutionContextRules.cs` adds:

- full SHA-256 validation and deterministic length-framed hashes;
- root and child segment path hashes directly bound to PlaybackHash;
- decision/category/result/causality/sequence validation;
- replacement entry-to-locator validation;
- capability calculation that fails closed for partial, unknown, opaque, issue-bearing, or mismatched contexts;
- authored-key -> option-set/index -> same-locale text response matching;
- limits: 512 decisions, 256 KiB JSON, segment depth constant 64 for future runtime builders.

`History/HistoricalExecutionContextCodec.cs` adds:

- camelCase JSON and string enums;
- schema version 1;
- no integer enum acceptance;
- pre-deserialization future-schema detection;
- Missing/Invalid degradation instead of load exceptions;
- payload size protection;
- PlaybackHash binding rejection.

## Invariants enforced

- Trace binding is PlaybackHash-based; EventId cannot authorize reuse.
- Every segment path directly binds PlaybackHash.
- Root/child segment structures differ and validate their source/entry shape.
- A replacement segment's parent command site must match the automatic decision locator.
- Automatic/player decisions share one unique contiguous Sequence.
- Player-choice-derived automatic decisions reference an earlier recorded choice.
- Automatic and player decision kinds cannot be interchanged.
- Event questions cannot claim authored response keys; NPC dialogue responses require authored keys.
- Generated ordinal response keys must equal the recorded index and are guarded by option-set hash.
- `NotCaptured` coverage cannot contain recorded entries.
- Opaque/unknown decisions remain parseable but cannot receive outcome/exact capability.
- Missing, EmptyComplete, Complete, Partial, and Invalid are distinct.
- Context collections cannot be mutated through the caller's original list.
- Future/malformed/oversized payloads degrade without fabricated data.

## Capability semantics

```text
missing / invalid / partial / mismatched / opaque / unknown / issue-bearing
-> ContentOnly

complete automatic coverage
+ no opaque/unknown/issues
-> OutcomeAware

OutcomeAware
+ complete player-choice coverage
-> ExactCapable
```

EmptyComplete is ExactCapable only when both automatic and choice instrumentation coverage is explicitly complete.

## Response identity

Matching priority:

1. unique authored response key;
2. exact option-set hash plus original index;
3. unique selected-text hash in the same locale;
4. no match.

Translated text is never a cross-locale stable key. Ambiguity returns no match.

## Pure tests

The linked core check project covers:

- full JSON roundtrip and structural equality;
- full hash persistence;
- Missing vs EmptyComplete and Partial vs Complete;
- malformed syntax, non-object JSON, wrong schema type, numeric enums, unknown future schema;
- PlaybackHash and segment-path binding;
- DecisionLocator/ScriptSegmentIdentity equality;
- duplicate command ordinal and repeated occurrence;
- sequence gaps and bad causal references;
- malformed automatic results and mismatched replacement entries;
- authored/ordinal/text response priority, cross-locale and ambiguity rejection;
- legacy, automatic-only, exact, EmptyComplete, unknown, and opaque capabilities;
- 512-entry and 256-KiB limits;
- defensive collection copies;
- automatic/player decision-kind validation and coverage consistency;
- asset source slash/case normalization.

## Deliberately deferred

- Phase 7C: passive observers, trace builder/session lifecycle, one-record-per-natural-watch production writes, SQLite v2.
- Phase 7D: automatic decision replay, fork input override, HistoricalReplaySession.
- Phase 7E: passive explicit choice capture.
- Phase 7F: ExactHistoricalReplay config/GMCM and exact choice replay.
- Phase 7G: PreviewState/PreviewPlan/StateInjector.

The unresolved runtime source of Sophia `195012` marriage/estrangement remains a 7C fixture investigation. It does not change the pure 7B contract.

## Validation

- `dotnet build -c Release`: passed, 0 warnings, 0 errors.
- `dotnet run --project Checks/StardewGallery.Checks.csproj -c Release`: `Stardew Gallery checks passed.` (existing `NETSDK1138` only).
- `dotnet run --project PersistenceChecks/StardewGallery.PersistenceChecks.csproj -c Release`: `Stardew Gallery persistence checks passed.` (existing `NETSDK1138` only).
- `git diff --check`: passed.
