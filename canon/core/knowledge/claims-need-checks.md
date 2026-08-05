---
name: claims-need-checks
applies_when: writing any statement about what the code guarantees — a readme, a comment, a config field, a status line
enforces: a documented guarantee ships with the check that enforces it, in the same change; an unenforceable claim is written as intent, never as fact
---

# A documented guarantee needs the check that enforces it

**When you write that something is guaranteed — always true, never happens, verified, enforced — write
the thing that enforces it in the same change. If you cannot enforce it yet, say so in the same
sentence.**

## Why

An unenforced guarantee is worse than none. Silence invites a reader to check; a confident sentence
tells them not to bother. So the claim outlives the behaviour and is believed for exactly as long, and
nothing reports the divergence — the whole point of the sentence was that nobody needed to look.

Carelessness is not the cause. The claim and the enforcement are written at different moments, and only
the claim is easy: documentation gets written while the behaviour is fresh and obviously true, which is
the one moment a check feels redundant and the moment it is cheapest to write.

The shapes repeat — a readme describing a property no test asserts, a comment deferring to a component
that does not exist, a configuration field parsed and never read, a status line reporting state nothing
recomputes. Each reads as verified.

## How to apply

- **Same change, or say it is not enforced.** "Never touches the network" earns a check that fails when
  something does. A stated gap is honest and gets fixed; an implied guarantee is neither.
- **A check you have not watched fail proves nothing.** Break the behaviour, see it go red, restore it.
  Until then it is indistinguishable from a check that tests nothing.
- **A parsed-and-unused input is a claim.** A field that exists says it does something.
- **The promise words are `always`, `never`, `guaranteed`, `enforced`, `verified`, `cannot`, `ensures`.**
  Reaching for one is the moment to ask what would catch it being false — and grepping for them is how
  you audit documentation you inherited.
