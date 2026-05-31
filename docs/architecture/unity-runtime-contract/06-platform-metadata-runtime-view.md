## Platform Metadata Runtime View Plan

Status: Planning required before implementation.

## Purpose

Define the read-only Unity runtime projection of dashboard-managed `gc.platform.json` data and the fallback behavior for missing or invalid metadata.

## Decisions To Make

- Runtime projection: entries, selected entry, min/max player limits, bot support, player colors, game identity, platform identity, schema version, source metadata, and validation state.
- Fallback defaults: exact `notdefined` entry shape, limits, bot support, colors, platform/game identity, and warning diagnostics.
- Partial invalid behavior: whether Unity discards all metadata on any invalid field or uses valid subtrees with diagnostics.
- Health semantics: whether fallback is local-play-only, allowed in Editor play, allowed in WebGL build, or blocking for upload/publish.
- Read-only boundary: confirm Unity never creates, repairs, bootstraps, or writes `gc.platform.json`.
- Future metadata: reserved fields for capabilities, localization, audio asset references, feature flags, controller config, and dashboard-owned runtime behavior.

## Open Questions

- Should runtime code receive raw metadata or a normalized safe view only?
- How should runtime code distinguish valid metadata from fallback metadata?
- Should fallback colors be generated, fixed defaults, or omitted?
- What diagnostics should appear persistently in Editor vs DevApp vs hosted validation?

## Ready When

- Fallback shape is concrete enough for tests.
- Valid, missing, invalid, and partially invalid metadata behavior is defined.
- Build/upload/local-run boundaries are explicit.
- Runtime projection can evolve for future dashboard-owned features without exposing raw unstable JSON to game code.
