# WORKFLOWCLEAN1: the design. A place follows a saved graph, the runtime lands it, and the old shapes convert on the person's press

Status: accepted by the steward 2026-11-02 under D227 and D228 (§12); proposed 2026-11-02 from D227. Read on main at `e04e16dc9`, which carries D228; D227 itself is at `5ea8e2489`. The reading was read-only and no tests were run. This design changes a foundation: who performs a landing, what a place follows, and what `driver.json` means. Under D191 it therefore needs two makers' reads before the steward accepts it.

Paths are repository-relative, as the inventory spells them:
- `Driver/` is `src/Daoris.Desktop/Daoris.Desktop.Driver/`
- `Modules/` is `src/Daoris.Desktop/Daoris.Desktop.Modules/`
- `Web/` is `src/Daoris.Web/src/`

## 0. In short

- **What a place follows.** A place follows a saved graph. Row 6 of the choosing order becomes **the home's default**, a new `defaultWorkflow` key naming a saved copy of a shipped default. A fresh home is seeded with `merge-after-accept`. A fresh workspace or repository inherits that default with nothing written. Each *Set up…* delivery saves, where the home has none, the copy of its shipped workflow, and chooses it, through `WORKFLOW_USE`'s own edit. `SET_LANDING` and *Current* stop being written.
- **The landing rule retires field by field** (§1.5):
  - method and press go into the graph;
  - `pattern`, `plugin`, `key` and `squash` become `land` options; an open one resolves at the bind to a derived default, never to a rule;
  - `tidy` retires (the look's clean-up is the one remover);
  - `cycle` folds into the version;
  - `integrate` moves beside the line (`linePolicies`).
- **The runtime is the one performer of `land`.** The presses write the person's word and wait. `AutoLander`'s choosing, the access setup's `landing: rule`, the door's tidy, `GateSteps`' landing half and `LAND_WITHOUT_LOOK` retire. A terminal with no loop keeps the word and never lands.
- **Chats are not left the old way.** CHATFLOW1f is taken up in the wave. Otherwise, once places follow graphs, every chat in a tree of its own would land the old way.
- **Saves of old shapes are refused at the one save point.** A new steps, `handed` or `land`+`accept` save is refused by `ReadVersion(onSave)` and `NodeGraphs.Read(onSave)`, so every door refuses alike.
- **One conversion plan.** It is planned (`CONVERT_PLAN`, a dry run with a digest), applied by one Confirm in the window (`CONVERT_APPLY`, receipt, `moved` guard) and undoable (`CONVERT_UNDO`). It converts steps versions, `handed` versions, `land`+`accept` versions, every place on Current, and the rule-only shapes. Bound runs are never touched.
- **Every transitional path has an end and a deleting row** (D228). A path kept for an unconverted home, or a run bound to an old version, is kept only until the census reads ended (§4.7). Row z then deletes it.

## 1. A place follows a saved graph (D227 point 1)

### 1.1 What each place follows

| Place | Follows | Written, and by what |
|---|---|---|
| A fresh home (the build creates its `driver.json`) | `merge-after-accept`, this machine's copy | At the home's creation, once: the copy saved (`from` = the default's id and digest), and `defaultWorkflow: "merge-after-accept"`. A home that already exists is never seeded; it goes through §4. |
| A fresh workspace | The home's default (row 6, level `home`) | Nothing |
| A fresh repository (`daoris import`, a registry row, not driven) | Its workspace's choice, else the home's default | Nothing |
| *Set up…* › *Merge after you accept* | The copy of `merge-after-accept` | `WORKFLOW_SAVE` (only where the home has no copy with that `from` and those gates), then `WORKFLOW_USE` at the place |
| *A branch to push* | The copy of `branch-to-push` | The same |
| *A pull request* | The copy of `pull-request`, `plugin` left open (resolved at the bind, §1.5) | The same. `no-landing-plugin` is still refused where no installed plugin serves the remote's platform (`Driver/Setup/PlacePlan.cs:234-242`). |
| *Integrated into the line* | The copy of `integrated` | The same, plus `SET_LINE`'s policy where the line has none (§1.5). `no-line` is still refused (`:244-248`). |
| *Not driven* | Unchanged | `SET_DRIVABLE` off, as today |
| *Keep how it lands* | Unchanged | Nothing about the workflow (SETUPUX1e). It no longer takes the ticks (§1.3). |

**Row 6.** `WorkflowSelection.Resolve` ends today at `Pick(Current, WorkflowLevels.Current)` (`Driver/Workflows/Domain/WorkflowSelection.cs:228`, and the remark at `:84-85`). It reads `config.DefaultWorkflow` first, at a new level `WorkflowLevels.Home = "home"` (`Driver/Knowledge/Domain/WorkflowFacts.cs:13-40`). Current is reached only where the home names no default. That is the transitional path of §4.7.

**The copies.** `WorkflowDefaults.Copy(shipped, opinion, look)` gives:
- ids `merge-after-accept`, `merge-after-accept-opinion`, `-look` and `-opinion-look`, numbered `-2` where a person's own workflow holds the id;
- a graph from `WorkflowPresets.WithGraphGates`;
- `from` stamped as DEFAULTCONFIG1a(3) keeps it (`docs/2026-11-01-fabric2-plan.md:79`).

A copy is saved once per home and shared. Editing it appends a version with the reach consent SETUPUX1s already asks; *Save as a new workflow* splits it. When a build ships a changed default, nothing moves by itself: the place page says *Daoris ships a newer Merge workflow · Update…*, a plan of one that appends it.

**When *Set up…* covers a workspace.** Where the set-up covers every member of a workspace and all get one workflow, the plan writes the workspace's default once and no repository choices. Otherwise it writes one choice per repository.

### 1.2 What *Set up…* writes instead of `SET_LANDING` and *Current*

- **`PlacePlans.Apply`** (`Driver/Setup/PlacePlan.cs:182-213`) drops two things:
  - the reset to Current (`:190-203`);
  - `Landing(...)` and its `SET_LANDING` (`:205`, `:229-263`).

  In their place a `Workflow(...)` part makes two changes: `WORKFLOW_SAVE` (copy absent) and `WORKFLOW_USE`. Both go through the same edit the narrow door makes (`WorkflowSelection.Apply`, `:256`). Setting up and choosing are then one operation reached from two pages, not two ways.
- **The ticks.** *A second opinion before it lands* and *Your look before it lands* choose the copy's gates variant. They no longer write `SET_OPINION none` or `SET_REVIEW Required` (`:265-314`). The refusals `no-reviewers` and `no-review-environment` stay, since a node still chooses among what is declared. Under *Keep how it lands* the ticks are refused, with the sentence "to add a step to the workflow it keeps, use *Edit steps…*", because editing a saved workflow has one door.
- **`PlaceApply`** gains a receipt operation `WORKFLOW_SAVE`, written before the one `driver.json` change. It is idempotent: a newest version with the planned digest already there reads as done. This amends the setup UX design §4's row "Apply … saves no workflow" (`docs/2026-11-01-setup-ux-design.md:583`) and D221's rejection of choosing through `PLACE_APPLY` (`docs/decisions/D221.md:19-21`). The receipt still serves a multi-effect set-up; the choice inside it is `WORKFLOW_USE`'s edit, reviewed in the plan.
- **`SetupPresets`.** `RuleOf` (`Driver/Workflows/Domain/SetupPresets.cs:121-141`), `Said(LandingRule)` (`:182-196`) and `Pattern` (`:60`, which moves to `LandNodes.DefaultPattern`) are deleted. The class summary saying presets are "applied to Current through the landing rule, never stored" (`:43-48`) is reworded. `Match` and `LandOf` read the graph's `land` directly, not steps (`:86-119`).
- **`PLACES`' `lands`** is read from the followed version: its `from` and `WorkflowDefaults.Differs`. The Current branch of DEFAULTCONFIG1a(4) goes with Current (§4.7).

### 1.3 The `delivery` blocks, derived from their graphs

Today each file restates its graph's landing in the old rule's words (`Driver/Defaults/workflows/merge-after-accept.json:6-10`), and `WorkflowDefaults.Read` trusts the restatement (`Driver/Workflows/WorkflowDefaults.cs:118-120`). After this row:
- **`Read` derives the `SetupLand`** from the graph:
  - `method` from `land.method`;
  - `accept` is `you` where a `review` precedes `land` (`LandNodes.Reviewed`), else `automatic`;
  - `plugin` from `land.plugin`, or `none` for a branch;
  - `request` from the `integrate` node's `request`.
- **A file keeps only its words**, `delivery: { name, for }`, and only where it is the delivery's owner, meaning the first default by `order` whose derived land is that shape. `documents.json` and `release.json` carry none: their deliveries are `merge-automatic` and `merge-after-accept`, today restated by hand (`documents.json:6-7`).
- **Twins and tests.** The catalogue keys `workflow.shipped.delivery.<id>.*` stand, and the twin row is updated (`.claude/knowledge/twins.md`). `WorkflowGraphPresetsTests` fails any file whose derived land matches no owner or two.

### 1.4 What becomes of the landing rules

| `landings`/`workspaceLandings` field (`Driver/Settings/LandingKey.cs:48-70`) | Job | One home after this design | On an existing home |
|---|---|---|---|
| `form` | How work lands | The version's `land.method` | Converted (§4) |
| `autoAccept` | A press or none | Whether a `review` precedes `land` | Converted |
| `pattern` | The branch's name | `land.pattern`; open is `LandNodes.DefaultPattern` (`feature/{quest}-{slug}`) | Converted where it differs from the default |
| `plugin` | Who opens the pull request | `land.plugin`; open is the installed plugin serving the remote's platform (`SetupPresets.PluginFor`, `SetupPresets.cs:148-154`) | Converted where it differs |
| `key` | The ticket key's shape | A new `land` option `key`, judged by `LandingKey`; open is `LandingRules.DefaultKey` | Converted |
| `squash` | One commit | `land.squash` (already an option) | Converted |
| `tidy` | Remove the tree after landing | Retired (§2.6) | Stops being read; removed at the apply |
| `cycle` | What follows the landing | The version's graph after `land` | Folded in (§4.2) |
| `integrate` (policy keys) | The line's policy | `linePolicies`/`workspaceLinePolicies`, edited by `SET_LINE` (the *Line* row) | Moved |
| `integrate.request`/`conflicts` | This unit's process | The `integrate` node's options (already so under a version, `LandingKey.cs:72-77`) | Converted |

What this settles:
- **Not a saved workflow's options.** The rule is not read as a saved workflow's open options. LANDNODE1h's bind-time resolution stays one resolution, frozen at the bind, but its source becomes derived facts: the default pattern, the remote platform's plugin, the default key. The rule (`Driver/Workflows/WorkflowRunBindings.cs:135-148`; `Driver/Workflows/Nodes/LandNodes.cs:46-56`) is no longer that source. A written option stands. A workflow chosen for a whole workspace still serves each repository, since the platform's plugin is resolved per repository; a repository that wants another pattern takes its own version.
- **Amendments.** This amends D157 point 4: `pattern` and `plugin` become a step's options and `tidy` is no step's, while review environments, reviewers, `verify` and `minutes` stay declared. It also amends the land node design §4's reading 2 and §5's "not taken now": the policy moves now because the rule retires.
- **Not kept for older builds** (§1.5). The rules are read only by Current's transitional path and by the conversion's census. Their writers stop in row 1a:
  - `SET_LANDING`, refused `retired` naming *Workflow › Landing* and *Line* (`Modules/Bridge/Routes/LinesRoutes.cs:200-215`);
  - `daoris driver landing`;
  - Ask Daoris's `landing` key (`Driver/Help/Proposals/HelpSettingProposals.cs:384-453`);
  - the plan's `SET_LANDING`;
  - `workflow new --from current:<repository>`, refused naming `workflow convert`, since converting has one door.

### 1.5 An older build reading the home

- **Unknown keys survive.** An older writer keeps keys it does not know (`JsonKeeping.Kept`, `Driver/Settings/DriverConfig.cs:1042`), so `defaultWorkflow` and `linePolicies` survive its saves. No older reader reads them (`.claude/knowledge/compatible-builds.md`: a mark binds only the builds that read it).
- **A fresh home under an older build.** Row 6 falls to Current from no rules, which is the default merge with the person's Accept. That is the same delivery, landed by that build's doors.
- **A *Set up…* place under an older build.** Its `workflows` choice names the copy:
  - builds since LANDNODE1m land it through the version's delivery (`docs/decisions/D215.md:670`);
  - builds before LANDNODE1a hold `land` as an unknown kind (land node design §4);
  - an open `plugin` resolves over a rule that is no longer written, so the run is held as "lands by a plugin, and neither it nor the rule names one" (`WorkflowRunBindings.cs:139-147`).

  Each of these holds; none lands wrongly.
- **A converted home under an older build.** The rules are gone and the policy has moved, so an older build would draw Current as the default merge and refuse integration (`NoIntegrate`). Hence `InstallRollback.Refusal` (`Driver/Workplace/InstallRollback.cs:20-25`) gains a second refusal, code `workflows`: while an applied conversion stands, a staged build below this one is refused, as it is today while a delivery is unresolved.
- **No writer stamp is raised.** No new desktop-store table is added. The review word's new `repair` field is read past by older builds (the `NodeRecord.Pending` precedent, `docs/decisions/D190.md:334-336`): the word is lost there, never misapplied.
- **`compatible-builds.md` gains one line:** a `driver.json` key is kept by older writers and read by no older reader, so only the rollback refusal guards it.

## 2. The runtime performs `land` (land node design §3, §7, §8 row 10)

### 2.1 The performer

- **Today.** `land` is begun as an observed node and read from the landing record (`Driver/Workflows/Nodes/NodeRuntime.cs:976-993`, `Driver/Workflows/Nodes/BuiltInNodes.cs:79-80`). The doors land it.
- **The start.** For a run bound to a version holding `land`, the pass instead **starts** `land` once its incoming wires hold, its subject is due and the gate holds nothing, modelled on `IntegrateAsync` (`NodeRuntime.cs:1044`). `LandAsync` builds the bound intent that LANDNODE1m's `IWorkplaceProcess.Land` already writes, from:
  - the binding's frozen options;
  - the run's branch at the tip the pass read;
  - the authorization: the review's Accept on the run's record (person, revision, digest, options, prerequisites' receipts), or, with no review, the standing authority, judged again before the first effect as today (`Driver/Workplace/CurrentDeliveries.cs:27-60`).
- **The run.** It hands the intent to `ILandingDoor` over `SessionTrees` (land node design §7), run off the pass on the heavy executor as `check` is. The door runs under GITLOCK and LANDRACE1, through `CurrentDeliveries.StartAsync` and `TreeLands.LandAsync` (`Driver/Workplace/TreeLands.cs:414`), from the intent alone; no live rule is read.
- **Reach, failure and recovery.** `land` is reached by observing its method's recorded effects (§3 of the land node design) and failed by the landing's refusal. *Try again* is `WORKFLOW_NODE`'s. `HANDOFF` stays the person's resend for a plugin that declares no key. A restart settles by LANDNODE1b3's ladder before any new start.
- **A carried Accept** (LANDBACK1) becomes the review word's `repair: <carry-on turn>`. Where the subject's tip moved only by that continuation's commits, the next pass binds the review again at the new tip, once. The `CarriedAccepts` file is no longer written for a run.

**A terminal with no loop** (land node design §9, decided): it keeps the word under the verb's lease (`VerbLeases`) and says "accepted; Daoris lands it at its next pass". It never performs. Hosting the performer in a verb would make a second host of the plugin wire, the reconciler and the line's run.

### 2.2 The presses

| Door | Today | After row 1c |
|---|---|---|
| `LAND_SESSION_TREE` (*Accept…*), `Modules/Bridge/Routes/TreesRoutes.cs:495-500` | Under a run, keeps the word, waits for the pass, then lands in place (LANDNODE1m). Under Current it lands in place. | Writes the review's Accept on the run's record (a Person route). It asks a whole pass and waits within `GraphPasses.Bound` (`Driver/Workflows/Domain/GraphGate.cs:238`) for the delivery to start. It answers from `LANDING`'s `authorized`/`running`/`completed`: *landed*, *accepted; landing*, or the refusal. It builds no `LandingPlugins` and calls no `TreeLands`. |
| `LAND_WITHOUT_LOOK` | The same press past the look (CHATFLOW1d2) | Retired. The page asks for the skip's words, sends `WORKFLOW_NODE {act: skip}` on the look, then the Accept. Deleted with the page's caller in row 1h. |
| `LAND_ENTRY` (`Modules/Bridge/Routes/EntriesRoutes.cs:37`) | An entry's Accept lands through its operation | Writes the head's review word carrying the operation. The performer delivers the head's unit, and the members are brought into it (LANDNODE1b3, `D215.md:643-646`). |
| `TOGETHER_PRESS` (`Modules/Bridge/Routes/TogetherRoutes.cs:39`) | Brings sessions together, then lands them | Bringing together stays its own job; its Accept is the head's review word, as above |
| `trees land` | Lands, through Daoris or alone | Through the running Daoris it is the press above; alone, the word only (§2.1) |
| `LANDING` | The preview | Unchanged: the one preview |

`bridge-presses.md` row 8's count of the landings the routes build falls for a run's press, and `PluginRecordWiringTests` follows.

### 2.3 `AutoLander` and `AutoLanding`

- **No due entry for a `land` run.** The conclusion writes none for a run bound to a `land` version (`Driver/Workplace/AutoLander.cs:790`), and `ChooseAsync` skips such a run. Its choosing by `rule.AutoAccept` (`:118-137`) is reached only by Current units, steps runs and `handed` runs: the transitional path of §4.7, deleted in row z with `AutoLandings` and `AutoConcluded`.
- **`AutoConcluded.Merges`** (`Driver/Workplace/AutoLanding.cs:22-26`, `:55-58`) and the merge exclusion in `Landing.Effective` (`Driver/Workplace/Landing.cs:903-912`) describe that same transitional composition correctly, and go with it in row z.
- **The stale words go now:**
  - the CLI's "`--auto-accept` on a branch" (`src/Daoris.Cli/src/cli/driver.ts:19-44`), with the `landing` verb;
  - `LandingKey`'s note that "the next look lands it" (`LandingKey.cs:30-36`);
  - the room's landing door.

### 2.4 The access setup's `landing: rule` (D184; `Driver/Workplace/Landing.cs:903-912`): retired

It is not made a graph default. A default for whether work lands with no press, set outside the workflow, would be a second door for choosing a workflow. That is against D145's note of 2026-11-01 ("no rule outside the workflow…", `docs/decisions/D145.md:231`) and against D228.
- **Writes.** `SET_ACCESS` refuses `landing` with: "how work lands is its workflow's now: a workflow with no review lands with no press. Choose one on its Workflow". `full` no longer reaches landing, which the access page says. Its *Landing* row goes.
- **Graph runs never read it.** The composition stays only inside `WorkflowProcess.Current`, for Current's transitional units.
- **Conversion.** A place where the composition turned the switch on converts to a version with no review, as it behaved. Its plan row offers *Keep a review instead* (§4.2).

### 2.5 The tidy (land node design §9, decided): neither a node nor a `land` option; retired

There are two removers today:
- the door's tidy under `rule.Tidy` (`Driver/Workplace/TreeLands.cs:953-967`);
- the look's clean-up (`Driver/Workplace/BranchTidying.cs`, `AutoTidies.cs`). It already removes, behind the press's own guards, every session branch the line holds and every landing branch the press would clear, whatever `tidy` says (`BranchTidying.cs:5-27`).

The door's tidy retires. The clean-up is the one remover. `tidy` stops being read and is removed at the conversion. Why:
- no node removes anything (§E9; `BuiltInNodes.cs:70-72`);
- a failed removal must never fail or hold `land`;
- whether a tree may go is this machine's call (a live session in it), not a workflow's that serves many repositories;
- the clean-up's guards are the stricter ones.

The cost: a landed tree goes at the next pass (at most five minutes, `BranchTidying.DefaultPace`) rather than at once.

### 2.6 The gates read the graph without `GateSteps` (amends D190 point 4): decided yes

`GateSteps` (`Driver/Workflows/Domain/WorkflowNamed.cs:496-554`) turns a graph back into steps. Its landing half reads a bound `land` back as a `LandingRule` (`Driver/Workflows/Domain/WorkflowGate.cs:162-200` via `LandNodes.AsLanding`). That half is the second path by which a graph's landing is decided outside the runtime. Once the runtime performs `land`, no door needs a process-level landing rule.

- **Row 1c deletes the landing half for `land`.** `WorkflowNamed.cs:507` and `:514`, `GraphGate`'s door part for a `land` run, and `IntegrateChoice.Of`'s use (`Driver/Workflows/IntegrateChoice.cs:33`), which reads the `integrate` node and `linePolicies`.
- **A new `GraphGates.Of(graph, declared)`** reads the `opinion`, `look` and `claimed` options straight from the node options, with the same "among what is declared" refusals (`WorkflowGate.cs:202-219`). A bad option still holds naming the node.
- **`WorkflowProcesses.Graphed`** (`Driver/Workflows/WorkflowProcesses.cs:119-148`) calls it.
- **The `handed` half** stays transitional for runs bound to `handed` versions (§4.7).

D190 point 4's "`WorkflowProcesses` stays the binding and gate interface" stands. So does "read … from its gate nodes' options". What goes:
- "as the step of each kind reads its fields" (the CUTOVER1d note, `D190.md:374-378`);
- the door's graph part for a `land` run (`D190.md:379-385`). The runtime's own evaluation is that judgement.

### 2.7 Chats: CHATFLOW1f taken up (row 1d)

A chat in a tree of its own binds only to Current or a steps version. Its gate reads Current where the place follows a graph, and its words say "its work is landed as before" (`Driver/Workflows/ChatRuns.cs:13-25`, `docs/2026-10-24-chat-flow-design.md:382-386`). Once D227 point 1 makes graphs the default, every such chat would land the old way. Row 1d therefore builds the chat-flow design's §4.5: a chat run binds a graph version, and its `land` is the runtime's. `chat-graph-later` goes.

## 3. New steps, `handed` and `accept` saves refused (D227 point 2)

One judge sits at the one save point: `WorkflowNamed.ReadVersion(..., onSave: true)` and `NodeGraphs.Read(onSave)` (`WorkflowNamed.cs:251-281`, `:794-836`). Every door refuses alike, and stored versions read unchanged.

| Door | Becomes |
|---|---|
| Step presets `pull-request-after-accept`, `pull-request-no-press` (`Driver/Workflows/Domain/WorkflowPresets.cs:34-40`) | Deleted, with `SourceAsync`'s preset branch (`Driver/Workflows/WorkflowOperations.cs:497-502`) |
| `workflow new --from <id>@<v>` where that version is steps (`:504-513`) | Refused `steps-refused` |
| `workflow edit` (`Driver/Workflows/DriverWorkflowsCommand.cs:796-845`) | Refused, naming the editor and `apply <id> <graph-file>` |
| `workflow apply <file>` holding steps; `import <file>` holding steps; `WORKFLOW_SAVE` with `steps` | Refused `steps-refused` |
| `workflow apply --as-graph` | Folded into `workflow convert`; refused naming it |
| Ask Daoris's steps door (`Driver/Help/Proposals/HelpWorkflowProposals.cs:116-129`) | `steps` refused. A development pipeline's `graph` is now proposed: the person's part has been read since CUTOVER1e (`D190.md:282-290`), so the "not read yet" refusal is stale. |
| The editor offering `handed`, `pushed`, `pull-request` alone (`Web/workflow/edit/StepPicker.tsx:19`) | `PaletteKind` gains `offered`: `alone`, `tail:<method>` or `retired`. `handed` is retired; `pushed`, `pull-request` and `integrate` are tails a method brings (`setLandMethod`). The picker reads the field; the page keeps no list of its own. |

Words. Codes are for the page to key on in en and zh, checked by `names:check --strict`:
- **`steps-refused`:**
  - en: "`<id>` is not saved: a workflow is saved as a graph of steps now, and a list of steps (schema 1) is no longer saved. Compose it in the workflow editor, or save a graph with `daoris driver workflow apply <id> <file>`. A workflow saved as steps before is converted by the conversion plan: `daoris driver workflow convert --plan`."
  - zh: "`<id>` 未保存：工作流现在以步骤图保存，不再保存步骤列表（架构 1）。请在工作流编辑器中编排，或用 `daoris driver workflow apply <id> <file>` 保存一张图；以前以步骤保存的工作流由转换计划转换：`daoris driver workflow convert --plan`。"
- **`handed-refused`** (a new `GraphRefusal`): "node `<n>` is a `handed`, which only watches a landing some door made, and is no longer saved: a `land` step performs the landing. Choose its method (merge, a branch, a push, a pull request, or integrated into the line) and the steps after it come with it."
- **`accept`** (exists, `LandNodes.cs:85`, reworded): "node `<n>`: a `land` takes no `accept`. Where you accept the work, put *Your review* before it; with no review it lands once the steps before it are done."
- **`land-tail`** (exists, reworded): "node `<n>` is a `<kind>`, which comes with a `land` whose method is `<method>`: choose that method on the landing and it brings this step."

**Bound runs** keep their engine (D190 point 1, D157 point 6):
- a run bound to a steps version stays on D157's engine;
- a run bound to a `handed` version keeps the door path.

Its view says: "This run follows `<id>` v`<n>`, saved as steps. It runs as it began and lands the old way; new work follows the converted version once you convert." A new start whose resolved newest version is steps still binds it until the person converts (D227 point 3), and its bind line says so. From row z on, a start on an unconverted shape holds with the conversion as its door (§4.7).

## 4. The conversion plan (D227 point 3)

### 4.1 What it reads: `WorkflowLegacy.Census`

A pure read in `Driver/Workflows/Legacy/`. It writes nothing. It finds:
1. Each saved workflow whose newest version is steps.
2. Each whose newest holds `handed`.
3. Each whose newest holds a `land` with `accept` (held and never landing, `Driver/Workflows/Nodes/LandNodes.cs:85`).
4. Each place resolving to Current at any level or kind: a repository, a workspace, the home's missing default, an explicit `current`.
5. The rule-only shapes on those places: `landings` fields (§1.4), `access.landing`, `tidy`, `cycle`, `integrate`, the opinion rule's `None` and the review rule's `Required` as *whether*.
6. `workCycles` and `workspaceWorkCycles` (`Driver/Workflows/Nodes/NodeGraphs.cs:1162-1175`).
7. The runs bound to steps, `handed` or Current that have not ended, counted with their end.
8. Leftover rules: a rule no place follows any more.

It answers `Ended` when items 1 to 8 are all empty.

### 4.2 What it plans

| Found | Operation |
|---|---|
| A steps newest | Append v`n+1` to the same id. The decoder reads by shape (GRAPH1j, `WorkflowGraphs.Saved`), then the lift below. Every choice naming the id follows it for new work. |
| A `handed` newest | Append v`n+1`, lifted: `form: merge` becomes `land merge`; `form: branch` with a plugin and a `pull-request` tail becomes `land pull-request` with the tail kept; a branch with `integrate` after becomes `land integrate`; else `land branch, plugin: none`. `accept: you` (or an open press over a non-automatic rule) adds a `review` wired from what led in. `pattern` is kept, and `key` is written where the pattern says `{ticket}`. Node ids are kept, so cycle wires and drawings resolve. |
| `handed` leaving `form` or `accept` to the rule, chosen by places whose rules differ | One version per distinct resolution: the id keeps the first group, and `<id>-<place>` takes each other group, its places re-pointed. Said. |
| `land` with `accept` | Append v`n+1` with no `accept`. `you` adds a `review` where none precedes `land`. `automatic` defaults to **with your review** (it never landed, so the default lowers nothing), with the row's choice *With no review, as it was saved*, said as lowering. |
| A place on Current | Its graph is drawn by the one handler (`WorkflowOperations.CurrentSource`, `:516-523`; `GraphOfCurrent` gains `key` and `squash`). Options are written only where they differ from the derived defaults, the rule's `cycle` is folded after `land` with wires from `handed` re-aimed at the land's id, and integrate's request and conflicts go to the node. The graph is then deduplicated by digest: a match to a shipped copy (with gates) chooses that copy, saved where absent; otherwise a new `<place>-workflow`. A workspace's rule becomes the workspace's choice. A repository whose graph equals its workspace's gets no choice. A repository given its own choice copies its workspace's kind mappings into it, since a repository's choice replaces its workspace's whole (D157 point 10). |
| A place whose access setup composed the switch | Converted with no review, as it landed, with the row's choice *Keep a review* |
| The home with no `defaultWorkflow` | `defaultWorkflow` set to the `merge-after-accept` copy |
| `integrate` policy keys | Moved to `linePolicies` |
| Converted rules, `access.landing`, `tidy` | Removed in the one `driver.json` change and kept in the receipt for *Undo*. A rule whose place has an open Current unit is left as a leftover (item 8) until it ends, then offered for removal by a later plan. |
| A place with a `workCycles` entry | Not converted: kept on Current, said ("its quests close by its work cycle, which a saved workflow does not run yet: FABRIC-CUTOVER1h"). Its end is row j. |
| A version the save refuses (e.g. `observed-twice`) | Kept and said with the save's refusal and the editor's door. Never forced. |

Lowering is said per place by `Involvement.Lowers` and `InvolvementRoutes`, as *Set up…*'s review says it.

### 4.3 How it is shown

- **A *What needs you* row** (`workflow-convert`, *needs attention*, one per home while the census holds anything convertible): "`n` places follow old workflow shapes · Review the conversion…".
- **The place's head.** Each affected place's head reads *Follows its old landing rule · Convert…* and its Workflow label replaces "Current, read from the rules as they stand". Its *Edit steps…* and its *Current* tab in *Change…* open the conversion; there is no per-place save-as-a-workflow.
- **The review page** reuses *Set up…*'s review molecules (`PlaceReview`, SETUPUX1k), in this order:
  1. what changes, by shape, each operation once with its places;
  2. the row choices;
  3. what lowers your part;
  4. what stays as it is: bound runs counted with *they end on the old engine*, leftovers, work cycles;
  5. what it never touches (§4.6);
  6. the terminal fold;
  7. one *Apply*.

  At 1546 px the first five read in two columns; at 680 px, in one.
- **Ask Daoris** opens the page and never proposes the apply: one door. Its room names the page and `workflow convert --plan`.

### 4.4 Applied

- **The routes.** `CONVERT_PLAN` writes nothing and answers the operations and a digest. The digest covers the census inputs (the `driver.json` keys read, each workflow file's newest digest, the shipped digests), the operations and the row choices. `CONVERT_APPLY {digest, choices}` is the person's press (census *Person*), confirmed in the window. The terminal's `daoris-driver workflow convert --apply <digest>` goes through the running Daoris (RPC1e).
- **The order.** Apply plans again and refuses `moved` where the digest differs, with the driver's sentence and the plan read again. Then:
  1. a receipt modelled on `PlaceReceipt` (`pending`, `uncertain`, `done`) is written before the first effect;
  2. the workflow saves are made, each idempotent by version digest;
  3. one `DriverContext.Change` writes the choices, `defaultWorkflow`, `linePolicies` and the removals;
  4. the `workflows` rollback refusal stands (§1.5).
- **A restart** settles from the receipt's postconditions.

### 4.5 Undone

*Undo the conversion* (`CONVERT_UNDO`, a plan of one with its own digest) is offered while all of these hold:
- the receipt is the home's latest and done;
- the `driver.json` keys it wrote still read as it wrote them;
- the build is before row z.

It restores the removed rules, the access entries and the old choices in one change. The versions it saved stay saved, unchosen, and said. Runs bound since keep their versions. The rollback refusal lifts once no applied conversion stands. Anything moved since is refused `moved`, naming it.

### 4.6 What it never touches

- Bound runs and their versions, and any version except a newest.
- The delivery store, the line's queue and its candidates, the quests, the role catalogue and staffing, the plugins and their credentials, permission rules, the acceptances (`integrationGates`, landed gates).
- The repositories' own files (`daoris.gates.json`), and the landed route (`landed`).
- Any place already on a saved graph, and `workCycles`.

### 4.7 Transitional paths and their ends (D228)

| Kept transitionally | Reached by | Ends when | Deleted by |
|---|---|---|---|
| D157's engine for steps runs (`WorkflowProcesses.cs:84-106`, `WorkflowProcess.Of`) | Runs bound to steps versions; new starts on an unconverted steps workflow | The census has no open steps run and no steps newest | Row z |
| `GateSteps`' `handed` half; the door landing of `handed` runs | Runs bound to `handed` versions | No open `handed` run and no `handed` newest | Row z |
| Current as a followed workflow (`WorkflowCurrent.Derive` as a process; `NodeGraphHomes.Landing`/`OfLanding`, `NodeGraphs.cs:1084-1148`; `LandingRules.Choose`/`Effective`, `Landing.cs:887-912`; the rules' reader; `SET_*`'s *whether* halves) | Places and units on Current on an unconverted home | No place resolves to Current, no Current-bound unit is open, no leftover rule | Row z |
| `AutoLander`, `AutoLandings`, `AutoConcluded`, the `CarriedAccepts` file, the presses' in-place landing, `TreeLands`' tidy, the access composition | The three rows above | The three rows above | Row z |
| The conversion's pure readers (census, translator), in `Workflows/Legacy/` | A home updated across row z | The owner says no older home remains | ONEWAY1 |
| `workCycles` | Places on Current with a work cycle | Row j built and the person's conversion | Row j |
| The stored `roles` mode read as `setup` (`Web/agents/modes.ts:30-33`) | Browser storage | One release after 1y | Row z |
| The staffing write-back to adapter keys; `SET_ACTOR` | Older builds; the old Roles mode | The `workflows` rollback refusal (no older build opens a converted home) and 1y | FOUNDATION1 row v / ONEWAY1 (this design hands them the condition) |

The census's `Ended` is shown on This machine's page, the place being *Workflows: nothing old here*. A dated note records it on the owner's install before row z merges. A build at row z that opens a home whose census has not ended holds each old-shape place's new starts, with the conversion as their door. It never migrates.

## 5. The doors that point the old way

- **Get Started** (`Web/help/setup.ts:62-92`):
  - *helper* points at This machine's Roles (Ask Daoris and Intake as roles, `daoris driver role …`) instead of the adapter keys;
  - *driven* and *landing* are replaced by one *Set up…* step on the place page (`daoris-driver place <repo> --plan`);
  - *rules* stays.
- **One adoption door: `PLACE_APPLY`'s adoption part.**
  - `SetupPress.ApplyAsync`/`PublishAsync` (`Driver/Setup/SetupPress.cs:160-208`) become its one publisher.
  - `SETUP_PLAN`/`SETUP_PRESS` (`Modules/Bridge/Routes/SetupRoutes.cs:26`, `:37`) and the page's `SetUpForAgents` (`Web/projects/setup/SetUpForAgents.tsx`, `Web/projects/ProjectPage.tsx:11`, Ask Daoris's card through `Web/help/ProposalCard.tsx:4`) are deleted together. The card seeds `PLACE_PLAN`'s review, as SETUPUX1k opens a seed.
  - `daoris-driver setup <repo>` is removed; its usage names `daoris-driver place <repo> --preset keep --adoption [--what-it-builds …]`.
  - The workspace plan (`Driver/Setup/WorkspaceSetup.cs`, `WorkspaceSetupCommand.cs`) keeps its pacing. An apply whose adoption covers more repositories than `atOnce` writes the plan, and the tick (`Driver/Loop/Driver.cs:1127`, `:1877-1890`) publishes it. `setup --workspace` becomes `place --workspace <ws> --adoption --at-once <n> --pilot <n>`, and `--pause`/`--resume`/`--stop` steer it.
  - An existing plan file has the same shape and is worked by the same publisher, so nothing converts.
- **Ask Daoris:**
  - the `landing` door retires, its key refused naming `workflow_propose` (a graph) or `setup_propose`;
  - the line's policy goes through `setting_propose`'s `line`;
  - the steps door, as §3.
- **The terminal:**
  - the `landing` verb goes from `DriverChoicesCommand.Verbs` (`Driver/Settings/DriverChoicesCommand.cs:166-170`) and from the CLI help (`src/Daoris.Cli/src/cli/driver.ts:19-40`);
  - `line` gains the policy flags;
  - `work-cycle`'s help says "for a place on Current; a saved workflow's quests close by their agent's done until FABRIC-CUTOVER1h", and its writes are refused for a place on a saved workflow;
  - `workflow convert` is added.
- **The place's Roles** (`Web/agents/setup/PlaceRoles.tsx:84`) and `RoleEditor`'s staffing (`Web/agents/roles/RoleEditor.tsx:14`) send `SET_ROLE` alone. The row checks that `RoleChange` carries a role's choices, and adds them if not. `SET_ACTOR` itself is ONEWAY1's.

## 6. The lit wave (D227 point 4)

### 6.1 SETUPUX1x: the sitting on the owner's install (the steward, with the owner)

It runs after the update that carries waves 1 and 2. That update is staged after REVIEW2's blocking fixes. SETUPUX1i (the tree) must be built first. It shares a sitting with FLOWCHART2h. It must show, in dated shots in en and zh, both themes, at 1546 and 680 px, with run records:
1. **The design's seven flows** (`docs/2026-11-01-setup-ux-design.md:524-552`) on mostly undriven workspaces, with a repaired refusal, a restart mid-review, a moved plan, an interrupted *Apply*, and a release run of each outcome (row x, `:621`).
2. **The conversion on the owner's home:**
   - the *What needs you* row;
   - the plan listing each old shape found;
   - a row choice;
   - *Apply*, a restart mid-apply, and *Undo* then apply again;
   - the census afterwards (leftovers, work cycles, bound runs).
3. **A fresh workspace** following the home default with nothing written, and *Set up…* writing `WORKFLOW_SAVE`/`WORKFLOW_USE`.
4. **Landings performed by the runtime:**
   - a merge after Accept (*accepted; landing*, then landed);
   - a merge with no review on a development repository;
   - a pull request through a real plugin (WORKSETUP1's first Azure DevOps pull request, if its sign-in is ready);
   - a restart mid-landing;
   - a chat in a tree of its own.
5. **Every old door** opening its new home while the flag is set on (the door census).

What it finds is filed and fixed before 1y.

### 6.2 SETUPUX1y: lit, and the old removed

The flag is deleted rather than defaulted on: a flag that keeps the old surface reachable is a second way (D228). Removed:
- `setupLit` and its readers: `Web/agents/modes.ts:39`, `Web/agents/AgentsView.tsx:166`, `Web/app/App.tsx:681`, `Web/projects/ProjectsView.tsx:143-146`, `Web/projects/workspace/WorkspaceView.tsx:114`, `:170`;
- `placeOpening`'s `lit` and its legacy branch (`Web/app/opener.ts:210-218`);
- the Roles mode's dark branch (`Web/agents/AgentsView.tsx:321-340`), `AgentsModes.tsx`, `RoleSections.tsx`, `StructureSections.tsx`'s lists, and `RolesView.tsx`'s mode part;
- the repository's and the workspace's Workflow and Setup tabs (`Web/projects/setup/WorkspaceSetup.tsx`; `RepositorySetup.tsx` as a tab, its folds kept as the place page mounts them, SETUPUX1o);
- `Web/workflow/WorkflowTab.tsx` and `Web/workflow/WorkflowStartNode.tsx`.

Kept by rename:
- `LandedLines` (`WorkflowStartNode.tsx:25`) moves to `Web/workflow/place/LandedLines.tsx`;
- `useWorkflowTab` (`Web/projects/useWorkflowTab.ts:20`; users `PlaceSessions.tsx`, `ProjectsView.tsx`) becomes `usePlaceWorkflow`.

Proof: an opener test per old door, the door census, `HelpCoverageTests`, and the installed window again at 1546 and 680 px in en and zh.

## 7. What stays, and why

| Path | Kept? | Why it is not a second way, and its end | Who decides |
|---|---|---|---|
| `workCycles` (a quest's or an ask's whole graph) | Only for a place on Current that has one | Today the only home of a quest's close: a run's take names nothing under a version (`D190.md:233-235`). A converted place's work cycle governs nothing, so §4.2 keeps such a place on Current. Ends with row j. | The owner, since CUTOVER1h is parked (`docs/backlog-parked.md:212`). Recommended as row j if the dry run finds any. |
| A rule's `cycle` | No: folded into the version | None | None |
| FABRIC-CUTOVER1h | Row j, conditional | As above | The owner |
| CHATFLOW1f | No: taken up as row 1d | Without it, D227 point 1 creates the second way (§2.7) | The steward moves it back under D227 and D228; the owner is told |
| INTEGRATE1 row k | Stays the owner's sitting | This repository's own integration by `tools/merge-branch.mjs` is the development's process, ONEWAY1's. Under this design its flip writes `linePolicies` and chooses the `integrated` copy. | The owner |
| The look's clean-up (AUTOTIDY) | Kept | The one remover once the door's tidy retires (§2.5) | None |
| Opinion and review rules as declarations | Kept | The one home of who reads and where a look runs. Their *whether* halves go with Current. | None |
| The decoder | Kept read-only | Draws an old version and feeds the conversion; runs nothing | None |
| `LANDING`; *Keep how it lands*; *Set up…* beside *Change…* | Kept | One preview; an ask that writes nothing; one edit reached from two pages | None |
| D190 point 7 (a look the work's own choice adds, outside the graph) | Handed to ONEWAY1 | A second way to put a look before a landing. Its condition ("revisited once a reach survives an edit") was met by RUN1f. | The owner, through ONEWAY1 |
| The landed route's `current` ("no verification") | Kept | Not a workflow followed; a reword for ONEWAY1 | None |
| The plain `/bind` (FABRIC-CLOSEBIND1) | No: deleted in row z | Dead | None |

## 8. Each record: converted on the press, or simply no longer written (D228 point 3)

- **Converted:**
  - steps newests, `handed` newests, `land`+`accept` newests;
  - places on Current, with their `form`, `autoAccept`, `pattern`, `plugin`, `key`, `squash` and `cycle`;
  - `access.landing`;
  - the opinion's `None` and the review's `Required` as *whether*;
  - `integrate`, moved;
  - the home default, written.
- **No longer read, and removed by the conversion:** `tidy`.
- **No longer written for graph runs, and ending with their units:** the `AutoLandings` due entries and the `CarriedAccepts` file.
- **Never converted:**
  - bound runs, and versions other than a newest;
  - `workCycles`, until row j;
  - existing workspace plan files (one publisher, same shape).
- **No longer read at 1y:** the browser's `daoris.agents.setup`.

## 9. Rows in build order

Waves 1 and 2 ship as the next update to the owner's install. Wave 1 changes the foundation and is built and merged as one wave.

WORKSETUP1's plan over its 29 repositories should apply on wave 2's *Set up…*. Applied earlier, it writes rules that then wait for the conversion: one more press, for the owner to choose.

| Row (lanes; after) | Contract | Proof | Install |
|---|---|---|---|
| **Wave 1** | | | |
| **1a** a place follows a saved graph (driver, modules) | §1: `defaultWorkflow` and level `home`; the home seed; `WorkflowDefaults.Copy` and *Update…*; open options from derived defaults; `land.key`; derived `delivery`; `PlacePlans.Apply`, `PlaceApply` and `SetupPresets` per §1.2; `PLACES` `lands` from the version; `linePolicies` and `SET_LINE`'s policy; `IntegrateCarry` and admission reading the node and the policy; the old writers retired (§1.4); `InstallRollback` `workflows` | A fresh home: an imported repository's first start binds `merge-after-accept` v1 at `home` and lands by 1c. *Set up…* for each delivery end to end: the plan shows `WORKFLOW_SAVE`/`USE`, Apply, a start binds the copy, it lands by its method (the example plugin's pull request; integration requested). `WorkflowSelectionTests` row 6; `WorkflowRunBindingTests` (open options never from a rule); `PlacePlanTests`, `PlaceApplyTests`, `SetupPresetsTests`, `WorkflowGraphPresetsTests`, `ShippedDeliveryWordsTests`; an older build's `DriverConfig` save keeps the new keys. | Yes |
| **1b** saves refused (driver, modules; after a) | §3: the judge at `onSave`; presets and doors refused with their codes; Ask Daoris's steps door; `PaletteKind.offered` | A test per door's refusal; a stored steps file and a `handed` version read and bind as before; the palette fixture | Yes |
| **1c** the runtime performs `land` (driver, modules; after a) | §2.1–§2.6: the performer and `ILandingDoor`; the presses per §2.2; no due entry for a `land` run; the `repair` word; `GraphGates.Of`; the land half of `GateSteps` and `GraphGate`'s door part deleted; `SET_ACCESS` refusing `landing`; no tidy on the performer's path; a terminal alone keeping the word | `GraphRunLandingTests` through the performer (merge after Accept, past the bound; merge with no review, with a route double that fails if a route calls `TreeLands`; branch; push; pull request with the key; integrate); a restart at each phase; a press and a pass at once land once; a dirty tree's carried Accept; the alone word landed by the next pass; a role's write of the review word refused; a downgrade refused with a delivery open; `PluginRecordWiringTests`' count | Yes |
| **1d** a chat's run binds its graph (CHATFLOW1f; driver; after c) | The chat-flow design §4.5; `chat-graph-later` gone | That design's row-f proofs; a chat in a tree on a saved graph landed by the runtime | Yes |
| **1e** the conversion (driver, modules; after a, b, c) | §4: census, translator, grouping, `CONVERT_PLAN`/`APPLY`/`UNDO`, receipt, `workflow convert`, the attention row's driver half | One fixture home holding each old shape: a steps workflow with an open bound run; a `handed` version open to two different rules; `land` with `accept` `you` and `automatic`; a branch rule with `pattern`, `plugin`, `key`, `squash`, `tidy` and `cycle`; a workspace rule with `integrate`; `landing: rule` over a non-automatic branch; an automatic merge; a work cycle; an open Current unit. Apply: each outcome; a restart mid-apply; `moved`; *Undo*; the plan again showing only leftovers, the work cycle and the bound runs. A start after it binds the converted version and lands by 1c. The bound run lands the old way. | Yes |
| **1f** one adoption door (driver, modules; after a) | §5: `SetupPress` folded into `PLACE_APPLY`; `place --adoption`; the paced plan written by the apply; `setup` verbs removed | `PlaceApplyTests`' adoption; `WorkspaceSetupLoopTests` over an applied plan; the `setup` usage | Yes |
| **Wave 2** | | | |
| **1g** the conversion on the page (web; e) | §4.3: the row, the place head's line, the review, the row choices, *Undo*, the terminal fold | Page tests; stories at 80, 39 and 25 rem, en and zh, both themes, keyboard | Yes |
| **1h** the page on saved graphs (web, modules; a, b, c, f) | *Set up…*'s changes and ticks; *Change…* without *Current* except as the conversion's door; *Edit steps…* on Current opening the conversion; `LandOptions` under a version only; the picker by `offered`; *Update…*; Get Started; `SetUpForAgents`, `SETUP_*` and `LAND_WITHOUT_LOOK`'s callers and routes deleted; the *Line* row's policy; the Roles' `SET_ROLE` | Page tests; the door census; stories at 80, 39 and 25 rem, en and zh | Yes |
| **1i** the words (help, cli, web locales; a–h) | Ask Daoris's rooms; the terminal's usage and help; glossary rows (*conversion*, *home default*, *old landing rule*); every refusal's zh | `names:check --strict`; parity; `HelpCoverageTests`; usage fixtures | Yes |
| **After the update** | | | |
| **SETUPUX1i** the tree | As filed | As filed | Yes |
| **SETUPUX1x** the sitting (the steward with the owner) | §6.1 | §6.1's shots and records | Is the install |
| **Fixes** from x | As found | As found | Yes |
| **SETUPUX1y** lit and removed (web; x and its fixes) | §6.2 | §6.2 | Yes |
| **1j** a run graph's close (CUTOVER1h; driver; only if the census finds a work cycle) | The parked row's contract, then the conversion folds a work cycle into the version | The parked row's proofs; a work-cycle place converted | Yes |
| **1z** the transitional paths deleted (driver, modules, service; the census ended on the install) | §4.7's deletions; the plain `/bind`; a home not ended held with the conversion as its door | The build with that code gone; its tests removed and their replacements green; an old-shape fixture opened by z holds and offers the conversion | Yes |

## 10. Notes to write

- **D227** carries a note for each row.
- **D190:**
  - point 1's "never by a migration" becomes "never without the person's press";
  - point 4 as amended in §2.6.
- **D157:**
  - point 4 (§1.4);
  - point 6's "Current is read live" becomes transitional;
  - point 7's "never a migration" is amended;
  - point 15's "every repository follows Current" gives way to the home default.
- **D221** (§1.2), and **D184**'s AUTONOMY1c2 (§2.4).
- **D145**'s note of 2026-11-01, now kept: no rule outside the workflow.
- **D215:** point 4's "by the person's Accept or the rule's `autoAccept`" becomes "by its `land` node's start".
- **The land node design:** §4 (nothing is migrated; reading 2), §5, and §9's tidy and terminal answers.
- **The fabric2 plan's DEFAULTCONFIG1a(4)**, and **the chat-flow design §4.3**.
- **`.claude/knowledge/compatible-builds.md`**: the line in §1.5.

## 11. Measure first

- Arming and tip reads with many bound runs, now that every start is a run (land node design §9).
- How long the census and the plan take on the owner's home and on the 29-repository workspace.
- Whether the owner's home holds any work cycle. This decides row j.
- The landed tree's wait under the clean-up's pace, against the door's immediate tidy it replaces.

## 12. Accepted, and settled (the steward, 2026-11-02)

Accepted under D227 and D228 as written, with an Opus read beside wave 1's first rows (Codex's owed while its spend cap holds);
the read's findings are settled here before wave 1 merges. Settled now:

1. **WORKSETUP1 applies on wave 2.** The work workspace's set-up over its 29 repositories waits for waves 1 and 2 and applies on
   the new *Set up…*, so it is written once in the new shape and never converted.
2. **Waves 1 and 2 are the next update the owner's install takes** after REVIEW2's page and checks fixes; DOGFOOD1's rows go
   through that install.
3. **Row j** is taken only if the census on the owner's home finds a work cycle; the measurement in §11 decides it before wave 1 ends.
