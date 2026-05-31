## Problem Statement

The Unity package currently exposes platform-owned player identity and player names to game runtime code. That lets games accidentally couple behavior to stable player IDs, player ordering, playlist state, or localized display names, which works against the platform goal that games remain pure and only act on the participants in the current round.

The current player state API is also ambiguous. `SetEliminated` can be reversed with `SetUneliminated`, and `SetFinished` does not distinguish final finish from a finish that can still be revoked. Games need explicit permanent and revokable state methods so developers choose recoverability intentionally at each call site.

Developer feedback is too local. Invalid API use usually appears only in the Unity console, and some warnings can be hidden behind package log level settings. DevApp and future hosted developer tools need structured diagnostics from games so developers can see integration mistakes during local play and uploaded-game validation.

Unity project configuration also does not fully consume dashboard-managed `gc.platform.json` data at runtime. The editor reads some platform data for local play validation, but Unity game code does not receive a stable runtime view and missing platform data is not treated as a persistent project health issue.

## Solution

Move the Unity runtime contract to an index-based model. Unity games should receive and emit `playerIndex` values only. Platform player IDs remain inside platform, client, SDK, and DevApp infrastructure and are mapped at the platform boundary when necessary.

Tighten the Unity player API so `Index` is canonical, old game-facing `Id`/`playerId`/`PlayerName` usage fails at source level with migration guidance where possible, and player state uses explicit permanent/revokable elimination and finish methods.

Add a strict runtime-to-platform event and diagnostics path. Player/game events provide future extensibility for features like permanent-elimination phone screens, personal death sounds, winning-player sounds, and playlist victory sounds. Structured GC API diagnostics are visible in DevApp immediately and wired at the hosted WebGL boundary for future tools.

Make `gc.platform.json` a read-only dashboard metadata input for Unity editor and runtime. When it is missing or invalid, Unity uses a fallback `notdefined` entry with minimum defaults and persistent warnings instead of silently behaving as if configuration is healthy.

## Orchestration Role

This file is the top-level orchestration plan for the Unity runtime contract work. It describes the intended product direction, workstream split, sequencing, and acceptance themes.

The child plans now capture the core contract decisions needed to start implementation planning. This PRD remains a planning artifact, not implementation evidence: as of 2026-05-31, no Unity package, DevApp/client/SDK, hosted, or internal-game code changes have been implemented from this plan.

## Implementation Readiness

| Workstream | Status | Before implementation |
| --- | --- | --- |
| Contract framing and rollout | Core contract decisions captured | Use the staged adapter rollout in `07-cross-repo-rollout.md`: no immediate `gameProtocolVersion` bump, adapter support before Unity `0.2.0-alpha.1`, internal-game migration, and a legacy bridge removal checkpoint. |
| Active Player Index mapping | Core contract decisions captured | Implementation planning can use `01-active-player-index-mapping.md`; diagnostics and rollout sequencing are captured in the child plans. |
| Unity API migration | Core contract decisions captured | Implementation planning can use `02-unity-api-migration.md`; rollout still owns the temporary runtime bridge for older built games. |
| Player state model | Core contract decisions captured | Implementation planning can use `03-player-state-model.md`; diagnostics code naming and runtime-events alignment remain dependencies. |
| Runtime events and results | Core contract decisions captured | Implementation planning can use `04-runtime-events-and-results.md`; game-over migration uses object-shape discrimination rather than a one-off per-result schema field. |
| Diagnostics spine | Core contract decisions captured | Implementation planning can use `05-diagnostics-spine.md`; rollout sequencing is captured in `07-cross-repo-rollout.md`. |
| Platform metadata runtime view | Core contract decisions captured | Implementation planning can use `06-platform-metadata-runtime-view.md`; rollout sequencing captures upload/publish enforcement timing. |
| Examples and docs | Depends on child plans | Update generated examples, glossary, migration guide, and docs after the contracts above are stable. |

## Child Planning Files

- `01-active-player-index-mapping.md`
- `02-unity-api-migration.md`
- `03-player-state-model.md`
- `04-runtime-events-and-results.md`
- `05-diagnostics-spine.md`
- `06-platform-metadata-runtime-view.md`
- `07-cross-repo-rollout.md`

## Planning Sequence

These child plans were stabilized in this dependency order. Cross-repo implementation and release order are owned by `07-cross-repo-rollout.md`.

1. Active Player Index mapping, because it affects Unity API, DevApp, hosted WebGL, HUD, game-over, deterministic active-player shuffle, and the JavaScript follow-up.
2. Diagnostics spine, because API deprecation warnings, invalid state warnings, metadata warnings, and unsupported API warnings should all use the final path from the start.
3. Unity API migration and player state model, because these are the developer-facing breaking changes.
4. Runtime events/results, because result and event contracts need to stabilize before source/API changes become legacy.
5. Platform metadata runtime view, because fallback behavior and future dashboard-owned metadata need clear health semantics.
6. Cross-repo rollout, so Unity package, DevApp, client, SDK, and internal games do not drift during implementation.

## User Stories

1. As a Unity game developer, I want to use `GCPlayer.Index`, so that my game logic addresses players by round-local participant position.
2. As a platform engineer, I want Unity games to never receive platform player IDs, so that games cannot persist or special-case real player identity.
3. As a platform engineer, I want Unity games to never receive player names, so that localization and display-name constraints stay owned by the platform.
4. As a Unity game developer, I want old `Id` and `playerId` usage to fail with clear migration guidance, so that game code moves intentionally to `Index` and `playerIndex`.
5. As a Unity game developer, I want old `PlayerName` usage to fail at compile time, so that I do not ship unsupported name rendering.
6. As a Unity game developer, I want input callbacks and polling to use `playerIndex`, so that input routing matches the rest of the runtime API.
7. As a Unity game developer, I want HUD updates to use `playerIndex`, so that player state, screen points, and placements all address the same game-facing identity.
8. As a platform engineer, I want hosted Unity WebGL to map platform IDs to player indices at the SDK adapter boundary, so that Unity code stays ID-free while platform bookkeeping remains correct.
9. As a DevApp developer, I want seats to remain local development concepts, so that local controller routing can stay ergonomic without leaking seats into game runtime APIs.
10. As a DevApp developer, I want active seat input to route to `playerIndex`, so that Unity Editor play matches hosted runtime behavior.
11. As a game developer, I want explicit permanent and revokable elimination methods, so that terminal and recoverable removal from play are chosen intentionally.
12. As a game developer, I want explicit permanent and revokable finish methods, so that finish can be final or revocable depending on game rules.
13. As a game developer, I want revoke methods for revokable elimination and finish state, so that recovery and finish rollback are visible at the call site.
14. As a game developer, I want old ambiguous state methods removed, so that old reversible-elimination and generic-finish code must be migrated intentionally.
15. As a game developer, I want wrong-state player action calls to warn and no-op, so that invalid state transitions are visible but do not crash a live round by default.
16. As a game developer, I want revokable-to-permanent calls to promote state in place, so that temporary outcomes can become final without reset/reapply ceremony.
17. As a game developer, I want duplicate state actions to warn clearly, so that repeated calls reveal game logic mistakes.
18. As a platform engineer, I want player state changes to emit structured runtime events, so that the client can add features later without updating existing Unity games.
19. As a platform engineer, I want permanent elimination to be represented in the event stream, so that player clients can eventually show elimination-specific UI.
20. As a product developer, I want to record a future phone death screen idea, so that permanent elimination can later become a fun player-facing feature.
21. As a product developer, I want to record personal death sound ideas, so that player-client audio can be explored after the event path exists.
22. As a product developer, I want to record winning-player and playlist victory sound ideas, so that game-over celebrations can be designed later.
23. As a Unity game developer, I want essential HUD state like score, lives, status, and meter to be documented and tested, so that I do not invent unsupported side channels.
24. As a Unity game developer, I want HUD state to keep working in local play and hosted play, so that the same code path is reliable in both environments.
25. As a DevApp user, I want GC API misuse diagnostics to appear in DevApp, so that I can fix integration mistakes while playtesting.
26. As a platform engineer, I want structured diagnostics to have stable codes, so that future tooling can group, filter, and explain repeated issues.
27. As a Unity game developer, I want optional Unity warning/error log capture in development, so that non-GC runtime problems can also be investigated when needed.
28. As a platform engineer, I want all-log capture to be filtered and rate-limited, so that diagnostics do not become an unbounded runtime stream.
29. As a hosted-game tooling developer, I want hosted WebGL to receive structured GC diagnostics, so that future developer tools can consume them without changing Unity games again.
30. As a DevApp user, I want missing or invalid `gc.platform.json` to be shown persistently, so that project configuration problems are not hidden.
31. As a Unity game developer, I want a fallback `notdefined` entry when platform data is missing, so that local play can still run in a degraded state.
32. As a Unity game developer, I want runtime code to read game entries and player colors from platform data, so that Unity games use dashboard-managed settings.
33. As a platform engineer, I want Unity to treat `gc.platform.json` as read-only, so that dashboard-managed metadata remains the source of truth.
34. As a Unity game developer, I want generated examples to show lifecycle and input wiring, so that I can start from a correct integration pattern.
35. As a Unity game developer, I want generated examples to show player-state actions and diagnostics, so that API misuse and state transitions are visible during onboarding.
36. As a Unity game developer, I want generated examples to remain small, so that they explain integration without becoming a full sample game.
37. As a package maintainer, I want unsupported multiplayer APIs hidden by default behind an explicit define, so that developers do not mistake them for a supported contract.
38. As a package maintainer, I want the staged adapter rollout documented in rollout planning, so that we can preserve older internal Unity games now while keeping a future `gameProtocolVersion` or broader versioning boundary available.
39. As a platform engineer, I want active-player indices shuffled deterministically for each run, so that Unity games cannot infer stable player identity or give a persistent advantage to roster order.
40. As a client engineer, I want JS game contract migration planned as the next tightly coupled follow-up, so that Unity-first changes do not leave long-lived JS/Unity semantic drift under the same platform vocabulary.

## Implementation Decisions

- The game-facing participant term is **Active Player Index**: zero-based, dense, and local to the active runtime play.
- The local development roster term is **Seat**: one-based, DevApp/local-play facing, and not a runtime game identity.
- The platform-owned participant term is **Platform Player Id**: infrastructure-only and unavailable to Unity game code.
- New or changed runtime payloads use `playerIndex` and object-wrapped result payloads. The initial game-over result object carries `playerIndicesByPlacement` so it is unambiguous from the legacy array shape and can grow later.
- Platform/client adapters own all ID-to-index and index-to-ID mapping.
- Hosted Unity input maps platform player IDs to player indices before Unity receives input.
- Hosted Unity game over maps player indices back to platform player IDs before playlist and stats bookkeeping.
- Hosted Unity HUD payloads map player indices back to platform player IDs before existing host HUD rendering consumes them.
- DevApp runtime control keeps `seatIndex` for local development routing while Unity runtime messages expose active player indices.
- Active player mapping includes deterministic shuffle in this PRD. Shuffle uses a run seed and participant stable key at the platform boundary so Unity game code only sees dense shuffled indices.
- `GCPlayer.Index` is canonical.
- Old game-facing `Id` and `playerId` APIs are source-level compile breaks with migration guidance where possible. Already-built older games are handled by a short-lived platform/client/SDK runtime bridge planned in rollout.
- `PlayerName` is removed from the Unity runtime API with no replacement.
- Unity setup and play data no longer provide player names or platform player IDs to runtime game code. This is an intentional privacy and fairness boundary: games should not be able to discriminate by stable platform identity or display name.
- The new game-facing active-player DTO contains only `playerIndex`, player type, and player color. Package-internal or adapter-owned DTOs that need platform IDs, names, or seat routing must stay outside the Unity game-facing runtime API.
- Old generic state methods `SetEliminated(reason)`, `SetUneliminated(reason)`, and `SetFinished(reason)` are source-level compile breaks with migration guidance.
- Elimination state is explicit: `None`, `Revokable`, or `Permanent`.
- Finish state is explicit: `None`, `Revokable`, or `Permanent`.
- Public state methods are `SetEliminatedPermanent(reason)`, `SetEliminatedRevokable(reason)`, `SetRevokeEliminated(reason)`, `SetFinishedPermanent(reason)`, `SetFinishedRevokable(reason)`, and `SetRevokeFinished(reason)`.
- Use the spelling `Revokable` in public API names.
- Elimination and finish are independent facts. A player can be both finished and eliminated before game over.
- Revokable states count at `GameOver()` if they have not been revoked.
- Permanent calls promote matching revokable state in place. Invalid revoke, duplicate state calls, invalid transitions, and post-game-over mutations warn through diagnostics and no-op.
- Existing score, lives, status, and meter behaviors should keep their public intent, but warnings should be made consistent where calls are invalid or clamped.
- Placement sorting must continue to support broad elimination, score, and finished criteria while using the new state model. Complex state-aware placement ordering is deferred to a future custom comparer.
- Runtime player state is canonical. HUD remains configurable, but HUD data is a projection of runtime state.
- HUD player data uses `playerIndex`, `eliminationState`, `finishState`, placement, status/lives/text values, and meter. Temporary receiving-end adapters may derive old boolean `eliminated` behavior while platform/client code catches up.
- Runtime-to-platform events use the strict v1 envelope, transport, and catalog in `04-runtime-events-and-results.md`, not arbitrary untyped custom event names.
- Initial event catalog includes play-start, player elimination state, player finish state, and submitted-result events. Richer result semantics such as ties, DNF, no-contest, teams, score snapshots, and structured result reasons are reserved for a future GameOverResult v2 contract after a general result/versioning policy is chosen.
- Permanent elimination event support is included, but platform/client enforcement behavior is not. No input suppression is required in this PRD.
- Structured GC diagnostics are a first-class runtime output with stable code, severity, source area, emitter timestamp, optional run context, optional player index, bounded details, and bounded debug evidence. Public diagnostics are `playerIndex`-only; hosted adapters may privately correlate diagnostics to platform player IDs after validation, but platform IDs do not appear in `RuntimeDiagnostic` fields, DevApp diagnostics UI, or hosted public diagnostics callbacks. Diagnostic codes use the current hard-break and permanent/revokable state vocabulary; stale deprecated-ID and out-of-action terms are not part of the v1 catalog. The detailed spine lives in `05-diagnostics-spine.md`.
- GC diagnostics must not depend on the package log level to be emitted.
- Optional Unity warning/error log capture is development-only, externally launch-controlled, off by default, and limited to warnings, errors, asserts, and exceptions.
- DevApp stores recent active-run diagnostics with sink-side aggregation, a 200-row ring buffer, and a hidden-by-default virtualized diagnostics console behind the existing Settings diagnostics toggle.
- Hosted WebGL exposes an API-level structured diagnostics callback path, but no host diagnostics UI is required.
- `gc.platform.json` is read-only platform metadata for Unity.
- Unity runtime receives a stable view of dashboard-managed game entries, entry limits, bot support, and player colors.
- Missing or invalid `gc.platform.json` creates a persistent editor and runtime warning.
- Missing or invalid `gc.platform.json` falls back to a `notdefined` entry with minimum defaults.
- Unity must not create, repair, or write `gc.platform.json`.
- Generated examples demonstrate setup, play, input polling by `playerIndex`, console logging, HUD essentials, explicit player state actions, diagnostics, and game over.
- Generated examples remain a wiring demo and do not become a polished mini-game.
- Unsupported multiplayer APIs are contained by default. Passive support checks may remain only when they return `false`; actionable multiplayer calls either compile only under `GC_ENABLE_UNSUPPORTED_MULTIPLAYER` or throw a clear unsupported error with diagnostics when retained for source-transition reasons.
- Unity-first rollout uses the staged adapter path from `07-cross-repo-rollout.md`: no immediate `gameProtocolVersion` bump, strict adapter validation, package-version skew checks, internal-game migration, and a legacy bridge removal checkpoint.
- A future `gameProtocolVersion` bump or broader runtime versioning boundary remains available after internal Unity games and JavaScript runtime semantics are aligned.

## Testing Decisions

- Tests should verify externally visible behavior and contracts, not private implementation details.
- Deep modules should be extracted around identity mapping, player state transitions, diagnostics emission, platform metadata fallback, and runtime message translation.
- Unity EditMode tests should cover canonical index setup, `playerIndex` DTO/HUD payloads, old ID/name/state API compile-break guidance where practical, explicit permanent/revokable elimination and finish transitions, wrong-state warnings, HUD state projection, diagnostics emission, log-level bypass, Unity console mirroring, end-of-frame diagnostics batching, and launch-only Unity log capture filtering.
- Unity local-play and DevApp message tests should cover seat-to-index routing, runtime snapshot identity, game-over placements, diagnostics ingress, active-run filtering, malformed diagnostics batch rejection, sink-side aggregation, 200-row ring-buffer eviction, hidden diagnostics rail behavior, and diagnostics UI update batching.
- Client/SDK tests should cover hosted Unity ID-to-index input mapping, index-to-ID game-over mapping, new object-wrapped game-over result handling, temporary legacy array bridge behavior for older built games, HUD state mapping, diagnostics callback validation, public `playerIndex`-only diagnostics identity, and strict rejection of malformed runtime events.
- DevApp tests should cover visible diagnostics for active Unity Editor runs, virtualized diagnostics rendering, diagnostic retention/reset across runs, manual clear/filter/search behavior, and local runtime input routing by active player index.
- Platform metadata tests should cover valid platform data, missing platform data fallback to `notdefined`, invalid platform data fallback, persistent warnings, runtime visibility, and no write-back.
- Generated-example tests should assert that the example source demonstrates lifecycle, inputs by `playerIndex`, HUD essentials, explicit player-state actions, diagnostics, and game over.
- Existing contract fixture style should be reused for JSON-backed local play cases.
- Existing runtime message tests should be extended rather than replaced for DevApp protocol changes.
- Existing active-scene setup asset tests should be extended for generated example behavior.
- Existing client unit tests around game integration, HUD, and DevApp protocol should be extended for mapping and diagnostics.

## Tasks

Task status reflects implementation state. `Not started` means the work remains to be implemented against the current contract; it does not imply the current code already matches the plan.

| ID | Task | Status | Done when | Dependencies | Notes |
| --- | --- | --- | --- | --- | --- |
| Task 0 | Complete child planning and grilling gates. | Completed | Active Player Index mapping, diagnostics spine, runtime events/results, platform metadata fallback, Unity API migration, player state model, and cross-repo rollout plans are reviewed and marked implementation-ready. | None | Child plans are implementation-ready for planning; no code implementation has started from this PRD. |
| Task 1 | Introduce the Active Player Index contract across Unity setup, play, input, HUD, and game-over flows. | Not started | Unity game code receives deterministically shuffled player indices, emits player indices, and no runtime game-facing payload exposes platform player IDs or player names. | Task 0 | Current runtime uses `playerId` in setup/play options, input routing, HUD, stores, and placements. |
| Task 2 | Add hosted Unity boundary mapping for IDs and indices. | Not started | Hosted Unity maps platform IDs to shuffled player indices for game input and maps player indices back to platform IDs for HUD/game-over bookkeeping without exposing IDs to Unity. | Task 0, Task 1 | The client SDK currently forwards platform player IDs directly to Unity. |
| Task 3 | Add DevApp seat-to-index runtime routing. | Not started | Unity Editor runtime messages and routed inputs use active player indices while DevApp retains seat indices for local controller assignment and display only. | Task 0, Task 1 | Seats are local development concepts and should not become game-facing identity. |
| Task 4 | Migrate Unity player identity API. | Not started | `Index` is canonical, public game-facing DTO/payload fields use `playerIndex`, active-player DTOs contain no names or platform IDs, old ID/name APIs fail at source level with migration guidance where possible, and input/store APIs are index-named. | Task 0, Task 1 | Source API breaks intentionally; older built-game compatibility belongs to the rollout bridge. |
| Task 5 | Replace ambiguous player state APIs with explicit permanent/revokable state. | Not started | Old generic state methods fail at source level, permanent/revokable elimination and finish methods are implemented, invalid transitions emit diagnostics and no-op, and post-game-over mutations no-op. | Task 0, Task 4 | Current elimination is a reversible boolean and finish is a single timestamp. |
| Task 6 | Update player store, placement, and HUD state semantics. | Not started | Player collections expose `Players...` state-filtered lists with bot/non-bot symmetry, old store collection names hard-break with substitute messages, broad placement criteria work with permanent/revokable state, and HUD data projects canonical runtime state. | Task 5 | Existing store tracks eliminated/uneliminated lists only and HUD currently exposes a boolean eliminated field. |
| Task 7 | Add strict runtime event stream for player and game events. | Not started | Unity can emit structured player/game events with player indices and the same elimination/finish state vocabulary used by `GCPlayer`; DevApp/host adapters validate the event catalog. | Task 0, Task 1, Task 5 | Event stream should become the future source of truth for state; platform-to-runtime implementation is deferred, but ingress envelope planning is required. |
| Task 8 | Add structured GC API diagnostics and development log capture. | Not started | GC API misuse, legacy bridge use, invalid state transitions, metadata fallback, and unsupported API access emit stable current-vocabulary diagnostics visible in DevApp; hosted WebGL receives structured diagnostics, and optional Unity log capture is filtered and rate-limited. | Task 0 | DevApp is visible in v1; hosted diagnostics UI is future work. |
| Task 9 | Expose read-only `gc.platform.json` data to Unity runtime with fallback defaults. | Not started | Unity runtime receives platform entries, limits, bot support, and colors when valid; missing or invalid data falls back to `notdefined` and emits persistent editor/runtime warnings. | Task 0 | Existing platform data reader is editor-focused and warning-only. |
| Task 10 | Stabilize essential HUD APIs around player indices and canonical state projection. | Not started | Score, lives, status, text, meter, placement, elimination state, and finish state HUD flows are index-based, documented, and covered by tests in local and hosted paths. | Task 1, Task 6 | HUD remains configurable, but state data should be projected from canonical runtime state. |
| Task 11 | Expand generated/example setup to demonstrate the correct integration path. | Not started | Generated examples show setup, play, input polling by `playerIndex`, console logs, HUD essentials, explicit state actions, diagnostics, and game over without becoming a full mini-game. | Task 4, Task 5, Task 8, Task 10 | Existing active scene setup already generates example scripts and should be evolved. |
| Task 12 | Contain unsupported multiplayer APIs behind explicit legacy behavior. | Not started | Multiplayer capability checks return false by default, actionable APIs are absent or throw clear unsupported errors unless `GC_ENABLE_UNSUPPORTED_MULTIPLAYER` is enabled, and all retained use is documented as unsupported temporary internal migration surface. | Task 8 | This is containment, not multiplayer feature work. |
| Task 13 | Update documentation and domain glossary for the runtime contract. | Not started | Docs consistently explain Seat, Active Player Index, Platform Player Id, permanent/revokable elimination, permanent/revokable finish, diagnostics, and platform metadata fallback. | Task 1, Task 5, Task 8, Task 9 | Existing docs mention `player.Id`, `PlayerName`, generic `SetEliminated`, `SetFinished`, and player-id HUD patterns. |
| Task 14 | Add cross-repo validation for the Unity-first runtime contract. | Not started | Unity tests, DevApp/client tests, temporary older-built-game bridge tests, and focused integration checks pass for shuffled index mapping, object game-over results, diagnostics, platform metadata fallback, HUD, and generated examples. | Task 2, Task 3, Task 8, Task 11 | Use the open-Editor Unity test bridge for package validation where practical. |

## Out of Scope

- No immediate `gameProtocolVersion` bump implementation in this Unity-first slice; the staged adapter rollout preserves a later bump or broader versioning decision.
- No full DevApp versioning or incompatible-version UX redesign.
- No JavaScript game runtime migration in this Unity-first slice. JS migration should be planned as the next tightly coupled follow-up.
- No platform-to-runtime event stream implementation beyond reserving the future ingress shape during runtime events/results planning.
- No hosted diagnostics UI.
- No player-client death screen, death sound, winning-player sound, or playlist victory sound implementation.
- No input suppression or enforced client behavior for eliminated players.
- No broad HUD toolkit beyond stabilizing essential existing HUD state.
- No supported multiplayer feature work.
- No Unity writes, repair, or bootstrap behavior for `gc.platform.json`.

## Further Notes

- Future player-client elimination and victory features should consume runtime events rather than requiring Unity games to be updated.
- Future HUD rendering should treat HUD as a configurable projection of canonical runtime state rather than the source of state truth.
- Future runtime/platform capability planning should leave room for capability versioning, either as versioned capability names or explicit version fields.
- The temporary runtime bridge for older built Unity games is a one-off internal migration bridge, not the long-term versioning or deployment policy.
- The fallback platform entry name is `notdefined`, intentionally avoiding `undefined`.
- Developer diagnostics should be treated as product infrastructure, not just logging, because they are the bridge to better DevApp and hosted developer tools.

## Post-Legacy Cleanup Ledger

Remove these temporary surfaces after all internal games are migrated off the older built-game bridge and source-level hard-obsolete APIs:

- Remove legacy `playerIdsByPlacement` / array-shaped game-over result acceptance from platform, client, SDK, DevApp, and Unity local-play adapters.
- Remove temporary platform/client/SDK mapping code that adapts already-built ID-based Unity games.
- Remove hard-obsolete Unity source symbols after their substitute messages have served the migration, including old ID/name APIs, old state methods, and old store collection names.
- Remove temporary HUD adapters that derive old boolean `eliminated` payloads after platform/client HUD rendering consumes canonical state.
- Remove `GC_ENABLE_UNSUPPORTED_MULTIPLAYER` and any retained unsupported multiplayer stubs once no migrated game needs the legacy path.
