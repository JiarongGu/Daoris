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
| **Quest** | Operations: `published`, `taken`, `done`, `declined`, `conflict`. Each has the machine that made it (a stable machine id under the home, not the key, which rotates), a per-machine sequence, the time, and its payload (note, reason, session) | Per quest, a fast-forward: nothing new has reached that quest at the remote since the push was rebased (§3) |
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

**Before a driven session starts, the driver takes the quest locally and, when the quest's workspace
has a reachable remote, pushes the take and waits.** Accepted: the session starts. Rejected (another
machine's take reached the remote first): the take rebases into a conflict and nothing spawns. That is
D47's guarantee, no duplicate work while online, kept by a fast-forward instead of by a remote home.

**Unreachable: the take stays local, marked unconfirmed, and the session runs.** Offline work is
allowed, and this is the trade D47 declined: two machines offline can both work one quest. §5 is how
that ends.

## 5. Conflicts — decided: first push wins, the loser is kept

The remote's order decides. The operation that reached it second becomes a **`conflict` on the
quest**: which machine, what it attempted, its note, its session and that session's evidence
(commits, which stay in the repository they were made in). The quest shows its state *and* the
conflict, until a person dismisses it or acts, for instance by publishing a follow-up. Nothing is
thrown away, and nothing is merged automatically into a second truth.

A losing take whose session is still running is stopped by its own machine's driver, with the reason
in the record (*another machine's take reached the remote first*). A driver stops only its own
processes (D47 §6 unchanged).

## 6. When it runs — decided: automatic, and on demand

- **Every driver tick**: fetch, rebase, push for each wired workspace, like auto-fetch. A session
  running no longer holds the sync back: the sync gets its own cadence beside the tick. Before, a
  tick ran sessions to completion inside itself, so no sync ran for up to 30 minutes.
- **On demand**: `daoris-driver sync [status|pull|push] [--workspace <name>]`, the terminal door
  (D50). The screen door is a *Sync now* action. The sync stays in the driver because git provenance
  needs a spawn, and the service spawns nothing (D46 §7).
- **Seen**: the status bar shows, per workspace, what is ahead (unpushed), behind (fetched, not yet
  seen) and in conflict, with the time of the last sync. Conflicts are listed where they can be
  acted on.

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

## 8. Build order

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
- **SYNC6 — the surfaces**: ahead, behind and conflicts on the status bar; the conflict list with its
  actions; *Sync now*; `daoris-driver sync`.
