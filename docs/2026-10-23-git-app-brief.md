# Daoris's git, as comprehensive as a git management app, and Review fast: the brief

> GITAPP1, phase 1 of a co-design under D191 (2026-10-23). This brief is the one document every proposal is written
> from. It holds the direction, the constraints, the facts of today's code with `file:line`, what was measured on this
> machine, what established git management apps offer and how they stay fast, and the questions a proposal answers.
> The proposals are in `docs/2026-10-23-git-app-proposals.md`, one section per maker, each written from this brief alone
> and sealed before its author read another. A synthesis follows in a later quest. `Dnn` is `docs/decisions/Dnn.md`.
> Paths in the code map are under `src/`; `Driver/` is `Daoris.Desktop/Daoris.Desktop.Driver/`, `Modules/` is
> `Daoris.Desktop/Daoris.Desktop.Modules/`, `Web/` is `Daoris.Web/`.

## 1. The direction

The ask names two things: Daoris's git **as comprehensive as a git management app**, and **Review (审阅) fast**. Review,
landing, the session trees, sync and the git screens all rest on git, and Review is slow. The row (TASKS.md, GITAPP1)
asks for one design: what Daoris's git reads and how (the row suggests one reader, cached and invalidated by the ref
watch, bounded parallel), what it offers the person per repository and per session tree, and how Review becomes fast,
within D147/D150's built-in git and D198's identities and access. Its proof: the design accepted with Review's measured
cost before and its target after, its rows filed, the first making Review fast.

"As comprehensive as a git management app" reaches past what D147 §2.8 chose not to build (staging, a conflict editor,
checkout, stash, rebase, cherry-pick, revert, tags, a file tree). Which of those Daoris now offers, where, and to whom,
is one of the questions (§7), not a settled answer.

## 2. The constraints a proposal works within

These are decided and stand unless a proposal argues to amend one, saying which and why (an amendment is appended to
the decision, never a rewrite).

- **Orchestration is central, ownership local** (AGENTS.md). Machine-local streams, filesystem actions and browser state
  stay on the desktop; shared hosts serve no UI and answer no machine path (D47 §4). A route's answer names no machine
  path (D147's GIT1d-a note).
- **The git binary, not a library** (D147 point 6, design §4.3): the git Tools resolves, the same git the agents run
  (D121 §3); a library is reconsidered "only if a view is measured that cannot be one call". LibGit2Sharp was rejected
  for SSH, credentials, hooks, partial clones, reftable and install size.
- **Nothing written into a repository's `.git` to make it faster**: no `commit-graph`, no `maintenance` (D147 §4.2);
  the person's own configuration decides. Nothing cached on disk: git's object store is the cache.
- **A ref is never a cache key**; an answer named by whole commit ids cannot change and may be kept (D147 §4.2).
- **The person's checkout is theirs; a session's tree is the session's** (D51, D113 §5). D147 offers no act that writes a
  working tree or an index; its acts write refs or talk to origin (fetch, create a branch, push one branch at a shown
  commit on a press, open its pull request through the hand-off, delete a local branch).
- **Where git lives in the frame** (D150 point 3): Repositories holds each repository's Branches and History tabs; no Git
  place on the activity bar. The organising object is the branch, then the commit (D55, D147 point 2).
- **Identity and access** (D198): Daoris's git acts as identities of its own through two doors (an ssh launcher and a
  remote helper), handed by `GitWiring.Hand`; nothing prompts; a remote that cannot authenticate is held and said once;
  git access (read, write, dangerous) is judged at the doors for the landing and an agent alike.
- **Every process start passes one gate** (PROCSTART2): `ProcessStarts.Bound = 4` starts under way at once, three of them
  the background's (`Driver/Platform/ProcessStarts.cs:51-57`). Only the start is gated, not the run.
- **Events over polling** (D173): a change is heard by `ChangeWatch`, judged per topic with a deadline behind it.
- **Both doors** (D50): what the window does, the terminal can (`daoris-driver git …`).
- **UI chrome in English and Chinese**; authored content stays as written (the translation contract).
- **Model-free.** Nothing here needs a model; where a proposal adds one, it says the tier that ran (D24).

## 3. Today's git code

### 3.1 How git is started

- **One start site in the driver**: `WorkingTree.GitStart` (`Driver/Trees/WorkingTree.cs:894-930`) resolves git through
  Tools (`Driver/Agents/Tools.cs:269,277`; a chosen git that cannot run is refused, never replaced by `PATH`'s), always
  prepends `-c core.longpaths=true` (`:920`), hands the environment through `Tools.Hand` → `GitWiring.Hand`
  (`Tools.cs:691-696`, `Driver/Platform/GitWiring.cs:28-38, 59-90`), and starts the real `git.exe` past Git for Windows'
  launcher (`GitForWindows.Direct`, `:928`).
- **Three runners** call it: `RunGitAsync` (`:1039`, whole output, through `ChildProcess.RunAsync` and the shared gate),
  `GitBytesAsync` (`:1097`) and `ReadGitAsync` (`:1187-1237`, streamed in 16K-char pieces, the tree killed once the reader
  stops).
- **Bounds**: `GitBounds.Of` gives a question two minutes and other work ten (`Driver/Trees/Domain/GitBounds.cs:19-37`).
- **Per-flow memos** before `RunGitAsync`: `GitFacts.Asked` (id-only and layout reads within `GitFacts.Within()`,
  `Driver/Trees/GitFacts.cs`) and `RefSnapshot.Asked` (one `for-each-ref` per flow,
  `Driver/Trees/SessionTrees/RefSnapshot.cs:26,59,128`), consulted at `WorkingTree.cs:1009-1011`; a writing call drops both
  (`:1069-1070`). Only the landing, sweep and judgement flows open them (`AutoLander.cs:353-354`, `TreeLands.cs:483`).
- **Outside the driver**: the service's `IgnoredPaths.Start` runs `PATH`'s git for `ls-files --others --ignored`
  (`Daoris.Service/Daoris.Service.Core/Registry/IgnoredPaths.cs:94`), past Tools and `GitWiring`; the devkit's
  `GitStart.Run` and `GitObjects` (`cat-file --batch-all-objects`); the landing plugins' own `git push`.
- **No long-lived git process** anywhere in the product: every call starts and ends.

### 3.2 Review (审阅): what one open costs

The page (`Web/src/work/review/DiffPane.tsx`, the head in `Web/src/work/WorkFrame.tsx`) asks `SESSION_DIFF`
(`useSessionDiff`, `DiffPane.tsx:133`, shared with the head's `WorkFrame.tsx:1645` by one key), `HANDOFF_PLAN`
(`:188`, always) and `LANDING` (`:163`, where the tree is plannable), and the head `OPINION_GATE` (`WorkFrame.tsx:1680`).
Every file's patch comes in the one `SESSION_DIFF` answer; nothing is fetched per file.

`SESSION_DIFF` is `TreesRoutes.DiffAsync` (`Modules/Bridge/Routes/TreesRoutes.cs:55-117`). Every step is awaited in turn.
For an own tree with its base recorded, the line configured and nothing landed, its git starts are:

| Step | Git | Where |
|---|---|---|
| Uncommitted (A) | `rev-parse --show-toplevel`; `diff … -M -z --raw --numstat -p HEAD`; `status --porcelain -z --untracked-files=all`; **one `diff --no-index -- /dev/null <path>` per untracked file**, one after another, skipping files over 200 KB and stopping at the 400,000-character budget | `WorkingTree.cs:325-391` |
| Range (B) | `merge-base --is-ancestor <base> HEAD`; `rev-parse --git-common-dir`; the line (0-3 starts where none is configured); `merge-base HEAD <line>` and `merge-base HEAD origin/<line>`; `merge-base --independent` (and `merge-tree`) where both answer; `rev-list --count` twice | `Driver/Trees/SessionTrees/ReviewRanges.cs:35-126` |
| Committed (C) | `rev-parse --show-toplevel` again; one `diff … -p <from>..HEAD` for every file, streamed and stopped at 400,000 characters, 80,000 a file | `WorkingTree.cs:298-305, 459-480` |
| Landed (D) | where a landing is recorded: three more, more where its branch is gone | `LandedReviews.cs:33-90` |
| Branch (E) | `symbolic-ref --short -q HEAD` | `TreesRoutes.cs:88` |
| Offer (F) | where nothing landed and the work is ahead: `rev-parse` ×3, `status --porcelain`, `symbolic-ref`, `log --oneline <tip> --not --branches --remotes`, then the squash and content-hold checks (`ContentHolds.cs` holds 21 git calls, not traced) | `TreeWorkReads.cs:37-61`, `TreeFollows.cs:26-68` |

That is **12 + U starts** at the least (U the untracked files), 18 with the offer, all serial, before `LANDING`'s own
chain (gate, subject, plan and another uncommitted read, `TreesRoutes.cs:587-611`, not enumerated here) runs beside it.

**Caching**: none in the driver. `SESSION_DIFF` opens neither memo, and `GitReadCache` is the git screens' alone. The page
keeps the answer (`Web/src/bridge/sessions/trees.ts:157-187`): key `['diff', session]`, fresh 15 s for a live session,
forever for an ended one read after its record last moved, no focus refetch, one read in flight per session
(`:81-108`); it is read again when the record's `updated` passes the answer's time (`:182-184`, every move of a live
session) and on the state hub's `diff` scope (`Web/src/bridge/state/stateHub.ts:31`), which landing, discarding,
sweeping, recovery and sync publish. `LANDING` and `HANDOFF_PLAN` keep the defaults (15 s, refetch on focus, one retry).
Leaving the page abandons the wait, not git.

### 3.3 The git screens

- **Routes** in `Modules/Bridge/Routes/GitRoutes.cs` (moved from `DriverModule.Git.cs` by PART10e): `GIT_BRANCHES`
  (`:24-66`), `CHECKOUT_BRANCH` (`:71-90`), and `GIT_GRAPH`, `GIT_COMMIT`, `GIT_HISTORY`, `GIT_BLAME`, `GIT_COMPARE`
  (`:93-123`) through one `GitReads` that lives as long as the module (`:19`).
- **The list**, `GitBranches.ListAsync` (`Driver/Trees/Branches/GitBranches.cs:240`): repositories four at a time
  (`TreeRefs.AtMostAsync`), serial within one; one `for-each-ref` naming each ref's commit, date, symref, `worktreepath`,
  `ahead-behind:<line>` and subject (`GitBranches.Refs.cs:83-90`); fallbacks for an older git (`:360-401`); a
  `merge-base --is-ancestor` per moved landed branch and a `rev-list --left-right --count` per branch whose origin copy
  differs (`:408-445`). The line guess is cached per root on the ref files' stamps (`Driver/Platform/LineGuesses.cs:20-60`).
  The list itself is not cached.
- **The reads**, `GitReads` (`Driver/Trees/GitReads/GitReads.cs:201`), whole commit ids only: a graph page, one `log`
  of 201 (`:253`); a commit, a `log -1` and the range reader, two in series (`:294`); a file's history, one
  `log --follow --name-status` that walks from the top for every page, since `--follow` ignores `--skip` (`:325`); a
  blame, one `blame --porcelain` (`:358`); a compare, a `log --left-right A...B` and the range reader (`:384`).
- **`GitReadCache`** (`GitReadCache.cs`): in memory, least recently used out, 64 MiB by bytes, keyed by the common git
  dir, view, ids, path and page; nothing kept from a shallow repository or a failed read.
- **The page** (`Web/src/bridge/repositories/git.ts`): the list on the 15 s default with focus refetch (`:185`); graph
  pages each a query (`:233-259`); commit, history, blame and compare kept forever (`:262-311`). The screens are all in
  `Web/src/projects/git/RepositoryGitView.tsx`: the Branches tab, a workspace's groups, the History tab and a branch's
  page. **No screen calls the commit, history, blame or compare reads**: the hooks and routes are built (GIT1b, GIT1d)
  and nothing draws them.

### 3.4 Trees, landing, sync, tidying and the ref watch

- **A session tree** opens through `SessionTrees.OpenAsync` (`Driver/Trees/SessionTrees/SessionTrees.cs:270-419`):
  `worktree add --lock … -b daoris/s-…`, a verify, the dependency copy, `worktree unlock`; removal is `worktree remove`
  and `branch -D` (`TreeRemovals.cs:206,234`).
- **Landing**, `TreeLands.LandAsync` (`SessionTrees/TreeLands.cs:373-411`): a merge (`:287-295`) or a branch with
  `update-ref` (`:1088, 1193`), a squash by `commit-tree` (`TreeSquashes.cs:23`), the plugin's push (`:704`).
  `AutoLander.LandAsync` lands one at a time (`Landing/AutoLander.cs:299-330`).
- **Bring up to date** (D109): one `fetch --no-write-fetch-head origin <line>` joined across callers
  (`TreeRefs.cs:97-140`); the replay rebases in a tree set aside (`SessionTrees/BranchReplays.cs:163, 296, 467-508`).
- **Sync** (`SessionTrees/TreeSync.cs`): which repositories hold Daoris's branches and their fetches four at a time
  (`:89, 108`), then judged one repository at a time (`:220`). Nothing runs it on a clock.
- **Tidying**, `BranchTidying.LookAsync`, at most every five minutes (`Trees/Branches/BranchTidying.cs:30, 74-133`).
- **The ref watch** is built: `RefWatch` (`Driver/Trees/RefWatch.cs:104-383`) holds one `ChangeWatch` root on each
  repository's `.git` that `session-branches.json` or `landings.json` names (`:249-265`), hearing `HEAD`, `packed-refs`,
  `refs/heads|remotes/**`, `reftable/**` and `worktrees/*/HEAD` (`:229-242`), 250 ms quiet, 2 s cap, at most 32 roots
  (`Driver/Platform/ChangeWatch.cs:171-189`), two repositories judged at once, a five-minute deadline behind it
  (`:107-110`). Its wakes feed tidying, landing moments and `MachineFacts`' per-repository git topics (`help.lines`,
  `help.trees`, `help.work`, `help.sweep`, `Help/HelpFacts.cs:35-186`). **Nothing in Review or the git screens listens
  to it.** The service watches checkouts on its own for knowledge freshness (`CheckoutFreshness.cs`).

### 3.5 D198's doors and identities

`GitWiring.Hand` is built and hands every process the no-prompt environment and, where the install lays the doors out,
`DAORIS_GIT_HOME`, the ssh door in `GIT_SSH_COMMAND` and `insteadOf` rewrites to `daoris::`
(`GitWiring.cs:59-109`, `GitRouting.cs:11-54`). The doors are one executable laid out as `daoris-git-ssh.exe` and
`git-remote-daoris.exe` (`Daoris.Desktop.GitDoor/Program.cs:12-24`, `GitDoors.cs:78, 227`). The access judge runs its
default (reads allowed; writes by a landing's grant, 15 min) (`Driver/Platform/GitAccessRules.cs:27-193`). Not built:
the identities' store (`git-identities.json`, GITAUTH1c) and per-scope git access (GITAUTH1f). The doors touch only
acts that reach a remote; a local read passes none of them.

## 4. What was measured (2026-10-23, this machine)

**The machine**: Windows 11, 22 logical processors, 64 GB; Git 2.53.0.windows.3 from Git for Windows; the Daoris
repository (8,857 commits, 4,756 tracked files, 183 MB packed in 33 packs, 18 working trees, a `commit-graph` that
the person's own tools wrote). It was **not idle**: ten Claude Code processes, Codex, fifteen Daoris desktop processes
and 55 `dotnet` processes ran (other sessions and builds), CPU about 28%. The OS's real-time antivirus reported off.
Every number below is wall time from a scratch probe (`_fixtures/`, not tracked) that starts git directly as .NET does,
with `GIT_OPTIONAL_LOCKS=0`, against the session tree this brief was written in. Single calls varied two- to fourfold
between runs, so ranges are given. This is the machine the owner and the sessions use, under the load they run.

**Starting git is the cost.**

| What | Time |
|---|---|
| One `git rev-parse HEAD`, 30 in a row, three runs | min 236-257 ms; median 330-781 ms; p90 792-2,144 ms |
| 20 starts one after another | 25.0 s |
| The same 20, eight at a time | 1.5 s |
| A held `git cat-file --batch`: its start and first answer | 156-189 ms |
| The same process answering 499 commits more | 29-50 ms in all, **0.06-0.10 ms a question** |

D147 §1 measured about 100 ms a start on 2026-10-04 from an idle shell; under today's load a start costs three to eight
times that, and its tail is seconds.

**Review (审阅), replayed.** The probe ran `SESSION_DIFF`'s eighteen starts (A, B, C, E, F above) one after another, in
this tree, against a base 28 commits back (368 changed files, 2.5 MB of patch) and 200 back (1,771 files, 16.6 MB):

| Case | Serial, as today | The same facts in four starts at once* |
|---|---|---|
| 368 files committed, nothing untracked | **16.2 s** (the two diffs 0.7-1.0 s; each small read 0.2-3.7 s) | 0.95 s |
| 368 files committed, 50 untracked | **44.1 s** (32.4 s of it the 50 untracked files, one start each) | 1.05 s |
| 1,771 files committed | **8.4 s** | 1.6 s |

\* The committed diff, `status --porcelain=v2 -z --untracked-files=all --branch`, one `for-each-ref` with
`%(ahead-behind:HEAD)` for the line and its origin copy, and `diff -p HEAD`, started together. This shows the order of
the cost, not a design: it reads the untracked files' names but not their patches.

The diff itself is not the cost: one `diff -M --numstat --patch` over 368 files took 1.0-2.6 s, over 1,771 files 2.2 s,
and `--name-status` alone 0.6-1.2 s. One git per file is: 30 files one by one took 17-28 s, eight at a time 4-20 s,
and in one call naming all 30 paths 0.35-4.1 s. `LANDING`, `HANDOFF_PLAN` and `OPINION_GATE` were not replayed; they
run beside `SESSION_DIFF`, and share the gate's three background places with it.

**The git screens' reads** (each one call, two runs):

| Screen | Call | Time |
|---|---|---|
| Branches | `for-each-ref` with `worktreepath` and `ahead-behind`, 28 refs | 0.34-1.4 s |
| | `worktree list --porcelain` (an older git's fallback) | 0.32-2.3 s |
| History | a graph page of 201 from `HEAD` | 0.71-1.4 s |
| | the same page 2,000 commits down (`--skip`) | 1.2-1.3 s |
| A commit | its fields (`log -1`) + its changes (the range reader) | 0.71-2.8 s + 1.2-1.7 s |
| A file's history | `log --follow` of 101 for `TASKS.md` (1,954 commits touch it) | 1.9-3.0 s |
| | the third page (301), walked from the top | 2.2-2.6 s |
| Blame | `TASKS.md` (518 lines, 200 KB of porcelain) | 1.9-4.4 s |
| | `WorkingTree.cs` (1,245 lines) | 1.1-2.5 s |
| Compare | `log --left-right A...B`, 200 commits apart | 0.79-1.3 s |
| Search (no screen) | messages, `--all -i --grep=review` | 0.70-1.3 s |
| | content, `-S RangeAsync` from `HEAD` | 12.4-14.6 s |

`git status` in this tree (clean, with ignored `node_modules`) took 0.6-3.8 s, and with `--untracked-files=no` 2.3 s
once. This repository sets `core.fscache`; `core.fsmonitor` and `core.untrackedCache` are unset.

**Other repositories here** are smaller (the largest by history: about 2,650 commits, 1,200 files, 42 MB packed); this
repository is the large case.

**Not measured**: the install's own window (Review opened there and timed from the press); `LANDING`'s chain; a session
tree with hundreds of real untracked files; git's fsmonitor daemon or untracked cache here (turning them on writes the
repository's configuration); whether the `commit-graph` carries changed-path Bloom filters; an idle machine.

## 5. What established git management apps offer

Read from their public documentation and the vendors' own descriptions (2026-10-23), not measured here. GitKraken and
Sourcetree have left their files in this repository's git directory, so the person has used both on it.

| Capability | What the apps offer | Daoris today |
|---|---|---|
| History graph | Every app: lanes across all refs, paged or virtualised, filters by branch, author, path, date; GitKraken, Fork, Tower, Sublime Merge, SmartGit, JetBrains, GitLens | History tab and a branch's page; *All branches*; 200 a page |
| Commit details | Message, author, parents, refs holding it, files, diff | Built in the driver (GIT1b), no screen |
| Staging by hunk and line | Every desktop client; lazygit in a terminal | None (D147 §2.8) |
| Composing a commit, amend | Every client | None; the agents commit |
| Diff views | Side by side and inline, word diff, whitespace toggles, image diff (Tower, GitKraken, Fork) | Review's inline patch (`PatchView`) |
| Merge and conflict views | Three-way merge editors (GitKraken, Tower, Sublime Merge, JetBrains, VS Code's merge editor) | None; a session or the person resolves |
| Branches | Create, rename, delete, checkout, track, ahead/behind, filtering | List by kind, ahead/behind; create, push, delete owed by GIT1g/h |
| Stash | Save, apply, pop, drop, show | None |
| Rebase | Interactive rebase as a list to reorder, squash, edit (GitKraken, Fork, Tower, Sublime Merge, JetBrains) | The replay (D109) only |
| Cherry-pick, revert, reset | Every client, from the graph | None |
| Blame | Inline and in a gutter, *blame before this change* (GitLens, JetBrains, Fork, Tower) | Built in the driver, no screen |
| File history | Following renames | Built in the driver, no screen |
| Search | Messages, authors, content (`-S`/`-G`), file names, across all history; JetBrains indexes it | None |
| Remotes | Several, fetch, pull, push, prune, pull requests (GitKraken, Tower, GitHub Desktop) | `origin` only; fetch on a press; pull requests by plugin |
| Tags | List, create, push, delete | None (D147 §2.8) |
| Submodules | List, update, open as their own repository | None |
| LFS | Lock, track, pointer awareness (GitKraken, Tower, Fork, Sourcetree) | None |
| Worktrees | Created and listed (GitKraken, Fork, Tower, JetBrains) | Daoris's own session trees, one per session |
| Reflog, undo | GitKraken's and Tower's undo; reflog views | Recovery refs of Daoris's own (D51 rule 7) |

**How they stay fast**, as their makers describe it:

- **A long-lived reader instead of a start per question.** Sublime Merge reads repositories with its own git reading
  library and calls git only for what changes a repository; GitKraken reads through libgit2 in process; git's own
  `cat-file --batch` serves the same role to a process that keeps it open (measured above: 0.1 ms a question).
- **An index of the history.** JetBrains builds a persistent log index in the background for fast filtering by path,
  author and text and for file history across branches. Git's own `commit-graph`, with changed-path Bloom filters,
  speeds `log -- <path>` two- to fivefold on git's and Linux's repositories and up to 28 times on a deep internal one,
  and `blame` walks with it too.
- **Incremental status.** Git's built-in fsmonitor daemon (Windows and macOS, `core.fsmonitor=true`) with
  `core.untrackedCache` spares `status` the scan of the working tree; Scalar turns these on for large repositories.
- **File-system watching, then a debounced refresh.** VS Code's git extension watches `.git` (the index, refs) and the
  working tree, refreshes status on a debounce, and also hears git run in its own terminal; GitLens caches per
  repository and invalidates on those events.
- **One call per view**, machine formats (`-z`, `--porcelain=v2`), and virtualised lists that draw only the rows on
  screen.

Sources: Sublime Merge's announcement (sublimetext.com/blog/articles/sublime-merge); IntelliJ IDEA's help on the Git log
index (jetbrains.com/help/idea/settings-version-control-log.html); the GitHub blog on git's file system monitor
(github.blog, "Improve Git monorepo performance with a file system monitor"); git's changed-path Bloom filter series
(gitgitgadget, 2020); the Pro Git book's libgit2 appendix.

## 6. What the facts add up to

These are observations, for each proposal to weigh, not a design:

1. Review's cost is **the number of serial starts**, and on this machine a start costs 0.25-0.8 s with a tail of
   seconds. Eighteen starts are 8-16 s; each untracked file adds one more. The patch read itself is one to three seconds.
2. Nothing Review reads is kept between opens in the driver; the page keeps it for 15 s on a live session and reads it
   again on every move of the session's record.
3. The ref watch already hears every ref and `HEAD` move of the repositories with Daoris's work, and nothing in Review or
   the git screens listens to it.
4. Half of what D147 designed for reading (a commit, a file's history, blame, compare) is built in the driver and drawn
   nowhere.
5. A held git process answers object questions about a thousand times faster than a new start, and needs no library.
6. The one view that cannot be one fast call is search across history by content.

## 7. The questions each proposal answers

1. **Review fast.** What Review reads and how; its target on this machine for the cases in §4 (368 files with and
   without untracked files, 1,771 files), opened first and opened again; what changes when a live session moves.
2. **The reader.** One reader for all of Daoris's git reads, or per area? Long-lived processes (`cat-file --batch`,
   others), bounded parallel starts, or both? Inside which process (driver, modules)? How it shares the gate of four.
3. **Caching and invalidation.** What is kept, keyed by what, invalidated by what (the ref watch, a working-tree watch,
   Daoris's own writes), and how a stale answer is never shown as current.
4. **Working-tree state.** How uncommitted and untracked work is read fast without writing the repository's
   configuration (fsmonitor, the untracked cache) or within that rule amended.
5. **What the person gets.** Which of §5's capabilities Daoris offers, per repository and per session tree, and which it
   still refuses, with the reason against D55, D113 §5 and D147 §2.8. Which of them a session's agent may use through the
   same door. Where each lives in the frame (D150).
6. **Writes.** For each writing act offered: whose tree it touches, its plan and its press, its refusals, how D198's
   access and identities apply, and how it is undone.
7. **History at scale.** Graph, file history, blame and search on a repository of tens of thousands of commits: an index
   of Daoris's own, git's `commit-graph`, or neither, against D147 §4.2's rule.
8. **The terminal door** (D50) and **Ask Daoris**'s kinds for what is added.
9. **What to measure and how** to hold the target: the proof of the first row and the regression check that keeps it.
10. **The rows**, in order, the first making Review fast, each with its proof.
11. **What is rejected**, and why.

A proposal is complete when it answers each question, names the decisions it amends, and says what it did not measure.
