---
name: autonomous-development
applies_when: planning how a task will be run and verified, or about to pause a step to ask
enforces: the owner sets the target and judges the outcome; agents run the steps between, verified by gates and reviews; whether an act needs a yes, and whose, is the repository's risk policy, and only what it classes for the owner is asked
---

# Autonomous development: the owner at the ends, gates in the middle

**A person states the target and judges the result; agents run the steps between, verified by gates and by
other agents' reviews, not by per-step approval. Whether an act needs a yes, and whose, is one risk policy the
repository declares, and the brief names where.**

## Why

A person approving every reversible step becomes the bottleneck on exactly the work that needed no
judgement, and a reviewer asked to confirm forty mechanical steps stops reading them, so the one step that
deserved a decision arrives to a reader trained to click through. A gate runs every time, identically; a
person verifies occasionally and tires. The owner owns the outcome and does not manage the steps, so the
process hands them an outcome to judge: a reviewable diff, green gates, two reviews and a record of what
was decided and why.

## How to apply

- **Take the target, then run.** A step the policy passes is executed and recorded, not proposed and
  awaited. Delivery follows risk: the policy says what may run, land and publish unasked, and no human
  step is added that it does not name.
- **Ask only what the policy classes for the owner.** By default direction and targets, money and budgets,
  sign-ins and credentials, releases the owner marked, history rewrites, deleting their data, publishing outside,
  and a change to the policy itself; the list is a default the owner may move, not a law. Everything else is
  decided, alone or with another agent, and stated at the hand-back.
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

## The policy

One record, read by one judge: each kind of act has a class at a place: none (it passes, recorded), a role's yes
(never the act's author's) or the owner's own. Where it declares no risk there is no gate. A change to the policy
itself is the one fixed owner act: it takes effect on the owner's own approval in their own channel, never on an
agent's words, a commit or a file, since a session that could change who approves could approve anything.

Without the tool the policy degrades to a declaration, one tracked entry beside the gates, read by tools and accepted
once by the person: each grant an exact act, and the owner calls named as excluded. A session that edits it and
obeys the edit in the same run is granting itself permission, so it takes effect once it has landed and the person
has accepted it.
