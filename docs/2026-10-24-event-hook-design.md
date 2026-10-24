# The work cycle's events reach an outside agent's session by a plugin, pushed, never polled (EVENTHOOK1; proposed, 2026-10-24)

**Standing:** proposed, round 1, under D191. Written by Claude Opus 5.5 from the code at `5e9a563bd` (this branch's base on main). Two
reads follow before anything is built: Codex's, and the independent Opus foundation reader's, since the design changes a protocol
between processes (the hook wire gains a point, and a plugin serves an endpoint other processes connect to; D191's amendment of
2026-10-20). Dated notes amend D173 (the fabric events design's decision), D119 and D120, each proposed with this. Nothing here is built.

The row EVENTHOOK1 in `TASKS.md` carries the case. The steward watches the install by polling its sessions in a shell loop it
re-arms every half hour. A plugin hooked into the work cycle should push the fabric's events (a session ended, waiting on the person,
failed, landed; a quest published or closed; a Confirm asked) to a subscriber outside Daoris, the first being a Claude Code session.
This is the first real test of a plugin listening to Daoris's event pipeline.

A path without a root is under `src/Daoris.Desktop/`; `Driver/` is `src/Daoris.Desktop/Daoris.Desktop.Driver/`, `Modules/` is
`src/Daoris.Desktop/Daoris.Desktop.Modules/`, and `Mcp/` is `src/Daoris.Service/Daoris.Service.Mcp/`.

---

## 1. What is true today

Each statement was read at `5e9a563bd` at the lines named.

### 1.1 The kinds of point, and the points

`HookPoints` names every point once (`Driver/Plugins/Domain/HookPoints.cs:24-88`). By kind:

| Kind | Point | What it is |
|---|---|---|
| decision | `quest/consider` | a planned start, a waterfall that fails closed (`:26-27`; `Driver/Plugins/Hooks.cs:1027-1059`) |
| observation | `session/ended` | what a tick concluded; contained (`HookPoints.cs:29-30`; `Hooks.cs:1061-1088`) |
| act | `work/land` | a landing's branch handed on, spoken by the landing, never the loop (`HookPoints.cs:32-37`) |
| query | `work/state` | a landed branch's pull request, asked at an occasion that may remove (`:39-44`; D148) |
| observation | `state/changed` | what a press changed, told off the press's path (`:46-51`; D169 point 9) |
| work wire | `work/watch`, `work/step`, `work/perform` | the fabric's performed steps, the lease's loop alone (`:67-87`) |

- **A point is additive.** A new point goes last in `HookPoints.All` (`:53-58`), and a handshake naming a point a build lacks is said
  once and never asked there (`:11-13`; D148 point 8). `protocolVersion` is 1 (`Hooks.cs:108`).
- **What a new point costs** is listed on the class (`HookPoints.cs:15-23`): its frame in `HookFrames`, its call on `IHookChannel`,
  `HookPeer` and `HookProcess`, its kit entry and sample, the kit test's `ANSWERS` entry, its case in `PluginTrials` (the cases at
  `Driver/Plugins/PluginTrials.cs:324-360`), `PluginsCommand.Cost`, Ask Daoris's `PointSaid`, the rows of `PluginKitTests` and
  `DriverModulePluginsTests`, the page's gloss and `twins.md`'s answer row.
- **The loop's points** are `quest/consider`, `session/ended` and `state/changed` (`HookPoints.Loop`, `:60-65`): only a plugin on one
  of them, or on the work wire where this loop holds the lease, is kept running beside the ticks (`Hooks.cs:702-710`).
- **A plugin asks Daoris nothing.** The peer's reader takes one notification, `work/step`, and only from a process that declared it
  (`Hooks.cs:524-531`). A request from a plugin is neither answered nor errored, by design (D64 §7), and any other notification is
  ignored (`:532-534`).

### 1.2 The hook set: how the loop tells

`HookSet` (`Hooks.cs:617-1216`) keeps one process per wanted plugin, reconciled at each look (`:687-829`), and stops each with the loop.

- **`session/ended`.** `Driver.EndingsAsync` drains what ended since the last look and tells each concluded `SessionEnded` to the set
  (`Driver/Loop/Driver.cs:1615-1642`). Under the watch it is judged beside the look, **`HooksAtOnce` endings at a time, which is 2**
  (`:1633-1637`, `:1695-1698`). So two endings may reach one plugin at once, and their answers come back in either order: today's
  observation keeps no order across sessions. Each call is a request whose answer is waited for, for `HookPeer.DefaultPatience`, 10 s
  (`Hooks.cs:116-117`, `:240-241`), and never read. A failure is a line and the tick goes on (`:1079-1085`).
- **`state/changed`.** The shell's loop subscribes to `DAORIS.STATE_CHANGED` (`Modules/Loop/DriverLoop.cs:825-828`;
  `Modules/Platform/StatePublisher.cs:223-234`). `Tell` writes the change to a bounded queue and returns (`Hooks.cs:1090-1100`): at most
  `ChangesWaiting`, 64, the oldest dropped first (`:654-662`). One teller tells each change in order to every listening plugin at once,
  each failure contained (`:1102-1152`). **A headless loop has no bus** and tells this point nothing (`HookPoints.cs:61-64`; D169's
  STATEHUB1g note, `docs/decisions/D169.md:272-274`).
- **Health without words.** Each start, call, failure and stop is a `plugin.*` line in the machine log and a word in the loop's record of
  the plugin's health (`Hooks.cs:612-615`; D119 point 5), never what the plugin wrote.

### 1.3 The plugin's process and its environment

`HookProcess.StartInfo` (`Driver/Plugins/HookProcess.cs:55-114`) starts the manifest's command:

- **in the plugin's install folder** (`:88`), its first word resolved by Tools, never handed to the system bare (`:61-83`);
- **stdin and stdout the wire, UTF-8 without a mark** (`:90-97`); stderr relayed line by line under `plugin:<id>`, each line at most
  8 KB (`:227-250`);
- **with its environment told, not guessed** (`:101-106`): `DAORIS_PLUGIN_ID`, `DAORIS_PLUGIN_FOLDER`, `DAORIS_PLUGIN_DATA`,
  `DAORIS_HOME`, and the tools' environment. The data folder is `<home>/plugins/.data/<id>`, which survives an update
  (`Driver/Plugins/Domain/PluginCatalog.cs:40-41`). The wiring census lists the three plugin variables
  (`Driver/Platform/WiringVariables.cs:118-120`), and `SystemWiringTests` (`:576-583`) fails on a variable named in code and not there;
- **inside the agents' job.** Its start goes through `AgentStarts` as `ContainedKind.Plugin` (`HookProcess.cs:141-146`): D201 point 4
  contains a plugin's process as an agent's, so nothing a plugin starts is ever covered by the person's standing confirmation;
- **told to go and given 2 s** before it is ended (`:49-50`, `:195-225`).

The person's key is never in a Daoris process's environment (D156 point 2), so a plugin cannot hold it.

### 1.4 The kit's cycle: the exemplar for a stand-in world

`PluginCycles` (`Driver/Plugins/PluginCycles.cs:18-35`) drives a folder's performer as the driver would: started by
`HookProcess.StartInfo`, spoken to through `HookPeer`, its events read by the reader the hook set hears with. **Nothing reaches a
platform**: the plugin is told a stand-in world (`DAORIS_PLUGIN_WORLD`, `:37-38`) that counts each external effect (`:200-210`,
`:421-422`), in a scratch home and data folder removed after (`:114-137`). It drives performers only: it refuses a plugin without
`work/step` and `work/watch` (`:188-198`). This design's proof takes its shape (a real plugin process, a stand-in on the far side,
counted) and not its code.

### 1.5 The pipeline the window reads

- **`STATE_CHANGED` says what moved, never what it is now** (`StatePublisher.cs:99-106`; D169 point 3): scope names and, where a route
  named one, the sessions. No words, no answer, no machine path. It is published once a press has answered (`:189-221`), or by the
  driver with no press behind it (`:258-264`). Shell-only (D169 point 4): nothing forwards it off this machine.
- **NOBLOCK1** (D56's NOBLOCK1 note; `docs/task-archive.md:19-26`): the reads the window waited on answer from what is held and tell
  their work's answer as `DAORIS.ANSWERED` (`Modules/Bridge/DriverModule.cs:462-473`). It moved answers off the window's path. It added
  no event a plugin hears.
- **The notices record** (`Driver/Platform/Notices.cs:6-60`): *Done and failed*, kinds `ended`, `expired`, `withdrawn`, `closed`,
  `answered` and `look`, in `data/notices.json`, written by any process on the home. A machine record, never on a wire to a shared host.

### 1.6 The fabric's events

The fabric events design §C5 names the events (`docs/2026-10-16-fabric-events-design.md:785-799`), and `ChainEvents` holds them
(`Driver/Platform/Chain.cs:5-61`): the wakes (`refs`, `home`, `ledger`, `press`, `focus`, `plugin.step`, `harness`, `exit`, `timer`)
and the end judgement's facts (`work.step`, `work.held`, `work.ended`, `work.tidied`).

- **An event is a wake, never a fact** (the design §A1 point 1, `:50-52`): whoever acts reads the truth itself.
- **§C4 designed a point `work/ended`**, an observation every listening plugin gets when a unit's end is reached (`:721-722`), fed by
  §C6's link 3 (`:817`). **It was never built.** Link 3 is a row with no binding (`Chain.cs:170`; D173's FABRIC-CHAIN2 note,
  `docs/decisions/D173.md:399-401`); the row that was to add the point, FABRIC-PLUGSTEP1b, was superseded by part D
  (`docs/task-archive.md:15807-15811`); `HookPoints.All` has no `work/ended` (`HookPoints.cs:58`). No production code emits
  `work.ended`: `Chain.cs` alone names it.

### 1.7 What an event carries today

| Point | Frame (`HookFrames`, `HookPoints.cs:103-175`) | Free words or machine paths |
|---|---|---|
| `session/ended` | `session`, `quest`, `repository`, `state`, `adapter`, `account`, `byPerson`, `note` (`:114-125`) | `note` is the conclusion's sentence; on a failure it is an exception's message (`Driver.cs:2927`, `Driver/Loop/DriverContinuations.cs:539`, `Driver/Loop/DriverIntakes.cs:454`), which may name a path. `account` is a credential profile's name (`Driver/Sessions/Domain/SessionEnded.cs:13`) |
| `state/changed` | `scopes`, `sessions` (`:166-174`) | none |
| `quest/consider` | the quest's id, title, from and to, `repository`, `workspace`, `root` (`:105-112`) | `root` is a checkout's path; `title` is authored |
| `work/land`, `work/state` | the branch, the commits' subjects, `root`, `origin` (`:127-164`) | `root`; commit subjects |

A park is a session end: a session that stops to wait on the person concludes `awaiting-person` (`Driver/Sessions/Domain/Observation.cs:174`,
`:289`) and reaches `session/ended` with that state, as `failed` and `stopped` do (`Driver.cs:2896-2927`). These frames go to a
plugin's process on this machine, where a path is the machine's own. None was made to leave it.

### 1.8 The outside session today

The steward watches by polling (the row). Daoris pushes nothing to a process it did not start: the bus is the shell's
(D169 point 4), and the hook wire speaks only to the plugins the loop started.

## 2. What Claude Code documents for waking a session without a poll

Read 2026-10-24 from Claude Code's documentation (`https://code.claude.com/docs/en/cross-session-messaging` and
`https://code.claude.com/docs/en/channels`, `https://code.claude.com/docs/en/channels-reference`). Nothing here was run on this machine.

### 2.1 The cross-session inbox

- **The socket.** A session with messaging on binds an inbox: a Unix domain socket on macOS and Linux, **a per-session named pipe on
  native Windows**. Messaging is on with nothing to enable from v2.1.234 on native Windows (v2.1.224 elsewhere). `/status` shows it
  as `Peer address`.
- **Handed to the session's own children.** Claude Code exports `CLAUDE_CODE_MESSAGING_SOCKET` and a per-session
  `CLAUDE_CODE_MESSAGING_TOKEN` to its hooks and Bash commands, before any hook runs, `SessionStart` included.
- **Posting.** A script posting to its own session's socket sends `{"type":"auth","token":"<token>"}` as its first line. **On native
  Windows the line is required**: a connection whose first line is not a valid auth line is closed and nothing it sent is delivered.
  A connection that has not sent a complete line within 30 s is closed, so a poster opens it only when its message is ready. The
  documentation gives the auth line's shape and not the message line's.
- **Delivery.** During a turn the message is read between tool calls; **an idle session starts a new turn with it**. Once delivered it
  counts toward usage as a typed prompt does.
- **Own-child messages.** Where no `crossSessionInbound` value applies, a message verified as from the session's own child is
  delivered. On native Windows the token in the auth line is the only verification. Unverified, it is treated as asserting no permission
  class, so a session that bypasses permission prompts holds it for approval.
- **Limits.** Plain text; about a million characters; a rapid burst to one session is refused at the sender; identical repeats within a
  short window are dropped; repeated messages per sender are rate-limited; at most 50 accepted messages queue for Claude; at most 100
  are held.
- **What a message may do.** It cannot approve anything, cannot change configuration, and a command in its text is not run. The
  receiving session's permission prompts still fire.

### 2.2 MCP channels

- **Research preview.** A channel is an MCP server over stdio that declares `capabilities.experimental['claude/channel']` and emits
  `notifications/claude/channel` with `content` and `meta` (identifier keys only; a key with a hyphen is dropped silently). Events while
  Claude is busy are delivered together on the next turn. Claude Code acknowledges nothing: an event to a session that did not
  register the server as a channel is dropped with no error.
- **Registration.** A server pushes only if named for the session with `--channels`, which during the preview accepts only an
  allowlist's plugins. A server off it needs `--dangerously-load-development-channels server:<name>`, which asks a confirmation and
  **works only in an interactive session**: under `-p` or the Agent SDK the flag is ignored. Organization policy (`channelsEnabled`)
  can block every channel.
- **Protocol.** Claude Code does not register a channel server that negotiates protocol revision 2026-07-28. The knowledge server
  answers that revision's probe *method not found* and falls back to `initialize` (`Mcp/Program.cs:168-170`).
- **Windows support is not stated.** The channel pages name no platform; the shipped channels need Bun, and iMessage macOS.

### 2.3 What is unmeasured on this machine

Everything in §2. In particular: which Claude Code is installed; whether its inbox binds a named pipe here and what `/status` says;
**the message line's shape**, which the documentation does not give; whether a stand-in child's post with the token is delivered,
held or refused under the session's permission mode; whether an idle session starts a turn; what a burst and a repeat cost; whether
a relay started from the session's Bash or its `SessionStart` hook outlives the session on Windows; whether a development channel
registers on native Windows; and whether the .NET MCP SDK the knowledge server uses can declare an experimental capability and send a
raw notification. **EVENTHOOK1a measures each before anything depends on it** (§7).

## 3. The shape

```text
 Driver (the loop)                    the plugin `event-relay`              outside Daoris
 ─────────────────                    ──────────────────────────            ───────────────────────────────────
 EndingsAsync ─┐                      hook/work/event ─▶ filter per ──▶ pipe ──▶ relay (a child of the session)
 (later kinds) ├─▶ HookSet.Tell ─▶ teller ─▶ (one process)   subscriber           │ auth line + message line
               ┘    queue 256              answers at once                        ▼
                                           serves a pipe, address in its     the session's inbox ─▶ a new turn
                                           data folder                       (or: the knowledge server's channel)
```

1. **The driver tells one new observation point, `work/event`,** to the plugins that listen, off every path that answers anyone (§4).
2. **A plugin, `event-relay`, keeps the subscribers.** It serves a local endpoint (a named pipe on Windows, a Unix socket elsewhere),
   writes its address and a key into its own data folder, and pushes each event, filtered, to each connected subscriber (§5).
3. **A subscriber connects and is pushed to.** The first is a relay the outside session starts: a script shipped in the plugin's folder,
   run from the session's Bash or its hook, so it holds the session's inbox address and token as its own environment. It posts each
   batch into the inbox (§6.1). The second, opt-in, is the knowledge server's channel (§6.2).

**Why the plugin serves and the session's child posts.** The inbox's token is the session's, handed to its children alone (§2.1), and
on Windows it is the only thing that verifies a message as the session's own. Daoris holding it would make Daoris the keeper of another
tool's secret and would reach into a process it did not start. The plugin process is in the agents' job (§1.3), and is no child of the
session. So the plugin never learns the inbox, and the relay never learns anything of Daoris's but the plugin's address and key.

**Why a plugin at all.** It is D64's one way to extend the loop without code loaded into it, and D169 point 9's way a plugin observes
the pipeline: beside it, never able to hold, change or refuse anything. The driver gains a point; serving subscribers and speaking
another tool's protocol stay outside it, where a person can switch them off on the Plugins page.

## 4. The hook point `work/event`

### 4.1 The point

- **An observation and a loop point.** `work/event` joins `HookPoints.All` last and `HookPoints.Loop` (§1.1), contained as
  `session/ended` is and told off the path as `state/changed` is. `protocolVersion` stays 1; an older build ignores a plugin's claim to
  the point (D148 point 8).
- **One point, many kinds.** Each point costs about twelve places (§1.1). The kinds are fields of one frame, and a kind is added as a
  value, never as a point.
- **It replaces §C4's `work/ended`.** The unbuilt point (§1.6) becomes the kind `work.ended` on this one, and §C6's link 3 tells it
  here once link 1 emits (the D173 note).

### 4.2 The kinds

Named as §C5 names events, `<subject>.<what>`. Each says what moved, never what it is now.

| Kind | Says | Source | Loops | Row |
|---|---|---|---|---|
| `session.ended` | a session ended, in an end state: `completed`, `failed`, `stopped`, `awaiting-person`, … | `Driver.EndingsAsync`, each concluded `SessionEnded`, in the order drained (`Driver.cs:1617-1622`) | both | 1b |
| `work.ended` | a unit's end was reached (landed by its cycle), or no longer | §C6's link 3, once link 1 emits `work.ended` | both | 1d |
| `quest.published`, `quest.closed` | a quest appeared or closed in the ledger | the look's quest snapshot, compared with the last; later the `ledger` wake (FABRIC-EVENT3a) | both | 1d |
| `person.asked` | a terminal's press waits for the window's Confirm | the instance pipe's confirmation, as it is made (CONFIRMWAIT1) | the shell's | 1d |

The row's six things map onto these: *ended*, *waiting on the person* and *failed* are `session.ended` with its `state`; *landed* is
`work.ended`; *a quest published or closed* and *a Confirm asked* are the last two. A subscriber chooses by kind and state (§5.2).

**Latency, honestly.** `session.ended` is told at the look's end-taking, as `session/ended` is. A quest an agent publishes is written
by its own connector's process, never through the desktop's host (the fabric design §A2's correction, `:101-105`), so until the change
stream lands it is seen at the next look: up to `pollSeconds`, 15 s by default. Pushing it is still no poll by the subscriber.

### 4.3 The frame: ids and codes, never a secret, never a machine path

```json
{ "run": "r-6c0f1a", "seq": 12, "missed": 0, "kind": "session.ended", "at": "2026-10-24T11:02:03Z",
  "session": "s-4b62a9fe", "quest": "48f8ed9121ec", "repository": "Daoris", "workspace": "games",
  "state": "completed", "byPerson": false, "limit": false, "adapter": "claude-code-acp" }
```

- **A closed list of fields per kind**, built in `HookFrames.Event` and nowhere else, every name spelled there.
  `session.ended`: `session`, `quest?`, `repository`, `workspace?`, `state`, `byPerson`, `limit`, `notOffered`, `adapter`.
  `work.ended`: `repository`, `workspace?`, `unit`, `ended` (true, or false where it no longer holds). `quest.*`: `quest`, `to`,
  `from?`, `state`. `person.asked`: `confirmation`, `route`.
- **No words.** `note`, a title, a commit subject, a press's words: never. A sentence may name a path (§1.7), and authored words handed
  to an agent's session are a prompt someone else wrote. The subscriber reads the words it needs through its own doors (§8).
- **No machine path, no secret.** `root`, `origin`, `account`, a home, a key: never. Every value is an id (a session, a quest, a
  repository or workspace name as registered), a code from a closed set, a boolean, a count or a UTC time.
- **Held by a test, seen failing**: a census over `HookFrames.Event` for every kind lists its fields against the allow-list and fails on
  a value shaped as a path (a drive letter, a leading `/` or `\\`, a backslash) or longer than 200 characters. The frame is at most
  4 KB.

### 4.4 Order and the rate bound

- **One order.** `Tell` takes an event, stamps it with the set's `run` (random, minted when the set is made) and the next `seq`, writes
  it to a bounded queue and returns: no caller waits on a plugin. One teller tells each event to every listening plugin, awaiting them
  all before the next, as `state/changed`'s does (§1.2). So every plugin hears every event it hears in `seq` order, unlike
  `session/ended`'s two at once (§1.2). `EndingsAsync` tells in the order it drained.
- **Bounded.** At most 256 events wait (the work wire's `EventsWaiting`, `Driver/Plugins/Domain/WorkBounds.cs:12-13`), the oldest
  dropped first. A drop is never silent: the next event told carries `missed`, the count dropped since the last told, and `seq` still
  counts them, so a gap shows. A plugin slow to answer holds the next event by its patience at most (10 s).
- **What it is not.** No replay and no store: an event is a wake (§1.6). A subscriber that missed one reads the truth again.

### 4.5 A loop with no subscriber

A set with no plugin listening at `work/event` tells nothing. **Its teller is never started** and `Tell` keeps nothing, so the loop's
report, its log and its timings are what they were. Nothing is written to the home. A plugin on the point with no subscriber connected
answers each event and drops it (§5.3).

## 5. The plugin `event-relay`

### 5.1 What it is

A plugin of the existing shape: `plugin.json` with `hooks.points: ["work/event"]` and a command, `node plugin.mjs`; no steps, no
servers. It is written first as an example under `examples/plugins/event-relay/`, where this repository's tests can run it, as
`hold-by-title` is the rehearsals' fixture (D120 point 3). Offering it as Daoris's own from Daoris.Plugins is a quest to that
repository, never an edit (§9).

### 5.2 The endpoint, the address and the filter

- **The endpoint.** At its handshake the plugin listens on a named pipe on Windows (`\\.\pipe\daoris-event-relay-<random>`), a Unix
  socket in its data folder elsewhere: Node's `net` serves both.
- **The address.** It writes `subscribe.json` into `DAORIS_PLUGIN_DATA`: `{ pipe, key, run }`, the key 32 random bytes in base64,
  minted at each start, the file written atomically and removed at `shutdown`. The data folder is under the home, readable by its
  user. The key admits a subscriber to read events and nothing else; it is not the person's key and opens no door of Daoris's.
- **The hello.** A subscriber's first line is `{ "key", "kinds": [...], "states"?: [...], "repositories"?: [...],
  "workspaces"?: [...], "sessions"?: [...] }`. A wrong key closes the connection. The answer is `{ "run", "seq" }`, the last event told,
  so a subscriber knows where it joined. Absent lists take everything of the kinds named.
- **The filter is the plugin's.** The driver tells every kind to every plugin on the point; the plugin forwards to each subscriber what
  its hello takes. A filter in the driver would put a subscriber's choice into Daoris's records, which this keeps out.

### 5.3 Ordering, rate and a subscriber gone or slow

- **In order, per subscriber.** One writer per connection, in `seq` order, newline-delimited JSON, each event the driver's frame
  unchanged.
- **Bounded per subscriber.** At most 64 waiting; past it the oldest go, and the next event sent carries the subscriber's own `missed`.
  A write that has not completed within 5 s marks the subscriber slow, and it is dropped from then on until it reconnects.
- **The driver is never held by a subscriber.** The plugin answers `hook/work/event` as soon as it has queued the event for each
  subscriber; nothing waits on a write.
- **A subscriber gone** (its connection closed or broken) is forgotten. A subscriber that comes back says hello again and is answered
  the current `run` and `seq`: a new `run` means Daoris's loop restarted, and nothing is replayed.
- **The plugin gone.** The loop restarts a loop point's process at a later look (`Hooks.cs:722-751`), which writes a new address. A
  subscriber whose connection breaks reads `subscribe.json` again on a back-off of 1, 5 and 15 s, then every 60 s, and says so on its
  stderr. That is a reconnection, bounded, never a poll of Daoris's state.

### 5.4 Its words and its log

Its stderr says each subscriber's connect, filter, drop and departure, under `plugin:event-relay` on the console (§1.3). The machine log
keeps the set's `plugin.called` for each `work/event` as it does for `state/changed` (`Hooks.cs:1116`), with no subscriber in it.

## 6. The delivery adapters

### 6.1 The inbox relay (first)

`relay.mjs`, in the plugin's folder, is started by the outside session itself: from its Bash in the background, or by a `SessionStart`
hook the person adds. Which one works on Windows is measured (§7).

1. **It reads only what is its own.** `CLAUDE_CODE_MESSAGING_SOCKET` and `CLAUDE_CODE_MESSAGING_TOKEN` from its environment, and the
   plugin's `subscribe.json`, found from `DAORIS_HOME` or a `--home` argument as `<home>/plugins/.data/event-relay/subscribe.json`.
   It never sends the token to Daoris, and no file of Daoris's holds it.
2. **It subscribes** with the filter it was started with (by default `session.ended` of every state, and `person.asked`).
3. **It batches.** Events arriving within 10 s go as one message, at most one message per 10 s: the inbox refuses bursts, drops
   identical repeats and queues at most 50 (§2.1). Each event is one line of fixed English words built from its codes, such as
   `Daoris: session s-4b62a9fe ended completed (quest 48f8ed9121ec, Daoris) [r-6c0f1a #12]`. The words are a machine's to an agent,
   not the window's chrome, so they are not translated. A `missed` count is said as its own line. The `run` and `seq` keep two batches
   from ever being identical.
4. **It posts as the documented poster does.** It opens the inbox only when the batch is ready, sends the auth line, then the message in
   the shape EVENTHOOK1a records, and closes. A refusal is said on its stderr and the batch is dropped: events are wakes.
5. **It ends** when the inbox refuses its connection twice running (the session is gone), or its parent process has exited.

What this adapter cannot promise: an idle session's new turn, the inbound class under the session's permission mode, and the message's
shape are Claude Code's, measured in EVENTHOOK1a.

### 6.2 The knowledge server's channel (second, opt-in)

The knowledge server is a stdio MCP server, one per session that registers it (`Mcp/Program.cs:146-167`). Behind an opt-in variable on
its registration, `DAORIS_CHANNEL_EVENTS`, with its kinds as the value (a new row of the wiring census, §1.3), it:

- declares `experimental['claude/channel']` with instructions saying the events are wakes and naming the tools that read their truth;
- subscribes to the plugin as the relay does, and sends each event as `notifications/claude/channel`, `content` the line of §6.1 and
  `meta` the frame's ids and codes under identifier keys (`kind`, `seq`, `session`, `quest`, `repository`, `state`);
- declares nothing, and subscribes to nothing, without the variable. Daoris never sets it on a connector it hands a session it starts:
  those run non-interactively, where the development flag is ignored (§2.2).

It is second and opt-in because the channel is a research preview that needs the development flag in an interactive session, its
Windows support is not stated, Claude Code acknowledges nothing, and the SDK's support is unmeasured (§2.3). It is built only once
EVENTHOOK1a has seen a development channel register and deliver on this machine.

## 7. Measured first: EVENTHOOK1a

On the installed Claude Code on this machine, before any row depends on it. The probes are scripts under
`src/Daoris.Cli/test/fixtures/event-hook/` (a stand-in poster, a one-file channel server), run by hand from a real interactive
session. No test runs them, and no test starts a real console.

| # | Measures | Evidence kept |
|---|---|---|
| M1 | `claude --version`; `/status`'s `Peer address` form on native Windows | the version and the address's form, never the address |
| M2 | a child started from the session's Bash posts the auth line and one message line: the line shapes accepted; delivered, held or refused under `default`, `acceptEdits` and bypass modes | each mode's outcome; the accepted shape |
| M3 | the same post to an idle session: does a turn start | yes or no, with the turn's first line |
| M4 | two identical posts, and ten in two seconds: what is dropped or refused | the counts |
| M5 | a relay started from Bash in the background, and one from a `SessionStart` hook: does each live past the session's end | each, with how it ended |
| M6 | a one-file channel server registered in `.mcp.json` with `--dangerously-load-development-channels server:<name>`: registers and delivers on native Windows, to an idle session | the startup notice's words; delivered or not |
| M7 | the knowledge server's .NET MCP SDK: can it declare `experimental['claude/channel']` and send a raw notification; does Claude Code register it after the revision fallback | a small in-process check's result |

The evidence is a dated record, `docs/2026-10-xx-event-hook-measurements.md`, routed in `docs/README.md`: versions, the steps, each
outcome, and no machine path, token or address. A probe that cannot run is recorded as such, and the rows that depend on it wait.

## 8. What a subscriber may do with an event

- **Read it as a wake.** It says what moved. The truth is read again through the subscriber's own doors: its connector's tools
  (`quest_list`, the session reads), `daoris-driver sessions` and the CLI's reads.
- **Act through its own doors, as an agent.** What it may do after reading is what it could do before the event. An event grants
  nothing.
- **Never a press of the person's.** D156 stands: no process in this chain holds the person's key (§1.3), and the plugin's endpoint
  opens no door of Daoris's. D201 stands: the plugin is contained, so nothing it starts is covered by the standing confirmation. The
  relay is the outside session's own child, and what that session may press is what it could press before (D201 point 4, and
  STEWARDROLE1's roles where they land). A message in the inbox cannot approve anything there either (§2.1).
- **No instruction rides an event.** Its words are fixed and its fields are codes (§4.3), so nothing authored reaches the receiving
  session through it.

## 9. Rows, in build order, with their proofs

| Row | Lanes | What | Proof |
|---|---|---|---|
| **EVENTHOOK1a** | the steward, a real interactive session | M1-M7 (§7) | the measurements record, every probe's outcome or why it did not run |
| **EVENTHOOK1b** | driver | `work/event` with `session.ended`: the point in every place §1.1 lists; `HookFrames.Event`; `Tell` with `run`, `seq`, `missed`, its queue and one teller; `EndingsAsync` tells each ending; the kit's sample and gloss | (1) **no subscriber, unchanged**: a loop over a stand-in run with no plugin on the point makes the same report and log as before, and its teller never starts; (2) **once and in order**: a stand-in loop concludes three endings, two judged at once beside the look, and a fake channel hears each once, in drain order, `seq` contiguous; (3) a full queue drops the oldest and the next event says `missed`; (4) the frame census of §4.3, seen failing on a `note` added and on a path-shaped value |
| **EVENTHOOK1c** | examples, driver | `event-relay`: the endpoint, `subscribe.json`, the hello, the filter, the per-subscriber bounds; `relay.mjs` with the inbox adapter in the shape M2 recorded | `node --test` in the plugin's folder: **a stand-in loop** (a script speaking the hook wire as `HookPeer` does) tells three endings to the real plugin process; a relay connects with a filter; **a stand-in inbox** (a script serving a pipe, checking the auth line and recording lines) receives the session's end **once and in order**; a wrong key is closed; a slow subscriber is dropped and the plugin still answers at once; a subscriber gone and back is answered the current `seq`. And the driver's own half: a Process-lane class starts the example through `HookProcess` beside a stand-in inbox and sees one post. Every script is under the tests, spawned hidden: no window, no console, no real agent |
| **EVENTHOOK1d** | driver, modules | the later kinds: `work.ended` (link 3 bound, once link 1 emits), `quest.published` and `quest.closed` (the look's quest snapshot compared), `person.asked` (the shell's pipe, as a confirmation is made) | per kind: told once on its change and never on a standing fact; the census extended; `person.asked` never told by a headless loop |
| **EVENTHOOK1e** | service | the knowledge server's channel behind `DAORIS_CHANNEL_EVENTS` (§6.2), after M6 and M7 | an in-process MCP client sees the capability only with the variable, and one notification per event of the kinds named; without it, the server's capabilities and tools are as before; the wiring census row |
| **EVENTHOOK1f** | a request | a quest to Daoris.Plugins to offer `event-relay` as Daoris's own (D120 point 3), with the relay in the package | that repository's answer |

Each row records its plans and findings as they land. A row that builds the driver's half runs only its own classes, filtered.

## 10. Rejected

- **The driver posting into the inbox itself.** It needs the session's token, which is handed to the session's children only (§2.1);
  Daoris would keep another tool's secret and reach into a process it did not start.
- **The plugin starting the relay.** It is in the agents' job (§1.3) and no child of the session, so on Windows its posts are
  unverified (§2.1).
- **The subscriber reading a file the plugin appends, or calling Daoris on a clock.** That is the poll this removes, moved.
- **Forwarding the shell's bus.** D169 point 4: the bus is the shell's alone, and a headless loop has none (§1.2).
- **A point per kind.** About twelve places each (§1.1), for kinds a field distinguishes.
- **Carrying `note`, titles or commit subjects.** Paths and authored words (§1.7, §4.3). The subscriber reads them through its doors.
- **A replay on reconnection.** An event is a wake (§1.6); a store of events would be a second record of what the ledger and the
  records already hold.
- **A filter in the driver.** It would keep a subscriber's choice in Daoris's records (§5.2).
- **The channel first.** A research preview behind the development flag, interactive only, with Windows support not stated (§2.2).

## 11. What the gates do not cover

This is a design and nothing is built. The code was read at `5e9a563bd` and each statement in §1 names its lines; the quick checks
(`doc-duplicates`, `dogfood`) check this document's shape and links, none of its words. Claude Code's behaviour in §2 is its
documentation as read on 2026-10-24, and none of it was run here (§2.3). The plugin, the relay and every bound in §4 and §5 exist only
as rows.
