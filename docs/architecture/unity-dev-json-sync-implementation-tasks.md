# Unity `gc.dev.json` Sync Implementation Tasks

Status: In progress
Last updated: 2026-05-09
Owner: Gaming Couch Unity package team

## Source Context

Primary PRD:

- `/Users/anttil/dev/dsb/gamingcouch/client/docs/prd/gaming-couch-unity-dev-json-sync-prd.md`

Prep context:

- `/Users/anttil/dev/dsb/gaming-couch-unity/docs/architecture/unity-dev-json-sync-prep-refactor-roadmap.md`
- `/Users/anttil/dev/dsb/gaming-couch-unity/docs/architecture/unity-dev-json-sync-prep-execution-tasks.md`

Repos and local bridge:

- Unity package repo: `/Users/anttil/dev/dsb/gaming-couch-unity`
- Gaming Couch main repo base path from `AGENTS.local.md`: `/Users/anttil/dev/dsb/gamingcouch/client`
- Main repo relative mappings from `AGENTS.md`:
  - GC SDK: `sdk`
  - GC Client: `client`
  - GC DevApp: `devspace/devapp`
- Always load `/Users/anttil/dev/dsb/gaming-couch-unity/AGENTS.local.md` before using main-repo mappings.
- Never use `/Users/anttil/dev/dsb/gaming-couch-unity/AGENTS.local.example.md` as live truth. It is only a human template.
- Ask the user for permission before editing anything outside `/Users/anttil/dev/dsb/gaming-couch-unity`.

This file tracks implementation work only. Creating this plan does not implement production code.

## Status

Overall status: In progress

Current task: Task 2

Next action: Implement Task 2 with a GPT-5.5 xhigh subagent after the Task 1 commit.

| Task | Status | Owner | Notes |
| --- | --- | --- | --- |
| 1. Editor JSON dependency and `gc.dev.json` store | Done | GPT-5.5 xhigh subagent | Editor-only JSON dependency and preserving `gc.dev.json` store complete; second review-and-patch pass complete; parent validation passed. |
| 2. `gc.metadata.json` light read and validation gates | Pending | Unassigned subagent | Adds metadata parser, warning-only missing/invalid behavior, and valid-metadata gates. |
| 3. File-backed editor play capture | Pending | Unassigned subagent | Replaces serialized editor play source with validated `gc.dev.json` capture while keeping public payloads unchanged. |
| 4. Active custom inspector and Apply/Revert draft | Pending | Unassigned subagent | Activates inspector UI for file-backed local play settings and hides obsolete serialized settings. |
| 5. External reload, dirty draft, conflict, and pending play state | Pending | Unassigned subagent | Adds polling, conflict actions, metadata refresh, and play-mode pending-change status. |
| 6. Play Mode and Gaming Couch restart gates | Pending | Unassigned subagent | Auto-applies valid drafts before capture and blocks invalid/conflicted Play or restart. |
| 7. Documentation, package release metadata, and final validation | Pending | Unassigned subagent | Updates docs, dependency notes, changelog, package version, and manual validation record. |

## Blocker Log

No blockers recorded at plan creation.

Known pre-existing unrelated worktree files at plan creation:

- `VERSIONING_PLAN.md`
- `VERSIONING_PLAN.md.meta`
- `docs.meta`
- `docs/architecture.meta`
- `docs/architecture/unity-dev-json-sync-prep-execution-tasks.md.meta`
- `docs/architecture/unity-dev-json-sync-prep-refactor-roadmap.md.meta`

Do not touch these files unless a later user instruction explicitly changes scope.

## Execution Rules

- Execute tasks in order. Do not run implementation tasks in parallel.
- Use one implementation subagent per task.
- After each task implementation, run two separate review-and-patch subagent passes for that same task before marking it done.
- Tell every subagent: it is not alone in the codebase, must not revert others' edits, must work only inside its assigned scope, and must not ask to enable caveman.
- Each subagent must re-read this plan, the PRD, relevant existing source files, and `git status --short` before editing.
- Each task owns only its listed files. If implementation discovers an additional file is required, update this plan with the reason before editing the extra file.
- Stage and commit per task only if the user or active workflow asks for commits. Never stage unrelated files.
- Do not edit the Gaming Couch main repo without explicit user permission.
- Do not update DevApp package targets in this Unity implementation plan. DevApp target bump remains a separate main-repo follow-up.
- Do not move or rewrite existing release tags. In particular, do not move `unity-0.1.0-alpha.1`.
- Do not create or repair managed local project files from Unity in v1.
- Do not touch `VERSIONING_PLAN.md`, `VERSIONING_PLAN.md.meta`, `docs.meta`, `docs/architecture.meta`, or generated `.meta` files unless the task requires a new Unity asset/script `.meta`.
- Preserve public runtime payload shapes:
  - `GCSetupOptions`
  - `GCPlayOptions`
  - `GCPlayerOptions`
- Keep Newtonsoft/JObject usage in editor-only code paths. Runtime/player builds must not depend on editor JSON sync behavior.
- Prefer structured JSON parsing through `com.unity.nuget.newtonsoft-json` and `JObject`; do not implement JSON sync with ad hoc string manipulation.

## Product Rules To Preserve

- Root `gc.dev.json` is required for Unity editor play settings.
- Unity does not create, bootstrap, or repair `gc.dev.json` or `gc.metadata.json` in v1.
- Old serialized editor play settings are hidden and ignored:
  - `gameModeId`
  - `playerData`
  - `numberOfPlayers`
  - `randomizePlayerIds`
- Do not migrate old serialized values and do not keep them as fallback.
- Unity reads and writes only canonical `gc.dev.json` fields:
  - `devVersion`
  - `entryKey`
  - `seed`
  - `seats`
- Unity preserves unrelated top-level `gc.dev.json` fields by loading the current file as `JObject`, replacing canonical fields, and writing indented JSON plus a trailing newline.
- `gc.dev.json` supports `devVersion: 2`, one `entryKey`, seed `"random"` or fixed string integer `1..999999`, and exactly eight seats.
- At least one seat must be enabled.
- Enabled seats produce dense runtime player ids starting at 1.
- Seat colors are stable by source slot:
  - 1: `blue`
  - 2: `red`
  - 3: `green`
  - 4: `yellow`
  - 5: `purple`
  - 6: `pink`
  - 7: `cyan`
  - 8: `brown`
- Missing or invalid `gc.metadata.json` is warning-only. Unity still displays raw `gc.dev.json` and can apply structurally valid raw edits.
- Valid metadata gates Apply and Play:
  - `platform.id` must be `unity`.
  - selected `entryKey` must exist.
  - enabled-seat count must be within selected entry `minPlayers..maxPlayers`.
- Valid metadata with enabled bot seats on an entry where `botSupport: false` is warning-only.
- External `gc.dev.json` changes auto-reload in edit mode only when the inspector draft is clean.
- External `gc.dev.json` changes while the inspector draft is dirty must not overwrite inspector values and must enter conflict state.
- Conflict state must offer `Reload from disk` and `Write draft`.
- External `gc.metadata.json` changes refresh display and validation context.
- Entering Unity Play Mode or triggering Gaming Couch restart auto-applies a valid, non-conflicted dirty draft before capture.
- Invalid or conflicted drafts block Unity Play Mode or Gaming Couch restart until resolved.
- Editor play captures config once per Play Mode entry and once per Gaming Couch restart.
- Changes during active Play Mode do not mutate active setup/play state. They apply only after restart or next Play Mode entry.
- `seed: "random"` resolves once at capture time to `1..999999`.
- Package release target for this feature is `0.1.0-alpha.2`, with tag `unity-0.1.0-alpha.2`.

## Validation Rules

Required after every task:

- Run `git diff --check`.
- Inspect `git status --short` and confirm unrelated files were not touched.
- If a Unity compile/import is available, run it or record why it was skipped.
- Record validation results in this file under the completed task.

Required before final completion:

- Parse `package.json` as JSON.
- Confirm `package.json` contains `com.unity.nuget.newtonsoft-json` once implementation starts using `JObject`.
- Confirm package version is `0.1.0-alpha.2` in the release/docs task.
- Confirm public docs mention root `gc.dev.json`, editor-only Newtonsoft dependency, and required local-project files.
- Confirm `CHANGELOG.md` records the new sync behavior and release version.
- Run the manual Unity 2022.3 validation pass when available, or record the exact skipped environment gap.

Manual Unity scenarios to cover before release:

1. Inspector reads game name/key, platform, entries, seed, and eight seats from JSON.
2. Inspector edits write to root `gc.dev.json` and do not dirty old serialized editor play settings.
3. Unknown top-level `gc.dev.json` fields survive Unity writes.
4. Clean inspector auto-reloads external `gc.dev.json` edits in edit mode.
5. Dirty inspector plus external `gc.dev.json` edit enters conflict state.
6. `Reload from disk` discards draft and shows current file state.
7. `Write draft` validates and overwrites `gc.dev.json`.
8. External `gc.metadata.json` edits update labels, limits, and color swatches.
9. Missing `gc.dev.json` blocks Apply and Play with a clear error.
10. Invalid `gc.dev.json` blocks Apply and Play with a clear error.
11. Missing or invalid metadata allows structurally valid raw `gc.dev.json` Apply with warning.
12. Valid metadata with non-`unity` platform blocks Apply and Play.
13. Valid metadata with missing selected entry blocks Apply and Play.
14. Valid metadata with enabled-seat count outside entry limits blocks Apply and Play.
15. Enabled bot seats warn, but do not block, when selected entry has `botSupport: false`.
16. Unity Package Manager resolves `com.unity.nuget.newtonsoft-json`.
17. Entering Play Mode auto-applies a valid non-conflicted draft before capture.
18. Entering Play Mode blocks invalid or conflicted drafts.
19. Changing JSON during Play Mode does not mutate active players, seed, or game mode.
20. Restart during Play Mode auto-applies a valid non-conflicted draft, then captures latest JSON.
21. Restart during Play Mode blocks invalid or conflicted drafts.
22. Stop and re-enter Play Mode captures latest valid JSON.
23. DevApp can still read and validate `gc.dev.json` after Unity writes it.

## Task 1: Editor JSON Dependency And `gc.dev.json` Store

### Objective

Add the editor-only JSON dependency, `gc.dev.json` data model, parser, validation primitives, and preserving writer. This task should not activate a custom inspector or change editor play behavior yet.

### Owned Files

- `package.json`
- `Runtime/Dev/GCDevJsonFile.cs`
- `Runtime/Dev/GCDevJsonFile.cs.meta`
- `Runtime/Dev/GCDevJsonStore.cs`
- `Runtime/Dev/GCDevJsonStore.cs.meta`
- `Runtime/Dev/GCDevJsonValidation.cs`
- `Runtime/Dev/GCDevJsonValidation.cs.meta`
- `/Users/anttil/dev/dsb/gaming-couch-unity/docs/architecture/unity-dev-json-sync-implementation-tasks.md`

If the implementer chooses different new file names, update this task before editing.

### Implementation Steps

1. Add `com.unity.nuget.newtonsoft-json` to `package.json` dependencies.
2. Add editor-only `gc.dev.json` model types under `Runtime/Dev`, guarded so non-editor player builds are unaffected.
3. Model the canonical `devVersion`, `entryKey`, `seed`, and exactly eight seats.
4. Represent parsed file state separately from valid data so missing file, invalid JSON, unsupported `devVersion`, invalid seed, invalid seat count, and no enabled seats can be reported precisely.
5. Treat missing `gc.dev.json` as an error, not as bootstrap input.
6. Treat invalid `gc.dev.json` as an error that later tasks can use to block Apply and Play.
7. Implement read behavior from the project root resolved by `GCUnityLocalProjectRootResolver`.
8. Implement write behavior by loading the current file as `JObject`, replacing only canonical top-level fields, writing indented JSON, and ending with a trailing newline.
9. Preserve unknown top-level fields on write.
10. Do not write `gc.metadata.json`.
11. Do not alter `GamingCouch` editor play flow in this task.

### Verification

- Run `git diff --check`.
- Parse `package.json` as JSON.
- Confirm `package.json` includes `com.unity.nuget.newtonsoft-json`.
- Inspect the new store to confirm missing `gc.dev.json` is an error.
- Inspect the writer to confirm it preserves unknown top-level fields by using `JObject`.
- Compile/import in Unity 2022.3 if available.

### Status Update Rules

After implementation and both review-and-patch passes:

- Mark Task 1 `Done` or `Blocked`.
- Add changed paths.
- Add validation results and skipped validation gaps.
- Set current task to Task 2 if complete.

### Task 1 Review Record

Status: Done

Changed paths:

- `package.json`
- `Runtime/Dev/GCDevJsonFile.cs`
- `Runtime/Dev/GCDevJsonFile.cs.meta`
- `Runtime/Dev/GCDevJsonStore.cs`
- `Runtime/Dev/GCDevJsonStore.cs.meta`
- `Runtime/Dev/GCDevJsonValidation.cs`
- `Runtime/Dev/GCDevJsonValidation.cs.meta`
- `docs/architecture/unity-dev-json-sync-implementation-tasks.md`

Validation:

- `git diff --check`: passed.
- `git diff --check --no-index /dev/null <new-file>`: no whitespace output for new Task 1 files; command exits non-zero because `/dev/null` differs from each new file.
- `node -e "JSON.parse(require('fs').readFileSync('package.json','utf8'))"`: passed.
- `package.json` dependency check: `com.unity.nuget.newtonsoft-json` present at `3.2.1`.
- Store inspection: missing `gc.dev.json` returns `GCDevJsonIssueCode.MissingFile` for read and write; no bootstrap path is implemented.
- Writer inspection: write path loads current `gc.dev.json` with `JToken.Parse`, requires a `JObject`, replaces only `devVersion`, `entryKey`, `seed`, and `seats`, writes `Formatting.Indented` plus a trailing newline, and does not reference or write `gc.metadata.json`.
- `git status --short`: only Task 1 owned files were touched beyond the pre-existing unrelated untracked files listed in the blocker log.

Skipped validation:

- Unity 2022.3 compile/import skipped because `which Unity` returned `Unity not found`; no Unity editor/CI command is available in this shell.

Decisions:

- Missing `gc.dev.json` is treated as an error for both read and write.
- Parsed file state is represented separately from validated `GCDevJsonFile` data so later tasks can distinguish missing file, invalid JSON/root, unsupported `devVersion`, invalid seed, invalid seat count, invalid seat fields, and no enabled seats.
- Public runtime DTOs and existing editor play flow were left unchanged.

Review pass 1 patches:

- Moved Task 1 back to `In review`; Task 2 must wait for review pass 2 and parent validation.
- Made `GCDevJsonFile` construction, cloning, and enabled-seat counting tolerate invalid seat arrays so `ValidateData` can return structured issues instead of constructor exceptions.
- Hardened validation result/read-result handling for null issues, missing parser state, and null parsed root objects.
- Tightened `entryKey` validation to reject empty or whitespace-only strings.

Review pass 1 validation:

- `git diff --check`: passed.
- `node -e "const fs=require('fs'); const pkg=JSON.parse(fs.readFileSync('package.json','utf8')); const v=pkg.dependencies && pkg.dependencies['com.unity.nuget.newtonsoft-json']; if (v !== '3.2.1') throw new Error('missing com.unity.nuget.newtonsoft-json 3.2.1');"`: passed.
- `rg -n "[ \t]+$" <Task 1 scoped files>`: no trailing whitespace matches.
- `which Unity`: Unity not found; Unity 2022.3 compile/import remains skipped in this shell.

Review pass 2 findings:

- No blocking Task 1 implementation defects found.
- `GCDevJsonFile` now tolerates invalid seat arrays only as validation input; validated read results still expose `data` only when `ValidateData` succeeds, and the writer validates before serializing seats.
- `ValidateData` catches null, wrong-length, null-entry, invalid-name, and no-enabled-seat rosters without throwing.
- The writer refuses missing `gc.dev.json` and does not bootstrap the file.

Review pass 2 patches:

- Updated this task record to show second review complete while leaving parent validation/commit pending.
- No code changes were made in pass 2.

Review pass 2 validation:

- `git diff --check`: passed.
- `node -e "const fs=require('fs'); const pkg=JSON.parse(fs.readFileSync('package.json','utf8')); const deps=pkg.dependencies||{}; if (deps['com.unity.nuget.newtonsoft-json'] !== '3.2.1') throw new Error('missing dependency');"`: passed.
- Scoped inspection confirmed Newtonsoft/JObject references are inside `#if UNITY_EDITOR` files, missing `gc.dev.json` remains an error for read/write, writes load the current file as `JObject`, and no `gc.metadata.json` write path exists.
- `which Unity`: Unity not found; Unity 2022.3 compile/import remains skipped in this shell.

Parent validation:

- `git diff --check`: passed.
- `node -e "const fs=require('fs'); const p=JSON.parse(fs.readFileSync('package.json','utf8')); if(p.dependencies['com.unity.nuget.newtonsoft-json']!=='3.2.1') throw new Error('missing newtonsoft');"`: passed.
- Parent inspection confirmed no `gc.metadata.json` writes, no `GamingCouch` editor play flow changes, and no public DTO changes in Task 1.
- Unity 2022.3 compile/import skipped because Unity is unavailable in this shell.

## Task 2: `gc.metadata.json` Light Read And Validation Gates

### Objective

Add `gc.metadata.json` parsing and validation context for labels, colors, platform, entry limits, and bot support. Missing or invalid metadata must be warning-only, while valid metadata must gate Apply and Play.

### Owned Files

- `Runtime/Dev/GCMetadataJsonFile.cs`
- `Runtime/Dev/GCMetadataJsonFile.cs.meta`
- `Runtime/Dev/GCMetadataJsonStore.cs`
- `Runtime/Dev/GCMetadataJsonStore.cs.meta`
- `Runtime/Dev/GCDevJsonValidation.cs`
- `Runtime/Dev/GCDevJsonValidation.cs.meta`
- `Runtime/Dev/GCDevJsonStore.cs`
- `/Users/anttil/dev/dsb/gaming-couch-unity/docs/architecture/unity-dev-json-sync-implementation-tasks.md`

### Implementation Steps

1. Add editor-only metadata model and store under `Runtime/Dev`.
2. Read only the fields required by the PRD:
   - `game.key`
   - `game.name`
   - `platform.id`
   - `game.entries[entryKey].name`
   - `game.entries[entryKey].minPlayers`
   - `game.entries[entryKey].maxPlayers`
   - `game.entries[entryKey].botSupport`
   - `properties.colors.players`
3. Treat missing or invalid metadata as warning-only.
4. Keep raw `gc.dev.json` editing possible when metadata is missing or invalid.
5. When metadata is valid, block Apply and Play if `platform.id` is not `unity`.
6. When metadata is valid, block Apply and Play if selected `entryKey` is missing.
7. When metadata is valid, block Apply and Play if enabled seats are outside selected entry limits.
8. When metadata is valid, warn but do not block if enabled bot seats exist for an entry with `botSupport: false`.
9. Expose validation output as structured errors and warnings for both inspector and play capture tasks.
10. Do not add visible inspector UI in this task.

### Verification

- Run `git diff --check`.
- Inspect metadata failure handling and confirm it cannot block raw structurally valid `gc.dev.json` Apply by itself.
- Inspect validation gates and confirm they activate only when metadata parse is valid.
- Compile/import in Unity 2022.3 if available.

### Status Update Rules

After implementation and both review-and-patch passes:

- Mark Task 2 `Done` or `Blocked`.
- Add changed paths.
- Add validation results and skipped validation gaps.
- Set current task to Task 3 if complete.

## Task 3: File-Backed Editor Play Capture

### Objective

Replace serialized editor play settings as the source for setup/play capture with validated `gc.dev.json`, while keeping public runtime payloads unchanged.

### Owned Files

- `Runtime/GamingCouch.cs`
- `Runtime/Dev/GCEditorPlayCapture.cs`
- `Runtime/Dev/GCDevJsonFile.cs`
- `Runtime/Dev/GCDevJsonStore.cs`
- `Runtime/Dev/GCDevJsonValidation.cs`
- `Runtime/Dev/GCSeatIdentity.cs`
- `/Users/anttil/dev/dsb/gaming-couch-unity/docs/architecture/unity-dev-json-sync-implementation-tasks.md`

### Implementation Steps

1. Add a file-backed editor play config adapter that reads validated `gc.dev.json` from the project root.
2. Use valid metadata, when available, as the same gates defined in Task 2.
3. Build setup options from file data:
   - `mode = GCMode.Development`
   - `isServer = true`
   - `gameModeId = dev.entryKey`
4. Build play options from enabled seats only.
5. Keep runtime player ids dense, starting at 1.
6. Preserve source seat indexes for DevApp runtime snapshots through `GCSeatIdentity`.
7. Map seat slot colors by fixed source seat order, not compressed active-player order.
8. Resolve `seed: "random"` once at capture time to `1..999999`.
9. Parse fixed seed strings and enforce `1..999999`.
10. Hide or ignore old serialized editor play settings as functional input. Do not migrate them and do not fallback to them.
11. Ensure invalid or missing `gc.dev.json` produces clear errors and prevents invalid setup/play callbacks.
12. Keep `GCSetupOptions`, `GCPlayOptions`, and `GCPlayerOptions` unchanged.
13. Do not activate the custom inspector in this task.

### Verification

- Run `git diff --check`.
- Inspect `GamingCouch` and confirm editor play no longer reads `gameModeId`, `playerData`, `numberOfPlayers`, or `randomizePlayerIds` as functional input.
- Confirm public DTO shapes are unchanged.
- Confirm dense runtime players can come from sparse enabled seats.
- Confirm invalid capture cannot call listener setup/play with invalid options.
- Compile/import in Unity 2022.3 if available.

### Status Update Rules

After implementation and both review-and-patch passes:

- Mark Task 3 `Done` or `Blocked`.
- Add changed paths.
- Add validation results and skipped validation gaps.
- Set current task to Task 4 if complete.

## Task 4: Active Custom Inspector And Apply/Revert Draft

### Objective

Activate a custom `GamingCouch` inspector that draws normal component fields and adds a file-backed local play settings editor with explicit Apply/Revert behavior.

### Owned Files

- `Editor/GamingCouchEditor.cs`
- `Editor/GamingCouchEditor.cs.meta`
- `Editor/GamingCouchInspectorHost.cs`
- `Editor/GCDevJsonInspectorState.cs`
- `Editor/GCDevJsonInspectorState.cs.meta`
- `Editor/GCDevJsonInspectorView.cs`
- `Editor/GCDevJsonInspectorView.cs.meta`
- `Runtime/Dev/GCDevJsonFile.cs`
- `Runtime/Dev/GCMetadataJsonFile.cs`
- `/Users/anttil/dev/dsb/gaming-couch-unity/docs/architecture/unity-dev-json-sync-implementation-tasks.md`

If implementation uses fewer or differently named editor helper files, update this task with the final ownership.

### Implementation Steps

1. Register an active `[CustomEditor(typeof(GamingCouch))]`.
2. Draw existing normal component fields through `GamingCouchInspectorHost`.
3. Continue excluding obsolete editor play fields from visible inspector UI:
   - `gameModeId`
   - `playerData`
   - `numberOfPlayers`
   - `randomizePlayerIds`
4. Load project-root `gc.dev.json` into an in-memory draft.
5. Load project-root `gc.metadata.json` for labels, limits, bot-support warnings, and color swatches.
6. Show raw entry-key editing fallback when metadata is missing or invalid.
7. Show metadata-backed entry dropdown when metadata is valid.
8. Show seed mode and fixed seed controls.
9. Show eight stable seat rows with seat number, color swatch when available, name, enabled toggle, and bot toggle.
10. Track clean versus dirty draft state.
11. Implement `Apply` to validate and write `gc.dev.json`.
12. Implement `Revert` to discard the draft and reload from disk.
13. Ensure local play setting edits do not mark old serialized editor play settings as scene data.
14. Show structured errors and warnings from Tasks 1 and 2.
15. Do not implement external polling or conflict state in this task unless needed to support the draft model.

### Verification

- Run `git diff --check`.
- Confirm the active inspector hides obsolete editor play fields.
- Confirm normal component fields remain visible.
- Confirm Apply writes only `gc.dev.json`.
- Confirm Revert reloads from disk.
- Confirm missing/invalid metadata warning still allows structurally valid raw Apply.
- Confirm valid metadata gates Apply.
- Compile/import in Unity 2022.3 if available.

### Status Update Rules

After implementation and both review-and-patch passes:

- Mark Task 4 `Done` or `Blocked`.
- Add changed paths.
- Add validation results and skipped validation gaps.
- Set current task to Task 5 if complete.

## Task 5: External Reload, Dirty Draft, Conflict, And Pending Play State

### Objective

Add file polling and state transitions for external JSON edits, dirty drafts, conflicts, metadata refreshes, and pending play changes.

### Owned Files

- `Editor/GamingCouchEditor.cs`
- `Editor/GamingCouchInspectorHost.cs`
- `Editor/GCDevJsonInspectorState.cs`
- `Editor/GCDevJsonInspectorView.cs`
- `Runtime/Dev/GCDevJsonStore.cs`
- `Runtime/Dev/GCMetadataJsonStore.cs`
- `/Users/anttil/dev/dsb/gaming-couch-unity/docs/architecture/unity-dev-json-sync-implementation-tasks.md`

### Implementation Steps

1. Poll root `gc.dev.json` and `gc.metadata.json` through `EditorApplication.update` while the inspector state is active.
2. Use `LastWriteTimeUtc`, content hash, or another deterministic lightweight root-file change check.
3. Do not use `FileSystemWatcher` in v1.
4. In edit mode with a clean draft, auto-reload external `gc.dev.json` changes into the inspector.
5. In edit mode with a dirty draft, preserve inspector values when external `gc.dev.json` changes.
6. Dirty draft plus external `gc.dev.json` change must enter conflict state.
7. Add conflict action `Reload from disk` to discard draft and load current file.
8. Add conflict action `Write draft` to validate draft and overwrite current `gc.dev.json`.
9. Refresh display and validation context when external `gc.metadata.json` changes.
10. If metadata refresh makes a dirty draft invalid, keep the draft dirty and show validation errors.
11. During active Play Mode, external JSON changes must not mutate captured setup/play state.
12. During active Play Mode, show pending play change state so the user knows changes apply on restart or next Play Mode entry.
13. Keep polling lifecycle clean when inspectors are disabled or targets are destroyed.

### Verification

- Run `git diff --check`.
- Confirm clean external `gc.dev.json` edits auto-reload in edit mode.
- Confirm dirty external `gc.dev.json` edits enter conflict without overwriting draft.
- Confirm `Reload from disk` and `Write draft` behavior.
- Confirm metadata changes refresh labels and validation.
- Confirm active Play Mode changes are pending only.
- Compile/import in Unity 2022.3 if available.

### Status Update Rules

After implementation and both review-and-patch passes:

- Mark Task 5 `Done` or `Blocked`.
- Add changed paths.
- Add validation results and skipped validation gaps.
- Set current task to Task 6 if complete.

## Task 6: Play Mode And Gaming Couch Restart Gates

### Objective

Enforce draft auto-apply, validation blocking, capture timing, and restart behavior at Unity Play Mode entry and Gaming Couch restart boundaries.

### Owned Files

- `Runtime/GamingCouch.cs`
- `Runtime/Dev/GCEditorPlayCapture.cs`
- `Runtime/Dev/GCDevJsonStore.cs`
- `Runtime/Dev/GCDevJsonValidation.cs`
- `Editor/GamingCouchEditor.cs`
- `Editor/GCDevJsonInspectorState.cs`
- `Editor/GCDevJsonInspectorView.cs`
- `/Users/anttil/dev/dsb/gaming-couch-unity/docs/architecture/unity-dev-json-sync-implementation-tasks.md`

### Implementation Steps

1. Add a shared editor-only coordinator for Play Mode and restart preflight if needed.
2. Before entering Unity Play Mode, auto-apply a valid, non-conflicted dirty draft.
3. Before entering Unity Play Mode, block if the current draft is invalid or conflicted.
4. Before editor setup/play capture, read and validate `gc.dev.json`.
5. Capture one config for setup and play during the same editor run.
6. Do not change captured config during active Play Mode.
7. Before Gaming Couch play-mode restart, auto-apply a valid, non-conflicted dirty draft.
8. Before Gaming Couch play-mode restart, block if the current draft is invalid or conflicted.
9. On successful Gaming Couch restart, capture latest valid JSON before calling setup/play again.
10. Log clear errors for blocked Play Mode entry, blocked restart, missing `gc.dev.json`, invalid `gc.dev.json`, and valid-metadata gate failures.
11. Preserve non-editor and WebGL production behavior.

### Verification

- Run `git diff --check`.
- Confirm Play Mode entry auto-applies valid non-conflicted drafts.
- Confirm Play Mode entry blocks invalid or conflicted drafts.
- Confirm setup/play use the same captured config during one editor run.
- Confirm JSON edits during Play Mode do not mutate active players, seed, or game mode.
- Confirm Gaming Couch restart auto-applies valid non-conflicted drafts and captures latest JSON.
- Confirm Gaming Couch restart blocks invalid or conflicted drafts.
- Confirm production/WebGL paths remain unaffected by editor-only code.
- Compile/import in Unity 2022.3 if available.

### Status Update Rules

After implementation and both review-and-patch passes:

- Mark Task 6 `Done` or `Blocked`.
- Add changed paths.
- Add validation results and skipped validation gaps.
- Set current task to Task 7 if complete.

## Task 7: Documentation, Package Release Metadata, And Final Validation

### Objective

Document the feature, record release metadata, update package versioning, and complete final validation for the sync-capable Unity package.

### Owned Files

- `README.md`
- `Documentation~/README.md`
- `CHANGELOG.md`
- `package.json`
- `/Users/anttil/dev/dsb/gaming-couch-unity/docs/architecture/unity-dev-json-sync-implementation-tasks.md`

Do not edit `VERSIONING_PLAN.md` or `VERSIONING_PLAN.md.meta` unless the user explicitly expands scope.

### Implementation Steps

1. Update `README.md` with the root `gc.dev.json` local play settings behavior.
2. Update `Documentation~/README.md` with the same package-facing guidance.
3. Document that Unity requires existing root `gc.dev.json` for editor play and does not bootstrap it.
4. Document that missing or invalid `gc.metadata.json` is warning-only, while valid metadata gates Apply and Play.
5. Document that the package uses `com.unity.nuget.newtonsoft-json` for editor-only JSON sync.
6. Update `CHANGELOG.md` for `0.1.0-alpha.2`.
7. Update `package.json` version to `0.1.0-alpha.2`.
8. Confirm `package.json` dependency on `com.unity.nuget.newtonsoft-json`.
9. Do not create or move git tags unless explicitly requested after implementation is complete.
10. Add any remaining DevApp follow-up note as a non-blocking note only. Do not edit the Gaming Couch main repo.

### Verification

- Run `git diff --check`.
- Parse `package.json` as JSON.
- Confirm package version is `0.1.0-alpha.2`.
- Confirm dependency includes `com.unity.nuget.newtonsoft-json`.
- Confirm README, package documentation, and changelog mention the sync behavior.
- Run or record the final manual Unity validation scenarios listed in this plan.
- Confirm no main-repo files changed.
- Confirm no protected files were touched.

### Status Update Rules

After implementation and both review-and-patch passes:

- Mark Task 7 `Done` or `Blocked`.
- Add changed paths.
- Add validation results and skipped validation gaps.
- Set current task to None if complete.
- Set next action to release/tag follow-up only if user explicitly asks for release publishing.

## Handoff Protocol

At the start of any future chat:

1. Read `/Users/anttil/dev/dsb/gaming-couch-unity/AGENTS.local.md`.
2. Read `/Users/anttil/dev/dsb/gaming-couch-unity/AGENTS.md`.
3. Read this plan.
4. Read the PRD at `/Users/anttil/dev/dsb/gamingcouch/client/docs/prd/gaming-couch-unity-dev-json-sync-prd.md`.
5. Run `git status --short`.
6. Identify the first `Pending` task in the status table.
7. Mark only that task `In progress`.
8. Assign exactly one implementation subagent for that task.
9. Run two review-and-patch subagent passes before marking the task `Done`.
10. Update this file with changed paths, validation, blockers, and the next task.

When blocked:

- Stop before broadening scope.
- Record the blocker in the Blocker Log.
- Ask the user only if the blocker requires a product decision, outside-repo edit permission, destructive git action, or release/tag action.

When finishing the whole plan:

- Ensure every task has a final status.
- Ensure every task records changed paths and validation.
- Ensure remaining follow-ups are explicit and outside this Unity repo implementation scope.
