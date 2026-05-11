# Gaming Couch Unity Package

This package integrates a Unity game with the Gaming Couch platform.

## Compatibility

This development line targets Unity 6 (`6000.0`) so clean WebGL export setup can remove Unity splash/logo branding.

## Local Editor Play Settings

Unity editor play settings are file-backed. The `GamingCouch` inspector reads and writes the root `gc.dev.json` file in the Unity project, and uses it as the source of truth for local play entry, seed, and the eight-seat player roster.

Unity requires an existing root `gc.dev.json`. It does not create, bootstrap, or repair `gc.dev.json` or `gc.metadata.json`; use the Gaming Couch DevApp local project flow to create and maintain those files before entering Play Mode.

The inspector writes only the canonical `gc.dev.json` fields:

- `devVersion`
- `entryKey`
- `seed`
- `seats`

Unknown top-level fields in `gc.dev.json` are preserved on Unity writes. Local play entry, seed, and seats are not maintained as scene-serialized fallback settings.

Missing or invalid `gc.metadata.json` is warning-only. In that state, the inspector shows raw `gc.dev.json` data and can apply structurally valid raw edits. When metadata is valid, it gates Apply and Play:

- `platform.id` must be `unity`.
- The selected `entryKey` must exist.
- Enabled seats must include at least one seat and no more than the selected entry's `maxPlayers`. Production `minPlayers` metadata is still displayed and exported unchanged, but local editor playtests may run with one enabled seat.

Enabled bot seats on an entry with `botSupport: false` are warning-only.

Entering Play Mode or restarting Gaming Couch from Play Mode auto-applies a valid, non-conflicted inspector draft before capturing setup/play options. Invalid or conflicted drafts block Play Mode or restart until resolved. JSON changes during active Play Mode are deferred until a Gaming Couch restart or the next Play Mode entry.

The package depends on `com.unity.nuget.newtonsoft-json` for editor-only JSON parsing and `JObject` writes that preserve unrelated root `gc.dev.json` fields. Runtime and WebGL builds do not use this editor sync path.
