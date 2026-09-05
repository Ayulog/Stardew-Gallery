# Stardew Gallery 2.0.3 — Complete Vanilla Event Preconditions

## Goal and boundaries

Make the existing condition domain layer understand and describe all 41 Stardew Valley 1.6.15 event preconditions, including case-sensitive legacy aliases and deprecated negative aliases. Keep the gallery's current two-level UI, unlock rules, playback, persistence, history, and catalog ownership unchanged. Content Patcher conditions, SpaceCore/EPU extensions, deep Game State Query parsing, route solving, and new preview simulation are out of scope.

## Player flow

Opening a character page still shows the same event cards. Each card now parses every vanilla condition into a typed value, evaluates only the proven current-state subset, and formats a localized requirement. Unknown third-party syntax and malformed vanilla syntax preserve their original text and are marked as safely indeterminate.

## Implementation and responsibilities

- `ConditionParser`: quote-aware split injection; explicit `!`; case-sensitive alias lookup; case-insensitive canonical lookup; invariant numeric parsing; one direct dispatch table.
- `ConditionExpression`: typed nodes for 41 canonical conditions. Friendship/shipping pairs use ALL; seen-event/date/week/season/tile lists use ANY; chosen answer IDs use ALL.
- `ConditionDescriber` and `ConditionTextFormatter`: convert typed values into localization specs, then resolve NPC/item/game vocabulary only at the UI boundary. The domain layer has no `Game1` dependency.
- `ConditionEvaluator`: retains the existing supported subset. Friendship is ALL, seen events are ANY, `DaysPlayed` uses strict `>`, year 1 is exact, later years are minimums, and rainy/sunny use an explicit rain predicate. Other valid nodes are unsupported, never invalid.
- `PreviewPlanner`: only single friendship and single seen-event requirements remain injectable; multi-value requirements stay analysis-only. Season/time replay behavior is unchanged; weather uses the new typed shape without simulating custom weather.

Reference: the locally retained decompilation of Stardew Valley 1.6.15 `StardewValley.Preconditions` and `Event.TryGetPreconditionHandler`. No third-party code or assets are reused.

## Configuration, storage, and UI

No configuration or stored-data changes. No new UI or coordinates; the existing event summary and bounded tooltip consume the unified formatter.

## Compatibility risks

- Third-party preconditions remain opaque by design.
- Custom weather IDs can be described and evaluated, but replay does not synthesize them.
- Multiplayer behavior is unchanged and remains untested.
- Multi-season parsing follows the declared ANY semantics rather than reproducing the Stardew Valley 1.6.15 loop-index defect.

## Acceptance

- Parser matrix covers 41 canonical names, all aliases, case handling, negative XOR, multi-value forms, optional defaults, malformed/unknown distinction, quoted Game State Query text, and invariant numbers.
- Describer covers every typed node; all 12 locale files have identical keys and interpolation tokens.
- Evaluator checks collection, days-played, year, and weather semantics.
- Existing replay, ownership, unlock, persistence, and core checks remain green; Release build and whitespace checks pass.
- Player testing remains required for representative vanilla combinations, aliases `M/m/d/z/k`, third-party unknown syntax, replay, long German/Russian/Hungarian strings, CJK text, and multiplayer.
