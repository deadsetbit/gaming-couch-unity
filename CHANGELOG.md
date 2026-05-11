# Changelog

## [0.1.0-alpha.2] - 2026-05-09

### Added

- Added root `gc.dev.json` sync for Unity editor local play settings.
- Added a file-backed `GamingCouch` inspector flow for entry, seed, and eight-seat roster edits.
- Added warning-only `gc.metadata.json` light-read behavior when metadata is missing or invalid.
- Added valid metadata gates for Apply and Play, including Unity platform, selected entry, and entry `maxPlayers`.
- Added editor Play Mode and Gaming Couch restart gates that auto-apply valid non-conflicted drafts and block invalid or conflicted drafts.
- Added editor-only `com.unity.nuget.newtonsoft-json` dependency for structured JSON parsing and unknown-field-preserving `gc.dev.json` writes.

### Changed

- This development line now targets Unity 6 for clean WebGL export setup and splash/logo removal.
- Unity editor local play settings now use root `gc.dev.json` as the source of truth instead of old scene-serialized entry/player settings.
- Unity requires an existing root `gc.dev.json`; it does not create, bootstrap, or repair local project JSON files.
- Unity editor local playtests may run with one enabled seat even when production metadata declares a higher `minPlayers`.
