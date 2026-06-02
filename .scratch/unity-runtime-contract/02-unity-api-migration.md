## Unity API Migration Plan

Status: Core contract decisions captured. Implementation planning can proceed after cross-repo rollout confirms the temporary runtime bridge for older built games.

## Purpose

Define the developer-facing Unity API migration from platform-owned identity and ambiguous state methods to Active Player Index identity and explicit permanent/revokable player state methods.

This plan depends on:

- `01-active-player-index-mapping.md` for `playerIndex` semantics.
- `03-player-state-model.md` for elimination/finish state behavior.
- `05-diagnostics-spine.md` for structured warnings and unsupported API diagnostics.
- `07-cross-repo-rollout.md` for the short-lived platform/client/SDK bridge for older built games.

## API Contract

- `GCPlayer.Index` is the canonical game-facing identity.
- Public game-facing DTO and payload fields use `playerIndex`, not `index`, `Id`, or `playerId`.
- Old game-facing ID/name APIs become source-level compile breaks with explicit migration guidance where possible:
  - `GCPlayer.Id`
  - `GCPlayer.PlayerName`
  - `GamingCouch.GetInputsByPlayerId(int)`
  - player-store lookup APIs named by player ID
  - game-facing option and HUD DTO fields named `playerId`
- The new input polling API is `GamingCouch.GetInputsByPlayerIndex(int playerIndex)`.
- The new player store lookup API is `GetPlayerByIndex(int playerIndex)`.
- Old state methods become source-level compile breaks with explicit migration guidance:
  - `SetEliminated(reason)` -> choose `SetEliminatedPermanent(reason)` or `SetEliminatedRevokable(reason)`.
  - `SetUneliminated(reason)` -> use `SetRevokeEliminated(reason)` when revoking a revokable elimination.
  - `SetFinished(reason)` -> choose `SetFinishedPermanent(reason)` or `SetFinishedRevokable(reason)`.
- The source API intentionally breaks so internal game source migrates explicitly. Compatibility for already-built older games is a short-lived platform/client/SDK rollout bridge, not a permanent Unity package source-compatibility lane.

## Player State API Surface

- Add explicit elimination methods:
  - `SetEliminatedPermanent(string reason)`
  - `SetEliminatedRevokable(string reason)`
  - `SetRevokeEliminated(string reason)`
- Add explicit finish methods:
  - `SetFinishedPermanent(string reason)`
  - `SetFinishedRevokable(string reason)`
  - `SetRevokeFinished(string reason)`
- Use the spelling `Revokable` in public API names to keep technical language consistent across elimination and finish methods.
- Add enum-backed public state properties and derived booleans as defined in `03-player-state-model.md`.
- Add state-change callbacks using event-args structs, not multi-parameter `Action` signatures.

## Unsupported Multiplayer API Policy

- Multiplayer-facing APIs are hidden by default and compile out of the normal package surface.
- Legacy projects can opt in with scripting define `GC_ENABLE_UNSUPPORTED_MULTIPLAYER`.
- The NGO assembly should require both Netcode for GameObjects and `GC_ENABLE_UNSUPPORTED_MULTIPLAYER`.
- Multiplayer remains documented as unsupported in Gaming Couch. The opt-in exists only for temporary internal migration of games that already use the old multiplayer path.
- Runtime diagnostics for unsupported multiplayer use granular stable diagnostic codes when the opt-in path is exercised.

## Docs And Release Policy

- Target package release line: `0.2.0-alpha.1`.
- Add a dedicated migration guide covering:
  - `Id`/`playerId`/`PlayerName` migration to `Index`/`playerIndex`.
  - input polling by `playerIndex`.
  - old state method replacements.
  - permanent versus revokable elimination and finish examples.
  - unsupported multiplayer opt-in define and warning that the surface is not supported.
- Update README, `Documentation~/README.md`, generated example expectations, and `CHANGELOG.md`.
- Do not decide `gameProtocolVersion` here. The rollout plan owns bump versus adapter versus staged release.

## Testing Decisions

- EditMode tests cover canonical `Index`, `playerIndex` DTO/HUD payloads, new input API names, state API compile-break attributes/source guidance, and generated example source updates.
- Old compile-breaking APIs should be tested through attribute/source inspection or focused compile-fixture strategy that does not make the normal test assembly uncompilable.
- Unsupported multiplayer tests verify default symbol absence and opt-in assembly/API availability under `GC_ENABLE_UNSUPPORTED_MULTIPLAYER`.
- Diagnostics tests cover granular stable codes for unsupported multiplayer and invalid state/API runtime paths that can still execute.

## Ready When

- Every public API change has a replacement or explicit no-replacement decision.
- The player-state API names match `03-player-state-model.md`.
- Docs and examples map old game code to the new API.
- Cross-repo rollout has a temporary bridge plan for older built games.
- Tests can verify compile-break guidance without breaking the normal test assembly.
