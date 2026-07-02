# 0003: Permanent/revokable player state on a dual timebase

Reversible booleans (`eliminated`, `finished`) could not express whether a state was final, which broke placement and game-over snapshots when games un-eliminated players. We replaced them with explicit independent enums — `GCPlayerEliminationState` and `GCPlayerFinishState`, each `None`/`Revokable`/`Permanent` (`Runtime/GCPlayer.cs`) — where revoke only works on `Revokable`, permanent calls promote revokable state in place, and `GameOver()` freezes whatever state is current. Timestamps use a dual timebase: game-facing properties (`LastSet...GameTime`, `changedAtGameTime` in state events) use scaled Unity game time for game logic such as respawn delays and rule timers, while runtime messages and receiver ordering use unscaled active-run-relative `runtimeTimeMs` — never game time — so pause and `Time.timeScale` cannot distort output ordering.

## Consequences

- Old boolean-era APIs (`LastSetEliminatedTime`, `LastSetUneliminatedTime`, etc.) are hard-obsoleted with substitute guidance rather than silently aliased.
- Deferred follow-ups, tracked in the backlog: the `SetMeter` payload semantics and stable elimination/finish `reason` codes (currently free-form text).
