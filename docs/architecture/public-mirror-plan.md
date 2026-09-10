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

- [x] Public repo `deadsetbit/gaming-couch-unity-public` created (public, empty).
- [x] Landing page written for its `main` (Phase 5).
- [ ] Deploy key pair generated; public half added to the public repo **with write access**.
- [ ] Private half stored as secret `GAMING_COUCH_UNITY_PUBLIC_DEPLOY_KEY` on
      `deadsetbit/gaming-couch-unity`.

Everything from Phase 1 on is unstarted.

## Facts this plan builds on

- This repo **is** the UPM package `com.dsb.gamingcouch` — `package.json` at the root, plus
  `Runtime/`, `Editor/`, `Tests/`, `Plugins/`, `Documentation~/`, `ContractFixtures/`.
- Consumers install **UPM-via-git-tag**: `…/gaming-couch-unity.git#unity-<version>`.
- Releases are lightweight `unity-<version>` tags cut by `Tools/bump-version.py`, which also
  re-bakes `Runtime/Resources/GamingCouchRuntimeInfo.json` and verifies it with
  `Tools/check-runtime-package-info.py`.
- **Promotion to end users is already manual and separate from publishing.** What a device
  installs is the exact `unity-<tag>` string that the `devapp-component-versions` Supabase
  edge function serves for its environment, read from
  `backend/supabase/functions/devapp-component-versions/componentVersionMap.json` in the
  client monorepo. `develop` and `production` are distinct sections; production is promoted
  by a manual `develop` to `main` pull request.
- The org already runs this pattern once: the client's `.github/workflows/sync-public.yml`
  mirrors its `public/dist/` folder to `deadsetbit/gaming-couch-public` via a scoped SSH
  deploy key.

## Target architecture

```
PRIVATE  deadsetbit/gaming-couch-unity            PUBLIC  deadsetbit/gaming-couch-unity-public
──────────────────────────────────────            ─────────────────────────────────────────────
public/package/         <- the package            (contents of public/package/ become the ROOT)
  package.json                                     tag unity-0.1.0-alpha.8  (orphan snapshot)
  Runtime/ Editor/ Tests/ Plugins/                 tag unity-0.1.0-alpha.9  (orphan snapshot)
  ContractFixtures/ Documentation~/                gh-pages  = the docs site
  README CHANGELOG LICENSE                         main      = hand-written landing page
public/AGENTS.md        <- the rule, unpublished
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
   publish is inert until someone points the map at it.
4. **Tags are immutable.** Never move or reuse a published tag — UPM caches by ref. A broken
   release is fixed by cutting the next version. Republishing a tag means deleting it first;
   the workflow builds a fresh commit each run, so publishing over a live tag is rejected.
5. **Nobody installs a moving ref.** Public `main` is a landing page and carries no package
   manifest, so a URL without a tag fails loudly instead of silently tracking whatever
   shipped last. "Latest" is whatever the map's `production` section names; "latest edge" is
   its `develop` section.

## Naming / constants

| Thing | Value |
|---|---|
| Public repo (SSH) | `git@github.com:deadsetbit/gaming-couch-unity-public.git` |
| Public repo (docs/consume URL) | `https://github.com/deadsetbit/gaming-couch-unity-public` |
| Package folder in private repo | `public/package/` |
| Tag format | `unity-<version>` (unchanged) |
| Actions secret (private repo) | `GAMING_COUCH_UNITY_PUBLIC_DEPLOY_KEY` |
| Bot identity | `gaming-couch-bot <bot@deadsetbit.com>` |
| Docs URL (after Phase 6) | `https://deadsetbit.github.io/gaming-couch-unity-public/` |

---

## Phase 0 — Credentials

The public repo already exists. What remains is the deploy key, and only a human can do it.

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
4. **Delete the local key files.**

One key serves both the release mirror (Phase 4) and the docs deploy (Phase 6).

## Phase 1 — Restructure the private repo into `public/package/`

A repo-wide move commit. Every open branch conflicts with it, so land or abandon what matters
first, then do the move as one isolated commit.

- [ ] Move the package into `public/package/`: `package.json`, `Runtime/`, `Editor/`,
      `Tests/`, `Plugins/`, `Documentation~/`, `ContractFixtures/`, `README.md`,
      `CHANGELOG.md`, `LICENSE.md` and their `.meta` files.
- [ ] **`ContractFixtures/` moves inside the package**, not outside it.
      `Tests/Editor/GCDevJsonContractFixtureTests.cs` resolves its corpus from the package
      root, so leaving the fixtures behind gives every consumer six failing tests. There is
      nothing private about them, and they are the clearest public description of the
      `gc.dev.json` contract.
- [ ] Leave outside `public/package/` (private): `docs/`, `Tools/`, `.claude/`, `.cursor/`,
      `.github/`, `CONTEXT.md`, `AGENTS*.md`, `VERSIONING_PLAN.md`, `.vscode/`.
- [ ] Add `public/AGENTS.md` (and the `CLAUDE.md` beside it) stating the rule: everything
      under `public/package/` is published on release; write paths and links relative to the
      package root, because the `public/package/` prefix does not exist in the public repo.
      Files in `public/` itself are not published.
- [ ] **Split the manifest.** The shipped `public/package/package.json` becomes a pure Unity
      manifest (name, version, unity, dependencies, keywords, author). The `release:*` npm
      scripts move to a **new private root `package.json`** that never ships.
- [ ] In `public/package/package.json`, repoint the URLs at the public repo:
      `documentationUrl` to `https://deadsetbit.github.io/gaming-couch-unity-public/`, and
      `changelogUrl`/`licensesUrl` into the public repo.
- [ ] **Fix the shipped README's links.** It links five times into
      `…/gaming-couch-unity/blob/main/docs/…` and `CONTEXT.md`, all of which 404 for a
      consumer once the source is private. Move `docs/contracts/platform-runtime-contract.md`
      and `docs/contracts/devapp-local-play-contract.md` into
      `public/package/Documentation~/` and link them there; drop the links to the ADRs, the
      backlog and `CONTEXT.md`.
- [ ] `Tests/` keeps shipping. It is Apache-2.0 code that is public today, and shipping it
      keeps the package we test byte-identical to the package consumers install.
- [ ] Update the host Unity project: `Packages/gaming-couch-unity` is a symlink to the repo
      root and must point at `…/gaming-couch-unity/public/package`. Any project using a
      `file:` dependency moves the same way.
- [ ] Update the paths named in `AGENTS.md` and under `docs/` that assume the package sits at
      the repo root.

## Phase 2 — Retool the scripts for the new path

- [ ] `Tools/bump-version.py`: rebase `PACKAGE_JSON_PATH` and `BAKED_RUNTIME_INFO_PATH` under
      `public/package/`, and fix the two paths in the final commit's pathspec.
      `CHECK_SCRIPT_PATH` stays under `Tools/`.
- [ ] `Tools/check-runtime-package-info.py`: introduce
      `PACKAGE_DIR = ROOT_DIR / "public" / "package"` and rebase `PACKAGE_JSON_PATH`,
      `RUNTIME_DIR`, `RUNTIME_INFO_PATH`, `BAKED_RUNTIME_INFO_PATH`, `WEBGL_BOOTSTRAP_PATH`,
      `WEBGL_BRIDGE_PATH`, `PACKAGE_CODE_DIRS`, and the literal `Runtime/GamingCouch.cs` path
      near the end of the file.
- [ ] Delete `.releaserc.json`. It targets `Development-v${version}` tags and semantic-release
      was never wired in; fixing its asset paths would preserve a file nothing runs.
- [ ] Run `npm run release:dry` and confirm the bump and check tooling operate on
      `public/package/` end to end.

## Phase 3 — Publish gate (`Tools/check-dist-complete.py`)

A static, no-Unity-license gate that fails the publish if `public/package/` is not a
self-contained package. Because the boundary is a folder, the residual risk is a reference
that points outside it.

- [ ] `package.json` present, valid JSON, `name == com.dsb.gamingcouch`, and
      `version == <tag without the "unity-" prefix>`.
- [ ] **Meta pairing, with an explicit exclusion rule.** Every asset has a sibling `.meta` and
      every `.meta` has its asset — except paths with a segment ending in `~` (Unity hides
      them, so `Documentation~` has no metas by design), the package's own top-level folders
      (which carry no committed folder `.meta`), and `.DS_Store`. Without those exclusions the
      gate fails against a correct tree.
- [ ] Every `.asmdef` parses and its references resolve within `public/package/`.
- [ ] `ContractFixtures/LocalPlay` is present. The asmdef check cannot see it — the shipped
      tests reach it by string path — so it needs its own assertion.
- [ ] Secret scan (for example `gitleaks`) over `public/package/` as the leak backstop.
- [ ] Wire it into the mirror workflow as a hard gate before any publish, alongside
      `Tools/check-runtime-package-info.py`. `bump-version.py` enforces the baked-identity
      invariant, but a hand-cut tag bypasses it.
- [ ] *(Optional, later)* A real compile gate via GameCI (`game-ci/unity-test-runner`, needs a
      Unity license secret), or a local `unity test` run invoked before the tag goes up. The
      static gate covers the common failure; the compile gate is the gold standard.

## Phase 4 — Mirror workflow (tag-triggered orphan snapshot)

New workflow in the **private** repo, `.github/workflows/publish-mirror.yml`. Runs on
`unity-*` tag push, and manually for a republish. It builds an orphan snapshot of
`public/package/` at the tag and publishes **only that tag**. It never touches the public
repo's `main`.

Shape of the job:

- Resolve the tag from either the trigger or the `workflow_dispatch` input.
- `actions/checkout@v4` at that tag, `fetch-depth: 1`, `permissions: contents: read` — the
  publish authenticates with the deploy key, not `GITHUB_TOKEN`.
- Run `Tools/check-runtime-package-info.py`, then
  `Tools/check-dist-complete.py public/package <tag>`. Both are hard gates.
- Install the deploy key into `~/.ssh` and set `GIT_SSH_COMMAND` with `IdentitiesOnly=yes`.
- Copy `public/package/.` into a temp directory, initialise a repo there on an orphan branch,
  commit as `gaming-couch-bot <bot@deadsetbit.com>` with the message `gaming-couch <version>`,
  tag it `unity-<version>`, add the public repo as a remote, and publish that one tag ref.
- `concurrency: { group: publish-mirror, cancel-in-progress: false }` so two releases never
  race.

Two things the workflow cannot do, by construction:

- **Republish over a live tag.** Every run builds a fresh orphan commit with a new SHA, so
  publishing an existing tag is rejected as a non-fast-forward. A republish means deleting the
  tag from the public repo first (Phase 8), which is the same operation as an unpublish and
  keeps principle 4 honest.
- **Publish a tag cut before Phase 1.** Checking out an older tag yields a tree with no
  `public/package/` and no gate script. The first exercise of this workflow is therefore a
  throwaway tag cut after the restructure, not a replay of history.

- [ ] Add the workflow.
- [ ] Cut a throwaway tag (`unity-0.0.0-test.1`), let it publish, and confirm the public repo
      shows the package at its root and that the tag installs into a scratch Unity project.
- [ ] Delete the throwaway tag (Phase 8) and confirm the release is gone while `main` and the
      docs are untouched.

## Phase 5 — The public repo's landing page

`main` on the public repo is a hand-written page, not a snapshot. It welcomes a browser,
explains what the package is, and sends them to the tags. It deliberately carries **no**
`package.json`, so the repo's git URL with no `#tag` fails to resolve instead of installing a
moving version.

- [x] Write the landing page and publish it as the public repo's first commit.
- [ ] Keep version numbers out of it, so it does not drift.
- The mirror workflow never touches `main`. If the page is ever worth generating per release,
  that is an addition, not a requirement.

## Phase 6 — Docs published from the public repo

A private repo cannot serve public GitHub Pages on Free or Pro, so the docs **build stays in
the private repo** and only the rendered HTML is deployed to the **public** repo's
`gh-pages`.

The build runs `deadsetbit/docfx-unitypackage`, a composite action that assembles a DocFX site
out of package conventions read from the **repo root**: `Documentation~/` becomes the manual,
`README.md` its index, `CHANGELOG.md` and `LICENSE.md` their own pages, `package.json` the
site title, and `**/*.cs` the API reference. It takes exactly one input, `github_token`, so
there is nothing to point at a subfolder. Two of its steps also resolve the site's base URL
from the Pages config of the repo the workflow runs in, which stops existing the moment that
repo is private.

- [ ] **Patch the fork.** `deadsetbit/docfx-unitypackage` is ours (a fork of
      `CaseyHofland/docfx-unitypackage`, one tag, untouched since 2024). Add a `base_url`
      input used by the `docfx.json` generation and the redirect step, falling back to the
      existing Pages lookup when it is empty. Cut `v1.1.0`. Patch `v1.0.2` as it stands rather
      than rebasing on upstream first.
- [ ] **Stage the package as the workspace root** instead of teaching the action about
      subfolders: check out into a `src/` path, move `src/public/package/*` (dotglob on) up to
      the workspace root, remove `src/`. Every root-relative assumption then holds, and the C#
      crawl covers only the package.
- [ ] **Trigger on `unity-*` tags**, not on pushes to `main`, so the published docs describe
      the newest release rather than unreleased work.
- [ ] **Deploy to the public repo** with `peaceiris/actions-gh-pages@v3`:
      `external_repository: deadsetbit/gaming-couch-unity-public`,
      `deploy_key: ${{ secrets.GAMING_COUCH_UNITY_PUBLIC_DEPLOY_KEY }}`,
      `publish_branch: gh-pages`, `publish_dir: _site`, `destination_dir: latest`,
      `keep_files: true` — and `base_url` set to the `latest/` URL.
- [ ] Place a one-line `index.html` at the `gh-pages` root redirecting to `latest/`, by hand.
      It never changes, so it does not need generating.
- [ ] Enable Pages on `gaming-couch-unity-public` with `gh-pages` as the source, and confirm
      `documentationUrl` in the shipped manifest matches the site root.

Publishing into `latest/` from the first deploy is what keeps versioned docs additive later:
the URL scheme is already versioned-shaped, so archives can be added without breaking a
bookmark. **Versioned archives are deferred to the beta transition** — during `0.x` a folder
per alpha is churn, and each archived version wants its own build because the base URL is
absolute, so a release would build the docs twice.

## Phase 7 — Go-private cutover

Do this **only after** Phases 1–6 are green and the mirror is serving real tags.

The DevApp and backend half of this cutover is tracked separately in
https://github.com/deadsetbit/gaming-couch/issues/587, including the ordering trap: the DevApp
recognises the managed package by a hardcoded URL allowlist while the backend composes the
install URL, and the two ship separately, so the DevApp change must land first.

- [ ] Repointing the consumer game repos is handled outside this plan.
- [ ] Cut a fresh release once the mirror is publishing, and point the component-version map
      at it. The mirror can only publish tags cut after Phase 1, so the public repo starts
      with no history — a current release is what consumers move to.
- [ ] **Only now** flip `deadsetbit/gaming-couch-unity` to private (Settings → Danger Zone).
- [ ] Smoke test: a clean checkout of a consumer project resolves the package from the public
      URL with no credentials.

`https://deadsetbit.github.io/gaming-couch-unity/` dies at the flip, and the already-published
tags carry that URL in their `package.json`. A published tag cannot be fixed retroactively; a
line on the landing page is the mitigation.

## Developer install routes

Two routes, for two different situations. Both are recognised by the DevApp.

- **Local path or symlink — the default for anyone with a clone.** Unity treats a folder in
  `Packages/` as an embedded package, and the DevApp reads the version straight out of the
  embedded `package.json` and version-compares it. Edits are live, and there is no
  authentication to arrange. After Phase 1 the symlink points at
  `…/gaming-couch-unity/public/package`.
- **`?path=` against the private repo — for a pinned tag without a clone:** the repo's git URL
  with `?path=/public/package` before the `#unity-<version>` revision. This needs git to
  authenticate **without prompting**, because UPM runs it with no terminal attached: either
  HTTPS with a credential helper (`gh auth setup-git`) or SSH with a passphrase-less key the
  Editor's environment can see. Never put a token in the URL — `manifest.json` is committed.

A project installed from the private `?path=` URL will be rewritten to the public URL by the
DevApp's update action, because the managed target is composed from the public URL. That is
the intended behaviour; document it rather than engineering around it.

## Phase 8 — Promotion, channels, deletion

**Test / edge release:** cut a prerelease tag, the mirror publishes it (gated), and it is
available but inert. Point the **`develop`** section of `componentVersionMap.json` at it and
deploy the develop function, and dev devices get it; production is untouched.

**Promote to production:** verify in the DevApp, then the manual `develop` to `main` pull
request. This is the single promotion gate.

**Throwaway tags are cheap:** publish a version, find it broken, delete its tag, cut the next
one. No production history is touched, and a published tag is never re-pointed.

**Routine unpublish:** delete the release tag from the public mirror with the deploy key. The
orphan commit becomes unreachable and GC-eligible; no other release is affected.

**Leaked secret — deletion is not erasure on a public host.** Deleting the tag reduces
exposure but does not guarantee removal: GitHub keeps unreachable commits reachable by SHA
until its own GC, forks and archive or search caches persist, and anyone who cloned still has
it. **The only real remediation for a leaked secret is to rotate it.** The `public/package/`
boundary and the Phase 3 secret scan are the defense; deletion is cleanup, not a safety net.

## Cost

- The mirror workflow runs in the **private** repo, so it draws private-repo Actions minutes.
  `ubuntu-latest` is a 1x multiplier; avoid macOS (10x) and Windows (2x).
- **Per release:** shallow checkout, static gate, copy, commit, tag, publish — roughly 1–2
  billed minutes, only on a `unity-*` tag push. Single-digit minutes a month against 2,000
  (Free) or 3,000 (Pro/Team).
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
- **Docs versioning timing:** `latest/` only at cutover; per-version archives and a selector
  around the beta transition.
- **Whether the install URL moves into the component-version map** instead of being a code
  constant, so a future repoint is config rather than a DevApp release plus a backend deploy.
  Tracked with the rest of the monorepo side in
  https://github.com/deadsetbit/gaming-couch/issues/587.
