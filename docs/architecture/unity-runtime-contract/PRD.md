## Problem Statement

The Unity package currently exposes platform-owned player identity and player names to game runtime code. That lets games accidentally couple behavior to stable player IDs, player ordering, playlist state, or localized display names, which works against the platform goal that games remain pure and only act on the participants in the current round.

The current player state API is also ambiguous. `SetEliminated` can be reversed with `SetUneliminated`, and `SetFinished` does not distinguish final finish from a finish that can still be revoked. Games need explicit permanent and revokable state methods so developers choose recoverability intentionally at each call site.

Developer feedback is too local. Invalid API use usually appears only in the Unity console, and some warnings can be hidden behind package log level settings or developer-controlled build/runtime logging choices. DevApp and future hosted developer tools need structured diagnostics from games so developers can see integration mistakes during local play and uploaded-game validation without treating `Debug.Log`, WebGL loader output, or the browser console as critical transport.

Runtime output is also too HUD-shaped and result-shaped. Current screen-point and player HUD payloads are named and modeled as HUD data even though they contain runtime facts and screen-space coordinates that support tooling, hosted adapters, overlays, and future AI/debugging flows should be able to consume without treating HUD as the source of truth.

Unity project configuration also does not fully consume dashboard-managed `gc.platform.json` data at runtime. The editor reads some platform data for local play validation, but Unity game code does not receive a stable runtime view and missing platform data is not treated as a persistent project health issue.

## Solution

Move the Unity runtime contract to an index-based model. Unity games should receive and emit `playerIndex` values only. Platform player IDs remain inside platform, client, SDK, and DevApp infrastructure and are mapped at the platform boundary when necessary.

Tighten the Unity player API so `Index` is canonical, old game-facing `Id`/`playerId`/`PlayerName` usage fails at source level with migration guidance where possible, and player state uses explicit permanent/revokable elimination and finish methods.

Add a strict runtime output contract with two physical paths. `runtime_messages` carries semantic state snapshots, transition messages, diagnostics, captured Unity log records when launch policy enables them, and effectful platform submissions. `screen_space` carries hot latest-state screen-coordinate anchors. HUD rendering consumes these runtime outputs plus HUD configuration rather than owning the semantic data model.

Separate platform-owned runtime output from host-owned console mirroring. GC diagnostics and runtime facts are structured runtime messages. Optional Unity log capture is development-only, launch-controlled, and wrapped into structured diagnostics when enabled. WebGL loader `print`/`printErr` and browser console capture are HTML/JavaScript host debugging aids, not Unity runtime contract paths.

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
| Player state model | Core contract decisions captured | Implementation planning can use `03-player-state-model.md`; diagnostics code naming and runtime-output alignment remain dependencies. |
| Runtime output contract | Core contract decisions captured | Implementation planning can use `04-runtime-output-contract.md`; runtime output uses `runtime_messages` and `screen_space`, and terminal placement submission uses object-shape discrimination rather than a one-off per-result schema field. |
| Diagnostics spine | Core contract decisions captured | Implementation planning can use `05-diagnostics-spine.md`; rollout sequencing is captured in `07-cross-repo-rollout.md`. |
| Platform metadata runtime view | Core contract decisions captured | Implementation planning can use `06-platform-metadata-runtime-view.md`; rollout sequencing captures upload/publish enforcement timing. |
| Examples and docs | Depends on child plans | Update generated examples, glossary, migration guide, and docs after the contracts above are stable. |

## Child Planning Files

- `01-active-player-index-mapping.md`
- `02-unity-api-migration.md`
- `03-player-state-model.md`
- `04-runtime-output-contract.md`
- `05-diagnostics-spine.md`
- `06-platform-metadata-runtime-view.md`
- `07-cross-repo-rollout.md`

## Planning Sequence

These child plans were stabilized in this dependency order. Cross-repo implementation and release order are owned by `07-cross-repo-rollout.md`.

1. Active Player Index mapping, because it affects Unity API, DevApp, hosted WebGL, runtime messages, screen-space anchors, game-over, deterministic active-player shuffle, and the JavaScript follow-up.
2. Diagnostics spine, because API deprecation warnings, invalid state warnings, metadata warnings, and unsupported API warnings should all use the final path from the start.
3. Unity API migration and player state model, because these are the developer-facing breaking changes.
4. Runtime output contract, because state snapshots, transition messages, diagnostics, screen-space anchors, and effectful terminal placement need to stabilize before source/API changes become legacy.
5. Platform metadata runtime view, because fallback behavior and future dashboard-owned metadata need clear health semantics.
6. Cross-repo rollout, so Unity package, DevApp, client, SDK, and internal games do not drift during implementation.

## User Stories

1. As a Unity game developer, I want to use `GCPlayer.Index`, so that my game logic addresses players by round-local participant position.
2. As a platform engineer, I want Unity games to never receive platform player IDs, so that games cannot persist or special-case real player identity.
3. As a platform engineer, I want Unity games to never receive player names, so that localization and display-name constraints stay owned by the platform.
4. As a Unity game developer, I want old `Id` and `playerId` usage to fail with clear migration guidance, so that game code moves intentionally to `Index` and `playerIndex`.
5. As a Unity game developer, I want old `PlayerName` usage to fail at compile time, so that I do not ship unsupported name rendering.
6. As a Unity game developer, I want input callbacks and polling to use `playerIndex`, so that input routing matches the rest of the runtime API.
7. As a Unity game developer, I want runtime state and screen-space output to use `playerIndex`, so that player state, screen coordinates, and placements all address the same game-facing identity.
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
18. As a platform engineer, I want player state changes to emit structured runtime messages, so that the client can add features later without updating existing Unity games.
19. As a platform engineer, I want permanent elimination to be represented in `runtime_messages`, so that player clients can eventually show elimination-specific UI.
20. As a product developer, I want to record a future phone death screen idea, so that permanent elimination can later become a fun player-facing feature.
21. As a product developer, I want to record personal death sound ideas, so that player-client audio can be explored after the runtime message path exists.
22. As a product developer, I want to record winning-player and playlist victory sound ideas, so that game-over celebrations can be designed later.
23. As a Unity game developer, I want essential runtime state like score, lives, status, and meter to be documented and tested, so that I do not invent unsupported side channels.
24. As a Unity game developer, I want HUD rendering to work from runtime state and screen-space anchors in local play and hosted play, so that the same output paths are reliable in both environments.
25. As a DevApp user, I want GC API misuse diagnostics to appear in DevApp, so that I can fix integration mistakes while playtesting.
26. As a platform engineer, I want structured diagnostics to have stable codes, so that future tooling can group, filter, and explain repeated issues.
27. As a Unity game developer, I want optional launch-controlled Unity log capture in development, with warning/error capture by default and full log capture only when explicitly requested, so that non-GC runtime problems can also be investigated when needed without relying on browser console history.
28. As a platform engineer, I want Unity log capture, WebGL loader mirroring, and browser console capture to be separate configurable concerns, so that diagnostics can stay structured while local debugging and slow-device profiles remain controllable.
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
41. As a support/tooling developer, I want a reliable runtime output stream, so that support tools and future AI analysis can inspect what happened without treating HUD rendering as the source of truth.
42. As a platform engineer, I want effectful platform submissions modeled as typed runtime messages, so that terminal placement is the first accepted/rejected platform-state update rather than a one-off special call.

## Implementation Decisions

- The game-facing participant term is **Active Player Index**: zero-based, dense, and local to the active runtime play.
- The local development roster term is **Seat**: one-based, DevApp/local-play facing, and not a runtime game identity.
- The platform-owned participant term is **Platform Player Id**: infrastructure-only and unavailable to Unity game code.
- New or changed runtime payloads use `playerIndex` and object-wrapped terminal placement payloads. The initial terminal placement object carries `playerIndicesByPlacement` so it is unambiguous from the legacy array shape and can grow later.
- Platform/client adapters own all ID-to-index and index-to-ID mapping.
- Hosted Unity input maps platform player IDs to player indices before Unity receives input.
- Hosted Unity terminal placement maps player indices back to platform player IDs before playlist and stats bookkeeping.
- Hosted Unity runtime messages and screen-space anchors map player indices back to platform player IDs only inside validated adapter-private bookkeeping where platform-owned consumers still require IDs.
- DevApp runtime control keeps `seatIndex` for local development routing while Unity runtime messages expose active player indices.
- Active player mapping includes deterministic shuffle in this PRD. Shuffle uses a run seed and participant stable key at the platform boundary so Unity game code only sees dense shuffled indices.
- Deterministic shuffle uses UTF-8 FNV-1a 32-bit hash-sort with unsigned ordering and captured-roster-order tie-breaks. Production hosted play requires a platform/session active-run seed and fails validation if it is missing; Unity Editor local play uses the captured `gc.dev.json` seed after `"random"` has resolved to an integer for the active captured run only.
- Client/SDK adapter implementation owns resolving and validating the platform/session active-run seed before active-player mapping construction. Production hosted play fails before Unity play payload creation if the seed is absent.
- `GCPlayer.Index` is canonical.
- Old game-facing `Id` and `playerId` APIs are source-level compile breaks with migration guidance where possible. Already-built older games are handled by a short-lived platform/client/SDK runtime bridge planned in rollout.
- `PlayerName` is removed from the Unity runtime API with no replacement.
- Unity setup and play data no longer provide player names or platform player IDs to runtime game code. This is an intentional privacy and fairness boundary: games should not be able to discriminate by stable platform identity or display name.
- The new game-facing active-player DTO contains only `playerIndex`, player type, and player color. Package-internal or adapter-owned DTOs that need platform IDs, names, or seat routing must stay outside the Unity game-facing runtime API.
- Old generic state methods `SetEliminated(reason)`, `SetUneliminated(reason)`, and `SetFinished(reason)` are source-level compile breaks with migration guidance.
- Elimination state is explicit: `None`, `Revokable`, or `Permanent`.
- Finish state is explicit: `None`, `Revokable`, or `Permanent`.
- Public state methods are `SetEliminatedPermanent(reason)`, `SetEliminatedRevokable(reason)`, `SetRevokeEliminated(reason)`, `SetFinishedPermanent(reason)`, `SetFinishedRevokable(reason)`, and `SetRevokeFinished(reason)`.
- New game-facing state timestamps use scaled Unity game time in seconds with `GameTime`-suffixed names, and state event args expose `changedAtGameTime` rather than an ambiguous `changedAt`. Runtime messages and diagnostics continue to use unscaled active-run-relative `runtimeTimeMs` for ordering.
- Use the spelling `Revokable` in public API names.
- Elimination and finish are independent facts. A player can be both finished and eliminated before game over.
- Revokable states count at `GameOver()` if they have not been revoked.
- Permanent calls promote matching revokable state in place. Invalid revoke, duplicate state calls, invalid transitions, and post-game-over mutations warn through diagnostics and no-op.
- Existing score, lives, status, and meter behaviors should keep their public intent, but warnings should be made consistent where calls are invalid or clamped.
- Placement sorting must continue to support broad elimination, score, and finished criteria while using the new state model. Complex state-aware placement ordering is deferred to a future custom comparer.
- Runtime player state is canonical. HUD remains configurable, but HUD rendering consumes canonical runtime state, screen-space anchors, and HUD configuration rather than owning the semantic data model.
- Unity runtime output has two physical paths: `runtime_messages` for semantic/effectful records and `screen_space` for hot latest-state screen-coordinate anchors.
- Active-run roster/setup carries static player facts: `playerIndex`, player type, and player color. Dynamic state snapshots reference `playerIndex` and do not repeat type or color.
- Runtime messages use per-run `sequence` and unscaled `runtimeTimeMs`; `runId` is active-run context metadata resolved by the receiver/adapter, not a repeated field on every message.
- Full state snapshots carry game status and all active players' dynamic state: score, lives, status/statusText, meter, placement, elimination state, and finish state.
- State snapshots emit on play start and meaningful semantic changes. Unless snapshots are disabled at boot for profiling, Unity flushes pending snapshot output before, or in the same ordered batch immediately before, terminal placement submission.
- Transition messages cover score, lives, status/statusText, meter, elimination state, and finish state changes. They carry `playerIndex`, previous value, new value, and bounded reason text where available.
- `screen_space` carries `playerIndex`-addressed anchors such as `playerOverhead` and `playerPosition` with normalized coordinates, `isOffScreen`, `frameIndex`, and `runtimeTimeMs`. Absence from the latest batch means the anchor is not visible right now.
- Terminal placement submission is the first v1 effectful runtime message. Receiver-side accept/reject semantics are required, but no Unity-facing acknowledgement response is required in v1.
- Terminal placement acceptance is first-accepted-wins per active run. The first valid submission mutates platform-owned result state once; later duplicate, replayed, identical, or second terminal placement messages are rejected, diagnosed, and must not mutate playlist, stats, or platform result state again.
- Richer result semantics such as ties, DNF, no-contest, teams, score snapshots, and structured result reasons are reserved for future effectful runtime messages after a general result/versioning policy is chosen.
- Permanent elimination runtime message support is included, but platform/client enforcement behavior is not. No input suppression is required in this PRD.
- Structured GC diagnostics are first-class runtime messages with stable code, severity, source area, optional player index, bounded details, and bounded debug evidence. Diagnostic timing uses runtime message `sequence` and `runtimeTimeMs`; receiver wall-clock time is ingest metadata. Public diagnostics are `playerIndex`-only; hosted adapters may privately correlate diagnostics to platform player IDs after validation, but platform IDs do not appear in `RuntimeDiagnostic` fields, DevApp diagnostics UI, or hosted public diagnostics callbacks. Diagnostic codes use the current hard-break and permanent/revokable state vocabulary; stale deprecated-ID and out-of-action terms are not part of the v1 catalog. The detailed spine lives in `05-diagnostics-spine.md`.
- Diagnostic `sourceArea` values include both `runtime_messages` and `screen_space` so malformed screen-space anchors are not mislabeled as runtime-message problems.
- GC diagnostics must not depend on the package log level to be emitted.
- Optional Unity log capture is development-only, externally launch-controlled, and off by default. Launch policy can choose off, exception/error-only, warning-and-error, or explicit full-log capture. Full-log capture includes normal Unity `Log` output only when explicitly requested and must use strict filtering, rate limits, and size bounds. Captured Unity logs are not raw console records; the package wraps them as structured `gc.diagnostic` runtime messages with the active-run `sequence` and `runtimeTimeMs` before DevApp or hosted tooling stores them.
- Unity log records do not natively carry the runtime ordering fields needed for a coherent debug stream. Runtime messages and captured Unity logs use the same package-owned active-run clock; sinks sort by `runtimeTimeMs`, then `sequence`, then ingest time. Browser console or WebGL loader output that is only mirrored to DevTools is not assumed to be mergeable into that stream.
- Browser console capture and WebGL loader `print`/`printErr` mirroring are host-owned HTML/JavaScript debugging controls. They are not gated by Unity log capture, do not need to reach Unity runtime, and are not substitutes for `runtime_messages`.
- DevApp stores recent active-run diagnostics with sink-side aggregation, a 200-row ring buffer, and a hidden-by-default virtualized diagnostics console behind the existing Settings diagnostics toggle.
- Hosted WebGL exposes an API-level structured diagnostics callback path, but no host diagnostics UI is required.
- Boot-time profiling configuration may disable optional Unity-originated informational diagnostics, Unity log capture, `screen_space`, state snapshots, WebGL loader mirroring, and browser console capture. Core GC validation diagnostics for rejected runtime messages, invalid terminal placement, invalid mapping references, metadata fallback, unsupported retained APIs, and state no-op warnings must remain enabled. Transition messages remain enabled by default, and effectful messages cannot be disabled because they carry platform-state submissions.
- `gc.platform.json` is read-only platform metadata for Unity.
- Unity runtime receives a stable view of dashboard-managed game entries, entry limits, bot support, and player colors.
- Missing or invalid `gc.platform.json` creates a persistent editor and runtime warning.
- Missing or invalid `gc.platform.json` falls back to a `notdefined` entry with minimum defaults.
- Unity must not create, repair, or write `gc.platform.json`.
- Generated examples demonstrate setup, play, input polling by `playerIndex`, console logging, runtime state, screen-space/HUD essentials, explicit player state actions, diagnostics, and game over.
- Generated examples remain a wiring demo and do not become a polished mini-game.
- Unsupported multiplayer APIs are contained by default. Passive support checks may remain only when they return `false`; actionable multiplayer calls either compile only under `GC_ENABLE_UNSUPPORTED_MULTIPLAYER` or throw a clear unsupported error with diagnostics when retained for source-transition reasons.
- Unity-first rollout uses the staged adapter path from `07-cross-repo-rollout.md`: no immediate `gameProtocolVersion` bump, strict adapter validation, package-version skew checks, internal-game migration, and a legacy bridge removal checkpoint.
- A future `gameProtocolVersion` bump or broader runtime versioning boundary remains available after internal Unity games and JavaScript runtime semantics are aligned.

## Testing Decisions

- Tests should verify externally visible behavior and contracts, not private implementation details.
- Deep modules should be extracted around identity mapping, player state transitions, runtime message emission, diagnostics emission, platform metadata fallback, and runtime message translation.
- Unity EditMode tests should cover canonical index setup, deterministic shuffle fixtures, `playerIndex` DTO/runtime output payloads, old ID/name/state API compile-break guidance where practical, explicit permanent/revokable elimination and finish transitions, scaled game-time state timestamps, wrong-state warnings, state snapshots, transition messages, screen-space anchors, diagnostics emission, log-level bypass, Unity console mirroring, runtime output batching, boot-time output toggles, launch-only Unity log capture filtering including full-log opt-in, captured-log timestamp stamping, and the separation between Unity log capture and host-owned console mirroring.
- Unity local-play and DevApp message tests should cover seat-to-index routing, runtime snapshot identity, terminal placement submissions, diagnostics ingress through `runtime_messages`, active-run filtering, captured Unity log ingestion through `gc.diagnostic`, malformed runtime message batch rejection, sink-side aggregation, 200-row ring-buffer eviction, hidden diagnostics rail behavior, and diagnostics UI update batching.
- Client/SDK tests should cover hosted Unity ID-to-index input mapping, index-to-ID terminal placement mapping, new object-wrapped terminal placement handling including first-accepted-wins rejection of duplicate/replayed terminal submissions, temporary legacy array bridge behavior for older built games, screen-space/state mapping, diagnostics callback validation, public `playerIndex`-only diagnostics identity, and strict rejection of malformed runtime messages.
- DevApp tests should cover visible diagnostics for active Unity Editor runs, virtualized diagnostics rendering, diagnostic retention/reset across runs, manual clear/filter/search behavior, and local runtime input routing by active player index.
- Platform metadata tests should cover valid platform data, missing platform data fallback to `notdefined`, invalid platform data fallback, persistent warnings, runtime visibility, and no write-back.
- Generated-example tests should assert that the example source demonstrates lifecycle, inputs by `playerIndex`, runtime state/HUD essentials, explicit player-state actions, diagnostics, and game over.
- Existing contract fixture style should be reused for JSON-backed local play cases.
- Existing runtime message tests should be extended rather than replaced for DevApp protocol changes.
- Existing active-scene setup asset tests should be extended for generated example behavior.
- Existing client unit tests around game integration, runtime output, HUD rendering, and DevApp protocol should be extended for mapping and diagnostics.

## Implementation Review Rules

For every implementation task, the implementer and both review passes must check whether the change introduces, removes, renames, or changes behavior for any public Unity API, runtime message schema, DevApp/client/SDK protocol, diagnostics code or source area, metadata projection, generated example contract, or launch/configuration behavior.

Before marking a task `Completed`, update `## Breaking Change Ledger` with the task ID and affected surface, or record that the task introduced no API/protocol/runtime-contract break. The ledger is for release and migration accounting; it does not mean legacy behavior should be preserved beyond the staged bridge decisions in this PRD.

Review passes should treat a missing, vague, or incorrect ledger update as an actionable finding for the task under review.

## Tasks

Task status reflects implementation state. `Not started` means the work remains to be implemented against the current contract; it does not imply the current code already matches the plan.

Task IDs are stable. Dependency order controls execution order, so later-added task IDs may run before lower-numbered tasks when they are prerequisites.

| ID | Task | Status | Done when | Dependencies | Notes |
| --- | --- | --- | --- | --- | --- |
| Task 0 | Complete child planning and grilling gates. | Completed | Active Player Index mapping, diagnostics spine, runtime output contract, platform metadata fallback, Unity API migration, player state model, and cross-repo rollout plans are reviewed and marked implementation-ready. | None | Child plans are implementation-ready for planning; no code implementation has started from this PRD. |
| Task 1 | Introduce the Active Player Index contract across Unity setup, play, input, runtime output, and terminal placement flows. | Completed | Unity game code receives deterministically shuffled player indices, emits player indices, invalid mapping references can emit core diagnostics, and no runtime game-facing payload exposes platform player IDs or player names. | Task 0, Task 15 | Current runtime uses `playerId` in setup/play options, input routing, HUD, stores, and placements. |
| Task 2 | Add hosted Unity boundary mapping for IDs and indices. | Completed | Hosted Unity resolves and validates the platform/session active-run seed before Unity play payload creation, maps platform IDs to shuffled player indices for game input, and maps player indices back to platform IDs for screen-space anchors, runtime messages, and terminal placement bookkeeping without exposing IDs to Unity. | Task 1 | The client SDK currently forwards platform player IDs directly to Unity. |
| Task 3 | Add DevApp seat-to-index runtime routing. | Completed | Unity Editor runtime messages and routed inputs use active player indices while DevApp retains seat indices for local controller assignment and display only. | Task 1 | Seats are local development concepts and should not become game-facing identity. |
| Task 4 | Migrate Unity player identity API. | Completed | `Index` is canonical, public game-facing DTO/payload fields use `playerIndex`, active-player DTOs contain no names or platform IDs, old ID/name APIs fail at source level with migration guidance where possible, and input/store APIs are index-named. | Task 1 | Source API breaks intentionally; older built-game compatibility belongs to the rollout bridge. |
| Task 5 | Replace ambiguous player state APIs with explicit permanent/revokable state. | Completed | Old generic state methods fail at source level, permanent/revokable elimination and finish methods are implemented, new game-facing timestamps use scaled Unity game time, invalid transitions emit diagnostics and no-op, and post-game-over mutations no-op. | Task 4, Task 15 | Current elimination is a reversible boolean and finish is a single timestamp. |
| Task 6 | Update player store, placement, and runtime state semantics. | Completed | Player collections expose `Players...` state-filtered lists with bot/non-bot symmetry, old store collection names hard-break with substitute messages, broad placement criteria work with permanent/revokable state, and runtime state snapshots project canonical player state. | Task 5 | Existing store tracks eliminated/uneliminated lists only and HUD currently exposes a boolean eliminated field. |
| Task 7 | Add strict `runtime_messages` and `screen_space` output. | Not started | Unity extends the Task 15 runtime-message foundation with validated state snapshots, transition messages, effectful terminal placement with exact v1 schemas, first-accepted-wins terminal behavior, validated `screen_space` anchors for overhead and player-position coordinates, batching rules, and boot-time output toggles. | Task 1, Task 5, Task 6, Task 15 | Output uses active-run context, sequence, runtime-relative timing, and strict per-type validation. |
| Task 8 | Add diagnostics sinks and development log capture. | Not started | DevApp and hosted callback paths ingest, validate, aggregate, and display or expose GC diagnostics from `runtime_messages`; optional Unity log capture is launch-controlled, filtered, rate-limited, timestamped with the active-run clock, can opt into full normal-log capture for development, and stays separate from host-owned WebGL/browser console mirroring. | Task 7, Task 15 | Core package diagnostics emission is established earlier in Task 15; DevApp is visible in v1, while hosted diagnostics UI is future work. |
| Task 9 | Expose read-only `gc.platform.json` data to Unity runtime with fallback defaults. | Not started | Unity runtime receives platform entries, limits, bot support, and colors when valid; missing or invalid data falls back to `notdefined` and emits persistent editor/runtime diagnostics and warnings. | Task 15 | Existing platform data reader is editor-focused and warning-only. |
| Task 10 | Stabilize HUD rendering around runtime state and screen-space anchors. | Not started | Score, lives, status, text, meter, placement, elimination state, finish state, overhead anchors, and player-position anchors are index-based, documented, and covered by tests in local and hosted paths. | Task 1, Task 6, Task 7 | HUD remains configurable, but state and coordinates come from canonical runtime output paths. |
| Task 11 | Expand generated/example setup to demonstrate the correct integration path. | Not started | Generated examples show setup, play, input polling by `playerIndex`, console logs, runtime state/HUD essentials, explicit state actions, diagnostics, and game over without becoming a full mini-game. | Task 4, Task 5, Task 8, Task 10 | Existing active scene setup already generates example scripts and should be evolved. |
| Task 12 | Contain unsupported multiplayer APIs behind explicit legacy behavior. | Not started | Multiplayer capability checks return false by default, actionable APIs are absent or throw clear unsupported errors unless `GC_ENABLE_UNSUPPORTED_MULTIPLAYER` is enabled, and all retained use is documented as unsupported temporary internal migration surface. | Task 15 | This is containment, not multiplayer feature work; diagnostics use the core emitter from Task 15. |
| Task 13 | Update documentation and domain glossary for the runtime contract. | Not started | Docs consistently explain Seat, Active Player Index, Platform Player Id, permanent/revokable elimination, permanent/revokable finish, runtime messages, screen-space anchors, diagnostics, and platform metadata fallback. | Task 1, Task 5, Task 7, Task 8, Task 9 | Existing docs mention `player.Id`, `PlayerName`, generic `SetEliminated`, `SetFinished`, and player-id HUD patterns. |
| Task 14 | Add cross-repo validation for the Unity-first runtime contract. | Not started | Unity tests, DevApp/client tests, temporary older-built-game bridge tests, and focused integration checks pass for shuffled index mapping, object terminal placement, runtime messages, screen-space anchors, diagnostics, platform metadata fallback, HUD, and generated examples. | Task 2, Task 3, Task 7, Task 8, Task 11, Task 15 | Use the open-Editor Unity test bridge for package validation where practical. |
| Task 15 | Install runtime output foundation and core diagnostics emitter before API/state/metadata migrations. | Completed | GC diagnostics can be emitted as `gc.diagnostic` runtime messages through a minimal `runtime_messages` foundation: active-run clock, monotonic sequence, diagnostic payload schema, source-area/code validation, package-log-level bypass, and warning/error Unity console mirroring. | Task 0 | This foundation intentionally runs before Tasks 1, 5, 7, 8, 9, and 12 despite the appended stable ID; Task 7 owns full runtime output schemas, batching, state/transition/effectful messages, and `screen_space`, while Task 8 owns DevApp sinks, hosted callback exposure, and optional Unity log capture. |

## Breaking Change Ledger

Track implementation-time API/protocol/runtime-contract breaks here as each task completes. Include intentional breaks and any accidental or discovered breakages that implementation exposes. If a task introduces no break, add a row that records `None` for the affected surface so reviewers can verify the check happened.

| Task | Affected surface | Change | Intentional? | Compatibility / follow-up |
| --- | --- | --- | --- | --- |
| Task 15 | Runtime message schema; diagnostics payload/schema; diagnostics code/source-area catalog; hosted WebGL runtime output hook | Adds minimal `runtime_messages` envelope/schemaVersion 1 and `gc.diagnostic` message records with active-run sequence/time, bounded diagnostic payload fields including optional mapping context, the v1 diagnostics code/source-area catalog, and optional `window.gamingCouchRuntimeMessages` WebGL delivery. No public Unity game API, DevApp sink, SDK/client protocol, metadata projection, generated example contract, launch/config behavior, or `gameProtocolVersion` change. | Yes | Task 7 owns strict batching and additional runtime message schemas; Task 8 owns DevApp/hosted diagnostic sinks and optional Unity log capture. Existing legacy game-over/HUD outputs remain unchanged for this task. |
| Task 1 | Public Unity API; runtime game-facing play/player/HUD payload schema; Unity WebGL terminal placement bytes; Unity Editor DevApp runtime snapshot/game-over protocol; diagnostics mapping context usage; local-play fixture contract | `GCPlayerOptions` and player setup now expose `playerIndex`, type, and color without platform player IDs or names; active-player mapping uses deterministic FNV-1a hash-sort per run; input and HUD/screen-point game-facing data are keyed by `playerIndex`; terminal placement emits validated active player indices; DevApp runtime snapshots/game-over messages use `playerIndex` / `playerIndicesByPlacement`; invalid mapping references emit `gc.mapping.invalid_player_index` with opaque mapping context that does not expose stable keys. Because Task 1 removed runtime values for ID/name identity, retained `GCPlayer.Id`, `GCPlayer.PlayerName`, `GetPlayerById`, and `GetInputsByPlayerId` now fail at source level instead of silently aliasing IDs to indices or returning stale defaults. No metadata projection, generated example contract, launch/config behavior, or `gameProtocolVersion` change. | Yes | Task 2 owns hosted SDK/client ID-to-index adapter updates and legacy hosted bridge behavior; Task 3 owns broader DevApp seat display/routing work; Task 4 owns the broader Unity API migration and any remaining old source API hard-break guidance. |
| Task 2 | Hosted Unity SDK/client adapter protocol; Unity WebGL terminal placement bridge; hosted Unity play payload handling; hosted HUD/screen-space and runtime-message adapter bookkeeping | Hosted Unity now validates the active-run seed, builds the deterministic FNV-1a platform ID to active player index mapping in the SDK adapter, sends Unity only `{ seed, activePlayers }`, maps hosted controller input from platform player IDs to `playerIndex`, maps Unity-originated player-index HUD/screen-space data and runtime-message references back through private platform-ID bookkeeping, and maps object-wrapped `{ playerIndicesByPlacement }` game-over results back to platform `playerIdsByPlacement`. The Unity package treats `activePlayers` as pre-mapped rather than shuffling fallback seat keys again, and the WebGL bridge now emits the object-wrapped placement result. No public Unity game API, DevApp local seat protocol, generated example contract, launch/config behavior, metadata projection, or `gameProtocolVersion` change. | Yes | Legacy bare-array hosted game-over results remain accepted as platform `playerIdsByPlacement` for already-built games. Task 7 owns strict effectful runtime-message schemas and `screen_space`; Task 8 owns hosted/DevApp diagnostics sinks and UI exposure. |
| Task 3 | DevApp Unity Editor runtime ingress schema; DevApp runtime session/client protocol; Unity Editor DevApp input routing | DevApp now accepts and stores runtime snapshot seats as `playerIndex` plus local-only `seatIndex`, stores runtime game-over results as `playerIndicesByPlacement`, and routes local controller input to Unity Editor with `payload.playerIndex` while retaining `targetSeat`/seat indices for local assignment, display, coalescing, and logs. Embedded JS/local-runner game APIs keep legacy one-based `playerId` inputs/game-over calls through a narrow DevApp compatibility adapter, while the runner emits zero-based `playerIndex` / `playerIndicesByPlacement` on the runtime protocol. No public Unity game API, SDK protocol, diagnostics catalog, metadata projection, generated example contract, launch/config behavior, or `gameProtocolVersion` change. | Yes | Task 7 owns the fuller strict runtime output schemas; JavaScript runtime semantic migration remains out of scope for this Unity-first task. |
| Task 4 | Public Unity API; game-facing active-player DTO; Unity package internal setup/store/input naming | Replaces game-facing `GCPlayerOptions` usage with `GCActivePlayerOptions` carrying only `playerIndex`, type, and color; leaves `GCPlayerOptions` as a hard-obsolete source-level migration marker; moves `GCPlayerSetupOptions` out of the public surface and renames its setup identity field to `playerIndex`; keeps `GCPlayer.Id`, `GCPlayer.PlayerName`, `GamingCouch.GetInputsByPlayerId`, and `GCPlayerStore.GetPlayerById` as hard-obsolete guidance symbols; updates public setup/play/input/store APIs to use index-named signatures and `GCActivePlayerOptions`. Internal adapter identity retains platform ID only as `platformPlayerId`; DevApp legacy `payload.playerId` compatibility ingress is unchanged. No runtime message schema, metadata projection, generated example contract, launch/config behavior, or `gameProtocolVersion` change. | Yes | Task 5 owns state API replacement and timestamp changes. Task 7/8 own strict runtime output and diagnostics sinks. Remove hard-obsolete source symbols after source migration per the post-legacy cleanup ledger. |
| Task 5 | Public Unity API; generated example contract; Unity package internal player state callbacks; diagnostics payload behavior; NGO broad state sync; placement/HUD state consumption; local Unity host validation fixture | Adds explicit `GCPlayer` permanent/revokable elimination and finish methods, enum-backed state properties, derived booleans, scaled Unity `Time.time` `GameTime` timestamps, and event-args state callbacks; removes old reversible callback fields from the public surface; keeps `SetEliminated`, `SetUneliminated`, and `SetFinished` as hard-obsolete source-level migration markers. Generated active-scene setup now calls the explicit permanent finish API so generated source continues compiling against the new public API. The local Unity host validation fixture source now uses explicit permanent finish/elimination calls and state-event callbacks so package tests compile against the new API. Invalid duplicate, revoke, transition, clamped value, and post-game-over player mutator calls now emit existing `gc.state.*` diagnostics and no-op where required. Store, placement, HUD, and NGO listeners consume broad state through the new events/derived booleans. No new diagnostics code/source area, full runtime state snapshot schema, DevApp/client/SDK protocol, metadata projection, launch/config behavior, or `gameProtocolVersion` change. | Yes | Task 6 owns state-specific store collection renames and runtime state semantics beyond broad eliminated/finished behavior. Task 7 owns full state snapshot/transition runtime messages. Task 10 owns HUD rendering redesign from runtime state. Task 11/13 own broader generated examples and docs migration. |
| Task 6 | Public Unity player store API; runtime state snapshot projection/model; Unity package internal placement/HUD state consumption; local Unity host validation fixture | Adds canonical `Players...` player-store collections for all players, bot/non-bot partitions, broad uneliminated/eliminated/finished state, and permanent/revokable eliminated/finished state; keeps old store enumerable/count/collection names only as hard-obsolete source-level migration markers with substitute guidance. Package internals and the local Unity host validation fixture now consume `Players...` names instead of old store aliases, while broad placement criteria continue to use permanent/revokable-aware booleans and `GameTime` timestamps. Adds an internal `gc.state.snapshot` payload projection with normalized game status and dynamic player fields (`playerIndex`, score, lives, status/statusText, meter, one-based placement, elimination state, finish state) while keeping static type/color out of dynamic snapshots. No runtime message emission, batching, transition messages, terminal-placement runtime-message behavior, DevApp/client/SDK protocol, metadata projection, generated example contract, launch/config behavior, or `gameProtocolVersion` change. | Yes | Task 7 owns strict runtime-message emission schemas, batching, transition messages, boot toggles, `screen_space`, and effectful terminal placement. Task 10 owns HUD rendering redesign from runtime state. Remove hard-obsolete store names after the post-legacy cleanup condition is met. |

## Out of Scope

- No immediate `gameProtocolVersion` bump implementation in this Unity-first slice; the staged adapter rollout preserves a later bump or broader versioning decision.
- No full DevApp versioning or incompatible-version UX redesign.
- No JavaScript game runtime migration in this Unity-first slice. JS migration should be planned as the next tightly coupled follow-up.
- No platform-to-runtime message implementation beyond reserving the future ingress shape during runtime output planning.
- No hosted diagnostics UI.
- No hosted browser-console diagnostics UI or durable storage for raw WebGL loader/browser console output.
- No v1 multi-threaded Unity log capture; `Application.logMessageReceivedThreaded` can be considered later if a thread-safe queue and main-thread drain are needed.
- No player-client death screen, death sound, winning-player sound, or playlist victory sound implementation.
- No input suppression or enforced client behavior for eliminated players.
- No broad HUD toolkit beyond consuming canonical runtime state and screen-space anchors.
- No supported multiplayer feature work.
- No Unity writes, repair, or bootstrap behavior for `gc.platform.json`.

## Further Notes

- Future player-client elimination and victory features should consume `runtime_messages` rather than requiring Unity games to be updated.
- Future HUD rendering should treat HUD as a configurable consumer of canonical runtime state and `screen_space`, not the source of state truth.
- Future runtime/platform capability planning should leave room for capability versioning, either as versioned capability names or explicit version fields such as `runtime_messages.v1` and `screen_space.v1`.
- The temporary runtime bridge for older built Unity games is a one-off internal migration bridge, not the long-term versioning or deployment policy.
- The fallback platform entry name is `notdefined`, intentionally avoiding `undefined`.
- Developer diagnostics should be treated as product infrastructure, not just logging, because they are the bridge to better DevApp and hosted developer tools.
- Raw browser console history is not a reliable data source for product tooling. If console-like information needs durable ordering, it must be captured and stamped at the point of emission or treated as best-effort host debug output.

## Post-Legacy Cleanup Ledger

Remove these temporary surfaces after all internal games are migrated off the older built-game bridge and source-level hard-obsolete APIs:

- Remove legacy `playerIdsByPlacement` / array-shaped terminal placement acceptance from platform, client, SDK, DevApp, and Unity local-play adapters.
- Remove temporary platform/client/SDK mapping code that adapts already-built ID-based Unity games.
- Remove hard-obsolete Unity source symbols after their substitute messages have served the migration, including old ID/name APIs, old state methods, and old store collection names.
- Remove temporary HUD adapters that derive old boolean `eliminated` payloads after platform/client HUD rendering consumes canonical runtime state and `screen_space`.
- Remove `GC_ENABLE_UNSUPPORTED_MULTIPLAYER` and any retained unsupported multiplayer stubs once no migrated game needs the legacy path.
