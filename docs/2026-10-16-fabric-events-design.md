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
- at the window's focus while a handed unit waits on its pull request, bounded by D148's one minute (§Q4).

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
- **§B8:** the automatic tidy still needs *held*. Whether it also waits for an end defined after the line is the owner's
  choice (§C3, §Q1). Everything else stands.

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

Every step after `handed` is about a commit: a newer commit on the work makes it stale for that work (D157 §5.3).
`pushed` and `pull-request` are built in: a plugin speaks them through the points it already has (`work/land`'s answer,
`work/state`'s answer or its event form, §C4), never through its declared `steps`, which name only its own.

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
  machine log. Its units' end reads *unreachable*, and the removal goes on at *held* (§C3).

## C3. The end and the removal, kept apart

*Held* is the line holding the committed tip, by §B3's proofs: ancestry, content after a squash (with its commits kept on
`refs/daoris/discarded/`), or a completed pull request's merge commit on the line. **Only *held* lets the look's automatic
tidy take anything.**

An end not yet reached is one of three, said apart everywhere it is read:

- **pending**: reachable and not yet reached. The pull request is open, or the plugin has not reported its step.
- **unreadable**: reachable, but its evidence cannot be read now. The plugin's process is down or refusing, or a git read
  failed. It is tried again at its topic's deadline.
- **unreachable**: the cycle names what its rule can no longer reach. The plugin is removed or switched off, the step is
  no longer declared, or the form changed. It is said once, and stays so until the definition changes.

| The defined end | The tidy takes the unit |
|---|---|
| before the line (`handed`, `pushed`, `pull-request`, a plugin step placed before the line) | at *held*, as today: an end defined earlier never by itself allows a removal (D88:150-153 stands) |
| `line` (the default) | at *held*, as today |
| after the line, reached | at *held* (it is later) |
| after the line, pending or unreadable | **the owner's choice (§Q1)**; until it is made, at *held*, as today |
| unreachable | at *held*, said once |

**A deadline's origin is persisted and stable.** If the owner chooses to wait, the wait counts from `Held.Since`, the
moment *held* was first recorded for the unit's current tip. It is kept on the entry, unchanged by later judgements. It
resets only when *held* stops holding and is recorded again, never at a restart or a re-read. D157 §5.4's seven days
stops a run's asking; it is no ground for a removal.

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
plugin's step. Whether a workspace may also choose further effects from a bounded menu is the owner's (§Q2). **What
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
- **A removed unit becomes a trace.** Its kept facts stay as they were at the removal, with `GoneAt` and `RemovedAs`, the
  tombstone that `landings.json` already keeps and that §B5 adds for session branches. A trace is never judged again:
  §B5's rule that a fact stops holding when the tip moves applies to standing units only. So a branch's disappearance
  never reads as its end being lost.
- **An effect is done once its unit no longer meets its condition.** A trace meets no effect's condition.
- **So each wake ends.** It runs one judgement per due subject (finitely many) and at most one try of each pending
  effect. That try either completes, turning the unit into a trace, or leaves it pending until the next unblocking wake.
  The world's echo of a completed effect is judged once, finds no change and no pending effect, and emits nothing.
- **The change stream answers families by their own revisions**, so the driver's own sync pass wakes nothing (§A4.2).
- **No configuration adds a link that can feed itself.** Under §Q2's menu, if chosen, every effect is one that writes
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
  - a plugin's own steps: `Steps [{ plugin, step, commit, state, at, id, evidence }]` on the same entries. Each plugin
    step keeps at most two: the newest for the current tip, and the newest for the tip before it. An entry holds at most
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
| **A timed plugin query** | Same reason | **Stands** (D148:84-85). The fetch-when-due moments of §A4.3 add occasions to D148 point 2's list, which is an amendment to D148 (§Q4) |
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
| **Chains as rules the person writes** (an event, a condition, an action, freely) | D65's no engine, and D157 §2.1's no expressions | A bounded, typed menu of events and effects, with no expressions, is not rejected: it is §Q2's option |
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
15. An end before the line never lets the tidy go before *held*; pending, unreadable and unreachable are said apart, and
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

**The rows**, in order, re-cut on part C. Each is sized near a hundred turns, and each later row names what it waits on.
The first two were dispatched as part B proposed them; FABRIC-END2a takes the cycle as an input, so part C's defined
cycles plug into it.

| # | Row | Lanes | After | Builds | Proves |
|---|---|---|---|---|---|
| 1 | FABRIC-EVENT2 | driver | - | `ChangeWatch` with its topic queue and recovery (§A3, §A4.1), the topic-carrying `Nudge`, the request and presses folders on it with a 3 s listing behind, and a machine-log line timing the watch's look | canaries 1-3 on a fake source; a Process class with a real watcher on a scratch folder; a request answered within the debounce, and inside the caller's 10 s with the source silenced |
| 2 | FABRIC-END2a | driver | - | the end judgement extracted: §B3's cycles and proofs as one pure judgement over a snapshot, with no store and no reader changed | canary 6; each cycle of §B3 with the named workflow's landing; the Trees suites unchanged |
| 3 | FABRIC-CYCLE1a | driver, cli | 2 | the `cycle` on the landing rule with its built-in steps (§C2): both twins, D164's inheritance, a named workflow's `end`, the refusals naming the Setup row | canary 14; one table of rows read the same by both twins; each refusal; a repository rule replacing its workspace's cycle whole |
| 4 | FABRIC-END2b | driver | 2, 3; the cross-process writer: RPC1's pipe covering the verbs that write these two records (RPC1d covers the sessions verbs; HOMELEASE1 leaves a verb beside an instance holding nothing, HomeLease.cs:192-197), or a cross-process lock on the two files, chosen first | the facts kept (§C7): `Held` (with `Since`) and `Steps` on both entries, *ended* derived from the effective cycle, traces on both forget paths, written only by the lease's loop | canaries 9 and 20; an old entry reads as not yet judged; a trace survives the branch on every removal path |
| 5 | FABRIC-END2c | driver | 4 | when a fact stops holding (§B5), on standing units only: the tip, the line's commit, the cycle's line, the repository | canary 7, with real git in a Process class (a line rewound, a repository replaced at its path, a trace untouched) |
| 6 | FABRIC-CHAIN2 | driver | 1 | the chains (§C6): fact links on a change, effect links pending until done with their unblocking wakes, and each topic's deadline as a `timer` event (§C5); `chain.*` lines | canaries 4, 11, 12, 16, 17 and 18; a link to a lower rank fails a test |
| 7 | FABRIC-END3 | driver | 1, 5, 6 | the ref watcher on link 1 (§B4) | canaries 1-4 on git; a Process class with real git (a commit, a merge onto the line, a pack-refs, a tree's switch, each judged once within a second); canary 10's first day |
| 8 | FABRIC-END4a | driver | 7; §Q1, and PLUGSTEP1e where the answer is to wait | the look's automatic tidy as link 2's pending effect, at *held*, with §C3's rows | canaries 8, 15, 16 and 17; the tidy's git starts counted down; `BranchTidyingProcessTests`, `SweepTests` and LAND3's tests green |
| 9 | FABRIC-CYCLE1b | web-shell, modules | 3 | the Setup row *Work ends* in both locales, and Ask Daoris's room saying a repository's cycle | the Setup rows' tests and the locale checks; the help room's goldens; HelpCoverage's row for any new press |
| 10 | FABRIC-END4b | driver, web-shell | 4, 9 | the page draws the cycle: the steps reached, the end marked, pending, unreadable and unreachable said apart, *held* said (§B6, §C3) | both locales; the window shows a merged pull request's session as ended with no press |
| 11 | FABRIC-PLUGSTEP1a | driver, cli | 3 | a plugin's own `steps` in its manifest, both twins (`plugins.ts`, `PluginManifest`), `twins.md`'s row; the cycle's plugin steps validated against them | the manifest twins' tables; each manifest refusal |
| 12 | FABRIC-PLUGSTEP1b | driver | 11 | the handshake for `work/watch`, `work/step` and `work/ended` in `HookPoints`, and the reverse transport: the reader accepting `work/step` on a watch process only (Hooks.cs:349-351), with its byte, rate and queue bounds | a notification on any other process or method still ignored; each bound; `protocolVersion` 1 unchanged |
| 13 | FABRIC-PLUGSTEP1c | driver | 12 | the kit: `plugin-kit`'s template sends and tests the frame, its bounds and its refusals (`plugin.test.mjs.template`:183) | the kit's own tests; a kit-made plugin's frame read by the host's reader |
| 14 | FABRIC-PLUGSTEP1d | driver | 6, 12 | supervision: the lease's loop owns the process, its start and `shutdown`, restarts on an unasked exit only, the generation and `resync` | canary 19; an intentional shutdown never counted; three unasked exits mark it unsound |
| 15 | FABRIC-PLUGSTEP1e | driver | 4, 14 | authorization: §C4's five rules, the unit looked up in the last frame sent, a built-in `pull-request` event written to `PullRequestState` by its existing writer, the refusal codes | canary 13; each rule refusing a fake plugin's event; no event removes anything |
| 16 | FABRIC-PLUGSTEP1f | examples, tools | 13, 15 | an example plugin that contributes a step from its own watch, with its README and `plugins try` samples | the example's tests; the rehearsal check written for the parent to run |
| 17 | FABRIC-EVENT3a | driver | 1, 6 | the look's topics: the remote sync on its own clock, and a `ledger` wake that runs the local plan only | canary 4; the planner's tests unchanged; a sync pass still at its own pace |
| 18 | FABRIC-EVENT3b | service, driver | 17 | the change stream (§A4.2), `sessions` first | canary 5; a second connection's write wakes the wait; the cursor's bootstrap, stale, malformed and future cases; an agent's quest starts without the poll |
| 19 | FABRIC-END5 | driver, modules | 10 | the CLI, a quest's page and Ask Daoris read the cycle and its end | the CLI's and the rehearsal's sentences; the help room's goldens |
| 20 | FABRIC-END6 | driver | 7; §Q4 | a fetch and a plugin's ask when due (§A4.3, link 6), with no second fetch where the occasion just fetched | each moment and its bound; never at a removing press; an end with no press |
| 21 | FABRIC-EVENT4 | driver, modules | 1, 6 | the home's record files on the watch (§A4.1, link 7) | each heard within the debounce, and found by its deadline when silenced |
| 22 | FABRIC-END7 | driver | 7, 18, 21 | auto-landing woken by refs, the ledger, the config and the gate's declarations, together | every release of AutoLander.cs:153-177 lands without the look |
| 23 | FABRIC-EVENT5a | web-shell, modules | 21 | Ask Daoris's two polls (rows 23, 24) become publishes | no interval in a desktop window; a plain browser keeps its polls (D169 §4) |
| 24 | FABRIC-EVENT5b | web-settings, modules | 21 | Settings' wiring poll (row 22) becomes a publish | as row 23 |
| 25 | FABRIC-EVENT6 | service | 1 | the knowledge server marks a checkout stale on a watch (row 20); the service references no driver library, so its watch is a twin in `twins.md` or a shared home the row chooses | a document's edit read at the next search, with no minute's re-read |
| 26 | FABRIC-EVENT7 | driver, cli | 7, 18, 21, 22 | the clocks as deadlines (§C5): `pollSeconds` the `look` topic's, with its CLI twin, the tidy's pace the `refs` topic's, each default raised only after its measurement | canary 10 shows the wakes carry the work, and their recovery held, before and after |
| 27 | FABRIC-CHAIN3 | driver, cli | 6; only if §Q2 chooses the menu | the chain's bounded menu: typed events and non-destructive effects a workspace selects, with no expressions, in both twins | canary 12 over every menu value; no chosen effect writes a ref or a ledger family a link listens to |

# Questions only the owner can answer

**Answered** in D173's acceptance note: a push may be a defined end, on its evidence (§C2, §C3); what only a server knows
comes from a plugin, learned its own way (§C4); and the clocks are events, with chains (§C5, §C6). The two clock raises
stay behind their measurement (FABRIC-EVENT7). Still open:

1. **Does the automatic tidy wait for an end defined after the line** (a deploy a plugin reports) while it is pending or
   unreadable? (§C3)
   - (a) No: the tidy goes at *held*, as today, and the later end changes only what is said.
   - (b) Yes, for a bound N counted from `Held.Since`, then at *held*, said once. N is to be measured; D157's seven days
     stops asking and justifies no removal.
   - (c) Yes, until the end is reached or the person presses.

   *Recommendation:* (a). The work is on the line, the tree is moved aside for fourteen days and can be brought back,
   and (b) can follow once a plugin's real delays have been measured.
2. **May a workspace configure its chains beyond its end?** (§C6)
   - (a) The end only: a workspace selects which step ends its work and which plugin steps it includes.
   - (b) A bounded, typed menu as well: for a fixed set of events (a step reached, failed, ended, held), the workspace
     chooses effects from a fixed list that never remove anything and never write a ref or a ledger family (to tell the
     person in What needs you; to send a named plugin the observation). No expressions and no scripts. Judging,
     removal, publication and the reconciliation stay fixed (FABRIC-CHAIN3).

   *Recommendation:* (b), built after the fixed chains run (FABRIC-CHAIN2), each new menu item its own decision.
3. **The end and the quest.** Should a quest's Done wait for the end, or the end be written on the quest's record (D144's
   evidence) so an asker on another machine sees it? *Recommendation:* not in this pass. This machine's quest page shows
   it beside the claim (FABRIC-END5); the evidence rows decide, and the service keeps knowing nothing of landing (D46).
4. **May a plugin be asked at the new due moments** (amending D148 point 2): when the person's fetch moves origin's line,
   after Daoris pushes a handed branch, and at the window's focus while a pull request is open? *Recommendation:* yes:
   - within D148's one-minute freshness, its kept answers standing (a failure never overwrites one);
   - with a fetch of the line so the merge commit is held here, except where the occasion itself just fetched (the
     person's fetch, bringing up to date), so no fetch is made twice;
   - never on a timer, and never at a press that removes.

   A plugin's own watch (§C4) is the plugin's, and needs no such yes.

# What this design does not decide

- **The end crossing machines** (a teammate's view of an end): machine-local in the first pass (D63).
- **The look's further split into topic clocks** beyond FABRIC-EVENT3a's: each topic its own row, after the wakes land.
- **The harness frames for usage** (§A4.4), **lock releases** (row 11) and **the knowledge server's freshness**
  (row 20): later rows.
