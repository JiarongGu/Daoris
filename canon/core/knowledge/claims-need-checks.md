---
name: claims-need-checks
applies_when: writing any statement about what the code does or guarantees — a readme, a doc comment, a config field, a status line
enforces: verify behavioural prose against the implementation, not the design; ship the check in the same change; say which claims the gate did not cover
---

# A statement about behaviour is verified against the code, or it is not a statement

**When you write that something is guaranteed — always, never, throws, defaults to, verified, enforced —
check it against the implementation and ship the thing that enforces it in the same change. If you
cannot enforce it yet, say so in the same sentence.**

## Why

Prose is the one surface with no compiler. A remark saying what the code does can be wrong for a whole
release with every gate green, because no gate reads it.

An unenforced guarantee is worse than none. Silence invites a reader to check; a confident sentence
tells them not to bother. So the claim outlives the behaviour and is believed for exactly as long, and
nothing reports the divergence — the whole point of the sentence was that nobody needed to look.

Carelessness is not the cause. The claim and the enforcement are written at different moments and only
the claim is easy: documentation gets written while the behaviour is fresh and obviously true, which is
the one moment a check feels redundant and the moment it is cheapest to write.

Two repositories in this family derived this independently and from opposite ends — one auditing shipped
API documentation against its own source, one finding a configuration field parsed and never read.
Neither could have found the other by searching: they share almost no vocabulary.

## How to apply

### Write from the implementation, never from the design

**A design document states intent, and intent is what the last edit before merge changes.** Prose
written from the design describes the feature that was planned. Open the file, find the line, then
write.

One audit doing exactly that found three claims that had not survived contact with the code: a case
documented as throwing that quietly succeeded with a default instead, an interface described as though
an adapter shipped alongside it when nothing implemented one, and a helper from the design that was
never written at all.

**Spend the check on the newest code.** Older areas have survived reviews; a component documented in one
burst alongside its own design has had no second reader. In that same audit the long-shipped surfaces
were clean and everything wrong was recent.

### Enforce it, or say that you did not

- **Same change, or state the gap.** "Never touches the network" earns a check that fails when something
  does. A stated gap is honest and gets fixed; an implied guarantee is neither.
- **A check you have not watched fail proves nothing.** Break the behaviour, see it go red, restore it.
  Until then it is indistinguishable from a check that tests nothing.
- **A surprising behaviour gets a test, not a corrected comment.** A comment that contradicts intuition
  gets "fixed" back by the next reader who finds it surprising. Only a test survives that.
- **Say which claims the gate did not cover.** A green build on a documentation-only change proves
  nothing about the words. Report that, rather than letting the green imply the prose was checked.
- **A parsed-and-unused input is a claim.** A field that exists says it does something.

### The expensive words

`always`, `never`, `throws`, `cannot`, `defaults to`, `guaranteed`, `enforced`, `verified`, `ensures`.
Reaching for one is the moment to ask what would catch it being false — and grepping for them is how you
audit documentation you inherited.

**"Throws" is the costliest to get wrong**, because it fails silently: a caller told to expect an error
gets a quiet default, with nothing in the log. **"Ships with X" ages badly** — X gets cut before release,
or is planned and never written. Grep for the thing before saying it exists, and if the consumer has to
supply it, say so in the same sentence.
