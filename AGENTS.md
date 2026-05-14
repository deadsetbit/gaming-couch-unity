@file AGENTS.local.md

## Cross-repo path bridge

- Load `AGENTS.local.md` first before applying the rest of this file.
- The local bridge file is machine-specific and must define only the Gaming Couch main repo base path.
- Relative mappings inside Gaming Couch main repo:
  - GC SDK: `sdk`
  - GC Client: `client`
  - GC DevApp: `devspace/devapp`
- Resolve absolute paths by joining base path from `AGENTS.local.md` with the relative mappings above.
- If a task needs GC SDK, GC Client, or GC DevApp paths and `AGENTS.local.md` is missing, invalid, or does not define base path, ask the user for the Gaming Couch main repo base path.
- After user provides the base path, create or update `AGENTS.local.md` on the fly (it is git-ignored).
- Never use `AGENTS.local.example.md` as a live source of truth; it is only a template for humans.
- Always ask permission from the user if making changes to outside repos.

## Unity package test execution

- When validating changes to this Unity package, prefer the open-Editor test bridge over launching a second Unity process.
- Use the bridge runner from this package:
  - `python3 Tools/run-open-unity-tests.py /path/to/unity/project --mode EditMode`
  - Add `--test Full.Test.Name`, `--filter Regex`, or `--category Name` for focused runs.
  - Add `--mode PlayMode` for Play Mode tests.
- The known local host project for this workspace is `/Users/anttil/dev/dsb/gaming-couch-unity-template`; use it when it exists and is the intended symlinked project.
- If the runner times out without a `started` status, ask the user to open or refresh the host Unity Editor so it loads `Editor/GamingCouchCodexTestBridge.cs`, then retry.
- Fall back to Unity batchmode `-runTests` only when the open-Editor bridge is unavailable or the user explicitly asks for batchmode.
