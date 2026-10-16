# Events over polling, and the end of a piece of work (FABRIC-EVENT1, FABRIC-END1)

**Parts A and B accepted by the owner, 2026-10-16, with three amendments in their words (D173's acceptance note); part C,
which answers them, proposed, pending the owner's yes and a second maker's opinion.** The contract is D173's notes of
2026-10-16, *the end of a piece of work*, *events over polling* and the acceptance. Parts A and B design both directions
together because they share one shape (Part A), and the end of a piece of work is that shape's first consumer (Part B).
Part C makes the end defined per workspace and repository, lets a plugin contribute a step or an end, and turns the
clocks into events with chains; §C1 says what it changes in A and B. It builds nothing; the rows at the end do.

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
- at the window's focus while a handed unit waits on its pull request, bounded by D148's one minute (§Q3).

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
| No landing (no rule; the person merges by hand) | never by Daoris | when the line holds it | the look's `landed` kinds, as AUTOTIDY2a | the session branch and its tree |
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
- landed only on another branch of the person's (D88:150-153).

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
- **§B8:** the automatic tidy takes a unit at the later of *held* and *ended* (§C3). Everything else stands.

## C2. The cycle: its steps, its end, its definition

A cycle names the steps a repository's work passes and which of them is its end. The steps are a fixed set, plus what a
plugin declares:

| Step | Reached on | Its evidence, and where it is kept | May be the end | Lets a removal go |
|---|---|---|---|---|
| `claimed` | the agent's done | the ledger (Sessions.cs:10-41, Quests.cs:6-19) | **no**: a claim, and the owner's own condition is that a step is reached only on git's or a plugin's evidence | no |
| `handed` | Daoris's landing: the landing branch made at its recorded tip; for the merge form, the merge on the line | the landing entry (`LandedAt`, `Tip`, `AcceptedBy`); for a merge, `Held` | yes | no (a merge is *held* at once) |
| `pushed` | the plugin's `work/land` answer that it pushed, at its pushed tip, **and** git's remote-tracking ref holding that tip (a push moves `refs/remotes/<remote>/<branch>`, which the ref watcher hears) | `Pushed` and `PushedTip` on the landing entry; the ref read again at each judgement | yes | no |
| `pull-request` | the plugin's `completed` answer with the full merge and source commits and the line as its target (D148 point 3) | `PullRequestState` on the landing entry (D148 point 6) | yes | no, until git holds the merge commit on the line, which is `line` |
| `line` | git's proof (§B3) | `Held` on the entry (§C7) | yes, **the default** | **yes**: this is *held* |
| `<plugin>/<step>` | the plugin's event and its evidence (§C4), bound to a commit | `Steps` on the entry | yes | no |

Every step after `handed` is about a commit: a newer commit on the work makes it stale for that work (D157 §5.3).

**The definition: a `cycle` on the landing rule.** Its home is `landings` and `workspaceLandings` (Landing.cs:47; read and
written at DriverConfig.cs:842-881):

```json
"landings": { "orders": { "form": "branch", "plugin": "azure-devops-pull-request",
                          "cycle": { "end": "pull-request" } } }
```

- `end` is one step's id.
- `steps` lists the plugin steps the cycle includes, each `<plugin>/<step>`, placed where its manifest says (§C4). An
  `end` naming a plugin step includes that step without listing it.
- **Why the landing rule.** The end is how work proceeds after it lands: D157 §2.3 gives that sort of field to the landing
  rule under Current (`form`, `autoAccept`) and to the workflow where a named one governs. The plugin a cycle's steps need
  is already the rule's `plugin`. A key of its own would be a second rule that had to agree with this one (rejected below).
- **Inheritance.** The repository's whole landing rule replaces its workspace's (D164's amendment), and its cycle comes
  with it. A repository rule with no `cycle` takes the default, never the workspace's cycle. Under a named workflow, the
  run's bound version decides its `end`, read where its landing is read (TreeLands.cs:346-350). D157 §2.3's table gains
  the row *The end of work*: the plugin steps a repository may use stay a declaration, and `end` is the workflow's.
- **The default** is absent, read as `{ "end": "line", "steps": [] }`. That is today's behaviour exactly: ended when the
  line holds the committed tip, and the look's tidy as AUTOTIDY2a and 2b.
- **The CLI twin** is `driverconfig.ts`'s landing rule. `twins.md`'s row for the driver's choices names the field, and
  both twins read one table of rows the same way.
- **The Setup row.** The landing rule's rows on the repository's Setup and its workspace's gain *Work ends*, offering only
  what the rule can reach: *when it is on the line* (the default), *when its pull request completes*, *when it is pushed*,
  *when it is handed*, and each switched-on plugin's declared steps under the plugin's own label, shown as written (D142).
- **Refused at the door that sets it**, naming the Setup row that would allow it (D157 §2.3; D154's pattern):
  - `claimed`;
  - `pull-request` without a branch form whose plugin answers `work/state`;
  - `pushed` without a plugin;
  - a plugin step whose plugin is not installed, switched on, sound and declaring it.

  A cycle that later becomes unreachable (the plugin removed, the form changed) is said once, on the Setup row and in the
  machine log. Its units' end reads *not known*, and the removal goes on at *held* (§C3).

## C3. The end and the removal, kept apart

*Held* is the line holding the committed tip, by §B3's proofs: ancestry, content after a squash (with its commits kept on
`refs/daoris/discarded/`), or a completed pull request's merge commit on the line. **Only *held* lets the look's automatic
tidy take anything.** The tidy takes a unit at the later of *held* and *ended*:

| The defined end | The tidy takes the unit |
|---|---|
| before the line (`handed`, `pushed`, `pull-request`, a plugin step placed before the line) | at *held*, as today: an end defined earlier never by itself allows a removal (D88:150-153 stands) |
| `line` (the default) | at *held*, as today |
| after the line (a plugin's step placed after it, a deploy say) | at that end, waiting at most 7 days after *held*, then at *held*, said once (§Q1) |
| unreachable, or not known | at *held*, said once |

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

**The process's lifetime.** Today a landing starts its plugin for one frame, while the loop keeps a process for each
plugin on a loop point (`HookPoints.Loop`).

- The loop keeps one process per plugin on `work/watch`: started when a unit first awaits one of its steps, and sent
  `shutdown` (ended 2 s later) once none does.
- On exit it is restarted with a back-off of 1, 5 and 15 minutes. Three exits within an hour mark it unsound until its
  page's *Try again* (D119).
- At most 60 events a minute are read from one plugin; past that, they are dropped and said once.

**What makes an event believed.** A `work/step` counts only where all four hold:

1. **The plugin is the one a rule names for that step** (D100: a plugin acts on a repository only where a rule names it):
   the landing rule's `plugin` for `pushed` and `pull-request`, and the cycle's `steps` for the plugin's own.
2. **The step is declared** in its manifest, confirmed by its handshake, and included by the effective cycle.
3. **Its `commit` is the unit's current tip**, or for `pull-request`, D148 point 4's three checks pass. A newer commit
   makes it stale.
4. **It reads by shape**, its `at` is no more than a minute ahead, and no newer event is kept for that step and commit.

Anything else is no event. It is counted in the machine log as `plugin.step` with a code (`not-named`, `not-declared`,
`stale`, `shape`, `late`) and no words.

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
| 2 | `work.held` and `work.ended` both standing (§C3) | the look's automatic tidy, judging again inside its hold (D88) | `work.tidied` | fixed; no setting turns it off (D88's AUTOTIDY2a judgement) |
| 3 | `work.ended` | `work/ended` to the plugins that listen; the quest's page and What needs you read it | nothing | fixed |
| 4 | any `work.*` | `STATE_CHANGED` with the existing scopes (D169) | nothing | fixed |
| 5 | `ledger`, `timer(look)` | the local plan (FABRIC-EVENT3a) | starts, through the existing paths | fixed |
| 6 | `refs` moving origin's line, Daoris's push, `focus` while a pull request is open | a plugin's `work/state` ask, with a fetch of the line (§A4.3) | an answer kept as today | fixed moments, within D148's minute |
| 7 | `home`, `timer(<folder>)` | that folder's reader | its own | fixed |

**What a workspace and a repository configure** is which step ends their work, and which plugin steps their cycle
includes (§C2). So the event that ends a piece of work is theirs to choose: a ref, a platform's word, or a plugin's step.
**What stays fixed, for safety:** what judges, what may remove and when, what is published, and that every topic keeps a
reconciliation.

**Order.** Hints for one subject coalesce in its due set and are judged in one pass that reads the truth as it stands, so
their order does not matter. A plugin's events for one step are ordered by `at` and commit: an older one never replaces a
newer, and a failure never replaces a kept answer (D148 point 5).

**De-duplication.** A due set is a set. A plugin event equal to the kept one (the same plugin, unit, step, commit and
`id`) is no event.

**Loops**, since a chain must never wake itself:

- **A link fires on a change, never on a standing fact.** A judgement that finds what it found before emits nothing.
- **The links are ranked** (sources, judgements, facts, actions, publication), and a link leads only to a higher rank.
- **An action's effect comes back once.** When the tidy deletes a branch, the ref watcher hears it and the end judgement
  runs once, finding no change.
- **The change stream answers families by their own revisions**, so the driver's own sync pass wakes nothing (§A4.2).
- **No configuration can make a loop.** The configurable part is chosen from fixed values, none of which adds a link.

## C7. Where a chain's state lives

- **The definition:** the landing rules in `driver.json` (their `cycle`), and a named workflow's version file for its
  `end` (D157 §2.4). One of them governs a run, by its binding.
- **The facts, one store each:**
  - claimed: the ledger;
  - handed, pushed and pull-request: the landing entry, as today (`LandedAt`, `Pushed` and `PushedTip`,
    `PullRequestState`);
  - held: `Held { At, By, Tip, Line, On, OnAt, Repository }` on the landing entry and on the session branch's entry
    (§B5's fields and rules, renamed);
  - a plugin's steps: `Steps [{ plugin, step, commit, state, at, id, evidence }]` on the same entries, the newest per step
    and commit.

  §B5's traces, its one writer across processes and its rules for when a fact stops holding apply to each.
- ***Ended* is never stored.** It is derived at read from the kept facts and the effective cycle, so a changed definition
  reads at once and no verdict goes stale.
- **What is in flight:** the due sets, the de-duplication ids and the plugins' processes live in memory. A restart loses
  them, and the start's reconciliation judges every topic whole. A chain's progress is never a second record.
- **The machine log** (D94), with codes and no words: `chain.fired` (link, topic, count), `chain.dropped` (code), and
  `plugin.step` (plugin, step, and its state or refusal code).

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
| **A timed plugin query** | Same reason | **Stands** (D148:84-85). The fetch-when-due moments of §A4.3 add occasions to D148 point 2's list, which is an amendment to D148 (§Q3) |
| **No watcher yet** (the scout's 6.3: nudge on Daoris's own moves and measure first) | The probe measured it: every ref write heard on this machine under its antivirus, with no process start | **Overridden by the owner's direction**, on the measurement; the scout's safety is kept as the reconciliation, and its caution as the recovery and bounds of §A4.1 |
| **Git hooks** (`reference-transaction`, `post-commit`) | Daoris writes nothing into the person's repository configuration (SessionBranches.cs:28-30), and the person's and agents' git would run Daoris's code | - |
| **`fsmonitor` or an outside file watcher service** | A dependency that watches working trees, not refs | - |
| **SSE or a WebSocket for the change stream** | A new transport for one hint; a long-poll uses `ServiceClient` and the host as they are | - |
| **An in-memory change signal in the service host** | Each connector writes the store from its own process (Service.Mcp/Program.cs:100), so the host would not hear an agent's quest | - |
| **A watcher on a task's output file** (row 17) | Its offset read sees every byte and is its own reconciliation | **Stands**: OutputTail.cs:16-18 |
| **Raising `pollSeconds` or the tidy's pace now** | Before the wakes land and their recovery is measured, a raise only makes things later; `pollSeconds`' CLI twin and the rehearsals read it | - |
| **A `cycles` key of its own** (part C) | A second rule that must agree with the landing rule's form and plugin, which D164's inheritance would split: a repository could take its workspace's cycle and its own landing rule | - |
| **An end only in a named workflow** | The editor is not built (WORKFLOW1g), and the owner asked for each repository's and workspace's end now, so Current must carry it; a named workflow's `end` still governs where one is chosen | - |
| **`claimed` as an end** | The owner's own condition: a step is reached only on git's or a plugin's evidence; and the model is not the authority on its own success | - |
| **Storing *ended*** | A verdict that goes stale the moment the definition changes; derived from the kept facts instead | Part B's `Ended` field, superseded by `Held` |
| **Chains as rules the person writes** (an event, a condition, an action) | D65's no engine, and D157 §2.1's no expressions; what is configured is the end and its steps, chosen from fixed values | - |
| **A configurable tidy after the end** | D88 judged that no setting turns the look's tidy off: what goes is what the press would clear, kept fourteen days | **Stands** (AUTOTIDY2a) |
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
6. No end without git's proof: each case of §B7 is not ended, and a failed read leaves the subject unknown.
7. An end stops holding when its tip, its line's commit, its cycle's line or its repository changes.
8. The removal judges again: an end contradicted before the act keeps the branch; the person's removals are unchanged.
9. One writer: a structure test finds no writer of `Ended` outside the end judgement, and a verb beside an instance writes
   none.
10. Measured on the owner's use: the machine log's look timing, its git starts per hour, the delay from a ref move to its
    end, and the watchers' handles and nonpaged memory, before and after (Process classes and a day's log, not a load).
11. A chain that changes nothing wakes nothing: a judgement that finds what it found emits no event, and the tidy's own
    ref moves are judged once.
12. No configuration makes a loop: every value a `cycle` may take is tried against the link table.
13. A plugin event that is not named, not declared, stale or misshapen is no event, and no event removes anything.
14. The default cycle is today's: with no `cycle` set, the Trees suites and the tidy's tests are unchanged.
15. An end before the line never lets the tidy go before *held*; an end after it waits no longer than its bound.

**The rows**, in order, re-cut on part C. Each is sized near a hundred turns, and each later row names what it waits on.
The first two were dispatched as part B proposed them; FABRIC-END2a takes the cycle as an input, so part C's defined
cycles plug into it.

| # | Row | Lanes | After | Builds | Proves |
|---|---|---|---|---|---|
| 1 | FABRIC-EVENT2 | driver | - | `ChangeWatch` with its topic queue and recovery (§A3, §A4.1), the topic-carrying `Nudge`, the request and presses folders on it with a 3 s listing behind, and a machine-log line timing the watch's look | canaries 1-3 on a fake source; a Process class with a real watcher on a scratch folder; a request answered within the debounce, and inside the caller's 10 s with the source silenced |
| 2 | FABRIC-END2a | driver | - | the end judgement extracted: §B3's cycles and proofs as one pure judgement over a snapshot, with no store and no reader changed | canary 6; each cycle of §B3 with the named workflow's landing; the Trees suites unchanged |
| 3 | FABRIC-CYCLE1a | driver, cli | 2 | the `cycle` on the landing rule with its built-in steps (§C2): both twins, D164's inheritance, a named workflow's `end`, the refusals naming the Setup row | canary 14; one table of rows read the same by both twins; each refusal; a repository rule replacing its workspace's cycle whole |
| 4 | FABRIC-END2b | driver | 2, 3 | the facts kept (§C7): `Held` and `Steps` on both entries, *ended* derived from the effective cycle, traces on both forget paths, one writer across processes | canary 9; an old entry reads as not yet judged; a trace survives the branch on every removal path; a changed cycle reads at once |
| 5 | FABRIC-END2c | driver | 4 | when a fact stops holding (§B5): the tip, the line's commit, the cycle's line, the repository | canary 7, with real git in a Process class (a line rewound, a repository replaced at its path) |
| 6 | FABRIC-CHAIN2 | driver | 1 | the chains' links as a declared, ranked table (§C6), firing on changes only, and each topic's deadline as a `timer` event (§C5); `chain.*` lines in the machine log | canaries 4, 11 and 12; a link to a lower rank fails a test |
| 7 | FABRIC-END3 | driver | 1, 5, 6 | the ref watcher on link 1 (§B4) | canaries 1-4 on git; a Process class with real git (a commit, a merge onto the line, a pack-refs, a tree's switch, each judged once within a second); canary 10's first day |
| 8 | FABRIC-END4a | driver | 7 | the look's automatic tidy on link 2, at the later of *held* and *ended*, with the 7-day bound (§C3) | canaries 8 and 15; the tidy's git starts counted down; `BranchTidyingProcessTests`, `SweepTests` and LAND3's tests green |
| 9 | FABRIC-CYCLE1b | web-shell, modules | 3 | the Setup row *Work ends* in both locales, and Ask Daoris's room saying a repository's cycle | the Setup rows' tests and the locale checks; the help room's goldens; HelpCoverage's row for any new press |
| 10 | FABRIC-END4b | driver, web-shell | 4, 9 | the page draws the cycle: the steps reached, the end marked, *held* said (§B6) | both locales; the window shows a merged pull request's session as ended with no press |
| 11 | FABRIC-PLUGSTEP1a | driver, cli | 4, 6 | a plugin's `steps` in its manifest, and `work/watch`, `work/step` and `work/ended` (§C4): both manifest twins, the kit's entries, the process's lifetime, belief and its refusal codes; the cycle's plugin steps validated | canary 13; a fake plugin's events believed and refused by each rule; the manifest twins' tables |
| 12 | FABRIC-PLUGSTEP1b | examples, tools | 11 | an example plugin that contributes a step from its own watch, with its README and `plugins try` samples | the example's tests; the rehearsal check written for the parent to run |
| 13 | FABRIC-EVENT3a | driver | 1, 6 | the look's topics: the remote sync on its own clock, and a `ledger` wake that runs the local plan only | canary 4; the planner's tests unchanged; a sync pass still at its own pace |
| 14 | FABRIC-EVENT3b | service, driver | 13 | the change stream (§A4.2), `sessions` first | canary 5; a second connection's write wakes the wait; the cursor's bootstrap, stale, malformed and future cases; an agent's quest starts without the poll |
| 15 | FABRIC-END5 | driver, modules | 10 | the CLI, a quest's page and Ask Daoris read the cycle and its end | the CLI's and the rehearsal's sentences; the help room's goldens |
| 16 | FABRIC-END6 | driver | 7; §Q3 | a fetch and a plugin's ask when due (§A4.3, link 6) | each moment and its bound; never at a removing press; an end with no press |
| 17 | FABRIC-EVENT4 | driver, modules | 1, 6 | the home's record files on the watch (§A4.1, link 7) | each heard within the debounce, and found by its deadline when silenced |
| 18 | FABRIC-END7 | driver | 14, 17 | auto-landing woken by refs, the ledger, the config and the gate's declarations, together | every release of AutoLander.cs:153-177 lands without the look |
| 19 | FABRIC-EVENT5a | web-shell, modules | 17 | Ask Daoris's two polls (rows 23, 24) become publishes | no interval in a desktop window; a plain browser keeps its polls (D169 §4) |
| 20 | FABRIC-EVENT5b | web-settings, modules | 17 | Settings' wiring poll (row 22) becomes a publish | as row 19 |
| 21 | FABRIC-EVENT6 | service | 1 | the knowledge server marks a checkout stale on a watch (row 20); the service references no driver library, so its watch is a twin in `twins.md` or a shared home the row chooses | a document's edit read at the next search, with no minute's re-read |
| 22 | FABRIC-EVENT7 | driver, cli | 7, 14, 17 | the clocks as deadlines (§C5): `pollSeconds` the `look` topic's, with its CLI twin, the tidy's pace the `refs` topic's, each default raised only after its measurement | canary 10 shows the wakes carry the work, and their recovery held, before and after |

# Questions only the owner can answer

**Answered** in D173's acceptance note: a push may be a defined end, on its evidence (§C2, §C3); what only a server knows
comes from a plugin, learned its own way (§C4); and the clocks are events, with chains (§C5, §C6). The two clock raises
stay behind their measurement (FABRIC-EVENT7). Still open:

1. **An end after the line** (a deploy a plugin reports): should the automatic tidy wait for it? *Recommendation:* yes,
   at most 7 days after the line holds the work, then as today, said once.
2. **The end and the quest.** Should a quest's Done wait for the end, or the end be written on the quest's record (D144's
   evidence) so an asker on another machine sees it? *Recommendation:* not in this pass. This machine's quest page shows
   it beside the claim (FABRIC-END5); the evidence rows decide, and the service keeps knowing nothing of landing (D46).
3. **May a plugin be asked at the new due moments** (amending D148 point 2): when the person's fetch moves origin's line,
   after Daoris pushes a handed branch, and at the window's focus while a pull request is open? *Recommendation:* yes,
   within D148's one-minute freshness, each with a fetch of the line so the merge commit is held here, and never on a
   timer or at a press that removes. A plugin's own watch (§C4) is the plugin's, and needs no such yes.

# What this design does not decide

- **The end crossing machines** (a teammate's view of an end): machine-local in the first pass (D63).
- **The look's further split into topic clocks** beyond FABRIC-EVENT3a's: each topic its own row, after the wakes land.
- **The harness frames for usage** (§A4.4), **lock releases** (row 11) and **the knowledge server's freshness**
  (row 20): later rows.
