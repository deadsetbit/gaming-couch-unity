# Portable State Kernel + Lifecycle Guardrails — Plan (grill starting point)

Status: Draft for grilling — **do not implement yet**
Owner: Gaming Couch Unity package team
Created: 2026-07-11
Related: CONTEXT.md (`:138` portability doctrine), `docs/contracts/platform-runtime-contract.md`,
ADR 0008 (`gameProtocolVersion` stays 1), ADR 0009 (two-sidecar identity), `VERSIONING_PLAN.md`,
`example-adapter-refactor-plan.md` (sibling seam work), `ContractFixtures/` + `GCDevJsonContractFixtureTests`.

> Written to be torn apart in a grill session. It states a recommended direction plus the open
> decisions that must be settled before any code changes. Everything is grounded in the current
> codebase (file:line citations).

---

## 0. Settled direction (from the kick-off decision)

Three forks were decided up front; the rest of this plan is built on them:

- **D-SCOPE = Unity-first, contract-out.** This plan executes entirely in *this* repo: extract the
  Unity-free core, add the guardrails, and author the shared **spec** as the artifact other engines
  consume. Godot/JS/other ports get their own downstream plans; they are **not** tasks here.
- **D-BUILD = Seams now, corpus later.** Build the core, the guardrails, the spec doc, and a *thin*
  vector-emit hook with a handful of sample vectors. **Defer** the full versioned golden-vector corpus
  and the cross-language conformance runner until a second engine is actually in flight — but leave
  clean, documented seams so that work is turnkey.
- **D-GUARD = Ignore + emit diagnostic.** New lifecycle/state-rule violations are **non-breaking**:
  reject the illegal call, do nothing, emit a stable `gc.diagnostic`. Matches the existing
  post-game-over-mutation and game-over-twice guards. `gameProtocolVersion` stays 1.

---

## 1. Purpose & scope

**Purpose.** Package the Gaming Couch *runtime state logic* — the player transition rules, the game
lifecycle state machine, the outbound wire protocol, and their guardrails — as a **Unity-free C# core**
that doubles as the **reference implementation** and the source of a **language-neutral specification**.
This lets Godot/JS/other integrations execute the *same* state logic and versioning while wrapping it in
their own idiomatic DX, exactly as CONTEXT.md already mandates for the Local Play Contract:

> "portable" means preserving the contract and fixtures across engine packages, **not forcing shared
> implementation code.** — `CONTEXT.md:138`

**Also in this plan:** close the guardrail gap the investigation surfaced — the *per-player* state
machines are richly guarded, but the *game lifecycle* (`PendingSetup → SetupDone → Playing → GameOver`)
is advanced imperatively with **no illegal-order guard**. Formalizing the kernel is the moment to pin
the lifecycle down.

**In scope:** the outbound state model + rules + protocol + diagnostics core; a new `dsb.gamingcouch.core`
assembly; lifecycle guardrails; the portable state contract doc; a thin vector-emit hook + sample vectors;
rehoming the headless tests; ADR + docs.

**Out of scope (this plan):**
- Inbound parsing/inputs (`GCSetupOptions`/`GCPlayOptions`/`GCControllerInputs*`) — stays in runtime.
- The Godot/JS implementations and the full cross-language corpus/runner (deferred; seams only).
- Any change to the platform ↔ `GamingCouch` **wire bytes** or `gameProtocolVersion` (stays 1, ADR 0008).
- The example-adapter refactor (`example-adapter-refactor-plan.md`) — sibling effort; must not collide.

---

## 2. Current state (grounded)

### 2.1 Already Unity-independent (the extractable kernel)
- **`GCPlayerTransitions.cs`** — pure static functions returning `Accepted / NoOp / Rejected + reason`
  (`GCPlayerTransitionRejectionReason { DuplicateValue, InvalidTransition, InvalidRevoke }`). Only Unity
  dependency is `Time.time` for `changedAtGameTime` (`:531, :624, :644, :679`).
- **`GCRuntimeMessages.cs`** — hand-built JSON (`GCRuntimeJson.AppendString` `:1006`), envelope + record
  builders, state-snapshot schema, the versioned envelope (`EnvelopeVersion = 1` `:663`, emitted `:797`;
  screen-space `:910`). Emits via a plain C# event `RuntimeMessagesEmitted` (`:778`); the WebGL transport
  is `#if`-gated `[DllImport]` (`:675-678, :780-782`). Time is an injectable provider (`:860`).
- **The game-over-twice guard** — `GCRuntimeOutput.TrySubmitGameOverPlacement` (`:155-159`), flag set at
  `:193`, reset per run in `HandleActiveRunBegan` (`:208-213`).
- **`GCActiveRunProjection.cs`, `GCRuntimeInfo.cs`** — pure POCO, no `UnityEngine` import.
- **`GCDiagnostics.cs`** — POCO record builders + code/severity validation; the only Unity parts are the
  `Debug.Log*` mirror (`:625-636`) and `Application.logMessageReceived` capture.

### 2.2 The hard seam (where state is *stored* / *phased*)
- **`GCPlayer.cs`** is a `MonoBehaviour` (`:82`) holding the mutable fields, and it reads the phase via the
  `GamingCouch.Instance` singleton. The post-game-over mutation guard `TryAllowMutation` (`:739-754`) emits
  `gc.state.post_game_over_mutation`.
- **Game phase** is a bare enum inside the orchestrator: `GCStatus { PendingSetup, SetupDone, Playing,
  GameOver }` (`GamingCouch.cs:25`), advanced imperatively — `SetupDone` sets it (`:667`), `Play` sets
  `Playing` (`:442`), the game-over callback sets `GameOver` (`:746`). **No guard rejects out-of-order
  calls** (e.g. `Play` before `SetupDone`).
- **Snapshot + placement read the concrete `GCPlayer`.** `GCRuntimeStateSnapshotBuilder.BuildPayload`
  takes `IReadOnlyList<GCPlayer>` (`GCRuntimeMessages.cs:321`); placement sorting reads `GCPlayer`
  properties (`GCGame.cs:117-165`). Moving these to core requires a **pure player-state view** so the core
  never references the MonoBehaviour.

### 2.3 Versioning that already exists (do not duplicate)
Envelope `"v":1` (`:663/:910`); `GameProtocolVersion = 1` (`GCEditorPackageIdentity.cs:34` →
`GCRuntimeInfo.gameProtocolVersion` `:13`); `GCPlayOptions.CurrentSchemaVersion = 1`. Diagnostic codes are
a de-facto versioned contract (`KnownCodes` validation).

---

## 3. Goals & non-goals

**Goals**
- G1. A `dsb.gamingcouch.core` assembly with **no `UnityEngine` reference** holding the transition rules,
  lifecycle state machine, wire protocol + versioning, and diagnostics record model.
- G2. **Behavior-preserving extraction** — the shipped Unity build behaves identically after Phase 1
  (existing headless tests pass unchanged).
- G3. **Lifecycle guardrails**: illegal phase transitions are rejected + emit a stable diagnostic
  (ignore + emit, per D-GUARD).
- G4. A **portable state contract doc** that fully specifies §G1 as the artifact Godot/JS implement against.
- G5. **Conformance seams**: a vector-emit hook + sample vectors + a Unity test that runs them, with the
  full corpus/runner explicitly deferred and documented.

**Non-goals**
- N1. No wire-byte or `gameProtocolVersion` change (stays 1; confirm in grill).
- N2. No second-engine implementation and no full corpus/runner (deferred).
- N3. No inbound (setup/play/inputs) portability in this plan.
- N4. Not a rewrite — Phase 1 moves and re-wires existing, reviewed code; it does not re-author it.

---

## 4. Proposed architecture

```
        dsb.gamingcouch.core  (netstandard2.1, NO UnityEngine)         dsb.gamingcouch.runtime (Unity)
        ┌───────────────────────────────────────────────┐             ┌──────────────────────────────┐
        │ Rules:      GCPlayerTransitions (pure)          │  refs       │ GamingCouch : MonoBehaviour   │
        │ Lifecycle:  GCGameLifecycle (NEW state machine) │◄────────────┤  - owns Unity wiring          │
        │ Protocol:   GCRuntimeMessages* + envelope + v   │             │  - supplies clock (Time.*)    │
        │ Diagnostics:record model + codes + validation   │             │  - WebGL [DllImport] transport│
        │ Views:      IGCPlayerStateView (NEW seam)       │             │ GCPlayer : MonoBehaviour       │
        │ Projections:GCActiveRunProjection, GCRuntimeInfo│             │  - implements the view        │
        │ Emit:       events (RuntimeMessagesEmitted …)   │────emit────►│  - Debug.Log mirror, capture  │
        └───────────────────────────────────────────────┘             └──────────────────────────────┘
                         ▲ vector-emit hook                                     ▲ subscribes to events
                         │ (thin, Phase 4)                                      │ + jslib externs
                 ContractFixtures/runtime-state/*.json  (sample vectors)
```

Four seams make the split clean and are the load-bearing design choices to grill:

1. **`IGCPlayerStateView`** (new). A read-only projection of a player's state — `Index, Score, Lives,
   Status, StatusText, Meter, EliminationState, FinishState`, plus the placement-timing fields
   (`IsEliminated`, `LastSetEliminatedGameTime`, `IsFinished`, `LastSetFinishedGameTime`). `GCPlayer`
   implements it; the core snapshot builder + placement comparator consume it. **Nothing in core references
   `GCPlayer`.**
2. **`GCGameLifecycle`** (new, pure). Owns the `GCStatus` value + a `TryTransition(current, requested) →
   outcome + reason` guard. `GamingCouch` routes every phase change through it and only mutates its status
   on `Accepted`; a rejection emits a diagnostic. This is where the missing guardrails live.
3. **Deterministic clock.** The transition functions stop calling `Time.time` internally; `changedAtGameTime`
   becomes a **parameter** supplied by the Unity layer. Core is deterministic (essential for vectors); the
   `realtimeSecondsProvider` default that references `Time.*` moves to the Unity bootstrap.
4. **Transport by subscription.** The `[DllImport("__Internal")]` externs move out of core into a Unity-side
   transport that subscribes to `RuntimeMessagesEmitted` / `ScreenSpaceEmitted` and forwards to the jslib.
   Core already exposes the events; this just relocates the sink.

`Mathf.Clamp/Clamp01` → `System.Math.Clamp`. `GCDiagnostics` splits: record-building + code validation +
event emission in core; `Debug.Log` mirror + `Application.logMessageReceived` capture stay in runtime.

---

## 5. Execution order & task trackers

**Dependency order:** Phase 0 → 1 → 2 → 3 → 4 → 5. Phase 3 (spec doc) may begin in parallel with Phase 2
once the Phase 1 seams exist, but must not finalize until Phase 2's new codes are known. Each phase must
stay green on the open-Editor test bridge (`Tools/run-open-unity-tests.py … --mode EditMode`).

Status legend: `[ ]` todo · `[~]` in progress · `[x]` done · `[-]` dropped. IDs are stable; do not renumber.

### Phase 0 — Grill & decide
| ID | Task | Artifact / acceptance | Status |
|----|------|-----------------------|--------|
| P0-T1 | Run this plan through a grill; resolve every §9 decision | §9 all marked resolved | [ ] |
| P0-T2 | Draft **ADR 00XX — Portable state kernel + lifecycle guardrails** (core seam, D-GUARD stance, protocol stays 1) | ADR merged; supersedes nothing, references ADR 0008/0009 | [ ] |
| P0-T3 | Confirm `gameProtocolVersion` stays 1 with the wire-bytes-unchanged argument | Recorded in ADR (D7) | [ ] |

### Phase 1 — Extract `dsb.gamingcouch.core` (behavior-preserving)
| ID | Task | Artifact / acceptance | Status |
|----|------|-----------------------|--------|
| P1-T1 | Create `Runtime/Core/dsb.gamingcouch.core.asmdef` — netstandard2.1, **no `UnityEngine`/Unity refs**; runtime asmdef references it | asmdef compiles with zero Unity refs; `dsb.gamingcouch.runtime` depends on core | [ ] |
| P1-T2 | Define **`IGCPlayerStateView`** in core (fields per §4.1); `GCPlayer` implements it | Interface compiles in core; `GCPlayer : … , IGCPlayerStateView` | [ ] |
| P1-T3 | Move **transition rules + result/accepted structs + enums** (`GCPlayerTransitions`, `GCPlayerTransitionResult`, `GCPlayerAcceptedTransition`, `GCPlayerStatusValue`, `GCStatus`, `GCPlayerStatus`, elim/finish/kind/reason/outcome enums, `GCPlayerEnumNames`) to core | Files under `Runtime/Core/`; runtime still compiles | [ ] |
| P1-T4 | **Make transitions deterministic** — replace internal `Time.time` with a `float gameTimeSeconds` parameter; callers in `GCPlayer`/`GCGame` pass `Time.time` | No `Time` reference in core; behavior identical | [ ] |
| P1-T5 | Move **protocol layer** (`GCRuntimeMessages*`, envelope/record builders, snapshot builder, payloads, `GCRuntimeJson`, `GCRuntimeOutput` incl. game-over guard) to core; snapshot builder + placement comparator consume `IGCPlayerStateView` not `GCPlayer` | Core builds; `GCGame`/`GamingCouch` pass views | [ ] |
| P1-T6 | **Relocate transport** — move `[DllImport]` externs to a runtime-side `GCWebGLRuntimeTransport` that subscribes to `RuntimeMessagesEmitted`/`ScreenSpaceEmitted` | No `DllImport` in core; WebGL emit still reaches jslib | [ ] |
| P1-T7 | Replace `Mathf.Clamp/Clamp01` → `Math.Clamp` in moved code | No `Mathf` in core | [ ] |
| P1-T8 | **Split `GCDiagnostics`** — record model + codes + validation + emit-event in core; `Debug.Log` mirror + `logMessageReceived` capture stay runtime | Core emits structured diagnostics with no `Debug`/`Application` refs | [ ] |
| P1-T9 | Move **pure POCOs** `GCActiveRunProjection`, `GCRuntimeInfo`, `GCPlayerIndexMapping` to core | Compiles; runtime references them via core | [ ] |
| P1-T10 | Fix `[assembly: InternalsVisibleTo]` (`GamingCouch.cs:17-19`) + `internal` visibility across the new assembly boundary (promote to `public` in core only where the boundary requires) | No accidental new public API beyond the boundary; documented | [ ] |
| P1-T11 | **Rehome headless tests** into a `dsb.gamingcouch.core.tests` edit-mode assembly (`GCPlayerStateModelTests`, `GCRuntimeOutputContractTests`, `GCRuntimeDiagnosticsTests`, `GCActiveRunProjectionTests`); they pass **unchanged** | All green on `--mode EditMode` | [ ] |
| P1-T12 | Rebake `Runtime/Resources/GamingCouchRuntimeInfo.json` if identity moved (per project memory) + full Play-mode smoke | Play-mode run behaves identically to pre-refactor | [ ] |

### Phase 2 — Lifecycle guardrails (the new correctness)
| ID | Task | Artifact / acceptance | Status |
|----|------|-----------------------|--------|
| P2-T1 | Add **`GCGameLifecycle`** to core: owns `GCStatus`, exposes `TryTransition(requested) → outcome + rejection reason`; legal edges only (`PendingSetup→SetupDone→Playing→GameOver`) | Pure, deterministic, no Unity refs | [ ] |
| P2-T2 | Route `GamingCouch` phase changes through it — `SetupDone`/`Play`/game-over set status **only on Accepted** (`GamingCouch.cs:442,:667,:746`) | Status never advances on a rejected transition | [ ] |
| P2-T3 | Illegal transition → **ignore + emit** new diagnostic `gc.state.invalid_lifecycle_transition` (from/to in context); add to `KnownCodes` | Diagnostic emitted, no throw, no state change (D-GUARD) | [ ] |
| P2-T4 | Reconcile the existing hard-throws (`RequireGameSetupDone`, "Game already set" `:692`, snapshot null/dup player) — decide keep-as-throw (programmer contract) vs harmonize to ignore+emit | Decision D-RECONCILE recorded; code matches it | [ ] |
| P2-T5 | Fold the two existing guards into the lifecycle model conceptually (game-over-twice, post-game-over mutation) so the spec has one place | Spec + code cross-reference the same codes | [ ] |
| P2-T6 | Core unit tests: **every** illegal lifecycle edge + each existing guard = one test each | Full transition matrix covered, green | [ ] |

### Phase 3 — Portable state contract doc
| ID | Task | Artifact / acceptance | Status |
|----|------|-----------------------|--------|
| P3-T1 | Author `docs/contracts/runtime-state-contract.md`: state shape, **player transition table** (from/to/outcome/reason), **lifecycle state machine** + guards, envelope + message schema, diagnostic codes, versioning | Doc reviewed; matches core 1:1 | [ ] |
| P3-T2 | Tag the contract with protocol version; state the "stays 1" rationale (ADR 0008) and how a future v2 would add — not mutate — vectors | Versioning section complete | [ ] |
| P3-T3 | Reconcile with CONTEXT.md vocabulary; add terms (*Runtime State Kernel*, *Game Lifecycle*, *State View*) via the domain-model flow | CONTEXT.md updated, no term collisions | [ ] |

### Phase 4 — Conformance seams (thin now; corpus deferred)
| ID | Task | Artifact / acceptance | Status |
|----|------|-----------------------|--------|
| P4-T1 | Add a **vector-emit hook**: a core-driven harness that runs an ordered op list and dumps `{ops, expected: {outcomes, emittedMessages}}` as deterministic JSON | Runs headless; output stable across runs | [ ] |
| P4-T2 | Author **3–5 sample vectors** under `ContractFixtures/runtime-state/`: one accept, one no-op, one reject (transition); game-over-twice; post-game-over mutation; one illegal lifecycle edge | Files checked in; human-readable | [ ] |
| P4-T3 | Unity test runs the sample vectors against core and asserts equality (reusing `ContractFixtures` + `GCDevJsonContractFixtureTests` case-discovery plumbing) | Green; proves the format + reference impl agree | [ ] |
| P4-T4 | **Document the deferred work** in the contract doc: full versioned corpus + cross-language runner, with the seam described so a second engine is turnkey. Log explicitly that the corpus is *not* exhaustive yet (no silent "covered everything") | "Deferred / Conformance" section present | [ ] |

### Phase 5 — Docs & alignment
| ID | Task | Artifact / acceptance | Status |
|----|------|-----------------------|--------|
| P5-T1 | Finalize ADR 00XX (P0-T2) with the resolved decisions | ADR merged | [ ] |
| P5-T2 | Update README + VERSIONING_PLAN.md (core assembly, contract doc, protocol-vs-package version) | Docs consistent, no contradictions | [ ] |
| P5-T3 | Backlog alignment note; confirm no collision with `example-adapter-refactor-plan.md` (that plan's adapter seam is listener-side; this is package-internal) | Relationship recorded in both docs | [ ] |

---

## 6. Test strategy (cheapest-first)
1. **Behavior-preservation (Phase 1):** existing headless edit-mode tests pass unchanged after the move —
   the primary safety net for the refactor.
2. **Guardrail unit tests (Phase 2):** the full lifecycle transition matrix + each existing guard, in the
   core test assembly, no scene.
3. **Vector self-conformance (Phase 4):** the reference impl reproduces its own sample vectors exactly.
4. **Play-mode smoke (Phase 1/2):** one scripted run through `SetupDone → Play → transitions → GameOver`
   asserting identical observable output to pre-refactor, plus one deliberate illegal call asserting
   "ignored + diagnostic emitted."

---

## 7. Versioning & guardrail policy
- **Wire bytes unchanged → `gameProtocolVersion` stays 1** (ADR 0008). New diagnostic *codes* are additive
  and non-breaking; adding codes does not bump the protocol.
- **D-GUARD (ignore + emit)** applies to *runtime state-rule violations* (phase ordering, post-game-over
  mutation, double game-over). **Programmer-contract precondition failures** (double `SetupGame`, calling a
  game API with no game) are a separate class — P2-T4/D-RECONCILE decides whether they stay hard throws.
- Future protocol growth: a v2 **adds** vectors and message names; it never silently changes v1 semantics.

---

## 8. Risks & constraints
- **R1. Hidden Unity coupling in "pure" code.** The player-state view (P1-T2) is the riskiest seam; if any
  core path still needs a `GCPlayer` member not on the view, the boundary leaks. Mitigation: the view is the
  *only* player type core sees — compile enforces it.
- **R2. Assembly-boundary visibility churn.** Moving `internal` types across assemblies can force `public`
  or new `InternalsVisibleTo`. Mitigation: P1-T10 audits deliberately; keep new public surface minimal.
- **R3. Determinism regressions.** Removing `Time.time` from transitions changes *where* time enters. Golden
  vectors must feed fixed timestamps. Mitigation: P1-T4 threads time as a parameter everywhere.
- **R4. Unity version matrix (6000.0 → 6000.5)** and netcode-compiles-only-on-WebGL (project memory) still
  apply; the core is target-agnostic but the runtime wiring is not.
- **R5. Over-fitting deferred corpus.** Sample vectors risk encoding Unity quirks. Mitigation: keep them
  minimal and behavior-level; full corpus waits for a real second consumer (D-BUILD).
- **R6. Scope bleed into inbound/inputs.** Tempting but out of scope (N3). Hold the line.
- **R7. `GamingCouchRuntimeInfo.json` rebake** on any identity move (project memory) — P1-T12.

---

## 9. Open decisions for the grill
- **D7. `gameProtocolVersion`** — confirm stays 1 (expected; wire bytes unchanged). ★
- **D-RECONCILE.** Do the existing hard-throw precondition guards (double `SetupGame`, missing game,
  null/dup snapshot players) stay throws, or harmonize to ignore+emit under D-GUARD? *Recommend:* keep as
  throws — they signal programmer bugs, not runtime state-rule violations; document the distinction. ★
- **D-CORE-HOME.** Does `dsb.gamingcouch.core` live under `Runtime/Core/` in this package (one UPM package,
  new asmdef) or as a separately published package? *Recommend:* new asmdef in-package — no distribution
  change, matches the bare-package reality. ★
- **D-VIEW-SHAPE.** Is the player seam a C# `interface` (`IGCPlayerStateView`) or an immutable POCO struct
  the runtime fills each frame? *Recommend:* interface (zero-copy, `GCPlayer` implements directly); revisit
  if it forces awkward members.
- **D-VECTOR-FORMAT.** Confirm the vector JSON shape (`{ops, expected:{outcomes, emittedMessages}}`) and
  that it reuses the `ContractFixtures` corpus conventions rather than a new one.
- **D-LIFECYCLE-EDGES.** Confirm the exact legal edge set — notably whether `Restart` / dev re-entry needs
  a legal `GameOver → PendingSetup` (or `Playing → PendingSetup`) edge, given `Restart()` (`GamingCouch.cs
  :1268`) resets state.
- **D-DIAG-CODE.** Confirm the new code name `gc.state.invalid_lifecycle_transition` (vs per-edge codes).

---

## 10. Alignment: ADRs, backlog, docs
- **ADR 00XX (new)** — records the core seam, D-GUARD, protocol-stays-1, and the four §4 seams. References
  ADR 0008/0009; supersedes nothing.
- **CONTEXT.md** — add *Runtime State Kernel*, *Game Lifecycle*, *State View*; reuse *Runtime Messages*,
  *Diagnostics*, *Permanent/Revokable* states; the doctrine at `:138` is the north star.
- **`example-adapter-refactor-plan.md`** — sibling, non-colliding: that plan splits the *listener-side*
  example (adapter + swappable game); this plan is *package-internal* (the state kernel behind the
  runtime API). Neither changes the wire contract. Cross-link both.
- **Backlog** — relates to the broader portability/authoring theme; this plan is the state-kernel slice.
