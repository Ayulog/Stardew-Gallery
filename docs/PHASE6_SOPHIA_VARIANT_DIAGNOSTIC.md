# Phase 6 — Sophia「修罗场」Event Variant Diagnostic

Status: Diagnostic only. No replay business logic, EventLauncher, ReplayCoordinator, EventPlayback, HistoricalReplayAssets, SQLite, history semantics, or UI changed. Phase 7 not started.
Branch/HEAD: `phase6/exact-script-event-launcher` @ `817b8ae6d68383b28be24b5c4f75b5cdf5ad27b1`.

This document locates two real, distinct event variants of the same SVE intervention ("修罗场") event that differ **only by whether Sophia is present**, in order to support P6-3 exact-resolved-content replay independence acceptance, and historical A/B capture validation.

---

## 2.1 Identified event

- **AssetName:** `Data/Events/HaleyHouse`
- **EventIds:**
  - `195012` — intervention **without** rabbit's foot `/item 446`; `/k 195019`; the jealousy "confrontation" branch.
  - `195019` — intervention **with** rabbit's foot (`/i 446`); `/k 195012`; the peaceful/negotiated branch.
- **Source:** Stardew Valley Expanded.
- **Patch source:** `code/OtherEvents/Other.json` in the SVE `[CP] Stardew Valley Expanded` pack, targeting `data/events/haleyhouse`.

This is a Content Patcher modification of the **vanilla group intervention / jealousy event** content belonging to `Data/Events/HaleyHouse`. The vanilla event is the six-bachelorette intervention scene; SVE patches the same location event to import Sophia (and, for the dating-trio block, Olivia + Claire). The two EventIds are the two rabbit's-foot-dependent sub-branches of the same intervention moment:

- `195019` (rabbit's foot held; `/i 446` present): the calmer "negotiation" branch — key ends `/i 446/k 195012`.
- `195012` (no rabbit's foot): the actual jealousy "修罗场" confrontation — key ends `/k 195019`, no `/i 446`.

Note: the two EventId keys are **interconnected via `/k`** (seen-event chaining). Entering the scene with the rabbit's foot plays `195019`; without it plays `195012`.

---

## 2.2 Variant model classification

There are **two structurally different** kinds of Sophia-present/absent variation. They must not be conflated.

### CASE A — same RawEventKey / different resolved script (10-hearts, not-married block)

From `code/OtherEvents/Other.json` lines ~32–75:

- The base key is **identical to the vanilla key**. Example (rabbit's-foot branch):
  `195019/f Haley 2500/f Emily 2500/f Penny 2500/f Abigail 2500/f Leah 2500/f Maru 2500/o Abigail/o Penny/o Leah/o Emily/o Maru/o Haley/o Shane/o Harvey/o Sebastian/o Sam/o Elliott/o Alex/e 38/e 2123343/e 10/e 901756/e 54/e 15/i 446/k 195012`
- Under the CP `When` condition, SVE **replaces the script at that same key** with one that additionally declares `Sophia 19 20 0` in the actor list and issues `Sophia` speak/face/jump commands.

Because it is a CP **content substitution** on an identical key:

- `EventIdentity` unchanged (same asset + same EventId).
- `RawEventKey` **unchanged**.
- `RootScript` **changed**.
- `RootDefinitionHash` **changed** (see 2.5).
- `RootScriptHash` **changed**.
- `PlaybackHash` **changed**.

This is **NOT** "same EventId + multiple RawEventKey selection". Its essence is:
> **same definition identity/key, different resolved content state** (driven by a CP `When` condition).

### CASE B — distinct RawEventKey (dating-trio block)

From `code/OtherEvents/Other.json` lines ~17–31:

- Under the CP `When` condition (Sophia/Claire/Olivia all dating), SVE adds a **distinct raw event key** that prepends the dating trio to the precondition list:
  `195019/f Olivia 2500/f Sophia 2500/f Claire 2500/f Haley 2500/f Emily 2500/...`
  `195012/f Olivia 2500/f Sophia 2500/f Claire 2500/f Haley 2500/f Emily 2500/...`
  (note the added `f Olivia 2500/f Sophia 2500/f Claire 2500` tokens).

This is a **genuinely different raw condition definition** — same semantic event family and same EventId, but a different precondition key. It belongs to the familiar model "same EventId + multiple RawEventKey selection".

**Conclusion:** CASE A and CASE B are different kinds of variant. CASE A is the primary target for P6-3 (resolved-content independence). CASE B is a supplementary "distinct raw definition" case.

---

## 2.3 Exact A/B script difference

### Variant A — Sophia present

- **EventId:** `195019` (rabbit's foot) / `195012` (no rabbit's foot)
- **RawEventKey:** `195019/f Olivia 2500/f Sophia 2500/f Claire 2500/f Haley 2500/f Emily 2500/f Penny 2500/f Abigail 2500/f Leah 2500/f Maru 2500/o Abigail/o Penny/o Leah/o Emily/o Maru/o Haley/o Shane/o Harvey/o Sebastian/o Sam/o Elliott/o Alex/e 38/e 2123343/e 10/e 901756/e 54/e 15/i 446/k 195012`
  (the dating-trio block; the 10-hearts block uses the same key as vanilla with a Sophia-injected script)
- **contains Sophia:** YES
- **actors (command-relevant):** `Sophia`, plus (for the trio block) `Olivia`, `Claire`, and the vanilla bachelorettes `Haley Emily Penny Maru Leah Abigail`.
- **evidence (actor + Sophia commands):**
  - actor declaration: `... Abigail 20 20 0 Sophia 18 20 0 Olivia 23 19 3 Claire 22 20 0 /positionOffset Abigail -10 -15 ...`
  - `faceDirection Sophia 3 true`
  - `speak Sophia "{{i18n:Intervention.Ladies.06}}"`
  - `jump Sophia` / `jump Claire / jump Olivia / jump Sophia`
  - for the 10-hearts block (`Sophia 19 20 0`): `faceDirection Sophia 3 true`, `speak Sophia "{{i18n:Intervention.195012.06}}"`.

### Variant B — Sophia absent

- **EventId:** `195019` / `195012` (same as A)
- **RawEventKey:** identical to A's base key when the 10-hearts/not-married block applies, but with **no Olivia/Sophia/Claire tokens** and a **vanilla (no-Sophia) script**. Reference (line ~32+, pre-SVE vanilla) is the `195019/f Haley 2500/f Emily 2500/...` / `195012/...` key whose script does **not** declare Sophia.
  - Absolute-absent key (no SVE import at all): `195019/f Haley 2500/f Emily 2500/f Penny 2500/f Abigail 2500/f Leah 2500/f Maru 2500/o Abigail/o Penny/o Leah/o Emily/o Maru/o Haley/o Shane/o Harvey/o Sebastian/o Sam/o Elliott/o Alex/e 38/e 2123343/e 10/e 901756/e 54/e 15/i 446/k 195012`.
- **contains Sophia:** NO
- **actors (command-relevant):** `Haley Emily Penny Maru Leah Abigail` (no Sophia / Olivia / Claire).
- **evidence (no Sophia):**
  - actor list contains **no** `Sophia` (or `Olivia`/`Claire`) reference.
  - no `speak Sophia ...`, no `faceDirection Sophia ...`, no `jump Sophia`.

**Summary:** Actor-level proof of difference is the presence/absence of the `Sophia` actor and the `Sophia`-targeted commands; on top, the dating-trio block also adds `Olivia` and `Claire`.

---

## 2.4 CP `When` conditions

Extracted verbatim from `code/OtherEvents/Other.json`. These are **Content Patcher `When` conditions** (GameStateQuery tokens), not archive-side logic.

### Variant A (Sophia present) is active when:

1. `Relationship:Sophia|contains=Married` is `false`
2. AND `Hearts:Sophia` is `10`

(the 10-hearts/not-married block, lines ~35–75 — this is the *primary* CASE A gate.)

**Also** (separate, earlier patch block — CASE B dating-trio):

3. `Relationship:Sophia` is `dating`
4. AND `Relationship:Claire` is `dating`
5. AND `Relationship:Olivia` is `dating`

(this block *additionally* yields the distinct RawEventKey in section 2.2/2.3.)

### Variant B (Sophia absent) is active when:

- **Not CASE A:** `Hearts:Sophia` is not `10`, OR `Relationship:Sophia|contains=Married` is `true`.
- **And not CASE B:** Sophia (and/or Claire/Olivia) is not `dating`.
- In that case SVE applies **no** Sophia patch to that key, so the base vanilla script (no Sophia) stands as the final resolved content.

### Condition types present

All gates here are **relationship/friendship state** conditions (`Relationship:Sophia`, `Hearts:Sophia`) evaluated by Content Patcher's `When` against the live save. There is **no** conditio on: `eventSeen`, mail flag, NPC-availability token, SVE config, or a `Token` — for these specific Sophia-injection blocks. So the only lever for A/B toggling at runtime is Sophia's friendship/heart and dating/married state.

### Important: Content Patcher patch order / overlay semantics

These are `EditData` / `Entries` patches on `data/events/haleyhouse`. There is no chained "base → patch1 → patch2" additive layering for two *different* scripts on the same key in the same load; rather:

- When the `When` condition is **true**, the SVE entry's script **overrides** the key's resolved script (this is the change that **adds** Sophia).
- When the `When` condition is **false**, no override is applied, so the **vanilla script (no Sophia) is the resolved content**.

So in the 10-hearts case one key has exactly one resolved script per asset state, and the A/B distinction is a **time-varying resolved state** of the same `EventIdentity`/`RawEventKey`, not two simultaneously-present siblings. This is precisely the P6-3 scenario.

---

## 2.5 Hash semantics (per current code)

Verified against source:

- `EventIdentity` = `(AssetName, EventId)`; asset name is normalized (case-insensitive, trimmed), EventId comparison remains case-sensitive/sensitive-after-trim. (`Domain/EventIdentity.cs`, `Checks/Program.cs`.)
- `RootScriptHash` = `SHA256(rootScript)` — full hex, `EventHashes.RootScript(script)`. (`Domain/EventHashes.cs:8`.)
- `RootDefinitionHash` = `SHA256(rawEventKey + '\0' + rootScript)` — `EventHashes.RootDefinition(rawEventKey, rootScript)`. (`Domain/EventHashes.cs:10`.)
- `PlaybackHash` = snapshot fingerprint of the historical playback **bundle**: `GetSnapshotFingerprint(rootScript, eventAssets, translations)` — a SHA-256 over the root script plus sorted nested event assets plus sorted translations. (`EventKey.cs:32`, `WatchedEventHistory.cs:190`.)

### Applied to CASE A

Same `EventIdentity`, same `RawEventKey`, **different `RootScript`** (Sophia injected vs not):

- `RootScriptHash` — **DIFFERENT**
- `RootDefinitionHash` — **DIFFERENT** (functions of the script)
- `PlaybackHash` — **DIFFERENT** (snapshot fingerprint over the script + nested assets)

### Applied to CASE B

`RawEventKey` **differs** (dating-trio tokens added). Since `RootDefinitionHash = SHA256(rawEventKey + '\0' + rootScript)`, even if the root script were hypothetically identical, `RootDefinitionHash` is **DIFFERENT** because the key differs.

### Why A/B can coexist

`ObservedVariantKey = (EventIdentity, RootDefinitionHash, PlaybackHash)` (`Domain/ObservedVariantKey.cs`). Both CASE A's two resolved states produce different `RootDefinitionHash` and `PlaybackHash`, so they are keys that can coexist as two observed variants (subject to being captured at separate points where the CP condition splits them). CASE B produces a different `RootDefinitionHash` via a different key. → **YES, A and B variants may coexist.**

---

## 3. P6-3 — exact resolved-content replay independence

**Definition (corrected):** Archive's catalog holds a resolved script **A**. Even if the Content Patcher condition later changes so the same `EventIdentity` (and even the same `RawEventKey`) now resolves to a different script **B**, replaying historical **A** must play **A**'s exact root script — it must **not** re-resolve by `EventId` from the current `Data/Events` and play **B**.

This is exactly the Sophia present/absent CASE A situation: one `EventId`/`RawEventKey`, time-varying resolved content.

---

## 4. P6-3 Sophia exact-resolved-content test (shortest manual path)

### State A — Sophia present

Prerequisite save state:
- Location/asset trigger: enter **Haley House**.
- Need a save where the six vanilla bachelorettes reach the intervention conditions (the vanilla `195019`/`195012` preconditions: `f ... 2500` on the bachelorettes and the `e 38/e 2123343/...` seen-event chain), rabbit's foot in inventory for `195019` (or no rabbit's foot for `195012`).
- For CASE A gate: `Hearts:Sophia = 10` and `Relationship:Sophia|contains=Married = false`.
- For CASE B gate (optional, trio): Sophia, Claire, and Olivia all `dating`.
- SVE config: n/a — no config token gates these blocks.

Operations:
1. Set Sophia to 10 hearts and not married so CP resolves the key to the Sophia-present script.
2. Command to verify Archive captured Sophia-present: open the Archive event detail for `Data/Events/HaleyHouse` / EventId `195019` (or `195012`), or read the diagnostic output `diagnostics/catalog-latest.json`. Look at `actorNames` / `containsSophia` (should include `Sophia`) and note:
   - `EventId`: `195019` (or `195012`)
   - `RawEventKey`: `195019/f Olivia 2500/f Sophia 2500/f Claire 2500/f Haley 2500/...` (trio) or the 10-hearts key
   - `RootDefinitionHash prefix`: ~12 chars from diagnostics
   - `RootScriptHash prefix`: ~12 chars from diagnostics
   - **Expected Sophia: YES**

3. If the event hasn't naturally fired yet, Archive current UI reflects the current resolved version; you can `current replay` it after first natural capture (historical-first caveat applies — see below).

### Change to State B — Sophia absent

4. With the least re-play, flip the CP condition to Sophia-absent. Safest/quickest order:
   - Lower Sophia below 10 hearts **or** set `Relationship:Sophia|contains=Married = true` (via a save-editing tool / SMAPI relationship edit), keeping the bachelorette intervention preconditions intact.
   - Do **not** regenerate farm/player identity (see section 5).
5. Trigger a Content Patcher reload so the token re-evaluates. Preferred methods (lowest to highest risk):
   - Reload the save (return to title then re-load) — reliably re-evaluates CP `When`.
   - Use SMAPI's `patch reload` / content reload where available; otherwise sleep or re-enter the location — but a **title re-load** is the guaranteed refresh for CP `Relationship:`/`Hearts:` tokens.
6. Verify Archive current resolved script is now Sophia-absent:
   - `EventId` / `RawEventKey`: same as State A base key (10-hearts case), no Sophia/Olivia/Claire tokens
   - `RootDefinitionHash prefix` / `RootScriptHash prefix`: different from State A
   - **Expected Sophia: NO**

### Critical replay check

Because the Archive version-selection UI primarily views the **current** resolved version, you cannot test "A replayed against the current world" by re-capturing A now. Instead:

- Capture A naturally first (historical A).
- Then flip to State B.
- Then replay **historical A** while the current world is State B (Sophia absent).
- **PASS** if Sophia appears (historical A preserved).
- **FAIL** if historical A is substituted by the current Sophia-absent script.

---

## 5. Historical A/B capture test (different cast under the same profile)

Goal: under one Archive profile, capture Variant A, then Variant B, and finally have the history UI show **both** playback variants distinctly.

### 5.1 Profile identity

- `SaveProfileKey = (Game1.uniqueIDForThisGame, Game1.player.UniqueMultiplayerID)` i.e. `(FarmUniqueId, PlayerUniqueId)`.
- To write both test states into the same Archive SQLite profile, keep both IDs unchanged.
- **Never** "New Game" / re-create the farmer / regenerate unique IDs. Prefer copying one base save folder and editing state, so the XML's farm/player IDs stay original.

### 5.2 Save-copy workflow (recommended)

- BaseSave → copy to `TestA` (State A) → copy to `TestB` (State B).
- The Archive profile key depends on the **internal XML IDs**, not the folder name; the folder name is only metadata. You may copy the folder as long as the XML `FarmUniqueId` and player `UniqueMultiplayerID` keep their original values.
- Safe practice: quit the game, back up the external saves, and place only one test version into `Saves` at a time — or use distinct folder names keeping internal IDs — so Stardew's save-selection does not clash. Never overwrite an unbacked-up save.

### 5.3 Capture A (Sophia present)

1. Start from base save → switch to State A conditions (section 4, State A).
2. Let the event trigger naturally in the Haley House.
3. Watch it fully to completion.
4. Archive history records it (natural occurrence → `eventSeen` committed → `WatchedEventHistory` commit-pending).
5. Record: `EventId`, `LastObservedAt`, `RootDefinitionHash prefix`, `PlaybackHash prefix`, `contains Sophia = YES`.

> Replay does **not** generate natural history; you must obtain the natural occurrence first.

### 5.4 Capture B (Sophia absent)

1. Restore the same profile's base state (same IDs).
2. Switch to State B (Sophia absent) per section 4.
3. Trigger naturally, watch to completion, capture B.
4. Record: same `EventIdentity`? same `RawEventKey`? (same for the 10-hearts CASE A), `RootDefinitionHash prefix`, `PlaybackHash prefix`, `contains Sophia = NO`.

### 5.5 Final historical replay acceptance

The history UI must be able to distinguish A and B. Minimum validation:

- Historical A → Sophia present.
- Historical B → Sophia absent.
- Switching the current world state must not change historical playback.

Leave the world in **B**:
- Replay A → Sophia still present.
- Replay B → Sophia still absent.

Then switch the world back to **A**:
- Replay B → Sophia still absent.

This simultaneously validates Phase 4 historical semantic split, Phase 5 SQLite full-observed-variant persistence, Phase 6 exact-script launcher, and HistoricalReplayAssets frozen nested content.

---

## 6. dating-trio as a secondary case (CASE B)

Optional second acceptance, for "same EventId + different raw definition conditions".

- **Variant 1 RawEventKey:** `195019/f Olivia 2500/f Sophia 2500/f Claire 2500/f Haley 2500/f Emily 2500/f Penny 2500/f Abigail 2500/f Leah 2500/f Maru 2500/o Abigail/o Penny/o Leah/o Emily/o Maru/o Haley/o Shane/o Harvey/o Sebastian/o Sam/o Elliott/o Alex/e 38/e 2123343/e 10/e 901756/e 54/e 15/i 446/k 195012`
  (and the `195012` twin ending `/k 195019`).
- **Variant 2 RawEventKey:** the vanilla base `195019/f Haley 2500/f Emily 2500/f Penny 2500/f Abigail 2500/f Leah 2500/f Maru 2500/o Abigail/o Penny/o Leah/o Emily/o Maru/o Haley/o Shane/o Harvey/o Sebastian/o Sam/o Elliott/o Alex/e 38/e 2123343/e 10/e 901756/e 54/e 15/i 446/k 195012`.
- **Condition difference:** the `f Olivia 2500`, `f Sophia 2500`, `f Claire 2500` precondition tokens (dating-trio) are added to Variant 1.

Use this to test **same EventId / different raw definition conditions**; use CASE A (section 4) to test **same RawEventKey / different resolved content**. They are not interchangeable.

---

## 7. P6-4 — custom (non-vanilla) event command

**Candidate available? NO.**

- A command scan of `code/OtherEvents/Other.json` (and the Sophia intervention scripts specifically) found only **vanilla** event commands: `farmer`, `positionOffset`, `pause`, `message`, `move`, `viewport`, `faceDirection`, `textAboveHead`, `emote`, `speak`, `jump`, `fade`, `playMusic`, `playSound`, `showFrame`, `stopAnimation`, `startJittering`, `stopJittering`, `animate`, `question`, `fork`, `resetVariable`, `dump`, `warpOut`, `end`, `globalFade`, `friendship`.
- No custom or mod-specific command token appears in the Sophia variant scripts.
- (`changeLocation` exists elsewhere in SVE data but is a **vanilla** event command and is not part of the Sophia intervention path; `SVE_BadlandsDeath` is a data/asset id token, not a custom command used here.)

**Conclusion:** The Sophia「修罗场」event is **not suitable** for P6-4. P6-4 remains **PENDING**; candidate will be screened later from actual loaded mod event scripts. No code change made for P6-4.

---

## 8. P6-5 — switchEvent / fork

**Candidate available? YES — via `fork`.** (No `switchEvent`.)

- `switchEvent`: **0 occurrences** in `Other.json` and none in the Sophia intervention scripts.
- `fork`: present. Branch keys (verified as real keys) are `choseToExplain`, `crying`, `lifestyleChoice`. Example in the Sophia variant script: `.../question fork1 "{{i18n:Intervention.195012.fork}}"/fork choseToExplain/...` then `choseToExplain` → `/fork lifestyleChoice`.

Behavior mapping to Archive:
- **Current replay:** nested/branch content uses the live current content.
- **Historical replay:** nested/branch content uses the frozen `HistoricalReplayAssets`.

**Shortest P6-5 test step (using `195012`'s fork):**
1. In the history UI, select historical `195012` (Sophia variant).
2. During replay, at the `question fork1` leave on **`choseToExplain`**; it branches into `choseToExplain`, which itself has a `fork lifestyleChoice`.
3. Confirm the historical branch plays the frozen `choseToExplain`/`lifestyleChoice` script, even if the current Data/Events for that key has since changed.

Note: The `question`-based dialogue choice is not a substitute for true `switchEvent`/`fork` acceptance, but `195012` genuinely ships a real `fork` (with real sub-script keys), so it is a valid P6-5 fork case. P6-5 is only scoped there; `switchEvent` still needs a separate suit.

---

## 9. No new business code

This round changed only `docs/PHASE6_SOPHIA_VARIANT_DIAGNOSTIC.md`. A read-only probe was **not** added — the content of `code/OtherEvents/Other.json` (external SVE pack, not the repo) already provided the raw keys, `When` conditions, actor lists, and fork targets. The only runtime-only values (`RootDefinitionHash`/`RootScriptHash`/`PlaybackHash` prefixes) must be read from the Archive diagnostics on the live save; no code was required to obtain them.

---

## 10. Runtime blockers / unresolved

- `RootDefinitionHash`, `RootScriptHash`, `PlaybackHash` **prefix values** are runtime-only (they require the live Archive catalog/diagnostics over the SVE-rendered asset). The repo has no SVE data. Obtained via `diagnostics/catalog-latest.json` (`Constants.DataPath\StardewGallery\diagnostics`). No blocker to the test procedure — only note that the concrete prefixes are recorded at capture time.
- Content Patcher `When` re-evaluation after a relationship edit needs a **title reload** to be authoritative; a same-session partial refresh may or may not re-resolve `Relationship:`/`Hearts:` tokens. The procedure recommends title-reload to remove this ambiguity.

---

## 11. Validation

Per repository instructions:
- `dotnet build -c Release` — expect 0 warnings / 0 errors (only pre-existing `NETSDK1138`).
- `dotnet run --project Checks/StardewGallery.Checks.csproj -c Release` — expect `Stardew Gallery checks passed.`
- `dotnet run --project PersistenceChecks/StardewGallery.PersistenceChecks.csproj -c Release` — expect `Stardew Gallery persistence checks passed.`
- `git diff --check` — expect clean.
