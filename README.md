Unity integration for Gaming Couch platform.

---

// TODO: Create simple example project to demonstrate the integration.
// TODO: Document whole Unity project basic setup.

# Installation

You can import this package by using Unity's _Package manager's_ import from git URL.

Follow the integration steps below to get started.

# Basic integration

## 1) Add GamingCouch game object

Add GamingCouch game object to your main game scene alongside your main game script object,
by clicking the scene hierarchy right mouse button and selecting "GamingCouch" from the menu.

## 2) Create and link game script

- create main game script eg Game.cs or use your existing main game script
- create game object to the main game scene and assign the script to it
- link the game script to the "listener" field in GamingCouch game object (via inspector)
  - now the game script will be able hook up to GamingCouch specific messages, we will get back to this

## 2) Create and link player prefab

- create Player.cs script extend GCPLayer, or modify your existing player script
- create player prefab and assign the script to it
- link the player prefab to the "player prefab" field in GamingCouch game object (via inspector)

## 3) Hook up your main game script

### Define player store in your main game script

```C#
// Use GamingCouch:
using DSB.GC;

// Add new field for playerStore. Replace the "Player" with your player script name, if it differs:
private GCPlayerStore<Player> playerStore = new GCPlayerStore<Player>();
```

### Listen for GamingCouchSetup message

This is the place where you can choose to load levels and what not based on the GCSetupOptions:

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
private void GamingCouchPlay(GCPLayOptions options)
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

If the game has multiple game modes you can setup the hud differently for each game mode,
but here is the most basic setup that will just display the avatars and names:

```C#
GamingCouch.Instance.Hud.Setup(new GCHudConfig
{
    players = new GCHudPlayersConfig()
});
```

To display score or status text, see the [API documentation for GCHudPlayersConfig](https://deadsetbit.github.io/gaming-couch-unity/api/DSB.GC.Hud.GCHudPlayersConfig.html#DSB_GC_Hud_GCHudPlayersConfig_valueType).

## Update the players HUD

```C#
GamingCouch.Instance.Hud.UpdatePlayers(new GCPlayersHudData
{
    players = Game.Instance.Players.Select(player => new GCPlayersHudDataPlayer
    {
        playerId = player.GetId(),
        eliminated = !player.IsAlive,
        placement = 0,
    }).ToArray()
});
```

If your HUD displays score or status text, you can pass value for it, see the [API documentation for GCPlayersHudDataPlayer](https://deadsetbit.github.io/gaming-couch-unity/api/DSB.GC.Hud.GCPlayersHudDataPlayer.html#DSB_GC_Hud_GCPlayersHudDataPlayer_value).
