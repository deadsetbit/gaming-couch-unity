## Diagnostics Spine Plan

Status: Core contract decisions captured; implementation-ready for the diagnostics spine. Cross-repo rollout still owns branch and release ordering.

## Purpose

Define the structured diagnostics path before API migration and state-model work begin. Removed or hard-obsolete API guidance, invalid state warnings, mapping validation failures, metadata fallback warnings, unsupported API warnings, and optional Unity log capture should all use this spine from the start. Runtime diagnostics must not depend on `Debug.Log`, WebGL loader output, or browser console history.

## Stable Contract

- Diagnostics are a stable public core contract from day one for `code`, `severity`, `sourceArea`, runtime-relative timing, and context shape.
- Human `message` text is not contractual. `details` and `debug` may receive additive fields over time.
- The diagnostic envelope is named `RuntimeDiagnostic`.
- `code` is a lowercase namespaced string such as `gc.api.removed_identity_api` or `gc.mapping.invalid_player_index`.
- `severity` is one of `info`, `warning`, or `error`, based on behavior impact:
  - `error`: runtime output was rejected or blocked.
  - `warning`: behavior degraded, no-oped, fell back, hit a legacy bridge, or used unsupported API.
  - `info`: notable non-problem diagnostic state.
- `sourceArea` is one of `api`, `mapping`, `state`, `runtime_messages`, `screen_space`, `metadata`, or `unity_log`.
- Diagnostic timing uses the enclosing `runtime_messages` record `sequence` and unscaled `runtimeTimeMs`.
- `runId` is active-run context metadata resolved by the receiver or adapter. In DevApp local play it identifies the active runtime play run and is not stable across restart.
- `playerIndex` is optional but top-level when a diagnostic is about a game-facing active player. Public diagnostics must not expose platform player IDs.
- Hosted adapters may correlate diagnostics to platform player IDs in private adapter state after validation, but `RuntimeDiagnostic`, `details`, `debug`, DevApp UI, and hosted public diagnostics callbacks must not include platform player ID fields.
- Mapping diagnostics may include an optional `mapping` object with bounded run-scoped context such as `mappingId`, seed, participant count, and the offending reference.
- `details` is flat, bounded, fingerprint-friendly data: primitive values, bounded strings, and small primitive arrays.
- `debug` is optional, bounded, non-fingerprinted troubleshooting data, collapsed by default in UI. Use it for evidence such as stack traces, raw message previews, or notes.
- Captured Unity logs do not natively include runtime-message sequencing. When capture is enabled, the Unity package stamps each captured log with the active-run runtime clock and sequence before emitting it as a `gc.diagnostic` runtime message.

## Code Taxonomy

- Use one stable code per meaningful developer-facing condition. Do not create separate codes for every invalid field or schema path; put field/path specifics in `details`.
- Initial v1 code catalog:
  - `gc.api.removed_identity_api`
  - `gc.api.removed_player_name_api`
  - `gc.api.removed_state_api`
  - `gc.api.removed_store_api`
  - `gc.api.unsupported_multiplayer_api`
  - `gc.api.legacy_runtime_payload`
  - `gc.mapping.invalid_player_index`
  - `gc.mapping.unmapped_participant`
  - `gc.state.duplicate_elimination`
  - `gc.state.duplicate_finish`
  - `gc.state.invalid_revoke`
  - `gc.state.invalid_transition`
  - `gc.state.post_game_over_mutation`
  - `gc.state.clamped_value`
  - `gc.runtime.malformed_message`
  - `gc.runtime.unknown_message`
  - `gc.runtime.invalid_game_over_placement`
  - `gc.runtime.malformed_screen_space`
  - `gc.metadata.missing_platform_data`
  - `gc.metadata.invalid_platform_data`
  - `gc.metadata.fallback_active`
  - `gc.log.unity_log`
  - `gc.log.unity_warning`
  - `gc.log.unity_error`
- Runtime message envelope, message catalog, and game-over placement validation diagnostics use `sourceArea: runtime_messages`.
- Screen-space envelope and anchor validation diagnostics, including `gc.runtime.malformed_screen_space`, use `sourceArea: screen_space`.

## Emitters And Sinks

- The Unity package exposes an internal diagnostics emitter for package code. Do not add a public game-developer custom diagnostic API in v1.
- GC diagnostics must be emitted independently of `GCLog.logLevel`.
- GC diagnostic warnings and errors always mirror to the Unity console. `info` diagnostics are structured-only in v1.
- Unity console mirroring is a human-facing copy of structured GC diagnostics, not the transport. Do not emit GC diagnostics by first writing `Debug.LogWarning` or `Debug.LogError` and then scraping Unity/browser logs.
- Source-level hard breaks should prefer compiler guidance through `[Obsolete(..., true)]` or removal errors. The runtime diagnostic codes for removed APIs are used only where a retained stub or temporary bridge can still execute.
- Temporary legacy bridge diagnostics, such as accepting an old array-shaped game-over payload, use `gc.api.legacy_runtime_payload` and include the legacy field or payload kind in bounded `details`.
- Unity runtime emits diagnostics as `gc.diagnostic` records inside `runtime_messages` batches.
- Runtime message batch flushing follows the timing rules in `04-runtime-output-contract.md`.
- DevApp validates diagnostic records during `runtime_messages` ingress. A malformed diagnostic record is rejected and recorded/logged as an ingress validation warning, but it does not close the runtime connection.
- Hosted WebGL receives structured diagnostics through a validated callback path before hosted diagnostics UI exists. The host may store, forward, or ignore diagnostics until product UI is added.
- Future uploaded-game validation can reuse the same envelope and code taxonomy.

## Launch-Owned Capture Policy

- Runtime diagnostics, optional Unity log capture, WebGL loader mirroring, and browser console capture are separate launch concerns.
- `runtime_messages` is the only contract path for GC diagnostics and captured Unity log records.
- Unity log capture is package/runtime-owned after startup configuration is read. Launch policy may choose `off`, exception/error-only, warning-and-error, or explicit full-log capture.
- WebGL loader `print`/`printErr` mirroring is HTML/JavaScript host-owned and does not need to reach Unity runtime unless the host reports it as status metadata.
- Browser console capture is HTML/JavaScript host-owned and is not gated by Unity log capture.
- Slow-device launch profiles may disable Unity log capture, WebGL loader mirroring, browser console capture, state snapshots, `screen_space`, and optional Unity-originated informational diagnostics. Core GC validation diagnostics for rejected runtime messages, invalid game-over placement payloads, invalid mapping references, metadata fallback, unsupported retained APIs, and state no-op warnings must remain enabled. Effectful platform result messages must remain enabled.
- Raw browser console history is not treated as a durable or coherently timestamped diagnostics source.

## Retention And Aggregation

- Deduplication and rate limiting happen at the sink, not the Unity emitter.
- DevApp aggregates repeats immediately at ingress before app state or UI updates.
- Repeat fingerprints are sink-computed from stable fields: `code`, `sourceArea`, active-run context, `playerIndex`, `mapping.mappingId`, and stable reason/path-like `details` fields.
- Fingerprints exclude `message`, `sequence`, `runtimeTimeMs`, received wall-clock time, occurrence counts, changing offending values, and all `debug` content.
- DevApp displays diagnostics only for the active `runId`. Stale, missing-run, or non-active-run diagnostics are ignored for display and counted internally for observability.
- DevApp keeps a 200-row active-run ring buffer of distinct diagnostic fingerprints.
- When the 200-row cap is reached, a new distinct diagnostic evicts the oldest distinct row. Repeated diagnostics update their existing row and do not consume new rows.
- Each aggregated row tracks occurrence count, first-seen time, and last-seen time.
- The active-run diagnostics store resets on run change or stop. A manual clear removes currently visible active-run diagnostics.

## DevApp Console

- The runtime diagnostics console lives inside the existing hidden Diagnostics right rail, controlled by the current Settings menu "Show diagnostics panel" toggle.
- The diagnostics right rail remains hidden by default.
- The console shows newest distinct diagnostics first.
- Rows show severity and source badges, code, message, occurrence count, last-seen time, and expandable `details` and `debug`.
- Controls include severity filters, source-area filters, text search over code/message, and manual clear.
- Use `@tanstack/react-virtual` for the diagnostics list.
- Batch diagnostics UI state flushes to at most every 250 ms, with immediate first diagnostic visibility for a new active run.

## Unity Log Capture

- Non-GC Unity log capture is optional, development-only, externally controlled at launch, and off by default.
- Launch control applies to the next run only. Changing the setting during a run does not alter capture until restart.
- DevApp exposes the launch-only capture toggle in the Diagnostics right rail controls.
- The normal development mode captures Unity `Warning`, `Error`, `Assert`, and `Exception` log types.
- Full-log capture may include normal Unity `Log` output only when explicitly requested by launch policy, and must stay filtered, rate-limited, and bounded.
- Map Unity `Log` to diagnostic severity `info`.
- Map Unity `Warning` to diagnostic severity `warning`.
- Map Unity `Error`, `Assert`, and `Exception` to diagnostic severity `error`.
- Captured non-GC Unity logs use `sourceArea: unity_log` and codes `gc.log.unity_log`, `gc.log.unity_warning`, or `gc.log.unity_error`.
- Unity-specific fields such as Unity log type and bounded stack trace belong in `details` or `debug`.
- Install v1 capture with a startup hook before scene `Awake` where practical, then use main-thread Unity log callbacks for capture. Multi-threaded capture is out of scope for v1; if it is added later, the threaded callback must only enqueue thread-safe data and a main-thread drain must emit diagnostics.
- The package may attempt to set Unity logger filtering to the requested launch level, but third-party logs remain best-effort: developer code can disable or compile out log emission. Critical GC diagnostics must bypass Unity logging entirely.

## Versioning And Rollout

- Do not bump `gameProtocolVersion` for the diagnostics spine.
- The DevApp runtime ingress shape may add diagnostic records inside `runtime_messages` without old-DevApp compatibility shims because DevApp is not released yet.
- Cross-repo rollout still owns branch order and validation across Unity package, DevApp, SDK, and hosted client code.

## Ready When

- Unity package has an internal diagnostics emitter and all GC diagnostic warnings/errors bypass package log level.
- Removed/hard-obsolete identity, name, state, and store APIs have source guidance; any retained stubs or temporary bridges emit planned diagnostics. Invalid mapping references, invalid state transitions, metadata fallback, unsupported multiplayer APIs, malformed runtime messages, invalid game-over placement payloads, malformed screen-space anchors, and captured Unity logs emit planned codes.
- DevApp accepts diagnostic records through `runtime_messages`, rejects malformed diagnostics without disconnecting the runtime, aggregates by fingerprint, keeps a 200-row active-run ring buffer, and renders the hidden-by-default virtualized console.
- Hosted WebGL has a validated structured diagnostics callback path with no hosted UI requirement.
- Tests cover log-level bypass, Unity console mirroring, captured-log runtime timestamp stamping, end-of-frame batching, sink aggregation, ring-buffer eviction, active-run filtering, DevApp ingress validation, hidden diagnostics rail behavior, UI batching, hosted callback validation, launch-only Unity log capture filtering, and separation between Unity log capture and host-owned console mirroring.

## Remaining Dependencies

- Runtime output planning owns the runtime message catalog and strict payload schemas.
- Active Player Index mapping owns exact mapping object construction, `mappingId`, and invalid index contexts.
- Cross-repo rollout owns branch order, release sequencing, and skew validation.
