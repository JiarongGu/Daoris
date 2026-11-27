---
name: family-pins
applies_when: this repository builds against another repository of the same product, or another builds against it
enforces: a pin is one landed commit of the producer, never a branch; a pin move is a change of its own; shared code moves in three changes
---

# A family pins landed commits, and moves a pin as a change of its own

**A repository that builds against another pins one commit of it, landed on its line, and builds against that
commit, never against the neighbour's checkout as it happens to stand.**

## Why

A branch, a tag or "latest" names a different commit tomorrow, so a build that was green turns red with no change in the
repository that failed. A pin moved inside a feature hides what the move forced; a sibling checkout read as it stands makes
one machine's build differ from another's.

## How to apply

- **Never pin a branch, a tag or "latest".** The pin is one full commit id on the producer's line, kept in the consumer's own
  lock file, one pin for each producer. Two consumers may pin different commits.
- **A pin move is a change of its own.** It changes the lock and only what the move forces in this repository, never a
  feature. Run the checks at the new commit before landing it. If they stay red, leave the pin where it was and say the
  failing case to the producer's owner: the line stays green, and the producer fixes or says what to change.
- **Moving code into a shared repository is three changes, in order:** the shared repository gains the code with its tests
  and attribution; each consumer moves its pin to that commit; each consumer deletes its copy, its tests unchanged.
- **A feature across repositories lands its consumer part with the pin moved to the commit the producer's part landed.**
  Never land the consumer first and move the pin later, and never pin a commit that is not on the producer's line.
- Never write into another repository to move its pin: ask its owner, with the commit and the evidence.

Where a tool checks the family, run it before landing a pin move; where none does, read each pin against its producer's
history by hand.
