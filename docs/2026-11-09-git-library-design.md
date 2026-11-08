# One managed git in .NET: every Daoris git read and act through one service (GITLIB1)

**Round 4, proposed for both makers' reads before a decision is written** (2026-11-09). The owner's direction for it, on D147's note
of 2026-11-09: Daoris's git is free-flowing, not managed by .NET. Every read is a fresh process, any number run at once, nothing queues
them per repository, identical reads in flight each start their own git, and a read nobody waits on runs on. It is to be one git
service in .NET that every Daoris git read and act passes, so git is never started any other way. §14 to §17 are round 4: the
steward's choice drops the held object reader, and the round answers what both makers' reads of round 3
(`docs/2026-11-09-git-library-reads.md`) leave once it is gone, each finding placed in §15. Rounds 1 (§1 to §4), 2 (§5, §6, §8, §9)
and 3 (§10 to §13) are kept as history: their measures stand, and their design and rows are superseded where a later round differs.
§7, the owner's open question, is current. The code was read at `3d0f5916f5`; nothing in the product changed.

**In brief.** One service, `GitService`, is the only way Daoris's desktop starts git: a census of every start site, each seen failing
on a missed member, and a start gate that refuses a git image at every purpose of Daoris's own hold it, and the programs that start git
themselves are named. A scheduler runs every git Daoris starts: at most four at once, one always free for a person's read whatever else
runs, a queue per repository, a running act's hooks placed above the four so no act waits on its own git, and a git that outlived its
kill counted apart; an ask shares another's answer only where its own start would have been that start, not yet begun, and only a
read that takes no lock is ever ended. No git is held between questions: an object is one start of `git cat-file --batch` asked one
question through one parser, its type and size from the header. LibGit2Sharp, opened for one call and disposed at its end, answers
refs and their stored ids, the layout, merge-bases, counts and two commits' changed names where the repository's own files admit each,
read again at every question under a stamp of the inputs the asker's git reads. Walks, patches, a commit's fields, the status and
every working-tree read, the worktree list, remote acts, hook-running acts and writes stay git's binary, run by the scheduler. The
first row needs neither engine: Review's untracked files' patches become two starts through an index of its own, measured on the
install as the baseline the library must beat.

## Round 1

**Round 1, read by both makers.** The owner's direction is D147's note of 2026-11-09: git's starts
are the cost (many at once, Review's tree read slow), so Daoris's own reads go in process through a library, and git's binary keeps
every act that needs it. D147 §4.3, the built-in-git design §4.3 and the git-app proposals rejected LibGit2Sharp on a start assumed to
cost about 100 ms; §3 answers each reason. Measured on 2026-11-09 with `tools/git-library-bench` (§1), on this machine as the agents
load it (600 to 650 processes, 22 cores), from the code at `1cd07efab3`.

**In brief.** The library wins wherever the answer is small and loses where git's own work is large. A start costs 0.2 to 0.6 s here,
up to 1.7 s with every core busy and 6.6 to 13 s at the median when twenty start under that load, against a few milliseconds of git's
work, and the
library answers a ref, a merge-base, the counts or two trees' file names in under a millisecond to 55 ms. But on vscode (21,000 files,
170,000 commits, 6,358 refs) its walk of the commits no branch holds takes 20.4 s against git's 1.1 s, its patches of twenty commits
3.7 s against 1.4 s, and its status of a deep tree fails on a long path; there a whole Review is 25 s in process against 19.6 s through
git. It also errs on a filter driver and sparse checkout, and refuses reftable and SHA-256. So the questions whose cost is bounded by
their answer move to one reader per repository (Review's ten of sixteen starts, the look's sixteen of twenty-three); the walks, the
patches and every read of a working tree stay on git's binary, with every remote act, hook and write. A repository whose format the
library refuses keeps the binary for those questions too, a second path stated with the condition that ends it (§2.4).

## 1. What was measured

The bench is a console project referenced by no product project, LibGit2Sharp 0.32.0 pinned in it only (libgit2 1.8.6,
`LibGit2Sharp.NativeBinaries` 2.0.324). It starts git as `GitRuns` does: the real git behind Git for Windows' launcher with its
variables, pipes redirected, no window, `-c core.longpaths=true`. Its fixtures are clones under `_fixtures/gitlib` laid out as Daoris's
trees are: the line, and linked worktrees on `daoris/bench-<n>` branches, the first a session twenty commits past its base with five
changed files, one deleted and ten untracked. How to run it is in the area, *Git's starts, counted*.

### 1.1 A start

Twenty `git rev-parse HEAD` in this checkout, run twice an hour apart as the agents' load moved (first / second); git's own time is its
trace2 `exit` event's `t_abs`, which counts from git's `main`. Medians, the largest start after it.

| Starts | All twenty | A start | `Process.Start` | git's own | Outside git |
|---|---|---|---|---|---|
| `git --version`, one at a time | 9.1 / 9.3 s | 442 / 401 ms | 68 / 59 ms | 172 / 146 ms | 272 / 293 ms |
| one at a time | 14.2 / 5.0 s | 585 / 228 ms (2.7 s) | 229 / 20 ms | 141 / 99 ms | 461 / 107 ms |
| twenty at once | 2.7 / 1.7 s | 1.5 / 1.4 s | 1.1 / 1.2 s | 162 / 131 ms | 1.4 / 1.3 s |
| one at a time, every core busy | 39.1 / 9.9 s | 1.7 s / 461 ms | 483 / 124 ms | 191 / 118 ms | 1.5 s / 344 ms |
| twenty at once, every core busy | 21.9 / 32.5 s | 12.9 / 6.6 s (32.3 s) | 9.8 / 4.4 s | 179 / 156 ms | 12.7 / 6.4 s |

git's own time is mostly not the command: the trace splits it into 41 to 56 ms of setup before its `start` event, 52 to 92 ms in which
Git for Windows' trace walks the process list (`windows/ancestry`, paid only while tracing), and 1.6 to 8 ms of `rev-parse`'s work.
Under load the start is the cost, and most of it is `Process.Start` itself, waiting on process creation: the install's `createdMs` of
5.5 s a start is the same thing.

### 1.2 Review and the look, both ways

Review's reads are the sixteen its count test lists (`ReviewGitCountTests`), each untracked file's patch one start; the library answers
the same questions, the untracked patches in one call. The look is the worktree list, the refs, the line, the session branches no other
holds, and per tree its status, merge-base with the line and the files the line lacks. Five passes each; *first* is the first pass of a
fresh process (the library loading, compiling and opening; the system's file cache is not emptied), then the median of the rest.

| Repository | Read | Starts | Binary | Library, opened each time | Library, held |
|---|---|---|---|---|---|
| this one (8,991 files, 16,304 commits, 6 trees) | Review | 25 | 11.8 s, then 11.2 s | 2.2 s, then 0.92 s | 0.80 s |
| | the look | 23 | 17.8 s, then 9.7 s | 2.0 s, then 0.77 s | 0.55 s |
| vscode (20,996 files, 170,823 commits, 6,358 refs, 4 trees) | Review | 25 | 35.8 s, then 19.6 s | 14.4 s, then 30.0 s | 25.1 s |
| | the look | 17 | 24.1 s, then 14.5 s | 15.0 s, then 38.9 s | 28.4 s |

Per read, the library held against one start of the binary, this repository then vscode:

| Read | Binary | Library |
|---|---|---|
| layout, branch, origin's head, a merge-base, the counts | 300 to 440 ms; 450 to 990 ms | 0.1 to 0.7 ms; 0.2 to 3.4 ms |
| the refs (`for-each-ref`, 60 refs; 6,358) | 315 ms; 1.09 s | 6.8 ms; 1.02 s |
| the files the line lacks (`diff --name-only`) | 315 ms; 1.23 s | 9.8 ms; 55 ms |
| the commits no branch holds (`log --not --branches --remotes`) | 446 ms; 1.08 s | 4.2 ms; 20.4 s |
| the committed range's patches, twenty commits | 610 ms; 1.44 s | 422 ms; 3.67 s |
| the status with every untracked file | 685 ms; 1.10 s | 120 ms; fails |
| the uncommitted diff | 716 ms; 797 ms | 122 ms; fails |
| ten untracked files' patches (ten starts; one call) | 3.8 s; 7.2 s | 120 ms; not reached |

The library's walk hides each of the 6,358 tips one by one where git's walk is bounded by its commit-graph, and git's patches are
faster than libgit2's at every size; the library wins those only by the start, on a small repository.

### 1.3 Parity

`git-library-bench parity` builds a census repository with git and compares 54 answers as values. Equal (42): refs, the worktree list,
merge-base, ahead and behind, the commits no branch holds, the files the line lacks, a committed range's files and counts with a rename
and a case-only rename, a stored file and a checked-out one under `eol=crlf`, `eol=lf`, `core.autocrlf`, `ident` and `binary`; in the
line's checkout, a session's linked tree, a detached linked tree and a sparse clone: head, branch, ignored files, and in the first three
the status with untracked files nested, in CRLF and binary, a file renamed by case on disk under `core.ignorecase`, and each untracked
file's patch. The differences, every one:

1. **The rename's score.** A rename's patch says `similarity index 96%` where git says 95%: libgit2 scores similarity its own way. The
   pair and the counts are equal.
2. **A filter driver** (`filter.<name>.clean` and `smudge`; Git LFS is one): the library runs none. A filtered file whose stat changed
   reads as modified, with a diff of its stored form against its checked-out form; a checked-out read gives the stored bytes.
3. **Sparse checkout.** Each file outside the cone is reported deleted, in the status and in the diff against the tree.
4. **A path past 260 characters in a deep tree.** In vscode's linked tree (a 102-character prefix) the status with untracked folders
   searched, the status of a tree's head and branch and the diff against the tree fail with `path too long`, where git, given
   `core.longpaths`, reads them; the repository's own `core.longpaths=true` does not help.
5. **The binding's diff against the working tree** lists each untracked file as an addition, which `git diff HEAD` does not. A file
   whose stat alone changed is listed by git and not by the library; Daoris's reader drops it already.

### 1.4 What the library cannot open or read

`git-library-bench opens`, each case made with git 2.53 and read by the library first:

| Repository | git | The library |
|---|---|---|
| a full clone; objects shared (alternates); a shallow clone; `extensions.worktreeConfig`; a commit-graph and a multi-pack index; `feature.manyFiles` (index v4, no index hash, untracked cache) | reads | reads |
| a partial clone (`--filter=blob:none`; git 2.53 writes no `extensions.partialclone`) | reads, fetching a missing object from its promisor | opens; a missing object is `object not found` |
| reftable refs | reads | refuses (`extensions.refstorage`); allowed, reads no ref |
| SHA-256 objects | reads | refuses (`unknown object format`), allowed or not |
| an extension neither knows | refuses | refuses |
| a split index; a sparse index | reads | head and objects read; status and every diff fail (`unsupported mandatory extension`) |
| another account's folder | refuses (`safe.directory`) | refuses (`not owned by current user`) |

libgit2's changelog through 1.9.7 names no reftable support, and its SHA-256 is a build option LibGit2Sharp's binaries do not enable.
LibGit2Sharp 0.32.0 carries libgit2 1.8.6; nothing newer is packaged.

### 1.5 Size and threads

The install ships `win-x64` only: `git2-5853918.dll` is 2.0 MB and `LibGit2Sharp.dll` 0.5 MB (the other runtimes are 1.2 to 2.7 MB each).
The binaries carry threads, HTTPS and SSH. A repository opens in 5 to 6 ms warm (27 to 32 ms the first time) and holds 3.1 MB of
private memory after a status on this repository, 12 MB on vscode. libgit2 shares no object between threads, so a repository is one
thread's at a time. Reviews on threads of their own, a repository each: on this repository one 1.13 s, four 0.93 s, eight 1.4 s; on
vscode one 30.4 s, four 35.8 s, eight 47.6 s, the walk's CPU shared.

## 2. The design

*Round 1's, superseded where round 2 differs: the pool (§2.2) by the lease per flow (§5.6), the worktree list and a commit's fields
(§2.1) by §5.8 and §5.5, the refused repositories' path (§2.4) by §5.9, the census (§2.5) by §5.10.*

### 2.1 Which reads move

The questions whose cost is bounded by their answer, where the library is equal to git in every repository it opens (§1.3) and
milliseconds against a start: the layout (`rev-parse --git-common-dir --show-toplevel --abbrev-ref`, `symbolic-ref`), the ref listing
and a name's commit (`for-each-ref`, `rev-parse --verify`, `show-ref`), the worktree list, `merge-base` in all its forms, the counts
(`rev-list --count` both ways, one divergence), a commit's fields and parents, two trees' changed files without patches (`diff
--name-only`, `--raw`), and a stored file's bytes. These are Review's ten of sixteen starts, the offer's four of six, the look's sixteen
of twenty-three, the Git page's branches, and the judgements' ref and merge-base reads before a landing. Each moves whole: the binary's
path for that read is deleted in the same row (D228), and a source census refuses the command started as a read anywhere else.

### 2.2 Where the library sits

One reader per repository in the driver library, beside `GitReads` and keyed as `GitReadCache` is, by the common git directory: it
holds a small pool of repositories (one per thread in use, at most four, closed after a minute idle, opened per linked tree as it is
asked), and answers by meaning (`Layout`, `Refs`, `MergeBase`, `Counts`, `ChangedFiles`, `Blob`), never by git's arguments. `GitLaunch`
counts it as an in-process read with its time, so `review.read` and the count tests say starts and reads apart. It reads the
configuration Daoris's git reads: libgit2's global search path names a folder of the home holding one `.gitconfig` that includes
`profiles/git/global.gitconfig`, and owner validation stays on, so `safe.directory` judges as it does for git. The package is pinned
in the driver only; its native library joins the install beside SQLite's.

### 2.3 What stays on git's binary

- **The walks**: `log` and `rev-list` over a set of tips with exclusions (the commits no branch holds, the session branches no other
  holds), a file's history, blame and the graph's pages. git's walk is bounded by its commit-graph; libgit2's is 20 times slower past
  6,358 refs.
- **The patches**: every `diff -p`, a range's, a commit's page, a compare's and the uncommitted one. git's diff is faster at every size,
  and its rename score is the one Daoris shows.
- **Every read of a working tree**: the status (Review's two become one `status --porcelain=v2 --branch -z --untracked-files=all`),
  each untracked file's patch, a checked-out file, and the set-aside and pool judgements' `ls-files -v`. git's answer there depends on
  filters, sparse checkout, the index's format and long paths, where the library errs or fails (§1.3, §1.4).
- **Every act that talks to a remote** (`GitWiring`, the doors), every act that runs a hook, every write of a tree, an index, a ref or
  an object (`merge-tree --write-tree` included), and every start an agent makes.

### 2.4 A repository the library cannot read

The reader opens a repository once and admits it by the census's own checks: its object format, its ref storage, every extension it
declares, and its index's mandatory extensions. A repository admitted is read by the library alone for §2.1's questions. One refused
(reftable, SHA-256, an extension libgit2 does not know, a split or sparse index) keeps the binary's reads for them, said once in the
machine log (`git.read.binary`, its reason) and in the repository's Git page head. Under D228 that is a second path, kept with the
condition that ends it: the opens census reads every refused format equal on the pinned LibGit2Sharp. Its row runs the census at each
upgrade of the package and deletes the path when it passes. If git makes reftable its default before then, the path stops being rare,
and the steward weighs retiring the library instead (§2.7's fallback becomes the one way).

A missing object in a partial clone is not a refusal: the library says the object is not here, and the view says so. A read never
fetches; git's lazy fetch from a promisor is a remote act that no door judges as one.

### 2.5 The parity census

The bench's census becomes a test class in the driver's tests (`GitLibraryParityTests`, in the git `Process` suite): the census
repository made with git, each moved read asked of the reader and of git, compared as values, for each tree kind (the checkout, a
session's linked tree, a detached one, a sparse clone, a partial clone, a shallow one). A difference is listed in the class with its
reason, and a new one fails it. The opens census becomes the admission's test, and the bench's per-read times become a measured row
of the area's table at each package upgrade.

### 2.6 The starts tables

The area's table gains a column *in process*. Review becomes 5 starts plus one an untracked file (10 in process), 4 once its two
statuses are one; the offer 2 (4); the look's tidy 1 a tree and one walk (the rest in process); Branches 0 (3); the commit, switch and
stash plans their status and log only; a landing's reads of refs and merge-bases in process, its walks, patches, writes and gates'
starts as they are. The count tests hold both columns as budgets.

### 2.7 Rejected

- **Every read in process, the walks, patches and working tree's included, with the binary where the library errs.** On vscode the
  walk alone is 20 s against 1 s and the tree reads fail on a long path; the errors are in trees Daoris reads every day (a long path in
  a deep tree, Git LFS), so the second path would be common: two full implementations, §4.3's objection.
- **Refusing a repository the library cannot open.** Review and the Git page would go blank on a repository the agents work in.
- **Fewer starts and a held `cat-file --batch-command`, without a library.** Review folds to about eight starts (one status, the counts
  in one `rev-list --left-right --count`, the layout read from disk), and the held reader answers object questions, which Review asks
  none of. At 1.5 to 13 s a start under load that leaves seconds to a minute; it is the fallback if §2.4's condition goes the other way.
- **An exclusion from the scanner for `git.exe` or the trees.** It is the person's machine and their call; Daoris may say once where a
  start costs seconds, and never changes it.

## 3. D147 §4.3's reasons, answered

*Round 1's answers; §6 reads them again.*

1. **"The start it saves is not the cost."** For a small answer it is: 0.2 to 0.6 s a start here, up to 1.7 s with cores busy, 6.6 to
   13 s median twenty at once under that load, against a command's few milliseconds; the install's log says 5.5 s a start. Parallelism does not hide
   it, as the creation serialises. For a walk or a patch git's work is the cost, and it stays git's.
2. **"Credentials and SSH are git's."** Unchanged: no remote act moves, and the reader opens no transport (the binaries now carry SSH,
   which Daoris does not use).
3. **"Hooks."** No act that runs one moves; git runs no hook for a read either.
4. **"Repositories git opens and libgit2 does not."** Measured (§1.4): a partial clone opens and reads what it holds; reftable, SHA-256,
   a split or sparse index are refused and keep the binary's reads under §2.4's condition.
5. **"Worktrees."** Measured equal: the worktree list, a linked tree's head and branch, detached or on a branch.
6. **"No surprise."** The moved reads read the same refs and objects git does, and the census holds them equal; every read whose answer
   depends on the agents' working tree, and every walk and patch, stays git's.
7. **"Size."** 2.5 MB on the one platform the install ships.

The git-app proposals' "the held `cat-file` gives the same speed" holds for object questions, and Review's are refs, merge-bases,
counts and names, which it does not answer. Their "parallelism already hides the latency" is measured false under load.

## 4. The rows

*Round 1's, superseded by §9's.*

- **GITLIB1a — the reader and its admission.** The package in the driver, the reader per repository with its pool, the configuration
  and owner validation as git's, the admission of §2.4 with `git.read.binary`, counted by `GitLaunch`. Proof: the opens census as the
  admission's test; the pool's bound and close held by a unit test.
- **GITLIB1b — refs and the layout.** `RefSnapshot`'s listing, the layout, `symbolic-ref`, `rev-parse --verify`, the worktree list.
  Proof: the parity class's refs and worktree rows; `ViewGitCountTests` and `TidyGitCountTests` lowered.
- **GITLIB1c — merge-bases, counts and a commit's fields.** Every `merge-base` and `rev-list --count`, a commit's facts. Proof: parity
  on the census and on a merge-heavy fixture; `ReviewGitCountTests` and `OfferGitCountTests` lowered.
- **GITLIB1d — two trees' changed files and stored bytes.** The name-only and raw diffs of two commits, a stored file's content. Proof:
  the parity class's rows; the look's count lowered.
- **GITLIB1e — the working tree's reads cut.** One status for Review and the offer, read once a pass. Proof: the count tests.
- **GITLIB1f — the refused formats' path.** Re-run the opens census and the per-read times at each package upgrade; delete §2.4's path
  when every refused format passes, and say where a walk or a patch would now win in process.

Measured on one machine under its agents' load, on two repositories, five passes each; the system's file cache was not emptied, so
*first* is a fresh process, not a cold disk. Nothing in the product changed.

## 5. Round 2: one git service

*Round 2's, superseded where §10 differs: §5.3's starts by §10.7; §5.4's merge, epoch, ending and repository bound by §10.2 and
§10.6; §5.5 by §10.4; §5.6's lease per flow and its admission at the lease's open by §10.5 and §10.3; §5.9's probe by §10.8; §5.10's
census and hold test by §10.1 and §13's proofs.*

### 5.1 Round 1's measures, read again

- §1.1's starts were traced, so each includes the 52 to 92 ms in which Git for Windows' trace walks the process list; a product start
  pays none of it. Under load the start is the cost still.
- §1.2's whole flows measure neither today's product nor the split. The bench's Review asked its sixteen reads one after another and
  read the status, the patches and the walk in process; the product's Review is a graph (`ReviewGraphReads`) whose ref reads run beside
  the chain W → S → U₁…Uᵣ → M → D → the offer (17 starts in `ReviewGitCountTests`). The bench's look read every tree's status; the
  product's tidy reads none (`AutoTidies`). With the walks, patches and tree reads on git, Review's gain is about half (Opus: about
  14 starts and 6.3 s here with ten untracked files, the ten ref starts' creation gone from the runtime's one lock), the untracked tail
  becomes its largest share, and the look's tidy falls from 20 starts on six trees to about two, the worktree list and the walk.
- The refs listing that barely won on vscode (1.02 s against 1.09 s) looked each ref's object up; `for-each-ref`'s `%(objectname)`
  reads the stored id. GITLIB1d measures the listing reading ids alone.
- A commit's fields and a stored file's bytes were not measured in process. Round 2 gives both to the held object reader (§5.5),
  which is git.
- Branches' one listing (`GitBranches`: `for-each-ref` with `%(ahead-behind:<line>)` and `%(worktreepath)`) is a walk over every tip,
  which git's commit-graph bounds and §1.2's walk shows the library's does not. It stays git's.

### 5.2 Today, by the code

| Where git starts | How | Gated |
|---|---|---|
| `GitRuns`' three runners (`Whole`, `Streamed`, `Bytes`), beneath `WorkingTree`'s door and `GitLaunch`'s count | `ChildProcess`, `ProcessStarts` | the start alone, always `Background` |
| An act's effect step (`ActSteps.HeldGitAsync`) | `HeldChild`: `CreateProcessW` suspended into the act's job | not at all (`ProcessStartsCensusTests` names it) |
| A pool claim's git (`PoolGit`) | `HeldChild`, into the claim's named job | not at all |
| An HTTPS identity's test (`GitIdentityHttps`, `ls-remote --heads`) | `GitIdentityActs`' runner | the start alone, a person's |
| The version probe (`GitVersions`, `--version`) | `ToolQuestions` | the start alone |
| The doors' program, the service host's `IgnoredPaths` (`ls-files`) and `UncommittedRecords` (`status`) | programs of their own | outside this process |

So only the start is gated, never the run (`ProcessStarts`' remarks); a person's git waits as the background's; nothing joins two
identical reads across flows (`GitFacts`, `RefSnapshot` and `GitReadOnce` hold one answer within a flow; `HeldAnswers` joins a route's
work under way for its key); a held work runs with no token (`HeldAnswers.Begin`), so one superseded by a newer work of its key, whose
answer can no longer land, runs to its end; and a speculative read of Review's graph whose condition went the other way runs on.

### 5.3 The service

`GitService` (`Workplace/Git/`, its units beside it) is the one door: every git read and act Daoris's code makes asks it, and nothing
else starts git or opens the library (§5.10's census). `WorkingTree`'s git members become one line each to it; `GitRuns`' three runners,
the act step's held git and the pool's become the scheduler's start kinds (`Whole`, `Streamed`, `Bytes`, `Held`), and the identity
test's `ls-remote` and the version probe its asks. It is asked two ways, never two ways for one question:

- **by meaning**, for the questions the engines answer: `Layout`, `Refs`, `Ref`, `MergeBase`, `IsAncestor`, `Independent`, `Count`,
  `ChangedNames` (§5.6), and `Blob`, `Commit` (§5.5). An object is the object reader's in every repository; each other question of a
  repository has one engine, chosen by the admission (§5.6): the library where admitted, else the scheduler running the command
  today's code runs (§5.9);
- **by command**, for everything else (§5.8), run by the scheduler as written.

The flow's memos stay its front (`GitFacts`, `RefSnapshot`, `GitReadOnce`: one answer a flow). `GitLaunch` stays the one count beneath,
counting a start, an in-process read and a joined ask apart, so the count tests hold all three as budgets. The test seams stay one:
a stand-in answers a start by its arguments or a question by its meaning, and either is counted.

### 5.4 The scheduler

Every git process Daoris starts runs through it, whichever start kind.

- **Places, for the run.** Four gits run at once in the process: a git holds its place from before its creation to its end, and its
  creation inside the place still passes `ProcessStarts`, which shares the runtime's one start lock with every other start. The
  background holds at most three, so a person's git always finds one, and a place that frees goes to a person's ask first. A git whose
  run waits on what is outside Daoris (a remote act, an act that runs a hook) holds at most two, so two places always serve reads and a
  hook that asks Daoris for a read never waits behind its own act. The scheduler replaces `GitRuns`' entry at the gate, which says
  `Background` for every git, with the asker's class. Four is the start gate's figure; GITLIB1b measures six beside it on the install.
  The bound is the process's, as the start lock is: a terminal verb reading in its own process is bounded there.
- **Whose.** From the flow, as `GitLaunch.Within` carries a budget: a bridge route's work, held or not, and a terminal verb's are a
  person's; the loop's, a tick's and a workflow's are the background's. A merged read takes the highest class among its askers.
- **A queue per repository.** Asks wait per repository, keyed by the common directory (`RefFiles.CommonDirOf`, the checkout lock's
  key). A place that frees goes to a person's oldest ask, else to the next repository in turn with an ask waiting. A repository runs
  at most two of the background's at once, so one repository's fan-out never holds every place.
- **Identical reads, merged.** A read asked while the same read is queued or running joins it: the same git in use (`GitInUse`), folder,
  arguments and start kind, with no variable, input or bound of its own (the cases `GitAsync` already asks as written), and no write
  begun in that repository through the scheduler since: every git `GitFacts.Ran` says may move a ref starts a new epoch, so a flow
  reads its own writes. A streamed read joins nothing, since each reader stops where it has enough.
- **A read nobody waits on, ended.** Each ask carries its asker's token; a read every asker has left is dropped while queued and its
  tree ended while running (`ChildProcess`' kill). The askers that leave: a held work superseded by a newer one of its key
  (`HeldAnswers`, `RouteFlights`), handed a token for it; Review's speculative reads whose condition went the other way (`ReviewGraph`
  counts them wasted) and its reads still running when the open failed; a route's request ended with the window. A write or an act is
  never ended because its asker left, and a judgement's reads after a write pass no token, as today, each named in the census.
- **Bounds and times.** Each git keeps its kind's time bound (`GitBounds`). `GitLaunched` gains the queue's wait beside the gate's,
  the creation and the run, and `review.read` says it (`queuedMs`).

### 5.5 The held object reader

One `git cat-file --batch-command` per repository, a client of the scheduler. It starts when an object is asked of a repository with
none running, holding one place, answers every object asked of that repository while it runs, from any flow, and ends as its queue
empties, and in any case after ten seconds, the next ask starting a fresh one: no pack, commit-graph or multi-pack index it maps
outlives its batch, since a held pack is git's hazard too while git runs.

- **A stored file's bytes** (today's `cat-file blob` and `show <commit>:<path>`): `info` first; a blob within the asker's limit is read
  through it, and one over the limit is read for its first bytes and the reader ended rather than drained, as `GitBytesAsync` ends git
  today. No blob is read whole into the desktop's memory past its asker's bound, and bytes are never decoded on the way.
- **A commit's fields** (tree, parents, author, committer, message), parsed from the raw object, each compared by the parity class with
  the `log` format today's code reads (an `encoding` header and a `.mailmap` among the census's commits) and moved only when equal.
- **It is git.** Replace refs, reftable, SHA-256 and a partial clone's fetch of a missing object from its promisor through the doors are
  git's, so it needs no admission. An object the promisor could not give is `missing`, never an absent path: `FilePreview` says the file
  could not be fetched, not `NotOnBranch`, and nothing judges by its absence.
- A tree's listing stays a start (`ls-tree -r` is a walk of trees). Below git 2.36 the reader's questions refuse as `git-too-old`, as a
  git setting refuses below 2.32 (the area's rule 14).

### 5.6 The library per flow

LibGit2Sharp 0.32.0 is pinned in the driver only; its native library is laid beside SQLite's in the `win-x64` publish.

- **A lease per flow.** A flow (one Review open, one look pass's repository, one judgement, a page's read) leases a repository at its
  first library question and disposes the lease at its end; a question outside every flow leases for itself. A lease is one
  `Repository`, opened per linked tree as asked, its calls one at a time under an asynchronous lock that survives a thread switch, its
  disposal waiting on the call under way; its calls run on the thread pool, never the caller's thread. No lease lives past ten seconds
  (a longer flow takes a fresh one at its next question) or across a gate's run, an agent's turn, a person's answer or a network act.
  At most eight are open in the process, about 12 MB each on vscode (§1.5), a person's first. libgit2's Windows share mode shares
  delete (`GIT_OPT_SET_WINDOWS_SHAREMODE`), so a file git removes while a lease holds it goes when the lease lets go instead of refusing
  git. A lease costs 5 to 6 ms warm to open, about 0.12 s a Review here (Opus).
- **Nothing of the binding leaves it.** Every answer is a record of strings, ids and counts made inside the call; no LibGit2Sharp object
  or type crosses the lease (`Commit.Tree` loads lazily), and §5.10's census refuses the namespace outside `Workplace/Git/`.
- **Admitted per question**, at each lease's open, from the repository's own files: its format version and extensions, its ref storage
  and object format, `refs/replace/` and `info/grafts`, a promisor remote and its filter. No library question reads the index, so its
  extensions do not count. A cell that reads *library* is the parity class's to hold; one that differs moves to git in its row.

| Repository | Refs, the layout | Merge-bases, counts | Two commits' changed names |
|---|---|---|---|
| A full clone, a linked tree, alternates, shallow, `extensions.worktreeConfig`, a commit-graph or multi-pack index, `feature.manyFiles` | library | library | library |
| A split or a sparse index | library | library | library once the opens census reads a name-only diff there equal; git until then (§1.4's diff failed there) |
| A partial clone whose filter keeps trees (`blob:none`, `blob:limit`) | library | library | library |
| A partial clone with any other filter (`tree:<depth>`) | library | library | git |
| Replacements (`refs/replace/`, `info/grafts`) | library | git | git |
| Reftable, SHA-256, an extension libgit2 does not know | git | git | git |
| A repository a native fault was journaled in | git | git | git |

A missing object in an admitted repository is `ObjectMissing`, never an absent path nor an unheld ref: that ask goes to git through the
scheduler, which fetches from the promisor through the doors as today's read does. A merge-base with more than one best base (a
criss-cross) goes to git too, whose choice among them is its own.

- **Configuration as Tools' git reads it.** libgit2's search paths are pointed at the files Tools' git reads for the same start, never
  libgit2's own search: that git's system file (asked of it once per git in use), the global file the start is handed
  (`GIT_CONFIG_GLOBAL` where `GitGlobalFile.Hand` hands one, else the person's own as git finds it), and no program-data file where
  that git reads none. Owner validation stays on, so `safe.directory` judges as it does for git. The keys the library's questions read
  (`safe.directory`, `core.ignorecase`, `core.precomposeunicode`, an `includeIf` that sets one) are set at each level in the parity class.
- **A native fault.** libgit2 runs in the desktop's process, so a fault there ends the process and the window with it, and .NET cannot
  catch it. The first is accepted: the calls are libgit2's read-only refs, walk and tree diff over repositories the admission read,
  and a helper process to contain it would bring the start back and hold the packs. The repeat is contained: the process appends each
  lease's open and close to a journal of its own under `run/git/` (one write each, which the process's death does not lose), and a start
  that finds a dead process's journal ending inside a lease refuses those repositories from the library until the package changes, said
  once in the machine log (`git.read.binary`, `fault`). A repository that breaks libgit2 cannot end the window at each open.
- **Counted.** `GitLaunch` counts each library answer as an in-process read with its time; `review.read` says starts and reads apart.

### 5.7 The untracked cut, first and without either engine

Review's untracked files' patches are one start each today (`TreeDiffs.UntrackedReadAsync`, `diff --no-index`). They
become two starts whatever their count, in Π's place after S: the fold's paths (`UntrackedFold`, its file limit and patch budget as
now) are added as intents (`add -N`, read from its input with `--pathspec-from-file`, so no command line bounds them) to an empty index
of the read's own under the home (`GIT_INDEX_FILE`, as the piece keeper's, removed after; `core.splitIndex=false`, so nothing is written
beside the repository's index), and one `diff` of that index against the tree, with today's untracked read's flags, is split into the
fold by its file headers. A clean filter and textconv run in it as in today's read. With it, Review and the offer read the tree's
status once a pass (round 1's GITLIB1e).

### 5.8 What stays git's binary

- **The walks, the patches and the tree's reads**: `log` and `rev-list` over tips with exclusions, a file's history, blame, the graph's
  pages, every `diff -p`, a diff that detects renames (§1.3's score), the status and every read of a working tree (`ls-files -v`, a
  checked-out file), `ls-tree`, `merge-tree --write-tree`, and Branches' listing with its distances (§5.1).
- **The worktree list** (`TreeWorktrees`). It is the one guard of a branch delete, since the delete's `update-ref` transaction refuses
  no branch checked out elsewhere; libgit2's list leaves the main checkout out, and its model of a locked, prunable or missing tree is
  not git's; and it is one start a flow. GITLIB1d proves the deletes still refused once the refs beside it move.
- **Every remote act, every act that runs a hook, every write** of a tree, an index, a ref or an object, and every git an agent starts,
  which is the agent's own.
- **Outside the service**: the doors' program (git's own children) and the service host's two reads, a program of its own that runs
  where no desktop does; §5.10's census names them with why.

### 5.9 The second way, and its two ends

For a question the admission refuses, the scheduler runs the command today's code runs: one question, two engines by repository.
D228 counts that a second way, and D232 deleted the register its condition would have been written in. It is kept with both ends,
each held by a test that fails when its end is reached, and said in the area's rule when GITLIB1d lands:

1. **Deleted** when the opens census and the parity class read every refused case equal on the pinned package. `GitAdmissionTests`
   holds each refusal of §5.6's table and fails when one passes, naming it; GITLIB1g reruns it at each package upgrade and deletes
   what passed.
2. **The library retired** for §2.7's fallback (the scheduler and the object reader alone, which GITLIB1a to c build anyway) at the
   first Tools git that defaults to reftable or SHA-256, git 3.0's announced defaults for new repositories. The service makes a scratch
   repository under the home with each new git in use (`GitInUse`: its file and write time, so the global file's
   `init.defaultRefFormat` and `init.defaultObjectFormat` count), reads its formats and removes it; such a default is said once in the
   machine log (`git.read.formats`). In CI, `GitAdmissionTests` makes one with the runner's git and with the built-in list's pinned
   MinGit and fails on either default, opening GITLIB1g's retirement.

Nothing the person sees differs between the two engines, since the answers are git's either way, so neither end is a page's.

### 5.10 The proofs

- **The parity class**, `GitLibraryParityTests`, a `Process` class in the git gate: the census repository made with git; each moved
  question asked of the service and of git, compared as values (bytes as bytes, paths as git gives them, never normalised), over every
  kind of §5.6's table. Refs: loose, packed, a loose one over a packed one, symbolic, broken, an annotated tag peeled, case-only names
  under `core.ignorecase`. Merge-bases: two tips, `--is-ancestor`, `--independent`, a criss-cross whose two bases are committed in the
  same second. Counts: one divergence, a count over two refs (`ReviewGraphReads`' N↑), several tips with exclusions, a shallow
  boundary, a range of tens of thousands on vscode, timed. Changed names: a case-only rename as two names, a mode change, a type change,
  a gitlink, a path in NFD, a path past 260 characters. The object reader's bytes and commits as §5.5 says, and a partial clone's
  filtered blob fetched from the stand-in SSH host, then asked with the host down. A difference is listed with its reason; a new one
  fails.
- **On Windows.** The release workflow runs the git gate on ubuntu. The parent's merge runs it on Windows already, and the workflow's
  Windows job (`desktop`) gains a step running the library's `Process` classes, filtered to them.
- **The hold**, `GitLibraryHoldTests` (`Process`, Windows): a repository with two packs, a commit-graph and a multi-pack index, a lease
  mid-flow (and in a second case the object reader mid-batch); then `git repack -ad`, `git gc --prune=now` and `git commit-graph write
  --reachable`, with `GIT_ASK_YESNO` naming a stand-in that records every ask. Each exits 0, the stand-in is never asked, and once the
  lease or the batch ends no old pack, pack index or graph file remains; the same again with a commit running git's auto maintenance
  (`gc.auto=1`), as a session's does.
- **The census**, `GitStartsCensusTests`: no git started outside the scheduler, no `LibGit2Sharp` outside `Workplace/Git/`, every read
  handed its asker's token but the judgements named; `ProcessStartsCensusTests`' entry for `GitRuns` moves to the scheduler, its class the
  asker's.
- **The install's measures.** Each row is measured on the install before and after it with `daoris git review <session> --time`, five
  opens of one session tree: the median of `review.read`'s `ms`, of its starts' `createdMs` and of `queuedMs`, and the tidy's starts a
  pass, written into the area's table. GITLIB1a to c's figures are the baseline; a library row that does not shorten them is not taken.

## 6. The reads answered

*Round 2's answers to the reads of round 1; §11 answers the reads of round 2.*

| Finding | Round 2 |
|---|---|
| **Packs held open** (Opus blocking 1, Codex 3) | A lease per flow, at most ten seconds, never across a wait on anything but git, delete shared (§5.6); the object reader ends as its queue empties (§5.5); a lease's calls exclusive across a thread switch, its disposal waiting on the call under way; proved by the hold test on Windows (§5.10) |
| **A second path** (Opus blocking 2) | One second way, with both ends held by failing tests and said in the area's rule, since D232 deleted the register (§5.9); split and sparse indexes admitted for the questions that read no index (§5.6) |
| **The worktree list's parity** (Opus blocking 3, Codex 1) | Kept on git (§5.8); the deletes proved still refused for the main checkout, a locked tree and a missing one (GITLIB1d) |
| **Replace refs** (Codex 2) | Refs and the layout in the library; merge-bases, counts and names git's; the object reader is git; admission read again at every lease, so a change of state is seen at the next flow (§5.6) |
| **Partial clones** (Codex 4, Opus) | Refs, the layout, merge-bases and counts; names where the filter keeps trees; a missing object never an absent path, asked of git, which fetches through the doors; bytes from the object reader, which fetches as git does; `FilePreview` says a fetch failed (§5.5, §5.6) |
| Merge-base and count cases (Opus, Codex) | The parity class's rows (§5.10); several best bases go to git (§5.6) |
| Configuration (Opus) | libgit2 pointed at the files Tools' git reads, the global file as each start is handed it (§5.6) |
| A native fault (Opus) | The first accepted, the repeat contained by the journal (§5.6) |
| Threads (Opus, Codex) | No binding object leaves a lease; a census (§5.6, §5.10) |
| The parity class on Windows (Opus) | The merge's Windows run and a step in the workflow's Windows job (§5.10) |
| The wins on the install (Opus) | `review.read`'s `ms`, `createdMs` and `queuedMs` before and after each row; a library row that does not shorten the baseline is not taken (§5.10) |
| Large blobs (Opus) | The object reader reads a blob past its asker's limit for its first bytes and ends (§5.5) |
| A commit's fields and bytes unmeasured (Codex) | Given to the object reader, which is git; proved by the starts it saves (GITLIB1c) |
| Counts and ancestry are walks (Codex) | Timed on vscode in the parity class; a family slower there than one unloaded start stays git's (GITLIB1e) |
| The Branches query (Codex) | Its listing with distances stays git's (§5.1) |
| The native load from the published install (Codex) | The deployment rehearsal reads refs through the published driver (GITLIB1d) |
| Byte comparisons, status parity (Codex) | Bytes compared as bytes; the status stays git's, and the untracked cut is compared by its fold (GITLIB1a) |
| Traced starts and whole-flow figures (Opus) | Read again (§5.1) |

**D147 §4.3, read again.** The start's cost: answered, its figures corrected (§5.1). Credentials, SSH and hooks: unchanged, no act
moves. Repositories libgit2 does not open: one second way with two ends (§5.9), a partial clone admitted question by question. Worktrees:
the list stays git's. No surprise: refs and objects as git reads them, the configuration git's files, replacements git's. Size: 2.5 MB on
`win-x64`. The held reader's lifetime and the repack hazard, which round 1 left out: §5.5, §5.6 and §5.10.

## 7. The owner's open question: Daoris's own managed git by default

D121 offers a managed git (one exact version Daoris downloads and verifies, `tools.json`'s *managed*) and never switches to it. Whether
it should be the default is the owner's to decide, and this design does not decide it. What each answer changes here:

- **The person's git stays the default**, as today. The scheduler starts whatever Tools resolves, which is the agents' git too (D121
  point 3), so Daoris's reads and the sessions' git agree. The design stands as written: the floors are checked per git in use (the
  doors' 2.31, and the first release that reads `GIT_NO_LAZY_FETCH` for a read in a partial clone to hold an ordinary place, §14.5);
  §5.9's second end is an event the person's own update or their
  configuration can bring at any time, caught by the probe whenever either moves (§10.8); CI proves the runner's git and the pinned
  MinGit, and the person's git is known only by the machine log's line.
- **Daoris's managed git becomes the default.** One pinned git for Daoris and every child, so the parity class and the opens census run
  in CI against exactly the git the install runs, as GITAUTH1b runs the doors' classes; every floor always holds, so no read in a
  partial clone takes an outside-waiting place for its git's age; §5.9's second end becomes a pin Daoris moves on purpose, its census run before
  the move, so the library is retired by a row the
  steward schedules and not by an update met; the system file is the managed git's own, known in advance. In exchange, the download
  and its security updates are Daoris's to carry, and a repository the person's newer git made in a newer format reads as git's refusal
  until the pin moves.
- **Neither changes** the service, the scheduler, the object read, the library's questions or its admission, which reads the
  repository's own format whichever git made it.

## 8. Rejected in round 2

- **A pool closed after a minute idle** (round 1, §2.2): it never idles while sessions read (Opus blocking 1).
- **A lease shared by every flow of a repository**: under steady reads it never closes, the same sequence.
- **The worktree list in the library with full parity**: one start a flow saved, for the delete's one guard read through a model that is
  not git's.
- **Replacements ignored by both** (`GIT_NO_REPLACE_OBJECTS` on every start): Daoris's reads would show another history than the agents'
  own git.
- **A helper process holding the library**, to contain a fault: the start comes back, and it holds the packs between flows.
- **Folding the untracked diff into W** (S first, then `diff HEAD` against the intents): one start more saved, for Π's order moved and
  W's budget shared with the untracked files.
- **One start an object kept below git 2.36**: a second way with no end Daoris controls.
- **The service host's two reads through the desktop's service**: the host runs where no desktop does, and a scheduler there would bound
  that process alone, as each process's does.
- **A bound per repository with none for the process**: twenty repositories' fan-outs would run twenty gits at once.

## 9. The rows

*Round 2's, superseded by §13's.*

GITLIB1a lands alone. GITLIB1b comes before c and d, which are apart once it is in; e and f follow d; g follows d and stays open
until one of its ends is reached.

- **GITLIB1a — the untracked cut, and one status a pass** (§5.7). Proof: the fold the cut reads equal to today's per-file reads over
  the census's untracked files (nested, CRLF under `core.autocrlf`, binary, empty, over the file limit, past the patch budget, under a
  filter driver, a path with spaces, one in NFD), any difference listed with its reason; `ReviewGitCountTests` and
  `OfferGitCountTests` lowered, the untracked tail two starts whatever its count; the install's measures (§5.10), the first baseline.
- **GITLIB1b — the service and the scheduler** (§5.3, §5.4). Every git start moved into it; `GitRuns`' runners and `HeldChild`'s git
  callers its start kinds; `ProcessStarts`' git entry replaced; the held works and Review's speculative reads handed tokens. Proof: unit
  tests of the places (a fan-out of a hundred held to four, a person's ask passing a full background queue, a hook-running git never
  holding the last two places, one repository's fan-out held to two), of the merge (two askers, one start; an ask after a write in the
  repository, its own start; a streamed read never joined) and of the ending (a queued read dropped and a running one's tree ended when
  its askers leave; an act never); `GitStartsCensusTests` and `ProcessStartsCensusTests`; a `Process` class in which twenty reads across
  two repositories never run more than four gits at once, read from the Git job's census (`OwnedProcesses.Census`); the install's
  `review.read` with a tick's fan-out beside it, at four places and at six.
- **GITLIB1c — the held object reader** (§5.5), its floor 2.36. Proof: the parity class's bytes and commit rows, a partial clone's
  fetched blob and its failed fetch said as such; the hold test with the reader mid-batch; the object column of `LandGitCountTests`,
  `StartGitCountTests` and `ViewGitCountTests` lowered; the install's measures, which close the baseline.
- **GITLIB1d — the library's lease and admission, with refs and the layout** (§5.6, §5.9's rule in the area). The package in the
  driver, its native library in the publish, the fault journal; `RefSnapshot`'s listing (ids as stored), the layout, `symbolic-ref`,
  `rev-parse --verify`, `show-ref --exists`. Proof: the hold test on Windows; `GitAdmissionTests`, each refused case read by git and each
  admitted one by the library, replacements and a journaled fault among them; the lease's bound, lifetime and lock by unit tests; the
  parity class's refs and layout rows; a delete of a branch checked out in the main checkout, in a locked tree and in a tree whose
  folder is missing still refused; the deployment rehearsal laying `git2-*.dll` and the published driver reading refs through it;
  `ViewGitCountTests` and `TidyGitCountTests` lowered; the install's measures against the baseline.
- **GITLIB1e — merge-bases and counts.** Every `merge-base` (two tips, `--is-ancestor`, `--independent`) and `rev-list --count` the
  code asks. Proof: the parity class's merge-base and count rows on the census, a merge-heavy fixture and vscode, timed;
  `ReviewGitCountTests` and `OfferGitCountTests` lowered; the install's measures.
- **GITLIB1f — two commits' changed names.** The name-only and raw diffs of two commits, without rename detection. Proof: the parity
  class's rows and the opens census's name-only diff over a split and a sparse index; the look's count lowered; the install's measures.
- **GITLIB1g — the second way's ends** (§5.9). At each package upgrade the opens census, the parity class and the bench's per-read
  times again, and what passes leaves §5.6's table with its path deleted; at the second end, the library and its admission deleted for
  the scheduler and the object reader alone.

## 10. Round 3: four classes, each closed by construction

*Round 3's, superseded where §14 differs: §10.4 and the reader in §10.1, §10.3, §10.5 and §10.6 by §14.1; §10.6's places by §14.2 and
its lazy-fetch floor by §14.5; §10.7's gate by §14.3; §10.3's stamp and §10.5's derived fields by §14.5; §11 to §13 by §15 to §17.*

Both reads of round 2 found eleven blocking sequences between them, five Codex's and six Opus's. Read together they are four
failures, each with more members than the reads named, so a round that answered each sequence alone would leave the next member to a
round 4. Each class is closed here by a construction a missed member cannot pass, held by a census or a test that fails on one. §10.1
names the classes, their members and how the members were searched; §10.2 to §10.8 are the constructions; §11 places each finding.

### 10.1 The classes

| Class | Its members: the reads', then those found beside them | Closed by | Fails on a missed member |
|---|---|---|---|
| **State read once, or never, and trusted after it moved** | A merged read answering an older state (Opus B1; Codex, across writes); ref fields the library derives from an object a replacement changes, and a replacement added after a lease's admission (Codex); the reader started before a replacement or a configuration change (Codex); the probe keyed by the git's file alone (Codex). *Found:* `GitReadCache`'s answers keyed by ids alone, which a replacement, a graft or a deepened `shallow` moves; the route flights and held answers, which join a view's work under way | One rule for a shared answer (§10.2); a stamp of the inputs, read at each question, under which every holder answers (§10.3); the fields derived from an object given to the engine that reads replacements (§10.5) | `GitKeepersCensusTests`: every field in the driver holding a git answer, a git process or a binding object belongs to a listed holder with its stamp or its flow; `GitStampTests` moves each input of each stamp between two questions |
| **A process or handle that outlives its question** | Mappings kept between questions (Codex); the reader's folder and its packs under gc (Opus B2); a fetch inside the reader holding every object ask (Opus B3); leases held a flow long, waiting on themselves (Opus B5). *Found:* every read's folder in a tree an act removes; a direct `repack -ad`, which writes neither of gc's markers; a `gc.pid` a crashed gc left; every read in a partial clone, which may fetch inside a read's place; a kill the process outlived and a `HeldChild` past its bound, each keeping its place; a detached auto gc after Daoris's own commit and a built-in fsmonitor's daemon a status starts, each living in the Git job past its git; two hook-running acts of one repository filling its two places while their hooks ask it a read | The library opened for one call (§10.5); the reader bounded by its queue, its stamp, gc's markers and its pack folder (§10.4); what a read may hold, a place freed at its process's exit, a removal ending the reads in its folder (§10.6) | The hold tests under legacy delete semantics, each with a control that shows a mapping pins its pack (GITLIB1c, d); `GitLifetimesTests`: every process and handle the service keeps, with what ends it |
| **An act ended mid-write** | A token ending any git, and a working-tree `diff` that locks the index whatever `--no-optional-locks` says (Opus B4). *Found:* two more such diffs (`BranchReplays.cs:521`, `IntegrationReplays.cs:574`) beside `WorkOverlaps.cs:44`; every other way a git ends early: its kind's bound, the reader's ends, a removal, the Git job's end with Daoris | Only a git `RefSnapshot.MayWrite` calls a read and that takes no lock is handed a token; every other runs untokened once started (§10.6) | The optional-locks census extended to a working-tree diff; `GitEndingsCensusTests`: every early end listed with what it may end; the predicate run over every start the count classes record |
| **A door that escapes the one service** | The Tools page's two git starts through `ToolQuestions` (Opus B6); the service host's two reads, kept out with their condition (both). *Found:* `GitVersions`' `--version` and `ToolsCommand`'s `tool use … file`, both through `ToolQuestions`; the programs Daoris starts that start git themselves (the devkit, gates, generators, plugin hooks, a heavy run's gate, a person's found command, the terminal's shell, the agents) | `ToolQuestions` asks no git; the start gate and `HeldChild` refuse a git's image holding no place of the scheduler's (§10.7) | `GitStartsCensusTests`: a source census and the gate's refusal at run time; a `Process` class driving each door through the scheduler |

**How the members were searched.** In the code at `be0f0e7c55`, class by class, not in the reads. For the first: every field in the
driver typed with a git answer (the `(int Code, string Stdout, string Stderr)` tasks of `GitFacts`, `RefSnapshot` and `GitReadOnce`),
every cache of answers (`GitReadCache`), every join of work under way (`RouteFlights`, `HeldAnswers`) and each holder round 2 adds
(the reader, the lease, the probe), each read against every input git reads for its answer: the refs, the configuration at each
level, the replacements, the grafts, `shallow` and the git itself. For the second: every start kind and what it holds while it runs
(its place, its folder, its mappings) and after (a kill `ChildProcess` says a process outlived, `HeldChild`'s bound, the detached gc
`ProcessPurpose.Git` names, a daemon a read starts, a lazy fetch), and every wait of a place on another. For the third: every path by
which a git ends early (a token reaching `ChildProcess.RunAsync`, `GitBounds`, the kills `ProcessStartsCensusTests.OwnKills` names,
the jobs' end with the process, the reader's ends), and every `status` and `diff` the driver asks, read by kind (`GitLaunch.KindOf`).
For the fourth: every caller of `ProcessStarts`, `ChildProcess`, `ToolQuestions` and `HeldChild`, and every `ProcessStartInfo` that
names git anywhere under `src/`.

### 10.2 A shared answer: one rule

**An ask takes another's answer only where its own start would have been that start, begun no earlier than its ask.** *That start*
is the whole start as built, compared before it queues: the file, the folder, every argument, every variable handed, no input, the
start kind and the bound. A summary of the arguments is not equivalence (Codex: only complete immutable inputs establish it), so the
key is the built start itself, and nothing is handed to a start after its key is taken. *No earlier than its ask* means the joined
git has not taken its place: a git reads nothing before it is created and its place is taken before that, so a read still queued
answers a state at least as new as the joiner's own start would read (Opus: join only a read still queued, never one running).

The two reads' sequences each fail one half of it. Opus's landing asks its `rev-parse` and `status` of a tree after the session's own
commit there, while the tick's identical reads run; Codex's caller asks after W has completed, while R, begun during W, runs. Neither
joins; each starts its own. So round 2's epoch is deleted, and no write needs a mark of its own: a write that ended before an ask
precedes every start the ask can join, whether the service ran it or a session's own git did, and a read asked while a write runs
takes the moment it gets, as a read beside a write does today. Write barriers within a flow are the flow's awaits, which the scheduler
keeps: a flow asks its next git only once the last is answered, so its reads after its write are asked after the write ended. Across
flows nothing is ordered, as now (`RefSnapshot`: news from another moment). A streamed read joins nothing; a merged read takes the
highest class among its askers.

`RouteFlights` and `HeldAnswers`, above the service, join a view's work under way. They are forgotten at every change the bus
publishes (`DriverModule`), no act takes their answer, and each git beneath them is shared only by this rule; `GitKeepersCensusTests`
names them so.

### 10.3 The stamp

Every holder of a git answer or of a git's state beyond one call answers only under the stamp of the inputs it read, and the stamp is
read again at each question. A stamp is file stats, never a start, per repository (its common directory):

- **the git**: `GitInUse`, its file and write time;
- **its configuration**: the system file that git reads (asked of it once per git in use), the global file as the start is handed it
  (`GIT_CONFIG_GLOBAL` where `GitGlobalFile.Hand` hands one, else the person's own and the XDG file), the repository's `config`, and
  each file those include (`include.path`, `includeIf`), each by write time and length, the includes read again only when a file that
  names them moves;
- **its history**: `refs/replace/` (the loose folder's names and the lines under it in `packed-refs`, read again only when
  `packed-refs` moves), `info/grafts` and `shallow`; in a reftable repository `reftable/tables.list`, which any ref's change moves,
  stands for the replacements;
- **the variables** the start is handed that change an answer (`GIT_NO_REPLACE_OBJECTS`, `GIT_DEFAULT_HASH`,
  `GIT_DEFAULT_REF_FORMAT`, `GIT_CONFIG_*`), which are part of the start already.

A stamp is some ten to fifteen stats of tens of microseconds each, warm; GITLIB1b measures it a question. Its holders: the object
reader, which answers only under the stamp it started with and is replaced at the first question under another (§10.4); the
library's admission, read at each question (§10.5); an answer kept across flows by its ids (`GitReadCache`), whose key carries the
stamp, so a replacement, a graft or a deepen is never answered from before it; and the retirement probe, made again when the git or
its configuration moves (§10.8). A flow's memos (`GitFacts`, `RefSnapshot`, `GitReadOnce`) carry none, as today: each is one
judgement's moment, dropped at the flow's own writes, and an act judges again before it moves anything; the census names them so.

### 10.4 The object reader

One `git cat-file --batch` per repository, a client of the scheduler; what §5.5 says otherwise stands.

- **Every supported git.** `--batch` prints `<id> <type> <size>` before an object's bytes in every git Daoris supports (both reads),
  so the reader has no floor and no `git-too-old`, and `--batch-command` is not used.
- **In the common directory, asked by id.** Its folder is the repository's common directory, so it holds no working tree open, and
  the pool's retire or `worktree remove` of the tree that asked first is never refused for it (Opus B2a). It is asked by `ObjectName`,
  which takes a full id or a full ref name under a shared namespace (`refs/heads/`, `refs/remotes/`, `refs/tags/`, `refs/notes/`,
  `refs/stash`), with or without `:<path>`, and refuses at construction `HEAD`, every pseudo-ref, `@{…}`, `:<path>` and the
  per-worktree namespaces (`refs/worktree/`, `refs/bisect/`, `refs/rewritten/`). A question naming one of those is resolved first in
  the asker's folder, as the layout question, and the reader is asked the id, so `WorkingTree.ProvenanceAsync` of tree B answers B's
  commit whichever tree started the reader (Codex). A name in the index (`:<path>`) is a working tree's read and stays a start in the
  asker's folder.
- **Under its stamp.** A question under another stamp than the reader's goes to a fresh reader, and the old one ends once its answer
  under way is given, so a replacement or a configuration key added between two questions is answered as a fresh git answers it
  (Codex: git reads its replacements once a process).
- **Never while the packs are rewritten.** Before each question the repository is looked at for `gc.pid` (one younger than the twelve
  hours after which git calls it stale) and `objects/maintenance.lock`: with either, no reader starts, a running one ends, and each
  object is one start meanwhile, as every read is today. While it runs, a watcher on `objects/pack/` (its handle shares delete, and
  the folder is never removed) ends it at the first change there, which a direct `repack -ad` makes by writing its new pack before it
  deletes the old, as a fetch's new pack does, neither writing a marker; the question under way is asked again of the next. So no
  pack the reader maps is held when git means to delete it, and Git for Windows, which retries a refused unlink only briefly before it
  asks the person, is not refused (Opus B2b; Codex's mappings, the reader's half).
- **It never fetches** (Opus B3). It runs with `GIT_NO_LAZY_FETCH=1`, which git reads from 2.44 (`--no-lazy-fetch`'s release, which
  GITLIB1c confirms in git's notes before it relies on it), so a missing object in a partial clone is answered `missing` at once. A
  `missing` in a repository with a promisor is asked again as one start allowed to fetch through the doors, counted as a remote git
  and holding a place of the class that waits outside Daoris (§5.4), so a fetch waiting up to fifteen minutes on the person's grant (`GitAsks.Bound`) holds no
  read's place and no reader. Below 2.44 no reader is held in a repository with a promisor, and each object there is that one start.
  `FilePreview` says a file could not be fetched where the second ask is `missing` too.
- **A person first, one question in the pipe.** The reader's queue answers a person's ask before the background's, oldest first, and
  writes one question at a time, so a person's ask waits behind at most the answer being read.
- **Past an asker's bound.** A blob larger than its asker's bound is read for the bytes the asker wants and the rest drained, up to
  8 MiB, a few milliseconds through the pipe; past that the reader is ended, a read that takes no lock (§10.6), and the asks queued
  behind it are asked of the next reader, never answered `missing` (Opus).

One engine, git's `cat-file`, with two lifetimes, held or one start a question: no second implementation, since both are the same
program asked the same question, and the parity class compares the held answers with one start's.

### 10.5 The library, one call at a time

LibGit2Sharp stays pinned in the driver only (§5.6), and what it holds lasts one call.

- **Opened for one call and disposed at its end.** A question opens a `Repository` for its folder, makes one call on a pool thread
  and disposes it before it answers, so no pack, index or graph the library mapped outlives the call (Codex: no mapping between
  questions; on every Windows, so nothing reads which delete semantics a machine has). An open costs 5 to 6 ms warm (§1.5), about
  60 ms for a Review's ten questions here; §1.2's *opened each time* column measured it on whole flows (0.92 s against 0.80 s held,
  here). Round 2's lease per flow, its ten-second bound, its lock across a thread switch and its disposal waiting on a call are
  deleted with it.
- **No call waits on another** (Opus B5). At most eight run at once in the process, a person's first; a call awaits nothing, so no
  flow holds a `Repository` while it asks the next repository, and housekeeping's inspect over nine repositories
  (`HelpHousekeeping.cs:133`) waits on nothing of its own. Opus's change closes the least recently used lease not in a call when
  eight are open; Codex's mappings close every one not in a call at once, which is that change at its limit.
- **Admitted at each question**, under the stamp (§10.3), from the repository's own files as §5.6's table says, so a replacement
  added after a first question sends the next merge-base, count or changed names to git (Codex). The table stands.
- **A ref as it is stored.** The library answers what a ref holds: its name, its stored id, a symbolic ref's target, and a branch's
  upstream as its configuration names it. Every field git derives from the object (`RefSnapshot.Format`'s `%(objecttype)` and
  `%(tree)`) is the object reader's, asked by the stored id, which reads replacements as git does, so a branch tip replaced by a
  commit with another tree gives `ContentHolds` git's tree (`TreeHeld` reads the reader's answer). With it a repository with
  replacements keeps refs and the layout in the library.
- **The fault journal** writes each call's start and end, two appends a call that the process's death does not lose; a start that
  finds one ending inside a call refuses that repository from the library until the package changes, as §5.6 says.

### 10.6 What a read may hold, and what may end a git

**What may be ended** (Opus B4). A git is *endable* where `RefSnapshot.MayWrite` calls it a read and it takes no lock: a `status` with
`--no-optional-locks`, a diff against the working tree with `--no-optional-locks` and `-c diff.autoRefreshIndex=false` (git's diff
refreshes and locks the index whatever `--no-optional-locks` says, `TreeDiffs`' remark), and `MayWrite`'s other reads as they are.
The predicate, `Endable` beside `MayWrite`, reads the start's arguments by kind as `GitLaunch.KindOf` does, and a command it does
not know is not endable. An endable read is handed its askers' token: dropped while queued and its tree ended while running once
every asker has left. Every other git is untokened once it starts and runs to its end or its bound; an asker's leaving drops it only
while it is still queued, when nothing has run. A write whose asker left still drops the flow's memos (`RefSnapshot.Ran`). Round 2's
tokens for the held works and Review's speculative reads stand, safe now by the predicate rather than by each caller. The source
holds the same line: `tools/optional-locks.mjs` asks a diff of the working-tree kind (no `--cached`, no `--no-index`, fewer than two
revisions) for `diff.autoRefreshIndex=false` beside `--no-optional-locks`, or a mark saying why it may write the index, and finds
three without it today (`WorkOverlaps.cs:44`, `BranchReplays.cs:521`, `IntegrationReplays.cs:574`).

**The early ends, listed.** A git ends early in five ways, each named in `GitEndingsCensusTests` with what it may end: its askers'
leaving (an endable read alone); its kind's bound (`GitBounds`, any git, a hang's end as today, the machine log saying its kind and
any `.lock` it could hold that remains); the reader's ends (§10.4, a read); a removal (below, an endable read); and the Git job's end
with Daoris (every git, the acts reconciled from their journal as today, `ActReconciler`). A new way to end a git fails the census
until it is listed.

**What a read may hold.** A git the scheduler starts holds its place, its folder and what it maps for its run, and nothing past it:

- *no fetch*: every read is handed `GIT_NO_LAZY_FETCH=1`, and one that fails in a repository with a promisor is asked again once as
  §10.4's remote start, so no read's place waits on a remote or on the person's grant (B3's members past the reader: a `log -p`, a
  `diff`, a `show` or a `blame` of a filtered blob); below 2.44, where git does not read it, a read in a repository with a promisor
  takes a place of the outside-waiting class from its start;
- *no daemon*: every read is handed `-c core.fsmonitor=false`, so a person's built-in fsmonitor is never started by Daoris's read as
  a daemon that lives in the Git job past it;
- *no detached maintenance*: Daoris's own acts that run auto maintenance (a commit, a merge, a fetch) are handed
  `-c gc.autoDetach=false` and `-c maintenance.autoDetach=false`, so their maintenance runs inside the act's place and no gc of
  Daoris's is cut mid-repack by Daoris's end;
- *no folder an act removes*: an act that removes a folder (the pool's retire, `worktree remove`, the tidy's removal) is admitted once
  the scheduler has ended the endable reads whose folder is inside it, their askers told the tree is going, and it holds new reads of
  that folder until it ends; an unendable git inside it is waited for;
- *its place until its exit*: a place is freed when its process exits, never when its runner returns, so a git whose kill it
  outlived (`ChildProcess`' remark) or a `HeldChild` past its bound keeps its place until it exits (Opus);
- *no wait on its own repository*: a git waiting outside Daoris counts toward that class's two places and not toward its
  repository's two, so two hook-running acts of one repository leave a place for their hooks' reads of it (Codex's re-entry).

`GitLifetimesTests` lists every process and handle the service keeps (a place, the reader and its watcher, a library call) with what
ends it, and a source census refuses a `Repository` or a git `Process` held in a field outside the listed types.

### 10.7 Every git through one door

The door is the desktop's process (the driver, the modules, the app): `GitService` is the only way it starts git, and every start is
one of the scheduler's kinds. The starts in the code at `be0f0e7c55`, each placed:

| Where git starts | Through the service as |
|---|---|
| `GitRuns`' three runners, beneath `WorkingTree` | the kinds `Whole`, `Streamed`, `Bytes` |
| An act's effect step (`ActSteps.HeldGitAsync`) and a pool claim's git (`PoolGit`), each through `HeldChild` | the kind `Held` |
| An HTTPS identity's test (`GitIdentityHttps`, `ls-remote --heads`) | by command, a person's, waiting outside Daoris |
| The version of the git in use (`GitVersions`, `--version`) | by command of the file, the background's |
| The Tools page's version of a git file (`ToolsRoutes.cs:606`) and its system configuration (`:621`, `config --system --list`) | by command of the file, a person's |
| `daoris tool use git <file>`'s version (`ToolsCommand`, through `ToolQuestions.VersionOfAsync`) | by command of the file, a person's |
| The retirement probe's scratch repository (§10.8) | by command, the background's |

`ToolQuestions` asks no git: a question asked for git's declaration, which each caller names, or of a file whose image is a git, is
the service's (`GitService.AskFileAsync`), so its four git callers reach the scheduler through it (Opus B6). **A git is recognised at
its start, not by its caller:** the start gate and `HeldChild` refuse a start of purpose `Git` or `Read` whose image is a git (a file
named `git`, Tools' git, or the git behind Git for Windows' launcher) and that holds no place of the scheduler's, which only the
scheduler makes; the refusal is git's failure, said in the machine log as `git.start.outside` with its caller. So a git started by
file through a helper written later fails its first test.

**Outside the door, each named with why** in `GitStartsCensusTests`:

- *the service host's two reads* (`IgnoredPaths`, `ls-files -z --others --ignored --exclude-standard --directory`;
  `UncommittedRecords`, `--no-optional-locks status --porcelain=v1 -z --untracked-files=normal`): the host is a program of its own
  that runs where no desktop does, and both take no lock; the census holds that condition (no write among their arguments, the
  status's `--no-optional-locks`), so a write added there fails it (both reads);
- *the doors' program*, git's own child, which asks git the repository's ssh setting;
- *the programs that start git themselves*: the agents and their sessions, a repository's gates and the devkit (`GitStart`),
  generators, plugin hooks, a heavy run's gate, a person's found command through the shell (`FoundReads`) and the terminal's shell;
  each is a program Daoris starts, whose git is its own (Opus).

`ProcessStartsCensusTests`' entry for `GitRuns` moves to the scheduler, its class the asker's.

### 10.8 Retirement, read with its configuration

§5.9's second end stands; its detection changes (Codex). The probe's scratch repository is made again whenever the probe's stamp
moves: the git in use, the system and global files and their includes as `init` reads them, and `GIT_DEFAULT_HASH` and
`GIT_DEFAULT_REF_FORMAT` as the start is handed them. So a global `init.defaultRefFormat` changed under an unchanged git is read at
the next question. **What disables the library at once:** the probe's answer is an input of every question's admission, and while it
finds a reftable or SHA-256 default, or while the probe of a moved stamp has not answered, every question of every repository goes
to git from the next one; it is said once in the machine log (`git.read.formats`), which opens GITLIB1g's retirement. It is computed
and never stored: correctness never rested on it, since each repository's own format is read at each question, and the probe says
when the refused way stops being rare. A default set by an `includeIf` for some folders is not seen by the scratch repository; each
repository made under it is refused by its own format all the same.

## 11. The reads of round 2 answered

| Finding | The sequence it closes | Round 3 | Proof |
|---|---|---|---|
| **Windows mappings** (Codex) | A packed object read and its mapping kept between questions; the person's `git gc`, under the legacy delete semantics, cannot remove the pack | The library opened for one call (§10.5); the reader never started under `gc.pid` or `objects/maintenance.lock`, ended at once by either or by its pack folder's change (§10.4) | The hold tests, each pinning a mapped pack through an attempted legacy delete as its control (GITLIB1c, d) |
| **Ref fields derived from objects** (Codex) | A branch tip replaced by a commit with another tree: the library's `%(tree)` is the original's and moves `ContentHolds`; a replacement added after a lease was admitted | Names and stored ids from the library, `%(objecttype)` and `%(tree)` from the object reader; admission at each question, under the stamp (§10.3, §10.5) | The replaced tip judged as git judges it; a replacement added between two questions (GITLIB1d) |
| **The held reader's state** (Codex) | The reader started from tree A answers tree B's implicit `HEAD` for A; a replacement added between two requests is not read | The common directory and `ObjectName`, names resolved in the asker's folder; a fresh reader at a moved stamp (§10.4) | B's provenance; a replacement and a configuration key added between questions (GITLIB1c) |
| **Merged reads across writes** (Codex) | W begins; R starts during W; W completes; a caller after W joins R and is answered from before W | One rule: no join of a running read, the whole start its key (§10.2) | GITLIB1b |
| **Retirement detection** (Codex) | `init.defaultRefFormat` changed globally under an unchanged git, which a probe keyed by `GitInUse` misses | The probe's stamp holds the configuration; admission git's from the next question (§10.8) | `GitAdmissionTests` (GITLIB1d) |
| **B1** (Opus) | A tick's `rev-parse --verify HEAD^{commit}` and `status` of a tree run; the session commits; the landing's `ReadTreeAsync` joins them, reads the old tip, and a failed landing is not tried again | Join a read still queued, never one running: the other half of Codex's rule (§10.2) | An ask after a ref moved by a git outside the service answered by its own start (GITLIB1b) |
| **B2** (Opus) | (a) The reader holds the first asker's tree, so its retire fails, and `HEAD:path` resolves in the reader's tree; (b) under steady asks the reader restarts on the packs gc is deleting, and the person's gc asks to retry | (a) The common directory and `ObjectName`; (b) no reader under gc's markers, a running one ended, and the pack folder's change for a repack without them (§10.4) | The first asker's tree removed at once; `git gc` itself, `git maintenance run` and `repack -ad` under asks (GITLIB1c) |
| **B3** (Opus) | In a partial clone the reader fetches through the doors, waiting up to fifteen minutes on a grant inside a read's place, and every object ask of the repository waits behind it | `GIT_NO_LAZY_FETCH=1`, a `missing` asked again as one start of the remote class, a person first with one question in the pipe (§10.4); every read in a repository with a promisor the same (§10.6) | A person's preview answered beside a waiting fetch (GITLIB1c); a `log -p` of a filtered blob asked again as remote (GITLIB1b) |
| **B4** (Opus) | A working-tree `diff` (`WorkOverlaps.cs:44`) ended on its caller's token leaves `index.lock`, and the session's next commit fails | `Endable`; every other git untokened once started; the optional-locks census extended, which finds three such diffs today (§10.6) | The census red on the three before their fix; a write whose asker left completes with no `.lock` left (GITLIB1b) |
| **B5** (Opus) | Housekeeping's inspect sweeps every repository in one flow and at the ninth waits on its own eight leases | No lease outlives its call; eight calls at most, none awaiting (§10.5) | The inspect over nine repositories, never more than eight open (GITLIB1d) |
| **B6** (Opus) | `ToolsRoutes.cs:621` and `:606` start git through `ToolQuestions`, which no census sees | Listed and routed; `ToolQuestions` asks no git; the gate refuses a git's image holding no place (§10.7) | `GitStartsCensusTests` red on a git planted through `ToolQuestions`; each door seen at the scheduler (GITLIB1b) |

**The points that did not block**, each placed in a row's proof. Row b's bound is counted from the scheduler's own places, not from a
job that also holds hooks, transports and `Read` runs (both reads); a commit whose hook asks Daoris a read of its own repository, and a
write whose asker left, are proved by their outcomes (Codex; GITLIB1b). The untracked cut reads its paths with `--pathspec-file-nul`
under `--literal-pathspecs` (Codex; GITLIB1a). In every row from c to f a parity or judgement passes before a count test is lowered
(Codex). An over-limit blob never answers the asks behind it `missing` (Opus; GITLIB1c). A `HeldChild` past its bound keeps its place
until it exits, and the census names the programs that start git themselves (Opus; GITLIB1b). The 2.36 refusal is dropped, `--batch`
serving every supported git (both; GITLIB1c). The service host's two reads are named with their condition, which the census holds
(both; GITLIB1b).

## 12. Rejected in round 3

- **A lease per flow that unmaps between its questions**: it rests on libgit2's window cache being empty between calls, where a
  disposal empties it by construction, and it keeps the lease's bound, lock and waits (B5).
- **Telling which Windows refuses a mapped file's delete**, to keep leases where it does not: a reading of each volume's semantics,
  for one open a question saved.
- **Joining a running read, with the epoch moved at each write's admission and completion** (Codex's smaller change alone): a
  session's own git moves no epoch; the rule of §10.2 covers both reads' sequences with none.
- **A reader per working tree, asked any name in it**: each holds its tree's folder, and one repository holds as many readers as trees.
- **`--batch-command`'s `info`, for a floor of 2.36**, or a `--batch-check` held beside `--batch` to know a size first: `--batch`'s
  header gives the size in every git, for one process.
- **A reader held through gc with `GIT_ASK_YESNO` answering no**: the person's own gc in a terminal asks the person, not Daoris's
  stand-in.
- **Ending any git whose askers left and removing its locks after**: a lock another git took cannot be told from the ended one's.
- **The git starts listed at their callers, with no check at the start**: the next helper that starts a file is missed, as B6 found.
- **The probe's finding stored until the package changes**: a second state for the person's configuration to disagree with; read at
  each question it needs none.

## 13. The rows

*Supersede §9's.* GITLIB1a lands alone. GITLIB1b comes next; c follows b, and d follows c, since a ref's derived fields are the
object reader's; e and f follow d and are apart; g follows d and stays open until one of its ends is reached. In every row a parity or
a judgement passes before a count test is lowered, and the row is measured on the install before and after it (§5.10).

- **GITLIB1a — the untracked cut, and one status a pass** (§5.7). The fold's paths added as intents from a NUL-separated file
  (`--pathspec-from-file`, `--pathspec-file-nul`) under `--literal-pathspecs`. Proof: the fold equal to today's per-file reads over
  the census's untracked files (nested, CRLF under `core.autocrlf`, binary, empty, over the file limit, past the patch budget, under a
  filter driver, a path with spaces, one in NFD, one holding `*`, `?` or `[`, one starting with `:` or `-`, and one with a newline
  where the file system allows it), any difference listed with its reason; the read's own index gone after a run that failed or was
  ended; then `ReviewGitCountTests` and `OfferGitCountTests` lowered, the untracked tail two starts whatever its count; the install's
  measures, the first baseline.
- **GITLIB1b — the service and the scheduler** (§5.3, §5.4, §10.2, §10.3, §10.6, §10.7). Every git start moved in, its whole start
  built before it queues; the places, classes and queues; the shared answer's rule; `Endable`; what a read may hold; `ToolQuestions`
  asking no git and the gate's refusal; the stamp, measured a question, and `GitReadCache`'s key carrying it. Proof: unit tests of the
  places (a fan-out of a hundred held to four by the scheduler's own count; a person's ask passing a full background queue;
  outside-waiting gits never holding the last two; one repository's background reads held to two; a git whose kill it outlived
  keeping its place to its exit), of the shared answer (two askers of a queued read, one start; an ask while it runs, its own; an ask
  after a ref was moved by a git outside the service while the first ran, its own, answering the moved ref; a start differing in a
  variable or a folder, its own; a streamed read never shared) and of the ends (an endable read dropped while queued and ended while
  running when its askers leave; a `status` without `--no-optional-locks`, a working-tree diff without `diff.autoRefreshIndex=false`
  and every write run to their end; a queued write dropped with nothing run); `GitStampTests` for a commit's page kept by id, opened
  again after a replacement, a graft and a deepen, each answered anew; `GitStartsCensusTests` red on a git planted outside the
  service, through `ToolQuestions`, and at the gate with no place; the optional-locks census red on the three diffs before their fix;
  `GitKeepersCensusTests`, `GitEndingsCensusTests`, `GitLifetimesTests`; `ProcessStartsCensusTests`' entries. A `Process` class:
  twenty reads across two repositories never above four places; a commit whose hook asks Daoris a read of the same repository,
  answered while the commit runs; `worktree remove` with a status running inside the tree; a `log -p` of a filtered blob in a partial
  clone asked again as a remote start; each door (the Tools page's version and system configuration, `daoris tool use git <file>`,
  the version probe, the identity test) seen passing the scheduler. The install: `review.read` with a tick's fan-out beside it, at
  four places and at six.
- **GITLIB1c — the held object reader** (§5.5, §10.4). Proof: the parity class's bytes (as bytes) and commits (each field against
  the `log` format today's code reads, an `encoding` header and a `.mailmap` among them), the held answers equal to one start's;
  tree B's provenance answered for B with the reader started from A; a replacement added and a configuration key set at each level
  between two questions, each answered as a fresh git; the hold test on Windows: a pack the reader maps refused a legacy delete
  (`FileDispositionInfo`) mid-answer as its control, deleted once `objects/pack/` changes, and `git gc`, `git maintenance run
  --task=gc`, `git repack -ad` and `git commit-graph write --reachable` under asks every ten milliseconds exiting 0 with the
  `GIT_ASK_YESNO` stand-in never asked and no old pack, index or graph left; a `gc.pid` older than twelve hours not counted; the first
  asker's tree removed at once; a partial clone's filtered blob answered `missing`, asked again as one remote start, a person's
  preview answered while that fetch waits on its grant, and a failed fetch said as such; an over-limit blob with asks queued behind
  it, each answered; then the object columns of `LandGitCountTests`, `StartGitCountTests` and `ViewGitCountTests` lowered; the
  install's measures, which close the baseline.
- **GITLIB1d — the library one call at a time, with refs and the layout** (§5.6, §10.3, §10.5, §10.8; §5.9's rule in the area).
  The package in the driver, its native library in the publish, the fault journal; `RefSnapshot`'s names, ids and upstreams from the
  library and its derived fields from the reader; the layout, `symbolic-ref`, `rev-parse --verify`, `show-ref --exists`; the probe
  and its effect on admission. Proof: the hold test on Windows, a pack a call maps refused a legacy delete mid-call and deleted
  between calls, and gc under questions every ten milliseconds never asking; `GitAdmissionTests` (each refused case read by git and
  each admitted one by the library; a replacement added between two questions sending the second to git; a journaled fault;
  `init.defaultRefFormat=reftable` set in the handed global file under an unchanged git sending every question to git from the next,
  said once); housekeeping's inspect over nine repositories, never more than eight `Repository` open; the parity class's refs and
  layout rows, a branch tip replaced by a commit with another tree judged by `ContentHolds` as git judges it; a delete of a branch
  checked out in the main checkout, in a locked tree and in a tree whose folder is missing still refused; the deployment rehearsal
  laying `git2-*.dll` and the published driver reading refs through it; then `ViewGitCountTests` and `TidyGitCountTests` lowered;
  the install's measures against the baseline.
- **GITLIB1e — merge-bases and counts.** Every `merge-base` (two tips, `--is-ancestor`, `--independent`) and `rev-list --count` the
  code asks, admitted at each question. Proof: the parity class's merge-base and count rows on the census, a merge-heavy fixture and
  vscode, timed (a criss-cross whose two bases are committed in the same second, N↑ over two refs, several tips with exclusions, a
  shallow boundary before and after a deepen between two questions); Review's base and the tidy's judgement equal to git's on those
  fixtures; then `ReviewGitCountTests` and `OfferGitCountTests` lowered; the install's measures.
- **GITLIB1f — two commits' changed names.** The name-only and raw diffs of two commits, without rename detection. Proof: the parity
  class's rows (a case-only rename as two names, a mode change, a type change, a gitlink, a path in NFD, one past 260 characters) and
  the opens census's name-only diff over a split and a sparse index; the landed proofs' judgement equal to git's; then the look's
  count lowered; the install's measures.
- **GITLIB1g — the second way's ends** (§5.9, §10.8). At each package upgrade the opens census, the parity class and the bench's
  per-read times again, and what passes leaves §5.6's table with its path deleted; at the second end (the probe's finding on the
  install, or CI's on the runner's git or the pinned MinGit), the library and its admission deleted for the scheduler and the object
  reader alone.

Nothing was measured anew for round 3, and nothing in the product changed.

## 14. Round 4: the held reader dropped

Both reads of round 3 found ten blocking sequences, six Codex's and four Opus's, and both found the censuses unproved on a missed
member. Seven of the ten are in the held object reader or begin there: of Codex's, a pack's lifetime under an alternate's gc, the
caller's configuration, raw parents against effective ones, the lazy-fetch version and newline paths; of Opus's, the reader restarted
on a file a writer replaces (N3) and alternates (N4). The steward's choice for this round removes it (§14.1). What is left is the
scheduler (§14.2), a door the census missed (§14.3), the censuses' proof (§14.4) and the two findings that outlive the reader (§14.5);
§15 places each finding, §17 re-cuts the rows. The code was read at `3d0f5916f5`.

### 14.1 The steward's choice: one start an object, no reader held

**The held object reader is dropped, by the steward's choice.** Three rounds of reads found most of their blocking findings in it:
round 2's Windows mappings, gc's packs and a fetch inside it; round 3's alternates, a writer's final rename, the caller's
configuration, shallow and graft parents, the lazy-fetch version and newline paths. Each round's guards closed the members named and
the next read found one past them, and its gain is the smallest of the service's parts: it saves objects' starts alone, at most 9 of a
merge press's 39 and none of Review's (the area's count). **Rejected:** keeping the held reader with more guards (a settle interval
after the pack folder moves, a watcher on every alternate, readers partitioned by the caller's configuration, parents rewritten for
`shallow` and grafts), each another member of the class round 3 meant to close; and a reader of Daoris's own over git's files (its
packs, indexes and loose objects), a second implementation of git's object store whose every format, replacement and lazy fetch is
Daoris's to follow.

**An object read is one start, asked one question.** Every stored file's bytes and every object's type, size or presence Daoris asks
is one start of `git cat-file --batch` in the asker's folder, run by the scheduler as every git is (the kind `Bytes`, its question
written to its input), asked one question and read by one parser: the header `<id> <type> <size>` and the bytes after it, or
`<name> missing`. Today's forms leave for it, each with its parser (D228; Opus: no second set of parsers beside the reader's):
`cat-file blob` of an id or a `<rev>:<path>` (`BacklogMerge`, `LandedGates`, `VerifyExecution`, `FilePreview`, `FileGit`,
`UnionJoins`), `cat-file commit` (`GitHistoryEdits`, a raw object's bytes), `cat-file -e` and `-t` (`DeletedBranches`,
`LandedReviews`, `GitRefActs` among them), `show <rev>:<path>` (`InstallScripts`, `RepositorySetup`) and the size `ls-tree -l` gives
`FilePreview`.

- **The size and type from the header** (Opus N2). `FilePreview.ReadLandedAsync` lists the path with `ls-tree -z` without `-l`, which
  reads the tree alone and tells a file from a link or a folder by its mode, then asks the blob's id; the header gives its size, so no
  `BAD` reaches `Listed`. A blob larger than its asker's bound is read for the bytes it wants and its git ended by the runner
  (`GitRuns.cs:272`), a read that takes no lock.
- **Never fetching, and `missing` said.** Lazy fetch is off for every read (§14.5), so a filtered blob answers `missing` with exit 0,
  which the parser reads; in a repository with a promisor it is asked again once as a remote start (§14.5), and `FilePreview` says the
  file could not be fetched where that answers `missing` too, never `NotOnBranch`.
- **A question is one line** (Codex's path framing). `ObjectName` takes an id, or a name with `:<path>` whose path holds no newline,
  and refuses one at construction: a path Daoris names (a lock file, a manifest, the declared backlog, the heavy gates' file) holds
  none, and a path a person's tree gives is listed by `ls-tree -z` to its id first, as the preview lists it already.
- **Not a commit's fields** (Codex: raw parents are not effective ones). A commit's tree, parents, author and message stay the format
  read today's code makes (`ActCommits.FactsAsync`, `GitFormats.CommitArguments`), which applies `shallow`, grafts, replacements,
  `.mailmap` and `encoding` as git's history does; nothing parses a commit's fields from `--batch`.
- **Its own start, never shared.** Its question is its input, so §10.2's rule joins it to no other, as each ask of the held reader was
  its own, and it holds its place for its one answer.

With no git held between questions and none run in a common directory, the rest of the reader's findings have no subject: a pack
mapped across questions, an alternate's gc and a writer's rename (Codex; Opus N3, N4), since a library call maps a pack for that call
alone (row d's hold test), and `safe.bareRepository=explicit` (Opus), since every git runs in the asker's folder as today's reads do.

### 14.2 The scheduler (row b)

Round 3's places stand (§5.4, §10.6): four gits at once, a place held from before a git's creation to its exit, the background at most
three, the gits waiting outside Daoris (a remote act, an act that runs a hook) at most two and counted toward no repository's two, a
repository's background at most two. Three changes close Opus's gaps and Codex's re-entry, and one gap falls away.

- **A person's place, kept against every long-running class** (Opus (i)). The background and the gits waiting outside Daoris, whoever
  asked them, hold at most three places together. Two of a person's outside-waiting gits (a push running its hook, a fetch waiting up
  to fifteen minutes on a grant, `GitAsks.Bound`) and two of the background's reads filled the four; now the fourth is always free for
  a person's git that waits on nothing outside Daoris, which waits only behind the person's own.
- **No reader keeps a person's place** (Opus (ii)): with none held, every git takes its askers' class for its own run (a merged read
  the highest among them, §10.2), and none outlives its question.
- **A git that outlived its kill, counted apart** (Opus (iii)). A git the scheduler ended (its kind's bound, its askers' leaving, the
  runner's own stop, a removal) that has not exited `ChildProcess.ExitBound` after the kill, which the git seam says today as `git.unended`, keeps a place
  until its process exits (§10.6); that place leaves the four and every class's count for a count of its own, said in that line with
  the count apart, and again at its exit. While its repository holds one, the background's asks of that repository are answered at
  once as git's failure (`stalled`), never queued, and a person's are started as asked. So four gits hung on a network share hold four
  places apart and stall their own repositories alone, and the count apart grows only with the repositories that hang and the
  person's own presses. Before its bound a hung git holds its place as a slow one does, and the person's place is never one of them.
- **Re-entrant places** (Codex's re-entry). The sequence: two hook-running acts in two repositories hold both outside-waiting places;
  each hook asks Daoris a read, which fails in a partial clone and is asked again as a remote start; both retries wait for the outside
  class's places their parents hold, until a bound ends an act. An ask whose caller descends from a git holding an outside-waiting
  place (the caller read over the instance pipe as `DeskCaller` reads a caller's ancestors, a parent created after its child ending
  the walk) is that git's re-entry: it is placed at once in a place its parent lends, above the four, one at a time for each parent
  and of the parent's class, counted toward none of the caps its ancestors hold (the four, the outside class's two, the repository's
  two). A re-entry that waits outside Daoris in turn lends one to its own descendants, to a depth of two, and an ask deeper still is
  refused as git's failure, said as `git.start.depth`. So the process runs at most four gits and two lent beneath each outside-waiting
  place, eight in all, and no act waits on a place its own git holds. A hook's terminal verb reading in its own process is placed by
  that process's scheduler (§5.4), beneath no place of the desktop's. Round 3's rule that an outside-waiting git counts toward no
  repository's two stays, for the other reads of its repository.

### 14.3 Every git through one door (Opus N1)

A plugin declaring a tool whose id is git (the `acme.tooled` fixture does) is checked by `PluginToolChecks.CheckAsync`, which asks
that git's version and runs the manifest's `ready` checks through `PluginToolRuns.RunAsync` and `ChildProcess` as `Read`. §10.7 did
not list it, so once the gate refused a git holding no place the check would say the git could not be started and the plugin would
show it missing.

- **Routed.** Where a declared tool's id is git, its version and each check whose program is that git's file are asked of
  `GitService.AskFileAsync` by command of the file, a person's (a check runs only on a press or at the kit's trial), with its scratch
  folder and git's word that no repository is there handed as the start is built today; every other tool's checks keep
  `PluginToolRuns`. §10.7's table gains the row *a plugin's declared git, its version and its checks (`PluginToolChecks`)*: *by
  command of the file, a person's*.
- **The gate, at every purpose of Daoris's own.** The start gate and `HeldChild` refuse a git image holding no place at every purpose
  Daoris's own code starts by (`Git`, `Read`, `Tool`, `Outlives`, `EndsItself`), not at `Git` and `Read` alone. `GitIdentityHttps`'
  `ls-remote` starts as `Tool` today through `GitIdentityActs.RunWithAsync`, which keeps its other programs (`ssh-keygen`) and hands git
  to the service (§10.7's row for the identity test). The agents', plugins' and pieces' starts (`Launched`, `Agent`, `Plugin`) stay
  outside the door, their git their own (§10.7's outside list), whichever way the launcher takes them.

### 14.4 The censuses, each seen failing

A census of fields and types passes an omission inside a member it lists (Codex), so no census is proof until it is seen red. Each is a
test over the source or the running service that runs twice more on a mutated input, once with one member it lists removed from its
table and once with an omission planted in a fixture, and each run is shown red naming what is missing.

| Census | What it holds | Shown red on |
|---|---|---|
| `GitKeepersCensusTests`, `GitStampTests` (state) | Every field holding a git answer, a git process or a binding object belongs to a listed holder with its stamp or its flow; each holder's stamp moves on each input git reads for its answer | A listed holder removed; a planted field of a git answer's type; each holder's stamp built with one input left out, its seam dropping each in turn, red on that input's case (the asker's `config.worktree` among them: Codex's omission inside a listed holder) |
| `GitLifetimesTests` (lifetimes) | Every process and handle the service keeps (a place, a library call's `Repository`, an object start), each with what ends it; no `Repository` or git `Process` in a field outside the listed types | A planted field holding either; a call whose `Repository` is not disposed, seen by the count of open repositories after each call |
| `GitEndingsCensusTests`, the optional-locks census (ends) | Every early end with what it may end; `Endable` over every start the count classes record; every working-tree diff with `diff.autoRefreshIndex=false` or its mark | A planted `OwnedProcesses.End`, or a token reaching a runner, outside the listed ends; a `Bytes` or `Streamed` start of a command `Endable` does not call a read; each of the three diffs (`WorkOverlaps.cs:44`, `BranchReplays.cs:521`, `IntegrationReplays.cs:574`) before its fix |
| `GitStartsCensusTests` (doors) | Every start site, not only those naming git, as the service's, as a site that never starts git, or as a program that starts git itself; the gate's refusal at run time | A planted site starting a variable file, unlisted; a listed site's route to the service removed, seen by its drive test; a git with no place started at each purpose of Daoris's own, `Tool` among them |

- **The memos, joins of a running read** (Opus, class 1). `GitReadOnce`, `RefSnapshot` and `GitFacts` let the stages of one flow join
  a read under way, under its first asker's token; once Review's speculative reads carry tokens of their own, a stage that uses the
  answer would inherit a speculative asker's cancellation. Each memo asks under the flow's own token, never a stage's, and the keepers'
  census names the three as joins of a running read within one flow, the one place a running read is joined (§10.2's rule is the
  service's).
- **The runners' own stop, an ending** (Opus, class 3). `GitRuns` ends a git once its reader has enough: a `Bytes` read past its
  asker's bound (`GitRuns.cs:272`) and a `Streamed` read whose reader is done (`:390`). It is listed as an ending, an endable read's
  alone, and a `Bytes` or `Streamed` start of a command `Endable` does not call a read is refused at its construction. Round 3's five
  ends are now its askers' leaving, its kind's bound, the runner's own stop, a removal and the Git job's end with Daoris; the reader's
  ends left with it.
- **The doors' source half** (Opus, class 4). It missed N1 because the program's path was a variable, and the gate's run-time half
  would have caught it only by breaking a plugin, since the plugin checks' fast tests use a stand-in runner. So the source census reads
  every caller of `ProcessStarts`, `ChildProcess`, `HeldChild` and `ToolQuestions`, each site with a variable file is driven in a test
  with a git file through the real gate, and `PluginToolChecks`' drive is a `Process` class with the real runner (row b).

### 14.5 What outlives the reader

- **Lazy fetch off, by a verified predicate** (Codex). §10.6's *no fetch* holds every read, so the version that admits it moves with
  it. Git 2.44.0 ignores `GIT_NO_LAZY_FETCH` where 2.44.1 reads it (Codex, from git's sources), so the predicate is a git at or past the
  first release that reads it, 2.44.1 by that reading, which row b confirms in git's notes and proves against the runner's git and the
  pinned MinGit. Under such a git a read in a repository with a promisor holds an ordinary place, and one that fails there, or an
  object start that answers `missing`, is asked again once as a start allowed to fetch through the doors, counted as a remote git and
  holding an outside-waiting place (§10.4's rule, now every read's); under an older git it takes an outside-waiting place from its
  start. Every read kind Daoris asks (`GitLaunch.KindOf`) is run over a filtered blob with lazy fetch off, and each fails or answers a
  marker its parser reads; a kind that exits 0 with a degraded answer and no marker fails it (Opus N2's class, whose one member,
  `ls-tree -l`'s `BAD`, leaves with row c).
- **The asker's configuration in the stamp** (Codex). The sequence: with `extensions.worktreeConfig`, tree B sets
  `core.useReplaceRefs=false` in its `config.worktree` while A reads replacements, and a stamp of the common directory alone answers B
  from A's state. The stamp is read for the asking folder: the common directory's inputs (§10.3), the asking tree's `config.worktree`
  where `extensions.worktreeConfig` is set, and each `includeIf` evaluated for that folder (`gitdir:`) and its branch (`onbranch:`,
  which brings the tree's `HEAD` into the stamp only where such an include exists). Its holders are three: `GitReadCache`'s key, which
  carries the stamp of the tree that asked, so two trees share an answer only where their stamps agree; the library's admission; and
  the retirement probe (§10.8).
- **A ref's derived fields** (row d). With no reader to ask them of, a listing's `%(objecttype)` and `%(tree)` are the library's, read
  from the object at the stored id in the same call, which is git's answer wherever no replacement names it; a repository with
  `refs/replace/` or `info/grafts` is admitted for no ref question, so its listing is git's `for-each-ref` (§5.6's replacements row
  now reads *git* for refs too). Codex's round 2 sequence, a tip replaced by a commit with another tree, is then git's answer, and a
  replacement added between two questions moves the stamp and sends the next to git.
- **Row a's diff** (Opus). The cut's `diff` of its own index against the tree is a working-tree diff: it is handed
  `--no-optional-locks` and `-c diff.autoRefreshIndex=false`, so row b's optional-locks census reads it clean.

## 15. The reads of round 3 answered

| Finding | The sequence it closes | Round 4 | Proof |
|---|---|---|---|
| **A pack's lifetime, alternates** (Codex; Opus N4) | An object read through an alternate store, then that store's gc: the reader keeps its mapping, and the markers and the pack change are outside the folder it watches | No reader; a library call maps a pack for that call alone (§14.1) | Row d's hold test: the alternate's source gc'd under calls every ten milliseconds, never asking |
| **The caller's configuration** (Codex) | Tree B's `config.worktree` turns replacements off while A reads them; a reader and a stamp of the common directory answer B from A | No reader; the stamp read for the asker's folder, `GitReadCache` keyed by it (§14.5) | `GitStampTests`: B and A each answered as its own git (row b) |
| **Raw parents** (Codex) | A shallow boundary's raw bytes keep a parent `log` reports none of, and the commit page selects an absent parent | A commit's fields stay the format read; none parsed from `--batch` (§14.1) | A shallow boundary's page as today; the census red on a planted commit parse of `--batch` (row c) |
| **The lazy-fetch version** (Codex) | Git 2.44.0 admitted while it ignores the variable, and a filtered object fetched inside a read's place | The predicate at the first release that reads it, confirmed; an older git's reads in a partial clone outside-waiting from their start (§14.5) | The runner's git and the pinned MinGit with the promisor's host down, no connection recorded (row b) |
| **Remote re-entry** (Codex) | Two hook-running acts in two repositories hold both outside-waiting places, and each hook's read, retried as a remote start, waits on its parent's class | Re-entrant places lent above the four, to a depth of two (§14.2) | The sequence as a unit test and a `Process` class, both acts ending 0 before any bound; a depth of three refused (row b) |
| **Path framing** (Codex) | A `<rev>:<path>` holding a newline, written to `--batch`, read as two questions | `ObjectName` refuses it; a person's path listed to its id by `ls-tree -z` (§14.1) | A path holding a newline read by its id (row c) |
| **Censuses on a missed member** (Codex; Opus) | An omission inside a listed holder passes a census of fields and types | Each census shown red on a removed member and a planted omission (§14.4) | The four censuses' mutations (row b) |
| **N1** (Opus) | `acme.tooled`'s declared git checked through `ChildProcess` as `Read`, refused by the gate, shown missing | Routed through `GitService.AskFileAsync` and listed; the gate at every purpose of Daoris's own (§14.3) | `acme.tooled` checked present through the real runner and gate; a git started as `Tool` with no place refused (row b) |
| **N2** (Opus) | `ls-tree -l` prints a missing blob's size as `BAD` with lazy fetch off and exits 0, and the preview answers `NotOnBranch` | The size from `--batch`'s header, `ls-tree` without `-l` (§14.1); reads that exit 0 degraded found by kind (§14.5) | The partial clone's landed preview fetched, or said unfetchable (row c); every read kind over a filtered blob (row b) |
| **N3** (Opus) | `multi-pack-index write`'s lock ends the reader, and the next starts on the file the writer then renames over | No reader to restart; a call maps the old file for that call alone (§14.1) | Row d's hold test: `multi-pack-index write` under calls every ten milliseconds, never asking |
| **The scheduler's three gaps** (Opus) | (i) two of a person's outside-waiting gits and two background reads fill the four; (ii) a reader keeps a person's place; (iii) four gits hung on a share stall every git | (i) a person's place kept against every long-running class; (ii) no reader; (iii) an outlived git counted apart, its repository's background asks answered `stalled` (§14.2) | Unit tests of each, each class's longest wait reported (row b) |
| **The class notes** (Opus) | The memos joining a running read under a speculative asker's token; the runners' own stop unlisted | The memos under the flow's token, named as joins; the runners' stop an ending (§14.4) | A used read that joined a memo surviving its speculative starter's cancellation; the endings census red on a planted end (row b) |

**The points that did not block.** D228's one parser: every object one `--batch` question through one parser, today's forms deleted
(§14.1; row c). `safe.bareRepository=explicit`: no git runs in a common directory, so it has no subject. Row a's diff with
`diff.autoRefreshIndex=false` (§14.5; row a). The order both reads confirmed, a → b → c → d, is re-cut: with no reader, d needs nothing
of c (§17).

## 16. Rejected in round 4

- **The held reader with more guards**, and **a reader of Daoris's own over git's files** (§14.1).
- **A `--batch` held for one flow's questions**: the held reader at a flow's length, with its members.
- **A commit's fields parsed from `--batch`**: raw parents are not git's under `shallow` and grafts, and it is a second parser beside
  the format read.
- **A NUL-framed `--batch` for a path with a newline**: not every supported git reads it, and it is a second framing for one question.
- **A hook's ask told by a variable its act hands it**: a word any process can carry or drop; the pipe reads its caller's ancestors
  already (`DeskCaller`).
- **The outside class's two raised for re-entry**: any fixed cap is filled by as many parents.
- **Waiting on a git that outlived its kill**: it does not exit for being waited on; the scheduler can only stop counting on it.

## 17. The rows

*Supersede §13's.* GITLIB1a lands alone. GITLIB1b comes next; c and d follow b and are apart; e and f follow d and are apart; g follows
d and stays open until one of its ends is reached. In every row a parity or a judgement passes before a count test is lowered, and the
row is measured on the install before and after it (§5.10).

- **GITLIB1a — the untracked cut, and one status a pass** (§5.7, §14.5). The fold's paths added as intents from a NUL-separated file
  (`--pathspec-from-file`, `--pathspec-file-nul`) under `--literal-pathspecs`; the diff of that index handed `--no-optional-locks` and
  `-c diff.autoRefreshIndex=false`. Proof: §13's for this row, unchanged.
- **GITLIB1b — the service and the scheduler** (§5.3, §5.4, §10.2, §10.3, §10.6, §10.7, §14.2 to §14.5). Every git start moved in,
  its whole start built before it queues; the places, classes and queues, with a person's place kept against every long-running class,
  re-entrant places and an outlived git counted apart; the shared answer's rule; `Endable` and the runners' own stop; what a read may
  hold, lazy fetch off by the verified predicate and a failed read's one remote retry; `ToolQuestions` asking no git, a plugin's
  declared git checked through the service and the gate's refusal at every purpose of Daoris's own; the asker's stamp, measured a
  question, and `GitReadCache`'s key carrying it; the memos asking under the flow's token. Proof: §13's for this row, and: a person's read placed at once while two of the person's outside-waiting gits and two background reads
  run, each class's longest wait reported; Codex's re-entry as a unit test, both re-entries placed at once above the four, and a
  depth of three refused as `git.start.depth`; a git that outlived its kill counted apart and said, its repository's background asks
  answered `stalled` while other repositories' are answered; a removal while an unendable git is queued in its folder, the one queued
  before its admission run to its end and one asked after it answered as the tree's absence; a used read that joined a memo surviving
  its speculative starter's cancellation; `GitStampTests`' tree B under its `config.worktree` beside A's replacements; the four
  censuses green on the source and red on each mutation of §14.4, `GitStartsCensusTests` red on a git with no place started as
  `Tool`. A `Process` class, beside §13's: Codex's sequence on real gits, two hook-running commits in two partial clones whose hooks
  each ask the service a filtered blob, the promisor on the stand-in SSH host, both ending 0 before any bound; `acme.tooled`'s
  declared git checked present through the real runner with the gate on; the predicate on the runner's git and the pinned MinGit, a
  filtered blob with the promisor's host down failing at once and no connection recorded; every read kind over a filtered blob
  failing or answering its marker. The install: §13's.
- **GITLIB1c — object reads as one `--batch` question** (§14.1). Every stored file's bytes and every object's type, size or presence
  one start of `cat-file --batch` in the asker's folder, asked one question through one parser; today's forms and their parsers
  deleted. Proof: each caller's answer equal to today's on the census repository, bytes as bytes (empty, binary, over its asker's bound
  read for its bytes and ended with no lock left); the landed preview in a partial clone, a filtered blob answered `missing`, asked
  again as one remote start, read when the promisor gives it and said unfetchable when it does not, never `NotOnBranch`; a path
  holding a newline read by its id, `ObjectName` refusing it in a `<rev>:<path>`; a shallow boundary's commit page as today; a census
  red on a planted `cat-file blob`, `cat-file -e`, `show <rev>:<path>`, `ls-tree -l` or a second parser of the answer. No start is
  saved, so the count tests stand; the install's measures, no longer than before.
- **GITLIB1d — the library one call at a time, with refs and the layout** (§5.6, §10.3, §10.5, §10.8, §14.5; §5.9's rule in the
  area). The package in the driver, its native library in the publish, the fault journal; `RefSnapshot`'s names, ids, upstreams and
  derived fields from the library where the repository holds no replacement, git's `for-each-ref` where it does; the layout,
  `symbolic-ref`, `rev-parse --verify`, `show-ref --exists`; the probe and its effect on admission. Proof: §13's for this row, the
  replaced tip now judged by `ContentHolds` from git's listing, and the hold test adding an alternate's source gc'd and a
  `multi-pack-index write`, each under calls every ten milliseconds, exiting 0 with the stand-in never asked.
- **GITLIB1e — merge-bases and counts**, and **GITLIB1f — two commits' changed names**: §13's, unchanged.
- **GITLIB1g — the second way's ends** (§5.9, §10.8). As §13's, but at the second end the library and its admission are deleted for
  the scheduler alone.

Nothing was measured anew for round 4, and nothing in the product changed.
