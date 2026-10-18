# Events over polling, and the end of a piece of work (FABRIC-EVENT1, FABRIC-END1)

**Parts A and B accepted by the owner, 2026-10-16, with three amendments in their words (D173's acceptance note); part C,
which answers them, accepted with the owner's answers (D173's part C note).** The contract is D173's notes of
2026-10-16, *the end of a piece of work*, *events over polling* and the acceptance. Parts A and B design both directions
together because they share one shape (Part A), and the end of a piece of work is that shape's first consumer (Part B).
Part C makes the end defined per workspace and repository, lets a plugin contribute a step or an end, and turns the
clocks into events with chains; §C1 says what it changes in A and B. Part C was accepted with the owner's answers.
**Part D** (proposed, pending a second maker's read and the owner's yes) sets the design under the owner's goal: a
system easier to extend with plugins and new steps, maximizing automation. It adds the work unit, one step contract,
human tasks, performed plugin steps, the ownership inventory and the completed menu; where it conflicts with A-C it
governs (§D1), and its row order (§D12) replaces the earlier table. Part D was accepted with the owner's amendments.
**Part E** (proposed, pending a second maker's read and the owner's yes) makes every step a **node** with one state
model, joins nodes with typed **wires**, makes the quest's close and waiting on other work nodes, and turns D157's
workflows into graphs; it governs where it conflicts (§E1), and its rows (§E10) replace §D12's. The design builds
nothing; the rows do.

The owner, in order:

- *"how does cleanup currently work, so do we have a watcher on git?"*
- *"because the end cycle is different depending on the workspace and its config, so if we have a git watcher as the end,
  we should clarify this as the new design of the fabric"*
- *"so this should be an event using a watcher instead of polling every 15 s? I think we have a lot of polling logic that
  can be improved"*

Read for it: D173 whole (its guards: one store per concept, no second writer, measured not assumed); D169 §1-§8 (the
event says what moved, shell-only); D88's AUTOTIDY2a, AUTOTIDY2b and PROC1d-ii notes; D147:62; D148 points 1-6 and its
rejections; the proposal's §2, §3, §8 and its appendix's Evidence-First Completion; the C0 audit's matrix
(`docs/2026-10-16-fabric-contract-audit.md` §1, rows 2, 4, 9 and 12); FIX-LOG's UIFREEZE1; the terminal pipe's design
(`docs/2026-10-16-terminal-rpc-design.md`, where the request folder stays for a headless loop).

**A correction to the record.** FABRIC-END1's row says a conversation's end removes its tree. It does not: `END_CHAT`
only finishes input (`ChatRunner.Conclude`, ChatRunner.cs:2037-2062), and a chat removes a tree only where its start
failed (:414-418). The tree seen going on 2026-10-16 went by the look's tidy (AUTOTIDY2a), because its pull request had
merged and its work was on the line.

---

# Part A — events over polling

## A1. The direction, and what it binds

D173's events note sets four things: what the machine can say when it changes is an event, not a poll; a watcher is
backed by a reconciliation minutes apart, never trusted alone; what only a server knows comes by a webhook where a
reachable service can take one, else by a fetch asked for when something says it is due, never on a clock; and the
inventory comes first.

What this design takes that to bind, for every wait built from here on:

1. **An event is a wake, never a fact.** It says a topic and a subject (`refs` of a repository, `requests`, `ledger`),
   never a value and never a verdict. Whoever acts reads the truth itself (D169 §3, carried from the page to the driver).
2. **Every event source has a reconciliation behind it**, which finds what a dropped event missed. A missed event costs
   latency, never correctness; an extra event costs one judgement, which is idempotent.
3. **A wake runs its topic's judge, never the whole look.** More wakes never mean more full looks (§A3).
4. **The judgement stays where it is.** A source wakes the existing judge; no handler judges beside it (D173's guard: one
   writer per concept).
5. **The state is published once**, by the publisher D169 built, never by the source.
6. **Local operation needs no remote**, and a source a remote feeds is only ever a further wake.

## A2. The inventory

The scout's table, read again against the code (*read* marks the rows whose file and interval were checked on this
branch, at the lines named). Costs are read from the code unless marked *measured*, which means this branch's probe
(§A5) on this machine. `D` is `src/Daoris.Desktop/Daoris.Desktop.`.

| # | Where | Every | Waits for | Cost per round | Who feels it | Replaced by | Kept as the reconciliation | |
|---|---|---|---|---|---|---|---|---|
| 1 | DDriver/Loop/DriverWatch.cs:241-261, `Running.NextAsync` with `config.PollSeconds`; `Nudge` :89-98 | `pollSeconds`, 15 (DriverConfig.cs:60) | everything: config, plugins, proposals, quests, sessions, asks, channel, endings | the remote sync first (Driver.cs:332), then 8+ local HTTP calls (the snapshot at :378), the plugin catalogue, config and folder reads, hook reconcile, auto-landing's git (row 4). Not logged: the install's machine log carries no look or tick timing (§A5) | the person: a quest a terminal, an agent or a teammate made starts up to 15 s late | the service's change stream (§A4.2) and the home's files (§A4.1), each running its topic's part of the look (§A3); about 40 routes already `Nudge()` | `pollSeconds` as a slow clock, raised only once the wakes carry the work | read |
| 2 | DriverLoop.cs:696-723 `DRIVER_TICK`; the page's ShellSignals.tsx | each changed look | 9 query families invalidated | 9 refetches | the person | scoped `STATE_CHANGED` from the look's own diffs | the tick as the catch-all | |
| 3 | DDriver/Service/RemoteSync.cs via Driver.cs:332 `SyncAsync` | each look; after an ending | each circle's registry, feeds and quests | 4+ HTTP calls per circle, 3+ per repository; each pass recorded in the ledger (QuestSync.cs:91) | the person: a teammate's quest is late; the remote | its own clock, apart from the local look (§A6, FABRIC-EVENT3a); a held fetch on the remote's cursors later | a pull 60-120 s apart | read |
| 4 | DDriver/Trees/Landing/AutoLander.cs:98-180 (Driver.cs:644-657) | each look | each open due entry, held by a quest's hold, the gate's review, opinion and workflow declarations (:153-177) | git starts per open entry per look | CPU, and the window under antivirus | wakes from refs, the ledger, the config and the gate's files, together (FABRIC-END7) | minutes | read |
| 5 | DDriver/Trees/Branches/BranchTidying.cs:29 `DefaultPace` 5 min, via DriverWatch.cs:214-225 | 5 min | every checkout's session and landing branches | a `RefSnapshot` per repository judged: one `for-each-ref` (*measured* 83-124 ms) and one worktree list (*measured* 75-135 ms), plus the proofs' reads | load; ends seen up to 5 min late | the ref watcher (Part B §B4) | the 5 min pass | read |
| 6 | DDriver/Trees/PullRequestStates.cs:231 `Fresh` 1 min; LandingPlugins.cs | at a D148 occasion, per entry due | a plugin's `work/state` | a node process and a platform call | the clean-up's list | a fetch when due (§A4.3) | D148's occasions | read |
| 7 | DDriver/Sessions/Requests/SessionRequestWatch.cs:80, 236-259, a 1 s `PeriodicTimer` | 1 s | the requests and presses folders | a listing a second | a terminal's stop or say, which waits 10 s before it withdraws the words (SessionsCommand.cs:68, 885); a headless loop once RPC1d lands | the change watch on the two folders | a listing every 3 s, inside the caller's 10 s | read |
| 8 | DDriver/Sessions/SessionsCommand.cs:68-71; Work/WorkPausing.cs:19-22 | 250 ms up to 10 s | the ledger record moving | a GET each 250 ms | a terminal | RPC1d's pipe while a desktop runs; the change stream otherwise | the 10 s bound | read |
| 9 | DModules/Loop/SessionWords.cs:117, 681 | 30 s or the record moving | held words | a read per held session | the person | already woken by the move | keep | |
| 10 | DDriver/Accounts/Usage/ClaudeUsage.cs (1 min floor), CodexUsage.cs:76 (5 min), SwitchDecision.cs:69 (1 min) | when due | the harness's usage process | a process start, 1.5-2.6 s | a start waits | the harness's own frames (`rate_limit_event`) | the floors | read |
| 11 | ProbeLock.cs, HomeLease.cs, TreeLock.cs | 100-250 ms, 30 s patience | lock files | file creates | start latency | a lock's release waking the look | the bound | |
| 12 | DModules/Update/InstallUpdater.cs:100, 281-305 | 2 s or `Poke()` | the staged build's files | 3-4 small reads | idle CPU | the change watch on the staged folder | 30 s | |
| 13 | DModules/Windows/UiStallWatch.cs | 1 s | the UI's latency | a post a second | nobody | keep: it is a gauge | - | |
| 14 | DApp/BrowserProcess.cs | 1 s | `HasExited` | trivial | nobody | `WaitForExitAsync` | - | |
| 15 | DApp/EngineBrowserHost.cs | 30 s | Edge's cookies over CDP | a CDP call | nobody | keep | - | |
| 16 | HostSupervisor.cs, InstancePipe.cs, EngineBrowser.cs | at start | a host answering | a few probes | at launch | a process exit or the port ready | the bound | |
| 17 | DDriver/Agents/OutputTail.cs:16-23 | 500 ms per running background task | a task's output file | a read each 0.5 s | the person watching a stream | keep: its offset read sees every byte, so it is its own reconciliation (the stance at :16-18 stands) | - | read |
| 18 | BatchRelay.cs | batch windows | in-memory events | none | - | already events | - | |
| 19 | AtomicFile, lineage and swap retries | 5-250 ms | sharing violations | tiny | - | keep: contention retries | - | |
| 20 | Service.Core/Composition/ServiceFactory.cs:59 `CheckoutRereadAfter` 1 min | lazily at a search | the checkout, read again whole | 1.2-1.7 s of CPU for 7,724 entries (the scout's measure); one server per session | an agent's search stalls once a minute | the change watch on the checkout's refs and knowledge folders, marking it stale | 5-10 min | read |
| 21 | The service: no timer, loop or hosted service | - | - | - | - | the change stream is added here (§A4.2) | - | |
| 22 | Web bridge/system.ts:32 `wiringAgainIn` | 2 s while measuring | `SYSTEM_WIRING` | a route each 2 s | Settings | a WIRING `STATE_CHANGED` as each size lands | - | read |
| 23 | Web help/AskConversation.tsx:28 `GOING_ON_POLL` 1.5 s | while a message is still to go on | Ask Daoris's session list | a list read each 1.5 s | Ask Daoris | `sessions` published when the driver takes the words up | - | read |
| 24 | Web bridge/help.ts:81 | 4 s while live | the proposals folder | a route each 4 s | Ask Daoris | `help` published where a proposal is written | - | read |
| 25 | Web bridge/update.ts:25 | 30 s | update state | a route | nobody | already `UPDATE_STATE` | keep | read |
| 26 | Web main.tsx `staleTime` 15 s, refetch on focus and mount; git.ts `CHECKOUT_BRANCH` 0 | focus, mount | refetches | a git start per mount for `CHECKOUT_BRANCH` | the person | `STATE_CHANGED` (D169 §6) | the staleTime | |
| 27 | Web clocks with no I/O | - | - | - | - | - | - | |
| 28 | CLI and tools: `lease.ts` 250 ms, `probelock.ts`, `fsx.ts`; the rehearsals' waits | | | | | keep: a CLI is a short process | | |

Two corrections to the scout's reading:

- **Row 1's 24.9 s** (D89:41-43) was Ask Daoris's room look (`help.room.look`, bound 500 ms), not the watch's look. The
  watch's look is not timed anywhere; FABRIC-EVENT2 adds the line that times it.
- **Row 21's cursors** (`/api/sessions/since`, `/api/channel/since`, `/api/quests/operations`, Program.cs:1821, 1857,
  1931) are the shared mode's doors (README:534-537), and the local service does not serve them to itself. More to the
  point, every session's connector composes the store in its own process (`ServiceFactory.CreateAsync`,
  Service.Mcp/Program.cs:100) and writes the database directly. A quest an agent publishes never passes through the
  desktop's service host, so nothing held in that host's memory hears it (§A4.2).

## A3. The shape

```text
 source ──hint──▶ debounce ──▶ topic queue ──drain──▶ judge (one per subject) ──writes──▶ its one store ──▶ publish
   ▲                                                     ▲
   └── overflow, lost handle, reopen, resume ────────────┴── reconciliation: the same judge, at the topic's deadline
```

- **Source.** Something that can say *a thing under this root moved*: a file watcher (§A4.1), the service's change stream
  (§A4.2), a fetch that completed (§A4.3), a harness's frame (§A4.4), a process's exit. It says a topic and a subject.
  It never reads the thing, and it never decides.
- **Debounce.** Per topic and subject, a quiet window of 250 ms after the last hint, capped at 2 s so a steady stream
  still wakes. It coalesces; it is not a barrier: nothing says git's operation is over when the window closes (§A5
  measured one operation's events arriving within about 230 ms, which is a size, not a guarantee).
- **The topic queue.** A hint adds its subject to its topic's due set. A judge takes the set by swapping it for an empty
  one, atomically, so a hint that arrives while a judgement runs is kept for the next one and none is lost. One judgement
  runs per subject at a time (a repository is never judged twice at once), and a topic bounds how many subjects it judges
  at once (git judgements: two, under PROC1a's measured limit of four process starts at once).
- **Judge.** The existing judgement for that topic, reading the truth afresh: git through a `RefSnapshot`, the ledger
  through its service, the files through their record classes. A hint's path is never read as the value. A read that
  fails or is incomplete leaves the subject unknown, never changed, and the subject is tried again with a back-off up to
  its topic's deadline.
- **Wake, and the look.** A hint never runs today's full look, which syncs with the remote before it plans (Driver.cs:332)
  and reads the whole snapshot (:378). It runs only its topic's judge. `DriverWatch.Nudge` gains a topic and a subject; the
  plain `Nudge()` stays and means *everything*, as today. The look's own clock (`pollSeconds`) is the topics'
  reconciliation until each topic has its own deadline. Splitting the look so a `ledger` wake runs the local plan without
  the remote sync is its own row (FABRIC-EVENT3a), and lands before the change stream does.
- **Publish.** The judge writes its own store; the page learns through `STATE_CHANGED` with the existing scopes, through
  the path D169 point 8 built for a terminal's press (STATEHUB1e), never a second publisher.
- **Reconciliation.** Each topic has a deadline: the longest a subject may go unjudged (the look's `pollSeconds`, the
  tidy's 5 min, a folder's listing interval). A subject is judged at its deadline whether hinted or not, and at once for a
  root whose watcher raised `Error`, lost its handle, was reopened, or after the machine resumed.

## A4. The sources

### A4.1 Files: Daoris's home folders, and git's common dir per repository

One primitive, `ChangeWatch`, in the driver library beside `DriverWatch` (the headless host has no bus, so not in the
shell), in `Platform/`, since the structure rule keeps `FileSystemWatcher` out of `Domain` (Driver.Tests/StructureCheck.cs:74).
It owns one `FileSystemWatcher` per root, with a 64 KB buffer and subdirectories, a path filter, the debounce and the topic
queue (§A3), and a seam a fast test replaces with a fake source that raises events, overflows, renames and losses.

**Git: one watcher per repository's common git dir, never per tree.** A tree's `HEAD` is `<common>/worktrees/<name>/HEAD`
and its branches are the common dir's refs, so the common dir hears every tree of that repository (measured: a tree's
switches and commits were heard there, §A5). The filter keeps `HEAD`, `packed-refs`, `refs/heads/**`, `refs/remotes/**`,
`worktrees/*/HEAD`, `reftable/**` and `worktrees/*/reftable/**` (git's optional reftable format writes tables, and a tree
keeps its own); it drops `objects/**`, `logs/**`, `index` and every temporary (`*.lock`, `*.new`): a temporary's rename
onto its final name is the event. The watched set is the repositories whose checkouts here hold Daoris's work (the set the
tidy judges), and it is rebuilt at each reconciliation.

**The home's folders:** the requests and presses folders (row 7), `driver.json` and the plugins folder (row 1),
`lines-moved.json` and the held words (row 1), the staged update's folder (row 12) and the help proposals folder (row 24).

**Recovery**, since a watcher fails quietly as well as loudly:

- **Generations.** Each opened watcher has a generation; its callbacks carry it, and a callback from a disposed or
  replaced generation is dropped.
- **Error or a lost handle:** dispose, reopen, and reconcile the root. A root that overflows again within a minute is
  reopened with a back-off and left to its topic's deadline after three tries, said once in the machine log.
- **The root absent** (a repository not cloned yet, a folder being replaced): tried again at each reconciliation, never
  in a loop.
- **The root moved, deleted or replaced at the same path:** outside what `ReadDirectoryChangesW` promises to report, so the
  reconciliation checks it. The common dir is resolved again (`git rev-parse --git-common-dir`, one process at an open,
  never per hint), and the folder's identity (its file id) compared with the one the watcher opened; a change reopens.
- **After a resume** from sleep or hibernation, every root is reconciled.
- **Bounds:** at most 32 watched repositories (the rest stay on their deadline, said once); judgements bounded per topic
  (§A3); overflow retries bounded as above. Each watcher holds a handle and a 64 KB buffer the system pins; FABRIC-EVENT2
  and FABRIC-END3 measure handles and nonpaged memory with the watchers open on the owner's use.

### A4.2 The service's change stream: a long-poll

`GET /api/changes?since=<epoch>.<n>&wait=<s>` on the local service. It answers as soon as a family moved after `since`,
or at `wait` (30 s at most) with nothing: `{ "since": "<epoch>.<n>", "moved": ["sessions"] }`. It says families, never
records, and carries no path (D169 §3). The driver holds one open and wakes the family's topic (§A3); a refused or
dropped poll is retried with a back-off, and the topic's deadline still runs behind it.

- **What wakes it.** One sampler in the host serves every waiter. It holds a dedicated observer connection, apart from the
  store's own (the composed stores share one, ServiceFactory.cs:285), and reads `PRAGMA data_version` in short autocommit
  reads. That value changes when any other connection commits, so the observer sees the host's own writes and every
  connector's, since each connector writes the store from its own process (Service.Mcp/Program.cs:100). It is an
  in-process call, with no process start and no HTTP.
- **Families, not the database.** When `data_version` moves, the sampler reads each family's own revision and marks only
  the families whose revision moved. A database-wide hint would loop: every look syncs, each pass is recorded
  (QuestSync.cs:91) and its `tried` time rewritten (Quests.cs:1954-1957), so the driver's own look would wake its next
  look for ever. A write that moves no family's revision wakes nobody. Families are added one at a time, each once its
  revision is shown to move on every write that matters and on none that does not: `sessions` (`session_revision`,
  Sessions.cs:496-540) first; `quests` (operations, Quests.cs:2032) and `channel` (Channel.cs:286) after their checks.
  A family not yet proved stays on the look's clock.
- **The cursor.** `<n>` is the sampler's own count of samples that moved a family; the host keeps, per family, the last
  `n` it moved at, so a family that moved several times since `since` is answered once, and nothing needs replaying. The
  epoch is new when the host starts, when the observer reconnects, and when the database file is replaced (its identity
  changes). A missing `since` answers at once with the current cursor and every family; a stale epoch, a malformed cursor
  or one ahead of the host's count answers every family with the current cursor, never an error the driver must handle
  apart.
- **The door.** Registered in `PersonDoors` as `Open` (a read every caller may make, as the connector reads the same
  store), with at most 8 waits held at once (a ninth is answered at once with the current cursor). It is not mapped in
  shared mode: a shared host keeps its own cursors for its circles (row 3).

### A4.3 A fetch when due, for what only a server knows

A pull request's completion, or a remote branch moving, is known to the platform first. The desktop is not reachable for
a webhook, and local operation needs no remote, so the fact is fetched, at moments that say it may be due rather than
on a clock (D147:62, D148:84-85; the owner's own words, D173's events note point 3):

- after Daoris pushes a handed branch, or a plugin opens its pull request (the next answer is now worth asking);
- when the person's own `git fetch` or pull moves `refs/remotes/origin/<line>`, which the ref watcher hears: a plugin's
  completed answer can now be confirmed here;
- at D148's four occasions, which stand;
- at the window's focus while a handed unit waits on its pull request, bounded by D148's one minute (the owner's yes, D173's part C acceptance note).

At each new moment the plugin's ask goes with a fetch of the line, so a completed answer's merge commit is held here for
git to confirm (D148 point 4). None of them is a press that removes. Without any of these moments, a completion on the
platform stays unknown, for as long as nothing makes it due; that is the honest cost of no clock. A back-off clock while
a pull request is open is a timed fetch, and is not proposed; nor is a webhook (the owner's answer: what only a server
knows comes from a plugin, learned its own way, §C4).

### A4.4 The harness's own frames

A harness already streams events: Claude Code's `rate_limit_event`, its `task_started` and the hooks' lines (C1a). Row 10's
usage probe at a start can read the last frame instead of starting a process. This is a later row; it needs no watcher.

## A5. Windows and antivirus: what is proved

**The probe.** A scratch .NET 10 program in this branch's `_fixtures/` (ignored; deleted after), run once on this
machine, with its real-time antivirus on (the one that hooks process creation, FIX-LOG UIFREEZE1) and the owner's
installed Daoris running and otherwise idle. A `FileSystemWatcher` (64 KB buffer, subdirectories, file name, folder
name, last write and size) watched a scratch repository's common git dir while 16 ref operations ran one after another
with git 2.53.0.windows.3, its global configuration replaced by an empty file. A second watcher timed five plain atomic
replaces with no process. Then the scratch repository was deleted. No load was generated.

| Operation | n | git's wall time, ms | first event on the ref, ms after git's start | before git's exit, ms | how the write was seen | events in the operation |
|---|---|---|---|---|---|---|
| commit | 3 | 186-255 | 90-182 | 73-105 | the ref deleted, then `refs/heads/main.lock` renamed onto it | 37 (17 under `objects/`, 2 under `logs/`) |
| branch create | 3 | 63-199 | 56-190 | 8-13 | `<branch>.lock` renamed onto it | 8-9 |
| loose branch delete | 1 | 115 | 101 | 14 | deleted | 12 |
| `pack-refs --all` | 1 | 94 | 74 | 20 | `packed-refs.new` renamed onto `packed-refs` | 23 |
| packed branch delete | 2 | 99-114 | 81-99 | 15-18 | `packed-refs` deleted, then `packed-refs.new` renamed onto it | 16 |
| `worktree add` | 1 | 341 | 226 | 115 | `worktrees/wt/HEAD.lock` renamed onto it | 66 |
| a tree's `switch -c` | 3 | 164-378 | 143-349 | 20-31 | the tree's `HEAD` deleted, then its `.lock` renamed onto it | 26-28 |
| a tree's commit | 2 | 195-203 | 122-129 | 73-74 | the ref deleted, then its `.lock` renamed onto it | 43-44 |
| a plain atomic replace (no process) | 5 | - | the handler ran 0.3-0.5 ms before the replace returned to the writer | - | renamed | - |

- **Every write was heard:** 16 of 16 on the expected path, and the watcher raised no `Error`. Each operation's last event
  came 6-29 ms before its git exited, so by the time a writer is done, its events are in hand.
- **Renames are seen.** Every loose ref and tree `HEAD` write was its `.lock` renamed onto the name (11 of 11), and every
  `packed-refs` rewrite was `packed-refs.new` renamed onto it (3 of 3). The scout expected `packed-refs.lock`: git takes
  that lock, but the file arrives from `.new`. So the filter names final paths, never temporaries.
- **A replace is told as a deletion, then the rename** (10 of the 14 writes that replaced an existing file). A reader
  woken at the deletion could find the ref missing. Whether it really is missing for an instant was not measured, so the
  source never reads, the judge reads after the debounce, and one read's absence is never an end (§B7).
- **The watcher's own delay is under a millisecond.** What costs time is the writer's process start, which the antivirus
  slows: git's operations took 63-378 ms (median 190) here, idle. A watcher starts no process.
- **What the look pays instead**, per repository per judgement: `for-each-ref` 83-124 ms (median 89) and `worktree list`
  75-135 ms (median 91), about 180 ms of two process starts on an idle machine, where PROC1a measured 193-720 ms for one
  start under load. Today every repository is judged each pace whether its refs moved or not.
- **The install's machine log carries no look or tick timing.** Read only, the log of 2026-10-16 has `ui.stalled` (three,
  446-544 ms), `splash.lifted`, `help.room.look` (0 ms) and `session.ended` seconds, and no line that times the watch's
  look, the tidy or a git start. The look's cost per round is therefore read from the code, not measured.

**One run shows the watcher can work here; it does not show it keeps working.** Not measured, and why it matters:

- **Overflow.** Not provoked, by the brief's rule against load on this machine. The design does not depend on it never
  happening: `Error` reconciles the root (§A4.1).
- **The antivirus under a scan or an install**, or with the home's folders, which it may treat differently from the
  repository's `_fixtures/` folder.
- **A watcher held for hours, across sleep and resume**, a watched folder deleted or a repository moved, a network or
  removable drive, and tens of repositories at once: handles, and the nonpaged memory each buffer pins.
- **A fetch's writes** to `refs/remotes` (the probe had no remote), and the reftable format.
- **The instant between a replace's deletion and its rename.**

FABRIC-EVENT2 and FABRIC-END3 carry the Process classes and the machine-log lines that measure these on the owner's use,
and the slow clocks are raised only after them (FABRIC-EVENT7).

## A6. The replacements, ranked and sliced

Ranked by what the person feels, then by risk:

1. **The look's 15 s and its fan-out** (rows 1-3): a quest an agent publishes starts late, and every look calls the
   service eight times. The look's topics (FABRIC-EVENT3a), then the change stream (§A4.2) and the home's files (§A4.1).
2. **The git-reading waits** (rows 4-6): an end is seen up to five minutes late, and each pace judges every repository.
   Part B.
3. **The knowledge server's minute** (row 20).
4. **The terminal's waits** (rows 7-8): RPC1d's pipe takes row 8 while a desktop runs; row 7 stays for a headless loop.
5. **The page's polls in Ask Daoris and Settings** (rows 22-24): the cheapest, since the events exist and only the
   publish is missing.

The order to build is not the order of the ranking. The primitive is proved first where a mistake costs least: row 7's
folders hold no git and no removal, and its fast tests already exist. Then Part B, which needs it. The rows are listed
in **Canaries and proof, and the rows** below.

---

# Part B — the end of a piece of work (the first consumer)

## B1. The unit

**The tree's branch**: a session branch `daoris/s-<id>` in `workspace/repository`, and, where *Accept* made one, the
landing branch that carries it. It is linked to its sessions (several may carry on in one tree, and a hand-over moves
work between records), its quest and its ask.

Why the branch, and not:

- **the session**: a session ends many times (a stop, a resume, a hand-over), several share one tree, and its state is a
  claim (§B2);
- **the quest**: its done is the agent's word, and one quest may change several repositories;
- **the ask**: it spans repositories and machines.

The branch is what git holds, and so what can be observed. A chain's step is its own unit. A landing branch a later done
advanced (LAND2c) is one unit carrying several sessions' work, each ended when the line holds its advance.

## B2. The vocabulary

- **Claim.** The agent's `done`: the quest's Done, the session's Completed. The ledger keeps it (Sessions.cs:10-41,
  Quests.cs:6-19). It starts the end cycle (auto-landing at done, D145), and it is never the end: the model is not the
  authority on its own success (the appendix's Evidence-First Completion).
- **Handed.** The work left the tree into the person's flow: *Accept*'s landing made a branch, a plugin pushed it or opened
  its pull request, or the merge form merged it onto the line. Recorded today, in `landings.json` (`LandedAt`,
  `AcceptedBy`, `Pushed`, `PullRequest`), except for the merge form, which keeps no landing record (TreeLands.cs:489-490).
- **Ended.** The line holds the work's **committed tip**: the repository's configured line, as the local branch or as
  origin's copy last fetched, proven by git. It is a fact about a tip and a line at a moment (§B5): a branch that moved
  past the tip, or a line that no longer holds it, is not ended. It says nothing of work not yet committed, nor of a
  session that may add more: a reader says *its committed work is on the line*, never *done*.
- **Dropped** is not an end: the person's Discard or abandon, recorded where it already is (`abandoned.json`,
  `DISCARD_SESSION_TREE`), never inferred.

## B3. The cycles

*Amended by part C (§C2): the end is defined per workspace and repository, and this table is its default, `line`.*

There is no `end` setting, and none is added. The cycle is resolved as *Accept* resolves it:

- **for handed work**, from what its landing recorded: the rule as it stood (`LandedBranch.Rule`), the named workflow's
  version (`Workflow`), the plugin, `Squashed`;
- **for work not yet handed**, through the same effective process *Accept* reads (TreeLands.cs:346-350): under a named
  workflow, the run's bound version's landing; otherwise the repository's landing rule, else its workspace's
  (`LandingRules.Choose`, Landing.cs:945);
- and from the commit rule, which says when work first exists in git. Uncommitted work is never ended.

| Cycle | When handed | When ended | The proof it needs (existing code) | What the look's automatic tidy may take then |
|---|---|---|---|---|
| Merge form | at *Accept* | at *Accept*: the merge is on the local line | the session tip is an ancestor of `refs/heads/<line>` (`LineHolds`) | the session branch and its tree |
| Branch form, no plugin | at *Accept*: the landing branch is made | when the line holds it: the person merged or pulled it | ancestry against the local line or `refs/remotes/origin/<line>` (`LandedProofs`); by content after a squash, its commits first kept on `refs/daoris/discarded/` (`ContentHolds`) | the session branch, its tree, then the landing branch (AUTOTIDY2a/2b) |
| Branch form with a plugin (a pull request) | at *Accept* and the plugin's push (`Pushed`, `PullRequest`) | when the line holds it, as above, or on the plugin's completed answer where git confirms it | `VerdictAsync` (PullRequestAsks.cs:202): completed, the target is the line, the merge commit is held here, the recorded tip is under the source commit (D148 point 4) | as above; `carried` and `removedAs: "pull-request"` |
| Squash (`squash` on the rule) | at *Accept*: one commit on the landing branch, `Squashed` recording the session tip | when the line holds the landing branch's commit, by ancestry or its pull request | the recorded `Squashed` tip vouches for the session's commits (PROCESS1b-ii); the line holding the landing commit | as above |
| *Accept* at done (`autoAccept`, D145) | at the quest's done, by the rule's switch | as the form it lands by | as that form | as that form |
| A chain's advance (LAND2c) | each later done advances the landing branch | each session when the line holds its advance's `To` | as the form, per advance | each session branch as its advance ends |
| A tree its agent moved (TREEFOLLOW1) | as its form, on the branch it moved to (`Now`) | when the line holds `Now`'s work; landed only on the person's other branch is not ended (D88:150-153) | as the form, on `Now` | as the form |
| Not accepted (the person integrates the session branch by hand, with no *Accept*) | never by Daoris | when the line holds it | the look's `landed` kinds, as AUTOTIDY2a | the session branch and its tree |
| Nothing beyond the line | - | at once: there is nothing to hand | `empty` (the branch holds no commit the line lacks) | the session branch and its tree |

Removal by the person's authority is not on this table and does not wait for an end (§B6): LAND3's tidy at *Accept* takes
the session branches the new landing branch holds, and the press's sweep takes what a branch of the person's holds.

D173's note says one workspace may end *"at Accept's branch pushed"*. Today that is **handed**: D88:150-153 keeps a
branch landed only on another branch of the person's until the line holds it, since its pull request may still be open
and the person may still ask its session to change the work in its own tree. The owner answered: a workspace may define
its end, a push included, on its evidence, and a removal still waits for the line (§C2, §C3).

## B4. The observer

**The ref watcher** is Part A's `ChangeWatch` on each repository's common git dir (§A4.1). Its hint, debounced, adds that
repository to the `refs` topic's due set (§A3). The end judgement drains the set beside the look, as the tidy runs beside
it today (DriverWatch.cs:214-225), one judgement per repository at a time and two repositories at once. It never runs the
look. Each judgement takes one `RefSnapshot`, reads the open units of that repository (session branches and landing
branches not ended at their current tips, and ends that may no longer hold, §B5), runs §B3's proofs, and writes what it
finds. Nothing is removed here (§B8).

- **Daoris's own ref moves** (*Accept*, bringing up to date, a discard) add their repository in process as well, so the
  press's next read does not wait on the debounce.
- **The person's git and the agents' git** (a commit in a tree, a merge onto the line, a pull, a fetch) are what only the
  watcher hears without a poll.
- **The remote's end** comes by a fetch when due (§A4.3); the fetch's own write to `refs/remotes` is then heard like any
  other.
- **Auto-landing is not moved here.** Its due entries are released by a quest's hold, the gate's review, a second opinion
  and a workflow's declaration (AutoLander.cs:153-177), none of which is a ref. It stays on the look until those wakes
  exist too (FABRIC-END7).
- **The reconciliation** is the tidy's 5 min pass, unchanged, which judges every repository as today. It stays until the
  watcher's canaries and recovery have been measured on the owner's use (FABRIC-EVENT7).

**The cost.** The watcher starts no process. A judgement costs one snapshot (about 180 ms here, idle) for a repository that
moved, where today every repository costs one each pace. Under antivirus, git process starts are the cost that matters
(PROC1a, FIX-LOG:2136-2175; UIFREEZE1), and the watcher removes the ones for repositories that did not move.

## B5. The store

*Amended by part C (§C7): the `Ended` field below is kept as `Held`, the line's proof, with the same fields and rules;
a plugin's steps are kept beside it as `Steps`; and* ended *is derived at read from the kept facts and the effective
cycle, never stored.*

One store per fact, no second writer (D173's guard), and no new file:

- **Handed** stays where it is: the landing entry in `landings.json`, and its advances.
- **Ended, for a landing branch**: a field on its `landings.json` entry,
  `Ended { At, By, Tip, Line, On, OnAt, Repository }`:
  - `By`, one of the proof codes the clean-up already writes (`LandedKind`: `on-line`, `merged`, `inside`,
    `pull-request`);
  - `Tip`, the commit judged;
  - `Line`, the line the cycle named;
  - `On`, the form of the line that held it (as `RemovedOn` names it), and `OnAt`, that form's commit when judged;
  - `Repository`, the identity of the repository judged (its common dir's file id), so a repository replaced at the same
    path is never read as the same.
  `RemovedAs` and `RemovedOn` stay the removal's, written when it goes, which may be long after.
- **Ended, for a session branch**: the same field on its `session-branches.json` entry (`GrownBranch`), with `By` from
  the sweep's kinds (`empty`, `landed`, `carried`) and the landing branch it was inside, where it was. This is the record
  the merge form and the no-*Accept* case lack today.
- **When an end stops holding.** An end holds only while the branch's tip is still `Tip`, the cycle still names `Line`,
  the form `On` still descends from `OnAt` (the line was not rewound or replaced), and the repository is the one judged.
  Any of these failing makes the end *not known*, never *not ended*: the subject is due again, and readers say nothing
  until it is judged.
- **A session branch's entry outlives its branch once ended.** Today it is dropped when its branch goes, by two paths:
  `ForgetGoneAsync` (BranchReplays.cs:406, its forget at :432), called by the look's tidy (AutoTidies.cs:155, 216),
  LAND3's tidy (TreeTidies.cs:97), the landed clean-up (LandedCleanups.cs:207) and bringing up to date (TreeSync.cs:255);
  and `TreeRemovals`' own forget (TreeRemovals.cs:206), on *Accept*'s removal path too. Both keep an ended entry as a
  trace instead, the newest 50 per repository, as `landings.json` keeps its traces (LEFT3, `TracesKept`), so a quest's
  close and the page can still say where the work went after the branch is gone.
- **One writer, across processes.** The end judgement, in the driver library, through each file's record class. Those
  lock only within a process (`SessionBranches`' gate, SessionBranches.cs:43), and a terminal's verb may act beside a
  running instance holding nothing (HomeLease.cs:192-197). So ends are written only by the process that holds the home's
  lease as its loop; a verb beside an instance never writes one, and asks the instance through RPC1's pipe where it
  needs one judged. A verb alone, under the lease it took, may judge. *Accept*'s landing adds its repository to the due
  set after writing the handed facts; no reader writes an end.
- **Machine-local** (D63), never in the repository, never on a shared host's wire in the first pass. The ledger keeps the
  claim, and nothing in the service changes (D46: the service knows nothing of landing).
- **Additive shapes.** A field is added and an old entry reads as not yet judged, never as ended.

## B6. The readers, and what each stops judging for itself

| Reader | Today it judges | It reads instead | What it still does itself |
|---|---|---|---|
| The look's automatic tidy (AUTOTIDY2a/2b) | each branch's proofs, each pace | its candidates: units ended at their current tip, and `empty` | judges again inside its hold and right before each goes; every keep of D88 and D102 (§B8) |
| The press's sweep and LAND3's tidy at *Accept* | the person's own removals: the sweep takes what any branch of the person's holds (TreeSweeps.cs:342-354), and LAND3's tidy the session branches a just-made landing branch holds (TreeLands.cs:527-537) | **unchanged**: they remove handed work that is not ended, on the person's press or rule, and are not moved to the end | their own judgement, as today |
| The chat's *Accept* and the page (DiffPane's landed note, LandedNote, SessionHead's foot, What needs you, WorkspaceTidied, Sweep, BranchPage) | `SessionGroups.LookAsync` and `TreeWorkReads` derive landed and on-the-line | handed and ended from the driver's answer, which reads the store | nothing: a view holds no state (D169 §1) |
| A quest's close | nothing: Done is the agent's word | its work's end, shown beside the claim | never changes the ledger in this pass |
| The CLI: `trees clean`, `state`, `land --plan`, `sync`; `git branches` | the same proofs as the press | the store, printed | its words as today, unless a row changes them with the rehearsal's readers (`tools/family-rehearsal.mjs`:4031-4198) |
| Ask Daoris (HelpRoomDoors, HelpRoomMachineNow, HelpRoomMayPropose, HelpRoomWorkspaces, HelpRoomMayDo) | what the page's reads say | the store, through the same reads | its goldens updated with the words |

The page learns that an end was written through `STATE_CHANGED` with the existing scopes (`landing`, `sweep`, `sessions`,
`treesSync`), published once by the path STATEHUB1e built. No scope is added until a reader needs one.

## B7. Never ended by a guess

- the model's `done`, a quest's Done, a session's Completed, a conversation's end;
- a remote branch read as `[gone]`;
- patch equivalence, or comparing trees (D102, D148's rejections);
- a plugin's word without its merge commit on the line here (D148 point 4);
- a push without its evidence: a push may be a defined end only on the plugin's answer and the remote-tracking ref
  holding the tip (§C2), and it never lets anything be removed (§C3);
- an unread, failed or timed-out git answer: absent is never zero (D57);
- one read woken by a hint: a ref replaced is told as deleted first (§A5);
- a tip the branch has moved past, a line rewound or replaced, a cycle now naming another line, or a repository replaced
  at the same path (§B5);
- landed only on another branch of the person's, under the default cycle (D88:150-153). A cycle whose end comes before
  the line ends there instead, its work held in its result (§C3);
- a human step pressed by anyone but the person at a person's door (§C2).

A session running or waiting in the tree, uncommitted or untracked work, a held lock, waiting words and a review showing do
not stop the committed tip's end being observed: they stop a removal, as they do today, and a reader says both
(*its committed work is on the line; its tree holds uncommitted changes*).

## B8. The observation, kept apart from the removal

The end judgement observes and writes the end; it never removes. The look's automatic tidy takes its candidates from the
ends and keeps every guard it has: judged again inside the hold, once more right before each goes, the delete only at the
commit judged, commits kept on a `refs/daoris/discarded/` ref where proven by content, the tree moved aside for fourteen
days. The person's removals (the sweep's press, LAND3's tidy) keep their own judgement.

So a false end cannot remove anything: the removal's own judgement refuses it. It can still mislead: the page, the CLI
and Ask Daoris would say *on the line* until the subject is judged again. That is why an end names its tip, its line and
the line's commit, stops holding when any of them moves (§B5), and is checked by canaries 6-8. A missed end costs time:
the reconciliation finds it.

---

# Part C — the event chain, a defined end, and plugin-contributed steps

The owner, accepting parts A and B (D173's acceptance note):

- *"so we can define a clear end of work: each repo or workspace might have a different cycle of work"*
- *"so this will be a plugin; with an extra plugin we can define a different end for the fabric"*
- *"so instead of using a 15 s [clock] we should build them into the event system, so the fabric can define a different
  event as its chain"*

Read for it, beyond parts A and B: the workflow design (`docs/2026-10-09-workflow-design.md` §2.1-§2.4, §3.2-§3.5, §5;
D157), which already composes the steps after the landing and keeps a run's state; the plugin hooks design
(`docs/2026-10-04-plugin-hooks-design.md` §1, §2, §4); D164's amendment (a repository's whole rule replaces its
workspace's); `twins.md`'s rows for the driver's choices and for plugins.

## C1. What part C changes in parts A and B

- **§A1 point 1** stands for every source but one: a plugin's step event carries its evidence, which is kept as the
  plugin's word (§C4). It still never removes anything.
- **§A3:** the topic queue is the chains' input (§C6), and each topic's reconciliation deadline is a `timer` event (§C5).
- **§A4.3:** what only a server knows may also come from a plugin's own watch (§C4). Daoris's own asks stay at due moments.
- **§B2:** *claimed*, *handed* and *ended* keep their meaning, and *ended* now means the **defined** end (§C2). A fourth
  word, ***held***, is the line's proof, which a removal needs (§C3).
- **§B3:** its table is the default cycle, whose end is `line`. A defined end picks another step.
- **§B5:** `Ended` is kept as `Held` with the same fields and rules. A plugin's steps are kept beside it as `Steps`, and
  *ended* is derived at read, never stored (§C7).
- **§B7:** a push may be a defined end, on its evidence (§C2).
- **§B8:** the automatic tidy still needs *held*, on the line or, for an end before the line, in the cycle's result. At a
  true end it takes everything the work made except its result, and whether it waits for an end after the line is the
  cycle's `wait` (§C3). Everything else stands.

## C2. The cycle: its steps, its end, its definition

A cycle names the steps a repository's work passes and which of them is its end. The steps are a fixed set, plus what a
plugin declares:

| Step | Reached on | Its evidence, and where it is kept | May be the end | Lets a removal go |
|---|---|---|---|---|
| `claimed` | the agent's done | the ledger (Sessions.cs:10-41, Quests.cs:6-19) | **no**: a claim, and the owner's own condition is that a step is reached only on git's or a plugin's evidence | no |
| `handed` | Daoris's landing: the landing branch made at its recorded tip; for the merge form, the merge on the line | the landing entry (`LandedAt`, `Tip`, `AcceptedBy`); for a merge, `Held` | yes | no (a merge is *held* at once) |
| `pushed` | the plugin's `work/land` answer that it pushed, at its pushed tip, **and** git's remote-tracking ref holding that tip (a push moves `refs/remotes/<remote>/<branch>`, which the ref watcher hears) | `Pushed` and `PushedTip` on the landing entry; the ref read again at each judgement | yes | no |
| `pull-request` | the plugin's `completed` answer with the full merge and source commits (D148 point 3), its target the line, and the recorded tip the source commit or an ancestor of it held here: D148 point 4's first and third checks | `PullRequestState` on the landing entry (D148 point 6) | yes | no: its second check, the merge commit held on the local line or origin's copy, is `line` |
| `line` | git's proof (§B3) | `Held` on the entry (§C7) | yes, **the default** | **yes**: this is *held* |
| `<plugin>/<step>` | the plugin's event and its evidence (§C4), bound to a commit | `Steps` on the entry | yes | no |
| `human/<step>` | the person's press at a person's door, once the steps before it are reached | `Steps` on the entry: who (the person, and by which door), when, at which tip, and the step's label as it stood | yes | no |

Every step after `handed` is about a commit: a newer commit on the work makes it stale for that work (D157 §5.3).
`pushed` and `pull-request` are built in: a plugin speaks them through the points it already has (`work/land`'s answer,
`work/state`'s answer or its event form, §C4), never through its declared `steps`, which name only its own.

**The definition: a `cycle` on the landing rule.** Its home is `landings` and `workspaceLandings` (Landing.cs:47; read and
written at DriverConfig.cs:842-881):

```json
"landings": { "orders": { "form": "branch", "plugin": "azure-devops-pull-request",
                          "cycle": { "end": "pull-request" } },
              "shop":   { "form": "branch", "plugin": "azure-devops-pull-request",
                          "cycle": { "steps": [ { "human": "qa", "label": "QA done", "after": "line" },
                                                "deploy-watch/deployed" ],
                                     "end": "deploy-watch/deployed",
                                     "wait": { "until": "end", "days": 14 } } } }
```

The second reads: on the line, then a person's *QA done*, then the plugin's *deployed*, which is the end.

- `end` is one step's id.
- `steps` lists, in order, the steps the cycle adds after the built-in ones. Each is a plugin step, `<plugin>/<step>`,
  or a human step, `{ "human": id, "label": …, "after": … }`. A plugin step must come after what its manifest's `after`
  names (§C4); a human step's `after` names a built-in step or a step listed before it. An `end` naming a plugin step
  includes that step without listing it.
- **The order is the cycle's.** *Ended* means the end step **and every step the cycle orders before it** are reached at
  the current tip. A step reached out of order is kept and shown, and counts once the steps before it are reached. So
  *deployed* reported before *QA done* is pressed does not end the work.
- `wait` is §C3's: whether the tidy waits for an end after the line, and for how long.

**The human step.** A step a person reaches by a press.

- **Who may press it:** the person on this machine, at a person's door (D156). That means the window's press, or a
  terminal's, which asks and is confirmed in the window (D156 §4.2, carried over RPC1's pipe into the running Daoris),
  or, with no window, a terminal holding the person key (D156 §4.3).
  - Never an agent: no tool offers it, and a terminal an agent runs cannot confirm in the window.
  - Never a plugin: a `work/step` naming a human step is refused as `not-named`.
  - The honest edge is D156 §4.4's: on one account, a terminal the person keys is the person's.
- **Where the press lives:** the page's cycle line on the work (FABRIC-END4b), and What needs you, which lists *waits for
  your `<label>`*. The terminal twin (D50) is `daoris-driver trees step <session> <step>`, with `--undo` to take it back
  before the end is reached.
- **The press is offered only once the steps before it are reached.** A terminal's press before then is refused, naming
  the step it waits on.
- **What it keeps:** on `Steps`, the step, who (`person`, and the door: `window`, `confirmed` or `keyed`), when, the tip
  it was pressed at, and the label as it stood. The label is the person's words, shown as written, while the chrome
  around it is translated. Its id is `human/<id>`.
- **When it lapses:** by §B5's rules for the tip and the repository. A newer commit on the work makes the press stale; it
  is shown as about the older tip (D157 §5.3), and the press is offered again. A line rewound past the work lapses
  *held*, and every step after the line waits for *held* again, while the person's press stays recorded at its tip.
- **Under a named workflow** it is a step of a new kind, `human`, added to D157 §3.2's table. It is a checkpoint the
  person marks done, apart from `go-ahead` (a yes for an act) and `look` (a review before the landing).
- **Why the landing rule.** The end is how work proceeds after it lands: D157 §2.3 gives that sort of field to the landing
  rule under Current (`form`, `autoAccept`) and to the workflow where a named one governs. The plugin a cycle's steps need
  is already the rule's `plugin`. A key of its own (`cycles`, `workspaceCycles`) was weighed: a validation could reconcile
  the two declarations. The landing rule is still preferred because D164's whole-rule inheritance then moves the landing
  and its end together, where two keys would let a repository take its workspace's end beside a landing rule of its own,
  a pairing nobody wrote; and because one Setup section and one twin carry both.
- **Inheritance.** The repository's whole landing rule replaces its workspace's (D164's amendment), and its cycle comes
  with it. A repository rule with no `cycle` takes the default, never the workspace's cycle. A repository with no rule at
  all, its own or its workspace's, lands by merge (`LandingRules.Choose`, Landing.cs:979-984), and its cycle is the
  default.
- **Under a named workflow**, the run's bound version decides its `end`, read where its landing is read
  (TreeLands.cs:346-350). Every named workflow holds a landing (WorkflowNamed.cs:491), and a version saved before `end`
  existed reads `line`. D157 §2.3's table gains the row *The end of work*: the plugin steps a repository may use stay a
  declaration, and `end` is the workflow's.
- **Work integrated by hand**, never accepted, follows the same cycle. `handed`, `pushed` and `pull-request` never come for
  it, so an end among them stays *pending* for it, and its removal goes at *held* (§C3). An end apart from the landing is
  therefore set on the same rule, whatever its form.
- **The default** is absent, read as `{ "end": "line", "steps": [], "wait": { "until": "held" } }`. That is today's
  behaviour exactly: ended when the line holds the committed tip, and the look's tidy as AUTOTIDY2a and 2b.
- **The CLI twin** is `driverconfig.ts`'s landing rule. `twins.md`'s row for the driver's choices names the field, and
  both twins read one table of rows the same way.
- **The Setup row.** The landing rule's rows on the repository's Setup and its workspace's gain *Work ends*, offering only
  what the rule can reach: *when it is on the line* (the default), *when its pull request completes*, *when it is pushed*,
  *when it is handed*, and each switched-on plugin's declared steps under the plugin's own label, shown as written (D142).
  It also gains *Steps after it lands* (human steps added and labelled, plugin steps chosen) and *Clean up* (§C3's wait).
- **Refused at the door that sets it**, naming the Setup row that would allow it (D157 §2.3; D154's pattern):
  - `claimed`;
  - `pull-request` without a branch form whose plugin answers `work/state`;
  - `pushed` without a plugin;
  - a plugin step whose plugin is not installed, switched on, sound and declaring it;
  - a human step whose `after` names no step before it, whose id repeats, or whose label is empty or over 80 characters;
  - more than 8 added steps;
  - a `wait` other than `held` for an end at or before the line, and `days` outside 1-90.

  A cycle that later becomes unreachable (the plugin removed, the form changed) is said once, on the Setup row and in the
  machine log. Its units' end reads *unreachable*, and the removal goes on at *held* (§C3).

## C3. The end and the removal, kept apart

The owner, accepting part C: *"a true 'end' means cleaning up everything unless it's the result, so we can set up
different tasks in the fabric, including some tasks that require human steps"*.

**The line's rule stands: nothing goes before the work is held where it cannot be lost, and every removal judges again
inside its hold** (D88). *Held* takes one of two forms:

- **on the line:** the line holds the committed tip, by §B3's proofs (ancestry, content after a squash with its commits
  kept on `refs/daoris/discarded/`, or a completed pull request's merge commit on the line). This is the default, and the
  only form for an end at or after the line.
- **in the result:** for an end before the line, the cycle's result (below) holds the committed tip by git's proof
  (ancestry, or the `Squashed` tip it records). Where the end is `pushed` or `pull-request`, the remote-tracking ref also
  holds it, which that step's own proof already reads.

**The result** is what the work made that the cycle keeps. It is never taken:

| The end | The result |
|---|---|
| `line` (the default), and every end after it | the landed work on the line, which is the person's branch. A landing branch the line holds is not the result, and goes as AUTOTIDY2b takes it today |
| `pull-request` | the pull request's branch (the landing branch), until it is merged and the line holds it; then as the row above |
| `pushed`, or a plugin step before the line | the pushed landing branch, until the line holds it |
| `handed` | the landing branch; for the merge form, the line |
| a plugin step that names an artifact | also what its evidence names (`evidence.ref`): a ref in this repository is kept, and anything outside it was never Daoris's to take |

**At a true end, everything the work made goes, except its result:**

- the session's tree: moved aside, then purged with its folder at the purge, fourteen days after the move, as today;
- its session branches, whose work the result or the line holds;
- the landing's other branches: an earlier step's or advance's, which the result holds;
- the tree's scratch, its ignored paths, which go with the tree.

**What is never taken:**

- the result;
- the person's own branches: the line, and any branch Daoris did not make and record;
- anything not Daoris's: another checkout, a tag, a stash, a remote's branch (Daoris deletes nothing on a remote);
- the recovery refs under `refs/daoris/discarded/`, which keep their own rule;
- anything a guard keeps: uncommitted or untracked work, a session running or waiting, waiting words, a review showing,
  or a held lock. Each keeps the tree as today, and the end's cleanup waits for it, as a pending effect (§C6).

So a cycle that ends before the line amends D88:150-153 for itself, by the owner's choice: at its end the session's tree
and branches go while the result stays. A change asked of that work later opens a new tree from the result. The default
cycle keeps D88:150-153 exactly.

An end not yet reached is one of three, said apart everywhere it is read:

- **pending**: reachable and not yet reached. The pull request is open, or the plugin has not reported its step.
- **unreadable**: reachable, but its evidence cannot be read now. The plugin's process is down or refusing, or a git read
  failed. It is tried again at its topic's deadline.
- **unreachable**: the cycle names what its rule can no longer reach. The plugin is removed or switched off, the step is
  no longer declared, or the form changed. It is said once, and stays so until the definition changes.

**The wait is the cycle's** (`wait`, §C2), and applies only to an end after the line:

- `{ "until": "held" }`, the default: the tidy goes at *held* on the line, as today, and the later end changes only what
  is said;
- `{ "until": "end" }`: the tidy waits for the end, or for the person's *Clean up now*;
- `{ "until": "end", "days": n }`: it waits for the end at most `n` days (1-90) from `Held.Since`, then goes at *held*,
  said once.

| The defined end | The cleanup goes |
|---|---|
| before the line, reached | at the end, once its result holds the work |
| before the line, not yet reached | at *held* on the line, as today |
| `line` (the default) | at *held*, as today |
| after the line, reached | at the end (it is later than *held*) |
| after the line, pending or unreadable | by the cycle's `wait`: at *held*; or at the end, the bound or the person's press, whichever comes first |
| unreachable | at *held*, said once |

**An end after the line that is never reached** goes by the cycle's bound, said once, or by the person's *Clean up now*.
That press sits on the work's cycle line and in What needs you while the work waits, with its terminal twin (D50) at a
person's door, as the human step's is. With no bound (`until: end`, no `days`), nothing goes by itself: the work waits in
What needs you, which names the step it waits on.

**A deadline's origin is persisted and stable.** A bound counts from `Held.Since`, the moment *held* was first recorded
for the unit's current tip. It is kept on the entry and unchanged by later judgements. It resets only when *held* stops
holding and is recorded again, never at a restart or a re-read. D157 §5.4's seven days stops a run's asking; it is no
ground for a removal.

The person's own removals (the sweep's press, LAND3's tidy) are unchanged (§B6). The rest of §B8 stands.

## C4. A plugin's steps and its events

**The manifest declares `steps`**, each `{ id, after, label }`:

- `id`: letters, digits and `-`, 40 at most;
- `after`: `handed`, `pushed`, `pull-request`, `line` or another of its own steps, never forming a cycle;
- `label`: 80 characters at most, shown as written.

It is twinned in `plugins.ts` and `PluginManifest` (PluginManifest.cs:114), read by `PluginCatalog`, with a row of
`twins.md`'s plugins line. A problem refuses the plugin, as a server's declaration does.

**Three points** join `HookPoints` (HookPoints.cs:54, 61), each with its kit entry (D101):

- **`work/watch`, a loop point.** Daoris tells the plugin which units await its steps, one frame each time that set
  changes: `units: [{ repository, workspace, root, branch, tip, line, pullRequest?, awaiting: [steps] }]`. The plugin
  learns the steps its own way (its own watch, its platform's hook, its own queries), and Daoris does not ask how.
- **`work/step`, an event:** a fifth kind of point. It is a JSON-RPC notification the plugin sends on that process, with
  no id, never answered:

  ```json
  { "repository": "…", "branch": "…", "commit": "<full id>", "step": "<id>", "state": "reached",
    "at": "<time>", "id": "<the plugin's id for this event>",
    "evidence": { "ref": "…", "url": "…", "environment": "…", "message": "…" } }
  ```

  `state` is `reached`, `failed` or `unknown`, and `message` is 300 characters at most. For `pull-request`, the evidence
  carries the fields `work/state` answers (D148 point 3). An event fails toward nothing: one unread is no event.
- **`work/ended`, an observation.** Every switched-on plugin that listens gets it when a unit's end is reached, as
  `session/ended` is sent today. It changes nothing.

**A frame from the plugin is a real change to the wire.** Today the host's reader drops every frame a plugin sends that
is not an answer: *"A notification is noise, and is ignored"* (Hooks.cs:349-351). The kit's test does the same
(`plugin-kit/plugin.test.mjs.template`:183). The reader is changed to accept exactly one method, `work/step`, and only
on a process Daoris started for `work/watch`. Every other notification stays ignored, and a request from a plugin is
still never answered (D64 §7). An older host ignores the frame, so `protocolVersion` stays 1 (D148 point 8's rule). The
kit's template gains the frame, its bounds and its refusals.

The event form of a built-in step: a plugin may send `pull-request` as a `work/step` whose `evidence` is `work/state`'s
answer, the same as if Daoris had asked. `pushed` is never an event; it is `work/land`'s answer only.

**The process's lifetime.** Today a landing starts its plugin for one frame, while the loop keeps a process for each
plugin on a loop point (`HookPoints.Loop`).

- **Who owns it.** The process is the loop's that holds the home's lease (the shell's or the headless host's), the same
  process the loop keeps for the plugin's other loop points (`HookPoints.Loop`, reconciled at each look, Driver.cs:349-352).
  A verb never starts one, and a second loop on the home never does either.
- **When it runs.** It starts when a unit first awaits one of its steps, and is sent `shutdown` (ended 2 s later) once
  none does, when the plugin is switched off, at an update's drain, and when the loop ends.
- **Restarts.** An exit Daoris did not ask for is restarted with a back-off of 1, 5 and 15 minutes. Three such exits
  within an hour mark the plugin unsound until its page's *Try again* (D119). An exit after Daoris's own `shutdown`
  never counts.
- **Bounds, before anything is believed:**
  - a frame longer than 64 KB is dropped unread;
  - at most 60 events a minute are parsed from one plugin;
  - at most 256 events wait in its queue, and the oldest is dropped first;
  - a refusal is logged at most once a minute per plugin and code, with a count.
- **What replays after a drop.** Every `work/watch` frame carries the whole awaited set and a generation. After any drop
  or restart, the next frame says `resync: true`, and the plugin sends again what it knows of each awaited step. A
  `pull-request` is also asked at the due moments (§A4.3), so a dropped event costs time, never a fact.

**What makes an event believed.** A `work/step` counts only where all five hold:

1. **Who sent it is the process, never the frame.** The sender is the plugin whose process Daoris started for
   `work/watch` and read the frame from. A `plugin` field inside the frame is ignored.
2. **That plugin is the one a rule names for the step** (D100: a plugin acts on a repository only where a rule names it):
   the landing rule's `plugin` for `pull-request`, and the cycle's `steps` for the plugin's own. Its own step must also be
   declared in its manifest, confirmed by its handshake, and included by the effective cycle.
3. **It names a unit Daoris told that process about.** The repository, the branch and the repository's identity (§B5's
   `Repository`) are looked up in the last `work/watch` frame sent to it, never by name alone.
4. **It is bound to the work.** Its `commit` is that unit's current tip. For `pull-request`, D148 point 4's first and
   third checks (§C2) must also pass. A newer commit makes it stale.
5. **It reads by shape**, its `at` is no more than a minute ahead, and no newer event is kept for that step and commit.

Anything else is no event. It is counted in the machine log as `plugin.step` with a code (`not-sender`, `not-named`,
`not-declared`, `not-watched`, `stale`, `shape`, `late`) and no words. Binding to the commit ties the plugin's claim to
this work. It does not prove a deployment happened: that stays the plugin's word.

**Trust.** An event reaches a step, and nothing else:

- It never removes anything: removal needs *held* (§C3).
- It never writes the ledger or a rule.
- Its evidence is kept as the plugin's word, with the plugin and when (D143), and shown as written (D142).
- How the plugin learned it, and how often it asks its platform, are the plugin's business, said in its README. Daoris's
  rule against timed fetches binds Daoris's own asks (§A4.3), not a plugin's.
- A plugin that declares `work/watch` for a step is not asked on D157 §5.4's back-off for an awaited run: its own events
  replace those asks.

**Under a named workflow,** a plugin step is a step of a new kind, `signal`, added to D157 §3.2's table. What proves it
done is the plugin's event. Its participation is *Automatic · `<plugin>`*, and it sits where the person draws it after
the landing.

## C5. Events, and the timer as one source among them

| Event | From | Subject | Says |
|---|---|---|---|
| `refs` | the ref watcher; Daoris's own ref moves; a fetch's completion | a repository | its refs moved |
| `home` | the home's record folders | a file | it changed |
| `ledger` | the change stream (§A4.2) | a family | it moved |
| `press` | a route answered (D169's publisher) | scopes | what it changed |
| `focus` | the window's focus (shell only) | - | the person is here |
| `plugin.step` | a plugin's `work/step` (§C4) | a unit and a step | reached, failed or unknown, with evidence |
| `harness` | a harness's frames (§A4.4) | a session | a frame |
| `exit` | a process's exit | a process | it ended |
| `timer` | a topic's deadline | a topic | the whole topic is due |
| `work.step`, `work.held`, `work.ended` | the end judgement, each only on a change | a unit | reached, held, ended, or no longer |
| `work.tidied` | the look's tidy | a unit | removed, or moved aside |

**A timer is one source among the others.** Each topic's deadline (§A3) expires as a `timer` event, which the queue treats
like any other hint, naming the whole topic. `pollSeconds` becomes the deadline of the `look` topic, the part of the look
no event yet carries, and the tidy's pace becomes the deadline of the `refs` topic.

- **Floors:** `look` 5 s, `refs` 1 minute, a folder 1 s.
- **None is removed:** a source with no timer behind it is the watcher-alone shape part A rejects.
- **What the person sets:** `pollSeconds` alone, as today. The defaults are raised only after canary 10 (FABRIC-EVENT7).

## C6. Chains

A chain is the links from an event to what follows it.

| # | On | Runs | May emit | |
|---|---|---|---|---|
| 1 | `refs`, `timer(refs)`, `plugin.step`, Daoris's own ref move | the end judgement for that repository or unit (§B4) | `work.step`, `work.held`, `work.ended` | fixed |
| 2 | the tidy pending: *held* and §C3's condition standing, and the branch still there | the look's automatic tidy, judging again inside its hold (D88) | `work.tidied` | fixed; an effect link; no setting turns it off (D88's AUTOTIDY2a judgement) |
| 3 | `work.ended` | `work/ended` to the plugins that listen; the quest's page and What needs you read it | nothing | fixed |
| 4 | any `work.*` | `STATE_CHANGED` with the existing scopes (D169) | nothing | fixed |
| 5 | `ledger`, `timer(look)` | the local plan (FABRIC-EVENT3a) | starts, through the existing paths | fixed |
| 6 | `refs` moving origin's line, Daoris's push, `focus` while a pull request is open | a plugin's `work/state` ask, with a fetch of the line (§A4.3) | an answer kept as today | fixed moments, within D148's minute |
| 7 | `home`, `timer(<folder>)` | that folder's reader | its own | fixed |

**Two sorts of link.** A **fact link** (1, 3, 4) fires when a fact changes, and never on a fact that stands. An **effect
link** (2, and every later effect) is level-triggered:

- **An effect is pending while its condition stands and it has not happened.** The tidy is pending while *held* and the
  removal's condition (§C3) stand, and the unit's branch is still there.
- **It is tried when its facts change, and again at its topic's deadline.** It is also tried on the wakes that can
  unblock it: a session leaving the tree or ending (`ledger`), a review closed, a lock released, the configuration
  changed (`home`), and a deadline reached.
- **A refusal is not an outcome.** When the tidy refuses because the tree is busy, the effect stays pending and is tried
  again on those wakes.
- **After a crash**, the start's reconciliation finds every pending effect from the persisted facts, so a crash between
  recording a fact and acting on it loses nothing.
- **Retries are bounded:** one try per unit per wake, and at most one per deadline while nothing changes.

**What a workspace and a repository configure** is, in this design, which step ends their work and which plugin steps
their cycle includes (§C2). So the event that ends a piece of work is theirs to choose: a ref, a platform's word, or a
plugin's step. The owner chose that a workspace may also choose further effects from a bounded menu (FABRIC-CHAIN3). **What
stays fixed, for safety, under either answer:** what judges, what may remove and when, what is published, and that
every topic keeps a reconciliation.

**Order.** Hints for one subject coalesce in its due set and are judged in one pass that reads the truth as it stands, so
their order does not matter. A plugin's events for one step are ordered by `at` and commit: an older one never replaces a
newer, and a failure never replaces a kept answer (D148 point 5).

**De-duplication.** A due set is a set. A plugin event equal to the kept one (the same plugin, unit, step, commit and
`id`) is no event.

**Loops, and convergence.** A chain must never wake itself, but ranking the links does not stop an effect from coming
back through the world: the tidy deletes a branch, and the ref watcher hears it. What makes it converge:

- **A fact link emits only on a change.** A judgement that finds what it found before emits nothing.
- **A retired unit becomes a trace** (as amended by §D3's retirement). A branch's removal is not the unit's retirement.
  The unit retires only once its end is reached (or the person drops it), every inventory entry has a terminal
  disposition, and no attempt is unresolved. Until then it stays open on its record, its git facts frozen at the removal
  with `GoneAt` and `RemovedAs`. A retired unit's kept facts are its tombstone, which `landings.json` already keeps and
  §B5 adds for session branches. A trace is never judged again: §B5's rule that a fact stops holding when the tip moves
  applies to standing branches only. So a branch's disappearance never reads as its end being lost.
- **An effect is done once its unit no longer meets its condition.** A trace meets no effect's condition, and an open
  unit whose branch is gone meets only the conditions of its remaining steps, attempts and resources.
- **So each wake ends.** It runs one judgement per due subject (finitely many) and at most one try of each pending
  effect. That try either completes, moving the unit towards retirement, or leaves it pending until the next unblocking
  wake. The world's echo of a completed effect is judged once, finds no change and no pending effect, and emits nothing.
- **The change stream answers families by their own revisions**, so the driver's own sync pass wakes nothing (§A4.2).
- **No configuration adds a link that can feed itself.** On the bounded menu (FABRIC-CHAIN3), every effect is one that writes
  no git ref and no ledger family a link listens to.

## C7. Where a chain's state lives

- **The definition:** the landing rules in `driver.json` (their `cycle`), and a named workflow's version file for its
  `end` (D157 §2.4). One of them governs a run, by its binding.
- **The facts, one store each:**
  - claimed: the ledger;
  - handed, pushed and pull-request: the landing entry, as today (`LandedAt`, `Pushed` and `PushedTip`,
    `PullRequestState`);
  - held: `Held { At, Since, By, Tip, Line, On, OnAt, Repository }` on the landing entry and on the session branch's
    entry (§B5's fields and rules, renamed). `At` is the last judgement; `Since` is when it was first held at this tip;
  - a plugin's own steps: `Steps [{ plugin, step, commit, state, at, id, evidence }]` on the same entries; and the human
    steps, `{ human, step, label, by, door, commit, at }`, written by the person's press alone (§C2). Each step keeps at
    most two: the newest for the current tip, and the newest for the tip before it. An entry holds at most
    16 steps, and a trace keeps only the step that ended it.

  A built-in step's event (`pull-request` sent as `work/step`) updates the existing fact, `PullRequestState`, through the
  writer `work/state`'s answer uses, never a competing `Steps` entry. A named workflow's `signal` step projects these
  same facts; its run file keeps no answer of its own for it (D157 §5.1 keeps only what only a run knows). `Held.Since`
  (§C3) is the stable origin of any deadline. §B5's traces, its one writer across processes and its rules for when a
  fact stops holding apply to each.
- ***Ended* is never stored.** It is derived at read from the kept facts and the effective cycle, so a changed definition
  reads at once and no verdict goes stale.
- **What is in flight:** the due sets, the de-duplication ids and the plugins' processes live in memory. A restart loses
  them, and the start's reconciliation judges every topic whole. A chain's progress is never a second record.
- **The machine log** (D94), with codes and no words: `chain.fired` (link, topic, count), `chain.dropped` (code), and
  `plugin.step` (plugin, step, and its state or refusal code).

---

# Part D — the work unit, its steps and its cleanup, toward the owner's goal

**Proposed, 2026-10-16, pending a second maker's read and the owner's yes.** The owner set the goal over this design
(D173's note *"the owner's goal over the fabric's design"*):

- *"all my input is kind of 'requirements', but if you have any better idea you can always update it; what I'm thinking of
  is a system that's easier to work with plugins and new steps, maximizing the automation we can have"*
- *"I hope you understand that's why we introduced the fabric system; please go ahead and work with Codex"*

Codex checked parts A-C against that goal.

- **What it found:** a strong event-driven landing lifecycle, but only a partial delivery of the goal:
  - the branch lifecycle had become almost the whole fabric;
  - human steps were thin attestations;
  - "everything" in the cleanup was a fixed git set;
  - plugins could bring neither a task lifecycle nor a cleanup contract;
  - the chain menu was unspecified;
  - the build order front-loaded plumbing.
- **What it proposed:** this part, which takes its proposal, checks each claim against the code, and changes it where
  the code or D173's guards ask.

**Where part D conflicts with parts A-C, part D governs** (§D1). The guards stand throughout: ownership is local, there is
one store per concept and no second writer, the person's doors (D156) decide who the person is, nothing runs a script,
and D88's line rule holds.

## D0. Scope, and the milestone the person sees

**Scope.** Branch cycles are the fabric's **first adapter**, not the fabric. The fabric is units of work, their steps,
and what each unit made. Quests, asks and delegated tasks join as adapters on the same unit and step contract (§D2, §D4),
and the collaboration checkpoint (C2's proof, then one cross-harness round trip) runs beside them as its own gate (§D10).
Parts A-C remain the branch adapter's design.

**The milestone the person sees**, for every unit, from the first slice and with the default cycle:

| Shown | Read from |
|---|---|
| **End reached:** which step, when, at which revision | the unit's kept steps (§C7, §D4) |
| **Result kept:** what stays, by name | the inventory's `result` entries (§D3) |
| **Cleanup:** *pending*, with what waits and why (a guard, the cycle's wait, a blocked entry), or *completed*, with what went and when | the inventory's dispositions (§D3) |
| **Recovery retained:** the moved-aside folder until its purge date, the recovery refs | the inventory's `recovery` entries |

The milestone is shown on the work's foot, in What needs you when something waits on the person, on the Branches tab and
WorkspaceTidied, by `daoris-driver trees state` and `work state`, and by Ask Daoris. It is built first (FABRIC-MILESTONE1a and 1b),
for the default cycle, before any configuration exists, so the person sees the lifecycle working as soon as it does.

## D1. What part D changes in parts A, B and C

| Where | Change |
|---|---|
| §A1-§A6 | Unchanged. `timer.step` (§D4) and `work.cleanup` (§D8) join §C5's events |
| §B1, the unit | The tree's branch becomes the **branch adapter** of `WorkUnit` (§D2). Its identity is a stable unit id, and the branch name and path are aliases |
| §B5, §C7, the store | The facts stay where §C7 puts them. The unit id is added to the session branch's entry and referenced from landing entries (`Units`). Persisted attempts (§D6) and plugin receipts (§D3) join the same entries |
| §C2, the steps | Every step, built-in, human or plugin, is one **step contract** (§D4). `cycle.steps` names declared steps. The `human` press becomes a **human task** (§D5) |
| §C3, "everything" | Replaced by the **ownership inventory** with a disposition per entry (§D3). The result, the line rule, the wait and the guards stand |
| §C4, a plugin's events | Kept: the sender checks, bounds, generations and `resync`. Added: **performed** steps through `work/perform` (§D6), a negotiated capability, and plugin resources with a cleanup adapter (§D3) |
| §C6, the menu | Completed (§D8): typed transitions, four effects, Setup's *Work* pages and their terminal twin |
| §C7, in flight | **Amended:** dispatch receipts and unresolved attempts persist on the unit's canonical record, and `timer.step` deadlines persist. The due sets stay ephemeral |
| The rows | Re-cut toward what the person feels first (§D12) |

## D2. The work unit

A `WorkUnit` names responsibility toward a result, apart from whatever executes it:

```ts
{ id, kind, owner: { repository, record }, commitment?, space?, cycle,
  subject: { artifact, revision }, results: [], resources: [] }
```

**An adapter interface, never a work database.** Each kind's adapter names its canonical record and that record's one
writer, and reads the unit from it. A `WorkUnit` is a projection.

| Kind | Canonical record | Writer | Adapter |
|---|---|---|---|
| branch work | the session branch's `session-branches.json` entry, with the landing entries that reference it | the driver: the tree's opening, the end judgement, the landing | **the first**, built over parts B and C |
| quest | `QuestStore` (Quests.cs) | the service, through `QuestExchange` | after the branch adapter, read-only first |
| ask | `AskStore` (ServiceFactory.cs:291) | the service | read-only, as the quest's space |
| delegated task | a quest another repository's session took: its take (D69; the C0 audit's row 2), the proposal's §3 `Commitment` | the service | after the collaboration checkpoint (§D10) |

**Deployments, documents and plugin artifacts are not kinds yet.** No canonical record holds one today, so for now each
is a **result** or a **resource** of a step on another unit (§D3, §D4). It becomes a kind of its own only when a lifecycle
needs it, and then its adapter names its store first.

- **Identity.** A branch unit's id is written once, into the session branch's entry when its tree opens. Landing entries
  reference the ids of the units whose work they carry (`Units`), so a landing branch carrying several sessions' work
  (LAND2c's advances) is one result of several units, never a unit itself.
- **Sessions execute; they do not own.** A session is an execution binding (the proposal's §3). A hand-over changes the
  executor and keeps the unit (D163). A native subagent's binding (C1a's lineage) identifies an executor, never an owner.
- **Cycles stay optional routines over units.** They never become the fabric's universal execution graph (ADR-A).

## D3. Ownership: the inventory and its dispositions

Every resource a unit's work made has an **ownership entry**:

```ts
{ id, unit, resource, revision, createdBy, role: "temporary" | "result" | "evidence" | "recovery",
  cleanup: { owner, policy, retainUntil?, proof? } }
```

**Derived where a record already names the resource, recorded only where none does.** No receipt duplicates a record:

| Resource | Role | Where its entry comes from | Who cleans it |
|---|---|---|---|
| the session's tree (worktree) | temporary | the session record, the worktree list, `locks/trees` | Daoris: moved aside, with every guard (D88) |
| its moved-aside folder | recovery | `trees/.tidied/…`, with its purge date | Daoris, at its purge, fourteen days after the move |
| its session branches | temporary | `session-branches.json` | Daoris, with fresh proof (§C3) |
| a landing branch | result, or temporary once the line holds it | `landings.json` | Daoris, as AUTOTIDY2b, when not the result |
| the recovery refs | recovery | `refs/daoris/discarded/` | their own rule; never the end's |
| the auto-landing entry | temporary | `sessions/auto-landings.json` | Daoris, when the unit ends |
| the session's records and logs (`sessions/<id>/…`, `lineage.jsonl`) | evidence | the session record | never the end's: history, which D153's clearing governs |
| a human task's attachments (§D5) | evidence | the unit's evidence folder, a new home entry placed under D172 | never the end's |
| what a plugin step created (`creates`) | temporary or result, as declared | a **receipt** in the plugin's `work/step`, kept on the unit's canonical record (`Resources`, at most 32) | the plugin, through its cleanup adapter only |

**Each entry gets a disposition:** *kept* (result, evidence), *retained until* (recovery), *removed* (with proof),
*blocked* (with the guard or the unknown that blocks it), or *owner-notified* (an external resource, its owner told).
**"Everything cleaned" means every entry has a disposition.** An unknown owner blocks the unit's cleanup; it is never a
guess.

**Who removes what:**

- **Daoris** removes only the types its own executors own (worktrees, its branches, moved-aside folders at their purge),
  with fresh proof and every guard.
- **A plugin's local resources** go only through the plugin's registered cleanup adapter. Its manifest declares
  `resources: [{ schema, cleanup: "work/cleanup" }]`. At a true end, Daoris sends a cleanup request for each receipt the
  plugin owns. The plugin removes what it created and answers a disposition with its proof. Daoris runs no command for
  it, and never hands it a path: `resource` is an opaque id the plugin issued.
- **External resources** stay their owner's: Daoris records the disposition, or a notice that the resource was retained.

**The cleanup is durable and idempotent.** A crash between a deletion and the recording of its disposition must converge:

- **Daoris persists the request before sending it:** `cleanup: { state: requested, key, at, tries }` on the receipt.
- **The frame:** `work/cleanup { receipt, resource, idempotencyKey }`, answered
  `{ disposition: "removed" | "already-gone" | "retained" | "failed", proof?, message? }`.
- **After a restart or a lost answer**, Daoris sends the same request again with the same key. The plugin answers
  `already-gone` for what it already removed, so a second delivery never fails for having succeeded.
- **`failed` is retried with a back-off** (1, 5, 15 and 60 minutes, then daily). After seven days it is shown as
  *blocked* in the milestone, with the plugin's message. It never disappears.
- **Every receipt ends** in `removed`, `already-gone` or `retained` (with its reason), or stays visibly blocked.

**Invalidation keeps resource accountability.** A deployment may still be running when its prerequisite or the revision
changes. The attempt is then marked `superseded`:

- its completion is still recorded, as the outcome of a stale attempt, and never counts as current evidence;
- its receipts are still taken, reconciled and kept in the inventory;
- its resources become temporary whatever role they declared, since a stale attempt has no result. They are cleaned
  through the adapter at once, unless the plugin answers `retained` (a reused environment, say).

**A unit retires, a branch is removed.** The two are kept apart:

- **The branch's removal** is the disposition of its git entries: the tree, the session branches, the landing's other
  branches.
- **The unit's retirement** comes only once its end is reached (or the person drops it), every entry has a terminal
  disposition, and no attempt is unresolved.
- §C3 lets the cleanup take the branch at *held* while later steps remain. The unit then stays **open** on its record:
  its revision, frozen at the removal, still binds the remaining steps' evidence, and its attempts, resources and
  cleanup obligations stay live.
- **The traces' pruning** (`TracesKept`, LEFT3) never prunes an open unit. Only a retired one becomes an inert trace.

**A plugin's word** establishes neither safe deletion of anything Daoris owns nor git preservation (*held*). A performed
step never removes anything.

## D4. One step contract

Built-in, human and plugin steps share one declaration:

```json
{ "id": "acme.deploy/deploy", "version": 1, "after": ["line", "human/qa"],
  "inputs": { "release": { "step": "acme.build/build", "output": "artifact" } },
  "reach": "performed", "evidence": { "schema": "acme.deploy/receipt@1" },
  "perform": { "point": "work/perform" }, "retry": { "maxAttempts": 3, "delaysSeconds": [5, 30] },
  "timeoutSeconds": 600, "creates": ["acme.deploy/preview@1"] }
```

- **`reach`** is `observed` (git or a plugin reports it: every built-in step, and §C4's events), `performed` (Daoris asks
  a plugin to do it, §D6) or `pressed` (a person, §D5).
- **`after`** names prerequisites, which may be several. **`inputs`** reference earlier steps' immutable outputs by id,
  never copied payloads.
- **Validated before a cycle is saved:** a missing reference, a dependency cycle, an unsupported version, an unreachable
  step, a performer not installed, or a schema not declared is refused at the door that saves it, naming the field.
- **Evidence is bound to the work:** `{ unit, subjectRevision, stepVersion, inputs, prerequisiteReceipts }`. A branch's
  revision is the repository's identity and the full tip (§B5); any other subject is named by an immutable id and its
  revision. **A changed dependency invalidates everything downstream.** This generalizes §B5's lapse: a new tip, a
  changed input, or an invalidated prerequisite makes every later step's acceptance stale.
- **Outcomes** are `reached`, `failed`, `unknown`, and for a person, `rejected`.
  - A rejection blocks the cycle's progress and keeps its reason; reopening is a new attempt.
  - Only failures the step declares transient retry automatically.
  - An unknown external outcome must be reconciled before another attempt.
- **Timeouts** are persisted deadlines, delivered as `timer.step` events. They never mean success, and never trigger a
  deletion.
- **The built-in steps** (`claimed`, `handed`, `pushed`, `pull-request`, `line`) are declarations with `reach: observed`,
  their evidence where §C7 keeps it. `claimed` still can never be an end.
- **Where steps are declared:** a plugin's in its manifest (twinned, §C4), a human task's in the cycle's configuration
  (§D5), and the built-ins in code. A cycle composes them by id.

## D5. Human tasks

A human step is a task, not a bare press:

- It declares a `brief`, an `attachmentSchema` (a note, files, links; each `required` or not), an `approveLabel` and a
  `rejectLabel`, all the person's words, shown as written.
- **It appears in What needs you and on the work's cycle line**, showing the subject and its revision, the prerequisites
  reached, the checks the brief asks for, and its attachments.
- **Approve or reject** records the person, the door (window, confirmed terminal, keyed terminal), the revision, the
  evidence references and the reason. A rejection requires a reason. Required attachments must be present before
  *Approve* is offered.
- **Only D156's person doors answer it** (§C2's rules stand): never an agent, never a plugin. The terminal twin is
  `daoris-driver work task <unit> <step> approve|reject`.
- It lapses when anything it was bound to changes (§D4), and is offered again as a new attempt. The old answer stays
  shown, as about its revision.

## D6. Performed steps: `work/perform`

`work/watch` still tells the plugin which units concern it, and still resyncs (§C4). When a performed step becomes
eligible, Daoris sends a request on the same supervised connection:

```json
{ "jsonrpc": "2.0", "id": 7, "method": "work/perform",
  "params": { "unit": "u1", "step": "acme.deploy/deploy", "stepVersion": 1, "invocation": "i1",
              "idempotencyKey": "k1", "subjectRevision": "r1", "inputs": ["artifact:a1"], "deadlineAt": "…" } }
```

- **The plugin's answer acknowledges receipt.** Completion arrives later through `work/step`, carrying the `invocation`,
  the outcome, evidence and output references, and ownership receipts.
- **Dispatch is persisted before it is sent.** The attempt (invocation, key, revision, inputs, deadline, state) is written
  on the unit's canonical record first. A redelivery after a restart uses the same key, and the plugin deduplicates it
  durably and reconciles its external effects. A new key is a deliberate new attempt: a declared retry, or the person's.
  A step that declares neither idempotency nor reconciliation gets no automatic retry.
- **This amends §C7:** dispatch receipts and unresolved attempts survive a restart; the due sets stay ephemeral.
- **The capability is negotiated.** The handshake names `work.perform@1` among the plugin's capabilities, and
  `protocolVersion` stays 1 (D148 point 8's rule). §C4's sender checks, bounds and generations apply to every frame.

**The performer's contract**, which the kit promises (§D9), so that no author has to invent it.

*What it declares.* In the step's `perform`:

```json
"perform": { "point": "work/perform", "idempotent": true, "reconcile": "work/reconcile",
             "transient": ["unavailable", "rate-limited", "timeout"] }
```

- `idempotent`: the plugin deduplicates a redelivered key durably, so the same key never causes a second external
  effect.
- `reconcile`: the plugin can answer what became of an attempt.
- `transient`: the failure codes worth retrying. A `failed` outcome carries a `code`; only a listed code retries, under
  the step's `retry`, and every other code stops the step for the person.
- Automatic retry needs `idempotent`, or `reconcile`, or both. A step with neither is never redelivered by itself.

*How outputs flow.*

- A `reached` outcome carries `outputs: { <name>: { ref: "<schema>:<opaque id>", revision } }`: at most 8, each ref at
  most 200 characters. Daoris keeps them on the step's record (§C7), never their payloads.
- A later step's `inputs` name `{ step, output }`. At dispatch, Daoris resolves each one to the ref recorded on the
  producing step's **current, valid** acceptance. A stale or invalidated producer leaves the consumer ineligible, never
  given an old ref.
- The ref is opaque to Daoris. The plugin that made it, or one declaring its schema, reads it.

*How reconciliation is asked.* Before any new attempt, Daoris asks after an attempt whose outcome is unknown: after a
restart with an unresolved attempt, after its deadline, or after a lost connection. The request:

`work/reconcile { unit, step, invocation, idempotencyKey, subjectRevision }`

It is answered `{ state: "done" | "not-started" | "running" | "failed" | "unknown", outcome?, resources? }`:

| The answer | What Daoris does |
|---|---|
| `done` | records `outcome` and `resources` as if they came by `work/step` |
| `not-started` | delivers again with the same key |
| `running` | waits to the deadline |
| `failed` | follows `transient` |
| `unknown` | stops the step, and the person sees it; no new attempt is made by itself |

**The trust model.** Plugins are trusted code. A hook process is an ordinary process on the person's account, started in
the plugin's folder and told the home (HookProcess.cs:83-99). Like anything the account runs, it can read and write
whatever that account can, Daoris's stores included. Installing a plugin and switching it on (D64, D100) is the person's
decision to trust it, as D156 §4.4 already says of anything on one account.

So these are **protocol obligations**, which Daoris checks on every frame it receives (refusing a frame that claims what
it may not, §C4) and the kit's contract test checks for each plugin. They are not isolation, and no enforcement against
arbitrary plugin code is promised. A performer, whatever its own authority, does not:

- write a Daoris store, a rule or a ref Daoris owns;
- remove a resource except through its cleanup adapter (§D3);
- stand in for a person;
- make another step's evidence;
- inherit a publishing right or a credential (ADR-F);
- write into another repository.

It acts with its own external authority only. If plugins from sources the person does not trust are ever wanted, running
them isolated (a restricted token, an app container, another account) needs a design first. FABRIC-ISOLATE1 is named for
that, and not scheduled.

## D7. Automatic by default

- **With nothing configured**, branch work is exactly part C's default: `end: line`, no added steps, the cleanup at
  *held*. The existing landing choices and person gates are unchanged, and events replace the waits. No plugin is
  installed and no approval is added.
- **A configured cycle runs every eligible automatic step through to its end.** Observed steps are heard, and performed
  steps are dispatched as soon as their prerequisites and inputs are reached. Only a human task, or an authority not
  granted, stops it, and that stop is shown: What needs you names the task, or the authority missing and who grants it.
- **Authority is never granted by a step.** Adding a step gives a plugin nothing. A performer uses its own, independently
  approved authority, and production stays the person's press (D157 §3.6).
- **Configuration homes:** branch cycles on the landing rules (§C2, D164's inheritance). Other kinds use `workCycles` and
  `workspaceWorkCycles`, keyed by kind, inherited the same way. No kind has two homes, and named workflows keep their
  version binding.
- **D88's line proof** remains the branch default. The pre-line exception (§C3) still needs preservation in the kept
  result, independently verified by git. A plugin's evidence alone never permits a removal.

## D8. The bounded menu, completed

**Transitions** are typed and validated; source events stay wakes:

- `work.step`: `reached`, `failed`, `rejected`, `invalidated`;
- `work.held`: `acquired`, `lost`;
- `work.end`: `reached`, `invalidated`;
- `work.cleanup`: `completed`, `blocked`;
- `timer.step`: `due`.

Each carries an envelope: `{ eventId, unit, revision, causationId, receipt }`.

**The four effects.** None writes domain state or touches a resource:

- `notifyPerson`: an item in What needs you, with the transition's words;
- `deliverPluginEvent`: an observation to a named, switched-on plugin (a subscription; performance belongs to §D6);
- `requestReconcile`: the unit's judgement asked again, at most once per unit and transition;
- `offerPersonAction`: an existing person's action offered in What needs you, the person pressing it at a person's door.
  For example, a quest's own done (`POST /api/quests/{id}/done`, a `Person` door, QUESTCLOSE1).

**Setup** gains **Work → Cycle / Steps / Cleanup / Event responses**, with the terminal twin
`daoris-driver work cycle|responses`. That is a new verb group beside `workflow`, which stays D157's choice of a named
workflow. Both doors use the shared handlers and the same validation. Selectors are enumerated: a kind, a step, a
transition. There are no expressions and no scripts.

**Three scenarios:**

1. Line → a QA task with a brief and a required screenshot → approval → a deploy plugin performs → its deployment receipt
   is the end.
2. Line → an approved deploy plugin performs → the end is reached automatically → `notifyPerson`.
3. The end → `notifyPerson` → `offerPersonAction`, the quest's own done.

**Why responses terminate.** That no effect writes is not enough: `requestReconcile` runs a judge, which can write facts,
and `deliverPluginEvent` reaches a process, which can send another event. Rate limits bound the traffic, but they prove
nothing. Termination rests on four rules, each with its canary:

1. **A transition has an identity**: `(unit, kind, step, revision, attempt, outcome)`. Each effect runs at most once per
   identity. `notifyPerson` and `offerPersonAction` are keyed by it in What needs you. `deliverPluginEvent` carries the
   transition's `eventId`, which the plugin must deduplicate. `requestReconcile` only adds the unit to its due set, which
   coalesces.
2. **A judge is a function of the world.** Run again on unchanged git, unchanged kept evidence and unchanged person
   answers, it finds the same facts and emits nothing. So `requestReconcile` alone can never make a transition.
3. **A plugin cannot mint attempts.** For one `(step, revision)`, attempts are created only by Daoris's dispatch under
   the step's `retry`, by the person, or by a new revision, never by a plugin's event. Each attempt's outcome is final
   once `reached`, `failed` or `rejected`. An observed step may change its reported state for one revision at most 8
   times; past that it is refused (`too-many`) and shown *unknown* to the person.
4. **No response creates a revision.** No effect writes git, a ledger family or a step's evidence.

So for every unit, the measure *remaining attempts plus remaining observation changes, over its steps at its revision*
is finite. Every transition a response causes lowers it, and no response raises it. A chain of responses therefore ends;
only the world (a commit, a person, a new event the rules accept) starts a new one. Canaries 32 and 33 hold it with a
plugin that echoes every delivered event, and one that alternates *reached* and *failed*.

## D9. The plugin author's path

- **The manifest:** `steps` hold step contracts (§D4); `hooks.points` names `work/watch`, `work/perform`, `work/step` and,
  where it cleans, `work/cleanup`; `resources` names the schemas it creates and who cleans them.
- **The kit scaffolds** a performer, a receipt schema, a durable idempotency journal, a watch and resync handler, the
  `work/reconcile` and `work/cleanup` handlers, and a contract test. It implements §D6's performer's contract (what is
  declared, how outputs flow, how reconciliation is asked) and §D3's durable cleanup, so an author fills in only the
  external calls.
- **`daoris-driver plugins try --cycle <fixture>`** drives synthetic eligibility, duplicate delivery, failure, timeout,
  restart and stale evidence, without a real deployment.
- **The author writes** the external execution, its reconciliation and the evidence. **Daoris supplies** ordering,
  references, dispatch recovery, the task's presentation, deadlines, validation and the cleanup accounting.
- **The contract test** must show:
  - one external effect under duplicate delivery;
  - each `work/reconcile` answer;
  - `already-gone` on a repeated cleanup;
  - an echoed event deduplicated;
  - no frame claiming a forbidden Daoris mutation.

  It checks the protocol, not the code's honesty (§D6's trust model).

## D10. The collaboration fit

- **A delegated step references a quest commitment**, its correlated messages (the ask's channel, D167) and its returned
  artifacts by reference. The receiving repository accepts the responsibility and executes locally (repository owns its
  work).
- **Native subagents** keep native spawning. Their bindings identify executors, never owners, and D173's lineage
  restrictions (C2a), restricted grants and honest partial visibility stand.
- **The checkpoint is its own gate:** FABRIC-ACP1 → FABRIC-C2a → FABRIC-C2b, then FABRIC-COLLAB1, the first cross-harness
  round trip of a message and an artifact. Delegation (FABRIC-DELEGATE1) follows its contract tests. Neither waits for
  the menu, and no correctness proof is dropped.

## D11. Rejected, in part D

| Shape | Why not |
|---|---|
| **The branch as the universal unit** | It excludes work that is not git's (a quest, a delegated task, a deployment) |
| **A new work database** | A second authoritative store of quests, asks and branches; adapters read the canonical records |
| **A kind with no canonical record** (deployment, document, plugin artifact as kinds now) | An adapter must name its store and writer first; until then they are results and resources |
| **Label-only human approvals** | A press with no brief, evidence or reason weakens the person's judgement into a click |
| **Arbitrary cleanup scripts, or a plugin-supplied deletion path** | They bypass ownership; a plugin cleans only what it created, through its adapter, by an opaque id |
| **Receipts for what a record already names** | A second store of the same fact |
| **Volatile dispatch deduplication** | A crash would duplicate an external effect |
| **A step that grants authority** | Authority is approved apart from the step, and production stays the person's |
| **A central scheduler assigning all collaboration** | Contrary to ADR-A and D173's foundation |
| **Automatic quest closure in this pass** | It would amend the menu's non-writing rule and the quest's contract (§Q1) |

## D12. The rows, re-cut toward what the person feels first

*Superseded by §E10 for every row not yet built.* This replaced the row table under **Canaries and proof, and the rows**.

**The collaboration gate runs in parallel, from now**, in its own lane, apart from the order below:

| Gate | Row | Lanes | After | Fate | Builds |
|---|---|---|---|---|---|
| G1 | FABRIC-ACP1 → FABRIC-C2a → FABRIC-C2b | parent; service | as their rows | survive, **in parallel with every row below** | the collaboration checkpoint |
| G2 | FABRIC-COLLAB1 | service, driver | G1 | **new** | one cross-harness round trip: a message and an artifact by reference |
| G3 | FABRIC-DELEGATE1 | service, driver | G2, UNIT1b | **new** | a delegated step as a quest commitment, the delegated-task adapter |

**The lifecycle, in order.** Each row is sized near a hundred turns:

| Order | Row | Lanes | After | Fate | Builds |
|---|---|---|---|---|---|
| 1 | FABRIC-EVENT2 | driver | - | **built** (2026-10-17) | the change watch (§A3, §A4.1) |
| 2 | FABRIC-END2a | driver | - | **built** (2026-10-17) | the end judgement, extracted, the cycle an input |
| 3 | FABRIC-EVENT2b, 2c, 2d | driver; modules | 1 | **built** (2026-10-17) | the watch's follow-ups |
| 4 | FABRIC-CHAIN2 | driver | 1 | **built** (2026-10-17), whole | the chains: fact links, effect links pending until done, topic deadlines as `timer` events, the ranked table. The menu stays CHAIN3's |
| 5 | FABRIC-END2 | driver | 2, 4; **the cross-process writer chosen first** (RPC1's pipe for the verbs that write these records, or a lock on the two files) | **merges** END2b and END2c, default cycle only | `Held` with `Since`, traces never pruning an open unit, the branch unit's id, lapse on standing branches |
| 6 | FABRIC-END3 | driver | 1, 5 | survives | the ref watcher |
| 7 | FABRIC-MILESTONE1a | driver | 5 | **new** | the default inventory **derived** (§D3's rows for the branch adapter, read-only) and the milestone's answer; `trees state`, `work state` |
| 8 | FABRIC-MILESTONE1b | web-shell, modules | 7 | **new**, from the default parts of END4b and END5 | the milestone on the work's foot, What needs you, Branches and WorkspaceTidied, and Ask Daoris |
| 9 | FABRIC-END4a | driver | 6, 7 | survives, **default only** | the safe cleanup at *held*, acting on the derived inventory; a branch's removal kept apart from the unit's retirement |
| 10 | FABRIC-EVENT3a | driver | 1, 4 | **built** (2026-10-17) | the look's topics |
| 11 | FABRIC-EVENT3b | service, driver | 10 | survives, **moved forward** | the change stream |
| 12 | FABRIC-EVENT4 | driver, modules | 1, 4 | survives, **moved forward** | the home's record files |
| 13 | FABRIC-END7 | driver | 6, 11, 12 | survives | auto-landing on its wakes |
| 14 | FABRIC-END6 | driver | 6 | survives | a fetch and a plugin's ask when due |
| 15 | FABRIC-UNIT1a | driver | 5 | **split** | the adapter model: `WorkUnit`, the branch adapter, `Units` on landings, read-only quest and ask adapters |
| 16 | FABRIC-UNIT1b | driver | 15 | **split** | the common step contract (§D4): declaration, validation, evidence binding, invalidation, outcomes, persisted `timer.step` |
| 17 | FABRIC-UNIT1c | driver, cli | 16 | **split**, takes CYCLE1a | the cycle's configuration: `cycle` on the landing rule and `workCycles`, both twins, D164, the refusals |
| 18 | FABRIC-UNIT1d | driver, cli | 16 | **split**, takes PLUGSTEP1a | the manifest twins: `steps`, `resources`, `perform` (`idempotent`, `reconcile`, `transient`) |
| 19 | FABRIC-PERFORM1a | driver | 18 | **split**, takes PLUGSTEP1b and 1d | transport and supervision: the reverse `work/step` with its bounds, the lease's loop owning the process, the capability, generations and `resync` |
| 20 | FABRIC-PERFORM1b | driver | 17, 19 | **split** | durable execution: `work/perform`, dispatch persisted before sending, keys, `work/reconcile`, retries by `transient`, superseded attempts |
| 21 | FABRIC-PERFORM1c | driver | 20 | **split**, takes PLUGSTEP1e | evidence authorization: §C4's rules, evidence binding, outputs resolved for later inputs |
| 22 | FABRIC-PERFORM1d | driver | 9, 21 | **split** | resource cleanup: receipts and `Resources`, durable idempotent `work/cleanup`, accountability through invalidation, retirement with attempts and resources |
| 23 | FABRIC-PERFORM1e | driver, examples | 22 | **merges** PLUGSTEP1c and 1f | the kit's scaffold of §D6's and §D3's contracts, `plugins try --cycle`, an example performed plugin |
| 24 | FABRIC-END4c | driver | 9, 17, 22 | **split** from END4a | the configured cleanup: the cycle's `wait`, the result per cycle, the pre-line exception |
| 25 | FABRIC-HUMAN1 | driver, modules | 16, 17; **RPC1d and D156 §4.2's confirmation** for the terminal twin | survives, **reshaped**, independent of the PERFORM rows | human tasks (§D5): brief, attachments, approve or reject, their doors and twin |
| 26 | FABRIC-CYCLE1b | web-shell, modules | 17, 25 | survives, **reshaped** | Setup's *Work → Cycle / Steps / Cleanup* |
| 27 | FABRIC-END4b | driver, web-shell | 8, 24, 25 | survives, **configured part only** | the page draws configured cycles, tasks and performed steps |
| 28 | FABRIC-CHAIN3 | driver, cli, web-shell | 4, 26 | survives, **specified** | §D8: transitions, the four effects, *Event responses*, `work responses` |
| 29 | FABRIC-END5 | driver, modules | 27 | survives, **configured part only** | the CLI and Ask Daoris read configured cycles |
| 30 | FABRIC-EVENT5a, 5b, EVENT6, C1b | as their rows | as their rows | survive | focused integrations (PROOF-UNREAD1 is built) |
| 31 | FABRIC-EVENT7 | driver, cli | 6, 11, 12, 13 | survives, measurement-gated | the clocks as deadlines |

FABRIC-ISOLATE1 (§D6's trust model) is named and not scheduled.

**Gone:**

- **CYCLE0**, done: its amendments are in §C2 and §C3, and part D supersedes the human press.
- **CYCLE1a**, now UNIT1c; **PLUGSTEP1a**, now UNIT1d.
- **PLUGSTEP1b-1f**, now PERFORM1a-e.
- **CHAIN2**: built whole (2026-10-17), so no CHAIN2a; the menu is CHAIN3's.
- **END2b and END2c**, merged into END2.
- **END4a**, split into its default part and END4c.
- **MILESTONE1**, split into 1a and 1b.

**Canaries for part D:**

26. With nothing configured, every suite of the default lifecycle is unchanged, and the milestone shows on the page.
27. Every inventory entry has a disposition, and an unknown owner blocks the cleanup and says so.
28. A redelivered `work/perform` after a restart carries the same key and causes one external effect.
29. A changed input or prerequisite invalidates every downstream acceptance.
30. A human task's *Approve* is refused without its required attachments, and *Reject* without its reason.
31. No menu effect writes a fact, a ref or a ledger family, and each effect runs at most once per transition identity.
32. A plugin that echoes every delivered event causes no second transition.
33. A plugin that alternates *reached* and *failed* for one revision stops at the bound and reads *unknown*.
34. A crash between a plugin's deletion and its recorded disposition converges: the same key is sent again and answered
    `already-gone`.
35. A branch removed while a later step, an attempt or a resource is unresolved leaves its unit open. The pruning of
    traces skips it, and it retires once its last entry is terminal.
36. A superseded attempt's completion is recorded and never counted as current evidence, and its receipts are reconciled
    and cleaned.
37. An output ref from an invalidated producer is never handed to a consumer, and an unknown outcome is reconciled
    before any new attempt.

## D13. Questions for the owner

*Answered at part D's acceptance:* the quest's close is a node to set (§E5), and waiting on another task is a node
(§E6). The text below is kept as asked.

1. **Should reaching the end close the quest by itself?** Today only the person's own done (`POST /api/quests/{id}/done`)
   or the agent's done closes one.
   - (a) Offer it: `offerPersonAction` puts the quest's done in front of the person.
   - (b) Automatic: a new contract by which the driver closes an open quest when its work's end is reached. No existing
     door does this. The evidence door (`/api/quests/{id}/evidence`, a `Driver` door) records a verdict only on a quest
     already done and awaiting its evidence, and refuses any other (QuestExchange.cs:1529-1540).

   *Recommendation:* (a) now. (b) is a change to the quest's contract and to D46's line, for the evidence rows to weigh.
   This replaces the earlier open question on the end and the quest.
2. **Which kind of work should the fabric take on second, after branch work?**
   - (a) The delegated task (FABRIC-DELEGATE1), riding the collaboration checkpoint.
   - (b) A quest's own cycle: claimed, its evidence verified, the person's done.
   - (c) A deployment as a unit of its own, with a store.

   *Recommendation:* (a). It is the collaboration the fabric exists for, and its gate is already on the way. (b) follows
   with question 1's answer.

---

# Part E — nodes and wires

**Proposed, 2026-10-17, pending a second maker's read and the owner's yes.** The owner accepted part D (D173's note
*"part D accepted, with the owner's amendments: nodes and wires"*) and amended it:

- *"each step can have its own state, so we don't have any difference between different types of steps, so they can link
  and communicate normally; and I think the rest is fine"*
- *"close automatically can be a step to set"*
- *"waiting for another task can be a step too (and we can name those steps better, more like nodes? and we have
  different ways to wire them)"*

Codex proposed this part. It is taken here with each claim checked against D157's workflow code and design,
`QuestExchange`, the done judgement and D69's claims. Where it conflicts with parts A-D, part E governs (§E1).

## E1. What part E changes

| Where | Change |
|---|---|
| §D4, the step contract | Renamed **node**. Every kind shares one state table (§E2), and `reach` gains `waited` |
| §C2, §D7, the cycle | The cycle is a **graph** of nodes joined by **wires** (§E3, §E7). Its end, cleanup wait and result stay the graph's |
| §D5, §D6 | Human tasks are `pressed` nodes, and plugin steps are `observed` or `performed` nodes. Their contracts stand |
| D157, workflows | Workflows **become graphs**: one definition and one evaluation model (§E7) |
| §D8, the menu | Unchanged. Its responses are subscriptions, never wires (§E8) |
| §D13 | Both questions are answered: closing the quest is a node, `quest.close` (§E5), and waiting on other work is a node, `wait.unit` (§E6) |
| §D12, the rows | Re-cut (§E10) |

## E2. One node contract, one state table

A node is `{ id, kind, version, reach, inputs, outputs, evidence, retry?, timeoutSeconds?, options? }`. Its `kind`
chooses an adapter; its `reach` says how it starts (`observed`, `performed`, `pressed`, `waited`). **The lifecycle is
literally one, for every node.** What differs between kinds is only **evidence authority**: who may report each outcome.

| From → to | When | Who causes it |
|---|---|---|
| blocked → ready | its wires, inputs and authority are satisfied | Daoris's evaluation |
| ready → blocked | that stops being true before it starts | Daoris's evaluation |
| ready → active | it starts: observing, dispatching, offering the task, or waiting | Daoris |
| active → reached | accepted, bound evidence | the node's **reach authority** |
| active → failed | an authoritative negative outcome; silence and a deadline never count | the node's **failure authority** |
| active → rejected | a person's rejection, with a reason | a person at D156's doors, on a node whose kind lets a person judge it |
| active → unknown | the outcome cannot be established now | Daoris |
| unknown → active | reconciliation says it is running, or never started (redelivered with the same key, §D6) | Daoris, on the authority's answer |
| unknown → reached or failed | reconciliation establishes the outcome | the authority |
| unknown → reached or failed | an attempt held because nothing else can say what became of it (§D6), settled on the person's word, never as the plugin's: a plugin's performed node alone (D173's FABRIC-PERFORM1g note) | the person |
| failed → ready | a declared retry: a `transient` code with attempts left (§D6), as a new attempt | Daoris, by the node's policy |
| failed, rejected or stopped → ready | a new attempt the person asks for | the person |
| ready, blocked or active → skipped | the person passes it over for this graph and run, with their words (D157's skip) | the person, where the node allows a skip |
| active → skipped | its kind's record passes the attempt over, where nothing is asked of it: an `opinion` whose rule asks none, or an optional one none could be had. It carries no words, and a changed binding lapses it (D173's FABRIC-WIRE1g note) | the record's authority: an opinion's gate alone |
| active → skipped | the same, for two more kinds (D173's FABRIC-WIRE1h note): a `look` whose review the chain's or the ask's choice turns off for this work, and a `pull-request` whose work went into the line with no branch landed to open one on | the record's authority: the review gate's, or the landing record's |
| skipped → blocked | the person withdraws their skip; it is judged again | the person |
| skipped → invalidated | its graph or run is another: a skip holds for its own alone | Daoris |
| any live state → stopped | the person stops this node | the person |
| any bound state → invalidated | its binding changed. History is kept, and unresolved attempts are superseded (§D3) | Daoris |
| invalidated → blocked or ready | judged again on a fresh binding | Daoris |

**The authority by kind:**

| Kind | Reach authority | Failure authority | May a person reject? |
|---|---|---|---|
| `handed`, `line` | git's proof | none: not yet is *pending*, never failed | no |
| `pushed` | the plugin's push answer and the ref | the plugin's failed push answer | no |
| `pull-request`, a plugin's own node | the plugin's believed answer or event (§C4, §D6) | its believed `failed` or `abandoned`, with a code | only where the node declares it |
| `human`, `look`, `go-ahead` | the person's approval, *reviewed* or yes | none | yes: their reject, *not yet* or refusal |
| `wait.unit` | the commitment's receipt (§E6) | the commitment declined or abandoned by its owner | no |
| `quest.close` | the close door's write (§E5) | the door's authoritative refusal: a lost claim, a declined quest | no |
| an `all` join (§E7) | every member reached or skipped | a member failed, under the group's policy | no |

- **A skipped node** satisfies `after` wires, as D157's *settled* does (`WorkflowRunStates.Settled`, WorkflowRun.cs:39).
  It produces no outputs, so a `data` wire from it is never satisfied.
- **A `wait` wire targets `wait.unit` only.** It is that boundary adapter's input, not a special case in the lifecycle.
  Every other wire connects any nodes alike.
- **An unknown outcome** is reconciled before any new attempt, and so is a dispatched attempt (a performed or waited node's)
  moved off `active` before its authority answered. Every outcome names its attempt, and a reach must be the binding its
  attempt started on (D173's FABRIC-NODE1c note).
- **An acceptance binds** `{ unit, subjectRevision, graphDigest, nodeVersion, inputs, prerequisiteReceipts, attempt }`. A
  branch's revision is its repository's identity and its full tip.
- **A changed dependency invalidates every acceptance downstream, transitively.** Old answers and resource receipts stay
  accountable (§D3). Invalidation never reverses an external effect, and never reopens a closed quest by itself.
- **Ports are references.** A reached node exposes an immutable `evidence` reference and its declared outputs, each
  `{ schema, ref, revision, acceptance }`. A consumer receives references, never copied payloads or file paths, and a
  resolver checks permission for itself. Human attachments, plugin artifacts and built-in proofs use the same ports.
- **Reporting:** adapters write through their canonical writers. An accepted change emits `work.node` (it replaces §D8's
  `work.step`). Source events stay wakes, except §D4 and §D6's validated evidence frames. §C4's sender checks,
  generations, `resync` and bounds stand.

`line`, a human QA, a plugin's deploy, `wait.unit` and `quest.close` all use this table. Their evidence differs; their
scheduling and how they connect do not.

## E3. Wires

A wire is `{ id, kind, from, to, group? }`, with no expressions, scripts or commands.

| Kind | Meaning |
|---|---|
| `after` | the target becomes eligible when the source has a current `reached` acceptance |
| `data` | names a source output and a target input. Needs a current `reached`, compatible schemas and an immutable output reference |
| `wait` | gives a `wait.unit` node what it waits for: another unit's named node or end, at an agreed revision (§E6) |
| `trigger` | an accepted, typed transition receipt starts the target once for that receipt and binding. A raw source hint only asks for reconciliation |
| `failed` | the path taken once the source's declared retries are exhausted |
| `rejected` | the path taken on the person's recorded rejection |

- **Unknown, invalidated and stopped satisfy no wire.**
- **Groups:** a node's incoming eligibility wires form `allOf` or `anyOf` groups, and groups combine by all.
  - An `anyOf` records **one witness**, by wire-id order when several qualify at once, and cancels nothing. Losing the
    witness invalidates its consumers; switching to another witness takes a fresh binding.
  - A fork starts every eligible target, within the local concurrency limits (§A3).
- **Data inputs stay mandatory** whatever the groups say. Alternative producers need an explicitly typed choice input.
- **Refused when saved:**
  - a local dependency cycle, a node's own `trigger` loop included;
  - a missing reference, or an incompatible schema;
  - a wire to a node another repository owns. A cross-repository dependency ends at a local `wait.unit`, through a
    commitment (§E6).

## E4. The built-in nodes

- **Observed:** `claimed`, `handed`, `pushed`, `pull-request` and `line` (§C2's proofs). `claimed` can never be the end.
- **Pressed:** `human` (§D5), a task at D156's doors.
- **Performed:** a plugin's (§D6), and Daoris's own `quest.close` (§E5).
- **Waited:** `wait.unit` (§E6).
- **D157's kinds** map onto nodes:
  - `work` is the quest's done or `ready` (§E5), observed;
  - `opinion` and `look` are their gates as observed nodes, before the landing, in D157 §2.2's fixed order;
  - `landing` is `handed`;
  - `pull-request` stays itself;
  - `check` and `stage` are performed or observed plugin nodes (D157 §3.5-§3.6, WORKFLOW1k-1l's work, now on the PERFORM
    rows);
  - `go-ahead` is a pressed node on D135's answer;
  - `all` is an `all` join node, which keeps the group's identity and its failure policy (§E7).

## E5. `quest.close`, the quest's close as a node

The owner: *"close automatically can be a step to set"*. Where a workspace's graph holds it, and only there, the quest
closes as a node.

**Why it needs more than a door.** Everything that judges a result today hangs on the Done:

- the evidence a done waits on and the hold are computed only for a quest whose status is Done (`EvidenceWanted`,
  `AwaitsEvidence` and `Hold`, Quests.cs:144-158, 187-192);
- the evidence door refuses a quest that is not done (QuestExchange.cs:1529-1540);
- a chain's next quest is published only by an unheld Done, in the same transaction (Quests.cs:1120-1128);
- automatic landing starts at the done (D145), and D157's `work` step counts the chain's finished quests
  (WorkflowRun.cs:380).

Closing the quest at a graph's end therefore moves all of that onto a **submission**, in a closure graph only.

**One rule: in a graph that holds `quest.close`, it is the graph's end.** A graph that puts any node after it, or that
needs the quest's own Done before it, is refused when saved. So **Daoris never writes Done before the work's end**. The
asker sees *submitted*, then the current gate or hold (*waiting for your look*, *evidence missing*, *deploying*), then
*done*. The two exceptions are modelled, never hidden:

- **The person's override.** The person's own done (`/api/quests/{id}/done`, a `Person` door) stays theirs, at any time.
  - A quest they close is closed as theirs, and `quest.close` leaves it as it is.
  - When they have closed every quest the close node would close, the graph's end is reached as **overridden**, never as
    reached.
  - Its pending nodes are stopped (no new dispatch). An attempt already in flight is still reconciled and its receipts
    kept (§D3), and the cleanup follows the cycle, *held* still required.
  - The milestone says *closed by you before its end*, and names the nodes stopped.
- **A chain closed only in part**, which only D69 can cause (below).

**The submission.**

- **Where it applies.** In a closure graph, the agent's `quest_respond` takes a new action, `ready`, carrying exactly
  what a done carries: its note, an answer to each requirement, and the commit. It applies only to a quest that is
  taken, in a run bound to a closure graph, from the machine holding the take. Anywhere else it is refused.
- **Its operation.** The service records it as a `Submitted` operation. The quest stays taken: a submission is not a
  status.
- **Take ownership and loss** follow D69's rules for a take's own moves, by adding `Submitted` to the kinds a take owns
  (`QuestLog.TakersOwn`):
  - on a take that reads lost, it is refused inside the write (`OnALostTake`, QuestLog.cs:312; WAITCLAIM2);
  - a remote refuses it as a push arrives (WAITCLAIM3);
  - a rebase that finds the take lost loses it with the take, and names it on the take's conflict
    (`QuestStore.RebaseAsync`, Quests.cs:1636-1671; WAITCLAIM1, 4);
  - a submission on a numbered take reads held, and is never lost.
- **Replacement order** is the quest's operation order. The latest applied `Submitted` is current, and a later `ready`
  replaces an earlier one, judged again from the start. **A submission that has published its successor is sealed**
  (below): a later `ready` on it is refused while the successor stands, naming it.
- **The agent's `done` is refused there**, with words naming `ready`, so no done slips past the graph. The session's
  prompt says `ready` in such a repository.

**Submission-bound holds and evidence.** The judgements that hang on a Done today hang, in a closure graph, on the
current submission instead, unchanged otherwise:

- `EvidenceWanted`, `AwaitsEvidence` and `Hold` (departed, evidence unread or missing, unreviewed) are computed for a
  taken quest with a current submission;
- the evidence door takes a verdict for a submission that awaits evidence, read at the submission's commit;
- the person's yes to a departure (`/accept`) and the review's verdict apply to the submission;
- the landing and its gates wait on an **unheld submission**, where today they wait on an unheld Done, with every hold,
  opinion and look still applying.

**Chains, and the successor's binding.**

- **Publication.** A chain's next quest in a closure graph is published when the submission is unheld, in the same
  transaction, as an unheld Done publishes it today (Quests.cs:1120-1128). The successor records the submission that
  published it (`publishedBy: { quest, submission }`), and that submission is **sealed**.
- **The person's reopen.** Only the person may reopen a sealed submission, at a `Person` door. That changes its
  successor:
  - **not yet taken:** it is withdrawn, as a decline that applies while it is open (D132 point 10, `whileOpen`);
  - **taken or started:** it is held, with the reason *the work it grew from was reopened*, and its session is told at
    its next step. The person chooses to go on (the successor rebinds to the new submission once that is unheld, its
    branch brought up to date) or to abandon it.

  The withdrawal and the hold are operations, so they travel (D68). A teammate's machine that synced or took the
  successor sees them as this machine does.
- **Lost with its source.** A successor whose publishing submission is lost on a rebase (above) is lost with it: its
  publication was pending on the same take. Its session, if one started, is stopped as a lost take's session is today
  (Driver.cs:335-338). The winner's machine publishes its own successor from its own submission; the step's id is the
  ask's (D65 §4), so nothing is published twice.
- **D157's `work` node** counts a chain quest finished when its submission is unheld and sealed.
- **At the end**, `quest.close` closes every quest of the chain in this repository **in one transaction** (below): all
  of them or none.

**The node and its door: an explicit fence across two stores.** Closing reads facts held in two places, and no
distributed transaction is pretended:

| Store | Holds | Fenced by |
|---|---|---|
| the driver's records (the unit's entries, §C7) | the node acceptances and prerequisite receipts, the revision, the graph binding | the unit's record lock, taken by the lease's loop (§B5's one writer) and **held across the whole close** |
| the service's quest store (SQLite) | the claims, the submissions, the holds' inputs (answers, evidence verdicts, the departure's yes, review verdicts), the chain's quests | **one SQLite transaction** in the close door |

1. **The driver, under the unit's lock**, recomputes the close node's eligibility: every prerequisite acceptance still
   current (none invalidated), the revision current on a fresh `RefSnapshot`, and the graph binding unchanged. It
   computes a `closeToken` from the unit, revision, graph digest and the prerequisite acceptances' ids, and persists the
   invocation (key, token) before sending (§D6).
2. **The door** (`POST /api/quests/{id}/close`, a local `Driver` door, not mapped in shared mode, scoped to the graph,
   node, unit, take and submissions it names) runs **one SQLite transaction** over every quest of the chain. For each
   quest it checks, on the quest replayed inside the transaction:
   - the claim (`OnALostTake`, Quests.cs:1112);
   - that the named submission is the current one and sealed;
   - that `Hold` is null: no departure without its yes, the evidence found at the submission's commit, the review's
     verdict given;
   - that the key is unused.

   Only then does it append Done to every quest, with its submission's answers and the token as provenance. Any failed
   check writes nothing, and answers which check, on which quest.
3. **The driver, still under the lock**, records the close node's outcome with the token. No driver-side fact could
   change between steps 1 and 3, since the lock's holder is their only writer, and a service-side fact that changed
   (a review verdict, an evidence verdict, a claim) is caught inside step 2's transaction. A new commit arriving during
   the call does not undo the close: the quest closed on the submission it judged, and the newer commit invalidates the
   unit's acceptance as new work (§E2), never reopening the quest.
4. **A timeout or a lost answer** makes the node *unknown*. It is reconciled by sending the same key again, which
   answers the first outcome. A stale binding, a lost claim or a racing close leaves every quest untouched and answers
   why.

**Amended (FABRIC-CLOSE1q, D173's note).** A close speaks for the submissions it named, by their machine and sequence. The
same commit made ready again is another submission, which a close kept for the one before neither reaches nor keeps from
the person's override. A close the door answered whose node could not take the answer is held as the door's. The person
goes on by a close's own words: record its kept close again, ask for its answer again with the same key, or stop the
work's further nodes. Never a new close, and never their word for the door's answer.

**Amended (FABRIC-CLOSE1t, D173's note).** Step 1's eligibility is every named quest's. Before a chain's close is sent,
each other quest it names has its own graph's prerequisites to its close reached: on its own node record, under the graph it
is bound to, at its current revision. Until then the close waits, naming the quest and the node.

**Amended (FABRIC-CLOSE1u, D173's note).** Step 4 asks, and never sends the close again. A key's idempotency protects an outcome the door
already keeps, not a key it never took, so the same close sent again after a restart could run on evidence that moved since
it was recorded. The driver asks the door what became of the key (`close/outcome`), which answers from its own record and
runs nothing. A key it took is settled from that answer. A key it never took is withdrawn in the same transaction, so that
close never runs: its node is judged again, and a close is made again only through step 1's fence, under a new key. A close
whose door answers neither stays unknown, never sent again by itself, and the person may stop its work.

**Amended (FABRIC-CLOSE1c, D173's note).** The reopen's withdrawal is a hold on the open step, not a decline. A decline is final, so
the next submission's step would be a second quest or never published. Withdrawn, the step is taken by nobody, and the next unheld
submission rebinds the same step, with its id and its history. The step records the submission it grew from (`grewFrom`), which the
seal reads. A started step goes on only by the person's word, once its source is unheld again, or is abandoned by their decline. A
reopen is refused once the step closed on the submission.

**Amended (FABRIC-CLOSE1h, D173's note).** The landing waits on an unheld submission as it waits on an unheld done, and the gates read a
submission as its done. The automatic landing is judged again once it holds its tree, before anything is made: its commitment point. A
hold or reopen before it stops the landing; one after it applies to what follows, and a push made is never retracted.

**A chain closed only in part** can come only from D69. The close is atomic on this machine, but a remote judges a push
quest by quest (D68), and may refuse one quest's Done because its take was lost meanwhile.

- The others stay Done: they were this machine's to close.
- The refused one is lost with its take, named on the take's conflict.
- The graph reads **partially closed**, naming the quest and why.
- **Recovery** is the person's: the work on that quest is now the winner's, and the milestone offers to stop the node.
  It never retries over the winner's take.

The close's reach and failure authority are §E2's.

**Cutover.** A quest follows a closure graph only when the graph was bound at its first start. Quests already in flight
keep today's done. Removing `quest.close` from a graph changes nothing for runs already bound.

This amends D46 and D144's timing of the done, and D65's moment of a chain's advance, **only in graphs that hold
`quest.close`**. Everywhere else everything is as today.

## E6. `wait.unit`, waiting on other work as a node

The owner: *"waiting for another task can be a step too"*.

- **A waited node.** A `wait` wire names what it waits for: a unit's node or its end, at a revision.
- **Local targets** (this repository's units) are read through their canonical adapter.
- **Foreign targets** (another repository's work) need an **accepted quest commitment** naming
  `{ unit, revision, graphDigest, node | end, outputs }`.
  - The receiving repository publishes the receipts it permits through the commitment (the quest's operations, D68).
  - The waiter reads its own local projection, never the other repository's files (repository owns its work).
- **D79's `wait`**, a session parked on a quest it asked another repository, stays the session's verb. `wait.unit` is the
  same need at the graph's level, and a session's `wait` may resolve through one.
- **D69's unconfirmed and lost claims stay explicit.**
  - Unavailable evidence is *unknown*.
  - A revoked or superseded commitment invalidates the wait.
  - A newer revision is never followed silently.
  - A commitment dependency cycle that is known is refused, and partial visibility is shown honestly.
- **Distributed waits are not prevented in general.** Two repositories each waiting on the other, through work one of
  them cannot see, can wait for ever, and no universal deadlock prevention is promised. The running view shows a
  `wait.unit` whose commitment's own graph is not visible here as *may be waiting on work Daoris cannot see*, with how
  long it has waited (its `timer.step` deadline). The person's recovery is to stop the node, or to rewire it to other
  work, and nothing is reached on their say-so.
- **It is built after the delegated task** (FABRIC-DELEGATE1), since the commitment is that adapter's.

## E7. Graphs, and D157's workflows

**One model.** A graph is `{ schemaVersion, nodes, wires, entries, end, cleanup }`, bounded to 24 nodes (D157's
`WorkflowKindTable.MaxSteps`) and 96 wires. It is validated before saving:

- acyclic;
- every node reachable from an entry, and the end reachable;
- typed ports, and supported adapters;
- D157 §2.2's protected order before the landing (holds, opinion, look, landing).

A failure sink may end without reaching the end.

**Its homes are D157's split, never two models:**

- **Current's graph** is derived from the rules, with the landing rule's `cycle` (§C2) now a graph fragment
  (`{ nodes, wires, end, cleanup }`) after the landing.
- **A named graph** is a named workflow's version in `<home>/workflows/<id>.json` (D157 §2.4), chosen by workspace,
  repository, kind and task (D157 §4.1), whole-rule replacement under D164 kept.
- **One governs a run, by its binding.** Named versions stay immutable and bound at the first start, and Current stays
  live.

**D157's versions decoded, never rewritten: the exact contract.**

| D157 (WorkflowNamed.cs) | Graph |
|---|---|
| step order | `after` wires |
| D157 §2.2's protected order before the landing: the holds (the work's claim carries them), the opinion, the look, the landing | an `after` wire from each into every one after it, beside the step order. A skip satisfies `after` (§E2), so reachability alone would let a skip pass a gate still unsettled (D173's FABRIC-GRAPH1h note) |
| a step's inputs | `data` wires |
| an `all` group, its members and its own `failed` policy (WorkflowNamed.cs:92) | an **`all` join node** with the group's id: reached when every member is reached or skipped, failed under the group's policy when a member fails. Members are wired into it, and it into the next step, so the group's identity and policy survive as a node |
| `failed: stop` | a `failed` wire to a synthetic sink |
| `failed: wait` | a `failed` wire to a synthetic person's task |
| `failed: send-back` | the adapter's bounded return (D157 §3.7), never a backward wire |
| a person's skip on a run | the node's `skipped` state (§E2), which satisfies `after`, as *settled* does today (WorkflowRun.cs:39) |

- **The identity is the original digest.** `WorkflowNamed.Digest` stays the version's identity. Decoding is a
  deterministic, pure function of the stored version and the decoder's own version, and a decoded graph's `graphDigest`
  is derived from both.
- **Synthetic nodes are counted apart.** Sinks, tasks and joins made by decoding are marked `synthetic`. They do not
  count toward the 24-node authoring bound, which a decoded version keeps in its authored steps (members counted, as
  WorkflowNamed.cs:239-241 counts them). They are bounded at three per authored step. So a maximal old version always
  decodes.
- **An unsupported stored version** (a kind this build does not run, or a version that does not read: `NamedVersion`'s
  `Problem`) is kept as it is and read as *unreadable*, with its reason. It is never decoded in part, and a run bound to
  it says it cannot start (D157 §5.2).
- **The facts stay where they are.** Run files keep their bindings and run-only decisions (D157 §5.1), and unit records
  keep §D6's attempts. Anything that overlaps moves once, with one writer, and no engine runs beside the old one.
- **Tested at the boundaries**, not only on representative fixtures:
  - a maximal 24-step version with groups and every `failed` policy;
  - each kind;
  - a skip on each kind;
  - a group failing under each policy;
  - an unreadable version;
  - decoding twice, and in two processes, giving the same graph and digest.

An example graph fragment for a branch's cycle, closing the quest once a deploy and a dependency are done:

```json
{ "schemaVersion": 2, "entries": ["line", "dependency"], "end": "close", "cleanup": { "until": "end" },
  "nodes": [
    { "id": "line", "kind": "line", "version": 1, "reach": "observed" },
    { "id": "qa", "kind": "human", "version": 1, "reach": "pressed",
      "options": { "brief": "Verify the release", "attachmentSchema": "qa-proof@1" } },
    { "id": "dependency", "kind": "wait.unit", "version": 1, "reach": "waited" },
    { "id": "deploy", "kind": "acme.deploy/deploy", "version": 1, "reach": "performed" },
    { "id": "close", "kind": "quest.close", "version": 1, "reach": "performed" } ],
  "wires": [
    { "id": "a", "kind": "after", "from": "line", "to": "qa" },
    { "id": "b", "kind": "wait", "from": { "commitment": "q2/take-7", "unit": "u2", "revision": "r2", "end": true },
      "to": "dependency" },
    { "id": "c", "kind": "after", "from": "dependency", "to": "deploy" },
    { "id": "d", "kind": "data", "from": "qa.evidence", "to": "deploy.approval" },
    { "id": "e", "kind": "data", "from": "deploy.evidence", "to": "close.delivery" } ] }
```

## E8. Chains, configuration and the views

- **Chains are not wires.** Chains route wakes to judges and recover pending effects (§C6). CHAIN3's responses stay
  subscriptions with §D8's four effects, none of which writes. A `trigger` wire starts a node inside a graph.
- **One editor.** Setup's *Work* pages become the graph editor: laid out automatically, a drawer per node, typed wire
  selectors. D157's planned editor (WORKFLOW1g) is the same editor; there is never a second one.
- **The terminal:** `daoris-driver workflow` manages named graphs, and `work` inspects units and acts on tasks.
- **The running view** shows each node's state, each wire's condition, the revision, the evidence references, any
  authority missing, and any commitment waited on. It keeps §D0's milestone: the end, the result, the cleanup and the
  recovery.
- **Words:** chrome in English and Chinese, through the glossary's new names for *node* and *wire* (§Q2); authored
  content stays as written.
- **With nothing configured,** today exactly: the live rules, the existing gates, the end at `line`, the cleanup at
  *held*, no added task and no automatic closure.

## E9. Rejected, in part E

| Shape | Why not |
|---|---|
| **An expression engine, or conditions on wires** | D65's no engine and D157 §2.1's no expressions; wires are typed |
| **A central scheduler** | ADR-A; graphs stay optional routines over units |
| **A wire into another repository's node** | Repository owns its work; a cross-repository dependency is a local `wait.unit` through a commitment |
| **A second store or engine beside D157's workflows** | One model, decoded from the old versions, never run beside them |
| **A plugin closing a quest, or a person's press impersonated** | `quest.close` is Daoris's executor at a scoped `Driver` door; a person's doors stay D156's |
| **Unknown counted as failed** | Silence never counts (§E2) |
| **Authority for production granted by a graph** | D157 §3.6; a node grants no authority (§D7) |
| **Deletion driven by a graph** | §D3's inventory and the fresh proofs of removal govern cleanup; no node removes anything |
| **`quest.close` everywhere by default** | The owner: a step *to set*. Absent configuration, closure stays the agent's or the person's done |
| **Reusing the row name QUESTCLOSE1** | It is already the person's own done (Program.cs:679); the node's rows are FABRIC-CLOSE1a and 1b |
| **`ready` with holds still Done-only** | The landing would either pass unjudged work or wait for evidence that can only be read at a Done that comes after it; holds and evidence move to the submission (§E5) |
| **A `quest.close` that is not the graph's end** | The quest would read *done* while its work still moves; in a closure graph it is the end |
| **Reinterpreting the agent's `done` as `ready` silently** | The record would say what did not happen; `done` is refused there, naming `ready` |
| **Promising no distributed deadlock** | Partial visibility makes it impossible; the view shows a possible one and the person recovers (§E6) |

## E10. The rows, re-cut on part E

This replaces §D12's lifecycle table for every row not yet built. The parallel gate and the first rows stand.

**The parallel gate:**

| Gate | Row | Lanes | After |
|---|---|---|---|
| G1 | FABRIC-ACP1 → FABRIC-C2a → FABRIC-C2b | parent; service | as their rows |
| G2 | FABRIC-COLLAB1 | service, driver | G1 |
| G3 | FABRIC-DELEGATE1 | service, driver | G2, FABRIC-NODE1 |
| G4 | FABRIC-WAITUNIT1 | service, driver | G3, FABRIC-WIRE1b (not the archived WIRE1, a terminal door) |

**Built:** EVENT2, END2a, EVENT2b-2d, CHAIN2, PROOF-UNREAD1.

| Order | Row | Lanes | After | Fate |
|---|---|---|---|---|
| 1 | FABRIC-END2 | driver | the cross-process writer chosen first | as §D12 |
| 2 | FABRIC-END3 | driver | 1 | as §D12 |
| 3 | FABRIC-MILESTONE1a | driver | 1 | as §D12 |
| 4 | FABRIC-MILESTONE1b | web-shell, modules | 3 | as §D12 |
| 5 | FABRIC-END4a | driver | 2, 3 | as §D12 |
| 6-10 | FABRIC-EVENT3a, EVENT3b, EVENT4, END7, END6 | as §D12 | as §D12 | as §D12 |
| 11 | FABRIC-UNIT1a | driver | 1 | as §D12 |
| 12 | FABRIC-NODE1 | driver | 11 | **renames** UNIT1b: the node contract and its state table (§E2) |
| 13a | FABRIC-WIRE1a | driver | 12 | **new**: the wires' validation when saved: kinds, groups, data inputs, and the refusals (§E3) |
| 13b | FABRIC-WIRE1b | driver | 13a | **new**: runtime receipt evaluation: eligibility, `anyOf` witnesses, a `trigger` once per receipt, forks within the limits, `all` joins (§E3, §E7) |
| 14 | FABRIC-GRAPH1 | driver, cli | 13 | **renames** UNIT1c: the graph schema and its homes, both twins (§E7) |
| 15 | FABRIC-WORKFLOW2 | driver, cli | 14 | **new** (Codex's WORKFLOW-MIGRATE1): D157's versions decoded as graphs, equivalence held |
| 16 | FABRIC-UNIT1d | driver, cli | 12 | as §D12: the manifest twins, now node declarations |
| 17-21 | FABRIC-PERFORM1a-e | as §D12 | as §D12, with NODE1 and WIRE1 for UNIT1b | as §D12 |
| 22 | FABRIC-END4c | driver | 5, 14, PERFORM1d | as §D12 |
| 23 | FABRIC-HUMAN1 | driver, modules | 12, 14; RPC1d and D156 §4.2's confirmation | as §D12, a pressed node |
| 24a | FABRIC-CLOSE1a | service | 14, 15 (the cutover: graphs bound at first start) | **new** (Codex's QUESTCLOSE1, renamed and split): `ready` and `Submitted` as a take's own kind (applicability, replacement order, D69's loss), `done` refused in a closure graph, submission-bound evidence, holds, departure and review, the chain's advance with the successor bound and the submission sealed, the person's reopen, the close door's one transaction over the whole chain (§E5) |
| 24b | FABRIC-CLOSE1b | driver | 13b, 24a, PERFORM1b | **new**, split: `quest.close`'s execution under the unit's lock (the fence's steps 1, 3 and 4), landing on an unheld submission, the person's override, *partially closed* and its recovery, the rule that it is the end (§E5) |
| 25 | FABRIC-GRAPHEDIT1 | web-shell, modules | 14, 15, 23 | **merges** CYCLE1b and D157's editor (WORKFLOW1g): the graph editor |
| 26 | FABRIC-GRAPHVIEW1 | driver, web-shell | 4, 15, 22, 23 | **merges** the configured END4b: the running view, *may be waiting on work Daoris cannot see* included |
| 27 | FABRIC-CHAIN3 | driver, cli, web-shell | CHAIN2, 25 | as §D12 |
| 28 | FABRIC-END5 | driver, modules | 26 | as §D12 |
| 29 | FABRIC-EVENT5a, 5b, EVENT6, C1b | as their rows | as their rows | as §D12 |
| 30 | FABRIC-EVENT7 | driver, cli | 2, 7, 8, 9 | as §D12 |

**D157's open rows affected** (the steward's to re-cut):

- WORKFLOW1g becomes GRAPHEDIT1.
- WORKFLOW1j-1l become node kinds. Each of their proofs is kept, and allocated:

  | D157's promise | Its row | Its proof |
  |---|---|---|
  | an awaited pull request asked with D157 §5.4's back-off (WORKFLOW1j) | PERFORM1c, as an observed plugin node's asking while a run waits | WORKFLOW1j's wait, occasion and plugin tests |
  | checks bound to a commit, unknown and stale told apart, failure policy, all-member joins (WORKFLOW1k) | PERFORM1c (checks as observed nodes) and WIRE1b (the `all` join) | WORKFLOW1k's fake-tool tests |
  | stages started by a plugin (WORKFLOW1l) | PERFORM1b, as performed nodes | WORKFLOW1l's declared-stage tests |
  | production only by the person's press, and a check's outcome read before a retry (WORKFLOW1l, D157 §3.6) | HUMAN1 (a pressed node always before a production stage) and PERFORM1b (reconcile before retry) | a production stage refused without the press; a retry refused before its check is read |

- WORKFLOW1m and 1n stand, over graphs.

**Canaries for part E:**

38. **Migration equivalence:** every stored named version decodes to a graph whose evaluation equals the old run's on
    recorded fixtures, with its digest unchanged.
39. **Lost claims:** `quest.close` and `wait.unit` on a lost or unconfirmed take are refused or unknown, never Done or
    reached.
40. **Closure deadlocks:** a graph needing its own Done before `quest.close` is refused when saved, and a landing in a
    closure graph runs on `ready`.
41. **Witness invalidation:** an `anyOf` witness lost invalidates its consumers, and no other witness is taken silently.
42. **Restart de-duplication:** a `quest.close` invocation and a `wait.unit` receipt replayed after a crash write the quest
    once and record the receipt once.
43. **One state table:** every node kind passes the same transition tests.
44. **No silent revision:** a `wait.unit` never follows a newer revision of the unit it waits on.
45. **A stale binding at the close door:** a close naming a submission or revision no longer current writes nothing, in
    the same transaction that would have written Done.
46. **A claim race at the close door:** a close and a lost claim arriving together leave exactly one outcome, the refusal
    when the claim was lost first, and never a Done on a lost take.
47. **Holds before the landing:** in a closure graph, a submission with a departure, unread or missing evidence, or an
    unreviewed set-up holds the landing exactly as a Done does today, and the agent's `done` is refused.
48. **A chain under `ready`:** a two-step chain advances on the first step's unheld submission, lands from the last, and
    `quest.close` closes both quests in order at the end.
49. **Skips and groups decoded:** a skipped step satisfies `after` and no `data`, and a group fails under its own policy
    as D157's did.
50. **A maximal and an unreadable version:** a 24-step version with groups and every `failed` policy decodes within its
    synthetic bound; an unreadable one stays unreadable and kept; decoding is identical across runs and processes.
51. **The person's override:** the person's done on every quest the close would close ends the graph as *overridden*.
    Pending nodes stop, an in-flight attempt is still reconciled, and the milestone says who closed it.
52. **An atomic chain close:** a failed check on any one quest of a chain writes no Done on any of them.
53. **Partially closed:** a remote refusing one quest's Done for a lost take leaves the others Done, the graph *partially
    closed* with the reason, and no retry over the winner's take.
54. **`Submitted` on a lost take:** it is refused inside the write, lost on rebase and named on the take's conflict. Its
    successor's publication is lost with it, and a session started on the successor is stopped.
55. **A sealed submission:** a later `ready` is refused while its successor stands. The person's reopen withdraws an
    untaken successor, and holds a started one with its reason.
56. **The fence:** a review verdict or an evidence verdict changed after the driver's step 1 refuses the close in step
    2's transaction. A prerequisite cannot be invalidated between steps 1 and 3, because the lock is held. A timeout
    reconciles by the same key, writing Done once.

## E11. Questions for the owner

1. **In a graph that closes the quest itself, what does the asker see?** By §E5's rule, `quest.close` is the graph's end,
   so the quest is Done only when the work's end is reached. *Recommendation:* *submitted* once the agent's result is in,
   then the current gate or hold (*waiting for your look*, *evidence missing*, *deploying*), then *done*.
2. **The names on the page.** *Recommendation:* *node* and *wire* in English, and 节点 and 连线 in Chinese, added to the
   glossary; *step* stays only in D157's older words until the editor replaces them.

---

# Rejected shapes

| Shape | Why not | Against an earlier rejection |
|---|---|---|
| **Trusting a watcher alone** | Windows drops events on overflow and while a handle is reopened; a missed end would never be seen | The owner's direction says the same (D173's events note point 2) |
| **A hint running the full look** | The look syncs with the remote before it plans and reads the whole snapshot (Driver.cs:332, 378); more wakes would mean more remote calls | - |
| **A database-wide change hint** | Every look records its sync pass (QuestSync.cs:91; Quests.cs:1954-1957), so the driver would wake itself for ever; families by their own revisions | - |
| **A watcher per tree** | A tree's `HEAD` and refs live in the common dir (measured, §A5); per-tree watchers multiply handles and hear a build's churn in the working files, which no end needs | - |
| **Watching the service's database file** | WAL writes, an internal format, and another process's store read behind its owner's back; the database's own `data_version`, asked through SQLite, is the API | The scout's *never watch the SQLite file* stands |
| **Event handlers judging apart from the look** | Two judges of one concept is the second writer D173's guard forbids; a handler only wakes | - |
| **Moving the person's removals to the end** | The sweep's press and LAND3's tidy take handed work that is not ended, on the person's authority (TreeSweeps.cs:342-354; TreeLands.cs:527-537); reading the end would stop them, or make the end lie | - |
| **A new `ends.json`** | A second record keyed on what `landings.json` and `session-branches.json` already key | - |
| **An `end` key in `driver.json`** | Every cycle in §B3 is resolved from the effective landing rule and the plugin; a key only where a cycle cannot be | - |
| **The end at the quest's done or a conversation's end** | A claim, not evidence (Evidence-First Completion) | - |
| **A webhook only** | The desktop is not reachable, local operation needs no remote, and with no reconciliation a missed delivery never ends | A platform's hook is a plugin's way to learn a step (§C4), never Daoris's |
| **A timed fetch** | A network call nobody waits on | **Stands** (D147:62) for Daoris's own asks: the owner's direction itself says *not on a clock*; a plugin's own cadence is the plugin's (§C4) |
| **A timed plugin query** | Same reason | **Stands** (D148:84-85). The fetch-when-due moments of §A4.3 add occasions to D148 point 2's list, which amends D148, by the owner's yes |
| **No watcher yet** (the scout's 6.3: nudge on Daoris's own moves and measure first) | The probe measured it: every ref write heard on this machine under its antivirus, with no process start | **Overridden by the owner's direction**, on the measurement; the scout's safety is kept as the reconciliation, and its caution as the recovery and bounds of §A4.1 |
| **Git hooks** (`reference-transaction`, `post-commit`) | Daoris writes nothing into the person's repository configuration (SessionBranches.cs:28-30), and the person's and agents' git would run Daoris's code | - |
| **`fsmonitor` or an outside file watcher service** | A dependency that watches working trees, not refs | - |
| **SSE or a WebSocket for the change stream** | A new transport for one hint; a long-poll uses `ServiceClient` and the host as they are | - |
| **An in-memory change signal in the service host** | Each connector writes the store from its own process (Service.Mcp/Program.cs:100), so the host would not hear an agent's quest | - |
| **A watcher on a task's output file** (row 17) | Its offset read sees every byte and is its own reconciliation | **Stands**: OutputTail.cs:16-18 |
| **Raising `pollSeconds` or the tidy's pace now** | Before the wakes land and their recovery is measured, a raise only makes things later; `pollSeconds`' CLI twin and the rehearsals read it | - |
| **A `cycles` key of its own** (part C; weighed, not chosen) | Workable, with a validation reconciling it with the landing rule; not chosen because D164's whole-rule inheritance would then split a repository's landing from its end, and two Setup sections and two twins would carry one decision (§C2) | - |
| **An end only in a named workflow** | The editor is not built (WORKFLOW1g), and the owner asked for each repository's and workspace's end now, so Current must carry it; a named workflow's `end` still governs where one is chosen | - |
| **`claimed` as an end** | The owner's own condition: a step is reached only on git's or a plugin's evidence; and the model is not the authority on its own success | - |
| **Storing *ended*** | A verdict that goes stale the moment the definition changes; derived from the kept facts instead | Part B's `Ended` field, superseded by `Held` |
| **Chains as rules the person writes** (an event, a condition, an action, freely) | D65's no engine, and D157 §2.1's no expressions | A bounded, typed menu of events and effects, with no expressions, is not rejected: the owner chose it (FABRIC-CHAIN3) |
| **A tidy that can be switched off** | D88 judged that no setting turns the look's tidy off: what goes is what the press would clear, kept fourteen days. A cycle configures *when* it goes (§C3's `wait`), never *whether* | **Stands** (AUTOTIDY2a) |
| **A human step an agent may press** (through a tool, a terminal it runs, or a field naming the person) | The step is the person's word; an agent's press is the self-declared success Evidence-First Completion refuses; D156's doors decide who the person is | - |
| **A human step a plugin may report** | A plugin speaks for a platform, never for the person (D100) | - |
| **An end that takes the result** | The result is the work; taking it is losing it | - |
| **Cleaning up at an end before the work is held anywhere** | The line's rule: nothing goes before the work is held where it cannot be lost, on the line or in the kept result | D88 |
| **Purging the moved-aside folder at the end, not at its fourteen days** | The default must stay today's, and the keep is the only recovery of a tree's ignored scratch, which no end proves unneeded | - |
| **One wait policy for every workspace** | The owner's answer: the wait is the cycle's | - |
| **A plugin event that removes, writes the ledger, or speaks for a repository no rule names it on** | D51 rule 7, D46, D100 | - |
| **A plugin calling Daoris with requests Daoris answers** | A notification on a declared point is enough for an event | **Stands** (D64 §7) |
| **Daoris opening a port for a platform's webhook** | The desktop serves no outside caller; a plugin's own listener is its process's business, and its event comes over stdio like any other | - |
| **No timers at all** | A source with nothing behind it is the watcher-alone shape; the owner's *build them into the event system* is met by timers as events | - |
| **Keeping the due sets on disk** | A second record of what the facts and the reconciliation already say | - |

---

# Canaries and proof, and the rows

**Canaries**, each held by a fast test on the fake source unless named:

1. A missed event costs latency, never a removal: with the source silenced, the reconciliation ends and tidies by the
   topic's deadline.
2. An overflow (`Error`), a lost handle, a reopen or a resume judges that root at once; a callback from an old generation
   is dropped.
3. A burst of hints for one subject is one judgement; a hint arriving during a judgement is judged after it; a subject is
   never judged twice at once.
4. More wakes are not more full looks: a thousand hints run no look, and no remote sync.
5. A look that changes nothing wakes nothing: the driver's own sync pass moves no family (§A4.2).
6. No step without its evidence: git's for `handed`, `pushed`'s ref and `line`; the plugin's believed answer or event for
   `pull-request` and its own steps. Each case of §B7 is not ended, and a failed read leaves the subject unreadable,
   never ended.
7. A fact stops holding when its tip, its line's commit, its cycle's line or its repository changes; a trace never does.
8. The removal judges again: an end contradicted before the act keeps the branch; the person's removals are unchanged.
9. One writer: a structure test finds no writer of `Held` or `Steps` outside the end judgement, and none of
   `PullRequestState` outside its existing writer; a verb beside an instance writes none.
10. Measured on the owner's use: the machine log's look timing, its git starts per hour, the delay from a ref move to its
    end, and the watchers' handles and nonpaged memory, before and after (Process classes and a day's log, not a load).
11. A chain that changes nothing wakes nothing: a judgement that finds what it found emits no event, and the tidy's own
    ref moves are judged once.
12. No configuration makes a loop: every value a `cycle` may take is tried against the link table.
13. A plugin event that is not named, not declared, stale or misshapen is no event, and no event removes anything.
14. The default cycle is today's: with no `cycle` set, the Trees suites and the tidy's tests are unchanged.
15. Nothing goes before *held*: on the line, or for an end before the line in its result; pending, unreadable and
    unreachable are said apart, and
    each takes §C3's row.
16. A restart between a fact and its effect: the fact recorded, the process killed before the tidy, and the start's
    reconciliation performs the pending tidy once.
17. Busy, then free: the tidy refused for a busy tree stays pending, and runs when the session leaves the tree, with no
    fact having changed.
18. A deadline expiring: with every source silent, each topic is judged whole at its deadline, and a deadline counted
    from `Held.Since` survives a restart unchanged.
19. A dropped plugin event replayed: an event dropped past a bound is sent again after the next `resync` frame, and is
    believed then.
20. A definition changed: a cycle's `end` changed in `driver.json` makes every unit's *ended* read anew at once, with no
    fact rewritten.
21. A human step is reached only by the person at a person's door: an agent's tool or unconfirmed terminal, and a
    plugin's event naming it, are refused, and a press before the steps it follows is refused naming the one it waits on.
22. A human step lapses with its tip: a newer commit shows the press as about the older tip and offers it again.
23. A true end takes everything the work made except its result: the result, the person's branches, recovery refs and
    anything not Daoris's stay, and every guard still keeps a tree.
24. An end before the line cleans up only once its result holds the work by git's proof; the default cycle cleans up
    exactly as today.
25. The wait is the cycle's: `held` goes at *held*; `end` with `days` goes at the bound counted from `Held.Since`, across
    a restart; `end` alone waits for the end or the person's *Clean up now*, and an end reached out of order does not end
    the work.

*Superseded by §D12 for every row not yet built: the table below is part C's order, kept as the record of it.*

**The rows**, in order, re-cut on part C. Each is sized near a hundred turns, and each later row names what it waits on.
The first two were dispatched as part B proposed them; FABRIC-END2a takes the cycle as an input, so part C's defined
cycles plug into it.

| # | Row | Lanes | After | Builds | Proves |
|---|---|---|---|---|---|
| 1 | FABRIC-EVENT2 | driver | - | `ChangeWatch` with its topic queue and recovery (§A3, §A4.1), the topic-carrying `Nudge`, the request and presses folders on it with a 3 s listing behind, and a machine-log line timing the watch's look | canaries 1-3 on a fake source; a Process class with a real watcher on a scratch folder; a request answered within the debounce, and inside the caller's 10 s with the source silenced |
| 2 | FABRIC-END2a | driver | - | the end judgement extracted: §B3's cycles and proofs as one pure judgement over a snapshot, with no store and no reader changed | canary 6; each cycle of §B3 with the named workflow's landing; the Trees suites unchanged |
| 3 | FABRIC-CYCLE1a | driver, cli | 2 | the `cycle` on the landing rule (§C2): `end`, ordered `steps` with human steps, and `wait`; both twins, D164's inheritance, a named workflow's `end`, the order rule for *ended*, the refusals naming the Setup row | canaries 14 and 25's reading; one table of rows read the same by both twins; each refusal; a repository rule replacing its workspace's cycle whole |
| 4 | FABRIC-END2b | driver | 2, 3; the cross-process writer: RPC1's pipe covering the verbs that write these two records (RPC1d covers the sessions verbs; HOMELEASE1 leaves a verb beside an instance holding nothing, HomeLease.cs:192-197), or a cross-process lock on the two files, chosen first | the facts kept (§C7): `Held` (with `Since`) and `Steps` on both entries, *ended* derived from the effective cycle, traces on both forget paths, written only by the lease's loop | canaries 9 and 20; an old entry reads as not yet judged; a trace survives the branch on every removal path |
| 5 | FABRIC-END2c | driver | 4 | when a fact stops holding (§B5), on standing units only: the tip, the line's commit, the cycle's line, the repository | canary 7, with real git in a Process class (a line rewound, a repository replaced at its path, a trace untouched) |
| 6 | FABRIC-CHAIN2 | driver | 1 | the chains (§C6): fact links on a change, effect links pending until done with their unblocking wakes, and each topic's deadline as a `timer` event (§C5); `chain.*` lines | canaries 4, 11, 12, 16, 17 and 18; a link to a lower rank fails a test |
| 7 | FABRIC-END3 | driver | 1, 5, 6 | the ref watcher on link 1 (§B4) | canaries 1-4 on git; a Process class with real git (a commit, a merge onto the line, a pack-refs, a tree's switch, each judged once within a second); canary 10's first day |
| 8 | FABRIC-END4a | driver | 7, CYCLE1a | the end's cleanup as link 2's pending effect (§C3): *held* on the line or in the result, everything but the result at a true end, the cycle's `wait` with its bound from `Held.Since` | canaries 8, 15, 16, 17, 23, 24 and 25; the tidy's git starts counted down; `BranchTidyingProcessTests`, `SweepTests` and LAND3's tests green |
| 9 | FABRIC-CYCLE1b | web-shell, modules | 3 | the Setup row *Work ends* in both locales, and Ask Daoris's room saying a repository's cycle | the Setup rows' tests and the locale checks; the help room's goldens; HelpCoverage's row for any new press |
| 10 | FABRIC-END4b | driver, web-shell | 4, 9, 10a | the page draws the cycle: the steps reached in order, human steps with their press, the end and the result marked, pending, unreadable and unreachable said apart, *held* said, the wait and *Clean up now* (§B6, §C2, §C3) | both locales; the window shows a merged pull request's session as ended with no press, and a human step pressed and then lapsed by a new commit |
| 10a | FABRIC-HUMAN1 (built before row 10) | driver, modules | 3, 4; RPC1d for the terminal twin | the human step's press and *Clean up now* (§C2, §C3): the person's route (`Publishes`), the record on `Steps`, the terminal twin asking and confirmed in the window over RPC1's pipe (D156 §4.2) or keyed with no window (§4.3), What needs you's item | canaries 21 and 22; an agent's connector and an unconfirmed terminal refused; HelpCoverage's row for the new press |
| 11 | FABRIC-PLUGSTEP1a | driver, cli | 3 | a plugin's own `steps` in its manifest, both twins (`plugins.ts`, `PluginManifest`), `twins.md`'s row; the cycle's plugin steps validated against them | the manifest twins' tables; each manifest refusal |
| 12 | FABRIC-PLUGSTEP1b | driver | 11 | the handshake for `work/watch`, `work/step` and `work/ended` in `HookPoints`, and the reverse transport: the reader accepting `work/step` on a watch process only (Hooks.cs:349-351), with its byte, rate and queue bounds | a notification on any other process or method still ignored; each bound; `protocolVersion` 1 unchanged |
| 13 | FABRIC-PLUGSTEP1c | driver | 12 | the kit: `plugin-kit`'s template sends and tests the frame, its bounds and its refusals (`plugin.test.mjs.template`:183) | the kit's own tests; a kit-made plugin's frame read by the host's reader |
| 14 | FABRIC-PLUGSTEP1d | driver | 6, 12 | supervision: the lease's loop owns the process, its start and `shutdown`, restarts on an unasked exit only, the generation and `resync` | canary 19; an intentional shutdown never counted; three unasked exits mark it unsound |
| 15 | FABRIC-PLUGSTEP1e | driver | 4, 14 | authorization: §C4's five rules, the unit looked up in the last frame sent, a built-in `pull-request` event written to `PullRequestState` by its existing writer, the refusal codes | canary 13; each rule refusing a fake plugin's event; no event removes anything |
| 16 | FABRIC-PLUGSTEP1f | examples, tools | 13, 15 | an example plugin that contributes a step from its own watch, with its README and `plugins try` samples | the example's tests; the rehearsal check written for the parent to run |
| 17 | FABRIC-EVENT3a | driver | 1, 6 | the look's topics: the remote sync on its own clock, and a `ledger` wake that runs the local plan only | canary 4; the planner's tests unchanged; a sync pass still at its own pace |
| 18 | FABRIC-EVENT3b | service, driver | 17 | the change stream (§A4.2), `sessions` first | canary 5; a second connection's write wakes the wait; the cursor's bootstrap, stale, malformed and future cases; an agent's quest starts without the poll |
| 19 | FABRIC-END5 | driver, modules | 10 | the CLI, a quest's page and Ask Daoris read the cycle and its end | the CLI's and the rehearsal's sentences; the help room's goldens |
| 20 | FABRIC-END6 | driver | 7 | a fetch and a plugin's ask when due (§A4.3, link 6), with no second fetch where the occasion just fetched | each moment and its bound; never at a removing press; an end with no press |
| 21 | FABRIC-EVENT4 | driver, modules | 1, 6 | the home's record files on the watch (§A4.1, link 7) | each heard within the debounce, and found by its deadline when silenced |
| 22 | FABRIC-END7 | driver | 7, 18, 21 | auto-landing woken by refs, the ledger, the config and the gate's declarations, together | every release of AutoLander.cs:153-177 lands without the look |
| 23 | FABRIC-EVENT5a | web-shell, modules | 21 | Ask Daoris's two polls (rows 23, 24) become publishes | no interval in a desktop window; a plain browser keeps its polls (D169 §4) |
| 24 | FABRIC-EVENT5b | web-settings, modules | 21 | Settings' wiring poll (row 22) becomes a publish | as row 23 |
| 25 | FABRIC-EVENT6 | service | 1 | the knowledge server marks a checkout stale on a watch (row 20); the service references no driver library, so its watch is a twin in `twins.md` or a shared home the row chooses | a document's edit read at the next search, with no minute's re-read |
| 26 | FABRIC-EVENT7 | driver, cli | 7, 18, 21, 22 | the clocks as deadlines (§C5): `pollSeconds` the `look` topic's, with its CLI twin, the tidy's pace the `refs` topic's, each default raised only after its measurement | canary 10 shows the wakes carry the work, and their recovery held, before and after |
| 27 | FABRIC-CHAIN3 | driver, cli | 6 (the owner chose the menu) | the chain's bounded menu: typed events and non-destructive effects a workspace selects, with no expressions, in both twins | canary 12 over every menu value; no chosen effect writes a ref or a ledger family a link listens to |

# Questions only the owner can answer

**Answered** in D173's notes:

- **At acceptance:** a push may be a defined end, on its evidence (§C2, §C3); what only a server knows comes from a
  plugin, learned its own way (§C4); and the clocks are events, with chains (§C5, §C6). The two clock raises stay behind
  their measurement (FABRIC-EVENT7).
- **Part C's answers:**
  - the wait for an end after the line is the cycle's to configure, a true end cleans up everything except the result,
    and a cycle may hold human steps (§C2, §C3);
  - a workspace may configure its chains from a bounded menu (FABRIC-CHAIN3);
  - a plugin is asked at the new due moments, within D148's minute, with no redundant fetch, and never at a removing
    press (§A4.3).

Still open: §D13's two questions. The first replaces the earlier question on the end and the quest.

# What this design does not decide

- **The end crossing machines** (a teammate's view of an end): machine-local in the first pass (D63).
- **The look's further split into topic clocks** beyond FABRIC-EVENT3a's: each topic its own row, after the wakes land.
- **The harness frames for usage** (§A4.4), **lock releases** (row 11) and **the knowledge server's freshness**
  (row 20): later rows.
