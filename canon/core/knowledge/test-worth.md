---
name: test-worth
applies_when: writing, changing, cutting or quarantining a test, writing the test of a bug fix, or reviewing a change whose tests grew faster than its code
enforces: a test guards a behaviour a plausible change could break and no other test catches; it asserts relations, never a value the project chose or a wall-clock duration; a fix's test fails without the fix; cutting or quarantining tests is a change of its own that lists each one
---

# A test earns its place, and says what it guards

**Write a test for a behaviour a plausible change could break that no other test would catch. Assert how
things relate, not what the project happens to have chosen. Never assert how long something took.**

## Why

A test is code every later change pays for: it runs, it breaks, it is read and it is updated. A test that
guards nothing a change could plausibly break still charges all four, and the charge is hidden because a
green run looks identical with it and without it. One repository's tests grew to twice its production code, and
a large part of them restated the code (a count of call sites, a whole message where one phrase mattered, the
value a setting held on the day it was written). Each failed on edits that broke nothing, and a test that fails
on unrelated edits is updated by rote and so stops guarding anything (`current-knowledge`).

Time is the sharpest case, because its failure is silent in the other direction. An assertion that something
finished in under a second passes on a free machine and fails on a loaded one, and a change cannot break it:
what it measures is the machine. It trains the reader to rerun a red result until it is green, which is how a
real regression gets rerun away. Time is a budget, and a budget belongs to the gate that judges durations
across the whole suite, where one slow test is named and ranked rather than failed by a coin toss.

## How to apply

- **Ask first what plausible change the test would fail on, and what else would catch it.** If no change
  could break it, or another test already fails on the same change, the test is not earning its place. Write
  the one that does, and name the behaviour in its title.
- **Assert relations, not values the project chose.** Prefer: inside, wider, changed, unchanged, before,
  after, at most, the same as the input said. A relation survives a retuned constant; the value itself is
  pinned only where it is the contract (a protocol, a stored format, a documented limit).
- **Never assert a wall-clock duration.** Not an elapsed time, not a stopwatch reading, not "within N
  seconds", not a reading of one clock against another. Instead:
  - **A relation the clock only measures.** "It did not wait" is the work's own outcome (the held answer came
    back while the work was still running, the cache was read and no process started); assert that.
  - **A clock the test moves.** Where the code takes its clock or its sleep from a seam, hand it one the
    test advances; the assertion is then exact and takes no time.
  - **The budget gate.** Where speed is the claim, leave it to the gate that judges test durations. A
    wait that must end has a bound the test's own timeout holds, and a failure of the bound is a hang,
    not an assertion.
  - **Where a clock really is the behaviour** (a bench at scale, a lower bound on a real wait), the test
    says so in a list kept beside the check that finds such assertions, each with its reason, and the
    list only shrinks.
- **A bug fix's test fails without the fix.** Run it against the code as it was before the fix and see it
  fail for the reason the bug had; a test written after the fix, never seen red, proves only that it passes.
- **Cutting or quarantining tests is a change of its own.** It lists each test it removes, skips or marks
  flaky, and says for each why it guards nothing or what now guards it. A deletion folded into a feature, or
  a skip with no owner and no date, is a loss nobody reviewed.
