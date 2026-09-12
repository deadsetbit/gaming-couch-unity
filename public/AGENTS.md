# The publish boundary

Everything under `public/package/` is published to
`deadsetbit/gaming-couch-unity-public` on release. Nothing else in this repository is.

The boundary is the filesystem, not a list. There is no allowlist to keep up to date and no
ignore-list inside the published folder: if a file must stay private, it lives outside
`public/package/`. Dev-only *behaviour* ships and is gated at runtime rather than stripped at
publish time, so the package we test is the package consumers install.

## Writing files under `public/package/`

- **`public/package/` is the package root.** In the public repo its contents sit at the
  repository root, so the `public/package/` prefix does not exist there. Write every path and
  link relative to the package root.
- **A link that escapes the package root is a 404 for consumers.** `../docs/`, `../CONTEXT.md`
  and anything under `https://github.com/deadsetbit/gaming-couch-unity/` resolve to files that
  stay private. Where an internal document is load-bearing for a reader, fold its point into
  the shipped text instead of linking to it.
- **API and docs links** point into the release's own folder on the published docs site,
  `https://deadsetbit.github.io/gaming-couch-unity-public/<version>/`. Do not write the
  version by hand: `Tools/bump-version.py` rewrites every one of these when a release is cut,
  so any placeholder version in a link is corrected at that point.

## Files in `public/` itself

`public/AGENTS.md` and `public/landing/` are **not** published by the mirror — only
`public/package/` is. `public/landing/README.md` is the source of the public repo's
hand-written landing page, copied across by a human.

The full design is in `docs/architecture/public-mirror-plan.md`.
