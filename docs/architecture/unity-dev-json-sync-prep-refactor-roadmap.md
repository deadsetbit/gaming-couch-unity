# Unity `gc.dev.json` Sync Prep Refactor Roadmap

Status: Active prep roadmap
Last updated: 2026-05-09
Owner: Gaming Couch Unity package team

## Purpose

Prepare `gaming-couch-unity` for the DevApp `gc.dev.json` sync PRD through small behavior-neutral refactors.

This roadmap does not implement JSON sync, custom inspector file editing, `gc.metadata.json` parsing, package version bumps, or Newtonsoft dependency changes. The goal is to create deeper modules so the later PRD implementation can swap the editor play settings source without spreading rules across `GamingCouch`, inspector code, restart code, and DevApp runtime snapshots.

## Source Context

Primary PRD:

- `/Users/anttil/dev/dsb/gamingcouch/client/docs/prd/gaming-couch-unity-dev-json-sync-prd.md`

Repos:

- Unity package repo: `/Users/anttil/dev/dsb/gaming-couch-unity`
- Gaming Couch main repo: `/Users/anttil/dev/dsb/gamingcouch/client`
- Main repo path is also defined by the local bridge file `AGENTS.local.md`; use that bridge if this workspace is moved on the same machine.

Important PRD decisions this prep work must preserve:

- `gc.dev.json` will become the canonical Unity editor play settings file for entry, seed, and seats.
- Unity must not keep old serialized editor play settings as a functional fallback after the PRD implementation.
- Unity will light-read `gc.metadata.json` for game identity, platform, entry labels, entry limits, bot support, and player colors.
- Inspector edits will use an in-memory dirty draft, explicit Apply/Revert, and conflict actions.
- Entering Play Mode or using Gaming Couch restart will auto-apply a valid non-conflicted draft, then capture editor play config once.
- Invalid or conflicted drafts will block Play Mode or Gaming Couch restart.
- Active Play Mode config changes will be deferred until restart or next Play Mode entry.
- Runtime public payloads `GCSetupOptions`, `GCPlayOptions`, and `GCPlayerOptions` must remain unchanged.

Out of scope for this roadmap:

- Implementing `gc.dev.json` or `gc.metadata.json` parsing/writing.
- Adding the custom inspector UI.
- Adding `com.unity.nuget.newtonsoft-json`.
- Bumping package version, release notes, or tags.
- Editing the Gaming Couch main repo without separate approval.

## Status Tracking

Overall status: Prep implementation in progress.

Current phase: Task 3 ready.

Next action: Implement Task 3 after the Task 2 commit.

| Task | Status | Notes |
| --- | --- | --- |
| 1. Editor Play Capture module | Done | Behavior-neutral extraction complete; second review-and-patch pass complete; parent validation passed. |
| 2. Seat Identity module | Done | Runtime-safe identity module complete; replacement second review-and-patch pass complete; parent validation passed. |
| 3. Local Project Root module | Not started | Small shared extraction from DevApp integration. |
| 4. Editor assembly and inspector host prep | Not started | Prep only; no `gc.dev.json` UI. |
| 5. Package metadata hygiene | Not started | Defer dependency/version changes unless a prior slice needs structure. |

## Refactor Sequence

### 1. Editor Play Capture Module

Extract current editor setup/play option creation from `GamingCouch`.

- Keep the existing serialized fields as the only adapter for now.
- Centralize seed resolution, player option creation, setup option creation, and validation errors.
- Preserve current behavior unless an existing bug is explicitly documented.
- Keep runtime public payloads unchanged.

Completion criteria:

- `GamingCouch` no longer owns the detailed editor play option construction.
- Current editor play setup and play still work from serialized settings.
- The module can later accept a `gc.dev.json` adapter without redesigning the call sites.

Task 1 implementation note, 2026-05-09:

- Changed paths: `Runtime/GamingCouch.cs`, `Runtime/Dev/GCEditorPlayCapture.cs`, `Runtime/Dev/GCEditorPlayCapture.cs.meta`, `docs/architecture/unity-dev-json-sync-prep-execution-tasks.md`, `docs/architecture/unity-dev-json-sync-prep-refactor-roadmap.md`.
- Verification: `git diff --check` passed during implementation, first review, second review-and-patch pass, and parent validation. Unity 2022.3 compile/import was not run in this environment.
- Scope remained prep-only; no JSON sync, metadata parsing, custom inspector UI, seat identity, dependency, version, or main-repo changes were made. The second pass kept setup option creation from touching play-only serialized fields and normalized the new script `.meta`.

### 2. Seat Identity Module

Preserve source seat index and color assignment separately from public `GCPlayOptions`.

- Keep `GCSetupOptions`, `GCPlayOptions`, and `GCPlayerOptions` unchanged.
- Track source seat indexes for captured editor play state.
- Make DevApp runtime snapshots read captured seat identity instead of inferring seat index from active player array position.

Completion criteria:

- Sparse enabled seats can be represented internally without changing public play payloads.
- DevApp snapshot seat indexes come from captured editor play state.
- Dense player IDs remain compatible with current runtime behavior.

Task 2 implementation note, 2026-05-09: Done.

- Changed paths: `Runtime/GamingCouch.cs`, `Runtime/Dev/GCEditorPlayCapture.cs`, `Runtime/Dev/GCSeatIdentity.cs`, `Runtime/Dev/GCSeatIdentity.cs.meta`, `Runtime/Dev/GCDevAppIntegration.cs`, `docs/architecture/unity-dev-json-sync-prep-execution-tasks.md`, `docs/architecture/unity-dev-json-sync-prep-refactor-roadmap.md`.
- Verification: `git diff --check` passed during implementation, parent review, replacement second review-and-patch pass, and parent validation. Parent validation also confirmed no `UnityEditor` references in the Task 2 runtime/dev path. Unity 2022.3 compile/import was not run in this environment.
- Scope remained prep-only; the replacement second pass kept null seat identities on the private play path compatible with dense fallback seats and prevented default identities from emitting invalid snapshot seat indexes. Public setup/play/player option DTOs, player setup index, spawn indexing, `GCPlayer.Id`, package metadata, JSON sync, metadata parsing, inspector UI, Task 3+, and main-repo files were left unchanged.

### 3. Local Project Root Module

Extract Unity project-root resolution and path normalization from `GCDevAppIntegration`.

- Reuse the module from current DevApp runtime registration.
- Keep behavior unchanged for macOS, Windows-style paths, and UNC-style paths.
- Leave future JSON readers to consume the same module later.

Completion criteria:

- Project-root resolution has one interface and one implementation.
- `GCDevAppIntegration` delegates to the shared module.
- The module is testable without WebSocket behavior.

### 4. Editor Assembly And Inspector Host Prep

Split current Editor menu behavior from future inspector behavior.

- Keep the existing Gaming Couch creation menu behavior.
- Add an editor-only shape where a future custom inspector can draw current serialized fields while excluding obsolete editor play settings.
- Do not add `gc.dev.json` UI, file polling, draft state, conflict state, or Apply/Revert behavior in this prep slice.

Completion criteria:

- Menu item code and future inspector host code have separate modules.
- There is a clear seam for a later custom inspector.
- Existing inspector behavior remains acceptable until the PRD implementation replaces it.

### 5. Package Metadata Hygiene

Prepare package structure for later editor-only JSON support.

- Do not add `com.unity.nuget.newtonsoft-json` during prep unless required by an earlier slice.
- Do not bump package version or create release/tag changes during prep.
- Keep package metadata changes behavior-neutral.

Completion criteria:

- Any package structure needed by the prep modules is in place.
- PRD dependency and release changes remain deferred to the PRD implementation.

## Compatibility Rules

- Keep `GCSetupOptions`, `GCPlayOptions`, and `GCPlayerOptions` unchanged.
- Keep existing serialized editor play fields functional until the PRD implementation replaces them.
- Prefer internal interfaces for prep modules.
- Do not edit the Gaming Couch main repo from this package roadmap unless separately approved.
- Do not modify unrelated untracked files.

## Test And Validation Plan

- Prefer pure tests for extracted editor play capture, seat identity, and project-root rules if a low-cost Unity test path exists.
- If no practical automated harness exists, validate each slice by compiling in Unity and manually checking current editor play behavior.
- For each slice, record behavior preserved, tests run, and any test gap before moving to the next slice.

## Handoff Protocol For Future Chats

Default handoff is one task per chat. For the execution task file, one orchestrating chat may run the whole sequence only by assigning exactly one task at a time to separate subagents and waiting for each task to finish before starting the next.

At the start of a task:

- Read this roadmap.
- Read the target files for that task.
- Confirm no unrelated worktree changes need to be touched.

At the end of a task:

- Update the status table in this file.
- Add a short note under the relevant task if scope changed.
- Record verification performed.
- Leave the next task status and next action clear.
