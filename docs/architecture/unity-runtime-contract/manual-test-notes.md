# Manual Test Notes

## 2026-06-04 Unity Editor Play Mode Does Not Appear In DevApp

Context:
- Project: `/Users/anttil/dev/dsb/gaming-couch-unity-template`
- Unity version: 6000.2.7f2
- DevApp project: `gaming-couch-unity-template`
- Platform: Unity
- Active build target: WebGL
- Defines: `GC_ENABLE_UNSUPPORTED_MULTIPLAYER`

Observed:
- Unity Editor compiles without errors.
- Entering Play Mode works.
- Unity Editor reads `gc.platform.json` correctly.
- DevApp local project status shows `gc.platform.json`, `gc.dev.json`, Unity project, and Gaming Couch Unity installed as ready.
- DevApp still shows the blank "Run from Unity Editor" state instead of detecting an active Unity Editor runtime.

Action Taken:
- Investigation started in the Unity runtime-to-DevApp handshake path.
- DevApp `/api/status` showed Unity `runtime_register` was accepted, but no `runtime_snapshot` state was applied.
- A live WebSocket probe with `playerIndex`-only seats was rejected by the running DevApp with `1003 Invalid websocket payload`.
- The running DevApp bundle was protocol v9, while DevApp source protocol v10 accepts `playerIndex` / `playerIndicesByPlacement`.
- Decision: keep the Unity package on the current player-index runtime contract. Rebuild/restart DevApp from v10 source instead.
- Rebuilt and restarted DevApp from source. `/api/status` now reports `controllerProtocolVersion:10`.
- With Unity Editor in Play Mode, DevApp now reports `isRunning:true`, two player-index seats, and a populated `activePlaySession`.

Result:
- Fixed by rebuilding/restarting DevApp from v10 source and removing runtime ingress compatibility for the old runtime wire shape.

Next:
- Continue manual Play Mode testing from the active DevApp session.
