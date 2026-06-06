## Runtime Output Contract Plan

Status: Core contract decisions captured; implementation-ready for the staged rollout plan in `07-cross-repo-rollout.md`.

## Purpose

Define the Unity runtime output contract for semantic runtime messages, screen-space data, diagnostics, and effectful platform result messages. Replace HUD-owned data and passive event framing with a typed runtime output model that support tooling, DevApp, hosted adapters, HUD rendering, and future AI/debugging flows can consume.

Reserve platform-to-runtime ingress shape without implementing inbound behavior in this slice.

## Carry-Forward Decisions

- Runtime player/game state is the canonical source for player facts such as score, lives, status, meter, placement, elimination state, and finish state.
- HUD is a consumer of runtime state, screen-space anchors, and HUD configuration. HUD is not the semantic data model.
- Unity has two physical runtime output paths in v1:
  - `runtime_messages` for semantic state, transitions, diagnostics, and effectful platform-state result messages.
  - `screen_space` for hot latest-state screen-coordinate anchors.
- Host-owned WebGL loader output and browser console mirroring are not runtime output paths. They may be useful for local debugging, but `runtime_messages` and `screen_space` are the only v1 Unity runtime contract outputs.
- Static active-player facts live in the active-run roster/setup context: `playerIndex`, player type, and player color. Dynamic state snapshots reference `playerIndex` and do not repeat type or color.
- Runtime message vocabulary reuses the `GCPlayer` state model from `03-player-state-model.md`: elimination state and finish state are each `None`, `Revokable`, or `Permanent`.
- Runtime messages are not a public custom message API in v1. The Unity package emits cataloged messages from supported GC runtime APIs only.
- The Unity package must not mirror runtime messages through `Debug.Log` or browser console output. Logging runtime messages would double the runtime output work and make console text look like a contract source.
- Game over is the first v1 effectful runtime message. Future `gc.game.game_over` payloads may carry additional platform-owned game-over facts, but v1 only accepts total placement order.
- The immediate game-over migration uses an object-wrapped result payload instead of a bare array. Initial shape is `GameOverResult { playerIndicesByPlacement: int[] }`.
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
- Payloads must be bounded, but arbitrary numeric caps such as total JSON bytes, messages per batch, string lengths, and debug preview sizes are implementation defaults, not PRD-level compatibility commitments in this planning slice. Task 7 and adapter implementation must choose, document, and test concrete ingress limits before release.
- A malformed batch envelope is rejected as a whole and diagnosed.
- A valid batch with one malformed non-effectful message keeps valid messages and rejects only the malformed message.
- Unknown non-effectful `messageType` values are ignored and diagnosed. Unknown effectful message types are not applied.
- Sinks validate exact per-type schema, field types, string bounds, array bounds, known catalog membership, `playerIndex` mapping, unknown fields, and prototype-pollution keys before storing or forwarding messages.
- Unity emitters should produce bounded, well-formed payloads and may clamp or truncate informational fields, but DevApp/client/hosted ingress is authoritative because games and emitted payloads cannot be trusted.
- Public runtime messages and diagnostics never include platform player IDs. Hosted adapters may correlate `playerIndex` to platform player IDs in private adapter state after validation.

Common `runtime_messages` envelope schema:

| Field | Required | Type/bounds | Notes |
| --- | --- | --- | --- |
| `type` | Yes | exact string `runtime_messages` | Physical runtime output path. |
| `schemaVersion` | Yes | integer `1` | Envelope schema version. |
| `runId` | Transport-dependent | non-empty bounded string | Only for transports that are not strictly scoped to one active run; exact max length is an ingress implementation limit. |
| `messages` | Yes | bounded non-empty array | Each item is validated independently after the envelope is accepted; exact max count is an ingress implementation limit. |

Common runtime message record schema:

| Field | Required | Type/bounds | Notes |
| --- | --- | --- | --- |
| `schemaVersion` | Yes | integer `1` | Message record schema version. |
| `messageType` | Yes | known v1 message type string | Unknown non-effectful types are ignored and diagnosed; unknown effectful types are not applied. |
| `sequence` | Yes | integer, `>= 1` | One-based and monotonic per active run. |
| `runtimeTimeMs` | Yes | integer, `>= 0` | Unscaled milliseconds since active run start. |
| `payload` | Yes | object | Exact schema depends on `messageType`; unknown fields reject that message. |

All integer fields are JSON numbers that must be finite integers. Unless a field has a narrower bound, sinks must reject values outside the JavaScript safe integer range.

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
- Snapshots emit on play start, meaningful semantic state changes, and before game over. Unity flushes pending state snapshot output before, or in the same ordered batch immediately before, the game-over message; receiver acceptance happens after validation.
- Snapshot emission is change-driven and coalesced. Do not emit every frame unless a semantic state value changes every frame.
- Snapshot payloads include all active players exactly once.
- Player type and color are read from the active-run roster/setup context, not repeated in each dynamic snapshot.
- Score, lives, status, status text, meter, placement, elimination state, finish state, and game status are v1 canonical semantic fields.
- Arbitrary game-defined custom data is out of scope for v1.
- State snapshots may be disabled at boot for profiling. Transition messages and effectful messages remain enabled by default.

`gc.state.snapshot` payload schema:

| Field | Required | Type/bounds | Notes |
| --- | --- | --- | --- |
| `game` | Yes | object | Exact fields below. |
| `game.status` | Yes | enum `pending_setup`, `setup_done`, `playing`, `game_over` | Normalized runtime game status. |
| `players` | Yes | array length equals active-player count | Contains every active `playerIndex` exactly once. |
| `players[].playerIndex` | Yes | integer `0..activePlayerCount - 1` | Game-facing active-player index. |
| `players[].score` | Yes | integer | Current score. |
| `players[].lives` | Yes | integer `>= 0` | Current lives after clamping. |
| `players[].status` | Yes | enum `Neutral`, `Pending`, `Success`, `Failure`, `Warning`, `Alert` | Mirrors `GCPlayerStatus`. |
| `players[].statusText` | Yes | bounded string | Empty string is allowed; exact max length is an ingress implementation limit. |
| `players[].meter` | Yes | integer `-1..100` | `-1` means no meter value. |
| `players[].placement` | Yes | integer `1..activePlayerCount` | One-based current placement rank from configured broad placement order. |
| `players[].eliminationState` | Yes | enum `None`, `Revokable`, `Permanent` | Uses the state model vocabulary. |
| `players[].finishState` | Yes | enum `None`, `Revokable`, `Permanent` | Uses the state model vocabulary. |

## Transition Message Catalog V1

Transition messages explain supported semantic changes. Full state snapshots remain the source for current truth.

Initial player transition messages:

- `gc.player.score_changed`
- `gc.player.lives_changed`
- `gc.player.status_changed`
- `gc.player.meter_changed`
- `gc.player.elimination_state_changed`
- `gc.player.finish_state_changed`

Common transition payload rules:

- `playerIndex` is required.
- Each transition carries `previousValue`, `value`, and bounded `reasonText` when the public API supplies a reason.
- Elimination and finish transition values are `None`, `Revokable`, or `Permanent`.
- Duplicate calls and invalid transitions that no-op emit diagnostics, not transition messages.
- Permanent elimination is represented for future phone death screens, personal death sounds, and player-facing elimination UI. No v1 input suppression is implied.

Transition payload schemas:

| Message type | `previousValue` / `value` type | Additional fields |
| --- | --- | --- |
| `gc.player.score_changed` | integer | `playerIndex`; optional bounded `reasonText` |
| `gc.player.lives_changed` | integer `>= 0` | `playerIndex`; optional bounded `reasonText` |
| `gc.player.status_changed` | object `{ "status": GCPlayerStatus enum, "statusText": bounded string }` | `playerIndex`; optional bounded `reasonText` |
| `gc.player.meter_changed` | integer `-1..100` | `playerIndex`; optional bounded `reasonText` |
| `gc.player.elimination_state_changed` | enum `None`, `Revokable`, `Permanent` | `playerIndex`; optional bounded `reasonText` |
| `gc.player.finish_state_changed` | enum `None`, `Revokable`, `Permanent` | `playerIndex`; optional bounded `reasonText` |

Every transition payload rejects unknown fields. `reasonText` is omitted when no public API reason exists; it is not emitted as `null`.

## Diagnostics Message

Structured GC diagnostics and captured Unity log records are carried as `gc.diagnostic` runtime messages.

- The diagnostic payload uses the stable contract from `05-diagnostics-spine.md`.
- Diagnostic timing uses the enclosing message `sequence` and `runtimeTimeMs`.
- Receiver wall-clock time is ingest metadata, not runtime truth.
- Optional Unity-originated informational diagnostics may be disabled at boot for profiling, but core GC validation diagnostics remain enabled and receiver-side validation failures may still be logged by the receiver.
- Optional non-GC Unity log capture remains development-only, externally launch-controlled, and off by default. Warning/error capture is the normal development mode; full normal-log capture is available only by explicit launch policy and must stay filtered, rate-limited, and bounded.
- Captured Unity logs do not natively contain `sequence` or `runtimeTimeMs`. The Unity package stamps captured log records with the same active-run clock and sequence source used by other runtime messages before emitting them.
- Raw WebGL loader `print`/`printErr` output and browser console records are host-owned debug output. They are not assumed to have runtime-relative timing and are not merged into the canonical runtime message sequence unless a host explicitly wraps and stamps them as a supported diagnostic input.
- Diagnostics are runtime messages, not transition messages. Do not encode player state changes as diagnostics.

`gc.diagnostic` payload schema summary:

| Field | Required | Type/bounds | Notes |
| --- | --- | --- | --- |
| `code` | Yes | known lowercase namespaced diagnostic code | Catalog lives in `05-diagnostics-spine.md`. |
| `severity` | Yes | enum `info`, `warning`, `error` | Based on behavior impact. |
| `sourceArea` | Yes | known source area | Includes `runtime_messages` and `screen_space` as distinct sources. |
| `message` | Yes | bounded string | Human text is not contractual; exact max length is an ingress implementation limit. |
| `playerIndex` | No | integer `0..activePlayerCount - 1` | Only when the diagnostic is about a game-facing active player. |
| `mapping` | No | object | Bounded run-scoped mapping context from the diagnostics spine. |
| `details` | No | flat object | Primitive values, bounded strings, and small primitive arrays only. |
| `debug` | No | bounded object | Non-fingerprinted troubleshooting evidence. |

## Effectful Game-Over Message

`gc.game.game_over` is the first v1 effectful runtime message.

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
- Unknown payload fields reject the effectful message. Reserved future result fields also reject in v1.
- V1 represents a total placement order only. It does not represent ties, DNF, teams, abandoned rounds, no-contest outcomes, score snapshots, or structured result reasons.
- Receiver-side accept/reject semantics are required. Accepted messages update platform-owned result state once. Rejected messages do not update result state and emit diagnostics.
- No Unity-facing acknowledgement response is required in v1. A future bidirectional acknowledgement contract is reserved.
- Pending state snapshot output must be flushed before, or in the same ordered batch immediately before, the game-over message.
- The legacy bare array bridge remains array-shaped and is interpreted only as `playerIdsByPlacement` for already-built older Unity games.

Game-over acceptance is first-accepted-wins per active run:

- The receiver tracks whether game over has already been accepted for the active run.
- The first valid game-over message updates platform-owned result state and freezes that result for the active run.
- Rejected messages before any accepted game over do not freeze the result; a later valid game-over message may still be accepted.
- Any later game-over message after the first accepted one is rejected and diagnosed with `gc.runtime.invalid_game_over_placement`, even if the later payload is byte-for-byte identical. V1 does not have a Unity acknowledgement or retry contract that would make identical duplicates idempotently accepted.
- If one ordered batch contains multiple game-over messages, process them by `sequence`: the first valid one may be accepted, and every later one is rejected after the result is frozen.
- Rejections for duplicate, replayed, or second game-over messages must not mutate playlist, stats, or platform result state again.

`gc.game.game_over` payload schema:

| Field | Required | Type/bounds | Notes |
| --- | --- | --- | --- |
| `playerIndicesByPlacement` | Yes | array length equals active-player count | Total one-based placement order encoded as zero-based `playerIndex` values. |
| `playerIndicesByPlacement[]` | Yes | integer `0..activePlayerCount - 1` | Each active player appears exactly once. |

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

`screen_space` envelope schema:

| Field | Required | Type/bounds | Notes |
| --- | --- | --- | --- |
| `type` | Yes | exact string `screen_space` | Physical runtime output path. |
| `schemaVersion` | Yes | integer `1` | Envelope schema version. |
| `runId` | Transport-dependent | non-empty bounded string | Only for transports that are not strictly scoped to one active run; exact max length is an ingress implementation limit. |
| `frameIndex` | Yes | integer `>= 0` | Monotonic rendered-frame index for the active run. |
| `runtimeTimeMs` | Yes | integer `>= 0` | Unscaled milliseconds since active run start for this batch. |
| `anchors` | Yes | array, max `2 * activePlayerCount` items | Empty array is allowed and means no anchors are visible in this latest batch. This bound is contractual because v1 has exactly two player anchor types. |

`screen_space` anchor schema:

| Field | Required | Type/bounds | Notes |
| --- | --- | --- | --- |
| `anchorType` | Yes | enum `playerOverhead`, `playerPosition` | V1 anchor catalog. |
| `playerIndex` | Yes | integer `0..activePlayerCount - 1` | Game-facing active-player index. |
| `x` | Yes | finite number `0..1` | Normalized and clamped horizontal screen coordinate. |
| `y` | Yes | finite number `0..1` | Normalized and clamped vertical screen coordinate. |
| `isOffScreen` | Yes | boolean | True when the source point is outside the camera view; `x` and `y` still carry the clamped normalized coordinate. |

Unknown `screen_space` envelope or anchor fields are rejected. A batch may include at most one anchor per `(anchorType, playerIndex)` pair; duplicates make the batch malformed and produce `gc.runtime.malformed_screen_space` with `sourceArea: screen_space`.

## Boot-Time Output Configuration

All useful runtime outputs default on:

- transition messages
- state snapshots
- GC diagnostics
- `screen_space`
- effectful messages

Boot-time profiling configuration may disable runtime/package-owned optional outputs:

- state snapshots
- optional Unity-originated informational diagnostics
- Unity log capture
- `screen_space`

Core GC validation diagnostics remain enabled even under profiling configuration. This includes diagnostics for rejected runtime messages, invalid game-over placement payloads, invalid mapping references, metadata fallback, unsupported retained APIs, and state no-op warnings.

Host-owned debug output has separate launch controls and is not part of the Unity runtime contract:

- WebGL loader stdout/stderr mirroring through `createUnityInstance` `print`/`printErr`
- browser console monkey-patch or DevTools capture

Those host controls do not need to reach Unity runtime unless the host wants to expose them as status metadata. They are not gated by Unity log capture.

Transition messages remain enabled by default when snapshots are disabled. Effectful messages cannot be disabled by profiling config because they carry platform-state results.

## Reserved Result Growth

Richer game-over result semantics are reserved for future `gc.game.game_over` payload evolution after a general result/versioning policy is chosen.

Reserved future concepts:

- `resultKind`: `placements`, `no_contest`, or `abandoned`.
- `placements[]`: ordered placement groups with `rank`, `playerIndices[]`, optional `outcome`, optional `reason`, and optional score summary.
- `teams[]`: team id plus player indices for team-based results.
- `scoreSnapshots[]`: bounded per-player final score/lives/status/meter snapshots.
- `reason`: stable result-level reason code plus bounded developer text.
- `metadata`: bounded platform-owned extension object, not a game-defined arbitrary data bag.

V1 sinks must reject these future fields if they appear in the v1 game-over payload. Do not silently ignore richer result data because that would make developer validation misleading.

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
- Full state snapshots are emitted on play start, meaningful semantic changes, and before game over unless disabled at boot.
- Transition catalog covers score, lives, status, meter, elimination state, and finish state changes.
- Diagnostics and captured Unity logs use the runtime message path while preserving the diagnostics spine contract.
- `screen_space` carries player-indexed overhead and player-position anchors as latest-state view data.
- `gc.game.game_over` accepts only the object-wrapped total placement payload and keeps the legacy bare array bridge separate.
- Runtime messages are not duplicated through Unity/browser console logging, and host-owned console mirroring is documented as non-contract debug output.
- Future death screens, personal sounds, victory sounds, stats, moderation, replay, telemetry, and AI support can consume v1 runtime messages or clearly require future message types.

## Test Scenarios

- `playerIndex: 0` appears in state snapshots, transition messages, screen-space anchors, diagnostics, and game-over placement payloads.
- Active-run roster carries player type and color; dynamic snapshots do not repeat type or color.
- A score, lives, status, or meter change emits a transition message and one coalesced state snapshot.
- A permanent elimination state call emits one `gc.player.elimination_state_changed` message with `value: "Permanent"`.
- A duplicate permanent elimination call emits a state diagnostic and no second transition message.
- With snapshots disabled, transition messages and the game-over message still emit.
- `screen_space` emits separate `playerOverhead` and `playerPosition` anchors for the same player.
- `screen_space` can be disabled at boot without disabling the game-over message.
- Game over with every active player index exactly once is accepted and updates platform result state.
- Game over with a missing, duplicate, or out-of-range placement index is rejected and does not publish platform result state.
- After one valid game-over message is accepted, any duplicate, replayed, or second game-over message is rejected and does not mutate platform result state again.
- A malformed runtime message is rejected and diagnosed.
- A captured Unity warning record is emitted as `gc.diagnostic` with active-run `sequence` and `runtimeTimeMs` when Unity log capture is enabled.
- With Unity log capture disabled, third-party `Debug.LogWarning` output does not enter `runtime_messages`.
- Normal third-party `Debug.Log` output enters `runtime_messages` only when full-log capture is explicitly enabled by launch policy.
- Toggling WebGL loader `print`/`printErr` mirroring does not enable or disable Unity runtime log capture.
- A v1 game-over payload containing reserved future result fields is rejected rather than partially accepted.
- A legacy bare game-over array is accepted only through the temporary bridge and diagnosed as `gc.api.legacy_runtime_payload`.
