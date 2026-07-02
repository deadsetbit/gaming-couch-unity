# Gaming Couch Unity Package Context

This context records the domain language for the Gaming Couch Unity package. It keeps local play, runtime, and platform integration terms stable while the package architecture is deepened.

## Language

**Local Play Contract**:
The root file contract shared by DevApp and engine packages for local editor play.
_Avoid_: Unity settings format, engine-specific settings

**Local Play Settings**:
The selected entry, seed, and eight-seat roster stored in `gc.dev.json`.
_Avoid_: serialized editor play settings, scene play settings

**Platform Data**:
The game identity, platform, entries, player limits, bot support, and player colors read from `gc.platform.json`.
_Avoid_: secondary settings store

**Seat**:
One one-based local development and DevApp roster slot in the eight-seat roster, used for controller assignment and display before **Capture**. Seats are not game-facing runtime identity; a **Seat** may map to a different **Player Index** each run, and sparse **Seats** become dense **Players** at **Capture**.
_Avoid_: player, controller, runtime identity

**Player**:
An enabled **Seat** captured into runtime play, exposed to game code as `GCPlayer` and addressed by a **Player Index**.
_Avoid_: source seat, platform player

**Player Index**:
The zero-based, dense, run-scoped runtime participant identity exposed to game code as `playerIndex` and `GCPlayer.Index`. It is assigned from the captured roster by deterministic seeded ordering and frozen until the run restarts; the same human or bot may receive a different index in a later run.
_Avoid_: player id, platform id, seat index

**Seat Identity**:
Package-internal local-play provenance (`GCSeatIdentity`) that preserves each **Player**'s source seat index, stable key, label, player type, and color for DevApp and local routing after **Capture**.
_Avoid_: public play payload field, platform identity

**Capture**:
The snapshot of valid **Local Play Settings** used for setup and play until restart or the next Play Mode entry.
_Avoid_: live settings, current JSON

**Local Play Session**:
The Runtime-owned editor-only boundary (`GCLocalPlaySession`) that caches **Capture**, runs preflight, and exposes setup/play options and **Seat** identities without knowing the JSON implementation.
_Avoid_: JSON session, runtime JSON parser

**Editor Local Play Contract Provider**:
The Editor-owned provider (`GCDevJsonLocalPlaySessionProvider`) that reads, writes, and validates `gc.dev.json` and `gc.platform.json`, then maps the result into the **Local Play Session**.
_Avoid_: runtime provider, shared JSON implementation

**Contract Fixture**:
An example local play file set with expected validation and capture results shared across engine packages.
_Avoid_: Unity test data

**Platform Player Id**:
The platform-owned stable participant identifier used by hosted client, SDK, playlist, stats, and adapter bookkeeping. It is never exposed to game code, public **Runtime Messages**, public **Diagnostics**, or **Screen-Space Anchors**.
_Avoid_: game player id, runtime identity

**Platform Metadata Fallback**:
The degraded read-only platform view used when `gc.platform.json` is missing or invalid in local development, with `notdefined` game and entry values, built-in player color defaults, and warning **Diagnostics**. Unity never writes, repairs, or bootstraps `gc.platform.json`.
_Avoid_: repaired platform data, bootstrap defaults

**Permanent Elimination**:
The final elimination state set through `SetEliminatedPermanent(reason)`. It promotes an existing **Revokable Elimination** in place, cannot be revoked, and counts as eliminated at `GameOver()`.
_Avoid_: death flag, removal

**Revokable Elimination**:
The elimination state set through `SetEliminatedRevokable(reason)`. It counts as eliminated while active, can be cleared only with `SetRevokeEliminated(reason)`, and can be promoted by **Permanent Elimination**.
_Avoid_: temporary death, soft elimination

**Permanent Finish**:
The final finish state set through `SetFinishedPermanent(reason)`. It promotes an existing **Revokable Finish** in place, cannot be revoked, and counts as finished at `GameOver()`.
_Avoid_: win flag, placement

**Revokable Finish**:
The finish state set through `SetFinishedRevokable(reason)`. It counts as finished while active, can be cleared only with `SetRevokeFinished(reason)`, and can be promoted by **Permanent Finish**.
_Avoid_: temporary win, soft finish

**Runtime Messages**:
The `runtime_messages` output path for ordered semantic and effectful records emitted by the package, including state snapshots, player transitions, **Diagnostics**, captured Unity logs when enabled, and game over.
_Avoid_: log stream, event bus

**Screen-Space Anchors**:
The `screen_space` output path for hot latest-state normalized screen-coordinate anchors (`playerOverhead`, `playerPosition`) addressed by **Player Index**. Screen-space data is view-derived presentation data, not durable semantic game state.
_Avoid_: HUD state, semantic game state

**Diagnostics**:
Structured GC warning/error/info records emitted as `gc.diagnostic` **Runtime Messages** with stable codes. Diagnostics bypass package log-level filtering and never expose **Platform Player Id** values.
_Avoid_: console logs, custom diagnostics API

**Start Screen**:
The Unity editor window and inspector entry point that displays readiness and setup actions.
_Avoid_: setup wizard

**Start Screen Readiness**:
The model of active-scene facts, checklist rows, action metadata, and summary state used by the **Start Screen**.
_Avoid_: window state, UI-only checks

**Active Scene Setup**:
Safe setup applied to the currently active user scene, including creating or reusing `GamingCouch`, generated **Example Assets**, listener wiring, player prefab wiring, Build Settings, Game View aspect, and WebGL export setup where applicable.
_Avoid_: generated scene setup

**Example Assets**:
Generated editable project assets in `Assets/GamingCouch/GCExample`, including `GCGameExample.cs`, `GCPlayerExample.cs`, and `GCPlayerExample.prefab`. They are a wiring demo, not a full sample game.
_Avoid_: package samples

## Relationships

- The **Local Play Contract** consists of **Local Play Settings** and **Platform Data**.
- **Local Play Settings** contain exactly eight **Seats**.
- Enabled **Seats** become **Players** during **Capture**; sparse **Seats** become dense **Players**.
- **Players** are addressed by **Player Index** in game-facing runtime code.
- **Seats** are local development and DevApp-facing; **Player Index** is game-facing.
- **Seat Identity** preserves the **Seat**-to-**Player Index** mapping for local routing after **Capture**.
- A **Capture** is stable for the active editor run until restart or the next Play Mode entry.
- The **Editor Local Play Contract Provider** owns JSON parsing, writing, validation, and Newtonsoft usage.
- The **Local Play Session** owns active **Capture** caching and consumes only neutral provider results and issues.
- **Contract Fixtures** verify the **Local Play Contract** for each engine package.
- **Runtime Messages** and **Screen-Space Anchors** are the package's two output paths; **Diagnostics** travel as **Runtime Messages**.
- **Permanent** states promote matching **Revokable** states in place and count at `GameOver()`.
- The **Start Screen** renders **Start Screen Readiness** and exposes safe setup actions.
- **Active Scene Setup** changes the user's currently active scene or related editor launch settings.
- **Example Assets** are editable project content created or reused by **Active Scene Setup**.

## Example Dialogue

> **Dev:** "If DevApp changes `gc.dev.json` while Unity is playing, do we update the players immediately?"
> **Domain expert:** "No. The next **Capture** uses the changed **Local Play Settings**; the current **Players** stay stable."

## Flagged Ambiguities

- "player" can mean a configured **Seat** or a runtime **Player**. Use **Seat** for the root roster slot and **Player** for captured runtime play.
- "player id" can mean **Platform Player Id** or **Player Index**. Use **Platform Player Id** only for platform-owned correlation outside game-facing runtime code, and **Player Index** for game-facing runtime identity.
- "seat index" and **Player Index** are not interchangeable. Seat indexes are one-based local development and DevApp roster positions; **Player Index** is zero-based and dense for runtime game code.
- "eliminated" and "finished" each cover two states. Say **Permanent** or **Revokable** when the distinction matters.
- "portable" means preserving the **Local Play Contract** and **Contract Fixtures** across engine packages, not forcing shared implementation code.
