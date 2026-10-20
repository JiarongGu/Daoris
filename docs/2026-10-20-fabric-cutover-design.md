# A run bound to a graph version is governed by its graph: the cutover (proposed, 2026-10-20)

**Standing:** proposed under D190 (FABRIC-CUTOVER1, with FABRIC-WORKFLOW2c, GRAPH1e, GRAPH1j, RUN1f and FABRIC-RUN1c's left
part). Pending the owner's review: no row builds before the owner's yes, and another maker's read comes before that review.
The design builds nothing; its rows (§10) do.

Read for it: the fabric events design §E7, §E2, §C4's five rules, §D2 and §D6; the workflow design §2.2, §4.5, §4.6, §5.1
and §5.2; D157 points 2, 6, 7 and 8; D155's gate as `OpinionGate` builds it; D173's FABRIC-GRAPH1b, FABRICRUN1 and
GRAPHTWINS1 notes; WORKFLOW2b's outcome in the archive. Every statement about today was read from the code at `d3d0d15ce`
and names its file and line; none was run.

## 0. What the decisions rest on

| Today | Read at |
|---|---|
| A run bound to a graph version is held before any step: its process says the version cannot be read here, the run's part of the landing gate is *cannot start*, and every landing door refuses it | `WorkflowProcesses.cs:60`, `WorkflowNamed.cs:120-121`, `WorkflowGate.cs:437-441`, `TreeGates.cs:39-53` |
| The runtime governs no quest bound to a named version, of either schema; a branch unit is governed by its landing rule's cycle whatever run produced it | `NodeRuntime.cs:270-272`, `:251` |
| The runtime arms on the choices alone, so a bound run arms nothing | `NodeRuntime.cs:214-226`, `:335-337` |
| What the runtime governs is one unit under one graph; a node record is one unit's, written by the driver that holds the lease | `NodeRuntime.cs:87-89`, `NodeRecordFiles.cs:9-14` |
| `claimed` observes a quest unit and the other observed built-ins a branch unit; a quest unit's revision is its last update's moment, not a commit, so an `opinion` on it moves only where its rule asks none | `BuiltInNodes.cs:110-111`, `QuestUnits.cs:33`, `OpinionNode.cs:68-76` |
| The evaluation is already many units wide: revisions, units, opinions, reviews and go-aheads by unit, and a node's own revision apart from its unit's | `WireEvaluation.cs:42-100` |
| A gate's process is built from steps: the landing, look and opinion steps' fields, each chosen among what the repository declares | `WorkflowGate.cs:117-219` |
| What a run would follow without its kind skips a graph newest for the newest version with steps | `WorkflowProcesses.cs:125` |
| Schema 1 holds the look only while an agent is at the opinion; the person's *Reviewed* answers a dispute and commits since; the `opinion` node reaches only on *settled* and the person's three answers | `WorkflowRun.cs:326-332`, `OpinionGate.cs:250`, `:356-365`, `OpinionNode.cs:78-85` |
| Schema 1 shows a go-ahead step it cannot start, and nothing holds the landing on it; the runtime leaves a pressed node ready, so a `go-ahead` node is never observed | `WorkflowRun.cs:357-365`, `NodeRuntime.cs:560`, `GoAheadNode.cs:30-44` |
| A look the work's choice adds is a step of the run that the version's graph has no node for | `WorkflowRun.cs:572-592`, `WorkflowRunNodes.cs:22-26`, `:152` |
| Every way to a landing must pass a look; nothing holds the opinion so | `NodeGraphs.cs:239-267` |
| A decoded version is judged as no saved graph, so two observed nodes of one plugin step pass | `WorkflowGraphs.cs:62-64`, `NodeGraphs.cs:287-297` |
| An acceptance lapses when its whole graph's digest moves; a run, a skip's scope and a trigger's firing are named by that digest | `NodeAcceptances.cs:117-121`, `NodeRuntime.cs:125-129`, `NodeLifecycle.cs:101`, `WireEvaluation.cs:643` |
| A quest bound at its take to a closure graph is closed only by that digest or one this home kept, and a graph is kept only while the choices still hold it | `NodeRuntime.cs:256-263`, `:793-800` |

## 1. Two engines, never on one run

**Decided.** A run bound to a schema-1 version stays D157's to its end: its gates read its steps and `WorkflowRuns.Derive`
draws it, as today. A run bound to a schema-2 version is the node runtime's. No run is governed by a decoded graph, so a
steps version reaches the runtime only as a new version saved as a graph: a person's save, never a migration.

**Why.** One engine governs a run, by its binding (§E7). A version is whole and its run keeps it (D157 point 6), and what a
version means is what its engine does with it. Running a schema-1 run on its decoded graph would change, with no save of the
person's, what its look does beside an opinion none could give or none read, and what a go-ahead before its landing holds
(§5); moving a run in flight would change it mid-run. Canary 38's comparison names those
differences; beside a save of a steps version as a graph (§7.2), it says what that save changes.

**What the decoder still does:** canary 38's read-only comparison (`WorkflowRunNodes`); a steps version drawn as a graph; and
the graph a steps version is saved as.

**Withdrawn:** FABRIC-RUN1c's left part, a bound run's decoded graph governing its unit with canary 38 run by the runtime.
§E7's *no engine runs beside the old one* holds run by run; FABRICRUN1's note already named the decoded graph's live
evaluation the second engine §E7 forbids.

**A consequence for the owner (§9).** The kinds schema 1 does not run (`check`, `stage`, `all`; D157 point 8) run as graph
nodes, or not at all. WORKFLOW1j and WORKFLOW1k as written build them for schema 1.

## 2. The run is the unit of its record

**Decided.** A run bound to a schema-2 version is a unit of the runtime's own, `run:<run>`. Its canonical record is its
binding, `<home>/workflows/runs/<run>.json`, written once by the driver (D157 §5.1), and its node record is `run:<run>`'s, so
a node record stays one unit's. Every node of the run's graph is recorded there, and each observes the subject its kind names
among the run's:

| Kind | Its subject | The revision it binds |
|---|---|---|
| `claimed` | the run's work: its chain's quests in its repository, finished as `WorkflowRuns` reads them | the run's: its chain's quests and its branch's tip, digested as a closing quest's are (`QuestClosing.Revision`) |
| `opinion`, `look`, `handed`, `pushed`, `pull-request`, `line`, a plugin's own node | the run's branch: the tree its landing would land, its newest session's here as `DriverWatch.cs:763-787` reads it, and the landed branch once the landing record names one | that branch's tip, read in the pass |
| `go-ahead` | the run's ask, the binding's `Ask`, where D135's answers for its act are read | the run's |
| `human`, `wait.unit`, `quest.close`, `stop`, `all` | the run itself | the run's |

- **The question FABRICRUN1's note left.** A quest's opinion and look act on its run's branch, at that branch's tip, and are
  recorded on the run's record. A quest's work cycle under Current is no run: it keeps its quest unit and its limit.
- **Why one record, not each node on its subject's.** A chain's run spans its sessions' branches, and a landing branch
  carrying several units' work is one result of several units, never a unit itself (§D2). The run's branch moves as its
  sessions start, so its nodes would scatter across records, and one evaluation's moves would be several writes.
- **Not the inventory's.** The run is listed nowhere the inventory's units are. Its subjects stay the inventory's, and tidy
  and the end judgement read them as today. The run's own end is its graph's.
- **Current never governs a run's subjects.** A quest whose run is bound to a named version is skipped already
  (`NodeRuntime.cs:270-272`). A branch whose quests' run is bound to a schema-2 version is the run's alone, so a landing rule's
  cycle never runs beside the run's graph on it.

## 3. Arming, governance and recovery

- **Arming discovers bound work.** The runtime is armed by the choices, as today, or by a run bound to a schema-2 version
  whose record does not say it ended. A saved graph version that no run binds arms nothing. A bound one arms it, though it
  names no performed node. Arming lists `<home>/workflows/runs/` and reads each binding naming a workflow, with the version it
  names, kept until one of those files is written again. A home with no runs folder costs one stat, so an install naming
  nothing is still judged with no file read (`NodeRuntime.cs:214-221`).
- **Governed whole.** Each bound schema-2 run is governed whatever its graph holds. Every built-in the runtime begins today it
  begins on the run (`NodeRuntime.cs:526-537`). A `go-ahead` is begun too, observing D135's answers on the run's ask
  (`GoAheadNode`), so a go-ahead its session raised moves the graph. Raising one nobody raised stays FABRIC-HUMAN1's.
- **One writer.** The runtime writes the run's record. A door keeps the person's word on it, as a held attempt's word is kept
  (`WorkPerformer.cs:449-462`), and the next pass applies it. A record that does not read holds the run, said, as a unit's
  does (`NodeRuntime.cs:494-497`).
- **Recovery is the runtime's own.** Attempts, reconciliation, retries and the awaited set work as for a unit. The run's
  plugin nodes are told under the run's unit, with its branch, its repository's identity and its tip. A run whose version
  stops reading is held whole, its attempts kept until it reads again (D157 §5.2).
- **A schema-1 run is unchanged.** The runtime still skips it, and its gates read its steps.

## 4. `WorkflowProcesses` stays the binding and gate interface

- **The binding is unchanged.** `WorkflowRunBindings.Plan` binds a graph version by its digest already
  (`WorkflowRunBindings.cs:104-108`).
- **A graph version is read as a process.** `WorkflowProcesses.Read` reads the version's gate nodes back as the steps
  `WorkflowProcess.Of` reads: the `handed` node's `form`, `accept`, `pattern` and `plugin`; the `look`'s `environment`; the
  `opinion`'s `reviewers`, `required`, `steps` and `recheck`. Each still chooses among what the repository declares, never
  beyond it, and the process carries the graph beside them.
- **A named version's graph has one landing.** It holds one `handed`, and at most one `opinion` and one `look`, since the
  process has one of each. Judged wherever a version is read, in both twins, as its graph is: refused at the save, and a
  stored version that breaks it is kept as written and read as unreadable with its reason. A run whose graph holds
  `quest.close` cannot start, saying why, until CUTOVER1g.
- **The graph's part of the landing gate.** Every door reads the gate once and refuses its first part that holds
  (`TreeGates.cs:39-53`). A run's gate gains a part after the look: the run's record evaluated as it stands, read-only, with
  the opinion and the review the door just read handed in. While that evaluation does not offer `handed`, the part holds,
  naming the node it waits on. Nothing a door evaluates is kept; the runtime's next pass moves the record the same way.
- **The gates' own engines wait for their node.** The second opinion is asked, and the look's set-up made, only once the
  run's record has begun its node. A node the person placed before them (a check, a person's task) comes first.
- **The holds stay the door's.** The quest's holds and the work's content stay the gate's first parts (D157 §2.2), never a
  node an authored graph must hold.
- **The words go** that say a graph version does not run: `WorkflowNamed.cs:120-121`, `HelpWorkflowProposals.cs`, and the
  CLI's `namedworkflows.ts:138` and `workflowdoor.ts:365`, `:691`. No e2e spec or rehearsal holds them at `d3d0d15ce`.

## 5. The person's two choices (WORKFLOW2b)

### 5.1 A look beside a disputed opinion: it goes on beside it

**Decided.** The look goes on beside an opinion that owes only the person's next press: D155's disputed findings and its
commits since (`PressAnswers`, `OpinionGate.cs:250`), which the look's *Reviewed* or the landing's *Accept…* answers
(`OpinionGate.cs:356-365`). An opinion an agent is still reading, one not read, and a required one none could give hold the
look.

- **How the graph says it.** The `opinion` node is reached when its gate owes only that press: its reading stands, answered
  by its working session. Its evidence names the gate's record, and its words say what waits for the press. The landing
  door still judges the dispute itself (§4), so nothing lands over an unanswered one.
- **Why.** The order exists so that the person looks once, at work that already answered the opinion (workflow design §2.2).
  A disputed reading has been answered by its session; what is owed is the person's adjudication, which D155 made the look's
  own press. Holding the look would hide that answer: under a required look, only *Go on anyway…* and *I looked myself…*
  would be left, and they say something else. No look answers a required opinion none could give, or an unread one
  (`PressAnswers` is false for both), so holding the look there hides nothing. Unknown is never passed (D157 point 2), and the
  person still looks once.
- **What moves.** `OpinionNode`'s table, which no graph today holds on a branch unit, since a branch's cycle cannot hold
  D157's gates (FABRICRUN1). The comparison reads where schema 1 stands through the same table, so the dispute row reads
  with no difference. The required-none and unread rows keep `look-beside-opinion`, a difference of schema 1's alone.

### 5.2 A go-ahead that cannot start: the landing holds

**Decided.** In a graph, a go-ahead before the landing holds it until the person's yes, or until the person passes the
go-ahead over with their words (§E2's skip).

- **Why.** A go-ahead is a node the person placed so that what follows waits for their yes. Only a reach or the person's
  skip satisfies an `after` wire (§E2), and an unstarted node is neither. Schema 1 goes on past one because a partial kind
  cannot start there, not because it chose to land without it; D157 §5.2's table has *cannot start* wait for the door that
  fixes it.
- **The doors.** The go-ahead is begun and answered through D135 (§3), and the person's skip of a run's node is a door of
  its own (CUTOVER1f), so a go-ahead nobody raised never strands a run.
- **What moves.** Nothing in schema 1. Its two go-ahead rows keep `cannot-start-holds-nothing`, schema 1's alone. A graph run
  proves the hold.

## 6. A look the work's choice adds (WORKFLOW2c): a hold kept outside the graph

**Decided.** A look that the chain's, the ask's or a set-up step's choice adds (workflow design §4.6) stays D154's hold at
every landing door, as in schema 1. The run's graph gains no node, and its binding and digest stay the version's. The run
draws it as a look added for this work, beside the graph.

**Why.** A version is whole and never edited (D157 point 6), and the person's presses act on the run, never on the workflow
(§4.6). A node added under a derived digest moves the graph that governs the run when the choice is made, and a choice made
mid-run lapses every binding the run holds unless §8's rule is assumed. D154's gate already holds every landing door, and its
set-up already waits while an agent reads the opinion, so the order holds with no node. Nodes after the landing wait on
`handed`'s reach, which the door holds.

**What it costs.** A node the version placed between the opinion and the landing does not wait for the added look; the version
asked for no look there. The comparison reads an added look as the gate's hold beside the graph, so its rows agree.

## 7. The rules a run's graph is held to

### 7.1 The protected order on every way (GRAPH1e)

**Decided.** No look and no landing can start with no opinion passed, whichever member of an `anyOf` a run takes, as no landing
can with no look passed today (`NodeGraphs.cs:257-267`). One check serves every gate of the order, under the same code,
`protected-order`, with words of its own, in both twins and on `graph-problems.json`.

A graph that lets a landing pass an unsettled opinion must govern no run, so the rule judges every graph
`NodeGraphs.Problems` judges, as `protected-wire` does. A stored graph version it newly refuses is kept as written and read as
unreadable with its reason, and a run bound to it cannot start (§E7, D157 §5.2). No decoded version is touched: the decoder
wires every gate into every later one, in no group (`WorkflowGraphs.cs:116-123`).

### 7.2 Two observed nodes of one plugin step (GRAPH1j)

**Decided.** No run binds a decoded graph (§1), so a bind owes it nothing. The rule is asked where a decoded graph becomes a
saved one: a steps version saved as a graph version is judged as saved, `observed-twice` with the rest, and refused naming both
nodes. The steps version stays readable and runs as it always did. The CLI's decoding judges a decoded graph as the driver's
does (as no saved graph), which GRAPHTWINS1 left undone.

## 8. A graph edit and the reaches it leaves (RUN1f)

**Decided.** A reach survives an edit of a live graph (Current's cycles and work cycles) that leaves its node's place unchanged:
the node's declaration, the wires into it, and whether it is an entry.

- **How.** An acceptance binds its node's place where it bound its whole graph (§E2's `graphDigest`), so invalidation follows
  dependency, as §E2 states it: what a changed node rests on, transitively through receipts and inputs, and nothing else. A
  skip, a rejection, a failure kept for the person and a trigger's firing hold by the same place. A run is still named by its
  unit and graph (`NodeRuns`), so the record keeps each edit's history.
- **The stored shape.** The place is added beside the graph digest an acceptance holds. One written before it is read as it was
  and lapses on any edit, so nothing lapses at the upgrade.
- **Why not keep lapsing everything.** Today an edit re-sends every reached performed node on every governed unit, and lets
  every skipped one start: an outside effect per edit that nobody asked for. A warning at Setup would still re-run them.
- **A named version is untouched:** it is never edited, and its run keeps it.

## 9. For the owner's review

1. Two engines, never on one run (§1), with FABRIC-RUN1c's left part withdrawn. Under it, WORKFLOW1j and WORKFLOW1k as written
   build for schema 1 kinds that only graph nodes would run: kept, or moved to graph nodes (FABRIC-PERFORM1l)?
2. The look goes on beside an opinion that owes only the person's next press (§5.1).
3. A go-ahead holds the landing in a graph (§5.2).
4. A look the work's choice adds stays the gate's hold, outside the graph (§6).
5. A reach survives an edit that leaves its place unchanged (§8).
6. **Not settled here: FABRIC-CLOSE1k.** A quest bound at its take to a closure graph that Setup has edited since gets no
   close, and nothing is said (`NodeRuntime.cs:256-263`, `:793-800`). Two ways, the owner's to choose:
   - *Keep the bound graph.* The take keeps the graph it binds beside its digest, as a version is kept, and the quest is closed
     by that graph whatever Setup holds since. It follows §E7's *one governs a run, by its binding*. Its cost: a node the
     person edited away, a deploy say, still runs for the quests bound before.
   - *The person's close.* The quest is said once, as bound to a graph Setup no longer holds, and its done closes it. Its cost:
     the graph's nodes after the claim never run for it.

   §8's place rule helps neither: the service holds the whole graph's digest.
7. A schema-1 run's branch is still governed by its landing rule's cycle beside D157's run. Left as it stands, since schema 1
   is unchanged here.

## 10. Build rows

| Row | What | After |
|---|---|---|
| FABRIC-CUTOVER1a | a run's graph read over its subjects, pure: the proof first | — |
| FABRIC-CUTOVER1b | a named version's graph has one landing, in both twins | — |
| FABRIC-CUTOVER1c | the runtime governs a bound schema-2 run | a |
| FABRIC-CUTOVER1d | `WorkflowProcesses` reads a graph version, and the gate holds on it | b, c |
| FABRIC-CUTOVER1e | selection, involvement and presentation | d |
| FABRIC-CUTOVER1f | the person's skip and stop on a run's node | c, e |
| FABRIC-CUTOVER1g | a run graph's close | c, d |
| FABRIC-GRAPH1e | the opinion held to the order on every way | — |
| FABRIC-GRAPH1j | a steps version saved as a graph, judged as saved | GRAPH1e |
| FABRIC-RUN1f | a reach kept across an edit | — |

- **CUTOVER1a** (driver). A run's graph evaluated over its record, each node observing its subject (§2), pure and nowhere
  kept; `OpinionNode` reaches on what the next press answers (§5.1); the comparison reads where schema 1 stands through the
  same table, and an added look as the gate's hold. Proof: `WorkflowRunNodesTests`' recorded runs, each version's graph read
  as a schema-2 graph over the same records, give the rows' node states, the dispute row and the `added` rows with no
  difference; `GateNodesTests` and canary 43 for the opinion's new reach.
- **CUTOVER1b** (driver, cli). §4's one landing, judged where a version is read. Proof: rows in `workflow-graph-versions.json`
  read by both twins: none, two landings, two looks, two opinions.
- **CUTOVER1c** (driver). §2 and §3: arming by binding, the run governed on `run:<run>`'s record, its subjects, a go-ahead begun,
  Current kept off its subjects, a schema-1 run still skipped. Proof: `NodeRuntimeTests` (an unbound graph version arms
  nothing; a bound one with no performed node arms and moves `claimed`, the opinion, the look and `handed` on its record; the
  no-graph count still zero with no runs folder; a run's branch not governed by its rule's cycle), and
  `NodeRuntimeLoopProcessTests` by class.
- **CUTOVER1d** (driver, cli). §4: a graph version read as a process, the graph's part of the landing gate, the opinion's ask
  and the look's set-up waiting for their node, `quest.close` cannot start, the words gone. Proof: `WorkflowGateTests` rows; a
  landing test where a person's task before `handed` holds *Accept…* naming it, and a *Reviewed* then an *Accept…* land in one
  pass; the opinion not asked while a check before it waits.
- **CUTOVER1e** (driver, cli, web-shell). `WorkflowProcesses.Otherwise` takes a graph newest and `Involvement.Lowers` reads its
  person part (a person's task removed lowers it too); the run's view and `daoris driver workflow run` draw a schema-2 run from
  its record in §5.2's words, the added look beside it; `show` and `use` say a graph runs. Proof: the twins' tests, and on the
  window a schema-2 run's Workflow view.
- **CUTOVER1f** (driver, modules, web-shell, cli). The person's skip and stop of a run's node, with their words, at the run's
  view and the terminal, kept on the run's record and applied by the pass. Proof: a go-ahead nobody raised, skipped with words,
  lets the landing go; Ask Daoris's door or exemption in `HelpCoverageTests`.
- **CUTOVER1g** (driver). A run graph's close: the run bound before its first take, so the take names its version's graph where
  it holds `quest.close` (`ClosureBinding.AtTakeAsync`), and the runtime closes the run's quests at its end. Proof: a schema-2
  run whose version ends at `quest.close` closes its quest; an unbound take keeps today's done.
- **GRAPH1e** (driver, cli). §7.1. Proof: `graph-problems.json` rows read by `NodeGraphsTests` and `graphproblems.test.ts`, an
  `anyOf` way past the opinion to the look and one to the landing.
- **GRAPH1j** (cli, driver). §7.2: `daoris driver workflow apply <id> --as-graph` saves a steps version's decoded graph as the
  next version, judged as saved, and says where the graph holds what the steps did not (§5); `decodeSteps` judges as the
  driver's. Proof: a version with two checks of one plugin refused naming both, its steps version still read; rows on
  `workflow-graphs.json`.
- **RUN1f** (driver). §8. Proof: a runtime test where an edit adding an unrelated node sends nothing again and starts no skipped
  node; an edit of one node's option lapses it and what rests on it alone; a record written before the build lapses nothing at
  the upgrade.

## 11. Rejected

- **Running a schema-1 run on its decoded graph,** now or once canary 38 agrees: it changes what a version does with no save
  of the person's (§1).
- **Each node on its subject's own record:** a chain's run spans branches and moves with its sessions (§2).
- **The look held behind a disputed opinion:** it hides the answer D155 gives the look (§5.1).
- **No wire from the opinion to the look, the look's timing left to the review's gate:** the look would start while an agent
  still reads the opinion, unlike schema 1 and the order.
- **A wire kind or a receipt that an opinion's reading satisfies:** a special case in §E2's one table.
- **The landing going on past a go-ahead that cannot start:** the go-ahead would hold nothing, and a node not started would
  satisfy a wire (§5.2).
- **A node the work's choice adds under a derived digest:** it edits the graph governing the run, mid-run (§6).
- **A whole-graph lapse kept, with a warning at Setup:** the warning does not stop the re-runs (§8).
- **The landing door judging on the runtime's record alone:** a *Reviewed* given a moment before an *Accept…* would be refused
  until the next pass (§4).
- **Two landings, two looks or two opinions in a named version's graph:** the process has one of each (§4).

## 12. What this document's gate does not cover

Documents only; nothing is built. The statements about today were read from the code at `d3d0d15ce`, and none was run. No test
or run shows the door's read-only evaluation agreeing with the runtime's next pass, a node observed on a subject apart from its
record's unit, or arming's cost on a home with bound runs: CUTOVER1a and CUTOVER1c are those proofs, and come first. `verify`
checks this document's place and links, and none of its words.
