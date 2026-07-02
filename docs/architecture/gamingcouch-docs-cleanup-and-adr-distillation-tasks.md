# Gaming Couch Unity Docs Cleanup and ADR Distillation Tasks

Status: Ready for implementation (all decisions made — see Decisions)
Last updated: 2026-07-02
Owner: Gaming Couch Unity package team
Execution model: coordinator + parallel subagents, independent verification per unit (see Agent Execution Model)

## Purpose

One-shot cleanup of the markdown doc estate on `feature/standalone-controller` before merge:
keep living docs, update stale ones, and delete completed planning logs **after** distilling
their durable decisions into `docs/adr/` (which exists but is empty). End state: every doc is
either a living reference, an active task tracker, or a one-paragraph ADR — nothing that
silently drifts from the implementation — and every produced or surviving doc has passed at
least one claims-vs-code verification.

## How To Use This File (for a fresh agent chat)

1. Work the phases top-down; **one phase = one commit** (finer is fine). Do not mix deletions
   with content updates in one commit.
2. **Distill first, delete second.** Phase 2 deletions must not start until the Phase 1 ADRs
   referencing those sources exist and are verified.
3. **No unit is done until its verification pass is green.** See Verification Protocol.
4. Unity `.meta` hygiene: the Unity Editor is normally open on this project and auto-generates
   `.meta` files for new files. Every deleted `.md` must take its `.meta` with it (`git rm` both).
   After creating new files, confirm the generated `.meta` and `git add` it too.
   `.scratch/` has no `.meta` files (dot-folders are ignored by Unity).
5. ADR format (from the `/grill-with-docs` → `/domain-modeling` skill convention):
   `docs/adr/NNNN-slug.md`, a short `# Title` plus 1–3 sentences (context, decision, why).
   Optional Status/Considered Options/Consequences sections only when they add real value.
   The ADR briefs below are ready to adapt — the writer verifies against the cited code before
   writing, and a separate verifier re-checks after.
6. This file only covers docs work plus one optional one-line code comment (unit U8).
   Do **not** make other code changes here; code fixes live in
   `docs/architecture/standalone-controller-review-remediation-tasks.md`.
7. Tick the checkboxes, update the Phase Tracker and the Verification Matrix as you go.

## Agent Execution Model

**Coordinator (main agent):**

- Owns sequencing, all git operations (subagents never commit), and all edits to
  **shared files** (see ownership rules below).
- Spawns writer subagents for independent units, then verifier subagents per the
  Verification Protocol. A unit's writer and verifier must be different agents.
- Commits a phase only when every unit in it is implemented **and** verified.

**Unit map and parallelism:**

| Phase | Units | Parallel? | Notes |
| --- | --- | --- | --- |
| 0 | hygiene | n/a | Coordinator does this directly; mechanical. |
| 1 | A01–A16 (one per ADR 0001–0016) | Yes — all 16 writers, then all 16 verifiers | Each writes exactly one new file in `docs/adr/`; zero overlap. |
| 2 | C1–C3 carry-forwards, then D1 deletions | C1/C2 sequential (same file), C3 parallel to them; D1 after all three | Deletions are mechanical — coordinator runs them. |
| 3 | U1–U8 | Yes, except U3 (edits backlog — serialize behind C1/C2) | Each unit owns disjoint files (see below). |
| 4 | doc map + reference sweep + estate sweep | Sweep verifiers fan out in parallel | Coordinator finishes. |

**Shared-file ownership (conflict prevention):**

- `docs/architecture/gamingcouch-unity-backlog.md` is edited by C1, C2, and U3. These are
  **coordinator-owned and applied sequentially** (or delegated to one dedicated agent).
  No other agent may touch the backlog.
- `docs/architecture/gamingcouch-web-export-settings-prd.md` is edited only by C3.
- Every other unit maps 1:1 to files no other unit touches. Keep it that way if re-slicing.

**Sizing:** each unit is deliberately sized for one subagent context: the brief in this file,
the source doc(s), and the cited code anchors. Writers should not need repo-wide exploration.

**Single-session execution (this plan is designed to complete in ONE coordinator session):**

- Subagent returns must be terse and structured: writers return "done + files touched + open
  questions" (≤ ~100 words); verifiers return only the claim/verdict table. Never let a
  subagent dump file contents back to the coordinator.
- The coordinator updates this file's checkboxes and Verification Matrix **immediately after
  each unit resolves**, not batched at phase end. This file is the externalized state: after an
  interruption or context compaction, a (re)started coordinator resumes from the first
  unchecked item and must never redo checked ones.
- Per-phase commits are the checkpoints; a resumed session can trust anything committed.
- If the harness limits concurrent subagents, batch the fan-outs (e.g. 8 at a time) — order
  within a fan-out never matters, only the barriers between writer→verifier→commit.
- Rough scale for planning: ~80 subagent runs total across all phases. If the coordinator's
  context grows tight, prefer finishing the current phase + commit before continuing.

## Verification Protocol

The rule the whole plan answers to: **every task and every remaining document gets at least one
independent pass that checks its claims against the code.**

1. **Writer ≠ verifier.** After a unit is implemented, the coordinator spawns a fresh subagent
   that receives only: the produced/updated file, this file's brief for that unit, and repo
   access. It must not see the writer's reasoning.
2. **Claim-level checking.** The verifier extracts every factual claim in the artifact (API and
   class names, file paths, line-anchored behavior, schema fields, term definitions) and marks
   each **CONFIRMED** (with `file:line` evidence) or **CONTRADICTED** (with the conflicting
   evidence). ADRs additionally get the question: "does the code still actually do this, and is
   the decision framing accurate?" Unverifiable claims (other-repo behavior, e.g. hosted SDK or
   DevApp) are marked **OUT-OF-REPO** and must be phrased in the artifact as cross-repo contract
   statements, not local facts.
3. **Fail handling.** Any CONTRADICTED claim goes back to the writer (or coordinator) for a fix;
   re-verify only the failed claims. A unit is green when zero CONTRADICTED remain.
4. **Deletion safety (Phase 2/3 deletions).** Before the delete commit: verifier confirms all
   carried-forward content (C1–C3) actually landed at its destination. After: run the reference
   sweep — `grep -rn` each deleted filename across remaining `*.md`; zero dangling references.
5. **Glossary check (U1).** Every CONTEXT.md term must anchor to a real code identifier or
   concept (verifier names it). Superseded terms (`Active Player Index`, `activePlayers`,
   "Session Seam", "Contract Adapter") must have zero occurrences in surviving docs except this
   file and the remediation tracker.
6. **Final estate sweep (Phase 4).** After all phases: one verifier per **remaining** markdown
   doc (including untouched keepers like README.md and AGENTS.md) does a claims-vs-code pass as
   in rule 2. This guarantees the "at least one pass per document" invariant even for docs this
   plan never edited. Record results in the Verification Matrix.
7. **Record everything.** The Verification Matrix at the bottom of this file is the audit trail:
   unit, writer done, verifier verdict, date.

## Decisions (all decided 2026-07-02, owner)

- **Decision A — hard delete.** Completed planning docs are deleted, not archived; git history
  is the archive. No `docs/archive/` folder.
- **Decision B — one glossary.** Merge `docs/architecture/unity-runtime-contract/08-domain-glossary.md`
  into root `CONTEXT.md` as the single glossary, adopting code-true terms (**Player Index**, not
  "Active Player Index"); then delete 08.
- **Decision C — WebGL consolidation.** `gamingcouch-web-export-settings-prd.md` absorbs the
  durable rules from the two completed WebGL task logs; both logs are then deleted.
- **Decision D — fine-grained ADRs.** Write all 16 ADRs below (one decision per file).

## Phase Tracker

| Phase | Scope | Implemented | Verified |
| --- | --- | --- | --- |
| 0 | Repo hygiene (gitignore, commit trackers) | ☑ | n/a (mechanical) |
| 1 | ADRs 0001–0016 in `docs/adr/` | ☐ | ☐ |
| 2 | Carry-forwards C1–C3 + deletions | ☐ | ☐ |
| 3 | Survivor updates U1–U8 | ☐ | ☐ |
| 4 | Doc map + reference sweep + estate sweep | ☐ | ☐ |

## Audit Basis (2026-07-02)

Full audit of all 43 markdown files on this branch, three parallel review passes, claims
spot-checked against `Runtime/`, `Editor/`, `Tests/Editor/`, and git history. Facts a fresh
agent should trust (re-verify only if something looks off):

- Every plan/PRD/task doc except `standalone-controller-review-remediation-tasks.md` describes
  **completed, code-verified** work.
- `.scratch/unity-runtime-contract/` is a strictly superseded early draft of
  `docs/architecture/unity-runtime-contract/` (all tasks "Not started" there vs "Completed" in
  canonical; uses the deliberately removed `activePlayers` terminology). Zero unique content.
- `CONTEXT.md` terminology drifted: **Active Player / Active Player Index have zero code
  occurrences**; code and 08-domain-glossary use **Player Index** (`GCPlayer.Index`,
  `playerIndex`). Also "Seam" → code says Session (`GCLocalPlaySession`), "Adapter" → code says
  Provider (`GCDevJsonLocalPlaySessionProvider : IGCLocalPlaySessionProvider`).
- Docs referencing `Runtime/Dev/GCEditorPlayCapture.cs` are stale — that file was folded into
  `GCLocalPlaySession`.
- README / `Documentation~/README.md` / `VERSIONING_PLAN.md` triplicate the web-export and
  runtime-identity prose (~80% overlap in places).
- `docs/adr/` + `docs/adr.meta` exist; the directory is empty.

---

## Phase 0 — Repo hygiene (coordinator, mechanical)

- [x] Add Python ignore rules to `.gitignore`: `**/__pycache__/`, `*.pyc`, `**/__pycache__.meta`.
- [x] Delete `Tools/__pycache__/` and stray `Tools/__pycache__.meta` from disk.
- [x] `git add` + commit the two tracker docs if still uncommitted:
      `docs/architecture/gamingcouch-unity-backlog.md` (+ `.meta`, already staged),
      `docs/architecture/standalone-controller-review-remediation-tasks.md` (+ `.meta`),
      and this file (+ `.meta`).

## Phase 1 — Distill ADRs into docs/adr/ (units A01–A16, fully parallel)

Write each as `docs/adr/NNNN-slug.md`. Briefs give the decision essence and the code/doc anchors.
Source docs still exist during this phase — writers consult them for nuance.

- [ ] **A01 / 0001-strict-runtime-identity-boundary** — Games address participants only by dense,
      zero-based `playerIndex` (`GCPlayer.Index`); platform player IDs and player names never
      cross into the game-facing Runtime API. Legacy `playerId`/`players[]` payloads are
      hard-rejected with no fallback, and removed APIs are `[Obsolete(..., true)]` compile
      errors. One-way privacy/fairness boundary; adapters upstream translate instead.
      *Verify:* `Runtime/GCPlayOptions.cs` strict-current rejection; `Runtime/GCPlayer.cs`
      obsolete markers. *Sources:* runtime-contract PRD/01/02, `.scratch/unity-package-legacy-cleanup`.
- [ ] **A02 / 0002-deterministic-player-index-shuffle** — Player indices assigned by FNV-1a
      32-bit hash-sort (unsigned order, captured-order tie-break, per-run seed) so C# and TS
      produce byte-identical assignments without shared code. Must stay bit-exact
      cross-language; change only with cross-language fixtures. *Verify:*
      `Runtime/GCPlayerIndexMapping.cs:67-91`. *Source:* runtime-contract 01.
- [ ] **A03 / 0003-permanent-revokable-player-state** — Elimination/finish modeled as
      permanent/revokable states (replacing reversible booleans); dual timebase: scaled
      `GameTime` for game logic, unscaled `runtimeTimeMs` for ordering. *Verify:*
      `Runtime/GCPlayer.cs` enums + `GameTime` timestamps. *Source:* runtime-contract 03.
      Consequences note: `SetMeter` payload and elimination `reason` remain deferred follow-ups
      (carried to backlog by C1).
- [ ] **A04 / 0004-two-path-runtime-output** — `runtime_messages` + `screen_space` are the only
      physical output paths; HUD is demoted from data owner to consumer. *Verify:*
      `Runtime/GCRuntimeMessages.cs`, `Runtime/Hud/GCHud.cs`. *Source:* runtime-contract 04.
- [ ] **A05 / 0005-object-wrapped-game-over** — Game-over result is `{ playerIndicesByPlacement }`;
      the object wrapper disambiguates from the legacy bare array (which could not distinguish
      `playerIndex: 0`), and the first accepted result wins and freezes platform state.
      *Verify:* `Runtime/GCRuntimeMessages.cs` + `Tests/Editor/GCRuntimeOutputContractTests.cs`.
      *Sources:* runtime-contract 04/07.
- [ ] **A06 / 0006-diagnostics-spine** — Structured `gc.*` diagnostic codes emitted as
      first-class runtime messages; stable code taxonomy is contract; bypasses Unity log level;
      carries `playerIndex` only; separate from host console mirroring. *Verify:*
      `Runtime/RuntimeMessages/GCDiagnostics.cs:17-97`. *Source:* runtime-contract 05.
- [ ] **A07 / 0007-readonly-platform-metadata-notdefined** — Unity reads `gc.platform.json` but
      never writes or repairs it; missing/invalid metadata projects as explicit `notdefined`
      (not null/undefined) with persistent warnings. *Verify:* `GCPlatformRuntimeView` usage in
      `Runtime/GCActiveRunProjection.cs`. *Source:* runtime-contract 06 (reserved "Future
      Metadata" fields carried to backlog by C2).
- [ ] **A08 / 0008-game-protocol-version-stays-1** — `gameProtocolVersion` stays 1:
      adapter-translated changes (incl. sidecar/upload validation) do not bump it; a bump is
      reserved for changes games/SDK cannot adapter-translate. *Verify:*
      `Runtime/Resources/GamingCouchRuntimeInfo.json`. *Sources:* VERSIONING_PLAN, sidecar
      tasks, CHANGELOG release-notes block.
- [ ] **A09 / 0009-two-sidecar-identity-model** — `gc.runtime-info.json` is the identity gate
      (hosted SDK hard-rejects startup on identity drift vs the runtime callback; required at
      upload; deliberately not cryptographic proof) while `gc.unity-build-info.json` is
      non-gating, path-redacted build diagnostics; `package.json` is the sole source of package
      name/version — no literals in runtime or template. *Verify:* `Runtime/GCRuntimeInfo.cs`,
      `Editor/GamingCouchWebGLRuntimeInfoSidecar.cs`, `Editor/GCUnityBuildInfoSidecar.cs`.
      *Sources:* webgl-runtime-info-sidecar tasks, VERSIONING_PLAN, `.scratch/unity-build-info-sidecar`.
- [ ] **A10 / 0010-gc-dev-json-canonical** — Root `gc.dev.json` is the only editor local-play
      settings source (`devVersion: 2`, exactly eight seats, fixed seat→color map); old
      serialized inspector fields are hidden and ignored with no migration or fallback; Unity
      never creates or repairs `gc.dev.json`/`gc.platform.json`. *Verify:* `Editor/GCDevJson*.cs`.
      *Source:* unity-dev-json-sync-implementation-tasks Product Rules.
- [ ] **A11 / 0011-runtime-is-json-free** — All Local Play Contract JSON parsing (Newtonsoft)
      lives Editor-only behind `IGCLocalPlaySessionProvider`; Runtime consumes neutral capture
      results, so player/WebGL builds carry no JSON dependency. *Verify:* zero Newtonsoft usage
      in `Runtime/`; `Runtime/Dev/GCLocalPlaySession.cs`. *Sources:* then-now doc, dev-json-sync tasks.
- [ ] **A12 / 0012-defer-cross-engine-extraction** — No shared cross-engine code until a second
      engine adapter (e.g. Godot) exists; portability is guaranteed by the Local Play Contract +
      Contract Fixtures, not shared implementation. *Source:* improvement roadmap.
- [ ] **A13 / 0013-webgl-compression-disabled** — Build profiles force
      `WebGLCompressionFormat.Disabled` for Dev and Release by design (decided 2026-07-02);
      serving-layer compression is the hosting platform's concern. Do not "fix" to Brotli/Gzip;
      tests pin `Disabled`. *Verify:* `Editor/GamingCouchWebGLBuildSettingsProfiles.cs:292`,
      `Tests/Editor/GamingCouchActiveSceneSetupAssetTests.cs:666-733`. *Source:* remediation
      tasks Decision D1.
- [ ] **A14 / 0014-minimal-shell-web-template** — The package web export template is a minimal
      production shell: no playtest harness, no JS shims, no PWA/service worker; output is not
      playable outside the hosting platform by design. *Verify:* template under
      `Editor/WebGLTemplates`. *Source:* web-export-settings PRD.
- [ ] **A15 / 0015-ordered-transitions-never-coalesced** — Semantic player transitions are
      ordered facts emitted in occurrence order and never coalesced; only latest-state
      projections (snapshots/HUD) may coalesce at frame boundaries. *Verify:*
      `Runtime/GCPlayerTransitions.cs`. *Source:* `.scratch/deepen-player-state-transitions/PRD.md`.
- [ ] **A16 / 0016-no-generated-example-scene** — There is deliberately no generated example
      scene route; Active Scene Setup applies safe setup to the user's currently active scene,
      and Example Assets are generated editable project files. *Verify:*
      `Editor/GamingCouchActiveSceneSetup.cs`. *Source:* start-screen PRD Task 6.

Verification: one verifier per ADR (16 parallel), per Verification Protocol rules 1–3.
Commit when all 16 are green.

## Phase 2 — Carry-forwards, then delete completed planning docs

Carry-forwards (backlog edits are coordinator-owned; C1 → C2 sequential, C3 independent):

- [ ] **C1** From `unity-runtime-contract/03-player-state-model.md`: copy the "Deferred Contract
      Follow-Ups" (`SetMeter`, elimination `reason`) into `gamingcouch-unity-backlog.md` as new
      backlog rows.
- [ ] **C2** From `unity-runtime-contract/06-platform-metadata-runtime-view.md`: copy the
      reserved "Future Metadata" fields list into the backlog likewise.
- [ ] **C3** Per Decision C, move into `gamingcouch-web-export-settings-prd.md`: the rule "the
      preview window's spec-driven setting list is the single authoritative list — docs must not
      maintain a second manual copy" (from webgl-build-settings-preview) and a short compression
      note pointing at ADR 0013.

Pre-delete verification: verifier confirms C1–C3 content landed at destinations (Protocol rule 4).

Then **D1 deletions** — coordinator runs `git rm` (each doc together with its `.meta`):

- [ ] `docs/architecture/gamingcouch-unity-architecture-improvement-roadmap.md`
- [ ] `docs/architecture/gamingcouch-unity-package-architecture-then-now.md`
- [ ] `docs/architecture/unity-dev-json-sync-implementation-tasks.md`
- [ ] `docs/architecture/unity-dev-json-sync-prep-execution-tasks.md`
- [ ] `docs/architecture/unity-dev-json-sync-prep-refactor-roadmap.md`
- [ ] `docs/architecture/webgl-build-settings-preview-implementation-tasks.md`
- [ ] `docs/architecture/webgl-runtime-info-sidecar-implementation-tasks.md`
- [ ] `docs/architecture/unity-runtime-contract/PRD.md`
- [ ] `docs/architecture/unity-runtime-contract/01-player-index-mapping.md`
- [ ] `docs/architecture/unity-runtime-contract/02-unity-api-migration.md`
- [ ] `docs/architecture/unity-runtime-contract/03-player-state-model.md`
- [ ] `docs/architecture/unity-runtime-contract/04-runtime-output-contract.md`
- [ ] `docs/architecture/unity-runtime-contract/05-diagnostics-spine.md`
- [ ] `docs/architecture/unity-runtime-contract/06-platform-metadata-runtime-view.md`
- [ ] `docs/architecture/unity-runtime-contract/manual-test-notes.md`
- [ ] `git rm -r .scratch/` (all 12 files; no `.meta` files there)

Keep in this phase: `07-cross-repo-rollout.md` (live rollout checkpoints) and
`08-domain-glossary.md` (deleted in Phase 3 after the CONTEXT.md merge).

Post-delete verification: reference sweep per Protocol rule 4.

## Phase 3 — Update surviving docs (units U1–U8; parallel except U3)

- [ ] **U1 — CONTEXT.md** (Decision B): merge in `08-domain-glossary.md`; rename
      **Active Player** → **Player**, **Active Player Index** → **Player Index**;
      "Local Play Session Seam" → "Local Play Session" (code: `GCLocalPlaySession`);
      "Editor Local Play Contract Adapter" → "Editor Local Play Contract Provider"
      (code: `GCDevJsonLocalPlaySessionProvider`); keep it a pure glossary (no implementation
      detail). Then `git rm` `08-domain-glossary.md` (+ `.meta`). *Verify:* Protocol rule 5.
- [ ] **U2 — gamingcouch-start-screen-prd.md**: flip header status "In progress" → Implemented
      (all 6 tasks are Completed). Keep as the feature spec.
- [ ] **U3 — gamingcouch-cleanup-triage-tasks.md** (serialize behind C1/C2 — edits backlog):
      reconcile Tasks 2/3/5 against later commits (parts of Task 3's deprecation list already
      landed via "Remove legacy HUD update API", "Hide legacy multiplayer toggle",
      "runtime: remove legacy player id routing"); move the still-open remainder into
      `gamingcouch-unity-backlog.md`; then `git rm` the file (+ `.meta`) and prune the
      now-duplicate backlog row B007 if it merges.
- [ ] **U4 — unity-runtime-contract/07-cross-repo-rollout.md**: mark the completed
      branch/release steps done; keep the open legacy-bridge removal checkpoint, Post-Legacy
      Cleanup Ledger, and JS runtime follow-up.
- [ ] **U5 — Documentation~/README.md**: shrink to a docs-site landing page that links to
      README/API instead of restating the web-export/local-play/runtime-contract prose
      (~80% duplicate today).
- [ ] **U6 — VERSIONING_PLAN.md**: replace the runtime-identity/sidecar/protocol prose with
      links to ADRs 0008/0009; resolve the stale "Open questions" (CI workflows and
      `.releaserc.json` already exist — semantic-release is in place); keep a lean versioning plan.
- [ ] **U7 — CHANGELOG.md**: add missing unreleased entries — web-export settings rename
      (`b55004f`) and controller input removals (`106b34c`, `93732d4`).
- [ ] **U8 — README.md**: clarify that `activeSeats` (line ~85) is a DevApp-repo concept, not a
      Unity package field. *(Optional, from remediation D1)*: add a one-line comment at
      `Editor/GamingCouchWebGLBuildSettingsProfiles.cs:292` — "Disabled by design, see
      docs/adr/0013-webgl-compression-disabled.md" — and tick D1's optional doc-note box in
      `standalone-controller-review-remediation-tasks.md`.

Verification: one verifier per unit U1–U8 (parallel), per Protocol rules 1–3 (+5 for U1).
Commit when all green.

## Phase 4 — Documentation map + sweeps (coordinator + parallel sweep verifiers)

- [ ] Add a short "Documentation map" section to README.md: living references (README,
      Documentation~, CONTEXT.md, web-export PRD, start-screen PRD), decisions (`docs/adr/`),
      active trackers (remediation tasks, backlog, 07-cross-repo-rollout, this file).
- [ ] Reference sweep: `grep -rn` every deleted filename across all remaining `*.md` (and
      `AGENTS.md`) and fix or drop dangling links — known: backlog "Related Planning Files"
      lists the improvement roadmap and cleanup-triage; backlog row B004 references
      "runtime-contract Task 11".
- [ ] **Final estate sweep** (Protocol rule 6): one verifier per remaining markdown doc —
      including untouched keepers (README.md, AGENTS.md, AGENTS.local.example.md, CHANGELOG.md,
      web-export PRD, start-screen PRD, 07-cross-repo-rollout, backlog, remediation tracker,
      all 16 ADRs if not already re-verified post-edit) — claims-vs-code pass; record verdicts
      in the Verification Matrix.
- [ ] Final check: `git status` clean of stray `.meta` (no orphan `.meta` without its file, no
      file without `.meta` under `docs/`); Phase Tracker and Verification Matrix updated.
      Then either delete this file (it becomes a completed planning log) or keep it until the
      branch merges — owner's call at that point.

## Verification Matrix (audit trail — fill during execution)

| Unit | Artifact | Implemented | Verifier verdict | Notes |
| --- | --- | --- | --- | --- |
| A01–A16 | `docs/adr/0001`–`0016` | ☐ | — | one row per ADR when executing |
| C1–C3 | backlog, web-export PRD | ☐ | — | pre-delete destination check |
| D1 | 16 deletions + `.scratch/` | ☐ | — | post-delete reference sweep |
| U1–U8 | survivor updates | ☐ | — | one row per unit when executing |
| Sweep | every remaining `*.md` | ☐ | — | one row per doc when executing |

## Related Planning Files

- `docs/architecture/standalone-controller-review-remediation-tasks.md` (code-side branch completion)
- `docs/architecture/gamingcouch-unity-backlog.md` (speculative ideas)
