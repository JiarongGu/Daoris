# Conversations and context — how it works now

Read from main at `ea73c6439` (2026-11-04). Paths without a root are under `src/Daoris.Desktop/Daoris.Desktop.Driver/`.

## What it does

A conversation is a chat with an agent in a repository, or with Ask Daoris in its own room. A driven session runs a quest.
Both are lines of work: one entry on the window, its records its parts. When a part ends, or its account runs out, or the
person or the agent asks to move, one pure decision says how the line goes on. It may resume the agent's own conversation,
open a new part on another account or agent, wait for a reset, or wait for the person. A new part never sees the agent's own
conversation. Instead Daoris hands it a brief of the line, composed from its own records with no model. Every session is told,
once at its start, a standing block saying where things are. It also sees its line's counts as facts. Daoris keeps an account
of everything it handed, section by section.

## Its parts

| Unit | File | Its job |
|---|---|---|
| `CourseDecision` | `Course/Domain/LineCourse.Decide.cs` | The one pure decision of how a line goes on, paths A to N, for both loops |
| `CourseTrigger`, `CourseFacts` | `Course/Domain/CourseTriggers.cs`, `CourseFacts.cs` | What asks the course, and the facts an executor hands it |
| `MoveStore`, `MoveRecords` | `Course/MoveStore.cs`, `Course/MoveRecords.cs` | Every move's one durable row (`moves`, `move_words`) and its phases |
| `ConversationMoves`, `MoveTakeUp` | `Course/ConversationMoves.cs`, `Course/MoveTakeUp.cs` | The conversations' move holder; takes up a dead shell's moves at come-up and at each look |
| `MoveDelivery`, `MoveResolve`, `MoveBoxImports` | `Course/` | Delivers moved words; settles a held move; imports an older build's boxes once |
| `ChatRunner` | `Loop/Chat/ChatRunner.cs` (hub, 202 KB) | Starts and runs conversations on both doors; composes a first message; builds the registered executors and reaches each through its kind's door |
| `ExecutorAttribute`, `IExecutor`, `IExecutorWiring` | `Course/Chat/Executors/` | A conversation's kinds of run, each registered with its phase (a start before a move) and what it comes after; the runner builds them in that order from its wiring and refuses a cycle; listed in `docs/index/registered.md` |
| `HelpStart` | `Help/HelpStart.cs` | Ask Daoris's start: placed in its workspace's room, the machine read within its bound, run; its replacement's open |
| `ChatHelp`, `IChatHelping` | `Help/ChatHelp.cs`, `Course/Chat/ChatHelping.cs` | Executor `help-rooms`: a start from an earlier Ask Daoris conversation's words, a workspace's running one set aside, the room as it stands; the course reaches it by its port |
| `HelpAccessSwap`, `HelpPlacement` | `Help/` | A change of Ask Daoris's access ending its running conversations at their turn's end, and the receipt of what a start was handed; the place Ask Daoris runs in, under its access |
| `BriefedMove` | `Course/Chat/BriefedMove.cs` | The move to a new part handed Daoris's brief: by itself where a conversation is proven lost, or by the person's press |
| `WordsStart`, `StartRequests` | `Course/Chat/` | The start on words: *Start a conversation with these words*, and a conversation opened on its first words, a request id answering a retry |
| `ChatReplacement` | `Course/Chat/ChatReplacement.cs` | A start whose agent refused the model at its open: the re-ask, and the replacement opened with its words once the refused run let go |
| `ChatTurns` | `Loop/Chat/ChatTurns.cs` | A conversation's turn queue: holds, limit waits, edits, stops |
| `ProtocolChat`, `NativeChat` | `Loop/Chat/` | A conversation's transport on each door: the protocol session's turns, steers, retries and stop; the native wire's stdin line per turn and its interrupt |
| `ChatGoOn` | `Course/Chat/ChatGoOn.cs` | Executor `go-on`: an ended chat goes on with the words kept on its record; it hears the record once the runner is made |
| `ChatHelperWords` | `Course/Chat/ChatHelperWords.cs` | Executor `helper-words`: a helper's answer said to the conversation that asked, as its next turn |
| `LimitMove`, `LimitWaits` | `Course/Chat/` | Executors `limit-move` and `limit-wait`: a limit at a turn's end moves to the next account now, or waits for the look's `limits` stage to wake it |
| `CurveMove` | `Course/Chat/CurveMove.cs` (`Agents/Domain/SwitchDecision.cs`) | Executor `curve-move`: the usage curve's move of a conversation at its turn's end |
| `HandOver`, `ChatHandOver`, `HandOverQuest` | `Course/HandOver/`, `Course/Chat/` | Hand-overs to another agent or account, for conversations (executor `hand-over`) and quests' sessions |
| `StartFrom`, `StartWith` | `Course/Chat/ChatRunner.StartFrom.cs`, `StartWith.cs` | *Start a conversation with these words*; a conversation opened on its first words |
| `DrivenCourse`, `LimitResume` | `Course/GoOn/DrivenCourse.cs`, `Course/LimitResume.cs` | A driven line's carry-ons, and its resume after a limit or update |
| `Continuations`, `ResumeAsk` | `Course/Domain/Continuation.cs`, `ResumeAsk.cs` | Judges whether a record resumes; what a resumed run is told |
| `ContinuationAdmissions`, `AnswersAloneHold` | `Course/ContinuationAdmissions.cs`, `Course/GoOn/` | A line's serialized go-on admission; the answers-alone bound |
| `SaidDoor`, `DrivenInbox` | `Course/SaidDoor.cs`, `Course/DrivenInbox.cs` | Words kept on a record's `said`, and their delivery to a running session |
| `ContextBrief`, `ContextBriefs` | `Context/Domain/ContextBrief.cs`, `Context/Events/ContextBriefs.cs` | The brief (pure), and the read of the line it is composed from |
| `StandingBlock`, `StandingBlocks` | `Context/Domain/StandingBlock.cs`, `Context/StandingBlocks.cs` | The standing block (pure), and the facts a start reads for it |
| `InstructionAccounting`, `HandedCodes` | `Context/InstructionAccounting.cs`, `Events/Domain/HandedCodes.cs` | The account of what a session was handed; page `work/conversation/HandedAccount.tsx` |
| `LineKeys`, `LineCounts`, `LineCountsRead`, `CountTexts` | `Course/Domain/`, `Course/LineCountsRead.cs`, `Roles/Domain/CountTexts.cs` | A line's key, its counts, and Daoris's words for them |
| `SessionEventLog`, `TranscriptFile` | `Context/Events/`, `Context/TranscriptFile.cs` | A session's events and verbatim transcript, kept on the machine |
| `HelpConversations` | `Context/HelpConversations.cs` | Ask Daoris's history: names, pins, what a conversation started from and was handed |
| `HelpRoom`, `HelpMode`, `HelpProposals` | `Help/` | Ask Daoris's room, access mode and proposals |
| `LineEvents`, `SessionEntries` | `src/Daoris.Service/Daoris.Service.Core/Sessions/` | The connector's `line_read`; the entry key and `from` on the service's records |

## Its rules

**Records and lines**

1. One Daoris record is one harness conversation. Daoris resumes only by the id it kept from the wire, never by
   `--continue`, `--last` or a name. (from D131, D137)
2. A session on the window is an entry: one line of work in one repository. Its records are its parts, linked by `from`
   with a why and who. The ledger writes the entry key at the open and never rewrites it. (from D167, D189)
3. Every continuation writes its `from` link first. The whys are `press`, `near`, `limit`, `agent`, `curve`, `resume`,
   `carry-on`, `retry`, `not-offered`, and a policy's `transient`, `failed`, `quiet`, `budget`. (from D189, D163, D226)
4. A line has one key, `LineKeys.Of`: walk back by `from` on this machine; the quest's id for a driven part, else the first
   part's id. Moves, counts and the go-on admission all read it. (from D229)
5. A session's events (`<id>.events.jsonl`) and verbatim transcript stay on the machine. No HTTP surface reads them.
   (from D76)

**Two contexts**

6. The agent's own conversation is the working context. It lives in the account's home, and Daoris never reads, copies or
   writes it. Daoris's record is the portable context. (from D189, D175)
7. Same agent, account and tree, a kept id and a door that resumes: the same part goes on. Otherwise a new part of the entry
   opens in the same tree. The old part ends at its turn's end before the new one opens. (from D189, D131)
8. A conversation that was left is never resumed again. Going back to its agent or account opens another new part.
   (from D189)

**The course and moves**

9. Every executor asks `CourseDecision.Decide(facts, trigger)` and acts on the answer. What never goes on is judged first:
   a teammate's record, an intake, a stood-down record, a superseded record, a standing move's old part. (from D212, D137)
10. Every move is one durable row in the desktop store, with phases `decided` to `delivered`, `fell-back` or `held`, then
    `closed`. This build writes no box. An older build's `.hand-over.json` or `.limit-move.json` is imported once.
    (from D212)
11. While a move stands, its destination takes no move of its own. A press holds the line's reservation, so a second
    hand-over waits. Nothing opens while the application winds up. (from D212)

**Go-ons**

12. Words said to a parked or ended record are kept on its `said` before anything holds them. They come off by id once on
    the wire. A word a gone shell attempted is settled and never sent twice. (from D137, D188)
13. An ended chat goes on at once with its kept words. The ledger moves it out of its ended state to `working`. Then its
    own conversation resumes, every waiting word in its first turn. A go-on gets no block and no brief. (from D137, D230)
14. A conversation whose own conversation is lost (`gone`, `unkept`, `unable`, `offered`) goes on by itself in a new part
    handed the brief, why `resume`. Refused as `elsewhere`, or for a reason that is not loss: it waits for the person.
    (from D189)
15. A driven record whose resume falls back carries on by itself in a new part handed the brief, why `retry`. Only
    `elsewhere` holds it for the person. (from D189, D131)
16. *Start a conversation with these words* is only the person's press. It opens a new line with no `from`, carrying the
    person's words alone. It is refused while running, with no words, or while the quest is open or taken. (from D137, D189)
17. Answers alone (a helper's, a second opinion's) take a line on at most twice in a row. The next one waits for the
    person's word, *Go on with this answer* or *Not now*. On a home whose rules are reached, the supervisor rule sets the
    number. (from D188, D229)
18. Past its transient bound, a record goes on only with a word the person said after it ended, or with *Try again*.
    (from D210, D212)

**Moves between accounts and agents**

19. A conversation's limit, at its turn's end: another account of the same agent with room gets a new part, briefed, the
    held words after the brief, why `limit`. Else the role's next choice on another agent. Else it waits for the earliest
    reset. (from D206, D205, D197)
20. An account the person picked is never moved off. It waits for that account's reset. (from D206, D205)
21. A limited end is `failed` with the limit flag on the wire. It is worded *limited*, never red, with no strike and no
    *Ended* notice. (from D206)
22. The wake is the look's `limits` stage, with no timer. Own account ready and conversation resumable: a full resume,
    Daoris's `limit.reset` word, then the held words, never a brief. Else a briefed part elsewhere. Else it waits. (from D206)
23. A driven line at a limit resumes the same way, inside its carry-on's start. It walks its role's choices only where its
    own account is not ready. (from D206)
24. The curve moves a conversation at its turn's end, with nothing running or queued, to another account of the same agent
    in its scope with room. Never mid-turn or across agents. Never Ask Daoris or the tool's own home. Not again within 30
    minutes. (from D165)
25. `switch: off` moves nothing. A driven line meets the curve only at its next start's walk, why `curve`. (from D165, D125)
26. A hand-over is the person's press or the agent's ask. The agent's ask passes the `handOver` rule first: `on` admits it,
    `ask` puts it to the person, `off` refuses it. (from D163)
27. A conversation's hand-over is taken at its turn's end. Its first message is the brief, with none of the person's words.
    A quest's session stops at once for the person's press, which is their stop. An agent's ask waits for the turn's end.
    (from D163, D188)
28. A hand-over is refused for Ask Daoris, for the same agent and account, for a closed quest, for a tree that is gone, and
    for a session the person parked. (from D163, D212)

**The brief**

29. The brief is a pure function of the line's records, in Daoris's one English, with no model. It opens by saying it is
    not the agent's memory. (from D189)
30. Its sections: the request as first written; every word taken (the person's verbatim, others' as claims); each turn's
    last message and one line of what it did; the plan; uncommitted paths and commits; the last failed check; attachments;
    the outgoing agent's note; the entry's other parts; the line's course; what was left out. (from D189, D163, D229)
31. Its bound is 40,000 weight, with a Han character weighing 3. Each section has a cap. Past the bound, the oldest turn and
    then the oldest word drop. It is kept as the new part's first event, with its account. (from D189)
32. It never hands the agent's thoughts, a tool call's output (but the last failed check's, bounded), a model's summary, an
    account's name, or anything from a harness's store. (from D189)
33. `line_read` pages one part at a time, by part id, under the brief's bound. A working session may call it; a reviewer and a
    helper may not. It carries no counts. (from D189, D229)

**What a session is told at its start**

34. The driver composes the standing block with no model. It is handed once, at the start or at a move (with the memory the
    line read), and never at a go-on. It says where things are, the routing, the person's choices, and the repository's and
    workspace's facts. (from D230)
35. The whole first prompt fits the native door's one argument (32,767). Over 30,000, the block goes to
    `spawn/<session>.standing/block.md`, and one line points to it. (from D230)
36. A conversation's first message carries where the person is (bounded), the browser line, the process in a tree of its
    own, an open workflow draft, the person's words, then the block. A hand-over's first message is the brief and Daoris's
    briefing. (from D162, D163, D164, D230)
37. A session is listed only the tools its kind may call: the core 18 plus its kind's. Ask Daoris gets every tool. A Claude
    session runs with auto-memory and synced skills, plugins and connectors off. No account home is written. (from D230)

**Counts and the handed account**

38. `LineCounts.Of` reads each count from the one record that carries it, and stores nothing. A count is *exact*,
    *at least* or *unknown*, with why. A count a line does not have is absent. (from D229)
39. The session sees its counts as facts: in the brief's course section, in a go-on's words, and at a start with no brief
    where a count is above zero. They are never instructions and never another agent's text. (from D229)
40. Everything handed is accounted: each section's size, source and cuts, by `HandedSections` and `HandedCuts` codes. The
    account rides the event of the instruction or first message. A section that could have been handed and was not is
    said, with why. (from D143, D189)

**Ask Daoris**

41. Ask Daoris is a conversation in its own room, one at a time per workspace. It proposes, and the person applies every
    change. A limit move opens it again in its room. (from D89, D170, D183, D158)
42. Its conversations stay on this machine. Only those the person spoke in or named are listed, pinned first, then newest.
    They resume by kept id. A briefed continuation stays one history row. (from D158, D189)
43. A new Ask Daoris conversation started from an earlier one is handed Daoris's brief of that one's line, why `press`, ahead
    of the person's first message, and keeps it as Daoris's first prompt. A repository's *Start a conversation with these
    words* carries the words alone. (from D158, D233)

## Where a change goes

- **How a line goes on** (a new trigger, path or hold): `Course/Domain/LineCourse.Decide.cs` and its `CourseFacts`. The
  executor that acts goes in `Course/Chat/` for a conversation and `Course/GoOn/` for a driven line, in a unit of its own over
  `ChatContext`. A new kind of conversation run is a module in the subdomain of its job, marked `[Executor(kind, phase, after)]`
  and built from `IExecutorWiring`; the runner gains only its door. An executor reaches another only by `Of<T>()` on its wiring
  (its type, or its port's where a test stands in for it), when it runs; never through a constructor or a callback the runner
  sets. Durable state goes in `MoveStore`, never a new box.
- **What a session is told** (brief, block, first message, go-on words): `Context/` for composing. `ChatRunner` and
  `ResumeAsk` only hand it on. A new section needs its `HandedSections`/`HandedCuts` code and both catalogues' twin rows.
- **Counts:** `Course/Domain/LineCounts.cs` reads them, `Roles/Domain/CountTexts.cs` words them, and the supervisor's
  rules are the roles area's.
- **Ask Daoris:** history in `Context/HelpConversations.cs`; a door's answer, where one is wanted, in `Help/Proposals/` with its
  row in the room's doors table.
- **Never grow the hubs.** `Loop/Chat/ChatRunner.cs` (202 KB), `ChatTurns.cs` (72 KB) and `Course/Chat/ChatGoOn.cs`
  (66 KB) are over budget. New behaviour goes in its own file (D231). Agents comes before Participants and Context in the
  structure gate (D166).
- **Tests that pin it:** `LineCourseTests` (the pinned path table), `CourseCensusTests` (`fixtures/course-sites.json`),
  `CourseBoundaryTests`, `CoursePortsTests`; `ChatGoOnTests`, `BriefedMoveTests`, `LimitWaitsTests`, `LimitMoveStoreTests`,
  `CurveMoveTests`, `ChatHandOverTests`, `HandOverTests`, `MoveStoreTests`, `MoveStoreCensusTests`, `MoveDeliveryTests`; the
  `*CrashProcessTests`, `*DowngradeProcessTests`, `*OlderBuildProcessTests` per move; `ContextBriefTests`,
  `StandingBlockTests`, `InstructionAccountTests`, `CourseCountsTests`, `LineCountsTests`, `LineKeysTests`,
  `AnswersAloneHoldTests` (all in `Daoris.Desktop.Driver.Tests`); `Loop/ChatGoesOnProcessTests`, `Loop/ChatTurnsTests`;
  `DriverModuleConversationTests` (modules); `LineEventsShapeTests`, `LineEventFilesTests` (service);
  `ConversationView.test.tsx`, `conversation.test.ts`, `HandedAccount.test.tsx`, `locales/say.test.ts` (web).
- **Gates that bite:** `driver`, `censuses`, `driver-process` (moves' crash, downgrade and older-build runs), `modules`,
  `service` (for `line_read`), `web-unit` (en and zh twins), and `universal` (structure gate, size ratchet, generated index).

## Contradictions

1. D189 point 2 ("the old part ends at its turn's end, never mid-turn") vs `Course/Domain/LineCourse.Decide.cs:530`: the
   person's hand-over of a quest's session moves now, as their stop. The code reads right (D163 point 1).
2. D137 point 4 and D158 point 3 (a chat, or one with no kept id, waits for the person's new conversation) vs
   `Agents/Domain/ContinueReason.cs:46` and `LineCourse.Decide.cs:152`: `unkept` and `gone` count as lost, and the line goes on
   by itself, briefed. The code (and D189 point 3) reads right. `Course/Chat/ChatGoOn.cs:72-74`'s comment is stale.
3. D137 point 7 (a cooling account holds the words, with *Go on in a new session* offered) vs `LineCourse.Decide.cs:172-176,
   563`: a conversation's words switch off a cooling account now, and *Go on in a new session* is a driven record's alone.
   The code reads right.
4. D125 point 4 ("a running session never rotates") vs D165 point 3 and `Course/Chat/CurveMove.cs`: a conversation moves at
   its turn's end. D165 reads right.
5. D80, D125 point 4, D131 point 3 and D163 point 1 (a new part gets the request, last plan, last words and what is
   uncommitted) vs D189 point 4 and `Context/Domain/ContextBrief.cs:171`: every new part gets the brief. D189 reads right.
6. D163's MIXAGENT1a-i note point 1 (the ask is the box `<id>.hand-over.json`) vs `Course/HandOver/HandOver.cs:115-118`: a
   move row in the store; boxes are imported only. The code reads right. `HandOver.cs:1-5`'s comment is stale.
7. `Events/Domain/HandOverWhy.cs:9-10` and D163's note (`near` means the account is near its limit) vs
   `Roles/Domain/CountTexts.cs:104` ("handed over near its context's end"). D163 reads right; the count's words are wrong.
8. D206's body cites `Conversations/Chat/ChatGoOn.cs`. The driver has no `Conversations` folder; the unit is
   `Course/Chat/ChatGoOn.cs`. The code reads right.
9. D188's status line says *proposed*. Its own note (`docs/decisions/D188.md:178`) says accepted and built by COWORK1a-d.
    The note reads right.
10. `TASKS.md`'s LIMITGOON1 line and `Course/Chat/LimitMove.cs:4` say a limit move goes on "in the same conversation". D189
    points 1-2 and `LimitMove.cs:12-13` make it a new part handed the brief. The new part is right; the wording mixes up the
    line and the agent's conversation.
11. SESSCTX1e's re-cut (*Go on fresh* as a hand-over to the same agent and account) vs `LineCourse.Decide.cs:500`, which
    refuses exactly that (`HandOver.SameAgent`). The code is today's; the re-cut must change the refusal when it is built.

## Open

- **SWITCHCTX1a onward: the account carry.** A move keeps the agent's own conversation by carrying it to the other
  account's home. Proposed, waiting on Codex's owed read; nothing is built. `Course/MoveRecords.cs` reserves the `carry`
  kind and the `copied` and `verified` phases.
- **SESSCTX1a:** resume and a fresh part measured on each door. No brief has been counted on a harness.
- **SESSCTX1e (ONEWAY1j):** *Go on fresh*, Daoris's compaction on the person's press, re-cut as a hand-over to the same
  agent and account.
- **LIMITGOON1 (row i):** the limit move seen on the installed window.
- **LIMITRESUME1 (ONEWAY1g row f):** the limit resume's remaining row on the installed doors.
- **CONTEXTDRIVE1n:** a conversation's course reads the supervisor rules (STAFFPOLICY1e3's conversation half).
- **CHATFLOW1:** a conversation's work in its own tree runs its repository's workflow.
- **HELPWIRED1b:** what the door says it loaded, beside what was handed.
- **COAGENT1c / COAGENT1g:** a conversation as the lead; a hand-over waits for the outgoing part's background work and
  children.
- **RPC1g:** a terminal's conversation is the running Daoris's (design first).
- **MSG1h, MSG1i, STEER3:** next-step words on Codex's and the native doors; other steering measured.
