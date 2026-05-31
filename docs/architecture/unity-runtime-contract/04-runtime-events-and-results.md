## Runtime Events And Results Plan

Status: Core contract decisions captured; implementation-ready for the staged rollout plan in `07-cross-repo-rollout.md`.

## Purpose

Define a strict runtime-to-platform event contract and a future-proof game result schema. Reserve platform-to-runtime ingress shape without implementing inbound behavior in this slice.

## Carry-Forward Decisions

- Runtime player state is the canonical source for elimination and finish facts.
- HUD should be treated as a configurable projection of runtime state, not the long-term source of player state truth.
- Runtime event vocabulary reuses the `GCPlayer` state model from `03-player-state-model.md`: elimination state and finish state are each `None`, `Revokable`, or `Permanent`.
- The immediate game-over migration uses an object-wrapped result payload instead of a bare array. Initial shape is `GameOverResult { playerIndicesByPlacement: int[] }`.
- The legacy bare array bridge is migration-only and must not receive new result semantics.
- Runtime events are not a public custom event API in v1. The Unity package emits cataloged events from supported GC runtime APIs only.

## Runtime Event Transport

- Unity emits runtime events in `runtime_events` batches.
- Each batch has `schemaVersion: 1`, optional `runId`, and an `events[]` array.
- A malformed batch envelope is rejected as a whole and diagnosed with `gc.events.malformed_event`.
- A valid batch with one malformed event keeps the valid events and rejects only the malformed event.
- Sinks must validate event count, field types, string bounds, array bounds, and known catalog membership before storing or forwarding events.

## Runtime Event Envelope

Each event uses this normalized shape:

```json
{
  "schemaVersion": 1,
  "eventId": "runtime-generated unique id",
  "eventType": "gc.player.elimination_state_changed",
  "sequence": 1,
  "emittedAt": 1760000000000,
  "runId": "optional active run id",
  "mappingId": "optional active player mapping id",
  "playerIndex": 0,
  "actorPlayerIndex": null,
  "reason": {
    "code": "optional_stable_reason",
    "text": "optional bounded developer text"
  },
  "payload": {}
}
```

- `eventId` is unique within the active run. A GUID is acceptable; deterministic IDs are not required.
- `sequence` is a one-based integer that increases monotonically per active run and never resets inside a run.
- `emittedAt` is UTC epoch milliseconds from the Unity runtime or hosted adapter.
- `runId` is included whenever the current transport already knows the active run.
- `mappingId` is included when the event references active players and the mapping is available.
- `playerIndex` is the affected Active Player Index when an event has one clear target.
- `actorPlayerIndex` is optional and used only when a supported GC API can identify a game-facing player actor. Most v1 state calls do not have an actor.
- `reason.code` is optional, stable, lowercase snake-case, and platform-defined. Developer-provided free text belongs in `reason.text`, not `reason.code`.
- `payload` is catalog-specific, bounded, and object-shaped. Do not allow arbitrary extension data in v1.
- Public events and diagnostics never include platform player IDs. Hosted adapters may correlate `playerIndex` to platform player IDs in private adapter state after validation.

## Ordering And Delivery

- The event stream is ordered by `sequence` within one active run.
- Delivery is best-effort over the live runtime connection in v1. No replay-on-reconnect, durable telemetry storage, or exactly-once delivery is required.
- Sinks use `eventId` and `sequence` to detect duplicates, gaps, and out-of-order delivery when useful, but v1 UI should not fail a run only because an event gap is observed.
- Runtime state changes are applied before their events are enqueued. HUD projections built in the same frame read the new canonical state.
- HUD updates and runtime events are separate streams. Consumers must not depend on cross-stream arrival order.
- A terminal game-over API call enqueues `gc.game.result_submitted` and submits the object-wrapped `GameOverResult` from the same canonical state snapshot.

## Event Catalog V1

### `gc.game.play_started`

Emitted when runtime play data is accepted and the active-player mapping is frozen.

Payload:

```json
{
  "activePlayerCount": 2,
  "seed": 12345
}
```

### `gc.player.elimination_state_changed`

Emitted when a supported GC player-state API changes elimination state.

Payload:

```json
{
  "previousState": "None",
  "state": "Permanent"
}
```

Rules:

- `playerIndex` is required.
- `previousState` and `state` are `None`, `Revokable`, or `Permanent`.
- Duplicate calls and invalid transitions that no-op emit diagnostics, not state-change events.
- Permanent elimination is represented here for future phone death screens, personal death sounds, and player-facing elimination UI. No v1 input suppression is implied.

### `gc.player.finish_state_changed`

Emitted when a supported GC player-state API changes finish state.

Payload:

```json
{
  "previousState": "None",
  "state": "Revokable"
}
```

Rules:

- `playerIndex` is required.
- `previousState` and `state` are `None`, `Revokable`, or `Permanent`.
- Duplicate calls and invalid transitions that no-op emit diagnostics, not state-change events.

### `gc.game.result_submitted`

Emitted when `GameOver()` submits a result.

Payload is the accepted `GameOverResult` object for the same terminal submission.

Rules:

- This event is emitted only after result validation succeeds.
- Invalid game-over placements emit `gc.events.invalid_game_over_placements` and do not emit `gc.game.result_submitted`.
- The event does not replace the existing game-over result message in v1; it gives future platform features a cataloged event to consume.

## GameOverResult V1

The v1 result object is intentionally narrow:

```json
{
  "playerIndicesByPlacement": [0, 1]
}
```

Rules:

- `playerIndicesByPlacement` is required.
- Every active `playerIndex` must appear exactly once.
- `playerIndex: 0` is valid.
- Missing, duplicate, non-integer, negative, out-of-range, extra, or unknown placement values reject the result and emit `gc.events.invalid_game_over_placements`.
- V1 represents a total placement order only. It does not represent ties, DNF, teams, abandoned rounds, no-contest outcomes, score snapshots, or structured result reasons.
- The legacy bare array bridge remains array-shaped and is interpreted only as `playerIdsByPlacement` for already-built older Unity games.

## Reserved Result Growth

Richer result semantics are reserved for a future GameOverResult v2 contract after a general result/versioning policy is chosen, rather than being partially accepted in v1.

Reserved v2 concepts:

- `resultKind`: `placements`, `no_contest`, or `abandoned`.
- `placements[]`: ordered placement groups with `rank`, `playerIndices[]`, optional `outcome`, optional `reason`, and optional score summary.
- `teams[]`: team id plus player indices for team-based results.
- `scoreSnapshots[]`: bounded per-player final score/lives/status/meter snapshots.
- `reason`: stable result-level reason code plus bounded developer text.
- `metadata`: bounded platform-owned extension object, not a game-defined arbitrary data bag.

V1 sinks must reject these future fields if they appear in the v1 result object. Do not silently ignore richer result data because that would make developer validation misleading.

## Validation Behavior

- Unknown `eventType` values are ignored and diagnosed with `gc.events.unknown_event`; they do not close the runtime connection.
- Malformed known events are rejected and diagnosed with `gc.events.malformed_event`.
- Event payloads that reference invalid player indices are rejected and diagnosed with `gc.mapping.invalid_player_index`.
- Reason codes that fail naming or length bounds are treated as malformed event payloads.
- Bounded developer text may be truncated by the emitter before transport; sinks should still enforce maximum length.
- Diagnostics are separate from runtime events. Do not encode diagnostics as runtime events.

## Platform-To-Runtime Ingress Reservation

No platform-to-runtime event implementation is included in this slice, but reserve this future shape:

```json
{
  "type": "platform_runtime_messages",
  "schemaVersion": 1,
  "runId": "active run id",
  "messages": [
    {
      "schemaVersion": 1,
      "messageId": "platform-generated unique id",
      "messageType": "gc.platform.capability_changed",
      "sequence": 1,
      "sentAt": 1760000000000,
      "payload": {}
    }
  ]
}
```

Reserved ingress rules:

- Ingress is best-effort until a future plan explicitly requires acknowledgements or replay.
- Unknown `messageType` values are ignored by Unity and diagnosed when diagnostics are available.
- Future capability negotiation should use explicit capability names such as `runtime_events.v1` and `platform_runtime_messages.v1`; exact advertised capability transport is owned by rollout planning.
- No v1 Unity game code should depend on receiving platform-to-runtime messages.

## Ready When

- Event transport and envelope are implemented with strict validation.
- Initial catalog includes play-start, player elimination state, player finish state, and submitted-result events.
- V1 result submission accepts only the object-wrapped total placement result and keeps the legacy bare array bridge separate.
- Future death screens, personal sounds, victory sounds, stats, moderation, and replay/telemetry can consume v1 state/result events or clearly require a future v2 result.
- Tests cover valid events, malformed events, unknown events, invalid player indices, result edge cases, event ordering by sequence, best-effort duplicate/gap handling, and adapter-private mapping to platform IDs.

## Test Scenarios

- `playerIndex: 0` appears in state-change events and game-over results.
- A permanent elimination state call emits one `gc.player.elimination_state_changed` event with `state: "Permanent"`.
- A duplicate permanent elimination call emits a state diagnostic and no second state-change event.
- `GameOver()` with every active player index exactly once emits `gc.game.result_submitted` and submits `GameOverResult { playerIndicesByPlacement }`.
- `GameOver()` with a missing, duplicate, or out-of-range index emits `gc.events.invalid_game_over_placements` and does not publish platform results.
- A runtime event with an unknown `eventType` is ignored with `gc.events.unknown_event`.
- A v1 result object containing reserved v2 fields is rejected rather than partially accepted.
- A legacy bare game-over array is accepted only through the temporary bridge and diagnosed as `gc.api.legacy_runtime_payload`.
