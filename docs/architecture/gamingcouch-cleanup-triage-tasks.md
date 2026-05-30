# GamingCouch Cleanup Triage Tasks

Status: Ready for implementation
Last updated: 2026-05-21
Owner: Gaming Couch Unity package team

## Source Context

This plan turns the May 18, 2026 cleanup scan and follow-up grilling session into executable tasks.

Primary intent:

- Remove low-risk user-facing confusion before release.
- Keep compatibility-sensitive Runtime cleanup separate from simple cleanup.
- Treat NGO/WebGL transport findings as bug hardening, not cosmetic cleanup.
- Remove the unreachable generated scene path instead of exposing it as a supported product route.

Current user decisions:

- Use a local tracked task document, not GitHub issues.
- Execute the first task as low-risk UI/docs/package cleanup.
- Keep direct WebGL menu access, but rename it to preview-oriented wording.
- Do not change the README multiplayer WIP section in the first cleanup task.
- Use Apache-2.0 for this package, with `Gaming Couch` as the copyright holder.
- Track transport hardening, Runtime API deprecation, Active Scene Setup cleanup, and Contract Fixture hardening as separate follow-ups.

This file tracks implementation work only. Creating this plan does not implement production code.

## Status

Overall status: Task 1 and Task 4 completed

Current task: None

Next action: Stop here unless explicitly asked to continue with another pending task.

| ID | Task | Status | Done when | Dependencies | Notes |
| --- | --- | --- | --- | --- | --- |
| 1 | Low-risk user-facing cleanup | Completed | Misleading menu routes, stale WebGL menu naming, package URLs, Apache-2.0 license text, and obvious non-multiplayer README TODOs are cleaned up without changing Runtime public APIs or transport behavior. | None | Keep README multiplayer WIP section unchanged. Preserve Unity `.meta` hygiene when renaming files. Updated affected docs/tests where the renamed WebGL menu module is referenced. |
| 2 | NGO/WebGL transport hardening | Pending | Duplicate/stale `.jslib` bridge behavior, payload slicing, static server state, unused transport helpers, and missing transport tests are addressed through a focused bug-hardening slice. | None | Treat as behavior work. Validate with tests and WebGL build/smoke coverage where feasible. |
| 3 | Runtime API deprecation cleanup | Pending | Deprecated or internal-looking Runtime public surfaces are reviewed with compatibility rules, migration notes, and staged deprecation/removal decisions. | None | Candidates include `GCNameTag`, `GCPlayerStoreInput`, mutable store views, and public internal setup/dev methods. Do not casually delete serialized compatibility shims. |
| 4 | Active Scene Setup terminology cleanup | Completed | The unreachable generated scene route is removed, setup code is named for Active Scene Setup, and docs use Start Screen, Start Screen Readiness, Active Scene Setup, and Example Assets vocabulary. | None | Generated editable Example Assets stay available for Active Scene Setup. Legacy generated prefab replacement checks remain for compatibility. |
| 5 | Contract Fixture warning-only assertion | Pending | The missing-platform-data warning-only Contract Fixture asserts successful Capture behavior if that is still intended. | None | Small test-hardening task; do not bundle with UI cleanup. |

## Task 1 Details

Scope:

- Remove the asset-creation GamingCouch menu item because it mutates the active scene from an asset-creation menu.
- Keep the other GamingCouch object creation routes:
  - `GameObject/GamingCouch`
  - `GamingCouch/Create GamingCouch GameObject`
  - Start Screen Active Scene Setup actions
- Rename the stale WebGL build wrapper into a WebGL build menu module.
- Keep the direct WebGL menu routes, but rename labels so users know they open preview flows:
  - `GamingCouch/WebGL Build/Preview release build settings (slow build)`
  - `GamingCouch/WebGL Build/Preview dev build settings (fast build)`
  - `GamingCouch/WebGL Build/Preview clean WebGL export setup`
- Update `package.json` URLs:
  - `documentationUrl`: `https://deadsetbit.github.io/gaming-couch-unity/`
  - `changelogUrl`: `https://github.com/deadsetbit/gaming-couch-unity/blob/main/CHANGELOG.md`
  - `licensesUrl`: `https://github.com/deadsetbit/gaming-couch-unity/blob/main/LICENSE.md`
- Fill `LICENSE.md` with standard Apache-2.0 license text and copyright holder `Gaming Couch`.
- Remove obvious non-multiplayer public README TODO lines if they can be removed without expanding docs.

Out of scope:

- Do not alter Runtime public APIs, serialized compatibility fields, or HUD compatibility shims.
- Do not touch NGO/WebGL transport behavior.
- Do not change generated scene creation/exposure outside Task 4; Task 4 removes that route.
- Do not rewrite or remove the README online multiplayer WIP section.
- Do not edit the Gaming Couch main repo, GC SDK, GC Client, or GC DevApp without explicit permission.

## License Notes

Without a license, default copyright rules apply and users generally do not have explicit permission to use, modify, or distribute the code. This package is intended to be available for others to use, so Task 1 uses a permissive open-source license.

Apache-2.0 is chosen because it is permissive and includes an explicit patent grant, which is a good fit for SDK/platform integration code. Reference material:

- GitHub licensing docs: `https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/licensing-a-repository`
- Choose a License no-license guidance: `https://choosealicense.com/no-permission/`
- Apache-2.0 text: `https://www.apache.org/licenses/LICENSE-2.0.html`

## Execution Rules

- Execute tasks in order only when requested; start with Task 1.
- Before editing, inspect `git status --short` and preserve unrelated dirty worktree changes.
- Use the `implement-tasks` skill PRD task table mode if executing the full task workflow.
- Preserve Unity `.meta` files when renaming existing Unity-imported assets or scripts.
- Keep each task independently commit-ready; do not mix task scopes.
- If implementation discovers another file is required, update this plan with the reason before editing the extra file.

## Validation Guidance

For Task 1:

- Parse `package.json` as JSON and confirm no `example.com` URLs remain.
- Static search confirms the removed asset-creation route and stale wrapper name are gone.
- Static search confirms the new WebGL menu labels use preview wording.
- Static search confirms the README multiplayer WIP section is unchanged.
- Run focused editor tests for menu routing and WebGL preview behavior.

Use the package open-Editor bridge first. Resolve the host project from `AGENTS.local.md`:

```bash
python3 Tools/run-open-unity-tests.py /Users/anttil/dev/dsb/gaming-couch-unity-template --mode EditMode
```

If the bridge times out without a `started` status, ask the user to open or refresh the host Unity Editor so it loads `Editor/GamingCouchCodexTestBridge.cs`, then retry.

## Blocker Log

No blockers recorded at plan creation.

## Task 1 Completion Note

Completed on 2026-05-18.

Changed paths:

- `Documentation~/README.md`
- `Editor/GamingCouchMenuItems.cs`
- `Editor/GamingCouchWebGLBuildMenu.cs`
- `Editor/GamingCouchWebGLBuildMenu.cs.meta`
- deleted former `Editor/Utils` WebGL build wrapper script, script `.meta`, and folder `.meta`
- `LICENSE.md`
- `README.md`
- `Tests/Editor/GamingCouchStartScreenSetupActionsTests.cs`
- `docs/architecture/gamingcouch-clean-webgl-export-template-prd.md`
- `docs/architecture/gamingcouch-cleanup-triage-tasks.md`
- `docs/architecture/unity-dev-json-sync-prep-execution-tasks.md`
- `docs/architecture/unity-dev-json-sync-prep-refactor-roadmap.md`
- `docs/architecture/webgl-build-settings-preview-implementation-tasks.md`
- `package.json`

Validation:

- `git diff --check`
- `node -e "const fs=require('fs'); const pkg=JSON.parse(fs.readFileSync('package.json','utf8')); if ([pkg.documentationUrl,pkg.changelogUrl,pkg.licensesUrl].some(u => /example\\.com/.test(u))) process.exit(1); console.log(pkg.documentationUrl); console.log(pkg.changelogUrl); console.log(pkg.licensesUrl);"`
- stale route/name static search across `Editor`, `Tests`, `README.md`, `Documentation~`, `docs/architecture`, and `package.json`
- preview label static search across `Editor`, `README.md`, `Documentation~`, and `docs/architecture`
- `python3 Tools/run-open-unity-tests.py /Users/anttil/dev/dsb/gaming-couch-unity-template --mode EditMode --filter GamingCouchStartScreenSetupActionsTests`

Focused EditMode result: 14 passed, 0 failed, 0 skipped, 0 inconclusive.
