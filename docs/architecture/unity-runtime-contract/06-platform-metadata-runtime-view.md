## Platform Metadata Runtime View Plan

Status: Core contract decisions captured; implementation-ready for the Unity package runtime view. Cross-repo rollout still owns upload/publish enforcement timing.

## Purpose

Define the read-only Unity runtime projection of dashboard-managed `gc.platform.json` data and the fallback behavior for missing or invalid metadata.

## Carry-Forward Decisions

- `gc.platform.json` is Platform Data, not a second Unity settings store.
- Unity must never create, repair, bootstrap, or write `gc.platform.json`.
- Runtime game code receives a normalized safe view only. It does not receive raw JSON or unknown dashboard fields.
- Missing or invalid platform data is visible as persistent editor/runtime health, not a silent healthy state.
- Local Editor play may continue in a degraded fallback state so developers can test game logic before dashboard metadata is repaired.

## Runtime Projection

The runtime projection is named `GCPlatformRuntimeView` in this plan. Implementation may choose C# type names that fit local style, but the public shape should preserve these fields.

```json
{
  "schemaVersion": 1,
  "validationState": "valid",
  "fallbackActive": false,
  "source": {
    "fileName": "gc.platform.json",
    "platformDataVersion": 1,
    "path": "optional local path",
    "message": null,
    "fieldName": null
  },
  "game": {
    "key": "contract-game",
    "name": "Contract Game"
  },
  "platform": {
    "id": "unity"
  },
  "selectedEntryKey": "duel",
  "entries": [
    {
      "entryKey": "duel",
      "name": "Contract Entry",
      "minPlayers": 1,
      "maxPlayers": 4,
      "botSupport": true
    }
  ],
  "playerColors": {
    "blue": {
      "base": [7, 78, 234],
      "muted": [185, 229, 252],
      "mutedDarker": [23, 37, 84]
    }
  }
}
```

Rules:

- `schemaVersion` is the runtime projection schema version, not the raw `platformDataVersion`.
- `validationState` is `valid`, `missing`, or `invalid` in v1.
- `fallbackActive` is true for `missing` and `invalid`.
- `source.platformDataVersion` is present only when read from a parsed platform data file.
- `source.path`, `source.message`, and `source.fieldName` are allowed in editor/development contexts and may be omitted from hosted runtime payloads.
- `selectedEntryKey` comes from the current Local Play Settings capture in Editor play or the selected hosted entry in hosted play.
- `entries` is an array sorted by entry key for deterministic serialization and fixtures.
- `playerColors` is keyed by supported runtime color names. RGB arrays are integer triples in the range `0..255`.
- Runtime code may inspect `validationState` and `fallbackActive`; it must not infer health from game key, entry key, or platform id alone.

## Valid Metadata Behavior

- Valid platform data requires:
  - integer `platformDataVersion`,
  - non-empty `game.key`,
  - non-empty `game.name`,
  - non-empty `platform.id`,
  - object-shaped `game.entries`,
  - each entry with non-empty `name`, non-negative `minPlayers`, `maxPlayers >= minPlayers`, and boolean `botSupport`,
  - object-shaped `properties.colors.players`; it may be empty, and missing individual color keys are filled from package defaults.
- For Unity editor play, `platform.id` must be `unity` when platform data is valid.
- Unknown top-level or nested fields are ignored until a future normalized field is added to the runtime projection.
- Entry `minPlayers` is preserved from dashboard metadata, but local editor play may still allow a one-seat run for development as existing local play behavior does.
- Entry `maxPlayers` remains a hard gate for local play capture when valid metadata is available.
- Bot seats on entries without bot support remain warning-only for local development unless rollout planning chooses a stricter hosted validation rule.

## Fallback Defaults

Missing, unreadable, invalid JSON, invalid root, invalid required fields, invalid entries, platform mismatch, or invalid required color structure use this fallback view:

```json
{
  "schemaVersion": 1,
  "validationState": "missing",
  "fallbackActive": true,
  "source": {
    "fileName": "gc.platform.json",
    "message": "gc.platform.json was not found."
  },
  "game": {
    "key": "notdefined",
    "name": "notdefined"
  },
  "platform": {
    "id": "unity"
  },
  "selectedEntryKey": "notdefined",
  "entries": [
    {
      "entryKey": "notdefined",
      "name": "notdefined",
      "minPlayers": 1,
      "maxPlayers": 8,
      "botSupport": true
    }
  ],
  "playerColors": {
    "blue": { "base": [7, 78, 234], "muted": [185, 229, 252], "mutedDarker": [23, 37, 84] },
    "red": { "base": [243, 63, 94], "muted": [253, 204, 210], "mutedDarker": [76, 5, 25] },
    "green": { "base": [75, 130, 22], "muted": [216, 248, 156], "mutedDarker": [22, 101, 52] },
    "yellow": { "base": [250, 190, 36], "muted": [253, 239, 137], "mutedDarker": [179, 83, 9] },
    "purple": { "base": [138, 92, 245], "muted": [215, 179, 253], "mutedDarker": [88, 28, 134] },
    "pink": { "base": [251, 164, 164], "muted": [255, 240, 241], "mutedDarker": [218, 39, 119] },
    "cyan": { "base": [45, 211, 190], "muted": [219, 251, 230], "mutedDarker": [3, 105, 160] },
    "brown": { "base": [158, 72, 35], "muted": [201, 137, 4], "mutedDarker": [67, 20, 7] }
  }
}
```

Rules:

- The fallback entry key is exactly `notdefined`.
- The fallback game key and name are exactly `notdefined`.
- The fallback platform id is `unity` because this is the Unity package runtime projection; `validationState` and `fallbackActive` are the source-of-truth health fields.
- Fallback `maxPlayers` is 8 to match the local eight-seat roster.
- Fallback colors use the package's built-in `GCPlayerColorData` palette. `base` maps to `BaseColor`, `muted` maps to `Light`, and `mutedDarker` maps to `Dark`.
- Missing individual color keys in an otherwise valid platform file are filled from package defaults. Malformed color values make the platform data invalid and activate full fallback.

## Partial Invalid Behavior

V1 does not expose partially valid platform metadata.

- If required root, game, platform, entries, or color structure is invalid, discard the raw metadata and publish the fallback view.
- Do not merge valid entries from an invalid file into fallback metadata.
- Do not expose raw unknown fields to runtime code.
- The only per-field fallback in v1 is missing optional color keys, which use package defaults because player colors already have a stable runtime palette.
- Diagnostics include the first blocking invalid field/path where available. A future implementation may collect multiple invalid fields, but that is not required for v1.

## Health Semantics

- Editor inspector: missing or invalid platform data is a persistent warning. It should remain visible until the file becomes valid.
- Editor local play: fallback is allowed, but runtime and DevApp diagnostics must show degraded metadata health.
- Unity WebGL build: fallback is allowed for local development builds, but the build should be marked unhealthy in package/DevApp validation output.
- DevApp upload/publish validation: fallback metadata is blocking. Do not upload or publish a game build whose platform metadata is missing or invalid.
- Hosted runtime: production hosted play should receive validated dashboard/platform metadata from the platform. If hosted metadata is absent or invalid, fail validation before play rather than silently running with `notdefined`.

## Diagnostics

- Missing platform data emits `gc.metadata.missing_platform_data` and `gc.metadata.fallback_active`.
- Invalid platform data emits `gc.metadata.invalid_platform_data` and `gc.metadata.fallback_active`.
- Fallback diagnostics use `severity: warning` in Editor/local play.
- Upload/publish validation may promote the same condition to a blocking validation error outside the runtime diagnostic stream.
- Runtime fallback diagnostics should include `validationState`, `fieldName` when available, and `selectedEntryKey` when relevant.

## Read-Only Boundary

- Unity reads `gc.platform.json` from the local project root or receives a hosted normalized equivalent from platform infrastructure.
- Unity never writes, repairs, formats, or bootstraps `gc.platform.json`.
- Any "fix" or "create" action belongs to DevApp/dashboard-managed project setup, not the Unity package runtime.
- Runtime code receives snapshots only. Changing platform data during a run does not mutate the active runtime view until the next capture/run.

## Future Metadata

Reserved future normalized fields:

- runtime capabilities,
- localized entry names and descriptions,
- audio asset references for elimination/victory features,
- feature flags,
- controller configuration,
- dashboard-owned runtime behavior toggles,
- upload validation metadata.

Future fields must be added to the normalized projection intentionally. Do not expose a raw `metadata` or `properties` bag to game runtime code.

## Ready When

- Valid, missing, invalid, platform mismatch, and malformed-color cases produce concrete runtime views.
- Fallback shape is stable enough for fixtures.
- Editor, local play, build, upload, and hosted health semantics are explicit.
- Runtime diagnostics report metadata fallback without depending on package log level.
- Runtime projection can evolve for future dashboard-owned features without exposing raw unstable JSON to game code.

## Test Scenarios

- Valid `gc.platform.json` exposes game key/name, platform id, sorted entries, selected entry, limits, bot support, and player colors.
- Missing `gc.platform.json` exposes the `notdefined` fallback and emits missing/fallback diagnostics.
- Invalid JSON, invalid root, missing required fields, platform mismatch, invalid entry fields, and malformed color variants expose fallback and emit invalid/fallback diagnostics.
- Missing optional color keys in an otherwise valid file use package default color variants.
- Unknown fields are ignored and do not appear in runtime projection.
- Local play fallback is allowed with persistent warnings.
- Upload/publish validation treats fallback metadata as blocking.
- Changing `gc.platform.json` during an active run does not mutate the runtime view until the next capture/run.
