# The steward: a role the owner appoints, making development's management acts on their behalf (STEWARDROLE1, 2026-10-24)

**Status: accepted (D209) 2026-10-24 by the owner's decision (D209's acceptance note), round 3 with its named changes, amended 2026-10-24 by Codex's owed read of the final text (stands after named amendments, 14 findings, 7 blocking; each placed in §12.4, D209's amendment note).** Round 1 was written by Claude Opus from the code at `d580caeb4` and read at `48f9b3cff` by Codex
(another round) and the independent Opus foundation reader (accept after named changes, 20 findings, 7 blocking), their reads verbatim in
`docs/2026-10-24-steward-role-reads.md`. Round 2 was written by Claude Opus 5.5 from the code on main at `f5c515f0b`, where SUPERVISOR1a-ii's
door is merged, and read at `92dd03ec5` by Codex (another round, 12 findings, 8 blocking) and the Opus foundation reader (accept after named
changes, 14 findings, 5 blocking), their reads verbatim in the same file. Round 3 is written by Claude Opus 5.5 from the code on main at
`aa16658e7`; §12.2 answers every finding of round 2's two reads, §12.1 keeps round 1's answers. Round 3 was read at `3d2d446f8` by the
Opus foundation reader alone (accept after named changes the steward checks without another read, 14 findings, 4 blocking), verbatim in
the same file; Codex's read stopped when its credits ran out and is owed on the final text. Its named changes are taken here, from the code
on main at `ffcf42926`, each placed in §12.3. Codex's read of the final text, at `16e0950e2`, is in the same file; its amendments are
written into the sections they touch, each checked against the code on main at `7cda79f01` and placed in §12.4. Under D191 (its amendment of 2026-10-23: the design changes a protocol between processes,
the pipe's census and its judge, and the decided rules D201 and D208 rest on).

The direction (the quest that asked for this design, 2026-10-24): the steward is a role the owner appoints, so development's management
needs no click. Every management act the steward makes for the owner from the terminal waits for the window's Confirm today: a
repository driven or given trees, a setup, a session's words (`sessions say`, a go-on after an outage), a hand-over, a finish; the owner
pressed many such cards on 2026-10-24. These sit under the role logic D208 begins: the owner appoints the steward once in the window, and
it then makes development's management acts on the owner's behalf by its own decision, each recorded as the steward's and never as the
owner's words, revocable at once; never a production or live go-ahead, a sign-in, an access widening, a push or publication, spending, or
a deletion of the owner's data.

This design serves two parts of D193's company. **The owner** (part 1): each wait on the owner should be one only they could answer, and
a drive switch, a go-on after an outage or a finish of a session already waiting is not. **Management** (part 18): the steward orders,
dispatches, merges and verifies, and settles what is not the owner's; here its management acts get a door of their own, recorded as its
own, never as the owner's.

Read for it: D208 and `docs/2026-10-24-supervisor-design.md` (the appointment, the holder key, the dedicated session, the bounds and the
record), with D208's note of SUPERVISOR1a-ii as built; D201 and `docs/2026-10-23-terminal-trust-design.md` (the standing confirmation, its
judge and its containment, not yet offered); D156 and `docs/2026-10-09-person-door-design.md`; D193 and `docs/2026-10-21-company-concept.md`;
D191; D188 (the answers-alone count); D51 (session trees); D124 §2.4 (the set-up's rule); `.claude/knowledge/bridge-presses.md`,
`person-waits.md`, `session-words.md`; the glossary.

## 0. What is true today

### 0.1 The acts the owner pressed, and what each is

Every one is a route the pipe's census makes the person's (`PipeCensus.cs`'s `Person` class), so from a terminal it waits for the window's
Confirm (`InstancePipe.cs`; D156 point 4) and, confirmed, runs as the owner's own press. The forms are the ones the verbs send.

| Act | Verb | Route and payload | What it does | Read at |
|---|---|---|---|---|
| Drive a repository, or stop driving it | `driver drive\|undrive <repo>` | `SET_DRIVABLE { repository, drivable }` | Writes `driver.json`'s drivable flag; no check that the repository is registered | `DriverChoicesCommand.cs:75`; `DriverRoutes.cs` |
| Give a repository trees | `driver trees <repo> on\|off` | `SET_TREES { repository, ownTree }` | Writes the standing choice a **fresh session or a new part** reads at its start (`DriverConfig.cs`, read at a start by `Driver.cs`); a running session is not moved, and **a continuation of an existing conversation resumes in its recorded directory** (`park.Tree`, `DriverContinuations.cs:227`; `ConversationTrees.cs:11` accepts any existing directory), so one that ran in the checkout goes on there. `off` makes the next fresh starts work in the checkout (D51) | `DriverChoicesCommand.cs:77`; `DriverRoutes.cs` |
| Hold or resume a repository | `driver hold\|resume <repo>` | `SET_HOLD { repository, held }` | Writes the hold; a change made outside the standing confirmation forgets that confirmation's own hold | `DriverChoicesCommand.cs:76`; `DriverRoutes.cs` |
| Set a repository up | `setup <repository>` | `SETUP_PRESS { repository }` | Recomputes its plan in the handler (`SetupRoutes.cs:46`), adds `SetupPressRules.RulesOf(plan)` to the repository's allow list just before it saves (`SetupPress.cs:170-171`): for the set-up, nine exact doctrine verbs; for an index quest, four verbs and two commands **generated** by `IndexForms.For`, fixed `node`, `dotnet run` or `python` lines over a generator file in the repository (`IndexForms.cs:56-61`); then publishes one ask **as the person's** | `SetupRoutes.cs`; `SetupPress.cs`; `SetupPressRules.cs` |
| Words to a session; a go-on after an outage | `sessions say <id> "…"` | `SESSION_INPUT { id, text, paths?, now?, level?, said: true }` (`SessionsCommand.cs:536-538`, `Body` adding `said` at `:501`) | With `said`, the route takes the verb's own path (`ConversationRoutes.cs:104-116`, `SessionsVerbs.SaidAsync`), not the window's screen branch (`:138-146`); either keeps the words with no author, so they are the person's (`SaidWord.Persons`), and a word a running session takes is kept on its ask as the person's | `ConversationRoutes.cs`; `Sessions.cs` (service) |
| Hand a session over | `sessions hand-over <id> --agent <a>` | `SESSION_HAND_OVER { id, agent, account?, said: true }` (`SessionsCommand.cs:440-441`) | For a quest's session, a box in the home the next look takes (`HandOverQuest.cs:101-109`), stopping it and carrying its quest on in the same tree with the agent named; the terminal's path records `HandOverAsker.Terminal` (`SessionsCommand.cs:1683`), the window's `HandOverAsker.Window` | `ConversationRoutes.cs`; `HandOverQuest.cs`; `DriverHandOvers.cs` |
| Answer a session's ask to hand over | `sessions hand-over <id> --approve\|--decline` | `SESSION_HAND_OVER_APPROVE { session, ask?, said? }`, `_DECLINE` the same | The yes or no to an agent's own `handover_propose`; with no `ask`, the one current in the files (`SessionsRoutes.cs:405`); a conversation's taken at once | `SessionsRoutes.cs:396-445`; `SessionsCommand.cs:444` |
| Stop a session | `sessions stop <id>` | `STOP_SESSION { id, said: true }` | Stops it; the record concludes `stopped` with the note *the person stopped it* (`Observation.cs:374`) or, parked, *The person stopped this at a checkpoint* (`SessionMoves.cs:81`); its quest held until *Try again* (`Planner.cs:259`) | `SessionsRoutes.cs`; `SessionMoves.cs` |
| Finish a session | `sessions finish <id>` | `RESOLVE_SESSION { id, state: "completed", said: true }` | Resolves a parked session with *The person finished this at a checkpoint* (`SessionMoves.cs:80`); a helper's words reach its asker, a workflow node waits, the person's words waiting carry it on, a driven quest otherwise sits for the owner's done (the terminal-trust design §2.1's `finish` row) | `SessionsRoutes.cs`; `SessionMoves.cs` |
| Try again | `driver retry <quest>` | `RETRY_QUEST { quest }` | Releases a quest a stop held | `DriverChoicesCommand.cs:82`; `DriverRoutes.cs:413` |
| Close a quest as done | `quest done <id>` | none: the service's own door, acting alone | TERMTRUST1b moves it onto the pipe as `QUEST_DONE`, not built. It is the owner's acceptance and stays theirs (§3.2) | `TASKS.md`'s TERMTRUST1b |

The go-on after an outage is `sessions say` with a few words to a session whose turn an API error ended and which then waited on the
person. Today the driver reads no such error (AGENTERRORS1's row: no `529`, no *Overloaded*), so nothing tells such a park from one whose
agent asked the owner something in prose. AGENTERRORS1 (D210, proposed) reads every agent's errors into kinds and tries a transient one
again by itself; past its bound its row 1a ends the record `failed` with the note code `ended.transient`, holds the quest
(`TransientHolding`, `held.transient`) and releases it by the person's words or by the `retry` act, which resumes the same conversation
with Daoris's own word `retry.go-on` (D210's design §8 and §11 row 1a). That release, and only after an error AGENTERRORS1 read, is the
steward's go-on here (§4).

**Since, on main at `7cda79f01`** (Codex's read of the final text, finding 13; the paragraph above is what was read at `aa16658e7`, kept
as that read's evidence): D210 is accepted (`D210.md:3`), and AGENTERRORS1a and 1a2 have landed. The driver reads provider errors
(`TransientRetries.cs:25`, logged `agent.error`), concludes an exhausted episode `ended.transient` (`Driver.cs:2794`), and *Try again*
releases a transient hold for the end it read (`StandingChoices.cs:121-125`, `WithTransientReleased`, written with no author). So row 11's
mechanism exists; D210 point 3 still reads *Only the person releases it* (`D210.md:65`), so §4.3a's note is still owed there.

### 0.2 The door SUPERVISOR1a-ii built, on main

D208 is accepted. SUPERVISOR1a-i is landing in parts and SUPERVISOR1a-ii, the door, is built dark and merged (D208's note of 2026-10-24):

- **The record**: `appointments` in D201's `data/standing-confirmation.json`, each `{ role, repository, holder, generation, since }`, never
  the key nor its hash (`StandingConfirmation.cs`'s writer).
- **A key per appointment**: `AppointAsync` draws a key for each appointment, opens its holder's terminal with it, and keeps only its hash in
  memory by generation (`_hashes`); a replaced or dismissed appointment's hash is forgotten in the same step under the judge's lock, then
  swept (`SupervisorDoor.AppointAsync`). An appointment read at a start has no hash: dormant until *Appoint again*, which opens the same ask.
- **The variable `DAORIS_SUPERVISOR_KEY`** (`HolderKey.Variable`, `SupervisorProof.cs:20`), removed as the first statement of every Daoris
  executable (`Launcher`, `GitDoor`, `PieceStart`, the service's HTTP and MCP hosts, the driver's host) and from every start
  (`WiringVariables.cs:103`, `GitWiring.Removed`), checked by `HolderKeyStripTests`. The driver's host's first act is `HolderKey.Take()`
  (`Daoris.Desktop.Driver.Host/Program.cs:337`), which reads the variable into memory and removes it (`SupervisorProof.cs:50-57`), so no verb
  after it finds the variable set.
- **The holder's terminal is a tab of Daoris's own terminal panel** (`TerminalModule.OpenHolderAsync`, the owner's direction of
  2026-10-24), the owner's shell with `-NoProfile`, in the role's emptied folder `work/rooms/roles/<role>/` (`HolderTerminal.Folder`,
  `HomeEntry.RoleRooms`), the key in that one shell's environment block alone, titled *Daoris: the supervisor's terminal*, its first lines
  printing the session's start line, `HolderTerminal.SessionLine` (1a-i replaces it by `SessionArguments`, §2.2), which allows `Read`,
  `Grep`, `Glob` and `Bash(daoris-driver sessions supervise:*)` alone. The line is `claude --setting-sources user --strict-mcp-config --mcp-config
  '{"mcpServers":{}}' --allowedTools …` (`HolderTerminal.cs:27-29`): it leaves out project and local settings and every MCP server, and
  **loads the user's own settings**, their hooks, enabled plugins, status line and helper commands, and the user's instruction file; D208
  wrote *with no user hooks or plugins* as a precondition (the supervisor design §2.2), which nothing enforces. `HolderTerminalTests.cs:90`
  holds the printed text, not what the session loaded. A shell's own end is told as `TERMINAL_EXITED` (`TerminalModule.Ended`,
  `:270-274`); a tab the page closes is taken out of the table before it is disposed (`:147-153`), so its end emits nothing. The judge is
  told of neither.
- **The census and the judge**: `PipeClass.Role` with `SUPERVISOR_ANSWER`; the judge's role method before the open path and any card,
  under the judge's lock and D201's caller look, then the form, the generation and an HMAC proof over the hello's nonce; a covered act runs
  under `PipeDoor.Role` (`RefusalLog.cs:104`, a `RoleJudgement { Role, Repository, Generation }`). `SET_APPOINTMENT { role, repository,
  holder, on }` is `Window`; `SUPERVISOR_WAITING` is `Open`, told by event under NOBLOCK1's rule, its answer naming the moment it was
  computed.
- **The flags**: `AppointmentsOffered` false, and `AppointsOffered = Offered && …` (`StandingConfirmation.cs:257`), so the door waits for
  D201's own offering. It is the only flag the supervisor's door reads: `AppointAsync` checks it and the role's name (`:537`), and the
  judge's decision and role methods check it alone (`:633`, `:657`). SUPERVISOR1a-iii's row waits on *1b and D201's offering*
  (`TASKS.md:270`); SUPERVISOR1b is open (`:267`). *Since, at `7cda79f01`* (finding 13): SUPERVISOR1b has landed, dark: the census's
  `Role` class holds `SUPERVISOR_ANSWER` and `SUPERVISOR_DEFER` (`PipeCensus.cs:298`), the latter's handler at `SessionsRoutes.cs:620`
  (D208's note, `D208.md:273`); SUPERVISOR1a-iii's row is `TASKS.md:269`.
- **The programs the key is handed to** (round 3's read, finding 1). The tab's shell is the one `HolderTerminal.ShellOf` picks
  (`TerminalModule.cs:238-249`): `pwsh` as the home's tools file names it, else PATH's (`Terminals.cs:44-47`, `:90-96`; `Tools.cs:284-289`,
  a named file), else Windows PowerShell found on the tools' PATH. Its environment is Daoris's own, inherited from whatever started Daoris,
  plus the key (`Terminals.cs:7`). The owner then types a bare `claude`, which that shell resolves by its PATH (`HolderTerminal.cs:27-29`);
  Daoris's own Claude Code toolchain installs it with `npm install -g` (`Adapters.cs:2359`), a folder of the account's, and the harness's
  own updater and every child it starts inherit the key. Each of these is a file or a variable any process of the account can change.
  Daoris also keeps a managed, pinned `claude-code` of its own in the home's toolchain (`HarnessSettings.ManagedBinary`, the maker's
  `bin/claude.exe` moved there only once it verified), which the ACP door already starts by its absolute path (`HarnessRoster.cs:1234-1236`).

What D208 decided that this design reuses whole: a role is appointed only by the owner's press in the window, per scope, off by default;
its acts are named routes in exact forms; its holder decides each, with no card, and an act it does not cover is refused, never turned into
a card; each act is recorded as the role's; the holder is a dedicated session that runs no branch's code; the key's hash is in memory alone,
the appointment lasts until Daoris restarts; every write of the record is one mutation step under the judge's lock, a revoke sweeping after
it (D208 points 1 to 3; the supervisor design §2).

### 0.3 D201's standing confirmation

Built and not offered (`StandingConfirmation.OfferedInThisBuild`). It names four presses, `stop`, `finish`, `hold` and `resume`, each in an
exact form (`IsForm`, `StandingConfirmation.cs:763`: field names compared without case, `:766-783`, a name seen twice refused; values
exact), judged under one lock, the caller placed outside every agent job (`StandingCaller`), a covered press run inside
`PipeDoor.EnterStanding()`, recorded as the person's with the coded note part `standing.terminal` (`SessionMoves.Standing`). Its
containment, TERMTRUST1a part (i), is built and on the install; its §8 case 4 has been seen for driven sessions, carry-ons and
conversations and not yet for a plugin's process, a partner's piece, an intake, a helper, a reviewer, a brainstorm pass or a workflow node
(D201's note of 2026-10-24). D201 was asked for exactly this case: the steward's stops and finishes from a terminal (`D201.md:7-13`).

### 0.4 Who the steward is today, and what its working session runs

The steward is an agent session the owner runs by hand outside the install (D193 part 18; the company concept's *Management* row). It
plans, files rows, dispatches builders, **merges branches through `tools/merge-branch.mjs` and runs the gates** (the `dispatch-subagent`
skill; DOGFOOD1), so it runs the code of every branch it merges and tests. D208's round-4 point is exactly this: the session that holds a
role's key runs no branch's code, so the steward's working session never holds it (D208 point 2).

### 0.5 Records that say the person where the person did not act

- **A terminal's confirmed words are the person's**: kept with no author. To a running session they are also kept on its ask through the
  added door (`ConversationRoutes.cs:146` → `SessionSay`'s `WordsOnAsk.Keep` → `POST /api/sessions/{id}/added`, `Program.cs:1465`), which
  keeps every word it is handed as `AskWordKind.Added`, *one thing the person said on an ask, verbatim* (`Asks.cs:200`), quoted to every
  later session as the person's own words (`AskWords.cs:143-144`). The say and taken doors keep only `Persons` words on the ask; the added
  door has no such filter.
- **A move's own note is the person's**: *the person stopped it* (`NoteCodes.EndedStopped`, `Observation.cs:374`); *The person stopped
  this at a checkpoint* and *The person finished this at a checkpoint* (`StoppedCheckpointStopped`, `StoppedCheckpointFinished`,
  `SessionMoves.cs:79-81`), worded so on the page (`locales/*/note.json`); the planner holds a stopped quest by its state
  (`Planner.cs:259`) and says *you stopped session …; Try again carries it on*. A suffix after these does not make them true.
- **A hand-over's reason `press` reads *the person asked for it*** (`HandOverRecords.cs:46`, `:60`); its asker is one of `agent`,
  `terminal`, `window`.
- **A set-up's ask is published as the person's** (`SetupPress.cs`'s publish, no session and no agent mark), so the ask is not `byAgent`
  and every session on it is told its words are *the person's own words … verbatim* though Daoris composed them.
- **The hand-over's box and the handed file are home files** (`HandOverFiles.ReadBox`, `ReadHanded`), consumed by the driver's look
  (`DriverHandOvers.cs:84`), which writes their `askedBy` on to the handed quest (`:133`); any process of the account can write them.

### 0.5a Where an act takes effect later than its handler

- **A live stop** from the terminal writes a `SessionRequest` (`SessionsCommand.cs:949`); the driver's `SessionRequestWatch` later builds its
  move note (`:345-372`), outside any `PipeDoor.Role`.
- **A hand-over** keeps a box; the next look stops the session and carries the quest on (`HandOverQuest.cs:101-109`,
  `DriverHandOvers.cs:102`, `:133`). **The start then reads the handed file again by itself** (`Driver.cs:1896`, and `:1087`'s agent),
  taking its agent, account and note from disk (`HandOverFiles.cs:97-99`); a consumed handed file stays the quest's agent and account
  override.
- **A yes to an agent's ask to hand over** writes `approved` into the ask's state file (`HandOverAskWord.Approve`, which takes the agent from
  that writable state, `:44-50`); at the turn's end `AtTurnEndAsync` turns it into a handed file **as `HandOverBy.Agent`**
  (`DriverHandOvers.cs:279-294`).
- **A park's continuation** first moves the record to working (`DriverContinuations.cs:268-274`); the supervisor's path, where the word is
  not handed, restores the park with its other words (`:795-819`), and the service's restore takes back only supervisor words
  (`SessionLedger.cs:1091-1097`).

### 0.5b The owner's doors onto the same things

The owner acts on a repository's settings, a quest or a session through more doors than the routes named in §0.1: the window's presses and
a terminal's confirmed cards reach the same `DriverRoute` handlers through the one dispatcher (`InstancePipe.cs:778-780`); **Ask Daoris's
applied proposals** (`HELP_APPLY`, `Person`, `HelpRoutes.cs:494`) drive, undrive, hold, resume, set trees on or off, set the line and the
landing through the config editor (`HelpSettingProposals.cs:80-105`), retry a quest (`:61`) and set a repository up
(`HelpSetupProposals`), none through `SET_*`; D201's `Forget` of the standing hold runs only in `SET_HOLD` (`DriverRoutes.cs:62`). Other
quest-scoped `Person` routes (`SESSION_GO_AHEAD`, `SUPERVISOR_OVERRULE`, `SESSION_QUEUE_EDIT` at `ConversationRoutes.cs:355`, `CANCEL_TURN`,
`SESSION_GO_ON_NEW`, `SESSION_GO_ON_BRIEF`, `SESSION_PERMISSION_ANSWER`, `DISCARD_SESSION_TREE`) act on a quest's sessions too. And the
service's own person doors (`PersonDoors.cs`, 35 `Person` doors) reach records without the instance: `quest done` until TERMTRUST1b among
them.

### 0.6 What an agent's ordinary reach already does

D201 recorded that an agent's ordinary reach already makes several of these acts without any grant, recorded as the person's: the request
folder takes a terminal's words and moves as the person's (`SessionRequestWatch.cs`), `driver.json` and the hand-over files are home files
any process of the account writes, and the service's doors stay ungated until PERSONDOOR1g (`D201.md:59-63`). This design adds no reach to
an agent: every act below is admitted only by a role's key, which no agent's environment holds (§6.3); and a home file claiming the
steward is refused unless this run's memory admitted it (§5.1).

## 1. The decision in one paragraph

The **steward** (管家) is a second role on D208's appointment, for this Daoris: every repository its registry names. The owner appoints it
once in the window, on Settings → This Daoris → *Roles*, with their own press; from then until Daoris restarts, its holder makes the
**management acts** of §3.1 with no card, each judged in an exact form, each written with a record of its own that names the steward and
never the person, in the machine log and in *Notifications*, and each overruled by the owner: revoked at once, reversed by the owner's own
press, and anything the owner does to a repository, a quest or one of its sessions, through any of their doors, makes it theirs for the
rest of the run (§5.4). An act whose effect lands later is a pending operation of the run's memory, cancelled by a revoke or the owner's
act and checked again by its final consumer (§5.1a). It never makes what is the owner's, closing a quest as done included (§3.2). **One
appointment model**: SUPERVISOR1a-ii's door as built, amended so a second role may share the run's holder or have its own, each role
offered by a flag of its own, and D201's four presses become the steward's acts rather than a separate grant (§2). Its **go-on** is the
release of a quest AGENTERRORS1 holds after its agent's provider kept failing, resumed with Daoris's own fixed word; it adds no word to
any record (§4); free words are a later row with their own read. The **holder** is the dedicated session, now the steward's management
desk, its harness Daoris's own pinned binary started by Daoris itself, on a configuration Daoris writes and checks: it reads through Daoris's open routes and makes the role's acts, and runs no
branch's code. The steward's **working session**, which builds, tests and merges, holds no key and no role: its presses ask as today, and
its management needs reach the holder through the desk (§6).

## 2. One appointment model

### 2.1 What is shared

The steward is a role in D208's sense (the supervisor design §2.1, points 1 to 8): appointed by the owner's press in the window, its acts
named, its holder deciding each, each recorded as the role's, said and overruled, bounded by what is the owner's, revoked at once, on
this machine alone. Nothing here is a third model: D201's record holds the appointments, the judge's role method judges them, and D201's
caller look stays the first denial.

### 2.2 The amendment of the built door

SUPERVISOR1a-ii draws a key per appointment and opens a tab per *Appoint…*. With two roles, an owner who appoints both runs two dedicated
sessions and presses twice after every restart. So, amending D208 §2.2 and the door as built, before `AppointmentsOffered` is on:

- **A holder is a record of the run's memory, apart from the appointment.** Each holder has an id, its key's hash, its tab's id and its
  roles; each appointment names its holder. A proof is checked against its appointment's holder's hash, then the act against that
  appointment (`_hashes` keyed by holder instead of generation; `RoleJudgement` gains `Holder` and `Act`).
- **The first *Appoint…* of a run draws a key and opens a tab, as built.** A later *Appoint…* while a holder's tab lives offers, in its
  ask, two presses: **Add to the terminal Daoris started at 14:02** (the same key, no second tab, a new appointment and generation), or
  **A terminal of its own** (a second holder, as built). The ask says what sharing means: *a session holding both roles reads the questions
  and messages of the agents it supervises while it can also drive, stop and hand over their work* (§6.3). Neither is preselected.
- **A role revoked this run is re-appointed only to a terminal of its own**, never added back to a holder that held it: the session that
  held it may have been misled into what made the owner turn it off.
- **A holder ends with its last role or its tab.** **Revoking one role ends that appointment alone** (Codex's final read, finding 10): its
  record, its admitted acts and its pending operations and bindings, by its generation, in one mutation step; the holder's hash stays
  while the holder holds another live appointment, so revoking the steward never disables the supervisor it shares a tab with, nor the
  reverse. Revoking a holder's last role, or its tab ending, drops its hash and every appointment it holds in one mutation step, swept
  after it; the tab, where it still lives, prints *the roles this terminal held are turned off* and
  the next *Appoint…* draws a new key and a new tab. **The tab's end reaches the judge by one internal callback, `HolderEnded(tab)`**, which
  the terminal module calls on every path a holder's tab ends by: the page's `CLOSE` (before the tab leaves the table, since `Ended` then
  emits nothing, `TerminalModule.cs:147-153`, `:270-274`), the shell's own exit, the module's disposal at Daoris's close, and a shell that
  exits while `AppointAsync` is still opening it (the appointment then is never written, or is dropped in the same step if it was). The
  page's `TERMINAL_EXITED` stays the page's.
- **The variable is `DAORIS_ROLE_KEY`**, renamed from `DAORIS_SUPERVISOR_KEY` at every site the build names (§0.2's list,
  `HolderKey.Variable`, `WiringVariables`, `HolderKeyStripTests`'s pattern), since a shared holder's key is no one role's. No install has
  held the old one: `AppointmentsOffered` was never on.
- **The entry marks a role's terminal.** `HolderKey.Take()` records, beside what it read, `HolderKey.RoleTerminal`: that the variable was
  **present** at the entry, whatever its value and whether or not it parses as a key. Every guard of §3.3 reads that marker, never the
  environment after the entry, which `Take` has already emptied (§0.2).
- **The folder is the holder's, by its id** (Codex's final read, finding 11): `work/rooms/roles/holders/<holder id>/`, the id Daoris
  draws for the holder, created emptied at the holder's start (`HolderTerminal.Emptied`, by holder rather than by role: as built
  `Folder(home, role)` gives two supervisor holders of different repositories one folder, which the second start deletes,
  `HolderTerminal.cs:45-52`), and **never moved or emptied while the holder lives**: adding a role to a live holder changes its title and
  its brief, not its folder. The folders of holders that no longer live are removed at the next holder's start. The tab's title names its
  roles (*Daoris: the steward's and supervisor's terminal*).
- **The start line allows every role's verbs and the open reads it needs, each by its exact prefix**, whichever role the tab was opened
  for. **It is an argument vector Daoris starts itself as the tab's process, not a line the owner types** (§6.1, round 3's read's option
  (a)): the image is Daoris's managed, pinned `claude-code` by its absolute path, and the arguments after it are these, each one element,
  so no shell parses or re-quotes them. Stated whole, as `HolderTerminal.SessionArguments` holds it and `HolderTerminalTests` asserts it:

  ```
  <toolchain>/claude-code/<pin>/bin/claude.exe --setting-sources user --settings '{"disableAllHooks":true}' --strict-mcp-config --mcp-config '{"mcpServers":{}}'
    --allowedTools 'Read' 'Grep' 'Glob' 'Bash(daoris-driver sessions supervise:*)' 'Bash(daoris-driver steward:*)'
    'Bash(daoris-driver roles brief)' 'Bash(daoris-driver sessions --json:*)' 'Bash(daoris-driver notices:*)'
    'Bash(daoris-driver history:*)'
    'You hold a role Daoris appointed. Run `daoris-driver roles brief` and do as it says.'
  ```

  (The quotes only mark each element; the JSON arrives as written, `{"disableAllHooks":true}` with its quotes. `sessions --json` is the
  list, `SessionsCommand.cs:350`; a session's log is read by path with `Read`.) **The last element is the holder's initial prompt**
  (Codex's final read, finding 12), `HolderTerminal.InitialPrompt`, fixed text in Daoris's code, the harness's positional prompt that
  starts its interactive session with it: with no instruction file and the tab's printed lines the owner's screen (`TerminalModule.cs:248`,
  `open.Say`), it is the model's first input, and it names no key, no appointment and no particular role (the brief says which it holds). It is an element of `SessionArguments`, so
  the walk (§6.1) checks it like the rest.

  with the configuration directory of §6.1 in the environment Daoris composes for it. No `sessions:*`: the press verbs are not allowed, and are refused
  `role-terminal` besides (§3.3). The permission is not the authority: a verb of a role this holder does not hold is refused
  `not-appointed` by the judge, so adding a role to a live holder needs no change to its session's permissions or its restart.
- **The holder's brief is Daoris's own, printed by an open verb**: `daoris-driver roles brief` prints, for each role the build knows, what
  it does, what it never does and how its verbs answer, from text in Daoris's code, naming no key and no appointment, headed by its
  revision. **Every answer of a role's verb (`steward …`, `sessions supervise`) names the holder's current roles and the brief's revision**,
  and the brief tells the session to read `roles brief` again whenever either changes from what it last read: what the session reads is
  the verbs' answers, not the tab's screen. The tab's printed line (*the steward was added to this terminal at 14:05*) is for the owner.
  The holder loads no instruction file (§6.1), so this is where its brief comes from, the initial prompt telling it to read it.

### 2.3 Where the steward's appointment lives, and its form

In `appointments` beside the supervisor's, in `data/standing-confirmation.json`:

```json
{ "role": "steward", "scope": "machine", "holder": "terminal", "generation": "…", "since": "…", "acts": ["drive", "…"], "setup": false }
```

- **`scope`**: `machine` or `repository:<name>`. The steward's is `machine`: every repository the service's registry names on this machine
  as each act is judged, one registered later included, which the ask says. The supervisor's is `repository:<name>`; a record written by
  the built door with `repository` and no `scope` is read as `repository:<name>`.
- **`acts`**: the acts the ask named when the owner pressed it, from the build's offered set (§10's flags). An act not in it is refused
  `not-granted`, so a later build's act never joins an appointment the owner made before it (a new build is a restart: the appointment is
  dormant, and *Appoint again* names the current build's acts).
- **`setup`**: the owner's own tick in that ask for §3.1's set-up act; absent or false, the set-up is refused `not-granted`.
- **The form**: `SET_APPOINTMENT { role, scope, holder, on, share?, setup? }`, `Window` in the census as built, through the one mutation
  step; `share` is the later *Appoint…*'s choice (§2.2). The supervisor's ask sends `scope: "repository:<name>"`; the built
  `{ role, repository, … }` form is read as that scope for one release and then refused `form`.

***Appoint again* re-appoints one role at a time, from the window's own ask, never from the file.** A role found at a start is shown dormant
on its own row (the steward's on *Roles*, the supervisor's on Setup → Driving as built) with its own *Appoint again*, which opens that role's
ordinary ask: its acts the build's offered set, the set-up tick **always shown off**, the found record's `acts` and `setup` never read into
it. A steward record planted in the file while Daoris was down therefore turns nothing on and widens nothing: at most it shows a dormant
row the owner can dismiss.

### 2.4 D201's presses become the steward's acts

D201 was asked for the steward's stops and finishes (§0.3). With the steward a role, the same presses would have two doors: a standing
grant that records them as the person's from an unkeyed terminal, and the role that records them as the steward's. Two doors for one act
is the third model this design must not make, and the first records the steward's act as the owner's, which the direction forbids. So:

- **`stop`, `finish`, `hold` and `resume` are steward's acts** (§3.1 rows 4 to 7), in D201's forms and conditions, judged under its lock,
  the caller placed by its look, `resume` releasing only a hold the steward made this run that nothing has touched since (D201's own rule,
  its memory moved from the grant to the role).
- **TERMTRUST1b's `done` is never the steward's.** Closing a quest as done answers its requirements by none, publishes a chain's next step
  and resumes a quest waiting on it, and no press undoes those (the terminal-trust design §2.1, the `finish` row's *not by a press*;
  `Quests.cs:2057`, `:2066`: *Reopen* needs a taken quest with a current submission). It is the owner's acceptance (§3.2). TERMTRUST1b
  stays D201's row and the owner's card.
- **The standing confirmation is not offered as a grant of its own.** `OfferedInThisBuild` stays false; its row on This Daoris becomes
  *Roles* (§5.3); `SET_STANDING_CONFIRMATION` stays `Window` and refuses `not-offered`. Its code stays and serves the role: the judge, its
  lock and record, the forms, the caller look, the run's memory of holds, the *Notifications* line.
- **Each role is offered by a flag of its own, and its activation waits on its own proof.** `AppointsOffered = Offered && …`
  (`StandingConfirmation.cs:257`) is replaced by two role flags, **`SupervisorOffered`** and **`StewardOffered`**, both false in this
  build; `AppointmentsOffered` becomes derived (either role offered) and gates nothing by itself. A role's flag gates its own ask and row,
  its `SET_APPOINTMENT` (`AppointAsync`, `:537`, refuses `not-offered` for a role whose flag is off), and its admission: the judge's
  decision method (`:633`, `SUPERVISOR_ANSWER`) reads `SupervisorOffered`, its role method (`:657`, `ROLE_ACT`) reads the flag of the
  role the act names. So turning on the steward never opens the supervisor, and turning on the supervisor never opens the steward.
  **SUPERVISOR1a-iii's prerequisite becomes SUPERVISOR1b and D201 part (i)'s case 4 seen on every door**, in place of D201's offering,
  which D209 keeps off for good; it turns on `SupervisorOffered`. STEWARDROLE1d turns on `StewardOffered` after the same containment's
  look (D201's note on order) and its own proof (§10).
- **What the owner loses**: an unkeyed terminal of their own pressing those four without a card. Nobody asked for it: D201's direction was
  the steward's presses, and the owner presses in the window in one click. Offering it later is a widening the owner may choose (§11).

### 2.5 Why not one grant for both

A role over D201's grant alone (an unkeyed terminal outside every agent job) was weighed: it is how D201 judges and needs no key. It fails
on attribution, which is the direction's point: an unkeyed caller outside every job is the owner's own terminal as much as the steward's
(D201 point 4), so the record cannot say the steward acted. D208's key is what lets the record name the role; so the key carries the
steward too.

## 3. The acts

### 3.1 What the role covers, and why each is safe to delegate

Each act is one route in one exact form: the fields named and nothing more, field names compared as D201's `IsForm` compares them (without
case, since the handler reads them so and a form must mean what its handler reads; a name given twice in any casing is refused), every
value exact. A form with another field or value is refused `form`, never asked. Each runs its route's handler on the path named, inside
the role's door (§3.3), which writes the record named; no handler runs its window path (`HandOverAsker.Window`, `door: "screen"`).

Every session act is on **a quest's own session** (`IsQuestSession`): never the owner's conversation, Ask Daoris, an intake, a teammate.

| # | Act | Verb | Route and exact form; the path it runs | Conditions Daoris holds | Its own record | Why it is the steward's to make |
|---|---|---|---|---|---|---|
| 1 | Drive | `steward drive <repo>` | `SET_DRIVABLE { repository, drivable: true }` | The repository in the service's registry on this machine (`unregistered`); not the owner's this run (`owner-set`, §5.4); its landing rule not a plugin that pushes (`landing-pushes`, `SetupPressRules.LandsAs`'s plugin); **its sessions opening trees of their own** (`no-tree` where `!config.OpensOwnTree(repository)`, `DriverConfig.cs:1423`), so the steward gives trees first (row 3, under `landing-pushes`); **and every start its drive causes held out of the checkout at the start itself** (§3.1a), since `OpensOwnTree` governs only a fresh tree | *Set by the steward* (§5.1) | Starts work only through Daoris's own sessions, on the accounts and within the cap the owner set (`SET_CAP`, never the steward's): the development's own runs (D191 point 3). Undone by the owner's *Stop driving*. A repository whose landing pushes is the owner's to drive, since its every landing publishes. **A repository with no session trees is the owner's to drive**: trees are opt-in (`DriverConfig.cs:1416-1424`), so a repository never driven is by default a checkout one, and there Daoris's sessions work and commit in the owner's checkout, where a done is `NoTree`, already in place, with no landing for the owner to accept (`AutoLanding.cs:207`) — the reason row 3 keeps `trees off` the owner's |
| 2 | Stop driving | `steward undrive <repo>` | `SET_DRIVABLE { repository, drivable: false }` | As 1, but `landing-pushes` and `no-tree` | As 1 | Only narrows: no session is started; running ones go on as an undrive leaves them today |
| 3 | Give trees | `steward trees <repo> on` | `SET_TREES { repository, ownTree: true }` | As 1 but `no-tree`, **`landing-pushes` included**, judged on `LandingRules.Effective` | As 1 | Each **fresh session** started after it works in a tree of its own; a continuation of an existing conversation goes on in its recorded directory (§0.1), and **so does a new part that resumes one** (`resumedIn`, `Driver.cs:2108-2110`, which takes the recorded directory whenever trees are on, the checkout included; Codex's final read, finding 2): where a steward's act caused that start, §3.1a holds it for the owner. Not a narrowing where the landing pushes: in the checkout a done is `NoTree` and lands nothing, in a tree it reaches the landing, and an auto-accepting branch rule with a pushing plugin publishes it (`AutoLanding.cs:203-209`), so that is the owner's, as row 1's drive. **`off` is never the steward's**: later starts would work in the owner's checkout, where their uncommitted work lies (D51) |
| 4 | Hold | `steward hold <repo>` | D201's `hold`, `SET_HOLD { repository, held: true }` | As 2 | As 1 | Only narrows (D201 §2.1) |
| 5 | Resume | `steward resume <repo>` | D201's `resume` | As 2; a hold the steward made this run that nothing has touched since (`not-own`) | As 1 | Undoes its own act and nothing of the owner's |
| 6 | Stop | `steward stop <id>` | D201's `stop` form, `STOP_SESSION { id, said: true }`; **run in the instance**: a parked session resolved in the ledger's step, a live one stopped by the driver's own stop with the steward's note handed in at the call (`SessionRequestWatch`'s `_stop`), **never a `SessionRequest` file** (§5.1a) | A quest's own session; **parked, or live in a process this instance runs** (`running-elsewhere` otherwise, nothing written: the driver's own stop reaches only this instance's processes, `SessionMoves.StopAsync` answering `Elsewhere` for one another Daoris process of this machine runs, `SessionMoves.cs:22-31`, which the verb reaches today only through the request folder, `SessionsCommand.cs:946-951`); its quest not the owner's this run; **not where the stop would make a pushed landing due** (`landing-pushes`: its quest `Done` and `AutoLandingRules.Concluded` answering `Due` for the stopping state under an effective rule whose plugin pushes), judged at admission and again under the gate | `ended.stopped-steward`, or parked `stopped.checkpoint-stopped-steward` (§5.1) | Only ends work; the tree is kept; the steward's own *Try again* (row 8) or the owner's carries it on. Where a stop would release a push, the moment of publication is the owner's |
| 7 | Finish | `steward finish <id>` | D201's `finish`, `RESOLVE_SESSION { id, state: "completed", said: true }` | D201's: no process of it here (`running-here`), moved only from `awaiting-person` in the ledger's step, never through the request folder; a quest's own session, **not a helper's, not a workflow node's run** (`not-quest-session`); **no word waiting on it** (`words-waiting`); its quest not the owner's; **`landing-pushes` as row 6**: a park of a `Done` quest is held from its due landing only by being parked (`AutoLanding.cs:206`, `Attention.cs:365`), so finishing it would release a push | `stopped.checkpoint-finished-steward` | A session already waiting with nothing running, nothing said to it and nobody but its quest after it: what the finish ends is that session alone (`SessionMoves.cs:62` advances the session and nothing of its quest), and **it neither accepts nor closes the quest: an open or taken quest stays so for the owner's done or acceptance (§3.2), and an already-done one stays done** (Codex's final read, finding 14). `decline` is never the steward's |
| 8 | Try again | `steward retry <quest>` | `RETRY_QUEST { quest }` | The quest's last session stopped **by the steward** this run (`ended.stopped-steward` or the checkpoint's, and this run's memory of the act), nothing touched since (`not-own`); not the owner's; **`StandingChoices.Retry` answering `Released` for that stop** (`not-own` otherwise: an `Exhausted` verdict's forgiveness, `StandingChoices.cs:108-131`, is the bound before the owner, D58) | `retried` by the steward | Undoes its own stop, as `resume` its own hold; a stop the owner made stays theirs (D201 point 3), and strikes are never forgiven by it |
| 9 | Hand over | `steward hand-over <id> --agent <a>` | `SESSION_HAND_OVER { id, agent, said: true }`, **no `account`**, the terminal's path | A quest's own session; `agent` in the machine's effective `build` order, the one a quest's start reads (`ActorChoices.Effective("build", config)`, `Driver.cs:702`, which takes no repository), within the fence a repository's hand-over rule passes where it has one (none does today: `HandOverRule(Mode)`, `HandOverRules.cs:13`); `not-actor` otherwise; its quest not the owner's; no box waiting for it (`HandOver.Waiting`); **no owner-only hold released by it** (`owner-held`, §3.1b) | A pending operation (§5.1a); box and handed file `askedBy: steward`, `why: steward`, consumed only by it | Carries the quest on in its own tree with an agent the owner already put in the machine's `build` order; the account is Daoris's pick by the owner's rules, never named by the steward. The owner hands back with their own press. After 1e (§10) |
| 10 | Answer a hand-over ask | `steward hand-over <id> --approve\|--decline --ask <ask> --agent <a>` | `SESSION_HAND_OVER_APPROVE { session, ask, agent }`, `_DECLINE { session, ask }`, no `said` | A quest's own session; `ask` the one open in its files **at application**, and **`agent` the agent the holder's listing showed, carried in the signed form**, equal to the ask's agent at application and again at its final consumer (`ask-changed` otherwise), and in the machine's effective `build` order as row 9 (`not-actor`); its quest not the owner's; the yes, as row 9, releasing no owner-only hold (`owner-held`, §3.1b) | The yes a pending operation (§5.1a), its handed file `HandOverBy.Steward`; `handover.refused` by the steward for the no | As 9 for the yes; the no only keeps the session where it is. After 1e |
| 11 | The go-on | `steward go-on <id>` | `ROLE_ACT`'s own handler (§4): AGENTERRORS1's `retry` release of its quest, held for this session's `ended.transient` | §4.2's, `not-outage` first; **one per quest per run** (`go-on-spent`) | `retried` by the steward, its note *The steward asked it to go on*; the word the agent reads is AGENTERRORS1's `retry.go-on` (§4) | The go-on after an outage, which grants nothing and says nothing but Daoris's own *go on*. After 1d and AGENTERRORS1a |
| 12 | Set up | `steward setup <repo>` | `SETUP_PRESS { repository }`, the set-up form only | The appointment's `setup` tick (`not-granted`); held again **inside the application**, immediately before the rules are saved, against the plan being applied: the set-up's plan, never an index quest's, its rules exactly `SetupPressRules.Rules` (`plan-changed` otherwise, nothing saved, nothing published) | The ask `by: steward` (§5.1) | The rule is nine exact doctrine verbs Daoris names in its own code (D124 §2.4); adding it is the one widening in this table, so it is the owner's: granted by their own tick at the appointment. An index quest's two commands run the repository's own generator code, so they never are. After 1g |

**Every act is the steward's by its own decision.** No act is a card, a proposal or a request to the owner; the holder decides, Daoris
judges the form and the bounds, the act runs. What the owner sees is the record (§5).

### 3.1a The checkout exclusion, at the actual start (Codex's final read, finding 2)

`no-tree` reads the repository's standing choice, and that choice governs only where a **fresh** tree is opened: a continuation runs in
its park's recorded directory (`DriverContinuations.cs:227`, `park.Tree`), and a new part takes its resumed record's directory whenever
trees are on (`Driver.cs:2108-2110`, `resumedIn`), opening a tree only where there is none (`:2166`), so an earlier session that ran in
the checkout before the repository had trees goes on there (`:2196`, `workTree`). A drive admitted on `OpensOwnTree` can therefore
restart earlier work in the owner's checkout. So the exclusion is held **where the directory is actually chosen**, not at the act:

- **A start a steward's act caused** is one whose repository the steward drove this run (memory's own record of row 1, not since
  `owner-set`), or whose quest is carried on by a steward's pending operation or binding (rows 8 to 11, §5.1a). Its work directory, as
  the start resolves it (`opened`, `resumedIn`, `joinedIn`, the root; for a continuation `park.Tree`), must be a tree this home opened
  for the repository (a path under the home's session trees, which `Context.Trees` opens; no such predicate exists on main, so 1b adds it), **never the checkout nor any other directory**.
- **Otherwise the start is held for the owner, not relocated**: nothing starts, the quest is held with the reason *Its earlier work is
  in your checkout, so carrying it on is yours* (`held.checkout`), said on the role's line, and a steward's pending operation on it is
  ended. The owner's own *Try again* or *Drive* makes the quest or the repository theirs (`owner-set`), and the start then runs as today.
  Daoris never moves a conversation's directory implicitly.
- Rows 8, 9, 10 and 11 hold the same rule at their consumers: the start they release is a start a steward's act caused.

**Proof (row 1b, and 1e and 1f for their rows)**: a repository whose earlier session recorded its checkout, trees then given and the
repository driven by the steward: the session's continuation and a new part resuming it each held `held.checkout`, nothing started in
the checkout, and each carried on after the owner's *Try again*; a fresh quest of the same repository started in its own tree.

### 3.1b The owner-only holds a hand-over must not release (Codex's final read, finding 1)

A hand-over from a held record is itself a release: the planner passes a transient hold and a person's stop where the quest was handed
from that session (`Planner.cs:1488-1495`, `TransientHolding`'s `!HandedFrom`; `:1516-1524`, `StopHolding`'s; `HandedFrom` at `:1482`), and a hand-over chooses
a new agent and a briefed start. Row 11's one go-on and row 8's own-stop rule would be bypassed by rows 9 and 10. So rows 9 and 10 are
refused **`owner-held`** where the session's quest is held, or its record would hold it, by:

- **a transient hold** (`TransientHolding`, or the record `ended.transient`): its release is row 11 alone, once per quest per run, and
  never by a hand-over;
- **a stop** (`StopHolding`, `PersonStopped` or the steward's stop codes) **other than this run's steward stop untouched since**, as row 8
  judges it (this run's memory of the act, `not-own`): the owner's stop, one from before this run and one made since by any other door
  stay held;
- **any other hold the owner alone releases** (`Exhausted`'s strikes, a pause), as rows 8 and 11 refuse to forgive them.

Judged at admission, again under the gate, and again at **every consumer** (the look, the turn's end, `GoAsync`, the start, §5.1a), since
the session may end held between them: a consumer that finds such a hold ends the operation, hands nothing, and says it. **Proof (row
1e)**: a steward's hand-over and a steward's yes each refused `owner-held` at admission on a quest held `ended.transient` (the go-on
spent and unspent alike) and on one held by the owner's stop or a stop of an earlier run; and each admitted, then the session ending
transient or stopped by the owner before the look, the turn's end and the start in turn, each consumer ending it with nothing handed.

### 3.2 What the role never makes

Refused `not-role` at the judge, before any handler, whatever the form: the census's other `Person` routes are never a role's act. By the
direction's six, with the routes that carry them (`PipeCensus.cs`'s `Person` class):

| The owner's | Routes | Why |
|---|---|---|
| A production or live go-ahead | `SESSION_GO_AHEAD`; the go-on while a go-ahead on the ask is open (§4.2) | D135 point 2, D191 point 3: answered by the owner alone |
| A sign-in, an account | `ACCOUNT_USE`, `HARNESS_ACTION`, `HARNESSES_REFRESH` | D37, D191 point 3 |
| An access widening | `SET_ACCESS`, `SET_READ_ACROSS`, `SET_WRITE_ACROSS`, `TRUST_FOLDER`, `SET_HELP_GRANT`, `SET_HELP_MODE`, `SESSION_PERMISSION_ANSWER`, `RULE_ACTION`, `RULE_PROPOSAL`, `PLUGIN_*`, `TOOLS_*`, `SET_TREES` off, `SETUP_PRESS` beyond §3.1 row 12, `SET_HEAVY_GATES`, `WORKFLOW_LANDED`, `SET_STANDING_CONFIRMATION`, `SET_APPOINTMENT` | D72, D74, D183, D201 §2.2; a role never widens its own reach |
| A push or a publication, and **accepting what is delivered** | `LAND_SESSION_TREE`, `LAND_ENTRY`, `LAND_ASK`, `HANDOFF`, `SET_LANDING`, `SET_COMMITS`, `RESOLVE_SESSION` declining, `TREES_SYNC`, **`QUEST_DONE`** and `quest done`, a quest's *Reopen*; `SET_DRIVABLE` on a repository whose landing pushes | D191 point 3; D193 part 1: the owner accepts or declines what is delivered |
| Spending beyond the development's own runs | `SET_CAP`, `SET_ACTOR`, `SET_AGENT_SETTINGS`, `SET_COOLOFF`, `ASK_OPINION`, `SESSION_HAND_OVER`'s `account` | D191 point 3: how much runs and on what is the owner's |
| A deletion of the owner's data | `DISCARD_SESSION_TREE`, `DISCARD_SESSION_BRANCH`, `RECOVERY_DROP`, `SWEEP`, `HISTORY_CLEAR`, `SYSTEM_DOWNLOADS_CLEAR`, `SYSTEM_SPACE_FREE`, `SESSION_DELETE`, `SESSION_ARCHIVE`, `WORK_ABANDON` | D191 point 3; D37, D145 |
| What only the owner's word lets go on, or a role's own check | `SESSION_ANSWER_ALONE`, `SESSION_QUIET_CARRY_ON`, `SESSION_QUIET_LEAVE`, `RETRY_QUEST` on a stop not the steward's own, `OPINION_ANYWAY`, `OPINION_MYSELF`, `WORK_HELD`, `WORKFLOW_NODE`, `SUPERVISOR_OVERRULE`, `SESSION_QUEUE_EDIT` | D188 point 4 (the bound before the owner), D200, D201 point 3, D154, D208 |
| Every other `SET_*`, every route not in §3.1, and words | `SET_LINE`, `SET_LANGUAGE`, `SET_NOTIFY`, `SET_REVIEW`, `SET_OPINION`, `SET_HELPERS`, `SET_HAND_OVER`, `SET_STANDING`, `SET_STRIKES`, `SET_KEEP`, `SET_SESSION_CHECKS`, `SET_SESSION_OPTION`, `WORK_PAUSE`, `WORK_RESUME`, `SESSION_INPUT` (the steward's only words are row 11's), conversations begun or carried on (`START_CHAT`, `SESSION_GO_ON_NEW`, …) | The owner's standing choices and their own conversations; a later row may add one with its own reason and read |

A route added to the census later is never a role's act until a decision names it, as D201's grant names its presses.

### 3.3 The route and the verbs

- **One route, `ROLE_ACT { role, act, generation, form, proof }`,** census class `Role`, judged by the judge's role method as built: the
  caller look, the proof against the appointment's holder's hash and the hello's nonce over every field but `proof`, the appointment its
  `role` names and its `generation`, the act in that role's table and the appointment's `acts`, the form exact, then the act's conditions.
  The supervisor's `SUPERVISOR_ANSWER` stays its own route (its decision lifecycle is not an act's); the steward's acts share this one, so
  a new act is a table row, not a new route and census line.
- **Admitted, the act runs inside `PipeDoor.EnterRole`**, whose `RoleJudgement` names the role, the generation, the holder, the act and
  the act's id, which Daoris draws at admission (never the verb). Rows 1 to 10 and 12 run their route's handler on the path §3.1 names,
  and each handler §3.1 names writes the record of its own from `PipeDoor.Role` (§5.1). Its door stays `terminal`, so every handler that
  refuses the terminal refuses it too.
- **The verbs are `daoris-driver steward <act> …`**, each the grammar of the existing verb it mirrors, sent only over the pipe, with **no
  alone path**: with no Daoris running, the role does not exist. Each reads `DAORIS_ROLE_KEY` into memory, removes it from its own
  environment, checks the pipe's server image as `sessions supervise` does (`not-install`), and sends the proof. `steward acts` lists the
  run's acts and `steward desk` the desk (§6.2), both open reads answered by the instance.
- **A role's terminal never acts as the owner nor alone.** Where the entry's marker says the process started in a role's terminal
  (`HolderKey.RoleTerminal`, §2.2: the variable present at the entry, read before `Take` removed it, whether or not its value is a key),
  every verb that would send a `Person` route refuses `role-terminal` before it connects (*this terminal holds a role: act as the role, or press from another terminal*), and every
  verb that would act alone (`InstanceFound.None`, a headless loop's home: `trees clean`, `trees sync`, `trees land --plan`, `git branches`,
  `git review`, `trace`, `drive`, `chat`, `help resume`) refuses `role-terminal` instead, exit 2. So the holder never presses as the owner,
  never runs git or an agent in its own process, and an owner's confirmed card is never a steward's act.

### 3.4 The judgement, in order

1. The census: `ROLE_ACT` is `Role`. A `Person` route sent from a role's terminal never reaches here (§3.3).
2. The caller look (D201 §3.3; the built door's codes `in-agent-job`, `untold`): a caller in an agent job, untold, or with any agent of this
   process uncontained is refused.
3. The proof: no proof, a wrong one, a replay or another connection's nonce is refused `not-holder`, logged, never asked.
4. The appointment: none for `role` is `not-appointed`; dormant is `dormant`; another generation is `appointment-changed`; a build that does
   not offer the role is `not-offered`.
5. The act: not in the role's table is `not-role`; not in the appointment's `acts`, or the set-up without the tick, is `not-granted`; the
   form not exact is `form`.
6. The conditions, each by its code (`unregistered`, `landing-pushes`, `no-tree`, `owner-set`, `not-own`, `not-actor`, `running-here`,
   `running-elsewhere`, `not-quest-session`, `words-waiting`, `not-outage`, `go-on-spent`, `question-open`, `go-ahead-open`,
   `answers-held`, `ask-changed`, `holder-config`, …), judged under the judge's lock where they read its memory, and in the store's own step where they read a record (D201's finish).
7. **Admitted once**, under the judge's lock: the act's id drawn and the act **registered in the run's memory as `admitted`**, with its
   holder, role, generation and target; the lock released.
8. **Under its target's gate** (§5.4), the act takes the judge's lock once more, briefly, and in that one step checks that **its entry is
   still `admitted` (no revoke cancelled it), the holder's hash still present and the appointment's generation still live**
   (`appointment-changed` otherwise), and **the target not `owner-set`**; then moves the entry to `running` and releases the lock. **The
   gates are the act's target's and its dependencies'** (§5.4's target resolution; finding 6), taken in their fixed order before this step,
   and every condition that reads a dependency (`landing-pushes`, `no-tree`) is judged again under them. Only then
   does the handler run; it records the act (§5) and the gate is released. An act whose effect lands later stays registered as a
   **pending operation** until its final consumer takes it (§5.1a).

**The revoke's order against an act.** A revoke (**Turn off**, a holder's end, a re-appointment) is one mutation step under the judge's
lock: it drops the appointment, **and the holder's hash only where that was the holder's last live appointment** (§2.2; finding 10),
and, in the same step, **cancels every entry of that generation still `admitted`, every stage of its pending operations not yet executed
and every binding they hold** (§5.1a); its sweep then clears what they left on disk (§5.4). So an act either moved to `running` before the
revoke's step, and is past its gate, or finds itself cancelled at step 8 and is refused `appointment-changed`, said on the role's line.
The judge's lock is never held across an await: step 8 takes it inside the gate for a check and a state change, never around the handler.

## 4. The steward's go-on

### 4.1 One fixed go-on now; free words later, with their own read

Round 1 gave the steward free words through `SESSION_INPUT`. Both reads showed what that reaches: the running-session path keeps them on
the ask as the person's verbatim words (§0.5); the inbox's `KeepOrHoldAsync` falls back to holding a word in the process alone when the
record refuses it (`DrivenInbox.cs:488`), so a revoke's take-back on the record cannot withdraw it; a word not solicited beside a held
answer lifts the answers-alone hold (`AnswersAlone.cs:34-35`, `AnswersAloneHold.cs:31`, `:57`) and defeats a go-on mark
(`GoOnMarks.Judged`); and text the holder read from an agent's question, a file or a desk request would travel on to another session under
management's name. The act the direction names is the go-on after an outage, which needs no text of the steward's at all.

So this design gives the steward **one go-on, `steward go-on <id>`, whose text is Daoris's own**, and no `SESSION_INPUT`. **Free words stay
out of D209**: if they are wanted, they are their own row (STEWARDROLE1h) with their own design and its own read, as D208 gave SUPERVISOR1f;
nothing in this design is that read.

**Round 3: the go-on is AGENTERRORS1's release, not a word.** Round 2 kept the go-on as a word `From: steward` on a record
`awaiting-person`. Both reads of round 2 showed what that admits and what it leaves: any quiet park met its conditions, a session that ended
its turn by asking the owner in prose among them (the Opus read's finding 4), and its `not-handed` path left the record working with no
run (Codex's finding 7). AGENTERRORS1 settles the first by reading the error: past its bound, a session whose provider kept failing ends
`failed` with `ended.transient`, its quest held (`TransientHolding`, `held.transient`), released by the person's words or by the `retry`
act, which resumes **the same conversation** by D206's step 1 with Daoris's own word `retry.go-on` (*The last request failed on the
provider's side ({{words}}). Go on where you stopped.*; D210's design §8, §11 row 1a). That release is exactly the act the direction names,
and its word is already Daoris's. So the steward's go-on **is that release**, made as the steward's: it keeps no word on any record, so the
round-2 word's lifecycle (the service's conditional keep, `SaidFrom.Steward`, the quarantine, the ticket, the neutrality to the
answers-alone hold and the go-on marks) leaves the design with it.

### 4.2 Its conditions

Judged at admission, again under the quest's gate (§3.4 step 8), and again by the final consumer (§4.3), each by its code:

- **After an error AGENTERRORS1 read, and only then** (`not-outage`): the session is its quest's last record here, `failed` with the note
  code `ended.transient`, and its quest is held by `TransientHolding` for that session. A park, a session ended any other way, a record
  ended by a kind AGENTERRORS1's policy hands elsewhere (`access` is a sign-in, the owner's; `limit` is D206's, which resumes by itself;
  `fatal` fails with its reason) and a door AGENTERRORS1 does not read yet, which never writes `ended.transient`, are all refused. A session
  that asked the owner in prose is a park, so it is refused.
- **The release is the transient hold's**: `StandingChoices.Retry` answers `Released` for that hold and session, never `Marked` (no
  forgiveness of strikes, row 8's rule).
- **Waiting on nobody but a go-on**: no process of it here (`running-here`); no question recorded open on it (`question-open`); no go-ahead
  open on its ask (`go-ahead-open`); **no word waiting on it of any author** (`words-waiting`): where the owner's own words wait, they are
  the release already (AGENTERRORS1's), and the steward's go-on would overtake them.
- **Its quest not the owner's this run** (`owner-set`, §5.4).
- **One go-on per quest per run** (`go-on-spent`). The run's memory keeps, per quest, that the steward released it once. A quest whose
  steward-released run ends `ended.transient` again is refused and waits on the owner: their word or their *Try again*. D210's hold is the
  bound before the owner, as `Exhausted` is for strikes, which row 8 refuses to forgive; each release is another five tries over 31
  minutes and a turn, so a release with no bound would let an outage loop every hour with no strike and no cool-off and nobody but the
  steward. One release covers the outage the owner would have pressed through once; a second outage on the same quest is the owner's.

### 4.3 Released, consumed and recovered: fail-closed

The go-on is a **pending operation** of the run's memory (§5.1a), `{ act id, generation, holder, quest, session, ended, kind: go-on }`,
`ended` being the record's `Updated` the release is keyed to, with one final consumer, the planner's start of that quest:

- **The release is written by the handler under the quest's gate**, keyed as D210 keys the owner's *Try again* of a transient hold,
  `WithTransientReleased(quest, session, ended)`, to the end it releases, never the stop's session-keyed `WithReleased`
  (`StandingChoices.cs:118-125`; the agent-errors design §5.1), but by its own writer, `WithRoleReleased`. **It carries its author on disk, in a shape no older reader takes as the owner's** (Codex's final read,
  finding 7): not in the `transientReleased` map, whose reader on main takes `{ session, ended }` and ignores any other field
  (`DriverConfig.cs:1158-1173`, read by `ReleasesTransient`, `:139-143`), so a steward's entry there would survive a restart into an
  older build as the owner's release; but in a role-only key, **`roleReleases`**, `{ quest: { kind: "transient", session, ended, by:
  "steward", act } }`, which no released map's reader reads (§5.1a). An older build ignores it, and an older rewrite that drops it leaves
  the quest held: both fail closed. It is recorded as the steward's: the planner's reason *Tried again by the steward* and the
  line in *Notifications*. No word is kept on the record, so nothing reaches the ask, the inbox, the answers-alone count or the go-on marks.
- **Consumed only by the start, under the quest's gate.** The planner's next start of a quest whose release is a steward's pending
  operation takes the quest's gate and, in one step under the judge's lock, finds the operation still pending, its generation live and the
  quest not `owner-set`, which read memory alone; then, **in the start's own step, never under the judge's lock** (§3.4 forbids reading the
  service under it), the record's check: D210's end key, the record's session and `Updated` equal to the operation's `ended`, and no
  question, go-ahead or word waiting. **Then the execution cutoff** (Codex's final read, finding 5): the start takes the judge's lock once
  more and, in one step, finds the operation **still pending**, its generation live, its holder's hash present and the quest not
  `owner-set`, and moves it to `executing`; a revoke or an owner's mark that landed while the record check awaited finds it pending and
  cancels it, so this step refuses and nothing starts. Only after the cutoff does it start AGENTERRORS1's resume of the same conversation,
  the agent reading `retry.go-on`; a revoke after the cutoff does not recall it, as one after `running` recalls no act (§3.4). Where any of that fails, **the release is withdrawn** (the transient hold applies again, the steward's
  `roleReleases` entry removed in the same config write), the operation is ended, and the reason is said on the record and the role's
  line. A release whose author is not `steward` is the owner's, as today, and nothing here touches it.
- **No briefed fallback and no walk.** D210 lets a refused resume of the person's release go briefed (D206's step-1 fallback) and lets
  D205's walk run inside it, because that release is *the person's authorized carry-on*. The steward's is not the person's, so a resume of
  the steward's release that meets the account, the conversation or the choice gone, or a native door whose release would go briefed, is
  **not started**: the release is withdrawn, the record restored and held as below, and the quest waits on the owner.
- **A revoke or the owner's act cancels it** (§3.4, §5.4) before the start: the release is withdrawn by the revoke's sweep or inside the
  owner's gate, so a quest the owner touched is never started by the steward's release.
- **After the start is admitted, a failure resolves the record and keeps the queue.** AGENTERRORS1's resume moves the record to working
  before the agent runs. A failure after that and before the agent's first turn (the record's reread, the preparation, the spawn) is
  resolved by **restoring the record to what the release found**: `failed` with `ended.transient`, its quest held again by
  `TransientHolding` for that session, **every word on it kept as it was**, and any word that arrived after the release, the owner's above
  all, kept waiting on it, where it is itself AGENTERRORS1's release by the owner's words. The steward's operation added no word, so there
  is none to take back; the restore is the service's restore extended to a `failed` record (`SessionLedger.cs:1080-1100` restores only a
  working record to `awaiting-person` and takes back only supervisor words today), with no word taken. Where the restore itself fails, the
  record is left working with no run for the orphan sweep, as SUPERVISOR1a-i's `NotHandedAsync` leaves it (`DriverContinuations.cs:795-819`),
  and the quest is said held.
- **After a restart nothing is pending**: a release found written **`by: steward`** whose operation no memory holds is withdrawn at the
  first look and said (*the steward's go-on was not carried out before Daoris restarted*), since its author's appointment ended with the
  run. **Only those**: the owner's own *Try again*, the stop's release today and D210's transient release by design (agent-errors design
  §5.1: *across a driver restart (the release is on disk)*), carries no `by`, lives in its own map, and stands. Since the steward's is in
  `roleReleases`, a build that never runs this sweep cannot read it as the owner's either.

### 4.3a D210's amendment, a note its next round takes

AGENTERRORS1 (D210, proposed) is being revised beside this design, so D209 does not edit it: this is the note D210's next round takes,
and D209's acceptance waits on D210 carrying it or answering it. *Amended 2026-10-24* (Codex's final read, its row 1f): D209 was accepted
and D210 too, and D210 point 3 still reads *Only the person releases it* (`D210.md:65`), so **row 1f waits on D210 carrying this note or
answering it**; no other row does. The release the note names is now written in `roleReleases` (§4.3), not by `WithTransientReleased`.

> **D210 point 3, *The release*, and its *Why***, amended (STEWARDROLE1, D209): *Only the person releases it* reads *Only the person
> releases it, and the steward once* (D209 §4): a steward's *Try again* of a transient hold is written by the same
> `WithTransientReleased(quest, session, ended)` with `by: steward` and its act's id, **at most once per quest per Daoris run**, so a
> quest whose steward-released run ends `ended.transient` again waits on the person. **The steward's release takes no briefed fallback and
> no walk**: a resume that meets the account, the conversation or the choice gone, or a native release that would go briefed, is not
> started; the release is withdrawn and the record held again. A restart withdraws a release `by: steward` and keeps the person's.
> **Why**: the hold is the bound before the person, as `Exhausted` is for strikes, and D210 justifies the briefed fallback and the walk
> by the release being *the person's authorized carry-on*. A release by another with no bound would let an outage cycle without the
> person, which D210's *nothing loops without the person* forbids; one release per quest per run, with neither fallback, keeps that
> sentence true with the steward in it.

### 4.4 What the session is handed

AGENTERRORS1's `retry.go-on`, Daoris's word, the same one the owner's *Try again* sends: *The last request failed on the provider's side
({{words}}). Go on where you stopped.* It grants nothing and names no author, which is right: it carries no decision of the steward's, only
the resumption after an outage the owner would have pressed. The record and the page say who released it: the planner's reason and the
role's line say *the steward asked it to go on* (管家请它继续); the record's own note stays AGENTERRORS1's. The working note and the planner's
go-on sentence never say *you* for it, and *What needs you* drops the quest because the hold is released, as with the owner's press.

### 4.5 The set-up's ask

A set-up made by the steward publishes its ask with `by: steward` (an additive field of the ask's record), so the ask is never the
person's: `AskWords.Beneath` heads its first word *The steward's words, published on the owner's behalf* and keeps the owner's own words
after it under today's heading. Until that field exists, §3.1 row 12 is not offered (§10).

## 5. The record, the notice and the overrule

### 5.1 On what it acted on: records of its own

The steward's acts write **their own records**, never the person's with a suffix. Each new code is a behavioural sibling of the person's,
and every reader of the person's code is updated by code to read the steward's (a sweep test finds them by search, as D208's does):

| Act | The person's record today | The steward's | Readers updated by code |
|---|---|---|---|
| Stop, live | `ended.stopped`, *the person stopped it* (`Observation.Stopped`) | `ended.stopped-steward`, *the steward stopped it* | The planner's hold (`Planner.cs:259`'s `PersonStopped` becomes a stop held for *Try again*, its reason *the steward stopped session `s1`; the steward's or your Try again carries it on*); the page's `note.json` en and zh; the D79 resume's brief; *What needs you*, which names it *Stopped by the steward* |
| Stop, parked | `stopped.checkpoint-stopped`, *The person stopped this at a checkpoint* | `stopped.checkpoint-stopped-steward`, *The steward stopped this at a checkpoint* | As above |
| Finish | `stopped.checkpoint-finished`, *The person finished this at a checkpoint* | `stopped.checkpoint-finished-steward`, *The steward finished this at a checkpoint* | TERMTRUST1b's done condition (*its last session here finished by its note code*), which reads both; `note.json`; the resumed brief |
| Try again | the planner's release | the same release, its line *Tried again by the steward* | The planner's reason; *Notifications* |
| Hand-over | `HandOverBy.Person`, why `press`, *the person asked for it* (`HandOverRecords.cs:46`, `:60`); asker `terminal` or `window` | `HandOverBy.Steward`, why `steward`, *the steward asked for it*; asker `steward` | `HandOverRecords.Briefed` and `Noted`; `HandOverAsks`' asker note; `note.handed.why.*` en and zh; the handed brief |
| A hand-over ask answered | the person's yes or no | the steward's, its note *answered by the steward* | `HandOverAskNotes`; the page |
| A setting (drive, trees, hold) | no author: Setup → Driving's row shows none today (no *Set by you* in `src/Daoris.Web/src`) | *Set by the steward at 14:02*, from this run's memory | Setup → Driving's row |
| The go-on | the person's *Try again* of a transient hold (AGENTERRORS1a) | the same release, its reason *The steward asked it to go on*, from this run's memory (§4.3) | The planner's reason; *Notifications* |
| The set-up's ask | the person's | `by: steward` (§4.5) | `AskWords.Beneath` |

D201's `standing.terminal` part is not written for the steward's acts. The behaviour a code carries (a stop held for *Try again*, a finish
a done may follow) is the same for the steward's sibling, so no behaviour changes but who is named.

**A mark is a claim; this run's memory is the authority.** A note code, a box's `askedBy` and a handed file's are within an agent's ordinary
reach. So the page draws *By the steward, on your behalf* live, with its reverse press, only where this run's memory recorded the act's id;
a record carrying the steward's code that the memory does not hold is drawn as history (*recorded as the steward's*), with no press and no
standing.

### 5.1a Pending operations: an act whose effect lands later

An act's handler is not always where its effect lands (§0.5a). So **every role act whose effect a later step carries out is a pending
operation in the run's memory**, registered at admission (§3.4 step 7) and held until its **final consumer** takes it: `{ act id, role,
generation, holder, the actor it is recorded as (steward), the target quest or repository, and the complete operation }`. The complete
operation is everything the consumer will act on, so the consumer acts on memory and **never on what a file says**:

| Act | Carrier on disk today | Final consumer | The operation memory holds |
|---|---|---|---|
| Live stop (row 6) | none: the steward's stop never writes a `SessionRequest` (`SessionsCommand.cs:949`); the instance calls the driver's stop with the steward's note at once | the handler itself; the process's end writes the note handed in at the call | the note code, the session |
| Hand-over (row 9) | the box (`HandOverQuest.cs:101-109`), then the handed file | the look that consumes the box (`DriverHandOvers.cs:84-133`), **then the start that reads the handed agent** (`Driver.cs:1896`, `:1087`) | session, quest, agent, `account: null`, note none, `HandOverBy.Steward`, why `steward` |
| Yes to a hand-over ask (row 10) | the ask's state file, `approved` (`HandOverAskWord.Approve`) | **for a session still running**, the turn's end, `AtTurnEndAsync` (`DriverHandOvers.cs:279-294`); **for a session parked on the owner** (its turn's end parks it, *and their yes hands it over from there*, `:300`), the look's `SettleAsync` → `GoAsync` (`:161`, `:221-243`), which keeps a box; **then the look that consumes that box** (`:84-133`), **then the start** | session, quest, ask id, the agent the holder saw, `account: null`, `HandOverBy.Steward`, why `steward` |
| Go-on (row 11) | the config's `roleReleases` entry, `kind: transient`, keyed as `WithTransientReleased(quest, session, ended)` is, `by: steward` and the act's id (§4.3) | the planner's start of the quest (§4.3) | session, quest, the end it releases (`ended`) |
| Try again (row 8) | the config's `roleReleases` entry, `kind: stop`, keyed as `WithReleased(quest, session)` is, `by: steward` and the act's id | the planner's start of the quest | session, quest, the stop it releases |

**An operation moves through stages, each consumer taking exactly one** (Codex's final read, finding 4: with one *pending, not consumed*
state, the first of several successive consumers would make the next fail). Each operation names its stages in order, and each stage
names its one consumer:

| Operation | Its stages, in order, each the consumer that takes it |
|---|---|
| Hand-over (row 9) | `boxed` (the look consuming the box) → `handed` (the first start reading the handed file) → **bound** |
| Yes, session running (row 10) | `approved` (the turn's end, `AtTurnEndAsync`) → `handed` (the first start) → **bound** |
| Yes, session parked (row 10) | `approved` (the look's `GoAsync`) → `boxed` (the look consuming that box) → `handed` (the first start) → **bound** |
| Try again (row 8), go-on (row 11) | `released` (the planner's start) → `executing` |

**Each consumer, under the quest's gate, takes its own stage in one step under the judge's lock**: it finds the operation **at its
stage** (not cancelled, not at an earlier or later one), its generation live, its holder's hash present and the quest not `owner-set`,
moves it to the next stage, and acts with the operation's own fields and actor. A consumer whose checks await (a record's read, a file's)
does them **between two such steps**: the first finds the stage, the second, after the awaits, finds it **still** at that stage with
everything live and makes the transition, **the execution cutoff** (finding 5); a revoke or an owner's mark while the checks awaited
cancelled it, so the second step refuses. Otherwise it acts on nothing of the steward's: it clears what the operation left on disk, logs
`role.cancelled` (or `role.unattributed` for a claim no operation backs) and says it on the session's record.

**A hand-over's last stage is a binding, not a consumption.** Once its first start has run, the operation becomes the **quest's live
hand-over binding**, `{ quest, agent, account: null, act id, generation, holder }`, which the quest's later starts **this run** read
as the steward's agent choice. A binding lives only as long as its appointment's generation and the quest's not being `owner-set`: a
revoke, the holder's end or the owner's mark ends it in their own step, the handed file is cleared by the sweep, and every later start
runs on the owner's actor order (`ActorChoices.Effective`). After a restart the memory is gone, and no binding outlives it. In
particular:

- **Memory, not the carrier, chooses the authority path** (Codex's final read, finding 3). The look and the start read the run's memory
  **first**, by the target (the quest or the session) and the stage they consume, **whatever the carrier claims**: `HandOverFiles`'
  readers take any known `askedBy` and `why` (`HandOverFiles.cs:88-99`), the look writes the box's on (`DriverHandOvers.cs:91-92`,
  `:133`) and the start reads the handed file again by itself (`Driver.cs:1907`), so a file whose actor was changed to `person` with an
  ordinary reason would otherwise take the disk-authoritative path. Where memory holds an operation or a binding for that target at that
  stage, the carrier must match it in every field, its actor, its operation's id, the agent, the account (none) and the note (none):
  a missing or changed one invalidates the carrier, which is cleared, said `role.unattributed`, and acted on as nothing (the look hands
  nothing; the start runs on the owner's actor order), and the operation is ended. Where memory holds none, a carrier claiming the
  steward is cleared and said, as before, and an ordinary one is the owner's or the agent's as today. The same holds for the ask's state
  file: an `approved` whose author or id was changed to the ordinary form, while memory holds a steward's yes for that ask, invalidates
  the yes.
- **The yes to an agent's ask is the steward's to its end.** The steward's yes writes `approved` with `by: steward` and the operation's id;
  `AtTurnEndAsync` takes the quest's gate, takes the operation's `approved` stage, compares the ask's current agent with the agent the holder saw
  (`ask-changed` otherwise, the ask left waiting on the owner), and writes the handed file **as `HandOverBy.Steward`** from the operation,
  never `HandOverBy.Agent`. **A parked session's yes is taken by the look's `GoAsync`, inside the same operation**: by the time the holder
  sees an ask waiting on the owner its session has usually parked, and today `GoAsync` keeps a box `askedBy: agent`, `why: agent`
  (`DriverHandOvers.cs:242-243`), whose consumer stops the parked record (`:108`) and writes the handed file as the agent's (`:133`). For
  a steward's yes, `GoAsync` takes the quest's gate, takes the steward's `approved` from memory in one step under the judge's lock (the
  operation pending, its generation live, the quest not `owner-set`), compares the ask's current agent with the signed agent
  (`ask-changed` otherwise, the ask left waiting on the owner), and writes its box **`askedBy: steward`, `why: steward`, with the
  operation's id**, consumed by the look as row 9's box is. That consumer ends a parked record only for an agent's ask today
(`agentAsk && awaiting-person`, `:106-108`), so it reads the steward's yes to an agent's ask as one too, ending the park with the steward's
reason, never the agent's. A state file claiming the steward's `approved` with no pending operation is
  cleared to waiting and said, on either path.
- **A stop's note is fixed at the call.** The live stop runs in the instance under `PipeDoor.Role`, so nothing on disk carries its
  attribution, and `SessionRequestWatch` never builds a steward's note.

**The revoke and the owner's act cancel every stage not yet executed and every binding of their scope** in the same step that makes them
(§3.4, §5.4); the revoke's sweep then clears what they left on disk: the steward's untaken boxes and handed files, a steward's `approved`
state back to waiting, a steward's release withdrawn. **Every release the steward writes is in `roleReleases`, with `by: steward` and its
act's id** (rows 8 and 11; finding 7), never in the `released` or `transientReleased` maps, whose writers take no author
(`StandingChoices.cs:118-125`) and whose readers on main ignore any field they do not know (`DriverConfig.cs:1158-1173`): an older build
reading the config after a restart finds no release of the steward's to take as the owner's, and one that rewrites the config without
the key leaves the quest held. The planner reads a `roleReleases` entry as a release only where this run's memory holds its operation at
`released`. A restart forgets every pending operation and binding, and the first look treats what they left as above, withdrawing every
`roleReleases` entry and keeping the owner's own maps whole.

### 5.2 In the machine log

`role.act` for each admitted act (`role`, `act`, `generation`, `target`: a session id, a quest or a repository's name, `outcome`),
`role.refused` for each refusal (`role`, `act`, `why`), `role.cancelled` for an admitted act or pending operation a revoke or the owner's
act cancelled, `role.unattributed` for a claim memory did not admit, `role.appointment` for an
appointment, a revoke, a holder's end and what a start found: codes only, beside D201's `person.*` family, which keeps its own.

### 5.3 What the owner sees

- **Settings → This Daoris → *Roles*** (D201's *A terminal* row, renamed): *Steward*, its state, *Appoint…* or *Appoint again*, and **Turn
  off**. *Appoint…* opens the inline ask: what it does (§3.1 in one line each, the offered acts only), what it never does (§3.2's six in a
  line each, closing a quest as done named), that it drives or gives trees only to repositories whose landing does not push, and never
  stops or finishes a session where that would publish, that a repository the owner left undriven in an earlier run is not theirs this
  run unless they act on it, that *every repository registered on this machine* includes one any process of the account registers (the
  registry's door is not the owner's alone until PERSONDOOR1g), that it drives only a repository whose sessions open trees of their own
  (`no-tree`), so Daoris's sessions never work in the owner's checkout by its act, that the holder's harness is Daoris's own pinned
  `claude-code`, started by Daoris itself with an environment it composes and on Daoris's own configuration, and what that does not close:
  **the programs the key is handed to are files of the account's, so a process of the account that rewrites the pinned harness or reaches
  into the holder can act as the steward** (§6.3), that the owner signs in once in the holder's tab (its configuration starts signed out),
  that a quest gets one steward go-on per run, the tick *and set repositories up: adds nine exact
  doctrine commands to a repository's allowed rules* (always shown off), that its acts are recorded as the steward's and said in
  *Notifications*, that it lasts until Daoris restarts, who holds it and, for a later *Appoint…*, §2.2's choice and what sharing means;
  D208 §4.6's plain sentences of its limits; *Appoint* and *Cancel*. The supervisor's rows stay on Setup → Driving as built.
- ***Notifications***: one group line per role, as D201's presses' line (`notifications/StandingLine.tsx`): *Steward appointed · 14 acts
  since Daoris started · Turn off*, opening to the run's acts, newest first, each with its target, its time and, where one exists, the
  owner's reverse press (§5.4). **No notice per act**: D201 rejected one per use as noise, and the steward's acts are the many small ones
  this design takes off the owner.
- **The bell** counts no act. A steward's stop that the steward has not tried again shows in *What needs you* as *Stopped by the steward*,
  since its quest waits on a *Try again* (the steward's or the owner's); it is the one act that can leave something waiting on the owner,
  and the steward's own *Try again* (row 8) is how the steward clears it.

### 5.4 The overrule

- **Revoked at once**: **Turn off** on the row or the line, no ask, D208's two steps: the mutation, which cancels every admitted act not
  yet running and every pending operation of the generation (§3.4), then the sweep, which clears what the pending operations left on disk
  (§5.1a). An act already past its gate (moved to `running`) is not recalled, as neither D201's nor D208's revoke recalls one; its
  deferred effect is, since its consumer finds it cancelled.
- **Reversed by the owner's own press**, which the line offers beside each act that has one, each an act that really exists today: *Stop
  driving* for a drive, *Drive* for an undrive, *Trees off* for trees on, *Resume* for a hold, *Hold* for a resume, *Try again* for a stop,
  *Stop* for a steward's *Try again* or go-on, *Hand back…* (the owner's own hand-over) for a hand-over. A finish has none: its session was
  waiting with nothing said and nothing running, and the finish neither accepts nor closes its quest: an open or taken quest stays so for
  the owner's done or acceptance, and an already-done one stays done (finding 14). Each is the owner's ordinary
  press, recorded as theirs.
- **Anything the owner does to a thing makes it theirs for the run, through whichever door.** `owner-set` is keyed by **the thing acted
  on**: the repository for rows 1 to 5 and 12, the **quest** for rows 6 to 11, a session resolved to its quest. It is **not a list of
  routes**: it is marked where every owner door is dispatched, from what that door's payload or plan names:
  - **Every `Person`-class route and every `Window`-class route** the census holds, dispatched from the window or a confirmed card, passes
    through one step before its handler (the one dispatcher, `InstancePipe.cs:778-780`, and the bridge's): **`OwnerMarks.Of(route,
    payload)`** answers the repositories, quests and sessions it names, from a census table, **`PipeCensus.Targets`**, which declares for
    every such route the payload fields that name a repository, a quest or a session, or `none` with its reason from a closed list
    (*names no repository, quest or session*; *the owner's own conversation*; *a machine setting*; **acts on nothing a role acts on**:
    a folder opened, a review shown again, a notice read or dismissed, a channel marked, `SESSION_OPEN_FOLDER`, `SHOW_REVIEW_AGAIN`,
    `WORK_RECORDS_FOLDER`, `PipeCensus.cs:270`, `NOTICES_READ`, `NOTICE_DISMISS`, `HELP_DISMISS`, `CHANNEL_MARK`, `:222-224`). **`SET_APPOINTMENT`
    is `none` by that reason**: it names the supervisor's repository (`:284`), but appointing a role acts on the role, not on the
    repository, so appointing the supervisor on X never freezes the steward on X. The step takes each named target's gate, marks it, and
    runs the handler inside.
  - **`HELP_APPLY`** marks by its applied plan, not its route: each proposal door of `HelpSettingProposals` and `HelpSetupProposals`
    declares its target (the repository for `drive`, `undrive`, `hold`, `resume`, `trees`, `line`, `landing` and the set-up; the quest for
    `retry`), and the apply takes that target's gate and marks it before the config edit. A workspace-scoped plan marks every repository of
    the workspace.
  - **D201's `Forget`** (the standing hold's memory, `StandingConfirmation.cs:721-727`) moves into the mark: any hold change the role's
    door did not make, through `SET_HOLD`, `HELP_APPLY` or any other config write, forgets the steward's own hold of that repository, so
    the steward's *resume* never releases a hold the owner set (`not-own`).
  - **The service's own person doors and the request folder**, which reach records without the instance (`PersonDoors.cs`'s `Person`
    doors; `quest done` until TERMTRUST1b; a terminal's `SessionRequest`), mark by **the record**: the `owner-set` condition also reads the
    target's records and refuses where, after this run's start, a person door wrote on that quest **one of these kinds, exactly**: a
    session's move (a stop, a finish, a resolve), a word to one of its sessions, a hand-over, or a status changed **after its
    publication**. A quest's publication and its ask's own words (`AskWordKind.Added`, `Asks.cs:200`) never mark, so a quest the owner set
    going this run is not the owner's from birth. Each such door declares, in the same census sweep, which of those kinds its effect is
    attributed to the person by on the record the condition reads. **What orders them is the record, not a gate**: the service's doors take
    no gate of the instance's, so the steward's handler and each final consumer read those marks in the store's own step that writes their
    effect where the store has one (a session's move, D201's finish), and where it has none an owner's service door that lands between the
    check and the effect is not ordered (§6.3's *Not closed*, until PERSONDOOR1g moves those doors behind the instance).

  Then every steward act on that repository or quest is refused `owner-set` until Daoris restarts, and every pending operation on it is
  cancelled in the mark's step (§5.1a). A quest's successor sessions (a retry, a hand-over, a carry-on, a new part) share its quest, so the
  mark follows them. So the steward can neither drive again after the owner applied *Stop driving* from Ask Daoris, nor stop again after
  the owner's go-on in a new session, nor go on after the owner edited or took back their own queued words (`SESSION_QUEUE_EDIT`), nor
  try again after the owner discarded the tree. **A census sweep proves it** (§10 row 1a-ii): it fails on any `Person` or `Window` route,
  `HELP_APPLY` door or service person door that names a repository, a quest or a session and declares no mark; and, since a declaration
  can be wrong, it **cross-checks `PipeCensus.Targets` against each handler**. **It tells naming a target from mutating one** (Codex's
  final read, finding 9: `SESSION_OPEN_FOLDER` is `none` and its handler requires `id`, `SessionsRoutes.cs:1016`, so a sweep that failed
  every `none` with a required `id` could not pass): a route whose handler **mutates** a role-relevant target, a session it publishes
  (`[Publishes(…, Session = …)]`, `SessionsRoutes.cs:22`, `ConversationRoutes.cs:356`) or a state scope a role's acts read (the driver's,
  the sessions', the landing's, the lines'), fails the sweep if its entry is `none` or names another field; a route that **only names**
  one (a required `repository`, `quest`, `session` or `id` it reads and publishes nothing of) may be `none` only by the reason *acts on
  nothing a role acts on*, and only where it is on the sweep's closed list of **reviewed exemptions**, each with its reason, in the test's
  own source. Each exemption is cross-checked against its handler's effects (it publishes none of those scopes and no session), and a
  test holds that a mutating route declaring `none`, or adding itself to the list, fails.
- **The target resolution, with the act's dependencies** (Codex's final read, finding 6). A target is resolved to **its scopes**:
  - **A workspace-scoped owner change** marks every repository the registry places in that workspace, at every door that takes one, not
    only `HELP_APPLY`: `SET_LANDING` accepts a `workspace` with no `repository` (`LinesRoutes.cs:188-201`), and `LandingRules.Effective`
    reads the workspace's rule where the repository has none, so `PipeCensus.Targets` declares a `workspace` field as a target, resolved
    to the workspace and its repositories.
  - **An act depends on what its conditions read**: rows 6 and 7 depend on their quest's repository's effective landing rule and so on its
    workspace (`landing-pushes`, `AutoLanding.cs:203-207`: a `Done` park is held from a due landing only by being parked); rows 1 to 3 on
    the repository's landing and trees choice and its workspace; rows 8 to 11 on the repository the start runs in (§3.1a). A steward act,
    and each consumer of its operations, takes **the gates of its target and of its dependencies**, in a **fixed order** (workspace, then
    repository, then quest, each by its name, ordinally), and judges every condition that reads a dependency again under them. The
    owner's change takes the gates of every scope it resolves to in the same order. So an owner's pushing landing rule set after the
    steward's last check and before its stop ends a parked `Done` session is either set first, and the stop refused `landing-pushes`
    under the gates, or set after the stop has run, the owner's own act over it. `owner-set` stays keyed by the act's own target: a
    repository's landing change marks the repository, not its quests; the gates order the quest's acts against it.
- **Ordered against the owner's act, by the target's gate.** The run's memory holds a gate per workspace, per repository and per quest. A steward act
  takes its target's gate, and its dependencies' in the fixed order above, after admission and checks under it (§3.4 step 8), runs its handler and releases it; an owner's act takes the
  same gate in the marking step before its handler and sets the mark inside it; a pending operation's final consumer takes it too
  (§5.1a). So if the owner acts while a steward's act or operation waits, either the steward's runs first and the owner's then runs over
  it, or the owner's mark is set first and the steward's is refused or cancelled: never a steward's effect after the owner's. The gate is
  the run's memory's own (a semaphore per target, never the judge's lock), held only across the handler or the consumer; a press with no
  target takes none.

### 5.5 Why no overrule window

D208 holds the supervisor's decision five minutes before it is a word, because an answer to a question is the one act a session follows
at once and cannot be taken back. The steward's acts differ: each of rows 1 to 10 only narrows, is the development's own run the owner set
up, or is undone by one press of the owner's that exists today (§5.4), and none of them may release a push (`landing-pushes`); the go-on
is a release the start consumes, cancelled until then by the owner's act. A hold on every act would make the go-on after an outage wait five minutes for nothing, and the owner's frame is the role acting by
its own decision. The set-up, the one widening, is the owner's own tick, and closing a quest is not the steward's.

## 6. The holder, and the steward's working session

### 6.1 The holder: the steward's management desk

The holder is the dedicated session of SUPERVISOR1a-ii, in its tab of Daoris's terminal panel, unchanged but for what it may run:

- started in its tab's folder (§2.2), outside every repository, by the start line of §2.2: no project or local settings, an empty MCP
  configuration;
- **as a program Daoris chose, not one any process of the account chooses** (round 3's read, finding 1). The key is handed to every
  program the tab starts, so which programs those are is the boundary. As built they are the tools file's or PATH's shell, PATH's
  `claude` and an environment Daoris inherited (§0.2). **1a-i's build choice is the read's option (a): Daoris starts the harness itself
  as the tab's process.** The image is Daoris's managed, pinned `claude-code` by its absolute path (`HarnessSettings.ManagedBinary`'s
  maker layout, `<toolchain>/claude-code/<pin>/bin/claude.exe`, as the ACP door already takes it, `HarnessRoster.cs:1234-1236`), its hash
  checked immediately before the start against the one recorded when its verified install moved it there (1a-i records it where the
  install does not yet); the argument vector is §2.2's, element by element, with no shell between to parse it; and the environment is one
  Daoris **composes**, not its own inherited: the system's base variables a process needs, a `PATH` of the install's own `app` folder and
  the system's folders, `CLAUDE_CONFIG_DIR`, the harness's updater turned off (`DISABLE_AUTOUPDATER=1`), and the key; no loader or
  runtime variable Daoris's own environment carries (`NODE_OPTIONS` and its kind) passes. No managed pin, or a hash that differs, refuses
  the appointment `holder-harness`, its ask saying how to pin `claude-code`; it never falls back to PATH's `claude`, as
  `ManagedBinary`'s other callers do and say. The tab ends when the harness exits (§2.2's `HolderEnded`). **Wherever a role's tab is still
  a shell** (the built door until 1a-i lands), **the shell is taken from the system's own location**,
  `%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe`, never the tools file's `pwsh` nor PATH's (`HolderTerminal.ShellOf`,
  `Terminals.cs:44-47`, `:90-96`);
- **on a harness configuration Daoris writes, not the user's.** The environment Daoris composes, the one that carries the key, also
  carries `CLAUDE_CONFIG_DIR` naming the holder's own configuration directory, `work/rooms/roles/config/`, outside every repository.
  **Daoris owns exactly these, and checks exactly these** (round 3's read, finding 5): its `settings.json`, byte for byte
  (`{"disableAllHooks":true}`: no hook, no `enabledPlugins`, no `statusLine`, no helper command), and the absence of every other file
  that would load something: no instruction file (`CLAUDE.md`), no other settings file (`settings.local.json`), and no `plugins`,
  `commands`, `agents`, `skills` or `hooks` folder. **Everything else in the directory is the harness's own state and is left alone**:
  the harness creates and populates `CLAUDE_CONFIG_DIR` itself (`Harnesses.cs:171-174`), its credential among it, so a hash of the whole
  directory would refuse the first act and a rewrite at each start would drop its sign-in. At each holder's start Daoris rewrites its own
  `settings.json` and removes any of the absent files it finds. A fresh directory reports logged out (`Adapters.cs:2349-2350`), so **the
  owner signs in once in the tab**, the harness's own sign-in, which is the owner's act (D37) and is said in the ask (§5.3). The argument
  vector's inline `--settings '{"disableAllHooks":true}'` turns hooks off again **above every settings source but managed settings**,
  which stand above it, so a hook written into that directory after the start is not run either. The user's own `~/.claude` is never read
  by the holder's harness;
- **established by what it actually started with, at each act.** The judge does not trust the printed line. At each admission of
  **every route of the census's `Role` class** (`ROLE_ACT`, `SUPERVISOR_ANSWER`, the since-built `SUPERVISOR_DEFER`, `PipeCensus.cs:298`,
  and any later one; Codex's final read, finding 13), through one guard the judge's role method runs for the class rather than a list of
  routes, so a `Role` route added later cannot miss it, the caller look (D201's `StandingCaller`, which already resolves the pipe's client process) walks from the
  verb's process up its parents, through the shell the harness's Bash tool ran it in, to **the holder's harness process, by the process id
  and start time the terminal module recorded when it started it**, and reads that process's **image path, argument vector and
  `CLAUDE_CONFIG_DIR` from the operating system**. The image must be the managed binary's path, the argument vector after the image must
  equal `HolderTerminal.SessionArguments` element by element, the variable must name the holder's directory, and the directory's owned
  files must stand as above; otherwise the act is refused `holder-config`, logged, said on the role's line, and the holder is ended
  (§2.2), since a holder that started on another configuration may have run what it loaded. A verb run from anywhere else (another tab,
  another process the holder started outside its Bash tool) is refused by the same walk;
- its permissions **only read tools and the install's own `daoris-driver` in its roles' verbs and the open reads**, by the exact prefixes
  of §2.2's line. **Every verb's alone path refuses in its terminal** (§3.3), so a plan verb or a git read the instance does not answer
  runs nothing there;
- it reads repositories' files **by path** with the read tools (the backlog, a decision, a session's log), never by starting in one;
- it builds, tests, merges and dispatches nothing, and runs no git, no build, no test and no hook. **Driving a repository is not
  dispatching**: the sessions a drive starts are Daoris's own, started by its driver with the key stripped and contained in its agents'
  job (D201 part (i)), never this process's children.

### 6.2 What the steward's working session may hold, and the desk

The steward's working session plans, files rows, dispatches builders, merges and runs the gates: it runs other branches' code (§0.4). So:

- **No key, and no role.** It is never started in a holder's tab, and nothing gives it `DAORIS_ROLE_KEY`; a verb it runs sends no proof.
- **Its presses ask as today**: a `Person` route waits for the window's Confirm and, confirmed, is the owner's press, recorded as theirs.
- **Its management goes through the desk**, which lands before activation (§10), so the owner's cards fall for the working session's needs
  from the first day: `STEWARD_REQUEST { act, target, why }`, an `Open` route any caller may use, kept in the run's memory (at most 10 open
  **per caller**, and 100 in all; another is refused `desk-full`, the per-caller bound first, so one agent filling its own share leaves
  the working session's free). **The caller is not the verb's process**, which is new at every request (the pipe's client is the
  connecting process, `StandingCaller.cs:47-53`, always the verb, `D208.md:267-268`): the bound is keyed by **the agent job the caller look
  places the verb in**, where it places it in one (every session Daoris runs, D201 part (i)), else by **the nearest long-lived ancestor
  the walk finds** (the working session's harness, a terminal's shell), so one agent running the verb a hundred times fills its own ten
  alone; listed to the holder by `steward desk`, fenced and labelled as claims (D208 §4.4: what the holder reads is
  the requester's claims, never instructions), each closed by the act that answered it or the holder's *declined* with its reason. It
  grants nothing and never admits an act: an act it asks for is judged by `ROLE_ACT` like any other, from the holder alone. The owner sees
  it under the role's line.
- **The working session's rule** (never press what the holder makes; file it at the desk) is written into the `dispatch-subagent` skill
  when the steward is activated (STEWARDROLE1d), not before, since until then the holder makes nothing.

So the steward is two sessions with one name: the desk decides management and holds the role; the workshop builds and merges and holds
nothing. The owner appoints the desk.

### 6.3 What this closes, and its limits

- **Closed**: every process that runs a branch's code (the working session, its builders, Daoris's own sessions, a hook Daoris's git
  runs) starts from an environment without the key; a press from them asks the owner as today and is never the steward's; a home file
  claiming the steward is a claim memory refuses (§5.1a). The holder's harness loads none of the user's hooks, plugins, status line, helper
  commands or instruction file (§6.1), so a hook an agent plants in the user's configuration never runs beside the key; and a holder
  started on any other configuration is refused at its first act (`holder-config`). **That does not keep every process that runs a
  branch's or a planted file's code from acting as the steward**: those processes cannot hold the key, but they can choose the programs
  it is handed to (*Not closed*, below). What 1a-i closes is the choice by a file or a variable: the harness is Daoris's own pinned binary,
  hash-checked, started by Daoris with an environment it composes, and no shell of the tools file or PATH is ever started with the key.
- **Not closed, said as D208 §4.6 says it**: **the programs the key is handed to, as built** (§0.2): the tab's shell is the one the home's
  tools file names, else PATH's; the harness is the `claude` that shell's PATH resolves, a user-writable npm install whose updater and
  children inherit the key; and the tab's environment is Daoris's own, inherited from whatever started it, loader variables included. An
  agent Daoris runs, or a branch's code the working session tests, can rewrite any of these once, and the next *Appoint* hands the key to
  that code, whose stand-in can pass a walk that reads only a command line. Until 1a-i lands none of this is closed, which is one reason
  no role is offered before it. **After 1a-i**, what stays open is the same-account reach below: the managed binary's folder is the
  account's, so a write between the hash check and the start, and what the harness's own Bash tool resolves for a verb (the shell it runs
  it in, found by the harness from the composed environment, which 1d's look records), are that reach. Also a process of the same account
  that reads another process's memory or environment, or injects into the holder or the instance (D156 point 7, PERSONDOOR2), **writing
  the holder's configuration directory among them**: Daoris rewrites its own files at each start and checks them at each act, but a write
  between a check and the harness's own read is that same-account reach; a holder
  session talked into an act by what it reads (its acts are bounded by §3, recorded and overruled); a holder that runs something it should
  not. **An open limit, checked at activation**: that the pinned harness honours `CLAUDE_CONFIG_DIR` and `disableAllHooks` as this
  design reads them, and reads no instruction file or plugin from the user's own configuration **nor an instruction file in a folder
  above the holder's** (it starts under the home's `work/rooms/roles/`, which agents write; neither `--setting-sources user` nor an
  emptied folder says anything of a parent's `CLAUDE.md`, and this design does not know whether the harness reads one: if 1d's canary
  shows it does, activation waits on a start folder with no folder above it an agent writes). Daoris cannot
  prove a harness's loading from outside it; STEWARDROLE1d's look proves it with canaries and records the harness's version, and a later
  harness is a new look (§10). **A registration is not the owner's alone**: `POST /api/registry` is a `Driver` door until PERSONDOOR1g
  (`PersonDoors.cs:215`), so *every repository registered on this machine* includes one any process of the account registered; the ask
  says so, and the owner's *Stop driving* or *Hold* makes it theirs. **The desk can be crowded**: its per-caller bound, keyed by the
  caller's agent job or long-lived ancestor (§6.2), keeps one agent from filling it, not many processes of the account started apart.
  **The service's own person doors are ordered only by the record** (§5.4): an owner's act through one that lands between a steward's
  check and its effect, where the store has no step that holds both, is not ordered against it until PERSONDOOR1g. **A shared holder widens what the supervisor's inputs
  reach**: the asking agent's question, its `does`, its commits and its last message are read by a session that can also drive, stop and
  hand over, where D208 §4.4 bounded them by an offered label alone. The owner chooses it knowingly (§2.2's ask) or gives the steward a
  terminal of its own.
- **What it adds to an agent's reach: nothing.** Every act in §3.1 an agent's ordinary reach could already make as the person's (§0.6);
  the role makes the steward's own acts the steward's, and takes no grant away from the owner's own presses.

## 7. Where it sits in the company

The steward is D193's management (part 18), now a role the owner appoints, not a session that borrows the owner's presses. It is **not a
hierarchy of agents commanding agents** (D193's rejection): it changes Daoris's own controls (which repositories run, how, when a session
stops, goes on or hands over), says only Daoris's fixed go-on, and never edits a department's work, answers its questions, lands its
branches or accepts what it delivered. The supervisor answers questions asked of the owner; the steward runs the work the owner set going;
neither decides what is the owner's. Ask Daoris may explain the role and never appoints or overrules it (D208 §7).

## 8. Words

- **The role**: *steward*, zh **管家**. 管家 is the plain word for one who runs a household's affairs for its owner, which is the role; 总管
  was weighed and not taken, since beside the supervisor's 主管 the two read as ranks of one office. A glossary entry with the build
  (STEWARDROLE1b), its `avoid` holding 总管.
- **Its mark**: *By the steward, on your behalf* (管家代你); history *Recorded as the steward's* (记录为管家所为); its stop *Stopped by the
  steward* (管家已停止); its go-on *The steward asked it to go on* (管家请它继续).
- **The row and the line**: *Roles* (角色); *Steward appointed · {{count}} acts since Daoris started* (已任命管家 · 自 Daoris 启动以来 {{count}} 项操作),
  in the plural's forms; *Add to the terminal Daoris started at {{time}}* (加入 {{time}} 启动的终端), *A terminal of its own* (单独的终端).
- **Refusals**, each a sentence in both languages: `not-role` *That is the owner's to do; the steward's appointment does not cover it*;
  `owner-set` *The owner acted on this themselves since Daoris started; it stays theirs*; `role-terminal` (§3.3); `landing-pushes` *This
  repository's landing pushes, so driving it is the owner's*; and the rest by the act.

## 9. What D209 amends, what stands

**Amends**, once accepted: **D208** point 2, the supervisor design §2.2-§2.3 and SUPERVISOR1a-ii's door as built (a holder apart from its
appointment, shared or a role's own; `DAORIS_ROLE_KEY` and the entry's role-terminal marker; a holder's own folder by its id,
`work/rooms/roles/holders/<id>/`, never moved while it lives; a role revoked alone, the holder's hash dropped only with its last role; a
holder ending with its last role or its tab, told to the judge on every path; the holder's fixed initial prompt; the start line stated whole, with every role's verb
and the open reads; the holder's harness Daoris's own pinned binary, started by Daoris with an environment it composes, on Daoris's own configuration, checked by what it actually started with at each act; an
appointment's `scope`, `acts` and `setup`); **D208 point 3** (*Appoint again* one role at a time from the window's ask, the set-up tick
off, never the scope or acts the file names; each role offered by its own flag, `SupervisorOffered` and `StewardOffered`; SUPERVISOR1a-iii's
prerequisite SUPERVISOR1b and D201 part (i)'s case 4 on every door, in place of D201's offering); **D201** points 1, 2 and 5 (its four
presses the steward's acts; the grant not offered alone; its row *Roles*; no `standing.terminal` part on the steward's acts; its `Forget`
made by any hold change the role did not make); **D156** point 4 (a terminal's press of the person's waits for the window, unless a role's
appointment covers it by the key, recorded as the role's), **read beside CONFIRMWAIT1's proposed note on the same point**
(`D156.md:276-287`): a request the owner confirms hours later is a press at its Confirm, the owner's then, and marks its target
`owner-set` then, not when it was asked, so a steward's act between the ask and the Confirm stands until the Confirm and none follows it;
**D210** once accepted (its `retry` release of a transient hold made by the steward as well as the owner, **once per quest per run, with
no briefed fallback and no walk, written `by: steward`**, recorded as the steward's: §4.3a is the note D210's next round takes; its restore
of a `failed` record when a resumed start fails before the agent runs); **the move notes and hand-over reasons** (the steward's own
codes); **the ask's record** (`by: steward`) and DRIFT1b's quoting (`AskWords`); **the hand-over's asker**, its files and its start's read, authenticated against the run's memory; **the census**
(`ROLE_ACT`, `STEWARD_REQUEST`, `PipeCensus.Targets`); **the glossary**.

**These stand**: D208's foundation (appointed only in the window, recorded as the role's, the key never shown or sent, the dedicated
holder, the appointment lasting until a restart, the supervisor's decision lifecycle and its overrule window); D201's judge, record, lock,
forms and containment; TERMTRUST1b as D201's and the owner's; D156 points 1 to 3 and 5 to 7; D191 point 3; D193's rejection of a
hierarchy and part 1's acceptance by the owner; D135 point 2; D51; D124 §2.4.

## 10. Proof, and the rows in build order

Each row lands alone with its proof, each test by class, filtered. Nothing reaches the owner until activation (1d). **The flags**
(§2.4): `SupervisorOffered` gates the supervisor's ask, its `SET_APPOINTMENT` and `SUPERVISOR_ANSWER`, and only SUPERVISOR1a-iii turns it
on; `StewardOffered` gates the steward's *Roles* row, its ask, its `SET_APPOINTMENT` and `ROLE_ACT` for `steward`, and only STEWARDROLE1d
turns it on; `AppointmentsOffered` is derived from the two and gates nothing alone; `OfferedInThisBuild`, D201's grant, stays false.
`StewardActs.Offered`, a set in code, names the acts this build offers, each later row adding its own act behind its own look, and each
appointment keeping the set its ask named (§2.3). **The first usable slice is 1a to 1d**: drive, undrive, trees on, hold and resume, stop,
finish and *Try again*, with the desk; the hand-over's authenticated operations, the go-on and the set-up follow, none on that slice's
path. No ranking of which acts
remove the most cards is claimed: the owner's cards of 2026-10-24 named drives, a setup, go-ons and finishes, and 1d's look counts them.

**STEWARDROLE1a is two rows** (round 3's read, finding 13: as written it was round 1's size again). Each lands dark with its own proof,
and 1b needs both.

**STEWARDROLE1a-i — the holder and the role's door** (modules, driver, web; after D209's acceptance; lands dark). Against SUPERVISOR1a-ii
as built: the holder apart from its appointment (hash by holder, `RoleJudgement`'s `Holder` and `Act`), shared or its own at a later
*Appoint…*; a revoked role only to a new holder; the holder ending with its last role or its tab, by `HolderEnded` on the page's close,
the harness's exit, the module's disposal and an exit during the appointment's opening, swept; `DAORIS_ROLE_KEY` at every site and
`HolderKey.RoleTerminal` at the entry; one role revoked alone, the holder's hash dropped only with its last (finding 10);
`work/rooms/roles/holders/<holder id>/`, never moved or emptied while the holder lives (finding 11); the tab's title; the fixed initial
prompt as the argument vector's last element (finding 12); **the harness started by Daoris as the tab's process**
(§6.1, the read's option (a)): the managed, pinned `claude-code` by its absolute path and its hash, `HolderTerminal.SessionArguments` as an
argument vector, the composed environment, `holder-harness` with no pin, and, for as long as any role's tab is a shell, the system's
Windows PowerShell; the holder's configuration directory, its owned files rewritten and checked at each start and the harness's state left
alone; the judge's walk to the harness process the module started, run by one guard for every route of the `Role` class (finding 13), its image, its argument vector after the image and its
`CLAUDE_CONFIG_DIR` (`holder-config`); `roles brief` and every role verb's answer naming the holder's roles and the brief's revision;
`SET_APPOINTMENT`'s `scope`, `share`, `setup` and the record's `scope`, `acts`, `setup` (the built `repository` read as a scope);
*Appoint again* per role, the tick off; `SupervisorOffered` and `StewardOffered`, both false, `AppointsOffered` derived and decoupled from
D201's `Offered`; `ROLE_ACT` (`Role`) with the judge's order of §3.4 and an empty steward table; the run's memory of admitted acts and
pending operations **by their stages** (§5.1a's table: each consumer taking its own stage once, the execution cutoff after a consumer's
awaits) and of hand-over **bindings**, every stage not yet executed and every binding cancelled by a revoke in its mutation step
(finding 4); `PipeDoor.Role` carrying the act's id; `role-terminal` on every press verb
and every alone path; the log lines. **Proof**: `StandingConfirmationTests` (a second role shared and its own; a revoked role refused to
the old holder; the holder's end by its last role and by each of the tab's four end paths; a planted steward record dormant, *Appoint
again* naming the build's acts with the tick off, never the file's; **a shared holder's steward revoked while its supervisor goes on
answering, and the reverse, its hash kept until the last**; the built supervisor record read as its scope; **with `StewardOffered`
on and `SupervisorOffered` off, the supervisor's `SET_APPOINTMENT` and `SUPERVISOR_ANSWER` refused `not-offered`, and the reverse**),
`PipeStandingTests` (a `ROLE_ACT` with no proof, a wrong key, a holder's key for a role it does not hold, a replay, each refused; the act
outside `acts` refused `not-granted`; **a revoke between an act's admission and its gate: the act refused `appointment-changed` under the
gate, nothing written**; a revoke after `running`: the act completes and its pending operation is cancelled; **an operation of three stages taken by three
stand-in consumers in turn, each once, a second take of a stage refused; a revoke between each pair of stages ending every later one;
a binding read by two later starts and ended by a revoke between them; a revoke while a consumer's await is suspended between its two
steps, the cutoff then refused**; `holder-config` for a
process tree whose harness image is not the managed binary (PATH's `claude`, a `.cmd` or `.ps1` shim, a node running a script), whose
argument vector after the image differs by one element, whose `CLAUDE_CONFIG_DIR` names another directory, or whose directory holds a
changed `settings.json` or any of the absent files, and for a verb outside the holder's harness, **each for `ROLE_ACT`, `SUPERVISOR_ANSWER` and `SUPERVISOR_DEFER`**; **and the walk admitting a tree whose
harness directory also holds the harness's own state files**, so the check passes on what the real harness writes, not on a stand-in's
empty directory), **`HolderHarnessTests`** (the tab's process started with the managed binary's absolute path and no shell between; the
binary's hash differing, or no pin, refusing `holder-harness` with nothing started and never PATH's `claude`; the composed environment
holding `CLAUDE_CONFIG_DIR`, the updater off, the key and the install's own `PATH`, and none of a planted `NODE_OPTIONS` or other variable
of Daoris's own environment; a tools file naming another `pwsh` and a PATH with another `claude` and `powershell` both unused; the system's
Windows PowerShell taken where the tab is a shell), `HolderTerminalTests` (the argument vector whole, `{"disableAllHooks":true}` arriving
with its quotes, the initial prompt its last element; **two holders of the same role in two folders, neither emptying the other, and a
role added to a running holder leaving its folder in place, its title and brief changed**; the configuration directory's owned files and the harness's state left in place, the folder, the title; a stand-in
terminal factory, never a shell or a window), `HolderKeyStripTests` (the new variable at every entry), `PipeCensusTests`,
`SessionsCommandTests` (`role-terminal` through the real entry-to-dispatch path, `HolderKey.Take` then the verb, for a press verb and an
alone path, with the variable set to a key and to a text that is no key).

**STEWARDROLE1a-ii — `owner-set`'s census and the gates** (modules, driver, service; after 1a-i; lands dark). `owner-set` marked at
every owner door by `OwnerMarks.Of` and `PipeCensus.Targets`, `none` by its closed reasons with *acts on nothing a role acts on* and
`SET_APPOINTMENT` among them, `HELP_APPLY`'s plans, D201's `Forget` on any hold change, the record kinds that mark for the service's
doors, with the target's gates; **the target resolution of §5.4 (finding 6): a `workspace` field a target resolved to its repositories
at every door, and each act's dependencies' gates taken in the fixed order and its dependent conditions judged again under them; naming
told from mutating in the sweep, with its reviewed exemptions (finding 9)**. **Proof**: **the census sweep, `OwnerSetCensusTests`**: every `Person` and `Window` route has a
`PipeCensus.Targets` entry, every `HELP_APPLY` door of `HelpSettingProposals` and `HelpSetupProposals` declares its target, every
`Person` door of `PersonDoors.cs` declares the record kind its effect is attributed on, the sweep fails on any of them that names a
repository, a quest or a session and marks nothing, **and it cross-checks each entry against its handler's `[Publishes(…, Session = …)]`
and its required `repository`, `quest`, `session` or `id` field, so a wrong `none` fails, `SESSION_OPEN_FOLDER` passing as a reviewed
exemption, and a mutating route declaring `none` or listing itself an exemption failing**; `OwnerSetTests` (a mark by repository and by
quest; an applied Ask Daoris *Stop driving* then the steward's drive refused `owner-set`; Ask Daoris's *Resume* then *Hold* after a
steward's hold, then the steward's resume refused `not-own`; the owner's `SESSION_QUEUE_EDIT`, `SESSION_GO_ON_NEW` and
`DISCARD_SESSION_TREE` each marking the quest; **`SESSION_OPEN_FOLDER`, `NOTICES_READ` and the supervisor's `SET_APPOINTMENT` on a
repository marking nothing; a quest published this run and its ask's own words marking nothing, a person's word or move written on its
record after publication through the service's door marking it**; a quest's successor session covered; **the landing race: an owner's pushing landing rule, set by repository and by workspace alone,
between the steward's admission of a stop or finish of a parked `Done` session and its gate, the act refused `landing-pushes` under the
gates; and a direct workspace-scoped `SET_LANDING` and `HELP_APPLY` each marking every repository of the workspace**; the gate's two orders, a steward
act admitted then an owner's act, and the reverse, with no steward effect after the owner's and the judge's lock never held across an
await).

**STEWARDROLE1b — the steward's immediate acts and their records** (modules, driver, web; after 1a-i and 1a-ii; dark). §3.1 rows 1 to 8 in their
forms, paths and conditions, D201's `resume` memory moved to the role; **§3.1a's checkout exclusion at the actual start, `held.checkout` (finding 2)**; **the
steward's releases in `roleReleases`, never the owner's maps (finding 7)**; **finish's wording: neither accepting nor closing its quest
(finding 14)**; the steward's own move notes and every reader by code (§5.1's
table); *Set by the steward*; history against memory; *Roles* with its ask; the *Notifications* line with its reverse presses; *Stopped by
the steward* in *What needs you*; the glossary. **Proof**: `RoleActTests` (each act admitted in its exact form; another field or value
refused `form`, a field name's casing read as D201's, a name twice refused; every §3.2 route, `QUEST_DONE` among them, refused `not-role`;
`trees off`, `decline` and a retry of the owner's stop refused; an unregistered repository refused; **a drive of a repository whose
sessions open no trees of their own refused `no-tree`, nothing written, and admitted once the steward gave it trees**; **drive and trees on refused
`landing-pushes` where the effective landing pushes; a stop of a session whose quest is `Done` and a finish of such a park refused
`landing-pushes` under an auto-accepting branch rule with a pushing plugin, and admitted under a rule that does not push**; a retry where
`StandingChoices.Retry` would answer `Marked` refused; a finish of a helper's, a workflow node's or a session with a word waiting refused;
**a live stop writes no `SessionRequest` and its record's note is `ended.stopped-steward`**; **a stop of a session another Daoris process of
this machine runs refused `running-elsewhere`, nothing written, no request file**), §3.1a's proof (**an earlier checkout session with trees
then given and the repository driven: its continuation and a new part resuming it held `held.checkout`, nothing started in the
checkout**), **the deferred authority of *Try again* (finding 8)**: an owner's stop or *Try again*, a revoke and a restart each between the
steward's release and the planner's start, each ending it with nothing started; the release stored in `roleReleases` with `by: steward`
and the act's id; the start's run attributed to the steward's release at its final consumer; **and the older reader and writer (finding 7)**:
`DriverConfig`'s readers and writer as on main reading a config with a steward's `roleReleases` entry release nothing, a rewrite by them
dropping it leaves the quest held, and an owner's ordinary release beside it stays usable, the notes' sweep test (no record of a steward's act, stored
English or translated en and zh, nor a resumed brief nor the planner's reason, reads *person* or *you*; every reader of the person's codes
has the steward's case), `PlannerTests` (a steward's stop held with its own reason, released by the steward's *Try again* and by the
owner's), the row's and the line's page tests in en and zh, both themes.

**STEWARDROLE1c — the desk** (modules, driver; after 1a-i, which is all it needs; after 1b only as the work's sequence). `STEWARD_REQUEST`, `steward desk`, the fenced print, its bound, its closing by act
or reasoned decline, its line in *Notifications* (the web's line and its page test). **Proof**: `StewardDeskTests` (any caller files;
nothing is granted; a request never admits an act nor reaches a handler; **the per-caller bound filed from many verb processes of one
agent job, and from many children of one long-lived ancestor, binding at ten**, and the whole bound; closing by the act that names it).

**STEWARDROLE1d — activation** (modules; after 1c and D201 part (i)'s §8 case 4 seen on every door; **never turning on
`SupervisorOffered`**). A candidate build with `StewardOffered` on and `SupervisorOffered` off, installed and looked at before the change
turning it on lands, carrying SUPERVISOR1a-iii's installed checks: the holder's tab and the exact argument vector it ran, read from the
process; **the programs the key was handed to**: the tab's process the managed, pinned `claude-code` by its path and hash, no shell
between, its environment the composed one (no inherited loader variable, the updater off), a `pwsh` named in the tools file and a
`claude` first on PATH both left unstarted, and the shell the harness's Bash tool ran a verb in recorded; **the owner's sign-in in the
tab**, its configuration directory starting signed out, said in the ask and done by the owner; **the configuration it loaded, proved by
canaries**: a harmless hook, an enabled plugin with a hook of its own, a status line and an instruction file placed in the owner's own
user configuration for the look (and removed after), **and a canary `CLAUDE.md` in `work/rooms/roles/` and at the home's root**, none of
which the holder's harness runs, lists or reads, and the same hook written into the holder's directory after its start, not run; the harness's version recorded; no
project or local settings, no project MCP process; the verb against the install's own `app/Daoris.Desktop.exe`; the supervisor's ask
absent and `SUPERVISOR_ANSWER` refused `not-offered`; then the owner appoints the steward; its holder
gives a repository trees, drives and undrives it, its drive refused `no-tree` before the trees, holds and resumes it, stops a session and tries it again, finishes a parked one, each
with no card, each said on its record and in *Notifications*, read in en and zh with no *person* or *you*; the owner's *Stop driving* after
a steward's drive, and the steward's drive then refused `owner-set`; the owner's go-on after a steward's stop, and the steward's stop then
refused; a request filed at the desk by the working session and answered by an act, **the holder having read its brief and the desk from its
initial prompt alone, the owner typing no instruction into its tab (finding 12)**; a press from the working session waiting for the card;
a caller with no key refused; **Turn off**, and the holder's next act refused; a restart, the appointment dormant, *Appoint again* with the
tick off; the holder's tab closed, and the next *Appoint…* opening a new one. The `dispatch-subagent` skill gains the working session's
rule. **Proof**: the look's record in the decision's note, with the count of cards the owner did not press.

**STEWARDROLE1e — the hand-over and its answers** (driver, modules, web; after 1d; offered with its own look). §3.1 rows 9 and 10 as
pending operations (§5.1a): the look, the turn's end and **the start** consuming them from memory under the quest's gate, the steward's yes
written `by: steward` and taken by `AtTurnEndAsync` as `HandOverBy.Steward`, the revoke's and the owner's cancelling, the restart's
clearing, `HandOverBy.Steward` and its readers; **the look's `GoAsync` as row 10's consumer for a parked session**, its box `askedBy:
steward` with the operation's id, consumed as row 9's. **Proof**: `RoleActTests` (a hand-over with `account` refused; an agent outside the
effective `build` order refused `not-actor`; an approve without `ask` or without `agent`, of an owner's conversation, or **with a same-id
ask whose agent changed after the listing and before admission**, refused `ask-changed`), `DriverHandOversTests` (§3.1b's `owner-held` proof, at admission and at each consumer (finding 1); a planted box and an
altered one claiming the steward cleared and said, never consumed; **the carrier's actor removed or replaced with the ordinary
`person` or `agent` and an ordinary reason, and its operation id removed, between each pair of consumers (the look, `GoAsync`, the turn's
end, the start): the carrier invalidated by memory, nothing handed as the owner's or the agent's, the operation ended (finding 3)**;
**the parked yes's whole chain, `GoAsync`, the look and the start, each taking its stage once, with a revoke between every pair of
stages, and a revoke after the first start and before a later one: the later start on the owner's order (finding 4)**; **a handed file replaced between the look and the start: the start uses
neither the changed agent, an account nor a note, starts on the owner's order and says it**; a steward's handed file found after a restart
cleared, the quest on the owner's order; **a steward's yes, then the ask's agent changed before the turn's end: refused `ask-changed`, the
ask left waiting**; a steward's yes taken at the turn's end written `HandOverBy.Steward`, never `Agent`; a planted `approved` claiming the
steward with no operation set back to waiting; **a steward's box or yes kept, then the owner's stop, then the consumer: cancelled, nothing
handed**; **a session parked before the steward's yes, then the owner's stop, then the looks: the yes cancelled under the quest's gate,
no box kept, nothing handed; and without the stop, the box `askedBy: steward`, the park ended with the steward's reason, the handed file
`HandOverBy.Steward`, never `Agent`**; the same with a revoke in place of the stop; an owner's stop then a steward's hand-over refused `owner-set`).

**STEWARDROLE1f — the go-on** (driver, modules, service, web; **after 1d, AGENTERRORS1a (landed) and D210 carrying or answering
§4.3a's note**, whose reader, `ended.transient`,
`TransientHolding` and `retry` release it is; offered with its own look). §4: the `not-outage` condition on AGENTERRORS1a's note code and
hold, the release `WithTransientReleased` written `by: steward` with its act's id, one per quest per run (`go-on-spent`), no briefed
fallback and no walk, the release as a pending operation consumed by the planner's start under the quest's gate, its record check D210's
end key in the start's own step, its withdrawal on cancel or a failed check, the restore of a `failed` record extended in the service with no word taken, the planner's reason and the line. A door AGENTERRORS1
reads later (its rows 1b to 1f) needs nothing here: its sessions write the same note code. **Proof**: `RoleGoOnTests` (admitted on a
session `failed` with `ended.transient` and its quest held by it; **refused `not-outage` on a park whose agent asked the owner in prose,
on a park of any kind, on a record ended `access`, `limit` or `fatal`, and on a session that is not its quest's last record**; refused on a
running session, an open question, an open go-ahead, a word of any author waiting; refused where `StandingChoices.Retry` would answer
`Marked`; the release written, the planner's reason *the steward asked it to go on*; **an owner's stop, queue edit or word between the
release and the start, and a revoke there, each cancelling it: the release withdrawn, nothing started**; **a revoke while the start's
record check is suspended: the cutoff refused, nothing started (finding 5)**; the release in `roleReleases`, an older reader releasing
nothing from it (finding 7); **a quest whose steward-released
run ends `ended.transient` again refused `go-on-spent`, waiting on the owner; a steward-released resume meeting the account, the
conversation or the choice gone, or a native release that would go briefed: not started, the release withdrawn, the record held, no walk**;
**after a restart, a release `by: steward` withdrawn and said and the owner's own *Try again* release, beside it on another quest, kept**), `DriverContinuationsTests` or AGENTERRORS1a's resume class (**the record reread, the preparation and the spawn each
failing after the start is admitted: the record restored to `failed` `ended.transient`, the quest held again, every word on it kept, an
owner word that arrived after the release still waiting; the restore itself failing: the record left for the orphan sweep and the quest
said held**), the service's restore test for a `failed` record, `PlannerTests`; the look: a stand-in agent installed as a plugin harness
that keeps failing transient past AGENTERRORS1's bound, the steward's go-on, the same conversation going on with no card, read as the
steward's in en and zh.

**STEWARDROLE1g — the set-up** (service, driver, modules, web; after 1d, offered with its own look; ordered after 1f only as the work's
sequence, not by a mechanism). The appointment's `setup` tick in the ask, row 12, the check inside
`SetupPress.ApplyAsync` under `PipeDoor.Role` immediately before the rules are saved, the ask's `by: steward` and `AskWords`' heading.
**Proof**: `RoleActTests` (refused without the tick and for an index quest), `SetupPressTests` (a plan changed from set-up to index between
judgement and application refused `plan-changed`, nothing saved, nothing published), the service's publish test for `by`,
`AskAndWaitPromptTests` (a steward's ask is never quoted as the person's).

**STEWARDROLE1h — free words** (not designed here). If wanted: their own design, under D191, with their own read, as D208 gave SUPERVISOR1f.
D209 decides nothing of them.

## 11. Considered and rejected

- **The steward's working session holding the key.** It runs other branches' code (§0.4); D208's round-4 direction stands.
- **A broker in the working session that signs what it is asked.** A signer any process of that session can ask is the key by another name.
- **Stripping the key before each build or test in the working session.** A harness's every tool call inherits its environment, and one
  forgotten strip is the leak; D208 rejected the same for the supervisor.
- **D201's standing confirmation as the steward's door** (§2.5): it cannot tell the steward from the owner's own terminal.
- **Both doors for D201's four presses**: two records for one act, one of them the owner's for a steward's act.
- **A route per steward act**: a census line, handler and twin per act for what one table holds.
- **A card per act, or *don't ask again* on a card**: the per-act confirmation the owner's frame removes (D208 point 1, D201's rejection).
- **An overrule window on every act** (§5.5). **A notice per act** (§5.3).
- **The steward's mark as a suffix after the person's note** (round 1): the note still says *the person*, and every reader by code reads
  it so. The steward writes its own codes (§5.1).
- **`owner-set` by route and target** (round 1): an owner's go-on is another route than the steward's stop, and a quest's next session
  another target, so the overrule did not stick. Keyed by repository or quest (§5.4).
- **Closing a quest as done** (round 1's row 12): the owner's acceptance, not undone by *Reopen* (§2.4).
- **Free words in D209, through `SESSION_INPUT`** (round 1): they reach the ask as the person's, the inbox's process-only fallback, the
  answers-alone hold and other sessions under management's name. The fixed go-on covers the direction's act; free words are their own row
  and read (§4.1).
- **The go-on as a word `From: steward` kept on a parked record** (round 2): admitted to any quiet park, a prose question to the owner
  among them, and needing a word lifecycle of its own (a conditional keep, a quarantine, a ticket, a recovery that left the record working
  with no run). The go-on is AGENTERRORS1's release of a transient hold instead, adding no word (§4.1).
- **One `AppointmentsOffered` for every role** (round 2): turning on the steward would open the supervisor before SUPERVISOR1b and its own
  look. A flag per role (§2.4).
- **`owner-set` as a list of routes** (round 2): Ask Daoris's applied proposals, D201's `Forget`, other quest routes and the service's
  own doors escaped it. Marked at every owner door by what it names, proved by a census sweep (§5.4).
- **A gate held only across the handler** (round 2): a hand-over's box, an approved ask and a release take effect later, so the owner's
  act or a revoke between them escaped it. Pending operations, checked by their final consumer (§5.1a).
- **The steward's live stop through the request folder**: its note would be built later, outside `PipeDoor.Role`, from a file any
  process writes. The instance stops it with its note at once (§3.1 row 6).
- **The holder on the user's own configuration, with *no user hooks or plugins* as a precondition** (D208's wording, round 2): nothing
  enforced it, and a hook planted once would act as the steward at every appointment. Daoris's own configuration, checked by what the
  harness actually started with (§6.1).
- **The holder's shell and harness as a file or PATH resolves them, the owner typing the line** (round 3): any process of the account
  chooses those programs, and the key is handed to them. Daoris starts its pinned harness itself, with an environment it composes, and
  takes any shell from the system's location (§6.1). The read's option (b) alone, naming the limit and keeping the typed line, was not
  taken as the build: it is named under §6.3's *Not closed* for the door as built.
- **A whole-directory hash of the holder's configuration** (round 3): the harness writes its own state there, so it would refuse the
  first act. Daoris checks exactly the files it owns (§6.1).
- **A steward go-on on every transient hold** (round 3): an outage would loop past D210's bound with nobody but the steward. One per quest
  per run, with no briefed fallback and no walk (§4.2, §4.3a).
- **Drive on a repository with no session trees** (round 3): Daoris's sessions would work in the owner's checkout, delivering with no
  acceptance; refused `no-tree` (§3.1 row 1).
- **The steward limited to repositories registered before its appointment**: D209 point 1 takes in one registered later, and the owner's
  *Stop driving* or *Hold* makes any repository theirs; the ask says that registration is not the owner's alone (§6.3).
- **`trees off`, `decline`, *Try again* on a stop not its own, a work's pause and resume, a quiet session's carry-on**: each the owner's
  (§3.1, §3.2); a later row may argue one in with its own reason.
- **Naming the account on a hand-over**: choosing what is spent is the owner's.
- **One holder for every role, always** (round 1): a session holding the supervisor's role reads asking agents' text with management acts
  in hand, which D208 §4.4's bound did not cover. A shared holder is the owner's choice at the second *Appoint…*, said in the ask (§2.2).
- ***Appoint again* re-appointing every role found in one ask** (round 1): it would revive what a file says. One role at a time, from the
  window's ask (§2.3).
- **A gitignored relay file before the desk**: a second, unbounded channel for what the desk holds; the desk lands before activation instead.
- **Keeping the owner's overrules past a restart** and **an unkeyed terminal's standing grant for the owner's own presses**: widenings or
  narrowings the owner may choose later; not taken now.
- **管家's alternative 总管** (§8).

## 12. The reads

### 12.1 Round 1's reads, answered in round 2

Round 1 was read at `48f9b3cff` by Codex (another round) and by the independent Opus 5.5 foundation reader (accept after named changes,
20 findings, 7 blocking), verbatim in `docs/2026-10-24-steward-role-reads.md`. Each finding was checked against the code on main at
`f5c515f0b`; *taken* means round 2 makes the change, *answered* that it holds otherwise for the reason given, *deferred* that it waits with
its reason. **C** is Codex's read, **O** the Opus foundation read. Round 2's reads reopened some of these; §12.2 answers them, and where it
changes a mechanism this table names (C6's and O8's word lifecycle, O10's revoke, O17's marks), §12.2 and the sections above are what
stands.

| # | Finding | Checked on main | Outcome |
|---|---|---|---|
| C1 | Blocking: deferred hand-overs can impersonate the steward | `HandOverFiles.ReadBox`, `ReadHanded`; `DriverHandOvers.cs:84`, `:133` write the box's `askedBy` on | **Taken**: the admitted operation kept in memory, a steward box or handed file consumed only on an exact match, else cleared and said; planted and altered files tested (§5.1; row 1e) |
| C2 | Blocking: `owner-set` protects a route, not the owner's decision | `DriverHandOvers.cs:102-108`: a hand-over releases an ended session's stop; a new part changes the session | **Taken**: keyed by repository or quest across every route, successor sessions covered; tested with a stop then a hand-over (§5.4; rows 1a, 1e) |
| C3 | Blocking: an admitted act can overwrite a later owner press | The settings handlers write independently (`DriverRoutes.cs`) | **Taken**: a gate per target, `owner-set` checked again under it, the owner's mark set inside it; both orders proved, the judge's lock never across an await (§3.4 step 7, §5.4; row 1a) |
| C4 | Blocking: hand-over approval's form and binding | `SessionsRoutes.cs:400` reads `session`, `:405` defaults to the current ask; `SessionsCommand.cs:444` sends `session` | **Taken**: `{ session, ask }`, a quest's own session, the ask and its agent judged at application, `ask-changed`; tested (§3.1 row 10; row 1e) |
| C5 | Blocking: a steward word bypasses the answers-alone hold | `AnswersAlone.cs:34-35`; `AnswersAloneHold.cs:31`, `:57`; `GoOnMarks.Judged` | **Taken**: the go-on refused where any word waits or a hold is on or due, and the hold, its candidates and `Judged` read words less the steward's; tested (§4.2-§4.3; row 1f) |
| C6 | Blocking: role words need a complete keep-to-hand protocol | `DrivenInbox.cs:488`: `KeepOrHoldAsync` falls back to the process alone | **Taken**: free words leave D209; the fixed go-on is kept only by the service's conditional step, never to a running session, nothing held in the process, rechecked and withdrawn before its start; failure, a late wait and a revoke tested (§4.1, §4.3; row 1f) |
| C7 | Blocking: appending `role.steward` leaves personal attribution | `SessionMoves.cs:79-81`; `Observation.cs:374`; `HandOverRecords.cs:46`, `:60` | **Taken**: the steward's own codes and reasons, every reader by code; stored English, translations and resumed briefs tested (§5.1; row 1b) |
| C8 | Blocking: session words can become owner requirements on the ask | `ConversationRoutes.cs:146` → `Program.cs:1465`, no author filter | **Taken**: the go-on never goes to a running session, so never to the added door; the taken door keeps `Persons` only (`Program.cs:1504`); absence from `AskWords` tested (§4.3; row 1f) |
| C9 | Blocking: the set-up restriction must guard the plan applied | `SetupRoutes.cs:46` recomputes the plan; `SetupPress.cs:170-171` adds `RulesOf(plan)` | **Taken**: the check inside the application, immediately before the save, against the plan applied; a changed plan tested (§3.1 row 12; row 1g) |
| C10 | Blocking: *Reopen* cannot reverse this `done` | `Quests.cs:2057`, `:2066`; the terminal-trust design's `finish` row | **Taken**: row 12 and STEWARDROLE1e dropped; `QUEST_DONE` the owner's acceptance (§2.4, §3.2) |
| C11 | The casing claim does not describe D201's forms | `IsForm`, `StandingConfirmation.cs:763-783`, names without case, a name twice refused | **Answered**: the steward's forms keep D201's comparison, since the handler reads names so and the form must mean what the handler reads; values exact; tests aligned (§3.1; row 1b) |
| C12 | Tree settings govern starts, not relocation | `DriverConfig` writes the choice; `Driver` reads it at a start | **Taken**: §0.1 and row 3 say starts and continuations; `off` stays the owner's |
| C13 | Index commands are generated, not declared | `SetupPress.cs` via `IndexForms.For`; `IndexForms.cs:56-61` | **Taken**: §0.1 and row 12 corrected; the exclusion stands, since they run the repository's generator code |
| C14 | A finish does not always leave a quest for owner acceptance | The terminal-trust design's `finish` row; `SessionsCommand`'s finish answer | **Taken**: finish narrowed to a quest's own session, not a helper's or a workflow node's, no word waiting; the quest stays open or taken for the owner (§3.1 row 7; tested in 1b) |
| C15 | Adding a role to a live holder needs a permission transition | `HolderTerminal.SessionLine` allows the answer verb alone | **Taken**: the start line allows every role's verb and `roles brief`; the judge is the authority; the brief re-read when a role is added (§2.2; row 1a) |
| C16 | The row proofs weaken the inherited activation evidence | Supervisor design §11 row 1a-iii; `TASKS.md`'s SUPERVISOR1a-iii | **Taken**: 1d carries the command line, configuration, MCP, revoke, restart and tab cases; each later act offered by its own flag and look, an appointment keeping its ask's `acts`; the first usable slice named, the ranking removed (§2.3, §10) |
| O1 | Blocking: the steward's stop and finish read as the owner's | As C7, and `Planner.cs:259`'s held reason | **Taken** as named: own move notes `ended.stopped-steward`, `stopped.checkpoint-stopped-steward`, `stopped.checkpoint-finished-steward`; readers by code, TERMTRUST1b's done and the planner's reason among them; a proof that no record reads *person* or *you* (§5.1; row 1b) |
| O2 | Blocking: `owner-set` does not keep an overrule | As C2 | **Taken** as named: keyed by repository (rows 1-5, 12) or quest (rows 6-11) whatever the route; a stop, the owner's go-on, a stop refused, and across a retry tested (§5.4; row 1a) |
| O3 | Blocking: row 12 is the owner's acceptance | As C10 | **Taken** as named: row 12 and STEWARDROLE1e dropped; `QUEST_DONE` in §3.2's acceptance row |
| O4 | Blocking: words to a running session kept on the ask as the person's | As C8; `Asks.cs:200` | **Taken** as named: the steward's go-on never takes `KeepOnAsk` or the added door; it goes only to records not running; absence from `AskWords` proved (§4.3; row 1f) |
| O5 | Blocking: the steward's words lift the answers-alone hold | As C5 | **Taken** as named: `answers-held` refuses the go-on where the hold is on or due, and the hold's predicate reads words less the steward's; `AnswersAloneTests` (§4.2-§4.3; row 1f) |
| O6 | Blocking: *Appoint again* revives what a file says | The built *Appoint again* opens the same ask per row | **Taken** as named: one role at a time, from the window's ask, the set-up tick always off and the file's `acts` and `setup` never read in; a planted record tested (§2.3; row 1a) |
| O7 | Blocking: STEWARDROLE1a is written against a door already built differently | §0.2's facts on main: `DAORIS_SUPERVISOR_KEY`, a key per appointment, `work/rooms/roles/supervisor/`, the panel's tab, `SUPERVISOR_WAITING` by event | **Taken** as named: §0.2 and §2.2 rewritten against the built door; 1a is its amendment (§10) |
| O8 | The handed words come from the record, not memory | D208's rejection of handing a stored text | **Taken**: the go-on's text and id kept in memory, the paragraph composed from it, a differing record word quarantined; the act's id drawn by Daoris at admission (§3.3, §4.3) |
| O9 | A role added to a live holder meets a session without its verbs | As C15 | **Taken**: the start line always allows every role's verbs; the judge's `not-appointed` is the authority (§2.2) |
| O10 | The revoke and the holder's end | `TerminalModule.Ended` emits `TERMINAL_EXITED`, the judge not told; `HandOverQuest.cs:101-109` | **Taken**: the last role's revoke or the tab's exit drops the holder's hash; a revoked role only to a new holder; the revoke clears the steward's untaken boxes (§2.2, §5.1, §5.4) |
| O11 | One holder per run widens what the supervisor's inputs reach | Supervisor design §4.4 | **Taken**: §6.3 and §11 corrected; the ask says it; *a terminal of its own* offered at the second *Appoint…* (§2.2) |
| O12 | The holder's plan verbs act alone with git | `InstanceDoor.cs`'s `None` and a headless loop's home | **Taken**: where `DAORIS_ROLE_KEY` is set, every alone path refuses `role-terminal` (§3.3, §6.1) |
| O13 | The forms choose paths the design does not name | `SessionsRoutes.cs` reads `said`; `ConversationRoutes.cs:253` the window's path | **Taken**: each form stated whole with its path and record (§3.1); §0.1's forms and §0.5's citation corrected (`SessionsCommand.cs:1683`) |
| O14 | Free words launder agents' text | — | **Taken**: 1f is a fixed `steward go-on <id>`, Daoris's text; free words are STEWARDROLE1h, with their own read (§4.1) |
| O15 | The relay from the working session to the holder | `.claude/skills/dispatch-subagent` | **Taken**: the desk moves before activation (1c), bounded at 100; the working session's rule into the skill at activation; the holder's brief from `roles brief`. The interim relay file is not taken, since the desk precedes activation (§6.2, §11) |
| O16 | A steward's stop waits on the owner | `Planner.cs:259` | **Taken** both ways: *What needs you* names it *Stopped by the steward*, and the steward's own *Try again* on its own untouched stop (§3.1 row 8, §5.3) |
| O17 | The record marks are claims | `HandOverFiles`; the note codes | **Taken**: live marks and reverse presses only from this run's memory; otherwise history; hand-over files matched against memory (§5.1) |
| O18 | Drive's reach | `SetupPressRules.LandsAs`'s pushing plugin | **Taken**: `landing-pushes` refuses a drive where the landing pushes; the ask says an earlier run's undriven repository is not the owner's this run (§3.1 row 1, §5.3) |
| O19 | The appointment's form and its flags | `SET_APPOINTMENT { role, repository, holder, on }` as built | **Taken**: `scope` as `machine` or `repository:<name>`, the built form read as a scope; which flag gates what (§2.3, §10) |
| O20 | STEWARDROLE1a's size | — | **Taken**: 1a the door's amendments, 1b the acts and their records, each with its own proof (§10) |

Since free words leave D209 for STEWARDROLE1h, no row of this design carries them, and STEWARDROLE1h gets its own read if it is written.

### 12.2 Round 2's reads, answered in round 3

Round 2 was read at `92dd03ec5` by Codex (another round, 12 findings, 8 blocking) and the Opus foundation reader (accept after named
changes, 14 findings, 5 blocking), verbatim in `docs/2026-10-24-steward-role-reads.md`. Each finding was checked against the code on main
at `aa16658e7`, at the lines in the second column. **C2-n** is Codex's finding n, **O2-n** the Opus read's. Codex's table of round 1's
findings marks C1-C4, C6, C7, C12, C16, O1, O2, O10, O13 and O17 *open* for reasons its findings 1 to 12 carry, so they are answered there.
Both reads asked to keep the shared appointment, the separate working session, the steward's own records, the fixed go-on and the removal
of steward done; all five stand.

| # | Finding | Checked on main | Outcome |
|---|---|---|---|
| O2-1 | Blocking: `owner-set` is a list of routes; `HELP_APPLY`, its `Forget` and quest routes escape it | `HelpRoutes.cs:494`; `HelpSettingProposals.cs:61`, `:80-105` edit the config directly; `Forget` only at `DriverRoutes.cs:62`; `SESSION_QUEUE_EDIT` at `ConversationRoutes.cs:355`; `PersonDoors.cs`' 35 `Person` doors | **Taken as named**: marked by what was acted on at every owner door, `OwnerMarks.Of` over `PipeCensus.Targets` for every `Person` and `Window` route, `HELP_APPLY` by its plan's target, `Forget` on any hold change the role did not make, the service's doors and the request folder by the record; proved by the census sweep `OwnerSetCensusTests` and an applied Ask Daoris *Stop driving* then the steward's drive refused (§0.5b, §5.4; row 1a) |
| O2-2 | Blocking: 1d turns on every role, the supervisor included | `StandingConfirmation.cs:209`, `:257`, `:537`, `:633`, `:657` read one flag; `TASKS.md:266`, `:269` | **Taken as named**: `SupervisorOffered` and `StewardOffered`, each gating its role's ask, `SET_APPOINTMENT` and admission; `AppointmentsOffered` derived; 1d never turns on the supervisor's, proved by a refused `SUPERVISOR_ANSWER`; SUPERVISOR1a-iii's prerequisite SUPERVISOR1b and the containment's look in place of D201's offering (§2.4, §9, §10) |
| O2-3 | Blocking: trees on, a stop or a finish can publish where the landing pushes | `AutoLanding.cs:203-209`: a done in its own tree is `Due` under an auto-accepting branch rule, `IsParked` the only hold back; `SetupPressRules.cs:64-67` | **Taken as named**: row 3 carries `landing-pushes` on `LandingRules.Effective`, its *Only narrows* dropped; rows 6 and 7 refused `landing-pushes` where `AutoLandingRules.Concluded` would answer `Due` under a pushing plugin, judged again under the gate; D209's *Why* corrected; both in `RoleActTests` (§3.1, §5.5; row 1b) |
| O2-4 | Blocking: the go-on is admitted to any quiet park | `TASKS.md:375`: the driver reads no outage; D210's design §8 and §11 row 1a: `ended.transient`, `TransientHolding`, the `retry` release | **Taken as named, through AGENTERRORS1's own mechanism**: `not-outage` admits only a quest's last record `failed` with `ended.transient` and held by `TransientHolding`; the go-on is that hold's `retry` release, resuming the same conversation with D210's `retry.go-on`; a prose question to the owner refused; 1f after 1d and AGENTERRORS1a (§0.1, §4; row 1f) |
| O2-5 | Blocking: the holder's line loads the user's configuration | `HolderTerminal.cs:27-29`: `--setting-sources user`; `HolderTerminalTests.cs:90` holds the text | **Taken, both halves the read offered**: neutralised (the holder's harness runs on `CLAUDE_CONFIG_DIR`, a directory Daoris rewrites and hashes at each start, `disableAllHooks` there and inline on the line, no plugin, status line, helper or instruction file), and what Daoris cannot prove from outside the harness stated as an open limit checked at activation by canaries in the owner's own configuration (§6.1, §6.3; rows 1a, 1d) |
| O2-6 | A revoke falls between admission and the gate | `StandingConfirmation.cs:629`'s later recheck for the supervisor | **Taken**: as C2-1 |
| O2-7 | The role-added line never reaches the holder session | `TerminalModule.cs:248` prints to the screen | **Taken as named**: every role verb's answer names the holder's roles and the brief's revision; the brief tells the session to read it again when either changes; the printed line is the owner's (§2.2) |
| O2-8 | The start line and §6.1 name different permissions | `SessionsCommand.cs:350` (`sessions --json` the list) | **Taken**: the line stated whole with each verb's exact prefix, no `sessions:*`, held by `HolderTerminalTests` (§2.2, §6.1) |
| O2-9 | A hand-over chooses *on what* runs | `ActorChoices.cs:101` (`Effective`) | **Taken**: `agent` must be in the repository's effective `build` order, `not-actor` otherwise, for rows 9 and 10 (§3.1) |
| O2-10 | `RETRY_QUEST`'s one form also forgives strikes | `StandingChoices.cs:108-131`: `Exhausted` answers `Marked` | **Taken**: rows 8 and 11 admitted only where `StandingChoices.Retry` answers `Released` (§3.1, §4.2) |
| O2-11 | Row 10's yes goes through the ask's state file | `HandOverAskWord.cs:44-50`; `DriverHandOvers.cs:279-294` writes `HandOverBy.Agent` | **Taken**: the yes is a pending operation; `approved` written `by: steward` with the operation's id, `AtTurnEndAsync` consuming it from memory as `HandOverBy.Steward`; a planted `approved` claiming the steward set back to waiting (§5.1a; row 1e) |
| O2-12 | `scope: machine` takes in any process's registration | `PersonDoors.cs:215`: `POST /api/registry` is `Driver` | **Taken, the read's first option**: said in §6.3 and the ask; limiting to earlier registrations rejected with its reason (§11) |
| O2-13 | Any caller can fill the desk | §6.2's one bound | **Taken**: 10 per caller process and 100 in all, the per-caller bound first; the remaining crowding by many processes said in §6.3 (§6.2; row 1c) |
| O2-14 | D209's body against the design | D209 points 2, 3, 6 and its Amends | **Taken**: point 3 says row 2 stops driving any registered repository and that trees on, a stop and a finish refuse `landing-pushes`; the Amends name D208 point 3; point 6 says every owner door, as §5.4 now does |
| C2-1 | Blocking: revocation does not invalidate an act waiting for its gate | `QuestionSupervisorDecisions.cs:357`, `StandingConfirmation.cs:629`: the supervisor rechecks later | **Taken, concretely**: an admitted act is registered `admitted`; the revoke's one mutation step cancels every `admitted` entry and pending operation of its generation; under the gate the act, in one step under the judge's lock, checks it is still `admitted`, the holder's hash present and the generation live, and moves to `running`, else `appointment-changed`; the lock never across an await; a revoke in the wait tested (§3.4; row 1a) |
| C2-2 | Blocking: the gate and the sweep miss deferred effects and their actor | `HandOverQuest.cs:101`; `DriverHandOvers.cs:102`, `:133`, `:279-294`; `SessionsCommand.cs:949`; `SessionRequestWatch.cs:345-372` | **Taken, concretely**: every deferred effect is a pending operation with its actor, generation and complete operation (§5.1a's table: hand-over, yes, go-on, *Try again*); each final consumer (the look, the turn's end, the start, the planner's start) takes the quest's gate and the operation from memory, rechecks authority and `owner-set`, and acts as `HandOverBy.Steward`; the revoke and the owner's mark cancel them, the sweep clearing what they left; the live stop writes no request file, its note fixed in the instance; each path tested with its final note (§5.1a; rows 1b, 1e, 1f) |
| C2-3 | Blocking: the start rereads the handed file | `Driver.cs:1896`, `:1087`; `HandOverFiles.cs:97-99` | **Taken, concretely**: the start takes agent, null account and note from the operation in memory where the file names the steward, any difference treated as tampering (owner's order, file cleared, said); after a restart a steward's handed file is cleared and the quest goes on with the owner's order; a replacement between look and start tested (§5.1a; row 1e) |
| C2-4 | Blocking: the approval does not bind the agent the holder saw | `HandOverAskWord.cs:44-50` reads the agent from writable state; `SessionsRoutes.cs:405-410` | **Taken, concretely**: `SESSION_HAND_OVER_APPROVE { session, ask, agent }`, `agent` in the signed form, compared at application and at the turn's end; a same-id ask's agent changed before admission and after it tested (§3.1 row 10; row 1e) |
| C2-5 | Blocking: Take back does not make the quest the owner's | `ConversationRoutes.cs:355-380` | **Taken**: `SESSION_QUEUE_EDIT` marks its session's quest through the census, as every quest-scoped route does; the go-on adds no word, so *Take back* is no longer its reverse (the owner's *Stop* is); the owner's queue edit then the steward's go-on refused, tested (§5.4; rows 1a, 1f) |
| C2-6 | Blocking: the steward's activation can activate the supervisor | As O2-2 | **Taken**: as O2-2; each role's activation independent, 1d's look proving the supervisor refused |
| C2-7 | Blocking: the go-on's `not-handed` recovery leaves the lifecycle unresolved | `DriverContinuations.cs:268-274`, `:795-819`; `SessionLedger.cs:1080-1100` restores a working record and takes back only supervisor words | **Taken, concretely**: the go-on keeps no word, so there is none to take back; a failure after the start's admission (reread, preparation, spawn) restores the record to `failed` `ended.transient`, the hold again, every word kept and an owner word arriving after the release left waiting; the service's restore extended to a `failed` record; a failed restore left for the orphan sweep; each failure tested (§4.3; row 1f) |
| C2-8 | Blocking: the printed line does not establish the configuration exclusion | `HolderTerminal.cs:27-29`; `HolderTerminalTests.cs:90` | **Taken, concretely**: at each admission the judge walks from the verb to the holder's tab shell through one harness process and reads its actual command line and `CLAUDE_CONFIG_DIR` from the operating system, and the directory's hash, refusing `holder-config` and ending the holder otherwise; the canaries at activation (§6.1; rows 1a, 1d) |
| C2-9 | The inventory's payloads and a symbol are wrong | `SessionsCommand.cs:440-441`, `:501`, `:536-538`; `ConversationRoutes.cs:104-116`; `SupervisorProof.cs:20` | **Taken**: §0.1's `said: true` on say and hand-over, say's verb path, `HolderKey.Variable` (§0.1, §0.2, §2.2) |
| C2-10 | Trees on does not move every later continuation | `DriverContinuations.cs:227`; `ConversationTrees.cs:11` | **Taken**: fresh sessions and new parts only; a continuation resumes in its recorded directory, said in §0.1 and row 3 |
| C2-11 | Closing a holder tab emits no exit event | `TerminalModule.cs:147-153`, `:270-274` | **Taken**: one internal `HolderEnded` callback on the page's close, the shell's exit, the module's disposal and an exit during the appointment's opening, each proved in 1a (§2.2) |
| C2-12 | `role-terminal` must use the captured entry state | `Program.cs:337`; `SupervisorProof.cs:50-57` | **Taken**: `HolderKey.RoleTerminal`, recorded by `Take` from the variable's presence whatever its value; tested through the real entry-to-dispatch path (§2.2, §3.3; row 1a) |

Round 3 changes the go-on's mechanism (from a word to AGENTERRORS1's release) and adds pending operations, the per-role flags, the census
of owner doors and the holder's configuration check: each touches who may act and whose later act wins, so round 3 goes to both readers
before the steward settles it. The go-on's design also rests on D210, which is proposed: if D210 changes its transient end or its release,
row 1f changes with it.

### 12.3 Round 3's read, its named changes taken

Round 3 was read at `3d2d446f8` by the Opus foundation reader alone (accept after named changes the steward checks without another read,
14 findings, 4 blocking), verbatim in `docs/2026-10-24-steward-role-reads.md`. Each change is taken as the read names it, checked against
the code on main at `ffcf42926` at the lines in the second column; the third says where it went. **O3-n** is the read's finding n.
Codex's read stopped when its credits ran out and is owed on this text: a blocking finding of it becomes an amendment, as D208 kept.

| # | Checked on main | Where it went |
|---|---|---|
| O3-1 Blocking: the shell and harness are programs any process of the account chooses | `TerminalModule.cs:238-249`; `Terminals.cs:7`, `:44-47`, `:90-96`; `Tools.cs:284-289`; `HolderTerminal.cs:27-29`; `Adapters.cs:2359`; `HarnessSettings.cs:855-875`; `HarnessRoster.cs:1234-1236` | §0.2's last point names how each resolves. §6.3's *Closed* now says it does not keep every branch's code from acting as the steward, and its *Not closed* and §5.3's ask name the shell's and harness's resolution and the inherited environment. §6.1: *1a-i's build choice is the read's option (a): Daoris starts the harness itself as the tab's process*, the pinned binary by path and hash with a composed environment, and wherever a tab is still a shell, the system's Windows PowerShell. Row 1a-i's `HolderHarnessTests` is its proof; D209's *Why* and point 7 corrected |
| O3-2 Blocking: a parked session's yes is taken by `GoAsync` as the agent's | `DriverHandOvers.cs:161`, `:221-243` (`HandOverWhy.Agent`, `HandOverBy.Agent`), `:106-108`, `:133`, `:300` | §5.1a's table names `SettleAsync` → `GoAsync` as row 10's consumer for a parked session, and its paragraph: *`GoAsync` takes the quest's gate, takes the steward's `approved` from memory … and writes its box `askedBy: steward`, `why: steward`, with the operation's id*. Row 1e gains the parked-then-stopped proof |
| O3-3 Blocking: the go-on lets an outage loop without the owner | `D210.md:57-60`, `:94-96`; the agent-errors design §5.1 (*The release is the person's*) | §4.2: *One go-on per quest per run* (`go-on-spent`); §4.3: *No briefed fallback and no walk*; §4.3a holds D210's amendment and its reason as the note D210's next round takes; row 11's condition, row 1f's two proofs; §9 and D209 point 5 |
| O3-4 Blocking: drive puts sessions in the owner's checkout | `DriverConfig.cs:1416-1424`, `:1423` (`OpensOwnTree`); `AutoLanding.cs:207` | §3.1 row 1 refuses `no-tree` where `!config.OpensOwnTree(repository)`, the steward giving trees first; rows 2 and 3 take it as written; §5.3's ask; row 1b's `RoleActTests` and 1d's look |
| O3-5 The walk's checks cannot pass | `Harnesses.cs:171-174`; `HolderTerminal.cs:38-42` | §6.1: *Daoris owns exactly these, and checks exactly these*, the harness's state left alone; the harness process defined by its image and its argument vector after the image; §2.2's line an argument vector with no shell (option (a)), so the quoting of Windows PowerShell 5.1 never applies; row 1a-i's walk proofs on the real directory's shape |
| O3-6 `owner-set`'s census marks too much and too little | `PipeCensus.cs:222-224`, `:270`, `:284`; `Asks.cs:200`; `SessionsRoutes.cs:22`; `ConversationRoutes.cs:356` | §5.4: the reason *acts on nothing a role acts on*, `SET_APPOINTMENT` `none` by it; the record kinds that mark, *exactly*, publication and the ask's own words excluded; *what orders them is the record, not a gate*; the sweep's cross-check against `[Publishes(…, Session = …)]` and the required fields; row 1a-ii |
| O3-7 The releases on disk carry no author | `StandingChoices.cs:118-119`; agent-errors design §5.1 (`WithTransientReleased`) | §4.3: the release is `WithTransientReleased(quest, session, ended)`, `by: steward` and the act's id; the record check D210's end key in the start's own step, never under the judge's lock; a restart withdraws only a release `by: steward`; §5.1a's table and last paragraph; row 1f |
| O3-8 Row 6's live stop reaches only this instance's processes | `SessionMoves.cs:22-31`; `SessionsCommand.cs:946-951` | §3.1 row 6: *parked, or live in a process this instance runs* (`running-elsewhere`, nothing written); row 1b's test |
| O3-9 `ActorChoices.Effective` is the machine's order | `ActorChoices.cs:101-102`; `Driver.cs:702`; `HandOverRules.cs:13` | §3.1 rows 9 and 10: *the machine's effective `build` order … within the fence a repository's hand-over rule passes where it has one* (none does today) |
| O3-10 Small claim slips | `AutoLanding.cs:171`, `:203`; no *Set by you* in `src/Daoris.Web/src`; `TASKS.md:267`, `:270` | `AutoLandingRules.Concluded` in row 6 and §12.2; §5.1's setting row *shows none today*; §6.1 *above every settings source but managed settings*; §0.2's `TASKS.md:267`, `:270` |
| O3-11 The desk's per-caller bound binds a verb | `StandingCaller.cs:47-53`; `D208.md:267-268` | §6.2: the bound keyed by *the agent job the caller look places the verb in … else by the nearest long-lived ancestor the walk finds*; §6.3; row 1c's proof from many verb processes |
| O3-12 1d's look leaves out the folder's ancestors and the sign-in | `Adapters.cs:2349-2350` | §6.1: *the owner signs in once in the tab*; §6.3's open limit names a parent's `CLAUDE.md`; row 1d's canary `CLAUDE.md` in `work/rooms/roles/` and at the home's root, and the sign-in in the look and the ask (§5.3) |
| O3-13 STEWARDROLE1a is round 1's size again | §10 | §10: *STEWARDROLE1a is two rows*, 1a-i (the holder, the variable and marker, the flags, the harness start, configuration and walk, `ROLE_ACT`, the memory) and 1a-ii (`owner-set`'s census and the gates), each with its own proof; 1b after both; `TASKS.md` split the same way |
| O3-14 D209's records against what landed since | `D156.md:276-287` | §9: D156 point 4 *read beside CONFIRMWAIT1's proposed note … a request the owner confirms hours later is a press at its Confirm … and marks its target `owner-set` then*; D209's status names the one reader and Codex's owed read |

### 12.4 Codex's read of the final text, its amendments taken

Codex's read of the accepted text and D209 was made at `16e0950e2` (stands after named amendments, 14 findings, 7 blocking; findings 1
to 7 before the rows they touch, 8 to 14 bounded corrections to their rows), verbatim in `docs/2026-10-24-steward-role-reads.md`. D209
stays accepted; these are amendments to it, its dated note of 2026-10-24 saying so. Each finding was checked against the code on main
at `7cda79f01`, at the lines in the second column, and held there; the third column says where it went. **C4-n** is the read's finding n.

| # | Checked on main | Where it went |
|---|---|---|
| C4-1 Blocking: hand-over bypasses the owner-only release bounds | `Planner.cs:1482`, `:1488-1495` (`TransientHolding` passes a quest handed from the held record), `:1516-1524` (`StopHolding` likewise); `HandOverQuest.cs:172-175` (only an open or taken quest) | §3.1b: rows 9 and 10 refused `owner-held` where the quest is held transient, by a stop not this run's untouched steward stop, or by another owner-only hold, at admission, under the gate and at every consumer; rows 9 and 10's conditions; row 1e's proof at admission and consumption |
| C4-2 Blocking: `OpensOwnTree` does not establish the checkout exclusion | `Driver.cs:2108-2110` (`resumedIn` takes a recorded directory whenever trees are on), `:2166`, `:2196`; `DriverContinuations.cs:227` (`park.Tree`) | §3.1a: the exclusion held at the actual start of every start a steward's act caused, its directory a tree the home opened, else held for the owner `held.checkout`, never relocated; row 1's condition and row 3's claim corrected; row 1b's proof with an earlier checkout session as a continuation and a new part |
| C4-3 Blocking: changing the carrier's actor can bypass memory authentication | `HandOverFiles.cs:88-99` (any known `askedBy`, `why`); `DriverHandOvers.cs:91-92`, `:133`; `Driver.cs:1907` (the start reads the handed file again) | §5.1a: *Memory, not the carrier, chooses the authority path*: each consumer reads memory first by target and stage; a carrier differing in actor, operation id, agent, account or note invalidated; the same for the ask's `approved`; row 1e's proof replacing and removing the marker between each pair of consumers |
| C4-4 Blocking: successive consumers cannot each consume one operation once | Design §5.1a as accepted (one *pending, not consumed* state for the look, `GoAsync`, the turn's end and the start) | §5.1a: stages per operation, each consumer taking its own once; a hand-over's last stage a live binding, ended by a revoke, the holder's end or the owner's mark; §3.4's revoke cancelling every unexecuted stage and binding; row 1a-i's memory and its stage proofs; row 1e's whole chain with a revoke between every stage and after the first start |
| C4-5 Blocking: the go-on's revocation interval after its memory check | Design §4.3 as accepted (memory check, lock released, record check, then consumed) | §4.3 and §5.1a: the execution cutoff, a second step under the judge's lock after the awaits, the operation still at its stage and live, moved to `executing`; row 1a-i's suspended-await proof; row 1f's revoke while the record check is suspended |
| C4-6 Blocking: the target gates do not cover the act's dependencies | `LinesRoutes.cs:188-201` (`SET_LANDING` by workspace alone); `AutoLanding.cs:203-207` (a `Done` park held only by being parked) | §5.4: the target resolution, a workspace resolved to its repositories at every door, each act's dependencies' gates in the fixed order workspace, repository, quest, its dependent conditions judged again under them; §3.4 step 8; row 1a-ii's landing race and workspace proofs |
| C4-7 Blocking: an older build reads a steward's transient release as the owner's | `DriverConfig.cs:1158-1173` (the reader takes `{ session, ended }`, ignores the rest), `:139-143` (`ReleasesTransient`); `StandingChoices.cs:118-125` (no author written) | §4.3 and §5.1a: the steward's stop and transient releases in a role-only `roleReleases` key no released map's reader reads, an older rewrite dropping it failing closed; rows 8 and 11's carriers; row 1b's proof with the older reader and writer beside an owner's release, row 1f's |
| C4-8 The first slice's retry proof omits its deferred authority | `StandingChoices.cs:118-119` | Row 1b's proof: owner takeover, revoke and restart between release and start, the authored storage and the final consumer's attribution |
| C4-9 The census sweep rejects an exemption it must accept | `SessionsRoutes.cs:1013-1016` (`SESSION_OPEN_FOLDER` requires `id`) | §5.4: naming a target told from mutating one, reviewed exemptions cross-checked against their handlers' effects, a mutating route never exempt by declaring it; row 1a-ii's proof |
| C4-10 The revoke text drops a shared holder's hash too early | `StandingConfirmation.cs:586`, `:597` (as built, a hash per appointment) | §2.2 and §3.4: one role revoked alone, the holder's hash dropped only with its last appointment; row 1a-i's proof revoking either role while the other goes on |
| C4-11 Holder folders are named by role | `HolderTerminal.cs:45-52` (`Folder(home, role)`, emptied recursively at each start) | §2.2: `work/rooms/roles/holders/<holder id>/`, never moved or emptied while the holder lives; §9; row 1a-i's proof with two holders of one role and a role added to a running holder |
| C4-12 The holder has no specified initial model input | `TerminalModule.cs:248` (`open.Say`, the owner's screen) | §2.2: the argument vector's last element, `HolderTerminal.InitialPrompt`, a fixed prompt to read `roles brief`, checked by the walk; row 1a-i; row 1d's proof of a desk request handled with no instruction typed |
| C4-13 The baseline inventory and inherited role coverage need rebasing | `TransientRetries.cs:25`; `Driver.cs:2794`; `StandingChoices.cs:121-125`; `D210.md:3`; `PipeCensus.cs:298`; `SessionsRoutes.cs:620`; `D208.md:273` | §0.1 and §0.2: dated *Since* notes beside the inventory as read; §6.1: the configuration walk by one guard for every `Role` route; row 1a-i's proof for `SUPERVISOR_DEFER` |
| C4-14 Finish's rationale promises a quest state its conditions do not require | `SessionMoves.cs:62` (advances the session alone); `AutoLanding.cs:207` | §3.1 row 7 and §5.4: the finish neither accepts nor closes its quest; an open or taken one stays so, a done one stays done; row 1b |

**What may be built.** With these amendments, **STEWARDROLE1a-i may be built as amended**: its own findings (4's stages and bindings in
memory, 10, 11, 12 and 13) are written into its scope and proof, and no finding asks another read before it. The later rows wait on
nothing new but their place in the order and what each now carries: **1a-ii** after 1a-i, with findings 6 and 9; **1b** after 1a-i and
1a-ii, with findings 2, 7, 8 and 14; **1c** after 1a-i, unchanged; **1d** after 1c and D201 part (i)'s case 4 on every door, with
finding 12's proof; **1e** after 1d, with findings 1, 3 and 4's proofs; **1f** after 1d, AGENTERRORS1a (landed) and **D210 carrying or
answering §4.3a's note**, which is still owed (`D210.md:65`), with findings 5 and 7; **1g** after 1d, unchanged. The first usable
slice stays 1a-i to 1d.
