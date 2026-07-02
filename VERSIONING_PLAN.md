# Gaming Couch Unity Plugin - Versioning and Release Plan

## Goals
- Version the package predictably across alpha, beta, stable, and nightly builds.
- Define distribution paths for Unity Asset Store and pre-release channels.
- Keep the Asset Store listing clean while enabling early adopters.

## Current state
- Package: `com.dsb.gamingcouch` at `0.1.0-alpha.3` (`package.json`) — alpha phase.
- License: Apache-2.0 (`LICENSE.md`) — the plugin is open source.
- Minimum Unity: `6000.0` (Unity 6), per `package.json`.
- Releases so far are Git tags (`unity-0.1.0-alpha.1`, `unity-0.1.0-alpha.3`) with a hand-maintained `CHANGELOG.md`.

## Versioning scheme
- Use SemVer: `MAJOR.MINOR.PATCH`.
- Alpha: `0.y.z-alpha.N` (API can change). Example: `0.1.0-alpha.3`.
- Beta: `1.0.0-beta.N` once API is mostly stable. Example: `1.0.0-beta.2`.
- Stable: `1.0.0` and onward.
- Nightly/unstable (optional, future): `1.1.0-nightly.YYYYMMDD` (optional metadata like `+commit`).

## Runtime identity and protocol versioning
These decisions are recorded as ADRs; this plan only points to them:

- `gameProtocolVersion` stays `1` and is bumped only for a required platform/game contract change that adapters cannot translate — never for sidecar, diagnostics, package-metadata, or tooling changes. See [ADR 0008](docs/adr/0008-game-protocol-version-stays-1.md).
- WebGL export writes two sidecars: `gc.runtime-info.json` as the narrow identity gate and `gc.unity-build-info.json` as non-gating diagnostics with its own `schemaVersion`; `package.json` is the sole source of package name/version. See [ADR 0009](docs/adr/0009-two-sidecar-identity-model.md).

## Release automation
- CI: GitHub Actions is set up; `.github/workflows/docfx-unitypackage.yml` builds and publishes DocFX docs to `gh-pages` on pushes to `main`.
- semantic-release is configured in `.releaserc.json`: angular commit conventions on `main`, tag format `Development-v${version}`, changelog generation, and `package.json` version bumps committed back (no npm publish). No workflow in this repo invokes it yet, and existing tags still use the manual `unity-<version>` format — wiring semantic-release into CI (or aligning the tag format) is the remaining automation step.
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
