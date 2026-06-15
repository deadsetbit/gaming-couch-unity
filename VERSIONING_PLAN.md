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

## Runtime identity, build diagnostics, and upload validation
- `package.json` is the source of truth for the Unity package name and package version.
- `gameProtocolVersion` is the Gaming Couch game integration contract version. Bump it only for a required platform/game contract change, not for diagnostics, package metadata, editor tooling, or upload validation changes.
- Editor code that needs package identity should read package metadata through one shared helper, using Unity package metadata or package-root `package.json`.
- Gaming Couch WebGL export writes a runtime identity sidecar named `gc.runtime-info.json` at the export root, next to `index.html`.
- `gc.runtime-info.json` stays narrow and contains only the same Unity identity fields used by DevApp registration and the hosted WebGL runtime identity callback. The package name and version values are illustrative here; generated sidecars read them from `package.json`.

```json
{
  "platform": "unity",
  "packageName": "<package.json name>",
  "packageVersion": "<package.json version>",
  "gameProtocolVersion": 1
}
```

- The same canonical runtime-info payload is baked into the WebGL runtime resource and sent early through `window.gamingCouchRegisterRuntimeInfo(metadata)` by a package-owned `RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)` bootstrap. `GamingCouchInstanceStarted()` remains payload-free lifecycle startup.
- Any WebGL export also writes `gc.unity-build-info.json` at the export root, even when another WebGL template is selected. This is a separate schema-versioned diagnostic sidecar for Unity editor version, package identity, build target, active WebGL template, selected typed WebGL settings, and selected BuildReport summary values.
- `gc.unity-build-info.json` normalizes path-like values before JSON serialization. Build-output paths are build-output-relative, project paths are project-relative, user-home paths use `${USER_HOME}`, and unknown absolute paths are redacted.
- Build diagnostics do not require a `gameProtocolVersion` bump because they do not change the platform/game runtime contract.
- Current Gaming Couch upload validation preserves root `gc.runtime-info.json`, requires it for Unity uploads, and validates `platform: "unity"`, non-empty `packageName`, SemVer `packageVersion`, and `gameProtocolVersion: 1`.
- Current Gaming Couch upload validation does not validate `gc.unity-build-info.json`. A later main-repo policy may use it for template and WebGL settings warnings or rejections, while `gc.runtime-info.json` remains the Gaming Couch template runtime identity contract.
- DevApp Editor runtime registration must not depend on `gc.runtime-info.json`, because local Editor play may happen before any WebGL build exists. It should use the shared editor package identity helper directly.
- Hosted identity uses `gc.runtime-info.json` as the sidecar-first path before `createUnityInstance`. If the sidecar exists, the hosted SDK rejects startup when the subsequent runtime callback identity differs. If the sidecar is missing, transitional legacy behavior remains.
- Future main-repo policy can still add minimum package-version, stale-package, unsupported-package-version, or `gc.unity-build-info.json` template/settings checks.
- The sidecars are diagnostics sources and validation inputs, not cryptographic proof that the WebGL data/wasm was built with the declared package or settings.

## Runtime identity implementation notes
- Do not duplicate `packageVersion` in runtime C# constants; the shared editor package identity helper and sidecar writer read package identity from package metadata.
- Keep WebGL export identity generation and DevApp Editor runtime registration in sync by routing both through that helper.
- Hosted identity uses `gc.runtime-info.json` as the sidecar-first path before `createUnityInstance`.
- If `gc.runtime-info.json` ever needs its own file schema version, add a separate sidecar schema field or parser migration. Do not use `gameProtocolVersion` for sidecar file schema changes.
- `gc.unity-build-info.json` has its own `schemaVersion`; changes to that diagnostic schema do not imply runtime protocol changes.
- Main Gaming Couch upload/client validation docs and code live outside this Unity package and should be maintained separately with explicit cross-repo permission.

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
