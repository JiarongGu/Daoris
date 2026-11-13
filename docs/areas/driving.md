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
| `Settings/` | `driver.json` (`DriverConfig` and its one writer), every machine setting declared once (`MachineSettings`), `QuietSetting`, `ManifestSetting`, the standing choices |
| `Workplace/` | Trees (`SessionTrees` over `TreeContext`), dependencies (`Deps/`), the kept slots (`Pool/`), rooms (`LaneRooms`, `RoomCheck`), the files rules (`Files/`), the tidy (`AutoTidies`, `TreeSetAside`, `EndJudgement`, `Orphans`) |
| `Setup/Create/`, `Setup/Room/`, `Setup/Growth/` | A department made by one press (rule 51: `RepositoryCreates`, `RepositoryCreatePlans`, `CreateJudge`, `CreateReceipts`, `RepositoryCreateWork`, `RepositoryCreateCommand`); a room asked of an adopted repository (rule 53: `PlaceRooms`, `PlaceRoomApplies`, `RoomBrief` in `Setup/Domain/`); the growth switch's one resolver (`GrowthRules`) |
| `Departments/` | A place's manifests and turns (`Manifest/`), a folder turn's review (`Review/`), a system's plans (`Systems/`), `PlaceHomeMarker` |
| `Service/Quests/`, `Service/Sessions/`, `Service/Asks/` | `QuestStore` and `QuestExchange` (every door's judgement, the log's operations, `QuestLog`, `QuestTransitions`), `SessionStore` and `SessionLedger` (opens, moves, says, the tree lock and the place lease), `AskStore` and `AskDesk`, each a root wiring parts of its own |

A folder that is no git checkout is a place of its own (`daoris repository add <folder>`), its manifest every file but the
excludes with size, time and SHA-256, too large to follow past 250,000 files or five minutes (the `manifest` setting). A system
place is never changed by a session: it plans through the connector's `system_plan`. The kept slots (`Workplace/Pool/`, D214) are
built, but nothing outside tests makes, takes or ends one; `LinePlaces` answers where a record's line stands.

## Its rules

### Quests and the take

1. A quest is `Open`, `Taken`, `Done` or `Declined`. It goes to a registered repository in the same workspace, its body says why
   with evidence, never the change, and it is kept under its receiver's registered spelling. (D32, D34, D70)
2. The driver never writes quest state. The session it starts takes, finishes or declines its own quest through its own
   connector, so driven and outside work look the same at the quest layer. (D46)
3. A take on a quest shared through a remote claims by push before work starts. It is held, lost (the session stands down), or
   unconfirmed (work goes on until a sync decides). (D68, D69)
4. Each whole look asks the host where every running session's claim stands. A session whose take lost is stopped, and the record
   says why. (D68, D69)
5. Two machines' takes on one quest end as a conflict. The loser's work is kept; the person drops it or brings it forward as a fresh
   take that its kept session goes on from. (D178)
6. A quest nobody has started on may be deleted: what stands on it is judged again inside the delete's own write, and the delete
   travels as an operation. (D95)
7. A session that ended done has its handback read by Daoris from its tree once, beside the note it gave: the base against the
    line and how far the line moved past it, the paths touched, the gates those paths reach (the landing's own selection, never a
    list of the worker's) with the verdict each has at this tip. A reached gate with no verdict at
    this tip is run by Daoris through the broker, never refused for; a red comes back to the session as a word. The reading is
    a note on the session's record. A session's handback is the note it gave at done, ready or decline, written once in the same write as the move, kept with the
   session that wrote it, and read whole. (D219)

### Planning a look

8. The planner is pure and plans over one snapshot. The service's ledger judges every open again; the plan only avoids asking for
   what the ledger would refuse, and gives each refusal a sentence first. (D46)
9. Only repositories the person opted into are driven. A held repository starts nothing new. A repository needs a registration,
   adoption where the door needs it, and a root. (D46, D70)
10. Quests whose last session here has the person's words waiting are planned first; the rest in the service's order, oldest open
    first. (D46, D137)
11. The cap counts what the driver started, passes included (helpers, partners, opinions, brainstorms), never a conversation the
    person holds. It is at least one. (D130, D181)
12. Whether a repository's sessions work apart is its workflow's `isolate` step (*Work apart*, `Workflows/Kinds/Isolate/`): its
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
    place under the cap) before it begins. (D181)
14. Under `overlap: wait`, a first start whose declared paths meet earlier unintegrated work in the repository, a done unit's open
    integration request included, waits until that work is integrated. A quest that goes back into a tree is no first start.
    (D215)
15. Failed sessions on one quest park it at the strike count, three by default and `0` for never, derived from the records; only
    `failed` and a stop that was not the person's count. On a home whose role rules are reached, the count is a seeded rule.
    *Try again* marks where counting restarts. (D58, D125, D229)
16. A person's stop holds the quest here until they release it; a pause holds an ask's or quest's work until *Resume*. Another
    machine may still take an open one. Neither is a strike. (D126, D132)
17. A taken quest whose take is not this machine's sits: nothing carries it on or resumes it here, and the ledger refuses the open
    as `TakenElsewhere`. (D80, D178)
18. At each start the plugins are asked first, and a hold names the plugin. A start held at spawn (a harness absent or signed out,
    a tree that would not grow, trust not given) reports its sentence as the quest's reason. Uncommitted work in the place does not
    hold a start: the lease and the manifest say whose it is. A start in a checkout another home on this machine works in place is
    held, naming no path (`PlaceHome`). (D64, D73, D254)

### Start, tree and resume

19. The record opens before the spawn, naming the quest, repository, door, tree, access setup and the actor's choice. The tree,
    profile and waiting words are machine-local and never travel. A throw or cancel between the open and the hand ends the record,
    failed (stopped where the driver is closing), and lets its tree lock go. (D47, D49, D184, D205)
20. A fresh tree is a linked git worktree at `<home>/work/trees/<workspace>/<repository>/s-<8 hex>`, on a new branch
    `daoris/s-<8 hex>`, grown from the repository's line or the branch a chain's step before landed on. A root that is not the top
    of its own repository is refused. (D51, D82, D86)
21. A tree opens with its hooks held and is marked `unworked` in the home's files record (`work/files/trees/`); a failed open, like
    every other end of a tree, removes the tree, its branch and its record. The `files` run stage, after `open` and before `hand`
    (and the same step at a conversation's open and a go-on), is the one way a tree gets its dependencies: in an unworked tree it
    keeps, places from the store (`work/deps/<key>`, copied, never linked) or installs each effective `dependency` rule in order,
    under its key's lock and the machine's admission (`npm ci --ignore-scripts` where the lockfile marks no install script). It
    seals only a fresh install that wrote nothing beside its folder (else `started.deps-unstored`); it runs the owed
    `post-checkout` hook and each rule's `setup` commands (`per: repository` or `per: tree`), links each shared path and copies
    the machine-local files the `reach: copy` rules name. A `reach: read` or `write` path naming one place that git ignores is
    linked to its place in the checkout, recorded before it is made; else `started.share-declined` says why. A share counts only
    while it reads as its link (`TreeShares`). In a worked tree the stage only keeps. The hand, an open, a go-on and a terminal's
    visit take the mark off first. The store evicts an entry unplaced for thirty days, then the least recently placed past 10 GB.
    (D51, D214, D216)
22. A tree serves one line of work and is never handed to another. A repository may declare rooms (lanes carrying `builds`,
    joined by a `room`), which `RoomCheck` checks. Every path git does not manage has an effective rule from four layers, a later
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
    whose role no longer lists that agent, which takes the role's first entry and that entry's model (`FailedCarryOn`).
    (D80, D83, D104)
27. A session that asks another repository a quest ends completed and waits. Its quest stays taken, and it resumes in the same
    tree once that quest is answered. (D79)

### Words to a session

28. The person's words reach a session in any state: at its next step where its door takes words during a turn, else at its
    turn's end, else by reopening its ended record. The only move out of an ended state is back to working with words waiting.
    (D137, D188)
29. Answers alone that would take a line past its answers-alone rule's number in a row wait for the person; the hold's note and
    an ended conversation's refusal name that number. (D188, D229)

### The look, timeouts and limits

30. The loop looks at its pace (15 s by default), on a nudge and on a wake (a ledger change, a terminal's words, a session's end).
    A whole look syncs with the remote and stops lost claims; a wake only plans. A slow start runs on beside the look, reserved.
    A read costs no more as the home grows: the lists a pass reads are asked at once; the tidy reads a repository's refs, top and
    trees once a look until one of its stages writes, a branch's end judgement is three starts, and a repository whose last pass did nothing and said nothing starts no git while the stamp of git's own
    files, its trees' folders and its branch records stands, read again after thirty minutes whatever moved (`TidyIdleness`); a reader of the session or
    quest list reads it by its change since its mark (`SessionListKept`, `QuestListKept`), judged once while it is the same
    (`JudgedOnce`); a look reads the open records and every record once each, and every graph run and its record once
    (`RunOutcomeNotices`, a workflow file decoded once a read); the overlaps read a running tree's changes only
    where its `HEAD` or index moved or its read is two minutes old (`TreeChangesKept`); the evaluation runtime passes at a whole
    look, at its own wakes and at a ledger change naming the quests, beside whatever look runs, so a step a person or an agent
    closed is read within a pass, never another ledger change, reading tips from git's ref files (`NodeTips`) and a run's record
    once while its file stands. Each look pass is a stage of its part's name in `watch.looked` unless it times itself, so a look's
    own stages add up to its milliseconds. The budgets are the bench tests' (below) and the git area's *Git's starts, counted*.
    (D68, D181)
31. A working session has no time limit: a driven run, a resumed run, a go-on and an intake wait for their exit. Only a pass with
    minutes of its own is bounded (a helper 15, a partner 45, a second opinion 20 within 5 to 120). `timeoutMinutes` is read by
    nothing; the migration's driver-keys step takes it out. (D200)
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
    or the role's next choice; else it waits and shows the earliest reset. An account the person picked is never moved off.
    (D205, D206)
35. A transient error is retried in the same conversation after 1, 2, 4, 8 and 16 minutes, and is never a strike; past that the
    line waits for the person's word or *Try again*. Access goes to the person; a fatal error fails with its reason. (D210)

### States and the end

36. A session record moves forward only: `Queued`, `Starting`, `Working`, `AwaitingPerson`, then `Completed`, `Declined`,
    `StoodDown`, `Failed` or `Stopped`. Every live state may be stopped. A parked session still holds its tree. (D46, D51, D83)
37. The end is observed, never self-reported. Done ends completed and a decline ends declined. A clean exit holding its own quest
    parks as `AwaitingPerson`, quoting its last words; a clean exit on another's take stands down; a messy exit with the quest
    taken fails. A park the person or the steward finishes ends `completed` and leaves its quest as it stands; a taken quest so
    left is `Unclosed`, a row of *What needs you* until the person's done closes it or their word goes on with the session, its
    tree's work staying to review. (D46, D83)
38. On the native door, a turn that ended while its own background work ran is cut off as failed and carried on; on the protocol
    door the session keeps waiting on that work. (D83, D105)
39. A park never resumes by itself. The person answers it, or an appointed supervisor's word the driver can attribute.
    (D83, D208, D229)
40. Each end gets one class from its record: completed, failed, limited, transient-bound, access, stopped by the person, a role or
    Daoris, refused-choice, awaits-helper, awaits-check, stood-down, or unknown. A limit outranks a good end or one Daoris made.
    (D173, D206, D210)
40a. A session that asks the check broker (`check_run`, a file under the home's `checks` folder, run as the heavy run
    `daoris gate run` makes) and ends its turn on it with no question to the person ends `completed` by `ended.awaits-check`,
    holding its tree as a wait on its helper does; the verdict (reached, failed or stopped, a red's log tail) is Daoris's word
    under the key `check/<id>`, which reopens it in the same conversation, so the wait spends no tokens. The broker runs one
    check at a time on this machine, a hand-back's own checks before a worker's, each class first come, first served. (TEAMWORK1a)
41. The sweep stops a record that says it runs with nothing running it, as interrupted. A shutdown kills each session's process
    tree and records the same. Both are carried on. (D104)
42. One writer holds a home: an instance for its life, a verb for its act. (D104)

### Asks and intakes

43. An ask is a sentence entered at a workspace, kept verbatim with every later word of the person's. It is `Open`, `Proposed`,
    `Published` or `Closed`, and `Done` derived from its quests, none of them held or left (`Quest.Left`: a done that departed from
    a requirement, accepted or not). Every session on it is handed its words, and a requirement must
    quote them. A new ask published at once (`--to`) takes `--require "<quote>" "<check>"`, its quotes kept on it as the person's
    `quoted` words before its quest is judged; it and `ask through <id>` take *through words*, the person's asking for the work to
    be delivered with no press of theirs, the latest standing and `--clear` taking them back; the ask's page says the standing words
    (*Delivered on your words*) with *Take back…*, the person's press. Ask Daoris's press puts on an ask only
    words that stand verbatim, whitespace and case aside, in the person's own messages of the conversation whose delegate key proved
    it; a working session's own press puts none. Any caller closes (`ask close <id> --reason`, the reason kept as its note) or
    deletes an ask, and a quest or a session: the service keeps no key for these (D284). `ask list [--workspace <w>|--all]
    [--open] [--json]` and `quest list [--repository <r>] [--workspace <w>] [--open] [--json]` read them and change nothing; an
    ask whose whole text is one word that names a verb of `ask` (`list`, `show`, `close`, …) is refused, saying the verb
    probably meant, and is never made. (D65, D133, D277, D282, D284)
44. An intake is a session in the workspace's room under `<home>/work/rooms/intake`, recorded as a chat naming its ask. It reads
    the declarations and publishes quests, runs only where an Intake actor is set, one per ask, in slots the quests leave. A parked
    one ends in the ask's close's own step, with its reason (`AskIntakeClose`, `ledger.intake-closed`), whichever door closed it;
    at the look when the person publishes or deletes the ask, or when one running at the close parks. Where no declaration covers the work and no repository's own files fit it, the
    intake publishes nothing and ends its last message with one line, `no-department-owns-this: <why>`, which the driver reads
    (`IntakeUnowned`) after a clean exit that published nothing: the park then carries `intake.unowned`, naming Ask Daoris as the one
    that can open a department or a room (`repository create`, `place --room`) beside the person's own publish or close. The intake
    creates nothing; a verdict past one sentence or not the last line is the ordinary park. An ask asks each receiver once: a publish to a receiver holding an open, taken
    or done quest for it, by another hand than that quest's publisher, answers that quest and publishes nothing. Until an intake
    has served the ask, and while it runs, a session's publish other than the intake's is refused, naming it; the person's press
    never is. A publish names the session that made it and says when it carries no requirement. (D65, D205, D255)
45. An ask's or a quest's work is paused and resumed whole here; a quest held by a pause not its own (a quest its work published)
    is named with that pause by *Try again* and by its own resume, which changes nothing and names the resume that carries it on
    (`PauseHolder`). Work is abandoned by a listed second press that discards only what
    nothing else holds and stops every live step of its runs as the person's, with their reason. A run with a step waiting on the
    person lists its newest session in Needs you. A session is in one of five states, read once by `SessionStateReader` in this
    order: needs you (a park's question, a run's wait, a policy's hold, an unclosed quest, the steward's stop, a sign-in, a done left
    unfinished), failed (nothing carries it on), working (live or going on; an open conversation whose turn ended, its record
    still `working`, shows `idle`), pending (queued, or its next start held) and done
    (completed, stopped, declined, stood down). A done whose own record says work is left is never the plain done: the quest's newest
    session is needs you with reason `left` where its answers departed from a requirement the person has not yet accepted
    (`QuestLeft.Waits`), or where its run holds a step failed and kept for the person after the done, no word on it
    (`PersonWaits.Left`); once the person accepted the departure it is done with reason `part-left` (`QuestLeft.Accepted`), their
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
    land`, which presses nothing (`LandingHint`). `daoris session list --group` takes the five and `archived`. (D132, D273, D280, D282)
46. The records of one line share their `line`, and the lines of one ask or quest in one repository share an entry key,
    `<origin>/<repository>`, which an Accept lands together (`LandingEntries`); an entry's slice partition is its leading session. A line's
    head is found by walking `from` forward, at most 256 hops; a cycle or the bound reads as no head. (D167, D212)

### The tidy

47. A branch's end is judged from git alone: claim, handed, ended or unknown. Ended means merged into the line, on the line by
    content, carried by a pull request whose merge is here, or inside an ended landing. An agent's done is a claim. An unread proof
    ends nothing. (D88, D102, D148)
48. The look's tidy takes, with no press, a session branch the clean-up would clear: empty, landed by ancestry or content, or
    carried; with a tree, only this home's tree on a branch that moved and that no run owns (rule 51). Landed work goes only once it is on the line, and a branch
    its tree moved onto only once the line holds that branch's tip. The folder is moved aside into `.tidied`, never deleted; the
    keeping pass purges it. (D88, D195)
49. A tree holding only ignored paths or the repository's declared output goes with its branch. Uncommitted or untracked work
    keeps it, and so does an ignored folder of more than 5,000 files, counted by name before any file is read. (D195, D218)
50. The person's clean-up press lists every session branch with what it holds and removes those the proof clears, judging each
    again right before it goes. A removal git would refuse for the tree's lock takes no link off it first. (D88)
51. A workflow run owns its trees: each governed pass writes the run on its branches' records (`GrownBranch.Run`), and a run's
    tree is ended by its graph's `release` (workflows rule 31b) or by the look's sweep (`TreeReleases.SweepAsync`, in the lease
    holder's tidy of each repository, said once in its report): for a branch whose part the run's `tidy` ended once its pull request
    merged (`TidiedParts`), whatever the run does after and whether or not its graph carries a `release`, and for a run that ended
    settled or whose record is gone. The sweep
    leaves a tree a session still runs or waits in and one a start holds the repository for, and ends the rest by the clean-up's
    own unforced proof, so a tree with uncommitted work or commits no branch of the person's holds stays and its reason is kept
    on its record (`GrownBranch.Kept`). `daoris tree list` names each tree's owner run and that reason; a
    tree no run owns is the look's tidy's and the person's. (D228)

### Departments and rooms

51. `daoris repository create <name> --purpose "…"` (`REGISTRY_CREATE`, a person's press, no grantable family) makes a department and
    ends before any content. Its plan (`REGISTRY_CREATE_PLAN`, open; the press's own ground and its view's one judge) reads the
    registry, the workspace's commit identities and the folder, and writes nothing: the folder goes under the parent most of the
    workspace's registered git roots share (`--under` where none can be read), the line is the one most of them land on, the author the
    identity they commit as; a name registered in any workspace in any case, a folder that exists and is not empty, an unknown workspace,
    an unusable line, no identity (naming `daoris git use`) and no `git` each refuse it with nothing written. The apply keeps a receipt
    before its first effect (`data/place-receipts/create/<digest>.json`) and goes in order: the folder; `git init` with one empty commit and
    no remote; the unadopted registration; drive on with hold off, left off with the step and the next press named where the inherited
    workflow holds a `push` or a `pull-request` and the place has no remote; the set-up quest. A failure leaves the folder and the receipt
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
| when a look runs | `Loop/Watch/WatchRun.cs` | `LookStagesTests`, `DriverWatchLookTests`, `LookAccountTests` (120 graph runs: a look's stages add up to it, a warm look under 2 s), `NodeWakesTests` |
| what a read costs at a home's size | the read's own unit | `HomeSizeBenchProcessTests` over `BenchHome` (3,000 records, two dozen trees: the look 0 git starts and 2 record requests, the tidy 8 starts, a tree's walk 17), `LookWorkBenchProcessTests` (ten open trees: no git where nothing moved), `TidyHomeBenchProcessTests` (thirty repositories, forty-two
trees: a warm tidy pass 0 starts, one repository moved only its own), `TidyIdlenessTests`, `SessionReadsBenchProcessTests` |
| quiet sessions | `Events/SessionWatch.cs`, `Loop/Run/Conclude/QuietWord.cs`, `Settings/QuietSetting.cs` | `SessionWatchTests`, `QuietWordTests` |
| a machine setting or a door onto one | an entry in `Settings/List/MachineSettings.cs`, each door spelled by its key | `MachineSettingsCensusTests`, `everySetting.test.tsx` |
| opening, following or removing a tree | a class over `TreeContext` in `Workplace/`, wired in `SessionTrees` | `SessionTreesTests`, `DependencyStoreTests` |
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
- **LINESTOGETHER1e**: the steward's forms for several lines in one repository.
- D178's last clause, handing a lost take's work to the winner: no row until a way to reach it is asked for.
