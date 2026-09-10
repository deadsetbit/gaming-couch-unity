# Two-Path Runtime Output

The Unity runtime previously pushed player facts (score, lives, status, placement) to the hosted HUD through HUD-owned data calls, which made the HUD the de facto semantic data model. The runtime now emits all output through exactly two physical paths — `runtime_messages` for semantic state snapshots, player transitions, diagnostics, and the effectful game-over result, and `screen_space` for hot, latest-state, frame-stamped screen-coordinate anchors — with runtime player/game state as the canonical source and the HUD demoted to one consumer among others. Separating durable semantic truth from per-frame view data lets any receiver in the cross-repo contract (hosted HUD, DevApp, tooling, future AI/debugging flows) consume the same typed output without HUD-shaped coupling.

## Consequences

- `GCHud.UpdatePlayers` and `GCHud.UpdateScreenPointHud` are removed as erroring `[Obsolete]` stubs; games drive `GCPlayer` state APIs, and HUD anchors flow through `screen_space` (`Runtime/RuntimeMessages/GCRuntimeMessages.cs`, `Runtime/Hud/GCHud.cs`).
- Host-owned WebGL loader output and browser console mirroring are debug aids, not contract output paths.
- Hosted/DevApp receivers are expected, per the cross-repo contract, to treat ingress validation as authoritative rather than trusting emitted payloads.
