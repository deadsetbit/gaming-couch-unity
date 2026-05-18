# GamingCouch Clean WebGL Export Template PRD

Status: Implemented; package-only validation complete, consuming-project Unity validation pending
Owner: Gaming Couch Unity package team

## Summary

The GamingCouch Unity package provides a Unity 6 clean WebGL export setup workflow. It previews and applies a package-owned WebGL template, project-local template selection, release-oriented export settings, and WebGL export readiness through the shared Start Screen readiness system.

The v1 template is a clean production/upload shell only. It provides Unity loading progress and error display without standalone browser playtest behavior, GamingCouch JavaScript callback shims, controller simulation, DevApp communication, PWA assets, service workers, visible chrome, or platform emulation.

## Goals

- Produce clean GamingCouch WebGL export output instead of Unity's default WebGL page.
- Keep template installation no-overwrite and safe for user-edited project files.
- Make WebGL export readiness visible from the Start Screen without mixing it into scene wiring setup.
- Apply previewed release defaults that are suitable for upload-oriented builds.
- Require Unity 6 so splash/logo expectations match the supported editor line.
- Keep setup explicit while reducing manual build prep: the clean WebGL export setup may switch the active build target to WebGL, and readiness still warns if Unity leaves another target active.

## Requirements

- Package metadata requires Unity `6000.0`.
- Package version remains `0.1.0-alpha.2`.
- The setup workflow is available from `GamingCouch/WebGL Build/Preview clean WebGL export setup`.
- The setup workflow shows a generated preview before mutating project or editor settings.
- The package owns the source template and copies it into the consuming project at `Assets/WebGLTemplates/GamingCouch`.
- Unity selects the installed custom template as `PROJECT:GamingCouch`.
- Template installation creates missing folders and files, reuses existing files, and never overwrites existing project-local template files.
- Wrong-kind collisions block setup with a clear reason, including file collisions where a folder is required and folder collisions where a template file is required.
- The setup action configures only WebGL export settings. It must not run active-scene setup.
- Setup switches the active build target to WebGL when Unity accepts the switch. A non-WebGL active build target remains warning-only in readiness so users still get clear guidance if the switch does not complete.
- Splash/logo and release-profile settings come from shared internal setting specs used by preview, apply, and readiness. Skipped setting rows are skipped only for the current apply run and remain readiness drift.

## Release Defaults

Clean WebGL export setup applies generated release-oriented defaults. The preview is the authoritative detailed list of setting labels, current values, and target values; this PRD must not duplicate that list manually.

## Template Requirements

- The template uses Unity WebGL build macros for loader, data, framework, code, memory, symbols, company name, product name, and product version where applicable.
- The template presents only a clean fullscreen canvas, loading progress, and error display.
- The template has no standalone browser playtest harness.
- The template defines no GamingCouch JavaScript callback shims.
- The template includes no controller simulation.
- The template includes no DevApp communication.
- The template includes no PWA behavior, service worker, manifest, visible toolbar, footer, reload button, fullscreen button, unload button, project title chrome, GamingCouch logo, or Unity logo.

## Start Screen Integration

- The Start Screen includes a dedicated checklist row labeled `WebGL export settings configured`.
- The row's tooltip explains that readiness checks the WebGL target, template, and generated release settings preview.
- The row setup action opens the shared clean WebGL export preview and runs only clean WebGL export setup when applied.
- The row does not run active-scene setup, create scene objects, change listener or prefab references, update Build Settings scene order, or change Game View aspect.
- WebGL export readiness participates in the shared readiness model used by the Start Screen and the GamingCouch inspector.
- Actionable WebGL setup is not a blocker for active-scene wiring readiness.

## Non-goals

- Making exported WebGL builds playable outside the GamingCouch platform.
- Adding local browser playtest controls, fake seats, fake controller input, or DevApp communication.
- Defining browser-side GamingCouch JavaScript callback shims in the clean template.
- Adding a PWA template, service worker, install prompt, app manifest, or offline behavior.
- Switching the active Unity build target outside the explicit clean WebGL export setup action.
- Overwriting user-edited project-local WebGL template files.
- Editing generated `.meta` files as part of this PRD.
- Editing the Gaming Couch main repo, GC SDK, GC Client, or GC DevApp.

## Validation

Available validation in this package-only checkout:

- `git diff --check`.
- Parse `package.json` as JSON and confirm `unity` is `6000.0`, `version` is `0.1.0-alpha.2`, and stale Unity release-floor metadata is absent.
- Confirm the Unity binary version declared by package metadata is Unity 6.
- Static inspection of package-owned WebGL template source for the required clean-shell constraints.

Pending validation requires a consuming Unity project with package import support:

- Unity edit-mode tests for install, no-overwrite reuse, wrong-kind collision blockers, selected template readiness, release setting drift, splash/logo accepted values, and non-WebGL warning/action behavior.
- Manual Unity WebGL smoke validation: run setup, inspect selected template, build WebGL output, confirm clean visible shell, confirm loading progress and error display paths, and confirm build instantiation.
