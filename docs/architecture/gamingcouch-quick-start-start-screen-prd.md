# GamingCouch Quick Start Start Screen PRD

Status: In progress
Owner: Gaming Couch Unity package team

## Summary

The GamingCouch Unity package provides an editor Start Screen that reports active-scene readiness, creates or wires safe quick-start pieces, and exposes launch-readiness checks for Build Settings, Game View aspect, WebGL export configuration, and local play JSON validity.

The same readiness model is shared with the `GamingCouch` inspector so users can see a concise Start Screen readiness summary from the component inspector without duplicating readiness logic.

## Goals

- Give new package users an obvious path from an incomplete Unity scene to a minimal local GamingCouch play loop.
- Make all current setup requirements visible in one checklist.
- Keep setup safe by preserving existing non-null or occupied serialized references and never overwriting generated user-editable assets.
- Keep manual guidance clear for states that cannot be fixed safely.
- Keep the Start Screen available from the GamingCouch menu and the `GamingCouch` inspector.
- Let users suppress automatic startup opening per project while preserving manual access.

## Readiness Statuses

The shared readiness API exposes these statuses:

- `Ready`: the item is complete.
- `Warning`: the item may need attention but is not a blocker.
- `Actionable`: the item can be fixed by a safe setup action.
- `Blocked`: the item requires manual user action or an unavailable environment capability.
- `PendingCompilation`: generated scripts or setup continuation depend on Unity compilation completing.

Actionable setup rows are not blockers. Startup auto-open and readiness summaries should distinguish actionable work from true blocking states.

## Checklist Rows

The visible Start Screen checklist contains these rows:

- `GamingCouch game object in scene`
- `Game script is ready`
- `Player prefab is assigned`
- `Active scene is first Build Settings scene`
- `Game View uses 16:9 preview`
- `WebGL export settings configured`
- `Local play JSON is valid`

The `GamingCouch game object in scene` row presents missing, created, ready, and multiple-instance states cleanly. It should not nag about "exactly one" unless more than one `GamingCouch` object exists in the active scene.

The `Game script is ready` row preserves the existing listener-reference readiness semantics while presenting the requirement in user-facing Game script language.

Checklist row messages are visually indented and padded under their rows so they do not align with the main checklist item text.

Each checklist row has a far-right icon-only help button. Hovering the button shows tooltip text. Clicking the button toggles an indented help box that explains the checklist item. Row setup or focus action buttons remain separate from the help button.

## Start Screen UX

- The EditorWindow tab is titled `GamingCouch Start Screen`.
- The content starts with active-scene readiness, not a marketing page.
- If no usable active scene is loaded, the window shows a standalone help box before the checklist.
- The scene summary shows name and path only.
- The primary `Set up missing pieces` action appears only while safe automatable setup work exists.
- The global Actions section is hidden when no global actions remain.
- Individual row actions remain available for relevant missing or focusable rows.
- The `Game script is ready` row action is labeled `Create & Wire Game` when setup is available.
- Normal successful, focus, reuse, and no-op action confirmations are silent; the checklist reflects the resulting state.
- Warning and error outcomes use help boxes or dialogs with concise current-state guidance.
- The auto-open suppression control appears at the bottom with the label `Never open this again on startup`.

## Inspector UX

- The `GamingCouch` inspector shows `Open Start Screen` as the first visible item.
- The inspector also shows a concise Start Screen readiness summary based on the shared readiness status.
- The inspector must not duplicate Start Screen readiness rules.

## Setup Behavior

- The Start Screen can create or reuse a `GamingCouch` object when the active scene has none.
- Multiple active-scene `GamingCouch` objects are a manual cleanup blocker and are never deleted automatically.
- Game script listener and player prefab assignments are written only when the serialized fields are genuinely empty.
- Occupied serialized listener or player prefab references are preserved when they still resolve to objects.
- Missing serialized listener references caused by deleted scene objects are reported as missing GameObject references. The `Create & Wire Game` action may replace that missing listener reference with a newly created compatible `Game` object.
- Other unresolved listener references must explain likely causes instead of calling the state "broken"; the user may need to clear or replace the listener reference manually.
- Missing serialized player prefab references are preserved and require manual cleanup or replacement.
- Generated quick-start assets live under `Assets/GamingCouch/QuickStart`.
- Generated scripts, prefabs, and scenes are editable project content and are never overwritten on rerun.
- Script generation uses a staged flow because Unity must compile generated scripts before components can be added.
- The generated player prefab is based on `GCPlayer` and includes a simple visible placeholder that applies GamingCouch player colors.
- The generated listener demonstrates `GamingCouchSetup`, `GamingCouchPlay`, `SetupGameVersus`, `SetupDone`, `SetupPlayers`, randomized final scores, and `GameOver()`.
- The generated quick-start scene includes a wired `GamingCouch` object, listener, player prefab, camera, and light.
- Creating a quick-start scene opens the scene and adds it to Build Settings.
- The package detects missing or invalid local play JSON and reports it clearly, but does not create, bootstrap, migrate, or repair `gc.dev.json` or `gc.metadata.json`.

## Launch Readiness

- The active scene passes the Build Settings row only when it has a saved scene path and is the first enabled scene in `EditorBuildSettings.scenes`.
- Missing, disabled, later, duplicate, and unsaved active-scene states are reported as clear checklist failures or blockers.
- Safe setup may prompt to save an unsaved active scene when needed for Build Settings setup.
- Safe setup may make the active scene first and enabled in Build Settings while preserving unrelated scenes.
- The Game View row passes when a 16:9 preview is confirmed.
- A confirmed non-16:9 Game View is actionable when an existing 16:9 entry can be selected safely.
- An uninspectable Game View state is a non-blocking warning and must not trigger startup auto-open by itself.
- Setup must not create custom Game View sizes.
- The WebGL row is labeled `WebGL export settings configured`.
- The WebGL row tooltip explains the expected `PROJECT:GamingCouch` template and clean release defaults.
- The WebGL row setup action configures only WebGL export settings and does not run active-scene setup.

## Auto-open Policy

- The Start Screen may auto-open once on Unity editor startup when the active scene has visible blocking or actionable setup work.
- It must not repeatedly auto-open on later scene changes, every domain reload, while compiling, while updating, while entering Play Mode, or for prefab/preview contexts.
- Suppressing auto-open is stored per project.
- Manual entry points remain available after suppression.

## Non-goals

- Creating, bootstrapping, repairing, or migrating local play JSON files.
- Adding a full sample game or controller-input mini-game.
- Changing public runtime payload shapes or platform message contracts.
- Automatically deleting duplicate `GamingCouch` objects.
- Automatically patching existing user-authored `Game.cs` or `Player.cs` files.
- Creating custom Game View sizes.
- Changing runtime resolution, WebGL template layout, or build output sizing as part of the 16:9 checklist item.
- Publishing backlog tickets to GitHub or another external service.
- Moving generated content into package samples.

## Tasks

| ID | Task | Status | Done when | Dependencies | Notes |
| --- | --- | --- | --- | --- | --- |
| Task 1 | Rename listener readiness to Game script readiness end to end. | Completed | The Start Screen checklist, shared readiness summary, inspector summary, tooltips, help text, and validation naming all use `Game script is ready` while preserving the existing readiness model. | None | Keep actionable setup rows distinct from true blockers. |
| Task 2 | Validate compatible Game script receivers. | Completed | Assigned listener objects pass readiness when they can receive `GamingCouchSetup(GCSetupOptions)` and `GamingCouchPlay(GCPlayOptions)`, including compatible custom listener objects not named `Game`; unresolved references fail with manual guidance. | Task 1 | Do not require generated asset names for manually assigned compatible listeners. |
| Task 3 | Implement safe `Create & Wire Game` setup. | Completed | The row action creates or reuses `Assets/Game.cs`, `Assets/Player.cs`, a scene object named `Game`, and the `Game` component through the staged compile flow, then assigns the listener only when the serialized field is empty. | Task 2 | Do not overwrite generated or user-authored assets and do not create or assign the player prefab from this action. |
| Task 4 | Update generated starter Game and Player assets. | Completed | Generated `Game.cs` demonstrates the required GamingCouch setup and play callbacks, `SetupGameVersus`, `SetupDone`, `SetupPlayers<Player>`, randomized final scores, and `GameOver()`; generated `Player.cs` extends `GCPlayer` and supports the visible color placeholder prefab flow. | Task 3 | Existing `GCQuickStartGame` and `GCQuickStartPlayer` assets remain unmigrated unless already assigned and compatible. |
| Task 5 | Add focused validation for Game script readiness behavior. | Completed | Package-local editor tests and static inspection cover receiver compatibility, missing and unresolved listener guidance, staged compile classification, `Game.cs` and `Player.cs` conflict handling, `Create & Wire Game` result handling, and generated quick-start source structure; consuming-project/manual Unity validation remains documented as pending. | Task 1, Task 2, Task 3, Task 4 | Package-local coverage was added where feasible; consuming-project Play Mode validation remains pending/manual. |

## Validation

Available validation in this package-only checkout:

- `git diff --check`.
- Static inspection of Start Screen readiness rows, `Game script is ready` naming, help-button behavior, row message indentation, action separation, and inspector entry ordering.
- Static inspection of package JSON parsing and Unity compatibility metadata where relevant to WebGL readiness.
- Package-local editor-test coverage in `Tests/Editor/GamingCouchQuickStartEditorTests.cs` for compatible custom receivers, inherited receiver methods, incompatible receiver signatures, missing listener guidance and replacement, unresolved listener guidance, root `Assets/Game.cs` and `Assets/Player.cs` path collision blocking, generated `Game.cs`/`Player.cs` source structure, quick-start player prefab color wiring, `Create & Wire Game` action labels, pending-compilation warning visibility, and silent ready success classification.
- Package-local static source inspection confirms generated quick-start play-loop structure includes `GamingCouchSetup(GCSetupOptions)`, `GamingCouchPlay(GCPlayOptions)`, `SetupGameVersus`, `SetupDone`, `SetupPlayers<Player>`, color application, a coroutine round, randomized final scores, and `GameOver()`.

Pending validation requires a consuming Unity project with package import support:

- Consuming-project Unity edit-mode confirmation that package-local editor tests pass under a real package import, including generated script compile/resume behavior after Unity domain reload.
- Manual Unity validation for staged compile continuation from newly created `Assets/Game.cs` and `Assets/Player.cs`, `Create & Wire Game` end-to-end success feedback in the Start Screen UI, generated quick-start Play Mode loop with valid local play JSON, missing or invalid local play JSON messaging, Game View 16:9 behavior, and clean WebGL setup interaction from the Start Screen.
