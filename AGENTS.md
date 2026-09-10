@file AGENTS.local.md

## Cross-repo path bridge

- Load `AGENTS.local.md` first before applying the rest of this file.
- The local bridge file is machine-specific and keeps local absolute paths out of tracked guidance.
- It may define:
  - Gaming Couch main repo base path
  - Local Unity host project path for test runs
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

## Repository layout

- The UPM package lives in `public/package/`. Everything under that folder is published to
  `deadsetbit/gaming-couch-unity-public` on release; nothing else is. Read `public/AGENTS.md`
  before editing anything there — links and paths inside the package must resolve from the
  package root, not this one.
- Everything outside `public/package/` stays private: `docs/`, `Tools/`, `CONTEXT.md`,
  `AGENTS*.md`, `VERSIONING_PLAN.md`, and the root `package.json`, which is marked private and
  carries the release scripts only.
- The design is `docs/architecture/public-mirror-plan.md`.

## Runtime API compatibility

- The Unity package exposes runtime identity to the Gaming Couch platform.
- Keep WebGL and Editor/devapp registration payloads in sync.
- `packageVersion` identifies the package release for diagnostics.
- `gameProtocolVersion` identifies the platform integration contract for compatibility.
- When changing package/platform APIs, question whether `gameProtocolVersion` must be bumped. Treat the bump as a user decision: explain the compatibility risk and suggest practical alternatives that preserve legacy support without changing the protocol.

## Unity package test execution

- The package has no test tooling of its own. Validate changes with the Unity CLI (`unity`) against a host project that has this package installed.
- Take the host project path from `AGENTS.local.md`. If it defines none and validation needs one, ask the user for it.
- The host project reaches the package through an untracked `Packages/gaming-couch-unity` symlink, which is per-developer local state. It must point at this repository's `public/package` folder; a fresh clone or a new machine has to create it.
- `unity test <host-project> --mode EditMode` runs the EditMode suite; `--mode PlayMode` runs Play Mode. Write the report somewhere outside the repo, e.g. `--output "$TMPDIR/test-results.xml"` — the default (`test-results.xml`) lands in the working directory.
- `--filter` narrows a run: a semicolon-separated list of full test names or regexes, each optionally negated with `!`. There is no assembly filter; scope by name instead. The Editor tests declare no namespace, so a full name is just the fixture, e.g. `GCPlayerIndexMappingTests`.
- `unity test` prints nothing while it runs — no progress, no spinner. A first run against a cold project imports the whole project first and can take several minutes of complete silence; that is not a hang. Let it finish and read the exit code.
- Read the exit code, not the log: `0` all passed, `8` the run finished and reported failures, anything else (commonly `6`) means it never produced a verdict — a compile error, an unavailable license, an Editor crash, or `--timeout`. Under `--format json` the same split appears as `errors[0].code`.
- `unity test` spawns its own Editor in batch mode, so it cannot run while an Editor holds the host project's lock. Close that Editor or point the run at a separate host-project clone.
- Driving an already-open Editor is possible via the `com.unity.pipeline` package (`unity pipeline install`, then `unity command` / `unity list`, also exposed to agents through `unity mcp`), which round-trips in a warm session with no domain reload. The package ships no test-run command, so this is not a test path today. If warm test runs become worth it, register a `[CliCommand]` in the **host project** rather than here — a `[CliCommand]` in this package would force a `com.unity.pipeline` dependency on every consumer.
