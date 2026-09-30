---
name: dispatch-subagent
description: The brief every subagent working a branch of this repository follows, and the parent's half of dispatching one. The subagent's half says what to read, which gates it may and may not run, which files it never edits, how it takes its reserved decision number, and what its hand-back carries. The parent's half covers reserving the number, naming the lane, running at most three at once, and merging with tools/merge-branch.mjs. Use when dispatching a subagent, and, as that subagent, before starting the task.
---

# dispatch-subagent

The parent used to type this brief into every subagent's prompt. It now lives here, so a dispatch prompt
carries only what differs per branch: the task, its lane, the files it must not touch, and its reserved
decision number (MOD9, `docs/2026-09-30-parallel-development-design.md` §3 rule 7).

## The dispatch prompt

```
Task: <ROW>: <what, and why>. The contract is <design document, section> and the <ROW> row in TASKS.md.
Start from: main at <sha> or later.
Lanes: <lane ids in daoris.lanes.json, e.g. cli, tools>. Do not touch: <files a branch in flight holds>.
Decision number: D<n>, reserved for this branch. (Or: none. Write no decision.)
Follow the dispatch-subagent skill's subagent half.
```

## The subagent's half

### Before any code

1. Merge main into the branch (`git merge --ff-only main` on a fresh worktree) and confirm the start
   commit the prompt names.
2. Read `CLAUDE.md` and `AGENTS.md`, run `doc-loader`, and read what it routes you to, starting with the
   design document the prompt names. Say which documents you read.
3. Count the tests in the suites your lane touches before changing anything. The hand-back reports the
   count before and after.

### While working

- **Stay in your lane.** Change the files your lane owns (`daoris.lanes.json` lists each lane's paths
  under its id) and the tests beside them. If you need a change in another lane, say so in the
  hand-back and leave it to the parent to schedule. Never make it yourself.
- **Never edit the `records` lane's files**: `TASKS.md`, `docs/task-archive.md` and `daoris.lanes.json`.
  They are the steward's, and the parent is the steward. The merge tool names a branch that touches
  them. One exception: a path you add that no lane owns fails the lanes test in `verify` until the map
  places it. Place it, and name that change in the hand-back.
- **Take exactly the reserved decision number.** With no reservation, write no decision. Never take
  the next free number: branches that did that took one number between them (D106).
- **TDD.** Write the failing test first and watch it fail.
- **Follow `CLAUDE.md`'s conventions.** Writes are atomic, BOM-less UTF-8 and LF. No machine path or
  private repository name goes in a tracked file or a commit message. A code comment gives the reason
  and names the task, never the owner's words. A `tools/` script whose helpers are imported guards its
  runner with `isMain`.

### Gates

- **You may run** `npm run verify` at the root (`npm ci` first in a fresh worktree), `node --test` in
  `src/Daoris.Cli`, the web's vitest loop (`npm --prefix src/Daoris.Web run test`, after
  `npm --prefix src/Daoris.Web ci`) when your lane is the web, and `dotnet test` on the .NET project
  your lane changes, one suite at a time. A desktop suite runs its fast half only:
  `dotnet test src/Daoris.Desktop/<project> --filter Category!=Process` (MOD8).
- **Never run** any of these:
  - a desktop suite's `Process` half (`--settings src/Daoris.Desktop/process.runsettings`): the test
    classes that start real processes or run real ticks
  - the rehearsals (`npm run rehearse`, `rehearse:family`, `rehearse:deploy`) or `test:web`
  - the desktop window (`npm run desktop -- run|shot|eval|click|restart|kill`)
  - `publish:desktop` or `publish:service`
  - `tools/merge-branch.mjs`
  - anything that touches the owner's running install: its folder, its home, its processes. Never stop
    a Daoris process.

  The parent runs these serially when it merges. Several worktrees building at once is the load that
  makes real-process tests fail (FLAKE1).
- **What you cannot run, read.** When you change words a person or a test reads (a sentence, a label, a
  refusal), search for the old words in every test you may not run: the desktop suites' `Process` half,
  `tools/*rehearsal*.mjs` and the web's `e2e/` specs. Update each hit, and list them in the hand-back.
  NAME1b's renames passed every gate it could run and failed three it could not, all on words it had changed.
- **Report a flake; do not chase it.** If a real-process test fails under load and passes alone, say so.
  If you think a failure is not yours, name it in the hand-back with the evidence (for example, it
  fails the same way on main). Never skip it silently.

### Finishing

1. Merge main again, resolve any conflict inside your lane, and run your gates again.
2. Commit once per part: `type(scope): <ROW>, <what>`, ending with the session's `Co-Authored-By:`
   line. The merge tool's commit check refuses a commit without that line. Leave nothing uncommitted in
   the worktree: the check refuses that too. Never push, and never rewrite history.
3. The hand-back carries:
   - the **branch** and its **commits** (sha and subject)
   - the **files** changed, grouped by lane
   - **the shape built**, in a paragraph a reviewer can hold the diff against
   - the **decision number** taken, or none
   - **what the parent must look at on the window**: each surface changed and the screenshot that would
     prove it, or "nothing on the window"
   - **test counts** before and after, per suite
   - what was **left out**, and why
   - **an outcome paragraph** the parent can paste under the row in `docs/task-archive.md`

## The parent's half

1. **Reserve the decision number** before dispatching. It is the next number after both the highest in
   `docs/DECISIONS.md` and every number already reserved for a branch in flight. Name it in the prompt.
2. **Name the lanes by id** (`daoris.lanes.json`, design §5) and the files the branch must not touch,
   which is anything a branch in flight holds. When two branches need the same lane, one waits for the
   other or the work is split.
3. **Run at most three at once.** More load makes real-process tests flake.
4. **Merge with `tools/merge-branch.mjs`**, from the main checkout with a clean tree:
   - `--plan <branch>` shows the lanes, the commit check and the gate order, and merges nothing.
   - `<branch>` merges with `--no-ff --no-commit`. It then runs every gate `daoris.gates.json` declares
     and every rehearsal the release workflow adds, fast first, whatever the branch touched. Each
     gate's whole log goes to `local/scratch/merge-<branch>/`. The lane report names lanes by id and
     title. Look harder at the diff of a branch that crossed lanes or touched the steward's records.
   - On a conflict the tool stops and names the files. Resolve them, `git add` them, then run
     `--continue`. On a failed gate, read that gate's log, then either fix it in the merge and run
     `--continue`, or run `git merge --abort`.
   - For several branches, run `<first> --batch <second> …`. Each merge gets the checks and suites.
     Commit it, then `--continue` merges the next. The rehearsals run once, after the last.
   - A FLAKE line is a real-process test that failed in the suite and passed alone, or a rehearsal that
     died (its process ended, or it printed nothing) and passed when run again. Record it under FLAKE1;
     never let it through unrecorded.
5. **Commit the merge yourself.** The tool never commits. Read `git diff --cached` and write the
   records, as the `records` lane's steward: move the row from `TASKS.md` to the archive with the
   hand-back's outcome paragraph, add the changelog entry, and update the counts in `TASKS.md`'s State.
   Then commit as `Merge <ROW>: <what>`, with a body and the `Co-Authored-By:` line.

## Why

Each rule here is here because a branch broke it. Two branches took one decision number. A hand-typed
gate chain lost a rehearsal's reason. A "web only" batch skipped the .NET suites and broke main unseen.
Three worktrees building at once made tests fail that pass alone. A prompt that restates all of this
drifts from the last one. A skill is read the same way every time.
