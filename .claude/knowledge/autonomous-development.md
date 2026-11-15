---
name: autonomous-development
applies_when: planning how a task will be run and verified, or about to pause a step to ask
enforces: the owner sets the target and judges the outcome; agents run the steps between, verified by gates and reviews; what runs unasked is the repository's declaration, and only the closed list of owner calls is asked
---
<!-- daoris: core/core/knowledge/autonomous-development.md @ 0.0.1 — canonical; edit via `daoris upstream` -->

# Autonomous development: the owner at the ends, gates in the middle

**A person states the target and judges the result; agents run the steps between, verified by gates and by
other agents' reviews, not by per-step approval. What may run unasked is one declaration the repository keeps
beside its gates, and the brief names where.**

## Why

A person approving every reversible step becomes the bottleneck on exactly the work that needed no
judgement, and a reviewer asked to confirm forty mechanical steps stops reading them, so the one step that
deserved a decision arrives to a reader trained to click through. A gate runs every time, identically; a
person verifies occasionally and tires. The owner owns the outcome and does not manage the steps, so the
process hands them an outcome to judge: a reviewable diff, green gates, two reviews and a record of what
was decided and why.

## How to apply

- **Take the target, then run.** A step the declaration covers is executed and recorded, not proposed and
  awaited. Delivery follows risk: the declaration says what may run, land and publish unasked, and no human
  step is added that it does not name.
- **Ask only the closed list.** Direction and targets, money and budgets, sign-ins and credentials, releases
  the owner marked as theirs, history rewrites, deleting their data, and publishing to people outside beyond
  the declaration. Everything else is decided, alone or with another agent, and stated at the hand-back.
- **Look before you ask.** A question the task's material, the repository's documents, code and history can
  settle is settled, with the evidence kept for the hand-back. A choice nothing settles (a name, which of two
  sound ways) is the agent's, stated the same way.
- **A question carries its analysis:** what it is, what yes does, what no does and where to look. Work that
  cannot be finished is reported as "gave up" with what was tried, never asked about.
- **Two reviews before landing,** wide (design, effects beyond the change) and close (details, edge cases,
  tests), from different agent families where two are ready.
- **Verify in proportion, and in full once at the boundary.** While working, run what the change can reach;
  run the whole set where the work leaves the run (a landing, a release). After a fix, re-run what failed.
- **Record as you go,** because nobody watches the middle: a step that exists only in a transcript did not
  happen, as far as the final verification can tell.

## The declaration

One tracked entry beside the gates, read by tools and accepted once by the person: each grant an exact act,
and the owner calls named as excluded. A session that edits it and obeys the edit in the same run is
granting itself permission, so a change to it takes effect once it has landed and the person has accepted it.
