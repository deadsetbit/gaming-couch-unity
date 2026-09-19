# Gaming Couch for Unity

The Gaming Couch Unity package is developed here. This is the source repository, not the one
you install the package from.

## Where the package comes from

Every release is published to the public mirror,
https://github.com/deadsetbit/gaming-couch-unity-public, as a standalone snapshot with the
package contents at the repository root. That is the git URL Unity installs:

```
https://github.com/deadsetbit/gaming-couch-unity-public.git#unity-<version>
```

DevApp installs the package for you and keeps it on the version the platform expects. The
manual covers that path and the manual install.

## Documentation

[The manual](https://deadsetbit.github.io/gaming-couch-unity-public/latest/) takes a game
developer from install and setup through players, inputs, the HUD, game flow, and building and
uploading, with the generated API reference beside it. It documents the newest release, and
every page can switch to the documentation for an older one.

## What is in this repository

`public/package/` is the package, and it is the only folder the mirror publishes. Everything
else stays private to development: design docs, release tooling, and agent guidance.

`Tools/bump-version.py` cuts a release and tags it `unity-<version>`. Pushing that tag is what
publishes the snapshot and its documentation.
