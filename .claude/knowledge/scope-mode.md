---
name: scope-mode
applies_when: planning a batch of related parts toward one target design, such as a foundation rebuilt or a redesign carried through
enforces: a scope is built in waves on one integration branch; builders hand back once; merges inside it run no per-merge gates; the full verification and two reviews, wide and close, run once at its end before it lands
---
<!-- daoris: core/core/knowledge/scope-mode.md @ 0.0.1 — canonical; edit via `daoris upstream` -->

# The scope mode: build a batch in waves, verify it once

**When a batch of parts moves toward one design, build it as one scope: the parts are taken in waves by builders who
each hand back once, merged into one integration branch with no gates per merge, and the whole is verified once at
the end, by the full set of checks and two reviews (wide and close), before it lands on the main line.**

## Why

Gating every merge of a redesign pays the full cost many times for a state nobody ships. A day that merged parts one
by one behind their own gates spent most of its time in verification, re-ran the same slow suites on states that
the next part changed again, and still found the parts' joint defects only when the last one was in. The gates
were measuring the steps, and the defects were in the whole.

A builder that stops to report mid-part costs a round trip per stop, and a part merged with failing reviews it never
saw costs a second wave. One hand-back per builder, and reviews once on the whole, put the cost where the defects are.

## How to apply

1. **Name the scope and its end.** The target design, the parts and their waves (what may run together, what waits
   for what), and one integration branch that every part merges into. The main line does not move for the scope
   until its end.
2. **Builders take whole waves.** Each writes its tests, compiles, runs only the tests it added or changed, and
   hands back once with what is done and what is left.
3. **Merges inside the scope carry no gates.** A plain merge, its conflicts resolved, its generated files written
   again. What must never reach history (leaked names, credentials) is still checked at every commit: that check is
   not a gate on the step, it guards the record.
4. **At the end, once:** the full set of checks on the integration branch, and two reviews of the whole, a **wide**
   one (the design, and effects beyond the change) and a **close** one (details, edge cases, tests), from different
   model families where two are ready. Their reds and high findings are fixed forward on the branch, then it lands.

## When not to

- A single part, or parts with no shared design: each lands by the ordinary path, gated on its own.
- A part another team is waiting on: land it alone, ahead of the scope, so the wait ends.
