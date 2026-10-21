# Phase 7 Implementation Roadmap

Base: Phase 6 closed at `b2bcbd724211bb23a94e8665283b92bd6d8e7150`.

Phase 8 is not part of this roadmap execution.

## Phase 7B - Pure execution trace domain

Goal:

- implement the immutable `HistoricalExecutionContext` contract, validation, serialization, response matching, capability/fidelity enums, and pure tests.

Allowed scope:

- `Domain/HistoricalExecutionContext.cs` (or equivalent pure domain files);
- `History/HistoricalExecutionContextCodec.cs`;
- linked BCL checks in `Checks`;
- `docs/PHASE7B_DEVELOPMENT.md`.

Forbidden scope:

- Harmony/native hooks;
- runtime capture/replay sessions;
- SQLite schema/migration;
- GMCM/UI;
- PreviewState/StateInjector;
- EventLauncher/ReplayCoordinator behavior changes.

Automated tests:

- the 20 pure contract tests in `PHASE7_EXECUTION_TRACE_SCHEMA.md` section 15.

Runtime/manual acceptance:

- none; no runtime hooks exist in 7B.

Stop/go:

- GO if roundtrip, validation, binding, response matching, and capability semantics pass without Stardew runtime dependencies.
- STOP if the contract needs an unresolved native object or command-specific serialized type.

Dependencies: completed Phase 7A architecture gate.

Primary risks: R2 locator drift, R3 nested identity, R7 false claims, R11 response identity.

## Phase 7C - Passive capture and occurrence persistence

Goal:

- passively capture automatic decisions during natural events;
- begin writing one `HistoricalEventRecord` per natural occurrence;
- persist optional execution contexts via SQLite v2.

Allowed scope:

- targeted Harmony observers;
- event-scoped `NaturalExecutionTraceSession` / builder;
- `WatchedEventHistory` lifecycle integration;
- repository occurrence reads/writes;
- transactional v1 -> v2 migration;
- trace limits and diagnostics.

Forbidden scope:

- decision forcing;
- player-choice auto replay;
- broad GSQ/global state overrides;
- GMCM/UI/Preview;
- Event engine replacement.

Automated tests:

- populated/empty v1 migration;
- forced migration failure rollback;
- repeat open and future-version rejection;
- FK cascade and malformed child isolation;
- variant + record + context commit/fallback;
- replay exclusion and natural-only history;
- lifecycle complete/skip/interrupted/overflow;
- observer exception passivity;
- locator construction for replacement and duplicate commands.

Runtime/manual acceptance:

- no-op observer first: verify natural gameplay unchanged;
- Sophia `195012`: log `questionKey`, selected index, `specialEventVariable1` pre/post, fork args/result, command-list hashes, and handler provenance;
- autonomous `fork <requiredId> <newKey>` fixture: capture both outcomes;
- verify record/context survives reload.

Stop/go:

- STOP on altered natural behavior, cross-event leakage, unbounded trace, or ambiguous occurrence ownership.
- GO to 7D only after the runtime decision source for the chosen automatic fixture is proven.

Dependencies: 7B; exact 1.6.15 hook signatures; selected autonomous branch fixture.

Primary risks: R1, R2, R6, R9, R10, R12, R14.

## Phase 7D - Automatic historical branch replay

Goal:

- apply recorded autonomous/random branch results while native Event mechanics remain authoritative.

Allowed scope:

- `HistoricalReplaySession`;
- event/locator-scoped decision interceptor;
- synchronous native fork-input injection or equivalent verified handler boundary;
- matching, cursor, fidelity, mismatch diagnostics;
- ReplayCoordinator session activation/cleanup only.

Forbidden scope:

- explicit player-choice replay;
- GMCM/UI;
- broad replay-long state injection;
- script rewriting/Event engine implementation;
- Preview.

Automated tests:

- session binding/cursor/sequence;
- event instance and segment scoping;
- automatic result match/mismatch;
- player-choice-derived decision not independently forced;
- handler unavailable/changed;
- cleanup after launch failure/restore/exception.

Runtime/manual acceptance:

- capture an autonomous branch outcome;
- change current branch-driving state;
- historical replay preserves recorded automatic outcome;
- unrelated events/queries remain live;
- future Sophia acceptance only after the X/Y decision point is identified: capture Sophia-present + marriage, set world to estrangement, replay remains Sophia-present + marriage.

Stop/go:

- STOP if forcing cannot preserve native replacement side effects, needs global GSQ, or leaks outside the active Event.

Dependencies: 7C capture evidence; replay firewall adequate for fixture side effects.

Primary risks: R1, R2, R3, R7, R8, R9, R12, R13.

## Phase 7E - Passive explicit choice capture

Goal:

- capture event `question`/`quickQuestion` and translated NPC `$r` choices during natural play; default replay still asks again.

Allowed scope:

- passive `Event.answerDialogue` observer;
- passive `Dialogue.chooseResponse` observer;
- response identity and option-set hashing;
- choice coverage/provenance persistence.

Forbidden scope:

- auto-selection;
- GMCM/config/UI;
- synthetic input;
- Preview.

Automated tests:

- authored key, guarded ordinal, text fallback, ambiguity, locale mismatch;
- quickQuestion insertion segment;
- player-choice-derived automatic causality;
- observer failure passivity.

Runtime/manual acceptance:

- event `4000004`: naturally choose `Olivia_event5` (the tested "kiss" fixture if the localized content maps to it); verify stable key/index/options captured;
- historical interactive replay shows choices again and permits a different response;
- no new natural history is created by replay.

Stop/go:

- STOP if observation changes dialogue behavior or cannot bind response to a unique decision locator.

Dependencies: 7C persistence/session lifecycle; 4000004 translation fixture.

Primary risks: R1, R11, R12.

## Phase 7F - ExactHistoricalReplay and UX

Goal:

- implement `ExactHistoricalReplay=false`, GMCM, exact player-choice replay, capability/fallback UX, and replay-start config snapshot.

Allowed scope:

- config/GMCM strings and registration;
- per-replay `exactModeForThisReplay`;
- event answer index substitution at native callback;
- NPC Response object matching/substitution;
- record-level capability/fidelity UI;
- optional per-replay Interactive/Exact override.

Forbidden scope:

- fabricated legacy choices;
- silent choice substitution;
- Preview/StateInjector;
- synthetic click unless native callback route is proven unusable.

Automated tests:

- default false/config snapshot;
- exact-capable/content-only/legacy fallback;
- mismatch leaves choice interactive and degrades;
- all required choices applied before Exact claim;
- config changes mid-event affect only next replay.

Runtime/manual acceptance:

- natural 4000004 chooses the recorded response;
- default replay asks and permits another response;
- exact replay applies the recorded response through native handling;
- old/malformed/opaque record never claims Exact.

Stop/go:

- Exact only when every required decision matched and applied; otherwise explicit degradation or stop.

Dependencies: 7D automatic forcing and 7E choice capture.

Primary risks: R7, R11, R12, R13.

## Phase 7G - PreviewState / PreviewPlan / StateInjector

Goal:

- implement hypothetical sparse-state native evaluation only after historical semantics stabilize.

Allowed scope:

- separate `PreviewState`, `PreviewPlan`, scoped `StateInjector`;
- safe sparse overrides with `finally` restoration;
- preview-specific diagnostics and capability limits.

Forbidden scope:

- consuming HistoricalExecutionContext as a solver input;
- full save snapshots;
- planner/solver/CP1/Phase 8;
- claiming unsupported world-state simulation.

Automated tests:

- type/business separation from historical context;
- sparse override validation;
- nested/exception restoration;
- no natural history during preview;
- unsupported state fails closed.

Runtime/manual acceptance:

- preview representative friendship/eventsSeen/mail branches;
- verify all injected state restores on success, skip, transition, and exception;
- current/historical replay remains unchanged.

Stop/go:

- STOP on global leakage, incomplete restore, or need for full world snapshot.

Dependencies: completed 7F; explicit replay firewall approval.

Primary risks: R5, R8, R9, R12.

## Cross-phase hard stops

- No Phase 8 work.
- No Event engine rewrite.
- No global GSQ patch for historical replay.
- No full-save per-event history payload.
- No replay-created natural record.
- No fabricated legacy trace.
- No exact guarantee for opaque/missing custom handlers.
- No automatic advance from 7B into 7C; runtime hooks require human review/runtime fixture evidence.
