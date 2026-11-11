---
name: change
description: The one way to make a change in this repository, from reading the area to the commit. Use for every change that is more than a typo, before reading any code.
---
<!-- daoris: core/core/skills/change/SKILL.md @ 0.0.1 — canonical; edit via `daoris upstream` -->

# change

One way to make a change, in five steps. Nothing else is asked of a change unless a gate asks for it.

## Steps

1. **Read the area.** Open the area document for the part you change; the brief names the area
   documents. Read the lines of code it points at: ranges, not whole files. With no area document,
   read that part's own instructions or README. A knowledge document whose *applies when* matches the
   task is read too; the generated index lists them. Search what is true now (the area documents, the
   index, the code) before the records, and look history up by its identifier.
2. **Mirror one exemplar.** Search for one unit that already has the shape you are adding, read it end
   to end (its wiring, its error path, its test) and mirror it. New behaviour goes in a unit of its own,
   a named type or module in the folder that owns its job: never a partial piece of a large file, never
   a branch added to a unit that does something else. A file over the repository's size budget does not
   grow; what it would gain goes in a new unit beside it.
3. **Test first.** Write the test, run it and see it fail; a test that cannot compile without the code
   has failed too. Then write the code. Run the tests that cover what you changed, filtered to them, and
   the repository's quick checks, which the brief names. A whole suite is the integration gate's job.
4. **Rewrite the record in place.** Where behaviour someone relies on changed, rewrite the area
   document's sentence that describes it: what is true now, said once, with no history and no status
   words. A choice that rejected an alternative gets a decision entry naming what it rejected. Nothing
   else is written by hand.
5. **Commit.** One commit per piece that passes its tests, as `type(scope): what changed`. A regression
   fix's body names the root cause (the mechanism, not the symptom) and the commit that introduced it.

A gate that fails says what to add and where: add exactly that.

## Why

Each step used to be a procedure of its own (find the documents, find the exemplar, audit the
finished work, record the fix), and each was either skipped or done twice. A session pays for what it
reads on every step after it reads it, so what it loads before its first edit is the largest cost of a
change, and a record that retells another is paid for by every later reader. One area document read at
the lines it names, one exemplar, a test that was seen to fail, and a sentence rewritten in place is
the whole of what a later session needs. A second convention, a hub that grows, or a record that tells
history is what makes the next change cost more than this one did.
