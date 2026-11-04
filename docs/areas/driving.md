# Driving — how it works now

## What it does

Driving turns requests into running sessions on this machine. A quest asks one repository for work. An ask is a sentence
the person enters at a workspace; an intake session reads it and publishes quests. The desktop's driver loop looks
again and again. At each look a pure planner decides which quests start and why every other one sits. Each start opens
a session record, a working tree and an agent process. The session takes, finishes or declines its own quest. The
driver only observes: it concludes each end from the exit code and the quest's state, carries cut-off work on, resumes
the agent's own conversation where it can, and brings a quiet session to the person. Nothing is ended on a clock.
Finished trees are tidied by the look once git proves their work has ended. The service keeps the quest, ask and
session records and judges every move for every door.

## Its parts

Paths are under `src/Daoris.Desktop/Daoris.Desktop.Driver/` unless they start with `Service/`, which is
`src/Daoris.Service/Daoris.Service.Core/`.

| Unit | File | Its job |
|---|---|---|
| `Driver` | `Loop/Driver.cs` | One look (`LookAsync`) and one quest's start (`RunAsync`): holds, tree, record, spawn, wait, conclusion |
| The look's passes | `WorkOrders/Look/`, listed in `docs/index/registered.md` | Each pass a module registers (`[LookPass(name, phase, after)]`, `ILookPass`), run at its phase's anchor in `LookAsync` in the kit's order and built with the driver as its `ILookWiring` |
| `LookState` | `WorkOrders/Look/LookState.cs` | What a look carries from pass to pass on its `LookRound`: the snapshot as each pass shapes it, what a pass marks for the ones after it, and what ended; a member read before the pass that writes it throws, naming that pass |
| `LookSync`, `LostClaimStop` | `WorkOrders/` | The look's head: the remote sync, then the stop of each session whose take lost |
| `PluginReload` | `Plugins/PluginReload.cs` | The look's local phase, first: the plugins read fresh, taken up through `IPluginWiring`, the hooks reconciled |
| `RuleSettling`, `WatchLook`, `MovedLines`, `TogetherSteps` | `WorkOrders/` | The local phase, in this order: the rule proposals settled, the session watch, the lines Daoris moved followed, the operations bringing sessions together stepped |
| `WorkspacePlansLook` | `Setup/WorkspacePlansLook.cs` | The local phase's last: every workspace plan worked once, through `ISetupWiring` |
| `SnapshotLook`, `PausedLook`, `SupervisorLook` | `WorkOrders/`, `WorkOrders/Work/` | The read phase, first: the roster's mark and the starts opening taken, then the snapshot; the paused work held; the supervisor's decisions moved, its unattributed words taken back, and the look's answers-alone bound left in the state |
| `AloneLook`, `UnableLook`, `PiecesOnFileLook`, `HandOversLook`, `StaffPolicyLook`, `CommittedLook`, `OverlapsLook`, `OverlapsToldLook`, `RosterLook` | `WorkOrders/`, `WorkOrders/Work/` | The read phase, shaping the snapshot in this order: the answers that would go on alone marked; the words a record could not go on with; the pieces on file; the quests handed to another agent; the staff course's moves and holds; the commits a failed session left; what running units changed and the overlaps told; the roster told what started, leaving `LookState.Looked` |
| `ChannelLook`, `ReopensLook`, `ReviewsLook` | `WorkOrders/`, `Review/` | The read phase, from `Looked`: the ask's channel, the sources' reopens (their taken-off words leave the snapshot), the set-ups' tabs |
| `OpinionsLook`, `HelpersLook`, `PartnersLook`, `BrainstormsLook`, `MeetingsLook` | `Review/`, `Participants/` | The read phase's last: the second opinions owed, then the helpers, partners, brainstorms and meetings, each started beside the look and counted into `LookState.Passes` under the cap. They reach the driver's classes through `ILookStagesWiring` |
| The plan | `Loop/Driver.cs` | Not a pass: it writes `LookState.Starts`. The `Planned` phase follows it: `AnsweredIntakesLook` ends a parked intake once its ask is answered, `IntakesLook` finds the asks due an intake in the slots left. The starts are begun in `LookAsync`; it writes `LookState.OpenedStarts` |
| `BindRunsLook`, `EndingsLook` | `Workflows/`, `Participants/` | The `Begun` phase: the runs the starts opened bound; what ended reported, told to the plugins' hooks and synced (the idle loop asks the same `EndAsync`) |
| `UnresolvedDeliveries` | `Workflows/UnresolvedDeliveries.cs` | The look's end: the deliveries a stopped run left unresolved, settled by the lease's holder |
| `DriverWatch` | `Loop/DriverWatch.cs` | The loop: when a look runs (pace, nudge, wake), whole look or plan only |
| `LookStages` | `Platform/LookStages.cs` | Times each stage of a look and the work beside it, says a slow one |
| `DriverIntakes` | `Loop/DriverIntakes.cs` | Asks due an intake, the intake's run, ending a parked intake once its ask is answered |
| `DriverContinuations` | `Loop/DriverContinuations.cs` | The person's words continue their session: a park or an ended record goes on |
| `Planner` | `WorkOrders/Domain/Planner.cs` | Pure plan over a `Snapshot`: `Start` or a `StartVerdict` with its sentence; strikes; the carry-on rule |
| `CourseDecision` | `Course/Domain/LineCourse.Decide.cs` | The one decision of how a line goes on: resume, move to a new part, wait, hold, refuse |
| `DrivenCourse` | `Course/GoOn/DrivenCourse.cs` | A driven line's facts handed to the course, and its answers |
| `DrivenInbox` | `Course/DrivenInbox.cs` | Words to a working driven session: held, delivered at the next step or turn end; the person's turn stop |
| `LimitResume` | `Course/LimitResume.cs` | A limited line resumes its own conversation once its account is ready |
| `MoveStore` | `Course/MoveStore.cs` | The durable record of each move of a line, taken up by the next loop after a dead one |
| `Observation` | `Events/Domain/Observation.cs` | A run's end concluded from exit code and quest status, with its coded note |
| `PartEnds` | `Events/PartEnds.cs` | The class of each end, read from the journal row alone |
| `SessionWatch` | `Events/SessionWatch.cs` | Marks a quiet session and brings it to the person; ends nothing by itself |
| `TransientRetry` | `Agents/TransientRetry.cs` | Retries a transient failure in the same conversation on a fixed backoff |
| `DriverConfig` | `Settings/DriverConfig.cs` | Drivable, holds, cap, trees, strikes, releases, poll; the kept-but-unread `timeoutMinutes` |
| `SessionTrees` | `Workplace/SessionTrees.cs` | Opens a fresh tree; the front over every tree part (`TreeContext`) |
| `TreeDependencies`, `TreeLocalInputs` | `Workplace/` | Copy installed dependencies and declared machine-local files into a tree as it opens |
| `AutoTidies`, `TreeSetAside`, `TreeSweeps` | `Workplace/` | The look's own tidy, the move aside, and the clean-up press's one path |
| `EndJudgement`, `EndKeeping` | `Workplace/EndJudgement.cs`, `Workplace/EndKeeping.cs` | Whether a branch's work ended, from git alone, and the one writer of that fact |
| `Orphans` | `Workplace/Orphans.cs` | The sweep: a record that says it runs with nothing running it is stopped as interrupted |
| `QuestStore`, `QuestExchange` | `Service/Quests/Quests.cs`, `Service/Quests/QuestExchange.cs` | Quest records and log; every door's judgement; claim by push |
| The quest log's operations | `Service/Quests/Operations/`, listed in `docs/index/registered.md` | Each kind a module registers (`[QuestOperation(kind, Log)]`, `IQuestOperation`): its log payload written and read, what a lost one takes with it in a rebase, and its verbs on the `QuestTx` the store's one-line door opens; built as the store opens, refusing a kind declared twice or a mark naming another kind |
| `QuestHandbacks` | `Service/Quests/QuestHandbacks.cs` | A session's closing note on its quest |
| `SessionStore`, `SessionLedger`, `Lines` | `Service/Sessions/` | Session records, the ledger's opens, moves and says, the tree lock, a line's head |
| `AskStore`, `AskDesk` | `Service/Asks/Asks.cs` | Ask records, the person's words on them, publishing and deleting |

## Its rules

### Quests and the take

1. A quest is `Open`, `Taken`, `Done` or `Declined`. It goes to a registered repository in the same workspace, and its
   body says why with evidence, never the change. (from D32, D34, D70)
2. The driver never writes quest state. The session it starts takes, finishes or declines its own quest through its own
   connector, so driven and outside work look the same at the quest layer. (from D46)
3. A take on a quest shared through a remote claims by push before work starts. It is held, lost (the session stands
   down), or unconfirmed (work goes on until a sync decides). (from D68, D69)
4. Each whole look asks the host where every running session's claim stands. A session whose take lost is stopped,
   and the record says why. (from D68, D69)
5. Two machines' takes on one quest end as a conflict. The loser's work is kept; the person drops it or brings it
   forward as a fresh take that its kept session goes on from. (from D178)
6. A quest nobody has started on may be deleted, and the delete travels as an operation. (from D95)
7. A session's handback is the note it gave at done, ready or decline. It is written once, in the same write as the
   move, kept with the session that wrote it, and read whole. (from D219)

### Planning a look

8. The planner is pure and plans over one snapshot. The service's ledger judges every open again; the plan only
   avoids asking for what the ledger would refuse, and gives each refusal a sentence first. (from D46)
9. Only repositories the person opted into are driven. A held repository starts nothing new. A repository needs a
   registration, adoption where the door needs it, and a root. (from D46, D70)
10. Quests whose last session here has the person's words waiting are planned first. The rest go in the service's
    order, oldest open first. (from D46, D137)
11. The cap counts what the driver started, passes included (helpers, partners, opinions, brainstorms), never a
    conversation the person holds. It is at least one. (from D130, D181)
12. In a repository without its own trees, one session runs at a time, in the root. With trees on, sessions run side by
    side, one per tree; only a resume or a carry-on goes back into an existing tree. (from D51, D167)
13. A quest held by a session in flight does not start again: one working it, or a start still opening. Each start is
    reserved (quest, tree, place under the cap) before it begins. (from D181)
14. Under the landing rule's `overlap: wait`, a first start whose declared paths meet earlier unintegrated work in the
    repository waits until that work is integrated. (from D215)
15. Failed sessions on one quest park it at the strike count, three by default and `0` for never, derived from the
    records. Only `failed` and a stop that was not the person's count. On a home whose role rules are reached, the
    count is a seeded rule. *Try again* marks where counting restarts. (from D58, D125, D229)
16. A person's stop holds the quest here until they release it. A pause holds an ask's or quest's work here until
    *Resume*. Another machine may still take an open one. Neither is a strike. (from D126, D132)
17. A taken quest whose take is not this machine's sits. Nothing carries it on here. (from D80, D178)
18. At each start the plugins are asked first, and a hold names the plugin. A start held at spawn (a dirty root, a
    harness absent or signed out, a tree that would not grow, trust not given) reports its sentence as the quest's
    reason. (from D64, D73)

### Start, tree and resume

19. The record opens before the spawn. It names the quest, repository, adapter, tree, access setup and the actor's
    choice. The tree, profile and waiting words are machine-local and never travel. (from D47, D49, D184, D205)
20. A fresh tree is a linked git worktree at `<home>/work/trees/<workspace>/<repository>/s-<8 hex>`, on a new branch
    `daoris/s-<8 hex>`. It grows from the repository's line, or from the branch a chain's step before landed on. A root
    that is not the top of its own repository is refused. (from D51, D82, D86)
21. While the tree is locked as opening, it gets copies, never links, of the checkout's installed dependencies and of
    the machine-local files the gates declare, read-only. A failed open removes its tree and branch. (from D51, D216)
22. A tree serves one line of work and is never handed to another. (from D51)
23. A session commits on its tree's branch as it goes. An agent that moves its tree onto a branch of its own is
    followed there. (from D164)
24. One decision says how a line goes on after any trigger (words kept, an answer, a limit, a transient bound, a
    carry-on, a hand-over): resume, a new part, wait, hold or refuse. (from D137, D189, D206, D212)
25. The agent's own conversation is resumed only by the id Daoris kept, where its agent, account and tree still hold.
    Otherwise a new part opens and is handed Daoris's brief, composed without a model. (from D137, D189)
26. A taken quest is carried on in the same tree when its last session here failed, was cut off by the sweep or a
    shutdown, was a person's stop they released, or parked and was answered. (from D80, D83, D104)
27. A session that asks another repository a quest ends completed and waits. Its quest stays taken, and it resumes in
    the same tree once that quest is answered. (from D79)

### Words to a session

28. The person's words reach a session in any state: at its next step where its door takes words during a turn, else
    at its turn's end, else by reopening its ended record. The only move out of an ended state is back to working
    with words waiting. (from D137, D188)
29. Answers alone that would take a line on a third time in a row wait for the person. (from D188, D229)

### The look, timeouts and limits

30. The loop looks at its pace (15 s by default), on a nudge, and on a wake (a ledger change, a terminal's words, a
    session's end). A whole look syncs with the remote and stops lost claims; a wake only plans. A slow start runs on
    beside the look, reserved. (from D68, D181)
31. A working session has no time limit: a driven run, a resumed run, a go-on and an intake wait for their exit. Only a
    pass with minutes of its own is bounded (a helper 15, a partner 45, a second opinion 20 within 5 to 120).
    `timeoutMinutes` is kept in the file and read by nothing. (from D200)
32. The watcher marks a session quiet after Q (10 minutes) with nothing heard and its job idle, and brings it to the
    person at 3Q. While it waits on its own background work these are 3Q and 5Q. A wait for a permission answer is
    never quiet. Daoris ends nothing quiet but a dead process and a headless `--quiet-end`. (from D200)
33. An account's limit is read from the agent's own words. It cools that account until the reset it names, and is
    never a strike. The record ends `failed` with its `limited` flag and reads as *limited*. (from D125, D206)
34. A limited driven line resumes by itself: its own conversation when its account is ready; else a briefed part on
    another account or the role's next choice; else it waits and shows the earliest reset. An account the person
    picked is never moved off. (from D205, D206)
35. A transient error is retried in the same conversation after 1, 2, 4, 8 and 16 minutes, and is never a strike.
    Past that the line waits for the person's word or *Try again*. Access goes to the person; a fatal error fails with
    its reason. (from D210)

### States and the end

36. A session record moves forward only: `Queued`, `Starting`, `Working`, `AwaitingPerson`, then `Completed`,
    `Declined`, `StoodDown`, `Failed` or `Stopped`. Every live state may be stopped. A parked session still holds its
    tree. (from D46, D51, D83)
37. The end is observed, never self-reported. Done ends completed and a decline ends declined. A clean exit holding its
    own quest parks as `AwaitingPerson`, quoting its last words. A clean exit on another's take stands down. A messy exit
    with the quest taken fails. (from D46, D83)
38. On the native door, a turn that ended while its own background work ran is cut off as failed and carried on. On
    the protocol door the session keeps waiting on that work. (from D83, D105)
39. A park never resumes by itself. The person answers it, or an appointed supervisor's word that the driver can
    attribute. (from D83, D208, D229)
40. Each end gets one class from its record: completed, failed, limited, transient-bound, access, stopped by the
    person, a role or Daoris, refused-choice, awaits-helper, stood-down, or unknown. A limit outranks a good end or one
    Daoris made. (from D173, D206, D210)
41. The sweep stops a record that says it runs with nothing running it, as interrupted. A shutdown kills each session's
    process tree and records the same. Both are carried on. (from D104)
42. One writer holds a home: an instance for its life, a verb for its act. (from D104)

### Asks and intakes

43. An ask is a sentence entered at a workspace, kept verbatim with every later word of the person's. It is `Open`,
    `Proposed`, `Published` or `Closed`, and `Done` derived from its quests. Every session on it is handed its words,
    and a requirement must quote them. (from D65, D133)
44. An intake is a session in the workspace's room under `<home>/work/rooms/intake`, recorded as a chat that names its
    ask. It reads the declarations and publishes quests. It runs only where an Intake actor is set, one per ask, in
    slots the quests leave. A parked intake ends when the person answers the ask. (from D65, D205)
45. An ask's or a quest's work is paused and resumed whole here, and abandoned by a listed second press that discards
    only what nothing else holds. (from D132)
46. The records of one line share an entry key, `<origin>/<repository>`. A line's head is found by walking `from`
    forward, at most 256 hops, and a cycle or the bound reads as no head. (from D167, D212)

### The tidy

47. A branch's end is judged from git alone: claim, handed, ended or unknown. Ended means merged into the line, on the
    line by content, carried by a pull request whose merge is here, or inside an ended landing. An agent's done is a
    claim. An unread proof ends nothing. (from D88, D102, D148)
48. The look's tidy takes, with no press, a session branch the clean-up would clear: empty, landed by ancestry or
    content, or carried. With a tree, only this home's tree on a branch that moved. Landed work goes only once it is on
    the line. The folder is moved aside into `.tidied`, never deleted; the keeping pass purges it. (from D88, D195)
49. A tree holding only ignored paths or the repository's declared output goes with its branch. Uncommitted or
    untracked work keeps it. (from D195, D218)
50. The person's clean-up press lists every session branch with what it holds and removes those the proof clears,
    judging each again right before it goes. (from D88)

## Where a change goes

Put new behaviour in a unit of its own, beside the subdomain below (D231). Do not grow `Loop/Driver.cs`,
`Service/Quests/Quests.cs` or `Service/Sessions/SessionLedger.cs`: each is over the size budget.

| A new behaviour about | Goes in | Tests that pin it |
|---|---|---|
| whether a quest starts, and why it sits | `WorkOrders/Domain/Planner.cs` (a `StartVerdict` and its `SittingFact`) | `WorkOrders/PlannerTests`, `WorkOrders/ParkedQuestsTests`, `WorkOrders/OverlapPlannerTests`, `StrikeTests`, `Loop/CapCountTickTests`, `Loop/PauseTickTests` |
| how a line goes on after an end, words or a limit | `Course/Domain/LineCourse.Decide.cs` only; executors in `Course/` | `Course/LineCourseTests` with `course-cases.txt`, `Course/CourseCensusTests`, `Course/LimitWaitsTests`, `Course/LimitResumeTickTests`, `Loop/AnswerContinuesTickTests` |
| what a run's end is | `Events/Domain/Observation.cs`, its code in `Events/NoteCodes.cs`, its class in `Events/PartEnds.cs` | `Events/ObservationTests`, `Events/PartEndedTests`, `Loop/AskAndWaitTickTests`, `Loop/OrphanedSessionTests` |
| a part of the look | a pass of its own in its subdomain, marked `[LookPass]`; what it reaches of the driver goes on `ILookWiring`, or on its module's own wiring interface where the type sits above Work orders; what it reads of an earlier pass or leaves for a later one goes on `LookState`, written by one pass | `Loop/DriverPartsTests`, and the tests of the stage it moves |
| a kind of quest operation, or a verb of one | a module of its own in `Service/Quests/Operations/`, marked `[QuestOperation(kind, Log)]`, its payload on `IQuestOperation` and its verbs on `QuestTx`; the store gains only the verb's door | `Quests/QuestsPartsTests`, and the tests of the verb it moves |
| when a look runs | `Loop/DriverWatch.cs` | `Loop/LookStagesTests`, `Loop/DriverWatchLookTests`, `Loop/LookParallelTests`, `Loop/SessionsOutliveTheirLookTests` |
| quiet sessions | `Events/SessionWatch.cs` | `Events/SessionWatchTests`, `Events/SessionWatchProcessTests`, `Loop/QuietEndProcessTests` |
| transient failures | `Agents/TransientRetry.cs` | `Loop/TransientRetryTickTests` |
| opening, following or removing a tree | a class over `TreeContext` in `Workplace/`, wired in `SessionTrees`' front | `Workplace/SessionTreesTests`, `Workplace/FreshTreeTests`, `Workplace/TreeDependenciesTests`, `Workplace/TreeLocalInputsTests`, `Workplace/TreeReturnsTests` |
| the tidy and branch ends | `Workplace/EndJudgement.cs`, `Workplace/TreeSetAside.cs`, `Workplace/AutoTidies.cs` | `Workplace/EndJudgementTests`, `Workplace/EndJudgementGitTests`, `Workplace/BranchTidyingTests`, `Workplace/TreeScratchTests` |
| a rule every door shares | `Service/Quests/QuestExchange.cs`, `Service/Sessions/SessionLedger.cs`, `Service/Asks/Asks.cs` (`AskDesk`) | `Quests/QuestExchangeTests`, `Quests/QuestTakeBindTests`, `Quests/QuestDeleteTests`, `Quests/QuestHandbackTests`, `Sessions/SessionLedgerTests`, `Sessions/SessionLockAcrossHostsTests`, `Asks/AskDeskTests`, `Remote/QuestSyncTests` |
| intakes | `Loop/DriverIntakes.cs`, `WorkOrders/IntakeRoom.cs`, `WorkOrders/IntakeKinds.cs` | `WorkOrders/IntakeTests`, `WorkOrders/IntakeRoutingTests`, `WorkOrders/IntakePromptGoldenTests`, service `Asks/IntakeTests` |

Driver tests are under `src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/`; service tests under
`src/Daoris.Service/Daoris.Service.Tests/`.

The gates that bite: `driver` (which also runs `StructureTests`: the module structure and the size ratchet), `censuses`,
`driver-process` and `driver-process-git` (real processes and git, run serially), `service`, and `orient-index`,
`test-map` and `code-map` for any new file. A changed driven loop also meets the family rehearsal
(`npm run rehearse:family`), whose stub-adapter phase proves the loop.

## Contradictions

- D46 and D51 say one driven session per repository; `WorkOrders/Domain/Planner.cs:1416-1418` runs sessions side by
  side, one per tree, where the repository opens its own trees. The code reads right (as D115 and D167 already say).
- D46 says a fresh session per quest; `Course/Domain/LineCourse.Decide.cs:164-185` resumes the agent's own conversation
  where it holds. D189 reads right.
- D51 rule 2 says a tree is opened for one line and never reused; D214 hands trees from line to line. D214 is proposed
  and unbuilt, and `Workplace/SessionTrees.cs:362-364` follows D51. D51 reads right today.
- D83 says the person answers a park; `Service/Sessions/SessionLedger.cs:1208-1381` lands an appointed supervisor's
  attributed word on a park. D208 and D229 read right.
- D88 counts work on any branch of the person's as landed; `Workplace/EndJudgement.cs:322-326` calls it handed, not
  ended, and `Workplace/TreeSetAside.cs:23-30` makes the look wait for the line; only the press goes on D88's proof.
  The code reads right.
- D58 makes `strikes` in `driver.json` the count; D229 makes it a seeded rule and refuses the key on a reached home
  (`Settings/DriverConfig.cs:47-49` is marked transitional). D229 reads right.
- D228 says a superseded way's doors go with it; `Settings/DriverChoicesCommand.cs:485-486` and
  `src/Daoris.Service/Daoris.Service.Mcp/ChosenSettingsTools.cs:47-54` keep the retired `timeout` doors, which only
  refuse. The code is today's; ONEWAY1s removes them.
- D81 sets a driven session's posture to the harness's `auto`; D184 sets it from the repository's access setup,
  written on the record at the open (`Service/Sessions/Sessions.cs:208-217`). D184 reads right.
- D131 falls back to a plain carry-on when a conversation cannot resume; D189 hands the new part Daoris's brief
  (`LineCourse.Decide.cs:271-306`). D189 reads right.
- D65 point 4 says a workflow is a chain of quests; D157 and D190 make it a named, versioned graph that governs a
  bound run, with chains kept as its steps. D190 reads right.

## Open

- **D214** (no backlog row): a repository's pool of trees handed from line to line; proposed, both makers' reads of
  round 3 owed.
- **SESSWATCH1b**: Daoris's word to a quiet session.
- **SESSWATCH1c**: `quietMinutes`, the one quiet setting, in every door.
- **SESSWATCH1d, 1e**: the record of quiet episodes, then an automatic end only where it shows such quiet is not work.
- **SESSWATCH1f**: each pass asked for its own minutes.
- **ONEWAY1s**: the retired timeout doors removed.
- **ONEWAY1j / SESSCTX1e**: *Go on fresh*, one press per go-on case.
- **SESSCTX1a**: resume and a fresh part measured on each door.
- **LIMITRESUME1** (under ONEWAY1g): what is left of the limit resume.
- **AGENTERRORS1** (rows 1c to 1e): the one error policy built into the agent interface.
- **AGENTPROVIDER1m-i**: every executor (the driven start, continuations, intakes, passes) holds `AgentRun`.
- **TOOL4n**: a driven Claude Code session that ends on a limit on the native door cools its account.
- **TREEOPENERR1**: the next fresh-tree open that fails with no stderr, read and reported.
- **LINESTOGETHER1e**: the steward's forms for several lines in one repository.
- **D178's last clause** (no row): handing a lost take's work to the winner waits for a way to reach it.
