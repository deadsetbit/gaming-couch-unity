# Public Mirror Plan — private source, public releases

Status: **planned, not started.** Execute the phases in order. Phase 0 is the only
thing to do today; everything else waits until the active feature branches are merged
(see the timing gotcha in Phase 1).

## Goal

Make `deadsetbit/gaming-couch-unity` **private** (all ADRs, reviews, agent notes, WIP
branches, dev tooling stay internal) and publish each release to a separate **public**
repo that consumers install from:

- **Public repo:** `https://github.com/deadsetbit/gaming-couch-unity-public`
  (follows the shape of the existing `gaming-couch-public`: generated, read-only,
  package contents at the repo root).
- **Keep release history** as an immutable, browsable set of `unity-<version>` tags —
  unlike the client mirror, which force-pushes a single squashed snapshot.
- **Be able to delete a specific release outright** if something slips in by accident.

## Current state (facts this plan builds on)

- This repo **is** the UPM package `com.dsb.gamingcouch` — `package.json` at the root,
  plus `Runtime/`, `Editor/`, `Tests/`, `Plugins/`, `Documentation~/`.
- Consumers install **UPM-via-git-tag**: `…/gaming-couch-unity.git#unity-0.1.0-alpha.6`.
- Releases are lightweight `unity-<version>` tags cut by `Tools/bump-version.py`, which
  also re-bakes `Runtime/Resources/GamingCouchRuntimeInfo.json` and verifies it with
  `Tools/check-runtime-package-info.py`.
- **Promotion to end users is already manual and separate from publishing.** What a
  device installs is the exact `unity-<tag>` string that the `devapp-component-versions`
  Supabase edge function serves for its environment, read from
  `backend/supabase/functions/devapp-component-versions/componentVersionMap.json` in the
  client monorepo. `develop` and `production` are distinct sections; production is
  promoted by a manual `develop → main` PR. `Tools/bump-version.py` can hand off to
  `devspace/devapp/scripts/prepare-devapp-release.sh` to edit that map.
- The org already runs this pattern once: the client's `.github/workflows/sync-public.yml`
  mirrors `public/dist/` to `deadsetbit/gaming-couch-public` via a scoped SSH deploy key.

## Target architecture

```
PRIVATE  deadsetbit/gaming-couch-unity            PUBLIC  deadsetbit/gaming-couch-unity-public
──────────────────────────────────────            ─────────────────────────────────────────────
public/dist/            <- the package            (contents of public/dist/ become the repo ROOT)
  package.json                                     tag unity-0.1.0-alpha.6  (orphan snapshot)
  Runtime/ Editor/ Tests/ Plugins/                 tag unity-0.1.0-alpha.5  (orphan snapshot)
  Documentation~/ README CHANGELOG LICENSE         tag unity-0.1.0-alpha.4  (orphan snapshot)
                                    │  tag push     main = latest snapshot (landing page only)
docs/ Tools/ .claude/ .github/  ───┼──────────▶
CONTEXT.md AGENTS*.md ...           │  CI: build orphan snapshot of public/dist@tag,
package.json (dev scripts only)     │      push ONLY that tag with a scoped deploy key
  (never leaves the private repo)   ┘
```

**Principles:**

1. **The boundary is the filesystem, not a list.** If a file is under `public/dist/`, it
   ships; if not, it can't. No allowlist to forget to update (kills failure mode A).
2. **One release = one orphan snapshot commit + one tag.** Releases share no ancestry, so
   deleting a version's tag makes its commit unreachable **without touching any other
   release**. The unit of deletion is the tag, and it's independently deletable.
3. **Publish ≠ promote.** Pushing a tag to the public repo only makes it *available*. The
   DevApp component-version map (manual, per environment) is the sole thing that puts a
   version in front of end users. A broken publish is inert until someone points the map
   at it.
4. **Tags are immutable.** Never move or reuse a published tag — UPM caches by ref. A
   broken release is fixed by cutting the next version, never in place.
5. **End users never track a moving ref.** "Latest" is whatever the DevApp map's
   `production` section names; "latest edge" is its `develop` section. No `latest` git
   branch is exposed for installation.

## Naming / constants

| Thing | Value |
|---|---|
| Public repo (SSH) | `git@github.com:deadsetbit/gaming-couch-unity-public.git` |
| Public repo (docs/consume URL) | `https://github.com/deadsetbit/gaming-couch-unity-public` |
| Package folder in private repo | `public/dist/` |
| Tag format | `unity-<version>` (unchanged) |
| Actions secret (private repo) | `GAMING_COUCH_UNITY_PUBLIC_DEPLOY_KEY` |
| Bot identity | `gaming-couch-bot <bot@deadsetbit.com>` |
| Docs Pages URL (after move) | `https://deadsetbit.github.io/gaming-couch-unity-public/` |

---

## Phase 0 — Groundwork (do today, manual; see the checklist at the bottom)

Create the empty public repo and the scoped deploy key. Zero risk, unblocks everything,
touches no code. **Full step-by-step is in the "Today's manual GitHub task" appendix.**

- [ ] Public repo `deadsetbit/gaming-couch-unity-public` created (empty, public).
- [ ] Deploy key pair generated; public half added to the public repo **with write access**.
- [ ] Private half stored as secret `GAMING_COUCH_UNITY_PUBLIC_DEPLOY_KEY` on
      `deadsetbit/gaming-couch-unity`.
- [ ] Local key files deleted.

## Phase 1 — Restructure the private repo into `public/dist/`

**Timing gotcha — do this on a quiet tree.** This is a repo-wide move commit; every open
branch (`feature/standalone-controller`, `feature/portable-state-kernel`, the `codex/*`
and `prototype-*` branches, `point-hud`, …) will conflict on it. Land the branches you
care about first, then do the move as one isolated commit, then rebase or abandon the
stragglers deliberately.

- [ ] `git mv` the package into `public/dist/`: `package.json`, `Runtime/`, `Editor/`,
      `Tests/`, `Plugins/`, `Documentation~/`, `README.md`, `CHANGELOG.md`, `LICENSE.md`
      and their `.meta` files. (`.meta` files travel with their assets.)
- [ ] Leave outside `public/dist/` (private): `docs/`, `Tools/`, `.claude/`, `.cursor/`,
      `.github/`, `.releaserc.json`, `CONTEXT.md`, `AGENTS*.md`, `VERSIONING_PLAN.md`,
      `ContractFixtures/`, `.vscode/`.
- [ ] **Split the manifest.** The shipped `public/dist/package.json` becomes a *pure Unity
      manifest* (name, version, unity, dependencies, keywords, author). Move the `release:*`
      npm `scripts` into a **new private root `package.json`** that never ships.
- [ ] In `public/dist/package.json`, repoint the URLs to the public repo:
      `documentationUrl` → `https://deadsetbit.github.io/gaming-couch-unity-public/`,
      `changelogUrl`/`licensesUrl` → `…/gaming-couch-unity-public/blob/main/…`.
- [ ] Decide whether `Tests/` ships (keeping it under `public/dist/Tests/` preserves
      today's behavior; move it out only if tests must stay private — then they can't be
      package-embedded tests). **Default: keep it shipped.**
- [ ] Update any host/test Unity project that references this package by local path:
      `file:../gaming-couch-unity` → `file:../gaming-couch-unity/public/dist`.

## Phase 2 — Retool the scripts for the new path

All the affected paths funnel through a single `ROOT_DIR`, so this is a handful of edits.

- [ ] `Tools/bump-version.py`: rebase `PACKAGE_JSON_PATH` and `BAKED_RUNTIME_INFO_PATH`
      under `public/dist/`, and fix the two paths in the final `git commit … --` call
      (`public/dist/package.json`, `public/dist/Runtime/Resources/GamingCouchRuntimeInfo.json`).
      Keep `CHECK_SCRIPT_PATH` → `Tools/` (stays private).
- [ ] `Tools/check-runtime-package-info.py`: introduce `PACKAGE_DIR = ROOT_DIR / "public" / "dist"`
      and rebase `PACKAGE_JSON_PATH`, `RUNTIME_DIR`, `RUNTIME_INFO_PATH`, `WEBGL_BRIDGE_PATH`.
      (Display strings like `"Runtime/GamingCouch.cs"` are cosmetic — update for clarity, not correctness.)
- [ ] `.releaserc.json`: `assets` → `["public/dist/package.json", "public/dist/CHANGELOG.md"]`
      (only relevant if semantic-release is ever wired in; still fix it).
- [ ] Run `npm run release:dry` and confirm the bump/check tooling operates on
      `public/dist/` end to end.

## Phase 3 — Publish gate (`Tools/check-dist-complete.py`)

A static, no-Unity-license gate that fails the publish if `public/dist/` isn't a
self-contained package. Because the boundary is now a folder, the residual risk is a
*reference that points outside the folder*, which this catches.

- [ ] `package.json` present, valid JSON, `name == com.dsb.gamingcouch`, and
      `version == <tag without the "unity-" prefix>`.
- [ ] **Meta pairing:** every asset under `public/dist/` has a sibling `.meta`, and every
      `.meta` has its asset. (Missing `.meta` is the classic dropped-file symptom.)
- [ ] Every `.asmdef` parses and its references resolve within `public/dist/`.
- [ ] Secret scan (e.g. `gitleaks`) over `public/dist/` as the leak backstop.
- [ ] Wire it into the mirror workflow (Phase 4) as a hard gate before any push.
- [ ] *(Optional, later)* Upgrade to a real compile gate via GameCI
      (`game-ci/unity-test-runner`, needs a Unity license secret) or a local Unity smoke
      test invoked from `bump-version.py` before it pushes the tag. The static gate covers
      the common failure; the compile gate is the gold standard.

## Phase 4 — Mirror workflow (tag-triggered orphan snapshot)

New workflow in the **private** repo, `.github/workflows/publish-mirror.yml`. Runs on
`unity-*` tag push (and manually for re-publish). Builds an orphan snapshot of
`public/dist/` at the tag and pushes **only that tag** to the public repo. Also
force-updates the public `main` to the latest snapshot as a browsable landing page (no one
installs from `main`).

```yaml
name: Publish release to public mirror

on:
  push:
    tags: ["unity-*"]
  workflow_dispatch:
    inputs:
      tag: { description: "Existing unity-* tag to (re)publish", required: true }

permissions:
  contents: read          # only read THIS repo; the push authenticates with the deploy key
concurrency:
  group: publish-mirror
  cancel-in-progress: false

jobs:
  publish:
    runs-on: ubuntu-latest
    steps:
      - id: tag
        run: |
          if [ "${{ github.event_name }}" = "workflow_dispatch" ]; then
            echo "name=${{ inputs.tag }}" >> "$GITHUB_OUTPUT"
          else
            echo "name=${GITHUB_REF#refs/tags/}" >> "$GITHUB_OUTPUT"
          fi
      - uses: actions/checkout@v4
        with: { ref: "${{ steps.tag.outputs.name }}", fetch-depth: 1 }

      - name: Completeness gate
        run: python3 Tools/check-dist-complete.py public/dist "${{ steps.tag.outputs.name }}"

      - name: Publish orphan snapshot + tag
        env:
          DEPLOY_KEY: ${{ secrets.GAMING_COUCH_UNITY_PUBLIC_DEPLOY_KEY }}
          TAG: ${{ steps.tag.outputs.name }}
        run: |
          set -euo pipefail
          mkdir -p ~/.ssh && chmod 700 ~/.ssh
          printf '%s\n' "$DEPLOY_KEY" > ~/.ssh/id_ed25519 && chmod 600 ~/.ssh/id_ed25519
          ssh-keyscan github.com >> ~/.ssh/known_hosts 2>/dev/null
          export GIT_SSH_COMMAND="ssh -i ~/.ssh/id_ed25519 -o IdentitiesOnly=yes"

          WORK="$(mktemp -d)"
          cp -a public/dist/. "$WORK/"
          cd "$WORK"
          git init -q && git checkout -q --orphan release && git add -A
          git -c user.name="gaming-couch-bot" -c user.email="bot@deadsetbit.com" \
              commit -q -m "gaming-couch ${TAG#unity-}"
          git tag "$TAG"
          git remote add public git@github.com:deadsetbit/gaming-couch-unity-public.git
          git push public "refs/tags/$TAG"          # the release (immutable)
          git push --force public "release:main"     # landing page = latest snapshot
```

- [ ] Add the workflow; dry-run it against an existing tag via `workflow_dispatch` while
      the source repo is still private-to-be but the public repo already exists.
- [ ] Confirm the public repo shows the package at its root and the tag is installable:
      `…/gaming-couch-unity-public.git#unity-<v>` in a throwaway project.
- [ ] *(Optional)* also create a GitHub Release per tag with the **prerelease** flag for
      alpha/beta, so the public repo's "Latest release" badge only ever shows stable.

## Phase 5 — Docs/Pages published from the public repo

A private repo can't serve public GitHub Pages on Free/Pro, so the docs **build stays in
the private repo** and only the rendered HTML is deployed to the **public** repo's
`gh-pages`. Two tiers: 5a is the cutover baseline; 5b is the versioned bonus (land it
around the beta transition — see the recommendation at the end).

### Phase 5a — Relocate docs to the public repo (unversioned, `/latest` only)

- [ ] In `.github/workflows/docfx-unitypackage.yml`, point the package source at
      `public/dist/` and change the deploy step (`peaceiris/actions-gh-pages`) to target the
      external public repo using the deploy key:
      ```yaml
      - uses: peaceiris/actions-gh-pages@v3
        with:
          external_repository: deadsetbit/gaming-couch-unity-public
          deploy_key: ${{ secrets.GAMING_COUCH_UNITY_PUBLIC_DEPLOY_KEY }}
          publish_branch: gh-pages
          publish_dir: _site
      ```
- [ ] Enable Pages on `gaming-couch-unity-public` (source: `gh-pages`).
- [ ] Verify `https://deadsetbit.github.io/gaming-couch-unity-public/` renders.
- [ ] Confirm `documentationUrl` in `public/dist/package.json` matches that URL.

### Phase 5b — Versioned docs + version selector (bonus)

One `gh-pages` branch, one subfolder per version, `/latest/` = newest, a root redirect,
and a `versions.json` that drives an in-page dropdown. No tooling change — DocFX stays.

```
gh-pages/
  index.html        -> redirect to /latest/
  versions.json     -> ["0.1.0-alpha.6", …] + which is "latest"
  latest/           -> full COPY of newest docs (so /latest/<anypage> always resolves)
  0.1.0-alpha.6/  0.1.0-alpha.5/  …
```

- [ ] Change the docs deploy to a **custom step** (instead of peaceiris) that: clones the
      public `gh-pages` via the deploy key, drops the freshly built `_site` into
      `pages/<version>/` and overwrites `pages/latest/`, regenerates `versions.json` +
      root `index.html`, then commits once and pushes. Cloning the real branch (not
      force-pushing) is what preserves older version folders.
- [ ] `Tools/gen-docs-index.py <pages-dir>`: lists the version folders (`sort -rV`), writes
      `versions.json` and the root redirect `index.html`.
- [ ] `Tools/inject-version-selector.py <site-dir>` (post-build): inserts one `<script>`
      into each built HTML page. The script fetches
      `/gaming-couch-unity-public/versions.json`, renders a `<select>` (current preselected,
      a "latest" entry), and on change navigates to the **same sub-path** under the chosen
      version, falling back to that version's home on 404. Absolute base path is hardcoded
      to `/gaming-couch-unity-public/`. (If `docfx-unitypackage` exposes a custom
      `docfx.json`/template, prefer injecting the dropdown as a template partial instead —
      but this post-build step works regardless of the action.)
- [ ] **Branch-growth policy (decide before enabling):** while in `0.x` alpha, do **not**
      archive every alpha — keep `/latest/` plus optionally `/next/` for the current
      prerelease line; start real per-version archiving at beta/stable. Prevents dozens of
      throwaway alpha doc-sites bloating `gh-pages`.
- [ ] **"latest" definition:** newest stable once stable exists; newest alpha while in
      `0.x`. Optionally source "latest stable" from the DevApp `production` tag.

## Phase 6 — Go-private cutover (order matters — don't flip early)

Do these **only after** Phases 1–5 are green and the public mirror is serving real tags.

- [ ] Repoint every consumer game's `Packages/manifest.json`:
      `com.dsb.gamingcouch` URL from `…/gaming-couch-unity.git#…` →
      `…/gaming-couch-unity-public.git#…`. (Batch this the way the GC game migrations are
      batched.)
- [ ] Repoint wherever the DevApp composes the install URL from the map's tag (client
      config / `componentVersionMap` base URL) to the public repo. Verify a `develop`
      install resolves from the public URL.
- [ ] Confirm `documentationUrl`/`changelogUrl`/`licensesUrl` in `public/dist/package.json`
      already point at the public repo (Phase 1).
- [ ] **Only now** flip `deadsetbit/gaming-couch-unity` to private (Settings → Danger Zone).
- [ ] Smoke test: a clean checkout of a consumer game resolves the package from the public
      URL with no credentials.

## Phase 7 — Promotion, channels, "latest", test releases (operating model)

Nothing here is new machinery — it's how to *use* the DevApp map you already have.

- **Test / edge release:** cut a prerelease tag (`unity-0.1.0-alpha.N`) → the mirror
  workflow publishes it (gated) → it's available but inert. Point the **`develop`** section
  of `componentVersionMap.json` at it via `prepare-devapp-release.sh`, deploy the develop
  function → dev/edge devices get it; **production untouched**.
- **Promote to production:** verify in the DevApp, then the manual `develop → main` PR (or
  set the `production` section's Unity tag). This is the single promotion gate.
- **"Latest":** = whatever the map's `production` section names (stable) / `develop` names
  (edge). Do **not** add a moving `latest` git branch for installs.
- **Throwaway tags are cheap:** publish `alpha.7`, find it broken, delete its tag (Phase 8),
  cut `alpha.8`. No production history is touched. Never re-point `alpha.7`.

## Phase 8 — Deletion / leak-incident runbook

**Routine unpublish (retiring or accidental release):**

```bash
# delete the release tag from the public mirror
GIT_SSH_COMMAND="ssh -i <deploy-key>" \
  git push git@github.com:deadsetbit/gaming-couch-unity-public.git --delete unity-<v>
```

The orphan commit becomes unreachable and GC-eligible; no other release is affected. If
`main` happened to point at it, the next release resets `main`.

**Leaked secret — deletion is NOT erasure on a public host.** Deleting the tag reduces
exposure but does not guarantee removal: GitHub keeps unreachable commits reachable by SHA
until its own GC (you can't trigger it — file a support request to expunge), forks and
`GH Archive`/search caches persist, and anyone who cloned still has it. **The only real
remediation for a leaked secret is to rotate/revoke it.** Treat the `public/dist/` folder
boundary + the Phase 3 secret scan as the real defense; deletion is cleanup, not a safety
net.

---

## Cost / Actions quota

- The mirror workflow runs in the **private** repo → draws from private-repo Actions
  minutes. `ubuntu-latest` = **1× multiplier** (avoid macOS = 10×, Windows = 2×).
- **Per release:** shallow checkout + static gate + copy + git commit/tag/push ≈
  **1–2 billed minutes**. It fires **only on `unity-*` tag push**, at your release cadence
  → single-digit minutes/month vs. 2,000 (Free) / 3,000 (Pro/Team). Effectively free.
- **Tag-only by construction:** each run publishes **only the tag just pushed**; existing
  releases are never re-enumerated or re-uploaded (unlike the client's mirror, which
  re-syncs the whole folder on every `main` push). The single `git push --force release:main`
  is a cosmetic landing-page pointer, not a re-publish — drop it for a strict tags-only mirror.
- **The only real cost lever** is the *optional* in-CI Unity compile gate (GameCI): long
  builds, often macOS (10×). The default static gate (Phase 3) keeps cost negligible.
- **Side effect of going private:** the existing DocFX workflow is free today (public repo)
  but will start billing against private minutes after the flip — infrequent, still small.

## Decisions still open

- **Folder name:** `public/dist/` (matches the client) vs a flatter `Package/`. Plan
  assumes `public/dist/`.
- **Ship `Tests/`?** Plan default: yes (keeps today's behavior).
- **Compile gate depth:** static-only (Phase 3) now; GameCI or local-Unity smoke test later.
- **`main` on the public repo:** latest-snapshot landing page (plan default) vs no default
  branch at all (tags only).
- **Docs versioning timing:** ship Phase 5a (unversioned) at cutover; land Phase 5b
  (versioned + selector) around the beta transition, when multiple pinned versions start to
  matter and the alpha-doc churn is behind you. **Recommended, not yet committed.**

## Rollback

Every phase before Phase 6 is reversible and invisible to consumers (the public repo is
additive; the source is still public). The point of no return is flipping the source repo
private in Phase 6 — and even that is reversible by flipping it back, *provided* consumers
haven't yet been repointed. Sequence Phase 6 exactly as written so a rollback never breaks
an installed game.

---

## Appendix — Today's manual GitHub task (Phase 0)

Mirrors the proven setup from the client's `sync-public.yml`, retargeted to this repo.
Do this from a scratch directory.

1. **Create the public repo** (empty — no README, no license, no .gitignore), **Public**
   visibility, under the org: `deadsetbit/gaming-couch-unity-public`.

2. **Generate a dedicated deploy key pair** (no passphrase):
   ```bash
   ssh-keygen -t ed25519 -C "gaming-couch-unity-public deploy key" \
     -f ./gc-unity-public-deploy-key -N ""
   # -> gc-unity-public-deploy-key  (private)  and  .pub  (public)
   ```

3. **Add the PUBLIC key to the PUBLIC repo, with write access:**
   `deadsetbit/gaming-couch-unity-public` → Settings → Deploy keys → Add deploy key →
   paste `gc-unity-public-deploy-key.pub` → **check "Allow write access"**.
   (Scoping to this repo means the key can never touch the private source.)

4. **Add the PRIVATE key to the PRIVATE repo as an Actions secret:**
   `deadsetbit/gaming-couch-unity` → Settings → Secrets and variables → Actions →
   New repository secret → Name: `GAMING_COUCH_UNITY_PUBLIC_DEPLOY_KEY` →
   Value: full contents of `gc-unity-public-deploy-key` (incl. BEGIN/END lines).

5. **Delete the local key files:**
   ```bash
   rm gc-unity-public-deploy-key gc-unity-public-deploy-key.pub
   ```

That's all for today — no code changes, nothing goes public yet. It just puts the
credential plumbing in place so Phase 4's workflow has somewhere to push.
