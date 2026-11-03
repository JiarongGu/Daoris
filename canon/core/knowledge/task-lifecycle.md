---
name: task-lifecycle
applies_when: adding or finishing a task, or editing the backlog
enforces: the backlog holds open work only, a row of a line or two; a finished task leaves it and the archive gains one line
rule: true
---

# Task lifecycle — the backlog is open work; finished work moves to the archive

**The backlog lists what is still to do. When a task is finished, its row is cut from the backlog and
the archive gains one line for it, in the same change or its follow-up.**

## Why

A backlog that keeps completed items stops being scannable, and the next open task stops being obvious.
A session pays for what it reads on every step after, so a long row is paid for by everyone who picks
work, and an archive entry that retells a decision by everyone who looks it up.

## How to apply

- **A backlog row is a line or two:** an identifier, what is needed and why, and its proof, with a
  `file:line` where known. The detail stays where it lives.
- **On completion, move; never tick.** Cut the row, and add one archive line: the identifier, the date,
  and the outcome with where its detail lives (the commit, the decision, the area document).
- **Three records, three jobs:** the backlog is what is still to do, the archive what was done, the
  changelog what a user of a release sees changed. What a decision, a design or a commit already says
  is pointed to, never told twice.
- **Keep the summary honest.** Never a stale "all done" banner over open items, nor open items under a
  "done" banner.
- Picking up work you cannot finish now? Leave it open in the backlog.
