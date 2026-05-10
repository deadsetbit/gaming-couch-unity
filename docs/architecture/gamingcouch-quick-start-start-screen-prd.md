# GamingCouch Quick Start Start Screen PRD

Status: Ready for implementation
Last updated: 2026-05-10
Owner: Gaming Couch Unity package team

This document is the local Markdown source of truth for the feature. Do not publish these tasks to GitHub issues, Linear, or any external backlog unless a later instruction explicitly changes that.

## Task Status Legend

- `TODO`: not started
- `IN PROGRESS`: actively being worked
- `DONE`: completed and verified
- `BLOCKED`: waiting on a decision or dependency

To mark a task complete, change `Status` to `DONE` and change the checkbox to `[x]`.

## Problem Statement

New GamingCouch Unity package users currently need to manually discover and wire several required pieces before they can run a minimal local play loop: a `GamingCouch` object, a listener game object, a `GCPlayer`-based prefab, setup/play hook methods, and valid local play configuration. The existing package menu can create only a bare `GamingCouch` object, so the first run experience still leaves users guessing what is missing and how the pieces connect.

The result is that a user can install the package, open Unity, and still not have an obvious path to a scene where pressing Play exercises the GamingCouch setup, play, scoring, and game over lifecycle.

## Solution

Add a Unity Editor start screen for GamingCouch that detects whether the active scene is wired up, explains missing setup, and can generate a minimal editable quick-start scene, game script, player script, and player prefab. The primary quick-start path should let a user create or wire all missing scene pieces with one action, then press Play and observe a short round that assigns random scores and finishes with `GameOver()`.

The start screen should appear automatically when Unity starts into an active scene that is not GamingCouch-ready, while still allowing users to suppress automatic opening for the project. The same start screen should also be reachable from the GamingCouch menu and from the `GamingCouch` inspector.

## User Stories

1. As a new Unity package user, I want GamingCouch onboarding to appear when I open an incomplete scene, so that I immediately know how to get started.
2. As a new Unity package user, I want the start screen to show whether my active scene has a `GamingCouch` object, so that I can understand the current setup state.
3. As a new Unity package user, I want the start screen to detect whether the `GamingCouch` object has a listener assigned, so that I can fix the message receiver before entering Play Mode.
4. As a new Unity package user, I want the start screen to detect whether the `GamingCouch` object has a player prefab assigned, so that player spawning can work in the generated loop.
5. As a new Unity package user, I want a primary action that wires all missing quick-start pieces, so that I do not need to know the exact setup order.
6. As a cautious Unity user, I want individual setup actions to remain available, so that I can choose only the pieces I want to create.
7. As a Unity user with an existing scene, I want setup actions to avoid overwriting non-null references, so that my existing custom wiring is preserved.
8. As a Unity user with generated quick-start files, I want rerunning setup to reuse existing generated assets without overwriting edits, so that experimentation is safe.
9. As a new Unity package user, I want an option to create a new quick-start scene, so that I can test GamingCouch without modifying my own scene.
10. As a new Unity package user, I want the generated quick-start scene to open automatically, so that I can press Play without hunting for the scene asset.
11. As a Unity user preparing a build, I want the generated quick-start scene added to Build Settings, so that the scene is available for WebGL builds.
12. As a Unity package user, I want the generated player prefab to be based on `GCPlayer`, so that it works with `SetupPlayers<T>()`.
13. As a Unity package user, I want the generated player prefab to have a visible placeholder, so that spawned players are easy to identify in the editor.
14. As a Unity package user, I want generated player visuals to use GamingCouch player colors, so that the placeholder demonstrates platform player data.
15. As a Unity package user, I want the generated game script to include `GamingCouchSetup` and `GamingCouchPlay`, so that I can see the required hook methods.
16. As a Unity package user, I want the generated game script to call `SetupGameVersus`, `SetupDone`, `SetupPlayers`, and `GameOver`, so that the full lifecycle is demonstrated.
17. As a Unity package user, I want the generated round to last a short editable duration, so that I can observe the loop before it finishes.
18. As a Unity package user, I want randomized final scores in the quick-start loop, so that placement and scoring behavior are demonstrated with minimal game logic.
19. As a Unity package user, I want clear messaging when `gc.dev.json` is missing or invalid, so that I understand why local Play Mode is blocked.
20. As a GamingCouch developer, I want the Unity package to avoid creating or repairing `gc.dev.json`, so that DevApp remains the source of truth for local project configuration.
21. As a Unity user working in non-game scenes, I want to suppress automatic opening for this project, so that onboarding does not interrupt intentional workflows.
22. As a Unity user who suppressed automatic opening, I want the start screen to remain available from the menu, so that I can return to onboarding later.
23. As a Unity user inspecting a `GamingCouch` component, I want an inspector button for the start screen, so that setup help is available where missing references are visible.
24. As a GamingCouch developer, I want multiple `GamingCouch` objects to be reported as an error state instead of auto-fixed, so that destructive cleanup is not hidden behind onboarding.
25. As a package maintainer, I want quick-start setup split into testable editor services, so that readiness detection and generation behavior can be verified without relying only on manual Unity testing.

## Implementation Decisions

- Build an editor-only GamingCouch start screen using the package's existing IMGUI editor style.
- Treat an active scene as GamingCouch-ready only when it has exactly one `GamingCouch` component and that component has both listener and player prefab references assigned.
- Auto-open the start screen once on Unity editor startup when the active scene is incomplete.
- Do not auto-open on later scene changes, every domain reload, or while entering Play Mode.
- Add a persistent per-project setting to suppress automatic opening.
- Keep manual entry points through the GamingCouch top menu and the `GamingCouch` inspector.
- Keep the existing create-GamingCouch menu action, but route object creation through a shared helper with Undo support.
- Generate editable quick-start assets under a project-owned `Assets/GamingCouch/QuickStart` folder.
- Generate collision-resistant starter types named `GCQuickStartGame` and `GCQuickStartPlayer`.
- Use a staged setup flow because Unity must compile generated scripts before components can be added.
- Reuse generated assets on rerun and never overwrite existing generated scripts, prefabs, scenes, or non-null scene references.
- Create a generated `GCQuickStartPlayer` prefab based on `GCPlayer` with a simple 3D primitive visual that applies `ColorBase`.
- Create a generated `GCQuickStartGame` listener that configures a versus game, sets up a points HUD, spawns players, runs a default 10-second timer, assigns random final scores, and calls `GameOver()`.
- Create a new quick-start scene that includes the wired `GamingCouch` object, generated listener, generated player prefab, camera, and light.
- Open the generated quick-start scene after creation and add it to Build Settings if it is not already present.
- Detect missing or invalid local play JSON and report it as a blocker, but do not create, bootstrap, or repair local project JSON.
- Report multiple active-scene `GamingCouch` instances as a manual cleanup error.

## Testing Decisions

- Prefer automated editor tests for readiness detection, rerun safety, generated asset reuse, scene wiring, and build settings insertion.
- Test behavior at the editor service boundary rather than testing private IMGUI layout details.
- Include tests for incomplete scenes, fully wired scenes, missing listener, missing player prefab, multiple `GamingCouch` objects, and suppressed auto-open settings.
- Include tests that prove setup does not overwrite existing generated files or non-null serialized references.
- Include manual Unity validation for the staged compile flow, because generated scripts must compile before components can be attached.
- Include manual Play Mode validation with a valid local play JSON file: setup runs, players spawn, a 10-second round assigns scores, and `GameOver()` is called.
- Include manual validation that missing or invalid local play JSON is reported clearly and is not repaired by Unity.

## Out of Scope

- Creating, bootstrapping, repairing, or migrating `gc.dev.json` or `gc.metadata.json`.
- Adding a full sample game or controller-input mini-game.
- Changing public runtime payload shapes or platform message contracts.
- Automatically deleting duplicate `GamingCouch` objects.
- Publishing any backlog ticket to GitHub or another external service.
- Moving generated content into package samples.

## Further Notes

- DevApp remains the source of truth for GamingCouch local project JSON.
- The quick-start template is intentionally editable project content, not immutable package content.
- Rerun safety is more important than reset convenience for v1.
- The generated starter code should be simple enough for a new user to delete, rename, or expand after they understand the lifecycle.

## Implementation Task Tracker

Overall status: IN PROGRESS

Current task: None

Next action: Implement Task 7.

| Done | Status | Task | Notes |
| --- | --- | --- | --- |
| [x] | DONE | 1. Build editor start screen shell and readiness model | Added the editor window shell, active-scene readiness detection, multiple-instance error state, and local play JSON status display. |
| [x] | DONE | 2. Add launch entry points and auto-open policy | Added menu and inspector entry points, startup-only auto-open policy, project-level suppression, and skip conditions for ready/play/compile/update/non-normal-scene contexts. |
| [x] | DONE | 3. Extract shared scene wiring helpers | Added shared scene wiring helpers with Undo-backed object creation and null-only serialized reference assignment. |
| [x] | DONE | 4. Generate quick-start scripts through staged setup | Added staged no-overwrite script generation, asset refresh, pending compile continuation, path-gated type resolution, and duplicate compiled-type collision blocking. |
| [x] | DONE | 5. Generate and wire quick-start player prefab | Created/reused `GCQuickStartPlayer` prefab through the scripts-ready continuation and wires the active-scene player prefab only when the serialized reference is empty. |
| [x] | DONE | 6. Generate and wire quick-start game listener | Added staged active-scene `GCQuickStartGame` listener creation/reuse and null-only `GamingCouch.listener` assignment. |
| [ ] | TODO | 7. Create quick-start scene flow | Generate, open, save, and add the quick-start scene to Build Settings while preserving unsaved-scene prompts. |
| [ ] | TODO | 8. Complete start screen actions and messaging | Connect checklist actions, primary setup action, blocker messages, rerun warnings, and success states. |
| [ ] | TODO | 9. Add editor tests for detection and generation behavior | Cover readiness states, no-overwrite behavior, reference preservation, suppression state, and build settings updates. |
| [ ] | TODO | 10. Run manual Unity validation | Validate staged compilation, generated scene Play Mode loop, JSON blocker messaging, and rerun safety in Unity 2022.3. |

## Task Details

### Task 1: Build editor start screen shell and readiness model

Objective: Add the editor-only window and a reusable readiness model that reports the active scene state without mutating anything.

Implementation steps:

1. Add an editor window titled `GamingCouch Start Screen`.
2. Add a readiness service that inspects only the active scene.
3. Report missing `GamingCouch`, missing listener, missing player prefab, multiple `GamingCouch` instances, and local play JSON validity.
4. Draw a checklist that maps directly to the readiness model.
5. Keep all action buttons disabled or stubbed until later tasks wire behavior.

Verification:

- Run `git diff --check`.
- Confirm an empty scene reports missing `GamingCouch`.
- Confirm a scene with a bare `GamingCouch` reports missing listener and player prefab.
- Confirm a fully wired scene reports ready.
- Confirm multiple `GamingCouch` objects report an error.

Review pass 2, 2026-05-10:

- No code defects found in the Task 1 scoped files.
- Static inspection confirmed readiness detection scans only the active scene roots, duplicate `GamingCouch` components remain a manual cleanup error, disabled action buttons do not mutate scene/assets, and local play JSON issues are reported through existing `GCDevJsonValidationResult` issues.

Completion notes, 2026-05-10:

- Changed paths: `Editor/GamingCouchStartScreenReadiness.cs`, `Editor/GamingCouchStartScreenReadiness.cs.meta`, `Editor/GamingCouchStartScreenWindow.cs`, `Editor/GamingCouchStartScreenWindow.cs.meta`, `docs/architecture/gamingcouch-quick-start-start-screen-prd.md`.
- Validation: implementation pass, review pass 1, review pass 2, and parent validation ran `git diff --check`.
- Parent static inspection confirmed the Task 1 code does not add menu items, auto-open policy, settings persistence, scene writes, asset writes, prefab writes, or setup mutations.
- Unity 2022.3 editor import/manual scene validation was not run in this package-only environment.
- Validation run: `git diff --check`; scoped trailing-whitespace and later-task scope greps.

### Task 2: Add launch entry points and auto-open policy

Objective: Make the start screen available from expected editor surfaces and auto-open only under the agreed startup conditions.

Implementation steps:

1. Add `GamingCouch/Start Screen` menu item.
2. Add an `Open Start Screen` button to the custom `GamingCouch` inspector.
3. Add startup-only auto-open logic that runs after the editor has an active scene.
4. Skip auto-open if the active scene is ready, auto-show is suppressed, Play Mode is active, or the inspected context is not a normal scene.
5. Add a project-level suppression toggle in the start screen.

Verification:

- Run `git diff --check`.
- Confirm menu and inspector entry points open the same window.
- Confirm auto-open triggers for an incomplete startup scene.
- Confirm auto-open does not trigger for a ready startup scene.
- Confirm suppression prevents future auto-open but does not hide menu access.

Completion notes, 2026-05-10:

- Changed paths: `Editor/GamingCouchEditor.cs`, `Editor/GamingCouchMenuItems.cs`, `Editor/GamingCouchStartScreenLauncher.cs`, `Editor/GamingCouchStartScreenLauncher.cs.meta`, `Editor/GamingCouchStartScreenWindow.cs`, `docs/architecture/gamingcouch-quick-start-start-screen-prd.md`.
- Added `GamingCouch/Start Screen` and the `Open Start Screen` inspector button, both routed to `GamingCouchStartScreenWindow.Open()`.
- Added a one-shot editor-session startup launcher using the Task 1 readiness model; it skips auto-open when the active scene is ready, auto-open is suppressed, Play Mode is active or changing, or the active context is not a normal scene, and waits while the editor is compiling/updating.
- Added a per-project auto-open suppression toggle in the start screen through `EditorUserSettings`.
- Left setup/generation actions as disabled placeholders and kept the existing create-GamingCouch menu action unchanged.
- Validation run: `git diff --check`; `git diff --check --no-index /dev/null Editor/GamingCouchStartScreenLauncher.cs`; `git diff --check --no-index /dev/null Editor/GamingCouchStartScreenLauncher.cs.meta`; scoped static search for later-task setup/generation APIs in Task 2 files, which matched only the pre-existing create-GamingCouch object creation.
- Unity 2022.3 manual editor validation was not run in this package-only environment.

Review pass 1, 2026-05-10:

- No code defects found in the Task 2 scoped files.
- Static inspection confirmed the menu item and inspector button open the same start screen window, auto-open is one-shot per editor session through `SessionState`, suppression persists through `EditorUserSettings`, and auto-open skips ready scenes, suppressed projects, Play Mode or pending Play Mode, compiling/updating editors, prefab stages, and preview scenes.
- Static inspection confirmed setup/generation buttons remain disabled placeholders and the existing create-GamingCouch menu behavior was not changed.
- Validation run: `git diff --check`; `git diff --check --no-index /dev/null Editor/GamingCouchStartScreenLauncher.cs`; `git diff --check --no-index /dev/null Editor/GamingCouchStartScreenLauncher.cs.meta`; scoped static search for later-task setup/generation APIs in Task 2 files.

Review pass 2, 2026-05-10:

- Patched `GCStartScreenStartupLauncher` so compiling/updating states do not consume the one-shot startup check before the editor can make the auto-open decision.
- Static inspection confirmed menu and inspector entry points still route to `GamingCouchStartScreenWindow.Open()`, suppression remains project-scoped through `EditorUserSettings`, and setup/generation actions remain disabled placeholders.
- Static inspection confirmed the existing create-GamingCouch menu behavior was not changed.
- Validation run: `git diff --check`; `git diff --check --no-index /dev/null Editor/GamingCouchStartScreenLauncher.cs`; `git diff --check --no-index /dev/null Editor/GamingCouchStartScreenLauncher.cs.meta`; trailing-whitespace search; scoped static search for later-task setup/generation APIs in Task 2 files.
- Unity 2022.3 manual editor validation was not run in this package-only environment.

### Task 3: Extract shared scene wiring helpers

Objective: Provide safe reusable setup operations for current-scene wiring.

Implementation steps:

1. Replace direct menu-only `GamingCouch` creation with a shared helper.
2. Use Undo for created scene objects and serialized reference edits.
3. Create a missing `GamingCouch` object only when there is no active-scene instance.
4. Never auto-delete duplicate instances.
5. Assign listener and player prefab only when the serialized fields are null.

Verification:

- Run `git diff --check`.
- Confirm the existing create menu still creates and selects a `GamingCouch` object.
- Confirm existing non-null listener and prefab references are preserved.
- Confirm duplicate instances block automatic setup.

Completion notes, 2026-05-10:

- Changed paths: `Editor/GamingCouchSceneWiring.cs`, `Editor/GamingCouchSceneWiring.cs.meta`, `Editor/GamingCouchMenuItems.cs`, `Editor/GamingCouchStartScreenReadiness.cs`, `docs/architecture/gamingcouch-quick-start-start-screen-prd.md`.
- Added internal helper APIs for active-scene `GamingCouch` discovery, create-or-reuse behavior, duplicate-instance blocking, serialized reference reads, and null-only listener/player prefab assignment.
- Routed the existing `GameObject/GamingCouch`, `Assets/Create/GamingCouch`, and `GamingCouch/Create GamingCouch GameObject` menu paths through the shared helper; successful runs select the created or existing active-scene `GamingCouch`, and duplicate active-scene instances report a blocking dialog.
- Used `Undo.RegisterCreatedObjectUndo` for created scene objects and `Undo.RecordObject` for serialized listener/player prefab reference edits.
- Left generated scripts, prefab generation, listener generation, scene generation, and start-screen action wiring untouched.
- Validation run: `git diff --check`; `git diff --check --no-index /dev/null Editor/GamingCouchSceneWiring.cs`; `git diff --check --no-index /dev/null Editor/GamingCouchSceneWiring.cs.meta`; scoped static search for generation/scene/prefab APIs in Task 3 files.
- Unity 2022.3 manual editor validation was not run in this package-only environment.

Review pass 1, 2026-05-10:

- Patched null-only serialized reference assignment to treat missing/broken Unity object references as occupied serialized slots, so quick-start setup cannot overwrite user-owned listener or player prefab fields that currently resolve to `null`.
- Static inspection confirmed active-scene-only `GamingCouch` discovery, duplicate active-scene instances remain a blocking state without deletion, menu creation routes through the shared helper, and no generated script, prefab, listener, scene, build-settings, or start-screen action wiring was added.
- Validation run: `git diff --check`; trailing-whitespace/conflict-marker search across Task 3 files; scoped static searches for later-task generation/action APIs and active-scene-only discovery APIs.
- Unity 2022.3 manual editor validation was not run in this package-only environment.

Review pass 2, 2026-05-10:

- Patched Undo ordering so created `GamingCouch` scene objects are registered immediately after `GameObject` construction, before adding the `GamingCouch` component.
- Patched serialized reference assignment to pair the explicit `Undo.RecordObject` call with `SerializedObject.ApplyModifiedPropertiesWithoutUndo()`, keeping listener/player prefab edits in the named Undo operation without adding a second SerializedObject undo registration.
- Static inspection confirmed active-scene-only `GamingCouch` discovery, duplicate active-scene instances remain a blocking state without deletion, menu creation routes through the shared helper, non-null and missing/broken serialized references remain preserved, and no generated script, prefab, listener, scene, build-settings, or start-screen action wiring was added.
- Validation run: `git diff --check`; `git diff --check --no-index /dev/null Editor/GamingCouchSceneWiring.cs`; `git diff --check --no-index /dev/null Editor/GamingCouchSceneWiring.cs.meta`; trailing-whitespace/conflict-marker search across Task 3 files; scoped static searches for later-task generation/action APIs and active-scene-only discovery APIs.
- Unity 2022.3 manual editor validation was not run in this package-only environment.

### Task 4: Generate quick-start scripts through staged setup

Objective: Generate missing starter scripts in a way that survives Unity compilation boundaries.

Implementation steps:

1. Create the quick-start asset folder if missing.
2. Generate `GCQuickStartGame` and `GCQuickStartPlayer` scripts only when absent.
3. Do not overwrite scripts that already exist.
4. Refresh assets and persist pending setup state for continuation after compilation.
5. Resume setup after compilation and continue with prefab, listener, and scene wiring.

Verification:

- Run `git diff --check`.
- Confirm first run creates missing scripts.
- Confirm rerun does not modify existing script contents.
- Confirm setup can resume after Unity compiles generated scripts.

Completion notes, 2026-05-10:

- Changed paths: `Editor/GamingCouchQuickStartSetup.cs`, `Editor/GamingCouchQuickStartSetup.cs.meta`, `docs/architecture/gamingcouch-quick-start-start-screen-prd.md`.
- Added an internal staged setup coordinator that creates `Assets/GamingCouch/QuickStart` through `AssetDatabase` when run inside Unity, generates `GCQuickStartGame.cs` and `GCQuickStartPlayer.cs` with `FileMode.CreateNew`, refreshes assets after file creation, and never overwrites existing script assets.
- Added `SessionState` pending setup persistence plus an `InitializeOnLoad` resume poller that waits for compilation/update to finish and dispatches a scripts-ready continuation hook for later prefab, listener, and scene wiring tasks.
- Kept start screen setup buttons disabled placeholders; Task 4 only exposes the editor service and does not create prefabs, listeners, scenes, build settings entries, or package-root generated assets.
- Validation run: `git diff --check`; `git diff --check --no-index /dev/null Editor/GamingCouchQuickStartSetup.cs`; `git diff --check --no-index /dev/null Editor/GamingCouchQuickStartSetup.cs.meta`; `find . -maxdepth 3 -path './Assets/GamingCouch/QuickStart' -type d`; scoped static searches for prefab/scene/build APIs, conflict markers, no-overwrite/refresh/session state usage, and disabled start-screen actions.
- Unity 2022.3 manual editor validation was not run in this package-only environment.

Review pass 1, 2026-05-10:

- Patched quick-start folder creation to import existing on-disk directories, block existing files, verify `AssetDatabase.CreateFolder` created the exact requested `Assets/GamingCouch` or `Assets/GamingCouch/QuickStart` path, and block if Unity cannot import the result as a valid asset folder.
- Patched staged continuation type resolution to load `MonoScript` assets from the generated script paths and use `MonoScript.GetClass()`, so unrelated same-named compiled types cannot satisfy the scripts-ready gate.
- Static inspection confirmed generated assets still target project-owned `Assets/GamingCouch/QuickStart`, scripts are written with `FileMode.CreateNew`, existing scripts are reused without overwriting, refresh is only triggered after script file creation, and no prefab, listener, scene, build-settings, or start-screen action wiring was added.
- Validation run: `git diff --check`; `git diff --check --no-index /dev/null Editor/GamingCouchQuickStartSetup.cs`; `git diff --check --no-index /dev/null Editor/GamingCouchQuickStartSetup.cs.meta`; scoped static searches for package-local `Assets` generation, prefab/listener/scene/build-settings/start-screen action APIs, no-overwrite/refresh/session state usage, and generated source API references.
- Unity 2022.3 manual editor validation was not run in this package-only environment.

Review pass 2, 2026-05-10:

- Patched script reuse to import existing on-disk `.cs` files before the `MonoScript.GetClass()` gate, while still blocking folder and non-script asset collisions at the generated script paths.
- Patched pending handler registration to restart the resume poller when a handler is registered while setup is pending.
- Patched script creation to block generation when a compiled same-named type already exists outside the generated script path, avoiding duplicate class compile errors.
- Patched the script type gate to reject abstract or generic compiled types, added the required `System.Reflection` import for resilient type loading, and added a concrete `GCQuickStartPlayer.GetHudValueText()` override so the generated player remains compatible with this repo's `GCPlayer` HUD contract.
- Static inspection confirmed generated assets still target project-owned `Assets/GamingCouch/QuickStart`, scripts are written with `FileMode.CreateNew`, existing scripts are not overwritten, `SessionState`/domain-reload continuation remains in place, and no prefab, listener, scene, build-settings, or start-screen action wiring was added.
- Validation run: `git diff --check`; `git diff --check --no-index /dev/null Editor/GamingCouchQuickStartSetup.cs`; `git diff --check --no-index /dev/null Editor/GamingCouchQuickStartSetup.cs.meta`; scoped static searches for conflict markers, package-local `Assets` generation, prefab/listener/scene/build-settings/start-screen action APIs, no-overwrite/refresh/session state usage, and generated source API references.
- Unity 2022.3 manual editor validation was not run in this package-only environment.

### Task 5: Generate and wire quick-start player prefab

Objective: Create or reuse an editable player prefab that satisfies `GamingCouch.playerPrefab`.

Implementation steps:

1. Create a root prefab with `GCQuickStartPlayer`.
2. Add a simple 3D primitive child or equivalent built-in visual.
3. Make the player apply `ColorBase` to the visual at runtime.
4. Save the prefab under the quick-start folder.
5. Assign it to `GamingCouch.playerPrefab` only when that reference is null.

Verification:

- Run `git diff --check`.
- Confirm the prefab includes a `GCPlayer`-derived component.
- Confirm the prefab has a visible placeholder.
- Confirm rerun reuses the prefab without overwriting it.

Implementation pass notes, 2026-05-10:

- Changed paths: `Editor/GamingCouchQuickStartSetup.cs`, `docs/architecture/gamingcouch-quick-start-start-screen-prd.md`.
- Added `Assets/GamingCouch/QuickStart/GCQuickStartPlayer.prefab` as the generated prefab target, created only from the scripts-ready continuation after the compiled `GCQuickStartPlayer` type is available.
- Generated first-run prefabs have `GCQuickStartPlayer` on the root, a built-in capsule child named `Visual`, and the generated player script's `colorRenderer` serialized reference assigned to the child renderer so `ColorBase` can apply at runtime.
- Existing prefab assets at the target path are reused without modification when their root has `GCQuickStartPlayer`; folder, file import, non-prefab asset, and wrong-root-component collisions block with explicit reasons instead of overwriting.
- The continuation wires the active scene by reusing Task 3 scene helpers and assigns `GamingCouch.playerPrefab` only through the null-only serialized reference helper, preserving existing non-null, missing, or broken serialized references.
- Quick-start scene assignment is deferred for the later scene-wiring task; this task did not create listeners, scenes, Build Settings entries, or start-screen action wiring.
- Validation run: `git diff --check`; scoped static searches for prefab creation/reuse APIs, listener/scene/build-settings exclusions, null-only assignment usage, and generated QuickStart folder absence in the package worktree.
- Unity 2022.3 manual editor validation was not run in this package-only environment.

Review pass 1, 2026-05-10:

- Patched defensive cleanup for the temporary primitive used during prefab creation, so an exception before parenting cannot leave an unparented generated object in the active scene.
- Static inspection confirmed prefab generation remains behind the scripts-ready continuation, existing prefab assets are reused without overwrite, wrong-type/collision cases block, ActiveScene assignment uses the Task 3 null-only helper, and QuickStartScene assignment remains deferred.
- Validation run: `git diff --check`; conflict-marker search; scoped static searches for listener, scene, Build Settings, and start-screen action APIs.
- Unity 2022.3 manual editor validation was not run in this package-only environment.

Review pass 2, 2026-05-10:

- Changed paths: `Editor/GamingCouchQuickStartSetup.cs`, `Editor/GamingCouchSceneWiring.cs`, `docs/architecture/gamingcouch-quick-start-start-screen-prd.md`.
- Patched existing prefab collision handling to reuse only regular prefab assets and prefab variants, while blocking non-prefab assets and immutable/model-style prefab imports without overwriting them.
- Patched the shared null-only scene wiring helper to explicitly mark the owning loaded scene dirty after serialized listener/player prefab assignments, while still preserving existing non-null, missing, or broken serialized references.
- Static inspection confirmed the built-in player prefab handler is de-duplicated through `RegisterScriptsReadyHandler`, prefab generation remains staged behind the scripts-ready continuation, temporary creation objects are destroyed in `finally`, and Task 5 still does not add listeners, scenes, Build Settings edits, or start-screen action wiring.
- Validation run: `git diff --check`; conflict-marker search; scoped static searches for forbidden listener/scene/Build Settings/start-screen APIs; scoped signature/call searches for `SaveAsPrefabAsset`, prefab asset type handling, temporary object cleanup, continuation registration, null-only assignment, and scene dirty marking.
- Unity 2022.3 manual editor validation was not run in this package-only environment.

Parent validation, 2026-05-10:

- Validation run: `git diff --check`; conflict-marker search; scoped static searches for forbidden listener/scene/Build Settings/start-screen APIs; scoped signature/call searches for `EnsureScriptAsset`, prefab creation/reuse APIs, continuation registration, null-only assignment, and scene dirty marking; generated QuickStart folder absence check.
- No package-local `Assets/GamingCouch/QuickStart` directory was created during command-line validation.

### Task 6: Generate and wire quick-start game listener

Objective: Create or reuse the listener object and component that demonstrates the GamingCouch lifecycle.

Implementation steps:

1. Create a `GCQuickStartGame` scene object if no listener is assigned.
2. Implement `GamingCouchSetup(GCSetupOptions)` to configure a versus game and call `SetupDone()`.
3. Implement `GamingCouchPlay(GCPlayOptions)` to spawn players and start the round timer.
4. Use serialized defaults for `roundSeconds` and `maxScore`.
5. Assign random final scores and call `GameOver()` when the timer completes.
6. Assign the listener reference only when it is null.

Verification:

- Run `git diff --check`.
- Confirm generated setup/play hook names match GamingCouch `SendMessage` calls.
- Confirm the listener object is assigned to `GamingCouch.listener`.
- Confirm rerun preserves an existing listener assignment.

Implementation pass notes, 2026-05-10:

- Changed paths: `Editor/GamingCouchQuickStartSetup.cs`, `Editor/GamingCouchSceneWiring.cs`, `docs/architecture/gamingcouch-quick-start-start-screen-prd.md`.
- Added a scripts-ready `GCQuickStartGame` listener continuation beside the Task 5 player prefab continuation, registered through the same de-duplicating handler API.
- Active-scene setup now creates or reuses a `GCQuickStartGame` listener object only after the compiled `context.gameType` is available, uses Undo for created scene objects and existing-object component additions, marks the scene dirty, and assigns `GamingCouch.listener` only through the existing null-only serialized reference helper.
- Existing non-null, missing, or broken listener references are treated as occupied serialized references before any listener object is created, preserving user-owned wiring.
- QuickStartScene listener assignment remains deferred to the later scene flow; this task did not create scenes, Build Settings entries, start-screen actions, tests, or package-local generated `Assets/GamingCouch/QuickStart` content during command-line validation.
- The generated `GCQuickStartGame` source already configures versus play with a points HUD, calls `SetupPlayers<GCQuickStartPlayer>()`, runs the serialized timer, assigns random scores, marks players finished, and calls `GameOver()`.
- Validation run: `git diff --check`; conflict-marker search; scoped static searches for forbidden scene/Build Settings/start-screen APIs, scripts-ready handler registration, listener null-only assignment, generated setup/play hook names, points HUD, player spawning, random scores, `GameOver()`, Undo usage, scene dirty marking, and generated QuickStart folder absence.
- Unity 2022.3 manual editor validation was not run in this package-only command-line environment.

Review pass 1, 2026-05-10:

- Patched listener reuse to block when a scene contains multiple `GCQuickStartGame` components, avoiding nondeterministic assignment of an arbitrary generated listener when `GamingCouch.listener` is empty.
- Patched new listener object creation so the dynamic `GCQuickStartGame` component is added before `Undo.RegisterCreatedObjectUndo`, allowing failed component creation to destroy the temporary object without leaving a registered half-created listener behind.
- Static inspection confirmed existing serialized listener references, including missing or broken object references, still prevent auto-assignment; a single existing `GCQuickStartGame` component is reused by assigning its owning GameObject; QuickStartScene work remains deferred to Task 7.
- Validation run: `git diff --check`; conflict-marker search; scoped static searches for forbidden scene/Build Settings/start-screen APIs, listener null-only assignment, Undo usage, scene dirty marking, scripts-ready handler registration, generated setup/play hook names, `SetupDone`, `SetupPlayers`, random score assignment, `GameOver`, and generated QuickStart folder absence.
- Unity 2022.3 manual editor validation was not run in this package-only command-line environment.

Review pass 2, 2026-05-10:

- Changed paths: `Editor/GamingCouchQuickStartSetup.cs`, `Editor/GamingCouchSceneWiring.cs`, `docs/architecture/gamingcouch-quick-start-start-screen-prd.md`.
- Patched listener setup to verify the `GamingCouch.listener` serialized object-reference slot exists before creating or mutating any `GCQuickStartGame` scene object, preventing an orphan listener object if assignment would be blocked by a missing or incompatible serialized field.
- Static inspection confirmed Unity 2022.3 dynamic component APIs are used with `Component` results, existing serialized listener references still short-circuit before creation, duplicate `GCQuickStartGame` components still block rerun assignment, and ActiveScene scripts-ready handlers can independently converge on `GamingCouch`, player prefab, and listener references when those serialized slots are empty.
- Static inspection confirmed generated `GamingCouchSetup`/`GamingCouchPlay` hook names match `SendMessage(..., RequireReceiver)`, `SetupDone()` happens before `SetupPlayers<GCQuickStartPlayer>()`, the points HUD config has a positive `maxScore`, the round timer assigns random scores and finished state, and `GameOver()` is called after the timer.
- Validation run: `git diff --check`; conflict-marker search; scoped static searches for forbidden scene open/save/create, Build Settings, and start-screen action APIs; scoped searches for listener null-only assignment, scripts-ready registration, Undo/component creation, scene dirty marking, generated setup/play hook names, points HUD, timer, random scores, `SetupDone`, `SetupPlayers`, and `GameOver`.
- Unity 2022.3 manual editor validation was not run in this package-only command-line environment.

Parent validation, 2026-05-10:

- Validation run: `git diff --check`; conflict-marker search; scoped static searches for forbidden scene open/save/create, Build Settings, and start-screen action APIs; scoped searches for listener reference-slot checks, null-only listener assignment, scripts-ready registration, Undo/component creation, scene dirty marking, generated setup/play hook names, points HUD, timer, random scores, `SetupDone`, `SetupPlayers`, `GameOver`, and generated QuickStart folder absence.
- No package-local `Assets/GamingCouch/QuickStart` directory was created during command-line validation.

### Task 7: Create quick-start scene flow

Objective: Generate a complete quick-start scene for users who want a clean playable starting point.

Implementation steps:

1. Prompt to save modified scenes before replacing the active scene.
2. Create or open the quick-start scene.
3. Add camera and light if the scene is newly created.
4. Run the same wiring helpers used by current-scene setup.
5. Save the scene.
6. Add the scene to Build Settings if not already present.

Verification:

- Run `git diff --check`.
- Confirm the generated scene opens after creation.
- Confirm the scene contains the wired `GamingCouch`, listener, player prefab reference, camera, and light.
- Confirm Build Settings contains the scene once, not duplicated.

### Task 8: Complete start screen actions and messaging

Objective: Connect the UI to the setup flows and make success and blocker states clear.

Implementation steps:

1. Wire individual checklist actions.
2. Wire the primary `Set up missing pieces` action.
3. Wire the `Create new quick-start scene` action.
4. Show clear warnings for missing or invalid local play JSON without repairing it.
5. Show a success state when the scene is ready.
6. Show rerun messages when existing assets are reused.

Verification:

- Run `git diff --check`.
- Confirm each button maps to the intended setup operation.
- Confirm JSON blockers are informational and do not write JSON files.
- Confirm success state appears after setup completes.

### Task 9: Add editor tests for detection and generation behavior

Objective: Cover the behavior most likely to regress without depending on IMGUI details.

Implementation steps:

1. Add tests for readiness detection states.
2. Add tests for no-overwrite behavior.
3. Add tests for preserving non-null listener and prefab references.
4. Add tests for suppression setting behavior.
5. Add tests for Build Settings insertion without duplicates.

Verification:

- Run the available Unity editor test suite if present.
- If no automated Unity test runner is available in this environment, record the exact skipped command and manual validation gap.
- Run `git diff --check`.

### Task 10: Run manual Unity validation

Objective: Confirm the full first-run experience in Unity 2022.3.

Implementation steps:

1. Open Unity into an empty scene and confirm auto-open behavior.
2. Run `Set up missing pieces`.
3. Confirm generated scripts compile and setup resumes.
4. Press Play with valid local play JSON and confirm the 10-second round finishes.
5. Stop Play Mode and rerun setup to confirm no generated files are overwritten.
6. Test missing or invalid local play JSON and confirm it blocks play with clear messaging.
7. Test suppressing auto-open and reopening the start screen from the menu.

Verification:

- Record Unity version used.
- Record pass/fail for each manual scenario.
- Update this document with final status and any remaining follow-up tasks.
