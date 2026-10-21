# Final 1.0.0 Review (OpenCode) — phase-final/1.0.0

Date: 2026-09-04. Read-only review of `phase-final/1.0.0` against
`FINAL_1.0.0_IMPLEMENTATION_TASK.md`. No files were edited.

- Base SHA: `696c55e9c6fd15a2e01d4b3e2de4862902b6d8ef` (accepted Phase 7C HEAD)
- Final HEAD: `a25ad158a306ae4994db02e1298ef5119244f90a`
- Commits on branch: `e9a289b` (current-state replay canonical), `f8ff6a4` (harden preview restore / capture failure), `a25ad15` (prepare 1.0.0 release)
- Working tree clean.

Validation run in this environment (all pass):
- `dotnet build -c Release` — success, 0 warnings / 0 errors.
- `dotnet run --project Checks/StardewGallery.Checks.csproj -c Release` — "Stardew Gallery checks passed."
- `dotnet run --project PersistenceChecks/StardewGallery.PersistenceChecks.csproj -c Release` — "Stardew Gallery persistence checks passed."
- `git diff --check` — clean.

---

## Review points 1–7

### 1. Preview does not create natural history and does not save — PASS
- `ReplayCoordinator.IsActive` is `snapshot is not null || previewScope is not null`
  (`ReplayCoordinator.cs:28`). In `TryStartPreview` the `snapshot` is set (line 100) and
  `previewScope` is applied (line 101) *before* the event launches, so the preview window is
  covered before any event command runs.
- Every observation hook gates on `NaturalExecutionTraceRules.CanObserve(replayActive(), ...)`
  (`ExecutionTraceObserver.cs:57,122,138,177`), with `replayActive = () => replay.IsActive`
  (`ModEntry.cs:45`). It is `true` throughout, so the previewed event is never captured.
- `WatchedEventHistory.Update(replay.IsActive)` aborts any pending observation and persists nothing
  during the active window (`WatchedEventHistory.cs:120-127`).
- Preview never invokes a save routine; `ReplaySaveGuard.BeforeSave` blocks saving
  (`ReplaySaveGuard.cs:26`), and `IsActive` includes the preview scope.
- Post-play `ReplaySnapshot.RestorePlayer()` resets `eventsSeen`/`mailReceived`/`friendshipData`
  to the pre-replay snapshot (`ReplaySnapshot.cs:104-128`), and `Clear()` disposes the
  `PreviewInjectionScope`, which restores the touched slots exactly.

Note: an earlier intermediate review (`FINAL_1_0_0_REVIEW.md`, HEAD `e9a289b`) flagged a Medium
finding that a throw inside `PreviewInjectionScope.Apply` leaked partially-applied state because the
scope was never returned. That was fixed by `f8ff6a4`: `Apply` now wraps `CaptureAndApply` in a
`try/catch` and returns the scope regardless so `Dispose` still rolls back whatever was applied
(`StateInjector.cs:35-48`). No residual leak.

### 2. Current-state replay canonical, no product-facing historical entry point — PASS
- Replay and preview both build playback from `EventPlayback.ForCurrent(entry.Resolved)`
  (`ReplayCoordinator.cs:47,94`; `PreviewPlanner.cs:131`).
- `EventPlayback.ForHistorical` is referenced only by `Checks/Program.cs:1015` (a test); no product
  launch path uses it.
- `ReplayCoordinator.historicalAssets` is only `.Clear()`d; it is never activated by any current
  launch path, so frozen/historical content is never the user-facing source.
- `GalleryMenu.watchedVersions` is stored and passed through (`GalleryMenu.cs:29,79,181,382`) but
  never invoked to launch; all UI paths resolve via `ModEntry.StartReplay` →
  `replay.TryStart`/`TryStartPreview`. `GalleryCharacterMenu` does not receive `watchedVersions`.
- No `历史*`/`Historical*`/`exact replay` terms remain in the player-facing i18n strings; the
  product-facing text uses current-state "回放/预览" only. Historical de-scope is documented in
  `docs/FINAL_1.0.0_DEVELOPMENT.md` and `docs/RELEASE_1.0.0_SCOPE.md`.

### 3. StateInjector / PreviewInjectionScope safety invariants — PASS
- I1 Apply scoped: `PreviewInjectionScope.Apply(...)` returns a scope that owns the restore list.
- I2 Restore idempotent: `Dispose` guards on `disposed`; a second call is a no-op
  (`StateInjector.cs:105-121`).
- I3/I4/I5 Restore on success/exception/launch failure: `Clear()` disposes the scope on
  `FinishRestore`; `Restore(error)` paths still reach `Clear()`; `Apply` now always returns a scope
  even when a setter throws mid-capture, so partial application is rolled back.
- I6 Nested scopes: only one scope is ever created per preview and `TryStart` rejects when already
  active; no nesting is possible.
- I7 Untouched never rewritten: `CaptureAndApply` only registers restores for slots that actually
  differ, and skips already-present eventsSeen/mail (`if (before) continue`).
- I8 Preview does not save: enforced by `ReplaySaveGuard` + no save call.
- `IPreviewStateAccessor` exposes exactly the restorable slots (season/day/year/time/friendship/
  eventsSeen/mail); weather/relationship/world-state are deliberately absent, so no unsafe
  injection can be requested. `PreviewState` is sparse, never a full snapshot.

Informational (not material): `RuntimePreviewStateAccessor` does not null-guard
`eventsSeen`/`mailReceived`/`friendshipData` the way `RuntimeStateReader.Capture` does
(`RuntimePreviewState.cs:61-77` vs `23-38`). Preview only runs on a loaded save where these are
populated, and `Apply` now catches a throw safely, so this is a cosmetic asymmetry rather than a
correctness gap.

### 4. SaveFirewall active during preview — PASS
- `ReplaySaveGuard.BeforeSave` short-circuits on `replay.IsActive`
  (`ReplaySaveGuard.cs:26`); `IsActive` includes `previewScope` (`ReplayCoordinator.cs:28`), so save
  is blocked for the whole preview/replay/restore window. `BeforeSave` rejects and shows the
  `replay.save-blocked` HUD message. `FailSafe` uses `SaveGame.Load` (load, not save), and the
  injection accessor never persists.

### 5. No Event engine reimplementation — PASS
- `EventLauncher.TryLaunch` constructs the native `StardewValley.Event` from the resolved current
  script and hands it to native `startEvent`/`currentLocation.currentEvent` (`EventLauncher.cs:38-76`).
  No Gallery-side inter&preter exists; custom commands execute natively. `EventLauncher` only
  schedules/warps per Phase 6 semantics.

### 6. Unknown conditions not treated as false — PASS
- `ConditionEvaluator` returns `ConditionTruth.Unknown` (with `Knowledge = MissingData/Unsupported/
  Invalid/Error`) for missing data, opaque, custom/native-query, and invalid cases
  (`ConditionEvaluator.cs:28-31,211-232`). Only genuinely-resolvable cases return `Known` +
  True/False.
- `PreviewPlanner.Analyze` routes `Knowledge != Known` into `unknown`, never into `missing`
  (`PreviewPlanner.cs:82-91`), and `IsCurrentlyAvailable` requires both counts to be zero.
  `ComputeCapability` treats any unknown as `AnalysisOnly`/`PreviewPartiallySupported` rather than
  `DirectReplay`/`PreviewSupported`, and never fabricates an override for opaque conditions
  (`TryBuildOverride` returns false for `Negated` and unsupported kinds).
- `OpaqueCondition` maps to `Unknown/Unsupported`; the planner is constructed with
  `checkNativeQuery: null` (`ModEntry.cs:14`), so native GSQ conditions remain honestly Unknown
  rather than being guessed.

### 7. Release docs / README / changelog / manifest consistency — PASS
- README (`README.md`) describes a current-state event gallery/planning tool, explicitly states
  replay/preview is current-state, not historical, and notes single-player-only and honest
  degradation. No historical-replay advertising.
- CHANGELOG 1.0.0 entry is consistent (current-state canonical, historical replay not a product
  feature).
- `manifest.json` version 1.0.0, description aligned with the current-state product.
- `docs/FINAL_1.0.0_DEVELOPMENT.md` and `docs/RELEASE_1.0.0_SCOPE.md` document the de-scope,
  override matrix, safety transaction, limitations, and explicitly mark runtime acceptance pending.

---

## Remaining material findings

None. All seven review areas match the task intent. No new correctness, safety, or
product-semantics defect was found at HEAD `a25ad15`. The single earlier-caught leakage gap
(reviewed at `e9a289b`) is resolved by the hardening commit `f8ff6a4`.

The one informational note (preview accessor null-guard asymmetry) is cosmetic and non-blocking.

## Known limitations (unchanged, documented, acceptable for 1.0.0)

- Weather, relationship (dating/spouse/roommate), and world-state preview are analysis-only
  (not injected).
- Single-player only; multiplayer not tested.
- Opaque/custom conditions degrade to "view only"/partial rather than being simulated.
- Not every event is previewable; unsupported events are not forced.
- Historical / execution-trace code remains for compatibility but is hidden from the product.

## Release readiness classification

**RELEASE CANDIDATE — MANUAL TESTS PENDING**

Runtime acceptance R1–R10 has **not** been executed in this environment. The automated gate
(build, core checks, persistence checks, diff check) passes, but the final runtime gate
(`FINAL_1.0.0_IMPLEMENTATION_TASK.md` §72) requires in-game execution of R1–R10, which cannot be
performed here and is explicitly listed as pending in `docs/RELEASE_1.0.0_SCOPE.md`. Therefore the
release is not classified `RELEASE READY`; it is a release candidate awaiting manual runtime
validation.
