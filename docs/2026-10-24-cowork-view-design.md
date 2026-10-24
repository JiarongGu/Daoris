# Co-working seen and routed: a session's team, what each part is on and what waits on what, read from the Fabric's records, and how a session hands its work out (COWORKVIEW1, proposed, 2026-10-24)

**Standing:** proposed, round 1, under D191, with dated notes amending D173 (the Collaboration Fabric), D203 (co-workers) and D115
(lanes). Written by Claude Opus 5.5 from the code on main at `fd1d2727e`. Two reads follow before anything is built: Codex's, which
every design takes, and the independent Opus foundation reader's, since this changes the data model of a record (the lineage file
gains a label and its moments, §3.6) and adds a read across the driver, the modules and the page (§4) (D191 and its 2026-10-20 and
2026-10-23 amendments). Nothing here is built, publishes, signs in or spends an account.

Paths: `Driver/` is `src/Daoris.Desktop/Daoris.Desktop.Driver/`, `Modules/` is `src/Daoris.Desktop/Daoris.Desktop.Modules/`, `Core/`
is `src/Daoris.Service/Daoris.Service.Core/`, `Mcp/` is `src/Daoris.Service/Daoris.Service.Mcp/`, `Web/` is `src/Daoris.Web/src/`.
`Dnn` is `docs/decisions/Dnn.md`. Every fact in §1 was read in that code; a fact this design could not find is said as *not found*,
with what was searched.

## 0. The case

The row COWORKVIEW1 in `TASKS.md` carries it. On the install a kit's chat was moved to another account into a fresh conversation
(`chat.moved-from`) and went on working its roadmap one native sub-agent at a time inside one long turn. Its page showed little but
*Next, RV16*, its record's `updated` stood at the turn's start, and nothing said who was doing what, so the work read as stuck and as
no team at all. The repository declares no lanes, so it could not hand its own rows out as Daoris sessions (SELFASK1), and a native
sub-agent is one tool card on the page although FABRIC-C1a binds it to its part by lineage.

Two things went wrong, and this design answers both:

1. **Seen.** The Fabric already records most of who works beside a session, but no reader puts those records in one place, and the
   one record that names native children is read by nothing but tests (§1.1).
2. **Routed.** The session did all of its roadmap in one turn of one agent because no door said there was another way, and the one
   door it would have met, a quest to its own repository, refuses without saying how (§1.9).

## 1. Today, read from the code

### 1.1 Native children: recorded, never read

- **The record.** FABRIC-C1a writes `<home>/sessions/<id>/lineage.jsonl` for a session the driver started (`Driver/Sessions/ChildLineage.cs:35`,
  `:52`; `Core/Sessions/LineageFile.cs:13-22`; D173's FABRIC-C1a note, `D173.md:104-112`). Four writers, each marked by `by`:
  - the connector, on every call, from the call's `_meta` (`Mcp/…/CallLineageFilter.cs:17-20`; fields `LineageFile.cs:72-92`);
  - Claude Code's hook on `PreToolUse` for the connector's tools, `SubagentStart` and `SubagentStop`
    (`Driver/Sessions/hooks/lineage-hook.mjs:7`, `:25`), handed in the session's settings (`Driver/Agents/Permissions/Permissions.cs:563-576`)
    on both the pipe and the protocol door: a chat hands it in `session/new`'s `_meta` (`Driver/Conversations/Chat/ChatRunner.cs:1354-1360`),
    a driven session at `Driver/Loop/Driver.cs:3085-3097`;
  - the driver reading Claude Code's stream on the pipe door, for each child's start, parent, depth and end
    (`Driver/Sessions/Domain/ChildBindings.cs:298-323`, hooked in at `Driver/Agents/Streams/HarnessCapture.cs:123`);
  - the driver at the part's end, clean or not (`ChildBindings.cs:326-327`; `ChatRunner.cs:2497`, `Driver.cs:3825`).
- **What it keeps.** Ids only: the hook keeps a value only where it is shaped as an id (`lineage-hook.mjs:10-12`, `:24`), so no path
  reaches the file. `ChildBindings.Fold` folds the lines into each child's harness, native id, type, parent, depth, provenance,
  connectivity, state (`active`, `finished`, `ended`, `lost`) and call count (`ChildBindings.cs:13-37`, `:64-73`, `:128`), and the
  part's root, end and counts (`:90-111`). The lines carry their moments (`at`), but the fold keeps no start or end time, and no line
  keeps what the child was asked to do: the stream's line drops the task's description (`:298-323`). Codex says no thread's end, so a
  Codex child ends with its part and is never `finished` (`D173.md:121-122`).
- **Nobody reads it.** `ChildLineage.Read` (`ChildLineage.cs:125-139`) is called from tests alone (`ChildLineageTests`,
  `LineageHookProcessTests`); no module route and no page code reads the lineage (searched `ChildBindings`, `ChildLineage`, `lineage`
  across `Modules/` and `Web/`). FABRIC-C1b, *a part's native children on its record*, is open and placed after C2a (`TASKS.md:177`).
- **What the page shows of a child comes from elsewhere**, and is gone when the window is:
  - *the protocol door* (every session on the install, D173's FABRIC-ACP1 note, `D173.md:5173-5180`): the adapter announces a child
    with its name, its task and its `subagentSessionId`, and its end with a state. The driver opens a console stream for it and keeps
    one tool card on the session's events, titled with the child's name, its task as the card's text, updated in place at the child's
    end (`Driver/Agents/Acp/AcpStreams.cs:152-203`). A child's own words and tools go to its stream, never the session's record or
    transcript (`:16-21`);
  - *the pipe door*: the `Task` call is a card titled `Agent: {description}` (`Driver/…/StructuredOutput.cs:461`), and the child's lines,
    tagged `parent_tool_use_id`, go to a stream named by the task's description (`Driver/Agents/Streams/ClaudeStreams.cs:73-80`, `:99-108`,
    `:137-149`);
  - the streams are held in memory only (`Driver/…/SessionOutput.cs:69`, `:92`, `:175`), served by `SESSION_STREAMS`
    (`Modules/Bridge/Routes/ConsoleRoutes.cs:44-55`) and drawn as the output panel's tabs, *subagent · live* or its state
    (`Web/bridge/sessions/console.ts:152-199`, `Web/work/console/streams.ts:23-44`), with no parent, depth or moments;
  - a conversation's blocks have no parent or nesting (`Web/work/conversation/conversation.ts:231-261`), so a child is one `ToolCard`
    (`Web/work/conversation/ToolCard.tsx:82`).

So a person can see that a child started and ended, one card at a time, in the conversation and the panel, and nothing else: not the
children together, not which one runs now, not one lost when its root was killed, and nothing at all once the window restarts.

### 1.2 Parts and co-workers

- **Parts** (D167): an entry's parts are the lead itself in turn, each a record `from` the last with its reason (`from_session`,
  `from_why`, written at `Core/Sessions/Sessions.cs:827`), shown in the head's facts as *continues…* (`Web/work/head/headFacts.ts:95-98`)
  and in the parts strip and flow (FLOWCHART1b, archived 2026-10-20).
- **Co-workers** (D203, accepted 2026-10-23): a record of this machine's whose `serves` names a part of another session's line,
  its role read as reviewer (`opinion`), partner (`serves` with `piece`) or helper (`serves` alone) (`D203.md` point 1; the design
  `docs/2026-10-23-coworker-design.md` §2.1).
  - A helper's ask is three files under `<home>/helpers`: the ask (`id`, `asker`, `question`, `agent`, `askedBy`, `urgent`, `at`), its
    state (`waiting`, `approved`, `started`, `answered`, `refused`, `cooling`) and its answer (`Core/Sessions/HelperBox.cs:33`, `:44`,
    `:73-83`, `:117-123`, `:185`). A lead waiting on its helper ends `completed` with `ended.awaits-helper` (`Driver/Sessions/Domain/Observation.cs:164-165`),
    a note with no values (`Web/…/noteLines.ts:50`), so which helper it waits on is a join through the box.
  - A partner's piece is a box under `<home>/data/partners` (`Driver/…/PartnerFiles.cs:6-10`, ask fields `:76-85`, state `:121-135`).
    COAGENT1a and 1b are built; `partner_ask`, the lead's own door, is not (COAGENT1d, `TASKS.md:400`).
  - The page shows a helper's open asks in the head's *Helpers* and a partner's *Pieces* (`Web/work/head/SessionHead.tsx:440-457`);
    answered and refused asks are filtered out (`Web/work/waits/HelperAsks.tsx:61-66`). None of COWORKUX1a-i is built: the key
    `work.helper.heading` still reads *Helpers* (`Web/locales/en/work.helper.json:9`), and no `work.team.*` key exists.
- **Not co-workers** (D203 point 1): a part, another repository's session, a meeting's participant, a native subagent, Ask Daoris,
  an intake and a brainstorm's pass.

### 1.3 Quests a line published, lanes, and waits

- **Lanes** (D115 §2.2, built by DEV4): `daoris.lanes.json` declares a repository's lanes; `connect` sends their words, never their
  globs, and the registration keeps them (`docs/2026-10-01-self-development-design.md:129-162`). A quest is addressed `repository:lane`;
  the exchange splits it once (`Core/…/QuestExchange.cs:528-531`), judges the lanes (`:707-743`) and keeps `Lanes` beside `To`
  (`Core/…/Quests.cs:117`). A repository may ask its own lane; to itself whole it is refused (`QuestExchange.cs:533-540`).
- **The driver plans by repository, never by lane.** Its only readers of lanes are the intake's room, the registration and the
  remote sync (`Driver/Quests/IntakeRoom.cs:133-138`, `Driver/Service/Domain/LineRegistration.cs:199`, `Driver/Service/RegistrationFollow.cs:155-166`,
  `Driver/Service/Domain/RemoteSyncPayloads.cs:169`). A lane's quest is started as any quest to its repository. Concurrent lanes and
  their cap (DEV6) and the steward that splits a whole-repository quest (DEV9) are open (`TASKS.md:513`, `:516`).
- **The links.** A quest names the session whose connector published it, `PublishedBy`, null for the person's and for a chain's step
  (`Quests.cs:104-109`); a session names the quest it serves, `Session.Quest` (`Sessions.cs:103`); no session record names a lane (no
  `Lane` in `Sessions.cs`). A taken quest waiting on a question it published names it in `Awaits` (`Quests.cs:96-102`), set by
  `quest_respond` `wait` (`Mcp/…/QuestTools.cs:372-374`, `:391-392`).
- **Shown:** a quest's lanes on its card and page (`Web/quests/QuestList.tsx:214-218`, `Web/quests/QuestPage.tsx:656`); its wait on the
  quest page and list (`QuestPage.tsx:665-682`, `Web/quests/QuestsView.tsx:563-567`) and on the rail as *awaits #q of repo*
  (`Web/work/rail/SessionRow.tsx:329-332`); *Who it worked with* (`Web/work/…/SessionRelations.tsx:105-111`). **Not shown:** a lane
  anywhere under `Web/work/`, in an ask's work (`Web/asks/AskWork.tsx`) or on the chain strip.

### 1.4 The ask's channel

The channel is a table of messages (`Core/Asks/Channel.cs:243-258`), each with its ask, moment, posting session (null for the
person), repository, agent, entry, addressee, reply, question, text and urgency (`:87-104`), and Daoris's own lines read from the
quests, never stored (`:116`). It is drawn on the ask's page (`Web/asks/AskPage.tsx:533-545`) and in a session (`ChannelPanel`,
`Web/work/AttendedSession.tsx:323-324`), each message by *repository · agent* or *You*, its moment, a question or urgent pill and
its receipts (`Web/asks/ChannelSection.tsx:32-34`, `:288-291`). A message's session and entry are carried and not drawn.

### 1.5 Brainstorms and meetings

A brainstorm is `<home>/brainstorms/<id>/`, its request `(Id, Question, Repository, Agents, At)` and its state
(`Driver/…/BrainstormFiles.cs:8`, `:13`, `:35-40`); a meeting is `<home>/brainstorms/meetings/<id>/`, its request the same shape and
its state with a round (`Driver/…/MeetingFiles.cs:9`, `:15`). **Neither request names the session, ask or quest that started it.**
Neither has a place on the page: *no place in the frame, no state on any screen* (`docs/2026-10-20-ui-ux-design.md:110`); their
page is MEETUX1a-d's (`TASKS.md:81-84`), and a caller from a session is BRAINSTORM1d's (`TASKS.md:168`) and AGENTROOM1b's (`:162`).

### 1.6 Work units, the running view and the flow

- **Units** (FABRIC-UNIT1a): kinds `branch`, `quest`, `ask`, `run` (`Driver/Trees/Inventory/WorkUnit.cs:4-20`), each with an owner,
  subject, end and links `produced-by`, `produced`, `awaits`, `follows` (`:53-73`, `:129`, `:140-152`). `WorkUnits.ReadAsync`
  (`Driver/…/WorkUnits.cs:54`) is read only for node evaluation (`Driver/Loop/DriverWatch.cs:900`). FABRIC-UNITDOOR1, the door that
  would answer every unit (`WORK_UNITS`, `work units`), is open (`TASKS.md:152`; no such route or verb under `src/`).
  `WORK_STATE` answers branch milestones, not units (`Modules/Bridge/Routes/LinesRoutes.cs:558-612`), pushed as slice `work`.
- **The running view.** `Flow` is built (`Web/design/Flow.tsx:147`) and draws a quest, a session's line and states, a landing and a
  workflow's run (FLOWCHART1a1-1d, archived; `Web/workflow/WorkflowChart.tsx:363`). FABRIC-GRAPHVIEW1, each node's §E2 state, each
  wire's condition and what waits on what, is open (`TASKS.md:144`) and is drawn in the side bar's *Workflow* view
  (`docs/2026-10-20-flowchart-design.md` §2.5, §10). That design rejects *a second running view beside the Workflow view* and *a path
  inferred from a conversation, or a state the record does not keep* (§8).
- **No fabric event names a child.** The chain's events are wakes naming a subject (`Driver/Platform/Chain.cs:5-59`); the session's
  event log has user, message, thought, tool, plan, usage, turn, note, transient, raw and receipt (`Driver/Sessions/Domain/SessionEvents.cs:13-54`);
  the design's typed envelope is not built (`docs/2026-10-16-fabric-events-design.md:1204-1213`).

### 1.7 The session's record and page during one long turn

- **`updated` stays at the turn's start.** It is *when the record last moved* (`Driver/Trees/SessionGroups.cs:36`), written at the
  record's creation and at each ledger move (`Core/Sessions/Sessions.cs:827-828`, `:890-912`), and a note kept on a said word
  (`:1562`); every other write leaves it (`:1413-1797`). The ledger has no move from `working` to `working`
  (`Core/…/SessionLedger.cs:2187-2189`), a chat moves to `working` once when its process starts and again only at its end
  (`ChatRunner.cs:2423`, `:3103`), and tool calls go to the event log, never the record (`AcpStreams.cs:173`). So an hour of work in
  one turn, children included, leaves `updated` where the turn began. Its readers rely on that meaning: the head's *ran* and
  *waiting* facts (`headFacts.ts:169`, `:175`, `:207`).
- **The head** (`Web/work/head/SessionPageHead.tsx:14-39`): the state, the title, a facts line (`headFacts.ts:143-209`: repository,
  the leading agent, *continues*/*helps*/partner links, then by state its age, attempt, retrying, the first answer's wait, the running
  call, a heavy run, quiet), the primary act, *Stop…* and ⋯. The running call is the `calls` slice's, pushed as a call starts and ends
  (`Modules/Loop/CallsStateSlice.cs:9-27`); the watcher's *quiet* is its second partition (`:37-40`).
- **The side bar's views** are notifications, timeline, review, workflow, ask, console and terminal (`Web/work/frame/placements.ts:9`,
  `:22`), on the right but the console and terminal (`:28-30`). None says who works beside the session.
- **`chat.moved-from`** is the new conversation's first note (`Driver/…/HandOver.cs:266-271`, `ChatRunner.cs:2125-2127`), drawn by the
  note table (`Web/…/noteLines.ts:295`), with the record's `from_session` and `from_why` as *continues…* (`headFacts.ts:95-98`).
- **The ask's page** lists its quests (`AskWork`, each with its state, route and sessions, `Web/asks/AskWork.tsx:23-38`), its lines
  of work (`AskLines`, `AskPage.tsx:526-529`) and its channel, in separate sections.

### 1.8 What reaches the page unasked

The service's change stream has five families, `sessions`, `quests`, `asks`, `channel` and `words` (`Core/Platform/ChangeStream.cs:19-51`),
which the driver hears in one wait (`Driver/Service/LedgerChanges.cs:26`). The modules push fourteen slices (`Modules/Loop/*StateSlice.cs`;
`Modules/Platform/StateSlices.cs:82-85`; `.claude/knowledge/pushed-slices.md`). Under NOBLOCK1 a computed read is marked `[ByEvent]`:
it answers at once from what is held (`Modules/Bridge/HeldAnswers.cs`), its work runs on a task of its own, and its answer, where it
moved, is told as `DAORIS.ANSWERED` naming the page's query (`Modules/Bridge/DriverModule.cs:464-473`, `Web/bridge/state/held.ts`;
D56's NOBLOCK1 note, `D56.md:177-190`); `WindowCensusTests` refuses a computed route that is neither. **No family, slice or held read
carries the lineage or a native child.**

### 1.9 How work is handed out today

| Way | Who starts it | Where it runs | What the record says | Seen |
|---|---|---|---|---|
| a lane's quest (D115) | a session's `quest_publish` to `repository:lane`; the person's composer offers no lane (DEV4's *left out*) | its own session, tree and turn, landed on its own | the quest's `Lanes`, `PublishedBy`; the taker's `Session.Quest` | the quest's page; nowhere in the publisher's session |
| a helper (D163) | the lead's `helper_ask` under `helpers`, or the person | its own one-turn record reading a copy | the helper box, `serves` | open asks in the head |
| a partner (D199) | the person only, until COAGENT1d | its own clone, landed by the lead's merge | the partner box, `serves`, `piece` | *Pieces* |
| a reviewer (D155) | a workflow's occasion, or the person | its own record, reading | `opinion` | the gate |
| a native child | the lead's agent, by its harness's own tool | inside the lead's process, tree, account and turn | the lineage (§1.1) | one card and a panel tab |
| another repository | `quest_publish` to it | its own repository's session | the quest | *Who it worked with*, the quest |

**A repository asking itself** is refused with *That is the repository you are in — a quest is work for someone else. Use its own
backlog.* (`QuestExchange.cs:535-540`), whether or not it declares lanes, and nothing in that sentence says that a lane is how a
repository asks itself. SELFASK1 (`TASKS.md:511`) is filed to say it. A lane named on a repository with none is refused naming the
repository (`QuestExchange.cs:713-716`).

### 1.10 The case replayed

The kit's chat ran on the protocol door. Each child it started became one card titled with the child's name and one panel tab
(§1.1); the lineage file under its session recorded each child's start and stop through the hook, and nothing read it. The chat's
record moved once, to `working`, at the turn's start (§1.7). The repository declares no lanes, so had the chat tried to hand a row to
a session of its own it would have been refused with a sentence that names no way (§1.9). Every fact the person needed, *which child
runs, which ended, since when, how many are left*, existed only as cards scrolled past, or did not exist.

## 2. The rule

1. **The team is a read, never a store** (D173's first guard; D203 point 3). Nothing records a team or a structure; the read composes
   the records that already link a part of the work to the session: `from`, `serves`, `opinion`, `piece`, the lineage, `PublishedBy`,
   `Lanes`, `Session.Quest`, `Awaits`, the helper and partner boxes, the channel.
2. **From the records, never the conversation's text.** No member, state, wait or moment is parsed from what an agent said. What a
   member is on is the record's field: a quest's title, a helper's question, a piece, a lane's title, and for a native child the
   harness's own announcement of it (§3.6), each drawn as the agent's words, never Daoris's (D137).
3. **Partial visibility is said, not filled** (ADR-B, canary 9). A child whose parent no harness said sits under the part with *parent
   not said*; a Codex child is *ended with its part*, never *finished*; a child of a killed root is *lost*. A member the read cannot
   place is not drawn as anything else.
4. **No model** (D24). Every word in the view is Daoris's fixed words or a record's as written.
5. **D203's co-worker stands.** A native child, a lane's session and another repository's session are not co-workers and gain no
   `serves`. The view shows them beside the co-workers, each under its own heading, because the person asked *who is doing what*, and
   the answer includes them; their rules are their own.

## 3. The team read

### 3.1 Who is on a session's team

A session's team is read for its line (D167's entry: every part, through `from`). For an ask, it is the union of its lines' teams
and its quests (§3.5).

| Group (en / zh) | Members | Read from | Who | On what | State | Since |
|---|---|---|---|---|---|---|
| **This session** / 本会话 | the lead: the line's live part; earlier parts folded | the entry's parts | agent, account's mark | its quest's title, or the chat's | the record's | the newest of its events and its children's lines (§3.7) |
| **Inside this turn** / 本轮之内 | native children, as a tree by `Parent` and `Depth` | the lineage fold, with §3.6's label and moments | harness, the child's type | the harness's label for it, the agent's words | `active` *working*, `finished`, `ended with its part`, `lost` | its start; its end |
| **Co-workers** / 同事 | helpers, partners, reviewers | `serves`, `piece`, `opinion`; the boxes | agent | the question; the piece; the candidate | the box's or the record's | the box's `at` |
| **Asked through quests** / 通过委托请求 | each quest any part of the line published: to its own lanes and to other repositories | quests with `PublishedBy` in the line; each one's taker by `Session.Quest` | the taking session's agent, or *not taken yet* | the quest's title; its lanes | the quest's status, and its session's state where taken | the quest's or the session's moves |
| **Asked by** / 由谁请求 | the quest this session serves, its publisher and its ask | `Session.Quest`, the quest's `PublishedBy` and `From` | the publishing session, the person, or the ask | the quest | — | — |
| **Gathered** / 召集 | a brainstorm's voices and a meeting's participants, once their requests name who started them (§3.6, row g) | the request and its state | each agent | the question | the state's | the request's `at` |

Each member row says **how it got there** (§3.4), and opens what it is: a session's page, a quest's page, a co-worker's thread
(D203 §6.4), or, for a native child, its console tab while the window holds it and its lineage facts after.

### 3.2 What waits on what

Waits are edges, each from a record, never inferred from moments:

| Edge | Read from |
|---|---|
| the lead waits on its helper | `ended.awaits-helper` joined to the open ask in the helper box whose `asker` is a part of the line |
| a taken quest waits on a question it published | `Quest.Awaits` |
| a lane's session waits on the person, on a helper, on its own question | that session's own state and its `Awaits`, the same edges one level down |
| a quest waits to be taken | its status `open` with no taking session |
| a child runs under its parent | the lineage's `Parent`; an edge *inside*, not a wait |
| the lead's turn holds its children | a child `active` while the lead is `working`: the turn has not ended because they run |

The read answers the edges and the view draws them as words on the waiting row (*waits on Codex · helper*, *waits on #a1b2 in
driver*) and once at the top, the session's own wait first: **Waiting on** / 在等 *the driver lane's session (#a1b2)*. A session that
published quests and waits on none says so (*published 3, waits on none*), which is the truth today for a chat, which takes no quest
and so has no `Awaits` (§6.3).

### 3.3 The channel's last words

The team read carries the ask's newest three channel messages, each with its moment, its author as the channel draws it, and, new on
the page though already on the record, the author's session where one posted it (`Channel.cs:87-104`), so a message is placed on the
member that said it. *All messages* opens the ask's channel. A session with no ask has no channel, and the block is not drawn.

### 3.4 The routing that put each member there

Each member's row says the door that started it, read from its record:

| Member | Its routing, as the record says |
|---|---|
| a lane's session | *published to {lane} by this session* (`PublishedBy`, `Lanes`); *by you* where the person published |
| another repository's session | *asked of {repository} by this session* |
| a helper | *asked by this session* or *asked by you* (`askedBy`), under the rule `helpers` |
| a partner | *asked by you* (and by the lead once COAGENT1d lands) |
| a reviewer | *the workflow's second opinion* or *asked by you* (the opinion's occasion) |
| a native child | *started by {agent} inside its turn*: the harness's own door, which Daoris neither starts nor confines (C2a held) |
| an earlier part | *handed on* with its `from_why`, as the parts strip says it |
| a brainstorm or meeting | *called by this session* or *by you*, once the request names it |

### 3.5 An ask's team

An ask's page draws the same component over the ask: its quests (each with its lanes and its taking session), each line of work on
it with its own children and co-workers folded to a count that opens the line's own view, and the channel's last words. It replaces
nothing on the ask's page: `AskWork` and `AskLines` keep their presses; the team section sits above them and links into them.

### 3.6 What the records gain

Only two stored changes, both additive, both desktop-only:

1. **The lineage keeps a child's label and its moments.**
   - *The protocol door*: the driver appends a line `by: "acp"` at the adapter's announcement and its end, with the
     `subagentSessionId`, the announced name and task, bounded (the name to 120 characters, the task's first line to 200), and the end's
     state. It already holds all three at `AcpStreams.cs:152-203`; today it writes them to one card and a memory stream.
   - *The pipe door*: the stream's line keeps the task's `description`, bounded the same way, which `ChildBindings.StreamLine` drops today.
   - *The hook* stays ids only. A label is not an id, and the hook's rule (`lineage-hook.mjs:10-12`) is that no value a path may hide in
     reaches the file through it.
   - *The fold* keeps each child's first and last `at`, and its label where a line gave one.
   - **Why a label may enter a file the rule kept to ids**: the file is the session's, under the home, never synced and never on a
     shared host (D47); the label is already on the session's events as the card's title (`AcpStreams.cs:172-180`), so it moves no new
     text to a new reader. It is the harness's announcement, a structured field of its wire, not words parsed from the conversation.
   - **The join to measure first.** Whether the protocol door's `subagentSessionId` is the hook's `agent_id` is not known: the hook's
     input names `agent_id` and `agent_type` (`lineage-hook.mjs:38-39`), the adapter names `subagentSessionId` (`AcpStreams.cs:155`).
     Row a reads the install's lineage files and a stand-in's for both and joins them where equal; where not, the fold keeps each
     door's child apart and the view says *seen by the harness* and *seen by its hook* as one child only where an id joins them. No
     join by time or by order.
2. **A brainstorm's and a meeting's request name who called it**: `session` and `ask`, null where the person did, additive, written by
   the door that makes the request. Row g, after BRAINSTORM1d and AGENTROOM1b give a session a door to call one.

Nothing else: no team record, no structure, no lane on the session record (a session's lane is its quest's), no stored wait.

### 3.7 The record's `updated`, and *since*

`updated` keeps its meaning, *when the record last moved*. Advancing it on every event would make *ran* and *waiting* wrong
(`headFacts.ts:169`, `:175`, `:207`) and write the service's store at the rate of a turn's events. Instead the team read gives each
working member a **last heard**: the newest `at` among its own events and its children's lineage lines. The head's facts line for a
working session gains one fragment from it where the session has children or members working: *3 working with it · last heard 1 min
ago* / *3 个在一起工作 · 1 分钟前有动静*, which opens the view. The watcher's *quiet* (D200) is not changed here; whether a lead whose
children work for long is read as quiet is §9's unmeasured.

### 3.8 The data types

The read answers one type, fed whole to a presentational component (`Web/design/presentational.test.ts` covers `work/` and `asks/`):

```ts
type Team = {
  of: { session: string } | { ask: string };
  waitingOn: TeamEdge[];                 // the session's own waits, §3.2
  groups: TeamGroup[];                   // in §3.1's order, an empty group absent
  channel: TeamChannelLine[];            // at most three, newest last; absent with no ask
  hint?: 'no-lanes';                     // §6.4
  at: string;                            // when the read was composed
};
type TeamGroup = { kind: 'lead' | 'inside' | 'coworkers' | 'quests' | 'askedBy' | 'gathered'; members: TeamMember[]; folded?: number };
type TeamMember = {
  key: string;                           // the record's own id: a session, a quest, a box, a child's native id
  kind: 'part' | 'child' | 'helper' | 'partner' | 'reviewer' | 'lane' | 'repository' | 'asker' | 'voice';
  agent?: string; mark?: string;         // the maker's mark, never a hue alone
  on?: { text: string; claim: boolean }; // claim: the agent's words (a child's label), drawn as such
  state: string;                         // the record's own state word
  since?: string; ended?: string; heard?: string;
  routedBy: { door: string; by: 'session' | 'person' | 'harness' | 'workflow'; detail?: string };
  waitsOn?: TeamEdge[];
  depth?: number; parentKey?: string;    // a child's place in the tree; absent where no harness said
  opens: { session?: string; quest?: string; stream?: string };
};
type TeamEdge = { to: string; kind: 'helper' | 'question' | 'person' | 'untaken' | 'partner'; label: string };
type TeamChannelLine = { at: string; author: string; session?: string; text: string; urgent?: boolean; question?: boolean };
```

## 4. How it updates without the window asking (NOBLOCK1)

- **One read, `SESSION_TEAM`, marked `[ByEvent]`**, keyed by its payload (a session or an ask), as NOBLOCK1 settles for reads asked per
  payload rather than one owner's snapshot (`D56.md:186-189`). It answers at once from the held team, `{pending: true}` while none is
  held, and composes on a task of its own; when the composed team differs from the held one it is told as `DAORIS.ANSWERED`, and the
  page's query is written by the state hub. `WindowCensusTests` gains the route as told by event.
- **Its wakes**, each an existing source except the first:
  - the attended session's and its line's `lineage.jsonl`, heard by a `HomePart.File` per attended line (`Driver/Platform/HomeWatch.cs:36-45`),
    added while a team read is held and dropped with it, so the watch is bounded by what the window shows;
  - the change stream's `sessions`, `quests`, `asks` and `channel` families (`ChangeStream.cs:19-51`);
  - the helper and partner boxes' folders, by a new `HomePart.Records` part beside the home watch's own: today it hears the
    configuration, the lines, the words, the accounts, the opinion gates, the workflows, Ask Daoris's proposals and the notices
    (`HomeWatch.cs:200-252`), and no box (searched `HomePart.` across `Driver/` and `Modules/`);
  - the `calls` slice's change for the line's sessions (`CallsStateSlice.cs:23-24`), for *last heard*.
- **Coalesced**: wakes within one judgement compose once. A turn with many children writes many lines; the team is composed at most
  once per wake burst, and told only where it moved.
- **The clock is the page's**: *for 4 min* and *1 min ago* are drawn from `since` and `heard` by the page's clock, never re-sent.
- **Off the window's thread**: the lineage read is a file read of one session's lines, folded in memory; a line's quests are read from
  what the driver holds, never a service call per member.

## 5. The view

### 5.1 Where it is

- **In a session**: a side bar view **Team at work** / **分工**, `team` in `ViewId` and `VIEW_IDS` after *workflow*
  (`placements.ts:9`, `:22`), on the right by default, movable to the panel as every view is (D118's frame). The head's new fragment
  (§3.7) and the waiting row's *waits on* open it.
- **In an ask**: the same component as a section of the ask's page above *Quests*, titled the same.
- **D203's *Co-workers* section in the head stays** the place of the presses on a co-worker (*Let it ask*, *Not now*, *Stop…*), with
  `data-wait-section` (`.claude/knowledge/person-waits.md`). The view shows co-workers as rows that open their threads and carries no
  second set of those presses.
- **The graph's half is FABRIC-GRAPHVIEW1's running view**, in the *Workflow* view: a session's run's nodes are not redrawn here, and
  the flowchart design's rejection of a second running view stands. The team view links to it where the session has a run (*Its
  workflow →*).
- **Help**: Ask Daoris's room learns the view's name in its prose list of views (`placements.ts`'s remark; `Help.cs`'s *The window*).

### 5.2 What it draws

From top to bottom, each block absent where it has nothing to say:

1. **Waiting on** (§3.2), one line, the session's own waits; *nothing* is not drawn.
2. **This session**: the lead's row: mark · agent · *working* · *last heard 1 min ago* · its quest or *chat*.
3. **Inside this turn**: one row per child as a tree, indented by depth, each *type · label · state · for 6 min* or *ended 14:02*;
   finished and ended children fold behind *{{count}} ended* past the newest three; an active or lost child never folds. A note under
   the heading says what the person is looking at: *Sub-agents run inside this session's own turn, on its account and in its tree.*
4. **Co-workers**: D203's rows, read-only here.
5. **Asked through quests**: one row per quest: *#a1b2 · driver lane · {title}*, then its session (*Claude Code · working · 12 min*,
   or *not taken yet*), then its own *waits on*, then its routing in the faint line.
6. **Asked by**: one row.
7. **Gathered**: once row g lands.
8. **On the channel**: three lines, *All messages* →.
9. **The no-lanes hint** (§6.4), where it holds.

Every state word is a status from the glossary's kinds, with the status palette and a word or icon beside every hue, never a hue alone
(D41). Every row is one button reaching the member, in the order drawn, so the keyboard and a reader meet them as the eye does.

### 5.3 At each width, theme and language

The side bar is 45% of the window, never under 300 px, and the whole frame under 768 px (`Web/work/frame/layout.ts:44`); a region lays
out by its own width (`docs/2026-09-19-platform-ux.md:304-311`).

| Window | The view's width | How it lays out |
|---|---|---|
| **1546 px** (the install's) | about 696 px (45%) | the wide row: mark, who, on what, state and since on one line; routing on a second faint line; the channel's lines whole to two lines |
| **888 px** | about 400 px | the narrow row: who and state on the first line, on what on the second (two lines, then *…* with the whole in the tip), routing behind the row's tip; the tree's indent capped at two steps, deeper children said by *depth 3* |
| **680 px** | the whole frame (under 768 px) | the narrow row at the frame's width; the side bar's view replaces the conversation while open, its close returning to it, as every view does there |

- **Themes**: tokens only, both themes' status colours, checked by the tokens gate; a lost child in `--warn`, never red alone.
- **Languages**: every label in both catalogues; zh lines are shorter by count but wider per glyph, so the narrow row wraps on the
  measured box, never by character count (FLOWCHART1e's measured-box check is the gate's precedent).
- **Stories** at `MAIN_WIDTHS`' wide, half and narrow (`Web/design/storyWidths.tsx:13`), both themes, en and zh, per state: a chat with
  three children (one active, two ended), a Codex child ended with its part, a lost child, a session with three lane quests (one
  untaken, one working, one waiting on a question), a lead waiting on a helper, an ask with two lines, the no-lanes hint, and the
  empty team (not drawn: the view says *Nobody else is working on this* / *没有其他成员参与*).

### 5.4 Words

Keys under `work.cowork.*` (D203 §8.3 reserves `work.team.*` for its *Co-workers* section), each a label with its kind's budget
(D116 §3), checked by `names:check -- --strict` and `i18n:check`:

| Key | en | zh |
|---|---|---|
| `view` | Team at work | 分工 |
| `group.lead` | This session | 本会话 |
| `group.inside` | Inside this turn | 本轮之内 |
| `group.coworkers` | Co-workers | 同事 |
| `group.quests` | Asked through quests | 通过委托请求 |
| `group.askedBy` | Asked by | 由谁请求 |
| `group.gathered` | Gathered | 召集 |
| `group.channel` | On the channel | 频道上 |
| `waitingOn` | Waiting on {{what}} | 在等{{what}} |
| `insideNote` | Sub-agents run inside this session's own turn, on its account and in its tree. | 子智能体在本会话自己的这一轮里运行，用它的账号，在它的工作树中。 |
| `child.ended` | ended with its part | 随所属部分结束 |
| `child.lost` | lost when its session stopped | 会话停止时丢失 |
| `child.noParent` | parent not said | 未说明上级 |
| `quest.untaken` | not taken yet | 尚未接手 |
| `routed.lane` | published to the {{lane}} lane by {{by}} | 由{{by}}发布到{{lane}}泳道 |
| `routed.repository` | asked of {{repository}} by {{by}} | 由{{by}}向{{repository}}提出 |
| `routed.harness` | started by {{agent}} inside its turn | 由{{agent}}在它的这一轮里启动 |
| `heard` | last heard {{ago}} | {{ago}}有动静 |
| `withIt` | {{count}} working with it | {{count}} 个在一起工作 |
| `published` | published {{count}}, waits on none | 已发布 {{count}} 个，未等待 |
| `empty` | Nobody else is working on this | 没有其他成员参与 |
| `hint.noLanes` | This repository declares no lanes, so its work runs inside this conversation. A `daoris.lanes.json` lets it hand rows to sessions of their own. | 这个仓库没有声明泳道，所以它的工作都在这个对话里进行。有了 `daoris.lanes.json`，它就能把条目交给各自的会话。 |

The glossary's words are kept: 委托 for quest, 需求 for ask (*the ask's channel* is 需求的频道), 重试 for try again, 会话 session, 泳道
lane, 频道 channel, 子智能体 subagent, 助手 helper, 搭档 partner, 阅读者 reviewer, 同事 co-worker, 主导 lead. *Team* / 团队 stays D203's
word for the structure in prose and is not the view's title, since the window's *team* already names the people sharing a remote
(D203 §0.2). New glossary terms: *team at work* / 分工 (a view), *inside this turn* / 本轮之内.

## 6. The routing half

### 6.1 The ways, and what each is for

| Way | Use it for | It costs | Its bound |
|---|---|---|---|
| a lane's quest | a row of the roadmap that can be built, checked and landed on its own: a separate session, tree, turn and landing | one session's turn on an account; a tree | D115; DEV6's cap once built; the person's account rules |
| a helper | a question about the work in progress, answered once | one turn, reading | `helpers`; two at once; D163 |
| a partner | a piece of code written apart, taken in by the lead's merge | one turn in a clone | `partners`; D199 |
| a native child | a short search or read inside the turn the lead needs before it goes on | the lead's own account and turn | the harness's; Daoris confines it only after C2a |
| another repository | work that repository owns | its session | D45: never write across |

**The difference that matters to the person:** a lane's session is a record Daoris starts, shows, stops, lands and resumes; a native
child is inside one turn that a limit, a crash or a stop ends whole, with no record of its own. A roadmap of rows, each of which lands
on its own, belongs in lanes; a turn that runs a roadmap through children is one turn's risk for every row.

### 6.2 How an agent is told (the doctrine)

The co-working skill (`canon/core/skills/co-working/SKILL.md`, synced to `.claude/skills/co-working/SKILL.md`) gains a section, project-agnostic, under
the canon-authoring rules: *handing work out*. It says the table above in the agent's words: rows that land on their own go to lanes
as quests (`quest_publish` to `repository:lane`), one quest per row with its proof; a question goes to a helper; a piece to a partner
where the door exists; children for reads inside the turn; and a session that published quests says so and ends its turn, rather than
building them itself. It says what the session reads of them afterwards (`quest_list`). It decides nothing: the agent chooses within
the person's rules, and Daoris never splits a session's work itself (D193's rejection of agents directing agents; D24).

### 6.3 A chat

A chat takes no quest, so it has no `Awaits` and cannot `wait` on what it published (`QuestTools.cs:372-374` waits a taken quest). It
can publish to its own repository's lanes, and `PublishedBy` names it, so its team view shows each quest and its session (§3.1). What
it does not get today is a wake when a quest it published closes. Whether a chat should be told its quests' ends, as a channel line
or as its next turn, is left to the readers: a chat is the person's conversation, and a turn it did not ask for spends their account
(D188 point 2's *a post never starts a run* is the precedent against). This design shows the ends in the view and starts nothing.

### 6.4 A repository with no lanes (SELFASK1)

Three places say how, each Daoris's fixed words:

1. **The refusal** (SELFASK1's row, unchanged here): where lanes are declared, it names them by their addresses; where none are, it
   says that a `daoris.lanes.json` with a steward lane is what lets the repository hand its own work out.
2. **The registry's row** for a repository with no lanes says the same (SELFASK1).
3. **The view's hint** (§5.4 `hint.noLanes`): drawn on a session's view where its line has started at least two native children in
   one turn and its repository declares no lanes, so it is said where the case happened and nowhere else. It links to the guide's
   lanes section. It is a fact about the repository, never a judgement of the agent's choice.

### 6.5 What is the person's

- **Whether a repository has lanes.** Writing `daoris.lanes.json` is a change to that repository, made by its own sessions or its
  owner, through its own landing (D45); Daoris never writes one.
- **How many run at once** (DEV6's cap, once built) and **which accounts they spend** (D197's walk, the actors of D205).
- **`helpers` and `partners`**, the rules already in Settings; a native child's tool, the person's permission rules (D72), which
  can deny the harness's agent tool as any other.
- **Accepting each lane's work**, by its own *Accept…*; nothing here lands anything.
- Not the person's to press each time: the agent's choice within those rules (§6.2), as D203 §3.3 has it for co-workers.

## 7. The rows, in build order

Each row is one feature a person can see working (D187), en and zh wherever words are drawn, filed in `TASKS.md` when this design is
accepted; until then COWORKVIEW1 points here.

1. **COWORKVIEW1a — a part's native children read, with their labels and moments** (driver; replaces FABRIC-C1b's read half and
   lifts its *after C2a*, since showing restricts nothing). §3.6 point 1: the protocol door's `acp` lines, the pipe's description, the
   fold's moments and label; the join measured on the install's lineage files and a stand-in of each door before the fold joins.
   *Proof:* `ChildBindingsTests` (a label and moments folded; a Codex child `ended`, never `finished`; a killed root's child `lost`;
   no join by time), a new `AcpStreams` case in the driver's tests (the announcement and the end appended, bounded; no such class exists yet), `ChildLineageTests`; the join's counts in
   D173's note. *Trap:* joining an ACP child to a hook line by order or time.
2. **COWORKVIEW1b — the team read and its doors** (driver, modules; after a). `SESSION_TEAM` `[ByEvent]`, `daoris-driver sessions
   team` (D203's verb, widened to every group of §3.1), §3.1-§3.4 and §4's wakes. Where COWORKUX1a has landed its line's exchange
   read, the co-workers group is that read; where it has not, the first of the two to build writes the one reader the other uses.
   *Proof:* the read's tests per group and edge; `WindowCensusTests` (told by event); `PipeCensusTests`; the verb's words tests;
   timed under a slow stand-in, answering within the window's bound while the event delivers.
3. **COWORKVIEW1c — the view in a session: a chat's children seen** (web-shell; after b). §5: `TeamView` and its rows as
   presentational components fed by `Team`, the side bar's `team` view, the head's fragment, the stories of §5.3, the words of §5.4.
   *Proof (the person's first):* **a chat working through native children shows each child, its state and its end**: a stand-in chat
   on the protocol door starts three children, one ends, one is killed with its root; the view shows three rows, *working*, *ended
   14:02*, *lost*, in en and zh, at the three widths and both themes; `presentational.test.ts`, `i18n:check`, `names:check -- --strict`,
   `HelpCoverageTests` (no new press); seen on the install's window at 1546, 888 and 680 px.
4. **COWORKVIEW1d — lanes' sessions and their waits** (driver's read, web-shell; after c). The *Asked through quests* group with its
   takers and edges, the *Waiting on* line, *Asked by*.
   *Proof (the person's second):* **a session that published quests to its lanes shows each lane's session and what it waits on**:
   an example repository with two lanes (family phase 4b's engine, `core` and `media`), a session publishing to each; one taken and
   waiting on a question, one untaken; the view shows both, each lane's session, *waits on #q of repo* and *not taken yet*, en and zh.
5. **COWORKVIEW1e — an ask's team** (web-shell; after d). §3.5 on the ask's page. *Proof:* an ask with two lines and a channel; the
   section above *Quests*, its rows opening each line; stories.
6. **COWORKVIEW1f — the routing told** (canon, web-shell; with or after SELFASK1). §6.2's skill section (canon, then this repository
   and both examples synced) and §6.4's hint. *Proof:* `daoris check` and the canon's tests; the hint's story and its rule's tests
   (drawn at two children and no lanes, not drawn with lanes or one child).
7. **COWORKVIEW1g — brainstorms and meetings on their caller's team** (driver, web-shell; after BRAINSTORM1d and AGENTROOM1b).
   §3.6 point 2. *Proof:* the request's fields written by each door, the *Gathered* group's rows.

SELFASK1 (`TASKS.md:511`) is a row of its own already and is unchanged. FABRIC-C1b's display half (*a session's console or record
lists each child*) is met by rows a to c; its row is rewritten to point here when this is accepted.

## 8. Rejected

- **A team or structure record**, or a lane on the session record: two stores for one fact (D173's guard, D203 §3.4).
- **Who does what read from the conversation**: an agent's prose, a plan's text, a tool card's words parsed. A record's field or nothing.
- **A model's summary of the team** (D24).
- **`updated` advanced on every event**: it would break *ran* and *waiting* and write the store at a turn's rate (§3.7).
- **Native children as co-workers**: no record, no confinement, nothing Daoris can stop by itself (D203 point 1 stands).
- **The child's prompt kept whole**, or the hook writing labels: a bounded label from the harness's announcement only, and the hook ids only (§3.6).
- **A second running view or a free graph of the team**: the running view is GRAPHVIEW1's in *Workflow*; the team is a list, which reads
  at every width (the flowchart design §8).
- **Daoris splitting a chat's roadmap into quests by itself**, or a coordinator agent dispatching the team: D193's rejection, D203 §3.4.
- **A poll** of the lineage or the boxes from the page: NOBLOCK1's rule.
- **The hint shown everywhere a repository has no lanes**: most chats need none; it is said where children ran a roadmap (§6.4).

## 9. Not built, and unmeasured

- **Unmeasured:** whether `subagentSessionId` equals the hook's `agent_id` (row a's first step); whether the watcher (D200) reads a
  lead whose children work for long as quiet, which this design did not find in the code and leaves to row b's tests; the view's
  budgets in zh at 400 px; how many lineage lines a long turn writes, and so the fold's cost per wake.
- **Not designed here:** a publishing chat told its quests' ends (§6.3, left to the readers); DEV6's cap and DEV9's steward, which make
  lanes run side by side and split a whole-repository quest; a child's own transcript after the window restarts (the console streams
  stay in memory).

## 10. What it amends, once accepted

- **D173**: the FABRIC-C1a note's *ids only* is kept for the hook and the connector; the driver's lines gain a bounded label and the
  fold its moments (§3.6); FABRIC-C1b's read and display are COWORKVIEW1a-c, no longer after C2a; GRAPHVIEW1 stays the graph's half.
- **D203**: point 1 stands; the team the person sees widens past co-workers to every member §3.1 names, each kept apart by its group;
  `sessions team` answers every group.
- **D115**: §2.2's lanes are shown on the publishing session's team, and a repository with none is told how (§6.4, with SELFASK1).
- **D176, D177**: a request names who called it (row g).
- **D118's frame**: a side bar view, `team`, after *Workflow*.
- **These stand:** D24, D45, D47, D56 (NOBLOCK1), D116, D137, D167, D186 and the flowchart design, D188, D193.
