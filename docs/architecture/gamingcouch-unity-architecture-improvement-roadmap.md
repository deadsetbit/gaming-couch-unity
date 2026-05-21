# Gaming Couch Unity Architecture Improvement Roadmap

Status: Active architecture roadmap
Last updated: 2026-05-14
Owner: Gaming Couch Unity package team

## Purpose

Sequence the next architecture improvements after the Local Play Contract, Local Play Session, and Contract Fixture Corpus slices.

This roadmap is about increasing **Depth**, **Leverage**, and **Locality** in the Unity package. It does not start Godot work directly. Portability means the **Local Play Contract** and **Contract Fixtures** stay stable enough that a future engine package can become another **Adapter** at the same contract **Seam**.

Terminology follows `CONTEXT.md`: **Start Screen** is the Unity editor UI/readiness surface, **Start Screen Readiness** is its shared facts and action model, **Active Scene Setup** is safe setup applied to the user's current scene, and **Example Assets** are generated editable assets in `Assets/GamingCouch/GCExample`.

## Source Context

- `<this-repo>/CONTEXT.md`
- `<this-repo>/docs/architecture/unity-dev-json-sync-implementation-tasks.md`
- `<this-repo>/docs/architecture/gamingcouch-start-screen-prd.md`
- `<this-repo>/docs/architecture/unity-dev-json-sync-prep-refactor-roadmap.md`

## Roadmap Principles

- Prefer behavior-neutral slices unless a task explicitly says otherwise.
- Deepen existing **Modules** before adding new **Seams**.
- Use the deletion test: a **Module** earns its place only when deleting it would spread complexity across callers.
- Keep public runtime payloads stable unless a separate product decision changes them.
- Treat one Unity **Adapter** as a hypothetical portability **Seam**; wait for a second engine **Adapter** before extracting shared cross-engine code.

## Current Baseline

Tasks 8 through 10 and Task 15 completed the local play architecture follow-up:

- **Local Play Contract Module**: root JSON stores, metadata store, file stamps, inspector draft, and executable **Contract Fixtures** are separated from inspector state and live in the Editor assembly.
- **Local Play Session Module**: active **Capture**, root Local Play Contract preflight, restart preflight/recapture, and issue logging coordination now sit behind one deeper Runtime **Seam** with neutral provider/result/issue types.
- **Editor Local Play Contract Adapter**: JSON-backed parsing, writing, validation, Newtonsoft usage, setup/play option construction, and **Seat** identity construction are registered from Editor at load time.
- **Contract Fixture Corpus**: package-root `ContractFixtures/LocalPlay` cases are portable data consumed by Unity editor tests.
- **Leverage gained**: tests can exercise real `gc.dev.json` and `gc.metadata.json` file behavior, active **Capture**, and portable **Contract Fixture** cases through smaller **Interfaces**.
- **Locality gained**: root file parsing/writing rules, local play session rules, and fixture expectations now live in their owning areas instead of being embedded in inspector state or broad test code.

Remaining friction observed in the current code shape:

- `Runtime/Dev/GCDevAppIntegration.cs` mixes connection lifecycle, runtime snapshots, project registration, and message construction.
- `Editor/GamingCouchStartScreenReadiness.cs` and `Editor/GamingCouchStartScreenWindow.cs` are both large and tightly paired.
- Active-scene setup now lives in `GamingCouchActiveSceneSetup.cs`; remaining setup work should keep Example Asset rules and active-scene wiring in focused tests.
- `Tests/Editor/GamingCouchStartScreenEditorSmokeTests.cs` keeps only cross-module smoke behavior, while setup asset coverage lives in `GamingCouchActiveSceneSetupAssetTests.cs`.

## Improvement Sequence

| Order | Module | Status | Problem | Slice | Done when |
| --- | --- | --- | --- | --- | --- |
| 1 | Local Play Contract Module | Done | Root JSON stores, draft state, file stamps, and tests were too close to inspector state. | Extract stores/draft/stamp and add executable **Contract Fixtures**. | Completed in Task 8 of the Dev JSON sync task plan. |
| 2 | Local Play Session Module | Done | **Capture**, preflight, restart, and Play Mode entry rules were split across runtime, editor capture, and inspector state. | Concentrate the editor play session rules behind one deeper **Module** while keeping current behavior. | Completed in Task 9 of the Dev JSON sync task plan. |
| 3 | Contract Fixture Corpus | Done | The new **Contract Fixtures** were executable but still lived as Unity test code. | Promote the fixture cases into a small contract corpus that Unity tests consume directly. | Completed in Task 10 of the Dev JSON sync task plan. |
| 4 | Runtime/Editor Local Play Contract Split | Done | Runtime still carried JSON implementation details and Newtonsoft references after the first local play slices. | Move JSON-backed contract behavior to Editor and keep Runtime as the neutral Local Play Session **Seam**. | Completed in Task 15 of the Dev JSON sync task plan. |
| 5 | Start Screen Readiness Module | Next | **Start Screen Readiness** rules and Start Screen rendering have high coupling, so adding a check requires understanding both. | Separate readiness facts/actions from the EditorWindow rendering **Implementation**. | Readiness checks are testable through one **Interface**, and the window mostly renders already-computed rows. |
| 6 | DevApp Runtime Adapter Module | Later | DevApp integration mixes transport, project identity, runtime snapshot, and message shape. | Split message construction from the WebSocket **Adapter** without changing DevApp behavior. | Runtime registration and snapshot messages are tested without a live transport. |
| 7 | Active Scene Setup Module | Later | Safe current-scene setup and generated **Example Assets** still sit in one broad setup module. | Concentrate setup rules behind a deeper editor setup **Module** while preserving Start Screen action behavior. | Generated assets, non-overwrite rules, and active-scene wiring have focused tests outside smoke coverage. |
| 8 | Editor Test Harness Module | Later | Smoke tests should stay small as new editor behavior grows. | Split tests by **Module** after the production **Modules** have deeper **Interfaces**. | New local play, readiness, setup, and DevApp rules have focused test homes. |
| 9 | Second Engine Adapter Readiness | Deferred | A Godot **Adapter** is still hypothetical, so extracting shared code now would create a speculative **Seam**. | Keep contract-level portability notes and wait for a real second **Adapter** need. | Godot work starts by implementing the **Local Play Contract** and running the shared **Contract Fixtures**, not by importing Unity internals. |

## Recommended Next Slice

Do the **Start Screen Readiness Module** next.

Why this is the best next step:

- It is the next highest-friction editor surface after the completed local play work.
- `GamingCouchStartScreenReadiness.cs` and `GamingCouchStartScreenWindow.cs` are both large and tightly paired, so adding a readiness rule currently requires understanding rule calculation, action availability, and rendering together.
- It improves **Locality** before touching broader Start Screen setup behavior.
- It creates a better test surface for Start Screen readiness rows and actions without changing setup behavior.

This slice should not rename public payloads, change setup behavior, or add another engine **Adapter**. It should make the existing Unity **Adapter** deeper.

## Execution Model

Use the same path as Task 8:

1. Add or update one tracked task in an existing architecture task file.
2. Implement that one slice.
3. Run two review-and-patch passes.
4. Record validation and skipped Unity test gaps.
5. Commit the slice independently.

Do not execute multiple roadmap rows in one implementation pass.
