# Workflows, review and landing — how it works now

## What it does

A workflow says how a repository's work moves from an agent's done to the line. It is a saved graph of steps of fixed kinds:
holds, a second opinion, the person's look, the person's Accept (`review`), one landing (`land`), and what follows it (a pushed
branch, a pull request, integration, a release). Each place (this machine, a workspace, a repository) follows one saved workflow;
a fresh home follows a shipped copy of *Merge after you accept*. A chain's work in one repository is a run, bound at its first
start to one version. The node runtime in the driver loop evaluates the run's graph and performs the `land` itself once the gates
hold: a merge into the line, a branch for the person, a plugin's push and pull request, or the line's integration. Work no saved
workflow governs is held, and nothing lands it.

## Its parts

Paths are under `src/Daoris.Desktop/Daoris.Desktop.Driver/` unless they name another root, each type in a file of its name;
`docs/index/README.md` says where a seam, route, verb or fixture is, and `docs/index/registered.md` lists the kinds in order.

| Where | Its job |
|---|---|
| `Defaults/workflows/`, `Workflows/` | The shipped graphs and presets, the saved files (`WorkflowStore`), the bind (`WorkflowRunStart`), the gate interface (`WorkflowProcesses`), the palette, chat and verification runs, `daoris workflow` |
| `Workflows/Domain/` | Choosing (`WorkflowSelection`), a version read and its digest (`WorkflowNamed`), a run derived from its records, the graph's gate (`GraphGate`) |
| `Workflows/Kinds/<Kind>/` | Each step kind whole: its declaration (`<Kind>Node`), options, save rules, reading and door (rule 49) |
| `Workflows/Deployables/` | A repository's declared deployables (`DeployableDeclaration`), each keyed to its release nodes (`DeployableTargets`), and their read (`DeployablesCommand`) (rule 3b) |
| `Workflows/Nodes/` | The runtime: one pass (`NodeRuntime`), what it governs, judges, takes in turn, tells the plugins and keeps; the performer (`WorkPerformer`), the wires, the graph rules (`NodeContract`, `NodeGraphs`), the person's waits |
| `Workplace/` | The landing performer (`LandPerformer`), a tree's landing (`TreeLands`, `LandingPress`), deliveries, landed branches, pull requests, integration, entry landings, the together press, tree hand-offs |
| `Review/` | The look and the opinion (`Review/Domain/`), entry Accepts, the session-tree gates, the heavy executor |
| `Setup/`, `Settings/LinePolicyKey.cs`, `Workflows/Migration/` | *Set up…*'s plan and Apply; the line policies; the release's conversion |
| modules `Bridge/Routes/` | `WORKFLOW_*`, `LANDING`, `LAND_ENTRY`, `RUN_REVIEW*`, the set-up and tree doors; the Accept's waits and answers |
| web `src/workflow/`, `src/work/review/run/`, `src/agents/setup/` | The chart, the editor, the place page, the run view and the review page |

## Its rules

**Choosing and saving**

1. A place follows a saved graph workflow. A fresh home saves a copy of `merge-after-accept` as `defaultWorkflow`, once; a home
   that has a `driver.json` is never seeded and converts on the person's press. (D227)
2. The workflow a chain's work follows is the first of: the task's choice, the repository's for the kind, its default, the
   workspace's for the kind, its default, the home default. With none, its run is bound to no workflow (`current`) and held.
   (D157, D227, D232)
3. A repository naming any development workflow replaces its workspace's whole. No door writes `current` as a choice; a file
   holding it is still read. (D157, D227)
3a. A repository's own values for a workflow it follows are options over it, never a copy: `driver.json`'s `workflowValues`, by
   repository, workflow id and node id, each option's JSON value (`WorkflowValuesKey`), laid over that node's options (`NodeValues`)
   wherever a run of the repository bound to that workflow is governed, so a step's attempts carry them; the graph, its digest and the
   binding stay the version's, and naming values names no workflow. No door writes them yet: the file is edited. (D288)
3b. A release target is keyed by the deployable it releases. A repository declares its deployables in its own `daoris.json`
   (`deployables`: an `id` unique in it, the repository-relative `paths` that build it, and what it `provides`, `owns` or `migrates`,
   and `consumes`, as a `mirror`, `calls` or `reads`, by resource), read from its line's `daoris.json` blob (`DeployableDeclaration`;
   an entry that does not read is left out and said, the others stand). A release node's values name the deployable under
   `deployable` and the earliest act that deploys into its target under `deploysOn` (`push`, `pull-request`, `merge` or `start`; read
   as `push` where undeclared, so every exposing act waits); a repository with two deployables on one build has two release nodes.
   `DeployableTargets` keys each deployable to the nodes naming it: undeclared where none does, several where two of one workflow do,
   and a node naming a deployable the manifest does not declare is said. Daoris reads those two names alone; the target's ids are the
   release step's plugin's to resolve. `daoris repository deployables <repository>` says it, writing nothing. (D290)
4. The landed route (`landed`) resolves on its own, names only a verification pipeline and keeps the accepted
   `daoris.gates.json`'s blob id; a changed route applies to the next run. (D192) The line's `daoris.gates.json` is accepted once for
   every purpose it serves (`DeclarationAcceptance`: the integration's blob and the landed route's gates): as it lands in a full-access
   repository (the look says the checks changed in one sentence), by one press (`INTEGRATION_GATES`, or `daoris integrate <repository>
   --accept|--withdraw [--plan]`) in restricted mode, shown as checks added, removed and changed by name with their commands
   (`DeclarationChange`), the row ending at the press. (D283)
5. Saving (`WORKFLOW_SAVE`) and choosing (`WORKFLOW_USE`) are two presses; *Set up…* alone does both, in one receipt-kept Apply. A
   choice is refused (`place-kind`), nothing written, where a department it reaches cannot run one of its steps by its place's
   kind: the git kinds (`land`, `push`, `monitor`, `tidy`, `integrate`, `line`, `content.holds`, `rebase`, `resolve`, `merge`, a
   pull request's Daoris half) run in a repository alone, `isolate` in a repository or a folder, `return` in a folder, `apply` in a
   system, the rest anywhere. *Work in place* (`in-place`) holds no git kind, and the window's add chooses it for a folder, a
   system and a repository the person ticks (`daoris place <department> --preset in-place --apply`). (D221, D227, D264, D265)
6. A workflow is one file, `<home>/workflows/<id>.json`, on this machine. A version is whole and never edited; a save appends the
   next number. The newest 20 and every version a kept run names are kept. (D157)
7. Only graphs are saved, and only a graph version binds and runs. The one save judge refuses steps (`steps-refused`), a `land`
   with no method (`land-method`), an `accept` option (`accept`), and a kind, port or outcome declared arriving
   (`arriving-kind`). (D227, D232)
8. A graph holds at most 24 authored nodes, 96 wires and one `land`. The steps writing the work (`land`, `return`, `apply`) come
   after the `review` where there is one, on every way (`review-order`), each fed the last review's record on its way
   (`review-ambiguous` where two feed one). A graph ends at any node no such step lies past (`effect-past-end`); a development
   graph with no `land` holds a `claimed`; holds judge the work before the landings, or before the reviews where nothing lands.
   There is no `handed` kind. (D190, D227, D232, D264, D273)
9. The opinion, the look and the `review` come before every landing, wired straight into it, and no landing starts before a look
   has passed; the opinion need not precede the look (`ProtectedOrder`). Each hold (`quest.holds`, `content.holds`,
   `work.requests`) is judged once, on every way to a landing. `quest.holds` never reaches on a done checked against nothing: an
   ask's quest with no requirement waits *unchecked* until the person passes it with words, their through words on its ask pass it
   (rule 23), or a version turns its `yes` off. (D157, D190, D247, D255, D277)
10. A `land` method brings a tail only for `integrate` (one `integrate`). A `push` stands after a `branch` landing, and a pull
    request is a plugin's step (`<plugin>/pull-request`) after it, composed by the place, never a method. A workflow that cannot
    bind or be drawn holds on a coded `BindingProblem`, worded from the catalogue. (D215, D227, D241)
11. A workflow file marks its pipeline: development (absent) or verification. A verification version holds at least one `check`
    and no `claimed`, `opinion`, `look`, `go-ahead`, `land`, `review`, `integrate`, `work.requests` or `quest.close`. (D192)
12. A change that lowers the person's part applies only by the person's press; an agent's stays a proposal. Editing the home
    default's copy asks the consent of every place that follows it. (D157, D192, D227)
13. Ask Daoris writes its change to the editor's open draft as a graph in the room's `drafts/`, and saves a version only by
    pressing `daoris workflow apply <id> drafts/<id>.json --base <version>`, which the person confirms on a card drawing the graph
    and its changes. It proposes no workflow and never chooses or runs one. (D223, D261)
14. The shipped defaults hold no `quest.close`: the agent's done closes the quest, and the flow's end is the work's. (D190)

**Runs**

15. A run is a chain's work in one repository, bound at its first start on this machine and written once. Its id is the chain's
    first quest in that repository. (D157)
16. One engine: a run bound to a graph version is the node runtime's. A run bound to anything else is held, its problem said.
    (D190, D227, D232)
17. A graph run is the unit `run:<run>` of its own node record. `claimed` observes the chain's quests and carries the work on
    `work`; a `review` binds its review record (rule 22); the gates, landing and plugin nodes bind the run's branch at its tip, the
    one its tree moved onto once the look learned it; a `go-ahead` binds the run's ask. Once its landing has reached, the steps bound to
    the landed branch keep it whatever branch stands as the run's after (an earlier attempt's once the landed one is tidied away), so only
    its chain coming back here or the person's word takes a landed run back (`LandedBranchKept`). A branch is the work of the quest its
    session served, never of the chain's first quest a landing is named for, which may be another repository's. A branch several
    runs made is each run's, one shared with Current's work held, said; a plugin's event on it goes to the runs awaiting it at that commit, else to those
    whose graph names the step. A node fed on a `data` wire binds that value, so a moved input rebinds only what it feeds; a
    target's tip, once read, rebinds only while nothing fed is active or reached. A node only a skipped producer feeds is skipped
    with it. An unbound step after a review, its tip unread, binds the commit the review accepted. A step's commits stay on
    `refs/daoris/steps/<run>/<node>/<attempt>` until the run ends settled. (D190, D259, D260)
18. The runtime is the record's one writer: a door keeps the person's word (skip, stop, send, ask again, go on, accept,
    changes, done, approve, reject) and the next pass applies it; no run ends while a word waits. A failed plugin step's run step
    holds its failed attempt for the person (`Held`, `PluginFailureHeld`) as a check's does, and its *Try again* is that attempt's
    `again` (`WORK_HELD`, `daoris work held <unit> <node> <attempt> again`), the one press What needs you makes too: the table's
    failed to ready by the person, a new attempt its plugin is sent. A run ends when its graph's end settles or
    no node can move, or when its quest closed and every standing step watches a branch the line holds whole (`on-line`) or that is
    gone (`branch-gone`), never while a step after a reached landing stands (a monitor, a plugin's release), since that reads the
    landing, which a tidied branch and the line's holding the commit leave readable; it is not governed again until its chain
    comes back. A plugin's outcome sent again for a settled attempt under another plugin id (a rename since) is a replay, not a
    counted refusal. A word names its author; a second on a node is
    refused `worded`, but the person's replaces a supervisor's pending one, and a supervisor's on a lapsed attempt or a dead
    decision is dropped `stale`. The Accept names its review record by attempt and revision, or binds the one standing, and is
    checked as kept and again as applied against the work read afresh, either miss `moved`. `changes` may name the part they go
    to, else the newest. *Stop…* stops the run before the Accept and *Stop what follows…* after it, promising no undo. A failed step
    has the same stop, one door for the window, Ask Daoris and the terminal: it ends the step and, nothing else standing, its run as
    `stopped` with the person's words, also where the run had ended failed at that step (the stop governs it again to end it). A
    step the record holds ready, active or invalidated in a run that ended reads not reached, never working
    (`EndedSteps`, read at the drawing and never stored). An Accept
    waits for its pass up to 20 seconds, never behind a tree's hold, that pass judging the pressed run first. A quest bound to a
    closure graph is closed by that graph. (D190)
19. A `go-ahead` holds the landing until the person's yes or their skip with words. (D190)
20. A conversation's run binds its graph. Its subject is the candidate the person submits, numbered past every one the run has
    had, whatever branch it was on; a newer submission lapses it. A graph holding `quest.close` holds a conversation's press. (D227)
21. On a place that follows a saved workflow, a session's checks are `claimed.checks` on the graph and a `place checks` write is
    refused; elsewhere they are the repository's rule. Under `light` a session runs only the gates not marked heavy. (D192, D227)

**Review and Accept**

22. The person's Accept is a `review` before the steps writing the work. A graph with no `land` is work in place and refuses a
    landing's press. A flow with no `review` lands with no press, its authority judged again before the first effect. A run is over
    when no node can move, nothing is pending and no failure is kept for the person, its end `completed`, `failed` or `stopped`
    (with whose). Each review attempt reads the run's work into one record, written as it begins and never edited
    (`workflows/units/<key>.reviews/<node>-<attempt>.json`): its kind (`branch`, `checkout`, `folder`, `copy`, `plan`, `answer`),
    the artifact, every part's handback and requirements, changed files with hashes, and what could not be read. A branch run binds
    its review to the tip, any other to the record's revision (a digest of artifact and handbacks), so a moved revision begins a
    new attempt and record. Only a branch's landing seen made, or its commits on the line, reach a review without a press; a branch held `empty` waits for the person. A folder
    reads from the run's baseline to its newest turn's end, a copy against its base, a checkout by its turns, a system by its
    newest plan. (D145, D227, D264, D273)
23. The Accept is the record's word at the node door: *Accept* in the window where the run holds a review, `daoris workflow
    accept` in the terminal (`trees land --plan` presses nothing). Where the runtime performs the `land`, it keeps the word,
    carries the opinion's answer to its gate, asks a pass and waits up to 20 seconds to say what the try came to (landed, closed,
    nothing to land, a gate's wait, uncommitted work, `moved`, carried on), else *accepted; landing*. On a conversation's run it is
    that conversation's press, refused by a `press` code (`busy`, `elsewhere`, `moved`, `asked`). A carried Accept covers its
    landing's repair alone, while the parts and handbacks are the accepted record's. An Accept before the review begins is kept
    (*accepted; it lands at the next look*), applied once the review waits, dropped `moved` if the work moved. Where the run waits
    on an unchecked done (D255), an Accept naming it (`unchecked`) passes each unchecked `quest.holds` in the same edit, saying so
    (`none-unchecked` where none waits); no other hold passes this way, and a gate held only by the review just accepted holds
    nothing. The Accept is also the try again. No door lands in place: work no run's `land` performs is held with why. A tree door
    given a tree's name takes the session whose tree it is. Where the run's ask holds the person's through words, the runtime keeps
    their word itself, a step at a pass: each `quest.holds` waiting unchecked is passed, then the review waiting on its record is
    accepted, bound to the record standing, each saying *accepted on your words, "<quote>", kept on ask #…*. It does so only where no
    requirement of the run's quests departed or waits unmet, no other step waits on the person (a hold, an open look or opinion, a
    go-ahead, a task, an approval, a choice), nothing is stopped, neither the ask nor a quest of its line is paused here, no word is
    kept and the person asked for no changes on that review;
    else the run waits for them. A go-ahead is never passed on them. The run's step so moved says *Accepted on your words* (a review) or
    *Passed on your words* (an unchecked done) with the quote and its ask (`Through`, read back from the words kept), and each is one
    notice in *Done and failed* by run, node and attempt (`ThroughNotices`). (D227, D228, D256, D273, D277)
24. An entry's or an ask's one Accept keeps each part's word on the record the press drew for it and lands it at the pass; a part
    no `land` performs is held, said. It is not atomic: a moved part is refused alone, and the answer says which landed, accepted,
    moved and failed. A part lands on its run's branch; each quest an ask sent is a run of its own unless its chain leads back to an
    earlier one there. `TOGETHER_PRESS` lands nothing. (D167, D227, D232, D259)
25. The look is the person's verdict in a review environment; no agent or card gives it. Under a saved graph the `look` node says
    whether one is needed, the environment still the `reviews` rule's, and `SET_REVIEW required` is refused. (D154, D227)
26. A second opinion is another maker's read-only reading before the landing; its findings reach the working session once. A
    dispute is answered by the person's *Reviewed* or *Accept…* given after the answers, findings and any recheck reached them,
    never before. The person's word at it holds at the tip it covers; only a newer commit asks again. Under a saved graph the
    `opinion` node says whether one is asked and among which declared reviewers, and cannot start with none; `SET_OPINION none`
    there is refused. (D155, D190, D227)
27. Review's diff is a dependency graph of git reads, kept by no later open, taking no lock and refreshing no index in a tree
    Daoris does not own; within one open each question is asked of git once (`GitReadOnce`), and leaving the page stops its git.
    (D202)

**Landing**

28. For a graph run the runtime is the one performer of `land`, started once on the landing door, off the pass, under the tree's
    hold and the delivery key, so a press and a pass land one delivery; with no review before it, it goes active with no press.
    One whose subject moved runs again at the new tip once every delivery at the old reads certain. Branch-side steps bind the tip
    of the branch the work stands on, read in the tree of its newest executor; a hold keeps its reason on the node, said by the
    plan and the run view. Where the line holds every commit by content or as the same change, `content.holds` and `land` reach it
    without replay, saying the proof (`LineHolds`). Where the tip no longer reads, an unbound step takes the commit its session
    branch records the line holding, so work merged outside the run ends it. `claimed` reaches at the first whole look reading its
    quest done, by any session. A merge whose tree is gone plans to the settings' line, else the one its branch grew from. (D227)
29. A `land`'s open options are resolved at the bind and kept: the pattern `feature/{quest}-{slug}` and the default key where it
    says `{ticket}`. A `land` names no plugin, and no landing rule is read. `squash` is the `branch` method's alone, and a session in
    its own tree is told once that its commits land as one. (D164, D227, D232)
30. `merge` lands into the line; `branch` makes a new branch from the session's, moves no checkout and refuses an existing name;
    `integrate` hands it to the line's integration. A landing whose commits add a line matching a private pattern
    (`sensitive.patternsFile`, read in the repository's own checkout) is held before anything is made, naming the path and the
    pattern's number, never its text. (D87, D215)
31. Daoris pushes, as a `push` step or a press, through the one push act, off the pass under the lease: reached when the
    remote-tracking ref holds the commit, failed by the act's refusal. Daoris opens no pull request: a plugin's step does, its
    evidence's `url` written on the landing for `work/state` to ask about; a graph whose `<plugin>/pull-request` no switched-on
    plugin performs is refused at its bind. `PushedGraphs`, an older build's plugin delivery (read, never sent) and the web's bare
    `pull-request` end when no home saved before G8's release can start; G8-CONVERT1 deletes them. A `monitor` watches the one
    step wired into it and reaches with that step's state; open or unanswered it is pending and never fails. A pull request is
    `merged` once its plugin says completed into the work's line (a squash counts), or unanswered once the keeper proves the line
    holds it, and `closed` once abandoned. A landing with nothing to deliver passes over the steps after it not yet started, said;
    a branch the line already holds passes over its failed `push` and pull-request steps too (a pull request merged another way
    after its step failed), so the run goes on to its monitor and the held list clears.
    A `tidy`, reached only by a `when merged` way, ends each part of the chain off the pass: its commits kept on a recovery ref, its
    branch deleted through the journal with its Undo, or kept where a tree has it checked out (a run's own tree then goes at the
    look's sweep, rule 31b), `tidied` written when every part ended, else `partly` for the next pass; nothing fails it. The save refuses a `monitor` not watching one stateful step (`monitor-watch`), a `tidy` off `when merged`
    (`tidy-merged`) and a `push` no `branch` landing leads to (`land-tail`). (D241, D245, D247)
31b. A `release` ends the trees a run owns once its work has landed: it is performed through its door, once per node and attempt
    (`TreeReleases`), and removes each session branch the run owns (`GrownBranch.Run`, written on its record at every governed
    pass) with its tree behind the clean-up's own unforced proof. A tree with uncommitted work, or commits no branch of the
    person's holds, stays: the refusal is kept on its record (`GrownBranch.Kept`), drawn in the tree list beside its owner run, and
    said in the step's words. It reaches either way and fails nothing; in a graph that sets nothing apart it finds no tree and says
    so. A save refuses one no way from a `land` reaches (`release-landing`). The shipped development workflows put one after the
    landing (after the line, or the tidy), the editor draws it in `Git` after the line, and a drawn graph matches its shipped copy
    with or without it (`MigratedGraphs.Shape`). A tree whose part a run's `tidy` ended, and the trees of a run that holds no
    `release`, ended or abandoned, are ended by the look's sweep (driving rule 51). (D228)
31a. A plugin step declaring a `goal` (at most 1,200 characters) is performed by an agent, never the plugin's process. Its ask (the
    goal, the run's place and commit, the landing's evidence, the step's outcomes, schema and options as its graph writes them) is kept under
    `workflows/agent-steps/<invocation>.json`, and the step's session starts off the pass: staffed as the place's builds, a chat on
    no quest at the run's checkout holding no tree, lease or cap, read-only, with only its plugin's `daoris-plugins` server and
    `step_answer`, refusing the person's words (*Workflow step* in Sessions). `step_answer` keeps one answer, `reached` with a
    declared decision, evidence and outputs, or `failed` with a code, applied as the plugin's completion; a session ending
    unanswered fails it `no-answer`. A step with no goal stays on `work/perform`. (D274)
32. A merge runs through the integration transport under the legacy policy (no gates, one at a time, a merge commit): the person's
    press at the `press` door, settled *merged on the person's press*; an automatic `land`'s delivery at its `landing` door,
    settled *merged by its workflow's landing, with no press*. (D215)
33. Each delivery's phases (prepared, ref written, recorded, plugin dispatched, plugin answered) are written ahead and a restart
    settles them in order. A preparation cut by a stop reads withdrawn, and a delivery all withdrawn settles so at the look,
    holding no update back. A delivery is keyed by unit, tip, method, target and generation: a retry of a withdrawn one starts the
    next generation, and readers take the newest. (D215, D227)
34. A landing refused for work the session can clear carries the session on and keeps the Accept. A refusal nothing changed is
    tried again only when a tip moves or the person presses, and work landed at its bound tip is done with its landing. A refusal
    for the repository's own checkout's uncommitted work (`root-dirty`) waits on that checkout and lands with no press once its
    status changes. A line holding every commit closes the entry `line-holds`; a merge waiting in integration is `queued`, made
    again each look; neither says a review waits. (D145, D227)
35. In a session tree the agent commits on the tree's branch as it goes, and never switches or pushes. The commit rule (`commits`)
    may say `ask` or `at-end`. Daoris follows a tree an agent moved anyway. (D164)
36. A chain's next step in the same repository starts on the step before's branch, and the chain lands on one branch, which
    advances only while its pull request can take it. (D82, D149)
37. Branches a landing made are kept in `<home>/landings.json`, traced once gone. A landing removes no tree and no other session
    branch; a session branch goes only by its own door, the look's tidy or, where a run owns its tree, that run's `release` or
    the sweep (rule 31b), behind D88's proof. A gone session branch stays traced
    while its unit owes anything, and a live run holds every subject its nodes observe, so an accepted `land` finds its branch.
    (D88, D102, D232)
38. A pull request's state is asked of the plugin that opened it, at most once a minute each. A landed session's review reads its
    landed branch. A folder's review is the turn's change set from its manifests; it names no base, and says that branches,
    history, integration and landing are git's. (D113, D148, D254)
39. Integration is one run per line, outside every run, with fixed steps; `done` and `steward` requests need declared, accepted
    gates. The line's policy is `linePolicies`; who requests and where a conflict goes are the `integrate` step's (the bound
    version's, or outside a run the newest of the place's default workflow, else the owner's). A failure is confirmed once before
    it is blamed; a gate that would not start is an unknown run. A `request` under `start: you` waits for the person's *Send*.
    (D215, D227, D232)
40. A verification pipeline's `check` is Daoris's, off the pass under the heavy slots, with no agent session, never skipped; a gate
    that would not start makes it unknown, named. Where the landed route names a pipeline with accepted gates, each whole look binds
    a line run, based on the line's newest verdict, once the tip passed it and none is in flight. A run holds the line until every
    check reached, failed or was stopped, is never superseded, and keeps its files until a newer run has a verdict. *Run again* is a new attempt; *Not now* settles an unknown with
    no verdict, or a failure's held row. *Ask for a fix…* publishes one ask per failed gate with its last 256 KiB, never twice;
    `failed: ask`, only in a version the person saved, asks at each look, `wait` waits for the press, `stop` asks nothing.
    `reach: paths` runs the gates the batch's paths reach and every gate not last passed, naming the rest *kept*. A person's
    verification (`daoris gate verify`) is a run the batching never counts. A session's own heavy check is the broker's
    (`check_run`, driving rule 40a), the same heavy run under the same admission; a hand-back's checks are chosen by `GateReach`,
    the declared gates' reach, as a landing's are. (D192)
41. `SET_ACCESS` refuses the `landing` pause, and `full` access opens no landing pause on a saved-graph place. (D184, D227)

**Set up, conversion and Current**

42. *Set up…* offers *Not driven* and the shipped deliveries marked `setUp`: merge after accept, a branch to push, a pull request,
    integrated. It suggests integrated where the line declares integration, else merge after accept; a pull request is only chosen,
    and refused where no plugin performing steps serves the remote's platform. *Keep how it lands* writes nothing. (D221, D227)
43. `PLACE_APPLY` is the one door of a place's set-up: its adoption, and a room asked of an adopted repository (`--room`, a part that rides with no
    other and is a quest to that repository's own session, driving rule 53). `place --workspace … --adoption` paces a workspace's adoption by `atOnce`. (D227, D276)
44. The release's one migration converts the workflow records once: each steps, `handed` or `land`+`accept` newest gets its next
    version as a graph; each place on the landing rules follows the home default or a saved copy of what they drew; a rule's
    `integrate` becomes its line's policy; the rules and work cycles leave `driver.json`. `run-rebinds` binds again, as a first
    start would, a run whose version does not read here. `review-records` keeps an older build's Accept on a branchless review only
    where the pinned graph ends there, else drops it, said. `abandoned-runs` stops each live step of a run whose quest is declined
    or ask closed. No door, receipt or undo. (D232, D273)
45. There is no landing rule: nothing reads or writes `landings` or `workspaceLandings` but the migration. Every door that lands,
    plans, names a branch or says how work lands reads the run's bound `land`; ungoverned work lands as the declared merge, held by
    its gate. A line's integration reads its policy, and `LINES` and `SESSION_PROCESS` answer no landing. (D228, D232)
46. Each old way still read is registered with the condition that ends it and the row that deletes it. There is no auto lander,
    landing look, due list or rule's `tidy`; the performer of a run's `land` is the only lander, and a try's end is a
    `LandingCode`. A start binds its own run; the service binds no quest or take to a graph, and its replay still reads an older
    build's `Bound` operation. (D227, D228, D232)

**Drawing**

47. One drawing serves the place's chart, the editor, a run and a proposal's diff: Daoris's components on the measured layout,
    always top to bottom, positions never saved. A wire's kind follows from where it goes. (D221, D223)
48. Every graph stands on one canvas: a viewport with pan, zoom, fit and find; no node is moved by hand. The workflow zooms 80 to
    200% while edited. (D224)

**The kinds**

49. Each kind is one unit, `Workflows/Kinds/<Kind>/<Kind>Node.cs` marked `[NodeKind]`, declaring its row, options, checks, offer,
    schema, title and look; `NodeKit` is built from the marks, and the page holds no list of kinds, reading `NODE_KINDS` from it in
    every language. The runtime asks a unit's interfaces and names no kind; a door kind's work leaves the pass only through the
    door runner, once per start key, under the run's own step (`StepAuthority`). (D247)
50. `isolate` is a graph's one entry, wired straight into `claimed` and performed by the start before the record opens. Its options
    are `kind` (`auto`, `worktree`, `copy`; one its place lacks holds the start), `at-once` (`2` to `24`, or `any`) and `overlap`
    (`go`, `wait`); a graph without one runs one session at a time in the place's checkout (driving rules 12 to 14). The trees it
    makes are ended by a `release` after the landing, else by the sweep (rule 31b).
51. A `human` node is a manual operation, reached by the person's *Done* or, for an approval, *Approve* with an optional note, or
    rejected by *Reject* with a reason, as a review's changes are. Its outcome (`done`, `approved`, or none) routes a `when` wire, a
    rejection the `rejected` wire. A press on a node not waiting is refused `not-waiting`, on another kind `not-task`.
52. A system's `apply` hands the adapter the plan its review record names, never a newer one, one apply at a time per system; no
    such plan reaches it with *nothing applied*, an empty one with *changed nothing*. A system declaring its precondition `none` is
    read before and after each write. `stale`, `changed-while-applying` and `apply-refused` fail it, naming each resource's present
    value, and a stale plan's receipt of what it applied is kept and told to the work.
53. A folder's `return` (*Bring back*, needing an `isolate`) runs after the review accepts, under the folder's start lock
    (`place-busy`; `moved` where the copy's newest turn is not the one read). It brings the reviewed copy back file by file against
    its base, both sides hashed afresh: a change in the copy alone is written, one in the folder alone left, and one made
    differently on both sides held for the person (`both-changed`), never a supervisor. A path it cannot bring back without
    guessing (a link, a type change, an escape, a name collision) is offered *Keep yours* alone. *Keep yours*, *Take the
    session's* and *Keep both* go per path, bound to the versions shown. A write lands only where the folder's file still holds the
    version read, never over a name; each is journalled with what it replaced, and a cut-off return resumes from the journal. The
    copy goes once every path settles.
54. `rebase`, `resolve` and `merge` are declared arriving: drawn in the editor's `Git` group and refused by a save, so no run
    performs them; their contract is `docs/2026-11-09-process-steps-design.md`. Built beneath them: `rebase` replays work in
    Daoris's own tree, never the person's checkout, the work's branch or its tree; `resolve` reserves a session tree as a hand-off
    row, a start there held (`held.reserved`) unless it carries the row's ask key; one door asks a tree's session to commit.
55. Every step of a live run that only the person moves is read from the bound runs, never stored: an active `review`, `human` or
    `go-ahead`, a `return` on `both-changed`, an attempt held `unknown`, and a step left failed (`left`: a failure kept for the person with no word on it, read for a quest closed done
    alone). Sessions places the run's newest session in Needs you with
    its `waits`, opening the run's view, and *What needs you* lists each with its presses.
56. A pass judges at most 32 units: the run a door asked for, a held event's unit and a record the pass moved first, then in
    rotation those owing a start or a rebinding, then the unchanged, which keep a quarter of the turn. A unit's resource record
    keeps 32 receipts and a guard of 256 dropped keys, so a replayed completion owes nothing new.
57. A run's review is read by its run alone (`RUN_REVIEW`): the record, its outputs (`ReviewOutputs`: each handback a document, the
    changes a change set, a plan a resource plan, each with a stable id and a digest), a branch's or a checkout's commits with the
    uncommitted paths apart (`ReviewBranchCommits`) and the patch per file between the record's commits, the steps before it, presses
    only on the attempt the review stands on, the supervisor's held decision, the steps an Accept may start. A branch's base is the
    commit its chain grew from the line (`RunWorkBase`), frozen with the tip on the record; a record with none reads the newer of the
    local and origin line's merge bases. The **Outputs** view (`outputs/<run>`) pins the attempt first answered, picks any revision,
    and draws *Under review · rN* (the change set first: commits, then every file's hunks in one scroll, a viewed or large file folded)
    and *After the Accept*; a newer revision is a banner naming the outputs that moved, never swapped in. A folder's file is drawn at
    the record's hash, or from the place only while it still hashes so, else refused `moved`; a Markdown file opens on its changes,
    rendered one press away. *Viewed* marks stay on this machine by path and content. Accepting, sending back and stopping a run are
    the **Accept card**'s alone (`ReviewCard`), under the session's conversation, bound to the attempt and revision it draws: a newer
    revision supersedes it and appends a new card, and a stale press is said `moved`. A session's own changes (`DiffPane`) keep the
    tree's discard and draw no Accept; the head, the strip and an entry's part draw an outputs door, *What needs you* and the run view
    open the outputs by run; the one press left outside the card is the ask-level *Accept the ask's parts…* in Sessions' list by ask.
58. A landing's replay settles the declared backlog by its rows' ids: a row one side changed takes that side, one changed alike is
    kept once, any other is left for a person with both texts, and a result losing a side's words or covering an id twice is
    refused. The integration's member merge and the merge tool (`fixtures/backlog-merges.json`) hold it alike.
59. Setup is the Agents place's default. A workspace's page has Details, Branches and Workflow tabs; its setup is its place's
    (defaults under Workflow, access and Reach under Sessions).

## Where a change goes

- **A new step kind:** its id in `NodeKinds` and its folder `Workflows/Kinds/<Kind>/` (rule 49); nothing else lists it. A rule
  about where a node stands is its `Problems`, one reading several kinds the kit's, one every graph keeps `NodeGraphs.Problems()`.
- **Which workflow work follows:** `WorkflowSelection`, held by `fixtures/workflow-selection.json`.
- **A shipped workflow:** a JSON file under `Defaults/workflows/`; the presets derive.
- **A landing:** `Workflows/Kinds/Land/`; the act in `Workplace/` (`LandPerformer`, `TreeLands`, the deliveries).
- **A gate or hold:** `Review/Domain/` (look, opinion) or `Workflows/Domain/GraphGate.cs`.
- **A door:** a route in the module's `Bridge/Routes/`, its census rows, the page under `src/workflow/` or `src/agents/setup/`.
- **Tests:** driver `Workflows/`, `Review/`, `Workplace/` (each landing through a run bound by `GraphLand`), `Setup/`; modules
  `DriverModuleWorkflow*Tests`, `PipeCensusTests`; web `src/workflow/**/*.test.ts*`. Gates: `driver`, `driver-process-git`,
  `modules`, `censuses`, `web-unit`, `web-rules`, `universal`.

## Contradictions

- D190 point 4 (a version holds one `handed`) vs `NodeKinds` (no `handed`): D227 and D232.
- D190 point 8 (no look before an opinion) vs `ProtectedOrder`: the code and D190 point 5.
- D190 "Not settled: FABRIC-CLOSE1k" vs `NodeGoverning.Govern` (a bound quest closed by its graph): the code.
- D157 point 1 (a list of steps) and point 15 (nothing set is Current) vs the save and `WorkflowSelection`: D227.
- D157's rejection of *merging with nobody* vs the shipped `merge-automatic` and `integrated-no-press`: the code.
- D87, D145 point 1 and D164 amendment point 2 (a landing rule's method, `autoAccept`, pattern) vs the bound `land`: D227.
- D88 (without `tidy` the tree stays) vs `BranchTidying` (the look's own tidy): the code.
- D215 point 2 (a rule's `integrate` policy) vs `linePolicies`: D227's 1a note; point 5 vs the `integrate` kind: the code.
- D221's rejection of a choice through `PLACE_APPLY` vs `PlacePlan` choosing through `WORKFLOW_USE`: D227.
- D157 and D223 (no zoom) vs D224 (80 to 200% edited, 50 to 200% shown): D224.
- D186 point 3 (left to right where it fits) vs D221 and D223 (top to bottom): each for its flows.
- `NodeKinds.Check` (started by nothing) vs `CheckNode` (its door on the heavy executor): the code.

## Open

- **THROUGH1** (D277): a direct ask taken through to its pull request on the person's quoted words.
- **APPROVE1y**: the work workspace's API token on its git identity, the person's act.
- **CHATFLOW1**, **SETUPUX1x**, **NODEKIT1**, **FABRIC-SQUASHCHAIN1**, **FABRIC-MILESTONE1c**: the installed look.
- **WORKSETUP1**: the first place composing a pull request end to end; its tail AUTOTIDY1c, SQUASHTIDY1d, PLUGHOOK1d, FABRIC-PERFORM1l.
- **INTEGRATE1 k, m**: this repository's integrator flipped and a sibling's declared integration; h's installed proof.
- **FABRIC-PIPELINE1 g, h**: this repository's pipeline on the install, a stage after the check.
- **WORKFLOWEDIT1c**, **LANDNODE1j**, **REVIEWFLOW1c**, **LANDPRESS1**: the installed window.
- **PROCSTEPS1** (`docs/2026-11-09-process-steps-design.md`): `resolve`'s loop resolvers, `merge`, `tidy`'s removals, the cut-over.
