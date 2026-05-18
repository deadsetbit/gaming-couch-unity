# Gaming Couch Unity Package Context

This context records the domain language for the Gaming Couch Unity package. It keeps local play and platform integration terms stable while the package architecture is deepened.

## Language

**Local Play Contract**:
The root file contract shared by DevApp and engine packages for local editor play.
_Avoid_: Unity settings format, engine-specific settings

**Local Play Settings**:
The selected entry, seed, and eight-seat roster stored in `gc.dev.json`.
_Avoid_: serialized editor play settings, scene play settings

**Metadata**:
The game identity, platform, entries, player limits, bot support, and player colors read from `gc.metadata.json`.
_Avoid_: secondary settings store

**Seat**:
One stable local play slot in the eight-seat roster.
_Avoid_: player index, controller

**Active Player**:
An enabled **Seat** captured into runtime play with a dense player id.
_Avoid_: source seat

**Capture**:
The snapshot of valid **Local Play Settings** used for setup and play until restart or the next Play Mode entry.
_Avoid_: live settings, current JSON

**Local Play Session Seam**:
The Runtime-owned editor-only boundary that caches **Capture**, runs preflight, and exposes setup/play options and **Seat** identities without knowing the JSON implementation.
_Avoid_: JSON session, runtime JSON parser

**Editor Local Play Contract Adapter**:
The Editor-owned implementation that reads, writes, and validates `gc.dev.json` and `gc.metadata.json`, then maps the result into the **Local Play Session Seam**.
_Avoid_: runtime contract adapter, shared JSON implementation

**Contract Fixture**:
An example local play file set with expected validation and capture results shared across engine packages.
_Avoid_: Unity test data

**Start Screen**:
The Unity editor window and inspector entry point that displays readiness and setup actions.
_Avoid_: quick start window, setup wizard

**Start Screen Readiness**:
The model of active-scene facts, checklist rows, action metadata, and summary state used by the **Start Screen**.
_Avoid_: window state, UI-only checks

**Active Scene Setup**:
Safe setup applied to the currently active user scene, including creating or reusing `GamingCouch`, generated **Example Assets**, listener wiring, player prefab wiring, Build Settings, Game View aspect, and WebGL export setup where applicable.
_Avoid_: quick-start wording for current-scene setup, quick-start wiring

**Quick Start Scene**:
The generated example scene at `Assets/GamingCouch/GCExample/GamingCouchQuickStart.unity`.
_Avoid_: active scene setup, generated current-scene setup

**Example Assets**:
Generated editable project assets in `Assets/GamingCouch/GCExample`, including `GCGameExample.cs`, `GCPlayerExample.cs`, and `GCPlayerExample.prefab`.
_Avoid_: quick-start classes, package samples

## Relationships

- The **Local Play Contract** consists of **Local Play Settings** and **Metadata**.
- **Local Play Settings** contain exactly eight **Seats**.
- Enabled **Seats** become **Active Players** during **Capture**.
- A **Capture** is stable for the active editor run until restart or the next Play Mode entry.
- The **Editor Local Play Contract Adapter** owns JSON parsing, writing, validation, and Newtonsoft usage.
- The **Local Play Session Seam** owns active **Capture** caching and consumes only neutral provider results and issues.
- **Contract Fixtures** verify the **Local Play Contract** for each engine package.
- The **Start Screen** renders **Start Screen Readiness** and exposes safe setup actions.
- **Active Scene Setup** changes the user's currently active scene or related editor launch settings; it is not the **Quick Start Scene** path.
- The **Quick Start Scene** is generated example content that uses the same **Example Assets** but opens a separate scene asset.
- **Example Assets** are editable project content reused by both **Active Scene Setup** and the **Quick Start Scene**.

## Example Dialogue

> **Dev:** "If DevApp changes `gc.dev.json` while Unity is playing, do we update the players immediately?"
> **Domain expert:** "No. The next **Capture** uses the changed **Local Play Settings**; the current **Active Players** stay stable."

## Flagged Ambiguities

- "player" can mean a configured **Seat** or a runtime **Active Player**. Use **Seat** for the root roster slot and **Active Player** for captured runtime play.
- "portable" means preserving the **Local Play Contract** and **Contract Fixtures** across engine packages, not forcing shared implementation code.
- "quick start" previously covered both active-scene setup and the **Quick Start Scene**. Use **Active Scene Setup** for setup applied to the current user scene and **Quick Start Scene** for `GamingCouchQuickStart.unity`.
