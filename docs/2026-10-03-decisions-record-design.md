# The decisions record under parallel merges — one file per decision

> DOC8, from the parent's account of 2026-10-02 and 03: four integrations in a row came out of their union
> merges with the decision log torn, and D130–D133 were rebuilt by script three times. This is the study and the
> contract, and its decision is **D134**. Status: **designed; nothing built.** Read with **D106** (the union
> merge), **D117** §2.4 (which kept the one log and named the condition for reopening it), **D122** §2.1 (a
> folder of records as a decisions role) and **D127** (what a session pays for a read).

- §1 is what was measured, at `155882ec`: how often the record tore, in which shapes, what reading one decision
  costs, and what reads the record.
- §2 weighs the four options. §3 is the shape chosen. §4 is the migration. §5 is the build, as rows.
- §6–§9 are what only use proves, what was rejected, what this amends, and what this document's gate does not
  cover.

## 1. What was measured

### 1.1 How

Every merge commit in the history was replayed by scratch scripts under the gitignored `local/`, untracked. For a
merge whose two parents both changed `docs/DECISIONS.md` against their merge base, and whose first parent's
`.gitattributes` already marked the record `merge=union` (D106, from `7cdc5b77` on 2026-09-30), the script asked
git for the merge it makes today, `git merge-tree --write-tree`, with the attributes in effect and its virtual base
for a criss-cross. The result was judged against what each side meant, by two facts:

- **A heading twice**: a `## D<n>` that appears more than once.
- **A side's line under another decision**: a line of 25 characters or more that one side added under decision X,
  found exactly once in the result, under a decision other than X.

Both are facts about the text. Neither reads meaning, so a tear made only of short lines, or of a line written
twice, is not counted. Git is 2.53.

### 1.2 How often it tore

| | |
|---|---|
| Merges since the union whose both sides changed the record | 134 |
| …left with a conflict marker | 0 |
| …**torn** | **33** (25%) |
| …with a heading twice | 16 |
| …with a side's lines under another decision | 22 (5 had both) |
| Torn, and committed as the union left them | 27 |
| Torn, and repaired inside the merge commit | 6 |
| Repairs committed afterwards, by subject (*back under*, *once each*, *the line the union merge left*) | 11 |

By day: 0 of 4 merges on 2026-09-30, 11 of 77 on 10-01, 17 of 45 on 10-02, 5 of 8 on 10-03. The rate rose with
the notes: a build writes its note under the decision it builds (D115 §5, D127), and the newest decisions carry
the most (D124 thirteen, D125 eleven, D126 nine), so more merges carry lines for decisions other than the last.

**A tear moved on.** Six of the 33 merged a side that already held a decision twice. `doc-duplicates` catches a
heading twice when `verify` runs, and nothing catches a note under the wrong decision, so a tear committed in one
branch's merge of main reached the next integration.

### 1.3 The four shapes

Each torn merge, by what its two sides did against their base:

| Shape | Merges | What happened |
|---|---|---|
| **One insertion point for two kinds of line** | 17 | One side added a note under the newest decision, the other added a new decision after it. Both insert at one place, the end of the file, so union keeps both in the order *ours, theirs*, and the note lands under the other side's new decision: D126's notes under D127 and D128, D130's under D131 and D132 |
| **A decision moved** | 3 | A branch moved D123 or D124 to keep the numbers in order. Union keeps the moved copy and the original |
| **One decision added on both sides** | 7 | A criss-cross: main and a branch each held D124, or D131, with different notes. Union keeps both copies |
| **A side already torn** | 6 | The first tear, merged on |

The parent's scripts (`rebuild-d130s.mjs`, `rebuild-d130s-2.mjs`, `move-notes.mjs`, `drop-copies.mjs`, in the
parent's `local/scratch/`) show what a repair needs. Each finds where a note ends by guessing: `drop-copies`
treats five label starts as boundaries (`## `, `**As built (`, `**Built 20`, `**Fixed `, `**Read 20`), and
`move-notes` names each misplaced note by its exact first and last lines. Each proves only that no line was
invented and that the copies agree as prefixes. **The record has no boundary for a note that a tool can read**:
178 labelled, dated notes sit under 73 decisions, in sixteen label forms, and `*Amended by D130*`, the commonest,
names the decision that amends, not the one amended.

### 1.4 What reading one decision costs today

The record is 815,821 bytes, 134,707 words and 9,801 lines, in 132 entries (D1–D133; D114 was never taken),
eleven of them out of numeric order on purpose. An entry's median is 603 words (3,688 bytes); the 90th
percentile 1,971 words; the longest 6,659 (D130 is 5,882). The notes per entry: median one, 90th percentile three,
most thirteen (D124).

A session that looks up D130 searches for its heading, which gives a line number, then reads from it. It cannot
know where the entry ends without a second search, so it reads a window. Counted with the read tool's line
numbers:

| How it reads | Bytes, median | 90th percentile |
|---|---|---|
| Exactly the entry (heading to next heading) | 4,037 | 13,145 |
| A 200-line window from the heading | 16,775 | 18,845 |
| A read from the heading with no limit (2,000 lines) | 170,365 | 188,375 |
| Every heading with its line number, to find the end first | 15,831 | — |

A 200-line window is **4.1 times** the entry at the median, and nine entries run past 200 lines. So a lookup is two
calls and about four times the bytes, or three calls and the exact bytes, and the session pays for the overshoot on
every step after (D127).

### 1.5 What reads the record

| Reader | How it reads today |
|---|---|
| `daoris.json` `documents.decisions` | `docs/DECISIONS.md`; `check` holds the path to a file or folder, and `sync` renders the region's *Where things are* row from it |
| The service (`RepositoryScanner`) | the declared path first: a file is split at its `##` headings, one entry each, id `Daoris:docs/DECISIONS.md#D130 — …`; a declared folder is one entry per markdown file, titled by its file name (DOC5, D122 §2.1). A refresh replaces the repository's entries whole |
| `tools/doc-duplicates.mjs` | `docs/DECISIONS.md`, kind `decision`: a `## D<n>` twice fails. Its test holds that the records `.gitattributes` marks union are exactly the ones checked |
| `tools/doc-shapes.mjs` | reads no decision: the backlog, the archive and the router. The router's *Records* row names the record |
| `tools/merge-branch.mjs` | reads `.gitattributes` for the union records and reports them as *shared records* |
| The router | one row in *Records* |
| `CLAUDE.md`, the dispatch skill, `canon-authoring.md`, `add-pack` | 4, 1, 6 and 1 lines naming `docs/DECISIONS.md`, most with a number: *read `docs/DECISIONS.md` D19 first* |
| Every other citation | by number: 11,474 `D<n>` tokens in 1,196 tracked markdown, code and JSON files. No markdown link anywhere points into the record, and none carries an anchor to it. The nine `DECISIONS.md#D…` strings are test data in the service's entry-id format, for other repositories |
| `dogfood.test.ts` | splits `docs/DECISIONS.md` at `## D` and holds every entry from D51 on to a *Rejected* line; it refuses to pass on fewer than three entries |
| `tools/knowledge-bench.mjs` | excludes `DECISIONS.md` from its corpus by name |
| Adopters | their own record, read by the same scanner; nothing here is theirs |

### 1.6 What could not be measured

- **Whether a reader was misled by a torn entry** before it was repaired. The 27 torn merges were committed, and
  the repairs came later; what a session read in between is in no record.
- **The tokens a lookup costs.** Bytes only, as D127 counts.
- **Merges made another way.** The replay is git's command-line merge; whether any merge here was made by a web
  page or an editor is not recorded.

## 2. The options

### 2.1 (a) One file per decision

Each decision is `docs/decisions/D<n>.md`, holding its entry as written today, its notes under it. The replay was
run again with each decision merged as a file of its own (its section of base and of each side, three ways):

| Of the same merges (133 split by decision against their first base) | |
|---|---|
| Merges where both sides changed the same decision | 64 |
| Decision files both sides changed: a plain merge conflicts | 62 |
| …with union per file: a side's line lost | 0 |
| …a run of one side's lines broken by a line only the other added | 0 |
| …a note label left without the blank line before it | 12 |

**What it stops.** All four shapes. A note is written in its decision's file, so no other decision's lines can
come between them. There is no order to keep, since the order is the file names, so nothing is moved. One
decision added on both sides is one file added on both: with union, a version that is the other's prefix resolves
to the fuller one, and two different sets of notes are both kept whole (tried with `git merge-file`). With no
first tear there is nothing to carry on.

**What it leaves.** Two notes written under one decision at once meet in one file. Union keeps both whole, in merge
order. In 12 of the decision files above, a note's label lost the blank line before it, which a renderer reads as
the paragraph above. That is a fact a check can see (§3.4).

**What it costs.** One migration (§4). A reader of one decision reads one file, whose path is its number: one
call, exactly the entry's bytes, and no search. Every reader in §1.5 either needs nothing, or one line (§3.5).

### 2.2 (b) A merge driver

`.gitattributes` names `merge=decisions` for the record, and each clone sets `merge.decisions.driver` to a script
that parses the three versions into decisions and notes, merges each decision, and writes them in order.

- **A fresh clone has no driver.** The configuration is not tracked. Tried on git 2.53 with the attribute set and
  no driver configured: git falls back to its text merge and leaves conflict markers. That is the loud failure
  D106 removed, at every parallel decision, until someone runs the configuration step. Worktrees share the
  repository's configuration, so one step covers the parent's checkout and every subagent's, and
  `merge-branch.mjs` could pass it with `-c` for its own merges. A subagent's own merge of main would not get it.
- **The driver must find where a note ends.** That is the boundary §1.3 found no tool can read today. A driver
  that guesses wrong mis-merges silently, as union does, and every merge depends on it.
- **It is (a)'s merge, done invisibly** on each merge, while the file stays 9,801 lines and a lookup stays two
  calls and four times the bytes. Whether `merge-tree` and every tool that merges here run a configured driver
  was not measured.

### 2.3 (c) Notes in their own dated file

Each note becomes its own file per task (`docs/notes/2026-10-02-TOOL4e.md`, naming the decision it amends), and
nothing is appended under an older decision.

- **It stops the notes' tears**: a new file per task never meets another. It leaves a moved decision to a rule,
  and new decisions still share the end of the file, which union handles while both are new.
- **It splits a decision from its account.** D124 would be fourteen files. A session reading D125 searches first
  for what names it, and a note that names it differently is missed. The canon says an amendment goes under the
  entry it amends (`development-documents`, *a decision's amendment*), so (c) changes the canon or departs from
  it.
- **The service would read a second path** as decisions, and a role takes one path: a change to the CLI and the
  service, which are twins on the manifest's documents. The 178 notes already written would stand where they are,
  so one record would hold two conventions.

### 2.4 (d) Keep the union, check each note's place

A check that every note sits under its own decision, in `verify`.

- **It needs to know a note's decision**, which no note says (§1.3). Each new note would name its decision in its
  label, from a cut-over, and the 178 written would go unchecked.
- **It detects and never prevents.** At the measured rate a quarter of merges fail `verify` and wait for a repair
  written by script, as the 17 repairs so far were, and on 2026-10-03 five of eight.
- **It costs no migration**, and a lookup stays two calls and four times the bytes.

### 2.5 Side by side

| | Torn shapes it stops (of 33 merges) | A fresh clone | Reading one decision | Migration | What is left |
|---|---|---|---|---|---|
| **(a) a file per decision** | all four (33) | nothing to set | one call, exact bytes | once, scripted, proved by concatenation | two notes at once: both whole; a lost blank line, checked |
| (b) a merge driver | all four, once configured | conflict markers until configured | two calls, about 4× | none | a driver every merge depends on |
| (c) a file per note | the notes' (24), and the 6 they carried on; a move only by rule | nothing to set | 1 + one per note, after a search | a new convention beside 178 notes | moves and the shared end by rule |
| (d) a check | none; detects 33 | nothing to set | two calls, about 4× | none | a repair per tear, by hand or script |

## 3. The decision: one file per decision

### 3.1 The folder and the name

- **`docs/decisions/D<n>.md`**, the number unpadded, so the path is the citation: *D7* is `docs/decisions/D7.md`.
  A padded name (`D007.md`) would list in order and break that, so it is not used. A name with a slug would need a
  search to find.
- **The entry's bytes as they are**, its `## D<n> — …` heading first. Keeping the level means the files,
  concatenated in the old order, are the old record: that is the migration's proof (§4).
- **Notes stay under their decision, in its file**, appended at its end, as the canon says.

### 3.2 `docs/DECISIONS.md` becomes a fixed page

It keeps its opening (what the record is, and that a rejected decision is recorded too), loses *Order*, and says:
each decision is its own file, `docs/decisions/D<n>.md`; *D114* was never taken; the next number is reserved at
dispatch (D106); `grep '^## D' docs/decisions/*.md` lists them all. It holds **no rows**, so no branch ever edits
it, and every citation that names `docs/DECISIONS.md` with a number reaches its decision in one hop.

**No index.** A hand-kept index is one more place every decision branch writes, and a row that can disagree with
its file's heading. A generated one needs a generator, a staleness check, and its own merge story. Neither tells a
reader anything the folder and a search do not.

*Amended 2026-10-04 by D134's ORIENT1b note: a generated digest now exists, `docs/index/decisions.md`, since a
search names a decision's file and not the note inside it. A hand-kept index stays rejected.*

### 3.3 What union still does, per file

`.gitattributes` marks `docs/decisions/*.md merge=union`, and no longer `docs/DECISIONS.md`. A plain merge would
conflict on 62 of the 64 shared decisions measured, at the one place both appended. Union keeps both notes whole.
Their order is the merge's (*ours* first), not their dates', and each note carries its date and task. Two branches
that change the same line still keep both versions, as D106 says of every union record, and the parent still reads
the merged diff.

### 3.4 The check

`doc-duplicates` reads the folder in place of the file. These are facts, and they fail (D54):

- each `docs/decisions/*.md` is named `D<n>.md` and holds exactly one `## D<n>` heading outside a fence, its own;
- no line opens a conflict marker (`<<<<<<< `, `>>>>>>> `);
- a note's label (`**Built`, `**As built`, `**Fixed`, `**Read`, `**Amended`, `*Amended by`, and the other forms
  §1.3 counted) never follows a non-blank line;
- `docs/DECISIONS.md` holds no `## D<n>` heading and no note label, so a branch that wrote under an old decision
  before the migration is refused instead of landing in the page.

Its test still holds that the records marked union are exactly the records checked.

### 3.5 Each reader

| Reader | What changes |
|---|---|
| `daoris.json` | `documents.decisions` is `docs/decisions`; `sync` renders the row, and `check` already accepts a folder (it leaves a folder's ceiling unmeasured, and the record has none) |
| The service | reads the declared folder as it already does (DOC5): one entry per decision, kind *Decision*, id `Daoris:docs/decisions/D130.md`. A refresh replaces the old ids whole, so no ghost is left. A folder's record is titled by its file name, *D130*: DOC8c titles it by its first heading, which every adopter's folder of records gains too |
| `doc-duplicates` | §3.4 |
| `dogfood.test.ts` | reads each file in the folder as one entry, with the same rule from D51 on; left as it is, its guard fails on the page's zero entries, so the migration cannot pass without it |
| `doc-shapes` | nothing: the router's row it measures names `decisions/` |
| `merge-branch.mjs` | nothing: it reads the attribute, globs included |
| The router | the *Records* row names `decisions/` |
| `CLAUDE.md`, the dispatch skill, `canon-authoring.md`, `add-pack` | their lines name `docs/decisions/` and `D19.md`; the dispatch skill reserves the number after the highest file there, and a brief says *write `docs/decisions/D<n>.md`* where it said *append at the very end* |
| Code comments, design documents, the archive, the changelog, `canon/CHANGELOG.md` | nothing. A bare *D130* resolves by name; *`docs/DECISIONS.md` D19* resolves through the page. History stays as written |
| `knowledge-bench.mjs` | nothing: it lists `docs/*.md`, so the folder is not in its corpus, and the page is excluded by name |
| Adopters | nothing. Their records are their own; the CLI and the service already accept a folder for the role |

### 3.6 The canon does not recommend the shape, yet

The canon already allows it: the decisions role is a path, and a folder of records is a repository's own shape of
it (D122 §2.1; `init` and `analyze` name `adr/` and `docs/decisions/` as candidates). What tore here is the union
merge, which is this repository's process (D106) and not the canon's, at three branches at once. One repository's
measurement is below the bar of two (`development-documents`, *two sources reaching one rule*). It is reconsidered
when a second repository reports a record torn by parallel merges, with this design's §1 as the first source. The
words would then go in `development-documents`, beside *a decision's amendment*: a record many branches write at
once keeps one file per entry.

## 4. The migration

One commit, the parent's, since it crosses the docs, tools, cli and doctrine lanes and a half-done migration
leaves the record in two shapes.

1. **When no branch in flight holds a change to `docs/DECISIONS.md`.** A branch that wrote under an old decision
   lands first, or moves its note after.
2. **A scratch script splits the record** at each `## D<n> ` heading outside a fence, into `docs/decisions/D<n>.md`
   holding the entry's lines exactly, with one final newline. It refuses a number twice, a heading inside a fence it
   would split at, and a file that already exists.
3. **The proof:** the files, concatenated in the old record's heading order, equal the old record from its first
   heading on, byte for byte; 132 files; the numbers are 1–133 without 114. The commit body gives the counts.
4. **In the same commit:** the page (§3.2); `.gitattributes`; `doc-duplicates` and its test (§3.4); the dogfood
   test's *Rejected* rule; `daoris.json` and the region `sync` renders; the lines of §3.5; the router's row. The
   folder falls under the lane map's `docs/**`, so the map does not change. A late branch's note under an old
   decision then meets a page that is not union-merged and that its check refuses, so it conflicts or fails, and
   never lands.
5. **Its proof is the gates**: `verify` (the check over 132 files, `check` clean with the folder), the service suite
   and the family rehearsal at the merge, since the scanner reads the folder.

## 5. The build

Rows ready for `TASKS.md`, in the shape D127 sets. D134 decides all of it.

- [ ] **DOC8b — the check for a folder of decisions.** `doc-duplicates` gains the folder's facts: one heading per
  file, its own; no conflict marker; no note label after a non-blank line; no decision in the page. Contract:
  `docs/2026-10-03-decisions-record-design.md` §3.4. Proof: `doc-duplicates.test.ts`, each case failing first on a
  fixture folder. Lanes: tools; cli (the test).
- [ ] **DOC8a — the migration** (the parent's, after DOC8b). The record becomes `docs/decisions/D<n>.md`, the page
  stays, and the attributes, the manifest, the dogfood rule and the lines that name it move with it. Contract:
  §3.1–§3.5, §4. Proof: the concatenation check in the commit body; `verify`; the service suite and the family
  rehearsal at the merge. Lanes: docs, tools, cli, doctrine.
- [ ] **DOC8c — a folder's record titled by its heading.** The scanner titles a record in a declared folder by its
  first heading, and by its file name without one, so search shows *D130 — …*. Contract: §3.5. Proof:
  `RepositoryScannerTests`, failing first; the service suite. Lanes: service. Any time after DOC8a.

**Order.** DOC8b, then DOC8a with no branch in flight on the record, then DOC8c.

## 6. What a gate can prove, and what only use can

**The gates prove** the split (the concatenation), the folder's facts, the manifest and region (`check`), the
service reading 132 entries, and the union records matching the checked ones.

**Only use proves** whether the next fifty merges tear no decision, and how often two notes meet in one file. The
first integration after DOC8a with two notes under one decision is the case to read.

## 7. Considered and rejected

- **A merge driver** (§2.2): a step per clone, conflict markers without it, and a boundary guessed on every merge.
- **A file per note** (§2.3): one decision read in many files, against the canon's amendment rule, and a second
  path for the service.
- **Union plus a check alone** (§2.4): it finds the tear and leaves the repair, at a quarter of merges.
- **A plain merge per decision file**, without union: 62 of 64 shared decisions would conflict at the one place
  both appended.
- **An index**, hand-kept or generated (§3.2).
- **Padded or slugged names** (§3.1).
- **Promoting each heading to `#`**: it would break the byte-for-byte proof for a heading level no reader needs.
- **Serialising the merges.** D106's reason holds: it would remove the parallelism.
- **The other union records.** The archive and the fix log are written by the parent alone, the router and the
  twins table hold one-line rows, and the changelog's lines are checked whole, so none takes a note from many
  branches under an older entry. They were not replayed.

## 8. What this amends

- **D106**: *one file per decision* moves from rejected to decided. Its reason, *every anchor that cites one*,
  measured nothing to break: no link points into the record (§1.5).
- **D117 §2.4**: it kept the one log until union merge failed again. It has, 33 times (§1.2). Its other reason,
  *a name per note and a map from every number*, does not hold for decisions: the number is the name.
- **`docs/2026-09-30-parallel-development-design.md` §7**: *renumbering decisions into files* is no longer
  rejected, and nothing is renumbered.
- **The dispatch skill**: the parent reserves after the highest file in `docs/decisions/`, and a brief names the
  file to write.

## 9. What this document's gate does not cover

This change is documents only, and nothing is built.

- **Measured by scratch scripts**, untracked, over the history at `155882ec`. The tear is judged by two facts
  (§1.1), so a tear of short lines only, or one that kept a line in the right place and a paragraph in the wrong
  one, is not counted, and the 33 is a floor.
- **The per-file replay** merged each decision's section as a file with `git merge-file`, not through
  `merge-tree`, and its base for a criss-cross is the first merge base, not git's virtual one.
- **The driver's fallback and the add/add merges** were tried on small files, on git 2.53. Whether `merge-tree` runs
  a configured driver was not.
- **Not measured**: what a session read from a torn entry, and the tokens of any read.
- **`verify` checks** this document's links, the decision log's shape, the budgets and the duplicates, and none of
  these words.
