# Stardew Gallery 1.0.0 Release Scope

## IN 1.0.0

- Current-state event gallery driven by the current installed content and resolved event index.
- Readable condition explanation (friendship/hearts, seen events, mail, season, day, year, time).
- Truth vs knowledge separation: Satisfied / Unsatisfied / Unknown (never collapsed).
- Current progress gap per event.
- Current-state replay via the exact current resolved script and native Stardew Event engine.
- Safe preview for supportable conditions (friendship, eventsSeen, mail, season, day, year, time) with exact restore.
- Scoped state injection (`PreviewInjectionScope`) with idempotent, failure-safe restore.
- Save firewall during replay/preview; pre-replay snapshot/backup; post-play restore.
- Current-content invalidation/rebuild.
- Bilingual (zh/en) UI, adaptive scaling, keyboard/mouse/controller, configurable keybinds, optional GMCM.
- Degraded/unknown handling for unparseable or unsupported conditions.
- Automated core and persistence checks.

## OUT OF 1.0.0

- Historical content replay.
- Historical automatic outcome replay.
- Historical exact replay.
- Historical player-choice replay.
- Execution trace UI.
- Frame-perfect replay.
- Full-world simulation.
- Universal custom-command simulation.
- Full multiplayer preview guarantee.
- Arbitrary mod-state solver.

## KNOWN LIMITATIONS

- Runtime acceptance (R1–R10) not executed in this environment; manual testing pending.
- Weather, relationship (dating/spouse/roommate), and world-state preview are analysis-only (not injected).
- Single-player only; multiplayer not tested.
- Opaque/custom conditions degrade to "view only" rather than being simulated.
- Not every event is previewable; unsupported events are not forced.
- Historical/execution-trace code remains for compatibility but is hidden from the product.

## FUTURE IDEAS (not implemented)

- Historical archive mode.
- Historical trace replay.
- Exact choice replay.
- Advanced condition solver.
- Route planner.
- Multi-event progression planning.
- Mod-specific condition adapters.
- Full multiplayer support.
