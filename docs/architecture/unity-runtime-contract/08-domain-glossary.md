## Unity Runtime Contract Domain Glossary

Status: Current contract glossary for Task 13 documentation alignment.

## Terms

- **Seat**: One-based local development roster slot used by DevApp and Unity Editor local play for controller assignment, display, and captured local play setup. Seats are not game-facing runtime identity. A seat may map to a different Active Player Index after deterministic shuffle, and sparse seats become dense active players at play capture.
- **Active Player Index**: Zero-based, dense, run-scoped Unity runtime participant identity exposed to game code as `playerIndex` and `GCPlayer.Index`. It is assigned from the captured active roster after deterministic shuffle and is frozen until the run restarts. The same human or bot may receive a different index in a later run.
- **Platform Player Id**: Platform-owned stable participant identifier used by hosted client, SDK, playlist, stats, and adapter bookkeeping. Unity game code, public runtime messages, public diagnostics, and screen-space anchors must not expose it. Hosted adapters may correlate it privately to `playerIndex` after validation.
- **Permanent Elimination**: Elimination state set through `SetEliminatedPermanent(reason)`. It is final for elimination state, promotes an existing revokable elimination in place, cannot be revoked, and counts as eliminated at `GameOver()`.
- **Revokable Elimination**: Elimination state set through `SetEliminatedRevokable(reason)`. It counts as eliminated while active, can be cleared only with `SetRevokeEliminated(reason)`, and can be promoted by `SetEliminatedPermanent(reason)`.
- **Permanent Finish**: Finish state set through `SetFinishedPermanent(reason)`. It is final for finish state, promotes an existing revokable finish in place, cannot be revoked, and counts as finished at `GameOver()`.
- **Revokable Finish**: Finish state set through `SetFinishedRevokable(reason)`. It counts as finished while active, can be cleared only with `SetRevokeFinished(reason)`, and can be promoted by `SetFinishedPermanent(reason)`.
- **Runtime Messages**: The `runtime_messages` output path for ordered semantic and effectful records emitted by the Unity package. V1 includes state snapshots, player transition messages, `gc.diagnostic` records, captured Unity logs when launch policy enables them, and `gc.game.game_over`. Records carry `sequence`, `runtimeTimeMs`, `messageType`, and bounded typed payloads.
- **Screen-Space Anchors**: The `screen_space` output path for hot latest-state normalized screen-coordinate anchors. V1 anchor types are `playerOverhead` and `playerPosition`; each anchor uses `playerIndex`, clamped `x`/`y`, and `isOffScreen`. Screen-space data is view-derived presentation data, not durable semantic game state.
- **Diagnostics**: Structured GC warning/error/info records emitted as `gc.diagnostic` runtime messages with stable `code`, `severity`, `sourceArea`, optional `playerIndex`, and bounded context. Diagnostics bypass package log-level filtering. Public diagnostics must not expose Platform Player Id values in payloads, details, debug data, DevApp UI, or hosted public callbacks.
- **Platform Metadata Fallback**: The degraded read-only `GCPlatformRuntimeView` used when `gc.platform.json` is missing or invalid in local development. The fallback has `validationState` of `missing` or `invalid`, `fallbackActive: true`, exact `notdefined` game and entry values, built-in player color defaults, and warning diagnostics. Unity never writes, repairs, or bootstraps `gc.platform.json`.

## Boundaries

- Unity game code uses `playerIndex` / `GCPlayer.Index`; it does not use Platform Player Id, player names, or seats.
- The Unity package boundary accepts current active-player / `playerIndex` identity only. Legacy hosted `players[]` and `playerId` payloads must be translated by the client/SDK before Unity is invoked.
- The Unity package preserves source-seat mapping to Active Player Index for local editor play after capturing Local Play Settings. This is current local-play behavior, not hosted Platform Player Id compatibility.
- Hosted adapters own Platform Player Id-to-index mapping. DevApp owns local seat assignment and display before local play capture.
- HUD rendering consumes runtime state and screen-space anchors. HUD data is not the semantic source of truth.
- Generated examples are a wiring demo for lifecycle, input polling, player state actions, runtime state/HUD essentials, diagnostics, and `GameOver()`. They are not a full sample game or a custom diagnostics API.
