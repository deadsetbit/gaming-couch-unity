Unity integration for Gaming Couch platform.

---

# Installation

You can import this package by using Unity's _Package manager's_ import from git URL.

Follow the integration steps below to get started.

# Configure the Editor

- fix the game window to 16:9 (from top of the Game window), as the platform is fixed to 16:9 aspect ratio

# Basic integration

## 1) Add GamingCouch game object

Add GamingCouch game object to your main scene by right clicking the scene hierarchy and selecting "GamingCouch" from the menu.

## 2) Create and link game script

- (for new game project) create "Game" game object to the main scene and create and add "Game.cs" main game script to it
- link your main game script to the "Listener" field in GamingCouch game object (via inspector)
  - now the game script will be able hook up to GamingCouch specific messages (we will get back to this)

## 2) Create and link player prefab

- (for new game project) create "Player" prefab and create and add "Player.cs" script to it
- make your player script extends DSB.GC.GCPlayer (instead of MonoBehaviour)
- link the player prefab to the "Player Prefab" field in GamingCouch game object (via inspector)

## 3) Hook up your main game script

### Define player store in your main game script

```C#
// Use GamingCouch:
using DSB.GC;

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
    GamingCouch.Instance.InstantiatePlayers(playerStore, options.players);

    // next we can set the game to play mode and or play intro
    StartMyGameNow();
}
```

When the game ends, simply call:

```C#
// you need to pass the player id's in placement order:
GamingCouch.Instance.GameEnd(placementsByPlayerId);
```

Next you should integrate the HUD, see the next section.

# HUD integration

## Setup the HUD

The most basic setup that will just display the avatars and names of the players:

```C#
using DSB.GC.Hud;

GamingCouch.Instance.Hud.Setup(new GCHudConfig
{
    players = new GCHudPlayersConfig()
});
```

To display score or status text, see the [API documentation for GCHudPlayersConfig](https://deadsetbit.github.io/gaming-couch-unity/api/DSB.GC.Hud.GCHudPlayersConfig.html#DSB_GC_Hud_GCHudPlayersConfig_valueType).

If the game has multiple game modes you can setup the HUD differently for each game mode.

## Update the players HUD

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
    }).ToArray()
});
```

If your HUD is setup to display score or status text, you can pass value for it, see the [API documentation for GCPlayersHudDataPlayer](https://deadsetbit.github.io/gaming-couch-unity/api/DSB.GC.Hud.GCPlayersHudDataPlayer.html#DSB_GC_Hud_GCPlayersHudDataPlayer_value).

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
        GetComponent<SpriteRenderer>().color = GetColor();
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

        player.PlayerController.Move(inputs.lx);
        player.PlayerController.Jump(inputs.b1 == 1);
    }
}
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
