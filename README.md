# Gaming Couch for Unity

The Gaming Couch Unity package is developed here. This is the source repository, not the one
you install the package from.

## Where the package comes from

Every release is published to the public mirror,
https://github.com/deadsetbit/gaming-couch-unity-public, as a standalone snapshot tagged
`unity-<version>`, with the package contents at the repository root. The releases are the
tags. The mirror's `main` branch holds a landing page and no package manifest, so an install
URL without a tag fails rather than tracking whatever shipped last.

Unity installs one tag, picked from the
[mirror's tag list](https://github.com/deadsetbit/gaming-couch-unity-public/tags):

```
https://github.com/deadsetbit/gaming-couch-unity-public.git#unity-<version>
```

Installed that way the version is pinned, and you move to a newer release by editing the tag.

## Documentation

The manual for the newest release is at
https://deadsetbit.github.io/gaming-couch-unity-public/latest/.

## What is in this repository

`public/package/` is the package, and it is the only folder the mirror publishes. Everything
else stays private to development: design docs, release tooling, and agent guidance.

`Tools/bump-version.py` cuts a release and tags it `unity-<version>`. Pushing that tag is what
publishes the snapshot and its documentation.
