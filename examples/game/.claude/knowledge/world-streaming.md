---
name: world-streaming
applies_when: loading or unloading world chunks in the example game
enforces: hydrate a chunk before its neighbours are visible; never evict the player's anchor chunk
---

# World streaming — chunk hydration order

The example game's own knowledge: **chunk hydration** runs neighbours-first, so a player turning the
camera never sees an unhydrated seam — and the anchor chunk, the one the player stands in, is never
evicted, whatever the memory pressure says.

This document also exists to be *found*: the family rehearsal searches the shared index for
"chunk hydration" and expects this repository to answer. That is the knowledge layer doing its job
across projects — a sibling learns what this repository knows without ever opening it.
