# Changelog

## [Unreleased]

## [0.1.0-alpha.4] - 2026-07-09

### Changed

- Added WebGL runtime-info drift detection: exports now write canonical root `gc.runtime-info.json`, bake the same payload into an early `GamingCouchRegisterRuntimeInfo` callback before splash screen, keep `GamingCouchInstanceStarted()` payload-free, let the hosted SDK fail startup on sidecar/runtime drift, and make upload validation require and validate `gc.runtime-info.json`.
- Updated Unity build diagnostics: `gc.unity-build-info.json` now has runtime identity, build environment, host OS diagnostics, and build result sections; upload processing preserves it when present, but upload validation does not require or validate it.
- Documented the Unity package as strict-current at its runtime boundary: hosted play payloads must arrive with current active-player identity, runtime input must use `playerIndex`, and legacy `players[]`/`playerId` adaptation belongs in the Gaming Couch client/SDK before Unity is invoked.
- Clarified that source-seat mapping remains supported for local editor play because seats are local setup, not hosted platform identity.
- Documented retained obsolete Unity APIs as compile-time errors with migration guidance toward `GCPlayer.Index`, `playerIndex`, current player-state APIs, and current HUD/runtime-state paths.
- Renamed the clean WebGL export workflow to Gaming Couch web export settings across editor setup code, menus, tests, and documentation.

### Removed

- Removed the right stick input API: `GCControllerInputs.rightX`/`rightY` and the underlying `a2`/`a3` axis fields no longer exist.
- Removed unused controller input fields `b3` and DPad `b12`–`b15`; `leftX`/`leftY` now read the left stick axes only, without DPad fallback.

### Release Notes

- Protocol-version decision: the current recommendation is no `gameProtocolVersion` bump if client/SDK adapters already translate legacy payloads and send current payloads to this package. Any bump remains a release-owner/user decision based on rollout risk and adapter compatibility.

## [0.1.0-alpha.3] - 2026-05-26

### Added

- Added the Gaming Couch Start Screen with quick-start setup actions that scaffold a working example scene from scratch: the `GamingCouch` object, an example game listener, an example player prefab, generated example scripts, and the scene wiring between them.
- Added Start Screen project-readiness reporting so setup surfaces which pieces are still missing.

### Fixed

- Fixed editor keyboard input priority during local editor play.
- Fixed DevApp runtime game-over and reconnect handling.
- Fixed a WebGL export preview compile error.

## [0.1.0-alpha.2] - 2026-05-09

### Added

- Added root `gc.dev.json` sync for Unity editor local play settings.
- Added a file-backed `GamingCouch` inspector flow for entry, seed, and eight-seat roster edits.
- Added warning-only `gc.platform.json` light-read behavior when platform data is missing or invalid.
- Added valid platform data gates for Apply and Play, including Unity platform, selected entry, and entry `maxPlayers`.
- Added editor Play Mode and Gaming Couch restart gates that auto-apply valid non-conflicted drafts and block invalid or conflicted drafts.
- Added editor-only `com.unity.nuget.newtonsoft-json` dependency for structured JSON parsing and unknown-field-preserving `gc.dev.json` writes.
- Added Gaming Couch web export settings documentation for the Unity 6 workflow, project-local template install, release defaults, no-overwrite behavior, warning-only build-target policy, and v1 playtest exclusion.

### Changed

- This development line now targets Unity 6 for Gaming Couch web export settings and splash/logo removal.
- Unity editor local play settings now use root `gc.dev.json` as the source of truth instead of old scene-serialized entry/player settings.
- Unity requires an existing root `gc.dev.json`; it does not create, bootstrap, or repair local project JSON files.
- Unity editor local playtests may run with one enabled seat even when production platform data declares a higher `minPlayers`.
