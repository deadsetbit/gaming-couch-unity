# 0013: WebGL compression disabled — serving-layer compression is the platform's concern

Unity's built-in WebGL compression (Brotli/Gzip pre-compressed build artifacts) shifts transfer-encoding concerns into the game build, coupling it to specific server configuration. We decided (2026-07-02) that both build profiles force `WebGLCompressionFormat.Disabled` — the Release and Dev specs in `Editor/GamingCouchWebGLBuildSettingsProfiles.cs` set it, the apply/readiness path re-asserts it over any user-set Brotli/Gzip, and editor tests (`Tests/Editor/GamingCouchActiveSceneSetupAssetTests.cs`) pin it — because, per the cross-repo contract, on-the-fly compression of served build files is owned by the hosting platform's serving layer (another repo), not by this package.

## Consequences

- This is not a bug: do not "fix" the profiles to Brotli/Gzip. The pinned tests failing on a compression change is the guardrail working.
- The hosting platform must compress WebGL build files at the serving layer; if a deploy target does not, it ships uncompressed WASM — resolve that in the platform repo, not here.
