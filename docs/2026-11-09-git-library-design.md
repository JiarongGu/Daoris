# One managed git in .NET: every Daoris git read and act through one service (GITLIB1)

**Round 2, proposed for both makers' reads before a decision is written** (2026-11-09). The owner's direction for it, on D147's note
of 2026-11-09: Daoris's git is free-flowing, not managed by .NET. Every read is a fresh process, any number run at once, nothing queues
them per repository, identical reads in flight each start their own git, and a read nobody waits on runs on. It is to be one git
service in .NET that every Daoris git read and act passes, so git is never started any other way. §5 to §9 are round 2: they answer both
makers' reads of round 1 (`docs/2026-11-09-git-library-reads.md`), each finding placed in §6. Round 1 (§1 to §4) is kept as history:
its measures stand, read again in §5.1, and its design and rows are superseded where §5 to §9 differ. The code was read at
`247e8b2090`; nothing in the product changed.

**In brief.** One service, `GitService`, is the only way Daoris's code reaches git. Three engines answer beneath it. A scheduler runs
every git process Daoris starts: at most four running at once, one always free for a person, a queue per repository, identical reads
in flight merged and a read nobody waits on ended. A held `git cat-file --batch-command` per repository answers a file's bytes and a
commit's fields, and ends as its queue empties. LibGit2Sharp, leased per flow and disposed at its end, answers the questions bounded by
their answer (refs, the layout, merge-bases, counts, two commits' changed names) where the repository's format admits each. Walks,
patches, the status and every working-tree read, the worktree list, remote acts, hook-running acts and writes stay git's binary, run by
the scheduler. The first row needs neither engine: Review's untracked files' patches become two starts through an index of its own,
measured on the install as the baseline the library must beat.

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
  `ChangedNames` (§5.6), and `Blob`, `Commit` (§5.5). Each question of a repository has one engine, chosen by the admission (§5.6): the
  library or the object reader where admitted, else the scheduler running the command today's code runs (§5.9);
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
outlives its batch (§1's hazard is git's too, while git runs).

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

Review's and the offer's untracked files' patches are one start each today (`TreeDiffs.UntrackedReadAsync`, `diff --no-index`). They
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
- **The census**, `GitStartsCensusTests`: no git started outside the scheduler, no `LibGit2Sharp` outside `Workplace/Git/`, no read
  handed no token but the judgements named; `ProcessStartsCensusTests`' entry for `GitRuns` moves to the scheduler, its class the
  asker's.
- **The install's measures.** Each row is measured on the install before and after it with `daoris git review <session> --time`, five
  opens of one session tree: the median of `review.read`'s `ms`, of its starts' `createdMs` and of `queuedMs`, and the tidy's starts a
  pass, written into the area's table. GITLIB1a to c's figures are the baseline; a library row that does not shorten them is not taken.
