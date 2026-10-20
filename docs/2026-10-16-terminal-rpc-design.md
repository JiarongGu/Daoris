# The running Daoris answers the terminal's verbs

**Standing:** contract under D171 point 3 (RPC1). Written by the steward, as the owner left the shape: *"you can decide but
this clearly is a gap"*; a second agent's (Codex's) review of the plan, checked against `RunningService.cs` and D171, added §3's
home identity, §5's lease and §6's request ids. Its rows are HOMEID1, HOMELEASE1 and RPC1a to RPC1g in `TASKS.md`. §3.1, §3.2
and §5 are built (D171's HOMEID1 and HOMELEASE1 notes); so are §2.2, §4.1, §4.2's census and §6 but its `SUBSCRIBE` and §6.2's
cancel, the server's side of §8 and §6.3's order (D171's RPC1a note); the rest is not.

> The owner, 2026-10-16: *"so we had better have a server, or set it up as an RPC service for Daoris, and this will also
> support multi-instance Daoris windows syncing messages (or the second time I click on the exe, we only start a new window
> from the same instance of Daoris's current session)"*.

D171 point 1 (FINDDAORIS1, SERVICEHOME2) lets a terminal find the home and the service, and point 2 (ONEINSTANCE1,
MAINWINDOWS2) keeps one instance with many windows. This is point 3: while an instance runs, a verb is its client over a local
channel, running the handler its windows' press runs, so there is one writer, every window hears it as it happens, and the
verb's answer is the press's. With no instance running, a verb acts alone as it does now, under the same lease the instance
holds. §1 is what acts outside the loop today; §2 the channel; §3 which home and which instance; §4 the pipe's security; §5 one
writer per home; §6 the wire; §7 how a verb chooses; §8 how windows hear; §9 the CLI; §10 what was rejected; §11 what this
leaves; §12 the rows.

## 1. What acts outside the loop today

### 1.1 Three ways a verb reaches a running Daoris

A `daoris-driver` verb runs in its own process (`Daoris.Desktop.Driver.Host/Program.cs`, the routing; `docs/index/verbs.md`,
the table). It reaches the desktop's loop in one of three ways, or none:

- **None: it acts alone.** It opens its own `ServiceClient` (the service's HTTP door, `ServiceHttp.FromEnvironment`), works
  the home's files and the trees with its own code, and the window learns of it at the loop's next tick that sees the change
  (D169's STATEHUB1d note: a press the page sends the service over HTTP reaches other windows by the tick).
- **The request folder** (D126 §7.1, `SessionRequests`, `SessionRequestWatch`): a move the verb cannot make itself, because the
  session's process is another loop's, is a file `<home>/sessions/requests/<id>.json` that every loop on the home looks at
  each second; the loop whose registry runs the session takes it once, and the verb waits up to ten seconds for the record to
  move, or reads the answer written beside a say.
- **A told note** (D169's STATEHUB1e note, `PressesTold`): the `trees` presses act alone, then leave `<key>.told` beside the
  requests naming the route their press is, and the shell's loop publishes that route's declaration to its windows within the
  watch's second. The note tells; the act was the terminal's.

### 1.2 The census, by family

Read off the host's routing, each family's world (`SessionsWorld`, `WorkWorld`, `HistoryWorld`, `SetupWorld`,
`RegistrationWorld`, the trees console) and the bridge's routes (`docs/index/routes.md`) at this branch's start. Every verb
was routed and its world read; not every write was traced line by line, and RPC1a's census test is where the classing
becomes exact.

| Family (`daoris-driver …`) | Its window's twin (`DAORIS.DRIVER`) | Acts in | What it writes | How a window hears it |
|---|---|---|---|---|
| `trees land`, `remove`, `clean`, `sync`, `hand` | `LAND_SESSION_TREE`, `LAND_ASK`, `LAND_ENTRY`, `DISCARD_SESSION_TREE`, `DISCARD_SESSION_BRANCH`, `SWEEP`, `TREES_SYNC`, `HANDOFF` | its own process | git (the trees, the line), the landings record, the service | a told note, within a second |
| `trees list`, `state`, `--plan` | `LANDING`, `SWEEP_PLAN`, `TREES_SYNC_PLAN`, `HANDOFF_PLAN` | its own process | nothing (a sync's plan fetches) | — |
| `sessions stop`, `finish`, `decline`, `say`, `take-back`, `reword` | `STOP_SESSION`, `RESOLVE_SESSION`, `SESSION_INPUT`, `SESSION_QUEUE_EDIT` | the loop that runs the session, through the request folder; alone where nothing runs it | the ledger through that loop; a request and its answer | the loop's own move, a second or more after |
| `sessions archive`, `unarchive`, `delete`, `go-on-new`, `start-from`, `hand-over`, `helper` | `SESSION_ARCHIVE`, `SESSION_DELETE`, `SESSION_GO_ON_NEW`, `SESSION_START_FROM`, `SESSION_HAND_OVER`, `SESSION_HELPER_*` | its own process | the ledger, the session's files under the home | the tick |
| `ask … --pause`, `--resume`, `--abandon`, `quest pause`, `resume`, `abandon` | `WORK_PAUSE`, `WORK_RESUME`, `WORK_ABANDON` | its own process, its stops through the request folder | `driver.json`'s pauses, the ledger, requests | the tick |
| `ask … --clear`, `quest clear`, `history clear` | `HISTORY_CLEAR` | its own process | deletes under the home, the ledger | the tick |
| `help rename`, `pin`, `unpin`, `delete` | `HELP_RENAME`, `HELP_PIN`, `SESSION_DELETE` | its own process | the ledger, the conversation's files | the tick |
| `opinion ask`, `stop`, `anyway`, `myself` | `ASK_OPINION`, `STOP_OPINION`, `OPINION_ANYWAY`, `OPINION_MYSELF` | its own process | the service's opinion records | the tick |
| `setup`, `register`, `sync`, `ask channel` | `SETUP_PRESS`, `REGISTRY_REFRESH`, `SYNC_NOW`, `CHANNEL_POST` | its own process | the service; a registration's lines | the tick |
| `plugins install`, `new`, `try` | `PLUGIN_INSTALL`, `PLUGIN_NEW`, `PLUGIN_TRY` | its own process | `<home>/plugins/` | the tick, or a look |
| `quest delete`, `accept`, `done`, `review`, `check`, `answer`, `ask "…"` | none: the page sends these to the service over HTTP | its own process | the service | the tick |
| `update` | the update module's banner | its own process | a request file the desktop's updater watches | the updater's look |
| `trace`, `git branches`, `workflow`, `logs`, `sessions` (the list), `help list`, `history`, `plugins show`, `activity`, `sync status` | `TRACE`, `GIT_BRANCHES`, `WORKFLOW_RUN`, `SESSION_GROUPS`, `HELP_CONVERSATIONS`, `HISTORY_PLAN`, `PLUGIN_READ`, … | its own process | nothing (`workflow keep` writes its run) | — |
| `chat`, `help resume` | `START_CHAT`, `SESSION_INPUT`, `START_HELP` | its own process, with its own agent | a conversation it runs itself, with no request watch | the tick; its stop is its own Ctrl+C |
| `drive` | — (it is a loop) | its own process | everything a loop does | refused where the desktop holds the home, unless `--share` (DRV8a) |

And the CLI: `daoris driver …`, `agent …`, `plugin …`, `tool …` edit their files under the home directly (D50, the management
class), and reach a window at a tick that sees them (D169's STATEHUB1e note). Its doctrine commands touch no home.

### 1.3 What it shows

- **Two writers of one home.** Every press but the request folder's acts from the terminal's process beside the loop: git on
  the same trees, the same `driver.json`, the same ledger. The locks hold what they guard (the driver's lock, each tree's,
  each record's), and every one of them exists because there are two.
- **One instance per install is not one writer per home.** The kit's guard is scoped by the install's root; the driver's lock
  (`driver.lock`, DRV8a) by one loop on a home, with `--share` beside it. A verb acting alone takes neither, nor does the
  desktop's first-start migration (D63), and a home reached by two paths (a junction, a mapped drive, another case) is one
  folder that nothing names as one.
- **Finding the instance trusts too much.** FINDDAORIS1 takes an address whose process lives and whose `/api/status` names a
  `tier` (`RunningService.IsDaorisStatus`, the CLI's `runningService`): another install's service passes that test, the CLI's
  check of the process is its id alone (an id the system gave to another process since passes), and the status is asked with
  redirects followed.
- **Windows hear late or not at all.** A told note is a second; the request folder a second and a wait; everything else a tick.
  A press from the terminal is in no window's *under way* (D169's STATEHUB1c2 note says so), and no plugin at `state/changed`
  hears it unless a note told it.
- **Two answers.** The verb's words and the route's answer are built from the same core (D50's twins), and they drift where a
  route learns a refusal its verb does not.
- **The person's door is a round trip.** D156 §4.2 has a terminal's keyless person-only write refused, then asked of the window
  through the service (`/api/confirmations`), though the window is the very process the terminal wanted. Only the service's half
  is built (PERSONDOOR1b): no client asks, no card shows and no shell hands a key until PERSONDOOR1d–g, so today no install
  gates a terminal's press, nor a window's.

## 2. The channel

### 2.1 Weighed

| | A named pipe the instance's dispatcher answers | The local service | The kit's single-instance pipe | A loopback port in the desktop | The request folder for everything |
|---|---|---|---|---|---|
| Runs the window's handler | yes: the transport hands the kit's envelope to the same `IMessageDispatcher` | no: the driver's routes are the desktop's (D47, D169 point 4), so it could only relay back into the desktop | no: it carries a launch's arguments and folder one way, and answers nothing | yes | no: each move is written twice, as a request and as its handler |
| Latency | a local round trip, answered when the route answers | two hops and HTTP | — | one hop and HTTP | the watch's second, and a wait |
| Security | an ACL for this user, no network, this Windows session (§4) | the person key and its confirmations, designed (D156) | the same user and session, the kit's | the loopback, which any web page reaches (D46's ORIGIN1 and ORIGIN2 notes) | the account's files |
| `[Publishes]` | the middleware runs, as for a window's press (§8) | none: the service sees no bridge | — | the middleware runs | a told note per route, by hand |
| What the routes need | their payload and the window's modules, as a page sends them | a reverse channel into the desktop | — | as for the pipe | a request shape per move |

The kit's dispatcher is built for this: `IMessageDispatcher.DispatchAsync` is *the transports' entry point*, and the request
and response envelopes are *transport-neutral*, their names pinned so the wire holds under any serializer (`Shenora.xml`,
0.21.1). The service is also reachable by every local program, a session's included, and stays spawn-free (D46).

### 2.2 Chosen: a pipe the instance answers with its own dispatcher

The desktop serves one named pipe. Each connection carries the kit's request envelopes, one JSON line each, and gets the kit's
response envelopes back, through the dispatcher every window's page uses, so the route is the window's press: the same handler,
the same middleware (`RefusalLog`, `UiStallWatch`, `OffTheWindow`, `StatePublisher`), the same answer. The service stays what it
is, the door for what a page also sends it over HTTP. The pipe is Daoris's own code over `System.IO.Pipes`; it asks nothing of
the kit's owner. Should the kit come to ship a pipe transport, Daoris's goes for it.

## 3. Which home, and which instance

### 3.1 The home's identity

A home is named by a path, and one folder has many: a junction, a mapped or `subst` drive, another case, a short name. So a home
gets an identity that every path to it reads alike: `<home>/home.json`, `{ "id": "<32 lower-case hex characters, 128 random
bits>", "minted": "<ISO 8601>" }`, minted once by the first Daoris process that needs it (the desktop at its start, a host, a
verb) and written as a new file only, so of two minting at once one file stands and both read it. It is never changed in place; a file that does not read
is said and never replaced (the person's home is not guessed at). Its id is never a path, so it may travel where a path may not.

### 3.2 The status answers it, and discovery checks it

- **`/api/status`** on a local host answers `home`, that id, read from the home it was started with, beside its `tier`; a
  shared host answers none (D47).
- **`running.json`** gains `home`, the same id, beside `url`, `pid`, `started` and `since` (fields added, never changed).
- **Discovery** (FINDDAORIS1's `RunningService.Find` and the CLI's `runningService`, then the pipe's) takes an address only
  where the status answers this home's id, asked with redirects off (a redirect is no answer). The id makes the process check a
  courtesy: an address another install's service holds, or a process id the system gave to another process since, answers
  another id or none, and is not taken. A status with no `home` (an older host) is not taken either.
- SERVICEHOME2's hosts read no address file, so they change only by answering the id.

### 3.3 The instance

The pipe's hello (§6) answers the same id, its process and its build; the client also asks the system which process serves the
pipe (`GetNamedPipeServerProcessId`) and takes the server only where that is the process `running.json` names. A client that
resolved another home's id is not this instance's client.

## 4. The pipe's security to this Windows user

### 4.1 The pipe

- **Its name** is `daoris-<the home's id>-<the Windows session's id>`, so two homes' instances, and one user's two sessions,
  never meet, as the kit's guard scopes its own. It is also written into `running.json` as `pipe`.
- **Its ACL** grants the current user's SID alone and denies the network SID, so nothing reaches it from another machine or
  another account. The server is made as the first instance of its name (`PipeOptions.FirstPipeInstance`): where the name is
  already held the desktop serves no pipe and says so in its machine log (`pipe.refused`), rather than share a name.
- **Its owner** is the current user's SID at either elevation (PIPEOWNER1, D171's note): left to the token's default owner, an
  elevated Daoris's pipe was the Administrators group's.
- **The client** takes a pipe owned by its user's SID or its token's default owner, and nothing else, and checks the server's
  process (§3.3). It reads the owner itself: `PipeOptions.CurrentUserOnly` took the token's default owner alone, so a terminal
  and a Daoris of one user at two elevations refused each other. A pipe that refuses the terminal is said as a refusal.
- **Bounded**: eight connections at once, a line of at most one mebibyte, a hello within five seconds, and a connection idle
  for two minutes closed.

These are this design's choices; none was measured here.

### 4.2 It identifies the user, not a person's press

A session's agent is the same account (D156 point 4, design §4.1): it can open this pipe as it can run `daoris-driver`, read
the home's files and press the service's open doors. So **the pipe confers no person's authority**, exactly as a binary does
not. Each route the pipe accepts is classed by RPC1a's census, a test that fails on a route it does not class:

- **open**: a read, or a press D156 §3 leaves to an agent or to the driver (an agent's doors are judged as an agent's, the
  driver's records what it read): run as asked. Which of the bridge's presses are these is the census's to say, route by
  route, against D156 §3's table; nothing here classes one in advance.
- **person**: a press D156 §3 makes the person's alone (a review's verdict, a yes, a go-ahead's answer, a close, a delete, a
  clear, a re-wiring): the instance shows the window's confirmation card (PERSONDOOR1f's, which D156 §4.2 describes), naming
  the terminal as the asker and saying *Daoris cannot tell whether you ran this or an agent did*, and runs the route only once
  **Confirm** is pressed within two minutes; the client says it waits. No `/api/confirmations` hop is needed: the pipe's
  connection is what the confirmation answers. Until RPC1c builds this, such a route is refused over the pipe with a code
  (`confirm-in-window`), never run, and its verb keeps acting alone as it does today (§7.2).
- **window**: what only a window does (its own commands, the pickers, the badge, the browser module, the terminal panel's
  shells): refused over the pipe.

An unclassed press is **person**, and an unclassed module is refused, so a route added later is safe before it is classed.

### 4.3 What never crosses

The person key stays in the window (D156 point 2): the pipe never answers it, and once the shell holds one (PERSONDOOR1g) a
route the pipe runs presents it to the service only after the person's **Confirm**, as a confirmed grant would. No machine path
is added to any answer; an answer is the window's, which already carries this machine's paths only to this machine (D46).

## 5. One writer per home: the lease

The instance is one writer only if nothing else writes beside it. So the home gets one lease, `<home>/home.lease`, the driver's
lock (`driver.lock`, DRV8a) grown to cover every writer rather than a second file beside it:

- **Who holds it.** An instance (the desktop's loop, a headless `drive`) for its life; a verb acting alone (§7.2) for its
  mutation only, never for a read; the desktop's first-start migration (D63); and the launcher's swap of `app/` (D139 §5), so no
  verb the install carries runs alone from a folder being replaced.
- **How it is taken.** As the driver's lock is today: written beside, moved into place with no overwrite, released only while it
  still says what its holder wrote, a holder whose process is gone or whose start differs replaced. It names its holder's kind
  (`desktop`, `headless`, `verb`, `migration`, `swap`), process and start, so a refusal says who holds it.
- **Waiting.** A verb finding an instance's lease is that instance's client (§7); finding another verb's, it waits up to thirty
  seconds, then says which verb holds it. An instance starting while a verb or a migration holds it waits up to thirty seconds,
  then says so in its window and its log.
- **Aliases.** The lease is a file in the folder, so every path to the folder takes the same lease, and its holder records the
  home's id (§3.1).
- **`--share`** is DRV8a's run beside the desktop's loop; under the lease it reads beside, and its writes go to the instance as
  a verb's do. HOMELEASE1 settles that with the loop's own writes.

## 6. The wire

- **Lines**: UTF-8, one JSON object per line, LF.
- **Hello first.** The client sends `{ id, module: "DAORIS.PIPE", type: "HELLO", payload: { protocol: 1, home, client,
  version } }`, `home` the id it read; the instance answers `{ protocol: 1, home, version, pid }`, or refuses with a code:
  `protocol` (another version of the wire), `home` (another home), `stopping` (the instance is closing, §6.3). A connection
  that says anything before its hello is closed.
- **Requests** are the kit's envelope as a page sends it, `{ id, module, type, payload }`; **responses** are its
  `{ category: "ipc", id, success, data?, error? }`. Several may be in flight on one connection, matched by `id`.
- **Structured refusals and exit codes.** A refusal answered as data (`{ …, why, message }`, the bridge's form) is the verb's
  exit 1, its `message` printed; an `error` (the kit's code, the driver's `Refusals` code) is exit 2 with that code's sentence;
  an answer is exit 0. The family's codes are unchanged.
- **Progress.** A route that reports progress (`context.Report`, the kit's tracker) has it forwarded to the connection that
  sent it, as the kit's event envelope naming the request's id; the verb prints each as a line. Nothing else is forwarded
  unasked.
- **Events, by subscription.** `DAORIS.PIPE`'s `SUBSCRIBE { modules: [...] }` forwards the bus's events for those modules until
  the connection closes: how a verb waits on a record's move or streams a conversation (RPC1g). A verb that only presses
  subscribes to nothing.
- **The census answers itself**: `DAORIS.PIPE`'s `ROUTES` lists each route the pipe accepts with its class.
- A wire shape like every other: fields and types are added, never changed in place (D169 point 3).

### 6.1 A request's id is durable

The client mints each request's id (a fresh UUID), and the instance keeps, under `<home>/pipe/`, a line per id when it starts and
when it ends, with the answer, for 24 hours. So:

- **A disconnect does not cancel a press.** The transport hands the dispatcher the instance's own lifetime, not the
  connection's, so a press runs to its answer whether or not its client is still there to hear it.
- **`DAORIS.PIPE`'s `RESULT { id }`** answers `running`, `answered` with the answer, or `unknown` (never seen, or begun by an
  instance that stopped before it ended).
- **An id seen before is answered from its line, never run twice**: a client that sends again after a lost connection gets the
  first answer.
- **The verb says how to find out**: one that lost its connection asks `RESULT` once it reconnects, and otherwise says *Daoris
  was still working on it (request `<id>`)*, exit 2; `daoris-driver result <id>` asks again later.

### 6.2 Cancelling

Ctrl+C sends the kit's cancel for the request's id where the route takes cancellation, and says what the answer was; a route
that cannot stop part-way runs on, and the verb says so with its id. Closing the connection cancels nothing (§6.1).

### 6.3 Start, stop and update

- **Start.** The instance takes the lease (§5), brings its service up, serves the pipe, and only then writes `running.json`'s
  `pipe`, so a client that reads one finds it served.
- **Stop.** The instance answers every new hello `stopping` from the moment its close begins, lets the presses under way answer
  within the loop's own drain, removes `running.json` and releases the lease, in that order.
- **Update** (D139): the updater's drain is a stop's beginning, so a verb meets `stopping`, exit 2: *Daoris is updating; run this
  again once it is back*; the launcher's swap holds the lease (§5), so no verb acts alone until the new build has taken it.

## 7. A verb: the instance's client while it runs, alone when none does

### 7.1 Which verbs, which door

| Door | Verbs |
|---|---|
| **The pipe** while an instance runs | each verb whose window twin is a bridge route (§1.2's second column), the CLI's edits that a route answers (§9) |
| **The service**, as today | the verbs whose window twin is the page's HTTP call: `quest delete`, `accept`, `done`, `review`, `check`, `answer`, an ask's publish; the service is already their one writer |
| **Alone, under the lease** | each of the above with no instance running; `drive`; `update`'s request |
| **Offline** | the CLI's doctrine commands, which touch no home (D50) |

`chat` and `help resume` stay alone until RPC1g.

### 7.2 The switch

| What the verb finds | A press | A read |
|---|---|---|
| No `running.json`, or one whose status or hello does not answer this home's id | takes the lease (§5) and acts alone | alone |
| The instance answers its hello on this home | sends its route; prints the answer | sends its route where it has one, else alone |
| The instance lives, but its pipe does not answer within five seconds, or the file names no pipe | refused, exit 2: *Daoris is running on this home (`Daoris.exe`, pid …) and did not answer; nothing was done* | alone |
| The hello refuses (`protocol`, `home`, `stopping`) | refused, exit 2, saying which | alone |
| The answer does not come (the connection lost, the verb's wait over) | asks `RESULT`; never acts alone (§6.1) | asks again or reads alone |
| A headless `drive` loop holds the lease | alone under the lease's rule, with the request folder for what that loop runs, until a headless loop serves the pipe | alone |

**A timeout never means "do it here".** A press never falls back to acting alone beside a live instance, nor after its request
was sent: the client finds out whether it happened (§6.1), and only a verb that finds no instance at all acts alone, under the
lease. A verb becomes a client only once its route is classed and the instance runs it: one whose route is `person` keeps
acting alone, as today, until RPC1c lets the window confirm it, so no row takes a door from the terminal.

### 7.3 What the verb prints

The route's answer, in the verb's own words. Where a route answers the record its core already returns (a landing's outcome,
a sweep's, a session's move), the verb reads the answer back into that record and prints it with the words it prints today,
so the terminal's text does not change: one handler, shared, and the words its own. Where a route answers the page's shape only,
its row adds the fields the terminal needs, beside the others. Each verb's row holds the text of the client path to the alone
path's, case for case.

### 7.4 Finding the instance

A verb takes the instance as §3 says: `running.json`'s process lives with its recorded start, its status and its hello answer
this home's id, and the pipe's server is that process. 🔴 **A workspace build reads no address file** (D171's FINDDAORIS1
note): on a machine with an install the account's `DAORIS_HOME` names that install's live home, and a gate or a test that met a
running Daoris would act on it. The dev loop and a rehearsal that mean to reach an instance name its pipe in `DAORIS_INSTANCE`,
as they name the service.

## 8. How the answer and its `[Publishes]` reach every window

- **The pipeline does it.** A route the pipe runs passes the dispatcher's middleware, so `StatePublisher` publishes its
  declaration (`STATE_CHANGED`, D169 point 2) once it has answered, its session read from the payload as for a window's press;
  every window's hub reads again, and the plugins at `state/changed` hear it (STATEHUB1g).
- **Under way, in every window.** The press is in the loop's list while it runs (`UNDER_WAY`, D169's STATEHUB1c2 note), so a
  landing pressed in a terminal shows *落地中…* in every window, which no told note could do.
- **Said as the terminal's.** The transport marks the request's door, so `RefusalLog` and the machine log say `door: terminal`
  for what came over the pipe; the page's own presses stay unmarked.
- **What retires.** The told notes (`PressesTold`) go once every trees verb is a client (RPC1b, or RPC1c for one the census
  classes `person`): with an instance there is a pipe, and without one no window to tell. The request folder stays for a
  headless loop and for a session nothing on this machine runs.

## 9. The CLI

`daoris` is the other terminal (D50). Its management verbs that edit a file the loop reads (`daoris driver …`'s standing
choices, whose window twins are the `SET_*` routes; `agent`, `plugin` and `tool` verbs whose routes the census finds) send their
route over the same pipe while the desktop runs, so a window shows a choice the moment it is made, and take the lease (§5) when
none runs. Node opens a Windows named pipe with `net.connect` and needs no dependency. The client sits in `service.ts`, the one
module the management class reaches the network through, so D50's transitive-import test holds as it is, and doctrine commands
reach nothing. The wire, the home's identity and the lease are twins (`twins.md`): the CLI's code and the driver's share none,
and each holds the hello's, the switch's and the lease's rows. A CLI the install does not carry reads no address file (§7.4).

## 10. Rejected

- **The local service as the channel**: the driver's handlers live in the desktop, so the service would relay into it, a second
  hop and a reverse channel; a shared host's code would hold machine-local acts (D47); and every local program reaches it.
- **A loopback port in the desktop**: every web page on the machine reaches the loopback (D46's ORIGIN1 and ORIGIN2 notes), and
  a pipe has no browser on it.
- **The kit's single-instance pipe**: one way, a launch's arguments only, and the kit's.
- **The request folder for every verb**: a second's polling and a shape per move; it stays where it is needed (§8).
- **Running the verb's own command inside the instance** (a `RUN { args }` over the pipe): one writer, but the window's route
  would still be a second path, `[Publishes]` would not run, and D50's twins would stay two.
- **Acting alone when a live instance does not answer, or after a request was sent**: the second writer, at the moment something
  is already wrong, or the same press twice.
- **The connection's life as the press's**: a lost terminal would cancel a landing half done, with nobody told how it ended.
- **The home's path as its identity**: one folder has many paths, and a path may not travel.
- **The pipe as the person's proof**: the same account opens it (§4.2, D156's rejection of an OS identity).

## 11. What this leaves

- **A conversation from a terminal** (`chat`, `help resume`) still runs in the terminal's own process. Its turn belongs in the
  instance, with the terminal a viewer streaming it by subscription; RPC1g designs that before it builds.
- **The account's own reach** (D156 point 7): another process of the account can read the home, take the lease's file, squat a
  pipe's name before the desktop starts, or drive the window. PERSONDOOR2's boundary is what closes it.
- **A copy of a home** (a backup put back beside the original) carries the original's id: two instances on the two copies meet
  at the pipe's name, the second serving none and saying so. A copy is not a use this design serves.
- **Windows only.** The desktop is a Windows application; a Unix socket is the same design where one is wanted.
- **Not measured**: the pipe's ACL against another account or machine, a round trip's time, and the census's every write; each is
  a row's proof. `CurrentUserOnly`'s check was measured on the install (PIPEOWNER1): it refused a terminal that was not elevated
  the elevated Daoris's pipe, which the ACL let in.

## 12. The rows

In order: the home's identity first, since today's discovery takes another install's service; the lease next, since "alone"
is safe only under it; the pipe and its census, which every verb needs; then the trees, whose told notes it retires; the
person's card before any person route is opened; then the rest.

- [ ] **HOMEID1 — the home has an identity, and finding the running Daoris checks it** (service/driver/CLI; D171 point 1, a defect in FINDDAORIS1's discovery). Discovery takes any address whose process lives and whose status names a `tier`, follows redirects, and the CLI checks only the process id, so another install's service, or an id reused, passes. A home's `home.json` id (§3.1), answered by a local host's `/api/status` and kept in `running.json`, and checked by `RunningService.Find` and the CLI's `runningService`, redirects off. Contract: §3. Proof: service status tests; `RunningServiceTests` and `running.test.ts` rows for another home's id, no id, a redirect and a reused process id; the twin row in `twins.md`.
- [ ] **HOMELEASE1 — one writer per home** (driver/desktop/CLI/launcher). One instance per install is not one writer per home: a verb acting alone, the first-start migration and the launcher's swap take no lease, and two paths to one folder are two homes to every check. The driver's lock grows into the home's lease (§5), held by an instance for its life and by a verb, a migration or a swap for its act, by any path to the folder. Contract: §5. Proof: lease tests (two takers at once, a dead holder, an alias path, a verb's wait and its sentence); the desktop's start waiting on a verb's hold; the CLI's twin rows.
- [ ] **RPC1a — the running Daoris answers on a pipe** (desktop/modules/driver/CLI; after HOMEID1 and HOMELEASE1; D171 point 3). The shell serves its named pipe (§4.1) carrying the kit's envelopes into its own dispatcher, with the hello, `ROUTES`, `RESULT`, the request journal, progress forwarding and the census that classes every route `open`, `person` (refused `confirm-in-window` until RPC1c) or `window`; `running.json` gains `pipe`, read by both twins; the start, stop and update order of §6.3. Contract: §2.2, §4, §6, §8. Proof: a modules test pressing a declared route over a real pipe that publishes `STATE_CHANGED` and lists it under way as a window's press does; a person route and a window route refused; a disconnect mid-press answered by `RESULT`; an id sent twice run once; `stopping` from a closing instance; the census failing on an unclassed route.
- [ ] **RPC1b — the trees verbs are the running Daoris's clients** (driver host/driver). `InstanceClient` finds the instance as §7.4 says (a workspace build reads no file) and switches as §7.2's table does; of `trees land`, `remove`, `clean`, `sync` and `hand`, each whose route the census classes `open` sends it and prints its answer in today's words, and one classed `person` waits for RPC1c; the told notes go with the last of them. Contract: §6, §7, §8. Proof: host tests for each row of §7.2 against a stand-in server, the lost answer never acted alone among them; each verb's text the same from the route's answer as alone; `StateToldTests` and `PressesToldTests` retired with the notes.
- [ ] **RPC1c — a person's press from a terminal is confirmed in the window** (desktop/web; after PERSONDOOR1f; D156 §4.2). A `person` route over the pipe shows PERSONDOOR1f's card with the terminal named and *Daoris cannot tell*, runs once on **Confirm** within two minutes, and the client says it waits; refused or expired, nothing runs; the verbs whose routes are `person` then become clients. Contract: §4.2, §4.3, §7.2. Proof: modules tests (confirmed, refused, expired, five waiting); the card's page test in en and zh; on the install, `daoris-driver opinion myself <id>` confirmed in the window.
- [ ] **RPC1d — the sessions verbs are the running Daoris's clients** (driver host). `sessions stop`, `finish`, `decline`, `say`, `take-back`, `reword`, `archive`, `unarchive`, `delete`, `go-on-new`, `start-from`, `hand-over` and `helper` send their routes while the desktop runs; the request folder stays for a headless loop. Contract: §1.2, §7. Proof: host tests per verb; the alone path's `SessionsCommandTests` unchanged.
- [ ] **RPC1e — the rest of the host's verbs with a window twin** (driver host). Work, history, help, opinion, setup, register, sync, plugins and the channel send their routes; the reads read through the instance where they have one. Contract: §1.2, §7.1. Proof: the census test with only `chat`, `help resume`, `drive`, `update` and the service's verbs left off the pipe, each with its reason.
- [ ] **RPC1f — the CLI's edits through the running Daoris** (CLI). `daoris driver …` edits that a `SET_*` route answers, and the `agent`, `plugin` and `tool` verbs the census finds a route for, go over the pipe from `service.ts` while the desktop runs, so a window shows them at once, and under the lease when none runs; the wire is a twin with the driver's client. Contract: §9. Proof: CLI tests against a stand-in pipe; the transitive-import test unchanged; a `twins.md` row with both tables.
- [ ] **RPC1g — a terminal's conversation is the running Daoris's** (design; driver host/desktop). `chat` and `help resume` start their turn in the instance and stream it by subscription, so every window shows it and the loop owns it; designed first, its streams' shape and its Ctrl+C. Contract: §6's events, §11. Proof: the design's note, then its rows.
