# Stardew Gallery 2.0.1 Task

## Product rules

- An event is unlocked only when the player has seen it or Unlock All is enabled.
- Current trigger availability does not unlock an event.
- Locked events have no playback action. The player-facing Preview action is removed.
- Every replay uses the current resolved event script.

## Replay environment

- Apply explicit positive season, time and supported vanilla weather requirements for every replay.
- Apply them at the target location immediately before the event becomes active.
- Preserve current season/time when already allowed; otherwise choose the first allowed season and the minimum time.
- Support Sun, Rain, Storm, Snow and Wind. `sunny` maps to Sun; `rainy` preserves Rain/Storm or chooses Rain.
- Ignore negative-only and unsupported custom weather requirements.
- Capture and restore season, time and the target location context's full weather flags.
- Environment setup failure logs a warning and replay continues. Launch failure logs an error. Restore failure logs an error and uses the existing failsafe.

## Out of scope

- No friendship, mail, seen-event, relationship, world-state or custom condition injection.
- No historical replay or Phase 7 continuation.
- Keep dormant compatibility code when deleting it would widen the change.
- Multiplayer remains unsupported and untested.

## Validation

- Release build, core checks, persistence checks and `git diff --check` must pass.
- In-game season/time/weather application and exact restoration remain manual acceptance items.
