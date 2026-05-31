## Player State Model Plan

Status: Core contract decisions captured. Implementation planning can proceed after diagnostics code names and rollout bridge details are aligned.

## Purpose

Define the runtime state model for permanent and revokable elimination, permanent and revokable finish, score/lives/status/meter, placement, HUD projection, input expectations, and invalid transition behavior.

## State Model

- Elimination state is explicit:
  - `None`
  - `Revokable`
  - `Permanent`
- Finish state is explicit:
  - `None`
  - `Revokable`
  - `Permanent`
- Elimination and finish are independent facts. A player can be both finished and eliminated before game over.
- Revokable states count as current state at `GameOver()` if they have not been revoked. `GameOver()` snapshots current state; it does not auto-revoke or auto-promote.
- Permanent calls promote matching revokable state in place and record the permanent action reason/time.
- Revoke calls only work for revokable state. Revoking `None` or `Permanent` warns and no-ops.
- Duplicate calls warn and no-op unless the call is the allowed revokable-to-permanent promotion.
- After `GamingCouch.Instance.GameOver()` is called, player state/stat/HUD mutator APIs warn and no-op because final results are frozen.
- Player input routing remains independent from state. Unity continues receiving inputs for every active runtime participant, and game code decides whether to ignore inputs based on player state.

## Public API Surface

- Elimination methods:
  - `SetEliminatedPermanent(string reason)`
  - `SetEliminatedRevokable(string reason)`
  - `SetRevokeEliminated(string reason)`
- Finish methods:
  - `SetFinishedPermanent(string reason)`
  - `SetFinishedRevokable(string reason)`
  - `SetRevokeFinished(string reason)`
- Expose enum properties:
  - `GCPlayerEliminationState EliminationState`
  - `GCPlayerFinishState FinishState`
- Expose derived booleans for common checks:
  - `IsEliminated`
  - `IsEliminatedPermanent`
  - `IsEliminatedRevokable`
  - `IsFinished`
  - `IsFinishedPermanent`
  - `IsFinishedRevokable`
- Keep `reason` as a free-form string in this slice. Stable reason codes are deferred to runtime-events/results planning.
- Public timestamp names match the method names, for example:
  - `LastSetEliminatedPermanentTime`
  - `LastSetEliminatedRevokableTime`
  - `LastSetRevokeEliminatedTime`
  - `LastSetFinishedPermanentTime`
  - `LastSetFinishedRevokableTime`
  - `LastSetRevokeFinishedTime`
- Also expose convenience timestamps for common logic that should not branch by permanence, such as the latest eliminated, finished, and revoke times.

## State Events

- Replace old reversible callbacks with event-args callbacks:
  - `OnEliminationStateChanged`
  - `OnFinishStateChanged`
- Event args include:
  - `playerIndex`
  - `oldState`
  - `newState`
  - `reason`
  - `changedAt`
- Event args structs leave room for future stable reason codes without another callback migration.

## Store, Placement, And HUD

- `GCPlayerStore` keeps broad uneliminated/eliminated concepts and adds state-specific collections.
- State-filtered player collections use `Players...` prefix naming for discoverability:
  - `PlayersEliminated`
  - `PlayersEliminatedPermanent`
  - `PlayersEliminatedRevokable`
  - `PlayersFinished`
  - `PlayersFinishedPermanent`
  - `PlayersFinishedRevokable`
- Bot and non-bot variants are fully symmetric:
  - Example: `PlayersEliminatedPermanentBot`, `PlayersEliminatedPermanentNonBot`, `PlayersFinishedRevokableBot`, `PlayersFinishedRevokableNonBot`.
- Keep `Uneliminated` terminology for now. A broader naming pass is deferred.
- Built-in placement criteria stay broad:
  - `Eliminated` criteria treat both permanent and revokable elimination as eliminated.
  - `Finished` criteria treat both permanent and revokable finish as finished.
  - Complex state-aware ordering is deferred to a future custom placement comparer API.
- Runtime player state is canonical. HUD remains configurable, but HUD data is a projection of runtime state.
- HUD payloads should expose `playerIndex`, `eliminationState`, `finishState`, `placement`, `value`, and `meter`.
- Temporary receiving-end adapters may derive old boolean `eliminated` behavior while platform/client code catches up.

## Diagnostics

- Invalid state calls emit structured diagnostics with granular stable codes.
- Planned diagnostic categories include duplicate state call, invalid revoke, invalid transition, post-game-over mutation, clamped value, and unsupported multiplayer use.
- Diagnostics bypass `GCLog.logLevel` as defined in `05-diagnostics-spine.md`.

## Testing Decisions

- Tests cover valid transitions, duplicate no-ops, invalid revoke attempts, revokable-to-permanent promotion, finish/elimination coexistence, and post-game-over no-ops.
- Tests cover state event args, timestamps, derived booleans, store collections including bot/non-bot variants, and placement with broad eliminated/finished criteria.
- Tests cover HUD projection payload state fields and temporary adapter-derived boolean behavior where that adapter exists.
- Tests confirm input routing continues for all active participants regardless of elimination or finish state.

## Ready When

- The explicit elimination and finish state tables are implemented and tested.
- Placement, HUD, store collections, and input behavior are defined for every state.
- Diagnostics are emitted for invalid state usage.
- Runtime-events/results planning reuses the same state names rather than introducing parallel HUD-only terms.
