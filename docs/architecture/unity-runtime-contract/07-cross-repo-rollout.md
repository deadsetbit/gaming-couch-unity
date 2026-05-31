## Cross-Repo Rollout Plan

Status: Planning required before implementation scheduling.

## Purpose

Define how the Unity package, Gaming Couch client, SDK, DevApp, and internal games move through this contract migration without long-lived semantic drift.

## Carry-Forward Decisions

- Unity package source API should break old game-facing ID/name/state calls intentionally so internal game source migrates to `Index`, `playerIndex`, and explicit permanent/revokable state APIs.
- Runtime game-facing DTOs should remove player names and platform player IDs completely. Temporary compatibility lives in platform/client/SDK adapters for already-built games, not in new Unity game source APIs.
- Active-player deterministic shuffle is in scope for this migration and should land with the index mapping contract.
- Already-built older Unity games still need a short-lived platform/client/SDK runtime bridge during migration.
- The bridge is a one-off internal migration bridge, not the long-term versioning, deployment, or legacy support strategy.
- New Unity game-over results use an object shape rather than a bare array so the result contract can expand later. The initial object carries `schemaVersion: 1` and `playerIndicesByPlacement`.
- During the temporary bridge, adapters distinguish new versus legacy game-over output by shape: object means new active-player-index result, bare array means legacy `playerIdsByPlacement`. This avoids ambiguity around `playerIndex: 0` versus old positive platform IDs.
- The API/state child plans do not decide `gameProtocolVersion`; this rollout plan owns bump versus adapter versus staged release.
- Target Unity package release line for the source API migration is `0.2.0-alpha.1`.

## Decisions To Make

- Compatibility strategy: `gameProtocolVersion` bump, internal-game migration without bump, compatibility adapter, or staged combination.
- Game-over bridge field ownership: exact runtime message field name for the new object result, legacy array acceptance boundaries, and rejection behavior for object/array hybrids.
- JS follow-up: scope and timing for aligning JavaScript runtime semantics with Unity, or documenting intentional divergence.
- Branch order: which repo lands first, how dependent branches are tested, and how temporary compatibility code is removed.
- Release order: package version, DevApp/client release, hosted runtime support, and internal game migration checkpoints.
- Skew matrix: old Unity package/new client, new Unity package/old client, old DevApp/new Unity, new DevApp/old Unity, hosted WebGL, and local Editor play.
- Validation: which checks run in Unity, client/SDK, DevApp, and cross-repo integration before merge/release.
- Rollback: how to recover if hosted runtime or DevApp sees unsupported contract shapes.

## Open Questions

- Are all affected games internal and migratable before release?
- Is a protocol bump cheaper than carrying compatibility adapters?
- Should JS migration be a hard prerequisite for release or the next tightly coupled follow-up?
- Which compatibility combinations should fail loudly instead of adapting?
- Which exact release checkpoint removes the legacy array bridge and hard-obsolete Unity symbols?

## Temporary Bridge Shape

- New result payload: object-wrapped `GameOverResult` with `schemaVersion: 1` and `playerIndicesByPlacement: int[]`.
- Legacy result payload: bare `int[]`, interpreted only as `playerIdsByPlacement` for already-built old games.
- Bridge discriminator: use value shape, not numeric range. `Array.isArray(value)` is legacy. Plain object is new. Null, primitive, mixed object/array, or object with legacy ID fields is malformed.
- New object validation allows `playerIndex: 0` and requires every active player index exactly once.
- Legacy array validation keeps old platform-ID rules and maps IDs through the stored legacy adapter context.
- The bridge may warn through diagnostics when legacy payloads are accepted, but it should not make migrated source code rely on legacy arrays.

## Post-Legacy Cleanup Ledger

After all internal games are migrated off the temporary bridge:

- Delete legacy array-shaped game-over acceptance in hosted client, SDK, DevApp, and Unity local-play adapters.
- Delete bridge diagnostics and tests that exist only for already-built ID-based Unity games.
- Delete hard-obsolete Unity placeholders after their compile messages are no longer needed.
- Delete adapter code that maps game-facing names or platform IDs into Unity runtime payloads.
- Delete unsupported multiplayer opt-in/stubs if no migrated game still requires them.

## Ready When

- The release decision is documented with tradeoffs.
- The cross-repo branch and release order is explicit.
- The skew matrix has expected outcomes and tests.
- The temporary object-vs-array game-over bridge is specified with removal criteria.
- Internal game migration scope is known.
- DevApp/client/SDK validation is scheduled before implementation tasks are marked ready.
