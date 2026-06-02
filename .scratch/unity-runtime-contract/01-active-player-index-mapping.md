## Active Player Index Mapping Plan

Status: Core contract decisions captured; implementation readiness depends on diagnostics code naming and rollout planning.

## Purpose

Define the exact mapping contract between platform-owned player identity, DevApp seats, and Unity game-facing `playerIndex` values. This plan must be implementation-ready before setup, play, input, HUD, placement, or game-over payloads are migrated.

## Mapping Contract

- Each Unity game run gets one **Active Player Mapping**. Games have a single run lifecycle; do not introduce a separate Unity round mapping lifecycle because the game restarts between plays.
- The mapping is captured at play payload construction, after setup has finalized the active participants and immediately before Unity receives play data.
- The mapping is frozen until restart or the next play run. Active player count does not change during a running Unity game.
- Active Player Indices are zero-based, dense, and run-scoped. The same `playerIndex` may map to a different platform player or DevApp seat after restart.
- Unity game-facing APIs receive `playerIndex`, player type, and player color only. Platform player IDs, player names, source seats, and DevApp routing details stay outside Unity runtime game code.
- Bots are Active Players. They enter the same captured roster and deterministic shuffle as human players; only player type differs.

## Mapping Owners

- Hosted WebGL: the SDK Unity adapter owns the mapping at the Unity platform boundary. It translates platform `playerId` to Unity `playerIndex` for inputs and translates Unity `playerIndex` back to platform `playerId` for HUD, events, diagnostics, and game-over.
- Unity Editor local play: the Unity package local-play capture/runtime seam owns the mapping from enabled `gc.dev.json` seats to game-facing `playerIndex` values.
- DevApp keeps one-based `seatIndex` as the controller routing and display concept. Any message crossing into Unity game-facing runtime behavior must use the captured mapping rather than inferring seat or player identity in multiple places.

## Deterministic Shuffle

- Shuffle is part of the initial contract, not a future-only seam. This prevents games from accidentally relying on stable roster order or giving `playerIndex: 0` persistent advantage.
- Shuffle the participant records, not a list of IDs. Color and type move with the participant through shuffle.
- Use hash-sort for deterministic cross-language parity:
  - Hash input: `"{seed}:{participantStableKey}"`.
  - Hash algorithm: FNV-1a 32-bit.
  - Sort ascending by hash.
  - Tie-break by captured roster order.
  - Assign `playerIndex` from `0` after sorting.
- Hosted participant stable key is platform `playerId`.
- Local participant stable key is one-based source `seatIndex`.
- Never derive player color from `playerIndex`. If Seat 3 is green and shuffle assigns it to `playerIndex: 0`, Unity sees `playerIndex: 0` with green.

## Boundary Validation

- All mapping translation happens through one run-scoped mapping object per adapter. Input, HUD, event, diagnostics, and game-over paths must not each reimplement mapping rules.
- Platform-side input from unmapped post-play participants is dropped at the adapter boundary. The mapping is not mutated during the run.
- Unity-originated invalid `playerIndex` references emit structured diagnostics.
- HUD updates and future runtime events may include subsets of players, but every referenced `playerIndex` must exist in the current mapping.
- Game-over placement payloads must include every active `playerIndex` exactly once. Missing, duplicate, or out-of-range indices reject the result and do not publish platform results.
- Mapping diagnostics include run-scoped mapping context: `mappingId`, seed, participant count, and the offending reference where applicable.
- Repeated mapping diagnostics are deduped or rate-limited by diagnostics policy. The diagnostics spine plan owns exact codes, timing, and retention behavior.

## Ready When

- The mapping lifecycle is written as a contract.
- Hosted Unity, DevApp local play, Unity Editor play, HUD, input, and game-over all share the same mapping rules.
- Deterministic shuffle has cross-language fixtures for TypeScript and C#.
- Tests cover sparse seats, bots, reconnect/skew behavior, color preservation after shuffle, `playerIndex: 0`, out-of-range indices, HUD mapping, and game-over mapping.

## Test Scenarios

- Sparse local seats such as `1, 3, 8` become dense zero-based Active Player Indices after deterministic shuffle while preserving source seat identity for DevApp routing.
- `playerIndex: 0` is exercised in play payloads, input routing, HUD updates, runtime events, diagnostics, and game-over.
- A shuffled participant keeps its original color and type, including when it becomes `playerIndex: 0`.
- Hosted input maps platform `playerId` to Unity `playerIndex` through the run mapping.
- Hosted HUD, events, diagnostics, and game-over map Unity `playerIndex` back to platform `playerId` through the same run mapping.
- DevApp displays controller seats in one-based seat order while routing active inputs through the seat-to-index mapping.
- Bots are shuffled and indexed exactly like humans.
- Reconnects preserve the mapped `playerIndex`; post-play joins or unmapped inputs do not remap the running game.
- Invalid HUD/event indices are diagnosed and ignored or rejected according to payload type.
- Invalid game-over placements are diagnosed and do not publish platform results.

## Remaining Dependencies

- Diagnostics spine must define stable mapping diagnostic codes, severity, dedupe/rate-limit behavior, and DevApp/hosted display rules.
- Cross-repo rollout must decide whether the mapping wire changes require a `gameProtocolVersion` bump, internal-game migration, compatibility adapter, or staged combination.
- JavaScript game runtime migration should be planned as the next tightly coupled follow-up so JS and Unity do not keep divergent identity vocabulary.
