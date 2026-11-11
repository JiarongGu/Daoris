# One conversation under every chat, driven session and Ask Daoris: a regular assistant's chat that outlives its processes and its agent (CONVO1)

**Proposed 2026-11-12; its choices are D280.** The direction is the backlog row CONVO1 (`TASKS.md`, *Conversations and context*),
on the owner's words of that day: rework the foundation of chat and session so it works like a regular LLM chat app, or can run a
workflow like one, carrying on across account and agent switches; sessions and Ask Daoris share it; Ask Daoris is a regular
assistant for all of Daoris, where today what it can and cannot do is a mess. Written by Claude Opus 5.5 on 2026-11-12 from main at
`25c4105bba`, read with `docs/areas/conversations-and-context.md`, `docs/areas/agents-and-harnesses.md`,
`docs/areas/roles-and-supervision.md` (rules 55-62), D188 and its note of 2026-11-12, D189, D206, D253, D277 and its note, the
switch-context design (`docs/2026-10-24-switch-context-design.md`) and the rows ASKCHAT1, ASKPUSH1, CHATUX1, SWITCHCTX1 and SESSCTX1.
A root-cause read of two days of the owner's own Ask Daoris use in the work workspace (kept off the tree, since it names private work)
gives §1's failures. Nothing here is built. An independent read is owed (§10). Driver paths start at
`src/Daoris.Desktop/Daoris.Desktop.Driver/`, module paths at `src/Daoris.Desktop/Daoris.Desktop.Modules/`, service paths at
`src/Daoris.Service/Daoris.Service.Core/`, web paths at `src/Daoris.Web/src/`.

## 0. In short

- **Most of a conversation exists; it is not yet one thing.** Daoris keeps every word, answer, tool call and note on its records, one
  record per harness conversation, linked into a line by `from`. It resumes by kept id, carries a conversation to another account of
  the same agent, hands Daoris's brief to another agent, and queues, steers and stops words. What it lacks is the conversation as the
  one thing the person talks in. A record's end reads as the conversation's end, and the person's *Stop* ends it for good. Ask Daoris's
  conversations share one answering slot per workspace, a new conversation can be handed to a running one, and the agent changes only
  by a hand-over that refuses Ask Daoris.
- **The model** (§2). A conversation is a line, its id the line's key. Its parts are harness conversations, its runs are processes,
  and its messages are its parts' events read as one. It outlives every run and every part. Its state is read, never stored:
  *replying*, *ready*, *waiting*, *archived*. *Stop* ends the reply; the next word continues whatever ended.
- **The agent and the account are attributes** (§3). They change between turns, by the person or by a limit, and the conversation
  goes on the first way that holds: the same part resumed, its harness conversation carried to another account, or a new part handed
  Daoris's brief. Each way is said with its figures. What the brief keeps and loses is listed; what it costs is partly measured, and
  the rest is measured once on the installed doors (row g).
- **A driven session is a conversation a workflow speaks in** (§4): its brief the first words, its steps' words messages from their
  senders, its review and landing what follows a turn's end, on the one run every conversation uses.
- **Ask Daoris is a conversation placed in its room, with Daoris's tools and the person's delegate** (§5). The access mode alone sets
  its reach: full access acts as the person, restricted asks on the card. Five fences hold in both modes, each named. Of its room's
  31 special rules, 14 are kept, 12 folded into the one rule or the one conversation, and 5 removed (§5.3).
- **Nothing moves at the migration** (§6). Today's records are already parts of lines, so every chat, driven session and Ask Daoris
  conversation reads into the model as it stands. Only Ask Daoris's history file is imported, once. ASKCHAT1 is the first row.
- **Eleven rows** (§7), each with its proof and what it removes. **Ten alternatives rejected** (§8).

## 1. The failures, and the mechanism each comes from

| What the owner met | The mechanism | Under CONVO1 |
|---|---|---|
| A word could not reach Ask Daoris while one tool call ran 20 minutes | The installed build had no *Send now*; a word waited for the turn's end (D188 point 3) | *Send now* steers or stops the turn and goes first (ASKPUSH1, built); a call past its bound is said with *Stop* beside it (ASKCHAT1) |
| A stop ended the conversation for good, and the word waiting was lost | Ask Daoris's *Stop* is `STOP_SESSION`: the process ends, the record is `stopped-person`, and "a person's stop keeps no word and never goes on" (conversations rule 13) | *Stop* ends the reply only, keeping what waits; the next word continues every end |
| A new conversation's first word went into the running one | `START_HELP` answers with the workspace's running conversation, "is carried on" (`Bridge/Routes/HelpRoutes.cs:58-65`), and the page hands it the words (`help/ask/useHelpSend.ts:267-273`) | A new conversation is always new |
| One conversation's turn held another's words | One Ask Daoris conversation runs per workspace: `ChatHelp.SetAsideHelpAsync` (`Help/ChatHelp.cs:102-116`) stops the others or holds the word as `held.answering`, and the ledger refuses a second open (`Sessions/SessionOpening.cs:158-167`) | Each conversation answers on its own |
| A new conversation knew nothing of the one before it | A new conversation is handed nothing of another, and the room names no recent conversation | The room lists the recent conversations and Ask Daoris reads one with `line_read` when the person points to it; a branch is handed the brief (§2.5) |
| What Ask Daoris may do read two ways | The room says full access presses as the person (`Knowledge/Room/HelpRoomMayDo.cs:38`) and that seven workflow presses are *yours alone* (`Knowledge/Room/HelpDoorRowsActs.cs:144-181`); full access hands no checkout and every server whole (`Help/HelpPlacement.cs:66-68`) | One rule, its five fences named (§5) |

## 2. The model

### 2.1 Four nouns

| Noun | What it is | Its id | Today |
|---|---|---|---|
| **Conversation** | What the person talks in and sees as one: a repository's chat, an Ask Daoris conversation, a driven session, a helper's or a reviewer's | its line's key (`LineKeys.Of`): the first part's id, the quest's for a driven line | the line, which only the course reads |
| **Part** | One harness conversation: one agent, one account's home, one kept harness id, one place | the record's id in the ledger | the record (conversations rule 1) |
| **Run** | One process serving a part for one or more turns | the process | `ChatRun` over `TransportRun`; a driven native run serves one turn |
| **Turn** | Words the agent took and its reply, up to the door's end of turn | its number on the part | `TurnState.Turn`, the `turn` event |

A conversation has one or more parts, linked by `from` with a why (rule 3); a part has any number of runs; a run serves one or more
turns. A conversation outlives every run (a restart, a crash, an idle release, a stop past its bound) and every part (a limit, a
switch, a harness conversation lost). The entry key (`SessionEntries`, `<origin>/<repository>`) keeps its job: it groups the
conversations that serve one ask or quest in the window's list, and is never a conversation's id.

### 2.2 Messages

A conversation's messages are its parts' events in `from` order (`SessionEvents`, `<part>.events.jsonl`). Nothing new stores them.

| Kind | What | Read from | Who says it |
|---|---|---|---|
| **words** | Words given to the agent, with their attachments and level | `user` with its `Origin` and `From`; a word kept on `said` | the person (verbatim); Daoris (a restart's word, a reset, a go-on); the workflow (a start's brief, a send-back); a co-worker's claim; a helper's answer; an opinion's findings. Each is named, and only the person's are the person's (D188) |
| **answer** | The agent's text, its thinking folded inside, its plan | `message`, `thought`, `plan` | the part's agent |
| **tool** | A call: kind, title, files, status, output bounded | `tool` | the part's agent |
| **note** | Daoris telling the person about the conversation: a stop, a switch and its figures, a limit, a card, a receipt, a focus change, a part's end, the turn's usage | `note`, `receipt`, `transient`, `ended`, `usage` | Daoris |

A word is a message from the moment it is sent. It is kept on the record before anything holds it (rule 12) and shown with its
delivery: waiting at its level, taken in turn *n*, or withdrawn by the person. Nothing drops a word. A stop keeps what waits, a
restart keeps the word (rule 13), and a start that fails gives back what it was composed with (rule 44).

### 2.3 Turns

A turn opens when the agent takes words, from any sender, and closes at the door's end of turn. It ends *answered*; *stopped* by the
person; *failed*, said with *Retry*; *limited*, when the conversation moves or waits (§3); or *cut* by a restart, when it goes on by
itself with Daoris's restart word (rule 13). Every outcome leaves the conversation ready or waiting, never ended.

### 2.4 States, read and never stored

| State | When | The composer |
|---|---|---|
| **replying** | a turn is in flight | *Stop* is the primary press; *Send* queues at the word's level; *Send now* |
| **ready** | no turn in flight and nothing holds the next; a run may be warm or not | *Send* |
| **waiting** | something other than the agent holds the next turn: a limit with no account to move to (its reset named), a move standing, a card the person answers, a workflow's hold | *Send* keeps the word for when it frees; the hold's own press beside it |
| **archived** | the person archived it | hidden from the list; a word brings it back |

The parts' ledger states (`working`, `completed`, `failed`, `stopped`, the `PartEnds` codes) stay each record's history and the
course's facts. No part's end is the conversation's end; only a delete removes a conversation.

### 2.5 Operations

| Operation | What it does | Today | Under CONVO1 |
|---|---|---|---|
| Send | Keeps the word, then a turn takes it: at once when ready, at its level when replying (D188), when it frees when waiting | `SESSION_INPUT`; Ask Daoris's first word through `START_HELP` | one press; a new conversation's first word opens it, never another |
| Send now | The person's own: into the running turn where the door steers, else the turn stops and the word goes first, said | ASKPUSH1 (`NativeSteers`, `AcpSteering`, `TurnCutIn`) | kept, on every kind, driven included (row i) |
| Stop | Ends the reply: the door's interrupt; a turn still in flight 10 s later has its run's process tree ended, the kept id kept, said; what waits stays | three presses: *Stop turn* (`CANCEL_TURN`), *Stop* (`STOP_SESSION`), *Finish* (`END_CHAT`) | one press, the keeping stop with its bound |
| Continue | The next word after any end goes on: the part resumed by kept id where it holds, else a new part handed the brief, saying why | rules 13-15; refused after the person's stop | every end; refused only for a teammate's record, which is read only |
| Retry | The last turn's words said again as a new turn; the agent sees its first answer | *Try again*, *Say it again* | one press with those two wordings (CHATUX1d) |
| Edit | A waiting word is edited in place. A sent word is never rewritten: the last turn's words go into the box for a new turn; an earlier turn's start a branch | `SESSION_QUEUE_EDIT` | kept, with the branch |
| Branch | A new conversation from a turn of another, handed Daoris's brief of the line cut at that turn and the place as it stands now; the first untouched | `HELP_START_FROM` (Ask Daoris, the whole line), `SESSION_START_FROM` (the words alone) | one press from any turn, recorded as `branched` |
| New conversation | Always new, on the choices the composer shows | `START_HELP` may carry on a running one; `START_CHAT` | always new |
| Attach | A file with a word, kept in the home's attachment folder of the part that took it and named to the agent by path | `ChatFiles`, readable by that part | every part of the conversation reads every attachment of it |
| Switch | Agent, account, model, effort, access, focus (§3) | `SESSION_OPTIONS`; `SESSION_HAND_OVER`, refused for Ask Daoris and for the same agent and account; the mode's swap; `HELP_FOCUS` | one attribute path, on any conversation |
| Rename, pin, archive, delete | The list's | `HELP_RENAME` and `HELP_PIN` for Ask Daoris alone; `SESSION_ARCHIVE`; delete | one set for every conversation |

### 2.6 What is stored, and where

| What | Where | Kept by |
|---|---|---|
| Messages and turns | the parts' event files and verbatim transcripts, on the machine (rule 5) | `SessionEvents` |
| Parts: state, `from` and why, kind, agent, account, place, kept harness id; words waiting (`said`) | the service's ledger | the service |
| The conversation's own facts no record holds: name, pin, archive, home, focus and its marks, `branched` (from which conversation and turn), the person's pick of agent, account, model and effort | one table in the desktop store, keyed by the conversation's id | a new `ConversationFacts`, replacing `HelpConversations`' file |
| Moves | the desktop store's `moves` (rule 10) | `MoveStore` |
| Attachments | the home's attachment folder of the part that took each | `ChatFiles`, `SessionAttachments` |
| The agent's own conversation | its account's home, the harness's format | the harness; Daoris copies it only in a carry (rule 6) |

### 2.7 Runs: a conversation outlives its processes

- **Start.** A turn that needs a run and finds none warm resumes its part by kept id, a go-on with no block and no brief (rule 13), or
  opens a new part (§3).
- **Warm.** A run with no turn stays up while its conversation may speak again. At most a bound of warm runs is kept, four to start
  with: the least recently spoken is released first, one the window shows last. Nothing is released on a clock (driving: *nothing is
  ended on a clock*). Nothing is released while something the run started still runs, such as a background shell, a monitor or a
  scheduled prompt (the job `PartLeftovers` reads). Such a run is held and listed. The bound is set from row g's figures.
- **Release.** End of input on the native door, `session/close` on the protocol door. The part ends `chat.closed`, as a shell's close
  between turns already ends one, and the conversation is ready.
- **Stop's bound.** The door's interrupt first (the native control request, the protocol's `session/cancel`). A turn still in flight
  10 s later has its run's process tree ended; the part keeps its id and the next word resumes it, said. A tool's server outside the
  tree, the in-app browser's, is told the cancel by the harness; a server that ignores it is held to its declared call bound (ASKFIX1f).
- **Restart.** Conversations rule 13 as it stands: a cut turn goes on by itself, and a ready conversation stays ready.
- **Many at once.** Each conversation's runs are its own. Nothing serialises across conversations but an account's limits and the
  line's own go-on admission (`ContinuationAdmissions`).

## 3. The agent and the account are attributes

### 3.1 The attributes

| Attribute | Chosen at the start by | Changed by | Takes effect |
|---|---|---|---|
| Agent (its door) | the role's order (`ask-daoris`, `chat`, the quest's role), or the composer's pick | the person between turns; Daoris at a limit, the curve or a rule's move | the next turn |
| Account | the walk, or the person's pick, never moved off (rule 21) | the person; a limit; the curve | the next turn |
| Model, effort | the role's choice within the agent's offer | the person (`ChatOptions`) | the next turn, the same part |
| Access | Ask Daoris: the mode; a repository: its access setup (D253 point 6) | the person | the next turn, the same part resumed under it (ACCESSSWAP1) |
| Focus (Ask Daoris) | the window's workspace | the person, in the composer | the next turn, the same part, one line ahead of the words |
| Place | the start: a repository's tree, Ask Daoris's room | never; a tree gone opens a new tree in a new part, briefed | the next turn |

A change asked while a turn runs waits for the turn's end, or for the person's *Stop*. A limit is met at a turn's end. A change before
the first answer keeps the word (ASKFIX1g).

### 3.2 The ways on, tried in order

| Case | The way on | What the agent keeps | What is lost | What is said |
|---|---|---|---|---|
| Same agent, account and place: model, effort, access or focus changed | the same part, warm or resumed by kept id | everything its harness kept | nothing Daoris takes; a harness compacts as it decides | a note: *Model: …*, *Access: …*, *Focus: …* |
| Same agent, another account | the part's harness conversation carried to that account's home (`IConversationCarry`): whole at or under the provider's bound, compacted by the harness past it, else briefed | whole: everything; compacted: the harness's summary | compacted: what the summary drops; briefed: as below | `session.moved` with its case and figures |
| Another agent | a new part handed Daoris's brief (D189) | every word (the person's verbatim, others' as claims), each turn's last answer and one line of what it did, the plan, uncommitted paths and commits, the last failed check, attachments by path, the outgoing agent's note, the other parts, the course | thinking; tool output but the last failed check; what files said when read; answers before each turn's last; its subagents' work; its own todo and memory; past the bound, the oldest turns and then the oldest words | a note naming the agent, that the brief is Daoris's record and not the agent's memory, its size and what it left out (`HandedCuts`); *Started with* opens it |
| Same agent and account, fresh (*Go on fresh*, SESSCTX1e) | a new part handed the brief | as another agent | as another agent | as another agent |
| Nowhere to go | the conversation waits | everything | nothing | the reset and the account it waits for |

Ask Daoris takes the same ways on. Its room is its place, so a carry needs nothing more; today its limit move is briefed only (rule 20).

### 3.3 Sizes, and what is measured

**Measured** (D206's note of 2026-11-08, on the install). A briefed part starts at about 68,000 tokens: its system text, tools,
standing block and brief together. Carrying 155,233 tokens whole costs about 87,400 more cached tokens at the first request, and their
reread after. Carrying whole is cheaper below about 170,000 to 200,000 tokens, so each provider's carry bound is 200,000 tokens
(`IConversationCarry.WholeBound`). The brief's own bound is 40,000 weight, a Latin character weighing 1 and a Han one 3 (rule 32):
about 10,000 to 13,000 tokens of English. A first prompt fits 32,767 characters (rule 36). STEER3 measured where each door takes a
word mid-turn (`docs/2026-10-03-steer-evidence.md`).

**Not measured.** Whether a resumed run's first request reads its context from the provider's cache or writes it again (SESSCTX1a,
owed on both doors). What a briefed part recalls against a resumed one: no brief has been counted on a harness. A warm run's memory,
and a resume's time to first words, on each door: these set the warm bound. How long each door takes to stop a turn, which sets
*Stop*'s 10 s.

**How row g measures them.** Once each on the installed doors, in HELPMODE1a3's spend sitting, never by a model's judgement:
- **Every way on**: resume; carry whole; carry compacted; brief from Claude Code to Codex and from Codex to Claude Code.
- **Figures** for each: the case, the context before (H), the first request's input, cache read and cache write tokens after, and
  the brief's weight and cuts.
- **Recall**: ten facts the record holds (a path, a number, a choice the person made, a failed check's name), written from the record
  before the switch and asked after it, each scored by its exact string in the answer.
- **Runs**: the warm run's memory, a resume's seconds to first words, and *Stop*'s seconds on each door.

The bound, the brief's sections and the warm bound move only on these numbers.

## 4. Sessions on it

A driven session is a conversation a workflow speaks in. Its first words are the quest's start prompt, from the workflow. Its steps'
words are messages from their senders: a review's *Ask for changes* is the person's, through the review; a hold's answer and an
opinion's findings are their senders'; a carry-on's and a limit's resume words are Daoris's. Its review, its landing and its quest's
done follow a turn's end, between turns, as CHATFLOW1 already reads a chat's work. The person reads it and talks in it as in any
conversation: *Send* at a level, *Send now*, and *Stop*, which ends the reply. The work's own stop is the workflow's press, *Pause* on
the line or the quest's stop, and is named so.

| Part of the system | Today | Under CONVO1 |
|---|---|---|
| A driven start | The driver composes the target prompt and spawns. A native driven run is one turn under a bare `-p`, taking no person's line (agents rule 11); continuations resume | The driver opens the quest's conversation, or continues it, and says its first words on the one run every conversation uses (row i, after row g) |
| Words to a running driven session | Kept on `said` and delivered by `DrivenInbox` at the protocol door's next step or the next part; *Send now* on a native driven run waits (ASKPUSH1's part left) | The conversation's queue and levels (D188), *Send now* as everywhere |
| Workflows | Runs keyed by the line, acting between turns or at an end | Unchanged: the line is the conversation |
| Quests | A driven line keyed by its quest; a chat may take one | Unchanged |
| Hand-backs | A helper's answer (`ChatHelperWords`), an opinion's findings (`ChatSendBack`), a quest's outcome, each said as a next turn | Messages from their senders at their levels; the answers-alone bound kept (rule 17) |
| Co-workers | Sessions of one entry, posting claims on a channel | Each its own conversation in the entry; a claim is a message at its level |
| Helpers, reviewers, intakes, opinions | Sessions of their kinds | Conversations of their kinds, read by the person, with no composer where the kind takes no word |
| Limit, carry, curve, hand-over | One decision (`CourseDecision.Decide`), with executors per loop (`Course/Chat/`, `Course/GoOn/`) | The same decision; one executor per way on for both kinds (row i) |

## 5. Ask Daoris on it

Ask Daoris is a conversation placed in its room, with Daoris's tools and the person's delegate. It reads everything Daoris records, in
every workspace. It does what the person can do in Daoris, at its tier. It talks as any conversation does (§2).

### 5.1 One rule, five fences

**The access mode alone sets its reach.** Full access acts as the person: each press runs at once, attributed, said once in the
conversation and listed in *Notifications* (roles rule 58). Restricted mode asks: each press goes on the card, the one way to ask the
person (D261). A family the person answered *Always* (a grant) then runs with no card, until the person takes it back where Settings
lists it. Nothing is refused by construction (D253 point 5).

**The fences hold in both modes.** Each guards something a yes on a card could not make safe:
1. what the person's own deny rules and the floor refuse (D253 point 2);
2. its own mode and grants, since setting them would raise its own reach;
3. a secret (a token, a password, a device code, a sign-in): it names the field and the person enters it;
4. staging, installing or restarting Daoris, which cuts every conversation, its own included (ASKFIX1b);
5. another repository's files: it asks that repository's work as an ask (`repository-owns-its-work`).

### 5.2 What Ask Daoris can and cannot do

| Act | Full access | Restricted |
|---|---|---|
| Read what Daoris records, in every workspace: sessions, conversations, quests, asks, runs, settings, logs, the room | at once | at once |
| Read a repository's checkout | at once, every checkout's root named (ASKFIX1c) | a granted checkout at once; another on the card, *Always* granting it |
| Press any verb the person can press: roles, settings, pauses, clean-ups, plugins, an ask with its requirements and through words, a run's Accept or skip | at once, as the person | on the card; a family answered *Always* at once |
| Act on another conversation: say a word, stop a turn, switch its agent, start one in a repository | at once | on the card |
| Its harness's own tools: shell, files in its room, the browser | its harness's unrestricted mode | its allow-list; a call outside it held for the person (agents rule 23) |
| A plugin's tools | as the plugin declares them for it (ASKFIX1f) | as declared, each approved |
| Its own mode or grants | never | never |
| A secret, or a sign-in | never: it names the field | never |
| Stage, install or restart Daoris | never | never |
| Write another repository's files | never: it asks | never |
| What the deny rules and the floor refuse | never | never |

### 5.3 The room's special rules: kept, folded, removed

| # | Today's rule | Where | Under CONVO1 |
|---|---|---|---|
| 1 | Its own room per workspace, written from the machine's facts at each start and go-on | `HelpRoom`, `HelpStart`, `ChatHelp.HelpRoomNowAsync` | **Kept**: the room is its place, as a tree is a chat's |
| 2 | One conversation runs per workspace | `ChatHelp.SetAsideHelpAsync`; the ledger's refusal | **Removed** (row a) |
| 3 | A new conversation's words carried into the running one | `START_HELP` (`Running = true`); the page's `handTo` | **Removed** (row a) |
| 4 | A word held because another conversation answers | `held.answering` | **Removed** (row a) |
| 5 | *Stop* ends the conversation; a person's stop never goes on | `STOP_SESSION`; rule 13 | **Folded** into *Stop* that ends the reply (rows a, e) |
| 6 | *Finish* | `END_CHAT` | **Removed** (row e): a ready conversation's run is released by the warm bound |
| 7 | The focus: machine, workspace or repository, chosen in the composer | `HelpFocus`, `HELP_FOCUS` | **Kept** as an attribute (§3.1) |
| 8 | A focus change ends a running agent at its turn's end, or ends one opened ahead at once | `HelpFocusTurn`, `HelpFocusAhead` | **Folded** into the one attribute path (row f) |
| 9 | Its home workspace; history per workspace; a conversation goes on where it is shown | `HelpConversations` | **Kept** as the conversation's facts (row d) |
| 10 | The access mode, machine-wide, chosen only by the person | `HelpMode`, `SET_HELP_MODE` | **Kept**: the one rule |
| 11 | An access change ends running conversations at their turn's end | `HelpAccessSwap` | **Folded** into the one attribute path (row f) |
| 12 | Grants: the act families `pause`, `clean-up`, `discard`, `settings`, and checkouts read | `ActFamilies`, `SET_HELP_GRANT` | **Folded**: the card's *Always* and Settings write the one grant (row b) |
| 13 | A delegate key per start; a press proved by it | `PipeDelegate` | **Kept**, as every session's (D253 point 6) |
| 14 | Restricted: a card per press; full: at once, recorded and listed | `PressViews`, `help.pressed` | **Kept**: the one rule |
| 15 | Seven workflow presses *yours alone*, never asked on a card | `HelpDoorAsk.Never` in `HelpDoorRowsActs` | **Folded** into the rule: pressed at its tier (row b; amends D277 point 5's restricted half) |
| 16 | *Done at a terminal*: acts it never presses | `HelpRoomTerminalActs` | **Folded** into the rule: pressed at its tier, a verb with no view on the card naming its command (row b) |
| 17 | Full access hands no checkout | `HelpPlacement.Under`, `Reads = []` | **Folded** into the rule (ASKFIX1c, row b) |
| 18 | Full access hands every plugin server whole | `EveryServer = true` | **Folded**: as declared in both modes, the mode deciding only who says yes (ASKFIX1f, row b) |
| 19 | The second check after each turn; its flags told ahead of the next words | `AskDaorisSelection`, `AskDaorisTold` | **Kept**, its flag a note the next words carry |
| 20 | Go links open a place on the person's press | `goLink.tsx` | **Kept**, drawn in every conversation by the one view (row c) |
| 21 | Every tool but the turns and the memory changes | rule 38 | **Kept** |
| 22 | Its limit move is briefed only, in its room under the same focus | rule 20 | **Folded** into the ways on: carried like any (row f) |
| 23 | The curve never moves it | rule 25 | **Folded**: the curve moves any ready conversation; the tool's own home stays excepted (row f) |
| 24 | A hand-over is refused for it | rule 29 | **Removed** (row f) |
| 25 | A new conversation from an earlier one, handed the brief | `HELP_START_FROM` | **Folded** into the branch (row h) |
| 26 | Rename, pin; its history lists only those spoken in or named | `HELP_RENAME`, `HELP_PIN` | **Folded** into the one list (row d) |
| 27 | Its agent from the `ask-daoris` role's order | `ActorChoices` | **Kept** as the default; the composer's pick holds for the conversation |
| 28 | No co-worker or helper of its own | roles rule 54 | **Kept**: it asks work of a repository as an ask |
| 29 | The beyond receipt: what a turn read or pressed beyond its focus | `HelpBeyond`, `HELP_BEYOND` | **Kept** |
| 30 | Reads and presses reach every workspace; the focus limits nothing | D253 point 3 | **Kept** |
| 31 | A plugin changes Daoris only through the person's presses, each on its card | roles rule 61 | **Kept**, read with row b: a press at its tier |

Fourteen kept, twelve folded, five removed. What is left special about Ask Daoris is its place (the room), its focus, its second
check, and that its presses are the person's.

## 6. Migration

- **Records.** Today's records are already parts of lines. Every chat, driven session and Ask Daoris conversation reads into the
  model as it stands: its id is its line's key, its messages its parts' events. Nothing is moved or rewritten, so nothing is lost.
- **Ends.** A part an older build ended, the person's stop included, is continued by the next word (row a for Ask Daoris, row e for
  every other conversation).
- **Ask Daoris's history.** `HelpConversations`' file (names, pins, focus and its marks, started-from) is imported once into the
  conversation's facts by a step of the home's migration, as `TurnMarksStep` removes the turn marks; a started-from becomes
  `branched` from the end of the earlier one. The step stays until no supported home holds the file (D228's condition). A downgrade
  reads no names or pins; the records themselves are untouched.
- **Attachments** stay where they lie; the reading rule (§2.5) widens to the line.
- **Moves, entries, the brief and the block** are unchanged.
- **ASKCHAT1 is the first step** (row a). It is being built on its own branch, and CONVO1 takes its rules as the model's: each
  conversation answers on its own, *Stop* ends the reply, and the next word continues.

## 7. Rows, in order

Every row proves itself filtered to its own tests, with `npm run quick`, and where it changes words, `i18n:check` and
`names:check -- --strict` in English and Chinese. A row changing a window is seen on the installed window by the steward. The order
puts the owner's two complaints first: row a, Ask Daoris's conversations, and row b, what it may do. Rows c to f make the conversation
one thing for every kind, row g measures the switches, row h branches, row i brings driven sessions onto the one run once row g's
numbers stand, and rows j and k are the surface and the installed proof.

| Row (lanes; after) | What the person sees working | Proof | What it removes |
|---|---|---|---|
| **CONVO1a = ASKCHAT1** (driver, modules, service, web; first) | Each Ask Daoris conversation answers on its own; a new one is always new; *Stop* ends the reply, ending the run's tree past its bound; the next word continues every end, those an older build's stop ended included; a call past its bound said with *Stop* | driver: two conversations of one workspace answering at once, a word to one never held by the other; a new conversation's first word never reaching a running one; *Stop* then a word on the native and the protocol stand-ins resuming the same record; a hung call's tree ended at the bound and the conversation going on. Service: two opens in one workspace. Web: `useHelpSend` never hands a new conversation's words to another | `SetAsideHelpAsync`, `held.answering` and its words; `START_HELP`'s carry-on and *finishing* answers, with the page's `handTo` of them and its `finished` set; the ledger's one-per-workspace refusal; `STOP_SESSION` and `END_CHAT` in Ask Daoris's composer |
| **b** Ask Daoris's reach by its mode alone (driver, web; a; ASKFIX1c, ASKFIX1f, THROUGH1) | The room says one rule and five fences; in restricted mode every press Ask Daoris makes reaches a card, the card offering *Always* for a family; in full access it reads every checkout and a plugin's declared tools | room census: one rule sentence in `HelpRoomMayDo` and the doors table, no `Never` row, no *Done at a terminal* section, each fence said once; `PressViews` census: each verb the person can press reaches the card, a verb with no view on the card naming its command; delegate tests: `workflow accept` and `skip` at once in full access and on the card in restricted; *Always* writes the grant `SET_HELP_GRANT` writes; `HelpPlacement` tests for checkouts and tools in both modes | `HelpDoorAsk.Never` and every *yours alone* row; `HelpRoomTerminalActs`; `EveryServer = true` and `Reads = []` (built as ASKFIX1c and ASKFIX1f, not twice); restricted mode's refusal of a run's step |
| **c** the conversation read as one (driver, modules, web; a) | Ask Daoris's and a session's conversation drawn by one view: every part in order, each boundary a note naming its why, every message kind with its sender, each turn's outcome, the state | `ConversationReadTests` over a fixture line of three parts (a chat, a carried part, a briefed part on another agent) holding every message kind and turn outcome, each state; one route for every kind (route census); `ConversationView` drawing Ask Daoris's and a session's from one fixture, go links in both | Ask Daoris's own drawing of earlier parts (`help/AskConversation.tsx`); a transcript assembled part by part as a second way (SWITCHCTX1c-i's line view becomes this row's) |
| **d** the conversation's facts (driver, modules, web; c) | Rename, pin and archive on every conversation; Ask Daoris's names and pins as they were | `ConversationFacts` store tests; the migration step importing a fixture history file once and removing it; route census | `HelpConversations`' file and its reader; `HELP_RENAME` and `HELP_PIN` (one pair, shared with CHATUX1h) |
| **e** every conversation continues (driver, modules, web; a, c) | In a repository's chat and a closed quest's session too, *Stop* ends the reply and the next word goes on; no *Finish*; idle runs released by the warm bound | `ChatGoOnTests`, one case per part end (completed, failed, stopped by the person, stopped by Daoris, cut, released, limited with an account ready); the warm bound's order and its hold while a stand-in's child runs; release and resume on each stand-in door; a census that no conversation's end refuses a word but a teammate's record | `END_CHAT`; `STOP_SESSION` for conversations; rule 13's clause that a person's stop never goes on; *Finish* and the second *Stop* press |
| **f** a switch between turns on any conversation (driver, modules, web; e; SWITCHCTX1b, SESSCTX1e) | The composer's agent and account pickers move any conversation, Ask Daoris included, the next turn going on the first way that holds, said with its figures; *Go on fresh* | `LineCourseTests` pinned rows for the person's switch in each case of §3.2; `ChatHandOverTests` for Ask Daoris and for the same agent and account; carry stand-in tests (whole, compacted, briefed) from Ask Daoris's room; each note's figures; the pickers' words | `HandOver`'s refusals for Ask Daoris and for the same agent and account (`HandOver.SameAgent`); *Hand over…* as a press beside the picker; Ask Daoris's briefed-only limit move; the curve's exception for Ask Daoris; `HelpAccessSwap` and the focus change's own ending, as paths of their own |
| **g** the switch measured (the steward; f; HELPMODE1a3's spend sitting, with SESSCTX1a) | Nothing new: the numbers of §3.3 | a measurements document beside this one, each figure with the build, door, account kind and model it ran on | nothing; the bounds move only on its numbers |
| **h** branch from a turn (driver, modules, web; c, d) | *Edit* on an earlier turn's words, or *New conversation from here*, opens a branch, the first untouched | `ContextBriefTests` for a brief cut at a turn, naming what it leaves out and the place as it stands; a start from a turn on Ask Daoris and on a repository's chat; the facts' `branched` link; route census | `HELP_START_FROM` and `SESSION_START_FROM` as two routes; *Start a conversation with these words* as a press of its own |
| **i** a driven session on the conversation's run (driver; e, g; Lyntai's layer) | A driven session takes *Send now* and *Stop* as any conversation does, on every door; its work's stop is the workflow's press, named so | the driving process tests on the stand-in doors, a quest end to end as a conversation (first words, turns, done, review, landing); `CourseCensusTests` with one executor per way on; a census that nothing spawns outside the one run; *Send now* and *Stop* on a driven native run; the family rehearsal at the merge | the native door's one-turn driven start (agents rule 11); `DrivenInbox` and `SaidDoor`'s delivery to a running driven session; `DrivenCourse`'s carry-on and `LimitResume` as executors beside the conversation's; ASKPUSH1's part left. Lyntai's layer (`LayerRun`) takes the same duplex, asked of it as a quest, or the row waits |
| **j** the surface (web-shell; c to f; CHATUX1) | CHATUX1's column, composer, handles and lists drawing this model; the composer's pickers bound to the attributes (§3.1), the list to the facts (row d) | CHATUX1's own proofs | what CHATUX1's own table of removals names |
| **k** the installed proof (the steward; all) | The owner's failures replayed once each on the installed window: a word into a running call, *Stop* then a word, a new conversation while another answers, two answering at once, Claude Code to Codex and back mid-conversation with the figures, a limit carried | dated shots in both languages, and the machine log's lines for each | nothing |

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
7. **Rewriting or truncating a sent word, as the apps branch in place.** The record is the line's account, and the world the turns
   acted on (a tree, a press) has moved on. A branch is a new conversation handed the brief cut at its turn.
8. **A harness's own fork for every branch.** No door's fork at a turn has been read from its wire or binary (agents rule 6). A door
   that declares one, measured, may serve the branch later; silence preserves.
9. **Ask Daoris's reach as mode × grants × per-verb exceptions.** That is the mess the owner named, and the room said it two ways
   (§1). One rule, the card's *Always* as the standing yes, and five fences, each with its reason. So in restricted mode a run's
   Accept or skip goes on the card like any press, rather than never being Ask Daoris's (D277 point 5's restricted half): the card is
   the person's own yes, and through words stay the way to authorise ahead, in both modes.
10. **A brief of the workspace's last conversation handed to every new one.** A new conversation is new. The room names the recent
    conversations, and Ask Daoris reads the one the person points to; a branch is the press for carrying one on.

## 9. Decision D280

D280 records the choices of §2 to §5 that rejected an alternative, with §8's rejections. It amends, each when its row is built:
- conversations rule 13's clause that a person's stop never goes on (D206), by rows a and e;
- rules 16 and 44 (D137, D158): the two starts from another conversation become one branch, by row h;
- rule 20 (D206): Ask Daoris's room carried like any conversation, by row f;
- rule 25 (D165): the curve no longer excepts Ask Daoris, by row f;
- rule 29 (D163): no hand-over refusal for Ask Daoris or for the same agent and account, by row f;
- rule 42 (D158): one conversation per workspace, by row a;
- roles rules 55 and 57 (D277 point 5's restricted half; D183 and D261's grants as the card's standing yes), by row b;
- agents rule 11 (D213, D49): the native one-turn driven start, by row i.

D188 point 1 (only a stop stops a turn) and its note of 2026-11-12 stand. D189 point 1 stands: the facts table holds no message.

## 10. Open, what to measure first, and the read owed

**Open.**
- **The warm bound's number and *Stop*'s 10 s** wait on row g.
- **Whether a driven session's work stop needs a press of its own**, or *Pause* and the quest's stop serve it: read when row i is
  designed in detail.
- **A door's fork at a turn** is not read.
- **Lyntai's layer** must take the conversation's duplex for row i; the request goes to Lyntai as a quest.

**Measure first** (row g): a resume's cache read on both doors (SESSCTX1a); recall after a brief, both ways between Claude Code and
Codex; a warm run's memory and a resume's seconds to first words; *Stop*'s seconds on each door.

**The independent read.** The repository's practice is a read by another maker before a design like this is built on: Codex in
`local/codex-tree`, read-only. The builder that wrote this could not run Codex, whose only binary is in the owner's install, which
builders do not touch. The read is owed: Codex's when the steward runs it, else an Opus read in its place. It should ask whether
rows a to f can land without row i; whether one attribute path can serve access, focus and agent, given access's posture is set at a
spawn; and whether row b's fences are the right five.
