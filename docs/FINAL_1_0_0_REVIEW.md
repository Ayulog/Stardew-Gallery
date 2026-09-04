# Final 1.0.0 rework code review (current-state canonical replay)

Scope: uncommitted/committed rework on branch `phase-final/1.0.0` (HEAD `e9a289b`,
"refactor: make current-state replay canonical with preview state"). Review is read-only.

Validation run: `dotnet build -c Release` clean (0 warnings/errors),
`dotnet run --project Checks/StardewGallery.Checks.csproj -c Release` -> "Stardew Gallery checks passed.",
`git diff --check` clean.

---

## Findings (ordered by severity)

### 1. Medium — exception during `CaptureAndApply` leaks already-applied overrides (breaks "success and failure" restore)

`Preview/StateInjector.cs:35-40`

```csharp
internal static PreviewInjectionScope Apply(IPreviewStateAccessor accessor, PreviewState state)
{
    PreviewInjectionScope scope = new(accessor);
    scope.CaptureAndApply(state);   // if this throws, |scope| is never returned
    return scope;
}
```

`PreviewInjectionScope.Apply` builds the restore list by mutating the accessor, adding a restore
action BEFORE each mutation (`StateInjector.cs:44-94`). If any accessor setter throws part-way
(e.g. `CaptureAndApply` at line 50/56/62/69/78/86), `Apply` throws and **never returns the scope**,
so the partial restore list built so far is discarded.

Call site `ReplayCoordinator.cs:101`:

```csharp
previewScope = PreviewInjectionScope.Apply(new RuntimePreviewStateAccessor(), state);
```

The assignment never completes on a throw; `previewScope` stays `null`. The `catch` runs
`Restore(error)` (`ReplayCoordinator.cs:123`), but `Clear()` (`ReplayCoordinator.cs:266`)
only calls `previewScope?.Dispose()` — `null` here — so the partially-applied
`eventsSeen` / `mail` / `friendship` / `time` overrides on the live `Game1.player` are
**never rolled back**. They persist in memory for the rest of the session, and become save-visible
once `IsActive` returns `false` (a normal save is no longer blocked).

This directly contradicts the class doc-comment intent: "restores the originals on Dispose
(idempotent, success and failure)." Reachability is low (Runtime accessor setters are simple
assignments) but it is the one case where the "failure" guarantee genuinely fails; see finding 2
for the concrete throw trigger.

Recommended (not applied — review only): make `Apply` dispose the scope in a `try`/`catch`
before rethrowing, or set `previewScope` before applying so `Clear()` always disposes it.

### 2. Low — `RuntimePreviewStateAccessor` does not null-guard player collections (asymmetry with the reader)

`Preview/RuntimePreviewState.cs:61-77`

```csharp
public bool HasEventSeen(string id) => Game1.player.eventsSeen.Contains(id);
public bool HasMail(string id) => Game1.player.mailReceived.Contains(id);
public void SetEventSeen(...) { ... Add/Remove ... }
public void SetMail(...)
public void SetFriendship(string npc, int points) { ... Game1.player.friendshipData.TryGetValue(...) ... }
```

`RuntimeStateReader.Capture` defensively null-checks these (`RuntimePreviewState.cs:31-38`):
`player.eventsSeen is null ? null : ...`, and guards `friendshipData.Keys`. The accessor does not.
If `player.eventsSeen` / `player.mailReceived` / `player.friendshipData` is `null` at preview
time, an NRE here is exactly the throw that triggers finding 1's leak. Preview only runs while a
loaded save is active, so this is unlikely; but the asymmetry is real and makes the failure path
in finding 1 reachable in principle. Low severity on its own.

### 3. Low / Informational — a replayed/previewed event still sets its own `eventsSeen` / mail in memory (not restored)

Preview/replay run a real Stardew `Event`; on completion `markEventSeen` etc. mutate the live
`Game1.player` for the event's own id. `PreviewState.EventsSeen`/`Mail` only contain ids sourced
from the parsed *conditions* (`PreviewPlanner.BuildSuggestion`, `PreviewPlanner.cs:171-218`), so
the replay's own id is not in the restore list. `ReplaySnapshot.RestorePlayer()`/`Clear()` do not
reset `eventsSeen`/`mail`. This is pre-existing canonical-replay behavior (not introduced by this
rework), and it is distinct from *natural-history* capture (which is suppressed, see below), so it
is noted for completeness rather than as a regression. Applies equally to non-preview replay.

---

## Confirmations requested (no material findings)

- **No historical replay entry point remains product-facing.** `EventPlayback.ForHistorical`
  (`EventPlayback.cs:16`) is referenced only by `Checks/Program.cs:1015`; no product code calls it.
  `ReplayCoordinator.TryStart`/`TryStartPreview` both use `EventPlayback.ForCurrent(entry.Resolved)`
  (`ReplayCoordinator.cs:47,94`) and the single `EventLauncher.TryLaunch(EventPlayback)`
  (`EventLauncher.cs:26`, `ReplayCoordinator.cs:61,108`). `GalleryMenu.watchedVersions` is stored
  and passed through (`GalleryMenu.cs:29,79,181,382`) but never used to launch; all UI paths go
  through `ModEntry.StartReplay` → `replay.TryStart`/`TryStartPreview` (`ModEntry.cs:334-349`).
  No `WatchedEventSnapshot`/`HistoricalPlaybackBundle` is consumed by any launch path.

- **No natural history is created by preview/replay.** Every capture hook in
  `ExecutionTraceObserver` gates on `NaturalExecutionTraceRules.CanObserve(replayActive(), ...)`
  (`ExecutionTraceObserver.cs:57,122,138,177`) with `replayActive = () => replay.IsActive`
  (`ModEntry.cs:45`). `ReplayCoordinator.IsActive` is true from the moment snapshot/previewScope is
  set (`ReplayCoordinator.cs:28`) and stays true through restore until `Clear()`. For a preview,
  `previewScope` and `snapshot` are set before the event launches (`ReplayCoordinator.cs:100-101`),
  so the previewed event is excluded. `NaturalExecutionTraceRules.CanObserve(replayActive:true,…)`
  is `false` (verified by check line 1280).

- **PreviewPlanner capability/override logic is consistent.** `ComputeCapability`
  (`PreviewPlanner.cs:142-169`) correctly returns `DirectReplay` only when no missing/unknown;
  `AnalysisOnly` when unknown-only; `PreviewSupported` when all missing are restorable; and
  `PreviewPartiallySupported` when any missing is unrestorable (weather / relationship / negated /
  opaque). `TryBuildOverride` (`PreviewPlanner.cs:220-252`) rejects `Negated` and any
  non-restorable kind at the top, and `BuildSuggestion` (`PreviewPlanner.cs:171-218`) only sets
  `Year`/`Time` for non-negated leaves — mutually consistent. `Restorable` whitelist
  (`PreviewPlanner.cs:49-58`) excludes weather/relationship/world-state, so no unsafe injection can
  be requested. Planner never mutates game state. Covered by checks F1-1..F1-8.

- **`PreviewInjectionScope` apply/restore/idempotency for eventsSeen/mail is correct on the
  non-throwing path.** `CaptureAndApply` only proceeds to mutate when `before` is `false`
  (`StateInjector.cs:80-84, 89-93`), so the captured restore value is `false` and restore correctly
  *removes* the injected id (reverting to the original presence). Untouched slots are never entered
  (`StateInjector.cs:44-62` only touches slots that differ, and `if (before) continue` skips
  already-present ids). Dispose is guarded by `disposed` and runs restores in reverse
  (`StateInjector.cs:100-117`); a second Dispose is a no-op (verified by check F1-11). `try/catch`
  inside Dispose keeps restore going for remaining slots (`StateInjector.cs:107-114`). Caveat is
  only the throw-during-`Apply` path in finding 1.

- **`IPreviewStateAccessor` contract is honored.** The exposed set
  (`StateInjector.cs:8-20`) is exactly the restorable kinds (season/day/year/time/friendship/
  eventsSeen/mail); weather/dating/spouse/roommate/world-state are deliberately absent, so
  `RuntimePreviewStateAccessor` cannot be used to inject them. `PreviewState` is sparse and never a
  full snapshot (`PreviewPlan.cs:7-20`).

- **`ReplaySaveGuard` correctly gates on `previewScope`.** `BeforeSave` short-circuits on
  `replay.IsActive` (`ReplaySaveGuard.cs:26`), and `IsActive` includes `previewScope`
  (`ReplayCoordinator.cs:28`). Save is blocked for the full preview/replay/restore window, so
  temporary state can never be persisted (finding 1's leak is the only wedge, via the throw path).
  The accessor also never saves, and `TryStartPreview` never calls a save routine.

- **StateInjector invariants (I1–I13 framing).** The rework upholds the intended invariants on the
  nominal path: only query-visible slots touched, untouched slots never rewritten, restore
  idempotency, never-saves, no gameplay actions performed, no natural-history created. The one
  divergence is the throw-during-`Apply` leak (finding 1), which each of I* (leak-free restore on
  failure) relies on. No other invariant is violated. Note: the repository has no document literally
  titled "StateInjector invariants I1–I13"; the closest spec is the execution-context invariant list
  in `docs/PHASE7_EXECUTION_CONTEXT_ANALYSIS.md:602-623` (I1–I19), which this review used as context.

- **UI wiring** (`GalleryCharacterMenu.cs:117-130`, `GalleryMenu.cs`, `ModEntry.cs:308-369`).
  Preview is only enabled for `PreviewSupported`/`PreviewPartiallySupported`; replay is enabled for
  seen/unlocked/`DirectReplay`; unsupported/AnalysisOnly degrade to analyze-only or locked without
  invoking an unsafe injection. No historical selection path is exposed. `PreviewCapability.Unsupported`
  is dead (never returned) but harmless. `planned.Suggestion` is computed at click time from a fresh
  `RuntimeStateReader.Capture()` (`GalleryCharacterMenu.cs:121,127`) in the same frame as `Analyze`,
  so no stale-state hazard.

---

## Summary

One material (Medium) correctness gap — a throw inside `PreviewInjectionScope.Apply`/`CaptureAndApply`
leaks the partially-applied temporary state because the scope is never handed back to be disposed
(`StateInjector.cs:35-40` + `ReplayCoordinator.cs:101`), which the asymmetric null-unsafety in
`RuntimePreviewStateAccessor` (`RuntimePreviewState.cs:61-77`) makes reachable in principle. This
violates the stated "success and failure" restore guarantee. All other requested areas
(canonical current-state replay, capability/override logic, eventsSeen/mail apply-restore on the
normal path, `IPreviewStateAccessor` contract, `ReplaySaveGuard` gating on `previewScope`, no
remaining historical replay entry point, no natural history from preview/replay, UI wiring) show
no material findings.
