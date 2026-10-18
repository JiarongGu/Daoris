---
name: dispatch-subagent
description: The parent's half of dispatching a subagent to work a branch of this repository — orienting the row with a branch-scout, sizing it, choosing each role's model, reserving the decision number, naming the lane, running at most three workers at once, merging with tools/merge-branch.mjs, and measuring what the dispatch cost. The subagent's half is the branch-worker agent's own definition. Use when dispatching a subagent.
---

# dispatch-subagent

A row is built by two agents: a **branch-scout** orients it and returns an orientation pack, and a
**branch-worker** builds it from the pack on its own branch (D160). Each one's brief is its agent
definition in `.claude/agents/`, which the harness loads as that agent's instructions, so a dispatch prompt
carries only what differs per branch (MOD9, `docs/2026-09-30-parallel-development-design.md` §3 rule 7).
This skill is the parent's half; a worker never reads it.

## Why two agents

Every turn re-reads the whole context, so what a branch loads before its first edit is paid again by every
turn after it; that load, not the work, was most of what branches cost (D160,
`docs/2026-10-10-subagent-load-evidence.md`). The scout pays for orienting once, on a smaller model and a
context it hands back; the worker starts from a page of pointers.

## The dispatch prompts

The scout, read-only and outside any worktree:

```
Row: <ROW> in TASKS.md. Contract: <design document, section>. Main is at <sha>.
Lanes the row names: <lane ids>. Branches in flight hold: <files>.
```

The scout preloads `doc-loader` and `pattern-finder` (D160's SUBLOAD1c note); a pack whose *Knowledge routed* line is
empty goes back to it.

The worker, with `subagent_type: branch-worker` and the model the table below chooses:

```
Task: <ROW>: <what, and why>. The contract is <design document, section> and the <ROW> row in TASKS.md.
Model: <model>, because <the row's kind in the table>.
Start from: main at <sha> or later.
Lanes: <lane ids in daoris.lanes.json, e.g. cli, tools>. Do not touch: <files a branch in flight holds>.
Decision number: D<n>, reserved for this branch: write docs/decisions/D<n>.md. (Or: none. Write no decision.)
How you work: edit files only with Edit and Write, never through python, sed, perl, cat/printf redirection or any
shell redirect (a hook refuses them; that includes appending a decision note); write
each failing test first and RUN it to watch it fail before writing the code (a compile failure is a red: run it),
and say which you saw fail; commit each piece once its tests pass. A component's sources changed on a later day than
its README asks the README to describe the change (the docs gate). New words on the page run `src/locales`' tests too:
`names:check --strict` does not hold a sentence's glossary words, `names.test.ts` does (WORKFLOW1h-d's 这台机器).
A control moved or renamed runs the whole web suite, not only the touched files: other views' tests press it by name
(HEAD2d's *Pause…*, pressed in QuestsView.work.test.tsx). An archive entry is appended at the file's end, after one
blank line, in its neighbours' shape (`## ROW — title (date, Dn)`, the quoted row, `**Outcome** date: …`).
Orientation pack:
<the scout's pack, as it came back, with any pointer you corrected marked>
```

## Choosing the model (D160, D174)

Choose by role, never by a remembered name: the current model, version and effort for each role, its evidence, what is
on trial and when to ask an agent of another maker are in [`docs/model-roles.md`](../../../docs/model-roles.md). Each
role's model is its definition's `model:`, and the Agent call's `model` overrides it for one dispatch with the role's
current entry.

| Role in this skill | The reference's role |
|---|---|
| Parent: plan, review, merge | steward |
| `branch-scout` | orient |
| `branch-worker`, one lane, the pack names the change exactly | build-exact |
| `branch-worker`, design, UI, more than one lane, a decision to write, deletion or concurrency | build-judgement |
| A read of a design, a decision or a risky merge by another maker | decide-support or review |

Measure each batch (step 8) against the reference's targets, note the numbers under D160, and move the reference when
they say so. A trial candidate takes one or two rows of its role in a batch. Daoris's managed sessions take the same roles
once three batches hold the targets (MODELROLE1).

## The parent's half

1. **Orient with a scout.** Dispatch `branch-scout` with the row. Scouts read and build nothing, so they do
   not count toward the three workers below, and several may run at once. Read the pack it returns,
   open any pointer that looks wrong, and correct it before passing the pack on. A pack whose
   *Size* says the row is more than one sitting is split first (step 3). Never skip it, a design row least of all: NEW1b
   went with the parent's pointers alone and ran 184 turns, 77 of them before its first edit (D160's twelfth batch).
   For a judgement row (an Opus row: a race, a format, a destructive path, a protocol), ask the other maker of step 9 for
   a pre-mortem beside the scout: the row, its contract and the code it touches, and "what will a builder get wrong
   here". Put its verified findings in the brief. A review after the build costs a rework round each time: on
   2026-10-18 LANDRACE1 took three rounds (a later commit hidden by `already`, path aliases, the wall clock, the
   hand-off, linked worktrees), GRAPH1b two and CLOSE1b one, each 30 to 60 minutes of building and a re-check.
2. **Reserve the decision number** before dispatching. It is the next number after both the highest file
   in `docs/decisions/` and every number already reserved for a branch in flight. Name it in the prompt.
3. **Size the branch to about a hundred turns.** Cost grows with the square of a branch's turns: in the
   evidence, branches of 100 to 200 turns cost three times those under 100, and branches of 200 to 300 cost
   seven times. A row that changes more than one surface, or more than one lane, is split into rows that
   each fit, and each gets its own pack. A pack's *one sitting* does not override this: ENTRY1f2, sized so
   across driver, modules, service and page with the room's goldens, ran 129 turns and cost what a branch
   did before the split (D160's third batch). A lane that only takes a sentence or a parameter may ride along.
4. **Name the lanes by id** (`daoris.lanes.json`, design §5) and the files the branch must not touch,
   which is anything a branch in flight holds. When two branches need the same lane, one waits for the
   other or the work is split.
5. **Run at most three workers at once.** More load makes real-process tests flake, and the owner's installed window shares
   the machine: on 2026-10-15 seven workers, a full gate set and one worker's CPU-load script (briefed to "reproduce under
   load") froze it for minutes while the owner worked. Never brief a worker to generate load, and while the owner works keep
   the builds and tests below normal priority, so the window always wins the processor. **Dispatch between merges: before one
   starts, or after its commit and before `--continue`.** The harness's worktree isolation reads the checkout's git
   metadata, refuses while the merge tool is writing it, and leaves a locked worktree behind. A dispatch made once a merge's
   first gates had passed was refused the same way (2026-10-10), so a merge's gates are no safe moment either. Unlock it
   (`git worktree unlock`) so the next prune takes it, and dispatch again. Two dispatched in one message
   while a merge's gates ran were both refused the same way (2026-10-08), each a half-made tree holding only
   `.git` and `.claude`: remove each (`git worktree remove --force`, then its branch), and dispatch one at a time.
6. **Merge with `tools/merge-branch.mjs`**, from the main checkout with a clean tree:
   - `--plan <branch>` shows the lanes, the commit check, the prune and the gate order, and merges nothing.
   - `<branch>` merges with `--no-ff --no-commit`, writes `docs/index/` from the merged tree (ORIENT1), then runs
     the baseline (the universal gates, the code map, the orientation index's check, `verify`), the fast halves of the suites the merge's changed paths can reach, and the web gate
     and the release and family rehearsals they reach, by the tool's lane table (GATE3). **It skips the
     long halves** (GATE5): the driver's and the modules' `Process` halves and the deployment rehearsal
     never run at a merge, and every run and `--plan` says each is in the full set before staging. It
     prints why each gate runs or is skipped. `--full` runs every gate `daoris.gates.json` declares and
     every rehearsal the release workflow adds. The light gates run beside the serial Process lane
     (GATELANES1); add `--one-at-a-time` on a loaded machine. Each gate's whole log goes to
     `local/scratch/merge-<branch>/`; a Process half also leaves a `.trx` there, with its ten slowest
     classes printed after it (PROC1). The lane report names lanes by id and title. Look harder at the
     diff of a branch that crossed lanes or touched the steward's records.
   - **The full set is owed before the install is built**, the long halves with it. `publish:desktop`
     refuses a tree no full set passed (`merge-branch --passed`). Run `node tools/merge-branch.mjs --full`
     on the checkout, or `--full` on the day's last merge, or `--full --beside` to gate main's tip in `local/gate-tree`
     while the merges go on, then stage it with `--from-gated <commit>` (GATEWT1); it copies `local/sensitive-patterns.txt` into the tree and keeps its logs in `local/scratch/beside-<sha7>/`. 🔴 Never `--full` on a `--batch`: it runs every gate,
     the long halves included, for EVERY merge in the batch (a docs-only design paid forty minutes of the driver's
     Process half, 2026-10-09). Merge the batch plain, then `--stale` once. A verdict stands until a path its gate reaches
     changes (GATE6), and records written after the gates are forgiven; a refusal names each stale gate and
     the path that made it so. After the full set, on a clean checkout with no merge open, `--rerun <gate>…`
     runs a gate that flaked, with each stale gate, and `--stale` runs only the stale ones after a fix you
     committed (GATE6b). A refusal names the smallest of these that would pass the tree, and `--full` only
     when nothing smaller would. `--force-ungated` is the person's override.
   - On a conflict the tool stops and names the files. Resolve them, `git add` them, then run
     `--continue`. On a failed gate, read that gate's log, then fix it in the merge and run
     `--rerun <gate>` (it re-runs that gate, any gate not yet run and any whose verdict the fix's paths
     reach, keeping the other verdicts; outside `--full` it runs a long half only when named), or
     `--continue` to gate the merge again whole, or run `git merge --abort`.
   - For several branches, run `<first> --batch <second> …`. Each merge gets the checks and suites its
     paths reach. Commit it, then `--continue` merges the next. The rehearsals run once, after the last.
   - Bisecting a failure, run the test five times at each commit: one pass proves nothing of a test that fails some of
     the time. A single pass at an earlier merge once blamed the wrong change; the test failed 5 of 8 there (AUTOTIDY1r).
   - A FLAKE line is a real-process test that failed in the suite and passed alone, or a rehearsal that
     died (its process ended, or it printed nothing) and passed when run again. Record it under FLAKE1;
     never let it through unrecorded.
   - The gate prunes (GATE2) at each merge's start, after its refusals, and at a batch's last
     `--continue`. Each local branch merged into main is deleted with its worktree, one line each. It
     keeps, and says why, a worktree locked for any reason but the agent harness's, one with modified,
     staged or untracked files, one with anything under `local/` but logs and scratch, and whatever git
     refuses. The harness's lock (`claude agent <id> (pid N)`), held for its session's life, is lifted
     when the branch is not at main's tip and no running process names the folder (CLEANUP2). A folder
     under `.claude/worktrees/` git does not list, with no `.git`, is removed; one a process holds is named
     with its pid, and the process is never stopped. It never forces a removal or uses `-D`. `--prune
     --plan` shows it, `--prune` runs it alone, and `--no-prune` skips it for one merge, for an agent you
     mean to continue in its worktree.
     🔴 **Lock a worktree you make by hand** (`git worktree add --lock …`, or `git worktree lock` after):
     an integration worktree at main's tip before its first commit counts as merged and clean, and the
     next merge's prune would take it. Unlock it once it has landed, so the prune can clear it.
   - **A hand-back that names `Process` tests it wrote and could not run** is run by the parent on the open merge before
     the commit: `dotnet test <the test project> --settings src/Daoris.Desktop/process.runsettings --filter
     "FullyQualifiedName~<class>"`, or `git.process.runsettings` for a class marked `Parallel=Git` (PROC1c), which
     the first file's filter leaves out: run with it, such a class matches no test. A merge never runs those halves,
     so nothing else would until the full set. XAGENT1g3's
     equivalence test failed on its first run there (a fixture seeded another state than the test named, 2026-10-12).
7. **Commit the merge yourself.** The tool never commits. Read `git diff --cached` and write the
   records, as the `records` lane's steward: move the row from `TASKS.md` to the archive with the
   hand-back's one-line outcome and its pointer, add the changelog entry, and update the counts in
   `TASKS.md`'s State. `doc-shapes` reports an outcome or a row over its shape.
   Then commit as `Merge <ROW>: <what>`, with a body and the `Co-Authored-By:` line.
   **Scan what you staged first** (`daoris-devkit scan`): records written after the gates, and every records
   commit made outside a merge, are the one change no universal gate reads before it is committed. A
   backlog row quoting a window's caption put a private name into history that way (2026-10-08).
   **The next merge's prune removes the worker's worktree and branch**, its harness lock with them, once nothing is
   left inside; `--prune` does it at once. Read the kept lines: each names what holds a tree (work, a file under
   `local/`, a running process), and what holds it is yours to settle. 43 subagent worktrees had piled up by
   2026-10-15, and 24 stood with three agents running on 2026-10-18, the leftover the owner cleared by hand.
8. **Measure the batch.** `node tools/subagent-usage.mjs <the harness's transcripts folder for this
   repository> --since <the batch's first day>` prints each agent type and model's turns and its context
   at the start, at the first edit and at the end. Feed the hand-back's *pack's gaps* into the next pack.
9. **Ask an agent of another maker for a different opinion, on your own initiative, where the owner has granted one**
   (the private notes name it and how it runs, read-only, in its own detached worktree). Not a step every batch passes
   through, but a habit: reach for it whenever a second view would help, such as a decision or a design before it is
   built on, a choice between two shapes, a defect whose cause will not show, or the backlog's order. Its words are
   findings, never facts: verify each against the code, then route it as a fix, a row or a decision note, and say which
   were dropped and why. It runs remotely, so it costs nothing beside the gates.

## Why

Each rule here is here because a branch broke it. Two branches took one decision number. A hand-typed
gate chain lost a rehearsal's reason. A "web only" batch skipped the .NET suites and broke main unseen.
Three worktrees building at once made tests fail that pass alone. A prompt that restates all of this
drifts from the last one. A brief is read the same way every time. And branch after branch spent its first
fifty turns finding what a page of pointers could have named (D160).
