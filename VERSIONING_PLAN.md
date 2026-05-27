# Gaming Couch Unity Plugin - Versioning and Release Plan

## Goals
- Plan a versioning strategy that covers alpha, beta, stable, and nightly builds.
- Define distribution paths for Unity Asset Store and pre-release channels.
- Keep the Asset Store listing clean while enabling early adopters.

## Versioning scheme
- Use SemVer: `MAJOR.MINOR.PATCH`.
- Alpha: `0.y.z-alpha.N` (API can change). Example: `0.9.0-alpha.3`.
- Beta: `1.0.0-beta.N` once API is mostly stable. Example: `1.0.0-beta.2`.
- Stable: `1.0.0` and onward.
- Nightly/unstable: `1.1.0-nightly.YYYYMMDD` (optional metadata like `+commit`).
  - Example: `1.1.0-nightly.20240205+abc123`.

## Runtime identity and upload gates
- `package.json` is the source of truth for the Unity package name and package version.
- `gameProtocolVersion` is the Gaming Couch game integration contract version. Bump it only for a required platform/game contract change, not for diagnostics, package metadata, editor tooling, or upload validation changes.
- Editor code that needs package identity should read package metadata through one shared helper, using Unity package metadata or package-root `package.json`.
- WebGL export should write a build artifact identity sidecar named `gc.runtime-info.json` at the export root, next to `index.html`.
- The sidecar should contain the same Unity identity fields used by DevApp registration and WebGL build diagnostics. The package name and version values are illustrative here; generated sidecars read them from `package.json`.

```json
{
  "platform": "unity",
  "packageName": "<package.json name>",
  "packageVersion": "<package.json version>",
  "gameProtocolVersion": 1
}
```

- DevApp Editor runtime registration must not depend on `gc.runtime-info.json`, because local Editor play may happen before any WebGL build exists. It should use the shared editor package identity helper directly.
- Hosted/upload validation can read `gc.runtime-info.json` before loading the Unity player and reject stale or unsupported builds early.
- Once a minimum sidecar-writing package version is established, upload validation can require the sidecar for Unity WebGL builds and reject older package versions with an update-and-rebuild message.
- The sidecar is a quality gate and diagnostics source, not cryptographic proof that the WebGL data/wasm was built with the declared package.

## Runtime identity implementation notes
- Do not duplicate `packageVersion` in runtime C# constants once the shared editor package identity helper exists.
- Keep WebGL export identity generation and DevApp Editor runtime registration in sync by routing both through that helper.
- The unreleased hosted Unity runtime callback path has been removed. Hosted identity should use `gc.runtime-info.json` as the sidecar-first path before loading the Unity player.
- If `gc.runtime-info.json` ever needs its own file schema version, add a separate sidecar schema field or parser migration. Do not use `gameProtocolVersion` for sidecar file schema changes.
- Main Gaming Couch upload/client validation docs and code live outside this Unity package and should be updated separately with explicit cross-repo permission.

## Release channels
- Stable: tagged releases only.
- Pre-release: alpha/beta tags for testers.
- Nightly: automated builds for early adopters; no support SLA.

## Distribution paths
- Unity Asset Store (UAS): stable releases only.
- UPM via Git tag or registry (OpenUPM or GitHub): alpha/beta/nightly.
- GitHub Releases: alpha/beta/nightly artifacts (optional, but convenient).

## Unity Asset Store strategy
- Use a single Asset Store listing.
- Publish stable versions only as the "Latest version".
- Avoid separate alpha/beta listings to prevent fragmentation.
- Provide links in the listing/docs to pre-release channels with clear disclaimers.

## Suggested rollout plan
1) Alpha phase
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

## Nightly builds
- Use CI to build and tag nightly packages.
- Keep nightlies opt-in and clearly labeled "unstable".

## Open questions
- Where should pre-releases be hosted (GitHub, OpenUPM, private registry)?
- Do we have CI (e.g., GitHub Actions) to automate nightlies?
- Is the plugin open source or commercial?
- Which Unity versions must be supported (LTS range)?
