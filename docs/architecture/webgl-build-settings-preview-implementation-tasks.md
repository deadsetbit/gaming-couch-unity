# WebGL Build Settings Preview Implementation Tasks

Status: Ready for implementation
Last updated: 2026-05-18
Owner: Gaming Couch Unity package team

## Source Context

This plan turns the May 18, 2026 design discussion into an executable task list for the `implement-tasks` skill.

Primary intent:

- Show a generated preview before applying any GamingCouch WebGL build settings route.
- Avoid drift by deriving preview rows, apply behavior, readiness checks, and result messages from the same internal setting definitions.
- Let users skip individual settings for the current apply run only.
- Include documentation changes, not just code changes.

Relevant existing entry points:

- `GamingCouch/WebGL Build/Preview dev build settings (fast build)`
- `GamingCouch/WebGL Build/Preview release build settings (slow build)`
- `GamingCouch/WebGL Build/Preview web export settings`
- Start Screen checklist row `Web export settings configured`

Current user decisions:

- Preview always appears for every route above.
- Preview rows are diff-only: `Setting: current -> target`.
- Preview lists changed items by default, with a concise already-configured state when there is no diff.
- One-time skips are allowed for settings rows only. Skipped settings are not persisted and must still be reported as drift by readiness later.
- Web export settings preview shows all planned changes, but only profile/settings-style rows are skippable. Web export template install/selection and active build target switch are visible but not individually skippable.
- Explicit dev/release profile routes remain profile-only and must not switch the active build target.
- Docs should avoid exact manual setting lists. The preview is the authoritative detailed setting list.

This file tracks implementation work only. Creating this plan does not implement production code.

## Status

Overall status: Implemented

Current task: None

Next action: None.

| ID | Task | Status | Done when | Dependencies | Notes |
| --- | --- | --- | --- | --- | --- |
| 1 | Refactor WebGL build profiles into generated setting specs | Completed | Dev and release profile settings are represented by one internal spec list per profile; preview diffs, apply behavior, readiness drift, and result details are generated from those specs; existing full-apply wrappers still work. | None | Cover IL2CPP code generation, managed stripping, mesh stripping, data caching, compression, exception support, debug symbols, WebAssembly 2023 when available, development build, and wasm code optimization. |
| 2 | Add selective apply and one-time skip support | Completed | Profile apply accepts selected setting ids; unchecked/skipped rows are omitted only for that apply call; skipped settings remain detected by readiness afterward; no project preference or persistent skip state is introduced. | 1 | Keep ids stable and internal. If a setting is already matching, it should not need selection. |
| 3 | Model web export settings as a previewable setup plan | Completed | Web export settings can dry-run planned template install/reuse/blockers, template selection, active WebGL target switch, splash/logo changes, and release-profile setting changes before mutating; non-skippable rows are visible but cannot be unchecked. | 1, 2 | Do not overwrite project-local template files. Block wrong-kind template path collisions before apply. |
| 4 | Build the shared preview popup and route all WebGL menu commands through it | Completed | Dev profile, release profile, and web export settings menu items all open the same reusable popup before mutation; changed settings are checked by default; no-change state reports already configured; apply/cancel behavior is explicit. | 2, 3 | Use one shared editor UI implementation. Avoid separate menu-specific copy. |
| 5 | Wire the Start Screen WebGL action to the shared preview flow | Completed | Clicking `Set Up Web Export` from the Start Screen opens the same preview popup and applies through the same plan/apply service; Start Screen does not duplicate WebGL setup logic; warning/error outcomes still render through existing Start Screen feedback patterns. | 4 | Preserve existing active-scene setup behavior and do not fold WebGL export into global scene setup. |
| 6 | Update docs and architecture notes to make preview authoritative | Completed | README, `Documentation~/README.md`, web export settings PRD, Start Screen PRD, and architecture/context docs no longer maintain stale exact setting lists; they explain that the preview shows the authoritative diff and that skipped settings remain drift. | 4, 5 | Include documentation changes in the same implementation flow. Do not edit external repos. |
| 7 | Add focused validation for preview, selective apply, and docs drift prevention | Completed | Editor tests cover generated diffs, selected apply, skipped-setting drift, web export blocker preview, menu/Start Screen routing, and no-change state; static docs checks or focused assertions prevent reintroducing exact manual build-setting lists. | 1, 2, 3, 4, 5, 6 | Prefer the open-Editor bridge against `<local-unity-host-project>` when available. |

## Execution Rules

- Execute tasks in order. Do not run implementation tasks in parallel.
- Use the `implement-tasks` skill PRD task table mode.
- Before editing, inspect `git status --short` and preserve unrelated dirty worktree changes.
- If this plan is executed before prior local WebGL setup changes are committed, work with those changes rather than reverting them.
- Do not edit the Gaming Couch main repo, GC SDK, GC Client, or GC DevApp without explicit user permission.
- Keep changes editor-only. Do not change public runtime payload shapes.
- Use generated/spec-driven copy for setting labels and values. Do not add a second manual list of build setting values.
- Use one shared preview/apply service for menu and Start Screen routes.
- Keep one-time skip behavior non-persistent.

## Validation Guidance

Use the package open-Editor bridge first. Resolve `<local-unity-host-project>` from `AGENTS.local.md`:

```bash
python3 Tools/run-open-unity-tests.py <local-unity-host-project> --mode EditMode
```

Focused validation should include:

- `GamingCouchActiveSceneSetupAssetTests` WebGL export/profile tests.
- `GamingCouchStartScreenReadinessTests`.
- `GamingCouchStartScreenSetupActionsTests` if Start Screen dispatch changes.
- Any new preview-window or preview-service tests added by the implementation.

If the bridge times out without a `started` status, ask the user to open or refresh the host Unity Editor so it loads `Editor/GamingCouchCodexTestBridge.cs`, then retry.

## Blocker Log

No blockers recorded at plan creation.
