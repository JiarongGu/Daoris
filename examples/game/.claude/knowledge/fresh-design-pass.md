---
name: fresh-design-pass
applies_when: planning a foundational redesign, when new ideas keep bending to old mechanisms in a long-running project, or at a periodic whole-product review
enforces: two designs from a purpose brief alone, reconciled against the code into ranked requests and owner questions; a structural idea is tried on one area against a recorded baseline and kept or retired by criteria written first
---
<!-- daoris: core/core/knowledge/fresh-design-pass.md @ 0.0.1 — canonical; edit via `daoris upstream` -->

# The fresh-design pass: design from the purpose, then compare with what exists

**Write two designs from a short purpose brief alone, with no code, documents or decisions in hand, then have
one reader compare both with the current product. The value is the comparison, never either design.**

## Why

Months of context make every new idea arrive already shaped like the mechanisms that exist: a reader who
knows why each part is the way it is proposes changes that keep it that way. A design written from the
purpose alone cannot inherit those shapes, so it names the gaps the history hides, and setting it beside
the code turns a vague direction into questions the owner can answer in one line.

## How to apply

1. **The purpose brief.** One page in the owner's picture, deliberately without architecture, names, files or
   decisions:

   ```markdown
   # <Product>: the purpose
   - What it is for, and who uses it.
   - Its parts, as the owner sees them.
   - What the owner must be able to do, and to see.
   - The goals, in the owner's words.
   ```

2. **Two designs.** Two different model families, or two strong models, each at its highest reasoning
   setting, each writing to one file of its own from the brief alone. Neither reads the code.
3. **The reconciliation.** One reader compares both designs with the current code and writes a gap
   analysis: where the product today is worse, the top items ranked by value over effort, and the
   questions only the owner can answer. Its output is a sorted list of requests, never a decision by itself.
4. **The owner's questions.** Each says what it is, what yes changes, what no changes and where to look.
   An answer is recorded as a decision, not as the owner's words.
5. **Adoption by trial.** A structural idea is tried first on one area, measured against a baseline recorded
   before the trial (what a change reads before its first edit, time and tokens per change, the main line's
   red rate, owner interventions), and kept or retired by criteria written before it starts. A retired
   design stays in the library as reference.

## What it is not good at

- It cannot see live constraints: permissions, data flows, a migration's records already written. The
  reconciliation is where those come back in.
- It costs a full design per model; run it for a foundation or a periodic review, not for a feature.
- Adopting one design whole throws away what the comparison found. Take the parts the comparison ranks.
