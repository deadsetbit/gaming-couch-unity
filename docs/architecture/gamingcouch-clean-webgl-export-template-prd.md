# GamingCouch Clean WebGL Export Template PRD

Status: Implemented; package-only validation complete, consuming-project Unity validation pending
Owner: Gaming Couch Unity package team

## Summary

The GamingCouch Unity package provides a Unity 6 clean WebGL export setup workflow. It installs a package-owned WebGL template into the consuming Unity project, selects that project-local template for WebGL builds, applies release-oriented export settings, and reports WebGL export readiness through the shared Start Screen readiness system.

The v1 template is a clean production/upload shell only. It provides Unity loading progress and error display without standalone browser playtest behavior, GamingCouch JavaScript callback shims, controller simulation, DevApp communication, PWA assets, service workers, visible chrome, or platform emulation.

## Goals

- Produce clean GamingCouch WebGL export output instead of Unity's default WebGL page.
- Keep template installation no-overwrite and safe for user-edited project files.
- Make WebGL export readiness visible from the Start Screen without mixing it into scene wiring setup.
- Apply release defaults that are suitable for upload-oriented builds.
- Require Unity 6 so splash/logo expectations match the supported editor line.
- Keep setup explicit and non-disruptive: warn on non-WebGL active build target, but never switch targets automatically.

## Requirements

- Package metadata requires Unity `6000.0`.
- Package version remains `0.1.0-alpha.2`.
- The setup workflow is available from `GamingCouch/WebGL Build/Setup clean WebGL export`.
- The package owns the source template and copies it into the consuming project at `Assets/WebGLTemplates/GamingCouch`.
- Unity selects the installed custom template as `PROJECT:GamingCouch`.
- Template installation creates missing folders and files, reuses existing files, and never overwrites existing project-local template files.
- Wrong-kind collisions block setup with a clear reason, including file collisions where a folder is required and folder collisions where a template file is required.
- The setup action configures only WebGL export settings. It must not run active-scene setup.
- A non-WebGL active build target is warning-only. Setup must not switch the active build target.
- Splash screen and Unity logo settings are disabled when Unity accepts the settings, and readiness is based on inspected accepted values.

## Release Defaults

Clean WebGL export setup applies these release-oriented defaults:

- WebGL compression disabled.
- WebGL data caching enabled.
- Development build disabled.
- Debug symbols disabled.
- Managed stripping level set to high.
- Unused mesh component stripping enabled.
- IL2CPP code generation optimized for size.
- WebAssembly 2023 enabled where available.
- Disk-size LTO enabled.
- Splash screen and Unity logo disabled when Unity accepts those settings.

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
- The row's tooltip explains the exact WebGL template and release settings expected by readiness.
- The row setup action runs only clean WebGL export setup.
- The row does not run active-scene setup, create scene objects, change listener or prefab references, update Build Settings scene order, or change Game View aspect.
- WebGL export readiness participates in the shared readiness model used by the Start Screen and the GamingCouch inspector.
- Actionable WebGL setup is not a blocker for active-scene wiring readiness.

## Non-goals

- Making exported WebGL builds playable outside the GamingCouch platform.
- Adding local browser playtest controls, fake seats, fake controller input, or DevApp communication.
- Defining browser-side GamingCouch JavaScript callback shims in the clean template.
- Adding a PWA template, service worker, install prompt, app manifest, or offline behavior.
- Automatically switching the active Unity build target to WebGL.
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

- Unity edit-mode tests for install, no-overwrite reuse, wrong-kind collision blockers, selected template readiness, release setting drift, splash/logo accepted values, and non-WebGL warning behavior.
- Manual Unity WebGL smoke validation: run setup, inspect selected template, build WebGL output, confirm clean visible shell, confirm loading progress and error display paths, and confirm build instantiation.
