# Knowledge freshness: what a hit says of its standing, and how a stale record is found

**Standing:** proposed, round 2 (KNOWFRESH1a under D191), after Codex's and the Opus reader's reads of round 1 (§7).
**Governing:** D220 and the canon's `current-knowledge`. Parts b (Lyntai's memory, quest `#d9c80453e46f`) and c (the
machine's memory server) are not this design's.

## 1. The problem as the code shows it

- **A hit is current but silent about its standing.** Ids are locations (`Knowledge/KnowledgeEntry.cs:165-172`) and a
  moved checkout is read again (`Knowledge/Store/CheckoutFreshness.cs:3-42`), but only `knowledge_refresh` tells the
  commit read (`KnowledgeSearchTools.cs:395-399`); a hit does not (`:104-117`).
- **History and the present read alike.** Each dated note is an entry of its decision's kind
  (`Registry/RepositoryScanner.cs:455-467`). The record already writes an amendment mark, `*Amended by D<n> (ROW, date): …*`,
  54 times in 36 files, each naming a newer decision; nothing reads it. No note says `Superseded by`. Present documents
  have no kind of their own: a README, a design and a proposal are all `Knowledge` (`RepositoryScanner.cs:554-572`).
- **Nothing finds a record the code no longer matches.** The docs gate compares last-commit days
  (`Daoris.Devkit.Core/DocsGate.cs:43-65`): a touch passes it, an unrelated edit fails it.
- **Nothing holds "retire in the same step"** beyond the canon and `post-feature`.

## 2. What a session needs from a hit and a record

From a hit: the commit its text was read at, and whether a later note amends or supersedes it. From a record: one home
per present fact, and a mark written by whoever replaced the fact, when they did.

Lyntai's `docs/DECISIONS.md` binds every choice here: D90 rules out "newest wins" (`:2430`); D109 measured a relevance
judge endorsing the superseded fact 3.4 times as often as the current one (`:3102-3105`); D215 has the writer name what
it replaces, since similarity never establishes contradiction (`:6165`, `:6178-6179`); D217 found a model filed a revised
value under its predecessor's key in only a quarter of questions (`:6203-6204`). **No proposal asks a model which entry
is current or which one a new entry replaces.**

## 3. Proposals

### 3.1 A hit says its standing; the record's existing mark is read

Each hit gains one line: `read at <commit>`, with `+ uncommitted` where one `git status` of the record folders at the
reading found changes (a signature reads disk, `Knowledge/Store/RootSignature.cs:4-10`); `fed at <commit>` where a feed
named one (`KnowledgeService.cs:19-20`); `unknown` only without either. A decision or note adds `later: <n> notes` (a
fact of position) and its marks. `knowledge_get` says the same.

**The form, one home: the older record's note.**
- `*Amended by D<n> (ROW, date): …*`, as written now, in either emphasis, ending in a colon or a period: D<n> amends
  the opening in part.
- `**Superseded by D<n> (ROW, date): …**`, the one form added: D<n> replaces the opening whole.
- Either may add `, replacing "<label>"` to target a note of the same file instead of the opening. A same-decision
  amendment is `**Amended (ROW, date), replacing "<label>": …**`, the shape `docs/decisions/D191.md:36-44` needed.
- The reverse (D<n> amends D<m>) is derived in the reading, from the entries, so a fed reading derives the same marks.
  Nothing is written in the newer decision.
- Older prose stays unmarked: its hit says `no mark`, never current.

`orient-index --check`, in GATESLIM1's `BLOCKING` set (`tools/merge-branch.mjs:1020`), gates the one-sided facts: the
named decision exists, is numbered higher, is not itself, makes no cycle, and a quoted label exists. The 54 marks pass.

**Rejected.** A two-sided form: 42 of the 54 lack a counterpart (Opus read), and a second side is a second home. The
file's last-changed date: 0.44 to 0.96 seconds of `git log` a file (Opus read), moved by a touch. Blame, file times,
ranking by date, a model reading old prose.

### 3.2 A record the code no longer matches is reported

`tools/doc-current.mjs`, a pure audit like `doc-system.mjs`, reads a declared list, `documents.present` in `daoris.json`:
each home's path, the bases its short paths are written after, and its runtime roots. Here: the root README, `AGENTS.md`,
`docs/development.md`, `docs/guide/README.md`, the four component READMEs and `tools/dsh-probes/README.md`. Designs,
contracts and decisions are not on it; their baseline is history (`docs/README.md:7-8`).

**Resolution.** A code span outside fences:
- **Path.** `:line` is stripped and never range-checked. Resolved exactly, relative to the file, then the root, then
  each declared base. It is present if tracked, a tracked folder, or ignored (a folder span tested as a folder). Under a
  runtime root, excluded. Checked only where it starts at a top-level folder or a base; any other path-shaped span (a
  protocol method, a folder named in its sentence's context) is counted unchecked. No basename rescue.
- **Symbol** (two humps, or dotted). Each part must be a whole word of the product code, comments stripped, with tests and
  `docs/index` left out. A name found only in tests counts as test-only. External names sit on an allow-list, each with
  its reason.
- **Route** (`/api/…`): checked against `docs/index/routes.md`. **Variable** (`DAORIS_*`): checked against the code.

It prints findings, unchecked and test-only counts, each finding with its record and line, and exits 0.

**Measured** by a read-only probe of this rule at round 2's base: 9 homes, 6,017 spans.
- **Paths.** 684 path-shaped: 391 present (216 only through a base), 50 runtime, 240 unchecked. None of the 243
  unresolved was true by hand review: files under a project the sentence names, folder names read in context, protocol
  names (`session/new`, `application/json`), another repository's `devtools/dev.mjs`. Anchored, 2 remain, both false:
  `src/test/driverSource.ts` (`docs/development.md:118`, web-relative) and `tools/call` (`src/Daoris.Service/README.md:505`).
- **Symbols.** 1,060 checked, 50 not in product code: 41 test classes or helpers, 3 external (`SendKeys`,
  `Input.dispatchMouseEvent`, the kit's `ChromiumChildBrowser`, cited only in a comment), and **6 true**:
  - `InstanceRoutes`, `PipeGrounds` and `SetupPorts`, each removed (`git log -S`: d89788aa6, 247003ad5, 584837457);
  - `ChannelWords`, `WorkOrdersSetupWiring` and `CourseHelpWiring`, absent from every file.
  
  None is false after the allow-list.
- **Routes and variables.** 25 routes, 1 unresolved (`/api/confirmations/waiting`), unconfirmed because a route built from
  parts is not text; 61 variables, all present.

A missing root-anchored path is a fact, and may gate as router rows do (`tools/doc-system.mjs:65-70`) once a run shows no
false finding. This one shows two, so it reports.

**Rejected.** A model's review; a failing gate now; the date rule as the measure; a README's folder coverage, which would
push READMEs to restate the tree.

### 3.3 Findings reach the writer at the change

`doc-current --since <base>`, where the base is the merge base with `main`, lists findings in the homes the diff touched
and the homes quoting a path or symbol the diff deleted or renamed; `post-feature` points at it. A hit does not carry
the verdict: a node tool's verdict on a service hit is a twin (the cost `DecisionNotes.cs:31-33` carries). "A hit says its record has an open finding" is a later row, if the report earns it.

### 3.4 Retire in the same step

The supersession mark has one home, so nothing is checked both ways; §3.1's gate holds its one-sided facts. Deletions
reach the writer through §3.3, which reports and never fails. **Rejected:** a new review prompt, since `post-feature` is that
prompt and lacked a list.

### 3.5 Restatement within one repository: a later row

Its preconditions:
- the declared present homes read as a kind of their own; a component README is read by default nowhere
  (`RepositoryScanner.cs:195-201`);
- comparison at the sentence, since the detector compares whole-entry token sets (`ConvergenceDetector.cs:159-183`)
  against a 0.82 default (`:45`);
- a measured candidate set.

It shows pairs and never ranks or chooses (D215, D109).

## 4. What it does not cover

Memory outside the repository (`no-global-memory`; parts b and c); the index's vectors are rebuilt from the records, so a
record's mark is the source. Contradiction by meaning. The censuses (CENSUSRULES1). Other repositories, until this holds.

## 5. Rows it would file

- **KNOWFRESH1a-i, the form and its gate** (records tools). **Proof:** fixtures in `orient-index` and the twin table of
  `DecisionNotes`, each read:
  - italic and bold marks, colon and period;
  - `Superseded by`;
  - a same-decision `replacing` note;
  - older prose, which reads unmarked;
  - each one-sided fault, which fails;
  - the 54 existing marks, which pass.
- **KNOWFRESH1a-ii, a hit's standing** (service). **Proof:**
  - a fixture decision's opening hit shows `superseded by`, a note's hit `later: 2 notes`, and an unmarked decision `no mark`;
  - a dirty root says `+ uncommitted`;
  - a fed reading round-trips with its commit and the same marks.
- **KNOWFRESH1a-iii, the report and `documents.present`** (tools, records). **Proof:**
  - negative fixtures: a symbol kept only in a comment is reported, one kept only in a test is test-only, a moved file is
    reported and never rescued, and a shifted `path:line` reads present;
  - this repository's run matches §3.2's counts;
  - the six findings are fixed in the same row.
- **KNOWFRESH1a-iv, restatement** (service), filed blocked on §3.5's preconditions. **Proof:** a sentence restated in two
  homes is one candidate, and a decision is never paired with its own note.

## 6. Questions for the owner

1. **The docs gate.** The recommendation: keep the devkit's date rule for adopters; here, run `--since` beside it for one
   batch, then switch here to a reporting mode (`doc-system.mjs:109-113` requires each component README's declaration, so
   the switch is a mode). Codex's alternative: retire the date comparison everywhere and keep the missing-document check
   (`DocsGate.cs:37-40`).
2. **Older decisions.** Both readers advise against a blanket read of the 202 unmarked files: parse the existing marks
   and mark the rest as found. Agreed?

## 7. Round 1's findings answered

| Finding | Answer |
|---|---|
| C1 | Taken: a mark targets the opening or a quoted note, part or whole (§3.1). |
| C2 | Taken in substance: one home, legacy marks read as written, one-sided gate. |
| C3 | Taken: measured, with samples (§3.2). |
| C4 | Taken, but not declaration-aware parsing: six true findings and no false one need none. |
| C5 | Taken: restatement is a later row (§3.5). |
| C6 | Taken: fed commit shown; marks derived from entries. |
| C7 | Taken: to the writer at the change; the hit later (§3.3). |
| C8 | Moot: no git call at answer time. |
| O-F1 | Taken: only `Superseded by` is added. |
| O-F2 | Taken: reverse derived; gate in `orient-index --check`. |
| O-F3 | Taken: declared homes, designs out, measured. |
| O-F4 | Taken (§3.2). |
| O-F5 | Taken: presence only. |
| O-F6 | Taken: cut. |
| O-F7 | Taken. |
| O-F8 | Taken: `documents.present`. |
| O-F9 | Taken: cut; the switch is a mode (§6). |
| O-F10 | Taken: 0.82, the base, gating once clean, sources by path. |
