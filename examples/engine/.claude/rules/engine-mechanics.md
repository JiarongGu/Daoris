---
name: engine-mechanics
applies_when: working in the example engine
enforces: this repository's own mechanics — the local tier, which the tool never touches
---

# Engine mechanics — this repository's own rule

This file demonstrates the **local** tier: it was written here, it is absent from `daoris.lock`, and
`sync` will never modify or remove it. The generated index lists it marked `(local)`.

A real project keeps its own mechanics in a file like this — the dev loop, the release policy, the
invariants nobody else needs — beside the canonical rules, which state only the shared principles.
The three-layer model exists so adopting doctrine never costs a repository what it already knew.
