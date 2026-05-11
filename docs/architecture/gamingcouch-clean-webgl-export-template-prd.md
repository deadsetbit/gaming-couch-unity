# GamingCouch Clean WebGL Export Template PRD

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

GamingCouch Unity package users who build WebGL projects currently get Unity's default WebGL export page and default loader chrome. That output includes Unity-oriented page branding, controls, footer elements, and template assets that are not appropriate for a clean GamingCouch web export. Users also have to remember several project-level WebGL settings manually, including compression and release-build settings.

This makes WebGL export feel disconnected from the GamingCouch package. A user can wire a scene for GamingCouch, but still produce a default Unity WebGL shell that is not suitable as a clean upload/export artifact and does not match the direction of future GamingCouch platform embedding.

## Solution

Add a Unity 6-only clean WebGL export setup workflow to the package. The workflow installs a neutral WebGL template into the consuming Unity project, selects it for WebGL builds, applies release-oriented WebGL defaults, disables Unity splash/logo settings where Unity 6 permits it, and reports readiness from the existing GamingCouch start screen.

The v1 template is only a clean production/upload shell. It should not emulate the GamingCouch platform, provide browser playtest controls, define GamingCouch JavaScript callback shims, or make exported builds playable outside the platform. Future playtest support can be added as a separate template or feature once the browser-side platform contract is intentionally designed.

## User Stories

1. As a Unity package user building for GamingCouch, I want a clean WebGL export template, so that my build does not use Unity's default WebGL page.
2. As a Unity package user, I want the clean template to be installable from the GamingCouch editor UI, so that I do not manually copy template folders.
3. As a Unity package user, I want the install action to preserve existing template files, so that local edits are not overwritten by rerunning setup.
4. As a Unity package user, I want clear blocking messages for file or folder collisions, so that I know when manual cleanup is required.
5. As a Unity package user, I want the GamingCouch template selected automatically after setup, so that the next WebGL build uses the intended page.
6. As a Unity package user, I want the start screen to show whether WebGL export is configured, so that scene setup and export setup are visible in one place.
7. As a Unity package user, I want WebGL export setup to have its own checklist row and action, so that scene wiring stays separate from project build settings.
8. As a Unity package user, I want the setup action to apply release export defaults, so that upload-oriented builds do not accidentally include development/debug settings.
9. As a Unity package user, I want compression disabled by default, so that GamingCouch hosting can serve compressed assets without Unity's fallback decompressor requirements.
10. As a Unity package user, I want the setup action to disable the Unity splash screen and Unity logo on Unity 6, so that clean builds avoid Unity branding when the editor permits it.
11. As a Unity package user, I want the template itself to be visually neutral, so that it can later be embedded by the GamingCouch platform without conflicting chrome.
12. As a Unity package user, I want the template to show only loading progress and errors before the game starts, so that there is enough feedback without visible controls or branding.
13. As a Unity package user, I want no fullscreen, reload, unload, footer, project title, GamingCouch logo, or Unity logo controls in the v1 template, so that the export surface stays minimal.
14. As a Unity package user, I want setup to warn when the active build target is not WebGL without switching it automatically, so that I avoid an unexpected slow platform switch.
15. As a Unity package user, I want a GamingCouch menu item for WebGL export setup, so that I can run it without opening the start screen.
16. As a package maintainer, I want the existing release/dev WebGL build settings menu items to share the same setup helper, so that WebGL defaults do not drift.
17. As a package maintainer, I want the package compatibility metadata to require Unity 6, so that the no-Unity-logo expectation matches the supported editor version.
18. As a package maintainer, I want the package version to stay unchanged during current development, so that this PRD does not create release metadata work before implementation is accepted.
19. As a package maintainer, I want tests for idempotent template installation and readiness reporting, so that rerun safety is verified before release.
20. As a package maintainer, I want a manual Unity 6 WebGL smoke test, so that the generated build output is checked in the editor version this workflow targets.

## Implementation Decisions

- Require Unity 6 for the package by updating package compatibility metadata to Unity `6000.0`; keep the current package version during development.
- Build a package-owned neutral WebGL template source and copy it into the consuming project's project-local WebGL template location because Unity discovers custom WebGL templates from project assets.
- Use `GamingCouch` as the project-local template folder name and select it through the WebGL template setting with the `PROJECT:GamingCouch` template identifier.
- Keep template installation idempotent and no-overwrite. Existing files are reused, missing files are created, and path collisions with the wrong asset kind block setup with a clear reason.
- The template is a minimal HTML shell with a fullscreen canvas, loading progress, error display, Unity loader invocation, and standard build file macro usage. It has no visible branding, toolbar, footer, fullscreen button, reload button, quit button, project-title chrome, PWA manifest, or service worker.
- Add an editor-only WebGL export setup service with a small result/readiness model. The service owns template installation, template selection, release WebGL settings, splash/logo settings, and active-build-target warning state.
- Add one explicit start-screen checklist row for WebGL export readiness. Its setup action runs only the WebGL export setup service and must not run scene quick-start setup.
- Add a GamingCouch menu item for setting up clean WebGL export. It should report success, warnings, and blockers through the same result model as the start screen action.
- Apply release export defaults from the setup action: selected template, disabled WebGL compression, enabled data caching, non-development build, debug symbols off, high managed stripping, unused mesh component stripping, IL2CPP optimize size, WebAssembly 2023 where available, and disk-size LTO.
- Disable Unity splash screen and Unity logo settings as part of setup. After writing the settings, readiness should inspect the accepted values rather than assuming Unity applied them.
- Warn, but do not fail, when the active build target is not WebGL. The setup action must not switch active build target automatically.
- Keep v1 separate from platform playtesting. Do not add browser-side GamingCouch callback shims, local player fixtures, DevApp WebSocket behavior, controller simulation, or game testing controls.
- Reuse or refactor the existing WebGL build optimizer so the release/dev menu items and new setup service share consistent settings behavior.
- Document the Unity 6 requirement, clean template setup flow, no-overwrite behavior, release defaults, no automatic build-target switch, and v1 playtesting exclusion.

## Testing Decisions

- Prefer editor tests around observable setup behavior: files created or reused, settings selected, readiness states, warnings, and blockers.
- Test the WebGL export setup service directly rather than testing IMGUI drawing details.
- Test template installation into a temporary project asset folder, including fresh install, rerun reuse, existing file preservation, folder/file collisions, and missing parent creation.
- Test readiness for selected template, missing template folder, wrong selected template, release setting drift, splash/logo state, and non-WebGL active build target warning.
- Test that the start-screen checklist exposes a dedicated WebGL export action and does not make the global scene setup action responsible for WebGL export settings.
- Test that existing release and dev WebGL menu actions still apply their intended profiles after shared-helper refactoring.
- Parse package metadata as JSON and confirm Unity compatibility is Unity 6 while the package version remains unchanged.
- Run `git diff --check` after each implementation task.
- Run Unity 6 edit-mode tests if a suitable consuming project is available; otherwise record the exact skipped environment gap in this PRD.
- Run a manual Unity 6 WebGL smoke test before marking the feature complete: setup action, selected template, generated build output inspection, no visible template branding/controls, loading progress, error display path, and successful build instantiation.

## Out of Scope

- Making exported WebGL builds playable outside the GamingCouch platform.
- Adding local browser playtest controls, fake seats, fake controller input, or DevApp communication.
- Defining browser-side GamingCouch JavaScript callback shims in the clean template.
- Adding a PWA template, service worker, install prompt, app manifest, or offline caching behavior.
- Automatically switching the active Unity build target to WebGL.
- Overwriting user-edited project-local WebGL template files.
- Publishing tasks to GitHub issues, Linear, or another external backlog.
- Editing the Gaming Couch main repo or DevApp.

## Further Notes

- Unity 2022.3 still documents Personal-plan limitations for disabling Unity splash/logo settings. Unity Support documents Unity 6 as the path where Personal can remove or customize the Made with Unity splash screen. Requiring Unity 6 keeps the no-logo goal aligned with the supported editor version.
- Unity documents custom WebGL templates as project-local assets, so a package installer/copy flow is more reliable than expecting Unity to discover templates directly inside a UPM package.
- The exact `PROJECT:GamingCouch` template identifier should be manually confirmed in Unity 6 because Unity's scripting API describes the setting as a template path but does not fully document the custom-template identifier format.
- Existing worktree changes and untracked metadata files should not be reverted or cleaned up as part of this feature.

## Implementation Task Tracker

Overall status: IN PROGRESS

Current task: None. Task 1 complete; Task 2 pending.

Next action: Add clean WebGL template source.

| Done | Status | Task | Notes |
| --- | --- | --- | --- |
| [x] | DONE | 1. Add Unity 6 package compatibility metadata | `package.json` now requires Unity `6000.0`, removes the old `unityRelease` floor, keeps version `0.1.0-alpha.2`, and adds concise public docs/changelog notes for the Unity 6 clean WebGL export development line. |
| [ ] | TODO | 2. Add clean WebGL template source | Add the neutral template source used by the installer, with no branding, controls, PWA assets, or platform playtest shims. |
| [ ] | TODO | 3. Build WebGL export setup service | Add editor-only install/readiness/result logic for template files, selected template, release defaults, splash/logo settings, and active-build-target warning. |
| [ ] | TODO | 4. Refactor WebGL build settings helpers | Make existing release/dev menu items delegate to shared profile helpers without changing the dev profile's intent. |
| [ ] | TODO | 5. Add menu entry for clean WebGL export setup | Add a GamingCouch menu item that runs the setup service and reports blockers or warnings clearly. |
| [ ] | TODO | 6. Add start-screen WebGL export checklist row | Surface WebGL export readiness and setup as a dedicated checklist row/action, separate from scene setup. |
| [ ] | TODO | 7. Add editor tests for setup and readiness | Cover install/reuse/collision behavior, readiness states, release setting drift, splash/logo values, and checklist action routing. |
| [ ] | TODO | 8. Update docs and progress notes | Document the workflow, Unity 6 requirement, no-overwrite behavior, release defaults, warning-only build-target policy, and v1 playtesting exclusion. |
| [ ] | TODO | 9. Run validation and manual Unity 6 smoke test | Run static validation and Unity tests if available; record any skipped environment gap and manual WebGL build smoke-test results. |

## Task Details

### Task 1: Add Unity 6 Package Compatibility Metadata

Objective: Make Unity 6 the supported package floor for this development line while keeping the package version unchanged.

Implementation steps:

1. Update package compatibility metadata to require Unity `6000.0`.
2. Remove the old Unity 2022.3 release floor.
3. Keep the package version unchanged.
4. Add documentation notes that this development line targets Unity 6 for clean WebGL export and splash/logo removal.

Verification:

- Parse package metadata as JSON.
- Confirm Unity compatibility is `6000.0`.
- Confirm package version remains `0.1.0-alpha.2`.
- Run `git diff --check`.

Task 1 notes:

- Changed paths: `package.json`, `CHANGELOG.md`, `README.md`, `Documentation~/README.md`, `docs/architecture/gamingcouch-clean-webgl-export-template-prd.md`.
- Verification: parsed `package.json` as JSON and confirmed `unity` is `6000.0`, `unityRelease` is absent, and `version` remains `0.1.0-alpha.2`. `git diff --check` passed.

### Task 2: Add Clean WebGL Template Source

Objective: Add the package-owned source template that the setup service can install into consuming projects.

Implementation steps:

1. Add a minimal WebGL template source with one HTML entry point and any required small support assets.
2. Use Unity WebGL macro variables for build file names, product/company/version config, and conditional symbols.
3. Include only neutral loading progress and error UI.
4. Exclude all visible Unity, GamingCouch, footer, toolbar, fullscreen, reload, quit, PWA, and playtest UI.
5. Do not define GamingCouch JavaScript callback shims.

Verification:

- Inspect template source for Unity logo asset references and visible branding strings.
- Inspect template source for no browser playtest or platform shim behavior.
- Run `git diff --check`.

### Task 3: Build WebGL Export Setup Service

Objective: Add a testable editor-only service that can install and inspect the clean WebGL export setup.

Implementation steps:

1. Add a readiness model for installed template files, selected WebGL template, release settings, splash/logo settings, and active build target.
2. Add an install action that creates missing project-local template folders and files without overwriting existing files.
3. Block setup on non-folder folder-path collisions and wrong-kind asset collisions.
4. Select the project-local template after successful install.
5. Apply the release export defaults and splash/logo settings.
6. Inspect actual accepted settings after applying them.
7. Treat non-WebGL active build target as a warning only.

Verification:

- Confirm no setup path switches active build target.
- Confirm rerun behavior is no-overwrite.
- Run `git diff --check`.

### Task 4: Refactor WebGL Build Settings Helpers

Objective: Keep existing WebGL build settings menu behavior while sharing settings code with the clean export setup.

Implementation steps:

1. Extract release profile application into shared editor-only helper logic.
2. Extract dev profile application into shared editor-only helper logic.
3. Make the existing release and dev menu items delegate to those helpers.
4. Ensure the clean export setup uses the release profile plus template/splash/logo setup.

Verification:

- Inspect release profile for upload-oriented settings.
- Inspect dev profile for fast/debug-oriented settings.
- Run `git diff --check`.

### Task 5: Add Menu Entry For Clean WebGL Export Setup

Objective: Provide a direct menu entry for users who do not open the start screen.

Implementation steps:

1. Add a GamingCouch WebGL build menu item for clean export setup.
2. Run the setup service from that menu item.
3. Show clear editor feedback for blockers and warnings.
4. Avoid success spam when there is nothing actionable beyond settings already being ready.

Verification:

- Confirm the menu item is editor-only.
- Confirm blockers are visible to the user.
- Run `git diff --check`.

### Task 6: Add Start-Screen WebGL Export Checklist Row

Objective: Integrate WebGL export readiness into the existing start screen without mixing it into scene wiring setup.

Implementation steps:

1. Add a dedicated WebGL export readiness row to the start screen checklist.
2. Add a setup button for the row when setup is incomplete.
3. Keep the global scene setup action focused on scene setup only.
4. Show warning state, not failure, when the active build target is not WebGL.
5. Refresh readiness after running the WebGL export setup action.

Verification:

- Confirm the row appears independently of scene wiring readiness.
- Confirm scene setup does not install/select templates.
- Confirm WebGL export setup does not create or modify scene objects.
- Run `git diff --check`.

### Task 7: Add Editor Tests For Setup And Readiness

Objective: Verify the setup service and start-screen integration through editor tests.

Implementation steps:

1. Add tests for fresh template installation.
2. Add tests for rerun reuse and existing file preservation.
3. Add tests for file/folder collision blockers.
4. Add tests for selected template readiness and wrong-template failure.
5. Add tests for release setting drift.
6. Add tests for splash/logo accepted value inspection.
7. Add tests for non-WebGL active build target warning.
8. Add tests that the WebGL checklist row action is dedicated and does not route through active-scene setup.

Verification:

- Run available editor tests if a Unity project/test runner is available.
- If Unity tests cannot run, record the exact environment gap under Task 9.
- Run `git diff --check`.

### Task 8: Update Docs And Progress Notes

Objective: Make the workflow understandable from public package docs and keep this PRD as the local task tracker.

Implementation steps:

1. Document the clean WebGL export setup workflow.
2. Document the Unity 6 requirement.
3. Document that setup preserves existing project-local template edits.
4. Document release defaults, disabled compression, splash/logo setup, and warning-only active build target policy.
5. Document that v1 is not a standalone browser playtest harness.
6. Update this task tracker as implementation progresses.

Verification:

- Confirm public docs do not claim browser playtest support.
- Confirm docs do not claim automatic build-target switching.
- Run `git diff --check`.

### Task 9: Run Validation And Manual Unity 6 Smoke Test

Objective: Complete static and Unity 6 validation before marking the feature done.

Implementation steps:

1. Run `git diff --check`.
2. Parse package metadata as JSON.
3. Run Unity 6 edit-mode tests if a consuming project is available.
4. In a Unity 6 consuming project, run clean WebGL export setup.
5. Confirm the WebGL template selection is `PROJECT:GamingCouch`.
6. Build WebGL and inspect generated output for no visible template branding, controls, footer, PWA assets, or playtest shims.
7. Serve/open the build and confirm loading progress, error path, and successful instantiation.
8. Record validation results and any skipped environment gaps in this PRD.

Verification:

- Mark the task `DONE` only after static validation is complete and either manual Unity 6 validation is complete or the exact blocker is recorded.
