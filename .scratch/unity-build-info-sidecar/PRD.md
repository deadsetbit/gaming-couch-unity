## Problem Statement

Unity WebGL builds for Gaming Couch currently emit `gc.runtime-info.json`, a narrow identity sidecar used for upload and hosted-runtime preflight. That file intentionally contains only runtime integration identity: platform, package name, package version, and game protocol version.

The package also needs a way to inspect WebGL build configuration after a build has been produced. The most useful diagnostic data includes Unity version, package identity, selected WebGL settings, build target, template, and selected BuildReport summary data. Putting that data into `gc.runtime-info.json` would blur the runtime identity contract, increase parser risk in Gaming Couch platform code, and make future schema changes harder to reason about.

The diagnostic artifact also must not leak local absolute paths, user home paths, Unity project paths, build output paths, or broad raw Unity settings blobs that may contain project identifiers or secrets.

## Solution

Add a separate versioned build diagnostic sidecar named `gc.unity-build-info.json` beside WebGL build output when the build uses the Gaming Couch WebGL template. Keep `gc.runtime-info.json` unchanged as the identity/preflight contract.

The build-info sidecar will capture allowlisted, typed Unity WebGL build metadata and selected BuildReport fields. Any path-like values included in the file will be normalized field-by-field before serialization: project paths become project-relative, build output paths become build-output-relative, user home paths become `${USER_HOME}`-relative, and unknown absolute paths are omitted or redacted.

The feature does not change the game runtime contract and does not require a `gameProtocolVersion` bump.

## User Stories

1. As a Unity package maintainer, I want build diagnostics in a separate sidecar, so that runtime identity stays small and stable.
2. As a Gaming Couch platform developer, I want to inspect Unity build settings before loading the player, so that upload and hosting issues can be diagnosed earlier.
3. As a Unity game developer, I want build diagnostic output to appear automatically for Gaming Couch WebGL builds, so that I do not need a manual export step.
4. As a Unity game developer, I want non-Gaming Couch WebGL builds to avoid Gaming Couch diagnostics, so that unrelated exports are not polluted with platform files.
5. As a Unity game developer, I want non-WebGL builds to skip the build-info sidecar, so that diagnostics match the hosted platform target.
6. As a maintainer, I want stale build-info files removed when a reused output folder is rebuilt with another WebGL template, so that old diagnostics are not mistaken for the latest build.
7. As a maintainer, I want `gc.runtime-info.json` to remain unchanged, so that upload/runtime identity readers do not need to understand build diagnostics.
8. As a platform developer, I want the diagnostic file to include a schema version, so that future schema changes can be handled deliberately.
9. As a platform developer, I want the diagnostic file to include Unity version, so that build issues can be correlated with Unity editor behavior.
10. As a platform developer, I want the diagnostic file to include package identity, so that diagnostics can be correlated with Unity package releases.
11. As a platform developer, I want the diagnostic file to include the active WebGL template, so that support can verify the Gaming Couch export path.
12. As a platform developer, I want the diagnostic file to include selected WebGL settings, so that common misconfiguration can be identified without opening Unity.
13. As a platform developer, I want the diagnostic file to include development-build and WebGL code optimization state, so that performance and debug-build issues are visible.
14. As a platform developer, I want selected BuildReport summary data, so that output size, warnings, errors, platform, and build result are visible.
15. As a privacy-conscious developer, I want local absolute paths removed or tokenized, so that shared build artifacts do not reveal my machine layout.
16. As a maintainer, I want project-root paths converted to project-relative paths, so that diagnostic paths remain useful without leaking local checkout location.
17. As a maintainer, I want build-output paths converted to build-output-relative paths, so that output file references remain useful after upload.
18. As a maintainer, I want user-home paths tokenized with `${USER_HOME}`, so that unavoidable user-scoped paths are still understandable without exposing the account path.
19. As a maintainer, I want unknown absolute paths redacted or omitted, so that unclassified paths do not leak by accident.
20. As a maintainer, I want path normalization to happen before JSON serialization, so that raw blob replacement does not miss escaped or nested values.
21. As a maintainer, I want tests proving no local project path, build path, or user home path appears in output JSON, so that path hygiene is regression-tested.
22. As a maintainer, I want raw Unity settings snapshots deferred, so that the first version avoids secrets and unstable Unity serialization details.
23. As a documentation reader, I want docs to distinguish runtime identity from build diagnostics, so that I know which file serves which purpose.
24. As a release manager, I want no `gameProtocolVersion` bump for this work, so that diagnostics do not imply a game-facing compatibility change.

## Implementation Decisions

- Build diagnostics use a new file named `gc.unity-build-info.json`.
- Runtime identity remains in `gc.runtime-info.json`; the existing runtime sidecar shape is not extended for build settings.
- The build-info file is emitted only for WebGL builds using the Gaming Couch template identifier.
- The build-info writer removes stale diagnostic sidecars when a WebGL build uses another template and reuses an existing output folder.
- The build-info schema is explicitly versioned with a `schemaVersion` field.
- The first schema includes capture metadata, Unity editor version, package identity, build target, WebGL template, selected typed WebGL settings, and selected BuildReport summary values.
- The first schema may include selected output file metadata only when paths are normalized and useful.
- The first schema does not include raw ProjectSettings snapshots.
- The first schema does not include raw Library user build settings snapshots.
- The first schema does not include broad EditorJsonUtility serialization blobs.
- WebGL setting capture uses typed Unity APIs that are already part of the package's WebGL build settings profile logic.
- The implementation should expose a deep path-normalization module with a small interface that can be tested independently from Unity build callbacks.
- Path normalization is field-by-field before serialization, not blanket string replacement on completed JSON.
- Project-root paths are represented as project-relative paths.
- Build-output paths are represented as build-output-relative paths.
- User-home paths are represented with a `${USER_HOME}` prefix when retaining the path is useful.
- Unknown absolute paths are either omitted or represented as redacted metadata rather than emitted verbatim.
- The postprocess callback remains a thin integration point; testable writer services own file decisions and serialization.
- The diagnostic sidecar is a support and validation aid, not cryptographic proof that build artifacts match the declared settings.
- The feature does not change the platform/game runtime contract and does not bump `gameProtocolVersion`.
- A pointer from `gc.runtime-info.json` to the build-info file is deferred until the Gaming Couch platform upload/client code has a concrete discoverability requirement.

## Testing Decisions

- Tests should verify externally visible file behavior and serialized JSON shape, not private implementation details.
- Writer tests should mirror existing runtime sidecar tests: write for Gaming Couch WebGL template, skip non-WebGL, skip other templates, delete stale files, and overwrite stale files on rebuild.
- Path sanitizer tests should cover Unix absolute paths, Windows absolute paths, project-root paths, build-output paths, user-home paths, and unknown absolute paths.
- JSON tests should assert that serialized output does not contain the local project root, local build output root, raw user home path, or unknown absolute paths.
- Settings capture tests should verify stable field names and representative values from typed WebGL settings.
- Tests should avoid requiring a full WebGL build where a focused writer or sanitizer unit test can prove behavior.
- Focused open-Editor EditMode validation should be used for Unity API-dependent tests.
- Existing runtime identity tests remain the regression guard that `gc.runtime-info.json` stays narrow.
- Documentation changes should be reviewed for clear separation between runtime identity, upload preflight, and diagnostic build metadata.

## Tasks

| ID | Task | Status | Done when | Dependencies | Notes |
| --- | --- | --- | --- | --- | --- |
| Task 1 | Add the versioned build-info DTO and writer service. | Completed | A Gaming Couch WebGL build write call creates `gc.unity-build-info.json` with `schemaVersion`, capture metadata, Unity version, package identity, target, template, typed WebGL settings, and selected build summary values while leaving `gc.runtime-info.json` unchanged. | None | Current runtime sidecar writer already has the right gating pattern and output-root behavior. |
| Task 2 | Add a field-level path normalization module. | Completed | Project-root, build-output, user-home, Unix absolute, Windows absolute, and unknown absolute paths normalize according to policy before serialization. | None | This module should be independently testable and should not rely on raw JSON string replacement. |
| Task 3 | Integrate path normalization into BuildReport output capture. | Completed | Any included BuildReport paths are useful relative or tokenized values, and unknown absolute paths are omitted or redacted. | Task 1, Task 2 | BuildReport data should remain selected and allowlisted because report fields may not be complete during postprocess. |
| Task 4 | Match runtime sidecar lifecycle behavior for skip and stale-file cases. | Completed | Non-WebGL builds do not write build-info; other WebGL templates do not write build-info and remove stale `gc.unity-build-info.json`; Gaming Couch WebGL rebuilds overwrite stale build-info. | Task 1 | Keep the diagnostic sidecar gated to Gaming Couch template builds. |
| Task 5 | Add focused tests for writer behavior, JSON shape, and path hygiene. | Not started | Tests prove write/skip/delete/overwrite behavior, schema fields, selected settings, and absence of raw local project, build output, user home, and unknown absolute paths in serialized JSON. | Task 1, Task 2, Task 3, Task 4 | Existing runtime sidecar tests provide prior art for file behavior tests. |
| Task 6 | Document runtime identity versus Unity build diagnostics. | Not started | README, package documentation, and versioning guidance explain `gc.runtime-info.json`, `gc.unity-build-info.json`, path hygiene, and why no `gameProtocolVersion` bump is needed. | Task 1, Task 5 | Documentation should avoid defining main-repo upload policy beyond what the sidecar enables. |
| Task 7 | Run focused validation. | Not started | Static/package checks and focused open-Editor EditMode tests pass, or any Unity Editor bridge blocker is recorded with retry guidance. | Task 5, Task 6 | Prefer the open-Editor bridge and the host Unity project from the local bridge file. |

## Out of Scope

- Changing `gc.runtime-info.json` schema.
- Adding a `unityBuildInfoFile` pointer to `gc.runtime-info.json`.
- Bumping `gameProtocolVersion`.
- Implementing Gaming Couch main repo upload or hosted-runtime validation policy.
- Capturing raw ProjectSettings snapshots.
- Capturing raw Library or EditorUserBuildSettings snapshots.
- Capturing broad EditorJsonUtility raw blobs.
- Proving cryptographic integrity between diagnostics and built WebGL artifacts.
- Running a full WebGL build as the only validation path.

## Further Notes

Unity build callbacks are the right capture point for build output sidecars, but BuildReport should be treated as selected metadata rather than a complete finalized build manifest. Typed settings from Unity APIs should be the primary source of truth for the first diagnostic schema.

The first implementation should optimize for stable, privacy-preserving diagnostics over completeness. Raw snapshots can be revisited later only with explicit allowlists, redaction rules, and a concrete consumer that needs them.
