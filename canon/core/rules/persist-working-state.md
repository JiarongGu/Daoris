---
name: persist-working-state
applies_when: any multi-step task, at each decision, finding or milestone
enforces: the commit and the area document are the durable state, written as each piece lands; a decision entry only for a choice that rejected an alternative
---

# Persist working state as you go — context is not durable

**Working context is ephemeral. Commit each piece that passes its tests, and rewrite the area
document's sentence when what it describes changes, as each one lands rather than at the end. A choice
that rejected an alternative gets a decision entry, with what it rejected.**

## Why

A long task's context is summarized, truncated, or simply ends. What was held only there (why an
approach was rejected, the measurement behind a constant, the half-finished plan) is gone exactly when
the next session needs it, and what remains is code whose motivation nobody can reconstruct. Writing at
the end is the same failure with extra steps: the end is where interruptions land.

## How to apply

- **The commit is the progress.** A piece that passes its tests is committed before the next begins; its
  message says what changed, and a regression fix's says the root cause.
- **The area document is the present fact.** Rewrite the sentence that is now wrong, in place; never add
  a newer one beside it, and write no history or status words into it.
- **A decision is for a choice.** Record one only where an alternative was weighed and rejected, with
  the reason: without it, someone reverses the choice and rediscovers the problem.
- **An unfinished plan is the backlog row's,** a line or two, updated as its steps land.
- Durable means in the repository, tracked and reviewable: not a scratch directory, and not a
  conversation.
