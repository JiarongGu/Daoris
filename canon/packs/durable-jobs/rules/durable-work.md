---
name: durable-work
applies_when: starting work that outlives the request that asked for it, or adding a kind to an existing job system
enforces: long work is a job, not a blocked call; jobs resume from a checkpoint after a restart; capacity is per lane; a new kind is a handler plus a registration, never a conditional
---

# Long work is a job — and a job survives a restart

**Work that can outlive the request is dispatched, not awaited. It records enough to resume from a
checkpoint after the process dies. Capacity is bounded per lane rather than globally. Adding a kind is a
new handler and one registration.**

## Why

Three applications in this family built one of these independently, and converged on the same shape
because the alternatives fail the same way each time. Awaiting long work inside a request blocks the
caller until it times out, and the user sees a frozen interface rather than a slow one. A single global
concurrency limit either starves fast work behind slow work or runs the slow kind at a parallelism the
machine cannot take. And a system that cannot resume turns every restart into lost work, which is worst
precisely when restarts are most likely.

## How to apply

- **Dispatch and return; never block the caller.** The request records the job and answers immediately.
  Anything that reports progress does so out of band, and the store of record is the backend — a UI that
  keeps its own copy of what is running disagrees with reality after the first reconnect.
- **Throttle progress.** Reporting per item is convenient at the call site and produces a message storm
  under a large batch, which costs more than the work being reported on.
- **Lanes, with independent caps.** Group by what a job *contends for* — network, disk, GPU, a remote
  rate limit — and bound each separately. One global limit cannot express "eight downloads or two
  transcodes", which is the actual requirement.
- **Checkpoint so resume is cheap and correct.** Record enough to restart from the last completed stage,
  and sweep the staging area on startup so a partially written artifact is never mistaken for a finished
  one. Resume must survive an ordinary shutdown *and* a kill; only the second one gets tested by
  accident.
- **Guard against a crash loop on resume.** A job that kills the process is retried on the next start,
  which kills the process again. Count attempts per job and quarantine one that keeps failing —
  otherwise the system cannot start, and the reason is a job that only exists because it failed. One
  application here hit this; another had already predicted it and left the gap recorded, which is how it
  came to be written down.
- **A new kind is a handler plus a registration.** If adding one means editing a conditional, the seam is
  in the wrong place — the same variation-point rule that applies to every other pluggable set.
- **Not everything is a job.** Work that is atomic and near-instant — a rename within one volume, say —
  costs more to enqueue, persist and dispatch than to do. Make the cheap path inline and reserve the job
  system for work that actually moves bytes or takes time.
- **Fan-out multiplies the limit you set.** A job that spawns children raises real parallelism above the
  lane cap unless children are counted against it. The failure surfaces as a resource exhausted far from
  the code that asked for the work.
- **Deduplicate on the whole identity, not part of it.** A key that omits the parameters treats "do X to
  A" and "do X to B" as the same pending job and silently drops one.
