# Changelog

## [Unreleased]

## [0.1.0-alpha.7] - 2026-09-10

### Fixed

- Fixed the Codex test bridge activating in every Editor with this package installed. It was gated behind `#if UNITY_INCLUDE_TESTS`, which is active in a package consumer's Editor, so simply installing the package started a background file-polling bridge that created directories outside the project, wrote a token manifest into the user's application-data directory, and (on macOS/Linux) tightened permissions there. The bridge is now opt-in per host project -- a `.gamingcouch/codex-bridge.enabled` marker file or the `GAMINGCOUCH_CODEX_TEST_BRIDGE` environment variable -- and is completely inert without one.
- Fixed the test bridge restricting a directory it does not own: its permission walk rooted at the local application-data directory itself, so on macOS/Linux it chmod-ed `~/.local/share` to 0700. It now creates, symlink-rejects and 0700-restricts only `<localappdata>/Gaming Couch` and below.
- Fixed `GamingCouch.Instance.Clear()` dropping the run-scoped player-index mapping, which did not reset the run but permanently disarmed it: every later platform inputs message was silently dropped and `GameOver()` could never submit a placement. The round-reset pattern -- `Clear()` followed by `SetupPlayers` without a fresh `Play()` -- works again.
- Fixed `GCPlayerStore.Clear()` leaving player game objects alive outside play mode. It called `Object.Destroy`, which defers outside play mode (and logs "Destroy may not be called from edit mode"), so the store emptied while the objects lingered in the scene. It now destroys immediately outside play mode and iterates a snapshot, so a `GCPlayer` subclass that touches the store from `OnDestroy` cannot break the loop mid-clear. Player builds are unaffected -- this only showed up in the Editor and editor tooling.
- Fixed the DevApp devtool router applying zeroed values for keys a message omits. `JsonUtility` cannot express an absent key, so a payload-less `timescale_state` was applied as timescale 0 (clamped downstream) and unpaused, silently unpausing a paused game. Missing-key devtool messages are now ignored.
- Fixed the `GamingCouch` inspector re-running the full Start Screen readiness scan -- disk reads, a whole-scene component walk, GameView reflection and WebGL template stats -- on every repaint. It is now cached and refreshed at most twice a second.

## [0.1.0-alpha.6] - 2026-07-18

### Changed

- Rebaked the WebGL runtime-info payload (`Runtime/Resources/GamingCouchRuntimeInfo.json`) and package version to `0.1.0-alpha.6`; `gameProtocolVersion` stays `1`.

### Added

- Added a one-command version-bump release protocol (`Tools/bump-version.py` plus `npm run release:*` shortcuts) that bumps `package.json`, re-bakes the runtime-info payload, verifies, commits, and creates the `unity-<version>` tag. This is maintainer tooling and does not change package runtime behavior.

### Removed

- Removed the Gaming Couch Unity Template link from the package documentation reference list.

## [0.1.0-alpha.5] - 2026-07-14

### Added

- Added a "Gaming Couch scenes" section at the top of the Start Screen that lists project scenes containing a `GamingCouch` component, highlights the active scene, and lets you open a pre-existing scene without opening each scene to detect it. Unity crash-recovery `_Recovery` backups are excluded from the list.
- Added a "Wire example game" action (Start Screen button and menu) that upgrades the barebones template scene in place into the full playable example, swapping the listener component and repointing the player prefab without overwriting existing work.

### Changed

- Restructured the generated example into two self-contained, swappable game scripts sharing one scene: a barebones `GCExampleTemplate` (wiring demo, stock `GCPlayer`) and a full `GCExampleGame` (playable loop, `GCExamplePlayer`). Each script is itself the platform listener and implements the `SendMessage` lifecycle methods directly, with no adapter or base class, and the example now compiles against the real runtime API so generation cannot silently drift from it. Renamed `GCGameExample` to `GCExampleGame` and `GCPlayerExample` to `GCExamplePlayer` accordingly.
- Changed "Create New Example Scene" to reset the example to a single canonical `GCExampleScene` instead of adding a scene alongside the existing one. The action is now idempotent: it moves previous example scenes and any leftover blocking folders to the Trash (recoverable) and reuses valid existing example scripts and the player prefab rather than deleting and regenerating them.
- Hid the Active Scene Name/Path summary in the Start Screen whenever the active scene already appears in the Gaming Couch scenes list, keeping it only as a fallback for an active scene with no `GamingCouch` component.

### Fixed

- Fixed the WebGL runtime attestation reporting a stale version: the baked `Runtime/Resources/GamingCouchRuntimeInfo.json` was left at `0.1.0-alpha.3` after the `0.1.0-alpha.4` version bump and is now rebaked to match the released package version.
- Fixed editor console noise and IMGUI layout errors during example scene setup by guarding the `[ExecuteInEditMode]` log initialization behind play mode and running Start Screen actions on the next editor tick instead of mid-`OnGUI`.
- Fixed example-folder cleanup so it is honestly recoverable: imported folders are moved to the Trash, a raw un-imported folder is removed only when empty, and a non-empty un-imported folder is refused rather than permanently deleted.

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
