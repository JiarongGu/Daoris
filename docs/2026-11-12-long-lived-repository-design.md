# A long-lived repository develops as well as a fresh start: what a change reads, measured (DOCDIET1)

**Accepted 2026-11-12, settled as D278.** The direction is the backlog row DOCDIET1 (`TASKS.md`): development on a repository
that has lived for months performs worse than on a fresh start, and every repository Daoris develops will age the same way. The
aim is a change's cost, not fewer files, and nothing about history was settled first: kept whole, moved to memory, summarised or
let go were all candidates, compared by measurement. Written by Claude Opus 5.5 on 2026-11-12 from main at `b738f7359b`. Read with
`docs/areas/knowledge-memory-records.md` (*records rule n*), `docs/areas/doctrine-and-cli.md` (*CLI rule n*), the canon's
`development-documents` knowledge, D54, D129, D220 and D228.

## 1. The question

A fresh copy of this repository, holding only what is true now (the brief, the area documents, the open backlog, the generated
index and the code), is the baseline. What does the same change cost here instead, and which of the candidates brings it back?
A change's cost is what it reads before its first edit, what its searches return, and the wrong turns between.

## 2. What was measured

Four instruments, each on this repository at `b738f7359b`. The scripts are scratch (`local/scratch/docdiet/`); §8 names the one
that becomes a tool.

- **M1, what builders read.** The harness's transcripts of this repository's dispatched builders since 2026-11-01: 849 runs, 753
  of which edited a tracked file. Each tool result's bytes, by what it read (Read, Grep, a shell command naming a record), before
  the first edit of a tracked file and over the run.
- **M2, what a present search returns.** Twelve questions whose answer is a rule in an area document ("what does the comment gate
  refuse", "what does `daoris check` fail on"), ranked by the question bench's own floor (`tools/question-bench.mjs`'s
  `indexPassages` and `floorHits`: Okapi over every line with its neighbours, three hits) over the fresh copy (281 files, 2.0 MB)
  and over this repository's documents as they are (886 files, 31 MB), and over the latter less its designs, less its records, and
  less both.
- **M3, recall of history, both ways.** Thirty-six questions written from the builders' real lookups in M1 into the archive, the
  fix log and the decisions (when a task closed and how; a fix's root cause; why a rule was chosen), fourteen by identifier as the
  builders asked them and twenty-two in words only. *Files*: the bench's floor for words, and for an identifier what the builders
  did, an exact search (a decision opened by its number). Over two layouts: the records as they are, and the archive and fix log
  split by month. *Memory*: the 4,615 entries (2,420 archive, 780 fix-log, 1,415 decision headings and notes) remembered one each
  into a `memory.db` through Daoris's own `LyntaiMemory` and asked through its `SearchAsync`, three hits, as `memory_search` asks:
  reinforcement off, words only, no embedder.
- **M4, the bytes at a change's start.** The brief, its area document, its backlog row and the index's `README.md`.

## 3. Results

**M1.** Before their first edit the 753 builders read, in total: code 76 MB, shell output 59 MB, **designs 34 MB**, **area
documents 9.4 MB**, decisions 3.9 MB, the backlog 2.4 MB, the index 1.0 MB, the archive 0.68 MB, the fix log 0.14 MB. Per builder
that is 45 KB of designs, 12.5 KB of area, 5.2 KB of decisions, 3.1 KB of backlog, 0.9 KB of archive and 0.2 KB of fix log; the
median builder read 218 KB in all before its first edit. They looked history up by identifier (`grep -n "WORKFLOWCLEAN1"` over
the archive, `D228.md` by name): 1,401 lookups, median result 1.0 KB (archive), 1.1 KB (fix log), 1.5 KB (decisions). The
knowledge server's search was asked four times. Of the design bytes, 81% went to designs an open row cites and 18.5% (6.3 MB) to
designs that neither an open row nor an area document cites: 251 of the 313 designs at `docs/`'s top, 11.4 MB.

**M2.** The right area document as the first hit, and within three, with the share of all hits that were history:

| Corpus | Files | Top hit | Within three | Hits from history |
|---|---|---|---|---|
| The fresh copy | 281 | 10/12 | 11/12 | 0/36 |
| This repository as it is | 886 | 1/12 | 6/12 | 29/36 |
| Less its designs | 573 | 2/12 | 8/12 | 24/36 |
| Less its records (decisions, archive, fix log, changelog) | 612 | 4/12 | 7/12 | 26/36 |
| Less both | 299 | 8/12 | 10/12 | 14/36 |

History buries the present rule, and no one kind of it does alone: the designs and the records together do.

**M3.** History's recall:

| | By identifier (14) | By words (22), top / within three | Returned per answer |
|---|---|---|---|
| Files, as they are | 11/14 at the first match | 17/22 / 19/22 | median 2.0 KB for an id search; three line hits and the entry (1.1–1.9 KB) for words |
| Files, by month | 11/14 | 17/22 / 19/22 | the same; a month file is 380 KB median, 2.2 MB the largest |
| Lyntai's memory | 4/14 top, 5/14 within three | 4/22 / 16/22 | median 9.6 KB (three whole entries) |

The month split changes no hit: the floor and an identifier search read lines, and a month here is 600 archive entries. Memory
found the right entry within three for most word questions but ranked it first for four of twenty-two, and lost identifiers
(`AGENTSPLACE1h`, `FOUNDATION1d-i`, `QUEUELOCK1`). A recall asked 125 ms median; loading took 44 s for a 52 MB store.

**M4.** The brief (`AGENTS.md`, 12.2 KB), the area document (48.8 KB median, 101 KB the largest), the row (0.4 KB median, 8.0 KB
the largest) and the index's `README.md` (20.0 KB): **81 KB at the median area, 142 KB at the largest.** The canon's day-one shape is
a brief of 1,500 words, an area of 600 and a row of 60.

## 4. The candidates

1. **A present search** (adopted). A question about how something works now is searched in what is true now (the brief, the area
   documents, the index, the code, the knowledge and rooms); history is looked up by its identifier, or searched when the question
   is about history. M2: the top hit goes from 1/12 to 8/12 once both kinds of history are out of the words search, against a
   fresh copy's 10/12. It moves no file, so no citation breaks.
2. **Finished designs filed** (adopted). A design that no open row and no area document cites has done its work: it moves to the
   archive folder the router already keeps (`docs/archive/`, its own router), its row goes from `docs/README.md` to that router,
   and every citation of it is rewritten in the same commit. M1: 18.5% of the design bytes builders read before their first edit.
3. **Each area at its day-one size** (adopted). An area document says the rules true now, with history in its decision and a long
   table in the index; `daoris check` reports an area past its declared size. M1, M4: the area is the largest read a change makes
   of the records, 49 KB at the median.
4. **The backlog as open rows** (adopted, the rule it already has). The same report holds it.
5. **The archive and fix log split by month** (rejected). M3: no hit changes and no returned byte; the files stay above a read's
   limit at this repository's pace, and the move rewrites every citation of them and the writers' paths.
6. **History moved into Lyntai's memory** (rejected now). M3: the top hit for 8 of 36 against the files' 28 (11 + 17), identifiers
   lost, four times the bytes per answer. What it lacks is asked of Lyntai (§7) and the bench is run again when it answers.
7. **History summarised or let go** (rejected). M1: builders read under 1 KB of archive and 0.2 KB of fix log each, by identifier;
   there is no cost to recover, and an identifier lookup is the files' strongest answer (11 of 14).
8. **A size that fails the check** (rejected, D54). A size is a judgement; the check says the read path grew and where.

## 5. The canon

- **`development-documents`**: *what a change reads stays the size it was*: the read path (the brief, the area, the row, the index)
  has a size, reported when it grows; the records read by lookup have none; a present question is searched in the present and
  history is looked up by identifier; a design leaves the read path for the archive folder once nothing open cites it.
- **`change`**, step 1: search what is true now before the records, and look history up by its identifier.
- **`daoris check`**: a ceiling declared on a folder of documents (the area documents) measures each file in it, so each area is
  reported against its size, as the brief and the backlog are. The line says the read path has grown; it never fails.

## 6. The migration of an adopted repository

In this order, each step keeping `daoris check`, the document tools and the index green:

1. Declare the read path's sizes in `daoris.json` (here `areas` at 4,500 words, about 30 KB). The check then lists every area over.
2. File the finished designs: a design no open row and no area cites, moved with its citations rewritten in the commit that moves
   it, and its router row moved to the archive router. One commit of whole-file moves, apart from any edit, so merges stay clean.
3. Cut each area to its present rules: history to its decision, a rule's long table to the index. One area per change.
4. Cut the backlog to open rows of a line or two.
5. The two examples next, by the same steps; their records are a few files, so steps 2 to 4 are small.

## 7. What Lyntai's memory lacks, as a request

Daoris keeps a repository's history in files because of the measurement in §3 M3; it would move it to memory when the bench run
again shows memory at least as good. Asked of Lyntai, with the bench's numbers:

- **A. Identifiers as terms.** A token of letters and digits with a suffix (`AGENTSPLACE1h`, `FOUNDATION1d-i`, `D228`) matched
  exactly and ranked above common words. Measured: 4 of 14 identifier questions at the top, against 11 for an exact search.
- **B. The ranking at the top.** For word questions the right entry was within three for 16 of 22 but first for 4, against 17
  first for line-level Okapi over the same entries; a lexical score, with the entry's first line weighted, and the score returned.
- **C. The matching span.** A recall that can return the lines that matched with the entry's reference, not the whole entry:
  9.6 KB median per three hits at full detail.
- **D. Long entries.** Entries up to 145 KB were stored and returned whole; a stated limit, or an entry kept in parts under one
  reference.

## 8. What is left

- **The change-cost run.** The same change done by a real session on this repository and on its fresh copy, compared in tokens,
  wall time, bytes before the first edit and wrong turns, with the knowledge bench's harness (`tools/knowledge-bench.mjs`) over two
  fixtures. M1 to M4 are its proxies; this run is the measurement the owner named.
- **The history bench as a tool.** M2 and M3's scripts become a bench beside the question bench, its cases from real lookups, run
  again at each Lyntai answer and after each step of §6.
- **A present scope in the knowledge server's search.** Its search answers present and history together today; a present scope
  first, history on asking.
