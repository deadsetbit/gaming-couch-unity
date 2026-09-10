# Object-Wrapped Game-Over Result

Legacy Unity builds ended a game by emitting a bare array that receivers interpret as platform player IDs, so a bare array of the new zero-based player indices would be shape-identical and ambiguous — `playerIndex: 0` cannot be told apart from old positive platform IDs. The game-over result is therefore an object payload, `{ "playersByPlacement": [<player indices in placement order>] }`, emitted as `gc.game.game_over` on the `runtime_messages` path; the wrapper makes new results structurally distinct from the legacy bare-array bridge and leaves room to add result fields later without a version field. The first valid placement per active run wins: it flushes pending transitions and a final state snapshot ahead of the game-over record in one envelope, and per the cross-repo contract the accepted result freezes platform-owned result state for that run.

## Consequences

- Every later submission after acceptance — even byte-identical or re-entrant during the accept callback — is rejected and diagnosed as `gc.runtime.invalid_game_over_placement`; rejected submissions before any acceptance do not consume the slot (`Runtime/RuntimeMessages/GCRuntimeMessages.cs`, `Tests/Editor/GCRuntimeOutputContractTests.cs`).
- The payload must be a permutation of all active player indices; invalid placements are rejected with a diagnostic instead of being emitted.
- Per the cross-repo contract, the legacy bare array remains a hosted client/SDK migration-only bridge interpreted as platform IDs and must not receive new result semantics.
