# Ordered Transitions, Never Coalesced

When several accepted player state changes land before a rendered-frame flush, dropping intermediate changes would erase semantic facts (an eliminate-then-respawn must not vanish into "nothing changed"). Every accepted transition (`Runtime/GCPlayerTransitions.cs`) is therefore an ordered fact: it is queued as its own `runtime_messages` transition record in occurrence order and never coalesced, while only latest-state projections — runtime state snapshots and HUD output, tracked via `GCPlayerLatestStateDirtyFlags` — may coalesce to the current value at frame-boundary (or internally capped) flushes. This keeps `runtime_messages` a replayable history of what happened while snapshots/HUD stay cheap current-truth views; batch caps may change flush timing but never remove or merge transition records.

## Consequences

- Duplicate-value requests are rejected as NoOps at the transition gate, so the ordered stream contains only real changes — dedup at source is allowed, coalescing of accepted changes is not.
- Consumers needing history read transition messages; consumers needing current truth read snapshots, and must not infer "no intermediate changes" from a single snapshot.
