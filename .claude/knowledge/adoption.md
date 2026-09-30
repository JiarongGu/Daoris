---
name: adoption
applies_when: onboarding a repository onto daoris for the first time
enforces: the adopting repository's agent executes the whole flow and the owner reviews the final diff; resolve collisions deliberately; hunt renamed twins by hand; preserve repo mechanics locally
---

# Adopting a repository — the playbook, learned from the first one

Adopting is not `sync`. `sync` is the mechanical part; the work is deciding what happens to the doctrine
the repository already had.

**The flow is agent-executed, end to end** (`autonomous-development`): the owner's part is the two
checkpoints — saying "adopt Daoris here" at the start, and reviewing the uncommitted diff at the end.
Every step between, including the judgement calls below, is the adopting repository's own agent working
from `analyze --json` and this playbook. What makes that safe is that nothing destructive can happen
without `--force`, and `--force` is only ever the answer to a question the tool asked.

## Why

The first adoption (a released .NET library) surfaced three things no synthetic test had: four collisions
on rules the repository already owned, a renamed twin the tool structurally cannot see, and a real 45%
budget overage. All three are normal. Expect them.

## How to apply

### 1. `init`, then read what it prints

`daoris init` lists the available packs *and* every document the repository already owns. That second
list is the adoption plan — each entry is something that will either collide, become a renamed twin, or
stay local.

### 2. `sync --dry-run` and read the collisions

A `COLLIDES` line means the repository wrote that file itself, before it ever heard of Daoris. Nothing is
overwritten. For each one, open both versions and separate:

- **The principle** — almost always already in the canonical version, often better generalized.
- **The mechanism** — the commands, paths, guards, and version policy specific to this repository. This
  is what would be *lost*, and it is usually the most load-bearing content in the file.

### 3. Preserve the mechanism in a local companion

Write the repository-specific mechanics into one local document — `.claude/knowledge/repo-mechanics.md`
works well — that says plainly: the canonical rules state the intent, this file states how it is
enforced *here*. It is local, so Daoris never touches it, and the index lists it marked `(local)`. A
mechanic every task needs goes in the repository's own part of `AGENTS.md`, outside Daoris's region.

This is the whole point of the three-layer model. A repository that loses its release policy to a generic
rule has been made worse by adoption.

### 4. Hunt renamed twins by hand

The tool **cannot** find these. A repository's `minimise-bash-prompts` and canonical
`file-tool-discipline` are the same rule under different names; the twin is local, and local is invisible
by design. After syncing, read the generated index end to end and look for two rows saying the same
thing. Delete the twin, and check whether the repository's entry document referenced it by name.

**`doctor` narrows this job; it does not replace it.** It compares vocabulary, so it finds a twin that
was *reworded* and misses one that was *rethought*. The clearest case in the survey — a rule present in
three repositories whose first half is canonical `no-tmp-for-repo-files` and whose second half is
canonical `file-tool-discipline` — scores **24% and 23%**, inside the unrelated band, because it reaches
both principles through an entirely different vocabulary (allow-lists, tooling directories, a `cd`
prefix). No threshold separates that from an unrelated pair, so lowering one buys noise (D17).

**A merged twin is not deleted.** Two harder shapes turn up, and the instinct to delete is wrong for
both:

- **One local rule that is two canonical rules combined.** Both halves are now canonical, so the file
  goes — but read it for the third thing it is carrying. That example also documents which allow-list
  entries exist and how a `cd` prefix defeats them, which is this repository's own mechanics and is
  nowhere in the canon. Move that to a local document *before* deleting, or adoption quietly costs the
  repository something it knew.
- **One local rule that is mostly canonical plus a genuine deep dive.** Retire the always-loaded rule and
  let the deep dive live in the on-demand tier, where it belongs — it was never something every task
  needed. This is the shape that pays: it removes always-loaded bytes without losing a sentence.

The test for anything you are about to delete is not "is this canonical now" but **"is every line of it
somewhere else."** Check each section against the canon *and* the repository's own knowledge tier, and
move what only exists here. A twin removed correctly costs nothing; one removed carelessly loses exactly
the hard-won specifics that were never going to be canonical.

### 5. `sync --force`, then `check`

`--force` here means "yes, take the canonical version" — a deliberate answer to a question that was
asked, not a way to skip it.

### 6. Write the brief

The sync just installed the core skill `set-up-documents` and the knowledge behind it,
`development-documents` (D122). Follow the skill from here to step 8; its templates are beside it.

Write the repository's own part of `AGENTS.md`, above the region, from `templates/brief.md`. Move into
it what every agent needs from an existing `CLAUDE.md`: codex reads only `AGENTS.md`, and only up to
32,768 bytes (LAYOUT2), so a rule left in `CLAUDE.md` never reaches it. Leave `CLAUDE.md` holding the
import and what only Claude Code needs. A line goes in the brief only if nearly every task needs it and
nothing in the code would tell a reader; the rest goes to the tier that holds it. Moving is not
trimming: every line that leaves the brief has a new home, and the hand-over names it (step 11).

### 7. Declare the documents and the rooms

- **Rooms**: a folder whose conventions, traps or checks differ gets its own `AGENTS.md`, from
  `templates/room.md`, and is listed in `daoris.json`'s `rooms` (D117 §2.2). `sync` writes its pointer
  and lists it in the region, and `check` fails on a declared room with no `AGENTS.md`.
- **Records**: each record the repository keeps, by role (router, decisions, backlog, archive, fixes,
  changelog, glossary), with its path. D122 §2.7 declares them in `daoris.json`'s `documents`, which
  `sync` renders as the region's *Where things are* table; the CLI reads that field once DOC3 lands and
  ignores it before, so until then the brief's own *Where things are* section is the declaration,
  written by hand.

### 8. Declare the safe work

What the repository's sessions may run without asking: the gates that are a session's to run (not a
rehearsal, not one that needs the machine quiet), the build and test commands, the install from the
lockfile. Exact commands, one per entry, and none of `autonomous-development`'s carve-outs. It goes in
`daoris.gates.json` as `safe`, beside `gates` (D122 §3.1), never in the brief's prose: a sentence shapes
what an agent tries, not what its harness allows. The brief's *Build, test, verify* says where it is.
Nothing is widened by writing it. The driver reads it from the repository's line, not a session's tree,
once UNBLOCK2 lands, and a person accepts it once (UNBLOCK3); say so in the hand-over.

### 9. Expect the budget to be over, and do not paper over it

The always-loaded core is measured for the first time at this moment, and it is usually larger than
anyone thought. It is **reported, never enforced** — a fact gates and a judgement reports — so nothing
stops; the number is there to be answered rather than silenced. Two honest responses:

- **Trim** — usually a long local rule that duplicates a canonical one, or a deep dive sitting in the
  always-loaded tier that belongs in `knowledge/`.
- **Raise the budget to the true number** and record the overlap as a task.

Both are legitimate. What is not legitimate is trimming someone's doctrine as a side effect of adopting a
tool — that is editorial work and deserves its own review.

### 10. Verify the repository, not just the doctrine

Run the adopting repository's own build and tests. Adoption changes what every future session in that
repository reads, so "the tool exits 0" is not the same as "the repository is fine".

### 11. Leave it uncommitted for review — this is the human checkpoint

Adoption rewrites always-loaded context. The owner should see the diff before it becomes history — and
under the automation-first model this is where their attention is spent, so hand them the whole outcome
at once: the diff, the gate results (`check`, the repository's own tests), which collisions were resolved
and how, which twins were retired and where each preserved line went, what the brief took in and where
each line it let go now lives, the documents and rooms declared, the safe work declared and that it waits
for their yes, and what the budget reads now, with the root file's bytes beside it.
A judgement call worth surfacing — a twin that might not be one, a budget that had to rise — is stated
here as a decision with its reasoning, not asked mid-flow.
