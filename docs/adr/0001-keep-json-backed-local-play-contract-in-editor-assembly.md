# ADR 0001: Keep JSON-Backed Local Play Contract In Editor Assembly

Status: Accepted
Date: 2026-05-15

## Context

The Unity package needs a Runtime-callable Local Play Session seam for Play Mode entry, restart preflight, active Capture caching, setup/play option access, and Seat identity access. The current Local Play Contract files, `gc.dev.json` and `gc.metadata.json`, are editor-owned project-root files produced by DevApp and inspected by Unity editor tools.

The JSON-backed implementation uses `Unity.Newtonsoft.Json`, `JObject`, and `JToken` to parse, validate, and preserve unrelated top-level `gc.dev.json` fields during writes. Those details are not needed in player builds and should not be part of the Runtime assembly boundary.

## Decision

Keep `GCLocalPlaySession` in Runtime as the Local Play Session seam, but make it depend only on neutral provider, capture, preflight, and issue types. Move the JSON-backed Local Play Contract stores, file models, validation, metadata reads, and root file stamps into the Editor assembly.

Register an Editor JSON adapter at editor load. The adapter reads and writes `gc.dev.json`, reads `gc.metadata.json`, builds `GCSetupOptions`, `GCPlayOptions`, and `GCSeatIdentity[]`, and maps JSON validation details to neutral session issues before returning to Runtime.

## Consequences

- Runtime no longer references `Unity.Newtonsoft.Json`, `JObject`, `JToken`, `GCDevJson*`, or `GCMetadataJson*`.
- Editor tools keep full JSON validation, preserving writes, inspector draft state, and Start Screen readiness behavior.
- Contract Fixture tests exercise the Editor adapter, while Local Play Session tests fake the neutral provider.
- Future engine packages can implement the same Local Play Contract behind their own adapter without importing Unity Runtime internals.
