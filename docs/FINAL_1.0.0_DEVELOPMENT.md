# Stardew Gallery FINAL 1.0.0 Development

Date: 2026-09-04

## Base

- Branch: `phase-final/1.0.0`
- Base SHA: `696c55e9c6fd15a2e01d4b3e2de4862902b6d8ef` (accepted Phase 7C HEAD)
- Final HEAD: latest commit on `phase-final/1.0.0`

## Final product scope

Stardew Gallery 1.0.0 is a **current-state event navigator and preview tool**, not a historical replay archive.

```text
Current game + current mods
  -> ResolvedEventIndex
  -> ConditionIR / analysis
  -> current availability + readable conditions + progress gap
  -> PreviewPlan
  -> PreviewState (sparse overrides only)
  -> StateInjector (scoped RAII)
  -> SaveFirewall + safety snapshot
  -> EventLauncher (exact current script)
  -> native Stardew Event engine
  -> restore
  -> Gallery UI
```

## History de-scope

Historical replay, historical content/outcome replay, historical exact replay, historical player-choice replay, and the execution-trace UI are **not** 1.0.0 features. The `EventPlayback.ForHistorical`/`HistoricalEventRecord`/`HistoricalExecutionContext`/SQLite history tables remain in code for backward compatibility but are no longer product-critical and are hidden from the product mainline.

Replay always resolves via `EventPlayback.ForCurrent(entry.Resolved)`; no launch path selects a frozen historical version.

## Current-content canonicality

Catalog invalidates and rebuilds on save load, title return, locale change, and event-asset/character invalidation. Replay and preview use the newly resolved current version; historical frozen content is never the user-facing source.

## Condition explanation and progress gap

`PreviewPlanner` parses the event's precondition tokens into a `ConditionSet`, evaluates each against a `CurrentStateSnapshot` (truth vs knowledge), and produces an `EventConditionStatus`:

- `IsCurrentlyAvailable`
- `RequiredCount` / `MissingCount` / `UnknownCount`
- `MissingSummaries` / `UnknownSummaries` / `ReadableRequired`
- `PreviewCapability`

`Unknown` is never collapsed into `False`. Unsupported/opaque conditions are reported as "requires live game evaluation" rather than invented.

## PreviewCapability

- `DirectReplay` — all conditions satisfied.
- `PreviewSupported` — all unmet requirements are representable by restorable overrides.
- `PreviewPartiallySupported` — some requirements restorable, some not (or an unknown condition present with other missing requirements).
- `AnalysisOnly` — conditions explained, but no safe launch through PreviewState.
- `Unsupported` — cannot be safely analyzed/previewed.

Restorable override kinds: friendship, eventsSeen, mail, season, day-of-month, year, time. Weather and relationship/world-state are analysis-only (not restorable by the existing snapshot and not safely injectable).

## PreviewState

Sparse record. Only fields a preview intends to override. Never a full save snapshot.

## PreviewPlan

```text
PreviewPlan(Identity, Playback, Capability, Suggestion(PreviewState), Overrides[], UnsupportedRequirements[], Warnings[])
```

Pure/descriptive; applies nothing itself.

## StateInjector

`PreviewInjectionScope.Apply(accessor, PreviewState)` snapshots exactly the touched slots, applies overrides, and restores on `Dispose`. It is idempotent, restores on success/failure/exception, leaves untouched slots alone, never saves, and `Apply` always returns a scope even if a setter throws mid-capture so applied state is still rolled back.

`IPreviewStateAccessor` exposes only the restorable slots. `RuntimePreviewStateAccessor` maps them to live game state.

## Supported override matrix

| State | Classification | Notes |
| --- | --- | --- |
| Season | SafeMutable | query-visible, restored |
| DayOfMonth | SafeMutable | query-visible, restored |
| Year | SafeMutable | query-visible, restored |
| Time | SafeMutable | query-visible, restored |
| Friendship | SafeMutable | points only, restored |
| EventsSeen | SafeMutable | membership, restored exactly |
| Mail | SafeMutable | membership, restored |
| Weather | AnalyzeOnly | location-weather, not safely injectable |
| Relationship (dating/spouse/roommate) | AnalyzeOnly | not restorable/proven |
| WorldState | AnalyzeOnly/Unsupported | not safely injectable |

## Safety transaction

Preview launch runs: backup snapshot → `IsActive` set (blocks save) → `PreviewInjectionScope.Apply` → `EventLauncher.TryLaunch(current)` → native event runs → `Restore()` (disposes injection scope, restores snapshot/position/presentation) → `Clear()`. `ReplaySaveGuard` blocks saving for the whole preview/replay window because `IsActive` includes the active preview scope.

## Restore failure behavior

Restore failure fails closed: existing fail-safe path restores the prior save backup and returns to title rather than pretending preview succeeded. The injection scope's `Dispose` is idempotent and continues restoring remaining slots even if one restore throws.

## Degraded modes

- Custom/unparseable condition -> event still listed, condition unknown, preview capability downgraded, no fake solution.
- SQLite unavailable -> existing fallback behavior.
- Runtime hook unavailable -> affected feature disabled, no crash.

## UI

GalleryCharacterMenu shows readable conditions, current availability, missing/unknown requirements, capability label, and Replay/Preview buttons. Historical "version" entry point is removed. Scroll/selection return position and Gallery navigation are preserved.

## Tests

Core checks cover: direct replay; friendship/seen/mail gap preview-supported; opaque degradation; AND grouping; NOT/relationship analyze-only; weather analyze-only (not injected); sparse PreviewState; injector apply/restore incl. eventsSeen/mail removal; idempotent restore; untouched-state unchanged; capture-failure still restores; current-script playback canonical.

Persistence checks (existing) still pass: SQLite v2 open, v1 migration, future-schema rejection, corrupt-context isolation, unused history infrastructure non-interference.

## Runtime acceptance

No in-game runtime acceptance was performed in this implementation environment. The documented R1–R10 matrix remains to be run manually and is reflected in `RELEASE_1.0.0_SCOPE.md` and the final report.

## Limitations

- Runtime acceptance (R1–R10) not executed in this environment.
- Weather/relationship/world-state preview are analysis-only.
- Single-player only.
- Opaque/custom conditions degrade rather than simulate.
- Not every event is previewable.

## Release status

See `RELEASE_1.0.0_SCOPE.md` and the final report. Implementation is complete and automated validation passes; runtime acceptance is pending, so the release classification is `RELEASE CANDIDATE — MANUAL TESTS PENDING`.
