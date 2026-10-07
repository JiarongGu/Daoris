# What the set-up pilot taught — checks kept green, an index read on demand, documents without frontmatter

> The owner's run, 2026-10-02 (WSSETUP12's first repository): the first real set-up (D124, `daoris-driver setup`)
> wrote good knowledge, a brief and rooms, closed its quest `done`, and left a branch that cannot be merged, for
> three faults of Daoris's own. This is the contract for the WSSETUP14 rows, and its decision is **D128**. Status:
> **designed; §2 built by WSSETUP14a and §3.1–§3.2 by WSSETUP14c** (D128's notes), the rest not yet. It amends D124 §2 (the set-up quest), D117 §2.1, §5.4 and §6, D122 §2.7 (the
> `knowledge` role) and D59 (what the region holds). Read with **D7**, **D13**, **D19**, **D32**, **D54** and
> **D127**.

The repository is private, and so are its owner's company and tickets. It is named here only as *the report
repository*.

- §0 is what the pilot did and what was measured here. §1–§4 are the design: checks kept green, the index, a
  document without frontmatter, and the pilot's branch.
- §5 is what it saves. §6 is the build, as rows. §7–§9 are what only a real run proves, what this amends, and what
  this document's gate does not cover.

## 0. What the pilot did

### 0.1 The owner's report

| | |
|---|---|
| The quest | *Set up this repository for every agent*, composed by `SetupBrief`, carried by the repository's own session on its own branch |
| The result | closed `done`; 7 commits, unpushed, unmerged; one turn: 52.3M tokens read from the cache, 464K new, 170K out |
| What is good | the knowledge documents it wrote, the brief, the rooms |
| Fault 1: its checks went red | step 2 moved the repository's own 169 knowledge documents and 18 skills from `.claude/` to `.agents/` with `git mv`. Its CI's first step (the repository's own knowledge check) and an opt-in pre-commit hook read `.claude/knowledge/` and `.claude/rules/` by path. 218 links between documents broke, and about 250 references name the old paths. The bounds (*never change a source, build or CI file*) forbade the fix, so the session listed it as follow-ups |
| Fault 2: the region grew with the knowledge | every knowledge document is a row of `AGENTS.md`'s region: the region is 53,398 bytes and `AGENTS.md` 60,935. The session raised the budget from 30,000 to 54,000 to make `check` say clean. One widely used agent reads 32,768 bytes of the file, so it never reaches the shared rules |
| Fault 3: no frontmatter | 166 of the old documents have none, so their rows read *needs frontmatter* twice |

### 0.2 Why each fault is Daoris's

- **Fault 1.** The quest told the session to move the documents and forbade it to repair what the move broke.
  `SetupBrief.Moves`: *"Move this repository's own documents out of `.claude/knowledge/` and `.claude/skills/` with
  `git mv` … Search the repository for anything that reads `.claude/skills/` or `.claude/knowledge/` by path … and
  name each in your close; changing one is not this set-up's."* Nothing in the quest asked whether the checks
  still passed. The move was there because the CLI refuses a move while a repository's own document stays in an
  old tier (D117 §5.4), since the index would stop listing it.
- **Fault 2.** `renderRoster` (`tierrender.ts`) writes the rules table, then a row for every knowledge document,
  then a row for every skill, then the rooms and *Where things are*, and only after them every rule in full. So the
  region grows with the repository's own documents, and the rules move further down with each one. The quest's
  *Verify* step offered *"raise the budget to the true number"*, and the session took it.
- **Fault 3.** A knowledge row is built from `applies_when` and `enforces`, and without them it prints the
  warning in both columns. Nothing reads the document itself.

### 0.3 Measured here

By today's CLI, `sync` run in a gitignored scratch folder on fixtures shaped like the pilot's branch (agents
layout, core canon, a brief of 7,398 bytes above the region), and on this repository's and the examples' own
`AGENTS.md`. *Proposed* is the rendered region with its knowledge and skill tables cut out and §2.2's pointer put
in their place. It was computed, not rendered by built code.

| | `AGENTS.md` | region | knowledge table | skill table | first rule at byte |
|---|---|---|---|---|---|
| This repository (10 knowledge, 8 skills, no brief outside the region yet) | 24,150 | 24,064 | 3,304 | 1,338 | 7,434 |
| … proposed | ≈19,730 | ≈19,650 | — | — | ≈3,020 |
| `examples/engine` (6, 6) → proposed | 21,844 → ≈18,970 | 21,758 → ≈18,890 | 2,102 | 992 | 5,128 → ≈2,260 |
| `examples/game` (7, 6) → proposed | 22,061 → ≈18,970 | 21,975 → ≈18,890 | 2,319 | 992 | 5,345 → ≈2,260 |
| Fixture: 169 knowledge (166 without frontmatter), 18 skills | 53,762 | 46,279 | 23,333 | 4,281 | **37,046** |
| … the same 169, every one with frontmatter | 71,010 | 63,527 | 40,581 | 4,281 | **54,294** |
| … proposed, either way | 26,493 | 19,010 | — | — | 9,777 |
| Fixture with no knowledge or skills of its own | 29,365 | 21,882 | 2,102 | 1,115 | 12,649 |
| The pilot's branch, as reported | 60,935 | 53,398 | | | |

Three findings:

1. **On the fixture the first rule starts past byte 32,768.** The agent that cuts there reads the brief, the roster
   and no rule at all, not only *the shared rules at the region's end*. The pilot's file is larger than the
   fixture's: its names and rows are longer, and it declares rooms and records.
2. **Fixing fault 3 under today's design makes fault 2 worse.** Frontmatter on all 169 documents grows the region
   by 17,248 bytes, since a described row is longer than a warning.
3. **The proposed region does not depend on the repository.** It is 19,010 bytes with no knowledge of its own and
   with 169 documents. What varies is the rooms and the records declared, each bounded.

## 1. Keeping the repository's checks green

### 1.1 The rule: move only what an agent needs moved

Where a file lives matters only to what reads it by its path. Knowledge is read by nobody automatically: it is read
on demand, *"a thing an agent does by being told a path"* (the instruction-file design, §3), and every agent reaches
it through the index, wherever it lives. D117 rejected a knowledge mirror for that reason. Skills
are read by folder: Claude Code lists `.claude/skills/`, and codex and dsh list `.agents/skills/` (the entry-point
evidence, §1, §2 and §4, read from their shipped code). So:

1. **The repository's own knowledge stays where it is**, declared (§1.2). A set-up never moves it. A repository
   that keeps none writes its new knowledge to `.agents/knowledge/`, as today.
2. **The repository's own skills move** to `.agents/skills/` with `git mv`, because two agents of three read them
   only there. `sync` then writes each one back under `.claude/skills/` as a mirror, so anything that reads a skill
   at its old path still reads it.
3. **Nothing else that a check, a script, a hook or a CI configuration reads by path is moved.** A `.claude/rules/`
   folder a hook reads stays where it is, and the close says its rules reach one agent alone.
4. **Where a move still breaks a reader, the set-up rewrites the path in that file, and only the path.** A link
   inside a moved skill that pointed outside it is rewritten to resolve from the skill's new place. A script that
   writes into `.claude/skills/` (which now holds mirrors) is pointed at `.agents/skills/`. Each file is named in
   the close.
5. **The checks decide** (§1.4): run before the first change and at the close.

Applied to the pilot, rules 1 and 2 leave nothing broken: the 169 documents and every link to them stay, and the
18 skills keep a copy at the paths their readers use. Rule 4 is the answer for whatever is left, and §1.4 finds it.

### 1.2 `documents.knowledge`: a folder of the repository's own knowledge

D122's closed set of roles names `knowledge` and refuses it, *"since the index already lists them from the target"*
(DOC3). That reason holds only where the knowledge is in the target. The role now takes a path:

```json
"documents": { "knowledge": ".claude/knowledge" }
```

- **A folder**, relative to the repository. It is refused, as every declared path is (D122 §2.7), when it escapes
  the repository, is the root, or sits inside the target or the mirror root. Under the `claude-code` layout,
  `.claude/knowledge` is the target's own tier, so declaring it is refused as needless.
- **Read, never written.** The index lists every markdown file in it and below it, marked `_(local)_`. `sync`
  writes nothing there, and the lock records nothing there (D5): it is the repository's own, exactly as a local
  document in the target is.
- **`check`** fails on a declared folder that is absent or a link (DOC3's facts). `sync` refuses, and `check`
  fails, on a file in it whose name a document in the target's knowledge tier also has. Two documents with one name
  are two copies, and which one is meant is the repository's call (D117 §5.4's *same name under both roots*).
- **The service** (`RepositoryDocuments`) reads it with its own code and indexes each file as local knowledge,
  titled by its file name as the tier's documents are, once (DOC5's *one file is one place*).
- **A twin**: `documents.ts` and `RepositoryDocuments.cs`, with their tables (`.claude/knowledge/twins.md`, *the
  development documents* row). `skill` stays refused: a skill outside a skill root reaches no agent.
- **`init` and `analyze`** name a folder holding the repository's own knowledge as a candidate for the role, with
  the declaration first and the `git mv` second.
- **It is a row of *Where things are***, like every declared path: *knowledge | `.claude/knowledge` | deep dives,
  listed in the index*. A session about to write a knowledge document learns there where this repository keeps
  them.

### 1.3 The move's cells, amended (D117 §5.4)

*The repository's own documents* gains one row and changes one:

| Where | Outcome |
|---|---|
| in a folder declared as `documents.knowledge`, the old tier's `knowledge/` among them | **read in place**: listed, indexed, never moved, never refused |
| in an old on-demand tier, undeclared | refuse the move, naming each document with the declaration that keeps it there and the `git mv` that moves it |

`LayoutFacts.Clean` (the driver's *already set up*) reads *nothing left in `.claude/knowledge/`* as *nothing left
there unless the manifest on the line declares it*.

### 1.4 The checks, before and after

- **Before the first change**, the session finds the checks this repository runs: its CI's steps, the check and
  test scripts its package files name, its hooks. It runs each one a session may run (not a deploy, a publish, or
  one that needs a secret or a person) on the tree as it is, and notes each result. A check already failing is
  noted and is not the set-up's to fix.
- **These are the checks its safe-work step declares** (D122 §3.1), so the comparison is written down where a tool
  reads it.
- **At the close** it runs them again. **A check that passed before and fails now means the set-up does not close
  `done`.** If the fix is inside its bounds, it fixes it. If it is not, it stops and asks, naming the check, the
  tail of its output and what would fix it: only the person can widen a bound, so this is the stop D124 §7 allows.
  A parked set-up frees its slot in the plan and has not closed (WSSETUP6).
- The rule binds a session's instruction, and no tool verifies it today: the driver concludes a set-up from its
  quest, and the queue that runs a repository's declared gates before it lands a branch (D115) is not built. The
  close carries each check with both results, and the review reads them.

### 1.5 The bounds, rewritten

The quest's words (`SetupBrief.Bounds`), replacing D124 §2.6:

> **What this writes, and what it never does.** Write only in this tree, on this branch: `daoris.json` and
> `daoris.lock`; `AGENTS.md` and `CLAUDE.md` (the region, the brief, the import); `.agents/`, with the shared
> documents and this repository's skills; the copies under `.claude/skills/` the tool writes; this repository's
> knowledge, in the folder where it already keeps it, or `.agents/knowledge/` where it keeps none; each room's
> `AGENTS.md`; the `safe` section of `daoris.gates.json`, and the file where there is none; a document that keeps a
> collision's mechanism; and, where another file names a path this set-up moved, that path, rewritten to where it
> went. Never push, merge into the line or open a pull request; never write outside this tree (another
> repository's checkout you may read is read only); never publish a request to another repository; never run
> `daoris connect` or `daoris upstream`; never set `remote.join` or `remote.knowledge` in `daoris.json`; never move
> this repository's knowledge, or any file a check, a script, a hook or a CI configuration here reads by its path,
> other than its skills; never change what a source, build or CI file does, since a moved path rewritten is the one
> change allowed in one; and never add frontmatter to a document this repository already had. Anything else that
> needs one of those is named in your close, as work for a request the person may publish.

### 1.6 The steps and the close

D124 §2.3's steps change in four places:

- **A new step after the tool's check: run the repository's checks first** (§1.4).
- **Take up the doctrine**: the move says §1.1's rules 1 to 4 in place of today's *"Move this repository's own
  documents out of …"*.
- **Initialise the knowledge**: new documents go to the folder §1.2 names. *Its own documents first* stands.
- **Verify**: `daoris sync`, `daoris check` and `daoris status --json`, then every check run first, compared. The
  budget sentence becomes: *if `daoris check` reports the budget over, say by how much; raise it only to the true
  number, and never past what keeps the root `AGENTS.md` under 32,768 bytes, brief and region together.*

The close (D124 §2.7) gains: each check with its result before and after; each file where a moved path was
rewritten; the knowledge folder declared, or that none was; and how many of the repository's own documents have
no frontmatter (§3.3). The adoption playbook (`.claude/knowledge/adoption.md`, steps 1, 6, 10, 11 and 12) says the
same, as its twin requires.

### 1.7 Rejected

- **Rewrite everything that reads a moved path, and keep the move.** About 250 references, a CI step and a hook,
  rewritten inside a doctrine change: a large diff in files the set-up is not about, for knowledge no agent reads by
  folder. Rule 4 keeps the rewrite for what an agent needs moved.
- **Declare skills in place too.** A skill outside the roots an agent lists reaches no agent; in `.claude/skills/`
  it reaches one of three, which is what D117 corrected.
- **Keep the bounds and add only *red never closes done*.** Every repository that keeps its own knowledge folder
  would stop the same way, and the person would be asked the same question twenty-nine times.
- **A knowledge mirror at the old path**, or **a link there**: D117 rejected the first, since nothing auto-reads
  knowledge and the service would find each document twice, and D3 the second.
- **The canonical knowledge in the declared folder too.** It would make a repository's folder partly Daoris's, and
  the lock, the move's cells and the service's root all assume Daoris writes only under its target.
- **The driver running the checks itself.** It would run commands Daoris chose rather than the repository's own
  session. D115's queue, which runs a repository's *declared* gates before it lands a branch, is the mechanical form
  once it is built.

## 2. The index, read on demand

### 2.1 What grows and what does not

The region holds what is bounded by the canon: the rules, in full and in a table. A list that grows with the
repository goes on demand. Knowledge grows with every document a set-up writes, which is the set-up's purpose. Skills
grow with the repository too, and each agent already lists them from its own skill root, so the region's skill table
is a second copy for every agent that reads skills. Rooms and *Where things are* stay: the records are a closed set
of roles, and a room's row is the one always-read pointer to that folder's instructions (D117 §2.2).

### 2.2 The region after

```
# Doctrine
Generated by daoris …
## Always loaded — every rule below, in full
| Rule | Applies when | Enforces |            (one row per rule, unchanged)
## Read on demand
<the pointer>
## Rooms                                       (when a room is declared, unchanged)
## Where things are                            (when a record is declared, unchanged)
<every rule in full>                           (unchanged)
```

The pointer, in the canon's words, 223 bytes, or 345 with the mirror's sentence:

> The knowledge and the skills, each with when it applies, are listed in `.agents/INDEX.md`, generated from the
> files: read it before a non-trivial task, and search it when it is long. Skills live in `.agents/skills/`;
> `.claude/skills/` mirrors them for the agent that reads only there — edit the source.

In the region the index's path is a link, as the roster's rows are today, and the byte counts include it. It names
the target the descriptor uses (`.claude/INDEX.md` under `claude-code`) and carries no count: a count would change
the region with every document (D106, *no moving counters in always-read files*).

### 2.3 `<target>/INDEX.md`

```
# Index
Generated by daoris from the files. Edit the documents, not this: `daoris sync` rewrites it, and `daoris check`
fails when it is behind.
## Knowledge
| Document | Applies when | Enforces |        (canonical and local, by name)
## Knowledge without frontmatter
| Document | Its first heading |                (§3.1; only when there is one)
## Skills
| Skill | Use when |
```

- **Each document is named by its path from the repository's root, as a code span**, not a link: an agent opens it
  as written, and a renderer shows no broken link from a file one folder down.
- **At the target's root, beside the tiers**, not inside `knowledge/`: there the CLI would list the index as a
  document, the service would index it, and a repository's own `INDEX.md` in that folder would collide. No agent's
  instruction-file cells in the entry-point evidence name a file at the root of `.agents/` or `.claude/` other than
  `CLAUDE.md`, so it is read only when a session opens it.
- **The service never indexes it.** It reads the tiers' folders, not the target's root, and a test holds that.

### 2.4 Its cells (D19, for the index)

| The lock names it | On disk | Outcome |
|---|---|---|
| no | absent | write it; the lock gains `index` |
| no | as it would be written | adopt silently |
| no | anything else | **collision**: the repository's own file. Refuse, naming it |
| yes | anything | rewrite it. It is generated, as the region's roster is: a hand edit is overwritten, and `check` named it stale before |
| yes, at the old root after a move | — | write it at the new root and delete the old |
| — | a link, or a link held as text | refuse, never write through (D117 §5.4) |

`check` rebuilds it from disk, offline and without the canon, and fails when it differs: the roster's staleness
check (`onDemandHalf`) moves from the region to the file. The lock's `index` is its path. A lock written before it
has none, which reads as no index written yet.

### 2.5 What reads it

> **Amended by D129** (`docs/2026-10-02-knowledge-design-review.md` §4.2–§4.4, KNOW2): a long index is searched in more
> than one wording, and a connected search is asked too; `skills-workflow` names the agent's own skill list first;
> `development-documents` gains a sentence on naming a document by its subject.

- **`doc-loader`**, step 2, in the canon's words: *The always-loaded doctrine names the index of the on-demand
  tiers. Open it and scan its knowledge table's applies-when column against the task: whole when it is a few dozen
  rows, by searching it for the task's words when it is longer. Read every matched document. The index is generated
  from what is actually on disk, so it is the exhaustive list.*
- **`skills-workflow`**: unchanged. *"Consult the generated index for the roster. It is built from what is on
  disk"* stays true of the file, and each agent lists the discovery skills from its own skill root besides.
- **`development-documents`**: *"Something always read names each one"* becomes *"Something always read names
  each one, or names the index that lists them: a list that grows with the repository is read on demand too."*
- **The set-up's twin hunt** (`SetupBrief.Twins`, the playbook's step 4) reads the index and the region's rules
  table end to end, in place of *the generated index in `AGENTS.md`*.
- **`daoris index`** says the roster is the region's rules table and `<target>/INDEX.md`.
- **`check`'s `size` line** is unchanged. It still compares the root file with 32,768 bytes, and now reports it
  only when the brief itself is long.

### 2.6 Measured against the limit and the budget

From §0.3:

- **The pilot's shape.** The region drops from 46,279 bytes on the fixture (53,398 on the pilot) to about 19,000,
  plus its rooms and records. With a 7.4 KB brief the root file is 26,493 bytes and its first rule at byte 9,777,
  so 32,768 leaves about 6,300 bytes for the brief to grow. The default budget, 30,000, holds with about 11,000 to
  spare, so the pilot's raise to 54,000 is undone.
- **This repository.** 24,064 → about 19,650 bytes of 26,000: 4,400 fewer, 18% of the region. With D127's 458 bytes
  (SESSOPT1a) about 20,100. LAYOUT6 moves a brief of about 1,300 words into `AGENTS.md`: about 8,500 bytes at
  `CLAUDE.md`'s 6.5 bytes a word, and about 1,000 more for the eight rooms (D117 §5.3's estimate). Today's region
  plus those is about 34,100 bytes, over 32,768. After this change it is about 29,700. That is arithmetic, not a
  render.
- **The examples.** About 2,870 and 3,090 bytes fewer.
- **The index file**: about 4,700 bytes here, 27,700 on the fixture, 45,000 with every document described. It is
  read when a task needs it, and searched when it is long.

### 2.7 Every adopter: the migration

It is a canon and CLI change, so every adopter meets it at its next `sync`. `canon/CHANGELOG.md` says:

> **The doctrine region no longer lists knowledge and skills.** They are in `<target>/INDEX.md`, which `sync` writes
> and `check` keeps true, and the region points to it. The always-loaded region no longer grows with your own
> documents, and the shared rules sit thousands of bytes higher in `AGENTS.md`. Run `daoris sync`. A file of your own
> at that path is named as a collision; rename it. A repository that keeps its own knowledge outside the target can
> declare the folder as `documents.knowledge` and leave it where it is.

This repository and both examples are re-synced in the same commit (the family rehearsal holds them current), and
the release rehearsal's *roster above them* check reads the pointer and the index (`release-rehearsal.mjs`).

### 2.8 Rejected

- **The canonical rows in the region, the repository's own on demand.** The region would still grow with each canon
  knowledge document (DOC2's two rows cost 634 bytes), and a session would scan two lists for one tier. The canon's
  knowledge is reached the way the repository's is, through the index.
- **Knowledge by folder**, one row per folder: it drops the *applies when* that `doc-loader` scans.
- **A cap**, the first N rows in the region: a row is in or out by its name's place in the alphabet, and the region
  still grows to the cap.
- **Shorter rows** (no *enforces*): still linear in the count; the fixture's rows would still be about 19,000 to
  23,000 bytes, by subtracting the column from §0.3's tables.
- **The rules before the tables.** The agent that cuts at 32,768 would lose the tables instead of the rules, and
  every agent would still pay for the fixture's 27,600 bytes of rows on every step.
- **Raising the budget, or the default**: the 32,768 cut is an agent's, not ours, and the budget reports (D54).
- **D59's rejection stands**: a pointer in place of the RULES turns *always loaded* into *always told to load*.
  Knowledge and skills were always told; this moves the list, not the tier.

## 3. A document without frontmatter

### 3.1 What the index says

It is listed in *Knowledge without frontmatter*, after the described ones, by its path and its first `#` heading,
or `—` where it has none. A heading says what a document is about. The warning said only that it lacked fields.
The bytes are about those of the warning row, and in the index rather than the region.

### 3.2 What `check` says

Once, never failing (D54): *`frontmatter  166 knowledge documents have none; the index lists them by their first
heading — advisory`*. A canonical document without frontmatter is a defect of the canon, held by the canon's own
tests, so the line counts local documents only. A skill without frontmatter keeps its warning in the index, since
such a skill never fires on any agent (`verifyHarnessContract`).

### 3.3 What the set-up writes

Frontmatter on every document it writes, from the template, as today. **Not on the repository's existing
documents.** Writing 166 of them would mean reading each to say when it applies, a judgement per document, inside a
change that is about the doctrine. The playbook holds the like of it, trimming a repository's doctrine as a side
effect of adopting a tool, to be editorial work that deserves its own review (step 10). The close names the count,
as a request the person may publish: *describe this repository's knowledge*, one session writing `name`,
`applies_when` and `enforces` above each document's first line and nothing else. A repository whose own check
refuses frontmatter keeps its convention, and its documents are listed by heading.

### 3.4 Rejected

- **The set-up describes every document**: 166 reads and 166 judgements in a bounded change, and the region would
  grow by 17,248 bytes on the fixture until §2 lands.
- **A capped number of documents**: which ones is a guess.
- **The description guessed from the heading** in the frontmatter's place: a guessed *applies when* presented as
  the repository's own words.
- **Keeping the warning**: 166 rows saying nothing a reader can use.

## 4. The pilot's branch

### 4.1 Never merged red

The branch stays as it is, unmerged, until a follow-up set-up finishes it. Merging it red would put the broken
checks on the line, and a fresh set-up from the line would throw away the turn's good work.

### 4.2 The follow-up set-up

When the press finds a set-up's branch standing and not on the line (the landings record, by its quest's title,
which WSSETUP5 already reads for *not set up*), it composes a follow-up rather than refusing or starting afresh:

- **Its title**: *Finish setting up this repository (<day>)*, a fourth stem in `SetupQuests`, which `IsSetup` knows.
- **Its rule**: the nine verbs, plus one exact `git merge --no-ff --no-edit <that branch>`, added as the person's
  say-so like the others (D124 §2.4), since a session on the protocol door cannot ask (D52).
- **Its body**: what was set up on that branch and when, by its commits; then the steps.
  1. The tool's check.
  2. **Run the repository's checks first**, on this tree, which grows from the line.
  3. **Merge that branch into this tree**, once.
  4. **Put back what the first set-up moved that §1.1 keeps in place**: the repository's knowledge, with `git mv`
     back to its folder, and that folder declared. The documents the first set-up wrote join it.
  5. `daoris sync` with the current tool, which writes the index and shrinks the region.
  6. **The budget back** to the default, or to the true number under 32,768 bytes for the root file.
  7. **Keep the rest**: the knowledge's words, the brief, the rooms, the domain, the documents and the safe work,
     each read once against the standard.
  8. Verify, compared (§1.4), and close as §1.6 says.
- **The plan**: the pilot's repository reads *setting up* again, and the plan stays paused until the follow-up
  closes; WSSETUP13 resumes it.

### 4.3 Rejected

- **Growing the follow-up's tree from the branch.** The *before* would be the red branch, and a baseline that is
  already red cannot show what the set-up broke.
- **The person repairing the branch by hand.** Development here is automation-first (D37), and the repair is the
  repository's own session's.
- **Moving the 169 documents back by hand, then merging.** It is the same work without a review of its whole.

## 5. What it saves

### 5.1 Every step of every session in an adopted repository

A harness carries what it was handed into every request after (D127, and its design's §1.6). So each byte cut from
the region is cut from every request of every session there.

| | Fewer bytes on every request |
|---|---|
| The pilot's repository | about 33,000 to 34,000 (53,398 → about 19,000 to 20,000) |
| The fixture | 27,269; 44,517 had every document been described |
| This repository | about 4,400 |
| The examples | about 2,870 and 3,090 |
| A repository a set-up gives its first knowledge | everything the set-up writes: each document is about 130 to 330 bytes of rows today, and none after |

At roughly four bytes a token, an estimate no tokenizer checked, the pilot's figure is about 8,500 tokens on every
request. Over a hundred requests that is some 850,000 tokens fewer read from the cache. The rules reach the agent
that cuts at 32,768 bytes, which is a correctness gain and not a cost one.

### 5.2 Every later set-up

- **No move's fallout.** The pilot's follow-ups (about 250 references, 218 links, a CI step and a hook) are a second
  session's work that a set-up under §1 never creates.
- **No overage to answer.** The region does not grow with what the set-up writes, so *Verify* meets no budget it can
  answer only by raising the number.
- **166 documents not opened** to write a guessed description.
- **A red result is caught by the session that caused it**, not by the next CI run after a merge.

### 5.3 What it adds

Each set-up runs the repository's checks twice, and the follow-up once more: a few commands, and an install where
the checks need one. A session that runs `doc-loader` reads the index: about 4,700 bytes here, once per task, and on
a long index the rows a search returns. The set-up's own session pays nothing different for the region, since the
region did not exist when it started.

## 6. The build

Rows ready for `TASKS.md`, in D127's shape. Lanes are `daoris.lanes.json`'s ids. *Doctrine* (canon, examples, this
repository's synced copy and `.claude/`) and *docs* are laneless groups. D128 decides all of it.

- [ ] **WSSETUP14a — the index leaves the region.** `sync` writes `<target>/INDEX.md` with the knowledge and skill
  tables, and the region keeps the rules, the pointer, the rooms and *Where things are*; `doc-loader` and
  `development-documents` read it. Without it, every document a set-up writes is paid for on every request in its
  repository, and the rules fall past the 32,768-byte cut. Contract: §2.2–§2.5, §2.7. Proof: §2.4's cells as `node --test` cases,
  each failing first; adding 169 knowledge documents and 18 skills to a fixture leaves the region's bytes unchanged;
  this repository and both examples re-synced, with the region within 100 bytes of §2.6; the release rehearsal's
  check reads the pointer and the index. Lanes: cli, tools, doctrine. After SESSOPT1a (both change
  `development-documents` and re-sync the examples).
- [ ] **WSSETUP14b — a knowledge folder declared in place.** `documents.knowledge` names a folder of the
  repository's own knowledge that the index lists, the service indexes and `sync` never writes, and `init` offers it
  before the `git mv`. A repository's knowledge reached through the index needs no move, and the pilot's move broke
  its checks. Contract: §1.2, §1.3. Proof: the twin's tables matched (`documents-manifest.test.ts`,
  `RepositoryDocumentsTests`); a move with `.claude/knowledge` declared neither refuses nor moves it, as a
  release-rehearsal phase; the service never indexes `INDEX.md`. Lanes: cli, service, tools. After WSSETUP14a.
- [ ] **WSSETUP14c — a document without frontmatter, by its heading.** The index lists it by its first heading in a
  table of its own, and `check` reports the count once. 166 rows of *needs frontmatter* told a reader nothing.
  Contract: §3.1, §3.2. Proof: `node --test` cases (frontmatter, a heading only, neither, a skill without one keeps its
  warning). Lanes: cli. After WSSETUP14a.
- [ ] **WSSETUP14d — the set-up keeps the checks green.** The quest runs the repository's checks first, keeps its
  knowledge in place, rewrites only a moved path, adds no frontmatter to old documents, and never closes `done` with
  a check gone red; the playbook says the same. The pilot closed `done` on a branch its own CI fails. Contract: §1.1,
  §1.4–§1.6, §3.3. Proof: `SetupBriefTests` (the new step, the bounds' words, the close's items, no decision number or
  Daoris path) and the playbook twin; `LayoutFacts.Clean` with a declared `.claude/knowledge`. Lanes: driver,
  doctrine. After WSSETUP14b.
- [ ] **WSSETUP14e — the follow-up for a set-up's branch.** The press, finding a set-up's branch standing and not on
  the line, composes *Finish setting up this repository* with the exact merge rule and §4.2's steps. A turn's good
  work is kept, and the branch is never merged red. Contract: §4.2. Proof: the composer and the press's tests (the
  case, the title, the rule added and taken back on a refusal); the family rehearsal's set-up phase with a standing
  branch, at the parent's merge. Lanes: driver, tools. After WSSETUP14d.
- [ ] **WSSETUP14f — the pilot, finished** (the owner's run, after a republish carrying a–e). The follow-up on the
  report repository: its checks green before and after, its knowledge back in its folder and declared, the region
  and the root file measured against 32,768 bytes, and the budget back. Contract: §4. Proof: the close's table of
  checks; `check` clean at the default budget; the follow-up's cost in the usage report beside the first turn's.
  Then WSSETUP13 resumes the plan.

**Order.** a, then b and c side by side, then d, then e, then f. a and b change the CLI's lane in turn; only a
changes the canon.

## 7. What a rehearsal can prove, and what only the follow-up can

- **A test or a rehearsal proves**: the region does not change with the knowledge count; the index's cells and its
  staleness; a declared folder read in place, on the packed CLI; the quest's words; the follow-up composed and
  pressed, and a stub session carrying it.
- **Only the follow-up shows**: that a real session finds and runs the repository's checks first and keeps them
  green; that the report repository's CI passes on the landed line; whether a session searches a long index rather
  than reading it whole; and, with LAYOUT2's canary, that the agent that cuts at 32,768 bytes now reads the rules.

## 8. What this amends

- **D124 §2**: §2.1 gains the follow-up's case (a set-up's branch standing unlanded); §2.3 gains *run the checks
  first* and §1.6's changes; §2.5's documents go to the declared folder; §2.6 is §1.5; §2.7 gains §1.6's items.
- **D117**: §2.1, the repository's own knowledge may stay where it is, declared; §5.4, §1.3's rows; §6.2's steps as
  amended above; `LayoutFacts.Clean`.
- **D122 §2.7 (DOC3)**: `knowledge` takes a folder; `skill` stays refused.
- **D59, and D7 through it**: the region carries the rules table, the pointer, the rooms, *Where things are* and
  the rules; the on-demand tables move to `<target>/INDEX.md`. D59's rejection of a pointer for the rules stands.
- **The canon**: `doc-loader` step 2, one sentence of `development-documents`, the changelog's entry.
- **The adoption playbook** (local): steps 1, 4, 6, 10, 11 and 12.
- **BUDGET1** stays the owner's call. Its arithmetic changes: with the region at about 19,000 bytes, the brief has
  about 13,700 bytes under 32,768. A region filling the default budget of 30,000 left it 2,768, as D122 found.

## 9. What this document's gate does not cover

This is documents only, and nothing is built. The pilot's facts (the counts, the bytes, the checks that went red,
the turn's tokens) are the owner's report: the repository is private and was not read here. The fixtures were synced
by today's CLI in a gitignored scratch folder. Their names are shorter than the pilot's, and they declare no rooms or
records. *Proposed* bytes are the rendered region with its tables cut out and the drafted pointer put in, and the
build may reword the pointer. No token was counted. What each agent lists as skills, and which files it reads as
instructions, is the entry-point evidence's reading of shipped code, not a turn. Whether a session follows the new
steps is the follow-up's to show. `verify` checks this document's links and the decision's shape, and none of their
words.
