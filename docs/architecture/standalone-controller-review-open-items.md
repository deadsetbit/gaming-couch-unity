# Standalone-controller review — open items

Status: Open register
Last updated: 2026-09-10
Owner: Gaming Couch Unity package team

## Purpose

The two branch reviews of `feature/standalone-controller` catalogued 122 findings and deferred most
of them with "file issues or fix opportunistically". They were re-verified against the code on
2026-09-10 and the actionable ones are now applied — see the commits between the test-bridge removal
and `docs: make the contracts and ADRs describe what the code actually does`.

This file is what is left. Every entry here was verified as real and current; nothing in it is a
guess. Entries fall into four kinds, and the distinction matters: **decisions** need an owner's call
before code can move, **deferred work** needs only effort, **invalid** findings must not be
reopened, and some belong to another repo entirely.

Delete an entry when it is settled. This file is not a backlog of ideas — for those see
`gamingcouch-unity-backlog.md`.

---

## Decisions

### 1. Authentication on the DevApp local socket

The runtime socket has no authentication. Any local process that claims port 3167 first receives
`projectRootPath` and `projectName` and can inject inputs, restart and pause the game. The
identity query parameter is trusted verbatim; the host/origin checks only prevent DNS rebinding and
a native process sends no origin at all.

Sized: a handful of lines at the check itself, but two harder parts. The token has to reach Unity,
and the DevApp never spawns the editor, so there is no environment or command-line channel — the
managed root files are the plausible carrier, but their content is fetched from an edge function and
`gc.dev.json` is strictly validated with no free-form field, making it a schema change on both
sides. And there is no protocol negotiation, so requiring a token hard-rejects every older Unity
build; a staged warn-then-require rollout would be new machinery rather than an extension of
anything that exists.

**Decide:** build it now, defer it, or accept the exposure for a loopback development tool.

### 2. Splitting `GamingCouchActiveSceneSetup` (2,781 lines)

Six separable responsibilities: the canonical-source rewriter, the pending-setup state machine, the
prefab factory, listener wiring, folder cleanup, and wire-example orchestration. The rewriter and
the state machine are the two clean seams.

No external risk — nothing here is public API. The risk is regression: the EditMode suite drives
`internal` members directly, so members moving across file boundaries have to keep that reachable.

**Decide:** split now and along which seam, or leave it until the next feature lands on the file.

### 3. ADR 0013 contradicts itself on WebGL compression

ADR 0013 says the apply/readiness path re-asserts compression `Disabled` over any user-set
Brotli/Gzip. Every settings row is built skippable and apply honours deselection, so unchecking the
compression row skips the re-assert.

But two tests deliberately pin that row as skippable — one uses it as the explicit counterpoint to
the rows that *are* required — and ADR 0013 itself names that test file as its guardrail, saying
those tests failing on a compression change is the guardrail working. So the ADR asks for behaviour
its own stated guardrail forbids.

Two further facts worth having before deciding: the readiness half of the guarantee already holds
(compression drift is reported and readiness blocks on it), so the hole is only interactive
deselection in the preview window; and enforcing it would additionally require the apply path to
gate its deselection check on whether a row is skippable, otherwise a non-skippable row is absent
from the default selection set and gets skipped rather than forced.

**Decide:** soften the ADR so readiness-blocks is the guarantee, or make the row non-skippable and
rewrite the two tests.

### 4. A deadline for the pending-setup poller

The per-tick assembly sweep is gone, but the poller itself is still unbounded: nothing clears the
pending flag when the expected type never appears, so a generated script that never compiles leaves
the poller installed and the setup buttons disabled for the rest of the editor session.

The value is the decision. A slow project's first compile can legitimately take minutes, so too
short a deadline breaks a legitimate case.

**Decide:** the timeout, and what expiry does — re-enable the buttons silently, or surface a message.

### 5. Where the hosted HUD's value column comes from

Removing the dead HUD builder left `PlayersHudValueType.Status`/`Text`/`Lives` and
`GCPlayer.GetHudValueText()`/`GetHudStatusText()` with no production consumer; the value-type enum
is now read only by the points maxScore validation. These are public API that consumer games
override — the reference template does — and those overrides have been dead since ADR 0004 retired
HUD delivery in favour of runtime-message snapshots.

**Decide:** is the value column meant to come from the snapshot now, making these accessors obsolete
surface to remove, or is this a gap to reconnect?

### 6. Whether `GameOver()` should report failure — recommendation: no

`GameOver()` is `void` and returns silently on a duplicate call or an invalid placement; the game
gets a diagnostic but no programmatic signal. All eleven consumer call sites are bare statements, so
returning `bool` is source-compatible, but it would be unused surface — and the failure surface
would stay split, because the not-set-up path throws rather than returning false.

Recommendation is to leave it. Noted because it surfaced a real defect elsewhere: see the
`piratewars` entry below.

---

## Deferred work — no decision needed, only effort

- **Run-generation stamping.** Stale player references can queue transitions into a new run:
  the queue path has no active-run guard and the mutation gate only blocks after game over. Needs a
  run id or generation stamped on each player at setup. Pairs with the next item.
- **A run-end seam.** Two findings need the same missing hook. The Unity log capture subscribes to
  `logMessageReceived` and is never torn down — it is configured only from the active-run begin
  path, there is no matching end, and nothing hooks destroy or play-mode exit — so the static
  handler survives the run and, with domain reload disabled, the next one. Separately `GCGame`
  subscribes to player transitions with no teardown, so after `Clear()` its handler stays attached
  to players whose destruction is deferred to end of frame. Deciding where "run end" lives unblocks
  both; the store half is already fixed.
- **The apply-transition pair in `GCPlayer`.** Deliberately not unified with the rest of the
  transition dedup: each writes a different state field and timestamp trio and raises a
  differently-typed event, so unifying needs three delegates per call and costs more lines than the
  duplication does. Revisit only if a third state family appears.
- **Start Screen refresh throttle.** The window still runs a full readiness inspection on every
  hierarchy change, unthrottled. The mechanical half (cached scene list, hoisted styles, catalog
  refresh on focus) is done; only the throttle interval remains, and that is a policy value.
- **Shared runtime-info builder for the release tools.** The two Python tools define the canonical
  payload shape and the platform/protocol constants independently and must emit byte-identical
  output. They agree today only because dictionary insertion order happens to match the
  hand-written template's key order, and they already diverge on escaping: one escapes the name and
  version, the other does not, so a package name containing a quote or backslash would bake invalid
  JSON. It fails safe — the checker rejects it — but the extraction needs a differential
  byte-identity test landed *first*, or a mistake silently breaks the release gate.
- **Test-side deferrals.** Reflection into private members in two suites (fixing it means `internal`
  seams plus `InternalsVisibleTo`, a production visibility change); hand-copied ordered JSON slices
  asserted as string literals; a production-grade C# literal scanner living inside a test file, with
  its own test; source-text assertions against implementation files across eight EditMode suites.
- **`GamingCouchSceneCatalog` coverage.** Deliberately skipped rather than deferred: its only entry
  point drives the asset database over real scene assets, repo convention is not to create or close
  scenes inside the shared EditMode session, and a test over whatever scenes happen to exist would
  pass vacuously.
- **The scaled half of the dual-timebase invariant.** The unscaled half is now pinned with real wall
  clock. Demonstrating that game-facing timestamps follow the scaled clock needs a PlayMode test —
  EditMode runs no frames, so scaled game time cannot be made to advance.
- **Object-reference-state duplication in `GamingCouchSceneWiring`.** Blocked only by branch
  divergence: an unmerged commit on `fix/standalone-controller-hardening` rewrites the same two
  version guards. Do it after that merges, not before.

---

## Invalid — do not reopen

Five findings recommended undoing something a test deliberately pins. Each was verified and
abandoned; the note is here so they are not rediscovered and "fixed" into a regression.

1. **Obsolete stubs for the removed `OnEliminated`/`OnUneliminated`/`OnFinished` events.** One test
   method asserts, side by side, that removed *methods* carry `[Obsolete(error)]` migration guidance
   and that these three *events* are absent. The methods-versus-events split is deliberate.
2. **A `-1` sentinel for the fallback platform-data version.** Three tests pin the
   declared-version behaviour on both the editor and runtime side, and nothing anywhere consumes
   the field. Settled as a contract correction instead.
3. **Checking blocked state before mutating Build Settings in Active Scene Setup.** A test named for
   the behaviour pins the opposite as a guarantee: Build Settings are normalised even when scene
   wiring is blocked.
4. **Making the WebGL compression row non-skippable.** See decision 3 — the ADR contradicts its own
   guardrail, so this needs settling before any code moves.
5. **Deleting the HUD data types.** They are the parameter type of the obsolete `UpdatePlayers`
   stub, which is pinned twice. The genuinely dead builder around them was removed instead, so they
   are now referenced by nothing but that obsolete signature.

---

## Belongs to another repo

Found while verifying the above; none is fixable in this package.

- **DevApp: clamp the round-seed range.** It offers a fixed seed up to `Number.MAX_SAFE_INTEGER`
  while an over-range seed in `gc.dev.json` is a hard validation error here — so a large seed chosen
  in the DevApp is written and then refused.
- **DevApp: the seat-name predicate checks its bounds inconsistently.** Minimum on the trimmed
  value, maximum on the raw one. Harmless from this side now that Unity trims before persisting, but
  still wrong on its own terms.
- **DevApp: nothing pins the runtime socket's JSON spacing.** Unity no longer depends on compact
  output, but the DevApp's own README documents the spaced form while its serializer emits compact.
  A test there would make the encoding a stated guarantee rather than an accident.
- **`piratewars`: `GameOver()` is called every `FixedUpdate`.** No state guard, so once one player
  remains it fires for the rest of the round, spamming duplicate-placement diagnostics. It works
  only because this package swallows the repeat.
- **Consumers already broken against the current package**, pre-existing and unrelated to this work:
  `game-sumo`, `rockets` and `NGOTest` call APIs that are now compile errors.
