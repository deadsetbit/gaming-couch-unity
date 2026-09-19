# Gaming Couch for Unity

Unity integration for the Gaming Couch platform.

Gaming Couch runs your game as a WebGL build inside the platform. The menus, lobby, phone
controllers, HUD and scoreboard come from the platform — this package hands your game the
players, their inputs and their colors, and feeds your game's state back to the Gaming Couch
platform.

## Install [Recommended]

DevApp installs this package for you and keeps it on the version the platform expects.

1. Sign in at [devspace.gamingcouch.com](https://devspace.gamingcouch.com) and bootstrap your
   game.
2. Download DevApp from the **Downloads** page.
3. In DevApp, add your Unity project folder and link it to your game.
4. DevApp installs the package, and offers an update whenever a newer release is required.

No account yet? Ask for one on [Discord](https://discord.gg/UqSX9ZGz5u).

## Manual Install

In Unity, open **Window → Package Manager → Add package from git URL** and enter a release
tag:

```
https://github.com/deadsetbit/gaming-couch-unity-public.git#unity-<version>
```

Releases are published as `unity-<version>` tags. Pick one from the
[tags](https://github.com/deadsetbit/gaming-couch-unity-public/tags) and install that — the
package lives in the tags, not on this branch. Installed this way the version is pinned: Unity
offers no update for a git URL, so you move to a newer release by editing the tag.

Requires Unity 6000.0 or newer.

## Documentation

The manual for the newest release is at
<https://deadsetbit.github.io/gaming-couch-unity-public/latest/>. It is the guide for game
developers, covering install, quick setup, players, inputs, the HUD, game flow, and building
and uploading, with the generated API reference alongside it.

Each release also keeps its own copy under its version number, so a pinned install reads the
documentation for the version it actually has:
<https://deadsetbit.github.io/gaming-couch-unity-public/0.1.0-alpha.11/>. Those addresses are
never rewritten, and every page has a picker for switching between releases.

## License

Apache-2.0
