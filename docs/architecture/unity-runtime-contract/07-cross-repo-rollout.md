## Cross-Repo Rollout Plan

Status: Planning required before implementation scheduling.

## Purpose

Define how the Unity package, Gaming Couch client, SDK, DevApp, and internal games move through this contract migration without long-lived semantic drift.

## Carry-Forward Decisions

- Unity package source API should break old game-facing ID/name/state calls intentionally so internal game source migrates to `Index`, `playerIndex`, and explicit permanent/revokable state APIs.
- Already-built older Unity games still need a short-lived platform/client/SDK runtime bridge during migration.
- The bridge is a one-off internal migration bridge, not the long-term versioning, deployment, or legacy support strategy.
- The API/state child plans do not decide `gameProtocolVersion`; this rollout plan owns bump versus adapter versus staged release.
- Target Unity package release line for the source API migration is `0.2.0-alpha.1`.

## Decisions To Make

- Compatibility strategy: `gameProtocolVersion` bump, internal-game migration without bump, compatibility adapter, or staged combination.
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

## Ready When

- The release decision is documented with tradeoffs.
- The cross-repo branch and release order is explicit.
- The skew matrix has expected outcomes and tests.
- Internal game migration scope is known.
- DevApp/client/SDK validation is scheduled before implementation tasks are marked ready.
