## Runtime Output Contract Plan

Status: Core contract decisions captured; implementation-ready for the staged rollout plan in `07-cross-repo-rollout.md`.

## Purpose

Define the Unity runtime output contract for semantic runtime messages, screen-space data, diagnostics, and effectful platform submissions. Replace HUD-owned data and passive event framing with a typed runtime output model that support tooling, DevApp, hosted adapters, HUD rendering, and future AI/debugging flows can consume.

Reserve platform-to-runtime ingress shape without implementing inbound behavior in this slice.

## Carry-Forward Decisions

- Runtime player/game state is the canonical source for player facts such as score, lives, status, meter, placement, elimination state, and finish state.
- HUD is a consumer of runtime state, screen-space anchors, and HUD configuration. HUD is not the semantic data model.
- Unity has two physical runtime output paths in v1:
  - `runtime_messages` for semantic state, transitions, diagnostics, and effectful platform-state submissions.
  - `screen_space` for hot latest-state screen-coordinate anchors.
- Host-owned WebGL loader output and browser console mirroring are not runtime output paths. They may be useful for local debugging, but `runtime_messages` and `screen_space` are the only v1 Unity runtime contract outputs.
- Static active-player facts live in the active-run roster/setup context: `playerIndex`, player type, and player color. Dynamic state snapshots reference `playerIndex` and do not repeat type or color.
- Runtime message vocabulary reuses the `GCPlayer` state model from `03-player-state-model.md`: elimination state and finish state are each `None`, `Revokable`, or `Permanent`.
- Runtime messages are not a public custom message API in v1. The Unity package emits cataloged messages from supported GC runtime APIs only.
- The Unity package must not mirror runtime messages through `Debug.Log` or browser console output. Logging runtime messages would double the runtime output work and make console text look like a contract source.
- Terminal placement submission is the first v1 effectful runtime message. Future effectful messages may submit other platform-owned game facts, but v1 only accepts total placement order.
- The immediate terminal placement migration uses an object-wrapped result payload instead of a bare array. Initial shape is `GameOverResult { playerIndicesByPlacement: int[] }`.
- The legacy bare array bridge is migration-only and must not receive new result semantics.

## Active Run Context

- The host, DevApp, or adapter establishes active-run context before runtime output is accepted.
- `runId` is context metadata, not a field repeated on every runtime message.
- Individual `runtime_messages` records carry:
  - `sequence`: one-based integer, monotonic per active run.
  - `runtimeTimeMs`: unscaled milliseconds since active run start.
- Receivers attach resolved run context, received wall-clock time, and connection identity at ingest.
- Transports that are not strictly scoped to one active run may include `runId` on the batch envelope for stale-message rejection. Individual records still should not repeat `runId`.
- `seed` remains gameplay/shuffle determinism input. It is not a substitute for `runId` because different runs may intentionally reuse the same seed.

## Runtime Messages Path

Unity emits `runtime_messages` batches for semantic and effectful records:

```json
{
  "type": "runtime_messages",
  "schemaVersion": 1,
  "messages": [
    {
      "schemaVersion": 1,
      "messageType": "gc.state.snapshot",
      "sequence": 1,
      "runtimeTimeMs": 1234,
      "payload": {}
    }
  ]
}
```

- Normal semantic messages are coalesced and flushed at most once per rendered frame after Unity simulation updates for that frame.
- Effectful messages must preserve ordering by flushing pending state before, or in the same ordered batch as, the effectful message.
- Batches are transport optimization, not product semantics. Consumers should process individual typed messages in sequence order.
- Use strict bounds: maximum runtime JSON is about `64 KiB`, and maximum messages per batch is `64` unless later profiling deliberately changes the limits.
- A malformed batch envelope is rejected as a whole and diagnosed.
- A valid batch with one malformed non-effectful message keeps valid messages and rejects only the malformed message.
- Unknown non-effectful `messageType` values are ignored and diagnosed. Unknown effectful message types are not applied.
- Sinks validate exact per-type schema, field types, string bounds, array bounds, known catalog membership, `playerIndex` mapping, unknown fields, and prototype-pollution keys before storing or forwarding messages.
- Public runtime messages and diagnostics never include platform player IDs. Hosted adapters may correlate `playerIndex` to platform player IDs in private adapter state after validation.

## State Snapshot Message

`gc.state.snapshot` carries the current semantic runtime state:

```json
{
  "schemaVersion": 1,
  "messageType": "gc.state.snapshot",
  "sequence": 2,
  "runtimeTimeMs": 1240,
  "payload": {
    "game": {
      "status": "playing"
    },
    "players": [
      {
        "playerIndex": 0,
        "score": 10,
        "lives": 2,
        "status": "Success",
        "statusText": "Finished lap",
        "meter": 74,
        "placement": 1,
        "eliminationState": "None",
        "finishState": "Revokable"
      }
    ]
  }
}
```

- Snapshots are full current run state, not deltas or patch operations.
- Snapshots emit on play start, meaningful semantic state changes, and immediately before accepted terminal placement submission.
- Snapshot emission is change-driven and coalesced. Do not emit every frame unless a semantic state value changes every frame.
- Snapshot payloads include all active players exactly once.
- Player type and color are read from the active-run roster/setup context, not repeated in each dynamic snapshot.
- Score, lives, status, status text, meter, placement, elimination state, finish state, and game status are v1 canonical semantic fields.
- Arbitrary game-defined custom data is out of scope for v1.
- State snapshots may be disabled at boot for profiling. Transition messages and effectful messages remain enabled by default.

## Transition Message Catalog V1

Transition messages explain supported semantic changes. Full state snapshots remain the source for current truth.

Initial player transition messages:

- `gc.player.score_changed`
- `gc.player.lives_changed`
- `gc.player.status_changed`
- `gc.player.meter_changed`
- `gc.player.elimination_state_changed`
- `gc.player.finish_state_changed`

Payload rules:

- `playerIndex` is required.
- Each transition carries `previousValue`, `value`, and bounded `reasonText` when the public API supplies a reason.
- Elimination and finish transition values are `None`, `Revokable`, or `Permanent`.
- Duplicate calls and invalid transitions that no-op emit diagnostics, not transition messages.
- Permanent elimination is represented for future phone death screens, personal death sounds, and player-facing elimination UI. No v1 input suppression is implied.

## Diagnostics Message

Structured GC diagnostics and captured Unity log records are carried as `gc.diagnostic` runtime messages.

- The diagnostic payload uses the stable contract from `05-diagnostics-spine.md`.
- Diagnostic timing uses the enclosing message `sequence` and `runtimeTimeMs`.
- Receiver wall-clock time is ingest metadata, not runtime truth.
- Unity-originated diagnostics may be disabled at boot for profiling, but receiver-side validation failures may still be logged by the receiver.
- Optional non-GC Unity log capture remains development-only, externally launch-controlled, and off by default. Warning/error capture is the normal development mode; full normal-log capture is available only by explicit launch policy and must stay filtered, rate-limited, and bounded.
- Captured Unity logs do not natively contain `sequence` or `runtimeTimeMs`. The Unity package stamps captured log records with the same active-run clock and sequence source used by other runtime messages before emitting them.
- Raw WebGL loader `print`/`printErr` output and browser console records are host-owned debug output. They are not assumed to have runtime-relative timing and are not merged into the canonical runtime message sequence unless a host explicitly wraps and stamps them as a supported diagnostic input.
- Diagnostics are runtime messages, not transition messages. Do not encode player state changes as diagnostics.

## Effectful Terminal Placement Message

`gc.game.terminal_placement_submitted` is the first v1 effectful runtime message.

Payload is the accepted v1 `GameOverResult` object:

```json
{
  "playerIndicesByPlacement": [0, 1]
}
```

Rules:

- `playerIndicesByPlacement` is required.
- Every active `playerIndex` must appear exactly once.
- `playerIndex: 0` is valid.
- Missing, duplicate, non-integer, negative, out-of-range, extra, or unknown placement values reject the effectful message and do not publish platform result state.
- V1 represents a total placement order only. It does not represent ties, DNF, teams, abandoned rounds, no-contest outcomes, score snapshots, or structured result reasons.
- Receiver-side accept/reject semantics are required. Accepted messages update platform-owned result state once. Rejected messages do not update result state and emit diagnostics.
- No Unity-facing acknowledgement response is required in v1. A future bidirectional acknowledgement contract is reserved.
- Pending state snapshot output must be flushed before, or in the same ordered batch immediately before, terminal placement submission.
- The legacy bare array bridge remains array-shaped and is interpreted only as `playerIdsByPlacement` for already-built older Unity games.

## Screen Space Path

Unity emits `screen_space` batches for latest-state screen-coordinate anchors:

```json
{
  "type": "screen_space",
  "schemaVersion": 1,
  "frameIndex": 2048,
  "runtimeTimeMs": 1240,
  "anchors": [
    {
      "anchorType": "playerOverhead",
      "playerIndex": 0,
      "x": 0.42,
      "y": 0.78,
      "isOffScreen": false
    },
    {
      "anchorType": "playerPosition",
      "playerIndex": 0,
      "x": 0.44,
      "y": 1,
      "isOffScreen": true
    }
  ]
}
```

- `screen_space` replaces HUD-specific screen-point language.
- V1 anchor types are `playerOverhead` and `playerPosition`.
- Deprecated name-tag behavior is a migration concern, not a first-class v1 anchor type.
- Anchors use `playerIndex`, not `playerId`.
- `x` and `y` are normalized screen coordinates.
- `isOffScreen` is required for every anchor.
- Individual anchors do not carry timestamps. The batch carries `frameIndex` and `runtimeTimeMs`.
- The latest batch replaces the prior screen-space state for that frame/run. Absence from the latest batch means the anchor is not visible right now.
- Screen-space sampling should happen late enough to reflect final camera/object positions for the rendered frame.
- `screen_space` can be disabled at boot for profiling.
- `screen_space` is view-derived presentation data, not durable semantic game truth.

## Boot-Time Output Configuration

All useful runtime outputs default on:

- transition messages
- state snapshots
- GC diagnostics
- `screen_space`
- effectful messages

Boot-time profiling configuration may disable runtime/package-owned optional outputs:

- state snapshots
- Unity-originated diagnostics
- Unity log capture
- `screen_space`

Host-owned debug output has separate launch controls and is not part of the Unity runtime contract:

- WebGL loader stdout/stderr mirroring through `createUnityInstance` `print`/`printErr`
- browser console monkey-patch or DevTools capture

Those host controls do not need to reach Unity runtime unless the host wants to expose them as status metadata. They are not gated by Unity log capture.

Transition messages remain enabled by default when snapshots are disabled. Effectful messages cannot be disabled by profiling config because they carry platform-state submissions.

## Reserved Result Growth

Richer terminal result semantics are reserved for future effectful runtime messages after a general result/versioning policy is chosen.

Reserved future concepts:

- `resultKind`: `placements`, `no_contest`, or `abandoned`.
- `placements[]`: ordered placement groups with `rank`, `playerIndices[]`, optional `outcome`, optional `reason`, and optional score summary.
- `teams[]`: team id plus player indices for team-based results.
- `scoreSnapshots[]`: bounded per-player final score/lives/status/meter snapshots.
- `reason`: stable result-level reason code plus bounded developer text.
- `metadata`: bounded platform-owned extension object, not a game-defined arbitrary data bag.

V1 sinks must reject these future fields if they appear in the v1 terminal placement payload. Do not silently ignore richer result data because that would make developer validation misleading.

## Platform-To-Runtime Ingress Reservation

No platform-to-runtime message implementation is included in this slice, but reserve this future shape:

```json
{
  "type": "platform_runtime_messages",
  "schemaVersion": 1,
  "messages": [
    {
      "schemaVersion": 1,
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
- `sentAt` is platform send time, not Unity runtime-relative time.
- Unknown `messageType` values are ignored by Unity and diagnosed when diagnostics are available.
- Future capability negotiation should use explicit capability names such as `runtime_messages.v1` and `platform_runtime_messages.v1`; exact advertised capability transport is owned by rollout planning.
- No v1 Unity game code should depend on receiving platform-to-runtime messages.

## Ready When

- Runtime output has two physical paths: `runtime_messages` and `screen_space`.
- Runtime messages have strict validation, sequence ordering, runtime-relative timing, and active-run context resolution.
- Full state snapshots are emitted on play start, meaningful semantic changes, and before terminal placement submission unless disabled at boot.
- Transition catalog covers score, lives, status, meter, elimination state, and finish state changes.
- Diagnostics and captured Unity logs use the runtime message path while preserving the diagnostics spine contract.
- `screen_space` carries player-indexed overhead and player-position anchors as latest-state view data.
- Terminal placement submission accepts only the object-wrapped total placement payload and keeps the legacy bare array bridge separate.
- Runtime messages are not duplicated through Unity/browser console logging, and host-owned console mirroring is documented as non-contract debug output.
- Future death screens, personal sounds, victory sounds, stats, moderation, replay, telemetry, and AI support can consume v1 runtime messages or clearly require future message types.

## Test Scenarios

- `playerIndex: 0` appears in state snapshots, transition messages, screen-space anchors, diagnostics, and terminal placement submissions.
- Active-run roster carries player type and color; dynamic snapshots do not repeat type or color.
- A score, lives, status, or meter change emits a transition message and one coalesced state snapshot.
- A permanent elimination state call emits one `gc.player.elimination_state_changed` message with `value: "Permanent"`.
- A duplicate permanent elimination call emits a state diagnostic and no second transition message.
- With snapshots disabled, transition messages and terminal placement submission still emit.
- `screen_space` emits separate `playerOverhead` and `playerPosition` anchors for the same player.
- `screen_space` can be disabled at boot without disabling terminal placement submission.
- Terminal placement with every active player index exactly once is accepted and updates platform result state.
- Terminal placement with a missing, duplicate, or out-of-range index is rejected and does not publish platform result state.
- A malformed runtime message is rejected and diagnosed.
- A captured Unity warning record is emitted as `gc.diagnostic` with active-run `sequence` and `runtimeTimeMs` when Unity log capture is enabled.
- With Unity log capture disabled, third-party `Debug.LogWarning` output does not enter `runtime_messages`.
- Normal third-party `Debug.Log` output enters `runtime_messages` only when full-log capture is explicitly enabled by launch policy.
- Toggling WebGL loader `print`/`printErr` mirroring does not enable or disable Unity runtime log capture.
- A v1 terminal placement payload containing reserved future result fields is rejected rather than partially accepted.
- A legacy bare terminal placement array is accepted only through the temporary bridge and diagnosed as `gc.api.legacy_runtime_payload`.
