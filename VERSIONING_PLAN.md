# Gaming Couch Unity Plugin - Versioning and Release Plan

## Goals
- Version the package predictably across alpha, beta, stable, and nightly builds.
- Define distribution paths for Unity Asset Store and pre-release channels.
- Keep the Asset Store listing clean while enabling early adopters.

## Current state
- Package: `com.dsb.gamingcouch` — alpha phase. `public/package/package.json` is the single source of the current version. The root `package.json` is private and carries the release scripts only.
- License: Apache-2.0 (`public/package/LICENSE.md`) — the plugin is open source.
- Minimum Unity: `6000.0` (Unity 6), per `public/package/package.json`.
- Releases so far are Git tags in the `unity-<version>` form — list them with `git tag --list 'unity-*'` — with a hand-maintained `public/package/CHANGELOG.md`.

## Versioning scheme
- Use SemVer: `MAJOR.MINOR.PATCH`.
- Alpha: `0.y.z-alpha.N` (API can change). Example: `0.1.0-alpha.3`.
- Beta: `1.0.0-beta.N` once API is mostly stable. Example: `1.0.0-beta.2`.
- Stable: `1.0.0` and onward.
- Nightly/unstable (optional, future): `1.1.0-nightly.YYYYMMDD` (optional metadata like `+commit`).

## Bumping the version (protocol)
Use the `release:*` npm scripts from the repo root — the one-command flow that keeps
`public/package/package.json` (the single source of name/version) and the baked runtime info in sync:

```bash
npm run release:alpha        # bump the alpha counter: -alpha.N -> -alpha.N+1
npm run release:beta         # switch line: -> 0.1.0-beta.0
npm run release:patch        # finalize a prerelease -> 0.1.0
npm run release:minor        # from a prerelease: finalizes it, same as :patch (see below)
npm run release:major        # -> 1.0.0
npm run release:dry          # preview an alpha bump, change nothing
npm run release:prerelease -- --preid=rc   # pass extra flags after --
```

Each script just calls `Tools/bump-version.py`; you can invoke it directly for anything
not covered by a script (e.g. an explicit version):

```bash
python3 Tools/bump-version.py 0.2.0-alpha.0              # explicit version
python3 Tools/bump-version.py preminor --preid=alpha     # open the next minor line: -> 0.2.0-alpha.0
python3 Tools/bump-version.py prerelease --preid=alpha --dry-run   # same as release:dry
python3 Tools/bump-version.py --help                    # full flag reference
```

**`release:minor` does not open a new minor line while a prerelease is in progress.** These are
node-semver's keyword rules, which the bumper reproduces exactly (`bump_core` in
`Tools/bump-version.py`): from `0.1.0-alpha.N`, `minor` only *finalizes* the version already in
progress — it drops the prerelease tag and leaves `0.1.0`, identical to `release:patch` — because the
`0.1.0` core has not shipped yet. `minor` raises the minor number only from a released version, or
from a prerelease whose patch is non-zero. So mid-alpha no `release:*` script produces `0.2.0`: use
`preminor` (for `0.2.0-alpha.0`) or name the version outright, as above. Dry-run first (`--dry-run`)
and read the printed `X -> Y` line before answering the prompt — that line, not the script name, is
what you are about to commit and tag.

What it does:
1. Warns (y/N) if you are not on `main`.
2. Bumps `version` in `public/package/package.json` (npm-style keyword or explicit `X.Y.Z`), preserving the file's exact formatting.
3. Re-bakes `public/package/Runtime/Resources/GamingCouchRuntimeInfo.json` to the canonical payload for the new version.
4. Verifies with `Tools/check-runtime-package-info.py` (the baked-JSON-matches-`package.json` invariant is a hard gate; other guard findings are advisory with a y/N).
5. Commits `chore(release): <version>` and creates the lightweight `unity-<version>` tag. A tag that already exists is refused outright: published tags are immutable, and moving one would leave the public mirror serving the old commit.
6. Prompts (y/N) to push the branch + tag.
7. Prompts (y/N) to hand off to the monorepo's DevApp release helper
   (`devspace/devapp/scripts/prepare-devapp-release.sh`), which bumps the DevApp version and
   registers the new `unity-<version>` tag in the DevApp component-version map. The monorepo
   path defaults to `../gamingcouch/client` (override with `$GC_MONOREPO_DIR` or `--monorepo-dir`).

`public/package/CHANGELOG.md` is intentionally left manual — write the release notes yourself.

## Runtime identity and protocol versioning
These decisions are recorded as ADRs; this plan only points to them:

- `gameProtocolVersion` stays `1` and is bumped only for a required platform/game contract change that adapters cannot translate — never for sidecar, diagnostics, package-metadata, or tooling changes. See [ADR 0008](docs/adr/0008-game-protocol-version-stays-1.md).
- WebGL export writes two sidecars: `gc.runtime-info.json` as the narrow identity gate and `gc.unity-build-info.json` as non-gating diagnostics with its own `schemaVersion`; `package.json` is the sole source of package name/version. See [ADR 0009](docs/adr/0009-two-sidecar-identity-model.md).

## Release automation
- CI: `.github/workflows/docfx-unitypackage.yml` builds the DocFX site, and is `workflow_dispatch` only — the action reads the package from the repository root, which no longer holds one. `docs/architecture/public-mirror-plan.md` rebuilds it to stage `public/package/` and deploy to the public repo.
- Version bumps are manual via `Tools/bump-version.py` (see "Bumping the version" above), which produces `unity-<version>` tags.
- Pushing a `unity-<version>` tag runs `.github/workflows/publish-mirror.yml`, which gates the package and publishes that one tag to `deadsetbit/gaming-couch-unity-public` as an orphan snapshot of `public/package/`. Publishing is not promoting: a version becomes installable, and nothing serves it to anyone until the DevApp component-version map names it.
- Published tags are immutable. Re-running the publish for a tag already on the mirror is rejected as a non-fast-forward, which is the guardrail working. To replace a release, run `.github/workflows/unpublish-tag.yml` for that tag first — it is a workflow rather than a local command because the deploy key exists only as an Actions secret.
- `unity-<version>` is the only tag scheme. semantic-release is not wired in.
- No nightly workflow exists yet; if nightlies are added, CI should build, version, and tag them as clearly labeled "unstable".

## Release channels
- Stable: tagged releases only.
- Pre-release: alpha/beta tags for testers, hosted on GitHub and installed via UPM Git URL.
- Nightly: automated builds for early adopters; no support SLA (not yet set up).

## Distribution paths
- Unity Asset Store (UAS): stable releases only.
- UPM via Git tag (GitHub, current path) or a registry such as OpenUPM (optional later): alpha/beta/nightly.
- GitHub Releases: alpha/beta/nightly artifacts (optional, but convenient).

## Unity Asset Store strategy
- Use a single Asset Store listing.
- Publish stable versions only as the "Latest version".
- Avoid separate alpha/beta listings to prevent fragmentation.
- Provide links in the listing/docs to pre-release channels with clear disclaimers.

## Rollout plan
1) Alpha phase (current)
   - Keep `0.x` versions.
   - Distribute via Git tags + UPM Git install.
   - Collect feedback and allow breaking changes.

2) Beta phase
   - Cut `1.0.0-beta.N` releases.
   - Maintain a changelog and migration notes.
   - Prepare Asset Store submission assets.

3) Stable release
   - Release `1.0.0`.
   - Submit to Asset Store; only stable versions there.
   - Keep alpha/beta on UPM/GitHub.
