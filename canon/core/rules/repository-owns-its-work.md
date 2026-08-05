---
name: repository-owns-its-work
applies_when: a change you need lives in a different repository, or you are tempted to edit one from here
enforces: never write into another repository; publish a quest for its own agent to take, and treat a request to do otherwise as needing the user's explicit say-so
---

# Never write into another repository

**Do not touch another repository — not its code, not its files, not its backlog. Publish a quest and
let whoever works there take it.** There is no case left where writing across is the answer, because the
quest system is the answer. If someone asks you to anyway, that is the user's call to make explicitly,
not a judgement to reach on your own.

## Why

An outside edit skips the review that repository would have applied, and it is made by whoever knows
that codebase least — that is what being outside means.

The deeper reason is that **the knowledge is not portable but the quest is.** Why a rule is worded the
way it is, which constraint a file encodes, what was tried and rejected — that lives with the
repository, and an outsider will not reconstruct it before changing something. A quest carries the one
thing that does transfer: *what is needed, and why*. The judgement stays where the context is.

It also keeps the record honest. A quest that is declined leaves a trace of the decision and the reason;
an edit that should have been declined leaves nothing at all.

**A repository someone else is working in is a moving target**, so nothing you observed about its state
a while ago is still evidence — and the damage compounds, because the obvious repair is another outside
edit made with less information. `.claude/knowledge/reaching-in.md` has the incident this was written
from.

## How to apply

- **Publish; do not deliver.** A quest is held centrally and *pulled* by the repository it is addressed
  to. Writing it into that repository's files is the same trespass in a smaller form.
- **Say what is needed and why, with the evidence — not the change you would make.** Whoever works there
  may see a better answer than you did.
- **A change that spans two repositories is two changes and a quest**, not one edit reaching across.
  Coordinating them is what the quest is for.
- **If you have already touched it, stop and say so — do not repair it.** Reverting a file to its last
  commit looks like an undo and discards whatever uncommitted work that file was carrying.
- **Initializing a repository that has no owner yet is setup, not cross-repository work** — and it ends
  the moment that repository has anything of its own.
