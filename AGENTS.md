# Stardew Gallery Agent Guide

## Current task

For Phase 1 work, read `docs/PHASE1_TASK.md` completely before editing. Use `docs/PHASE1_DEVELOPMENT.md` for the approved technical plan and acceptance boundaries.

Phase 1 is a zero-user-visible-behavior domain-model migration. Keep the diff inside the files allowed by the task, preserve the existing watched-event JSON schema, and do not begin Phase 2 work.

## Working rules

- Communicate and write reports in Simplified Chinese; keep player-facing text bilingual through `i18n`.
- Preserve unrelated working-tree changes.
- Use the smallest change that satisfies the task; do not refactor Replay or EventFragments unless compilation requires a compatibility edit.
- Do not modify game files or other Mods.
- Do not run destructive Git commands or push without explicit user approval.
- Do not add third-party code, assets, or dependencies without explicit approval and license review.

## Completion

Run:

```text
dotnet build -c Release
dotnet run --project Checks/StardewGallery.Checks.csproj -c Release
git diff --check
```

Then create `docs/PHASE1_REPORT.md` with the modified files, build/check results, compatibility confirmations, and unresolved items required by `docs/PHASE1_TASK.md`.
