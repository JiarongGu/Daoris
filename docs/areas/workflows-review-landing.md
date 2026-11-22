# Workflows, review and landing — how it works now

## What it does

A workflow says how a repository's work moves from an agent's done to the line. It is a saved graph of steps of fixed kinds:
holds, a second opinion, the person's look, the person's Accept (`review`), one landing (`land`), and what follows it (a pushed
branch, a pull request, integration, a release). Each place (this machine, a workspace, a repository) follows one saved workflow;
a fresh home follows a shipped copy of *Merge after you accept*, whose Accept follows the risk policy and passes where nothing gates it. A chain's work in one repository is a run, bound at its first
start to one version. The node runtime in the driver loop evaluates the run's graph and performs the `land` itself once the gates
hold: a merge into the line, a branch for the person, a plugin's push and pull request, or the line's integration. Work no saved
workflow governs is held, and nothing lands it.

## Its parts

Paths are under `src/Daoris.Desktop/Daoris.Desktop.Driver/` unless they name another root, each type in a file of its name;
`docs/index/README.md` says where a seam, route, verb or fixture is, and `docs/index/registered.md` lists the kinds in order.

| Where | Its job |
|---|---|
| `Defaults/workflows/`, `Workflows/` | The shipped graphs and presets, the saved files (`WorkflowStore`, whose list skips the home's own files kept beside them, `casts.json` and `quests.json`), the bind (`WorkflowRunStart`), the gate interface (`WorkflowProcesses`), the palette, chat and verification runs, `daoris workflow` |
| `Workflows/Domain/` | Choosing (`WorkflowSelection`), a version read and its digest (`WorkflowNamed`), a run derived from its records, the graph's gate (`GraphGate`) |
| `Workflows/Kinds/<Kind>/` | Each step kind whole: its declaration (`<Kind>Node`), options, save rules, reading and door (rule 49) |
| `Workflows/Deployables/` | A repository's declared deployables (`DeployableDeclaration`), each keyed to its release nodes (`DeployableTargets`), and their read (`DeployablesCommand`) (rule 3b) |
| `Workflows/Acts/` | A role's acts: the receipts a step maps and the knowledge it names (`StepReceipts`), the session's rounds and the run's journal (`StepActFiles`), the rows the run's view draws (`StepActViews`), the long loop's proposals (`StepLessons`), a repository's values moved into its delivery knowledge (`ValuesMoves`), the pass that journals and settles by them (`StepActs`), and testing a quest's parts together (`JointChecks`, `JointReturns`) |
| `Workflows/Cast/` | Who holds a step: each place's cast of every role (`PlaceCastBook`, `PlaceCastFiles`), a step's tenures (`AttemptTenures`, `StepHolding`), the one door that settles a step by its holder (`SettleDoor`), and the `daoris settings cast` verb (`PlaceCastVerb`), which the settings reach only through the answer it installs (`PlaceCastLease`) |
| `Workflows/Nodes/` | The runtime: one pass (`NodeRuntime`), what it governs, judges, takes in turn, tells the plugins and keeps; the performer (`WorkPerformer`), the wires, the graph rules (`NodeContract`, `NodeGraphs`), the person's waits |
| `Workplace/` | The landing performer (`LandPerformer`), a tree's landing (`TreeLands`, `LandingPress`), deliveries, landed branches, pull requests, integration, entry landings, the together press, tree hand-offs |
| `Review/` | The look and the opinion (`Review/Domain/`), entry Accepts, the session-tree gates, the heavy executor |
| `Setup/`, `Settings/LinePolicyKey.cs` | *Set up…*'s plan and Apply; the line policies |
| modules `Bridge/Routes/` | `WORKFLOW_*`, `LANDING`, `LAND_ENTRY`, `RUN_REVIEW*`, the set-up and tree doors; the Accept's waits and answers |
| web `src/workflow/`, `src/work/review/run/`, `src/agents/setup/` | The chart, the editor, the place page, the run view and the review page |

## Its rules

**Choosing and saving**

1. A place follows a saved graph workflow. A fresh home saves a copy of `merge-after-accept` as `defaultWorkflow`, once; a home
   that has a `driver.json` is never seeded and converts on the person's press. (D227)
2. The workflow a chain's work follows is the first of: the task's choice, the repository's for the kind, its default, the
   workspace's for the kind, its default, the home default. With none, its run is bound to no workflow (`current`) and held.
   An open quest has no run bound yet: its run reads "not started yet: waits to be taken" naming the workflow its start will bind
   and the level that chose it (`WorkflowNotStarted`), and says "no workflow is chosen" only where the choice is none.
   A kind's paths hold its work where the kind chose a workflow that asks less of the person or runs fewer checks than the one it
   would follow without the kind (`KindLowering`: the person's part, then a check dropped or its gates narrowed): a change outside
   them holds until `daoris workflow keep`. Nothing infers a kind from paths. (D157, D227, D232)
3. A repository naming any development workflow replaces its workspace's whole. No door writes `current` as a choice; a file
   holding it is still read. (D157, D227)
3a. A repository's own values for a workflow it follows are options over it, never a copy: `driver.json`'s `workflowValues`, by
   repository, workflow id and node id, each option's JSON value (`WorkflowValuesKey`), laid over that node's options (`NodeValues`)
   wherever a run of the repository bound to that workflow is governed, so a step's attempts carry them; the graph, its digest and the
   binding stay the version's, and naming values names no workflow. One door writes them, the window's `SET_WORKFLOW_VALUES` and its
   twin `daoris workflow values <repository> <workflow> <node> <name>=<json>…|--clear --why <reason>`, both through
   `WorkflowValuesKey.Apply` (the terminal twin is routed to `WorkflowConsole` from the tree, as `take`, `hand` and `give-back` are), which
   refuses a value that is no JSON or a change with no reason and records the reason beside the values; values are read by node id, so
   `workflow apply` says each declared entry its new version holds no node for and, for each new node of the same plugin that takes
   none, prints the `workflow values …` line that carries it, never writing one itself (`WorkflowApplyNotes`), except values whose
   node the new version replaced by a step naming a delivery knowledge entry, which it says move into that entry (rule 3e)
   (`workflowValuesWhy`, by `repository/workflow/node`); the release manager's holder sets a release target there from what a `no-target`
   or `target-changed` failure listed, never by editing the file: on the run's step failed so, the window's form (`ReleaseStepDoors`)
   asks the plugin's `discover-releases`, the holder picks a definition (with its pipeline and trigger branches), a stage and an action
   with a reason, and the door sets the choice on every release step of the workflow. `SettingDoorsCensusTests` holds every key of the driver's file a step
   reads to a setting of the window's and its verb. (D288, RELEASEVALUES1)
3c. A release target keeps to its route (ACTPIN1, D307): the Azure DevOps plugin reads each release definition's continuous-deployment
   trigger at the act, and the branch the merged build ran on routes to the definitions whose trigger takes it (`routes.mjs`). A failure
   for its target names that route as the choice (one definition, or several for the release manager's line to choose between); an act
   on a declared definition whose own trigger leaves the build's branch out (a feature branch's build on the main line's definition) is
   refused `target-changed` before anything is made or started; a definition released by no trigger is the declaration's choice. The
   approval's card, on the step in the run's view, in the session page's dock and in its *What needs you* row, names the release, its
   definition, the build and its source branch (APPROVEHERE1, APPROVEPRESS1, APPROVEPRESS2): where the plugin declares
   `approve-release` and the person holds the approver role (the step's holder, never the command's `tool` flag), *Approve* and *Reject*
   on the card run it through the window's plugin command door under their own sign-in; otherwise *Open* takes the person to the system's
   page in Daoris's browser, and the step settles on the approval observed. No release step approves, whatever its action; where the place
   casts the approver to an agent the approval is that agent's tool (rule 3e, APPROVEACT1).
3d. The release is the run's own steps where a version names them (RELEASESTEPS1): the plugin's `release-target`, `release-create`,
   `release-start`, `release-approve` and `release-watch`, one per fact. Each act reads its fact first, whoever made it, so a fact made
   while the step was only ready settles it at once, credited to the identity Azure DevOps names (`outside: <name>`, or Daoris), kept as
   the evidence's `by` beside its words; the wire reads it (`WorkEvidence.By`) and the attempt its outcome settles keeps it on the unit's
   record (`by`, which an older build passes over and drops where it writes the record again). Create matches a release by the landed commit its artifact carries, across every build of that
   commit, or reads the release its holder names (`release` option), held to the step's definition and taken where its commit is the
   landed one or descends from it (the landed commit is their merge base), else refused `target-changed`, naming the definition or the
   commit it carries; a link naming no release is refused `no-release`. It waits on the release manager while the build runs and continuous deployment has `findSeconds` from the build's
   end, with no give-up but the step's deadline, then makes the release once under `create-and-start` alone, its description carrying the effect
   key (definition and commit), so a lost answer or a restart finds it; under `start-stage` it waits for the release CD makes
   (`build <n> / CD`) and under `observe` for its holder's, to the step's deadline, never making one. Start starts only where the action
   says, once (a start Azure DevOps shows late is waited for, and a refused start that reads started is taken), else waits on the
   deployer; every step reads the kit's stand-down signal before each write and in each wait, so nothing is made after its attempt
   stands down; approve never approves (the approval is the approver role's act, the plugin's `approve-release`, rule 3e); a key or
   sign-in refused waits on the credentials keeper with its token page until the deadline, never failing for access (ACCESSSTEP1); at the
   deadline the driver climbs the reporting line instead of taking the `timeout` (`AccessWaits`, in the settle path): sign-ins are an
   owner-class kind, so the wait passes every level to the owner role and the attempt is held there (`access-climbed`) with the token
   page, its step still under way until its holder's word. Saved
   versions keep the bundled `release` step, and versions naming the five steps keep them, the reader D325 names with its end.
3e. A role's acts are its holder's tools, settled by their receipts (D325). A `task` step's `receipts` option maps each evidence name to
   the fact a tool's receipt names, and its `knowledge` option names the repository's delivery knowledge entry holding its procedure
   (`StepReceipts`); its hand-off names that entry, its acts and their facts in place of `step_settle`. This repository's own
   example of such knowledge is its verification rule (D349): `daoris.gates.json`'s `verifyBudget` and
   `.claude/knowledge/verification-budget.md`, kept by its merge tool until a supervisor actor keeps it (WORKSPACERULES1). Each round of the run's working
   session's plugin tools that answered a receipt is kept for the session by the tools server (`StepActFiles`, whatever its exit), and
   the pass at the look's deadline journals it once on the run (`workflows/acts/<key>.json`) against the agent-held step waiting whose receipts take its fact,
   with its attempt and epoch, where the round came after the step waited (`StepActs`, before the hand-off in `HandStepsAsync`); a round no
   such step takes is journaled nowhere. Once the newest receipt of each mapped fact is reached the pass settles the step done at the
   settle door as its holder, "by its receipts" with them as evidence, and `step_settle` refuses such a step (`by-receipt`). The approval
   is the approver role's act (APPROVEACT1): a plugin's step waiting on its own role's act whose `receipts` map it (`release-approve`,
   `{"approval":"approved"}`) is handed, where an agent holds the role, to the working session like a task (`AgentSettles.Handed`, its
   plugin's words and page in the hand-off, *Take* alone the person's press), its act `approve-release` reaching only a session at a place
   that casts the approver to an agent (plugins rule 29a), and its reached receipt, the plugin's own reading after the act, is applied as
   the attempt's reached outcome credited as the receipt says; a destination the place marks keeps the approver the person's, so nothing
   is handed and no agent's receipt settles it. The run's
   view draws one row per act journaled on the step's attempt under it (`StepActViews`, the step's `journal`): the fact, its state, its
   words, credit and page, and "waiting on `<role>`: <holder>" where a receipt waits on another role. A holder that cannot take an act
   asks the role that can through `question_ask` (the hand-off says so); the request a run's working session asked as it ended its turn
   awaiting its step is kept for its answer (`QuestionFiles.Settle`) and drawn in its dock, and the answer, the person's sign-in Daoris's
   browser sees (`SignInAnswers`) or their words, is its next turn through the say door, its retry's receipt settling the step with no
   one relaying it. The long loop: a step its receipts settle whose act reached only after a receipt that did not (failed, or waiting on
   another role) proposes what it came to for the knowledge entry it names (`StepLessons`, `workflows/acts/proposed.json`), carried
   once by the next run's hand-off of a step naming that entry in that repository, whose holder amends the entry in its own change. A
   repository's values the newest version of its workflow reads no more move the same way (`ValuesMoves`, D325 point 5): where that version
   holds a step naming an entry (of the dropped node's role, else the one entry it names), the pass at the look's deadline proposes the
   entry each value, whatever door saved the version, carried by every hand-off of a step naming it until the entry in the repository's
   checkout holds each value as a word of its own; then each node's values are cleared from `workflowValues` with that reason, save a
   node's that a run not ended is bound to a version holding (its fixed release steps) and a node's set anew since. The approval node's
   values, which the actor form still reads, never move: they are the reader D325 point 7 keeps. The shipped
   `pull-request-release` preset, once its Azure DevOps pull request merges, holds the release manager's `release` step (procedure
   `release-path`; receipts `release-created`, `stage-started`, `stage-ended`) beside the approver's `release-approve`, both before the
   after-release steps (CONFIGSTEP1): `config-needed`, a decision the release manager's holder proposes (or the work's session from its
   hand-back) and the owner confirms at a marked destination, `config-change` where it is needed, waiting on the config changer, whose
   *Done* note gives the new version's id and a capture's link, and `check-release` after either, held by operations; "not needed" passes
   `config-change` on the record naming both holders. Each step after the merge declares its role (`role`: the release manager for the
   release and the decision; the approver for approve; operations for the check; the config changer for the change), and the chart draws
   the decision's `not-needed` route as *Not needed this run*, never as *No further step*.
3f. One quest across repositories is its ask's plan (D325, ACTORFLOW1d): each part a quest in its own repository whose run its own
   roles hold and whose steps name its own delivery knowledge; no part's holder changes another repository. A part waits on another's
   receipt by the plan's `after` edge naming a `receipt` (`PartWait`, read into the look's `PartsReport` with each part's quest): until
   the run of the part it follows has the newest receipt of that fact reached, none of its steps is handed (`PartReceipts`). Testing
   together is a `task` step whose `joint` option maps each part to the fact its receipt gives, held by the tester or operations role
   before the marked destination (`JointChecks`): handed once every part's newest receipt of its fact is reached, again whenever a part
   acts after the last test, and settled by its own receipts only on a test newer than them all, the parts' receipts its evidence beside
   its own. A failed test naming its part (the receipt's `part`) goes back once (`JointReturns`): the request said to that part's working
   session as the tester's, a row journaled on the part's step (`returned:<id>`, naming the session asked), that session's later rounds
   of the fact journaled on the same step as its fix whether the part's run still stands or ended, and a reached fix proposed into the
   step's delivery knowledge (`StepLessons`). The look's one read of the run bindings (`AskRunReads`) gives every open run's ask, whose
   plan it reads for a part's run whether or not the train holds a request for it. The run's one reading reads its ask's plan and the
   parts' journals as the pass does (`PartViews`): its graph's `partWaits`, each other part's receipt it still waits on with that part's
   quest and its newest state, and on a joint check's step its `joint`, each part's receipt held or waited for and the part its newest
   failed test went back to once that return is journaled. The run's view draws them, "waits on `<part>`'s `<fact>`" under its graph's
   lines and "holds" or "waits for" under the check (`workflow/parts/PartRows`), each with the other part's quest as a door, and the
   session page's dock draws the same while its session awaits its step (desktop-app rule 38). No shipped preset holds a joint check:
   no team preset releases (`team-part` lands a part, `ask-delivery` the ask's branch; `pull-request-release` is one repository's), and
   `joint` names the ask's own parts and the fact each gives, which only its plan knows.
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
   every purpose it serves (`DeclarationAcceptance`: the integration's blob and the landed route's gates). Whether it waits for the
   person is the repository's access mode alone (`DeclarationMode`, asked by the check, the line's acceptance, the held checks' rerun
   and the window): full where its permission prompts resolve `unrestricted`, at whichever level, restricted otherwise. In full access
   it is accepted as it lands (the look says the checks changed in one sentence ending `(full access: taken without asking)`, and
   *What needs you* holds no row); in restricted access by one press (`INTEGRATION_GATES`, or `daoris integrate <repository>
   --accept|--withdraw [--plan]`), Setup saying the mode that decided, shown as checks added, removed and changed by name with their commands
   (`DeclarationChange`), the row ending at the press; its row in *What needs you* names the checks added, removed and changed and
   opens that press on Setup. A landed change that adds or alters no command keeps the acceptance as it lands, said in one sentence
   naming what it set aside (`IntegrationOwed.Kept`): two declarations run the same where they are equal once their inert parts are set
   aside (`DeclarationCommands`, read strictly, so a member written twice, a comment or a trailing comma waits: every member named with a
   leading `_` at the top or directly inside a check; each check's `paths`, `timeoutMinutes`, `timeoutWhy` and `memoryGb`; the top-level
   `tests`, `tiers` and `docs`), and a check's name, command, folder, hand-back or flags, a check added, removed or moved, and any other
   part wait for the person; the change shown names command changes alone. A change the look cannot judge waits and is said once, and
   never ends the look. In full access a check runs the work's own declaration as its commit carries it, a command change of its own
   included, as the work's agents run any command in their own sessions anyway, also before any acceptance (said in one line); its result
   names that blob and nothing holds. The workspace its mode resolves through is the work's bound one. A driver file that does not
   read is restricted. In
   restricted access a check runs under the accepted declaration, read from its blob, never the work's file (`CheckDeclaration`): a
   work whose declaration runs only what the accepted one runs, or whose own change against where it left the line (the merge base with
   the local branch of the line the look reads) alters no command, is checked as accepted, so a declaration change on the line holds no
   run; only a work whose own change alters a command, or one whose line or merge base git cannot name, holds `changed`, saying which,
   that restricted access decided, and what the person can do (land the change and accept it, or answer the check *It happened*).
   Daoris starts only the commands the person accepted; they run the work's own code. Once the acceptance moves, or the repository takes
   full access, each check so held is run again by itself (`ChangedChecks`, its held attempt's *Run again*). The set-up review
   offers the same one acceptance, a single tick per repository saying the checks changed (`daoris place … --apply --accept
   <repository>`), never one tick per purpose. (D283, D350, D353)
5. Saving (`WORKFLOW_SAVE`) and choosing (`WORKFLOW_USE`) are two presses; *Set up…* alone does both, in one receipt-kept Apply. A
   choice is refused (`place-kind`), nothing written, where a department it reaches cannot run one of its steps by its place's
   kind: the git kinds (`land`, `push`, `monitor`, `tidy`, `integrate`, `line`, `content.holds`, `rebase`, `resolve`, `merge`, a
   pull request's Daoris half) run in a repository alone, `isolate` in a repository or a folder, `return` in a folder, `apply` in a
   system, the rest anywhere. *Work in place* (`in-place`) holds no git kind, and the window's add chooses it for a folder, a
   system and a repository the person ticks (`daoris place <department> --preset in-place --apply`). The *A pull request* preset's
   saved copy names the plugin `SetupPresets.PluginFor` finds for the remote's platform in place of the default's own step, and keeps
   the branch pattern the place's followed workflow writes; one plan reads one set of workflows, so repositories that want the same
   copy share the one saved and each that differs gets its own id. (D221, D227, D264, D265)
6. A workflow is one file, `<home>/workflows/<id>.json`, on this machine. A version is whole and never edited; a save appends the
   next number. The newest 20 and every version a kept run names are kept. (D157)
7. Only graphs are saved, and only a graph version binds and runs. The one save judge refuses steps (`steps-refused`), a `land`
   with no method (`land-method`), an `accept` option (`accept`), and a kind, port or outcome declared arriving
   (`arriving-kind`). (D227, D232) `workflow new <id> --from <default>|<id>[@<version>]` saves a copy: a saved graph version
   (the newest where none is named) is copied whole under the new id, and a version of steps, an unkept version or an id already
   saved is named and writes nothing.
8. A graph holds at most 24 authored nodes, 96 wires and one `land`. The steps writing the work (`land`, `return`, `apply`) come
   after the `review` where there is one, on every way (`review-order`), each fed the last review's record on its way
   (`review-ambiguous` where two feed one). A graph ends at any node no such step lies past (`effect-past-end`); a development
   graph with no `land` holds a `claimed`; holds judge the work before the landings, or before the reviews where nothing lands.
   There is no `handed` kind. (D190, D227, D232, D264, D273)
9. The opinion, the look and the `review` come before every landing, wired straight into it, and no landing starts before a look
   has passed; the opinion need not precede the look (`ProtectedOrder`). Each hold (`quest.holds`, `content.holds`,
   `work.requests`) is judged once, on every way to a landing. An ask's quest with no requirement has a done checked against
   nothing, which waits only where the configuration names who reads it: where the step names no role and the place casts no
   `ask-reader` and is not at high risk (read by the pass, `CastReading.Now`), it reaches, said once on the step ("no one is set to
   read this done against its ask, so it goes on", code `no-reader`); else it waits *unchecked* (`pass`, on the step's role, the
   `ask-reader` where it names none) until that role's holder passes it with words: the attempt's tenure as the place casts the role,
   the line standing in where the person holds it, the owner where the step names `owner`; where the person holds it, its *What needs
   you* row and session page offer the run's skip with words, and their Accept naming the unchecked hand-back takes it from an agent
   holder in the same press (`pressed accept`). A pass from one of the run's working sessions (`NodeRuntime.Workers`), or from a working
   session while those are not read, is refused `own-work` (rule 49); the role is never cast or handed to the bare `agent`. The person's through
   words on its ask pass it (rule 23), or a version turns its `yes` off; it is never compared with the ask's words instead (D352); a
   set-up quest is Daoris's own ask, whose press reads the repository it set up, so it never waits so. It reads the run's branch first: where the line holds that branch's tip (any proof but
   *empty*), it reaches saying the branch was merged outside the run, and the steps after it reach on the branch's held fact, so the
   run ends landed without waiting on an earlier hold (`BranchOnLine`). (D157, D190, D247, D255, D277, D352)
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
14. The shipped defaults hold no `quest.close`: a run's quest is bound to its run, its agent's done is a submission `claimed` and
    `quest.holds` read as its done, and the run's end writes its Done (driving rule 2; D293). A Done anyone else writes on a quest bound to
    its run (`run:<run>`), the person's `quest done` or any session's, while the run holds no end and its chain names no step still to come here,
    ends the run as a stop does (`QuestDoneRuns`): at its next look, before its rounds, Daoris stops every live or failed step, the attempt in
    flight on each stood down and its runner told to stop (a check's executor too), and runs none of its rounds, so no step, a reached one
    whose branch moved included, is bound or started after the done; a promoted `integrate` stays the line's integration's; the run ends `stopped`
    by Daoris at the step it stood at (`quest-done`), its waiting sessions with it and a session still in its turn once it comes to wait; its
    tree and branch are kept as the clean-up keeps work the line lacks, and the end's line names the commits its branch holds that the line does
    not (the look's `Lacks`, read at the pass's tip), so what was left is said and never landed by a run nobody owns. A quest bound to no run
    closes by its agent's done and its run goes on. That end ends every session of this machine
    awaiting a step whose note names the run (`RunWaiters`), found again at each write, the owed one asked again included; a run already at its end that a session of this machine still waits on is governed at each look while one waits, the first after a start included, for its end alone: nothing else on it is judged, and the same end ends that session again (`RunReEnds`), whatever answered its first. A submission held for a departure
    or its evidence takes the person's one accept while its quest stays Taken (`daoris quest accept`, *Accept the departure* in What needs
    you), and `quest.holds` reaches on it; `quest.holds` reads a departure as the yes's hold (its `yes` option), never its evidence's
    verdict, and a refused yes names what waits (DEPARTLOCK1). Whose yes a departure waits for is the risk policy's at the quest's place
    (`QuestDepartures`, D354): a quest held as `Departed` has the act `work.done`, or `direction.set` where its done departs from every requirement
    (distinct requirement ids, never answer rows; a quest with no requirements is `work.done`), so its target moved.
    Where the judge passes it the look (`departures`, after `through-notices`) posts the policy's yes to the service's accept door, its author
    `policy:<act> · rule <rule> at <revision>` (`RiskPassWire`, whose act is one of those two and whose rule and revision are one line each, refused
    otherwise by the door), journals one `risk.judged` line and says it, a refusal of the service said once per quest and revision and asked again at each look, so with nothing configured a
    departure goes on with nobody asked; where it gates, `quest.holds` names the role whose yes it waits for, and where it classes the act
    `owner`, or no policy is read, the person's yes and press as before. The service keeps the yes and judges nothing; like a role's, the
    policy's yes takes a departure alone, so evidence still awaited beside it waits on its check. A held quest is
    closed only where the person passed over the run's `quest.holds` with words, which is their yes to a departure or its evidence, kept on
    the quest through the one accept before the close (`RunEndQuests`); any other held quest is left as the person left it, and the
    sessions still end. An end that closes and ends nothing, a run a session waits on that a look skips anyway, and session records that do not read each say why once in the machine log (`run.end.empty`). Every shipped development graph that holds
    `content.holds` checks after it, `team-part` included; `documents` holds no check by design, since its work is prose no gate reaches. The
    check is a `check` with `gates` `reached` between `content` and the review or landing, and a `back` wire from it `on` `failed` to the work, so a red verdict's words go to the work's open session as
    Daoris's word under the step's key and its next commit is checked afresh (D293): a check waits ready at a commit that failed it
    (`CheckRounds.RedAt`) until the work answers, so a red is never asked again before then, except by the person's new attempt; a work that
    answers with no commit, its session waiting on the run's next step since the red was asked and none of its sessions still working
    (`CheckRounds.Answered`), has the same commit asked again as its next round. Each exit of the check's start that leaves it ready (not
    sent, no executor, a red not answered, sessions unread) is said once per change of reason in the machine log (`check.waits`,
    `CheckStartWaits`, the memory `NodeWaits` the landing's `landing.waits` shares). A check's reds are counted from its run's record
    (`CheckRounds.Reds`, each settled failed attempt a round); the way back is taken while they stay under `CheckRounds.Bound` (3), and the
    red that reaches it is not sent back. That red is a left attempt's recovery, the act `work.recover` (D354), its failure kept on the
    left attempt's role, the place's supervisor (`SpentChecks.HeldOn`): a row classing it `role` gives it to that role's holder as the place
    casts it, or to the role's agent where nothing casts it, the words naming that role and holder, and it is held like any agent's wait:
    a holder no live session serves makes it a vacancy (`StepVacancies`, below), and one a live session serves but never answers reaches the
    final exit past the same bound; a row classing it `owner`, or no policy read, gives it to the person; where the policy asks nobody (the
    seed passes it) it reaches the owner's final exit (`FinalExits`) at once: one notice with the rounds, the red classes, what the session
    tried and for how long, one `risk.judged` line, and the person's row in What needs you until the owner looks, never stopped by Daoris and
    never one row per red. Who it went to is kept on its telling (`NodeTold.Decided`, and the role) in the same write, and only then is the
    decision done: its words reach the work's session, once, with its round and the bound (`NodeRunKeeping.SpentRedsAsync`), saying who
    holds it. A decision kept for a holder is judged again at each look (`CheckRounds.SpentOwed`, D354 point 4): a policy lowered or raised
    moves it without telling the session again, and one at the exit stays there. A final exit's row is a `step` row carrying `exit`
    (`StepExit`, its kind `spent`, `vacant` or `unanswered`), held by the person and counted by its own key as its obligation
    (`exit:<run>/<node>#<attempt>`, `FinalExits.Key`), with the presses that answer it, never the steward's; it is the exit's one
    interruption, announced once under that key (`WaitAnnouncements.ExitKind`: a toast in a window in front, the OS balloon behind it),
    naming the run, the step, the story's short form by its kind and the one answer, so a vacancy said before it is said again as the exit.
    Its record is a notice of Done and failed under the same key (`LookSaid.FinalExit` news naming the run and the step, whose *Open* opens
    the run), which no window says aloud: the whole story is the look's line, said once as the exit is kept (`NodeRuntimePass.Noticed`), and
    the notice is kept again at each look while the exit stands (`NodeRuntimePass.Standing`, `LookSaid.Stands`), so a look a later failure
    ended or a refused write loses none, and one kept, or dismissed, is never written again, across a restart too: the memory of both the
    announcement and the notice is the home's (`News.ClaimStanding`, the `standing:` keys of `news_said`, outliving the one-week horizon and
    forgotten by `News.Forget` only for an exit this process saw stand and stop standing, never for a reading taken before the runtime loaded its steps). A step at the exit is one row: its wait says so (`PersonWait.Exit`), so
    the session's `left` row is not made beside the step's own (the flags `exit` and `ended` pass the groups route and `GroupsStateSlice`'s allow-list). Each telling of the work what a step sent back is kept on the run's record (`NodeTold`: the session told, or why none was), and a
    failed check's step says where its words went from it (`WorkflowRunTold`), never that they went where none was told. A reached
    command's words name each red class first, with the lines its log failed it on within the excerpt's bounds, then the classes run, the
    flakes and main's reds (`ReachedVerdict.Told`); a failed gate that prints the outcome line is told by them, any other by its log's red
    blocks and last lines (`GateLogExcerpt`).

**Runs**

15. A run is a chain's work in one repository, bound at its first start on this machine and written once. Its id is the chain's
    first quest in that repository. (D157) An ask's delivery run is the one other keyed run, bound to the ask's branch and not to a chain
    (rule 39b).
16. One engine: a run bound to a graph version is the node runtime's. A run bound to anything else is held, its problem said.
    (D190, D227, D232)
17. A graph run is the unit `run:<run>` of its own node record. `claimed` observes the chain's quests and carries the work on
    `work`; a `review` binds its review record (rule 22); the gates, landing and plugin nodes bind the run's branch at its tip, the
    one its tree moved onto once the look learned it; a `go-ahead` binds the run's ask. Once its landing has reached, the steps bound to
    the landed branch keep it at the revision each was bound at, whatever branch stands as the run's after (an earlier attempt's once the
    landed one is tidied away) and wherever the landed branch moves that no landing took it: its session going on after the landing, on
    the person's words, is its turn, and what it commits then, or its tree brought to the line the work merged into, is new work for the
    chain's next quest here, never the landed run's (D319); a tip a landing of its own stands at (an advance its record keeps) is
    followed. So only its chain coming back here or the person's word takes a landed run back (`LandedBranchKept`,
    `LandedRunGoesOnTests`). A branch is the work of the quest its
    session served, never of another repository's quest. A branch several
    runs made is each run's, one shared with Current's work held, said; a plugin's event on it goes to the runs awaiting it at that commit, else to those
    whose graph names the step. A node fed on a `data` wire binds that value, so a moved input rebinds only what it feeds; a
    target's tip, once read, rebinds only while nothing fed is active or reached. A node only a skipped producer feeds is skipped
    with it. An unbound step after a review, its tip unread, binds the commit the review accepted. A step's commits stay on
    `refs/daoris/steps/<run>/<node>/<attempt>` until the run ends settled. (D190, D259, D260)
18. The runtime is the record's one writer: a door keeps the person's word (skip, stop, send, ask again, go on, accept,
    changes, done, approve, reject, decide with its outcome and, for a proposal, its holder) and the next pass applies it; no run ends
    while a word waits, and a decision's answer may change until the run ends. A failed plugin step's run step
    holds its failed attempt for the person (`Held`, `PluginFailureHeld`) as a check's does, and its *Try again* is that attempt's
    `again` (`WORK_HELD`, `daoris work held <unit> <node> <attempt> again`, which goes through the running Daoris's `WORK_HELD` as `workflow skip` does and records alone only where no instance answers), the one press What needs you makes too: the table's
    failed to ready by the person, a new attempt its plugin is sent. *Done outside Daoris* (`done-outside`, the same door and verb with
    `<link> ["…"]`, the link required) settles a plugin's step under way, held or failed on its holder's word (`active`, `unknown` or
    `failed → reached` by the person, the link its evidence and the note its words), a step whose kind carries its proof as evidence
    included, since the link is that proof; it is kept on the attempt as `happened` with its `link`, a word an older build reads. Before
    the word is applied, a reconciling plugin's step hands the link to its plugin's check (`WorkLinkChecks`: `work/reconcile` with `link`
    and the step's options, the kit's `check()` given `attempt.link`), which reads the thing named and never acts: a refusal takes the word
    back with the check's code and words (a release of another commit, `target-changed`, naming the commit it carries; a stage not started,
    `not-yet`), a settled link stays the holder's word credited to the identity the check read (`by`), and an answer naming no link (a kit
    that reads none) leaves the word its holder's; either answer marks the attempt checked (`linkChecked`, a field an older build passes
    over). A link is asked only while its word still waits (its attempt in flight or its failure kept, unchecked), so an applied one spends
    no send. A link the check has not answered (its plugin down, the pass's sends spent, an error) is held, never applied on the word
    alone; past the step's timeout from the word the wait climbs the reporting line (`LinkHolds`, `link-climbed`) and keeps holding. The
    plugin reads a link at the checkout its unit was last told, so a failed step, told to no plugin, is still read. *Done outside Daoris*
    is not offered on a step that declares outputs, which no word fills, and a word the table refuses keeps its refusal on the attempt. Skip
    (*Not needed this run*), stop (*End this work*) and done outside each stand the plugin's attempt in flight down (`StandDowns`): it
    settles `stood-down` with the act as its code, never a state of its own, its node's gate lifts so nothing holds the run, its plugin is
    told (`work/stand-down`, a notification), and its later outcome is not taken. The kit writes the stand-down in its journal, so no
    later delivery of the key acts, and aborts the act's `signal`; a kit with no `check()` makes afresh an act a gone process held. A skip a proof-carrying step refuses names the
    done-outside press, and ending the work leaves its quest to close by the person's word. A performed attempt's `progress` (`work/step` bound to its
    invocation, the one non-outcome state) is kept on the attempt, the next replacing it, and settles nothing; where it names an actor
    the step reads `waiting` (`StepWaiting`): its wait names the actor, the holder filling it, the plugin's words and the page, one
    rendering whoever holds it (D303). A step names its actor, never who fills it: the holder is its place's cast (`PlaceCastBook.At`, kept in `workflows/casts.json` by
    `PlaceCastFiles`: one holder per role, the workspace's and a repository's own over it, each choice journaled with its reason, the
    newest 500 acts of each kind kept apart (the owner's casts, the presses on steps). The file is read entry by entry: an act that does not
    read is skipped, counted and kept as written, never the casts dropped for it; casts that do not read keep the last good read, refuse
    every write (`unreadable-casts`) and are said in the machine log and in the cast's Settings; a rewrite keeps a holder word and a field
    it did not read. The
    owner role, an approver at a destination marked as the person's and every role at high risk stay the person's, the high-risk branch
    standing until RISKGATE1j retires `placeRisk`), the keys' keeper (`credentials-keeper`) the person's where no cast names another, a
    sign-in being their act in a browser (`sign-in`, D354 point 1), and every other role no cast names its agent's (`default`, *the role's
    agent fills it until a choice names another*): the person holds a role only by their own cast, a mark, the owner role or a high-risk
    place, never by default (D354 point 4). A role its agent holds only by default hands no plugin's role act (`CastReading.AgentHolds`),
    and a wait with no cast in hand reads the same rule (`PlaceCastBook.Uncast`). `daoris settings cast` and the workspace's and repository's Settings set it, only by the person, and neither casts
    an agent to the owner role (`AgentSettles.Roles`); an agent settles a step by an observation or a plugin's act, the work and the second
    opinion by its session, and a task's Done, a decision, an Accept and an approval by a session's tool, `step_settle` (D318, D354);
    through words take no step another holder holds. Each holder's tenure
    of an attempt, `{role, holder, epoch, since, why}`, is kept apart from the node record, in `workflows/tenures/` (`StepTenureFiles`),
    which no older build reads: a pass writes each standing attempt's first epoch before its rounds and after them (`AttemptTenures`, the
    person where the step's own words say `you`, else the cast's holder, `AttemptTenures.FirstHolder`), and a unit's file drops only ended
    attempts' tenures past its cap, never a standing one's. *Take*, *Pass to…* and *Give back* (`StepHolding`, `daoris workflow
    take|hand|give-back`, `WORKFLOW_HOLD` with the `epoch` its row read, shown on the run view's step and on its *What needs you* row) each
    begin a new epoch and journal it, written only where the epoch read still holds the step (else `ended-epoch`), and a hand-over restarts
    nothing; an agent hands on only a step it holds and never takes one, the owner role is handed to nobody but the person (`owner-role`),
    and *Give back* returns a step to its first holder. One door settles a step
    (`SettleDoor.Admit`, D312), asked by a run's word and an attempt's alike and reading the tenure, the speaker (`SettleBy`, its
    channel's and never the payload's: the window, a card the person confirmed and the person's terminal speak `person`; a working session's
    own press, run at once or allowed by its supervisor, speaks as the agent its start ran (`agent:<name>`, the roster's name for its door,
    `ProviderRegistry.AgentNameOf`, kept beside its delegate key by `SessionDelegates`), so a step cast to that agent takes its word and one
    cast to another refuses it; an appointed role's act speaks the unnamed `agent` and Ask Daoris's `agent:ask-daoris`, `SettleBy.OfChannel`)
    and the epoch it read (every settling door
    takes `--epoch`, `daoris work held` among them, the run view's and *What needs you*'s presses pass the epoch their row read,
    `HeldStep.Epoch`, and an entry's Accept each part's, drawn on its plan beside the revision), never the step's kind, judged and kept in
    one hold of the tenures' lock, so no hand-over lands between the read and the word's write; a kept word names the epoch it was admitted
    in (`NodeWord.Epoch`, none for an observation or the owner role's), which the pass reads again in one hold of that lock before it
    applies the word, refusing it `ended-epoch` where the step was passed on or tried anew since: a word naming an
    ended epoch is refused `ended-epoch`, one from a holder that does not hold the step now `not-holder`, each naming who holds it and since
    when; a word naming no epoch is judged against the holder now; the person's press on a step another holder holds, in the epoch it read,
    takes the step as the press is kept, once every check of its state admits it (a new epoch, journaled *Take* with the reason
    `pressed <word>`, `StepHolding.TakeOnPress`), and settles it in one act, while an agent's word never takes; an observation settles
    whoever made it; a stop and a decision's answer are the owner role's whoever holds the step and whichever epoch its row read; a
    supervisor's held word stands in for a step the person holds alone, so only such steps are listed to a landing supervisor
    (`LandingSteps`), whose decision keeps the epoch it read and names it at its release; an attempt no tenure holds yet refuses nothing;
    and where the tenures do not read only the person's word is taken and every hold act is refused (`tenure-unread`, said in the driver's
    log). A marked approver is held by the owner role from its start (`marked`): no Take, Pass or Give back, no other word. A run ends when its graph's end settles or
    no node can move, when its quest bound to it was closed Done outside it and Daoris stopped its steps (`quest-done`, rule 14), or when its quest closed and every standing step reads only the quest (its holds, on a done nothing reads any
    more: `quest-closed`, the run stopped by Daoris at that step and its waiting sessions ended `stopped` with it, none reopened), or watches a
    branch the line holds whole (`on-line`) or that is gone (`branch-gone`), never while a step after a reached landing stands
    (a monitor, a plugin's release), since that reads the landing, which a tidied branch and the line's holding the commit leave readable;
    or, with its quest still open, when its work step reached and no branch of it was ever recorded (work done in place, on the line: a
    set-up asked in the checkout): nothing is left for `content.holds` or the `land` to read, so it ends (`in-place`, `InPlaceRunEnds`);
    it is not governed again until its chain comes back. A `line` is reached by the kept end fact, or where the pass's read of the line
    holds every commit of the branch at its tip (a landing that found its work already there made no landing record).
    A check that came back unknown with the code `unrun` (Daoris could not run it) settles with no verdict and is asked again as a new
    attempt, up to three on one binding (`UnrunChecks`), counting as neither a red round nor a pass; its spent asks hold the step for the person.
    A check whose executor finds neither its result nor its job (its process gone: a restart, an update, a crash) is sent once more as the
    same attempt under its key. A send writes a start mark in the attempt's folder (`began-<send>`) when it holds its slots, and an executor
    that ends the job because it is closing itself (an update, a stop of driving) writes `closed-<send>`: a send that never began, or one
    that closed, is sent again each time and counts for nothing; the second send lost with a start mark and no close mark is held for the
    person (`lost`, `LostChecks`) with the last 20 lines of its newest log on the attempt's own `tail` (whole lines, never a broken character;
    the person's note stays theirs), shown on its held row and under its line in `daoris workflow run`, never run a third time by itself and
    never a red round. The held check's *Run again*, applied, also tells the executor to stop the old invocation. A held check holds no slot: the machine's verification and heavy-run
    slots are its process's and went with it. A plugin's outcome sent again for a settled attempt under another plugin id (a rename since) is a replay, not a
    counted refusal. A word names its author; a second on a node is
    refused `worded`, but the person's replaces a supervisor's pending one, and a supervisor's on a lapsed attempt or a dead
    decision is dropped `stale`. The Accept names its review record by attempt and revision, or binds the one standing, and is
    checked as kept and again as applied against the work read afresh, either miss `moved`. `changes` may name the part they go
    to, else the newest. *Stop…* stops the run before the Accept and *Stop what follows…* after it, promising no undo, each step's stop its
    channel's word at the settle door (`ReviewStops`): the person's the owner role's, an agent's only on the steps the door admits it on, and
    refused with the door's word where it stops none. A failed step
    has the same stop, one door for the window, Ask Daoris and the terminal: it ends the step and, nothing else standing, its run as
    `stopped` with the person's words, also where the run had ended failed at that step (the stop governs it again to end it). Its
    *Try again* (`daoris workflow again`, a new attempt; a plugin's held attempt's own where it holds one) and *Done outside Daoris…*
    (`daoris workflow done-outside`, where its kind reads the proof) govern such a run again the same way. A
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

22. The person's Accept is a `review` before the steps writing the work. It waits only where the risk policy gates `work.accept` at the
    run's place (`RiskStepGates`, the judge the pass reads from the look's config, D354): with nothing configured (the seed, `none`) the
    review is reached by the policy (`policy`, its words the verdict's rule and revision) and no one is asked; classed `role` it waits on
    its own role (`acceptor` where it names none) as the place casts it; classed `owner` the owner role holds it from its start, as a marked
    approver is. A `task`'s approval reads `task.approve`, or the act its `act` option names (an approval spending a destination,
    `release.start/production`), alike, reached `approved` where it passes; one naming no role is still the owner role's. An `act` that is no act of the census, spelled exactly (an unknown kind or a case mismatch, a qualifier on any kind but `release.start`, not a string) is refused at the graph's declaration (`RiskStepHolds.BadAct`) and judged `owner` at runtime, so it never fails open. A branch that committed nothing has nothing to accept: its review follows the policy like any other (it passes where the policy gates nothing). A tenure's class is judged again each pass where the step would release (`TenureRejudge`): a raise to `owner` while it waits moves it to the owner role, a lowering from `owner` hands it back to the holder the place casts, and a lowering to a pass reaches it by the policy. A graph with no `land` is work in place and refuses a
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
26. A second opinion is another maker's read-only reading before the landing. Its gate is the bus's first consumer (`opinion-gate`,
    `OpinionGateConsumer`, D356 step 3, `driving` rule 30a), never a look: each opinion fact it is handed (`opinion.owed` as a due is
    kept or the person asks its gate, then `opinion.asked`, `opinion.given`, `opinion.delivered`, `opinion.answered`, and the person's
    word at the gate, `opinion.said`, as *Go on anyway*, *I looked myself* or an Accept's answer is kept) judges that owed opinion at
    once by the gate's rule (`OpinionGateJudge`), reading the opinion's record at the fact for its session and occasion: it delivers,
    rechecks, passes by the policy, or waits, named, one judgment of an owed opinion at a time. A person's ask (*Try again*, *Ask
    again*, the same agent fresh, a named reviewer, *Ask another agent for help*) carries its own occasion, which no owed opinion has,
    so it reaches every owed opinion of its session. The judgments of one repository run one at a time (each runs git's content proof,
    and a round's nine at once queued behind one another and every other git, which held a restart's first look an hour); those of
    several repositories go beside each other, and across them no more than `AtOnce` (4) at a time, so a round of hundreds queues in
    the gate and not in git's places (a census, `BusConsumerSetsCensusTests`, holds that no consumer fans out over a growing set
    unbounded). Every other input its verdict reads is a fact too: the quests' move as the service's change stream names it
    (`quest.moved`: a hold lifted, a later step declined or published), a node a run's record began (`node.begun`, written where the
    record is: a graph's opinion node holds the ask until it begins, and one begun while a judgment read before it waits for that
    judgment, so the next reads it), and the standing choices this process wrote (`choices.written`: a reviewer added to a rule, a
    workflow, the policy). A `quest.moved` fact names what changed, by diffing the quest list the listener reads against the last
    read (`QuestMoves`): the quests whose records differ, every quest of their chains (parents and children, which a step opinion
    reads) and their repositories (which a landing opinion reads whole: every open set-up step there). A move round judges only the
    owed opinions those names reach; a move that names nothing (no earlier read, no difference found, more than a fact lists, a list
    that could not be read) and each node begun and choices written judge every owed opinion. A burst of moves folds: those waiting in
    a row on one subject into the last, whose handling stands for them (the queue tells the consumer each folded entry, so its names
    are kept), and those of several subjects into one round of judgments, a move heard before a round began being judged by it. What it found is the
    gate's view (`LookFacts`), which the look's plan reads for the quests it sits and the attention band's gates and never judges;
    no look stage judges an opinion. Its acknowledgement ends what is owed it: an owed opinion, a word or a move judged, the answers
    judged, and a clean pass, which leaves nothing to deliver. A turn ahead (a cool-off's reset, a reading pass's end) judges it
    again then, a pass or a delivery a drain held a minute on until the drain ends, and a pass or delivery begun beside that ends with
    no fact (a turn in flight, a busy conversation) after a backoff from half a minute to ten; a judgment that throws keeps the turn it
    found set. These live in memory, and a start judges every owed opinion once, which rebuilds the view and its turns, the watchdog's
    timers the net under all of it. The watch, `drive --once` and `drive --until-idle` each start it beside their looks
    (`OpinionGateRun`); a run that must return also hears the change stream for it and drains it before it ends, its queue quiet and
    what the gate began beside ended, within the gate's ten minutes, the watcher looking at the pace meanwhile so `--quiet-end` ends a
    quiet pass, and a run until idle looks again after a drain that judged, ran or moved the plan's view. The author's answers reach it as the go-on reads them at the
    turn's end; the service journals none of its own yet (BUS2b2). Its findings reach the working session once, as its next
    turn, a word into a conversation sent once by its effect key and never again after a crash left it uncertain (the findings then
    the person's, `word-uncertain`), a send that throws read as failed and tried again: a session awaiting its run's step takes them as an ended record does, through the one door and the one go-on, and the graph's
    `opinion` step then waits on that session's answer (`waiting`, its wait `answer` naming the session, held by that agent:
    `OpinionStepWaits`), never *working*. Answers the session gives after its answers were read (a turn a restart cut off is read as it
    ends, before it answered, and the line goes on and answers) are read again before any recheck is asked, once the answering turn
    has ended (a chat run's read by its turn state, as its hand is), the gate standing with the session until then; an answer counts as
    new where its word differs, or a fix's commit does, as the reading keeps them, and the one recheck is judged on them (`OpinionReading.Behind`, OPINIONSTALL1). A pass whose reviewer's session ended without an opinion, or whose minutes ran out first, fails
    the step saying so (`ended`, `out-of-time`), and the next pass that settles reaches it; a gate nobody is free to read is no failure. A
    dispute and commits since that no other agent read are the act `work.accept` at the work's place, which the gate asks the judge
    (`OpinionGateFacts.Risk`, read from the look's config, D354): with nothing configured the seed passes it, the gate `answered` by the
    policy (`policy`, its words the verdict's rule and revision, journaled once as a `risk.judged` pass line on the run and its `opinion`
    step, `PolicyPasses.Gate`, counted only once it appended, one line for each tip it passes at) and the work goes on to its landing:
    the `opinion` step is reached with the pass's line, which its run says under it (`OpinionGateReading.PassedLine`); a step its record
    still holds on the dispute until the runtime's next pass judges it says it passed, with that line and on nobody's answer (its wait
    `passed`), never *working*; judged again at each of its facts and at each new tip a fact brings, so a raise opens it, a lowering releases it and new
    commits are passed again at their own tip (OPINIONPASS1). Classed `role` the step holds on its own role as the place casts it (`role-disputed`,
    `role-commits-since`, read as the Accept's wait, its wait naming that role, never the row's `by`): no agent's tool answers it, so an
    agent holder makes it a vacancy, listed, announced once and at the owner's final exit past its bound, and a person the place casts
    holds it as a row of their own; the window says each by its holder, never as the owner's; classed `owner`, or where no policy is read (`OpinionGateState.OwnerHolds`), the owner's rule stands: a dispute is answered by
    the person's *Reviewed* or *Accept…* given after the answers, findings and any recheck reached them, never before; until then the step
    waits on the person's answer (`disputed`, as commits since that no other agent read wait under `commits-since`, each with its count, held
    by the person; a dispute says whose musts it holds, the first pass's the session did not fix and the recheck's own), never *working*: so
    is every other hold, each a wait of its own saying who holds it and why: a pass still reading (`reading`, `rechecking`, its reviewer's,
    and `reading-until`, `rechecking-until` where its minutes name an end), one nothing has asked (`not-asked` for the driver's next look,
    `not-asked-held` and `not-asked-press` the person's, `not-asked-recheck`), a gate git could not read (`unread`), the chain's later step
    (`waits-chain`), a required one nobody is free to read (`vacant`, `vacant-until` where a cool-off names its reset) and a required one no
    pass could settle for another reason, a provider that kept failing among them (`unavailable`, the person's). The run's view reads the
    gate in the work's tree here wherever its step stands on it (`IWorkflowRunRecords.OpinionAsync`), at the occasion its rule reads at, so
    the wait carries the gate's count and session; a read that failed for any cause but a cancel, or a folder with no git of its own (git
    would answer with the repository around it), is a gate *unread* saying why, and a gate already unread keeps its own reason, never a
    failed view. A pass's seconds are counted on the driver's clock (`LoopContext.Clock`). The one recheck is
    a pass of its own: `opinion.asked` said as the host opens it, `opinion.given` with the seconds from its open, and its consultation kept
    beside it once given (`OpinionDeliveries.RecheckConsulted`); one kept with its diff alone gets it at the next look, read as given and
    never asked again. Its findings never go back to the working session: the gate holds them as disputes, and `opinion show` lists the
    recheck, its word on each first-pass finding and its own (OPINIONQUIET1). The person's word at it holds at the tip it covers; only a
    newer commit asks again, and it stands before the policy is asked. A pass its reviewer's account
    limit or spend cap ends is asked of the role's next holder that is ready at once (a new reviewer session, the same opinion, `opinion.handed-on`
    said once), in the pass's own call and, for one already asked, at the next look; with no holder ready it waits for the reset. A
    recheck goes the same way: where its first reviewer cannot read it (a cool-off, no account signed in, not installed, refused), the
    rule's next ready holder reads it, another maker's first, its `opinion.asked` line naming the reviewer and label that took it; and one a cap ended is gated
    cooling and asked again by the next look's delivery. Under a saved graph the
    `opinion` node says whether one is asked and among which declared reviewers, and cannot start with none; `SET_OPINION none`
    there is refused. A copy made with `--with-opinion` names the shipped `opinion` role, which says only which model reads; the
    reviewers are named once, by the place (`daoris place opinion <repository> --agents …`), and choosing the workflow for a
    repository that declares none is refused with that press. An opinion owed closes as `landed` once its work is on the line:
    by commit ids, by a squashing landing's record, or by patch (a landing that replayed the commits and merged the replay
    leaves the tree's own ids on no branch), each read as the review reads the tree (`TreeWork`), kept while its `HEAD`, its
    repository's refs, its files and the home's records stand (`TreeReadMemo`), so a look over an owed opinion whose tree did not move
    starts no git. Under the watch an owed opinion is judged again only where what its judgment reads moved (`OpinionJudgments`: the
    due itself, its chain's quests, its line's, its reviewers' and its rechecks' records, its tree's commit, its gate, deliveries,
    run binding and node record, the landings, the rule, the cool-offs, the drain), past its gate's reset or a reading pass's
    minutes, or five minutes after its last judgment whatever moved, so work a branch outside Daoris's landings took closes within
    five minutes; a judgment whose gate or review read unread keeps no stamp, and a pass begun for it, or a pass's or a delivery's
    end, lets its stamp go (`LookFacts`, kept by the watch across the looks' drivers); every judgment of one look shares one read of
    the quests and the records (`OpinionLookWorld`). (D155, D190, D227)
27. Review's diff is a dependency graph of git reads, kept by no later open, taking no lock and refreshing no index in a tree
    Daoris does not own; within one open each question is asked of git once (`GitReadOnce`), and leaving the page stops its git.
    (D202)

**Landing**

28. For a graph run the runtime is the one performer of `land`, started once on the landing door, off the pass, under the tree's
    hold and the delivery key, so a press and a pass land one delivery; with no review before it, it goes active with no press.
    One whose subject moved runs again at the new tip once every delivery at the old reads certain. Branch-side steps bind the tip
    of the branch the work stands on, read in the tree of its newest executor; a hold keeps its reason on the node, said by the
    plan and the run view. A unit's executors are oldest first by when their records were made, and the `land` judges the newest
    with a tree by that time (`LandSessions`), so a session that went on in a tree stands for the one it went on from and a branch
    already landed moves on to the chain's next quest's commits after that quest's Accept (rule 17); a part a newer session superseded closes once as `superseded`, its end naming the session that stands for it; a push and a plugin's `pull-request` step after it are bound at the moved tip and performed again (`ReLandingPullRequestTests`); and
    each `landing.auto` line names the run whose `land` tried it. A live session on the tree holds the `land` back, but never the run's
    own part waiting on its step (`awaiting-step`, `LandTreeUse`), which waits on that landing; a look at which the `land` starts or says
    nothing writes a coded `landing.waits` line naming why, once per change of reason (`LandWaits`). Where the line holds every commit by content or as the same change, `content.holds` and `land` reach it
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
    remote-tracking ref holds the landing's tip, failed by the act's refusal; a push of the tip a landing stood at before it re-landed at a
    new one is not yet, so the new tip is pushed (`PushReading`, `ReLandingPullRequestTests`). Daoris opens no pull request: a plugin's step does, its
    evidence's `url` written on the landing for `work/state` to ask about; a graph whose `<plugin>/pull-request` no switched-on
    plugin performs is refused at its bind. A `monitor` watches the one
    step wired into it and reaches with that step's state; open or unanswered it is pending and never fails. A pull request is
    `merged` once its plugin says completed into the work's line (a squash counts), or unanswered once the keeper proves the line
    holds it, and `closed` once abandoned. Its plugin is asked at the moments of plugins rule 11 whatever reached the `push` before it,
    so a run revived after it ended failed there has its monitor reach as any run's does. A landing with nothing to deliver passes over the steps after it not yet started, said;
    a branch the line already holds passes over its failed `push` and pull-request steps too (a pull request merged another way
    after its step failed), so the run goes on to its monitor and the held list clears. A `push` the line comes to hold is passed over
    the same way while active or unknown, its lost attempt cleared; one the line does not hold whose remote's ref lacks the commit it
    recorded as pushed is pushed again (`PushAttempts.Due`). A `push` whose branch no longer stands is never pushed: the door reads the
    line once (`PushLineRead`, the line's fetch under the git policy and the workspace's own access) and, where the line holds the
    landing's tip or its content as the same change, writes that proof on the landing (`LandedBranches.Delivered`) for the push to be
    passed over as delivered; else it fails once, `push-branch-gone` (the line lacks the work) or `push-line-unread` (origin could not be
    read), worded for the person, and a refusal fails the attempt its start is for, except one only because the repository's trees were busy
    (`ActRun.Busy`), which leaves the node active and is tried again at a later pass. A `push` failed `push-refused` whose landed branch
    the remote-tracking ref holds at the landing's tip reads reached at the next observation, the person's own push included (an observed
    fact settles it, D304); at another tip it stays failed. A `push` failed any other way is read so once its holder says it was done
    outside Daoris (`done-outside`, kept on its status, `RunNodeWords.Outside`), and either is still observed after its run ended failed
    there (`FailedHeldRuns`), a reach governing the run again. A delivery step past a `land` that reached on a branch
    no record names any more (`WireRun.LandingUnread`, every record of the work read) is passed over too, saying so. Where only the
    landing's entry is gone and the session branch's record stands (`UnrecordedLanding`), that record says: held on the line, the delivery
    steps are passed over with words; standing off it, the `push` is started once on the branch the `land`'s evidence names; neither, it
    fails once, `push-branch-gone`. A monitor is reached at the commit its own binding holds, whatever tip the
    landing reads (a squash merge), saying the line holds the branch where it does; a `push` and a `tidy`, which keep no attempt journal and
    leave `active` only by their reading's reach, are reached at their own binding's commit too, their evidence naming the landing's.
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
    with or without it. A tree whose part a run's `tidy` ended, and the trees of a run that holds no
    `release`, ended or abandoned, are ended by the look's sweep (driving rule 51). (D228)
31a. A plugin step declaring a `goal` (at most 1,200 characters) is performed by an agent, never the plugin's process. Its ask (the
    goal, the run's place and commit, the landing's evidence, the step's outcomes, schema and options as its graph writes them) is kept under
    `workflows/agent-steps/<invocation>.json`, and the step's session starts off the pass: staffed as the place's builds, a chat on
    no quest at the run's checkout holding no tree, lease or cap, read-only, with only its plugin's `daoris-plugins` server and
    `step_answer`, refusing the person's words (*Workflow step* in Sessions). `step_answer` keeps one answer, `reached` with a
    declared decision, evidence and outputs, or `failed` with a code, applied as the plugin's completion; a session ending
    unanswered fails it `no-answer`. A session its agent refused for its account's sign-in goes on once under the walk's next account
    with no press; where no account could take the step, it fails `sign-in`, naming the accounts and the sign-in to make (agents rule
    40). A step with no goal stays on `work/perform`. (D274, D328)
32. A merge runs through the integration transport under the legacy policy (no gates, one at a time, a merge commit): the person's
    press at the `press` door, settled *merged on the person's press*; an automatic `land`'s delivery at its `landing` door,
    settled *merged by its workflow's landing, with no press*. (D215)
33. Each delivery's phases (prepared, ref written, recorded, plugin dispatched, plugin answered) are written ahead and a restart
    settles them in order. A preparation cut by a stop reads withdrawn, and a delivery all withdrawn settles so at the look,
    holding no update back. A delivery is keyed by unit, tip, method, target and generation: a retry of a withdrawn one starts the
    next generation, and readers take the newest. (D215, D227)
34. A landing refused for work the session can clear carries the session on and keeps the Accept. A landing refused for a cause
    that passes (`busy`: another of Daoris's writes held the checkout when its branch came to be written, as the look's tidy, an update
    or another landing does, or its delivery or its tree's landing hold was taken) keeps the Accept too and is never asked again: it is
    tried at the first look the checkout is free, through the same delivery key, so it lands once. Each such wait has a deadline
    (`LandRetries.Deadline`), judged at a look that finds the checkout still held and at each refusal again, so a cause no look reads (a
    delivery or a landing hold taken here) climbs too; past it the wait climbs the reporting line above the landing
    (`ReportingLine.Climb`), said once in the conversation and on the run's step, and is still tried at each look. Any other refusal nothing changed is
    tried again only when a tip moves or the person presses, and work landed at its bound tip is done with its landing. A refusal
    for the repository's own checkout's uncommitted work (`root-dirty`) waits on that checkout and lands with no press once its
    status changes. A line holding every commit closes the entry `line-holds`; a merge waiting in integration is `queued`, made
    again each look; neither says a review waits. (D145, D227)
35. In a session tree the agent commits on the tree's branch as it goes, and never switches or pushes. The commit rule (`commits`)
    may say `ask` or `at-end`. Daoris follows a tree an agent moved anyway. (D164)
36. A chain's next step in the same repository starts on the step before's branch, and the chain lands on one branch, which
    advances only while its pull request can take it. A landing's branch is named for the first quest of the unbroken run of steps in that
    repository that ends at it: the walk up a step's parents stops where a parent was asked of another repository, compared
    without regard to case (`BranchPatterns.SubjectAsync`), so a chain that returns to a repository names the returning step's
    branch for the step that returned, and no branch is named for another repository's quest. (D82, D149)
37. Branches a landing made are kept in `<home>/landings.json`, traced once gone. A landing removes no tree and no other session
    branch; a session branch goes only by its own door, the look's tidy or, where a run owns its tree, that run's `release` or
    the sweep (rule 31b), behind D88's proof. A gone session branch stays traced
    while its unit owes anything, and a live run holds every subject its nodes observe, so an accepted `land` finds its branch.
    (D88, D102, D232)
38. A pull request's state is asked of the plugin that opened it, at most once a minute each. A landed session's review reads its
    landed branch. A folder's review is the turn's change set from its manifests; it names no base, and says that branches,
    history, integration and landing are git's. (D113, D148, D254)
39. Integration is one run per line, outside every run, with fixed steps; `done` and `steward` requests need declared, accepted
    gates. The line's policy is `linePolicies`, edited beside the branch by `SET_LINE` and, over the same file, by `daoris place line
    <repository>|--workspace <name>` with the policy's keys (`--commit`, `--batch`, `--order`, `--gates`, `--unreached`, `--reached`,
    `--integrate-line`, each judged by `LinePolicyKey` as the press is; they merge into the place's own policy, and with no branch
    the line stays; `--no-policy` withdraws it); who requests and where a conflict goes are the `integrate` step's (the bound
    version's, or outside a run the newest of the place's default workflow, else the owner's). A failure is confirmed once before
    it is blamed; a gate that would not start is an unknown run. A `request` under `start: you` waits for the person's *Send*.
    (D215, D227, D232)
39a. An ask's parts land through that same queue, never a queue of the team's own. A part's landing is an integration request tagged with
    its ask and part (`integration_parts`, `TrainParts`; the `integrate` door carries `IntegrationAsk.Train`) whose line is the ask's
    branch `daoris/ask-<id>` (`TrainBranch`). Before a candidate forms, `TrainSelection` reads the ask's own plan from the service
    (`ITrainPlans`) and offers only the ready parts, in landing order: a part is ready when every part it builds on (`after`) has
    landed, a part waits behind one that lands before it (`orders`) while that one is queued and not ready, and a part the plan does
    not name, or whose plan is not read, waits. The rest is the line's: one candidate at a time, the members judged, a red's part
    handed back to its own request while the others are formed again, a conflict told to the member's session with its branch
    untouched. A part is landed when its member is promoted onto the ask's branch, never when its quest says Done. The `parts` node
    (`Workflows/Kinds/Parts`, observed from the integration record, no failure of its own) is reached once every part of the ask's own
    plan at its standing revision has landed (`PartsReports`, read by each pass for the asks the integration holds a part for); a part
    a revision dropped is no longer required. A part's `integrate` start tags its own request: the quest's publish key
    (`<ask>/<part>/<n>`) names the pair, and the ask's plan must give that part to that very quest (`TrainPartOf`, the plan's part
    carrying its `Quest`), else the request is an ordinary landing's. Where the ask's plan is built as one scope (its `integration`
    is `one-scope`, `ScopeLanding.GatesEachPart`) and the loop's doors ask the scope's two reviews (`NodeDoorWiring.ScopeReviewed`:
    both who offers them and where they are asked are wired), the part's request is frozen with no gates (`IntegrationRequests.NodeJudgement`),
    so each part merges into the ask's branch ungated; under `per-part`, a plan that names none, or a one-scope plan whose two reviews
    are not wired (the install's today, so no part lands with neither its gates nor doctrine rule 3's reviews), the line's own gates run. Before each batch the line's tip is merged into the ask's branch
    from objects alone (`TrainBases`: `merge-tree`, `commit-tree`, one verified `update-ref`; a conflict leaves the branch as it was
    and the parts land on it unmerged; a missing branch is made at the line's tip), and each promoted batch is kept on
    `refs/daoris/train/<ask>/<n>` (`TrainRefs`). The shipped `team-part` (work, checks, `integrate` with `request: done`, the line)
    and `ask-delivery` (`parts`, a joint `check`, the one `review`, a `merge` landing, the line) presets are saved and chosen as any
    other. A part's run is chosen by the kind `team-part` Daoris gives it (`TeamKinds`): where the ask makes no choice of its own and
    the place maps that kind (`workflow kind`, `workflow use --kind`), its quest, the one the plan gave the part, binds the mapped
    workflow, its plan read strictly at the bind so an unanswered read holds the start; a place that maps none binds a part as any
    quest. This repository maps it to `tools/workflows/daoris-part.json` (apart, checked on the gates reached, a second opinion, then
    integrated onto the ask's branch and released): the close review of the team's two. (TEAMWORK1f, D296)
39b. An ask's delivery run is one run of the saved `ask-delivery` per repository on this machine the ask's plan gives a part to
    (`AskRunKey`: `ask-<ask>-<digest of the repository>`, keyed by the ask, never a quest's chain). A whole look binds it
    (`AskRunBinder`: the open asks that have quests, their plans read from the service; the workflow the place maps to the kind
    `ask-delivery`, else the home's saved copy of the shipped workflow, saved from it where the home has none, its newest version, so a
    person's later version of it binds) and writes it once; an ask
    whose plan is bound for its quests as they stand is not read again until they change. It is a run like any other on the one runtime
    (`AskRunScopes`, hooked at `NodeGoverning`): its subject is `daoris/ask-<id>`, a branch unit no session made, whose tip each pass reads;
    it has no quest, so it ends (`AskRunEnds`) when nothing waits and no node can move, naming the ask, and is never governed again.
    Its `parts` waits for the plan's parts given to its repository (`PartsReport.For`, a part given to none waits everywhere); its
    `check` verifies its repository at the tip it is bound to under the accepted declaration (`RunChecks`, beside a verification
    run's send, for a chain's run as for an ask's), and where the ask's plan is one scope, the plan read at each send, it runs the full
    set, every declared gate and no reached reading (`ScopeLanding.EndAsync`, `VerifySend.Full`), and names the two reviews, wide
    and close, from the reviewers the landing role offers; its `review` is the ask's Accept in that repository, bound to the branch at its tip and to where it left the line
    (`AskRunReads`: the merge base), its handbacks the quests the ask gave the repository, answered by the person or by the place's
    appointed landing supervisor (roles rule 49), its step kept against the newest session of those quests (`LandingSessions`); its `land` merges the branch at the accepted
    tip into the line (`AskLanding`, through the integration the session merges use, with no session or tree) and is reached where the
    line holds the tip. This repository maps `ask-delivery` to `tools/workflows/daoris-ask.json` (parts, the whole checked on the
    gates reached, the review, a merge into main) and appoints an agent landing supervisor for it, so its wide review is an agent's of
    another family and no step waits on the owner; a place at high risk appoints none, and its review waits for the person. (TEAMWORK1f, D296)
40. A verification pipeline's `check` is Daoris's, off the pass under the heavy slots, with no agent session, never skipped; a gate
    that would not start makes it unknown, named. Where the landed route names a pipeline with accepted gates, each whole look binds
    a line run, based on the line's newest verdict, once the tip passed it and none is in flight. A run holds the line until every
    check reached, failed or was stopped, is never superseded, and keeps its files until a newer run has a verdict. *Run again* is a new attempt,
    and the run's `again` on a check not known is that word (`daoris workflow again <run> <check>`, `UnknownChecks`: kept on its current
    attempt, which is held with it where Daoris still asked after it), named on the run's terminal line beside its stop; a not-known check
    says it holds nothing as passed, never that Daoris asks after it; *Not now* settles an unknown with
    no verdict, or a failure's held row. *Ask for a fix…* publishes one ask per failed gate with its last 256 KiB, never twice;
    `failed: ask`, only in a version the person saved, asks at each look, `wait` waits for the press, `stop` asks nothing.
    `reach: paths` runs the gates the batch's paths reach and every gate not last passed, naming the rest *kept*. A person's
    verification (`daoris gate verify`) is a run the batching never counts. A session's own heavy check is the broker's
    (`check_run`, driving rule 40a), the same heavy run under the same admission; a check whose `gates` is `reached` reads the hand-back and runs the declaration's required `handback` commands
    the change reaches (driving rule 7), and with them the line's admission the change reaches (below), the builder's own fast tier: here `reached-checks --tier fast`, the reached classes less the heavy
    and Process tiers, which the run after the landing owes. A command whose log ends with the declaration's outcome line has its classes
    run, replayed from passes of the same inputs, red, flaked (passed alone, which is no red) and owed after the landing said in the
    verdict's why (`ReachedVerdict.Told`), and a failed check's step names the gate that failed, never only its code. The line's admission is the declaration's `admission`
    key: `always` and `suites` for every change, and `byPaths` checks, each reached by a changed path matching one of its globs or one
    of `verifyBudget`'s wide paths. The merge tool selects its blocking gates from it (`tools/merge/admission.ts`, `reach.ts`) and the
    check reads it in the declaration it runs under (under full access the merged tree's own, so a branch that predates an admission the line added is held to it), on the work as merged (`AdmissionGates`; a `!` glob excludes as the lanes file's does, an admitted gate with a `handback` command runs that command in its place, and one whose command holds an unbound `{base}` is unknown), so a gate the merge tool holds a change
    to holds a run's landing too, one list read twice, and a red one fails the step naming it with its red lines for the work; a name
    the declaration does not declare, or an admission that does not read (a declaration that is not JSON included), is unknown, coded `unrun`, never a shorter list. The merge tool reads the admission of the tree it selects for (the merge in place, a candidate's tree; one that predates the key is judged by the tool's own). (LANDGATES1) A failed gate's
    why carries the failing step's own lines with its log's path, said relative to the home: the log's red blocks (an `== RED <step>`
    header line, matched as a step's header only, to the next `== `) and its last lines, within about 40 lines and 4 KB
    (`GateLogExcerpt`), so the work reads why without asking for the log. The log is a child's words and passes `GitWords.Redact` first:
    a line holding a secret's shape (every token and key shape the sensitive scan's built-ins name, an API secret key and an AWS access key
    among them, a key block, an Authorization or Cookie header) is withheld whole, so what a verdict's why
    keeps and says never carries one. The same excerpt follows a `regenerate` that did not finish, its `regenerate.log` written in a
    try of its own (a log that cannot be kept leaves the command and its output said without a path), and a check cancelled in the
    preparation or a regenerate ends as ended (`it was ended before its verdict`, each gate not run), never as an exception. The
    runner keeps each group's passes as the group ends, so a check asked again at a new commit (a merge of the line's tip included), or
    after one stood down or stopped, replays every class whose inputs that commit left unchanged; Daoris keeps no class verdict of its own.
    A step wanting the heavy tier names it: `heavy` (the gates the declaration marks heavy, and the `reached` runner given `{tier}` `all`); any other
    verification pipeline's check runs the runner given `{tier}` `fast`, never the Process half the run after the landing owes. The machine runs as many heavy runs at once as its one admission admits
    (`CheckAdmission`), judging every heavy run on it together from the records beside the slots: the checks' and gated integration
    lines' check slots (`locks/verify/`), every repository's heavy-run slots, and the sessions' `run-heavy`, a run holding both counted
    once. The judgement, the take of each slot and the write of each record happen under one lock (`locks/heavy/admitting.lock`), so two
    starts never both see no holder. The machine setting `checks` (`daoris settings checks <n>|auto`, the window's `SET_CHECKS`) pins a
    number, never more than the machine's heavy-run admission holds; `auto`, the default, runs the first always and another while free
    physical memory and free commit both hold a run's need (the merge tool's 8 GB for a Process suite, since a check runs the Process
    classes its change reaches), counted again for each run whose first gate has not ended (its record says so; a Daoris check's one
    `reached` gate holds its build and its suites, so it counts twice for its whole run), up to a quarter of the processors within the
    machine's heavy-run admission, whose places each step still takes; memory that cannot be read runs one. A repository's heavy-run
    slots (`HeavySlot`, `locks/heavy/<key>.lock` then `<key>-n.lock`, each with its holder's record and its own dead-holder clean-up)
    are as many as the admission's, so one repository's checks, its sessions' heavy runs and its integration's gated lines run side by
    side, each check in its slot's verification tree (`lines/trees/<key>`, `<key>-n`; a tree past the slots there are now, left by a
    larger `checks`, stays as it is, kept for when they grow again). A repository whose gates share what two trees cannot declares
    `"heavySlots": <n>` in its `daoris.gates.json`, read for every caller (a check, a gated integration line, a session's `run-heavy`) from
    the declaration the person accepted for it, by git's object store at each look (`AcceptedSlots`), never from a branch's, tree's or
    checkout's own file: a branch that raises `heavySlots` gets the line's count, and where nothing is accepted the machine's count
    stands. Its heavy runs then run that many at once, 1 for one at a time, within the machine's admission (D349: the framework is
    Daoris's, the count the repository's); a change to `heavySlots` is a change of the declaration like any other part the person has not
    accepted, so it holds a restricted work's runs and is said by its key. A start that cannot have the admission's lock in its tries
    waits saying the lock is held (`CheckWait.Admitting`, `slot-admitting` on a check's step, and in a session's `run-heavy` words), never a slot count. A check judged as
    merged reads its line's tip once and merges from objects alone, so an integration landing beside it never shows it a half-moved
    line. A session's `run-heavy` that is not admitted while its verification holds a slot of the repository says the true reason
    (its repository's slots, memory or the machine's slots) and does not wait. A sent check the executor
    has not started says why it waits on its step, never *Ready*: every slot running (`slot`, how many of how many), memory (`slot-memory`,
    free and needed), every slot of its repository held (`slot-repository`), else its place in the queue (`slot-queued`), each with that place
    (`CheckSlotWaits`, read from the slots' waiter files at each look). `["reached"]` in a list still names a gate called so. (D192, D293) A declaration's `reached` entry (`run` with `{base}`, `cwd`, `clock`, and
    `outcome`, the marker of the last line the runner prints, JSON of `ran`, `replayed`, `flaked`, `mainRed`, `red`) replaces the
    gates for a check whose run has a base (its batch's base, else its line's branch): the runner runs in the verification tree
    under the step's one place in the machine's admission, a clean exit is reached, a red class fails it naming the class, any other
    exit is unknown, and the result's why says the classes run, replayed and red and names a flake; a declaration with none, or a
    run with no base, runs its gates as before (`ReachedRunner`, `ReachedVerdict`). A check holds one place in the machine's admission, taken
    after its tree is prepared and held while its regeneration and each of its gates or its runner run, never one place for each; the
    regeneration's commands are told it as each gate's are (`DAORIS_ADMISSION_HOLD`), so a child that asks the admission takes none. A run's check (`RunChecks`, `AsMerged`) judges the work
    as merged onto its line's tip (the remote's head, else `main` or `master`): the verification tree takes the work where it holds that tip,
    else a merge made from objects alone (`LineMerge`: `merge-tree --write-tree`, then `commit-tree`, the tip its first parent), never the
    session's branch rebased or moved. The verdict names the work's commit, which the landing lands, merging it onto the line as it stands
    then. A conflict fails the step, gate `hand-back`, naming the paths, and no gate runs; git unable to merge checks the work as it stands,
    saying so; a line's own run and a declaration's runner judge their commit as it stands (D299). The verification tree is prepared as a
    session's tree is before either runs (`VerifyTreeReady`: the files stage's dependencies by their lockfiles' keys, then the
    checkout's declared local inputs), again only where its stamp moved (a dependency key, or a declared local input's presence or
    content). Then, on every check, the tree runs each `regenerate` of the declaration the check runs under, read as its gates are
    (`TreePreparation.RegenerateAsync`; `--if-stale` is cheap where nothing moved), never the `treeBaseline`: a later commit whose
    generated inputs' sources moved reads them current though the stamp stands (VERIFYREGEN1). A preparation or a `regenerate` that
    does not finish ends the attempt `unknown` with code `unprepared` and its sentence (a `regenerate`'s command and its output's
    lines, kept as the attempt's `regenerate.log`), never failed, no gate run.
41. `SET_ACCESS` refuses the `landing` pause, and `full` access opens no landing pause on a saved-graph place. (D184, D227)

**Set up, conversion and Current**

42. *Set up…* offers *Not driven* and the shipped deliveries marked `setUp`: merge after accept, a branch to push, a pull request,
    integrated. It suggests integrated where the line declares integration, else merge after accept; a pull request is only chosen,
    and refused where no plugin performing steps serves the remote's platform. *Keep how it lands* writes nothing. (D221, D227)
43. `PLACE_APPLY` is the one door of a place's set-up: its adoption, and a room asked of an adopted repository (`--room`, a part that rides with no
    other and is a quest to that repository's own session, driving rule 53). `place --workspace … --adoption` paces a workspace's adoption by `atOnce`. (D227, D276)
44. No migration converts a workflow record: a saved version is read as it was written, a graph. A saved version is configuration
    a purge keeps, so its former `human` step reads as a `task` (`HumanFormerKind`) until no saved version on the install spells
    `human` (NOCOMPAT3); `daoris workflow list` names each version that still does, read-only. (D232, D323)
45. There is no landing rule: nothing reads or writes `landings` or `workspaceLandings`. Every door that lands,
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
51. A `task` step is an operation done by hand, reached by its role's holder's *Done* or, for an approval, *Approve* with an optional
    note, or rejected by *Reject* with a reason, as a review's changes are; a person and an agent hold it alike, the kind saying what
    settles it and never who, and a step naming no role is the `owner` role's (D304 point 2). Its outcome (`done`, `approved`, or none)
    routes a `when` wire, a rejection the `rejected` wire. A press on a node not waiting is refused `not-waiting`, on another kind `not-task`.
    A step naming `human` is refused as an unknown kind, and a save writing
    a `task` step closes going back below its build (`tasks-since.txt`, kept apart from the roles' floor, `WorkflowRolesSince.Floors`); each floor is written whole or not at all
    (`AtomicFile`).
    One declaring two or more outcomes of its own is a per-run decision (`TaskDecisions`, D304 point 5), answered by `decide` with
    one of them (`daoris workflow decide <run> <node> <outcome> "why"`; the run's view offers each outcome); another is refused
    `not-outcome`, and a save refuses a decision no `when` wire leaves on any outcome (`decision-unwired`). A word naming a holder
    is a proposal and stands as the answer at once, held for the owner's word: the owner's same answer confirms it, another
    overrules it, and the owner may answer again until the run ends (`reached → reached` on the same binding, so the old outcome's
    route lapses); a holder's proposal after the owner's answer is refused `not-waiting`. The step a `when` from the decision leads to
    on another outcome is skipped by the decision (`WireDecisions`), its words the decision's line ("Not needed: proposed by
    release manager (why), confirmed by the owner"), and what follows it goes on as after any skip; it takes the line's newer words
    as the owner confirms, and is judged again once a changed answer opens its route. A step closed only behind a skipped one is not
    skipped: it reads that skip. An agent holding a decision answers it through its working session's tool (`step_settle`, D318), and
    its answer settles the decision, no proposal held ("Not needed: decided by agent:<name> (why)", `NodeWord.Decider`). The other
    channels naming a holder (a hand-back's proposal, an appointed agent) and a marked destination's wait for the owner's confirmation are
    not built yet: the window and the terminal are the
    owner's.
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
55. Every step's wait is read by one reading, `StepWaiting.Read`, from the bound runs, never stored: the wait it stands at
    (`PersonWaits.All`: an active `review`, `task` or `go-ahead`, a `return` on `both-changed`, an attempt held `unknown`, a plugin step
    waiting on a role (`actor`, the role its plugin names), an `opinion` its gate holds for want of a reviewer (`vacant`, the hold
    `OpinionGateReading.Vacant` its record keeps), a step left failed (`left`: a failure kept with no word on it, read for a quest closed
    done alone), a `quest.holds` holding an unchecked done (`pass`, on the step's role, `ask-reader` by default, only where a role or cast names it; a done held for the yes is
    its held row's)), the role it waits on and that role's holder. Each kind says what its active step waits for (`IWaitsOnRole`), so the
    reading names no kind and never asks who holds the role: a step's own word (an Accept, an approval, a task's Done or answer, a
    go-ahead) and an unchecked done's pass wait on the step's role (`StepRoles.Of`, `StepWaiting.OnOwnRole`), every other wait on its own (`StepWaiting.RoleOf`), a vacancy and a failure kept
    the line's `lead`. A wait is answered by its own role (`StepWaiting.HolderOf`, D312): the step's own role by its attempt's tenure, any
    other role (a vacancy's line, a plugin's actor such as `credentials-keeper`) by the place's cast of that role (`CastReading`), never
    by the step's holder. A `task` an agent holds (its Done or a decision's answer, `AgentSettles.Tool`) is the run's working agent's to
    take (D318): once the pass keeps its tenure, the run's newest session of this machine (awaiting the run's step, else the work's own,
    resumed where it ended) is told it through the say door, once per attempt and epoch (`StepHandOffs`), with its outcomes, the evidence
    its `evidence` option names and the tool that settles it, `step_settle`; the session's settle is a file under the home's sessions
    folder the run's progression takes at once, as its fact (`step.settled`, `StepSettleAsks`, driving rule 30a), through the run's node door at the settle door as the agent its start ran, refused with
    nothing kept where the step does not wait on its holder's word, its outcome or a named evidence is missing (`TaskEvidence`), or the door
    refuses it, a refusal told to the session as its next turn; where it cannot do the step it asks up its line. Its row in *What needs
    you* reads "waiting on `<role>`: <agent>" with *Take* its only press. A plugin's actor whose role a staffing gives an agent, or a cast
    gives an agent a live session of serves the place (`SessionDelegates.Serving`), is served by that session, to which the plugin grants
    its acts, and makes no row. Every other wait an agent holds that no agent's session serves (a plugin's actor held by its role's agent
    by default or cast to an agent with no live session there, a go-ahead, a file's choice, an opinion's vacancy, an unchecked done's
    pass, and a role-classed Accept or approval, which no dispatcher hands to a session yet) is a vacancy (D354 point 4, `StepVacancies`).
    The pass alone judges it and keeps it from the moment it became vacant (`StepVacancyFiles`, `workflows/vacancies/`, which survives a
    restart), and every reader says what it kept: a `step` row marked `vacant`, never handed (so a release's approval is never handed to
    the run's maker), listed under its own heading, *Vacant*, in the window (`AttentionOwnership.VacantClass`) and in `daoris session
    list` (its `--json` carries the group as `vacant`), a run view's step saying it is vacant (`WorkflowRunStep.Vacant`, never shown as
    handed to an agent), never counted as the owner's and never hidden, announced once (`WaitAnnouncements.VacantKind`), and said once in
    the run's lines; the person's press still takes and settles it. Once announced and past its bound from when it became vacant
    (`vacancyHours` in `driver.json`, a day by default, 1 to 720, `VacancySetting`; `daoris settings vacancy <hours>` or the window's `SET_VACANCY`, its row beside `checks` at Settings → Agents and sessions) it reaches the owner's final exit (`FinalExits`, D354 point 4): one notice
    naming the cast, the role's agent and the Vacant list, how long each held it and why none answered it, and the one answer asked, and its
    `risk.judged` line, kept as exited (`StepVacancy.Exited`) so it is never said again whoever serves it later; every reader then says it as
    the person's row (`StepWaiting.Exited`), no longer vacant, until the owner settles it. A plugin's actor or a spent red's role that a live
    session serves, or a task handed to the run's working session (`StepVacancies.Bounded`; no live session need serve a task), is kept too
    (`StepVacancy.Served`, never listed) and, never answered past the same bound from when it was first served, reaches the final exit the same way.
    The bound runs from when the step first stood unanswered: a holder whose session comes and goes changes only whether the kept record is served
    (`StepVacancies.Look`), never its `Since`. A vacancy's line, notice and journal line are said only once its record is kept. Daoris never stops it as its own; a give-up a build before the final exit wrote
    (`NodeWord.GaveUp`, `NodeSource.Daoris`) is read and said as it stands. A step no longer vacant drops its
    vacancy, so its clock starts again if it becomes one. An exit is kept by its record whoever holds the step now (`StepVacancies.Kept`): a cast or a policy that moves the holder to the person keeps its row.
    A stored vacancy is judged again as it is read (`StepVacancies.Kept` with the place's
    serving: a staffing, or a live session of a cast agent, which the runtime's readers for the window, its held steps and its person waits, know and a run's view, reading the
    cast alone, does not), so a step served again leaves *Vacant* at once and
    not when the pass next drops it; a vacancy file that will not write is said once and never ends the pass; and one whose run's record is gone is
    swept by the pass with it. A place's production-releases mark keeps every `release.*` act at the owner over every policy row (`RiskGate.Judge`), its
    approver's approval is the owner's, and a step whose role is the owner's is the owner's whatever the policy says: `RiskStepGates.Verdict(judge, kind, node)` is the one verdict path every hold reads (a spent red, a quest's departure, the quest's holds, the opinion's gate; an unwired opinion port reads no judge). Every step the policy passes journals its `risk.judged` line once (`PolicyPasses`), a raise to the owner or a lowering where a hold would release
    writes its own at release (`TenureRejudge`, its `today` saying which, its key the hold's with the epoch it moved to), and a run whose
    passed step the person held is told so once in its lines, with that step's first pass line, so never again after a restart; an
    `opinion` step its gate's pass let go journals its own line the same way. A review's Accept and a task's approval wait only where the risk policy
    gates them (rule 22); where an Accept or an approval waits on an agent holder, that agent answers through its own session's `step_settle` (an Accept or
    `changes`, `approved` or `rejected`), never handed to the run's working session, whose answer is refused `own-work`; an unchecked done's pass is the one its agent holder settles itself, through the node
    door as itself (`workflow skip` from its own channel, D352), never handed to the run's working session, whose done it reads and whose
    pass of it is refused `own-work`. No worker answers its own gate (D354 point 5): at the settle door (`SettleDoor.Own`, asked by the
    node door and `step_settle` alike) a yes (an Accept, an approval, a pass, a decision's answer, a Done) to a step standing at a hold of
    the risk census (`RiskStepHolds.KindOf`) from a working session the run's makers hold (`NodeRuntime.Workers`: those serving its quests
    and those that worked its branch, with every helper session a maker started and theirs in turn, read from the home's helper asks, ids compared without case; handed to the look as `ILookWiring.RunWorkers`) is refused `own-work`, read from the word's channel
    and its proved session, never its words, whatever holder it speaks as; while the makers are not read such a yes from a working session
    is refused too (`not yet: try again after Daoris's next look`), and a `step_settle` so refused is not consumed: its ask stays open and the box's sweep, once a minute, takes it again; a task's own Done, which is work and not a hold, stays its worker's. A review's or an approval's `step_settle` names its outcome (`accepted` or `changes`, `approved` or `rejected`), never taken as the yes by omission.
    The waits whose role the person holds are theirs: Sessions places the run's newest
    session in Needs you with its `waits`, opening its page, and *What needs you* has a row for each with at least one press or the
    page where its act is taken, which `StepWaitCensusTests` holds for every wait in every run state it stands in, and its card on the
    session page's head (`RunWaitsHead`, its census beside it): an Accept and a part left after a done are the placed session's
    rows, the step a run ended failed at its own `step` row whatever its quest's state (`LeftSteps`: *Try again*, *Done outside
    Daoris…* where its kind reads the proof, *End this work…*), an attempt held its held row, and every other wait a `step` row (`HeldSteps`) carrying the role and holder, the step's own words
    that settle it (a skip reads *Not needed this run*) and its stop where the step offers one (*Stop this step…*, asked once, the run's node stop as the owner role's, D316), its tenure's epoch and the holder acts it offers (*Pass to…*, *Give back*; none on a go-ahead, whose answer is the grants', D306, none on a wait on another role than the step's, and none before the pass casts the step), a plugin's words and page, under a release step waiting on its approver the run's view's approval card, *Done outside Daoris* on the attempt
    out now where a plugin's wait has no page, or the run's view (`opens: run`) where a file's choice, a go-ahead and a vacancy are
    answered. A `step` row names the session its run placed; one whose role the person holds is announced once as a park is
    (`WaitAnnouncements`, kind `Step`, under a park's switch, its words naming the role: *a step waits on `<role>`, which you fill*),
    and its notification opens that session, whose head reads each wait "<node>: waiting on `<role>`: <holder>"
    with the same presses beside it, each passing the epoch its row read, and the row's *Take*, *Pass to…* and *Give back*
    (`RunWaitsHead`). Sessions' placing reads the waits as the rows do, the step's tenure and the place's cast (`PersonWaitReader`).
    That session's page reads its run from one answer, `SESSION_GROUPS`' `passage` (`RunPassageReader`: each step's mark, its role, its
    newest attempt's number, start and failure code, and the attempts its record keeps before it): the track under its head and a row for
    each attempt's start and end between its turns (`work/track/`), the record keeping no moment for an outcome, so an end stands where
    the next attempt or step started; the run's view opens from the track's *Flowchart*. A run's view and `daoris workflow show` say it the same way: a run step has one
    waiting state (`waiting`, with `waiting-on-you` and `waiting-on-agent` deleted), its `Role` the one it waits on and its `Holder` whoever
    fills it, said "waiting on `<role>`: <holder>" (等待 `<role>`：<holder>), the holder's word the only part that differs (D304 point 2, D309
    point 4); "you" is how the window names the person's holding, and its pill wears the person's hue only where they hold the role. A step
    the run's agent holds reads as the agent's on every surface (`heldByAgent`): its node's part says "agent alone", by an agent, never
    the *you* its kind declares (`whoSaid`), and where the session's dock holds a question, sign-in or go-ahead its agent asked, the head's
    card says the agent waits on the person for it with a link to that line. Its presses are the driver's alone: a step handed to the agent
    holding its role (`AgentSettles.Handed`) offers *Take* alone, no act of its own (Done, a skip, a stop), in the run's reading as in its
    *What needs you* row (`StepHoldActs.Handed`), and the window draws what it is sent, withholding nothing itself; the person's held
    step keeps its presses. (AGENTSTEPCARD1, AGENTSTEPCARD2) That an
    agent is still at the second opinion is read off the step's detail (`with-session`, `AgentAt`), never off the holder. (ONEWAIT1)
56. A pass judges at most 32 units: the run a door asked for, a held event's unit and a record the pass moved first, then in
    rotation those owing a start or a rebinding, then the unchanged, which keep a quarter of the turn. A unit's resource record
    keeps 32 receipts and a guard of 256 dropped keys, so a replayed completion owes nothing new. A fact that ends a step (a check's
    verdict, a settle taken, a session's turn end, a policy pass at the opinion's gate, a landing's try) asks the pass for its run at
    once through the bus (`run-step`, driving rule 30a), so that run is judged first, on its record, tips and gates read at that pass, and no step waits for a turn; the
    look's whole pass remains for the inputs no fact names.
57. A run's review is read by its run alone (`RUN_REVIEW`): the record, its outputs (`ReviewOutputs`: each handback a document, the
    changes a change set, a plan a resource plan, each with a stable id and a digest), a branch's or a checkout's commits with the
    uncommitted paths apart (`ReviewBranchCommits`) and the patch per file between the record's commits, the steps before it, presses
    only on the attempt the review stands on, the supervisor's held decision, the steps an Accept may start. A branch's base is the
    commit its chain grew from the line (`RunWorkBase`), frozen with the tip on the record; a branch record with none names no commit before and reads no
    change set. The **Outputs** view (`outputs/<run>`) pins the attempt first answered, picks any revision,
    and draws *Under review · rN* (the change set first: commits, then every file's hunks in one scroll, a viewed or large file folded)
    and *After the Accept*; a newer revision is a banner naming the outputs that moved, never swapped in. A folder's file is drawn at
    the record's hash, or from the place only while it still hashes so, else refused `moved`; a Markdown file opens on its changes,
    rendered one press away. *Viewed* marks stay on this machine by path and content. Accepting, sending back and stopping a run are
    the **Accept card**'s alone (`ReviewCard`), under the session panel's story (`ReviewCards`), bound to the attempt and revision it draws: a newer
    revision supersedes it and appends a new card, and a stale press is said `moved`. A session's own changes (`DiffPane`) keep the
    tree's discard and draw no Accept; the dock's work card and an entry's part draw an outputs door, *What needs you* and the run view
    open the outputs by run (their presses read *Open outputs*, 打开产出), and the work card's *To the Accept card* leads to the newest card
    drawn under the story (`seekReviewCard`), offered, as its *next: Accept…* is, only once the run's review has begun and
    its card is drawn (`landingStands`, the landing's `accept` with its attempt); the one press left outside the card is the ask-level *Accept the ask's parts…* in Sessions' list by ask.
58. A landing's replay settles the declared backlog by its rows' ids: a row one side changed takes that side, one changed alike is
    kept once, any other is left for a person with both texts, and a result losing a side's words or covering an id twice is
    refused. The integration's member merge and the merge tool (`fixtures/backlog-merges.json`) hold it alike.
59. Setup is the Agents place's default. Workspace and repository pages in Repositories each have a Workflow tab, reading Current
    only while open; a workspace's own-rules door opens the repository's Workflow tab. Settings doors open the place in Agents → Setup
    (defaults under Workflow, access and Reach under Sessions).

## Where a change goes

- **A new step kind:** its id in `NodeKinds` and its folder `Workflows/Kinds/<Kind>/` (rule 49); nothing else lists it. A rule
  about where a node stands is its `Problems`, one reading several kinds the kit's, one every graph keeps `NodeGraphs.Problems()`.
- **Which workflow work follows:** `WorkflowSelection`, held by `fixtures/workflow-selection.json`.
- **A shipped workflow:** a JSON file under `Defaults/workflows/`; the presets derive.
- **A landing:** `Workflows/Kinds/Land/`; the act in `Workplace/` (`LandPerformer`, `TreeLands`, the deliveries).
- **An ask's parts landing:** `Workplace/Train/` (`TrainParts`, `TrainPlan`, `TrainSelection`, `TrainBranch`, `TrainBases`, `TrainRefs`), hooked at `IntegrationReplayAsk.Train` and `.TrainBase`; the part a quest is the worker's of (`TrainPartOf`) beside the scope's landing in `Workflows/AskRuns/`; the `parts` kind in `Workflows/Kinds/Parts/`.
- **An ask's delivery run:** `Workflows/AskRuns/` (`AskRunKey`, `AskRunBinder`, `AskRunReads`, `AskRunScopes`, `AskRunContent`, `AskLanding`, `AskRunEnds`; a part's wait and its run's view of the parts, `PartReceipts`, `PartViews`), hooked at `WatchNodes` (the bind and the read), `NodeGoverning.Governs`, `NodeRunKeeping.EndAsync`, `CheckNode.Door` (its send, `Kinds/Check/RunChecks`) and `LandNode.Door` (`LandingDoor`'s `asks`); held by `Workflows/AskRuns/` tests.
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

- **The window redesign** (`docs/2026-11-19-window-design.md` §11): an ask's split is the supervisor's, made without a press
  (D341, UIWF2, UIWF6), and the workflow step editor goes for a before-and-after card (D342, UIWF8, UIWF10). Today the rules
  below stand; each row rewrites the one it makes false.
- **RISKGATE1** (D354): every hold, approval and acceptance read from one risk policy by one judge (an act's class at its place:
  `none` passes, `role` waits on a role, never the author, `owner` on the owner), then one hold per change deleting its hard-coded
  holder; only `policy.change` is fixed at the owner. The judge runs in shadow (roles-and-supervision rule 49b): each run step's hold
  (an unchecked done, an Accept, an approval, a go-ahead, a recovery wait) and each declaration owed acceptance journals one
  `risk.judged` line at its open and its release beside what today's gate did, deciding nothing. Today the holds below stand; each
  row rewrites the one it moves.
- **GATESPLIT1** (D355): the declaration split into what travels (a `daoris/` folder, one file per gate, accepted per unit by a
  digest under D354's `command.change`), the machine's values in the home, learned from green wall times, and one resolved reading
  for every reader. Today `daoris.gates.json` and its acceptance stand as rule 4 says (D350).
- **TEAMWORK1f** (`docs/2026-11-12-teamwork-design.md` §7): the train, the `parts` kind, the tagged landing, the two shipped workflows, the
  line's tip merged into the ask's branch, its batch refs and the ask-keyed delivery run (rule 39b) are built; left are the person's one item
  for the run's Accept (today a run is placed in Sessions and *What needs you* by a quest's session, which the delivery run has none of, and
  the run view's `--ask` read does not list it), the verdict store's replay and the red's bisect read part by part.
- **THROUGH1** (D277): a direct ask taken through to its pull request on the person's quoted words.
- **APPROVE1y**: the work workspace's API token on its git identity, the person's act.
- **CHATFLOW1**, **SETUPUX1x**, **NODEKIT1**, **FABRIC-SQUASHCHAIN1**, **FABRIC-MILESTONE1c**: the installed look.
- **WORKSETUP1**: the first place composing a pull request end to end; its tail AUTOTIDY1c, SQUASHTIDY1d, PLUGHOOK1d, FABRIC-PERFORM1l.
- **INTEGRATE1 k, m**: this repository's integrator flipped and a sibling's declared integration; h's installed proof.
- **FABRIC-PIPELINE1 g, h**: this repository's pipeline on the install, a stage after the check.
- **WORKFLOWEDIT1c**, **LANDNODE1j**, **REVIEWFLOW1c**, **LANDPRESS1**: the installed window.
- **PROCSTEPS1** (`docs/2026-11-09-process-steps-design.md`): `resolve`'s loop resolvers, `merge`, `tidy`'s removals, the cut-over.
- **Roles** (D304 to D309): workflows written in roles that do not tell a person from an agent, evolved in place in one scope (D309), the reporting line kept apart from authority (D305), acts spending only what their grants cover (D306) and re-checked at their effect (D307), consultations recording their mode (D308). Every step names the role that takes it in its `role` field
  (`NodeDeclaration.Role`, in the version's digest), its kind's default where it names none (`StepRoles.Of`). A save writes none where a
  step that no agent session takes names its kind's own (`StepRoles.Unwritten`), so such a version is the one an older build reads and
  closes no going back; one naming another role closes going back below the build that saved it (`roles-since.txt`), which the save's
  review says first (`closesBelow`), and a preset's second opinion names none. The save refuses a holder
  named there (`role-holder`) and a role no catalogue has (`role-unknown`); the editor sets it (`StepRoleField`, applied to the newest draft by
  its `onRole` act), the chart and the run view draw it, and an older version's agent-step `role` option is read as the role by the one
  reader (`StepRoles.Named`, the page's `namedRole`) until no kept version names one (`roles-since.txt`). Who settles a step is its
  holder's, by one door (D312, rule 18), and what it waits for is its kind's (rules 51 and 55). `you` is a holder word
  (`HolderWords`): a step whose `start` or `accept` says it is first held by the person through its tenure (`AttemptTenures`), whatever
  the place casts its role to, and no runtime code branches on it. The named steps' reading, gate and diff (`NamedStepReading`,
  `Involvement` in `WorkflowGate`, `WorkflowDiff`) read a stage's `start` and a landing's `accept` the same way, as the holder a version's
  step casts (`HolderWords.First(NamedStep)`), and a census holds them to it beside the runtime.
- **DELIVERY1** (`docs/2026-11-13-delivery-framework-design.md`): an ask's repositories delivered under one plan. A part waiting on
  another's receipt and the joint check are rule 3f's, on the plan's edges and the parts' receipts; left: the plan's release orders
  read at run time (the upstream release before a release, the production example), a release target naming its deployable and the
  earliest act that deploys, and every provider write a step makes judged and journaled; until then nothing reads a `wait.unit`'s
  commitment at run time and an ask's release order is kept by hand.
