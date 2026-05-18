@file AGENTS.local.md

## Cross-repo path bridge

- Load `AGENTS.local.md` first before applying the rest of this file.
- The local bridge file is machine-specific and keeps local absolute paths out of tracked guidance.
- It may define:
  - Gaming Couch main repo base path
  - Local Unity host project path for open-Editor test bridge runs
- Relative mappings inside Gaming Couch main repo:
  - GC SDK: `sdk`
  - GC Client: `client`
  - GC DevApp: `devspace/devapp`
- Resolve absolute paths by joining base path from `AGENTS.local.md` with the relative mappings above.
- If a task needs GC SDK, GC Client, or GC DevApp paths and `AGENTS.local.md` is missing, invalid, or does not define base path, ask the user for the Gaming Couch main repo base path.
- After user provides the base path, create or update `AGENTS.local.md` on the fly (it is git-ignored).
- If any required local absolute path is missing from `AGENTS.local.md`, ask the user for that path.
- When the user provides a missing local path, offer to create or update `AGENTS.local.md` with it.
- Never use `AGENTS.local.example.md` as a live source of truth; it is only a template for humans.
- Always ask permission from the user if making changes to outside repos.
- Do not add machine-local absolute paths to this tracked file. Put them in `AGENTS.local.md`.

## Unity package test execution

- When validating changes to this Unity package, prefer the open-Editor test bridge over launching a second Unity process.
- Use the bridge runner from this package:
  - `python3 Tools/run-open-unity-tests.py /path/to/unity/project --mode EditMode`
  - Add `--test Full.Test.Name`, `--filter Regex`, or `--category Name` for focused runs.
  - Add `--mode PlayMode` for Play Mode tests.
- If `AGENTS.local.md` defines a local Unity host project path, use it when it exists and is the intended symlinked project.
- If no local Unity host project path is defined and validation needs one, ask the user for the local host Unity project path.
- If the runner times out without a `started` status, ask the user to open or refresh the host Unity Editor so it loads `Editor/GamingCouchCodexTestBridge.cs`, then retry.
- Fall back to Unity batchmode `-runTests` only when the open-Editor bridge is unavailable or the user explicitly asks for batchmode.
