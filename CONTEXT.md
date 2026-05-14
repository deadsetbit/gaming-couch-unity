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

**Contract Fixture**:
An example local play file set with expected validation and capture results shared across engine packages.
_Avoid_: Unity test data

## Relationships

- The **Local Play Contract** consists of **Local Play Settings** and **Metadata**.
- **Local Play Settings** contain exactly eight **Seats**.
- Enabled **Seats** become **Active Players** during **Capture**.
- A **Capture** is stable for the active editor run until restart or the next Play Mode entry.
- **Contract Fixtures** verify the **Local Play Contract** for each engine package.

## Example Dialogue

> **Dev:** "If DevApp changes `gc.dev.json` while Unity is playing, do we update the players immediately?"
> **Domain expert:** "No. The next **Capture** uses the changed **Local Play Settings**; the current **Active Players** stay stable."

## Flagged Ambiguities

- "player" can mean a configured **Seat** or a runtime **Active Player**. Use **Seat** for the root roster slot and **Active Player** for captured runtime play.
- "portable" means preserving the **Local Play Contract** and **Contract Fixtures** across engine packages, not forcing shared implementation code.
