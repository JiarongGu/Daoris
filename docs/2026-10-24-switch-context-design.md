# A move keeps the agent's own context, and a session goes on as one conversation whatever runs it (SWITCHCTX1, 2026-10-24)

**Standing: proposed, round 2, under D191.** Round 1 was written by Claude Opus 5.5 from the code on main at `ddf0a24fd` and read
at `ff2461081` by the independent Opus foundation reader, its read verbatim in `docs/2026-10-24-switch-context-reads.md`: 12
findings, 4 blocking, another round. Codex's read is owed, its credits out. Round 2 is written by Claude Opus 5.5 from the code on
main at `426ed2b73`, whose `src/` is unchanged since `ddf0a24fd`; it answers every finding in §8, each checked against that code.
Nothing here is built. The design reverses decided rules other designs rest on (§4 names each), among them D189 point 1 on an agent's
own transcript, so by D191's note of 2026-10-24 it is not accepted on one maker's read: it waits for Codex's read as well as the
Opus reader's next. Its notes on D125, D131, D137, D165, D167, D189, D197 and D206 say *proposed* and point here.

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
  (`Acp.cs:3214-3216`, `:3236`, `:3290-3294`). A resume carries its record's chosen model and effort again (`:3260-3261`), and a
  choice the account does not offer ends the open `ChoiceNotOffered` after the wire accepted the resume (`:665-699`, `:814-821`).
  In a one-shot run a resume always sends the run's prompt as its next turn (`:774-776`, `:811-847`); a chat's session resumes or
  opens and then waits for words (`ChatRunner.cs:2618-2619`). The pipe door is Claude Code's alone, `--resume <id>`
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
| the door resumes: the protocol door, and the pipe door only once LIMITRESUME1f has recorded that its `--resume` of a missing id fails closed (§3.3) | `door` |
| the agent's carry table (§2.2) has a row for the adapter's pinned version, which is both the old record's `HarnessVersion` and the start's `selection.Version` | `version` |
| until SWITCHCTX1f has measured the cross-account case, the new part asks for the model and effort the old part ran (`PriorSession.Ran.Models`, as `LeftBy` reads it, `LimitMove.cs:454-455`) | `model` |

The moves that meet it: D165's curve move of a conversation (`CurveMove` → `HandOverTake`) and of a quest's session (the carry-on,
why `curve`); LIMITGOON1's move to another account of the agent (`LimitMove`, live or ended); D206's briefed move of a limit-held
conversation to another account that frees first; the person's own hand-over to another account of the same agent; a quest's
carry-on why `limit`; and Ask Daoris's limit move (`HelpStartAsync`) where its room is the previous part's tree. Everything else
(another agent, the person's own sign-in on either side, a teammate's record, an intake, a tree or room that differs, an id never
kept) goes by the brief, as today.

The judge runs twice: **before the old part ends**, on what is known then (the agent, the old record's account, its tree, its kept
id, the door, the version, the model), so a hand-over that will carry asks no closing note (§2.4 step 2); and **after the start's
selection settles the account**, on all of it, the old process's exit among it. Only the second decides.

### 2.2 What is copied, and what never is

The carry copies **the conversation's own files and nothing else**, named by its id, whole and byte for byte. Daoris never parses
them, never edits them and never writes into them. What it does is a narrow read and write inside two account homes, by name only:
it reads the previous home's files of a conversation the line owns and writes them into the next home. That amends the homes'
contract, *Daoris owns this location and nothing inside it* (`HarnessSettings.cs:878`), for these names alone; its one precedent is
the trust grant to `.claude.json` on the person's act (`ClaudeTrust.cs:69-73`). Both homes are reached only through
`HarnessSettings.ProfileHome(home, agent, account)` with the accounts §2.1 settled, never by a path read from a record, so the
person's own tool home is never a source or a destination.

**The carry table**, per agent and per pinned adapter version, beside the adapters. Each row classes **every place in the home
where a name holds the conversation's id** on that version, read by listing names only, as `copy` or `never`; a version with no row
carries nothing (`version`), and the row is read again when the pin moves.

| Agent, version | `copy` | `never` (each name holding the id) | Read on |
|---|---|---|---|
| Claude Code (both doors), the pinned claude-code-acp | `projects/<Flatten(tree)>/<id>.jsonl`; `projects/<Flatten(tree)>/<id>/` where it stands | `todos/<id>*` (the agent's own plan, which Daoris's record holds), `file-history/<id>/` (rewind checkpoints, which no resume needs), `session-env/<id>/` (a process's environment), `debug/<id>*` | written by SWITCHCTX1a from a names-only listing of an account home on the pin it names; §1.5's listing of 2026-10-24 names the first two and `session-env/` |
| Codex (codex-acp) | the one file under `sessions/` whose **name** holds `<id>`, at the same relative path | not read | not read: no Codex home here holds a session (§1.5); the row is SWITCHCTX1d's, and until then Codex moves by the brief |

**Never copied, per agent**, whatever the table says. For every agent: the account's credentials, its own configuration, its
history of prompts across conversations, another conversation's files, its plugins, skills, caches, telemetry, policy and usage
files. For Claude Code: `.credentials.json`, `.claude.json`, `settings.json`, `history.jsonl`, `sessions/`, `session-env/` and
`shell-snapshots/` (running processes' files). For Codex: `auth.json`, `config.toml`, and every file under `sessions/` but the one
its row names, since `sessions/` is where Codex keeps every conversation. A name the row does not class `copy` is not copied. An id
outside the grammar of §2.1 is refused before any path is built (a `:` names an alternate data stream on Windows), and a resolved
path that leaves the home, or passes through a link, is refused; either falls back (§2.5).

**How it lands.** Only once the old part's process has exited (§2.1, `busy`). Each file is copied to a name of its own beside its
destination and then renamed into place, so a reader never sees half a file. A destination file of the same name is replaced only
where Daoris's own record names that id as this line's (a line moved back to an account it ran on before, whose copy there is the
one it left); a same-named file no record of this line names is left as it is and the carry falls back (`exists`). The source is
**copied, never moved or removed**: the left conversation stays in its account's home, and the line's next part owns the copy. A copy
left by a carry that fell back stays where it landed, inside that account's home, and goes with the home when the account is
removed (D66 §3); nothing else removes it. `ToolHomeLeftovers` lists only the person's own tool home (`ToolHomeLeftovers.cs:25-27`),
which a carry never touches.

**Reasons, against §1.6's.** *It acts inside an account's home*: it does, narrowly, by name, on the line's own conversation only,
after its writer has exited, never on an account's credentials, identity or another conversation, and never on the person's own
tool home. *It depends on a format the harness changes at will*: the table is held to a version, so a new version carries nothing
until its row is read; and since a change that keeps the names but moves meaning elsewhere would resume without the turns, a resume
is counted only once its replay agrees with Daoris's record (§2.3), so a resume missing turns falls back rather than counting.
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
  is the line's conversation as it last ran.
- **Where it runs.** The carry is an ask handed to the start, `CarryAsk` (the old record, its kept id, the move's why, and the
  brief's recipe, §2.5), taken by the three starts that open a moved part: `ChatRunner.StartAsync` (the curve's take, the person's
  hand-over, LIMITGOON1's live move, D206's ended move), the driven open (the carry-on, `Driver.cs:2422-2439`), and
  `LimitMove.HelpStartAsync` (`LimitMove.cs:402-416`). In each, in this order:
  1. the selection settles the account (`LimitMove` passes `profile: null`, so its account is known only here);
  2. `Carries.Judge` decides, on that account (§2.1);
  3. the ledger opens the new record, which creates its id (`ChatRunner.cs:582-587`; `Driver.cs:2431-2439`; `PlaceHelpAsync` for
     Ask Daoris);
  4. the copy runs (§2.2) and the new record's `harness.json` is written as `HarnessConversation(adapter, carried id)`, with the
     carry's own file beside it (§2.5);
  5. the spawn, and `RunAsync` resumes the carried id as a `GoingOn` does: a chat's session by `ResumeAsync` on the new account's
     home (`ChatRunner.cs:2618`), the driven run and Ask Daoris's by the run's `resume` (`Acp.cs:774-776`).
  The carried start takes the agent's `session/load` where it is advertised, not `session/resume` (below).
- **Read back before it counts.** `session/resume` replays nothing, so a resume the wire accepted proves only that the agent found an
  id (`Acp.cs:3214-3216`). A carried start resumes by `session/load` and counts the replay the door already receives and drops
  (`:3236`, `:3290-3294`): the user turns it replays, in order, against the turns Daoris's record shows the agent took on the old
  part. It is **resumed** only where every turn the old part answered is in the replay, in order, and its last answered turn is the
  replay's last but for the refused turns' words of a limit (which may or may not follow it, and the log says which). Otherwise the
  carry falls back in the same start, `short-replay`. This reads the agent's wire, which the door receives on every load today, and
  never its store. D131 rejected load-first because the record already holds the history (`D131.md:47`); a carry is the one place
  where the replay is the check. A compaction inside the part (the harness's own, D189 point 7) that shortens the replay falls back
  too, which is the safe side; 1f records how often.
- **Where the agent names another id** on the resume, `HarnessConversations.Keep` replaces it as today, and the carry falls back:
  `forked`.
- **How it was handed** is kept for every kind of part, as a machine-only field of the session record, `handed`: `own` (its own
  conversation carried), `brief` (Daoris's brief handed), or absent (a part handed neither: a first part, a resume). It goes
  through every place of the chain `.claude/knowledge/session-record-fields.md` names (the service's record and store, the ledger's
  write, the host's request and response, the driver's write and read, the three stand-ins, the web's type), with no field on the
  sync wire, which a test holds; a teammate's mirror carries neither it nor any conversation id. The driver writes `brief` for
  every briefed move, today's among them, and `own` once a carried part's first turn answers (§2.5). Its readers: the *Parts*
  strip's *began* words, the line view (a part `own` draws no brief, §2.7), and Ask Daoris's fold, whose room records take no
  `from` (`LimitMove.cs:7-8`), so `HelpStartAsync` also writes `.help.json`'s `Handed` as `own` beside `transcript` and `brief`
  (`HelpConversations.cs:118`, `:124`).
- **The machine log** keeps one field for how a move handed its part: `session.moved`'s `briefed` (`Continuation.cs:585-593`),
  false where the carry's open resumed and true where it fell back at the open. One line of its own, `conversation.carried`, says
  what held, once, when it is known: the agent and its version, the two accounts by profile name, the files' count and bytes, the
  replay's count against the record's, whether the refused turns' words were already in it, and the outcome: `held` (the first
  turn answered), or the fallback's code (`refused <why>` at the judge or the wire, `not-found`, `exists`, `short-replay`,
  `forked`, `first-turn <why>`). Never a word, a path or an account's key.

### 2.4 The move, step by step

1. The turn ends; the move is decided as today (`SwitchDecision`, the limit's walk, the person's press).
2. The first carry judgement (§2.1). A person's hand-over that will carry asks the outgoing agent **no closing note**
   (`ChatHandOver.cs:145-149`), since its own turns go with it; where the carry later falls back, the brief goes without the note,
   as a forced move's does today.
3. The old part ends, as today (`EndForHandOverAsync`), and the move reads whether its watch ended or the fifteen seconds did.
4. The new part's start (§2.3): the selection, the second judgement, the open, the copy and `harness.json`, the spawn, the load and
   its count.
5. Where it resumed, the new part is handed only what the move would have handed after the brief, and nothing where nothing waits:
   - **a curve move or a person's hand-over** (no words wait, §1.1): **nothing is prompted**. The resumed chat waits, as a resumed
     chat's session already does (`ChatRunner.cs:2618-2619`), and the person's next words are its first turn, with one line of
     Daoris's before them saying the account changed. No turn is spent on the new account that nobody asked for;
   - **a limit's move**: the refused turns' words, then the words that waited, each once, as `LimitMove.cs:156-166` orders them;
   - **a driven carry-on**: its instruction composed as today without the brief, the ask's channel's held messages among it
     (`Driver.cs:2540`, `:2620`), marked handed once the record works (`:2712`), as every driven start's are.
6. The lines are written: the old part's last, the new part's first (§2.7), and `session.moved`.

**Where a shell ends partway.** The ended-record move has D206's durable box (`docs/2026-10-23-limit-resume-design.md:395-406`):
it carries the reason and the refused turns' bounds the brief is recomposed from (`LimitMove.cs:250`, `:293-310`) and gains the
carry's outcome, so a shell that ends between step 4 and step 5 comes up and goes on from the box: the resume where the carry
landed, the brief where it did not. On the other paths there is no box: once `harness.json` names the carried id, the new part is
an ended record with a kept id, and the next word said to it resumes it through the go-on (`ChatGoOn.cs:480-528`), with the carry's
file still beside it, so its first turn is still watched (§2.5). A shell that ends before `harness.json` is written leaves a record
with no id, which goes on by the brief as a lost conversation does today (`ChatGoOn.cs:487-490`).

### 2.5 When it does not hold: the brief, said

A carry that does not hold falls back to today's move, never to a wait and never to the person. **The brief is kept on the new part
until its first turn answers**: not its text but its recipe, in the carry's own file beside `harness.json`
(`<home>/sessions/<record>.carry.json`, under the same home entry): the old record, the move's why and the bound of the old part's
events the brief is composed up to. The old part has ended, so the brief recomposed from it is the one the move would have handed.

**At the open**, in the same start:

- the second judgement refuses (§2.1), or the table names no file, or the file is not there (`not-found`);
- a copy fails, a name is refused, or a same-named file no record of this line names stands (`exists`);
- the protocol door refuses the load: `resource_not_found` → `gone` (`Acp.cs:3245`), or any other refusal on the wire;
- the replay disagrees (`short-replay`), or the agent names another id (`forked`);
- the open ends `ChoiceNotOffered` after the wire accepted the load (`Acp.cs:814-821`).

**At the carried part's first turn**: the turn fails before any update of the agent's answer (the API refuses the carried history
or another account's or model's signed thinking, the context does not fit, a model is not offered). The words were the person's
next, or the limit's refused and waiting words, or the driven instruction; none was answered.

Either way the fallback runs **on the same process**: it opens `session/new`, replaces the record's kept id by the new
conversation's before any answer under it (`HarnessConversations.Keep`), and hands the brief recomposed from the carry's file, then
the same words step 5 handed, as `LimitMove` and `HandOverTake` hand them today. **This is a new path, not today's**: today a refused
resume kills the process (`ChatRunner.cs:2622-2640`) under the rule that a new session would be a new conversation under the same
record (`:2616-2617`). It holds that rule's purpose: no conversation ever answered under this record but the one it now names, so
the record still stands for one conversation; §4 names the amendment of D137 point 8 for it. The record is marked `handed: brief`,
the new part's first line says the carry failed and why (`moved.carry-failed`, §2.7), the carry's file is removed, and
`conversation.carried` says the code. On the first answer instead, the file is removed, the record marked `handed: own`, and the
log says `held`. A fallback is never tried again by spending a turn: the next move tries the carry afresh.

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
  (`PartsStrip.tsx:91-95`). A driven session's line shows the same. A teammate's record shows only what it shows today (D47 §4).
- **Between two parts, one line on each side, once.** Where a part began by a move or a hand-over, the boundary is the old part's
  last line and the new part's first, and nothing else: where a move carries, its two lines are the carried ones below alone,
  never `chat.moved`'s pair beside them, and they take `chat.moved`'s figures (the decision's window, used and curve) where the why
  is the curve. Where a part did not begin by a move (a sibling's request of the same ask, D167 point 2, a *Try again*, a resume
  into a new part), it has no move lines, and the page draws its *began* words from the strip as its boundary, one line. No
  separator of the page's own, no *new session*, no greeting drawn as a start. A carried part draws no brief, since none was
  handed; a briefed part's brief stays folded as today.
- **Ask Daoris's history** folds a part handed `own` into its line as it folds `brief` (`HelpConversations.cs:233-249`).
- **The words.** Moving and resuming are said in the session's words: a part, never a new session.

| Code | en | zh |
|---|---|---|
| `note.moved.on` (old part, last line, every move; reworded) | *Went on as part {{part}} with {{agent}} as `{{account}}`, because {{why}}.* | *由 {{agent}}（账户 `{{account}}`）以第 {{part}} 段继续，因为{{why}}。* |
| `note.moved.carried-from` (new part, first line, its replay agreed) | *Continues part {{part}}, which ran as `{{account}}`, because {{why}}, in its own conversation: its earlier turns were read back.* | *接续第 {{part}} 段（原账户 `{{account}}`），因为{{why}}，在它自己的对话中继续：已读回它先前的轮次。* |
| `note.moved.carry-failed` (new part, at the open or at its first turn) | *Its own conversation could not go on as `{{account}}` ({{why}}), so it continues part {{part}} from Daoris's brief of it, {{count}} characters.* | *它自己的对话无法在账户 `{{account}}` 上继续（{{why}}），因此凭 Daoris 对它的简报（{{count}} 个字符）接续第 {{part}} 段。* |
| `note.moved.from` (new part, first line, a briefed move; reworded) | *Continues part {{part}} …*, the rest as today | *接续第 {{part}} 段……* |
| `note.chat.taken-over` (reworded) | *This conversation went on from {{agent}} conversation {{session}}, handed over because {{why}}; it was handed Daoris's brief of it, not its turns.* | *这段对话是从 {{agent}} 的对话 {{session}} 转交过来继续的，原因：{{why}}；交给它的是 Daoris 对其的简报，而非它的轮次。* |
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

- Whether Claude Code, on the pinned claude-code-acp, loads a conversation whose two entries (`<id>.jsonl`, `<id>/`) were copied
  from another account's home: whether it reads anything else in the home for it, and whether a record inside the file names the
  first home (a persisted tool output's path does) in a way that matters (§2.2).
- Whether the load's replay holds every turn the old part answered, and how often a compaction inside the part shortens it.
- Whether the API takes the earlier turns' signed thinking under another account, and under another model; until 1f, a carry asks
  the same model and effort (§2.1). The account-rotation design saw a conversation outlive a re-sign-in in the maker's CLI
  (`docs/2026-10-01-account-rotation-design.md:338`), in the same home, which lowers the risk without measuring it.
- Whether a limit's refused turn's words are already in the carried transcript beside the refusal, so that handing them again puts
  them in the agent's context twice. The replay shows it (§2.3), and the log records it.
- What a carried part's first request costs: the prompt cache is per account, so it writes the whole context H, where a briefed
  move writes S and B (`docs/2026-10-20-session-context-design.md:275-279`); whether it reads anything from a cache is not known.
- Whether the resumed agent names the same id or forks another.
- Codex's file's name, and whether codex-acp's resume finds a file it did not write in that home.
- The pipe door's `--resume` of an id that is not there (LIMITRESUME1f's question, §3.3).

### 3.2 Measured on the install's own next real move, never by spending turns

No turn is spent to test it, and the account-switch design's two-turn evidence row is withdrawn. The build lands with every
fallback of §2.5, so the first real carried move on the install **is** the measurement: it either holds or falls back, at the open
or at its first turn, with nothing lost. Where the carry does not reach a real move, today's briefed move is what runs, and nothing
is claimed. The `conversation.carried` line holds the outcome; the parent reads it from the machine log after the install's next
carried move and writes it as a dated note on D165, naming the agent's version, the outcome, the replay's count against the
record's, whether the refused turns' words were already there, the first turn's cache write (the turn's `cacheWrite`, as the usage
history already keeps it, `Accounts/Usage/UsageHistory.cs:659`) and the part. Until that note, a carried part is said as read back, never claimed as
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
  one.
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
- **The homes' contract** in code, *Daoris owns this location and nothing inside it* (`HarnessSettings.cs:878`), is reworded by
  SWITCHCTX1a for the table's names; and the glossary's row *part* (§2.7) by SWITCHCTX1c-ii.

**Kept**: D163 point 1 and its rejection of carrying the transcript, for another agent; D205's (e); D49 §4, D66 §3 and D197 point 3
(no account's credentials, and never the person's own tool home or sign-in); D137 point 7 (a resume asks for its record's own
account: a carried part's own account is the new one).

## 5. Rejected

- **Spending two short turns to measure first** (the account-switch design's row): the fallbacks make the real move the measurement
  at no cost, and the owner's direction is not to spend turns on it.
- **Prompting the carried part at a curve move** with a line of Daoris's saying the account changed: it spends a turn on the new
  account that nobody asked for, whose first request writes the whole context H uncached; the line is the page's, and the agent
  takes it with the person's next words.
- **Counting `resumed` from the wire's acceptance**: it proves only that an id was found; the replay is read back instead.
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
never a real console. The stand-in ACP agent keeps each conversation as `projects/<Flatten(tree)>/<id>.jsonl` under its
`CLAUDE_CONFIG_DIR`, advertises `loadSession`, replays that file's user turns on `session/load`, refuses a missing one
`resource_not_found`, and answers a prompt with the turns it holds; switches make it replay short, fork an id, refuse the model, or
fail a first prompt before any answer.

| Row | What | After | Proof |
|---|---|---|---|
| SWITCHCTX1c-i | The line view and today's words (§2.7): a repository's chat and a driven line draw their earlier parts above the attended one; pressing a part attends it and scrolls to it; one boundary per side, the *began* words where a part was not a move; `chat.taken-over` says the new agent has the brief, not its turns; the rewording of `moved.on`, `moved.from`, `limit.resume-refused`, `work.limit.briefed` and `limit-went-on`, in en and zh. It reverses no decision | — | Page tests over fixture events: **a line handed to another agent shows one conversation across its parts** (its earlier part's turns above, `chat.handed-over` and the reworded `chat.taken-over` between, no *new session*); a curve move's line the same with one line per side; a sibling's part bounded by its *began* words; a press attends the part and scrolls to it; `i18n:check`, `names:check -- --strict`, `note.test.ts` |
| SWITCHCTX1a | The carry on the protocol door, wired on the curve's take (`CurveMove` → `HandOverTake` → `ChatRunner.StartAsync`): `Carries.Judge` and its codes; the table with its version and per-agent never-list, read on the pin; the id's grammar; the copy and its refusals; the start's order (selection, judgement, open, copy and `harness.json`, spawn, load and its count); nothing prompted where nothing waits; the carry's file and both fallbacks on the same process; `handed` through the record's field chain; `session.moved briefed=false` and `conversation.carried`; the homes' contract reworded | — | **A stand-in's conversation moved by the curve to another account's home resumes with its earlier turns** (the replay counted; the new part's `harness.json` holds the same id; no brief handed; nothing prompted until the next word, which the stand-in answers naming the earlier turns). And: a record on the person's own sign-in, either side, moves by the brief (`own-home`), nothing read in the fixture's own tool home; a same-named file no record names left alone, the brief taken (`exists`); a move back to an earlier account replacing that account's copy; an old process not yet exited, the brief taken (`busy`); a load refused, the brief on the same process, no process killed; a short replay, the brief taken; a first turn that fails after an accepted load, the brief on the same process with the same words, `moved.carry-failed`; `ChoiceNotOffered` after the load, the brief; an id outside the grammar, a version with no row, another model asked, each refused and briefed; the fixture home's `.credentials.json`, `.claude.json`, `history.jsonl`, `todos/<id>`, `file-history/<id>/`, another conversation's file and `sessions/` not in the destination; `handed` absent from the sync wire (a test holds it) |
| SWITCHCTX1b | Every other move of §2.1 takes it: `LimitMove` live (its account settled in the start) and ended (its box), D206's briefed move to an account that frees first, the person's hand-over to another account of the agent (no closing note asked), a quest's carry-on why `curve` or `limit` (`Driver.ContinuesFrom`), and `HelpStartAsync` where its room is the previous part's tree | 1a | The same stand-in per path: a live limit move with held words (each once, after the load, no brief); an ended limit-held record; a person's press with no closing turn asked; a driven carry-on resuming its own conversation, **its first prompt the instruction without the brief, the channel's held messages in it and marked handed once it works**; Ask Daoris carried in the same room and briefed in another (`room`); another agent on each path still briefed. A shell ended between the carry and the resume: **for the ended move, the come-up goes on from the box**; for the others, the next word resumes the carried id through the go-on, its first turn still watched by the carry's file |
| SWITCHCTX1c-ii | The carried lines (§2.7): `moved.carried-from`, `moved.carry-failed`, `work.parts.began.carried`, a part `own` drawn with no brief, Ask Daoris folding `own`, the glossary's row *part* amended | 1a | Page tests: a carried line with `moved.carried-from` and no brief; a failed carry with `moved.carry-failed` and its brief folded; `HelpConversations` folding `own`; `i18n:check`, `names:check -- --strict` |
| SWITCHCTX1d | Codex's row of the table, read from a Codex home that holds a session, with its never-list | a Codex account's session on the install | the row's names read on the pinned codex-acp, each name holding the id classed; a stand-in filing by date loaded after a carry; until then Codex briefed (`version`; a test holds it) |
| SWITCHCTX1e | The pipe door carries (§3.3) | LIMITRESUME1f | the stand-in's reaction to 1f's recorded shape: an end before `init` → the brief, said; a different id named → the brief and `forked`; a first turn failing → the brief |
| SWITCHCTX1f | The measurement (§3.2): the install's first real carried move read from the machine log and written as D165's note | 1a, 1b, 1c-i and 1c-ii built and installed | the parent's install proof: the outcome, the agent's version, the replay's count, whether the refused turns' words were already there, the first turn's cache write, the part, and the window showing the line as one conversation |

1c-i is first because it fixes what the person saw for both halves (§1.4) and reverses no decision; 1a gives the agent its memory,
but only once a path is wired and installed.

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

**What stays owed.** Codex's read, and the Opus reader's read of this round (D191, its note of 2026-10-24). The design stays proposed
until both makers' blocking findings are taken; by that note, a design that reverses D189 point 1 is not accepted on one maker's read.
