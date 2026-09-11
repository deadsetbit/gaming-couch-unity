# Public Mirror Plan — private source, public releases

Make `deadsetbit/gaming-couch-unity` **private** — ADRs, reviews, agent notes, WIP branches
and dev tooling stay internal — and publish each release to a separate **public** repo that
consumers install from.

The source repo stays public until the final phase, so every step before it is invisible to
consumers and reversible.

## Goal

- **Public repo:** `https://github.com/deadsetbit/gaming-couch-unity-public` — generated,
  read-only, package contents at the repo root.
- **Keep release history** as an immutable, browsable set of `unity-<version>` tags.
- **Be able to delete a specific release outright** if something slips in by accident.

## Where this stands

- [x] Public repo `deadsetbit/gaming-couch-unity-public` created (public, **still empty** —
      no commits, no branches, Pages not enabled).
- [ ] Deploy key pair generated; public half added to the public repo **with write access**.
- [ ] Private half stored as secret `GAMING_COUCH_UNITY_PUBLIC_DEPLOY_KEY` on
      `deadsetbit/gaming-couch-unity`.

Everything from Phase 1 on is unstarted.

## Where the work is tracked

Issues live in **`deadsetbit/gaming-couch`** — for all three repos this plan touches
(`gaming-couch-unity`, `gaming-couch-unity-public`, and `deadsetbit/docfx-unitypackage`).
`gaming-couch-unity`'s own issue list is deliberately unused. A commit made **in the Unity
repo** must therefore use the qualified form, `Refs deadsetbit/gaming-couch#<n>` — a bare
`#<n>` there resolves against the wrong repo, and `code-review` finds a commit's originating
spec by reading that reference.

Each phase below marks whether it is **agent-executable** or needs a **human**, and states
how to prove it is done. A phase with no verification is not finished.

## Facts this plan builds on

- This repo **is** the UPM package `com.dsb.gamingcouch` — `package.json` at the root, plus
  `Runtime/`, `Editor/`, `Tests/`, `Plugins/`, `Documentation~/`, `ContractFixtures/`.
- Consumers install **UPM-via-git-tag**: `…/gaming-couch-unity.git#unity-<version>`.
- Releases are lightweight `unity-<version>` tags cut by `Tools/bump-version.py`, which also
  re-bakes `Runtime/Resources/GamingCouchRuntimeInfo.json` and verifies it with
  `Tools/check-runtime-package-info.py`.
- **Promotion to end users is already manual and separate from publishing.** What a device
  installs is the `unity-<tag>` the `devapp-component-versions` Supabase edge function serves
  for its environment, from
  `backend/supabase/functions/devapp-component-versions/componentVersionMap.json` in the
  client monorepo. That map is **not a pointer**: each of the `production` and `develop`
  sections is a `supportedUpTo` ceiling plus a `floors` array of `{fromDevApp, unity}`
  entries, resolved highest-matching-floor-wins and failing closed above the ceiling.
  Production is promoted by a manual `develop` to `main` pull request plus an edge-function
  deploy. `componentVersionMap.md` in that folder is the operating procedure.
- The org already runs this pattern once: the client's `.github/workflows/sync-public.yml`
  mirrors its `public/dist/` folder to `deadsetbit/gaming-couch-public` over a scoped SSH
  deploy key.
- The org is on GitHub **Free**, so a private repo there cannot serve GitHub Pages at all.
  (Pages in private repos exists on Team and Enterprise; it is not available to us.)

## Target architecture

```
PRIVATE  deadsetbit/gaming-couch-unity            PUBLIC  deadsetbit/gaming-couch-unity-public
──────────────────────────────────────            ─────────────────────────────────────────────
public/package/         <- the package            (contents of public/package/ become the ROOT)
  package.json                                     tag unity-0.1.0-alpha.8  (orphan snapshot)
  Runtime/ Editor/ Tests/ Plugins/                 tag unity-0.1.0-alpha.9  (orphan snapshot)
  ContractFixtures/ Documentation~/                gh-pages  = the docs site
  README CHANGELOG LICENSE                         main      = hand-written landing page
public/landing/README.md   <- the landing page source, unpublished by the mirror
public/AGENTS.md           <- the rule, unpublished
                                    │  tag push
docs/ Tools/ .claude/ .github/  ────┼──────────▶
CONTEXT.md AGENTS*.md               │  CI: build an orphan snapshot of public/package@tag,
package.json (dev scripts only)     │      publish ONLY that tag with a scoped deploy key
  (never leaves the private repo)   ┘
```

## Principles

1. **The boundary is the filesystem, not a list.** If a file is under `public/package/`, it
   ships; if not, it cannot. There is no allowlist to forget to update — and no ignore-list
   inside the published folder either. Anything that must stay private lives outside the
   folder. Dev-only *behaviour* ships and is gated at runtime rather than stripped at publish
   time: stripping would mean the package we test is not the package that ships, and the
   completeness gate would stop gating the real artifact.
2. **One release = one orphan snapshot commit + one tag.** Releases share no ancestry, so
   deleting a version's tag makes its commit unreachable without touching any other release.
3. **Publish is not promote.** Publishing a tag only makes a version *available*. The DevApp
   component-version map is the sole thing that puts a version in front of anyone. A broken
   publish is inert until someone adds a floor pointing at it.
4. **Tags are immutable.** Never move or reuse a published tag — UPM caches by ref. A broken
   release is fixed by cutting the next version. Republishing means deleting the tag from the
   mirror first.
5. **Nobody installs a moving ref.** Public `main` is a landing page and carries no package
   manifest, so a URL without a tag fails loudly instead of silently tracking whatever
   shipped last. "Latest" is whatever the map's `production` floors name; "latest edge" is
   what its `develop` floors name.

## Naming / constants

| Thing | Value |
|---|---|
| Public repo (SSH) | `git@github.com:deadsetbit/gaming-couch-unity-public.git` |
| Public repo (docs/consume URL) | `https://github.com/deadsetbit/gaming-couch-unity-public` |
| Package folder in private repo | `public/package/` |
| Tag format | `unity-<version>` (unchanged) |
| Actions secret (private repo) | `GAMING_COUCH_UNITY_PUBLIC_DEPLOY_KEY` |
| Bot identity | `gaming-couch-bot <bot@deadsetbit.com>` |
| Docs site root | `https://deadsetbit.github.io/gaming-couch-unity-public/` |
| Docs folder for a release | `https://deadsetbit.github.io/gaming-couch-unity-public/<version>/` |
| Docs version manifest | `https://deadsetbit.github.io/gaming-couch-unity-public/versions.json` |
| Permanent link home baked into every page | `https://gamingcouch.com` |

---

## Phase 0 — Credentials · **human only**

The public repo already exists. What remains is the deploy key.

1. **Generate a dedicated deploy key pair** (no passphrase), from a scratch directory:
   ```bash
   ssh-keygen -t ed25519 -C "gaming-couch-unity-public deploy key" \
     -f ./gc-unity-public-deploy-key -N ""
   ```
2. **Add the PUBLIC key to the PUBLIC repo, with write access:**
   `deadsetbit/gaming-couch-unity-public` → Settings → Deploy keys → Add deploy key →
   paste `gc-unity-public-deploy-key.pub` → **check "Allow write access"**. Scoping the key
   to this repo means it can never touch the private source.
3. **Add the PRIVATE key to the PRIVATE repo as an Actions secret:**
   `deadsetbit/gaming-couch-unity` → Settings → Secrets and variables → Actions →
   New repository secret → Name: `GAMING_COUCH_UNITY_PUBLIC_DEPLOY_KEY` → Value: the full
   private key including its BEGIN/END lines.
4. **Delete the local key files.** Note that this leaves the key reachable *only* from
   Actions — see the unpublish workflow in Phase 4, which is why unpublishing has to be a
   workflow rather than something run from a laptop.

**Verify:** `gh api repos/deadsetbit/gaming-couch-unity-public/keys` lists one key with
`read_only: false`, and `gh api repos/deadsetbit/gaming-couch-unity/actions/secrets` lists
`GAMING_COUCH_UNITY_PUBLIC_DEPLOY_KEY`.

One key serves both the release mirror (Phase 4) and the docs deploy (Phase 6).

## Phase 1 — Restructure the private repo into `public/package/` · **agent, with one human step**

A repo-wide move commit. Every open branch conflicts with it, so land or abandon what matters
first, then do the move as one isolated commit. **Phases 1 and 2 land together** — between
them the release tooling points at the old paths and no release can be cut.

- [ ] Move the package into `public/package/`: `package.json`, `Runtime/`, `Editor/`,
      `Tests/`, `Plugins/`, `Documentation~/`, `ContractFixtures/`, `README.md`,
      `CHANGELOG.md`, `LICENSE.md` and their `.meta` files — including the top-level folder
      metas (`Runtime.meta`, `Editor.meta`, `Tests.meta`, `Plugins.meta`,
      `ContractFixtures.meta`), which carry the GUIDs that asmdef references resolve against.
- [ ] **`ContractFixtures/` moves inside the package**, not outside it.
      `Tests/Editor/GCDevJsonContractFixtureTests.cs` resolves its corpus from the package
      root via `PackageInfo.FindForAssembly(...).resolvedPath`, so leaving the fixtures behind
      breaks eleven of its tests for every consumer. There is nothing private about them, and
      they are the clearest public description of the `gc.dev.json` contract.
- [ ] Leave outside `public/package/` (private): `docs/`, `Tools/`, `.claude/`, `.cursor/`,
      `.github/`, `CONTEXT.md`, `AGENTS*.md`, `VERSIONING_PLAN.md`, `.vscode/`.
- [ ] **Neutralise `.github/workflows/docfx-unitypackage.yml` in the same commit.** It
      triggers on every push to `main` and the action reads `Documentation~/`, `README.md`,
      `CHANGELOG.md`, `LICENSE.md` and `package.json` from the **repo root**. After the move
      those are gone from the root, every one of the action's `if [ -f … ]` guards takes its
      not-found branch, and the run **stays green** while deploying a gutted site over
      `https://deadsetbit.github.io/gaming-couch-unity/` — the `documentationUrl` baked into
      every already-published tag, including the one production serves. Reduce the trigger to
      `workflow_dispatch:` only; Phase 6 rebuilds it properly.
- [ ] Add `public/AGENTS.md` stating the rule: everything under `public/package/` is published
      on release; write paths and links relative to the package root, because the
      `public/package/` prefix does not exist in the public repo; files in `public/` itself
      are not published. (This repo has no `AGENTS.md`/`CLAUDE.md` pairing convention — adopt
      it repo-wide or not at all, but do not add a lone nested `CLAUDE.md`.)
- [ ] **Split the manifest subtractively.** The shipped `public/package/package.json` keeps
      every field it has today **minus `scripts`** — `displayName` and `description` included;
      the DocFX action builds its site title from `displayName`, so dropping it retitles every
      page `" | <version>"`. Only `scripts` moves, into a **new private root `package.json`**
      carrying `"private": true`, which never ships.
- [ ] In `public/package/package.json`, repoint `documentationUrl` at the docs site root and
      `changelogUrl`/`licensesUrl` into the public repo.
- [ ] **Sweep every shipped link that dies at the flip.** Not just the five
      `…/gaming-couch-unity/blob/main/…` and `tree/main/docs/adr` links in `README.md`: there
      are five `https://deadsetbit.github.io/gaming-couch-unity/api…` links in `README.md`
      too, plus two in `Documentation~/README.md` — a github.io link, and a
      `../docs/contracts/` relative link that escapes the package root and cannot resolve in
      the published tree at all. Move `docs/contracts/platform-runtime-contract.md` and
      `docs/contracts/devapp-local-play-contract.md` into `public/package/Documentation~/` and
      link them there; repoint API links at this release's own **`/<version>/api/…`** folder,
      which `Tools/bump-version.py` maintains from then on; drop the
      links to the ADRs, the backlog and `CONTEXT.md`.
- [ ] **Rewrite the moved contract documents' own links.** They are not inert text: between
      them they carry around a dozen `../adr/…` links plus `../../README.md` and
      `../../CONTEXT.md`. From `public/package/Documentation~/` every one of those resolves
      outside the published package, and `docs/`, the ADRs and `CONTEXT.md` are deliberately
      staying private — so a consumer following any link inside a published contract hits a
      404. Rewrite the ones with a public destination and remove the rest; where an ADR is
      genuinely load-bearing for a game author, fold its point into the contract text rather
      than linking to a private file.
      **Acceptance:** `grep -rnE "\]\((\.\./)+|gaming-couch-unity/|github\.io/gaming-couch-unity" public/package/`
      returns nothing that escapes the package root.
- [ ] Delete the metas orphaned by the move — `docs.meta`, `Tools.meta`, `AGENTS.md.meta`,
      `CONTEXT.md.meta`, `VERSIONING_PLAN.md.meta`, `AGENTS.local.example.md.meta`,
      `Tools/__pycache__.meta`. Unity will never see those files again.
- [ ] Update the paths named in `AGENTS.md` and under `docs/`. ADR 0012 names
      `ContractFixtures/LocalPlay` as a repo-root path and calls it the portable corpus a
      second engine adapter would consume; changing an ADR's text is a deliberate act, so call
      it out in the PR rather than sweeping it in.
- [ ] `Tests/` keeps shipping. It is Apache-2.0 code that is public today, and shipping it
      keeps the package we test byte-identical to the package consumers install.
- [ ] **Human step — the host Unity project.** `Packages/gaming-couch-unity` in the host
      project is an **untracked** symlink to the repo root: per-developer local state that no
      CI agent and no fresh clone has. It must point at
      `…/gaming-couch-unity/public/package`, and every developer repeats the fix on their own
      machine. The host project path comes from the gitignored `AGENTS.local.md`.
      **Verify:** `readlink` ends in `/public/package`, and
      `test -f Packages/gaming-couch-unity/package.json` passes.

**Verify the phase — human, because it needs a Unity licence and a free project lock:**

```
unity test <host-project> --mode EditMode --output "$TMPDIR/test-results.xml"
```

Host project path from `AGENTS.local.md`. Read the **exit code, not the log**: `0` all passed,
`8` the run finished with failures, anything else (commonly `6`) means no verdict — a compile
error, an unavailable licence, a crash, or a timeout. It prints nothing while running and a
cold project can be silent for minutes; that is not a hang. It spawns its own Editor, so it
cannot run while an Editor holds the project lock.

The suites that actually catch a bad move: `GCDevJsonContractFixtureTests` (reaches the
corpus), `GCRuntimeInfoTests` and `GCRuntimeOutputContractTests` (resolve via
`FindPackageRootPath()`), and `GCEditorPackageIdentityTests` (reads the manifest). Also run
`python3 Tools/test_check_runtime_package_info.py`.

## Phase 2 — Retool the scripts for the new path · **agent**

Lands with Phase 1.

- [ ] `Tools/bump-version.py`: rebase `PACKAGE_JSON_PATH` and `BAKED_RUNTIME_INFO_PATH` under
      `public/package/`, and fix the one hardcoded literal in the final commit's pathspec
      (`"package.json"` — the baked path beside it is derived from `BAKED_RUNTIME_INFO_PATH`
      and corrects itself). `CHECK_SCRIPT_PATH` stays under `Tools/`.
- [ ] `Tools/check-runtime-package-info.py`: introduce
      `PACKAGE_DIR = ROOT_DIR / "public" / "package"` and rebase `PACKAGE_JSON_PATH`,
      `RUNTIME_DIR`, `WEBGL_BRIDGE_PATH`, `PACKAGE_CODE_DIRS`, and the literal
      `Runtime/GamingCouch.cs` near the end. `BAKED_RUNTIME_INFO_PATH` and
      `WEBGL_BOOTSTRAP_PATH` derive from `RUNTIME_DIR` and follow automatically.
- [ ] Delete `.releaserc.json`. It targets `Development-v${version}` tags and semantic-release
      was never wired in; fixing its asset paths would preserve a file nothing runs.
- [ ] Decide what `--force-tag` means now. `bump-version.py --force-tag` does `git tag -f`
      then a force push. Once the mirror exists, forcing an already-published tag makes the
      two repos diverge silently: the mirror re-runs, its push is rejected as a
      non-fast-forward, and the public repo keeps serving the old commit. Either remove the
      flag or make it refuse a tag that already exists on the mirror.

**Verify — agent.** Do **not** use `npm run release:dry` as the proof: `bump-version.py`
returns at its `--dry-run` branch *before* it touches the baked runtime info, before it runs
the check script, and before the commit pathspec, so a green dry run exercises one of the four
rebased paths. Instead: `python3 Tools/check-runtime-package-info.py` exits 0, and
`python3 Tools/test_check_runtime_package_info.py` passes. The bump and commit paths are
first genuinely exercised by the throwaway tag in Phase 4, so Phase 2 does not fully close
until then — say so rather than pretending otherwise.

## Phase 3 — Publish gate (`Tools/check-dist-complete.py`) · **agent**

A static, no-Unity-licence gate that fails the publish if `public/package/` is not a
self-contained package. Because the boundary is a folder, the residual risk is a reference
that points outside it.

**CLI contract:** `python3 Tools/check-dist-complete.py <package-dir> <tag>`. Exit `0` pass,
non-zero fail, with every failure printed as a path plus a reason. It ships with a unit test
beside it, following `Tools/test_check_runtime_package_info.py` — a gate that is the only
thing between a secret and the public internet does not ship untested.

- [ ] `package.json` present, valid JSON, `name == com.dsb.gamingcouch`, and
      `version == <tag without the "unity-" prefix>`.
- [ ] **Meta pairing.** Every asset has a sibling `.meta` and every `.meta` has its asset. The
      **only** exclusion is paths with a segment ending in `~`: Unity hides those, so
      `Documentation~` has no metas by design. Do **not** exclude the package's top-level
      folders — they do carry committed metas, and skipping them would blind the gate to
      exactly the loss that a bad `git mv` causes. (`.DS_Store` is untracked here and never
      reaches a CI checkout; ignore it defensively if you like, but it is not why the rule
      needs an exception.) **Acceptance:** run against today's tree and get zero unpaired
      assets and zero orphan metas.
- [ ] **Asmdef references.** Every `.asmdef` parses; every `GUID:` reference resolves to an
      `.asmdef.meta` inside `public/package/`; every by-name reference resolves inside the
      folder *or* appears on an explicit external allowlist. The allowlist is required and
      non-empty today — `Unity.Newtonsoft.Json` and `UnityEngine.UI` (editor assembly) and
      `Unity.Netcode.Runtime` (the NGO assembly, itself gated behind a define constraint).
      The gate prints the allowlist whenever it is consulted, so growth is visible.
- [ ] `ContractFixtures/LocalPlay` is present. The asmdef check cannot see it — the shipped
      tests reach it by string path — so it needs its own assertion.
- [ ] Secret scan over `public/package/` as the leak backstop. Pin the tool and the exact
      invocation before writing the ticket: `gitleaks` defaults to scanning git *history*, so
      a folder scan needs directory mode, and `gitleaks/gitleaks-action` requires a paid
      licence for **organization-owned** repos, which this is. A hit blocks the publish and
      the response is rotation, per Phase 8.

## Phase 4 — Mirror workflow (tag-triggered orphan snapshot) · **agent, human for the install check**

Two workflows in the **private** repo.

**`publish-mirror.yml`** runs on `unity-*` tag push, and manually for a republish. It builds
an orphan snapshot of `public/package/` at the tag and publishes **only that tag**. It never
touches the public repo's `main`.

- Resolve the tag from either the trigger or the `workflow_dispatch` input.
- `actions/checkout@v4` at that tag, `fetch-depth: 1`, `permissions: contents: read` — the
  publish authenticates with the deploy key, not `GITHUB_TOKEN`.
- Hard gates, in order: `python3 Tools/check-runtime-package-info.py`, then
  `python3 Tools/check-dist-complete.py public/package <tag>`.
- Install the deploy key into `~/.ssh` and set `GIT_SSH_COMMAND` with `IdentitiesOnly=yes`.
- Copy `public/package/.` into a temp directory and initialise a repo there with an explicit
  initial branch name (the runner warns otherwise). Note the root `.gitignore` deliberately
  sits **outside** `public/package/`, so the orphan `git add -A` sees the whole copied tree.
- Commit with the bot identity set per-command (`git -c user.name=… -c user.email=…`), message
  `gaming-couch <version>`; tag it `unity-<version>`; add the public repo as a remote and
  publish that one tag ref.
- `concurrency: { group: publish-mirror, cancel-in-progress: false }`.

**`unpublish-tag.yml`** is `workflow_dispatch` with the tag as a required input, and deletes
that tag from the public mirror using the same deploy key. It exists because Phase 0 step 4
deletes the local key files: after that, the key lives only as an Actions secret, so there is
no laptop from which a human can delete a mirrored ref. Without this workflow, Phase 4's own
last checkbox cannot close and Phase 8's unpublish has no mechanism.

Two things the mirror cannot do, by construction:

- **Republish over a live tag.** Every run builds a fresh orphan commit with a new SHA, so
  publishing an existing tag is rejected as a non-fast-forward. That rejection is the
  guardrail working — do not reach for `--force` when a re-run fails. A republish means
  running `unpublish-tag.yml` first.
- **Publish a tag cut before Phase 1.** Checking out an older tag yields a tree with no
  `public/package/` and no gate script. The first exercise of this workflow is a throwaway tag
  cut after the restructure, not a replay of history.

- [ ] Add both workflows.
- [ ] Cut a throwaway tag (`unity-0.0.0-test.1`) and let it publish.
      **Verify (agent):** `gh api repos/deadsetbit/gaming-couch-unity-public/contents?ref=unity-0.0.0-test.1`
      lists `package.json` at the root.
      **Verify (human):** the tag installs into a scratch Unity project — a throwaway project,
      not the host project, so the host's symlink stays the thing under test in Phase 1.
- [ ] Run `unpublish-tag.yml` on the throwaway tag and confirm the release is gone while
      `main` and the docs are untouched.

## Phase 5 — The public repo's landing page · **human**

`main` on the public repo is a hand-written page, not a snapshot. It welcomes a browser,
explains what the package is, and sends them to the tags. Publish it **before or with** the
first mirrored tag, so nobody lands on an empty repository.

The source of truth is `public/landing/README.md` in the private repo — outside
`public/package/`, so the mirror never publishes it — and it is copied to the public repo's
`main` by hand. The mirror workflow never touches `main`.

The page must:

- Say what Gaming Couch is and what the package does, without version numbers, so it does not
  drift.
- Give the install form with a pinned tag placeholder and link to the tags page.
- State that the package lives in the tags, not on this branch.
- Carry **no `package.json`** — that absence is what makes a tagless git URL fail loudly
  instead of installing a moving version.
- Not carry a notice about the dead legacy docs URL. Someone following
  `https://deadsetbit.github.io/gaming-couch-unity/` never arrives here, so a line on this
  page cannot mitigate it — see Phase 7.

**Verify:** `gh api repos/deadsetbit/gaming-couch-unity-public/contents` lists `README.md` and
**no** `package.json`.

## Phase 6 — Docs published from the public repo · **agent, human for Pages enablement**

The org is on Free, so a private repo cannot serve Pages. The docs **build stays in the
private repo** and only the rendered HTML is deployed to the **public** repo's `gh-pages`.

The build runs `deadsetbit/docfx-unitypackage`, a composite action that assembles a DocFX site
out of package conventions read from the **repo root**: `Documentation~/` becomes the manual,
`README.md` its index, `CHANGELOG.md` and `LICENSE.md` their own pages, `package.json` the
site title, and `**/*.cs` the API reference. It takes exactly one input, `github_token`, so
there is nothing to point at a subfolder. Two of its steps — the `docfx.json` generation and
the redirect — resolve the site's base URL from the Pages config of the repo the workflow runs
in, which stops existing the moment that repo is private.

- [ ] **Patch the fork.** `deadsetbit/docfx-unitypackage` is ours: a fork of
      `CaseyHofland/docfx-unitypackage`, one tag `v1.0.2`, untouched since 2024. Add a
      `base_url` input used by both of those steps, falling back to the existing Pages lookup
      when empty. Cut `v1.1.0` and pin the workflow to it. Patch `v1.0.2` as it stands rather
      than rebasing on upstream first. Tracked in `deadsetbit/gaming-couch` like everything
      else; clone it fresh, there is no local checkout.
- [ ] **Stage the package as the workspace root** rather than teaching the action about
      subfolders: check out into `src/`, move `src/public/package/*` (dotglob on) to the
      workspace root, remove `src/`.
- [ ] **Trigger on `unity-*` tags**, not pushes to `main`, so published docs describe the
      newest release. This replaces the `workflow_dispatch`-only stub left by Phase 1.
- [ ] **Deploy** with `peaceiris/actions-gh-pages@v3`:
      `external_repository: deadsetbit/gaming-couch-unity-public`,
      `deploy_key: ${{ secrets.GAMING_COUCH_UNITY_PUBLIC_DEPLOY_KEY }}`,
      `publish_branch: gh-pages`, `publish_dir: _site`,
      `destination_dir: <version>`; `base_url` is that same folder. **Not `keep_files`** —
      see the ordering note below. `site_root_url` is the root above it and `home_url` is
      `https://gamingcouch.com`; both are baked into every page.
- [ ] **Run the same gates as the release publish** before deploying — the identity guard and
      `check-dist-complete.py`. The action turns `README.md` and `CHANGELOG.md` into site
      pages, so without them a package the mirror workflow *refuses* to publish would have its
      contents published here as HTML instead. Gate before staging, because staging deletes
      the folder the gate scripts live in.
- [ ] Decide whether test classes belong in the public API reference. The metadata source is
      `**/*.cs` from the staged root, and `Tests/` now ships, so they will appear unless
      excluded. The action supports `Documentation~/manual/filter.yml` for exactly this.
- [ ] **Point the site root at the latest stable release.** A second deploy in the same job
      writes a root `index.html` redirecting to the release's folder, and runs only for a
      stable version — a prerelease publishes its own folder and leaves the root alone, so a
      stable reader is never sent to an alpha.
- [ ] **`keep_files` differs between the two deploys, deliberately.** The action's cleanup is
      a `git rm` run with the working directory set to `destination_dir`. For the per-version
      deploy that reaches only this release's folder, which is what stops a type deleted from
      the package keeping its API page inside that release: leave it **off**. The root deploy
      has no `destination_dir`, so the same cleanup would reach every version folder already
      published: it must be **on**.
- [ ] **Order matters:** `gh-pages` does not exist until the first deploy, and Pages cannot be
      enabled on a branch that does not exist, so Pages is enabled after it.
- [ ] **Publish the landing page (Phase 5) before the first release tag.** The mirror workflow
      pushes only a tag and creates no branch, so if a docs deploy is the first thing to create
      a branch on the public repo, `gh-pages` becomes its default branch and the repository's
      landing view is raw DocFX output. Check with
      `gh api repos/deadsetbit/gaming-couch-unity-public --jq .default_branch` afterwards.
- [ ] **Human:** enable Pages on `gaming-couch-unity-public` with `gh-pages` as the source,
      then confirm the site root redirects and a deep link such as `/<version>/api/` renders.

Every release publishes its own folder from the first deploy, and nothing overwrites it.
A release's documentation URL is frozen into an immutable tag, so a URL naming a channel
rather than a version would point a pinned consumer at a moving target for as long as that
release exists. The cost is one folder per release, including per alpha; the alternative
cannot be corrected after the fact.

Each page carries the version it documents, a link home, and the address of a version
manifest at the site root. Those three are baked into the HTML because a published page is
never rewritten; everything the banner says is read from the manifest, which is the one
mutable file and can change for pages published years earlier.

## Phase 7 — Go-private cutover · **human**

Do this **only after** Phases 1–6 are green and the mirror is serving real tags.

The DevApp and backend half is tracked in
https://github.com/deadsetbit/gaming-couch/issues/587, including the ordering trap: the DevApp
recognises the managed package through a hardcoded URL allowlist
(`devspace/devapp/src/main/localProjects/unityPackage.ts`) while the backend composes the
install URL (`resolveTarget.ts`), and the two ship separately, so the DevApp change must land
first.

**Preconditions — each must be true before the repo is flipped:**

- [ ] No `production` or `develop` floor in `componentVersionMap.json` names a tag that is
      absent from the public mirror. Both sections currently name `unity-0.1.0-alpha.3` and
      `unity-0.1.0-alpha.7`, neither of which the mirror can publish, so this means adding a
      floor for a post-restructure release and deciding what happens to the historical
      entries.
- [ ] The DevApp release carrying the public-URL allowlist is at or below the floor that
      serves the public URL, so no installed DevApp is handed a URL it cannot recognise.
- [ ] The client monorepo's `develop` to `main` promotion PR is merged and the production edge
      function is deployed.
- [ ] Repointing the consumer game repos is handled outside this plan.

Then:

- [ ] Flip `deadsetbit/gaming-couch-unity` to private (Settings → Danger Zone).
- [ ] Smoke test: a clean checkout of a consumer project resolves the package from the public
      URL with no credentials.

### The legacy docs URL

`https://deadsetbit.github.io/gaming-couch-unity/` dies at the flip — on Free, making a repo
private unpublishes its Pages site — and every tag published before the cutover carries that
URL in its `package.json`, where it cannot be fixed retroactively. A notice on the new landing
page does **not** mitigate this: requests to the old URL never reach the new repo.

Two honest options, and the plan takes the first:

- **Accept it, and make the population empty.** The preconditions above already require every
  device to be on a post-restructure release before the flip, so nobody should still be
  holding a pre-cutover tag. Say so in the `CHANGELOG` entry for the first post-cutover
  release, and treat any remaining pre-cutover install as needing an upgrade rather than a
  working docs link.
- **Preserve the URL.** The path segment is the repo name, so keeping it alive means renaming
  the source repo and creating a *public* stub at `deadsetbit/gaming-couch-unity` whose only
  job is a Pages redirect. That also captures the old git URL, which then resolves to a repo
  with no package instead of failing as "not found" — arguably clearer, but it is a second
  repo to own and a rename with its own redirect semantics. Only worth it if real traffic
  turns out to depend on the old URL.

## Developer install routes

Two routes, for two different situations.

- **Local path or symlink — the default for anyone with a clone.** Unity treats a folder in
  `Packages/` as an embedded package, and edits are live with no authentication to arrange.
  **The DevApp does not currently recognise this setup**: its embedded-package detection is a
  single hardcoded path, `Packages/com.dsb.gamingcouch/package.json`
  (`devspace/devapp/src/shared/localProject.ts`), while the host project's symlink is named
  `Packages/gaming-couch-unity`. So the DevApp falls through to `manifest.json`, finds the
  shadowed git dependency, ranks it older than the target and offers to "update" a project
  that is actually running off the symlink. Fixing that means either renaming the symlink to
  `com.dsb.gamingcouch` or widening the detection — small, but real work, and it belongs with
  the DevApp changes in https://github.com/deadsetbit/gaming-couch/issues/587.
  After Phase 1 the symlink points at `…/gaming-couch-unity/public/package`.
- **`?path=` against the private repo — for a pinned tag without a clone:** the repo's git URL
  with `?path=/public/package` before the `#unity-<version>` revision. This needs git to
  authenticate **without prompting**, because UPM runs it with no terminal attached: HTTPS
  with a credential helper (`gh auth setup-git`) or SSH with a passphrase-less key the
  Editor's environment can see. Never put a token in the URL — `manifest.json` is committed.

A project installed from the private `?path=` URL will be rewritten to the public URL by the
DevApp's update action: `getDependencyGitUrl` splits only on `#`, so the query stays attached,
the URL misses the allowlist, and an unrecognised install maps to the update action. That is
acceptable behaviour to document rather than engineer around, but the parser fix belongs in
issue 587 if the `?path=` route is to be recommended.

## Phase 8 — Promotion, channels, deletion · **human**

**Test / edge release:** cut a prerelease tag, the mirror publishes it (gated), and it is
available but inert. Add or edit a `{fromDevApp, unity}` floor in the **`develop`** section of
`componentVersionMap.json`, bump `supportedUpTo` if the validating DevApp version moved, and
deploy the develop edge function. Dev devices get it; production is untouched. The repo's
release gate (`checkReleaseCoverage.ts`) checks both that the map resolves and that the
deployed endpoint agrees. `componentVersionMap.md` is the full procedure.

**Promote to production:** verify in the DevApp, then the manual `develop` to `main` pull
request plus a production deploy. This is the single promotion gate.

**Throwaway tags are cheap:** publish a version, find it broken, unpublish it, cut the next
one. No production history is touched, and a published tag is never re-pointed.

**Routine unpublish:** run `unpublish-tag.yml` (Phase 4) with the tag. The orphan commit
becomes unreachable and GC-eligible; no other release is affected.

**Leaked secret — deletion is not erasure on a public host.** Deleting the tag reduces
exposure but does not guarantee removal: GitHub keeps unreachable commits reachable by SHA
until its own GC, forks and archive or search caches persist, and anyone who cloned still has
it. **The only real remediation for a leaked secret is to rotate it.** The `public/package/`
boundary and the Phase 3 secret scan are the defense; deletion is cleanup, not a safety net.

## Cost

- The mirror workflow runs in the **private** repo, so it draws private-repo Actions minutes.
  `ubuntu-latest` is a 1x multiplier; avoid macOS (10x) and Windows (2x).
- **Per release:** shallow checkout, static gate, copy, commit, tag, publish — roughly 1–2
  billed minutes, only on a `unity-*` tag push. Single-digit minutes a month against the
  2,000 the org's Free plan provides.
- **Tag-only by construction:** each run publishes only the tag just pushed. Existing releases
  are never re-enumerated or re-uploaded.
- **The only real cost lever** is the optional in-CI Unity compile gate: long builds, often
  macOS. The default static gate keeps cost negligible.
- **Side effect of going private:** the docs build is free today on a public repo and starts
  billing private minutes after the flip. Infrequent, still small.

## Rollback

Every phase before Phase 7 is reversible and invisible to consumers: the public repo is
additive and the source is still public. The point of no return is flipping the source repo
private — and even that reverses by flipping it back, provided consumers have not yet been
repointed. Sequence Phase 7 exactly as written so a rollback never breaks an installed game.

## Decisions still open

- **Compile gate depth:** static-only (Phase 3) now; GameCI or a local `unity test` gate later.
- **Whether the install URL moves into the component-version map** instead of being a code
  constant, so a future repoint is config rather than a DevApp release plus a backend deploy.
  Tracked with the rest of the monorepo side in
  https://github.com/deadsetbit/gaming-couch/issues/587.
