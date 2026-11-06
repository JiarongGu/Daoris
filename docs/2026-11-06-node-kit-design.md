# NODEKIT1: each node kind one unit, as the workflow is one flowchart of nodes

Design, proposed for the owner's yes (D247). Read with `docs/areas/workflows-review-landing.md`, the fabric events design §E2 to
§E7 (the one state table, the wires, groups and joins) and the CODESHAPE1 parts kit (`docs/2026-11-04-codeshape-plan.md` §1.5).
Paths are under `src/Daoris.Desktop/Daoris.Desktop.Driver/` unless they name another root.

## 1. What is asked, and what it changes

The owner's direction: the workflow is generic and reads as a flowchart; every piece of process and monitoring work is a node, a
task like any other, differing only in its state logic (performed, observed, a person's); several nodes may feed the next under
conditions; so the application's code reflects nodes as its units. D245's note states the flowchart part for the steps past a
pull request.

The graph already is that flowchart. §E2 gives every node one lifecycle and one state table, and the kinds differ only by their
authority. §E3 and §E7 give the wires (`after`, `data`, `wait`, `trigger`, `failed`, `rejected`, `when`, `back`), the `allOf` and
`anyOf` groups, the `all` join and the `stop` sink. Fan-in is several wires into one node under those conditions, and it stays as it
is. What is not one unit is the **code of a kind**: its pieces sit in rows of about twenty shared tables and switches, so a kind
is read in a dozen files and a new one edits them all. This design makes each kind one folder registered once, as the parts kit
registers look passes and executors. The shared runtime reads the registrations, and the tables go.

## 2. The survey: where each kind lives today

### 2.1 The pieces of a kind and the shared files that hold them

| Piece | Shared file (and member) | What each kind adds there |
|---|---|---|
| Its id and the list | `Workflows/Nodes/NodeContract.cs` (`NodeKinds`, `NodeKinds.BuiltIn`) | a constant, and its place in the list |
| Its contract row | `NodeContract.cs` (`NodeAuthorities.BuiltIn`) | reach, reach and failure sources, the person's reject and skip, may end, evidenced, skip by a record |
| Its own declaration checks | `NodeContract.cs` (`NodeDeclarations.Problem`) | an `if` arm (`integrate`, `request`, `land`, the holds, `claimed`) |
| Its options | `Workflows/Nodes/NodeOptionFields.cs` | a row of typed fields (9 kinds) |
| What a run binds it to | `Workflows/Nodes/GraphRuns.cs` (`RunSubjects.Of`) | an arm: quest, run, branch or landing (20 kinds) |
| Its graph rules | `Workflows/Nodes/NodeGraphs.cs` (`Problems`, `OfTheLanding`, the landing's tail), `CompletionGraphs.cs` | rules about where it stands (13 kinds) |
| Its reading (observed) | `Workflows/Nodes/BuiltInNodes.cs` (`Observed`, the `Observe` switch), `Workflows/Nodes/WireEvaluation.cs` (the observation switch) | a list entry and a switch arm, delegating to `HoldNodes`, `LookNode`, `OpinionNode`, `GoAheadNode`, `WorkRequestsNode`, `CompletionNodes` |
| Its start | `Workflows/Nodes/NodeRuntime.cs` (the `if` chain in `EvaluateAsync`), `NodeGoverning.Begins`, `CompletionNodes.RunBegins` | an `else if` arm: `quest.close`, `integrate`, `request`, `check`, the begun kinds, a plugin's |
| Its door | `NodeRuntime.cs` (one property per door), `Workflows/Nodes/NodeRunKeeping.cs` (`StartLandings`, `StartPushes`, `StartTidies`), `Loop/Watch/WatchNodes.cs` (the wiring) | a property, a start loop and a constructor line |
| The person's acts on it | `Workflows/Nodes/RunNodeWords.cs` | a kind check (`review`'s accept and changes, `request`'s send) |
| The editor's offer | `Workflows/WorkflowPalette.cs` (`OfferedOf`, `RoleTakesOf`, the built-in list) | an arm where it is not offered alone |
| The run view's part | `Workflows/Domain/WorkflowGraphRun.cs` (`Cell`), `Workflows/Domain/WorkflowGate.cs` (the step kind map and the lowering judgements) | participation, executor and press; a step kind |
| Its terminal words | `Workflows/Domain/WorkflowRunWords.cs` (`Title`), `Workflows/Domain/WorkflowWords.cs` | a title arm (17 kinds) |
| Its page words | web `workflow/chart/stepKinds.ts` (`BY_KIND`), `workflow/said.ts` (`NODE_TITLES`), `workflow/edit/StepPicker.tsx` (`GROUPS`), `locales/{en,zh}/workflow.json` (`workflow.step.*`), `locales/{en,zh}/workflow.edit.json` (`workflow.edit.kind.*`) | a variant, a title key, a group, four catalogue lines |

With `NodeGoverning` and `WorkflowWords` beside them, that is 16 shared driver files and 7 shared web files: 23 in all.

### 2.2 Each built-in kind

Driver files are the source files naming the kind's constant (`NodeKinds.<Kind>`); tests are the test files naming it. A kind also
lives in files that name it only through its own types (a door, a record), so these are floors.

| Kind | State logic (reach; reached by) | Its own unit(s) today | Door or executor | Driver files | Test files |
|---|---|---|---|---|---|
| `claimed` | observed; the ledger | `BuiltInNodes.Claimed` | none | 18 | 17 |
| `line` | observed; git | a `BuiltInNodes` arm | none (the end judgement's keeper writes the fact) | 8 | 7 |
| `push` | performed; git | a `BuiltInNodes` arm, `NodeEvaluations`' remote read | `Workplace/PushDoor.cs` (`IPushDoor`) | 11 | 11 |
| `human` | pressed; the person | none | none | 4 | 14 |
| `look` | observed; the person through the review gate | `LookNode` | none | 16 | 11 |
| `go-ahead` | pressed, begun on a run; the person | `GoAheadNode` | none | 10 | 7 |
| `wait.unit` | waited; a commitment | `NodeWires`' `wait` wire | the commitment adapter | 5 | 8 |
| `quest.close` | performed; the close door | `QuestClosing`, `QuestCloser`, `NodeClosing` | the service's close door | 11 | 6 |
| `all` | wired; its members | `WireEvaluation` step 3 | none | 8 | 5 |
| `opinion` | observed; the opinion gate | `OpinionNode` | the opinion presses (`Review/`) | 20 | 12 |
| `stop` | wired; the path into it | `WireEvaluation` step 3 | none | 5 | 2 |
| `check` | performed; the gates | `CheckResults`, `CheckPort` | `CheckExecution` over `HeavyExecutor`, through `WorkPerformer` as an attempt whose plugin is `check` | 19 | 8 |
| `integrate` | waited; the integration record | `IntegrateNode`, `NodeIntegrating` | `IIntegrationDoor`, `IntegrationTransport` | 24 | 9 |
| `work.requests` | observed; the quests' records | `WorkRequestsNode`, `RequestHolds` | none | 12 | 5 |
| `request` | waited; a commitment | `RequestNode`, `RequestOptions`, `NodeIntegrating.RequestAsync` | `IRequestPort` (the service) | 11 | 4 |
| `land` | performed; the landing record | `LandNodes`, `Landing`, `LandAttempts`, `LandAttemptSettling`, a `BuiltInNodes` arm | `Workplace/LandingDoor.cs`, `LandPerformer` | 23 | 22 |
| `review` | pressed; the person's Accept | the same `BuiltInNodes` arm as `land` | the landing door (one press lands) | 17 | 6 |
| `quest.holds`, `content.holds` | observed; the holds Daoris reads | `HoldNodes` | none | 9 each | 1 each |
| `monitor` | observed; the landing record | `CompletionNodes.Monitor`, `CompletionGraphs` | none (`PullRequestStates` asks `work/state`) | 7 | 1 |
| `tidy` | performed; the landing record | `CompletionNodes.Tidy`, `CompletionGraphs` | `Workplace/TidyDoor.cs` (`ITidyDoor`), `Workplace/ChainTidied.cs` | 8 | 2 |

Driver files per kind: median 11, from 4 (`human`) to 24 (`integrate`); 76 distinct driver source files name some kind, and 47 test
files. Every kind is also in the 7 shared web files (`stepKinds.ts`, `said.ts`, `StepPicker.tsx` and four catalogues).

What adding kinds has cost, from the history:

- `monitor` and `tidy` (FABRIC-SQUASHCHAIN1): 20 driver files in one commit (12 of them shared tables or wiring), 10 web files in
  another, and 3 test files in a third; 33 files for two kinds.
- `push` (G8): 32 files in one commit.

### 2.3 A plugin's step

A plugin's step enters the same machinery as data rather than as a row:

1. **Declared** in its manifest's `steps`, read by `Plugins/Domain/PluginSteps.cs` into the one `NodeDeclaration` every built-in
   is, of the kind `<plugin>/<step>`, and judged by `NodeDeclarations.Problem`. It may declare outcomes (a decision, DECIDE1), a
   `perform` (transient codes, idempotent or reconciled), resources, and its `title` and `about` as authored text (D222).
2. **Offered** by `WorkflowPalette.Build(plugins)` beside the built-ins, worded by its manifest.
3. **Its contract row** is made from its declaration by `NodeAuthorities.Of`'s second arm: reached and failed by the plugin's
   word, rejected and skipped as declared.
4. **Performed** through `NodeRuntime`'s last `else if` and `WorkPerformer.StartAsync`: the attempt is journalled with its key, sent
   as `work/perform`, reconciled with `work/reconcile`, and heard, believed and applied through `WorkHearing`, `WorkBelief`,
   `WorkReceipting` and `WorkOutcomes`. **Observed** it is begun as a built-in is and told to its plugin in the awaited set
   (`NodeTelling`).
5. **One step name has Daoris's half**: `pull-request` (`PullRequestSteps`, 8 files) keeps the address its evidence gives on the
   landing (`NodeRunKeeping.KeepPullRequests`), refuses a bind no plugin can serve, and is what a `monitor` watches.

So a plugin's step already meets the built-ins at `NodeDeclaration` and the one lifecycle, and `check` already rides the plugin's
performing path. What does not meet is everything in §2.1: the tables are keyed by built-in kind, and `IsPlugin` branches stand
in 8 files beside them.

## 3. The kit

### 3.1 The unit: a folder per kind

Each kind is one folder under `Workflows/Kinds/`, and its tests are in the same folder of the test project. The three exemplars:

```
Workflows/Kinds/
  NodeKindAttribute.cs  NodeGroup.cs  INodeKind.cs  IObserves.cs  IStarts.cs  IPressed.cs  IWatchable.cs
  NodeKit.cs  NodeReading.cs  NodeDoors.cs  NodeDoorWiring.cs
  Monitor/      MonitorNode.cs      the registration and declaration: contract row, subject, look, title, its save rules
                MonitorReading.cs   its state logic: the watched step's state read into an outcome
  Tidy/         TidyNode.cs         the registration and declaration
                TidyReading.cs      its record read back (ChainTidied at the landing's tip)
                TidyDoor.cs         its door, moved from Workplace/
  Push/         PushNode.cs  PushReading.cs  PushDoor.cs
  PullRequest/  PullRequestStep.cs  Daoris's half of any plugin's `pull-request` step: its state reading, the address kept, the bind's refusal
Daoris.Desktop.Driver.Tests/Workflows/Kinds/
  NodeKitTests.cs   Monitor/MonitorNodeTests.cs   Tidy/TidyNodeTests.cs   Push/PushNodeTests.cs
```

The declaration file of a kind is `<Kind>Node.cs`, and nothing else in the folder is: the web's list is generated from those
files alone (§3.8). A record other areas read stays in its area: `ChainTidied` and `LandedBranches` are the landing record's, read by
the keeper and the press, so they stay in `Workplace/`.

### 3.2 The registration

```csharp
// The seam, as the parts kit's: name, phase and after, each a literal or a NodeKinds constant.
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
internal sealed class NodeKindAttribute(string name, NodeGroup group, params string[] after) : PartAttribute(name, group, after);

/// The picker's groups, in its order: the phases. Within a group the order is After, ties by name.
public enum NodeGroup { Work, You, Checks, Landing, Others }

[NodeKind(NodeKinds.Monitor, NodeGroup.Landing, "*/pull-request")]
internal sealed class MonitorNode : INodeKind, IObserves { … }
```

`INodeKind` is the declaration every kind gives, pure and built with no wiring:

| Member | Was |
|---|---|
| `Authority(NodeDeclaration node)` | `NodeAuthorities.BuiltIn[kind]`; a plugin's step reads its declaration |
| `Subject(NodeDeclaration node, GraphRead graph)` | `RunSubjects.Of`'s arm |
| `Options` | `NodeOptionFields`' row |
| `Problem(NodeDeclaration node)`, `Problems(GraphRead graph, NodeDeclaration node)` | `NodeDeclarations.Problem`'s arm; the kind's rules in `NodeGraphs` and `CompletionGraphs` |
| `Part(NodeDeclaration node)` | `WorkflowGraphRun.Cell`'s participation, executor and press; `WorkflowGate`'s step kind |
| `Offered`, `RoleTakes` | `PaletteKind.OfferedOf`, `PaletteKind.RoleTakesOf` |
| `Title` | `WorkflowRunWords.Title`'s arm (the terminal's English) |
| `Look` | the page's `BY_KIND` variant: work, you, check, holds, landing, line, other |

The state logic is what a kind implements beside it, one interface per way a node's state moves:

| Interface | Its state logic | Kinds |
|---|---|---|
| `IObserves` | `Observe(NodeReading) → NodeObservation`: what its records say, read in the pass | `claimed`, `line`, `look`, `opinion`, `go-ahead`, the holds, `work.requests`, `monitor`; and `land`, `push`, `tidy`, whose outcome is read back from the record their door wrote |
| `IStarts` | `Door(NodeDoorWiring) → INodeDoor`: started once per node and binding, off the pass | `land`, `push`, `tidy`, `quest.close`, `integrate`, `request`, `check`, a plugin's performed step |
| `IPressed` | `Acts(NodeStatus)`: the acts its node door takes beyond the lifecycle's skip, stop and again | `review` (accept, changes), `request` (send, go on without it); `human` once its doors are built |
| `IWatchable` | `States` and `Read(NodeReading) → string?`: a settled state another node may watch | a plugin's `pull-request` step (§4) |
| none | reached by the wire evaluation itself | `all` (its members), `stop` (the path into it) |

`NodeReading` is what the pass hands an observing kind: the unit, the node, the binding, the graph as read (so a kind can name the
node it follows) and the pass's reads (`run.Git`, `run.Opinions`, `run.Reviews`, `run.GoAheads`, `run.Requests`), each kind taking
its own. The reads stay the pass's: a kind asks for nothing on its own.

### 3.3 What the shared runtime reads from the registrations

`NodeKit` is built once from the assembly (`Parts.Declared<NodeKindAttribute>`, `Parts.Build` with a parameterless constructor), so
the save, the palette and the reading of a version use it with no runtime. `NodeKit.Of(kind)` returns the built-in's unit, or for
`<plugin>/<step>` the one `PluginStep` adapter over its declaration, with Daoris's half where its step name has one (§3.6).

| Today | Reads instead |
|---|---|
| `NodeKinds.BuiltIn` | `NodeKit.Kinds`, in its order |
| `NodeAuthorities.BuiltIn`, and `NodeAuthorities.Of(node)` behind its callers in 11 files | `NodeKit.Of(node.Kind).Authority(node)`; `NodeAuthorities.Of` stays a one-line door onto it |
| `NodeDeclarations.Problem`'s kind arms | the generic checks, then `Of(kind).Problem(node)` |
| `NodeGraphs.Problems`' kind rules, `CompletionGraphs` | the generic rules, then each node's `Problems(graph, node)` |
| `RunSubjects.Of` | `Of(kind).Subject(node, graph)` |
| `NodeOptionFields.Of` | `Of(kind).Options` |
| `BuiltInNodes.Observed`, its `Observe` switch, `WireEvaluation`'s observation switch | `Of(kind) is IObserves observes ? observes.Observe(reading)` |
| `NodeGoverning.Begins`, `CompletionNodes.RunBegins` | the runtime begins a node whose kind `IObserves` or `IStarts` |
| `NodeRuntime`'s `if` chain, `NodeRunKeeping.StartLandings`, `StartPushes`, `StartTidies` | one loop: each active node whose kind `IStarts` started on its door (`NodeDoors`) |
| `NodeRuntime.Landing`, `.Pushing`, `.Tidying`, `.Checks`, `.Integration`, `.Requests` | `NodeDoors`, built from the kit and the wiring |
| `RunNodeWords`' kind checks | `Of(kind) is IPressed pressed ? pressed.Acts(status)` |
| `PaletteKind.OfferedOf`, `RoleTakesOf`, the palette's built-in list | the units' `Offered` and `RoleTakes`, in the kit's order and groups |
| `WorkflowGraphRun.Cell`'s map, `WorkflowGate`'s step kind map | `Of(kind).Part(node)` |
| `WorkflowRunWords.Title`'s kind arms | `Of(kind).Title` |

### 3.4 What stays shared

- **The one lifecycle**: `NodeLifecycle`, `NodeStates`, `NodeCause`, `NodeSource`, `NodePort`, the acceptance and its binding (§E2).
- **The wire evaluation**: eligibility, `allOf` and `anyOf` groups and their witness, receipts, firing keys, lapsing, the `all`
  join and the `stop` sink (`WireEvaluation`, `WireEligible`, `WireReceipts`, `WireLapsing`). `all` and `stop` get units for their
  declaration and words; their reaching is the evaluator's, since it is wiring, not a kind's reading.
- **The graph's generic rules**: bounds, cycles, reachability, one landing (`NodeGraphs`), and a version's reading and digest
  (`NodeGraphReading`, `NodeGraphPlaces`).
- **The rules several kinds share**, the kit's: the protected order before the landing (`Kinds/ProtectedOrder.cs`) reads the gates
  together, each gate reached before every landing and wired straight into it, and a look passed on every way to one, with the wire
  rule said only where the order holds. A kind takes its part by `INodeKind.Guard` (`Before` for `opinion` and `review`, `Passed` for
  `look`), and `NodeKit.Problems` runs the rule once per save, then each kind's own `Problems`, so `NodeGraphs` names no gate kind.
  Each gate's own `Problems` cannot say it: every gate would judge the whole order again, and the wire rule's silence where the
  order already failed spans the gates.
- **The run's keeping**: governing, owing, telling, the person's skips and stops, back to work, the run's end and reopening
  (`NodeGoverning`, `NodeOwing`, `NodeTelling`, `NodeRunKeeping`'s non-door half), and the performer's attempt journal for a
  port (`WorkPerformer`, `WorkHearing`, `WorkBelief`, `WorkReceipting`, `WorkOutcomes`).

### 3.5 Doors and executors

A starting kind's door is built by its unit from one `NodeDoorWiring` the loop hands the runtime: the home, the node records, the
clock, whether this loop holds the lease and may start now (lease and drain), the machine log, the service client, the plugin
port, the heavy executor and the integration transport. `WatchNodes` stops constructing `LandingDoor`, `PushDoor` and `TidyDoor`
by hand. A test replaces one kind's door through `NodeDoors.With(kind, door)`, which takes the place of today's door properties.

### 3.6 A plugin's step meets the same registration

- `NodeKit.Of("<plugin>/<step>")` returns `PluginStep`: one `INodeKind` over the step's declaration (the authority from it, its
  options none, its title and about its manifest's, its look `plugin` or `decision`), and `IStarts` or `IObserves` by its reach,
  through the plugin port (`work/perform`, the attempt journal) or the awaited set. The `IsPlugin` branches in the runtime go:
  the kit's `Of` is the one place a kind's name is split.
- Daoris's half of one step name registers as a kind does, with the name a role: `[NodeKind("*/pull-request", NodeGroup.Landing,
  NodeKinds.Push)] PullRequestStep`. `Of` hands that half to the plugin's step whose name it is. It holds `PullRequestSteps`' bind
  refusal, `KeepPullRequests`' address keeping, and the `IWatchable` state reading (§4).
- `check` stays Daoris's kind and starts on a port (`CheckExecution`) through the same attempt journal a plugin's step uses. Its unit
  says so by `IJournalled`, which the performer asks for the port and the perform.

### 3.7 The ids

`NodeKinds` keeps the ids alone, as `NodeStates` and `WireKinds` keep theirs: they are the words the saved files, the page and
the terminal use. The list `NodeKinds.BuiltIn` and the helpers `IsLanding`, `IsHold` and `IsPlugin` leave it for the kit. An
attribute names its kind by the constant, and `tools/orient-registered.mjs` resolves `NodeKinds.<Name>` from that one file, so
`docs/index/registered.md` lists the node kinds in the palette's order. `NodeKitTests` holds the constants and the units equal both
ways: no id without a unit, no unit without an id.

### 3.8 The web follows, generated

- **Generated.** `tools/contracts/PageNodeKinds.cs` writes `NODE_KINDS` into `bridge/generated/routes.ts` from the kit, as
  `PageSettings` writes `SETTINGS`: each built-in's `kind`, `group`, `look`, `reach` and `outcomes`, in the kit's order.
  `tools/contracts.mjs` adds the `<Kind>Node.cs` files under `Workflows/Kinds/` to its sources, by name, so a change to a kind's
  reading or door stales no page file.
- **Read.** `stepKinds.ts` takes `look` from it and loses `BY_KIND`; `StepPicker.tsx` takes `group` and order and loses `GROUPS`;
  `said.ts` titles a kind `workflow.step.<kind>` and loses `NODE_TITLES`. A plugin's step is still worded by its manifest.
- **Words.** The catalogues stay authored per language (D222). The run titles' keys take the kind's id (`workflow.step.quest.close`
  for `workflow.step.questClose`), as the editor's `workflow.edit.kind.<kind>` already do, so a kind has one stem. A built-in's
  outcome is chrome and is translated (`workflow.outcome.<kind>.<outcome>`); a plugin's stays as its manifest wrote it.
- **Checked.** `names-check.mjs` reads the generated list, as it reads the person's presses (`person-words.mjs`), and requires each
  kind's two keys and each built-in outcome's key in every language.
- **Not generated.** The palette answer stays the editor's per-place offer (plugins, role takes, what is offered alone). The two
  answer different questions: what may be added here, and how a kind is drawn and named.

### 3.9 What the kit rejects

- **One class per kind with no folder.** The class would hold the contract row and the reading, and leave the door in
  `Workplace/`, the tests in a flat folder of 85 files and the words' keys in shared maps: a kind still read in many places.
- **Keeping the tables and adding attributes beside them.** Every row declared twice, and a kind whose table row and unit disagree
  is the defect D228 forbids. Each table goes in the chunk that moves its last row.
- **The id only in the unit** (`MonitorNode.Kind`, `NodeKinds` deleted). It renames call sites in about 120 files for no behaviour,
  and the ids are the saved files' words, which other code compares, not a kind's private name.
- **The page reading the kinds from the palette answer at runtime.** The chart, the run view and the stories draw with no palette
  in hand, and the editor would then hold two lists of the same kinds.
- **The page restating the list**, as today: the 7 shared web files every kind edits.

## 4. D245's note, built in the kit

1. **The monitor watches the step it follows.** A `monitor` has exactly one node wired straight into it by `after`, and that node's
   kind is `IWatchable`. The save refuses a monitor with none, with two, or after a step that reports no state. It reads that step's
   state through the step's own `Read` and is reached when the state settles, its outcome the state; while the state is open or
   unanswered it is pending, and nothing fails it. Its source is `NodeSource.Watched` (new): the state its watched step's reading
   reports. Its subject is its watched step's. Today's rule that a pull-request step reaches it, and that a tidy follows a monitor,
   leaves `CompletionGraphs` for the two units.
2. **Its outcomes are its watched step's states, written in the file.** A built-in may declare outcomes only where its kind takes
   them: a monitor's must equal its watched step's `States`, in order, and every other built-in's are none. The save and the editor
   write them (`"outcomes": ["merged", "closed"]`), so the file describes the graph the chart draws and the digest covers it, as a
   plugin's decision's outcomes are written today. `NodeOutcomes.Fits`, the `when` wire and the chart's ports read them unchanged.
3. **A pull request reports `merged` or `closed`.** `PullRequestStep.Read` reads the answer the opening plugin gave through
   `work/state` (asked at D148's moments, as now): completed is `merged`, a squash included and said so; abandoned is `closed`; open
   or unanswered is no state yet.
4. **The prebuilt set routes on them.** The shipped pull-request delivery wires `when merged` from the monitor to `tidy`, and
   `when closed` to a `stop`, its end still `tidy`. The `stop` is reached on a `when` path into it as on a failure's or a rejection's
   (`WireEvaluation.PathInto`, one case more), so a closed request stops the run short of its end and the run page says where. The
   editor offers a person's task in its place; the shipped set takes the stop because it is built end to end today, and a `human`
   node's doors are not (4 driver files name it).
5. **The tidy no longer fails on a closed request.** It starts only on `when merged`. Its door ends the chain's parts where the
   request merged and does nothing otherwise; `TidyDoor.NotMerged` and its failure go, and its authority names no failure source.
   The save refuses a `tidy` no `when merged` way from a monitor reaches.
6. **Any step that reports a state can be monitored.** That is `IWatchable`. Today one step reports one: a plugin's
   `pull-request`. A plugin's own step reporting a state needs its manifest to declare its `states` and `work/state` asked for it,
   which is a row of its own (§7).

## 5. The chunks

Each chunk compiles on its own, runs only its filtered tests, and ends with `npm run quick` clean. The model is the builder's.

### Chunk 1: the exemplar

**1a. The kit and every kind's declaration** (Opus: the seam, and a wide, shallow move). The kit's files in `Workflows/Kinds/`; for
each of the 21 kinds and `*/pull-request` a folder with its `<Kind>Node.cs`, holding its contract row, subject, options, its own
checks and save rules, part, offer, title and look, moved from the tables; its state logic still calls today's static code
(`HoldNodes.Observe`, `LandNodes.Problem`, …) from the unit. Deleted in the same chunk: `NodeKinds.BuiltIn` and its helpers,
`NodeAuthorities.BuiltIn` (its `Of` a one-line door onto the kit), `NodeDeclarations`' kind arms, `NodeOptionFields`' table, `RunSubjects.Of`'s switch, `PaletteKind`'s two arms,
`WorkflowRunWords.Title`'s kind arms, `WorkflowGraphRun.Cell`'s map and `WorkflowGate`'s step kind map. Edited: `NodeContract.cs`,
`NodeGraphs.cs`, `GraphRuns.cs`, `NodeOptionFields.cs`, `WorkflowPalette.cs`, `WorkflowRunWords.cs`, `WorkflowGraphRun.cs`,
`WorkflowGate.cs`, `NodeWires.cs` (the evidence schema of an observed kind), `tools/orient-registered.mjs`.
*Proof:* `NodeKitTests` written first and failing to compile (the order builds from the assembly; constants and units equal both ways;
a duplicate, a cycle and an unknown `After` refused; `Of` on a plugin's step and on a `pull-request` step; an older kit `Without` a
row refuses its kind, as the integration design's §17.6 point 6 asks of an older build's table). Then green and unchanged: `BuiltInNodesTests`, `NodeLifecycleTests`, `NodeGraphsTests`,
`SavesRefusedTests`, `NodeWiresTests`, `WorkflowPaletteTests`, `LandNodeKindTests`, `RequestNodeKindTests`, `HoldNodesTests`,
`WorkRequestsNodeTests`, `GateNodesTests`, `PluginStepsTests`, `WorkflowGraphPresetsTests`, `GraphGatesTests`; modules
`DriverModuleWorkflow*Tests`; `node --test tools/orient-index.test.mjs`.

**1b. One dispatch** (Opus). The observation switch in `WireEvaluation` and `BuiltInNodes`, the start chain in `NodeRuntime`, the
three start loops in `NodeRunKeeping`, the acts in `RunNodeWords` and the door properties become kit reads through `IObserves`,
`IStarts`, `IPressed` and `NodeDoors`; `WatchNodes` hands one `NodeDoorWiring`. Every unit still delegates to today's code. The
tests that set a door property use `NodeDoors.With`. `NodeRuntime.cs` is 25 bytes under the 40 KiB budget: the chain it loses pays
for the loop.
*Proof:* green and unchanged: `WireEvaluationTests`, `NodeRuntime*Tests`, `GraphRunLandingTests`, `ChatGraphRunTests`,
`PullRequestKeepingTests`, `WorkflowRunBindingTests`, the integration and request node tests; the process classes it touches
(`WorkflowPullRequestStepsProcessTests`, `WorkflowSquashChainProcessTests`) filtered, under `process.runsettings`.

**1c. `monitor`, `tidy` and `push` whole, with D245's note** (Opus). `MonitorReading`, `TidyReading`, `PushReading` from
`CompletionNodes` and `BuiltInNodes`' push arm; `TidyDoor` and `PushDoor` into their folders; `PullRequestStep` takes
`PullRequestSteps` and the state reading; `CompletionNodes.cs` and `CompletionGraphs.cs` deleted; `NodeSource.Watched`; the
`when` path into a `stop`; `TidyDoor`'s closed failure removed; `Defaults/workflows/pull-request.json` wired as §4 says; the
monitor's outcomes worded on the page (`workflow.outcome.monitor.merged`, `.closed`, en and zh, in `editWords.ts` and `said.ts`).
The area document's rule 31 and its parts row rewritten.
*Proof, red first:* `MonitorNodeTests` (outcomes from the watched step; a squash read as merged; open and unanswered pending; the
save refuses no watched step, two, a step with no state, and outcomes not its states), `TidyNodeTests` (started only on `when
merged`; a closed request starts nothing and fails nothing; the save refuses a tidy no `when merged` reaches), `PushNodeTests` (the
push cells moved from `BuiltInNodesTests`), a `WireEvaluationTests` cell (a `when` path reaches a stop), `WorkflowGraphPresetsTests`
and `SetupPresetsTests` (the shipped wiring), `WorkflowSquashChainProcessTests` filtered (merged tidies, closed stops and ends
nothing); web `said.test.ts`, `editWords.test.ts`, `i18n:check`, `names:check -- --strict`.

### Chunk 2: the rest by mirror, in groups

Each group moves its kinds' state logic, doors and tests into their folders and deletes the static code it leaves empty. No
behaviour changes, so the proof is each moved test class green, filtered, plus `npm run quick`.

| Group | Kinds | Files that move or empty | Model |
|---|---|---|---|
| 2a | `claimed`, `line`, `quest.holds`, `content.holds`, `work.requests` | `BuiltInNodes` (emptied), `HoldNodes`, `WorkRequestsNode`, `RequestHolds`; `BuiltInNodesTests`, `HoldNodesTests`, `WorkRequestsNodeTests` | Sonnet |
| 2b | `human`, `look`, `go-ahead`, `opinion` | `LookNode`, `GoAheadNode`, `OpinionNode`, `RunNodeWords`' kind acts; `GateNodesTests` | Sonnet |
| 2c | `land`, `review` | `LandNodes`, `Landing`, `LandAttempts`, `LandAttemptSettling`, the node half of `LandingDoor`; `LandNodeKindTests`, `GraphRunLandingTests` | Opus |
| 2d | `quest.close`, `check` | `QuestCloser`, `QuestClosing`, `NodeClosing`, `ClosureBinding`; `CheckPort`, `CheckResults`, `CheckExecution` | Opus |
| 2e | `integrate`, `request`, `wait.unit`, `all`, `stop` | `IntegrateNode`, `NodeIntegrating`, `RequestNode`, `RequestOptions`; the area's parts table and *Where a change goes* rewritten | Sonnet |

### Chunk 3: the web's mirror

**3.** (Sonnet) `tools/contracts/PageNodeKinds.cs` and `Program.cs`; `tools/contracts.mjs`' sources by name and its test;
`stepKinds.ts`, `StepPicker.tsx` and `said.ts` reading `NODE_KINDS`; the run titles' keys renamed to the kind's id in
`locales/{en,zh}/workflow.json` and their readers; `scripts/kind-words.mjs` and `names-check.mjs`.
*Proof:* `node --test tools/contracts.test.mjs`; `npx vitest run` on `stepKinds.test.ts`, `said.test.ts`, the picker's and the
chart's tests; `npm run typecheck` in the web; `i18n:check`, `names:check -- --strict`; `npm run quick`.

## 6. The weakest points

1. **Two performing shapes remain.** `land`, `push`, `tidy` and `quest.close` start a door once per node and tip that writes a
   record the next pass reads back; `check` and a plugin's step start a port through the attempt journal, with keys, receipts and
   reconciliation. The kit registers both the same way and dispatches both through `IStarts`, but a kind's author still chooses a
   shape, and an unknown outcome is reconciled by two mechanisms. Making every performed node an attempt on a port is a choice of
   its own, not made here.
2. **The watch is generic in its shape, proven by one case.** `IWatchable` has one implementation, the pull request's, whose state is
   asked at D148's moments by its address. A second (a plugin's deploy, an integration) is where the seam is tested, and nothing in
   these chunks builds one.
3. **The ids stay a list beside the units.** `NodeKinds` keeps the constants, held to the units by a test, and the orient tool learns
   to resolve one class's constants. A new kind still edits that file and the four catalogues besides its folder: the 23 shared
   files fall to 5, not to none.

Beside them: between 1a and the end of chunk 2 a unit's state logic is a call into a static class outside its folder, so a reader
follows one more hop; and a version saved on main between D245's build and 1c holding a `monitor` with no outcomes is refused at
its next save and held at its bind (no release carried it, so nothing converts it).

## 7. Not decided here

- A plugin's own step reporting a state: the manifest's `states`, and `work/state` asked for a step by its attempt rather than a
  pull request by its address.
- One performing shape for every performed node (point 1 above).
- A person's task in the shipped set's `closed` route, once a `human` node's doors are built.
