# One conversation under every chat, driven session and Ask Daoris: a regular assistant's chat that outlives its processes and its agent (CONVO1)

**Decided 2026-11-12 (D280), round 2.** The direction is the backlog row CONVO1 (`TASKS.md`, *Conversations and context*), on the
owner's words of that day: rework the foundation of chat and session so it works like a regular LLM chat app, or can run a workflow
like one, carrying on across account and agent switches; sessions and Ask Daoris share it; Ask Daoris is a regular assistant for all
of Daoris, where today what it can and cannot do is a mess. Written by Claude Opus 5.5 on 2026-11-12 from main at `25c4105bba`;
ASKCHAT1's first part merged after it, at `7877692166`, and §1, §5.3 and row a say what it built. Revised the same day from main at
`1ee777665e` after Codex's independent read (gpt-6.1-sol, D191), whose fourteen findings were each checked against the code and
treated in §10: taken into the model, or answered there with the evidence. Read with `docs/areas/conversations-and-context.md`,
`docs/areas/agents-and-harnesses.md`, `docs/areas/roles-and-supervision.md` (rules 54-62), D188 and its note of 2026-11-12, D189,
D206, D253, D273, D277 and its note, the switch-context design (`docs/2026-10-24-switch-context-design.md`) and the rows ASKCHAT1,
ASKPUSH1, CHATUX1, SWITCHCTX1 and SESSCTX1. A root-cause read of two days of the owner's own Ask Daoris use in the work workspace
(kept off the tree, since it names private work) gives §1's failures. Nothing here is built but ASKCHAT1's first part. Driver paths
start at `src/Daoris.Desktop/Daoris.Desktop.Driver/`, module paths (*modules'*) at `src/Daoris.Desktop/Daoris.Desktop.Modules/`,
service paths at `src/Daoris.Service/Daoris.Service.Core/`, web paths at `src/Daoris.Web/src/`.

## 0. In short

- **Most of a conversation exists; it is not yet one thing.** Daoris keeps every word, answer, tool call and note on its records, one
  record per harness conversation, linked into a line by `from`. It resumes by kept id, carries a conversation to another account of
  the same agent, hands Daoris's brief to another agent, and queues, steers and stops words. What it lacks is the conversation as the
  one thing the person talks in. A record's end reads as the conversation's end, and the person's *Stop* ends it for good. Until
  ASKCHAT1's first part, Ask Daoris's conversations shared one answering slot per workspace and a new conversation could be handed to
  a running one. The agent changes only by a hand-over that refuses Ask Daoris.
- **The model** (§2). A conversation has an id of its own, fixed when it opens and written on every part's record, so one read gives
  its parts to sending, reading, the brief and a workflow alike. Its parts are harness conversations, its runs are processes, and its
  messages are its parts' events read as one. It outlives every run and every part. Its state is read from what is kept, never
  stored as a state: *replying*, *opening*, *ready*, *waiting*, *archived*. A word is kept before it is acknowledged. *Stop* ends the
  reply and holds what waits until the person sends; the next word continues whatever ended.
- **The agent and the account are attributes** (§3). They change between turns, by the person or by a limit, applied before the next
  words are taken. The conversation goes on the first way that holds: the same part resumed, its harness conversation's files carried
  to another account where the provider declares it for the version that runs, or a new part handed Daoris's brief. Each way is said
  with what it kept and lost. The switch is measured on the installed doors twice: what is feasible before it is built (row e), and
  what it keeps once built (row h).
- **A driven session is a conversation a workflow speaks in** (§4): its brief the first words, its steps' words messages from their
  senders, its review and landing what follows a turn's end. A stopped reply, a workflow's pause, finished work and a closed quest
  stay four things. Every door that holds a conversation across turns runs it on the one run; a door that holds none runs a turn per
  run, said.
- **Ask Daoris is a conversation placed in its room, with Daoris's tools and the person's delegate** (§5). The access mode alone sets
  who says yes: full access acts as the person, restricted asks on the card. *Always* grants one named family, shown before the yes.
  Five fences hold in both modes, each with the owner that holds it on each path, and said where only the room's instruction and the
  second check hold it. It may ask a read-only helper about a repository. Of its room's 31 special rules, 13 are kept, 12 folded into
  the one rule or the one conversation, 1 changed and 5 removed (§5.3).
- **Records stay where they are** (§6). Today's records are already parts of lines; each is given its conversation's id once. Ask
  Daoris's history file and the archive marks are imported once into the conversation's facts. ASKCHAT1 is the first row.
- **Twelve rows** (§7), each with its proof, its installed sight where it changes the window, and what it removes. **Twelve
  alternatives rejected** (§8). The read's findings and what each changed (§10).

## 1. The failures, and the mechanism each comes from

| What the owner met | The mechanism | Under CONVO1 |
|---|---|---|
| A word could not reach Ask Daoris while one tool call ran 20 minutes | The installed build had no *Send now*; a word waited for the turn's end (D188 point 3) | *Send now* steers or stops the turn and goes first (ASKPUSH1, built); a call past its bound is said with *Stop* beside it (ASKCHAT1) |
| A stop ended the conversation for good, and the word waiting was lost | Ask Daoris's *Stop* is `STOP_SESSION`: the process ends, the record is `stopped-person`, and "a person's stop keeps no word and never goes on" (conversations rule 13); the process-gone path takes the waiting words off on the person's stop (`Loop/Chat/Turns/TurnHolds.cs:288-293`) | *Stop* ends the reply only; what waits stays, held until the person sends; the next word continues every end |
| A new conversation's first word went into the running one | `START_HELP` answers with the workspace's running conversation, "is carried on" (`Bridge/Routes/HelpRoutes.cs:58-65`), and the page hands it the words (`help/ask/useHelpSend.ts:267-273`), at `25c4105bba` | A new conversation is always new: built by ASKCHAT1's first part |
| One conversation's turn held another's words | One Ask Daoris conversation ran per workspace: `ChatHelp.SetAsideHelpAsync` (`Help/ChatHelp.cs:102-116`) stopped the others or held the word as `held.answering`, and the ledger refused a second open (`Sessions/SessionOpening.cs:158-167`), at `25c4105bba` | Each conversation answers on its own: built by ASKCHAT1's first part (D158's note of 2026-11-12) |
| A start sat at *Opening Ask Daoris* nine minutes after the room's tool server stopped, until a restart | The opening has no bound of its own: the pump waits on the door's readiness (`Loop/Chat/Turns/TurnPump.cs:75-84`) and marks a turn in flight only after it (`:121`), so the keeping stop finds no turn and does nothing (`Loop/Chat/Turns/TurnStops.cs:21`); a protocol request waits on the token it is handed (`Agents/AcpWire.cs`) | *Opening* is a state of its own, each phase bounded, *Stop* reaching it, a server that does not come up said with its reason and the words kept (§2.8, row a) |
| A new conversation knew nothing of the one before it | A new conversation is handed nothing of another, and the room names no recent conversation | The room lists the recent conversations and Ask Daoris reads one with `line_read` when the person points to it; a branch is handed the brief (§2.6) |
| What Ask Daoris may do read two ways | The room says full access presses as the person (`Knowledge/Room/HelpRoomMayDo.cs:38`) and that seven workflow presses are *yours alone* (`Knowledge/Room/HelpDoorRowsActs.cs:144-181`); full access hands no checkout and every server whole (`Help/HelpPlacement.cs:59-69`) | One rule, its five fences named with what holds each (§5) |

## 2. The model

### 2.1 Four nouns

| Noun | What it is | Its id | Today |
|---|---|---|---|
| **Conversation** | What the person talks in and sees as one: a repository's chat, an Ask Daoris conversation, a driven session, a helper's or a reviewer's | fixed when it opens: its first part's id, the quest's for a driven line; written on each part's record as `line` (§2.2) | the line, keyed by a walk back (`LineKeys.Of`), which only the course reads |
| **Part** | One harness conversation: one agent, one account's home, one kept harness id, one place | the record's id in the ledger | the record (conversations rule 1) |
| **Run** | One process serving a part for one or more turns | the process | `ChatRun` over `TransportRun`; a driven native run serves one turn |
| **Turn** | Words the agent took and its reply, up to the door's end of turn | the part's id and the position in its event file of the words it took: durable across runs | `TurnState.Turn`, one run's count rebuilt at each start (`Loop/Chat/Turns/TurnState.cs`), and the `turn` event |

A conversation has one or more parts, linked by `from` with a why (rule 3); a part has any number of runs; a run serves one or more
turns. A conversation outlives every run (a restart, a crash, an idle release, a stop past its bound) and every part (a limit, a
switch, a harness conversation lost). The entry key (`SessionEntries`, `<origin>/<repository>`) keeps its job: it groups the
conversations that serve one ask or quest in the window's list, and is never a conversation's id.

### 2.2 Identity and membership

- **The id never moves.** It is set once, when the conversation opens, and each part's record carries it as `line`, set at the
  part's start: the `line` of the part it continues (`from`), its own id where it continues none, its quest's for a driven part. No
  walk recomputes it, so a conversation longer than any walk keeps its name, pins, archive mark and branches (today's walk keys a
  line past 256 hops to the current part, `Course/Domain/LineKeys.cs:25,40`).
- **Membership is one read**: the records held here whose `line` names the conversation, in the order they were created. Sending,
  reading (§2.3), the brief, the course's counts and a workflow's admission read it and nothing else. Today two walks disagree:
  `LineKeys.Members` joins a driven resume with no `from` by its quest, while the brief's `ContextBriefs.LineOf` follows `from` alone
  and stops at 50 parts (`Context/Events/ContextBriefs.cs:20,29-42`); both give way to the one read.
- **`from` and its why stay** each part's link to the one before, read for the boundary notes between parts.
- **History not held here is said.** A part whose `from` names a record this machine does not hold (a teammate's, a deleted one)
  starts the conversation's messages, under a note that earlier parts are not held here; the brief names the same in its cuts.
- **A workflow's run is not the conversation.** A chat's run is the work of one tree on it (`ChatRuns.KeyOf`, `chat-<first part in
  that tree>`, `Workflows/ChatRuns.cs:76-86`), so a conversation whose tree is replaced holds a new run; a driven conversation's run is
  its quest's. Nothing keys a run by the conversation alone.

### 2.3 Messages

A conversation's messages are its parts' events in the order the membership read gives (`SessionEvents`,
`<part>.events.jsonl`). Nothing new stores them.

| Kind | What | Read from | Who says it |
|---|---|---|---|
| **words** | Words given to the agent, with their attachments and level | `user` with its `Origin` and `From`; a word kept on `said` | the person (verbatim); Daoris (a restart's word, a reset, a go-on); the workflow (a start's brief, a send-back); a co-worker's claim; a helper's answer; an opinion's findings. Each is named, and only the person's are the person's (D188) |
| **answer** | The agent's text, its thinking folded inside, its plan | `message`, `thought`, `plan` | the part's agent |
| **tool** | A call: kind, title, files, status, output bounded | `tool` | the part's agent |
| **note** | Daoris telling the person about the conversation: a stop, a switch and its figures, a limit, a card, a receipt, a focus change, a part's end, the turn's usage | `note`, `receipt`, `transient`, `ended`, `usage` | Daoris |

**A word is acknowledged only once it is kept**: on the part's record (`said`, rule 12) with its attachments by reference, its sender,
its level and the press's request key, so a press repeated is kept once; where the record does not keep it (the service not
answering), in the desktop's held-words file (`HeldWordsDisk`) until it does. Never in memory alone, before a harness exists
included: Ask Daoris's starting list (modules' `Loop/HelpStarting.cs:24`, which keeps text and files in a dictionary and no key or
level) and the live queue's *waiting here alone* (`Loop/Chat/Turns/TurnKeeps.cs:16`) go. A word nothing can keep is refused, said,
and given back to the composer with its files.

**Each word shows its delivery**, read from the record and the events by its id: *waiting* at its level; *held*, by the person's
stop or a hold of §2.5; *taken* in turn *n*; *withdrawn* by the person; or *uncertain*, a word a run was writing when it was cut,
which the agent may have read (`UncertainWords`' `lost`), offered again and never sent again by itself. Nothing drops a word. A stop
holds what waits, a restart keeps the word (rule 13), and a start that fails gives back what it was composed with, its files
included (rule 44; today's fallback keeps the text alone, modules' `Bridge/Routes/HelpRoutes.cs:90`).

### 2.4 Turns

A turn opens when the agent takes words, from any sender, and closes at the door's end of turn. It ends *answered*; *stopped* by the
person; *failed*, said with *Retry*; *limited*, when the conversation moves or waits (§3); or *cut* by a restart, when it goes on by
itself with Daoris's restart word (rule 13). A turn the person stopped stays the person's stop when Daoris ends its run at *Stop*'s
bound: it is never a cut and never goes on by itself. Every outcome leaves the conversation ready or waiting, never ended.

### 2.5 States, read from what is kept

| State | When | The composer |
|---|---|---|
| **replying** | a turn is in flight | *Stop* is the primary press; *Send* queues at the word's level; *Send now* |
| **opening** | a run is coming up for the next turn (§2.8) | the phase it is in; *Stop*, which ends the opening and holds the words; past a phase's bound, its reason and *Retry* |
| **ready** | no turn in flight and nothing holds the next; a run may be warm or not | *Send* |
| **waiting** | something other than the agent holds the next turn | *Send* keeps the word for when it frees; the first hold's own press beside it |
| **archived** | the person archived it and nothing in it needs the person | hidden from the list; a word brings it back, and so does anything coming to need the person (a card, a failure, work to review), shown in that group with its mark. A conversation replying or needing the person is not archived (`Workplace/SessionArchive.cs:17-19`'s rule, kept) |

No state is stored as a state. What is kept is the intent that outlives a process, and the state is read from it: the person's stop
holding the words that wait (on each word, on the record); a switch asked and not yet applied (its revision, in the conversation's
facts, §3.1); a card (its store); a move (`MoveStore`); a limit with nowhere to go; a workflow's hold (its run). Several holds can stand
at once. The next turn runs when none does, and the composer shows the first, with its press, in this order: a card for the person;
the person's stop (*Send* releases it); a limit with nowhere to go (its reset named); a move or switch being applied; a workflow's
hold; the opening.

The parts' ledger states (`working`, `completed`, `failed`, `stopped`, the `PartEnds` codes) stay each record's history and the
course's facts. No part's end is the conversation's end. A delete, and a clear of finished history, remove a conversation, whole (§6).

### 2.6 Operations

| Operation | What it does | Today | Under CONVO1 |
|---|---|---|---|
| Send | Keeps the word (§2.3), then a turn takes it: at once when ready, at its level when replying (D188), when it frees when waiting; with words the person's stop held, it releases them into the same turn | `SESSION_INPUT`; Ask Daoris's first word through `START_HELP` | one press; a new conversation's first word opens it, never another |
| Send now | The person's own: into the running turn where the door steers, else the turn stops and the word goes first, said | ASKPUSH1 (`NativeSteers`, `AcpSteering`, `TurnCutIn`) | kept, on every kind, driven included (row k) |
| Stop | Ends the reply: the door's interrupt; a turn still in flight 10 s later has its run's process tree ended, the kept id kept, said (§2.8). What waits stays, held: nothing is taken until the person sends. During an opening, it ends the opening | three presses: *Stop turn* (`CANCEL_TURN`, whose keeping stop lets the waiting words start the next turn at once), *Stop* (`STOP_SESSION`), *Finish* (`END_CHAT`) | one press |
| Continue | The next word after any end goes on: the part resumed by kept id where it holds, else a new part handed the brief, saying why | rules 13-15; refused after the person's stop | every end; refused only for a teammate's record, which is read only, and in a kind that takes no word (§4) |
| Retry | The last turn's words said again as a new turn; the agent sees its first answer | *Try again*, *Say it again* | one press with those two wordings (CHATUX1d) |
| Edit | A waiting word is edited in place. A sent word is never rewritten. *Edit* on the last turn's words puts them in the box for a new turn; on an earlier turn's words it opens a branch cut before them, the edited words its first | `SESSION_QUEUE_EDIT` | kept, with the branch |
| Branch | A new conversation from a turn of another. *New conversation from here*, on an answer, takes the turn, words and answer; *Edit*, on a turn's words, takes only the turns before them. Handed Daoris's brief of the conversation cut at that boundary, reading no later event; the place, the waits and the memory as they stand now, named so; the attachments it names copied to its first part. The first untouched | `HELP_START_FROM` (Ask Daoris, the whole line), `SESSION_START_FROM` (the words alone) | one press from any turn, recorded as `branched`: the conversation, the part and the event boundary |
| New conversation | Always new, on the choices the composer shows | `START_HELP` may carry on a running one; `START_CHAT` | always new |
| Attach | A file with a word, kept in the home's attachment folder of the part that took it and named to the agent by path | `ChatFiles`, readable by that part | every part of the conversation reads every attachment of it; a branch copies those its brief names |
| Switch | Agent, account, model, effort, access, focus (§3) | `SESSION_OPTIONS`; `SESSION_HAND_OVER`, refused for Ask Daoris and for the same agent and account; the mode's swap; `HELP_FOCUS` | one attribute path, on any conversation |
| Rename, pin, archive, delete | The list's | `HELP_RENAME` and `HELP_PIN` for Ask Daoris alone; `SESSION_ARCHIVE` per record; delete | one set for every conversation, on the conversation |

### 2.7 What is stored, and where

| What | Where | Kept by |
|---|---|---|
| Messages and turns | the parts' event files and verbatim transcripts, on the machine (rule 5) | `SessionEvents` |
| Parts: state, `from` and why, `line`, kind, agent, account, place, kept harness id; words waiting and held (`said`), each with its sender, level and request key | the service's ledger | the service |
| Words no record could keep yet | the desktop's held-words file | `HeldWordsDisk` |
| The conversation's own facts no record holds: name, pin, archive and its time, home, focus and its marks, `branched` (from which conversation, part and event boundary), the person's pick of agent, account, model and effort, and a switch asked and not yet applied (its revision) | one table in the desktop store, keyed by the conversation's id | a new `ConversationFacts`, replacing `HelpConversations`' file and `SessionArchive`'s `archived.json` |
| Moves | the desktop store's `moves` (rule 10) | `MoveStore` |
| Attachments | the home's attachment folder of the part that took each, a branch's copies in its first part's | `ChatFiles`, `SessionAttachments` |
| The agent's own conversation | its account's home, the harness's format | the harness; Daoris copies it only in a carry (rule 6, §3.2) |

### 2.8 Runs: a conversation outlives its processes

- **Opening.** A turn that needs a run and finds none warm opens one: it resumes its part by kept id, a go-on with no block and no
  brief (rule 13), or opens a new part (§3). The opening's phases are said as it goes, each with a bound: the place made ready (the
  room written, a tree); the servers it is handed up; the harness started and its conversation opened or resumed (`initialize` and
  `session/new` or its resume on the protocol door, the first event on the native door). A server Daoris's own work needs (Ask
  Daoris's connector, a repository's knowledge server) not up within its bound fails the opening; a plugin's server not up is left
  out, said, and the opening goes on. A phase past its bound fails the opening with its reason, its process tree ended, the words
  kept and held with *Retry* beside them. *Stop* during the opening ends it the same way. Each bound is 60 s to start with, moved on
  row e's numbers; a harness's own server-start hint (Codex's 15 s) is used where it has one, never as the bound.
- **Warm.** A run with no turn stays up while its conversation may speak again. At most a bound of warm runs is kept, four to start
  with: the least recently spoken is released first, one the window shows last. Nothing is released on a clock (driving: *nothing is
  ended on a clock*). Nothing is released while its job holds a process the run did not start with: the servers it was handed are the
  run's own, and anything else (a background shell, a build) holds it. A monitor or a scheduled prompt a harness keeps inside its own
  process is not seen by the job (`PartLeftovers` counts the job's processes, `Course/HandOver/PartLeftovers.cs:44`): where the door
  reports one the run is held, and where it does not, a release says what it may have ended. Held runs are counted apart from the
  bound and listed with *Stop* beside each, so work held is seen and never releases another run early. The bound is set from row
  e's figures.
- **Release.** End of input on the native door, `session/close` on the protocol door. The part ends `chat.closed`, as a shell's close
  between turns already ends one, and the conversation is ready.
- **Stop's bound.** The door's interrupt first (the native control request, the protocol's `session/cancel`). A turn still in flight
  10 s later has its run's process tree ended. The bound's timer is taken for one run and one turn, so a late timer that finds them
  gone ends nothing of the next. The part ends as the person's stop (`stopped-person`) and keeps its id; the next word resumes it,
  said. It never enters the restart's go-on (rule 13), and what waited stays held: the process-gone path keeps it on the person's stop
  as on any other end. A tool's server outside the tree, the in-app browser's, is told the cancel by the harness; a server that
  ignores it is held to its declared call bound (ASKFIX1f).
- **Restart.** Conversations rule 13 as it stands: a cut turn goes on by itself, and a ready conversation stays ready. A turn the
  person stopped is not cut.
- **Many at once.** Each conversation's runs are its own. Nothing serialises across conversations but an account's limits and the
  line's own go-on admission (`ContinuationAdmissions`).

## 3. The agent and the account are attributes

### 3.1 The attributes

| Attribute | Chosen at the start by | Changed by | Takes effect |
|---|---|---|---|
| Agent (its door) | the role's order (`ask-daoris`, `chat`, the quest's role), or the composer's pick | the person between turns; Daoris at a limit, the curve or a rule's move | the next turn |
| Account | the walk, or the person's pick, never moved off (rule 21) | the person; a limit; the curve | the next turn |
| Model, effort | the role's choice within the agent's offer | the person (`ChatOptions`) | the next turn, the same part: on the protocol door through its session's options; on the native door, whose choices are spawn arguments (`Agents/Providers/ClaudeCode/ClaudeCodeSpawn.cs`), by releasing the run and resuming the part with them |
| Access | Ask Daoris: the mode; a repository: its access setup (D253 point 6) | the person | the next turn, the same part resumed under it, its rules, servers, guard and delegate key handed again (ACCESSSWAP1) |
| Focus (Ask Daoris) | the window's workspace | the person, in the composer | the next turn, the same part, handed with the turn's words, never written into the room's shared files (row a) |
| Place | the start: a repository's tree, Ask Daoris's room | never; a tree gone opens a new tree in a new part, briefed | the next turn |

**A change is a revision.** The latest the person asked is kept on the conversation's facts, and the run takes its next words only
after applying it: where the run's equipment differs, the run is released and the part resumed by kept id under the new attributes,
so words queued before the change run under it. The latest asked wins; a change back before it is applied applies nothing. A change
asked while a turn runs applies at the turn's end, or at the person's *Stop*. A restart during an application applies it at the next
opening, from the facts. A limit is met at a turn's end. A change before the first answer keeps the word (ASKFIX1g). Today's access
swap waits until no turn runs and nothing waits, so the turns already asked for run under the old access (`Help/HelpAccessSwap.cs:19-20`);
it and the focus change's own ending give way to this one path.

### 3.2 The ways on, tried in order

| Case | The way on | What the agent keeps | What is lost | What is said |
|---|---|---|---|---|
| Same agent, account and place: model, effort, access or focus changed | the same part, warm or resumed by kept id | everything its harness kept | nothing Daoris takes; a harness compacts as it decides | a note: *Model: …*, *Access: …*, *Focus: …* |
| Same agent, another account | the part's harness conversation carried to that account's home (`IConversationCarry`), where the provider declares the carry with evidence for the harness version that runs: whole at or under the provider's bound; past it, carried where the harness compacts on its own at resume, else briefed; a size not read is said unread and taken as past the bound | the conversation's files the provider's carry copies: Claude Code's conversation file and the folder beside it, with its stored tool results and subagents; Codex's rollout file | what those files do not hold: the account's own memory, settings and other state; past the bound, what the harness's compaction drops | `session.moved` with its case and figures: *carried whole*; *carried, compacted by the agent* only where the harness's events show the compaction, else *carried*; *briefed*, with why |
| Another agent | a new part handed Daoris's brief (D189) | every word, the person's verbatim to their caps (each word up to 4,000 weight, all together up to 12,000: `Context/Domain/ContextBrief.cs:40-67`) and others' as claims; each turn's last answer and one line of what it did; the plan; uncommitted paths and commits; the last failed check; attachments by path; the outgoing agent's note; the other parts; the course | thinking; tool output but the last failed check; what files said when read; answers before each turn's last; its subagents' work; its own todo and memory; a word's tail past its cap; past the bound, the oldest turns and then the oldest words; parts not held here; attachments no longer on disk | a note naming the agent, that the brief is Daoris's record and not the agent's memory, its size and every cut by name (`HandedCuts`), parts and attachments missing included; *Started with* opens it |
| Same agent and account, fresh (*Go on fresh*, SESSCTX1e) | a new part handed the brief | as another agent | as another agent | as another agent |
| Nowhere to go | the conversation waits | everything | nothing | the reset and the account it waits for |

**The carry's contract.** The source part has no run while its files are copied. The destination conversation is new to that home
and the carry's own: a copy that would overwrite a file there is refused and briefed instead (today's copy overwrites,
`Agents/Contract/CarriedFiles.cs:10-11`). The files are copied under a staging name and committed by one rename, the phase kept on the
move, so a restart completes a copy or removes it; a fallback removes only what the carry itself wrote. The note says the way that
ran, never the way planned (the compacted case today is chosen from a setting, `Course/Domain/LimitCarry.cs`, not read from the
harness). A provider that declares no conversation home (dsh, `Agents/Providers/Dsh/DshProvider.cs:50`), a plugin's door that
declares none, and Codex past its bound (it declares no way to ask for an early compaction) are briefed.

Ask Daoris takes the same ways on. Its room is its place, so a carry needs nothing more; today its limit move is briefed only (rule 20).

### 3.3 Sizes, and what is measured

**Measured** (D206's note of 2026-11-08, on the install, Claude Code). A briefed part starts at about 68,000 tokens: its system text,
tools, standing block and brief together. Carrying 155,233 tokens whole costs about 87,400 more cached tokens at the first request, and
their reread after. Carrying whole is cheaper below about 170,000 to 200,000 tokens, so Claude Code's carry bound is 200,000 tokens
(`IConversationCarry.WholeBound`); Codex's stands at the same until row h measures its own. The brief's own bound is 40,000 weight, a
Latin character weighing 1 and a Han one 3 (rule 32): about 10,000 to 13,000 tokens of English. A first prompt fits 32,767
characters (rule 36). STEER3 measured where each door takes a word mid-turn (`docs/2026-10-03-steer-evidence.md`).

**Not measured.** Whether a resumed run's first request reads its context from the provider's cache or writes it again (SESSCTX1a,
owed on both doors). Whether each harness version that runs accepts a carried copy. What a briefed part recalls against a resumed
one: no brief has been counted on a harness. A warm run's memory, an opening's seconds and a resume's time to first words, on each
door: these set the warm bound and the opening's bounds. How long each door takes to stop a turn, which sets *Stop*'s 10 s.

**Measured in two sittings**, on the installed doors, in HELPMODE1a3's spend sitting, never by a model's judgement; each figure with
the build, the door, the harness version, the account kind and the model it ran on. The doors are Claude Code native, Claude Code over
the protocol and Codex over the protocol; dsh and the layer are named as unsupported for a carry and a duplex.
- **Row e, before the switch is built** (what is feasible): a resume's cache read and write; a carry's copy accepted by the version
  that runs, per provider; an opening's seconds, and its failure with a server that never comes up; *Stop*'s seconds; a warm run's
  memory with its ordinary servers and idle children.
- **Row h, once it is built** (what it keeps): every way on (resume; carry whole; carry past the bound; brief from Claude Code to
  Codex and from Codex to Claude Code), each with the context before, the first request's input, cache read and cache write tokens
  after, and the brief's weight and cuts. Recall: ten facts the record holds (a path, a number, a choice the person made, a failed
  check's name), written from the record before the switch and asked after it, each scored by its exact string in the answer; and four
  continuation tasks scored against a fixed answer: a restriction the person gave kept, a request left unanswered recognised, another
  agent's claim told apart from the person's words, and a fact the brief cut recovered by reading the conversation (`line_read`). Per
  provider, model and version: one provider's economics do not set another's bound.

The bounds, the brief's sections and the warm bound move only on these numbers.

## 4. Sessions on it

A driven session is a conversation a workflow speaks in. Its first words are the quest's start prompt, from the workflow. Its steps'
words are messages from their senders: a review's *Ask for changes* is the person's, through the review; a hold's answer and an
opinion's findings are their senders'; a carry-on's and a limit's resume words are Daoris's. Its review, its landing and its quest's
done follow a turn's end, between turns, as CHATFLOW1 already reads a chat's work. The person reads it and talks in it as in any
conversation: *Send* at a level, *Send now*, and *Stop*, which ends the reply. The work's own stop is the workflow's press, *Pause* on
the line or the quest's stop, and is named so.

**Four things stay apart**, so a turn's end never does what belongs to the work:

| What | What it is | What the workflow does |
|---|---|---|
| A stopped reply | the person's *Stop* of a turn | nothing: no review, landing or done follows a stopped turn; the work stays at its step, and the next turn's end is read as any |
| A workflow's pause | *Pause* on the line, a hold's wait | holds the next turn (*waiting*, §2.5), the words kept for when it frees |
| Finished work | a run whose review, landing and done have followed | finished for good: its landing and its quest's done are each kept by the run and its attempt and never run again; a later word is a new turn whose work is a new run (as `ChatRuns.TakenOf` reads one today) |
| A closed quest | its quest closed | the conversation is read and takes words as a chat; a word never reopens the quest or repeats its done |

**Every door that holds a conversation across turns runs a driven session on the one run**: Claude Code native over its duplex, and
Claude Code and Codex over the protocol. A door that holds none runs a turn per run, each turn a resume, *Send now* a stop and resume,
*Stop* its runner's stop, said on the conversation: dsh, which declares itself not conversational (`Agents/Providers/Dsh/DshProvider.cs:33-36`),
and the layer's `LayerRun`, one turn per process with no protocol words (`Agents/Layer/LayerRun.cs:131-139`). That is the same run
with no warm turn, not a second path; row e names each provider's case, and the layer's duplex is asked of it as a quest.

| Part of the system | Today | Under CONVO1 |
|---|---|---|
| A driven start | The driver composes the target prompt and spawns. A native driven run is one turn under a bare `-p`, taking no person's line (agents rule 11); continuations resume | The driver opens the quest's conversation, or continues it, and says its first words on the one run every conversation uses (row k) |
| Words to a running driven session | Kept on `said` and delivered by `DrivenInbox` at the protocol door's next step or the next part; *Send now* on a native driven run waits (ASKPUSH1's part left) | The conversation's queue and levels (D188), *Send now* as everywhere |
| Workflows | Runs keyed by their work on the line, acting between turns or at an end | Unchanged, read with the table above: a run is its work, not its conversation (§2.2) |
| Quests | A driven line keyed by its quest; a chat may take one | Unchanged |
| Hand-backs | A helper's answer (`ChatHelperWords`), an opinion's findings (`ChatSendBack`), a quest's outcome, each said as a next turn | Messages from their senders at their levels; the answers-alone bound kept (rule 17) |
| Co-workers | Sessions of one entry, posting claims on a channel | Each its own conversation in the entry; a claim is a message at its level |
| Helpers, reviewers, intakes, opinions | Sessions of their kinds | Conversations of their kinds, read by the person; a kind that takes no word has no composer, and with a teammate's record these are the only conversations no word continues |
| Limit, carry, curve, hand-over | One decision (`CourseDecision.Decide`), with executors per loop (`Course/Chat/`, `Course/GoOn/`) | The same decision; one executor per way on for both kinds (row k) |

## 5. Ask Daoris on it

Ask Daoris is a conversation placed in its room, with Daoris's tools and the person's delegate. It reads everything Daoris records, in
every workspace. It does what the person can do in Daoris, at its tier. It talks as any conversation does (§2).

### 5.1 One rule, five fences

**The access mode alone sets who says yes.** Full access acts as the person: each press runs at once, attributed, said once in the
conversation and listed in *Notifications* (roles rule 58). Restricted mode asks: each press goes on the card, the one way to ask the
person (D261), the card keeping what the press concerns (a run's step names its run, node, attempt and revision, REVIEWUX1c's view,
refused `moved` at the press). Nothing is refused by construction (D253 point 5).

**A grant is a named family, shown before the yes.** The card offers *Always* only for a press in one of the grant's families:
`pause`, `clean-up`, `discard`, `settings`, and a checkout's read. It names the family and every verb in it before the person
answers, and Settings lists and takes back what was granted. A press in no family (a role's change, a run's Accept or skip, a delete, a
plugin's install, an ask) is a yes for that press alone. No family reaches a role (roles rule 57), and a new family is a decision of
its own.

**The fences hold in both modes, each by its owner.** Each guards something a yes on a card could not make safe:
1. what the person's own deny rules and the floor refuse (D253 point 2);
2. its own mode and grants, since setting them would raise its own reach;
3. a secret (a token, a password, a device code, a sign-in): it names the field and the person enters it;
4. staging, installing or restarting Daoris, which cuts every conversation, its own included (ASKFIX1b);
5. another repository's files: it asks that repository's work as an ask (`repository-owns-its-work`).

| Fence | Daoris's verbs, on every harness | The harness's own tools, on a harness with a rule language (Claude Code) | On a harness with none (Codex) | A plugin's tools |
|---|---|---|---|---|
| 1 | the route judges the press by the person's rules | the person's denies and the floor handed as rules (`HelpRoom.FullRules`, `Help/HelpRoom.cs:213`) | not held: its standing block and the access setup say so (agents rule 19) | as its harness |
| 2 | a delegate's press of the mode or a grant refused | the home's access record denied, handed with the rules (row b) | instructed; the second check selects a write of it | as its harness |
| 3 | no verb hands a secret; a sign-in is the person's | the accounts' homes and credential files denied, handed with the rules (row b) | instructed; the second check selects a read of one | as its harness |
| 4 | a delegate's press refused | the install's folder and its installer denied, handed with the rules (row b) | instructed; the second check selects a run of one | as its harness |
| 5 | an ask, never a write | restricted: the tree guard keeps writes in the room; full: denied outside the room and Daoris's own home, handed with the rules (row b) | instructed; the second check selects a write outside the room | as its harness |

Where no owner holds a fence, the fence is the room's instruction and the second check's selection after the turn (roles rule 58),
and the room, Settings where the mode is chosen, and the panel say so for the harness that runs, never claiming it held. No
combination of mode and harness is refused for it: a restriction is the person's to choose (D273's rule that a person who wants more
control uses restricted mode or a workflow node), and restricted mode or a harness with a rule language is how the person holds the
fences on its tools.

### 5.2 What Ask Daoris can and cannot do

| Act | Full access | Restricted |
|---|---|---|
| Read what Daoris records, in every workspace: sessions, conversations, quests, asks, runs, settings, logs, the room | at once | at once |
| Read a repository's checkout | at once, every checkout's root named (ASKFIX1c) | a granted checkout at once; another on the card, *Always* granting its read |
| Ask a read-only helper about a registered repository | at once | a granted checkout's at once; another on the card |
| Press any verb the person can press: roles, settings, pauses, clean-ups, plugins, an ask with its requirements and through words, a run's Accept or skip | at once, as the person | on the card; a granted family at once |
| Act on another conversation: say a word, stop a turn, switch its agent, start one in a repository | at once | on the card |
| Its harness's own tools: shell, files, the browser | its harness's unrestricted mode, the fences as §5.1's table holds them | its allow-list; a call outside it held for the person (agents rule 23) |
| A plugin's tools | as the plugin declares them for it (ASKFIX1f) | as declared, each approved |
| Its own mode or grants | never (fence 2) | never |
| A secret, or a sign-in | never: it names the field (fence 3) | never |
| Stage, install or restart Daoris | never (fence 4) | never |
| Write another repository's files | never: it asks (fence 5) | never |
| What the deny rules and the floor refuse | never (fence 1) | never |

### 5.3 The room's special rules: kept, folded, changed, removed

| # | Today's rule | Where | Under CONVO1 |
|---|---|---|---|
| 1 | Its own room per workspace, written from the machine's facts at each start and go-on | `HelpRoom`, `HelpStart`, `ChatHelp.HelpRoomNowAsync` | **Kept**: the room is its place, as a tree is a chat's; its files say no conversation's focus (row a) |
| 2 | One conversation runs per workspace | `ChatHelp.SetAsideHelpAsync`; the ledger's refusal | **Removed** (row a, built) |
| 3 | A new conversation's words carried into the running one | `START_HELP` (`Running = true`); the page's `handTo` | **Removed** (row a, built) |
| 4 | A word held because another conversation answers | `held.answering` | **Removed** (row a, built) |
| 5 | *Stop* ends the conversation; a person's stop never goes on | `STOP_SESSION`; rule 13 | **Folded** into *Stop* that ends the reply (rows a, f) |
| 6 | *Finish* | `END_CHAT` | **Removed** (row f): a ready conversation's run is released by the warm bound |
| 7 | The focus: machine, workspace or repository, chosen in the composer | `HelpFocus`, `HELP_FOCUS` | **Kept** as an attribute (§3.1), handed with each turn's words (row a) |
| 8 | A focus change ends a running agent at its turn's end, or ends one opened ahead at once | `HelpFocusTurn`, `HelpFocusAhead` | **Folded** into the one attribute path (row g) |
| 9 | Its home workspace; history per workspace; a conversation goes on where it is shown | `HelpConversations` | **Kept** as the conversation's facts (row c) |
| 10 | The access mode, machine-wide, chosen only by the person | `HelpMode`, `SET_HELP_MODE` | **Kept**: the one rule |
| 11 | An access change ends running conversations at their turn's end | `HelpAccessSwap` | **Folded** into the one attribute path (row g) |
| 12 | Grants: the act families `pause`, `clean-up`, `discard`, `settings`, and checkouts read | `ActFamilies`, `SET_HELP_GRANT` | **Folded**: the card's *Always*, offered for a named family and shown before the yes, and Settings write the one grant (row b) |
| 13 | A delegate key per start; a press proved by it | `PipeDelegate` | **Kept**, as every session's (D253 point 6) |
| 14 | Restricted: a card per press; full: at once, recorded and listed | `PressViews`, `help.pressed` | **Kept**: the one rule |
| 15 | Seven workflow presses *yours alone*, never asked on a card | `HelpDoorAsk.Never` in `HelpDoorRowsActs` | **Folded** into the rule: pressed at its tier, the card keeping the run, node, attempt and revision (row b; amends D277 point 5's restricted half) |
| 16 | *Done at a terminal*: acts it never presses | `HelpRoomTerminalActs` | **Folded** into the rule: pressed at its tier, a verb with no view on the card naming its command (row b) |
| 17 | Full access hands no checkout | `HelpPlacement.Under`, `Reads = []` | **Folded** into the rule (ASKFIX1c, row b) |
| 18 | Full access hands every plugin server whole | `EveryServer = true` | **Folded**: as declared in both modes, the mode deciding only who says yes (ASKFIX1f, row b) |
| 19 | The second check after each turn; its flags told ahead of the next words | `AskDaorisSelection`, `AskDaorisTold` | **Kept**, its flag a note the next words carry; it is the fences' check where no owner holds them (§5.1) |
| 20 | Go links open a place on the person's press | `goLink.tsx` | **Kept**, drawn in every conversation by the one view (row d) |
| 21 | Every tool but the turns and the memory changes | rule 38 | **Kept** |
| 22 | Its limit move is briefed only, in its room under the same focus | rule 20 | **Folded** into the ways on: carried like any (row g) |
| 23 | The curve never moves it | rule 25 | **Folded**: the curve moves any ready conversation; the tool's own home stays excepted (row g) |
| 24 | A hand-over is refused for it | rule 29 | **Removed** (row g) |
| 25 | A new conversation from an earlier one, handed the brief | `HELP_START_FROM` | **Folded** into the branch (row i) |
| 26 | Rename, pin; its history lists only those spoken in or named | `HELP_RENAME`, `HELP_PIN` | **Folded** into the one list (row c) |
| 27 | Its agent from the `ask-daoris` role's order | `ActorChoices` | **Kept** as the default; the composer's pick holds for the conversation |
| 28 | No co-worker or helper of its own | roles rule 54; knowledge rule 18 (`helper_ask` refused) | **Changed**: it asks a read-only helper about a registered repository, at its tier (§5.2), the helper's answer a message from its sender; a co-worker stays refused, and work in a repository is still asked as an ask (row b) |
| 29 | The beyond receipt: what a turn read or pressed beyond its focus | `HelpBeyond`, `HELP_BEYOND` | **Kept** |
| 30 | Reads and presses reach every workspace; the focus limits nothing | D253 point 3 | **Kept** |
| 31 | A plugin changes Daoris only through the person's presses, each on its card | roles rule 61 | **Kept**, read with row b: a press at its tier |

Thirteen kept, twelve folded, one changed, five removed. What is left special about Ask Daoris is its place (the room), its focus, its
second check, and that its presses are the person's.

## 6. Migration

- **Records.** Today's records are already parts of lines. Every chat, driven session and Ask Daoris conversation reads into the
  model as it stands, its messages its parts' events. Nothing is moved or rewritten, so nothing is lost.
- **The conversation's id.** A migration step of the ledger gives each record without `line` its conversation's id once, from
  today's walk (`LineKeys.Of`: the quest's for a driven line, else the first part's); a walk cut by a cycle or the bound keys the part
  to itself, said on the conversation. The step stays until no supported ledger holds a record without `line`; `LineKeys`' walk as an
  identity and `ContextBriefs.LineOf` go with it (D228's condition).
- **Ends.** A part an older build ended, the person's stop included, is continued by the next word (row a for Ask Daoris, row f for
  every other conversation).
- **Ask Daoris's history.** `HelpConversations`' file (names, pins, focus and its marks, started-from) is imported once into the
  conversation's facts by a step of the home's migration, as `TurnMarksStep` removes the turn marks; a started-from becomes
  `branched` from the end of the earlier one.
- **The archive marks.** `SessionArchive`'s `archived.json`, a mark per record, is imported once by the same step: a conversation is
  archived where its newest part was marked, at that mark's time; a mark on an older part alone is dropped, since the list showed the
  newest. `SessionArchive` and its file go, and the screen's route and the terminal's `sessions archive` write the facts.
- Both steps stay until no supported home holds either file (D228's condition). A downgrade reads no names, pins or archive marks;
  the records themselves are untouched.
- **History clearing.** A clear of finished history (`HistoryDesk`, `SessionHomeFiles`) and a delete take whole conversations: a
  conversation is cleared only where every part of it is finished and nothing in it needs the person, its facts row with it. A clear
  never leaves a conversation with some of its parts gone.
- **Words.** Words an older build held in memory alone went with that process; from row a, none is held so.
- **Attachments** stay where they lie; the reading rule (§2.6) widens to the conversation.
- **Moves, entries, the brief and the block** are unchanged.
- **ASKCHAT1 is the first step** (row a). Its first part is merged: each conversation answers on its own, and a new one is always
  new. CONVO1 takes its rules as the model's, and its part left is row a's.

## 7. Rows, in order

Every row proves itself filtered to its own tests, each seen failing first, with `npm run quick`, and where it changes words,
`i18n:check` and `names:check -- --strict` in English and Chinese. A row that changes what the person sees is seen on the installed
window by the steward as it lands, replaying the failure it answers with the machine log's lines and the records that show the word
delivered by its id and the old process gone, never a shot alone; row l replays them all together.

The order puts the owner's failures first (row a), and the reach (row b) beside the foundation, which it touches nowhere. Then the
conversation's identity and facts (c), the reading as one (d), the switch's feasibility measured before the switch is built (e),
every conversation continuing (f), the switch (g) and what it keeps measured (h), the branch (i), the surface (j), driven sessions on
the one run (k) and the installed replay (l). Rows b to j land without row k and claim nothing of a driven session's steering until
it lands.

| Row (lanes; after) | What the person sees working | Proof | What it removes |
|---|---|---|---|
| **CONVO1a = ASKCHAT1** (driver, modules, service, web; first) | Built: each Ask Daoris conversation answers on its own, and a new one is always new. Left: every word kept before it is acknowledged, before a harness exists included; the opening bounded and stoppable, a server that does not come up said with its reason and the words kept; *Stop* ends the reply, ending the run's tree past its bound, what waits held until the person sends; the next word continues every end, those an older build's stop ended included; a call past its bound said with *Stop*; the room's files free of any conversation's focus, the focus handed with each turn | Built with its tests: two conversations of one workspace answering at once, a new conversation's first word never reaching a running one, two opens in one workspace. Left: a word sent while Ask Daoris starts surviving the driver's loss and the start's failure, its files included; a press repeated under one request key kept once; a record that will not keep a word leaving it in the held file; a server that never comes up, an adapter that never answers `session/new` or its resume, and *Stop* during the opening, each ending the opening said with the words held; *Stop* then a word on the native and the protocol stand-ins resuming the same record, the words that waited taken only at *Send*; a late bound timer ending nothing of the next run; a run ended at the bound never going on by itself; a hung call's tree ended at the bound and the conversation going on; two conversations started and resumed at once on different focuses, in both orders of writing the room and reading it, each reading its own focus | Built: `SetAsideHelpAsync`, `held.answering`, `START_HELP`'s carry-on with the page's `handTo` of it, the ledger's one-per-workspace refusal. Left: `HelpStarting`'s list; *waiting here alone*; the keeping stop's start of the next turn; the process-gone path's removal of words on the person's stop; `STOP_SESSION` and `END_CHAT` in Ask Daoris's composer; the focus in the room's files |
| **b** Ask Daoris's reach by its mode alone (driver, web; a; ASKFIX1c, ASKFIX1f, THROUGH1) | The room says one rule and five fences, each with what holds it on the harness that runs, and Settings and the panel say which only the room's instruction holds; in restricted mode every press Ask Daoris makes reaches a card, *Always* offered for a named family and shown before the yes; in full access it reads every checkout and a plugin's declared tools; it asks a helper about a repository | Room census: one rule sentence in `HelpRoomMayDo` and the doors table, no `Never` row, no *Done at a terminal* section, the fences drawn from one table in the room and in Settings; `PressViews` census: each verb the person can press reaches the card, a verb with no view on the card naming its command; delegate tests: `workflow accept` and `skip` at once in full access and on the card in restricted, keeping the run, node, attempt and revision; a delegate's press of the mode, a grant, or a stage, install or restart refused in both modes; *Always* offered for a family's press and not for a role's change, a run's Accept or a delete, and writing the grant `SET_HELP_GRANT` writes; the denies handed to Claude Code in full access (the access record, the accounts' credentials, the install's folder, writes outside the room and the home); `HelpPlacement` tests for checkouts and tools in both modes; a helper asked from Ask Daoris answering in its conversation, on the card in restricted for a checkout not granted | `HelpDoorAsk.Never` and every *yours alone* row; `HelpRoomTerminalActs`; `EveryServer = true` and `Reads = []` (built as ASKFIX1c and ASKFIX1f, not twice); restricted mode's refusal of a run's step; `helper_ask`'s refusal for Ask Daoris |
| **c** the conversation's identity and facts (service, driver, modules, web; a) | Rename, pin and archive on every conversation; Ask Daoris's names and pins and every archive mark as they were; a conversation replying or needing the person not archived, and an archived one shown again when something comes to need the person | `line` set at each part's start (a chat, a carried part, a briefed part, a driven resume with no `from`); a census that sending, reading, the brief, the course and a workflow's admission read membership one way; a fixture line past 256 parts keeping its id; the ledger step and the home step on fixtures (a history file; `archived.json` with marks on newest and older parts; a cut walk), each run twice; `ConversationFacts` store tests; a clear taking a whole conversation and refusing one with a part not finished; route census | `LineKeys`' walk as an identity and `ContextBriefs.LineOf`, each once its step's condition holds; `HelpConversations`' file and its reader; `SessionArchive` and `archived.json`; `HELP_RENAME` and `HELP_PIN` (one pair, shared with CHATUX1h) |
| **d** the conversation read as one (driver, modules, web; c) | Ask Daoris's and a session's conversation drawn by one view: every part in order, each boundary a note naming its why, every message kind with its sender, each word's delivery, each turn's outcome, the state, and a note where earlier parts are not held here | `ConversationReadTests` over a fixture line of three parts (a chat, a carried part, a briefed part on another agent) holding every message kind, every delivery (waiting, held, taken, withdrawn, uncertain), every turn outcome and every state, and a fourth whose earlier part is not held; one route for every kind (route census); `ConversationView` drawing Ask Daoris's and a session's from one fixture, go links in both | Ask Daoris's own drawing of earlier parts (`help/AskConversation.tsx`); a transcript assembled part by part as a second way (SWITCHCTX1c-i's line view becomes this row's) |
| **e** the switch's feasibility measured (the steward; a; HELPMODE1a3's spend sitting, with SESSCTX1a) | Nothing new: §3.3's first sitting | A measurements document beside this one: per door (Claude Code native, Claude Code over the protocol, Codex over the protocol; dsh and the layer named unsupported), a resume's cache read and write, a carry's copy accepted by the version that runs, an opening's seconds and its failure with a server that never comes up, *Stop*'s seconds, a warm run's memory with its servers and idle children; each figure with its build, door, version, account kind and model | Nothing; the opening's bounds, *Stop*'s bound and the warm bound move only on its numbers |
| **f** every conversation continues (driver, modules, web; a, d) | In a repository's chat and a closed quest's session too, *Stop* ends the reply and the next word goes on; no *Finish*; idle runs released by the warm bound, held ones listed | `ChatGoOnTests`, one case per part end (completed, failed, stopped by the person, stopped by Daoris, cut, released, limited with an account ready); the warm bound's order; a genuine idle run with its ordinary servers released, one with a background shell held and listed beyond the bound; release and resume on each stand-in door; a census that no conversation's end refuses a word but a teammate's record and a kind that takes no word | `END_CHAT`; `STOP_SESSION` for conversations; rule 13's clause that a person's stop never goes on; *Finish* and the second *Stop* press |
| **g** a switch between turns on any conversation (driver, modules, web; e, f; SWITCHCTX1b, SESSCTX1e) | The composer's agent and account pickers move any conversation, Ask Daoris included, the next turn going on the first way that holds, said with the way that ran and its figures; *Go on fresh* | `LineCourseTests` pinned rows for the person's switch in each case of §3.2; `ChatHandOverTests` for Ask Daoris and for the same agent and account; carry stand-in tests (whole, past the bound, briefed) from Ask Daoris's room; words queued before an access change run under the new access; two switches at once, the latest applied; a change back applied as nothing; a change before the first answer keeping the word; a restart during an application; a native model change resuming the part with the new spawn arguments; a carry whose destination exists refused and briefed; a restart between copy and commit completing or removing it; each note saying the way that ran; the pickers' words | `HandOver`'s refusals for Ask Daoris and for the same agent and account (`HandOver.SameAgent`); *Hand over…* as a press beside the picker; Ask Daoris's briefed-only limit move; the curve's exception for Ask Daoris; `HelpAccessSwap`'s wait and the focus change's own ending, as paths of their own; the carry's overwriting copy |
| **h** what the switch keeps, measured (the steward; g) | Nothing new: §3.3's second sitting | The measurements document's second half: every way on with its figures; the ten-fact recall and the four continuation tasks, both ways between Claude Code and Codex; per provider, model and version | Nothing; the carry bounds and the brief's sections move only on its numbers |
| **i** branch from a turn (driver, modules, web; c, d) | *Edit* on an earlier turn's words, or *New conversation from here* on an answer, opens a branch, the first untouched | `ContextBriefTests` for a brief cut at a boundary reading no later event, naming what it leaves out and the place, waits and memory as now; *Edit* excluding the turn edited and *from here* including it; `branched` with its part and event boundary standing after the earlier conversation resumes and restarts; the attachments a branch names copied and standing after the earlier conversation is deleted; a start from a turn on Ask Daoris and on a repository's chat; route census | `HELP_START_FROM` and `SESSION_START_FROM` as two routes; *Start a conversation with these words* as a press of its own |
| **j** the surface (web-shell; d, g, i; CHATUX1) | CHATUX1's column, composer, handles and lists drawing this model; the composer's pickers bound to the attributes (§3.1), the list to the facts (row c) | CHATUX1's own proofs | what CHATUX1's own table of removals names |
| **k** a driven session on the conversation's run (driver; f, h; the layer's duplex) | A driven session takes *Send now* and *Stop* as any conversation does, on every door that holds a conversation; on dsh and the layer, a turn per run, said; its work's stop is the workflow's press, named so | The driving process tests on the stand-in doors, a quest end to end as a conversation (first words, turns, done, review, landing); §4's four kept apart: a stopped turn followed by no review, landing or done, a pause holding the next turn with the words kept, a word in finished work opening a new run with no second landing or done, a word in a closed quest's conversation never reopening it; `CourseCensusTests` with one executor per way on; a census that nothing spawns outside the one run; *Send now* and *Stop* on a driven native run; a turn per run on a stand-in that holds no conversation; the family rehearsal at the merge | The native door's one-turn driven start for the doors that hold a conversation (agents rule 11); `DrivenInbox` and `SaidDoor`'s delivery to a running driven session; `DrivenCourse`'s carry-on and `LimitResume` as executors beside the conversation's; ASKPUSH1's part left. Lyntai's layer (`LayerRun`) takes the same duplex, asked of it as a quest; until then it runs a turn per run |
| **l** the installed replay (the steward; all) | The owner's failures replayed once each on the installed window: a word into a running call, *Stop* then a word, a new conversation while another answers, two answering at once, a start with its tool server down, Claude Code to Codex and back mid-conversation with the figures, a limit carried | Dated shots in both languages, with the machine log's lines and the records for each: the word's delivery by its id, the stopped process gone | Nothing |

## 8. Rejected

1. **A store of the conversation's messages**, one log per conversation written beside the parts' events or copied from the harness.
   It would be two records of one conversation (D228), and D189 point 1 holds: Daoris keeps it already. One reading joins the parts.
2. **Daoris holding the conversation and calling models statelessly**, as a chat app's server sends the whole history to a model
   each turn, with harnesses reduced to tools. Daoris runs no model loop of its own (agents and harnesses), the harnesses own their
   context, tools and subagents (D253 point 7: Daoris never narrows an agent), and the accounts are used through their harnesses.
3. **Translating one harness's conversation into another's format at an agent switch.** Daoris never writes an agent's conversation
   in a format it does not own (rule 6). The formats change with versions; even a same-agent copy needed a pinned compatibility
   table (SWITCHCTX1). The brief is the portable context.
4. **One process for the life of a conversation.** A conversation outlives restarts, crashes, limits and switches, and many
   conversations would hold as many idle harnesses. Runs are warm by a bound instead.
5. **Releasing idle runs on a clock.** Nothing is ended on a clock. The warm bound releases by count, never while the run's job holds
   work.
6. **A *Stop* that ends the conversation, and a *Finish* beside it.** A chat's *Stop* ends the reply. The owner lost a conversation
   and a word to the other meaning.
7. **A *Stop* whose waiting words start the next turn at once**, today's keeping stop. The assistant would answer again the moment
   the person stopped it. The words stay, held and shown, and the person's *Send* takes them; *Send now* stays the press that stops
   the turn and goes on at once.
8. **Rewriting or truncating a sent word, as the apps branch in place.** The record is the line's account, and the world the turns
   acted on (a tree, a press) has moved on. A branch is a new conversation handed the brief cut at its turn.
9. **A harness's own fork for every branch.** No door's fork at a turn has been read from its wire or binary (agents rule 6). A door
   that declares one, measured, may serve the branch later; silence preserves.
10. **Ask Daoris's reach as mode × grants × per-verb exceptions.** That is the mess the owner named, and the room said it two ways
    (§1). One rule, the card's *Always* as the standing yes for a named family, and five fences, each with its reason and its owner.
    So in restricted mode a run's Accept or skip goes on the card like any press, rather than never being Ask Daoris's (D277 point
    5's restricted half): the card is the person's own yes, and through words stay the way to authorise ahead, in both modes.
11. **Refusing full access on a harness that cannot hold a fence on its own tools.** A restriction is the person's to choose (D273):
    the window says which fences only the room's instruction and the second check hold there, and restricted mode or a harness with
    a rule language holds them.
12. **A brief of the workspace's last conversation handed to every new one.** A new conversation is new. The room names the recent
    conversations, and Ask Daoris reads the one the person points to; a branch is the press for carrying one on.

## 9. Decision D280

D280 records the choices of §2 to §5 that rejected an alternative, with §8's rejections. It amends, each when its row is built:
- conversations rule 13's clause that a person's stop never goes on (D206), by rows a and f, and its process-gone path taking off
  the waiting words on the person's stop, by row a;
- rules 16 and 44 (D137, D158): the two starts from another conversation become one branch, by row i;
- rule 20 (D206): Ask Daoris's room carried like any conversation, by row g;
- rule 25 (D165): the curve no longer excepts Ask Daoris, by row g;
- rule 29 (D163): no hand-over refusal for Ask Daoris or for the same agent and account, by row g;
- roles rules 55 and 57 (D277 point 5's restricted half; D183 and D261's grants as the card's standing yes for a named family), by
  row b;
- knowledge rule 18's refusal of `helper_ask` to Ask Daoris, by row b;
- agents rule 11 (D213, D49): the native one-turn driven start, for the doors that hold a conversation, by row k.

D188 point 1 (only a stop stops a turn) and its note of 2026-11-12 stand. D189 point 1 stands: the facts table holds no message.

## 10. The second read: each finding and what was done

Codex read the design and the code read-only and gave fourteen findings, eight marked blocking. Each was checked against the code at
`1ee777665e`. All fourteen are taken; where the read offered two ways, the one taken is named, and the one way it proposed that is
not taken (finding 8's refusal) is answered.

| # | The finding | What was done |
|---|---|---|
| 1 | Blocking: `LineKeys.Of` is no durable identity (a walk past 256 hops keys the current part) and membership differs between readers (`LineKeys.Members` joins a driven resume by its quest; `ContextBriefs.LineOf` follows `from` alone, 50 deep) | Taken. The id is set at the open and written on each record as `line`; membership is one read for every reader; history not held is said; a workflow's run is told apart from its conversation (§2.2, §6; row c). Checked: `Course/Domain/LineKeys.cs:25-49`, `Context/Events/ContextBriefs.cs:20-42` |
| 2 | Blocking: "nothing drops a word" needs admission changed: Ask Daoris's starting words are held in memory, with no key or level; a failed start keeps the text alone; a keep that fails leaves a word *waiting here alone*; restart settlement's *lost* is a fourth delivery | Taken. A word is acknowledged only once kept, on the record or in the held-words file, with its files, sender, level and request key; the starting list and *waiting here alone* go; *held* and *uncertain* join the deliveries (§2.3; rows a, d). Checked: modules' `Loop/HelpStarting.cs:15-29`, `Loop/Chat/Turns/TurnKeeps.cs:16`, `Course/GoOn/UncertainWords.cs:3-9` |
| 3 | Blocking: the start that hung with its tool server down is not in the contract; it is another path than a hung call | Taken. *Opening* is a state with bounded phases, *Stop* reaching it, a required server told from a plugin's; the three cases proved in row a and the dead-server start replayed in row l (§1, §2.5, §2.8). Checked: `Loop/Chat/Turns/TurnPump.cs:75-121`, `Loop/Chat/Turns/TurnStops.cs:21` |
| 4 | Blocking: say what *Stop* does to the waiting words (the keeping stop starts them as the next turn at once; the process-gone path removes them on the person's stop), and bind the 10 s escalation to one run and turn | Taken as the read recommended: *Stop* holds what waits until the person sends, *Send now* unchanged; the timer bound to its run and turn; a run ended at the bound is the person's stop, never a restart's go-on, and keeps the words (§2.6, §2.8; row a; rejected 7). Checked: `Loop/Chat/Turns/TurnStops.cs:12-14`, `Loop/Chat/Turns/TurnHolds.cs:288-293` |
| 5 | Blocking: one attribute path needs a boundary before the next words (today's access swap waits for the queue, so queued turns run under the old access) and must keep the equipment rebuild; native model and effort are spawn arguments | Taken. A switch is a revision kept on the facts and applied before the next words, the run released and the part resumed where its equipment differs; native model and effort by resume; the access swap's wait goes (§3.1; row g). Checked: `Help/HelpAccessSwap.cs:17-28` |
| 6 | Blocking: "whole: everything" overstates the carry, and the copy is unsafe (it overwrites, has no aggregate commit, its fallback deletes the destination; dsh declares no home; the compacted case is a plan) | Taken. The carry keeps the provider's conversation files, its exclusions said; it is first only with evidence for the version; the source quiet, the destination the carry's own, a staged copy committed by one rename, a fallback removing only its own writes, the way that ran said; dsh, undeclared doors and Codex past its bound briefed (§3.2; rows e, g). Checked: `Agents/Contract/CarriedFiles.cs:10-11`, `Agents/Providers/Dsh/DshProvider.cs:50` |
| 7 | Should: the brief's losses are understated (each word capped at 4,000 weight, all at 12,000; the 50-part read can drop history unseen); measure continuation, not exact strings alone, per provider | Taken. The caps, parts not held and missing attachments are said; membership replaces the 50-part read; four continuation tasks join the recall; each figure per provider, model and version (§3.2, §3.3; row h). Checked: `Context/Domain/ContextBrief.cs:40-67` |
| 8 | Blocking: the fences are promised, not enforced: Codex holds no rules, full access drops the tree guard, and the delegate governs Daoris's presses alone; a census of text proves nothing | Taken: each fence's owner on each path is in §5.1's table, the denies row b hands to Claude Code named, and where none holds a fence the window says so per harness; the census is no longer the proof. The read's other way, refusing the combination, is not taken: a restriction is the person's to choose (D273), and restricted mode or a harness with rules holds the fences (rejected 11). Checked: `Help/HelpPlacement.cs:59-69`, agents rule 19 |
| 9 | Should: *Always* needs a defined family and scope before the grant; keep the person's evidence on the card; reconsider rule 28's ban on read-only delegation | Taken. *Always* only for a named family, shown with its verbs before the yes, never a role or a press outside the families; the card keeps the run, node, attempt and revision; rule 28 changed: a read-only helper about a repository at its tier, a co-worker still refused (§5.1-§5.3; row b). Checked: roles rule 57, knowledge rule 18, co-working rule 28 |
| 10 | Should: derive the state but keep the intent (a switch pending, a hold, a stop) with a precedence; migrate `archived.json`; archive must not hide what needs the person; reconcile "only a delete removes" with clearing history | Taken. The kept intent is listed and the holds ordered; `archived.json` imported and `SessionArchive` retired; archive keeps its rule that it never hides what needs the person; a clear and a delete take whole conversations (§2.5, §6; row c). Checked: `Workplace/SessionArchive.cs:6-23`, `Workplace/SessionDeletion.cs:18-24` |
| 11 | Should: a branch needs a durable boundary (`TurnState.Turn` is one run's), an edit contract, a brief that reads no later state, and attachments that outlive the parent | Taken. The boundary is the part and an event position, kept as `branched`; *Edit* excludes the turn edited, *from here* includes it; the brief reads no later event and names the waits and memory as now; the attachments it names are copied (§2.1, §2.6; row i). Checked: `Context/Events/ContextBriefs.cs:85-95` |
| 12 | Should: conversations of one room still share its focus-written files (`AGENTS.md`, `CLAUDE.md`, settings), so one can read another's focus | Taken, by ASKCHAT1's part left (row a): the room's files say no conversation's focus, the focus is handed with each turn, and simultaneous starts and resumes on different focuses are proved in both orders (§3.1, §5.3 rule 1). Checked: `Help/HelpRoom.cs:110-133` |
| 13 | Blocking: the driven row (then i, now k) needs a workflow lifecycle contract (a cancelled reply, a pause, finished work and a closed quest kept distinct), "every door" exceeds dsh and the layer, and the continuing row's census (then e, now f) contradicts the kinds with no composer | Taken. §4's table keeps the four apart and says what the workflow does after each; doors that hold a conversation run on the one run, dsh and the layer a turn per run, said; row f's census excepts the kinds that take no word (§4; rows f, k). Checked: `Agents/Providers/Dsh/DshProvider.cs:33-36`, `Agents/Layer/LayerRun.cs:131-139`, `Workflows/ChatRuns.cs:76-86` |
| 14 | Should: keep the staged rollout but move capability evidence earlier, split the measurements, strengthen the proofs (crash boundaries, duplicates, concurrent Send, Stop and switch, unsupported cases, a genuine idle run's children), installed proof per milestone, the dead-server start in the replay | Taken. The rows are reordered (§7): admission, opening and *Stop* first, identity before reading, the feasibility sitting before the switch and the keeping sitting after it, driven last; each row names its added proofs; the warm proof uses a genuine idle run and the job's blind spot is said (§2.8); each row is seen installed with log lines and records; row l replays the dead-server start |

## 11. Open

- **The opening's bounds, the warm bound's number and *Stop*'s 10 s** wait on row e.
- **Whether a driven session's work stop needs a press of its own**, or *Pause* and the quest's stop serve it: read when row k is
  designed in detail, within §4's table.
- **A door's fork at a turn** is not read.
- **Lyntai's layer** must take the conversation's duplex to run a driven session on the one run; the request goes to Lyntai as a
  quest, and until it lands the layer runs a turn per run.
