---
name: current-knowledge
applies_when: writing or changing anything that describes the code or the project (a decision's note, a readme, a knowledge document, an archive entry, an agent's memory entry, a structural test), or finding one that no longer matches
enforces: write only what the code cannot say; one home per current fact, rewritten when it changes, with what it replaces retired in the same step; history apart from the present; tests pin behaviour and rules, never incidental shape
---

# Keep what is written current: replace it, don't pile up beside it

**Write down only what the code cannot say. Give each current fact one home; when the fact changes,
rewrite that home and retire what it replaced. A test pins behaviour or a rule, never the shape the code
happens to have today.**

## Why

Writing that restates the code has to be edited every time the code changes, and the copies nobody edits
go stale without a sound. In one repository a day's work added about two characters of hand-written prose
for every three of production code, and twice as much test code as production code. Much of it restated
the code: a component's readme gained a sentence per change until one of its table rows ran to eight
thousand characters and conflicted in most merges; each step's decision note listed the tests it ran, with
counts, times and line numbers; structural tests pinned how many call sites a file held and how long it
was. Every change then paid to keep that writing in step, and the work slowed under it.

Accretion is the quieter half. A record that only grows (a note added per step, a memory entry added
beside the one it contradicts) makes a reader replay its history to learn the present, and a reader who
stops early believes the old fact. A search returns the old and the new side by side with equal weight,
and nothing reports that they disagree. Both failures look like diligence, which is why they persist.

## How to apply

### Write what the code cannot say

- **Keep** why a choice was made, what was rejected and why, the contract between two parts, what the
  checks do not cover, and the trap that succeeds wrongly.
- **Leave out** what each member does (its code and its name say it), line numbers and counts (they move
  with every edit, and a generated index says where things are), and the tests run with their counts and
  times (the commit or the hand-back holds them; they are history by the next day).
- **Size a record by what was decided, not by the work it took.** A long sitting that settled one thing
  earns one sentence.

### One home for the present, history apart

- **The current state lives in one place, rewritten in place when it changes**: how a component works, what
  a setting means, which rule holds. History (what was decided, when and why) is appended and read by
  lookup. An amendment log is not a description; a reader should never have to replay one to learn what
  is true now.
- **When a change makes a sentence wrong, rewrite that sentence.** Do not add a newer one beside it.
- **A document about a component describes its shape**, which changes rarely, never each change to it.

### Replace and retire, in memory too

- **A fact that contradicts an older one replaces it in the same step.** Rewrite or remove the old entry;
  where its history matters, the record of the change says what it replaced. Two entries that disagree are
  the defect, whichever of them is right.
- **This holds for an agent's memory and a knowledge store as much as for files.** Before adding an entry,
  search for the one it updates and replace that one. An entry that can be checked says how; when the
  check fails, the entry is retired, not left to be believed.
- **Prefer a home that a check keeps true** (a test, a generated index, a declaration a tool reads) over
  one that can only be believed.

### Tests pin behaviour and rules, not incidental shape

- **A test that fails on unrelated edits gets updated by rote, and a test updated by rote has stopped
  guarding anything.** The usual shapes: a count of call sites, a file's length or a line number, a whole
  message where one phrase is what a reader relies on.
- **Pin the rule itself**: "nothing outside this module calls that", named; an allow-list whose entries
  each carry their reason; the one phrase a reader depends on.
- **Where structure must be pinned**, such as a census of a dangerous call, it fails only on the change it
  exists to catch, and its failure says what to do.

### When you find something stale

- **Fix its one home, not a copy**, and delete what nobody reads.
- **A document that drifts again and again is restating something.** Move that part into the code, a test or
  a generated index, and keep only the reason in prose.
