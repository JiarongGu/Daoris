# A run bound to a graph version is governed by its graph: the cutover (proposed, 2026-10-20; revised after two makers' reads)

**Standing:** proposed under D190, revised under D191 after Fable's and Codex's independent reads, each finding checked against the
code by the steward (§13). Fable and Codex read this revision before the steward settles it; nothing here waits for the owner. The
design builds nothing; its rows (§11) do.

Read for it: the fabric events design §E7, §E2, §E3, §C4's five rules, §D2 and §D6; the workflow design §2.2, §4.5, §4.6, §5.1 and
§5.2; D157 points 2, 6, 7, 8 and 11; D155's gate as `OpinionGate` builds it; D173's FABRIC-GRAPH1b, FABRICRUN1, GRAPHTWINS1 and
CLOSE notes; D191. Every statement about today was read again from the code at `d0e51b54b`, unchanged at `33c057386`, and names its
file and line; none was run.
Where a choice leans on behaviour no test shows, the first build row proves it (§11).

## 0. What the decisions rest on

| Today | Read at |
|---|---|
| A run bound to a graph version is held before any step: its process says the version cannot be read here, the run's part of the landing gate is *cannot start*, and every landing door refuses it | `WorkflowProcesses.cs:60`, `WorkflowNamed.cs:117-121`, `WorkflowGate.cs:437-441`, `OpinionGate.cs:667-672` |
| The runtime skips a quest whose run is bound to a named version of either schema, unless the quest was bound at its take to a closure graph: the skip asks `!closes`, so that graph governs it whatever its run | `NodeRuntime.cs:255-272` |
| A branch unit is governed by its landing rule's cycle whatever run produced it; the named-version read answers nothing for a branch | `NodeRuntime.cs:251`, `:321-328` |
| A run binds at its first start once the start has opened, in the look; the quest's take, inside the start, names its closure graph from Current's work cycle before that | `Driver.cs:766-774`, `:1898-1906`, `WorkflowRunStart.cs:43-80`, `QuestCloser.cs:864-869` |
| The runtime arms on the choices and, where it closes quests, on a closure graph its home keeps | `NodeRuntime.cs:214-226`, `:335-357` |
| What the runtime governs is one unit under one graph: each node's status starts on that unit, and the performer keeps each status on its own unit's record, one file per unit | `NodeRuntime.cs:87-89`, `:573-595`, `WorkPerformer.cs:203-241`, `NodeRecordFiles.cs:9-14` |
| The evaluation takes a status's unit as the node's subject: an observation looks the unit up by `status.Unit`, and a start binds that unit and its revision | `WireEvaluation.cs:320-341`, `:523-525`, `:631` |
| Every observer refuses a binding naming another unit; `claimed` refuses a unit that is no quest, the other observed built-ins one that is no branch | `BuiltInNodes.cs:105-111`, `OpinionNode.cs:43-46`, `LookNode.cs:35-38`, `GoAheadNode.cs:25-28` |
| An acceptance binds its unit and that unit's revision, and lapses on a new revision, graph or node version | `NodeAcceptances.cs:12-18`, `:117-121` |
| A plugin's event naming no invocation binds to the one unit told under its repository and branch; two told under one leave the event unwatched | `WorkObserver.cs:96-105` |
| A go-ahead node takes the newest answer to its act, word for word, from what is handed in; a go-ahead names no repository, and each request on it names a session and a quest; schema 1 counts only those its work's sessions or quests asked | `GoAheadNode.cs:30-44`, `GoAheads.cs:7`, `:20`, `WorkflowRun.cs:457-466` |
| A `go-ahead` is a pressed node, which the runtime leaves ready; it begins only built-ins of observed reach, and hands no go-aheads to the evaluation | `NodeContract.cs:429`, `NodeRuntime.cs:526-537`, `:560`, `:580-593`, `:1063` |
| The runtime reads D157's gates at a unit's newest session's tree's `HEAD`; a landing door may judge them at a commit it pins; the opinion and the look nodes move only on a read at their bound commit | `DriverWatch.cs:779-789`, `TreeGates.cs:34-54`, `OpinionNode.cs:73-76`, `LookNode.cs:68-71` |
| A node starts only where it is ready, eligible, bound and holds no unreconciled attempt | `WireEvaluation.cs:403-406` |
| Schema 1 holds the look's set-up while an agent is at the opinion; the person's *Reviewed* answers a dispute and commits since; the `opinion` node reaches on *settled* and the person's three answers alone | `WorkflowRun.cs:326-332`, `OpinionGate.cs:250`, `:356-365`, `OpinionNode.cs:78-85` |
| Schema 1 shows a go-ahead step it cannot start, and nothing holds the landing on it | `WorkflowRun.cs:357-365` |
| A look the work's choice adds is a step of the run that the version's graph has no node for | `WorkflowRun.cs:572-592`, `WorkflowRunNodes.cs:22-26`, `:152` |
| A saved graph wires its opinion straight into each look and each landing, and each look into each landing, in no `anyOf`; the CLI judges the same | `NodeGraphs.cs:239-285`, `nodegraphs.ts:528-546` |
| The decoder wires each step after the one before and each gate into every later gate, so a decoded look waits on its opinion | `WorkflowGraphs.cs:114`, `:116-123` |
| A decoded version is judged as no saved graph; a saved file refuses `synthetic`; a version holding a kind this build does not run reads as no steps | `WorkflowGraphs.cs:62-64`, `NodeGraphs.cs:588-589`, `WorkflowNamed.cs:250-256` |
| A graph holding `quest.close` refuses any `claimed` before it | `NodeGraphs.cs:314-318` |
| A run on a live graph is named by its unit and its graph's digest; a skip's scope, a receipt's id, an acceptance's graph and a trigger's firing key each name that digest or that run | `NodeRuntime.cs:125-129`, `WireEvaluation.cs:470-478`, `:502`, `:517`, `:631`, `:643` |
| A quest bound at its take has its graph kept by digest before the take names it, and the runtime keeps every digest an unfinished quest names | `QuestCloser.cs:905-906`, `NodeRuntime.cs:259-263`, `:793-800`, `:815-816` |
| A closing quest's revision walks its ancestors through every repository and digests its chain's quests in its own repository and the branches they worked | `QuestCloser.cs:149-161`, `:170-198`, `:283-287` |
| A chain's next step opens a tree of its own, grown from the branch of the step before | `Driver.cs:1625-1642` |
| The replay comparison reads a run's steps onto its graph on one unit and observes nothing | `WorkflowRunNodes.cs:92-96`, `:148` |

## 1. Two engines, never on one run

**Decided.** A run bound to a schema-1 version stays D157's to its end: its gates read its steps and `WorkflowRuns.Derive` draws it,
as today. A run bound to a schema-2 version is the node runtime's. No run is governed by a decoded graph, so a steps version reaches
the runtime only as a new version saved as a graph: a person's save (§7.2), never a migration.

**Why.** One engine governs a run, by its binding (§E7). A version is whole and its run keeps it (D157 point 6), and what a version
means is what its engine does with it. Running a schema-1 run on its decoded graph would change, with no save of the person's, what
its go-ahead holds (§5.2); moving a run in flight would change it mid-run. Canary 38's comparison names those differences.

**One owner per record.** The first reading said the runtime governs no quest of a named run. A quest bound at its take to a
closure graph is governed by that graph whatever its run (`NodeRuntime.cs:255-272`), so a named run's quest could be D157's and a
closure graph's at once. Every combination, with the one writer of each record:

| The quest's run | Its quest unit's record | Its branch units' records | The run's record, `run:<run>` | What its landing doors read |
|---|---|---|---|---|
| Current, or none bound; no closure | Current's work cycle, where it acts | the landing rule's cycle, where it acts | none | the rules live |
| Current; bound at its take to a closure graph | that graph, kept by its digest | the landing rule's cycle | none | the rules live |
| a schema-1 version; no closure | none: D157's run writes no node record | the landing rule's cycle, as today | none | the version's steps |
| a schema-1 version; bound at its take before this build | that closure graph, which closes it | the landing rule's cycle | none | the version's steps |
| a schema-2 version; no closure, or bound at its take to the version's graph | none | none | the run's graph | the version's gate nodes, and the graph's part read from the run's record (§4) |
| a schema-2 version; bound at its take before this build to another graph | that closure graph, which closes it | none | the run's graph | as above |
| a version that no longer reads | as its row above | as its row above | kept, nothing moved (§3) | *cannot start* |

- **After this build** the take names no closure graph for a schema-1 run's quest, and only its version's graph for a schema-2
  run's (§8), so the fourth and sixth rows hold only quests bound before it, each closing as it started.
- **A chain across repositories** is a run per repository (`WorkflowRunBindings.cs:52-53`), each its own row.
- **What the decoder still does:** canary 38's read-only comparison (`WorkflowRunNodes`), a steps version drawn as a graph, and the
  graph a steps version is saved as (§7.2).
- **Withdrawn:** FABRIC-RUN1c's left part, a bound run's decoded graph governing its unit. §E7's *no engine runs beside the old
  one* holds run by run.

## 2. The run's record and its subjects

### 2.1 A record of the run's own, its nodes each observing a subject

**Decided.** A run bound to a schema-2 version is the runtime's unit `run:<run>`. Its canonical record is its binding,
`<home>/workflows/runs/<run>.json`, written by the driver (D157 §5.1); its node record holds every node of its graph, and only the
runtime writes it. Each node observes a subject the pass names for it (§2.2), handed to the evaluation beside the statuses; a
node's binding names its subject beside its record's unit, and lapses when the subject moves.

**Why not the other way Fable named: the run as a scope over its quest and branch units**, each node kept on its subject's record,
with wires across them. It leaves the observers as they are, and the evaluation and the performer already take statuses of
several units (`WireEvaluation.cs:320-341`, `WorkPerformer.cs:208`). It fails on the records:

- A subject's record has another writer first. Current's work cycle governs a quest from its first read, before its run binds
  (`NodeRuntime.cs:247-274`), and the landing rule's cycle every branch (`:251`). A run placing its nodes there would read and
  write that graph's statuses and attempts under the same node ids (`handed`, a plugin's step), and no rule tells them apart.
- A run's subjects move. Each step of a chain opens a tree of its own (`Driver.cs:1625-1642`), and the chain grows by quests.
  Every move would leave the old subject's record holding the run's statuses and attempts, governed again only to reconcile them.

A record of the run's own has one writer from its first pass, and a moved subject is a lapse on that one record.

**What it costs**, all in the pure core first (CUTOVER1a): each observer compares the binding's subject where it compared its
unit, four guards; `WireRun` carries each node's subject and each subject's revision; `NodeAcceptance` gains `Subject`, in its id
only when set, so every acceptance written before keeps its id and reads as it was; a binding lapses as `subject` when its node's
subject moves, a new code beside `revision`. The runtime's paths that read the governed unit read the run's subjects (§11.2).

### 2.2 Subjects and the revision each binds

| Kind | Its subject | The revision it binds |
|---|---|---|
| `claimed` | the run's newest quest here: its chain's last read in the run's repository | that quest's, the moment it last moved (`QuestUnits.cs:33`) |
| `quest.close` | the same quest, at which the chain's close is judged (`QuestCloser.cs:125-128`) | its closing revision (`QuestCloser.cs:170-198`), as a closing quest's today (`NodeRuntime.cs:265-267`) |
| `opinion`, `look`, `handed` | the run's branch: the branch unit whose executors hold the run's newest session here with a tree here | the branch's tip read in the pass, at which the pass reads the gates too (`SessionTrees.cs:768-771`) |
| a plugin's observed node | the run's branch | its tip read in the pass, as a branch unit's observed node binds today (`NodeRuntime.cs:966-976`) |
| `pushed`, `pull-request`, `line`, a plugin's performed node | the run's branch | the branch unit's kept revision, as a governed branch unit's nodes bind today (`NodeRuntime.cs:582`) |
| `go-ahead`, `human`, `wait.unit`, `stop`, `all` | the run itself, projected as a unit for the evaluation alone, which the inventory never lists | none: a null revision, which the evaluation allows off a branch (`WireEvaluation.cs:452-454`) |

- **A moved subject lapses.** A binding names its subject, so a new branch at the same commit, or the chain's next quest, lapses
  what was bound to the old one, and what rests on it lapses `upstream` (`WireEvaluation.cs:518-520`). Equal commits on another
  branch never carry a reach over (Codex's finding 5).
- **No subject this pass, no start.** A node whose subject the pass did not find (a teammate's tree, a tree tidied, as
  `DriverWatch.cs:783-786` reads nothing for one) is not started, and an active one is not observed, as an observed node whose tip
  was not read is not begun (`NodeRuntime.cs:528-530`). Its view says what it waits for.
- **A branch gone after its landing keeps its bindings,** as a tip not read does (`NodeRuntime.cs:971-972`): a removed branch is
  no new work.
- **The gates read at the pass's tip.** The pass reads a run's gates at the tip its branch-side nodes bind, where today it reads a
  unit's at its tree's `HEAD` (`DriverWatch.cs:788`), so the opinion and the look are judged at their bound commit by construction.

### 2.3 What keeps Current's graphs off a run's subjects

- **Its quests.** `Govern` already skips a quest whose run is bound to a named version of either schema (`NodeRuntime.cs:270-272`),
  and from this build its take names no Current closure graph (§8).
- **Its branches.** `Govern` reads a branch unit's run through the quest its `produced-by` link names (`WorkUnit.cs:131`, read so
  by `QuestCloser.cs:283-287`) and leaves it ungoverned where that run is bound to a schema-2 version: every branch the run's quests
  produced, its former subjects included. A schema-1 run's branch keeps its landing rule's cycle (§1's third row), as schema 1 is
  unchanged here.
- **A record written before the run bound** (a work cycle that began on an open quest) is left as it stands, its attempts held for
  the person as a removed graph's are, and said once.

### 2.4 Each told event, go-ahead and landing reaches the run it is for

1. **Told under the run, at its own branch.** A run's plugin nodes are told under `run:<run>` with its repository's identity, the
   branch subject's branch and tip, as a branch unit's are named today (`NodeRuntime.cs:301-302`, `:1023`). A session's branch
   serves one quest, and a quest is one run's in its repository, so no two runs are told one branch. A landed branch carrying
   several runs' work is never told: each run is told its own session's branch, whose landing records name the landed one
   (`BuiltInNodes.cs:168-170`). Where a pass finds one branch named for two (a name reused), it tells neither and says so; the
   observer's `not-watched` (`WorkObserver.cs:102-105`) stays the backstop.
2. **A go-ahead scoped to its run.** The pass hands a run's `go-ahead` only the go-aheads on the run's ask that a session serving one
   of the run's quests, or a request naming one, asked, as schema 1 counts them (`WorkflowRun.cs:457-466`); the act is still matched
   word for word (`GoAheadNode.cs:32`). Another repository's yes is another run's.
3. **A revision per subject** (§2.2). A commit moves the branch-side bindings and what rests on them, never `claimed`; the closing
   revision is `quest.close`'s alone. The first reading bound every node of the run to one digest of its chain and branch, so a
   commit lapsed `claimed` too.
4. **`handed` across a chain's landings.** The run's `handed` is its newest session's work handed on: reached once that session's
   branch's own landing holds the tip it is bound to (`BuiltInNodes.cs:120-129`). An earlier session's landing carries part of the
   run's work and is that session's. When the chain's next step here opens a session, the subject moves: `handed` and what rests on
   it lapse and are judged again on the new work. A landing, a push or a deploy already made is never reversed, and the nodes after
   `handed` run again for the new work, as the version asks of each landing. Under an opinion rule the gate waits for the chain's last
   step here (`OpinionGate.cs:382`), so such a run lands once.

## 3. Arming, governance and recovery

- **Arming, corrected.** Today the runtime arms on the choices (`NodeRuntime.cs:222-226`) and, where it closes quests, on a closure
  graph its home keeps (`:337`, `:357`); the first reading said the choices alone. Added: a run bound to a schema-2 version whose
  record does not hold its end, though its graph names no plugin node. `Armed`, `WithdrawAsync` (`:335-350`) and `Holding` with
  `Withdraw` (`:365-378`) each take it. Arming lists `<home>/workflows/runs/` once a look and reads a binding only when its file is
  new or changed since this process read it; a home with no runs folder costs one stat, so an install naming nothing is still judged
  with no file read. A run ends when its graph's end node is settled, or no node of it can move again with no attempt unsettled.
- **Governed whole.** Each bound schema-2 run is governed whatever its graph holds. Every built-in the runtime begins today it begins
  on the run (`NodeRuntime.cs:526-537`, `:1063`). **A change, named:** a run's `go-ahead` is begun too, though a go-ahead is pressed
  (`NodeContract.cs:429`) and the runtime leaves a pressed node ready (`NodeRuntime.cs:560`), since its answer is D135's record,
  which the node observes (§2.4). A person's task (`human`) stays HUMAN1's to start and a `wait.unit` WAITUNIT1's. Raising a
  go-ahead nobody asked stays the session's ask (D135) or FABRIC-HUMAN1's door; the person's skip passes one nobody raises (§5.2).
  Current's graphs keep FABRICRUN1's go-ahead as it stands.
- **One writer.** The runtime writes the run's record. A door keeps the person's word on it, as a held attempt's word is kept
  (`WorkPerformer.cs:421-447`), and the next pass applies it (`:462`). A record that does not read holds the run, said, as a unit's
  does (`NodeRuntime.cs:494-497`).
- **Recovery is the runtime's own.** Attempts, reconciliation, retries and the awaited set work as for a unit. An attempt whose
  node's subject moved is reconciled as any other, and its node starts again only once it is (`WireEvaluation.cs:406`). A performed
  call and its reconcile name a unit on the wire (`WorkWire.cs:75-80`): a run's name its subject, as a landing cycle's name its
  branch unit, so no plugin ever sees `run:`. A run whose version stops reading is held whole, its attempts kept until it reads
  again (D157 §5.2).
- **No plugin acts before the doors read the run.** Until CUTOVER1d makes a schema-2 run's process readable at its doors, the
  runtime governs and observes a run's nodes and starts none of a plugin's performed nodes, so nothing outside acts for a run whose
  landing every door still refuses.
- **A schema-1 run is unchanged.** The runtime still skips it, and its gates read its steps.

## 4. `WorkflowProcesses` stays the binding and gate interface; the door reads the run's record

- **The binding is unchanged** in its shape: `WorkflowRunBindings.Plan` binds a graph version by its digest already
  (`WorkflowRunBindings.cs:104-108`). Its moment moves (§8).
- **A graph version is read as a process.** `WorkflowProcesses.Read` reads the version's gate nodes back as the steps
  `WorkflowProcess.Of` reads (`WorkflowGate.cs:117`): the `handed` node's `form`, `accept`, `pattern` and `plugin`; the `look`'s
  `environment`; the `opinion`'s `reviewers`, `required`, `steps` and `recheck`. Each still chooses among what the repository
  declares, never beyond it, and the process carries the graph beside them.
- **A named version's graph has one landing.** It holds one `handed`, and at most one `opinion` and one `look`, since the process has
  one of each; judged wherever a version is read, in both twins: refused at the save, and a stored version that breaks it kept as
  written and read as unreadable with its reason. A run whose graph holds `quest.close` cannot start, saying why, until CUTOVER1g.
- **The graph's part of the landing gate is read, never evaluated.** The door reads the run's record. It lands only where the record
  holds `handed` active, in this run's scope, its binding naming the branch the door lands and, as its revision, the commit the door
  would land: the tip the gate is pinned at (`OpinionGate.cs:654-658`), else the tree's `HEAD` the door reads. The runtime begins a
  node only ready, eligible, bound and with no unreconciled attempt (`WireEvaluation.cs:403-406`), so an active `handed` is the last
  pass's judgement that the landing may start at that commit, on the subjects, revisions and rules it read. A door that evaluated the
  record itself would need the pass's rounds without their keeping (`NodeRuntime.cs:492-564`), a second engine in all but name.
- **Where the record says otherwise, the part holds,** after the look's and before the landing step's, refused as
  `AutoLandingCode.Refused` with the run's part's sentence (`OpinionGate.cs:667-672`):

  | The record | What the door says |
  |---|---|
  | does not read | its node record does not read, so its graph is judged by nobody until a person mends it |
  | `handed` waits on a node before it | `<run>`'s graph waits on `<node>` (its kind), in the words the run's view gives its state, before `<handed>` |
  | `handed` ready, or active at another commit or branch | Daoris has not judged `<run>`'s graph at `<commit>` yet: its next look does, and this asks for it now |
  | `handed` stopped or passed over by the person | you stopped, or passed over, `<handed>` with your words, so this run does not land |
  | the door's tree is not the run's branch | this work is carried by `<run>`'s newest session, `<session>`, whose branch its graph judges, and lands with it |

  A door that holds on a judgement not made yet asks the loop for a pass (a `nodes` wake, D173's FABRICRUN1 note, RUN1d). The
  look's automatic landing tries again at its next look; the person's press is refused, and pressed again once the view shows the
  landing ready. An earlier session's tree whose work a later one carries is closed `already` once that landing holds it (D173's
  CLOSELAND1 note, CLOSE1h19).
- **What it costs.** A *Reviewed*, or an answer the *Accept…* press gives to a dispute (`OpinionGate.cs:682`), lands at the press after
  the next pass, not at the same press. The first reading rejected this for that reason; it is taken now, since the alternative is
  the second engine above.
- **The gates' own engines wait for their node.** The second opinion is asked, and the look's set-up made, only once the run's record
  has begun its node; the look's set-up still waits while an agent reads the opinion (§5.1). A node the person placed before them (a
  check, a person's task) comes first.
- **The holds stay the door's.** The quest's holds and the work's content stay the gate's first parts (D157 §2.2), never a node an
  authored graph must hold.
- **The words go** that say a graph version does not run: `WorkflowNamed.cs:117-121`, the CLI's `namedworkflows.ts:130`, `:138` and
  `workflowdoor.ts:365`, `:691`, and the tests that hold them (`graphversions.test.ts`, `workflowchoice-door.test.ts`,
  `workflowdoor.test.ts`, `WorkflowGraphVersionsTests`). The first reading also named `HelpWorkflowProposals.cs`, whose words
  (`:283`) speak of a step kind this build does not run, and stay. No e2e spec or rehearsal holds them at `d0e51b54b`.

## 5. The person's two choices (WORKFLOW2b)

### 5.1 The look's timing is the review gate's

**Decided.** In a graph the look is not wired from the opinion. It begins with what comes before the opinion, and its set-up waits
while an agent reads the opinion, as schema 1 holds it (`WorkflowRun.cs:326-332`). The `opinion` node is reached only when its gate
is settled, by the reviewer or by the person's three answers, never on a dispute: `OpinionNode`'s table stands as it is
(`OpinionNode.cs:78-85`). The look's *Reviewed* answers a dispute and commits since at the gate (`OpinionGate.cs:356-365`); the
opinion node then reads *answered*, reaches, and the landing goes on.

- **The graph rules.** `protected-order` and `protected-wire` stop asking the opinion to reach, or be wired into, a look; each still
  asks it of every landing, and the look of every landing (`NodeGraphs.cs:246-255`, `:276-284`; the CLI's `nodegraphs.ts:528-546`).
  A person may still wire the opinion into the look, and hold their look behind it.
- **Why.** The order exists so that the person looks once, at work an agent already answered (the workflow design §2.2). What a
  dispute owes is the person's adjudication, which D155 made the look's own press. A required wire from the opinion would hold the
  look behind the very dispute the look answers; an opinion reached on a dispute, or a wire a dispute satisfies, would release every
  other consumer of the opinion before the person adjudicated (Codex's finding 8). The review gate already holds the set-up while an
  agent reads, so the order holds with no wire.
- **What moves.** The two rules in both twins, with rows on `graph-problems.json`. The decoder wires a look after the step before
  the opinion, never after the opinion (`WorkflowGraphs.cs:114-123`), its version 3 (`:33-36`), in both twins on
  `workflow-graphs.json`; no stored record binds a decoded digest (§1). The comparison reads a look the graph offers beside an
  opinion an agent still reads, which schema 1 draws not reached, as the review gate's hold, no difference; every
  `look-beside-opinion` row then agrees, and the name is kept for what a history holds of it.

### 5.2 A go-ahead holds the landing until the person's yes or skip

**Decided.** In a graph, a go-ahead before the landing holds it until the person's yes, or until the person passes it over with their
words (§E2's skip). Only a reach or the person's skip satisfies an `after` wire (§E2), and an unstarted node is neither. Schema 1
goes on past one because a partial kind cannot start there, not because it chose to land without it.

- **The doors.** The runtime begins the go-ahead and observes the run's own answers (§2.4, §3). The person's skip and stop of a run's
  node is a door of its own (CUTOVER1f), built before the doors hold on the graph (CUTOVER1d), so a go-ahead nobody raises never
  strands a run.
- **What moves.** Nothing in schema 1: its two go-ahead rows keep `cannot-start-holds-nothing`, schema 1's alone. A graph run proves
  the hold.

## 6. A look the work's choice adds (WORKFLOW2c): a hold kept outside the graph, for now

**Decided.** A look that the chain's, the ask's or a set-up step's choice adds (the workflow design §4.6) stays D154's hold at every
landing door, as in schema 1. The run's graph gains no node, and its binding and digest stay the version's. The run draws it as a
look added for this work, beside the graph.

**Why.** A version is whole and never edited (D157 point 6), and the person's presses act on the run, never on the workflow (§4.6).
A node added under a derived digest moves the graph governing the run when the choice is made, and a choice made mid-run lapses
every binding the run holds until §9's rule holds. D154's gate already holds every landing door, and its set-up already waits while
an agent reads the opinion. Both readers accept this as a compromise: a node the version placed between the opinion and the landing
does not wait for the added look, which a derived graph would say better. **Revisited** as a node under a derived digest once RUN1f
holds, when a derived digest no longer lapses what the choice does not touch.

## 7. The rules a run's graph is held to

### 7.1 The opinion on every way: no new rule (GRAPH1e dropped)

`protected-wire` already asks every saved graph for a wire straight from its opinion, in no `anyOf`, into every landing and, before
§5.1, every look (`NodeGraphs.cs:269-285`); the CLI judges the same (`nodegraphs.ts:536-546`). No saved graph lets a landing start with
its opinion unsettled. The first reading's GRAPH1e, a new rule for it, is dropped. §5.1 relaxes the look's wire alone, for the
reason it gives.

### 7.2 A steps version saved as a graph (GRAPH1j)

**Decided.** No run binds a decoded graph (§1), so the rules a saved graph is held to are asked where a decoded graph becomes a saved
one. `daoris driver workflow apply <id> --as-graph`:

- reads the steps version by its shape (`WorkflowNamed.ReadShape`), since one holding a kind schema 1 does not run reads as no
  steps (`WorkflowNamed.cs:250-256`), and decodes it (`WorkflowGraphs.Graph`);
- materializes every synthetic node as an authored one, its id kept, since a saved file refuses `synthetic` (`NodeGraphs.cs:588-589`),
  so the authored bound now counts them;
- judges the graph as saved, `observed-twice` among the rest, refusing it with its first problem, two observed nodes of one plugin
  step named both; and saves it as the next version, saying where the graph holds what the steps did not (§5.2).

The steps version stays as it was read. The CLI's `decodeSteps` judges a decoded graph as the driver's does
(`workflowgraphs.ts:194-198` judges nothing today), which GRAPHTWINS1 left undone. This is how a version holding `check`, `stage` or
`all` reaches the runtime (§10).

## 8. The binding moment, a run graph's close, and a quest bound to an edited graph

**Decided.**

- **The binding moment moves.** A run is bound inside its first start, before the start names a closure graph for its take
  (`Driver.cs:1898-1906`), by `WorkflowRunStart`'s own rule (`WorkflowRunStart.cs:43-80`). The look's bind after the opened starts
  (`Driver.cs:766-774`) stays for a run whose bind there failed or that started before this build, and finds the rest bound
  (`WorkflowRunStart.cs:61`). A start that holds or fails after its bind keeps its run bound to what it chose. **This amends D157
  point 11:** a run is bound at its first start, before its take, and the binding stands though that start opens no session; today a
  start that opened nothing bound nothing (`Driver.cs:766-768`). Why: the take must name the graph its run is bound to, and naming it
  from a selection made apart from the binding would race a choice or a version saved between the two.
- **The take names its run's graph.** `ClosureBinding.AtTakeAsync` reads the run's binding where it read Current's work cycle
  (`QuestCloser.cs:868`): under Current, that work cycle, as today; under a schema-1 version, none, so its quest closes by its done as
  schema 1 reads it; under a schema-2 version, the version's graph by its own digest (`WorkflowGraphs.cs:46-49`) where it holds
  `quest.close`, else none. A version is never edited and its file keeps it while a run names it, so nothing is kept in `closures/`
  for it.
- **The close.** The run's `quest.close` (§2.2) closes its chain's quests here that are bound to its version's graph, through the
  closer as today (`NodeRuntime.cs:658-712`), its attempt on the run's record. A quest of the run bound before this build to another
  graph closes through that graph (§1's sixth row), and the run's close waits for it, as a chain's close waits for a quest bound
  elsewhere (`QuestCloser.cs:130-135`).
- **No `claimed` before the close.** The validator refuses any `claimed` before `quest.close` (`NodeGraphs.cs:314-318`), and stands:
  `claimed` keeps its one meaning, a quest's done, which a closure graph writes only at its end. A run's graph holding `quest.close`
  begins at its gates, and what holds them until the work is in is what holds a landing today: a door lands a session's tree at its
  end or at the person's press, and an opinion is asked of finished work. The first reading's `claimed` read at a submission is
  withdrawn.
- **A quest bound to a graph Setup has edited since (FABRIC-CLOSE1k): the bound graph keeps it.** It already does: the take keeps the
  graph by digest before naming it (`QuestCloser.cs:905-906`), the runtime governs the quest with that kept graph
  (`NodeRuntime.cs:259-263`) and keeps it while the choices hold it (`:793-800`), and pruning keeps every digest an unfinished quest
  names (`:815-816`). The first reading took one keeper for the whole retention. What is missing is the saying: CLOSE1k says, once a
  pass, where a governed quest closes through a kept graph the choices no longer hold, naming the nodes that graph runs which the
  choices' graph does not, and where a bound quest's graph is kept nowhere here, so its close waits.

## 9. A graph edit and the reaches it leaves (RUN1f)

**Decided.** On a live graph (Current's landing cycles and work cycles), a run is named by its unit alone, and each node's place
enters everything today named by the whole graph's digest. A node's place is its declaration, the wires into it and whether it is
an entry, digested as `plc-` beside the graph's `gph-` (`NodeGraphs.cs:426-430`). The sites, as the reads named them:

1. **The run's name** (`NodeRuntime.cs:125-129`, `:306`): its unit alone, so an edit no longer begins a new run.
2. **A skip's scope** (`WireEvaluation.cs:418`, `:502`; `NodeLifecycle.cs:95-101`, `:313`): its node's place and the run, so a skip
   holds across an edit elsewhere.
3. **Whose a failure or a rejection is** (`WireEvaluation.cs:470`, `:512-515`): judged on the node's place and the run.
4. **A receipt's id** (`WireEvaluation.cs:474-478`): `skp-`, `fld-` and `rej-` digest the place, so a reach downstream of a skip rests
   on a receipt an edit elsewhere leaves current (`:518-520`).
5. **A trigger's firing key** (`WireEvaluation.cs:643`): the target's place, the run, the wire and the receipt, so an edit elsewhere
   fires nothing again.

Beside them, **an acceptance's graph** (`WireEvaluation.cs:517`, `:631`; `NodeAcceptances.cs:117-121`) binds its node's place where it
bound the whole graph. Invalidation then follows dependency, as §E2 states it: a node whose place changed lapses, what rests on it
lapses `upstream`, and nothing else.

- **Old records read as before.** A status or an acceptance naming a `gph-` digest, and a run named by unit and graph, are judged
  on the whole graph's digest and on that run's name worked out from the graph now; a firing key is looked for under both rules.
  At the upgrade the graph has not moved, so nothing lapses and nothing fires again; at the first edit after, an old one lapses as
  it always would have. No field changes: a value gains a prefix.
- **Why not keep lapsing everything.** Today an edit re-sends every reached performed node on every governed unit and lets every
  skipped one start: an outside effect per edit that nobody asked for. A warning at Setup would still re-run them. Keeping
  acceptances while the run, the scope, the receipts and the keys still moved would invalidate their descendants or refire their
  effects (Codex's finding 10), so every one of them takes the place.
- **A named version is untouched:** it is never edited (D157 point 6), its run is named by its binding, and a binding is never bound
  again (`WorkflowRunFiles.cs:40-41`).

## 10. Kinds schema 1 does not run (WORKFLOW1j, WORKFLOW1k)

**Decided.** `check`, `stage` and `all` run as graph nodes or not at all. WORKFLOW1j's wait on a pull request and WORKFLOW1k's outside
checks are built as a plugin's observed and performed nodes (FABRIC-PERFORM1l), never as a steps engine for schema 1, whose kinds
stay as D157 point 8 says: a kind it does not run is never offered there. A steps version holding them reaches the runtime through
§7.2's save. The first reading left this for the owner; under D191 it is settled here.

## 11. Build rows

### 11.1 The rows and their order

The order is a, b, c, e, f, d, g: each row a whole increment, and nothing a person sees half-built. CUTOVER1c governs a run's record
but starts no plugin's performed node (§3), so nothing acts for a run whose landing the doors still refuse; CUTOVER1f's skip exists
before CUTOVER1d makes the doors hold on the graph, so a go-ahead nobody raised never strands a run. RUN1f, GRAPH1j and CLOSE1k stand
beside them.

| Row | What | After |
|---|---|---|
| FABRIC-CUTOVER1a | a run's graph evaluated over its subjects, pure: the proof first | — |
| FABRIC-CUTOVER1b | a named version's graph: one landing, the look beside the opinion, the decoder and the comparison | — |
| FABRIC-CUTOVER1c | the runtime governs a bound schema-2 run, starting no performed node | a |
| FABRIC-CUTOVER1e | a graph run's selection, involvement and drawing from its record | c |
| FABRIC-CUTOVER1f | the person's skip and stop of a run's node | c, e |
| FABRIC-CUTOVER1d | the doors read the run's record; a graph version read as a process; performed nodes started | b, c, f |
| FABRIC-CUTOVER1g | the binding moment and a run graph's close | d |
| FABRIC-GRAPH1j | a steps version saved as a graph, judged as saved | b |
| FABRIC-RUN1f | a reach kept across a live graph's edit | — |
| FABRIC-CLOSE1k | a quest closed by the graph it was bound to, said | — |

- **CUTOVER1a** (driver). §2.1 and §2.2's pure core: `WireRun` names each node's subject and hands each subject's revision; a binding
  names its subject and lapses as `subject` when it moves; `NodeAcceptance.Subject`, in its id only when set; the four observers
  compare the binding's subject; a node with no subject this pass is offered no start. Nothing governs a run yet. Proof, each seen
  failing first: `WireEvaluationTests` over one record of unit `run:r` whose `claimed` observes a quest subject's done and whose
  `opinion`, `look` and `handed` observe a branch subject's gates and landing at the tip handed in, `handed` reached through its
  wires; that branch replaced by another at the same commit lapses `handed` and what rests on it; a go-ahead answered only through the
  list handed in; `GateNodesTests` for each observer's subject; every evaluation over records written before unchanged, a guard.
- **CUTOVER1b** (driver, cli). §4's one landing and §5.1's rules, decoder and comparison, in both twins. Proof: rows on
  `workflow-graph-versions.json` (no landing, two landings, two looks, two opinions), on `graph-problems.json` (a graph with no wire
  from its opinion into its look holds; a landing its opinion is not wired into is still refused) and on `workflow-graphs.json` (the
  decoder's version 3), each read by both twins; `WorkflowRunNodesTests`' `look-beside-opinion` rows agree.
- **CUTOVER1c** (driver). §2 and §3: arming by a bound schema-2 run (`NodeRuntime.cs:214-226`, `:335-378`); the run governed on its
  record over its subjects, its gates read at its branch's tip, its go-ahead begun on its scoped answers, its plugin nodes told under
  it at its branch; Current kept off its quests and branches (§2.3); no plugin's performed node started until 1d; a schema-1 run still
  skipped (`:270-272`). Proof: `NodeRuntimeTests` (an unbound graph version arms nothing; a bound one with no plugin node arms and
  moves `claimed`, the opinion, the look and `handed` on `run:r`'s record, its subjects' records untouched; its branch not governed
  by its landing rule's cycle; the chain's next session moving its branch lapses `handed`; another run's go-ahead not taken; each
  binding read once across passes, and no file read with no runs folder; a performed node not started), and by class
  `NodeRuntimeLoopProcessTests` and the observer's tests (an event at the run's branch reaching the run; one branch told for two runs
  told for neither).
- **CUTOVER1e** (driver, cli, web-shell). `WorkflowProcesses.Otherwise` takes a graph newest (`WorkflowProcesses.cs:125`), and a graph
  version's person part is read from its gate and go-ahead nodes where `Involvement.Lowers` reads steps (`WorkflowGate.cs:240`); the
  run's Workflow view and `daoris driver workflow run` draw a schema-2 run from its record, its subjects named, with an added look
  beside it (§6). Proof: both twins' tests; on the window, a schema-2 run's Workflow view.
- **CUTOVER1f** (driver, modules, web-shell, cli). §5.2: the person's skip and stop of a run's node, with their words, at the run's
  view and the terminal, kept on the run's record by the door under its lock and the lease, a field of its own that a record from
  before reads as none, and applied by the next pass. Proof: a go-ahead nobody raised, skipped with words, lets `handed` begin; a
  stop ends the run, said; a skip with no words refused; Ask Daoris's door or exemption for the view's hook in `HelpCoverageTests`.
- **CUTOVER1d** (driver, cli). §4: a graph version read as a process from its gate nodes (`WorkflowProcesses.cs:60`); the landing
  gate's graph part read from the run's record, holding with §4's sentences and asking a pass; the opinion asked and the look's
  set-up made once their node is begun; a plugin's performed node started; `quest.close` cannot start until 1g; the words go. Proof:
  `WorkflowGateTests` rows; a landing test where a pass makes `handed` active at a commit and the door lands it there, refuses a
  commit made since, and refuses a press made after a *Reviewed* and before the next pass, landing at the press after it; a person's
  task before `handed` holds *Accept…* naming it; the opinion not asked while a check before it waits.
- **CUTOVER1g** (driver). §8: the run bound inside its first start before the take (`Driver.cs:1898-1906`), the look's bind kept
  (`:766-774`); `ClosureBinding.AtTakeAsync` reads the binding (`QuestCloser.cs:864-869`); the run's `quest.close` closes its chain's
  quests here from its record. Proof: a schema-2 run ending at `quest.close` closes its quest; a schema-1 run's quest taken unbound
  under a work cycle that holds `quest.close`; a start that holds after its bind keeps its run bound; a quest bound before the build
  closing through its own graph, the run's close waiting for it.
- **GRAPH1j** (cli, driver). §7.2. Proof, on shape-level input: a version holding two checks of one plugin refused naming both, its
  steps version read as before; a version with a group saved with its join materialized; rows on `workflow-graphs.json` read by both
  twins, `decodeSteps` judging as the driver's.
- **RUN1f** (driver). §9. Proof: a runtime test where an edit adding an unrelated node sends nothing again, fires no trigger again and
  starts no skipped node; an edit of one node's option lapses it and what rests on it alone; records written before the build lapse
  nothing and fire nothing at the upgrade, and lapse at the first edit after.
- **CLOSE1k** (driver). §8's saying. Proof: a quest bound to a graph under choices holding its edit, said once and closed by the graph
  it was bound to; a bound quest whose graph is kept nowhere here, said once.

### 11.2 Traps for the builders

- **The observers' unit guards** (`BuiltInNodes.cs:105-111`, `OpinionNode.cs:43-46`, `LookNode.cs:35-38`, `GoAheadNode.cs:25-28`)
  and the evaluation's lookup by `status.Unit` (`WireEvaluation.cs:325`, `:332-335`): a run's status is its record's
  (`NodeRuntime.cs:579`), never its subject. 1a.
- **`Revised`** (`WireEvaluation.cs:452-454`) lets only a non-branch unit go without a revision: a run's is null, and each branch
  subject's must be handed in. 1a.
- **`Govern` takes the landing rule's cycle for every branch** (`NodeRuntime.cs:251`) and `BoundIn` answers null for a branch
  (`:321-328`). 1c.
- **The told frame names a branch only for a branch unit** (`NodeRuntime.cs:301-302`, `:1023`); a run's names its branch subject's.
- **A branch unit's paths**: `Tipped` (`NodeRuntime.cs:923`), `RebindsAsync` (`:930-939`), `ObservedRevisions` and `Observing`
  (`:966-988`), `GitReadsAsync` and `Pushing` (`:619-650`), and `GatesAsync` on the governed unit (`:602-607`): a run's read its branch
  subject's. `NodeGates` reads at the tree's `HEAD` (`DriverWatch.cs:788`): hand it the pass's tip (`SessionTrees.cs:768-771`).
- **The go-ahead's begin is a change** (`NodeContract.cs:429`, `NodeRuntime.cs:560`, `:1063`), and `Evaluate` hands in no go-aheads
  (`:580-593`). 1c.
- **The closer reads the governed unit's quest** (`NodeRuntime.cs:658-726`, `:740-787`, `:810-829`, `:839-896`): a run's reads its
  closing quest, and `Unreached` passes over every quest of the run, not its own unit alone (`:745`). 1g.
- **A unit on the wire** (`WorkWire.cs:75-80`): a performed call and a reconcile name the subject, never `run:`.
- **`NodeRuns.Of`** names a live graph's run (`NodeRuntime.cs:125-129`); a schema-2 run is named by its binding.
- **`Involvement.Lowers` takes steps** (`WorkflowGate.cs:240`). 1e.
- **The replay comparison observes nothing** (`WorkflowRunNodes.cs:92-96`): it proves no subject, and is no proof of 1a.

## 12. Rejected

- **Running a schema-1 run on its decoded graph,** now or once canary 38 agrees: it changes what a version does with no save of the
  person's (§1).
- **The run as a scope over its quest and branch units, each node on its subject's record:** those records have other writers first,
  and the subjects move (§2.1).
- **Telling a run's plugins the landed branch after `handed`:** one landed branch carries several runs' work, so its events reach
  none (§2.4).
- **One revision for every node of a run:** a commit would lapse `claimed` (§2.4).
- **The door evaluating the run's graph read-only:** the pass's rounds without their keeping, a second engine (§4).
- **The opinion reached on a dispute, or a wire or receipt an opinion's reading satisfies:** it releases every consumer of the
  opinion before the person adjudicates (§5.1).
- **A wire from the opinion into the look, required:** it holds the look behind the dispute the look answers (§5.1).
- **The landing going on past a go-ahead not started:** the go-ahead would hold nothing, and a node not started would satisfy a wire
  (§5.2).
- **A node the work's choice adds under a derived digest, now:** it edits the graph governing the run mid-run until §9's rule holds;
  revisited then (§6).
- **A new rule holding the opinion on every way (GRAPH1e):** `protected-wire` already holds it (§7.1).
- **A `claimed` read at a submission before `quest.close`:** the closure graph's validator refuses a `claimed` before its close, and a
  kind keeps one meaning (§8).
- **Naming the take's closure graph from a selection apart from the binding:** a choice or a version saved between the two would
  race it (§8).
- **The person's close for a quest whose graph was edited (CLOSE1k):** the bound graph already keeps it; only the saying is missing
  (§8).
- **A whole-graph lapse kept, with a warning at Setup:** the warning does not stop the re-runs (§9).
- **A steps engine for `check` and `stage`:** a second engine beside the graph's nodes (§10).
- **Two landings, two looks or two opinions in a named version's graph:** the process has one of each (§4).

## 13. Two makers' reads

Fable and Codex read the first reading independently (D191). The steward checked each finding against the code before it was taken.

| # | Finding | Found by | Checked at | Verdict | Taken in |
|---|---|---|---|---|---|
| 1 | A closure-bound quest of a named run is governed by its closure graph beside D157's run; "skipped already" is wrong | both | `NodeRuntime.cs:255-272` | holds | §1's table, §8 |
| 2 | CLOSE1k's premise: the bound graph already survives a Setup edit | Codex | `QuestCloser.cs:905-906`, `NodeRuntime.cs:259-263`, `:793-800`, `:815-816` | holds | §8: the bound graph kept, its saying CLOSE1k's |
| 3 | "Offers `handed`" undefined: eligibility is not a start, the door's pinned tip is not the runtime's `HEAD`, and a door evaluating right needs the pass's rounds | both | `WireEvaluation.cs:377`, `:403-406`, `TreeGates.cs:34-41`, `DriverWatch.cs:779-789`, `OpinionNode.cs:73-76`, `LookNode.cs:68-71`, `NodeRuntime.cs:492-564` | holds | §4: the door reads the record, never evaluates |
| 4 | Two runs on one branch leave a plugin's event unwatched | both | `WorkObserver.cs:96-105` | holds for the first reading, which told the landed branch | §2.4.1 |
| 5 | A reach must not survive its branch replaced; what rests on `handed` lapses | both | `NodeAcceptances.cs:12-18`, `:117-121` | holds | §2.2 (`Subject`), §2.4.4 |
| 6 | A go-ahead is matched by act across the ask, so another repository's yes answers this run | both | `GoAheadNode.cs:30-34`, `GoAheads.cs:7`, `:20` | holds | §2.4.2 |
| 7 | A chain across repositories is a run per repository | Codex | `WorkflowRunBindings.cs:52-53`, `WorkflowRun.cs:394-398` | holds | §1 |
| 8 | The closing revision digests every chain quest and branch across repositories, so one repository's commit lapses another's run | Fable | `QuestCloser.cs:149-161`, `:170-198`, `:283-287` | not as stated: it walks other repositories but digests only this one's quests and their branches; one digest for every node of a run did lapse `claimed` on every commit | §2.2, §2.4.3 |
| 9 | Every observer refuses a unit not its own; a status's unit is its record's; "many units wide" is statuses of several units, not a subject per node; `Govern` and `BoundIn` take branches as their landing rule's | Fable | `BuiltInNodes.cs:105-111`, `OpinionNode.cs:43-46`, `LookNode.cs:35-38`, `GoAheadNode.cs:25-28`, `NodeRuntime.cs:251`, `:321-328`, `:579`, `WireEvaluation.cs:325` | holds | §2.1 (a subject per node), §2.3 |
| 10 | RUN1f: the run is named anew at each edit, so skips, receipts and firing keys still lapse or refire | both | `NodeRuntime.cs:125-129`, `WireEvaluation.cs:470-478`, `:502`, `:518-520`, `:643` | holds | §9, as the steward decided |
| 11 | A binding is never bound again to a newer named version | Codex | `WorkflowRunFiles.cs:40-41` | holds; nothing here rebinds | §9 |
| 12 | The take reads Current's work cycle and the run binds after the take, so "bound before its first take" moves D157 point 11 | Fable | `QuestCloser.cs:868`, `WorkflowRunBindings.cs:13-16`, `Driver.cs:766-774`, `:1898-1906` | holds | §8; D157 point 11 amended |
| 13 | A `claimed` read at a submission conflicts with the validator refusing any `claimed` before `quest.close` | both | `NodeGraphs.cs:314-318` | holds | §8: withdrawn |
| 14 | GRAPH1j must materialize synthetic nodes, and its proof needs shape-level input | Codex | `NodeGraphs.cs:588-589`, `WorkflowNamed.cs:250-256` | holds | §7.2 |
| 15 | The proof is first by title only: the replay observes nothing, and the rows enable execution before the doors, involvement and the escape door | both | `WorkflowRunNodes.cs:92-96` | holds | §11: 1a a `WireEvaluation` case over subjects; the order a, b, c, e, f, d, g; 1c starts no performed node |
| 16 | 1c must change `Armed`, `WithdrawAsync`, `Holding` and `Withdraw` | Fable | `NodeRuntime.cs:214-226`, `:335-350`, `:365-378` | holds | §3, 1c |
| 17 | Traps: the observers' guards, the told frame's empty branch, `Tipped` a branch's alone, the go-ahead's begin a change, `Involvement.Lowers` taking steps | Fable | `NodeRuntime.cs:301-302`, `:560`, `:923`, `:1023`, `:1063`, `NodeContract.cs:429`, `WorkflowGate.cs:240` | holds | §3 (the change named), §11.2 |
| 18 | "Arms on the choices alone" is wrong: kept closure graphs arm it too | Fable | `NodeRuntime.cs:337`, `:357` | holds | §0, §3 |
| 19 | GRAPH1e overstates the gap: `protected-wire` already asks the opinion wired into every look and landing, and the CLI agrees | both | `NodeGraphs.cs:269-285`, `nodegraphs.ts:536-546` | holds | §7.1: GRAPH1e dropped |
| 20 | WORKFLOW2b: the look beside a dispute is right; an opinion reached on a dispute releases every consumer before adjudication; the go-ahead's hold is the checkpoint | Codex | `OpinionGate.cs:250`, `OpinionNode.cs:78-85` | holds | §5: the look's timing the review gate's, the opinion reached only when settled, `protected-wire` relaxed for the look alone; §5.2 |
| 21 | WORKFLOW2c is a defensible compromise; a derived graph would say the dependency better | Codex; Fable accepts the compromise | the first reading's §6 | holds | §6, revisited once RUN1f holds |

**What the first reading left for the owner** is settled here under D191: the two engines and WORKFLOW1j and 1k (§1, §10); the look
beside the opinion (§5.1, changed from the first reading); the go-ahead's hold (§5.2); the added look (§6); RUN1f (§9); CLOSE1k (§8);
a schema-1 run's branch keeping its landing rule's cycle (§1's table).

## 14. What this document's gate does not cover

Documents only; nothing is built. The statements about today were read again from the code at `d0e51b54b`, and none was run. No test
or run shows a node observed on a subject apart from its record's unit, the door's reading of `handed` agreeing with the pass that
made it active, or arming's cost on a home with bound runs: CUTOVER1a, CUTOVER1d and CUTOVER1c are those proofs, CUTOVER1a first.
`verify` checks this document's place and links, and none of its words.
