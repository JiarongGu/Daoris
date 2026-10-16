# Events over polling, and the end of a piece of work (FABRIC-EVENT1, FABRIC-END1)

**Proposed, 2026-10-16, pending the owner's yes and a second maker's opinion.** The contract is D173's two notes of
2026-10-16, *the end of a piece of work* and *events over polling*, which are the owner's direction in their words. This
document designs both together because they share one shape (Part A), and the end of a piece of work is that shape's
first consumer (Part B). It builds nothing; the rows at the end do.

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
3. **The judgement stays where it is.** A source wakes the existing judge; no handler judges beside it (D173's guard: one
   writer per concept).
4. **The state is published once**, by the publisher D169 built, never by the source.
5. **Local operation needs no remote**, and a source a remote feeds is only ever a further wake.

## A2. The inventory

The scout's table, read again against the code (*read* marks the rows whose file and interval were checked on this
branch, at the lines named). Costs are read from the code unless marked *measured*, which means this branch's probe
(§A5) on this machine. `D` is `src/Daoris.Desktop/Daoris.Desktop.`.

| # | Where | Every | Waits for | Cost per round | Who feels it | Replaced by | Kept as the reconciliation | |
|---|---|---|---|---|---|---|---|---|
| 1 | DDriver/Loop/DriverWatch.cs:241-261, `Running.NextAsync` with `config.PollSeconds`; `Nudge` :89-98 | `pollSeconds`, 15 (DriverConfig.cs:60) | everything: config, plugins, proposals, quests, sessions, asks, channel, endings | 8+ local HTTP calls, the plugin catalogue, config and folder reads, hook reconcile, auto-landing's git (row 4). Not logged: the install's machine log carries no look or tick timing (§A5) | the person: a quest a terminal, an agent or a teammate made starts up to 15 s late | the service's change stream (§A4.2); the home's files (§A4.1); about 40 routes already `Nudge()` | `pollSeconds` as a slow clock, raised only once the wakes carry the work | read |
| 2 | DriverLoop.cs:696-723 `DRIVER_TICK`; the page's ShellSignals.tsx | each changed look | 9 query families invalidated | 9 refetches | the person | scoped `STATE_CHANGED` from the look's own diffs | the tick as the catch-all | |
| 3 | DDriver/Service/RemoteSync.cs via Driver.cs `SyncAsync` | each look; after an ending | each circle's registry, feeds and quests | 4+ HTTP calls per circle, 3+ per repository | the person: a teammate's quest is late; the remote | a held fetch on the remote's cursors where reachable (§A6, row 10) | a pull 60-120 s apart | |
| 4 | DDriver/Trees/Landing/AutoLander.cs:98-160 (Driver.cs:644-657) | each look | each open due entry | git starts per open entry per look | CPU, and the window under antivirus | the ref watcher and the ledger's change (Part B) | minutes | |
| 5 | DDriver/Trees/Branches/BranchTidying.cs:29 `DefaultPace` 5 min, via DriverWatch.cs:214-225 | 5 min | every checkout's session and landing branches | a `RefSnapshot` per repository judged: one `for-each-ref` (*measured* 83-124 ms) and one worktree list (*measured* 75-135 ms), plus the proofs' reads | load; ends seen up to 5 min late | the ref watcher (Part B §B4) | the 5 min pass | read |
| 6 | DDriver/Trees/PullRequestStates.cs:231 `Fresh` 1 min; LandingPlugins.cs | at a D148 occasion, per entry due | a plugin's `work/state` | a node process and a platform call | the clean-up's list | a fetch when due (§A4.3) | D148's occasions | read |
| 7 | DDriver/Sessions/Requests/SessionRequestWatch.cs:80, 236-259, a 1 s `PeriodicTimer` | 1 s | the requests and presses folders | a listing a second | a terminal's stop or say, on a headless loop once RPC1d lands | the change watch on the two folders (§A6, row 1) | a listing 30 s apart | read |
| 8 | DDriver/Sessions/SessionsCommand.cs:68-71; Work/WorkPausing.cs:19-22 | 250 ms up to 10 s | the ledger record moving | a GET each 250 ms | a terminal | RPC1d's pipe while a desktop runs; the change stream otherwise | the 10 s bound | |
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
  watch's look is not timed anywhere; §A6's first row adds the line that times it.
- **Row 21's cursors** (`/api/sessions/since`, `/api/channel/since`, `/api/quests/operations`, Program.cs:1821, 1857,
  1931) are the shared mode's doors (README:534-537), and the local service does not serve them to itself. More to the
  point, every session's connector composes the store in its own process (`ServiceFactory.CreateAsync`,
  Service.Mcp/Program.cs:100) and writes the database directly. A quest an agent publishes never passes through the
  desktop's service host, so nothing held in that host's memory hears it (§A4.2).

## A3. The shape

```text
 source ──hint──▶ debounce ──▶ wake ──▶ judge ──writes──▶ its one store ──▶ publish (STATE_CHANGED)
   ▲                                      ▲
   └── overflow, lost handle, start ──────┴── reconciliation: the same judge, on a slow clock
```

- **Source.** Something that can say *a thing under this root moved*: a file watcher (§A4.1), the service's change stream
  (§A4.2), a fetch that completed (§A4.3), a harness's frame (§A4.4), a process's exit. It says a topic and a subject.
  It never reads the thing, and it never decides.
- **Debounce.** Per topic and subject, a quiet window of 250 ms after the last hint, capped at 2 s so a steady stream
  still wakes. Measured (§A5): a git operation's events all arrive before git exits, the longest burst within about
  230 ms of its start, so one operation is one wake.
- **Wake.** `DriverWatch.Nudge` gains a topic and a subject, held in a set the next look reads; the plain `Nudge()`
  stays and means *everything*. First, a wake runs the look as today with the woken set beside it, so a judge can do
  only what is due. Later, the look splits into topic clocks, one row at a time, each with its measurement.
- **Judge.** The existing judgement, reading the truth afresh: git through a `RefSnapshot`, the ledger through its
  service, the files through their record classes. A hint's path is never read as the value.
- **Publish.** The judge writes its own store; the page learns through `STATE_CHANGED` with the existing scopes,
  through the path D169 point 8 built for a terminal's press (STATEHUB1e), never a second publisher.
- **Reconciliation.** The same judge on a slow clock: `pollSeconds` for the look, the tidy's pace for the trees. It also
  runs at once for a root whose watcher raised `Error` (an overflow), lost its handle, or was just made, and at start.

## A4. The sources

### A4.1 Files: Daoris's home folders, and git's common dir per repository

One primitive, `ChangeWatch`, in the driver library beside `DriverWatch` (the headless host has no bus, so not in the
shell), in `Platform/`, since the structure rule keeps `FileSystemWatcher` out of `Domain` (Driver.Tests/StructureCheck.cs:74).
It owns one `FileSystemWatcher` per root, with a 64 KB buffer and subdirectories, a path filter, the debounce, the
overflow-to-reconcile, and a seam a fast test replaces with a fake source that raises events, overflows and renames.

**Git: one watcher per repository's common git dir, never per tree.** A tree's `HEAD` is `<common>/worktrees/<name>/HEAD`
and its branches are the common dir's refs, so the common dir hears every tree of that repository (measured: a tree's
switches and commits were heard there, §A5). The filter keeps `HEAD`, `packed-refs`, `refs/heads/**`, `refs/remotes/**`,
`worktrees/*/HEAD` and `reftable/**` (git's optional reftable format writes tables, not loose refs); it drops
`objects/**`, `logs/**`, `index` and every temporary (`*.lock`, `*.new`): a temporary's rename onto its final name is
the event. The watched set is the repositories whose checkouts here hold Daoris's work (the set the tidy judges), and it
is rebuilt at each reconciliation.

**The home's folders:** the requests and presses folders (row 7), `driver.json` and the plugins folder (row 1),
`lines-moved.json` and the held words (row 1), the staged update's folder (row 12) and the help proposals folder (row 24).

### A4.2 The service's change stream: a long-poll

`GET /api/changes?since=<epoch>.<n>&wait=<s>` on the local service, a machine door that a shared host never serves. It
answers as soon as something moved after `since`, or at `wait` (30 s at most) with nothing: `{ "since": "<epoch>.<n>",
"moved": ["ledger"] }`. It says families, never records, and carries no path (D169 §3). The driver holds one open
and nudges the look with each family; a refused or dropped poll is retried with a back-off, and the look's own clock
still runs behind it.

What wakes it is the database's own change counter, read in the host's process: SQLite's `PRAGMA data_version` on one
held connection changes when any other connection commits, and every connector is another connection. It is an
in-process call on a short interval inside the held request, with no process start and no HTTP. It replaces the
driver's eight calls each fifteen seconds. The first family is `ledger`, the whole store; finer families (`sessions`,
`quests`, `channel`) come where each store's own numbers are shown to move on every write (`session_revision`,
Sessions.cs:496-540; quest operations, Quests.cs:2032; the channel's, Channel.cs:286). The epoch changes when the host
starts, so a stale `since` answers *everything moved*.

### A4.3 A fetch when due, for what only a server knows

A pull request's completion, or a remote branch moving, is known to the platform first. The desktop is not reachable for
a webhook, and local operation needs no remote, so the fact is fetched, at moments that say it may be due rather than
on a clock (D147:62, D148:84-85; the owner's own words, D173's events note point 3):

- after Daoris pushes a handed branch, or a plugin opens its pull request (the next answer is now worth asking);
- when the person's own `git fetch` or pull moves `refs/remotes/origin/<line>`, which the ref watcher hears: the plugin's
  completed answer can now be confirmed here;
- at D148's four occasions, which stand;
- at the window's focus while a handed unit waits on its pull request, bounded by D148's one minute (§Q6).

A back-off clock while a pull request is open is a timed fetch, and stays the owner's call (§Q2). A webhook, where a
team's service could take one, would only ever wake such a fetch (§Q3).

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

**Not measured**, and why it matters:

- **Overflow.** Not provoked, by the brief's rule against load on this machine. The design does not depend on it never
  happening: `Error` reconciles the root.
- **The antivirus under a scan or an install**, or with the home's folders, which it may treat differently from the
  repository's `_fixtures/` folder.
- **A watcher held for hours, across sleep and resume**, a watched folder deleted or a repository moved, a network or
  removable drive, and tens of repositories at once (handles and buffers).
- **A fetch's writes** to `refs/remotes` (the probe had no remote), and the reftable format.
- **The instant between a replace's deletion and its rename.**

The EVENT2 and END3 rows carry the Process classes and the machine-log lines that measure these on the owner's use.

## A6. The replacements, ranked and sliced

Ranked by what the person feels, then by risk:

1. **The look's 15 s and its fan-out** (rows 1-3): a quest an agent publishes starts late, and every look calls the
   service eight times. The change stream (§A4.2) and the home's files (§A4.1).
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
- **Ended.** The line holds the work: the repository's configured line, as the local branch or as origin's copy last
  fetched, proven by git. It is a fact about a tip: the end names the commit it was judged at, and a branch that moved
  past it is not ended.
- **Dropped** is not an end: the person's Discard or abandon, recorded where it already is (`abandoned.json`,
  `DISCARD_SESSION_TREE`), never inferred.

## B3. The cycles

There is no `end` setting, and none is added: the cycle is derived from the repository's landing rule, else its
workspace's (`landings` and `workspaceLandings`: form, plugin, squash, tidy, autoAccept; Landing.cs:47, `Choose` :945),
and from the commit rule, which says when work first exists in git. Uncommitted work is never ended.

| Cycle | When handed | When ended | The proof it needs (existing code) | What may go then |
|---|---|---|---|---|
| Merge form | at *Accept* | at *Accept*: the merge is on the local line | the session tip is an ancestor of `refs/heads/<line>` (`LineHolds`) | the tree at LAND3's tidy where the rule says `tidy`, else the look's |
| Branch form, no plugin | at *Accept*: the landing branch is made | when the line holds it: the person merged or pulled it | ancestry against the local line or `refs/remotes/origin/<line>` (`LandedProofs`); by content after a squash, its commits first kept on `refs/daoris/discarded/` (`ContentHolds`) | the session branch, its tree, then the landing branch (AUTOTIDY2a/2b) |
| Branch form with a plugin (a pull request) | at *Accept* and the plugin's push (`Pushed`, `PullRequest`) | when the line holds it, as above, or on the plugin's completed answer where git confirms it | `VerdictAsync` (PullRequestAsks.cs:202): completed, the target is the line, the merge commit is held here, the recorded tip is under the source commit (D148 point 4) | as above; `carried` and `removedAs: "pull-request"` |
| Squash (`squash` on the rule) | at *Accept*: one commit on the landing branch, `Squashed` recording the session tip | when the line holds the landing branch's commit, by ancestry or its pull request | the recorded `Squashed` tip vouches for the session's commits (PROCESS1b-ii); the line holding the landing commit | as above |
| *Accept* at done (`autoAccept`, D145) | at the quest's done, by the rule's switch | as the form it lands by | as that form | as that form |
| A chain's advance (LAND2c) | each later done advances the landing branch | each session when the line holds its advance's `To` | as the form, per advance | each session branch as its advance ends |
| A tree its agent moved (TREEFOLLOW1) | as its form, on the branch it moved to (`Now`) | when the line holds `Now`'s work; landed only on the person's other branch is not ended (D88:150-153) | as the form, on `Now` | as the form |
| No landing (no rule; the person merges by hand) | never by Daoris | when the line holds it | the look's `landed` kinds, as AUTOTIDY2a | the session branch and its tree |
| Nothing beyond the line | - | at once: there is nothing to hand | `empty` (the branch holds no commit the line lacks) | the session branch and its tree |

D173's note says one workspace may end *"at Accept's branch pushed"*. Today that is **handed**: D88:150-153 keeps a
branch landed only on another branch of the person's until the line holds it, since its pull request may still be open
and the person may still ask its session to change the work in its own tree. Whether a workspace may count *handed* as
its end is §Q1.

## B4. The observer

**The ref watcher** is Part A's `ChangeWatch` on each repository's common git dir (§A4.1). Its hint, debounced, makes
that repository's end judgement due and nudges the look. The look starts the judgement beside itself, as it starts the
tidy today (DriverWatch.cs:214-225), for the due repositories only. The judgement takes one `RefSnapshot`, reads the
open units of that repository (session branches and landing branches not yet ended at their tips), runs §B3's proofs,
and writes each end it finds (§B5). Nothing is removed here (§B8).

- **Daoris's own ref moves** (*Accept*, bringing up to date, a discard) nudge in process as well, so the press's next
  read does not wait on the debounce.
- **The person's git and the agents' git** (a commit in a tree, a merge onto the line, a pull, a fetch) are what only the
  watcher hears without a poll.
- **Auto-landing's due entries** (row 4) are looked at when their repository's refs move or the ledger says a quest
  reached done (§A4.2), instead of every look.
- **The remote's end** comes by a fetch when due (§A4.3); the fetch's own write to `refs/remotes` is then heard like any
  other.
- **The reconciliation** is the tidy's 5 min pass, unchanged, which judges every repository as today. It stays until the
  watcher's canaries have held on the owner's use (§Q5).

**The cost.** The watcher starts no process. A judgement costs one snapshot (about 180 ms here, idle) for a repository that
moved, where today every repository costs one each pace. Under antivirus, git process starts are the cost that matters
(PROC1a, FIX-LOG:2136-2175; UIFREEZE1), and the watcher removes the ones for repositories that did not move.

## B5. The store

One store per fact, no second writer (D173's guard), and no new file:

- **Handed** stays where it is: the landing entry in `landings.json`, and its advances.
- **Ended, for a landing branch**: a field on its `landings.json` entry, `Ended { At, By, On, Tip }`. `By` is one of the
  proof codes the clean-up already writes (`LandedKind`: `on-line`, `merged`, `inside`, `pull-request`), `On` the form of
  the line that held it (as `RemovedOn` names it), and `Tip` the commit judged. `RemovedAs` and `RemovedOn` stay the
  removal's, written when it goes, which may be long after.
- **Ended, for a session branch**: the same field on its `session-branches.json` entry (`GrownBranch`), with `By` from
  the sweep's kinds (`empty`, `landed`, `carried`) and the landing branch it was inside, where it was. This is the record
  the merge form and the no-*Accept* case lack today.
- **A session branch's entry outlives its branch once ended.** Today `ForgetGoneAsync` (BranchReplays.cs:406) drops an
  entry when its branch goes, called by the look's tidy (AutoTidies.cs:155, 216), LAND3's tidy (TreeTidies.cs:97), the
  landed clean-up (LandedCleanups.cs:207) and bringing up to date (TreeSync.cs:255). An ended entry is kept as a trace
  instead, the newest 50 per repository, as `landings.json`
  keeps its traces (LEFT3, `TracesKept`), so a quest's close and the page can still say where the work went after the
  tidy took the branch.
- **One writer**: the end judgement, in the driver library, through each file's existing record class and its lock.
  *Accept*'s landing calls it after writing the handed facts; no reader writes an end.
- **Machine-local** (D63), never in the repository, never on a shared host's wire in the first pass. The ledger keeps the
  claim, and nothing in the service changes (D46: the service knows nothing of landing).
- **Additive shapes.** A field is added and an old entry reads as not yet judged, never as ended. The end is judged
  again at the next reconciliation.

## B6. The readers, and what each stops judging for itself

| Reader | Today it judges | It reads instead | What it still does itself |
|---|---|---|---|
| The look's tidy, the sweep's list, LAND3's tidy, the landed clean-up's plan | each branch's proofs, per look or per list (the UIFREEZE1 path asked git three times per checkout) | its candidates: units ended at their current tip, and `empty` | judges again inside its hold and right before each goes; every keep of D88, D102 and LAND3 (§B8) |
| The chat's *Accept* and the page (DiffPane's landed note, LandedNote, SessionHead's foot, What needs you, WorkspaceTidied, Sweep, BranchPage) | `SessionGroups.LookAsync` and `TreeWorkReads` derive landed and on-the-line | handed and ended from the driver's answer, which reads the store | nothing: a view holds no state (D169 §1) |
| A quest's close | nothing: Done is the agent's word | its work's end, shown beside the claim | never changes the ledger in this pass (§Q4) |
| The CLI: `trees clean`, `state`, `land --plan`, `sync`; `git branches` | the same proofs as the press | the store, printed | its words as today, unless a row changes them with the rehearsal's readers (`tools/family-rehearsal.mjs`:4031-4198) |
| Ask Daoris (HelpRoomDoors, HelpRoomMachineNow, HelpRoomMayPropose, HelpRoomWorkspaces, HelpRoomMayDo) | what the page's reads say | the store, through the same reads | its goldens updated with the words |

The page learns that an end was written through `STATE_CHANGED` with the existing scopes (`landing`, `sweep`, `sessions`,
`treesSync`), published once by the path STATEHUB1e built. No scope is added until a reader needs one.

## B7. Never ended by a guess

- the model's `done`, a quest's Done, a session's Completed, a conversation's end;
- a remote branch read as `[gone]`;
- patch equivalence, or comparing trees (D102, D148's rejections);
- a plugin's word without its merge commit on the line here (D148 point 4);
- an unread, failed or timed-out git answer: absent is never zero (D57);
- one read woken by a hint: a ref replaced is told as deleted first (§A5);
- a tip the branch has moved past: the end names its tip;
- landed only on another branch of the person's (D88:150-153), unless the owner says otherwise for a workspace (§Q1).

A session running or waiting in the tree, uncommitted or untracked work, a held lock, waiting words and a review showing do
not stop an end being observed: they stop a removal, as they do today, and a reader says both (*ended; its tree holds
uncommitted changes*).

## B8. The observation, kept apart from the removal

The end judgement observes and writes the end; it never removes. Removal stays where it is (the look's tidy, the
press's sweep, LAND3's tidy, the landed clean-up), takes its candidates from the ends, and keeps every guard it has:
judged again inside the hold, once more right before each goes, the delete only at the commit judged, commits kept on a
`refs/daoris/discarded/` ref where proven by content, the tree moved aside for fourteen days. So a false end costs nothing
(the removal's own judgement refuses it), and a missed end costs time (the reconciliation finds it).

---

# Rejected shapes

| Shape | Why not | Against an earlier rejection |
|---|---|---|
| **Trusting a watcher alone** | Windows drops events on overflow and while a handle is reopened; a missed end would never be seen | The owner's direction says the same (D173's events note point 2) |
| **A watcher per tree** | A tree's `HEAD` and refs live in the common dir (measured, §A5); per-tree watchers multiply handles and hear a build's churn in the working files, which no end needs | - |
| **Watching the service's database file** | WAL writes, an internal format, and another process's store read behind its owner's back; the database's own `data_version`, asked through SQLite, is the API | The scout's *never watch the SQLite file* stands |
| **Event handlers judging apart from the look** | Two judges of one concept is the second writer D173's guard forbids; a handler only wakes | - |
| **A new `ends.json`** | A second record keyed on what `landings.json` and `session-branches.json` already key | - |
| **An `end` key in `driver.json`** | Every cycle in §B3 is derived from the landing rule and the plugin; a key only where a cycle cannot be | - |
| **The end at the quest's done or a conversation's end** | A claim, not evidence (Evidence-First Completion) | - |
| **A webhook only** | The desktop is not reachable, local operation needs no remote, and with no reconciliation a missed delivery never ends | Not rejected as a further wake where a reachable service exists (§Q3); the owner's direction allows it there |
| **A timed fetch** | A network call nobody waits on | **Stands** (D147:62): the owner's direction itself says *not on a clock*. A per-workspace back-off is the owner's call (§Q2) |
| **A timed plugin query** | Same reason | **Stands** (D148:84-85). The fetch-when-due moments of §A4.3 add occasions to D148 point 2's list, which is an amendment to D148 (§Q6) |
| **No watcher yet** (the scout's 6.3: nudge on Daoris's own moves and measure first) | The probe measured it: every ref write heard on this machine under its antivirus, with no process start | **Overridden by the owner's direction**, on the measurement; the scout's safety is kept as the reconciliation |
| **Git hooks** (`reference-transaction`, `post-commit`) | Daoris writes nothing into the person's repository configuration (SessionBranches.cs:28-30), and the person's and agents' git would run Daoris's code | - |
| **`fsmonitor` or an outside file watcher service** | A dependency that watches working trees, not refs | - |
| **SSE or a WebSocket for the change stream** | A new transport for one hint; a long-poll uses `ServiceClient` and the host as they are | - |
| **An in-memory change signal in the service host** | Each connector writes the store from its own process (Service.Mcp/Program.cs:100), so the host would not hear an agent's quest | - |
| **A watcher on a task's output file** (row 17) | Its offset read sees every byte and is its own reconciliation | **Stands**: OutputTail.cs:16-18 |
| **Raising `pollSeconds` now** | Before the wakes land, a raise only makes things later; its CLI twin and the rehearsals read it | - |

---

# Canaries and proof, and the rows

**Canaries**, each held by a fast test on the fake source unless named:

1. A missed event costs latency, never a removal: with the source silenced, the reconciliation ends and tidies at its pace.
2. An overflow (`Error`) or a lost handle judges that root at once.
3. A burst of hints for one repository is one judgement.
4. A hint carries no state: the judge reads git, and a ref written back before the judge runs is judged as it stands.
5. A deletion hint followed by nothing is not an end.
6. No end without git's proof: each case of §B7 is not ended.
7. An end names its tip: a branch moved past it reads not ended, and is judged again.
8. The removal judges again: an end contradicted before the act keeps the branch.
9. One writer: a structure test finds no writer of `Ended` outside the end judgement.
10. Measured on the owner's use: the machine log's look timing, its git starts per hour, and the delay from a ref move to
    its end, before and after (a Process class and a day's log, not a load).

**The first three slices**, in two lanes, the first two able to run side by side:

1. **FABRIC-EVENT2**, the change watch on the request folders (driver).
2. **FABRIC-END2**, the end recorded at the existing judgements, with no watcher (driver).
3. **FABRIC-END3**, the ref watcher feeding the end judgement (driver), after both.

The full rows are in the hand-back to the steward, who places them in TASKS.md.

# Questions only the owner can answer

1. **May *handed* count as the end for a workspace?** D173's note names *"Accept's branch pushed"*; D88:150-153 keeps such a
   branch until the line holds it. *Recommendation:* not as the end that removes: the tree and branches stay until the
   line holds the work (D88 stands). A workspace whose work leaves through a pull request may close its quests and show
   *done* at handed, if the owner wants it, by a landing rule field (`close: handed|ended`, default `ended`).
2. **May a workspace fetch on a back-off while a handed unit's pull request is open** (amending D147:62)? *Recommendation:*
   not yet. Build the due moments of §A4.3 first, measure how late ends are on the owner's use, then decide with that
   number. If allowed: per workspace, opt-in, only while a pull request is open, 1, 5, 15 and 60 min, then the
   reconciliation's pace.
3. **The webhook.** *Recommendation:* none now. Where a team's service exists and the platform can reach it, a webhook
   records only *pull request N of repository R moved*, with no path, and the desktop's change stream wakes a fetch when
   due. Never an end by itself.
4. **Should a quest's Done wait for the end?** *Recommendation:* not in this pass. The end is shown beside the claim; the
   evidence rows decide whether it becomes a required evidence kind.
5. **The slow clocks.** *Recommendation:* raise `pollSeconds`' default from 15 to 60 s once the change stream and the
   home's files carry the look's work, and the tidy's pace from 5 to 30 min once the ref watcher's canaries hold for a
   week, each its own row with its measurement.
6. **May a plugin be asked at the new due moments** (amending D148 point 2's list): when origin's line moves by the
   person's fetch, and at the window's focus while a pull request is open? *Recommendation:* yes, within D148's one-minute
   freshness; still never on a timer or at a press that removes.

# What this design does not decide

- **The end crossing machines** (a teammate's view of an end): machine-local in the first pass (D63).
- **The look's split into topic clocks**: each topic its own row, after the wakes land.
- **The harness frames for usage** (§A4.4), **lock releases** (row 11) and **the knowledge server's freshness**
  (row 20): later rows.
