# examples/game

The **content half** of the example family — the project that asks the engine for what it needs,
instead of reaching in. A complete miniature adopter, readable as an example and driven as a fixture
by `npm run rehearse:family`.

What to look at:

- `daoris.json` — the manifest, with the **`domain`** declaration that makes this project addressable.
- `.claude/knowledge/world-streaming.md` — the local knowledge document the family rehearsal finds by
  **searching the shared index from outside this repository** ("chunk hydration"), which is the
  knowledge layer's whole job.
- the quest the rehearsal publishes from here to `engine` — what is needed and why, with the
  evidence, never the change itself. It is held by the service and pulled by the engine's own agent;
  nothing is ever written into the engine's tree.

See [`../README.md`](../README.md) for the whole setup story.
