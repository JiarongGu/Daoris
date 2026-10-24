# Keeping as a chat app does: a finished session kept as its text record, its tree let go, and Daoris within an allowance the person sets (proposed, round 2, 2026-10-24)

**Standing:** proposed, a note amending D195 (KEEPSIZE1), round 2. Round 1 (the same day, at `47c9b78a9`) proposed a size limit per
kind beside its days and the floor as a share. The owner's directions since, carried in the row KEEPSIZE1 in `TASKS.md`, set a
different frame, and this round is rewritten to it:

- what is kept of finished work is its **text record**: the conversation, its notes, the landing and the commits' recovery refs,
  held in the knowledge system, indexed and searchable with the repository's records, never a copy of the tree the repository
  already holds;
- a completed session's **tree is cleaned up**, not set aside whole;
- Daoris manages **its own allowed space**: an allowance the person sets (*Daoris may use up to N GB*), divided among its kinds,
  keeping what it holds within it and letting go first of what can be made again or is only a copy;
- the drive's free space is **at most a secondary warning**, never the rule;
- the person can **clean up by hand at any time** on the *Disk space* card.

It adds removal nobody presses of what cannot be made again (a finished tree's ignored scratch), a durable record and a rule other
designs rest on (D195 points 3 and 4, and two of the cleanup design's §7 rejections). So it meets D191's foundation threshold:
Codex reads it, and Claude Opus 5.5 in Fable's place (D191's 2026-10-23 amendment), and the steward checks each finding against the
code and settles it. Nothing here is built.

Read for it: the row KEEPSIZE1 and KEEPSIZE1a in `TASKS.md`; D195 and the cleanup design (`docs/2026-10-21-cleanup-design.md`: §1,
§2, §3, §5, §7); the history clearing design (`docs/2026-10-07-history-clearing-design.md` §2); D88's AUTOTIDY1 and D102's
SQUASHTIDY1c notes. Every statement about today was read from the code at `16769207d` and names its file and line. None was run.
Driver paths are under `src/Daoris.Desktop/Daoris.Desktop.Driver/`, service paths under `src/Daoris.Service/Daoris.Service.Core/`.

## 0. The case, measured

**The owner's report** (the row, 2026-10-23): the disk keeps filling, and chat apps bound what they keep. On 2026-10-24 the drive
held 44.4 GB free of 1,024 GB (96% used), above D195's floor (20 GB on a 1 TB volume), so the floor never fired.

**Measured read-only on 2026-10-24**, under the install's home alone, by a walk that follows no link (Node's `readdir` and `lstat`,
40 minutes over 4.3 million files). Sizes are binary, as Windows shows them. Daoris's home held about 114 GB, 105 GB of it set-aside
trees.

| Kind (D195's table) | Size | Files | Age, growth | Notes |
|---|---|---|---|---|
| Set-aside trees at the trees entry's new place (`work/trees/.tidied`) | 27.9 GB | 466,233 | 77 folders: 6 set aside on 10-22, 71 on 10-23 (26.9 GB) | 74 with a slim manifest. `bin/` 58%, other build folders 14% (an Angular build cache), tracked files and the sessions' own 18%, `obj/` 8%, `node_modules` 3% |
| … at the old place (`data/trees/.tidied`) | 76.7 GB | 3,489,163 | 164 folders set aside from 10-15 to 10-22 | no rule reached them (§1.4); **removed by hand on 2026-10-24** by the steward on the owner's go-ahead (the row KEEPSIZE1a) |
| Live trees (`work/trees`) | 4.1 GB | 263,962 | 6 trees, all opened 10-23 | `node_modules` 55%, an Angular build cache 26%, tracked and own 10%, `bin/` 5%, `obj/` 3% |
| The browser's profile (`browser-engine`) | 872 MB | 8,770 | 360 MB written within the day | its caches 767 MB |
| Downloaded versions (`toolchain`) | 1.4 GB | 14,307 | written 09-21 to 10-07 | one unused since 10-21 goes on 10-28 |
| Agents' account homes | 1.3 GB | 5,516 | | the agents' own |
| The program (`app/`) | 578 MB | 252 | | replaced by an update |
| The pipe's journal (`run/pipe`) | 391 MB | 3 | about 200 MB a day | a day kept after it ends (`PipeJournal.cs:225-240`) |
| The service's copy (`data/bin`) | 369 MB | 126 | | |
| Left by an earlier build | 282 MB | 2,286 | before 10-01 | the press alone (KEEP1e) |
| Sessions' records (`sessions`) | 182 MB | 1,751 | 09-29 to today | history |
| The window's profile | 166 MB | 642 | | the UI kit's own |
| The knowledge store (`knowledge.db`) | 86 MB | 1 | | the index, the quests and the sessions' rows |
| Plugins | 54 MB | 1,615 | | |
| The machine log | 26 MB | 94 | | 30 days at most |

**What it says.**
- **The set-aside trees are the only kind that grows**, by about 27 GB a day, both before the slim (10-20) and after it (10-23).
  After the slim the bytes are `bin/`, which no rule may take before its days. Each of them is a tree whose work is already on the
  line (§1.1): a copy of what the repository holds, plus its build outputs and its session's scratch.
- **What a finished session leaves that a person reads is small.** All the sessions' records, conversations and transcripts
  together are 182 MB; the knowledge store 86 MB. A set-aside tree of this repository is about 205 MB alone.
- **Everything else is small or bounded**, and what Daoris holds without the set-aside trees is about 9 GB: the live trees 4.1 GB,
  the downloads 1.4 GB, the sign-ins 1.5 GB, the program and the service's copy 0.9 GB, the browser's profile 0.9 GB, the pipe's
  journal 0.4 GB, the records 0.3 GB.

## 1. Today, read from the code

### 1.1 When a tree is set aside, and what holds its commits then

The look takes a session's branch without a press where `TreeSetAside.GoesByItself` holds (`Trees/SessionTrees/TreeSetAside.cs:26-29`):
the branch is removable (empty, landed or carried, `Trees/Landing/Landing.cs:506`), read, and, where landed, its work is on the line
(`SweepItem.OnLine`, `Landing.cs:543-549`); a tree goes only where it is this home's and its branch moved since it grew
(`Worked`, `TreeSweeps.cs:328`), since an unmoved tree is a conversation's place (D137). Candidates come from the kept fact that the
line holds the branch at its tip (`AutoTidies.cs:233-254`), at the look's five-minute pace (`BranchTidying.cs:30, 74-130`) and on the
repository's facts changing (`TidyEffect.cs:34-52`): a finished tree goes aside within minutes of its work reaching the line.

At that moment its commits are held by one of these (`TreeSweeps.JudgeAsync`, `TreeSweeps.cs:290-434`):

| Kind of hold | Read at | What keeps the commits once the session branch goes |
|---|---|---|
| The line by ancestry (`refs/heads/<line>` or `origin/<line>`) | `:410-433` | the line itself |
| A landing's branch the line holds (AUTOTIDY2b) | `:425-431` | that branch; once the landing's clean-up removes it after a squash, a recovery ref (`LandedCleanups.cs:324-335`), a merged one's tip being on the line |
| A squashing landing's record | `:337-356` | the landing's branch, then its recovery ref, as above |
| By content (`HeldBy`) | `:369-377` | a recovery ref written before the branch goes (`TreeSweeps.cs:179-191`, `ContentHolds.KeepDiscardedAsync`, `ContentHolds.cs:283-310`) |
| A completed pull request carried them (`Carried`) | `:361-365` | **nothing of this repository's**: `GoesByItself` takes the kind without `OnLine` (`TreeSetAside.cs:28`), and no ref is written for it (`TreeSweeps.cs:181` writes one only for `HeldBy`) |

The recovery refs are `refs/daoris/discarded/<branch>` (`ContentHolds.cs:106-109`), dated by their reflog (RECOVERYDATE1,
`:290-294`). One that a branch or a tag of the person's holds goes 30 days on; one nothing else holds is kept until the person drops it
(`RecoveryRetention.cs:6-29`, `RecoveryDropping.cs:19-60`). They are listed in the work inventory (`WorkInventories.cs:88`).

**The move** is one rename to `trees/.tidied/<workspace>/<repository>/<tree>-<moment>`, then `git worktree remove` (`TreeSetAside.cs:190-235`).
Only uncommitted and untracked paths keep a tree from it (`TreeSweeps.cs:311`); its ignored paths, the session's own scratch among
them, go aside with it (`:312-314`). An hour later, where nothing was written since, its package folders go (the slim,
`KeepKinds.SetAsideSettle`, `KeepKinds.cs:23`); after the days (14 by default, `:17-20`) the purge takes the folder, kept while
anything in it was written or created after the move, while a link is in it or while it does not read (`TreeSetAside.cs:366-431, 518-593`).

### 1.2 What a finished session's record is today, and where

| Part | Where | Kept by | Searchable |
|---|---|---|---|
| The session's row: quest, repository, state, `Note` and `NoteParts`, `Evidence`, `Tree`, `BaseCommit`, the person's words (`Said`) | the `sessions` table of `knowledge.db` (`Sessions/Sessions.cs:101-339, 457-622`), opened on the knowledge store's connection (`Composition/ServiceFactory.cs:296-298`) | history: the person's clear or age (KEEP1g) | no |
| The quest: its publish, notes, decline reasons and the closing answer | `quests` and `quest_log` in `knowledge.db` (the history clearing design §2.1) | history | no |
| The conversation and its transcript | `data/sessions/<id>.events.jsonl` and `<id>.log` (`Sessions/History/SessionHomeFiles.cs:97-124`) | history | no |
| The landing: branch, line, tip, when | `data/landings.json` (`Trees/LandedBranches.cs:14-16`) | **the newest 50 traces per repository** (`:320, 565-574`) | no |
| The branch it grew from and its first commit | `data/session-branches.json` (`Trees/Branches/SessionBranches.cs:19-20`) | the newest 50 per repository (the cleanup design §1.2) | no |
| Its commits and their messages | the session branch, then the line or a recovery ref (§1.1) | the line; a recovery ref | by git alone |
| Its files | the tree, then the set-aside folder | its days | no |

The session's row names no branch (it is `daoris/<tree folder>`, `SessionTrees.cs:354-356`) and no commit beyond its base. So after 50
later landings in its repository and its folder's days, nothing on this machine says which commits a finished session made.

### 1.3 The knowledge system

`knowledge_search` reads one index: `EntryKind` is rule, knowledge, skill, decision, fix, task outcome and index
(`Knowledge/KnowledgeEntry.cs:7-34`, `KnowledgeQuery.cs:57-58`), every entry scanned from a repository's files
(`Registry/RepositoryScanner.cs:94-204`) and stored in `entries` with its full-text table (`Knowledge/Store/SqliteKnowledgeStore.cs:26`).
A repository's entries are **replaced whole** at each feed (`ReplaceRepositoryAsync`, `SqliteKnowledgeStore.cs:152-168`, called at
`KnowledgeService.cs:655`): anything put there that the scan does not produce is gone at the next refresh. **No session, conversation
or landing is indexed** today.

### 1.4 What each kind's rule is

| Kind | Its rule | Its bound |
|---|---|---|
| `set-aside` | the slim an hour after the move; the purge at 1 to 90 days, 14 by default; `whole` (`KeepKinds.cs:17-23`) | none |
| `downloads` | 0 to 365 days into a version's unused stretch, 7 by default, or `never` (`:31-37`) | none |
| `browser-cache` | the engine's own; cleared by the press and below the floor while the browser does not run (`SpaceFree`, `SpaceFloor.cs:502-529`) | none |
| `logs` | 7 to 30 days (`:44-47`) | 20 MiB a file |
| `history` | the press, or an age the person sets (`:55-61`) | none |
| the floor | the smaller of 10% of a volume and 20 GB (`FloorPolicy.Of`, `KeepKinds.cs:216-222`); below it the slims due, unused downloads and the browser's caches go at once (`SpaceFloor.cs:311-529`) | — |

Every reader of the set-aside folders reads `TreesRoot/.tidied` alone (`TreeSetAside.cs:38`), though the trees entry has an old place
too (`DaorisHome.cs:379`): the purge, the slim, the floor, the press's list, the look's report and the card's walk
(`SystemSpace.cs:64-96`). Folders set aside before a home's layout moved are reached by none of them (round 1's §1.1; KEEPSIZE1a).

## 2. A finished session's record

### 2.1 What it is

The record of a finished session is **text**, and nothing of its tree but names:

| Part | Its source | Already kept? |
|---|---|---|
| The conversation: the person's words and the agent's messages, in order, with their times | `sessions/<id>.events.jsonl` | yes, as history; the record indexes it, it does not move it |
| Its notes: the session's `Note` and `NoteParts`, the quest's closing answer and its requirements' answers | the session's row; `quest_log` | yes |
| The landing: the branch it landed as, the line, the tip, when, and which hold of §1.1 kept the commits | `landings.json`'s entry and the look's judgement | **no**: the trace is the newest 50 |
| Its commits: each id, its subject and the files it changed, by name with the lines added and removed | `git log --numstat <grew from>..<tip>` at the judgement, while the branch stands | **no** |
| The recovery ref, where one holds them | `ContentHolds.KeepDiscardedAsync`'s answer, or the landing's | the ref is; nothing points from the session to it |
| What its tree held that no commit holds | the last look's walk: each ignored path outside a producer's folder, by name and size | **no** |

**Never a copy of the tree**: no file's contents, no diff. A commit's content is in the repository, on the line or on its recovery
ref, and the record names the ref and the commit. Where the person wants the tree again, `git worktree add <path> <ref>` makes it
(the record says the command); the session's ignored scratch is named, not kept (§3.2).

### 2.2 Where the knowledge system keeps it

In `knowledge.db`, beside the session's own row, as a **record of its own**, not as an entry of the repository's feed (which a refresh
replaces whole, §1.3):

- **`work_records`**, one row per finished session: `session`, `workspace`, `repository`, `quest`, `title`, `landed` (branch, line, tip,
  at, hold), `ref`, `commits` (each id, subject, files), `left` (the named scratch), `at`, and a schema number. Written by the service's
  `SessionStore`'s connection, as the `sessions` table is, so the history clearing's transaction takes it with its session.
- **`work_records_fts`**, its full-text table, holding the record's words and the conversation's: the person's words and the agent's
  messages, never a tool's output, at most 256 KB of text per session (the rest is in `events.jsonl`, which the record names).
- **Searchable with the repository's records**: `knowledge_search` gains the kind `record` (`EntryKind.Record`), answered from
  `work_records_fts` beside `entries_fts`, in the same ranking, scoped by the same workspace and repository filters. A hit names
  `record:<session>` and the session's page, as a fix names its file and lines. The repository's own kinds are unchanged.
- **Machine-local.** A shared host never receives it (AGENTS.md: shared hosts exchange permitted records and answer no machine
  paths), and it is not part of a repository's feed or the session sync. Sync of records is not in this design.

**Written by** the driver at the look's judgement, before the session branch goes (§3.1 step 2): it reads the commits from git while
the branch stands, composes the record, and posts it to the service on loopback (`POST /api/sessions/{id}/record`, in the shape of
KEEP1b's `/api/sessions/{id}/resolved`). The service answers once the row and its text are committed. **No answer, no tree goes**: a
record the service did not take keeps the folder set aside under the days, as today (§3.1), and the next pass tries again.

**Kept as history.** The record cannot be made again. It is history (D195 point 7): cleared by the person's *Clear history…* with its
session, or by the age the person sets, never by the allowance (§4). Its bytes count under `history` on the card.

### 2.3 Which sessions get one

A session whose tree the look sets aside (`GoesByItself`, §1.1) and whose row names that tree (`Session.Tree`). A tree no session's
row names (a stray, `TreeStrays`; an opinion's copy or a replay's tree, `PassLeftovers`) gets none, and keeps D195's days (§3.3). Ask
Daoris's conversations have no tree, and their records are history as today.

## 3. When a finished tree goes

### 3.1 The rule

A finished session's tree goes **whole, an hour after the move**, where all of these hold:

1. **Its commits are held by a ref that stays**: the line by ancestry, a landing's branch (whose own removal writes its recovery ref,
   `LandedCleanups.cs:324-335`), or a recovery ref. **A carried tree gets a recovery ref at the move**, written by
   `ContentHolds.KeepDiscardedAsync` as a content hold's is (`TreeSweeps.cs:179-191`): today it gets none (§1.1's last row, and
   round 1 of the cleanup design, Codex 2.3). A ref that cannot be written keeps the branch, as for a content hold today.
2. **Its record is kept** (§2.2): the service answered the record's post before the branch went.
3. **Nothing was written in the folder since the move** (`TreeSetAside.WrittenSince`, `TreeSetAside.cs:547-593`), no link is in it and
   it reads (`Kept`, `:518-531`): every guard of the purge. A write that raced the move is what the hour waits for, as the slim's hour
   does today (`KeepKinds.SetAsideSettle`).

**Through the purge itself**: `PurgeSetAside(now, after: SetAsideSettle, spent, out stopped, only: the folders whose record is
kept)` (`TreeSetAside.cs:366`), so each guard holds and no removal is added. The folder's manifest goes with it. The look's report,
`tidied.purged` with `rule: record`, and `last.set-aside` say it.

**The folder records that it may go.** The move writes, beside the slim's manifest, `record: { session, ref }` (an added field of
`.slim.json`), set only once step 2 answered. The pass's `only` is the folders with that field. A folder without it waits the days.

### 3.2 What of a tree is kept, and for how long

**Nothing of its files, after its hour.** The record names what it held that no commit holds (§2.1's last row); that is all. This
is the owner's frame read whole: the repository holds the code, the knowledge system holds the text, and a tree is neither.

What this lets go that cannot be made again, said plainly for the reads: **a finished session's ignored scratch** (a probe, a dump, a
local database a program wrote in `bin/`), which the cleanup design §7 kept fourteen days for (*Deleting a tree's folder at the move,
whole*; *`bin/` in the slim, ever*). A session's working rules put its scratch in the repository's own ignored folders, and a
finding worth keeping goes into a tracked record before the session ends (`persist-working-state`). The record names each such path
and its size, so a person who wants one knows it existed. A person who wants finished trees kept longer sets `whole` (§3.3).

### 3.3 D195's days

- **A finished session's folder with its record kept**: an hour, as §3.1.
- **Any other folder set aside** (a stray, a leftover, one whose record the service did not take, one from before this build, one
  at the old place): its days, **14 by default**, unchanged, and the allowance (§4) may take it sooner, past its first day.
- **`whole`** changes meaning, under the same word: *keep a finished session's folder for the days, not an hour*. It is the person's
  opt-out from §3.1, the slim still off under it as today. A `driver.json` written by an older build that set `whole` reads the same.
- **The slim** stays for the folders that wait their days, and is moot for those that go at their hour.

**How much space this gives back**: at 10-23's rate, the set-aside folders stand at about an hour's moves (1 to 2 GB) rather than
fourteen days' (about 380 GB, held short only by the floor). On the install, the 77 folders at the new place (27.9 GB) were all
landed work (the row's measure: 151 trees, 136 of them this repository's, every one landed); this build gives each a record and lets it go at the pass after it
(§7 row 1's backfill).

## 4. The allowance

### 4.1 The rule

**Daoris may use up to N GB**, a standing choice the person sets. It is the sum of the card's kinds (the whole Daoris folder, and the
trees home where it lies elsewhere), counted by their bounded walks (`WiringSizes`, the cleanup design §4.3), never a walk of the
whole home at a pass.

Daoris keeps what it holds within it by **letting go of what can be made again or is only a copy, in this order**, each kind through
its one removal with every guard it has:

1. **Set-aside folders** (copies: the repository holds their commits): first the finished ones whose record is kept, past their hour;
   then any other past its **first day**, oldest by its move, through the purge with its `only` (round 1's settle of a day, kept for the
   folders §3.1 does not cover).
2. **The browser's caches** (made again by browsing): cleared together, only while the shell's browser host says it does not run,
   at the browser's start inside the host's start gate and at the daily pass (round 1's §2.4, kept).
3. **Unused downloads** (fetched again): oldest by the start of their unused stretch, past the hour's settle, through CACHECLEAR1's
   act; **never one whose rule is `never`**, nor one pinned, used or held by a running session (D195 point 6).

It **never** lets go of what cannot be made again: history and the records (§2.2), live trees and their build outputs (the cleanup
design §7: a session's workplace), sign-ins, settings, plugins and what they keep, the program, the service's copy, what a running
Daoris holds, the person's names. Where these alone hold more than N, Daoris says so (§4.4) and removes nothing more.

**Why this is not the rejected *one limit for the whole folder*.** D195 rejected a limit that took the oldest of anything by size,
since it would remove what cannot be made again. The allowance takes only the three kinds above, in the order of how cheaply they
come back, and says the rest. The rejection's reason stands, and its wording is amended (§8).

### 4.2 Its kinds, their parts, and the default

The allowance is **divided among the kinds**: each kind that may be let go has a **part**, the most it may hold even while the whole
is within N; the rest of N is what the kinds that cannot go may hold before Daoris says so.

| Kind | Part by default | The person may set | Over its part |
|---|---|---|---|
| `set-aside` | 25% of N | 1 GB to N | §4.1 step 1's units, oldest first, until under it |
| `browser-cache` | 5% of N | 1 GB to N | step 2 |
| `downloads` | 15% of N | 1 GB to N | step 3's units, oldest first |
| `logs` | none: its days, at most thirty files of 20 MiB a day (`MachineLog.cs:37, 40`) | — | — |
| everything else | the rest of N | — | said, never taken |

**The default N is 32 GB**, bounds 8 GB to half the Daoris folder's volume, as the floor's size is judged today
(`FloorPolicy.HalfGb`, `KeepKinds.cs:213`). Why 32: without the set-aside trees the install holds about 9 GB (§0), and a live tree of
another workspace's .NET service is about 3.5 GB, so six of them and the rest come to about 30 GB. The parts at the default are 8 GB,
1.6 GB and 4.8 GB, each above the install's measure (§0). There is no `off`: an allowance is the rule (the owner's direction), and the
person may raise it to half the volume.

### 4.3 When it is judged

- **At each Keeping pass**, after the purge and the slims (the cleanup design §3.1 steps 3 and 4), under the run lock and within the
  pass's 60 s budget, from the kinds' latest counts: a kind over its part, then the whole over N, through §4.1's order.
- **A size not counted is unknown, and the unknown keeps** (D195 §6): a kind never counted, or counted short, takes nothing beyond
  what its counted size already shows over. A set-aside folder's size is the manifest's `bytes`, written by the move and the slim (round
  1's §2.2), so the step needs no walk for it.
- **The counts** come from the kinds' walks at most once a day and after a run that wrote a `last` (KEEP1c), and after this step for
  the kinds it took from.

### 4.4 Over the allowance with nothing left that may go

The card's head says *Daoris holds 41 GB of its 32 GB* in the warn ink, with the kinds that hold it largest first, each with its door:
the live trees' *Clean up…* on a workspace's Branches and *Discard*, history's *Clear history…*, a plugin's removal. A row in *What needs
you* says it once each time the whole goes over (`space.over`), with *Free up space…* as its door, and goes when the whole is under N
by the smaller of 1 GB and 5% (`space.within`). This replaces the floor's row as the primary word (§6).

### 4.5 The card

```
 Disk space                                  Daoris uses 11.2 GB of its 32 GB   [Change…]  [Free up space…]
   The drive (D:) · 44 GB free of 1 TB · below 10% free
 Set-aside trees ⓘ        1.1 GB of 8 GB · 4 folders · counted 10:42
                          a finished session's tree goes an hour after it is set aside, once its record is kept; others after 14 days   [Change…]
                          next: 3 folders at 11:40 · last: 10:40, 6 removed, 1.2 GB
 Finished work's records  0.3 GB · 214 sessions · kept until you clear them        [Change…]
 The browser's cache ⓘ    0.8 GB of 1.6 GB · cleared when the browser starts over it   [Change…]
 Downloaded versions      1.4 GB of 4.8 GB · unused versions go after 7 days       [Change…]
 …
```

- **The allowance** is the head's line and its *Change…*, an editor of one size in GB with its bounds, beside *The default*.
- **Each part** is said *of N GB* on its row, and the row's *Change…* gains a size beside its days (round 1's §2.5: `KeepEditor`,
  `WiringSpace.tsx:84-160`).
- **History** is labelled *Conversations and records* today (KEEP1c); the records' bytes join it, and the row's words say what a
  record is.
- **The drive** is one line under the head, its words only (§6).
- Every new word is a key in both languages; the glossary gains *allowance* (*Daoris may use up to*, 允许占用) and *record* for a
  finished session's (工作记录).

### 4.6 The person's choice

`driver.json`'s `keep` gains `allowance` and a `gb` per kind with a part; absent is the default:

```
"keep": {
  "allowance":    { "gb": 32 },
  "setAside":     { "days": 14, "gb": 8 },
  "downloads":    { "days": 7, "gb": 4.8 },
  "browserCache": { "gb": 1.6 },
  "floor":        { "percent": 10 }
}
```

A part is stored as GB and read against N: a part above N reads as N. `KeepKind` gains `DefaultShare` and `MayHavePart`;
`KeepKinds.BrowserCache` joins the table as a kind with a part and no days. `KeepRules.Apply` judges the allowance and each part
against its bounds, refusing as `KEEP_OUT_OF_RANGE` with `context: gb`, as the floor's refusal does (D195's KEEP1f note). The doors
are KEEP1d's: `SET_KEEP` `{ kind: allowance | <kind>, gb }`, `daoris driver keep allowance <n>GB|default`, `daoris driver keep
<kind> <n>GB|default`, each *Change…*, and Ask Daoris's `keep` door, a proposal only. `KeepPolicy.Unread` keeps the most: no
allowance step runs on a `driver.json` that does not read.

## 5. The person's press

*Free up space…* (KEEP1e) stays the person's door at any time, listed first and then pressed, on the card's head and as `daoris-driver
space free`. Round 2 changes what it lists:

**It may remove**, each unit judged again right before it goes:
- what each rule takes now, the allowance's step among them (*over its part: 3 folders, 1.1 GB*);
- **a finished session's tree within its hour**, its record kept first (§3.1 steps 1 and 2 run at the press as at the pass), so the
  person need not wait the hour;
- any other set-aside folder before its date, listed with whether it holds files of its own, as today;
- unused downloads within their days, a `never` one included (the press is what `never` leaves to the person);
- the browser's caches, while the browser does not run;
- what an earlier build left, and a failed update's folder, as today.

**It may never remove**, and never lists as a unit:
- a live tree or anything in it (its doors are *Clean up…* and *Discard*, listed as doors);
- a set-aside folder whose commits no ref that stays holds (§3.1 step 1), or one reached through a link or that does not read;
- a download pinned, used or held by a running session;
- history, the records and the conversations (their door is *Clear history…*, listed as a door);
- sign-ins, keys, settings, plugins and what they keep, the program, the service's copy, the knowledge index, what a running Daoris holds;
- a recovery ref, a branch or anything in a registered repository (D88 and D102's rules, and `work recovery drop`, stay theirs);
- the person's names (`salvage/`), and anything outside the home.

A press a session's environment sends alone stays refused (D194 point 5), and a terminal's waits for the window's Confirm (KEEP1e).

## 6. The drive's free space: a warning only

- **The floor removes nothing.** `SpaceFloor.PassAsync`'s three acts (`SpaceFloor.cs:436-529`) go: the slims due, the unused downloads
  and the browser's caches are the allowance's to let go (§4.1), and the hour's slim stays the pass's own. What the floor freed early
  (`freedEarly`) goes from `SYSTEM_SPACE` and the card.
- **It says.** Below it, the card's drive line is in the warn ink and a row in *What needs you* says the drive's free space once each
  stretch (`space.low` … `space.ok`, as KEEP1f built them), naming Daoris's own use beside it, so a person sees whether Daoris or
  something else fills the drive, with *Free up space…* as its door.
- **Its default becomes 10% of the volume, at most 100 GB.** A warning that held a 1 TB drive at 98% used (round 1's §4) is too late;
  one at 410 GB free on a 4 TB drive is noise. 100 GB on 1 TB would have said it on 10-23 (79 GB free). The person's size, share and
  `off` stand as KEEP1f built them (`FloorPolicy`, `KeepKinds.cs:180-235`).
- `FloorPolicy.Said` drops *Daoris frees early what it can make again* for *Daoris tells you*.

## 7. The rows, in build order

Each is landable alone, with its proof seen failing first, and runs its own tests and the quick checks; the merge and the CI beside
run the rest. The first gives the owner the most space back.

- **KEEPSIZE1b — a finished session's record kept, and its tree let go an hour after the move** (driver, service, web words). §2.1 to
  §3.3 with the record's write and store but not yet its search: `work_records` and the post, written at the judgement before the
  branch goes; a carried tree's recovery ref; the manifest's `record`; the pass's purge of recorded folders at the hour; `whole`'s
  meaning; the history clearing taking the record with its session; the card's set-aside words. **Backfill** once: each folder
  already set aside at the new place whose session's row names it and whose commits a ref that stays holds gets its record from the
  disk (the landing's trace where one stands, else the line or the ref and the session's row) and goes at the next pass; one that
  cannot be read keeps its days. Proof: a `Process` class over real git, a landed tree's record kept with its commits, ref and
  scratch named, and its folder gone at the hour; a carried tree's ref written first; a service that does not answer keeping the
  branch's folder for its days; a write after the move keeping it; `whole` keeping it; the history clear taking the record; the
  backfill in both forms.
- **KEEPSIZE1a — the set-aside folders at every place of the trees entry** (driver; the defect, under D195 as it stands). Unchanged
  from round 1 but in its place: every reader reads `.tidied` under each of `TreeContext.TreesRoots`, so the days, the allowance and
  the card reach the old place. The install's old place was removed by hand, so it gives the owner nothing now and is needed before
  the allowance counts the set-aside kind (KEEPSIZE1d). Proof: round 1's.
- **KEEPSIZE1c — the records searchable** (service, MCP, web). §2.2's `work_records_fts`, `EntryKind.Record`, `knowledge_search`'s
  `record` kind beside the others in one ranking, a hit's `record:<session>` and the session page's link. Proof: a record found by a
  word of its conversation and by a commit's subject, scoped by workspace; a repository's refresh leaving records whole; the shared
  host never answering one.
- **KEEPSIZE1d — the allowance and its parts** (driver, modules, web). §4: `keep.allowance` and each part's `gb`, `KeepRules`'
  bounds and refusal on every door, the pass's step in §4.1's order through each kind's removal and `only`, the manifest's `bytes`,
  the unknown keeping, `space.over` and its row, the card's head and parts in both languages. Proof: each kind over its part going
  oldest first; the whole over N going in order and stopping at what cannot go, said; a `never` download and a running session's kept;
  an uncounted kind taking nothing; out-of-bounds refused with `context: gb`; the both-ways test over the new choices; the card's
  stories at wide, half and narrow in both languages.
- **KEEPSIZE1e — the browser's caches within their part at the browser's start** (driver, shell, web). Round 1's §2.4 under the
  part: `KeepKinds.BrowserCache`, the check inside the host's start gate and at the daily pass. Proof: round 1's.
- **KEEPSIZE1f — the floor as a warning** (driver, web). §6: the floor's acts removed, its default 10% at most 100 GB, its words and
  `SYSTEM_SPACE`'s fields. Proof: `SpaceFloorTests`' below case removing nothing and saying the row; the default on 1,024 GB and 4 TB;
  a set floor unchanged; the card's below story in both languages. After KEEPSIZE1d, so nothing the floor freed is left without a rule.
- **KEEP1a2** (open, D195) is narrowed: `obj/` matters only for folders that wait their days, and the measure stands as its evidence.

## 8. Rejected

- **Keeping a finished tree whole for its days** (D195 as built, and round 1): about 27 GB a day of copies of what the repository holds,
  kept for a scratch that a finished session's working rules already move into records.
- **Keeping a copy of the tree in the record** (a diff, an archive of tracked files, a pack): the repository holds the commits on the line
  or a recovery ref; the owner's direction is *never a copy of the tree*.
- **Keeping the scratch by name list *and* its bytes for a day** (round 1's one-day settle for every folder): the hour already answers
  the write that races the move (the purge's `WrittenSince`), and a day's bytes is 27 GB at the rate measured.
- **The record as entries of the repository's feed**: a refresh replaces a repository's entries whole (§1.3), so the record would go
  at the next scan; it is a table of its own beside the sessions'.
- **The record as a file under the home**, indexed by a scan of the home: a second writer of history beside the store, with its own
  clear, where the session's row already lives in `knowledge.db`.
- **Letting go of a tree before its record is kept, or before a ref holds its commits**: what is let go must be what can be made again
  or is a copy, and a tree is a copy only once both hold.
- **An allowance with `off`**: the owner's rule is the allowance; half the volume is the person's widest.
- **The allowance taking what cannot be made again, oldest first** (D195's *one limit for the whole folder*): kept rejected; it says.
- **The floor freeing space** (D195 point 4 as built): the drive's free space is at most a warning (the owner's direction); the
  allowance does the freeing.
- **Slimming an idle live tree** (the cleanup design §7): kept rejected. A live tree is bounded by the sessions, and its bytes count
  against the allowance and are said.
- **A part for the logs or the pipe's journal**: each is bounded by its own writer's days.
- **Records synced to the remote** in this design: a record holds a conversation's words, and what leaves a machine is the session
  sync's to decide; a later row may.

## 9. What this amends, once accepted

- **D195 point 3**: a finished session's folder goes whole an hour after the move, once its record is kept and a ref holds its commits;
  the slim stays for folders that wait their days.
- **D195 point 4 and the cleanup design §3.2**: the floor is a warning that removes nothing, 10% of a volume at most 100 GB by
  default; the allowance (§4) is the trigger.
- **D195 point 2 and the cleanup design §2.1 and §2.2**: `keep` gains `allowance` and each part's `gb`; `browser-cache` becomes a kind
  with a part; `whole` keeps a finished folder for its days.
- **D195 point 7 and the history clearing design §2**: a finished session's record is history, cleared with its session.
- **The cleanup design §3.4**: *anything that cannot be made again before its rule's time* reads with a finished tree's scratch let
  go at its hour once its record is kept.
- **The cleanup design §7**: *Deleting a tree's folder at the move, whole* and *`bin/` in the slim, ever* are superseded for
  finished folders with a record (§3); *One size limit for the whole Daoris folder* is reworded as §8's.
- **D88's AUTOTIDY1 and D102's SQUASHTIDY1c notes**: a carried tree's commits get a recovery ref at the move.
- **The knowledge index** (`EntryKind`): a kind `record`, from its own table.
