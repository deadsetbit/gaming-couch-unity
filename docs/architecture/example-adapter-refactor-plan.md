# Example Refactor — Plan (grilled 2026-07-11; decisions resolved)

Status: **COMPLETE 2026-07-12 — all nine decisions resolved (§9), design simplified to two self-contained scripts (§4.1), ADR 0017 drafted, executable task tracker in §11 fully executed. Groups A–E DONE; EditMode + PlayMode suites verified green in Unity's Test Runner. Nothing committed (repo convention: no commits/push unless asked).**
Owner: Gaming Couch Unity package team
Created: 2026-07-10
Review: fact-checked against the codebase (2026-07-10). Load-bearing mechanism claims verified
(SendMessage listener contract; readiness gate accepts private magic methods on a base class).
Corrections folded in: Start Screen citation, generation-feasibility caveat (bare package
ships everything), naming collision with existing `GCGame`/`GCGameVersus`, B004 framing.
Re-checked against 2026-07-11 commits (`e3cb4d7`, `0508393`, `2aa8419`, `a5b313d`) — verdict:
architecture still sound, **minor staleness**. Folded in: "Create New Example Scene" is now a
reset-to-single-clean-scene action (§2.1/§4.2); the D8 canonical-source/generated-copy collision is
no longer hypothetical (the `FindTypeByName` guard forced today's fixture renames, §5.1/D8); the new
non-shipping runtime test assembly `GamingCouch.Tests.Fixtures` is a working precedent for the
compile guarantee (§5.2/D8); a new blocking-folder cleanup mechanism (R2/§6); and refreshed
line-number citations for the two files that grew today.
Related: ADR 0016 (example scene creation), backlog B004 / B001 / B002 / B006, CONTEXT.md vocabulary

---

## ✅ Complete (2026-07-12)

**§11 fully executed. Groups A, B, C, D, E are all DONE and verified green.** Branch `feature/standalone-controller`. Nothing committed (repo convention: no commits/push unless asked).

- **A ✅ / B ✅ / C ✅ / E ✅** — each verified green in the open-Editor bridge (EditMode) via the host project. E is docs-only (no test needed).
- **D ✅ — verified.** PlayMode smoke test (`Tests/PlayMode/`, + `InternalsVisibleTo` in `Runtime/GamingCouch.cs`). First run failed with "Game already set" — the test manually `SendMessage`d `GamingCouchSetup`, but `GamingCouch.Start()` already drives it, so setup double-ran. Fixed (removed the manual trigger; the runtime's `Start()` drives setup→play→game-over from the pre-cached fake capture). **Re-run green.**
- **Full suites green (2026-07-12):** both PlayMode and EditMode ran (all tests) in Unity's Test Runner.

**Verification workflow (decided):** the user runs tests manually in Unity's Test Runner (the open-Editor bridge only progresses while Unity is frontmost). Host project: `/absolute/path/to/gaming-couch-unity-template` (symlinks this repo at `Packages/gaming-couch-unity`).

**Remaining optional item:** A4 teeth-test — temporarily rename a runtime API the canonical sources use (e.g. `GCPlayer.SetLives`), confirm the build breaks, revert. The compile guarantee is already proven by construction (the `…ExampleCanonical` assembly compiles against the real runtime and the suite is green); this is only extra confidence. Not run yet; run on request.

**Deliberate deviations from §11 (confirmed with user):** C3 readiness checklist row skipped (button + menu are the surface; a template scene is a valid ready state); template player prefab is a generated `GCPlayer.prefab` capsule.

> This is a starting point written to be torn apart in a grill session. It states a
> recommended direction plus the open decisions that must be settled before any code
> changes. Everything below is grounded in the current codebase (file:line citations).

---

## 1. Purpose & scope

Restructure the example into **two self-contained, swappable game scripts** that share one
scene: a barebones template (the wiring demo you copy to start your own game) and a full
example game of the same shape. Each script *is* the platform listener and shows the whole
contract in one file — so a developer can drop in their own game without touching the platform
wiring, and without any separate adapter/base-class layer to understand (that separation is
deferred to the authoring-ergonomics API, B001).

Deliver this as an **additive flow**:

1. **Create example scene** → wires the scene to a *minimal debug-logging template game*.
2. **Wire example game** (optional, additive) → swaps in a *full example game of the same shape*,
   upgrading the same scene in place rather than replacing it.

And close the long-standing maintainability gap: the example must **fail the build/tests when
the runtime API drifts**, and the two-action flow must be covered by tests.

**In scope:** the generated Example Assets, the Editor setup/readiness/menu tooling that
creates them, the generation mechanism itself, and the test suite that guards them.

**Out of scope (this plan):** changing the platform ↔ `GamingCouch` wire contract, bumping
`gameProtocolVersion`, and the broader authoring-ergonomics API (B001) — though this plan
must not paint any of those into a corner.

---

## 2. Current state (grounded)

### 2.1 How the example is delivered today

- There are **no checked-in scenes or example scripts** in the repo. The example is
  *generated* into the user's project under `Assets/GamingCouch/GCExample/`:
  `GCGameExample.cs`, `GCPlayerExample.cs`, `GCPlayerExample.prefab`
  (`Editor/GamingCouchActiveSceneSetup.cs:301-311`).
- The generated source is **string-literal templates** in Editor code:
  `BuildGameScriptSource` (`GamingCouchActiveSceneSetup.cs:2286`) and
  `BuildPlayerScriptSource` (`:2461`), prefixed by a move/rename header
  `ExampleTemplateHeader` (`:324-330`).
- Two Editor entry points create/wire it (ADR 0016): **Active Scene Setup**
  ("Set up missing pieces", wires the open scene) and **Create New Example Scene**
  (`Editor/GamingCouchExampleSceneCreation.cs`), reachable from the **Start Screen**
  ("Create New Example Scene" button `Editor/GamingCouchStartScreenWindow.cs:205-207`;
  "Set up missing pieces" / Active Scene Setup button `:752`) and the `GamingCouch/…` menu
  (`Editor/GamingCouchMenuItems.cs:19-22`).
- **Update (2026-07-11, commit `0508393`):** "Create New Example Scene" no longer creates a scene
  *alongside* existing ones — it now **resets the example to a single clean `GCExampleScene`**,
  moving previous example scenes *and* leftover blocking asset folders to the Trash (recoverable)
  before regenerating one canonical scene (intent `Editor/GamingCouchExampleSceneCreation.cs:51-59`;
  `CreateExampleScene` `:66-193`, blocking-folder handling `:84`/`:113`, `MoveAssetToTrash` `:215`).
  So this action is now an **idempotent replace**, not an additive create — the §4.2 flow must
  build on that.
- ADR 0016 rule: Example Assets are **editable and never overwritten**; creating a scene
  never changes Build Settings. Any new action must honor this — and the reset above upholds it:
  example **scripts and the player prefab are reused, never overwritten**, and which scene a build
  boots into is still left to "Set up missing pieces", so a reset never silently changes Build
  Settings (`GamingCouchExampleSceneCreation.cs:56-59`).

### 2.2 The adapter/game coupling

- `GamingCouch` exposes two serialized fields: `listener` (a `GameObject`) and `playerPrefab`
  (`Runtime/GamingCouch.cs:50-54`).
- Dispatch is **`SendMessage` by string name** with `RequireReceiver`:
  `listener.SendMessage("GamingCouchSetup", …)` (`GamingCouch.cs:305`) and
  `listener.SendMessage("GamingCouchPlay", …)` (`:441`). **No interface, no delegate** —
  a runtime-only, convention-based contract.
- The readiness gate accepts the two magic methods even when **private** and defined on a
  **base class**, walking up to `MonoBehaviour` (`GamingCouchStartScreenReadiness.cs:1207-1208,1260-1266,1278+`).
  → So a game declaring `GamingCouchSetup`/`GamingCouchPlay` directly (public or private, even
    inherited) satisfies wiring — no base class or adapter needed for the readiness gate.
- The generated `GCGameExample` MonoBehaviour is the `listener` and **mixes two concerns**
  (`GamingCouchActiveSceneSetup.cs:2178-2337`):
  - *Adapter glue* (platform contract): `GamingCouchSetup(GCSetupOptions)` →
    `SetupGameVersus(...)` + `SetupDone()`; `GamingCouchPlay(GCPlayOptions)` →
    `SetupPlayers<T>(...)`; per-frame `GetInputsByPlayerIndex(...)`; `GameOver()`.
  - *Game logic*: round-timer coroutine, scoring/finish/eliminate rules, HUD/meter updates,
    random final scores.
- Game-facing API the example must exercise (this doubles as the drift checklist):
  `SetupGameVersus` (`GamingCouch.cs:669`), `SetupDone` (`:646`), `SetupPlayers<T>` (`:874/886`),
  `GetInputsByPlayerIndex` (`:953`), `GameOver` (`:704`), `Hud` (`:109`), `Status` (`:96`);
  and `GCPlayer` mutators `SetScore/AddScore`, `SetLives`, `SetStatus`, `SetMeter`,
  `SetFinished*`, `SetEliminated*`, `GetHudValueText` (`Runtime/GCPlayer.cs`).
- Three runtime invariants any refactor must preserve: exactly one `GamingCouch` in scene
  (`GamingCouch.cs:123`); a non-null `listener` carrying both methods (RequireReceiver at
  305/441); a `playerPrefab` whose root has both the game's `T` and a `GCPlayer`
  (`InstantiatePlayer<T>` `:805-815`).

### 2.3 Problems this plan solves

1. **Concerns are entangled.** Adapter glue and game rules live in one MonoBehaviour, so
   "swap the game" means editing the same file that talks to the platform.
2. **No adapter seam / shared shape.** There is no interface or base type a game plugs into,
   so "same shape" games cannot be swapped in an additive way.
   > Revised 2026-07-11: problems 1–2 (separating glue from rules, a shared contract type) are
   > **deliberately not solved at the example level** — that separation belongs to the
   > authoring-ergonomics API (B001). The example instead ships two self-contained games you
   > swap wholesale (§4.1). Problems 3–4 remain fully in scope.
3. **Generation is unmaintainable and drift-prone.** Source lives as string literals; the
   test suite asserts the *text* with hardcoded substring duplicates — 42 `Does.Contain(...)`
   checks in `AssertGeneratedGameSourceDemonstratesPlayFlow`
   (`GamingCouchActiveSceneSetupAssetTests.cs:1038-1086`; 59 across the file).
   **Nothing compiles the generated source or checks it against the runtime API** — rename
   `GCPlayer.SetLives` and every test still passes.
4. **Docs contradict themselves, and the example is neither tier cleanly.** README calls the
   example "a complete, working game" (`README.md:80`); CONTEXT.md calls Example Assets "a
   wiring demo, not a full sample game" (`:106`). In reality today's single generated example
   *already* runs a small playable loop (scoring, finish/eliminate, round timer, `GameOver`) —
   so B004's "demonstrate a playable loop" is largely met. This plan's value for B004 is not
   *adding* a loop but **splitting** it into two honest tiers: a genuinely minimal *template*
   (the wiring demo) and the *example game* (the playable loop) — two self-contained scripts you
   swap wholesale (§4.1), no adapter between them.

---

## 3. Goals & non-goals

**Goals**
- G1. A clear **swap point**: two self-contained example games (a barebones template and a
  full game) that plug into the same scene, so one can be swapped for the other.
- G2. **Additive two-action flow**: template scene first, example game wired in on top —
  one scene, not two disconnected ones.
- G3. **Removability**: authoring your own game = copy the template script and edit it; the
  `GamingCouch`/`playerPrefab` platform wiring is never touched.
- G4. **Drift-proof maintenance**: the example is compiled against the real runtime API in
  CI/dev, and generation cannot silently diverge from the canonical source.
- G5. **Tested**: both example scripts, the additive action, and the generation pipeline
  are covered by automated tests.

**Non-goals**
- N1. No change to the platform ↔ `GamingCouch` wire contract or `gameProtocolVersion`
  (confirm in grill; expected to stay 1 per ADR 0008).
- N2. Not building the full authoring-ergonomics API (B001) — but leave room to promote to it.
- N3. Not adopting UPM `Samples~` (CONTEXT.md explicitly says *avoid "package samples"*; also
  loses the never-overwrite tooling and version-adaptive wiring). Recorded as a rejected option.

---

## 4. Proposed architecture

### 4.1 The shape: two self-contained, swappable game scripts

No adapter, no shared base class. The example is **two complete game scripts**; each one *is*
the `listener` and implements the platform's magic methods directly. You attach **one** of them
to the scene's listener GameObject — swapping games means swapping which script is attached.

```
              platform (JS/WebGL host or editor play)
                          │  SendMessage (unchanged wire contract)
                          ▼
                 ┌───────────────────┐
                 │   GamingCouch      │  package (unchanged)
                 │  listener ─────────┼──────────┐  SendMessage("GamingCouchSetup"/"Play")
                 │  playerPrefab      │          ▼
                 └───────────────────┘   ┌──────────── the listener (attach one) ───────────┐
                                         │  GCExampleTemplate      GCExampleGame             │
                                         │  barebones + Debug.Log  full playable loop        │
                                         │  spawns stock GCPlayer  scoring / rounds / inputs │
                                         │  (copy me to start)     custom GCExamplePlayer    │
                                         └───────────────────────────────────────────────────┘
```

- Each script implements `GamingCouchSetup(GCSetupOptions)` / `GamingCouchPlay(GCPlayOptions)`
  directly (the methods `GamingCouch` invokes by `SendMessage`), spawns players, and signals
  `SetupDone`/`GameOver`. The whole platform contract is visible in one file — nothing hidden
  behind an adapter or base class.
- `GCExampleTemplate` is the barebones starting point (spawns the stock `GCPlayer`, logs each
  step, does the minimum). `GCExampleGame` is the full reference of the same shape (real
  scoring/rounds/inputs, custom `GCExamplePlayer`). "Same shape" is a convention (both implement
  the contract), not an enforced base type.
- "Author your own game" = copy `GCExampleTemplate` and edit it; the `GamingCouch`/`playerPrefab`
  wiring is untouched (G3). Factoring a reusable adapter/contract out of this is deferred to the
  authoring-ergonomics API (B001) — deliberately *not* part of the example.

> **Design revision (2026-07-11).** An earlier draft added a separate *adapter component* that
> forwarded to a *game-contract base*. That layer was cut: for a learn-by-example template, one
> self-contained script per game is clearer than hidden plumbing, and the separation it offered
> belongs to B001. D1 (§9) still holds — everything is generated Example Assets, no new package API.

### 4.2 The additive two-action flow (template → example game)

One scene, two buttons; the second upgrades the first in place.

- **Action A — "Create example scene"** (existing action): resets to one clean `GCExampleScene`
  with `GamingCouch` wired to a `GCExampleTemplate` component and the stock `GCPlayer` prefab.
  This is the idempotent reset already shipped (§2.1) — it moves stale example scenes / blocking
  folders to the Trash and regenerates the canonical scene; here it simply wires the template.
  The template runs end-to-end and `Debug.Log`s the flow — the honest "wiring demo."
- **Action B — "Wire example game"** (new): requires the template scene present (template-first).
  It swaps the wired component from `GCExampleTemplate` to `GCExampleGame` and the `playerPrefab`
  from the stock capsule to a `GCExamplePlayer` prefab, upgrading the same scene in place. It
  never overwrites existing files (ADR 0016): if the target files exist it blocks/warns, exactly
  like the current `CreateAndWireGameBlocksGCExampleScriptPathCollisionsWithoutOverwrite` behavior.

New surface: one Start Screen action + one menu item + one readiness row + result mapping
(mirrors `FromExampleSceneCreationResult`, `Editor/GamingCouchStartScreenSetupActions.cs:296`).

### 4.3 Naming (resolved — D6, revised 2026-07-11)

Two self-contained game scripts plus the example player, all under a consistent **`GCExample…`**
prefix so they stay clearly distinct from the package's built-in **`GCGame` / `GCGameVersus`**
game-mode types (`Runtime/Game/GCGame.cs:38`, `GCGameVersus.cs:8`) — a *different* concept.

| Role | Generated name | Master-copy name |
|---|---|---|
| Barebones template (the `listener`) | `GCExampleTemplate` | `GCExampleTemplateSource` |
| Full example game (the `listener`) | `GCExampleGame` | `GCExampleGameSource` |
| Player (full game only) | `GCExamplePlayer` (renamed from `GCPlayerExample`) | `GCExamplePlayerSource` |

No adapter or base-class types — cut in the 2026-07-11 revision (§4.1). The template uses the
stock `GCPlayer`, so it needs no custom player type. The `Source` suffix on each master copy is
mandatory: `FindTypeByName` matches by *simple* name across all loaded assemblies (§5.1), so a
master must not share a simple name with its generated output. The generator strips `Source` on
copy; the golden test (§5.3) compares post-rewrite. The `GCPlayerExample`→`GCExamplePlayer` rename
ripples into some tests and ADR 0016 wording — move them together (§6, §7).

---

## 5. Maintainability & generation strategy

### 5.1 Kill string templates → canonical source + copy

Move the example source out of string literals into **real, checked-in `.cs` files** that are
the single source of truth. Generation becomes **copy-with-header-injection** instead of
string concatenation.

- The generator reads the canonical text and writes it to the user's project (injecting
  `ExampleTemplateHeader`, keeping fixed class names). No behavioral change to the
  never-overwrite rule.
- **Feasibility caveat (settle in D8).** This repo is a *bare* UPM package — no `Assets/`, no
  `ProjectSettings/`, no `files`/`.npmignore` allowlist — so **every file under `Editor/` and
  `Runtime/` ships to consumers.** A folder like `Editor/ExampleSource/` therefore ships too;
  asmdef `defineConstraints` only stops *compilation*, not shipping. And if a consumer ever set
  the guard define *while* having generated the example, the canonical `GCGameExample` /
  `GCPlayerExample` and the generated copy would be **duplicate global types → compile error**.
  So a "define-gated `Editor/ExampleSource` asmdef, compiled only in dev" is not as clean as it
  sounds. Sounder options: **(a)** compile the canonical source in a **dedicated, clean host
  project** (one that never runs example generation) that the CI/open-editor bridge points at —
  avoids both shipping-as-live-code and collision; **(b)** give the canonical copy a **distinct
  namespace + type names** and have the generator rewrite them on copy (the equality test in
  §5.3 then compares post-rewrite); **(c)** keep canonical source in a Unity-ignored `…~/`
  folder (never imported, never shipped as code) and compile it only via (a). Recommend (a)+(c).
- **Update (2026-07-11): this collision is no longer hypothetical — it is demonstrated.** The
  generator's own guard `FindTypeByName(typeName)` (`GamingCouchActiveSceneSetup.cs:2253`, called at
  `:1633`) matches by *simple type name across every loaded assembly* and refuses generation —
  *"Cannot create … a compiled type named X already exists outside the generated script path"* —
  before C# ever sees a duplicate. It bit the test fixtures today: they were named
  `GCGameExample`/`GCPlayerExample` and had to be renamed `GCExampleGameFixture`/
  `GCExamplePlayerFixture` (commit `e3cb4d7`). So any canonical-source approach that keeps the real
  names `GCGameExample`/`GCPlayerExample` loaded in the generating editor **blocks generation via
  this guard**, not merely fails compilation — which validates recommended options (b) distinct-
  names-with-rewrite and (a) clean host project.

### 5.2 Compile guarantee (the core anti-drift net)

The canonical example **must compile against the real runtime assembly** in CI/dev. Today
nothing does (confirmed: no `CSharpCodeProvider`/Roslyn/reflection cross-check anywhere in
`Tests/`). This is the single highest-value change — a runtime API rename then breaks the
build, exactly the failure the current suite misses.

> **New precedent (2026-07-11, commit `a5b313d`).** The repo now ships a **non-shipping runtime
> test assembly** `GamingCouch.Tests.Fixtures` (`Tests/Fixtures/`: `autoReferenced:false`,
> `defineConstraints:["UNITY_INCLUDE_TESTS"]`, references only `GamingCouch`) holding
> `GCExamplePlayerFixture` and `ColorPlaceholderPrefabPlayer`, both `public … : GCPlayer` compiled
> against the real runtime. This is a working template for exactly the compile-guarantee mechanism
> above and informs D8's canonical-source home. Caveat: "nothing compiles" stays true for the
> *generated source*; the new assembly compiles fixture stand-ins, not the canonical example.

### 5.3 Replace text-duplication tests with a golden comparison

Delete the hardcoded substring assertions and replace them with a **full-text comparison**:
assert the generator output **equals** the canonical source (normalized for header/line
endings). We can reuse the *fixture-corpus infrastructure* of `ContractFixtures` +
`GCDevJsonContractFixtureTests.cs` (case discovery, package-vs-in-repo path resolution), but
note that corpus does structured + substring asserts (`AssertWrittenText`), **not** full-text
equality — so this equality check is stricter than anything the suite does today; we reuse the
plumbing, not an existing mechanism. One source of truth; generation drift fails a test.

---

## 6. Test strategy

Layered, cheapest-first:

1. **Compile guarantee** (§5.2) — canonical example compiles against runtime in CI/dev.
   Catches API-shape drift. *New capability.*
2. **Generation equality test** (§5.3) — generator output == canonical source (full-text,
   normalized). Catches generator/source divergence. Replaces the brittle substring asserts.
3. **Game-logic unit tests (EditMode)** — drive `GCExampleGame`'s rules directly (scoring,
   finish/eliminate, round completion) where feasible. Because the game reads
   `GamingCouch.Instance` directly (D3, no facade), this layer is thin — we lean on 1/2/4.
4. **Lifecycle behavioral test** — a **PlayMode smoke test** (D3): instantiate the wired scene
   and pump frames through setup → play → input → game-over, asserting the platform calls happen
   in order. New asmdef; the bridge already supports `--mode PlayMode`.
5. **Editor action tests** — the new "Wire example game" action: swapping the wired component
   (`GCExampleTemplate` → `GCExampleGame`) and the player prefab, never-overwrite blocking,
   blocked-when-template-scene-missing, result→message mapping. Extends
   `GamingCouchActiveSceneSetupAssetTests` / `GamingCouchStartScreenSetupActionsTests`. Model
   these on the existing `CreateAndWireGameBlocksGCExampleScriptPathCollisionsWithoutOverwrite`
   (`GamingCouchActiveSceneSetupAssetTests.cs:374`) and the new
   `RemoveBlockingExampleAssetFoldersClearsScriptPathCollisions` (`:401`) tests.

> D3 resolved (§9): **no injectable facade.** The game reads `GamingCouch.Instance` directly,
> so layer 3 stays thin and coverage rests on the compile guarantee (1), the golden generation
> test (2), and the PlayMode smoke test (4). A facade may be revisited only if the smoke test
> proves too coarse.

---

## 7. Implementation phases (Phase 0 done)

> For the granular, checkbox-level breakdown a fresh agent should execute, see **§11** — it
> regroups these phases into ordered task groups A–E with acceptance criteria.

- **Phase 0 — Grill & decide. ✅ DONE (2026-07-11).** §9 resolved; **ADR 0017** drafted
  (`docs/adr/0017-example-self-contained-games.md`), superseding/extending ADR 0016.
- **Phase 1 — Split.** Refactor the current single example into two self-contained scripts —
  `GCExampleTemplate` (barebones, stock `GCPlayer`) and `GCExampleGame` (full, with
  `GCExamplePlayer`, renamed from `GCPlayerExample`) — each implementing the platform contract
  directly, preserving today's behavior/parity. No adapter or base class. All generated Example
  Assets — no new package public API (D1).
- **Phase 2 — Generation.** Move source to canonical checked-in files; convert generator to
  copy; add golden test; delete substring-duplicate asserts.
- **Phase 3 — Guards.** Add the compile guarantee (in-repo `UNITY_INCLUDE_TESTS`-gated master
  assembly modeled on `GamingCouch.Tests.Fixtures`, D8) and the behavioral lifecycle test as a
  PlayMode smoke test (no facade — D3).
- **Phase 4 — Additive action.** Add "Wire example game" (Start Screen + menu + readiness row
  + result mapping) and its tests; enforce template-first and never-overwrite.
- **Phase 5 — Docs.** Update README / CONTEXT.md / ADR 0017; resolve the "complete game vs
  wiring demo" tension; mark backlog B004 addressed and note B001/B002/B006 relations.

Each phase should stay green on the host-project test run
(`unity test <host-project> --mode EditMode`, and `--mode PlayMode` once Phase 3 lands).

---

## 8. Risks & constraints

- **R1. Runtime invariants** (§2.2) must survive: one `GamingCouch`, non-null `listener` with
  both magic methods, `playerPrefab` with `T`+`GCPlayer`. Each self-contained game declares
  `GamingCouchSetup`/`GamingCouchPlay` on itself, so the readiness gate is satisfied directly
  (no base class needed).
- **R2. Never-overwrite (ADR 0016).** Action B and re-runs must not clobber user-edited files;
  additive-only + collision blocking. New first-class remediation (2026-07-11):
  `FindBlockingExampleAssetFolders`/`RemoveBlockingExampleAssetFolders`
  (`GamingCouchActiveSceneSetup.cs:513`/`:534`, result type `GCExampleAssetFolderCleanupResult`
  `:144`) moves *folders* blocking the script paths to the Trash (recoverable) but still
  reuses-never-overwrites existing script/prefab files — Action B must lean on this, not fight it.
- **R3. Unity version matrix.** 6000.0 → 6000.5; serialized-reference reads differ on 6000.3+
  (`objectReferenceEntityIdValue`, `GamingCouchSceneWiring.cs:235-243`). New inspector/wiring
  must handle both. Netcode still compiles only on WebGL target (per project memory).
- **R4. `GC_HAS_UGUI` gating** — the Game View overlay is uGUI-gated
  (`GamingCouchExampleSceneCreation.cs` — `CreateSceneInfoOverlay` `:333`, `#if GC_HAS_UGUI`
  `:335`/`:384`, called from `:158`); keep the template usable without uGUI.
- **R5. PlayMode tests are new infrastructure** (all current tests are EditMode). Scope the
  smoke test tightly; the bridge supports PlayMode but CI cost/flakiness must be watched.
- **R6. Public-API creep.** Resolved: D1 (all generated) and D3 (no facade) add **no** package
  public API, so no `gameProtocolVersion` question arises → stays 1 (D7).
- **R7. Singleton coupling.** `GamingCouch.Instance` makes pure unit testing of game rules hard.
  Accepted (D3, no facade): coverage rests on the compile guarantee, golden test, and PlayMode
  smoke test instead of fast pure unit tests.
- **R8. Naming/type collisions.** Renames ripple into existing tests and ADR 0016 wording
  (move together). The new `GCExample…` types must not overload the existing public
  `GCGame` / `GCGameVersus` game-mode types (`Runtime/Game/`) — see D6.
- **R9. Bare-package shipping.** No `files`/allowlist means the canonical-source assembly ships
  to consumers as-is. Resolved (D8): a `UNITY_INCLUDE_TESTS`-gated `autoReferenced:false`
  assembly keeps it out of player builds, and distinct `…Source` names avoid the `FindTypeByName`
  collision with the generated copy (§5.1) — no clean host project needed.

---

## 9. Decisions (resolved — grill 2026-07-11)

All nine settled in a plain-language grill session. Each landed on the plan's recommendation:
the example stays **fully generated**, kept drift-proof by an **in-repo compiled master copy**.
A follow-up revision (2026-07-11) simplified the structure — see D1/D6: no adapter, no base
class, just two self-contained swappable game scripts (§4.1).

- **D1. Ownership → RESOLVED: generated Example Assets.** The whole example (two self-contained
  game scripts + the player) is generated into the user's project as editable code; **no new
  package public API**, and no adapter/base-class layer (revised 2026-07-11 — §4.1). Reversible
  pre-B001, where a reusable contract could be introduced. (Bare-package shipping means the master
  copy ships regardless; D8 keeps it from colliding or compiling into player builds.)
- **D2. Generation mechanism → RESOLVED: canonical source + copy-with-rename.** String templates
  are retired. The generator copies the compiled master file and rewrites its names on the way
  into the user's project. Follows directly from D8. (`Samples~` still rejected.)
- **D3. Testability depth → RESOLVED: no facade.** Game logic keeps calling `GamingCouch.Instance`
  directly (matches today; simplest generated code). Drift/behaviour is covered by the compile
  guarantee (§5.2) + generation-equality test (§5.3) + a PlayMode smoke test (§6.4); revisit a
  facade only if the smoke test proves too coarse. No `IGCGameContext` for now.
- **D4. Setup flow → RESOLVED: additive, one scene.** Action A ("Create example scene") resets to
  a single clean scene wired to `GCExampleTemplate`; Action B ("Wire example game") upgrades that
  same scene in place to `GCExampleGame`. Re-running A returns to the template baseline (idempotent
  reset, §2.1). Separate scenes rejected.
- **D5. Template scope → RESOLVED: spawns stock players.** `GCExampleTemplate` instantiates the
  stock `GCPlayer` capsule so it runs end-to-end (setup → play → input → game-over) right after
  Action A; Action B swaps in `GCExampleGame` + the richer `GCExamplePlayer`.
- **D6. Naming → RESOLVED (revised 2026-07-11): consistent `GCExample…` family.** Generated types:
  `GCExampleTemplate` (barebones template) · `GCExampleGame` (full playable game) · `GCExamplePlayer`
  (player for the full game, **renamed** from `GCPlayerExample`). No adapter or base-class types.
  All prefixed `GCExample` to stay clearly distinct from the package's built-in `GCGame` /
  `GCGameVersus` game-mode types. The in-repo master copies carry a `Source` suffix (e.g.
  `GCExampleGameSource`) so their simple names differ from the generated ones — required because
  `FindTypeByName` matches by simple name across all loaded assemblies (§5.1) — and the generator
  strips `Source` on copy.
- **D7. `gameProtocolVersion` → RESOLVED: stays 1.** No change to the platform↔game wire contract;
  this is an example reorganization only.
- **D8. Canonical-source home + compile mechanism → RESOLVED: in-repo define-gated assembly +
  rename.** The master copy lives in a `UNITY_INCLUDE_TESTS`-gated, `autoReferenced:false`
  assembly modeled on today's `GamingCouch.Tests.Fixtures` — it compiles against the real runtime
  in the editor/CI but never compiles into a consumer player build. Distinct `…Source` names +
  generator-side rewrite (D6) avoid the `FindTypeByName` collision. The compile guarantee then
  falls out of the normal test run — **no separate clean host project needed.**
- **D9. Docs stance → RESOLVED: two tiers, fix the contradiction.** Codify "debug game = wiring
  demo; full example game = small playable loop" and fix the README ("complete, working game")
  vs CONTEXT ("wiring demo, not a full sample game") contradiction.

---

## 10. Alignment: ADRs, backlog, docs

- **ADR 0016** — extended/superseded by **ADR 0017** (`docs/adr/0017-example-self-contained-games.md`,
  drafted 2026-07-11: two self-contained swappable games + additive example-game action +
  canonical-source generation). Preserves 0016's editable/never-overwrite/no-surprise-Build-Settings
  properties; supersedes only its create-a-scene-alongside clause (now an idempotent reset).
- **Backlog** — directly advances **B004** (generated examples demonstrate a playable loop —
  promoted into this plan) and part of **B001** (authoring ergonomics); relates to **B002**
  (player-inputs convenience — the facade that would have advanced it was declined in D3, so B002
  stays separate) and **B006** (Start Screen new-vs-port paths — Action A/B are a step toward that
  distinction).
- **Vocabulary (CONTEXT.md)** — reuse *Start Screen*, *Active Scene Setup*, *Example Assets*;
  avoid *setup wizard*, *generated scene setup*, *package samples*. Settled terms (D6):
  *Example Template* (`GCExampleTemplate`, the barebones starting point), *Example Game*
  (`GCExampleGame`, the full playable game), *Example Player* (`GCExamplePlayer`).

---

## 11. Execution task tracker (for implementation)

Work top-to-bottom; keep each group green before moving on
(`unity test <host-project> --mode EditMode`; add `--mode PlayMode` once Group D
lands). Honor the guardrails below and in §8 / ADR 0016 / ADR 0017 throughout. Names per §4.3.

### Group A — Canonical master source + compile guarantee (Phase 1 + compile half of Phase 3)
- [x] **A1.** Add a non-shipping master assembly, e.g. `Tests/ExampleCanonical/GamingCouch.Tests.ExampleCanonical.asmdef` — `autoReferenced:false`, `defineConstraints:["UNITY_INCLUDE_TESTS"]`, `references:["GamingCouch"]`. Model on `Tests/Fixtures/GamingCouch.Tests.Fixtures.asmdef`. Put the code in a distinct namespace (e.g. `DSB.GC.ExampleCanonical`). — DONE (`Tests/ExampleCanonical/`, namespace `DSB.GC.ExampleCanonical`).
- [x] **A2.** Author `GCExampleTemplateSource.cs` — barebones, self-contained: implements `GamingCouchSetup(GCSetupOptions)` / `GamingCouchPlay(GCPlayOptions)` directly, spawns the stock `GCPlayer` (`SetupPlayers<GCPlayer>`), calls `SetupGameVersus`/`SetupDone`/`GameOver`, and `Debug.Log`s each lifecycle step. The absolute minimum that completes the platform loop. — DONE (HUD kept on `None` value/meter so stock `GCPlayer.GetHudValueText()` is never invoked).
- [x] **A3.** Author `GCExampleGameSource.cs` (full game — port today's scoring/rounds/input/HUD logic out of the string template) and `GCExamplePlayerSource.cs` (custom player — port today's `GCPlayerExample`). — DONE (faithful ports; `Source`-suffixed type names).
- [x] **A4.** *Acceptance:* the assembly compiles against the real runtime in editor/CI — this IS the compile guarantee (§5.2). Sanity-check by temporarily renaming a runtime API the sources use (e.g. `GCPlayer.SetLives`) and confirming it now breaks the build; revert. — DONE: `GamingCouch.Tests.ExampleCanonical.dll` builds clean against the real runtime; full EditMode suite green. (Explicit rename-a-runtime-API break-test not yet run — guarantee is proven by construction + the successful compile; can run on request.)

### Group B — Generator: canonical copy-with-rename + golden test (Phase 2)
- [x] **B1.** Replace the string-literal builders `BuildGameScriptSource` (`Editor/GamingCouchActiveSceneSetup.cs:2286`) / `BuildPlayerScriptSource` (`:2461`) with a reader that loads each `…Source.cs` master, strips the `Source` suffix from type/file names, rewrites the namespace to the generated one, and injects `ExampleTemplateHeader` (`:324`). — DONE: shared `RewriteCanonicalMasterToGeneratedSource` (drops leading comment → first `using`, strips the `DSB.GC.ExampleCanonical` namespace + de-indents one level, applies the `…Source`→generated name map, prepends the header); `ReadCanonicalMasterSource` locates the master via `PackageInfo.FindForAssembly(...).resolvedPath` with in-repo walk-up fallback.
- [x] **B2.** Generation emits `GCExampleTemplate.cs`, `GCExampleGame.cs`, `GCExamplePlayer.cs` (+ `GCExamplePlayer.prefab`) under `Assets/GamingCouch/GCExample/`. Update the path consts (`:301-311`) and the `FindTypeByName` guard usage (`:1633`) for the new names. — DONE: consts renamed to `GCExampleGame`/`GCExamplePlayer` + template consts added; `GenerateExample{Template,Game,Player}Source` factories. The `FindTypeByName` guard is driven by `spec.gameTypeName`/`playerTypeName`, so it now checks the new names automatically; the `…Source` master names never collide. (Which action generates the template vs game is Group C; B keeps today's full-game default for parity.)
- [x] **B3.** Add a golden equality test: generated output **==** master after name-rewrite (normalized for header/EOL). Delete `AssertGeneratedGameSourceDemonstratesPlayFlow` + its ~42 substring asserts (`GamingCouchActiveSceneSetupAssetTests.cs:1038`); reuse the ContractFixtures path-resolution plumbing (§5.3). — DONE: `AssertGeneratedEqualsCanonicalMaster` (full-text equality vs the shared transform) + `AssertGeneratedSourceShape` (header present, class renamed, no `Source` suffix, no canonical namespace) for game/player/template; the ~42 substring asserts + 3 helpers deleted.
- [x] **B4.** Ripple the `GCGameExample`→`GCExampleGame` and `GCPlayerExample`→`GCExamplePlayer` renames through tooling, tests, prefab type/GUID references, and ADR 0016 wording. *Acceptance:* EditMode suite green; generating into a scratch project yields compiling files identical to the masters modulo names + header. — DONE (code + 4 test files + fixture comments + ADR 0016). Prefab: generated at runtime from the compiled `playerType`, so no checked-in prefab GUID to rewrite. README/CONTEXT left for Group E. **EditMode suite verified green (manual Test Runner run, 2026-07-12).**

### Group C — Additive "Wire example game" action (Phase 4)
- [x] **C1.** "Create example scene" (the reset action, `Editor/GamingCouchExampleSceneCreation.cs`) wires `GCExampleTemplate` + the stock `GCPlayer` prefab. — DONE + EditMode green (2026-07-12): template-flavor spec selected by a flavor-aware `GetScriptSetupSpec`; stock `GCPlayer` type resolved directly (no player script); scene-info/guidance retargeted; collision test retargeted to the template path.
- [x] **C2.** New action `WireExampleGame`: on a template scene, swap the listener component `GCExampleTemplate`→`GCExampleGame` and `GamingCouch.playerPrefab` from the stock capsule to the `GCExamplePlayer` prefab — in place. Template-first guard (block + message if no template scene); never-overwrite (ADR 0016); reuse `Find/RemoveBlockingExampleAssetFolders` for blocking folders. Add a result type + message mapping mirroring `FromExampleSceneCreationResult` (`GamingCouchStartScreenSetupActions.cs:296`). — DONE + EditMode green (2026-07-12): `GCWireExampleGameResult`, `WireExampleGame()` + `TryGetTemplateSceneGamingCouch` guard, two-phase `SwapListenerToExampleGameOnScriptsReady` (component swap + `ReplacePlayerPrefab`), `FromWireExampleGameResult` mapping.
- [x] **C3.** Surface it: Start Screen button (near `GamingCouchStartScreenWindow.cs:205`), menu item (`GamingCouchMenuItems.cs:19`), one readiness row. — DONE (button + menu item + `GCStartScreenReadinessActionId.WireExampleGame` + `RunSetupAction` case). **Readiness checklist row intentionally skipped** (user decision 2026-07-12): a template scene is a valid ready state, so a readiness row would misrepresent it as incomplete; the button + menu are the surface.
- [x] **C4.** Editor tests: swap wiring, never-overwrite blocking, blocked-when-template-missing, result→message. Model on `CreateAndWireGameBlocksGCExampleScriptPathCollisionsWithoutOverwrite` (`…AssetTests.cs:374`) and `RemoveBlockingExampleAssetFoldersClearsScriptPathCollisions` (`:401`). — DONE: two template-first guard tests + `WireExampleGameResultMapsStatusesAndPreservesDetails`; the collision/never-overwrite test was retargeted to the template path in C1; `WireExampleGameBlocksWhenSceneIsNotTemplateScene` drives the real `WireExampleGame()` entry point's blocked path; and `WireExampleGameScriptsReadySwapReplacesListenerComponentAndPlayerPrefab` drives the actual swap continuation `SwapListenerToExampleGameOnScriptsReady` (made `internal`) with compiled fixture stand-ins, asserting the listener component upgrade + `playerPrefab` repoint (`SwapActiveSceneListenerComponentToExampleGame` + `ReplacePlayerPrefab`). **Correction (2026-07-12 review):** an earlier draft of this note claimed the swap was "covered end-to-end by the Group D PlayMode smoke test" — that was false (the PlayMode test builds the scene by hand and never calls `WireExampleGame`); the new EditMode swap-continuation test above is the real coverage. Still manually verified only: the post-domain-reload dispatch itself and template-component removal (keyed to the generated `GCExampleTemplate` name, which by design has no compiled stand-in).

### Group D — PlayMode smoke test (behavioral half of Phase 3)
- [x] **D1.** New PlayMode test asmdef (all current tests are EditMode — R5). Instantiate the wired `GCExampleScene`, pump frames through setup → play → input → game-over, and assert the platform calls fire in order. Keep it tight. Runs via the bridge `--mode PlayMode`. — DONE + **verified green (2026-07-12, full PlayMode + EditMode suites run in Test Runner)**. New `Tests/PlayMode/GamingCouch.Tests.PlayMode.asmdef` (+ metas); `[assembly: InternalsVisibleTo("GamingCouch.Tests.PlayMode")]` added in `Runtime/GamingCouch.cs`; `GCExampleGamePlayModeSmokeTests.ExampleGameDrivesSetupPlayInputAndGameOver` drives the real editor-play lifecycle on `GCExampleGameSource` (+ a `GCExamplePlayerSource` prefab) via a faked `GCLocalPlaySession` capture, asserting 2 players spawn → Playing → primary input scores → `GCStatus.GameOver`. First PlayMode run hit "Game already set" (a manual `SendMessage("GamingCouchSetup")` double-ran setup because `GamingCouch.Start()` already drives it); fixed by removing the manual trigger — the runtime's `Start()` now drives setup→play→game-over from the pre-cached fake capture, and the re-run is green.

### Group E — Docs (Phase 5)
- [x] **E1.** Fix README (`:80`, "a complete, working game") and CONTEXT.md (`:106`) to the two honest tiers: template = wiring demo; `GCExampleGame` = small playable loop. — DONE (2026-07-12): README example callout + "What next?" + Documentation list retargeted to `GCExampleTemplate` (+ `Wire Example Game` → `GCExampleGame`/`GCExamplePlayer`); CONTEXT "Example Assets" vocabulary now states both tiers. No test asserts this doc content (the only README test, `WebGLDocsDoNotDuplicateManualBuildSettingLists`, checks unrelated WebGL phrasing).
- [x] **E2.** Mark backlog B004 addressed; ADR 0017 (`docs/adr/0017-example-self-contained-games.md`) already records the decision. — DONE: B004 note in `gamingcouch-unity-backlog.md` marked addressed by the example refactor, deferring remaining ergonomics to B001.

### Guardrails (every group)
- **Never overwrite** user-editable generated files; block + warn on collision (ADR 0016).
- **Never change Build Settings** silently — first-enabled-scene promotion stays opt-in via "Set up missing pieces".
- `gameProtocolVersion` **stays 1**; do not touch the platform↔game wire contract (ADR 0008).
- **No adapter, no base class, no `IGCGameContext` facade** (D1/D3/§4.1) — two self-contained scripts.
- Master types keep the `Source` suffix; the generator strips it (avoids the `FindTypeByName` collision, §5.1).
- Keep the template usable **without uGUI** (`GC_HAS_UGUI` gating, R4).
- Netcode compiles only on the WebGL target; prefab test fixtures live in a runtime `Tests/Fixtures`-style assembly (project memory).
- Rebake `Runtime/Resources/GamingCouchRuntimeInfo.json` only if a version bump happens (not required by this work).
```
