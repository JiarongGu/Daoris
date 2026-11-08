# Daoris's own git reads in process (GITLIB1)

**Proposed, for both makers' read before a decision is written.** The owner's direction is D147's note of 2026-11-09: git's starts
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
