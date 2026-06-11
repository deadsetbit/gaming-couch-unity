## Player State Model Plan

Status: Core contract decisions captured. Implementation planning can proceed after diagnostics code names and rollout bridge details are aligned.

## Purpose

Define the runtime state model for permanent and revokable elimination, permanent and revokable finish, score/lives/status/meter, placement, runtime state output, HUD rendering, input expectations, and invalid transition behavior.

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
- Keep `reason` as a free-form string in this slice. Stable reason codes are deferred to runtime output planning.
- Public game-facing timestamp names match the method names and use a `GameTime` suffix, for example:
  - `LastSetEliminatedPermanentGameTime`
  - `LastSetEliminatedRevokableGameTime`
  - `LastSetRevokeEliminatedGameTime`
  - `LastSetFinishedPermanentGameTime`
  - `LastSetFinishedRevokableGameTime`
  - `LastSetRevokeFinishedGameTime`
- Also expose convenience game-time timestamps for common logic that should not branch by permanence, such as the latest eliminated, finished, and revoke times.

## Timestamp Timebase

- New game-facing state timestamp properties and state event timestamps use scaled Unity game time in seconds. They advance with `Time.timeScale`, stop while game time is paused, and are suitable for game logic such as respawn delays, slow-motion checks, and rule timers.
- `changedAtGameTime` in state event args is this scaled game-time value. Do not expose an ambiguous `changedAt` field.
- Runtime messages, diagnostics, captured Unity log records, and receiver ordering use unscaled active-run-relative `runtimeTimeMs`. Do not use game-time timestamps for runtime output ordering.
- If a future callback needs both game-rule timing and runtime-output ordering, expose separate fields such as `changedAtGameTime` and `runtimeTimeMs` rather than overloading one timestamp.
- Existing pre-PRD timestamp properties, such as `LastSetEliminatedTime`, `LastSetUneliminatedTime`, and `FinishedTime`, already use scaled Unity `Time.time`; if retained temporarily as obsolete compatibility surfaces, they may preserve that behavior until removal.
- Placement criteria that sort by elimination or finish timing should use the new scaled game-time timestamps for permanent/revokable state and an internal monotonic state-change order as a tie-breaker for same-time changes.

## State Events

- Replace old reversible callbacks with event-args callbacks:
  - `OnEliminationStateChanged`
  - `OnFinishStateChanged`
- Event args include:
  - `playerIndex`
  - `oldState`
  - `newState`
  - `reason`
  - `changedAtGameTime`
- Event args structs leave room for future stable reason codes without another callback migration.

## Store, Placement, Runtime Output, And HUD

- `GCPlayerStore` keeps broad uneliminated/eliminated concepts and adds state-specific collections.
- State-filtered player collections use `Players...` prefix naming for discoverability:
  - `PlayersUneliminated`
  - `PlayersEliminated`
  - `PlayersEliminatedPermanent`
  - `PlayersEliminatedRevokable`
  - `PlayersFinished`
  - `PlayersFinishedPermanent`
  - `PlayersFinishedRevokable`
- Bot and non-bot variants are fully symmetric:
  - Example: `PlayersUneliminatedBot`, `PlayersUneliminatedNonBot`, `PlayersEliminatedPermanentBot`, `PlayersEliminatedPermanentNonBot`, `PlayersFinishedRevokableBot`, `PlayersFinishedRevokableNonBot`.
- Old store collection, enumerable, count, and lookup names hard-break with good substitute guidance instead of silently aliasing:
  - `UneliminatedPlayers` and `UneliminatedPlayersEnumerable` -> `PlayersUneliminated`
  - `UneliminatedBotPlayers` and `UneliminatedBotPlayersEnumerable` -> `PlayersUneliminatedBot`
  - `UneliminatedNonBotPlayers` and `UneliminatedNonBotPlayersEnumerable` -> `PlayersUneliminatedNonBot`
  - `UneliminatedPlayerCount` -> `PlayersUneliminated.Count`
  - `EliminatedPlayers` and `EliminatedPlayersEnumerable` -> `PlayersEliminated`
  - `EliminatedBotPlayers` and `EliminatedBotPlayersEnumerable` -> `PlayersEliminatedBot`
  - `EliminatedNonBotPlayers` and `EliminatedNonBotPlayersEnumerable` -> `PlayersEliminatedNonBot`
  - `EliminatedPlayerCount` -> `PlayersEliminated.Count`
  - ID-named store lookup APIs, including `GetPlayerById(int playerId)`, -> `GetPlayerByIndex(int playerIndex)`
- Hard-obsolete placeholders may stay temporarily only to provide compiler messages such as "Use PlayersEliminated; broad eliminated includes permanent and revokable elimination." They are removed after the post-legacy cleanup condition is met.
- Keep `Uneliminated` terminology for now under the `Players...` prefix. A broader naming pass is deferred.
- Built-in placement criteria stay broad:
  - `Eliminated` criteria treat both permanent and revokable elimination as eliminated.
  - `Finished` criteria treat both permanent and revokable finish as finished.
  - Complex state-aware ordering is deferred to a future custom placement comparer API.
- Runtime player state is canonical. HUD remains configurable, but HUD rendering consumes runtime state, screen-space anchors, and HUD configuration instead of owning the semantic data model.
- Runtime state snapshots should expose dynamic player fields: `playerIndex`, score, lives, status/statusText, meter, placement, `eliminationState`, and `finishState`.
- Meter is semantic runtime state in v1. Calls to `SetMeter` may happen every physics or rendered frame, but accepted meter changes must remain ordered transition facts in `runtime_messages`; only latest-state projections such as snapshots and HUD output may be coalesced.
- Static player type and color belong to the active-run roster/setup context, not to every dynamic state snapshot.
- `screen_space` carries view-derived overhead and player-position anchors separately from semantic player state.
- Temporary receiving-end adapters may derive old boolean `eliminated` behavior while platform/client code catches up.

## Deferred Contract Follow-Ups

- `SetMeter` is intentionally preserved as semantic state for v1, but the name is vague: it does not say what kind of game fact the platform should infer from the value. A future grilling session should decide whether `meter` remains a generic developer-facing progress field, becomes one or more platform-named semantic fields, or gains clearer capability metadata.
- `reason` remains free-form developer text in this slice. Future contract planning should decide whether player-state mutators need stable reason codes plus bounded developer text, and should keep platform-facing reason semantics separate from debug-only explanation text.
- Runtime output already treats transition `reasonText` as bounded; snapshots do not include `reason`. Future work should keep that split unless a specific product use case needs durable reason history in state snapshots or another message type.

## Diagnostics

- Invalid state calls emit structured diagnostics with granular stable codes.
- Planned diagnostic categories include duplicate state call, invalid revoke, invalid transition, post-game-over mutation, clamped value, and unsupported multiplayer use.
- Diagnostics bypass `GCLog.logLevel` as defined in `05-diagnostics-spine.md`.

## Testing Decisions

- Tests cover valid transitions, duplicate no-ops, invalid revoke attempts, revokable-to-permanent promotion, finish/elimination coexistence, and post-game-over no-ops.
- Tests cover state event args, timestamps, derived booleans, store collections including bot/non-bot variants, hard-obsolete substitute messages for old store names, and placement with broad eliminated/finished criteria.
- Tests cover runtime state snapshot fields, HUD rendering from runtime state, screen-space anchors, and temporary adapter-derived boolean behavior where that adapter exists.
- Tests cover that meter changes are semantic transitions and are not hidden by rendered-frame or physics-frame batching.
- Tests confirm input routing continues for all active participants regardless of elimination or finish state.

## Ready When

- The explicit elimination and finish state tables are implemented and tested.
- Placement, runtime output, HUD rendering, store collections, and input behavior are defined for every state.
- Diagnostics are emitted for invalid state usage.
- Runtime output planning reuses the same state names rather than introducing parallel HUD-only terms.
