Unity integration for Gaming Couch platform.

---

# Installation

You can import this package by using Unity's _Package manager's_ import from git URL.

Follow the integration steps below to get started.

# Compatibility

This development line targets Unity 6 (`6000.0`) so Gaming Couch web export settings can remove Unity splash/logo branding.

## Platform compatibility

The package exposes its package version and runtime protocol version to the Gaming Couch platform through DevApp Editor registration, the WebGL runtime-info sidecar, and an early WebGL runtime callback. `packageVersion` is used for diagnostics, while `gameProtocolVersion` identifies the integration contract the platform should support. The package root `package.json` owns the Unity package name and version; do not duplicate those values in runtime constants or documentation examples.

`gameProtocolVersion` is not bumped for package metadata, sidecar generation, or upload validation changes. Bump it only when the platform/game integration contract itself changes.

For this strict package behavior, the current recommendation is no `gameProtocolVersion` bump if the Gaming Couch client/SDK already translates legacy hosted payloads into current Unity runtime payloads before invoking the package. Any protocol bump remains a release-owner/user decision based on rollout risk and adapter compatibility.

# Configure the Editor

- From _Build Settings_, switch the platform to "WebGL"
- fix the game window to 16:9 (from top of the Game window), as the platform is fixed to 16:9 aspect ratio

# Gaming Couch web export settings

Use `GamingCouch/WebGL Build/Preview web export settings` or the web export row in the GamingCouch start screen to prepare a project for Gaming Couch web builds.

The setup workflow requires Unity 6 (`6000.0`). It installs the package-owned Gaming Couch web export template into the project-local `Assets/WebGLTemplates/GamingCouch` folder and selects it as `PROJECT:GamingCouch`. The installer is no-overwrite: rerunning setup creates missing template files but preserves existing project-local template edits. If a destination path is blocked by the wrong asset kind, setup reports a blocker instead of replacing it.

Setup first shows a generated preview of the active build target, template, splash/logo, and release-profile changes it will apply. That preview is the authoritative detailed setting list; skipped setting rows are skipped only for the current apply run and remain reported as readiness drift afterward.

If Unity cannot switch the active build target automatically, setup leaves a warning in the result. Run setup again or switch to WebGL manually before building.

The v1 web export template is a production/upload shell only. It shows loading progress and errors, but it does not provide a standalone browser playtest harness, GamingCouch JavaScript callback shims, local player fixtures, controller simulation, or DevApp communication.

When a WebGL build uses the Gaming Couch template (`PROJECT:GamingCouch`), the package writes `gc.runtime-info.json` to the build output root, next to `index.html`. The sidecar records compact canonical JSON fields: `platform`, `packageName`, `packageVersion`, and `gameProtocolVersion`; `packageName` and `packageVersion` come from `package.json`.

The same canonical payload is baked into the WebGL runtime resource. A package-owned static bootstrap runs with `RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)`, calls the `.jslib` bridge, and forwards the parsed metadata to `window.gamingCouchRegisterRuntimeInfo(metadata)`. `GamingCouchInstanceStarted()` remains a payload-free lifecycle startup signal.

The Gaming Couch hosted SDK reads `gc.runtime-info.json` before `createUnityInstance`. If the sidecar is missing, transitional legacy behavior remains. If the sidecar exists, the hosted SDK stores the normalized identity and rejects startup if the subsequent runtime callback identity differs.

Gaming Couch upload processing in the main repo preserves root `gc.runtime-info.json`; upload validation requires it for Unity uploads and validates `platform: "unity"`, non-empty `packageName`, SemVer `packageVersion`, and `gameProtocolVersion: 1`.

Any WebGL build also writes `gc.unity-build-info.json` next to `index.html`, even when another WebGL template is selected. This separate diagnostic sidecar has runtime identity, build environment, host OS diagnostics, and build result sections for generator metadata, Unity editor/build/WebGL settings, and selected BuildReport values. It is not part of the runtime identity contract, and it does not change `gc.runtime-info.json`.

If a WebGL build later includes a baked `Resources/GamingCouchUnityBuildInfo` payload, the package bootstrap forwards it to optional `window.gamingCouchRegisterUnityBuildInfo(metadata)`. That optional baked diagnostics path does not gate runtime startup; the root `gc.unity-build-info.json` sidecar remains the authoritative complete build diagnostic.

Gaming Couch upload processing preserves root `gc.unity-build-info.json` for diagnostics when present, but upload validation does not require or validate it in this slice. A later main-repo policy may use it to warn or reject builds with the wrong template or WebGL settings, while `gc.runtime-info.json` remains the Gaming Couch template runtime identity contract.

Build diagnostic paths are normalized before JSON serialization. Build-output paths are written relative to the build output, project paths are written relative to the Unity project, user-home paths use a `${USER_HOME}` prefix, and unknown absolute paths are redacted instead of emitted verbatim.

Adding or changing these sidecars does not require a `gameProtocolVersion` bump unless the platform/game integration contract itself changes.

These sidecars and runtime callbacks are drift detection and diagnostics, not cryptographic proof that the WebGL data or wasm was built with the declared package or settings.

# Configure local editor play settings

Unity editor play settings are read from the root `gc.dev.json` file in your Unity project. The package uses this file as the source of truth for local play entry, seed, and the eight-seat player roster, matching the Gaming Couch DevApp local project format.

The root `gc.dev.json` file must already exist. Unity does not create, bootstrap, or repair `gc.dev.json` or `gc.platform.json`; create or update those files through DevApp before using editor play. The `GamingCouch` inspector edits only the canonical `gc.dev.json` fields:

- `devVersion`
- `entryKey`
- `seed`
- `seats`

Inspector writes preserve unrelated top-level `gc.dev.json` fields. Local play settings are no longer stored in scene-serialized editor fields, so changing entry, seed, or seats should not dirty the scene.

When `gc.platform.json` is missing or invalid, Unity shows a warning and keeps raw `gc.dev.json` editing available for structurally valid files. When platform data is valid, it gates Apply and Play: `platform.id` must be `unity`, the selected `entryKey` must exist, and the enabled seat count must be at least one and no more than the selected entry's `maxPlayers`. Production `minPlayers` platform data is still displayed and exported unchanged, but local editor playtests may run with one enabled seat. Enabled bot seats on an entry without bot support are warning-only.

Entering Play Mode or restarting Gaming Couch from Play Mode auto-applies a valid, non-conflicted draft before capture. Invalid or conflicted drafts block Play Mode or restart until you apply, revert, reload from disk, or fix validation errors. Changes made to root JSON files during active Play Mode apply after a Gaming Couch restart or the next Play Mode entry.

The package declares `com.unity.nuget.newtonsoft-json` for editor-only JSON sync and unknown-field-preserving `gc.dev.json` writes. Runtime and WebGL play behavior do not depend on this editor sync path.

# Runtime contract

Unity game code uses player indices only. `GCPlayer.Index`, `GCPlayerOptions.playerIndex`, input polling by `playerIndex`, runtime messages, screen-space anchors, diagnostics, and game-over placement payloads all refer to the same zero-based run-scoped participant index.

DevApp seats are one-based local development slots for controller assignment and display. Hosted platform player IDs are private adapter/platform bookkeeping. Neither seats nor platform player IDs are public Unity runtime identity, and structured diagnostics must not expose platform player IDs.

The package accepts only current runtime identity at its boundary. Current private transport boot payloads provide the player roster as `players[]` records with `playerIndex`, which Unity exposes through `GCPlayOptions.players`; runtime input addresses players by `playerIndex`. Legacy hosted roster and input payloads are not adapted inside the Unity package; client/SDK adapters own that translation before Unity is invoked. DevApp/local editor play may keep `seatIndex`, `activeSeats` (a DevApp-repo concept written by the DevApp, not a field of this Unity package), and `GCSeatIdentity` for controller routing and source-seat mapping, but those seat-domain values are separate from the game-facing `players[]` roster.

Legacy Unity source APIs retained for migration guidance are hard obsolete and fail at compile time. Follow the compiler messages to move old ID/name, input, timestamp, HUD helper, and ambiguous state calls to `GCPlayer.Index`, `playerIndex`, current player-state APIs, and current HUD/runtime-state paths.

Player state APIs distinguish permanent and revokable state. Use `SetEliminatedPermanent`, `SetEliminatedRevokable`, and `SetRevokeEliminated` for elimination, and `SetFinishedPermanent`, `SetFinishedRevokable`, and `SetRevokeFinished` for finish. Revokable state counts while active and can be revoked; permanent state cannot be revoked.

Runtime state and diagnostics flow through `runtime_messages`. Screen-coordinate presentation anchors flow through `screen_space` as `playerOverhead` and `playerPosition` anchors keyed by `playerIndex`. HUD rendering consumes runtime state and screen-space anchors; HUD payloads are not the semantic source of truth.

When `gc.platform.json` is missing or invalid in local development, runtime code receives a read-only fallback platform metadata view with `fallbackActive: true` and exact `notdefined` game/entry values. Unity reports warning diagnostics for the fallback and never writes or repairs `gc.platform.json`.

# Basic integration

## 1) Add GamingCouch game object

Add GamingCouch game object to your main scene by right clicking the scene hierarchy and selecting "GamingCouch" from the menu.

## 2) Create and link game script

- (for new game project) create "Game" game object to the main scene and create and add "Game.cs" main game script to it
- link your main game object to the "Listener" field in the GamingCouch game object (via inspector)
  - now the game script will be able hook up to GamingCouch specific messages (we will get back to this)

## 3) Create and link player prefab

- (for new game project) create "Player" prefab and create and add "Player.cs" script to it
- make your player script extend DSB.GC.GCPlayer (instead of MonoBehaviour)
- link the player prefab to the "Player Prefab" field in GamingCouch game object (via inspector)

## 4) Hook up your main game script

### Define player store in your main game script

```C#
using DSB.GC;
using DSB.GC.Game;
using DSB.GC.Hud;

// Add new field for playerStore. Replace the "Player" with your player script name, if it differs:
private GCPlayerStore<Player> playerStore = new GCPlayerStore<Player>();
```

### Listen for GamingCouchSetup message

This is the place where you can start to load levels and what not based on the GCSetupOptions:

```C#
private void GamingCouchSetup(GCSetupOptions options)
{
    // do stuff based on the options. Eg. load level based on game mode etc.

    // Setup the game and HUD based on the game/game mode
    GamingCouch.Instance.SetupGameVersus(
        new GCGameVersusSetupOptions()
        {
            // Adjust the placement sorting criteria to fit your game.
            // You can add/remove or change the order of the components.
            // NOTE: In order for the placement criteria to work, you need
            // to use the GCPlayer methods, such as SetEliminatedPermanent,
            // SetEliminatedRevokable, SetScore/AddScore, SetFinishedPermanent,
            // or SetFinishedRevokable.
            placementCriteria = new GCPlacementSortCriteria[] {
                GCPlacementSortCriteria.EliminatedDescending,
                GCPlacementSortCriteria.ScoreDescending,
                GCPlacementSortCriteria.Finished
            },

            // configure the HUD, see more on the HUD section
            hud = new GCGameHudOptions()
            {
                players = new GCHudPlayersConfig(),
                // this is by default true, but can be set to false if Players HUD needs to be controlled manually
                isPlayersAutoUpdateEnabled = true,
            }
        }
    );

    // after setup is done call:
    GamingCouch.Instance.SetupDone();
}
```

Next we need to listen when GC and all the players are ready to play:

```C#
private void GamingCouchPlay(GCPlayOptions options)
{
    // we now have all the successfully loaded players so we can instantiate them.
    // This will instantiate and config the players by using the player prefab linked to GamingCouch game object
    GamingCouch.Instance.SetupPlayers<Player>(options.players, (player) =>
    {
        playerStore.AddPlayer(player);
    });

    // next we can set the game to play mode and or play intro
    StartMyGameNow();
}
```

When the game ends, simply call:

```C#
GamingCouch.Instance.GameOver();
```

# HUD

NOTE: All HUD related features are only rendered in the Gaming Couch platform and cant be tested in the editor or unity build alone.

## Player position

Tracks players position and enables features such as:

- displaying an offscreen indicator for the player in the HUD when they move off screen.
- dimming/fading away the left hand player's HUD if the player is positioned "underneath" the HUD.

You should add this to a transform that indicates the player's position in the world.

## Overhead HUD (screen-space anchors, points, meter bar)

To emit an overhead screen-space anchor for a player, add the GCPlayerOverhead component to your player game object.
Usually you want to position the overhead anchor above the player, so you can also add the GCPlayerOverhead component
to a child object of the player game object and offset it above the player's head, for example.

In case you need to place the overhead anchor outside the player game object, manually define the anchor's player with `GCPlayerOverhead.SetPlayer`.

Adding GCPlayerOverhead emits `playerOverhead` screen-space data keyed by `playerIndex`. Hosted HUD rendering can use that anchor with canonical runtime state for related player elements such as points or meter bar.

NOTE: Currently, there is no way to show hosted overhead HUD rendering in the editor or Unity build alone.
The only way to see if the hosted overhead HUD rendering is working correctly is to test it in the Gaming Couch platform.

## Configure Players HUD to display score, lives etc.

### Display score

```C#
GamingCouch.Instance.SetupGameVersus(
    new GCGameVersusSetupOptions()
    {
        maxScore = 10, // required to display the score in HUD
        hud = new GCGameHudOptions()
        {
            players = new GCHudPlayersConfig()
            {
                valueTypeEnum = PlayersHudValueType.PointsSmall
                ...
            }
            ...
        }
        ...
    }
);
```

Now the hud is set to reflect the player score that is set by GCPlayer.SetScore or GCPlayer.AddScore.

To see other HUD value types, see [API documentation for GCHudPlayersConfig](https://deadsetbit.github.io/gaming-couch-unity/api/DSB.GC.Hud.GCHudPlayersConfig.html#DSB_GC_Hud_GCHudPlayersConfig_valueTypeEnum).

## Players HUD updates

The hosted Players HUD reads canonical runtime state from `runtime_messages`. Update player-facing HUD values by calling the `GCPlayer` state APIs, such as `SetScore`, `AddScore`, `SetLives`, `SetStatus`, and `SetMeter`.

# Player integration

## Configure player

When the player is instantiated by GamingCouch, game-facing runtime properties are available, such as player index, color, and player type.
Platform player IDs and player names are not available to Unity game code.

For all available properties, see the [API documentation for GCPlayer](https://deadsetbit.github.io/gaming-couch-unity/api/DSB.GC.GCPlayer.html#DSB_GC_GCPlayer_value).

The values are available on your player script instance on Start (note that they are not yet available on Awake!):

```C#
public class Player : GCPlayer
{
    ...

    private void Start()
    {
        GetComponent<SpriteRenderer>().color = ColorBase;
    }

    ...
}
```

## Player inputs

Read and apply the player inputs in your main game script Update method:

```C#
private void Update()
{
    foreach (var player in playerStore.Players)
    {
        var inputs = GamingCouch.Instance.GetInputsByPlayerIndex(player.Index);
        if (inputs == null) continue;

        player.PlayerController.Move(inputs.leftX);
        player.PlayerController.Jump(inputs.primary);
    }
}
```

# Player placement

You do not need to sort the players, just define correct placement criteria in the SetupGame call (see above)
and use the GCPlayer methods to set score, elimination state, and finish state:

```C#
// Set player permanently eliminated
player.SetEliminatedPermanent("Out of bounds");

// Set player temporarily eliminated, then revoke that state if they recover.
player.SetEliminatedRevokable("Tagged");
player.SetRevokeEliminated("Respawned");

// Set player score
player.SetScore(0, "Dropped all coins");
// ...or add score
player.AddScore(1, "Collected a coin");
// ...or subtract score
player.SubtractScore(2, "Pushed off the edge");

// Set player finished
// Set player permanently finished, or use revokable finish when finish can be rolled back.
player.SetFinishedPermanent("Finish line");
player.SetFinishedRevokable("Checkpoint finish");
player.SetRevokeFinished("Checkpoint invalidated");
```

# Player colors

Access different player color variants directly via the GCPlayer instance:

```C#
GCPlayer.ColorBase
GCPlayer.ColorDark
GCPlayer.ColorLight
GCPlayer.ColorOffWhite
```

# Build your project for Gaming Couch

When you are ready to build your project for Gaming Couch, run the Gaming Couch web export settings before creating the build. Review the generated preview, then apply the target, template, splash/logo, and release-profile changes needed for a web export.

If setup warns that the active build target is still not WebGL, run setup again or switch the project to WebGL manually before building.

WebGL builds produce `gc.unity-build-info.json` for privacy-preserving Unity build diagnostics; upload processing preserves it when present, but upload validation does not require or validate it. Builds that use the Gaming Couch template also produce `gc.runtime-info.json` for narrow runtime identity; current Gaming Couch upload validation requires and validates that runtime sidecar for Unity uploads.

# What next?

- Explore our example template game project: [Gaming Couch Unity Template](https://github.com/deadsetbit/gaming-couch-unity-template)
- Dive into the [API documentation](https://deadsetbit.github.io/gaming-couch-unity/api)

# Creating Unity project from scratch

If you do not want to use our [Gaming Couch Unity Template](https://github.com/deadsetbit/gaming-couch-unity-template),
you can create project from scratch by following these steps:

- create new unity project with the "Universal 3D" template (URP) or optionally "Universal 2D" (URP)
- follow the [installation and integration steps](#installation)

# Unsupported online multiplayer migration surface

Gaming Couch online multiplayer is not a supported Unity runtime contract. Default package builds keep `GamingCouch.OnlineMultiplayerSupport` as a false-returning compatibility probe, make `OnlineMultiplayerServerReady()` and `OnlineMultiplayerClientReady()` throw unsupported API errors, and do not compile the Netcode for GameObjects helper assembly.

The old multiplayer path can only be enabled with the `GC_ENABLE_UNSUPPORTED_MULTIPLAYER` scripting define. That define is reserved for temporary internal migration of legacy games that already used the old unsupported multiplayer implementation. Do not use it for new multiplayer feature work.
