# Minimal Shell Web Template

The package owns a single-file web export template (`Editor/WebGLTemplates/GamingCouch/index.html`) that is a minimal production/upload shell: a fullscreen canvas, Unity loading progress, and error display — nothing else. It deliberately ships no standalone browser playtest harness, no GamingCouch JavaScript callback shims, no controller simulation or DevApp communication, and no PWA assets, service worker, manifest, or visible chrome, because Gaming Couch games are only playable inside the hosting platform, which provides the browser-side integration as its own cross-repo contract. Keeping the shell minimal avoids duplicating or drifting from platform-owned behavior and keeps exported builds honest about that boundary.

## Consequences

- Opening an exported build directly in a browser loads the Unity instance but is not a supported way to play; local playtest tooling is explicitly out of scope for the template.
- Any browser-side GamingCouch behavior (callback shims, seats, input) is defined by the hosting platform, not by this template.
