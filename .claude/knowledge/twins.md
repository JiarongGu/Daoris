---
name: twins
applies_when: two artefacts must agree on a file, a layout or a rule, or you are changing one side of an existing twin
enforces: the artefacts share no code; the file is the contract; each side reads it with its own code and carries a test table the other matches; the twins change together
---

# Twins — how two artefacts agree without sharing code

**The CLI, the service, the driver and the page share no code, by design. Where two of them must agree on a fact,
the FILE is the contract: each reads it with its own code, each carries a test table the other must keep matching,
and a change to one side is a change to both, in the same commit.**

## Why

They are different runtimes shipped to different places: a zero-dependency Node package, a .NET host that may run on
a server, a .NET library the desktop carries, a React page. A shared library would make each ship the others'
dependencies. So a file is read twice on purpose, and the one risk is drift: a rule one reader enforces and the other
contradicts, in front of a person, on a machine where both run.

## How to apply

- **Name the file and its rules once**, in the design or decision that introduces them. **Each reader's header names
  its twin by file**, and its test names the table both hold. Those headers are the list (`git grep -n -i "twin" -- src
  tools` finds each reader that names one), so no table of them is kept here.
- **Each twin carries a test table of the rules**, written so the other side's can be compared line by line: the same
  cases, the same answers, including what absence means. A shared table among the fixtures, read by both, is better
  still. A rule one table has and the other lacks is the drift, found before a person meets it.
- **Change both sides in the same commit**, tables included. A commit that changes one reader of a twinned file is
  incomplete, whatever its tests say.
- **Absence is a rule, not a default.** The answers for a missing file or field are where twins drifted most.
- **An editor preserves what it has no field for.** It edits only the key paths that differ between the file as read
  and the edited choices, onto the file read again under its lock, so another writer's change made in between stays;
  a file missing at the read is `{}`, never a licence to write whole, and a file that does not read is refused. Both
  writers keep one key order. Prove it on a file the writer did not write, with keys neither side knows.
- **A constant both sides need is duplicated deliberately**, with a comment naming the other copy, never imported
  across.
- **A twin reads the other side's file by its name, never its folder**, so a move edits no reader.
- **A test that reads another lane's source or table names it to the merge tool** (a `REACH` row in
  `tools/merge-branch.mjs`), so a change to it runs the reader's gate.
- **A file taken by whoever creates it first is put in place with no overwrite, on both sides**: C#'s
  `File.Move(…, overwrite: false)`; in Node a `linkSync` of the written file, then its removal, since `renameSync`
  replaces on Windows and `{ flag: 'wx' }` leaves a moment where the file is empty.
