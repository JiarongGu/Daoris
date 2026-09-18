# examples/engine

The **runtime half** of the example family — the project the game asks for engine work. It is a
complete miniature adopter, readable as an example and driven as a fixture by
`npm run rehearse:family`.

What to look at:

- `daoris.json` — the manifest: pinned source, no packs (core only), and the **`domain`** declaration
  that makes this project addressable: what it owns, what it accepts.
- `.claude/rules/engine-mechanics.md` — the **local** tier: this repository's own rule, never synced,
  never touched, listed `(local)` in the generated index.
- everything else under `.claude/` — the **canonical** tier, materialized by `daoris sync` and pinned
  by `daoris.lock`. A canon change re-syncs these in the same commit; the family rehearsal fails if
  they lag.

See [`../README.md`](../README.md) for the whole setup story.
