---
name: repository-owns-its-work
applies_when: a change you need lives in a different repository, or you are tempted to edit one from here
enforces: work happens in the repository that owns it; a change you need elsewhere is a request in that repository's backlog, never an edit made from outside
---
<!-- daoris: core/core/rules/repository-owns-its-work.md @ 0.0.1 — canonical; edit via `daoris upstream` -->

# A repository owns its own work — elsewhere, you file a request

**Do not edit another repository to unblock yourself. File the change as a task in *its* backlog and
let whoever works there decide.** The exceptions are narrow: initializing a repository that has no
owner yet, and a change so tightly coupled that splitting it would leave neither side working.

## Why

An outside edit skips the review that repository would have applied, and it is made by whoever knows
that codebase least — that is what being outside means. It also lands as a surprise: the owner finds a
working tree they did not touch, in files they did not choose, with no record of who decided.

The deeper reason is that **the knowledge is not portable but the request is.** Why a rule is worded
the way it is, which constraint a file encodes, what was tried and rejected — that lives with the
repository, and an outsider is not going to reconstruct it before making a change. A request carries
the one thing that does transfer: *what is needed, and why*. The judgement stays where the context is.

This also keeps the record honest. A request that is declined leaves a trace of the decision. An edit
that should have been declined leaves nothing at all.

## How to apply

- **Write the request in the receiving repository's backlog**, as an ordinary item that repository can
  work, decline, or ask back on. Say what is needed and why; do not prescribe the change.
- **Include the evidence, not the conclusion.** The measurement, the failing case, the two documents
  that overlap — whoever works there needs the reason more than the instruction, and they may see a
  better answer than you did.
- **Say who asked and when.** Undated and anonymous, a request becomes a chore nobody can judge on its
  merits, and the safest thing to do with it is nothing.
- **Leave it uncommitted.** Filing is proposing. The repository's own commit is where it becomes real.
- **Initializing is the exception**, and it ends the moment the repository has anything of its own —
  after that it has an owner and the rule applies.
- **So is a genuinely inseparable change.** Two sides of one contract that must move together are one
  change; make it together, deliberately, and say so. A change that merely *touches* two repositories
  is not this — that is two changes and one request.
