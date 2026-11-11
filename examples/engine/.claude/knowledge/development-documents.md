---
name: development-documents
applies_when: setting a repository up for agents, adding, moving or splitting one of its documents, writing an entry into one, or when a session had to search for where something is written
enforces: a short brief every agent reads, an area document per part of the code read at the start of every change there, and records only where a later session reads them; a document read whole has a ceiling; what may run unasked is a declaration a tool reads, never a sentence
---
<!-- daoris: core/core/knowledge/development-documents.md @ 0.0.1 — canonical; edit via `daoris upstream` -->

# The development documents — what a repository keeps so a session works unasked

**A session starts with what it is handed and what it can find. Keep a short brief every agent reads,
an area document for each part of the code, and as few records as a later session reads: a short
backlog, one archive line per finished task, decisions only for real choices, and a fix record for
regressions only.**

## Why

Each failure this prevents is silent.

- **A brief grown past a byte limit.** At least one widely used agent reads the root instruction file
  only up to a fixed size, 32,768 bytes when this was written, and drops the rest without a word. The
  shared doctrine sits at the end of that file, so a brief that grows a paragraph at a time pushes the
  rules out of that agent's context while every other agent still sees them.
- **The search before the work.** A session that does not know where the part it changes is described
  searches, and a search names a file, not the line in it, so the read that follows is the whole file.
  Measured in one repository, a third of each session's calls came before its first edit. An area
  document that names the lines is read once, at the start of the change.
- **A settled question reopened.** A choice recorded without what it rejected is re-derived by the next
  session, which reaches the rejected alternative, finds it reasonable, and builds it.
- **A command allowed in prose and refused by the harness.** An instruction file shapes what an agent
  tries, not what its harness permits: "you may run the tests", written in a brief, is followed by a
  refusal and a stall.
- **A record with no reader, paid for on every step.** A session carries what it read into every step
  after. A fix log of every defect, an archive entry that retells its row, a decision for a choice
  nobody weighed: each is written once and paid for by every later reader, who learns nothing from it.
- **History found before the present.** A repository's records and finished designs only grow, so after
  months they are most of its text, and a search for how something works now returns them first.
  Measured in one repository, a fresh copy holding only its present state found the right rule first
  for ten of twelve questions, and the repository itself for one. What a change reads, and what its
  search returns, must cost in the second year what they cost on the first day.

## How to apply

The `set-up-documents` skill carries the procedure and a template for each shape below; the `change`
skill is how a session uses them.

### The roles

A **role** is a document's job. One document per role; the names and paths are the repository's own.

| Role | Its one job | Read |
|---|---|---|
| **brief** | what this is, the constraint every change serves, the non-negotiables, the layout's non-obvious half, the quick checks and the command that means done, the area documents, and where everything else is | always |
| **room** | one folder's own instructions, where an agent works inside that folder | on demand, when working there |
| **knowledge** | a deep dive one area needs, saying when it applies and what it enforces | on demand, from the index |
| **skill** | a procedure, invoked by name | its description always, its body on use |
| **router** | optional: each document, its kind and its standing | whole, at a task's start |
| **index** | optional: where things are in the code, generated and checked, never edited by hand | by lookup |
| **areas** | one part of the code: what is true of it now, the lines to start at, the exemplar a new unit mirrors, the folder new behaviour goes in, its tests and its traps | on demand, at the start of every change there |
| **decisions** | a choice that rejected an alternative: what, why, and what it rejected | by lookup |
| **backlog** | open work only, a row of a line or two | whole, when picking work |
| **archive** | one line per finished task: its date, its outcome, and where the detail lives | by lookup |
| **fixes** | regressions only: the root cause, and the commit that introduced it | by lookup |
| **changelog** | what a user of a release sees changed | by lookup |
| **glossary** | optional: the names people and code use for this repository's things | by lookup |
| **gates** | the checks, their exact commands, which are quick, and the work a session may do unasked | by tools |

The brief and the area documents are what every repository keeps. A router, a generated index and a
glossary are kept only where sessions use them; the area documents carry the lines otherwise.

### Three ways a document is read

- **Always**: the brief, beside the always-loaded doctrine, in the one root instruction file every agent
  reads. A nested file is not loaded by all of them.
- **On demand**: the area documents, rooms, knowledge and skill bodies. Something always read names each
  one, or names the list of them, because telling is how an on-demand document reaches an agent.
- **By lookup**: decisions, archive, fixes, changelog. Read for one entry and never whole, so they may
  grow without limit.

### The area documents

- **One folder of them**, `docs/areas/` unless the repository already keeps such a folder, one document
  per part a change is made in, named for the part. The brief lists them in a table, a row each.
- **What is true now, said once.** A change that alters behaviour someone relies on rewrites the
  sentence in place, with no history and no status words. What changed and when is the commit's; why
  one way was chosen over another is the decision's.
- **Lines, not files.** Each names the file and line range to start reading at, the exemplar a new unit
  mirrors, the folder new behaviour goes in, and the filtered command that runs its tests.
- **Short.** It is read whole at the start of every change in its part, so it has a ceiling and points
  at what is longer: a design, a knowledge document, a decision.

### Entries that point

- **A backlog row**: an identifier, what is needed and why, and its proof, in a line or two, naming its
  area document. Never a design's text, a log of sightings or a history.
- **An archive line**: the identifier, the date it finished, and the outcome in a line, naming where the
  detail lives: the commit, the decision, the area document.
- **A decision**: only for a choice that rejected an alternative, with what it rejected. Its amendment
  goes under it, headed by the work that made it.
- **A fix entry**: only for a regression, naming the mechanism and the commit that introduced it. Any
  other defect's root cause is its commit's body.
- **Look up by identifier.** A record is searched for the identifier a row or a decision names and read
  at that entry. Reading a record whole to find one entry pays for every other entry in it.
- **Search the present for a present question.** How something works now is searched in the brief, the
  area documents, the index, the knowledge and the code; the records are searched only when the question
  is about history. History stays whole in its files: found by identifier, it costs a line.
- **A finished design leaves the read path.** A design that no open row and no area document names has
  done its work: it moves to the folder the router keeps for history, and every citation of it is
  rewritten in the change that moves it.

### What goes in the brief

**A fact goes in the brief only if nearly every task needs it and a reader could not derive it from the
code.** The exact command that means done and the quick checks are in; a tour of the folders is out. Of
the layout, only what the tree does not say: which folders are generated, which are copies, which have
their own instructions, what must never be edited by hand. A brief never holds status, history, counts,
a quotation of a person, or a permission, which belongs to the declaration.

### Ceilings

What a change reads (the brief, its area document, its backlog row, the index) has a ceiling, so it stays
the size it was on the first day; a record read by lookup has none.

- **Bytes, for the root instruction file as a whole**: the brief and the doctrine together, under the
  smallest limit among the agents the repository serves. What is lost is the tail, and the tail is the
  doctrine.
- **Words, for attention.** Starting ceilings: the brief, 1,500 words and 200 lines; an area document or
  a room, 600 words; the backlog, 3,000 words; a backlog row or an archive line, 60 words.

**A ceiling reports and never fails**, because whether a document says too much is a judgement. When one
goes over, the read path has grown: move its history to the record that keeps it and what belongs in
another tier there, then condense, and only then raise the number,
deliberately and with the reason. What fails is a fact: a named document missing, or a list of places
that no longer matches the files.

### What may run unasked is a declaration

What a session may run without asking goes in a declaration a tool reads, beside the gates, and a
person accepts it once: the checks that are a session's to run, the build and test commands, the
install from the lockfile. The brief says only where it is.

- **Exact commands, one per entry.** A pattern that admits any argument to a runner admits anything.
- **The carve-outs never enter it**: a push, a publish, a release, a history rewrite, a discard, a
  recursive delete, a path outside the repository (`autonomous-development`).
- **It takes effect once it has landed and a person has accepted it.** One a session can edit and have
  obeyed in the same run is an agent granting itself permission.
- **Not every check is a session's to run.** One that needs the machine quiet, opens windows or runs
  long belongs to whoever runs the full set of gates, and the declaration says which.

### Without the tool

Every part of this is a committed file. A repository with no doctrine tool writes its list of places by
hand and keeps its ceilings by judgement; a tool adds the list kept true and the facts checked.
