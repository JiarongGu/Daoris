# Teamwork: a large ask built by a layered team of agents on the engines that exist, with checks capped instead of agents and every hand-back observed (TEAMWORK1)

**Decided 2026-11-12 (D285).** The owner directed on 2026-11-12 that Daoris may add managing, supervising and other structure so
that a large task is handled by several agents in a better layered workflow, supporting each agent and never narrowing it or
lowering its performance (D284). Two independent proposals answered one brief, each having read the repository and not the other:
**Codex** (gpt-6.1-sol) and **Fable** (Fable 5.2). The brief and both answers are the steward's untracked working notes, so each
point below names the proposal its detail comes from, *(Codex)* or *(Fable)*, and stands alone. The steward chose between them in
ten decisions, which §2 to §11 follow in order. Written by Claude Opus 5.5 on 2026-11-12 from main at `f0f7db8410`. Read with D284
and `docs/2026-11-12-access-model-design.md`, `docs/areas/roles-and-supervision.md` (rules 1-3, 7, 20, 29, 31-50),
`docs/areas/workflows-review-landing.md` (rules 8, 11, 15-24, 26, 30, 31b, 34, 39-40, 49-50, 58), `docs/areas/driving.md` (rules 7,
11-14, 40, 43-46) and `docs/areas/co-working-and-messages.md` (rules 4, 12). Nothing here is built. Driver paths start at
`src/Daoris.Desktop/Daoris.Desktop.Driver/`, module paths at `src/Daoris.Desktop/Daoris.Desktop.Modules/`, service paths at
`src/Daoris.Service/Daoris.Service.Core/`, web paths at `src/Daoris.Web/src/`.

## 0. In short

- **No new engine.** A part is a quest on one ask, with its own line and its own workflow run; the team is a membership record
  with a parent projection on top; parts land through the line's integration queue; the existing supervisor is woken by events;
  the lead plans through a door every session has.
- **Cap checks, not agents.** A check broker runs declared heavy checks under the machine's admission, one expensive build or
  test at a time here to start. A worker's turn ends while its check waits and resumes on the verdict, so the wait burns no tokens.
- **One verdict store for the machine**, keyed by gate and by the exact inputs, configuration and toolchain that reach it. A pass
  is never reused because a command's text matches.
- **The hand-back is observed, not self-reported.** Daoris reads the base, the touched paths, the reached checks and what was left
  running, runs a missing check itself, and stops what the part's run started.
- **Conflicts are planned out, never locked out:** a keel part lands the shared contracts first, a claim that collides becomes an
  ordering edge and a word, and workers send record intents that the integrator applies one at a time.
- **Integration is a train:** ready parts batch onto the ask's candidate, one reached run per batch, a bisect names the failing
  part, and the ask is delivered only when its required parts landed and its final acceptance passed.
- **The person sees one item**, its conversation, a parts table and the steering, with every question counted once.
- **Measured against one agent** on the same ask at the same commit, read blind by another maker.

## 1. What was measured, and what the team is for

On this repository's own development, before this design:

- builders took 45 to 123 minutes each, 75 to 90% of it inside builds and tests (a filtered .NET test run 6 minutes, a web
  typecheck 3);
- at five parallel builders the machine sat at 91% CPU and the same check took twice as long;
- 5 of 6 merges failed a check the builder had not run; once builders ran the merge's own light lane before handing back, every
  merge passed first try;
- the backlog and the area documents conflicted at most merges until builders stopped editing the backlog and the integrator wrote
  its rows;
- finished builders often left background processes and their worktrees behind;
- the installed Daoris's own quests took a median 9.4 hours from filed to done, and its watch loop a median 4.3 minutes a pass.

So the loss is in checks and landing, not in thinking *(Fable)*. Thinking runs on the providers' machines; builds and tests run
here. The team is structure an agent reaches for because it is faster than going without: nothing in it refuses a tool, a path or
an act (D284 point 1). A collision becomes an order, a missing check is run rather than demanded, and a role is a brief and some
extra tools, never a smaller toolbox.

## 2. No new engine (decision 1)

| Team piece | What it is built on |
|---|---|
| The large ask | the existing `Ask`, with the person's words, requirements and through words (driving rule 43) *(Codex)* |
| A part | a quest on that ask, in its repository and lane, with its own line (`SessionLines`, `from`) and its own workflow run *(Codex)* |
| The team | a membership record (§2.2) and a parent projection read from it; it stores no state of the parts' own *(Codex)* |
| The lead | a session holding the `plan` work on the ask, started as the intake is (`Loop/DriverIntakes.cs`) *(Fable)* |
| A scout | a session holding the `orient` work, answering once with a pack, as a helper answers *(Fable)* |
| A worker | a driven quest session under `isolate`, staffed by the part's weight (§2.1) |
| A reviewer | the `opinion` node, another maker's reading before the landing (workflows rule 26) *(both)* |
| The integrator | the line's integration queue (`IntegrationRequest`, `IntegrationCandidate`, `IntegrationPlans`, `IntegrationReplays`, `IntegrationPromotes`, `IntegrationVerdicts`): Daoris's own code, no model *(Codex; Fable)* |
| The supervisor | `SupervisorLook` and `Loop/Supervise/` with a typed team subject (§9), woken by events *(Codex)* |

**Each part keeps its own line and run.** `SessionLines.OfNewAsync` and a record's `from` describe a conversation's continuation;
two parallel parts are two conversations, so they are never given one continuation identity *(Codex)*. The lines of one ask in
one repository already share an entry key (`<origin>/<repository>`, driving rule 46); the projection reads that key and the
membership, and joins nothing.

**Two gaps in the quest records** *(Codex)*: `Quest.Then` and `Parent` express succession, not parallel parts with a join; and
`AskPublishGuard` knows a receiver by repository and lanes, so two parts addressed to one repository and lane would read as one
receiver (driving rule 44: an ask asks each receiver once). A part is published under a key `(ask, part)` carried through
`AskQuests` and `QuestExchange`: a retried publish returns the same quest, and a revised description changes that quest rather
than making another. Chains stay for work that is truly sequential.

**The lead's door is every session's** *(Fable)*. `team_plan` is a connector tool served to every caller (ACCESS1h): it writes a
plan revision for an ask or, from inside a part, a sub-plan of that part with the caller as its lead. A worker that finds its part
is really three splits it itself. Depth and revisions are counts the supervisor reads (§9), never a cap in code.

### 2.1 The roles and their staffing

Two works join `RoleWork` (roles rule 2): `plan` and `orient`. `review` and the supervisor exist. Each has a seeded machine-wide
role edited through `RoleDoor`; any agent and model fills any of them through `role_staffing`, walked by `StartPlan` as every role
is, and the tier that ran is said (roles rule 20) *(Codex)*.

| Role | Work | Seed staffing, the person's to change | What it is handed |
|---|---|---|---|
| Lead | `plan` (falls to Design's role when removed) | the design tier | the ask's words and requirements, the areas' table of contents, the index, the lanes, the measured part sizes *(Fable)* |
| Scout | `orient` | the cheapest exact tier; two choices of different makers | one part and the plan; answers a pack (§2.2) *(Fable)* |
| Worker | `build`, `design`, `exact`, unchanged | by the part's weight: `exact` to Exact build, `settled` to Build, `judgement` to Design *(Fable)* | the brief composed from the pack by code, as the hand-over brief is |
| Reviewer | `review`, through the `opinion` node | another maker than the part's worker where one is signed in *(both)* | the part's submission, its review record and its pack |
| Integrator | none: Daoris's code | — | the train (§7) |
| Supervisor | supervisor kind `team` | rules first; an agent holder where the person appoints one | the plan, the counts, the verdicts (§9) |

- **A role is a brief and extra tools, never a smaller toolbox** *(Fable)*. A scout is told to change nothing and works in a
  clone, so its edits go nowhere; nothing denies them. Where a workflow wants a read-only step it declares the posture (ACCESS1l),
  never a role name *(Codex)*.
- **Independence is said, never required** *(both)*. A review by the worker's own maker runs and says `not-independent`; the worker
  and its reviewer are always two sessions.
- **The integrator is not an agent** *(Fable)*. Ordering, merging, regenerating and writing records are deterministic; a model there
  adds a failure mode and buys nothing. A code conflict goes back to the part's worker (§7.3), so no conflict-resolving agent is
  staffed.
- **A harness's own subagents stay the agent's** *(Fable)*. A worker may fan out inside its part; Daoris neither counts nor forbids
  it, *At work* lists it, and the measurement records it (§10) *(Codex)*.

### 2.2 The records

All portable records live beside the service's ask and quest records, written through one service door with the revision read
compared in its write, idempotent across a crash, through the existing operation and replay machinery, never by an agent's direct
write *(Codex)*. They carry repository-relative references and commit ids, never a machine path. Trees, process ids, accounts,
resource readings, raw logs and the verdict store stay in the desktop's store.

| Record | What it holds |
|---|---|
| `TeamPlan` | the ask, `revision`, `by` (session and role), `base` (the line's commit it was read at), `why` (one paragraph), the requirements each part covers, `parts`, `edges`, `reservations`, `estimate` (wall minutes, tokens, check seconds). Versioned whole like a workflow file: a revision is never edited, a re-plan appends the next *(Fable; Codex)* |
| `TeamPart` | a stable `id` across revisions, its `parent` part where a split made it, the repository and lane, `outcome` (what a person sees working, one sentence), `weight`, the ask's requirements it serves, quoted *(Codex)*, `claims` (path globs it will change), `reads` (contracts it depends on), `after`, `proof` (one line per point, each naming its check or test), `size` (tool calls and check seconds expected), its workflow and its quest *(Fable)* |
| `TeamMember` | one row per session that serves the team: ask, part, role, session, line, plan revision. The team is these rows |
| `PartPack` | one per part and revision: `area`, `knowledge`, `change[]`, `mirror`, `tests` with their filter, `words` read by suites the worker will not run, `lanes`, `generated` outputs, `shared` paths expected, `unknowns`; each pointer a `file:range` with the blob id it was read at; `reached`, the gates its claims reach, computed by Daoris *(Fable; Codex)* |
| `PartSubmission` | one per hand-back, numbered: §5 |
| `TeamDecision` | the supervisor's subject, facts read, plan revision, decision, reason, author and resulting act, kept as today's decision files are *(Codex)* |

**Edges are of two kinds, written on the plan with their reason.** `after` is a build dependency: the other part's commits are this
one's base, at a milestone of `oriented`, `submitted` or `landed`, `landed` by default; a dependency on a submission names its
exact commit, and a later change of it invalidates what was built on it *(Codex)*. `orders` is a landing order only: the two share a
file and either order works, one at a time *(Fable)*. Independent parts build together even where one lands first *(Codex)*. The
write flags a missing reference and a cycle.

**Reservations** generalise the reserved decision number: a repository declares its numbered resources once (a decision file's
pattern, a migration's number) and the plan hands each part its numbers *(Fable)*.

**A pack is stale by pointer** *(Fable)*. When the line moves, Daoris compares each pointer's blob id and marks only the pointers
that moved; a scout is asked again for those alone.

**The parent projection** reads the membership, the quests, the sessions, the runs, the review records and the integration
requests, through one new source in `Exchanges.Of`, and stores no status of its own, so no second state machine can disagree with
the records it reads *(Codex; Fable)*.

## 3. Cap checks, not agents (decision 2)

### 3.1 The check broker

`check_run`, a connector tool, asks the running Daoris to run a declared gate or a filtered unit in the caller's tree; it is
`RUN_HEAVY`'s door, which the terminal's `daoris gate run` already uses, given a ticket: the call answers at once with a position,
and the verdict arrives as a solicited answer *(Fable)*. It runs under the machine's existing admission (`MachineAdmission`,
`HeavySlot`, `HeavyExecutor`, `HeavyAppRun`, with `tools/machine-admission.mjs` and its fixture kept in agreement) *(Codex)*:

- **one expensive build or test at a time on this machine to start** *(Codex)*, set as the admission's capacity here, raised only
  after measuring finished-work throughput, memory and stable check times; the gates not marked heavy run beside;
- a clock-bound gate never beside a heavy one, as the merge tool learned *(Fable)*;
- workers, verification, integration and a CI run beside all count in the one admission: five builders at 91% CPU leave no room to
  treat a CI run as free *(Codex)*;
- the broker orders its own tickets, the train's first, then hand-backs, then a worker's red-first runs *(Fable)*; between the
  broker and the merge tool the admission's first come, first served stands, so neither starves.

**A worker's turn ends while its check waits** *(Fable)*: the session ends with `ended.awaits-check`, the twin of
`ended.awaits-helper` (`SessionHolders`, driving rule 40), worded in both catalogues, and the verdict carries it on in the same
conversation. A six-minute suite holds no context open and burns no tokens.

An agent may still run `dotnet test` itself: the broker is faster and cheaper, which is the only incentive it needs *(Fable)*. No
agent command is intercepted or denied for being heavy *(Codex)*; the heavy-gate deny under `light` stays the person's workflow
choice it already is.

### 3.2 The team's pace

**Two active workers by default, three at most on this machine** *(Codex)*: a setting on the ask or its place that the person
changes, and a pace, never a deny. A ready part past it is pending, said with the pace and where it is changed (D284's *Paces*).
Scouts, reviews and the lead's revisions use no CPU here and do not count *(both)*. The supervisor's count of check-queue wait
against check duration says when another worker would only wait *(Fable)*; it moves the pace only through the person or the
appointed supervisor's act.

### 3.3 The warm build base

Reuse at four levels *(Codex)*:

| Work | Reuse |
|---|---|
| Dependencies | `DependencyStore` and `DependencyKeys`, keyed by lockfiles, install commands, toolchain, platform and environment |
| Compilation | one build per unchanged input set, kept as the line commit's build base (`.daoris-base`, `tools/tree-base.mjs`) and copied into each part's tree when it is set up, beside `work/deps/<key>`: compiled outputs, the web's incremental typecheck state, the generated index *(Fable)*; filtered tests then run with `--no-build` |
| Generated contracts | generated once for the candidate, reused while their inputs stand |
| Verification | the verdict store (§4) |

Each tree owns its copy: outputs are copied, never linked into another checkout (D3), and each tree keeps its own `bin`, `obj`,
test output and incremental state *(Codex)*. This repository's builders set up through `tools/worktree-setup.mjs` and
`tools/merge-branch/dependencies.mjs`. A worker's first build compiles its own change, not the repository *(Fable)*.

### 3.4 One prompt prefix

Every brief of one plan revision begins with the same bytes (the ask, the plan, the keel's contract) before the part's own pack,
so a provider's prompt cache serves the second worker onward *(Fable)*.

The figure to hold this to: a 45-to-123-minute part is 5 to 30 minutes of thought; with the warm base and replayed verdicts its
share inside tools should fall below half *(Fable)*.

## 4. One verdict store for the machine (decision 3)

A verdict is keyed by **gate**, the **digest of the exact inputs that reach it**, its **configuration** (its declared command and
environment, the declaration's blob for that gate) and the **toolchain** (SDKs, Node, the dependency keys), and kept with its log
in the machine's store *(Codex; Fable)*. How a verdict given on one tree stands for another is the merge tool's existing contract
(`verdictStanding` in `tools/merge-branch.mjs`):

- **exact** first: the same inputs, the same configuration, the same toolchain *(Codex)*;
- **records**: the trees differ only by records the integrator writes after the gates;
- **unreached**: every other differing path is one the gate cannot see by the reach table (`pathsReaching`, where a path no rule
  places reaches every gate), with a Process half's kept classes bounded by its sweep (`SWEEP_BOUND_MS`);
- anything else is **stale**, naming the paths that made it so.

Reach-based reuse goes no further than that contract *(Codex)*. A changed configuration, an unknown input or a changed toolchain
voids a verdict, and **a pass is never reused because the command's text matches** *(Codex)*. A verdict records the reach table's
digest, and a replayed verdict that a full run contradicts is logged as a defect of the table *(Fable)*.

It is one store for the machine: the driver's integration, every tree's broker run and this repository's merge tool read and write
it, through one format held by a shared fixture as the admission's twins are. The merge tool's own `local/gate-verdicts.json` goes
in the row that moves it (§11 row b). Consequences *(Fable)*: a worker never runs a gate its diff cannot reach, because the line's
verdict stands for it; two parts on one base share theirs; **the integrator replays standing verdicts**, so a batch of parts with
disjoint reach costs little. The ask's final acceptance replays exact verdicts only, and the full set before an install stays where
it is (`--passed`).

## 5. The hand-back, observed (decision 4)

A worker ends its part with `part_handback`: the quest's `ready` (driving rule 7) with a shape, which writes a numbered
`PartSubmission` *(Fable; Codex)*. The closing note stays the surviving account of the work (`QuestHandbacks`) *(Codex)*.

**What the worker says.** The outcome requirement by requirement, with any departure; the shape in a paragraph and the decision
taken; what a reviewer should look at; the tests added and which failed first; what was left out and why; its record intents; each
proof point mapped to its test *(Fable; Codex)*.

**What Daoris observes**, written beside it and never taken from the worker's word *(Fable; Codex)*:

1. **Base.** The tip contains the train's current tip (the line plus the parts landed). Where it does not, Daoris says so; a clean
   merge with no overlap on claimed paths it makes itself in the part's tree.
2. **Touched against claimed.** The changed paths from git. Paths beyond the claims are no error: they update the plan's edges.
3. **Checks.** The reached set for the touched paths, each with its verdict in the store at this tip's inputs (§4). **A missing
   verdict is run by Daoris**, through the broker (§3.1), and the submission stands *checking*, the worker's turn ended
   (`ended.awaits-check`); it is never refused for it (D284). A red reopens the worker with the log's tail as a solicited answer.
4. **Builds.** The builds the reached set contains compile clean.
5. **Leftovers.** Each process the part's run started is tracked by run, process id and creation time *(Codex)* and stopped at the
   part's end; a process it did not start is named, never stopped. The tree stays until the part lands, then the run's `release`
   takes it (workflows rule 31b); a tree kept has its reason shown *(Codex)*.
6. **Records untouched.** A diff on a record path is lifted into an intent where it reads as one, else shown to the lead.

A submission is *ready* when 1 to 6 hold. Its receipt binds the candidate tree, the source tip, the base, the units selected, the
commands, the toolchain, the configuration and the outcomes; a clean branch alone proves nothing of its merge *(Codex)*.

**The checks are the landing's own selection** *(Codex)*. Preflight and landing choose by one selection per landing path: in the
product the integration's plan over the declared gates' reach (`claimed.checks` with `reach: paths`, workflows rules 21 and 40)
*(Fable)*; in this repository's steward cycle the merge tool's (`selectGates`, `pathsReaching` in `tools/merge-branch/reach.mjs`,
`scopedSet` in `tools/merge-queue.mjs`, the scheduling in `tools/merge-branch/plan.mjs`). There is no second list of worker checks.
The two meanings of "light" today (a merge's selection, and the install's `--passed --light`) get names of their own *(Codex)*.

This is the measured fix, five red merges of six becoming none once builders ran the merge's own lane, made impossible to forget
rather than required *(Fable)*.

## 6. Conflicts (decision 5)

**Whether to split is the lead's first answer** *(Fable)*. One part is a valid plan. A team costs a lead, a scout and a review per
part; it pays where three or more parts are ready at once or the task exceeds one worker's context, and the lead is handed that
rule and the machine's numbers.

**The unit is a feature a person can see working**, never a layer: a host half without its page half shows nothing working *(both;
the dispatch skill's rule)*. The first size targets, re-fitted from the records: about 150 tool calls, one worker's context, and
claims that reach at most two heavy suites *(Fable)*; a reviewable candidate in about an hour on this machine *(Codex)*. They are
estimates, never timeouts; a part that reaches every gate says the split is along the wrong seam.

### 6.1 The keel part

Where several parts need one contract (a route's shape, an interface, a catalogue's keys, a composition root's wiring), the lead
writes a first part that lands only that: the types, stubs that throw, the registration lines, the generated contract. It is
small, of `judgement` weight, reviewed hardest, and every part that needs it is `after` it. A hub stops being a shared file because
it is written once, first *(Fable; Codex's "interface first")*.

### 6.2 The split judge

A pure judge in the service beside the plan *(Fable)*, given the claims:

- adds an `orders` edge between two parallel parts claiming one path, and says so on the plan;
- strips from every claim the paths the repository declares generated and the records `daoris.json` declares (the backlog, the
  archive, the changelog, the fix log, the areas), naming the integrator as their writer;
- flags a part whose claims reach every gate, and a plan whose widest set of parallel parts exceeds the check capacity (§3).

It refuses nothing: a plan with flags is saved with them, and the supervisor reads the flags.

### 6.3 Claims are soft

Claims ride what exists: `isolate`'s `overlap: wait` reads a quest's declared paths at its first start (driving rule 14), and
during the work Daoris reads each tree's changed paths (`TreeChangesKept`) against the other parts' claims *(Fable)*. A worker
touching a path another part claims is told once, at ordinary level, who holds it and that the integrator will order the two; the
plan gains the `orders` edge. The worker decides whether to go on, post to that part (co-working rule 12) or leave the file. A
path a worker needs that no claim names is reported as an expansion, and the lead updates the order *(Codex)*. Nothing is locked.

### 6.4 Records are written by the integrator alone

Workers never edit the records. They send **record intents** with their hand-back, and the integrator applies them one at a time,
in train order, against the current text of the candidate, before the batch's checks run, so an area's sentence lands with the
behaviour it describes *(Fable; Codex's condition)*.

| Kind | Who writes it | How |
|---|---|---|
| The backlog | the integrator | by rows (workflows rule 58), from the intent `{row, outcome}`; a hand-back proposes no row text |
| Archive, changelog, fix log | the integrator | from the intents, appended in train order |
| Area documents | the integrator | intents addressed `{area, rule number, new sentence}`; two intents on one rule go to the lead *(Fable)* |
| Generated files (code map, contracts, catalogues' derived files) | the integrator | regenerated from the assembled candidate; `docs/index/` stays each checkout's own *(both)* |
| Decisions | the worker | a new file at its reserved number, so nothing conflicts *(Fable)* |
| Lockfiles | the keel part, or a part of its own | the judge orders any two that claim one *(Fable)* |

Addressing an area intent to a rule number instead of a text hunk removes the one conflict today's cycle still has *(Fable)*.
`tools/merge-backlog.mjs` and the row-id merge stay as recovery *(Codex)*. In this repository a canon change still starts in
`canon/` and syncs this repository and both examples.

## 7. Integration is a train (decision 6)

### 7.1 The workflows

Each part has its own run; no graph expands every part's steps into one, which would pass the graph's 24-node bound *(Codex)*.
A shipped pair does the work with existing kinds and one new one, `parts`, under `Workflows/Kinds/Parts/` through `NodeKit`
*(Fable; Codex)*:

- **`team-part`**, each part's run: `isolate` → `claimed` (checks: reached) → `opinion` → `land` (method `integrate`, into the ask's
  candidate) → `release`. It holds no `review`: a part lands with no press of the person's (workflows rule 22).
- **The ask's delivery run**, one per repository the ask's plan reaches, bound when the plan's first revision is saved: `parts`
  (reaches once every required part of the standing revision landed or was dropped by a revision) → `check` (the final acceptance:
  the ask's whole change, reached, exact replays only) → `review` (the ask's one Accept, workflows rule 24, or the person's through
  words, rule 23) → `land` → `release`. A run keyed by the ask rather than by a chain's first quest (rule 15), and a `check` in a
  development graph (rule 11 has it in verification pipelines), are the gaps row f fills.

A workflow author who wants a person's yes on the plan adds a `human` approval before the parts start; none is seeded (D284 point
3). A repository that lands each batch straight on its line (this repository's main is its development branch) chooses a
`team-part` version whose `land` integrates into the line and a delivery run with no branch of its own: workflow versions, never a
code path *(Fable)*.

### 7.2 Review per part

The `opinion` node as it is, another maker's reading with findings sent to the worker once, with these additions:

- the reviewer reads the **pack** beside the diff, so it judges against the contract and the exemplar *(Fable)*;
- it starts when the hand-back is called, **beside** the checks, since it uses no CPU here *(Fable)*;
- its depth follows the weight: an `exact` part a diff read on the exact tier, a keel or `judgement` part the design tier; a finding
  that can be a failing test is given as one (its name, arrangement and expectation), and the worker adds it *(Fable)*;
- the reviewer's findings are not the Accept: the workflow's Accept is answered by its named party (D284 point 3) *(Codex)*;
- it reuses `ReviewRecord`, `RunReviews` and their attempt and revision binding; where record intents change the artifact they are
  in it before the review's record is written, or a new attempt begins *(Codex)*.

A dispute goes to the `landing` supervisor where one is appointed (roles rule 49), else to the person (workflows rule 26).

### 7.3 The train

The integrator is the line's integration queue, extended, never a queue of the team's own *(Codex)*:

1. A part's `land` makes an `IntegrationRequest` carrying its ask and part; the queue's eligibility reads the plan's `after` edges
   and its order the `orders` edges: topological, ties by fewest reached gates *(Fable; Codex)*.
2. The ask's branch, `daoris/ask-<id>`, is a line the integration runs on, one candidate at a time as on every line. For each
   batch of ready parts the run forms its `IntegrationCandidate` and replays the members onto it (`IntegrationReplays`): the line's
   tip merged in first, so the ask's branch never drifts more than one train, then the batch in order, kept on
   `refs/daoris/train/<ask>/<n>`; generated files regenerated, declared rows settled as `IntegrationPlans` settles them, record
   intents applied (§6.4) *(Fable; Codex)*.
3. **One reached union per batch**, judged by `IntegrationVerdicts`, every standing verdict replayed from the store (§4) instead of
   run again.
4. Green: `IntegrationPromotes` moves the ask's branch to the candidate's verified tip, and each part's landed commit is recorded.
   Red: the failure is confirmed once before it is blamed (workflows rule 39), then the bisect reads the judgement part by part
   (`IntegrationVerdicts` already judges a candidate's members as prefixes) to name the part that broke it; that part goes back to
   its worker with the candidate and the log (rule 34 carries a session on for a refusal it can clear), and the others land
   *(Fable; Codex)*.
5. A conflict of meaning goes back to the responsible worker with the candidate and its diagnostics; the integrator never rewrites
   a reviewed part *(Codex)*.

A part is *landed* when it is on the ask's branch with its batch green; *built* when its submission is ready. **The ask is delivered
only when its required parts have landed and its final acceptance checks pass.** A part's quest Done is the worker's completion
record and is not the team's delivery *(Codex)*.

## 8. The person sees one item (decision 7)

**One row in Sessions**: the ask, its state read as the line's state is (needs you, working, pending, from the first part in it,
driving rule 45). Opening it shows, on `AtWorkView` and `AtWorkAskView` over the projection's source in `Exchanges.Of`, with each
part's own conversation still addressable inside it *(Codex)*:

- **its conversation**: the lead's updates, each plan revision with what changed and why (a revision a diff away), the
  integrator's trains and the delivery's result;
- **a parts table** *(Codex)*:

  | Part | Role | State | Waiting on | Evidence |
  |---|---|---|---|---|
  | Contract | worker · the agent and model that ran | Landed | — | commit, review |
  | Service behaviour | worker | Checking | the check queue, position 2 | the submission, the check running |
  | Desktop behaviour | worker | Changes asked | review findings | the submission |
  | End to end | integrator | Pending | service and desktop behaviour | the checks planned |

  Landed and built are counted apart (*2 landed, 1 checking, 1 changes asked*), never as an invented percentage *(Codex)*;
- **what it costs**: tokens by role and model, check seconds run against replayed, the estimate it started with *(Fable)*.

**Steering** *(Codex; Fable)*: the target, the priority, the order, the staffing and the concurrency (the pace); words to everyone on
the ask's channel, which the lead turns into a revision, or to one part (co-working rule 12); per part *Pause*, *Start first*,
*Change who builds it…* (the role door, scoped to the part) and *Drop…*; for the ask *Stop…*. Each word shows whether it was held,
delivered or taken *(Codex)*. The terminal mirrors it: `daoris ask plan <id> [--revision n]`, `daoris ask parts <id>`,
`daoris part <id> pause|first|drop` *(Fable)*.

**Questions and approvals go through the existing obligation keys** (`NeedsAPerson`, `AttentionStateSlice`), so the item, the bell,
*What needs you* and the part's own session count one wait once *(Codex)*. A decision an agent took shows as its decision, with the
person able to step in.

Chrome is worded in both catalogues, English and Chinese; part names, requirements, plans and agents' prose are authored content
and stay as written (D222).

## 9. Supervision (decision 8)

**Woken by events, never by polling** *(both)*: a submission, a verdict, a review finding, a failed check, a dependency change, a
question, a limit, a process's exit, a claim collision, an estimate passed. The facts are collected by code; the judgement is the
supervisor's *(Codex)*. It is `SupervisorLook`, `Loop/Supervise/`, the decision records and the kind resolver, with a typed `team`
subject holding the ask, the plan revision and the event mark it read, so a late decision never applies to a newer plan without
reading it again *(Codex)*.

**Counts**, new rows in `Course/Domain/LineCounts.cs` read through `SupervisorRuleBook` *(Fable)*: a part's elapsed time over its
size's p90; bounces on one part; reds at the train attributed to a part; check seconds over the estimate; pack pointers gone stale;
plan revisions in 24 hours; ready parts waiting with capacity free; check-queue wait over check duration; team depth.

**Acts are re-plans through the lead's door** (`team_plan`), the door every session has *(Fable)*: split a broad part; join parts
whose setup and checks outweigh their parallelism; move independent work ahead of a blocked part; refresh a stale pack; ask for an
interface-first keel; move a part's next start to another choice or a heavier weight (never mid-turn, roles rule 29); start a repair,
or ask another repository by quest; change the pace; drop a part *(Codex; Fable)*.

- **A revision records what changed and why**, keeps every part's id and its submissions, and splits a part into linked children,
  never erasing one *(Codex)*.
- A running part is left alone unless the revision changes its claims or outcome; then its worker is told at urgent level, since
  that is a fact that makes the work in flight wrong (co-working rule 4) *(Fable)*. Ordinary re-planning interrupts no turn.
- A dropped part's branch is kept on a recovery ref *(Fable)*.
- A decision applies at once and is said on the line; the person's *Overrule* makes the next revision, restoring the one before.
  There is no default hold before it applies and no seeded rule (D284 point 6); Fable's ladder (two bounces raise a part, a third
  asks for a split, three revisions in a day ask the person) is a preset the person may switch on.

**It goes to the person only for a decision the evidence cannot supply** *(Codex)*: requirements that conflict (a revision that
would drop, weaken or add an outcome against the ask's quoted requirements is one *(Fable)*), a product trade-off, a budget (an
estimate past a bound the person set; none is seeded), an owner no record settles, or a workflow step that names the person. It is
one row of *What needs you* saying what it is, what yes does and where to look. Everything else the team settles and says on the
line. A restricted harness's prompt is ACCESS1's one approval request to the place's approver, nothing of the team's.

**A long build is a fact to investigate, never a refusal** *(Codex)*: its output, the processes it owns, its place in the admission
and comparable durations. No elapsed time becomes a cancellation or a new approval. D284 holds throughout: no act is reserved to
the person and nothing is held by default.

**The watch's latency comes first** *(Codex)*: the measured 4.3-minute pass deserves attention before more supervision rides on it.
`Loop/Watch/WatchRun.cs` and its wakes carry the events; model turns, builds and checks stay off the pass; event-to-action latency
is measured (§10), or the team spends its parallelism waiting for its coordinator.

## 10. Measurement (decision 9)

`tools/dev-metrics.mjs` is extended, never replaced by a dashboard of other definitions *(Codex)*, with an `--ask <id>` view, and
the same figures are written on the ask's record at its end *(Fable)*. Every event carries its **causal ids**: ask, part, plan
revision, session, submission, check, review and candidate.

| Measure | What it shows |
|---|---|
| Ask to delivered, median and p90; each part's own | whether the team helps the person |
| Ready to started | scheduling and account delay |
| Time by kind: thinking, inside tools, queued for a check, waiting on review, waiting on the person | where time goes |
| Hand-back to landed; landed to staged | the delivery gap DEVCYCLE1 still measures |
| First-integration pass rate, per unique submission | whether hand-backs predict landing |
| Rework: bounces, reds attributed to a part and its model, review findings kept, fixes to the ask's paths within seven days | quality and deferred cost |
| Conflicts by path and kind; claim collisions; stale pointers; revisions | the plan's boundaries |
| Cache hits: check seconds run against replayed, builds reused | the reuse actually won |
| Tokens and cost by role and the model that ran; the lead's, scouts', reviewers' and supervisor's share | the team's overhead |
| Leftovers: processes Daoris stopped, trees kept with their reason | lifecycle hygiene |
| Event to next action; the watch pass's duration | the driver's responsiveness |

Tool intervals are counted without double-counting overlapping calls; a check's failure, an infrastructure failure and a check never
started are told apart; a missing source says *not recorded*, never zero *(Codex)*. Today's queue totals are not a first-pass rate
per unique submission, and a landing's `goneAt` is an indirect delivery clock: explicit attempt, promotion and staging events come
before any improvement is claimed *(Codex)*.

**The race** *(Fable)*: the same ask at the same line commit is also given to one agent, a session of the Build role in its own
tree, which never lands. Both end at the same final acceptance. A reviewer of another maker reads the two results blind against
the ask's requirements. **The bench** *(Fable)*: five finished rows of this repository, each replayed at its parent commit with its
landed tests as the hidden proof, team and single agent twice each. The standing baseline is today's steward cycle.

The comparison is fair or it is not reported *(Codex)*: the same request and starting commit, the same acceptance tests, access
mode, hardware and delivery endpoint; cold and warm caches labelled; neither trial's solution or transcript reaching the other; the
run order varied and repeated over several asks; the single agent at its harness's normal reach, with its native delegation recorded
rather than disabled. A speedup is never read from overlapping worker clocks, and counts only if it survives review, integration and
rework. **The headline is cost and wall time per accepted requirement**, with the break-even said: how many ready-at-once parts make
the team win on this machine *(Fable)*.

## 11. Rows, in order (decision 10)

Each row is useful alone and lands with its proof, filtered to its own tests, each seen failing first, with `npm run quick`, and
where it changes words, `i18n:check` and `names:check -- --strict` in both languages. Each rewrites, in the same chunk, the area
sentences its change makes untrue (D228). Rows a to c help a single agent with no team at all *(Fable)*; d onward build the team.

| Row (lanes; after) | What the person sees working | Proof | What it removes |
|---|---|---|---|
| **a** the observed hand-back and the check broker (driver, service, modules, web, tools; first) | A quest's hand-back says what Daoris read: its base, its paths, its reached checks with their verdicts, what it left running; a missing check runs by itself under the admission while the session's turn has ended, and the session goes on with the verdict in the same conversation; what its run started is stopped at its end | A solo quest whose landing passes first try with no steward rule *(Fable)*; the hand-back and the landing choose the same checks for one diff, and a receipt for another tree is refused *(Codex)*; `ended.awaits-check` ends a turn and the verdict resumes it; two trees' heavy checks run one at a time, light ones beside; a process the run started is stopped at its end, one it did not start is named | The dispatch skill's big-scope exception (D180's note of 2026-11-04); the steward's untracked hand-back rule; the builder's words on build-server shutdown that could stop another tree's server *(Codex)* |
| **b** the verdict store (driver, tools; a) | A check whose inputs did not change is replayed and says from which tree and when; every tree, the integration and the merge tool read one store | The second run of an unchanged gate is a replay; a `records` or `unreached` replay only where `verdictStanding` gives it; a changed toolchain, configuration or declaration voids it; a matching command over other inputs never replays; five trees on one base run each heavy gate once | The merge tool's `local/gate-verdicts.json` as a store of its own |
| **c** the warm build base (driver, tools; b) | A new part's tree builds only its change | A fresh tree's first build time against today's, measured; filtered tests with `--no-build` after one build; contracts generated once per input set; two trees never share a writable output | Each tree's cold build |
| **d** the team records and the lead's plan door (service, driver, modules, web; a, ACCESS1h) | An ask's plan with its parts, edges and packs, written by the lead, by Ask Daoris or by hand, each part a quest; a worker splits its own part | Under `(ask, part)` a retried publish returns the same quest and two parts on one repository and lane publish two; a cycle, a missing reference and a claim on a record or generated path are flagged and saved; a moved pointer marks its pack stale alone; a worker's split counts depth 2; this repository's steward runs one batch from a plan instead of the skill's briefs | The skill's hand-written briefs for a planned batch, and its five workers for a team |
| **e** the supervisor's team subject and re-plan (driver, modules; d) | A part that bounces, stalls or collides is re-planned on the line, each revision saying what changed and why; the person is asked only for one of §9's reasons | Woken by a submission, a verdict and a collision, and never by a pass that found nothing; a decision against an older revision reads again; a restart recovers; a split keeps the id and links its children; the person's *Overrule* restores | — |
| **f** the integrator's train (driver, modules; b, d) | Ready parts land on the ask's branch in batches, one reached run each; a red goes back to the part that caused it while the rest land; the ask is delivered after its final acceptance and its one Accept | Three parts land in order; a red is bisected to its part; a conflict goes to its worker untouched; a part's Done before its landing delivers nothing; the `parts` kind and the ask's delivery run | — (the queue extended, no second queue) |
| **g** the person's view (web, modules; d, f) | One item with its conversation, its parts table, its costs and its steering | Component tests and stories in both catalogues; one question counted once across the item, the part's session and *What needs you*; landed and built counted apart; a word to one part shown held, delivered and taken | — |
| **h** record intents (driver, tools; f) | Workers' backlog, archive, changelog and area changes land with their parts, with no conflict | Three parts' intents on the backlog, the archive, the changelog and one area rule applied in train order; two intents on one rule go to the lead; a worker's edit of a record is lifted into an intent | The branch-worker's record instructions; area documents conflicting at merges |
| **i** the race and the bench (tools, driver; a-h) | An ask's report against one agent, per accepted requirement | One race reported blind with cost and wall time per accepted requirement; the bench's five rows twice each, cold and warm labelled | — |
| **z** the installed proof (the steward; all) | A real large ask in this repository delivered by a mixed team of Claude Code and Codex through the installed window | Dated shots in both languages of the item, its parts and its steering, through a restart and the clean-up; the machine log's lines; `dev-metrics --ask` for that ask | — |

## 12. What is removed (D228: one way)

Each goes in the row that replaces it, with its words, tests and area sentences.

- The dispatch skill's big-scope exception: builders handing back with no check run and merges carrying no gates (a). Deferred full
  checks stay owed explicitly *(Codex)*.
- The hand-back's own word for which checks ran, wherever a brief asks for it: the submission's observation replaces it (a).
- Blanket build-server shutdown guidance where it could stop another tree's server (a) *(Codex)*.
- `local/gate-verdicts.json` as a second store (b).
- The skill's hand-written briefs for a batch that has a plan, and its *at most five workers* for a team, which the pace replaces (d).
- Builders writing the backlog, the archive, the changelog and area documents (h).

## 13. Rejected

- **A team engine beside the workflow runtime**, with a scheduler and a landing queue of its own *(both)*: two ways to move work
  (D228); the runtime already binds, waits, lands and releases, and the integration queue already orders and gates.
- **Parallel parts as one quest chain, or one continuation identity for them** *(Codex)*: it conflates independent trees with
  successor work and cannot express a join.
- **Today's partner pieces unchanged** *(Codex)*: their lead-merge lifecycle has no independent review and no ordered integration.
- **Capping agents instead of checks**: a fixed worker count limits agents for a cost only checks have, and stays wrong as caches
  warm *(Fable)*; five workers with a CI beside measured saturated and slower *(Codex)*.
- **Every worker running the full suite** *(Codex)*: it repeats the dominant cost and adds contention.
- **Checks only once the whole task is assembled, and review after integration only** *(both)*: late discovery of what a worker
  could have caught, and a finding then costs a train.
- **One writable dependency or build folder shared by trees** *(Codex)*: trees corrupt each other's inputs and outputs.
- **A self-reported hand-back**: the word "I ran the checks" is what 5 of 6 red merges had; Daoris reads what it can read.
- **Path locks that refuse an edit** *(Fable)*: narrowing (D284), and wrong, since a collision is information for the order.
- **Role-shaped tool allow-lists, or a fixed maker per role** *(both)*: D284 retired refusals by kind of work; staffing is the
  person's and moves with measurement.
- **A frozen plan approved up front, or the supervisor's approval before every act** *(both)*: the person would answer before
  anything is known, and D284 removes the approval layer; a workflow's author may still add a step.
- **The harness's own subagents as the team** *(Fable)*: one maker, no records, no review by another, invisible to the person.
- **Workers writing the records, merged cleverly** *(both)*: measured, conflicts at most merges until they stopped.
- **An agent integrator for every merge** *(Fable)*: tokens for deterministic work, and a model's slip lands on the line.
- **The person's Accept per part** *(Fable)*: the person sees one item; the ask's delivery is where their Accept stands.
- **Splitting by layer** *(Fable)*: a layer alone shows nothing working.
- **A polling supervisor** *(Fable)*: 4.3 minutes a pass, most finding nothing.
- **Cancelling after a duration** *(Codex)*: it confuses expensive work with stalled work.
- **One graph holding every part's steps** *(Codex)*: it duplicates per-part runs and passes the graph's bound.
- **Reusing a pass because the command's text matches** *(Codex)*: the inputs, configuration and toolchain are the verdict's key.
- **Landing every part straight on the line as the only way** *(Fable)*: a default that moves the person's line before their Accept;
  kept as a workflow version for a repository whose line is its development branch.

## 14. Risks

- **A bad split costs more than no split** *(Fable)*, and dependency boundaries can be wrong *(Codex)*. One part is a valid plan; the
  keel lands first; the race and the bench say when a team loses; the plan's counts feed the lead's next plan.
- **A wrong reach table gives a false green from the store** *(Fable)*, and evidence reuse can be unsound *(Codex)*. Reuse starts
  exact, reach-based reuse goes no further than the merge tool's contract, a verdict records the table's digest, the final
  acceptance replays exact verdicts only, and the full set before an install stays.
- **Coordination overhead** *(both)*: the lead, scouts and reviewers could be a third of a small ask's tokens. Depth follows weight,
  packs carry across revisions, the shared prefix caches, and the overhead is reported per ask.
- **A stale orientation** *(Codex)*: packs are stale by pointer and asked again for what moved.
- **The ask's branch drifts** on a long task *(Fable)*: each train merges the line first, and the supervisor counts line commits not
  yet merged.
- **Re-plan thrash** *(Fable)*: revisions are a count, and running parts are stable by id.
- **No second maker available** (signed out, or at a spend cap) *(Fable)*: the review runs on the same maker, says
  `not-independent`, and blocks nothing.
- **Observation is only as good as the job and git** *(Fable)*: a process started outside the run's job is named, not stopped.
- **Soft claims still collide** when two workers ignore the word *(Fable)*: one ordered merge and perhaps a conflict sent back, the
  price of not narrowing, and counted.
- **The integrator's code writes the person's records** *(Fable)*: only declared record paths, only on the ask's candidate, under
  the floor (D284 point 7).
- **The broker's order could starve a class of check** *(Codex)*: row a proves starvation, cancellation and a killed holder.
- **Supervision latency** *(Codex)*: §9's watch latency is measured before more rides on it.
- **Full-access agents work outside the plan** *(Codex)*: Daoris shows those changes, reconciles the edges and voids the evidence
  they touch, and keeps the agent's reach. The team's value is orientation, coordination and delivery, not control.

## 15. Open

- **One reach table.** The product's integration selects by the declared gates' reach; this repository's merge tool by its own
  `REACH` table. §5 keeps one selection per landing path; whether the merge tool comes to read the declaration's reach, so the two
  paths share one table, is left to row b's reading.
- **The store with no install.** The merge tool runs on a machine where no Daoris is installed; where it finds the machine's store
  there (the home's, else the checkout's `local/`) is row b's to settle without making a second store.
- **This repository's default.** Whether its own teams land each batch on main, as its steward lands builders today, or on the ask's
  branch until the ask's Accept, is the owner's choice of workflow version once row f stands.
- **The admission's setting.** One heavy run at a time is set today through the admission's capacity variable; whether it becomes a
  setting in the window is row a's to say.
- **Not in these rows:** verdicts shared across machines or with a CI through the service as a permitted record, and an ask whose
  parts span several repositories, each repository's part a quest to its own lead *(Fable)*. Both follow once z stands.
- **ACCESS1 first where it is needed** *(Codex)*: row d's door served to every caller leans on ACCESS1h, and the scouts' and
  reviewers' declared postures on ACCESS1l; until they land, the roles area still describes the call table's refusals by work
  (roles rules 53 and 54). No team row adds a temporary permission of its own.
