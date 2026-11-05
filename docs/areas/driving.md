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
| `Driver` | `Loop/Driver.cs` | One look (`LookAsync`) and one quest's start (`RunAsync`), each running its passes or stages at each phase's anchor in turn; the driver holds the order and the wiring alone |
| A run's stages | `Loop/Run/`, listed in `docs/index/registered.md` | Each stage a module registers (`[RunStage(name, phase, after)]`, `IRunStage`), run at its phase's anchor in `RunAsync` in the kit's order and built with the driver as its `IRunWiring`; a stage that returns a `StartRun` holds the start there, and no stage after it runs |
| `RunState` | `Loop/Run/RunState.cs` | What a run carries from stage to stage on its `RunRound`: the agent, the account and how it was walked, the access setup, the tree and the repository's trees held until the record is open, the binding, the set-up step, what the continue stage settled and the opened record; a member read before the stage that writes it throws, naming that stage; the state lets go of the held trees and the carry-on's move record with the run |
| `GrowTree` | `Loop/Run/Tree/` | The `Grown` phase: the tree the session runs in, found where an earlier session worked or grown from the line, with the repository's trees held until the record is open; a hold lets the fresh tree go |
| `StartAgent`, `CleanCheckout`, `AccountWalk`, `StartAccess` | `Loop/Run/Choose/` | The `Choose` phase, in this order, before anything is grown, locked or opened: the agent, a clean checkout, the account the walk or a resume takes, the access setup |
| `StewardPlace`, `RunBinding`, `SetUpStep` | `Loop/Run/Placed/` | The `Placed` phase, once the tree is open and held and before the record opens: a steward's start, the workflow binding and the role it names, a set-up step's procedure and tab; a hold lets the fresh tree go |
| `TrustedTree`, `GoOnRecord` | `Loop/Run/Trust/`, `Loop/Run/Continue/` | The `Cleared` phase: whether the harness trusts the tree (`TrustAsk`, asked again of an account a resume's words walk to), then the record the person's words wait on, continued or handed on in a new session |
| `OpenRecord` | `Loop/Run/Open/` | The `Opened` phase: the record opened through the ledger, the carry-on's move record fenced before it, and how the account was chosen said on it |
| `HandToSession`, `RunConclusion`, `RunEndings` | `Loop/Run/Hand/`, `Loop/Run/Conclude/`, `Loop/Run/Ending/` | The `Handed` phase, whose one stage ends the run: the instruction composed and the spawn assembled, the hold's conclusion of the record, and the ends that are not a conclusion (the driver closing, a wire broken after a stop, a failure) |
| `SessionHold`, `DrivenProtocolDoor` | `Loop/Run/Hold/` | A prepared session started and held to its conclusion for every kind of run the driver starts (`Driver.Holds`), and the protocol door's driven session that hold opens |
| The look's passes | `WorkOrders/Look/` (the pass's kit), `Loop/Look/` (the passes that reach the loop's own classes, through `ILookStagesWiring`), listed in `docs/index/registered.md` | Each pass a module registers (`[LookPass(name, phase, after)]`, `ILookPass`), run at its phase's anchor in `LookAsync` in the kit's order and built with the driver as its `ILookWiring` |
| `LookState` | `WorkOrders/Look/LookState.cs` | What a look carries from pass to pass on its `LookRound`: the snapshot as each pass shapes it, what a pass marks for the ones after it, and what ended; a member read before the pass that writes it throws, naming that pass |
| `LookSync`, `LostClaimStop` | `WorkOrders/` | The look's head: the remote sync, then the stop of each session whose take lost |
| `PluginReload` | `Plugins/PluginReload.cs` | The look's local phase, first: the plugins read fresh, taken up through `IPluginWiring`, the hooks reconciled |
| `RuleSettling`, `WatchLook`, `MovedLines`, `TogetherSteps` | `WorkOrders/` | The local phase, in this order: the rule proposals settled, the session watch, the lines Daoris moved followed, the operations bringing sessions together stepped |
| `WorkspacePlansLook` | `Setup/WorkspacePlansLook.cs` | The local phase's last: every workspace plan worked once, through `ISetupWiring` |
| `SnapshotLook`, `PausedLook`, `SupervisorLook` | `WorkOrders/`, `WorkOrders/Work/` | The read phase, first: the roster's mark and the starts opening taken, then the snapshot; the paused work held; the supervisor's decisions moved, its unattributed words taken back, and the look's answers-alone bound left in the state |
| `AloneLook`, `UnableLook`, `PiecesOnFileLook`, `HandOversLook`, `StaffPolicyLook`, `CommittedLook`, `OverlapsLook`, `OverlapsToldLook`, `RosterLook` | `WorkOrders/`, `WorkOrders/Work/` | The read phase, shaping the snapshot in this order: the answers that would go on alone marked; the words a record could not go on with; the pieces on file; the quests handed to another agent; the staff course's moves and holds; the commits a failed session left; what running units changed and the overlaps told; the roster told what started, leaving `LookState.Looked` |
| `ChannelLook`, `ReopensLook`, `ReviewsLook` | `WorkOrders/`, `Review/` | The read phase, from `Looked`: the ask's channel, the sources' reopens (their taken-off words leave the snapshot), the set-ups' tabs |
| `OpinionsLook`, `HelpersLook`, `PartnersLook`, `BrainstormsLook`, `MeetingsLook` | `Review/`, `Participants/` | The read phase's last: the second opinions owed, then the helpers, partners, brainstorms and meetings, each started beside the look and counted into `LookState.Passes` under the cap. They reach the driver's classes through `ILookStagesWiring` |
| `LoopRuns` | `Loop/Look/LoopRuns.cs` | The one-look run and the gate's run-until-idle, and the wait for the sessions they started (the sync and the lost-claim stop beside them, the endings after); the driver's `RunOnceAsync` and `RunUntilIdleAsync` are its doors |
| `RunDoors`, `LookDoors` | `Loop/Run/RunDoors.cs`, `Loop/Look/LookDoors.cs` | The doors the run's stages (`IRunWiring`) and the look's passes (`ILookWiring` and the interfaces over it) reach the driver through, one line each to the driver's member or the unit that owns it; the stages and passes are built on them, never on `Driver` |
| `SessionEquipment` | `Loop/Session/SessionEquipment.cs` | The spawn prepared for either kind of session: the adapter's process, the toolchain's environment and tools-up-front switch on it (`Equip`, shared with the conversation's run), a dsh home's profile, and whether the handed rules let the connector's tool through |
| `LineageBrackets` | `Loop/Session/LineageBrackets.cs` | A process's lineage token issued before its spawn and its end written once the exit is read and the capture drained, for the driven run and the conversation's alike |
| `PartsHanded`, `MovedParts`, `StartTargets` | `Loop/Session/` | What a start hands beyond the quest: the earlier parts of its session, the line saying a part moved and the record's `from` link, and the target's words, language and go-aheads; the bind of the run to its workflow and its step's role are `StartBinding` (`Loop/Run/`), a stopped run's record `StoppedRecord` |
| `SessionRules` | `Loop/Session/SessionRules.cs` | The permission rules a session is handed: the machine's rules composed for its workspace and repository, shaped by its job and kind, written to its settings file, and the account of what was handed |
| `AccountEndings` | `Loop/Session/AccountEndings.cs` | What a session's ending says of its account: a limit, a refused credential or model, a sign-in refused, read from the door's failure and applied to the roster; the driver's `AccountLimited` and its kin are its doors |
| `SessionWatching` | `Loop/Session/SessionWatching.cs` | A run whose root has exited concluded by what it said within the capture bound (a held-open pipe ended, an unfinished capture abandoned), and the watcher's look at the running sessions, each step a line |
| `QuestWalk` | `Loop/Session/QuestWalk.cs` | The walk over the order of choices a quest's start takes: the role's order, the start plan with its passes and gate, the role taken, the moves counted, and the re-ask of a handed quest whose agent refused its model; the driver's `Walk` |
| `StartSentence`, `StartLanding` | `Loop/Session/` | The creating sentence a start's record carries while it runs (its tree's opening, a resume, a carry-on and why); and what a start's landing is named for, a chain's step landing on its chain's branch |
| `SessionWaits` | `Loop/Session/SessionWaits.cs` | How a driven session's turn waits and ends: the background-work judgement, the turn-end look at a quest closed with uncommitted work, the bounds on a lingering process, and a given-up run's capture observed |
| `LookKept` | `Loop/Look/LookKept.cs` | The look's first pass: a cool-off the file would not take written again, a lifted drain's held word freed, the conversations' and notices' files moved into the store |
| `PlanLook` | `Loop/Look/PlanLook.cs` | The read phase's last: the plan made over the snapshot with the starts still opening held, under the cap the passes' slots left; it writes `LookState.Plan` and `Starts` |
| `AnsweredIntakesLook`, `IntakesLook`, `StartsLook` | `Participants/`, `Loop/Look/` | The `Planned` phase: a parked intake ended once its ask is answered; the asks due an intake in the slots left; then every claimed start and intake begun at once (`BegunStarts`, the run of each in `ClaimedStart`) and settled, writing `LookState.OpenedStarts` and `Outcome` (`StartsOutcome`: held, signed out, untrusted, waits by `AccountWaits`, a limited line's news by `LimitedStart`). `StartDoor` reads the door the plan rides. They reach the driver through `IStartsWiring` |
| `BindRunsLook`, `EndingsLook` | `Workflows/`, `Participants/` | The `Begun` phase: the runs the starts opened bound; what ended reported, told to the plugins' hooks and synced (the idle loop asks the same `EndAsync`) |
| `UnresolvedDeliveries` | `Workflows/UnresolvedDeliveries.cs` | The look's end: the deliveries a stopped run left unresolved, settled by the lease's holder |
| `DriverWatch` | `Loop/DriverWatch.cs` | The loop's surface: its nudges, its doors and its settings; each run is a `WatchRun` |
| `WatchRun` | `Loop/Watch/WatchRun.cs` | One run of the loop: the parts it starts beside the looks, and when a look runs (pace, nudge, wake), whole look or plan only |
| `WatchWakes`, `WatchDoors` | `Loop/Watch/` | What a nudge says, ordered with no lock; what a running loop lends the watch's surface, published one reference at a time |
| `WatchNodes`, `WatchBeside` | `Loop/Watch/` | Link 8: the evaluation runtime with its doors and its pass beside the look; the tidy, the limits' wake and the line-following beside the looks |
| `LookStages` | `Platform/LookStages.cs` | Times each stage of a look and the work beside it, says a slow one |
| `DriverIntakes` | `Loop/DriverIntakes.cs` | Asks due an intake, the intake's run, ending a parked intake once its ask is answered |
| `DriverContinuations` | `Loop/DriverContinuations.cs`, `Loop/Continue/` | The person's words continue their session: a park or an ended record goes on. The hub holds `ContinueAsync` and one-line doors; `ContinuationLimits` (the limit word kept and taken off), `ResumeLedger` (the held saying, the record put back, not handed or unstarted, the park ended), `ResumeConclusion` (how a resumed run ends), `WordsHandedOn` (words handed to a new session or left with the person) and `NativeGoOn` (the native door's go-on) are types of their own; `NativeWords` is a file of its own |
| `HelperTurn`, `HelperSettling` | `Loop/Helpers/HelperTurn.cs`, `Loop/Helpers/HelperSettling.cs` | A helper's pass: one read-only turn in a clone of the asker's repository; `HelperSettling` takes an ask up, settles what the helper said and delivers it to the asker; `DriverHelpers` holds doors to both |
| `PartnerPassRunner` | `Loop/PartnerPassRunner.cs` | A partner's pass: admitted, its copy, its strict job, its record and its one turn; `DriverPartners` holds a door to it |
| `BrainstormAssembly`, `BrainstormTurnRunner` | `Loop/Brainstorm/` | A brainstorm's packet assembly, and its voice, room measurement and real turns; `DriverBrainstorms` holds one-line doors to both |
| `DriverCommand`, `DriveUsage*` | `Loop/DriverCommand.cs`, `Loop/Usage/` | The loop a person asks for by its verb; the usage text is six per-group constants joined in order (`DriveUsageIntro`, `Ask`, `Records`, `Place`, `Session`, `Machine`) |
| `SystemWiring`, `WiringFiles`, `WiringRemotes`, `ServiceProbe`, `WindowWiring`, `WiringWorld` | `Loop/SystemWiring.cs`, `Loop/WiringFiles.cs`, `Loop/WiringRemotes.cs`, `Loop/ServiceProbe.cs`, `Loop/WindowWiring.cs`, `Loop/WiringWorld.cs` | How this Daoris is wired: the reader, the settings files and records, the remotes, and its records in files of their own |
| `Planner` | `WorkOrders/Domain/Planner.cs` | Pure plan over a `Snapshot`: `Start` or a `StartVerdict` with its sentence; strikes; the carry-on rule. `Plan` is one line into `PlanRound`, which holds a tick's state and weighs reasons in order through `RoundReading` (the records), `RoundSaying` (go-on, carry-on, resume, taken elsewhere), `RoundConsidering` (the start's reasons, the cap, trees and repository), `RoundJoining` and `HoldVerdicts`; the records (`Snapshot`, `Consideration`, `StartVerdict`, `SittingFact`) are files of their own |
| `SessionsCommand` | `WorkOrders/SessionsCommand.cs`, `WorkOrders/Sessions/` | `daoris session`: the usage, the dispatch and the doors; the verb groups are `SessionsReading`, `SessionsListing`, `SessionsJson`, `SessionsEnding`, `SessionsSaying`, `SessionsKeeping`, `SessionsQueueEdit`, `SessionsGoingOn`, `SessionsArchiving`, `SessionsHandback` and `SessionsPresses`; `SessionsAsk`, `SessionsWorld`, `SessionGrouping`, `SessionLook`, `WordsHold` and `ShownState` are files of their own |
| `CourseDecision`, `LimitCourse` | `Course/Domain/LineCourse.Decide.cs`, `Course/Domain/LimitCourse.cs` | The one decision of how a line goes on: resume, move to a new part, wait, hold, refuse; `LimitCourse` holds the limit's end and wake, the carry-on and the transient bound |
| `DrivenCourse` | `Course/GoOn/DrivenCourse.cs` | A driven line's facts handed to the course, and its answers |
| `DrivenInbox` | `Course/DrivenInbox.cs` | Words to a working driven session: held, delivered at the next step or turn end; the person's turn stop; the one door of its parts |
| `InboxState` | `Course/Inbox/InboxState.cs` | The inbox's state under its one gate: what it guards, the keep's lock before it, what a race would break |
| `InboxKeeps`, `InboxHolds`, `InboxRun`, `InboxStops` | `Course/Inbox/` | The keeps on the record and the edits; the holds of words and claims; the run's next step and close, one step; the keeping stop |
| `LimitResume` | `Course/LimitResume.cs` | A limited line resumes its own conversation once its account is ready |
| `MoveStore` | `Course/MoveStore.cs` | The durable record of each move of a line, taken up by the next loop after a dead one |
| `Observation` | `Events/Domain/Observation.cs` | A run's end concluded from exit code and quest status, with its coded note |
| `PartEnds` | `Events/PartEnds.cs` | The class of each end, read from the journal row alone |
| `SessionWatch` | `Events/SessionWatch.cs` | Marks a quiet session and brings it to the person; ends nothing by itself |
| `TransientRetry` | `Agents/TransientRetry.cs` | Retries a transient failure in the same conversation on a fixed backoff |
| `DriverConfig` | `Settings/DriverConfig.cs` | Drivable, holds, cap, trees, strikes, releases, poll, the quiet bound; its doors over the units below, so callers name it alone |
| `MachineSettings` | `Settings/List/MachineSettings.cs` | Every machine setting declared once: its one key, which the terminal's verb, the route, Ask Daoris's door and the page's words spell, and its field in `driver.json`; the page reads it as `SETTINGS` |
| `QuietSetting` | `Settings/QuietSetting.cs` | `quietMinutes`, the one quiet setting: its bounds as the list declares them, its read, its edit and the watcher's Q |
| `DriverConfigReader`, `DriverConfigWriter`, `DriverConfigDisk`, `DriverConfigEdits` | `Settings/` | `driver.json` read by hand, written by hand, loaded and saved onto the file as it stands under its lock, and the keyed-map edits; the pause, release and role records (`WorkPause`, `TransientRelease`, `RoleRelease`, `RoleReleaseKinds`, `IRoleStarts`) each in a file of their own |
| `LandingKey`, `LandingRule`, `LandingIntegrate`, `LandingForm`, `LandingNames` | `Settings/` | The landing key's reading, writing and sentences, and the rule, integrate policy, form and names it reads |
| `DriverChoicesCommand`, `DriverChoicesList`, `DriverChoicesAcross`, `ChoiceRoutes`, `ChoicePress`, `ChoiceWords`, `DriverChoicesWorld`, `ChoicesRegistry` | `Settings/` | The standing choices from a terminal: the plan per verb, the `list` and `across` verbs, and the routes, press, refusal and world each in a file of their own |
| `SessionTrees` | `Workplace/SessionTrees.cs` | Opens a fresh tree; the front over every tree part (`TreeContext`) |
| `TreeHeads` | `Workplace/TreeHeads.cs` | The branch each listed tree has checked out, one `worktree list` per repository; a tree git does not list is asked on its own |
| `TreeDependencies`, `TreeLocalInputs` | `Workplace/` | Copy installed dependencies and declared machine-local files into a tree as it opens |
| `AutoTidies`, `TreeSetAside`, `TreeSweeps` | `Workplace/` | The look's own tidy, the move aside, and the clean-up press's one path |
| `EndJudgement`, `EndKeeping` | `Workplace/EndJudgement.cs`, `Workplace/EndKeeping.cs` | Whether a branch's work ended, from git alone, and the one writer of that fact |
| `Orphans` | `Workplace/Orphans.cs` | The sweep: a record that says it runs with nothing running it is stopped as interrupted |
| `QuestStore`, `QuestExchange` | `Service/Quests/Quests.cs`, `Service/Quests/QuestExchange.cs` | Quest records and log; every door's judgement; claim by push; the exchange is a root that wires, each door one line to a part |
| The exchange's records | `Service/Quests/Records/` | `QuestAsk`, `QuestPublishOutcome` and `QuestPublishRefusal`, `QuestRespondOutcome` and `QuestRespondRefusal`, `QuestCloseOutcome`, `QuestDeleteOutcome` and `QuestDeleteRefusal`, `QuestCarried`, one file each |
| The exchange's parts | `Service/Quests/` | `QuestPublishing` (a publish judged in order before anything is written), with `QuestLanes`, `QuestChains`, `QuestRequirementJudge`, `QuestCarry` (links and files judged and kept), `QuestWorkflowKinds` (the person's kind and an intake's proposal) and `QuestReceived` (a remote's say over a pushed publish); `QuestResponses` (take, done, decline, wait), `QuestReadies` and `QuestGraphBinds` (the closure graph's ready and bind), `QuestGraphCloses`, `QuestAnswering` (a done's answers and what they hold), `QuestEvidenceReads`, `QuestPersonMoves` (yes, reopen, go on, the person's done), `QuestBringForwards`, `QuestRemoteClaims` (claim by push) and `QuestDeletes`; `QuestWords` holds the sentences they share |
| The quest log's operations | `Service/Quests/Operations/`, listed in `docs/index/registered.md` | Every kind the log writes is a module (`[QuestOperation(kind, Log)]`, `IQuestOperation`): published, taken, declined, done, conflict, dismissed, waited, bound, submitted, rebound, reopened, accepted, evidenced, setup, verdict, chosen and deleted. Each writes and reads its log payload, says what a lost one takes with it in a rebase (`LostAsync`, a lost wait and a lost submission naming themselves on the take's conflict through `QuestRebaseLosses`), and holds its verbs on the `QuestTx` the store's one-line door opens; built as the store opens, refusing a kind declared twice or a mark naming another kind. The take, the done and the decline share one move verb, on the take. `QuestRecordCodec` reads and writes the quest row, its columns and the payloads' shared parts, and `QuestSchema` makes and upgrades the store's tables as it opens |
| The quest records | `Service/Quests/Records/` | `Quest`, `QuestStatus`, `QuestMove`, `QuestRequirement` and its answer, `QuestStep`, `QuestAttachment`, `QuestTakeBind`, `QuestTaker`, `QuestSubmission`, `QuestReopen` and `QuestDeletion`, one file each; the log's own: `QuestOperation` and its kind and reference, `QuestClose`, `QuestClaim`, `QuestLoss`, `QuestPush`, `QuestFetch`, `QuestAcceptance`, `QuestPushRefusal`, `QuestClosure`, `QuestRetirement`, `QuestIntegration`, `QuestConflict` with its end and reading, `QuestForwardTake`, `QuestForwardPress` and `QuestTakeReading` |
| The log's rules | `Service/Quests/QuestLog.cs`, `Service/Quests/QuestTransitions.cs` | `QuestLog` replays a history and judges what applies, who holds a claim and what a rebase loses; `QuestTransitions` is the one table of status moves, which the page's flowchart is held to |
| The store's parts | `Service/Quests/` | `QuestStore` wires and keeps one-line doors; each part is a type of its own over the store's gate: `QuestCache` (the quests table, written whole by a replay, repaired at open), `QuestLogSync` (a machine's pending, accepted and rebase; a remote's fetch, receive and standing; the log rows kept, numbered and rewritten), `QuestIntegrator` (a fetch's replay), `QuestFollowUps` (the follow-up steps a lost parent move rewrites), `QuestChain`, `QuestHistories`, `QuestTakeReadings`, `QuestForgetting`, `QuestReads` (find, list, from, waiting, cursor), `QuestPasses` (the passes and the remote's handshake) and `QuestClaims` |
| `QuestHandbacks` | `Service/Quests/QuestHandbacks.cs` | A session's closing note on its quest |
| `SessionStore`, `SessionLedger`, `Lines` | `Service/Sessions/Sessions.cs`, `Service/Sessions/SessionLedger.cs`, `Service/Sessions/Lines.cs` | Session records, the ledger's opens, moves and says, the tree lock, a line's head; the store and the ledger are roots that wire, each door one line to a part |
| The session records | `Service/Sessions/Records/` | `Session`, `SessionState`, `SessionKind`, `SaidWord`, `SaidFrom`, `SaidLevel`, and the ledger's outcomes and refusals (`SessionOpenOutcome`, `SessionAdvanceOutcome`, `SessionSayOutcome`, `SessionTakeOutcome`, `SessionAttemptOutcome`, `AskWordOutcome`, `SessionDeleteOutcome` and their refusals), one file each |
| The store's parts | `Service/Sessions/` | `SessionSchema` (the table, its triggers and upgrades), `SessionIndexes` (the indexes each record a list answers is read by: who continues it, a quest's last run, the conversation that took a quest, a mirror's id), `SessionRowCodec` (a row and a word read and written), `SessionReads` (finds, the tree and workspace locks, lists, the sync's reads, the review), `SessionWrites` (create, move, mirror, take, the machine-local keeps, delete, the cursors) and `SessionSaidStore` (words kept, taken, moved, marked and settled) |
| The ledger's parts | `Service/Sessions/` | `SessionOpening` (a quest's, a chat's, Ask Daoris's and an intake's open, and the keyed open that moves words), `SessionAdvances` (the lifecycle table `Allowed`, a move, the go-on out of an ended state and the park after a helper), `SessionHolders` (who holds a tree, the busy sentence), `SessionSays` (the say door and the supervisor's answer), `SessionSaidMoves` (take, mark, take back, move), `SessionAskWords`, `SessionKeeps` (the machine-local facts of a run), `SessionDeletes`, with `SaidAnswers` and `SaidKeeping` the sentences and the one keep of a person's word they share, and `SessionGoAheads` and `SessionReviews` beside them |
| `AskStore`, `AskDesk` | `Service/Asks/AskStore.cs`, `Service/Asks/AskDesk.cs` | Ask records, the person's words on them, publishing and deleting; the desk is a root that wires, each door one line to a part |
| The ask records | `Service/Asks/Records/` | `Ask`, `AskState`, `AskWord` and `AskWordKind`, `AskRequest`, `AskDraft`, `AskOutcome` and `AskRefusal`, the review and kind choices and proposals, one file each |
| The desk's parts | `Service/Asks/` | `AskStandings` (an ask read against its quests), `AskPublishing` (ask, publish, close), `AskQuests` (the quest an ask becomes), `AskChoices` (review, kind and workflow), `AskGoAheadAnswers` and `AskDeletes` |

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
    beside the look, reserved. The whole lists a pass or the work beside it reads together (the snapshot's, the tidy's
    holders, the nodes' units) are asked at once. The tidy reads a repository's refs, top and working trees once a look,
    each tree's branch from that list, until one of its stages writes. A branch's end judgement is three starts, where it left the
    line (which is also whether the line holds its tip), its tree and its files, from five: D88's question of every session
    branch is one `rev-list` of the judgement's refs (`UnheldTips`); on the benchmark's two dozen trees the tidy is 6 starts, on
    six trees with work 27 from 38. An ended session's offer reads each question of git once (`GitReadOnce` over the tree's walk), and a
    line still where the branch left it is neither walked nor compared again (17 starts of a tree of work, 12). A reader of the running ones asks the open ones, and every
    other reader of the session list (the window's sessions slice, the parks a look says, the readers that need a line's other
    parts and the per-session doors) reads the list kept by its change since the last mark (`SessionListKept`): its text and its
    typed records are made once for each state of the list, one record is typed alone, and a host from before the change door is
    read whole each time. The page merges a pushed change into the list it holds (`sessionsComposer`) where it sorted the whole
    list at every change. (from D68, D181)
31. A working session has no time limit: a driven run, a resumed run, a go-on and an intake wait for their exit. Only a
    pass with minutes of its own is bounded (a helper 15, a partner 45, a second opinion 20 within 5 to 120).
    `timeoutMinutes` is no setting: nothing reads or says it, a save keeps it as it keeps any key it does not own, and the
    migration's driver-keys step takes it out. (from D200)
32. The watcher marks a session quiet after Q with nothing heard and its job idle, and brings it to the
    person at 3Q. While it waits on its own background work these are 3Q and 5Q. A wait for a permission answer is
    never quiet. Daoris ends nothing quiet but a dead process and a headless `--quiet-end`. A quest session on the
    protocol door, once marked, is told once as Daoris's urgent word (`watch/<session>/<episode>`; `QuietWord`), unless
    `LongCalls` told the same call; the word is taken off the record when the episode or the run ends. Q is `quietMinutes` in
    `driver.json`, 10 by default and 5 to 120, written only when set and read at each look; it is set at Settings → Driver, by
    `daoris settings quiet <minutes>`, through `SET_QUIET` and by Ask Daoris's `quiet` door, and `driver list` says it. (from D200)
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
    untracked work keeps it. An ignored folder of more than 5,000 files is too large to look through and keeps it, counted by
    name before any file's time or bytes are read. (from D195, D218)
50. The person's clean-up press lists every session branch with what it holds and removes those the proof clears,
    judging each again right before it goes. (from D88)

## Where a change goes

Put new behaviour in a unit of its own, beside the subdomain below (D231). `Loop/Driver.cs` is a root that wires: it holds the order of the
look and the run, the lazily built parts and the settables, and grows by a one-line door at most. `Service/Quests/Quests.cs`, `Service/Quests/QuestExchange.cs`, `Service/Sessions/SessionLedger.cs`, `Service/Sessions/Sessions.cs` and `Service/Asks/AskDesk.cs` are roots that wire.

| A new behaviour about | Goes in | Tests that pin it |
|---|---|---|
| whether a quest starts, and why it sits | `WorkOrders/Domain/Planner.cs` (a `StartVerdict` and its `SittingFact`) | `WorkOrders/PlannerTests`, `WorkOrders/ParkedQuestsTests`, `WorkOrders/OverlapPlannerTests`, `StrikeTests`, `Loop/CapCountTickTests`, `Loop/PauseTickTests` |
| how a line goes on after an end, words or a limit | `Course/Domain/LineCourse.Decide.cs` only; executors in `Course/` | `Course/LineCourseTests` with `course-cases.txt`, `Course/CourseCensusTests`, `Course/LimitWaitsTests`, `Course/LimitResumeTickTests`, `Loop/AnswerContinuesTickTests` |
| what a run's end is | `Events/Domain/Observation.cs`, its code in `Events/NoteCodes.cs`, its class in `Events/PartEnds.cs` | `Events/ObservationTests`, `Events/PartEndedTests`, `Loop/AskAndWaitTickTests`, `Loop/OrphanedSessionTests` |
| a part of the look | a pass of its own in its subdomain, marked `[LookPass]`; what it reaches of the driver goes on `ILookWiring`, or on its module's own wiring interface where the type sits above Work orders; what it reads of an earlier pass or leaves for a later one goes on `LookState`, written by one pass | `Loop/DriverPartsTests`, and the tests of the stage it moves |
| a part of a quest's start | a stage of its own in its phase's subdomain of `Loop/Run/`, marked `[RunStage]`; what it reaches of the driver goes on `IRunWiring`; what it reads of an earlier stage or leaves for a later one goes on `RunState`, written by one stage | `Loop/DriverRunPartsTests`, and the tests of the stage it moves |
| a kind of quest operation, or a verb of one | a module of its own in `Service/Quests/Operations/`, marked `[QuestOperation(kind, Log)]`, its payload on `IQuestOperation` and its verbs on `QuestTx`; the store gains only the verb's door | `Quests/QuestsPartsTests`, and the tests of the verb it moves |
| when a look runs | `Loop/Watch/WatchRun.cs` | `Loop/LookStagesTests`, `Loop/DriverWatchLookTests`, `Loop/LookParallelTests`, `Loop/SessionsOutliveTheirLookTests` |
| what a read costs at a home's size | the read's own unit: whole lists asked at once, git once per repository rather than per tree, the session list read as its change | `Loop/HomeSizeBenchProcessTests` over `Loop/BenchHome` (3,000 records, two dozen trees, one with 12,000 ignored files), each read's time and its git starts and session-record requests held to its budget (the look 0 starts and 2 requests, the tidy 8 starts, a tree's walk 17); `Workplace/TidyGitCountTests` and `Workplace/OfferGitCountTests` hold the starts per branch and per offer; `Loop/SessionChangeBenchProcessTests`, one change read as its change |
| quiet sessions | `Events/SessionWatch.cs`, `Loop/Run/Conclude/QuietWord.cs`, `Settings/QuietSetting.cs` | `Events/SessionWatchTests`, `Events/SessionWatchQuietSettingTests`, `Events/SessionWatchProcessTests`, `Loop/QuietEndProcessTests`, `Loop/QuietWordTests`, `Settings/QuietSettingTests` |
| a machine setting, or a door onto one | an entry in `Settings/List/MachineSettings.cs`, each door spelled by its key: the verb in `DriverChoicesCommand` and `ChoiceRoutes`, `SET_<KEY>` in the modules, Ask Daoris's door, the page's `settings.<key>.*` words | the modules' `Platform/MachineSettingsCensusTests`, the page's `settings/everySetting.test.tsx`, and each door's own tests |
| transient failures | `Agents/TransientRetry.cs` | `Loop/TransientRetryTickTests` |
| opening, following or removing a tree | a class over `TreeContext` in `Workplace/`, wired in `SessionTrees`' front | `Workplace/SessionTreesTests`, `Workplace/FreshTreeTests`, `Workplace/TreeDependenciesTests`, `Workplace/TreeLocalInputsTests`, `Workplace/TreeReturnsTests` |
| the tidy and branch ends | `Workplace/EndJudgement.cs`, `Workplace/TreeSetAside.cs`, `Workplace/AutoTidies.cs` | `Workplace/EndJudgementTests`, `Workplace/EndJudgementGitTests`, `Workplace/BranchTidyingTests`, `Workplace/TreeScratchTests`, `Workplace/TidyGitCountTests` |
| a rule every door shares | `Service/Quests/QuestExchange.cs`, `Service/Sessions/SessionLedger.cs` (with the ledger's parts above), `Service/Asks/AskDesk.cs` (with the desk's parts above) | `Quests/QuestExchangeTests`, `Quests/QuestTakeBindTests`, `Quests/QuestDeleteTests`, `Quests/QuestHandbackTests`, `Sessions/SessionLedgerTests`, `Sessions/SessionLockAcrossHostsTests`, `Asks/AskDeskTests`, `Remote/QuestSyncTests` |
| intakes | `Loop/DriverIntakes.cs`, `WorkOrders/IntakeRoom.cs`, `WorkOrders/IntakeKinds.cs` | `WorkOrders/IntakeTests`, `WorkOrders/IntakeRoutingTests`, `WorkOrders/IntakePromptGoldenTests`, service `Asks/IntakeTests` |

Driver tests are under `src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/`; service tests under
`src/Daoris.Service/Daoris.Service.Tests/`.

The gates that bite: `driver` (which also runs `StructureTests`: the module structure and the size ratchet), `censuses`,
`driver-process` and `driver-process-git` (real processes and git, run serially), `service`, and `orient-index`,
`test-map` and `code-map` for any new file. A changed driven loop also meets the family rehearsal
(`npm run rehearse:family`), whose stub-adapter phase proves the loop.

## Contradictions

- D46 and D51 say one driven session per repository; `WorkOrders/Domain/RoundConsidering.cs` (the `OpensOwnTree` branch) runs sessions side by
  side, one per tree, where the repository opens its own trees. The code reads right (as D115 and D167 already say).
- D46 says a fresh session per quest; `Course/Domain/LineCourse.Decide.cs:164-185` resumes the agent's own conversation
  where it holds. D189 reads right.
- D51 rule 2 says a tree is opened for one line and never reused; D214 hands trees from line to line. D214 is proposed
  and unbuilt, and `Workplace/SessionTrees.cs:362-364` follows D51. D51 reads right today.
- D83 says the person answers a park; `Service/Sessions/SessionSays.cs` lands an appointed supervisor's
  attributed word on a park. D208 and D229 read right.
- D88 counts work on any branch of the person's as landed; `Workplace/EndJudgement.cs:322-326` calls it handed, not
  ended, and `Workplace/TreeSetAside.cs:23-30` makes the look wait for the line; only the press goes on D88's proof.
  The code reads right.
- D58 makes `strikes` in `driver.json` the count; D229 makes it a seeded rule and refuses the key on a reached home
  (`DriverConfig.Strikes` is marked transitional). D229 reads right.
- D81 sets a driven session's posture to the harness's `auto`; D184 sets it from the repository's access setup,
  written on the record at the open (`Service/Sessions/Records/Session.cs`, `Access`). D184 reads right.
- D131 falls back to a plain carry-on when a conversation cannot resume; D189 hands the new part Daoris's brief
  (`LineCourse.Decide.cs:271-306`). D189 reads right.
- D65 point 4 says a workflow is a chain of quests; D157 and D190 make it a named, versioned graph that governs a
  bound run, with chains kept as its steps. D190 reads right.

## Open

- **D214** (no backlog row): a repository's pool of trees handed from line to line; proposed, both makers' reads of
  round 3 owed.
- **SESSWATCH1b (rest)**: the word on the native, codex text and dsh doors at the turn's end; a `watch` word kept out of
  `AnswersAlone.Resets` and settled at the come-up; the row's *told* line; `LongCalls`' look folded into the watcher's.
- **SESSWATCH1d, 1e**: the record of quiet episodes, then an automatic end only where it shows such quiet is not work.
- **SESSWATCH1f**: each pass asked for its own minutes.
- **ONEWAY1j / SESSCTX1e**: *Go on fresh*, one press per go-on case.
- **SESSCTX1a**: resume and a fresh part measured on each door.
- **LIMITRESUME1** (under ONEWAY1g): what is left of the limit resume.
- **AGENTERRORS1** (rows 1c to 1e): the one error policy built into the agent interface.
- **AGENTPROVIDER1m-i**: every executor (the driven start, continuations, intakes, passes) holds `AgentRun`.
- **TOOL4n**: a driven Claude Code session that ends on a limit on the native door cools its account.
- **TREEOPENERR1**: the next fresh-tree open that fails with no stderr, read and reported.
- **LINESTOGETHER1e**: the steward's forms for several lines in one repository.
- **D178's last clause** (no row): handing a lost take's work to the winner waits for a way to reach it.
