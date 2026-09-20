---
name: repository-owns-its-work
applies_when: a change you need lives in a different repository, or you are tempted to edit one from here
enforces: never write into another repository; send the request to whoever works there, and treat writing across as needing the user's explicit say-so
---

# Never write into another repository

**Do not touch another repository — not its code, not its files, not its backlog. Publish the request
and let whoever works there take it** — as a quest where a request system exists, and as a message to
that repository's owner where none does. Either way the request travels and the edit does not. If
someone asks you to write across anyway, that is the user's call to make explicitly, not a judgement to
reach on your own.

## Why

An outside edit skips the review that repository would have applied, and it is made by whoever knows
that codebase least — that is what being outside means.

The deeper reason is that **the knowledge is not portable but the request is.** Why a rule is worded the
way it is, which constraint a file encodes, what was tried and rejected — that lives with the
repository, and an outsider will not reconstruct it before changing something. A request carries the one
thing that does transfer: *what is needed, and why*. The judgement stays where the context is.

It also keeps the record honest. A declined request leaves a trace of the decision and the reason; an
edit that should have been declined leaves nothing at all.

**A repository someone else is working in is a moving target**, and the repair is usually worse than the
edit that prompted it. The on-demand document on reaching in carries the incident this was written from.

## How to apply

- **Publish; do not deliver.** A request is held where its receiver will look for it, and *pulled* by
  them. Writing it into that repository's files is the same trespass in a smaller form.
- **Say what is needed and why, with the evidence — not the change you would make.** Whoever works there
  may see a better answer than you did.
- **A change that spans two repositories is two changes and a request**, not one edit reaching across.
  Coordinating them is what the request is for.
- **If you have already touched it, stop and say so — do not repair it.** The owner can, with context
  you do not have.
- **Initializing a repository that has no owner yet is setup, not cross-repository work** — and it ends
  the moment that repository has anything of its own.
