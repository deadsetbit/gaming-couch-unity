# Two-Sidecar Identity Model

Gaming Couch needed Unity build identity readable before `createUnityInstance` and build diagnostics for support, without runtime C# duplicating package metadata. WebGL export therefore writes two sidecars at the export root with deliberately different weights: `gc.runtime-info.json` is the narrow identity gate (platform, packageName, packageVersion, gameProtocolVersion; written only for the Gaming Couch template and deleted as stale on other-template WebGL builds — `Editor/GamingCouchWebGLRuntimeInfoSidecar.cs`), while `gc.unity-build-info.json` is non-gating build diagnostics (own `schemaVersion`, Unity/WebGL settings, path-normalized and redacted output paths; written for every WebGL build — `Editor/GCUnityBuildInfoSidecar.cs`). Splitting gate from diagnostics keeps the validated contract minimal and stable while diagnostics can grow freely under their own schema version.

## Consequences

- Per the cross-repo contract, Gaming Couch upload validation requires `gc.runtime-info.json` for Unity uploads, and the hosted SDK rejects startup when the runtime identity callback differs from the sidecar; `gc.unity-build-info.json` is preserved but never required or validated.
- Neither sidecar is cryptographic proof that the wasm/data was built with the declared package or settings — they are declared identity and diagnostics, by design.
- `package.json` is the sole source of package name/version: both sidecars resolve identity through the shared helper (`Runtime/GCEditorPackageIdentity.cs`), and no package-version literals exist in runtime code or the WebGL template.
