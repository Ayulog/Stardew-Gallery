# Stardew Gallery 2.0.2 — Condition Display

## Goal

Replace the event card's legacy condition whitelist with the existing ConditionIR parser, evaluator, and describer.

## Product behavior

- Show every parsed condition with a clear met, missing, or unknown state.
- Show available current/required values for numeric and state gaps.
- Never use the generic “Other original condition” label.
- For unsupported or malformed conditions, show the preserved raw condition and state that it can't be evaluated safely.
- Keep the existing two-line card summary and bounded full tooltip.

## Boundaries

- Do not change gallery unlock rules, replay behavior, replay environment, saves, history, SQLite, or Preview behavior.
- Do not add new condition syntax; use the existing parser/evaluator/describer.
- Keep Simplified Chinese and English text in i18n.

## Validation

- Existing and new condition checks pass.
- `dotnet build -c Release` succeeds with no warnings or errors.
- Core and persistence checks pass.
- `git diff --check` reports no whitespace errors.
- In-game: inspect known, missing, negated, Game State Query, and unsupported mod conditions in an event tooltip.
