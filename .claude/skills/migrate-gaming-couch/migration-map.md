# Gaming Couch migration map

The exhaustive old-symbol → new-symbol table. One idea underlies almost every row:
the runtime contract is now keyed by **`Index`** (the per-round 0..N-1 seat index),
never by the old platform `Id` or player name (ADR 0001, strict runtime identity boundary).

Each row carries a **class** — see `SKILL.md` for what each class means and how carefully to
treat it. In one line: **Rename** = safe swap, **Rewrite** = reshape the expression,
**Reindex** = the *value* changes not just the name (trap), **Choose** = a gameplay decision,
**Rethink** = gone with no replacement.

`CS0619` errors quote the replacement in the message text (reproduced below as `→ "…"`).
`CS1061` errors are silently removed members with **no** guidance — those live in the
[No-marker removals](#no-marker-removals-cs1061) section and can only be found here.

---

## `GCPlayer`

| Old | New | Class | Note |
|---|---|---|---|
| `GCPlayer.Id` | `GCPlayer.Index` | **Reindex** | `Id` and `Index` are different values. Follow the whole data path — anything that stored, keyed, or serialized `Id` must move to `Index`. Also catch **bare unqualified `Id`** (e.g. `$"Player {Id}"`, `"P" + Id`) inside `GCPlayer`-derived types — a `\.Id`-anchored search misses these but they are the same `CS0619`. → `"…Use GCPlayer.Index."` |
| `GCPlayer.PlayerName` | *(removed)* | **Rethink** | Player names are platform-owned and not exposed to game code. Delete the usage or drive UI from `Index`/color instead. |
| `GCPlayer.SetEliminated(reason)` | `SetEliminatedPermanent(reason)` **or** `SetEliminatedRevokable(reason)` | **Choose** | Permanent = cannot come back this round; Revokable = can be undone with `SetRevokeEliminated`. |
| `GCPlayer.SetUneliminated(reason)` | `SetRevokeEliminated(reason)` | **Rename** | Only revokable eliminations can be revoked. |
| `GCPlayer.SetFinished(reason)` | `SetFinishedPermanent(reason)` **or** `SetFinishedRevokable(reason)` | **Choose** | Revoke a revokable finish with `SetRevokeFinished`. |
| `GCPlayer.LastSetEliminatedTime` | `LastSetEliminatedGameTime` (broad) / `LastSetEliminatedPermanentGameTime` / `LastSetEliminatedRevokableGameTime` | **Choose** | Broad = either kind; the specific two split by state. |
| `GCPlayer.LastSetUneliminatedTime` | `LastSetRevokeEliminatedGameTime` | **Rename** | |
| `GCPlayer.FinishedTime` | `LastSetFinishedGameTime` (broad) / `LastSetFinishedPermanentGameTime` / `LastSetFinishedRevokableGameTime` | **Choose** | |
| `GCPlayer.Color` | `GCPlayer.ColorBase` | **Rename** | The single `Color` property was split into `ColorBase`/`ColorDark`/`ColorLight`/`ColorOffWhite`; `ColorBase` is the drop-in. `ColorName` (string) and `ColorEnum` also available. Watch for **bare `Color`** inside a `GCPlayer`-derived type (`= Color;`). |
| `GCPlayer.OnEliminated` (`Action<string>`) | `OnEliminationStateChanged` (`Action<GCPlayerEliminationStateChangedEventArgs>`) | **Rewrite** | **Silently removed — CS1061, no `[Obsolete]` hint.** New event fires on *every* elimination transition (incl. revoke→`None`), so guard `if (args.newState != GCPlayerEliminationState.None)` to keep old fire-on-eliminate semantics; reason is `args.reason`. Handler `(reason) => …` becomes `(args) => …`. |
| `GCPlayer.OnFinished` | `OnFinishStateChanged` (`Action<GCPlayerFinishStateChangedEventArgs>`) | **Rewrite** | Same shape; guard `args.newState != GCPlayerFinishState.None`. CS1061 silent. |
| `GCPlayer.OnUneliminated` | *(folded into `OnEliminationStateChanged`)* | **Rewrite** | The revoke transition now arrives as `OnEliminationStateChanged` with `newState == None`. CS1061 silent. |
| `GCPlayer.PlayerStatus` | `GCPlayer.Status` | **Rename** | |

Player state now models **permanent vs revokable** for both elimination and finish
(`GCPlayerEliminationState` / `GCPlayerFinishState`: `None`/`Revokable`/`Permanent`), with
matching read helpers `IsEliminated`, `IsEliminatedPermanent`, `IsEliminatedRevokable`,
`IsFinished`, `IsFinishedPermanent`, `IsFinishedRevokable`.

---

## `GCPlayerStore<T>` (and its `GCPlayerStoreOutput<T>` projection)

Every row applies to **both** the concrete store and the output-interface view a game may hold.

| Old | New | Class |
|---|---|---|
| `PlayersEnumerable` | `Players` | **Rename** |
| `PlayerCount` | `Players.Count` | **Rewrite** |
| `UneliminatedPlayers` / `UneliminatedPlayersEnumerable` | `PlayersUneliminated` | **Rename** (broad uneliminated = `eliminationState` is `None`) |
| `UneliminatedPlayerCount` | `PlayersUneliminated.Count` | **Rewrite** |
| `UneliminatedNonBotPlayers` / `…Enumerable` | `PlayersUneliminatedNonBot` | **Rename** |
| `UneliminatedBotPlayers` / `…Enumerable` | `PlayersUneliminatedBot` | **Rename** |
| `EliminatedPlayers` / `EliminatedPlayersEnumerable` | `PlayersEliminated` | **Rename** (broad eliminated = permanent + revokable) |
| `EliminatedPlayerCount` | `PlayersEliminated.Count` | **Rewrite** |
| `EliminatedNonBotPlayers` / `…Enumerable` | `PlayersEliminatedNonBot` | **Rename** |
| `EliminatedBotPlayers` / `…Enumerable` | `PlayersEliminatedBot` | **Rename** |
| `GetPlayerById(id)` | `GetPlayerByIndex(index)` | **Reindex** — the argument's meaning changes; feed it an `Index`, not an old `Id`. |

New finer-grained lists also exist if you need them (`PlayersEliminatedPermanent`,
`PlayersEliminatedRevokable`, `PlayersFinished*`, and their `NonBot`/`Bot` variants).

> **Grep caution — the `NonBot`/`Bot` variants hide from an anchored search.** A pattern like
> `UneliminatedPlayers\b` does **not** match `UneliminatedNonBotPlayers` / `UneliminatedBotPlayers`
> (nor the `Eliminated…` forms), so a code-only self-verify can read "clean" while these still
> compile-error (CS0619). Grep `(Uneliminated|Eliminated)(NonBot|Bot)Player` separately — or better,
> trust the **compile**, not the grep, as the final gate.

---

## `GamingCouch`

| Old | New | Class | Note |
|---|---|---|---|
| `GetInputsByPlayerId(id)` | `GetInputsByPlayerIndex(index)` | **Reindex** | Pass `player.Index`, not the old `player.Id`. Both the method and the argument change. |

---

## `GCHud`

| Old | New | Class | Note |
|---|---|---|---|
| `GCHud.UpdatePlayers(GCPlayersHudData)` | *(removed)* | **Rethink** | The manual player-HUD push is gone. Instead set state through `GCPlayer.SetScore/SetLives/SetStatus/SetMeter` and enable `isPlayersAutoUpdateEnabled = true` in `GCGameVersusSetupOptions.hud`. The hosted HUD then auto-updates from runtime-state snapshots. Delete every `UpdatePlayers` call and confirm the state it used to push is now set via player APIs. |
| `GCHud.UpdateScreenPointHud(GCScreenPointData)` | `QueuePointData(GCScreenPointDataPoint)` | **Rewrite** | Was one call with a `points[]` array; now queue each point individually (loop the old array and call `QueuePointData` per element). |

---

## HUD components (`GCNameTag`)

| Old | New | Class | Note |
|---|---|---|---|
| `GCNameTag` (type) | `GCPlayerOverhead` | **Rename** | Drop-in replacement — same `SetPlayer(GCPlayer)`, `hideWhenPlayerEliminated`, and screen-anchor behaviour. **Also a scene/prefab migration**, see below. |

**`GCNameTag` is a `MonoBehaviour`.** Renaming it in code is not enough: any `.prefab` or
`.unity` asset with the component attached references the old script by GUID. At tag
`unity-0.1.0-alpha.5` that GUID still resolves to a `[Obsolete(…, true)]` `GCNameTag` stub, so the
component binds to a **hard obsolete-error** MonoBehaviour rather than the literal *"missing script"*
Unity shows once the file is deleted — either way the swap is required. The fix is a **script-GUID
swap** in every affected asset (the two components share `hideWhenPlayerEliminated` + `drawDebugGizmo`,
so serialized values carry over as a true drop-in):

- old `GCNameTag` GUID:        `7aff896955ad84e14b9f0dcb7a3318a9`
- new `GCPlayerOverhead` GUID: `e69d99fd27a6c48309e4292e83f750d2`

Grep assets for the old GUID (and the literal `GCNameTag`) — note a component can be attached in a
prefab/scene with **no C# reference**, so a code-only grep can read "clean" while an asset still
binds the old script. Always grep assets independently.

---

## No-marker removals (`CS1061`)

Fully removed with **no** `[Obsolete]` guidance — the compiler just says the member does not
exist. These are findable only here.

| Old | New | Class | Note |
|---|---|---|---|
| `GamingCouch.InstantiatePlayers<T>(playerOptions, onReady)` | `GamingCouch.SetupPlayers<T>(playerOptions, onReady)` | **Rename** | Historical rename. An optional `GCPlayerSpawnProperties[]` overload also exists for spawn position/rotation. |
| `GCNameTag.SetPlayerId(int)` | `GCPlayerOverhead.SetPlayer(GCPlayer player)` | **Reindex** | The old id-based setter is gone; the replacement takes the player object. Pass the `GCPlayer` instance, not a numeric id/index. |
| `GCControllerInputs.rightX` / `rightY` | *(removed)* | **Rethink** | No right stick in the current contract. Rework the control scheme (there is only one stick: `leftX`/`leftY`). |
| `GCControllerInputs.lx`/`ly`/`rx`/`ry`/`b1`–`b4` | `leftX`/`leftY`/`primary`/`secondary`/`alt` (see controller section) | **Rename/Rethink** | Even-older field names on games pinned to early commits. `lx`→`leftX`, `ly`→`leftY`, `b1`→`primary`, `b2`→`secondary`; `rx`/`ry` removed. |
| `GCPlayer.OnEliminated` / `OnFinished` / `OnUneliminated` | `OnEliminationStateChanged` / `OnFinishStateChanged` | **Rewrite** | The whole legacy `On*` event surface is a silent removal — see the `GCPlayer` table for the guarded-lambda pattern. |

---

## Controller input surface

`GCControllerInputs` now exposes only: `leftX`, `leftY`, `primary`, `secondary`, `alt`, `RawData`.

| Change | Class | Note |
|---|---|---|
| **Older field names** `lx`/`ly` → `leftX`/`leftY`; `b1`/`b2` → `primary`/`secondary`; `b3`/`b4` → `alt` or removed (infer from use); `rx`/`ry` removed | **Rename / Rethink** | Games pinned to early commits use the pre-`leftX` names. Buttons were `int` (0/1); new `primary`/`secondary`/`alt` are `bool`, so `inputs.b1 == 1` → `inputs.primary`. `rx`/`ry` are the removed right stick. CS1061 silent. |
| `rightX` / `rightY` removed | **Rethink** | See above — no right stick. |
| `GCControllerInputsData.a2` / `a3` removed | **Rethink** | Backing fields for the right stick. |
| `GCControllerInputsData.b3`, `b12`–`b15` (DPad) removed | **Rethink** | DPad fields gone. |
| `leftX` / `leftY` no longer fall back to DPad | *behaviour note* | Same API, but they now read the left analog stick only (`a0`/`a1`). If old code relied on DPad-as-axis, re-check feel. |

---

## Not a migration (still current)

These look adjacent but are unchanged — do not "fix" them: `GCPlayer.Index`, `Score`, `Lives`,
`Meter`, `Status`, `SetScore/AddScore/SubtractScore`, `SetLives/AddLives/SubtractLives`,
`SetStatus`, `SetMeter`, `ColorBase/ColorDark/ColorLight/ColorOffWhite`, `ColorEnum`,
`SetupGameVersus`, `GameOver`, `SetupDone`, `GetPlayerOptions`, `Hud.QueuePointData`,
`GCPlayerStore.Players` / `.GetPlayerByIndex` / `.AddPlayer` / `.Clear`.
