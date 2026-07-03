# Standalone Controller Branch Review — Remediation Tasks

Status: Ready for implementation
Last updated: 2026-07-01
Owner: Gaming Couch Unity package team

## Source Context

This plan turns the `feature/standalone-controller` vs `main` branch review (6 parallel
review passes, ~35k lines / 107 code files) into executable tasks. Every finding below
was independently re-verified against the current code before being written here — the
per-task **Verified** note records that check, including three places where the original
review was imprecise (see Task 7, Task 11, Task 12, and Decision D1).

Primary intent:

- Fix the one merge-blocking bug (Codex `--sync` false timeout) first.
- Treat build-failure blast-radius (postprocess throw) and dirty-draft data loss (false
  conflict) as bug hardening, above cosmetic items.
- Drive each fix with **red/green TDD**: write a failing test that reproduces the defect
  (RED), then make the minimal change that turns it green (GREEN). Where the current code
  has no test seam, adding that seam is an explicit sub-step, not an afterthought.
- Keep the one product decision (WebGL compression) out of the "fix" queue until decided.
- Keep confirmed-deliberate changes as confirm-only, no code.

This file tracks implementation work only. Creating this plan does not implement production code.

## How To Use This File (for a fresh agent chat)

1. Work top-down by priority. Do **not** start a task whose dependencies are unmet.
2. For each task follow the red/green loop and tick the checklist:
   - **RED** — write the test(s) in the named test file first; run them; confirm they FAIL
     for the stated reason (not a compile error, not an unrelated failure).
   - **GREEN** — make the minimal production change; re-run; confirm the RED test now passes.
   - **Regression** — run the surrounding suite (at minimum the named test assembly) green.
   - **Review** — re-read the diff against the "Done when" and edge-case notes.
3. Update the task's **Status** in the tracker table and check its boxes as you go.
4. Editor/Unity tests live under `Tests/Editor/`. Runtime-only pure C# is unit-testable
   there too. The Codex bridge end-to-end path needs a live Editor + the Python harness.
5. Preserve Unity `.meta` hygiene for any new/renamed files.
6. Do not bundle unrelated tasks into one commit; one task = one focused change + its tests.

### Status legend

- **Not started** — no work yet.
- **RED** — failing test written and confirmed failing.
- **GREEN** — fix implemented, RED test passes.
- **Verified** — regression suite green + diff reviewed.
- **Blocked** — waiting on a decision or dependency.

## Status

Overall status: Not started — verification complete, awaiting implementation.

Current task: Task 1 — fix + tests applied; awaiting a live-Editor/CI run to close GREEN/Regression/Review.

Decision D1 (WebGL compression) is **decided**: no compression by design — see **Decision D1**.

| ID | Task | Priority | Verified? | Status | Done when | Deps |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | Codex `--sync` run reports false timeout | P0 (High, blocks merge) | ✅ Confirmed | Fix applied — live-Editor/CI run pending | A synchronously-completed EditMode run reports its true terminal status; Python harness exits 0 on success. | None |
| 2 | Postprocess build sidecar throw fails a completed build | P1 (Low prob / High blast) | ✅ Confirmed | Fix+tests applied — live run pending | A package-identity/sidecar failure in `OnPostprocessBuild` logs an error instead of turning a completed build into "build failed". | None |
| 3 | Transient read error trips a false dirty-draft conflict | P1 (Medium) | ✅ Confirmed | Fix+tests applied — live run pending | A momentary file read error no longer reports a "change" / pushes a dirty draft into conflict; real changes and deletions still detected. | None |
| 4 | `GetPlayerByIndex` list-position fallback | P1 (Medium, latent) | ✅ Confirmed | Fix+tests applied — live run pending | A dictionary miss yields a clear index-keyed error (or documented null), never a wrong-player-by-list-position. | None |
| 5 | Build-info `outputPath` silently downgrades to relative | P1 (Medium, latent) | ✅ Confirmed | Fix+tests applied — live run pending | A relative `outputPath` is canonicalized before classification so it maps to `buildOutputRelative`/`"."` and the redaction guarantee holds. | None |
| 6 | Game View "Select 16:9" misreports `changed` | P2 (Medium, cosmetic) | ✅ Confirmed | Fix+tests applied — live run pending | `changed` reflects the window's actual pre-set selection, not the stale `-1`. | None |
| 7 | `GCPlayerIndexMapping` silently overwrites duplicate source seat | P2 (latent) | ⚠️ Partial* | Fix+tests applied — live run pending | A duplicate `sourceSeatIndex` is rejected/diagnosed rather than silently last-wins. | None |
| 8 | Codex bridge request/output files never cleaned up | P2 (Low) | ✅ Confirmed | Not started | Handled request files and stale outputs are bounded (deleted or retention-swept) per session. | None |
| 9 | Empty `screen_space` envelope emitted every frame | P3 (Low) | ✅ Confirmed | Not started | No per-frame emit / implicit active-run start when the screen-space queue is empty. | None |
| 10 | `IsValidPlayerName` trim asymmetry | P3 (Low) | ✅ Confirmed | Not started | Min and max length use the same (trimmed) measure. | None |
| 11 | Culture-sensitive seed parse | P3 (Low) | ⚠️ Partial* | Not started | Seed parse uses `NumberStyles.None` + `InvariantCulture` to match the sibling parser; other culture-sensitive numeric parses audited. | None |
| 12 | Unobserved faulted `SendAsync` exception | P3 (Low) | ⚠️ Partial* | Not started | The faulted send Task's `Exception` is observed (read/logged), not just its `IsFaulted` flag. | None |
| 13 | Dead duplicate `WebSocket*` DTO block | P3 (Cleanup) | ✅ Confirmed | Not started | The unused `WebSocket*` message classes are removed; live path unaffected. | None |
| 14 | `ColorHex` always null | P3 (Cleanup) | ✅ Confirmed | Dead code removed — ⚠ public-API removal, owner-confirm | `ColorHex` is either wired to the resolved color or removed (with public-API check). | None |
| D1 | Release profile forces WebGL compression `Disabled` | Decision → Docs (P3) | ⚠️ Deliberate* | Decided | **Decided 2026-07-02: no compression by design (for now).** Optional: add a code comment + doc note so it isn't mistaken for a bug. | None |
| C1 | NGO elimination/finish sync is lossy | Confirm-only | n/a | Not started | Confirmed intentional/temporary; no code change. | None |
| C2 | DPad / right-stick removed from `leftX/Y` | Confirm-only | n/a | Not started | Confirmed intentional (matches "remove unused input fields" commits). | None |

\* **Partial / Deliberate** = the underlying code behavior is real, but the original review's
framing was imprecise. See the task detail for the correction.

---

## Task 1 — Codex `--sync` run reports false timeout (P0, blocks merge)

**Files:** `Editor/GamingCouchCodexTestBridge.cs:224-228`, `:304-331`, `:351-355`;
`Tools/run-open-unity-tests.py:297-316`.

**Verified:** ✅ Confirmed. For EditMode with `runSynchronously` (`:250-253`),
`activeTestRunnerApi.Execute(settings)` (`:224`) runs synchronously — Unity guarantees all
callbacks fire before it returns. So `RunFinished → CompleteRun` writes the terminal
`completed`/`failed` status (`:318-319`) and `CleanupActiveRun` nulls `activeCallbacks`
(`:351-355`), all *inside* the `:224` call. Line `:227` then unconditionally overwrites the
status file back to `Started`. Python `wait_for_completion` only sees `state == "started"`,
loops to `--timeout` (default 300s) and returns exit code 2 — a false timeout even on success.
**Secondary bug found:** `SetJobId` runs at `:225` *after* `Execute`, so a synchronous
`CompleteRun` records `JobId == null` in the terminal status (cosmetic).

**Fix (GREEN):** Prefer writing the `Started` status **before** `Execute` (move the `:227`
write to before `:224`) so a synchronous terminal write naturally lands last and is never
clobbered — this also lets the job id be filled by the callback and covers future refactors.
Minimal alternative: guard the post-`Execute` write with `if (activeCallbacks != null) WriteStatus(...)`.
Both fix the reported bug; the "write-before-Execute" form is more robust.

**Red/green testing:**

- **RED (pure-Python, fast, no Editor) — lock in the harness contract:** add a test for
  `wait_for_completion` pointing at a temp status file.
  - `state:"completed"` → returns 0; `state:"failed"` → returns 1;
  - file left at `state:"started"` with a short timeout → returns 2 and prints "timeout".
  This documents the failure mode but does not exercise the C# clobber.
- **RED (in-editor unit, recommended) — the actual ordering bug:** the bug is a private-static
  ordering issue in `StartRun`. **Prerequisite seam:** extract the status-write side effect
  behind an injectable sink and allow simulating "a synchronous `Execute` that fires
  `RunFinished` (writing terminal status + nulling `activeCallbacks`) before returning".
  Assert the **last** status written is `completed`, not `started`.
  - RED: last write is `started`. GREEN: last write is `completed`.
- **RED (end-to-end, needs live Editor):** run
  `run-open-unity-tests.py <project> --mode EditMode --sync --timeout 30` against passing tests.
  - RED: exits 2 "timeout: no completed Unity test status", status file stuck at `started`.
  - GREEN: exits 0, status file `state:"completed"`.

**Checklist:** ☑ RED written (Python contract passing; C# ordering test authored) ☑ Seam added
(`RunAndTrackStatus`) ◐ GREEN (Python verified; C# EditMode test pending live-Editor/CI run)
☐ Regression ☐ Review

**Progress (2026-07-03):** Fix applied in `Editor/GamingCouchCodexTestBridge.cs` — `"started"` is
now written *before* `Execute`, and the post-`Execute` job-id + `"started"` write is guarded by
`activeCallbacks != null`, so a synchronous run's terminal status is never clobbered (this also
removes the secondary `SetJobId`-after-cleanup NRE). Ordering extracted behind a seam
`RunAndTrackStatus(request, execute, writeStatus, isRunActive, setJobId)`; `CodexTestRequest` and
`CodexTestStatus` widened to `internal` (already exposed to `GamingCouch.Editor.Tests` via
`InternalsVisibleTo`). New `Tests/Editor/GamingCouchCodexTestBridgeStatusOrderingTests.cs` asserts
sync → last write `"completed"`, async → last write `"started"` + job id recorded. New
`Tools/test_run_open_unity_tests.py` (6 tests, **passing locally**) pins the harness exit-code
contract (0/1/2). The C# tests are **not yet executed** here — this project has no local `Library/`,
so a batchmode run would need a full first-time import. To close the last three boxes, run the
EditMode suite in the Editor/CI and the end-to-end
`Tools/run-open-unity-tests.py <project> --mode EditMode --sync --timeout 30` against passing tests
(expect exit 0, status file `state:"completed"`).

**Edge cases:** async EditMode/PlayMode runs must still write `Started` (there `activeCallbacks`
is live / `Execute` returns before completion) — the guarded fix preserves this (async writes
`Started` before Execute and again with the job id after). The `jobId==null`-in-sync-status symptom
is left as-is (cosmetic): a synchronous run's job id is unknown until Execute returns, by which
point the terminal status is already written.

---

## Task 2 — Postprocess build sidecar throw fails a completed build (P1)

**Files:** `Editor/GamingCouchWebGLRuntimeInfoSidecar.cs:263-266` (`OnPostprocessBuild`), `:288`
(`WriteForBuild → GCEditorPackageIdentity.Resolve()`); `Runtime/GCEditorPackageIdentity.cs`
(throws `InvalidOperationException` at `:59/68/90/95/116/135`).

**Verified:** ✅ Confirmed. The single `IPostprocessBuildWithReport.OnPostprocessBuild` calls
`GCWebGLBuildSidecarPostprocessWriter.WriteForBuild(...)` with no try/catch; that calls
`GCEditorPackageIdentity.Resolve()` (also unwrapped), which throws on any package-metadata
hiccup (missing/invalid manifest, missing name/version). An exception out of `OnPostprocessBuild`
turns an otherwise-completed build into a Unity "build failed". Low likelihood, high blast
radius — sidecar emission is post-completion metadata, not build-critical. (Note:
`GCUnityBuildInfoSidecar.cs:72` also calls `Resolve()` unwrapped but has no non-test caller,
so the live risk is the WebGL sidecar path only.)

**Fix (GREEN):** Wrap the sidecar write (`Resolve()` + emission) in the `report`-based
`OnPostprocessBuild` entry point in try/catch; on failure `Debug.LogError`/`LogWarning`
with context and return without rethrowing.

**Red/green testing:** `Tests/Editor/GCEditorPackageIdentityTests.cs` already proves `Resolve*`
throws on bad manifests. Add an EditMode test that the postprocess writer **swallows** a thrown
identity resolution and does not propagate.
- **Prerequisite seam:** make the throw injectable at the `report`-based entry point (the
  internal `WriteForBuild(BuildTarget, ..., packageIdentity, ...)` overload already takes
  identity as a param; the swallow must be added at the entry point that resolves it).
- RED: test asserts "no exception propagates" — fails today (throws). GREEN: passes; assert an
  error was logged.

**Checklist:** ☑ RED written ☑ Seam added ◐ GREEN (implemented; independent verifier GREEN — live-Editor/CI run pending) ☐ Regression ☐ Review

**Progress (2026-07-03):** Two-layer guard in the report-based postprocess entry (`GCWebGLBuildSidecarPostprocessWriter.WriteForBuild(BuildReport,...)`). Inner seam: a new injectable-resolver overload `WriteForBuild(BuildTarget, ..., Func<GCPackageIdentity> resolvePackageIdentity, ...)` wraps `resolvePackageIdentity()` + the write in a try/catch that `Debug.LogError`s and does not rethrow — this covers the reported P1 (`GCEditorPackageIdentity.Resolve` throwing on bad package metadata) and is unit-testable in isolation. Outer guard: after independent verification found `ResolveOutputRootPath` + the two `Capture(...)` evaluations ran *before* the seam and could still throw out of the callback, the whole post-gate sidecar body was additionally wrapped in a try/catch that logs+returns. Net: no sidecar work can fail an otherwise-completed build (only a `null` report — a programmer-error precondition — still surfaces). New `Tests/Editor/GCWebGLBuildSidecarPostprocessWriterTests.cs` drives the seam with a throwing resolver and asserts `DoesNotThrow` + the logged error + no file written. Nothing widened to public; no asmdef/InternalsVisibleTo edits. Authored red/green; not executed here (no local `Library/`).

---

## Task 3 — Transient read error trips a false dirty-draft conflict (P1)

**Files:** `Editor/GCRootJsonFileStamp.cs:59-69` (error stamp), `:72-80` (`IsSameAs`);
`Editor/GCDevJsonInspectorState.cs:107-174` (0.25s poller), `:379-384` (`EnterConflict`).

**Verified:** ✅ Confirmed (worse than a one-poll blip). On a read failure the stamp is built
with `exists=true, length=-1, contentHash=null, readError="<ex>"`. `IsSameAs` compares all six
fields incl. `readError`, so a healthy stamp is never equal to an error stamp → `devChanged=true`.
In edit mode a dirty draft (`IsDirty`) then hits `EnterConflict()` (`:163-166`), setting
`hasConflict=true`, and the state **sticks** until reload/write. A genuine file *deletion* is a
distinct `exists=false, readError=null` stamp (`:44-47`), so it is not masked by the fix.

**Fix (GREEN):** Treat a read-error stamp as "unchanged/unknown". Cleanest per verification:
put the policy in the **poller** — skip change handling when `nextStamp` has a non-null
`readError` (retain the last-known-good baseline; optionally soft-warn/retry) — keeping
`IsSameAs` a pure value-equality method. Equivalent minimal form: short-circuit at the top of
`IsSameAs` when either side has `readError != null`. Do **not** overwrite the baseline stamp
with the error stamp (verify both dev `:147` and platform-data `:136` paths retain baseline).
The optional length+mtime pre-check before SHA-256 is a separate perf change — do not bundle.

**Red/green testing:**

- **RED (unit on `IsSameAs`, fastest):** build a healthy stamp and an error stamp for the same
  path; assert `healthy.IsSameAs(errorStamp) == true` (and reverse). Also assert two healthy
  stamps with different `contentHash`/`length` still report changed (guard against
  over-broadening), and that a real deletion (`exists=false`) still reports changed.
  - RED: today all six fields differ → `IsSameAs` false. GREEN: true.
  - **Prerequisite:** an internal test factory to build an error stamp deterministically
    (tests already see internals via the editor test assembly).
- **RED (integration on the poller):** load a temp `gc.dev.json`, mutate the draft so
  `IsDirty` is true (edit mode), force the next `ReadFileStamp()` into the catch branch, then
  `PollForExternalChanges(true)`; assert `state.HasConflict == false`.
  - RED: enters conflict today. GREEN: no false conflict; release + re-poll still clean.
  - **Prerequisite seam:** stores are `new`'d directly in the ctor (`:26-28`) with no seam —
    add a constructor overload taking the stores/reader so the failure can be injected
    deterministically (OS file-locking is not portable enough to rely on).

**Checklist:** ☑ RED (unit) ☑ RED (integration) ☑ Seam(s) added ◐ GREEN (implemented; independent verifier GREEN — live-Editor/CI run pending) ☐ Regression ☐ Review

**Progress (2026-07-03):** Chose the poller-policy placement (task's preferred option): added a pure predicate `GCRootJsonFileStamp.IsExternalChangeFrom(previous)` that returns `false` when the *new* stamp carries `readError != null`, else `!IsSameAs` — `IsSameAs` stays untouched/pure. The poller (`GCDevJsonInspectorState`) now computes `devChanged`/`platformDataChanged` via `IsExternalChangeFrom`, so a transient read error yields no change → the `if (devChanged)`/`if (platformDataChanged)` blocks are skipped and the last-known-good baseline is retained on **both** paths (never overwritten by the error stamp). Genuine deletion (`exists=false, readError=null`) still reports changed. Seams: an internal error-stamp test factory + a store/reader-injecting ctor overload (default ctor wires readers to `store.ReadFileStamp`, so production is unchanged — sole caller `GamingCouchEditor.cs` uses the parameterless ctor). New `Tests/Editor/GCDevJsonInspectorReadErrorConflictTests.cs` (unit: error→no-change, IsSameAs still differs, different-content changed, deletion changed, recovery changed; integration: dirty draft + forced error read → no conflict, then recovers clean). Verifier GREEN by claims-vs-code; not executed here (no local `Library/`).

---

## Task 4 — `GetPlayerByIndex` list-position fallback (P1, latent)

**Files:** `Runtime/GCPlayerStore.cs:100-108` (fallback at `:107` — `return players[playerIndex];`);
interface contract `Runtime/IGCPlayerStore.cs:44`. Callers: `Runtime/GamingCouch.cs:631`, `:1080`
(both pass a real player `.Index`, so both hit the dictionary today).

**Verified:** ✅ Confirmed. On a `playerByIndex` dictionary miss it does `players[playerIndex]`,
indexing the insertion-ordered `List<T>` by index — returning the wrong player if in range, or
throwing a misleading `ArgumentOutOfRangeException` if not. Latent because `AddPlayer` keeps both
collections in lockstep and rejects duplicate indices.

**Fix (GREEN):** Drop the list-position fallback. On a dictionary miss, either throw a clear
index-keyed error (e.g. `KeyNotFoundException`/`ArgumentException` naming `playerIndex`) or return
a documented null — never a wrong list-position player. Caller check: both callers pass a real
player `.Index` so both hit the dictionary today; `:631` does not use the fetched player (it logs
`playerIndex`), and `:1080` already null-checks (`if (player == null) return;`). A documented null
therefore stays compatible with both; if you throw instead, update `:1080` so its graceful
early-return is not turned into an exception. Document the not-found semantics on `IGCPlayerStore.cs:44`.

**Red/green testing:**

- **RED:** add players with an index **gap** (e.g. index 0 and index 2 → list positions 0 and 1),
  then `GetPlayerByIndex(1)`. Today it silently returns the index-2 player (list slot 1) — assert
  it instead throws a clear index-keyed error / returns the documented null. Also assert
  `GetPlayerByIndex(99)` gives the same clear error, not a list `ArgumentOutOfRangeException`.
  - RED: silent wrong player today. GREEN: clear error.
- **Testability note:** `GCPlayerStore<T>` is plain C#, but `AddPlayer` may need a Unity test
  context for `GCPlayer` construction; existing tests live under `Tests/Editor/`.

**Checklist:** ☑ RED authored ◐ GREEN (dict-miss → documented null; independent verifier GREEN — live-Editor/CI run pending) ☑ Interface doc updated ☐ Regression ☐ Review

**Progress (2026-07-03):** `GCPlayerStore.GetPlayerByIndex` now returns a documented null on a `playerByIndex` miss instead of the wrong list-position player (removed `return players[playerIndex];`); not-found=null documented on `IGCPlayerStore` + the method. Both callers confirmed null-safe (`GamingCouch.cs:631` unused local; `:1080` null-guarded). New `Tests/Editor/GCPlayerStoreLookupTests.cs` (present-index, index-gap→null, index-99→null; reuses the `GCPlayerStateModelTests` player factory). Not executed here (no local `Library/`); independent verifier GREEN by claims-vs-code. Close Regression/Review with the EditMode suite on live Editor/CI.

---

## Task 5 — Build-info `outputPath` silently downgrades to relative (P1, latent)

**Files:** `Editor/GCUnityBuildInfoPathNormalizer.cs:78` (input only separator-normalized,
never `Path.GetFullPath`ed), `:109/118-122/139/177-180`; call site
`Editor/GCUnityBuildInfoSidecar.cs:364` (`Normalize(outputPath, ...)`, `outputPath = summary.outputPath`).
Comparison roots are absolute (`GamingCouchWebGLRuntimeInfoSidecar.cs:48` via `Path.GetFullPath`;
`GCUnityBuildInfoSidecar.cs:393-394`).

**Verified:** ✅ Confirmed. A relative `summary.outputPath` fails `IsAbsolutePath`, never matches
any absolute root (`TryNormalizeUnderRoot` bails at `:139`), and falls through to the `Relative`
branch emitting the **raw value** (`outputPathKind="relative"`, `outputPathRedacted=false`) instead
of `buildOutputRelative`/`"."`. Not a crash; Unity normally returns absolute paths, so latent —
but it weakens the redaction guarantee.

**Fix (GREEN):** Canonicalize the input through `Path.GetFullPath` inside `Normalize` before
classification (roots are already full). **Keep** the existing manual separator/drive handling
for cross-platform determinism. Wrap `Path.GetFullPath` in try/catch (it throws on malformed
input — mirror `ResolveOutputRootPath`'s try/catch at `:50-61`); on failure fall back to current
`Relative`/`UnknownAbsolute` handling. Note `Path.GetFullPath` resolves relatively to CWD
(project root for a relative build path) — make that intentional.

**Red/green testing:** `GCUnityBuildInfoPathNormalizer.Normalize` is a pure static taking an
explicit context (no Unity dep); tests exist in `Tests/Editor/`.
- **RED:** context with absolute `buildOutputRootPath` (e.g. `/build/out`) + a relative input that
  resolves under it; assert `kind == BuildOutputRelative` (or `"."` when equal).
  - RED: returns `Relative` + raw value today. GREEN: matches root.
- **RED (redaction):** a relative path resolving under *no* known root is handled consistently
  (redacted / not leaking a raw absolute segment).
- **RED (guard):** a malformed input string does not throw out of `Normalize`.

**Checklist:** ☑ RED (under-root) ☑ RED (redaction) ☑ RED (malformed guard) ◐ GREEN (implemented; independent verifier GREEN — live-Editor/CI run pending) ☐ Regression ☐ Review

**Progress (2026-07-03):** `GCUnityBuildInfoPathNormalizer.Normalize` now `Path.GetFullPath`-canonicalizes the input **before** classification, but only when `!IsAbsolutePath(input)` (already-absolute Win/Unix paths keep the manual separator/drive handling), wrapped in a try/catch mirroring `ResolveOutputRootPath`'s exception set with graceful `Relative`/`UnknownAbsolute` fallback; CWD-anchoring is commented as intentional. New `Tests/Editor/GCUnityBuildInfoPathNormalizerRelativePathTests.cs` (under-root→`BuildOutputRelative`, equal-root→`"."`, outside-all-roots→redacted, malformed→no-throw; all CWD-independent). One existing sub-case in `GCUnityBuildInfoSidecarWriterTests.cs` that pinned the old raw-relative leak was updated to the corrected `UnknownAbsolute`/redacted expectation (verifier confirmed only that hunk changed, coverage not weakened). Not executed here (no local `Library/`); verifier GREEN by claims-vs-code.

---

## Task 6 — Game View "Select 16:9" misreports `changed` (P2, cosmetic)

**Files:** `Editor/GamingCouchGameViewAspect.cs:172-231` (`SelectExisting16By9Size`), `changed`
computed at `:227` from stale `context.selectedSizeIndex`; context read with
`createGameViewWindow:false` at `:176`; default `-1` at `:339`. Callers use `result.changed` for
messaging only: `Editor/GamingCouchStartScreenSetupActions.cs:142`,
`Editor/GamingCouchActiveSceneSetup.cs:1073`.

**Verified:** ✅ Confirmed. When no Game View is open, `selectedSizeIndex` stays `-1`; after the
method opens a fresh window (`:198-202`) and sets the target (`:214`), `changed` is computed from
the stale `-1 != targetEntry.index` (always true) — so it reports `changed=true` even if the new
window already defaulted to 16:9. Log/result wording only; the mutation itself is correct.

**Fix (GREEN):** Re-read the selected index from the actually-used window immediately before
`SetValue`, and compute `changed` from that fresh pre-set value. Wrap the reflected getter read
in try/catch like `:342-350`; on read failure degrade conservatively (leave `changed=true`).
Applying the re-read to both the already-open and freshly-opened branches is simplest and correct.

**Red/green testing:** Low testability as-is — the flow is reflection over `UnityEditor.GameView`
internals with no injection seam.
- **Prerequisite refactor:** extract the decision into a pure helper, e.g.
  `bool ComputeChanged(int currentSelectedIndex, int targetIndex)`, and separate "read current
  index from window" from "decide changed".
- **RED:** `ComputeChanged(preSetIndex: targetEntry.index, targetEntry.index)` must be `false`;
  the current inline logic effectively hardcodes the stale `-1`.
  - RED: `true` today. GREEN: `false` when the window already shows 16:9.

**Checklist:** ☑ Helper extracted ☑ RED (unit on `ComputeChanged`) ◐ GREEN (implemented; independent verifier GREEN — live-Editor/CI run pending) ☐ Regression ☐ Review

**Progress (2026-07-03):** Extracted a pure `internal static bool ComputeChanged(int currentSelectedIndex, int targetIndex)` and a `ReadSelectedSizeIndex(window, prop)` that re-reads the selected index via reflection inside try/catch (degrades to `-1` → conservative `changed=true`; target indices are always ≥0). `SelectExisting16By9Size` now computes `changed` from a **fresh** re-read of the actually-used `gameViewWindow` taken immediately **before** `SetValue`, in both the already-open and freshly-opened branches — the stale `context.selectedSizeIndex` (`-1` when no window was open) is no longer used. Callers use `changed` for messaging only (verified). Only `ComputeChanged` is unit-testable (reflection flow has no seam), so the new `Tests/Editor/GamingCouchGameViewAspectComputeChangedTests.cs` characterizes it; the re-read wiring is verified by review. Not executed here (no local `Library/`); verifier GREEN.

---

## Task 7 — `GCPlayerIndexMapping` silently overwrites duplicate source seat (P2, latent)

**Files:** `Runtime/GCPlayerIndexMapping.cs:25-32` (indexer assignment at `:30`);
reader `TryGetPlayerIndexForSourceSeat` `:170-173`; factories `Create` `:93` /
`CreateFromProvidedPlayerIndices` `:135`; the `CreateFromProvidedPlayerIndices` duplicate-*playerIndex*
guard that already throws at `:111-114`.

**Verified:** ⚠️ **Partial — the review's framing is wrong; the behavior is real.** The original
review said "currently unreachable, no callers". In fact the private ctor is reached on **every**
`Create` / `CreateFromProvidedPlayerIndices` call, and `Create` has production callers
(`Runtime/GCActiveRunProjection.cs:43`, stored/used at `Runtime/GamingCouch.cs:57,898`) plus many
tests. Line `:30` uses `dict[key]=value`, so a duplicate `sourceSeatIndex > 0` is silently
last-wins. What is **actually** latent: the *reader* `TryGetPlayerIndexForSourceSeat` has **no
production-reachable caller** — the only production reference is the `GamingCouch.cs:892-895`
wrapper, which is itself uncalled; only tests exercise the reader, directly on the mapping — so
the wrong overwrite has no observable downstream effect *today*. (Also note: entries
with `sourceSeatIndex <= 0` are silently dropped by the `> 0` guard — clarify whether seat 0 is
valid.)

**Fix (GREEN):** First confirm the invariant (can upstream seat-identity generation ever produce a
duplicate `sourceSeatIndex`?). If duplicates are illegal, reject them in the factory — throw
`ArgumentException` (consistent with the existing duplicate-`playerIndex` guard at `:111-114`) or
emit a diagnostic via the existing `EmitInvalidPlayerIndex`/`GCDiagnostics` machinery (`:243-260`).
Decide the `sourceSeatIndex <= 0` policy explicitly.

**Red/green testing:** Testable via the public `Create` factory (pure C#; tests exist in
`Tests/Editor/GCPlayerIndexMappingTests.cs`).
- **RED:** seat identities where two seats share `sourceSeatIndex > 0`; call `Create`; assert it
  throws/diagnoses. Today `Create` silently succeeds and `TryGetPlayerIndexForSourceSeat(dup)`
  returns only the last-written value.
  - RED: silent success today. GREEN: throws/diagnoses.
- Optional characterization test pins the current last-wins behavior; note this test would be the
  first real consumer of `TryGetPlayerIndexForSourceSeat`.

**Checklist:** ☑ Invariant confirmed ☑ RED written ◐ GREEN (implemented; independent verifier GREEN — live-Editor/CI run pending) ☑ seat-0 policy decided ☐ Regression ☐ Review

**Progress (2026-07-03):** Added a `ContainsKey` guard in the private ctor (the choke point both `Create` and `CreateFromProvidedPlayerIndices` flow through) that throws `ArgumentException` before the `dict[key]=value` overwrite, matching the existing duplicate-`playerIndex` guard style. Invariant confirmed by tracing every production `sourceSeatIndex` writer (`GamingCouchEditor.cs` = distinct `seatIndex+1`; `GCActiveRunProjection.cs` = distinct `index+1` or `0`) — no legitimate path produces a duplicate `>0`, so a duplicate is a true invariant violation. seat-0 policy: the existing `>0` filter (seats `<=0` not indexed) is intentional — left unchanged, only documented with a comment. Verifier ran the critical regression hunt: **no** existing test or production path passes a duplicate `sourceSeatIndex>0` that would now throw. New tests in `Tests/Editor/GCPlayerIndexMappingTests.cs` (reject-duplicate + accept-distinct). Not executed here (no local `Library/`); verifier GREEN.

---

## Task 8 — Codex bridge request/output files never cleaned up (P2, Low)

**Files:** `Editor/GamingCouchCodexTestBridge.cs:130-135` (EditorPrefs dedup, correct, but no file
deletion), `:161-174` (`GetDefaultOutputPath`), `:316/368` (writes `.xml`/`.json` per run), `:29`
(`SessionId` fresh GUID per launch); only `File.Delete` calls are inside `WriteFileAtomically`
(`:596/606`).

**Verified:** ✅ Confirmed. Dedup prevents re-processing but never deletes the request file;
per-run `.xml`/`.json` outputs accumulate in `OutputDirectory`; stale *session* directories also
never get GC'd across editor restarts. Small local files, editor-only — low impact.

**Fix (GREEN):** Delete the request file after handling, and/or add a bounded retention sweep
(max count/age) of `OutputDirectory` and stale session dirs on `PrepareBridgeSession`. Use
symlink-safe deletion consistent with the bridge's existing path hardening.

**Red/green testing:** EditMode test — seed N output/request files, invoke cleanup, assert
bounded/zero remaining.
- **Prerequisite:** extract a testable helper (e.g. `PruneOutputs(dir, maxAge/maxCount)`) since the
  logic is `private static` and I/O-bound.
- RED: files remain today. GREEN: pruned to bound.

**Checklist:** ☐ Helper extracted ☐ RED written & failing ☐ GREEN ☐ Regression ☐ Review

---

## Task 9 — Empty `screen_space` envelope emitted every frame (P3, Low)

**Files:** `Runtime/GamingCouch.cs:185-192` (`LateUpdate → hud.HandleQueue()` every frame);
`Runtime/Hud/GCHud.cs:204-224` (unconditional `EmitScreenSpace`, no empty-queue early return);
emit chain `Runtime/RuntimeMessages/GCRuntimeMessages.cs:919-930`, implicit `EnsureActiveRun`/`BeginActiveRun`
`:849/837`, default `screenSpaceEnabled=true` `:51`.

**Verified:** ✅ Confirmed. `HandleQueue` emits an empty `anchors:[]` envelope every frame; pre-`Play()`
the first emit implicitly begins an active run with default config (reset by a later real
`BeginActiveRun(options)`, so net-benign) — but it is a per-frame string build + JS bridge call on WebGL.

**Fix (GREEN):** Early-return in `HandleQueue` when `screenSpaceQueue.Count == 0` (keep the queue
clear guarded).

**Red/green testing:** `Tests/Editor/GCRuntimeOutputContractTests.cs` already calls `HandleQueue()`
(`:683/776/798`).
- **RED:** with an empty queue, assert `ScreenSpaceEmitted` is NOT invoked (and no active run is
  implicitly begun).
  - **First check** no existing test asserts the empty-emit / per-frame heartbeat behavior — if one
    does, confirm suppression is intended before turning it red.
  - RED: emit fires today. GREEN: suppressed.

**Checklist:** ☐ Existing-heartbeat check ☐ RED written & failing ☐ GREEN ☐ Regression ☐ Review

---

## Task 10 — `IsValidPlayerName` trim asymmetry (P3, Low)

**Files:** `Editor/GCDevJsonValidation.cs:583-588` (min uses `name.Trim().Length`, max uses
`name.Length`); callers `:395`, `:434`.

**Verified:** ✅ Confirmed. Min-length checks trimmed length, max-length checks raw length —
inconsistent for whitespace-padded names (e.g. `"  ab  "`: trimmed 2 vs raw 6).

**Fix (GREEN):** Pick one policy — most likely `var trimmed = name.Trim();` then bound both min and
max on `trimmed.Length`.

**Red/green testing:** Pure function, cheap.
- **RED:** whitespace-padded names at the min and max boundaries; assert consistent accept/reject.
  - RED: asymmetric today. GREEN: consistent.

**Checklist:** ☐ Policy chosen ☐ RED written & failing ☐ GREEN ☐ Regression ☐ Review

---

## Task 11 — Culture-sensitive seed parse (P3, Low)

**Files:** `Editor/GamingCouchEditor.cs:510` (`int.TryParse(seed, out value)` — current-culture);
sibling `Editor/GCDevJsonDraft.cs:28` (correct `NumberStyles.None, CultureInfo.InvariantCulture`).

**Verified:** ⚠️ **Partial.** The parse *is* culture-sensitive and disagrees with the sibling parser
for the same `file.seed`. But the review's "the one non-invariant numeric parse" is **false** — an
audit found other culture-sensitive numeric parses in non-test code: `Runtime/GamingCouch.cs:473`
(`int.Parse(...)`), `Editor/GamingCouchGameViewAspect.cs:694-695` (`int.TryParse` no culture) and
`:344/569/647` (`Convert.ToInt32`). Seed feeds `UnityEngine.Random` (`GamingCouchEditor.cs:506`),
so culture-varying accept/reject is a genuine (if rare) determinism concern.

**Fix (GREEN):** Change `:510` to `int.TryParse(seed, NumberStyles.None, CultureInfo.InvariantCulture, out value)`
to match `GCDevJsonDraft`. Audit the other listed sites and align any that affect determinism/contract
(scope those explicitly rather than silently sweeping all of them).

**Red/green testing:** `TryResolveSeed` is `private static` — test via the surrounding validation
path or lift the parse into a testable helper.
- **RED:** a seed string that parses differently under a non-invariant culture (or with a thousands
  separator/whitespace the invariant `NumberStyles.None` rejects); assert invariant behavior.
  - RED: culture-dependent today. GREEN: invariant, matches `GCDevJsonDraft`.

**Checklist:** ☐ RED written & failing ☐ GREEN (`:510`) ☐ Other sites audited/scoped ☐ Regression ☐ Review

---

## Task 12 — Unobserved faulted `SendAsync` exception (P3, Low)

**Files:** `Runtime/Dev/GCDevAppIntegration.cs:148-160` (`SendJsonMessage` coroutine).

**Verified:** ⚠️ **Partial — milder than described.** The review called it "fire-and-forget, not
observed". In fact the Task **is** polled to completion (`WaitUntil(() => sendTask.IsCompleted)`,
`:155`) and its `IsFaulted` flag is logged (`:157-159`). What is not done: reading `sendTask.Exception`,
so the `AggregateException` is technically still unobserved by the TPL and can surface via
`TaskScheduler.UnobservedTaskException` on finalization. Editor-only (`#if UNITY_EDITOR`), very low.

**Fix (GREEN):** In the `IsFaulted` branch, read/log `sendTask.Exception` to fully observe it.

**Red/green testing:** Not worth a dedicated test (Unity coroutine + live websocket) — a no-op
compile check suffices. If a test is desired, it would need a websocket seam.

**Checklist:** ☐ GREEN (observe `Exception`) ☐ Compiles ☐ Review

---

## Task 13 — Dead duplicate `WebSocket*` DTO block (P3, Cleanup)

**Files:** `Runtime/Dev/GCDevAppIntegration.cs:581-618` (`WebSocketInputData` `:583`,
`WebSocketDevToolMessage` `:593`, `WebSocketDevToolPayload` `:602`, `WebSocketRuntimeOutputOptions` `:613`).

**Verified:** ✅ Confirmed. The four classes form a closed, unreferenced cluster (only reference
each other). The live inbound path uses the parallel `GCDevAppRuntime*` types
(`Runtime/Dev/GCDevAppRuntimeInbound.cs:229` `JsonUtility.FromJson<GCDevAppRuntimeDevToolMessage>`).

**Fix (GREEN):** Delete the four classes and the now-empty `#if UNITY_EDITOR ... #endif` wrapper
(`:581/618`). ~35 lines, no behavior change.

**Red/green testing:** No red/green needed — pure dead-code removal. A compile check (editor asmdef)
is sufficient; existing `GCDevAppRuntimeInbound`/`GCDevAppRuntimeMessages` tests cover the live path.

**Checklist:** ☐ Classes deleted ☐ Editor asmdef compiles ☐ Live-path tests still green ☐ Review

---

## Task 14 — `ColorHex` always null (P3, Cleanup)

**Files:** `Runtime/GCPlayer.cs:143-144` (`private string colorHex;` / `public string ColorHex => colorHex;`).

**Verified:** ✅ Confirmed. `colorHex` is never assigned (grep `colorHex =` → 0 writes) and
`.ColorHex` is never read (grep → 0 reads). Sibling `ColorEnum`/`ColorName` are populated
(`GamingCouch.cs:743`). Pre-existing, not introduced by this branch.

**Fix (GREEN) — choose one:**
- **Delete** the dead field + property (safest), **after** checking the `public ColorHex` is not
  part of an external game-facing API contract, or
- **Wire it up**: derive hex from the resolved color where `colorEnum`/`colorName` are set.

**Red/green testing:** If deleting → compile check only (+ external-API grep). If wiring up → a test
asserting `ColorHex` matches the resolved color's hex.

**Checklist:** ☑ External-API check (in-repo) ☑ Decision (delete) ◐ GREEN (compile-only; live-Editor/CI run pending) ☐ Regression ☐ Review

**Progress (2026-07-03):** Deleted the dead `private string colorHex;` field and `public string ColorHex => colorHex;` property (and the orphaned doc comment) from `Runtime/GCPlayer.cs`. Repo-wide grep (independently re-run by the verifier) confirms **zero** remaining references to `ColorHex`/`colorHex` in any `.cs`/jslib/json — no callers, no `[DllImport]`, no serialization key. `ColorEnum`/`ColorName`/`ColorOffWhite` untouched. Removal is consistent with the branch's hard-removal style (ADR 0001).
> ⚠ **OWNER CONFIRM before merge:** `ColorHex` was a *public* property on the game-facing `GCPlayer` type. It was always null and unreferenced in this repo, but external game code in **other repos** could theoretically read `player.ColorHex` — which cannot be checked from here. Confirm no external consumer depends on it (if one does, prefer wiring `ColorHex` to the resolved color instead of deleting).

---

## Decision D1 — Release profile forces WebGL compression `Disabled` (DECIDED: by design)

> **Decision (2026-07-02, owner):** No compression for now is **by design**. Keep the profiles
> forcing `WebGLCompressionFormat.Disabled`; do **not** switch to Brotli/Gzip. This is not a bug.
> Remaining work is optional documentation only (Option A below) so a future reader doesn't
> "fix" it. No code/behavior change; existing tests that pin `Disabled` stay authoritative.


**Files:** `Editor/GamingCouchWebGLBuildSettingsProfiles.cs:292` (Release spec
`CreateWebGLCompressionSpec(WebGLCompressionFormat.Disabled)`), `:372-382` (re-asserts on apply),
`:312` (Dev same). Pinned by tests: `Tests/Editor/GamingCouchActiveSceneSetupAssetTests.cs:666-733`,
`Tests/Editor/GCUnityBuildInfoSidecarWriterTests.cs:64,489`; recorded at
`Editor/GCUnityBuildInfoSidecar.cs:429`.

**Verified:** ⚠️ **Deliberate-but-unflagged.** Not a mechanical bug — `Disabled` is now an intended
contract with a readiness gate and sidecar assertions. Corrections to the review's "old code left it
untouched": the old `WebBuildOptimizer` Release path had the Brotli line **commented out** (so
Release genuinely didn't touch compression), while the old **Dev** path already forced `Disabled`.
So this is a behavioral change **for Release only** (untouched → forced `Disabled`), and it
re-asserts over any user Brotli/Gzip. The load-bearing assumption — "the host/CDN compresses on the
fly" — is **undocumented** anywhere in this repo (no comment, no doc; the serving config lives in
the platform repo, not here). If that assumption is wrong for any deploy target, this ships
uncompressed WASM.

**Chosen — Option A (docs only; optional):** ☑ Done — kept `Disabled`; a comment at the spec
(`GamingCouchWebGLBuildSettingsProfiles.cs:292`) now points to
`docs/adr/0013-webgl-compression-disabled.md`, which records the decision and serves as the
doc note. No behavior change; tests stay green.

**Rejected — Option B (switch Release to Brotli):** not wanted. Do not change the compression format.

No red/green work — this is a documentation-only follow-up (or skip entirely). The existing tests
that assert `Disabled` (`GamingCouchActiveSceneSetupAssetTests.cs:666-733`,
`GCUnityBuildInfoSidecarWriterTests.cs:64,489`) remain the authoritative pin on this behavior.

---

## Confirm-only (deliberate changes — no code)

- **C1 — NGO elimination/finish sync is lossy** (`Runtime/Unity/NGO/GCNetworkPlayer.cs:55-71`): server
  `Permanent` collapses to `Revokable` on clients via the bool NetworkVariable. Gated behind
  `GC_UNITY_NETCODE_GAMEOBJECTS`, explicitly framed as an unsupported temporary surface, pre-existing.
  Confirm it stays framed that way; no change.
- **C2 — DPad / right-stick removed from `leftX/Y`** (`Runtime/GCControllerInputs.cs`): matches the
  "remove unused input fields" commits. Any source relying on DPad-only movement now produces none.
  Confirm intentional; no change.

---

## Cleanup backlog (quality — not independently re-verified in this pass)

These were carried from the review's "Cleanup" section and are **not** verified to the same depth as
the tasks above. Promote individually before implementing (dedupe risk: consolidating a helper can
change one call site's behavior).

- Consolidate duplicated helpers: `TryReadBool` (DevJson vs PlatformData validation);
  default-selection (`GamingCouchWebGLExportSetup` vs the preview window);
  `GetObjectReferenceState`/`GetSerializedObjectReferenceState` (`GamingCouchSceneWiring`);
  `CreateSelectedIdSet`/`AddDetail`/`FormatEnabled`.
- Triple validation in `GCDevJsonValidation.ValidateParsedObject`.
- Effectively-dead `GCGame.BuildPlayersHudData` (test-only now) — confirm before removal.

---

## Verified-clean (no action — recorded for reviewer confidence)

Codex bridge security (GUID-nonce request IDs, path-traversal confinement, symlink rejection per
segment, 0600/0700 perms, constant-time token compare, no command-exec); JSON envelope contracts vs
`GCRuntimeOutputContractTests`; all `[DllImport]` externs exist in `GamingCouch.jslib`; obsolete-API
removals have no remaining in-package callers; seat→playerIndex identity mapping; game-over
snapshot-before-`game_over` ordering and placement idempotency; log-capture listener/mirror safety;
exact seat-count validation; fixture round-trips preserve key order + trailing newline.
