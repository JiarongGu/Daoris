# Session economy — what a session reads and writes, as doctrine

**Standing:** contract under D127. Its notes record the canon and shape-report implementation;
remaining measurement and relocation work is in `TASKS.md`. The counts below describe the named
baseline, not the current repository.

> The owner, asked on 2026-10-02 whether the task archive should be split (SESSOPT1): *"this is not about how we
> split its about how we optimize the session and this should also belong to doctrine too"*. This is the study
> and the contract, and its decision is **D127**. Original status: **design before implementation.** It folds in DOC7 (*what
> sessions read, measured*, `docs/2026-10-01-development-documents-design.md` §1.5). It builds on the standard of
> D122 and amends it where §9 says. Read with **D54**, **D106**, **D115** §5, **D117** §4.2, **D122** and
> **D124**.

- §1 is what a session here reads and writes, measured at `7e4cb1c`, and what could not be measured.
- §2 is the doctrine, as canon text drafted and measured. §3 is the check that reports it. §4 is this
  repository's practice. §5 is how the doctrine reaches other repositories.
- §6 is the build, as rows in the shape §2 sets. §7–§10 are what only a real run proves, what was rejected,
  what this amends, and what this document's gate does not cover.

## 1. What a session reads and writes, measured

### 1.1 How

Every count is from the committed tree at `7e4cb1c`. A word is a whitespace-separated token, as
`tools/doc-budgets.mjs` and `check` count one. A byte is a UTF-8 byte. The scripts that counted are scratch,
under the gitignored `local/`, and are not tracked: each figure below says what it counted, so it can be
counted again. The backlog's size in this row's dispatch (*about 7,600 words*) differs from this commit's: at
`7e4cb1c` it measures 8,503. The parent's uncommitted working copy was not read. Main moved to `e4dce8f` while
this was written. There the backlog measures 8,210 words, the archive 125,970 and `CLAUDE.md` 3,750, and the
canon, the skills and the region did not change.

### 1.2 At the start of every session

What Claude Code loads here before the first word of a task: `CLAUDE.md` and the `AGENTS.md` it imports.

| What | Bytes | Words | Note |
|---|---|---|---|
| `CLAUDE.md` | 24,404 | 3,749 | 302 lines; its ceiling is 3,750 words. *Current state* is 1,567 of them |
| `AGENTS.md`: the index (four tables, *Where things are* among them) | 7,435 | 1,147 | *Where things are* is 758 bytes |
| `AGENTS.md`: eight rule bodies | 16,715 | 2,790 | `task-lifecycle` is 1,758 bytes |
| **Both files** | **48,554** | **7,686** | `check` measures the region at 24,064 of 26,000 bytes |
| Eight skill descriptions the harness lists | 2,474 | — | the bodies (323 to 1,343 words) load on use |

The harness's own instructions and tool definitions come first, and they are not this repository's to measure.

`CLAUDE.md` grew from 778 words (2026-08-04) to 3,126 (2026-09-20) and reached 3,750 on 2026-09-23. It has sat
between 3,601 and 3,750 since. The ceiling stopped the growth. The brief's content test (D122: no status, no
history, no counts, no quotations) has not been applied, and *Current state*, which is status and history, is 42%
of the file.

### 1.3 A typical task

**Picking work** reads the backlog whole (`development-documents`: *whole, when picking work*).

| `TASKS.md`, 53,769 bytes, 8,503 words, 650 lines | Words |
|---|---|
| The opening: the open-only note, the goal, the arcs already closed | 207 |
| *State*: the counts' one home | 181 |
| *Handover*: where the last session left it | 362 |
| *Handover*: traps *"not in any contract"* | 676 |
| *Backlog*: its opening, with *"Eighty-six rows are open"* | 172 |
| *Backlog*: 86 rows | 5,001 |
| *Backlog*: section headings, contract and order lines | 1,301 |
| *Backlog*: the owner's quotations, 29 lines | 493 |
| *How to work a task* | 110 |

Its ceiling is 6,600 words (`daoris.json`). The standard's starting ceiling is 5,000.

**Starting any task** reads the router (`doc-loader`): `docs/README.md`, 17,822 bytes and 2,764 words, which is
over the standard's 2,500. It has 69 rows: median 25 words, and the longest 223 (the tools design's row). Twelve
rows run over 60 words, and 537 words sit beyond the 60th word of each. The long ones are not descriptions. Their
*Where it stands* cell has become a list of every row built under the contract.

**A contract** is read for the section a row names. The 35 design documents run to a median of 3,938 words. The
18 written since 2026-09-27 have a median of 5,058, and the nine of 2026-10-01 run from 4,143 to 11,271. Of the 86
backlog rows, 45 name a section.

**A skill or knowledge document** is read on use. `dispatch-subagent` is 1,343 words, `set-up-documents` 959 and
`doc-loader` 323. The knowledge documents run from 550 words (`model-decoupling`) to 3,227 (`twins`).

**A decision** is looked up. The record is 623,262 bytes and 103,197 words, in 123 entries: median 586 words, the
90th percentile 1,659 and the longest 5,110. August's 24 entries have a median of 249 words, September's 76 of
646, and October's 12 of 2,224, because their notes carry each build: D122 is 3,709 words with its notes, and D124
4,837.

**This row's own dispatch** named 22 files to read before writing: 289,837 bytes and 46,762 words, plus D54 and
D122 (4,275 words). Of these, the two neighbouring designs read whole for the house style were 19,845 words. Here
both were also inputs.

### 1.4 Writing a record

**The archive** is 766,964 bytes, 125,042 words and 9,397 lines: 349 headed sections, with 132 more entries as
checklist items under its earlier headings. It grew from 13,211 words on 2026-09-19 to 104,220 on 2026-09-30 and
124,119 on 2026-10-01. That last day added about 19,900 words, 13,163 of them in 72 outcome paragraphs.

| Month | Sections | Median words | 90th percentile | Longest |
|---|---|---|---|---|
| 2026-08 | 21 | 352 | 553 | 1,694 |
| 2026-09 | 246 | 344 | 690 | 1,201 |
| 2026-10 | 76 | 250 | 390 | 703 |

The 149 entries written with an **Outcome** paragraph, all from 2026-09-28 on:

| Month | Entries | Outcome, median words | 90th percentile | The quoted row, median |
|---|---|---|---|---|
| 2026-09 | 73 | 214 | 307 | 36 |
| 2026-10 | 76 | 190 | 240 | 38 |

Of the 149 outcomes, 140 run over 100 words and three are 60 or fewer.

**What an outcome repeats**, two ways:

- **Word for word: little.** For the 93 outcomes that cite a decision, a median of 5% of their six-word runs
  appear in the cited decision (the 90th percentile 13%, the most 36%).
- **Fact for fact: most.** A code span (an identifier, a file, a command, a field) is a fact a reader could check.
  Of the 86 outcomes that cite a decision and hold three spans or more, a median of 75% of their spans appear in
  the decision they cite (the quartiles 50% and 88%). Since 2026-10-01 it is 80%, across 56 outcomes, and 65 of
  the 86 have at least half. An outcome is a second account of its decision's note, in other words. Whether the
  other quarter of the spans is in the design or the commits was not measured.

**One landing, told in five places.** WSSETUP5 is told in D124's *As built (WSSETUP5)* note, 641 words; in its archive
outcome, 208 words, 16 of whose 17 code spans are in that note; in a changelog line of 85 words, shared with
WSSETUP3; in the router's row for its design, 185 words, which lists every row built under it; and in the design's
own status line. The changelog's job is a user's, so it is a third record, not a copy. The archive outcome and the
router's list are copies, and both are written again at every landing.

**What the canon asks for** does not agree with itself:

| Source | The outcome it asks for |
|---|---|
| `task-lifecycle` (core rule, always loaded) | *"the completion date plus a one-line outcome"* |
| `development-documents` (core knowledge) | *"finished work, each with its date and outcome"* |
| `set-up-documents`' `archive-entry.md` (core template, DOC2) | *"One paragraph: what was built, what proved it, and anything left out"* |
| D122 §2.5 | *"one paragraph of outcome"* |
| `dispatch-subagent` (this repository's skill) | *"an outcome paragraph the parent can paste under the row"* |
| D115 §5, the steward | *"The archive entry, with the outcome paragraph"* |

The rule is the only source every session reads, and the only one the practice does not follow.

**A backlog row.** The 86 rows hold 5,001 words: median 37, the 90th percentile 105, the longest 769. The ten
longest are FLAKE1 (769), TOOL5 (225), TEST1 (185), REH1 (176), BRW3 (168), FG5 (154), TOOL4 (134), COST1 (106),
SEM2 (105) and TOOL4d (97). Seventeen rows run over 60 words and hold 2,648 between them.

They are not pasted design rows. A median of 3% of a row's six-word runs appear in any design. The 13 rows with
30% or more are short ones, 10 to 33 words. The long rows are something else:

- **logs of sightings**: FLAKE1, TEST1 and REH1. D115 §5 says *"Any FLAKE line goes under FLAKE1"*, so every
  merge that sees a flake appends to a row every session reads whole;
- **histories of runs**: BRW3 and FG5;
- **reasoning for a hold**: TOOL5 and SEM2;
- **a decision restated**: TOOL4 retells D125;
- **a measurement**: COST1.

### 1.5 How the backlog grew, and what each trim did

`TASKS.md` in words, at the last commit of each day:

| 08-06 | 09-19 | 09-21 | 09-23 | 09-25 | 09-26 | 09-27 | 09-29 | 09-30 | 10-01 | 10-02 |
|---|---|---|---|---|---|---|---|---|---|---|
| 494 | 1,183 | 6,524 | 6,578 | 3,976 | 3,921 | 6,558 | 5,964 | 5,244 | 7,465 | 8,503 |

It was trimmed twice, and it grew back each time, because the rows went on being written the same way. A ceiling
that reports says when the file is long. It cannot say which kind of line made it long.

### 1.6 What a session pays for a read

COST1 and METER1 record one long driven turn: 48 minutes, about a thousand tool calls, its context growing from
48K to 679K tokens on a 1M window. It read 663K tokens anew and 68.5M from its cache, so it re-read about 103
tokens for every token it read anew. These are the records' numbers, not re-measured here. The records do not say
how many model requests the thousand calls were, so a cost per call cannot be derived from them.

What they do show is the mechanism. **A harness sends the context it holds with every request.** A cache makes the
repeat cheaper. It does not make it free. So a session pays for a document on every step after it reads it, and
what it read first, it pays for longest. The 48,554 bytes of §1.2 are carried by every step of every session here.
A backlog read whole at a task's start is carried by every step after.

### 1.7 What could not be measured

- **Tokens.** A token is the tokenizer's, and the tokenizer is the harness's. Every size here is in bytes and
  words. No figure is converted.
- **What sessions here actually open, when, and whether whole.** The install's conversation records hold each tool
  call and the files it touched, under the home (D76). This design did not open the home, and the harnesses' own
  transcripts under the user profile are the owner's. DOC7 is the line that counts it (§6), and its one-off
  script is the owner's to run over the records already kept.
- **How many steps a typical session here takes.** The usage record has each session's tool calls on the install.
  No tracked record has a distribution of them.
- **Whether a shorter outcome loses something a later reader needed.** That is judgement, and the owner's review of
  the first entries in the new shape is how it is found out (§7).

### 1.8 Findings

1. **The canon contradicts itself** on an archive entry: the rule says a line, and its own template says a
   paragraph (§1.4). The practice followed the template.
2. **Outcomes are second accounts.** Three quarters of an outcome's facts are already in the note it cites, in
   other words.
3. **The backlog is long by its logs, not its rows of work.** Five of its ten longest rows are logs of sightings
   or histories of runs, 1,452 words between them, and one rule (D115 §5) appends to it at every flaky merge.
4. **The router's *Where it stands* has become a build log.** The same list the decision's notes keep.
5. **The brief holds status.** *Current state* is 42% of `CLAUDE.md`, which sits at its ceiling (§1.2).
6. **The backlog holds a moving counter.** *"Eighty-six rows are open"* changes with every row and duplicates what
   a count derives. D106 took the same kind of counter out of `CLAUDE.md`.

## 2. The doctrine

### 2.1 The principle, in the canon's words

**A session pays for what it reads on every step after it reads it.** So what is read whole stays short by how
each entry is written, and what is long is read by lookup. Each fact has one home, and every other place names
that home in a line.

That one principle gives every shape below. It is project-agnostic: it names no harness, no model and no file. It
needs nothing running: a session that reads a row of the right shape benefits whether or not any tool exists
(D48 §2a).

### 2.2 `task-lifecycle`, amended

The rule is always loaded, so every byte it gains is paid by every session in every adopter. It gains the
principle in one sentence and the shapes in its existing bullets. Its frontmatter does not change: a rule's body
is in the region whole, so its index row only repeats it. Drafted and measured:

```markdown
Sessions pay for what they read on every step after: a long row is paid for by everyone who picks
work, and an entry that retells a decision by everyone who looks it up.

- **Backlog:** open tasks only, as checklist items grouped by theme: what and why in two sentences, the
  contract and the proof, a `file:line` where known. The detail stays where it lives.
- **On completion — move, don't tick.** In the same change or its follow-up: cut the entry out of the
  backlog, paste it into the archive under the right heading, and add the completion date and an outcome
  in a line or three: what changed, and where its detail lives. Preserve the original wording so the
  archive stays a faithful record.
- **Three records, three jobs, no duplication:** the backlog is what is still TODO; the archive is the
  per-task history; the changelog is the release-facing, user-visible log. What a decision, a design or
  a commit already says is pointed to, never told twice.
```

The first paragraph closes `## Why`; the bullets replace their namesakes. *"A line or three"* keeps the rule's
*one-line* and makes room for a pointer and a row left behind. The template, the decision and the skill now say
the same.

### 2.3 `development-documents`, amended

The knowledge document is on demand. Only its index row is always loaded. It gains:

- **A fifth silent failure** in `## Why`: *a record written twice, and paid for on every step*. A document's cost is
  its size times the steps that follow it, and a cache makes the repeat cheaper, never free. A retold entry costs
  every later reader and tells them nothing the first copy did not. A backlog trimmed without changing how its
  rows are written grows back.
- **A section**, *What a session pays for: one home per fact, and entries that point*, after the three ways a
  document is read:
  - **a backlog row**: an identifier; what and why in two sentences; its contract, by name and section; its proof.
    Never the design's own text, a log of sightings or a history: those are records, and the row points to them;
  - **an archive entry**: the row as it stood, the date, and the outcome in a line or three: what changed, and
    where its detail lives;
  - **a router row**: the document, its kind, what it is for, and where it stands in a line, naming the decision
    that changed it. What was built under it is that decision's;
  - **a decision's amendment**: under the entry it amends, headed by the work that made it;
  - **look up by identifier**: a record is searched for the identifier a row or a decision names and read at that
    entry; a design is read at the section a row names. Reading a record whole to find one entry pays for every
    other entry in it.
- **The entries' shapes, beside the starting ceilings**: a backlog row or a router row, 60 words; an archive
  entry's outcome, 60 words. They report, like every ceiling.
- **Its frontmatter**: `applies_when` gains *writing an entry into one*, so `doc-loader` routes a session that is
  about to write a record here, and `enforces` gains *an entry points to its detail*.

The document grows from 1,529 to 1,891 words on the draft.

### 2.4 `set-up-documents`, amended

- **`templates/archive-entry.md`**: the outcome's placeholder becomes *"A line or three: what changed, and where
  its detail lives (the decision, the design's section, the commits). What those already say is pointed to, not
  told again."*
- **Step 4**, *Place the records*, gains one sentence: a backlog row that holds more than its shape is open work,
  so its excess moves to the record that holds it (a log of sightings to the fix log, a history to the decisions,
  a design's text back to the design), and the row points there. Old archive entries still stand as written.
- **`templates/backlog-row.md`** already has the shape (44 words with its placeholders) and does not change.

### 2.5 No new knowledge document, and what the region pays

A new core knowledge document, `session-economy`, was weighed. It would cost every adopter an index row in the
always-loaded region (DOC2's two rows cost 634 bytes, D122's note), and it would split one subject across two documents:
`development-documents` already owns *a document read whole has a ceiling, and a record read by lookup has none*,
which is this principle's other half. So the two existing documents carry it.

Measured on the drafts, by syncing a scratch copy of each repository against a scratch canon:

| Repository | The region before | After | Change |
|---|---|---|---|
| this one | 24,064 of 26,000 | 24,522 | +458 |
| `examples/engine` | 21,758 of 30,000 | 22,216 | +458 |
| `examples/game` | 21,975 of 30,000 | 22,433 | +458 |

Of the 458 bytes, 400 are the rule's body and 58 the knowledge document's index row. The root `AGENTS.md` here goes
from 24,150 to 24,608 bytes, under the 32,768 that one agent cuts at. The build may reword the drafts, so its
proof measures again.

### 2.6 Canon-authoring holds

No product, harness, command, path or model is named. *The decisions record*, *the design*, *the history of
changes* and *the fix log* are roles, which the generated index places. Nothing needs a service. The shapes are
reported numbers, not gates (D54), and the old entries stand as written (`set-up-documents` step 4).

## 3. The shape report

### 3.1 What it reports

`tools/doc-shapes.mjs`, run by `verify` after `doc-budgets`. It prints, and exits 0:

- **backlog rows over their shape**: each checklist item and its indented lines, in the declared backlog, over 60
  words, named by its identifier with its count, longest first;
- **archive outcomes over their shape**: each entry headed on or after the cut-over date (§3.5) whose outcome is
  over 60 words. Entries before it are counted once, as *standing as written*, and never measured;
- **router rows over their shape**: each row of the declared router whose first cell is a code span, over 60
  words.

Today it would print 17 rows, 12 router rows and no outcomes. It counts a word as `doc-budgets` does, and imports
that tool's `words`, so the two counts can never disagree.

### 3.2 Where it lives, and why a sibling

- **Not inside `doc-budgets`.** That tool's reasoning is *a ceiling belongs on a document read whole, never on an
  append-only record*. A shape belongs on an entry of either. Folding entry parsing into it would blur the one line
  it exists to draw.
- **Not in `check` yet.** `check` ships to every adopter. Splitting an adopter's archive into entries guesses at a
  format that `development-documents` leaves to each repository, and a wrong guess would be a false report in
  every session there. The canon's text and template carry the shape everywhere. The report stays this
  repository's until a second repository keeps the template's shape and asks (§8).

### 3.3 What it reads

The roles in `daoris.json`'s `documents`, `backlog`, `archive` and `router`, by the path each declares, as
`doc-budgets` reads a declared ceiling (DOC4). Its numbers and the cut-over are in `tools/doc-shapes.json`, with a
`_why`, as `doc-budgets.json` keeps its own. A number is raised by a one-line diff there, with the reason in the
commit.

### 3.4 What fails

A fact fails, a judgement reports (D54). It exits 1 when the configuration does not read, when it names a key the
tool does not know, or when the cut-over is not a date, because each of these means a shape that silently stopped
applying. A declared role with no file on disk is already `check`'s failure, so here it is one line and no second
failure.

### 3.5 The cut-over

The date of the merge that lands the dispatch skill's change (SESSOPT1c). The steward writes it into
`tools/doc-shapes.json` in that merge. From then on, every archive entry is written by the new hand-back, so every
entry the report measures is one the new shape asked for.

## 4. This repository's practice

### 4.1 The dispatch skill's hand-back

The subagent's hand-back keeps *the shape built, in a paragraph a reviewer can hold the diff against*. That
paragraph is read once, by the parent at the merge, and is not recorded. Its last item changes from *an outcome
paragraph* to:

> - **the outcome, in one line**, and where its detail lives: the decision's note, the design's section. The
>   parent pastes it under the row in `docs/task-archive.md` with the merge commit;
> - **any rows it leaves**, in the backlog-row shape.

The parent's step 5 pastes *the hand-back's outcome line* rather than its paragraph. A design dispatch names a
style by one exemplar and the sections to mirror, rather than two designs read whole (§1.3: 19,845 words).

### 4.2 The steward's record step

The row is moved as it stood, which is cheap once rows have their shape. WSSETUP5, as it would have been written:

```markdown
## WSSETUP5 — registration follows the line (2026-10-02)

> - [ ] **WSSETUP5 — registration follows the line** (§3; driver, modules, cli; after LAYOUT7): three moments, read as git
> objects, a twin of `connect`'s `registration()`, `registry.followed`, `daoris-driver register`, the row's *Refresh*.

**Outcome.** Built: the driver registers a repository from what its line declares, after Daoris moves the line, as a
watch starts, and on `daoris-driver register`. Detail: D124's *As built (WSSETUP5)* note; merged in `7e4cb1c`.
```

The outcome is 34 words with its label, where the paragraph was 208. Every fact the paragraph held is in the note,
the merge, or the row. D115 §5's steward does the same when it exists, and its FLAKE lines go to the fix log's
FLAKE1 entry, not the backlog (§4.3).

### 4.3 `TASKS.md`, trimmed by the rule

Nothing is deleted that no other document says. A line that goes is either moved to the home below, or already
said there, which the build checks line by line (`set-up-documents`' test: *does anything else say it*).

| What | Words now | Where it goes | Words after, about |
|---|---|---|---|
| The opening's list of closed arcs | 207 | the router and the archive already name each arc; the open-only note and the two pointers stay | 90 |
| *State* | 181 | stays: the counts' one home | 181 |
| *Handover*, where it left off | 362 | condensed to what is in flight and what waits on the owner | 200 |
| *Handover*, the traps | 676 | each to the folder it is about: the desktop's (its README until LAYOUT6's rooms), the service's, the twins document, or nothing where the line repeats `CLAUDE.md`'s dev loop or `twins.md` | 0 |
| The backlog's opening | 172 | the row count goes (§1.8, finding 6); what merges next stays | 90 |
| The owner's quotations | 493 | each design already opens with its ask; the heading keeps *(owner, date) — Dn* | 0 |
| Headings, contract and order lines | 1,301 | stay | 1,301 |
| The rows | 5,001 | each to its shape. FLAKE1's, TEST1's and REH1's sightings become open entries in the fix log; BRW3's and FG5's runs go to the first-goal study's §8, which already keeps the loop's legs; TOOL5's and SEM2's reasoning to the note of the decision that holds them; TOOL4's to D125, where it already is; COST1's numbers stay in METER1's entry | about 3,000 to 3,400 |
| *How to work a task* | 110 | it repeats `CLAUDE.md`; the line *work left behind is a row* stays | 30 |

About 4,900 to 5,300 words, from 8,503: under the 6,600 ceiling and near the standard's 5,000. The ceiling stays at
6,600 for its headroom, and the shape report holds the rows.

**The fix log's open entries.** A flake has no root cause yet, and its sightings are the evidence the cause will
be found from. They are read by lookup when a test fails, which is the fix log's way of being read. So FLAKE1,
TEST1 and REH1 each get an entry headed *(open)*, with *Sightings* in place of *Root cause* until one is found. The
row keeps what to do and its proof.

### 4.4 The router's rows

Twelve rows run over 60 words. Each *Where it stands* becomes one line, naming the decision whose notes list what
was built: for example, *Designed (D121); built in part, and D121's notes say what*. Before a list leaves the
router, the build checks that every row it names is in that decision's notes, and writes a pointer into the note
where one is missing.

### 4.5 The archive from now on, and its 349 entries

- **From the cut-over**: the row as it stood, the date, and the outcome in a line or three, with where its detail
  lives and any row it left.
- **Not split.** A reader by lookup pays per entry, not per file: a search by identifier finds an entry in one file
  as fast as in one of several. Splitting moves bytes and changes no session's read. It would also break every
  link that cites the file, and change the declared archive role, the union line and the duplicates check (D106)
  for nothing a session reads.
- **Not compacted.** The archive is a faithful record (`task-lifecycle`), and old entries stand as written
  (`set-up-documents` step 4). No session reads it whole, so compaction saves no session a read. It costs a large
  rewrite, reviewed entry by entry. And only part of the detail is provably elsewhere: 94 of the 149 outcomes cite
  a decision at all, a quarter of their facts are not in it, and the 132 checklist entries and the older sections
  were never measured against anything. §8 says what a compaction would take, if the owner chose one anyway.

### 4.6 The brief

`CLAUDE.md`'s move is LAYOUT6's (D117 §4.2: into the root `AGENTS.md`, about 1,300 words, and eight rooms). LAYOUT6
gains one thing in its proof: the brief that moves is held to the content test, so *Current state* (1,567 words)
leaves for its homes rather than moving whole. No row here duplicates LAYOUT6.

## 5. The doctrine reaching other repositories

- **One commit, every copy.** SESSOPT1a changes the canon, adds an entry under `## Unreleased` in
  `canon/CHANGELOG.md`, and re-syncs this repository and both examples in the same commit, as WSSETUP10 did. The
  family rehearsal holds that the examples are current.
- **The set-up quest does not contradict it.** LAYOUT7's `SetupBrief` tells a set-up session to read
  `set-up-documents` and `development-documents` after its sync, and to write the brief and declare the records
  from the skill's templates. It quotes no record's shape, so it picks up the new shapes from the templates, with
  no change. Its twin, the adoption playbook, does not quote a shape either, so `SetupBriefTests`' composer table is
  unchanged.
- **The driven session's closing note is not a record.** A quest's note (D124 §2.7 for a set-up) is read by the
  person and, once it exists, the steward, who writes the outcome line from it as judgement (D115 §5).
- **Without the tool.** Every piece is a committed file: a rule, a knowledge document and a template. A repository
  with no doctrine tool reads the same text and keeps its rows short by judgement.

## 6. The build

Rows ready for `TASKS.md`, in the shape §2 sets. Lanes are `daoris.lanes.json`'s ids; *doctrine* and *docs* are
the laneless groups. D127 decides all of it, so no row takes a number unless it finds something D127 did not
decide. A row with a rehearsal in its proof is proven by the parent at merge.

- [ ] **SESSOPT1a — the doctrine, in the canon.** `task-lifecycle`, `development-documents` and the archive-entry
  template say a session pays for what it reads, so rows and entries point to their detail. Contract: §2. Proof:
  `verify` (`check` clean, the region within 100 bytes of §2.5's +458); the family rehearsal. Lanes: doctrine,
  with `examples/` re-synced in the same commit.
- [ ] **SESSOPT1b — the shape report.** `tools/doc-shapes.mjs` reports backlog rows, router rows and new archive
  outcomes over 60 words, from the declared roles, and exits 0; a configuration it cannot read fails. Contract:
  §3. Proof: `doc-shapes.test.ts`, each case failing first; `verify` prints the report. Lanes: tools; cli (the
  test).
- [ ] **SESSOPT1c — the hand-back is a line.** The dispatch skill's hand-back carries the outcome in one line and
  where its detail lives, and its rows in the backlog-row shape; the parent pastes that line. Contract: §4.1.
  Proof: the next merge's archive entry measures 60 words or fewer. Lanes: doctrine (`.claude/skills/`). After
  SESSOPT1a.
- [ ] **SESSOPT1d — the backlog, trimmed by the rule** (the steward's, after SESSOPT1b and SESSOPT1c). Each line
  over its shape moves to §4.3's home; FLAKE1, TEST1 and REH1 get open fix-log entries. Contract: §4.3. Proof:
  `doc-budgets` 5,300 words or fewer; `doc-shapes` no row over 60; the commit body names each line's home. Lanes:
  records; docs.
- [ ] **SESSOPT1e — the router's rows, by shape.** Each *Where it stands* becomes a line naming its decision, after
  a check that the decision's notes hold every row the list named. Contract: §4.4. Proof: `doc-shapes` no router
  row over 60; the router under 2,500 words. Lanes: docs. After SESSOPT1b.
- [ ] **DOC7 — what sessions read, measured** (folded in). `session.read {session, adapter, role, how, call}` and
  `session.skill` join the machine log; the usage report gives reads per role, whole or not, and the steps carried.
  Contract: §6.1. Proof: `SessionLog` tests (a role, no role, no path in any line); the report's parse table.
  Lanes: driver; tools.
- **LAYOUT6, amended** (the row is open): its proof gains the brief's content test, so *Current state* leaves for
  its homes and the brief measures 1,500 words or fewer. Contract: D117 §4.2 and §4.6 here.

**Order.** SESSOPT1a, SESSOPT1b and DOC7 side by side. SESSOPT1c after SESSOPT1a. SESSOPT1d after SESSOPT1b and
SESSOPT1c. SESSOPT1e any time after SESSOPT1b. Only SESSOPT1a changes the canon.

### 6.1 DOC7, as this design takes it

D122 §1.5 designed `session.read {session, adapter, role, beforeEdit}`. Session economy needs two more facts, and
both are numbers or words from a fixed list, within D94 §4:

- **`how`**: `whole`, `part` (a read with a range) or `search`, taken from the tool call's input; `null` where the
  door's frame does not say, never a guess (ACP3's rule).
- **`call`**: the read's place among the session's tool calls. With the session's total calls, which the usage
  record already keeps, the report says how many steps each read was carried for.

`beforeEdit` is kept. A read that resolves to no declared role writes nothing. The one-off script over the records
already kept is the owner's to run. Its counts are the *before* for SESSOPT1d.

## 7. What a gate can prove, and what only a real run can

**The gates prove the mechanism**: the canon's frontmatter and scans; `check` clean in this repository and both
examples, with the region's bytes; the examples current (the family rehearsal); the shape report's counts and its
failures; `session.read` carrying no path.

**Only use proves the rest:**

1. **Whether an outcome of a line or three is enough.** The owner's review of the first entries after the cut-over,
   and whether a session ever looks up an entry and then has to search further.
2. **Whether rows stay short** once the report exists, over the next weeks of merges, without another trim.
3. **What sessions read, and how long they carry it** (DOC7's counts), and so whether the 60-word shapes are the
   right numbers.
4. **Whether a session that reads less does the work as well.** No record can show this. It is judgement, read
   from the landed history.

## 8. Considered and rejected

- **Splitting the archive** (§4.5). It moves bytes and changes no session's read, and breaks every link to it.
- **Compacting the 349 entries.** Each would need a check that every fact dropped lives elsewhere, and only part
  provably does (§4.5). If the owner chose it anyway, it would take one commit, revertable, that keeps every
  heading and quoted row, shortens only outcomes whose every code span and number is found in the decision or
  design they cite, and a scratch script that proves that for each entry before the commit. The others would stand.
- **A new core knowledge document.** An index row in every adopter, and one subject in two documents (§2.5).
- **The shapes in the always-loaded rule only.** The rule carries the principle and the two shapes every task meets.
  The router's and the decision's shapes are on-demand detail (canon-authoring: split principle from detail).
- **Changing the rule's frontmatter.** The body is in the region whole, so the index row only repeats it, for 49
  bytes.
- **A shape check that fails.** D54: whether an entry says too much is a judgement.
- **The shape report in `check`, now** (§3.2). Reconsidered when a second repository keeps the template's shape
  and asks for it.
- **Raising the backlog's ceiling.** The file is long by lines that belong in records (§1.4). Raising it would
  leave them there.
- **Trimming without a shape.** It was done twice, and the file grew back each time (§1.5).
- **Deleting what a long row holds.** A sighting, a history and a reason for a hold are evidence. They move to the
  record that holds that kind of thing.
- **Pasting the hand-back's paragraph into the decision's note instead.** The note is written by the branch with
  its work (D115 §5). A second paste would make the note the copy.
- **Changing `persist-working-state`.** Its *in-progress plan state → the backlog* stands. A row says where the
  work stands in a line. What happened goes to the record that keeps history, which is what the new shapes say.
  An always-loaded rule changed for a nuance the knowledge document carries would cost every adopter bytes.

## 9. What this amends

Each row that builds a piece notes the amendment where it lands.

- **The canon's `task-lifecycle`**: the outcome is a line or three with where its detail lives; a row's shape; one
  sentence of why (SESSOPT1a).
- **The canon's `development-documents`**: a fifth silent failure, the entries' shapes, look up by identifier, the
  shapes beside the ceilings, and its frontmatter (SESSOPT1a).
- **The canon's `set-up-documents`**: the archive-entry template and step 4 (SESSOPT1a).
- **D122 §2.5**: an archive entry's outcome is a line or three, not a paragraph. **D122 §1.5 and §6**: DOC7's line
  gains `how` and `call`.
- **D115 §5**: the steward's archive entry carries the outcome line; FLAKE lines go to the fix log's FLAKE1 entry.
- **The dispatch skill** (MOD9, `docs/2026-09-30-parallel-development-design.md` §3 rule 7): the hand-back's
  outcome is a line, and its rows take the backlog-row shape (SESSOPT1c).
- **D117 §4.2**: LAYOUT6's brief is held to the content test as it moves.
- **D106**: *no moving counters in always-read files* reaches the backlog's row count (SESSOPT1d).

## 10. What this document's gate does not cover

This change is documents only, and nothing is built.

- **Read from the tree at `7e4cb1c`**: `CLAUDE.md`, `AGENTS.md`, `TASKS.md`, `docs/README.md`, `docs/DECISIONS.md`,
  `docs/task-archive.md`, `CHANGELOG.md`, every design document's size, the canon's core rules, knowledge and skills
  with their templates, `.claude/knowledge/`, `.claude/skills/`, `daoris.json`, `daoris.lanes.json`,
  `tools/doc-budgets.mjs` and `tools/doc-budgets.json`, `SetupBrief.cs`, and `TASKS.md`'s, `CLAUDE.md`'s and the
  archive's history.
- **Measured by scratch scripts**, untracked. The repetition measures are proxies: six-word runs for words, code
  spans for facts. Neither reads meaning. The other quarter of an outcome's facts was not looked for in the
  designs or the commits.
- **The region's bytes were measured on drafts.** The build may reword them.
- **The long turn's numbers are COST1's and METER1's**, not re-measured. No token count was taken anywhere.
- **Not measured**: what sessions here open, and when (DOC7); how many steps a session takes; whether shorter
  records cost a later reader anything.
- **`verify` checks** this document's links, the decision log's shape, the budgets and the duplicates, and none of
  these words.
