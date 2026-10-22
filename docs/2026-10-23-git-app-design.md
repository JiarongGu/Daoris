# Daoris's git, as comprehensive as a git management app, and Review fast: the design (GITAPP1, 2026-10-23)

**Status: proposed (D202), round 1, the synthesis of GITAPP1's co-design under D191.** Written by Claude Opus from the brief
(`docs/2026-10-23-git-app-brief.md`) and the three sealed proposals (`docs/2026-10-23-git-app-proposals.md`: Claude's, Codex's
through Daoris's helper, Fable's), each read whole. Codex's and Fable's reads of this synthesis come afterwards, through the
steward (D191 point 1; Fable reads because this changes a foundation: one reader every flow's git goes through, and a held
process). Nothing is built. `Dnn` is `docs/decisions/Dnn.md`; `Driver/` is `src/Daoris.Desktop/Daoris.Desktop.Driver/`,
`Modules/` is `src/Daoris.Desktop/Daoris.Desktop.Modules/`, `Web/` is `src/Daoris.Web/`. "Claude", "Codex" and "Fable" below
name the three proposals, and §-numbers prefixed C, X and F point into them (C2 is Claude's §C2, X1 Codex's §1, F2.2 Fable's §2.2).

## 0. The answer in twelve lines

1. **Review's cost is serial process starts**, and a start on this machine costs 0.2-1.6 s because process creation is slow
   under a real-time scanner and load (§1.2), not because git is slow. The cure is fewer starts, started together.
2. **Review in at most three rounds of parallel id-keyed reads**: ≤ 10 starts cold (11 where REVIEWBASE3's merge runs), none
   per untracked file, none for the tree's layout, none when nothing moved. ReviewBase's semantics are kept whole (§3).
3. **Targets under the brief's load**, medians: 16.2 s → ≤ 3 s; 44.1 s → ≤ 3.2 s; 8.4 s → ≤ 4 s; opened again with nothing moved
   ≤ 0.1 s; a live session's commit shown ≤ 3 s after its ref write (§3.4).
4. **Review's reads of a session's tree take no optional lock** (REVIEWLOCK1, built first, held by a census, §1.1).
5. **One `GitReader` per repository** in the driver library: the layout read from disk, one refs snapshot validated by the ref
   files' stamps, one shared id-keyed cache, in-flight joining, at most three starts of its own in flight, the gate unchanged (§4).
6. **No held git process for Review**; a held `cat-file --batch-command` only for the screens' object questions, 30 s idle,
   accepted only after the pack-hold hazard is measured (§4.3).
7. **Invalidation, never expiry**: the ref watch, the stamps, Daoris's own writes and, for an open review, a watch of its tree.
   An answer says the ids it was read at; nothing stale is drawn as current (§5).
8. **The person's git** (§7): every read a git management app offers, per repository and per session tree; the ref and remote
   acts; history edits on a branch no tree holds, computed from objects; and the working-tree acts (stage by hunk and line,
   commit, amend, stash, switch, resolve) in **the person's own checkout, behind a switch on its Setup, off by default**.
9. **A session's tree is never acted on by the person through Daoris**; its work is reached by words until it lands.
10. **Nothing written into `.git` on Daoris's own and nothing cached on disk** (D147 §4.2 unamended): no `commit-graph` press, no
    fsmonitor press; the command is shown to copy.
11. **Rows** (§12): REVIEWLOCK1, then GITAPP1a Review fast, 1b Review follows the tree, 1c the judgements through the reader, 1d
    the reads drawn, 1e ref and remote acts, 1f history edits without a working tree, 1g the person's checkout, 1h history at
    scale, 1i the install seen.
12. **Amends** D147 points 2, 3 and 6 and its design §2.8, §3.1 and §4.1; D113 §5 by the switch; D150 point 3 by one tab.
    D198, D51, D55 and D181 stand.

## 1. What this synthesis checked, and the two facts it accounts for

Each proposal's claims that a choice below rests on were read against the code at this tree's base (`fe4809b75`).

| Claim | By | Read | Verdict |
|---|---|---|---|
| Review's `status` passes no `--no-optional-locks` | Fable F1 | `HelperCapture.cs:90,104` and `HelpCheckouts.cs:104` pass it; Review's `WorkingTree` and `TreeWorkReads` reads do not; `GitWiring` sets no `GIT_OPTIONAL_LOCKS` | Holds; REVIEWLOCK1 is filed |
| The behind/ahead counts are unions over the line here and on origin, not pairwise | Codex X1 | `ReviewRanges.BehindAsync` (`Driver/Trees/SessionTrees/ReviewRanges.cs`) counts `rev-list --count <refs…> --not HEAD` and its converse over both refs at once | Holds: one `%(ahead-behind:HEAD)` per ref (Fable F2.2's round 3) is not the same number when the two tips diverge, so the two counts stay |
| The person's place in the gate is for "a start a person is waiting on" | Claude C2 vs Fable F4.3 | `StartClass.Person`'s summary (`Driver/Platform/ProcessStarts.cs:10-14`) names a conversation's agent, a press, a terminal, the host; git is named under `Background` | Both readings fit the words; §4.4 decides on what the place protects |
| "D51 rule 7" is the recovery ref | brief §5, design §3.1 | D51 has no numbered rule 7; the recovery ref is D102's SQUASHTIDY1c note and CLEANUP1 (Fable F1) | A citation corrected in D147's design by this decision |
| A session's process can be told apart | §7.5 | `DAORIS_SESSION_ID` is handed to every session (13 sources name it) | Holds |
| This repository's `commit-graph` carries changed-path Bloom filters | Fable F1 | Relayed, not re-read here | Taken as Fable's reading; it decides nothing below except that a press would gain nothing here |
| `merge-tree --write-tree` writes objects | Codex X11 | `ReviewRanges.MergedAsync` | Holds: objects only, no ref, index or file. Said wherever "reads only" is said |
| LANDING is not asked beside the first diff | Codex X11 | `DiffPane.tsx`: `plannable` derives from the tree's answer; `useHandOff(session)` is asked always | Holds; HANDOFF_PLAN is the one asked unconditionally |

### 1.1 Optional locks (REVIEWLOCK1)

`git status` refreshes the index and writes it back where it can take `index.lock`, unless `--no-optional-locks` or
`GIT_OPTIONAL_LOCKS=0` is given; `git diff` refreshes quietly the same way. So opening Review on a running session's tree can
take that tree's index lock for a moment, and an agent's `git add` or `commit` at that instant fails with "index.lock exists".
It is also a write Daoris makes into a tree that is the session's (D51). The brief's probe ran with `GIT_OPTIONAL_LOCKS=0`, so its
numbers are for the corrected call.

**Accounted for**: REVIEWLOCK1 stays its own row and is built first (§12): `--no-optional-locks` on every read-only `status` and
`diff` Daoris runs in a tree that is not its own write, and a census holds it. GITAPP1a's new chain is written with it, and its
census is REVIEWLOCK1's, widened to the reader. Not by `GIT_OPTIONAL_LOCKS=0` in `GitWiring.Hand`, which hands the agents' own git
the same environment and would change what their `status` does.

### 1.2 Process creation under the scanner

Fable's probe (F3) started programs that open no repository (`hostname.exe`, `cmd.exe /c exit`) and found them as slow as git or
slower (median 1.2 s), with eight starts together finishing in the time of one or two. With PROC1a's and UIFREEZE1's findings in
the fix log, the reading is: **each start waits on something serialised per process creation**, a third-party real-time scanner's
verdict being the kind that overlaps across concurrent starts and not across a chain; load multiplies it; the launcher is already
bypassed. The brief read the OS's own antivirus as off; Fable read a third-party one registered and on. Both are about this
machine; neither is something Daoris may change.

**Accounted for**, in what each costs to ignore:

- **Start fewer.** The layout and the refs come from disk, every answer named by ids is kept, and an untracked file costs no start.
  Review goes from 13-19 + U serial starts to ≤ 10 (§3).
- **Start together.** Rounds through the gate, never a chain. The gate's width stays four: process creation is serialised under
  .NET's one lock (`ProcessStarts`' remarks), and PROC1a measured no gain past four.
- **Do not start for object questions**, where a screen asks hundreds: the held `cat-file` of §4.3. Not for Review, which asks
  none.
- **Say the machine, not Daoris.** The `review.read` line carries `starts`, `rounds` and `waitedMs`; the bench prints each start's
  wait and run. When a start costs a second, the numbers say so. An exclusion for git in the scanner is the person's call; at most
  a Get started hint names it, and Daoris changes nothing of the scanner's.
- **The targets are set under this load**, not on an idle machine (§3.4), because this is the machine the person uses.

## 2. How the three compare, in one table

| Question | Claude | Codex | Fable | Chosen |
|---|---|---|---|---|
| Review's starts cold | ≤ 4 at once | ≤ 10, counted per path | ≤ 10, three rounds | **≤ 10 in ≤ 3 rounds** (Codex's count, Fable's rounds) |
| Review's targets | ≤ 1.5 / 1.5 / 2.5 s | ≤ 4 / 4.5 / 5 s median, p95 twice | ≤ 3 / 3.2 / 4 s median | **Fable's**, with a p90 ceiling of twice (§3.4) |
| Range semantics | merge-bases keyed by ids | union counts, candidate order, REVIEWBASE3 kept | `ahead-behind` for both counts | **Codex's**: kept whole |
| Untracked files | from disk, byte-equal to git | from disk, display data | from disk, byte-equal to git | **From disk**, proved byte-equal for text (Claude, Fable) and said where not exact (Codex) |
| The offer and the plans | off the critical path | off; HANDOFF_PLAN not eager | the offer in round 3 over the snapshot's tips | **Off the critical path** (Claude, Codex) |
| Layout and refs | snapshot by generation | status + refs, two starts | from disk, stamped | **From disk, stamped** (Fable) |
| Optional locks | not seen | `status` "disabled optional writes" | found the defect | **REVIEWLOCK1 first** |
| The reader | `GitRepository` per common dir | one coordinator in the host | `GitReader` per common dir | **`GitReader` per common dir** |
| A held process | `--batch-check`, 2 min idle, ≤ 8 | lazy, ≤ 4, retired idle | screens only, 30 s idle, hazard measured | **Fable's, capped at four** (Codex) |
| Gate class | Person | the reserved place kept | Background | **Background** (§4.4) |
| Working-tree watch | sessions' trees and the person's checkout while open | attended trees | open reviews only | **Open reviews**, and the person's checkout while its Changes tab is open (1g) |
| fsmonitor, untracked cache | never | never | a press | **Never; the command to copy** |
| `commit-graph` | a press, after measuring | never | a press | **Never; the command to copy** |
| Working-tree acts | person's checkout, behind a switch | a manual tree of the person's | an idle session's tree | **The person's checkout, behind a switch** (§7.2) |
| Ref-only history edits | cherry-pick, revert, rebase by objects | in the manual tree | in the idle tree | **By objects, on a branch no tree holds** (Claude) |
| Conflict resolution | three-way view writing a chosen result | whole side or external tool | resolved on the page | **A side per hunk, or the person's editor** (Codex, D55) |
| Agents | nothing new | common reads and their own tree | nothing new | **Nothing new** |
| File history pages | continue from the last id | cache prefixes, prove a cursor | keep the id list | **Keep the id list with each commit's path** (Fable) |

The sections below take each question in turn, say what was chosen and what each proposal's other answer would have cost.

## 3. Review fast (Q1)

### 3.1 What a Review open reads

Resolved first, with **no process**:

- **The tree's layout** from disk: the tree's `.git` (a folder, or a file naming `gitdir:`), `<gitdir>/commondir`, `<gitdir>/HEAD`. A
  folder is a top exactly when it holds its own `.git`, which answers D113 §1's walk-up hazard without asking git. Reftable or
  anything the reader does not know is one git call, said in the answer's `commands` (as `LineGuesses` falls back today).
- **The refs**, from the reader's snapshot (§5.1): the tree's `HEAD` as an id, its branch, the line's tip here and on origin, as
  ids. One `for-each-ref` where the snapshot is not current; none where its stamp stands.
- **The line**, through `LineGuesses` as today.

Then, in rounds, each start keyed by the ids it was asked about unless marked *tree*:

| Round | Started together | Depends on |
|---|---|---|
| 1 | `status --no-optional-locks --porcelain=v2 -z --untracked-files=all` (*tree*); `diff --no-optional-locks … -M -z --raw --numstat -p HEAD` (*tree*); `merge-base <head> <line>`; `merge-base <head> <origin-line>`; `merge-base --is-ancestor <recorded> <head>` | nothing |
| 2 | only where two or more distinct candidates: `merge-base --independent <candidates>`; where they are incomparable, `merge-tree --write-tree` (REVIEWBASE3) | round 1's bases |
| 3 | the committed diff `<from>..<head>` (REVIEW3's one-call reader, its caps unchanged); `rev-list --count <line-refs> --not <head>` and its converse (REVIEWBASE1's union counts) | `from` |

Untracked files are read from disk while round 1 runs (§6.2). The landed case (`LandedReviews`) is ids once its branch resolves
through the snapshot, so it joins round 3. The branch name (step E) is the `HEAD` file. ReviewBase's candidate order, the grown
base (WSR6), the replayed bases and REVIEWBASE3's merged base are kept exactly: the chain changes when each question is asked,
never which.

**Starts, cold**: the snapshot's one where not current, five in round 1, none to two in round 2, three in round 3: **≤ 10 in the
ordinary case** (an own tree, base recorded, the line configured and both its refs readable), **11 where REVIEWBASE3's merge
runs**. Each other path (no line configured, a displaced base, a gone branch, an older git without an atom) has its own counted
ceiling in the census (§10). **Warm, nothing moved**: a landed or ended session, 0 starts; a live session, the tree's two (row
1a), 0 under the tree watch (row 1b). **After a commit**: the tree's two, one range diff, two counts; the merge-bases of the new
`head` are new ids, so also round 1's three.

### 3.2 What leaves the critical path

`SESSION_DIFF` answers the review: its patch, its range and its counts. **The Accept offer** (`TreeWorkReads`, `TreeFollows` and
the content holds, at least six starts and up to 21 more, Fable F1) becomes its own read, asked by the foot once the diff has
answered, through the same reader and cache (Claude C2, Codex X1). **HANDOFF_PLAN**, asked unconditionally today, is asked after
the diff too. `LANDING` already waits for the tree's answer. The foot says *Reading…* while its reads run, as it does for
`LANDING` now, and its starts and time are reported apart in the `review.read` line, since a fast patch is not a finished plan
(Codex X1). Fable kept the offer in round 3 over the snapshot's tips (F2.2); that would keep a 21-call proof in the patch's wall
on every open, which is the cost being removed.

### 3.3 A live session moving

- **Nothing moved in git**: the record's `updated` no longer re-reads the diff (`Web/src/bridge/sessions/trees.ts:182-184`); in
  row 1a the page keeps its 15 s freshness for the tree's two calls, in row 1b the tree watch keeps them.
- **A commit**: the ref watch hears the session branch move; row 1b publishes the state hub's `diff` scope for the sessions whose
  tree is on that branch (`session-branches.json`), and pre-reads round 3 for an open review, so the page's refetch is served from
  the cache.
- **The line moved** (a fetch, D109, a landing): the same hint; `from` may change; one more range diff.
- **Claude's pushed slice per open session** (C2) is not taken in this round: the state hub's scope plus an id-keyed cache gives
  the same effect (no re-read when nothing moved, one read when something did) without a new slice contract; a pushed slice is
  reconsidered if 1b's measurement shows the refetch's round trip matters.

### 3.4 The targets

Held under the brief's load (§4 of the brief, named beside every number), on the bench (§10.3) and on the installed window
(GITAPP1i). Median over at least ten runs with p90 beside; the ceiling fails a row's acceptance, never a gate (D54's rule for
budgets).

| Case | Today | First open, median / p90 | Starts | Opened again, nothing moved | A live session's commit |
|---|---|---|---|---|---|
| 368 files committed, nothing untracked | 16.2 s | **≤ 3 s / ≤ 6 s** | ≤ 10, ≤ 3 rounds | live: ≤ 1.5 s, 2 starts (1a); ≤ 0.1 s, 0 (1b). Ended or landed: ≤ 0.1 s, 0 | shown ≤ 3 s after the ref write (1b) |
| 368 files, 50 untracked | 44.1 s | **≤ 3.2 s / ≤ 6.4 s** | ≤ 10; 0 per untracked file | as above | as above |
| 1,771 files committed | 8.4 s | **≤ 4 s / ≤ 8 s** | ≤ 10 | as above | as above |

**Why these and not the others.** The brief's "same facts in four starts at once" (0.95, 1.05, 1.6 s) is a floor with no dependency
between the calls; the real chain keeps up to three dependent rounds, each at least one start's median under this load (0.3-0.8 s),
and the committed diff alone is 1.0-2.6 s for 368 files. Three rounds' medians plus the diff is about 3 s. **Claude's ≤ 1.5 s**
(C2) holds only with four starts, which drops REVIEWBASE1's union counts and REVIEWBASE3's merge (the ids it keys still need asking
on a first open): it would be met by changing what Review shows. **Codex's ≤ 4-5 s** (X1) is looser than the arithmetic, so a chain
still half serial would pass it. Fable's medians are the arithmetic; the p90 at twice is the tail the brief measured (single
starts at 2-4× between runs). The page's paint of 368 file rows is not measured: 1a writes `review.painted` and collapses rows past
the first forty only if the paint is over 0.5 s (Fable's guess, taken as a measurement to make, not a change to make).

## 4. The reader (Q2)

### 4.1 One per repository

**`GitReader`** (new, `Driver/Trees/GitReads/`), one per common git directory, so a linked tree shares its repository's; handed out
by **`GitReaders`**, one registry per process: the window's loop holds one and every route and judge in that process reads through
it; `daoris-driver` holds one for a verb's life; a test takes its own with today's seams (`WorkingTree.GitRead`, `Started`, `Runs`,
`Answered`, `ProcessStarts.Within`). It holds the layout, the refs snapshot (§5.1), the one shared `GitReadCache` (64 MiB, LRU,
memory only), in-flight joining (one read per key under way; a second asker awaits it) and its bound of starts.

Every start still goes through `WorkingTree.GitStart`: Tools' git, `GitWiring.Hand`, `GitBounds`, the gate. `GitFacts` and
`RefSnapshot` keep their flow-scoped meaning ("asked once within this flow, dropped by this flow's writes"), consulting the reader
first for layout and ids.

**One, not per area** (Fable F4.1 against none): Review, the landing's proof and the commit page ask the same ids; per area would
keep three copies and three invalidations. **Not Codex's one coordinator in the host** (X2) beyond this: the reader is per
repository because its snapshot and stamps are, and the registry is the coordinator. **Not Claude's `GitRepository`** (C3) as a name:
it suggests something that writes.

### 4.2 Bounded parallel

At most **three of one reader's starts in flight** (the background's places), so one repository's Review cannot hold the person's
place; a round is handed over together and started as places come. **A long read** (a pickaxe, a whole-history walk) holds one of
those three at most, so a search never starves a Review of the same repository (from Codex X2's "two walks", made per reader). The
gate is unchanged (`ProcessStarts.Bound = 4`, three background).

### 4.3 The held process

**Not for Review**: nothing Review asks is an object question. **For the screens** (GITAPP1d): `cat-file --batch-command` (`--batch`
below git 2.36), through `GitStart`, one gated start, answers matched by the object id each echoes, so a desynced stream ends and
restarts it. It answers object questions only: a commit's fields and parents, a tree's entries, a blob for a preview, an object's
existence. It never answers a walk. It **lives 30 s past its last question** (Fable F4.2), **at most four across the machine**
(Codex X2), and is accepted only after GITAPP1d measures it against a `git gc` run beside it on this repository: on Windows a process
holding pack files open makes the person's own git ask "Unlink of file … failed. Should I try again?". If it does, the screens live
without it and pay one start a burst. **Claude's two idle minutes and eight held** (C3) is rejected on that hazard: a held process
across idle minutes is exactly what keeps packs open while the person's tools repack.

### 4.4 The gate's classes

Review's starts stay **`StartClass.Background`**. Claude (C2) gave them the person's place because the person pressed. The reserved
place exists so that a start a person waits on never queues behind a fan-out; Review's round is five starts at once, and as
`Person` they would take the reserved place and the background's together, so a conversation's agent the person just asked for
would queue behind git. Three places serve a round of five at one start's delay. The `review.read` line's `waitedMs` measures that
delay on the install; if it shows, a later row gives a pressed read one place of its own, never the agents' (Fable F4.3).

### 4.5 In which process

The driver library, used by the modules in the window's process, by `daoris-driver` (D50) and by the tests. No git-serving process
of Daoris's own: a protocol around one library, and the gate is per process anyway. The service's one `ls-files` for ignored paths
(`IgnoredPaths`) is untouched: a different process, no remote.

## 5. Caching and invalidation (Q3)

### 5.1 The refs snapshot

One `for-each-ref` over `refs/heads`, `refs/remotes`, `refs/tags` and `refs/stash`, plus each linked tree's `HEAD` file, kept with
a **stamp**: the write time and size of `packed-refs`, `HEAD`, each tree's `HEAD`, and each file under those ref folders. The stamp
is read again **before every use**; a difference reads the snapshot afresh. The ref watch drops it on its hint, and `RefMoves.Mine`
on Daoris's own move. Reftable has no stamp: read on every use, one start, said. This keeps D147 §4.2 whole: **a ref is never a key;
what the snapshot resolved it to is.**

The ref watch widens from repositories holding Daoris's work (`RefWatch.cs:249-265`) to every registered repository with a
checkout, within `ChangeWatch.MostRoots` (32), those holding work first; past the cap the stamp alone guards. Its hearing gains
`refs/tags/**` (Codex X11: it hears heads, remotes, reftable and trees' `HEAD` today, not tags).

**Claude's generation counter** (C3) is not taken: a generation raised by the watch is only as good as the watch, while the stamp is
git's own files, read at use, and catches a move the watch missed.

### 5.2 What is kept, under what key

| Answer | Key | Kept |
|---|---|---|
| A range's files and patches (Review's committed part, a commit, a compare, a landed branch) | common dir, view, from, to, options, bound version | until evicted |
| `merge-base`, `--is-ancestor` (its "no" is an answer), `--independent`, `merge-tree --write-tree`, `rev-list --count` | common dir, command, ids | until evicted |
| A graph page, a file's history, a blame, a commit's fields (GIT1b) | as today, with options | until evicted |
| A file's history's id list (§8) | common dir, commit, path | until evicted |
| A search by message, author or path | common dir, tips, query | until evicted |
| The layout | the folder | until its `.git` stamp changes |
| The refs snapshot | the repository | until its stamp or the watch says otherwise |
| **Never** | `status`, `diff HEAD`, anything the working tree decides; a name's commit alone; a read git did not answer; a pickaxe | |

**The context in the key** (Codex X3): every key carries the parser's version, and the reader reads a context stamp from disk
before keeping anything: where `refs/replace/`, `info/grafts` or `shallow` exist, nothing of that repository is kept (git's answer
for the same ids can differ); `info/attributes` and the configuration that changes an answer are either given on the call (as
today's `--no-color`, `--no-show-signature`) or part of the key. `blame.ignoreRevsFile` is given on the blame call, so it keys.

**Partial clones** (Codex X4): a local read must not fetch a missing object through a remote. The reader passes `GIT_NO_LAZY_FETCH=1`
where the git honours it; where it does not, a partial clone's answers are not kept and the view says objects may be missing. The
first row that touches it measures which.

### 5.3 Nothing stale drawn as current

- Every answer names the ids it was read at (`head`, `from`, the tips); the page keys by them (Review's key gains `head`).
- The page says **Read at … · HEAD …** where it shows a kept answer, never "current" (Codex X3); a kept answer is drawn dimmed while a
  newer read is in flight (D41).
- A completion for an older stamp never overwrites a newer answer, and a stale answer never enables an act: every act re-judges
  its inputs at the shown ids (D147 §3.1).
- A watch's loss, overflow, error or the machine's resume drops what it kept; the five-minute deadline behind each topic stands.

## 6. Working-tree state (Q4)

### 6.1 Read, never written

Two calls in round 1, both `--no-optional-locks`: `status --porcelain=v2 -z --untracked-files=all` and `diff … HEAD`. Status is kept
apart from the patch: staged and unstaged changes that cancel give an empty patch while the tree is still not clean (Codex X4).
Unknown is never clean.

**fsmonitor and the untracked cache are not turned on, and no press offers them** (Fable F9's press rejected): they write the
repository's configuration and start a daemon the person did not ask for; after row 1b `status` runs once per change, not once per
open, so it is not the wall. Where the person turns them on in their own configuration, Daoris's `status` is faster with them.
Where the `review.read` line shows a slow status, Review's head (and the Changes tab's, 1g) names the command
(`git config core.fsmonitor true`) to copy.

### 6.2 Untracked files from disk

An untracked file's patch is the file: `diff --git a/<p> b/<p>`, `new file mode 100644`, `--- /dev/null`, `+++ b/<p>`,
`@@ -0,0 +1,N @@`, each line with `+`, `\ No newline at end of file` where it lacks one. The reader reads the bytes within the 200 KB
bound (`UntrackedPatchLimit`), applies the binary test git and D111 apply (a NUL in the first 8,000 bytes), decodes as `ReadGitAsync`
does, and counts the patch against `PatchBudget` and `PatchCap` as the committed list counts.

It never reads a path `status` lists with a trailing `/` (a nested repository or a folder), never follows a link or reparse point
out of the tree, and reads a file whose size or time changes during the read again once, then says it is changing. Where an encoding,
line ending or attribute makes the view inexact, the row says so (Codex X4). **The proof is byte-equality with
`git diff --no-index -- /dev/null <path>`** over a fixture set (§10.2); the mode is `100644` as git writes it on this platform.

### 6.3 The tree watch

Row 1b keeps the uncommitted answer under a `ChangeWatch` root on the tree's folder **for each open review of a live session**,
released when the review closes or after fifteen idle minutes, filtering `.git` and the tree's ignored folders as `IgnoredPaths`
names them, 250 ms quiet, 2 s cap, stamped also by the tree's `index` file. A hint, a stamp change, or the ref watch's hint for the
tree's branch drops it. Not a watch per session tree (Claude C4 watched every running session's): a root per session under a
dependency install, for nobody looking. Row 1g adds the person's checkout, while its Changes tab is open.

## 7. What the person gets (Q5)

### 7.1 Every read

All three proposals offer every read of the brief's §5; they differ only in detail. Chosen, with where each lives (D150 point 3:
Repositories → a repository's tabs; a session's Review):

| Capability | Per repository | Per session tree | Where | Row |
|---|---|---|---|---|
| History graph | built; filters by branch, author, path, since and until (one `log`, keyed by tips and filter) | the branch beside its line | History tab; Review's *Show in Git* | 1d |
| A commit | message, author, committer, parents as doors, refs holding it, who made it (D143), its changes in `PatchView` | the same | a commit's page | 1d |
| Diff views | inline (built), side by side, word diff, whitespace ignored (a re-read with `-w`, keyed by it); a binary says so (D111), no image diff | the same in Review | Review, a commit's page | 1d |
| Blame | a gutter, runs grouped, *Before this change* | at the tree's `HEAD` | a file row's *Blame* | 1d |
| A file's history | following renames, pages from the kept id list | the same | a file row's *History* | 1d, 1h |
| Compare | two refs resolved once; `A...B` and `A..B` | the tree's branch against its line | a branch's *Compare with…* | 1d |
| Search | messages and authors (one `log`, kept); paths at a commit (`ls-tree`); content at a commit (`git grep`); content across history (`-S`/`-G`) as a long read: path-scoped, streamed, cancellable, never kept | from the tree's `HEAD` | History tab's search | 1d; the pickaxe 1h |
| Conflict preview | — | `merge-tree --write-tree` of the tree's head onto the line: would it conflict, in which files and hunks (objects only) | Review's foot, beside Accept | 1d |
| Stashes, tags, remotes, worktrees, a branch's reflog | read: `stash list` and a stash's changes by its id; tags on the graph and a commit; `remote -v` through `GitRemotes`; worktrees with the session holding each; `reflog show <branch>`, never kept | — | Branches tab; a branch's page | 1d |
| Submodules | listed from `.gitmodules` and `160000` entries, each a door to its own repository where registered | the same | a commit's files | 1h |
| LFS | a pointer drawn as *a file kept by LFS* with its size, never its pointer text | the same | Review, a commit's page | 1d |

### 7.2 Where the working-tree acts live

The three proposals answered differently, and this is the choice the synthesis turns on.

- **Claude**: the person's own checkout, behind a per-repository switch, off by default.
- **Codex**: an optional manual worktree of the person's, created on a press, never the checkout nor a session's tree.
- **Fable**: a session's tree while it is idle, to finish a session's work; the checkout stays read-only.

**Chosen: the person's checkout, behind the switch** *Daoris may change this checkout's files* (允许 Daoris 修改此检出中的文件) on
the repository's Setup, off by default, its state said on every act's plan. The ask is a git management app, and such an app works
where the person's changes are: in their checkout, beside their editor. The switch is the person's say-so, made once and visible
(D150 point 1: a setting lives on the thing it is about). The acts that write only the index and refs (stage, unstage, commit,
amend) never touch a file the editor holds; the acts that write files (stash, apply, switch, resolve) say each file in their plan.

**Codex's manual tree is rejected**: the person's edits are in their checkout, so a separate tree has nothing of theirs to stage
until they move their editing there, and it is a second kind of tree with its own lock, clean-up, knowledge and ownership rules
(Fable F13). **Fable's idle session tree is rejected**: the session's work is the session's until it lands (D51, D164); an idle
session can be gone on with, and its agent would find commits in its branch its transcript does not explain, and its trace (D143)
would not say who made them. The person reaches a session's work by words, and reads it in full (Claude C5). Fable's real need,
a session that ended with work unfinished or a replay's conflict, is met by going on with the session, or by landing what is
committed and finishing the rest in the checkout.

**Refused in the checkout**, with the reason: a hard reset, `clean`, a force anything (they lose work with no recovery); a recursive
submodule write (it writes other repositories); a merge or rebase of the checkout's branch (moving the line is D109's and the
landing's door, with its own refusals); any act while a conversation runs in the checkout (two writers on one index, as D51
refuses in a tree), held by the session records naming the checkout.

### 7.3 History edits with no working tree

From Claude C5, alone among the three: **cherry-pick a commit onto a branch, revert a commit on a branch, rebase a branch onto
another, rename a branch**, each computed from objects (`merge-tree --write-tree` and `commit-tree`, commit by commit) and applied
by one `update-ref` at the judged old id. A conflict refuses the whole act and names the files and hunks; nothing is half applied.
**Refused on a branch any tree has checked out** (the list's `worktreepath`), the person's checkout included: moving a checked-out
branch's ref leaves its tree's index and files behind it (tightened from Claude's "a running session's tree"). Refused on the line,
a session branch and a landed branch. `git replay` is not used: it is marked experimental in git's own documentation.

### 7.4 Agents

**Nothing new** (Claude, Fable). An agent runs its own git in its own tree; Daoris's reader is not offered to it as a tool, which
would double the starts and couple it to Daoris's bounds. The terminal verbs (§9) read for anyone; a writing verb refuses where
`DAORIS_SESSION_ID` is set, so a session cannot press as the person. That is honesty, not a boundary: an agent's own git is bounded
by D198's doors for anything that reaches a remote, and by its tree for the rest. Codex's "common reads and authorised local
operations" (X5) is the same in effect for reads and is not taken for acts.

## 8. Writes (Q6)

Every act is planned, shown with the command it will run, and judged again at the ids shown (D147 §3.1). An act that reaches a
remote passes D198's doors and is judged Read / Write / Dangerous there, the person's press being its grant as the landing's is
(D198 point 13). Daoris's own writes are serialised per tree and per ref, revalidated by git's locks and `update-ref`'s old id
(Codex X6). **Undo** is a recovery ref kept before every ref rewrite (D102's SQUASHTIDY1c note and CLEANUP1's pattern), named in
the act's sentence; a remote act has no undo and says so (D37).

| Act | Writes | Refused when | D198 | Undo | Row |
|---|---|---|---|---|---|
| Fetch (`origin --prune`, or a named remote) | remote refs, objects | no such remote; a held remote (GITAUTH1a) | Read | prune's removals named | 1e |
| Create a branch at a commit | a ref | exists; under `daoris/`; a name git refuses | — | delete | 1e |
| Rename a local branch | a ref, its configuration line | checked out anywhere; the line; a session or landed branch | — | rename back | 1e |
| Delete a local branch | a ref | as D147 §3.1 | — | the recovery ref | 1e |
| Push one branch at a commit | origin's branch | the line; a session branch; a tree holds it; not a fast-forward | Write | none (outward) | 1e |
| Open its pull request | through the hand-off's plugin (D100) | D100's | the plugin's grant | the host's | 1e |
| Create or delete a local tag | a ref | exists; for delete, a remote holds it | — | the recovery ref | 1e |
| Push a tag | origin's tag | not offered until GITAUTH1f lets the person set Dangerous | **Dangerous** | none | after GITAUTH1f |
| Cherry-pick, revert, rebase onto, by objects | objects, one ref | §7.3 | — | the recovery ref | 1f |
| Stage, unstage (file, hunk, line) | the checkout's index | the switch off; a conversation in the checkout; a conflicted file (resolve first); the shown bytes changed | — | the opposite press | 1g |
| Commit; amend the last | the checkout's branch | nothing staged; amend where a remote holds the commit; the shown staged snapshot changed | the author is the checkout's own configuration; D198 point 5 only where it names none | the recovery ref | 1g |
| Stash save, apply, drop | files, index, `refs/stash` | dirty conflict on apply aborts whole; drop is a second press | — | apply keeps the stash; drop keeps its commit in a recovery ref | 1g |
| Switch branch | files, index, `HEAD` | the tree dirty or in an operation; the branch checked out elsewhere | — | switch back | 1g |
| Resolve a conflict | one file, then `add` | a file not in conflict | — | the replaced bytes kept beside the operation until it ends | 1g |

The patch a hunk or line press applies is composed by Daoris from the shown hunk and applied with `apply --cached`, never through an
editor. **No `push -u`, no upstream tracking**: they write the checkout's configuration (D147's reason stands).

## 9. History at scale (Q7), the terminal door and Ask Daoris (Q8)

**History.** No index of Daoris's own (all three), no cache on disk, and **no `commit-graph` press** (Claude C6 and Fable F9 offered
one; Codex X7 refused). This repository already carries a `commit-graph` with changed-path filters (Fable F1), so a press gains it
nothing; the other repositories here are at most a third its size; nothing measured needs it. Where a history read is slow and the
repository has no `commit-graph` (read from `objects/info/` with no process), the History tab's head names
`git commit-graph write --reachable --changed-paths` to copy: the person's configuration decides, and D147 §4.2 stays unamended.
**A file's history** keeps its whole id list with each commit's path at `(commit, path)` from one `log --follow --format=%H
--name-status` walk (80 KB for 2,000 commits); a page is a slice, its fields read by one call or the held process (Fable F9).
Claude's cursor from the last id (C6) is rejected on Codex's ground: a cursor must be proved across renames and merges, while a
kept list is git's own answer sliced. **The pickaxe** is a long read, path-scoped, streamed, cancellable, with its time shown and
the command to copy, holding one of the reader's places (§4.2).

**The terminal door** (D50), one judge in the driver library for both doors:

- `daoris-driver git review <session> [--json] [--time]`: Review's answer; `--time` prints the rounds, each start's command, wait
  and run, what was kept, and writes `review.read`. The measurement door.
- `daoris-driver git show|history|blame|compare|search|stashes|tags <repository> …`: the reads.
- `daoris-driver git branches|fetch|branch|rename|delete|push|tag … [--plan|--yes]`: the ref and remote acts (GIT1c, GIT1g, GIT1h).
- `daoris-driver git pick|revert|rebase … [--plan|--yes]` (1f) and `daoris-driver git checkout <repository> stage|unstage|commit|
  amend|stash|switch|resolve … [--plan|--yes]` (1g), refusing under `DAORIS_SESSION_ID`.

Names are settled through the verb census (`cli-usage`) in their row. **Ask Daoris** (D110): the reads stay exempt, as `trace` is; a
`git` kind proposes the acts as cards the person presses (GIT1k, widened), never applying them; the Setup switch is proposed through
the setting door, never set. `HelpCoverageTests` holds each control to its kind or exemption.

## 10. The test plan (Q9)

### 10.1 Counts, load-independent, the fast half

Through the `GitRead` seam (REVIEW3's `TreeDiffTests` pattern) and `WorkingTree.Started`: a review open with 50 untracked files
starts **0** git per untracked file, **≤ 10** in all in the ordinary case and **≤ 11** with REVIEWBASE3, in **≤ 3 rounds** (a round
is a set of starts that overlap); each other path has its own ceiling; opened again with nothing moved: 2 (1a), 0 (1b); a landed
session: 0. A stand-in whose ref moves between resolve and read makes the answer name the new ids or read again, never the old ones
as current. **The ceilings fail the census on one start more**, which is how the cost stays down; each is shown to fail when a
per-file or serial start is put back.

`ReviewBaseTests` and `TreeUpdateTests`, unchanged, prove the range's semantics did not move. **REVIEWLOCK1's census**: every
`status` and `diff` the driver runs in a tree that is not its own write passes `--no-optional-locks`; a source census in the fast
half, widened to the reader in 1a. `ProcessStartsCensusTests` names no new direct start.

### 10.2 Real git, the Process half (`Parallel=Git`)

- **Byte-equality**: over untracked fixtures (ASCII, UTF-8 with a non-ASCII path, CRLF, no trailing newline, empty, a NUL inside,
  over 200 KB, a nested repository, a link, a file growing during the read), Daoris's patch equals `git diff --no-index -- /dev/null
  <path>`'s text for each text file and says binary, oversize, changing or not followed exactly where it should.
- **The stamp**: the snapshot drops on a loose ref write, `pack-refs`, a `HEAD` move and a linked tree's `HEAD` move, each by real git.
- **No lock**: a `status` and a `diff HEAD` in a tree whose index is stat-stale leave the index's write time unchanged; a stand-in
  holding `index.lock` fails neither Review nor itself (REVIEWLOCK1's Process case).
- **The context**: a repository with `refs/replace/` or `shallow` keeps nothing.
- **The acts** (1e-1g): bare-origin fixtures for push and fetch, the pre-push hook running; every refusal of §8 on a real fixture;
  a conflict refusing a pick whole; the recovery ref before every rewrite; the switch off refusing every checkout act; a stand-in
  conversation in the checkout refusing them.

Per the person's standing instruction on this machine, a row runs its own classes filtered and the censuses; the whole halves run
in CI after the merge.

### 10.3 The bench and the window

- **The bench**, reported, never failing: `daoris-driver git review <session> --time` over the three cases on this repository (368
  files; the same with 50 untracked made by the bench in `_fixtures/`; 1,771), at least ten runs each, median and p90 of wall, starts,
  rounds and `waitedMs`, with the machine's load beside (CPU, process counts). Before: the brief's numbers. After: §3.4's targets.
  Written to the row's fix-log entry with the sha.
- **The window** (GITAPP1i, the owner's): Review opened from the press, timed to first paint by `review.painted` through `logEvent`;
  the driver's `review.read` (`session`, `ms`, `starts`, `rounds`, `waitedMs`, `files`, `untracked`, `kept`, `head`, no path). Three
  opens before the republish and three after, under the owner's load.
- **Held afterwards**: the census is the gate; the bench's medians are read at each release rehearsal, and a rise past twice a
  target is a row, not a failure.

## 11. Surfaces in English and Chinese

Chrome only; commit messages, patches, file and branch names stay as written. Each new term passes D116's check before it is drawn,
git's own Chinese translation being the source where it has one. Existing glossary terms used as they stand: *review* 审阅, *branch*
分支, *tree* 工作树, *history* 历史, *graph* 提交图, *commit* 提交, *fetch* 获取, *push* 推送, *merge* 合并, *line* 主线, *landing*
落地, *git remote* 远程仓库.

| Surface | English | 中文 | Row |
|---|---|---|---|
| Review's head, a kept answer | Read at {time} · HEAD {id} | 读取于 {time} · HEAD {id} | 1a |
| Review's foot while its reads run | Reading… | 正在读取… | 1a |
| A file still changing | This file changed while it was read | 读取时该文件仍在变化 | 1a |
| Review's foot, the conflict preview | Would conflict in {n} files / Lands without conflict | 将在 {n} 个文件中冲突 / 可无冲突落地 | 1d |
| A commit's page | Commit · Parents · Held by · Made by | 提交 · 父提交 · 包含于 · 由谁创建 | 1d |
| A file row | Blame · History · Before this change | 逐行追溯 · 历史 · 此更改之前 | 1d |
| Compare | Compare with… | 对比… | 1d |
| Diff views | Inline · Side by side · Word diff · Ignore whitespace | 内联 · 并排 · 按词对比 · 忽略空白 | 1d |
| Search | Search messages, authors, paths · Search content (slow) | 搜索说明、作者、路径 · 搜索内容（较慢） | 1d, 1h |
| Branches tab | Stashes · Tags · Remotes · Worktrees · Reflog | 贮藏 · 标签 · 远程仓库 · 工作树 · 引用日志 | 1d |
| LFS | A file kept by LFS ({size}) | 由 LFS 保存的文件（{size}） | 1d |
| Ref acts | Rename · Create tag · Delete tag | 重命名 · 创建标签 · 删除标签 | 1e |
| History edits | Pick onto… · Revert · Rebase onto… | 拣选到… · 还原 · 变基到… | 1f |
| A refusal | Conflicts in {files}; nothing was changed | {files} 中有冲突；未做任何更改 | 1f |
| An act's undo | {commit} is kept as {ref} until {date} | {commit} 保留为 {ref}，直至 {date} | 1e-1g |
| Setup's switch | Daoris may change this checkout's files | 允许 Daoris 修改此检出中的文件 | 1g |
| The Changes tab | Changes · Stage · Unstage · Commit · Amend · Stash · Apply · Drop · Switch | 更改 · 暂存 · 取消暂存 · 提交 · 修订 · 贮藏 · 应用 · 丢弃 · 切换 | 1g |
| A hunk and a line | Stage hunk · Stage line | 暂存块 · 暂存行 | 1g |
| Resolving | Take ours · Take theirs · Take both · Open in your editor · Mark resolved | 采用我方 · 采用对方 · 两者都要 · 在你的编辑器中打开 · 标记为已解决 | 1g |
| History's head | To speed this up in your own git: {command} | 如需在你自己的 git 中加速：{command} | 1h |

*Submodule* (子模块) and *amend* (修订) are the two whose word D116's check settles first: git's own catalogue and the glossary's
neighbours are read then. Stories at 1546 and 680 px in both themes and both languages, and UX6a's counter, hold each screen.

## 12. The rows (Q10)

1. **REVIEWLOCK1** (filed; driver): `--no-optional-locks` on every read-only `status` and `diff` of a session's tree. First,
   because it is a defect in a tree that is the session's, and 1a's census extends it.
2. **GITAPP1a — Review fast** (driver, modules, web): `GitReader` and `GitReaders` with the layout from disk, the stamped snapshot,
   the shared cache with its context, in-flight joining and the bound; Review's chain in rounds (§3.1); untracked patches from disk;
   the offer and HANDOFF_PLAN after the diff; the answer and the page's key carrying `head`; *Read at*; `review.read`,
   `review.painted`; `daoris-driver git review --time`. **Proof**: §10.1's counts, §10.2's byte-equality, stamp and lock cases, the
   bench's before and after against §3.4, `ReviewBaseTests` and `TreeUpdateTests` unchanged. Amends D147 point 6 (§13).
3. **GITAPP1b — Review follows the tree** (after 1a): the ref watch widened and hearing tags; its hint publishing the `diff` scope
   for the sessions on the moved branch, with round 3 pre-read; the tree watch for open reviews; the page's refetch on `updated`
   removed. **Proof**: a fake change source moving a ref invalidates an open review's query and the next read serves 0 starts for the
   committed part; a file written under the tree drops the kept answer; an idle reopen starts nothing; a commit shown ≤ 3 s on the
   bench.
4. **GITAPP1c — the judgements through the reader** (after 1a): `help.*`, the tidy, the landing's plan, `GitBranches.ListAsync`
   and `LandedReviews` take layout and ids from the reader. **Proof**: PROC1a's start counts per judgement before and after; the
   offer's and LANDING's starts in `review.read`.
5. **GITAPP1d — the reads drawn** (after 1a; takes GIT1f and GIT1i): §7.1's 1d rows, the held `cat-file` measured against `git gc`,
   the conflict preview, the terms through D116. **Proof**: each screen's story and counter; `GitReadsTests` per read's one call and
   key; the hazard measured; `HelpCoverageTests`.
6. **GITAPP1e — the ref and remote acts** (after 1d; takes GIT1g, GIT1h, GIT1k): §8's 1e rows. **Proof**: as GIT1g and GIT1h name
   it, plus rename and tags; the terminal twins.
7. **GITAPP1f — history edits without a working tree** (after 1e): pick, revert, rebase onto, by objects, with the recovery ref.
   **Proof**: each over real fixtures; a conflict refusing whole with nothing moved; refused on a checked-out branch.
8. **GITAPP1g — the person's checkout** (after 1e): the Setup switch, the Changes tab, stage by file, hunk and line, commit, amend,
   stash, switch, resolve, the checkout's watch. **Proof**: §10.2's acts; the switch off refusing all; a conversation in the
   checkout refusing all; the staged bytes changed between plan and press refusing.
9. **GITAPP1h — history at scale** (after 1d): the file history's id list, the pickaxe as a long read, submodules, the commands to
   copy. **Proof**: the third page of `TASKS.md`'s history no longer walking from the top (one walk counted); the pickaxe cancelled
   ends its git.
10. **GITAPP1i — the install, seen** (the owner; after 1a, again after 1d and 1g): §10.3's window timings and the screens in both
    languages. **Proof**: the fix-log entry and its screenshots.

1a alone makes Review fast; 1b keeps it fast while a session moves; 1c pays the same debt where the landing and the judgements wait;
1d draws the half of D147 that is built and drawn nowhere.

## 13. Fit with D147, D150 and D198

- **D147** is amended, never rewritten: point 2 and design §2.8 (tags locally, stash, staging, commit, a three-way choice by hunk,
  cherry-pick, revert and rebase are now offered in §7-§8's forms; checkout's reset, clean, a force, a remote delete stay refused);
  point 3 and design §3.1 (acts may write the person's checkout behind its switch, and a history edit writes objects and one ref);
  point 6 and design §4.1 ("one process a view" becomes the reader: rounds of bounded starts, a held object reader for the screens,
  the refs snapshot stamped; still Tools' git and never a library). **§4.2 stands whole**: nothing written into `.git`, nothing on
  disk, a ref never a key. Design §3.1's "D51 rule 7" is corrected to D102's SQUASHTIDY1c note and CLEANUP1.
- **D113 §5** is amended by the switch: the checkout stays the person's, and Daoris acts in it only where the person set the switch.
- **D150 point 3**: a repository's page gains a **Changes** tab beside Branches and History, shown only where the switch is on. No
  Git place returns to the activity bar; the branch, then the commit, stays the organising object (D55); the Changes tab lists the
  checkout's changed files, reached through a change, never browsed; there is still no editor or file tree.
- **D198 stands**: every remote act passes the doors, judged Read, Write or Dangerous for the person's press as for the landing;
  tag push waits on GITAUTH1f; nothing prompts; a held remote is said once. A local act passes no door. The person's commit takes
  the checkout's own author configuration first (point 5).
- **D51, D55, D109, D164, D173, D181 stand.** D191's reads follow this round.

## 14. Rejected, by proposal

**From Claude's**: ≤ 4 starts and its 1.5 s target (§3.4: it drops REVIEWBASE1 and REVIEWBASE3); Review on the person's gate place
(§4.4); a held process for two idle minutes, eight across the machine (§4.3, the pack-hold hazard); the generation counter (§5.1); a
pushed slice per review, this round (§3.3); a tree watch on every running session (§6.3); the `commit-graph` press (§9); the file
history cursor (§9); a free three-way editor (D55, §8).

**From Codex's**: the manual worktree of the person's (§7.2); "four short jobs, two walks" as one global bound above the gate (§4.2,
made per reader); targets of 4-5 s (§3.4); agents' authorised local operations through Daoris (§7.4). Taken from it: the union
counts and every range path kept (§3.1), the offer and plans off the patch's path (§3.2), the context in the cache key and partial
clones (§5.2), *Read at* rather than *current* (§5.3), the refusal list (§7.2), serialised own writes (§8), the held-process cap.

**From Fable's**: the idle session tree's acts (§7.2); the offer in round 3 (§3.2); `ahead-behind` for both counts (§1); the
`commit-graph`, fsmonitor and untracked-cache presses (§6.1, §9); idle-machine targets as a held column (§3.4: reported, not held).
Taken from it: REVIEWLOCK1, the layout and refs from disk with the stamp, the rounds, the per-reader bound and Background class, the
held process's shape and its hazard, the kept file-history ids, the targets' medians, the bench, the tree watch for open reviews.

**By all three**: libgit2 or a reader of git's files (D147 §4.3); Daoris's own history index; a cache on disk; polling `status` on a
clock; a patch per file fetched from git; checking out in a session's tree; a force push; a delete on origin; a push by rule; line
comments; fetching on a timer; changing the gate's width.

## 15. Not measured

The installed window (Review's paint and the press-to-paint time); LANDING, HANDOFF_PLAN and the offer beside the diff; a tree with
hundreds of real untracked files; the held process's pack-hold hazard; the refs stamp on a reftable repository (none here); the
tree watch under an agent's dependency install; `GIT_NO_LAZY_FETCH` on this git; the gate's `waitedMs` under a sweep's fan-out; an
idle machine. Every target is set from the brief's and Fable's probes under the load they named; the rows measure the rest before
they are accepted.
