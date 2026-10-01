---
name: set-up-documents
description: Set a repository's development documents up to the standard, or repair ones that drifted — the brief, the rooms, the records and the declaration of safe work — from the templates beside this skill. Use when a repository is set up for agents, when a request asks for it, or when a session could not find where something is written.
---

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
   none. Look under the usual names: a changelog, a decisions file or a folder of decision records, a
   fix log, a backlog or task list, a documentation index, a glossary. Note any file playing two roles,
   and any agent-specific instruction file that only some agents read. Measure the root instruction file
   in bytes, and each document read whole in words.

2. **Write or trim the brief** from `templates/brief.md`, in the root instruction file, outside any
   region a tool generates. Keep every heading; a section with nothing to say says *none*, so a reader
   can tell an empty section from a forgotten one. Move into it what every agent needs from an
   agent-specific file, and leave there only what that one agent needs. Hold every line to the content
   test, then move each line that fails to the tier that holds it:
   - a deep dive one area needs → a knowledge document (`templates/knowledge.md`)
   - one folder's conventions, traps or checks → that folder's room
   - history, and what was rejected → the decisions
   - status and counts → the backlog, or the one place that keeps them
   - a permission → the declaration (step 5)

3. **Write the rooms.** A folder whose conventions, traps or checks differ from the rest of the
   repository gets an instruction file of its own, from `templates/room.md`. Not every agent loads a
   nested file, so each room must be named in something always read: the tool's declaration of rooms
   where there is one, the brief's *Layout* where there is not.

4. **Place the records.** Give each record the repository keeps a line in the brief's *Where things
   are*, or declare it where a tool generates that section. Check its shape against its template (see
   the table below). New entries take the shape; old entries stand as they were written, because
   rewriting history into a new form loses what the old form said.

5. **Declare the safe work** in the declaration beside the gates: the checks a session may run (not the
   ones that need the machine quiet or run for a long time), the build and test commands, the install
   from the lockfile. Exact, one command per entry, and none of the carve-outs. The brief's *Build,
   test, verify* names the one command that means done and says where the declaration is. It takes
   effect when a person accepts it, and the review says so.

6. **Measure.** Each document read whole against its ceiling in words; the root instruction file,
   brief and doctrine together, against the smallest byte limit among the agents the repository serves.
   Over is reported, never hidden. Relocate, then condense, then raise the ceiling with the reason.

7. **Check, and leave it for review.** Run the doctrine check where there is one and the repository's
   own build and tests. Then hand over: each line that left the brief and where it went, each record
   placed, each room written, the declaration and what it allows, and every measure against its ceiling.

## The templates

| Role | Its shape |
|---|---|
| brief | `templates/brief.md` |
| room | `templates/room.md` |
| knowledge | `templates/knowledge.md` |
| router | `templates/router.md` |
| decisions | `templates/decision.md`, one entry |
| backlog | `templates/backlog-row.md`, one row |
| archive | `templates/archive-entry.md`, one entry |
| fixes | the `fix-log` skill's entry |
| skill | a folder named for it, holding its entry file, whose frontmatter is `name` and `description` |
| changelog, glossary, gates | the repository's own shape, holding what `development-documents` says each holds |

**Copy a template's body, never its provenance line.** A template arrives stamped as shared doctrine.
On a copy, that line tells the next reader not to edit a file that is theirs. Replace every `<...>`;
a placeholder left in place reads as an instruction to whoever opens the file next.

## Why

Every session in the repository pays for what this sets up: the brief on every start, a missing record
on every search. Done once and with care, it is the cheapest improvement the repository's agents will
get. Done by accretion, the brief grows until an agent that cuts at a byte limit loses the doctrine at
its tail, and nobody sees it happen.

This skill pre-approves no tools, on purpose. What a session may run is the declaration's to say, and
a person accepts it.
