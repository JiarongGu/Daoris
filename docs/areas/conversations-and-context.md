# Conversations and context — how it works now

Read from main at `e38ab6db6` (2026-11-08). Paths without a root are under `src/Daoris.Desktop/Daoris.Desktop.Driver/`.

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
| `CourseDecision`, `LimitCourse` | `Course/Domain/LineCourse.Decide.cs`, `Course/Domain/LimitCourse.cs` | The one pure decision of how a line goes on, paths A to N, for both loops; the limit paths (D, E, F), the carry-on and the transient bound are `LimitCourse` |
| `CourseTrigger`, `CourseFacts` | `Course/Domain/CourseTriggers.cs`, `CourseFacts.cs` | What asks the course, and the facts an executor hands it |
| `MoveStore`, `MovePhases` | `Course/MoveStore.cs`, `Course/MoveRecords.cs` | Every move's one durable row (`moves`, `move_words`) and its phases. `MoveRows` holds the columns by store version and the row's reading, `MoveImporter` writes a box's move with its receipt, and `MoveTakes` takes or receives a move from a run that goes no further; the store keeps one-line doors to them |
| `ConversationMoves`, `MoveTakeUp` | `Course/ConversationMoves.cs`, `Course/MoveTakeUp.cs` | The conversations' move holder; takes up a dead shell's moves at come-up and at each look |
| `MoveDelivery`, `MoveResolve`, `MoveBoxImport` | `Course/` (`MoveBoxImports.cs`) | Delivers moved words; settles a held move; imports an older build's boxes once |
| `ChatRunner` | `Loop/Chat/ChatRunner.cs` (root, 37 KB) | Wires a conversation's units and the registered executors; keeps the doors callers use, each one line to the unit or executor that does the work |
| `ChatRun` | `Loop/Chat/ChatRun.cs` | The run of a conversation whose record is open, one method for every kind: its process spawned with its rules and servers, its door's turns held, its watch started, or its record concluded when the spawn fails |
| `ExecutorAttribute`, `IExecutor`, `IExecutorWiring` | `Course/Chat/Executors/` | A conversation's kinds of run, each registered with its phase (a start before a move) and what it comes after; the runner builds them in that order from its wiring and refuses a cycle; listed in `docs/index/registered.md` |
| `HelpStart` | `Help/HelpStart.cs` | Ask Daoris's start: placed in its workspace's room, the machine read within its bound, run; its replacement's open |
| `ChatHelp`, `IChatHelping` | `Help/ChatHelp.cs`, `Course/Chat/ChatHelping.cs` | Executor `help-rooms`: a start from an earlier Ask Daoris conversation's words, a workspace's running one set aside, the room as it stands; the course reaches it by its port |
| `HelpAccessSwap`, `HelpPlacement` | `Help/` | A change of Ask Daoris's access ending its running conversations at their turn's end, and the receipt of what a start was handed, with what the native door's init frame said it loaded (`WireReading.Loaded` from `NativeInit`, kept by `HelpLoaded` on the newest receipt and shown beside it); the place Ask Daoris runs in, under its access |
| `BriefedMove` | `Course/Chat/BriefedMove.cs` | The move to a new part handed Daoris's brief: by itself where a conversation is proven lost, or by the person's press |
| `WordsStart`, `StartRequests` | `Course/Chat/` | The start on words: *Start a conversation with these words*, and a conversation opened on its first words, a request id answering a retry |
| `ChatReplacement` | `Course/Chat/ChatReplacement.cs` | A start whose agent refused the model at its open: the re-ask, and the replacement opened with its words once the refused run let go |
| `ChatTurns` | `Loop/Chat/ChatTurns.cs` | A conversation's turn queue: holds, limit waits, edits, stops; the one door of its parts |
| `TurnState` | `Loop/Chat/Turns/TurnState.cs` | The turns' state under its one gate: what it guards, the keep's lock before it, what a race would break |
| `TurnPump`, `TurnTakes`, `TurnKeeps`, `KeepRetry`, `TurnHolds`, `TurnStops` | `Loop/Chat/Turns/` | The one pump; a word's take at its level; the keeps on the record, edits, press and send-back, each keep under a key and asked twice before it is held here alone (`KeepRetry`, so a word the record stored but never answered is held once); the holds; the stops |
| `ProtocolChat`, `NativeChat` | `Loop/Chat/` | A conversation's transport on each door: the protocol session's turns, steers, retries and stop; the native wire's stdin line per turn and its interrupt |
| `ChatOptions` | `Loop/Chat/ChatOptions.cs` | A conversation's model and effort as its agent offers them, the person's change of either (never the mode), and the change as last kept |
| `ChatWords` | `Loop/Chat/ChatWords.cs` | The words said to a live conversation: the person's (framed, kept on the record, at a level or at *now*), Daoris's own, a composed message and the workflow draft it carries |
| `ChatTurnDoors` | `Loop/Chat/ChatTurnDoors.cs` | What is read of a live conversation's turns and done to them: where they stand, a word's reach, the press hold, waiting words edited, the turn stopped |
| `ChatSendBack` | `Loop/Chat/ChatSendBack.cs` | Another agent's findings handed to a conversation as a turn: sent back to the one running, or the ones an ended conversation goes on with |
| `ChatWatch`, `ChatWatched` | `Loop/Chat/` | The watch over a conversation's run from its open to its end: opens or resumes its agent, reads how it ended, concludes its record and lets go of what the run held |
| `ChatConclusion` | `Loop/Chat/ChatConclusion.cs` | How a conversation's record ends: the state and note, the limit a last turn met, the ending note of a run that simply ended |
| `ProtocolHeld` | `Loop/Chat/ProtocolHeld.cs` | A conversation's session on the protocol door as its run holds it open: what it is handed and set before its first prompt, and what its open's failure says |
| `ChatStarts` | `Loop/Chat/ChatStarts.cs` | A conversation's start in a repository: its role, harness and account asked before any record exists, then its tree, its open and the run over its process; a failure between its open and its run concludes the record `failed` |
| `ChatClosing` | `Loop/Chat/ChatClosing.cs` | How conversations end at the driver's hand and a person's: the shell closing stops every one and keeps the update's restart word; a person finishing one |
| `RepositoryPlacement` | `Loop/Chat/RepositoryPlacement.cs` | Where a conversation in a repository runs and what its agent may do there: its tree, its rules, what it may reach across |
| `ChatWalk` | `Loop/Chat/ChatWalk.cs` | A new conversation's walk over its actor's effective order, choice by choice, remembering the passes it has said |
| `ChatServers` | `Loop/Chat/ChatServers.cs` | The servers a conversation is handed at its open: the knowledge connector carrying who it is, and the plugins' servers with the browser brought up for one that drives it |
| `ChatTold` | `Loop/Chat/ChatTold.cs` | What a new conversation is told with its first message, held from its start: its process, its execution's checks, its standing block's facts |
| `ChatAccountReads` | `Loop/Chat/ChatAccountReads.cs` | A refused call read for its account: a limit cools it, a refused sign-in signs it out; what a part going on from a limit carries |
| `ChatGoOn` | `Course/Chat/ChatGoOn.cs` | Executor `go-on`: an ended chat goes on with the words kept on its record; it hears the record once the runner is made. `GoingOnRun` (a go-on judged and started, a chat's or Ask Daoris's) and `GoOnRefusals` (the agent's refusal, words that cannot go on, a lost conversation) are built per call over it, reaching its siblings through the wiring; the executor keeps one-line doors to them |
| `ChatHelperWords` | `Course/Chat/ChatHelperWords.cs` | Executor `helper-words`: a helper's answer said to the conversation that asked, as its next turn |
| `LimitMove`, `LimitWaits` | `Course/Chat/` | Executors `limit-move` and `limit-wait`: a limit at a turn's end moves to the next account now, or waits for the look's `limits` stage to wake it. The stage skips a look while its `LimitStamp` stands and no wait is due: the choices' `Effective` (driver.json's content key and the staffing's revision the load read before it), the revisions of every family the move reads (`LimitMove.Reads`: `role_staffing`, `roles`, `role_policy`, `role_starts`, `supervisor_rules`, `role_moves`, `bindings`) in one query, the ledger's `StaffHolds` count (every mark, clearing, reservation and release moves it) and the accounts' files; a staffing store that failed at the load stamps nothing, so the next look runs, and a held wait is due no later than `StaffLedger.NextChange` (a mark's or a reservation's end, the oldest counted move leaving its 24 hours, the next day where a policy counts one). `LimitMove`'s ended-conversation move is `EndedLimitMove`, built by it and reaching its siblings through the wiring at run time; both carry through `ChatLimitCarry` and fence their hand through `MoveFence`; `LimitNotes`, `LimitResetWord` and `GoingOn` (the go-on's record of its words, and the carried part it opens) are files of their own |
| `CurveMove` | `Course/Chat/CurveMove.cs` (`Agents/Domain/SwitchDecision.cs`) | Executor `curve-move`: the usage curve's move of a conversation at its turn's end |
| `ConversationRules`, `TransientMove`, `LimitRuled` | `Course/Chat/` | A conversation's line as the supervisor's rules read it; executor `transient-move`, the rules' move or hold past its episode; the limits rule's wait and ask and the moves bound before the limit's walk |
| `HandOver`, `ChatHandOver`, `QuestHandOver`, `PartLeftovers`, `QuestPartStop` | `Course/HandOver/`, `Course/Chat/` | Hand-overs to another agent or account, for conversations (executor `hand-over`) and quests' sessions; what the old part left running, ended and waited for |
| `StartFrom`, `StartWith` | `Course/Chat/ChatRunner.StartFrom.cs`, `StartWith.cs` | *Start a conversation with these words*; a conversation opened on its first words |
| `DrivenCourse`, `LimitResume` | `Course/GoOn/DrivenCourse.cs`, `Course/LimitResume.cs` | A driven line's carry-ons, and its resume after a limit or update |
| `Continuations`, `ResumeAsk` | `Course/Domain/Continuation.cs`, `ResumeAsk.cs` | Judges whether a record resumes; what a resumed run is told |
| `ContinuationAdmissions`, `AnswersAloneHold` | `Course/ContinuationAdmissions.cs`, `Course/GoOn/` | A line's serialized go-on admission; the answers-alone bound |
| `SaidDoor`, `DrivenInbox` | `Course/SaidDoor.cs`, `Course/DrivenInbox.cs` | Words kept on a record's `said`, and their delivery to a running session |
| `ContextBrief`, `ContextBriefs` | `Context/Domain/ContextBrief.cs`, `BriefLine.cs` (its input records), `BriefReading.cs` (the line read into turns), `Context/Events/ContextBriefs.cs` | The brief (pure), and the read of the line it is composed from |
| `StandingBlock`, `StandingBlocks` | `Context/Domain/StandingBlock.cs`, `Context/StandingBlocks.cs` | The standing block (pure), and the facts a start reads for it |
| `InstructionAccounting`, `HandedSections` | `Context/InstructionAccounting.cs`, `Events/Domain/HandedCodes.cs` | The account of what a session was handed; page `work/conversation/HandedAccount.tsx` |
| `LineKeys`, `LineCounts`, `LineCountsRead`, `CountTexts` | `Course/Domain/`, `Course/LineCountsRead.cs`, `Roles/Domain/CountTexts.cs` | A line's key, its counts, and Daoris's words for them |
| `SessionEventLog`, `TranscriptFile` | `Context/Events/`, `Context/TranscriptFile.cs` | A session's events and verbatim transcript, kept on the machine; every writer of a record, whichever instance, shares one gate and numbers from the record's own length and last line |
| `HelpConversations` | `Context/HelpConversations.cs` | Ask Daoris's history: names, pins, what a conversation started from and was handed |
| `HelpRoom`, `HelpMode`, `HelpProposals` | `Help/` | Ask Daoris's room, access mode and proposals |
| `HelpFacts`, `HelpTreeFacts`, `HelpWorkFacts`, `HelpRoomCatchUp` | `Knowledge/HelpFacts.cs`, `HelpTreeFacts.cs`, `HelpWorkFacts.cs`, `HelpWorkRecords.cs`, `HelpTreesRecords.cs`, `HelpRoomCatchUp.cs` | The room's facts: topics, wakes and registration in the hub, the sessions' trees and the work's and clean-up's parts each in their own type, their records and the room's catch-up apart |
| `HelpRoomDoors`, `HelpDoor`, `HelpDoorAsk` | `Knowledge/Room/HelpRoomDoors.cs`, `HelpDoorRowsSetup.cs`, `HelpDoorRowsMachine.cs`, `HelpDoorRowsActs.cs`, `HelpDoorPlaces.cs`, `HelpDoor.cs` | The doors table: rows in three files by the act they serve, in the order a person meets the tasks |
| `HelpWorkflowProposals` | `Help/Proposals/HelpWorkflowProposals.cs`, `HelpWorkflowDrafts.cs`, `HelpWorkflowRoutes.cs`, `HelpWorkflowCard.cs` | The `workflow` proposal: the hub, the draft door, the routes and the card each apart |
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

6. The agent's own conversation is the working context. It lives in the account's home, and Daoris never reads or writes it;
   a driven line's limit move copies its files whole to the next account's home of the same agent (rule 24). Daoris's record is
   the portable context. (from D189, D175, D206)
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
    number, and a `refer` rule holds the line for the agent supervisor's course act (roles area). (from D188, D229, D233)
18. Past its transient bound, a record goes on only with a word the person said after it ended, or with *Try again*.
    (from D210, D212)
19. A conversation's line reads the supervisor's rules as a driven line does (`ConversationRules`): its episode makes its
    tries rule's number of tries; past them a `next` or `weighted` rule moves it to another choice of its role, a new part
    handed the brief (`transient`, by `daoris`) and counted in `moves` under the line's key once the take would accept it and
    the move is begun (`TransientMove`), an `ask` or the moves rule's
    bound holds it, and the seeded `hold` ends its turn `chat.transient-stopped`. Its limits rule's `wait` (within its
    `upTo`, which `LimitRuled` counts from each limit's judgement: a repeated limit is a new cool-off, and the course holds no
    window of its own for a conversation) and `ask`, and the bound, keep it on its own account before the limit's walk (`LimitRuled`), reading the moves
    under the line's key; a walk to another choice is counted there, the move of an ended conversation (`EndedLimitMove`) as the live one's. Each act is said and kept as the rule's decision. (from D229)

**Moves between accounts and agents**

20. A conversation's limit, at its turn's end: another account of the same agent with room gets a new part, why `limit`. A
    repository's chat carries its own conversation there as a driven line does (rule 24): the new part's record opens as that
    conversation's resume, the held words then Daoris's word its prompt, no brief, compacted first past its provider's bound. It is
    briefed, the held words after the brief, where its door cannot carry it or the copy fails, saying why; where the door refuses the
    carried resume, the same part goes on in the brief, the copy removed, saying so. Ask Daoris's room and a host whose open moves no
    word are briefed. Else the role's next choice on another agent, briefed. Else it waits for the earliest reset. (from D206, D205, D197)
21. An account the person picked is never moved off. It waits for that account's reset. A new part a move opens on the picked
    account of the same agent keeps the pick, one agent's two doors one agent and two providers sharing a pool two. (from D206,
    D205, D212)
22. A limited end is `failed` with the limit flag on the wire. It is worded *limited*, never red, with no strike and no
    *Ended* notice. (from D206)
23. The wake is the look's `limits` stage, with no timer. Own account ready and conversation resumable: a full resume,
    Daoris's `limit.reset` word, then the held words, never a brief. Else a briefed part elsewhere. Else it waits. (from D206)
24. A driven line at a limit resumes the same way, inside its carry-on's start. It walks its role's choices only where its
    own account is not ready. On another account of the same agent its new part carries the conversation there and resumes it
    with no brief, compacted first past its provider's bound, and is briefed only where the door cannot carry it or the copy
    fails, saying why (agents and harnesses, rule 38). A conversation's limit move carries the same way (rule 20). (from D206)
25. The curve moves a conversation at its turn's end, with nothing running or queued, to another account of the same agent
    in its scope with room. Never mid-turn or across agents. Never Ask Daoris or the tool's own home. Not again within 30
    minutes. (from D165)
26. `switch: off` moves nothing. A driven line meets the curve only at its next start's walk, why `curve`. (from D165, D125)
27. A hand-over is the person's press or the agent's ask. The agent's ask passes the `handOver` rule first: `on` admits it,
    `ask` puts it to the person, `off` refuses it. The steward's hand-over act of a quest's session runs the person's press
    inside its grants (roles area). (from D163)
28. A conversation's hand-over is taken at its turn's end. Its first message is the brief, with none of the person's words.
    A quest's session stops at once for the person's press, which is their stop. An agent's ask waits for the turn's end.
    Before the next part starts in the tree (a conversation's hand-over, an account's move, or a quest's session handed over),
    the old part's job is taken from its tracking, and once its process has gone what the job still holds (a command left in the
    background) is ended and waited for, up to 30 seconds (`PartLeftovers`; a quest's part through `QuestPartStop`); the old
    record says so, `chat.left-ended`, or `chat.left-running` past the bound. A quest's move is written old-ended only after that
    wait, so its carry-on starts at a later look. (from D163, D188)
29. A hand-over is refused for Ask Daoris, for the same agent and account, for a closed quest, for a tree that is gone, and
    for a session the person parked. (from D163, D212)
    *Go on in a new session*, the words a cooling account holds on a driven record, is the same press naming no agent:
    `SESSION_HAND_OVER` without `agent` (and `daoris session hand-over <id>`) keeps the choice for the next look, and
    answers `{sessionId: null, kept, why, message}` with `GoOnNew`'s codes. `SESSION_GO_ON_NEW` and `go-on-new` are gone.
    *Go on with Daoris's brief* is the same press for a conversation, a closed quest's session or Ask Daoris's: where `GoOnNew`
    says the session is one of those, `SESSION_HAND_OVER` without `agent` opens the new part through `GoOnBriefedAsync`, and
    answers the same `{sessionId, kept, why, message}` (`kept` that the part opened, `sessionId` the part). `SESSION_GO_ON_BRIEF`
    and `useGoOnBrief` are gone; the work page and Ask Daoris press `useHandOver` with no agent.

**The brief**

30. The brief is a pure function of the line's records, in Daoris's one English, with no model. It opens by saying it is
    not the agent's memory. (from D189)
31. Its sections: the request as first written; every word taken (the person's verbatim, others' as claims); each turn's
    last message and one line of what it did; the plan; uncommitted paths and commits; the last failed check; attachments;
    the outgoing agent's note; the entry's other parts; the line's course; what was left out. (from D189, D163, D229)
32. Its bound is 40,000 weight, with a Han, kana, Hangul or fullwidth character, or a surrogate pair, weighing 3 (search's own Han
    test is a different question and stays as it is). Each section has a cap. Past the bound, the oldest turn and
    then the oldest word drop. A list the state's weight cap shortens is said as left out (`BriefList`), and only a plan past
    its 50 steps as past its bound (`PlanBound`). It is kept as the new part's first event, with its account. (from D189)
33. It never hands the agent's thoughts, a tool call's output (but the last failed check's, bounded), a model's summary, an
    account's name, or anything from a harness's store. A page a turn opened is named by its address without user, query or
    fragment, a file page relative to the tree. (from D189)
34. `line_read` pages one part at a time, by part id, under the brief's bound, weighed as the brief weighs (`LineEvents.Weight`
    is `ContextBrief.CharWeight`'s twin). A working session may call it; a reviewer and a
    helper may not. It carries no counts. A tool's output the driver cut is shown with the length its own mark gives, and no cut
    on either side splits a surrogate pair. (from D189, D229)

**What a session is told at its start**

35. The driver composes the standing block with no model. It is handed once, at the start or at a move (with the memory the
    line read), and never at a go-on. It says where things are, the routing, the person's choices, and the repository's and
    workspace's facts. (from D230)
36. The whole first prompt fits the one value Windows allows (32,767 characters: the native door takes it on stdin, and
    `DAORIS_TARGET` carries it as an environment variable). Over 30,000, the block goes to
    `spawn/<session>.standing/block.md`, and one line points to it. (from D230)
37. A conversation's first message carries the browser line, the process in a tree of its
    own, where the person is (bounded), an open workflow draft, the person's words, then the block. A hand-over's first
    message is the brief, Daoris's briefing and the browser line. The brief, the browser line and the draft are consumed
    only by a message a turn takes: one refused (finishing, held, gone) gives them back to the words said again, and the
    person waits on no answer to it. (from D162, D163, D164, D230)
38. A session is listed only the tools its kind may call: the core 18 plus its kind's. Ask Daoris gets every tool but the turns and the memory changes. A Claude
    session keeps its account's auto-memory and synced skills, plugins and connectors, as by hand, and is handed the skills,
    subagents, commands, servers, enabled plugins, Chrome switch, settings beneath Daoris's rules and memory file of the person's
    own tool home; a Codex session its configuration, instructions, skills and prompts; each family the person's to switch off
    (agents-and-harnesses 15). Daoris writes no account home.
    Each start of every kind is handed a delegate key beside its id and its repository; a press its verb proves with it is taken by
    its tier: its repository's access setup for a working session, its mode and granted acts for Ask Daoris
    (roles-and-supervision 56-58). Its held tool calls are shown on its own panel too, Ask Daoris's included. (from D230, D253, D262, D266)

**Counts and the handed account**

39. `LineCounts.Of` reads each count from the one record that carries it, and stores nothing. A count is *exact*,
    *at least* or *unknown*, with why. A count a line does not have is absent. (from D229)
40. The session sees its counts as facts: in the brief's course section, in a go-on's words, and at a start with no brief
    where a count is above zero. They are never instructions and never another agent's text. (from D229)
41. Everything handed is accounted: each section's size, source and cuts, by `HandedSections` and `HandedCuts` codes. The
    account rides the event of the instruction or first message. A section that could have been handed and was not is
    said, with why. The words themselves of a go-on's or hand-over's first message stay on its event, and the openings route
    answers them as `briefs`, bounded (`StartBrief`, 4000 characters, the cut said with the full length), for the head's
    *Started with*; they are no session's opening. A session working in its place is handed `place` where its start's
    manifest found any: what changed there outside Daoris since the last session, and the uncommitted paths that are not its
    own with how to leave them out of its commits, 30 of each named and the rest counted (`PlacePrompt`). (from D143, D189, D254)

**Ask Daoris**

42. Ask Daoris is a conversation in its own room, one at a time per workspace. It proposes, and the person applies every
    change. A limit move opens it again in its room, under the same focus. Its room folder, history and the one running are
    its home workspace's own. What it is about, its focus, is the person's choice: this machine, a workspace or a repository
    (`HelpFocus`), its home's by default (`default`'s is this machine), kept on its file beside its name and pin with the one
    waiting, each one held, and each message that carried a change (`HelpFocusMark`). The room is written for the focus
    (`HelpRoomScope.Project` over a place): the whole machine, a workspace's projection, or a repository's workspace with that
    repository's own first under *In focus*; its first lines name the focus and that it limits nothing, and its tools start at
    the focus's workspace (`all` for this machine), which the start's receipt names beside the focus. It reads another
    workspace by naming it, or every workspace with `all`, and proposes, inspects, reads checkouts and sets settings in any
    workspace's repositories, each judged by its own judge and none refused for the workspace it names. What a turn looked at
    or did beyond its focus is one folded receipt under that turn (`help/focus/BeyondReceipt.tsx`, `HELP_BEYOND`), only as
    Daoris's doors saw it: each call its connector answered naming a workspace or a repository, noted in the session's
    `places.jsonl` (`CallPlaceFilter`) with how many entries, repositories, convergence groups or quests its answer found,
    including zero and a knowledge get's single entry, as its tool says it answering
    (`CallFound`), and each press it made whose place was beyond, one run in full access as *Did* and one asked on a card as
    *Asked you* with the card's state, judged against the focus
    held then (`HelpBeyond`: nothing is beyond this machine, a repository's own workspace is near), each row offering *Make …
    the focus*; a shell command is never parsed. The focus is chosen
    in the chip that leads the panel's composer bar, a picker of the window's place, *Recent*, this machine and the other
    workspaces with their repositories (`help/focus/FocusPicker.tsx`), the chip saying a focus the driver widened from a place
    gone until another is chosen, and named under the conversation's title as a
    breadcrumb marking a focus outside the window's workspace, a home that is not the window's, a change waiting and a place
    gone (`FocusCrumb.tsx`). A choice is sent at once (`HELP_FOCUS`). Chosen before its first words the focus is its first,
    carried by the start where none was opened ahead, and an agent opened ahead under another place ends at once so the page
    opens it ahead again (`reopens`); chosen after, it waits for the next words: a running agent ends at its turn's end, or
    now if idle, as for an access change, and the words resume it under the new focus, a gone place widened, the agent told
    in one line ahead of them. A new conversation takes the window's workspace, this machine in a window on every workspace,
    which asks nothing; one from *New conversation* in the head takes the shown one's applied focus. A conversation of
    another home goes on where it is shown, the window unmoved. (from D89, D170, D183, D158, D253)
43. Its conversations stay on this machine. Only those the person spoke in or named are listed, pinned first, then newest,
    every workspace's, one workspace's, or those about a place: homed there or inside it, or that held a focus there. The
    panel's *Showing* chip chooses among them, *This workspace* marked as the window's and each workspace unfolding onto its
    repositories by the focus picker's keys (`useFolds`): this workspace's by day, the others grouped by home with the window's first,
    a row's focus chip only where its focus is not its home. A row carries the messages that changed its focus (`marks`),
    and the transcript draws a divider above each from them, *Focus widened* where the driver widened it. The palette's
    *Ask Daoris: Change focus…*, the Help menu's *Change focus…*, opens the picker. They resume by kept id. A briefed continuation stays one history row, a replacement of its refused start included. (from D158, D189)
44. A new Ask Daoris conversation started from an earlier one is handed Daoris's brief of that one's line, why `press`, ahead
    of the person's first message, and keeps it as Daoris's first prompt. A repository's *Start a conversation with these
    words* carries the words alone. A start its agent refuses at the open, before a word took the brief, hands it to its
    replacement (`ChatPending.Unhanded`). Words a stop or a taken-back withdraws give back what they were composed with
    (`ChatMessage.Untaken`), as a message no turn took does. (from D158, D233)

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
- **Ask Daoris:** history in `Context/HelpConversations.cs`; a door's answer, where one is wanted, a route's view in
  `Modules/Platform/Views/` with its row in the room's doors table.
- **Never grow the hubs.** `Course/Chat/ChatGoOn.cs` (37 KB), `Loop/Chat/ChatRunner.cs` (37 KB) and `Loop/Chat/ChatRun.cs`
  (31 KB) stand near the 40 KB source budget and do not grow: `ChatGoOn` takes new go-on behaviour in a unit beside it,
  `ChatRunner` only wires and `ChatRun` is the one run. A conversation's turns grow in their parts under `Loop/Chat/Turns/`,
  never in `ChatTurns.cs`, which is their door. New behaviour goes in its own file (D231). Agents comes before Participants and
  Context in the structure gate (D166).
- **Tests that pin it:** `LineCourseTests` (the pinned path table), `CourseCensusTests` (`fixtures/course-sites.json`),
  `CourseBoundaryTests`, `CoursePortsTests`; `ChatGoOnTests`, `BriefedMoveTests`, `LimitWaitsTests`, `LimitMoveStoreTests`, `ChatLimitCarryProcessTests`,
  `CurveMoveTests`, `ChatHandOverTests`, `HandOverTests`, `PartLeftTests`, `HandOverLeftProcessTests`, `MoveStoreTests`, `MoveStoreCensusTests`, `MoveDeliveryTests`; the
  `*CrashProcessTests`, `*DowngradeProcessTests`, `*OlderBuildProcessTests` per move; `ContextBriefTests`,
  `StandingBlockTests`, `InstructionAccountTests`, `CourseCountsTests`, `LineCountsTests`, `LineKeysTests`,
  `AnswersAloneHoldTests` (all in `Daoris.Desktop.Driver.Tests`); `Loop/ChatGoesOnProcessTests`, `Loop/ChatTurnsTests`;
  `DriverModuleConversationTests` (modules); `LineEventsShapeTests`, `LineEventFilesTests` (service);
  `ConversationView.test.tsx`, `conversation.test.ts`, `HandedAccount.test.tsx`, `locales/say.test.ts` (web).
- **Gates that bite:** `driver`, `censuses`, `driver-process` (moves' crash, downgrade and older-build runs), `modules`,
  `service` (for `line_read`), `web-unit` (en and zh twins), and `universal` (structure gate, size ratchet, generated index).

## Contradictions

1. D189 point 2 ("the old part ends at its turn's end, never mid-turn") vs `HandOverOf` in `Course/Domain/LineCourse.Decide.cs`: the
   person's hand-over of a quest's session moves now, as their stop. The code reads right (D163 point 1).
2. D137 point 4 and D158 point 3 (a chat, or one with no kept id, waits for the person's new conversation) vs
   `ContinueReason.Lost` in `Agents/Domain/ContinueReason.cs` and `Judged` in `LineCourse.Decide.cs`: `unkept` and `gone` count
   as lost, and the line goes on by itself, briefed. The code (and D189 point 3) reads right.
3. D137 point 7 (a cooling account holds the words, with *Go on in a new session* offered) vs the `Elsewhere` cases of
   `LineCourse.Decide.cs`'s `Judged`: a conversation's words switch off a cooling account now, and *Go on in a new session* is a driven record's alone.
   The code reads right.
4. D125 point 4 ("a running session never rotates") vs D165 point 3 and `Course/Chat/CurveMove.cs`: a conversation moves at
   its turn's end. D165 reads right.
5. D80, D125 point 4, D131 point 3 and D163 point 1 (a new part gets the request, last plan, last words and what is
   uncommitted) vs D189 point 4 and `ContextBrief.Compose` (`Context/Domain/ContextBrief.cs`): every new part gets the brief. D189 reads right.
6. D163's MIXAGENT1a-i note point 1 (the ask is the box `<id>.hand-over.json`) vs `HandOver.JudgedAsync`'s summary in
   `Course/HandOver/HandOver.cs`: a move row in the store; boxes are imported only. The code reads right.
7. D206's body cites `Conversations/Chat/ChatGoOn.cs`. The driver has no `Conversations` folder; the unit is
   `Course/Chat/ChatGoOn.cs`. The code reads right.
8. D188's status line says *proposed*. Its own note (`docs/decisions/D188.md:178`) says accepted and built by COWORK1a-d.
    The note reads right.
9. The archived LIMITGOON1 entry (`docs/task-archive.md`) says a limit move goes on "in the same conversation". D189 points 1-2 and
    `LimitMove`'s summary make it a new part handed the brief. The new part is right; the wording mixes up the line and the agent's conversation.
10. SESSCTX1e's re-cut (*Go on fresh* as a hand-over to the same agent and account) vs `HandOverOf` in `LineCourse.Decide.cs`, which
    refuses exactly that (`HandOver.SameAgent`). The code is today's; the re-cut must change the refusal when it is built.

## Open

- **SWITCHCTX1a onward: the account carry.** A move keeps the agent's own conversation by carrying it to the other
  account's home. Proposed (`docs/2026-10-24-switch-context-design.md`, its round 5 read and another round owed), with no backlog row. A driven line's
  limit move and a conversation's carry through the providers' `IConversationCarry` (`Loop/Run/Hand/LimitCarried.cs`, `Course/Chat/ChatLimitCarry.cs`);
  the curve's move and the hand-over carry nothing; a carried resume the agent refuses after the copy is briefed on the same part, the copy removed.
  A carried move cut off by a restart before its new part ran goes on in the carried conversation: a chat's new part is bound to
  it as its record opens (`ChatLimitCarry.Bound`), so its go-on resumes it, and a driven start continuing a limit part that never
  ran plans the carry again from the part the limit ended (`CutLimitPart`); a copy gone by then is briefed, saying why.
  `Course/MoveRecords.cs` reserves the `carry` kind and the `copied` and `verified` phases.
- **SESSCTX1a:** resume and a fresh part measured on each door. No brief has been counted on a harness.
- **SESSCTX1e (ONEWAY1j):** *Go on fresh*, Daoris's compaction on the person's press, re-cut as a hand-over to the same
  agent and account, after SESSCTX1a is measured.
- **LIMITGOON1 (STAFFPOLICY1 row i):** the limit move seen on the installed window, among that row's installed proof.
- **LIMITRESUME1 (ONEWAY1g row f):** the installed proof, one session run to its limit once. A turn whose last words are its
  account's limit already concludes limited on both doors.
- **CHATFLOW1:** a conversation's work in its own tree runs its repository's workflow; left, the installed look (SETUPUX1x).
- **RPC1g:** a terminal's conversation is the running Daoris's. The design (`docs/2026-11-06-terminal-conversation-design.md`)
  is accepted (D238; g5, seen on the install, is left): one runner, a terminal holding one conversation over the pipe under one Confirm, nothing alone with no instance.
  The pipe holds a conversation for one connection (`src/Daoris.Desktop/Daoris.Desktop.Modules/Platform/PipeHolding.cs`),
  and a held conversation's head says *Held by a terminal*. `daoris chat --repository` starts one through `START_CHAT` with
  `hold: true` and `--session` holds one open; `session start-from` and `session hand-over <id> --agent` hold what they open
  (`HeldVerbs`, `HeldChat` and `HeldDoor` in `Daoris.Desktop.Driver/Instance/`, wired by `Driver.Host/HeldChatConsole.cs`). A line, `:attach`,
  a turn stop and the end of input are held requests; Ctrl+C at rest lets go; with no Daoris running they refuse and run nothing.
  `ask-daoris resume` is gone (`session say`). No terminal path runs a conversation: `ChatConsole` only hands to
  `HeldChatConsole`, the instance's loop makes the one `ChatRunner`, and the terminal's turn marks, their owner and the request
  folder's watch for them are gone, so a window reads every conversation's queue from the instance alone. A `*.turn` mark an
  older build left is removed once at the start by the home migration's `turn-marks` step
  (`Daoris.Desktop.Driver/Transitional/Migration/TurnMarksStep.cs`). `TerminalConversationCensusTests` holds all of it.
- **MSG1h, MSG1i, STEER3:** next-step words on Codex's and the native doors; other steering measured.
