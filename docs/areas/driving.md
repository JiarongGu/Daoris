# Driving — how it works now

## What it does

Driving turns requests into running sessions on this machine. A quest asks one repository for work. An ask is a sentence the
person enters at a workspace; an intake session reads it and publishes quests. The desktop's driver loop looks again and again: at
each look a pure planner decides which quests start and why every other one sits, and each start opens a session record, a place
to work and an agent process. The session takes, finishes or declines its own quest. The driver only observes: it concludes each
end from the exit code and the quest's state, carries cut-off work on, resumes the agent's own conversation where it can, and
brings a quiet session to the person. Nothing is ended on a clock. Finished trees are tidied once git proves their work ended. The
service keeps the quest, ask and session records and judges every move for every door.

## Its parts

Paths are under `src/Daoris.Desktop/Daoris.Desktop.Driver/` unless they start with `Service/`, which is
`src/Daoris.Service/Daoris.Service.Core/`. Each type is in a file of its name; `docs/index/registered.md` lists the look's passes,
the run's stages and the quest log's operations in their order.

| Where | Its job |
|---|---|
| `Loop/Driver.cs` | The root that wires: one look (`LookAsync`) and one quest's start (`RunAsync`), each running its registered passes or stages at their phases' anchors; the stages and passes reach it through `RunDoors` and `LookDoors` |
| `Loop/Run/` | A start's stages by phase (`[RunStage]`, `RunState`): `Choose/` (agent, account, access, `PlaceHome`), `Tree/`, `Placed/` (steward, binding, set-up step), `Trust/`, `Continue/`, `Open/`, `Hand/`, `Conclude/`, `Ending/`, `Hold/` (`SessionHold`) |
| `WorkOrders/Look/`, `Loop/Look/`, `WorkOrders/` | The look's passes (`[LookPass]`, `LookState`): the sync and lost claims, the local phase, the read phase shaping the snapshot, the plan (`PlanLook`), the starts and intakes (`StartsLook`), what ended (`EndingsLook`), the after phase |
| `WorkOrders/Domain/` | The pure `Planner` over a `Snapshot` (`PlanRound` and its `Round*` parts, `StartVerdict`, `SittingFact`) |
| `WorkOrders/Sessions/`, `WorkOrders/SessionsCommand.cs` | `daoris session` and the five states (`SessionStateReader`) |
| `Loop/Session/` | What every start shares: `SessionEquipment`, `SessionRules`, `QuestWalk`, `AccountEndings`, `SessionWatching`, `SessionWaits`, `PartsHanded`, `StartSentence` |
| `Loop/Watch/`, `Loop/` | When a look runs (`WatchRun`); intakes, continuations (`Loop/Continue/`), helpers, partners, brainstorms, `DriverCommand`, how this Daoris is wired (`SystemWiring`, `Loop/Setup/`) |
| `Course/`, `Events/` | How a line goes on (`LineCourse.Decide`, `LimitCourse`), the driven inbox, `MoveStore`; a run's end (`Observation`, `PartEnds`), `SessionWatch` |
| `Bus/` | The bus's log (D356 steps 1 and 2, rule 30a): `BusLog` writes envelopes behind into the desktop store through one port (`IBusLogStore`, today `DesktopBusLogStore`), the opinion and check facts teed in from their own subdomain (`Review/OpinionBusFacts`, `OpinionBusTee`, `Review/Heavy/CheckBusFacts`), its measures read by `daoris bus stats` (`BusStatsCommand`); the timers each write arms and clears (`BusTimers`), the shadow watchdog (`BusWatchdog`) with the look's reading beside each stall (`BusLook`'s port, today `Review/OpinionRecordLook`), read by `daoris bus stalls` (`BusStallsCommand`) |
| `Settings/` | `driver.json` (`DriverConfig` and its one writer), every machine setting declared once (`MachineSettings`), `QuietSetting`, `ManifestSetting`, the standing choices |
| `Workplace/` | Trees (`SessionTrees` over `TreeContext`), dependencies (`Deps/`), the kept slots (`Pool/`), rooms (`LaneRooms`, `RoomCheck`), the files rules (`Files/`), the tidy (`AutoTidies`, `TreeSetAside`, `EndJudgement`, `Orphans`) |
| `Setup/Create/`, `Setup/Room/`, `Setup/Growth/` | A department made by one press (rule 51: `RepositoryCreates`, `RepositoryCreatePlans`, `CreateJudge`, `CreateReceipts`, `RepositoryCreateWork`, `RepositoryCreateCommand`); a room asked of an adopted repository (rule 53: `PlaceRooms`, `PlaceRoomApplies`, `RoomBrief` in `Setup/Domain/`); the growth switch's one resolver (`GrowthRules`) |
| `Departments/` | A place's manifests and turns (`Manifest/`), a folder turn's review (`Review/`), a system's plans (`Systems/`), `PlaceHomeMarker` |
| `Service/Quests/`, `Service/Sessions/`, `Service/Asks/` | `QuestStore` and `QuestExchange` (every door's judgement, the log's operations, `QuestLog`, `QuestTransitions`), `SessionStore` and `SessionLedger` (opens, moves, says, the tree lock and the place lease), `AskStore` and `AskDesk`, each a root wiring parts of its own |

A folder that is no git checkout is a place of its own (`daoris repository add <folder>`); one decides git or folder, `PlaceKind.At`, by the folder's own `.git` (a linked worktree's file too), never an enclosing repository's, so a folder inside a checkout reads none of that checkout's branches, recovery refs or session-branch waits, the composer's tree chip and the workspace's branch list name none for it, and its page draws Details, Files and Workflow alone. Its manifest is every file but the
excludes with size, time and SHA-256, too large to follow past 250,000 files or five minutes (the `manifest` setting). A system
place is never changed by a session: it plans through the connector's `system_plan`. The kept slots (`Workplace/Pool/`, D214) are
built, but nothing outside tests makes, takes or ends one; `LinePlaces` answers where a record's line stands.

## Its rules

### Quests and the take

1. A quest is `Open`, `Taken`, `Done` or `Declined`. It goes to a registered repository in the same workspace, its body says why
   with evidence, never the change, and it is kept under its receiver's registered spelling. (D32, D34, D70)
2. The driver never writes quest state but at a workflow run's end. The session it starts takes, submits or declines its own quest
   through its own connector, so driven and outside work look the same at the quest layer. (D46) The one exception (D293): a quest worked
   in a run bound to a graph version is bound to that run at its session's first start (`RunQuestBinding`, `POST /api/quests/{id}/run`,
   its closure `run:<run>`), so its agent's done is a submission that leaves it `Taken`; the run's end writes the Done of each of its
   quests standing at a submission and ends the sessions waiting on it, in one service write (`RunEnds`, `POST /api/runs/{run}/end`,
   through the close door under a key of that end), where the run came to its graph's end. A run that ended any other way closes nothing.
   An end the service did not take is kept under the home (`OwedRunEnds`) and sent again at each look until it is taken. The agent's
   `done` is the one verb of a submission, on a quest a closure graph or a run closes alike; its prompt says so (`SessionTarget.Submits`),
   and its ask's channel reads the quest `submitted`, not `taken`, until its Done.
3. A take on a quest shared through a remote claims by push before work starts. It is held, lost (the session stands down), or
   unconfirmed (work goes on until a sync decides). (D68, D69)
4. Each whole look asks the host where every running session's claim stands. A session whose take lost is stopped, and the record
   says why. (D68, D69)
5. Two machines' takes on one quest end as a conflict. The loser's work is kept; the person drops it or brings it forward as a fresh
   take that its kept session goes on from. (D178)
6. A quest nobody has started on may be deleted: what stands on it is judged again inside the delete's own write, and the delete
   travels as an operation. (D95)
7. Daoris checks a session's work only in a workflow's `check` step, never outside its workflow (D293): no look reads a done session's tree
    or asks it a check. A session's handback is the note it gave at done or decline, written once in the same write as the move,
    kept with the session that wrote it, and read whole. (D219) A `check` whose `gates` is `reached` reads the hand-back from git at the tip it
    is bound to (`HandbackReading`, inside the check's executor): the base against the line, how far the line moved past it, the paths touched,
    the commits that carry no `Co-Authored-By` line naming a maker (`CommitAttribution` over the repository port's log, the merge check's rule, held equal to
    `tools/merge/attribution.ts` by one fixture, `attribution-contract.json`), and the work merged onto the line's tip (`LineMerge`, workflows
    rule 40). A line that moved is no fault on its own: a clean merge is what the gates judge. A commit without a maker's trailer, or a merge
    that conflicts, its paths named, is the step's failed verdict, gate `hand-back`, with the reading's words, and holds the steps after it; where the graph draws a `back` wire
    from the step to the work, those words go to the work's open session as Daoris's word under the step's key (`NodeRunKeeping`). No line to
    read against, or git unable to say, is unknown with the code `unrun`, never failed. A process a session left running is not read: nothing
    reads one at a hand-back. Else it runs the declaration's required hand-back commands the paths reach: a gate marked `"handback"` in
    `daoris.gates.json` (`true` for its `run`, or the command to run in its place), except one a broker check finished green at the very commit
    the verification tree takes, so never on a merge (`HandbackGates.Verified`: its whole command, or its name where its own run is the requirement), which is named kept. Every driven brief lists
    the required commands for the agent to run before done (`HandbackGates.Told`) and tells it to write its own `Co-Authored-By` line
    whatever its harness (`CommitTrailers`), since a shell commit through `claude-code-acp` gets none.

### Planning a look

8. The planner is pure and plans over one snapshot. The service's ledger judges every open again; the plan only avoids asking for
   what the ledger would refuse, and gives each refusal a sentence first. (D46)
9. Only repositories the person opted into are driven. A held repository starts nothing new. A repository needs a registration,
   adoption where the door needs it, and a root. (D46, D70)
10. Quests whose last session here has the person's words waiting are planned first; the rest in the service's order, oldest open
    first. (D46, D137)
11. The cap counts what the driver started, passes included (helpers, partners, opinions, brainstorms), never a conversation the
    person holds. It is at least one. A session awaiting its step (`awaiting-step`: its turn ended on a held check, a hold or an opinion,
    running no process) holds no place: it stays in the active list for its tree and its quest, but the count is of the sessions that
    run a turn or will within the look (`ActiveSessions.HoldsSlot`). When its step answers, the words the record keeps are a go-on,
    planned before every new start, and it takes a place again as an ended record does, so it waits for one first in line: the passes
    (helpers, partners, brainstorms, meetings) leave a place for each such resume (`Snapshot.SlotsClaimed`), and a resume whose start
    is reserved and whose record is not yet working holds its place meanwhile (`Snapshot.ResumesInFlight`, counted in `SlotsHeld`). (D130, D181)
12. Whether a repository's sessions work apart is its workflow's `isolate` step (*Open a tree*, closed by `release`'s *Close the tree*, `Workflows/Kinds/Isolate/`): its
    `kind` (`auto`, `worktree`, `copy`), its `at-once` (`2` to `24`, or `any` under the cap; a full count sits a start `apart`)
    and its `overlap` (`go`, `wait`: rule 14). A start carries the step it was planned under (`Consideration.Isolate`), so a save
    between plan and start never moves it; no other verb or key sets a repository apart.
    With no step, one writer works in the place at a time under the ledger's lease: the record whose turn runs there, or, in a git
    place, the parked or waiting line whose end left uncommitted paths of its own (`holds_place`), until a turn commits them, they
    are discarded, the person's commit or stash takes them out (`PlaceMarkDiscards`), or the person lets others work there.
    Nothing renews it on a clock; a start sits behind the holder. Under the lease a start is told what changed outside Daoris
    since the last session and which uncommitted paths are not its own, whose add-all, commit-all and stash its rules deny
    (`PlaceTurns`, `PlaceDenies`). A folder is worked in place the same way, its end marking nothing.
    Under the step a folder's session works in a copy of its own (`FolderCopies`); a folder past the copy bound (2 GB or the
    `manifest copy` setting; 20,000 files) or over half the home's free disk is held, never worked in place instead. The
    workflow's `return` brings the copy back. A path a turn changed that no tool event names is said as changed by none of its
    tools (`TurnUnattributed`).
    A git place's first in-place start marks its common git directory with its home (`PlaceHomeMarker`), and another home is held
    there. A pull or promotion that would move the checkout waits only for the lease's holder; in a session's own tree, for any
    session there. Under the step, sessions run side by side, one per tree; only a resume, a carry-on or a later quest of the
    same run goes back into a tree, and a later quest of a line that is a run of its own gets a tree and branch of its own
    (`RoundJoining`, `QuestRuns`). (D51, D167, D254, D258, D260, D265)
13. A quest held by a session in flight, working or still opening, does not start again. Each start is reserved (quest, tree,
    place under the cap) before it begins. (D181) A start that came to a hold or a cooling account (`HeldStarts`, kept from look
    to look) is weighed after every quest that can start, so it never holds a repository's turn or a place under the cap from
    one; it is tried again at each look it is the only one left, and goes the moment it can.
14. Under `overlap: wait`, a first start whose declared paths meet earlier unintegrated work in the repository, a done unit's open
    integration request included, waits until that work is integrated. A quest that goes back into a tree is no first start.
    (D215)
15. Failed sessions on one quest park it at the strike count, three by default and `0` for never, derived from the records; only
    `failed` and a stop that was not the person's count. On a home whose role rules are reached, the count is a seeded rule.
    *Try again* marks where counting restarts. (D58, D125, D229)
16. A person's stop holds the quest here until they release it; a pause holds an ask's or quest's work until *Resume*. Another
    machine may still take an open one. Neither is a strike. *Try again* on a quest its last look says holds nothing reads the
    records again before it refuses, so a stop made since that look releases at once; an ended record whose line goes on is
    said so by both *stop* and *archive* (`EndedLine`), never as ended by one and running by the other. (D126, D132)
17. A taken quest whose take is not this machine's sits: nothing carries it on or resumes it here, and the ledger refuses the open
    as `TakenElsewhere`. (D80, D178)
18. At each start the plugins are asked first, and a hold names the plugin. A start held at spawn (a harness absent or signed out,
    a tree that would not grow, trust not given) reports its sentence as the quest's reason. Uncommitted work in the place does not
    hold a start: the lease and the manifest say whose it is. A start in a checkout another home on this machine works in place is
    held, naming no path (`PlaceHome`). (D64, D73, D254)

### Start, tree and resume

19. The record opens before the spawn, naming the quest, repository, door, tree, access setup and the actor's choice. The tree,
    profile and waiting words are machine-local and never travel. A throw or cancel between the open and the hand ends the record,
    failed (stopped where the driver is closing), and lets its tree lock go. Each run stage, each step of the files stage
    (`files.read`, `files.deps` with its installs' wait `files.admit`, `files.hook`, `files.setup`, `files.shares`, `files.local`, the
    run's `files` stage handing its steps to the stage) and the hand's own preparation, its plugins' servers apart as `hand.servers`, are
    timed and written once as the agent is spawned, `session.opening` (`OpeningLine`). (D47, D49, D184, D205)
20. A fresh tree is a linked git worktree, its files written by git's parallel checkout on every core (`TreeOpenings.CheckoutSpread`, about half the `tree` stage of one worker, SESSIONOPEN3), at `<home>/work/trees/<workspace>/<repository>/s-<8 hex>`, on a new branch
    `daoris/s-<8 hex>`, grown from the repository's line or the branch a chain's step before landed on. A root that is not the top
    of its own repository is refused. (D51, D82, D86)
21. A tree opens with its hooks held and is marked `unworked` in the home's files record (`work/files/trees/`); a failed open, like
    every other end of a tree, removes the tree, its branch and its record. The `files` run stage, after `open` and before `hand`
    (and the same step at a conversation's open and a go-on), is the one way a tree gets its dependencies: in an unworked tree it
    keeps, places from the store (`work/deps/<key>`, copied, never linked) or installs each effective `dependency` rule in order,
    under its key's lock and the machine's admission (`npm ci --ignore-scripts` where the lockfile marks no install script; a package
    whose `os` leaves this system out, fsevents' shape, runs none here, `NpmPlatform`, so the repository's own `prepare`, which writes
    beside the folder, does not keep the install out of the store, SESSIONOPEN2). Its keys
    run each `toolchain` read once per stage for each environment, every dependency that names it sharing the output
    (`ToolchainReads`), since run per `package.json` folder the npm signature's three reads were most of a large web repository's
    `files.read`. The npm signature's three alone (`node --version`, `npm --version`, `npm config list --json`) are also held in the
    driver's memory for ten minutes under a fingerprint of what decides them (`NpmToolchain`: the node program, the npm launcher and
    package, and the content of the project, user, global and bundled `.npmrc`), so an opening after another starts no npm unless a
    program or file moved; a read that failed or printed nothing is never held, nor one where the fingerprint cannot place its inputs (an `npm_config_*` variable in the environment, a node or npm file in the tree, a path entry that is not whole (relative, `D:bin`, `\bin`), an `.npmrc` that names a `userconfig`, `globalconfig` or `prefix`), and nothing is written (SESSIONOPEN3). The kept orientation index is put only in a clean tree. It seals only a fresh install that wrote nothing beside its folder (else `started.deps-unstored`); the keys list the commit's files once for every dependency; a folder the
    store will not take (unstored, a group's, a companion) is recorded by size and write time, unhashed, a moved write time read as
    a change (FILESSLOW1); it runs the owed
    `post-checkout` hook and each rule's `setup` commands (`per: repository` or `per: tree`), links each shared path and copies
    the machine-local files the `reach: copy` rules name and the checkout's declared `localInputs` and sensitive-patterns file (`TreeLocalInputs`, read-only, copied after the dependencies and still copied where a dependency step fails on the disk, so a tree's checks read what the merge's read). An unworked session's tree is handed without what the checkout's gates file declares to prepare a tree (each gate's `regenerate`, then the `treeBaseline`, here `node tools/quick.mjs --record-base`): its session is told those commands, to run before it changes a file, since run before the hand they held every open of this repository about four minutes with its agent idle (OPENSLOW1, D298). A verification tree runs each `regenerate` before every check's gates, never at its preparation and never the baseline (`TreePreparation`, as a gate's command is: `GateEnvironment`, the machine's admission, a 30-minute bound), a command that does not finish ending the check `unprepared` (workflows rule 40). A `reach: read` or `write` path naming one place that git ignores is
    linked to its place in the checkout, recorded before it is made; else `started.share-declined` says why. A share counts only
    while it reads as its link (`TreeShares`). In a worked tree the stage only keeps. The hand, an open, a go-on and a terminal's
    visit take the mark off first. A folder is placed under a name beside its place and renamed into it, asked again up to twelve
    times while a program (a scanner, an indexer, a git reading the tree) holds a file in it open, and a refusal that outlasts them
    is said once, as `started.files-failed`, with that cause. The store evicts an entry unplaced for thirty days, then the least recently placed past 10 GB.
    (D51, D214, D216)
22. A tree serves one line of work and is never handed to another. A repository may declare rooms (lanes carrying `builds`,
    joined by a `room`; a lane carved across units lists their rooms, each file in its unit's room, and joins them), which
    `RoomCheck` checks. Every path git does not manage has an effective rule from four layers, a later
    match winning: the found layer (`data/files-found.json`, git's view and the signatures in `Defaults/file-signatures.json`, read
    at the commit), the repository's tracked `daoris.files.json` at the line's tip, and the person's `data/files.json`, its
    `machine` rules then the repository's. Each layer is `"form": 2`: git's ignore patterns, each with a rule per act (`reach`,
    `take`, `finish`, a dependency's `across`, an output's `builds` and `refuses`) and a kind naming its defaults, covering a top
    as git's ignore does (`GitPatternOracleTests`). A first-form file is read through `RuleFileFirstForm`; the person's is
    rewritten in git's form once none of its paths reads as written.
    A take's `base` top beside one project file is kept as the previous line's build base, marked `.daoris-base` and released
    only by `daoris tree build <path>`; the runners this repository integrates refuse a path under a marker
    (`tools/tree-base.mjs`). The floor holds over all: a tracked path is git's, a nested repository is `repository`, and a
    harness's or editor's settings are never copied. The person's `deny`, `allow` and `ask` are empty by default; a `files.json`
    that does not read holds every act as an ask and is never written over. `daoris files` and the Files tab show each path's
    rule and evidence; the page writes the person's rules through `FILES_SET`, `FILES_REMOVE` and `FILES_PERMIT`, the window's
    alone. An `ask` holding a start is kept in `data/files-asks.json` for the repository's `files` supervisor (roles rule 7). The
    found layer is written only at registration, when the line or toolchain moves past what it reads, and when a line finishes.
    (D51, D214)
23. A session commits on its tree's branch as it goes. An agent that moves its tree onto a branch of its own is followed there.
    (D164)
24. One decision says how a line goes on after any trigger (words kept, an answer, a limit, a transient bound, a carry-on, a
    hand-over): resume, a new part, wait, hold or refuse. (D137, D189, D206, D212)
25. The agent's own conversation is resumed only by the id Daoris kept, where its agent, account and tree still hold. Otherwise a
    new part opens and is handed Daoris's brief, composed without a model. (D137, D189)
26. A taken quest is carried on in the same tree when its last session here failed, was cut off by the sweep or a shutdown, was a
    person's stop they released, or parked and was answered. It goes on with the agent its record ran on, except a failed record
    whose role no longer lists that agent, which takes the role's first entry and that entry's model (`FailedCarryOn`). A session
    of the quest that asks for the take while the session here that took it ended failed or stopped is answered as the taker: the
    take passes to it (`QuestRetakes`), so a retry never stands down against a holder that is gone; a running holder or another
    machine's take is still refused. A newest stand-down of the quest here is read past at the open to that taker when the claim is
    this machine's and the taker ended failed or interrupted (`StandDownTakers`), never past the person's own stop. *Try again* on such a quest says whose take it is and how that session ended, and that the loop
    carries it on. (D80, D83, D104)
27. A session that asks another repository a quest ends completed and waits. Its quest stays taken, and it resumes in the same
    tree once that quest is answered. (D79)

### Words to a session

28. The person's words reach a session in any state: at its next step where its door takes words during a turn, else at its
    turn's end, else by reopening its ended record. The only move out of an ended state is back to working with words waiting.
    Words to a session whose run has landed are its turn, never new work for that run: what the turn commits, or its tree brought
    to the line, moves none of the run's steps (workflows rule 17, D319).
    A declined quest (an abandon declines it) has ended its line: words kept on its last session, a limited one's included, go on
    in nothing, so the line is not pending and its sessions archive at the next look. (D137, D188)
    Words a record could not go on with are marked by their ids (`<session>.cannot.json`) and the look leaves them waiting, but
    words held for the record's own account (why `account`) are read again at each look (`UnableLook`, `HeldAccountWords`): once
    that account is usable again, five minutes or more after the mark, they go on in its own conversation; while it stays
    unusable past half an hour they go on in a new session on a ready account, in its tree (a closed quest's session through
    the shell's runner, as for an account that cools); another agent's findings alone, which no new session is handed, stay with
    the person they went to. The dock says which and why (`held  session …`). A session awaiting its step whose words are so
    held is no longer reached by them, so *Go on in a new session* takes it. (D348)
29. Answers alone that would take a line past its answers-alone rule's number in a row wait for the person; the hold's note and
    an ended conversation's refusal name that number. (D188, D229)

### The look, timeouts and limits

30. The loop looks at its pace (15 s by default), on a nudge and on a wake (a ledger change, a terminal's words, a session's end).
    A whole look syncs with the remote and stops lost claims; a wake only plans. A slow start runs on beside the look, reserved.
    A read costs no more as the home grows: the lists a pass reads are asked at once; the tidy reads a repository's refs, top and
    trees once a look until one of its stages writes, a branch's end judgement is three starts, and a repository whose last pass did nothing and said nothing starts no git while the stamp of git's own
    files, its trees' folders and its branch records stands, read again after thirty minutes whatever moved (`TidyIdleness`); a reader of the session or
    quest list reads it by its change since its mark (`SessionListKept`, `QuestListKept`), judged once while it is the same
    (`JudgedOnce`); no look judges an owed opinion: its gate is the bus's consumer, judging it at its facts (rule 30a,
    `workflows-review-landing` rule 26), and the plan reads the gate's view (`LookFacts`), each look taking the view's wake before its
    passes read it, so only a judgment landing after wakes the next; a judgment reads its tree's work kept
    while the tree and its refs stand (`TreeReadMemo`);
    a look reads the open records and every record once each, and every graph run and its record once
    (`RunOutcomeNotices`, a workflow file decoded once a read); the overlaps read a running tree's changes only
    where its `HEAD` or index moved or its read is two minutes old (`TreeChangesKept`); the evaluation runtime passes at a whole
    look, at its own wakes and at a ledger change naming the quests, beside whatever look runs, so a step a person or an agent
    closed is read within a pass, never another ledger change, reading tips from git's ref files (`NodeTips`) and a run's record
    once while its file stands. Each look pass is a stage of its part's name in `watch.looked` unless it times itself, so a look's
    own stages add up to its milliseconds, and each stage's time is measured by the day into the bus log's measures (rule 30a). The
    budgets are the bench tests' (below) and the git area's *Git's starts, counted*. (D68, D181)
30a. The bus's log (D356 step 1) is the lease holder's: the desktop's loop and `daoris drive` open it once they hold the home's lease
    and close it before they let it go; a loop beside an instance keeps none. It is two tables of the desktop store (`bus_log`,
    `bus_measures`, made by the store's step 33), each entry one envelope (id, seq, kind as `name/version`, subject, emitter,
    answerer, deadline, effect key, causes, and a payload of ids, codes, counts and times), on a connection of its own, write-ahead
    logged with `synchronous = NORMAL` and its log file bounded as the store engine bounds it. An append is taken in memory, given
    its id, order and time at once, and written behind: plain entries a second after the first of them, a commit-class entry at
    once, grouped with whatever waits; nothing is written while nothing is appended, and a look stage timed or a miss counted while
    idle wakes the writer, which writes the measures alone within fifteen minutes of its last write, never sooner. Several drafts
    appended as one (`AppendAll`) ride one write, kept or lost together. A writer holding no log tees through the home's open one
    (`BusLog.Of`). Teed in beside today's writes: an opinion owed (a due kept, a person's ask of its gate), asked, given and
    answered (the landing line's channel), its findings delivered, the person's word at its gate, and what else its gate's verdict
    reads (the quests' move the change stream names, a node a run's record began, the standing choices this process wrote),
    and a check started and settled (passed, failed with its red classes, unknown with its code, requeued by its busy runner, or
    ended with no result by the driver's close or an unkept result), a settle caused by its start and every start settled before
    its effect starts again. Red classes no gate named stay unknown, never a count of zero.
    A run's progression is the queue's second consumer (`run-step`, `RunStepConsumer`, BUS2e1), started by the watching loop, the headless
    watch mode's too, after its first look (`RunStepRun`; `drive --once` and `--until-idle` run no node pass, start none and take no settle
    box, so a settle waits there for the next watching loop): each fact that ends a step of a run (`check.passed`,
    `check.failed`, `check.unknown`; `step.settled`, a working session's settle taken from its box; `step.turn-ended`, the session of a
    plugin step ending its turn; `policy.passed`, the policy passing a second opinion at its gate; `land.outcome`, a landing's try ending),
    its subject the run's unit, asks the one pass of the evaluation runtime for that run (`PassAsks.WaitUnitAsync`: that run judged
    first, no read of the units, its record, tips and gates read at the pass as a plugin step's wake reads them) and waits for it, so the
    next step begins at the fact and not at a look's rotation; a run the last whole read did not govern yet is left to the look. The
    acknowledgement, written after the pass, ends the fact's obligation only where the pass judged the run; one it did not (a drain, a
    closing loop, no lease) is acknowledged with the obligation armed, spending no retry, and the look's whole pass reaches it. A fact
    takes only the box its session named, and one take runs at a time. The sessions' settle boxes are heard as they are written (a watch on the sessions folder); a sweep once a
    minute is only the watcher's overflow net, not a look: it tees a box no event told or one that stays open, and `daoris bus stats` counts
    each (`found`, `retried`, `overflow`). The look no longer takes them. The whole pass the look's timer makes stays the net for every input no fact names, and `daoris bus stats` counts
    each node begun as the consumer's (in a pass it asked) or the look's, with its lag from the ending fact.
    Every type is declared once, in one registry (`BusTypes`, D356 step 2): its name and version, whether it is a fact, a request
    or a response, its payload's fields and their shapes (id, code, count, number, time, flag, a list of at most 16 ids), its
    emitter, its obliged handler or a request's answerer, a deadline and what set it, a retry budget, and a give-up from a closed
    list: `refused` (the bus answers the request itself), `held` (the gate stays shut), `dropped` (an observation counted) or
    `final-exit` (the subject reaches the owner's final exit, D354 point 4). No silent give-up is in the list: a `refused` request
    or a `held` gate leaves its step a wait, which a vacancy bounds and the final exit ends. No measure set a deadline yet (the
    install's `daoris bus stats` at cddcdac313 held one check start and no handling time, its quests paused), so each is stated
    provisional with what it stands on (a check's bound, a busy runner's offer, a pass's bound, about one look a minute) and the
    watchdog runs against it in shadow (below) before any give-up acts. `BusTypesCensusTests` holds that every type names its obligations, that its give-up
    is in the list, and that no desktop source builds a type elsewhere or spells a kind, so a type the registry lacks is never
    emitted; the page gets the types' list with its first reader in the window (BUS2f), not before. A subscription naming
    a type the registry lacks fails as it registers.
    A shadow append never fails the write it rides beside: a refusal (the store's, a type the registry does not declare, an
    envelope whose subject, answerer, effect or causes are no ids, a process not holding the lease) is counted by its reason and
    said once as `bus.refused`. A payload's field its type does not declare, one named twice, or a value not of its field's shape
    is dropped and the fact kept: the field left unknown, a list keeping the ids that fit, each drop counted by reason, type and
    field and said once as `bus.dropped`. An id or code is well-formed text with no whitespace and nothing shaped like a path or
    an e-mail address (a red class keeps its suite and its test file's name, never its path; a repository named with a space, or a
    parameterized test class, loses that value, never the fact). The lease is asked on the writers' path at most every two
    seconds, an unreadable lease keeping the last answer, and afresh inside each write's transaction, which takes the store for
    writing at its start, so what was taken while it was lost is dropped, never written by the old holder, and no other holder
    writes between the lease's answer and the rows. The write seats its batch after the newest order the store keeps and the log
    moves what waits with it, so a log opened again goes on after its last entry, and one that lost the lease and regained it goes
    on after the other holder's, no batch colliding. The bus waits on the store's lock as long as the engine does; a batch
    a busy store refused goes back ahead of the next, its order kept, tried again five seconds later at most six times running and
    counted, then dropped. Measured by the day: entries by kind,
    each kind's handling time where known, the flushes' count, entries and time (a write that first opened the store is no
    flush's time), the rows' own bytes beside the pages each write put on disk (the write-ahead log's frames, as the engine counts
    them), the misses, the dropped values and each look stage's time, which `daoris bus stats` reads. The log keeps its newest 50,000 rows (about 13 MiB of rows at the 270 bytes a row a check's facts
    measure) and 90 days of measures; nothing reads behind the cut yet. The history reset empties `bus_log` and `bus_timers` and
    keeps `bus_measures`, which are evidence. A second close waits for the first's write, and a writer's loop that failed is counted and
    said at the close, which still writes what waited.
    The watchdog (D356 step 2) runs in shadow beside the looks, started with the lease holder's watch and stopped before the log
    closes; nothing it records changes a run, a session or a notice. An entry whose type obliges someone (every give-up but
    `dropped`) arms a timer row (`bus_timers`, made by the store's step 34) in the write that keeps it, due at its envelope's
    deadline, which the log stamps from the registry alone (a draft names none). The registry declares what ends each obliged type
    (`BusTypes.Ends`, the census holding that every one has its ends): a later fact on the same subject or effect key, or the
    obliged handler's acknowledgement, which only its consumer gives (below). A check's start ends by its own attempt's verdict,
    requeue or end; a requeue and an end by that attempt's next start; a red or an unknown by the run's next start or the step's
    acknowledgement; a pass by the step's acknowledgement alone (BUS2e1's consumer); an owed opinion by the gate's
    acknowledgement; a pass asked by its given; a given by its delivery, its answers, or, clean, the gate's acknowledgement, a clean
    pass leaving nothing to deliver; a delivery by its answers; the answers by the gate's acknowledgement. Every 30 seconds, the
    first an interval after its start, the watchdog first asks whether this process still holds the lease (reading no look where
    it does not), drops the timers of a kind the registry no longer declares, and appends a `bus.stalled` for each timer past due:
    its entry, type, obliged one, since, due time, which stall of its retries plus one, the next due time, and the look's reading of
    the same subject at that moment. Past the last it appends, in the same write as that last stall, the `bus.give-up` it would
    apply, `applied` false, with what it hands to whom: a `final-exit` hands the step to its holder chain (the obliged one's holder,
    then the vacancy path, then the owner's exit, CASTCHAIN1's executors and manager joining before the vacancy), a `refused` request
    is answered to its requester and a `held` gate stays shut, both left a wait the vacancy bounds and the exit ends; never the owner
    straight from a missed deadline. In shadow none of it is commit-class, so a give-up applied to nothing costs no full sync. A
    tick that fails is said once in the machine log and tried at the next. A stall moves its timer in its own write and a give-up
    drops it, so a kill and a restart keep every due time and neither loses a stall nor makes one twice; one appended and not yet
    written holds back the same one for five minutes. The
    comparer is the look's reading kept on each stall (`IBusLook`): open (the look holds it unmet too: agree), handled (the look
    says it was met: disagree, the bus missing its acknowledgement or its deadline or ends wrong) or unknown (no reading, counted
    apart). Today's reading is the opinion's record (a pass asked is open while read; a given until answered, unless clean; a
    delivery until answered); the run's step and the checks are unknown until their jobs bring a reader.
    The queue (D356 points 5 and 6, BUS2d) is the log's delivery, in the lease holder's process on .NET channels, with no broker:
    each write of the log wakes it with the entries it kept, at their kept orders, and it hands them in frames at most 60 a
    second through an ordered middleware chain (`IBusMiddleware`, registered once in `BusMiddlewares`: today the delivery measures,
    each frame and each kind's hand-ons by the day) into each consumer subscribed to their types. A middleware passes, transforms,
    drops (counted against its name) or observes a frame; one that throws is counted and said once, and the frame goes on as it was
    handed to it. The registry's check at emit, the emit measures and the timers' arming stay in the write: they ride the entry's
    own transaction, and the chain runs only after it. A consumer (`IBusConsumer`) is a durable row (`bus_consumers`, made by the
    store's step 35): its types, its cursor (a new one's the log's newest, so it starts from now) and an epoch each claim raises.
    Its entries go one at a time per subject, in order, and a few subjects at once (entries of a kind it folds, waiting in a row on
    one subject, are acknowledged into the last, which stays open while it is handled); each is acknowledged after it is handled, in one
    transaction with the cursor, its parked row and, where its handler says the acknowledgement ends it, the timer obliged to that
    consumer, so an acknowledgement means handled. A restart resumes each from its cursor, its parked entries first: at least once.
    A handler that throws is said once and its entry parked (`bus_parked`) and tried again with backoff, its subject held alone;
    past its type's retries the queue appends the `bus.give-up` naming the consumer and what it hands to whom, and the subject
    goes on. A claim's epoch is read before each handling and inside each acknowledgement, so a later claim fences the earlier off
    and two processes never both execute one consumer; the lease is asked inside each write too. Retention cuts no entry a cursor
    or a parked row still needs; a park the store did not keep leaves its entries open, so no cursor passes them. An outside effect
    runs once by its key, kept in the act journal's family (`act_effects`, beside `git_acts`, not a journal of its own): a word to a
    harness is begun before it is sent and ended after, so a crash between reads `uncertain` and it is never sent again, and a send
    that throws reads failed, its key let go for the next try (`OnceWords`). A run that must return drains it (`QuietAsync`: the log
    written, each write framed, every consumer's entries acknowledged or parked, its start's rebuild ended, within a bound). Its first
    consumer is the opinion's gate (`workflows-review-landing` rule 26), started by the watch and the headless runs alike.
    `daoris bus stalls` (the Stalls view, the terminal's alone: the window has no diagnostics place, and its first reader of the
    bus's views is BUS2f's, which brings the page's types list) lists the armed timers and each stalled entry, newest first, with
    its type, subject, since, obliged one, which stall, and the next try or the give-up it would apply; `--compare` adds agreements,
    disagreements and unknowns by type and the newest twenty disagreements with both sides.
31. A working session has no time limit: a driven run, a resumed run, a go-on and an intake wait for their exit. Only a pass with
    minutes of its own is bounded (a helper 15, a partner 45, a second opinion 20 within 5 to 120). `timeoutMinutes` is read by
    nothing. (D200)
32. The watcher marks a session quiet after Q with nothing heard and its job idle, and brings it to the person at 3Q; while it
    waits on its own background work, 3Q and 5Q. A wait for a permission answer is never quiet. Daoris ends nothing quiet but a
    dead process and a headless `--quiet-end`. A quest session on a door that takes words is told once, as Daoris's urgent word,
    that it was marked (`QuietWord`), and once that a call or background task passed its pipeline's `callMinutes` (20 by default;
    `LongCallLook`); Daoris ends neither. The quiet word is no word a record waits on, is taken off when its episode ends, and
    resets no answers-alone count. Q is `quietMinutes` in `driver.json`, 10 by default and 5 to 120, set at Settings → Driver, by
    `daoris settings quiet <minutes>` or `SET_QUIET`. (D200)
33. An account's limit is read from the agent's own words. It cools that account until the reset it names and is never a strike.
    The record ends `failed` with its `limited` flag and reads as *limited*. (D125, D206)
34. A limited driven line resumes by itself: its own conversation when its account is ready; else a briefed part on another account
    or the role's next choice; else it waits and shows the earliest reset. Waiting words change none of it: where no account of its own
    agent is ready for them, the line walks the role's choices as one with no words does, its words going with it (`CutLineWalk`). An
    account the person picked is never moved off, nor is a line whose role waits on the limit.
    (D205, D206)
35. A transient error is retried in the same conversation after 1, 2, 4, 8 and 16 minutes, and is never a strike; past that the
    line waits for the person's word or *Try again*. Access goes to the person; a fatal error fails with its reason. (D210)

### States and the end

36. A session record moves forward only: `Queued`, `Starting`, `Working`, `AwaitingPerson` or `AwaitingStep`, then `Completed`,
    `Declined`, `StoodDown`, `Failed` or `Stopped`. Every live state may be stopped by the person, at *Stop…* and `session stop <id>` (one
    implementation, `SessionsEnding.StopAsync`; `SessionStopCensusTests` holds every state the page counts active): a process here
    stopped, a start or a working record nothing here runs ended as an orphan, a parked one stopped unanswered, and one awaiting its
    run's step stopped with that run (D316, `RunWaitStops`): every step of the run a stop reaches is stopped through the run's node door
    as their own `workflow stop`, at the settle door as the channel's speaker, and the run's end ends the session; the answer names the
    steps, or says why none was stopped. A parked session still holds its tree, and so does the
    newest part of a quest still open or taken unless it declined, which is never carried on. (D46, D51, D83) `AwaitingStep` (`awaiting-step`)
    is a session whose agent's turn ended while its work goes through its workflow (D293): open, holding its tree, running no process, its
    note's `waits.step` part naming the run it waits on and, where known, the step, its attempt and the tip (`StepWait`; `waits.check`
    for its own broker check). Every turn's end of a done submitted in the run waits so while the run goes on: on the wait its record
    kept, else on its quest's run by the binding kept for it (`WorkflowRunStart.TurnWaitsAsync`), so a record an earlier end read as
    finished goes on with words and awaits the step again; `ended.submitted` (`completed`) is left for a done no graph run governs or one
    whose run's end ended its sessions. A step of its run cast to the role's agent is such a word: the run's newest session awaiting it,
    else the work's own session where it ended, is handed the step through the say door and settles it with `step_settle` (D318). A word
    kept on it wakes
    it by the same go-on an ended record takes (`Went on with Daoris's words`), the planner reading it as any record with words waiting; it
    parks for the person, or ends `completed`, `declined` or `stopped`, never stands down. A run's end ends the sessions waiting on it
    (`RunSessionEnds`, through the runtime's `Finish` door, the service moving only a record still `awaiting-step`) in the write of rule 2:
    `completed` with its quests' Done where the run came to its graph's end, `stopped` where it was stopped, its quests left for the
    person's word. A run ended failed ends none (`RunSessionEnds.Ends`, said once as `failed-held`): its step's holder may still settle
    it done outside Daoris or try it again, which governs the run again, so the sessions awaiting its later steps wait on. Where the
    step's kind reads a failed attempt (a push refused, or one its holder says was pushed outside), it is still observed at each look and
    a reach governs the run again too (`FailedHeldRuns`, its record's end read again whenever a word lifts it). A run whose quests all
    closed asks nothing: its end (`quest-closed`) ends any session that waits on it as stopped by Daoris, with the reason in their note.
37. The end is observed, never self-reported. Done ends completed and a decline ends declined, except in a run bound to a graph version,
    where the agent's done or its submission ends its turn and waits on the run's next step (`StepWaits`: a first start's bind, which names the run and the commit
    its submission handed, a later turn's own `waits.step`), a fix turn whose quest closed before it included. A clean exit holding its own quest
    parks as `AwaitingPerson`, quoting its last words; a clean exit on another's take stands down; a messy exit with the quest
    taken fails. A park the person or the steward finishes ends `completed` and leaves its quest as it stands; a taken quest so
    left is `Unclosed`, a row of *What needs you* until the person's done closes it or their word goes on with the session, its
    tree's work staying to review. A park whose own quest is then closed reads ended, not waiting on the person. (D46, D83)
38. On the native door, a turn that ended while its own background work ran is cut off as failed and carried on; on the protocol
    door the session keeps waiting on that work. (D83, D105)
39. A park never resumes by itself. The person answers it, or an appointed supervisor's word the driver can attribute.
    (D83, D208, D229)
40. Each end gets one class from its record: completed, failed, limited, transient-bound, access, stopped by the person, a role or
    Daoris, refused-choice, awaits-helper, awaits-check (a journal row from before `awaiting-step`), stood-down, or unknown. A limit outranks a good end or one Daoris made.
    (D173, D206, D210)
40a. A session that asks the check broker (`check_run`, a file under the home's `checks` folder, run as the heavy run
    `daoris gate run` makes) and ends its turn on it with no question to the person waits as `awaiting-step` on that check
    (`StepWait.Check`, its note `waits.check`), holding its tree; the verdict (reached, failed or stopped, a red's log tail)
    is Daoris's word under the key `check/<id>`, which wakes it in the same conversation, so the wait spends no tokens. The broker runs one
    check at a time on this machine, the oldest ask first. One broker
    seat per home (`checks/broker.lock`, held while a check runs) keeps a second Daoris process from queuing again a run the first
    holds; every check log is opened in `CheckFiles` only, shared with its reader. A check Daoris could not run (its log, its
    process, a missing tree or a wrapper's tool-error exit) ends `errored`, held on the check with its error, said in the run's log as `look.said` and never to
    the session, and asked again after a minute; only a gate that ran is a verdict said to the session. The check log includes the gate's standard output and error before the wrapper's verdict. Every verdict is said to the
    session that asked, whatever its quest's state: Daoris asks no check of its own, so no verdict is owed to the person. An ask Daoris wrote
    for a hand-back before D293 ("by": "daoris") is deleted with its state and log when the files are read (`CheckFiles.Asks`). (CHECKLOCK1)
41. The sweep, at every look, stops a record that says it runs with nothing running it, as interrupted, so one left `working`
    mid-run reads stopped within a look. A go-on or a resume marks its record before the ledger moves it to `working` ahead of its
    spawn, and the sweep leaves it while that opener runs, as it leaves a record a run of its own loop holds. A `queued` or
    `starting` record is taken only where its opener's mark names a process that went, or where no mark was ever kept and the
    record has stood untouched past two minutes; the opener marks the record the moment it exists. A shutdown kills each
    session's process tree and records the same. Both are carried on. (D104) A session's tracking ending, by exit, stop or a run's end, ends what its
    job still holds, so nothing its agent started outlives it, a launcher still running included; the machine log's
    `session.reaped` says how many.
42. One writer holds a home: an instance for its life, a verb for its act. (D104)

### Asks and intakes

43. An ask is a sentence entered at a workspace, kept verbatim with every later word of the person's. It is `Open`, `Proposed`,
    `Published` or `Closed`, and `Done` derived from its quests, none of them held or left (`Quest.Left`: a done that departed from
    a requirement, accepted or not). Every session on it is handed its words, and a requirement must
    quote them. A new ask takes `--require "<quote>" "<check>"`, with or without a receiver: kept on the ask, its quotes as the person's
    `quoted` words before any quest is judged, and carried by every quest the ask becomes, the one published at once (`--to`) and each its
    intake publishes, the intake's own beside them; a quest it becomes with none is answered *Published with no requirement: its run's
    holds step will wait on the person's word*. It and `ask through <id>` take *through words*, the person's asking for the work to
    be delivered with no press of theirs, the latest standing and `--clear` taking them back; the ask's page says the standing words
    (*Delivered on your words*) with *Take back…*, the person's press. Ask Daoris's press puts on an ask only
    words that stand verbatim, whitespace and case aside, in the person's own messages of the conversation whose delegate key proved
    it; a working session's own press puts none. Any caller closes (`ask close <id> --reason`, the reason kept as its note) or
    deletes an ask, and a quest or a session: the service keeps no key for these (D284). `ask list [--workspace <w>|--all]
    [--open] [--json]` and `quest list [--repository <r>] [--workspace <w>] [--open] [--json]` read them and change nothing; an
    ask whose whole text is one word that names a verb of `ask` (`list`, `show`, `close`, …) is refused, saying the verb
    probably meant, and is never made. (D65, D133, D277, D282, D284, D321)
44. An intake is a session in the workspace's room under `<home>/work/rooms/intake`, recorded as a chat naming its ask. It reads
    the declarations and publishes quests, runs only where an Intake actor is set, one per ask, in slots the quests leave. Where
    the workspace has more than one repository it is the lead of the ask's team (`IntakeLead`): it starts as plan work on the Lead
    role's order, is told to decide one part or several and to write several through `team_plan`, and the plan door publishes as
    the writing session, so its tier moves as its `quest_publish` moves it, and it may ask a scout to orient a part (`helper_ask` with `part`, the one helper start; its pack is kept with the plan, `team_plan` shows it) (TEAMWORK1d). A parked
    one ends in the ask's close's own step, with its reason (`AskIntakeClose`, `ledger.intake-closed`), whichever door closed it;
    at the look when the person publishes or deletes the ask, or when one running at the close parks. Where no declaration covers the work and no repository's own files fit it, the
    intake publishes nothing and ends its last message with one line, `no-department-owns-this: <why>`, which the driver reads
    (`IntakeUnowned`) after a clean exit that published nothing: the park then carries `intake.unowned`, naming Ask Daoris as the one
    that can open a department or a room (`repository create`, `place --room`) beside the person's own publish or close. The intake
    creates nothing; a verdict past one sentence or not the last line is the ordinary park. An ask asks each receiver once: a publish to a receiver holding an open, taken
    or done quest for it, by another hand than that quest's publisher, answers that quest and publishes nothing; a part of the ask's
    plan (`Service/Teams/`, `team_plan`) is the one exception, published under its own key `<ask>/<part>/1`, so two parts to one
    repository and lane are two quests and a retried plan returns the quests it made (TEAMWORK1d). The intake plans work across
    repositories through `team_plan` (a part each, the declared deployables, the cited release orders, the questions) and chains
    no quest for it; a `then` step stays for a check that follows work in one repository (DELIVERY1b). Until an intake
    has served the ask, and while it runs, a session's publish other than the intake's is refused, naming it; the person's press
    never is. A publish names the session that made it and says when it carries no requirement. A parked intake's page offers a box beside its
    card's door to the ask: the session takes no words, so what the person writes there is posted on the ask's channel as theirs,
    and a proposed ask's page draws *Close ask* and *Delete…* in its header, both any caller's (D284). (D65, D205, D255)
45. An ask's or a quest's work is paused and resumed whole here; a quest held by a pause not its own (a quest its work published)
    is named with that pause by *Try again* and by its own resume, which changes nothing and names the resume that carries it on
    (`PauseHolder`). Work is abandoned by a listed second press that discards only what
    nothing else holds and stops every live step of its runs as the person's, with their reason. A run with a step waiting on the
    person lists its newest session in Needs you, one awaiting the run's step included, its waits on its row; one awaiting a step of a
    run that waits on no person works, awaiting a step. A session is in one of five states, read once by `SessionStateReader` in this
    order: needs you (a park's question, a run's wait, a policy's hold, an unclosed quest, the steward's stop, a sign-in, a done left
    unfinished), failed (nothing carries it on), working (live or going on; an open conversation whose turn ended, its record
    still `working`, shows `idle`), pending (queued, or its next start held) and done
    (completed, stopped, declined, stood down). A done whose own record says work is left is never the plain done: the quest's newest
    session is needs you with reason `left` where its answers departed from a requirement the person has not yet accepted
    (`QuestLeft.Waits`), or where its run holds a step failed and kept for the person after the done, no word on it
    (`PersonWaits.Left`); the step a run ended failed at places its session so whatever its quest's state (`PersonWait.Ended`), its own
    `step` row in *What needs you* carrying it (`LeftSteps`); once the person accepted the departure it is done with reason `part-left` (`QuestLeft.Accepted`), their
    word on it. Each row carries `state` and the finer
    `reason`; *Archived* folds a done or failed session alone, and a left one is the person's to set aside. Each
    row also carries its line's state (`line`, `LineStandings`), read through the line's membership and its co-workers: the first
    of needs you, working and pending any part is in, needs you and working where a co-worker serving a part is, else as its
    newest part ended, naming the part or co-worker it is read from. An entry is one line, its records sharing their `line`, a
    row each in Sessions, placed by the line's state; a co-worker is in the entry of the part it serves (`coworkers`), never an
    entry of its own. The list, the head, *What needs you* and the terminal say the line's state, and any part opens as its
    line's newest part, at that part. A tree holding commits no run waits on is offered on its
    session, bar one running; the terminal's list and the clean-up's say the verb that lands it, the run's Accept
    (`daoris workflow accept <run> <node>` where the run waits for it, else `daoris workflow run --session <id>`), never `tree
    land`, which presses nothing (`LandingHint`). `daoris session list --group` takes the five and `archived`. The list's look is one
    gatherer (`SessionGroups.LookAsync`), read by `SESSION_GROUPS` and by the terminal's verb alone: where a Daoris runs, it waits on
    no tree's git walk, its trees as the facts' parts judged them; a terminal alone places its rows from the records as well and
    walks only the trees of the rows needing you, its last line counting the trees it left unread (`ListedTrees`). Each listing writes one
    `session.list` line in the machine log, its stages in milliseconds (`SessionListLog`). (D132, D273, D280, D282)
46. The records of one line share their `line`, and the lines of one ask or quest in one repository share an entry key,
    `<origin>/<repository>`, which an Accept lands together (`LandingEntries`); an entry's slice partition is its leading session. A line's
    head is found by walking `from` forward, at most 256 hops; a cycle or the bound reads as no head. (D167, D212)
46a. Each agent conversation is a numbered turn of its session (D293 point 5). A record keeps the turns it ran (`turns`, this
    machine's own and answered on loopback only): its open writes the first, with its cause read from its `from` link (`start`
    where it continues none, `limit` for a limit or a near one, `curve`, `agent`, `person` for a press, else `daoris`), the agent,
    account and adapter it runs on, and the workflow step and attempt it performs where its run's graph has a work (`claimed`) node
    (`WorkflowBind.Work`, `WorkflowWorkSteps`, sent as the open's `node` and `attempt`; the run's conclusion waits after the same
    step); each wake by the go-on adds one, `person` where any word waiting is the person's or their supervisor's, else `step`
    with the node and attempt its word's key names (`check_run` for a broker check), else `daoris` where Daoris's own words name no
    step (a restart, a limit's reset), continuing the agent, account and conversation of the turn before it. The go-on first seals the
    ended turn with how its record ended (`SessionTurnEnd`: the state, the limit flag, whether the stop was not the person's, the note's
    codes), since it forgives the record's own flags; the strikes and the failures in a row read every end of a record, its turns' kept
    ends and its own (`RecordEnds`), so a go-on never erases a failed end. A move naming a transcript keeps it on the record's last turn, the conversation being that transcript's name.
    The list answers each record its line's turns oldest first, numbered from one, each with the record it ran in and its own
    agent, account, adapter and conversation (`SessionTurnLines`), so a limit or a curve move is the line's next turn and the
    session's id stays its first record's. A move out of a running turn
    hands what the door reported over the record's turns so far (`TurnSpent`, the event log's input and output tokens), and the
    record's last turn keeps its share, that less what the turns before it kept (`SessionTurnKeep.UsageAsync`); the window says
    each turn's input and output on its row. A turn that kept none of its own fields reads its record's agent, account, adapter and
    transcript (`SessionTurns.OfLine`). A line Daoris cut and carries on with its own agent and
    account goes on in its own record as its next turn (conversations area, rule 7); the moves to another account or agent still open a
    record of their own for each move's turn, which `OfLine` reads as the line's next turn alike, since a record's process, tree lease
    and event log are keyed by its id.

### The tidy

47. A branch's end is judged from git alone: claim, handed, ended or unknown. Ended means merged into the line, on the line by
    content, carried by a pull request whose merge is here, or inside an ended landing. An agent's done is a claim. An unread proof
    ends nothing. (D88, D102, D148)
48. The look's tidy takes, with no press, a session branch the clean-up would clear: empty, landed by ancestry or content, or
    carried; with a tree, only this home's tree on a branch that moved and that no run owns (rule 51). Landed work goes only once it is on the line, and a branch
    its tree moved onto only once the line holds that branch's tip. The folder is moved aside into `.tidied`, never deleted; the
    keeping pass purges it. It never starts in a repository whose landing is due, being made or waiting to be tried again
    (`LandRetries.Due`), and comes at a look after it lands. (D88, D195)
49. A tree holding only ignored paths or the repository's declared output goes with its branch. Declared output is the accepted
    `integration.scratch` and the index `daoris.json`'s `documents.index` names at the line's tip, which every checkout writes again:
    its folder where the line commits nothing there, else the file alone (`GeneratedIndex`); and every path the repository's own
    `daoris.files.json`, read at the line's tip, calls an output, an intermediate, a cache or scratch (`FileOutputs`), whatever bytes
    the checkout holds there. Uncommitted or untracked work keeps it, and so does an ignored folder of more than 5,000 files,
    counted by name before any file is read. A tidy or removal the repository's lock refuses says a session was starting only where
    a start holds it; a writer that has not said what it is reads as a write (`TreeLock.RefusedBy`). A copy of a machine-local file
    Daoris made into the tree (the files record's `Copies`, by the hash it wrote) and the half-made copy a cut-off open left
    (`.daoris-making-`) are never the session's work, whatever the checkout's file holds now: a stale copy keeps no tree, an edited one
    does (`TreeCopies.IsOwn`, passed to `ContentHolds.HoldsAsync`). (D195, D218)
50. The person's clean-up press lists every session branch with what it holds and removes those the proof clears, judging each
    again right before it goes. A removal git would refuse for the tree's lock takes no link off it first. A judged branch delete
    that finds the repository held by a start or another writer waits for it, thirty seconds in a flow holding no checkout alone
    (`ActPlan.Waits`, `JournaledDeletes.Waits`), and is then kept and said with the holder's words; a read git said nothing of says so
    (`TreeSweeps.TipUnread`). (D88)
51. A workflow run owns its trees: each governed pass writes the run on its branches' records (`GrownBranch.Run`), and a run's
    tree is ended by its graph's `release` (workflows rule 31b) or by the look's sweep (`TreeReleases.SweepAsync`, in the lease
    holder's tidy of each repository, said once in its report): for a branch the line holds (the end judgement's `GrownBranch.Held`,
    on a standing entry), for a branch whose part the run's `tidy` ended once its pull request
    merged (`TidiedParts`), whatever the run does after and whether or not its graph carries a `release`, and for a run that ended
    settled or whose record is gone. The sweep
    leaves a tree a session still runs or waits in and one a start holds the repository for, and ends the rest by the clean-up's
    own unforced proof, so a tree with uncommitted work or commits no branch of the person's holds stays and its reason is kept
    on its record (`GrownBranch.Kept`). `daoris tree list` names each tree's owner run and that reason; a
    tree no run owns is the look's tidy's and the person's, and the same sweep takes it, with its branch standing, once no session runs or
    waits in it, nothing has used it for 14 days (`TreeReleases.Unused`, `TreeUse.LastAtAsync`), whether no run owns it or its ended run's proof keeps it, and it holds no uncommitted work; where the
    sessions in the repository's trees cannot be read it takes none. The report and the machine log say each tree ended and the bytes it
    freed (`tree.ended`). (D228)

### Departments and rooms

51. `daoris repository create <name> --purpose "…"` (`REGISTRY_CREATE`, a person's press, no grantable family) makes a department and
    ends before any content. Its plan (`REGISTRY_CREATE_PLAN`, open; the press's own ground and its view's one judge) reads the
    registry, the workspace's commit identities and the folder, and writes nothing: the folder goes under the parent most of the
    workspace's registered git roots share (`--under` where none can be read), the line is the one most of them land on, the author the
    identity they commit as, else git's own `user.name` and `user.email` in one of their checkouts or the folder's parent; a name registered in any workspace in any case, a folder that exists and is not empty, an unknown workspace,
    an unusable line, no identity of either kind (naming `daoris git use`) and no `git` each refuse it with nothing written. The apply keeps a receipt
    before its first effect (`data/place-receipts/create/<digest>.json`) and goes in order: the folder; `git init` with one empty commit and
    no remote; the unadopted registration; drive on with hold off, left off with the step and the next press named where the inherited
    workflow holds a `push` or a `pull-request` and the place has no remote; the set-up quest, asked in the repository's checkout where its
    workflow works in place, since a repository of the creation's one commit that no session has worked has nothing else reading it
    (`ISetupWorld.NothingReadsItAsync`, judged in `SetupPress.PlanAsync`'s one set-up judgement; the plan's step says so, and a repository
    with a second commit or a session keeps the `no-own-tree` refusal). A failure leaves the folder and the receipt
    and deletes nothing; a re-press whose folder holds exactly the receipt's empty commit goes on from the first operation not done.
    `--kind folder` registers a folder with no history, identity or set-up quest, so `--work` is refused with it. No `daoris.json`, brief or
    code is written and nothing is pushed. The answer carries the `PressReceipt` it made, which Ask Daoris's press record keeps
    (roles-and-supervision 56). The growth switch (`GrowthRules`: `growth` and `workspaceGrowth` in `driver.json`, set only by the person with
    `daoris settings growth <workspace> [--repository <name>] mode|asked|off`) is read by the plan: `off` refuses it (`growth-off`), and the
    plan carries the value, so `asked` puts the press on a card even in full access.
52. The set-up quest is built once, by `SetupPress`, and asked through one door: `ISetupWorld.PublishAsync` posts one ask to one
    repository with its receiver named, as the person's, carrying `SetupPlan.Then`, a title and body the service chains behind the quest
    as its one step to the same repository, or none. `place --adoption`, a workspace tick, `repository create` and `place --room` publish
    through it; only a creation sets `Then`: the person's `--work`, a quest titled by its first line whose body is the request, the purpose
    and `{parent}`, published by the service when the set-up closes done and not at all when it is declined. The ask door's `then` is
    judged as any chain is, and refused `BadChain` where it names no receiver. The step goes to a place whose row may still read
    unadopted: the planner starts it over the protocol door, sits it over the pipe door (`NotAdopted`, a wait) until the driver's follow
    registers the adoption, and sits it `NotDrivable` where the drive was left off (rule 9).
53. A room is asked, never made: `daoris place <repository> --room <path> --purpose "…"` is a part of `PLACE_APPLY` (`PlaceAsk.Room`,
    `Purpose`), so a place's set-up stays one door, and its whole is one quest to that repository's own session; Daoris writes none of a
    room's text. It rides with no delivery, opinion, look, adoption, driving or pace (`room-alone`), asks one repository (`room-one`) with a
    one-line reason (`room-purpose`), and refuses, every refusal said at once and nothing written, a path that is not a folder inside the
    repository (`room-path`: empty, rooted, with `..` or a character Windows refuses, inside `.git`, `.agents` or `.claude`, through a link
    or submodule, or onto a file), a room already there (`room-exists`: `daoris.json`'s `rooms`, a folder with its own `AGENTS.md`, or a
    lanes room, read as git objects at the line's tip by `IPlaceWorld.RoomAsync`), a repository not registered, not adopted, without a
    checkout, held or with a set-up open, and a line that cannot be read. Its one change is `ROOM_PRESS`, so `--plan` and the receipt say
    it with no case of their own; the card is its own kind (`place-room`); its receipt is the quest's one `room` operation, found by the
    plan's key on a retry. The quest's body (`RoomBrief`) names the repository, the path, the reason, the canon's `rooms-and-classes`
    convention and the repository's own declaration as its line holds it, and asks only for `daoris --version`, `sync` and `check`
    (`SetupPressRules.RoomRules`). The growth switch for the repository (its own value, else its workspace's) is read by the same plan:
    `off` refuses the room (`growth-off`).

## Where a change goes

New behaviour goes in a unit of its own in its subdomain (D231). `Loop/Driver.cs`, `Service/Quests/Quests.cs`,
`Service/Quests/QuestExchange.cs`, `Service/Sessions/SessionLedger.cs`, `Service/Sessions/Sessions.cs` and `Service/Asks/AskDesk.cs`
are roots that wire and grow by a one-line door at most.

| A new behaviour about | Goes in | Tests |
|---|---|---|
| whether a quest starts, and why it sits | `WorkOrders/Domain/Planner.cs` (a `StartVerdict` and its `SittingFact`) | `PlannerTests`, `StrikeTests` |
| how a line goes on after an end, words or a limit | `Course/Domain/LineCourse.Decide.cs` only; executors in `Course/` | `LineCourseTests` with `course-cases.txt`, `CourseCensusTests` |
| what a run's end is | `Events/Domain/Observation.cs`, its code in `Events/NoteCodes.cs`, its class in `Events/PartEnds.cs` | `ObservationTests`, `PartEndedTests` |
| a part of the look | a pass of its own marked `[LookPass]`, reaching the driver on `ILookWiring`, sharing on `LookState` written by one pass | `DriverPartsTests` |
| a part of a quest's start | a stage of its own in its phase's folder of `Loop/Run/`, marked `[RunStage]`, reaching the driver on `IRunWiring`, sharing on `RunState` | `DriverRunPartsTests` |
| a quest operation or its verb | a module in `Service/Quests/Operations/` marked `[QuestOperation(kind, Log)]`, its verbs on `QuestTx` | `QuestsPartsTests` |
| when a look runs | `Loop/Watch/WatchRun.cs` | `LookStagesTests`, `DriverWatchLookTests`, `LookAccountTests` (120 graph runs: a look's stages add up to it, a warm look under half the cold one), `NodeWakesTests` |
| what a read costs at a home's size | the read's own unit | `HomeSizeBenchProcessTests` over `BenchHome` (3,000 records, two dozen trees: the look 0 git starts and 2 record requests, the tidy 8 starts, a tree's walk 17), `LookWorkBenchProcessTests` (ten open trees: no git where nothing moved), `TidyHomeBenchProcessTests` (thirty repositories, forty-two
trees: a warm tidy pass 0 starts, one repository moved only its own), `TidyIdlenessTests`, `SessionReadsBenchProcessTests` |
| quiet sessions | `Events/SessionWatch.cs`, `Loop/Run/Conclude/QuietWord.cs`, `Settings/QuietSetting.cs` | `SessionWatchTests`, `QuietWordTests` |
| a machine setting or a door onto one | an entry in `Settings/List/MachineSettings.cs`, each door spelled by its key | `MachineSettingsCensusTests`, `everySetting.test.tsx` |
| opening, following or removing a tree | a class over `TreeContext` in `Workplace/`, wired in `SessionTrees` | `SessionTreesTests`, `DependencyStoreTests`, `InstallScriptsTests`, `OpeningShapeProcessTests` (a second opening of a repository of this one's shape, its tree and files stages timed and held to 30 s), `NpmToolchainMemoTests`, `TreeCheckoutSpreadTests` |
| rooms, the files rules, the analysis | `Workplace/LaneRooms.cs`, `RoomCheck.cs`, `Workplace/Files/`, `Defaults/file-signatures.json` | `RoomCheckTests`, `FileRulesTests`, `FileAnalysisTests` |
| the tidy and branch ends | `Workplace/EndJudgement.cs`, `TreeSetAside.cs`, `AutoTidies.cs` | `EndJudgementTests`, `TidyGitCountTests` |
| a rule every door shares | `QuestExchange`, `SessionLedger`, `AskDesk` and their parts | `QuestExchangeTests`, `SessionLedgerTests`, `AskDeskTests` |
| intakes | `Loop/DriverIntakes.cs`, `WorkOrders/IntakeRoom.cs` | `IntakeTests`, `IntakePromptGoldenTests`, `IntakeUnownedTests`, `IntakeUnownedRunTests` |

Driver tests are under `src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/`, service tests under
`src/Daoris.Service/Daoris.Service.Tests/`. Gates: `driver` (with `StructureTests`), `censuses`, `driver-process` and
`driver-process-git` (run serially), `service`, `universal`, `test-map` and `code-map` for a new file; `npm run quick` holds the
size ratchet. A changed driven loop also meets the family rehearsal (`npm run rehearse:family`).

## Contradictions

- D46 and D51 (one driven session per repository) vs `RoundConsidering`'s `Isolate` branch (side by side, one per tree, under an
  `isolate` step): the code, as D115 and D167 say.
- D46 (a fresh session per quest) vs `LineCourse.Decide` resuming the agent's own conversation: D189.
- D51 rule 2 (a tree for one line, never reused) vs D214 (trees handed from line to line): D214 is unbuilt in production, so D51
  reads right today.
- D83 (the person answers a park) vs `SessionSays` landing an appointed supervisor's attributed word: D208 and D229.
- D88 (work on any branch of the person's is landed) vs `EndJudgement` calling it handed and the tidy waiting for the line: the
  code; only the press goes on D88's proof.
- D58 (`strikes` in `driver.json`) vs D229 (the failures rule; the key read by nothing and taken out by `RulesStep`): D229.
- D81 (posture from the harness's `auto`) vs D184 (from the repository's access setup, written on the record): D184.
- D131 (a plain carry-on where a conversation cannot resume) vs D189 (a new part with Daoris's brief): D189.
- D65 point 4 (a workflow is a chain of quests) vs D157 and D190 (a named, versioned graph governing a bound run): D190.

## Open

- **TREEPOOL1** (D214): rooms first with an optimised fallback for a session's tree; round 5's reads owed.
- **REPOCONFIG1**: the declarations' one store (`docs/2026-11-09-repo-config-design.md`); round 6's reads owed.
- **SESSWATCH1d, 1e, 1f**: the record of quiet episodes, an automatic end only where it shows quiet is not work, each pass asked
  for its own minutes.
- **ONEWAY1j / SESSCTX1e**: *Go on fresh*, one press per go-on case; **SESSCTX1a**: resume and a fresh part measured per door.
- **LIMITRESUME1**, **TOOL4n**: the native door's limit read, so a native session ended by a limit cools its account.
- **AGENTERRORS1**, **AGENTPROVIDER1**: the agents area's rows.
- **TREEOPENERR1**: the next fresh-tree open that fails with no stderr, read and reported.
- **BUS2b2, BUS2d to BUS2g** (D356): the Daoris bus, one driver-owned log every fact, request and answer goes through; its first two
  steps stand (rule 30a: the log, and the watchdog in shadow), and its rows move the looks of rule 30 to reducers over it one job at
  a time, the opinion gate first.
- **LINESTOGETHER1e**: the steward's forms for several lines in one repository.
- D178's last clause, handing a lost take's work to the winner: no row until a way to reach it is asked for.
