---
name: skills-workflow
applies_when: starting any change, and whenever a follow-up changes its scope
enforces: make every change through the `change` skill, starting at the area document; read it again when the scope moves
rule: true
---

# Make every change through the `change` skill — it starts at the area document

**Every change that is more than a typo goes through the `change` skill, which starts at the area
document for the part you change and the lines that document points at. When a follow-up names
another area, read that area's document before going on.**

## Why

Work done cold looks correct and breaks an invariant that was already written down, because nothing
surfaces an on-demand document unless the session goes and reads it. Reading the whole tree instead is
paid for on every later step, since a session carries what it read into each step after. One area
document, read at the lines it names, is both the contract and the cheapest start.

## How to apply

- **Start at the area document.** The brief names the area documents. With none for the part you
  change, read that part's own instructions or README, and the knowledge whose *applies when* matches;
  the generated index lists it.
- **Read again when the scope moves.** A follow-up in a different area is a new change, however
  continuous the conversation feels. Same files and same scope is a continuation.
- **Keep a procedure where it can be reviewed.** A managed instruction lives in the repository, or in
  the coordinator's managed store, never only in one agent's user directory, where it works on one
  machine and cannot be reviewed, shared or recovered.
- **Finished something reusable?** Rewrite the area document's sentence, or add the knowledge document,
  so the next change starts ahead of this one.
