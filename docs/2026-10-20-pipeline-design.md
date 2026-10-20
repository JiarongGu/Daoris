# Pipelines: a workflow's start node routes each piece of work, and verification is a pipeline of its own (proposed, 2026-10-21)

**Standing:** proposed under D192 (FABRIC-PIPELINE1), for the reads D191 asks. Codex reads it, as it reads every design. The author
judges that §3.4 and §4 change a foundation in D191's amendment's sense (a run kind on a repository's line beside D190's runs of a
chain, a node Daoris performs itself, and a second slot beside the heavy-run slot), so Fable is asked to read §2 to §4 too; the
steward decides. Nothing here is built; its rows (§7) build it. Nothing in it waits for the owner (D191 point 3): it publishes
nothing, spends no account beyond the development's own runs, and loses no data.

Read for it: D173's direction note of 2026-10-20 (FABRIC-PIPELINE1); D157 and the workflow design §3.2-§3.7 and §4; D190 and the
cutover design §2, §3, §8 and §10; D180 and its notes (the CI beside, CIPRIO1 to CIPRIO1e, the HEAVYRUN1 notes, FAMILYFAIL1b to
1d); D115's GATEWT1 and GATESLIM1 notes and `docs/development.md`'s *Integrate and record*; HEAVYRUN1a's archive entry; D83's
BGWAIT and DONEEND1 notes; D188; D154 and the review environment design §3.4; the verification lanes design (FABRIC-VERIFY1);
the UI/UX design §3.5 (D185); `.claude/knowledge/development-documents.md`'s *What may run unasked is a declaration*. Every
statement about today was read from the code at `300626e52` and names its file and line; none was run.

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
| A kind's paths never choose: they hold a run at its gate only where the kind's workflow lowers the person's part, which `Involvement.Lowers` judges step by step | `WorkflowGate.cs:26`, `:234-240`, `:315` |
| Current's live graphs are chosen by the kind of unit: a branch's from its landing rule, a quest's or an ask's from `workCycles` | `NodeGraphs.cs:705`, `:732-757`, `:771-784` |
| A graph is `{ schemaVersion, nodes, wires, entries, end, cleanup }`; every entry starts; no wire chooses one way among several | `NodeGraphs.cs:28-46`, `NodeWires.cs:7-46` |
| The units are a branch, a quest, an ask and (CUTOVER1a) a run; nothing projects a repository's line as a unit | `WorkUnit.cs:4-20` |
| The built-in kinds hold no `check`; the one built-in Daoris performs itself is `quest.close`; schema 1's `check` is a plugin's and not run, and D190 §10 makes `check`, `stage` and `all` graph nodes only | `NodeContract.cs:11`, `:24-68`, `:431`, `WorkflowNamed.cs:79-84` |
| A performed node's default timeout is 15 minutes | `WorkPerformer.cs:126` |
| `daoris.gates.json` marks a gate `"heavy": true`; `run-heavy` runs the heavy gates in declared order, holding one slot per repository across its trees, every gate in one kill-on-close job | `HeavyGates.cs:47-94`, `HeavyRun.cs:26-63`, `HeavySlot.cs:32-53`, `:85-104` |
| This repository marks no gate heavy, since marking them would have had each session run its suites at its end (D180's HEAVYRUN1c note) | `daoris.gates.json:4-21` |
| A driven session is told, as a sentence, to run its heavy gates once at its end through `run-heavy`, where its repository marks some | `HeavyTold.cs:35-46`, `Driver.cs:2016-2018` |
| A session's rules are composed per spawn: deny rules beside `no-push`, a sentence for auto mode's classifier, and the tree guard, which judges Edit and Write and never a shell command | `Permissions.cs:145-171`, `:188-193`, `SpawnSettings` at `Permissions.cs:503-532`, `Driver.cs:2422-2429`, `TreeGuard.cs:21-41` |
| The call a live session's agent is inside is heard with its start; a protocol door's background work is heard by task | `RunningCalls.cs:5-24`, `AcpBackground.cs:6-41` |
| The cap counts sessions across repositories; a session's timeout is the driver file's | `DriverConfig.cs:15-17`, `:59-60` |
| A merge blocks on `universal`, `code-map` and `orient-index` alone; every other gate runs in the CI beside | `merge-branch.mjs:781-786` |
| `--full --beside` lowers itself below normal with build servers of its own, raises its clock-bound lane, and a run of main's tip supersedes itself on any move of main, ending its step trees | `merge-branch.mjs:3921-3943`, `:3935`, `:3967-4004`; D180's CIPRIO1c note |
| The merge tool reads a gate's `name`, `run`, `cwd` and `clock`; the devkit and the driver pass over keys they do not know | `merge-branch.mjs:744-764`, `GateDeclaration.cs:150-187`, `HeavyGates.cs:35-39` |
| One verdict store per repository, keyed by tree; the install's stage requires the full set's verdict of the commit it publishes | `merge-branch.mjs:1270-1283`, `desktop-publish.mjs:38`, `:1012-1015` |
| The driver publishes an ask to a repository with files and a workflow choice; a workspace plan's tick publishes the ask its press stood for | `ServiceClient.cs:698-704`, `AskWorkflowCommand.cs:6`, `SetupPress.cs:165-178` |
| Ask Daoris proposes a workflow and its choice, applied by the person's press | `HelpWorkflowProposals.cs:11-12`, `:57` |

## 1. The start node

### 1.1 What it routes on

**Decided.** The start node routes each piece of work that arrives at a repository into one pipeline, on facts only:

| What arrives | Routed by | Read from |
|---|---|---|
| A chain's work (its quests in this repository) | D157 §4.1's order, unchanged: the person's choice for the task, the repository's for the task's kind, its default, the workspace's for the kind, its default, Current | the ask's `workflowChoices`, `driver.json`'s choices (`WorkflowSelection.cs:325-343`) |
| Landed work (the repository's line moved past the newest tip that has a verdict) | the repository's `landed` route (§1.2), else none | git's ref of the line, read in the pass |
| A person's ask to verify a branch or a commit | the same `landed` route, as a run of its own | the person's press or verb (§4.3) |
| A fix Daoris asks for after a failed verification | the kind the verification pipeline names for it (§4.5), routed as any task of that kind | the ask's choice, set by Daoris from the person's version |

- **The kind is the person's.** It is set by the person, or applied from the intake's proposal by the person's press (WORKFLOW1i-a). An
  agent sets one only on the person's quoted words where `Involvement.Lowers` finds nothing lowered (D157 point 10).
- **Paths never route.** The route is chosen at the bind, before the work changed anything. A kind's declared paths hold the run at
  its gate where the kind's pipeline lowers the person's part, as today (`WorkflowGate.cs:315`), and are drawn on the route. A kind
  inferred from the changed paths stays rejected (D157). Landed work is routed whole and never split by its paths: which gates a
  batch reaches is the check's (§4.7).
- **Never a model's judgement.** No route reads a conversation, an agent's words or a model's reading (D24, D54). The no-model
  tier routes everything (D157 point 14).

### 1.2 Where it sits: before the graph, kept on the binding

**Decided.** The start node is not a node of any version's graph. It is the choice table D157 point 5 already keeps (`workflows`
by repository, `workspaceWorkflows` by workspace), with one route added, resolved where the run is bound. It is drawn as one graph
with the pipelines it routes to (§1.3), and each pipeline is versioned and bound alone (§2).

- **The shape.** A repository's or a workspace's choice gains `landed`: `{ "workflow": "<id>", "gates": "<digest>" }`, the
  verification pipeline for work that reaches the line, and the digest of the gates the person accepted for it to run unasked
  (§4.2). `"current"` there is none, today's. A repository that names any route replaces its workspace's whole, `landed` among them
  (`WorkflowSelection.cs:93-95`). A choice with no `landed` is today's. Both twins read a choice by its `default` and `kinds` and
  pass over the rest (`WorkflowSelection.cs:183-207`, `workflowchoice.ts:129-151`), so an older build reads no route there; but a
  choice naming only `landed` is one that *names nothing* to both (`WorkflowSelection.cs:25`, `:261`), which the build changes, and
  an older build that writes a choice writes its `default` and `kinds` alone (`:283-315`), dropping the route: a downgrade's cost,
  said where the route is set.
- **When it is read.** A chain's route at the binding moment D190 §8 placed after the start's tree opens and before its take; a
  landed route when the pass reads the line's tip past the newest verdict (§4.3).
- **What the binding keeps.** The level, the kind, its label and paths, as now (`WorkflowRunBinding.cs:11-33`), and `route`
  (`work` or `landed`); a landed run's binding adds its line, its tip and its base (§4.3). `WorkflowRunBinding.Why`
  (`:54-66`) gains the landed route's sentence: *verifies `main` at `a1b2c3d` · this repository's landed work*.
- **Versions.** A route is a live choice, as every choice is: changed, it applies to the next run, and a run in flight keeps the
  pipeline and version it bound (D157 point 6; D190 §8, the choice read once at the bind).
- **Twins.** `WorkflowSelection` and the CLI's `workflowchoice.ts` read and write `landed` from one more row set on
  `fixtures/workflow-selection.json`.

### 1.3 How a route is shown and changed

- **Shown.** A repository's and a workspace's *Workflow* tab (`WorkflowTab.tsx`, WORKFLOW1b) draws the start node first: a row per
  route (each kind by its label with its paths, the default, `landed`), each wired to the pipeline it names, laid out by D186's
  `Flow`; a route naming nothing says what follows then (Current, or *no verification here*). A run's *Workflow* view
  (`WorkflowRunView.tsx`) heads with the route that chose its pipeline. In a terminal, `daoris driver workflow show --repository
  <name>` prints the routes before the steps.
- **Changed.** At Setup through the tab's doors, and in a terminal by WORKFLOW1e's verbs: `daoris driver workflow use <id>
  --repository <name> --landed` (a flag beside `--kind`), which asks the person to accept the gates the line's tip declares (§4.2)
  and refuses a workflow whose newest version holds no `check`; `kind` declares a kind as now. The editor (WORKFLOW1g) edits routes
  once built. Ask Daoris proposes a route as `workflow_propose`'s `choose` does today (`HelpWorkflowProposals.cs:57`), with a
  `route` field, and the person's *Apply* sets it.
- **The person's part is never lowered by an agent** (D157 point 10). `Involvement.Lowers` (`WorkflowGate.cs:240`) gains rows: a
  `landed` route removed, or set to `current`, where one stood; a verification pipeline's `failed` moved from `wait` to `ask`; a
  development pipeline's `checks` set to `light` where no `landed` route runs the heavy gates it gives away. Each is a lowering: an
  agent's choice of one is kept as a proposal, and only the person's press applies it.

## 2. What a pipeline is: a named workflow, chosen by its route

**Decided.** A pipeline is a named workflow (D157 point 5's file, `<home>/workflows/<id>.json`), its versions graphs (schema 2)
governed by the node runtime under D190. A run binds one version of one pipeline; the start node says which. On the *Workflow* tab
the start node and the pipelines it routes to are drawn as one graph, so the person configures one picture that begins at the
start node, while the engine binds each part alone.

**Why.** Development and verification differ in their unit. A development run is a chain's work, named by its first quest here
(`WorkflowRunBindings.cs:52-53`), its nodes observing the run's quests and branch (D190 §2.2). A verification run is a batch of
landed work on the line, named by the tip it judges (§4.3). A run is the unit of its node record (D190 point 2), so one graph
cannot be both, and a route that only selects needs no node in either.

**The others, and what they would cost.**

| Option | What it is | What it costs |
|---|---|---|
| A sub-graph | One version holds a route node and nested graphs, one per pipeline | A third schema in both twins' readers, digests, validators and decoder (`NodeGraphs.cs:28-46`); D190's one landing, one opinion and one look per version (cutover design §4) restated per sub-graph; a record holding nodes of two unit kinds, or an outer graph no run ever evaluates, which is the choice table in a graph's clothing |
| A path through one graph | One graph per repository; the start node a node whose wires lead to each pipeline's first nodes | A wire that chooses one way among several, which no wire kind does (`NodeWires.cs:7-46`) and D157 §3.3 rejects (*a branch is an outcome, never an expression*); a state for a way not taken, which the lifecycle lacks (`skipped` is the person's word with words, `NodeContract.cs:104-105`); one record for a chain's run and a line's run; `Involvement.Lowers` made path-aware; every edit of either pipeline a new version of both, and Ask Daoris's diff mixing them |
| **A named workflow chosen by its route** | The choice table routes; each pipeline is a workflow of its own | A route is not diffed as a version: a run's binding records what chose it, which is what a run needs; one more route key in the twins; a run kind on the line (§4.3) |

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
   from a variable it could leave out. A person's `run-heavy` from a checkout
   no session holds runs as today.
3. **Refused by the harness, exactly.** Each heavy gate's declared command joins the session's deny list, `Bash(<run>)` and
   `Bash(<run> *)`, beside `no-push` (`Permissions.cs:158-165`), and auto mode's classifier is told one sentence naming the heavy
   gates as the verification pipeline's (UNBLOCK4's channel, `Permissions.cs:188-193`, `:523-532`). Only declared commands, exact
   (development-documents: *exact commands, one per entry*). HEAVYRUN1's rejection of guessing a heavy run from command text
   stands, so a whole suite spelled another way (a test project with no filter) is not refused by its text.
4. **Said again while it runs.** A running call or a background task past `callMinutes` is said to the session once, at urgent
   level, as Daoris's own word on a fact that makes the work in flight wrong (D188 point 2): the call's title and time, and that a
   suite run whole is the verification pipeline's. It is heard where the calls already are (`RunningCalls.cs`,
   `AcpBackground.cs`). Daoris never ends the call: a turn is the agent's (DONEEND1), and the person's *Stop* stays the door.

Against the case: the three sessions would have been told by their graph, refused `run-heavy` and the declared commands, and told
again at twenty minutes; the one that still ran its suite would have been told, and seen on the rail with its time (WAITSAY1).
With no heavy run in its background, a session that reaches done ends within a minute (DONEEND1) and frees its place in the cap.

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

**Decided.** A built-in kind, `check`, performed by Daoris itself as `quest.close` is (`NodeContract.cs:11`, `:431`), its source a
new `gates`: the exits of the declared gates as the driver ran them. It is not an agent session: no harness, no account, no model,
and no place in the cap.

| Option | Values | Default |
|---|---|---|
| `gates` | `heavy` (the declaration's heavy gates, or its `verification` runner where it declares one), or gate names | `heavy` |
| `timeoutMinutes` | 1 to 480 | 120, since a full set outlasts a performed node's 15 (`WorkPerformer.cs:126`) |
| `failed` | `wait`, `ask`, `stop` | `wait` |
| `ask` | a kind the workspace declares: the kind a fix ask carries (§4.5) | none: the repository's default route |
| `settle` | minutes a line may go without a verdict before a run is no longer superseded (§4.3) | 60 |

- **Its authority** (§E2's table): performed; reached when every gate it ran passed at its subject's commit; failed when one failed,
  naming it with its exit; `unknown` for anything else (no tree, no declaration, a declaration not accepted, a runner that refused
  for its own lock), never passed or failed. The person may skip it with their words; it is no proof of git's.
- **Where it may stand.** In a verification pipeline on the line (§4.3), its first use. In a development pipeline on the run's branch
  too: before `handed` it blocks the landing (a sensitive scan, D180 point 1); placed beside, not wired into `handed`, it is
  FABRIC-VERIFY1's lane (the verification lanes design §1, §4), whose first slice may use it.
- **A service's checks stay a plugin's** (`<plugin>/<node>`, D190 §10, FABRIC-PERFORM1l). Schema 1's `check` (`WorkflowNamed.cs:79-84`)
  stays unrun.

### 4.2 What it runs, where, and with what say-so

- **The commands.** The repository's `verification` runner where its declaration names one (`{ "run", "cwd"?, "clock"? }`: one command
  that runs every gate in its own lanes, as this repository's merge tool does, §5); else the named or heavy gates one after another,
  the first failure ending the sequence, as `run-heavy` runs them (`HeavyRun.cs:37-38`); else this machine's mapping for a repository
  that declares none (HEAVYRUN1b2a), which is the person's already.
- **The person's acceptance.** D154's review design refuses a command read from the repository's files (review environment design
  §3.4: *a guess*), and a declaration a session can edit and have obeyed is an agent granting itself permission
  (development-documents). So the gates run unasked only as the person accepted them: the `landed` route keeps the digest of the
  runner and gates (each name, command and folder) the person accepted when they chose the route (§1.2). A batch whose tip declares
  others is `unknown`, *the gates changed since you accepted them*, with the change shown and *Accept the gates*, the person's door
  (D156), which writes the new digest; nothing runs meanwhile. A landed edit of `daoris.gates.json` never runs by itself.
- **Where.** In a verification tree of its own per repository, detached at the run's tip, made once and moved to each tip as
  GATEWT1's gate tree is (D115's GATEWT1 note), its dependencies copied as a session's tree's are (TREEDEPS1), never the main
  checkout (D180's rejection).
- **The slots.** The repository's heavy-run slot (`HeavySlot.cs:32-53`), so no session's `run-heavy` runs beside it; and one
  verification run on the machine at a time (a second slot under the home's `locks/`), since a CI run is one guest at a time (D180's
  budget note, point 2). A run waiting for either says behind what, on its run and in the repository's *Details* (D185 §3.5's
  *Heavy run* row, the verification run as its holder).
- **The job and its environment.** One kill-on-close job, as `run-heavy`'s (`HeavyRun.cs:40-44`), handed `DAORIS_HEAVY_RUN`, the run's
  tip as `DAORIS_VERIFY_COMMIT`, and `BESIDE_ENVIRONMENT`'s three variables (`merge-branch.mjs:3935`), so its builds keep servers of
  their own and nothing shuts a server down (SHUTDOWNBESIDE1).
- **Its priority beside the owner's window.** Below normal for the job (CIPRIO1); a gate or runner marked `clock` raised to normal,
  as FAMILYFAIL1b to 1d raise the clock-bound lane; never counted against the cap, as a CI run beside never lowers the workers
  (the dispatch skill's step 5; FAMILYFAIL1b). I/O priority stays CIPRIO1b's. The window and the sessions keep their priority.
- **Its timeout** is the node's `timeoutMinutes`; past it the job is ended and the check is `unknown`, *ran past its bound*.

### 4.3 A run per batch: what it takes, how it batches and supersedes

- **What it takes.** By default the repository's line (its landing rule's target, this repository's `main`) at its tip. A person may
  ask it of a branch or a commit, `daoris-driver verify <repository> [--branch <name> | --commit <sha>]`, and a *Verify…* door on
  the tab: a run of its own, never superseded, as CIPRIO1c's run given a commit polls nothing.
- **The run.** A verification run is the runtime's unit `run:<run>` (D190 point 2), named by its repository, its line and the tip it
  binds. D190 §2.2's table gains a row: every node of a landed run observes or acts on **the line at the run's tip**, the revision
  fixed at the bind, never a live read, as the nodes after `handed` bind the landing's tip. Its binding keeps the route, the line,
  the tip, and its **base**: the newest tip on the line with a verdict, passed or failed. Its batch is the commits from base to tip,
  and the landings that carried them.
- **Batching.** One run per line at a time. When the line's tip moves past base and none runs, a run binds the newest tip, so every
  landing since base is in it; landings that arrive while it runs are the next batch's.
- **Superseding.** A run of the line whose line has moved on is superseded within one look: Daoris ends its job (every process its
  gates started, by the job, never by image name, as CIPRIO1c ends its step trees), records the run `superseded` (never passed,
  never failed), and binds the next at the newest tip. It is an act on Daoris's own attempt, which no plugin's performed node has. A
  runner that keeps a lock of its own takes it over at its next start (CIPRIO1d, CIPRIO1e: `merge-branch.mjs:3684`).
- **A verdict is never starved.** A run is superseded only while the line's newest verdict is younger than `settle`; past it, the
  run in flight runs to its verdict, and the next batch takes all that landed meanwhile. Today `watchTip` supersedes on every move
  (`merge-branch.mjs:3974-3991`), so a line that moves faster than its full set never gets one.
- **Arming and its cost.** A `landed` route arms the runtime (`NodeRuntime.cs:222-226` gains it), and each look reads the line's ref
  of each such repository (one ref read, or the fabric's ref watcher where it serves).
- **A restart or a drain.** A run in flight when the driver ends loses its job with it: its check is reconciled `unknown`, and the
  next look binds a run at the newest tip. The staged update's drain (D139) ends a verification run the same way rather than waiting
  for it.

### 4.4 Its verdict, recorded and shown

- **Recorded** on the run's node record: the check reached, failed or unknown, its evidence each gate's name, exit, wall and log.
  The logs are kept beside the binding, `<home>/workflows/runs/<run>/`, and cleared with the run's history (D153, as WORKFLOW1e3
  clears a binding). *Whether a commit is verified, and for which gates* is one reader over the runs bound on a line, which the page
  and any door ask.
- **Shown.** The repository's *Workflow* tab: the verification pipeline's newest runs, each its tip, its batch, its state, its wall,
  its gates and a door to each log. The run's *Workflow* view (WORKFLOW1c), its subject the line at its tip. The repository's
  *Details*: the *Heavy run* row names the verification run while it holds the slot (D185 §3.5). *What needs you*: a failed
  verification, the person's row (§4.5). A terminal: `daoris-driver workflow run --line <repository>` beside WORKFLOW1c2's
  `--session|--quest|--ask`.
- **Words.** *verified*, *failed: `<gate>`*, *superseded by `<tip>`*, *not known: `<why>`*, *waiting for `<what>`*, each a status word
  in both catalogues (D116).

### 4.5 A failure becomes work for the development pipeline

- **`failed: wait`** (the default). *What needs you* shows the failed gate, the tip and the batch, with *Ask for a fix…*, *Run again*
  (a new run at the same tip, for a flake) and *Not now*.
- **`failed: ask`.** The person's standing say-so, drawn in *Your part* as *a failed verification asks for a fix without asking you*;
  Daoris then publishes the fix itself, as a workspace plan's tick publishes the ask its press stood for (`SetupPress.cs:165-178`).
- **The fix ask** is published to the repository (`to`), so a quest is published as a setup ask's is (`SetupWorld.cs:73-74`): Daoris's
  fixed words naming the gate, the tip, the batch's commits and the sessions whose landings carried them, marked as Daoris's, never
  the person's; the failing gate's log, its last 256 KiB, as the ask's file (`ServiceClient.cs:698-704`); and the kind the check's
  `ask` names as its choice (`AskWorkflowComposed`, `AskWorkflowCommand.cs:6`), so the start node routes it into the development
  pipeline the person chose for that kind (§1.1).
- **One per gate.** While a fix ask Daoris published for a gate is open, a later failure of that gate is added to the ask's channel at
  ordinary level (D188) rather than published again.
- **Nothing undone by itself.** No bisect, revert or rollback (D157 §3.7). A person may ask a commit of the batch (§4.3).
- **`failed: stop`** ends the run failed, and asks nothing.

### 4.6 A stage or a publish requires the verdict

- **In the graph.** A stage (a plugin's `work/stage` node, FABRIC-PERFORM1l) wired after the check binds the same tip, so it starts
  only on the check's reach at that commit; production stays the person's press (D157 point 9).
- **Outside it.** The install's stage, `publish:desktop --stage --from-gated <commit>` (`desktop-publish.mjs:1012-1015`), keeps
  requiring the full set's verdict of that commit from the merge tool's store, which this repository's runner writes when the
  install's verification runs it (§5). The verdict reader of §4.4 answers any other publisher the same question.

### 4.7 Later: the gates a batch reaches

CI1's reach generalised: a gate's optional `paths` in the declaration, and the check's `reach: paths` running only the gates the
batch's changed paths reach, the whole set before a stage. Until its row, every gate the check names runs.

### 4.8 Beside FABRIC-VERIFY1

The lanes design holds what this does: a verdict names its exact candidate, unknown is never success, a failure routes one fix and
holds what has not begun (its §1, §4, §5). A lane beside a quest step is a `check` on the run's branch not wired into `handed`; the
verification pipeline is the line's lane, at a batch. Neither design waits for the other.

## 5. This repository under the design

### 5.1 Its gates, split

| Gate | Under the design | Why |
|---|---|---|
| `universal`, `code-map`, `orient-index` | light: a session's, and the merge's blocking set | seconds to a minute; what must never reach history (`merge-branch.mjs:786`, D180 point 1) |
| `cli`, `tool-tests`, `service`, `devkit`, `driver`, `modules`, `driver-process`, `driver-process-git`, `modules-process`, `web`, `web-unit`, `web-measured`, `deployment` | `"heavy": true` | minutes to an hour each; the CI beside's (D180 point 2) |
| `verification` (top-level) | `node tools/merge-branch.mjs --full --beside` | the merge tool already runs the full set in its lanes, in its gate tree, writes the verdict the stage reads, and keeps its logs |

- **What a session runs here.** The three light gates; its own test classes and files, filtered; and the censuses its change reaches
  (`docs/development.md:92-104`), as the branch-worker's brief already asks of a builder.
- **HEAVYRUN1c's reason** (marking would have had each session run about thirty minutes of suites at its end) no longer holds under
  `light`, so the marks land with this repository's pipeline, never before it.
- **The runner and the tip.** `--full --beside` with no commit gates main's tip and supersedes itself. The merge tool reads
  `DAORIS_VERIFY_COMMIT` as the commit where none is named, so it gates the batch's tip, never supersedes itself, and Daoris does.
  A second run beside is still refused by the tool's lock, which the check reads as `unknown`, and tries again at its next look.
- **`docs/development.md`'s *Choose the checks*** (`:51-53`, *the complete reached set before recording completion*) is a builder's
  rule under the merge tool; a driven session's is its pipeline's, and the guide says so.

### 5.2 Its two pipelines, on the owner's install

- **`daoris-development`**, the repository's default route: `claimed` with `checks: light` and `callMinutes: 20`, then the gates and
  the landing as the install holds them now (a branch `quest/{slug}-{quest}`, never pushed and never merged by the install;
  DOGFOOD1).
- **`daoris-verification`**, the `landed` route on `main`: one `check` with `gates: heavy` (so the `verification` runner),
  `failed: wait` until the owner chooses `ask`, `ask: fix`, `settle: 60`.
- **The kind `fix`**, declared by the workspace (*Fix a failed verification*), mapped to `daoris-development`.

### 5.3 What the install then does differently with its six sessions

- Each session's start says its checks from its graph; `run-heavy` from its tree, and the declared heavy commands, are refused; a
  call past twenty minutes is said to it. The standing answer loses its test rule.
- A session that reaches done ends within a minute, with no suite in its background, so its place in the cap goes to the next quest
  rather than to a timeout.
- The parent stops running the full set beside by hand. Its merges move `main`; the install's verification sees the line move,
  binds one run at the newest tip (the whole batch), runs it below normal beside the six sessions, and supersedes it while a newer
  merge lands within the hour.
- A failure is on *What needs you*; *Ask for a fix…* publishes a `fix` ask with the gate's log, which a driven session takes.
- The stage is unchanged: `--from-gated` still requires the full verdict of the commit it publishes.
- Owed, measured on the install: a day's sessions ended by the timeout before and after, the verification's wall beside six
  sessions, and how often a run was superseded.

## 6. Rejected

- **A route chosen by a model's reading, or a kind inferred from the changed paths** (D157 point 10, D24, D54).
- **The start node as a node of a version's graph, a pipeline as a sub-graph or as a path through one graph** (§2's table).
- **Routing landed work by its paths into several verification pipelines**: a kind inferred from paths in another form; a batch's
  reach by paths is the check's (§4.7).
- **A versioned route set**: one more versioned object and choice level for what a run's binding already records.
- **Verification as nodes after `handed` in every development run** (D190 lets a check follow a landing): the heavy gates once per
  landing, no batching, and a run held open on them. A cheap per-landing check (a service's status) stays a plugin's node there.
- **The instruction as a standing text**, which is today's workaround: an agent drops a sentence (D161).
- **Guessing a heavy run from a command's text** (HEAVYRUN1's rejection stands): the harness refuses declared commands exactly.
- **Daoris ending a session's long call**: a turn is the agent's (DONEEND1); the person's stop stays.
- **A bound of a session's own on its background work**: BGWAIT1's rejection stands; its reason is removed instead (§3.4).
- **Daoris's own CI engine**, lanes and order of its own (D157's rejection stands): a repository with lanes declares its runner.
- **Running a landed edit of the declaration unasked**: the person accepts the gates by digest (§4.2).
- **Superseding on every move with no bound**: a fast line would never get a verdict (§4.3).
- **A fix ask published with no say-so of the person's by default**: it starts work on the owner's accounts; `ask` is the person's
  standing choice, a lowering only the person makes (§1.3).
- **An automatic bisect, revert or rollback** (D157 §3.7).
- **A verification run counted against the cap, or run in the main checkout** (FAMILYFAIL1b; D180's rejection).
- **Marking this repository's suites heavy before its development pipeline is light** (HEAVYRUN1c's reason holds until then).

## 7. Build rows, in order

Each is a feature a person can see working (D187), end to end across its lanes.

| Row | What a person sees | After |
|---|---|---|
| FABRIC-PIPELINE1a | the start node: routes drawn on the *Workflow* tab and printed by `workflow show`; a run's view says which route chose it | — |
| FABRIC-PIPELINE1b | a development pipeline's light checks: the rule and the option, the start's words from the graph, `run-heavy` refused from a held tree, the deny rules and the classifier's sentence, Setup's row | — (a graph version's option read at the bind after CUTOVER1g) |
| FABRIC-PIPELINE1c | a long call said: Daoris's urgent word past `callMinutes`, on the conversation and the rail | 1b |
| FABRIC-PIPELINE1d | the `check` node, and a person's verification of a commit or branch: accepted gates, tree, slots, job, priority, verdict recorded and shown | CUTOVER1a, CUTOVER1c |
| FABRIC-PIPELINE1e | landed work verified by itself: the `landed` route, a run per batch, superseded within `settle`, restart and drain, the failure on *What needs you* | 1a, 1d |
| FABRIC-PIPELINE1f | a failure becomes work: *Ask for a fix…*, `failed: ask` as the person's say-so, one ask per gate, the log attached, the kind routed | 1e |
| FABRIC-PIPELINE1g | this repository on the install: its marks and runner, the merge tool reading the tip, the guide's words, the install's two pipelines set by the owner's press | 1b, 1e |
| FABRIC-PIPELINE1h | a stage after the check, and the verdict reader any publisher asks | 1e, PERFORM1l |
| FABRIC-PIPELINE1i | Ask Daoris proposes routes and pipelines, and the new lowering rows hold an agent's choice as a proposal | 1e |
| FABRIC-PIPELINE1j | the gates a batch reaches by its paths, the whole set before a stage (CI1's reach for any repository) | 1e |

- **1a** (driver, cli, web). Proof: both twins' rows on `workflow-selection.json` for the routes as drawn; the tab's test and stories
  in both languages; `workflow show`'s golden.
- **1b** (driver, modules, web, cli). `sessionChecks` on both twins with its keeps rows; `checks` on `claimed` and on schema 1's `work`
  read by both twins; the start's words; `run-heavy`'s refusal; `SpawnSettings`' deny rules and sentence. Proof: a driven start's
  instruction golden under `light`; `run-heavy` from a held tree exits 1 with its sentence, from an unheld checkout runs; the spawned
  settings file's deny list names each heavy gate exactly and a filtered command passes; a repository marking none says so on Setup.
- **1c** (driver, web). Proof: a stand-in session whose call outlasts the bound is told once at urgent level, and never ended.
- **1d** (driver, modules, web, cli). Proof, a `Process` class on a scratch repository with stand-in gates: two verifications never
  overlap with each other or with a `run-heavy`; a failing gate's verdict, exit and log on the run's record; a changed declaration
  held `unknown` until accepted; a run past its timeout ended `unknown`; the job's processes gone before the slot is let go.
- **1e** (driver, web). Proof: two landings batched into one run; a third superseding it within one look and its job ended; a run past
  `settle` running to its verdict; a driver killed mid-run leaving `unknown` and the next look binding the newest tip.
- **1f** (driver, service, web). Proof: *Ask for a fix…* publishes one ask with the log as its file and the kind as its choice; a
  second failure of that gate adds to it; `failed: ask` publishes with no press only where the person set it.
- **1g** (records, tools, driver). Proof: the merge tool's test of `DAORIS_VERIFY_COMMIT`; on the install, a merge batch verified by
  the install, its wall beside the sessions, and a day of DOGFOOD1 sessions with none ended by the timeout for a whole suite.
- **1h**, **1i**, **1j**: their proofs as their rows say once filed.

## 8. What this document's gate does not cover

Documents only; nothing is built. The statements about today were read from the code at `300626e52`, and none was run. No test shows
a deny rule written from a declared command refusing it on both doors, a verification run's job ended within one look of the line
moving, the merge tool reading a handed commit, or the cost of the line's ref read per look on a home with routes; 1b, 1e and 1g
prove them. The 20-minute call bound, the 120-minute timeout, the 60-minute settle and the 256 KiB log are judgements, each a
default the person may set, to be measured on the install (§5.3).

## 9. The reads

None yet. Owed under D191: Codex's, and Fable's on §2 to §4, for the reasons the standing gives. The steward checks each finding
against the code and records it here, with its verdict.
