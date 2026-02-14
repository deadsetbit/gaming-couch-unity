@file AGENTS.local.md

## Cross-repo path bridge

- Load `AGENTS.local.md` first before applying the rest of this file.
- The local bridge file is machine-specific and must define only the Gaming Couch main repo base path.
- Relative mappings inside Gaming Couch main repo:
  - GC SDK: `sdk`
  - GC Client: `client`
  - GC DevApp: `devspace/devapp`
- Resolve absolute paths by joining base path from `AGENTS.local.md` with the relative mappings above.
- If a task needs GC SDK, GC Client, or GC DevApp paths and `AGENTS.local.md` is missing, invalid, or does not define base path, ask the user for the Gaming Couch main repo base path.
- After user provides the base path, create or update `AGENTS.local.md` on the fly (it is git-ignored).
- Never use `AGENTS.local.example.md` as a live source of truth; it is only a template for humans.
- Always ask permission from the user if making changes to outside repos.
