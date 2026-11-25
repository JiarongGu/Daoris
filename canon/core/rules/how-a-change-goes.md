---
name: how-a-change-goes
applies_when: making any change beyond a typo
enforces: start at the area document, mirror one exemplar, test first, rewrite the area in the same commit, run the checks the diff reaches, land the declared way
---

## How a change goes

1. Start from your brief or row, then the area document of the part you change. Find names through the index where one
   is generated, and read the ranges it gives, not whole files. Look the library (decisions, archive, fixes) up for a
   reason, never to orient; before reversing a rule an area states, look up why it stands.
2. Mirror one exemplar; new behaviour goes in a unit of its own.
3. Write the test first and see it fail, then the code. A test guards a behaviour no other test catches and asserts relations, never a duration (`test-worth`).
4. In the same commit, rewrite each area sentence the change made false, in the present tense. Write a decision only
   for a choice that rejected an alternative; a finished row leaves the backlog for one archive line.
5. Run the checks your diff can reach (the brief names them); commit when green as `type(scope): what changed`, with
   your maker's `Co-Authored-By` line. A regression fix names its root cause and the commit that introduced it.
6. Land only the way the brief names. A red main line comes first.
