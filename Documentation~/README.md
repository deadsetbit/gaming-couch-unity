# Gaming Couch Unity Package

This package integrates a Unity game with the Gaming Couch platform.

## Compatibility

This development line targets Unity 6 (`6000.0`) so clean WebGL export setup can remove Unity splash/logo branding.

## Clean WebGL Export

Run `GamingCouch/WebGL Build/Preview clean WebGL export setup` or use the WebGL export row in the GamingCouch start screen to configure a project for clean WebGL builds.

The setup workflow requires Unity 6 (`6000.0`). It installs the package-owned clean template into the project-local `Assets/WebGLTemplates/GamingCouch` folder and selects it as `PROJECT:GamingCouch`. The installer is no-overwrite: rerunning setup creates missing template files but preserves existing project-local template edits. If a destination path is blocked by the wrong asset kind, setup reports a blocker instead of replacing it.

Setup first shows a generated preview of the active build target, template, splash/logo, and release-profile changes it will apply. That preview is the authoritative detailed setting list; skipped setting rows are skipped only for the current apply run and remain reported as readiness drift afterward.

If Unity cannot switch the active build target automatically, setup leaves a warning in the result. Run setup again or switch to WebGL manually before building.

The v1 clean template is a production/upload shell only. It shows loading progress and errors, but it does not provide a standalone browser playtest harness, GamingCouch JavaScript callback shims, local player fixtures, controller simulation, or DevApp communication.

When a WebGL build uses the Gaming Couch template (`PROJECT:GamingCouch`), the package writes `gc.runtime-info.json` to the build output root, next to `index.html`. The sidecar contains `platform`, `packageName`, `packageVersion`, and `gameProtocolVersion`; the Unity package name and version come from the package root `package.json`.

The sidecar enables a Gaming Couch upload/client validation follow-up to inspect Unity build identity before loading the Unity player. This package does not define upload validation policy; that policy belongs in the Gaming Couch main repo.

Any WebGL build also writes `gc.unity-build-info.json` beside `index.html`, even when another WebGL template is selected. This is a separate schema-versioned diagnostic sidecar for Unity editor version, package identity, build target, active WebGL template, selected typed WebGL settings, and selected BuildReport summary values. It is not part of the runtime identity contract and does not extend `gc.runtime-info.json`.

Gaming Couch upload validation may use `gc.unity-build-info.json` to warn or reject builds with the wrong template or WebGL settings, while `gc.runtime-info.json` remains the Gaming Couch template runtime identity contract.

Build diagnostic paths are normalized before JSON serialization: build-output paths are build-output-relative, project paths are project-relative, user-home paths use `${USER_HOME}`, and unknown absolute paths are redacted.

`gameProtocolVersion` identifies the Gaming Couch game integration contract. Sidecar generation, package metadata, build diagnostics, and upload validation rules do not require a protocol bump unless the platform/game contract changes.

## Local Editor Play Settings

Unity editor play settings are file-backed. The `GamingCouch` inspector reads and writes the root `gc.dev.json` file in the Unity project, and uses it as the source of truth for local play entry, seed, and the eight-seat player roster.

Unity requires an existing root `gc.dev.json`. It does not create, bootstrap, or repair `gc.dev.json` or `gc.platform.json`; use the Gaming Couch DevApp local project flow to create and maintain those files before entering Play Mode.

The inspector writes only the canonical `gc.dev.json` fields:

- `devVersion`
- `entryKey`
- `seed`
- `seats`

Unknown top-level fields in `gc.dev.json` are preserved on Unity writes. Local play entry, seed, and seats are not maintained as scene-serialized fallback settings.

Missing or invalid `gc.platform.json` is warning-only. In that state, the inspector shows raw `gc.dev.json` data and can apply structurally valid raw edits. When platform data is valid, it gates Apply and Play:

- `platform.id` must be `unity`.
- The selected `entryKey` must exist.
- Enabled seats must include at least one seat and no more than the selected entry's `maxPlayers`. Production `minPlayers` platform data is still displayed and exported unchanged, but local editor playtests may run with one enabled seat.

Enabled bot seats on an entry with `botSupport: false` are warning-only.

Entering Play Mode or restarting Gaming Couch from Play Mode auto-applies a valid, non-conflicted inspector draft before capturing setup/play options. Invalid or conflicted drafts block Play Mode or restart until resolved. JSON changes during active Play Mode are deferred until a Gaming Couch restart or the next Play Mode entry.

The package depends on `com.unity.nuget.newtonsoft-json` for editor-only JSON parsing and `JObject` writes that preserve unrelated root `gc.dev.json` fields. Runtime and WebGL builds do not use this editor sync path.

## Runtime Contract Notes

Unity game code uses active player indices only. `GCPlayer.Index`, `GCActivePlayerOptions.playerIndex`, input polling by `playerIndex`, runtime messages, screen-space anchors, diagnostics, and terminal placement payloads all refer to the same zero-based run-scoped participant index.

DevApp seats are one-based local development slots for controller assignment and display. Hosted platform player IDs are private adapter/platform bookkeeping. Neither seats nor platform player IDs are public Unity runtime identity, and structured diagnostics must not expose platform player IDs.

Player state APIs distinguish permanent and revokable state. Use `SetEliminatedPermanent`, `SetEliminatedRevokable`, and `SetRevokeEliminated` for elimination, and `SetFinishedPermanent`, `SetFinishedRevokable`, and `SetRevokeFinished` for finish. Revokable state counts while active and can be revoked; permanent state cannot be revoked.

Runtime state and diagnostics flow through `runtime_messages`. Screen-coordinate presentation anchors flow through `screen_space` as `playerOverhead` and `playerPosition` anchors keyed by `playerIndex`. HUD rendering consumes runtime state and screen-space anchors; HUD payloads are not the semantic source of truth.

When `gc.platform.json` is missing or invalid in local development, runtime code receives a read-only fallback platform metadata view with `fallbackActive: true` and exact `notdefined` game/entry values. Unity reports warning diagnostics for the fallback and never writes or repairs `gc.platform.json`.
