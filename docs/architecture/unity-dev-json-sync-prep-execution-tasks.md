# Unity `gc.dev.json` Sync Prep Execution Tasks

Status: Prep tasks complete
Last updated: 2026-05-09
Owner: Gaming Couch Unity package team

## Source Context

Primary roadmap:

- `<this-repo>/docs/architecture/unity-dev-json-sync-prep-refactor-roadmap.md`

Primary PRD:

- `<gc-client>/docs/prd/gaming-couch-unity-dev-json-sync-prd.md`

Repos and bridge placeholders:

- Unity package repo: `<this-repo>`
- Gaming Couch main repo base path: `<gaming-couch-main-repo>`
- GC Client path: `<gc-client>`
- Resolve `<gc-client>` from `AGENTS.local.md` plus the `client` relative mapping from `AGENTS.md`.

## Execution Rules

- Execute tasks in order.
- A single orchestrating chat may run the full sequence, but Tasks 1-5 must not run in parallel. Start each subagent only after the prior task's diff and status updates are complete.
- Use a separate GPT-5.5 xhigh subagent for each task.
- Each subagent must re-read its target files from the current worktree before editing.
- Each subagent owns only its task files and must not revert unrelated changes.
- Before each task, run `git status --short` and preserve unrelated untracked files, especially `VERSIONING_PLAN.md` and `VERSIONING_PLAN.md.meta`.
- After each task, update this file and the roadmap status table.
- Do not edit the Gaming Couch main repo unless separately approved.
- Do not implement `gc.dev.json` sync, `gc.metadata.json` parsing, JSON file writes, inspector Apply/Revert, dirty/conflict state, Newtonsoft dependency, package version bumps, release notes, or tags in these prep tasks.

## Global Compatibility Rules

- Keep public runtime payloads unchanged:
  - `GCSetupOptions`
  - `GCPlayOptions`
  - `GCPlayerOptions`
- Keep current serialized editor play fields functional until the PRD implementation replaces them:
  - `gameModeId`
  - `playerData`
  - `numberOfPlayers`
  - `randomizePlayerIds`
- Prefer internal editor/dev modules over new public package interfaces.
- Keep non-WebGL/non-editor player compilation behavior-neutral. Editor/dev prep types referenced by `GamingCouch` must either compile without `UnityEditor` in all existing targets or all references to them must be guarded consistently at the existing call sites.
- If no Unity automated test harness exists, record the manual verification gap instead of creating a large harness.

## Status

Overall status: Prep tasks complete

Current task: None

Next action: Begin the PRD implementation only after explicit approval.

| Task | Status | Owner | Notes |
| --- | --- | --- | --- |
| 1. Editor Play Capture module | Done | GPT-5.5 xhigh subagent | Behavior-neutral extraction complete; second review-and-patch pass complete; parent validation passed. |
| 2. Seat Identity module | Done | GPT-5.5 xhigh subagent | Runtime-safe identity module complete; replacement second review-and-patch pass complete; parent validation passed. |
| 3. Local Project Root module | Done | GPT-5.5 xhigh subagent | Behavior-neutral resolver extraction complete; second review-and-patch pass complete; parent validation passed. |
| 4. Editor assembly and inspector host prep | Done | GPT-5.5 xhigh subagent | Editor-only asmdef and inactive inspector host prep complete; second review-and-patch pass complete; parent validation passed. |
| 5. Package metadata hygiene | Done | GPT-5.5 xhigh subagent | Placeholder package keywords replaced; second review-and-patch pass complete; parent validation passed; no Newtonsoft, version, dependency, release, or license changes. |

## Task 1: Editor Play Capture Module

### Objective

Extract Unity editor play setup/play option construction out of `GamingCouch` into a behavior-neutral module. Keep existing serialized inspector fields as the only source for now.

### Files

- `Runtime/GamingCouch.cs`
- `Runtime/Dev/GCEditorPlayCapture.cs`
- `Runtime/Dev/GCEditorPlayCapture.cs.meta`
- `docs/architecture/unity-dev-json-sync-prep-refactor-roadmap.md`
- `docs/architecture/unity-dev-json-sync-prep-execution-tasks.md`

### Implementation Steps

1. Add `Runtime/Dev/GCEditorPlayCapture.cs`.
2. Define internal non-serialized input types in `Runtime/Dev/GCEditorPlayCapture.cs`:
   - `GCEditorPlaySettingsSnapshot`
   - `GCEditorPlayPlayerSettings`
   Keep these types and `GCEditorPlayCapture` free of `UnityEditor` dependencies and available to the same compilation targets as the existing `GamingCouch` editor-play helpers. If an implementation wraps them in `#if UNITY_EDITOR`, it must also guard every `GamingCouch` reference to them and verify player compilation.
3. In `GCEditorPlayCapture`, add internal methods:
   - `CreateSetupOptions(snapshot): GCSetupOptions`
   - `CreatePlayOptions(snapshot): GCPlayOptions`
4. Preserve current behavior exactly:
   - `GCSetupOptions.mode = GCMode.Development`
   - `GCSetupOptions.isServer = true`
   - `GCSetupOptions.gameModeId = gameModeId`
   - seed uses `UnityEngine.Random.Range(1, 999999)`
   - randomized player IDs use `UnityEngine.Random.Range(1, 99)`
   - non-random player IDs are `i + 1`
   - duplicate active colors throw the existing error message shape
5. Update `GamingCouch`:
   - keep `PlayerEditorData` and all serialized field names in place
   - add a private adapter method that copies serialized values into `GCEditorPlaySettingsSnapshot`
   - replace `GetEditorSetupOptions()` and `GetEditorPlayOptions()` internals with delegation to `GCEditorPlayCapture`
6. Do not change `Awake`, `SetupDone`, `_EditorPlay`, restart flow, listener `SendMessage`, DevApp integration, inspector behavior, or `package.json`.

### Verification

- Run `git diff --check`.
- Compile/import in Unity 2022.3 if available.
- Confirm editor play still sends the same development setup and generated play options from serialized settings.
- Confirm duplicate selected colors still fail during play option creation.
- Confirm no JSON files are read or written.

### Status Update

After implementation:

- Mark Task 1 as `Done` or `Blocked` in both task files.
- Record files changed and verification run.
- Set current task to Task 2 if complete.

Task 1 implementation result, 2026-05-09: Done.

- Changed paths: `Runtime/GamingCouch.cs`, `Runtime/Dev/GCEditorPlayCapture.cs`, `Runtime/Dev/GCEditorPlayCapture.cs.meta`, `docs/architecture/unity-dev-json-sync-prep-execution-tasks.md`, `docs/architecture/unity-dev-json-sync-prep-refactor-roadmap.md`.
- Verification: `git diff --check` passed during implementation, first review, second review-and-patch pass, and parent validation. Unity 2022.3 compile/import was not run in this environment.
- Scope notes: No JSON sync, metadata parsing, custom inspector UI, seat identity, package dependency, package version, or main-repo changes were made. The second pass kept setup option creation from touching play-only serialized fields and normalized the new script `.meta`.

## Task 2: Seat Identity Module

### Objective

Preserve captured editor-play seat identity separately from public play payloads, so sparse source seats can later map to dense runtime players without losing source `seatIndex` or seat-slot color.

### Files

- `Runtime/GamingCouch.cs`
- `Runtime/Dev/GCEditorPlayCapture.cs`
- `Runtime/Dev/GCSeatIdentity.cs`
- `Runtime/Dev/GCSeatIdentity.cs.meta`
- `Runtime/Dev/GCDevAppIntegration.cs`
- `docs/architecture/unity-dev-json-sync-prep-refactor-roadmap.md`
- `docs/architecture/unity-dev-json-sync-prep-execution-tasks.md`

### Implementation Steps

1. Add internal `Runtime/Dev/GCSeatIdentity.cs`.
2. Include:
   - `playerId`
   - 1-based `sourceSeatIndex`
   - `label`
   - `GCPlayerType`
   - `GCPlayerColor`
   Keep `GCSeatIdentity` runtime-safe: no `UnityEditor` dependency and no editor-only type wrapper, because `GamingCouch` stores captured identity beside `playOptions` and platform fallback play paths must compile.
3. Extend Task 1 editor play capture to return a result containing both:
   - `GCPlayOptions`
   - matching `GCSeatIdentity[]`, matched by active-player order
4. Extend capture row data with a 1-based `sourceSeatIndex` independent of dense active-player ordinal.
5. When serialized editor settings build play options, set `sourceSeatIndex = i + 1`; future JSON adapters must be able to pass sparse source seats such as `1, 3, 8` while still producing dense `GCPlayOptions.players`.
6. Add a private/internal `Play(GCPlayOptions options, GCSeatIdentity[] seatIdentities)` path in `GamingCouch` that stores defensive copies.
7. Keep platform `GamingCouchPlay(string optionsJson)` compatible by creating dense fallback identities from `GCPlayOptions.players`.
8. Add `internal GCSeatIdentity[] GetCurrentPlaySeatIdentities()` returning a defensive copy.
9. Leave `GetCurrentPlayPlayerOptions()` unchanged.
10. Update `GCDevAppIntegration.BuildRuntimeSeats()` to use `GetCurrentPlaySeatIdentities()` and report `sourceSeatIndex`, not `index + 1`.
11. Do not change `GCPlayerSetupOptions.index`, `SetupPlayers` spawn indexing, `GCPlayer.Id`, or public DTOs.

### Verification

- Run `git diff --check`.
- Compile/import in Unity 2022.3 if available.
- Confirm current editor play still has dense runtime players.
- With `randomizePlayerIds = false`, confirm player IDs remain dense `1..n`.
- Confirm DevApp runtime snapshots read seat indexes from captured identity.
- If a low-cost test path exists, cover sparse source seats `1, 3, 8` producing dense players but snapshot seat indexes `1, 3, 8`.

### Status Update

After implementation:

- Mark Task 2 as `Done` or `Blocked` in both task files.
- Record files changed and verification run.
- Set current task to Task 3 if complete.

Task 2 implementation result, 2026-05-09: Done.

- Changed paths: `Runtime/GamingCouch.cs`, `Runtime/Dev/GCEditorPlayCapture.cs`, `Runtime/Dev/GCSeatIdentity.cs`, `Runtime/Dev/GCSeatIdentity.cs.meta`, `Runtime/Dev/GCDevAppIntegration.cs`, `docs/architecture/unity-dev-json-sync-prep-execution-tasks.md`, `docs/architecture/unity-dev-json-sync-prep-refactor-roadmap.md`.
- Verification: `git diff --check` passed during implementation, parent review, replacement second review-and-patch pass, and parent validation. Parent validation also confirmed no `UnityEditor` references in the Task 2 runtime/dev path. Unity 2022.3 compile/import was not run in this environment.
- Scope notes: `GCSeatIdentity` is runtime-safe and stores source seat index, label, type, and color beside public play options without changing public DTO shapes. Platform `GamingCouchPlay(string optionsJson)` now creates dense fallback identities, while editor play capture returns identities matched to dense active-player order. The replacement second pass kept null seat identities on the private play path compatible with dense fallback seats and prevented default identities from emitting invalid snapshot seat indexes. No JSON sync, metadata parsing, inspector UI, package dependency/version, Task 3+, public DTO, player setup index, spawn indexing, or main-repo changes were made.

## Task 3: Local Project Root Module

### Objective

Extract Unity project-root resolution and path normalization out of `GCDevAppIntegration` into a shared editor-only module that later JSON readers can reuse.

### Files

- `Runtime/Dev/GCDevAppIntegration.cs`
- `Runtime/Dev/GCLocalProjectRootResolver.cs`
- `Runtime/Dev/GCLocalProjectRootResolver.cs.meta`
- `docs/architecture/unity-dev-json-sync-prep-refactor-roadmap.md`
- `docs/architecture/unity-dev-json-sync-prep-execution-tasks.md`

### Implementation Steps

1. Add `Runtime/Dev/GCLocalProjectRootResolver.cs`.
2. Under `#if UNITY_EDITOR`, define:
   - `IGCLocalProjectRootResolver`
   - `GCUnityLocalProjectRootResolver`
3. Move current root/path behavior from `GCDevAppIntegration`:
   - `Path.GetFullPath(value)`
   - replace `\` with `/`
   - trim trailing `/`
   - preserve `/` as root
   - lowercase Windows drive paths and UNC-style paths
   - resolve project root from `Directory.GetParent(Application.dataPath)?.FullName`
   - fall back to `Application.dataPath` if parent resolution is empty
   - resolve project name from trimmed `Application.productName`, otherwise root directory name
4. Keep pure normalization separately callable for tests or characterization.
5. Update `GCDevAppIntegration.BuildRuntimeRegisterMessage()` to delegate `projectRootPath` and `projectName` to the resolver.
6. Remove only duplicated private path/project-name helpers from `GCDevAppIntegration`.
7. Leave WebSocket, snapshot, seat, and runtime message behavior unchanged.

### Verification

- Run `git diff --check`.
- Compile/import in Unity 2022.3 if available.
- Enter Play Mode with DevApp integration enabled and confirm `runtime_register.projectRootPath` and `projectName` match prior behavior.
- Characterize normalization for POSIX trailing slash, `/`, Windows drive paths, and UNC-style paths.

### Status Update

After implementation:

- Mark Task 3 as `Done` or `Blocked` in both task files.
- Record files changed and verification run.
- Set current task to Task 4 if complete.

Task 3 implementation result, 2026-05-09: Done.

- Changed paths: `Runtime/Dev/GCDevAppIntegration.cs`, `Runtime/Dev/GCLocalProjectRootResolver.cs`, `Runtime/Dev/GCLocalProjectRootResolver.cs.meta`, `docs/architecture/unity-dev-json-sync-prep-execution-tasks.md`, `docs/architecture/unity-dev-json-sync-prep-refactor-roadmap.md`.
- Verification: first and second review-and-patch passes confirmed by inspection that normalization preserves `Path.GetFullPath`, slash normalization, trailing slash trimming with `/` preservation, Windows drive and UNC lower-casing, root fallback, and project-name fallback behavior from the extracted source. `git diff --check` passed in both review passes and parent validation. Parent validation also confirmed resolver references remain in the editor-only path. Unity 2022.3 compile/import was not run in this environment.
- Scope notes: Project-root path normalization and project-name resolution now live in the editor-only resolver and DevApp runtime registration delegates to it. WebSocket, runtime snapshot, seat, public DTO, JSON sync, metadata parsing, inspector UI, package metadata, and main-repo files were left unchanged.
- Remaining Task 3 gates: none.

## Task 4: Editor Assembly And Inspector Host Prep

### Objective

Prepare the Editor-side structure for a future `GamingCouch` custom inspector without implementing JSON sync behavior or changing visible inspector behavior.

### Files

- `Editor/dsb.gamingcouch.editor.asmdef`
- `Editor/dsb.gamingcouch.editor.asmdef.meta`
- `Editor/GamingCouchEditor.cs`
- `Editor/GamingCouchEditor.cs.meta`
- `Editor/GamingCouchMenuItems.cs`
- `Editor/GamingCouchMenuItems.cs.meta`
- `Editor/GamingCouchInspectorHost.cs`
- `Editor/GamingCouchInspectorHost.cs.meta`
- `Runtime/GamingCouch.cs`
- `Editor/GamingCouchWebGLBuildMenu.cs` (inspect/compile impact only)
- `docs/architecture/unity-dev-json-sync-prep-refactor-roadmap.md`
- `docs/architecture/unity-dev-json-sync-prep-execution-tasks.md`

### Implementation Steps

1. Add `Editor/dsb.gamingcouch.editor.asmdef`.
2. Configure it as editor-only:
   - name: `GamingCouch.Editor`
   - include platform: `Editor`
   - reference runtime assembly `GamingCouch`
3. Add `[assembly: InternalsVisibleTo("GamingCouch.Editor")]` to the runtime assembly, preserving the existing NGO friend assembly, so future editor-only inspector code can consume internal Runtime/Dev prep modules without making them public.
4. Rename `Editor/GamingCouchEditor.cs` to `Editor/GamingCouchMenuItems.cs`.
5. Rename class to `GamingCouchMenuItems`.
6. Keep all existing `MenuItem` paths and object creation behavior unchanged.
7. Add inactive `Editor/GamingCouchInspectorHost.cs`.
8. Add an internal helper that can draw serialized `GamingCouch` fields while excluding:
   - `gameModeId`
   - `playerData`
   - `numberOfPlayers`
   - `randomizePlayerIds`
9. Do not register an active `[CustomEditor]` in this prep slice.
10. Keep the inspector host free of file I/O, polling, draft state, conflict state, `gc.dev.json` UI, play-mode hooks, and Apply/Revert behavior.
11. Do not remove or rename runtime serialized fields.

### Verification

- Run `git diff --check`.
- Compile/import in Unity 2022.3 if available.
- Confirm existing menu items still create a `GamingCouch` GameObject named `GamingCouch`.
- Confirm no `gc.dev.json` UI or inspector behavior is active.
- Confirm runtime behavior is unchanged.

### Status Update

After implementation:

- Mark Task 4 as `Done` or `Blocked` in both task files.
- Record files changed and verification run.
- Set current task to Task 5 if complete.

Task 4 implementation result, 2026-05-09: Done.

- Changed paths: `Editor/dsb.gamingcouch.editor.asmdef`, `Editor/dsb.gamingcouch.editor.asmdef.meta`, `Editor/GamingCouchMenuItems.cs`, `Editor/GamingCouchMenuItems.cs.meta`, `Editor/GamingCouchInspectorHost.cs`, `Editor/GamingCouchInspectorHost.cs.meta`, `Runtime/GamingCouch.cs`, `docs/architecture/unity-dev-json-sync-prep-execution-tasks.md`, `docs/architecture/unity-dev-json-sync-prep-refactor-roadmap.md`.
- Verification: implementation, first review-and-patch pass, second review-and-patch pass, and parent validation ran `git diff --check`. The second pass and parent validation parsed the editor asmdef JSON and confirmed its runtime asmdef GUID reference matches `Runtime/dsb.gamingcouch.runtime.asmdef.meta`. Static inspection confirmed the existing `GamingCouch` menu item paths and GameObject creation behavior remain unchanged, no active `[CustomEditor]` is registered, and the inactive inspector host adds no `gc.dev.json` UI, file I/O, polling, draft/conflict state, play-mode hooks, or Apply/Revert behavior. Unity 2022.3 compile/import was not run in this environment.
- Scope notes: The editor assembly is editor-only and references the runtime `GamingCouch` assembly by asmdef GUID. The menu script and `.meta` were renamed to preserve the existing script GUID. The inactive inspector helper draws serialized `GamingCouch` fields while excluding `gameModeId`, `playerData`, `numberOfPlayers`, and `randomizePlayerIds`. The WebGL build menu script, runtime serialized field names, package metadata, JSON sync behavior, and main-repo files were left unchanged.
- Remaining Task 4 gates: none.

## Task 5: Package Metadata Hygiene

### Objective

Prepare package metadata and editor-only package structure for the later JSON sync implementation while keeping this prep slice behavior-neutral.

### Files

- `package.json`
- `Editor/dsb.gamingcouch.editor.asmdef`
- `Editor/dsb.gamingcouch.editor.asmdef.meta`
- `docs/architecture/unity-dev-json-sync-prep-refactor-roadmap.md`
- `docs/architecture/unity-dev-json-sync-prep-execution-tasks.md`

### Implementation Steps

1. If Task 4 did not add an editor-only asmdef, add it here using the Task 4 rules.
2. Leave `Runtime/dsb.gamingcouch.runtime.asmdef` runtime-only.
3. Do not reference editor assemblies from runtime.
4. Keep `package.json`:
   - `version` unchanged
   - `dependencies` unchanged
5. Replace placeholder package keywords only if the replacement is unambiguous:
   - `gaming-couch`
   - `unity`
   - `webgl`
   - `local-multiplayer`
6. Do not change `licensesUrl` because `LICENSE.md` is currently empty.
7. Do not update `CHANGELOG.md`.
8. Do not edit public docs (`README.md`, `Documentation~/README.md`, or `CHANGELOG.md`) in this prep task.
9. Do not document future `gc.dev.json` sync behavior in public docs yet.

### Verification

- Run `git diff --check`.
- Parse `package.json` as JSON.
- Confirm `package.json` version is unchanged.
- Confirm `package.json` dependencies are unchanged.
- Confirm `com.unity.nuget.newtonsoft-json` is not present.
- Compile/import in Unity 2022.3 if available.

### Status Update

After implementation:

- Mark Task 5 as `Done` or `Blocked` in both task files.
- Record files changed and verification run.
- Leave next action as: begin the PRD implementation only after explicit approval.

Task 5 implementation result, 2026-05-09: Done.

- Changed paths: `package.json`, `docs/architecture/unity-dev-json-sync-prep-execution-tasks.md`, `docs/architecture/unity-dev-json-sync-prep-refactor-roadmap.md`.
- Verification: second review-and-patch pass and parent validation ran `git diff --check`, parsed `package.json` as JSON, and confirmed `package.json` version remained `0.0.1`, dependencies remained `{}`, the license URL remained a placeholder pending release metadata cleanup, keywords are exactly `gaming-couch`, `unity`, `webgl`, and `local-multiplayer`, and the Newtonsoft package dependency is not present. Static inspection confirmed Task 4 already added an editor-only `Editor/dsb.gamingcouch.editor.asmdef` referencing the runtime asmdef GUID and left `Runtime/dsb.gamingcouch.runtime.asmdef` runtime-only. Unity 2022.3 compile/import was not run in this environment.
- Scope notes: Replaced unambiguous placeholder keywords with `gaming-couch`, `unity`, `webgl`, and `local-multiplayer`. No editor asmdef patch was needed. No runtime editor references, JSON sync behavior, package version/dependency changes, license URL changes, changelog updates, public docs, `LICENSE.md`, `VERSIONING_PLAN.md`, or main-repo files were changed.

## Deferred Decisions

- `com.unity.nuget.newtonsoft-json` dependency is deferred to the PRD implementation.
- Package version bump to `0.1.0-alpha.2` is deferred to the PRD implementation.
- Release notes and tag `unity-0.1.0-alpha.2` are deferred to the PRD implementation.
- License metadata is deferred because `LICENSE.md` is currently empty.
- DevApp target bump is deferred to a separate DevApp follow-up.
