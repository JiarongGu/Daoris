# A move keeps the agent's own context, and a session goes on as one conversation whatever runs it (SWITCHCTX1, 2026-10-24)

**Standing: proposed, round 5, under D191; its line view, SWITCHCTX1c-i, accepted.** Round 1 was written by Claude Opus 5.5 from
the code on main at `ddf0a24fd` and read at `ff2461081` by the independent Opus foundation reader, every read verbatim in
`docs/2026-10-24-switch-context-reads.md`: 12 findings, 4 blocking, another round. Round 2, written from main at `426ed2b73`,
answered them in §8 and was read by the same reader at `260881273`: 10 findings, 3 blocking, accept after named changes. Round 3
took those in §9, from main at `0d3af537b`. Codex read round 3 at `972b17577`: 26 findings, 17 blocking, another round. Round 4
answered every one in §10, from main at `972b17577`, and adopted Codex's smaller shape: the corrected line view first, then the
carry on the curve's move alone through one durable carry record, falling back before any prompt wherever its proof fails, then the
same protocol on every other move of the same agent. Both makers read round 4 at `4698728a5`: the Opus reader 11 findings, 4
blocking, accept after named changes; Codex 22 findings, 9 blocking, another round. **Round 5 answers all 33 in §11**, written from
main at `e1f4ecb5d`, whose sources hold every line §0 to §7 cite as `972b17577` held them but `ServiceClient.cs`'s, cited anew, and
checked against the pinned adapter's own package (§1.7). The Opus reader read round 5 at `90bbffe4b`: 11 findings, 4 blocking,
accept after named changes the steward can check without another read. **Its named changes are taken, each as the read names it**
(§12), cited at `e1f4ecb5d` as the read cites them; main has moved since, and §12 names what moved. Codex's read of round 5 is owed,
its credits out. Nothing here is built.

**SWITCHCTX1c-i was accepted on 2026-10-25, alone, by the steward under D191**: both makers' reads of round 4 found its round-4 text
buildable, Codex's in its §4 item 2 and §5 item 1 and the Opus reader's in its §3, §4 and finding 11, whose three points §2.7 and §6
now state (where an ended line opens, that pressing an earlier part hides the later ones, and a bound on the parts drawn). It
reverses no decision, and is being built. **The carry (SWITCHCTX1a onward) stays proposed**: it reverses decided rules other designs
rest on (§4 names each), among them D189 point 1 on an agent's own transcript, so by D191's note of 2026-10-24 it is not accepted on
one maker's read. The Opus reader's named changes of round 5 are taken; the carry stays proposed until Codex's owed read of round 5
lands and its blocking findings are taken, and **nothing of the carry is built until then**. The notes on D125, D131, D137, D163,
D165, D167, D189, D197, D206 and D210 say *proposed* and point here; D167's of 2026-10-25 records the line view's acceptance.

The row SWITCHCTX1 in `TASKS.md` carries the case and the owner's direction of 2026-10-24: an account switch, and a switch to
another agent such as Codex, continue the session rather than start a new one. On the install a kit's chat moved at 06:52 UTC from
one account to another over the usage curve, and its agent started afresh: the person read it as a new session, though D167 keeps
every record on the line's one entry as its parts.

A path without a root is under `src/Daoris.Desktop/Daoris.Desktop.Driver/`; page paths are under `src/Daoris.Web/src/`. *An
account's home* is the configuration home Daoris makes per account, `HarnessSettings.ProfileHome(home, agent, account)`
(`Accounts/Harnesses/HarnessSettings.cs:881-882`), its `agent` the adapter's account owner, `Toolchain.Owner(adapter)`
(`Accounts/Harnesses/Harnesses.cs:281`), which is `claude-code` for both of Claude Code's doors: the entry `agent-homes`, at
`profiles/agents/<agent>/<account>` since HOMELAYOUT3 and `harnesses/<agent>/<account>` for an account made before it
(`Platform/DaorisHome.cs:324`, `:373`). This design reaches either only through `ProfileHome`. *The person's own tool home* is the
agent's home outside Daoris's accounts, the person's own sign-in (D197 point 3), where a record names no account.

## 0. The direction, and the two halves

A session is one line of work (D167 point 1). What runs it may change: the account, when the usage curve or a limit moves it
(D165, LIMITGOON1), and the agent, when it is handed over (D163) or its actor's next choice takes it (D205's (e), LIMITGOON2).
The direction is that none of these reads as, or works as, a fresh start. Two halves, because only one of them can keep the
agent's own memory:

1. **The same agent from one of Daoris's accounts to another** keeps its own conversation: the conversation's own files are
   carried to the next account's home, and the same conversation is loaded there by the agent's own load and read back against
   Daoris's record before anything is prompted. Where that proof does not hold, the move goes on as it does today, from Daoris's
   brief, and says so; where its provider refuses the carried part's first turn before any work started, the line goes on in a
   further part from the brief.
2. **Another agent** cannot take the first agent's own conversation: one maker's store is not another's. The line goes on from
   Daoris's record of it, the brief, as LIMITGOON1's and LIMITGOON2's go-on does today, and the page says so.

**On the page, either half reads as one conversation**: the parts the attended one continues drawn above it, the move said once
between, never a fresh session.

**In this order** (§6): the line view first, which serves both halves; then the carry on the curve's move alone; then every other
move of the same agent by the same protocol; and another door only once it can read a carried conversation back.

## 1. Today, read from the code

### 1.1 A move over the curve

- **The decision** is `SwitchDecision.Decide` (`Accounts/Domain/SwitchDecision.cs:83-85`), a pure function returning a
  `SwitchChoice` (`:10`): `Stay` with a reason (`:17`, reasons `:33-48`), `ReadOthers` (`:23`) or `MoveTo(Account, Window, Used,
  Curve)` (`:29`). A move is held for thirty minutes after the last (`Hold`, `:68`).
- **A conversation moves at its turn's end.** `ChatRunner` hands a turn's end to `CurveMove.Due`, or to the limit's move where
  the turn was limited (`Conversations/Chat/ChatRunner.cs:1509-1515`). The curve judges only a turn's end with nothing running,
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
  then `curve` where the walk's step was over the curve or the most room (`Loop/Driver.cs:4615-4626`, called at `:2445`); the
  carry-on opens a new record `from` it (`:2454-2462`), handed the brief (`:2544-2548`), its lines `moved.on` and `moved.from`
  (`SayMoved`, `:4582-4595`, called at `:2549`; `HandOverNotes.MovedOn`/`MovedFrom`, `HandOverRecords.cs:159`, `:169`) and the
  machine log's `session.moved` (`Conversations/Domain/Continuation.cs:585-593`, its field `briefed`). A driven start also reads what
  waited on the ask's channel for the entry's next part into its instruction (`Driver.cs:2563`, `:2643`) and marks it handed once
  the record works (`:2735`).

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
- **An ended record held by its limit** moves by a durable box, `LimitMoveBox` (`Conversations/Chat/LimitBox.cs:16`), holding the
  reason, the refused turns' bounds, the ids of the waiting words and the new part, written at `LimitMove.cs:250` and naming the
  new part at `:269`; the brief is composed again from it (`:296-313`). The live limit move, the curve's take and a driven
  carry-on have no such box.
- **LIMITGOON2**: where no account of its agent has room, `ElsewhereAsync` walks its actor's effective order without that agent and
  opens the new part on the first choice that can run (`LimitMove.cs:92`, `:444`), handed the same brief and words: D205's (e)
  (`docs/2026-10-23-actors-design.md:478-494`), *never the transcript*.
- **Ask Daoris moves only by a limit**, by `LimitMove.HelpStartAsync`, in the room `HelpRoom.GoesOnIn` returns (`LimitMove.cs:420-434`),
  never by the curve (`CurveMove.cs:70-77`) or a hand-over (`HandOver.cs:190-193`). That room is the workspace's, which is not always
  the previous part's (ASKSCOPE1b, `Conversations/Chat/ChatGoOn.cs:648-649`).
- **One conversation on the page, for Ask Daoris only.** Ask Daoris's history folds a part handed the brief into its line
  (`HelpConversation.Parts`, `Help/HelpConversations.cs:22`, the fold at `:233-249`, the link `Brief = "brief"` at `:124`), and
  the page follows a chosen conversation to its newest part (`help/AskConversation.tsx:202-207`) and draws the earlier parts above
  it (`:870-876`), each a `SessionConversation` with `live={false}` on the same scroll region. LIMITGOON1's note says a
  repository's chat *was one session by its parts already* (D189.md:255-258).

### 1.3 A hand-over to another agent

D163 point 1: *a hand-over is a carry-on with its agent named*, in the same tree, handed Daoris's record of it
(`docs/decisions/D163.md:9-11`), since D189 the brief (D189.md:57). D163 rejects *carrying the transcript or a model-written
summary* (D163.md:21-22). It goes by `HandOverTake.RunAsync` as §1.1's move does, the opening line `chat.taken-over` and the
last `chat.handed-over` (`HandOver.cs:270`, `:274`; `HandOverRecords.cs:134`, `:148`). The judge requires no account on the record
or the ask (`HandOver.cs:135-174`), so the person may hand a conversation on their own sign-in to an account, or the reverse.
`chat.taken-over` says where it came from and why, and nothing of what the new agent was given (`locales/en/note.json:202`), where
`moved.from` says *handed Daoris's brief*.

### 1.4 A line's parts, and what the page shows of them

- **One entry, many records.** D167 point 1: a session is *an entry holding one or more ledger records, its parts*, each *one
  harness conversation and one record* (`docs/decisions/D167.md:14-16`). The entry's key is `<origin>/<repository>`, a
  conversation's origin its own `session:<id>`, and a record that continues another takes that one's origin through its `from`
  (`src/Daoris.Service/Daoris.Service.Core/Sessions/SessionEntries.cs:26-37`, `:74`, `:87-89`; written at open,
  `Sessions.cs:818-819`). So a line's key is `session:<first>/<repository>`, `<first>` its first record (the fallback,
  `Trees/SessionGroups.cs:129`). Since D167's amendment of 2026-10-14 an entry may also hold parts running **alongside**, a
  sibling's request in a tree of its own, which continue no part by `from` (`D167.md:73-82`).
- **The page shows one part at a time.** A repository's chat draws the *Parts* strip, one button per part, oldest first, how each
  began read from its `fromWhy`, *Part 2 of 3* (`work/head/PartsStrip.tsx:90-113`, hidden for one part, `:107`), and the
  conversation of the attended part alone (`work/WorkFrame.tsx:2471-2472`). Pressing an earlier part shows it in place of the
  newest, and *every act on the page stays the part shown's* (`PartsStrip.tsx:91-95`). **This is what read as a fresh session on
  the install**: after the move the attended part is the new one, its conversation opens empty under the brief, and the turns
  before it are a press away.
- **A conversation's own view owns its region.** `SessionConversation` reads a part that is not live as ended: `settle` cuts its
  last unfinished turn and marks its open calls stopped (`work/conversation/conversation.ts:881-901`). It follows the tail of the
  region it is handed, or opens an ended one at its head (`work/conversation/SessionConversation.tsx:191`), and tells the frame its
  usage, its wait, its limit's wait and its tail (`:161-200`).
- **How a part was handed** is kept only for Ask Daoris, as `Handed` in its `.help.json` (`HelpConversations.cs:11`, `:156`,
  `:188`); a repository's chat and a driven part keep no such value.
- **The words call it a new session.** `note.moved.on` and `note.moved.from` say *went on in session {{session}}* and *the new
  session* (`locales/en/note.json:204-205`); `note.limit.resume-refused` says *it went on in a new session* (`:213`);
  `work.limit.briefed` says *It goes on in a new session* (`locales/en/work.json:150`) and
  `notifications.done.look.limit-went-on` *went on in a new session* (`locales/en/notifications.json:76`); zh the same at the same
  lines. The glossary's *session* is the whole line and a run is a *part*, *each with its own conversation (D167)*
  (`locales/glossary.json`, rows `session` and `part`).

### 1.5 Where each harness keeps a conversation, per account home

- **The homes.** `HarnessSettings.ProfileHome` (`HarnessSettings.cs:881-882`) is the one way to an account's home, at either
  spelling of `agent-homes` (`DaorisHome.cs:324`, `:373`); its contract says *Daoris owns this location and nothing inside it*
  (`HarnessSettings.cs:878`). `HarnessSelection.Apply` creates it and names it to the agent by its variable
  (`Accounts/Harnesses/HarnessSelection.cs:579-580`): `CLAUDE_CONFIG_DIR` for Claude Code on both doors
  (`Agents/Adapters/Adapters.cs:1930`, `:2368`), `CODEX_HOME` for Codex (`:2113`; `Accounts/SignIns/CodexAccounts.cs:42`). The one
  write Daoris already makes inside one is the trust grant to `.claude.json`, on the person's own act
  (`Accounts/SignIns/ClaudeTrust.cs:69-73`).
- **What writes the transcript.** On the protocol door the adapter's Agent SDK runs a `claude` CLI: the managed one the pipe door
  pins, where one is pinned, by `CLAUDE_CODE_EXECUTABLE` (`HarnessSelection.cs:603-608`, `Adapters.cs:2131-2153`), and otherwise
  the SDK's own (the pinned package's `claudeCliPath`, l. 626-662, used at l. 6638). So the adapter's version does not name the
  program that writes the conversation's file. On the install no `claude` is pinned (its toolchain holds claude-code-acp, codex and
  codex-acp alone), and claude-agent-acp 0.84.0 runs the SDK 0.3.284's own CLI, whose `package.json` says `claudeCodeVersion`
  2.1.284. The record keeps the adapter's version alone (`harnessVersion`). A comment of the code reads another pair,
  claude-agent-acp 0.91.0 on the SDK 0.3.300 (`Adapters.cs:1895`, HELPPLUG1b's reading of plugins); the pin read here is the
  install's. A managed pin lives in the toolchain's own folder per harness and version (`HarnessSettings.ManagedHome(home,
  harness, version)`, as `DoorUpdateTests` installs `claude-code-acp` 0.84.0), so the SDK's `package.json` sits under the
  adapter's own pin, in a folder Daoris installed.
- **Claude Code** keeps a conversation as `projects/<folder>/<id>.jsonl` under its home, `<folder>` the working directory with
  every character but a letter or a digit turned into `-` (`Platform/ToolHomeLeftovers.cs:30-31`, `Flatten` `:55`); its resume
  reads it there and refuses a missing one with `resource_not_found` (`docs/2026-10-02-answer-continues-design.md:36-38`). Read on
  this install's own account homes, 2026-10-24, by listing names only: beside `<id>.jsonl` is a folder `<id>/` holding that
  conversation's `tool-results/` and `subagents/`; the home also holds `.credentials.json`, `.claude.json` (the account's own
  configuration, a tree's trust among it, `ClaudeTrust.cs:82`), `sessions/` (running processes' files), `session-env/`,
  `shell-snapshots/`, `plugins/`, `skills/`, `cache/`, `state/`, `telemetry/`, `backups/`, the policy and remote settings files,
  and `usage-read/`. Claude Code versions have also kept files keyed by the conversation id in other places (`todos/`,
  `file-history/<id>/`, `session-env/<id>/`, `debug/<id>`), which a names-only read on the pinned identity classes (§2.2). A folder
  is the same on every account home for the same tree, since a move keeps its tree (D189 point 2).
- **Codex** files a session by the date it ran under its home's `sessions/`, the folder it ran in said only inside its file, which
  Daoris never reads (`ToolHomeLeftovers.cs:41-42`). No Codex account home on this install holds a session, so the file's name and
  whether it carries the conversation's id are not read here.
- **The person's own tool home** still holds legacy conversations that go on there: a record with no account keeps its folder
  `resumable` (`ToolHomeLeftovers.cs:9`, `:122-123`).
- **The ids.** The agent's conversation id is kept per record in `<home>/sessions/<record>.harness.json`, never read from the
  agent's home (`Conversations/GoOn/HarnessConversations.cs:8-10`, `Keep` `:31`, `Read` `:54`), as `HarnessConversation(Adapter,
  Conversation)` (`Conversations/Domain/GoOnRecords.cs:35`), kept for a chat at `ChatRunner.cs:2396`.
- **The resumes.** The protocol door resumes by `session/resume` where the agent advertises it, else `session/load`
  (`Agents/Acp/Acp.cs:3365`, `:3411-3429`), `resource_not_found` read as `gone` (`:3386`), for claude-code-acp and codex-acp alike
  (`Adapters.cs:1506`, `:1831`, `:2063`). `session/resume` replays nothing; a load's replay arrives as updates and is dropped
  (`Acp.cs:3355-3357`, `:3377`, `:3431-3435`). Neither a load nor a resume names an id on this door: the id is the one asked for
  (`:3359-3362`), kept without telling a listener (`:3396`). A resume carries its record's chosen model and effort again
  (`:3401-3402`). **A choice the account does not offer ends differently by start.** Only the driven start hands a re-ask
  (`ModelRefused`, `Loop/Driver.cs:4326`): there the re-ask's later choice is set on the same session (`Acp.cs:682-712`), and the
  open ends `ChoiceNotOffered` only where no later choice comes (`:684`), caught by the one-shot run (`:820-827`). A chat's session
  is built with no re-ask (`ChatRunner.cs:2618-2628`), so the set's refusal is not judged (`Acp.cs:679`): the model is set aside as
  not carried (`:735-739`, told by `Carried`, `:716`), and the session runs on the account's default. Only the driven run takes
  the one-shot `AcpSession.RunAsync` (`:804-818`), whose resume always sends the run's prompt as its next turn (`:817-853`); a
  chat's session, Ask Daoris's among them, resumes or opens in `ChatRunner.RunAsync` and then waits for words
  (`ChatRunner.cs:2645-2646`; Ask Daoris's own go-on, `ChatGoOn.cs:651-653`). The pipe door is Claude Code's alone, `--resume <id>`
  (`Adapters.cs:2215`, `:2222-2226`, `:2282`; `ChatRunner.cs:1321`); `CodexAcpAdapter` (`Adapters.cs:2063`) is the only Codex
  adapter.
- **No resume follows a move.** A fresh start passes no `GoingOn` to `RunAsync` (`ChatRunner.StartCoreAsync`,
  `ChatRunner.cs:496-642`, the run at `:638-641`), so the protocol door opens with `session/new` (`:2645-2646`). The record's id is
  created inside the start, by the ledger's open (`:586-591`; the driven open `Driver.cs:2454-2462`). The resume's judge,
  `Continuations.Judge`, is the go-on's for a record whose words wait: it answers `ended` where none wait (`Continuation.cs:217`)
  and refuses a record whose account is not the one it ran on, *another account would not find it* (`:177-181`, `:226-229`); its
  callers are the ended-record go-ons (`ChatGoOn.cs:483`, `:525`, `:615`; `Loop/DriverContinuations.cs:61`, `:209`). A refused
  resume kills the process (`ChatRunner.cs:2649-2667`), under the rule that a new session would be *a new conversation under the
  same record* (`:2643-2644`, D137 point 8).
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
  the code reads as *never a new conversation under the same record* (`ChatRunner.cs:2643-2644`).
- D167 point 1: *A part is still one harness conversation and one record* (`D167.md:15`).
- D189 point 1: the agent's own conversation is *one per record* and *never read, copied or written by Daoris*; point 5: never
  handed *anything read or written in a harness's store*; point 6: *A left conversation is not resumed again … going back to its
  agent or account opens another new part*; rejected: *copying or writing a harness's store across accounts or makers* and *going
  back to a left conversation with what came after* (D189.md:14-17, `:33-36`, `:51-54`). The session-context design §9 gives three
  reasons: it acts inside an account's home, it depends on a format the harness changes at will, and it can never cross makers
  (`docs/2026-10-20-session-context-design.md:368-371`).
- D206 (LIMITRESUME1) rejects *copying the transcript to another account's home: D189 point 5, D125's rejection*
  (`docs/2026-10-23-limit-resume-design.md:673`). Its own-account wake sends the refused turns' words again, since it cannot see
  whether the transcript kept them (`:360-366`).
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
- The session-messages design §4.3: the person may open Daoris's conversation in their own terminal with the account's home, and
  *Daoris neither offers nor prevents it* (`docs/2026-10-03-session-messages-design.md:291-295`).

### 1.7 What the record keeps, and what the pinned adapter replays

- **The record of a turn.** It keeps the person's words (`ChatMessage.Words`, `Conversations/Domain/ChatMessage.cs:195-207`); what
  the agent was sent, and stored, is the composed prompt: the brief, the briefing, the told process and the preface, then the words
  (`:181-182`); a go-on sends each word as a block of its own (`Conversations/Domain/ResumeAsk.cs:65-66`). The door maps a
  `user_message_chunk` to a person's `User` event with no id (`Acp.cs:3097-3098`), and the pinned adapter drops a prompt's own
  echo live, *the client already shows it* (package l. 4455-4460), so the record holds no id for a prompt the agent stores. **The
  agent's own messages and thoughts are kept as the wire gave them**, each event with the update's `messageId`
  (`Acp.cs:3091-3096`), and **each tool call with its `toolCallId`** (`:3100-3103`).
- **What a part last ran.** `PriorSession.Ran.Models` is a bounded set of every model any turn reported, first seen first, up to
  eight (`Sessions/Domain/ActorRun.cs:120`, `:142-150`): provenance for a maker's trailer (`LimitMove.cs:471-473`), holding no
  effort. **`Ran.Entries` is not what a part last ran** (round 3 and 4 said it was): an entry is written only at a person's
  composer change (`ChatRunner.cs:2106`), and a new part's watch starts from its go-on's record alone (`:1468`, `goOn?.Record.Ran`;
  a driven run's from its own record, `Loop/Driver.cs:3663-3666`), so a part whose model nobody changed holds none. **What a
  part asked is on its record**: `asked`, kept at a start as the spawn shaped it, best-effort (`TryKeepActorAsync`, where *a refusal
  here costs the record its provenance*, `ChatRunner.cs:1422-1428`, `ActorAsked.ForSpawn`; `Loop/Driver.cs:3663`, where a
  brainstorm keeps none), again where the agent would not take it (`ChatRunner.cs:1547`), and
  `override`, the person's composer change reconciled from what the agent acknowledged (`ChatRunner.cs:2090-2104`); a resume
  carries the first with the second laid over it (`ActorAsked.Overlaid`, `ActorRun.cs:22-27`; `Acp.cs:3401-3402`). Each value
  says its source: `account-default` (nothing named, the account's own settings and the tool's default decide), `entry` or
  `override` (named and set), `command` or `environment` (the machine's, `ActorRun.cs:29-50`). A curve move's new part asks
  no model: the take starts with no order (`ChatHandOver.cs:156-157`), so what it asks is `ActorAsked.Before`, the account's
  default (`ChatRunner.cs:503`, `:1278`; `ActorRun.cs:9`), which sets nothing (`Acp.cs:721`).
- **What a set acknowledges.** `LastAcknowledgedOptions` holds only the list the last set returned, null where nothing was set
  (`Acp.cs:597`, `:604-605`), and `CarryAsync` sets nothing where the option's current value already is the one asked (`:723`); the
  session's current options are what the open and every set left (`KeepOptions`, `:600`). An account's own model and effort are
  in its `settings.json`, which Daoris itself writes on the person's ask and reads with `AgentSettings.Read`
  (`Accounts/AgentSettings.cs:20-47`, `:68`).
- **The pinned adapter's load, read from its package with no turn spent**, as `Acp.cs:3359-3362` read its capabilities: the
  install's pin is claude-agent-acp 0.84.0 (`harnesses.json`'s `versions`), on the Agent SDK 0.3.284, its CLI 2.1.284 (§1.5), read
  for round 3 and again for round 4 from the install's toolchain, its `dist/acp-agent.js`:
  - `loadSession` (l. 1345-1360) reads the transcript by the SDK's `getSessionMessages(id)`, which searches every project folder
    under the home's `projects/` and builds the conversation's chain by its `parentUuid` links, answering an empty list for an id
    it does not find (`sdk.d.ts`, `getSessionMessages`); it then opens the session resumed and replays (`replaySessionHistory`,
    l. 5339-5600).
  - **The model.** Where neither `ANTHROPIC_MODEL` nor the settings' `model` names one, a loaded session runs on the model of the
    transcript's last real assistant record and reports it as current (`resumed-session.js`, `resumedModelFromTranscript`;
    `session-model.js`, `getAvailableModels`, l. 335-365, and `matchResumedModel`, l. 141-162).
  - **The replay.** Every user and assistant message of the chain goes out as updates, each stamped with
    `messageIdForGrouping(message)` (l. 7635-7646): an assistant message's API id, else its uuid, the persisted uuid being the
    live one (l. 5490-5494 says so of a prompt). Live, a streamed message's chunks carry the same API id, taken from its
    `message_start` (l. 4324, l. 4427). **So the agent's own messages replay with the `messageId` they had live**, and a thought with
    its message's. **A tool call replays with its tool use's own id as `toolCallId`, as it had live** (`tool-calls/renderer.js`
    l. 64, 84). A user message replays the composed prompt, local command metadata stripped. The synthetic sign-in message is
    skipped. A compaction's summary replays as user text, since Daoris advertises no compaction updates.
  - **A limit's message.** To a client that takes no typed failures, as Daoris (`Acp.cs:1756-1769`), the synthetic usage-limit
    message is forwarded live as the agent's text, stamped by the same `messageIdForGrouping` (it is skipped live only for a
    client that takes them, l. 4659-4668; stamped at l. 4712), so the record keeps it by its id; on replay it goes out as the
    agent's text again, a typed failure only to a client that takes them (l. 5490-5504).
  - **What else it reads in the home, and where a child's replay goes.** With subagents advertised, as Daoris does where it streams
    (`Acp.cs:1761-1762`), a launched subagent's history is read from the conversation's own `<id>/subagents/` and replayed to a
    child session `<id>:replay-subagent:<tool use id>` (l. 5363, 5405, 5427), announced by `subagent_spawned` on the parent's own
    id (l. 5446-5459); the launch's own tool call is not replayed (l. 5563-5568), as live Daoris keeps the child's card in its place
    (`Agents/Acp/AcpStreams.cs:169-181`). **Round 3 said the door's replay filter already passes over the child; it does not**: the
    filter drops only updates of the conversation's own id (`Acp.cs:3431-3435`), so the announcement is dropped, the child is never
    registered, and its messages, under the child's id, pass the filter, find no child (`AcpStreams.cs:95-99`) and fall through into
    the parent's record (`Acp.cs:2517-2533`). No load reaches this today, since claude-agent-acp advertises `session/resume`, which
    the door takes first (`:3411-3429`); a carried load would, so the carry filters it (§2.3). Nothing else in the home is read for
    the conversation; the spawn reads the account's own settings and sign-in as every spawn does.
  - **A user message's place.** A prompt is persisted with its own uuid before the request it makes (l. 1769-1771, l. 5490-5494),
    and a synthetic usage-limit message is the CLI's answer to that request, so in the chain the transcript's limit message directly
    follows the prompt it refused.

### 1.8 The start, the dispatcher and the record: what each makes durable

Read at `e1f4ecb5d`, the main round 5 is written from; these files are as `972b17577` had them.

- **A chat's start returns before its protocol open.** `ChatRunner.StartAsync` returns once the process is spawned and its watch
  started (`ChatRunner.cs:1609-1630`); the open, `session/new` or a resume, belongs to the watch (`:2645-2646`), and `chat.Opened`
  (`:2647`) is what releases the words queued for it. Meanwhile `HandOverTake.RunAsync` has already said its message, the brief and
  the opening line in it, and written the old part's last line (`HandOver.cs:256-276`), and the live `LimitMove` has queued the
  refused words the same way (`LimitMove.cs:141-172`): a message said before the open goes as the open's first prompt.
- **A protocol session opens once.** `AcpSession`'s handshake refuses a second open on the same session (`Acp.cs:1658`, called by
  `OpenAsync` at `:1614` and by `ResumeAsync`), and once a load is answered, updates of any id that is not a registered child reach
  the session (`:2485-2533`): a `config_option_update` replaces its options and a usage frame is measured.
- **The record and the dispatcher are best-effort by design.** An event the record cannot write costs a console line
  (`Sessions/Events/SessionEventLog.cs:84-93`; `Acp.cs:2554-2565`). A word whose keep the service does not answer waits in the
  process alone (`Conversations/Chat/ChatTurns.cs:155-200`), and a word whose attempt mark fails still goes, *the rare cost of a
  record that did not answer*, except a transient episode's try, which reads the mark (`Conversations/SaidDoor.cs:69-92`). A kept
  word is taken off once written (`ChatTurns.cs:1120-1124`; `Acp.cs:2070-2085`), and the say door answers a key a waiting word
  holds with that word (`.claude/knowledge/session-words.md`). A message's `Brief` is not a word: the say door keeps its text
  alone (`SaidDoor.cs:31-37`), and the take hands its message in memory (`Conversations/Chat/ChatHandOver.cs:238-248`).
- **The ledger's open has no key.** The store makes a fresh id at every create (`src/Daoris.Service/Daoris.Service.Core/Sessions/
  Sessions.cs:799`), and the ledger's opens take no operation key (`SessionLedger.cs:652`, `:1519`): an open whose answer was lost
  is found again only by what it wrote, its `from` and its why.
- **What a turn ran is known in memory.** The door sees each update of the turn's own session as it arrives, before the record
  keeps it (`Acp.cs:2485-2533`: an agent, tool call or plan update marks the oldest open prompt's turn `Heard`, `:2502-2512`).
- **The limit's opener and readers.** `LimitHeld` reads a turn refused only where its account's cooling line came before its
  `error` end (`LimitHeld.cs:31-37`), never a turn that ended `refusal`; the take's judge refuses a hand-over to the same agent on
  the same account (`HandOver.cs:160-166`); and a lost conversation on its own account goes on briefed by `BriefedMove`, why
  `resume` (`Conversations/Chat/BriefedMove.cs:127-148`), which takes words only from `said` (`:98-101`). Ask Daoris's history
  writes `Handed` as `brief` when its new part is placed, before any outcome (`LimitMove.cs:422-424`).
- **The name `KeepHandedAsync` is taken**: it keeps the downloaded versions a start was handed (KEEP1b,
  `Platform/DownloadKeeping.cs:33`, `Loop/Driver.cs:3015`).
- **A driver-made change reaches the windows** through a hook the loop wires to the publisher, as the help scope's is
  (`Daoris.Desktop.Modules/Loop/DriverLoop.cs:35-36`, `WiredKeptTold`; `Daoris.Desktop.Modules/Platform/StatePublisher.cs:263`).
- **The carried lines need their words first.** A note code the driver writes fails `NoteCodesTests` until both catalogues word
  it (`Daoris.Desktop.Driver.Tests/Platform/NoteCodesTests.cs:8-11`).

## 2. The design

### 2.1 Which moves carry

A move **carries** where all of these hold, and only there. They are judged by a carry judge of its own, `Carries.Judge`, pure
but for the looks it is handed, beside `Continuations.Judge` and never through it (the go-on's judge answers `ended` with no
words waiting, §1.5). Each refusal is a code, in the log (§2.3); a refusal by the move's kind (`agent`, `own-home`, `tree`,
`room`) is today's briefed move and says nothing more on the page, and every other refusal is said on the new part
(`moved.carry-failed`, its reason worded from the family `carry`, §2.7):

| Condition | Refused as |
|---|---|
| the agent is the same (its adapter unchanged) | `agent` |
| **both records name an account of Daoris's**: the old record's `Profile` and the start's settled `selection.Profile`, each non-empty, each a profile of that agent's owner (`Toolchain.Owner`); a part on the person's own sign-in, on either side, moves by the brief (D49 §4, D66 §3, D197 point 3) | `own-home` |
| the account changes | (not a move) |
| the tree is the same (every move keeps it, D189 point 2), so the conversation's folder is the same name in both homes, and the tree's path is one Claude Code is proven to flatten as `Flatten` does: absolute, ASCII letters and digits only besides what becomes `-`, and a flattened name of at most 200 characters, past which Claude Code hashes a name of its own (`ToolHomeLeftovers.cs:30-36`, `:49-50`), an unproven name refused as `folder` and said on the page (§2.7); for Ask Daoris, the room `HelpRoom.GoesOnIn` returns is the previous part's tree | `tree`, `folder`, `room` |
| the old record kept a conversation id (`HarnessConversations.Read`), held to the grammar `[A-Za-z0-9-]{1,128}` | `unkept`, `id` |
| the old part's process has exited: its watch ended, not the fifteen seconds (§1.1), and the process registry holds none for it | `busy` |
| the door can read a carried conversation back: the protocol door where the table's row names the replay's anchor (§2.2), and the pipe door never until SWITCHCTX1e gives it a read-back of its own (§3.3); and the line's parts that name the kept id hold at least one agent message with a `messageId` | `door` |
| **the compatibility identity is the same on both sides and the carry table has its row** (§2.2): the adapter's version and the version of the CLI it runs (§1.5), the old part's as kept beside its conversation id (§2.3) and the start's. **The start reads the CLI's version** from the pin where `claude` is pinned (`HarnessSelection.ClaudeExecutable`, its version the toolchain's pin), else from `claudeCodeVersion` in the SDK's `package.json` under the adapter's own managed pin (`HarnessSettings.ManagedHome`, §1.5), once per pin; an adapter that is not a managed pin, run from `PATH`, has no identity. This refuses a carry across either version, which D131 point 2 never refuses for a resume (§4); a conversation kept with no identity, before SWITCHCTX1a, is refused | `version` |
| until SWITCHCTX1f has measured the cross-account case, **what ran is the same**, judged below on the old record's `asked` with its `override` laid over (§1.7), never on `Ran.Entries`, which a part whose model nobody changed does not hold, and never on `Ran.Models`, a provenance set | `model` |

The moves that meet it: D165's curve move of a conversation (`CurveMove` → `HandOverTake`) and of a quest's session (the carry-on,
why `curve`); LIMITGOON1's move to another account of the agent (`LimitMove`, live or ended); D206's briefed move of a limit-held
conversation to another account that frees first; the person's own hand-over to another account of the same agent; a quest's
carry-on why `limit`; and Ask Daoris's limit move (`HelpStartAsync`) where its room is the previous part's tree. Everything else
(another agent, the person's own sign-in on either side, a teammate's record, an intake, a tree or room that differs, an id never
kept) goes by the brief, as today. SWITCHCTX1a builds the first of them alone, a conversation's curve move; the rest are
SWITCHCTX1b's, by the same protocol (§6).

The judge runs twice: **before the old part ends**, on what is known then (the agent, the old record's account, its tree, its kept
id, the door and the line's anchor, the identity, the old part's asked model and effort), so a hand-over that will carry asks no
closing note (§2.4 step 2); and **after the start's selection settles the account**, on all of it, the old process's exit among
it. Only the second decides whether the carry record is written (§2.3). The model's last condition is judged on the wire after
the load, since what ran is known only there; a refusal there falls back at the open (§2.5).

**The model, judged on what ran.** What the old part ran, as Daoris knows it, is its record's `asked` with its `override` laid over,
`ActorAsked.Overlaid` (§1.7): the very values a resume of that record carries today. A record keeps `asked` at its start, so the
owner's own case, a chat whose model nobody changed, holds it as `account-default`; **a record whose keep of `asked` failed, or a
brainstorm's, holds none and refuses `model`**. Each of the model and the effort is judged by its source, and the carried start
hands no re-ask on any path, the driven one included: a re-ask would set another model on the carried conversation
(`Acp.cs:682-712`).

- **A value named and carried** (`entry`, `override`) is asked again, carried, as a resume asks it. After the load and
  `CarryAsync`, what runs is read from the session's **current options** (§1.7), and from `LastAcknowledgedOptions` only where a
  set returned one: the option's current value is the one asked, or the agent's own alias of it as `CarryAsync` takes it
  (`Acp.cs:742-751`), and `Carried` was not told (`:716`). **A named `default`** (the tool's alias, kept as the option's value,
  `ActorRun.cs:5`, `:64-97`) names no model, so it is judged as `account-default` below, never set on the loaded session, where it
  would pass while the new account's default ran.
- **A value the account's default decides** (`account-default`) is asked as nothing, so nothing is set: the pinned adapter's
  load lands on the transcript's own last model unless the account's settings or the environment name one, and only where the
  destination offers it (package `session-model.js` l. 141-162, l. 335-366). An account's default is its plan's as well as its
  `settings.json`'s, so it holds only where **both accounts agree on both**: `AgentSettings.Read` of each account's
  `settings.json` names the same model, the same effort and the same effort per model, none on both counting as the same
  (`AgentSettings.cs:68-84`), **and** both accounts' offer readings, which every open keeps (`KeepOffers`, `ChatRunner.cs:2122-2131`),
  list the same model choices; an account with no reading kept refuses `model`. Then the load runs the transcript's model on an
  account that offers what the source offered, and the effort is the same setting. Accounts that differ refuse `model` at the second
  judgement, before anything is copied.
- **A value the machine names** (`command`, `environment`, `ActorRun.cs:29-50`) is the same for both starts on one machine, and is
  compared, never set.

Where a named value's set is refused, or another value runs, or `Carried` is told, the carry falls back `model` (§2.5), and the
fallback opens `session/new` with **the choice today's move would have asked** (a curve move's or a hand-over's: the account's
default, `ChatHandOver.cs:156-157`; a limit's move or a driven carry-on: its own start's, with the re-ask that start hands today),
never the refused one again, so a move that runs today never ends without going on because it tried to carry. Round 3 and 4 judged
on `Ran.Entries` and `LastAcknowledgedOptions`; the first is empty where nobody changed the model, and the second is null where
nothing was set, so the carry would never have run in the owner's case (§11, Opus 1).

### 2.2 What is copied, and what never is

The carry copies **the conversation's own files and nothing else**, named by its id, whole and byte for byte. Daoris never parses
them, never edits them and never writes into them. What it does is a narrow read and write inside two account homes, by name only:
it reads the previous home's files of a conversation the line owns and writes them into the next home where nothing of that name
stands. That amends the homes' contract, *Daoris owns this location and nothing inside it* (`HarnessSettings.cs:878`), for these
names alone; its one precedent is the trust grant to `.claude.json` on the person's act (`ClaudeTrust.cs:69-73`). Both homes are
reached only through `HarnessSettings.ProfileHome(home, Toolchain.Owner(adapter), account)` with the accounts §2.1 settled, never by
a path read from a record, so the person's own tool home is never a source or a destination. Besides these names, the judge reads
each account's own model and effort as Daoris already reads them, `AgentSettings.Read` of the `settings.json` it writes (§2.1).

**The carry table**, per agent, door and **compatibility identity**, beside the adapters. Each row classes **every place in the
home where a name holds the conversation's id** on that identity, read by listing names only, as `copy` or `never`, and names
**the replay's anchor**, read from that identity's package (§2.3); an identity with no row carries nothing (`version`), a row with
no anchor carries nothing (`door`), and the row is read again when a pin moves.

| Agent and door, identity | `copy` | `never` (each name holding the id) | Replay's anchor | Read on |
|---|---|---|---|---|
| Claude Code over its ACP adapter: claude-agent-acp 0.84.0 on the SDK 0.3.284, running that SDK's own CLI, 2.1.284 (no `claude` pinned) | `projects/<Flatten(tree)>/<id>.jsonl`; `projects/<Flatten(tree)>/<id>/` where it stands (its `subagents/` is read by the load, §1.7) | `todos/<id>*` (the agent's own plan, which Daoris's record holds), `file-history/<id>/` (rewind checkpoints, which no resume needs), `session-env/<id>/` (a process's environment), `debug/<id>*` | the agent's own messages and thoughts by `messageId` and its tool calls by `toolCallId`, the same live and on replay (§1.7) | the anchor read from the package for rounds 3 and 4 (§1.7); the names written by SWITCHCTX1a from a names-only listing of an account home on that identity; §1.5's listing of 2026-10-24 names the first two and `session-env/`. A `claude` pinned, or another SDK, is another identity, with no row until it is read |
| Claude Code's pipe door: the `claude` CLI's own version | the same names, once read on that version | the same, once read | none known: its `--resume` replays nothing to Daoris (§3.3) | not read: SWITCHCTX1e's |
| Codex (codex-acp) | the one file under `sessions/` whose **name** holds `<id>`, at the same relative path | not read | not read | not read: no Codex home here holds a session (§1.5); the row is SWITCHCTX1d's, and until then Codex moves by the brief |

**Held to its identity, in the checkout.** A row names the exact packages and versions, the adapter's, the SDK's and the CLI's.
SWITCHCTX1a checks in, beside its stand-in, a fixture of the replay's frames as read from that package, naming each identity and
the lines read (§1.7), so the stand-in follows a checked-in reading, never this document's or a reader's memory of it. It also
holds **the two readings that do not fail safe**, since a misreading of either runs the carry rather than falling back: which CLI
the SDK runs with no `CLAUDE_CODE_EXECUTABLE` (`claudeCliPath`, l. 626-662), on which the identity rests, and the model a load
restores (`resumedModelFromTranscript`, `matchResumedModel`), on which an account-default carry rests. The pair a code comment names
(`Adapters.cs:1895`) has no row.

**Never copied, per agent**, whatever the table says. For every agent: the account's credentials, its own configuration, its
history of prompts across conversations, another conversation's files, its plugins, skills, caches, telemetry, policy and usage
files. For Claude Code: `.credentials.json`, `.claude.json`, `settings.json`, `history.jsonl`, `sessions/`, `session-env/` and
`shell-snapshots/` (running processes' files). For Codex: `auth.json`, `config.toml`, and every file under `sessions/` but the one
its row names, since `sessions/` is where Codex keeps every conversation. A name the row does not class `copy` is not copied. An id
outside the grammar of §2.1 is refused before any path is built (a `:` names an alternate data stream on Windows), and a resolved
path that leaves the home, or passes through a link, is refused; either falls back (§2.5).

**How it lands: never over a name that stands.** Only once the old part's process has exited (§2.1, `busy`). Before writing
anything, every name the row classes `copy` is looked for at the destination; where any stands, the `<id>.jsonl`, the `<id>/`
folder or anything in it, the carry writes nothing and falls back (`exists`). Each entry is copied to a temporary name of the
carry's own beside its destination (the old record's id in it) and moved into place by a move the disk refuses where the name
already exists, never one that replaces: the `<id>/` folder first and the `<id>.jsonl` last, so the conversation is there only once
whole. **Each name is written to the carry record before its move** (§2.3), so a shell that ends mid-copy leaves names the record
calls its own. A move refused, because another client made the name in between, is `exists` too; what this carry already moved
into place stays, as the copy of a carry that fell back does, and its temporary names are removed. **Why never replace**: no exclusion can be
held against a client Daoris does not run. The person may open Daoris's conversation in their own terminal with the account's home,
which Daoris neither offers nor prevents (§1.6), and a look followed by a replace could write over turns made between them, which
D137 rejects (`D137.md:56`). Round 3 replaced a destination that was a byte prefix of its source; a prefix shows how the two files
stood when read, not that nobody writes before the move (§5). **The cost, in the owner's terms**: with the curve's thirty-minute
hold and two or three accounts, a long session comes back to an account it ran on, and every such return goes by the brief there
(`exists`), as every curve move does today; the next move carries the conversation that fallback opened. With two accounts, about
every other move carries. SWITCHCTX1f counts `exists` among the outcomes; setting the left copy aside (renamed, never deleted and
never written) is a later decision, only if that count shows it is worth one, and no row is opened for it now.

The source is **copied, never moved or removed**: the left conversation stays in its account's home, and the line's next part owns
the copy. **A carried line leaves one copy of its conversation in every account home it ran on**, as a line leaves one today, and
a copy left by a carry that fell back stays where it landed. Each goes with its home when the account is removed (D66 §3), and
nothing else removes it: a delete or a clear never removes the harness's conversation from an account's home
(`Sessions/History/SessionHomeFiles.cs:34-35`). `ToolHomeLeftovers` lists only the person's own tool home
(`ToolHomeLeftovers.cs:25-27`), which a carry never touches.

**Reasons, against §1.6's.** *It acts inside an account's home*: it does, narrowly, by name, on the line's own conversation only,
after its writer has exited, never over a name that stands, never on an account's credentials, identity or another conversation,
and never on the person's own tool home. *It depends on a format the harness changes at will*: the table is held to an identity,
the adapter's and the CLI's versions, so a new version carries nothing until its row, its anchor among it, is read; and since a
change that keeps the names but moves meaning elsewhere would load without the turns, nothing is prompted until the load's replay
agrees with Daoris's record (§2.3), so a load missing turns falls back rather than going on. Daoris never parses the file: it reads
the agent's wire, which states the anchor. *It can never cross makers*: it does not, which is why the other half is the brief.

**A carried transcript names its first home.** A persisted tool output's path inside the file names the home it was written in
(§3.1); the carry copies `<id>/tool-results/` beside it, so the outputs are in the new home at the same relative place, but the text
still names the old. Once that account is removed (D66 §3), those paths point nowhere, and the agent reading one finds nothing, as
it would for any file gone since. SWITCHCTX1f records whether the agent reads them at all.

### 2.3 The ids, the record, the carry record, and where the carry runs

- **The record.** A carried move is still a **new part**: a new record on the new account, `from` the old with the move's why
  (`curve`, `limit`, `press`), since a record names one account (D131 point 2). The entry is unchanged,
  `session:<first>/<repository>`. **Each record names one conversation id, the first it names, and never another** (§2.5): two
  records of one line name the same id, each in its own home, and only the newest part's is ever resumed. A copy left behind is
  never resumed: the carry never writes over it (§2.2), so a move back to its account goes by the brief, as D189 point 6 has it.
- **The carry record.** One durable record per carried move, `<home>/sessions/<old record>.carry.json`, keyed by the record the move
  leaves, as D206's box and D163's `HandOverBox` are, and in the session's file inventory under `hand-over`
  (`SessionHomeFiles.cs:52-53`, `:126`), so a delete and a clear take it with the record. It holds:
  - its **phase**: `judged`, `opened`, `copied`, `verified`, `delivering`, `falling-back` (below), and the moment it was written;
  - the **source and destination**: the old record, its kept id, the two accounts by profile name, and, once opened, the new
    record;
  - the **compatibility identity** (§2.2);
  - the **anchor**: the line's records that name the kept id, oldest first, whose events the replay is counted against;
  - the **brief's recipe** (§2.5): the move's why, the bound of the events the brief is composed up to, and the agent's own
    hand-over note where one was written (`HandOver.cs:247-251`);
  - the **pending delivery**, its disposition and its keys (§2.4 step 5): none, a limit's (the refused turns' bound and the waiting
    words' ids), or a driven instruction's recipe and the channel's held messages' ids; each word of it under a key of the carry's
    own, `carry/<old record>/<n>`, and the ids the say door answered, once it answered them;
  - each name this carry writes, written here **before** its move (§2.2);
  - at `falling-back`, the fallback's code, its target (the same record at the open, or a further part `from` the carried one,
    §2.5) and, once opened, that further part.

  Never a word's text, a path or an account's key. **It is the one owner of a carried move's delivery** until the delivery is kept
  on the new record: on the ended-record move, D206's box keeps the bounds and ids it keeps today (`LimitMove.cs:250`, `:269`), the
  carry record names that box, and the box's come-up hands the delivery to the carry record's rule instead of composing it
  (§2.4). Each step below writes its phase before the next begins, and each is safe to run again. A carry record whose new part has
  itself moved on before its first answer (a record `from` it exists, **other than the further part its own `falling-back` names**,
  §2.5) is ended there: the newer move's carry record owns the line from that point, its anchor covering every part that names the
  conversation. The carry's own further part never ends its carry record: that record stays until the further part's message is
  written.
- **Where it runs.** The carry is an ask handed to the start, `CarryAsk` (the old record, its kept id, the move's why, and the
  brief's recipe), taken by the three starts that open a moved part: `ChatRunner.StartAsync` (the curve's take, the person's
  hand-over, LIMITGOON1's live move, D206's ended move), the driven open (the carry-on, `Driver.cs:2445-2462`), and
  `LimitMove.HelpStartAsync` (`LimitMove.cs:420-434`). A chat's start returns before its open (§1.8), so steps 1 to 5 run in the
  start and steps 6 and 7 in its watch, and **the start hands its caller the open's outcome**: `ChatStart` gains `CarryOutcome`, a
  task the watch completes once, with `verified`, with the fallback's code once the fallback's own conversation is open, or with
  `stopped` (below). It is named apart from `AcpSession.Carried`, `ProtocolChat.Carried` and `RotatedOpening.Carried`, which the same
  flow already reads. In order:
  1. the selection settles the account (`LimitMove` passes `profile: null`, so its account is known only here);
  2. `Carries.Judge` decides, on that account (§2.1);
  3. the carry record is written: `judged`;
  4. the ledger opens the new record, which creates its id (`ChatRunner.cs:586-591`; `Driver.cs:2454-2462`; `PlaceHelpAsync` for
     Ask Daoris), and the carry record names it: `opened`. **An open whose answer, or whose `opened`, was lost is found again** by
     what it wrote (§1.8): the record `from` the old one with the move's why, created after the carry record's moment; one found
     is adopted as opened, none means the open never landed, and more than one ends the carry record, the move left as a shell's end
     leaves it today and said in the log (`carry.ambiguous`). **Ask Daoris's open writes no `from`** (`ChatRunner.cs:783-785`;
     `LimitMove.cs:7-8`), so its lost open is found by its opening line, which names the session it carries
     (`ChatRunner.cs:790-792`); none found is a move not yet made, nothing prompted;
  5. the copy (§2.2), each name written to the carry record before its move, and then `copied`;
  6. the spawn; the agent's `session/load` where it is advertised, not `session/resume`; the old part's model and effort judged and,
     where named, set (§2.1); the replay counted (below). A repository's chat and Ask Daoris's new part load by `ChatRunner.RunAsync`,
     the chat's protocol path (`ChatRunner.cs:2645`; Ask Daoris's by `LimitMove.cs:432-435`, as its own go-on does,
     `ChatGoOn.cs:651-653`); the driven run by the one-shot `AcpSession.RunAsync`'s `resume` (`Acp.cs:804-818`);
  7. where everything held, the new record's `harness.json` is written as `HarnessConversation(adapter, carried id, identity)`
     through a write that says whether it landed and reads it back, since `HarnessConversations.Keep` swallows a failed write
     (`HarnessConversations.cs:31-43`); then `verified`, and `CarryOutcome` completes. Where anything failed, or the write did not
     land, the fallback at the open (§2.5), which completes `CarryOutcome` with its code.

  **A stop, a closing shell or a hang is not a refusal.** A stop the person asked for during the carried open
  (`SessionProcesses.WasStopRequested`, as the watch reads it, `ChatRunner.cs:2488`, `:2509`), or the shell closing, completes
  `CarryOutcome` as `stopped` and opens nothing; the carry record stays for the next word. Only the agent's refusal on the wire, or its
  process ending on its own, falls back. The open is bounded: a load that has not answered within five minutes, a first value
  SWITCHCTX1f reads again, counts as `load` and falls back, and the take's wait on `CarryOutcome` is bounded by the same, since it
  holds the line's go-on meanwhile (`ChatHandOver.cs:128-131`).

  **Nothing is prompted in steps 1 to 7, and nothing is said to the new part before `CarryOutcome` completes.**
  `HandOverTake.RunAsync`, `LimitMove` and `HelpStartAsync` await it **before** they compose or say anything to the new part, write
  either part's line, write `session.moved` or `.help.json`'s `Handed` (today written as `brief` at placing,
  `LimitMove.cs:422-424`): a carried part is said no brief; a fallen-back one is said the brief and the words today's move says.
  **What goes first**: the new part's turns exist from `RunAsync` (`ChatRunner.cs:1524-1551`), and a word said to it during the
  load is queued for the open (`ChatTurns.cs:638-673`; `ChatRunner.cs:2909`). So `chat.Opened` (`ChatRunner.cs:2647`, its
  release `:2801-2806`) releases **the delivery, or the fallback's message, first, and then the words kept since the part's start,
  in their order**; until that first item is queued, the turns hold every other word. A curve's move, whose delivery is none,
  releases the kept words alone, the first of them its first turn. On the driven path the driver's run does steps 6 and 7 before its
  prompt, and the carry-on's `SayMoved` (`Driver.cs:2549`) moves after that open. The record names no conversation until step 7, so a fallback before it opens the
  record's first conversation, never a second (§2.5). Every later part of the line keeps its identity beside its id the same way,
  at its open, so the next move can compare it (§2.1).
- **Read back before anything is prompted.** `session/resume` replays nothing, so a resume the wire accepted proves only that the
  agent found an id (`Acp.cs:3355-3357`). A carried start loads and counts the replay the door already receives and drops (`:3377`,
  `:3431-3435`). D131 rejected load-first because the record already holds the history (`D131.md:47`); a carry is the one place
  where the replay is the check.
  - **Only the conversation's own replay counted, and none of it kept.** While the load is answered, **every** update, whatever
    its id, is kept out of the record and leaves the session's options as the load's answer set them (§1.8), so a child's replay,
    whatever shape its id takes (§1.7), and an option the replay moves reach neither; the count reads the updates of the
    conversation's own id alone.
  - **The unit**: the agent's own messages and thoughts, grouped by `messageId`, and its tool calls, grouped by `toolCallId`, in
    order. On the record's side, the `Message`, `Thought` and `Tool` events of **every part of the line that names the kept id**,
    oldest first (a part's own record holds only its own turns, since a load's replay is never kept), a subagent's card, whose id
    is the child's, and a plan excepted. On the replay's side, the load's `agent_message_chunk`, `agent_thought_chunk` and
    `tool_call` updates of the conversation's own id; a subagent's launch is not replayed as a call (§1.7), as the record holds its
    card instead.
  - **It is read back** only where the two lists are **equal**: every id of the record's in the replay, in the same order, and
    none in the replay the record lacks. An extra is a turn the record does not hold, another client's or a version's, and falls
    back. There is no exception for a limit's message: no reader in Daoris reads a limit from the agent's text (`LimitHeld` reads
    coded cooling lines, `LimitHeld.cs:17-56`; `AccountLimits.Read` reads the door's failure alone, never the agent's messages,
    `Accounts/Usage/AccountLimits.cs:63-67`), and the record already holds that message by the id it replays with (§1.7). Where a
    real CLI gives it another, the carry falls back, the safe side, and SWITCHCTX1f records it. Otherwise `short-replay`.
  - **What stands after the last answered turn**, read by place, never by words: the replay's groups (user messages by
    `messageId`, and the record's matched items) after the last item of the line's last turn that ended otherwise than by a limit.
    Where no words wait (a curve's move, a person's hand-over) there must be none. After turns a limit refused there must be, for
    each refused turn in order, **exactly one user message directly followed by that turn's own limit message**, matched by the id
    the record holds (§1.7): the limit message is the CLI's answer to the prompt it follows, so that user message *is* the refused
    turn's prompt, whatever words it batched. Anything else, a refused turn whose limit message the record does not hold, a user
    message not directly before one, two before one, or one after the last, falls back before any prompt, `suffix`. So a refused
    prompt is known to be in the transcript by its place in the chain, never by a count (§2.4 step 5).
  - **What it proves, and what it does not.** It proves that the transcript the agent loaded holds exactly the agent's own messages,
    thoughts and tool calls Daoris's record holds, in order, nothing of the agent's after them, and each refused turn's prompt in
    its place. It does not prove the person's prompts' text (the agent stores the composed prompt and the record the words, §1.7),
    a tool result's content or the thinking's signatures, and it does not show what the agent retains in its working memory. An
    event the record could not write (§1.8) leaves an id the replay holds and the record lacks, so the lists differ and the carry
    falls back: a lost event costs the carry, never a wrong count. The first-turn rule (§2.5) catches a provider's
    refusal of the history, and SWITCHCTX1f measures the rest. *Read back* says no more than that, on the page or in the log.
  - **An identity whose replay holds nothing that can be matched** carries nothing: its row names no anchor (`door`, §2.1), and the
    new part says so. A line that holds no agent message with an id (a part that never answered, or one written by a version that
    stamped none) is refused the same way, at the first judgement.
  - A compaction inside the part (the harness's own, D189 point 7) replays its summary as user text (§1.7); where the chain it
    builds leaves out the messages before it, the lists differ and the carry falls back, which is the safe side; SWITCHCTX1f records
    how often.
  This reads the agent's wire, which the door receives on every load today, and never its store.
- **No id is named on the protocol door.** Neither a load nor a resume names one (`Acp.cs:3359-3362`): `ResumeAsync` keeps the id it
  asked for and tells no listener (`:3396`). So no `forked` arises here; it can arise only on the pipe door's `init` line, which is
  SWITCHCTX1e's (§3.3).
- **How it was handed** is kept for every kind of part, as a machine-only field of the session record, `handed`: `own` (its own
  conversation carried and read back), `brief` (Daoris's brief handed), or absent (a part handed neither: a first part, a resume, or
  a carried part not yet read back). It goes through every place of the chain `.claude/knowledge/session-record-fields.md` names for
  a field set at the open (the service's record and store, the ledger's write, the host's request and response, the driver's write
  and read, the three stand-ins, the web's type), with no field on the sync wire, which a test holds; a teammate's mirror carries
  neither it nor any conversation id. **A briefed move writes `brief` at the open**, today's among them. **A carried part's is set
  once, after its open**: `own` at step 7, `brief` where the open falls back (§2.5), and it never changes after. So the field also
  has its own update door, as a field set later does (`session-record-fields.md:40`), on the shape of `/actor`'s:
  - the store's `SessionStore.KeepHowHandedAsync(id, handed)` and the ledger's `SessionLedger.KeepHowHandedAsync`, beside
    `KeepActorAsync` (`Daoris.Service.Core/Sessions/Sessions.cs:1408`), taking only `own` or `brief`, on a record of this machine's,
    with no sync revision, as `/actor`'s writes none (`:1407-1423`); named so because `KeepHandedAsync` is KEEP1b's already (§1.8);
  - the route `POST /api/sessions/{id}/handed`, answered on loopback alone, not found off it, as `/actor` is
    (`Daoris.Service.Http/Program.cs:1398-1409`), and **its row in `PersonDoors.Table` as the driver's door**,
    `new("POST", "/api/sessions/{id}/handed", DoorClass.Driver, …)` beside `/actor`'s (`Daoris.Service.Http/PersonDoors.cs:256`),
    with `PersonDoorTableTests`' row and its class's count, so a call without the key is refused;
  - the client's `ServiceClient.KeepHowHandedAsync`, beside `KeepActorAsync` (`Service/ServiceClient.cs:2380`);
  - **the push**: `handed` named in `SessionsStateSlice.Fields` (`Daoris.Desktop.Modules/Loop/SessionsStateSlice.cs:39-47`), which
    `SessionsStateSliceTests` holds against the page's `Session` type, and the driver's write followed by a declared change of the
    sessions, the slice's wake (`:62-67`), since the door writes no revision the change stream would carry
    (`.claude/knowledge/pushed-slices.md`). **The seam**: the driver tells a hook, `HandedTold(session)`, which the loop wires to
    `StatePublisher.Publish(bus, StateScopes.Sessions, session)` (`Daoris.Desktop.Modules/Platform/StatePublisher.cs:263`), as it
    wires the help scope's `WiredKeptTold` (`Daoris.Desktop.Modules/Loop/DriverLoop.cs:35-36`);
  - their tests: `SessionStoreTests` (written, read back, null for a record from before and for a mirror), the host's (loopback
    only, `ContinuesHostTests`; a keyless call refused, `PersonDoorTableTests`), the driver's (the body written), and the slice's (the
    value read through the pushed projection, not only the service's answer).
  Its readers: the *Parts* strip's *began* words, the line view (a part `own` draws no brief, §2.7), and Ask Daoris's fold, whose
  room records take no `from` (`LimitMove.cs:7-8`), so `HelpStartAsync` also writes `.help.json`'s `Handed` as `own` beside
  `transcript` and `brief` (`HelpConversations.cs:118`, `:124`), once `CarryOutcome` has completed, `brief` where it fell back.
- **The machine log** keeps one field for how a move handed its part: `session.moved`'s `briefed` (`Continuation.cs:585-593`),
  false where the carry's open was read back and true where it fell back at the open. One line of its own, `conversation.carried`,
  says what held, once, when it is known: the agent and its identity, the two accounts by profile name, the files' count and bytes,
  the replay's matched items against the record's, the refused turns found in place, the delivery's disposition, the model and
  effort that ran after the load, the first turn's stop reason and whether work started in it (§2.5), and the outcome: `held` (the
  first turn answered, or started work), `stopped` (§2.3), or the fallback's code (`refused <why>` at the judge, `folder`,
  `not-found`, `exists`, `unkept`, `model`, `short-replay`, `suffix`, `load <why>`, `first-turn`; `forked` only on the pipe door,
  SWITCHCTX1e's). It keeps no class of the provider's words: a `refusal` is a stop reason, not a door failure, and no `DoorFailure`
  is built for it (`AgentErrors.cs:47-52`); SWITCHCTX1f reads the provider's class from the record by hand. Never a word, a path or
  an account's key.

### 2.4 The move, step by step

1. The turn ends; the move is decided as today (`SwitchDecision`, the limit's walk, the person's press).
2. The first carry judgement (§2.1). A person's hand-over that will carry asks the outgoing agent **no closing note**
   (`ChatHandOver.cs:145-149`), since its own turns go with it; where the carry later falls back, the brief goes without the note,
   as a forced move's does today.
3. The old part ends, as today (`EndForHandOverAsync`), and the move reads whether its watch ended or the fifteen seconds did.
4. The new part's start (§2.3): the selection, the second judgement, the carry record, the open, the copy, the spawn and the load,
   the model's set and judgement, the replay's count, the checked `harness.json`. **Nothing is prompted in this step.**
5. **The delivery**, once `CarryOutcome` completed with `verified` (§2.3), by the carry record's disposition, released by
   `chat.Opened` ahead of any word said to the new part during the load (§2.3):
   - **none: a curve's move or a person's hand-over** (no words wait, §1.1). **Nothing is prompted.** Nothing may stand after the
     last answered turn (§2.3). The resumed chat waits, as a resumed chat's session already does (`ChatRunner.cs:2645-2646`), and
     the person's next words are its first turn, with one line of Daoris's before them saying the account changed. No turn is spent
     on the new account that nobody asked for;
   - **a limit's** (SWITCHCTX1b). Every turn a limit refused since the line's last answered turn in this conversation, across the
     line's parts that name it (each read by `LimitHeld.Of` from Daoris's coded lines, never a sentence), must stand in the replay
     as one user message directly followed by its own limit message (§2.3), which proves its prompt is in the transcript. So **no
     refused word is ever handed again**: the first prompt is the words that waited (said while the line was limited, never yet
     prompted), then Daoris's word to go on, kept through the say door under one key of its own, `DaorisKeys.LimitCarried(<old
     record>)`, beside `LimitReset` and classed with the resume keys a refusal takes off (`IsResume`,
     `Conversations/Domain/DaorisWords.cs:163-179`): *This conversation now runs on another account, after its last account's limit.
     Go on with the work where you stopped.* (the agent's English, as `LimitResetWord` is, `LimitMove.cs:563-566`). The waiting
     words' files resolve against the old record, as `LimitMove` resolves them today (`LimitMove.cs:158`, `:308-309`), and the words
     leave the old record by their ids once the new record's say door has kept them, as `LimitMove` takes them off at delivery
     (`:160-165`, `:331-334`).
     Where any refused turn is not found in its place, the carry falls back before any prompt (`suffix`), and the fallback hands the
     refused words and the waiting words once, in a conversation that holds neither, as `LimitMove` does today. **Consecutive
     limited carries**: a carried part whose first turn the new account's limit refuses moves again by this rule; its own prompt,
     which held the words that waited before it, is one more refused turn, found in its place, so none of its words is handed
     again either, whatever order the carries came in;
   - **a driven carry-on's** (SWITCHCTX1b): its instruction composed as today without the brief, composed before the spawn so the
     fallback can hand it, the ask's channel's held messages in it (`Driver.cs:2563`, `:2643`), marked handed once the record works
     (`:2735`); where the driven part's last turn was refused by a limit, its refused turns must stand in place as above, and the
     instruction takes the waiting words' place.

   **How the delivery is kept, and who owns it.** The carry record writes `delivering`, with each word's key, before anything is
   kept: one key per word, `carry/<old record>/<n>` for the person's words and `DaorisKeys.LimitCarried` for Daoris's.
   Each word is kept through the say door under its key, **checked**: a keep the service does not answer `Kept` stops the
   delivery there, the words staying in the carry record, never in the process alone (the general dispatcher's fallback, §1.8, is
   not taken), and the next look or the come-up tries again; a retry under the same key is answered with the word already waiting
   (`.claude/knowledge/session-words.md`). The ids the say door answered are written to the carry record, and only then does
   `chat.Opened` release them to the door (§2.3). Each is marked attempted before the wire, **checked** as a transient try's mark
   is read (`SaidDoor.cs:69-92`): a mark that fails sends nothing, and the word waits for the next try. A kept word leaves `said`
   only once written (§1.8), so the carry record and the service together say where each word is: listed and waiting, not
   attempted, it goes; listed, waiting and attempted, it is uncertain, settled by the dispatcher's rule and never sent again; listed
   and no longer waiting, it was written; not listed, the say door's key finds it waiting or it was never kept. The carry record is
   removed only once the carried part's first turn answered (`held`).
6. The lines are written, after `CarryOutcome` completed: the old part's last, the new part's first (§2.7), and `session.moved`. Until
   SWITCHCTX1c-ii words the carried lines, a carried move writes today's lines (`chat.moved` and `chat.moved-from` for the curve),
   since a code the driver writes fails `NoteCodesTests` until both catalogues word it (§1.8).

**Recovery, at every boundary.** The carry record is the authority until the carried part's first turn answers, and the come-up
reads it before it takes up any record, as it reads D206's box (`LimitMove.cs:352`, `ChatGoOn.cs:189`):

| The shell ended | The come-up |
|---|---|
| before the carry record was written | as today: the old part ended and no new part opened; a limit-held conversation waits by D206, a driven line by its next look |
| at `judged` | the open is looked for by what it wrote (§2.3 step 4): found, the record is `opened` and goes on below; not found, a move not yet made, made again from the record by the same start where its delivery is pending (a limit's, a driven one), and dropped where none is (a curve's move, a hand-over), the line left as a shell's end leaves it today |
| at `opened` or `copied` | nothing was prompted, and whatever the new record's `harness.json` names is not trusted before `verified`. Where a delivery is pending, the start runs again from the copy, writing `harness.json` again; a name the record lists is this carry's own where its bytes equal the source's, any other is `exists`; the temporary names are removed. **A curve's move or a hand-over, with nothing pending, is left to its next word**, whose go-on takes the carried way from the phase reached |
| at `verified` or `delivering` | the record names the carried id and nothing was prompted unless the delivery's words say otherwise: each is found where §2.4 step 5 says and goes, waits, is settled uncertain or is kept now, **at the come-up**, by the carried way below, not at a later word |
| at `falling-back` | the carry is never run again: the fallback is finished from its record (§2.5); where its further part is open and its message not yet written, the come-up hands that message once, its brief recomposed from the recipe |
| a turn answered under the carried id since `verified` | `held`: the carry record is removed |
| a record `from` the new part exists, **other than the further part this record's `falling-back` names** | the new part moved on: this carry record is ended, the newer one owning the line |

**The carried way.** Wherever a carry record names a record as its new part and has not ended, that record's go-on, which today
resumes by `session/resume` with no read-back (`Acp.cs:3368`; `ChatGoOn.cs:480-586`), takes the carried start's way instead: the
load, the model's judgement, the count, the delivery and the first-turn rule (§2.5). **Its delivery is derived afresh at each go-on
from the line's records**, never kept from the move that wrote the carry record: the refused turns found in place, then the words
waiting, then the go-on's own word (D206's `limit.reset` at its own-account wake). So a part carried by the curve whose first turn
its account's limit then refused, waiting for D206's own-account wake, reads its refused prompt in place at the wake and goes on in
its own conversation, never `suffix`. A driven record takes it the same way through
its go-on and its come-up (`Loop/DriverContinuations.cs:61`, `:209`, `:1220`). Since the record already names the carried id, a
fallback there opens a further part (§2.5). On the ended-record move the come-up reads D206's box as today (`LimitMove.cs:352`,
`:366-379`), and where a carry record names the box, the box composes nothing: the carry record's rule delivers, without the
brief and without a refused word where its phase is `verified` or later, and by the fallback's rule where it fell back.

### 2.5 When it does not hold: the brief, said

A carry that does not hold falls back to today's move, never to a wait and never to the person. **The brief is kept on the move
until the carry is done**: not its text but its recipe, in the carry record (§2.3): the old record, the move's why, the bound of
the old part's events the brief is composed up to, and **the agent's own hand-over note where one was written**
(`HandOver.cs:247-251` puts it in the brief, and the old record does not hold it). A brief recomposed from the recipe is **composed
as of then**: it reads the old part's events to the bound, which no longer change, and the tree's uncommitted state as it then is
(D189 point 4), so it is the move's brief but for what the tree did since, which is the current state the new part works in.

**Before any prompt, at the open**, in the same start:

- the second judgement refuses (§2.1), or the table names no file, or the file is not there (`not-found`);
- a name the row copies stands at the destination, or a create-only move is refused (`exists`, §2.2);
- the write of the carried id does not land (`unkept`, §2.3);
- the protocol door refuses the load: `resource_not_found` → `gone` (`Acp.cs:3386`), or any other refusal on the wire (`load <why>`);
- a named model or effort is refused, another runs, or the accounts' own settings differ (`model`, §2.1);
- the replay's lists differ (`short-replay`), or what stands after the last answered turn does not fit the delivery (`suffix`).

**The fallback is journaled before it opens anything.** The carry record writes `falling-back`, with its code and its target, the
same record, before anything else. The loaded process is ended and the record's run is spawned afresh for `session/new` **with the
choice today's move would have asked** (§2.1; never a refused one again): a protocol session opens once (§1.8), and a fresh process
holds nothing of the loaded conversation. **Where the respawn runs**: a chat's start has returned before its open, so the respawn is
the watch's again-path, `AgainAsync` (`ChatRunner.cs:1617-1628`, `:1638-1644`), wired for the carry beside the native go-on it
serves today, with the record not concluded on the failed load (the watch's conclusion, `:2495-2515`, waits for it); a driven one
is respawned in the driver's own run (`Loop/Driver.cs:4315-4379`), since `AcpSession` is built over a running process's streams
and spawns nothing (`Acp.cs:308-320`). The record's `harness.json` names the new conversation as today's start names it, when the
wire says it (`onConversation`, `ChatRunner.cs:2623`). **The record names one conversation, the fallback's**: it named none before
step 7, the carried conversation was loaded and never prompted, and nothing of its replay was kept. So this needs no amendment of
D137 point 8. At the open there was no turn and no tool call; the load did run the CLI's session-start hooks, and the fallback's
`session/new` runs them again, as any new conversation does. `CarryOutcome` completes with the code; the record is marked
`handed: brief`; the new part's boundary says the carry failed and why (`moved.carry-failed`, §2.7); `conversation.carried` says the
code. **The fallback's delivery** is the brief recomposed from the recipe, then the words today's move hands, as `LimitMove` and
`HandOverTake` hand them, in one message kept through the say door under the carry's own key, `carry/<old record>/fallback`,
checked as §2.4 step 5's words are, and released first by `chat.Opened`, before any word said during the load (§2.3); since the say
door keeps a message's text and not its `Brief` (§1.8), the message's brief is recomposed from the carry record's recipe wherever
that key's word is taken up: when the dispatcher hands it to the door, and when a go-on takes up the waiting message after a shell
ended (`ChatGoOn.cs:185` on, `ResumeAsk`), which is not the dispatcher. **The carry record is removed only once that message is
written** (it left `said`, §2.4 step 5). At the come-up a record at `falling-back` is never carried again: an opened fallback with
its message waiting goes on; one whose message was attempted is uncertain, its words settled by the dispatcher's rule and never sent
again, and the next word said to the record goes with the brief recomposed once more, since a brief twice costs its length and none
costs the agent its context; one not yet opened is opened. On the driven path the briefed instruction is composed before the spawn,
so the respawned run can hand it (SWITCHCTX1b).

**At the carried part's first turn**, after the prompt, judged by how the turn ended, in D210's four kinds
(`Agents/Errors/AgentErrors.cs:5-19`) and its stop reason, **and by whether work started**: the door's own mark of the turn,
`WorkStarted`, set in memory in the update handler, after the replay filter and before the streams route the update
(`Acp.cs:2489-2519`), beside the `Heard` it sets today (`:2502-2512`). **Any update of the turn sets it**, of the conversation's own
id or of a child's, **other than the agent's message and thought chunks** (`agent_message_chunk`, `agent_thought_chunk`) **and the
session's meters and settings** (`usage_update`, `available_commands_update`, `current_mode_update`, `config_option_update`,
`session_info_update`): so a `tool_call`, a `plan`, an `async_task_spawned` and a subagent's launch, which arrives as
`subagent_spawned` on the parent's id, the `Agent` call being off the wire where streams are taken (`AcpStreams.cs:19-21`,
`:81-82`, `:152-180`), each set it, and so does any update of a child, whose tools run on its own id. It is independent of whether
the record could write the event. It shows that no work started, not that nothing at all ran: the prompt's own hooks run before the
model, and run again with the fallback's prompt as with any prompt. Never judged by whether the agent's message came first: a
provider's refusal is said first as the agent's message, its `API Error`, and the turn then ends `refusal` (`Acp.cs:144-151`,
APIREFUSAL1).

| How the first turn ended | What it is | What follows |
|---|---|---|
| stop reason `refusal` (`AcpOutcome.Refusal`) **with no work started**: APIREFUSAL1's `Fatal` reading in D210 (`D210.md:75`) | the provider's own refusal of the request, on the wire, before any work: the carried history, another account's or model's signed thinking, or the prompt itself, which the log cannot tell apart (§2.3) | the carry's failure: **a further part**, below, `first-turn`. It is **no strike**, as D210 point 6 makes a `refusal` one, since the further part answers it; the further part's own turns are read by D210 as any turn's (§4) |
| `refusal` **after work started** | not the carry's: something ran in the carried conversation, a subagent's work among it, and a brief with the same words in a new one could run it again | the turn's own end, handled as any turn's is today (D210, a strike on the driven path as point 6 has it); the carry counts `held`, `handed` stays `own` |
| a `Fatal` failure, at the prompt or after it | not positive evidence of a refusal before execution: `Fatal` includes a failure Daoris cannot read (`AgentErrors.cs:17`) | the turn's own failure, said as today; the carry stays pending, and the next turn is judged as the first |
| a `Limit` (`LimitHeld.cs:7`, `:37`) | the account's limit, not the carry's | **the limit's move**, SWITCHCTX1b's, by §2.4's limit rule; no brief is handed on the limited account |
| a `Transient` failure, tried again in the same conversation by the door (`AgentErrors.cs:14`; `Acp.cs:847-853`) until its bound runs out | not the carry's | the carry stays pending: the turn ends as a transient's does today, and the next turn is judged as the first, by the carried way where the process has gone (§2.4) |
| an `Access` refusal | the account's, which no brief on it mends | the carry stays pending, and the account's access is said as today |
| the person's stop (`cancelled`) before any answer | the person's | the carry stays pending; nothing is handed again |
| any other end | the carried conversation answered | `held`: the carry record removed, `moved.carried-from` stands |

**The further part.** The carried part ends as a part whose turn was refused: its record keeps the carried conversation's id, its
events and its usage, so what the provider said and counted stays with the conversation that said it. The line goes on in a new
part `from` it, why `retry` (*its own conversation could not be resumed*, `HandOverRecords.cs:41`, `:53`), on the same account.
Nothing today opens it for a chat (the take refuses the same account, and `LimitHeld` reads no `refusal`, §1.8), so each path's
opener is named:

- **A chat's**: **`BriefedMove`'s body** (`Conversations/Chat/BriefedMove.cs:127-148`), its why `retry` in place of `resume`, run
  past `MovingAsync`'s gates (a live record, words on `said`, `:86-103`): the carried part is ended first, and its words come from
  the record, not `said`. `BriefedMove`'s body is the chat's opener alone.
- **A driven line's**: **the look's own carry-on of the failed run** (D80; `Loop/Driver.cs:2445-2462`), why `retry`, handed the
  brief and the instruction, since `BriefedMove` refuses an open quest's record and turns a closed one into a conversation
  (`BriefedMove.cs:26-28`, `:92-96`, `:130-135`). One further part opens, the look's, never two.

The carry record goes to `falling-back` with the further part as its target before it opens, and **names it once it opens**; the
fallback's journal and delivery are the ones above. **Its brief is composed `before:` the first turn of this conversation that no
answer followed**, the earliest turn refused by a limit across the parts naming the id, else the carried part's first turn, as
`LimitMove` bounds its brief before the refused turns (`LimitMove.cs:127-129`, `:303-305`), since a brief carries every word the
session took (`Conversations/Domain/ContextBrief.cs:178-194`) and would otherwise hand those words twice. **Its words are the
person's words of every such turn, in order**, read by a reader of their own, `FirstTurnWords` (`Conversations/Domain/`, beside
`LimitHeld`), from the records by the word ids, never from `said`, which no longer holds them; a chat's `User` event carries no id
(`ChatRunner.cs:2198-2204`), so the reader pairs each word's `receipt` event, which does (`Acp.cs:2070-2077`), with its `User`
event by place. On a limit carry these are the refused words and the words that waited, so the request the provider refused is the
further part's to answer, not history alone. Its boundary is `moved.carry-failed` (§2.7), and `handed: brief`. Round 3 opened the new
conversation under the carried record, which would leave the refused turn's events and usage under a record that no longer names
the conversation that made them (§5). A fallback is never tried again by spending a turn: the next move tries the carry afresh.

### 2.6 Another agent: the brief, as today

A hand-over to another agent (D163), LIMITGOON2's go-on with its actor's next choice (D205's (e)) and a pick of another agent
carry nothing of the agent's own: a Claude transcript is not a Codex thread, and writing one maker's store for another is what
D189 rejected and this design keeps rejected. The line goes on from the brief, as `HandOverTake` and `LimitMove.ElsewhereAsync`
already hand it. **What changes is only how it reads**: one conversation across its parts (§2.7), its boundary saying that the new
agent has Daoris's brief and not its turns.

### 2.7 On the page: one conversation, either half

- **A repository's chat draws its line, not its part.** Above the attended part's conversation, the pane draws **the parts it
  continues**: its ancestry by `from`, oldest first, the part it came from, that part's, and so on, each read-only, as Ask Daoris
  already draws its earlier parts (`help/AskConversation.tsx:870-876`). `WorkFrame` maps them before the attended
  `SessionConversation` (`work/WorkFrame.tsx:2471-2472`).
  - **Alongside is not ancestry.** A part of the same entry running alongside (§1.4) continues none of these by `from` and is not
    drawn into the line: it stays a press away in the *Parts* strip, and in COWORKVIEW1's view where that lands.
  - **Read-only is not ended.** Each earlier part is drawn with its own record's liveness, never `live={false}` for being earlier,
    since `settle` reads a part that is not live as ended, cutting its last turn and stopping its open calls (§1.4). An ancestor is
    ended in every move this design makes, but the view does not assume it. Its controls are withheld by a mode of its own: no box,
    no acts, no waits.
  - **One scroll owner.** An earlier part takes a mode in which `SessionConversation` neither follows the region's tail nor opens it
    at its own head, and tells the frame nothing of its usage, its wait, its limit's wait or its tail (§1.4), so the attended part
    alone owns the region's scrolling and the frame's facts. Ask Daoris's earlier parts take the same mode, which they lack today
    (`help/AskConversation.tsx:872-874`).
  - **Where a line opens.** An ended line opens at the region's top, as an ended session does today
    (`work/conversation/followTail.ts:49-61`): the attended record's head, which says how the line ended, with its *last words* and
    the way to the bottom beside it, then the earlier parts drawn, oldest first, then the attended part. A live line opens at its
    tail and follows it.
  - **Pressing an earlier part hides the later ones.** Attending an earlier part draws that part's own ancestry above it, so the
    parts after it are no longer drawn; the strip still names them, and pressing the newest draws the whole line again.
  - **A bound.** At most the three earlier parts nearest the attended one are drawn. Above them one press draws up to three more
    each time, and says how many it will draw: *Show the part before* or *Show {{count}} earlier parts*
    (`work.parts.earlier_one`, `work.parts.earlier_other`; zh *显示前一段*, *显示更早的 {{count}} 段*). Each part drawn still pages
    its own events as a conversation does today (`useSessionEvents`' earlier pages).
  The *Parts* strip stays; **pressing a part attends it and scrolls to it**, so every act on the page stays the part shown's
  (`PartsStrip.tsx:91-95`). The strip is also the rail of `PartsFlow` (`PartsStrip.tsx:102-103`), whose press attends and scrolls
  the same way. A driven session's line shows the same. A teammate's record shows only what it shows today (D47 §4).
- **Between two parts, one line on each side, once.** Where a part began by a move or a hand-over, the boundary is the old part's
  last line and the new part's first, and nothing else:
  - **the curve's figures**: where a move carries, its two lines are the carried pair below alone, never `chat.moved`'s pair beside
    them; the old part's line, `moved.carried-on`, holds the decision's window, used and curve where the why is the curve, as
    `chat.moved` holds them today, and the new part's names only where it came from;
  - **the opening line**: `RotatedOpening.Say` writes the new part's account line at its open (`ChatRunner.cs:607-608`;
    `Accounts/Rotation/AccountRotation.cs:600-610`). The record keeps it, and the line view folds it under the new part's boundary
    line, as a briefed part's brief is folded, so it never draws as a third line; a part that did not begin by a move draws it as
    today;
  - **a carry that falls back**: at the open, the new part's first line is `moved.carry-failed` and the old part's last is today's;
    after a refused first turn, the carried part keeps `moved.carried-from` and ends with today's `moved.on`, and the further part
    begins with `moved.carry-failed`. No line is drawn in place of another.
  Where a part did not begin by a move (a sibling's request of the same ask, D167 point 2, a *Try again*, a resume into a new
  part), it has no move lines, and the page draws its *began* words from the strip as its boundary, one line. No separator of the
  page's own, no *new session*, no greeting drawn as a start. A carried part draws no brief, since none was handed; a briefed part's
  brief stays folded as today.
- **Ask Daoris's history** folds a part handed `own` into its line as it folds `brief` (`HelpConversations.cs:233-249`).
- **The words.** Moving and resuming are said in the session's words: a part, never a new session. **A line written before keeps
  reading**: a reworded code keeps exactly the values its lines were written with (`moved.on` and `moved.from`: `session`, `agent`,
  `account`, `why`, `tokens` and `count`, `work/noteLines.ts:300-301`), so H and B stay on every briefed move's line (D189 point 8)
  and no line needs a value it was never given; and the carried lines, whose values differ, are new codes, as the code's convention
  has it (`noteLines.ts:235-242`). No line names a part by its number, which the strip says.

| Code | en | zh |
|---|---|---|
| `note.moved.on` (old part, last line, a briefed move; reworded, its values as written) | *Went on as a new part of this session, {{session}}, with {{agent}} as `{{account}}`, because {{why}}: its context stood at {{tokens}} tokens, and the new part was handed Daoris's brief of it, {{count}} characters.* | *以本会话的新一段 {{session}} 继续，由 {{agent}}（账户 `{{account}}`）运行，因为{{why}}：其上下文为 {{tokens}} 个 token，新的一段收到了 Daoris 对它的简报，{{count}} 个字符。* |
| `note.moved.from` (new part, first line, a briefed move; reworded, its values as written) | *Continues the part before it, {{session}}, which ran on {{agent}} as `{{account}}`, because {{why}}: its context stood at {{tokens}} tokens, and it was handed Daoris's brief of it, {{count}} characters.* | *接续前一段 {{session}}（由 {{agent}} 以账户 `{{account}}` 运行），因为{{why}}：其上下文为 {{tokens}} 个 token，它收到了 Daoris 对它的简报，{{count}} 个字符。* |
| `note.moved.carried-on` (new; old part, last line, a carried move not by the curve; `tokens` its context at its end, D189 point 8) | *Went on as a new part of this session, {{session}}, with {{agent}} as `{{account}}`, because {{why}}, in its own conversation: its context stood at {{tokens}} tokens.* | *以本会话的新一段 {{session}} 继续，由 {{agent}}（账户 `{{account}}`）在它自己的对话中运行，因为{{why}}：其上下文为 {{tokens}} 个 token。* |
| `note.moved.carried-over-curve` (new; old part, last line, a carried move by the curve, `chat.moved`'s figures and `tokens`) | *Moved to `{{to}}` at the end of its turn, as a new part, {{session}}, in its own conversation: `{{from}}` had used {{used}} of its {{window}} limit, over its curve's {{curve}}, and its context stood at {{tokens}} tokens.* | *在本轮结束时移至 `{{to}}`，以新的一段 {{session}} 在它自己的对话中继续：`{{from}}` 已用掉其 {{window}} 额度的 {{used}}，超过其曲线的 {{curve}}，其上下文为 {{tokens}} 个 token。* |
| `note.moved.carried-from` (new; new part, first line, written once its replay was read back) | *Continues the part before it, {{session}}, which ran as `{{account}}`, because {{why}}, in its own conversation: its earlier turns were read back.* | *接续前一段 {{session}}（原账户 `{{account}}`），因为{{why}}，在它自己的对话中继续：已读回它先前的轮次。* |
| `note.moved.carry-failed` (new; a new part's first line where the carry fell back, at the open or after a refused first turn; `why` from the family `carry` below) | *Its own conversation could not go on as `{{account}}` ({{why}}), so it continues the part before it, {{session}}, from Daoris's brief of it, {{count}} characters.* | *它自己的对话无法在账户 `{{account}}` 上继续（{{why}}），因此凭 Daoris 对它的简报（{{count}} 个字符）接续前一段 {{session}}。* |
| `note.chat.taken-over` (reworded, its values as written) | *This conversation went on from {{agent}} conversation {{session}}, handed over because {{why}}; it was handed Daoris's record of it, not its own turns.* | *这段对话是从 {{agent}} 的对话 {{session}} 转交过来继续的，原因：{{why}}；交给它的是 Daoris 对其的记录，而非它自己的轮次。* |
| `note.limit.resume-refused` (reworded, `{{why}}` kept from the family `continue`) | *Its own conversation could not be resumed ({{why}}), so it went on as a new part with Daoris's brief.* | *无法回到它的原对话（{{why}}），因此它带着 Daoris 的简报以新的一段继续。* |
| `work.limit.briefed` (reworded) | *It goes on as a new part of its session, handed Daoris's brief.* | *它将以会话的新一段继续，并附上 Daoris 的简报。* |
| `notifications.done.look.limit-went-on` (reworded) | *A quest's work, cut off by an account's limit, went on as a new part of its session, handed Daoris's brief* | *因账户额度用尽而中断的工作已作为会话的新一段继续，并附上 Daoris 的简报* |
| `work.parts.began.carried` | *went on from {{after}} in its own conversation* | *接{{after}}，在它自己的对话中继续* |

**The family `carry`**, the reasons `moved.carry-failed` says, each a key of its own (`note.carry.why.<code>`) in both catalogues,
read by a family beside `handed` and `continue` in the note reader (`work/noteLines.ts:795-806`), never the raw code; a code the
family does not know reads as none, as the other families do:

| Code | en | zh |
|---|---|---|
| `busy` | *its earlier process had not stopped yet* | *它之前的进程尚未停止* |
| `door` | *Daoris cannot read it back on this way of running the agent* | *以这种方式运行智能体时，Daoris 无法读回它* |
| `version` | *Daoris has not read this version of the agent for it* | *Daoris 尚未为此读过这个版本的智能体* |
| `unkept` | *Daoris could not keep its id* | *Daoris 无法保存它的 ID* |
| `id` | *its id is not one Daoris carries* | *它的 ID 不是 Daoris 可携带的形式* |
| `model` | *the account would not run it on the same model and effort* | *该账户无法以相同的模型和推理强度运行它* |
| `not-found` | *its files were not found* | *未找到它的文件* |
| `exists` | *the account already held a copy of it* | *该账户已存有它的一份副本* |
| `gone` | *the agent did not find it there* | *智能体在那里没有找到它* |
| `refused` | *the agent would not load it* | *智能体拒绝加载它* |
| `short-replay` | *what the agent loaded did not match Daoris's record* | *智能体加载的内容与 Daoris 的记录不符* |
| `suffix` | *it held words after its last answer that Daoris's record does not* | *它在最后一次回答之后含有 Daoris 记录里没有的内容* |
| `first-turn` | *its provider refused its first turn there* | *服务方拒绝了它在那里的第一轮* |
| `folder` | *Daoris cannot name its folder the way the agent does* | *Daoris 无法按智能体的方式命名它的文件夹* |

`chat.moved-from` and `chat.moved` keep their words for a curve move that did not carry. `chat.handed-over` keeps its words: the old
part's line says where it went, and only the new part's says what it was given. Where a sentence names a quest or an ask, zh says
委托 and 需求; a *Try again* is 重试 (the glossary's rows). The glossary's *part* (段) and *conversation* (对话) are the words; the
row *part*'s *each with its own conversation* is amended to *each its own record; a part carried from another account of the same
agent goes on in the earlier part's conversation*. `i18n:check` and `names:check -- --strict` hold the keys; `note.test.ts` holds a
line for every code of the family `carry` in both languages and `{{why}}` in `moved.carry-failed` and `limit.resume-refused`, so
the parity of keys is not the only check.

## 3. What is unmeasured, and how it is measured

### 3.1 Unmeasured

**Read at no turn's cost** (§1.7), for rounds 3, 4 and 5: where a prompt and its limit message stand, what the pinned adapter's load replays, the ids it stamps on
messages, thoughts and tool calls, where a child's replay goes, how a limit's message is forwarded live, and which CLI the adapter
runs. The replay's unit and the stand-in's shape follow from that reading. What stays unmeasured:

- Whether Claude Code, on the pinned identity, loads a conversation whose two entries (`<id>.jsonl`, `<id>/`) were copied from
  another account's home on a real account, and whether a record inside the file names the first home (a persisted tool output's
  path does) in a way that matters (§2.2).
- Whether a real replay's lists equal the record's, a limit's message among them, and how often a compaction inside the part, or a
  stopped turn's marker, makes them differ.
- Whether the API takes the earlier turns' signed thinking under another account; until SWITCHCTX1f, a carry runs the same model
  and effort, judged on what ran (§2.1), and a provider's refusal of the history is seen at the first turn (§2.5). The
  account-rotation design saw a conversation outlive a re-sign-in in the maker's CLI
  (`docs/2026-10-01-account-rotation-design.md:338`), in the same home, which lowers the risk without measuring it.
- **What the agent retains.** A replay that agrees shows what the transcript holds, not what the agent keeps in its working
  memory; only a carried part's first answer, read by the person, says that, and nothing here claims it.
- What a carried part's first request costs: the prompt cache is per account, so it writes the whole context H, where a briefed
  move writes S and B (`docs/2026-10-20-session-context-design.md:275-279`); whether it reads anything from a cache is not known.
  No way on is claimed cheaper (§4, D189 point 7).
- Codex's file's name, its replay's anchor, and whether codex-acp's load finds a file it did not write in that home.
- The pipe door's `--resume` of an id that is not there (LIMITRESUME1f's question), and whether the CLI offers any read-back of a
  resumed conversation without a turn (§3.3).

### 3.2 Measured on the install's own next real move, never by spending turns

No turn is spent to test it, and the account-switch design's two-turn evidence row is withdrawn. The build lands with every
fallback of §2.5, so the first real carried move on the install **is** the measurement: it either holds or falls back, before any
prompt or at its first turn, with nothing lost. Where the carry does not reach a real move, today's briefed move is what runs, and
nothing is claimed. The `conversation.carried` line holds the outcome; the parent reads it from the machine log after the install's
next carried move and writes it as a dated note on D165, naming **exactly which case ran** (the move's kind, curve or limit or the
person's, and whether its delivery was none, a limit's or a driven one), the agent's identity, the outcome, the replay's matched
items against the record's, the refused turns found in place, the model and effort that ran after the load, the first turn's stop
reason and whether work started, a subagent's launch among it, the provider's class of any refusal read from the record by hand
(§2.3), the first turn's cache write (the turn's `cacheWrite`, as the usage history already keeps it,
`Accounts/Usage/UsageHistory.cs:659`), the part, and how many moves since the build found `exists`, the owner's cost of the
create-only copy (§2.2), and how many first turns started a subagent. **Every other case stays unmeasured and is named so in the note**: a move back to an account holding a
left copy, a compaction inside the part, a provider's refusal at the first turn, a shell ended partway, a limit's carry,
consecutive limited carries. Until that note, a carried part is said as read back, never claimed as
measured, and the design's claim stays *unmeasured*. An identity with no row carries nothing (`version`); its row is then read on
that identity.

### 3.3 The pipe door carries nothing until it can read a conversation back

The pipe door puts the person's words into `--resume`'s own arguments or stdin before the harness names its conversation
(`Adapters.cs:2222-2226`; `docs/2026-10-23-limit-resume-design.md:407-418`), and it has no load whose replay Daoris receives, so
nothing on it can play the part §2.3's read-back plays. Its file is written by the `claude` CLI it runs, so its row is keyed by
that CLI's version, never the ACP adapter's (§2.2). The pipe door therefore carries only once all three hold: LIMITRESUME1f has
recorded on the real door that `--resume` of a missing id fails closed; the pinned CLI is read to offer a read-back of a resumed
conversation without a turn, equal to §2.3's (its messages and tool calls by id, before any prompt); and SWITCHCTX1a's protocol is
built for it to share. None is known today, so until SWITCHCTX1e it moves by the brief (`door`).

## 4. What it amends, each by a dated note (proposed)

Every decided rule the carry changes, each amended for a carry alone; none is said to stand where it does not.

- **D125**, its contract's §3.5 point 5, its point 4 and its rejection of copying between account homes: the line's own
  conversation, and only it, crosses from one of Daoris's account homes to another of the same agent, never over a name that
  stands; the credentials and everything else of an account's own never do, and the person's own tool home is never a source or a
  destination.
- **D131 point 4** (*one Daoris record for one harness conversation*), its rejections of *a linked record that the page folds into
  one thread* and *resuming the conversation in a new record*, its rejection of *`session/load` first*, and its point 5's carry-on
  after a refused turn: a carried move resumes one conversation in a new record, by load, read back before any prompt. **D131's
  answer**: its reason was that every reader of records would have to learn a fold (`D131.md:37-38`, `:41-46`); D167 has since made
  the entry that fold for every reader, the page, the review and the history; `session.moved` counts a move, not a session; and a
  teammate's mirror carries no conversation id and no `handed`, so a teammate still sees one row per part as today. Each record still
  names one conversation id. Point 2's *the same account* reads as the account whose home holds the conversation, which a carry
  makes the new one. **Point 2's last sentence**, *a changed harness version is said on the resumed run's first line, never refused*
  (`D131.md:18`), and the rejection of *refusing a resume across a harness version* (`:50-51`): a carry is refused across its
  compatibility identity, the adapter's and the CLI's versions (`version`, §2.1), since the table's names and the replay's anchor
  are bound to it; a resume of a record on its own account stays never refused. The rejection of *checking for the conversation in
  the agent's home before resuming* (`:48-49`): a carry checks there, by the table's names alone, in the two account homes it
  settled.
- **D137's rejections**, beside its point 8 below: *a new record continuing the old conversation, linked to it* (`D137.md:53`),
  which is the carry's shape, answered as D131's is; and *reading the account's home* for a conversation (`:55`), which a carry
  does, by its kept id and the table's names, never by `--continue`, `--last` or a name. Its rejection of *adopting a terminal
  conversation in place* (`:56`) is kept, and held more strictly: a carry never writes over a name that stands (§2.2).
- **D189 point 7** (*Daoris's compaction is a new part handed the brief: at a move*, `D189.md:37-38`) and its reason's *cheaper on
  every later request* (`:46-48`): a carried move compacts nothing and keeps H on every later request on the new account. The
  answer is the session-context design's §5 as amended: *no way on is claimed cheaper*
  (`docs/2026-10-20-session-context-design.md:288-289`); the agent's own conversation is tried first for continuity, its first
  request's cost is stated (§3.1, §5) and kept unmeasured until SWITCHCTX1f, and the person's *Go on fresh* still compacts by the
  brief.
- **D137 point 8** (*one record per harness conversation*): a carried conversation is named by two records, each in its own home,
  and only the newest part's is resumed. That is the whole amendment: **no record ever names a second conversation**, since a
  fallback before any prompt is the record's first conversation and a fallback after a refused first turn opens a further part
  (§2.5). Round 3's same-record replacement is withdrawn.
- **D165** point 3 and its design §4: a move over the curve with the agent unchanged, between two of Daoris's accounts, carries the
  conversation, built first for a conversation's curve move; the evidence row is withdrawn; the first real move measures the case
  that ran.
- **D167 point 1** (*a part is still one harness conversation and one record*): a part is one record, and a part carried from
  another account of the same agent goes on in the earlier part's conversation. The page draws above the attended part the parts it
  continues, each with its own liveness, alongside parts left to the strip, and every part keeps how it was handed.
- **D189 point 1**'s *one per record* and *never read, copied or written by Daoris*, and **point 5** with its rejection of copying
  a store across accounts: the same agent's own files may be copied whole between Daoris's account homes at a move, by name only,
  never over a name that stands and never parsed or written into; the carried conversation is named by each part of the line that
  carried it. **Point 6 and its rejection of *going back to a left conversation with what came after* stand**: a copy left behind
  is never written over or resumed, and a move back to its account opens another new part handed the brief. Across makers nothing
  changes. Point 4's brief becomes the first half's fallback. **Point 3's last sentence**, *a driven fallback, a carry-on after a
  limit and a hand-over stay new parts, handed the brief in place of the four facts* (`D189.md:25-26`): within one agent between
  two of Daoris's accounts, a carry-on after a limit and a hand-over are still new parts, but handed the agent's own conversation,
  the brief where the carry does not hold. **Its *Why***, a brief carries a session *without reaching into an account's home*
  (`:45-46`): the carry reaches into two of Daoris's account homes, by the table's names alone (§2.2), and the brief still never
  does.
- **D197**'s LIMITGOON1 note: the carrier is the conversation's own, where it carries; a refused turn's prompt is known in its
  transcript by its place, before its own limit message, and is never handed again; the brief otherwise, which hands each refused
  word once (§2.4).
- **D206**'s rejection of copying the transcript, and its §3.5 briefed move to another account: carried where §2.1 holds, its box
  keeping its bounds and ids beside the carry record, which owns the delivery (§2.3). **Its own-account wake** sends the refused
  words again unseen, unchanged, but for a part whose carry is still pending: there the wake's go-on (`ChatGoOn.cs:530-531`) takes
  the carried way, its delivery derived afresh (§2.4), so the refused prompt, found in its place, is not sent again and the wake's
  own word goes after the words that waited.
- **D163**'s rejection of *carrying the transcript or a model-written summary*, narrowed by D189 to *never the transcript whole*
  (`D163.md:21`, `:273-274`): it covers every hand-over, and the curve's take and the person's hand-over to another account of the
  same agent are D163 hand-overs with the agent unchanged (`CurveMove.cs:107-118`). Within one agent between two of Daoris's
  accounts they carry the agent's own conversation whole; to another agent the rejection stands.
- **D210 point 6** (*Fatal: fails with its reason, a strike, as today. APIREFUSAL1's provider refusal is one*, `D210.md:75`): a
  carried part's first turn that its provider refused before any work started is the carry's failure, answered by a further part
  (§2.5), and is no strike; a `refusal` after work started, and every turn of the further part, are read by point 6 as any turn's.
- **D189 point 8** (*each move keeps the part's context at its end and the brief's size*, `D189.md:39-41`): a carried move keeps the
  context, `tokens` on its line (§2.7), and has no brief's size, since no brief was handed; the brief's size stands for every
  briefed move, the carry's fallbacks among them.
- **The code's own statements of the rule**, each reworded by SWITCHCTX1a for the table's names: the homes' contract, *Daoris owns
  this location and nothing inside it* (`HarnessSettings.cs:878`); `LimitMove`'s remark that the conversation *cannot resume on the
  next account* (`LimitMove.cs:12-13`); `HarnessConversations`' *never read from the agent's own home … which D125 rejected*
  (`HarnessConversations.cs:8-10`), which stays true of the id and is amended for the carry's copy; and `Continuations`' *another
  account would not find it* (`Continuation.cs:177-181`), which stays the go-on's rule, a carry being judged by its own judge. The
  glossary's row *part* (§2.7) is amended by SWITCHCTX1c-ii.
- **The dispatcher's best effort** (COWORK1c, `.claude/knowledge/session-words.md`; §1.8), for a carry's delivery alone: a word of
  it whose keep or attempt mark the service does not answer is not sent from the process alone, but waits in the carry record
  for the next try, as a transient episode's try already reads its mark (`SaidDoor.cs:69-92`). Every other word keeps today's
  rule.

**Kept**: D163 point 1, and its rejection of carrying the transcript for a hand-over to another agent; D205's (e); D49 §4, D66 §3 and D197 point 3
(no account's credentials, and never the person's own tool home or sign-in); D137 point 7 (a resume asks for its record's own
account: a carried part's own account is the new one); D137's rejection of adopting a terminal conversation in place (§2.2); D189
point 6 and its rejection of going back to a left conversation; D210's four kinds, by which a carried part's first turn is judged
(§2.5), point 6 amended as above.

## 5. Rejected

- **Spending two short turns to measure first** (the account-switch design's row): the fallbacks make the real move the measurement
  at no cost, and the owner's direction is not to spend turns on it.
- **Prompting the carried part at a curve move** with a line of Daoris's saying the account changed: it spends a turn on the new
  account that nobody asked for, whose first request writes the whole context H uncached; the line is the page's, and the agent
  takes it with the person's next words.
- **Counting `resumed` from the wire's acceptance**: it proves only that an id was found; the replay is read back instead.
- **Counting the person's turns in the replay by their words**: the agent stores the composed prompt and the record the words
  (§1.7), so the two have no stated match; the agent's own messages, thoughts and tool calls, kept with the ids they replay with,
  are the anchor, and the person's are counted only after it.
- **A subsequence for the read-back** (round 3): turns another client ran would pass; the lists are equal (Codex 2.3).
- **Recognising a limit's message in the replay by its sentence** (round 3): Daoris reads a limit from the door's failure alone,
  never the agent's messages (`AccountLimits.cs:63-67`), and the record holds the message by its id (Codex 1.2).
- **Keying the carry table by the adapter's version alone** (round 3): the program that writes the file is the CLI the adapter's
  SDK runs (Codex 1.3).
- **Judging the first turn by whether the agent's message came first**: a provider's refusal is said as the agent's message before
  the turn ends `refusal` (§2.5); the turn's end, D210's kinds and whether work started decide.
- **Falling back after a first turn that started a tool** (round 3 took every `refusal` and prompt-level `Fatal`): the work ran in
  the carried conversation, and a brief with the same words in a new one could run it again (Codex 2.2).
- **Falling back on a `Fatal` first turn, or reading "no tool" from the record** (round 4): `Fatal` includes a failure Daoris
  cannot read, so it is no evidence of a refusal before execution, and the record's events may not have been written (§1.8); only
  `refusal` with the door's own in-memory mark of no work falls back (Codex 2.2, Opus 6). It shows no work started, not that nothing
  ran: the prompt's hooks run with every prompt.
- **Setting the first-turn mark on a `tool_call` alone** (round 5): a subagent's launch is no `tool_call` where streams are taken,
  and its tools run on its own id, so a first turn that launched one and was then refused would fall back and run the subagent's
  work again (the Opus reader's read of round 5, finding 1); any update but the agent's words and the session's meters and settings
  sets it.
- **Dropping every fallback once the prompt is sent** (one reading of Codex's smaller shape): a line whose carried history its
  provider refuses would be refused at every later turn the same way, and would not go on; the fallback stays where the provider
  refused before any work started, which is the positive evidence Codex 2.2 asks for.
- **Falling back on a limit, a transient, an access refusal or the person's stop**: a brief on a limited account is refused there
  and would leave the limit's next move an empty conversation to carry; the others are not the carry's failure, and a brief mends
  none of them.
- **Handing a limit's refused words again where the transcript holds them, or counting the duplicate and handing it anyway**
  (round 3): the agent's context would hold the request twice (Codex 2.1).
- **Counting the user messages after the last answer to decide which refused words to hand** (round 4's U and R): a count does not
  say which prompts stand, one missing and one stranger count the same (Codex 2.1), and two limited carries in a row could hand
  words twice (Opus 3). Each refused turn's prompt is known by its place, directly before its own limit message, and is never
  handed again; any other shape falls back before any prompt, and the fallback hands each word once.
- **Persisting each composed prompt's text, or a digest of it, to match the replay's user messages**: a store of the
  conversation's words beside Daoris's record, matched against text the adapter reshapes (local command metadata stripped,
  several blocks), where the chain's own place already ties a prompt to its refused turn.
- **Judging the model by what each start asks**: a curve move asks the account's default, unknown before the spawn; what ran is
  judged after the load.
- **Judging what ran on `Ran.Entries`, or on `LastAcknowledgedOptions` alone** (rounds 3 and 4): the first is written only at a
  composer change and the second is null where nothing was set, so in the owner's case, a chat whose model nobody changed, the
  carry would never have run (Opus 1). The record's `asked` and `override` decide, and an account-default model is judged by the
  two accounts' own settings.
- **Recording every open's options as an `ActorEntry`** (Opus 1's smallest change): it would hold option values, and `default`
  resolves to another model on another account; the record's `asked` already says what was named and what the account decided,
  and the accounts' settings say whether the two defaults agree.
- **A re-ask on a carried start**: it would set another model on the carried conversation; the fallback opens with today's choice
  instead.
- **Replacing a left copy where it is a byte prefix of the source** (round 3, from the Opus reader's read of round 2): the prefix
  shows how the two files stood when read, not that no other client writes between the read and the move, and Daoris holds no
  exclusion against a client it neither offers nor prevents (Codex 2.4, 3.1); a carry writes only names that do not stand.
- **Replacing a left copy because a record names its id**: the person may have run it since in their own terminal.
- **Moving the files** rather than copying them: it takes the left conversation out of its account's home (D189 point 6) and loses
  it where the load fails.
- **Copying the whole home, or the account's configuration**: it carries credentials and another account's own.
- **Carrying out of, or into, the person's own tool home**: D49 §4, D66 §3, D197 point 3.
- **Sharing one home between two accounts**, or a link from one to the other: the accounts are separate homes by design (D66 §3,
  D125) and a link would carry everything.
- **Handing the brief as well as the carried conversation**: the agent has its own turns, and the brief repeats them at up to
  40,000 characters. A carried part's first request costs H, a briefed part's S and B (§3.1).
- **Keeping the brief's text** until the carry is done: a second store of the conversation's words; its recipe is kept and the brief
  recomposed from the ended old part.
- **Replacing the record's conversation after its first turn** (round 3's same-process fallback under the carried record): the
  turn's messages and usage would stand under a record that no longer names the conversation that made them (Codex 3.2); a further
  part keeps each record one conversation's.
- **Opening a second record for a fallback at the open**: the record names no conversation yet, so the fallback is its first.
- **A second `session/new` on the loaded process** (rounds 3 and 4): a protocol session opens once, and a loaded one still hears
  its updates (Opus 5); the fallback respawns.
- **Writing `harness.json` before the read-back** (round 3): a shell that ended between would leave a record naming a conversation
  nobody read back, which a later go-on would resume unread (Codex 2.5); the carry record is the authority until `verified`.
- **Recovering an automatic delivery only at the person's next word** (round 3, for every path but the ended move's box): a limit's
  words or a driven instruction would wait for a word that may never come (Codex 2.5); the come-up delivers from the carry record.
- **Saying the move's message before the open's outcome** (rounds 1 to 4, as today's take does): the start returns before its open,
  so the brief would be the carried conversation's first prompt (Opus 2); the take awaits `CarryOutcome` first.
- **Removing the carry record before the fallback's message is written** (round 4): a shell ending between would leave the fallback
  an empty conversation and no recipe, and the say door keeps no brief (Codex 2.5, Opus 4); `falling-back` journals it until the
  message is written.
- **Sending a carry's delivery from the process alone when the service does not keep or mark it** (the dispatcher's general rule):
  a shell ending after would lose it or send it twice, which the carry's recovery could not tell apart (Codex 1.1, 2.4); it waits
  in the carry record.
- **A key of the carry's own on the ledger's open**: a new field through the record's whole chain, where the record's `from` and
  why already find an open whose answer was lost (Codex 2.3).
- **Two owners of an ended move's delivery** (round 4 left D206's box composing beside the carry record): the box composed the
  refused words whatever the carry's disposition (Codex 3.4, Opus 3); the carry record owns the delivery, and the box keeps its
  bounds and ids.
- **Folding D206's box into the carry record**: the box is LIMITRESUME1's built and proven come-up for its bounds and ids, which
  the carry reads; the carry record names it and owns the delivery.
- **Setting the left copy aside at a move back** (Opus 8's later option): it writes in an account's home beyond the table's
  names; decided only once SWITCHCTX1f's count of `exists` shows it is worth it.
- **Ending a carry record at its own further part** (round 5's moved-on rule): the further part is a record `from` the new part,
  opened by the same carry, and ending the record there loses the recipe its message's brief is recomposed from (the Opus
  reader's read of round 5, finding 2).
- **Releasing the words kept during the load first** (round 5): a word typed while a long transcript loads would go before the
  delivery, and after a fallback it would be a fresh conversation's first prompt with no brief (its finding 3).
- **`BriefedMove`'s body as a driven line's further part** (round 5): it refuses an open quest's record and turns a closed one into
  a conversation, so the quest would stay taken with nothing running (its finding 4); the look's own carry-on opens it.
- **Respawning inside `AcpSession` or in the returned start** (round 5): `AcpSession` spawns nothing, and the chat's start has
  returned before its open (its finding 5); the watch's again-path and the driver's run respawn.
- **Reading a stop, a closing shell or a hang during the load as a refusal** (round 5): the fallback would prompt the brief against
  the person's stop (its finding 5); a stop completes the outcome as `stopped`, and the open is bounded.
- **Writing a Claude transcript for Codex, or a Codex thread for Claude**: it parses and writes a store across makers (D189, kept).
- **Waiting for the person when the carry fails**: the brief is complete enough for a go-on (D189 point 3), so the line goes on.
- **Drawing every earlier part as ended, or every part of the entry above the attended one** (round 3): an entry may hold parts
  alive alongside, and a part read as not live is drawn cut (Codex 4.1); the line view draws ancestry, each part as it stands.
- **A separator of the page's own between parts**: the boundary lines already say it, in the session's words.

## 6. Rows, in build order, with their proofs

Stand-ins are scripts the tests write under their own fixture home, as `ChatGoesOnProcessTests.Agent` does
(`src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/Conversations/ChatGoesOnProcessTests.cs:748`): never a real agent, never a window,
never a real console. **The stand-in ACP agent replays in the shape of the checked-in fixture read from the pinned package**
(§2.2), never in the shape of the rule it tests. It keeps each conversation as `projects/<Flatten(tree)>/<id>.jsonl` under its
`CLAUDE_CONFIG_DIR`, advertises `loadSession`, and refuses a missing one `resource_not_found`. On `session/load` it replays every
message of the file in order: each user message as the composed prompt it was sent, several blocks where it was sent several; each
of its own answers and thoughts as chunks carrying the same `messageId` it streamed live; each tool call by the `toolCallId` it had
live, and its result as tool updates; a limit's message as the agent's text by the id it was forwarded with; a compaction's summary
as user text; and a subagent's launch as an announcement on its own id and its messages to a child session
`<id>:replay-subagent:<tool use id>`. It reports the transcript's model as current after a load, and answers a prompt with the turns
it holds. A prompt it answers with a limit is stored as the prompt and then the limit's message, which it forwards live by the id it
replays with. Its switches make it replay short, replay an extra message or tool call, append a user message the record lacks, drop
the ids, run another model after the load, end a first prompt `refusal` after an `API Error` message with or without a tool call
or a subagent's launch (`subagent_spawned`, its child's updates on the child's id) before it, fail it `Fatal`, `Limit`, `Transient`
or `Access`, end it `cancelled`, store a limited prompt with no limit message after it, store a stranger's prompt in a refused
prompt's place, or answer a load slowly enough for a word to be said, a stop pressed, or the load's bound to pass meanwhile. The fixture home's hooks, each a seam a test hands the code: make a
destination name appear between the carry's look and its move; make the record's event write fail, the say door's keep go
unanswered, or the attempt mark fail; and end the shell **at each phase of the carry record and at each boundary between them**:
after the ledger's open and before `opened`, mid-copy, after `harness.json` and before `verified`, after `delivering` and before
the keep, after the keep and before the ids are written, after a word left `said`, after `falling-back` and before the fallback's
open, after that open and before its message is written, and between the further part's open and its message. There is no switch to fork an id: the protocol door names none
(§2.3).

| Row | What | After | Proof |
|---|---|---|---|
| SWITCHCTX1c-i | **A row of its own** (`TASKS.md`), under D167 and D189 point 8, **accepted 2026-10-25** (Standing): it reverses no decision. The line view and today's words (§2.7): a repository's chat and a driven line draw above the attended part the parts it continues by `from`, oldest first, each with its own liveness and read-only by a mode of its own, a part running alongside left to the strip; at most the three nearest drawn, a press for up to three more; an ended line opening at the attended record's head, a live one at its tail; only the attended part owns the region's scroll and the frame's facts, and Ask Daoris's earlier parts take the same mode; pressing an earlier part draws its own ancestry and hides the later parts; pressing a part, in the strip or on `PartsFlow`'s rail, attends it and scrolls to it; one boundary per side, the opening line folded under a move's boundary, the *began* words where a part was not a move; `chat.taken-over` says the new agent was handed Daoris's record, not its own turns; `moved.on` and `moved.from` reworded with the values their lines were written with; `limit.resume-refused` (its `{{why}}` kept), `work.limit.briefed` and `limit-went-on` reworded, in en and zh | — | Page tests over fixture events: **a line handed to another agent shows one conversation across its parts** (its earlier part's turns above, `chat.handed-over` and the reworded `chat.taken-over` between, no *new session*); **a curve move's line the same with one line per side**, `chat.moved` above and `chat.moved-from` below, the new part's opening line folded under it; **a part running alongside is not drawn above the attended part**; **an earlier part still live is drawn live**, its last turn not cut and its calls not stopped; **one scroll owner**: a long line follows the attended part's tail, no earlier part opens the region at its own head, and the frame is told one usage and one tail; Ask Daoris's earlier parts the same; **an ended line opens with the attended record's head at the region's top** and the nearest drawn earlier part beneath it, a live line at the attended part's tail; **pressing an earlier part hides the parts after it**, and pressing the newest draws them again; **a line of five earlier parts draws three**, the press says *Show 2 earlier parts* and draws the other two, en and zh; a driven line's `moved.on`/`moved.from` pair **written before the rewording** drawing whole in the new words, its `tokens` and `count` shown; a sibling's part bounded by its *began* words; a press in the strip and on `PartsFlow` attends the part and scrolls to it; `i18n:check`, `names:check -- --strict`, `note.test.ts` |
| SWITCHCTX1a | The carry on the protocol door, Claude Code's ACP adapter alone, wired on a conversation's curve move alone (`CurveMove` → `HandOverTake` → `ChatRunner.StartAsync`): `Carries.Judge` and its codes; the table keyed by the compatibility identity, the CLI's version read from the pin, its anchor and per-agent never-list, with the checked-in replay fixture; the identity kept beside every kept id; the id's grammar and the tree's proven flattening; the model judged on the record's `asked` and `override`, the two accounts' settings and their offer readings; the carry record and its phases, each name written before its move; the create-only copy; the start's order, its steps 6 and 7 in the watch, and `ChatStart.CarryOutcome` awaited by the take, bounded, before it says or writes anything; `chat.Opened` releasing the delivery or the fallback's message before any word kept during the load; a stop during the load completing as `stopped`; the load with every update dropped; the count and the place of what follows the last answer; the checked `harness.json`; nothing prompted; the respawned fallback journaled at `falling-back`, respawned by the watch's again-path; the come-up at every boundary, the carry's own further part never ending its record, and the carried way on the go-on with its delivery derived afresh; the first turn judged in D210's kinds and by the door's in-memory `WorkStarted`, the further part by `BriefedMove`'s body past its gates, its brief bound before the first unanswered turn, with `FirstTurnWords`; `handed` through the record's field chain, `KeepHowHandedAsync`, its `PersonDoors` row, the pushed slice and its `HandedTold` hook; `session.moved briefed=false` and `conversation.carried`; **today's curve lines** (`chat.moved`, `chat.moved-from`) until SWITCHCTX1c-ii words the carried ones; the code's four statements of the rule reworded | — | **A stand-in's conversation moved by the curve to another account's home is loaded with its earlier turns**, the old part's model never changed, so its `asked` says `account-default` and the two accounts' settings agree (its messages, thoughts and tool calls counted equal by id against the line's records; the new part's `harness.json` holds the same id and identity; no brief handed; nothing prompted until the next word, which the stand-in answers naming the earlier turns); **two curve carries in a row** the same, the second's anchor the two parts' records. **No brief is ever queued to a carried part**: the take says nothing to the new part until `CarryOutcome` completes, and a carried part's first prompt is the person's next word alone. The model: an old part whose model the person changed asks it again and holds where it runs; **accounts whose settings name different models or efforts, or whose offer readings list different models, refuse `model` before any copy**; **a named `default` is judged as account-default, never set**; **a record with no `asked` refuses `model`**; another model running after the load falls back with today's choice, never asked again; no re-ask handed. The replay: **a subagent replayed to a child id is neither kept in the record nor counted**; an update during the load moves no option; a short replay, an extra message or tool call, and a replay with no ids each fall back before any prompt; **a user message after the last answer falls back `suffix` before any prompt**; **an event the record failed to write makes the lists differ and falls back**, never a wrong count. The first turn: **a provider refusal said as the agent's message and ending `refusal`, with no work started by the door's own mark, opens one further part** `from` the carried one, why `retry`, by `BriefedMove`'s body run past its gates, its brief bound before the first unanswered turn and the first turn's own words after it, each once, the carried record keeping its events and usage, no strike; **a first turn that launched a subagent and was then refused is the turn's own**, the subagent's work never run again; **the same with the record's tool event lost but the door's mark set is the turn's own**; **`refusal` after work started, and any `Fatal`, are the turn's own**, no brief, no further part; a limit's refusal stays the limit's (its move is 1b's), no brief on the limited account; **the person's stop and a transient retried** leave the carry pending. The copy: **any name standing at the destination is left as it is and nothing is written** (`exists`, the brief); **a name made between the look and the move is not overwritten** (`exists`); temporary names removed; a tree past 200 flattened characters refused and said (`folder`). The come-up: **a shell ended at each boundary the stand-in's hooks name comes up as §2.4's table says**: an open whose answer was lost found by its `from` and why and adopted, never opened twice; a curve move at `opened` or `copied` left to its next word; **a shell ended inside the fallback finishes the fallback, never carries again, and hands the brief once**, recomposed from the record, the carry record kept until the fallback's message is written; **a shell ended between the further part's open and its message**: the carry record is not ended by its own further part, and the come-up hands that message once, its brief recomposed; never a prompt before a read-back, never an attempted word sent again. **A word typed during the load**: with the carry held it is the first turn; fallen back, it goes after the fallback's brief and words, never before. **A stop during the load** completes the outcome as `stopped`, opens nothing, briefs nothing, and leaves the carry record for the next word; **a load past its bound** falls back `load`. And: a record on the person's own sign-in, either side, moves by the brief (`own-home`), nothing read in the fixture's own tool home; an old process not yet exited, the brief (`busy`); a load refused, the brief on a process the watch's again-path respawned, the record not concluded meanwhile and naming only the fallback's conversation; an id outside the grammar, an identity with no row (a pinned `claude` among them, and an adapter run from `PATH`), a row with no anchor, a line with no message id, each refused and briefed; a write of the id that fails, the brief (`unkept`); the fixture home's `.credentials.json`, `.claude.json`, `history.jsonl`, `todos/<id>`, `file-history/<id>/`, another conversation's file and `sessions/` not in the destination; the carry record taken by a delete; `handed` read through the pushed sessions slice after the `HandedTold` hook, its door refused without the key, and absent from the sync wire (tests hold each) |
| SWITCHCTX1b | Every other move of §2.1 by 1a's protocol: `LimitMove` live (its account settled in the start, its refused words no longer queued before `Carried`) and ended (its box keeping its bounds and ids, the carry record owning the delivery), D206's briefed move to an account that frees first, the person's hand-over to another account of the agent (no closing note asked), a quest's carry-on why `curve` or `limit` (`Driver.ContinuesFrom`, its `SayMoved` after the run's open), and `HelpStartAsync` where its room is the previous part's tree (`Handed` written after `Carried`). The delivery's dispositions (§2.4 step 5): refused turns found in place and never handed again, Daoris's word for a carried limit move, the keyed and checked keep and mark, the carry record as the one owner; consecutive limited carries; the come-up delivering a pending delivery from the carry record. On the driven path, **the fallback before any prompt respawns in the driver's own run** (`Loop/Driver.cs:4315-4379`), where a refused `ResumeAsync` throws out of `AcpSession.RunAsync` today (`Acp.cs:817-818`); **the briefed instruction is composed before the spawn**, so that fallback can hand it; the driven come-up and go-on take the carried way (`DriverContinuations.cs:61`, `:209`, `:1220`); **the driven further part is the look's own carry-on of the failed run**, why `retry`, handed the brief and the instruction, named by the carry record once it opens. Ask Daoris's lost open found by its opening line. The carried way's delivery derived afresh at each go-on, D206's own-account wake of a pending carry among them | 1a | The same stand-in per path. **A live limit move whose refused prompts stand in place**: nothing refused is handed again, the waiting words then Daoris's word, each once; **a refused turn with no limit message after its prompt, two prompts before one, or a stranger's prompt in a refused prompt's place, falls back before any prompt** (`suffix`), and the fallback hands each refused and waiting word once. **Two limited carries in a row before any answer, whatever each carried** (the first carry's prompt held the waiting words, the second's held more): the second hands only the words that waited since, and no word reaches the agent twice. **Batched refused words**, several said before one refused prompt, found as that prompt. An ended limit-held record: its box's come-up composes nothing where a carry record names it, the carry record delivering without the brief and without a refused word; **the box and the carry record never both deliver**. A person's press with no closing turn asked. A driven carry-on loaded, **its first prompt the instruction without the brief, the channel's held messages in it and marked handed once it works**; a driven carry-on whose load is refused, briefed by the driver's run on a respawned process, with the instruction composed before the spawn; **a driven first turn refused before any work: one further part, opened by the look's carry-on**, why `retry`, the quest never left taken with nothing running. **A limit carry whose first turn is refused**: the further part's brief bound before the earliest refused turn, and its words the refused words then the words that waited, each once. **A part carried by the curve whose first turn a limit refused, woken on its own account by D206**: its refused prompt found in place, not sent again, the waiting words then the wake's word, in its own conversation. **A word said during a limit carry's load** goes after the waiting words and Daoris's word. **Ask Daoris's lost open** found by its opening line, never opened twice. **The old record's waiting words** leave it by their ids once the new record kept them, their files resolved against the old record. **A shell ended at `verified` or `delivering` before the delivery: the come-up delivers the limit's words, or the driven instruction, from the carry record without waiting for a word**; a keep the service did not answer retried under the same key, never sent from the process alone; a mark that failed sends nothing; after the delivery was attempted, settled uncertain and never sent again; after a word left `said`, never kept again. Ask Daoris carried in the same room and briefed in another (`room`), its `Handed` written after the outcome; another agent on each path still briefed |
| SWITCHCTX1c-ii | The carried lines (§2.7), **driver and web**: the codes are the driver's (`NoteCodes`, `HandOverNotes`) and their words the web's, landed together since `NoteCodesTests` holds both (§1.8); `moved.carried-on` and `moved.carried-over-curve` with `chat.moved`'s figures and `tokens`, `moved.carried-from`, `moved.carry-failed` with the family `carry` in both languages, `first-turn` its one spelling, `folder` among it, `work.parts.began.carried`; the driver writing the carried pair in place of today's curve lines; the driver's English of `moved.on` and `moved.from`, which still says *new session* on the console and in the record's text (`Conversations/Domain/HandOverRecords.cs:159-176`), reworded to the part's words; a part `own` drawn with no brief, Ask Daoris folding `own`, the glossary's row *part* amended | 1c-i and 1a | Page tests **through the pushed `handed` value**, as the slice pushes it: a carried part not yet read back (absent), one read back (`own`) and one fallen back (`brief`); a carried line with `moved.carried-from` and no brief, one line per side; a carried curve move with the figures on the old part's line; a fallback at the open with `moved.carry-failed` at the new part's boundary, its reason worded; a first-turn fallback with the carried part's `moved.carried-from`, its refused turn, and the further part's `moved.carry-failed`; every code of the family `carry` worded in en and zh (`note.test.ts`); `HelpConversations` folding `own`; `i18n:check`, `names:check -- --strict` |
| SWITCHCTX1d | Codex's row of the table, read from a Codex home that holds a session, with its never-list and its replay's anchor read from the pinned codex-acp | a Codex account's session on the install; 1a and 1b's protocol | the row's names read on the pinned codex-acp, each name holding the id classed; the anchor read from its package; a stand-in filing by date loaded after a carry; **a version whose replay holds no readable anchor goes by the brief before any carried prompt**; until then Codex briefed (`version`; a test holds it) |
| SWITCHCTX1e | The pipe door carries (§3.3): its row keyed by the `claude` CLI's version, and a read-back of its own | 1a; LIMITRESUME1f; the pinned CLI read to offer a read-back without a turn | the stand-in's reaction to 1f's recorded shape and to the read-back as read: an end before `init` → the brief, said; a different id named on `init` → the brief and `forked`; a read-back that differs → the brief before any prompt; a first turn failing → judged as §2.5 |
| SWITCHCTX1f | The measurement (§3.2): the install's first real carried move read from the machine log and written as D165's note | 1a, 1c-i and 1c-ii built and installed (1b for a limit's case) | the parent's install proof: **which case ran**, the outcome, the agent's identity, the replay's matched items, the refused turns found in place, **the model and effort that ran after the load, the first turn's stop reason and whether work started**, the provider's class of a refusal read by hand, the first turn's cache write, the part, **the count of moves that found `exists`** and of first turns that started a subagent, and the window showing the line as one conversation; every case that did not run named unmeasured |

1c-i is first because it fixes what the person saw for both halves (§1.4) and reverses no decision, which is why it stands as a row
of its own; 1a gives the agent its memory on the curve's move, the case the install met, and 1b brings every other move onto the
same protocol once 1a has proved it.

## 7. What this design's gate does not cover

No real agent has loaded a carried conversation; §3.1 lists what is not known, and §3.2 says how it becomes known. The pinned
package's replay was read, not run, and its line numbers are claude-agent-acp 0.84.0's. The page's change has not been seen in the
window. Nothing here parses or writes inside a conversation's file; the carry reads and writes the table's names alone, between
Daoris's account homes, never over a name that stands; and nothing here touches the person's own tool home.

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

## 10. Round 4: Codex's read answered

Every finding of Codex's read of round 3 (`docs/2026-10-24-switch-context-reads.md`, its section *Codex, round 3*), numbered as
Codex numbers them, its section and item. Each is checked against the code on main at `972b17577`, the commit Codex read, and the
package findings against the pinned adapter's own package, claude-agent-acp 0.84.0 on the SDK 0.3.284 with its CLI 2.1.284, read
from the install's toolchain (§1.5, §1.7). *Taken* means the design now says it, and where; *disputed* means it is not taken, with
the line that shows why. None is disputed: each holds against the code. The lines Codex cites are `972b17577`'s, as this round's
are.

| # | Finding (blocking in bold) | Checked against | Answer |
|---|---|---|---|
| **1.1** | **Child replay is not already filtered** | `Acp.cs:3431-3435`, `:2517-2533`; `AcpStreams.cs:95-99`, `:152-184`; package l. 5363, 5405, 5427, 5446-5459 | **Taken**: round 3's claim is withdrawn (§1.7). The carried load drops every update of the conversation's own id and of every id beginning `<id>:replay-subagent:`, so no child reaches the record, and counts the conversation's own alone (§2.3); 1a's stand-in replays a subagent to a child id and asserts it is neither kept nor counted (§6). |
| **1.2** | **`LimitHeld` cannot recognise the replay exception** | `LimitHeld.cs:17-56`; `AccountLimits.cs:63-67`; package l. 4659-4668, 4712, 5490-5504 | **Taken**: the exception is gone, and no sentence is read anywhere. A limit's message is forwarded live as the agent's text to a client that takes no typed failures, by the same id it replays with, so the record already holds it; the lists are compared equal, and a mismatch falls back (§1.7, §2.3, §5). |
| **1.3** | **The adapter version does not identify the transcript writer** | `HarnessSelection.cs:603-608`; `Adapters.cs:2131-2153`; package `claudeCliPath` l. 626-662, l. 6638; the SDK's `package.json`, `claudeCodeVersion` | **Taken**: the carry table is keyed by the compatibility identity, the adapter's and the CLI's versions together, kept beside every kept id (§1.5, §2.1, §2.2, §2.3); the pipe door's row is the CLI's own and unread (§2.2, §3.3). |
| 1.4 | Package evidence is not in the checkout | `Adapters.cs:1895`; `Accounts/AgentSettings.cs:27`, `:55`; the install's toolchain | **Taken**: SWITCHCTX1a checks in a fixture of the replay's frames read from the package, each identity and line named, and the stand-in follows it (§2.2, §6); the other pair a comment names has no row (§1.5). |
| **2.1** | **Duplicate prompts remain an admitted outcome** | `LimitHeld.cs:17-56`; limit-resume design `:360-366`; package l. 5490-5494 | **Taken**: the replay's user messages after the last answered turn decide before any prompt: none where no words wait; for a limit, U = R hands no refused word, U = 0 hands each once, anything else falls back `suffix` (§2.3, §2.4 step 5). D206's own-account wake sends refused words again because it cannot see the transcript; a carry can, so it does not (§1.6). |
| **2.2** | **The first-turn fallback can repeat completed tool work** | `AgentErrors.cs:5-19`, `:17`; `Acp.cs:3100-3112` | **Taken**: the fallback needs positive evidence that nothing ran, `refusal` or a prompt-level `Fatal` with no `Tool` event in the turn; a turn that started any tool is the turn's own failure, handled as today, and the carry counts `held` (§2.5, §5). |
| **2.3** | **Assistant ids do not establish complete context** | `Acp.cs:3091-3112`; package `tool-calls/renderer.js` l. 64, 84 | **Taken** both ways: the invariant is stronger (messages, thoughts and tool calls by their ids, across the line's parts naming the conversation, equal lists with no extra; nothing may stand under `<id>/` at the destination), and *read back* is narrowed to what it proves (§2.2, §2.3). |
| **2.4** | **The copy is not protected against another writer** | session-messages design `:291-295`; `D137.md:56` | **Taken**: no replacement at all. Every `copy` name must be absent, and each lands by a move the disk refuses where the name exists (`exists`); the cost, a move back to an account holding a left copy goes by the brief, is said (§2.2, §5). |
| **2.5** | **Recovery lacks a committed delivery state** | `HarnessConversations.cs:31-43`; `LimitMove.cs:250`, `:269`, `:352`, `:366-379`; `ChatGoOn.cs:189`; `UncertainWords.cs:18` | **Taken**: one durable carry record per move, written before the open, with its phase, the delivery and its disposition; `harness.json` names the carried id only once read back; the come-up at every boundary in a table, a pending delivery delivered from the record at the come-up and an attempted one settled uncertain by the dispatcher's rule (§2.3, §2.4). |
| 2.6 | The account boundary is sound; name `Toolchain.Owner` | `Harnesses.cs:281`; `HarnessSettings.cs:881-882`; `D197.md:21` | **Taken**: homes reached by `ProfileHome(home, Toolchain.Owner(adapter), account)` (header, §2.1, §2.2). |
| **3.1** | **D125 and D189's copying prohibition is not yet answered** | 1.1, 2.3 and 2.4 above; the reads record `:417-418` | **Taken** through 1.1, 2.3 and 2.4: the copy writes only absent names, and the read-back is equal, child-filtered and before any prompt. On the byte prefix Codex's reading is the code's: it shows how two files stood when read, not that nobody writes between, and the rule is withdrawn (§2.2, §5, and below). |
| **3.2** | **D131, D137 and D167's record reversal is only partly justified** | `Acp.cs:144-151`; `D137.md:757-769` | **Taken**: no record names a second conversation. A fallback before any prompt is the record's first conversation, since `harness.json` names none until read back; a fallback after a first turn refused before anything ran opens a further part, why `retry`, so the carried part keeps the events and usage of the conversation that made them (§2.5, §4, §5). D137 point 8's amendment shrinks to the two records naming one id. |
| 3.3 | The compaction reversal has its reason; keep the cost unmeasured | — | **Taken**: §3.1 says a replay that agrees does not show what the agent retains; the cost stays unmeasured until SWITCHCTX1f (§3.1, §4). |
| **3.4** | **D197 and D206's limit amendments inherit the delivery semantics** | `D197.md:381-389`; `D206.md:320-327` | **Taken**: §2.4's limit disposition and recovery table, consecutive limited carries among them; new notes on D197 and D206 say *each once* holds in the agent's context, by the replay's count. |
| **4.1** | **Earlier does not mean ended** | `conversation.ts:881-901`; `D167.md:73-82`; `AskConversation.tsx:870-876` | **Taken**: the line view draws the attended part's ancestry by `from` alone, alongside parts left to the strip; each earlier part with its own liveness, read-only by a mode of its own (§1.4, §2.7, §5). |
| **4.2** | **`handed` misses D182's push chain** | `SessionsStateSlice.cs:39-47`, `:62-67`; `Sessions.cs:1407-1423` | **Taken**: `handed` in the slice's `Fields`, the driver's write followed by a declared change of the sessions (the door writes no revision), and tests through the pushed projection (§2.3, 1a, 1c-ii). |
| 4.3 | The endpoint needs explicit driver authority | `PersonDoors.cs:251-256`; `PersonDoorTableTests.cs:81`, `:86` | **Taken**: the route's `DoorClass.Driver` row, its test with its class's count, and a keyless call refused (§2.3). |
| 4.4 | The translation requirements are incomplete | `noteLines.ts:316`, `:795-806`; `note.json:213` | **Taken**: the family `carry` in both languages, read by the note reader as a family, `{{why}}` in `moved.carry-failed` and in the whole reworded `limit.resume-refused`, and `note.test.ts` holding each (§2.7). |
| **5.1** | **SWITCHCTX1c-i** | `SessionConversation.tsx:161-200`, `:191`; `conversation.ts:881-901` | **Taken**: 4.1's fixes and one scroll owner, Ask Daoris's earlier parts with them (§2.7, §6). Codex's disagreement with the Opus reader's endorsement holds (below). |
| **5.2** | **SWITCHCTX1a's stand-in** | — | **Taken**: §6's stand-in and 1a's proofs name each case: a child replay, an unmatched user message, an extra agent item, a failure after a tool, a destination name made between the look and the move, and a shell ended at each phase. |
| **5.3** | **SWITCHCTX1b** | — | **Taken**: consecutive limited carries, and the come-up delivering a pending limit's words or driven instruction from the carry record, not at a later word (§2.4, §6). |
| 5.4 | SWITCHCTX1c-ii's prerequisites | — | **Taken**: after 1c-i and 1a, tested through pushed `handed` values in each state (§6). |
| 5.5 | SWITCHCTX1d's dependence | — | **Taken**: after 1a and 1b's protocol; no readable anchor goes by the brief before any carried prompt (§6). |
| **5.6** | **SWITCHCTX1e** | `Adapters.cs:2222-2226` | **Taken**: the pipe door carries nothing until it has a read-back of its own equal to §2.3's and a row keyed by the CLI's version, after 1a and LIMITRESUME1f (§3.3, §6). |
| 5.7 | SWITCHCTX1f records which case ran | — | **Taken**: the note names the case that ran and every case that did not as unmeasured (§3.2, §6). |
| 6.1 | A smaller shape | — | **Adopted**, as below. |

**Where the readers disagree, decided on the code.** Three places, each decided for Codex:

- **SWITCHCTX1c-i.** The Opus read of round 2 endorsed it as the right first row without condition (the reads record, its round 2
  §3); Codex blocks it until its lifecycle, its siblings and its scroll are fixed (4.1, 5.1). The code sides with Codex: `settle`
  cuts a part read as not live (`conversation.ts:881-901`), D167's amendment lets an entry hold parts alive alongside
  (`D167.md:73-82`), and each `SessionConversation` owns the region's scroll and tells the frame its facts
  (`SessionConversation.tsx:161-200`). It stays the first row, with those fixes.
- **The byte prefix.** The Opus read of round 2 held that a destination which is a byte prefix of its source is safe to replace (the
  reads record `:417-418`); Codex holds that it shows an observed relationship, not exclusive ownership (2.4, 3.1). Nothing in the
  code holds an exclusion against the person's own terminal, which the session-messages design leaves free (`:291-295`), so the
  replacement is withdrawn and the carry writes only absent names.
- **The anchor's limit exception.** Round 3 wrote the Opus read's anchor with an exception for the limit's sentence; Codex shows no
  reader in Daoris reads that sentence (1.2), and the package shows the record already holds the message by its id. The exception
  is withdrawn and the lists are equal.

**The smaller shape** (Codex 6.1) is adopted, because it keeps the owner's outcome: a switch of account continues the session in its
own conversation, and a switch of agent continues it from the brief, read as one conversation. **The corrected line view first**
(SWITCHCTX1c-i, §2.7). **Then the carry on the curve's path alone** (SWITCHCTX1a), Claude Code's ACP adapter alone, through **one
durable carry record** holding the phase, the source and destination, the compatibility identity, the anchor and the pending
delivery with its disposition (§2.3). **It falls back before any prompt wherever its proof fails** (§2.5), and **after a turn
starts running, ordinary handling holds** (§2.5's table). **The same protocol then reaches the limits and the other moves**
(SWITCHCTX1b), and another door once it can read back (SWITCHCTX1d, SWITCHCTX1e). **Another agent continues through today's brief**
(§2.6). Two parts are kept apart from it, each with its reason in §5: the first-turn fallback stays where the record shows nothing
ran, since a line whose carried history its provider refuses would otherwise not go on; and D206's built box keeps its delivery
on the ended move, the carry record naming it.

**What stays owed.** Both readers' reads of round 4, Codex's and the independent Opus reader's (D191, its note of 2026-10-24). The
design stays proposed until both land and their blocking findings are taken. SWITCHCTX1c-i, which reverses nothing, needs both
readers' check of its round-4 text, not the carry's acceptance.

## 11. Round 5: the reads of round 4 answered

Both makers' reads of round 4 at `4698728a5` (`docs/2026-10-24-switch-context-reads.md`, its sections *Opus (foundation), round 4*
and *Codex, round 4*), numbered as each numbers them: the Opus reader's findings as **O1** to **O11**, Codex's by its section and
item. Each is checked against the code on main at `e1f4ecb5d`, whose cited sources are `972b17577`'s but `ServiceClient.cs`'s, and
the package findings against the pinned adapter's own package (§1.5, §1.7). *Taken* means the design now says it, and where;
*agreed* means the reader found it holding and nothing changes; *disputed* means not taken, with the line that shows why. None is
disputed. SWITCHCTX1c-i was accepted on both reads before this round, O11 taken with it (Standing).

| # | Finding (blocking in bold) | Checked against | Answer |
|---|---|---|---|
| **O1** | **The model condition cannot be met in the owner's case** | `ChatRunner.cs:1427`, `:1468`, `:1547`, `:2090-2106`; `Loop/Driver.cs:3663-3666`; `Acp.cs:597`, `:604-605`, `:723`; `ActorRun.cs:22-27` | **Taken, by another change than the one named**: what ran is judged on the record's `asked` with its `override` laid over, which every start keeps; a named value is asked again and read on the session's current options, `LastAcknowledgedOptions` only where a set returned one; an account-default value holds where the two accounts' own settings agree (§1.7, §2.1). The named change, an `ActorEntry` from every open's options, is rejected in §5: it records option values, and `default` resolves to another model on another account (package `session-model.js` l. 141-162). 1a proves a line whose model nobody changed, and two carries in a row (§6). |
| **O2** | **The carry's steps do not run inside the start** | `ChatRunner.cs:1609-1630`, `:2645-2647`; `HandOver.cs:256-276`; `LimitMove.cs:141-172` | **Taken** as named: `ChatStart.Carried`, completed by the watch at `verified` or at the fallback's open; the take, `LimitMove` and `HelpStartAsync` await it before saying or writing anything; `chat.Opened` after it; the driven `SayMoved` after the run's open; 1a proves no brief is ever queued to a carried part (§1.8, §2.3, §2.4, §6). |
| **O3** | **Two limited carries in a row can hand words twice** | `LimitHeld.cs:17-56`; `ChatRunner.cs:2149-2165`, `:2198-2204`; `LimitMove.cs:296-313`, `:366-379`; package l. 1769-1771, 5490-5494 | **Taken, by a stronger change than the one named**: no count at all. Each refused turn's prompt is found by its place, the user message directly before its own limit message, so a carry never hands a refused word again; any other shape falls back, and the fallback hands each word once; where a carry record names D206's box, the box composes nothing (§2.3, §2.4). 1b proves the orders, batched words and a stranger's prompt (§6). |
| **O4** | **A fallback's own boundaries are not recoverable** | `ChatRunner.cs:2623`; `HandOver.cs:271`; `LimitMove.cs:167`; `SaidDoor.cs:31-37` | **Taken** as named: `falling-back` journaled with its code and target before the fallback opens anything, finished at the come-up and never carried again; the carry record kept until the fallback's message is written; a curve move at `opened` or `copied` left to its next word; 1a proves a shell ended inside the fallback (§2.4, §2.5, §6). |
| O5 | The same-process fallback is a new door path, and the child filter rests on the package | `Acp.cs:1614`, `:1658`, `:2485-2533`, `:3431-3435` | **Taken**: the fallback respawns; while a load is answered every update is dropped, whatever its id, and only the own id's are counted (§2.3, §2.5). |
| O6 | The first-turn rule claims more than the record shows, and its further part has no opener | `HandOver.cs:160-166`; `LimitHeld.cs:31-37`; `BriefedMove.cs:98-101`, `:127-148` | **Taken**: worded as *no tool call started*, read from the door's own in-memory mark; `refusal` alone falls back, the wire's kind word logged; the opener is `BriefedMove`'s body, why `retry`, and its words the first turn's own by a reader of their own, `FirstTurnWords` (§2.3, §2.5). |
| O7 | The table's identity and names are underspecified | `ToolHomeLeftovers.cs:30-36`, `:49-50`; `HarnessSelection.cs:603-608`; the SDK's `package.json` | **Taken**: the CLI's version read from the pin or from `claudeCodeVersion` under the adapter's managed pin; a tree Claude Code is not proven to flatten as `Flatten` does refused `tree`; each name written to the carry record before its move (§1.5, §2.1, §2.2). |
| O8 | Create-only costs most where the owner works | — | **Taken**: said in the owner's terms; SWITCHCTX1f counts `exists`; setting the left copy aside left to a later decision, no row now (§2.2, §3.2, §5). |
| O9 | A reversal is unnamed | `D189.md:25-26`, `:45` | **Taken**: D189 point 3's last sentence and its *Why* named in §4 and in D189's note (the sentence stands at `:25-26`, the read's `:22-24` the point's start). |
| O10 | The rows, the lanes and the names are incomplete | `NoteCodesTests.cs:8-11`; `StatePublisher.cs:263`; `DriverLoop.cs:35-36`; `DownloadKeeping.cs:33`; `LimitMove.cs:422-424`; `DriverContinuations.cs:61`, `:209`, `:1220` | **Taken**: 1a writes today's curve lines until SWITCHCTX1c-ii, a driver-and-web row; the seam `HandedTold`, wired as `WiredKeptTold` is; `KeepHowHandedAsync`; a come-up row ending a carry record whose part moved on; 1b names the driven come-up and go-on, one further part only, and `Handed` written after the outcome (§2.3, §2.4, §6). |
| O11 | Three things in SWITCHCTX1c-i are left unsaid | `followTail.ts:49-61` | **Taken** in the line view's acceptance: where an ended line opens, that pressing an earlier part hides the later ones, and at most three earlier parts drawn with a press for more (§2.7, §6). |
| **1.1** | **The dispatcher does not guarantee the delivery durability claimed** | `ChatTurns.cs:155-200`; `SaidDoor.cs:69-92` | **Taken**: a carry's delivery keeps and marks each word checked, and waits in the carry record where either goes unanswered, never sent from the process alone; §4 names it as a change of the dispatcher's best effort for this delivery alone (§2.4). |
| 1.2 | The package claims remain unverified in the checkout | `Adapters.cs:1895` | **Taken** as round 4 had it: the fixture with exact identities is checked in before 1a enables its row (§2.2). |
| **2.1** | **`U = R` does not establish which prompts survived** | session-messages design `:291-295`; package l. 1769-1771, 4455-4460, 5490-5494 | **Taken**: the count is gone. A refused turn's prompt is the user message directly before its own limit message, matched by the id the record holds, since the limit message is the CLI's answer to it; batched words are that one prompt; a stranger in its place, or any other shape, falls back (§1.7, §2.3, §2.4). Persisting each composed prompt to match against was weighed and rejected (§5): the adapter drops a prompt's echo live, so no id of it is known, and the chain's place already ties it. |
| **2.2** | **"No recorded tool" is not positive evidence that nothing ran** | `SessionEventLog.cs:84-93`; `Acp.cs:2502-2512`, `:2554-2565`; `AgentErrors.cs:17` | **Taken**: the door's in-memory `ToolStarted`, independent of whether the record wrote the event; `Fatal` no longer falls back; only an explicit `refusal` before any tool call does, and anything less keeps ordinary handling (§2.5, §5). |
| **2.3** | **Opening the new record is not recoverably idempotent** | `Sessions.cs:799`; `SessionLedger.cs:652`, `:1519` | **Taken**: an open whose answer, or whose `opened`, was lost is found by its `from` and why after the carry record's moment and adopted, never opened twice; more than one found ends the carry record, said (§2.3 step 4, §2.4). A key on the open is rejected in §5. |
| **2.4** | **Delivery needs a durable terminal disposition** | `Acp.cs:2070-2085`; `ChatTurns.cs:1120-1124` | **Taken**: `delivering` with each word's key before any keep; the ids written before `chat.Opened` releases them; the carry record and the service together say waiting, attempted, written or never kept; the carry record the one owner, D206's box composing nothing beside it (§2.3, §2.4). |
| **2.5** | **The fallback discards its recovery recipe too early** | `ChatHandOver.cs:238-248`; `SaidDoor.cs:31-37` | **Taken**: `falling-back` journals the target and code; the fallback's message is kept under the carry's own key, its brief recomposed from the recipe whenever the dispatcher hands it; the record removed only once that message is written; an attempted one uncertain, its next word briefed once more; the further part the same (§2.5). |
| 2.6 | The replacement problem is answered | — | Agreed. |
| 3.1 | D125 and D189 have a sufficient reason for the bounded copy | — | Agreed. |
| 3.2 | D131, D137 and D167 have a sufficient record-shape reason; the newest-part restriction is essential | — | Agreed: the anchor is the line's parts that name the id, and no reader looks a record up by its conversation alone (the Opus read's §1). |
| 3.3 | D165 and D189's compaction reversal is bounded | — | Agreed. |
| **3.4** | **D197 and D206's promises exceed the delivery proof** | `D197.md:392`; `D206.md:331` | **Taken**: their new notes say a refused prompt is known by its place and never handed again in a carry, the fallback handing each once, with one delivery owner (§4). |
| 4.1 | The person-door, push and translation omissions are addressed | — | Agreed. |
| 4.2 | SWITCHCTX1c-i has a buildable contract | — | Agreed; accepted (Standing). |
| 4.3 | D191's standing is honest | — | Agreed. |
| 5.1 | SWITCHCTX1c-i can remain first | — | Agreed; accepted and first. |
| **5.2** | **SWITCHCTX1a leaves recovery assertions to the builder** | — | **Taken**: the stand-in's hooks end the shell at every boundary named, and fail the event write, the keep and the mark; 1a's proofs assert each (§6). |
| **5.3** | **SWITCHCTX1b's word mapping is unsettled** | — | **Taken**: a stranger's prompt in a refused prompt's place, batched words, consecutive carries in any order, and the box and the carry record never both delivering, each a 1b proof (§6). |
| 5.4 | SWITCHCTX1c-ii has the right dependencies | — | Agreed; and it is a driver-and-web row (O10). |
| 5.5 | SWITCHCTX1d has an explicit evidence dependency | — | Agreed. |
| 5.6 | SWITCHCTX1e is correctly held | — | Agreed. |
| 5.7 | SWITCHCTX1f measures a narrow claim; "nothing lost" needs the delivery guarantees | — | **Taken**: *nothing lost* now rests on §2.4's delivery contract; 1f also counts `exists` (§3.2). |

**Where the readers differ, decided on the code.**

- **The verdicts.** The Opus reader accepts after named changes; Codex asks another round. Their blocking findings fall in one
  layer, delivery and recovery (O2 to O4; Codex 1.1, 2.1 to 2.5, 3.4, 5.2, 5.3), and each is taken with a change a reader can check
  against the lines named above.
- **O3's named change against Codex 2.1.** The Opus reader would count refused turns and user messages from the earlier carry
  record's disposition; Codex shows that a count cannot tell one missing prompt and one stranger from the right ones. Codex's reading
  is the code's: the adapter drops a prompt's echo live (package l. 4455-4460), so the record has no id to count by. The design
  takes neither count: the chain's own place ties each refused prompt to its limit message, which answers both findings.
- **O1's named change.** The Opus reader would record each open's options as an `ActorEntry`. The package shows why that is not
  enough across accounts: after a load the model's current value is the new account's choice that matches the transcript's model,
  `default` where the new account's default resolves to it (`session-model.js` l. 141-162). The design judges the record's `asked`
  and the two accounts' settings instead.

**What stays owed.** Both readers' reads of round 5 (D191, its note of 2026-10-24). The carry stays proposed until both land; where
their blocking findings are taken with changes they can check, the steward accepts it. SWITCHCTX1c-i is accepted and is built
meanwhile.

## 12. Round 5's named changes, and where each went

The independent Opus reader read round 5 at `90bbffe4b` (`docs/2026-10-24-switch-context-reads.md`, its section *Opus (foundation),
round 5*): 11 findings, 4 blocking, accept after named changes the steward can check without another read. Each is taken as the
read names it, where it offers two forms the one named below, and cited at `e1f4ecb5d`, whose sources the read read. Its §0 checks
both reads of round 4 against round 5 and finds no finding of either wrong; its §1 claims are each finding's below.

| # | Finding (blocking in bold) | The change, as named | Where it went |
|---|---|---|---|
| **1** | **The first-turn fallback misses a subagent, so its work can run twice** | The mark is set by any update of the turn other than the agent's message and thought chunks and the session's meters and settings, so a subagent's launch counts; 1a proves a first turn that launched one and was refused is the turn's own | §2.5 (`WorkStarted`, set after the replay filter and before the streams route, `Acp.cs:2489-2519`), §2.3's log, §3.2, §5, §6 (stand-in, 1a, 1f) |
| **2** | **The carry's own further part ends its carry record before its message is written** | The moved-on rule reads *a record `from` the new part exists, other than the further part its `falling-back` names*, in both places; 1a's hooks end the shell between that part's open and its message, and the come-up hands the brief once | §2.3 (the carry record), §2.4 (the recovery table's two rows), §5, §6 (stand-in, 1a) |
| **3** | **Words kept on the new part during the carried open go before the delivery and the fallback's brief** | `chat.Opened` releases the delivery or the fallback's message first, then the words kept since, in their order, the turns holding every other word until that first item is queued; 1a proves a word typed during the load after the fallback's brief, 1b one said during a limit carry after the waiting words and Daoris's word | §2.3 (*What goes first*), §2.4 step 5, §2.5, §5, §6 (1a, 1b) |
| **4** | **The driven further part has no opener** | On the driven path the further part is the look's own carry-on of the failed run, why `retry`, handed the brief and the instruction; the carry record at `falling-back` names it once it opens; `BriefedMove`'s body is the chat's opener alone; 1b proves one further part, opened by the look | §2.5 (*The further part*), §5, §6 (1b) |
| 5 | The fallback at the open has no place to respawn, and reads a stop, a closing shell or a hang as a refusal | A chat respawns by the watch's again-path, the record not concluded; a driven one in the driver's run; a stop or the shell closing completes the outcome as `stopped`, opening nothing, the carry record kept; the open and the take's wait bounded, five minutes as a first value, past which `load` falls back; 1a proves a stop during the load | §2.3 (*A stop, a closing shell or a hang*), §2.5, §5, §6 (1a, 1b) |
| 6 | The further part's message doubles the first turn's words, and on a limit carry leaves the refused words out | Its brief is composed `before:` the first turn no answer followed, the earliest refused turn across the parts naming the id, else the first turn; its words are the person's words of every such turn, in order; the carried part is ended first and the chat's body runs past the live and `said` gates; 1a and 1b prove each | §2.5 (*The further part*), §6 (1a, 1b) |
| 7 | The model judgement does not hold what it claims | An account-default value also needs both accounts' offer readings (`KeepOffers`) to list the same models, an account with none refusing; a named `default` is judged as account-default; a record with no `asked` refuses `model`; 1a proves each | §1.7 (`asked` best-effort), §2.1, §6 (1a) |
| 8 | The carried way keeps the move's disposition, which a later limit changes | The carried way derives its delivery afresh at each go-on, from the line's records: refused turns found in place, then the waiting words, then the go-on's own word; §4's D206 line names the change; 1b proves it | §2.4 (*The carried way*), §4 (D206), D206's note, §6 (1b) |
| 9 | Ask Daoris's lost open cannot be found | Found by its opening line, which names the session it carries (`ChatRunner.cs:790-792`); none found is a move not yet made, nothing prompted; 1b proves it | §2.3 step 4, §6 (1b) |
| 10 | Three reversals are unnamed | D163's rejection of carrying the transcript and D210 point 6 named in §4, each with a dated note; §2.5's first row worded as APIREFUSAL1's `Fatal` reading, a carried first turn's refusal no strike; `moved.carried-on` and `moved.carried-over-curve` given `{{tokens}}`, and D189 point 8 named as amended for the brief's size alone | §2.5, §2.7 (the two codes), §4 (D163, D210 point 6, D189 point 8), the notes on D163, D210 and D189 |
| 11 | Smaller claims and names | The log keeps the stop reason and no class, 1f reading the provider's class by hand; `FirstTurnWords` pairs each word's `receipt` with its `User` event by place; the delivery's files resolve against the old record and its waiting words leave it once the new record kept them; one key per word, Daoris's `DaorisKeys.LimitCarried`, classed with the resume keys; `ChatStart.CarryOutcome`, named apart from the three `Carried`; `first-turn` one spelling, and an unproven folder name said as `folder`; *no turn and no tool call* at the open, the session-start hooks named; the fixture also holds `claudeCliPath` and the model a load restores; the fallback message's brief recomposed at a go-on's take-up too; the driver's English of `moved.on` and `moved.from` (`HandOverRecords.cs:159-176`) left to SWITCHCTX1c-ii, since SWITCHCTX1c-i's build is the web's | §2.1 (`folder`), §2.2 (the fixture), §2.3 (the log, `CarryOutcome`), §2.4 step 5, §2.5, §2.7 (the family), §3.2, §6 (1c-ii, 1f) |

**What moved on main since `e1f4ecb5d`.** Main now stands at `d365f769c`, merged into this branch. Of the files round 5 cites,
`ChatRunner.cs`, `ChatTurns.cs`, `BriefedMove.cs`, `ChatGoOn.cs`, `Loop/Driver.cs` and `Service/ServiceClient.cs` changed, so
their line numbers here are `e1f4ecb5d`'s, and SWITCHCTX1a's builder reads each by the name it cites. One change is a fact the
design states: MODELROLE1d2 gives a chat's first open a re-ask too (`ffd458ca3`), so §1.5's *only the driven start hands a re-ask*
is `e1f4ecb5d`'s; the carried start still hands none (§2.1).

**What stays owed.** Codex's read of round 5 (D191, its note of 2026-10-24), its credits out. The carry stays proposed until it
lands and its blocking findings are taken, and nothing of the carry is built until then. SWITCHCTX1c-i stays accepted and is being
built.
