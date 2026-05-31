## Runtime Events And Results Plan

Status: Planning required before implementation.

## Purpose

Define a strict runtime-to-platform event contract and a future-proof game result schema. Reserve platform-to-runtime ingress shape without implementing inbound behavior in this slice.

## Carry-Forward Decisions

- Runtime player state is the canonical source for elimination and finish facts.
- HUD should be treated as a configurable projection of runtime state, not the long-term source of player state truth.
- Runtime event vocabulary should reuse the `GCPlayer` state model from `03-player-state-model.md`: elimination state and finish state are each `None`, `Revokable`, or `Permanent`.
- The immediate game-over migration uses an object-wrapped result payload instead of a bare array. Initial shape is `GameOverResult { schemaVersion: 1, playerIndicesByPlacement: int[] }`; this plan owns how that object grows into a richer result schema.
- The legacy bare array bridge is migration-only and must not receive new result semantics.
- Capability planning should leave room for capability versioning, either through versioned capability names or explicit version fields. Exact shape remains a planning decision here.

## Decisions To Make

- Event envelope: event name/type, schema version, event id, sequence, timestamp source, run/round context, mapping context, and payload.
- Event catalog: initial player state events, game lifecycle events, result events, and diagnostics relationship.
- Result schema: whether to evolve from `playerIndicesByPlacement` to object placements, plus ties, teams, DNF, no-contest, abandoned rounds, score snapshots, result reasons, and per-player summaries.
- Reason codes: which events/results need stable machine-readable reasons in addition to developer strings.
- Actor/target model: how events represent the player affected, the actor that caused it, or no actor.
- Validation: strict rejection, warning, or ignore behavior for malformed or unknown events.
- Platform-to-runtime ingress reservation: envelope, lifecycle timing, unknown-message behavior, reliability expectation, and capability negotiation placeholder.

## Open Questions

- Are runtime events ordered relative to HUD updates and game-over messages?
- Do events need reliable delivery, replay on reconnect, or best-effort semantics?
- Should richer result events replace the initial object-wrapped game-over payload, extend it in place, or coexist during migration?
- How much extension data should be allowed without becoming an untyped custom-event channel?

## Ready When

- Event envelope and result schema are written with examples.
- Initial catalog includes terminal elimination and game result events.
- Future death screens, personal sounds, victory sounds, stats, moderation, and replay/telemetry can be built from the reserved data without changing Unity games.
- Tests cover valid events, malformed events, unknown events, result edge cases, and adapter mapping to platform IDs.
