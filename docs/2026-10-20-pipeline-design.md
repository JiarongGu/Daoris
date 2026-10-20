# Pipelines: a workflow's start node routes each piece of work, and verification is a pipeline of its own (proposed, 2026-10-21; revised after Codex's and Fable's reads)

**Standing:** proposed under D192 (FABRIC-PIPELINE1), revised twice under D191: after Codex's read of 2026-10-21, and after Fable's
read of §2 to §4 the same day, which D191's note asks because the design changes a foundation (a workflow that lands nothing, a node
Daoris performs itself whose job outlives the runtime's pass, a run on a repository's line, a slot per home, and a result contract
with a repository's runner). Every finding of both reads was checked against the code before it was taken (§9). Fable's simpler
shapes are taken and made smaller: a verification run, once bound, runs to its verdict and is never superseded, so the third round
removes the supersede, `settle` and the late-result rules, and answers the check through the performer's reconcile alone. Nothing
here is built; its rows (§7) build it. Nothing in it waits for the owner (D191 point 3): it publishes nothing, spends no account
beyond the development's own runs, and loses no data.

Read for it: D173's direction note of 2026-10-20 (FABRIC-PIPELINE1); D157 and the workflow design §3.2-§3.7 and §4; D190 (point 4
above all) and the cutover design §2, §3, §8, §10 and §11; the performer (FABRIC-PERFORM1, `WorkPerformer.cs`); D180 and its notes
(the CI beside, CIPRIO1 to CIPRIO1e, the HEAVYRUN1 notes, FAMILYFAIL1b to 1d); D115's GATEWT1 and GATESLIM1 notes and
`docs/development.md`'s *Integrate and record*; HEAVYRUN1a's archive entry; D83's BGWAIT and DONEEND1 notes; D188; D154 and the
review environment design §2.3; the verification lanes design (FABRIC-VERIFY1); the UI/UX design §3.5 (D185); D193's parts 7 and 8
(the fabric and verification, as the company's picture states them); `.claude/knowledge/development-documents.md`'s *What may run
unasked is a declaration*. Every statement about today was read from the code at `300626e52`, the second reading's again at
`3307f911f`, and the third's at `ff01a5881`, and names its file and line; none was run.

## 0. The case, and what the design rests on

**The case.** On 2026-10-20 the owner's install drove this repository with six sessions at once (DOGFOOD1). Three driven sessions
ran the whole driver test project inside their session, in the background, held their place in the cap until the 90-minute
timeout ended them, and committed nothing. Meanwhile the parent ran the full set beside by hand after each batch of merges, each
run superseded as main moved on. What held the sessions back was a sentence: the install's standing answer for this repository
hands every session a test rule. A sentence an agent can drop is a workaround for what the process should hold (D161's lesson:
a rule an agent drops belongs in a mechanism). The direction (D173's note): how work moves is configured in the fabric, a
workflow begins at a start node that routes each piece of work into one of several pipelines the person configures, and a
repository keeps development apart from a verification that works like CI.

| Today | Read at |
|---|---|
| A chain's work in a repository follows the first level that names a workflow: the task's choice, the repository's for the task's kind, its default, the workspace's for the kind, its default, then Current | `WorkflowSelection.cs:325-343` |
| A workspace declares its kinds, each with a label and the paths work of that kind keeps to; a repository maps them; a repository's choice replaces its workspace's whole | `WorkflowSelection.cs:183-246`, `:93-95` |
| A run is bound once, keeping the level that chose it, the kind, its label and paths; D190 §8 moves the bind to after the start's tree opens and before its take | `WorkflowRunBinding.cs:12-45`, `WorkflowRunStart.cs:37-80` |
| A kind's paths never choose: they hold a run at its gate only where the kind's workflow lowers the person's part, which `Involvement.Lowers` judges step by step | `WorkflowGate.cs:26`, `:234-271`, `:315` |
| Current's live graphs are chosen by the kind of unit: a branch's from its landing rule, a quest's or an ask's from `workCycles` | `NodeGraphs.cs:705`, `:732-757`, `:771-784` |
| A graph is `{ schemaVersion, nodes, wires, entries, end, cleanup }`; every entry starts; no wire chooses one way among several | `NodeGraphs.cs:28-46`, `NodeWires.cs:7-46` |
| A named workflow's graph holds exactly one `handed` and at most one `opinion` and one `look`, judged in both readers | `WorkflowNamed.cs:248-261`, `namedworkflows.ts:275-288` |
| `after` is satisfied by a source reached or skipped; `data` only by one reached, a skipped source producing no outputs; a node's `skippable` is declared, `true` where unsaid | `NodeWires.cs:9-16`, `WireEvaluation.cs:634`, `NodeGraphs.cs:608` |
| The units are a branch, a quest, an ask and (CUTOVER1a) a run; nothing projects a repository's line as a unit | `WorkUnit.cs:4-20` |
| The built-in kinds hold no `check`; the one built-in Daoris performs itself is `quest.close`; schema 1's `check` is a plugin's and not run, and D190 §10 makes `check`, `stage` and `all` graph nodes only | `NodeContract.cs:11`, `:24-68`, `:431`, `WorkflowNamed.cs:79-84` |
| The pass holds one semaphore through its starts and awaits each; the performer starts a plugin's performed node alone, its attempt written with a deadline of the node's `timeoutSeconds`, else 15 minutes, before it is sent; an answer is applied only while its attempt waits for it | `NodeRuntime.cs:388`, `:543-562`, `WorkPerformer.cs:117-126`, `:248-271`, `:885`, `NodeGraphs.cs:606` |
| `daoris.gates.json` marks a gate `"heavy": true`; `run-heavy` runs the heavy gates in declared order, holding one slot per repository across its trees, ending what a dead holder left, every gate in one named kill-on-close job ended before the slot goes | `HeavyGates.cs:47-94`, `HeavyRun.cs:26-63`, `:196-297`, `HeavySlot.cs:32-53`, `:85-104` |
| A heavy-run slot is a file under its home's `locks/`; the home's lease holds one writer per home | `HeavySlot.cs:100-104`, `HomeLease.cs:52-55` |
| This repository marks no gate heavy, since marking them would have had each session run its suites at its end (D180's HEAVYRUN1c note) | `daoris.gates.json:4-21` |
| A driven session is told, as a sentence, to run its heavy gates once at its end through `run-heavy`, where its repository marks some | `HeavyTold.cs:35-46`, `Driver.cs:2016-2018` |
| A session's rules are composed per spawn: deny rules beside `no-push`, a sentence for auto mode's classifier, and the tree guard, which judges Edit and Write and never a shell command | `Permissions.cs:145-171`, `:188-193`, `SpawnSettings` at `Permissions.cs:503-532`, `Driver.cs:2422-2429`, `TreeGuard.cs:21-41` |
| Only Claude Code takes those rules, over both doors; Codex and dsh take none, and are handed nothing | `Adapters.cs:1221-1227`, `:1675`, `:1871-1872`, `:2104`, `Driver.cs:2395` |
| The call a live session's agent is inside is heard with its start; a protocol door's background work is heard by task | `RunningCalls.cs:5-24`, `AcpBackground.cs:6-41` |
| A session waiting on its own background work reads its quest every fourth tick, then waits for a turn in flight to end; a woken turn that never says its end holds to the timeout | `Driver.cs:3445-3450`, `Acp.cs:823-838`, D83's DONEEND1 note |
| The cap counts sessions across repositories; a session's timeout is the driver file's | `DriverConfig.cs:15-17`, `:59-60` |
| A merge blocks on `universal`, `code-map` and `orient-index` alone; every other gate runs in the CI beside | `merge-branch.mjs:781-786` |
| The merge tool's full set runs the declared gates and every `npm run` step of the release workflow the declaration lacks, today `rehearse` and `rehearse:family` | `merge-branch.mjs:708-741`, `release.yml:297`, `:302` |
| `--full --beside` lowers itself below normal with build servers of its own, raises its clock-bound lane, and a run of main's tip supersedes itself on any move of main, ending its step trees; given a commit it gates that commit and is never superseded; one runs at a time, by a lock in the main checkout | `merge-branch.mjs:3921-3943`, `:3935`, `:3967-4004`, `:4055-4112`, `:3684-3697`; D180's CIPRIO1c note |
| The merge tool answers one exit and keeps each gate's verdict and log itself | `merge-branch.mjs:3339-3366` |
| The merge tool reads a gate's `name`, `run`, `cwd` and `clock`; the devkit and the driver pass over keys they do not know | `merge-branch.mjs:744-764`, `GateDeclaration.cs:150-187`, `HeavyGates.cs:35-39` |
| One verdict store per repository, keyed by tree; the install's stage requires the full set's verdict of the commit it publishes, checks the gate tree's commit and lock, then builds from the tree holding neither | `merge-branch.mjs:1270-1283`, `desktop-publish.mjs:38`, `:867-894`, `:1012-1028` |
| A tree's dependencies are copied only where its lockfile is the checkout's and the checkout installed it; otherwise nothing of that project | `TreeDependencies.cs:99-127` |
| The driver publishes an ask to a repository with files and a workflow choice; a workspace plan's tick publishes the ask its press stood for | `ServiceClient.cs:698-704`, `AskWorkflowCommand.cs:6`, `SetupPress.cs:165-178` |
| Ask Daoris proposes a workflow and its choice, applied by the person's press; its proposal carries `steps` alone, read as schema 1 | `HelpWorkflowProposals.cs:11-12`, `:57`, `:70`, `:93` |

## 1. The start node

### 1.1 What it routes on

**Decided.** The start node routes each piece of work that arrives at a repository into one pipeline, on facts only:

| What arrives | Routed by | Read from |
|---|---|---|
| A chain's work (its quests in this repository) | D157 §4.1's order, unchanged: the person's choice for the task, the repository's for the task's kind, its default, the workspace's for the kind, its default, Current; then the kind's declared paths against the paths the work changed | the ask's `workflowChoices`, `driver.json`'s choices (`WorkflowSelection.cs:325-343`); git, for the paths |
| Landed work (the repository's line moved past the newest tip that has a verdict) | the repository's `landed` route (§1.2), else none | git's ref of the line, read in the pass |
| A person's ask to verify a branch or a commit | the same `landed` route, as a run of its own | the person's press or verb (§4.3) |
| A fix Daoris asks for after a failed verification | the kind the verification pipeline names for it (§4.5), routed as any task of that kind | the ask's choice, set by Daoris from the person's version |

- **The kind is the person's.** It is set by the person, or applied from the intake's proposal by the person's press (WORKFLOW1i-a). An
  agent sets one only on the person's quoted words where `Involvement.Lowers` finds nothing lowered (D157 point 10).
- **Paths route through the person.** A chain's route is chosen at the bind, before the work changed anything, so its kind's
  declared paths are the start node's second reading, made from git before the work's first gate: inside them the route stands;
  outside them, where the kind's pipeline lowers the person's part, the run holds with the routes the person may take instead
  (*Follow `<the other pipeline>`*, *Keep `<this one>`*, *Choose…*: the workflow design §4.4, `WorkflowGate.cs:315`), and where it
  lowers nothing the paths are reported. Nothing switches by itself, and a kind inferred from the changed paths stays rejected
  (D157). The paths are drawn on their route. Landed work is routed whole and never split by its paths: which gates a batch
  reaches is the check's (§4.7).
- **Never a model's judgement.** No route reads a conversation, an agent's words or a model's reading (D24, D54). The no-model
  tier routes everything (D157 point 14).

### 1.2 Where it sits: before the graph, kept on the binding

**Decided.** The start node is not a node of any version's graph. It is the choice table D157 point 5 already keeps (`workflows`
by repository, `workspaceWorkflows` by workspace), with one route added, resolved where the run is bound. It is drawn as one graph
with the pipelines it routes to (§1.3), and each pipeline is versioned and bound alone (§2).

- **The shape.** A repository's or a workspace's choice gains `landed`: `{ "workflow": "<id>", "gates": "<blob>" }`, the
  verification pipeline for work that reaches the line, and the declaration the person accepted for it to run unasked, named by git's
  blob id of `daoris.gates.json` (§4.2). Its `workflow` names a workflow marked a verification pipeline (§2.1), and both twins refuse
  any other there, as they refuse one so marked for `default` or a kind. A `workflow` of `current` there is no verification, today's.
- **`landed` resolves on its own.** A repository's `landed` where it names one, else its workspace's, else none; a repository's
  `current` there keeps its workspace's away. It takes no part in the replacement rule, which stays the development routes' alone: a
  repository that names a `default` or a kind replaces its workspace's `default` and kinds whole (`WorkflowSelection.cs:93-95`), and
  one that names only `landed` keeps its workspace's development routes. Without this, `Resolve` would take a repository's entry
  whenever one exists (`WorkflowSelection.cs:334-338`), and setting a repository's verification alone would drop its workspace's
  mapping of its kinds.
- **Older builds.** Both twins read a choice by its `default` and `kinds` and pass over the rest (`WorkflowSelection.cs:183-207`,
  `workflowchoice.ts:129-151`), and an entry naming neither is not read (`WorkflowSelection.cs:25`, `:261`), so an older build reads
  a repository naming only `landed` as naming nothing and follows its workspace, as the build this design asks does. An older build that writes a
  choice writes its `default` and `kinds` alone (`:283-315`), dropping the route: a downgrade's cost, said where the route is set.
- **When it is read.** A chain's route at the binding moment D190 §8 placed after the start's tree opens and before its take; a
  landed route when the pass reads the line's tip past the newest verdict (§4.3), and when a person asks a verification (§4.3).
- **What the binding keeps.** A chain's run keeps the level, the kind, its label and paths, as now (`WorkflowRunBinding.cs:11-33`).
  A landed run's binding is kept apart, in a folder of its own (§4.3), and keeps the level that chose it, its line, its tip and its
  base. `WorkflowRunBinding.Why` (`:54-66`) gains the landed route's sentence: *verifies `main` at `a1b2c3d` · this repository's
  landed work*.
- **Versions.** A route is a live choice, as every choice is: changed, it applies to the next run, and a run in flight keeps the
  pipeline and version it bound (D157 point 6; D190 §8, the choice read once at the bind).
- **Twins.** `WorkflowSelection` and the CLI's `workflowchoice.ts` read, write and resolve `landed` from one more row set on
  `fixtures/workflow-selection.json`, a repository naming only `landed` among them.

### 1.3 How a route is shown and changed

- **Shown.** A repository's and a workspace's *Workflow* tab (`WorkflowTab.tsx`, WORKFLOW1b) draws the start node first: a row per
  route (each kind by its label with its paths, the default, `landed`), each wired to the pipeline it names, laid out by D186's
  `Flow`; a route naming nothing says what follows then (Current, or *no verification here*). A run's *Workflow* view
  (`WorkflowRunView.tsx`) heads with the route that chose its pipeline. In a terminal, `daoris driver workflow show --repository
  <name>` prints the routes before the steps.
- **Changed.** At Setup through the tab's doors, and in a terminal by WORKFLOW1e's verbs: `daoris driver workflow use <id>
  --repository <name> --landed` (a flag beside `--kind`), which shows the declaration at the line's tip, asks the person to accept it
  (§4.2), and refuses a workflow not marked a verification pipeline (§2.1); run again, it writes the declaration's new blob id, as the
  tab's *Accept the gates* does. `kind` declares a kind as now. The editor (WORKFLOW1g) edits routes once built.
- **Ask Daoris** proposes a route only once its proposal can carry one. It carries `steps` alone today, read as schema 1
  (`HelpWorkflowProposals.cs:70`, `:93`), so it can propose neither a graph version, which a verification pipeline is, nor a route
  with no version. FABRIC-PIPELINE1i adds both: a `use` with a `route` and no version (an existing workflow chosen for a route), and a
  `graph` beside `steps`, judged by the graph reader and the mark (§2.1). The person's *Apply* sets either.
- **The person's part is never lowered by an agent** (D157 point 10). `Involvement.Lowers` (`WorkflowGate.cs:240`) compares steps;
  a verification pipeline is a graph, so its rows read its `check` nodes, beside CUTOVER1e's reading of a graph's gate nodes. Each
  change below lowers the person's part, so an agent's choice of one is kept as a proposal, and only the person's press applies it:

  | Change | Why it lowers |
  |---|---|
  | The `landed` route removed, set to `current`, or pointed at a workflow whose checks run fewer of the declaration's gates | landed work goes unverified, or less verified |
  | A verification pipeline's version that drops a `check`, narrows a check's `gates` (from `heavy` to names, or by a name), or removes the wire a stage waits on a check by (§4.6) | the same, inside the route |
  | A check's `failed` moved off `wait`, to `ask` or `stop` | `ask` starts work with no press of the person's; `stop` ends the run with no row on *What needs you* |
  | A development pipeline's `checks` set to `light` where no `landed` route runs the heavy gates it gives away | the heavy gates then run nowhere |

  Raising one (a gate added, `failed` back to `wait`) never lowers. *Accept the gates* is the person's door alone (D156). Each row
  ships with the first row of §7 that lets its change be set: the first three with 1a, the fourth with 1b.

## 2. What a pipeline is: a named workflow, chosen by its route

**Decided.** A pipeline is a named workflow (D157 point 5's file, `<home>/workflows/<id>.json`), its versions graphs (schema 2)
governed by the node runtime under D190. A run binds one version of one pipeline; the start node says which. On the *Workflow* tab
the start node and the pipelines it routes to are drawn as one graph, so the person configures one picture that begins at the
start node, while the engine binds each part alone.

**Why.** Development and verification differ in their unit. A development run is a chain's work, named by its first quest here
(`WorkflowRunBindings.cs:52-53`), its nodes observing the run's quests and branch (D190 §2.2). A verification run is a batch of
landed work on the line, judged at the tip it bound (§4.3). A run is the unit of its node record (D190 point 2), so one graph
cannot be both, and a route that only selects needs no node in either.

**The others, and what they would cost.**

| Option | What it is | What it costs |
|---|---|---|
| A sub-graph | One version holds a route node and nested graphs, one per pipeline | A third schema in both twins' readers, digests, validators and decoder (`NodeGraphs.cs:28-46`); D190's one landing, one opinion and one look per version (cutover design §4) restated per sub-graph; a record holding nodes of two unit kinds, or an outer graph no run ever evaluates, which is the choice table in a graph's clothing |
| A path through one graph | One graph per repository; the start node a node whose wires lead to each pipeline's first nodes | A wire that chooses one way among several, which no wire kind does (`NodeWires.cs:7-46`) and D157 §3.3 rejects (*a branch is an outcome, never an expression*); a state for a way not taken, which the lifecycle lacks (`skipped` is the person's word with words, `NodeContract.cs:104-105`); one record for a chain's run and a line's run; `Involvement.Lowers` made path-aware; every edit of either pipeline a new version of both, and Ask Daoris's diff mixing them |
| **A named workflow chosen by its route** | The choice table routes; each pipeline is a workflow of its own | A route is not diffed as a version: a run's binding records what chose it, which is what a run needs; one more route key in the twins; a mark on a workflow saying which pipeline it is (§2.1); a run kind on the line (§4.3) |

### 2.1 A verification pipeline is marked, and lands nothing

**Decided.** A named workflow's file says which pipeline it is: a top-level `pipeline`, `development` (absent: today's) or
`verification`. D190 point 4's rule, one `handed` and at most one `opinion` and one `look` (`WorkflowNamed.cs:248-261`,
`namedworkflows.ts:275-288`), holds a development pipeline. A verification pipeline's versions are graphs (schema 2: the steps
engine runs no `check`, D190 §10), each holding at least one `check` and none of `claimed`, `opinion`, `look`, `go-ahead`,
`handed` or `quest.close`, whose subjects (a chain's quests, its branch, its ask) a landed run has none of; every node there observes
the run itself at its bound tip (§4.3), and a `work/stage` after a check takes its verdict as §4.6 says. The `landed` route names
only a verification pipeline, and a kind or a default never one (§1.2).

- **The mark reaches both readers.** Today a version is read alone: `ReadVersion` and the CLI's `readVersion` take the version and
  its number, and judge one landing inside (`WorkflowNamed.cs:201`, `:232`; `namedworkflows.ts:223`, `:253`), and a new workflow's
  file is written as `{ id, name, versions }` (`WorkflowNamed.cs:649-652`, `namedworkflows.ts:878`). So the mark is threaded in: both
  readers read it from the file and hand it to each version's reading, which judges the one-landing rule or the verification rule by
  it; a save is refused and a stored version read as unreadable, as the one-landing rule is today. `workflow-graph-versions.json`
  gains the mark as a column of its rows, read by both twins.
- **Set once, at creation.** A new workflow's first save writes its mark (`PlanAdd`'s new file, and the CLI's), and every later save
  keeps the file's other fields as written (`WorkflowNamed.cs:663`), so no door changes it: a workflow of the other kind is a new
  workflow. A hand edit that changes it is read as written: each version is judged again under the new mark, and a run whose version
  no longer reads holds, said, as for any version that does not read; a route naming it is judged at its next bind.

- **Why the file, rather than the version or the graph's shape.** A mark per version would let a route's workflow change its unit
  between versions, so two runs bound under one route would be different units. Read from the shape (no `handed`, some `check`), a
  development graph whose landing was dropped by mistake would read as a verification pipeline, and a route would take it. A dummy
  landing would misstate what the run does, and draw a landing that never happens.
- **An older build** reads a workflow file's `id`, `name` and `versions` (`WorkflowNamed.cs:169-197`) and passes over `pipeline`, so
  it reads a verification pipeline's versions as holding no landing, unreadable, and binds nothing to them.

## 3. The development pipeline: which checks its sessions run

### 3.1 The policy

**Decided.** A development pipeline says which checks its sessions run, on its work: the `claimed` node's option `checks` in a graph
version (an adapter's option, `NodeContract.cs:304-310`), the `work` step's field `checks` in a steps version, and under Current a
repository's rule, `sessionChecks` in `driver.json` beside `heavyGates` and `access` (D157 point 4: Current draws the rules).

| `checks` | A session runs | The repository's heavy gates |
|---|---|---|
| absent | as today | once at its end through `run-heavy`, where the repository marks some or this machine maps some (HEAVYRUN1b1) |
| `heavy-at-end` | the checks its change reaches, then the heavy gates once | the same, said as the pipeline's |
| `light` | the gates its repository does not mark heavy, and the checks its change reaches, filtered (a test class, a test file, a census) | never: they are the verification pipeline's (§4), and refused (§3.2) |

- **Light needs marks.** Under `light` a session may run every gate the declaration does not mark heavy. A repository that marks none
  and has no mapping on this machine gives nothing to verification: the *Workflow* tab and Setup say so (*this repository marks no
  heavy gate, so its sessions may run every gate*), and nothing is refused.
- **Nothing set is today** (D157 point 15).
- **A second option, `callMinutes`** (default 20), bounds a single call before it is said (§3.2's fourth layer).

### 3.2 How a heavy gate is refused, and said

Four layers, each holding what it can and no more:

1. **Said by the graph.** For a run whose bound policy is `light`, the start's words (`Driver.cs:2016-2018`, in place of
   `HeavyTold.Words`, `HeavyTold.cs:43-46`) are composed from the bound version and the declaration: the light gates by name, the
   checks its change reaches, filtered, and the heavy gates by name as the verification pipeline's, with where its verdict shows.
   No standing answer carries a test rule any more; the standing answer keeps what is not a check.
2. **Refused where the session asks Daoris.** `run-heavy` run from a tree a session holds whose run's policy is `light` exits 1,
   saying that the gates run in the verification pipeline after the work lands and where its last verdict is. The session is read
   from git's own lock on its tree, whose reason names it (`daoris session <id>`, `SessionLocks.cs:28`; TREELOCK1, D51's note), never
   from a variable it could leave out. A person's `run-heavy` from a checkout no session holds runs as today. This layer is Daoris's
   own verb, so it holds on every harness. And under any policy, a session's `run-heavy` never waits behind a verification run
   (§4.2): today it waits for the repository's slot with no bound (`HeavySlot.cs:171-206`, `HeavyRun.cs:223`), and a verification
   holds it for up to its bound, longer than a session's 90 minutes.
3. **Refused by the harness, where the harness takes Daoris's rules.** Only Claude Code takes them, over both doors (`TakesSettings`,
   `Adapters.cs:1675`, `:2104`); Codex and dsh take none (the default, `Adapters.cs:1221-1227`; Codex's own word, `:1871-1872`), and
   `HandRules` hands them nothing (`Driver.cs:2395`). Where it takes them, each heavy gate's declared command joins the session's deny
   list, `Bash(<run>)`, beside `no-push` (`Permissions.cs:158-165`), and auto mode's classifier is told one sentence naming the heavy
   gates as the verification pipeline's (UNBLOCK4's channel, `Permissions.cs:188-193`, `:523-532`). Only declared commands, exact
   (development-documents: *exact commands, one per entry*), and never as a prefix: a gate's command can begin a filtered check
   `light` allows (`dotnet test src/Daoris.Service` before its `--filter`). HEAVYRUN1's rejection of guessing a heavy run from command
   text stands, so a whole suite spelled another way (a test project with no filter) is not refused by its text. **Where the harness
   takes no rules, this layer is absent, and said**: Setup's row and the session's start say *this harness takes no rules, so the
   heavy gates are said to it and `run-heavy` is refused, never its shell*. Translating the denies into another harness's rule
   language is that adapter's claim, made when its flag is verified against its binary (`Adapters.cs:1221-1226`), never this
   design's. A `light` pipeline does not refuse those harnesses: the verification pipeline's verdict holds the outcome whichever
   harness did the work, and a whole suite a session runs anyway costs its time, never a verdict.
4. **Said again while it runs.** A running call or a background task past `callMinutes` is said to the session once, at urgent
   level, as Daoris's own word on a fact that makes the work in flight wrong (D188 point 2): the call's title and time, and that a
   suite run whole is the verification pipeline's. It is heard where the calls already are (`RunningCalls.cs`,
   `AcpBackground.cs`). Daoris never ends the call: a turn is the agent's (DONEEND1), and the person's *Stop* stays the door.

Against the case: the three sessions, Claude Code's, would have been told by their graph, refused `run-heavy` and the declared
commands, and told again at twenty minutes; the one that still ran its suite would have been told, and seen on the rail with its
time (WAITSAY1). With no heavy run in its background, a session that reaches done is not waiting on background work, so DONEEND1's
look (every fourth tick, a minute at the default poll, `Driver.cs:3445-3450`) has nothing to wait for, and the session ends as its
last turn does rather than at the timeout. D83's remaining case, a woken turn that never says its end and holds to the timeout
(D83's DONEEND1 note), is unchanged here.

### 3.3 The marks a declaration carries

`heavy` keeps its meaning (*only through the slot*, `HeavyGates.cs:9`); the pipeline decides whose turn in the slot it is: the
session's at its end, or the verification pipeline's. `clock` (`merge-branch.mjs:760-763`) is read by Daoris's check too (§4.2).
A repository whose own runner runs its gates in lanes declares it in a top-level `verification` key (§4.2), which the devkit, the
merge tool and the driver's gate reader pass over today. A light gate needs no mark.

### 3.4 What it does not hold

A session's wait on its own background work is bounded by its timeout alone, as BGWAIT1 decided (a bound of its own was rejected
there); the second and fourth layers remove the reason a session waits on a whole suite, never the wait's rule.

## 4. The verification pipeline

### 4.1 The `check` node

**Decided.** A built-in kind, `check`, performed by Daoris itself through the performer (§4.2), its source a new `gates`: the exits
of the declared gates as Daoris's executor ran them. It is not an agent session: no harness, no account, no model, and no place in
the cap.

| Option | Values | Default |
|---|---|---|
| `gates` | `heavy` (the declaration's heavy gates, or its `verification` runner where it declares one), or gate names | `heavy` |
| `failed` | `wait`, `ask`, `stop` | `wait` |
| `ask` | a kind the workspace declares: the kind a fix ask carries (§4.5) | none: the repository's default route |

- **Its bound** is the node's own `timeoutSeconds`, the field every graph node carries (`NodeGraphs.cs:606`, any positive number,
  `NodeContract.cs:472`); it has no option of its own. Where the node declares none, the `check` kind's default is 7200 seconds, in
  place of a plugin node's 15 minutes (`WorkPerformer.cs:126`), since a full set outlasts that. The executor owns it, counted from
  its slots held (§4.2).
- **Its output** `verdict`: the commit it judged, each gate's name and verdict, and the declaration's blob it ran under. A stage
  after it may take it by a `data` wire (§4.6).
- **Its authority** (§E2's table): performed, reached and failed by `gates`; reached when every gate it names passed at its
  subject's commit; failed when one failed, naming it with its exit; `unknown` for anything else (no tree, no declaration, a
  declaration not accepted, a runner's result that does not read, names another commit or leaves out a gate, past its bound), never
  passed or failed, and held for the person (§4.3). The performer applies an outcome as its plugin's today (`WorkPerformer.cs:703`,
  `:710`), so it names the source from the kind's authority instead, which the one table holds a reach and a failure to
  (`NodeLifecycle.cs:357`, `:372`).
- **Never skipped, by its kind.** As git's proofs (`handed`, `line`) and `quest.close` are never skipped (`NodeContract.cs:409-413`,
  `:419-421`, `:431`): a skip would read as a verdict, and a check is the evidence a stage and the line's reader rest on. A
  declaration cannot say so itself, since a built-in's declaration may not forbid a skip (`NodeContract.cs:476`); the kind's authority
  does. The person's doors on a check are a new attempt (*Run again*) and a stop (*Not now*), each the one table's own
  (`NodeLifecycle.cs:264`, `:266`, `:283`); the word *happened* or *did not* on a held attempt stays a plugin node's
  (`NodeContract.cs:396`, `NodeLifecycle.cs:340`). A check before `handed` that keeps failing is passed only by a version without it,
  a lowering the person makes (§1.3).
- **How it is performed is its kind's.** A built-in's declaration may not carry a `perform` (`NodeContract.cs:480-482`), and the
  performer reconciles only a node whose `perform` says it can (`WorkPerformer.cs:600`). So the runtime gives a `check`'s declaration
  its kind's `perform` (reconciled) after the declaration is judged, as it gives a plugin's node its step's (`NodeRuntime.cs:297-303`);
  no author writes it. (`quest.close` is no exemplar here: its closer performs it beside the performer with no `perform` at all,
  `QuestCloser.cs:298-310`, `:496`.)
- **Where it may stand.** In a verification pipeline on the line (§4.3), its first use. In a development pipeline on the run's branch
  too: before `handed` it blocks the landing (a sensitive scan, D180 point 1); placed beside, not wired into `handed`, it is
  FABRIC-VERIFY1's lane (the verification lanes design §1, §4), whose first slice may use it.
- **A service's checks stay a plugin's** (`<plugin>/<node>`, D190 §10, FABRIC-PERFORM1l). Schema 1's `check` (`WorkflowNamed.cs:79-84`)
  stays unrun.

### 4.2 What it runs, where, how, and with what say-so

**What it runs.**

- **The commands.** The repository's `verification` runner where its declaration names one (`{ "run", "cwd"?, "clock"?, "result"? }`:
  one command that runs the declaration's gates in its own lanes, as this repository's merge tool does, §5); else the named or heavy
  gates one after another, the first failure ending the sequence, as `run-heavy` runs them (`HeavyRun.cs:236-297`); else this
  machine's mapping for a repository that declares none (HEAVYRUN1b2a), which is the person's already.
- **A runner that answers by file** declares `"result": true`. Daoris hands it the run's tip as `DAORIS_VERIFY_COMMIT` and the path
  to answer in as `DAORIS_VERIFY_RESULT`, set on the job alone and never on the driver's own environment, so no session the driver
  starts inherits them; and a runner reads them only where its declared command says so (this repository's merge tool by a flag,
  §5.1), so a person's shell that still holds one never retargets their own run. It runs the declaration's gates at that commit, and
  no others, and writes the file once, atomically, before it exits: `{ "schema": 1, "commit", "gates": [{ "name", "verdict", "exit"?,
  "wallSeconds"?, "log"? }] }`, or `{ "schema": 1, "commit", "busy": true }` where another run of it holds its own lock, written
  before it refuses.
  - `commit` is the full object id git gives, compared exactly; `name` is the gate's name as the declaration writes it, never one the
    runner makes of it; `log` an absolute path where the gate's log stays once the runner has exited, never a folder its next run
    clears.
  - Each `verdict`: `pass`, the gate ran to its end at the commit and exited 0; `flake`, it failed and passed when run again alone
    (FLAKE1), read as a pass and said; `fail`, it ran to its end and failed, with its `exit`; `ended`, started and ended by the runner
    before its verdict; `not-run`, never started.
  - **Judged from the gates alone.** Failed where any gate's verdict is `fail`, naming it with its exit. Else reached where every gate
    the check names (the declaration's heavy gates, or its names) is there as `pass` or `flake`. Else `unknown`, naming each gate
    missing, ended or not run. And `unknown` where there is no file, where it does not read, or where it names another `schema` (said
    by its number), another commit, or a gate the accepted declaration does not declare: a runner that says it answers by file and
    wrote none is broken, never opaque. So an empty `gates` is never a pass.
- **A runner that declares no result is opaque**: one check, named by the runner, its exit the family's (0 reached; 1 failed, naming
  the runner; 75, `EX_TEMPFAIL`, busy; anything else `unknown`), its output kept as its one log. So any repository's script serves as
  a runner, and the contract buys the gate-by-gate verdict.
- **Busy is the executor's to wait out.** It lets go of its slots and offers the runner again ten minutes later, six times at most,
  the attempt still active and saying *waiting for the runner's own run*; then `unknown`, *the runner's own run held its lock*. No
  failure is written for it, so a busy runner never reads as a verdict on the line.

**With what say-so.**

- **The person's acceptance.** D154's review design refuses a command read from the repository's files (review environment design
  §2.3: *a guess*), and a declaration a session can edit and have obeyed is an agent granting itself permission
  (development-documents). So the gates run unasked only as the person accepted them. The `landed` route keeps the blob id git gives
  `daoris.gates.json` at the line's tip (`git rev-parse <tip>:daoris.gates.json`), read with no checkout, or `none` where the tip has
  no declaration. Any edit of the file changes it: a command, a folder, a gate added or removed, a `heavy` mark (which decides what
  `gates: heavy` runs, `HeavyGates.cs:78-89`), a `clock` mark (its priority) and the `verification` runner among them. An edit that
  changes what runs in no way (a key moved) asks again too, which is the price of a digest no twin has to compute alike. This
  machine's mapping is the person's own, set on their machine, and outside it. A run whose tip's blob differs is `unknown`, *the
  gates changed since you accepted them*, held with git's diff of the two blobs and *Accept the gates*, the person's door (D156), which
  writes the tip's blob and gives the check a new attempt; nothing runs meanwhile. A landed edit of `daoris.gates.json` never runs
  by itself.
- **What it does not cover.** What a command runs (a script, a test) is the repository's code, as it is whenever the person runs it:
  the acceptance holds which commands run unasked, never what the code they run says.

**Where.**

- **A runner owns its checkout.** It is started in the repository's checkout Daoris knows, where the person starts it, and makes and
  moves its own pinned tree, installs there, and keeps its logs and its lock, as this repository's merge tool does with its gate tree
  (`merge-branch.mjs:4055-4112`). Daoris makes no tree for it.
- **Direct gates run in a verification tree of Daoris's**, for a repository with no runner: one per repository, under its trees
  beside its session trees, detached, made once and moved to each run's tip as GATEWT1's gate tree is (D115's GATEWT1 note), and
  nothing but the check runs or builds there; never the main checkout (D180's rejection). It is held as a replay's tree is
  (STRAYOTHERS1, `PassLeftovers.cs:14-18`): made and moved only by a run holding the repository's slot, and locked while that run
  uses it, git's lock reason naming its holder's process, so the tidy keeps it as someone's and the next verification lets go of a
  lock whose process is gone. Between runs it is a tree git lists, which the tidy keeps (`TreeStrays.cs:54-57`).
- **Its dependencies** are copied as a session tree's are (TREEDEPS1), which copies nothing of a project whose lockfile is not the
  checkout's or that the checkout has not installed (`TreeDependencies.cs:99-127`). Daoris installs nothing of its own there: an
  install it derived from a lockfile would be a command the person never accepted. A repository whose gates need an install declares
  it as a gate the check names, accepted with the rest. Where neither copies nor installs, a gate that needs the folder fails as
  itself, and the run's record names each project not copied with TREEDEPS1's reason.

**How it runs: the executor, off the pass.** The pass never awaits a check's gates: it holds one semaphore through its starts and
awaits each (`NodeRuntime.cs:388`, `:560`), and a full set runs for an hour.

- **Started as a performed node is.** The performer starts `check` as it starts a plugin's performed node (its guard,
  `WorkPerformer.cs:252`, widened to the one kind Daoris performs this way), so its attempt (invocation, key, the run's tip as its
  revision, deadline) is written on the run's record under its lock before anything runs, and a second start of the node reads the
  first and is refused (FABRIC-PERFORM1h). The attempt's plugin is read from its kind's slash today (`:264`); a `check` has none, so
  its attempt names the executor, and the port sends it there by its kind, never by a plugin's name. The executor is Daoris's own
  **heavy executor**, in place of a plugin's process: `run-heavy`'s slot, named job, gate sequence and end-before-release
  (`HeavyRun.cs:196-297`) made one asynchronous executor in the driver, which `run-heavy` awaits and the check sends to. The send
  returns once the executor has taken the attempt; all that follows runs on the executor's own task.
- **The slots, the executor's.** It holds the repository's heavy-run slot (`HeavySlot.cs:145-160`), so no `run-heavy` in this home
  runs beside it, and one verification run in the home at a time, a second slot under the home's `locks/` beside the heavy slots. The
  home's first, then the repository's, each tried without waiting, one not free letting the other go at once: no run holds a slot
  while it waits for another, so a repository whose slot a session's `run-heavy` holds never keeps another repository's run from the
  home's. The attempts it was sent wait in the order they came, each with a waiter record of its own beside the slot naming its run
  (as HEAVYSLOT1's waiters are kept, `HeavySlot.cs:36-39`), so the run and the repository's *Details* say behind what (D185 §3.5's
  *Heavy run* row). Once both are held, the holder record names the run and its invocation beside its job (`HeavyHolder`,
  `HeavySlot.cs:19-20`, gains `run`, which an older reader passes over), and the bound starts. Until then the check is active and
  says *waiting for* what holds a slot.
- **Per home, and said so.** Both slots are the home's, as every slot is (`HeavySlot.cs:131`; one writer per home,
  `HomeLease.cs:52-55`). A second home on the machine (a development home beside the install's) holds its own, so two homes may each
  run one: D180's budget note, point 2 (one CI run at a time, `D180.md:68`), is narrowed to one per home. Where the repository's
  runner keeps a lock of its own (this repository's, in its main checkout, `merge-branch.mjs:3764-3794`), two homes' runs of it are
  still one at a time; a repository with no runner that two homes verify is verified twice, which Setup says where a route is set.
- **A session never waits behind it.** A session's `run-heavy` (its tree read from git's lock, §3.2) that finds the repository's slot
  held by a verification run does not wait: it says that the verification pipeline runs these gates on the line, and where its
  verdict will show, and exits 1. Behind another session's run, or a person's, it waits as today; a person's own `run-heavy` waits as
  today, and they may stop it.
- **Its answer, through the reconcile alone.** The executor writes the attempt's result atomically in the run's folder
  (`<home>/workflows/lines/<run>/attempts/<invocation>/`, §4.3), then tells the performer the attempt has an answer and asks a pass.
  The pass reconciles before it starts anything (`NodeRuntime.cs:456`), and the performer asks the executor as it asks a plugin's
  reconcile (`WorkPerformer.cs:580-669`), which answers from the folder: a reached or failed verdict as `done` with its outcome
  (`:643-644`), applied under the record's lock to that invocation alone and only while the attempt still waits for it; an `unknown`
  verdict as `unknown`, which the performer holds for the person (`:662-664`), since a `done` carrying unknown leaves the attempt
  unknown and asked again at every pass (`:714-722`); no result and no job of it here as `not-started`, which sends the same attempt
  again under its key (`:649-651`), since a check has no effect but its verdict, so one cut off before its result is one that never
  ran; and still running as `running`. No completion is heard: `WorkBelief.Completion` takes a frame only from a plugin's current
  process (`WorkBelief.cs:27-36`), and nothing here needs it to change.
- **Never asked at its deadline.** The performer asks after a sent attempt at its deadline (`WorkPerformer.cs:568-574`), and an
  answer of *running* past it records the node unknown and active again and puts the next ask a whole bound later (`:590-593`,
  `:657-661`). The executor runs in the driver and owns the bound, so a check's attempt is asked after only when the executor says it
  has an answer, or after a restart (an attempt this process did not send).
- **Its bound.** At the bound the executor ends the job and waits until it holds nothing, lets go of the repository's slot and then
  the home's, writes *unknown: ran past its bound*, and only then says it has an answer. No slot is let go before the job is empty.
- **The person's stop.** *Not now* on a running check is the person's move on the record; the pass that applies it tells the
  executor, which ends the job as at its bound, and the result that follows is a stale attempt's, settling it and moving nothing
  (`WorkPerformer.cs:691-695`). Daoris itself ends a check's job only at its bound.
- **A restart or a drain.** The job's one handle was the driver's, so it ended with the driver, the staged update's drain (D139)
  included. The reconcile is answered from the attempt's folder: a job its name still finds is ended first (`ProcessJob.EndNamedAsync`,
  as a heavy slot's next holder ends one, `HeavyRun.cs:233`); a whole result there is applied; none is `not-started`, and the attempt
  runs again at its tip, taking its slots again.
- **The job and its environment.** One kill-on-close job, as `run-heavy`'s, handed `DAORIS_HEAVY_RUN`, the two `DAORIS_VERIFY_*`
  variables where its runner answers by file, and `BESIDE_ENVIRONMENT`'s three variables (`merge-branch.mjs:4015`), so its builds keep
  servers of their own and nothing shuts a server down (SHUTDOWNBESIDE1).
- **Its priority beside the owner's window.** Below normal by its first process's class, which every process it starts inherits, as
  CIPRIO1 lowers the merge tool's (`merge-branch.mjs:3993-4008`); never by a priority limit on the job, which would hold every process
  in it to that class and keep a runner's raise of its clock-bound lane from taking (`merge-branch.mjs:4025-4040`). A direct gate or a
  runner marked `clock` starts at normal. It is never counted against the cap, as a CI run beside never lowers the workers (the
  dispatch skill's step 5; FAMILYFAIL1b). I/O priority stays CIPRIO1b's. The window and the sessions keep their priority.
- **Its cost to the pass:** a send is a hand-over, and a result is read once, at its reconcile.

### 4.3 A run per batch: what it takes, and how it batches

- **What it takes.** By default the repository's line (its landing rule's target, this repository's `main`) at its tip. A person may
  ask it of a branch or a commit, `daoris-driver verify <repository> [--branch <name> | --commit <sha>]`, and a *Verify…* door on
  the tab: a run of its own under the route's accepted declaration, which the line's batching never counts.
- **The run.** A landed run is the runtime's unit `run:<run>` (D190 point 2). Its id is new for each run, `v-` and sixteen hex
  digits, which `IsRunId` passes (`WorkflowRunBindings.cs:42`): a repository, its line and a tip name no run alone, since a person's
  run of a commit can share the line's tip. Its binding, written once, keeps its repository, the level that chose it, its line, its
  tip and its **base**: the tip of the newest run on the line whose check settled reached or failed. Its batch is the commits from
  base to tip, and the landings that carried them. **Before any verdict** there is no base: the first run's batch is the line's
  history to its tip, said as its first verification.
- **Its subject.** Every node of a landed run observes the run itself, `run:<run>`, handed to the evaluation with its bound tip as
  its revision, where a chain's run is projected with none (`WorkUnit.cs:16-19`) and a node whose subject has no revision is not
  judged (`WireEvaluation.cs:349-353`). The tip is written once, so nothing on the run lapses as the line moves. A plugin's node there
  is told the line by its name at that tip, as a node after `handed` is told the landed branch at the landing's tip (D190 §2.4.1).
  D190 §2.2's table gains this row.
- **A folder of its own.** `<home>/workflows/lines/<run>.json`, one file per run written once, its folder beside it (`lines/<run>/`:
  its attempts, their results, the logs it keeps). Beside the chains' runs a landed run would be lost and would hold work back: the
  history clear removes a binding no quest holds (`HistoryHome.cs:158-167`), which a landed run never is, and `AnyNamed` turns true
  once any run of a repository is bound to a named workflow (`WorkflowRunFiles.cs:60-83`), so a gate that cannot read its chain would
  be held (`WorkflowProcesses.cs:97-99`) in a repository whose sessions follow Current. An older build reads neither file. Arming
  lists `lines/` beside `runs/` (the cutover design §3), and the versions a kept run names, which a workflow keeps past its newest
  20, are read from both, since today they are read from `runs/` alone (`WorkflowRunFiles.cs:85-105`) and a version a landed run
  binds would be pruned under it. One folder, not one per repository: a run's file names its repository, and no repository's name
  has to make a safe folder name.
- **Batching.** One run per line at a time. A run is settled once its check is reached, failed or stopped by the person; until then,
  waiting, running or held, it is the line's run in flight. When the pass reads the line's tip past the base and the line has no run
  in flight, it binds one at the newest tip, so every landing since the base is in it; what lands while it waits for its slots or
  runs is the next batch's. So every landing on the line comes under a verdict: its batch's, or a later batch's that holds it (D193
  part 8).
- **Each run to its verdict.** A run, once bound, is never superseded: the line moving on binds nothing until it is settled, and only
  the bound or the person's stop ends its job (§4.2). A run that waits for its slots behind another verification or a `run-heavy`
  verifies its bound tip when they come, and what landed meanwhile is its successor's batch. Superseding needed a state no lifecycle
  has: `NodeStates.All` holds no `superseded` (`NodeContract.cs:113`), only the person stops a live node (`NodeLifecycle.cs:283`),
  and Daoris's own move, `invalidated` (`:286`), would judge the check again and start it at the same tip. Without it go the end of a
  job in flight, the rules for a result heard after one, and the bound that kept a fast line from going without a verdict. The cost
  is a verdict on the newest tip one run later when the line moves during a run, against D180's budget of thirty minutes a run, and
  batches that are smaller, so a failure names fewer commits.
- **An unknown holds the line for the person.** A check settled `unknown` is held (§4.2), and its run with it, so no run binds behind
  it: binding the next would bind the same tip whenever nothing landed, and a cause that stays (a runner whose result never reads, a
  gate that always outruns its bound) would run the full set again at every look. *What needs you* shows it with its reason, *Run
  again* (a new attempt at the run's tip) and *Not now* (the person's stop: the run settles with no verdict, and since the base moves
  only on a verdict, the next run's batch takes it in); a changed declaration's door is *Accept the gates* (§4.2).
- **The line is read from its runs.** It keeps no record of its own: its base, its run in flight and its newest verdict are read from
  the runs bound on it, whose bindings are written once and whose records only the runtime writes, in the pass, under the home's
  lease. With no supersede, nothing but a run's own outcome changes its record, so a late result has nothing to overtake and a slot is
  only ever let go by the executor that holds it.
- **Arming and its cost.** A `landed` route arms the runtime (`NodeRuntime.cs:222-226` gains it), and each look reads the line's ref
  of each such repository (one ref read, or the fabric's ref watcher where it serves).
- **Its end.** A landed run ends by the cutover design §3's rule (its graph's end settled, or no node of it able to move again with no
  attempt unsettled), its clause on a run's newest quest read as met, since a landed run has none. CUTOVER1c keeps that end on the
  run's record.
- **A restart or a drain** sends the run's check again at its tip (§4.2); the line waits for it as for any run in flight.
- **Cleared with the history** (D153): a landed run's file, folder and node record once a newer run on its line has a verdict, and a
  person's run once it has settled; never the newest run with a verdict, and never one unsettled.

### 4.4 Its verdict, recorded and shown

- **Recorded** on the run's node record: the check reached, failed or unknown, its evidence each gate's name, verdict, exit, wall and
  log, from the runner's result or from the executor's own sequence; an opaque runner's, one entry. Each failed gate's log, its last
  256 KiB, is copied into the run's folder (`<home>/workflows/lines/<run>/`) once its result is applied, and the others are named
  where their runner keeps them. The run's folder is cleared with the run (§4.3). *Whether a commit is verified, and for which
  gates* is one reader over the runs bound on a line, which the page, a stage and any door ask.
- **Shown.** The repository's *Workflow* tab: the verification pipeline's newest runs, each its tip, its batch, its state, its wall,
  its gates and a door to each log. The run's *Workflow* view (WORKFLOW1c), its subject the line at its tip. The repository's
  *Details*: the *Heavy run* row names the verification run while it holds the slot (D185 §3.5). *What needs you*: a failed
  verification, the person's row (§4.5). A terminal: `daoris-driver workflow run --line <repository>` beside WORKFLOW1c2's
  `--session|--quest|--ask`.
- **Words.** *verified*, *failed: `<gate>`*, *not known: `<why>`*, *waiting for `<what>`*, *not now*, each a status word in both
  catalogues (D116).

### 4.5 A failure becomes work for the development pipeline

- **`failed: wait`** (the default). *What needs you* shows the failed gate, the tip and the batch, with *Ask for a fix…*, *Run again*
  (a new attempt at the same tip, for a flake, the person's own move, `NodeLifecycle.cs:264`) and *Not now*.
- **`failed: ask`.** The person's standing say-so, drawn in *Your part* as *a failed verification asks for a fix without asking you*;
  Daoris then publishes the fix itself, as a workspace plan's tick publishes the ask its press stood for (`SetupPress.cs:165-178`).
- **The fix ask** is published to the repository (`to`), so a quest is published as a setup ask's is (`SetupWorld.cs:73-74`): Daoris's
  fixed words naming the gate (or the runner, where it is opaque), the tip, the batch's commits and the sessions whose landings carried
  them, marked as Daoris's, never the person's; the failing gate's log, its last 256 KiB, as the ask's file (`ServiceClient.cs:698-704`);
  and the kind the check's `ask` names as its choice (`AskWorkflowComposed`, `AskWorkflowCommand.cs:6`), so the start node routes it
  into the development pipeline the person chose for that kind (§1.1). Its fixed words name the run, the line and the gate.
- **Published once, by a look first.** An ask is sent with no key of the sender's (`ServiceClient.cs:698-704`), so a restart between
  its send and its answer could not tell whether it was made. Before it publishes, Daoris looks among the workspace's asks for one it
  published naming this line and gate: one naming this run is the ask already made; another one still open takes the new failure on
  its channel at ordinary level (D188), so there is one per gate; none, it publishes. It reads only words Daoris wrote, never a
  person's, and changes nothing in the service.
- **Nothing undone by itself.** No bisect, revert or rollback (D157 §3.7). A person may ask a commit of the batch (§4.3).
- **`failed: stop`** ends the run failed, shown on the tab, and asks nothing.

### 4.6 A stage or a publish requires the verdict

- **In the graph.** A stage (a plugin's `work/stage` node, FABRIC-PERFORM1l) after a check waits on it by any wire. `after` is
  satisfied by a source reached or skipped (`NodeWires.cs:9`, `WireEvaluation.cs:634`), and a check is never skipped (§4.1), so from
  a check `after` holds only on its reach, as `data` does (`NodeWires.cs:13-14`), and a stop satisfies neither (D190 point 6). A
  `data` wire also hands the stage the verdict. So the workflow design §3.7's refusal of *Go on anyway…* for a check a production
  stage depends on holds by the check's kind, and no rule on the stage's wire is needed. At the stage's dispatch, where its inputs are
  resolved again (`WorkPerformer.cs:258-259`), the verdict reader (§4.4) is asked for that exact commit: the check reached at the run's
  tip under the declaration the route accepts now, else the stage is not started and says why. Production stays the person's press
  (D157 point 9).
- **Outside it.** The install's stage, `publish:desktop --stage --from-gated <commit>` (`desktop-publish.mjs:1012-1028`), keeps
  requiring the full set's verdict of that commit from the merge tool's store, which this repository's runner writes when the
  install's verification runs it (§5). Today it checks the gate tree's commit and lock, then builds from the tree holding neither
  (`desktop-publish.mjs:867-894`, `:1028`), so a verification run that starts meanwhile would move the tree under the build. It holds
  the gate tree's lock through its build instead, a holder of its own kind, so a full set started meanwhile is refused by that lock
  as a second run is, and answers `busy` (§4.2). Building each stage from a checkout pinned for it was the other shape: it costs a
  checkout and an install per stage, and stages a tree other than the one the full set built and tested. The verdict reader answers
  any other publisher the same question.

### 4.7 Later: the gates a batch reaches

CI1's reach generalised: a gate's optional `paths` in the declaration, and the check's `reach: paths` running the gates the batch's
changed paths reach, and every gate still owed: one whose newest verdict on the line is not a pass (failed, unknown, or never run)
stays owed, and runs in each batch until it passes, whatever the batch changed, since the base moves past a failed verdict (§4.3).
The whole set runs before a stage. Until its row, every gate the check names runs.

### 4.8 Beside FABRIC-VERIFY1

The lanes design holds what this does: a verdict names its exact candidate, unknown is never success, a failure routes one fix and
holds what has not begun (its §1, §4, §5). A lane beside a quest step is a `check` on the run's branch not wired into `handed`; the
verification pipeline is the line's lane, at a batch. Neither design waits for the other.

## 5. This repository under the design

### 5.1 Its gates, split

| Gate | Under the design | Why |
|---|---|---|
| `universal`, `code-map`, `orient-index` | light: a session's, and the merge's blocking set | seconds to a minute; what must never reach history (`merge-branch.mjs:786`, D180 point 1) |
| `web-types` | light: a session's, and the merge's blocking set where the page's sources change | about a minute; two sides that each typecheck may not together (MERGETSC1, D180's note) |
| `cli`, `tool-tests`, `service`, `devkit`, `driver`, `modules`, `driver-process`, `driver-process-git`, `modules-process`, `web`, `web-unit`, `web-measured`, `deployment` | `"heavy": true` | minutes to an hour each; the CI beside's (D180 point 2) |
| `rehearse` (`npm run rehearse`), `rehearse-family` (`npm run rehearse:family`) | declared, `"heavy": true` | the release and family rehearsals, which the merge tool runs today from the release workflow's steps the declaration lacks (`merge-branch.mjs:708-741`, `release.yml:297`, `:302`); declared, a session's deny rules name them and the acceptance covers them. Under the names the tool already gives them, so its reach table (`merge-branch.mjs:792-848`) is unchanged |
| `verification` (top-level) | `{ "run": "node tools/merge-branch.mjs --full --beside --daoris-verify", "result": true }` | the merge tool already runs the full set in its lanes, in its gate tree, writes the verdict the stage reads, and keeps its logs |

- **What a session runs here.** The three light gates; its own test classes and files, filtered; and the censuses its change reaches
  (`docs/development.md:92-104`), as the branch-worker's brief already asks of a builder.
- **HEAVYRUN1c's reason** (marking would have had each session run about thirty minutes of suites at its end) no longer holds under
  `light`, so the marks land with this repository's pipeline, never before it.
- **The runner and the tip.** `--full --beside` given a commit already gates it and is never superseded by main's moves
  (`merge-branch.mjs:4162-4167`). Its new flag `--daoris-verify` alone makes it the declared runner: it reads `DAORIS_VERIFY_COMMIT` as
  that commit and refuses where the variable is missing, so the variable left in a person's shell never retargets their own
  `--full --beside`. Under the flag it runs the declaration's gates alone, reports each under the name the declaration writes, where
  its plan slugs names and numbers a second of one name (`merge-branch.mjs:733-735`), and reports none of the release workflow's steps
  it adds today (`:724-727`), which the table above declares; it writes `DAORIS_VERIFY_RESULT` from its own verdicts, the full commit where it
  names folders by seven characters (`:4145`), and each log's path as it stays once kept in the main checkout (`keepGateLogs`,
  `:3965-3970`), not in the gate tree its next run clears. Busy, it writes the result before it refuses, where today its lock's
  refusal is exit 2 with nothing written (`:3776`, `:581-587`). Nothing ends it before its verdict but its bound or the person's stop
  (§4.2).
- **The stage.** `publish:desktop --from-gated` holds the gate tree's lock through its build (§4.6).
- **`docs/development.md`'s *Choose the checks*** (`:51-53`, *the complete reached set before recording completion*) is a builder's
  rule under the merge tool; a driven session's is its pipeline's, and the guide says so.

### 5.2 Its two pipelines, on the owner's install

- **`daoris-development`**, the repository's default route: `claimed` with `checks: light` and `callMinutes: 20`, then the gates and
  the landing as the install holds them now (a branch `quest/{slug}-{quest}`, never pushed and never merged by the install;
  DOGFOOD1).
- **`daoris-verification`**, marked `pipeline: verification`, the `landed` route on `main`: one `check` with `gates: heavy` (so the
  `verification` runner), `failed: wait` until the owner chooses `ask`, `ask: fix`.
- **The kind `fix`**, declared by the workspace (*Fix a failed verification*), mapped to `daoris-development`.

### 5.3 What the install then does differently with its six sessions

- Each session's start says its checks from its graph; `run-heavy` from its tree, and the declared heavy commands, are refused; a
  call past twenty minutes is said to it. The standing answer loses its test rule.
- A session that reaches done ends as its last turn does, with no suite in its background, so its place in the cap goes to the next
  quest rather than to a timeout (D83's remaining case, a woken turn that never says its end, aside).
- The parent stops running the full set beside by hand. Its merges move `main`; the install's verification sees the line move,
  binds one run at the newest tip (the whole batch), runs it to its verdict below normal beside the six sessions, and binds the next
  at the newest tip once it settles, every merge landed meanwhile in that batch.
- A failure is on *What needs you*; *Ask for a fix…* publishes a `fix` ask with the gate's log, which a driven session takes.
- The stage is unchanged but for its lock: `--from-gated` still requires the full verdict of the commit it publishes.
- Owed, measured on the install: a day's sessions ended by the timeout before and after, the verification's wall beside six
  sessions, and how many landings a batch held, and how long the newest tip waited for its verdict.

## 6. Rejected

- **A route chosen by a model's reading, or a kind inferred from the changed paths** (D157 point 10, D24, D54).
- **The start node as a node of a version's graph, a pipeline as a sub-graph or as a path through one graph** (§2's table).
- **A verification pipeline told by a dummy landing, by a mark per version, or by its graph's shape** (§2.1).
- **Routing landed work by its paths into several verification pipelines**: a kind inferred from paths in another form; a batch's
  reach by paths is the check's (§4.7).
- **A versioned route set**: one more versioned object and choice level for what a run's binding already records.
- **Verification as nodes after `handed` in every development run** (D190 lets a check follow a landing): the heavy gates once per
  landing, no batching, and a run held open on them. A cheap per-landing check (a service's status) stays a plugin's node there.
- **The instruction as a standing text**, which is today's workaround: an agent drops a sentence (D161).
- **Guessing a heavy run from a command's text** (HEAVYRUN1's rejection stands): the harness refuses declared commands exactly.
- **A `light` pipeline refusing a harness that takes no rules**: the verdict holds the outcome whatever harness did the work (§3.2).
- **Daoris ending a session's long call**: a turn is the agent's (DONEEND1); the person's stop stays.
- **A bound of a session's own on its background work**: BGWAIT1's rejection stands; its reason is removed instead (§3.4).
- **Daoris's own CI engine**, lanes and order of its own (D157's rejection stands): a repository with lanes declares its runner.
- **Running a landed edit of the declaration unasked**: the person accepts the declaration by its blob (§4.2).
- **A digest of the declaration's keys, made canonical**: a canonical form computed alike in two twins, and a key it leaves out
  that later decides what runs would go unasked; git's blob id costs a re-acceptance on an edit that changes nothing that runs, and
  its diff is git's (§4.2).
- **An install of Daoris's own in the verification tree**: a command derived from a lockfile that nobody accepted (§4.2).
- **The check awaited inside the pass, or performed beside the performer as `quest.close` is**: the pass holds its semaphore through
  each start, and a check runs for an hour (§4.2).
- **A `timeoutMinutes` option beside the node's `timeoutSeconds`**: two bounds on one attempt (§4.1).
- **The check's answer heard as a plugin's completion**: the hearing believes a frame only from a plugin's current process
  (`WorkBelief.cs:27-36`), so it would need a carve-out for Daoris's own; the reconcile already reads an answer back, and a restart
  reads the same folder (§4.2).
- **The performer asking a check's attempt at its deadline**: each ask past it re-arms a whole bound and records the node unknown and
  active again, while the executor that owns the bound runs in the same process (§4.2).
- **A busy runner as the performer's transient failure**: a failed check reads as a verdict on the line (§4.2).
- **A check a person may skip, or one a declaration makes unskippable**: a built-in's declaration may not forbid a skip, and a skipped
  check would read as a verdict a stage and the line rest on (§4.1).
- **A machine-wide verification slot outside every home**: a test's scratch home would wait on the install's run; D180's one run at a
  time is narrowed to one per home and said, and a repository's runner's own lock holds two homes' runs of it apart (§4.2).
- **A session's `run-heavy` waiting behind a verification run**: it waits with no bound, and a verification outlasts the session's
  timeout (§4.2).
- **A record of the line's own beside its runs**: a second writer of what the runs already say (§4.3).
- **Landed runs beside the chains' runs**: the history clear removes them and `AnyNamed` holds a Current repository's gates by them
  (§4.3).
- **Superseding a run in flight, with a bound or without**: the lifecycle has no state for it and Daoris no move to it, and it brings
  the end of a job in flight and the rules for a late result; each run running to its verdict costs one run's wall at most (§4.3).
- **Replacing a run that waits for its slots**: the replaced run would need an end Daoris writes for a run none of whose nodes moved;
  a run binds only when none on its line is unsettled, so at most one waits, and its batch is verified as bound (§4.3).
- **An unknown that lets the next run bind by itself**: a cause that stays would run the full set at every look (§4.3).
- **A key of the client's on the service's asks**: a change to the service's protocol where a look at the asks Daoris wrote answers
  the same question (§4.5).
- **Building each stage from a checkout pinned for it**: a checkout and an install per stage, and a tree the full set never built
  (§4.6).
- **A fix ask published with no say-so of the person's by default**: it starts work on the owner's accounts; `ask` is the person's
  standing choice, a lowering only the person makes (§1.3).
- **An automatic bisect, revert or rollback** (D157 §3.7).
- **A verification run counted against the cap, or run in the main checkout** (FAMILYFAIL1b; D180's rejection).
- **Marking this repository's suites heavy before its development pipeline is light** (HEAVYRUN1c's reason holds until then).

## 7. Build rows, in order

Each is a feature a person can see working (D187), end to end across its lanes. Each lowering row of §1.3 ships with the first row
that lets its change be set, never later.

| Row | What a person sees | After |
|---|---|---|
| FABRIC-PIPELINE1a | the start node and its routes: `landed` set, resolved on its own and its declaration accepted by its blob and drawn; a verification pipeline marked at its creation and judged in both readers, its `check` declared, never skippable, and not yet run; a run's view says which route chose it; the lowering rows for routes and verification pipelines | — |
| FABRIC-PIPELINE1b | a development pipeline's light checks: the rule and the option, the start's words from the graph, `run-heavy` refused from a held tree, the deny rules and the classifier's sentence where the harness takes rules and the saying where it does not, Setup's row, and the `light` lowering row | — (a graph version's option read at the bind after CUTOVER1g) |
| FABRIC-PIPELINE1c | a long call said: Daoris's urgent word past `callMinutes`, on the conversation and the rail | 1b |
| FABRIC-PIPELINE1d | the `check` performed, and a person's verification of a commit or a branch: the executor off the pass and answering through the reconcile, its slots and queue, the runner's contract, the tree and its dependencies, the job, priority and bound, the person's stop, a restart sending it again, a session's `run-heavy` never waiting behind it, the verdict recorded and shown | 1a; CUTOVER1d (performed nodes started, itself after CUTOVER1c, 1e and 1f: the runtime governing a run, the run's view, the person's skip and stop) |
| FABRIC-PIPELINE1e | landed work verified by itself: a run per batch and its base in a folder of its own, each run to its verdict, an unknown held for the person, the line read from its runs, the history clear, the failure on *What needs you* | 1d |
| FABRIC-PIPELINE1f | a failure becomes work: *Ask for a fix…*, `failed: ask` as the person's say-so, one ask per gate found by a look first, the log attached, the kind routed | 1e |
| FABRIC-PIPELINE1g | this repository on the install: the rehearsals declared and the suites marked, the merge tool as the declared runner, the stage holding the gate tree's lock, the guide's words, the install's two pipelines set by the owner's press | 1b, 1e |
| FABRIC-PIPELINE1h | a stage after the check, its dispatch reading the exact commit's verdict, and the verdict reader any publisher asks | 1e, PERFORM1l |
| FABRIC-PIPELINE1i | Ask Daoris proposes routes and verification pipelines: a `use` with a route and no version, and a graph version beside `steps` | 1a |
| FABRIC-PIPELINE1j | the gates a batch reaches by its paths, every owed gate with them, the whole set before a stage (CI1's reach for any repository) | 1e |

- **1a** (driver, cli, web). Proof: both twins' rows on `workflow-selection.json` (`landed` stored and read; refused naming a
  development pipeline; a kind or a default refused naming a verification one; a repository naming only `landed` following its
  workspace's development routes, and its `landed` the workspace's where it names none) and on `workflow-graph-versions.json`, the
  mark a column (a verification pipeline with no `check`, with a `handed`, with a `claimed`; a development pipeline with no landing
  still refused); a new workflow's first save writing its mark and a later save keeping it; the route's blob read from the tip and
  `none` with no declaration; a `check` declared `skippable: false` refused as a built-in's is, and a person's skip of one refused by
  its kind; `Involvement`'s tests for the route and verification rows, an agent's choice of each kept as a proposal; the tab's test
  and stories in both languages; `workflow show`'s golden.
- **1b** (driver, modules, web, cli). `sessionChecks` on both twins with its keeps rows; `checks` on `claimed` and on schema 1's `work`
  read by both twins; the start's words; `run-heavy`'s refusal; `SpawnSettings`' deny rules and sentence. Proof: a driven start's
  instruction golden under `light`; `run-heavy` from a held tree exits 1 with its sentence, from an unheld checkout runs; the spawned
  settings file's deny list names each heavy gate exactly and a filtered command passes; a Codex start under `light` writes no rules
  file and its words and Setup's row say the heavy gates are said, not refused; a repository marking none says so on Setup.
- **1c** (driver, web). Proof: a stand-in session whose call outlasts the bound is told once at urgent level, and never ended.
- **1d** (driver, modules, web, cli). Proof, a `Process` class on a scratch repository with stand-in gates: the passes go on while a
  check's gate runs a minute; the check's outcome reaching the record through the reconcile, under the `gates` source, with no
  completion heard; two verifications never overlap with each other or with a `run-heavy`, and wait in the order sent; a run whose
  repository's slot a `run-heavy` holds keeps another repository's run from nothing; a session's `run-heavy` behind a verification
  exits 1 with its sentence, and a person's waits; a failing gate's verdict, exit and log on the run's record; a runner's result read
  gate by gate, an empty `gates`, a missing gate, an undeclared name, another commit, another schema and a missing file each
  `unknown`; an opaque runner as one check and its exit 75 busy; `busy` waited out with the slots let go, then `unknown`; a changed
  declaration held `unknown` until accepted; a run past its `timeoutSeconds` ended `unknown` and held, its job empty before either slot
  goes, and never asked at the performer's deadline; the person's stop ending the job; a driver ended mid-run sending the same attempt
  again at its restart, under its key, and a job its name finds ended; the job's first process below normal and a `clock` gate at
  normal.
- **1e** (driver, web). Proof: two landings batched into one run; a third landing while it runs binding nothing until it settles,
  then a run at the newest tip; a run waiting for its slots verifying its bound tip; a line's first run with no base; an `unknown`
  holding the line, *Run again* a new attempt and *Not now* the next run's batch taking it in; landed runs read from their own folder,
  the history clear and `AnyNamed` passing over them, and a version a landed run binds kept past twenty; the clear keeping the newest
  run with a verdict.
- **1f** (driver, service, web). Proof: *Ask for a fix…* publishes one ask with the log as its file and the kind as its choice; a
  second failure of that gate adds to its channel; `failed: ask` publishes with no press only where the person set it; a restart
  between the send and its answer publishes once, the look finding the ask made.
- **1g** (records, tools, driver). Proof: the merge tool's tests (under `--daoris-verify` alone, the commit read from
  `DAORIS_VERIFY_COMMIT` and refused without it; the result written gate by gate under the declaration's names, with the full commit
  and each kept log's path; `busy` written on a held lock before the refusal; no release workflow step reported); `desktop-publish`'s
  test (a full set started during a stage refused by the lock); on the install, a merge batch verified by the install, its wall beside
  the sessions, and a day of DOGFOOD1 sessions with none ended by the timeout for a whole suite.
- **1h** (driver, plugins). Proof: a stage after a failed check, or one the person stopped, never starts, whichever wire it waits by;
  a stage whose route's declaration changed after its check is not started at dispatch.
- **1i** (driver, web). Proof: a route proposed with no version applies at the person's press; a verification graph proposed is
  judged by the mark; an agent's lowering is kept as a proposal.
- **1j**: its proofs as its row says once filed, a gate failed in one batch and unreached by the next still run there among them.

## 8. What this document's gate does not cover

Documents only; nothing is built. The statements about today were read from the code at `300626e52`, again at `3307f911f` and a
third time at `ff01a5881`, and none was run. No test shows a deny rule written from a declared command refusing it on both doors, a
check's job running while the passes go on, a check's outcome applied through the reconcile alone, an attempt sent again after a
restart re-running the same tip, two homes' runs of one repository held apart by its runner's lock, a stage holding the gate tree's
lock against a run, the merge tool reading a handed commit, or the cost of the line's ref read per look on a home with routes; 1b,
1d, 1e and 1g prove them. That a priority limit on a job would hold a raised process below normal is read from Fable's finding and
Windows' documented job limits, not run. The 20-minute call bound, the check's 7200-second default, `busy`'s six tries ten minutes
apart and the 256 KiB log are judgements, each a default the person may set, to be measured on the install (§5.3).

## 9. The reads

**Codex, 2026-10-21, on `60df3f6dd`.** Every finding was checked against the code at `3307f911f` before it was taken (D160's step 9).
All hold and are taken; none is dropped. Fable read the revision after it (the next table).

| # | Finding | Checked at | Verdict | Taken in |
|---|---|---|---|---|
| 1 | The harness's refusal is only words where the harness takes no rules: `TakesSettings` defaults false, Codex takes none, and composition is skipped for such adapters | `Adapters.cs:1221-1227`, `:1675`, `:1871-1872`, `:2104`, `Driver.cs:2395` | holds: only Claude Code takes them | §3.2's third layer, where the harness takes rules and said where it does not; 1b's proof |
| 2 | The gate split omits `rehearse` and `rehearse:family`, which the merge runner adds from the release workflow | `merge-branch.mjs:708-741`, `release.yml:297`, `:302` | holds | §5.1: both declared heavy, under the names the tool gives them |
| 3 | *Ends within a minute* overstates DONEEND1: four polls, a turn in flight, and the case that holds to the timeout | `Driver.cs:3445-3450`, `Acp.cs:823-838`, D83's DONEEND1 note | holds; with no background work DONEEND1's look has nothing to wait for, and D83's remaining case stands | §3.2, §5.3, reworded |
| 4 | A check-only workflow breaks D190's one-landing rule; tell verification workflows apart in both readers, never by a dummy landing | D190 point 4, `WorkflowNamed.cs:248-261`, `namedworkflows.ts:275-288` | holds | §2.1: the file's `pipeline` mark; D190 point 4 amended by D192 |
| 5 | A skipped check satisfies `after`, so a stage after it does not require a pass; production may never bypass a prerequisite check | `NodeWires.cs:9`, `WireEvaluation.cs:634`, the workflow design §3.7 | holds | §4.6: the verdict by a `data` wire, the check not skippable, the exact commit read at dispatch |
| 6 | The lowering rows miss a kept route whose gates or check are removed, and `failed` moved to `stop` | D157 point 10, `WorkflowGate.cs:240-271` | holds | §1.3's table |
| 7 | The acceptance digest leaves out `heavy`, which decides what `gates: heavy` runs | `HeavyGates.cs:78-89` | holds; a runner runs every declared gate besides | §4.2: the declaration's gates and runner digested whole |
| 8 | The pass holds its semaphore while it awaits a start, and the performer starts plugin nodes alone: a check must launch and report asynchronously, with its ownership and cancellation kept | `NodeRuntime.cs:388`, `:560`, `WorkPerformer.cs:252` | holds | §4.2: the heavy executor as the performer's own port, off the pass |
| 9 | `timeoutMinutes` is not the node's `timeoutSeconds` the performer reads; say the deadline's owner, when it counts, and how it ends before either slot goes | `WorkPerformer.cs:126`, `:885`, `NodeGraphs.cs:606` | holds | §4.1: the node's `timeoutSeconds`, 7200 by default; §4.2: counted from the slots held, the executor its owner |
| 10 | The *machine-wide* slot sits under one home, and the home's lease holds one writer per home only | `HeavySlot.cs:100-104`, `HomeLease.cs:52-55` | holds | §4.2: a slot per home, said so; two homes held apart by a runner's own lock; §6 |
| 11 | The reused tree moves between tips while the stage builds from it, its state checked only before | `desktop-publish.mjs:867-894`, `:1028` | holds for the merge tool's gate tree, which its runner moves | §4.6, §5.1: the stage holds the gate tree's lock through its build |
| 12 | A first verdict's base and age when none exists | the first reading's §4.3 | holds | §4.3 |
| 13 | The order slots are taken and checked again; holding one while waiting on a repository blocks another | the first reading's §4.2 | holds | §4.2: one order, neither waited for, the tip read again once both are held |
| 14 | One durable transition per line, so a late result never replaces a newer batch's state, publishes a stale fix ask or frees a successor's resources | the first reading's §4.3 | holds | §4.3: the line read from its runs, each change one edit of a run's record; §4.5's key |
| 15 | Rows 1a and 1d need the `landed` route and its digest, which arrived in 1e | the first reading's §7 | holds | §7: storage, validation and acceptance in 1a; triggering and batching in 1e |
| 16 | CUTOVER1c starts no performed node and has no run view and no skip or stop | the cutover design §11.1 | holds | §7: 1d after CUTOVER1d, which follows 1c, 1e and 1f |
| 17 | The lowering guards land in 1i, after the rows that introduce what they guard | the first reading's §7 | holds | §1.3, §7: each guard with the first row that lets its change be set (1a, 1b) |
| 18 | A runner has no result contract: it answers one exit and keeps its own verdicts | `merge-branch.mjs:3339-3366` | holds | §4.2: `DAORIS_VERIFY_RESULT`, else an opaque runner as one check; §5.1 |
| 19 | A route field does not enable graph proposals: the proposal requires `steps`, read as schema 1 | `HelpWorkflowProposals.cs:70`, `:93` | holds | §1.3; 1i: a route with no version, and a graph beside `steps` |
| 20 | TREEDEPS1 copies nothing where the lockfiles differ: say the fallback and who allows it | `TreeDependencies.cs:99-127` | holds | §4.2: no install of Daoris's own; a declared gate installs |
| 21 | 1j's reach by later paths drops a failed gate the batch base has moved past | the first reading's §4.3, §4.7 | holds | §4.7: a gate not passed stays owed |
| 22 | Simpler: the runner owns its checkout, Daoris's tree only the direct-gate fallback | `merge-branch.mjs:4055-4112` | taken | §4.2 |
| 23 | Simpler: one shared asynchronous executor from `run-heavy`'s and a small durable batch supervisor, the choice table the drawn start | `HeavyRun.cs:196-297`, `merge-branch.mjs:4055` | taken: the executor is `run-heavy`'s made asynchronous; the supervisor keeps no record of its own and reads the line from its runs | §4.2, §4.3 |

**The choices the second round made**, where two shapes remained: the mark on the workflow file, not per version and not read from
the graph (§2.1); a stage's verdict by a `data` wire and a read at its dispatch, rather than a wire kind of its own, since `data`
already refuses a skipped source (§4.6; the third round drops the wire's rule, row 52); the stage holding the gate tree's lock
rather than a checkout pinned per stage (§4.6); a slot per home rather than one outside every home (§4.2); a runner's result
optional, an opaque runner one check, so any script serves (§4.2); the executor inside the driver on its own task, its job ending
with the driver, rather than a child process with an ownership protocol of its own (§4.2); the line read from its runs rather than
kept in a record of its own (§4.3); the declaration digested whole rather than a runner's resolved plan, which a runner would have to
print before it runs (§4.2; the third round takes its blob, row 51); and, on a harness that takes no rules, the heavy gates said
rather than the harness refused (§3.2). After that round the design still changed a foundation, so Fable's read of §2 to §4 was
owed.

**Fable, 2026-10-21, on `04112c560`, reading §2 to §4.** Every finding was checked against the code at `ff01a5881` before it was
taken (D160's step 9). All five blocking findings and all nineteen others hold, two in part (rows 24 and 38, whose exemplar or
premise is corrected); none is dropped. The five simpler shapes are taken, the first made smaller. Rows 53 to 57 are the steward's
own, found while checking.

| # | Finding | Checked at | Verdict | Taken in |
|---|---|---|---|---|
| 24 | (B) A built-in `check` cannot carry `skippable: false` or a `perform`, and the reconcile needs `node.Perform.Reconcile`: never skippable by its kind, its `perform` given as Daoris gives `quest.close`'s | `NodeContract.cs:476`, `:480-482`, `:409-413`, `WorkPerformer.cs:600` | holds; its exemplar does not: `quest.close` is performed by its own closer beside the performer and carries no `perform` (`QuestCloser.cs:298-310`, `:496`), while the runtime gives a plugin's node its step's (`NodeRuntime.cs:297-303`), the shape taken | §4.1 |
| 25 | (B) A landed run's state lives in bindings the history clear removes, and `AnyNamed` turns true once one is bound, holding a Current repository's gate reads where the service does not answer: a folder of its own | `HistoryHome.cs:158-167`, `WorkflowRunFiles.cs:60-83`, `WorkflowProcesses.cs:97-99` | holds | §4.3: `<home>/workflows/lines/` |
| 26 | (N) The `pipeline` mark has no path into the version readers, and a new file is written as `{ id, name, versions }`: say where it is set, how it reaches both readers and the fixture, and whether it may change | `WorkflowNamed.cs:201`, `:232`, `:649-652`; `namedworkflows.ts:253`, `:878` | holds | §2.1: set at creation, threaded into both readers, a column of the fixture, kept by every later save |
| 27 | (N) A repository naming only `landed` loses its workspace's development mapping | `WorkflowSelection.cs:334-338` | holds for this build; an older build passes over an entry naming nothing (`:261`) and follows the workspace | §1.2: `landed` resolves on its own |
| 28 | (N) A landed run's id must pass `IsRunId` and stay unique under *Run again*; the run unit has no revision and the evaluation needs one: say which unit the check observes | `WorkflowRunBindings.cs:42`, `WorkUnit.cs:16-19`, `WireEvaluation.cs:349-354` | holds | §4.3: `v-` and sixteen hex digits; every node observes `run:<run>` at its bound tip |
| 29 | (N) `settle` is an act on the run held in a node's option: put it on the route or the workflow file | the second reading's §4.1 | holds; moot once nothing is superseded | removed (row 48) |
| 30 | (B) There is no `superseded` to write, Daoris cannot write `stopped`, its own `invalidated` would start the check again at the same tip, and the record holds no run's end | `NodeContract.cs:113`, `NodeLifecycle.cs:283`, `:286`, `:400-401`, `NodeRecord.cs:160-191` | holds; the run's end is CUTOVER1c's to build (the cutover design §3, §11), not CUTOVER1h's as the finding names it | §4.3: each run to its verdict; its end by the cutover design §3's rule |
| 31 | (B) An `unknown` run is bound again and again at the same tip, up to 7200 s each, since the base moves only on a verdict | the second reading's §4.3 | holds | §4.3: an unknown holds the line for the person |
| 32 | (N) A start that never ran should answer `not-started`, sent again under its key, never held | `WorkPerformer.cs:649-651`, `:662-664` | holds, and a check cut off before its result is one that never ran | §4.2: the restart's answer |
| 33 | (N) Two homes on one machine each run a full set for a repository with no runner: narrow D180's one CI run at a time, or key the slot by machine | `D180.md:68-69`, `HeavySlot.cs:115-118`, `:131` | holds: the heavy slot's key names the repository on the machine, but its lock file is the home's | §4.2: narrowed to one per home and said; §6 |
| 34 | (N) A session's `run-heavy` waits behind a 7200 s verification with no bound, inside a 90-minute timeout | `HeavySlot.cs:171-206`, `HeavyRun.cs:223` | holds | §3.2, §4.2: it never waits behind one |
| 35 | (N) *Heard as a plugin's is* is not reuse as is: the hearing refuses a sender that is not the attempt's plugin, and the start reads the plugin from the kind's slash | `WorkBelief.cs:27-36`, `WorkPerformer.cs:264` | holds | §4.2: the answer through the reconcile alone; the attempt names the executor |
| 36 | (N) The deadline's reconcile records the node unknown and active again and puts the next ask a whole bound later | `WorkPerformer.cs:590-593`, `:657-661` | holds | §4.2: never asked at its deadline while the executor runs |
| 37 | (N) A priority limit on the job would hold the clock-bound lane's raise below normal: lower the first process's class, as CIPRIO1 does | `merge-branch.mjs:3993-4008`, `:4025-4040` | holds, read from Windows' job limits and not run | §4.2 |
| 38 | (N) The verification tree is a stray to the tidy, and the slot's holder record needs the run | `BranchTidying.cs:92-108`, `HeavySlot.cs:19-20` | holds in part: the tidy keeps a tree git lists and moves aside only a folder with none, or a cut-off opening (`TreeStrays.cs:48-57`); but the design named no holder for its tree, and `HeavyHolder` has no field for the run | §4.2: held as a replay's tree is (`PassLeftovers.cs:14-18`); `HeavyHolder` gains `run` |
| 39 | (N) *Publishes once* across a restart: an ask is sent with no key of the client's | `ServiceClient.cs:698-704` | holds | §4.5: a look at the asks Daoris wrote, first |
| 40 | (B) The gates a runner reports are never held to those accepted, so an empty `gates` reads as reached | the second reading's §4.1, §4.2 | holds | §4.2: every gate the check names must be there; an undeclared name is `unknown` |
| 41 | (N) `outcome` and the gates' verdicts can disagree: judge by the gates, each verdict defined | the second reading's §4.2 | holds | §4.2: `outcome` removed, five verdicts defined |
| 42 | (N) The merge tool slugs and numbers names and adds the release workflow's steps: the declaration's names, verbatim | `merge-branch.mjs:724-727`, `:733-735` | holds | §4.2, §5.1 |
| 43 | (N) `commit` is the full id, compared exactly; the tool names folders by seven characters | `merge-branch.mjs:4145` | holds | §4.2, §5.1 |
| 44 | (N) A missing file reads the same for an opaque runner and a broken one | the second reading's §4.2 | holds | §4.2: `"result": true` declares a runner that answers by file |
| 45 | (N) `busy` is exit 2 today with nothing written; reserve an exit for an opaque runner's | `merge-branch.mjs:581-587`, `:3776`, `:4146` | holds | §4.2: written before the refusal; 75 for an opaque runner; waited out by the executor |
| 46 | (N) Another `schema` is `unknown`, named; a log's path absolute and final, since the tool copies its logs out of the tree | `merge-branch.mjs:3965-3970` | holds | §4.2, §5.1 |
| 47 | (N) `DAORIS_VERIFY_COMMIT` left in a person's or a session's shell retargets `--full --beside` | the second reading's §5.1 | holds | §4.2, §5.1: read only under `--daoris-verify`; set on the job alone |
| 48 | Simpler: supersede only before the job starts; a run whose job began runs to its verdict | `NodeContract.cs:113`, `NodeLifecycle.cs:283`, `:286` | taken, and made smaller: no run is replaced before its start either, since a replaced run would need an end Daoris writes for a run none of whose nodes moved, and at most one run per line waits | §4.3; `settle`, the end of a job in flight and the late-result rules gone |
| 49 | Simpler: answer through the reconcile, not the hearing | `NodeRuntime.cs:456`, `WorkPerformer.cs:643-644` | taken; the executor tells the performer, in the same process, rather than writing the attempt's ask time, so the record's edits stay the performer's | §4.2 |
| 50 | Simpler: landed runs in a folder of their own | as row 25 | taken, as one folder: a run's file names its repository | §4.3 |
| 51 | Simpler: the declaration's blob at the tip as its digest | the second reading's §4.2 | taken: no canonical form for two twins to agree on, and git shows the change; an edit that changes nothing that runs asks again | §1.2, §4.2 |
| 52 | Simpler: the check never skippable by its kind, *Run again* and *Not now* the person's doors, `settle` on the route | `NodeContract.cs:409-413` | taken; `settle` went with row 48, and with the skip went the rule that a stage takes a check's verdict by `data`, since `after` from a check that cannot be skipped holds only on its reach | §4.1, §4.6, §1.3 |
| 53 | The performer applies an outcome under its plugin's source, which the one table refuses for a kind reached by another | `WorkPerformer.cs:703`, `:710`, `NodeLifecycle.cs:357`, `:372` | the steward's, checking row 35 | §4.1 |
| 54 | A reconcile's `done` carrying unknown leaves the attempt unknown, asked again at every pass | `WorkPerformer.cs:714-722`, `:568-574` | the steward's, checking row 31 | §4.2: an unknown verdict answered `unknown`, held |
| 55 | The versions a kept run names are read from `runs/` alone, so a version a landed run binds could be pruned under it; arming lists `runs/` alone | `WorkflowRunFiles.cs:85-105`, the cutover design §3 | the steward's, checking row 25 | §4.3 |
| 56 | *Happened* and *did not* on a held attempt are a plugin node's alone, so a held check's doors are a new attempt and a stop | `NodeContract.cs:396`, `NodeLifecycle.cs:340` | the steward's, checking row 31 | §4.1, §4.3 |
| 57 | D180's proposed note held its first wording twice, a copy the second round left | `D180.md:382-386` | the steward's, in the records | D180's note |

**The choices this round made**, where two shapes remained: no run superseded at all, not even one waiting for its slots (rows 30,
48); an unknown holding the line for the person rather than settling its batch (row 31); the answer told to the performer in the
same process rather than written as the attempt's ask time (row 49); one folder of landed runs rather than one per repository (row
50); the declaration's blob rather than a canonical digest, at the price of accepting again an edit that changes nothing that runs
(row 51); D180's one run at a time narrowed to one per home and said, rather than a slot keyed by machine, which round two rejected
for a test's scratch home (row 33); a session's `run-heavy` refusing to wait behind a verification, rather than the verification
giving up the repository's slot, which would let a session's full set and a verification run at once (row 34); busy waited out by
the executor rather than the performer's retry, whose failure reads as a verdict (row 45); and the stage's wire left free (row 52).

**Whether it still changes a foundation, and what is left.** The third round removed mechanisms and changed none of the model the
reads tested: a route that only selects, a pipeline marked on its file, a check performed through the performer's attempt and
reconcile by `run-heavy`'s executor, a run on the line read from its runs, a slot per home and a result contract. What remains of the
foundation is what the two reads already tested. So, as D190's third round was, the design is ready for the steward to accept once
each change above is verified as taken, with no further pair of reads. Nothing in it is the owner's under D191 point 3: it changes no
direction, spends no account beyond the development's own runs, signs nothing in, publishes nothing and loses no data. One amendment
touches a rule written under the owner's direction, D180's budget note: its point 2's run *stopped rather than finished* when a newer
tip supersedes it becomes, for the verification pipeline, a batch run to its verdict with the next batch taking what landed, and its
one run at a time becomes one per home. Each keeps the note's purpose (a CI run is a guest that never blocks the work), so it is the
steward's to settle, and the owner may reopen it (D191 point 3).
