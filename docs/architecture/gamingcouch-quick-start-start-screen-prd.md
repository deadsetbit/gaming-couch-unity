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

Next action: Implement Task 2.

| Done | Status | Task | Notes |
| --- | --- | --- | --- |
| [x] | DONE | 1. Build editor start screen shell and readiness model | Added the editor window shell, active-scene readiness detection, multiple-instance error state, and local play JSON status display. |
| [ ] | TODO | 2. Add launch entry points and auto-open policy | Add menu entry, inspector button, startup check, project-level suppression, and skip conditions for Play Mode and non-startup reloads. |
| [ ] | TODO | 3. Extract shared scene wiring helpers | Centralize creating a `GamingCouch` object, assigning serialized references safely, preserving existing references, and using Undo. |
| [ ] | TODO | 4. Generate quick-start scripts through staged setup | Create missing starter scripts, refresh assets, persist pending setup, and resume after compilation without overwriting existing scripts. |
| [ ] | TODO | 5. Generate and wire quick-start player prefab | Create or reuse a `GCQuickStartPlayer` prefab with `GCPlayer` inheritance and a simple 3D placeholder visual. |
| [ ] | TODO | 6. Generate and wire quick-start game listener | Create or reuse a listener object/component that configures versus play, spawns players, runs the timer, assigns scores, and ends the game. |
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
