## Cross-Repo Rollout Plan

Status: Core contract decisions captured; implementation-ready for the staged adapter rollout.

## Purpose

Define how the Unity package, Gaming Couch client, SDK, DevApp, and internal games move through this contract migration without long-lived semantic drift.

## Carry-Forward Decisions

- Unity package source API should break old game-facing ID/name/state calls intentionally so internal game source migrates to `Index`, `playerIndex`, and explicit permanent/revokable state APIs.
- Runtime game-facing DTOs should remove player names and platform player IDs completely. Temporary compatibility lives in platform/client/SDK adapters for already-built games, not in new Unity game source APIs.
- Active-player deterministic shuffle is in scope for this migration and should land with the index mapping contract.
- Already-built older Unity games still need a short-lived platform/client/SDK runtime bridge during migration.
- The bridge is a one-off internal migration bridge, not the long-term versioning, deployment, or legacy support strategy.
- New Unity terminal placement submissions use an object shape rather than a bare array so effectful runtime messages can expand later. The initial object carries `playerIndicesByPlacement` only; no one-off version field is introduced for this method.
- During the temporary bridge, adapters distinguish new versus legacy terminal placement output by shape and message path: a `runtime_messages` effectful placement object means new active-player-index result, while legacy `runtime_game_over.playerIdsByPlacement` or hosted bare array means legacy platform IDs. This avoids ambiguity around `playerIndex: 0` versus old positive platform IDs.
- The Unity-first migration uses a staged adapter rollout with no immediate `gameProtocolVersion` bump. A future bump remains available as a later public compatibility boundary after internal games and JavaScript runtime semantics are aligned.
- Launch policy for diagnostics is staged with the runtime output work: Unity log capture is package/runtime-owned and emitted as structured `gc.diagnostic` messages when enabled, while WebGL loader `print`/`printErr` mirroring and browser console capture remain host-owned debug controls outside the Unity runtime contract.
- Target Unity package release line for the source API migration is `0.2.0-alpha.1`.

## Release Decision

Chosen path: staged adapter rollout.

- Do not immediately bump `gameProtocolVersion` for the Unity-first source migration.
- Ship strict adapter support for both new object-shaped terminal placement payloads and legacy top-level `playerIdsByPlacement` before recommending Unity package `0.2.0-alpha.1`.
- Use package-version compatibility checks to block new Unity package usage against old DevApp/client paths.
- Migrate internal Unity games to `Index`, `playerIndex`, explicit state APIs, and the new object terminal placement shape.
- Keep the bridge until no internal deployed build emits legacy ID-shaped Unity payloads.
- Revisit a protocol bump or broader runtime versioning boundary when JavaScript runtime semantics align and the legacy bridge is ready for removal.

## Runtime Message Ownership

DevApp/local runtime message:

- New Unity contract sends:

```json
{
  "type": "runtime_messages",
  "schemaVersion": 1,
  "runId": "active run id",
  "messages": [
    {
      "schemaVersion": 1,
      "messageType": "gc.game.terminal_placement_submitted",
      "sequence": 12,
      "runtimeTimeMs": 1234,
      "payload": {
        "playerIndicesByPlacement": [0, 1]
      }
    }
  ]
}
```

- Legacy already-built games may still send:

```json
{
  "type": "runtime_game_over",
  "timestamp": 1760000000000,
  "runId": "active run id",
  "playerIdsByPlacement": [2, 1]
}
```

Rules:

- `gc.game.terminal_placement_submitted.payload.playerIndicesByPlacement` is the new runtime-owned terminal placement field.
- `runtime_game_over.playerIdsByPlacement` is legacy-only and removal-bound.
- Messages containing both new terminal placement payloads and legacy `playerIdsByPlacement` are malformed.
- New versus legacy terminal placement payloads are discriminated by message path and shape: object-wrapped `playerIndicesByPlacement` inside `runtime_messages` is the new active-player-index result; top-level `playerIdsByPlacement` is the legacy platform-ID result.
- New terminal placement payloads must not contain legacy ID fields.
- Local DevApp and hosted adapters map accepted `playerIndicesByPlacement` through the active-player mapping before updating platform-facing `LatestGameOverResult.playerIdsByPlacement`.
- Platform-facing playlist/stats surfaces may keep `playerIdsByPlacement` while platform state still uses platform player IDs.
- Terminal placement acceptance is first-accepted-wins per active run across both the new runtime message path and the temporary legacy bridge. After one new or legacy result is accepted, later duplicate, replayed, or second result messages are rejected, diagnosed, and must not mutate playlist, stats, or platform result state again.
- Accepting a legacy array/message emits `gc.api.legacy_runtime_payload`.

Hosted WebGL bridge:

- New Unity WebGL output uses `runtime_messages` and the same terminal placement object shape.
- Legacy Unity WebGL output remains a bare array and is interpreted only as platform player IDs.
- Object/array discrimination is by value shape only.
- Null, primitive, mixed object/array, object with legacy ID fields, or object with reserved future result fields is malformed.

## Branch Order

1. Unity package planning/docs and fixtures branch captures the final contracts.
2. Client/SDK adapter branch adds shape-discriminated Unity terminal placement handling, active-player mapping helpers, `runtime_messages`/`screen_space` validation, diagnostics callback validation, hosted launch policy for Unity log capture and host-owned console mirroring, and tests while preserving legacy behavior.
3. DevApp branch adds local `runtime_messages`, `screen_space`, active seat-to-index routing, metadata fallback health, diagnostics UI ingestion, launch-only Unity log capture controls, host-owned console mirroring controls where needed, and tests while preserving legacy `runtime_game_over.playerIdsByPlacement`.
4. Unity package implementation branch migrates source APIs, state model, runtime output, diagnostics, metadata view, examples, and local runtime messages.
5. Internal game migration branches update game source to `Index`, `playerIndex`, explicit state APIs, and object-shaped terminal placement behavior.
6. Hosted rollout branch/release enables the new Unity adapter path after client/SDK tests pass.
7. Cleanup branch removes the legacy bridge after the removal checkpoint is met.

Do not release `0.2.0-alpha.1` as the recommended package for internal games until DevApp/client adapters that understand `runtime_messages`, `screen_space`, and the new terminal placement shape are available.

## Release Order

1. Ship DevApp/client/SDK support that can accept old and new Unity result shapes and validate new runtime output paths.
2. Release Unity package `0.2.0-alpha.1`.
3. Migrate internal Unity game source to the new package and APIs.
4. Validate hosted play and local Editor play against migrated games.
5. Decide JS runtime follow-up timing before declaring the runtime identity vocabulary stable across engines.
6. Remove the temporary bridge only after no internal deployed build still emits legacy ID-shaped Unity payloads.

## Skew Matrix

| Combination | Expected outcome | Required validation |
| --- | --- | --- |
| Old Unity package / new DevApp-client | Supported through legacy `playerIdsByPlacement` bridge with `gc.api.legacy_runtime_payload`. | Legacy game-over, HUD/screen-point, and diagnostics bridge tests. |
| New Unity package / new DevApp-client | Supported. Runtime output uses active-player indices, `runtime_messages`, `screen_space`, and object-shaped terminal placement. | Full Unity, DevApp, client, SDK, and hosted integration tests. |
| New Unity package / old DevApp-client | Not supported. Block by package-version compatibility check or fail loudly before play/result publishing. | Negative skew test with clear developer error. |
| Old DevApp / new Unity Editor package | Not supported for local runtime messaging. Developers must update DevApp before using package `0.2.0-alpha.1`. | Local run compatibility check. |
| New DevApp / old Unity Editor package | Supported through legacy local runtime bridge while migration is active. | DevApp legacy message tests. |
| Old hosted Unity build / new hosted adapter | Supported through temporary legacy bridge. | Hosted adapter legacy array tests. |
| New hosted Unity build / old hosted adapter | Not released. Hosted deploy order must prevent this combination. | Release gate/checklist. |
| New Unity semantics / JavaScript runtime v1 | Allowed as temporary documented divergence, but JS follow-up must be scheduled. | Docs and follow-up issue/PRD. |

## Validation Plan

Unity package:

- EditMode tests for active-player mapping, deterministic shuffle fixtures, source API guidance, state transitions, `runtime_messages`, `screen_space`, diagnostics, metadata fallback, HUD rendering, and generated examples.
- Use the open-Editor test bridge for package validation where practical.

Client/SDK:

- Unit tests for ID-to-index input mapping, index-to-ID terminal placement/runtime-message/screen-space mapping, object terminal placement validation, legacy array bridge, malformed hybrid rejection, diagnostics callback validation, and hosted skew behavior.
- Cross-language deterministic shuffle fixtures for TypeScript and C#.

DevApp:

- Runtime message validation tests for `runtime_messages`, `screen_space`, effectful terminal placement, legacy `playerIdsByPlacement`, malformed hybrid messages, diagnostics ingress, active-run filtering, ring-buffer aggregation, seat-to-index routing, metadata fallback warnings, and upload/publish blocking.
- Browser/UI tests for the hidden diagnostics rail, persistent project health warnings, and the separation between runtime-owned Unity log capture and host-owned WebGL/browser console mirroring controls.

Cross-repo integration:

- Local Editor play smoke with a migrated Unity game.
- Local Editor play smoke with an older built Unity game while the bridge is active.
- Hosted WebGL smoke for new object-shaped terminal placement.
- Hosted WebGL smoke for legacy array result while the bridge is active.
- Rollback test that disables migrated game/package use without removing the adapter bridge.

## Rollback

- Keep the legacy bridge deployed until after migrated internal games are verified in hosted and local play.
- If hosted runtime rejects new object results, roll back internal game builds/package recommendation while leaving DevApp/client legacy bridge support in place.
- If DevApp local runtime support fails, block package `0.2.0-alpha.1` in DevApp compatibility messaging and continue using the old package for local tests.
- If diagnostics or non-effectful runtime messages are noisy but core terminal placement mapping works, keep the core adapter path and disable only optional diagnostics visibility, state snapshots, `screen_space`, optional Unity log capture, WebGL loader mirroring, or browser console capture.
- Do not remove legacy support in the same release that first enables new package support.

## JS Follow-Up

- JavaScript runtime migration is not a hard prerequisite for the Unity-first package source migration under the recommended staged path.
- JS follow-up must be scheduled before claiming the platform has one stable cross-engine runtime vocabulary.
- JS planning should decide whether JavaScript games also move to active-player indices, object-shaped terminal placement, `runtime_messages`, `screen_space`, structured diagnostics, and the same metadata fallback vocabulary.
- Until JS is migrated, docs must call out Unity-first semantics explicitly and avoid implying that JavaScript games already share the new Unity contract.

## Post-Legacy Cleanup Ledger

After all internal games are migrated off the temporary bridge:

- Delete legacy array-shaped terminal placement acceptance in hosted client, SDK, DevApp, and Unity local-play adapters.
- Delete bridge diagnostics and tests that exist only for already-built ID-based Unity games.
- Delete hard-obsolete Unity placeholders after their compile messages are no longer needed.
- Delete adapter code that maps game-facing names or platform IDs into Unity runtime payloads.
- Delete unsupported multiplayer opt-in/stubs if no migrated game still requires them.

## Ready When

- The staged adapter rollout is the implementation path.
- The cross-repo branch and release order is explicit.
- The skew matrix has expected outcomes and tests.
- The temporary object-vs-array terminal placement bridge is specified with removal criteria.
- Internal game migration scope is known.
- DevApp/client/SDK validation is scheduled before implementation tasks are marked ready.
