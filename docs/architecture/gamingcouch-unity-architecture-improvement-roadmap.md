# Gaming Couch Unity Architecture Improvement Roadmap

Status: Active architecture roadmap
Last updated: 2026-05-14
Owner: Gaming Couch Unity package team

## Purpose

Sequence the next architecture improvements after the Local Play Contract hardening slice.

This roadmap is about increasing **Depth**, **Leverage**, and **Locality** in the Unity package. It does not start Godot work directly. Portability means the **Local Play Contract** and **Contract Fixtures** stay stable enough that a future engine package can become another **Adapter** at the same contract **Seam**.

## Source Context

- `/Users/anttil/dev/dsb/gaming-couch-unity/CONTEXT.md`
- `/Users/anttil/dev/dsb/gaming-couch-unity/docs/architecture/unity-dev-json-sync-implementation-tasks.md`
- `/Users/anttil/dev/dsb/gaming-couch-unity/docs/architecture/gamingcouch-quick-start-start-screen-prd.md`
- `/Users/anttil/dev/dsb/gaming-couch-unity/docs/architecture/unity-dev-json-sync-prep-refactor-roadmap.md`

## Roadmap Principles

- Prefer behavior-neutral slices unless a task explicitly says otherwise.
- Deepen existing **Modules** before adding new **Seams**.
- Use the deletion test: a **Module** earns its place only when deleting it would spread complexity across callers.
- Keep public runtime payloads stable unless a separate product decision changes them.
- Treat one Unity **Adapter** as a hypothetical portability **Seam**; wait for a second engine **Adapter** before extracting shared cross-engine code.

## Current Baseline

Task 8 completed the first architecture suggestion:

- **Local Play Contract Module**: root JSON stores, metadata store, file stamps, inspector draft, and executable **Contract Fixtures** are separated from inspector state.
- **Leverage gained**: tests can exercise real `gc.dev.json` and `gc.metadata.json` file behavior through a smaller **Interface**.
- **Locality gained**: root file parsing/writing rules now live in the Local Play Contract area instead of being embedded in inspector state.

Remaining friction observed in the current code shape:

- `Runtime/GamingCouch.cs` still coordinates play capture, restart, setup/play callbacks, and runtime state.
- `Runtime/Dev/GCDevAppIntegration.cs` mixes connection lifecycle, runtime snapshots, project registration, and message construction.
- `Editor/GamingCouchStartScreenReadiness.cs` and `Editor/GamingCouchStartScreenWindow.cs` are both large and tightly paired.
- `Tests/Editor/GamingCouchQuickStartEditorTests.cs` is a broad test Module that makes new editor rules harder to place.

## Improvement Sequence

| Order | Module | Status | Problem | Slice | Done when |
| --- | --- | --- | --- | --- | --- |
| 1 | Local Play Contract Module | Done | Root JSON stores, draft state, file stamps, and tests were too close to inspector state. | Extract stores/draft/stamp and add executable **Contract Fixtures**. | Completed in Task 8 of the Dev JSON sync task plan. |
| 2 | Local Play Session Module | Next | **Capture**, preflight, restart, and Play Mode entry rules are split across runtime, editor capture, and inspector state. | Concentrate the editor play session rules behind one deeper **Module** while keeping current behavior. | `GamingCouch` asks one Module for editor play readiness and captured options; restart and Play Mode entry use the same path. |
| 3 | Contract Fixture Corpus | Next | The new **Contract Fixtures** are executable but still live as Unity test code. | Promote the fixture cases into a small contract corpus that Unity tests consume directly. | A future engine **Adapter** can reuse the same JSON cases and expected outcomes without reading Unity test code. |
| 4 | Start Screen Readiness Module | Later | Readiness rules and Start Screen rendering have high coupling, so adding a check requires understanding both. | Separate readiness facts/actions from the EditorWindow rendering **Implementation**. | Readiness checks are testable through one **Interface**, and the window mostly renders already-computed rows. |
| 5 | DevApp Runtime Adapter Module | Later | DevApp integration mixes transport, project identity, runtime snapshot, and message shape. | Split message construction from the WebSocket **Adapter** without changing DevApp behavior. | Runtime registration and snapshot messages are tested without a live transport. |
| 6 | Quick Start Setup Module | Later | Safe scene/object/asset setup rules are spread across setup, scene wiring, and broad tests. | Concentrate the setup rules behind a deeper editor setup **Module**. | Generated assets, non-overwrite rules, and scene wiring have focused tests outside the mega test file. |
| 7 | Editor Test Harness Module | Later | The broad Quick Start test file reduces **Locality** for new editor behavior. | Split tests by **Module** after the production **Modules** have deeper **Interfaces**. | New local play, readiness, setup, and DevApp rules have focused test homes. |
| 8 | Second Engine Adapter Readiness | Deferred | A Godot **Adapter** is still hypothetical, so extracting shared code now would create a speculative **Seam**. | Keep contract-level portability notes and wait for a real second **Adapter** need. | Godot work starts by implementing the **Local Play Contract** and running the shared **Contract Fixtures**, not by importing Unity internals. |

## Recommended Next Slice

Do the **Local Play Session Module** next.

Why this is the best next step:

- It is adjacent to the completed Local Play Contract work.
- It reduces the largest remaining local play **Seam** confusion: preflight, auto-apply, capture, restart, and active-play stability.
- It improves **Locality** before touching broad Start Screen or DevApp integration code.
- It creates a better test surface for active **Capture** behavior without changing public payloads.

This slice should not rename public payloads, change Play Mode behavior, or add another engine **Adapter**. It should make the existing Unity **Adapter** deeper.

## Execution Model

Use the same path as Task 8:

1. Add or update one tracked task in an existing architecture task file.
2. Implement that one slice.
3. Run two review-and-patch passes.
4. Record validation and skipped Unity test gaps.
5. Commit the slice independently.

Do not execute multiple roadmap rows in one implementation pass.
