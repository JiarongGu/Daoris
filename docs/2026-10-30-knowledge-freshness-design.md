# Knowledge freshness: what a hit says of its standing, and how a stale record is found

**Standing:** proposed, round 1 (KNOWFRESH1a under D191). **Governing:** D220 and the canon's `current-knowledge`.
Parts b (Lyntai's memory, quest `#d9c80453e46f`) and c (the machine's memory server) are not this design's.

## 1. The problem as the code shows it

- **The index is current; a hit's standing is not said.** An entry's id is its location
  (`Knowledge/KnowledgeEntry.cs:165-172`), so an edited section replaces its own entry, and a moved checkout is read
  again (`Knowledge/Store/CheckoutFreshness.cs:3-42`, `Knowledge/KnowledgeService.cs:1137-1177`). The commit each
  checkout was read at is known (`KnowledgeService.cs:944-1006`) and told only by `knowledge_refresh`
  (`KnowledgeSearchTools.cs:395-399`). A hit says its title, kind, path, lines and excerpt (`KnowledgeSearchTools.cs:104-117`):
  no date, no commit, nothing of what came after it.
- **History and the present are read alike.** Each dated note of a decision is an entry of its own
  (`Knowledge/Ingest/DecisionNotes.cs:12-29`), of the same kind as the decision it amends
  (`Registry/RepositoryScanner.cs:455-467`). The note parser already reads
  a `Superseded` label (`DecisionNotes.cs:127-130`), but nothing reads what it supersedes. 17 of 219 decision files
  say "supersede" at all, and most of those name a product state (`superseded` as a session's code), not a record replaced.
- **Nothing finds a record the code no longer matches.** The docs gate compares five READMEs' last-commit days with
  their areas' (`daoris.gates.json:61-66`, `Daoris.Devkit.Core/DocsGate.cs:43-65`): any commit to the README passes it,
  and an unrelated code edit fails it. `tools/doc-system.mjs:2` leaves semantic claims to a review; `tools/doc-links.mjs:5-6`
  resolves links, not the names a record quotes in code spans.
- **Nothing finds two entries of one repository that disagree**: `knowledge_convergence` groups entries of different
  repositories by similarity (`Knowledge/Search/ConvergenceDetector.cs:19-29`), never one repository's, and never contradiction.
- **Nothing holds "retire in the same step"** beyond the canon's words and `post-feature`'s audit.

## 2. What a session needs from a hit and a record

From a hit, before it reads further: which commit the text was read at; when the text was written, where the record
says; whether something later in its own record amends or supersedes it; and whether the names it quotes still exist.
Each is a fact read from the files or from git, never a judgement of which text is true; an entry with no mark is
said to have none, never to be current.

From a record: one home per present fact (D220), and history marked as history, so a mark is written by the person or
agent who replaced the fact, at the moment they knew it. Lyntai's evidence binds every choice below: its D90 forbids
"newest wins", its D109 found a relevance judge endorsing the superseded fact 3.4 times as often as the current one, and
its D217 found a model-chosen key matched its predecessor a quarter of the time. So **no proposal here asks a model which
of two entries is current, or which entry a new one replaces**; the writer names it, and the tools read the name.

## 3. Proposals

### 3.1 A hit says its standing

Each hit gains one line: `read at <commit>` (the reading's, already held per repository); `written <date>` where the
record writes one (a note's label, an archive or fix-log entry's heading); `file last changed <date> <commit>`, asked of
git once per distinct file among the hits shown and cached per reading; and, for a decision or one of its notes,
`later: <n> notes, last <label>` and `superseded by D<n>` or `amended by D<n>` where a marked note says so. `knowledge_get`
says the same. A fed host, with no checkout, says `no commit` rather than leaving the line out.

**The form a decision's note uses.** The older decision gains a note at its end (D134's place for a note on an older
decision): `**Superseded by D<n> (<ROW>, <date>).**` or `**Amended by D<n> (<ROW>, <date>).**`, its sentence saying
which part. The newer decision's opening names it back: `(supersedes D<m>)` or `(amends D<m>)`. The mark is per
decision; the note's text says the part, for the reader. An older note, written before the form, reads as **unmarked**:
its hit gives its date and the notes after it, which are facts of position, and no standing.

**Rejected.** A section's last change by `git blame` on every entry at every reading: thousands of entries against a
read of one to two seconds (`CheckoutFreshness.cs:37-42`), for a precision the file's commit and the record's own date
already give. File modification times: a checkout resets them (`DocsGate.cs:7-10`). Ranking a newer entry above an older
one: "newest wins" by another name. Reading prose such as "this replaces …" with a model to mark the 17 older files:
D109's failure, at write time instead of read time; the few real ones are marked by hand.

### 3.2 A record the code no longer matches is reported

A new report, `tools/doc-current.mjs` (a pure audit like `doc-system.mjs`, import-safe, no writes), reads the
present-fact homes (component READMEs, rooms, the router, this repository's own `.claude/knowledge`, `docs/development.md`,
and designs whose standing is accepted or a contract) and resolves what they quote in code spans: a repository path
against the tracked files; a `path:line` against that file's length; a symbol (a two-hump or dotted identifier) by a
whole-word search of the tracked code; a route, a verb or a catalogue key against `docs/index/` (`routes.md`, `verbs.md`,
`catalogues.md`); a `DAORIS_*` variable against the code. For each README `daoris.gates.json` tracks, it lists the
folders of its area the README names nowhere. It prints each finding with its record and line, and exits 0: whether a
quoted word is a claim about the code is a judgement, so it reports, as every ceiling does
(`canon/core/knowledge/development-documents.md:131-134`).

History (decisions, the archive, the fix log, the changelog) is not read: a record of what was true names what has since
gone (`doc-links.mjs:23-25`). The report stays this repository's until a second repository asks, for
`doc-shapes.mjs:21-24`'s reason: the shapes it reads are this repository's conventions.

**Rejected.** A model reading a record against the code: the review `doc-system.mjs:2` names, not a check. A failing
gate: a quoted word that is not a symbol would stop a merge. Keeping the date comparison as the measure: it is the
census shape D220 names, passed by a touch and failed by an unrelated edit.

### 3.3 Two entries of one repository that may disagree: restatement, not contradiction

`knowledge_convergence` gains a scope within one repository, over the present-fact kinds only, by its two lexical
methods (`ConvergenceDetector.cs:9-13`). It reports two present homes stating one thing, which D220 calls the defect
whichever is right; it does not say which is current, rank them, or read history against the present. Its semantic
method runs only where a model is named, and says so (D24's tier).

Lyntai's evidence does not reach this, because nothing here chooses: a pair is shown, and the person or agent who
holds the fact picks its one home and removes the copy. Contradiction by meaning is not attempted yet: telling two
statements that disagree from two that differ in scope is the judgement D109 measured failing.

**Rejected.** A second detector in a node tool: a twin needing a twin table, the cost `DecisionNotes.cs:31-33` carries.
A gate: the similarity threshold has no right default (`ConvergenceDetector.cs:32-35`).

### 3.4 Retire in the same step: two checks at the change, no new prompt

- **The supersession form is checked both ways**, by `tools/doc-duplicates.mjs`, which already reads each decision's
  notes (`doc-duplicates.mjs:17-26`): a newer decision saying `supersedes D<m>` with no `Superseded by` note in D<m>,
  or the reverse, fails. It is a fact about the files, and a fact gates (`doc-duplicates.mjs:28-31`). Prose without the
  form is not read.
- **A change's deletions name the records they made stale.** `doc-current.mjs --since <base>` takes the paths and the
  symbols the diff deleted or renamed and lists every present-fact home quoting one. It reports at verify and in the
  merge's quick checks; `post-feature`, which already asks for the records a change made stale, is pointed at its output.

**Rejected.** A new review prompt: `post-feature` is that prompt, and it lacked a list, not a reminder. Failing on a
deletion's report: a quoted name in a sentence that still holds would stop a merge.

## 4. What it does not cover

An agent's memory outside the repository (the harness's per-project folder, the machine's memory server): project
facts do not belong there (`no-global-memory`), and parts b and c hold the retirement of a memory entry. Daoris's half
meets part b only through the store's vectors, which are rebuilt from the records at each reading, so a record's mark
is the source and Lyntai's retirement need not reach the index. Contradiction by meaning. The censuses (CENSUSRULES1).
Other repositories: the form and the report enter the canon only after they hold here.

## 5. Rows it would file

- **KNOWFRESH1a-i, the form and its check** (records tools, decisions). D134's forms gain `Superseded by` and `Amended
  by`, read by `DecisionNotes` and the digest alike through the twin table; `doc-duplicates` checks both sides; the real
  supersessions among the 17 are marked by hand. Proof: twin-table rows (a one-sided mark fails, a pair passes, older
  prose "superseded" reads unmarked) pass in both twins; the digest shows the marked decisions.
- **KNOWFRESH1a-ii, a hit's standing** (service). Proof: a fixture decision with an opening, two notes and a
  `Superseded by` note: a hit on the opening shows the mark, a hit on the first note `later: 2 notes`, an unmarked
  decision no mark and never "current", a fed entry `no commit`; `knowledge_get` says the same.
- **KNOWFRESH1a-iii, the record report** (tools). Proof: on a fixture tree, a README quoting a deleted file, a missing
  symbol and an unknown route, and an area with an unnamed folder, is reported with exit 0; `--since` lists the records
  quoting a file the diff deleted; the count of findings on this repository at landing is in the commit.
- **KNOWFRESH1a-iv, restatement within one repository** (service). Proof: two fixture READMEs restating a sentence are
  one candidate; a decision and its own note are never paired; the answer orders no pair by date.

## 6. Questions for the owner

1. **The docs gate's date rule.** It ships in the devkit and fails in every repository that tracks a README. Replace
   it with the record report (3.2), which reports and never fails, there too, or keep it beside the report here only?
   Yes to replacing retires a failing gate in adopting repositories.
2. **Marking the older decisions.** 1a-i marks only the hand-checked supersessions among the 17 files that say the
   word. Should a session also read the other 202 for supersessions written in other words, or is an older decision
   left unmarked until someone finds it replaced?
