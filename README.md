Unity integration for Gaming Couch platform.

---

# Installation

You can import this package by using Unity's _Package manager's_ import from git URL.

Follow the integration steps below to get started.

# Configure the Editor

- From _Build Settings_, switch the platform to "WebGL"
- fix the game window to 16:9 (from top of the Game window), as the platform is fixed to 16:9 aspect ratio

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
    GamingCouch.Instance.InstantiatePlayers(playerStore, options.players, Vector3.up * 100, Quaternion.identity);

    // Setup the game and HUD based on the game/game mode
    GamingCouch.Instance.SetupGameVersus(
        new GCGameVersusSetupOptions()
        {
            // Adjust the placement sorting criteria to fit your game.
            // You can add/remove or change the order of the components.
            // NOTE: In order for the placement criteria to work, you need
            // to use the GCPlayer methods, such as SetEliminated, SetScore/AddScore, SetFinished.
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

## Name tags

To add name tags for players, you need to add GCNameTag component to your player game object.
Usually you want to position the name tag to be above the player, so you can also add the GCNameTag component
to child object of the player game object and offset it to be above the player's head for example.

NOTE: Currently, there is no way to show the name tags in the editor or unity build alone.
The only way to see if the name tags are working correctly is to test it in the Gaming Couch platform.

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

// TODO: Examples for all the value types, and how to update them

To see other HUD value types, see [API documentation for GCHudPlayersConfig](https://deadsetbit.github.io/gaming-couch-unity/api/DSB.GC.Hud.GCHudPlayersConfig.html#DSB_GC_Hud_GCHudPlayersConfig_valueTypeEnum).

## Manually update the Players HUD

NOTE: You need to disable the auto update in the SetupGame's GCGameHudOptions to manually update the Players HUD!

```C#
using System.Linq;
using DSB.GC.Hud;

GamingCouch.Instance.Hud.UpdatePlayers(new GCPlayersHudData
{
    players = playerStore.Players.Select(player => new GCPlayersHudDataPlayer
    {
        playerId = player.Id, // The GamingCouch player id
        eliminated = player.IsEliminated,
        placement = 0, // The placement of the player to sort the players HUD by
        value = ""; // The value to display in the HUD. Set depending on the value type set in the GCGameHudOptions
    }).ToArray()
});
```

# Player integration

## Configure player

When the player is instantiated by GamingCouch some properties are available, such as Gaming Couch player id, color and name.

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
        var inputs = GamingCouch.Instance.GetInputsByPlayerId(player.Id);
        if (inputs == null) continue;

        player.PlayerController.Move(inputs.leftX);
        player.PlayerController.Jump(inputs.primary);
    }
}
```

# Player placement

You do not need to sort the players, just define correct placement criteria in the SetupGame call (see above)
and use the GCPlayer methods to set the player state (eliminated, score, finished):

```C#
// Set player eliminated
player.SetEliminated("Out of bounds");

// Set player score
player.SetScore(0, "Dropped all coins");
// ...or add score
player.AddScore(1, "Collected a coin");
// ...or subtract score
player.SubtractScore(2, "Pushed off the edge");

// Set player finished
player.SetFinished("Finish line");
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

When you are ready to build your project for Gaming Couch, you need to build it as WebGL. You can change this by selecting the WebGL tab from
"Project Settings > Player".

Under "Project Settings > Player > WebGL" tab's "Publish settings", set the compression format to "Disabled".

// TODO: Further instructions on how to build the project for Gaming Couch and integrate it to the platform.

# What next?

- Explore our example template game project: [Gaming Couch Unity Template](https://github.com/deadsetbit/gaming-couch-unity-template)
- Dive into the [API documentation](https://deadsetbit.github.io/gaming-couch-unity/api)

# Creating Unity project from scratch

If you do not want to use our [Gaming Couch Unity Template](https://github.com/deadsetbit/gaming-couch-unity-template),
you can create project from scratch by following these steps:

- create new unity project with the "Universal 3D" template (URP) or optionally "Universal 2D" (URP)
- follow the [installation and integration steps](#installation)
