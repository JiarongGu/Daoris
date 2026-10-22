# Daoris's git, as comprehensive as a git management app, and Review fast: the proposals

> GITAPP1, phase 1 of a co-design under D191 (2026-10-23). Each section is one maker's complete proposal, written from
> `docs/2026-10-23-git-app-brief.md` alone and sealed before its author read another. Sections are added verbatim and
> never edited afterwards; the synthesis, in a later quest, is written apart. Claude's was written first, Codex's second
> through a helper's call; Fable's comes with the synthesis.

## Claude (Opus 5.5)

*Sealed 2026-10-23, before any other proposal was read.*

### C1. The shape in one paragraph

Review is slow because it asks git eighteen questions one after another, each in a new process, and keeps none of the
answers. Almost everything it asks is a function of commit ids that cannot change, so the fix is to ask fewer
questions, ask them together, remember the answers by the ids, and know when the ids have moved instead of asking
again. One reader per repository does that for every git read Daoris makes: a ref snapshot it holds until the ref watch
says the repository moved, an id-keyed answer cache, a held `cat-file --batch-check` for the small questions, and
bounded parallel starts for the large ones. On top of that reader Daoris offers everything a git management app reads,
the history edits that need no working tree, and the working-tree acts in the person's own checkout behind a switch the
person sets. A session's tree stays the session's.

### C2. Review fast (question 1)

**What a Review open reads, after the change.** Four kinds of fact, and only the first two ever need git on an open:

| Fact | Read by | Kept until |
|---|---|---|
| The ids: the tree's `HEAD`, its branch name, the line and origin's line | the repository's ref snapshot (C3) and one `status --porcelain=v2 -z --branch --untracked-files=all` in the tree | the ref watch hears the repository move, or the tree's watch hears its files move (C4) |
| What is uncommitted | the same `status` call; then, only where it lists a changed tracked file, one `diff -p HEAD` | the tree's watch hears a change |
| The range: where the work parted from the line, how far behind it is, the base of a chain's step | `merge-base`, `merge-base --independent`, `merge-tree`, `rev-list --count`, each keyed by the ids it was asked about | never: an answer named by ids cannot change (D147 §4.2) |
| The committed changes | one range diff, keyed by its two ids | never; 64 MiB least-recently-used, as `GitReadCache` |

**Untracked files are read from disk, not through git.** A file git lists as `??` is not in any object; its patch is
the file itself with every line added. The driver reads it (the same 200 KB file limit, the same 400,000-character
budget, git's own binary test: a NUL in the first 8,000 bytes) and writes the `+` lines and the `@@ -0,0 +1,N @@` head
itself. That removes the one cost that grows with the work: 50 untracked files were 32 s of the measured 44 s. A
twins-style table test holds the composed patch byte for byte against `git diff --no-index` over a set of fixtures (an
empty file, no trailing newline, CRLF, a long line, UTF-8, a binary), so the two never drift.

**The questions go together.** The facts above split into what needs the ids and what does not. Cold, a Review open
starts at most four processes at once (the `status`, the snapshot's `for-each-ref` where the snapshot is not current,
the range diff, the uncommitted diff), then the id-keyed questions, which are cache hits after the first open of a
given pair of ids. Each start a Review makes is the **person's** start in `ProcessStarts`, since the person opened it,
so it never queues behind a scan.

**The offer and the landing plan leave the critical path.** `SESSION_DIFF` answers the review; the Accept offer
(`TreeWorkReads`, its content holds) becomes its own read the foot asks after the patch is drawn, through the same
reader and cache. `LANDING`, `HANDOFF_PLAN` and `OPINION_GATE` read the same snapshot, so a Review's four reads share
one set of ids instead of each resolving its own.

**A live session moves without a re-read.** Review becomes a pushed slice per open session (the pushed-slices
contract): the driver's reader hears the ref watch and the tree's watch, recomputes only the facts whose inputs moved,
and pushes the new answer's ids; the page asks for the diff of a new range only when a range moved. The page's 15 s
staleness and the read on every record move retire for Review: a record move that did not move git moves nothing.

**The targets on this machine, under the load measured in the brief §4**, each held by a test that counts starts and
timed on the installed window:

| Case | Today | Target, first open | Target, open again with nothing moved | Target, a live session's commit |
|---|---|---|---|---|
| 368 files committed, clean | 16.2 s, 18 starts serial | ≤ 1.5 s, ≤ 4 starts at once | ≤ 100 ms, 0 starts | ≤ 1.5 s, ≤ 3 starts |
| 368 files, 50 untracked | 44.1 s, 68 starts | ≤ 1.5 s, ≤ 4 starts | ≤ 100 ms, 0 starts | ≤ 1.5 s |
| 1,771 files committed | 8.4 s | ≤ 2.5 s (the 400,000-character budget stops the patch read) | ≤ 100 ms | ≤ 2.5 s |

### C3. The reader (questions 2 and 3)

**One `GitRepository` per common git directory**, in the driver library beside `WorkingTree`, owned by one registry
that the modules hold for the process's life (as `GitRoutes` holds `GitReads` today). Every read Daoris makes goes
through it: Review, the git screens, the landing's judgements, the tidy, `MachineFacts`' git topics. It holds:

1. **A ref snapshot**: one `for-each-ref` over `refs/heads`, `refs/remotes`, `refs/tags` and `refs/stash` naming each
   ref's id, symref, `worktreepath`, subject, date and `ahead-behind` against the line, read at a **generation**. The
   ref watch (`RefWatch`) raises the generation for its repository; Daoris's own writes raise it through `RefMoves.Mine`
   before they return. A read asks for the snapshot at the current generation and gets the held one or one fresh read,
   joined across callers. `RefSnapshot` and `GitFacts` become views of it, so the flows that open them today keep their
   behaviour with one source.
2. **An id-keyed answer cache**: `GitReadCache` widened to everything named by ids (range diffs, merge-bases, counts,
   a commit, a blame, a file's history, a compare, a graph page from a set of tip ids). Its rules stand: a ref is never
   a key, nothing from a shallow repository, nothing git did not answer, 64 MiB, memory only.
3. **A held `cat-file --batch-check`** (and `--batch` where an object's content is read: a file at a commit for the
   preview, a tree's paths for the path field) started on the repository's first read and closed after two idle minutes,
   at most eight held across the machine. It answers the small questions that are each a start today: `rev-parse
   --verify <name>^{commit}`, `cat-file -e`, an object's type and size, at about 0.1 ms each (brief §4). It is not a
   start per question, so it takes one place in the gate once. A held process that dies is started again on the next
   question; a question it cannot answer in its bound falls back to a start, said in the machine log.
4. **Bounded parallel starts** for the large reads, at most three of one repository's at once, all through
   `ProcessStarts`, the person's place for what the person opened and the background's for the rest.

**Invalidation, never expiry.** A held answer is current while its generation is; nothing is dropped on a clock. Every
answer carries the generation and the ids it was read at, so a page can say *as of* and a push can say *newer*. The ref
watch's roots widen from the repositories holding Daoris's work to those with a git screen or a Review open, within
`ChangeWatch.MostRoots` (32); a repository past it reads its snapshot fresh on every open and says so, as the brief's
other bounds do.

### C4. Working-tree state (question 4)

D147 §4.2's rule stands: Daoris writes nothing into a repository's configuration, so it turns on neither git's fsmonitor
nor the untracked cache. Instead, **Daoris watches the trees it reads**: a session's tree while its session runs or its
Review is open, and the person's checkout while a git screen of its repository is open. The watch is `ChangeWatch` on the
tree's root with git's own ignore read once (`ls-files --others --ignored --directory`, the service's call) so
`node_modules`-like folders are filtered; a change marks the tree's status stale, and the next read runs `status` once.
With no change heard, an open reads nothing. An overflow, an error or a resume marks every watched tree stale (the
watch's recovery rules). The person may still turn on fsmonitor in their own configuration; Daoris reads the `status`
git gives, and is faster with it.

### C5. What the person gets (questions 5 and 6)

**Every read a git management app offers**, per repository on Repositories' tabs and per session tree on its Review:

- The commit page, a file's history, blame with *before this change*, and compare: built in the driver and drawn nowhere
  today; drawn first, because they cost no new reader.
- Search: by message and author (`log --grep`, `--author`, one call, fast) and by content (`-S`, `-G`), which is the one
  read that cannot be one fast call (12-15 s here): run on the press, streamed, cancellable, bounded to a path and a
  range where given, its first 200 hits shown as they come.
- Tags, stashes (`stash list`, a stash's changes), the reflog of a branch, every remote (not only `origin`), submodules
  (listed, each opened as its own repository where it has a checkout), LFS (a pointer drawn as *a file kept by LFS*,
  never as a pointer's text diff), and worktrees with the session that holds each.
- Diff views: side by side beside the inline view, word diff, whitespace ignored, an image's before and after.
- A conflict preview: `merge-tree --write-tree` answers, without a working tree, whether a landing, a rebase or a
  cherry-pick would conflict, and in which files and hunks; Review shows it before Accept.

**The history edits that need no working tree**, as ref writes (D147 §3.1's kind, and its plan-then-press with the
judgement again at the shown commit): rename a branch, create and delete a local tag, cherry-pick a commit onto a
branch, revert a commit on a branch, and rebase a branch onto another, each computed by `merge-tree --write-tree` and
`commit-tree` (or `git replay` where the git has it) and applied by one `update-ref` at the judged old id. A conflict
refuses the act and names the files and hunks; nothing is half applied. **Every ref write is undoable** for the
recovery window (D51 rule 7): the old id is kept in Daoris's recovery ref and the act's page says the press that brings
it back. A branch a running session's tree holds is refused, as D147 refuses a push of it. Pushing a tag or anything
not a fast-forward stays D198's *dangerous* class, denied unless the person's git access says otherwise.

**The working-tree acts, in the person's checkout only, behind a switch per repository**, *Daoris may change this
checkout's files*, off by default and set on the repository's Setup: stage and unstage by file, hunk and line; commit
and amend the last unpushed commit; stash, apply, drop; switch branch; resolve a conflict with a three-way view writing
the result the person chose. Each refuses on what git refuses, and each says what it will change before the press.
The switch exists because the person's editor may have those files open (D113 §5): it is their checkout, so it is their
choice, made once and visible. This amends D147 §2.8 and §3.1 for the person's checkout alone.

**A session's tree is never changed by the person through Daoris.** Its work is the session's until it lands (D51,
D164): the person reads it in full, previews its conflicts, and asks the session in words; Review's *Commit* stays the
agent's (PROCESS1g).

**The agents get nothing new.** They run their own git in their trees; the reader is Daoris's. D198's doors and access
apply to the person's remote acts here exactly as to a landing.

### C6. History at scale (question 7)

No index of Daoris's own, and nothing written into the repository by default. The reader's id-keyed cache makes every
second look free; a graph page, a blame and a file's history are one call each and measured at 0.7-4.4 s cold here. Where
the person wants more, **a press on the repository's Setup, *Speed up history in this repository***, runs
`git commit-graph write --reachable --changed-paths` once, as the person, and says what it wrote: the person's act on
their repository, not Daoris's on its own (an amendment to D147 §4.2's rule, which forbids Daoris writing it unasked).
A file's history stops walking from the top for each page: the reader keeps the walk's ids and continues from the last
page's last id with `--follow` on the path that commit had.

### C7. The terminal door and Ask Daoris (question 8)

Each read gains its `daoris-driver git` verb (`review <session>`, `show`, `history`, `blame`, `compare`, `search`,
`tags`, `stashes`) printing the same answer, and each act its plan-and-`--yes` verb, one judge for both doors (D50).
Ask Daoris's owed `git` kind covers the acts as cards; the reads stay exempt, as `trace` is.

### C8. Measured and held (question 9)

- **A machine-log line per view**, `git.read`: the view, starts made, cache hits, held-process answers, ms; no path.
- **A fast-half census** with a stand-in `GitStart` counting starts per Review open for each case of C2's table: cold
  at most four, warm none. It fails on a fifth start, which is how the cost stays down.
- **A Process case** on a fixture of 368 committed and 50 untracked files, timing the open twice.
- **The installed window**, Review timed from the press on this repository, before the first row and after it, with
  the machine's load said beside the numbers.

### C9. The rows (question 10)

1. **GITAPP1a — Review fast.** Untracked patches composed in process (with the table against git); the range's
   questions keyed by ids; the review's reads started together on the person's place; the offer as its own read.
   Proof: the census (≤ 4 cold); the Process case; the window's time before and after.
2. **GITAPP1b — the repository reader.** `GitRepository`: the generation snapshot, the widened cache, the held
   `cat-file`, bounded starts; `RefSnapshot`, `GitFacts`, the list and Review on it. Proof: the census at 0 starts warm;
   a ref write by Daoris and one by git in a terminal each raising the generation.
3. **GITAPP1c — Review pushed, with the trees watched.** The tree watch; Review as a pushed slice. Proof: a live
   session's commit pushes within 1.5 s; an idle open starts nothing.
4. **GITAPP1d — the built reads drawn**: the commit page, history, blame, compare, in both languages.
5. **GITAPP1e — the remaining reads**: search, tags, stashes, reflog, remotes, submodules, LFS, worktrees, diff views,
   the conflict preview.
6. **GITAPP1f — the history edits without a working tree**, with undo.
7. **GITAPP1g — the person's checkout**: the switch, then staging, commit, stash, switch and the three-way view.
8. **GITAPP1h — the history press** (commit-graph), only after a measurement on this repository shows its gain.

### C10. Rejected (question 11)

- **libgit2 or a reader of Daoris's own over git's files.** D147 §4.3 stands, and the held `cat-file` gives the same
  speed for the questions that are many; a reader of git's files would break on reftable and partial clones.
- **Turning on fsmonitor or the untracked cache.** It writes the person's configuration (D147 §4.2).
- **A history index on disk.** A second record of git's objects; the cache and the person's own `commit-graph` suffice
  until a measurement says otherwise.
- **A patch per file fetched on demand from git.** That is the start per file this design removes; the one range read
  is split per file from memory instead.
- **Polling `status` on a clock.** The watch hears it.
- **The person editing a session's tree.** It is the session's work until it lands; words reach it, presses do not.
- **The working-tree acts on by default.** The checkout's open files are the person's to risk.

**Decisions amended**: D147 §2.8, §3.1 (the person's checkout behind its switch, the history edits) and §4.2 (the
person's press may write a `commit-graph`); D113 §5 by the switch. D150, D198, D51 and D55 stand. Under D191 this
changes a foundation (a held process and a shared reader every flow reads through), so it is one for Fable's read too.

**Not measured by this proposal**: the held `cat-file` under the gate with eight repositories; the tree watch's cost on
a tree with `node_modules`; `git replay`'s floor on Git for Windows; the commit-graph's gain here.
