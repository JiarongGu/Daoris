# A move keeps the agent's own context, and a session goes on as one conversation whatever runs it (SWITCHCTX1, 2026-10-24)

**Standing: proposed, round 3, under D191.** Round 1 was written by Claude Opus 5.5 from the code on main at `ddf0a24fd` and read
at `ff2461081` by the independent Opus foundation reader, its read verbatim in `docs/2026-10-24-switch-context-reads.md`: 12
findings, 4 blocking, another round. Round 2, written from main at `426ed2b73`, answered them in §8 and was read by the same reader
at `260881273`: 10 findings, 3 blocking, accept after named changes. Round 3 takes those named changes, each in §9, checked against
the code on main at `0d3af537b`, whose `src/` is the same as `260881273`'s, and against the pinned adapter's own package (§1.7).
Codex's read is owed, its credits out. Nothing here is built. The design reverses decided rules other designs rest on (§4 names
each), among them D189 point 1 on an agent's own transcript, so by D191's note of 2026-10-24 it is not accepted on one maker's read:
it stays proposed until Codex's read lands. The line view (SWITCHCTX1c-i) reverses nothing and is split out as a row of its own
(§6). The notes on D125, D131, D137, D165, D167, D189, D197 and D206 say *proposed* and point here.

The row SWITCHCTX1 in `TASKS.md` carries the case and the owner's direction of 2026-10-24: an account switch, and a switch to
another agent such as Codex, continue the session rather than start a new one. On the install a kit's chat moved at 06:52 UTC from
one account to another over the usage curve, and its agent started afresh: the person read it as a new session, though D167 keeps
every record on the line's one entry as its parts.

A path without a root is under `src/Daoris.Desktop/Daoris.Desktop.Driver/`; page paths are under `src/Daoris.Web/src/`. *An
account's home* is the configuration home Daoris makes per account, `HarnessSettings.ProfileHome(home, agent, account)`
(`Accounts/Harnesses/HarnessSettings.cs:881-882`): the entry `agent-homes`, at `profiles/agents/<agent>/<account>` since HOMELAYOUT3
and `harnesses/<agent>/<account>` for an account made before it (`Platform/DaorisHome.cs:324`, `:370`). This design reaches either
only through `ProfileHome`. *The person's own tool home* is the agent's home outside Daoris's accounts, the person's own sign-in
(D197 point 3), where a record names no account.

## 0. The direction, and the two halves

A session is one line of work (D167 point 1). What runs it may change: the account, when the usage curve or a limit moves it
(D165, LIMITGOON1), and the agent, when it is handed over (D163) or its actor's next choice takes it (D205's (e), LIMITGOON2).
The direction is that none of these reads as, or works as, a fresh start. Two halves, because only one of them can keep the
agent's own memory:

1. **The same agent from one of Daoris's accounts to another** keeps its own conversation: the conversation's own files are
   carried to the next account's home, and the same conversation resumes there by the agent's own resume, read back before it is
   counted. Where that does not hold, at the open or at the carried part's first turn, the move goes on as it does today, from
   Daoris's brief, and says so.
2. **Another agent** cannot take the first agent's own conversation: one maker's store is not another's. The line goes on from
   Daoris's record of it, the brief, as LIMITGOON1's and LIMITGOON2's go-on does today, and the page says so.

**On the page, either half reads as one conversation**: the earlier parts' turns above, the move said once between, never a
fresh session.

## 1. Today, read from the code

### 1.1 A move over the curve

- **The decision** is `SwitchDecision.Decide` (`Accounts/Domain/SwitchDecision.cs:83-85`), a pure function returning a
  `SwitchChoice` (`:10`): `Stay` with a reason (`:17`, reasons `:33-48`), `ReadOthers` (`:23`) or `MoveTo(Account, Window, Used,
  Curve)` (`:29`). A move is held for thirty minutes after the last (`Hold`, `:68`).
- **A conversation moves at its turn's end.** `ChatRunner` hands a turn's end to `CurveMove.Due`, or to the limit's move where
  the turn was limited (`Conversations/Chat/ChatRunner.cs:1497-1501`). The curve judges only a turn's end with nothing running,
  opening or queued, so **no words wait at a curve move** (`CurveMove.TurnJustEnded`, `Conversations/Chat/CurveMove.cs:36-37`).
  `CurveMove.MoveAsync` (`:62`) passes Ask Daoris, teammates' records, intakes and a record with no account by (`:70-77`: it
  requires `Profile: { Length: > 0 }`), keeps the `MoveTo` (`:106`), and asks the hand-over with the agent unchanged, why `curve`,
  by `daoris` (`:107-108`), then takes it (`:118`).
- **The take opens a new conversation.** `ChatHandOver` holds the turns, asks the outgoing agent a closing note where the person
  pressed or the agent asked (`Conversations/Chat/ChatHandOver.cs:145-149`), and starts the new part on the account the decision
  named (`:153`, `:156-157`). `HandOverTake.RunAsync` (`Conversations/HandOver/HandOver.cs:232`) composes the brief (`:250`), ends
  the old part (`:254`), starts a new record `from` it (`:256`), and hands the new agent a message holding the brief and nothing of
  the person's (`:271`). The new part's first line is `HandOverNotes.MovedFrom(left, move)`, `chat.moved-from`, and the old
  part's last line `HandOverNotes.Moved`, `chat.moved` (`:266-276`; `Conversations/Domain/HandOverRecords.cs:188`, `:198`).
- **The old part's end is a stop, not a wait for its exit.** `EndForHandOverAsync` stops the process and returns when its watch
  ends **or after fifteen seconds** (`ChatHandOver.cs:16`, `:225-230`); `LimitMove` ends the old part by the same call
  (`Conversations/Chat/LimitMove.cs:133`).
- **A quest's session moves by its carry-on.** `Driver.ContinuesFrom` names the why: a hand-over, then `limit` on another account,
  then `curve` where the walk's step was over the curve or the most room (`Loop/Driver.cs:4548-4559`, called at `:2422`); the
  carry-on opens a new record `from` it (`:2431-2439`), handed the brief (`:2521-2525`), its lines `moved.on` and `moved.from`
  (`SayMoved`, `:4515-4528`, called at `:2526`; `HandOverNotes.MovedOn`/`MovedFrom`, `HandOverRecords.cs:159`, `:169`) and the
  machine log's `session.moved` (`Conversations/Domain/Continuation.cs:585-593`, its field `briefed`). A driven start also reads what
  waited on the ask's channel for the entry's next part into its instruction (`Driver.cs:2540`, `:2620`) and marks it handed once
  the record works (`:2712`).

So every move over the curve is a new part on a new conversation, handed the brief. The account-switch design said what that
costs and left this row to measure it (`docs/2026-10-12-account-switch-design.md:132-135`).

### 1.2 A limit's go-on

- **LIMITGOON1**: a conversation whose turn its account's limit refuses moves at that turn's end to the next account of its
  agent the walk offers (`LimitMove.cs:51`, the walk at `:85-86`). It ends the old part (`:133`), starts the new `from` it, why
  `limit`, by `daoris`, by `runner.StartAsync` with `profile: null`, so **the start's own selection picks the account**
  (`:141-144`), and hands it the brief, then the words the refused turns held, then the words that waited (`:156-166`;
  `LimitHeld.Of`, `Conversations/Domain/LimitHeld.cs:11`). It requires no account on the record (`:66`). Its remarks say why the
  brief carries it: *the agent's own conversation is in the cooling account's home, which Daoris never reads, copies or writes
  (D189 point 5), so it cannot resume on the next account* (`LimitMove.cs:12-13`). D197's LIMITGOON1 note and D189's say the same
  (`docs/decisions/D197.md:371-379`; `docs/decisions/D189.md:246-250`).
- **An ended record held by its limit** moves by a durable box, `LimitMoveBox`, holding the reason, the refused turns' bounds, the
  ids of the waiting words and the new part (`LimitMove.cs:250`, `:266`); the brief is composed again from those
  (`:293-310`). The live limit move, the curve's take and a driven carry-on have no such box.
- **LIMITGOON2**: where no account of its agent has room, `ElsewhereAsync` walks its actor's effective order without that agent and
  opens the new part on the first choice that can run (`LimitMove.cs:92`, `:426`), handed the same brief and words: D205's (e)
  (`docs/2026-10-23-actors-design.md:478-494`), *never the transcript*.
- **Ask Daoris moves only by a limit**, by `LimitMove.HelpStartAsync`, in the room `HelpRoom.GoesOnIn` returns (`LimitMove.cs:402-416`),
  never by the curve (`CurveMove.cs:70-77`) or a hand-over (`HandOver.cs:190-193`). That room is the workspace's, which is not always
  the previous part's (ASKSCOPE1b, `Conversations/Chat/ChatGoOn.cs:648-649`).
- **One conversation on the page, for Ask Daoris only.** Ask Daoris's history folds a part handed the brief into its line
  (`HelpConversation.Parts`, `Help/HelpConversations.cs:22`, the fold at `:233-249`, the link `Brief = "brief"` at `:124`), and
  the page follows a chosen conversation to its newest part (`help/AskConversation.tsx:202-207`) and draws the earlier parts above
  it, read-only (`:870-876`). LIMITGOON1's note says a repository's chat *was one session by its parts already* (D189.md:255-258).

### 1.3 A hand-over to another agent

D163 point 1: *a hand-over is a carry-on with its agent named*, in the same tree, handed Daoris's record of it
(`docs/decisions/D163.md:9-11`), since D189 the brief (D189.md:57). D163 rejects *carrying the transcript or a model-written
summary* (D163.md:21-22). It goes by `HandOverTake.RunAsync` as §1.1's move does, the opening line `chat.taken-over` and the
last `chat.handed-over` (`HandOver.cs:270`, `:274`; `HandOverRecords.cs:134`, `:148`). The judge requires no account on the record
or the ask (`HandOver.cs:135-174`), so the person may hand a conversation on their own sign-in to an account, or the reverse.
`chat.taken-over` says where it came from and why, and nothing of what the new agent was given (`locales/en/note.json:195`), where
`moved.from` says *handed Daoris's brief*.

### 1.4 A line's parts, and what the page shows of them

- **One entry, many records.** D167 point 1: a session is *an entry holding one or more ledger records, its parts*, each *one
  harness conversation and one record* (`docs/decisions/D167.md:14-16`). The entry's key is `<origin>/<repository>`, a
  conversation's origin its own `session:<id>`, and a record that continues another takes that one's origin through its `from`
  (`src/Daoris.Service/Daoris.Service.Core/Sessions/SessionEntries.cs:26-37`, `:74`, `:87-89`; written at open,
  `Sessions.cs:818-819`). So a line's key is `session:<first>/<repository>`, `<first>` its first record (the fallback,
  `Trees/SessionGroups.cs:129`).
- **The page shows one part at a time.** A repository's chat draws the *Parts* strip, one button per part, oldest first, how each
  began read from its `fromWhy`, *Part 2 of 3* (`work/head/PartsStrip.tsx:90-113`, hidden for one part, `:107`), and the
  conversation of the attended part alone (`work/WorkFrame.tsx:2456-2457`). Pressing an earlier part shows it in place of the
  newest, and *every act on the page stays the part shown's* (`PartsStrip.tsx:91-95`). **This is what read as a fresh session on
  the install**: after the move the attended part is the new one, its conversation opens empty under the brief, and the turns
  before it are a press away.
- **How a part was handed** is kept only for Ask Daoris, as `Handed` in its `.help.json` (`HelpConversations.cs:11`, `:156`,
  `:188`); a repository's chat and a driven part keep no such value.
- **The words call it a new session.** `note.moved.on` and `note.moved.from` say *went on in session {{session}}* and *the new
  session* (`locales/en/note.json:197-198`); `note.limit.resume-refused` says *it went on in a new session* (`:206`);
  `work.limit.briefed` says *It goes on in a new session* (`locales/en/work.json:146`) and
  `notifications.done.look.limit-went-on` *went on in a new session* (`locales/en/notifications.json:69`); zh the same at the same
  lines. The glossary's *session* is the whole line and a run is a *part*, *each with its own conversation (D167)*
  (`locales/glossary.json`, rows `session` and `part`).

### 1.5 Where each harness keeps a conversation, per account home

- **The homes.** `HarnessSettings.ProfileHome` (`HarnessSettings.cs:881-882`) is the one way to an account's home, at either
  spelling of `agent-homes` (`DaorisHome.cs:324`, `:370`); its contract says *Daoris owns this location and nothing inside it*
  (`HarnessSettings.cs:878`). `HarnessSelection.Apply` creates it and names it to the agent by its variable
  (`Accounts/Harnesses/HarnessSelection.cs:579-580`): `CLAUDE_CONFIG_DIR` for Claude Code on both doors
  (`Agents/Adapters/Adapters.cs:1927`, `:2365`), `CODEX_HOME` for Codex (`:2110`; `Accounts/SignIns/CodexAccounts.cs:42`). The one
  write Daoris already makes inside one is the trust grant to `.claude.json`, on the person's own act
  (`Accounts/SignIns/ClaudeTrust.cs:69-73`).
- **Claude Code** keeps a conversation as `projects/<folder>/<id>.jsonl` under its home, `<folder>` the working directory with
  every character but a letter or a digit turned into `-` (`Platform/ToolHomeLeftovers.cs:30-31`, `Flatten` `:55`); its resume
  reads it there and refuses a missing one with `resource_not_found` (`docs/2026-10-02-answer-continues-design.md:36-38`). Read on
  this install's own account homes, 2026-10-24, by listing names only: beside `<id>.jsonl` is a folder `<id>/` holding that
  conversation's `tool-results/` and `subagents/`; the home also holds `.credentials.json`, `.claude.json` (the account's own
  configuration, a tree's trust among it, `ClaudeTrust.cs:82`), `sessions/` (running processes' files), `session-env/`,
  `shell-snapshots/`, `plugins/`, `skills/`, `cache/`, `state/`, `telemetry/`, `backups/`, the policy and remote settings files,
  and `usage-read/`. Claude Code versions have also kept files keyed by the conversation id in other places (`todos/`,
  `file-history/<id>/`, `session-env/<id>/`, `debug/<id>`), which a names-only read on the pinned version classes (§2.2). A folder
  is the same on every account home for the same tree, since a move keeps its tree (D189 point 2).
- **Codex** files a session by the date it ran under its home's `sessions/`, the folder it ran in said only inside its file, which
  Daoris never reads (`ToolHomeLeftovers.cs:41-42`). No Codex account home on this install holds a session, so the file's name and
  whether it carries the conversation's id are not read here.
- **The person's own tool home** still holds legacy conversations that go on there: a record with no account keeps its folder
  `resumable` (`ToolHomeLeftovers.cs:9`, `:122-123`).
- **The ids.** The agent's conversation id is kept per record in `<home>/sessions/<record>.harness.json`, never read from the
  agent's home (`Conversations/GoOn/HarnessConversations.cs:8-10`, `Keep` `:31`, `Read` `:54`), as `HarnessConversation(Adapter,
  Conversation)` (`Conversations/Domain/GoOnRecords.cs:35`), kept for a chat at `ChatRunner.cs:2369`.
- **The resumes.** The protocol door resumes by `session/resume` where the agent advertises it, else `session/load`
  (`Agents/Acp/Acp.cs:3224`, `:3270-3288`), `resource_not_found` read as `gone` (`:3245`), for claude-code-acp and codex-acp alike
  (`Adapters.cs:1506`, `:1828`, `:2060`). `session/resume` replays nothing; a load's replay arrives as updates and is dropped
  (`Acp.cs:3214-3216`, `:3236`, `:3290-3294`). Neither a load nor a resume names an id on this door: the id is the one asked for
  (`:3218-3221`), kept without telling a listener (`:3255`). A resume carries its record's chosen model and effort again
  (`:3260-3261`). **A choice the account does not offer ends differently by start.** Only the driven start hands a re-ask
  (`ModelRefused`, `Loop/Driver.cs:4259`): there the re-ask's later choice is set on the same session (`Acp.cs:676-706`), and the
  open ends `ChoiceNotOffered` only where no later choice comes (`:678`), caught by the one-shot run (`:814-821`). A chat's session
  is built with no re-ask (`ChatRunner.cs:2591-2601`), so the set's refusal is not judged (`Acp.cs:673`): the model is set aside as
  not carried (`:729-733`, told by `Carried`, `:710`), and the session runs on the account's default. Only the driven run takes
  the one-shot `AcpSession.RunAsync` (`:798-812`), whose resume always sends the run's prompt as its next turn (`:811-847`); a
  chat's session, Ask Daoris's among them, resumes or opens in `ChatRunner.RunAsync` and then waits for words
  (`ChatRunner.cs:2618-2619`; Ask Daoris's own go-on, `ChatGoOn.cs:651-653`). The pipe door is Claude Code's alone, `--resume <id>`
  (`Adapters.cs:2212`, `:2219-2223`, `:2279`; `ChatRunner.cs:1311`); `CodexAcpAdapter` (`Adapters.cs:2060`) is the only Codex
  adapter.
- **No resume follows a move.** A fresh start passes no `GoingOn` to `RunAsync` (`ChatRunner.StartCoreAsync`,
  `ChatRunner.cs:492-638`, the run at `:628-631`), so the protocol door opens with `session/new` (`:2618-2619`). The record's id is
  created inside the start, by the ledger's open (`:582-587`; the driven open `Driver.cs:2431-2439`). The resume's judge,
  `Continuations.Judge`, is the go-on's for a record whose words wait: it answers `ended` where none wait (`Continuation.cs:217`)
  and refuses a record whose account is not the one it ran on, *another account would not find it* (`:177-181`, `:226-229`); its
  callers are the ended-record go-ons (`ChatGoOn.cs:483`, `:525`, `:615`; `Loop/DriverContinuations.cs:61`, `:209`). A refused
  resume kills the process (`ChatRunner.cs:2622-2640`), under the rule that a new session would be *a new conversation under the
  same record* (`:2616-2617`, D137 point 8).
- **HOMELAYOUT5** (open, `TASKS.md:120`) asks the neighbouring question, whether a conversation resumes from a *moved folder*
  (`docs/2026-10-16-install-layout-design.md:166-167`, `:243-245`, `:272-273`): the tree moved and the home kept. Here the tree is
  kept and the home changes, so its answer is not this one's, and neither has been measured.

### 1.6 What the decisions say of copying, and of one conversation per record

- The account-rotation design, D125's contract, §3.5 point 5: *What does not travel is the harness's own conversation … which
  Daoris never reads or copies (D49 §4, D66 §3) … Daoris's accounts are separate homes by design, and what crosses between them is
  Daoris's* (`docs/2026-10-01-account-rotation-design.md:336-339`). D125 point 4 and its rejection of *copying the harness's
  conversation between account homes (a read inside an account's directory, D66 §3)* (`docs/decisions/D125.md:25-30`, `:61-62`).
- D131 point 4, *One Daoris record for one harness conversation* (`docs/decisions/D131.md:24`); point 5, *a carry-on after … a
  refused turn* stays a new session (`:26-28`); its rejections of *a linked record that the page folds into one thread*, of
  *resuming the conversation in a new record … That is the linked shape: one conversation over two records*, and of *`session/load`
  first* (`:41-47`). Its reason: one record standing for one conversation makes the page, the log, the review and a teammate's
  view show one session *without each learning a fold* (`:37-38`).
- D137 point 8: *One record per harness conversation; Daoris resumes only by the id it kept* (`docs/decisions/D137.md:31`), which
  the code reads as *never a new conversation under the same record* (`ChatRunner.cs:2616-2617`).
- D167 point 1: *A part is still one harness conversation and one record* (`D167.md:15`).
- D189 point 1: the agent's own conversation is *one per record* and *never read, copied or written by Daoris*; point 5: never
  handed *anything read or written in a harness's store*; point 6: *A left conversation is not resumed again … going back to its
  agent or account opens another new part*; rejected: *copying or writing a harness's store across accounts or makers* and *going
  back to a left conversation with what came after* (D189.md:14-17, `:33-36`, `:51-54`). The session-context design §9 gives three
  reasons: it acts inside an account's home, it depends on a format the harness changes at will, and it can never cross makers
  (`docs/2026-10-20-session-context-design.md:368-371`).
- D206 (LIMITRESUME1) rejects *copying the transcript to another account's home: D189 point 5, D125's rejection*
  (`docs/2026-10-23-limit-resume-design.md:673`).
- D165 point 3 moves a conversation *by the hand-over with its agent unchanged* (`docs/decisions/D165.md:17-19`), and the
  account-switch design's row SWITCHCTX1 was an evidence row spending two short turns (`:198`).
- D49 §4, D66 §3 and D197 point 3 keep the person's own tool home and sign-in outside Daoris's accounts.
- D131 point 2 also says a changed harness version is *said on the resumed run's first line, never refused* (`D131.md:18`), and
  D131 rejects *checking for the conversation in the agent's home before resuming* and *refusing a resume across a harness
  version* (`:48-51`). D137 rejects *a new record continuing the old conversation, linked to it*, *reading the account's home* for
  one, and *adopting a terminal conversation in place: two clients writing one conversation* (`D137.md:53`, `:55`, `:56`). D189
  point 7 makes Daoris's compaction *a new part handed the brief: at a move*, and its reason calls the brief *cheaper on every
  later request* (`D189.md:37-38`, `:46-48`). The code states the rule too: `LimitMove.cs:12-13`, `HarnessConversations.cs:8-10`
  (*never read from the agent's own home … which D125 rejected*) and `Continuation.cs:177-181` (*another account would not find
  it*).

### 1.7 What the record keeps, and what the pinned adapter replays

- **The record of a turn.** It keeps the person's words (`ChatMessage.Words`, `Conversations/Domain/ChatMessage.cs:188-200`); what
  the agent was sent, and stored, is the composed prompt: the brief, the briefing, the told process and the preface, then the words
  (`:174-175`); a go-on sends each word as a block of its own (`Conversations/Domain/ResumeAsk.cs:65-66`); and the record keeps the
  door's echo of a prompt as a person's `User` event (`Acp.cs:2956-2957`). So a person's turn has no stated counterpart in what
  the agent stores. **The agent's own messages are kept as the wire gave them**, each event with the update's `messageId`
  (`Acp.cs:2950-2952`).
- **What a part last ran.** `PriorSession.Ran.Models` is a bounded set of every model any turn reported, first seen first, up to
  eight (`Sessions/Domain/ActorRun.cs:120`, `:142-150`): provenance for a maker's trailer (`LimitMove.cs:453-455`), holding no
  effort. The part's last acknowledged model and effort are the last of `Ran.Entries` (`ActorRun.cs:119`, `:151-154`). A curve
  move's new part asks no model: the take starts with no order (`ChatHandOver.cs:156-157`), so what it asks is
  `ActorAsked.Before`, the account's default (`ChatRunner.cs:499`, `:1268`; `ActorRun.cs:9`), which sets nothing (`Acp.cs:715`).
- **The pinned adapter's load, read from its package with no turn spent**, as `Acp.cs:3218-3221` read its capabilities: the
  install's pin is claude-agent-acp 0.84.0 (`harnesses.json`'s `versions`), on the Agent SDK 0.3.284, its `dist/acp-agent.js`:
  - `loadSession` (l. 1345-1360) reads the transcript by the SDK's `getSessionMessages(id)`, which searches every project folder
    under the home's `projects/` and builds the conversation's chain by its `parentUuid` links, answering an empty list for an id
    it does not find (`sdk.d.ts`, `getSessionMessages`); it then opens the session resumed and replays (`replaySessionHistory`,
    l. 5339-5600).
  - **The model.** Where neither `ANTHROPIC_MODEL` nor the settings' `model` names one, a loaded session runs on the model of the
    transcript's last real assistant record and reports it as current (`resumed-session.js`, `resumedModelFromTranscript`;
    `session-model.js`, `getAvailableModels`, l. 335-365, and `matchResumedModel`, l. 141-162).
  - **The replay.** Every user and assistant message of the chain goes out as updates, each stamped with
    `messageIdForGrouping(message)` (l. 7635-7646): an assistant message's API id, else its uuid. Live, a streamed message's chunks
    carry the same API id, taken from its `message_start` (l. 4324, l. 4427). **So the agent's own messages replay with the
    `messageId` they had live**, which the record already keeps. A user message replays the composed prompt, local command
    metadata stripped. A tool call and its result replay as tool updates. The synthetic sign-in message is skipped. A synthetic
    usage-limit message replays as a typed failure only to a client that takes them, which Daoris does not advertise
    (`Acp.cs:1622-1635`), so it replays as the agent's text. A compaction's summary replays as user text, since Daoris advertises
    no compaction updates.
  - **What else it reads in the home.** With `subagents` advertised, as Daoris does where it streams (`Acp.cs:1627-1628`), a
    launched subagent's history is read from the conversation's own `<id>/subagents/` and replayed to a child session
    `<id>:replay-subagent:<tool use id>`, which the door's replay filter, keyed on the conversation's own id, already passes over
    (`Acp.cs:3290-3294`). Nothing else in the home is read for the conversation; the spawn reads the account's own settings and
    sign-in as every spawn does.

## 2. The design

### 2.1 Which moves carry

A move **carries** where all of these hold, and only there. They are judged by a carry judge of its own, `Carries.Judge`, pure
but for the looks it is handed, beside `Continuations.Judge` and never through it (the go-on's judge answers `ended` with no
words waiting, §1.5). Each refusal is a code, said on the new part and in the log (§2.3):

| Condition | Refused as |
|---|---|
| the agent is the same (its adapter unchanged) | `agent` |
| **both records name an account of Daoris's**: the old record's `Profile` and the start's settled `selection.Profile`, each non-empty, each a profile of that agent; a part on the person's own sign-in, on either side, moves by the brief (D49 §4, D66 §3, D197 point 3) | `own-home` |
| the account changes | (not a move) |
| the tree is the same (every move keeps it, D189 point 2), so the conversation's folder is the same name in both homes; for Ask Daoris, the room `HelpRoom.GoesOnIn` returns is the previous part's tree | `tree`, `room` |
| the old record kept a conversation id (`HarnessConversations.Read`), held to the grammar `[A-Za-z0-9-]{1,128}` | `unkept`, `id` |
| the old part's process has exited: its watch ended, not the fifteen seconds (§1.1), and the process registry holds none for it | `busy` |
| the door resumes by a load whose replay can be matched: the protocol door where the table's row names the replay's anchor (§2.3), and the pipe door only once LIMITRESUME1f has recorded that its `--resume` of a missing id fails closed (§3.3); and the old part's record holds at least one agent message with a `messageId` | `door` |
| the agent's carry table (§2.2) has a row for the adapter's pinned version, which is both the old record's `HarnessVersion` and the start's `selection.Version`. This refuses a carry across a version, which D131 point 2 never refuses for a resume (§4) | `version` |
| the old part acknowledged a model: the last of its `PriorSession.Ran.Entries` (`ActorRun.cs:119`), never `Ran.Models`, which is a provenance set (§1.7) | `model` |
| until SWITCHCTX1f has measured the cross-account case, **what ran** is the same: the carried start asks that model and its effort explicitly, with no re-ask, and after the set the session's acknowledged options (`LastAcknowledgedOptions`) name both, with no `Carried` told (`Acp.cs:710`) and no set refused (§2.5) | `model` |

The moves that meet it: D165's curve move of a conversation (`CurveMove` → `HandOverTake`) and of a quest's session (the carry-on,
why `curve`); LIMITGOON1's move to another account of the agent (`LimitMove`, live or ended); D206's briefed move of a limit-held
conversation to another account that frees first; the person's own hand-over to another account of the same agent; a quest's
carry-on why `limit`; and Ask Daoris's limit move (`HelpStartAsync`) where its room is the previous part's tree. Everything else
(another agent, the person's own sign-in on either side, a teammate's record, an intake, a tree or room that differs, an id never
kept) goes by the brief, as today.

The judge runs twice: **before the old part ends**, on what is known then (the agent, the old record's account, its tree, its kept
id, the door and the record's anchor, the version, the old part's acknowledged model), so a hand-over that will carry asks no
closing note (§2.4 step 2); and **after the start's selection settles the account**, on all of it, the old process's exit among
it. Only the second decides whether to copy. The model's last condition is judged on the wire after the load, since what ran is
known only there; a refusal there falls back at the open (§2.5).

**The model, judged on what ran.** A curve move asks the account's default today (§1.7), which is unknown before the spawn, so the
condition never compares what two starts *ask*. A carried start asks the old part's last acknowledged model and effort as values
of source `entry`, carried, and hands no re-ask on any path, the driven one included: a re-ask would set another model on the
carried conversation (`Acp.cs:676-706`). The pinned adapter's load already lands on the transcript's model (§1.7), so the set
confirms it where the next account offers it. Where the set is refused, or the acknowledged model or effort is another, or
`Carried` is told, the carry falls back `model`, and the fallback opens `session/new` with **the choice today's move would have
asked** (a curve move's or a hand-over's: the account's default, `ChatHandOver.cs:156-157`; a limit's move or a driven carry-on:
its own start's, with the re-ask that start hands today), never the refused one again, so a move that runs today never ends
without going on because it tried to carry.

### 2.2 What is copied, and what never is

The carry copies **the conversation's own files and nothing else**, named by its id, whole and byte for byte. Daoris never parses
them, never edits them and never writes into them. What it does is a narrow read and write inside two account homes, by name only:
it reads the previous home's files of a conversation the line owns and writes them into the next home. That amends the homes'
contract, *Daoris owns this location and nothing inside it* (`HarnessSettings.cs:878`), for these names alone; its one precedent is
the trust grant to `.claude.json` on the person's act (`ClaudeTrust.cs:69-73`). Both homes are reached only through
`HarnessSettings.ProfileHome(home, agent, account)` with the accounts §2.1 settled, never by a path read from a record, so the
person's own tool home is never a source or a destination.

**The carry table**, per agent and per pinned adapter version, beside the adapters. Each row classes **every place in the home
where a name holds the conversation's id** on that version, read by listing names only, as `copy` or `never`, and names **the
replay's anchor**, read from that version's package (§2.3); a version with no row carries nothing (`version`), a row with no
anchor carries nothing (`door`), and the row is read again when the pin moves.

| Agent, version | `copy` | `never` (each name holding the id) | Replay's anchor | Read on |
|---|---|---|---|---|
| Claude Code (both doors), claude-agent-acp 0.84.0 | `projects/<Flatten(tree)>/<id>.jsonl`; `projects/<Flatten(tree)>/<id>/` where it stands (its `subagents/` is read by the load, §1.7) | `todos/<id>*` (the agent's own plan, which Daoris's record holds), `file-history/<id>/` (rewind checkpoints, which no resume needs), `session-env/<id>/` (a process's environment), `debug/<id>*` | the agent's own messages' `messageId`, the API id live and on replay (§1.7) | the anchor read from the package for this round (§1.7); the names written by SWITCHCTX1a from a names-only listing of an account home on the pin; §1.5's listing of 2026-10-24 names the first two and `session-env/` |
| Codex (codex-acp) | the one file under `sessions/` whose **name** holds `<id>`, at the same relative path | not read | not read | not read: no Codex home here holds a session (§1.5); the row is SWITCHCTX1d's, and until then Codex moves by the brief |

**Never copied, per agent**, whatever the table says. For every agent: the account's credentials, its own configuration, its
history of prompts across conversations, another conversation's files, its plugins, skills, caches, telemetry, policy and usage
files. For Claude Code: `.credentials.json`, `.claude.json`, `settings.json`, `history.jsonl`, `sessions/`, `session-env/` and
`shell-snapshots/` (running processes' files). For Codex: `auth.json`, `config.toml`, and every file under `sessions/` but the one
its row names, since `sessions/` is where Codex keeps every conversation. A name the row does not class `copy` is not copied. An id
outside the grammar of §2.1 is refused before any path is built (a `:` names an alternate data stream on Windows), and a resolved
path that leaves the home, or passes through a link, is refused; either falls back (§2.5).

**How it lands.** Only once the old part's process has exited (§2.1, `busy`). Each file is copied to a name of its own beside its
destination and then renamed into place, so a reader never sees half a file. **Nothing that is not the line's is ever overwritten.**
A destination of the same name stands where a line moves back to an account it ran on before, and the person may since have run
that conversation in their own terminal with the account's home (the session-messages design §4.3,
`docs/2026-10-03-session-messages-design.md:292-293`: turns run there are in the harness's conversation and not in the record), which D137 will not adopt in place (`D137.md:56`). So:

- a destination `<id>.jsonl` is replaced only where it is a **byte prefix of the source**: the source is then the same file grown
  since, and the copy there is still what the line left;
- under `<id>/`, only the files that are missing are added; a file standing there is never replaced;
- otherwise the destination is left as it is and the carry falls back (`exists`), and a rename the disk refuses falls back the
  same way.

The source is **copied, never moved or removed**: the left conversation stays in its account's home, and the line's next part owns
the copy. **A carried line leaves one copy of its conversation in every account home it ran on**, as a line leaves one today, and
a copy left by a carry that fell back stays where it landed. Each goes with its home when the account is removed (D66 §3), and
nothing else removes it: a delete or a clear never removes the harness's conversation from an account's home
(`Sessions/History/SessionHomeFiles.cs:34-35`). `ToolHomeLeftovers` lists only the person's own tool home
(`ToolHomeLeftovers.cs:25-27`), which a carry never touches.

**Reasons, against §1.6's.** *It acts inside an account's home*: it does, narrowly, by name, on the line's own conversation only,
after its writer has exited, never on an account's credentials, identity or another conversation, and never on the person's own
tool home. *It depends on a format the harness changes at will*: the table is held to a version, so a new version carries nothing
until its row, its anchor among it, is read; and since a change that keeps the names but moves meaning elsewhere would resume
without the turns, a resume is counted only once its replay agrees with Daoris's record (§2.3), so a resume missing turns falls
back rather than counting. Daoris never parses the file: it reads the agent's wire, which states the anchor.
*It can never cross makers*: it does not, which is why the other half is the brief.

**A carried transcript names its first home.** A persisted tool output's path inside the file names the home it was written in
(§3.1); the carry copies `<id>/tool-results/` beside it, so the outputs are in the new home at the same relative place, but the text
still names the old. Once that account is removed (D66 §3), those paths point nowhere, and the agent reading one finds nothing, as
it would for any file gone since. SWITCHCTX1f records whether the agent reads them at all.

### 2.3 The ids, the record, and where the carry runs

- **The record.** A carried move is still a **new part**: a new record on the new account, `from` the old with the move's why
  (`curve`, `limit`, `press`), since a record names one account (D131 point 2). The entry is unchanged,
  `session:<first>/<repository>`. Each record names one conversation id, the one kept in its own `harness.json`; two records of
  one line name the same id, each in its own home, and only the newest part's is ever resumed. A copy left behind by a carry is
  never resumed as it was left: a move back to its account first replaces it with the line's newest (§2.2), so what resumes there
  is the line's conversation as it last ran, and only where the copy there is still what the line left (§2.2).
- **Where it runs.** The carry is an ask handed to the start, `CarryAsk` (the old record, its kept id, the move's why, and the
  brief's recipe, §2.5), taken by the three starts that open a moved part: `ChatRunner.StartAsync` (the curve's take, the person's
  hand-over, LIMITGOON1's live move, D206's ended move), the driven open (the carry-on, `Driver.cs:2422-2439`), and
  `LimitMove.HelpStartAsync` (`LimitMove.cs:402-416`). In each, in this order:
  1. the selection settles the account (`LimitMove` passes `profile: null`, so its account is known only here);
  2. `Carries.Judge` decides, on that account (§2.1);
  3. the ledger opens the new record, which creates its id (`ChatRunner.cs:582-587`; `Driver.cs:2431-2439`; `PlaceHelpAsync` for
     Ask Daoris);
  4. the copy runs (§2.2) and the new record's `harness.json` is written as `HarnessConversation(adapter, carried id)`, with the
     carry's own file beside it (§2.5). **The write is checked**: `HarnessConversations.Keep` swallows a failed write today
     (`HarnessConversations.cs:33-43`), so the carry writes through a call that says whether it landed and reads the id back; where
     it did not, the carry falls back `unkept` before the spawn, and no carried process ever runs under a record with no id;
  5. the spawn, and the carried id is resumed as a `GoingOn` is, each by the path that runs it: a repository's chat and Ask
     Daoris's new part by `ChatRunner.RunAsync`, the chat's protocol path, which resumes at `ChatRunner.cs:2618` (Ask Daoris's by
     `LimitMove.cs:414-417`, as its own go-on does, `ChatGoOn.cs:651-653`); the driven run by the one-shot
     `AcpSession.RunAsync`'s `resume` (`Acp.cs:798-812`).
  The carried start takes the agent's `session/load` where it is advertised, not `session/resume`, then sets the old part's model
  and effort and judges them (§2.1), then counts the replay (below).
- **Read back before it counts.** `session/resume` replays nothing, so a resume the wire accepted proves only that the agent found an
  id (`Acp.cs:3214-3216`). A carried start resumes by `session/load` and counts the replay the door already receives and drops
  (`:3236`, `:3290-3294`). D131 rejected load-first because the record already holds the history (`D131.md:47`); a carry is the one
  place where the replay is the check. **The unit and the anchor**, from the pinned adapter's replay as read (§1.7):
  - **The unit is one agent message of the conversation's own session**: on the record's side, the old part's `Message` events
    that carry an id (`SessionEvent.Id`, the update's `messageId`, `Acp.cs:2950-2952`), grouped by that id, in order; on the
    replay's side, the load's `agent_message_chunk` updates for the conversation's own id (a subagent's replay goes to a child
    session and is not counted, §1.7), grouped by `messageId`, in order. Thoughts, tool updates and the person's turns are not
    counted: the agent stores the composed prompt where the record keeps the words (§1.7), so a person's turn has no stated
    counterpart.
  - **It is resumed** only where every id of the record's list is in the replay's list, in the same order, and no agent message of
    the replay follows the record's last but one whose text is the limit's own sentence, which `LimitHeld` already reads
    (`Conversations/Domain/LimitHeld.cs:7`, `:37`). Otherwise the carry falls back in the same start, `short-replay`.
  - **The person's turns after it** are counted, never read: the number of user messages the replay holds after the record's last
    matched message, which for a limit's carry says whether the refused turns' words already reached the transcript. The log keeps
    the number; the words handed (§2.4 step 5) are not changed by it.
  - **A version whose replay holds nothing that can be matched** carries nothing: its table row names no anchor (`door`, §2.1), and
    the new part says so. A record that holds no agent message with an id (a part that never answered, or one written by a version
    that stamped none) is refused the same way, at the first judgement.
  - A compaction inside the part (the harness's own, D189 point 7) replays its summary as user text (§1.7); where the chain it
    builds leaves out the messages before it, the count falls short and the carry falls back, which is the safe side; 1f records
    how often.
  This reads the agent's wire, which the door receives on every load today, and never its store.
- **No id is named on the protocol door.** Neither a load nor a resume names one (`Acp.cs:3218-3221`): `ResumeAsync` keeps the id it
  asked for and tells no listener (`:3255`). So no `forked` arises here; it can arise only on the pipe door's `init` line, which is
  SWITCHCTX1e's (§3.3).
- **How it was handed** is kept for every kind of part, as a machine-only field of the session record, `handed`: `own` (its own
  conversation carried), `brief` (Daoris's brief handed), or absent (a part handed neither: a first part, a resume, or a carried
  part whose first turn has not yet answered). It goes through every place of the chain `.claude/knowledge/session-record-fields.md`
  names for a field set at the open (the service's record and store, the ledger's write, the host's request and response, the
  driver's write and read, the three stand-ins, the web's type), with no field on the sync wire, which a test holds; a teammate's
  mirror carries neither it nor any conversation id. **A briefed move writes `brief` at the open**, today's among them. **A carried
  part opens with none and is set later**, so the field also has its own update door, as a field set later does
  (`session-record-fields.md:40`), on the shape of `/actor`'s:
  - the store's `SessionStore.KeepHandedAsync(id, handed)` and the ledger's `SessionLedger.KeepHandedAsync`, beside
    `KeepActorAsync` (`Daoris.Service.Core/Sessions/Sessions.cs:1408`), taking only `own` or `brief`, on a record of this machine's;
  - the route `POST /api/sessions/{id}/handed`, answered on loopback alone, not found off it, as `/actor` is
    (`Daoris.Service.Http/Program.cs:1398-1409`);
  - the client's `ServiceClient.KeepHandedAsync`, beside `KeepActorAsync` (`Service/ServiceClient.cs:2266`);
  - their tests: `SessionStoreTests` (written, read back, null for a record from before and for a mirror), the host's (loopback
    only, `ContinuesHostTests`), and the driver's (the body written).
  The driver sets `own` at the carried part's first answer and `brief` where it falls back (§2.5). Its readers: the *Parts* strip's
  *began* words, the line view (a part `own` draws no brief, §2.7), and Ask Daoris's fold, whose room records take no `from`
  (`LimitMove.cs:7-8`), so `HelpStartAsync` also writes `.help.json`'s `Handed` as `own` beside `transcript` and `brief`
  (`HelpConversations.cs:118`, `:124`).
- **The machine log** keeps one field for how a move handed its part: `session.moved`'s `briefed` (`Continuation.cs:585-593`),
  false where the carry's open resumed and true where it fell back at the open. One line of its own, `conversation.carried`, says
  what held, once, when it is known: the agent and its version, the two accounts by profile name, the files' count and bytes, the
  replay's count of matched messages against the record's, the number of user messages after the last matched one, the model and
  effort acknowledged after the set, the first turn's stop reason and failure kind (§2.5), and the outcome: `held` (the first turn
  answered), or the fallback's code (`refused <why>` at the judge or the wire, `not-found`, `exists`, `unkept`, `model`,
  `short-replay`, `first-turn <why>`; `forked` only on the pipe door, 1e's). Never a word, a path or an account's key.

### 2.4 The move, step by step

1. The turn ends; the move is decided as today (`SwitchDecision`, the limit's walk, the person's press).
2. The first carry judgement (§2.1). A person's hand-over that will carry asks the outgoing agent **no closing note**
   (`ChatHandOver.cs:145-149`), since its own turns go with it; where the carry later falls back, the brief goes without the note,
   as a forced move's does today.
3. The old part ends, as today (`EndForHandOverAsync`), and the move reads whether its watch ended or the fifteen seconds did.
4. The new part's start (§2.3): the selection, the second judgement, the open, the copy and the checked write of `harness.json`,
   the spawn, the load, the model's set and judgement, and the replay's count.
5. Where it resumed, the new part is handed only what the move would have handed after the brief, and nothing where nothing waits:
   - **a curve move or a person's hand-over** (no words wait, §1.1): **nothing is prompted**. The resumed chat waits, as a resumed
     chat's session already does (`ChatRunner.cs:2618-2619`), and the person's next words are its first turn, with one line of
     Daoris's before them saying the account changed. No turn is spent on the new account that nobody asked for;
   - **a limit's move**: the refused turns' words, then the words that waited, each once, as `LimitMove.cs:156-166` orders them;
   - **a driven carry-on**: its instruction composed as today without the brief, the ask's channel's held messages among it
     (`Driver.cs:2540`, `:2620`), marked handed once the record works (`:2712`), as every driven start's are.
6. The lines are written: the old part's last, the new part's first (§2.7), and `session.moved`.

**Where a shell ends partway, the carry is still watched.**

- **The ended-record move** has D206's durable box (`docs/2026-10-23-limit-resume-design.md:395-406`): it carries the reason and
  the refused turns' bounds the brief is recomposed from (`LimitMove.cs:250`, `:293-310`) and gains the carry's outcome. A shell
  that ends between step 4 and step 5 comes up and goes on from the box. Today the come-up hands again the box's composed message,
  the brief in it (`LimitMove.cs:351-361`, `:300-309`); **where the box says the carry landed, it composes without the brief**, and
  hands only the words step 5 hands. Where it did not land, the brief as today.
- **Every other path** has no box. Once `harness.json` names the carried id, the new part is an ended record with a kept id, and the
  next word said to it goes through the go-on. Today the go-on resumes by `session/resume`, with no read-back (`Acp.cs:3227`), and
  its refusal ends in `CannotGoOn` or `LostAsync` (`ChatGoOn.cs:480-586`; `ChatRunner.cs:2622-2640`). **Wherever the record's
  `.carry.json` stands, the go-on takes the carried start's way instead**: the load, the model's set and judgement, the count, and
  the same-process fallback of §2.5, the brief recomposed from the carry's file. So a shell that ended before the load still has
  its count made, by the go-on, before the first turn.
- **A shell that ends before `harness.json` is written** leaves a record with no id, which goes on by the brief as a lost
  conversation does today (`ChatGoOn.cs:487-490`). A write that fails is `unkept` at step 4 (§2.3), never a record with no id under
  a running carried process.

### 2.5 When it does not hold: the brief, said

A carry that does not hold falls back to today's move, never to a wait and never to the person. **The brief is kept on the new part
until its first turn answers**: not its text but its recipe, in the carry's own file beside `harness.json`
(`<home>/sessions/<record>.carry.json`, under the same home entry): the old record, the move's why, the bound of the old part's
events the brief is composed up to, and **the agent's own hand-over note where one was written** (`HandOver.cs:247-251` puts it in
the brief, and the old record does not hold it). A brief recomposed from the recipe is **composed as of then**: it reads the old
part's events to the bound, which no longer change, and the tree's uncommitted state as it then is (D189 point 4), so it is the
move's brief but for what the tree did since, which is the current state the new part works in. `.carry.json` is a per-session
file in the session's file inventory (`Sessions/History/SessionHomeFiles.cs:91-124`) under the name `hand-over`, beside
`HandOverBox`'s (`:52-53`, `:114`), so a delete and a clear take it with the record.

**At the open**, in the same start:

- the second judgement refuses (§2.1), or the table names no file, or the file is not there (`not-found`);
- a copy fails, a name is refused, a destination stands that is not a byte prefix of its source, or a rename is refused
  (`exists`, §2.2);
- the write of the carried id does not land (`unkept`, §2.3);
- the protocol door refuses the load: `resource_not_found` → `gone` (`Acp.cs:3245`), or any other refusal on the wire;
- the set of the old part's model or effort is refused, or the session acknowledges another (`model`, §2.1);
- the replay disagrees (`short-replay`, §2.3).

**At the carried part's first turn**, judged by how the turn ended, in D210's four kinds (`Agents/Errors/AgentErrors.cs:5-19`) and
its stop reason, **never by whether the agent's message came first**: a provider's refusal is said first as the agent's message,
its `API Error`, and the turn then ends `refusal` (`Acp.cs:144-151`, APIREFUSAL1).

| How the first turn ended | What it is | What follows |
|---|---|---|
| stop reason `refusal` (`AcpOutcome.Refusal`), whatever the agent's message said before it | the carry's failure: the provider refused the carried history, or another account's or model's signed thinking | the fallback below, `first-turn refusal` |
| a `Fatal` failure at the prompt (`DoorWhere.Prompt`): the context does not fit, a request that cannot succeed by repeating it | the carry's failure | the fallback below, `first-turn fatal` |
| a `Limit` (`LimitHeld.cs:7`, `:37`) | the account's limit, not the carry's | **the limit's move** (LIMITGOON1, D206), with the kept id and the carry's file left as they are, so the next move carries the line's own conversation, never an empty fallback; no brief is handed on the limited account |
| a `Transient` failure, tried again in the same conversation by the door (`AgentErrors.cs:14`; `Acp.cs:841-847`) until its bound runs out | not the carry's | the carry stays pending, its file kept: the turn ends as a transient's does today, and the next word goes through the go-on's carried way (§2.4) |
| an `Access` refusal | the account's, which no brief on it mends | the carry stays pending, its file kept, and the account's access is said as today |
| the person's stop (`cancelled`) before any answer | the person's | the carry stays pending, its file kept; nothing is handed again |
| any other end | the carried conversation answered | `held`: the file removed, `handed: own`, `moved.carried-from` stands |

The words were the person's next, or the limit's refused and waiting words, or the driven instruction; none was answered.

Either way the fallback runs **on the same process**: it opens `session/new` **with the choice today's move would have asked**
(§2.1; never a refused one again), replaces the record's kept id by the new conversation's before any answer under it, through the
checked write (§2.3), and hands the brief recomposed from the carry's file, then the same words step 5 handed, as `LimitMove` and
`HandOverTake` hand them today. **This is a new path, not today's**: today a refused resume kills the process (`ChatRunner.cs:2622-2640`),
and in the one-shot run a refused `ResumeAsync` throws out of the run (`Acp.cs:811-812`), under the rule that a new session would be
a new conversation under the same record (`ChatRunner.cs:2616-2617`). It holds that rule's purpose: no conversation ever answered
under this record but the one it now names, so the record still stands for one conversation; §4 names the amendment of D137 point 8
for it. The record is marked `handed: brief`, the new part's boundary line says the carry failed and why (`moved.carry-failed`,
§2.7), the carry's file is removed, and `conversation.carried` says the code. A fallback is never tried again by spending a turn: the
next move tries the carry afresh.

### 2.6 Another agent: the brief, as today

A hand-over to another agent (D163), LIMITGOON2's go-on with its actor's next choice (D205's (e)) and a pick of another agent
carry nothing of the agent's own: a Claude transcript is not a Codex thread, and writing one maker's store for another is what
D189 rejected and this design keeps rejected. The line goes on from the brief, as `HandOverTake` and `LimitMove.ElsewhereAsync`
already hand it. **What changes is only how it reads**: one conversation across its parts (§2.7), its boundary saying that the new
agent has Daoris's brief and not its turns.

### 2.7 On the page: one conversation, either half

- **A repository's chat draws its line, not its part.** The conversation pane draws every earlier part's conversation above the
  attended part's, oldest first, read-only, as Ask Daoris's already does (`help/AskConversation.tsx:870-876`): `WorkFrame` maps
  the entry's parts before the attended `SessionConversation` (`work/WorkFrame.tsx:2456-2457`), each earlier one `live={false}`.
  The *Parts* strip stays; **pressing a part attends it and scrolls to it**, so every act on the page stays the part shown's
  (`PartsStrip.tsx:91-95`). The strip is also the rail of `PartsFlow` (`PartsStrip.tsx:102-103`), whose press attends and scrolls
  the same way. A driven session's line shows the same. A teammate's record shows only what it shows today (D47 §4).
- **Between two parts, one line on each side, once.** Where a part began by a move or a hand-over, the boundary is the old part's
  last line and the new part's first, and nothing else:
  - **the curve's figures**: where a move carries, its two lines are the carried pair below alone, never `chat.moved`'s pair beside
    them; the old part's line, `moved.carried-on`, holds the decision's window, used and curve where the why is the curve, as
    `chat.moved` holds them today, and the new part's names only where it came from;
  - **the opening line**: `RotatedOpening.Say` writes the new part's account line at its open (`ChatRunner.cs:603-604`;
    `Accounts/Rotation/AccountRotation.cs:600-610`). The record keeps it, and the line view folds it under the new part's boundary
    line, as a briefed part's brief is folded, so it never draws as a third line; a part that did not begin by a move draws it as
    today;
  - **a carry that fails later**: `moved.carried-from` is written at the open, where the replay agreed, and a carried part whose
    first turn then fails also gains `moved.carry-failed` (§2.5). The page draws a part's last move line alone at its boundary, so
    `moved.carry-failed` takes the place of `moved.carried-from`; the earlier line stays in the record and in the machine log.
  Where a part did not begin by a move (a sibling's request of the same ask, D167 point 2, a *Try again*, a resume into a new
  part), it has no move lines, and the page draws its *began* words from the strip as its boundary, one line. No separator of the
  page's own, no *new session*, no greeting drawn as a start. A carried part draws no brief, since none was handed; a briefed part's
  brief stays folded as today.
- **Ask Daoris's history** folds a part handed `own` into its line as it folds `brief` (`HelpConversations.cs:233-249`).
- **The words.** Moving and resuming are said in the session's words: a part, never a new session. **A line written before keeps
  reading**: a reworded code keeps exactly the values its lines were written with (`moved.on` and `moved.from`: `session`, `agent`,
  `account`, `why`, `tokens` and `count`, `work/noteLines.ts:290-291`), so H and B stay on every briefed move's line (D189 point 8)
  and no line needs a value it was never given; and the carried lines, whose values differ, are new codes, as the code's convention
  has it (`noteLines.ts:230-237`). No line names a part by its number, which the strip says.

| Code | en | zh |
|---|---|---|
| `note.moved.on` (old part, last line, a briefed move; reworded, its values as written) | *Went on as a new part of this session, {{session}}, with {{agent}} as `{{account}}`, because {{why}}: its context stood at {{tokens}} tokens, and the new part was handed Daoris's brief of it, {{count}} characters.* | *以本会话的新一段 {{session}} 继续，由 {{agent}}（账户 `{{account}}`）运行，因为{{why}}：其上下文为 {{tokens}} 个 token，新的一段收到了 Daoris 对它的简报，{{count}} 个字符。* |
| `note.moved.from` (new part, first line, a briefed move; reworded, its values as written) | *Continues the part before it, {{session}}, which ran on {{agent}} as `{{account}}`, because {{why}}: its context stood at {{tokens}} tokens, and it was handed Daoris's brief of it, {{count}} characters.* | *接续前一段 {{session}}（由 {{agent}} 以账户 `{{account}}` 运行），因为{{why}}：其上下文为 {{tokens}} 个 token，它收到了 Daoris 对它的简报，{{count}} 个字符。* |
| `note.moved.carried-on` (new; old part, last line, a carried move not by the curve) | *Went on as a new part of this session, {{session}}, with {{agent}} as `{{account}}`, because {{why}}, in its own conversation.* | *以本会话的新一段 {{session}} 继续，由 {{agent}}（账户 `{{account}}`）在它自己的对话中运行，因为{{why}}。* |
| `note.moved.carried-over-curve` (new; old part, last line, a carried move by the curve, `chat.moved`'s figures) | *Moved to `{{to}}` at the end of its turn, as a new part, {{session}}, in its own conversation: `{{from}}` had used {{used}} of its {{window}} limit, over its curve's {{curve}}.* | *在本轮结束时移至 `{{to}}`，以新的一段 {{session}} 在它自己的对话中继续：`{{from}}` 已用掉其 {{window}} 额度的 {{used}}，超过其曲线的 {{curve}}。* |
| `note.moved.carried-from` (new; new part, first line, written at the open where its replay agreed) | *Continues the part before it, {{session}}, which ran as `{{account}}`, because {{why}}, in its own conversation: its earlier turns were read back.* | *接续前一段 {{session}}（原账户 `{{account}}`），因为{{why}}，在它自己的对话中继续：已读回它先前的轮次。* |
| `note.moved.carry-failed` (new; new part, at the open or at its first turn; drawn in place of `carried-from`) | *Its own conversation could not go on as `{{account}}` ({{code}}), so it continues the part before it, {{session}}, from Daoris's brief of it, {{count}} characters.* | *它自己的对话无法在账户 `{{account}}` 上继续（{{code}}），因此凭 Daoris 对它的简报（{{count}} 个字符）接续前一段 {{session}}。* |
| `note.chat.taken-over` (reworded, its values as written) | *This conversation went on from {{agent}} conversation {{session}}, handed over because {{why}}; it was handed Daoris's record of it, not its own turns.* | *这段对话是从 {{agent}} 的对话 {{session}} 转交过来继续的，原因：{{why}}；交给它的是 Daoris 对其的记录，而非它自己的轮次。* |
| `note.limit.resume-refused` (reworded) | *… so it went on as a new part with Daoris's brief.* | *……因此它带着 Daoris 的简报以新的一段继续。* |
| `work.limit.briefed` (reworded) | *It goes on as a new part of its session, handed Daoris's brief.* | *它将以会话的新一段继续，并附上 Daoris 的简报。* |
| `notifications.done.look.limit-went-on` (reworded) | *A quest's work, cut off by an account's limit, went on as a new part of its session, handed Daoris's brief* | *因账户额度用尽而中断的工作已作为会话的新一段继续，并附上 Daoris 的简报* |
| `work.parts.began.carried` | *went on from {{after}} in its own conversation* | *接{{after}}，在它自己的对话中继续* |

`chat.moved-from` and `chat.moved` keep their words for a curve move that did not carry. `chat.handed-over` keeps its words: the old
part's line says where it went, and only the new part's says what it was given. Where a sentence names a quest or an ask, zh says
委托 and 需求; a *Try again* is 重试 (the glossary's rows). The glossary's *part* (段) and *conversation* (对话) are the words; the
row *part*'s *each with its own conversation* is amended to *each its own record; a part carried from another account of the same
agent goes on in the earlier part's conversation*. `i18n:check` and `names:check -- --strict` hold them.

## 3. What is unmeasured, and how it is measured

### 3.1 Unmeasured

**Read since round 2, at no turn's cost** (§1.7): what the pinned adapter's load replays, and what else it reads in the home for
the conversation (its own `<id>/subagents/`, nothing more). The replay's anchor and the stand-in's shape follow from that reading.
What stays unmeasured:

- Whether Claude Code, on the pinned claude-agent-acp, loads a conversation whose two entries (`<id>.jsonl`, `<id>/`) were copied
  from another account's home on a real account, and whether a record inside the file names the first home (a persisted tool
  output's path does) in a way that matters (§2.2).
- Whether a real replay holds every message the old part's record holds, and how often a compaction inside the part shortens it.
- Whether the API takes the earlier turns' signed thinking under another account; until 1f, a carry runs the same model and
  effort, judged on what ran (§2.1), and a provider's refusal of the history is seen at the first turn (§2.5). The
  account-rotation design saw a conversation outlive a re-sign-in in the maker's CLI
  (`docs/2026-10-01-account-rotation-design.md:338`), in the same home, which lowers the risk without measuring it.
- Whether a limit's refused turn's words are already in the carried transcript beside the refusal, so that handing them again puts
  them in the agent's context twice. The replay's count of user messages after the last matched answer shows it (§2.3), and the
  log records it.
- What a carried part's first request costs: the prompt cache is per account, so it writes the whole context H, where a briefed
  move writes S and B (`docs/2026-10-20-session-context-design.md:275-279`); whether it reads anything from a cache is not known.
- Codex's file's name, its replay's anchor, and whether codex-acp's resume finds a file it did not write in that home.
- The pipe door's `--resume` of an id that is not there (LIMITRESUME1f's question, §3.3).

### 3.2 Measured on the install's own next real move, never by spending turns

No turn is spent to test it, and the account-switch design's two-turn evidence row is withdrawn. The build lands with every
fallback of §2.5, so the first real carried move on the install **is** the measurement: it either holds or falls back, at the open
or at its first turn, with nothing lost. Where the carry does not reach a real move, today's briefed move is what runs, and nothing
is claimed. With the first-turn rule and the read-back stated (§2.3, §2.5), the first real carried move measures the agent's
context, not only the wire's acceptance. The `conversation.carried` line holds the outcome; the parent reads it from the machine
log after the install's next carried move and writes it as a dated note on D165, naming the agent's version, the outcome, the
replay's count against the record's, whether the refused turns' words were already there, the model and effort acknowledged after
the set, the first turn's stop reason, the first turn's cache write (the turn's `cacheWrite`, as the usage history already keeps
it, `Accounts/Usage/UsageHistory.cs:659`) and the part. Until that note, a carried part is said as read back, never claimed as
measured, and the design's claim stays *unmeasured*. A version of the agent that changes where it files a conversation has no row
and carries nothing (`version`); its row is then read on that version.

### 3.3 The pipe door waits for LIMITRESUME1f

The pipe door puts the person's words into `--resume`'s own arguments or stdin before the harness names its conversation
(`Adapters.cs:2219-2223`; `docs/2026-10-23-limit-resume-design.md:407-418`), so a resume that silently opened a fresh conversation
would send them to an agent without context, and it has no load to read back. LIMITRESUME1f records on the real door what `--resume`
of a missing id does. Where it fails closed, the pipe door may carry, an end before `init` read as the carry's failure and the brief
taken, with the first-turn rule of §2.5; where it opens a fresh conversation, the pipe door never carries and moves by the brief.
Until 1f lands it moves by the brief (`door`).

## 4. What it amends, each by a dated note (proposed)

Every decided rule the carry changes, each amended for a carry alone; none is said to stand where it does not.

- **D125**, its contract's §3.5 point 5, its point 4 and its rejection of copying between account homes: the line's own
  conversation, and only it, crosses from one of Daoris's account homes to another of the same agent; the credentials and everything
  else of an account's own never do, and the person's own tool home is never a source or a destination.
- **D131 point 4** (*one Daoris record for one harness conversation*), its rejections of *a linked record that the page folds into
  one thread* and *resuming the conversation in a new record*, its rejection of *`session/load` first*, and its point 5's carry-on
  after a refused turn: a carried move resumes one conversation in a new record, by load, read back. **D131's answer**: its reason
  was that every reader of records would have to learn a fold (`D131.md:37-38`, `:41-46`); D167 has since made the entry that fold
  for every reader, the page, the review and the history; `session.moved` counts a move, not a session; and a teammate's mirror
  carries no conversation id and no `handed`, so a teammate still sees one row per part as today. Each record still names one
  conversation id. Point 2's *the same account* reads as the account whose home holds the conversation, which a carry makes the new
  one. **Point 2's last sentence**, *a changed harness version is said on the resumed run's first line, never refused*
  (`D131.md:18`), and the rejection of *refusing a resume across a harness version* (`:50-51`): a carry is refused across a version
  (`version`, §2.1), since the table's names and the replay's anchor are bound to a version; a resume of a record on its own
  account stays never refused. The rejection of *checking for the conversation in the agent's home before resuming* (`:48-49`): a
  carry checks there, by the table's names alone, in the two account homes it settled.
- **D137's rejections**, beside its point 8 below: *a new record continuing the old conversation, linked to it* (`D137.md:53`), which is
  the carry's shape, answered as D131's is; and *reading the account's home* for a conversation (`:55`), which a carry does, by its
  kept id and the table's names, never by `--continue`, `--last` or a name. Its rejection of *adopting a terminal conversation in
  place* (`:56`) is kept: a carry never writes over turns another client ran (§2.2).
- **D189 point 7** (*Daoris's compaction is a new part handed the brief: at a move*, `D189.md:37-38`) and its reason's *cheaper on
  every later request* (`:46-48`): a carried move compacts nothing and keeps H on every later request on the new account. The
  answer is the session-context design's §5 as amended: *no way on is claimed cheaper*
  (`docs/2026-10-20-session-context-design.md:288-289`); the agent's own conversation is tried first for continuity, its first
  request's cost is stated (§3.1, §5) and measured (§3.2), and the person's *Go on fresh* still compacts by the brief.
- **D137 point 8** (*one record per harness conversation*): a carried conversation is named by two records, each in its own home,
  and only the newest part's is resumed; and where a carry falls back at its open or its first turn, the same record opens a new
  conversation on the same process, its kept id replaced before any answer under it (§2.5).
- **D165** point 3 and its design §4: a move over the curve with the agent unchanged, between two of Daoris's accounts, carries the
  conversation; the evidence row is withdrawn; the first real move measures it.
- **D167 point 1** (*a part is still one harness conversation and one record*): a part is one record, and a part carried from
  another account of the same agent goes on in the earlier part's conversation. The page draws a line's earlier parts above the
  attended one, and every part keeps how it was handed.
- **D189 point 1**'s *one per record* and *never read, copied or written by Daoris*, **point 5**, **point 6** and its rejections of
  copying a store across accounts and of *going back to a left conversation with what came after*: the same agent's own files may be
  copied whole between Daoris's account homes at a move, by name only and never parsed or written into; the carried conversation is
  named by each part of the line that carried it; a copy left behind is never resumed as it was left, and a move back to its account
  replaces it with the line's newest first. Across makers it stays rejected. Point 4's brief becomes the first half's fallback.
- **D197**'s LIMITGOON1 note: the carrier is the conversation's own, where it carries; the brief otherwise.
- **D206**'s rejection of copying the transcript, and its §3.5 briefed move to another account: carried where §2.1 holds.
- **The code's own statements of the rule**, each reworded by SWITCHCTX1a for the table's names: the homes' contract, *Daoris owns
  this location and nothing inside it* (`HarnessSettings.cs:878`); `LimitMove`'s remark that the conversation *cannot resume on the
  next account* (`LimitMove.cs:12-13`); `HarnessConversations`' *never read from the agent's own home … which D125 rejected*
  (`HarnessConversations.cs:8-10`), which stays true of the id and is amended for the carry's copy; and `Continuations`' *another
  account would not find it* (`Continuation.cs:177-181`), which stays the go-on's rule, a carry being judged by its own judge. The
  glossary's row *part* (§2.7) is amended by SWITCHCTX1c-ii.

**Kept**: D163 point 1 and its rejection of carrying the transcript, for another agent; D205's (e); D49 §4, D66 §3 and D197 point 3
(no account's credentials, and never the person's own tool home or sign-in); D137 point 7 (a resume asks for its record's own
account: a carried part's own account is the new one); D137's rejection of adopting a terminal conversation in place (§2.2); D210's
four kinds, by which a carried part's first turn is judged (§2.5).

## 5. Rejected

- **Spending two short turns to measure first** (the account-switch design's row): the fallbacks make the real move the measurement
  at no cost, and the owner's direction is not to spend turns on it.
- **Prompting the carried part at a curve move** with a line of Daoris's saying the account changed: it spends a turn on the new
  account that nobody asked for, whose first request writes the whole context H uncached; the line is the page's, and the agent
  takes it with the person's next words.
- **Counting `resumed` from the wire's acceptance**: it proves only that an id was found; the replay is read back instead.
- **Counting the person's turns in the replay**: the agent stores the composed prompt and the record the words (§1.7), so the two
  have no stated match; the agent's own messages, kept with the id they replay with, are the anchor.
- **Judging the first turn by whether the agent's message came first**: a provider's refusal is said as the agent's message before
  the turn ends `refusal` (§2.5); the turn's end and D210's kinds decide.
- **Falling back on a limit, a transient, an access refusal or the person's stop**: a brief on a limited account is refused there
  and would leave the limit's next move an empty conversation to carry; the others are not the carry's failure, and a brief mends
  none of them.
- **Judging the model by what each start asks**: a curve move asks the account's default, unknown before the spawn; what ran is
  read after the set.
- **A re-ask on a carried start**: it would set another model on the carried conversation; the fallback opens with today's choice
  instead.
- **Replacing a left copy because a record names its id**: the person may have run it since in their own terminal; only a byte
  prefix of the source is replaced (§2.2).
- **Moving the files** rather than copying them: it takes the left conversation out of its account's home (D189 point 6) and loses
  it where the resume fails.
- **Copying the whole home, or the account's configuration**: it carries credentials and another account's own.
- **Carrying out of, or into, the person's own tool home**: D49 §4, D66 §3, D197 point 3.
- **Sharing one home between two accounts**, or a link from one to the other: the accounts are separate homes by design (D66 §3,
  D125) and a link would carry everything.
- **Handing the brief as well as the carried conversation**: the agent has its own turns, and the brief repeats them at up to
  40,000 characters. A carried part's first request costs H, a briefed part's S and B (§3.1).
- **Keeping the brief's text on the new part** until its first answer: a second store of the conversation's words; its recipe is
  kept and the brief recomposed from the ended old part.
- **Opening a second record for the fallback**: one move would open two parts, the first of which never answered.
- **Writing a Claude transcript for Codex, or a Codex thread for Claude**: it parses and writes a store across makers (D189, kept).
- **Waiting for the person when the carry fails**: the brief is complete enough for a go-on (D189 point 3), so the line goes on.
- **A separator of the page's own between parts**: the boundary lines already say it, in the session's words.

## 6. Rows, in build order, with their proofs

Stand-ins are scripts the tests write under their own fixture home, as `ChatGoesOnProcessTests.Agent` does
(`src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/Conversations/ChatGoesOnProcessTests.cs:748`): never a real agent, never a window,
never a real console. **The stand-in ACP agent replays in the shape read from the pinned package (§1.7)**, never in the shape of
the rule it tests. It keeps each conversation as `projects/<Flatten(tree)>/<id>.jsonl` under its `CLAUDE_CONFIG_DIR`, advertises
`loadSession`, and refuses a missing one `resource_not_found`. On `session/load` it replays every message of the file in order:
each user message as the composed prompt it was sent, several blocks where it was sent several; each of its own answers as
`agent_message_chunk`s carrying the same `messageId` it streamed live; a tool call and its result as tool updates; a compaction's
summary as user text; and a subagent's messages to a child session id. It reports the transcript's model as current after a load,
and answers a prompt with the turns it holds. Its switches make it replay short, drop the ids, refuse the model or acknowledge
another, end a first prompt `refusal` after an `API Error` message, fail it `Fatal`, `Limit`, `Transient` or `Access`, or end it
`cancelled`. There is no switch to fork an id: the protocol door names none (§2.3).

| Row | What | After | Proof |
|---|---|---|---|
| SWITCHCTX1c-i | **Split out as a row of its own** (`TASKS.md`), under D167 and D189 point 8, accepted on its own read: it reverses no decision, so it waits neither for Codex's read of the reversal nor for 1a. The line view and today's words (§2.7): a repository's chat and a driven line draw their earlier parts above the attended one; pressing a part, in the strip or on `PartsFlow`'s rail, attends it and scrolls to it; one boundary per side, the opening line folded under a move's boundary, the *began* words where a part was not a move; `chat.taken-over` says the new agent was handed Daoris's record, not its own turns; `moved.on` and `moved.from` reworded with the values their lines were written with; `limit.resume-refused`, `work.limit.briefed` and `limit-went-on` reworded, in en and zh | — | Page tests over fixture events: **a line handed to another agent shows one conversation across its parts** (its earlier part's turns above, `chat.handed-over` and the reworded `chat.taken-over` between, no *new session*); **a curve move's line the same with one line per side**, `chat.moved` above and `chat.moved-from` below, the new part's opening line folded under it and not drawn as a third; a driven line's `moved.on`/`moved.from` pair **written before the rewording** drawing whole in the new words, its `tokens` and `count` shown; a sibling's part bounded by its *began* words; a press in the strip and on `PartsFlow` attends the part and scrolls to it; `i18n:check`, `names:check -- --strict`, `note.test.ts` |
| SWITCHCTX1a | The carry on the protocol door, wired on the curve's take (`CurveMove` → `HandOverTake` → `ChatRunner.StartAsync`): `Carries.Judge` and its codes; the table with its version, its anchor and per-agent never-list, read on the pin; the id's grammar; the copy, its byte-prefix rule and its refusals; the start's order (selection, judgement, open, copy and the checked `harness.json`, spawn, load, the model's set and judgement, the count); nothing prompted where nothing waits; the carry's file in the inventory, the first turn judged in D210's kinds, and both fallbacks on the same process with today's choice; `handed` through the record's field chain and its update door; `session.moved briefed=false` and `conversation.carried`; the go-on's carried way where `.carry.json` stands; the code's four statements of the rule reworded | — | **A stand-in's conversation moved by the curve to another account's home resumes with its earlier turns** (the replay counted by `messageId` against the record; the new part's `harness.json` holds the same id; no brief handed; nothing prompted until the next word, which the stand-in answers naming the earlier turns). The first turn: **a provider refusal said as the agent's message and ending `refusal` falls back**, the brief on the same process with the same words, `moved.carry-failed` drawn in place of `carried-from`; a `Fatal` the same; **a limit's refusal goes to the limit's move with the same id** and the carry's file kept, no brief on the limited account; **the person's stop and a transient retried** neither fall back, the file kept, the next word taking the carried way through the go-on. The model: **the old part's model asked and not offered falls back with today's choice**, never asked again; another model acknowledged, the same; no re-ask handed. The copy: **a left copy that grew elsewhere is left as it is** (`exists`, the brief); a move back to an earlier account whose copy is a byte prefix replacing it; under `<id>/`, a standing file kept and a missing one added. A write of the id that fails, the brief (`unkept`). And: a record on the person's own sign-in, either side, moves by the brief (`own-home`), nothing read in the fixture's own tool home; an old process not yet exited, the brief taken (`busy`); a load refused, the brief on the same process, no process killed; a short replay and a replay with no ids, the brief taken; an id outside the grammar, a version with no row, a row with no anchor, a record with no message id, each refused and briefed; the fixture home's `.credentials.json`, `.claude.json`, `history.jsonl`, `todos/<id>`, `file-history/<id>/`, another conversation's file and `sessions/` not in the destination; `.carry.json` taken by a delete; `handed` set by its update door on loopback alone and absent from the sync wire (tests hold both) |
| SWITCHCTX1b | Every other move of §2.1 takes it: `LimitMove` live (its account settled in the start) and ended (its box), D206's briefed move to an account that frees first, the person's hand-over to another account of the agent (no closing note asked), a quest's carry-on why `curve` or `limit` (`Driver.ContinuesFrom`), and `HelpStartAsync` where its room is the previous part's tree. On the driven path, **the same-process fallback goes inside `AcpSession.RunAsync`**, where a refused `ResumeAsync` throws out of the run today (`Acp.cs:811-812`), and **the briefed instruction is composed before the spawn**, so that fallback can hand it. **The come-up of an ended move composes without the brief where the box says the carry landed** | 1a | The same stand-in per path: a live limit move with held words (each once, after the load, no brief); an ended limit-held record; a person's press with no closing turn asked; a driven carry-on resuming its own conversation, **its first prompt the instruction without the brief, the channel's held messages in it and marked handed once it works**; a driven carry-on whose load is refused, briefed inside the same run, its briefed instruction the one composed before the spawn; Ask Daoris carried in the same room and briefed in another (`room`); another agent on each path still briefed. A shell ended between the carry and the resume: **for the ended move, the come-up goes on from the box, without the brief where the carry landed**; for the others, the next word takes the carried way through the go-on, its load counted and its first turn watched by the carry's file |
| SWITCHCTX1c-ii | The carried lines (§2.7): `moved.carried-on`, `moved.carried-over-curve` with `chat.moved`'s figures, `moved.carried-from`, `moved.carry-failed` drawn in its place, `work.parts.began.carried`, a part `own` drawn with no brief, Ask Daoris folding `own`, the glossary's row *part* amended | 1a | Page tests: a carried line with `moved.carried-from` and no brief, one line per side; a carried curve move with the figures on the old part's line; a failed carry with `moved.carry-failed` alone at its boundary and its brief folded; `HelpConversations` folding `own`; `i18n:check`, `names:check -- --strict` |
| SWITCHCTX1d | Codex's row of the table, read from a Codex home that holds a session, with its never-list and its replay's anchor read from the pinned codex-acp | a Codex account's session on the install | the row's names read on the pinned codex-acp, each name holding the id classed; the anchor read from its package; a stand-in filing by date loaded after a carry; until then Codex briefed (`version`; a test holds it) |
| SWITCHCTX1e | The pipe door carries (§3.3) | LIMITRESUME1f | the stand-in's reaction to 1f's recorded shape: an end before `init` → the brief, said; a different id named on `init` → the brief and `forked`; a first turn failing → judged as §2.5 |
| SWITCHCTX1f | The measurement (§3.2): the install's first real carried move read from the machine log and written as D165's note | 1a, 1b, 1c-i and 1c-ii built and installed | the parent's install proof: the outcome, the agent's version, the replay's count, whether the refused turns' words were already there, **the model and effort acknowledged after the set, the first turn's stop reason**, the first turn's cache write, the part, and the window showing the line as one conversation |

1c-i is first because it fixes what the person saw for both halves (§1.4) and reverses no decision, which is why it stands as a row
of its own; 1a gives the agent its memory, but only once a path is wired and installed.

## 7. What this design's gate does not cover

No real agent has loaded a carried conversation; §3.1 lists what is not known, and §3.2 says how it becomes known. The page's
change has not been seen in the window. Nothing here parses or writes inside a conversation's file; the carry reads and writes the
table's names alone, between Daoris's account homes; and nothing here touches the person's own tool home.

## 8. Round 2: round 1's read answered

Every finding of the Opus foundation read of round 1 (`docs/2026-10-24-switch-context-reads.md`, §4), each checked against the code
on main at `426ed2b73`. *Taken* means the design now says it; *answered* means it is not taken as named, for the reason given;
*deferred* means a later row carries it. The read's §1 corrections are findings 2, 5, 6, 8, 11 and 12 below.

| # | Finding (blocking in bold) | Checked against | Answer |
|---|---|---|---|
| **1** | **The carry changes decided rules the design does not name, and says two of them stand** | `D131.md:24`, `:37-38`, `:41-47`; `D137.md:31`; `D167.md:15`; `D189.md:14`, `:35-36`, `:53`; `ChatRunner.cs:2616-2617` | **Taken**: §4 names each as amended for a carry: D131 point 4, its rejections of the linked shape, of a new record resuming the conversation and of load first, and point 5; D137 point 8; D167 point 1; D189 point 1's *one per record*, point 6 and its rejection of going back to a left conversation. D131's answer is given (the entry is the fold, `session.moved` counts a move, a teammate's mirror carries no id). What stays is restated: each record names one id; a copy left behind is never resumed as it was left (§2.3). The notes on D131, D137 (new), D167 and D189 now say each is amended, none that it stands. |
| **2** | **The new part's resume is described with the wrong machinery** | `Continuation.cs:217`; `ChatRunner.cs:582-587`, `:628-631`, `:2618-2619`; `LimitMove.cs:141-144`; `Driver.cs:2431-2439`; `Acp.cs:774-776` | **Taken** as named: `ChatRunner.StartAsync`, the driven open and `HelpStartAsync` take a `CarryAsk`; the selection settles the account, the carry judge decides, the open creates the id, the copy and `harness.json` follow, then the spawn, which resumes as a `GoingOn` does; the judge is `Carries.Judge`, never `Continuations.Judge` (§2.1, §2.3, §2.4). |
| **3** | **A resume accepted and then failing is not seen; `resumed` proves only the wire** | `Acp.cs:814-821`, `:3214-3216`, `:3236`, `:3260-3261`, `:3290-3294` | **Taken**: a carried start loads and counts the replay against the record, `held` only after the first answer (§2.3); the first turn failing before any answer is the carry's failure, falling back on the same process, the brief kept on the new part by its recipe until then (§2.5); a carry asks the old part's model and effort until 1f (§2.1, `model`). Thinking signatures and the model case are in §3.1. |
| **4** | **The person's own tool home is not excluded** | `LimitMove.cs:66`; `HandOver.cs:135-174`; `ToolHomeLeftovers.cs:9`, `:122-123`; `CurveMove.cs:70-77` (the curve already requires an account) | **Taken**: §2.1's `own-home` condition, both records an account of Daoris's; homes reached only through `ProfileHome` with the settled accounts (§2.2); 1a proves a record on the person's own sign-in, either side, briefed, nothing read in the fixture's own home. |
| 5 | The old process is not known to have stopped | `ChatHandOver.cs:16`, `:225-230`; `LimitMove.cs:133` | **Taken**: the carry runs only once the watch ended, not the fifteen seconds, and no process is registered (`busy`, §2.1, §2.2); 1a proves it. |
| 6 | `handed: own` has nowhere to live; `carried=` duplicates `briefed` | `HelpConversations.cs:11`, `:118`, `:124`, `:156`, `:188`; `Continuation.cs:585-593`; `.claude/knowledge/session-record-fields.md` | **Taken**: a machine-only record field `handed` through the chain that knowledge names, off the sync wire, written for every briefed move too; its readers are the strip, the line view and Ask Daoris's fold (which also writes `.help.json`'s `Handed`); the log keeps `briefed` alone, `conversation.carried` saying what held (§2.3). |
| 7 | The table and the never-copied list | `ToolHomeLeftovers.cs:41-42`; §1.5's listing | **Taken**: the never-list per agent, Codex's `sessions/` excepted for its one file; each row held to a pinned version (`version` otherwise) and read again when the pin moves; every name holding the id classed `copy` or `never`; the id held to `[A-Za-z0-9-]{1,128}` (§2.1, §2.2). The persisted outputs' paths to the first home are said (§2.2) and measured by 1f. |
| 8 | Three paths uncovered: Ask Daoris's room, a driven part's channel messages, the hand-over's closing note | `LimitMove.cs:402-416`; `ChatGoOn.cs:648-649`; `Driver.cs:2540`, `:2620`, `:2712`; `ChatHandOver.cs:145-149` | **Taken**: `HelpStartAsync` carries only where `GoesOnIn`'s room is the previous part's tree (`room`); the driven carried part's first prompt is its instruction without the brief, the channel's held messages in it, marked handed once it works; no closing note where the first judgement says it carries (§2.1, §2.4). |
| 9 | At a curve move an unasked turn is spent, uncached | `CurveMove.cs:36-37`; `HandOver.cs:271`; session-context design `:275-279` | **Taken**: nothing is prompted where nothing waits, the account line given with the next word (§2.4 step 5); §3.1 and §5 state that the first request writes H; 1f reads its cache write (§3.2); §3.1 adds whether the refused turns' words are already in the transcript. |
| 10 | The page's words | `note.json:195`; `PartsStrip.tsx:91-95`; `work.json:146`; `notifications.json:69` | **Taken**: `chat.taken-over` says the new agent was handed the brief, not its turns; one line per side, the carried pair replacing `chat.moved`'s; a part that was not a move bounded by its *began* words; a press attends and scrolls; `work.limit.briefed` and `limit-went-on` reworded, en and zh (§2.7). |
| 11 | The rows | `LimitMove.cs:250`, `:266`, `:293-310`; `ChatGoOn.cs:480-528` | **Taken**: 1a wires the curve's take with the listed proofs; 1b names the box for the ended move and the go-on for the others; §2.4 says the box carries the bounds the brief is recomposed from; 1c splits, 1c-i first with no prerequisite, 1c-ii after 1a (§6). |
| 12 | Citations and wording against the code | `Driver.cs:2422`, `:2526`, `:4515-4528`, `:4548-4559`; `DaorisHome.cs:324`, `:370`; `HarnessSettings.cs:878`, `:881-882`; `ChatRunner.cs:2616-2640`; `Adapters.cs:1512`, `:2060` | **Taken**: the lines corrected (§1.1); both homes' spellings said and reached only through `ProfileHome` (header, §1.5); §2.2's reason reworded as the narrow read and write it is, amending the homes' contract (§2.2, §4); the same-process fallback said as a new path (§2.5); the Codex adapter claim cited to `CodexAcpAdapter` (§1.5). |

What round 2 left owed, the Opus reader's read of it, came at `260881273`; §9 takes it.

## 9. Round 3: round 2's read taken

Every finding of the Opus foundation read of round 2 (`docs/2026-10-24-switch-context-reads.md`, its round 2 §4), each taken as the
read names its smallest change, and each checked against the code on main at `0d3af537b` (its `src/` the same as `260881273`'s) and,
for findings 2 and 3, against the pinned adapter's own package (§1.7). The read's §1 corrections are finding 4; its §2 holes are
findings 1 to 3 and 5 to 9; its §3 rows are finding 10.

| # | Finding (blocking in bold) | Checked against | Where it went |
|---|---|---|---|
| **1** | **The carried part's first-turn trigger misses the provider's refusal and takes in ends that are not the carry's** | `Acp.cs:144-151`; `LimitHeld.cs:7`, `:37`; `AgentErrors.cs:5-19`, `:14`; `Acp.cs:841-847` | **Taken** as named: §2.5's table judges the first turn by its stop reason and D210's kinds, never by whether the agent's message came first. The carry's failure is an end on `refusal` or a `Fatal` at the prompt; a `Limit` goes to the limit's move with the kept id and the carry's file as they are; a `Transient` that runs out, an `Access` refusal and the person's stop leave the carry pending, its file kept; `held` only on any other end. §5 rejects the old trigger and the fallback on ends not the carry's. 1a proves each (§6). |
| **2** | **The read-back has no stated unit, and the replay it counts is not read** | `ChatMessage.cs:174-175`, `:188-200`; `ResumeAsk.cs:65-66`; `Acp.cs:2950-2952`, `:2956-2957`, `:3218-3221`, `:3290-3294`, `:1622-1635`; claude-agent-acp 0.84.0's `dist/acp-agent.js` (l. 1345-1360, 4324, 4427, 5339-5600, 7635-7646) and the SDK 0.3.284's `getSessionMessages` | **Taken** as named: the pinned package's `loadSession` replay is read (§1.7): the agent's own messages replay with the `messageId` they had live, which the record keeps. §2.3 states the unit (one agent message of the conversation's own session, grouped by `messageId`) and the anchor (every id of the record's in the replay, in order, nothing after the record's last but the limit's own sentence); the person's turns are counted after it, never matched. The stand-in replays in that shape (§6). A version whose replay holds nothing that can be matched carries nothing: its table row names no anchor (`door`, §2.1, §2.2), said. |
| **3** | **The model and effort condition reads the wrong field, judges what is asked, and its fallback is refused again** | `ActorRun.cs:9`, `:119-120`, `:142-154`; `ChatHandOver.cs:156-157`; `ChatRunner.cs:499`, `:1268`, `:2591-2601`; `Acp.cs:673`, `:676-706`, `:710`, `:713-733`; `Loop/Driver.cs:4259`; the package's `getAvailableModels` and `resumedModelFromTranscript` | **Taken** as named: the old part's last acknowledged model and effort are the last of `Ran.Entries`; a carried start asks them explicitly with no re-ask, and the condition is judged after the set, on the acknowledged options and `Carried` (§2.1); where either differs or is refused, the fallback opens `session/new` with the choice today's move would have asked, so it is never refused again (§2.1, §2.5). The package adds that a load already runs the transcript's model (§1.7). |
| 4 | Five claims about the code are wrong | `LimitMove.cs:414-417`; `ChatRunner.cs:2618`; `ChatGoOn.cs:651-653`; `Driver.cs:4259`; `Acp.cs:676-706`, `:798-812`, `:3218-3221`, `:3255`; `ActorRun.cs:119-150`; `HandOver.cs:247-251` | **Taken**, each corrected: Ask Daoris's new part resumes by `ChatRunner.RunAsync`, only the driven run by the one-shot run (§1.5, §2.3 step 5); `ChoiceNotOffered` only where a re-ask is handed, which only the driven start does, and a chat sets the model aside (§1.5); `Ran.Models` is provenance, the last of `Ran.Entries` what ran (§1.7, §2.1); no `forked` on the protocol door, and the stand-in's *fork an id* switch dropped from 1a (§2.3, §6); the recomposed brief is composed as of then and the recipe keeps the agent's own note (§2.5). |
| 5 | Rules the carry changes that are still unnamed | `D131.md:18`, `:48-51`; `D137.md:53`, `:55`, `:56`; `D189.md:37-38`, `:46-48`; `LimitMove.cs:12-13`; `HarnessConversations.cs:8-10`; `Continuation.cs:177-181`; session-context design `:288-289` | **Taken**: §1.6 cites each; §4 names D131 point 2's *never refused* and its two rejections, D137's two rejections (its adopt-in-place rejection kept), and D189 point 7 with its reason, answered from the session-context design's §5 (*no way on is claimed cheaper*); 1a rewords the three code remarks beside `HarnessSettings.cs:878`. The notes on D131, D137 and D189 name them. |
| 6 | A move back can overwrite another client's turns | session-messages design `:292-293`; `D137.md:56` | **Taken** as named: a destination file is replaced only where it is a byte prefix of the source; under `<id>/` only missing files are added; otherwise, or where a rename is refused, `exists` and the brief (§2.2). 1a proves a left copy that grew elsewhere left as it is. |
| 7 | A carry the shell interrupts, or whose id write fails, is not watched | `ChatGoOn.cs:480-586`; `Acp.cs:3227`; `LimitMove.cs:300-309`, `:351-361`; `HarnessConversations.cs:33-43`; `HandOver.cs:247-251` | **Taken** as named: the go-on takes the carried start's load, count and same-process fallback wherever `.carry.json` stands (§2.4); the come-up composes without the brief where the box says the carry landed (§2.4, 1b); step 4's write is checked and falls back `unkept` (§2.3); the brief is composed as of then and the recipe keeps the agent's own note (§2.5). |
| 8 | Where `handed` and `.carry.json` are kept | `session-record-fields.md:40`; `Sessions.cs:1408`; `Program.cs:1398-1409`; `ServiceClient.cs:2266`; `SessionHomeFiles.cs:34-35`, `:52-53`, `:91-124` | **Taken** as named: `handed`'s update door is named on `/actor`'s shape, the store's and ledger's `KeepHandedAsync`, `POST /api/sessions/{id}/handed` on loopback alone, `ServiceClient.KeepHandedAsync`, and their tests (§2.3); `.carry.json` goes under the inventory's `hand-over` (§2.5); a carried line's copies stay, one per home it ran on, until each account is removed (§2.2). |
| 9 | The page's words | `noteLines.ts:230-237`, `:290-291`; `ChatRunner.cs:603-604`; `AccountRotation.cs:600-610`; `PartsStrip.tsx:102-103` | **Taken**: of the read's two ways, the reworded `moved.on` and `moved.from` keep exactly the values their lines were written with, so lines written before read whole and `tokens` and `count` stay (D189 point 8); the carried lines, whose values differ, are new codes (§2.7). The curve's figures sit on the old part's carried line, `moved.carried-over-curve`; the opening line is folded under a move's boundary; `moved.carried-from` is written at the open and `moved.carry-failed` drawn in its place; `PartsFlow`'s press attends and scrolls (§2.7). `chat.taken-over` says *Daoris's record*, which holds for lines written before D189 as after. |
| 10 | The rows | `Acp.cs:811-812`; `LimitMove.cs:351-361` | **Taken** as named: 1c-i is a row of its own in `TASKS.md`, accepted on its own read (§6); 1a's proofs gain the cases of findings 1, 3 and 6 and a stand-in shaped by finding 2; 1b names the same-process fallback inside `AcpSession.RunAsync` and the briefed instruction composed before the spawn, and the come-up without the brief; 1f records the acknowledged model and effort and the first turn's stop reason. |

**The measurement is unchanged**: the install's own next real move, with today's briefed move as the fallback, never turns spent
to test it (§3.2). The package reading of finding 2 cost no turn.

**What stays owed.** Codex's read (D191, its note of 2026-10-24). The design stays proposed until it lands and its blocking
findings are taken; by that note, a design that reverses D189 point 1 is not accepted on one maker's read. SWITCHCTX1c-i, which
reverses nothing, waits only for its own read.
