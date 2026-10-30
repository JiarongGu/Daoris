# Knowledge freshness: what a hit says of its standing, and how a stale record is found

**Standing:** proposed, round 3 (KNOWFRESH1a under D191), after two rounds of Codex's and the Opus reader's reads (§7).
**Governing:** D220 and the canon's `current-knowledge`. Parts b (Lyntai's memory, quest `#d9c80453e46f`) and c (the
machine's memory server) are not this design's.

## 1. The problem as the code shows it

- **A hit is silent about its standing.** Ids are locations (`Knowledge/KnowledgeEntry.cs:165-172`) and a moved
  checkout is read again (`Knowledge/Store/CheckoutFreshness.cs:3-42`), but only `knowledge_refresh` tells the commit
  (`KnowledgeSearchTools.cs:395-399`); a hit does not (`:104-117`).
- **History and the present read alike.** Each note is an entry of its decision's kind (`Registry/RepositoryScanner.cs:455-467`).
  54 notes in 36 files open `Amended by D<n>`, each naming a higher decision; nothing reads them. None says `Superseded by`.
  A README, a design and a proposal are all `Knowledge` (`RepositoryScanner.cs:554-572`).
- **Nothing finds a record the code no longer matches.** The docs gate compares last-commit days
  (`Daoris.Devkit.Core/DocsGate.cs:43-65`): a touch passes it, an unrelated edit fails it.

## 2. What a session needs

From a hit: the commit its text was read at, and whether a later note amends or supersedes it. From a record: one home
per present fact, and a mark written by whoever replaced it, when they did.

Lyntai's `docs/DECISIONS.md` binds this: D90 rules out "newest wins" (`:2430`); D109 measured a relevance judge endorsing
the superseded fact 3.4 times as often as the current one (`:3102-3105`); D215 has the writer name what it replaces, since
similarity never establishes contradiction (`:6165`, `:6178-6179`); D217 found a model filed a revised value under its
predecessor's key in only a quarter of questions (`:6203-6204`). **No proposal asks a model which entry is current or
which one a new entry replaces.**

## 3. Proposals

### 3.1 A hit says its standing; the record's mark is read

Each hit gains one line: `read at <commit>`, plus `+ uncommitted` where the reading's one `git --no-optional-locks status`
of the record folders found changes (a signature reads disk, `Knowledge/Store/RootSignature.cs:4-10`); `fed at <commit>`
where a feed named one (`KnowledgeService.cs:19-20`); `unknown` without either. A decision or note adds `later: <n> notes`
and its marks. `knowledge_get` says the same.

**The mark, one home: a note of the older record whose label opens `Amended by D<n>` or `Superseded by D<n>`.** Whatever
follows in the label is free, so all 54 read as written, among them `D107.md:123` (`D158's … note`), `D117.md:131`
(`, §6.5:`) and `D84.md:41` (a date, no row). `Amended` is in part, `Superseded` whole. Either may add `, replacing
"<label>"` to target an earlier note of the same file instead of the opening. A same-decision amendment is
`**Amended (ROW, date), replacing "<label>": …**`, the shape `docs/decisions/D191.md:36-44` needed. The reverse is derived
in the reading from the entries, so a fed reading derives the same. A parenthetical inside a paragraph (`D33.md:53`) is
no note and reads unmarked until marked; no unmarked entry reads as current.

`orient-index --check`, in GATESLIM1's `BLOCKING` set (`tools/merge-branch.mjs:1020`), gates the one-sided facts: the
named decision exists and is numbered higher, and a quoted label names an earlier note. A higher number rules out a cycle.

**Rejected.** A two-sided form: 42 of the 54 lack a counterpart (Opus read), and a second side is a second home. The file's
last-changed date: 0.44 to 0.96 seconds of `git log` a file (Opus read), moved by a touch. Blame, ranking by date, a model
reading old prose.

### 3.2 A record the code no longer matches is reported

`tools/doc-current.mjs`, a pure audit like `doc-system.mjs`, reads `documents.present` in `daoris.json`: each home's path,
the bases its short paths follow, its runtime roots. Here: the root README, `AGENTS.md`, the router, `docs/development.md`,
`docs/guide/README.md`, the four component READMEs, `tools/dsh-probes/README.md` and the 17 knowledge files this repository
owns (the rest are the canon's). Designs, contracts and decisions are history (`docs/README.md:7-8`).

**The rule**, over each code span outside a fence:
- **Path.** `:line` stripped and never range-checked; resolved exactly, against the file's folder, the root, then each base;
  present if tracked, a tracked folder, or ignored (a folder span tested as one). A placeholder, a bare file name or a span
  under a runtime root is unchecked, and so is a path whose first part is no top-level entry. No basename rescue.
- **Symbol** (PascalCase, camelCase, or dotted). A span that is the stem of a tracked source file is a file reference,
  present by that file. Otherwise each part, or its PascalCase form, must be an identifier of product source: `.cs`, `.ts`,
  `.tsx`, `.mjs`, `.js`, `.ps1`, comments stripped, data such as JSON fixtures left out. One found only in test source is
  test-only, printed by name. External names sit on an allow-list, each with its reason.
- **Route** against `docs/index/routes.md`; **variable** (`DAORIS_*`) against product source.

It prints each finding with its record and line and exits 0.

**Measured** by a read-only probe of this rule at `c15dc6838`, 27 homes, 10,290 spans:
- **Paths:** 2,065. Of these, 42 are placeholders, 995 bare names, 539 present (216 through a base), 63 runtime and 421
  unanchored. That leaves 5 findings, all false: `src/locales` twice and `src/test/driverSource.ts` are web-relative (a base
  for those homes), `tools/call` is a protocol method, and `.claude/rules/` in `canon-authoring.md:74` names a former
  layout as a reason.
- **Symbols:** 2,603. Of these, 996 are file references, 1,532 product and 49 test-only. That leaves 26 findings:
  - 7 stale claims, all in `twins.md`: `ResolveScope`, `deviceLogin`, `commitModeSays`, `versionDigest`, `stepName`,
    `resourceSaid`, `changesBetween`;
  - 13 history sentences in present homes, names said to have "left with" a row, or `ProofRefsAsync` as "round 3's",
    which D220 cuts;
  - 5 external names for the allow-list;
  - 1 false: `keptTrees`, a design's word in its router row.
- **Routes:** 29, with 1 false finding. `/api/confirmations/waiting` exists (`PersonConfirmations.cs:174`), so this is an
  index-extraction limit.
- **Variables:** 72, with 1 finding, `DAORIS_PERSON_KEY`, written as owed work.

Root paths do not gate yet. Five false path findings say so; gating waits on a run with none, as router rows gate
(`tools/doc-system.mjs:65-70`).

**Rejected.** A model's review; a failing gate now; the date rule as the measure; folder coverage. A retired-term registry,
Lyntai's D42 (`retiredTerms`, `docs/DECISIONS.md:1000-1005`), is the same idea from the vocabulary side. Removed identifiers
come from the code here, so it waits for a retired word that is no identifier: a later row at most.

### 3.3 Findings reach the writer at the change

`doc-current --since <base>`, where the base is the merge base with `main`, lists findings in the homes the diff touched
and in those quoting what it deleted or renamed. `post-feature` points at it. A hit does not carry the verdict, since a node
tool's verdict on a service hit is a twin (`DecisionNotes.cs:31-33`). A hit naming an open finding is a later row, if earned.

### 3.4 Retire in the same step

The mark has one home, and §3.1's gate holds its one-sided facts. Deletions reach the writer through §3.3, which reports
and never fails. No new prompt: `post-feature` is one, and it lacked a list.

### 3.5 Restatement within one repository: a later row

Its preconditions:
- the declared homes are read as a kind of their own, since no component README is read by default
  (`RepositoryScanner.cs:195-201`);
- comparison is by sentence, since the detector compares whole-entry token sets (`ConvergenceDetector.cs:159-183`) at 0.82
  (`:45`);
- a measured candidate set.

It shows pairs; it never ranks or chooses.

## 4. What it does not cover

Memory outside the repository (`no-global-memory`; parts b and c); the index's vectors are rebuilt from the records, so a
record's mark is the source. Contradiction by meaning. The censuses (CENSUSRULES1). Other repositories, until this holds.

## 5. Rows it would file

- **KNOWFRESH1a-i, the mark and its gate** (records tools). **Proof:** fixtures in `orient-index` and `DecisionNotes`' twin
  table carry the actual text of `D107.md:123`, `D117.md:131`, `D84.md:41` and an italic mark. Each reads as a mark, as do
  `Superseded by` and a `replacing` note. `D33.md:53` reads unmarked. An unknown or lower decision fails, as does a
  `replacing` label naming no earlier note. The 54 marks pass.
- **KNOWFRESH1a-ii, a hit's standing** (service). **Proof:** a fixture decision's opening shows `superseded by`, its note
  `later: 2 notes`, an unmarked decision `no mark`; a dirty root says `+ uncommitted`; a fed reading round-trips its commit
  and marks.
- **KNOWFRESH1a-iii, the report and `documents.present`** (tools, records). **Proof:** negative fixtures, each reported as
  it should be: a symbol kept only in a comment or in JSON data, one only in test source (printed), a moved file never
  rescued, a shifted `path:line` present, a file's stem present. The landing run on this repository is reviewed by hand,
  and its true findings are fixed in the row.
- **KNOWFRESH1a-iv, restatement** (service), blocked on §3.5's preconditions.

## 6. Settled by the steward (D191 point 3)

1. **The docs gate.** The devkit's date rule stays for adopters. Here, `doc-current --since` runs beside it for one batch,
   then this repository switches to a reporting mode (`doc-system.mjs:109-113` requires the declaration, so the switch is
   a mode). Rejected for now: Codex's retiring of the date comparison everywhere, which would leave adopters only the
   missing-document check (`DocsGate.cs:37-40`) and no staleness check.
2. **Older decisions.** The 54 marks are parsed. The other amendments, written in prose or inline (the Opus read counted
   105 references in 66 files, 54 of them marks), are marked as found. There is no blanket read.

## 7. Findings answered

Round 1: C1, C3, C5 to C7 and O-F1 to O-F10 taken; C2 and C4 taken in substance, as round 2 below completes; C8 moot.
Round 2's text (commit `bda7dfafe`) holds that table line by line.

| Round 2 | Answer |
|---|---|
| O2-1 | Taken: JSON data left out, test-only is test source and printed, the pinned-count proof dropped; `movesMissing` resolves as `MovesMissing` (`Trace.Words.cs:212`). |
| O2-2 | Taken: the router and the 17 knowledge files added, measured. |
| O2-3 | Taken: a label opening the phrase; cycle check cut. |
| O2-4 | Taken: `--no-optional-locks`. |
| C2-1 | Taken: the forms in fixtures; `D33.md:53` classified. |
| C2-2 | Taken: file stems are file references; round 2's six were all files, withdrawn. |
| C2-3 | Taken: counts reconciled, base and rule stated. |
| C2-4 | Taken: an index-extraction limit. |
