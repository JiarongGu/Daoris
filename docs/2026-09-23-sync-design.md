# The remote as a git remote — design (SYNC, D68)

> *"the remote share server is more like a git system, and local is the main driver and we can
> push/sync/merge/rebase remote so to keep everything in sync, what this gives is the system works
> with/without remote share and we can have proper merging logic when there is conflict"* — the
> owner, 2026-09-23.

This amends D47, whose rule was that a joined quest lives at the remote and its verbs write through
or fail. The owner answered its three trade-offs the same day (§4–§6).

## 0. What the sync did before this (read from the code, 2026-09-23)

Snapshots every tick, no history, no cursor. A joined quest had one home, the remote; this machine
held a read-only mirror, and every verb on it wrote through or failed, so nothing on a shared quest
could happen offline. Knowledge went up only, ordered by commit time against one held commit.
Registrations went up with the last writer winning, and foreign rows came down once. Session records
went up only, all of them, every tick. Five defects, none covered by a test:

- **SYNC0a** — a write-through answer reset the mirror row's workspace to `default`
  (`RemoteQuests.cs` builds no workspace), so the next verb in another circle resolved the wrong
  remote until a tick repaired it, and no tick runs while a session works.
- **SYNC0b** — foreign registrations were mirrored once and never updated or removed, and no retire
  travelled either way.
- **SYNC0c** — two machines at the same commit could replace each other's knowledge every tick:
  the same commit was always accepted, and the content was each machine's index, not the commit.
- **SYNC0d** — a newly wired remote needed a restart (`RemoteSyncSet` and `RemoteQuestRoutes` are
  built once), though `RemotesModule` says the loop re-reads the map on its next pass.
- **SYNC0e** — a quest from an ask or an unregistered sender to a foreign repository was frozen
  after publish: the mirror filter matched neither end.

## 1. The principle: every machine is a repository, the remote is a bare remote

- **Every verb commits locally, and always succeeds locally**, judged by the same transition table
  wherever it runs (D47's lock stays one class). A machine with no remote works exactly as one with
  a remote that is down, and exactly as today's local mode.
- **Change is kept as history, not only as current state.** A quest is its operations in order. Its
  current status is what replaying them through the transition table gives, and the table the
  platform reads is a cache of that replay.
- **Sync is fetch, rebase, push**, per wired workspace (D48 unchanged: one deployment serves one
  workspace). The remote orders what it accepts, the way a remote branch orders commits. It is the
  meeting point, not the only store.

## 2. What history each record has

| Record | Its history | Push is accepted when |
|---|---|---|
| **Quest** | Operations: `published`, `taken`, `done`, `declined`, `conflict`, `dismissed` (§5), `waited` (D79), `deleted` (D95). Each has the machine that made it (a stable machine id under the home, not the key, which rotates), a per-machine sequence, the time, and its payload (note, reason, session) | Per quest, a fast-forward: nothing new has reached that quest at the remote since the push was rebased (§3) |
| **Knowledge, code map** | The repository's own git — Daoris keeps no second history | The pushed commit descends from the one the remote holds (`git merge-base --is-ancestor`, asked on the machine with the checkout). The same commit is a no-op only if its content digest matches |
| **Registration** | The manifest at a commit; a retire is a tombstone that travels | Same ancestry rule as knowledge. The machine holding the checkout owns the row; others pull it, updated and removed |
| **Session record** | Owned by the machine that ran it; append-only | Always, by origin + id: two machines cannot write the same record |
| **Ask, usage, accounts, driver settings** | Stay on the machine (unchanged) | — |

Knowledge already had most of this (WSP4): it now compares ancestry where it compared timestamps,
and a content digest where it trusted the same commit.

**The machine id lives in the store, beside the sequence it numbers** (SYNC1). It does not get a
file of its own. If a store were deleted and its machine id kept, the sequence would start again at
one under the same id and reuse numbers the remote already holds. A new store is a new machine. Two
hosts over one store are one machine, and `BEGIN IMMEDIATE` keeps them from taking one number twice.

## 3. Fetch, rebase, push

1. **Fetch**: the operations the remote accepted since this machine's cursor for it, in the remote's
   order. The remote numbers each operation as it accepts it, and that number is the cursor.
2. **Rebase**: this machine's unpushed operations are replayed on top of what was fetched, through
   the transition table. One that still applies stays pending. One that no longer applies (a take on
   a quest another machine took first) becomes a `conflict` operation on that quest, carrying what
   was attempted (§5). It is never dropped.
3. **Push**: the pending operations, with the remote number they were rebased on. The remote
   re-judges them through the same table and accepts per quest only if that quest has not moved
   since. Otherwise it answers *fetch first*, and the machine goes round again, a bounded number of
   times.

**Nothing is queued in the old sense, because nothing needs to be**: an unpushed operation is simply
history the remote has not seen yet, like an unpushed commit.

## 4. The lock, online and offline — decided: claim by push

**The take itself claims by push (D69).** A take commits on the machine where it was made. When the
quest's workspace has a remote, the take is then pushed and awaited before the answer returns.
Accepted: the quest is taken. Lost (another machine's take reached the remote first): the take
rebases into a conflict, and the answer is *already taken, stand down*, so the session stands down
before any work. That is D47's guarantee, no duplicate work while online, kept by a fast-forward
instead of by a remote home. It holds for a driven session and an outside one alike, because the
session is still the one that claims (D46). A take by the driver on the session's behalf was the
first wording here, and D69 records why it was not built.

**Unreachable: the take stays local, unconfirmed, and the session runs.** Offline work is allowed,
and this is the trade D47 declined: two machines offline can both work one quest. §5 is how that
ends. Once a machine's take has lost, its later moves on that quest were made on a claim it never
held, so the rebase turns them into conflicts too.

## 5. Conflicts — decided: first push wins, the loser is kept

The remote's order decides. The operation that reached it second becomes a **`conflict` on the
quest**: which machine, what it attempted, its note, its session and that session's evidence
(commits, which stay in the repository they were made in). The quest shows its state *and* the
conflict, until a person dismisses it or acts, for instance by publishing a follow-up. Nothing is
thrown away, and nothing is merged automatically into a second truth.

**Dismissing is an operation that travels** (SYNC6c). A conflict keeps the machine and sequence of
the move that lost, which names it on every machine. A person dismisses it from the quest's drawer,
or with `daoris-driver sync dismiss <quest>` for every conflict the quest carries. That appends a
`dismissed` operation naming it, which the next pass carries like any other. It moves no status. It
applies to any quest there is, whether or not the conflict is still there, so two people dismissing
one conflict make one dismissal, never a refusal or a new conflict.

A losing take whose session is still running is stopped by its own machine's driver, with the reason
in the record (*another machine's take reached the remote first*). A driver stops only its own
processes (D47 §6 unchanged).

## 6. When it runs — decided: automatic, and on demand

- **Every driver tick**: fetch, rebase, push for each wired workspace, like auto-fetch. A session
  running no longer holds the sync back: the sync gets its own cadence beside the tick. Before, a
  tick ran sessions to completion inside itself, so no sync ran for up to 30 minutes.
- **On demand**: `daoris-driver sync [status] [--workspace <name>]`, the terminal door (D50). The
  screen door is a *Sync now* action. There is no `pull` or `push`. A pass pushes what it rebased on
  what it fetched, and the tick runs the whole pass within seconds, so holding back either half would
  be undone before anyone relied on it. The knowledge feed stays in the driver, because git
  provenance needs a spawn and the service spawns nothing (D46 §7). The quest sync runs in the hosts
  (D69), because a take pushes and awaits from the door it was made at. The driver's tick asks its
  local host for a pass.
- **Seen**: the status bar shows, per workspace, what is ahead (unpushed), behind and in conflict,
  with the time of the last sync. Conflicts are listed where they can be acted on. A pass fetches and
  rebases in one step, so nothing is ever fetched and left unapplied. *Behind* is therefore the quests
  the last pass could not bring level, because the remote moved them on every round. The time the
  circle last reached its remote says how old that knowledge is. A pass that hits a wall keeps that
  time, and names the wall as the last try. The host's half of a pass runs whatever the driver's
  feed met, because the host is where a try is recorded. A feed that stopped the pass left the
  standing saying *synced* while the sync was failing.

## 7. What stays, and what goes

**Stays from D47/D48:** shared mode is the same host; per-person per-machine keys; the two manifest
declarations and the structural strip (no roots, transcripts or private content in anything pushed);
one workspace per deployment; records keyed by origin; no processes and no doctrine on the remote.

**Goes:** the remote *home* of a quest (the `home` column and the read-only mirror), write-through
verbs, the refusal *home unreachable*, snapshot re-sends every tick, and registrations where the last
writer wins. Nothing is deployed, so the store is rebuilt rather than migrated (the store's own rule).

**Widened:** quest ids. Six hex characters (24 bits) were enough for one machine. Once every machine
holds every quest touching its repositories, unrelated asks can collide, so an id grows to twelve
characters (48 bits) and stays content-derived. The same ask from two machines is still one quest.
The hash did not change, so a quest from before keeps the six characters it was quoted by, and the
same ask finds it as the first six of today's id.

## 8. The quest doors (SYNC2)

**The number is the remote's log position.** A remote numbers an operation by appending it: its
own log's position is the number, gap-free and only ever growing. A machine keeps, per operation,
the number the remote gave it (none while it is pending), and per workspace, a **cursor**: the last
number it fetched. The cursor is not the highest number it holds. An operation this machine pushed
may be numbered past operations on other quests that it has not fetched yet.

**A history replays in the remote's order, then this machine's.** Accepted operations go by
number. Pending ones follow, in the order they were made. On a machine with no remote, nothing is
ever numbered, so the order is simply the order the operations were made.

**The remote's doors** (shared mode only):
- `GET /api/quests/operations?since=N` returns what it accepted after N, in order.
- `POST /api/quests/operations {base, operations}` judges each quest on its own. It is **behind** if
  anything reached that quest after `base`. It is **refused** if an operation does not apply through
  the table, or a publish fails the exchange's judgement (a receiver not registered there, a link
  or a file name no record keeps). Otherwise it is **accepted** and numbered. An operation the
  remote already holds, by machine and sequence, is answered with its number, so a retried push is
  harmless.

**The machine's doors** (local mode only, D69): `POST /api/sync?workspace=` runs one pass of
fetch, rebase and push, and answers what a person should hear about it: conflicts, refusals, what is
still behind, and the wall if there was one. `GET /api/quests/{id}/claim` says where this machine's
claim on a quest stands (held, unconfirmed, lost or none). The pass is the host's (`QuestSync`), and
there is one of it: a take on a shared quest runs it to claim by push, and the driver's tick asks for
it. Replaying and rebasing happen in the store, through the one table. The remote's doors and the
host's client speak one wire, `QuestWire`.

**What is pushed: quests whose receiver is joined** in that workspace, whether the joined
repository is on this machine or is a teammate's registered here without a root. Silence means
local. This is D47's *home follows the receiver*, kept as a disclosure rule now that a quest has no
home. A quest to a local-only repository never leaves the machine, even if its asker is joined. An
ask is not a repository and has no row, so its quests are pushed by their receiver alone (SYNC0e).

**Neither door carries a workspace.** A published operation arrives without one, and the receiving
side files it by its own wiring: at the remote, by the receiver's registration; on a machine, by the
sync it came through (SYNC0a).

**A rebase never drops a loser.** A pending take, done or decline that no longer applies becomes a
`conflict` on the quest. A conflict carries what was attempted and its note, and changes no status.
Two things are dropped, because neither was ever anyone's decision. One is a pending publish of an
ask the remote already holds, since the first publish wins, as it always has. The other is a
follow-up that was published only by a close that has now lost, when nothing else has happened to it.

*Amended by D95 (QUEST1, 2026-09-30): a delete is an operation too, `deleted`, and a rebase drops a
third thing.* A delete applies only to an open quest and replays to no quest, so the remote keeps the
tombstone and no fetch from any cursor brings the quest back. A pending delete that no longer
applies is dropped: a take reached the remote first, or another machine's delete already did the
same. Its condition, that nobody has taken the quest, no longer holds, and it carries no work for a
person to reconcile. A pending take on a quest another machine deleted first becomes a conflict as
usual, which the remote keeps on a quest no list shows, so that machine's claim reads lost. What is
pushed is read from each quest's own first publish in the log rather than from its row, because a
deleted quest has none. A quest that never left the machine and was never numbered is removed
outright rather than tombstoned.

**The mirror is gone.** Its rows are dropped when the store opens, and so is the `home` column. The
first fetch runs from cursor zero, so it brings every row back as history. A tick syncs before it
plans and again after any session concludes, so a closure reaches the remote within the tick that
made it.

**Session records ride the same pass (SYNC4).** A machine's own records go up by a cursor: the
store gives every write the next revision, and a push sends what changed since the last one it made
for that workspace, for joined repositories only. The team's records come down by the remote's
revision cursor. A fetch leaves out the caller's own records, so nothing comes back doubled. A fetched
record keeps its `origin/id`, the id the web already reads as *elsewhere*. It is read-only, and it is
never this machine's lock: a teammate's session holds a tree on the teammate's machine, not here
(D47 §6). So neither the ledger's one-session-per-tree check nor the driver's planner counts it. The
doors are `POST /api/feed/sessions` (existing) and `GET /api/sessions/since?since=N` at the remote,
and the host's pass door becomes `POST /api/sync?workspace=` for quests and sessions together.

**Knowledge and the code map by ancestry (SYNC5a).** A feed names the commit the deployment held
when this machine checked it (`base`). Git on the machine with the checkout answers whether that
commit is in its history; the deployment cannot run git. The deployment's rules:
- It takes the feed when it still holds `base`. That is a fast-forward, checked the way a
  compare-and-swap is.
- It answers *moved* when another machine fed in between.
- It falls back to commit time when the machine could not order the feed: a diverged history, or an
  older client.

A machine that is behind, or that has not fetched the commit the deployment holds, feeds nothing and
says so. The same commit fed twice is a no-op when the content digest matches. When the digest
differs, the first reading of that commit stands, reported as information, and that ends SYNC0c. A
machine feeds knowledge only from a clean checkout, so that what it sends is the commit's content and
not work in flight. The code map (MAP3b) rides the same judgement and is re-judged whole at the door
by `CodeMapReader`. A newer commit with no map deletes the held one and keeps the commit held. The
doors are `GET /api/feed/held?repository=` (the commits a feeding machine asks git about) and
`POST /api/feed/code-map` beside the existing entries feed, which now carries `base`. The digest is
computed at the deployment, never taken from the wire.

**The team's code maps come down (MAP3e).** A machine's host pass brings down the map of each
repository of the circle it holds only a teammate's copy of, as the remote holds it, beside the
quests and the records. The remote already ordered it, so the machine holds what the remote holds:
a newer commit replaces, a commit with none is held as none, and one the remote no longer holds is
forgotten. The commit is asked first, so an unmoved map is not sent again. A checkout here is read
from disk and never asked for. The map design's §3 has the rest.

**Registrations and the travelling retire (SYNC5b).** The machine holding a checkout owns its row.
Other machines' copies are updated and removed rather than mirrored once, and a retire travels as a
tombstone (SYNC0b).
- **Up, by ancestry.** A registration names the commit its checkout stands on, and the held commit
  git said it descends from, exactly as a feed does. It names a commit only while the manifest file
  itself is unmodified, because the declaration is the manifest. A shared deployment holds each
  registration at a commit and judges it by the same `FeedOrder`, with a digest over the declaration.
  Three rules are the registration's own. The first registration is taken from any line, since
  nothing else of a repository can travel until it is registered. After that, one is taken only from
  the line it declares canonical, because a declaration on a feature branch is not yet the family's.
  And one naming no commit is taken only where none naming one is held.
- **Down, updated and removed.** On every pass, a row of the team's is written here when it is new
  or its declaration changed. A row held here without a root, in that circle, is retired here when
  the remote no longer lists it. A row held here with a root, in any circle, is never touched by the
  sync. A pass runs for every wired circle (§6), whether or not anything here joins it: nothing
  leaves that no manifest declared, and a machine that joined nothing still hears the team's rows
  and quests, and still carries a retire it owes.
- **A retire is a tombstone that travels.** A joined row with a root can leave a circle three ways:
  it is retired, re-wired to another circle, or re-registered unjoined. Each records a tombstone for
  that circle in the store. The next pass retires the repository at that circle's deployment, then
  clears the tombstone. It tells the circle only when the circle still lists the repository, so a
  deployment never hears a name it was not given. Joining that circle again clears the tombstone
  first, and one found for a repository joined here is void. A foreign row retired here records
  none, because removing a teammate's repository from the team is not this machine's to do. The
  deployment keeps no tombstone of its own. A retire names no commit to order it by, and another
  machine that still holds the checkout still declares the join, so its next pass registers the
  repository again. That is correct.
- **The doors**: `GET /api/feed/held` answers `registration` beside the knowledge and the map. A
  local host has `GET /api/registry/retired?workspace=` and
  `DELETE /api/registry/retired/{repository}?workspace=`, which the driver reads and clears. At the
  remote, the retire is `DELETE /api/registry/{repository}`, the same door a person retires with.
  On a local host that door's answer says when the circle will hear of it too.

## 9. Build order

- **SYNC0d first**, on its own: a newly wired remote takes effect without a restart. It stands under
  any design.
- **SYNC1 — quests as history, locally.** The operation log, replay through the transition table, the
  status table as its cache, widened ids. Local behaviour is unchanged, and every existing quest test
  passes on the log.
- **SYNC2 — fetch, rebase, push for quests.** The remote's operation door (fetch since a number; push
  rebased on a number, fast-forward per quest), the driver's loop, conflicts recorded. It replaces the
  mirror and the write-through, which dissolves SYNC0a and SYNC0e. The family rehearsal's two-machine
  phase is rewritten: the online race, the offline race, and one machine with no remote at all.
- **SYNC3 — claim by push**: the take pushed and awaited before a spawn, unconfirmed offline, and a
  losing session stopped.
- **SYNC4 — session records both ways**, by cursor instead of snapshot: every machine sees the
  team's, read-only.
- **SYNC5 — knowledge, code map and registrations by ancestry**: fast-forward by
  `merge-base --is-ancestor`, a content digest for the same commit, and retire as a tombstone. This
  dissolves SYNC0b and SYNC0c and carries MAP3b.
- **SYNC6 — the surfaces**, in three landings:
  - **SYNC6a — where a circle stands, and the terminal door.** Every pass records how it ended, in
    the store, whether a take or a tick ran it. A local host's `GET /api/sync?workspace=` answers
    ahead, behind, the quests in conflict, when the circle last synced and last tried, and the wall.
    It reads the store and reaches no remote. `daoris-driver sync` runs the tick's pass now, and
    `sync status` prints the standing.
  - **SYNC6b — the screen door.** The status bar's sync item, and *Sync now* over the bridge running
    the same pass as the terminal. The conflict list links each quest to where it can be acted on.
  - **SYNC6c — dismissing a conflict.** A person's dismissal is an operation that travels, so every
    machine stops showing it (§5). It changes no status.
