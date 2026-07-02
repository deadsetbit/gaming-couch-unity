# Gaming Couch Unity Backlog

Status: Idea backlog
Last updated: 2026-07-03
Owner: Gaming Couch Unity package team

## Purpose

Track future package ideas that are worth preserving but are not yet approved PRDs, implementation tasks, or roadmap commitments.

This backlog is intentionally lighter than the active architecture roadmap. Items here can be rough, speculative, or waiting for more product/design input. Promote an item into a focused PRD or task file before implementation starts.

## How To Use This File

- Add ideas when they are plausible future work but too early for an implementation plan.
- Keep entries short enough to scan.
- Prefer linking to existing PRDs/task files instead of duplicating their details.
- Do not treat backlog order as priority.
- Do not implement directly from this file. First promote the item into a scoped PRD, task plan, or existing roadmap row.

## Promotion Checklist

Before promoting a backlog item, answer:

- Which user workflow does this improve?
- Is the target workflow for ground-up Gaming Couch games, ported Unity games, or both?
- Does this change public Runtime API, generated Example Assets, editor setup, WebGL output, DevApp behavior, or hosted platform behavior?
- If package/platform APIs change, does `gameProtocolVersion` need to be bumped, or can legacy support be preserved without a protocol change?
- What is the smallest useful vertical slice?
- What docs, generated examples, and tests need to move with the slice?

## Backlog

| ID | Area | Idea | Why | Promotion trigger | Notes |
| --- | --- | --- | --- | --- | --- |
| B001 | Game authoring ergonomics | Create a dedicated GC game authoring ergonomics PRD. | The package needs a coherent plan for making new GC-native games and ported Unity games easier to build. | We are ready to define concrete public API, generated example, or editor workflow changes. | Keep this separate from low-level architecture cleanup. |
| B002 | Player input API | Expose current player inputs directly from `GCPlayer`, for example `player.Inputs` and/or `player.TryGetInputs(out inputs)`. | Current game code must call `GamingCouch.Instance.GetInputsByPlayerIndex(player.Index)` and manually route input into player logic. | We decide the canonical convenience API shape. | Keep `GamingCouch` as the likely source of truth unless a PRD proves ownership should move. |
| B003 | Porting input adapter | Explore a Unity-friendly adapter for existing controllers that currently use `Input.GetAxis`, `Input.GetButton`, or Unity Input System actions. | Ported games should not need a full controller rewrite before they can run on Gaming Couch. | We identify the first legacy input pattern to support. | Make clear whether this is a migration bridge, a supported long-term API, or both. |
| B004 | Generated examples | Improve generated Example Assets and docs so they demonstrate a small playable loop, not only setup wiring. | Ground-up users learn the intended architecture from generated code. | A concrete ergonomics slice needs sample code to show the happy path. | Completed runtime-contract work already expanded the generated examples; avoid duplicating that scope. |
| B005 | Porting guide | Add a porting guide for adapting an existing Unity local multiplayer game to Gaming Couch. | Ported projects need different guidance than clean new projects. | We have one or two real migrated games to extract patterns from. | Include input, players, GameOver, HUD/state, WebGL export, and unsupported multiplayer caveats. |
| B006 | Start Screen authoring guidance | Consider Start Screen affordances that distinguish "new game setup" from "port existing game setup." | The editor should guide different user paths without hiding important GC concepts. | The authoring ergonomics PRD chooses concrete editor workflow changes. | Coordinate with Start Screen Readiness work instead of expanding the window opportunistically. |
| B008 | Runtime state semantics | Decide `SetMeter` payload semantics: keep `meter` a generic developer-facing progress field, split it into platform-named semantic fields, or add capability metadata. | `SetMeter` is preserved as semantic state for v1 but the name does not say what game fact the platform should infer from the value. | A grilling session decides the meter contract shape. | Deferred contract follow-up carried from the runtime-contract player-state model (see `docs/adr/0003-permanent-revokable-player-state.md`). |
| B009 | Runtime state semantics | Decide whether player-state mutators need stable elimination/finish `reason` codes plus bounded developer text. | `reason` is free-form developer text today; platform-facing reason semantics should stay separate from debug-only explanation text. Transition `reasonText` is bounded; snapshots deliberately omit `reason` — keep that split unless a product use case needs durable reason history. | Future contract planning takes up reason semantics. | Deferred contract follow-up carried from the runtime-contract player-state model (see `docs/adr/0003-permanent-revokable-player-state.md`). |
| B010 | Platform metadata | Add reserved future normalized platform-metadata fields intentionally when needed: runtime capabilities, localized entry names/descriptions, audio asset references for elimination/victory features, feature flags, controller configuration, dashboard-owned runtime behavior toggles, upload validation metadata. | Future fields must be added to the normalized `GCPlatformRuntimeView` projection deliberately; never expose a raw `metadata`/`properties` bag to game runtime code. | A concrete feature needs one of the reserved fields. | Reserved "Future Metadata" list carried from the runtime-contract platform-metadata view (see `docs/adr/0007-readonly-platform-metadata-notdefined.md`). |
| B011 | Multiplayer transport | Harden or delete the define-gated NGO/WebGL transport (`Runtime/Unity/NGO/Transport`): stale `.jslib` bridge behavior, payload slicing, static server state, unused transport helpers, and missing transport tests. | The transport is contained behind `GC_ENABLE_UNSUPPORTED_MULTIPLAYER` and excluded from default builds, but the dormant code was never hardened. | We decide to re-support Gaming Couch online multiplayer, or decide to remove the legacy transport entirely. | Carried from cleanup triage Task 2. Containment landed via "Contain unsupported Unity multiplayer APIs"; treat any revival as behavior work validated with tests and WebGL smoke coverage. |
| B012 | Runtime API cleanup | Finish the Runtime public-surface cleanup: replace mutable `List<T>` store views on `GCPlayerStoreOutput<T>` with read-only views, hide or internalize `GCPlayerStoreInput<T>.AddPlayer`, and remove hard-error `[Obsolete]` stubs (`GCNameTag`, legacy store members) after the migration window. Also keep reviewing public Runtime surfaces that feel internal, deprecated, or awkward for game authors. | A cleaner API improves both new-game and porting workflows; the remaining surfaces feel internal or are dead compile-error stubs. | Cleanup requires compatibility decisions (staged removal, `gameProtocolVersion` impact) beyond simple deletion. | Carried from cleanup triage Task 3 and merged from former B007. Most deprecations already landed: legacy HUD update API removal, legacy player id routing removal, right-stick input removal, player options API cleanup, hard-error obsolete migration markers. |

## Related Planning Files

- `docs/architecture/gamingcouch-start-screen-prd.md`
- `docs/adr/` (distilled decisions from completed planning docs)
