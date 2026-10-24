# The steward: a role the owner appoints, making development's management acts on their behalf (STEWARDROLE1, 2026-10-24)

**Status: proposed (D209), round 2.** Round 1 was written by Claude Opus from the code at `d580caeb4` and read at `48f9b3cff` by Codex
(another round) and the independent Opus foundation reader (accept after named changes, 20 findings, 7 blocking), their reads verbatim in
`docs/2026-10-24-steward-role-reads.md`. Round 2 is written by Claude Opus 5.5 from the code on main at `f5c515f0b`, where SUPERVISOR1a-ii's
door is merged; §12 answers every finding of both reads. Under D191 (its amendment of 2026-10-23: the design changes a protocol between
processes, the pipe's census and its judge, and the decided rules D201 and D208 rest on).

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
| Give a repository trees | `driver trees <repo> on\|off` | `SET_TREES { repository, ownTree }` | Writes the standing choice a session's **start or continuation** reads (`DriverConfig.cs`, read at a start by `Driver.cs`); a running session is not moved. `off` makes the next starts work in the checkout (D51) | `DriverChoicesCommand.cs:77`; `DriverRoutes.cs` |
| Hold or resume a repository | `driver hold\|resume <repo>` | `SET_HOLD { repository, held }` | Writes the hold; a change made outside the standing confirmation forgets that confirmation's own hold | `DriverChoicesCommand.cs:76`; `DriverRoutes.cs` |
| Set a repository up | `setup <repository>` | `SETUP_PRESS { repository }` | Recomputes its plan in the handler (`SetupRoutes.cs:46`), adds `SetupPressRules.RulesOf(plan)` to the repository's allow list just before it saves (`SetupPress.cs:170-171`): for the set-up, nine exact doctrine verbs; for an index quest, four verbs and two commands **generated** by `IndexForms.For`, fixed `node`, `dotnet run` or `python` lines over a generator file in the repository (`IndexForms.cs:56-61`); then publishes one ask **as the person's** | `SetupRoutes.cs`; `SetupPress.cs`; `SetupPressRules.cs` |
| Words to a session; a go-on after an outage | `sessions say <id> "…"` | `SESSION_INPUT { id, text, level?, paths? }` | Keeps the words on the session's record with no author, so they are the person's (`SaidWord.Persons`); to a running session through the screen's door, and then kept on its ask by `KeepOnAsk` (`ConversationRoutes.cs:146`) | `ConversationRoutes.cs`; `Sessions.cs` (service) |
| Hand a session over | `sessions hand-over <id> --agent <a>` | `SESSION_HAND_OVER { id, agent, account? }` | For a quest's session, a box in the home the next look takes (`HandOverQuest.cs:101-109`), stopping it and carrying its quest on in the same tree with the agent named; the terminal's path records `HandOverAsker.Terminal` (`SessionsCommand.cs:1683`), the window's `HandOverAsker.Window` | `ConversationRoutes.cs`; `HandOverQuest.cs`; `DriverHandOvers.cs` |
| Answer a session's ask to hand over | `sessions hand-over <id> --approve\|--decline` | `SESSION_HAND_OVER_APPROVE { session, ask?, said? }`, `_DECLINE` the same | The yes or no to an agent's own `handover_propose`; with no `ask`, the one current in the files (`SessionsRoutes.cs:405`); a conversation's taken at once | `SessionsRoutes.cs:396-445`; `SessionsCommand.cs:444` |
| Stop a session | `sessions stop <id>` | `STOP_SESSION { id, said: true }` | Stops it; the record concludes `stopped` with the note *the person stopped it* (`Observation.cs:374`) or, parked, *The person stopped this at a checkpoint* (`SessionMoves.cs:81`); its quest held until *Try again* (`Planner.cs:259`) | `SessionsRoutes.cs`; `SessionMoves.cs` |
| Finish a session | `sessions finish <id>` | `RESOLVE_SESSION { id, state: "completed", said: true }` | Resolves a parked session with *The person finished this at a checkpoint* (`SessionMoves.cs:80`); a helper's words reach its asker, a workflow node waits, the person's words waiting carry it on, a driven quest otherwise sits for the owner's done (the terminal-trust design §2.1's `finish` row) | `SessionsRoutes.cs`; `SessionMoves.cs` |
| Try again | `driver retry <quest>` | `RETRY_QUEST { quest }` | Releases a quest a stop held | `DriverChoicesCommand.cs:82`; `DriverRoutes.cs:413` |
| Close a quest as done | `quest done <id>` | none: the service's own door, acting alone | TERMTRUST1b moves it onto the pipe as `QUEST_DONE`, not built. It is the owner's acceptance and stays theirs (§3.2) | `TASKS.md`'s TERMTRUST1b |

The go-on after an outage is `sessions say` with a few words to a session whose turn an API error ended and which then waited on the
person. AGENTERRORS1 will retry a transient error by itself, so most of these go-ons leave the owner and the steward both; what remains (an
error past the bound, a kind the policy leaves waiting) is the steward's go-on here (§4).

### 0.2 The door SUPERVISOR1a-ii built, on main

D208 is accepted. SUPERVISOR1a-i is landing in parts and SUPERVISOR1a-ii, the door, is built dark and merged (D208's note of 2026-10-24):

- **The record**: `appointments` in D201's `data/standing-confirmation.json`, each `{ role, repository, holder, generation, since }`, never
  the key nor its hash (`StandingConfirmation.cs`'s writer).
- **A key per appointment**: `AppointAsync` draws a key for each appointment, opens its holder's terminal with it, and keeps only its hash in
  memory by generation (`_hashes`); a replaced or dismissed appointment's hash is forgotten in the same step under the judge's lock, then
  swept (`SupervisorDoor.AppointAsync`). An appointment read at a start has no hash: dormant until *Appoint again*, which opens the same ask.
- **The variable `DAORIS_SUPERVISOR_KEY`** (`SupervisorProof.Variable`), removed as the first statement of every Daoris executable
  (`Launcher`, `GitDoor`, `PieceStart`, the service's HTTP and MCP hosts, the driver's host) and from every start (`WiringVariables.cs:103`,
  `GitWiring.Removed`), checked by `HolderKeyStripTests`.
- **The holder's terminal is a tab of Daoris's own terminal panel** (`TerminalModule.OpenHolderAsync`, the owner's direction of
  2026-10-24), the owner's shell with `-NoProfile`, in the role's emptied folder `work/rooms/roles/<role>/` (`HolderTerminal.Folder`,
  `HomeEntry.RoleRooms`), the key in that one shell's environment block alone, titled *Daoris: the supervisor's terminal*, its first lines
  printing the session's start line, `HolderTerminal.SessionLine`, which allows `Read`, `Grep`, `Glob` and
  `Bash(daoris-driver sessions supervise:*)` alone. Its shell's exit is told as `TERMINAL_EXITED`; the judge is not told of it.
- **The census and the judge**: `PipeClass.Role` with `SUPERVISOR_ANSWER`; the judge's role method before the open path and any card,
  under the judge's lock and D201's caller look, then the form, the generation and an HMAC proof over the hello's nonce; a covered act runs
  under `PipeDoor.Role` (`RefusalLog.cs:104`, a `RoleJudgement { Role, Repository, Generation }`). `SET_APPOINTMENT { role, repository,
  holder, on }` is `Window`; `SUPERVISOR_WAITING` is `Open`, told by event under NOBLOCK1's rule, its answer naming the moment it was
  computed.
- **The flags**: `AppointmentsOffered` false, and `AppointsOffered = Offered && …` (`StandingConfirmation.cs:257`), so the door waits for
  D201's own offering.

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
press, and an owner's press on a repository or a quest makes it theirs for the rest of the run, whatever route either takes. It never makes
what is the owner's, closing a quest as done included (§3.2). **One appointment model**: SUPERVISOR1a-ii's door as built, amended so a
second role may share the run's holder or have its own, and D201's four presses become the steward's acts rather than a separate grant (§2).
Its **words** are, for now, one fixed go-on whose text is Daoris's own, to a quest's session waiting on nobody but the owner, kept only
through the role's door and never on the ask (§4); free words are a later row with their own read. The **holder** is the dedicated session,
now the steward's management desk: it reads through Daoris's open routes and makes the role's acts, and runs no branch's code. The
steward's **working session**, which builds, tests and merges, holds no key and no role: its presses ask as today, and its management needs
reach the holder through the desk (§6).

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
- **A holder ends with its last role or its shell.** Revoking a holder's last role, or `TERMINAL_EXITED` for its tab, drops its hash and
  every appointment it holds in one mutation step, swept after it; the tab, where it still lives, prints *the roles this terminal held are
  turned off* and the next *Appoint…* draws a new key and a new tab. The judge is told of the tab's exit by the terminal module; until this
  row the built door is not (§0.2).
- **The variable is `DAORIS_ROLE_KEY`**, renamed from `DAORIS_SUPERVISOR_KEY` at every site the build names (§0.2's list, `HolderKey`'s
  and `SupervisorProof.Variable`, `WiringVariables`, `HolderKeyStripTests`'s pattern), since a shared holder's key is no one role's. No
  install has held the old one: `AppointmentsOffered` was never on.
- **The folder**: a shared holder's is `work/rooms/roles/holder/`, a holder of one role keeps `work/rooms/roles/<role>/` as built; each
  emptied at its start (`HolderTerminal.Emptied`). The tab's title names its roles (*Daoris: the steward's and supervisor's terminal*).
- **The start line allows every role's verbs**, whichever role the tab was opened for: `Bash(daoris-driver sessions supervise:*)` and
  `Bash(daoris-driver steward:*)` beside the read tools. The permission is not the authority: a verb of a role this holder does not hold is
  refused `not-appointed` by the judge, so adding a role to a live holder needs no change to its session's permissions or its restart.
- **The holder's brief is Daoris's own, printed by an open verb**: `daoris-driver roles brief` prints, for each role the build knows, what
  it does, what it never does and how its verbs answer, from text in Daoris's code, naming no key and no appointment. The tab's first lines
  tell the session to read it at its start and again when a role is added, which the tab prints as a line (*the steward was added to this
  terminal at 14:05; read `daoris-driver roles brief` again*). The holder loads no instruction file, so this is where its brief comes from.

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
- **The activation's prerequisite is the containment's look, not the grant.** `AppointsOffered = Offered && …`
  (`StandingConfirmation.cs:257`) becomes `AppointmentsOffered` alone, its turning on waiting for D201 part (i)'s case 4 seen on every door,
  which is what D201's offering itself waited for (D201's note on order).
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
| 1 | Drive | `steward drive <repo>` | `SET_DRIVABLE { repository, drivable: true }` | The repository in the service's registry on this machine (`unregistered`); not the owner's this run (`owner-set`, §5.4); its landing rule not a plugin that pushes (`landing-pushes`, `SetupPressRules.LandsAs`'s plugin) | *Set by the steward* (§5.1) | Starts work only through Daoris's own sessions, on the accounts and within the cap the owner set (`SET_CAP`, never the steward's): the development's own runs (D191 point 3). Undone by the owner's *Stop driving*. A repository whose landing pushes is the owner's to drive, since its every landing publishes |
| 2 | Stop driving | `steward undrive <repo>` | `SET_DRIVABLE { repository, drivable: false }` | As 1, but `landing-pushes` | As 1 | Only narrows: no session is started; running ones go on as an undrive leaves them today |
| 3 | Give trees | `steward trees <repo> on` | `SET_TREES { repository, ownTree: true }` | As 2 | As 1 | Only narrows: each session **started or continued** after it works in a tree of its own. **`off` is never the steward's**: later starts would work in the owner's checkout, where their uncommitted work lies (D51) |
| 4 | Hold | `steward hold <repo>` | D201's `hold`, `SET_HOLD { repository, held: true }` | As 2 | As 1 | Only narrows (D201 §2.1) |
| 5 | Resume | `steward resume <repo>` | D201's `resume` | As 2; a hold the steward made this run that nothing has touched since (`not-own`) | As 1 | Undoes its own act and nothing of the owner's |
| 6 | Stop | `steward stop <id>` | D201's `stop`, `STOP_SESSION { id, said: true }`, the verb's path | A quest's own session; its quest not the owner's this run | `ended.stopped-steward`, or parked `stopped.checkpoint-stopped-steward` (§5.1) | Only ends work; the tree is kept; the steward's own *Try again* (row 8) or the owner's carries it on |
| 7 | Finish | `steward finish <id>` | D201's `finish`, `RESOLVE_SESSION { id, state: "completed", said: true }` | D201's: no process of it here (`running-here`), moved only from `awaiting-person` in the ledger's step, never through the request folder; a quest's own session, **not a helper's, not a workflow node's run** (`not-quest-session`); **no word waiting on it** (`words-waiting`); its quest not the owner's | `stopped.checkpoint-finished-steward` | A session already waiting with nothing running, nothing said to it and nobody but its quest after it: what the finish ends is that session alone, and its quest stays open or taken for the owner's done or acceptance (§3.2). `decline` is never the steward's |
| 8 | Try again | `steward retry <quest>` | `RETRY_QUEST { quest }` | The quest's last session stopped **by the steward** this run (`ended.stopped-steward` or the checkpoint's, and this run's memory of the act), nothing touched since (`not-own`); not the owner's | `retried` by the steward | Undoes its own stop, as `resume` its own hold; a stop the owner made stays theirs (D201 point 3) |
| 9 | Hand over | `steward hand-over <id> --agent <a>` | `SESSION_HAND_OVER { id, agent, said: true }`, **no `account`**, the terminal's path | A quest's own session; `agent` one this machine drives (`SessionsCommand.DrivesBy`); its quest not the owner's; no box waiting for it (`HandOver.Waiting`) | Box and handed file `askedBy: steward`, `why: steward`, matched against memory (§5.1) | Carries the quest on in its own tree with another agent the owner configured; the account is Daoris's pick by the owner's rules, never named by the steward. The owner hands back with their own press. After 1e (§10) |
| 10 | Answer a hand-over ask | `steward hand-over <id> --approve\|--decline --ask <ask>` | `SESSION_HAND_OVER_APPROVE { session, ask }`, `_DECLINE { session, ask }`, no `said` | A quest's own session; `ask` the one open in its files **at application**, and its agent then the agent the holder's listing showed (`ask-changed` otherwise, judged under the target's gate, §5.4); its quest not the owner's | As 9 for the yes; `handover.refused` by the steward for the no | As 9 for the yes; the no only keeps the session where it is. After 1e |
| 11 | The go-on | `steward go-on <id>` | `ROLE_ACT`'s own handler (§4), no route of the owner's | §4.2's | `From: steward`, its text Daoris's (§4) | The go-on after an outage, which grants nothing and says nothing but *go on*. After 1f |
| 12 | Set up | `steward setup <repo>` | `SETUP_PRESS { repository }`, the set-up form only | The appointment's `setup` tick (`not-granted`); held again **inside the application**, immediately before the rules are saved, against the plan being applied: the set-up's plan, never an index quest's, its rules exactly `SetupPressRules.Rules` (`plan-changed` otherwise, nothing saved, nothing published) | The ask `by: steward` (§5.1) | The rule is nine exact doctrine verbs Daoris names in its own code (D124 §2.4); adding it is the one widening in this table, so it is the owner's: granted by their own tick at the appointment. An index quest's two commands run the repository's own generator code, so they never are. After 1g |

**Every act is the steward's by its own decision.** No act is a card, a proposal or a request to the owner; the holder decides, Daoris
judges the form and the bounds, the act runs. What the owner sees is the record (§5).

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
- **A role's terminal never acts as the owner nor alone.** Where `DAORIS_ROLE_KEY` is set, every verb that would send a `Person` route
  refuses `role-terminal` before it connects (*this terminal holds a role: act as the role, or press from another terminal*), and every
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
6. The conditions, each by its code (`unregistered`, `landing-pushes`, `owner-set`, `not-own`, `running-here`, `not-quest-session`,
   `words-waiting`, `question-open`, `go-ahead-open`, `answers-held`, `ask-changed`, …), judged under the judge's lock where they read its
   memory, and in the store's own step where they read a record (D201's finish).
7. Admitted once, under the judge's lock, with the act's id drawn; the lock released; the act takes its **target's gate** (§5.4), checks
   `owner-set` again under it, runs its handler, records the act (§5), and releases the gate. The judge's lock is never held across an
   await.

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

### 4.2 Its conditions

Judged at admission and again in the service's keeping step, each by its code:

- **A quest's own session, ended and waiting**: the record `awaiting-person` with no process for it here (`running-here`), so the go-on is
  never said to a running session, never through the screen's door or the inbox, and never kept on the ask.
- **Waiting on nobody but the owner's go-on**: no question recorded open on it (`question-open`; a question is answered by the owner or
  the supervisor, D208); no go-ahead open on its ask (`go-ahead-open`); **no word waiting on it of any author** (`words-waiting`), so the
  owner's own words, a helper's answer or a supervisor's decision waiting there are never joined or overtaken; no answers-alone hold on it,
  marked or due (`answers-held`, read by `AnswersAloneHold`'s own predicate); no go-on mark of the owner's *not now* or a quiet leave on it.
- **Its quest not the owner's this run** (`owner-set`, §5.4).

### 4.3 Kept, handed and taken back: fail-closed

The go-on is a word `From: steward`, a fourth value of `SaidFrom` beside `daoris`, `agent` and `supervisor` (`Sessions.cs`, service), on SUPERVISOR1a-i's lifecycle for a role's word:

- **Kept only by the service's conditional step**, through a say door that takes `from: steward` with the act's id and a code, `go-on`,
  never a text: the service composes the text from the code, and keeps it only where the record is still `awaiting-person`, no question,
  go-ahead or word waits, in the same transaction. A refusal or a failure of that step refuses the act (`unwritten`); **nothing is held in
  the process** (no `KeepOrHoldAsync` fallback), so there is never an inbox copy a take-back misses.
- **Attributed from memory**: the driver's memory records the act's id, the generation and the composed text; a record's word claiming
  `steward` whose id or text this run's memory does not hold is **quarantined**: never handed, taken back at the next look, shown *Not
  attributed* (`SupervisorQuarantine`, extended by a role). The paragraph handed is composed from memory, never from the record's text.
- **Handed by ticket**: at the go-on's start, the look checks again that the appointment's generation is live, no question, go-ahead or
  other word has appeared since the keep, and the quest is not the owner's; then admits the start by ticket as SUPERVISOR1a-i does
  (`went-on` at the start, `not-handed` restoring nothing, since the go-on is withdrawn instead of restored). A wait that appeared after the
  keep, a revoke or an owner's press withdraws it by the same conditional step, said on the record.
- **Never on the ask**: the say and taken doors keep only `Persons` words on the ask (`Program.cs:1504`, `:1554`), and the go-on never
  takes the added door, since it is never said to a running session. A test holds that it is absent from `AskWords`.
- **Neutral to the answers-alone hold and the go-on marks**: `AnswersAlone.Holds`, `AnswersAloneHold`'s candidate look and
  `GoOnMarks.Judged` read the waiting words less the steward's, so a steward's word neither counts, resets, lifts a hold nor defeats a mark;
  a word arriving beside it after the keep withdraws it (above). After a restart nothing is live: a steward's word still waiting is taken
  back at the first look and said.

### 4.4 What the session is handed

> The steward, who manages this development for the owner, asks you to go on with your quest where you left off. These are Daoris's words,
> not the owner's, and they grant no go-ahead, sign-in, access, push or publication, spending or deletion. The owner's own words, here or
> on your ask, come first.

Every carrier and reader of a word's author gives `steward` its case beside `supervisor` (the supervisor design §6.1's sweep test, which
finds them by search): the working note and the planner's go-on sentence never say *you answered* for it; *What needs you* is not cleared by
it; the D79 resume carries it as the steward's; `line_read` marks it `steward`.

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
| A setting (drive, trees, hold) | *Set by you* | *Set by the steward at 14:02*, from this run's memory | Setup → Driving's row |
| The go-on | the person's words, no author | `From: steward` (§4.3) | §4.4's readers |
| The set-up's ask | the person's | `by: steward` (§4.5) | `AskWords.Beneath` |

D201's `standing.terminal` part is not written for the steward's acts. The behaviour a code carries (a stop held for *Try again*, a finish
a done may follow) is the same for the steward's sibling, so no behaviour changes but who is named.

**A mark is a claim; this run's memory is the authority.** A note code, a box's `askedBy` and a handed file's are within an agent's ordinary
reach. So the page draws *By the steward, on your behalf* live, with its reverse press, only where this run's memory recorded the act's id;
a record carrying the steward's code that the memory does not hold is drawn as history (*recorded as the steward's*), with no press and no
standing. **The hand-over's files are authenticated against memory**: an admitted hand-over keeps in memory its act's id, session, quest,
agent, generation and moment; the look consumes a box or a handed file whose `askedBy` is `steward` only where memory holds that exact
operation, and otherwise clears it, logs `role.unattributed` and says it on the session's record, so a planted or altered file never
becomes a steward's act. A restart forgets the memory: a steward's box found at a start is cleared and said; a handed file already
consumed is history. A revoke clears the steward's boxes not yet taken (`HandOverQuest.cs:101-109`), as it withdraws a waiting go-on.

### 5.2 In the machine log

`role.act` for each admitted act (`role`, `act`, `generation`, `target`: a session id, a quest or a repository's name, `outcome`),
`role.refused` for each refusal (`role`, `act`, `why`), `role.unattributed` for a claim memory did not admit, `role.appointment` for an
appointment, a revoke, a holder's end and what a start found: codes only, beside D201's `person.*` family, which keeps its own.

### 5.3 What the owner sees

- **Settings → This Daoris → *Roles*** (D201's *A terminal* row, renamed): *Steward*, its state, *Appoint…* or *Appoint again*, and **Turn
  off**. *Appoint…* opens the inline ask: what it does (§3.1 in one line each, the offered acts only), what it never does (§3.2's six in a
  line each, closing a quest as done named), that it drives only repositories whose landing does not push and that a repository the owner
  left undriven in an earlier run is not theirs this run unless they press on it, the tick *and set repositories up: adds nine exact
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

- **Revoked at once**: **Turn off** on the row or the line, no ask, D208's two steps (the mutation, then the sweep: the run's waiting
  go-ons withdrawn, the steward's untaken hand-over boxes cleared). An act already past its gate is not recalled, as neither D201's nor
  D208's revoke recalls one.
- **Reversed by the owner's own press**, which the line offers beside each act that has one, each an act that really exists today: *Stop
  driving* for a drive, *Drive* for an undrive, *Trees off* for trees on, *Resume* for a hold, *Hold* for a resume, *Try again* for a stop,
  *Stop* for a steward's *Try again*, *Hand back…* (the owner's own hand-over) for a hand-over, *Take back* for a waiting go-on. A finish
  has none: its session was waiting with nothing said and nothing running, and its quest stays open for the owner's done or acceptance.
  Each is the owner's ordinary press, recorded as theirs.
- **The owner's press makes the thing theirs for the run, whatever the route.** `owner-set` is keyed by **the thing acted on**: the
  repository for rows 1 to 5 and 12, the **quest** for rows 6 to 11. Any owner press on that repository's settings (`SET_DRIVABLE`,
  `SET_TREES`, `SET_HOLD`, `SETUP_PRESS`, `SET_LANDING`), or on any session of that quest or the quest itself (`STOP_SESSION`,
  `RESOLVE_SESSION`, `SESSION_INPUT`, `RETRY_QUEST`, `SESSION_HAND_OVER` and its answers, `SESSION_ANSWER_ALONE`, the quiet presses, a
  landing, `quest done`, *Reopen*), from the window or a confirmed card, marks it; then every steward act on that repository or quest is
  refused `owner-set` until Daoris restarts. A quest's successor sessions (a retry, a hand-over, a carry-on, a new part) share its quest, so
  the mark follows them. The steward can then neither stop again after the owner's go-on, nor hand over a session the owner stopped, nor
  go on after the owner's own words.
- **Ordered against the owner's press, by the target's gate.** The run's memory holds a gate per repository and per quest. A steward act
  takes its target's gate after admission, checks `owner-set` under it, runs its handler and releases it; an owner's press on a marked
  route takes the same gate before its handler and sets the mark inside it. So if the owner presses while a steward's act is admitted and
  waiting, either the steward's act runs first and the owner's press then runs over it, or the owner's mark is set first and the steward's
  act is refused `owner-set`: never a steward's write after the owner's. The gate is the run's memory's own (a semaphore per target, never
  the judge's lock), held only across the handler; a press with no target takes none.

### 5.5 Why no overrule window

D208 holds the supervisor's decision five minutes before it is a word, because an answer to a question is the one act a session follows
at once and cannot be taken back. The steward's acts differ: each of rows 1 to 10 only narrows, is the development's own run the owner set
up, or is undone by one press of the owner's that exists today (§5.4); the go-on waits on the record until the look and is withdrawn until
then. A hold on every act would make the go-on after an outage wait five minutes for nothing, and the owner's frame is the role acting by
its own decision. The set-up, the one widening, is the owner's own tick, and closing a quest is not the steward's.

## 6. The holder, and the steward's working session

### 6.1 The holder: the steward's management desk

The holder is the dedicated session of SUPERVISOR1a-ii, in its tab of Daoris's terminal panel, unchanged but for what it may run:

- started in its tab's folder (§2.2), outside every repository, by the tab's printed start line: no project or local settings, an empty MCP
  configuration, no hooks, plugins or instruction files (`HolderTerminal.SessionLine`'s flags);
- its permissions **only read tools and the install's own `daoris-driver` in its roles' verbs**: `sessions supervise`, `steward`, `roles
  brief`, and reads the instance answers over an open route (`sessions`, `notices`, `history`). **Every verb's alone path refuses in its
  terminal** (§3.3), so a plan verb or a git read the instance does not answer runs nothing there;
- it reads repositories' files **by path** with the read tools (the backlog, a decision, a session's log), never by starting in one;
- it builds, tests, merges and dispatches nothing, and runs no git, no build, no test and no hook. **Driving a repository is not
  dispatching**: the sessions a drive starts are Daoris's own, started by its driver with the key stripped and contained in its agents'
  job (D201 part (i)), never this process's children.

### 6.2 What the steward's working session may hold, and the desk

The steward's working session plans, files rows, dispatches builders, merges and runs the gates: it runs other branches' code (§0.4). So:

- **No key, and no role.** It is never started in a holder's tab, and nothing gives it `DAORIS_ROLE_KEY`; a verb it runs sends no proof.
- **Its presses ask as today**: a `Person` route waits for the window's Confirm and, confirmed, is the owner's press, recorded as theirs.
- **Its management goes through the desk**, which lands before activation (§10), so the owner's cards fall for the working session's needs
  from the first day: `STEWARD_REQUEST { act, target, why }`, an `Open` route any caller may use, kept in the run's memory (at most 100 open;
  another is refused `desk-full`), listed to the holder by `steward desk`, fenced and labelled as claims (D208 §4.4: what the holder reads is
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
  claiming the steward is a claim memory refuses (§5.1). An agent cannot make itself the steward.
- **Not closed, said as D208 §4.6 says it**: a process of the same account that reads another process's memory or environment, or injects
  into the holder or the instance (D156 point 7, PERSONDOOR2); a holder session talked into an act by what it reads (its acts are bounded
  by §3, recorded and overruled); a holder that runs something it should not. **A shared holder widens what the supervisor's inputs
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
appointment, shared or a role's own; `DAORIS_ROLE_KEY`; `work/rooms/roles/holder/` for a shared holder; a holder ending with its last role
or its tab's shell; the start line allowing every role's verb and `roles brief`; *Appoint again* one role at a time from the window's ask,
the set-up tick off; an appointment's `scope`, `acts` and `setup`; activation's prerequisite the containment's look, not D201's offering)
and point 7 (another author, `steward`, in every reader's case); **D201** points 1, 2 and 5 (its four presses the steward's acts; the grant
not offered alone; its row *Roles*; no `standing.terminal` part on the steward's acts); **D156** point 4 (a terminal's press of the
person's waits for the window, unless a role's appointment covers it by the key, recorded as the role's); **D188** (`SaidFrom.Steward`,
neutral to COWORK1e's count and its hold); **the move notes and hand-over reasons** (the steward's own codes); **the ask's record**
(`by: steward`) and DRIFT1b's quoting (`AskWords`); **the hand-over's asker** and its files' attribution; **the census** (`ROLE_ACT`,
`STEWARD_REQUEST`); **the glossary**.

**These stand**: D208's foundation (appointed only in the window, recorded as the role's, the key never shown or sent, the dedicated
holder, the appointment lasting until a restart, the supervisor's decision lifecycle and its overrule window); D201's judge, record, lock,
forms and containment; TERMTRUST1b as D201's and the owner's; D156 points 1 to 3 and 5 to 7; D191 point 3; D193's rejection of a
hierarchy and part 1's acceptance by the owner; D135 point 2; D51; D124 §2.4.

## 10. Proof, and the rows in build order

Each row lands alone with its proof, each test by class, filtered. Nothing reaches the owner until activation (1d). **The flags**:
`AppointmentsOffered` (built) gates `SET_APPOINTMENT` for every role, and no longer waits on D201's `Offered`; `StewardOffered` gates the
steward's role (its *Roles* row, its ask, `ROLE_ACT` for `steward`), never true while `AppointmentsOffered` is false; `StewardActs.Offered`,
a set in code, names the acts this build offers, each later row adding its own act behind its own look, and each appointment keeping the
set its ask named (§2.3). **The first usable slice is 1a to 1d**: drive, undrive, trees on, hold and resume, stop, finish and *Try again*,
with the desk; the hand-over's authenticated files, the go-on and the set-up follow, none on that slice's path. No ranking of which acts
remove the most cards is claimed: the owner's cards of 2026-10-24 named drives, a setup, go-ons and finishes, and 1d's look counts them.

**STEWARDROLE1a — the door's amendments** (modules, driver, web; after D209's acceptance; lands dark). Against SUPERVISOR1a-ii as built:
the holder apart from its appointment (hash by holder, `RoleJudgement`'s `Holder` and `Act`), shared or its own at a later *Appoint…*; a
revoked role only to a new holder; the holder ending with its last role or its tab's `TERMINAL_EXITED`, swept; `DAORIS_ROLE_KEY` at every
site; `work/rooms/roles/holder/`; the tab's title and the start line with every role's verb; `roles brief`; `SET_APPOINTMENT`'s `scope`,
`share`, `setup` and the record's `scope`, `acts`, `setup` (the built `repository` read as a scope); *Appoint again* per role, the tick off;
`AppointsOffered` decoupled from D201's `Offered`; `ROLE_ACT` (`Role`) with the judge's order of §3.4 and an empty steward table;
`PipeDoor.Role` carrying the act's id; the run's memory of `owner-set` by repository and quest and the target's gates; `role-terminal` on
every press verb and every alone path; the log lines; `StewardOffered` false. **Proof**: `StandingConfirmationTests` (a second role shared
and its own; a revoked role refused to the old holder; the holder's end by its last role and by its tab's exit; a planted steward record
dormant, *Appoint again* naming the build's acts with the tick off, never the file's; the built supervisor record read as its scope),
`PipeStandingTests` (a `ROLE_ACT` with no proof, a wrong key, a holder's key for a role it does not hold, a replay, each refused; the act
outside `acts` refused `not-granted`), `HolderTerminalTests` (the start line, the folder, the title; a stand-in terminal factory, never a
shell or a window), `HolderKeyStripTests` (the new variable at every entry), `PipeCensusTests`, `SessionsCommandTests` (`role-terminal` on a
press verb and on an alone path with the key set), `OwnerSetTests` (a mark by repository and by quest from each route §5.4 names; a
quest's successor session covered; the gate's two orders, a steward act admitted then an owner's press, and the reverse, with no write of
the steward's after the owner's and the judge's lock never held across an await).

**STEWARDROLE1b — the steward's immediate acts and their records** (modules, driver, web; after 1a; dark). §3.1 rows 1 to 8 in their
forms, paths and conditions, D201's `resume` memory moved to the role; the steward's own move notes and every reader by code (§5.1's
table); *Set by the steward*; history against memory; *Roles* with its ask; the *Notifications* line with its reverse presses; *Stopped by
the steward* in *What needs you*; the glossary. **Proof**: `RoleActTests` (each act admitted in its exact form; another field or value
refused `form`, a field name's casing read as D201's, a name twice refused; every §3.2 route, `QUEST_DONE` among them, refused `not-role`;
`trees off`, `decline` and a retry of the owner's stop refused; an unregistered repository and one whose landing pushes refused; a finish
of a helper's, a workflow node's or a session with a word waiting refused), the notes' sweep test (no record of a steward's act, stored
English or translated en and zh, nor a resumed brief nor the planner's reason, reads *person* or *you*; every reader of the person's codes
has the steward's case), `PlannerTests` (a steward's stop held with its own reason, released by the steward's *Try again* and by the
owner's), the row's and the line's page tests in en and zh, both themes.

**STEWARDROLE1c — the desk** (modules, driver; after 1b). `STEWARD_REQUEST`, `steward desk`, the fenced print, its bound, its closing by act
or reasoned decline, its line in *Notifications*. **Proof**: `StewardDeskTests` (any caller files; nothing is granted; a request never
admits an act nor reaches a handler; the bound; closing by the act that names it).

**STEWARDROLE1d — activation** (modules; after 1c and D201 part (i)'s §8 case 4 seen on every door). A candidate build with
`AppointmentsOffered` and `StewardOffered` on, installed and looked at before the change turning them on lands, carrying SUPERVISOR1a-iii's
installed checks: the holder's tab and the exact command line it ran, the configuration it loaded (no project or local settings, no
project MCP process, no hook), the verb against the install's own `app/Daoris.Desktop.exe`; then the owner appoints the steward; its holder
drives and undrives a repository, gives it trees, holds and resumes it, stops a session and tries it again, finishes a parked one, each
with no card, each said on its record and in *Notifications*, read in en and zh with no *person* or *you*; the owner's *Stop driving* after
a steward's drive, and the steward's drive then refused `owner-set`; the owner's go-on after a steward's stop, and the steward's stop then
refused; a request filed at the desk by the working session and answered by an act; a press from the working session waiting for the card;
a caller with no key refused; **Turn off**, and the holder's next act refused; a restart, the appointment dormant, *Appoint again* with the
tick off; the holder's tab closed, and the next *Appoint…* opening a new one. The `dispatch-subagent` skill gains the working session's
rule. **Proof**: the look's record in the decision's note, with the count of cards the owner did not press.

**STEWARDROLE1e — the hand-over and its answers** (driver, modules, web; after 1d; offered with its own look). §3.1 rows 9 and 10, the
files authenticated against memory, the revoke's clearing, the restart's, `HandOverBy.Steward` and its readers. **Proof**: `RoleActTests`
(a hand-over with `account` refused; an approve without `ask`, of an owner's conversation, or with the ask or its agent changed between
judgement and application refused `ask-changed`), `DriverHandOversTests` (a planted box and an altered one claiming the steward cleared and
said, never consumed; a steward's box cleared by a revoke and at a start; an owner's stop then a steward's hand-over refused `owner-set`).

**STEWARDROLE1f — the go-on** (service, driver, modules, web; after 1d; offered with its own look). §4: `SaidFrom.Steward`, the service's
conditional keep by code, attribution and the paragraph from memory, the ticket at the start, the withdrawal, every reader's case by the
sweep. **Proof**: `RoleGoOnTests` (kept only through the door; one claiming `steward` from the service's say, or with another text,
quarantined and never handed; refused on a running session, an open question, an open go-ahead, a word of any author waiting, a hold;
a keep that fails refuses the act and holds nothing in the process; a question, a go-ahead or a word appearing after the keep, a revoke,
and an owner's press each withdrawing it before the start; absent from `AskWords`), `AnswersAloneTests` and `GoOnMarksTests` (a steward's
word beside held answers neither lifts the hold nor defeats a mark, neither counts nor resets), the sweep test, `ConversationView.test.tsx`;
the look: the steward's go-on to a session an error left waiting goes on with no card and reads as the steward's.

**STEWARDROLE1g — the set-up** (service, driver, modules, web; after 1f). The appointment's `setup` tick in the ask, row 12, the check inside
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
- **The steward's words counted in COWORK1e's count**: they answer nothing; counted, the third go-on after an outage would wait on the owner.
  Neutral instead, and never beside another waiting word (§4.2-§4.3).
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

Round 1 was read at `48f9b3cff` by Codex (another round) and by the independent Opus 5.5 foundation reader (accept after named changes,
20 findings, 7 blocking), verbatim in `docs/2026-10-24-steward-role-reads.md`. Each finding was checked against the code on main at
`f5c515f0b`; *taken* means round 2 makes the change, *answered* that it holds otherwise for the reason given, *deferred* that it waits with
its reason. **C** is Codex's read, **O** the Opus foundation read.

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
The changes of round 2 touch authority and execution (the holder's sharing and end, `owner-set`'s key and gate, the hand-over's files, the
go-on's lifecycle), so Codex's verdict of another round stands for them: round 2 goes to both readers before the steward settles it.
