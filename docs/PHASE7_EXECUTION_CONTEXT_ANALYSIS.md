# Phase 7A Historical Execution Context Analysis

Date: 2026-09-04

## 1. Scope and environment

- Branch: `phase7/execution-context-analysis`
- Base: `b2bcbd724211bb23a94e8665283b92bd6d8e7150`
- Base branch: `origin/phase6/exact-script-event-launcher`
- Game: Stardew Valley `1.6.15.24356`
- Game assembly SHA-256: `7F1E5B8E58D2758B78570BA771BBEB03D33522F62188BF6C32EDF0CF626DEAEE`
- Decompiler: ILSpyCmd `8.2.0.7535`
- SVE content: `1.15.11`
- Phase 6 is closed. P6-1, P6-2, P6-3, and P6-5 are accepted; P6-4 remains deferred and non-blocking.
- This analysis does not start Phase 8 and does not reopen the Phase 6 exact-script launcher.

Evidence labels used below:

- `[REPO]`: Stardew Gallery source or tests.
- `[NATIVE-1.6.15]`: installed Stardew Valley 1.6.15 decompile.
- `[SVE]`: installed Stardew Valley Expanded content.
- `[RUNTIME-CONFIRMED]`: accepted manual test evidence supplied for this task.
- `[INFERENCE]`: conclusion from confirmed evidence, not independently runtime-tested.
- `[OPEN]`: needs a runtime probe or fixture.

## 2. Locked product semantics

Historical Interactive Replay (default):

```text
frozen historical content
+ recorded autonomous/automatic branch results
+ new explicit player choices
```

Historical Exact Replay:

```text
frozen historical content
+ recorded autonomous/automatic branch results
+ recorded explicit player choices
```

`ExactHistoricalReplay` is a future boolean GMCM option, defaults to `false`, and is snapshotted when replay starts. Phase 7A/7B does not implement it.

Exact means narrative-route exact, not frame-perfect playback. Skip, speed, mouse input, pause duration, animation tick timing, and viewport microtiming remain live presentation concerns.

## 3. Content is not execution outcome

The domain split remains:

```text
ObservedVariant            = frozen content version
HistoricalEventRecord      = one natural watched occurrence
HistoricalExecutionContext = decisions made during that occurrence
```

`ObservedVariantKey` must not include execution outcome. One content variant may be watched repeatedly with different outcomes.

### 3.1 Current production gap

`[REPO]` `Domain/HistoricalEventRecord.cs` already models one occurrence. SQLite v1 also has append-only `historical_event_records`. However, production `WatchedEventHistory.CommitPending` only calls `HistoryRepository.UpsertObservation`; it never calls `AddHistoricalEventRecord`. No production query reads historical occurrence rows.

Therefore current persistence can preserve content variants and first/last observation summaries, but cannot yet attach X/Y execution outcomes to individual watches. Phase 7C must close this gap; Phase 7B must not pretend it is already closed.

## 4. Sophia 195012 worked matrix

Fixture identity:

```text
AssetName = Data/Events/HaleyHouse
EventId   = 195012
```

Let:

- A = Sophia-present frozen content.
- B = Sophia-absent frozen content.
- X = marriage outcome.
- Y = estrangement outcome.

| Watched instance | Content owner | Outcome owner | Required persisted association |
| --- | --- | --- | --- |
| A + X | `ObservedVariant A` | record A-X execution context | record -> A + trace X |
| A + Y | `ObservedVariant A` | record A-Y execution context | record -> A + trace Y |
| B + X | `ObservedVariant B` | record B-X execution context | record -> B + trace X |
| B + Y | `ObservedVariant B` | record B-Y execution context | record -> B + trace Y |

`[RUNTIME-CONFIRMED]` Current code freezes A/B correctly, while the same historical A/B content followed the current-world X/Y outcome. This proves current code stores the content axis only.

`[SVE]` Important correction: the visible `195012` branch commands are `question fork1` -> `fork choseToExplain` and `question fork2` -> `fork lifestyleChoice`. `[NATIVE-1.6.15]` Those `fork` results are driven by `specialEventVariable1`, which the explicit answer toggles. They are player-choice-derived forks, not proof of an autonomous friendship branch.

`[OPEN]` The exact native decision boundary responsible for the runtime marriage/estrangement observation has not been identified. Phase 7C must record a no-op trace of `195012` before Phase 7D uses it as an automatic-branch acceptance fixture. This does not block the general domain contract because the contract represents autonomous, player-choice-derived, random-derived, and unsupported decisions explicitly.

## 5. Native Event runtime evidence

### 5.1 Dispatch

`[NATIVE-1.6.15]` `Event.CheckForNextCommand` splits the current command and synchronously calls virtual `Event.tryEventCommand`. That method resolves the currently registered `EventCommandDelegate`, creates an `EventContext`, and invokes the delegate. A handler may leave `CurrentCommand` unchanged and be retried on later ticks.

Public surfaces include `Event.RegisterCommand`, `TryGetEventCommandHandler`, `TryResolveCommandName`, `RegisterPrecondition`, `Event.GetCurrentCommand`, `ReplaceCurrentCommand`, `InsertNextCommand`, and `ReplaceAllCommands`.

This yields two practical observation boundaries:

1. `Event.tryEventCommand` pre/post observes every command, including currently registered custom handlers.
2. Known native handlers can be patched directly when their private state transition is needed.

Capture must be observer-only: exceptions are swallowed by Gallery tracing, and the native delegate always continues.

### 5.2 `fork`

`[NATIVE-1.6.15]` `Event.DefaultCommands.Fork(Event, string[], EventContext)` parses:

```text
fork [requiredId] newKey [isTranslationKey]
```

If only one key is supplied, the condition is `event.specialEventVariable1`. With `requiredId`, it checks local-player `mailReceived` OR `dialogueQuestionsAnswered`. It does not call GSQ.

- false -> `CurrentCommand++`.
- true -> load branch from a translation, festival field, or `Data/Events/<current location>`; call `Event.ParseCommands`; call `ReplaceAllCommands`; set `forked = true`.

`ReplaceAllCommands` replaces the whole array and resets `CurrentCommand = 0`. There is no branch stack or return to the parent segment.

### 5.3 `switchEvent`

`[NATIVE-1.6.15]` `SwitchEvent` unconditionally loads the target festival field or location event key, parses it, calls `ReplaceAllCommands`, and sets `eventSwitched = true`. It is a segment transition, not a decision. The selected target and resulting segment still need observation so later locators are anchored correctly.

### 5.4 `question` and `quickQuestion`

`[NATIVE-1.6.15]` Both commands create `Response` objects whose native keys are generated ordinals (`"0"`, `"1"`, ...). Event `question` passes its authored `questionKey`; `quickQuestion` always passes `quickQuestion`.

The UI callback is asynchronous:

```text
Event command creates DialogueBox
-> player selects an index
-> DialogueBox.receiveLeftClick
-> Event.answerDialogue(lastQuestionKey, selectedIndex)
-> DialogueBox outro/pause
-> closeDialogue increments CurrentCommand
```

`answerDialogue` stores `previousAnswerChoice`. `forkN` toggles `specialEventVariable1` when index N is selected. `quickQuestion` inserts the selected command fragment after the current command. Calling `answerDialogue` alone would skip the normal DialogueBox close/progression lifecycle; exact replay should alter the selected answer at the native callback boundary and let the UI lifecycle finish normally.

### 5.5 Translation-backed `$r` dialogue

`[NATIVE-1.6.15]` `Dialogue.parseDialogueString` parses `$r <responseKey> <friendship> <id>#<text>` into `NPCDialogueResponse`. `Dialogue.chooseResponse(Response)` matches the authored `responseKey`, applies friendship and seen-response side effects, then loads the response-key dialogue.

`[SVE]` Event `4000004` uses authored keys such as `Olivia_event5` through `Olivia_event8`; these are more stable than translated text. This is the future explicit-choice fixture.

### 5.6 GSQ and RNG

`[NATIVE-1.6.15]` Event-start `G`/`GameStateQuery` preconditions call GSQ before an event is selected. Exact replay bypasses event selection, so these are already frozen into the selected content path.

`fork` and `switchEvent` do not call GSQ. Dialogue `$query` calls GSQ while parsing dialogue. Dialogue `$c` consumes `Game1.random`; `||` uses a deterministic week index. Event random preconditions and GSQ `RANDOM` also use RNG, but they occur during selection rather than exact launch.

Historical narrative RNG should record the resulting decision, not global RNG state. Presentation-only randomness stays live.

**HistoricalRandomTrace needed: ONLY FOR SPECIFIC CASES, represented inside `AutomaticDecisionTrace`; no separate random-trace type for the MVP.**

## 6. Command capability matrix

Classes: A presentation, B automatic deterministic, C automatic state-dependent branch, D explicit choice, E persistent mutation, F script replacement/transition, G RNG-sensitive, H opaque.

| Command/path | Class | Trace required | Exact interception | Preview dependency | Safety | Evidence |
| --- | --- | --- | --- | --- | --- | --- |
| `fork` | C/D/F | yes for autonomous result; verify choice-derived result | yes | event/player state | high | `[NATIVE-1.6.15]` |
| `switchEvent` | B/F | transition only | no branch override | event assets | medium | `[NATIVE-1.6.15]` |
| `question` | D | player choice | exact mode only | none | high | `[NATIVE-1.6.15]` |
| `quickQuestion` | D/F | choice + inserted segment | exact mode only | none | high | `[NATIVE-1.6.15]` |
| `splitSpeak` | A, choice-derived | no new decision | no | prior answer | low | `[NATIVE-1.6.15]` |
| `$r` NPC response | D/E | player choice | exact mode only | dialogue asset | high | `[NATIVE-1.6.15]` `[SVE]` |
| `speak`, `textAboveHead` | A | no | no | content/locale | low | `[NATIVE-1.6.15]` |
| `move`, `advancedMove`, actor pathing | A/B | only if route-relevant custom behavior | normally no | map/collision | medium | `[NATIVE-1.6.15]` |
| `warp`, `changeLocation` | A/B/F | segment/location observation | no | map/location | high | `[NATIVE-1.6.15]` |
| `faceDirection`, `emote`, `pause` | A | no | no | none | low | `[NATIVE-1.6.15]` |
| mail commands | E, later C input | record later branch result, not mutation | no | local/host mail | high | `[NATIVE-1.6.15]` |
| item/money commands | E | no route trace unless later queried | no | inventory/money | high | `[NATIVE-1.6.15]` |
| `friendship` | E | no route trace unless later queried | no | friendship | high | `[NATIVE-1.6.15]` |
| conversation topics | E | no route trace unless later queried | no | player dialogue state | medium | `[NATIVE-1.6.15]` |
| `eventSeen` / event completion | E | lifecycle only | no | eventsSeen | high | `[NATIVE-1.6.15]` |
| fades, viewport, `skippable` | A | no | no | location/presentation | low-medium | `[NATIVE-1.6.15]` |
| `end` | E/F | lifecycle outcome | no | broad world state | critical | `[NATIVE-1.6.15]` |
| `ReplaceAllCommands` | F | segment transition | native primitive retained | content | high | `[NATIVE-1.6.15]` |
| dialogue `$query` / `$c` | C/G | selected route if narrative-relevant | scoped parser hook if supported | GSQ/RNG | high | `[NATIVE-1.6.15]` |
| custom command | H | opaque marker unless adapter exists | handler-specific | arbitrary | critical | `[REPO]` `[OPEN]` |

`pathActor` was not found as a 1.6.15 default handler; until a registered handler is observed it is H/unsupported rather than assumed native.

## 7. Determinism analysis

| Source | Classification | Historical handling |
| --- | --- | --- |
| Frozen root/nested scripts/translations | deterministic from frozen content | `HistoricalPlaybackBundle` |
| `fork` on event-local/mail/answer state | recorded decision | replay autonomous results; choice-derived follows choice |
| `switchEvent` | deterministic transition | verify frozen target and segment |
| weather/date/time/friendship/relationship/eventsSeen/mail/inventory | live state may differ | record decision result, avoid broad historical state injection |
| state mutated earlier in the event | start snapshot is stale | record when each decision occurs |
| narrative RNG | recorded automatic result | no global RNG snapshot |
| presentation RNG/movement timing | live | outside exact narrative guarantee |
| locale | frozen content for historical replay | response text is diagnostic fallback only |
| multiplayer/remote player state | unsupported for MVP | no exact claim |
| opaque custom handler | unknown | no exact claim without adapter |

**Is event-start state snapshot alone sufficient? NO.** Commands and choices can mutate `specialEventVariable1`, mail, answers, friendship, inventory, and command lists before later decisions. Reconstructing at event end is also unsafe. Actual result must be recorded at each decision boundary.

## 8. Capture architecture

```text
Native natural Event
-> event-scoped Decision Observer
-> ExecutionTraceBuilder
-> HistoricalEventRecord
-> Persistence
```

Recommended hybrid observers:

- `Event.tryEventCommand` prefix/postfix: capture command text, current segment, pre/post command array, current index, and known automatic result. This includes currently registered custom handlers and detects opaque replacements.
- `Event.answerDialogue` postfix: record event-level question index after native mutation.
- `Dialogue.chooseResponse` prefix/postfix: record authored `$r` response key and side-effect path.
- event lifecycle observer: begin only for natural `Game1.CurrentEvent`; finalize on confirmed natural completion; mark partial on skip/interruption/quit/exception/overflow.

Capture failure must be isolated in observer code and never decide whether the native handler runs. The natural event remains authoritative.

### 8.1 Lifecycle and completion

```text
natural event starts
-> trace session begins
-> decisions recorded as they occur
-> complete / skip / abort / title / exception
-> immutable trace finalized
-> variant + record + context committed
```

Persisted completion states are `EmptyComplete`, `Complete`, and `Partial`. `Missing` and `Invalid` are read-model states:

- Missing: legacy or no child context row.
- EmptyComplete: capture coverage complete; event completed; no decisions.
- Complete: capture coverage complete; event completed; trace has decisions.
- Partial: started but skipped/interrupted/failed/overflowed.
- Invalid: row exists but version/binding/shape/payload is invalid.

Skip is not a narrative choice. Replay-time observation never creates a natural record, and replay never mutates a persisted natural trace.

## 9. Decision identity

### 9.1 ScriptSegmentIdentity

Final model:

```text
ScriptSegmentIdentity
  Kind
  PathHash
  CommandListHash
  Source { Kind, AssetName?, Key? }
  EnteredBy { ParentPathHash, command site, transition kind, selected target? }?
```

Kinds: Root, ForkReplacement, SwitchEventReplacement, ChoiceInsertion, DynamicReplacement.

- Root path binds to full historical `PlaybackHash` and root command-list hash.
- Asset/translation child includes normalized source + key + parsed command-list hash.
- Dynamic child includes parent path + transition command site + selected result + resulting command-list hash.

No recursive object graph is persisted; `EnteredBy` stores parent path and entry site. This handles destructive replacement while avoiding recursive serialization.

### 9.2 DecisionLocator

```text
DecisionLocator
  Segment
  DecisionKind
  CommandHash
  CommandOrdinal
  Occurrence
```

`CommandHash` is full SHA-256 of exact parsed command text. Because replay content is frozen, cross-version canonicalization is unnecessary. Ordinal distinguishes duplicate identical commands in one segment; occurrence distinguishes repeated execution. Ordinal is a hint, never the sole identity.

### 9.3 Ordering

Use separate automatic/player arrays with one global monotonically increasing `Sequence`. Replay merges by sequence and advances one cursor. It never searches ahead by EventId or locator. Sequence plus locator detects drift and repeated sites.

## 10. Automatic versus player-derived routing

`AutomaticDecision` records causality:

- Autonomous: world/event state selected a route without a player choice causing it.
- PlayerChoiceDerived: native automatic command reflects an earlier explicit choice.
- RandomDerived: narrative RNG selected a route.
- Unknown: observed route without proven semantics; prevents fidelity claim.

Default historical replay forces Autonomous and RandomDerived recorded results. It does **not** independently force PlayerChoiceDerived results after the user chooses differently; the new choice must control the route. Exact mode applies the recorded choice and verifies the derived decision.

This refinement is required to satisfy both locked rules: automatic historical decisions are preserved, while interactive replay choices remain meaningful.

## 11. Historical replay architecture

```text
HistoricalEventRecord
-> ObservedVariant (frozen HistoricalPlaybackBundle)
-> HistoricalExecutionContext
-> HistoricalReplaySession
-> ReplayCoordinator
-> EventLauncher
-> native Event
        ^
   Decision Interceptor
```

`HistoricalReplaySession` owns record/context binding, exact-mode snapshot, active Event reference, trace cursor, current segment, applied/missed decision counts, fidelity, and mismatch state. It is event-scoped and cleared in every success/failure/restore path.

`ReplayCoordinator` remains responsible for backup, snapshot, scheduling, lifecycle, and restore. It activates/deactivates the session and assets but does not parse decisions.

`EventLauncher` stays unchanged: `EventPlayback -> exact Event -> schedule`. It does not know history, traces, branches, or choices.

`HistoricalReplayAssets` remains content only. Execution decisions remain in `HistoricalExecutionContext`.

### 11.1 Fork override strategy

Do not patch GSQ. Recommended runtime strategy for known native `fork`:

1. Scope by active replay session, exact `Event` reference, segment, locator, and expected sequence.
2. At `Event.tryEventCommand`, verify the registered handler is known-compatible.
3. Temporarily set only the synchronous input read by native `fork`:
   - no required ID: `specialEventVariable1`;
   - required ID: membership in local `mailReceived`/`dialogueQuestionsAnswered`.
4. Let the currently registered native handler perform parsing/loading/`ReplaceAllCommands`/flags.
5. Restore temporary input in postfix/finalizer immediately.
6. Observe and verify the resulting command-list hash/segment.

This preserves native branch mechanics and side effects, avoids global GSQ changes, and does not rewrite the Event engine. If the handler is custom/changed or the result does not match, do not claim outcome fidelity.

Alternatives considered:

- Direct condition-result override inside native handler: narrow but version-sensitive/transpiler-heavy.
- Reimplement branch loading then call `ReplaceAllCommands`: feasible fallback, but duplicates native source selection/error behavior.
- Broad state injection for the whole replay: rejected; leaks and can affect unrelated systems.

### 11.2 Player-choice capture and exact replay

Event `question`/`quickQuestion` identity priority:

1. locator + exact option-set hash + selected index;
2. same-locale unique selected-text hash only as diagnostic fallback.

Generated response keys are not treated as authored stable keys.

NPC `$r` identity priority:

1. unique authored response key;
2. exact option-set hash + index;
3. same-locale unique text hash;
4. mismatch.

Default mode does not intercept explicit choices. Exact mode changes the answer only when the native question UI/callback is ready:

- event question: rewrite `answerChoice` in a scoped `Event.answerDialogue` prefix, retaining normal DialogueBox close/progression;
- NPC `$r`: replace the `Response` argument passed into `Dialogue.chooseResponse` with the uniquely matched current response object, retaining friendship/seen-response/dialogue side effects.

Synthetic mouse clicks and bypassing the UI are rejected unless later runtime evidence proves the callback route unusable.

## 12. Mismatch, capability, and fidelity

Stored capability:

- ContentOnly: Missing, Invalid, Partial, binding mismatch, incomplete automatic coverage, unknown/opaque behavior.
- OutcomeAware: bound complete automatic coverage, no unsupported/unknown route decisions.
- ExactCapable: OutcomeAware plus complete player-choice coverage. Zero-choice and EmptyComplete traces can be ExactCapable only when choice instrumentation coverage is explicitly complete.

Runtime fidelity:

- Exact: all required automatic results and exact-mode choices matched and applied.
- AutomaticBranchesPreserved: all autonomous/random results matched; choices remained interactive.
- InteractiveContentOnly: frozen content replay, no trusted outcome context.
- Degraded: a trusted context started but a locator/result/choice/handler mismatch occurred.
- Failed: replay could not continue safely.

Policies:

- Partial traces are all-or-nothing for OutcomeAware claims: replay content-only; do not apply a prefix of decisions while claiming preserved outcome.
- Invalid/future-schema contexts are isolated to the record and replay content-only.
- Automatic mismatch: continue native behavior only with explicit `Degraded`, or stop if continuation is unsafe; never silently claim historical outcome.
- Exact choice mismatch: show current choice UI and degrade; never select a different option.
- Missing custom handler: `MissingCommandHandler`; stop or degrade explicitly.
- GMCM ON is not capability evidence.

## 13. Persistence recommendation

Select a separate v2 child table:

```sql
CREATE TABLE historical_execution_contexts (
    context_pk         INTEGER PRIMARY KEY,
    record_fk          INTEGER NOT NULL UNIQUE
                       REFERENCES historical_event_records(record_pk) ON DELETE CASCADE,
    schema_version     INTEGER NOT NULL,
    completion_status TEXT NOT NULL,
    execution_json     TEXT NOT NULL
);
```

Why separate rather than columns on `historical_event_records`:

- no row means Missing without nullable-column consistency rules;
- malformed JSON cannot prevent the occurrence/variant from loading;
- payload can load lazily for selected replay;
- execution ownership remains one-to-one and independently extensible.

Use one versioned JSON payload, not one row per decision. The trace is small, replay-oriented, and not queried analytically in the MVP. Independent payload `SchemaVersion` remains required even when SQLite `user_version` becomes 2.

Transactional v1 -> v2 plan:

1. reject future DB versions without writes;
2. begin transaction;
3. create child table and indexes;
4. validate schema/foreign keys;
5. set `user_version = 2`;
6. commit, otherwise rollback;
7. add no context rows for old records (they remain Missing).

Natural persistence should serialize/validate first, then store variant/summary/record. A context insertion failure leaves a truthful content-only record (savepoint or separately isolated insert), logs diagnostics, and never changes gameplay.

Protection limits: initial proposal `MaxTraceEntries = 512`, `MaxExecutionJsonBytes = 256 KiB`, `MaxSegmentDepth = 64`, JSON depth 64. Overflow stops capture, marks Partial/TraceLimitExceeded, and leaves gameplay running.

Expected size: typical hundreds of bytes to 5 KiB; complex events tens of KiB; hard cap 256 KiB. CPU/memory are O(decisions), with one final serialization and lazy reads.

## 14. Side-effect safety boundary

| State | Captured/restored today | Future need |
| --- | --- | --- |
| eventsSeen / seen-this-location | yes/yes | host/team scope |
| mail received/tomorrow/mailbox | yes/yes | host/team broadcasts |
| friendship | selected fields | full native/mod fields audit |
| relationship | status subset | wedding/proposer/roommate/mod fields |
| inventory | cloned item + stack | custom item fidelity tests |
| money/health/stamina | yes/yes | player ownership |
| quests | shallow object list | deep mutation audit |
| recipes/experience/professions | yes/yes | low risk audit |
| achievements/stats | no/no | critical before broad claims |
| world state/special orders | no/no | critical, often team-scoped |
| NPC/map/location state | player position only | broad expansion required |
| conversation topics/answers | yes/yes | scope validation |

This is a replay firewall concern, not an execution-trace reason to save a full world snapshot. Phase 7 does not expand it.

## 15. Preview separation

```text
HistoricalExecutionContext = what actually happened
PreviewState               = hypothetical state to evaluate
```

They are separate types and business meanings. A historical trace never feeds the preview solver; PreviewState never determines a recorded historical outcome.

```text
ConditionIR
-> PreviewPlan
-> PreviewState
-> StateInjector
-> EventLauncher
-> native Event
```

Preview sparse override classification:

| Override | MVP classification | Reason |
| --- | --- | --- |
| friendship, local eventsSeen, local mail | safe-mutable candidate | sparse and restorable after full field audit |
| season/day/year/time | launch-only candidate | global caches and handlers also read them |
| weather, relationship | analyze-only | location/network or incomplete native fields |
| world state | unsupported/unknown | broad shared/network state |

If recorded branch decisions suffice, historical replay does not need broad historical state injection. State can still affect downstream presentation or side effects; those are verified by native execution and replay restore, not by feeding PreviewState into historical replay.

## 16. Multiplayer, locale, and custom code

Multiplayer MVP: single-player only for OutcomeAware/Exact guarantees. Current replay already rejects multiplayer. Preserve profile ownership `(farm ID, local player ID)`. Farmhand/host-mediated choices, remote state, broadcast commands, and team mutations remain unsupported until runtime-tested.

Locale: historical content binds through PlaybackHash and frozen translations. Authored response key is preferred. Event generated ordinal needs option-set hash. Translated text is never the sole cross-locale identity; text fallback is same-locale only and degrades on ambiguity.

Custom commands: frozen content does not freeze executable mod code. A custom handler may branch, show choices, mutate state, or disappear after an update. Known native decisions can be exact; opaque custom behavior prevents an exact guarantee unless an explicit adapter supplies observation, replay, side-effect, and provenance semantics.

## 17. User-facing semantics

| Behavior | Historical Interactive | Historical Exact |
| --- | --- | --- |
| Historical content | frozen | frozen |
| Autonomous/random branches | recorded result | recorded result |
| Player-choice-derived branch | follows new choice | follows recorded choice |
| Explicit choices | ask again | recorded choice |
| Replay speed / skip | current | current |
| Frame/timing details | native/live | native/live |
| Unsupported custom logic | degraded/content-only | unavailable/degraded |

Preview comparison:

| Mode | Content | Decisions | State |
| --- | --- | --- | --- |
| Historical Interactive | frozen | recorded automatic + new choices | current world except replay firewall |
| Historical Exact | frozen | recorded automatic + recorded choices | current world except replay firewall |
| Preview | selected/current | native evaluation | hypothetical sparse PreviewState |

## 18. Runtime responsibility boundaries

- `ReplayCoordinator`: lifecycle, backup, snapshot, scheduling, restore, failure cleanup, session activate/clear.
- `EventLauncher`: exact Event construction and scheduling only; unchanged.
- `HistoricalReplayAssets`: frozen root dependencies/translations only.
- `HistoricalExecutionContext`: immutable natural decisions only.
- `HistoricalReplaySession`: ephemeral replay cursor, mode, matching, fidelity.
- `DecisionObserver`: passive natural capture.
- `DecisionInterceptor`: active historical replay only; exact event/locator scoped.
- `PreviewState`/`StateInjector`: separate future 7G path.

## 19. Future UI/UX

Hierarchy should become Event -> Content Variant -> Watched Instances. Generic metadata may show watched time, automatic decision count, player choice count, completion, and capability. Do not hard-code Sophia outcome labels.

Legacy records remain historical content replayable but exact reproduction unavailable. If global exact mode is on and a record lacks capability, the UI must state the fallback; it must not silently label it Exact. A per-replay Interactive/Exact override is appropriate for 7F after runtime fidelity exists.

## 20. Future test strategy

Phase 7B pure tests are specified in `PHASE7_EXECUTION_TRACE_SCHEMA.md` and include roundtrip, completion distinctions, locators, duplicate commands, ordering, binding, response matching, malformed/future payload, capabilities, and full hashes.

Runtime fixtures:

- Sophia `195012`: player-choice-derived fork and runtime X/Y source probe.
- Event `4000004`: authored `$r` explicit choices.
- SVE Alex `switchEvent AlexFourHeart`: nested switch transition.
- Duplicate identical decision commands: synthetic content fixture.
- Translation-backed fork/choice: frozen translation fixture.
- Real custom command: deferred until found.
- Autonomous `fork <requiredId> <newKey>`: required before Phase 7D automatic-branch acceptance.

## 21. Risk register

| ID | Risk | Likelihood / impact | Mitigation | Blocking phase | Evidence |
| --- | --- | --- | --- | --- | --- |
| R1 | Harmony/native hook sensitivity | H/H | exact signatures, startup verification, passive probe, fail closed | 7C+ | `[REPO]` `[NATIVE-1.6.15]` |
| R2 | fork replacement locator drift | M/H | parent path + command hash/ordinal/occurrence + result hash | 7C/7D | `[NATIVE-1.6.15]` |
| R3 | nested segment identity | M/H | source + parent transition + command-list hash | 7B/7D | `[REPO]` `[NATIVE-1.6.15]` |
| R4 | custom command opacity | H/H | opaque marker; no exact claim without adapter | per record | `[REPO]` |
| R5 | multiplayer scope | M/H | single-player fidelity MVP | multiplayer | `[REPO]` `[OPEN]` |
| R6 | partial/corrupt trace | M/H | completion states, child row, caps, isolated parse | 7C | `[INFERENCE]` |
| R7 | false exact guarantee | M/critical | coverage + binding + applied-decision accounting | 7D/7F | `[RUNTIME-CONFIRMED]` |
| R8 | replay side-effect restore gaps | H/critical | existing backup/firewall, audit before new fixtures | 7D+ | `[REPO]` |
| R9 | CP invalidation timing | M/H | activate frozen assets before launch, runtime hash checks | 7C/7D | `[REPO]` |
| R10 | SQLite migration | M/H | transactional v1->v2, no fabricated rows, rollback tests | 7C | `[REPO]` |
| R11 | response identity instability | H/H | authored key; guarded ordinal; same-locale text last | 7E/7F | `[NATIVE-1.6.15]` |
| R12 | replay-session leakage | M/critical | exact Event ref, locator, sequence, finally cleanup | 7D+ | `[REPO]` |
| R13 | mod update/missing handler | H/H | handler provenance, explicit degradation/failure | per record | `[INFERENCE]` |
| R14 | oversized trace | L/H | 512/256KiB/depth caps; Partial on overflow | 7C | `[INFERENCE]` |

## 22. Open questions

| Question | Severity | State | Required evidence |
| --- | --- | --- | --- |
| What exact decision causes runtime marriage/estrangement for `195012`? | BLOCKING for Sophia 7D fixture, not 7B | `[OPEN]` | no-op trace of command/choice/fork state |
| Does scoped dispatcher interception coexist with real command wrappers? | BLOCKING for 7C implementation | `[OPEN]` | handler provenance + wrapper-chain runtime probe |
| Does the proposed fork input injection preserve all 1.6.15 side effects? | BLOCKING for 7D | `[INFERENCE]` | A/B runtime branch forcing probe |
| Can NPC `$r` exact replay safely replace the Response argument? | BLOCKING for 7F | `[INFERENCE]` | `4000004` runtime probe |
| Are switchEvent segment hashes stable through location transitions? | NON-BLOCKING for 7B | `[INFERENCE]` | SVE Alex/Morgan runtime trace |
| Which custom handlers can expose decisions? | NON-BLOCKING | `[OPEN]` | loaded handler inventory/adapters |
| Multiplayer decision owner/scope? | NON-BLOCKING for single-player MVP | `[OPEN]` | host/farmhand tests |
| Which RNG paths affect narrative in real loaded events? | NON-BLOCKING | `[OPEN]` | trace inventory |

## 23. Architecture gate

The P0 domain questions are resolved sufficiently for a pure Phase 7B contract:

- Decision boundaries are identifiable from installed native code.
- Destructive command replacement is represented by segment transitions.
- Locator does not rely solely on command index.
- Event and NPC choices use separate stable identity strategies.
- PlaybackHash prevents trace reuse across content.
- Runtime hook uncertainty is isolated behind observer/interceptor interfaces and does not alter the pure persisted contract.
- No Event-engine rewrite or global GSQ override is required.

Unresolved runtime questions block 7C/7D/7F fixtures, not the 7B immutable schema.

```text
ARCHITECTURE GATE

Unresolved blockers:
none for Phase 7B pure domain/serialization

Resolved blockers:
- fork can be observed/forced at an Event-scoped native boundary without global GSQ
- destructive replacement has a stable parent/child segment identity
- event question and NPC response identity are representable without translated text as primary key
- trace binds to exact PlaybackHash, not EventId
- partial/invalid/custom behavior cannot create a false fidelity claim

Phase 7B safe to start:
YES
```

## 24. Hard invariants

```text
I1  Event selection != Event launch
I2  ObservedVariant != HistoricalEventRecord
I3  Content version != Execution outcome
I4  Autonomous/random historical branches replay recorded result
I5  Explicit player choices ask again by default
I6  ExactHistoricalReplay replays recorded explicit choices
I7  ExactHistoricalReplay default = false
I8  HistoricalExecutionContext != PreviewState
I9  No full-save historical snapshots
I10 Replay never creates natural history
I11 Capture failure never alters natural gameplay
I12 Unsupported opaque custom behavior means no false exact guarantee
I13 Old records never receive fabricated decisions
I14 Trace binds to exact historical content / PlaybackHash
I15 Exact means narrative-route exact, not frame-perfect video replay
I16 Player-choice-derived branches follow the replay-time choice in interactive mode
I17 A persisted natural trace is immutable
I18 Runtime Exact is earned only after every required decision matched and applied
I19 Missing, EmptyComplete, Complete, Partial, and Invalid are distinct
I20 Historical decision overrides are active-Event and active-session scoped
```

## 25. Required conclusions

```text
Historical automatic branch replay?: YES
Historical explicit player choice recorded?: YES
Historical explicit player choice replay by default?: NO
ExactHistoricalReplay option?: YES
ExactHistoricalReplay default?: OFF
Full world-state snapshot?: NO
Branch decision trace?: YES
Player choice trace?: YES
HistoricalExecutionContext vs PreviewState?: SEPARATE TYPES
HistoricalEventRecord can have multiple outcomes per ObservedVariant?: YES

HistoricalRandomTrace needed?: ONLY FOR SPECIFIC CASES, inside AutomaticDecision
Decision locator strategy: segment path + exact command hash + ordinal + occurrence + sequence
Script segment identity strategy: root/content binding or parent transition + source + command-list hash
Trace ordering model: separate typed arrays with one global sequence
Capture strategy: passive boundary observer; record actual result when it occurs
Replay strategy: session-scoped matching; native Event engine remains authoritative
Fork override strategy: narrowly inject synchronous native fork input, run native handler, verify result
Player-choice capture strategy: Event.answerDialogue + Dialogue.chooseResponse observers
Player-choice exact-replay strategy: rewrite native callback argument; preserve UI lifecycle/side effects
Old-record degradation policy: ContentOnly, historical content replay remains available
Partial-trace policy: content-only; no OutcomeAware claim
Invalid-trace policy: isolate record payload; content-only
Exact mismatch policy: do not substitute; show choice/degrade or stop safely
Exact capability rule: valid binding + complete coverage + no opaque/unknown + all required choices
Custom-command exact guarantee: none without a proven adapter
Multiplayer MVP policy: single-player fidelity only
Locale policy: authored key first; guarded ordinal; same-locale text fallback only
Persistence schema recommendation: one-to-one historical_execution_contexts child table
JSON vs normalized decisions: versioned JSON
Trace versioning: independent payload SchemaVersion = 1
Storage estimate: typical <5 KiB; complex tens of KiB; hard proposal 256 KiB
Performance expectation: O(decisions), not O(ticks x state)
```
