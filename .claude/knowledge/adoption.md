---
name: adoption
applies_when: onboarding a repository onto daoris for the first time
enforces: the adopting repository's agent executes the whole flow and the owner reviews the final diff; resolve collisions deliberately; hunt renamed twins by hand; preserve repo mechanics locally
---

# Adopting a repository — the playbook, learned from the first one

Adopting is not `sync`. `sync` is the mechanical part; the work is deciding what happens to the doctrine
the repository already had.

**The flow is agent-executed, end to end** (`autonomous-development`): the owner's part is the two
checkpoints — saying "adopt Daoris here" at the start, and reviewing the outcome at the end (step 12).
Every step between, including the judgement calls below, is the adopting repository's own agent working
from `analyze --json` and this playbook. What makes that safe is that nothing destructive can happen
without `--force`, and `--force` is only ever the answer to a question the tool asked.

**A repository Daoris drives is set up by a quest** (D117 §6, D124 §2): `daoris-driver setup <repository>`
publishes one to that repository, and its own session carries it on its branch. That session cannot read
this file, which is Daoris's own, so the quest's body is this playbook in the canon's words
(`SetupBrief` in the driver). The two are twins: a test holds that the body names each step below, so a
step added here fails until the body says it too.

## Why

The first adoption (a released .NET library) surfaced three things no synthetic test had: four collisions
on rules the repository already owned, a renamed twin the tool structurally cannot see, and a real 45%
budget overage. All three are normal. Expect them.

## How to apply

### 1. `init`, then read what it prints

`daoris init --harness agents` writes the manifest on the layout every agent reads (D117: knowledge and
skills under `.agents/`, a copy of each skill under `.claude/skills/` for the agent that reads only there)
and lists the available packs *and* every document the repository already owns. That second list is the
adoption plan — each entry is something that will either collide, become a renamed twin, or stay local.
A document of the repository's own under `.claude/knowledge/` or `.claude/skills/` is named with the
`git mv` that moves it to `.agents/`: move it, since `sync` refuses while one is left there (the index
would stop listing it, and nothing would say so). Search the repository too for anything that reads
`.claude/skills/` or `.claude/knowledge/` by path (a CI step, a hook, a script) and name each in the
hand-over. A third list, the records the repository seems to keep by role (a changelog, a decisions file
or folder, a fix log, a backlog), is what step 8 declares; `init` writes none of them.

An adopter already on `claude-code` moves by its manifest: `"harness": "agents"` and `"target": ".agents"`,
then the same `git mv` and the steps below.

### 2. `sync --dry-run` and read the collisions

Read every `COLLIDES`, `DRIFTED`, `LEFT BEHIND` and `LINK` line. A `LINK` line is a `CLAUDE.md` or a
`.claude/skills` that is a link, or a link checked out as text (`core.symlinks=false`): `sync` never
writes through one. A real file holding `@AGENTS.md` works on every checkout; the choice is the
repository's, and the hand-over says what was chosen. A `COLLIDES` line means the repository wrote that
file itself, before it ever heard of Daoris. Nothing is overwritten. For each one, open both versions and
separate:

- **The principle** — almost always already in the canonical version, often better generalized.
- **The mechanism** — the commands, paths, guards, and version policy specific to this repository. This
  is what would be *lost*, and it is usually the most load-bearing content in the file.

### 3. Preserve the mechanism in a local companion

Write the repository-specific mechanics into one local document — `.agents/knowledge/repo-mechanics.md`
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

### 6. Initialise the knowledge

In the canon's words (D124 §2.5), which the set-up quest says too:

Write what a session in another repository would need from this one and could not find: what it owns and
where, what it promises and the shape of its data, and how the figures others rely on are computed, each
fact with the place in the code that holds it. Say which facts the code did not confirm.

- **The domain**, in `daoris.json`: `summary`, one line for someone who has never opened the repository;
  `owns`, the areas where a change belongs here rather than anywhere else; `accepts`, the kinds of work
  worth asking of it; `uses`, the workspace's repositories its code depends on, by the names the
  workspace knows them by, where the code shows it (a client, a package reference, an address).
- **Knowledge documents** of the repository's own, in `.agents/knowledge/`, from the knowledge template
  beside `set-up-documents`, one per area a neighbour would ask about: what it owns and where; its
  contracts and data (what it exposes, what it takes from whom, their shapes, which are promised and
  which incidental); and the computations others depend on (each figure, status or rule another
  repository or a person reads from it, how it is derived, from which inputs, and where in the code). In
  each, where every fact lives, a path and a symbol. Each `applies_when` names the question a neighbour
  would be asking, since that is the line the index and the search show.
- **Not** a tour of the folders, anything a reader sees at a glance, the build commands, or the
  repository's history.
- **Truth before coverage.** A fact confirmed in the code is stated with its place; one that could not be
  is written as not confirmed, with where it would be settled; one only a neighbour knows names that
  neighbour. A set-up publishes no request of its own: the next set-ups write those answers anyway.
- **Its own documents first.** What the repository already keeps is read before anything is written; a
  document that already answers is named in the hand-over and not rewritten, and a new one says where the
  older one is.

### 7. Write the brief

The sync just installed the core skill `set-up-documents` and the knowledge behind it,
`development-documents` (D122). Follow the skill from here to step 9; its templates are beside it.

Write the repository's own part of `AGENTS.md`, above the region, from `templates/brief.md`. Move into
it what every agent needs from an existing `CLAUDE.md`: codex reads only `AGENTS.md`, and only up to
32,768 bytes (LAYOUT2), so a rule left in `CLAUDE.md` never reaches it. Leave `CLAUDE.md` holding the
import and what only Claude Code needs. A line goes in the brief only if nearly every task needs it and
nothing in the code would tell a reader; the rest goes to the tier that holds it. Moving is not
trimming: every line that leaves the brief has a new home, and the hand-over names it (step 12).

### 8. Declare the documents and the rooms

- **Rooms**: a folder whose conventions, traps or checks differ gets its own `AGENTS.md`, from
  `templates/room.md`, and is listed in `daoris.json`'s `rooms` (D117 §2.2). `sync` writes its pointer
  and lists it in the region, and `check` fails on a declared room with no `AGENTS.md`.
- **Records**: each record the repository keeps, by role (router, decisions, backlog, archive, fixes,
  changelog, glossary, gates), with its path, in `daoris.json`'s `documents` (D122 §2.7). A path is a
  file or a folder (a folder of decision records is the decisions); `{ "path": ..., "words": ... }`
  adds a ceiling, and `brief` and `room` take `{ "words": ... }` alone. `sync` renders them as the
  region's *Where things are* table, so the brief's own section says only that the region lists them,
  and the list is kept once. `check` fails on a declared path that is absent or a link, and on a table
  the manifest no longer matches; it reports a document over its ceiling and no backlog or decisions
  declared, and fails on neither. An unknown role, a path outside the repository or inside the
  doctrine's folders, and a role declared twice are refused before anything runs.

### 9. Declare the safe work

What the repository's sessions may run without asking: the gates that are a session's to run (not a
rehearsal, not one that needs the machine quiet), the build and test commands, the install from the
lockfile. Exact commands, one per entry, and none of `autonomous-development`'s carve-outs. It goes in
`daoris.gates.json` as `safe`, beside `gates` (D122 §3.1), never in the brief's prose: a sentence shapes
what an agent tries, not what its harness allows. The brief's *Build, test, verify* says where it is.
Nothing is widened by writing it. The driver reads it from the repository's line, not a session's tree,
once UNBLOCK2 lands, and a person accepts it once (UNBLOCK3); say so in the hand-over.

### 10. Expect the budget to be over, and do not paper over it

The always-loaded core is measured for the first time at this moment, and it is usually larger than
anyone thought. It is **reported, never enforced** — a fact gates and a judgement reports — so nothing
stops; the number is there to be answered rather than silenced. Two honest responses:

- **Trim** — usually a long local rule that duplicates a canonical one, or a deep dive sitting in the
  always-loaded tier that belongs in `knowledge/`.
- **Raise the budget to the true number** and record the overlap as a task.

Both are legitimate. What is not legitimate is trimming someone's doctrine as a side effect of adopting a
tool — that is editorial work and deserves its own review.

### 11. Verify the repository, not just the doctrine

Run the adopting repository's own build and tests. Adoption changes what every future session in that
repository reads, so "the tool exits 0" is not the same as "the repository is fine".

### 12. Hand it over for review — this is the human checkpoint

Adoption rewrites always-loaded context. The owner should see the diff before it becomes history. Run by
hand, leave it uncommitted for them. A driven set-up commits on its own branch as each step lands, and the
branch is what is reviewed: the workspace's landing rule lands it, as a branch for the owner's review or
merged into the line, where the landed history is the review (D117 §6.3); the session never pushes,
merges or opens a pull request. Under the automation-first model this is where the owner's attention is
spent, so hand them the whole outcome at once: the diff, the gate results (`check`, the repository's own
tests), the tool's version, which collisions were resolved and how, which twins were retired and where
each preserved line went, the domain in its words and each knowledge document with the question it
answers and each fact not confirmed, what the brief took in and where each line it let go now lives, the
documents and rooms declared, the safe work declared and that it waits for their yes, what was chosen
about links, what was found that needs a request elsewhere, and what the budget reads now, with the root
file's bytes and every ceiling `check` reports over beside it.
A judgement call worth surfacing — a twin that might not be one, a budget that had to rise — is stated
here as a decision with its reasoning, not asked mid-flow.
