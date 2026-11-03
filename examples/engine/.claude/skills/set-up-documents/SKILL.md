---
name: set-up-documents
description: Set a repository's development documents up to the lean standard, or repair ones that drifted — the brief, the area documents, the few records and the declaration of safe work — from the templates beside this skill. Use when a repository is set up for agents, when a request asks for it, or when a session could not find where something is written.
---
<!-- daoris: core/core/skills/set-up-documents/SKILL.md @ 0.0.1 — canonical; edit via `daoris upstream` -->

# set-up-documents

The procedure behind the `development-documents` knowledge document, which says why each step is shaped
the way it is. Read that first. The templates are in `templates/`, beside this file.

## Before you start

- **The repository's own words are kept.** Every step moves, trims or declares what is already here.
  Nothing is deleted until each of its lines is somewhere else, and the test for a line you are about to
  drop is not "is this obvious" but "does anything else say it".
- **This repository only.** A document another repository should change is a request to whoever works
  there, never an edit made from here.
- **The result is for review.** It changes what every later session here reads, so it is left for a
  person to see whole, with what moved where.

## Steps

1. **Inventory by role.** For each role in `development-documents`, name the file that plays it, or
   none: a changelog, a decisions file or folder, a fix log, a backlog, an archive, a documentation index
   or router, per-folder instructions. Note any file playing two roles. Measure the root instruction file
   in bytes, and each document read whole in words.

2. **Write or trim the brief** from `templates/brief.md`, in the root instruction file, outside any
   region a tool generates. Keep every heading; a section with nothing to say says *none*. Hold every
   line to the content test, and move each line that fails to the tier that holds it: what one part of
   the code needs → that part's area document; a deep dive → a knowledge document
   (`templates/knowledge.md`); history and what was rejected → the decisions; status and counts → the
   backlog; a permission → the declaration (step 5).

3. **Write the area documents** from `templates/area.md`, one per part a change is made in, in one folder
   (`docs/areas/` unless the repository keeps one already). Fill each from what is already written about
   that part (its README, its design, the brief's lines about it) and from the code: the lines to start
   at, one exemplar, the folder new behaviour goes in, the filtered test command, the traps. List them in
   the brief's *Areas* table. A folder whose own instructions an agent loads when working inside it may
   keep them as its room (`templates/room.md`), pointing at its area document.

4. **Trim the records to their shape.** The backlog to rows of a line or two (`templates/backlog-row.md`),
   each naming its area document; what a row held beyond that moves to the record that holds it, and the
   row points there. New archive entries are one line (`templates/archive-entry.md`), new decisions are
   for real choices only (`templates/decision.md`), and new fix entries are for regressions only
   (`templates/fix-entry.md`). Old entries stand as written: rewriting history into a new form loses what
   the old form said. Give each record a line in the brief's *Where things are*, or declare it where a
   tool generates that section.

5. **Declare the safe work** in the declaration beside the gates: the quick checks a session runs after
   every change, the filtered test commands, the build, the install from the lockfile. Exact, one command
   per entry, and none of the carve-outs. The brief's *Build, test, verify* names the quick checks and the
   one command that means done, and says where the declaration is. It takes effect when a person accepts
   it, and the review says so.

6. **Measure.** Each document read whole against its ceiling in words; the root instruction file, brief
   and doctrine together, against the smallest byte limit among the agents the repository serves. Over is
   reported, never hidden. Move, then condense, then raise the ceiling with the reason.

7. **Check, and leave it for review.** Run the doctrine check where there is one and the repository's
   quick checks. Then hand over: each line that left the brief and where it went, each area document
   written, each record trimmed, the declaration and what it allows, and every measure against its
   ceiling.

## The templates

| Role | Its shape |
|---|---|
| brief | `templates/brief.md` |
| area | `templates/area.md` |
| room | `templates/room.md` |
| knowledge | `templates/knowledge.md` |
| decisions | `templates/decision.md`, one entry |
| backlog | `templates/backlog-row.md`, one row |
| archive | `templates/archive-entry.md`, one line |
| fixes | `templates/fix-entry.md`, one regression |
| router, index | `templates/router.md`, `templates/index.md`, only where the repository keeps one |
| skill | a folder named for it, holding its entry file, whose frontmatter is `name` and `description` |
| changelog, gates | the repository's own shape, holding what `development-documents` says each holds |

**Copy a template's body, never its provenance line.** A template arrives stamped as shared doctrine.
On a copy, that line tells the next reader not to edit a file that is theirs. Replace every `<...>`;
a placeholder left in place reads as an instruction to whoever opens the file next.

## Why

Every session in the repository pays for what this sets up: the brief on every start, an area document
on every change, a missing line on every search. Done once and with care, it is the cheapest
improvement the repository's agents will get. Done by accretion, the brief grows until an agent that
cuts at a byte limit loses the doctrine at its tail, and the records grow until nobody reads them.

This skill pre-approves no tools, on purpose. What a session may run is the declaration's to say, and
a person accepts it.
