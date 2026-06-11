## Problem Statement

GamingCouch player state is growing from simple public mutators into a platform contract surface: elimination and finish states, score, lives, status, meter, placement, HUD state, runtime messages, diagnostics, and game-over output all depend on the same facts. Today those responsibilities are spread across the player object, player store, game orchestration, HUD projection, and runtime message projection. That keeps the public API easy to call, but it makes the core harder to reason about as new state features arrive.

The immediate risk is not only code size. The risk is semantic drift: one path can treat a change as latest visual state while another path treats it as an ordered game fact. That matters especially for hot calls such as meter updates and for Unity timing boundaries where several physics ticks can happen before one rendered frame. The client and SDK must not miss accepted semantic transitions because Unity batched output for transport efficiency.

## Solution

Deepen player state transitions into a package-internal module with a small, stable interface. Public `GCPlayer` mutators and callbacks remain the game developer surface, but the transition rules, no-op behavior, diagnostics, transition records, and dirty latest-state projection flags are decided in one place.

The refactor should preserve existing game-facing event compatibility while giving GamingCouch core one authoritative path for accepted player state changes. Semantic transitions are ordered facts and are not coalesced away. Latest-state projections, such as snapshots and HUD output, may be coalesced at rendered-frame boundaries as transport and presentation optimizations.

## User Stories

1. As a Unity game developer, I want existing `GCPlayer` mutator calls to keep working, so that upgrading the package does not force game code rewrites.
2. As a Unity game developer, I want existing public player events to keep firing with the same observable meaning, so that current games do not have to rewire event listeners.
3. As a Unity game developer, I want duplicate state calls to no-op consistently, so that accidental repeated calls do not create false runtime facts.
4. As a Unity game developer, I want invalid revokes and post-game-over mutations to produce diagnostics, so that contract mistakes are visible during development.
5. As a Unity game developer, I want revokable and permanent elimination to behave consistently, so that respawn and elimination rules are predictable.
6. As a Unity game developer, I want revokable and permanent finish to behave consistently, so that finish-line and race-style games can model temporary and final finish facts.
7. As a Unity game developer, I want score, lives, status, and meter changes to follow the same accepted-change path, so that state features do not drift apart.
8. As a Unity game developer, I want per-frame meter updates to be cheap, so that I can update semantic progress without manually throttling package APIs.
9. As a Unity game developer, I want no-op calls to be especially cheap, so that repeated setting of the same value does not allocate or flood output.
10. As a Unity game developer, I want game logic timing to remain based on Unity game time where appropriate, so that time scale and pause behavior stay intuitive for gameplay.
11. As a client or SDK consumer, I want semantic transitions to arrive in sequence order, so that I can reconstruct meaningful state changes.
12. As a client or SDK consumer, I want snapshots to represent current state, so that I can recover or render without replaying every transition.
13. As a client or SDK consumer, I want transition batching to be an output optimization only, so that transport details do not change semantic truth.
14. As a client or SDK consumer, I want meter changes to be semantic transition records, so that platform features can react to progress changes when they matter.
15. As a client or SDK consumer, I want rendered-frame freezes not to hide physics-step transitions, so that eliminate-then-respawn sequences remain observable.
16. As a platform implementer, I want one internal transition result model, so that runtime messages, snapshots, HUD, placement, diagnostics, and game-over output consume the same accepted facts.
17. As a platform implementer, I want legacy and new messaging concerns separated, so that this refactor does not reintroduce legacy transport coupling.
18. As a package maintainer, I want the player object to become thinner, so that future state fields can be added without duplicating transition rules in several places.
19. As a package maintainer, I want tests around public behavior rather than private method calls, so that internals can be reorganized safely.
20. As a package maintainer, I want protocol compatibility risks called out explicitly, so that `gameProtocolVersion` changes remain a deliberate product decision.
21. As a DevApp user, I want diagnostics and runtime state to stay understandable, so that debugging a local run does not require knowing internal package layout.
22. As a hosted platform operator, I want bounded runtime output behavior, so that high-frequency state changes do not create unbounded memory or message growth.

## Implementation Decisions

- Preserve public `GCPlayer` mutator names and existing public event listener behavior for this refactor. A public event migration is out of scope unless the user explicitly approves a compatibility break.
- Add a package-internal player transition module that owns accepted/rejected state transitions for elimination, finish, score, lives, status, and meter.
- The internal transition module should expose a narrow result shape: accepted change, rejected/no-op reason, previous value, new value, semantic transition kind, optional bounded reason text, game-time timestamp, runtime timestamp or runtime sequencing hook, and latest-state dirty flags.
- Keep `GCPlayer` as the game-facing API facade. It should call into the transition module, apply accepted results, emit existing public callbacks, and route accepted semantic changes to internal consumers.
- Introduce one package-internal state-change path for core consumers. Player store, placement, runtime messages, snapshots, HUD state, and game-over behavior should not each rediscover transition meaning from different callbacks.
- Semantic runtime transition messages are ordered facts. They must not be collapsed into only the last value per rendered frame.
- Latest-state projections are current truth. Snapshots, HUD output, and screen-space/presentation consumers may coalesce to the latest state when multiple accepted changes happen before a transport flush.
- Meter is semantic runtime state in v1. `SetMeter` remains the public API for this slice, but accepted meter changes must be available as ordered transition facts.
- Per-frame calls such as meter updates must be optimized inside the package. Game developers should not need to debounce or manually reason about Unity frame cadence to avoid package overhead.
- Unity `LateUpdate` can remain a transport flush seam for rendered-frame batching, but it must not define semantic truth. FixedUpdate, Update, and other game-code call sites all create accepted changes at call time.
- If physics simulation runs multiple times before the next rendered frame, accepted semantic transitions from each simulation step must remain ordered. Latest-state snapshots may show only the final state for that rendered frame.
- Effectful messages such as game-over must preserve ordering by flushing pending semantic state before, or in the same ordered batch as, the effectful message.
- Runtime messages and diagnostics use unscaled active-run-relative runtime timing for ordering. Game-facing state timestamps continue to use scaled Unity game time where the contract already says they are game-rule timing.
- This refactor should not change the new messaging API shape unless implementation exposes a contract gap. If a public package/platform API change becomes necessary, stop and ask whether `gameProtocolVersion` should be bumped.
- Legacy messaging cleanup is out of scope. Do not add new behavior to legacy bridges except temporary compatibility adaptation already required by the runtime contract plan.
- Treat free-form public `reason` strings as developer text for this slice. Keep runtime-facing reason text bounded where emitted. Stable reason codes and `SetMeter` naming improvements are deferred contract follow-ups.

Decision-rich internal shape to guide implementation, not a required exact API:

```csharp
internal readonly struct GCPlayerTransitionResult
{
    public bool Accepted { get; }
    public GCPlayerTransitionKind Kind { get; }
    public int PlayerIndex { get; }
    public object PreviousValue { get; }
    public object Value { get; }
    public string ReasonText { get; }
    public float ChangedAtGameTime { get; }
    public bool EmitsSemanticTransition { get; }
    public bool MarksLatestStateDirty { get; }
}
```

## Testing Decisions

- Tests should verify external behavior through public APIs, runtime output, diagnostics, and observable store/game state, not private implementation details.
- Add or extend editor tests around player state model behavior: accepted transitions, duplicate no-ops, invalid revokes, revokable-to-permanent promotion, finish/elimination coexistence, post-game-over no-ops, public callbacks, and timestamps.
- Add or extend runtime output contract tests around ordered transition messages, snapshot coalescing, game-over ordering, and meter semantics.
- Add compatibility tests proving existing public event listeners still see the expected changes.
- Add focused hot-path tests or lightweight allocation/performance guard tests for repeated no-op calls and high-frequency meter changes if the Unity test environment can measure them reliably. If not reliable, document the optimization expectations and cover them through code structure plus regression tests.
- Prior art exists in the current editor test suite for player state, runtime output, diagnostics, active run projection, DevApp runtime messages, and local play sessions. Follow those test styles before adding new test harnesses.
- Prefer the open-Editor Unity test bridge for validation when available through the local host Unity project path.

## Tasks

| ID | Task | Status | Done when | Dependencies | Notes |
| --- | --- | --- | --- | --- | --- |
| Task 1 | Add the package-internal player transition model and route one low-risk state family through it. | Completed | A public mutator still behaves the same externally, invalid/duplicate calls keep their diagnostics/no-op behavior, and tests prove accepted/rejected results are represented by the new internal model. | None | Start with one state family that exercises old/new value, reason, timestamp, and dirty-output behavior without changing public API. |
| Task 2 | Route elimination and finish transitions through the internal transition model. | Completed | Permanent, revokable, revoke, duplicate, invalid, promotion, coexistence, timestamp, and public callback behavior match the contract and are covered by tests. | Task 1 | Public `GCPlayer` listener compatibility is required. |
| Task 3 | Route score, lives, status, and meter through the same accepted-change path. | Completed | Accepted changes for score, lives, status, and meter produce one consistent internal change record, no-op calls stay cheap, clamping behavior remains correct, and tests cover meter as semantic state. | Task 1 | Meter may be called every physics or rendered frame, so this task must avoid per-call heap churn on the normal path where practical. |
| Task 4 | Consolidate package-internal consumers onto the accepted-change path. | Completed | Player store, placement, snapshots, HUD state, and game-over preparation consume accepted state changes consistently, with tests proving externally visible state remains correct. | Task 2, Task 3 | Avoid replacing public callbacks with a new public event surface. |
| Task 5 | Preserve ordered semantic runtime transitions while coalescing latest-state projections only. | Completed | Multiple accepted changes before one rendered-frame flush emit ordered transition records, while snapshots/HUD projection may reflect only the latest current state; tests include eliminate-then-respawn and meter-change cases. | Task 4 | `LateUpdate` may flush batches but must not erase semantic facts from FixedUpdate or Update. |
| Task 6 | Preserve effectful message ordering around game-over. | Completed | Pending state transitions and latest snapshot state are flushed before, or in the same ordered batch immediately before, game-over output; tests prove receivers can observe final semantic facts before the effectful result. | Task 5 | Keep the object-wrapped game-over result shape from the runtime output contract. |
| Task 7 | Add compatibility and protocol review coverage. | Completed | Tests or compile-time checks prove existing game-facing events and mutators still work, docs call out whether `gameProtocolVersion` remains unchanged, and any required protocol risk is escalated to the user before implementation proceeds. | Task 6 | Default assumption is no protocol bump because public/wire behavior should be preserved or clarified, not broken. |
| Task 8 | Validate hot-path behavior and document remaining contract follow-ups. | Completed | High-frequency meter/stat calls avoid avoidable allocations and unbounded queues, bounded reason text behavior is preserved, and docs retain TODOs for `SetMeter` naming and stable reason codes. | Task 7 | Do not solve `SetMeter` renaming or reason-code design in this slice. |

## Task 7 Protocol Review

- `gameProtocolVersion` remains unchanged at `1` for this player transition refactor.
- No protocol bump is required because the public `GCPlayer` mutator names, public callback delegate shapes, runtime transition message names, snapshot fields, HUD projection fields, and object-wrapped game-over payload shape remain compatible with the existing contract.
- New coverage locks this decision with compile-time public mutator/callback guards and an editor identity test that asserts the compatible protocol version is still `1`.
- If a later task changes public or wire-facing schema shape, pause before implementation and ask whether `gameProtocolVersion` should be bumped.

## Task 8 Hot-Path Review

- Repeated no-op score, lives, and meter calls return before public callbacks, accepted-transition fanout, runtime transition queueing, and latest-state snapshot dirtying.
- Accepted high-frequency player transitions retain ordered semantic facts, but the package now flushes pending runtime transition batches at an internal cap so the pending runtime-message list cannot grow without bound between rendered-frame flushes. This changes batch timing only, not message schema; latest-state snapshots remain coalesced to the next frame or forced output flush.
- Forced game-over output after an auto-flushed transition batch remains ordered: already emitted semantic transition batches precede the final batch, and the final latest-state snapshot still appears before `gc.game.game_over`.
- Runtime-facing `reasonText` continues to be truncated to `GCRuntimePayloadBounds.MaxReasonTextLength` when transition payloads are emitted. Public game-facing callbacks still receive the original developer reason string.
- Unity allocation measurement is not used as a hard editor-test gate in this slice because it is noisy across Editor and bridge runs. Coverage instead locks the observable no-op behavior, bounded queue behavior, bounded runtime reason text, and the code structure that avoids transition/output fanout on duplicate calls.
- Deferred contract follow-ups remain deferred: do not rename `SetMeter` or introduce stable public reason codes without a separate protocol review and user decision.

## Out of Scope

- Renaming `SetMeter` or replacing meter with a new platform-named semantic field.
- Introducing stable public reason codes for all mutators.
- Removing or redesigning legacy messaging bridges.
- Replacing the new messaging API contract shape.
- Changing existing games to listen to a new public event API.
- Suppressing inputs for eliminated or finished players.
- Adding custom arbitrary game-defined runtime state.
- Broad placement comparer redesign beyond preserving current broad eliminated/finished behavior.
- Any `gameProtocolVersion` bump without explicit user decision.

## Further Notes

- Contract references: `docs/architecture/unity-runtime-contract/03-player-state-model.md`, `docs/architecture/unity-runtime-contract/04-runtime-output-contract.md`, `docs/architecture/unity-runtime-contract/05-diagnostics-spine.md`, and `docs/architecture/unity-runtime-contract/08-domain-glossary.md`.
- Architecture review artifact: `/private/var/folders/dp/t1bf4_g12pz_rd3dkbghklf40000gn/T/architecture-review-20260610-193232.html`.
- Existing docs already record that meter is semantic in v1, accepted meter changes must remain ordered transition facts in `runtime_messages`, snapshots/HUD may coalesce, and `SetMeter` naming plus reason-code design are deferred follow-ups.
- Current repository guidance says to load `AGENTS.local.md` before working and to ask before changing outside repositories.
