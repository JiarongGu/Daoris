# Keeping as a chat app does: a finished session kept as its text record, its tree let go, and Daoris within an allowance the person sets (proposed, round 3, 2026-10-24)

**Standing:** proposed, a note amending D195 (KEEPSIZE1), round 3. Round 1 (the same day, `d57b7f8d6`, merged at `16769207d`)
proposed a size limit per kind beside its days and the floor as a share; its §6 holds the proofs rows KEEPSIZE1a and 1e still take.
Round 2 (`a2993a3bb`) was rewritten to the owner's directions, carried in the row KEEPSIZE1 in `TASKS.md`:

- what is kept of finished work is its **text record**: the conversation, its notes, the landing and the commits' recovery refs,
  held in the knowledge system, indexed and searchable with the repository's records, never a copy of the tree the repository
  already holds;
- a completed session's **tree is cleaned up**, not set aside whole;
- Daoris manages **its own allowed space**: an allowance the person sets (*Daoris may use up to N GB*), divided among its kinds,
  keeping what it holds within it and letting go first of what can be made again or is only a copy;
- the drive's free space is **at most a secondary warning**, never the rule;
- the person can **clean up by hand at any time** on the *Disk space* card.

Round 2 was read by Codex (another round: 14 findings, 11 blocking) and by Claude Opus 5.5 in Fable's place (D191's 2026-10-23
amendment; accept after named changes: 22 findings, 4 blocking), their reads verbatim in `docs/2026-10-24-keep-size-reads.md`.
**Round 3 answers every finding of both** (§10), each checked against the code at `49cd7bdbc`. Its changes:

- **The owner's decision** on the Opus read's first blocking finding, asked on the ask that set this round (2026-10-24): a finished
  session's own scratch (the ignored files it wrote, its logs, probes and notes, which are not code) goes with its tree; what is worth
  keeping is committed or in its record (§3.2).
- **One eligibility judgement** every removal door reads (§3.4), from a manifest every finished tree gets (§3.3), and a recovery
  protocol for each step that can stop or be refused (§3.5).
- **The exact tip held**: a finished session's tip is kept on a recovery ref wherever the line's own branch does not hold it by
  ancestry, a squash's, a carried branch's and a fetch's form of the line included (§3.1).
- **The record keyed per tree**, naming every session that ran in it (§2.2), composed after the branch goes from the manifest's
  inputs, outside the repository's hold (§3.5).
- **The allowance takes only folders marked as having their record kept**, never a stray, a leftover, a refused record's or one from
  before this build; it has a stopping rule; its default is 48 GB, reasoned against its parts (§4).
- **One set of press bounds**, judged at the press (§5).
- **No backfill** (§7).

Nothing here is built. Driver paths are under `src/Daoris.Desktop/Daoris.Desktop.Driver/`, service paths under
`src/Daoris.Service/Daoris.Service.Core/`. Read for it: the row KEEPSIZE1 and KEEPSIZE1a in `TASKS.md`; D195 and the cleanup design
(`docs/2026-10-21-cleanup-design.md`: §1, §2, §3, §5, §7); the history clearing design (`docs/2026-10-07-history-clearing-design.md`
§2); D88's AUTOTIDY1 and AUTOTIDY2a and D102's SQUASHTIDY1c notes.

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
repository's facts changing (`TidyEffect.cs:34-52`): a finished tree goes aside within minutes of its work reaching the line. A tree
a session is in is `InUse` (`TreeSweeps.cs:302`), the sessions, their waits and reviews are asked again inside the repository's hold
(`AutoTidies.cs:291-294`; `BranchTidying.cs:143`), and Windows refuses the rename of a folder with a file open in it (`busy`,
`TreeSetAside.cs:245-265`). Uncommitted and untracked paths keep a tree (`TreeSweeps.cs:311`; `--untracked-files=all`,
`ContentHolds.cs:562-563`).

At that moment its commits are held by one of these (`TreeSweeps.JudgeAsync`, `TreeSweeps.cs:290-434`):

| Kind of hold | Read at | What keeps the session's own commits once the session branch goes |
|---|---|---|
| The line by ancestry, `refs/heads/<line>` | `:410-433` | the line itself |
| The line by ancestry, `refs/remotes/origin/<line>` alone | `:420` | a remote-tracking ref, which a fetch moves or prunes, so no hold the recovery refs' own rule accepts (`RecoveryRetention.cs:12-14`) |
| A landing's branch the line holds (AUTOTIDY2b) | `:425-431` | that branch; once the landing's clean-up removes it, a recovery ref at its tip where it was not merged (`LandedCleanups.cs:324-335`), a merged one's tip being on the line |
| A squashing landing's record | `:337-356` | **nothing of the session's own history**: the landing's branch holds a commit made from the session tip's tree with another parent (`TreeSquashes.cs:20-27`), and the record vouches only that the tip is inside a squashed tip (`TreeSquashes.cs:70-91`); a recovery ref later written for that branch keeps the squash, not the session's commits and messages |
| By content (`HeldBy`) | `:369-377` | a recovery ref written before the branch goes (`TreeSweeps.cs:179-191`, `ContentHolds.KeepDiscardedAsync`, `ContentHolds.cs:283-310`) |
| A completed pull request carried them (`Carried`) | `:361-365` | **nothing of this repository's**: `GoesByItself` takes the kind without `OnLine` (`TreeSetAside.cs:28`), and no ref is written for it on either the look's or the press's path (`TreeSweeps.cs:181` writes one only for `HeldBy`) |

The recovery refs are `refs/daoris/discarded/<branch>` (`ContentHolds.cs:106-109`), dated by their reflog (RECOVERYDATE1,
`:290-294`). One that a branch or a tag of the person's holds goes 30 days on; one nothing else holds is kept until the person drops it
(`RecoveryRetention.cs:6-29`, `RecoveryDropping.cs:19-60`). They are listed in the work inventory (`WorkInventories.cs:88`).

**The move** (`TreeSweeps.cs:193-209`, `TreeSetAside.cs:190-235`): the folder's name and moment are chosen (`AsideName`,
`:270-273`), a slim manifest is written `judged` beside it **only where the last look confirmed a package folder to drop**
(`:204`; under `whole` none, `AutoTidies.cs:271-277`), the folder is renamed to `trees/.tidied/<workspace>/<repository>/<tree>-<moment>`,
then `git worktree remove`, and the manifest marked `moved`. **Only then is the branch deleted** (`DeleteJudgedAsync`,
`TreeSweeps.cs:239`); where that delete refuses, the folder stays aside, the branch stands and a content hold's ref is dropped
(`:240-253`). Its ignored paths, the session's own scratch among them, go aside with it (`:312-314`), an ignored nested repository
too (`TreeSetAside.cs:104-113`; one git does not ignore keeps the tree). An hour later, where nothing was written since, its package
folders go (the slim, `KeepKinds.SetAsideSettle`, `KeepKinds.cs:23`); after the days (14 by default, `:17-20`) the purge takes the
folder, kept while anything in it was written or created after the moment in its name, while a link outside a build folder is in it, or
while it does not read (`TreeSetAside.cs:366-421, 518-593`; a link under a build folder goes as a link, `:585`).

Strays (a folder git lists no tree in, `TreeStrays.cs:12-13, 41-56`) and a pass's leftovers (`PassLeftovers.cs:20-25`) are moved into
the same place by `MoveFolderAside` alone, with no judgement of any commit and no manifest.

### 1.2 What a finished session's record is today, and where

| Part | Where | Kept by | Searchable |
|---|---|---|---|
| The session's row: quest, repository, state, `Note` and `NoteParts`, `Evidence`, `Tree`, `BaseCommit`, the person's words (`Said`) | the `sessions` table of `knowledge.db` (`Sessions/Sessions.cs:101-339, 457-622`), opened on the knowledge store's connection (`Composition/ServiceFactory.cs:296-298`) | history: the person's clear or age (KEEP1g), or the single delete (`SessionLedger.cs:1996-2003`); both through `Sessions.DeleteAsync` (`Sessions.cs:1522-1528`) | no |
| The quest: its publish, notes, decline reasons and the closing answer | `quests` and `quest_log` in `knowledge.db` (the history clearing design §2.1) | history | no |
| The conversation and its transcript | `data/sessions/<id>.events.jsonl` and `<id>.log` (`Sessions/History/SessionHomeFiles.cs:97-124`) | history | no |
| The landing: branch, line, tip, when | `data/landings.json` (`Trees/LandedBranches.cs:14-16`) | standing entries, and the newest 50 retired traces per repository by when each went (`:565-575`) | no |
| The branch it grew from, its first commit and its work unit | `data/session-branches.json` (`Trees/Branches/SessionBranches.cs:19-30, 70`) | standing entries, an open unit's traces, the trace following a live tree, and the newest 50 other retired traces per repository (`:232-248`) | no |
| Its commits and their messages | the session branch, then the line or a recovery ref (§1.1) | the line; a recovery ref | by git alone |
| Its files | the tree, then the set-aside folder | its days | no |

The session's row names no branch (it is `daoris/<tree folder>`, `SessionTrees.cs:354-356`) and no commit beyond its base. Several
rows can name one tree: a chain's successive sessions run in one tree (`Trace.Chain.cs:1150-1158`), and teammates and helpers share
one. So once its traces are retired and its folder's days end, nothing on this machine says which commits a finished tree made.

### 1.3 The knowledge system

`knowledge_search` reads one index: `EntryKind` is rule, knowledge, skill, decision, fix, task outcome and index
(`Knowledge/KnowledgeEntry.cs:7-34`, `KnowledgeQuery.cs:57-58`), every entry scanned from a repository's files
(`Registry/RepositoryScanner.cs:94-204`) and stored in `entries` with its full-text table (`Knowledge/Store/SqliteKnowledgeStore.cs:26`).
A repository's entries are **replaced whole** at each refresh (`ReplaceRepositoryAsync`, `SqliteKnowledgeStore.cs:152-168`, called
locally at `KnowledgeIndex.cs:41` and on the fed path at `KnowledgeService.cs:655`; a gone or retired repository emptied at
`KnowledgeIndex.cs:61`, `KnowledgeService.cs:357, 560`). The index is **derived data with no migrations**: an older schema drops
`entries` and `entries_fts` and rebuilds (`SqliteKnowledgeStore.cs:13-21, 112-117`). `knowledge_get` resolves from `entries` alone
(`KnowledgeService.cs:248`, `SqliteKnowledgeStore.cs:222`), and the hybrid tier's vectors cover `entries` alone
(`KnowledgeService.cs:180`). **No session, conversation or landing is indexed** today.

### 1.4 What each kind's rule is

| Kind | Its rule | Its bound |
|---|---|---|
| `set-aside` | the slim an hour after the move; the purge at 1 to 90 days, 14 by default; `whole` (`KeepKinds.cs:17-23`) | none |
| `downloads` | 0 to 365 days into a version's unused stretch, 7 by default, or `never` (`:31-37`) | none |
| `browser-cache` | the engine's own; cleared by the press and below the floor while the browser does not run (`SpaceFree`, `SpaceFloor.cs:502-529`) | none |
| `logs` | 7 to 30 days (`:44-47`) | 20 MiB a file, one file a day for each process that writes (`MachineLog.cs:37, 40, 349`) |
| `history` | the press, or an age the person sets (`:55-61`) | none |
| the floor | the smaller of 10% of a volume and 20 GB (`FloorPolicy.Of`, `KeepKinds.cs:216-222`); below it the slims due, unused downloads and the browser's caches go at once (`SpaceFloor.cs:311-529`) | — |

Every reader of the set-aside folders reads `TreesRoot/.tidied` alone (`TreeSetAside.cs:38`), though the trees entry has an old place
too (`DaorisHome.cs:379`): the purge, the slim, the floor, the press's list, the look's report and the card's walk
(`SystemSpace.cs:64-96`). A press's unit names its folder by workspace, repository and name and is resolved under one root
(`SpaceFree.cs:291-296`). Folders set aside before a home's layout moved are reached by none of them (round 1's §1.1; KEEPSIZE1a).

## 2. A finished tree's record

### 2.1 What it is

The record of a finished tree is **text**, and nothing of its tree but names:

| Part | Its source | Already kept? |
|---|---|---|
| The conversations: each session's person's words and agent's messages, in order, with their times | `sessions/<id>.events.jsonl` of every session the record names | yes, as history; the record indexes it, it does not move it |
| Its notes: each session's `Note` and `NoteParts`, the quest's closing answer and its requirements' answers | the sessions' rows; `quest_log` | yes |
| The landing: the branch it landed as, the line, when, and which hold of §1.1 kept the commits, in which form | the manifest (§3.3), from the judgement | **no** |
| The tip, the commit it grew from and its work unit | the manifest, from the judgement and `session-branches.json` | **no** once the trace retires |
| Its commits: each id, its subject and the files it changed, by name with the lines added and removed | `git log --numstat <grew from>..<tip>`, read from the tip while a ref holds it (§3.5 step 6) | **no** |
| Which session made which commits | each session's `BaseCommit`, **only where the bases bound the range** (a chain's later session based on its predecessor's tip); otherwise the record names the tree's commits and the sessions that ran in it, never an attribution it cannot read | **no** |
| The recovery ref, where one holds the tip | the manifest; looked up again when the record is read, since a ref may be dropped or retired (§1.1) | the ref is; nothing points from the session to it |
| What its tree held that no commit holds | the judgement's `TreeHolds`: `Ignored`, `Written` and `TooLarge` (`ContentHolds.cs:560-622`), by name, at most `TreeScratch.ScratchWalkLimit` (5,000) names, the rest counted; a build folder's contents (`TreeScratch.BuildFolders`) are the build's output and are not named | **no** |

**Never a copy of the tree**: no file's contents, no diff. A commit's content is in the repository, on the line or on a recovery ref,
and the record names the tip commit. Where the person wants the tree again, `git worktree add <path> <tip>` makes it while anything
holds the tip (the record says the command with the commit, and which ref holds it when read). The scratch is named, not kept (§3.2).

### 2.2 Where the knowledge system keeps it

In `knowledge.db`, beside the sessions' rows, as **records of their own**, not as entries of a repository's feed (which a refresh
replaces whole and a schema bump drops, §1.3):

- **`work_records`**, **one row per finished tree** (Opus finding 10): keyed by the tree's set-aside identity, `(workspace, repository,
  folder)`, where the folder is `<tree>-<moment>` (§1.1), so one tree reused by a chain's sessions is one record and a reopened
  session's second tree is another. It holds `unit` (the branch's work unit where `session-branches.json` names one, FABRIC-END2),
  `branch`, `line`, `hold` (kind and form), `tip`, `grewFrom`, `ref`, `commits`, `left` (the named scratch, its count and whether it
  was cut at the limit), `at`, and a schema number.
- **`work_record_sessions`**: `(record, session)` for every session whose row names the tree's path and that was made before the
  move. The service reads these from its own `sessions` rows when the record is posted.
- **`work_records_fts`**, its full-text table: the record's words and its sessions' conversations (the person's words and the agent's
  messages, never a tool's output), at most 256 KB of text per session (the rest is in `events.jsonl`, which the record names).
- **Kept, not derived** (Opus finding 13): `work_records` and `work_record_sessions` carry their own schema version and migrate; the
  index's rebuild never drops them. `work_records_fts` carries its own version and is rebuilt from `work_records` and the
  conversations' files.
- **Gone with its sessions**: `Sessions.DeleteAsync` (`Sessions.cs:1522-1528`), which both the history clear (`Asks/History.cs:303-323`)
  and the single delete (`SessionLedger.cs:1996-2003`) call inside their transaction, deletes the session's links, and a record left
  with no session in the same statement's transaction.
- **Machine-local.** A shared host never receives it (AGENTS.md: shared hosts exchange permitted records and answer no machine
  paths), and it is not part of a repository's feed or the session sync. Sync of records is not in this design.

**Written by** the service, on the driver's post on loopback (`POST /api/trees/record`, in the shape of KEEP1b's
`/api/sessions/{id}/resolved`) of the manifest's facts and the commits read from the tip. The service answers once the rows and their
text are committed. The post is made **after the branch went and outside the repository's hold** (§3.5), from the manifest alone, so
the next pass, the press and a restart compose the same post. Until it is answered the folder is not marked and nothing takes it
early (§3.4).

**Kept as history.** The record cannot be made again. It is history (D195 point 7): cleared by the person's *Clear history…* with its
sessions, or by the age the person sets, never by the allowance (§4). Its bytes are inside `knowledge.db`, counted once, under the
card's rest as today (`SystemSpace.cs:90-92`); the card says the records' count, not an added size (§4.5).

### 2.3 Which trees get one

A tree the look sets aside as finished (`GoesByItself`, §1.1), whose manifest was written (§3.3). A stray or a pass's leftover gets
none and no manifest (§1.1), and keeps D195's days (§3.4). Ask Daoris's conversations have no tree, and their records are history as
today.

## 3. When a finished tree goes

### 3.1 The exact tip held

**A finished session's tip is held by a ref that stays before its branch goes** (Codex finding 1, Opus claim 6 and finding 21): where
`refs/heads/<line>` holds the tip by ancestry, that is the hold; otherwise the sweep writes a recovery ref **at the tip** with
`ContentHolds.KeepDiscardedAsync`, as a content hold's is today (`TreeSweeps.cs:179-191`). That covers a squashing landing's record
(whose branch holds a squash, not the session's commits), a carried branch, a landing branch's hold and a line held only by
`origin/<line>`. It is written on **both** of `SweepAsync`'s paths, the look's and the person's *Clean up…* (`TreeSweeps.cs:181` serves
both). A ref that cannot be written keeps the branch and the tree, as a content hold's does today. Where the line later holds the tip, the
ref goes 30 days on by `RecoveryRetention`; where nothing else holds it (a squash's or a carried branch's), it is kept until the person
drops it, and the inventory says so (`WorkInventories.cs:88`).

This is one `update-ref` inside the repository's hold for the kinds that had none (KEEP1a kept that hold free of new git, Fable F1.5):
it must precede the branch's delete, which is inside the hold, so it cannot move out.

### 3.2 What of a tree is kept, and for how long

**The owner's decision** (asked on the ask that set round 3, 2026-10-24, on the Opus read's first blocking finding): **a finished
session's own scratch, the ignored files it wrote, its logs, probes and notes, which are not code, goes with its tree. What is worth
keeping is committed or in its record.**

So nothing of a finished tree's files is kept after its hour: the repository holds the code, the knowledge system holds the text, and a
tree is neither. The record names what it held that no commit holds (§2.1's last row), so a person who wants a path knows it existed.
This reverses, for finished trees with a record, the cleanup design §7's *Deleting a tree's folder at the move, whole* and *`bin/` in the
slim, ever*, and D88's AUTOTIDY2a ground that the look takes a tree with ignored paths "since the look moves its folder aside for
fourteen days" (`TreeSetAside.cs:16-21`, `TreeSweeps.cs:312-313`); D195 point 6 (*only what is known to be made again goes by itself
before its time*) reads with a finished tree's scratch let go at its hour by the owner's word (§9). A person who wants finished trees
kept longer sets `whole` (§3.4).

### 3.3 The manifest every finished tree gets

**The move writes a manifest for every finished tree** (Opus finding 7), its slim paths possibly empty, `whole` included; the slim
manifest becomes **schema 2**, beside the folder as today (`SlimManifest.cs:6-11`). An older build reads no manifest of another schema
(`:13-15`) and purges the folder by its days, which keeps more of a folder awaiting its record than this build would, never less.

| Field | What | Written at |
|---|---|---|
| `schema`, `state` | 2; `judged`, `moved`, `refused`, `deleted`, `recorded` (§3.5) | each step |
| `read` | the moment before the judgement's read of what the tree holds (`ContentHolds.HoldsAsync`, `TreeSweeps.cs:308`) | `judged` |
| `judged`, `moved` | as today | as today |
| `workspace`, `repository`, `tree` (its path before the move), `branch`, `line` | the sweep's item | `judged` |
| `tip`, `grewFrom`, `unit` | the commit judged (`TreeSweeps.cs:158`); `session-branches.json`'s entry for the branch | `judged` |
| `hold` | `{ kind: line \| landing \| squash \| content \| carried \| empty, where, form }`, from the judgement | `judged` |
| `ref` | the recovery ref at the tip, or none where `refs/heads/<line>` holds it (§3.1) | `judged` |
| `left` | the judgement's `Ignored`, `Written`, `TooLarge`, bounded as §2.1 says | `judged` |
| `paths` | the package folders to slim, as today; empty where none or under `whole` | `judged` |
| `whole` | whether `whole` was set at the move | `judged` |
| `deleted`, `recorded` | when the branch's delete succeeded; when the service answered the record's post | those steps |

**A manifest that cannot be written keeps the tree** (Codex finding 6): no move, the branch and the tree stand, kept `unread`, and the
next look tries again; today the move goes on whole without one (`TreeSetAside.cs:204, 280-296`). A manifest never names a session: the
service resolves those from its rows (§2.2).

### 3.4 Which folder may go, by which door: one judgement

Every removal of a set-aside folder (the hour's purge, the days' purge, the allowance, the person's press) reads **one judgement**,
`SetAsideEligibility`, of the folder's class and the guards, at the moment it removes (Codex's central finding; Opus findings 2 and 3).

**The classes**, from the manifest:

| Class | What it is |
|---|---|
| **recorded** | a finished tree at `recorded`: its branch deleted, its record kept |
| **awaiting record** | a finished tree at `deleted`: its branch deleted, its record not yet kept |
| **stopped between steps** | a finished tree at `judged` or `moved` (§3.5) |
| **refused** | a finished tree at `refused`: the move done, the branch's delete refused, the branch standing |
| **unmarked** | no manifest, one of schema 1, or one that does not read: a stray, a pass's leftover, a folder from before this build, at either place of the trees entry |

**The guards every door keeps**, judged again right before the removal: a link on the way to the folder; a link in it outside a build
folder; a folder that does not read; anything in it written or created after its threshold, which is the manifest's `read` for a
finished tree (§3.5's race) and the moment in its name for any other (`TreeSetAside.cs:518-593`).

**The holding check** for `recorded`, judged again right before the removal (Codex finding 3): the branch is absent, and the
manifest's `tip` is an ancestor of the manifest's `ref`, or of `refs/heads/<line>` where `ref` is none. Where it fails (the person
dropped the ref, the line was rewound), the folder is **unmarked** from then on: it waits its days and says why.

| Class | The hour's purge | The days' purge (D195, 14 by default) | The allowance (§4) | The press (§5) |
|---|---|---|---|---|
| recorded, the holding check passing, `whole` not set | **at its hour** | — | yes | yes |
| recorded, `whole` set at the move or now | no | at its days | **no** (Opus finding 17) | yes |
| awaiting record | no | **no**: it waits for its record, said on the card | no | yes, its record posted first; where the service does not take it, listed *its record is not kept* and removed only as listed |
| stopped between steps | no | resumed (§3.5), else at its days | no | yes, listed as what it is |
| refused | no | at its days (the branch holds its commits) | no | yes |
| unmarked | no | at its days, as built | **never** | yes, listed |

**Why the days' purge stays for unmarked folders and the allowance does not** (Codex finding 4): the days are D195's standing rule,
built for folders whose content Daoris cannot vouch for; a stray is moved aside "so nothing in it is lost before the purge's fourteen
days" (`TreeStrays.cs:41-56`). This design does not shorten that, and the allowance, which takes only what is a copy, takes none of
them. An awaiting-record folder is held past its days, since the record is what the owner's frame keeps of it and the folder is the
one place its scratch's names and its unread inputs still are.

**One purge call** (Opus finding 8): the hour's purge and the days' purge are one `PurgeSetAside` call, its `after` a function of the
folder's class, so `purge-failed.json` keeps every folder's failure and the day's wait after a repeated one engages.

**Renamed before it is deleted** (Opus finding 19): a folder that goes is renamed first into `.tidied/.going/`, as the press moves an
earlier build's folders aside first (`SpaceFree.cs:127`); a refused rename keeps it whole, `busy`, and nothing is half deleted under
the name a person opened. A folder left in `.going/` by a stop is deleted by the next pass.

### 3.5 The steps, and what each stop or refusal leaves

Inside the repository's hold, at the look's sweep (`AutoTidies.cs:291-297`, `TreeSweeps.cs:150-275`):

1. **Judge** as today, taking the moment `read` first (`TreeSweeps.cs:308`). A tree holding uncommitted paths is kept, as today.
2. **Hold the tip** (§3.1). Refused: nothing moves; the branch and the tree stand.
3. **Write the manifest `judged`** (§3.3). Not written: the ref of step 2 is dropped as a refused move's is (`TreeSweeps.cs:203`),
   nothing moves, kept `unread`.
4. **Move and let git go** as today, then mark `moved`. Refused: the manifest is forgotten, the ref dropped, the folder put back, as
   today (`TreeSetAside.cs:213-234`).
5. **Delete the branch** at the commit judged (`DeleteJudgedAsync`, `TreeSweeps.cs:239`). Succeeded: mark `deleted`, then the record's
   inputs are complete. **Refused: mark `refused`**; the branch stands and holds the commits, so the folder is a copy of a branch that
   stands and waits its days, never armed (Opus finding 5, Codex finding 3). A content hold's ref is dropped as today (`:242`), and
   the tip ref of step 2 likewise.

After the hold, outside it (Opus finding 18):

6. **Compose and post the record** from the manifest: `git log --numstat <grewFrom>..<tip>` (read from the tip, which the ref or the
   line holds), and the post of §2.2. Answered: mark `recorded`. Not answered, or refused: it stays `deleted` and every later pass, the
   press, or a restart composes the same post again from the manifest (Opus finding 6). Where the tip no longer reads (the ref dropped
   by hand), the record is posted without its commits, saying so.
7. **The hour's purge** takes a `recorded` folder past its hour by §3.4's judgement.

**The race the hour does not cover** (Codex finding 2): a write after the judgement's read and before the folder's name was chosen
predates the moment in its name, so the move's moment cannot be the threshold. A finished folder's threshold is `read` (§3.4): anything
written or created in it after the judgement began reading keeps it, and anything written before was seen by that read, which keeps a
tree holding an uncommitted path (`TreeSweeps.cs:311`). The judgement's own read writes nothing in the tree (`--no-optional-locks`,
`ContentHolds.cs:562`), and the rename and `git worktree remove` of the moved path write nothing inside it. A folder so kept is said
*written after it was judged*, waits its days, and is the press's.

**Resuming a stop**, at each pass before the purge, for each finished folder not at `recorded`:
- `judged`: the process stopped before or during the move. Unknown, as today (`SlimManifest.cs:11-13`): kept whole for its days.
- `moved`: stopped before the delete. Where the branch is absent and the holding check passes, mark `deleted` and go on at step 6;
  where the branch stands, the next look judges it again (its tree's folder is gone, `TreeSweeps.cs:304-305`), and its delete marks
  this manifest `deleted` by its `branch` and `tip`; otherwise the folder waits its days.
- `deleted`: step 6.

### 3.6 D195's days, and how much space this gives back

- **A recorded folder**: an hour, as §3.4.
- **Any other**: its days, **14 by default**, unchanged; an awaiting-record folder waits its record (§3.4). The allowance takes none of
  them.
- **`whole`** changes meaning, under the same word: *keep a finished session's folder for the days, not an hour*. The record is kept
  under it all the same. A `driver.json` written by an older build that set `whole` reads the same.
- **The slim** stays for the folders that wait their days, and is moot for those that go at their hour.

**How much space this gives back** (restated, Codex finding 14; the owner's decision on scratch does not change it, since no finished
tree waits for its scratch): today a set-aside folder waits its fourteen days, bounded by nothing else (the floor slims package folders,
which the move's slim has already taken, and frees downloads and caches, not the folders), so at 10-23's rate the kind would stand at
about 380 GB. With the hour, it stands at about an hour's moves (1 to 2 GB) **plus every folder that is not recorded**, at its days.
On the install that is nearly every folder: the row's count was 151 set-aside trees, every one landed work; how many of each day's moves
are strays, refused or awaiting a record the install's look will say once 1b counts its classes. The 77 folders at the new place
(27.9 GB) are from before this build: unmarked, they go by their days, by 11-06, or by the person's press now (§7: no backfill).

## 4. The allowance

### 4.1 The rule

**Daoris may use up to N GB**, a standing choice the person sets. It is the sum of the card's kinds (the whole Daoris folder, and the
trees home where it lies elsewhere), counted by their bounded walks (`WiringSizes`, the cleanup design §4.3), never a walk of the
whole home at a pass. The records count once, inside `knowledge.db` (§2.2).

Daoris keeps what it holds within it by **letting go of what is only a copy or can be made again, in this order**, each kind through
its one removal with every guard it has, and only units §3.4 makes eligible:

1. **Recorded set-aside folders** (copies: a ref holds their tip and the knowledge store holds their record), past their hour, oldest by
   their move, not under `whole`. **Never a stray, a leftover, a refused or awaiting record's folder, or one from before this build**
   (Opus finding 2): those are counted and said (§4.4), with the press as their door.
2. **The browser's caches** (made again by browsing): cleared together, only while the shell's browser host says it does not run
   (`BrowserCacheClearing`, `Platform/BrowserCaches.cs:24-33`).
3. **Unused downloads** (fetched again): oldest by the start of their unused stretch, past the hour's settle, through CACHECLEAR1's
   act; **never one whose rule is `never`**, nor one pinned, used or held by a running session (D195 point 6).

It **never** lets go of what cannot be made again: history and the records, live trees and their build outputs (the cleanup design §7: a
session's workplace), sign-ins, settings, plugins and what they keep, the program, the service's copy, what a running Daoris holds, the
person's names.

**Why this is not the rejected *one limit for the whole folder*.** D195 rejected a limit that took the oldest of anything by size,
since it would remove what cannot be made again. The allowance takes only the three kinds above, only their eligible units, in the order
of how cheaply they come back, and says the rest. The rejection's reason stands, and its wording is amended (§8).

### 4.2 Its parts, and the default

Each kind that may be let go has a **part**, the most it may hold even while the whole is within N. The rest of N is what the kinds
that cannot go may hold before Daoris says so.

| Kind | Part by default, at N = 48 | Its default at another N | The person may set |
|---|---|---|---|
| `set-aside` | 8 GB | `max(1, ⌊8 × N / 48⌋)` GB | 1 GB to N, in whole GB |
| `downloads` | 5 GB | `max(1, ⌊5 × N / 48⌋)` GB | 1 GB to N, in whole GB |
| `browser-cache` | 2 GB | `max(1, ⌊2 × N / 48⌋)` GB | 1 GB to N, in whole GB |
| `logs` | none: its days, at most one 20 MiB file a day for each process that writes (`MachineLog.cs:37, 40, 349`) | — | — |
| everything else | the rest of N | — | said, never taken |

**Whole GB** (Opus finding 22, Codex finding 7): N and each part are whole GB, as the floor's size is (`KeepChoices.FloorGb`,
`KeepChoices.cs:40`); no stored value is a fraction. **The parts' sum is at most N**: a door refuses a part that would bring the parts the
person set, with the others at their defaults for that N, above N, and refuses an N below the sum of the parts the person set, as
`KEEP_OUT_OF_RANGE` with `context: gb`, naming the parts. **A value in the file that breaks its bounds is not read**, as KEEP1d reads
every stored rule (`KeepChoices.cs:13`): an N above half the volume reads as the default; parts are read in the table's order, and a
part that would bring the sum above N is not read and keeps its default.

**The default N is 48 GB**, bounded by half the Daoris folder's volume (`FloorPolicy.HalfGb`, `KeepKinds.cs:213`); the person may set
8 GB to half the volume (where half is under 8 GB, half is both bounds). **Why 48** (Opus finding 4): what cannot go is about 30 GB in
the sizing case (six live trees of another workspace's .NET service at about 3.5 GB each, and the rest of what the install holds that
cannot go, about 7 GB of §0's 9 GB once the downloads and the browser's caches are taken out), and the parts at the default are 15 GB, so
45 GB holds both with a margin. At round 2's 32 GB the parts (14.4 GB) left 17.6 GB for what cannot go, which that case is over for good.
Against the install's measure (§0): the downloads (1.4 GB) and the browser's caches (0.77 GB) are under their parts; the set-aside
folders (27.9 GB) are over theirs, but every one is unmarked and none is the allowance's (§3.6), and with the hour the recorded folders
stand at about 1 to 2 GB. There is no `off`: an allowance is the rule (the owner's direction), and the person may raise it to half the
volume.

### 4.3 When it is judged, from which counts

At each Keeping pass, after the purge and the slims (the cleanup design §3.1 steps 3 and 4), under the run lock and within the pass's
60 s budget:

1. **Each part.** A kind over its part lets go of its eligible units, oldest first, until it is within its part or none is left.
2. **The whole: the stopping rule** (Opus finding 4, Codex §2). Where the whole is still over N, the pass sums every unit step 1 of §4.1
   to step 3 may take now. **Only where taking all of them would bring the whole within N** does it take them, in §4.1's order, until
   it is within N. Otherwise it takes nothing beyond the parts' overage and says so (§4.4): emptying the caches and the unused
   downloads with no hope of reaching N gains nothing and costs a fetch. A unit that a guard keeps (the browser running, `never`, a
   settle not past, a folder unread or not recorded) is not counted as one it may take. It is judged again at each pass, so a change
   (the browser stopping, a live tree removed) is met at the next. **No hard ceiling is promised**: what cannot go keeps growing while
   its writers write.

**Which counts** (Codex finding 8, Opus finding 16):
- **A count is fresh for a kind** where it was measured after the kind's last change Daoris made: a move into `.tidied` changes both
  `trees` and `set-aside`, a purge or slim `set-aside`, a clearing its kind. A destructive step reads only fresh counts. A kind whose
  count is not fresh takes nothing beyond its part's overage as last measured less what this pass removed, and asks a recount of that
  kind at once (`SystemSpace.StartDue`, `SystemSpace.cs:199-205`, with the kind named), so the next pass judges it.
- **Within a pass**, each removal subtracts the bytes it reports (the purge's `Bytes`, the press's and the clearings' bytes) from its
  kind's count before the next step reads it, so a purge earlier in the pass prevents a further removal it made unneeded.
- **A set-aside folder's size** is the set-aside walk's per-folder size (`SpaceFree.cs:153-156` reads `Sizes.Of(home,
  "set-aside").Folders` today; Opus finding 7), never a walk inside the repository's hold. A folder newer than the walk is unknown: it
  counts as at least nothing and is not taken by the whole's step until counted. **A size not counted is unknown, and the unknown
  keeps** (D195 §6). The card shows *at least* where a kind holds an unknown.

### 4.4 Over the allowance, or over a part, with nothing left that may go

The card's head says *Daoris holds 51 GB of its 48 GB* in the warn ink, with the kinds that hold it largest first, each with its door:
the live trees' *Clean up…* on a workspace's Branches and *Discard*, history's *Clear history…*, a plugin's removal, and the set-aside
folders the allowance may not take, by class, with *Free up space…*. A row in *What needs you* says it once each time the whole goes
over (`space.over`), with *Free up space…* as its door, and goes when the whole is under N by the smaller of 1 GB and 5%
(`space.within`). This replaces the floor's row as the primary word (§6).

**A part over with the whole within N** (Codex finding 13) says itself on its kind's row, not in *What needs you*: *over its part: the
browser is running; cleared when it stops*; *over its part: 4 folders wait their days or their record*; *`never` keeps 3 versions*,
with the next chance where one is known.

### 4.5 The card

```
 Disk space                                  Daoris uses 11.2 GB of its 48 GB   [Change…]  [Free up space…]
   The drive (D:) · 44 GB free of 1 TB · below 10% free
 Set-aside trees ⓘ        1.1 GB of 8 GB · 4 folders · counted 10:42
                          a finished session's tree goes an hour after its record is kept; others after 14 days   [Change…]
                          2 waiting for their record · 1 written after it was judged
                          next: 3 folders at 11:40 · last: 10:40, 6 removed, 1.2 GB
 Conversations and records  0.2 GB · 214 records · kept until you clear them (records counted in the rest)   [Change…]
 The browser's cache ⓘ    0.8 GB of 2 GB · cleared when the browser is not running and over its part   [Change…]
 Downloaded versions      1.4 GB of 5 GB · unused versions go after 7 days       [Change…]
 …
```

- **The allowance** is the head's line and its *Change…*, an editor of one whole size in GB with its bounds, beside *The default*.
- **Each part** is said *of N GB* on its row, and the row's *Change…* gains a whole size beside its days (round 1's §2.5: `KeepEditor`,
  `WiringSpace.tsx:84-160`).
- **The words come from the effective policy** (Codex finding 13): `whole` says *finished trees kept 14 days*; a history age says its
  age; the classes waiting say themselves; an unknown size says *at least*.
- **History** is labelled *Conversations and records* (KEEP1c); its size is the sessions' and workflows' folders as today
  (`SystemSpace.cs:81-82`), and the records are said by count.
- **The drive** is one line under the head, its words only (§6).
- Every new word is a key in both languages; the glossary gains *allowance* (*Daoris may use up to*, 允许占用) and *record* for a
  finished tree's (工作记录).

### 4.6 The person's choice

`driver.json`'s `keep` gains `allowance` and a `gb` per kind with a part; absent is the default:

```
"keep": {
  "allowance":    { "gb": 48 },
  "setAside":     { "days": 14, "gb": 8 },
  "downloads":    { "days": 7, "gb": 5 },
  "browserCache": { "gb": 2 },
  "floor":        { "percent": 10 }
}
```

`KeepKind` gains `DefaultPartGb` and `MayHavePart`; `KeepKinds.BrowserCache` joins the table as a kind with a part and no days, in row
1d alone (Codex finding 12, Opus finding 22). `KeepRules.Apply` judges the allowance and each part against §4.2's bounds, refusing as
`KEEP_OUT_OF_RANGE` with `context: gb`, as the floor's refusal does (D195's KEEP1f note). The doors are KEEP1d's: `SET_KEEP`
`{ kind: allowance | <kind>, gb }`, `daoris driver keep allowance <n>GB|default`, `daoris driver keep <kind> <n>GB|default`, each
*Change…*, and Ask Daoris's `keep` door, a proposal only. `KeepPolicy.Unread` keeps the most: no allowance step runs on a `driver.json`
that does not read.

## 5. The person's press

*Free up space…* (KEEP1e) stays the person's door at any time, listed first and then pressed, on the card's head and as `daoris-driver
space free`. **Its bounds are one set, judged at the press** (Opus finding 3): each unit is judged again by §3.4 right before it goes, and
one changed since the list is kept and counted, as today.

**It may remove:**
- what each rule takes now, the allowance's step among them (*over its part: 3 folders, 1.1 GB*);
- **any set-aside folder at any age**, as today's early group lists it (`SpaceFree.cs:163`; `SetAsideKeeping.RemoveEarly`,
  `SetAsideKeeping.cs:78-99`), each listed with its class and what the list can say of it: *recorded, its tip on `<ref>`*; *its
  record is not kept*; *its branch stands*; *a folder from before this build: what holds its commits is not known*; and *holds files
  written after it was judged* where it does. A finished folder awaiting its record has its record posted first, from its manifest; a
  folder the list said one thing of and that reads another now is kept, `changed`;
- unused downloads within their days, a `never` one included (the press is what `never` leaves to the person);
- the browser's caches, while the browser does not run;
- what an earlier build left, and a failed update's folder, as today.

**It never removes**, and lists only as a door:
- a live tree or anything in it (*Clean up…* and *Discard*);
- history, the records and the conversations (*Clear history…*);
- a recovery ref, a branch or anything in a registered repository (D88's and D102's rules, and `work recovery drop`, stay theirs);
- a download pinned, used or held by a running session; sign-ins, keys, settings, plugins and what they keep, the program, the
  service's copy, the knowledge store, what a running Daoris holds; the person's names (`salvage/`); anything outside the home.

Round 2's *never a set-aside folder whose commits no ref holds* is no longer a bound but a listed fact: for a folder whose branch went
before this build, the press has no tip to judge (`SlimManifest.cs:21-22` keeps `Branch` and no commit), and the person's word on a
listed folder is what removes it. A folder reached through a link or that does not read stays kept, as today.

A press a session's environment sends alone stays refused (D194 point 5), and a terminal's waits for the window's Confirm (KEEP1e).

## 6. The drive's free space: a warning only

- **The floor removes nothing.** `SpaceFloor.PassAsync`'s three acts (`SpaceFloor.cs:436-529`) go: the slims due, the unused downloads
  and the browser's caches are the allowance's to let go (§4.1), and the hour's slim stays the pass's own. What the floor freed early
  (`freedEarly`) goes from `SYSTEM_SPACE` and the card.
- **It says.** Below it on **each volume Daoris writes to**, the trees home's own where it lies on another (Codex §3 row 1f), the card's
  drive line is in the warn ink and a row in *What needs you* says that volume's free space once each stretch (`space.low` …
  `space.ok`, as KEEP1f built them), naming Daoris's own use beside it, so a person sees whether Daoris or something else fills the
  drive, with *Free up space…* as its door.
- **Its default becomes 10% of the volume, at most 100 GB.** A warning that held a 1 TB drive at 98% used (round 1's §4) is too late;
  one at 410 GB free on a 4 TB drive is noise. 100 GB on 1 TB would have said it on 10-23 (79 GB free). The person's size, share and
  `off` stand as KEEP1f built them (`FloorPolicy`, `KeepKinds.cs:180-235`).
- `FloorPolicy.Said` drops *Daoris frees early what it can make again* for *Daoris tells you*.

## 7. The rows, in build order

Each is landable alone, with its proof seen failing first, and runs its own tests and the quick checks; the merge and the CI beside
run the rest. The first gives the owner the most space back. **No backfill** (Opus finding 11, Codex finding 6): a folder set aside
before this build has no git record of its own (`git worktree remove` dropped it, `TreeSetAside.cs:215`), no tip in its manifest
(`SlimManifest.cs:21-22`), a trace only among the newest 50, and, where carried, no ref; none of §3.4's facts can be proven of it. Its
gain is one-off: the install's 27.9 GB, which the person's press frees now and whose days end by 11-06.

- **KEEPSIZE1b — a finished tree's record kept, and its tree let go an hour after** (driver, service, web words). §2 and §3 with the
  record's write and store but not yet its search: the tip's recovery ref on both paths (§3.1); the schema-2 manifest for every
  finished tree, a manifest not written keeping the tree (§3.3); `SetAsideEligibility` read by the hour's purge, the days' purge and the
  press (§3.4); one purge call with `after` per class; the rename before the delete; the steps, the `read` threshold and the resume
  (§3.5); `work_records`, `work_record_sessions` and their migration, the post and its retry; `Sessions.DeleteAsync` taking the record;
  `whole`'s meaning; the card's set-aside words and its classes. Proof: a `Process` class over real git:
  - a landed tree's record kept with its commits, tip, ref and named scratch, its folder gone at the hour, renamed first;
  - a multi-commit session squashed at its landing: the session's own commits and messages read from the tip's ref after both the
    session branch and the squash's landing branch are removed (Codex finding 1);
  - a carried tree's ref written first, on the look's path and on the press's (Opus finding 21); a line held only by `origin` likewise;
  - a tree with no package folder getting its manifest and going at its hour (Opus finding 7);
  - a write after the judgement's read and before the move keeping the folder (Codex finding 2);
  - a branch delete refused after the move: `refused`, never armed, kept its days (Opus finding 5);
  - a service that does not answer: `deleted`, kept past its days, the next pass's retry recording it from the manifest alone, and the
    press posting it first (Opus finding 6);
  - a stop after each step resumed as §3.5 says; a manifest not written keeping the tree and the branch;
  - a ref dropped by hand making a recorded folder unmarked;
  - a stray and a leftover never purged at the hour, and both purges' failures kept in `purge-failed.json` (Opus finding 8);
  - a chain's two sessions in one tree giving one record naming both, attributed by their bases; a session based elsewhere named and
    not attributed (Opus finding 10, Codex finding 6);
  - `whole` keeping the folder its days with its record kept;
  - the history clear and the single delete taking the record with its last session (Opus finding 13);
  - a file held open keeping the folder whole, `busy` (Opus finding 19).
- **KEEPSIZE1a — the set-aside folders at every place of the trees entry** (driver; the defect, under D195 as it stands). Every reader
  reads `.tidied` under each of `TreeContext.TreesRoots`, so the days, the allowance and the card reach the old place, and **a press's
  unit carries which place it is at** through listing, selection, removal, sizing and reporting (Codex finding 10; `SetAsideListed`,
  `SetAsideKeeping.cs:254`; `SpaceFree.cs:291-296`). The install's old place was removed by hand, so it gives the owner nothing now,
  and is needed before the allowance counts the set-aside kind (KEEPSIZE1d). Proof: round 1's §6 at `d57b7f8d6`, a home with a folder
  at each place both purged at their days, listed and counted under `set-aside`; and two folders of the same relative name and
  manifest, one at each place, each selected and removed alone.
- **KEEPSIZE1c — the records searchable and readable** (service, MCP, web). §2.2's `work_records_fts`, `EntryKind.Record`, and:
  - **one ranking** (Opus finding 14): the two full-text tables' hits merged by each one's rank (reciprocal rank, the bm25 scores never
    compared); the hybrid tier's vectors cover `entries` alone, so a record ranks by its words;
  - **a no-kinds search capped**: at most 3 records, as `IndexEntriesUnasked` caps the index's entries (`KnowledgeService.cs:186-190`);
  - **who receives one**: a `record` hit holds the person's words, so it is answered only on this machine, to the window and to sessions
    of the record's workspace; the shared host never;
  - **the complete record read** (Codex finding 11): `knowledge_get` resolves `record:<workspace>/<repository>/<folder>` from
    `work_records`, scoped as the search is, and the session page shows its tree's record: landing, tip, ref looked up now, commits,
    the named scratch;
  - **records written before 1c** indexed from `work_records` and the conversations' files at its first open.
  Proof: a record found by a word of its conversation and by a commit's subject, scoped by workspace; search → read → clear, both forms
  gone; a repository's refresh and an index rebuild leaving records whole; a 1b record indexed at 1c's first open; a no-kinds search
  not crowded; the shared host never answering one.
- **KEEPSIZE1d — the allowance, its parts and the browser's kind** (driver, modules, web). §4: `keep.allowance` and each part's `gb`,
  `KeepRules`' bounds and refusal on every door, `KeepKinds.BrowserCache` with its stopped-browser clearing at the daily pass (Codex
  finding 12), the pass's steps in §4.3's order through each kind's removal and §3.4, the counts' freshness and subtraction,
  `space.over` and its row, a part's own words, the card's head and parts in both languages. Proof:
  - each kind over its part going oldest first, eligible units only; a stray, an unmarked folder and an awaiting-record folder past their
    first day over the part kept and said (Opus finding 2); a `whole` folder kept (Opus finding 17);
  - the whole over N going in order where every eligible unit would reach N; **what cannot go over N taking only the parts' overage**,
    said (Opus finding 4);
  - a purge earlier in the pass preventing a further removal; a stale count taking nothing beyond its part and asking a recount; a tree
    moved since its count not counted twice (Codex finding 8, Opus finding 16);
  - a `never` download and a running session's kept; the browser running keeping its caches; an uncounted kind taking nothing;
  - the arithmetic (Codex finding 7, Opus finding 22): defaults at N = 8 and on a volume whose half is under 48 GB; a part refused above
    N; an N refused below the set parts' sum; a file whose parts' sum is above N reading the last as its default; the both-ways test
    over the new choices;
  - the card's stories at wide, half and narrow in both languages, with a part over and the whole within, and *at least*.
- **KEEPSIZE1e — the browser's caches checked at the browser's start** (shell, driver). Only the start: the check inside the host's
  start gate (`EngineBrowserHost.cs:113`), from 1d's kind and part. Proof: round 1's §6 at `d57b7f8d6`, a browser over its part
  starting with its caches cleared, one within it untouched.
- **KEEPSIZE1f — the floor as a warning** (driver, web). §6: the floor's acts removed, its default 10% at most 100 GB, its words and
  `SYSTEM_SPACE`'s fields, a warning per volume. Proof: `SpaceFloorTests`' below case removing nothing and saying the row; the default on
  1,024 GB and 4 TB; a set floor unchanged; a trees home on another volume below its floor saying that volume; the card's below story in
  both languages. After KEEPSIZE1d, so nothing the floor freed is left without a rule.
- **KEEP1a2** (open, D195) is narrowed: `obj/` matters only for folders that wait their days. The measure is evidence of what it could
  free, not that any `obj/` can be made again (Codex §3).

## 8. Rejected

- **Keeping a finished tree whole for its days** (D195 as built, and round 1): about 27 GB a day of copies of what the repository holds,
  kept for a scratch the owner decided goes with its tree.
- **Keeping a finished tree with its own scratch for its days** (the Opus read's finding 1(a)): the owner decided the scratch goes.
- **Keeping a copy of the tree in the record** (a diff, an archive of tracked files, a pack): the repository holds the commits on the line
  or a recovery ref; the owner's direction is *never a copy of the tree*.
- **Trusting a landing's hold for the session's history**: a squash keeps the content and not the commits (§1.1); the tip's own ref is
  one `update-ref`.
- **Arming the folder before its branch is gone, or before its record is kept**: a refused delete or a lost post would leave a mark that
  lies.
- **The move's moment as a finished folder's threshold**: a write between the judgement and the name predates it (§3.5).
- **Composing the record inside the repository's hold**: every start in that repository waits on it; the manifest carries the inputs out.
- **One record per session**: one tree is run in by a chain's sessions, teammates and helpers.
- **A backfill of the folders already set aside**: none of its facts can be proven (§7).
- **The allowance taking any folder past a day**: strays and leftovers are not copies, and an unrecorded folder is what the owner's frame
  keeps a record of first.
- **The whole's step emptying what may go when that cannot reach N**: a fetch and a cache lost for nothing, each pass.
- **The record as entries of the repository's feed**, or in the index's tables: a refresh replaces a repository's entries whole and a
  schema bump drops them (§1.3); the records are tables of their own that migrate.
- **The record as a file under the home**, indexed by a scan of the home: a second writer of history beside the store, with its own
  clear, where the sessions' rows already live in `knowledge.db`.
- **An allowance with `off`**: the owner's rule is the allowance; half the volume is the person's widest.
- **Parts as shares of N, or as decimals**: three shares of one N cannot be set apart without a rest that goes negative, and the stored
  sizes are whole GB as the floor's.
- **The allowance taking what cannot be made again, oldest first** (D195's *one limit for the whole folder*): kept rejected; it says.
- **The floor freeing space** (D195 point 4 as built): the drive's free space is at most a warning (the owner's direction); the
  allowance does the freeing.
- **Slimming an idle live tree** (the cleanup design §7): kept rejected. A live tree is bounded by the sessions, and its bytes count
  against the allowance and are said.
- **A part for the logs or the pipe's journal**: each is bounded by its own writer's days.
- **Records synced to the remote** in this design: a record holds a conversation's words, and what leaves a machine is the session
  sync's to decide; a later row may.

## 9. What this amends, once accepted

- **D195 point 3**: a finished session's folder goes whole an hour after its record is kept and its branch is deleted, its tip held by
  the line or a recovery ref; the slim stays for folders that wait their days; every removal reads one eligibility judgement.
- **D195 point 4 and the cleanup design §3.2**: the floor is a warning that removes nothing, 10% of a volume at most 100 GB by default;
  the allowance (§4) is the trigger.
- **D195 point 2 and the cleanup design §2.1 and §2.2**: `keep` gains `allowance` and each part's `gb`, whole GB; `browser-cache`
  becomes a kind with a part; `whole` keeps a finished folder for its days.
- **D195 point 6**: a finished tree's own scratch goes at its hour, by the owner's decision (§3.2).
- **D195 point 7 and the history clearing design §2**: a finished tree's record is history, cleared with its last session.
- **The cleanup design §3.4**: *anything that cannot be made again before its rule's time* reads with a finished tree's scratch let
  go at its hour once its record is kept.
- **The cleanup design §7**: *Deleting a tree's folder at the move, whole* and *`bin/` in the slim, ever* are superseded for
  recorded folders (§3); *One size limit for the whole Daoris folder* is reworded as §8's.
- **D88's AUTOTIDY1 and AUTOTIDY2a and D102's SQUASHTIDY1c notes**: a finished tip not held by the line's own branch gets a recovery
  ref before its branch goes, on both paths; the look's ground for taking a tree with ignored paths is the owner's decision, not the
  fourteen days.
- **The knowledge index** (`EntryKind`): a kind `record`, from tables of its own.

## 10. Round 3's answers to the reads of round 2

Each was checked against the code at `49cd7bdbc`. *Taken* says where; *answered* gives the reason it holds; *deferred* says to which
row.

### 10.1 Codex

| # | Finding | Checked | Answer |
|---|---|---|---|
| 1 | **Blocking.** The squash path can lose the session's original commits | holds: `TreeSquashes.cs:20-27, 70-91`, `TreeSweeps.cs:337-356`, `LandedCleanups.cs:324-335` | **Taken**: §1.1's table corrected; the tip's own ref wherever `refs/heads/<line>` does not hold it (§3.1); 1b's proof removes both branches |
| 2 | **Blocking.** The hour does not cover a write between the judgement and the moment in the name | holds: the name is chosen in `MoveAsideAsync` (`TreeSetAside.cs:203`, `:271-273`) after `HoldsAsync` (`TreeSweeps.cs:308`) | **Taken, by another means**: the finished folder's threshold is the judgement's `read` (§3.5), which covers the interval with the purge's own read and adds no git inside the hold or in a folder git has let go. 1b proves the race |
| 3 | **Blocking.** The mark authorizes deletion before the removal is settled, and the purge checks neither record nor holder | holds: move at `TreeSweeps.cs:198`, delete at `:239`, the ref dropped at `:242` | **Taken**: armed only at `recorded`, after `deleted` (§3.5); `refused` never armed; the holding check before each removal (§3.4); stops resumed (§3.5) |
| 4 | **Blocking.** Fallback and allowance paths bypass the record | holds: round 2's §3.3 and §4.1 step 1 | **Taken**: §3.4's classes and one judgement for every door; the allowance takes `recorded` alone; an awaiting-record folder waits for its record past its days. **Answered** for unmarked folders: they keep D195's days as built (`TreeStrays.cs:41-56`), not as copies, and the allowance never takes them |
| 5 | **Blocking.** Naming scratch does not preserve needed evidence | holds as a reading of the code: `TreeScratch.cs:15-19` excludes build folders | **Decided by the owner** (§3.2): a finished session's own scratch goes with its tree; what is worth keeping is committed or in its record. **Taken** for the inventory: named from `TreeHolds`, bounded, a build folder's contents not named, and the `bin/` database example dropped (§2.1). The press discloses each folder's class (§5) |
| 6 | **Blocking.** Retry, backfill and attribution lack identity | holds: several rows name one tree (`Trace.Chain.cs:1150-1158`); traces retire (`SessionBranches.cs:232-248`) | **Taken**: the inputs captured into the manifest before the delete, a manifest not written keeping the tree (§3.3); the record per tree naming its sessions, attributed only where bases bound it (§2.1, §2.2); **no backfill** (§7) |
| 7 | **Blocking.** The allowance's bounds and division are inconsistent | holds: `KeepChoices.cs:40` whole GB; round 2's shares and bounds | **Taken**: whole GB, the parts' sum at most N on every door, defaults scaled with a 1 GB minimum, N bounded by half the volume, the file read part by part (§4.2) |
| 8 | **Blocking.** Cached sizes can authorize excessive deletion or miss pressure | holds: `SystemSpace.cs:199-205` counts daily and after a run | **Taken**: fresh counts only for a destructive step, subtraction within the pass, a recount asked at once, per-folder sizes from the set-aside walk, the unknown keeping (§4.3) |
| 9 | Record bytes have no physical accounting rule | holds: `SystemSpace.cs:81-82, 90-92` | **Taken**: `knowledge.db` counted once under the rest; records said by count (§2.2, §4.5) |
| 10 | **Blocking.** The old-place extension needs place-aware identities | holds: `SpaceFree.cs:291-296` resolves under one root | **Taken** in KEEPSIZE1a's row and proof (§7) |
| 11 | **Blocking.** A searchable hit has no complete-record read | holds: `KnowledgeService.cs:248`, `SqliteKnowledgeStore.cs:222` | **Taken** in KEEPSIZE1c: `knowledge_get` resolves a record, the session page shows it, older records indexed, search → read → clear proved (§7) |
| 12 | **Blocking.** 1d depends on a browser rule assigned to 1e | holds: round 2's rows | **Taken**: the kind and its daily clearing in 1d, only the start's check in 1e (§4.6, §7) |
| 13 | Blocked parts and policy exceptions underspecified on the card | holds | **Taken**: a part's own words, the classes waiting, `whole`, history's age and *at least* from the effective policy (§4.4, §4.5) |
| 14 | Correct the factual prose | each holds: `TreeSetAside.cs:585`; `LandedBranches.cs:565-575`, `SessionBranches.cs:232-248`; the floor; `MachineLog.cs:349`; the 8 GB part against 27.9 GB | **Taken**: §1.1, §1.2, §1.4, §3.6 and §4.2 corrected, and D195's note with them |
| §3 | The rows' table: 1a's two-place proof, 1f's every volume, KEEP1a2's evidence | holds | **Taken** in §6 and §7 |

### 10.2 The Opus foundation read

| # | Finding | Checked | Answer |
|---|---|---|---|
| claim 1 | The scratch's source is the judgement's `TreeHolds`, not the last look's walk; build folders unnamed | holds: `TreeSetAside.cs:132-175`, `ContentHolds.cs:560-607`, `TreeScratch.cs:15-19` | **Taken** (§2.1; finding 9) |
| claim 2 | No `bytes` field; the move writes a manifest only with a package folder | holds: `SlimManifest.cs:21-22`, `TreeSetAside.cs:204` | **Taken** (findings 7) |
| claim 3 | The press already removes any folder early; steps 1 and 2 cannot run at the press | holds: `SpaceFree.cs:163`, `SetAsideKeeping.cs:78-99`, `TreeSweeps.cs:239` | **Taken** (finding 3) |
| claim 4 | No later pass meets the session again to retry | holds: `AutoTidies.cs:218-254` | **Taken** (finding 6) |
| claim 5 | The set-aside kind holds strays and leftovers, not copies | holds: `TreeStrays.cs:12-13, 41-56`, `PassLeftovers.cs:20-25` | **Taken** (finding 2) |
| claim 6 | `origin/<line>` is no hold that stays | holds: `RecoveryRetention.cs:12-14` | **Taken**: its own row in §1.1, the tip's ref written, the form recorded (§3.1, §3.3) |
| claim 7 | The local refresh replaces at `KnowledgeIndex.cs:41`; the index is derived and dropped on a bump | holds: `KnowledgeIndex.cs:41, 61`, `SqliteKnowledgeStore.cs:13-21, 112-117` | **Taken** (§1.3; finding 13) |
| claim 8 | Round 1 is `d57b7f8d6`, not `47c9b78a9` | holds: `47c9b78a9` changed `TASKS.md` alone, and the design is first at `d57b7f8d6` | **Taken**: the standing line, rows 1a and 1e, and D195's note |
| 1 | **Blocking.** The hour removes a tree's own scratch, unpressed | holds: `ContentHolds.cs:552-559, 620-622`, `TreeSweeps.cs:312-314` | **Decided by the owner**, (b): own scratch goes with its tree (§3.2), recorded in D195's note; AUTOTIDY2a added to §9 |
| 2 | **Blocking.** The allowance takes folders that are not copies | holds | **Taken**: `recorded` alone (§3.4, §4.1); round 2's *the allowance may take it sooner* deleted; the rest counted and said with the press as door (§4.4); 1d's proof |
| 3 | **Blocking.** The press's bounds contradict each other | holds | **Taken**: today's early group kept as the person's word, each folder listed with its class and holder; the "never" a listed fact; the record posted first from the manifest (§5) |
| 4 | **Blocking.** No stopping rule when N cannot be reached | holds: 32 − 14.4 = 17.6 GB against about 30 GB | **Taken**: the whole's step only where all eligible units would reach N, else the parts' overage alone, said (§4.3); the default 48 GB reasoned (§4.2); 1d's proof |
| 5 | The mark is written before the branch goes | holds: `TreeSweeps.cs:239-253` | **Taken**: `recorded` follows `deleted`; a refused delete marks `refused` (§3.5) |
| 6 | A retry has nothing to compose the record from | holds | **Taken**: the inputs in the manifest at `judged`; the pass, the press and a restart post from it (§3.3, §3.5). The backfill is dropped |
| 7 | Most trees get no manifest | holds: `TreeSetAside.cs:204`, `AutoTidies.cs:271-277` | **Taken**: a manifest for every finished tree, `whole` included, since the record's inputs live in it (Opus suggested none under `whole`; a `whole` tree's record needs them too); sizes from the set-aside walk (§3.3, §4.3) |
| 8 | Two purges overwrite `purge-failed.json` | holds: `TreeSetAside.cs:377-392, 437-464` | **Taken**: one call, `after` per class (§3.4) |
| 9 | The named scratch's source and bound | holds | **Taken**: `TreeHolds`, at most 5,000 names, build folders unnamed, the `bin/` example dropped; no size is read (§2.1) |
| 10 | One record per session does not fit shared trees | holds: `Trace.Chain.cs:1150-1158` | **Taken**: one per tree, keyed by its set-aside identity, its unit named, every session linked (§2.2) |
| 11 | The backfill cannot prove what it needs | holds: `TreeSetAside.cs:215`, `SlimManifest.cs:21-22`, `SessionBranches.cs:30` | **Taken**: dropped (§7) |
| 12 | The record names a ref that may go | holds: `RecoveryRetention.cs:21-29`, `RecoveryDropping.cs:19-23` | **Taken**: the record keeps the tip; the command uses it; the ref is looked up on read (§2.1); a dropped ref unmarks a folder (§3.4) |
| 13 | Records in a store built for derived data; the single delete | holds: `SqliteKnowledgeStore.cs:13-21`, `Sessions.cs:1522-1528`, `SessionLedger.cs:1996-2003` | **Taken**: tables that migrate, the full-text table rebuilt; `Sessions.DeleteAsync`, which both doors call, takes the links and an orphaned record (§2.2) |
| 14 | 1c's ranking, default and audience | holds: `KnowledgeService.cs:180, 186-190`, `MachineLog.cs:30-32` | **Taken** in 1c (§7) |
| 15 | The records' bytes counted twice | holds: `SystemSpace.cs:81-82, 90-92` | **Taken** (Codex 9) |
| 16 | Stale and doubled counts | holds | **Taken** (Codex 8; §4.3) |
| 17 | The allowance overrides `whole` | holds | **Taken**: never under `whole` (§3.4, §4.1) |
| 18 | The record composed inside the repository's hold | holds: `AutoTidies.cs:291-297` | **Taken**: composed and posted after the hold from the manifest; only the tip's ref is new inside, since it must precede the delete (§3.1, §3.5) |
| 19 | The hour's purge can leave a half-deleted folder | holds: `TreeSetAside.cs:596-640` | **Taken**: renamed into `.going/` first (§3.4) |
| 20 | Pointers and citations | holds | **Taken**: `d57b7f8d6`; rows 1a and 1e name round 1's §6 at it and state their proof; `KnowledgeIndex.cs:41` |
| 21 | The carried ref on both paths | holds: `TreeSweeps.cs:181` | **Taken**: on both paths, and the inventory says such refs are never retired (§3.1) |
| 22 | §4.6's shape | holds: `KeepChoices.cs:40` | **Taken**: whole GB; a part above a lowered N refused at the door and not read from the file, as KEEP1d reads every rule; `KeepKinds.BrowserCache` in 1d alone |

**Deferred**: none to a later round. What the rows build is named in each row's proof (§7).
