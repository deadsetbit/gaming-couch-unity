# Local Bridge Example

Copy this to `AGENTS.local.md`, which is gitignored, and fill in the real paths.

- Gaming Couch main repo base path: `/absolute/path/to/gamingcouch`
- Local Unity host project path: `/absolute/path/to/gaming-couch-unity-template`

Claude Code reads `CLAUDE.md` and never `AGENTS.md`, so it needs a `CLAUDE.local.md` beside
`AGENTS.local.md` containing one line:

```
@AGENTS.local.md
```

Both are gitignored. Without it, Claude Code has no local paths and will ask for them.
