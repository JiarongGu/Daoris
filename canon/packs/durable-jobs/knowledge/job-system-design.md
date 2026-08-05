---
name: job-system-design
applies_when: building a durable job system, or adding parent/child fan-out, capacity governance, or resume to an existing one
enforces: one consumer loop over a mailbox rather than a task per item; a container job is bookkeeping, not work; back off from measured pressure, not from a guessed constant
---

# Building the job system itself

The invariants are in the `durable-work` rule. This is the shape underneath them — the decisions three
independent implementations made the same way, and the ones they each got wrong first.

## One loop over a mailbox, not a task per item

The obvious implementation starts a task per queued item and lets the runtime schedule them. It gives
away the two things the system exists to provide: nothing owns the count of what is running, so capacity
cannot be enforced, and nothing owns the ordering, so priority cannot be.

The shape that works is an actor: an unbounded channel with a single reader, one loop that receives
messages, mutates state, and pumps. Enqueue, cancel, finish and reconfigure all become *messages* rather
than direct state changes, so there is no lock and no race — every mutation happens on one thread by
construction. The pump then walks each lane while it has capacity and a runnable item.

This also makes cancellation honest. A cancel is a message the loop applies to its own state, rather than
a flag some other thread races the scheduler to observe.

## Priority is a comparison, not a queue per priority

Separate queues per priority level starve the low one and multiply the pumping logic. One ordered
comparison — the field that matters, then submission time as a tiebreak — keeps arrival order stable
within a level, which is what users actually notice.

## A container job is bookkeeping

Fan-out wants a parent that reports aggregate progress. The parent is **never dispatched** — it has no
handler and no work of its own, and a worker that picks it up either does nothing or fails looking for
one. Skip it at dispatch and let it complete when its children do.

Its children must count against the same lane capacity as anything else. This is the single most common
way a carefully chosen limit stops meaning anything: the cap holds for top-level jobs and the fan-out
runs underneath it.

## Back off from what you measure

A fixed concurrency number is a guess about a machine you have not seen, and the interesting failures are
on machines unlike the developer's. Prefer a governor that reduces capacity when it observes pressure —
failures, timeouts, a remote rate limit, memory — and restores it when the pressure clears.

**A rate limit is not a terminal state.** It is the system saying "later", and a job marked failed on a
429 is work thrown away that would have succeeded in a minute. Retry with a cooldown; reserve terminal
failure for errors that will not resolve on their own.

## Resume is the part that is never tested by accident

Ordinary shutdown gets exercised constantly. The kill path does not, and it is the one that matters —
restarts are most likely when something is already wrong.

- **Sweep staging on startup.** A partially written artifact left by a killed process must never be
  mistaken for a finished one. Write to a temporary name and rename on completion, so presence at the
  final path *means* complete.
- **Resume to the start of a stage, not the middle.** Restarting a download is cheap and correct;
  resuming halfway needs bookkeeping that itself has to survive the crash.
- **Test it by killing the process**, not by calling shutdown. They exercise different code, and only one
  of them is the case you are trying to survive.

## Filesystem targets, if jobs write files

Two limits bite in different places: a single name component has a byte limit, and so does the whole
path — and both are *byte* limits, so non-ASCII names reach them at a fraction of the character count.
A deep root plus long names is where this lands, and the failure is a write error far from the code that
chose the name.

Do the de-duplication and the length fitting in **one** place that reserves room for the suffix it may
add. Two functions that each fit a name independently produce a name that fits until the other one
appends to it. Neutralise reserved device names while you are there — they are case-insensitive and apply
with any extension.
