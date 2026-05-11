# GamingCouch Quick Start Start Screen PRD

Status: Ready for implementation
Last updated: 2026-05-11
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
26. As a Unity user preparing a WebGL or local preview build, I want the start screen to show whether the active scene is the first enabled Build Settings scene, so that Play Mode and build launch use the scene I just configured.
27. As a Unity user testing the generated quick-start flow, I want the start screen to show whether the Game View is using a 16:9 preview aspect, so that the editor preview matches the expected GamingCouch web embed shape.
28. As a new Unity package user, I want the main setup action to fix all safe automatable readiness issues, so that I can get from an incomplete scene to a launch-ready scene with minimal manual steps.
29. As a Unity user, I want checklist rows that cannot be fixed safely to show clear manual guidance, so that I understand what remains without the package making risky editor changes.

## Implementation Decisions

- Build an editor-only GamingCouch start screen using the package's existing IMGUI editor style.
- Treat an active scene as GamingCouch-ready only when it has exactly one `GamingCouch` component and that component has both listener and player prefab references assigned.
- Auto-open the start screen once on Unity editor startup when any blocking visible checklist row is incomplete.
- Do not auto-open on later scene changes, every domain reload, or while entering Play Mode.
- Add a persistent per-project setting to suppress automatic opening.
- Keep the start screen title in the EditorWindow tab only; the content starts with active-scene readiness.
- Place the auto-open suppression control at the bottom of the window with the label `Never open this again on startup`.
- Keep manual entry points through the GamingCouch top menu and the `GamingCouch` inspector.
- Show setup actions only for missing checklist rows; ready GamingCouch/listener/prefab rows offer focus actions for the resolved scene object or asset.
- Use one visible checklist row labeled `Exactly one GamingCouch in scene` for active-scene `GamingCouch` count: missing is an actionable create state, exactly one is ready and focusable, and more than one is a red manual-cleanup error with no setup or focus action on that row.
- Polish update, 2026-05-11: keep the start screen focused on active-scene setup by showing the global `Set up missing pieces` action only while checklist rows have actionable setup work, hiding the global Actions section once no global actions remain, and not showing a global quick-start scene creation action in this window. Occupied serialized listener/player references, including broken or missing-object references, are not actionable for no-overwrite setup. The Task 11 launch-readiness scope extends this action so it can still fix safe Build Settings and Game View readiness work when active-scene wiring is already complete.
- Polish update, 2026-05-11: keep active-scene validity as an internal readiness/action guard while excluding the active-scene guard from the visible checklist. If no usable active scene is loaded, show that issue as a standalone help box before the readiness summary/checklist. Keep the scene summary to name/path only; do not show ready or generic setup summary boxes because the checklist already communicates readiness.
- Polish update, 2026-05-11: reserve action-result help boxes for warning and error outcomes only. Normal successful, focus, reuse, and no-op action confirmations stay silent because the checklist already reflects current setup state.
- Polish update, 2026-05-11: represent each checklist row state with one left-side colored indicator and tooltip. Do not show text markers such as `[x]`, warning/error marker text, or a duplicate right-side status indicator.
- Follow-up scope, 2026-05-11: add active-scene Build Settings readiness to the same checklist, not a separate section. The row passes only when the active scene has a saved scene path and is the first enabled entry in `EditorBuildSettings.scenes`. Missing, disabled, later, duplicate, and unsaved active-scene states should be clear checklist failures or blockers.
- Follow-up scope, 2026-05-11: add Game View 16:9 readiness to the same checklist. A confirmed 16:9 Game View passes, a confirmed non-16:9 Game View fails, and an uninspectable Game View state is a yellow non-blocking warning. Unknown Game View state must not trigger startup auto-open by itself.
- Follow-up scope, 2026-05-11: make `Set up missing pieces` fix all safe automatable checklist failures. It should keep existing scene wiring behavior, prompt to save an unsaved active scene when needed for Build Settings setup, make the active scene first and enabled in Build Settings, and select an existing 16:9 Game View entry when that can be done safely. It must not create custom Game View sizes and must not create or repair `gc.dev.json`.
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
- Hide local play JSON details when the file is valid without warnings so the start screen stays focused on actionable setup.
- Report multiple active-scene `GamingCouch` instances as a manual cleanup error.

## Testing Decisions

- Prefer automated editor tests for readiness detection, rerun safety, generated asset reuse, scene wiring, and build settings insertion.
- Test behavior at the editor service boundary rather than testing private IMGUI layout details.
- Include tests for incomplete scenes, fully wired scenes, missing listener, missing player prefab, multiple `GamingCouch` objects, and suppressed auto-open settings.
- Include tests that prove setup does not overwrite existing generated files or non-null serialized references.
- Include tests for active-scene Build Settings readiness: saved scene first and enabled, unsaved active scene, missing from Build Settings, present but disabled, present but not first, duplicate entries, and setup preserving unrelated scenes while moving/enabling the active scene.
- Include tests or static coverage proving setup actions remain available when only Build Settings or Game View readiness is incomplete.
- Include manual Unity validation for the staged compile flow, because generated scripts must compile before components can be attached.
- Include manual Play Mode validation with a valid local play JSON file: setup runs, players spawn, a 10-second round assigns scores, and `GameOver()` is called.
- Include manual validation that missing or invalid local play JSON is reported clearly and is not repaired by Unity.
- Include manual Unity 2022.3 validation for Game View 16:9 detection and any optional aspect-selection action, because the relevant editor APIs are internal and version-sensitive.

## Out of Scope

- Creating, bootstrapping, repairing, or migrating `gc.dev.json` or `gc.metadata.json`.
- Adding a full sample game or controller-input mini-game.
- Changing public runtime payload shapes or platform message contracts.
- Automatically deleting duplicate `GamingCouch` objects.
- Creating custom Game View sizes or changing runtime resolution, WebGL template layout, or build output sizing as part of the 16:9 checklist item.
- Publishing any backlog ticket to GitHub or another external service.
- Moving generated content into package samples.

## Further Notes

- DevApp remains the source of truth for GamingCouch local project JSON.
- The quick-start template is intentionally editable project content, not immutable package content.
- Rerun safety is more important than reset convenience for v1.
- The generated starter code should be simple enough for a new user to delete, rename, or expand after they understand the lifecycle.

## Implementation Task Tracker

Overall status: BLOCKED

Current task: 11. Add launch-readiness checklist rows

Next action: Provide a Unity 2022.3 editor plus a Unity project that consumes this checkout, then run Task 11 edit-mode tests and manual Game View validation. Task 10 manual validation remains blocked on the same environment gap.

| Done | Status | Task | Notes |
| --- | --- | --- | --- |
| [x] | DONE | 1. Build editor start screen shell and readiness model | Added the editor window shell, active-scene readiness detection, multiple-instance error state, and local play JSON status display. |
| [x] | DONE | 2. Add launch entry points and auto-open policy | Added menu and inspector entry points, startup-only auto-open policy, project-level suppression, and skip conditions for ready/play/compile/update/non-normal-scene contexts. |
| [x] | DONE | 3. Extract shared scene wiring helpers | Added shared scene wiring helpers with Undo-backed object creation and null-only serialized reference assignment. |
| [x] | DONE | 4. Generate quick-start scripts through staged setup | Added staged no-overwrite script generation, asset refresh, pending compile continuation, path-gated type resolution, and duplicate compiled-type collision blocking. |
| [x] | DONE | 5. Generate and wire quick-start player prefab | Created/reused `GCQuickStartPlayer` prefab through the scripts-ready continuation and wires the active-scene player prefab only when the serialized reference is empty. |
| [x] | DONE | 6. Generate and wire quick-start game listener | Added staged active-scene `GCQuickStartGame` listener creation/reuse and null-only `GamingCouch.listener` assignment. |
| [x] | DONE | 7. Create quick-start scene flow | Added staged quick-start scene creation/opening, scene wiring, saving, and Build Settings insertion. |
| [x] | DONE | 8. Complete start screen actions and messaging | Wired checklist and primary active-scene setup actions with pending, blocker, checklist readiness, reuse-safe setup, and read-only JSON messaging. |
| [x] | DONE | 9. Add editor tests for detection and generation behavior | Added edit-mode coverage for readiness states, no-overwrite behavior, reference preservation, suppression state, and build settings updates. |
| [ ] | BLOCKED | 10. Run manual Unity validation | Blocked: no Unity 2022.3 executable is installed or on PATH, this checkout is a Unity package rather than a Unity project, and nearby 2022.3 projects that consume GamingCouch reference GitHub URLs instead of this checkout. |
| [ ] | BLOCKED | 11. Add launch-readiness checklist rows | Implementation and review passes are complete, but Unity 2022.3 edit-mode tests and manual Game View validation are blocked by the local environment. |

## Task Details

### Task 1: Build editor start screen shell and readiness model

Objective: Add the editor-only window and a reusable readiness model that reports the active scene state without mutating anything.

Implementation steps:

1. Add an editor window titled `GamingCouch Start Screen`.
2. Add a readiness service that inspects only the active scene.
3. Report missing `GamingCouch`, missing listener, missing player prefab, multiple `GamingCouch` instances, and local play JSON validity.
4. Draw a visible setup checklist from the displayable readiness rows.
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

Implementation pass notes, 2026-05-10:

- Changed paths: `Editor/GamingCouchQuickStartSetup.cs`, `docs/architecture/gamingcouch-quick-start-start-screen-prd.md`.
- Added `Assets/GamingCouch/QuickStart/GamingCouchQuickStart.unity` as the quick-start scene target and exposed `CreateOrOpenQuickStartScene()` as an editor service API without wiring start-screen buttons.
- Added the `QuickStartScene` scripts-ready continuation path: script generation can queue through compilation, then scene setup resumes after compiled `GCQuickStartGame` and `GCQuickStartPlayer` types are available.
- The scene flow prompts through `EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()` before replacing the active scene, opens/reuses an existing quick-start scene when present, or creates a new empty scene when absent.
- Newly created scenes get Undo-backed camera and directional light objects; reruns of existing scenes do not add duplicate camera/light objects.
- Scene wiring reuses the existing helpers for `GamingCouch` creation, quick-start prefab creation/reuse, quick-start listener creation/reuse, and null-only listener/player prefab assignment, preserving non-null, missing, or broken serialized references.
- Successful setup saves the quick-start scene and appends it to `EditorBuildSettings.scenes` only when the scene path is not already present.
- Validation run: `git diff --check`; conflict-marker search; scoped static searches for scene open/new/save APIs, Build Settings insertion, QuickStartScene staged handler, camera/light creation, start-screen button wiring exclusion, and generated QuickStart folder absence.
- Unity 2022.3 manual editor validation was not run in this package-only command-line environment.

Review pass 1 notes, 2026-05-10:

- Patched the quick-start scene flow so it opens or creates and explicitly activates `Assets/GamingCouch/QuickStart/GamingCouchQuickStart.unity` before creating the player prefab or running active-scene wiring helpers. This keeps prefab temporary objects and `GamingCouch` wiring scoped to the quick-start scene instead of the previously active scene.
- Left Task 7 `IN PROGRESS`; next action is Task 7 review pass 2.

Review pass 2 notes, 2026-05-10:

- No additional code defects found in the Task 7 scoped scene-service flow.
- Static inspection confirmed `CreateOrOpenQuickStartScene()` uses the `QuickStartScene` continuation, ActiveScene prefab/listener handlers defer for that intent, and `EnsureQuickStartSceneOnScriptsReady()` performs the scene setup once scripts are available.
- Static inspection confirmed scene replacement is gated by `EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()` unless the active loaded scene is already `Assets/GamingCouch/QuickStart/GamingCouchQuickStart.unity`, and active-scene helpers run only after the quick-start scene is active.
- Static inspection confirmed reruns reuse existing scripts, prefab assets, scene assets, non-null or broken serialized references, and Build Settings entries; camera and light creation is limited to newly created scenes.
- Failure-mode decision: setup may leave the opened or newly created quick-start scene dirty if later prefab/listener/wiring/save blockers occur; this is acceptable for Task 7 because the flow reports a blocking result and avoids overwriting existing assets or serialized references.
- Validation run: `git diff --check`; conflict-marker search; scoped static searches for Unity scene APIs, Build Settings insertion, QuickStartScene handler/deferred ActiveScene handlers, camera/light creation, start-screen action wiring exclusion, and generated QuickStart folder absence.
- Unity 2022.3 manual editor validation was not run in this package-only command-line environment.
- Left Task 7 `IN PROGRESS`; next action is parent validation for Task 7.

Parent validation, 2026-05-10:

- Validation run: `git diff --check`; conflict-marker search; scoped static searches for Unity scene APIs, Build Settings insertion, QuickStartScene handler, deferred ActiveScene handlers, camera/light creation, start-screen action wiring exclusion, `CreateOrOpenQuickStartScene()`, and generated QuickStart folder absence.
- No package-local `Assets/GamingCouch/QuickStart` directory was created during command-line validation.

### Task 8: Complete start screen actions and messaging

Objective: Connect the UI to the setup flows and make readiness and blocker states clear.

Implementation steps:

1. Wire individual checklist actions.
2. Wire the primary `Set up missing pieces` action.
3. Keep quick-start scene creation out of the start screen.
4. Show clear warnings for missing or invalid local play JSON without repairing it.
5. Rely on the checklist to show the ready state after successful setup.
6. Keep reruns safe when existing assets are reused without showing normal informational action boxes.

Verification:

- Run `git diff --check`.
- Confirm each button maps to the intended setup operation.
- Confirm JSON blockers are informational and do not write JSON files.
- Confirm the checklist reflects readiness after setup completes without showing a normal success action box.

Implementation notes, 2026-05-10:

- Changed paths: `Editor/GamingCouchStartScreenWindow.cs`, `Editor/GamingCouchQuickStartSetup.cs`, `docs/architecture/gamingcouch-quick-start-start-screen-prd.md`.
- Replaced disabled placeholder actions with active IMGUI buttons for GamingCouch creation/reuse, listener wiring, player prefab wiring, and primary active-scene setup.
- Added a staged quick-start action discriminator so individual listener and player prefab buttons resume narrowly after script compilation instead of running the whole active-scene setup.
- Primary active-scene setup creates/reuses the active-scene `GamingCouch`, generated player prefab, generated listener, and null-only serialized references through existing setup and scene-wiring helpers.
- Missing or invalid `gc.dev.json` is displayed as Play Mode readiness only, with explicit messaging that the start screen will not create or repair the file.
- Scene setup readiness is reflected by the checklist separately from local Play Mode JSON readiness, and normal reused/unchanged setup confirmations do not render action-result help boxes.
- Multiple active-scene `GamingCouch` components keep active-scene setup actions blocked with manual cleanup messaging; quick-start scene creation remains outside this window.
- Validation run: `git diff --check`; conflict-marker search; scoped action-wiring search; scoped JSON write API absence search; primary setup path search; pending compilation path search; `git status --short` to confirm no tests or manual scene assets were generated.
- Unity 2022.3 manual editor validation was not run in this package-only shell.

Review pass 1, 2026-05-10:

- Patched pending setup resume to validate the stored action against the stored intent and fall back to the intent-specific default action. This keeps stale or missing pending action session data from turning a quick-start scene continuation into a no-op after compilation.
- Patched primary active-scene setup to ensure the active scene has zero-or-one `GamingCouch` before generating quick-start scripts, so invalid scenes and duplicate `GamingCouch` states block without writing script assets first.
- Patched active-scene action disabled-state handling to block when readiness has no valid active scene, in addition to pending compilation and duplicate `GamingCouch` states.
- Validation run: `git diff --check`; conflict-marker search; scoped signature/callsite search for `CreateContinuationContext`, `PersistPendingSetup`, and `EnsureQuickStartScripts`; scoped action handler gating search; scoped JSON write API absence search in Task 8 touched editor files; generated asset/test absence check via `git status --short`.
- Unity 2022.3 manual editor validation was not run in this package-only shell.

Review pass 2, 2026-05-10:

- No additional code defects were found in the Task 8 scoped files.
- Confirmed all continuation context constructor calls and setup helper callsites use the action-aware signatures, and pending action recovery falls back to the stored intent's valid default action.
- Confirmed active-scene pending continuations keep primary setup broad while listener and player prefab actions resume only their own references; quick-start scene continuations remain scene-only.
- Confirmed active-scene actions are blocked only for pending compile, invalid active scene, or duplicate `GamingCouch` state, while no global quick-start scene action is shown in this window.
- Confirmed Task 8 UI/readiness paths read local play JSON without calling JSON write or repair APIs; file writes remain limited to existing quick-start script/prefab/scene setup code.
- Decision: no extra multi-window pending-result patch in this pass. The pending state is shown in-window, continuation results are logged after compilation, and the window refreshes through existing focus/project/hierarchy hooks.
- Validation run: `git diff --check`; conflict-marker search; scoped signature/callsite search for continuation context, pending setup, pending action, quick-start script setup, and setup result usage; scoped action label search; scoped JSON write API absence search in Task 8 touched UI/readiness files; generated asset/test absence check.
- Unity 2022.3 manual editor validation was not run in this package-only shell.

Parent validation, 2026-05-10:

- Validation run: `git diff --check`; conflict-marker search; scoped signature/callsite search for action-aware continuations, pending setup, pending action, setup handlers, and setup result usage; scoped JSON write API absence search in Task 8 touched UI/readiness files; generated asset/test absence check; `git status --short`.
- No tests, generated scenes, prefabs, or package-local quick-start assets were created during command-line validation.

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

Implementation notes, 2026-05-10:

- Added a `Tests/Editor` Unity editor test assembly for readiness detection states, duplicate `GamingCouch` detection, null-only listener/player prefab reference preservation, generated-file no-overwrite behavior, suppression settings, and Build Settings insertion de-duplication.
- Added `GamingCouch.Editor.Tests` internals access plus narrow internal seams for generated asset file creation without overwrite and build-settings insertion without duplicates.
- Tests create temporary additive scenes and dynamic test-owned asset folders; they do not call `EnsureQuickStartScripts()` or `CreateOrOpenQuickStartScene()` and do not create `Assets/GamingCouch/QuickStart` content during command-line validation.
- Validation run: `git diff --check`; static NUnit/Test attribute search; asmdef JSON reference sanity check; scoped search confirmed tests do not invoke quick-start generation APIs or reference the real `Assets/GamingCouch/QuickStart` path; conflict-marker search.
- Unity editor test run skipped: `/Applications/Unity/Hub/Editor/2022.3.19f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath /Users/anttil/dev/dsb/gaming-couch-unity -runTests -testPlatform EditMode -testResults /tmp/gaming-couch-unity-editmode-results.xml -quit` was not run because `command -v Unity` and `command -v UnityHub` returned no executable, `/Applications/Unity/Hub/Editor` contains only `6000.2.7f2`, and this package repo has no `ProjectSettings/` or `Packages/manifest.json` for a safe package-local Unity project import.

Review pass 2, 2026-05-10:

- Patched the edit-mode fixture cleanup so active scene restoration, additive test scene closure, temporary asset deletion, Build Settings restoration, and suppression-setting restoration are each attempted even if another cleanup step fails.
- Guarded temporary asset deletion to the test-owned `Assets/GamingCouchQuickStartEditorTests_*` prefix and added filesystem fallback cleanup for AssetDatabase import edge cases.
- Extended the Build Settings insertion test to confirm existing entries are preserved while repeated insertion of the same quick-start test scene does not duplicate it.
- Static inspection found no Task 9 production regression in `EnsureGeneratedAssetFileWithoutOverwrite` or `AddSceneToBuildSettingsIfMissing`; both remain narrow internal seams for test coverage.
- Validation run: `git diff --check`; asmdef JSON parse; new-file whitespace checks for the test assembly, test file, and editor assembly-info file; scoped static searches for real quick-start generation calls, `Assets/GamingCouch/QuickStart` references, conflict markers, and trailing whitespace.
- Unity editor test run still skipped for the same environment reason: no Unity 2022.3 executable or package-local Unity project files are available in this workspace.

Parent validation, 2026-05-10:

- Patched suppression-setting cleanup to restore the raw `EditorUserSettings` config value so the test fixture does not leave a previously absent setting behind.
- Validation run: `git diff --check`; parsed `package.json`, runtime asmdef, editor asmdef, and test asmdef as JSON; scoped static search confirmed the tests do not call real quick-start generation APIs or reference `Assets/GamingCouch/QuickStart`; conflict-marker search; new-file trailing whitespace check.
- Unity editor test run skipped: no `Unity` or `UnityHub` executable is on `PATH`, `/Applications/Unity/Hub/Editor` only contains `6000.2.7f2`, and this package workspace has no `ProjectSettings/` or `Packages/manifest.json` for a safe package-local Unity 2022.3 edit-mode test run.

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

Review pass 1, 2026-05-10:

- Blocker independently verified with read-only shell checks; Unity was not launched.
- `Unity`, `unity`, `UnityHub`, and `unityhub` were not available on PATH, `/Applications/Unity/Hub/Editor` contained only `6000.2.7f2`, and `/Applications/Unity/Hub/Editor/2022.3.19f1/Unity.app/Contents/MacOS/Unity` was not executable.
- This checkout is a package-only repo: `package.json` declares `com.dsb.gamingcouch` for Unity `2022.3`, while package-local searches found no `ProjectSettings/`, `ProjectVersion.txt`, or `Packages/manifest.json`.
- Nearby Unity `2022.3.19f1` projects that consume `com.dsb.gamingcouch` (`piratewars`, `lesheep`, `rockets`, `game-sumo`, and `temp2/gaming-couch-unity-template`) reference `github.com/deadsetbit/gaming-couch-unity.git` or `git@github.com:deadsetbit/gaming-couch-unity.git` in `Packages/manifest.json`, not this checkout.
- Manual Task 10 validation remains blocked until a Unity 2022.3 environment can open a project whose manifest references this local package checkout.

### Task 11: Add launch-readiness checklist rows

Objective: Extend the unified start-screen checklist so it catches launch-preview readiness issues before users enter Play Mode or make a WebGL build.

Implementation steps:

1. Add an active-scene Build Settings readiness row to the existing checklist.
2. Treat the Build Settings row as passing only when the active scene has a saved scene path and `EditorBuildSettings.scenes[0]` is enabled with that exact path.
3. Report clear non-ready states for unsaved active scenes, missing Build Settings entries, disabled entries, duplicate entries, and active scenes present later than index 0.
4. Add a `Set First Build Scene` row action that prompts to save an unsaved active scene when needed, inserts or moves the active scene to index 0, enables it, removes duplicate entries for the same scene path, and preserves the relative order and enabled state of all other scene entries.
5. Add a Game View 16:9 readiness row to the existing checklist.
6. Treat confirmed 16:9 Game View state as pass, confirmed non-16:9 state as fail, and uninspectable Game View state as a non-blocking warning.
7. Implement Game View inspection and selection behind a narrowly scoped editor service so Unity internal API use is isolated.
8. Add a Game View row action only for selecting an existing 16:9 Game View entry. If no existing 16:9 entry can be found, or if Unity internal API access fails, show manual guidance instead of creating custom Game View sizes.
9. Update the primary `Set up missing pieces` action so it fixes all safe automatable checklist failures: current scene wiring, Build Settings first-scene readiness, and existing-entry 16:9 Game View selection.
10. Keep `gc.dev.json` read-only: invalid or missing local play JSON remains a blocking checklist row and can trigger startup auto-open, but setup actions show DevApp/manual guidance instead of creating or repairing the file.
11. Update startup auto-open to use blocking checklist readiness. Warning-only states, including unknown Game View state, must not trigger auto-open by themselves.

Verification:

- Run `git diff --check`.
- Add editor tests for active scene Build Settings states: saved active scene first and enabled, unsaved active scene, missing entry, disabled entry, entry not first, and duplicate active-scene entries.
- Add editor tests for the `Set First Build Scene` action preserving unrelated scenes while moving/enabling the active scene and removing duplicates for that one path.
- Add tests or static coverage proving active-scene setup actions remain available when only Build Settings or Game View readiness is incomplete.
- Add Game View helper coverage where the logic can be tested without relying on Unity internal editor windows.
- Manually validate the Game View 16:9 row in Unity 2022.3, including pass, fail, unknown fallback, and existing-entry selection behavior.
- Do not mark this task `DONE` until the Game View internal API path has been manually validated or the row is deliberately shipped as inspect-only/manual-guidance behavior.

Implementation notes, 2026-05-11:

- Added active-scene Build Settings readiness to the unified checklist. The row passes only when the active scene has a saved path and is the first enabled `EditorBuildSettings.scenes` entry; unsaved, missing, disabled, later, and duplicate active-scene entries are reported as non-ready states.
- Added a Build Settings setup service and row action that saves an unsaved active scene through Unity's normal save flow, inserts or moves the active scene to index 0, enables it, removes duplicate entries for the same scene path, and preserves unrelated Build Settings entries.
- Added a Game View 16:9 editor service that isolates Unity internal API reflection, reports confirmed 16:9 as ready, confirmed non-16:9 as failed, and uninspectable state as a warning. The only automatic Game View action selects an existing 16:9 entry when reflection finds one; it does not create custom Game View sizes.
- Updated `Set up missing pieces` so it still performs existing active-scene wiring, but no longer no-ops when scene wiring is already complete and Build Settings or safe Game View setup remains. It does not create or repair `gc.dev.json`.
- Updated startup auto-open to use blocking visible checklist readiness, so warning-only states such as unknown Game View aspect do not trigger auto-open by themselves.
- Added focused editor tests for Build Settings readiness states, Build Settings normalization, blocking-checklist warning handling, safe action availability for Build Settings/Game View-only launch readiness gaps, and pure Game View aspect helper logic.
- Validation run: `git diff --check`; `git diff --check --no-index /dev/null Editor/GamingCouchBuildSettingsReadiness.cs`; `git diff --check --no-index /dev/null Editor/GamingCouchGameViewAspect.cs`; conflict-marker search; JSON parse for `package.json`, editor asmdef, test asmdef, and runtime asmdef.
- Unity edit-mode tests and Game View manual validation were not run because no Unity 2022.3 executable was found on PATH or under `/Applications/Unity/Hub/Editor`.

Review pass 2, 2026-05-11:

- Patched primary setup ordering so safe Build Settings and Game View readiness work runs before returning a manual scene-wiring blocker such as duplicate `GamingCouch` objects.
- Added regression coverage for duplicate `GamingCouch` objects still allowing Build Settings normalization before the setup result blocks on manual scene cleanup.
- Validation run: `git diff --check`; conflict-marker search; JSON parse for `package.json` and asmdefs; static search confirmed the Game View service does not create custom sizes.
- Task 11 remains blocked because Unity 2022.3 edit-mode tests and manual Game View validation could not be run in this package-only workspace.
