---
name: evidence-routine
applies_when: at the end of a working day, after a run of failures, or when the same kind of defect has been fixed twice
enforces: the day's failures written as one list from the records, each with its root-cause class and the backlog row that closes the class, never only the instance
---

# The evidence routine: a day's failures become rows that close their class

**At the end of a working day, list the day's failures from the records, not from memory. Give each its root-cause
class, and name the backlog row that closes that class: a check that catches every member, then a sweep for the
siblings already in the tree.**

## Why

A failure fixed alone comes back as its sibling. A day that repaired four defects one by one found the fifth the
next morning: the same reader missed in another place, the same wait left with no one holding it. Each repair was
right, and none asked what kind of defect it was, so nothing stopped the kind.

Memory picks the failures that were loud. The quiet ones (a run someone stopped by hand, a wait nobody held, a
refusal that was routed around) are in the records and nowhere else, and they are the ones that recur, because no
one felt them.

## How to apply

1. **Read the day from the records.** Each kind the repository keeps, at least:
   - runs or sessions that ended because someone stopped them, not because they finished;
   - waits with no holder: a step, request or question that waited on nobody, or on someone who never knew;
   - reds on the main line, each with the change that turned it red;
   - review findings rated high;
   - acts that were refused, and what was done instead.
2. **One line each.** What happened, where it is recorded, and its **root-cause class**: the general shape, named
   so a check could look for it (a reader that misses a new kind of record; a wait whose holder is never told; a
   gate that runs where the failure cannot occur).
3. **One row per class, not per failure.** The row names the check that catches every member of the class and the
   sweep for members already present. A failure whose class already has a row is added to that row as evidence.
4. **Proposed, then accepted.** The list proposes the rows; whoever keeps the backlog accepts, merges or declines
   each, with its reason. A declined class is said once, so the next day's list does not propose it again.

Where no tool gathers the records, the list is written by hand from the same sources; the routine is the list and
its classes, not the tool.

## What it is not good at

- A class named too wide ("bugs in the reader") closes nothing; name it so a check could be written for it.
- It sees only what the records keep. A failure no record holds is a finding of its own: a record to add.
