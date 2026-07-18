---
name: migrate-gaming-couch
description: Migrate a Unity game from an older GamingCouch package to the current strict-identity runtime contract, fixing the CS0619/CS1061 compile errors the upgrade introduces (GCPlayer.Id→Index, GCNameTag→GCPlayerOverhead, SetEliminated→Permanent/Revokable, …). Use when a project's DSB.GC / GamingCouch code stops compiling after bumping the package, or the user asks to upgrade or migrate a Gaming Couch game.
---

# Migrate a Gaming Couch game to the current runtime contract

Upgrading the GamingCouch package removes the old platform-identity surface. The fixes are
mechanical but numerous, and **two of the five kinds silently change behaviour if you rename
blindly** — so this skill classifies every error before touching it.

One idea explains almost everything: the runtime is now keyed by **`Index`** (the per-round
0..N-1 seat index), never by the old platform `Id` or player name (ADR 0001, *strict runtime
identity boundary*). `Id` and names are gone; `Index` is the identity.

## The five fix classes

Read an error, decide which class it is, then fix it that way. The full symbol table lives in
[`migration-map.md`](migration-map.md) — consult it for every error; do not fix from memory.

- **Rename** — an identifier swap, meaning unchanged (`PlayersEnumerable` → `Players`). Safe.
- **Rewrite** — the *expression* reshapes, not just a name (`PlayerCount` → `Players.Count`;
  `UpdateScreenPointHud(data)` → a loop of `QueuePointData(point)`).
- **Reindex** — `Id` → `Index`. The **value** changes, not just the name. Renaming only the
  member while leaving the old `.Id` operand (also removed) ships a bug. Follow the whole data
  path: anything that stored, keyed, passed, or serialized an `Id` must move to `Index`.
- **Choose** — one old call maps to two new ones and picking is a **gameplay decision**
  (`SetEliminated` → permanent vs revokable). Infer from the surrounding logic; if genuinely
  ambiguous, ask the user rather than guess.
- **Rethink** — the member is gone with **no replacement** (`PlayerName`, `GCHud.UpdatePlayers`,
  the right stick). Cannot be mechanically fixed; each needs a real code change, so surface it.

Two compiler codes tell you how much help you get:

- **`CS0619 … is obsolete: '<guidance>'`** — the replacement is *in the message*. Every
  `[Obsolete]` marker is a hard error carrying its own migration hint.
- **`CS1061 … does not contain a definition for …`** — silently removed, **no** guidance. These
  (e.g. `InstantiatePlayers`, `GCNameTag.SetPlayerId`, `rightX`) are findable only via the map.

## Workflow

1. **Start clean, then collect.** Before editing anything, gate on two preconditions: (a) the
   game's git working tree is clean — no uncommitted changes **and** no untracked files
   (`git status --porcelain` returns nothing); and (b) the package is on the target version. A
   clean baseline is what makes the migration's diff reviewable and lets a bad run be reverted
   wholesale — decisive when batching many games. If the tree is dirty, **stop and ask the user to
   commit or stash first**; never fold their pending work into the migration or commit it yourself.
   Then gather **every** compile error into one worklist — clearing errors piecemeal hides the
   shape of the job and re-triggers recompiles (see [Getting the errors](#getting-the-errors)).
   *Done when:* the tree is clean (or the user explicitly waived it), the package version is
   confirmed, and you hold the complete error list.
2. **Classify.** Tag each error with its class using `migration-map.md`.
   *Done when:* every error maps to a row and a class (no "unknown" left).
3. **Apply the mechanical + semantic fixes** — Rename, Rewrite, Reindex. For each **Reindex**,
   re-key the whole data path, not just the call site.
4. **Resolve Choose** items: read the surrounding logic to pick permanent vs revokable (does the
   game let the player come back? then revokable). Ask only when the code gives no signal.
5. **Surface Rethink** items: list each to the user with a concrete recommendation. Never silently
   drop the behaviour the removed member provided.
6. **Recompile and loop 2–5** until **zero** errors — clearing one layer routinely reveals errors
   the compiler could not reach before.
7. **Fix asset component references.** For renamed `MonoBehaviour`s (`GCNameTag` →
   `GCPlayerOverhead`), swap the component in every `.prefab`/`.unity` that used it — a code-only
   rename leaves a "missing script". Grep assets for the old type name.
8. **Verify.** Drive the game (or run the project's EditMode/PlayMode tests) and eyeball every
   **Choose** and **Reindex** site — those are where a green compile can still hide wrong runtime
   behaviour. See [Reference implementation](#reference-implementation).
9. **Feed back what you learned.** A real migration almost always teaches this skill something the
   map lacked: a `CS1061` removal you had to reverse-engineer, a Choose call the surrounding code
   made obvious (a reusable heuristic), an asset type that broke, a wording that failed to match a
   real error, or a map row that was simply wrong. Before finishing, **always** review the run for
   such lessons and propose concrete edits to `migration-map.md` / `SKILL.md` (new or corrected
   rows, a sharpened heuristic). Propose — don't silently self-edit — the map mirrors the package's
   `[Obsolete]` markers and edits get reviewed; then offer to apply them. If the run genuinely
   surfaced nothing new, say so in one line rather than inventing a change.

**Migration is complete when:** the project compiles with zero errors, every Choose item was
decided (with reasoning), every Rethink item was surfaced to the user with a recommendation,
renamed-component asset references were swapped, and you have reported what (if anything) this run
taught the skill.

## Getting the errors

Fastest and authoritative: have the user **paste the Unity Console errors**, or read them from the
project's `Logs/` or the editor log. The editor-log location is per-platform:

- macOS: `~/Library/Logs/Unity/Editor.log`
- Windows: `%LOCALAPPDATA%\Unity\Editor\Editor.log`
- Linux: `~/.config/unity3d/Editor.log`

Autonomous fallback — force a headless compile and grep for the errors. Point `<unity-editor>` at
the Unity binary for the project's version (macOS `…/Unity.app/Contents/MacOS/Unity`, Windows
`…\Editor\Unity.exe`, Linux `…/Editor/Unity`, typically under the Unity Hub install root):

```bash
"<unity-editor>" \
  -batchmode -quit -projectPath "<project>" -logFile - 2>&1 | grep -E "error CS[0-9]+"
```

Re-run this (or ask for a fresh paste) after each edit pass to drive the step-6 loop.

## Reference implementation

If the package ships `Tests/ExampleCanonical/` (`GCExampleGameSource`, `GCExamplePlayerSource`),
it is compile-guaranteed against the current API (ADR 0017) — the authoritative **"after"** for
`SetupPlayers`, `GetInputsByPlayerIndex`, the permanent/revokable state calls,
`isPlayersAutoUpdateEnabled`, and `QueuePointData`. Match its patterns when a fix is non-obvious.
