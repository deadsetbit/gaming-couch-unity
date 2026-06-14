## Problem Statement

The Unity package still contains runtime compatibility behavior for legacy player identity paths that should live in the Gaming Couch client/SDK adapter layer instead. The package can still adapt legacy play payloads shaped around `players[]`, `playerId`, and player names into the current game-facing runtime contract. It also still accepts DevApp runtime input routed by legacy `playerId`, and a few obsolete source APIs remain warnings instead of compile-time errors.

This creates a blurry boundary. Unity package code is supposed to expose only current runtime identity to game code: captured players addressed by zero-based dense `playerIndex` values. Platform-owned player IDs, player names, and legacy hosted payload shapes should be handled before data enters the Unity package. Developers also need obsolete API failures to be direct and actionable, not warnings that allow old source usage to linger.

## Solution

Make the Unity package strict-current for legacy player identity cleanup.

The package will reject legacy inbound play payloads that provide `players[]` without current `activePlayers[]`, with a targeted error explaining that client/SDK must translate legacy payloads before calling Unity. DevApp/runtime input routing will stop resolving legacy `playerId` values inside the package; input must arrive as `playerIndex` or via supported local seat routing. Source-seat mapping for Local Play Settings remains supported because Seats are part of the current local play contract.

Obsolete source-level migration markers remain in the package so developers get compiler errors with fix guidance, but all remaining obsolete warnings become hard errors. Messages must tell developers what current API to use.

## User Stories

1. As a Unity game developer, I want legacy `playerId` payloads to fail clearly, so that I do not accidentally build against platform-owned identity.
2. As a Unity game developer, I want `GCPlayer.Index` and `playerIndex` to be the only game-facing player identity, so that my game logic is portable across hosted and local runs.
3. As a Unity game developer, I want obsolete APIs to be compile-time errors, so that migration issues are found before playtesting or release builds.
4. As a Unity game developer, I want obsolete API messages to name the replacement API, so that I can fix my code without reading platform internals.
5. As a Unity game developer, I want old timestamp property usage to fail with guidance, so that elimination and finish state code moves to the explicit current timestamps.
6. As a Unity game developer, I want old HUD update APIs to remain hard-obsolete with clear messages, so that HUD state is driven through current player state and runtime output.
7. As a Unity game developer, I want deprecated HUD helper components to fail at source level when they are not current contract APIs, so that old scene wiring does not remain silently supported.
8. As a Unity game developer, I want current `activePlayers[]` hosted payloads to keep working, so that strict cleanup does not break current SDK integration.
9. As a Gaming Couch SDK maintainer, I want the Unity package to reject legacy play payloads, so that all legacy adaptation is centralized in the client/SDK adapter.
10. As a Gaming Couch SDK maintainer, I want package errors to identify missing SDK translation, so that hosted integration bugs point to the correct ownership boundary.
11. As a Gaming Couch SDK maintainer, I want no Unity package fallback from platform `playerId` to `playerIndex`, so that the SDK-owned active-player mapping remains authoritative.
12. As a DevApp maintainer, I want local input routing to use `playerIndex` or Seat-based routing, so that DevApp local play follows the current runtime contract.
13. As a DevApp maintainer, I want local Seat mapping preserved, so that sparse Local Play Settings and controller assignment still route to the correct Active Player Index.
14. As a DevApp maintainer, I want legacy `playerId` text input messages rejected or ignored consistently, so that old controller messages do not appear partly supported.
15. As a package maintainer, I want source-seat mapping tests to keep passing, so that cleanup does not remove current local editor behavior.
16. As a package maintainer, I want legacy player identity tests updated to assert rejection, so that the package cannot silently reintroduce adaptation.
17. As a package maintainer, I want misleading local identity fields reviewed, so that package-local metadata is not mistaken for hosted platform player identity.
18. As a package maintainer, I want obsolete attributes covered by tests, so that warning-level obsolete APIs cannot return unnoticed.
19. As a package maintainer, I want current runtime output and game-over contracts left unchanged, so that this cleanup stays focused on legacy inbound/player identity support.
20. As a platform maintainer, I want platform-facing `playerIdsByPlacement` compatibility to remain outside the Unity package, so that platform result state can evolve independently from Unity game code.
21. As a release owner, I want this cleanup documented as a package strictness change, so that SDK rollout and package recommendation can be coordinated.
22. As a tester, I want focused EditMode tests for strict payload parsing, input routing, and obsolete errors, so that the change is verifiable without broad manual playtesting.
23. As a tester, I want current `playerIndex: 0` behavior preserved, so that strict cleanup does not regress zero-based Active Player Index handling.
24. As a tester, I want malformed legacy payload errors to be deterministic, so that failures are easy to diagnose in automated logs.
25. As a future maintainer, I want legacy cleanup tasks split into vertical slices, so that parser, routing, and obsolete API work can be reviewed independently.

## Implementation Decisions

- The Unity package will not support legacy games at runtime. Legacy hosted behavior is the responsibility of the Gaming Couch client/SDK adapter before it calls the Unity package.
- The package play-payload parser will accept current play payloads containing the active-player roster and reject legacy payloads that contain only legacy player arrays.
- Legacy play-payload rejection will happen at parse time with a targeted exception. The message will explain that the client/SDK must translate legacy payloads before invoking Unity.
- The package will preserve the current game-facing play options shape used by game scripts: developers continue to receive play options containing players addressed by `playerIndex`.
- Current active-player payloads must continue to preserve pre-mapped player indices. The package must not re-shuffle already mapped hosted payloads.
- Local editor play keeps Seat-to-Active Player Index mapping. Seats are current Local Play Settings concepts, not legacy platform identity.
- DevApp/runtime inbound input in the package will no longer resolve legacy `playerId` fields. Supported input identity is `playerIndex`, plus current local Seat routing where applicable.
- The active-player mapping module will keep source-seat lookup behavior and remove or stop exposing lookup behavior that exists only for legacy player IDs.
- The local editor capture path currently assigns a package-local numeric identity that is named like platform identity. Implementation must review that field carefully: if it exists only for legacy player-ID fallback, remove it; if a value is still needed for local bookkeeping, rename or constrain it so it is not confused with hosted Platform Player Id.
- Source-level obsolete symbols are intentionally retained as migration markers. They should not be deleted in this cleanup unless a symbol cannot be retained safely.
- Every obsolete marker in the runtime package must be a compile-time error. Remaining warning-level obsolete attributes must be changed to error-level attributes.
- Obsolete messages must be actionable. They should name the replacement API or explain that there is no game-facing replacement when platform-owned data was removed.
- Deprecated HUD helper components count as obsolete API for this cleanup and should become hard errors with replacement guidance.
- Existing current-contract runtime output schemas, game-over result shape, diagnostics payload rules, platform metadata runtime view, and Local Play Settings file format are not changed by this PRD.
- This cleanup changes package strictness. Before implementation finalizes, the implementer must call out whether `gameProtocolVersion` should change. The current recommendation is no bump if the SDK already sends current payloads and legacy handling remains in the SDK/client layer, but the bump remains a user decision.

## Testing Decisions

- Tests should assert externally visible behavior: accepted current payloads, rejected legacy payloads, input-routing decisions, and obsolete attribute metadata. Avoid tests that only lock down private helper structure.
- Parser tests should prove current active-player JSON still produces playable options and legacy player-array JSON throws the targeted package error.
- Mapping tests should continue to cover source-seat mapping, sparse seats, deterministic ordering, color preservation, and `playerIndex: 0`.
- Mapping tests should remove or invert assertions that legacy player IDs can resolve to active player indices inside the package.
- DevApp inbound tests should prove `playerIndex` input routes normally and legacy `playerId` input is ignored or rejected according to the existing inbound decision model.
- Obsolete API tests should inspect `ObsoleteAttribute` metadata for all retained migration markers and assert `IsError` is true.
- Obsolete API tests should verify messages include useful replacement guidance for identity APIs, timestamp APIs, HUD APIs, and deprecated HUD helper components.
- Existing tests around hard-obsolete identity APIs, player store collections, HUD update APIs, and player state methods are prior art for the new obsolete coverage.
- Existing tests around local play contract fixtures and active-player mapping are prior art for current active-player payload coverage.
- Focused EditMode tests are sufficient for this PRD. Manual Unity playtesting is useful after implementation, but the core contract can be validated through parser, mapping, and inbound routing tests.

## Tasks

| ID | Task | Status | Done when | Dependencies | Notes |
| --- | --- | --- | --- | --- | --- |
| Task 1 | Reject legacy play payloads at the Unity package boundary while preserving current active-player payload behavior. | Completed | Current active-player play payloads parse and project successfully; legacy player-array payloads throw a targeted error that points to client/SDK translation ownership; tests cover both outcomes. | None | Current parser adapts legacy player arrays into current players and builds private participant identities from legacy player IDs. |
| Task 2 | Remove package runtime fallback from legacy player IDs to Active Player Index while preserving Seat mapping. | Completed | Runtime input routing no longer resolves legacy `playerId`; source-seat mapping still routes sparse local Seats to Active Player Indices; tests prove current `playerIndex` and Seat paths still work. | Task 1 | Current DevApp inbound routing accepts `playerId` through a legacy resolver, while source-seat mapping is current local play behavior. |
| Task 3 | Clean up misleading local identity bookkeeping tied to legacy player-ID fallback. | Not started | Any package-local field or helper that exists only for legacy player-ID fallback is removed or renamed; local editor capture remains deterministic and does not expose hosted platform identity to game-facing code; tests document the retained identity semantics. | Task 2 | Local editor capture currently assigns a numeric field named like platform player identity even though local Seats and stable keys are the current contract concepts. |
| Task 4 | Convert remaining obsolete warnings into compile-time errors with migration guidance. | Not started | All runtime obsolete attributes are hard errors; remaining timestamp and HUD helper warnings are converted; tests assert `IsError` and useful messages for every retained migration marker. | None | Most obsolete APIs are already hard errors; remaining warning-level markers include legacy timestamp properties and a deprecated HUD helper component. |
| Task 5 | Update documentation and release notes for package strictness and adapter ownership. | Not started | Package docs explain that legacy runtime adaptation belongs in the client/SDK, the package accepts only current runtime identity, and game developers should use `playerIndex`/current APIs; any protocol-version note is explicitly called out for user decision. | Tasks 1-4 | Documentation should avoid exposing deep internal terminology to game developers except where wire contract precision is required. |

## Out of Scope

- Renaming the public active-player DTO or broader developer-facing player terminology. That concern is deferred.
- Removing client/SDK legacy support. The client/SDK adapter may continue to support old hosted builds.
- Changing platform-facing playlist or stats concepts that still use platform player IDs.
- Changing runtime output schemas, screen-space schemas, diagnostics schemas, or object-wrapped game-over result shape.
- Changing Local Play Settings, Platform Data, or Contract Fixture file formats beyond tests needed for this cleanup.
- Removing all obsolete symbols outright. This PRD keeps hard-obsolete migration markers unless implementation proves a specific symbol cannot be retained safely.
- Bumping `gameProtocolVersion` automatically. The implementer must surface the compatibility question, but the bump is a user decision.

## Further Notes

- The current package glossary distinguishes Seats, Active Players, Active Player Index, and Platform Player Id. This PRD relies on that vocabulary.
- The core policy is ownership: package current-contract enforcement lives in Unity; legacy adaptation lives in client/SDK.
- The implementation should avoid making game developers learn internal boundary terms unless they are reading wire-level integration errors or architecture docs.
