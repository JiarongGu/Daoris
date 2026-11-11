# Workflows, review and landing — how it works now

## What it does

A workflow says how a repository's work moves from an agent's done to the line. It is a saved graph of steps of fixed kinds:
holds, a second opinion, the person's look, the person's Accept (`review`), one landing (`land`), and what comes after it (a
pushed branch, a pull request, integration, a release). Each place (this machine, a workspace, a repository) follows one saved
workflow; a fresh home follows a shipped copy of *Merge after you accept*. A chain's work in one repository is a run, bound at
its first start to one version. The node runtime in the driver loop evaluates the run's graph, and it performs the `land` itself
once the gates hold. A merge goes into the line; a branch is made for the person; a plugin pushes and opens a pull request; or
the line's integration takes it. Work no saved workflow governs is held, and nothing lands it. The release's migration converts
the old records (steps versions, `handed`, `land` with `accept`, the places on the landing rules) to saved graphs once.

## Its parts

Paths are under `src/Daoris.Desktop/Daoris.Desktop.Driver/` unless they name another root.

| Unit | File | Its job |
|---|---|---|
| Shipped defaults | `Defaults/workflows/*.json`, `Workflows/WorkflowDefaults.cs`, `Workflows/HomeSeed.cs` | The shipped graphs read once and judged, `plan-review-apply` (a system's plan reviewed, then applied) and `in-copy` (a folder's copy reviewed, then brought back) among them; the copy a home saves; a fresh home seeded with `merge-after-accept` |
| Delivery presets | `Workflows/Domain/SetupPresets.cs` | *Set up…*'s deliveries, the suggestion, the remote platform's plugin, *Keep how it lands* |
| Graph presets | `Workflows/Domain/WorkflowPresets.cs` | The shipped graphs for `workflow new --from`, with the opinion and look variants |
| Saved workflows | `Workflows/WorkflowStore.cs`, `Workflows/Domain/WorkflowNamed.cs` | One file per workflow; versions appended whole; the one save judge |
| Node kit | `Workflows/Kinds/` (`NodeKit`, `NodeKindAttribute`, `INodeKind`, `BuiltInNode`, `PluginStep`; one folder per kind, its declaration `<Kind>/<Kind>Node.cs`; `PullRequest/PullRequestStep.cs`) | Each kind's one declaration, marked `[NodeKind(id, group, after…)]`: its contract row, the subject a run binds it to, its options, its own declaration checks, its part on the run view and the step a version's person part reads it as, its offer and role, its evidence schema, its terminal title and its look. `NodeKit` is built once from the marks, in the picker's groups and their order (`docs/index/registered.md`); `NodeKit.Of` reads a built-in's unit, or a plugin's step as `PluginStep` with Daoris's half where its step name has one (`*/pull-request`); `Without(kind)` is an older build's kit. `NodeKitTests` holds the ids and the units equal both ways. A unit says its state logic beside its declaration: `IObserves` (read from the pass's reads, `NodeReading`, and begun when its wires fire), `IStarts` (started on its door in the pass, `NodeStarting`, an active run's node kept on it after the rounds, `NodeKept`, and the attempts a lapse left unreconciled settled before each round, `SettleAsync`; `IJournalled` where its node is started on a port through the performer's attempt journal, as `check` is, rather than on a door once per node and tip: the performer asks the unit, sends the attempt to the port its door is, and gives the node its kind's perform), `IPressed` (the person's acts of its own and the move each asks of the one table, so the node door names none); the runtime asks the unit and names no kind (`NodeDispatchCensusTests`). `NodeDoors` holds each starting kind's door, built from the loop's `NodeDoorWiring` by its unit; a test swaps one with `NodeDoors.With(kind, door)`. A door kind's work leaves the pass only through the kit's door runner (`NodeDoorRuns`: once per start key, not while the drain says no, the pass's flow suppressed), under the run's own step its start carries (`StepAuthority`, made by `NodeKept.Authority`); the step is set only there, by `IntegrateStart` around each attempt's answer, by `WorkPerformer` for a `check` and a plugin step, and by the `resolve`'s door for its hand-off's work in the tree it reserves (`TreeHandOffs.AuthorityFor`) (`StepAuthorityCensusTests`). `NodeKit.Problems` is the save's kind rules: the protected order the gate kinds share (`ProtectedOrder`, reading each kind's `Guard`: `opinion` and `review` before every landing, `look` also passed on every way to it), then each kind's own `Problems` (where its node stands: a `land` once and an `integrate` after it, the `integrate` once and a tail of a `land` of its method, a `quest.close` the end with only its failure's path leaving it), then, for a graph a person saves now, `LandingRules` (a `land` with its method chosen; a kind that reads the landing with no `land` on some way to it); a kind that says its refusal of a declaration's field in its own words and code gives `DeclarationProblem` (`land`'s `accept`), and one never an end says why in `NeverEndBecause` (`stop`). Every kind is whole in its folder: `Integrate/` (`IntegrateNode`, `NodeIntegrating` the start through the integration door and the card waits, `IntegrateStart`, `IIntegrationDoor`), `Request/` (`RequestNode`, `NodeRequesting`, `RequestStart`, `RequestOptions`) and `Apply/` (`ApplyNode`, `ApplyDoor`, `ApplyStart`, `IApplyDoor`: a system place's apply, begun in the pass and kept on its door once per node and attempt with the review record it is fed (`EffectRecord`), which hands the plan that record names, by its session, number and digest (`PlanReview.Named`), never one written since, to the system's adapter at `apply/perform`, one apply at a time (work that kept no plan, a plan that does not read, one no longer kept or not the one read reach it with *nothing applied* and why on its words; an empty plan reaches it *changed nothing*; a stale plan's receipt of what it applied is kept beside the plan, `applied-<n>.json`, and its way back tells the work the applied resources), a system declaring its precondition `none` read before and after each write (`SystemReadBack`), and writes what it came to on the node: applied reaches it by the plugin's source, `unchanged` reaches it saying the plan had nothing to change and nothing was written, `stale`, `changed-while-applying` (a read-back that differs from its write) and `apply-refused` fail it with their words on its status, the first two naming each such resource's present value and version; two plans read against one system at once apply one at a time, the second of a moved resource refused as stale); `wait.unit`, `all` and `stop` are declarations alone, the join and the sink being the shared wire evaluation's |
| Kinds and graph rules | `Workflows/Nodes/NodeContract.cs`, `Workflows/Nodes/NodeGraphs.cs` | The kinds' ids (`NodeKinds`), the states, the authority row's shape and the checks every declaration is held to; `Problems()` refuses a graph that breaks a rule every graph is held to (schema, bounds, entries, end, reach, the data wires, the cleanup) and names no kind: the kinds' rules are the kit's |
| Palette | `Workflows/WorkflowPalette.cs` | What the editor offers, read from the kit's units in its order: alone, as a landing's tail, or retired; each built-in's ports and the outcomes that carry them, none that arrives |
| Arriving declarations | `Workflows/Kinds/Resolve/ResolveNode.cs`, `Workflows/Kinds/Merge/MergeNode.cs`, `Workflows/Kinds/NodeArrivals.cs`, `Workflows/Kinds/ArrivingKinds.cs` | `resolve` and `merge` declared arriving with their ports, options, outcomes and waits; the ports and outcomes the cut-over wires on kinds already offered (`integrate`'s `ref` and `conflicted`, `push`'s `ref`, `line`'s and `check`'s `commit`, `tidy`'s `work`, `commit`, `tidied` and `partly`) each kind's `Arrives`; a kind's `Waits` are the codes a run's step says under it (`WorkflowRunStep.Wait`, `WorkflowRunWait`) |
| Choosing | `Workflows/Domain/WorkflowSelection.cs` | Which workflow a chain's work follows, and the landed route |
| Run binding | `Workflows/Domain/WorkflowRunBinding.cs`, `Workflows/WorkflowRunBindings.cs`, `Workflows/WorkflowRunStart.cs` | A run bound at its first start, kept in `workflows/runs/<run>.json` |
| Process per gate | `Workflows/WorkflowProcesses.cs` | The binding and gate interface: a graph version read from its gate nodes; no binding, no workflow |
| Node runtime | `Workflows/Nodes/NodeRuntime.cs`, `WorkPerformer.cs`, `WorkObserver.cs`, `WireEvaluation.cs`, `NodeRecordFiles.cs` | One pass at a time: evaluate each governed unit, keep its moves, start what it proposes |
| `NodeGoverning` | `Workflows/Nodes/NodeGoverning.cs` | Which units a graph governs and how: a closure graph's quest, a run bound to a graph version on its own record, a unit held for the person; the quests bound to a closure graph the choices no longer hold. `NodeRuntime` keeps one-line doors |
| `NodeEvaluations` | `Workflows/Nodes/NodeEvaluations.cs`, `PassReads.cs` | A governed unit's graph judged on its record: a unit's, a run's over its subjects, and the pass's gate, content and git reads it waits on, each chosen by what the node's unit declares it waits on (`IAsksReads`) and the landing found through the unit that makes it (`IMakesLanding`), so it names no kind (a census holds it); it clocks each start (`WireRun.Clock`, kept as the status's `since`) |
| `NodeTelling` | `Workflows/Nodes/NodeTelling.cs` | The awaited set the plugins are told: each unit's frame, the tips last told, the next timer the records persist |
| `NodeRunKeeping` | `Workflows/Nodes/NodeRunKeeping.cs` | What a run bound to a graph version has kept around its rounds: the person's skips and stops, what a review sent back told to the work (a failure that carries its own words on its status, as a stale `apply` does, told in them), each active node of a starting kind handed to its door, the run's end and reopening |
| Integrate and request | `Workflows/Kinds/Integrate/`, `Workflows/Kinds/Request/` | A unit's `integrate` (`NodeIntegrating`: started through the integration door, its card waits, `work.integrated`) and a run's `request` (`NodeRequesting`: published through the request port) |
| `NodeOwing` | `Workflows/Nodes/NodeOwing.cs` | Which units a pass judges: those owing a start or a rebinding, and the turn they are taken in past 32 units a pass: the run a door asked for, a held event's unit and a record the pass's answers moved first, then a rotation of the units owing a start or a rebinding, then of the unchanged ones, which keep a quarter of the turn |
| `PassAsks`, `PassTaken` | `Loop/` | A door's ask for a whole pass, naming the run it waits on: the pass that takes it judges that run first (`NodeRuntimeLook.Asked`) and answers the press as soon as the run's judgement is kept (`Judged`), else at its end; a run asked for while a pass runs is the next pass's |
| Performing | `Workflows/Nodes/WorkPerformer.cs`, `WorkKeeping.cs`, `WorkHearing.cs`, `WorkReceipting.cs`, `WorkOutcomes.cs`, `WorkPersonWord.cs`, `NodeRecordEdits.cs` | The performer sends, reconciles and retries; keeping an evaluation's moves and a no-dispatch start, hearing a completion, taking its receipts, applying an outcome, and the person's word on a held attempt are each their own type. A unit's resource record keeps 32 receipts, the oldest settled ones going past it, and each one dropped leaves its key (`plugin`, `schema`, `resource`) in the record's guard of at most 256 keys, the oldest forgotten first (`ResourceRecord.Dropped`, `GuardKept`), so a replayed completion makes no new owing receipt for a resource that was given |
| Evaluating | `Workflows/Nodes/WireEvaluation.cs`, `WireReceipts.cs`, `WireLapsing.cs`, `WireEligible.cs` | The evaluation's pass and firing keys; the receipts it judges on, why a bound node lapses, one node's eligibility; the wire records in files of their own |
| Graph reading | `Workflows/Nodes/NodeGraphs.cs`, `NodeGraphStarts.cs`, `NodeGraphPlaces.cs`, `NodeGraphReading.cs` | The save's rules; which nodes can start before a gate; the digest and each node's place; a graph's JSON read into its parts |
| Quest close | `Workflows/Kinds/QuestClose/` | `quest.close` whole in its folder, started once per node and tip on the close door the runtime is made with: its declaration (`QuestCloseNode`); a graph closing a quest, the start and its fence, what a closure graph's unit owes and the pruning of closure graphs nothing needs (`NodeClosing`); the close's sending and answering (`QuestCloser`); what it judges on (`QuestClosing`, `QuestCloseJudged`); the closure graphs kept by digest (`ClosureGraphs`); the last quest read (`QuestReads`); the bind at the take (`ClosureBinding`, `ClosureBound`) |
| Run derivation | `Workflows/Domain/WorkflowRuns.cs`, `WorkflowChatSteps.cs`, `WorkflowReviewSteps.cs`, `WorkflowLandingSteps.cs` | A run derived from its records: the work and the pass in the derivation, the chat, review and landing steps each their own type; the run's records in files of their own |
| Named workflow reading | `Workflows/Domain/WorkflowNamed.cs`, `NamedStepReading.cs`, `NamedStepCanon.cs`, `Workflows/WorkflowOperations.cs`, `WorkflowUsing.cs` | A saved file and its ids and plan to add a version; a version's steps read, and their canonical text and digest; the typed operations, with choosing a workflow for a place apart |
| `daoris workflow` | `Workflows/DriverWorkflowsCommand.cs`, `WorkflowTerminal.cs`, `WorkflowListing.cs`, `WorkflowChoosing.cs`, `WorkflowWriting.cs` | The usage and the door; the terminal's flags, say and press; the reading, choosing and writing verbs |
| Holds | `Workflows/Kinds/QuestHolds/`, `Workflows/Kinds/ContentHolds/`, `Workflows/Kinds/HoldReading.cs`, `Workflows/Kinds/WorkRequests/`, `Workplace/ContentHolds.cs` | `quest.holds`, `content.holds` and `work.requests` before the landing, each whole in its folder: its declaration and save rules (`<Kind>Node`, the hold's once, before the landing and after the claim in `INodeKind.Problems` on `GraphStanding`), its reading (`QuestHoldsReading`, `ContentHoldsReading`, `WorkRequestsReading`; what the two holds share is `HoldReading`; a done checked against nothing `UncheckedDones`) and the git read a hold takes (`HoldContent`, `HoldReads`); `RequestHolds` is the runs held at a declined request |
| Isolate kind | `Workflows/Kinds/Isolate/IsolateNode.cs`, `IsolateSteps.cs`; `WorkOrders/Domain/IsolateStep.cs` | The `isolate` kind (group Work): the graph's one entry, wired by `after` straight into a `claimed`, performed by the start before the record opens. Its options are `kind` (`auto`, `worktree` for a git repository, `copy` for a folder; a kind its place has none of holds the start), `at-once` (`2` to `24`, or `any`) and `overlap` (`go`, `wait`); a graph without one runs one session at a time in the place's checkout. The driving area's rules 12 to 14 say what the planner does with it |
| Return kind | `Workflows/Kinds/Return/` (`ReturnNode`, `ReturnStart`, `ReturnDoor`); `Departments/Return/FolderReturn.cs`; web `workflow/return/ReturnChoice.tsx` | `return` (*Bring back*, group Others, a folder's alone; a graph with one and no `isolate` is refused, `return-isolate`): performed through its door once per node, attempt and choices made, after the review accepts the work. The door brings back the copy its review record names (`EffectRecord`, `FolderCopies.Named`), takes the folder's start lock, waits `place-busy` while a turn runs there or in the copy, fails `moved` where the run's newest turn in the copy is not the one the record read, and brings the copy back file by file against the copy's base, both sides hashed afresh (`FolderReturn`): a file changed in the copy alone written into the folder, one changed in the folder alone left, one changed alike nothing, and one changed differently on both sides held for the person (a supervisor never answers it: the kind offers no landing acts), the node kept active on `both-changed` with the paths, the versions of each it showed and the count already brought back on its status (`ReturnHolds`) and its wait (`paths`, `refused`, `returned`), the copy kept, the files only the copy changed already written; a file either side could not read waits the same way, a copy's only settled by *Keep yours*; a *Keep both* whose beside-write did not hold waits on the chosen path. A path it cannot bring back without guessing is refused with what was found, read on the disk since a manifest skips links and lists files alone (`ReturnRefusal`): `escapes` (outside the folder), `link-copy` and `link-folder` (a link or junction at the path or on its way, never followed nor read as a removal), `type-copy` (the session turned a file into a folder or back), `type-folder` (the folder holds a folder where the file goes, or a file on its way) and `collision` (names one file to the disk, as a rename that changes only case, or a *Keep both* name the folder already holds); each waits on the same `both-changed`, a line of the wait's `refused` (the path, a tab, the code), offered *Keep yours* alone and said in the reader's words. The person's *Keep yours*, *Take the session's* and *Keep both* (written beside as `<name> (<copy>)<ext>`) go per path through `WORKFLOW_NODE` (`keep-yours`, `take-session`, `keep-both`, the path as the words), each kept on the record as a mark of the attempt as a request's send is, bound to the versions of the path its wait showed (`ReturnNode.ChoiceKey`); a choice whose path no longer holds them lapses and the path waits again on its new versions, the others standing; once every path has one, the node is reached and the copy removed (the review reads a turn's copy by its folder's name, `FolderCopies.IdAt`, so the removal moves no revision). Every write is conditioned on the folder's file still holding the version the return read (`ReturnWrite`): the session's bytes staged beside it, the folder's file renamed aside and hashed under a handle no one else holds, and the staged file renamed into the free name, never over one; a file written meanwhile, or held open by another program, is put back and waits as changed on both sides. Each file's decision and effect are journalled around its write beside the run's record (`ReturnJournal`, `workflows/units/<key>.return/<copy>/`, `ReturnDoor.JournalOf`), with what a write replaced or a removal took kept there under `kept/` (what an earlier write of the same path kept moves to `earlier/<n>/`), and a return cut off at any boundary (`ReturnSteps`) is taken up from the journal before either side is read. A run whose session worked in the folder itself reaches it with nothing to bring back |
| Observed kinds, shared | `Workflows/Kinds/NodeObserving.cs`, `NodeObservation.cs`, `NodeGitReads.cs`; `Kinds/Claimed/`, `Kinds/Line/`, `Kinds/Look/`, `Kinds/GoAhead/`, `Kinds/Opinion/`, `Kinds/Land/LandReading.cs` | `NodeObserving` dispatches a node to its kind's reading and builds an observation (`Seen`, `Reached`, `NotBound`); `claimed`, `line`, `look`, `go-ahead` and `opinion` each read in their folder (`ClaimedReading`, `LineEndReading`, `LookReading`, `GoAheadReading`, `OpinionGateReading`), a `land`'s by `LandReading`, a `review`'s by `ReviewReading`; the look's skipped words are `LookReading.SkippedWords` |
| Person's task | `Workflows/Kinds/Human/HumanNode.cs`, `HumanTasks.cs`; modules `Loop/HumanTaskRows.cs`; web `workflow/task/TaskPress.tsx`, `work/attention/taskRead.ts` | A `human` node, a manual operation a run waits on: begun on a run (on a unit it waits ready), reached by the person's *Done*, or for an approval *Approve*, with a note they may give, or rejected by their *Reject*, which takes their reason as a review's changes do, kept on the run's record (`done`, `approve`, `reject` at the node door, `WORKFLOW_NODE`, `daoris workflow done\|approve\|reject <run> <node> ["…"]`); a press on a node not waiting for it is refused (`not-waiting`), on another kind `not-task`. Its outcome is written in the file, a task's `done` or an approval's `approved` (or none: a task reached with no outcome), and a `when` wire routes on it; an approval's refusal is no outcome but the node rejected, routed on the graph's `rejected` wire as a review's changes are. The editor adds a task and the inspector's *What it waits for* turns it into an approval (`humanDraft.ts`); the note stays with the reach. *What needs you* lists each waiting task as `task:<run>/<node>` with its presses and since when it waits (the node's `since`, `HumanTask.Since`), read from the runtime's last look, so a press kept clears it; the run view offers the same presses under the step |
| Steps waiting on the person | `WorkOrders/Sessions/PersonWaits.cs` (the `PersonWait` record and its words, below Workflows), `Workflows/Nodes/PersonWaitReader.cs` (`IWorkOrdersWorkflows.PersonWaits` hands the terminal's look); `WorkOrders/Sessions/RunWaitSessions.cs` (the run by `QuestRuns`), `RunWaitWords.cs`; `Transitional/Migration/AbandonedRunsStep.cs`; web `work/rail/runWaitFact.ts` | Every step of a run not ended that only the person moves (`PersonWaitReader`, read from the bound runs and each run's record now, never stored): an active `review` (`accept`), an active `human` (`approval` or `task`), an active `go-ahead`, and an attempt held `unknown` (`held`); a step their word is kept on waits on no one. Sessions' groups place the run's newest session here by them (its chain's quests in its repository), To review where each waits for an Accept and Waiting on you otherwise, whatever the session's own ending or archive mark, its row carrying `waits` (`run`, `node`, `why`, `since`) on `SESSION_GROUPS`, the groups' push and `session list --json`; *What needs you* offers a review row for each so placed, and the row, the list's row and the band's row open the run's view; `daoris session list` says each step and `daoris workflow run --session <id>`. The window reads them through its runtime's bound runs, the terminal's own look from the files. The home migration's `abandoned-runs` step stops each live step of a run whose quest is declined or whose ask is closed through the run's node door, with words saying which, so the first pass applies and says it; a step already holding a stop is left |
| Land and review | `Workflows/Kinds/Land/`, `Workflows/Kinds/Review/` | `land` and `review` each whole in their folder. The land's declaration and its options' checks (`LandNode`); its options, their resolution at the bind, and `Landing`, the intent a run lands by (`LandNodes.LandingOf`; a process with no bound version declares `Landing.Merge`); its reading, which a review's Accept shares (`LandReading`); the attempts a lapse left unreconciled, settled by their deliveries before anything new starts on the unit (`LandAttempts`, `LandAttemptSettling`); and its door, started once per node and tip (`LandingDoor`, `ILandingDoor`; `LandStart`, its run's record, is in `Workplace/`). The review is the person's Accept and their changes (`ReviewNode`), its subject the run's review record (`ReviewWork`, read for the pass as the kit's step reads; `ReviewRecords`, kept after the rounds by `NodeRunKeeping.KeepReviews`; the readers per kind `FolderReview`, `CheckoutReview`, `PlanReview`; the turn log `Departments/Manifest/RunTurns.cs`); a review reached because its landing is seen made, with no press, is the landing record's (`by: git`), never the person's. The Accept bound to its record and checked as kept and as applied (`ReviewAccepts`), the unchecked done passed with it (`UncheckedAccepts`), the changes' part (`ChangesParts`), a branch's changes answered at the same commit (`ChangesAnswered`), the review's Stop (`ReviewStops`) and the carried Accept's limit (`CarriedRepairs`) are each a unit beside it |
| Pull-request step | `Workflows/Kinds/PullRequest/PullRequestStep.cs` (`PluginOf`, `IsAddress`, `Keep`), `Workplace/PullRequestRecords.cs`, `NodeRunKeeping.KeepPullRequests`, `Workflows/Nodes/NothingToDeliver.cs` | A `<plugin>/pull-request` step's plugin, the bind's refusal where no plugin performs it, and the address its evidence gives written on the landing; the steps an empty landing or one the line holds passes over |
| Push, monitor and tidy | `Workflows/Kinds/Push/`, `Workflows/Kinds/Monitor/`, `Workflows/Kinds/Tidy/`, `Workflows/Kinds/PullRequest/PullRequestReading.cs`, `Workflows/Kinds/IWatchable.cs`, `Knowledge/Domain/ChainTidied.cs` | Each kind whole in its folder: its declaration and save rules (`<Kind>Node`), its reading (`PushReading`, `MonitorReading`, `TidyReading`) and its door (`PushDoor`, `TidyDoor`); the state a pull-request step reports, which a monitor watches; the tidy's record, which the keeper and the press read |
| Rebase (arriving) | `Workflows/Kinds/Rebase/`, `Workplace/ReplaySettles.cs`, `Workplace/TogetherBinding.cs` | `rebase`, declared arriving (not offered, refused by a save) with its words: fed `work`, it replays the work onto its `onto` (the line by default, or a branch by name, its tip read through `ITargets`) in a tree of Daoris's own (`RebaseReplay`), leaving out what the target holds by ancestry, by patch, by the squash the target's landing record names and by an operation that brought the work in as its member (`TogetherBinding.BroughtIn`), and is `ready` with its `commit` (onto the target's tip, on the attempt's step ref), `conflicted` with its `problem` (the stop's commit, patch id and files) or `uncommitted` (the paths with their blobs); the person's checkout, the work's branch and its tree are never written. Its start stays ready, said, while a session runs a turn or waits in its tree (the door's `Turning`, the look's in-use trees) or an operation holds the work (`RebaseWaits` over `TogetherBinding.Of`: a member of an open operation, a head before its operation is `ready` at its tip). `TogetherBinding.Including` is the one read of a member's operation, which `DeliveryReach` reads too. Its door (`RebaseDoor`) writes each attempt on `workflows/rebases/<run>.json`; the pass hands its reading (`RebaseReading`) that record and its targets' tips (`IReadsSteps`, `NodeStepReads`), and the run's settled end lets the record go beside its step refs. The together press refuses a member delivering (its `rebase` reached at its tip, a step its commit feeds unsettled, or an unsettled delivery) through `TogetherPress.Delivering`, given from above by `RebaseDelivering`. `ReplaySettles` is the one replay that settles what the records may settle, the landing's and the rebase's |
| Resolve (arriving) | `Workflows/Kinds/Resolve/` (`ResolveNode`, `ResolveDoor`, `ResolveTree`, `ResolveRecords`, `ResolveReading`, `ResolveWords`, `IResolvers`, `TreeReserving`, `CommitAskers`), `Workplace/TreeHandOffs.cs`, `Workplace/StartIn.cs` | `resolve`, declared arriving, fed a `rebase`'s `problem` and its `work`: its door reserves the work's session tree, prepares the replay there at the stop on a detached head (the branch never moves), what the rebase's rules settle staged and only the stop's files left (`ReplaySettles.SettleStopsAsync`, partly), and asks its resolver (`IResolvers`) with each file's hunks, counted per stop by the stop's identity on `workflows/resolves/<run>.json` across attempts and restarts, `retry.maxAttempts` asks of `timeoutSeconds` each (3 of 1800 by default). At the resolver's end it reads the tree, HEAD included (a commit or `rebase --continue` of the resolver's read as its edits), refuses a file the stop did not name, goes on with each later stop settled or asked as its own try 1, and proves the stop's commit differs from what the rebase made only in the named files; resolved, the commit is kept on the rebase's attempt as its `Resolution` on that attempt's step ref, which the rebase's next attempt reads (`ready` with it where the target has not moved, else its stretch replayed onto the new tip). Every end stops the resolver first, keeps an unresolved ask's edits on `refs/daoris/steps/<run>/<node>/edits` (applied over the stop at the next ask), aborts, puts the tree on its branch at its tip and closes the row; a restart restores each open row (`ResolveDoor.RestoreOpenAsync`); past the bound it fails, said "Waits on you: …". A tree handed over is one open row of the store's `tree_hand_offs` (`TreeHandOffs`: the tree, its branch and tip, the run, node, attempt and stop, the resolver, the ask's key, the phase `reserved`, `prepared`, `asked` or `restoring`, until when), never an act of the journal, so no writer's reconcile closes it and no promotion or tree update waits on it; its preparation and restoration run under the row's step (`TreeHandOffs.AuthorityFor`). `TreeReserving` writes it under the tree's landing hold and then the repository's lock alone (`HeldBy.Reserving`), once the holders show no session in the tree. Every start names what it starts in (`StartIn`) and `TreeLock.TryStarting` reads the row inside its shared hold: a start in a reserved tree is held, said (`held.reserved`), unless it carries the row's ask key, which the asked session's going on carries (`TreeHandOffs.AskOf`). The trees' holders (`TidyHolders`, each read taking the home) name a reserved tree by its step, so every door that reads them keeps off it; the removal reads the row under the landing hold and keeps the tree, said. One door asks a tree's session to commit (`CommitAskers`): nobody while a hand-off reserves it, a run's `resolve` while its `rebase` will read the work, else the turn's end (`SessionWaits.CarriesOn`, whose look over `TurnCarries` asks nothing where another asker answers). `TogetherLocks` carries its shared hold as a value (`Hold`), as a start does |
| Landing performer | `Workplace/LandPerformer.cs`, `Workplace/LandTries.cs`, `Workplace/LandEnds.cs`, `Workplace/LandOutcomes.cs` | The performer of a run's `land`, off the pass under the tree's hold and the delivery key, its entry `LandEntry` (`Knowledge/Domain/WorkplaceFacts.cs`) and its tries' rules and notes (`LandTries`, `LandTryNotes`); a run whose work landed at its bound tip is done with its landing, so a node the pass starts again after its tree was tidied is tried no more (`LandTries.Landed`, read before the tree is); a landing of the session accepted since the node started at its tip (`LandStart.Since`, the node's `since`; none where it was started unclocked) closes it as already made, and a tree whose head is not that tip is refused, for the pass to start the node at the new one, as a conversation's landing is: the refusal is kept with the tree's tip and the start's (`AutoTry.Start`), and tried again once either differs, so a tree back at its start lands; the end of every try, landed, closed, waiting, refused or carried on, under its code (`LandEnds`), which the press that waited answers from (`LandOutcomes`) |
| Branch naming and notes | `Workplace/BranchPatterns.cs`, `Workplace/LandingNotes.cs`, `Plugins/LandingPlugins.cs` | A `land`'s pattern, ticket key, slug and chain subject; the conversation's notes of a press and a part left out; whether a named plugin performs pull-request steps (`LandingPlugins.ProblemOf`) |
| Line policy | `Settings/LinePolicyKey.cs` | `linePolicies` and `workspaceLinePolicies` read, written and judged |
| Tree landing | `Workplace/SessionTrees.cs`, `Workplace/TreeLands.cs` | The merge, the branch and the squash; the plan and its refusals, each read from the run's bound `land`. `TreeLands` is the front: `TreeLandPlans` says the plan before the press, `TreeMerges` is the merge form, `TreeBranches` the branch form and a chain's advance, `LandingPress` the landing itself, `LandingSayings` the words; a landing's records (`LandingSubject`, `LandingPlan`, `TreeLanding`, `SweepItem` and the rest) each sit in a file of their own beside them. `SessionTrees` keeps one-line doors; opening a tree is `TreeOpening`, reading where the trees stand `TreeListings`. The replay settles the declared backlog by its rows' ids (`BacklogRows`, the ids a row covers `BacklogIds`, compound lists and ranges read or refused): rows sharing an id are one row, each id standing as only one side changed it and the rest of the line taking the side that changed it, so a compound row each side archived a part of keeps the ids neither archived, its list written again; a row only one side added, removed or rewrote takes that side, one both changed alike (whitespace normalised) is once, and any other (both rewrote, one removed or archived while the other changed it, one id added twice in two texts) is left for a person with both texts said, as is a side covering an id twice; the made replay is held to the same rules for every id, where git stopped or not (`BacklogHeld`): a row they take out that git kept (the line moved it, the work archived it) leaves in a commit on top, said, and a result that lost a side's words or covers an id twice is refused, as the integration's member merge holds its merge and the merge tool its own (`backlog-held.mjs`); a heading union kept twice in a union record is kept once in a commit on top (`UnionOnce`). Declared row tables keep the merge base line by line. The merge tool's twin is `tools/merge-branch/backlog-merge.mjs`, row for row of `fixtures/backlog-merges.json` |
| Delivery record | `Workplace/DeliveryStore.cs`, `DeliveryReconciler.cs`, `CurrentDeliveries.cs` | Each landing's phases written ahead, settled after a restart; a delivery is keyed by its unit, tip, method, target and generation (`DeliveryStore.KeyOf`, `Latest`) |
| Landed branches, pull requests | `Workplace/LandedBranches.cs`, `PullRequestAsks.cs`, `PullRequestStates.cs`, `PullRequestBounds.cs`, `BranchTidying.cs` | `landings.json` (`LandedBranchJson` writes and reads it by hand), a pull request's state from its plugin, the look's tidy (`TidyHolders` the trees sessions hold, `TidyKeeping` what the look keeps beside the branches; `BranchTidying` keeps their doors and the saying) |
| Integration | `Workflows/Kinds/Integrate/IntegrateStart.cs`, `Workflows/IntegrationTransport.cs`, `Workflows/PlaceIntegrates.cs`, `Workplace/PlaceIntegrate.cs`, `Workplace/IntegrationRequests.cs`, `IntegrationStore.cs`, `IntegrationLines.cs`, `Review/Integration/` | One request door, one run per line, its gates and judgement; `PlaceIntegrate` (read from the saved workflows by `PlaceIntegrates`) the place's requester outside a run; the store's step keeping is `IntegrationStepKeeping` and its row reading `IntegrationRows` |
| Landing gate | `Review/SessionTreeGates.cs`, `Workflows/Domain/GraphGate.cs`, `GraphGates.cs`, `WorkflowGate.cs` | Whether work may land now, read once per decision and handed to every door |
| Look and opinion | `Review/Domain/ReviewGate.cs`, `ReviewRules.cs`, `OpinionGate.cs` (with `OpinionGateFacts.cs`, `OpinionGateState.cs`, `OpinionGateWords.cs`, `OpinionAskKept.cs`, `OpinionPersonWord.cs`, `LandingGate.cs`), `OpinionRules.cs`, `Review/OpinionPresses.cs` | The review environment's levels; the second opinion and the person's words at it |
| Entry Accept | `Review/EntryAccepts.cs`, `Review/EntryRunAccepts.cs`, `Workplace/EntryLands.cs`, `Workplace/RunParts.cs`, `Workplace/TogetherPress.cs` | One Accept for an entry or an ask, part by part: the word kept for each part a `land` performs, the rest held; the plan; the first part of a part's own run, which its landing is named for |
| The window's press | modules `Bridge/Routes/TreesLandDesk.cs`, `LandPresses.cs`, `Bridge/PerformedWaits.cs`, `Bridge/PerformedAnswers.cs`, `Bridge/AcceptWaits.cs`, `Bridge/AcceptAnswers.cs`, `Bridge/ChatAccepts.cs` | `LAND_SESSION_TREE` and the plan behind it, and the node door's Accept waiting as it does, carrying the opinion's answer, and pressing a conversation's work; the press run once per session under the loop's lifetime and always answered, held in words past its bound, and logged as `landing.pressed` |
| Verification check | `Workflows/Kinds/Check/` (`CheckNode`, `ICheckPort`, `CheckExecution`, `CheckResults`), `Workflows/VerifyRuns.cs`, `Workflows/VerifyCommand.cs`, `Workflows/LineBatching.cs`, `Workflows/VerifyHelds.cs`, `Workflows/VerifyFixAsks.cs` (with `VerifyBatchSessions.cs`, `VerifyFixStanding.cs`), `Review/Heavy/GateReach.cs`, `Workflows/LandedRunsLeft.cs`, `Review/Heavy/HeavyExecutor.cs` (with `VerifySend.cs`, `VerifyWorld.cs`, `VerifyAnswer.cs`, `VerifySlot.cs`, `VerifyExecution.cs`); `Workflows/LineRunReader.cs`, `Domain/WorkflowRunLine.cs`, `Domain/LineRunWords.cs`; web `workflow/verify/` (`VerifyRuns.tsx`, `lineSaid.ts`), `work/attention/heldWorded.ts` | The `check` node performed by Daoris, off the pass, under the heavy slots, whole in its folder: its declaration, perform and options (`CheckNode`), its port and the executor behind it, and its results read back; a landed run bound per batch of a line at a whole look (`LineBatching`, called from `WatchNodes` before the pass); a person's verification of a branch or a commit bound as a run of its own; a check with `reach: paths` runs the gates its batch's changed paths reach, by each gate's `paths` in the accepted declaration (`GateReach`), and names the rest as *kept*; a failed check's *Ask for a fix…* publishes one ask per gate (`VerifyFixAsks`), and a check set to `failed: ask` has the look ask it with no press (`VerifyFixStanding`); the history clear takes a landed run's files once a newer run on its line has a verdict (`LandedRunsLeft`); a held check named by what it verifies (`HeldAttempt.Verification`) and worded on *What needs you*; a line's runs read with their verdicts and batches (`workflow run --line`, `WORKFLOW_RUN {line}`) and drawn under *Verifications* on the repository's Workflow tab |
| Chat runs | `Workflows/ChatRuns.cs`, `Workflows/ChatCandidates.cs`, `Review/ChatPresses.cs` | A conversation's run bound to its graph; its subject the submitted candidate |
| Place set-up | `Setup/PlacePlan.cs`, `Setup/PlaceApply.cs`, `Workflows/PlaceProjection.cs` | *Set up…*'s plan and receipt-kept Apply; the `PLACES` read |
| Workflow migration | `Workflows/Migration/WorkflowMigration.cs`, `MigratedPlaces.cs`, `MigratedGraphs.cs`; `Transitional/Migration/WorkflowsStep.cs` | The release migration's workflow step: old versions and the places on the rules converted to saved graphs once |
| Routes and page | Modules `Bridge/Routes/WorkflowRoutes.cs`, `WorkflowEditRoutes.cs`, `SetupRoutes.cs`, `TreesRoutes.cs`, `EntriesRoutes.cs`; web `src/workflow/` (`chart/`, `edit/`, `place/`), `src/agents/setup/` | The bridge doors; the chart, the editor and the place page. The page holds no list of the built-in kinds: `NODE_KINDS` in the generated service types (`tools/contracts/PageNodeKinds.cs`, from the kit) gives each kind's picker group and order (the `Git` group holding the landing's kinds and the rebase, its resolution and the merge), its chart look, the step a run draws it as, its ports, what of it arrives and its waits; run titles are `workflow.step.<kind>`, a built-in's outcome word `workflow.outcome.<kind>.<outcome>` (an arriving one's too), a step's wait `workflow.wait.<kind>.<code>` (`StepWait`), and the names check (`scripts/kind-words.mjs`) requires them in every language. A step of the editor reads its kind's ports beside its declaration's (`edit/stepPorts.ts`); a `rebase` added brings its work from the run's entry, its `ready` way on and its resolution's tail, a `merge` its commit from the nearest producer, `merged` and `held` on as one and `behind` back to its rebase, in one change (`edit/gitTail.ts`); a way back on an outcome leaves that outcome's port on the chart |

## Its rules

**Choosing and saving**

1. A place follows a saved graph workflow. A fresh home saves a copy of `merge-after-accept` and names it `defaultWorkflow`, once.
   A home that already has a `driver.json` is never seeded; it converts on the person's press. (from D227)
2. The workflow a chain's work follows is the first of: the task's choice, the repository's for the kind, its default, the
   workspace's for the kind, its default, the home default. Where none names one, its run is bound with no workflow (`current`)
   and held. (from D157, D227, D232)
3. A repository that names any development workflow replaces its workspace's whole. No door writes `current` as a development
   choice; a file that holds it is still read. (from D157, D227)
4. The landed route (`landed`) resolves on its own, names only a verification pipeline, and keeps the blob id of the accepted
   `daoris.gates.json`. A route changed applies to the next run. (from D192)
5. Saving (`WORKFLOW_SAVE`) and choosing (`WORKFLOW_USE`, a plan of one) are two presses. *Set up…* alone saves the shipped copy
   and chooses it, in one receipt-kept Apply. A choice is refused, nothing written, where a department it reaches (the one named, or
   each of the workspace's that its own choice does not replace) cannot run one of its steps by its place's kind, the refusal naming
   the department, the step, its kind and where it runs (`place-kind`, `WorkflowPlaceKinds`, `PlaceKindUnserved`). Each kind declares
   its places (`INodeKind.Places`): `land`, `push`, `monitor`, `tidy`, `integrate`, `line`, `content.holds`, `rebase`, `resolve`,
   `merge` and Daoris's half of a plugin's `pull-request` run in a git repository alone, `isolate` in a git repository (a worktree)
   and a folder (a copy), `return` in a folder alone, `apply` in a system alone, and every other kind and any other plugin step in
   every kind of place, since no plugin declares where its step runs. The shipped *Work in place* (`in-place`) holds none of the git
   kinds, so every kind of department can follow it: the window's add chooses it for a folder and a system in the press that registers
   them, and for a repository where the person ticks it, as a set-up's plan of one and its apply (`chooseInPlace`, the bridge's set-up domain); the
   terminal's choice is `daoris place <department> --preset in-place --apply`. (from D221, D227, D264, D265)
6. A workflow is one file, `<home>/workflows/<id>.json`, on this machine. A version is whole and never edited; a save appends the
   next number. The newest 20 and every version a kept run names are kept. (from D157)
7. Only graphs are saved. One judge at the one save point refuses steps (`steps-refused`), a `land`
   with no method (`land-method`), an `accept` option (`accept`), a kind marked arriving, declared before the palette offers it, and a wire
   naming a port or an outcome its kind declares arriving (`arriving-kind`, `ArrivingKinds`, `NodeArrivals`). Only a graph version binds and runs. (from D227, D232)
8. A graph holds at most 24 authored nodes and 96 wires, and at most one `land`. The steps writing the work (`land`, `return`, `apply`:
   `IWritesWork`) come after the `review` where the graph holds one, every way to them passing it (`review-order`, a landing's own order
   being `ProtectedOrder`'s), and each is fed the record of the last review on every way to it (`EffectOrder.Fed`; two reviews feeding one
   by ways of their own are refused, `review-ambiguous`). A graph ends at any node no such step lies past (`effect-past-end`), so a graph
   that lands nothing (a folder's, a system's, or a git department's worked in place) may go on after its review to an `apply`, a `return`
   or a plugin's step; a development graph with no `land` holds a `claimed`. Its holds judge the work before its landings, or, landing
   nothing, before its reviews and the steps writing the work (`GraphStanding.Judged`). There is no `handed` kind. (from D190, D227, D232, D264, D273)
9. The opinion, the look and the `review` each come before every landing and are wired straight into it, and no landing may start
   before a look has passed. The opinion need not come before the look. These gates are the kinds that declare a guard, and their
   order is one rule the kit runs at the save beside each kind's own (`ProtectedOrder`). The holds (`quest.holds`, `content.holds`,
   `work.requests`) are judged once each, and every way to a landing passes them (`HoldReading.Problems`). `quest.holds` never reaches
   on a done checked against nothing: an ask's quest that carries no requirement waits as *unchecked*, naming the quest whose handback
   the person reads, until their pass with words or a version with its `yes` off. (from D157, D190, D247, D255)
10. A `land` method brings its tail only for `integrate`, which brings one `integrate`; `merge` and `branch` bring nothing. A `push`
    (Daoris's, through the push act) stands after a `branch` landing, and a pull request is a plugin's performed step
    (`<plugin>/pull-request`) after the `push`: steps a place composes, never a `land` method or a suggestion. (from D215, D227, D241)
    A saved workflow that cannot bind or be drawn holds on a `BindingProblem` (a code and its values, `Said` its English): the chart
    (`problemCoded` on `WorkflowEffective`) and the run page (`problemCoded` on a run, from `WorkflowProcess.Coded`) word it from the
    catalogue in each language, the driver's sentence showing only where it sent no code. (from D241)
11. A workflow file marks its pipeline: development (absent) or verification. A verification version holds at least one `check`
    and no `claimed`, `opinion`, `look`, `go-ahead`, `land`, `review`, `integrate`, `work.requests` or `quest.close`. (from D192)
12. A change that lowers the person's part applies only by the person's press; an agent's stays a proposal. Editing the home
    default's copy asks the consent of every place that follows it. (from D157, D192, D227)
13. Ask Daoris is told the draft open in the editor with the person's next words and writes its change as a graph in the room's
    `drafts/` folder; it saves a version only by pressing `daoris workflow apply <id> drafts/<id>.json --base <version>`, a press the
    person confirms on a card that shows the graph as the editor draws it and its changes. It proposes no workflow and never chooses
    or runs one. (from D223, D261)
14. The shipped defaults hold no `quest.close`: the agent's done closes the quest, and the flow's end is the work's. (from D190)

**Runs**

15. A run is a chain's work in one repository, bound at its first start on this machine and written once. Its id is the chain's
    first quest in that repository. (from D157)
16. One engine: a run bound to a graph version is the node runtime's. A run bound to anything else is held, its problem said.
    (from D190, D227, D232)
17. A graph run is the unit `run:<run>` of its own node record. `claimed` observes the chain's quests and carries the run's work
    on its `work` output; a `review` its review record (rule 22); the gates, the landing's nodes and a plugin's nodes the run's branch at its tip, the branch its tree moved
    onto where the look learned it, named alike by binding, telling and answers; a `go-ahead` the run's ask. A branch the quests of
    several graph runs produced, as when a later run's session works in the tree an earlier run's opened, is each of those runs'
    branch; one Current's work shares with a graph run's is held, said, and neither governs it (`RunOwners`). A node fed a value on a
    `data` wire (`work`, `commit`, `ref`, `problem`: `WireValues`) binds to that value (`RunSubject.Input`, `RunInputs`), so a moved
    input rebinds only what it feeds. A step with a target is bound to the target's tip it read, and its move rebinds the step only
    while nothing it feeds is active or reached (`WireTargets`). A ref the run's own step moved reads at its `from` while it stands at
    its `to` (`RunMoves`, on the run's record). A node whose input only a skipped producer feeds is skipped with it, naming it
    (`WireSkips`). A step's commits stay on `refs/daoris/steps/<run>/<node>/<attempt>` until the run ends with no attempt unsettled
    (`StepRefs`). Where a pass does not read the run's branch tip, a step after the review not bound yet binds the commit the
    reached review accepted on that branch (`ReviewedTip`), so an accepted landing begins rather than standing ready with no subject.
    A branch two runs worked before each run had its own tree is told once for both, and a plugin's event on it goes to each run
    whose own node awaits it there at the event's commit, else to each run whose graph names its step (`ToldRuns`). (from D190, D259, D260)
18. The runtime is the record's one writer. A door keeps the person's word (skip, stop, send, ask again, go on, accept, changes, done, approve, reject) on the
    record, and the next pass applies it; a run does not end while a word waits on that pass. A run ends when its graph's end settles or
    no node can move (`NodeRunKeeping.EndAsync`), or when its quest closed (done or declined) and every step still standing observes its
    branch and cannot move: the line holds each one's bound commit whole (`LineHolding.Whole`, the landing's judgement by content and
    patch, asked at the checkout through the look's `OnLine`), ended `on-line`, or no branch of the run stands, ended `branch-gone`
    (`ClosedRunEnds`); its steps stay as they stood and the end keeps why. An ended run is not governed again until its chain comes back
    here, so no look pays for it. The terminal says each of those words as
    `daoris workflow <act> <run> <node>`, every act of `NodeWordActs` a verb of the group the tree routes to the host's `WorkflowNodeCommand`,
    which presses the page's `WORKFLOW_NODE` (`WorkflowNodeWordsRouteTests`). A word names its author: the person's names none, a landing supervisor's names its role, its
    decision and the attempt it was released on (`NodeWordBy`), a node taking it as `supervisor` where the person's is `person`. A
    second word on a node is refused `worded`, but the person's replaces a supervisor's pending one, which the pass settles
    `superseded`; the pass drops a supervisor's word whose attempt has lapsed or whose decision is not live in this run (`stale`). The
    person's Accept names the review record it is given on, by attempt and revision (`WORKFLOW_NODE`'s `attempt` and `revision`, the
    terminal's `--at <revision>`, whole or its first seven characters or more), and is checked twice (`ReviewAccepts`): as it is kept, against the record of the
    attempt the review stands on, a word on another refused `moved` with the revision standing; and as the pass applies it, against the
    run's work read afresh that pass (its branch at the tip read then, its turns, its plans, its handbacks), a word whose work moved since
    dropped `moved`, said with both revisions. An Accept naming none is bound to the record standing as it is kept, which the terminal
    says with its revision; nothing is accepted past a move either way. A review's `changes` may name the part of the chain they go to,
    by its quest (`part`), kept as a mark of the rejected attempt (`ChangesParts`) and told to the session that made that part's
    handback, else to the newest part's; a branch's review rejected and answered by a new handback at the same commit is asked again on
    its new record (`ChangesAnswered`), as a folder's, a copy's, a checkout's or a system's is by the revision a handback moves.
    The review's *Stop…* (`WORKFLOW_NODE`'s `stop` with `whole`, `ReviewStops`) stops the run before its Accept, every live step as the
    person's with their words, as an abandon does; after it, *Stop what follows…* stops only the steps after the review not yet at their
    end, the Accept and what already happened left as they are. Each says what stays by the kind of work the record read (the branch and
    its tree, the checkout's commits, the folder's changes, nothing more back from a copy, nothing more of a plan applied) and promises no
    undo; a step's own stop stays the run view's. An Accept waits for that pass, within `GraphPasses.Bound` (20 seconds), saying so beside the
    button (`PassNote`, the press step `pass`); it never waits behind a tree's hold. That pass judges the pressed run first, however many
    runs the home holds, and so does a whole pass for a run whose word it applied. A press its pass left short of the commit says what the
    judgement found (`handed`'s state and the commit it is bound to), and one whose pass did not come within the bound says so. A quest bound to a closure graph is closed by that graph. (from D190)
19. A `go-ahead` holds the landing until the person's yes or their skip with words. (from D190)
20. A conversation's run binds its graph. Its subject is the candidate the person submits, numbered past every one the run has had,
    whatever branch it was on; a newer submission lapses it. A graph
    holding `quest.close` holds a conversation's press. (from D227)
21. On a place that follows a saved workflow, a session's checks are `claimed.checks` on the graph; a `place checks` write is
    refused; elsewhere they are the repository's rule. A conversation's run view names the checks its executions are held to, read
    the same way. Under `light`, a session runs only the gates not marked heavy, and the heavy ones are refused. (from D192, D227)

**Review and Accept**

22. The person's Accept is a `review` step before the steps writing the work. A graph with no `land` is work in place
    (`InPlaceProcess`), whatever carries it out after its review, and a landing's press under it is refused, saying nothing lands it. A
    flow with no `review` lands with no press, and the standing authority is judged again before the first effect; its `apply` or `return`
    reads the run's work into a record of its own at its start, kept under that step and attempt (`EffectRecord`). A run is over when no
    node of its graph can move (none ready, active, unknown or invalidated), nothing is pending and no failure is kept for the person, so a
    step after the graph's end runs to its settle; its end keeps its outcome (`RunEnd.Outcome`, `RunOutcomes`: `completed`, `failed` at the
    first failed step, `stopped` at a stopped or turned-down one with whose). (from D145, D227, D264, D273)
    Each attempt of a review reads the run's work into one review record, written once as the attempt begins, beside the run's record
    (`ReviewRecords`, `workflows/units/<key>.reviews/<node>-<attempt>.json`), and never edited: its kind (`branch`, `checkout`, `folder`,
    `copy`, `plan`, or `answer` where only the handbacks are left), the artifact's reference, every part's handback (its submission and
    requirements, *unchecked* where it carried none), the files a folder's or a copy's work changed with their hashes and marks, what could
    not be read (`unreadable`, `not-kept`, `over-bound`, `not-read`) and how far back the rows reach. A run on a branch binds its review to
    that branch at its tip; any other binds it to the run itself at the record's revision (`RunSubject.Record`, `ReviewWork`), a digest over
    the reference and the handbacks' ids, so a moved revision, a corrected answer at the same content included, lapses the binding and begins
    a new attempt and record, the older kept. No review is withheld for want of a branch, and nothing it reads reaches it but a branch's
    landing seen made or its commits on the line (`ReviewReading`): a branch held `empty` waits for the person. A folder is read from the
    run's baseline to its newest turn's end, a path a later turn's start found changed marked *changed outside its turns*; a copy against its
    base, a file the folder changed too marked (`FolderReview`); a checkout by its turns' `HEAD` intervals and its own uncommitted paths
    (`CheckoutReview`); a system by its sessions' newest plan's record, number and digest (`PlanReview`). The run's turns are kept as each
    ends under its place's `turns/<key>/` (`RunTurns`), and its first turn's start manifest as the named record `run-<key>-base` in the
    place's manifest store, kept by the store's tidy while a review record names it (`ReviewRecords.Manifests`). (from D273)
23. A press under a run whose `land` the runtime performs keeps the word, asks a pass and waits up to 20 seconds for what the try
    came to: the landing, or why nothing landed (closed, nothing to land, a gate's wait, uncommitted work, or a head moved past the tip
    judged, answered `moved` as the return and the apply answer a moved record, and tried again once the tree moves), each said as
    the try said it, and a carry-on as *accepted; its session was carried on* (`PerformedAnswers`). Only where no try ended within the
    bound does it answer *accepted; landing*. `WORKFLOW_NODE`'s Accept on a review a `land` follows waits the same way and answers its
    `landing` (`AcceptWaits`); the second opinion's token it carries (`answers`) is kept at the gate before the Accept (`AcceptAnswers`), and
    an Accept on a conversation's run is that conversation's press, its submission and then the same keep (`ChatAccepts`), answering the
    `press` code a refusal is worded by (`busy`, `elsewhere`, `moved`, `asked`). The carried Accept stands for its landing's repair alone (`CarriedRepairs`): the repair names the review
    attempt accepted, and the review begun again at a tip continuing that one is accepted again, its words naming the record and the tip
    carried to, only while its parts and their handbacks are the accepted record's; a new handback or a send-back lapses it, and the review
    asks again. A gate held only by the review the press itself just accepted, read before the pass it
    asked came, holds nothing, and the press waits for the landing all the same. A press while the run is on its way to its review (its
    graph not judged at the commit yet, or a node before the review still to settle) is kept on the run's record as the review's Accept
    (`EarlyAccepts`) and answered *accepted; it lands at the next look*, or once the step it waits on is through; `WORKFLOW_NODE`'s Accept on
    a review not begun is kept the same way, on the revision the page drew. Each pass leaves it while the review has not begun and applies it
    at the first that finds the review waiting; the work read at another revision than the one named (another commit, a new handback) drops
    it `moved`, and the person's later word on the review takes its place. Where the run waits on an unchecked done (D255), an Accept
    naming it (`unchecked`) keeps two words in one edit of the run's record (`UncheckedAccepts`): each unchecked `quest.holds` passed with
    the words *passed by your Accept on its review, which named it unchecked*, and the review's early Accept, both bound to the record drawn;
    a replay finds them kept and is refused `worded`, the work moved before the pass drops both, and an Accept naming an unchecked done where
    none waits is refused `none-unchecked`. No other hold is passed this way. (from D256, D273) The window's press runs once per session under the
    loop's lifetime, never the window's (`LandPresses`): it is answered within 60 seconds with what it came to, or held in words while
    it goes on, and a second press of a session whose press runs is held and runs nothing. Each answer is the machine log's
    `landing.pressed` (`landed`, `accepted`, `held` or `refused`, with its sentence), and a press that ends after its held answer tells
    the windows its scopes again. The person's press is also the try again. No door lands in place: the window's press,
    a chat's press and the terminal's `trees land` keep the word for the run's `land` (the terminal with no loop keeps it for the next
    pass), and work no run's `land` performs is held with the gate's refusal or why nothing lands it (`RunLandHolds`). A tree's name
    given where a tree door takes a session (the plan, the press, the discard, the review) names the session whose tree it is
    (`TreeNamedRefusals`), as the terminal's alone path does. (from D227, D228)
24. An entry's or an ask's one Accept keeps each part's word where its run's `land` is the runtime's, and lands it at the pass. A
    part no `land` node performs is held, said. Each part's word names the review record the press drew for it (`LAND_ENTRY`'s
    `revisions`, by session) and is kept as the node door keeps it; it is not atomic across runs: a run whose review stands on another
    record is refused `moved` alone, the others go on, and the answer says which landed, which accepted, which moved and which failed
    (`EntryRunAccepts`). A part's own landing is named for the first part of its run in its entry and joins
    that run's branch; each quest an ask sent to the repository is a run of its own unless its chain leads back to an earlier one
    there, so each lands on its own quest's branch (`RunParts`). `TOGETHER_PRESS` brings sessions together and lands nothing. (from D167, D227, D232, D259)
25. The look is the person's verdict in a review environment; no agent or card gives it. Under a saved graph the `look` node says
    whether one is needed; the environment is still the `reviews` rule's. `SET_REVIEW required` there is refused. (from D154, D227)
26. A second opinion is another maker's read-only reading before the landing. Its findings reach the working session once. What
    it disputes is answered by the person's *Reviewed* or *Accept…*, given after the answers or the findings went to the person and after a recheck's findings reached them: that
    time is kept beside the recheck as the first pass's is (`OpinionDeliveries.ToPerson`, `OpinionGateState.RecheckToPersonAt`), and a *Reviewed*
    given before it, or while none is kept, answers nothing. A person's word at it (*Go on anyway…* and the others `OpinionPersonSaid` names) is
    bound to the tip it covers and stands for any later ask at that tip; only a newer commit asks again. Under a saved graph the `opinion` node says whether one is
    asked, and chooses among the reviewers the place declares; with none declared it cannot start, said on its node. `SET_OPINION none` on a saved-graph place is refused.
    (from D155, D190, D227)
27. Review's diff is a dependency graph of today's git reads, kept by no later open, taking no lock and refreshing no index in a tree
    Daoris does not own. Within one open each question is asked of git once (`GitReadOnce`): a question asked again is handed the
    first answer, a tree's layout is one `rev-parse`, and a branch's commit comes from one listing of the folder's refs. Leaving the
    page stops its git. (from D202)

**Landing**

28. For a graph run the runtime is the one performer of `land`. A `land` with no review before it is made active, with no press, in
    the round that readies it at its standing tree's tip, and the pass that finds it active at its tip starts it once on the
    landing door, off the pass, under the tree's hold and the delivery key, so a press and a pass land one delivery. A `land` whose
    subject moved while it was active runs again at the new tip once every delivery at the old one reads certain by phase. A run's
    branch-side steps bind the tip of the branch its work stands on: the one its tree's agent moved onto, where the look learned it
    (TREEFOLLOW1, the session branch's `now`), else its session branch; the end keeper judges that branch's tip, so a moved tree's
    `content.holds` and `land` read the tree at its bound commit, sharing `tree land --plan`'s `LineHolds` judgement: where the local line holds every commit by content or as the same change, the hold lets go and the landing reaches it without replay, keeping the bound tip and saying the line's proof in the run's words; any commit it lacks still goes through the landing door. Its merge is the one `landing` and `line` read. The holds and the gates
    read the tree of the branch's newest executor, a session whose record names that tree, as the runtime's units join them
    (`WorkUnits.ReadAsync`); a hold that keeps its step active keeps its reading's reason on the node's record (`NodeStatus.Holds`,
    cleared by every move), and `tree land --plan`, `workflow run` and the run's view say it ("Holds the work: …"). Where the
    branch's tip no longer reads (its tree tidied), a step bound there keeps its binding's revision and one not bound yet takes the commit
    its session branch records the line holding, so work merged outside the run reads its holds, landing and line reached and the run
    ends. Its `work` (`claimed`) is reached at the first whole look that reads its quest done, whichever session said it: the one that
    took it, or one that carried it on in its tree after an update cut it; a drain runs no pass, so a done said during one is read after it.
    The plan of a merge whose tree is gone names the line the settings set, else the one its session branch grew from, else says it
    names none. (from D227)
29. A `land`'s open options are resolved once at the bind and kept: the pattern `feature/{quest}-{slug}` and the default key where the
    pattern says `{ticket}`. A `land` names no plugin. A written option stands; no landing rule
    is read. The plan carries `squash` for the branch form, which only a `branch` land may set (the save refuses it on a `merge` or an
    `integrate`; the release's migration writes it for a branch alone), and a session or a conversation in a tree of its own is told once, after
    the branch line, that its commits land as one (`ProcessWords`). (from D164, D227, D232)
30. `merge` lands into the line. `branch` makes a new branch from the session's branch, moves no checkout, and refuses a name that
    exists. `integrate` hands it to the line's integration. (from D87, D215)
31. Daoris pushes, as a `push` step or the person's press, through the one push act (`PushDoor`, off the pass, under the lease); the node
    is reached when git's remote-tracking ref holds the commit and failed by the act's refusal, and only of the attempt the refused tip
    is bound to; an act that ends neither done nor refused is said in the machine log and leaves the node active. Daoris opens no pull request: a plugin's
    performed step does, for its platform, and the pass writes the address its completion's evidence gives (`evidence.url`) and its plugin on
    the branch's landing record (`NodeRunKeeping.KeepPullRequests`, `PullRequestRecords`), so `work/state` has an address to ask about and the
    page shows the pull request. A run whose graph holds a `<plugin>/pull-request` step no installed, switched-on plugin performs is refused at
    its bind with its reason (`PullRequestUnserved`, the binding's `Problem`), never run to a step nothing performs. A saved graph that
    landed by `push` or `pull-request` converted once (`PushedGraphs`, the home's `push-steps` step), and a delivery a pre-G8 build began
    for a plugin (`plugin-dispatched`, `plugin-answered`, the `push` and `pull-request` methods) is read, said and never sent again
    (`DeliveryReconciler`); both end when no home saved before G8's release can start, and G8-CONVERT1 deletes them. (from D241)
    Past the pull request a place composes a `monitor` and a `tidy` from the prebuilt set `land`, `push`, `pull-request`, `monitor`,
    `tidy`, which the shipped pull-request delivery holds whole. A `monitor` watches the one step wired straight into it by `after`
    through that step's own state reading (`IWatchable`, which a pull-request step declares: `PullRequestReading`), and is reached with
    the state as its outcome (`NodeSource.Watched`); open or unanswered it is pending, and it never fails. A pull request reports `merged`
    once the answer the opening plugin gave through `work/state` (asked at D148's moments) says completed into the line the work grew
    from (a completion into another branch is no merge, as the content proof's `other-target` has it), a squash read as merged, and
    `closed` once it says abandoned; a pull request kept anew on the same landing starts with no answer. With no answer, a landing the
    keeper proved its line holds (`held`) reads merged (`PullRequestReading.OnTheLine`): a request completed on the platform that no plugin
    answered for. What a landing leaves to deliver is `NothingToDeliver`'s, Daoris's skip on the path with its sentence, held while the landing
    holds: a session that committed nothing makes no branch (its session branch held `empty`), so every step after the `land` not started yet
    is passed over as nothing to deliver, and a `push` already begun is passed over by the landing record (`SkipBy` `landing`); a landed
    branch the line already holds passes over its `push` and pull-request steps not started yet, and its monitor reads it merged
    (`NothingToDeliverTests`). The monitor declares those outcomes in the file, and the shipped delivery wires `when merged` to the
    `tidy` and `when closed` to a person's task (`human`); the page says those outcomes, and the task's `done` and `approved`, in its own words
    (`workflow.outcome.<kind>.<outcome>`, read by the editor's rows, ways and causes, the chart's ports and edges and the run view's outcome line and
    routes by the kind of the step they leave, the raw name left as identity and as the words of a kind with none). The tidy (`TidyDoor`, off the pass, under the lease) ends each part of the
    chain, the session branch of every unit its landing carries: each part's commits kept on a recovery ref, its branch deleted through
    the journal (`JournaledDeletes`, with its Undo), one a tree has checked out left standing and ended all the same, and what it ended
    written on the landing as `tidied` (`ChainTidied`), whole only when every part ended: a part whose door threw after earlier ones
    ended records those as `partly`, which the keeper reads but the node does not, and the next pass goes on from them. Nothing fails a tidy: where the request did not merge its door ends nothing. A chain the look's own
    tidy already ended (its landing gone from the record by its end with nothing tidied on it, the unit's session branch gone too) leaves
    the tidy nothing to clean: its door finds no standing landing to write on, and its reading reaches it from the record
    (`TidiedByTheLook`, `TidyAfterTheLookTests`). Each
    kind's save rules are its own (`INodeKind.Problems`): the save refuses a `monitor` on no step, on two, after a step that reports no
    state, or with outcomes other than that step's states in their order (`monitor-watch`), a `tidy` no monitor's `when merged` way
    reaches (`tidy-merged`), and a `push` no way from a method's landing reaches or after a landing not of the `branch` method
    (`land-tail`). The palette offers a monitor with its outcomes and its `when` ways. The keeper (`EndKeeping`, `EndBy.Tidied`) and the
    press (`TreeSweeps`, resting on the part's recovery ref) read that record for a part at the tip the tidy ended it at, and judge it no
    further: the keeper keeps that end once, and a later pass over the same repository starts the same git and writes nothing
    (`TidiedPartKeepingTests`); a run's passes reach the `monitor` on a merged request, start the `tidy` on its door and leave it reached with
    the part ended, and a closed request reaches the person's task and ends nothing (`TidyRunGitTests`). The web reads a pull-request step as a
    plugin's `<plugin>/pull-request` or an older steps version's bare `pull-request` (`isPullRequestStep`), and a graph run finished at its
    `tidy` as merged. The run view marks the step the run stands at as the live one, except a stopped step, which it draws stopped
    (`WorkflowRunView`, `RunViewStopped.test.tsx`). (from D245, D247)
32. A merge runs through the integration transport under the legacy policy: no gates, one at a time, a merge commit. The person's
    press is requested at the `press` door and settled *merged on the person's press*; an automatic `land`'s delivery at its own
    `landing` door (`IntegrationRequester.Landing`), which its delivery's standing authority admits, settled *merged by its workflow's
    landing, with no press*. (from D215)
33. Each delivery's phases are written ahead of their effects: prepared, ref written, recorded, plugin dispatched, plugin answered.
    A restart settles it phase by phase. A preparation cut by a stop is read withdrawn, no checkout needed, and its unit's next landing
    goes on from it; a delivery whose every entry is a preparation its landing withdrew (a refusal made nothing) is settled withdrawn at
    the driver's look (`WithdrawnPreparations`), so it never holds an update back. A delivery is keyed by its unit, tip, method, target and
    generation, the first generation's key being the four alone: a settled row keeps its meaning for every reader, and a retry of a landing
    whose delivery was settled withdrawn starts the next generation, with its own phases written ahead, while a delivered or stopped one is
    still the answer its key gives. The current reader of a unit's delivery reads its newest generation (`DeliveryStore.Latest`, `OfUnit`). (from D215, D227)
34. A landing refused for work the session can clear carries the session on and keeps the Accept. A refusal nothing changed is not
    tried again until the tip moves or the person presses; a try that closed at one tip says nothing of the next. A refusal for the
    repository's own checkout's uncommitted work has a code of its own (`root-dirty`) and waits on that checkout, not the session's tree
    and not a review: it is kept with the checkout's status fingerprint and made again at the first pass where that status differs or
    reads clean, so the landing lands with no press, and its note says it waits for the checkout to be clean. A line that already holds
    every commit of the branch closes the entry as `line-holds`, and a merge left waiting in its line's integration is `queued` and made
    again at every look; neither note says a review waits. (from D145, D227)
35. In a session tree the agent commits on the tree's branch as it goes, and never switches or pushes. The commit rule
    (`commits`) may say `ask` or `at-end`. Daoris follows a tree an agent moved anyway. (from D164)
36. A chain's next step in the same repository starts on the step before's branch, and the chain lands on one branch, which
    advances only while its pull request can take it. (from D82, D149)
37. Branches a landing made are kept in `<home>/landings.json`, with traces once gone. A landing removes no tree and no other
    session branch. A session branch goes only by its own door or the look's tidy, behind D88's proof: every commit is on a branch
    that is not Daoris's, never forced. A gone session branch's entry in `session-branches.json` is kept as a trace while its unit owes
    anything (`WorkflowObligations.Owing`), and a unit a run's live work is bound on owes its landing: while any node of the run is ready,
    active or unknown, every subject its nodes observe is held (`RunHeldUnits`, `RunHeldTraceTests`), so a run whose review the person
    accepted still finds the branch its `land` binds. (from D88, D102, D232)
38. A pull request's state is asked of the plugin that opened it, at most once a minute per pull request. A landed session's
    review reads its landed branch. A session in a folder's review is its turn's change set from its manifests, each text file
    with its diff and a file that is not text with none; it names no base, and a folder's page and review say that branches,
    history, integration and landing are git's. (from D113, D148, D254)
39. Integration is one run per line, outside every run, with fixed steps. `done` and `steward` requests need declared, accepted
    gates. The line's policy is `linePolicies`, how the line is integrated; who requests and where a conflict goes are the
    `integrate` step's, the bound version's in a run and, outside one (the sweep, the steward's door, the recheck at promotion, the
    card's wait), the newest version of the place's default workflow (`PlaceIntegrate`), the owner's where it has none. A failure is
    confirmed once before it is blamed; a gate that would not start (its folder missing, no job to hold it) is an unknown run, asked
    again, never a failure that blames a member. (from D215, D227, D232)
40. A verification pipeline's `check` is Daoris's, with no agent session, never skipped. A gate that would not start (its folder
    missing, no job to hold it) makes the check unknown, naming that gate and failing none. Where a repository's landed route names a
    verification pipeline with accepted gates, each whole look reads its line's tip and binds a run in `workflows/lines/<run>.json`
    (its `line`, and its `base`: the tip of the line's newest run with a verdict, none for the first) once the tip is past the line's
    newest run and no run of the line is in flight. A run is settled when every check reached, failed or was stopped; until then it
    holds the line, an unknown held for the person included, and is never superseded. *Run again* is a new attempt at the run's tip;
    *Not now* settles an unknown with no verdict, so the next batch, bound at the next landing, takes it in; on a failure it settles
    the held row as the person's and leaves the verdict failed. *Ask for a fix…* on a failure publishes one ask per failed gate to the
    repository (Daoris's fixed words naming the line, gate and run, the batch's commits and the sessions whose landings carried them,
    read from the integration's requests settled into the line whose tip the batch holds; the gate's last 256 KiB as the ask's file;
    the kind the check's `ask` option names), found again by its words so a restart makes none twice, and a later failure of the same
    gate goes to the open ask's channel once. A check whose `failed` is `ask`, which only a version the person saved can say, has
    Daoris ask for that fix itself at each whole look (`VerifyFixStanding`), the failure still held for the person; `wait`, or none,
    waits for the press, and `stop` asks nothing; the place's *Your part* says which. The terminal's `daoris workflow fix <run> [<node>]`
    is the press's twin (`WORK_HELD`'s `ask-fix`, answering its `said`), and `work held` lists a failed check's words without
    `ask-fix` and names that verb beside them. A check with `reach: paths` runs only the gates
    the batch's changed paths reach (a gate's optional `paths` in the accepted declaration; none reaches every change; this
    repository's gates carry theirs, placed as its merge tool's reach table places them) and every gate whose newest evidence on the
    line is not a pass, and names the rest as *kept* in its result; a batch with no base runs them all. A person's verification of a branch or a commit (`daoris gate verify`, *Verify…*) is a run
    of its own under the accepted gates that the batching never counts. A check's outcome is recorded on `run:<run>` under the `gates`
    source; a held or failed check carries what it verifies (repository, commit, line, base, branch, failed gate, why), which *What
    needs you* words with *Run again* and *Not now*, and the line's read says each run's verdict (verified, failed naming its gate, not
    known with why, not now, running, waiting) and batch, with each gate from the check's newest result. The versions a workflow keeps
    include those a run under `lines/` names. (from D192)
41. `SET_ACCESS` refuses the `landing` pause, and `full` access opens no landing pause on a saved-graph place. (from D184, D227)

**Set up, conversion and Current**

42. *Set up…* offers *Not driven* and the shipped deliveries marked `setUp`: merge after accept, a branch to push, a pull request,
    integrated. It suggests integrated where the line declares integration, else merge after accept; a pull request is only chosen,
    and refused where no plugin performing steps serves the remote's platform. *Keep how it lands* writes no landing and no choice. (from D221, D227)
43. `PLACE_APPLY` is the one adoption door. `place --workspace … --adoption` paces a workspace's adoption by `atOnce`. (from D227)
44. The release's one migration converts the workflow records once through `WorkflowMigration.Convert`, its `workflows` step
    reading the repositories from the service's registry table: each steps, `handed` or `land`+`accept` newest gets the next
    version as a graph; each place on the landing rules follows the home default or a saved copy of what its rules drew; a rule's
    `integrate` becomes its line's policy; the landing rules and work cycles leave `driver.json`. A version the save refuses is
    left, said. Its `run-rebinds` step (`RunRebindsStep`) binds a run whose version does not read here (Current's, or one its
    workflow's file no longer holds as a graph) and that has no node record again, as a first start binds it, to the place's choice
    today; a run whose place chooses nothing that binds is left as it was. There is no door, receipt or undo. (from D232)
45. There is no landing rule: no type, reader or writer of `landings` or `workspaceLandings` outside the migration, which reads them raw
    (`MigratedPlaces`) and takes them out of `driver.json` once. Every door that lands, plans, names a branch or says how work lands reads
    the run's bound `land`; work no version governs lands as the declared merge and is held by its gate. A line's integration reads
    the line's policy. A pull request is asked of the plugin whose step opened it. `LINES`
    answers no `landings` and `SESSION_PROCESS` no landing. (from D228, D232)
46. Each old way still read is registered with the condition that ends it and the row that deletes it. There is no auto lander,
    no look that lands, no due list, and no landing rule's `tidy`; the performer of a run's `land` is the only lander. What a landing try
    came to is a `LandingCode`. The service answers no `POST /api/quests/{id}/bind`. (from D227, D228, D232)

**Drawing**

47. One drawing serves the place's chart, the editor, a run and a proposal's diff: Daoris's components on the measured layout,
    always top to bottom, positions never saved. A wire's kind follows from where it goes. (from D221, D223)
48. Every graph stands on one canvas: a viewport with pan, zoom, fit and find; no node is moved by hand. The workflow zooms 80 to
    200% while edited. (from D224)

## Where a change goes

- **A new step kind:** its id in `NodeKinds` (`Workflows/Nodes/NodeContract.cs`) and its folder `Workflows/Kinds/<Kind>/`, whose
  `<Kind>Node.cs` is a `BuiltInNode` marked `[NodeKind(id, group, after…)]` declaring its row, subject, options, own checks, part,
  offer, title and look; nothing else lists it. Its state logic is the unit's interfaces: `IObserves`, `IStarts` with its door from
  `NodeDoorWiring`, `IPressed`; their bodies call the kind's own reading in its folder (`NodeObserving` holds what every reading shares), and a rule about where a node
  stands in the graph is its `Problems` (`GraphStanding` is the graph read for it); a kind's words for a refused field or for
  never being an end are `DeclarationProblem` and `NeverEndBecause`. A rule that reads several kinds together is the kit's, run
  beside each kind's `Problems` (`NodeKit.Problems`, as `ProtectedOrder` reads each kind's `Guard` and `LandingRules` each kind's
  `ReadsTheLanding`); a graph rule every graph is held to goes in `NodeGraphs.Problems()`, which names no kind.
- **Which workflow work follows:** `Workflows/Domain/WorkflowSelection.cs`, held cell for cell by `fixtures/workflow-selection.json`.
- **A shipped workflow or delivery:** a JSON file under `Defaults/workflows/`; `WorkflowDefaults` reads it, and the presets derive.
- **How a landing behaves:** the method's options, the node's checks and its door in `Workflows/Kinds/Land/`; the act in `Workplace/`
  (`LandPerformer`, `SessionTrees`, `TreeLands`, the delivery files).
- **Integration:** `Workplace/Integration*.cs` and `Review/Integration/`; the node's declaration in `Workflows/Kinds/Integrate/IntegrateNode.cs`, its
  start in `Workflows/Kinds/Integrate/IntegrateStart.cs`.
- **A gate or a hold at landing:** `Review/Domain/` (look, opinion) or `Workflows/Domain/GraphGate.cs` (the graph's part).
- **An old shape to convert:** the release migration's workflow step, `Workflows/Migration/WorkflowMigration.cs`.
- **A door:** a route in the module's `Bridge/Routes/`, its census rows, and the page under `src/workflow/` or `src/agents/setup/`.
- **The tests that pin it:** driver `Workflows/` (`NodeGraphsTests`, `SavesRefusedTests`, `WorkflowSelectionTests`,
  `WorkflowRunBindingTests`, `GraphRunLandingTests`, `GraphGatesTests`, `ChatGraphRunTests`, `HomeDefaultTests`,
  `SetupPresetsTests`, `NodeRuntime*Tests`, `WorkflowMigrationTests`), `Review/` (`ReviewGateTests`, `OpinionGateTests`), `Workplace/`
  (`Landing*Tests`, `Delivery*Tests`, `Integration*Tests`, `PullRequest*Tests`, each landing through a run bound by `GraphLand`),
  `Setup/PlacePlanTests` and `PlaceApplyTests`; modules `DriverModuleWorkflow*Tests`,
  `DriverModulePlace*Tests`, `PipeCensusTests`, `OwnerSetCensusTests`; web `src/workflow/**/*.test.ts*` and
  `src/agents/setup/*.test.tsx`; the family rehearsal's 17c set-up phase (`tools/family-rehearsal.mjs`, with `tools/setup-kit.mjs`).
- **The gates that bite:** `driver`, `driver-process-git` (landing and integration over real git), `modules`, `censuses` (a new
  route), `web-unit`, `web-rules`, `universal` (the structure gate, the names and catalogue checks, and `orient-index`), and in
  `npm run verify` `size-ratchet`.

## Contradictions

- D190 point 4 (a named version holds one `handed`) vs `NodeKinds` (no `handed` kind): a version lands by `land`. D227 and D232 read right.
- D190 point 8 (no look starts with no opinion passed) vs `Workflows/Kinds/ProtectedOrder.cs` (gates only before the landing): the code and D190 point 5 read right.
- D190 "Not settled: FABRIC-CLOSE1k" vs `NodeGoverning.Govern` (a bound quest closed by its bound graph): the code reads right.
- D157 point 1 (a workflow is a list of steps) vs `WorkflowNamed.ReadVersion(onSave)` refusing steps: D227 point 2 reads right.
- D157 point 15 (nothing set is Current) vs `WorkflowSelection.Resolve` (the home default before `Current`): D227 reads right.
- D157's rejection of *merging with nobody* vs the shipped `Defaults/workflows/merge-automatic.json` and `integrated-no-press.json`: the code reads right.
- D87 (how work lands is a workspace's landing rule) vs no rule read (`Workplace/Landing.cs`, the bound `land`): D227 and D232 read right.
- D164 amendment point 2 (the rule holds pattern, key, squash) vs `Workflows/Kinds/Land/LandNodes.cs` (`land`'s options): D227 reads right.
- D145 point 1 (a branch rule's `autoAccept`) vs `LandNodes.Reviewed` (no `review` step means no press): D227 reads right.
- D88 (without `tidy` the tree stays; clean-up is the person's press) vs `BranchTidying.cs` (the look's own tidy): the code reads right.
- D215 point 2 (the install's policy on a branch rule's `integrate`) vs `DriverConfig.LinePolicyOf` (`linePolicies` alone): D227's 1a note reads right.
- D215 point 5 (no node added to any graph) vs `Workflows/Kinds/Integrate/IntegrateNode.cs` (`integrate` kind) and `land` method `integrate`: the code reads right.
- D221's rejection of a place's choice changing through `PLACE_APPLY` vs `Setup/PlacePlan.cs` (Apply chooses the copy through `WORKFLOW_USE`'s own edit): D227 reads right.
- D157 and D223 (no zoom, no minimap) vs D224 and `workflowViewport` in `workflow/chart/FlowChart.tsx` (zoom 80 to 200% edited, 50 to 200% shown): D224 reads right.
- D186 point 3 (a flow placed left to right where it fits) vs D221/D223 (a workflow is read in sections, so never left to right, `design/flowLayout.ts`): both right, each for its flows.
- `NodeContract.cs` (`NodeKinds.Check`: started by nothing) vs `Workflows/Kinds/Check/CheckNode.cs` (its door on the heavy executor): the code reads right.

## Open

- **SETUPUX1x:** the installed sitting of the place page, and its fixes.
- **The place page's opener** (built): Setup is the Agents place's default and its tree of places is always drawn; the opener's `roleSection` names Appointed
  roles or All roles only. A door naming a repository's place says `placeOf`, which `placeOpening` resolves to the place's id.
- **Place pages and the service's binds** (built): a workspace's page has three tabs
  (Details, Branches, Workflow); its setup is its place's (defaults under Workflow, access and Reach under Sessions) and the remote's
  wiring a row of Details, so a door naming a section is sent there by the opener. The service has no door that binds a quest to a graph, none that names one for
  a take, and no take bind: the store's open drops `quest_take_binds` and `quest_owned_take_binds`, and the `took_bind` session column is
  gone. A `Bound` operation only arrives in a log from an older build, which the replay still reads; service tests seed one with
  `QuestStore.SeedBound` (`BoundSeed`). A start binds its own run (`WorkflowRunStart.AtStartAsync`) and the look only says what the starts bound
  (`WorkflowRunStart.Say`); the help room names no `workspace-setup` part.
- **CHATFLOW1 (the rest):** the installed window.
- **INTEGRATE1 k, m:** the flip of this repository's integrator and a sibling repository's declared integration; h's installed proof
  (i, the steward's acts, is parked with STEWARDROLE1d). A `request` under `start: you` waits for the person's *Send*, the `send` act of the run's node door (`WORKFLOW_NODE`, `daoris
  workflow send`), which keeps a mark on the record (`RunNodeWords.SendKey`) that the next pass reads as the start's say-so.
- **FABRIC-SQUASHCHAIN1:** the monitor and the tidy are built, read and proved end to end (rule 31); nothing left but the owner's acceptance on an install.
- **FABRIC-PERFORM1l:** the pull-request event keeps its fields; WORKFLOW1j and 1k on observed nodes.
- **LANDNODE1j:** the installed window's proof.
- **PROCSTEPS1:** designed in `docs/2026-11-09-process-steps-design.md`: `rebase`, `resolve`, `merge` and a generic `tidy` as kinds
  of the kit, `check` fed a commit, the `land` kind removed, and every preset a composition of them.
- **REVIEWFLOW1c and LINESTOGETHER1:** a session's steps as one flow, proven in the installed window.
- **WORKSETUP1:** no pull request is made by default; a place that needs one composes it from `push` and `<plugin>/pull-request` steps in its
  workflow, and the first such place is proved end to end on the install.
