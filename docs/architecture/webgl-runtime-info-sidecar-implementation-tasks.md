# WebGL Runtime Info Sidecar Implementation Tasks

Status: Ready for implementation
Last updated: 2026-05-27
Owner: Gaming Couch Unity package team

## Source Context

This plan turns the May 27, 2026 design discussion into an executable task list for the `implement-tasks` skill.

Primary intent:

- Write Unity package/runtime identity into WebGL build output before the Unity player is loaded.
- Enable a follow-up Gaming Couch upload and hosted runtime preflight to validate Unity build identity without launching the Unity loader, wasm, or runtime.
- Remove package-version drift by treating `package.json` as the source of truth for package name and package version.
- Keep DevApp Editor runtime registration working before any WebGL build exists.

Original behavior before this plan:

- Runtime C# duplicated package identity fields that now belong to package metadata.
- Hosted WebGL identity did not have a sidecar-first preflight artifact.
- DevApp Editor runtime registration built identity from the runtime DTO instead of a shared editor package identity helper.
- Clean WebGL export setup installs and selects the package-owned `PROJECT:GamingCouch` WebGL template.

Decisions:

- `package.json` is the source of truth for Unity package name and package version.
- `gameProtocolVersion` remains the Gaming Couch game integration contract version. This work must not bump it.
- DevApp Editor runtime registration must not read `gc.runtime-info.json`; Editor play may happen before any WebGL build exists.
- WebGL builds using the Gaming Couch export template write `gc.runtime-info.json` at the export root, next to `index.html`.
- The sidecar is a build artifact identity for diagnostics and future upload validation, not cryptographic attestation.
- Main Gaming Couch upload/client validation changes are out of scope for this Unity package task plan unless the user explicitly grants cross-repo edit permission.

Target sidecar shape; generated `packageName` and `packageVersion` values come from `package.json`:

```json
{
  "platform": "unity",
  "packageName": "<package.json name>",
  "packageVersion": "<package.json version>",
  "gameProtocolVersion": 1
}
```

## Tasks

| ID | Task | Status | Done when | Dependencies | Notes |
| --- | --- | --- | --- | --- | --- |
| 1 | Add a shared editor package identity helper | Completed | Editor code can resolve package name and version from Unity package metadata or package-root `package.json`; focused tests cover current manifest values and failure behavior; `package.json` remains the only source of truth for package version. | None | Keep the helper editor-only if it depends on `UnityEditor.PackageManager.PackageInfo`. Provide a small serializable identity DTO or conversion method matching the sidecar/runtime fields. |
| 2 | Route DevApp Editor runtime registration through the identity helper | Completed | `GCDevAppRuntimeMessages.BuildRuntimeRegisterMessage` uses the shared identity helper for package name/version while preserving the existing `runtime_register` wire fields; tests prove package name/version match `package.json`; no `gc.runtime-info.json` dependency is introduced. | 1 | Keep `gameProtocolVersion` and `platform` in one package-owned identity path. If runtime C# still needs identity for non-editor builds, avoid reintroducing package-version duplication. |
| 3 | Add WebGL runtime-info sidecar writer for Gaming Couch template builds | Completed | A WebGL build postprocess writes `gc.runtime-info.json` to the build output root only when the active WebGL template is `PROJECT:GamingCouch`; it writes valid JSON with `platform`, `packageName`, `packageVersion`, and `gameProtocolVersion`; non-WebGL builds and WebGL builds using other templates do not write the sidecar. | 1 | Prefer a testable writer service plus a thin `IPostprocessBuildWithReport` wrapper. Use the build report output path and place the file beside `index.html`. Overwrite stale sidecar output on rebuild. |
| 4 | Remove the unreleased hosted identity path | Completed | The implementation records the hosted identity path removal decision; tests and docs reflect the sidecar as the chosen primary identity path; no duplicate `packageVersion` source remains. | 2, 3 | Since that path was not released or concretely used, prefer sidecar-first behavior. Keep an alternate hosted identity path only if there is a clear backward-compatibility reason. |
| 5 | Document sidecar generation and versioning rules | Completed | README, `Documentation~/README.md`, clean WebGL export PRD, and `VERSIONING_PLAN.md` explain that Gaming Couch template WebGL builds emit `gc.runtime-info.json`, that `package.json` owns package version, and that `gameProtocolVersion` is not bumped for sidecar/upload validation. | 3, 4 | Avoid defining main-repo upload validation policy unless that code is implemented separately. Phrase upload validation as what the sidecar enables. |
| 6 | Add focused validation coverage | Not started | Editor tests cover helper parsing, DevApp registration identity, sidecar write/skip behavior, JSON shape, and package-version drift prevention; focused open-Editor EditMode validation passes or any blocker is recorded. | 1, 2, 3, 4, 5 | Prefer tests that call the writer service with temp directories instead of requiring a full WebGL build. Use the open-Editor bridge from `AGENTS.local.md` for final focused validation. |

## Execution Rules

- Execute tasks in order using `implement-tasks` PRD task table mode.
- Mark exactly one task `In progress` before editing it, then mark it `Completed` only after validation, staging, and commit.
- Inspect `git status --short` before each task and preserve unrelated dirty worktree changes.
- Do not edit the Gaming Couch main repo, GC SDK, GC Client, or GC DevApp without explicit user permission.
- Keep Unity `.meta` files in sync for any new files.
- Prefer editor-only implementation for build/export helpers.
- Keep runtime/public API changes narrowly scoped. If a `gameProtocolVersion` bump seems necessary, stop and ask the user; current decision is no bump.
- Do not use `AGENTS.local.example.md` as live path source. Resolve the host Unity project from `AGENTS.local.md` when running open-Editor tests.

## Validation Guidance

Use focused static and editor validation first:

```bash
python3 Tools/check-runtime-package-info.py
python3 Tools/run-open-unity-tests.py /Users/anttil/dev/dsb/gaming-couch-unity-template --mode EditMode --filter RuntimeInfo|DevAppRuntimeMessages|WebGL
```

If the local host project path differs, resolve it from `AGENTS.local.md`.

Focused test areas:

- `GCRuntimeInfoTests`
- `GCDevAppRuntimeMessagesTests`
- `GamingCouchActiveSceneSetupAssetTests` WebGL export/template coverage
- New tests for `gc.runtime-info.json` writer behavior

Manual smoke validation, if time allows:

- In a Unity host project using the Gaming Couch template, build WebGL and confirm `gc.runtime-info.json` exists next to `index.html`.
- Inspect the JSON and confirm package version matches `package.json`.
- Switch away from the Gaming Couch template, build or call the writer path, and confirm no sidecar is produced for non-Gaming Couch templates.

## Follow-Up Outside This Repo

After this Unity package change lands, the Gaming Couch main repo can add upload/client validation that reads `gc.runtime-info.json` before loading Unity. That follow-up can decide whether and how to validate sidecar presence, `platform`, SemVer `packageVersion`, minimum supported package version, and supported `gameProtocolVersion`.
