# Unity `gc.dev.json` Sync Implementation Tasks

Status: Done
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

Overall status: Done

Current task: None

Next action: Release/tag follow-up only if explicitly requested.

| Task | Status | Owner | Notes |
| --- | --- | --- | --- |
| 1. Editor JSON dependency and `gc.dev.json` store | Done | GPT-5.5 xhigh subagent | Editor-only JSON dependency and preserving `gc.dev.json` store complete; second review-and-patch pass complete; parent validation passed. |
| 2. `gc.metadata.json` light read and validation gates | Done | GPT-5.5 xhigh subagent | Metadata light-read and validation gates complete; both review passes complete; parent validation passed. |
| 3. File-backed editor play capture | Done | GPT-5.5 xhigh subagent | File-backed editor capture complete; callback bypass patched in review pass 2; parent validation passed. |
| 4. Active custom inspector and Apply/Revert draft | Done | GPT-5.5 xhigh subagent | Active inspector, in-memory draft UI, Apply/Revert, metadata-backed labels/colors, raw fallback, and validation display complete; parent validation passed. |
| 5. External reload, dirty draft, conflict, and pending play state | Done | GPT-5.5 xhigh subagent | Polling, conflict actions, metadata refresh, and play-mode pending-change status complete; parent validation passed. |
| 6. Play Mode and Gaming Couch restart gates | Done | GPT-5.5 xhigh subagent | Play Mode entry and Gaming Couch restart gates auto-apply valid drafts, block invalid/conflicted state, and recapture on restart boundaries; parent validation passed. |
| 7. Documentation, package release metadata, and final validation | Done | GPT-5.5 xhigh subagent | Docs, dependency notes, changelog, package version, and final validation record complete; both review passes complete; parent validation passed. |

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
  - enabled-seat count must be at least one and no more than selected entry `maxPlayers`.
  - production `minPlayers` remains parsed and displayed, but does not raise the local dev seat minimum.
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
14. Valid metadata with zero enabled seats or more than `maxPlayers` enabled seats blocks Apply and Play.
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

Add `gc.metadata.json` parsing and validation context for labels, colors, platform, entry `maxPlayers`, and bot support. Missing or invalid metadata must be warning-only, while valid metadata must gate Apply and Play.

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
7. When metadata is valid, block Apply and Play if enabled seats exceed selected entry `maxPlayers`; keep zero-seat blocking in structural `gc.dev.json` validation.
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

### Task 2 Review Record

Status: Done

Changed paths:

- `Runtime/Dev/GCMetadataJsonFile.cs`
- `Runtime/Dev/GCMetadataJsonFile.cs.meta`
- `Runtime/Dev/GCMetadataJsonStore.cs`
- `Runtime/Dev/GCMetadataJsonStore.cs.meta`
- `Runtime/Dev/GCDevJsonValidation.cs`
- `Runtime/Dev/GCDevJsonStore.cs`
- `docs/architecture/unity-dev-json-sync-implementation-tasks.md`

Validation:

- `git diff --check`: passed.
- `git diff --check --no-index /dev/null <new Task 2 file>`: no whitespace output for new metadata files; command exits non-zero because `/dev/null` differs from each new file.
- Metadata read inspection: `GCMetadataJsonStore` only reads project-root `gc.metadata.json`; no metadata write path was added.
- Missing/invalid metadata inspection: metadata missing, invalid JSON, invalid root, read error, or invalid required fields produce `GCDevJsonIssueSeverity.Warning` issues and `GCMetadataJsonReadResult.data == null`; `GCDevJsonValidation` returns before metadata gates unless `metadataReadResult.IsValid`.
- Apply/Play gate inspection: valid metadata adds blocking errors for `platform.id != "unity"`, missing selected entry, and enabled seats above `maxPlayers`; zero enabled seats remain blocked by structural `gc.dev.json` validation, while enabled seats below production `minPlayers` are allowed for local dev. Enabled bot seats with `botSupport == false` add a warning only.
- Structurally valid dev-file inspection: `GCDevJsonReadResult.data` remains available after metadata gate errors while `GCDevJsonReadResult.IsValid` is false, so later inspector work can still show raw `gc.dev.json` values.
- Store inspection: normal `GCDevJsonStore.Read()` and `Write()` read metadata and use combined validation; overloads accept a pre-read metadata result for later inspector/play capture reuse.
- `git status --short`: only Task 2 owned files were touched beyond the pre-existing unrelated untracked files listed in the blocker log.

Skipped validation:

- Unity 2022.3 compile/import skipped because `which Unity` returned `Unity not found`; no Unity editor/CI command is available in this shell.

Decisions:

- Metadata parsing reads only the PRD-required fields: `game.key`, `game.name`, `platform.id`, `game.entries[entryKey].name`, `minPlayers`, `maxPlayers`, `botSupport`, and `properties.colors.players`.
- `platform.id` is considered structurally valid when it is a non-empty string; any valid metadata value other than `unity` then becomes a blocking metadata gate error.
- Required metadata fields, including player color variants, must parse for metadata-derived labels and gates to activate. Malformed required metadata remains warning-only.
- No visible inspector UI, GamingCouch editor play flow changes, public runtime DTO changes, or `gc.metadata.json` writes were added.

Review pass 1 findings:

- Patched one metadata-validity hardening issue: a manually constructed `GCMetadataJsonReadResult` with both `data` and warning issues could previously report `IsValid == true`, which could activate valid-metadata gates accidentally.
- No blocking defects found after the patch. Structurally valid `gc.dev.json` data remains available when valid metadata gate errors exist, and enabled bot seats with `botSupport: false` remain warning-only.

Review pass 1 patches:

- Moved Task 2 back to `In review`; Task 3 now waits for review pass 2 and parent validation.
- Hardened `GCMetadataJsonReadResult.IsValid` so metadata gates activate only when parsed metadata data exists and metadata validation has no errors or warnings.

Review pass 1 validation:

- `git diff --check`: passed.
- `rg -n "[ \t]+$" <Task 2 scoped files>`: no trailing whitespace matches.
- Metadata write inspection: no write API or write method references in `GCMetadataJsonFile.cs`, `GCMetadataJsonStore.cs`, or metadata validation code.
- Inspector/play-flow inspection: no `CustomEditor`, `OnInspectorGUI`, or `GamingCouch` editor play references in scoped Task 2 runtime-dev files.
- `which Unity`: Unity not found; Unity 2022.3 compile/import remains skipped in this shell.
- `git status --short`: only Task 2 owned files were touched beyond the pre-existing unrelated untracked files listed in the blocker log.

Review pass 2 findings:

- No blocking Task 2 implementation defects found.
- `GCMetadataJsonReadResult.IsValid` requiring zero warnings still surfaces missing or invalid metadata warnings through combined dev validation while preventing metadata gates from activating.
- `GCDevJsonReadResult.data` remains available when structurally valid `gc.dev.json` has valid-metadata gate errors.
- `GCDevJsonStore.Write` blocks valid-metadata gate errors and allows warning-only missing or invalid metadata results.

Review pass 2 patches:

- Updated this task record to show second review complete while leaving parent validation and commit pending.
- No code changes were made in pass 2.

Review pass 2 validation:

- `git diff --check`: passed.
- `rg -n "[ \t]+$" <Task 2 scoped files>`: no trailing whitespace matches.
- Metadata gate inspection: `GCDevJsonValidation` copies metadata warnings into the validation result, returns before gates unless `metadataReadResult.IsValid`, and uses error severity only for valid metadata platform, entry, and seat-count gates.
- Store inspection: `GCDevJsonStore.Write` fails only when combined validation has errors; warning-only metadata validation can return a successful write result with warnings.
- Scoped inspection confirmed no visible inspector UI, GamingCouch play-flow changes, public runtime DTO changes, or `gc.metadata.json` writes were added.
- Unity 2022.3 compile/import remains skipped because no Unity editor/CI command is available in this shell.
- `git status --short`: only Task 2 owned files were touched beyond the pre-existing unrelated untracked files listed in the blocker log.

Parent validation:

- `git diff --check`: passed.
- `node -e "JSON.parse(require('fs').readFileSync('package.json','utf8'))"`: passed.
- Scoped write inspection: Task 2 adds no `gc.metadata.json` write path; only the existing Task 1 `GCDevJsonStore.Write` and `File.WriteAllText` references remain for `gc.dev.json`.
- Scoped UI/play inspection: Task 2 files add no `CustomEditor`, `OnInspectorGUI`, or `GamingCouch` editor play-flow references.
- Public runtime DTO inspection: `GCSetupOptions`, `GCPlayOptions`, and `GCPlayerOptions` remain untouched.
- `git status --short`: only Task 2 owned files are modified or untracked beyond the pre-existing unrelated files listed in the blocker log.
- Unity 2022.3 compile/import skipped because `which Unity` returned no Unity executable in this shell.

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

### Task 3 Review Record

Status: Done

Changed paths:

- `Runtime/GamingCouch.cs`
- `Runtime/Dev/GCEditorPlayCapture.cs`
- `docs/architecture/unity-dev-json-sync-implementation-tasks.md`

Validation:

- `git diff --check`: passed.
- Serialized editor play source inspection: `GamingCouch` no longer creates editor setup/play snapshots from `gameModeId`, `playerData`, `numberOfPlayers`, or `randomizePlayerIds`; those fields remain only as hidden deserialization remnants and are not read by setup/play capture.
- File-backed setup inspection: `GCEditorPlayCapture.Capture()` reads through `GCDevJsonStore.Read()`, so Task 2 metadata gates are applied when metadata is valid; setup options are built with `mode = GCMode.Development`, `isServer = true`, and `gameModeId = data.entryKey`.
- File-backed play inspection: enabled seats are the only players; `activePlayerIndex + 1` creates dense runtime player IDs from sparse seats while `GCSeatIdentity.sourceSeatIndex` preserves the original one-based seat slot.
- Color mapping inspection: source seat slots map through the fixed order `blue`, `red`, `green`, `yellow`, `purple`, `pink`, `cyan`, `brown`; disabled seats do not compress the color source slot.
- Seed inspection: `seed: "random"` resolves once during `GCEditorPlayCapture.Capture()` with `UnityEngine.Random.Range(1, 1000000)`; fixed seed strings are parsed after existing `gc.dev.json` validation.
- Invalid capture inspection: missing or invalid `gc.dev.json`, or valid-metadata gate errors, produce `success = false`; `GamingCouch` logs clear errors and returns before `listener.SendMessage("GamingCouchSetup", ...)` or `Play(...)` can call the listener with invalid options.
- Warning-only metadata inspection: missing or invalid metadata issues are logged as warnings and do not block capture because `GCDevJsonReadResult.IsValid` only requires zero errors.
- Public DTO shape inspection: `Runtime/GCSetupOptions.cs` and `Runtime/GCPlayOptions.cs` were not changed; `GCSetupOptions`, `GCPlayOptions`, and `GCPlayerOptions` public fields remain unchanged.
- Custom inspector inspection: no `Editor/` files, `CustomEditor`, or `OnInspectorGUI` changes were added.
- `git status --short`: only Task 3 owned files are modified beyond the pre-existing unrelated untracked files listed in the blocker log.

Skipped validation:

- Unity 2022.3 compile/import skipped because `which Unity` returned `Unity not found`; no Unity editor/CI command is available in this shell.

Decisions:

- The editor capture is cached at `Awake` for the current Play Mode entry so setup and play use the same captured data and random seed. Restart-time recapture and draft auto-apply remain Task 6 scope.
- Obsolete serialized editor play fields are hidden with `HideInInspector` and retained only to let old scenes deserialize without migration or fallback.

Review pass 1 findings:

- No blocking Task 3 code defects found.
- `GCEditorPlayCapture.Capture()` reads through validated `GCDevJsonStore.Read()` and fails capture for missing or invalid `gc.dev.json` and for valid-metadata gate errors.
- Missing or invalid metadata stays warning-only because capture success follows `GCDevJsonReadResult.IsValid`, which allows warnings and blocks only errors.
- Scoped old-field inspection found `gameModeId`, `playerData`, `numberOfPlayers`, and `randomizePlayerIds` only as hidden deserialization fields; setup/play capture no longer reads them as input.
- Sparse enabled seats map to dense runtime player IDs while `GCSeatIdentity.sourceSeatIndex` preserves one-based source seat slots; player colors use fixed source seat order.
- `seed: "random"` resolves once in the cached editor capture with the inclusive `1..999999` range; fixed seed strings pass through after existing validation.

Review pass 1 patches:

- Updated this task record and top-level status to keep Task 3 in review until pass 2 and parent validation complete.
- No code changes were made in pass 1.

Review pass 1 validation:

- `git diff --check`: passed.
- Scoped grep for old serialized fields in `Runtime/GamingCouch.cs` and `Runtime/Dev/GCEditorPlayCapture.cs`: only hidden field declarations remain, plus `gameModeId = data.entryKey` when building the public setup DTO.
- Public DTO shape inspection: `Runtime/GCSetupOptions.cs` and `Runtime/GCPlayOptions.cs` remain unchanged.
- Custom inspector inspection: no active `CustomEditor`, `OnInspectorGUI`, or `GamingCouchEditor` source changes are present.
- `git status --short`: only Task 3 owned files are modified beyond the pre-existing unrelated untracked files listed in the blocker log.

Review pass 1 skipped validation:

- Unity 2022.3 compile/import skipped because `which Unity` returned `Unity not found` and `which UnityHub` returned `UnityHub not found`; no Unity editor/CI command is available in this shell.

Review pass 2 findings:

- Found and patched an editor-only callback bypass: direct `GamingCouchSetupOptions`, `GamingCouchSetup`, or `GamingCouchPlay` platform-style callbacks could otherwise use externally supplied options in the Unity editor instead of the cached file-backed capture.
- Missing or invalid `gc.dev.json`, and valid-metadata gate failures, now block direct editor setup/play callback paths as well as the normal editor setup and `_EditorPlay` coroutine paths.
- Setup and play remain tied to the same cached capture for one Play Mode entry; direct editor setup/play callbacks now reuse `editorPlayCapture.setupOptions`, `editorPlayCapture.playOptions`, and `editorPlayCapture.seatIdentities`.
- `SetupDone` still returns before fade, `_EditorPlay`, and `status = GCStatus.SetupDone` when editor capture is invalid, so a setup-blocked session cannot advance through play accidentally.
- Online multiplayer editor server readiness still uses the file-backed setup path; invalid capture returns before setup, while the existing client-ready assertion behavior remains unchanged because editor file-backed setup is server-authoritative for this task.
- Obsolete serialized editor play fields remain hidden declarations only and are not read as functional setup/play input.
- Public DTO shapes and custom inspector scope remain untouched.

Review pass 2 patches:

- Updated `Runtime/GamingCouch.cs` so editor `GamingCouchSetupOptions` ignores external setup JSON and keeps `setupOptions` sourced from the valid cached file-backed capture only.
- Updated editor `GamingCouchSetup` to re-check the cached capture immediately before sending `GamingCouchSetup` to the listener and to refresh `setupOptions` from that capture.
- Updated editor `GamingCouchPlay` to ignore external play JSON and call `Play` only with the cached file-backed play options and seat identities.

Review pass 2 validation:

- `git diff --check`: passed.
- Scoped grep for old serialized fields in `Runtime/GamingCouch.cs` and `Runtime/Dev/GCEditorPlayCapture.cs`: only hidden field declarations remain, plus `gameModeId = data.entryKey` when building the public setup DTO.
- Scoped editor-only reference inspection: `GCEditorPlayCapture`, `GCEditorPlayCaptureResult`, `GCDevJsonValidationResult`, and `GCDevJsonIssue` references in `Runtime/GamingCouch.cs` are under `#if UNITY_EDITOR`; JSON/Newtonsoft metadata and dev-file types remain in `Runtime/Dev` files that start with `#if UNITY_EDITOR`.
- Public DTO shape inspection: `Runtime/GCSetupOptions.cs` and `Runtime/GCPlayOptions.cs` have no diff.
- Custom inspector inspection: no `Editor/` files, `CustomEditor`, `OnInspectorGUI`, or `GamingCouchEditor` changes are present.
- `git status --short`: only Task 3 owned files are modified beyond the pre-existing unrelated untracked files listed in the blocker log.

Review pass 2 skipped validation:

- Unity 2022.3 compile/import skipped because `which Unity` returned `Unity not found` and `which UnityHub` returned `UnityHub not found`; no Unity editor/CI command is available in this shell.

Parent validation:

- `git diff --check`: passed.
- Public DTO inspection: `git diff -- Runtime/GCSetupOptions.cs Runtime/GCPlayOptions.cs` produced no diff; `GCSetupOptions`, `GCPlayOptions`, and `GCPlayerOptions` shapes remain unchanged.
- Scoped old serialized field inspection: `gameModeId`, `playerData`, `numberOfPlayers`, and `randomizePlayerIds` remain only as hidden deserialization fields; setup/play capture reads `gc.dev.json` data instead.
- Callback bypass inspection: editor `GamingCouchSetupOptions`, `GamingCouchSetup`, `_EditorPlay`, `GamingCouchPlay`, and `SetupDone` all require the cached file-backed capture before listener setup/play callbacks can run.
- Dense sparse-seat inspection: `GCEditorPlayCapture` counts enabled seats, assigns runtime `playerId = activePlayerIndex + 1`, and stores one-based source seat slots in `GCSeatIdentity.sourceSeatIndex`.
- Seed inspection: `seed: "random"` resolves once in `GCEditorPlayCapture.Capture()` with `UnityEngine.Random.Range(1, 1000000)`; fixed seeds pass existing validation and parse into the same captured `GCPlayOptions`.
- Custom inspector inspection: no active `CustomEditor`, `OnInspectorGUI`, `GamingCouchEditor`, or `Editor/` source changes were added in Task 3.
- `git status --short`: only Task 3 owned files are modified beyond the pre-existing unrelated untracked files listed in the blocker log.
- Unity 2022.3 compile/import skipped because `which Unity` returned `Unity not found` and `which UnityHub` returned `UnityHub not found`; no Unity editor/CI command is available in this shell.

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

### Task 4 Review Record

Status: Done

Review pass 1 findings:

- No code defects found.
- Plan status was corrected to keep Task 4 in review until pass 2 and parent validation complete.

Review pass 2 findings:

- No code defects found.
- Task 4 remains in review for parent validation.

Changed paths:

- `Editor/GamingCouchEditor.cs`
- `Editor/GamingCouchEditor.cs.meta`
- `Editor/GCDevJsonInspectorState.cs`
- `Editor/GCDevJsonInspectorState.cs.meta`
- `Editor/GCDevJsonInspectorView.cs`
- `Editor/GCDevJsonInspectorView.cs.meta`
- `docs/architecture/unity-dev-json-sync-implementation-tasks.md`

Validation:

- `git diff --check`: passed.
- `rg -n "[ \t]+$" <new Task 4 editor files and metas>`: no trailing whitespace matches.
- Active inspector inspection: `Editor/GamingCouchEditor.cs` registers `[CustomEditor(typeof(GamingCouch))]` and implements `OnInspectorGUI`.
- Serialized field inspection: `GamingCouchEditor.OnInspectorGUI` calls `GamingCouchInspectorHost.DrawSerializedFields(serializedObject)` before drawing file-backed local play settings, so normal component fields remain visible.
- Obsolete field inspection: `GamingCouchInspectorHost` still excludes `gameModeId`, `playerData`, `numberOfPlayers`, and `randomizePlayerIds`.
- Apply inspection: editor Apply calls only `GCDevJsonStore.Write(Draft.ToFile(), metadataReadResult)`; the only `File.WriteAllText` path remains the existing preserving `gc.dev.json` writer in `Runtime/Dev/GCDevJsonStore.cs`.
- Revert inspection: the Revert button calls `GCDevJsonInspectorState.Reload()`, which re-reads project-root `gc.metadata.json` and `gc.dev.json` from disk and rebuilds the clean draft.
- Missing/invalid `gc.dev.json` inspection: when `GCDevJsonStore.Read()` cannot produce data, the inspector has no draft, displays structured issues, and `CanApply` is false.
- Missing/invalid metadata inspection: invalid metadata makes the view use raw `Entry Key` editing; draft validation copies metadata warnings, but `CanApply` follows `GCDevJsonValidationResult.IsValid`, which blocks only errors.
- Valid metadata gate inspection: draft validation calls `GCDevJsonValidation.ValidateData(Draft.ToFile(), DevJsonPath, metadataReadResult)`, so valid metadata platform, entry existence, and enabled-seat count gates disable Apply through validation errors.
- Local play setting serialization inspection: file-backed controls mutate only the editor draft after `serializedObject.ApplyModifiedProperties()` has already run for normal component fields; no obsolete serialized editor play settings are written by the inspector.
- Task 5 scope inspection: no `EditorApplication.update`, `playModeStateChanged`, `FileSystemWatcher`, external timestamp polling, conflict state, or pending-play hooks are present in the Task 4 editor files.
- `git status --short`: only Task 4 owned editor files and this task record are touched beyond the pre-existing unrelated untracked files listed in the blocker log.
- Pass 2 `git diff --check`: passed.
- Pass 2 trailing whitespace inspection: `rg -n "[ \t]+$" <Task 4 editor files, metas, and task record>` returned no matches.
- Pass 2 editor assembly inspection: runtime assembly internals are exposed to `GamingCouch.Editor` through `InternalsVisibleTo("GamingCouch.Editor")`, and `Editor/dsb.gamingcouch.editor.asmdef` names the editor assembly `GamingCouch.Editor`.
- Pass 2 `.meta` inspection: new Task 4 script `.meta` files use `fileFormatVersion: 2`, `MonoImporter`, and unique 32-character hex GUIDs not duplicated elsewhere in repo `.meta` files.
- Pass 2 IMGUI inspection: Task 4 editor UI uses Unity 2022.3-safe IMGUI calls (`EditorGUILayout.Popup`, `EditorGUILayout.IntField`, `EditorGUILayout.GetControlRect`, `EditorGUI.DrawRect`, `EditorGUI.DisabledScope`, and `EditorGUILayout.HorizontalScope`) without unavailable overloads.
- Pass 2 draft read-state inspection: `GCDevJsonDraft.FromFile` is called only when `GCDevJsonStore.Read()` produced non-null parsed data; invalid read states leave the inspector without a draft and cannot throw through draft construction.
- Pass 2 apply/revert inspection: Apply is disabled without a dirty valid draft, delegates to `GCDevJsonStore.Write`, and the store rejects missing `gc.dev.json`; Revert calls `Reload()` to re-read both root JSON files and rebuild the clean draft.
- Pass 2 metadata behavior inspection: missing or invalid metadata remains warning-only and uses raw entry-key editing, while valid metadata still gates Apply through platform, entry existence, and enabled-seat count validation errors.
- Pass 2 Task 5 scope inspection: no polling, conflict state, pending-play state, `EditorApplication.update`, `playModeStateChanged`, `FileSystemWatcher`, `LastWriteTime`, or timestamp tracking exists in the Task 4 editor files.
- Pass 2 `git status --short`: only Task 4 owned editor files and this task record are touched beyond the pre-existing unrelated untracked files listed in the blocker log.

Skipped validation:

- Unity 2022.3 compile/import skipped because `command -v Unity` and `command -v UnityHub` both returned not found in this shell.
- Pass 2 Unity compile/import skipped because no Unity 2022.3 editor is installed under `/Applications/Unity/Hub/Editor`; only Unity `6000.2.7f2` was found, and this package repo has no Unity project `ProjectSettings` or `Assets` directory to import without creating unrelated project files.

Decisions:

- No `Runtime/Dev/GCDevJsonStore.cs` or `Runtime/Dev/GCDevJsonValidation.cs` helper changes were needed.
- `Runtime/Dev/GCDevJsonFile.cs` and `Runtime/Dev/GCMetadataJsonFile.cs` were left unchanged because existing internal models exposed enough data for safe editor draft cloning and display.
- External polling, conflict state, metadata refresh on disk changes, and pending play state were left for Task 5.

Parent validation:

- `git diff --check`: passed.
- `rg -n "[ \t]+$" <Task 4 editor files, metas, and task record>`: no trailing whitespace matches.
- Write-path inspection: Task 4 editor code calls `GCDevJsonStore.Write(Draft.ToFile(), metadataReadResult)` only; the only `File.WriteAllText` path remains the existing preserving `gc.dev.json` writer in `Runtime/Dev/GCDevJsonStore.cs`.
- Revert inspection: `GCDevJsonInspectorState.Reload()` re-reads root `gc.metadata.json` and `gc.dev.json` and rebuilds the clean draft from disk.
- Obsolete serialized field inspection: `GamingCouchEditor` draws normal fields through `GamingCouchInspectorHost`, which still excludes `gameModeId`, `playerData`, `numberOfPlayers`, and `randomizePlayerIds`; local play controls mutate only the in-memory draft.
- Metadata behavior inspection: missing or invalid metadata uses raw entry-key editing and warning-only validation, while valid metadata gates Apply through existing platform, entry existence, and enabled-seat-count errors.
- Task 5 scope inspection: no polling, conflict state, pending-play state, `EditorApplication.update`, `playModeStateChanged`, `FileSystemWatcher`, `LastWriteTime`, or timestamp tracking exists in the Task 4 editor files.
- `.meta` inspection: new Task 4 script GUIDs are present once each in the repository.
- `git status --short`: only Task 4 owned editor files and this task record are touched beyond the pre-existing unrelated untracked files listed in the blocker log.
- Unity 2022.3 compile/import skipped because no Unity 2022.3 editor is installed under `/Applications/Unity/Hub/Editor`; only Unity `6000.2.7f2` is present, and this package repo has no Unity project `ProjectSettings` or `Assets` directory to import without creating unrelated files.

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

### Task 5 Review Record

Status: Done

Review pass 1 findings:

- No blocking code defects found.
- The Task 5 record was prematurely marked done and advanced the plan to Task 6 before review pass 2 and parent validation. The record now keeps Task 5 in review.

Review pass 1 patches:

- Updated this task record and top-level status to keep Task 5 in review until pass 2 and parent validation complete.

Changed paths:

- `Editor/GamingCouchEditor.cs`
- `Editor/GCDevJsonInspectorState.cs`
- `Editor/GCDevJsonInspectorView.cs`
- `Runtime/Dev/GCDevJsonStore.cs`
- `Runtime/Dev/GCMetadataJsonStore.cs`
- `docs/architecture/unity-dev-json-sync-implementation-tasks.md`

Validation:

- `git diff --check`: passed.
- Clean external `gc.dev.json` edit inspection: `GamingCouchEditor` polls through `EditorApplication.update`, `GCDevJsonInspectorState.PollForExternalChanges()` detects root-file stamp changes, and edit-mode clean dev-file changes call `ReloadFromDisk(false)` to replace the inspector draft from disk.
- Dirty external `gc.dev.json` edit inspection: when `IsDirty` or an existing conflict is present, dev-file stamp changes call `EnterConflict()` without calling `ReloadFromDisk`, preserving inspector draft values.
- `Reload from disk` inspection: the conflict button calls `GCDevJsonInspectorState.Reload()`, which discards the draft, clears conflict state, reads current `gc.metadata.json`, reads current `gc.dev.json`, rebuilds the clean draft, and refreshes file stamps.
- `Write draft` inspection: the conflict button calls `GCDevJsonInspectorState.WriteDraft()`, which validates the current draft and writes through `GCDevJsonStore.Write()`, preserving unrelated top-level `gc.dev.json` fields through the existing `JObject` writer.
- Metadata refresh inspection: metadata-file stamp changes call `RefreshMetadata()`, re-read `gc.metadata.json`, keep any existing draft in memory, and re-run draft validation so metadata-derived labels and errors update without discarding dirty edits.
- Active Play Mode pending inspection: play-mode file changes set `HasPendingPlayChange`; clean `gc.dev.json` changes during active Play Mode are deferred as unloaded disk changes until edit mode returns, dirty/pending edits enter conflict before writing, and no play capture, auto-apply, or blocking code was added.
- Polling implementation inspection: root file stamps use path, existence, `LastWriteTimeUtc`, byte length, and SHA-256 content hash; no `FileSystemWatcher` code was added.
- `git status --short`: only Task 5 owned files were modified beyond the pre-existing unrelated untracked files listed in the blocker log.

Skipped validation:

- Unity 2022.3 compile/import skipped because `which Unity` returned `Unity not found`, `/Applications/Unity/Hub/Editor` only listed `6000.2.7f2`, and this package repo has no Unity project `ProjectSettings` or `Packages/manifest.json` to import directly in this shell.

Review pass 1 validation:

- `git diff --check`: passed.
- Polling lifecycle inspection: `GamingCouchEditor.OnEnable()` removes then adds `EditorApplication.update` and `EditorApplication.playModeStateChanged` handlers for the inspector instance; `OnDisable()` and destroyed-target handling both unsubscribe through `DisposeDevJsonState()`.
- File stamp inspection: root JSON polling uses `GCRootJsonFileStamp` with path, existence, `LastWriteTimeUtc`, byte length, and SHA-256 content hash. No `FileSystemWatcher` references are present.
- Clean edit-mode `gc.dev.json` inspection: when the dev-file stamp changes with no dirty draft or conflict, `PollForExternalChanges()` calls `ReloadFromDisk(false)`, rebuilding the inspector draft from disk and refreshing file stamps.
- Dirty edit-mode `gc.dev.json` inspection: when the dev-file stamp changes with `IsDirty` or an existing conflict, `EnterConflict()` is called without reloading, preserving the draft values.
- Conflict action inspection: `Reload from disk` calls `Reload()` and discards the draft by reading current `gc.metadata.json` and `gc.dev.json`; `Write draft` calls `WriteDraft()`, validates `Draft.ToFile()`, and writes through `GCDevJsonStore.Write()`.
- Preserving writer inspection: the only Task 5 write path still delegates to `GCDevJsonStore.Write()`, which loads the current file as a `JObject`, replaces canonical fields, and writes indented JSON plus a trailing newline.
- Metadata refresh inspection: metadata stamp changes call `RefreshMetadata()`, keep an existing draft in memory, and re-run validation so dirty drafts stay dirty while labels and metadata-derived errors refresh.
- Active Play Mode inspection: Play Mode dev-file changes set pending state and do not call `ReloadFromDisk()` for clean changes until edit mode returns; no Task 6 auto-apply, Play Mode entry blocking, or restart-gate code was added.
- Task 6 scope inspection: `Runtime/GamingCouch.cs` and `Runtime/Dev/GCEditorPlayCapture.cs` have no Task 5 diff; scoped search for Play Mode entry blocking, restart gates, auto-apply, and cancellation terms in Task 5 files matched only the pending-state user-facing message.
- Static write/path inspection: `rg -n "FileSystemWatcher|File.WriteAllText|WriteDraft|Apply\\(|EnterConflict|ReloadFromDisk|HandlePlayModeStateChanged" Editor Runtime/Dev` matched only the expected polling, conflict, preserving write, and existing store-write paths.
- `git status --short`: only Task 5 owned files are modified beyond the pre-existing unrelated untracked files listed in the blocker log.

Review pass 1 skipped validation:

- Unity 2022.3 compile/import skipped because `which Unity` returned `Unity not found`, `/Applications/Unity/Hub/Editor` only listed `6000.2.7f2`, and this package repo has no Unity project `ProjectSettings` or `Packages/manifest.json` to import directly in this shell.

Review pass 2 findings:

- No concrete code defects found.
- Own write inspection: `Apply()` and conflict `WriteDraft()` both write through `WriteDraftToDisk()`, which delegates to the preserving `GCDevJsonStore.Write()` path and then calls `ReloadFromDisk(...)`; that reload clears conflict state, rebuilds clean data from current disk, and refreshes file stamps so the next poll does not treat the just-written file as an external edit.
- Dirty metadata refresh inspection: metadata stamp changes call `RefreshMetadata()`, keep an existing draft in memory, and re-run `ValidateDraft()`, so dirty drafts remain dirty while new metadata-derived validation errors can appear.
- Clean edit-mode dev-file inspection: clean `gc.dev.json` stamp changes outside Play Mode call `ReloadFromDisk(false)` and replace the inspector draft with current disk state.
- Play-mode pending inspection: active Play Mode dev-file changes set pending state and either defer clean disk reloads through `hasUnloadedPlayDevJsonChange` or enter conflict for dirty drafts; no active setup/play capture objects are mutated by the inspector poll path.
- Conflict action inspection: `Reload from disk` calls `Reload()` and clears conflict through `ReloadFromDisk(...)`; `Write draft` validates, overwrites current disk through `GCDevJsonStore.Write()`, then reloads and clears conflict after a successful write.
- Polling lifecycle inspection: `GamingCouchEditor.OnEnable()` removes then adds update/play-mode callbacks, and both `OnDisable()` and destroyed-target handling unsubscribe through `DisposeDevJsonState()`, avoiding duplicate callbacks for the same inspector instance.
- Task 6 scope inspection: no Play Mode entry blocking, auto-apply, Gaming Couch restart gate, or cancellation path was added; Task 5 play-mode handling is limited to pending-state UI/poll bookkeeping.

Review pass 2 patches:

- No code patches required.
- Updated this Task 5 review record with pass 2 findings and validation while keeping Task 5 `In review` for parent validation.

Review pass 2 validation:

- `git diff --check`: passed.
- `rg -n "FileSystemWatcher" Editor/GCDevJsonInspectorState.cs Editor/GCDevJsonInspectorView.cs Editor/GamingCouchEditor.cs Runtime/Dev/GCDevJsonStore.cs Runtime/Dev/GCMetadataJsonStore.cs`: no matches.
- Expected write path inspection: `rg -n "File\\.WriteAllText|devStore\\.Write|WriteDraftToDisk|WriteDraft\\(|Apply\\(" Editor/GCDevJsonInspectorState.cs Editor/GCDevJsonInspectorView.cs Runtime/Dev/GCDevJsonStore.cs` matched only the inspector Apply/WriteDraft calls, `WriteDraftToDisk()`, `devStore.Write(...)`, and the preserving store's existing `File.WriteAllText(...)`.
- Task 6 scope inspection: scoped search for Play Mode blocking, auto-apply, restart gates, cancellation, and play-mode entry terms in Task 5 files matched only `playModeStateChanged`, `EnteredPlayMode` bookkeeping, and the pending-state user-facing message.
- `git diff -- Runtime/GamingCouch.cs Runtime/Dev/GCEditorPlayCapture.cs`: no output.
- Compile/API hazard inspection: `SHA256.Create()` is used inside the file-stamp read try/catch and serialized with Base64 output, `FileInfo.LastWriteTimeUtc` and file length are paired with a content hash, `EditorApplication.timeSinceStartup` is used as a double poll throttle, and IMGUI controls remain inside normal `BeginChangeCheck`/`EndChangeCheck` and disabled-scope patterns.
- `git diff --name-only`: only Task 5 owned files and this task plan are modified.
- `git status --short`: only Task 5 owned files are modified beyond the pre-existing unrelated untracked files listed in the blocker log.

Review pass 2 skipped validation:

- Unity 2022.3 compile/import skipped because `which Unity` returned `Unity not found`, `/Applications/Unity/Hub/Editor` listed only `6000.2.7f2`, `ProjectSettings/` is absent, and `Packages/manifest.json` is absent, so this package repo is not directly importable as a Unity project from this shell.

Notes:

- Task 6 auto-apply and Play Mode/Gaming Couch restart blocking remain unimplemented by design.
- `Editor/GamingCouchInspectorHost.cs` was inspected as an owned Task 5 file but did not require changes.

Parent validation:

- `git diff --check`: passed.
- `rg -n "FileSystemWatcher" <Task 5 files>`: no matches.
- Write-path inspection: inspector `Apply` and `Write draft` both flow through `WriteDraftToDisk()`, `GCDevJsonStore.Write(...)`, and the existing preserving `File.WriteAllText(...)` path only.
- Clean edit-mode external dev-file inspection: stamp changes with no dirty draft or conflict call `ReloadFromDisk(false)` and rebuild the inspector draft from current disk state.
- Dirty external dev-file inspection: stamp changes with `IsDirty` or conflict call `EnterConflict()` without reloading, preserving draft values.
- Conflict action inspection: `Reload from disk` calls `Reload()` and clears conflict through `ReloadFromDisk(...)`; `Write draft` validates then overwrites current disk through `GCDevJsonStore.Write()` and reloads after successful write.
- Metadata refresh inspection: metadata stamp changes call `RefreshMetadata()`, keep any existing draft, and re-run validation so metadata-derived labels and errors update without discarding dirty edits.
- Active Play Mode inspection: file changes set pending state and defer clean disk reloads until edit mode returns; Task 5 files do not call play capture, auto-apply, block Play Mode entry, or add restart gates.
- Task 6 scope inspection: `git diff -- Runtime/GamingCouch.cs Runtime/Dev/GCEditorPlayCapture.cs` produced no diff.
- `git diff --name-only`: only Task 5 owned files and this task plan are modified.
- `git status --short`: only Task 5 owned files are modified beyond the pre-existing unrelated untracked files listed in the blocker log.
- Unity 2022.3 compile/import skipped because `which Unity` returned `Unity not found`, `/Applications/Unity/Hub/Editor` listed only `6000.2.7f2`, and this package repo has no `ProjectSettings/` or `Packages/manifest.json`.

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

### Task 6 Review Record

Status: Done

Changed paths:

- `Runtime/GamingCouch.cs`
- `Runtime/Dev/GCEditorPlayCapture.cs`
- `Runtime/Dev/GCDevJsonValidation.cs`
- `Editor/GamingCouchEditor.cs`
- `Editor/GCDevJsonInspectorState.cs`
- `docs/architecture/unity-dev-json-sync-implementation-tasks.md`

Validation:

- Review-and-patch pass 1: no source defects patched; task status record corrected to keep Task 6 in review until pass 2 and parent validation.
- Review-and-patch pass 2: patched multi-inspector preflight ordering so all registered inspectors validate before any dirty draft is auto-applied, and Play Mode/restart is blocked when multiple inspectors have unsaved drafts; also stopped `EnteredPlayMode` from advancing JSON file stamps after capture so post-capture changes remain detectable as pending play changes.
- `git diff --check`: passed.
- Play Mode entry auto-apply inspection: `GCDevJsonEditorPlayModeGate` runs on `PlayModeStateChange.ExitingEditMode`, registered inspector states call `PrepareForPlayBoundary(...)`, and valid non-conflicted dirty drafts write through the existing preserving `GCDevJsonStore.Write()` path before root validation.
- Play Mode entry blocking inspection: conflicted inspector drafts return a failed preflight before Play Mode entry, invalid dirty drafts return draft validation failures, and root `gc.dev.json` read/metadata gate failures cancel entry via `EditorApplication.isPlaying = false`.
- Multiple-inspector determinism inspection: preflight snapshots registered states, polls and validates each with `ValidateForPlayBoundary(...)` before writing, permits only a single dirty inspector draft to auto-apply, and fails deterministically before disk mutation when multiple inspectors have unsaved drafts.
- Setup/play shared capture inspection: `GamingCouch` still captures once into `editorPlayCapture` at `Awake`, and editor setup/play callbacks continue to reuse the cached setup/play options and seat identities for the same editor run.
- Active Play Mode mutation inspection: inspector polling still records pending/unloaded play changes without recapturing or mutating `editorPlayCapture`, `setupOptions`, `playOptions`, players, seed, or game mode during active Play Mode.
- Capture notification inspection: `NotifyCaptureSucceeded()` reloads inspector state only when it is clean and non-conflicted, preserves dirty/conflicted drafts by updating stamps only, and `EnteredPlayMode` no longer masks a JSON edit that lands after capture notification.
- Gaming Couch restart auto-apply and recapture inspection: restart paths call the editor preflight bridge before resetting/reloading, valid dirty drafts are written first, public `Restart()` recaptures before calling `Start()`, and scene-reload restart relies on the new `Awake` capture before setup/play callbacks run.
- Gaming Couch restart blocking inspection: restart preflight returns without clearing state, reloading the scene, or calling setup/play when the active draft is conflicted, invalid, or root `gc.dev.json` fails read/metadata validation.
- Logging inspection: blocked Play Mode entry, blocked restart, missing/invalid `gc.dev.json`, and valid metadata gate failures log boundary-specific errors plus formatted issue code/path/field details.
- Editor-only guard and assembly inspection: Task 6 runtime-dev preflight/capture/formatter types are guarded by `#if UNITY_EDITOR`, `Runtime/GamingCouch.cs` calls them only inside `#if UNITY_EDITOR`, `Runtime/dsb.gamingcouch.runtime.asmdef` has no editor assembly reference, and `Editor/dsb.gamingcouch.editor.asmdef` references the runtime assembly.
- Production/WebGL inspection: new preflight and capture-notification calls are guarded by `#if UNITY_EDITOR`; non-editor and `UNITY_WEBGL && !UNITY_EDITOR` platform callback behavior remains on existing paths.
- DTO/package scope inspection: `git diff -- package.json Runtime/GCSetupOptions.cs Runtime/GCPlayOptions.cs Runtime/GCPlayerOptions.cs` produced no output.
- `git status --short`: Task 6 owned files are modified; pre-existing unrelated untracked files from the blocker log remain untouched.

Skipped validation:

- Unity 2022.3 compile/import skipped because `command -v Unity` and `command -v UnityHub` returned no executable, `/Applications/Unity/Hub/Editor` only contains `6000.2.7f2`, and this package repo has no `ProjectSettings` or `Packages/manifest.json` for a safe package-local import run.

Decisions:

- No new helper file was added. The runtime/editor bridge lives in existing `Runtime/Dev/GCEditorPlayCapture.cs`, while editor Play Mode subscription/inspector-state coordination lives in existing `Editor/GamingCouchEditor.cs`.
- The runtime assembly does not reference editor assembly types. Runtime restart code calls an editor-only runtime-dev callback bridge, and the editor assembly registers the inspector-state preflight handler through `InternalsVisibleTo("GamingCouch.Editor")`.

Parent validation:

- `git diff --check`: passed.
- DTO/package/asmdef inspection: `git diff -- package.json Runtime/GCSetupOptions.cs Runtime/GCPlayOptions.cs Runtime/GCPlayerOptions.cs Runtime/dsb.gamingcouch.runtime.asmdef Editor/dsb.gamingcouch.editor.asmdef` produced no output.
- Runtime editor-API inspection: scoped search for `UnityEditor`, `EditorApplication`, `PlayModeStateChange`, `InitializeOnLoad`, `EditorGUI`, and `EditorGUILayout` under `Runtime/` produced no matches outside files already guarded by `#if UNITY_EDITOR`.
- Runtime/editor assembly boundary inspection: `Runtime/GamingCouch.cs` calls preflight and capture-notification APIs only inside `#if UNITY_EDITOR`; editor code registers handlers through `GCEditorPlayPreflight`, so the runtime assembly still has no editor assembly dependency.
- Play Mode entry gate inspection: `GCDevJsonEditorPlayModeGate` runs at `ExitingEditMode`, validates all registered inspectors before any write, auto-applies at most one valid dirty draft, blocks conflicted/invalid/multiple dirty drafts, validates root `gc.dev.json`, and cancels entry with `EditorApplication.isPlaying = false` on failure.
- Capture timing inspection: `GamingCouch` captures `editorPlayCapture` at `Awake`, editor setup/play callbacks reuse that same capture during the editor run, and `NotifyCaptureSucceeded()` clears pending inspector state without masking later post-capture file changes.
- Restart gate inspection: public `Restart()` and `_HandleGamePlayModeRestart()` both run restart preflight before clearing state or reloading; successful public restart recaptures before `Start()`, while scene-reload restart relies on the next `Awake` capture.
- Production/WebGL inspection: non-editor and `UNITY_WEBGL && !UNITY_EDITOR` platform callback paths are unchanged.
- `git status --short`: only Task 6 owned files are modified beyond the pre-existing unrelated untracked files listed in the blocker log.
- Unity 2022.3 compile/import skipped because `command -v Unity` and `command -v UnityHub` returned no executable, `/Applications/Unity/Hub/Editor` only contains `6000.2.7f2`, and this package repo has no `ProjectSettings` or `Packages/manifest.json` for a safe package-local import run.

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

### Task 7 Review Record

Status: Done

Changed paths:

- `README.md`
- `Documentation~/README.md`
- `CHANGELOG.md`
- `package.json`
- `docs/architecture/unity-dev-json-sync-implementation-tasks.md`

Validation:

- Review pass 1 inspected the existing Task 7 docs/package diff and patched this task record to keep Task 7 in review until pass 2 and parent validation complete.
- Review pass 2 inspected the current Task 7 docs/package diff and found no concrete docs, changelog, or package metadata defects requiring product-file changes; Task 7 remains `In review` for parent validation.
- `git diff --check`: passed.
- Pass 2 `git diff --check`: passed.
- `node -e "const p=require('./package.json'); if (p.version !== '0.1.0-alpha.2') throw new Error('version '+p.version); const d=p.dependencies && p.dependencies['com.unity.nuget.newtonsoft-json']; if (!d) throw new Error('missing newtonsoft dependency'); console.log('version='+p.version); console.log('com.unity.nuget.newtonsoft-json='+d);"`: passed with `version=0.1.0-alpha.2` and `com.unity.nuget.newtonsoft-json=3.2.1`.
- Pass 2 package metadata check: `node -e "const fs=require('fs'); const text=fs.readFileSync('package.json','utf8'); const p=JSON.parse(text); if (p.version !== '0.1.0-alpha.2') throw new Error('version '+p.version); const deps=p.dependencies || {}; const names=Object.keys(deps).filter(k=>k==='com.unity.nuget.newtonsoft-json'); if (names.length !== 1) throw new Error('dependency count '+names.length); if (deps['com.unity.nuget.newtonsoft-json'] !== '3.2.1') throw new Error('newtonsoft '+deps['com.unity.nuget.newtonsoft-json']); console.log('package.json parses'); console.log('version='+p.version); console.log('com.unity.nuget.newtonsoft-json='+deps['com.unity.nuget.newtonsoft-json']);"` passed with `package.json parses`, `version=0.1.0-alpha.2`, and `com.unity.nuget.newtonsoft-json=3.2.1`.
- Documentation sync-behavior check: `rg -n "gc\\.dev\\.json|gc\\.metadata\\.json|newtonsoft|sync|0\\.1\\.0-alpha\\.2" README.md Documentation~/README.md CHANGELOG.md` confirmed the root `gc.dev.json` sync behavior, metadata warning/gate behavior, editor-only Newtonsoft dependency, and changelog release entry are documented.
- Pass 2 documentation sync-behavior check: `rg -n "gc\\.dev\\.json|gc\\.metadata\\.json|bootstrap|repair|metadata|Play Mode|restart|newtonsoft|Newtonsoft|sync|0\\.1\\.0-alpha\\.2|validated|manual" README.md Documentation~/README.md CHANGELOG.md` confirmed the required root `gc.dev.json`, no bootstrap/repair, metadata warning/gate behavior, Play Mode/restart gates, editor-only Newtonsoft dependency, and changelog release entry remain documented. `rg -n "validated|verified|Unity 2022\\.3|manual Unity|manual scenarios|scenario [0-9]|scenarios [0-9]" README.md Documentation~/README.md CHANGELOG.md` returned no matches, so the public docs/changelog do not overclaim manual Unity validation.
- `git diff --name-only`: only Task 7 owned tracked files are modified.
- Pass 2 changed-file check: `git diff --name-only` showed only `CHANGELOG.md`, `Documentation~/README.md`, `README.md`, `docs/architecture/unity-dev-json-sync-implementation-tasks.md`, and `package.json`.
- Pass 2 `.meta` diff check: `git diff --name-only | rg '\\.meta$'` returned no matches, confirming no tracked `.meta` files are modified.
- Protected-file diff check: `git diff --name-only -- VERSIONING_PLAN.md VERSIONING_PLAN.md.meta docs.meta docs/architecture.meta docs/architecture/unity-dev-json-sync-prep-execution-tasks.md.meta docs/architecture/unity-dev-json-sync-prep-refactor-roadmap.md.meta` produced no output. Their existing untracked status remains unchanged from the initial worktree status.
- Pass 2 protected-file status check: `git status --short -- VERSIONING_PLAN.md VERSIONING_PLAN.md.meta docs.meta docs/architecture.meta docs/architecture/unity-dev-json-sync-prep-execution-tasks.md.meta docs/architecture/unity-dev-json-sync-prep-refactor-roadmap.md.meta` still shows those protected files only as pre-existing untracked files.
- Main repo status check: `git -C /Users/anttil/dev/dsb/gamingcouch/client status --short` shows that the main repo is already dirty with unrelated modified/untracked files, including `docs/architecture/dev-flow-todo.md`, several docs, and game-local JSON files. Task 7 performed no writes outside `/Users/anttil/dev/dsb/gaming-couch-unity`.
- Pass 2 main-repo status check: `git -C /Users/anttil/dev/dsb/gamingcouch/client status --short` still shows only unrelated pre-existing main-repo changes; no main-repo files were edited by Task 7 pass 2.

Parent validation:

- `git diff --check`: passed.
- `node -e "const p=require('./package.json'); if (p.version !== '0.1.0-alpha.2') throw new Error('version '+p.version); const d=p.dependencies && p.dependencies['com.unity.nuget.newtonsoft-json']; if (d !== '3.2.1') throw new Error('newtonsoft '+d); console.log('ok version='+p.version+' newtonsoft='+d);"`: passed with `ok version=0.1.0-alpha.2 newtonsoft=3.2.1`.
- Documentation grep confirmed `README.md`, `Documentation~/README.md`, and `CHANGELOG.md` mention root `gc.dev.json`, `gc.metadata.json`, editor-only Newtonsoft JSON sync, no bootstrap/repair behavior, Play Mode/restart gates, and release version `0.1.0-alpha.2`.
- `git diff --name-only`: only Task 7 owned tracked files are modified.
- Protected-file diff check produced no output; the protected files remain untouched.
- Main repo status check still shows only unrelated pre-existing main-repo changes; Task 7 made no writes outside `/Users/anttil/dev/dsb/gaming-couch-unity`.

Skipped validation:

- Unity 2022.3 compile/import and manual Unity scenarios were not runnable in this shell: `command -v Unity` and `command -v UnityHub` returned no executable, `/Applications/Unity/Hub/Editor` contains only `6000.2.7f2`, and this package repo has no `ProjectSettings` directory or `Packages/manifest.json` for a safe package-local import run.

Final manual scenario status:

- Manual Unity 2022.3 scenarios 1-23 remain not run in this shell because Unity 2022.3 is unavailable. This includes inspector read/write, unknown-field preservation, external reload/conflict actions, metadata refresh/gates, missing/invalid `gc.dev.json` behavior, UPM dependency resolution, Play Mode/restart gates, active Play Mode deferral, and DevApp readback after Unity writes.

Non-blocking follow-up:

- DevApp package target bump to `unity-0.1.0-alpha.2` and DevApp Unity seed-range alignment remain separate main-repo follow-ups. No main-repo files were edited.

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
