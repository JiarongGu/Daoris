# Who is at work on a session: its parts, its subagents, its co-workers and the quests it asked, what each is on and what waits on what, read from the Fabric's records, and how a session hands its work out (COWORKVIEW1, proposed, 2026-10-24)

**Standing:** proposed, round 2, under D191, with dated notes amending D173 (the Collaboration Fabric), D203 (co-workers), D115
(lanes) and D167 (sessions and their parts). Round 1 was written by Claude Opus 5.5 from the code on main at `fd1d2727e`. The
independent Opus foundation reader read it at `ccb2e5a45`, and its read is verbatim in `docs/2026-10-24-cowork-view-reads.md`: 19
findings, 10 of them blocking, and another round. Codex's read stopped when its credits ran out, and is owed. Round 2 is written by
Claude Opus 5.5 from the code on main at `426ed2b73`, whose `src/` is the same as `fd1d2727e`'s, so no claim moved. It answers every
finding in §11, each checked against that code. Nothing here is built, publishes, signs in or spends an account.

**What changed in round 2.**
- **A lane quest to the session's own repository is a further part of that session** (finding 1). It is drawn once, as a part, and
  it lands on the session's one branch under its one *Accept* (§1.3, §1.9, §3.1, §6.1).
- **The routing doctrine says what each kind of session does after publishing** (finding 2). It names the person's rule that bounds
  an agent's quests and the preconditions for them (finding 3) (§6.2, §6.5).
- **The read is woken by one named path into the held reads**: a `work` scope told on the bus, and one fixed watch part over the
  lineage files (finding 4) (§4).
- **The protocol door's lineage line carries the parent and the wire's own end word**, and never an end the driver made (finding 5).
  The join to the hook uses the evidence already on record and never draws one child twice (finding 6) (§3.6).
- **No group names a boundary no record keeps.** The subagents are grouped by the lead's agent process, not by a turn (finding 7).
  *Last heard* comes from what the window heard, and gives way to the watcher's *quiet* (finding 8) (§3.1, §3.7).
- **The view is *At work* (分工), its read `SESSION_AT_WORK`, its verb `sessions at-work`.** D203's *team* and `sessions team`
  stay D203's (finding 9) (§5.4, §7).
- **The rows are features.** The first is *a chat's subagents seen*, built driver to page (finding 10) (§7).

Paths: `Driver/` is `src/Daoris.Desktop/Daoris.Desktop.Driver/`, `Modules/` is `src/Daoris.Desktop/Daoris.Desktop.Modules/`, `Core/`
is `src/Daoris.Service/Daoris.Service.Core/`, `Mcp/` is `src/Daoris.Service/Daoris.Service.Mcp/`, `Web/` is `src/Daoris.Web/src/`.
`Dnn` is `docs/decisions/Dnn.md`. Every fact in §1 was read in that code. A fact this design could not find is said as *not found*,
with what was searched. Backlog rows are cited by id, not by line.

## 0. The case

The row COWORKVIEW1 in `TASKS.md` carries it. On the install, a kit's chat was moved to another account into a fresh conversation
(`chat.moved-from`). It went on working its roadmap one native subagent at a time inside one long turn. Its page showed little but
*Next, RV16*, and its record's `updated` stood at the turn's start. Nothing said who was doing what, so the work read as stuck and as
no team at all. The repository declares no lanes, so it could not hand its own rows out as Daoris sessions (SELFASK1). A native
subagent is one tool card on the page, although FABRIC-C1a binds it to its part by lineage.

Two things went wrong, and this design answers both:

1. **Seen.** The Fabric already records most of who works beside a session, but no reader puts those records in one place. The one
   record that names native children is read only by tests (§1.1).
2. **Routed.** The session did all of its roadmap in one turn of one agent. Its repository had no lanes, so the one way to hand
   itself work did not exist. The publish tool's own description already names a repository's lanes as "the one way to ask your
   own repository" (`Mcp/QuestTools.cs:37-40`). The refusal it would have met, a quest to its own repository whole, names no way
   (§1.9). What the case lacked was lanes, not words.

## 1. Today, read from the code

### 1.1 Native children: recorded, never read

- **The record.** FABRIC-C1a writes `<home>/sessions/<id>/lineage.jsonl` for a session the driver started
  (`Driver/Sessions/ChildLineage.cs:35`, `:52`; `Core/Sessions/LineageFile.cs:13-22`; D173's FABRIC-C1a note, `D173.md:104-112`).
  It has four writers, each marked by `by`:
  - the connector, on every call, from the call's `_meta` (`Mcp/…/CallLineageFilter.cs:17-20`; fields `LineageFile.cs:72-92`);
  - Claude Code's hook on `PreToolUse` for the connector's tools, `SubagentStart` and `SubagentStop`
    (`Driver/Sessions/hooks/lineage-hook.mjs:7`, `:25`). It is handed in the session's settings
    (`Driver/Agents/Permissions/Permissions.cs:561-576`) on both the pipe and the protocol door: a chat hands it in `session/new`'s
    `_meta` (`Driver/Conversations/Chat/ChatRunner.cs:1349-1360`), a driven session at `Driver/Loop/Driver.cs:3087-3097`. The hook
    writes no parent (`lineage-hook.mjs:31-46`);
  - the driver reading Claude Code's stream on the pipe door, for each child's start, parent, depth and end, with the end's `status`
    (`Driver/Sessions/Domain/ChildBindings.cs:298-323`, hooked in at `Driver/Agents/Streams/HarnessCapture.cs:123`). The protocol
    door's capture runs no stream watcher (`ChatRunner.cs:2397-2419`);
  - the driver at the part's end, clean or not (`ChildBindings.cs:326-327`; `ChatRunner.cs:2497`, `Driver.cs:3825`).
- **What it keeps.** Ids only: the hook keeps a value only where it is shaped as an id (`lineage-hook.mjs:10-12`, `:24`), so no path
  reaches the file. `ChildBindings.Fold` folds the lines into each child's harness, native id, type, parent, depth, provenance,
  connectivity, state (`active`, `finished`, `ended`, `lost`) and call count (`ChildBindings.cs:13-37`, `:64-73`, `:128-266`), and
  the part's root, end and counts (`:90-111`).
  - It takes a parent only from a stream line or a Codex call (`:268-291`).
  - A part's `end` line closes every open child as `ended` where clean and `lost` where not (`:241-244`). A stop from the hook or
    the stream makes a child `finished`, whatever its end's `status` said (`:195`, `:211`, `:278`).
  - The lines carry their moments (`at`), but the fold keeps no start or end time.
  - No line keeps what the child was asked to do: the stream's line drops the task's description (`:298-323`).
  - Codex says no thread's end, so a Codex child ends with its part and is never `finished` (`D173.md:121-122`). A Codex child is
    bound only from the `thread` of its own calls (`ChildBindings.cs:223-235`).
- **Nobody reads it.** `ChildLineage.Read` (`ChildLineage.cs:125-139`) is called only by tests (`ChildLineageTests`,
  `LineageHookProcessTests`). No module route and no page code reads the lineage (searched `ChildBindings`, `ChildLineage` and
  `lineage` across `Modules/` and `Web/`). FABRIC-C1b, *a part's native children on its record*, is open and placed after C2a.
- **What the page shows of a child comes from elsewhere**, and is gone when the window is:
  - *the protocol door* carries every session on the install (D173's FABRIC-ACP1 note, `D173.md:5173-5180`).
    - The adapter announces a child on its parent's wire session (`from`), which is the root's or another child's, with its name,
      its task and its `subagentSessionId`. It announces the child's end with a state (`Driver/Agents/Acp/AcpStreams.cs:152-204`).
    - The driver opens a console stream for the child. It keeps one tool card on the session's events, titled with the child's name
      and carrying its task as text, updated in place at the child's end, for a child the root spawned only (`:158`, `:169-181`,
      `:202`). A nested child's name and task are held only in memory.
    - When the session ends, `EndAllAsync` closes every open child with the driver's own `session-ended` (`:110-148`).
    - A child's own words and tools go to its stream, never to the session's record or transcript (`:16-21`, `:95-102`).
  - *the pipe door*: the `Task` call is a card titled `Agent: {description}` (`Driver/…/StructuredOutput.cs:461`). The child's
    lines, tagged `parent_tool_use_id`, go to a stream named by the task's description (`Driver/Agents/Streams/ClaudeStreams.cs:73-80`,
    `:99-108`, `:137-149`);
  - the streams are held in memory only, each line a sequence number and its text with no moment (`Driver/…/SessionOutput.cs:9`,
    `:69`, `:92`, `:175`). They are served by `SESSION_STREAMS` (`Modules/Bridge/Routes/ConsoleRoutes.cs:44-55`) and drawn as the
    output panel's tabs, *subagent · live* or its end word from `work.panel.stream.state.*` (`Web/bridge/sessions/console.ts:152-199`,
    `Web/work/console/streams.ts:23-44`), with no parent, depth or moments;
  - a conversation's blocks have no parent or nesting (`Web/work/conversation/conversation.ts:231-261`), so a child is one
    `ToolCard` (`Web/work/conversation/ToolCard.tsx:82`).
- **The ids.** The CONSOLE2 evidence records that `subagentSessionId` is "the harness's agent id, seventeen hex characters", and that
  a re-run of the same task gets `<id>:generation:<n>` (`docs/2026-09-28-console2-streams-evidence.md:61-64`). The adapter's end
  states are `completed`, `failed`, `cancelled` and `disconnected` (`:69-70`). The hook's `agent_id` is Claude Code's agent id, the
  same id as the stream's `task_id` (`D173.md:100-102`). Whether the hook reaches Daoris over the adapter at all (`SubagentStart` on
  the protocol door) is unmeasured: FABRIC-ACP1 saw no native child (`D173.md:5177-5180`, `:131-133`).

So a person can see that a child started and ended, one card at a time, in the conversation and the panel. Nothing else is shown:
not the children together, not which one runs now, not one lost when its root was killed, and nothing at all once the window
restarts.

### 1.2 Parts and co-workers

- **Parts** (D167). A session is an entry, `<origin>/<repository>`, and its parts are every record under that key
  (`Core/Sessions/SessionEntries.cs:25-37`). The origin of a quest's session is the ask that asked it; else the conversation that
  took it (CHATTAKE1); else the origin of the session that published it; else its chain parent's; else the quest itself
  (`:96-113`). A conversation handed over takes the one it continues, through `from` (`:86-90`). So an entry's parts are:
  - its `from` chain, each a record continuing the last with its reason (`from_session`, `from_why`, written at
    `Core/Sessions/Sessions.cs:827`), shown in the head's facts as *continues…* (`Web/work/head/headFacts.ts:95-98`);
  - **every session opened on a quest whose origin is the entry's own**: a quest the line published to its own repository's lanes,
    a sibling request of the same ask to the same repository, and a quest a chat took (CHATTAKE1, `Session.TookQuest`,
    `Sessions.cs:246-250`). The ledger opens such a session under its quest's origin (`Core/Sessions/SessionLedger.cs:1599-1616`).
  - They are shown in the *Parts* strip and as the session's one rail row (D167 points 2, 4 and 5; FLOWCHART1b, archived 2026-10-20).
- **How a further part runs** (D167 point 4 as amended 2026-10-14; SESSONE1d). A new quest of an entry goes into the newest part's
  tree when nothing live holds it. Otherwise it starts at once alongside, in a tree of its own, as a further part
  (`Driver/Quests/Domain/Planner.cs:1716-1759`). This holds only where the repository opens its own trees here
  (`config.OpensOwnTree`, `:1723`). Where it does not, the repository runs one session at a time and the entry changes only the
  grouping (`:2010-2021`).
- **How it lands** (D167's LANDSYNC0 amendment, ACCEPTALL1, `D167.md:286-336`). An entry's parts land on the entry's one branch
  under the entry's one *Accept*, a part that ran alongside replayed onto that branch's tip. A part's own *Accept* lands it on the
  same branch (ACCEPTALL1b, ACCEPTALL1f).
- **Co-workers** (D203, accepted 2026-10-23): a record of this machine's whose `serves` names a part of another session's line. Its
  role is read as reviewer (`opinion`), partner (`serves` with `piece`) or helper (`serves` alone) (`D203.md` point 1; the design
  `docs/2026-10-23-coworker-design.md` §2.1). A helper and a reviewer keep their own entry and are no part of the line they serve
  (`SessionEntries.cs:76-77`).
  - A helper's ask is three files under `<home>/data/helpers` (`Core/Platform/DaorisHome.cs:340`): the ask (`id`, `asker`,
    `question`, `agent`, `askedBy`, `urgent`, `at`), its state (`waiting`, `approved`, `started`, `answered`, `refused`, `cooling`)
    and its answer (`Core/Sessions/HelperBox.cs:33`, `:44`, `:73-83`, `:117-123`, `:185`). A lead waiting on its helper ends
    `completed` with `ended.awaits-helper` (`Driver/Sessions/Domain/Observation.cs:164-165`), a note with no values
    (`Web/…/noteLines.ts:50`), so which helper it waits on is a join through the box's `asker`.
  - A partner's piece is a box under `<home>/data/partners` (`Driver/…/PartnerFiles.cs:6-10`, ask fields `:76-85`, state
    `:121-135`). COAGENT1a and 1b are built; `partner_ask`, the lead's own door, is not (COAGENT1d).
  - The page shows a helper's open asks in the head's *Helpers* and a partner's *Pieces* (`Web/work/head/SessionHead.tsx:440-457`);
    answered and refused asks are filtered out (`Web/work/waits/HelperAsks.tsx:61-66`). None of COWORKUX1a-i is built: the key
    `work.helper.heading` still reads *Helpers* (`Web/locales/en/work.helper.json:9`), and no `work.team.*` key exists.
- **Not co-workers** (D203 point 1): a part, another repository's session, a meeting's participant, a native subagent, Ask Daoris,
  an intake and a brainstorm's pass.

### 1.3 Quests a line published, lanes, and waits

- **Lanes** (D115 §2.2, built by DEV4). `daoris.lanes.json` declares a repository's lanes. `connect` sends their words, never their
  globs, and the registration keeps them (`docs/2026-10-01-self-development-design.md:129-162`). A quest is addressed
  `repository:lane`. The exchange splits it once (`Core/…/QuestExchange.cs:528-531`), judges the lanes (`:707-743`) and keeps
  `Lanes` beside `To` (`Core/…/Quests.cs:117`). A repository may ask its own lane, "work for another lane is work for another
  session"; to itself whole it is refused (`QuestExchange.cs:533-540`).
- **Under D167, that other session is a further part of the publisher's own.** A quest's origin is its publisher's (§1.2), so a
  session publishing to `itsrepository:lane` opens a session in its own entry. That session runs alongside in a tree of its own,
  where the repository opens its own trees, and lands on the entry's one branch. A lane quest to *another* repository opens a session
  in that repository, a different entry of the same origin.
- **The driver plans by repository, never by lane.** Its only readers of lanes are the intake's room, the registration and the
  remote sync (`Driver/Quests/IntakeRoom.cs:133-138`, `Driver/Service/Domain/LineRegistration.cs:199`,
  `Driver/Service/RegistrationFollow.cs:155-166`, `Driver/Service/Domain/RemoteSyncPayloads.cs:169`). A lane's quest is started as
  any quest to its repository, and only where that repository is driven here (`Planner.cs:1889-1891`). PAR1 already runs a
  repository's sessions side by side where it opens trees of its own (`Planner.cs:1964-1966`). DEV6 adds lane reservations and a
  cap; DEV9 adds the steward that splits a whole-repository quest. Both are open.
- **The links.** A quest names the session whose connector published it, `PublishedBy`. It is null both for the person's own
  publish and for a chain's step (`Quests.cs:104-109`), and an intake publishes as its ask (`From` is `ask #id`). A chain's step
  names its `Parent`. A session names the quest it serves, `Session.Quest` (`Sessions.cs:103`), or the quest a chat took,
  `Session.TookQuest` (`:246-250`). No session record names a lane (no `Lane` in `Sessions.cs`).
- **The wait.** A taken quest waiting on a question names it in `Awaits`, one id (`Quests.cs:96-102`), set by `quest_respond`
  `wait` with one `on` (`Mcp/QuestTools.cs:366-374`). The exchange refuses a wait on the quest itself or on a closed one, and on
  nothing else (`QuestExchange.cs:1454-1505`), so a lead may wait on a quest it published to its own lane.
- **Units.** Quest units already carry `awaits`, `follows`, `produced-by`, their executors and their end (`WorkUnit.cs:53-73`,
  `:140-152`), built from the quests and sessions by `QuestUnits.Read` with no other read (`Driver/Quests/QuestUnits.cs:20`).
- **Why a quest sits.** The driver's tick says why each open quest does not start (D46 §3): not drivable, the repository busy, the
  cap spent, waiting for an account, and the rest (`Planner.cs:1023-1048`, `:2048`), read by the page through `sittingBecause`
  (`Web/data/signals.ts:5-7`, `:145-158`).
- **Shown:** a quest's lanes on its card and page (`Web/quests/QuestList.tsx:214-218`, `Web/quests/QuestPage.tsx:656`); its wait on the
  quest page and list (`QuestPage.tsx:665-682`, `Web/quests/QuestsView.tsx:563-567`) and on the rail as *awaits #q of repo*
  (`Web/work/rail/SessionRow.tsx:329-332`); *Who it worked with* (`Web/work/…/SessionRelations.tsx:105-111`). **Not shown:** a
  lane anywhere under `Web/work/`, in an ask's work (`Web/asks/AskWork.tsx`) or on the chain strip.

### 1.4 The ask's channel

The channel is a table of messages (`Core/Asks/Channel.cs:243-258`). Each message has its ask, moment, posting session (null for the
person), repository, agent, entry, addressee, reply, question, text and urgency (`:87-104`). Daoris's own lines are read from the
quests and never stored (`:116`). The channel is drawn on the ask's page (`Web/asks/AskPage.tsx:533-545`) and in a session
(`ChannelPanel`, `Web/work/AttendedSession.tsx:323-324`). Each message shows *repository · agent* or *You*, its moment, a question or
urgent pill and its receipts (`Web/asks/ChannelSection.tsx:32-34`, `:288-291`). A message's session and entry are carried and not
drawn.

### 1.5 Brainstorms and meetings

A brainstorm is `<home>/brainstorms/<id>/`, with its request `(Id, Question, Repository, Agents, At)` and its state
(`Driver/…/BrainstormFiles.cs:8`, `:13`, `:35-40`). A meeting is `<home>/brainstorms/meetings/<id>/`, its request the same shape and
its state with a round (`Driver/…/MeetingFiles.cs:9`, `:15`). **Neither request names the session, ask or quest that started it.**
Neither has a place on the page: *no place in the frame, no state on any screen* (`docs/2026-10-20-ui-ux-design.md:110`). Their page
is MEETUX1a-d's, and a caller from a session is BRAINSTORM1d's and AGENTROOM1b's.

### 1.6 Work units, the running view and the flow

- **Units** (FABRIC-UNIT1a): kinds `branch`, `quest`, `ask`, `run` (`Driver/Trees/Inventory/WorkUnit.cs:4-20`), each with an owner,
  subject, end and links `produced-by`, `produced`, `awaits`, `follows` (`:53-73`, `:129`, `:140-152`). `WorkUnits.ReadAsync`
  (`Driver/…/WorkUnits.cs:54`) reads every unit, the branches' inventories with it, and is read only for node evaluation
  (`Driver/Loop/DriverWatch.cs:900`). FABRIC-UNITDOOR1, the door that would answer every unit (`WORK_UNITS`, `work units`), is open
  (no such route or verb under `src/`). `WORK_STATE` answers branch milestones, not units (`Modules/Bridge/Routes/LinesRoutes.cs:558-612`),
  pushed as slice `work`.
- **The running view.** `Flow` is built (`Web/design/Flow.tsx:147`) and draws a quest, a session's line and states, a landing and a
  workflow's run (FLOWCHART1a1-1d, archived; `Web/workflow/WorkflowChart.tsx:363`). FABRIC-GRAPHVIEW1 is open: each node's §E2
  state, each wire's condition and what waits on what, drawn from the units in the side bar's *Workflow* view
  (`docs/2026-10-20-flowchart-design.md` §2.5, §10). That design rejects *a second running view beside the Workflow view* and *a path
  inferred from a conversation, or a state the record does not keep* (§8).
- **No fabric event names a child.** The chain's events are wakes naming a subject (`Driver/Platform/Chain.cs:5-59`). The session's
  event log has user, message, thought, tool, plan, usage, turn, note, transient, raw and receipt
  (`Driver/Sessions/Domain/SessionEvents.cs:13-54`). The design's typed envelope is not built
  (`docs/2026-10-16-fabric-events-design.md:1204-1213`).

### 1.7 The session's record and page while its agent works

- **`updated` stays at the turn's start.** It is *when the record last moved* (`Driver/Trees/SessionGroups.cs:36`). It is written at
  the record's creation and at each ledger move (`Core/Sessions/Sessions.cs:827-828`, `:890-912`), and for a note kept on a said word
  (`:1562`); every other write leaves it (`:1413-1797`). The ledger has no move from `working` to `working`
  (`Core/…/SessionLedger.cs:2181-2192`). A chat moves to `working` once when its process starts and again only at its end
  (`ChatRunner.cs:2423`, `:3103`), and tool calls go to the event log, never the record (`AcpStreams.cs:173`). So an hour of work,
  children included, leaves `updated` where it began. Its readers rely on that meaning: the head's *ran* and *waiting* facts
  (`headFacts.ts:169`, `:175`, `:207`).
- **No record keeps a turn.** A chat on the protocol door keeps one agent process across its turns, taking each new message into
  that process (`ChatRunner.cs:1804`, `:1826`, `:2397-2399`). Its lineage gets an end line only when the process lets go
  (`:2486-2497`). A child whose stop the hook missed stays `active` through later turns, and a background child holds no turn.
- **The head** (`Web/work/head/SessionPageHead.tsx:14-39`) shows the state, the title and a facts line (`headFacts.ts:143-209`):
  repository, the leading agent, *continues*/*helps*/partner links, then by state its age, attempt, retrying, the first answer's
  wait, the running call, a heavy run, *quiet*. Then the primary act, *Stop…* and ⋯. The running call is the `calls` slice's, pushed
  as a call starts and ends (`Modules/Loop/CallsStateSlice.cs:9-27`). The watcher's *quiet · N min* is its second partition
  (`:37-41`, `headFacts.ts:199`), from the watcher's own hearing of a driven session's output, stamped in memory
  (`Driver/Sessions/Watch/SessionWatch.cs:64-71`, `:183-189`).
- **The side bar's views** are notifications, timeline, review, workflow, ask, console and terminal (`Web/work/frame/placements.ts:9`,
  `:22`), on the right but the console and terminal (`:28-30`). None says who works beside the session. The side bar is never under
  300 px (`DOCK.floor`, `Web/work/frame/layout.ts:44`), and its views are storied at 300 and 430 px
  (`Web/help/AskPanel.stories.tsx:335-341`; `Web/work/review/FilePreview.stories.tsx:5`).
- **`chat.moved-from`** is the new conversation's first note (`Driver/…/HandOver.cs:266-271`, `ChatRunner.cs:2125-2127`). It is
  drawn by the note table (`Web/…/noteLines.ts:295`), with the record's `from_session` and `from_why` as *continues…*
  (`headFacts.ts:95-98`). A moved chat's earlier part ran on another account.
- **The ask's page** lists its quests (`AskWork`, each with its state, route and sessions, `Web/asks/AskWork.tsx:23-38`), its lines
  of work (`AskLines`, `AskPage.tsx:526-529`) and its channel, in separate sections.

### 1.8 What reaches the page unasked

- **The service's change stream** has five families, `sessions`, `quests`, `asks`, `channel` and `words`
  (`Core/Platform/ChangeStream.cs:19-51`). The driver hears them in one wait (`Driver/Service/LedgerChanges.cs:26`). The window's loop
  tells each move it heard to `DriverLoop.MoveHeard` (`Modules/Loop/DriverLoop.cs:140-154`), and a slice hears a ledger family there
  (`QuestsStateSlice.HearLedger`, `Modules/Loop/QuestsStateSlice.cs:130-144`).
- **The modules push fourteen slices** (`Modules/Loop/*StateSlice.cs`; `Modules/Platform/StateSlices.cs:82-85`;
  `.claude/knowledge/pushed-slices.md`).
- **A change with no press** is told as `DAORIS.STATE_CHANGED`, naming its scopes and, where it is one session's, that session
  (`StatePublisher.Publish`, `Modules/Platform/StatePublisher.cs:258-264`; the scopes `:15-87`). The page's one hub asks again the
  queries each scope names (`Web/bridge/state/stateHub.ts:27-68`, `:93-100`).
- **Under NOBLOCK1 a computed read is marked `[ByEvent]`.** It answers at once from what is held (`Modules/Bridge/HeldAnswers.cs`).
  Its work runs on a task of its own, but only when the page asks (`:20-22`, `:74-106`). Where the answer moved, it is told as
  `DAORIS.ANSWERED`, naming the page's query, to the pages that asked (`:170-177`; `Web/bridge/state/held.ts`; D56's NOBLOCK1 note,
  `D56.md:177-190`). The page asks again only when a query is invalidated (`held.ts:24-34`), and D169's one place for that is the
  hub. `WindowCensusTests` refuses a computed route that is neither.
- **The home watch.** `HomeWatch` registers its topics once and cannot remove one (`Driver/Platform/HomeWatch.cs:270-280`).
  `ForDriver` wakes the driver's look, not a route (`:196-213`). `ForProposals` and `ForNotices` are the window's own process's,
  whoever holds the home's lease (`:234-258`; `DriverLoop.cs:802-816`). A part may hear a whole folder tree while its judge reads
  only the files named to it (`HomePart.ByName`; `HomePart.Workflows`, `:13-33`).
- **No family, slice, scope or held read carries the lineage or a native child.** The `calls` slice moves only when a call starts or
  ends (`CallsStateSlice.cs:23-24`).

### 1.9 How work is handed out today

| Way | Who starts it | Where it runs | What the record says | Seen |
|---|---|---|---|---|
| a lane's quest to the session's own repository (D115, D167) | a session's `quest_publish` to `itsrepository:lane`; the person's composer offers no lane (DEV4's *left out*) | **a further part of the publisher's own session**: alongside in a tree of its own while a part holds the session's tree, else in the newest part's tree; only where the repository is driven here, and side by side only where it opens its own trees | the quest's `Lanes`, `PublishedBy`; the part's `Session.Quest` and its `entry` | the *Parts* strip and the rail row; the quest's page; lands on the session's one branch under its one *Accept* |
| a lane's quest to another repository | the same, to `other:lane` | that repository's session, an entry of the same origin | as above | the quest's page; *Who it worked with* |
| a helper (D163) | the lead's `helper_ask` under `helpers`, or the person | its own one-turn record reading a copy | the helper box, `serves` | open asks in the head |
| a partner (D199) | the person only, until COAGENT1d | its own clone, landed by the lead's merge | the partner box, `serves`, `piece` | *Pieces* |
| a reviewer (D155) | a workflow's occasion, or the person | its own record, reading | `opinion` | the gate |
| a native child | the lead's agent, by its harness's own tool | inside the lead's agent process, its tree and its account | the lineage (§1.1) | one card for a child of the root, and a panel tab |
| another repository whole | `quest_publish` to it | its own repository's session | the quest | *Who it worked with*, the quest |

**A repository asking itself whole** is refused with *That is the repository you are in — a quest is work for someone else. Use its
own backlog.* (`QuestExchange.cs:535-540`), whether or not it declares lanes. That sentence does not say that a lane is how a
repository asks itself, although the publish tool's description does (`QuestTools.cs:37-40`). SELFASK1 is filed to say it at the
refusal. A lane named on a repository with none is refused naming the repository (`QuestExchange.cs:713-716`).

**A chat can take a quest** (CHATTAKE1): `quest_respond take` from a chat records `Session.TookQuest`
(`QuestTools.cs:395-403`), and the quest joins the chat's line (`SessionEntries.cs:102-106`; `D167.md:176`).

### 1.10 The case replayed

The kit's chat ran on the protocol door. Each child its root started became one card titled with the child's name and one panel tab
(§1.1). Whether the hook recorded each child's start and stop in its lineage file is not known. `SubagentStart` over the adapter is
unmeasured, and row a reads the install's files first (§7). Nothing read the file either way. The chat's record moved once, to
`working`, at the start (§1.7). The repository declares no lanes, so it could not hand a row to a further part of its own session.
Had it addressed its own repository whole, it would have been refused with a sentence that names no way (§1.9). The person needed to
know which child runs, which ended, since when and how many are left. Those facts existed only as cards scrolled past, or did not
exist.

## 2. The rule

1. **The view is a read, never a store** (D173's first guard; D203 point 3). Nothing records a team or a structure. The read
   composes the records that already link a part of the work to the session: the entry and its parts, `from`, `serves`, `opinion`,
   `piece`, the lineage, `PublishedBy`, `Parent`, `Lanes`, `Session.Quest`, `Session.TookQuest`, `Awaits`, the quest units, the
   helper and partner boxes, and the channel.
2. **From the records, never the conversation's text.** No member, state, wait or moment is parsed from what an agent said. What a
   member is on is the record's field: a quest's title, a helper's question, a piece, a lane's title. For a native child it is the
   harness's own announcement of it (§3.6), a structured field of its wire. Each is drawn as the agent's words, never Daoris's
   (D137).
3. **Partial visibility is said, not filled** (ADR-B, canary 9). A child whose parent no harness said sits under the lead with
   *parent not said*. A Codex child is *ended with its part*, never *finished*. A child of a killed root is *lost*. A harness whose
   children are seen only when they call Daoris says so in place of an empty group. A member the read cannot place is not drawn as
   anything else.
4. **One home for each record.** A record is drawn once, in the one group its record places it in (§3.1). A part is a part, however
   it joined the session, and the view and the *Parts* strip agree.
5. **No model** (D24). Every word in the view is Daoris's fixed words or a record's as written.
6. **D203's words stand.** Point 1's co-worker and point 2's team are unchanged. A native child, another repository's session and
   the quest this session serves are not co-workers and gain no `serves`. The view shows them beside the team, each under its own
   heading, because the person asked *who is doing what*, and the answer includes them. Their rules are their own.

## 3. The read

### 3.1 Who is at work on a session

The read is asked for a session: its entry (D167), whose parts the *Parts* strip draws. For an ask, it is the union of its entries'
reads and its quests (§3.5).

**The lead** is the part the page attends where it is live. Otherwise it is the entry's newest live part, by `created` and then id,
the order `SessionEntries.Read` lists and `EntryParts.Earlier` finds the newest by. Where no part is live, the lead is the newest
part. Other live parts are drawn *working alongside*.

| Group (en / zh) | Members | Read from | Who | On what | State | Since |
|---|---|---|---|---|---|---|
| **This session** / 本会话 | every part of the entry, once: the lead first, then other live parts, then ended parts folded behind a count | the entry's records, as the *Parts* strip reads them | agent, account's mark | its quest's title and its lanes, or *chat* | the record's | *last heard* for a live part (§3.7); the record's end for an ended one |
| **Subagents** / 子智能体 | the lead's own native children, as a tree by `Parent` and `Depth` | the lead's lineage fold, with §3.6's label, moments and end word | the child's type | the harness's label for it, the agent's words | *working*; the wire's end word; *ended with its part*; *lost* | its start; its end |
| **Co-workers** / 同事 | helpers, partners and reviewers of any part | `serves`, `piece`, `opinion`; the boxes | agent | the question; the piece; the candidate | the box's or the record's | the box's `at` |
| **Asked through quests** / 通过委托请求 | each quest a part of the entry published that opened no part of it: to another repository (its lanes or whole), or to its own lanes and not yet taken | the quest units (`QuestUnits.Read`) of quests whose `PublishedBy` is a part, and of their chain steps by `follows` | the taking session's agent, or *not taken yet* with the driver's reason | the quest's title; its lanes | the unit's end and wait, and its session's state where taken | the quest's or the session's moves |
| **Asked by** / 由谁请求 | the quest the lead serves or took, its publisher and its ask | `Session.Quest ?? Session.TookQuest`; the quest's `PublishedBy`, `Parent` and `From` | the publishing session, the step before, the ask, or the person | the quest | — | — |
| **Gathered** / 召集 | a brainstorm's voices and a meeting's participants, once their requests name who started them (§3.6, row f) | the request and its state | each agent | the question | the state's | the request's `at` |

- **A part carries how it joined** (§3.4): *handed on*, *retried*, *published to the {lane} lane by part n* or *took #q*. Where it
  runs alongside, it says *alongside, in a tree of its own*. Each part other than the lead carries its own subagents as a count,
  *2 subagents · 1 working*, which opens that part's view. Only the lead's subagents are drawn as rows. An earlier part of a moved
  chat ran on another account, and the subagents' note (§5.4) is true only of the lead.
- **A quest to the session's own lanes moves from *Asked through quests* to *This session* when it is taken.** Untaken, it has no
  record but the quest. Taken, its session is a part, and the quest is that part's *on what*. It is never drawn in both.
- **A chain's step** (a quest with a `Parent`) is *the next step of #parent* and is placed with that parent, followed from it by
  the unit's `follows` (`WorkUnit.cs:64-65`). A quest whose `From` is `ask #id` belongs to that ask. Only a quest with neither, and
  no `PublishedBy`, is *by you*.
- **A Codex lead's subagents** are bound only from the threads of their own calls (§1.1). For such a lead the group says that its
  subagents appear here only when they call Daoris, in place of *Nobody else is working on this*.

Each member row says **how it got there** (§3.4). It opens what it is: a part's page, a quest's page, a co-worker's thread (D203 §6.4),
or, for a native child, its console tab while the window holds it and its lineage facts after.

### 3.2 What waits on what

Waits are edges, each from a record, never inferred from moments:

| Edge | Read from |
|---|---|
| the lead waits on its helper | `ended.awaits-helper` joined to the open ask in the helper box whose `asker` is a part of the entry |
| a taken quest waits on a quest it published | the quest unit's `awaits`, which is `Quest.Awaits`: **one quest at most** (`Quests.cs:96-102`) |
| a part or a quest's session waits on the person, on a helper, on its own quest | that session's own state and its unit's `awaits`, the same edges one level down |
| a quest waits to be taken | its unit's end `open` with no executor, and the driver's tick's verdict and sentence for it (`sittingBecause`): not driven here, the repository busy, the cap spent, waiting for an account |
| a child runs under its parent | the lineage's `Parent`; an edge *inside*, not a wait |

The read answers the edges, and the view draws them as words on the waiting row: *waits on Codex · helper*, *waits on #a1b2 in
driver*, *not taken yet: the concurrency cap (3) is spent*. The session's own wait is drawn once at the top, first: **Waiting on** /
在等 *#a1b2 (driver lane)*. A session that published quests and waits on none says so: *published 3, waits on none*. That is the
truth for a chat that took no quest, which has no `Awaits`, and for a driven lead that handed rows on and finished (§6.2). The
subagents' group says how many run, *2 working*, and states no cause: a background child holds no turn (§1.7).

### 3.3 The channel's last words

The read carries the ask's newest three channel messages, each with its moment and its author as the channel draws it. It also
carries the author's session, where a session posted the message: that is new on the page, though already on the record
(`Channel.cs:87-104`), and it places a message on the member that said it. *All messages* opens the ask's channel. A session with no
ask has no channel, and the block is not drawn.

### 3.4 The routing that put each member there

Each member's row says the door that started it, read from its record:

| Member | Its routing, as the record says |
|---|---|
| a part opened by a lane quest | *published to the {lane} lane by part {n}* (`PublishedBy`, `Lanes`); *by you* only where the quest has no `PublishedBy`, `Parent` or ask |
| a part a chat took | *took #{quest}* (`TookQuest`) |
| an earlier part | *handed on* with its `from_why`, or *retried*, as the parts strip says it |
| another repository's session | *asked of {repository} by part {n}*; *by ask #{id}* where `From` is an ask |
| a chain's step | *the next step of #{parent}* |
| a helper | *asked by this session* or *asked by you* (`askedBy`), under the rule `helpers` |
| a partner | *asked by you* (and by the lead once COAGENT1d lands) |
| a reviewer | *the workflow's second opinion* or *asked by you* (the opinion's occasion) |
| a native child | *started by {agent}*: the harness's own door, which Daoris neither starts nor confines (C2a held) |
| a brainstorm or meeting | *called by this session* or *by you*, once the request names it |

### 3.5 An ask's view

An ask's page draws the same component over the ask. It shows the ask's quests, each with its lanes and its taking session, and each
of its entries with its parts, subagents and co-workers folded to a count that opens the entry's own view. It also shows the channel's
last words. It replaces nothing on the ask's page: `AskWork` and `AskLines` keep their presses. The section sits above them and links
into them.

### 3.6 What the records gain

Only two stored changes, both additive and both desktop-only:

1. **The lineage keeps a child's label, its parent, its end word and its moments.**
   - **The protocol door.** The driver appends a line `by: "acp"` at the adapter's announcement, with:
     - the `subagentSessionId`;
     - the announced name and task, bounded: the name to 120 characters, the task's first line to 200;
     - **its place**: `parent`, the announcing wire session's id where that is a child's, or `depth: 1` where the root announced it.
       This is the one place on that door where a parent is said (`AcpStreams.cs:152-158`).
   - **The end.** It appends another `acp` line, with the wire's `state` word, **only from `subagent_state_update`**
     (`AcpStreams.cs:186-204`). `EndAllAsync` writes no lineage line. A child still open when the process lets go is closed by the
     part's own `end` line, as `ended` where clean and `lost` where not. A killed root's child therefore reads *lost*, never
     *finished*.
   - **The pipe door.** The stream's start line keeps the task's `description`, bounded the same way. `ChildBindings.StreamLine`
     drops it today. Its end line already keeps the `status` (`ChildBindings.cs:313-318`).
   - **The hook** stays ids only. A label is not an id, and the hook's rule (`lineage-hook.mjs:10-12`) is that no value a path may
     hide in reaches the file through it.
   - **The fold** keeps:
     - each child's first and last `at`;
     - its label, where a line gave one;
     - its parent and depth from an `acp` line, beside the stream's and Codex's;
     - **the end word as the wire or the stream said it**: `completed`, `failed`, `cancelled` or `disconnected`. A stop with no word
       (the hook's) stays `finished`. The view draws each with the output panel's own words (`work.panel.stream.state.*`), adding
       `disconnected`.
   - **Why a label may enter a file the rule kept to ids.**
     - The file is the session's, under the home, never synced and never on a shared host (D47).
     - For a child of the root, the label is already on the session's events as the card's title and text (`AcpStreams.cs:169-181`).
     - For a nested child, this is the first time its name and task reach disk (they were held only in memory, §1.1). It is the same
       class of text, from the same structured field of the harness's wire, kept in the same session's own folder. It is never parsed
       from the conversation, and it is bounded.
   - **The join.** The CONSOLE2 evidence says what the ids are: `subagentSessionId` is the harness's agent id, and a re-run is
     `<id>:generation:<n>` (§1.1). The hook's `agent_id` is the same harness's agent id.
     - The fold joins an `acp` child to a hook child where the ids are equal, or where the `acp` id is the hook's id followed by
       `:generation:<n>`. That is the same child run again, as the fold already reads a child named again after an end.
     - **An `acp` child that joins nothing stays the child's one row.** Hook children that join no `acp` child on a part that has
       `acp` lines are not drawn as rows. They are a count beside the group, *2 more its agent did not announce*.
     - On a part with no `acp` lines (the pipe door), the hook's and the stream's children are the rows, as they join today by
       `task_id`.
     - Nothing joins by time or by order.
     - Row a measures the join on the install's lineage files and on a stand-in's, on a first run and on a re-run, before the fold
       relies on it. Its counts go in D173's note.
2. **A brainstorm's and a meeting's request name who called it**: `session` and `ask`, null where the person did. This is additive,
   written by the door that makes the request. It is row f's, after BRAINSTORM1d and AGENTROOM1b give a session a door to call one.

Nothing else is stored: no team record, no structure, no lane on the session record (a part's lane is its quest's), no stored wait,
no turn. One fact is kept in memory and not stored: each console stream's newest moment, for *last heard* (§3.7).

### 3.7 The record's `updated`, and *last heard*

`updated` keeps its meaning, *when the record last moved*. Advancing it on every event would make *ran* and *waiting* wrong
(`headFacts.ts:169`, `:175`, `:207`), and would write the service's store at the rate of the agent's events.

On the protocol door, a child's words and tools never reach the session's events, and its lineage gets a line only at its start, its
end and its connector calls (§1.1). A moment taken from the events and the lineage would read *38 minutes ago* for a child that has
worked for 38 minutes. So each working part has a **last heard**, the first of these that holds:

1. **What the window heard**: the newest line of the part's own console stream and its children's streams. `SessionOutput` keeps
   each stream's newest moment beside its lines, in memory, with no stored change (`SessionOutput.cs:90-110`).
2. **After the window restarted** with the part still working, nothing was heard before the start. The view says *nothing heard
   since the window opened at 14:02*, never a moment it did not hear.
3. **The watcher's hearing** of a driven session (`SessionWatch.cs:183-189`), where the window holds no stream for the part.
4. **The part's events and its lineage's lines**, newest `at`.

The head's facts line for a working session gains one fragment where it has subagents or other parts working: *3 working with it ·
last heard 1 min ago* / *3 个在一起工作 · 1 分钟前有动静*, which opens the view. **The fragment gives way to *quiet*** wherever the
watcher has marked the session (`CallsStateSlice.cs:37-41`). The head never says two things about one session's hearing, and
D200's watcher is unchanged.

### 3.8 The data types

The read answers one type, fed whole to a presentational component (`Web/design/presentational.test.ts` covers `work/` and `asks/`):

```ts
type AtWork = {
  of: { session: string } | { ask: string };
  lead?: string;                         // the lead's record id, §3.1
  waitingOn: AtWorkEdge[];               // the session's own waits, §3.2; one quest at most
  groups: AtWorkGroup[];                 // in §3.1's order, an empty group absent
  channel: AtWorkChannelLine[];          // at most three, newest last; absent with no ask
  hint?: 'no-lanes' | 'not-driven' | 'one-at-a-time'; // §6.4
  at: string;                            // when the read was composed
};
type AtWorkGroup = {
  kind: 'parts' | 'subagents' | 'coworkers' | 'quests' | 'askedBy' | 'gathered';
  members: AtWorkMember[];
  folded?: number;                       // ended members past the newest three
  unannounced?: number;                  // subagents: the hook's children no announcement joined, §3.6
  seenByCalls?: boolean;                 // subagents: a harness whose children appear only when they call Daoris
};
type AtWorkMember = {
  key: string;                           // the record's own id: a session, a quest, a box, a child's native id
  kind: 'part' | 'child' | 'helper' | 'partner' | 'reviewer' | 'quest' | 'asker' | 'voice';
  agent?: string; mark?: string;         // the maker's mark, never a hue alone
  on?: { text: string; claim: boolean }; // claim: the agent's words (a child's label), drawn as such
  lanes?: string[];                      // a part's or a quest's lanes
  state: string;                         // the record's own state word; a child's end word as the wire said it
  since?: string; ended?: string;
  heard?: { at: string } | { sinceStart: string }; // §3.7
  alongside?: boolean;                   // a part in a tree of its own
  children?: { count: number; working: number }; // a part other than the lead
  routedBy: { door: string; by: 'session' | 'person' | 'ask' | 'chain' | 'harness' | 'workflow'; detail?: string };
  waitsOn?: AtWorkEdge[];
  depth?: number; parentKey?: string;    // a child's place in the tree; absent where no harness said
  opens: { session?: string; quest?: string; stream?: string };
};
type AtWorkEdge = {
  to: string;
  kind: 'helper' | 'quest' | 'person' | 'untaken' | 'partner';
  label: string;
  sitting?: { verdict: string; said: unknown }; // untaken: the driver's tick's verdict and its fact, §3.2
};
type AtWorkChannelLine = { at: string; author: string; session?: string; text: string; urgent?: boolean; question?: boolean };
```

## 4. How it updates without the window asking (NOBLOCK1)

- **One read, `SESSION_AT_WORK`, marked `[ByEvent]`**, keyed by its payload (a session or an ask), as NOBLOCK1 settles for reads
  asked per payload rather than one owner's snapshot (`D56.md:186-189`). It answers at once from what is held, or `{pending: true}`
  while nothing is held. `WindowCensusTests` gains the route as told by event.
- **The path from a wake to a held read.** `HeldAnswers` runs a read's work only when the page asks, so every wake goes through the
  page's one hub:
  1. A wake raises `DAORIS.STATE_CHANGED` with a new scope, **`work`**, naming the session where it is one session's
     (`StatePublisher.Publish(bus, StateScopes.Work, session)`, a new bit after `Notices`).
  2. The hub's `work` entry invalidates `SESSION_AT_WORK` for the sessions named, or every held one where none is
     (`stateHub.ts:27`, `:93-100`).
  3. The query asks again. `HeldAnswers` answers what it holds at once, runs the work on its own task (or joins the one under way),
     and tells `DAORIS.ANSWERED` where the answer moved.

  The hub's `sessions`, `quests` and `asks` entries also invalidate it, since a press that moves any of those moves a member.
- **The wakes**, each telling `work`:
  - **The ledger's families**: `sessions`, `quests`, `asks` and `channel`, heard on `DriverLoop.MoveHeard` by the existing
    `QuestsStateSlice.HearLedger` seam (`QuestsStateSlice.cs:130-144`). A connector's write (a quest published, a take, a wait, a
    message) is told with no press.
  - **The lineage files and the boxes**, heard by one watch of the window's own process, as `ForProposals` and `ForNotices` are, and
    whoever holds the home's lease (`HomeWatch.ForWork`). It has one topic with three fixed parts, none added or dropped:
    - the `sessions` folder with its subfolders, hearing only `*/lineage.jsonl`, its judge reading by name only the lineage files of
      the sessions whose reads are held (`HomePart.ByName`, as `HomePart.Workflows` reads only the runs named to it). The watch is
      bounded by what the window shows, and its listing never walks every session's folder;
    - `data/helpers` and `data/partners`, by `HomePart.Records`.

    A move under a session's folder tells `work` for that session. A box tells `work` for its `asker` or its lead.
  - **What the window hears**: a console stream's newest line, for *last heard*. It is coalesced to one telling per session per ten
    seconds, since the page draws *1 min ago* by its own clock.
- **Coalesced.** Wakes within one judgement compose once: the hub's `askAgain` and `HeldAnswers` joining the work under way each
  hold it to one read per burst, and the answer is told only where it moved.
- **The clock is the page's.** *for 4 min* and *1 min ago* are drawn from `since` and `heard` by the page's clock, never re-sent.
- **Off the window's thread.** The lineage read is a file read of one session's lines, folded in memory. The quest units are built
  by `QuestUnits.Read` over the quests and sessions the driver holds (`QuestUnits.cs:20`), never by `WorkUnits.ReadAsync`, which
  reads every branch's inventory, and never by a service call per member.

## 5. The view

### 5.1 Where it is

- **In a session**: a side bar view **At work** / **分工**, `atWork` in `ViewId` and `VIEW_IDS` after *workflow* (`placements.ts:9`,
  `:22`). It sits on the right by default, and is movable to the panel as every view is (D118's frame). The head's new fragment
  (§3.7) and the waiting row's *waits on* open it.
- **In an ask**: the same component as a section of the ask's page above *Quests*, titled the same.
- **D203's *Co-workers* section in the head stays** the place of the presses on a co-worker (*Let it ask*, *Not now*, *Stop…*), with
  `data-wait-section` (`.claude/knowledge/person-waits.md`). The view shows co-workers as rows that open their threads, and carries no
  second set of those presses.
- **The *Parts* strip stays** the place of each part's presses. The view's part rows open the part and carry no press, so the strip
  and the view list the same parts in the same order.
- **The graph's half is FABRIC-GRAPHVIEW1's running view**, in the *Workflow* view. A session's run's nodes are not redrawn here, and
  the flowchart design's rejection of a second running view stands. The view links to it where the session has a run (*Its
  workflow →*). Its quest rows read the same units, so the two views agree on a quest's state.
- **Help**: Ask Daoris's room learns the view's name in its prose list of views (`placements.ts`'s remark; `Help.cs`'s *The window*).

### 5.2 What it draws

From top to bottom, each block is absent where it has nothing to say:

1. **Waiting on** (§3.2): one line, the session's own wait; *nothing* is not drawn.
2. **This session**: the lead's row (mark · agent · *working* · *last heard 1 min ago* · its quest and lane, or *chat*), then each
   other live part (*alongside, in a tree of its own*, its lane, its subagents' count), then ended parts behind *{{count}} earlier
   parts*.
3. **Subagents**: one row per child of the lead as a tree, indented by depth: *type · label · working · for 6 min*, or its end word
   and time (*completed 14:02*, *failed 14:05*), or *ended with its part*, or *lost*. Finished children fold behind *{{count}}
   ended* past the newest three; a working or lost child never folds. The heading carries *{{count}} working*. A note under the
   heading says what the person is looking at: *Subagents work inside this session's own agent and on its account: they stop when
   it stops.* Beside it is the unannounced count where there is one (§3.6), or, for a Codex lead, the line that its subagents
   appear only when they call Daoris.
4. **Co-workers**: D203's rows, read-only here.
5. **Asked through quests**: one row per quest: *#a1b2 · core lane · {title}*, then its session (*Claude Code · working · 12 min*,
   or *not taken yet: {the driver's reason}*), then its own *waits on*, then its routing in the faint line.
6. **Asked by**: one row.
7. **Gathered**: once row f lands.
8. **On the channel**: three lines, *All messages* →.
9. **The hint** (§6.4), where it holds.

Every state word is a status from the glossary's kinds, with the status palette and a word or icon beside every hue, never a hue
alone (D41). Every row is one button reaching the member, in the order drawn, so the keyboard and a reader meet them as the eye does.

### 5.3 At each width, theme and language

The side bar is 45% of the window, never under 300 px, and the whole frame under 768 px (`Web/work/frame/layout.ts:44`). A region
lays out by its own width (`docs/2026-09-19-platform-ux.md:304-311`).

| The view's width | Where | How it lays out |
|---|---|---|
| **about 696 px** | the install's 1546 px window, at 45% | the wide row: mark, who, on what, state and since on one line; routing on a second faint line; the channel's lines whole to two lines |
| **430 px** | a roomier side bar (the 888 px window gives about 400) | the narrow row: who and state on the first line, on what on the second (two lines, then *…* with the whole in the tip), routing behind the row's tip; the tree's indent capped at two steps, deeper children said by *depth 3* |
| **300 px** | the side bar's floor (`DOCK.floor`), where zh is narrowest | the narrow row with the state word under the name where the two do not fit on one line; the tree's indent capped at one step; the channel's lines to one line, whole in the tip |
| **680 px** | the whole frame (under 768 px) | the narrow row at the frame's width; the side bar's view replaces the conversation while open, its close returning to it, as every view does there |

- **Themes**: tokens only, both themes' status colours, checked by the tokens gate. A lost child is drawn in `--warn` with its word,
  never red alone.
- **Languages**: every label is in both catalogues. zh lines are shorter by count but wider per glyph, so the narrow row wraps on the
  measured box, never by character count (FLOWCHART1e's measured-box check is the gate's precedent).
- **Stories** in the side bar at 300 and 430 px, as the side bar's views are storied (`AskPanel.stories.tsx:335-341`), and as the whole
  frame at 680 px, in both themes and both languages. One story per state:
  - a chat with three subagents (one working, one completed, one failed);
  - a nested child with *parent not said*;
  - a Codex lead whose subagents appear only when they call Daoris;
  - a lost child;
  - a child with two the agent did not announce;
  - a session with two lane parts alongside and one untaken lane quest with its reason;
  - a quest to another repository waiting on a question;
  - a lead waiting on a helper;
  - a chat that took a quest;
  - an ask with two entries;
  - each hint;
  - the empty view, which says *Nobody else is working on this* / *没有其他成员参与*.

### 5.4 Words

Keys under `work.atWork.*` (D203 §8.3 reserves `work.team.*` for its *Co-workers* section), each a label with its kind's budget
(D116 §3), checked by `names:check -- --strict` and `i18n:check`. A child's end words are the output panel's own
(`work.panel.stream.state.completed`, `failed`, `cancelled`, and a new `disconnected` / 已断开), so a child's end reads the same in
the panel and the view.

| Key | en | zh |
|---|---|---|
| `view` | At work | 分工 |
| `group.parts` | This session | 本会话 |
| `group.subagents` | Subagents | 子智能体 |
| `group.coworkers` | Co-workers | 同事 |
| `group.quests` | Asked through quests | 通过委托请求 |
| `group.askedBy` | Asked by | 由谁请求 |
| `group.gathered` | Gathered | 召集 |
| `group.channel` | On the channel | 频道上 |
| `waitingOn` | Waiting on {{what}} | 在等{{what}} |
| `subagentsNote` | Subagents work inside this session's own agent and on its account: they stop when it stops. | 子智能体在本会话自己的智能体里、用它的账户工作：它停止时它们也停止。 |
| `subagentsWorking` | {{count}} working | {{count}} 个在工作 |
| `subagentsByCalls` | Its agent's subagents appear here only when they call Daoris. | 它的智能体的子智能体只有在调用 Daoris 时才会出现在这里。 |
| `unannounced` | {{count}} more its agent did not announce | 另有 {{count}} 个智能体未通告的 |
| `child.ended` | ended with its part | 随所属段结束 |
| `child.lost` | lost when its session stopped | 会话停止时丢失 |
| `child.noParent` | parent not said | 未说明上级 |
| `part.alongside` | alongside, in a tree of its own | 并行，在自己的工作树中 |
| `part.children` | {{count}} subagents · {{working}} working | {{count}} 个子智能体 · {{working}} 个在工作 |
| `part.earlier` | {{count}} earlier parts | 更早的 {{count}} 段 |
| `quest.untaken` | not taken yet: {{why}} | 尚未接手：{{why}} |
| `quest.nextStep` | the next step of #{{quest}} | #{{quest}} 的下一步 |
| `routed.lane` | published to the {{lane}} lane by {{by}} | 由{{by}}发布到{{lane}}泳道 |
| `routed.took` | took #{{quest}} | 接手了 #{{quest}} |
| `routed.repository` | asked of {{repository}} by {{by}} | 由{{by}}向{{repository}}提出 |
| `routed.ask` | by ask #{{ask}} | 由需求 #{{ask}} 提出 |
| `routed.harness` | started by {{agent}} | 由{{agent}}启动 |
| `heard` | last heard {{ago}} | {{ago}}有动静 |
| `heardSinceStart` | nothing heard since the window opened at {{time}} | 窗口 {{time}} 打开以来没有动静 |
| `withIt` | {{count}} working with it | {{count}} 个在一起工作 |
| `published` | published {{count}}, waits on none | 已发布 {{count}} 个，未等待 |
| `empty` | Nobody else is working on this | 没有其他成员参与 |
| `hint.noLanes` | This repository declares no lanes, so its work runs inside this conversation. A `daoris.lanes.json` lets it hand rows to further parts of this session. | 这个仓库没有声明泳道，所以它的工作都在这个对话里进行。有了 `daoris.lanes.json`，它就能把条目交给本会话的后续段。 |
| `hint.notDriven` | This repository is not driven here, so a quest to its lanes would not start on this machine. | 这个仓库不在本机驱动，所以发给它泳道的委托不会在本机开始。 |
| `hint.oneAtATime` | This repository's sessions run one at a time here, so a quest to its lanes would wait for this one. | 这个仓库的会话在本机一次只运行一个，所以发给它泳道的委托会等这一个结束。 |

**The glossary's words are kept**: 委托 for quest, 需求 for ask (*the ask's channel* is 需求的频道), 会话 session, 段 part, 泳道 lane,
频道 channel, 子智能体 subagent, 智能体 agent, 账户 account, 工作树 tree, 并行 alongside, 助手 helper, 搭档 partner, 阅读者 reviewer,
同事 co-worker, 主导 lead, *not driven here* 不在本机驱动.

**Words kept off the window.**
- *Team* / 团队 stays D203's word for the structure in prose, and *team* already names the people sharing a remote (D203 point 9),
  so neither the view's title nor a group says it.
- *Turn* is not a boundary any record keeps (§1.7), so no label says *this turn*.
- *Run* is the glossary's workflow run (进展, matched as *this run*), so no label says *this run* either. The subagents are the lead's
  agent's, which is what the note says.

**New glossary terms**: *at work* / 分工 (a view), and `disconnected` / 已断开 as a child's end word.

## 6. The routing half

### 6.1 The ways, and what each is for

| Way | Use it for | It costs | Its bound |
|---|---|---|---|
| a lane's quest to the session's own repository | a row of the roadmap that can be built and checked on its own: **a further part of this session**, in a tree of its own where the repository opens trees, landing on the session's one branch under its one *Accept* | one part's turns on an account; a tree | the person's drive and trees here, the machine's cap, and the person's account rules (§6.5) |
| a lane's quest to another repository | that repository's owned work | its session | D45: never write across |
| a helper | a question about the work in progress, answered once | one turn, reading | `helpers`; two at once; D163 |
| a partner | a piece of code written apart, taken in by the lead's merge | one turn in a clone | `partners`; D199 |
| a native child | a short search or read the lead needs before it goes on | the lead's own account and process | the harness's; Daoris confines it only after C2a |

**The difference that matters to the person.** A lane part is a record Daoris starts, shows, stops, resumes and lands with the
session. A native child lives inside the lead's agent process, which a limit, a crash or a stop ends whole, with no record of its own.
A roadmap of rows, each of which can be checked on its own, belongs in lane parts. A process that runs a roadmap through children
risks every row at once. The parts still land together, on the session's one branch (D167's LANDSYNC0 amendment), so the person
accepts the session's work once. A superseded part is named and never dropped unsaid.

### 6.2 How an agent is told (the doctrine)

The co-working skill (`canon/core/skills/co-working/SKILL.md`, synced to `.claude/skills/co-working/SKILL.md` and both examples)
gains a section, *handing work out*. It is project-agnostic, written under the canon-authoring rules, and says the table above in
the agent's words. It decides nothing: the agent chooses within the person's rules, and Daoris never splits a session's work itself
(D193's rejection of agents directing agents; D24).

- **When it applies** (§6.5's preconditions). The repository lists its lanes in `registry`. The session is one the driver started,
  so its repository is driven here; or it is a chat whose person asked it to hand its work out. Otherwise the section's first way
  is not open, and the agent works its rows itself.
- **What goes where.**
  - Rows that can be built and checked on their own go to the repository's own lanes, as quests (`quest_publish` to
    `repository:lane`), one quest per row with its proof. Each becomes a further part of the session and lands with it.
  - A question goes to a helper.
  - A piece goes to a partner, where the door exists.
  - Subagents are for reads and searches the agent needs before it goes on.
- **After publishing, by kind of session.**
  - **A chat** says what it published and ends its turn. Its view shows each part and quest and its end. A quest's outcome is an
    ordinary message and never starts a turn (D188 point 2), so the chat is told at its next turn.
  - **A driven session** never ends a turn while it still holds its quest with nothing named: that parks it on the person
    (`Observation.cs:167-174`), the case HELPERWAIT1 fixed for helpers (D203 point 5). It does one of two things:
    - it **finishes its own quest** (`done` or `ready`), naming the quests it handed on in its reason, where its own work is done;
    - or it **waits on one of them** (`quest_respond wait`, one `on`) and ends its turn, where its own work cannot go on without that
      one. A lead waits on at most one quest (§3.2).
- **What it reads afterwards**: `quest_list`, and the channel.

### 6.3 A chat

A chat that took no quest has no `Awaits` and cannot `wait`. A chat that took one (CHATTAKE1) is read through `Session.TookQuest`:
the quest is its *Asked by*, and its wait is that quest's (§3.1). Either may publish to its own repository's lanes: `PublishedBy`
names it, and each quest opens a further part of its session (§1.3). D188 settles how a chat hears its quests' ends: a quest's
outcome is ordinary and never starts a run (`D188.md:18`, `:29-30`). This design shows the ends in the view and starts nothing.

### 6.4 Where the way is not open (SELFASK1)

Three places say how, each in Daoris's fixed words:

1. **The refusal** (SELFASK1's row, unchanged here). Where lanes are declared, it names them by their addresses. Where none are, it
   says that a `daoris.lanes.json` is what lets the repository hand its own work out.
2. **The registry's row** for a repository with no lanes says the same (SELFASK1).
3. **The view's hint** is drawn where the lead's agent process started at least two subagents, counted from the lead's lineage since
   its process's last `end` line. It is said where the case happened and nowhere else, as one of:
   - `no-lanes`: the repository declares no lanes, and it is driven here with trees of its own;
   - `not-driven`: it is not driven on this machine (`config.Drivable`), whether or not it declares lanes, since a quest to its lanes
     would not start here;
   - `one-at-a-time`: it is driven but does not open its own trees (`config.OpensOwnTree`), so a quest to its lanes would wait for
     this session.

   The hint links to the guide's lanes section. It is a fact about the repository and this machine, never a judgement of the
   agent's choice.

### 6.5 What is the person's

- **The rule that bounds an agent's quests to its own repository** is the person's choice to drive that repository here (D46 §2,
  `config.Drivable`, `Planner.cs:1889-1891`) with trees of its own (`config.OpensOwnTree`), together with the machine's concurrency
  cap (`config.Cap`, *the concurrency cap is spent*, `Planner.cs:2048`), which holds today. DEV6's lane cap adds to it once built.
  - **Why no rule shaped like `helpers`.** A quest to a driven repository already starts under exactly these conditions, whoever
    publishes it: an ask, the person, or another repository's session. A session publishing to its own lanes gains no power that
    another repository's session lacks. Driving is the person's opt-in to sessions they did not start, and the cap bounds their
    count.
  - **What `helpers` covers that this does not.** A helper is a turn started outside the driver's plan and its cap
    (`HelperBox.cs:86-88`). A quest goes through the plan. If the person wants a separate rule for self-published quests, it is a
    later row. It is named in §9 and not designed here.
- **Whether a repository has lanes.** Writing `daoris.lanes.json` is a change to that repository, made by its own sessions or its
  owner, through its own landing (D45). Daoris never writes one.
- **Which accounts they spend** (D197's walk, the actors of D205).
- **`helpers` and `partners`**, the rules already in Settings. A native child's tool falls under the person's permission rules (D72),
  which can deny the harness's agent tool as any other.
- **Accepting the session's work**, with its parts on one branch, by the entry's one *Accept* (ACCEPTALL1). Nothing here lands
  anything.
- Not the person's to press each time: the agent's choice within those rules (§6.2), as D203 §3.3 has it for co-workers.

## 7. The rows, in build order

Each row is one feature a person can see working, built driver to page by one builder (D187), in en and zh wherever words are drawn.
They are filed in `TASKS.md` when this design is accepted; until then COWORKVIEW1 points here.

1. **COWORKVIEW1a — a chat's subagents seen** (driver, modules, web-shell; replaces FABRIC-C1b's read and display and lifts its
   *after C2a*, since showing restricts nothing).
   - **Its first step is the measure.** Read the install's lineage files and a stand-in's on each door, on a first run and on a
     re-run (`:generation:<n>`). Record whether `SubagentStart` reaches the hook over the adapter, and how many `acp` ids join a hook
     id equal and by the generation suffix. The counts go in D173's note before the fold joins.
   - **What it builds.**
     - §3.6 point 1: the `acp` lines with their parent, depth and end word, written only from the wire; the pipe's description; the
       fold's moments, label, end word, generation join and unannounced count.
     - §3.7's *last heard*, with `SessionOutput`'s newest moment per stream.
     - `SESSION_AT_WORK` `[ByEvent]` with the `parts` and `subagents` groups and the `work` scope; `HomeWatch.ForWork` over the
       lineage files; `sessions at-work <session> [--json]`.
     - The side bar's `atWork` view with those two groups; the head's fragment, which gives way to *quiet*; §5.3's stories for its
       states; §5.4's words.
   - *Proof:*
     - `ChildBindingsTests`: a label and moments folded; the parent from an `acp` line; the wire's end words kept; a killed root's
       child `lost`, never `finished`; a Codex child `ended`; an `acp` id with `:generation:2` joined to its hook id, and an unjoined
       hook child counted, never a row; no join by time.
     - A new `AcpStreamsTests` class: the announcement appended bounded with its parent; the end appended from
       `subagent_state_update` only; `EndAllAsync` appending nothing.
     - `ChildLineageTests`; `SessionOutput`'s newest moment.
     - `WindowCensusTests` (told by event); `PipeCensusTests`; the verb's words tests.
     - A timed case under a slow stand-in, answering within the window's bound while the event delivers.
     - `presentational.test.ts`, `i18n:check`, `names:check -- --strict`, `HelpCoverageTests` (no new press).
     - **The person's first:** a chat working through native subagents shows each one, its state and its end, in **two runs**.
       In run 1, a stand-in chat on the protocol door starts three children: one completes, then the root is killed, and the other
       two read *lost*. In run 2, the chat is resumed and starts one child, which reads *working* with *last heard* moving. The view
       draws *completed 14:02*, *lost*, *lost* and *working* in en and zh, at 300, 430 and 680 px and in both themes, seen on the
       install's window.
   - *Trap:* joining an `acp` child to a hook line by order or time, or writing an end the driver made.
2. **COWORKVIEW1b — a session's lane parts and the quests it asked, with their waits** (driver's read, web-shell; after a).
   - **What it builds.** The `parts` group's lane and *took* routing, *alongside* and each part's subagents' count. The *Asked
     through quests* group from the quest units, with the takers, their waits and the driver's reason for an untaken quest. The
     *Waiting on* line, one quest at most. *Asked by* (`Quest ?? TookQuest`; `Parent`; `From`). The `published` line.
   - *Proof (the person's second):* **a session that handed rows to its lanes shows each part and what it waits on.**
     - An example repository with two lanes (family phase 4b's engine, `core` and `media`), driven with trees of its own. A session
       publishes to each and to another repository; one lane quest is taken and runs alongside, and one is untaken while the cap
       is spent.
     - The view draws the taken lane's session **once, as a part**, in the same order as the *Parts* strip. The untaken one is
       under *Asked through quests* with *not taken yet: the concurrency cap (1) is spent*, and moves to *This session* when
       taken. The other repository's session shows *waits on #q of repo*. Each is drawn in en and zh.
     - The read's tests: a chain's step under its parent, never *by you*; an ask's quest under its ask; a chat's taken quest as
       its *Asked by*.
3. **COWORKVIEW1c — who serves it, and what was said** (driver's read, web-shell; after b).
   - **What it builds.** The *Co-workers* group and its edges. Where COWORKUX1a's exchange read has landed, the co-workers are that
     read; where it has not, the first of the two to build writes the one reader the other uses. The helper and partner boxes join
     the `work` watch. The channel's three lines, with their sessions.
   - *Proof:* a lead waiting on a helper shows *Waiting on Codex · helper* and the helper's row opening its thread; a channel line
     placed on the part that posted it; en and zh.
4. **COWORKVIEW1d — an ask's view** (web-shell; after c). §3.5 on the ask's page.
   - *Proof:* an ask with two entries and a channel: the section above *Quests*, its rows opening each entry; stories.
5. **COWORKVIEW1e — the routing told** (canon, web-shell; with or after SELFASK1). §6.2's skill section, through canon and then this
   repository and both examples synced, under the canon-authoring rules; §6.4's three hints.
   - *Proof:* `daoris check` and the canon's tests. The hints' stories, and their rule's tests: each hint drawn at two subagents in
     one process, under its own condition; none drawn with lanes declared and driven with trees, or at one subagent.
6. **COWORKVIEW1f — brainstorms and meetings on their caller's view** (driver, web-shell; after BRAINSTORM1d and AGENTROOM1b).
   §3.6 point 2.
   - *Proof:* the request's fields written by each door; the *Gathered* group's rows.

SELFASK1 is a row of its own already and is unchanged. FABRIC-C1b's row is rewritten to point here when this is accepted, since row
a meets both its halves.

## 8. Rejected

- **A team or structure record**, or a lane on the session record: two stores for one fact (D173's guard, D203 §3.4).
- **Who does what read from the conversation**: an agent's prose, a plan's text or a tool card's words, parsed. A record's field or
  nothing.
- **A model's summary of who is at work** (D24).
- **`updated` advanced on every event**: it would break *ran* and *waiting* and write the store at the agent's rate (§3.7).
- **Native children as co-workers**: they have no record, no confinement, and nothing Daoris can stop by itself (D203 point 1 stands).
- **The child's prompt kept whole**, or the hook writing labels: a bounded label from the harness's announcement only, and the hook
  ids only (§3.6).
- **An end written at the session's end for an open child** (round 2): it would fold as the harness's own stop and read a killed
  root's child as *finished* (§3.6).
- **A lane part drawn twice**, as a part and as a quest's member (round 2): one record, one home (§2 point 4).
- **A group named for a turn, and a stored turn line** (round 2). The read offered a driver line at each turn's end as the
  alternative. It is rejected: it is a stored change for a boundary the person does not act on, and the subagents are the lead's
  agent's either way (§1.7).
- **A `helpers`-shaped rule for self-published quests, now** (round 2): the person's drive, trees and cap already bound them as they
  bind every quest (§6.5). It is left as a later row if wanted.
- **A watch part per attended line, added and dropped** (round 1's): `HomeWatch` cannot remove a topic (§1.8). One fixed part,
  its judge reading by name, replaces it.
- ***Team at work* and `sessions team` widened** (round 1's): D203's word and verb (§5.4, §7).
- **A second running view or a free graph of who is at work**: the running view is GRAPHVIEW1's, in *Workflow*. This view is a list,
  which reads at every width (the flowchart design §8).
- **Daoris splitting a chat's roadmap into quests by itself**, or a coordinator agent dispatching the parts: D193's rejection, D203
  §3.4.
- **A poll** of the lineage or the boxes from the page: NOBLOCK1's rule.
- **The hint shown everywhere a way is not open**: most chats need none. It is said where subagents ran a roadmap (§6.4).

## 9. Not built, and unmeasured

- **Unmeasured:**
  - whether `SubagentStart` reaches the hook over the adapter, and how many `acp` ids join a hook id on a first run and a re-run
    (row a's first step);
  - the view's budgets in zh at 300 px;
  - how many lineage lines a long process writes, and so the fold's cost per wake;
  - whether a chat in its repository's checkout counts as the repository's active session for `one-at-a-time` (the hint takes the
    driver's own verdict for its untaken quests, never its own guess).
- **Not designed here:**
  - DEV6's lane reservations and cap, and DEV9's steward;
  - a separate rule for self-published quests (§6.5);
  - a child's own transcript after the window restarts (the console streams stay in memory);
  - a chat told whether its repository is driven here before it publishes. The view's hint says it after the fact.

## 10. What it amends, once accepted

- **D173**: the FABRIC-C1a note's *ids only* is kept for the hook and the connector. The driver's lines gain a bounded label, a
  parent and the wire's end word, and the fold its moments and the generation join (§3.6). FABRIC-C1b's read and display are
  COWORKVIEW1a's, no longer after C2a. GRAPHVIEW1 stays the graph's half.
- **D203**: points 1 and 2 stand as written. The view draws the team (the entry's parts and its co-workers) and, beside it, records
  point 1 keeps out, each under its own heading. `sessions team` stays COWORKUX1a's. This design's verb is `sessions at-work`.
- **D115**: §2.2's *work for another session* is, under D167, a further part of the publisher's session where the lane is its own
  repository's. Its lanes are shown on that session's view, and a repository where the way is not open is told how (§6.4, with
  SELFASK1).
- **D167**: a lane quest to the session's own repository is a further part of the session that published it (point 2's origin
  rule), drawn once as a part, and landed by the entry's one *Accept*.
- **D176, D177**: a request names who called it (row f).
- **D118's frame**: a side bar view, `atWork`, after *Workflow*.
- **These stand:** D24, D45, D46, D47, D56 (NOBLOCK1), D116, D137, D169, D186 and the flowchart design, D188, D193, D200.

## 11. Round 2: the read's findings and their answers

Every finding of the Opus foundation read of round 1 (`docs/2026-10-24-cowork-view-reads.md` §4), each checked against the code on main
at `426ed2b73`. *Taken* means the design now says it. *Answered* means it is not taken as written, for the reason given. *Deferred*
means a later row carries it. The read's §1 to §3 corrections are findings below.

| # | Finding (blocking in bold) | Checked against | Answer |
|---|---|---|---|
| **1** | **A lane quest to a session's own repository makes a further part of that session's line** | `SessionEntries.cs:33-37`, `:96-113`; `SessionLedger.cs:1599-1616`; `Planner.cs:1716-1759`; `D167.md:22-31`, `:73-82`, `:286-336` | **Taken**, the second way: such a session is a part, drawn once under *This session* with its lane, its routing and *alongside*, in the *Parts* strip's order. An untaken quest to the session's own lanes is under *Asked through quests* until taken (§2 point 4, §3.1). The subagents are read only for the lead (§3.1, with finding 7). The lead is the attended live part, else the newest live part by `created` then id. The entry rule's effect is stated in §1.2, §1.3, §1.9, §6.1 and §6.5, and the landing on the entry's one branch. The hint says *further parts of this session*. Row b's proof asserts one row per part and the strip's order. A D167 note is added beside D115's. |
| **2** | **The routing doctrine parks a driven lead on the person** | `Observation.cs:159-174`; `Quests.cs:96-102`; `QuestTools.cs:366-374`; `QuestExchange.cs:1454-1505` | **Taken** as named: a chat ends its turn; a driven session finishes its quest naming the quests it handed on, or waits on one and ends its turn, and never ends a turn holding its quest with nothing named (§6.2). §3.2 says a lead waits on at most one quest. The exchange refuses no wait on a quest to one's own lane, so the wait is open. |
| **3** | **The routing names no rule of the person's and no precondition** | `QuestTools.cs:148-163`; `QuestExchange.cs:108-113`; `HelperBox.cs:86-88`; `Planner.cs:1889-1891`, `:1964-1966`, `:2010-2021`, `:2048`; `D203.md:31-33` | **Taken**, the second way: the rule is the person's drive with trees of its own and the machine's cap, which hold today. The reason is that a quest to a driven repository starts under exactly these, whoever publishes it, and a helper's rule covers a turn outside the plan (§6.5). The skill section applies only where lanes are listed and the session is driven, or the chat's person asked (§6.2). The hint is one of three, naming a missing drive or one-at-a-time where the way is not open (§6.4). A `helpers`-shaped rule is rejected for now and left as a later row (§8, §9). |
| **4** | **None of §4's wakes reaches a held read** | `HeldAnswers.cs:20-22`, `:74-106`, `:170-177`; `held.ts:24-34`; `stateHub.ts:27-68`, `:93-100`; `StatePublisher.cs:258-264`; `HomeWatch.cs:196-213`, `:234-258`, `:270-280`; `QuestsStateSlice.cs:130-144`; `CallsStateSlice.cs:23-24` | **Taken**, the first way: every wake tells a new `work` scope on `STATE_CHANGED`, and the hub invalidates `SESSION_AT_WORK`, whose next ask runs the work and is told `ANSWERED` (§4). The ledger's families come through `MoveHeard` by the existing `HearLedger` seam. The lineage files come through one fixed part over `sessions/` hearing `*/lineage.jsonl`, its judge reading by name only the held sessions', in the window's own process as `ForProposals` is. The boxes are fixed parts of the same watch. The `calls` slice is no longer a wake: *last heard* comes from the streams (finding 8). |
| **5** | **The protocol door's `acp` line omits the parent and can write a false end** | `AcpStreams.cs:110-148`, `:152-204`; `lineage-hook.mjs:31-46`; `ChatRunner.cs:2397-2419`; `ChildBindings.cs:195`, `:211`, `:241-244`, `:268-291`; CONSOLE2 evidence `:69-70` | **Taken** as named: the `acp` line carries `parent` (the announcing child's id) or `depth: 1` (the root's); the end line is written only from `subagent_state_update`, and `EndAllAsync` writes none, so the part's `end` line makes an open child `ended` or `lost`; the fold keeps the wire's end word and the view draws it with the panel's words (§3.6, §5.4). Row a's proof covers all three. |
| **6** | **The join's fallback draws one child twice; the evidence about the id is on record** | CONSOLE2 evidence `:61-64`; `D173.md:100-102`; `ChildBindings.cs:167-178` | **Taken** as named: the evidence is cited (§1.1). The join is by equal id or the `:generation:<n>` suffix, as the fold already reads a child run again. An unjoined announcement stays the child's one row, and the hook's unjoined children are a count beside the group, never a second row (§3.6). Row a measures a first run and a re-run, and its proof includes the unjoined case. |
| **7** | **No record keeps a turn, so *Inside this turn* names a boundary nothing holds** | `ChatRunner.cs:1804`, `:1826`, `:2397-2399`, `:2486-2497`; `glossary.json` (*run* 进展, matched as *this run*) | **Taken**, the first way, with one change of wording: the group is *Subagents* 子智能体, and its note says they work inside the session's own agent and stop when it stops. *In this run* is not used, since the glossary's *run* is a workflow's (进展) and `names:check` would read it so. The edge "the turn has not ended because they run" is dropped; the heading says *n working* (§3.2, §5.2). The hint counts per process, since its last `end` line (§6.4). The stored turn line is rejected (§8). |
| **8** | ***Last heard* stays still while a child works, and duplicates the watcher's** | `AcpStreams.cs:16-21`, `:95-102`; `SessionOutput.cs:9`, `:90-110`; `SessionWatch.cs:64-71`, `:183-189`; `CallsStateSlice.cs:37-41`; `headFacts.ts:199` | **Taken** as named: *last heard* is the newest line of the part's and its children's streams, kept in memory; after a restart, *nothing heard since the window opened at…*; then the watcher's hearing, then the events and the lineage. The head's fragment gives way to *quiet* wherever the watcher marked the session (§3.7). |
| **9** | ***Team at work* and `sessions team` take D203's word and verb** | `D203.md:22-24`, `:58-60`, `:83`, `:199-206`; coworker design `:282-283`, `:661-664` | **Taken**, the first way for each: the title is *At work* (分工); the read is `SESSION_AT_WORK` and the verb `sessions at-work`, beside an existing `sessions team` left exactly as COWORKUX1a defines it; the keys are `work.atWork.*`; the types are `AtWork…`. The D203 note says that points 1 and 2 stand, and that the view draws the team and other records beside it, widening neither (§2 point 6, §10). `SESSION_WORK` and `sessions work` were not taken, since `work` already names the units and `WORK_STATE`. |
| **10** | **Rows a and b are layers, and the first row shows the owner nothing** | `D187.md:13-16`; round 1 §7 | **Taken** as named: row a is *a chat's subagents seen*, built driver, modules and web-shell by one builder, with the join measured as its first step and finding 5's line in it. Each later row is a feature with its own proof (§7). Rows are renamed a-f. |
| 11 | A chat can take a quest | `QuestTools.cs:395-403`; `Sessions.cs:246-250`; `SessionEntries.cs:102-106`; `D167.md:176`; `D188.md:18`, `:29-30` | **Taken**: `Session.Quest ?? Session.TookQuest` everywhere a part's quest is read; §6.3 rewritten and cites D188 point 2; §1.9 says it. |
| 12 | A null `PublishedBy` does not mean "by you" | `Quests.cs:104-109`; `WorkUnit.cs:64-65` | **Taken** as named: a quest with a `Parent` is *the next step of #parent*, placed by `follows`; one whose `From` is `ask #id` is the ask's; only a quest with neither is *by you* (§3.1, §3.4). |
| 13 | An untaken quest is shown without the reason it sits | `signals.ts:5-7`, `:145-158`; `Planner.cs:1023-1048` | **Taken**: the edge carries the tick's verdict and its fact, worded by the page as the Overview words them, *not taken yet: {why}* (§3.2, §3.8, §5.4). |
| 14 | Smaller claims to correct | `QuestTools.cs:37-40`; `D173.md:131-133`, `:5177-5180`; `AcpStreams.cs:158`, `:169-181`, `:202`; `Planner.cs:1964-1966`; `DaorisHome.cs:340`; `TASKS.md` | **Taken**, each: (a) §0 and §1.9 say the tool's description names lanes, and the case lacked lanes; (b) §1.10 says the hook's record on the install is unmeasured, row a's to read; (c) §1.1 and §3.6 say only a root's child has a card, and that a nested child's label reaches disk for the first time, with the reason that holds; (d) §1.3 and §9 say PAR1 already runs sessions side by side with trees of their own, and DEV6 adds reservations and a cap; (e) `data/helpers`; (f) rows cited by id. |
| 15 | Three Chinese words go against the glossary | `glossary.json` (*part* 段, *account* 账户, *subagent*); `names-check.mjs:184-188`, `:197` | **Taken**: 随所属段结束, 账户, *Subagents* (§5.4). |
| 16 | The stories use the main area's widths, not the side bar's | `storyWidths.tsx:9-13`; `layout.ts:44`; `AskPanel.stories.tsx:335-341`; `FilePreview.stories.tsx:5` | **Taken**: §5.3 has a 300 px row and a 430 px row; the stories are at 300 and 430 px in the side bar and 680 px as the frame, in both languages and both themes. |
| 17 | Row c's proof cannot happen in one run | `ChildBindings.cs:241-244`; `ChatRunner.cs:2486-2497` | **Taken**: row a's install proof is two runs, a killed one and a resumed one, and names *completed 14:02*, *ended with its part* and *lost* apart (§7). |
| 18 | A Codex lead's children are seen only when they call Daoris | `ChildBindings.cs:223-235`; `D173.md:120-122`, `:5184-5188` | **Taken**: for such a lead the subagents group says so in place of the empty state (§2 point 3, §3.1, §5.4), with its story. |
| 19 | The quests group re-derives what the units already carry | `WorkUnit.cs:53-73`, `:140-152`; `QuestUnits.cs:20`; `WorkUnits.cs:54-70` | **Taken**: each quest member's state, wait and step are read from its unit, built by `QuestUnits.Read` over the quests and sessions held. That is the same adapter `WorkUnits.ReadAsync` uses, without the branches' inventories it also reads, so the view and GRAPHVIEW1 agree on a quest's state (§3.1, §4, §5.1). |

**What stays owed.** Codex's read, on this text (D191, its 2026-10-23 amendment). The read asked for a narrow next round over §1.3,
§1.9, §3.1-§3.2, §3.6-§3.7, §4, §6 and §7. The design stays proposed until both reads' blocking findings are taken.
