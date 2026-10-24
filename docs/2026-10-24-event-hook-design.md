# The work cycle's events reach an outside agent's session by a plugin, pushed, never polled (EVENTHOOK1; proposed, 2026-10-24)

**Standing:** proposed, round 2, under D191. Round 1 was written by Claude Opus 5.5 from the code at `5e9a563bd` and read at
`71e08c3c0` by the independent Opus foundation reader alone, its read verbatim in `docs/2026-10-24-event-hook-reads.md`: 16 findings,
7 blocking, another round. Codex's read is owed, its credits out. By D191's note of 2026-10-24 this design, which sends Daoris's events
outside it, is not accepted on one maker's read. Round 2 is written by Claude Opus 5.5 from the code on main at `5d19f6b3a`, where
nothing round 1 or the read cites has changed (an empty `git diff --stat 5e9a563bd` over `src/Daoris.Desktop`, `src/Daoris.Service`,
`tools/knowledge-server.mjs` and `examples/plugins`). It answers every finding in §12, each checked against that code. Dated notes amend
D173 (the fabric events design's decision), D119 and D120, each proposed with this and rewritten to match round 2. Nothing here is built.

The row EVENTHOOK1 in `TASKS.md` carries the case. The steward watches the install by polling its sessions in a shell loop it
re-arms every half hour. A plugin hooked into the work cycle should push the fabric's events (a session ended, waiting on the person,
failed, landed; a quest published or closed; a Confirm asked) to a subscriber outside Daoris, the first being a Claude Code session.
This is the first real test of a plugin listening to Daoris's event pipeline.

**What round 2 changed, in short.** The session kinds come from the attention watch, where a park is seen, not from `session/ended`,
where it never arrives (F1). *Landed* is its own kind, told where the install's landing keeps a branch it made, not `work.ended` (F2).
The inbox's variables are removed from every environment Daoris hands on (F3). The relay is no longer plugin code: it is Daoris's own
verb, holding none of the terminal's keys and pressing nothing (F4). The pipe's direction is reversed: the relay serves a pipe with
Daoris's instance pipe's access rules, so it answers at either elevation, and the plugin connects to it. The relay takes a connection
only from a process the holding driver started, and validates every frame against the one grammar the driver's census holds (F5, F6).
Only the lease's loop starts the plugin, and a trial's process is marked and refused (F7).

A path without a root is under `src/Daoris.Desktop/`; `Driver/` is `src/Daoris.Desktop/Daoris.Desktop.Driver/`, `Modules/` is
`src/Daoris.Desktop/Daoris.Desktop.Modules/`, `Host/` is `src/Daoris.Desktop/Daoris.Desktop.Driver.Host/`, and `Mcp/` is
`src/Daoris.Service/Daoris.Service.Mcp/`.

---

## 1. What is true today

Each statement was read at `5d19f6b3a` at the lines named.

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
  `HookPeer` and `HookProcess`, its kit entry and sample, the kit test's `ANSWERS` entry, its case in `PluginTrials`,
  `PluginsCommand.Cost`, Ask Daoris's `PointSaid`, the rows of `PluginKitTests` and the modules' `DriverModulePluginsTests`, the
  page's gloss in `src/Daoris.Web/src/plugins/PluginKit.tsx`, and `twins.md`'s answer row. So a point's lanes are the driver, the
  modules and the web.
- **The loop's points** are `quest/consider`, `session/ended` and `state/changed` (`HookPoints.Loop`, `:60-65`): a plugin on one of
  them is kept running beside the ticks in every loop, the lease's or not; a plugin on the work wire only where this loop holds the
  lease (`Hooks.cs:702-710`).
- **A plugin asks Daoris nothing.** The peer's reader takes one notification, `work/step`, and only from a process that declared it
  (`Hooks.cs:524-531`). A request from a plugin is neither answered nor errored, by design (D64 §7).
- **The handshake carries the home and the data folder** (`initialize`, `Hooks.cs:147-150`), and nothing of the loop's run.

### 1.2 The hook set: how the loop tells

`HookSet` (`Hooks.cs:617-1216`) keeps one process per wanted plugin, reconciled at each look (`:687-829`), and stops each with the loop.

- **`session/ended`.** `Driver.EndingsAsync` tells each concluded `SessionEnded` to the set (`Driver/Loop/Driver.cs:1615-1642`),
  `HooksAtOnce`, 2, at a time under the watch (`:1633-1637`, `:1695-1698`). Each call waits for `HookPeer.DefaultPatience`, 10 s
  (`Hooks.cs:116-117`), and its answer is never read.
- **`state/changed`.** `Tell` writes the change to a bounded queue and returns (`Hooks.cs:1096-1100`): at most `ChangesWaiting`, 64,
  the oldest dropped. **Its teller is started at the first change told, whether or not any plugin listens** (`:1099`). It tells each
  change to every listening plugin at once and awaits them all before the next (`:1109`), so a plugin slow to answer holds the others
  for its patience. A headless loop has no bus and tells this point nothing (`HookPoints.cs:61-64`).
- **A live plugin that never answers is never replaced.** Reconcile replaces a channel that is gone, changed or no longer wanted
  (`Hooks.cs:725-729`), never one that is alive and late. The work wire alone counts unasked exits and marks a plugin unsound after
  `WorkBounds.ExitsUnsound`, 3 (`Driver/Plugins/Domain/WorkBounds.cs:25`).
- **Events with no listener are neither told nor counted** (`Listening`, `Hooks.cs:1196-1206`).
- **Health without words.** Each start, call, failure and stop is a `plugin.*` line in the machine log (`Hooks.cs:612-615`; D119 point 5).

### 1.3 The attention watch: where a park is seen

- **A park is no ending.** Every conclusion that parks returns no `SessionEnded`: a driven start (`Driver.cs:2855-2865`, *A park is not
  an ending (STANDDOWN2)*), an intake (`Driver/Loop/DriverIntakes.cs:414-416`) and a resumed conversation
  (`Driver/Loop/DriverContinuations.cs:786-788`). Nor is a run swapped onto a changed setup, or one waiting on its own helper. So nothing
  of a park reaches `session/ended`.
- **`AttentionWatch.Observe`** (`Driver/Quests/Domain/Attention.cs:210-261`) is what sees one. It compares the active sessions with the
  last look's and says, once each: `Parked` (a session newly waiting on the person, a park the person answered seen apart,
  `:194-195`), `QuestParked` (a quest parked on its failed sessions, `:239-244`), and `Ended` for each concluded ending except the
  person's own stops, an account's limit, a refused choice and an ending its quest's park already says (`:246-258`). `Waiting` is an
  account's cool-off, which names accounts (`:227-228`). The first look is a baseline and says nothing (`:224`).
- **Both loops call it after each look**: the shell's (`Modules/Loop/DriverLoop.cs:916`) and the headless host's (`Host/Program.cs:972`),
  whatever the notify switch says (the switch is about being told). Each holds the loop's `HookSet` (`DriverLoop.cs:823`;
  `Host/Program.cs:925`).
- **The workspace** of an `Ended` is the one the session was filed under at the last look that saw it (`Attention.cs:256`); a
  `Parked` session's is on its `SessionView` (`Driver/Quests/Domain/Planner.cs:770`). A `QuestPark` names a quest, a repository, its
  last session and its strikes (`Driver/Trees/SessionGroups.cs:563-570`), no workspace.
- **An intake's repository is `ask #<id>`** (`DriverIntakes.cs:385`, `:416`, `:454`; `Planner.cs:733-737`). `SessionEnded` carries no
  workspace (`Driver/Sessions/Domain/SessionEnded.cs:14-16`).
- **A conversation's end** goes through the chat runner's `onEnded` (`DriverLoop.cs:892-900`), never `EndingsAsync`; the watch's
  `Answered` is a turn the person's own words started (`Attention.cs:32-36`).

### 1.4 A landing: where the install keeps a branch it made

- **Only the lease's holder lands** (`Driver.LandDueAsync`, `Driver.cs:1403-1410`): a `--share` loop drops its wakes.
- **Each try is kept once, with its code**, in `AutoLander.Tried` (`Driver/Trees/Landing/AutoLander.cs:648-672`): the due list's try,
  the session's note and the machine log's `LandingLine`. `landed` is a branch made and recorded, pushed where the rule's plugin names
  one; `advanced` a chain's branch moved on (`Driver/Trees/Landing/AutoLanding.cs:39-46`).
- **The end judgement** calls a landed branch the line does not hold yet `Handed` (`Driver/Trees/SessionTrees/EndJudgement.cs:263`),
  and `Ended` only once the line holds the work (`:241-258`). No chain event names `Handed`. Link 1 emits `work.held` alone
  (`Driver/Trees/RefWatch.cs:373`), and no open row makes it emit `work.ended`.

### 1.5 The plugin's process and its environment

`HookProcess.StartInfo` (`Driver/Plugins/HookProcess.cs:55-114`) starts the manifest's command:

- **in the plugin's install folder** (`:88`), its first word resolved by Tools (`:61-83`);
- **stdin and stdout the wire, UTF-8 without a mark** (`:90-97`); stderr relayed line by line under `plugin:<id>`;
- **over the environment the process inherits**: `DAORIS_PLUGIN_ID`, `DAORIS_PLUGIN_FOLDER`, `DAORIS_PLUGIN_DATA`, `DAORIS_HOME` and
  the tools' environment are set on top of it (`:101-106`). `Tools.Hand` calls `GitWiring.Hand` (`Driver/Agents/Tools.cs:679`, `:693`),
  which removes seven names from every child, the holder key among them (`Driver/Platform/GitWiring.cs:40-41`); a session's start
  alone also loses the four token variables (`:47`, `:98`). **Nothing removes `CLAUDE_CODE_MESSAGING_SOCKET` or
  `CLAUDE_CODE_MESSAGING_TOKEN`**: a Daoris started from a Claude Code session's shell hands both to its plugins and its agents.
- **Stripped at entry**: the desktop removes the holder key from its own environment first (`Daoris.Desktop.App/Program.cs:34`,
  `HolderKey.Strip`), and `daoris-driver` takes it into memory and removes it (`Host/Program.cs:345`, `HolderKey.Take`;
  `Driver/Sessions/Questions/SupervisorProof.cs:10-20`): the pattern for a variable one verb may read and nothing may pass on.
- **inside the agents' job.** Its start goes through `AgentStarts` as `ContainedKind.Plugin` (`HookProcess.cs:141-146`;
  `Driver/Platform/AgentStarts.cs:5-11`): the launcher is the process answered and the plugin its child, in the same job as the agents
  (D201 point 4). So a plugin's process and an agent's are one user's, in one job.

### 1.6 A second process of the same plugin

- **A Try** on an installed plugin starts it with the installed entry, so in the same data folder (`Driver/Plugins/PluginTrials.cs:58-78`;
  the page's route `Modules/Bridge/Routes/PluginsRoutes.cs:399`). It is told the kit's sample frames (`Driver/Plugins/PluginKit.cs:18-25`).
- **A `--share` loop** (`Host/Program.cs:20-27`) keeps every loop-point plugin running beside the lease's loop (§1.1).

### 1.7 Pipes, owners and elevation

- **The instance pipe** is the exemplar (`docs/2026-10-16-terminal-rpc-design.md:145-152`): the user's SID alone, the network denied,
  `FirstPipeInstance`, owned by the user's SID at either elevation (`Modules/Platform/InstancePipe.cs:400-410`). Its client takes a pipe
  owned by its user's SID or its token's default owner, and asks the system which process serves it (`GetNamedPipeServerProcessId`,
  `Driver/Platform/InstanceClient.cs:105-114`, `:196-211`).
- **PIPEOWNER1** (`docs/FIX-LOG.md:1506-1520`): a pipe left to the token's default owner is the Administrators group's under an elevated
  token, and a client that is not elevated was refused. The install has run elevated. A pipe made by Node's `net` gets the system's
  default descriptor; Node has no knob to set an owner or a user-only ACL.
- **The reverse works** (`docs/2026-10-23-terminal-trust-design.md:253-255`): an elevated process opens what one that is not elevated
  made, since an elevated token holds the user's SID enabled.
- **The lease** names its holder's process and start (`Driver/Platform/HomeLease.cs:31`, `:165`), checked alive against the system.

### 1.8 The pipeline the window reads, and the fabric's events

- **`STATE_CHANGED` says what moved, never what it is now** (`Modules/Platform/StatePublisher.cs:99-106`; D169 point 3). Shell-only
  (D169 point 4).
- **NOBLOCK1** moved answers off the window's path and added no event a plugin hears (`Modules/Bridge/DriverModule.cs:462-473`).
- **The notices record** (`Driver/Platform/Notices.cs:6-60`) keeps the attention watch's events on the machine.
- **`ChainEvents`** (`Driver/Platform/Chain.cs:5-61`) holds the wakes (`refs`, `home`, `ledger`, `nodes`, `press`, `focus`,
  `plugin.step`, `harness`, `exit`, `timer`) and the end judgement's facts (`work.step`, `work.held`, `work.ended`, `work.tidied`).
  An event is a wake, never a fact (the fabric events design §A1 point 1, `docs/2026-10-16-fabric-events-design.md:50-52`).
- **§C4's `work/ended` point was never built** (`Chain.cs:170`; D173's FABRIC-CHAIN2 note, `docs/decisions/D173.md:399-401`;
  `docs/task-archive.md:15807-15811`).

### 1.9 What an event carries today

| Point | Frame (`HookFrames`, `HookPoints.cs:103-175`) | Free words or machine paths |
|---|---|---|
| `session/ended` | `session`, `quest`, `repository`, `state`, `adapter`, `account`, `byPerson`, `note` (`:114-125`) | `note` is the conclusion's sentence, on a failure an exception's message (`Driver.cs:2927`), which may name a path; `account` a credential profile's name |
| `state/changed` | `scopes`, `sessions` (`:166-174`) | none |
| `quest/consider` | the quest's id, title, from and to, `repository`, `workspace`, `root` (`:105-112`) | `root` is a checkout's path; `title` is authored |
| `work/land`, `work/state` | the branch, `base`, `title`, `quest` with its title, `session`, the commits' subjects, `pullRequest`, `acceptedBy`, `tip`, `root`, `origin` (`:127-164`) | `root`; titles and commit subjects |

The attention watch's events carry a `Note` (a sentence) and, for a park, `NoteParts` (`Attention.cs:236`).

### 1.10 The outside session today

The steward watches by polling (the row). Daoris pushes nothing to a process it did not start: the bus is the shell's (D169 point 4),
and the hook wire speaks only to the plugins the loop started.

## 2. What Claude Code documents for waking a session without a poll

Read 2026-10-24 from Claude Code's documentation (`https://code.claude.com/docs/en/cross-session-messaging`,
`https://code.claude.com/docs/en/channels` and `https://code.claude.com/docs/en/channels-reference`). Nothing here was run on this machine.

### 2.1 The cross-session inbox

- **The socket.** A session with messaging on binds an inbox: a Unix domain socket on macOS and Linux, **a per-session named pipe on
  native Windows**. Messaging is on with nothing to enable from v2.1.234 on native Windows (v2.1.224 elsewhere). `/status` shows it
  as `Peer address`.
- **Handed to the session's own children.** Claude Code exports `CLAUDE_CODE_MESSAGING_SOCKET` and a per-session
  `CLAUDE_CODE_MESSAGING_TOKEN` to its hooks and Bash commands, before any hook runs, `SessionStart` included. Whatever those children
  start inherits them unless something removes them (§1.5).
- **Posting.** A poster sends `{"type":"auth","token":"<token>"}` as its first line. **On native Windows the line is required**, and the
  token is the only verification. A connection that has not sent a complete line within 30 s is closed. The documentation gives the
  auth line's shape and not the message line's.
- **Delivery.** During a turn the message is read between tool calls; **an idle session starts a new turn with it**. Once delivered it
  counts toward usage as a typed prompt does.
- **Own-child messages.** A message verified as from the session's own child is delivered; unverified, it asserts no permission class,
  so a session that bypasses permission prompts holds it for approval.
- **Limits.** Plain text; about a million characters; a rapid burst is refused at the sender; identical repeats within a short window
  are dropped; repeated messages per sender are rate-limited; at most 50 accepted messages queue; at most 100 are held.
- **What a message may do.** It cannot approve anything, cannot change configuration, and a command in its text is not run.

### 2.2 MCP channels

- **Research preview.** A channel is an MCP server over stdio declaring `capabilities.experimental['claude/channel']` and emitting
  `notifications/claude/channel` with `content` and `meta` (identifier keys only). Claude Code acknowledges nothing.
- **Registration.** Only an allowlist's plugins with `--channels`; any other server needs `--dangerously-load-development-channels
  server:<name>`, which **works only in an interactive session**. Organization policy can block every channel.
- **Protocol.** Claude Code does not register a channel server that negotiates revision 2026-07-28; the knowledge server falls back
  to `initialize` (`Mcp/Program.cs:168-170`).
- **Windows support is not stated.**

### 2.3 What is unmeasured on this machine

Everything in §2, and the facts round 2 rests on: whether a process started from the session's Bash, and its children, hold the two
variables; whether the relay's pipe is reached from a plugin at either elevation, and the relay can read its client's process and
ancestry across elevations; the sustained per-sender rate at the relay's own pace; the launcher relay of the knowledge server.
**EVENTHOOK1a measures each before anything depends on it** (§7).

## 3. The shape

```text
 the loop (lease holder)                 the plugin `event-relay`                outside Daoris
 ───────────────────────                 ──────────────────────────               ───────────────────────────────────────
 AttentionWatch.Observe ─┐                                                       relay: `daoris-driver events relay`
 AutoLander.Tried ───────┼─▶ HookSet.Tell ─▶ its teller ─▶ hook/work/event         (a child of the session, holding no
 (later kinds) ──────────┘   per plugin       │ answers at once                     Daoris key, pressing nothing)
                                              ▼                                    serves a user-owned pipe, writes
                              watches subscribers/ ─▶ connects ──▶ pipe ──▶ checks    subscribers/<pid>.json
                              in its data folder                             the client, validates each frame
                                                                                  │ auth line + message line
                                                                                  ▼
                                                                             the session's inbox ─▶ a new turn
```

1. **The driver tells one new observation point, `work/event`,** to the plugins that listen, off every path that answers anyone,
   from the lease's loop only (§4).
2. **A plugin, `event-relay`, carries the events to the subscribers.** It watches a folder in its data folder where a subscriber
   says it is there, connects to each subscriber's pipe as a client, and pushes each event, filtered by what the subscriber asked (§5).
3. **The subscriber is Daoris's own verb**, started by the outside session from its hook or its Bash, so it holds the session's inbox
   address and token as its own environment. It serves the pipe, takes a connection only from a process the holding driver started,
   validates every frame against the frame's grammar, and posts fixed words into the inbox (§6.1). The knowledge server's channel is
   second and opt-in (§6.2).

**Why the subscriber serves and the plugin connects.** A pipe Node makes has the system's default descriptor, which an elevated
plugin's pipe gives to the Administrators group and a relay that is not elevated cannot open (PIPEOWNER1, §1.7). The relay is Daoris's
own .NET code and serves its pipe with the instance pipe's rules: the user's SID owner and alone, the network denied. A plugin at
either elevation holds that SID, so it connects to that pipe as the reverse case does (§1.7).

**Why the relay is Daoris's verb, not the plugin's script.** The outside session runs the relay outside every Daoris job, where D201
point 4 covers a terminal's presses with the standing confirmation, and with the terminal's environment. A script shipped in the plugin's
installed folder would be code that any agent (one user, one job, §1.5) and any package update can rewrite, run uncontained. The verb is
the install's own build, the one every terminal verb already is. It is not in any plugin's folder. It removes every Daoris key from
its environment at entry, opens no door of Daoris's, and makes no press (§6.1).

**Why the session's child posts.** The inbox's token is the session's, handed to its children alone (§2.1), and on Windows it verifies
the poster. Daoris holding it would make Daoris the keeper of another tool's secret. Round 2 also keeps the token out of Daoris when
Daoris itself was started from that session's shell (§6.3).

**Why a plugin at all.** It is D64's one way to extend the loop without code loaded into it, and D169 point 9's way a plugin observes the
pipeline: beside it, never able to hold, change or refuse anything. Carrying events to subscribers stays outside the driver, where a
person can switch it off on the Plugins page.

## 4. The hook point `work/event`

### 4.1 The point

- **An observation and a loop point, told by the lease's loop alone.** `work/event` joins `HookPoints.All` last and `HookPoints.Loop`,
  contained as `session/ended` is and told off the path as `state/changed` is. Unlike the other loop points, a plugin on `work/event` is
  wanted only where this loop holds the lease (the work wire's rule, `Hooks.cs:708`), so a `--share` loop starts no second process of
  it (F7). `protocolVersion` stays 1; an older build ignores a plugin's claim to the point (D148 point 8).
- **One point, many kinds.** The kinds are values of one frame's `kind`, and a kind is added as a value, never as a point.
- **The handshake says where the plugin joins.** For a plugin at `work/event`, `initialize` gains `{ "events": { "run", "seq" } }`,
  the set's `run` and the last `seq` told (0 before any). A field a plugin does not read changes nothing (F8). A trial's handshake says
  `run: "sample"` (§5.4).
- **It replaces §C4's `work/ended`.** The unbuilt point becomes a later kind of this one, behind a row that owns link 1's emission (§4.2).

### 4.2 The kinds

Named as §C5 names events, `<subject>.<what>`. Each says what moved, never what it is now.

| Kind | Says | Source | Row |
|---|---|---|---|
| `session.parked` | a session newly waits on the person | the attention watch's `Parked` (`Attention.cs:231-237`), both loops after each look | 1b |
| `quest.parked` | a quest parked on its failed sessions here | its `QuestParked` (`:239-244`) | 1b |
| `session.ended` | a session ended in a state the person did not ask for: `completed`, `failed`, `stopped`, … | its `Ended` (`:246-258`), which leaves out the person's own stops, an account's limit, a refused choice, and an ending its quest's park says | 1b |
| `work.landed` | the install landed a session's work as a branch the line does not hold yet, or moved a chain's branch on | `AutoLander.Tried` where its code is `landed` or `advanced` (`AutoLander.cs:648-672`), the lease's holder alone (`Driver.cs:1405`) | 1b |
| `quest.published`, `quest.closed` | a quest appeared in the ledger, or its state became `Done` or `Declined` | `service.SnapshotAsync` at the look (`Driver.cs:917`), the quests to this home's registered repositories, compared by id and state with the last look's; a quest leaving the snapshot is not *closed* unless its state says so | 1d |
| `person.answered` | a terminal's press the person was asked to Confirm was confirmed, refused or withdrawn | the shell's pipe, as a confirmation ends (CONFIRMWAIT1's record of it, `TASKS.md:384`) | 1d |
| `work.ended` | the line holds a unit's work | link 1's emission of `work.ended`, which no row owns yet (§1.4) | a later row |

- **The row's six things map onto these.** *Waiting on the person* is `session.parked` (and `quest.parked`); *ended* and *failed* are
  `session.ended` with its `state`; *landed* is `work.landed`, a branch the install landed and nobody has merged yet (`Handed`); *a quest
  published or closed* is the two quest kinds; *a Confirm asked* becomes `person.answered`. A press only the person makes is no wake for
  the steward; its answer is, since an asker that left finds the answer in the request's record.
- **Told once.** The watch says each park, quest park and ending once, and its first look is a baseline (§1.3), so a restart floods
  nothing. `AutoLander.Tried` keeps each try once. The quest kinds compare with the last look's snapshot, and the first look is a
  baseline too.
- **Not told:** the watch's `Waiting` (it names accounts), a conversation's end and the watch's `Answered` (the person's own, through
  the chat runner, §1.3), and the person's own landing by *Accept* (their own press). A later row may add a kind; none changes a point.
- **Latency, honestly.** Every kind is told at the look's end, after the watch observes: up to `pollSeconds`, 15 s by default, after the
  fact, and sooner where a wake runs the look. Pushing it is still no poll by the subscriber.

### 4.3 The frame: ids and codes, never a secret, never a machine path

```json
{ "run": "r-6c0f1a", "seq": 12, "missed": 0, "kind": "session.parked", "at": "2026-10-24T11:02:03Z",
  "session": "s-4b62a9fe", "quest": "48f8ed9121ec", "repository": "Daoris", "workspace": "games" }
```

- **A closed list of fields per kind**, built in `HookFrames.Event` and nowhere else, every name spelled there, and **one grammar**,
  `WorkEventGrammar`, which both the driver's census and the relay's check read (§6.1):
  - `session.parked`: `session`, `quest?`, `repository?`, `ask?`, `workspace?`.
  - `quest.parked`: `quest`, `session?`, `repository`, `strikes?`.
  - `session.ended`: `session`, `quest?`, `repository?`, `ask?`, `workspace?`, `state`, `adapter`.
  - `work.landed`: `session`, `quest?`, `repository`, `workspace?`, `code` (`landed` or `advanced`).
  - `quest.published`, `quest.closed`: `quest`, `to`, `from?`, `state`.
  - `person.answered`: `confirmation`, `route`, `answer` (`confirmed`, `refused` or `withdrawn`).
- **An intake names its ask, not a repository.** Where the watch's repository is `ask #<id>`, the frame carries `ask: "<id>"` and no
  `repository` (§1.3). A frame has one or the other.
- **The workspace's source is said.** `session.ended`'s is the watch's (`Attention.cs:256`); `session.parked`'s the session's
  `SessionView.Workspace`, which `Observe` sets on the event as it does for `Ended`; `work.landed`'s the due entry's
  (`AutoLander.cs:670`). `quest.parked` has none and says none.
- **No words.** `Note`, `NoteParts`, a title, a commit subject, a press's words: never. A sentence may name a path (§1.9), and authored
  words handed to an agent's session are a prompt someone else wrote. The subscriber reads the words it needs through its own doors (§8).
- **No machine path, no secret.** `root`, `origin`, `account`, a home, a key: never. Every value is an id (a session, a quest, an ask, a
  confirmation, a repository or workspace name as registered), a code from a closed set, a boolean, a count or a UTC time.
- **The grammar's bounds**: `kind`, `state`, `code`, `answer` and `route` from closed sets; an id matches `^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$`
  (a registered name keeps its letters, never a space, a slash, a backslash, a colon or a quote); a count is a non-negative integer;
  `at` an ISO UTC time. A frame is at most 4 KB.
- **Held by a test, seen failing**: a census over `HookFrames.Event` for every kind checks each frame against `WorkEventGrammar`, and
  fails on a `note` added, on a path-shaped value and on an id outside the pattern.

### 4.4 Order, the rate bound, and a plugin that hangs

- **One order per plugin.** `Tell` takes an event, stamps it with the set's `run` (random, minted when the set is made) and the next
  `seq`, and writes it to **each listening plugin's own queue**, then returns: no caller waits on a plugin. Each plugin's teller tells
  its events in `seq` order and awaits each answer before the next. So every plugin hears every event in `seq` order, and a slow plugin
  holds only itself (F9).
- **Bounded per plugin.** At most 256 events wait for one plugin (the work wire's `EventsWaiting`, `WorkBounds.cs:13`), the oldest
  dropped first. A drop is never silent: the next event told to that plugin carries `missed`, its own count dropped since its last.
- **A plugin that hangs is replaced.** An answer not given within the patience (10 s) is late. After `WorkBounds.ExitsUnsound`, 3, late
  answers in a row at `work/event`, the set stops the plugin, logs it `plugin.failed` with the kind `late`, and the next look starts it
  again as it starts a gone one. A restarted plugin is handed the current `run` and `seq` (§4.1).
- **What it is not.** No replay and no store: an event is a wake (§1.8). The `seq` counts every event the set minted; events minted
  while no plugin listened are counted by the next handshake's `seq`, so a gap shows to a plugin that comes back (F8). A subscriber that
  sees a gap, a new `run`, or reconnects reads the truth again.

### 4.5 A loop with no plugin on the point

**The set makes no queue and starts no teller for `work/event` until a plugin listening at it is running**, and `Tell` with no listener
mints the `seq` and keeps nothing. This is a change from `state/changed`'s teller, which starts at the first change whatever listens
(§1.2), and 1b's proof holds it. So the loop's report, its log and its timings are what they were, and nothing is written to the home.

## 5. The plugin `event-relay`

### 5.1 What it is

A plugin of the existing shape: `plugin.json` with `hooks.points: ["work/event"]` and a command, `node plugin.mjs`; no steps, no
servers. It is written first as an example under `examples/plugins/event-relay/`, where this repository's tests run it, as `hold-by-title`
is the rehearsals' fixture (D120 point 3). Offering it from Daoris.Plugins is a quest to that repository, never an edit (§9). **Its folder
and its package hold no file any process outside Daoris runs**: the relay is Daoris's own verb (§6.1).

Its description, which the person reads before switching it on, says: *Passes Daoris's work events (ids and codes, never words) to
programs on this machine that ask for them, such as `daoris-driver events relay`.* The page's gloss for `work/event` says the same of
any plugin at the point (D119's note) (F16).

### 5.2 Finding the subscribers: a folder watched, never a file polled

- **Where a subscriber says it is there.** `<DAORIS_PLUGIN_DATA>/subscribers/`. Each subscriber writes one file, `<its pid>.json`:
  `{ "pipe", "pid", "kinds": [...], "states"?: [...], "workspaces"?: [...], "repositories"?: [...] }`, written atomically, and
  removes its own file and no other as it ends. One file per process: no subscriber's file is another's to overwrite (F7).
- **The plugin reads the folder once at its handshake and then watches it** (Node's `fs.watch`), connecting to each file's pipe as it
  appears. A file whose pipe does not answer is said once on stderr and left; a file removed closes its connection. Nothing is reread on
  a clock (F11).
- **The plugin is a client.** It opens the subscriber's pipe with Node's `net.connect`, which needs no descriptor of its own. Its first
  line is `{ "plugin": "event-relay", "run", "seq" }` from its handshake (§4.1). It writes nothing to the pipe but that line and events.
- **The filter is the plugin's.** The driver tells every kind to every plugin on the point; the plugin sends each subscriber what its file
  asks. A filter in the driver would put a subscriber's choice into Daoris's records, which this keeps out.

### 5.3 Ordering, rate and a subscriber gone or slow

- **In order, per subscriber.** One writer per connection, in `seq` order, newline-delimited JSON, each event the driver's frame unchanged.
- **Bounded per subscriber.** At most 64 waiting; past it the oldest go, and the next event sent carries the subscriber's own `missed`.
  A write not completed within 5 s marks the subscriber slow; its connection is closed and opened again when its file is next changed.
- **The driver is never held by a subscriber.** The plugin answers `hook/work/event` as soon as it has queued the event for each
  subscriber; nothing waits on a write.
- **The plugin gone.** The lease's loop starts it again at a later look (§4.4), and it reads the folder again: a subscriber waits on its
  own pipe, polling nothing (§6.1 step 6).

### 5.4 A trial is never taken for the live plugin

- **A Try** starts the installed plugin in its own data folder (§1.6), told the kit's sample `work/event` frame, whose `run` is
  `"sample"`, and handshaken with `run: "sample"` (§4.1). The plugin, told a sample run, connects to no subscriber, and says so. A relay
  refuses any connection whose first line names `run: "sample"` and drops any frame whose `run` is `"sample"` (§6.1).
- **A second live process** cannot come from the loops: only the lease's loop starts one (§4.1). Were one to start anyway, it would
  connect as a second client and the relay would refuse it while the first is connected (§6.1 step 2). Neither process removes a
  subscriber's file, so neither silences the other.

### 5.5 Its words and its log

Its stderr says each subscriber's connection, drop and departure, under `plugin:event-relay` on the console, naming the subscriber by
its pid, never its pipe. The machine log keeps the set's `plugin.called` for each `work/event` as it does for `state/changed`
(`Hooks.cs:1116`), with no subscriber in it.

## 6. The delivery adapters

### 6.1 The inbox relay: `daoris-driver events relay` (first)

The outside session starts it from a `SessionStart` hook the person adds, or from its Bash in the background, as
`daoris-driver events relay --session-pid <pid> [--workspace <name>] [--kinds …]`. Which start works on Windows is measured (§7).

1. **It takes what is its own and holds nothing of Daoris's.** At entry, before anything else, it takes `CLAUDE_CODE_MESSAGING_SOCKET`
   and `CLAUDE_CODE_MESSAGING_TOKEN` into memory and removes them from its environment, as `HolderKey.Take` does for the holder key
   (§1.5). It also removes `DAORIS_SUPERVISOR_KEY`, `DAORIS_SERVICE_KEY` and `DAORIS_REMOTE_KEY` unread. It opens no door of Daoris's:
   not the instance pipe, not the service, not the remote. It starts no process. So it makes no press, and the standing confirmation
   D201 point 4 applies to a terminal has nothing of its to cover. Its one write is its own subscriber file (§5.2). It never sends the
   token anywhere but the inbox, and no file holds it. The home it writes in is the one `DAORIS_HOME` or `--home` names, the
   install's.
2. **It serves one pipe and takes one plugin.** On Windows `daoris-event-<random>`, made with `InstancePipe.Security` (the user's SID
   owner and alone, the network denied) and `FirstPipeInstance`, so a plugin at either elevation reaches it (§1.7; F6). Elsewhere, a
   Unix socket in a folder only the user reads. It takes one connection at a time: while a plugin is connected, another is refused and
   said on stderr.
3. **It authenticates the connecting process, not a file.** On Windows it asks the system which process is the client
   (`GetNamedPipeClientProcessId`) and takes it only where that process is a direct child of a launcher (the install's
   `Daoris.Desktop.Launcher`), whose parent is the process the home's lease names, checked alive at its start (`HomeLease`, §1.7), and
   whose image is the install's. A plugin is started so (§1.5); a process an agent starts is a grandchild of the launcher at least, and a
   process that rewrote the subscriber file or the plugin's folder has no such parentage. A connection that fails is closed and said.
   Its first line must name `plugin: "event-relay"` and a `run` that is not `"sample"` (§5.4).
4. **It validates every frame against `WorkEventGrammar`** (§4.3) before it builds a word: a closed `kind`, its closed fields, every id
   matching the pattern, a frame at most 4 KB. A frame that fails is dropped and said on stderr, and three in a row close the connection.
   So whatever reaches the pipe, what the relay posts is fixed words over validated ids (F5).
5. **It batches and bounds.** Its default is what wakes the steward: `session.parked`, `quest.parked`, `session.ended` and
   `work.landed`, in the workspace it was started for (`--workspace`, the home's default where absent). The driver already leaves out the
   person's own stops, a limit and a refused choice (§4.2). Events within 30 s go as one message, at most one message per 30 s, and at
   most 20 an hour, past which events are counted and the next message says how many were held back. Each event is one line of fixed
   English words built from its codes, such as `Daoris: session s-4b62a9fe parked, waiting on the person (quest 48f8ed9121ec, Daoris)
   [r-6c0f1a #12]`. The words are a machine's to an agent, not the window's chrome, so they are not translated. The `run` and `seq` keep
   two batches from ever being identical (F10).
6. **It posts as the documented poster does.** It opens the inbox only when a batch is ready, sends the auth line, then the message in
   the shape EVENTHOOK1a records, and closes. A refusal is said on stderr and the batch dropped: events are wakes.
7. **It ends** when the process `--session-pid` names exits (it waits on that process, never polls it), or when the inbox is gone (its
   pipe or socket not found). A refusal or a rate limit is no ending. It removes its subscriber file as it ends. While Daoris is closed
   it waits on its own pipe and reads nothing (F11).

**What Windows does not draw.** Processes of one user at one integrity level are not separated: such a process may read another's
memory or start a child under a parent of its choosing. Against that, step 3 cannot hold, and nothing on this machine can. What then
still holds is step 4: such a process can make the relay post a false wake in fixed words about ids, never a word of its own, and a wake
grants nothing (§8). What step 3 does hold against is the read's case: no process redirects the relay or feeds it by rewriting a file,
since the relay reads no address and trusts no file it did not write.

What this adapter cannot promise: an idle session's new turn, the inbound class under the session's permission mode, and the message's
shape are Claude Code's, measured in EVENTHOOK1a.

### 6.2 The knowledge server's channel (second, opt-in)

The knowledge server is a stdio MCP server, one per session that registers it (`Mcp/Program.cs:146-167`). A hand-run session in this
repository does not start it directly: `.mcp.json` starts `node tools/knowledge-server.mjs`, a relay that serves a build's home under
`local/knowledge-server/home/`, never the install's, and moves a running session onto a new build (`tools/knowledge-server.mjs:18-29`).
So, behind an opt-in variable on its registration, `DAORIS_CHANNEL_EVENTS`, whose value names the kinds and the install's home
(`kinds@home`; a new row of the wiring census), it:

- declares `experimental['claude/channel']` with instructions saying the events are wakes and naming the tools that read their truth;
- subscribes as the relay does (§6.1 steps 1 to 5: its own pipe, the same checks, the same grammar, in the home the variable names),
  and sends each event as `notifications/claude/channel`, `content` the line of §6.1 and `meta` the frame's ids and codes under
  identifier keys (`kind`, `seq`, `session`, `quest`, `repository`, `state`);
- declares nothing, and subscribes to nothing, without the variable. Daoris never sets it on a connector it hands a session it starts.

It is second and opt-in because the channel is a research preview that needs the development flag in an interactive session, its
Windows support is not stated, and the SDK's support and the launcher's swap are unmeasured. It is built only once EVENTHOOK1a has seen
a development channel register and deliver on this machine through `tools/knowledge-server.mjs`, across a build swap (F12).

### 6.3 The inbox's variables never pass through Daoris

`GitWiring.Removed` gains `CLAUDE_CODE_MESSAGING_SOCKET` and `CLAUDE_CODE_MESSAGING_TOKEN`, so every child Daoris starts through Tools
or a tree's git (`Tools.cs:679`, `:693`; `Driver/Trees/WorkingTree.cs:973`) is handed neither: a plugin, an agent, a piece, a git.
And each Daoris executable removes both from its own environment at entry, beside `HolderKey.Strip` and `HolderKey.Take`
(`Daoris.Desktop.App/Program.cs:34`, `Host/Program.cs:345`), so no start that bypasses `Hand` copies them. `daoris-driver` takes them
into memory there, and only `events relay` reads what it took (§6.1 step 1). The wiring census names both as removed. So a Daoris
started from the session's shell passes the token to nothing it starts (F3).

## 7. Measured first: EVENTHOOK1a

On the installed Claude Code on this machine, before any row depends on it. The probes are scripts under
`src/Daoris.Cli/test/fixtures/event-hook/` (a stand-in poster, a stand-in pipe server and client, a one-file channel server), run by
hand from a real interactive session. No test runs them, and no test starts a real console.

| # | Measures | Evidence kept |
|---|---|---|
| M1 | `claude --version`; `/status`'s `Peer address` form on native Windows | the version and the address's form, never the address |
| M2 | a child started from the session's Bash posts the auth line and one message line: the line shapes accepted; delivered, held or refused under `default`, `acceptEdits` and bypass modes | each mode's outcome; the accepted shape |
| M3 | the same post to an idle session: does a turn start | yes or no, with the turn's first line |
| M4 | two identical posts; ten in two seconds; one every 30 s for ten minutes: what is dropped, refused or rate-limited | the counts |
| M5 | a process started from Bash in the background, and one from a `SessionStart` hook: does each live past the session's end, and is the session's process id known to it | each, with how it ended |
| M6 | a one-file channel server registered with `--dangerously-load-development-channels server:<name>`: registers and delivers on native Windows, to an idle session | the startup notice's words; delivered or not |
| M7 | the knowledge server's .NET MCP SDK: can it declare `experimental['claude/channel']` and send a raw notification; does Claude Code register it after the revision fallback, through `tools/knowledge-server.mjs` and across a build swap | a small check's result |
| M8 | a process started from the session's Bash, and its children: do they hold the two messaging variables; a stand-in Daoris child started there with the `Removed` list applied: does it hold them | present or absent, each |
| M9 | a pipe made with `InstancePipe.Security` by a process that is not elevated, opened by Node's `net.connect` from an elevated one, and the reverse; and a Node-made pipe opened across elevations, to confirm the failure | each opened or refused |
| M10 | from a relay that is not elevated: `GetNamedPipeClientProcessId` on an elevated client, and its parent and image read, across elevations | read or refused |

**The decision rule.** If M3 and M6 both answer *no*, nothing would consume the point, and 1b to 1e wait; the row is said in the
backlog with the measurements. If M9 or M10 refuses, §6.1 steps 2 and 3 are designed again before 1c.

The evidence is a dated record, `docs/2026-10-xx-event-hook-measurements.md`, routed in `docs/README.md`: versions, the steps, each
outcome, and no machine path, token, address or process id. A probe that cannot run is recorded as such, and the rows that depend on it
wait.

## 8. What a subscriber may do with an event

- **Read it as a wake.** It says what moved. The truth is read again through the subscriber's own doors: its connector's tools
  (`quest_list`, the session reads), `daoris-driver sessions` and the CLI's reads.
- **Act through its own doors, as an agent.** What it may do after reading is what it could do before the event. An event grants
  nothing.
- **Never a press of the person's.** D156 stands: no process in this chain holds the person's key. D201 stands: the plugin is contained,
  and the relay, which runs where a terminal runs, holds no Daoris key, opens no Daoris door and makes no press (§6.1 step 1). What the
  outside session may press is what it could press before (D201 point 4, and STEWARDROLE1's roles where they land). A message in the
  inbox cannot approve anything there either (§2.1).
- **No instruction rides an event.** Its words are fixed, its fields are codes, and the relay checks every frame against the grammar the
  driver's census holds (§4.3, §6.1 step 4), so nothing authored reaches the receiving session through it, whoever wrote to the pipe.

## 9. Rows, in build order, with their proofs

| Row | Lanes | What | Proof |
|---|---|---|---|
| **EVENTHOOK1a** | the steward, a real interactive session | M1-M10 (§7), and the decision rule | the measurements record, every probe's outcome or why it did not run |
| **EVENTHOOK1b** | driver, modules, web; after 1a's M3 or M6 answers *yes* | `work/event` in every place §1.1 lists; `HookFrames.Event` and `WorkEventGrammar`; `Tell` with `run`, `seq`, `missed`, a queue and teller per plugin made only once one listens; `initialize`'s `events`; a plugin on the point wanted only by the lease's loop; late answers counted and the plugin replaced; both loops tell the attention watch's `Parked`, `QuestParked` and `Ended`, `Observe` setting a park's workspace; `AutoLander.Tried` tells `work.landed`; the two messaging variables removed (§6.3); the kit's sample with `run: "sample"`; the page's gloss | (1) **no plugin, unchanged**: a loop over a stand-in run with no plugin on the point makes the same report and log as before, and no teller starts; (2) **a session that parks is told once**, and one that ends; an intake's park names `ask`; (3) **a landing is told once** with its code, by the lease's loop and not by a `--share` loop's; (4) **once and in order**: three events heard once each, `seq` contiguous; (5) a full queue drops the oldest and the next event says `missed`; (6) a plugin answering late three times is stopped and started again, and a second plugin is not held by it; (7) the census over `WorkEventGrammar`, seen failing on a `note` added, on a path-shaped value and on an id outside the pattern; (8) a child's start lacks both messaging variables though the parent holds them |
| **EVENTHOOK1c** | examples, driver; after 1a's M2, M9 and M10, and 1b | `event-relay` (§5) and `daoris-driver events relay` with the inbox adapter in the shape M2 recorded (§6.1) | `node --test` in the plugin's folder: a stand-in loop speaking the hook wire tells three events to the real plugin process, which connects to a stand-in subscriber's pipe found by its file; a subscriber file added and removed while it runs; a slow subscriber dropped while the plugin still answers at once; a sample run connecting to nothing. The verb's own classes: a relay started with every Daoris key and the messaging variables strips them at entry and opens nothing but its pipe and a stand-in inbox; a client that is not the launcher's child is refused; a second client refused while the first is connected; a `run: "sample"` refused; a frame with an instruction-shaped `repository`, an unknown kind or a word field is dropped and said; the stand-in inbox receives the auth line and one batch, in order; the relay ends when its session's process ends. A Process-lane class starts the example through `HookProcess` beside the verb's relay and a stand-in inbox and sees one post. Every script is under the tests, spawned hidden: no window, no console, no real agent |
| **EVENTHOOK1d** | driver, modules; after 1b | `quest.published` and `quest.closed` from the look's snapshot (§4.2), `person.answered` from the shell's pipe as a confirmation ends | per kind: told once on its change and never on a standing fact; a quest leaving the snapshot unclosed is no `quest.closed`; `person.answered` never told by a headless loop; the census extended |
| **EVENTHOOK1e** | service; after 1a's M6 and M7 | the knowledge server's channel behind `DAORIS_CHANNEL_EVENTS` naming the install's home (§6.2) | an in-process MCP client sees the capability only with the variable, and one notification per event of the kinds named; without it, the server's capabilities and tools are as before; the wiring census row |
| **EVENTHOOK1f** | a request; after 1c | a quest to Daoris.Plugins to offer `event-relay` as Daoris's own (D120 point 3); its package carries the plugin alone, no relay | that repository's answer |

`work.ended` is no row here: it waits on a row that owns link 1's emission (§1.4). Each row records its plans and findings as they land.
A row that builds the driver's half runs only its own classes, filtered.

## 10. Rejected

- **The driver posting into the inbox itself.** It needs the session's token, handed to the session's children only (§2.1).
- **The plugin starting the relay.** It is in the agents' job (§1.5) and no child of the session, so on Windows its posts are unverified.
- **The subscriber reading a file the plugin appends, or calling Daoris on a clock.** That is the poll this removes, moved.
- **Forwarding the shell's bus.** D169 point 4: the bus is the shell's alone, and a headless loop has none (§1.2).
- **A point per kind.** About twelve places each (§1.1), for kinds a field distinguishes.
- **Carrying `note`, titles or commit subjects.** Paths and authored words (§1.9, §4.3).
- **A replay on reconnection.** An event is a wake; a store of events would be a second record of what the ledger already holds.
- **A filter in the driver.** It would keep a subscriber's choice in Daoris's records (§5.2).
- **The channel first.** A research preview behind the development flag, interactive only, Windows support not stated (§2.2).
- **`session.ended` from `EndingsAsync`** (round 2): a park never reaches it (§1.3), and its frame would need the person's stops filtered
  again; the attention watch already says each once, leaves those out, and is a baseline at its first look (F1).
- **`landed` as `work.ended`** (round 2): `work.ended` is the line holding the work, after the steward's merge, and nothing emits it (F2).
- **The relay as a script in the plugin's folder, or a pinned copy of it with its hash checked** (round 2): a pinned copy keeps plugin
  code running uncontained, needs the hash kept where agents cannot rewrite it, which on one user's machine is nowhere, and leaves the
  pipe's elevation open (F6), since Node cannot set its owner. Daoris's own verb has none of those (§3).
- **The plugin serving the pipe** (round 2): Node's pipe takes the default descriptor, refused across elevations (PIPEOWNER1), and a
  subscriber finding it through a file the plugin writes can be pointed elsewhere by rewriting it (F5, F6).
- **A loopback socket gated by a key** (round 2): the key would sit in a file any process of the user reads, and the port is open to
  every user of the machine.
- **One address file shared by every process of the plugin** (round 2): a second process overwrites and removes it (F7).
- **A plugin on `work/event` in every loop** (round 2): a `--share` loop's second process would tell every event twice (F7).

## 11. What the gates do not cover

This is a design and nothing is built. The code was read at `5d19f6b3a` and each statement in §1 names its lines; the quick checks
(`doc-duplicates`, `dogfood`) check this document's shape and links, none of its words. Claude Code's behaviour in §2 is its
documentation as read on 2026-10-24, and none of it was run here (§2.3). The pipe's reach across elevations and the client's ancestry
read across them (§6.1 steps 2 and 3) are the instance pipe's precedent, not measured for this pipe (M9, M10). The plugin, the relay
and every bound in §4 to §6 exist only as rows.

## 12. Round 2: the read's findings and their answers

Every finding of the Opus foundation read of round 1 (`docs/2026-10-24-event-hook-reads.md` §4), each checked against the code on main
at `5d19f6b3a`. *Taken* means the design now says it; *answered* means it is not taken as named, for the reason given; *deferred* means a
later row carries it.

| # | Finding (blocking in bold) | Checked against | Answer |
|---|---|---|---|
| **1** | **A park is no `session/ended`** | `Driver.cs:2855-2865`; `DriverIntakes.cs:414-416`; `DriverContinuations.cs:786-788` (each returns no `SessionEnded` for a park); `Attention.cs:210-261` (`Observe`); `DriverLoop.cs:916`, `Host/Program.cs:972` (both loops call it) | **Taken** as named: the session kinds are the attention watch's `Parked`, `QuestParked` and `Ended`, as `session.parked`, `quest.parked` and `session.ended`, told by both loops after `Observe`, each event's `Note` dropped (§4.2). §1.3 says where a park is seen; §1.9's park line is removed; D173's note point 2 is rewritten. Proved by 1b's *a session that parks is told once*. |
| **2** | **"Landed" mapped to `work.ended`** | `EndJudgement.cs:241-258`, `:263` (`Handed`); `RefWatch.cs:373` (link 1 emits `work.held` alone); `Driver.cs:1403-1410`; `AutoLander.cs:648-672`; `AutoLanding.cs:39-46` | **Taken**: `work.landed`, told where `AutoLander.Tried` keeps a try coded `landed` or `advanced`, with the code, by the lease's holder alone (§1.4, §4.2); it means a branch the install landed and nobody has merged yet. `work.ended` waits behind a row that owns link 1's emission, and is no row of this design (§9). The read named `LandDueAsync`; `Tried` is the one place inside it where each try is kept once with its code, beside the look or in it. |
| **3** | **The inbox token can reach Daoris and everything it starts** | `HookProcess.cs:85-106` (set over the inherited environment); `GitWiring.cs:40-47`, `:98`; `Tools.cs:679`, `:693`; `WorkingTree.cs:973`; `App/Program.cs:34`, `Host/Program.cs:345` | **Taken**, and wider than named: both names join `GitWiring.Removed`, so every child is handed neither, and each Daoris executable removes both at entry as it does the holder key, `daoris-driver` taking them for `events relay` alone (§6.3). §1.5 says the plugin inherits. M8 measures that a process from the session's Bash holds them and a child with `Removed` applied does not. Proved by 1b's proof 8. |
| **4** | **The relay is plugin code running uncontained** | D201 point 4 (`docs/decisions/D201.md:31`); `SupervisorProof.cs:10-20`; `GitWiring.cs:38-41`; `AgentStarts.cs:5-11` (plugins and agents in one job) | **Taken, by another way than named**: the relay is not plugin code at all but Daoris's own verb, `daoris-driver events relay`, from the install's build, which no plugin folder or package carries. It removes every Daoris key at entry, opens no Daoris door and makes no press, so the standing confirmation has nothing of its to cover, and it holds only the two messaging variables (§3, §6.1 step 1). The read's pinned copy is rejected with its reason (§10): its hash has no place agents cannot rewrite, and Node leaves F6 open. 1c proves the verb strips and opens nothing but its pipe and the inbox. |
| **5** | **The relay trusts its input** | §5.2 round 1 (`subscribe.json`); `InstanceClient.cs:196-211` (`GetNamedPipeServerProcessId`); `HomeLease.cs:31`, `:165` | **Taken**: the relay reads no address file. It serves its own pipe, takes a client only where the system names a process that is a launcher's direct child under the lease's holder, and validates every frame against `WorkEventGrammar`, the grammar the driver's census holds, before building a word (§6.1 steps 3 and 4). So no process redirects or feeds it by rewriting a file. §6.1 says plainly what Windows does not separate (one user's processes), and that step 4 still bounds such a process to a false wake in fixed words. 1c proves the refusals and the dropped frames. |
| **6** | **The endpoint names no ACL, owner or elevation** | `InstancePipe.cs:400-410`; `docs/FIX-LOG.md:1506-1520` (PIPEOWNER1); `docs/2026-10-16-terminal-rpc-design.md:145-152`; `docs/2026-10-23-terminal-trust-design.md:253-255` | **Taken**, the read's reverse direction: the relay serves, with `InstancePipe.Security` (the user's SID owner and alone, the network denied, `FirstPipeInstance`), and the plugin connects as a client, which needs no descriptor; an elevated plugin holds the user's SID, and a relay at either elevation owns its pipe by that SID, which is PIPEOWNER1's fix (§1.7, §3, §6.1 step 2). M9 measures it from both elevations, and confirms that the Node-made pipe fails, before 1c. |
| **7** | **A second process in the same data folder breaks delivery** | `PluginTrials.cs:58-78`; `PluginsRoutes.cs:399`; `Hooks.cs:702-710`; `Host/Program.cs:20-27` | **Taken**, all three of the read's changes: one subscriber file per process, each removing only its own, written by the subscriber and none by the plugin (§5.2); the kit's sample and a trial's handshake say `run: "sample"`, which the plugin connects nowhere with and the relay refuses (§5.4); a plugin on `work/event` wanted only by the lease's loop (§4.1). And the relay takes one plugin at a time (§6.1 step 2). Proved in 1b (proof 3) and 1c. |
| 8 | A restart cannot be seen | `Hooks.cs:147-150` (`initialize` without run or seq); `:1196-1206` | **Taken**: `initialize` gains `events: { run, seq }` for a plugin at `work/event`, and `seq` counts events minted while none listened, so a gap shows to a plugin that comes back; a subscriber reads the truth again on a gap, a new run or a reconnection (§4.1, §4.4, §5.2). |
| 9 | A hung plugin holds every event and every other plugin | `Hooks.cs:725-729`; `:1109`; `WorkBounds.cs:25` | **Taken**, both of the read's options: a queue and teller per plugin, so one holds only itself; and three late answers in a row stop it, to be started again at the next look (§4.4). Proved by 1b's proof 6. |
| 10 | The relay's default wakes too much, too often | `Attention.cs:246-258` (the rule); §2.1's limits | **Taken**: the driver tells only what the attention watch says, so the person's stops, limits and refused choices never reach a plugin (§4.2); the relay's default is the four steward kinds in the workspace it was started for, 30 s batches and at most 20 messages an hour, the held-back count said (§6.1 step 5). M4 measures the sustained rate at that pace. |
| 11 | The relay's lifetime is underspecified; its reconnect is a poll | round 1 §5.3, §6.1 step 5 | **Taken**: `--session-pid`, a process waited on; the relay ends when that process exits or the inbox is gone, never on a refusal (§6.1 step 7). With the direction reversed the relay rereads nothing: it waits on its pipe, and the plugin watches the subscribers' folder (§5.2). M5 measures that the session's process id is known to the start. |
| 12 | §6.2 and M7 skip the knowledge server's launcher | `tools/knowledge-server.mjs:18-29` | **Taken**: `DAORIS_CHANNEL_EVENTS` names the install's home, and M7 measures through `tools/knowledge-server.mjs` and across a build swap (§6.2, §7). Row 1e stays last and opt-in. |
| 13 | Three frame details unsaid | `DriverIntakes.cs:385`, `:416`, `:454`; `SessionEnded.cs:14-16`; `Attention.cs:256`; `DriverLoop.cs:892-900` | **Taken**: an intake's frame carries `ask` and no `repository`; each kind's workspace source is named, a park's set by `Observe` from its `SessionView`; the point covers driven sessions and intakes as the attention watch sees them, and not a conversation's end, which is the person's own (§4.2, §4.3). |
| 14 | Smaller claim errors | `Chain.cs:30`; `HookPoints.cs:139-144`, `:15-23`; `Hooks.cs:1096-1100` | **Taken**: `nodes` in §1.8's wakes; the `work/land` row lists `base`, `title`, the quest's title, `session`, `pullRequest`, `acceptedBy` and `tip` (§1.9); §1.2 says the state teller starts whatever listens, and §4.5 makes *no teller without a listener* the change 1b builds and proves; 1b's lanes are driver, modules and web (§9). |
| 15 | Order and sources in the rows | `Driver.cs:917`; `TASKS.md:384` | **Taken**: 1b waits on M3 or M6 answering *yes*, with the decision rule in §7; the quest kinds name `service.SnapshotAsync` and its scope, and judge *closed* by the quest's state (§4.2); `person.asked` is replaced by `person.answered` (§4.2). |
| 16 | The manifest does not say the plugin serves an endpoint | `Hooks.cs:178-182` | **Taken**: the plugin's description and the page's gloss for `work/event` say its events may be passed to programs on this machine (§5.1; D119's note). Round 2 makes the plugin a client, so it serves no endpoint; the words say what it does. |
