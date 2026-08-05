---
name: repository-owns-its-work
applies_when: a change you need lives in a different repository, or you are tempted to edit one from here
enforces: work happens in the repository that owns it; a change you need elsewhere is a quest published for that repository to take, never an edit or a file written from outside
---
<!-- daoris: core/core/rules/repository-owns-its-work.md @ 0.0.1 — canonical; edit via `daoris upstream` -->

# A repository owns its own work — elsewhere, you file a request

**Do not touch another repository to unblock yourself — not its code, and not its files. Publish a
quest and let whoever works there take it.** The exceptions are narrow: initializing a repository that has no
owner yet, and a change so tightly coupled that splitting it would leave neither side working.

## Why

An outside edit skips the review that repository would have applied, and it is made by whoever knows
that codebase least — that is what being outside means. It also lands as a surprise: the owner finds a
working tree they did not touch, in files they did not choose, with no record of who decided.

The deeper reason is that **the knowledge is not portable but the request is.** Why a rule is worded
the way it is, which constraint a file encodes, what was tried and rejected — that lives with the
repository, and an outsider is not going to reconstruct it before making a change. A quest carries
the one thing that does transfer: *what is needed, and why*. The judgement stays where the context is.

This also keeps the record honest. A quest that is declined leaves a trace of the decision and the
reason. An edit that should have been declined leaves nothing at all.

**A quest is taken, not assigned** — which is the property that keeps declining a real answer. It is
called a quest rather than a task or a request because every backlog here is already full of tasks, and
a word that collided with those would be ambiguous in exactly the file where the distinction matters.

**The practice came before the rule.** Repositories in this family already did this by hand — one keeps
a "waiting on the sibling repository" section in its backlog, another separates work needing a decision
elsewhere from work it can do itself. Nobody agreed on that; it was arrived at independently because it
works. What was missing was a name, a place to put one, and a status anyone could read — not the idea.

## How to apply

- **Publish it; do not deliver it.** A quest is held centrally and *pulled* by the repository it is
  addressed to. Writing it into that repository's files yourself would be the same trespass in a smaller
  form — it is still an outside edit, still unreviewed, and still made by whoever knows that codebase
  least. Say what is needed and why; do not prescribe the change.
- **Only a repository that has adopted can be addressed.** One without the client has no way to see what
  was asked, so the quest sits in a queue nobody opens — which is indistinguishable from having been
  read and ignored.
- **Include the evidence, not the conclusion.** The measurement, the failing case, the two documents
  that overlap — whoever works there needs the reason more than the instruction, and they may see a
  better answer than you did.
- **Say who asked and when, and keep the status current.** Open, taken, done, declined — four states,
  because anything finer is status for its own sake. Undated and anonymous, a quest becomes a chore
  nobody can judge on its merits, and the safest thing to do with it is nothing.
- **Declining needs a reason.** It is a real answer and often the right one, but a bare refusal tells
  the asker nothing they can act on.
- **Publishing is proposing.** The receiving repository decides whether it becomes a change, and its own
  commit is where that happens.
- **Initializing is the exception**, and it ends the moment the repository has anything of its own —
  after that it has an owner and the rule applies.
- **So is a genuinely inseparable change.** Two sides of one contract that must move together are one
  change; make it together, deliberately, and say so. A change that merely *touches* two repositories
  is not this — that is two changes and one request.
