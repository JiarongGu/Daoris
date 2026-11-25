# <Area> — <what it holds, in a few words>

<Two or three sentences: what this part of the code is for, what it owns, and what it must not depend
on. What is true now, said once: no history, no status words. A change that alters it rewrites the
sentence here.>

## Where it is

| What | Where |
|---|---|
| <an entry point, a seam, a key type> | `<path>:<lines>` |

## How a change here is made

- Exemplar: `<path>:<lines>`, the unit a new one mirrors.
- New behaviour goes in `<folder>`, a unit of its own named for its job.
- Tests: `<path>`, run with `<the filtered command>`.

## Adding to it

<One row per kind of addition this part takes (a gate, a route, a setting, a catalogue key, a plugin), so "where does a new
X go" is answered here and nowhere else. A row names the place and the registration and points at the rule above; it does
not restate it. A part that takes no addition says so in one sentence and keeps no table. The index gathers every area's
rows into one page.>

| Kind | Place | Registered by | Fails until |
|---|---|---|---|
| <a kind of addition> | `<the file or table that holds it>` | <the registration it needs, and where> | `<the census or test that fails until it is there>` |

## What holds here

- <An invariant a change must keep, in one line, and the decision that chose it where one did.>

## Traps

- <What went wrong here and would again: the symptom and its cause, in a line.>
