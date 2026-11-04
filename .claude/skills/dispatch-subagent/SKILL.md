---
name: dispatch-subagent
description: The parent's half of dispatching a subagent to work a branch of this repository — orienting the row with a branch-scout, sizing it, choosing each role's model, reserving the decision number, naming the lane, running at most five workers at once (a CI run beside lowers that never), merging with tools/merge-branch.mjs, and measuring what the dispatch cost. The subagent's half is the branch-worker agent's own definition. Use when dispatching a subagent.
---

# dispatch-subagent

A feature is built by two agents (D160, D187), each through the `change` skill: a **branch-scout** takes its reading
steps and returns the brief's lines, and a **branch-worker** builds it on its own branch and worktree, end to end
across the lanes it needs. Each one's
instructions are its definition in `.claude/agents/`, so a dispatch prompt carries only what differs per branch. The
unit is a feature a person can see working; the rows it covers become one block row when it is dispatched.

## The workflow: main is the development branch

- **Branch from main's tip.** A builder merges main in at the start and again before it hands back, and reruns its
  touched tests after that last merge. No batch branch is stacked on another.
- **Merge a finished branch into main the same hour**, through `node tools/merge-branch.mjs <branch>`. Its default runs
  the blocking set (`universal`, `code-map`, `test-map`) and the path checks its paths reach; a plain `git merge`
  runs none. A conflict goes back to its builder.
- **Commit only after the tool exits 0** and `git status` shows no `UU` path. A merge it leaves *NOT committed* is
  committed after `git add -A`, since its gates read the tree as `git add -A` would stage it.
- **The CI runs on its own cycle**: `--ci <main>` in the background after merges land, never wrapped in `timeout`; its
  reds come back as small fixes on main. The install is staged only from a green run (`--stage --from-gated <commit>`).
- **Clean up as you go**: `--prune` removes each worktree and branch main holds. Lock a worktree you make by hand, or
  the next prune takes it as merged.

## The briefs

The scout, read-only and outside any worktree:

```
Row: <ROW> in TASKS.md. Contract: <document, section>. Main is at <sha>.
Lanes the row names: <lane ids>. Branches in flight hold: <files>.
```

The worker, with `subagent_type: branch-worker` and the model its role takes:

```
Task: <ROW>: <the outcome a person sees>, because <why>.
Area: <the area document, or the contract: document, section>. Beyond it: <design §>, or none.
Start from: main at <sha>.
Change: <file>:<a-b> <symbol>; the new unit goes in <folder>.
Mirror: <file>:<a-b>.
Tests: <file>; filter: <command>.
Words or fields read by suites you cannot run: <term> -> <file>:<line>, or none.
Do not touch: <files a branch in flight holds>.
Decision: D<n> reserved, or none.
Done when: <one line per point of the proof>.
Follow the `change` skill. Hand back as your definition says.
```

Fill the lines from the scout's pack, opening any pointer that looks wrong. A brief that reaches the service says to
copy `local/lyntai-local.props` from the main checkout's `local/` into the worktree's.

**Every brief carries D231** (the worker's definition spells it out): its *Change* line names the subdomain a new unit
belongs in, not only the hub's lines where the area lives today, and a composition root only wires.

**Inside a big scope** (D180's note of 2026-11-04) the brief asks for less: the builder takes a whole wave or more,
builds it row after row with a commit each, writes its tests, compiles and hands back once, running none; the parent
merges with no gates; every check runs once when the scope's development is done. Outside one, the builder runs its
own filtered tests, `npm run quick`, the censuses of each desktop test project it changes (`--filter Category=Census`),
and the `Process` classes it added or changed.

## Choosing the model (D160, D174)

Choose by role: [`docs/model-roles.md`](../../../docs/model-roles.md) names each role's model and effort. The Agent
call's `model` overrides the definition's for one dispatch. Measured 2026-11-04: Sonnet built exact rows on 70-90K
tokens where Opus builders took 300-900K, so **Sonnet takes every chunk that follows an exemplar already on main**
(the next routes onto a pattern, a deletion sweep with its list, a rename, a comment cut, tests rewritten onto a
named helper). Opus takes plans, reviews, a pattern's first exemplar and calls that need judgement. Each builder
hands back at about 150 tool calls, and a fresh one continues: a start costs about 32K, and a carried context of
450K costs that much on every turn. Measure each batch with `tools/subagent-usage.mjs` and `tools/subagent-time.mjs`. The split is a balance,
not a ban: weigh cost against quality, attributing each red the scope's verification finds (and each rework) to the
chunk and model that made it, and move the line toward Opus where Sonnet's chunks come back with more of either.

| Role in this skill | The reference's role |
|---|---|
| Parent: plan, review, merge | steward |
| `branch-scout` | orient |
| `branch-worker`, one lane, the change named exactly | build-exact |
| `branch-worker`, an accepted design: UI, several lanes, a decided deletion or concurrency | build-settled |
| `branch-worker`, writing a design or a decision, or a row whose design is the work | build-judgement |
| A read of a design, a decision or a risky merge by another maker | decide-support or review |

## The parent's half

1. **Orient with a scout**, every row, a design row most of all. Scouts build nothing and do not count toward the cap.
   For a judgement row, ask another maker for a pre-mortem beside the scout and put its verified findings in the brief.
2. **Reserve the decision number**: the next after both the highest file in `docs/decisions/` and every number reserved
   for a branch in flight.
3. **Dispatch a feature, end to end.** Never split it into a host half and a page half; split only a feature too large
   for one builder, into parts that each work. A hand-back proposes no new row.
4. **Name the lanes by id** (`daoris.lanes.json`) and the files a branch in flight holds. Two features needing one file
   go one after the other.
5. **Run at most five workers at once; a CI run beside never lowers that.** Never brief a worker to generate load or to
   run or time a long run. Dispatch between merges, never while the merge tool writes git metadata; a refused dispatch
   leaves a half-made tree to remove first. Nothing started outlives its work: a watch is a loop that ends on its outcome,
   never `tail -f | grep`, which outlives its shell on Windows. A builder stopped by hand never ran its own
   `dotnet build-server shutdown`, so after a batch the parent runs it, and stops each process a stopped builder left.
6. **Merge through the queue: records commit, preview, queue** (D207). The records commit goes on the branch (the row to
   the archive, `TASKS.md`, the changelog, a FIX-LOG entry); `<branch> --preview` records the tip read; then
   `<branch> --message <file>`. Before a batch is queued, run the desktop's and the service's fast halves, `npm run
   verify` and `npm run test:web:unit` on it. The other modes are in the tool's header; the full set is owed before the
   install is built.
7. **Scan what you commit** (`daoris-devkit scan`): no universal gate reads records written after the gates. Then
   `--prune`; end what you started, and name to the owner what you did not.
8. **Measure the batch**: `node tools/subagent-usage.mjs <transcripts folder> --since <day>` against the reference's
   targets, noted under D160.
9. **Ask another maker for a second opinion** where the owner granted one and a decision, a design or a stubborn defect
   would gain from it. Its words are findings: verify each, then route it.
