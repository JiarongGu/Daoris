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

## Codex (codex-acp, through Daoris's helper, ask 319eed62)

Its answer as the helper gave it, verbatim but for its headings set one level down. Written from the brief alone; it did not read Claude's section.

Read against afbb0807948cc2405dcd8c73f1340a7fd50e6bee. Times are targets, not new measurements. No other proposal was read.

### 1. Review fast

Separate a versioned review snapshot from action plans. The snapshot contains committed and uncommitted changes, branch, range, line distance and landing record. Acceptance, content-hold proofs, hand-off readiness and opinion judgements have separate states. An unread judgement grants nothing.

Remove WorkAsync's content proofs from the patch dependency chain (TreesRoutes.cs:70-94; TreeWorkReads.cs:37-69).

The first implementation retains git's range algorithms:

1. Prove the tree's own git layout/common directory on disk, following GitReads.cs:519-538. Never let git discover a parent repository.
2. Start status v2, including branch/HEAD and all untracked names, beside one ref enumeration. Pin HEAD and both line tips. Fetch nothing.
3. Once HEAD is known, start the dirty diff beside independent range questions. Select independent candidates after their inputs arrive.
4. Read one committed diff from the selected base tree to pinned HEAD. Preserve rename handling, complete file names, caps and truncation.
5. Read bounded untracked additions directly from files. Publish only against the observed generation; an invalidated answer cannot overwrite a newer one.

Preserve ReviewBase's candidate order, replayed bases, local/remote union counts and REVIEWBASE3's merged base. Pairwise ahead/behind atoms do not replace union counts when line tips diverge.

For a configured line, valid recorded base, two readable line refs and comparable bases, the cold ceiling is:

| Read | Starts |
|---|---:|
| Status and ref enumeration | 2 |
| Recorded-base ancestry | 1 |
| Merge bases against both line tips | 2 |
| Independent candidates | 1 |
| Union behind/ahead counts | 2 |
| Committed/dirty diffs | 2 |
| Total | 10 |

Deduplicate equal inputs. Untracked files add zero starts; incomparable bases add one merge-tree. Displaced bases, missing configuration, unread refs, landed/gone trees and old git have separate counted paths.

The targets include today's complete file list and bounded patch coverage, not all 2.5 or 16.6 MB:

| Brief case | First open median / p95 | Starts | Second unchanged open | Live commit: event to replacement |
|---|---|---:|---|---|
| 368 committed files | ≤4 s / ≤8 s | ≤10 | ≤100 ms / ≤250 ms; 0 starts | ≤4 s / ≤8 s; ≤10 starts |
| 368 plus 50 bounded text additions | ≤4.5 s / ≤9 s | ≤10 | ≤100 ms / ≤250 ms; 0 starts | ≤4.5 s / ≤9 s; ≤10 starts |
| 1,771 committed files | ≤5 s / ≤10 s | ≤10 | ≤100 ms / ≤250 ms; 0 starts | ≤5 s / ≤10 s; ≤10 starts |

First open has no preheated review or cached answers. Second open requires retained answers and uninterrupted reliable watches. The live target includes debounce. Closed reviews invalidate on commits but generate no patches until opened.

Record-only changes cost zero starts. Commits refresh range/dirty state; edits refresh dirty state only. Head/pane share requests.

Do not eagerly request LANDING/HANDOFF_PLAN on Review open. Use published judgement facts, or offer a door that reads an acceptance plan and presents its press. Report its starts and latency separately. Landing still rejudges its inputs. A fast patch is not described as a completed acceptance plan.

### 2. The reader

One driver-host-owned coordinator serves Review, preview, git screens, landing and sync. Share layout/results/in-flight work/scheduling; keep domain judgements separate. Modules adapt answers.

Allow four short jobs/two walks globally, foreground first. Preserve ProcessStarts.Shared's four total/three background and reserved person place (ProcessStarts.cs:51-57; WorkingTree.cs:1198-1200). Repeat timing through that gate.

Lazy cat-file --batch uses Tools/GitWiring.Hand and fixed contexts for commits/trees/blobs. Bound framed requests/deadlines; drain stderr. Cancellation drains or retires the process; EOF fails pending requests.

Cap held readers at four globally, retired on idle/context change. Creation costs one gated start; further questions cost zero and hold no start slot.

### 3. Caching and invalidation

Keep immutable results under common-directory identity, whole ids, view, path, options, git context and parser version. Keep mutable observations separately under repository/worktree/generation, with observation time and watch reliability.

Use a shared 64 MiB LRU and bound in-flight output. Ref names, memberships and landing metadata remain mutable observations.

Invalidate on refs/HEAD/index/files, own writes, configuration/layout and resume. Contexts include replace/shallow/grafts/attributes/blame-ignore/alternates; otherwise refuse caching.

Extend watches to opened repositories without Daoris branches. RefWatch.cs:249-265 currently selects standing session/landing records and main checkouts, not every registered repository.

Watch before reading; compare generations; coalesce bursts. Stale completions cannot replace newer answers or enable writes.

Watch loss/overflow/root limits/reconnect/resume invalidate freshness. Reopen/Refresh validates status+refs (two starts), reusing matching immutable patches. Reconcile missed events on an attended-view deadline.

No watch makes external git atomic. Say “Read at …, HEAD …”, not an absolute “current”. Zero-start reopen serves that identified observation. Writes revalidate independently.

### 4. Working-tree state

Use status v2/all untracked names/disabled optional writes. Pin HEAD. Keep status separate: staged/unstaged cancellation can yield an empty patch while still blocking acceptance.

Distinguish committed/staged/unstaged/untracked/conflicted work; batch staged/unstaged diffs for manual work.

Render untracked text as bounded additions from bytes, not one git call per file. This is display data, not an applicable patch. Say when encoding, line endings, attributes, binary content or size prevents an exact view.

Keep the approximately 200 KiB addition limit, 80,000-character file cap and 400,000-character budget. Check containment, links/reparse points, allocation size and changes during reading. Never follow an untracked link outside the tree. Unknown is not clean.

Watch attended trees, exclude safe bulk, invalidate on ignore-rule changes. Do not maintain git's index.

Do not enable fsmonitor/untrackedCache/maintenance/commit-graph; use existing acceleration.

Partial clones require proved lazy-fetch suppression for local reads; otherwise refuse incomplete local-only answers. Fetch missing objects separately through D198.

### 5. What the person gets

Keep D150's Branches/History. Open details from graph, history/blame/compare from changed paths. Search lives in History; Review retains session outcomes.

Offer one optional person's manual worktree per repository, created on a press under Branches → Changes. Never assign it to a session or create one merely for Review.

| §5 capability | Person per repository | Session tree |
|---|---|---|
| Graph | All refs, filters, paging/virtualisation | Session/line tips |
| Commit details | Parents/message/files/trace; refreshed containing refs | Same pinned read |
| Hunk/line staging | Manual tree | Agent's own index; person requests work |
| Commit/amend | Staged manual work; amend unpublished only | Agent commits under process rules |
| Diff | Inline, side by side, word/whitespace, bounded images | Same reads |
| Merge/conflicts | Base/ours/theirs; whole-side choice or external resolver | Facts; session resolves |
| Branches | Create/rename/delete/track; switch manual tree | Follow actual branch; existing agent authority |
| Stash | Show/save/apply/explicit drop, manual tree | No competing person stash act |
| Rebase | Plan reorder/squash/drop of unpublished manual work | Existing replay and agent local work |
| Cherry-pick/revert/reset | Manual tree; reset soft/mixed, unpublished only | Request from session |
| Blame | Gutter, before-this-change | Same read |
| File history | Rename following, bounded pages | Same read |
| Search | Messages/authors/paths; explicit slower content query | Pinned history |
| Remotes | Several; explicit fetch/prune/exact push; manual integration; PR plugin | D198 fetch; accepted work through landing |
| Tags | Read/create/delete local; no remote mutation | Read; existing local agent authority |
| Submodules | Pointer/dirty facts; register/open independently | Facts; owning repository changes work |
| LFS | Pointer/availability awareness, external-tool door | Same |
| Worktrees | List all; create/remove manual tree | Own session lifecycle |
| Reflog/undo | Read reflog; receipts and explicit recovery | Existing recovery/discard |

Keep D55: no source editor/tree or editable three-way editor. Permit commit messages and operation-specific changed paths.

Refuse force push, remote branch deletion, remote tag writes, hard reset, clean and recursive submodule writes. Defer LFS locking/credentialed management until its transport is governed. These acts lose data, bypass ownership or exceed access coverage.

Amend D147 points 2–3/design §2.8/§3.1/§7 for manual work, D150 point 3 for Changes. D113 §5 and D51 ownership stand.

Agents may use common reads and authorised local operations in their own tree. They cannot impersonate person presses, mutate the manual tree, write another repository through this API or gain remote authority through another surface.

### 6. Writes

Each write has a typed plan/press: tree, ids, touched state, preconditions, side effects and recovery. Changed inputs require replanning.

Serialise own writes per tree/shared-ref operation; revalidate with git locks/ref compare-and-swap. External changes stop the next workflow step.

| Act | State written | Refusal/recovery |
|---|---|---|
| Create manual tree | New person's directory/index; shared worktree metadata/new branch | Occupied branch/name or nonempty destination refuses; remove only clean/idle |
| Stage/unstage | Manual index | Exact entries and shown bytes must match; unsupported/conflicted patches refuse; preserve prior index |
| Commit/amend | Manual branch/objects/index; hooks may have side effects | Exact staged snapshot/HEAD; published or uncertain amend refuses; keep old tip |
| Switch/track | Manual files/index/HEAD; shared tracking config | Dirty/conflicted/active operation or protected/session/landing/occupied branch refuses; retain previous config |
| Branch create/rename/delete | Shared refs/config, no registered files | Protect line/session/landing owners and checked-out branches; keep old tip; verified holder transaction |
| Fetch/prune | Objects/tracking refs/FETCH_HEAD | Named remote, D198 Read; record previous tips; no remote undo claim |
| Push | Exact remote branch, possibly tracking refs | Shown id, same name, fast-forward only, no line/session branch, D198 identity/Write; preserve hooks; no Undo push |
| PR hand-off | Host through existing plugin | Existing ownership/plugin/access checks; host reversal separate |
| Local tags | Shared refs/tag objects | No overwrite or hidden signing prompt; keep old id/metadata |
| Stash | Manual files/index; shared stash objects/refs | Explicit tracked/untracked inclusion; ignored excluded; apply keeps source; drop separate with recovery ref |
| Merge/cherry-pick/revert | Manual files/index/branch/operation state | Clean start, exact ids, no active operation; old tip retained; git abort; conflicts held visibly |
| Replay; soft/mixed reset | Manual branch/index; replay also files | Unpublished only; protect line/session/landing; unknown reachability refuses; original tip/state retained |
| Whole conflict side | Selected manual file; staging separate | Exact conflict stages/bytes; preserve replaced bytes; arbitrary editing external |
| Remove manual tree | Manual files/administration | Dirty/unique ignored files/active operation/unread state refuses; no force; branch deletion separate |

Bounded private records preserve index/file bytes; recovery refs preserve tips. Reflogs cannot preserve uncommitted bytes. Keep interrupted recovery visible; receipts define recovery, not generic Undo.

Ref removal follows git-ref-absence and ref-removal-transactions: unread is not missing; every holder proof ref is verified in the deletion transaction.

Existing session lifecycle doors stay: merge landing may touch the registered checkout under its contract; replay uses its designated tree; branch landing writes refs/objects.

Remote writes await D198/install proof. Today's judge has Read/Write plus landing grant/no-push-off (GitAccessRules.cs:40-45), not scoped access. Person push needs exact authorisation, never a borrowed grant/bypass.

Keep D198 identity/commitsAs, local-author precedence, readOnly/holds/no prompts. Add no access level or permission rule. Dangerous remote operations remain absent.

### 7. History at scale

Use git/existing commit-graphs, no persistent database/maintenance. Virtualise graph pages with lane continuation and disclosed ref bounds.

Keep log --follow; cache/share bounded prefixes before replacing its repeated walk (GitReads.cs:325-351). Prove any cursor across renames/merges.

Message/author/date search uses bounded log. Content search uses explicit -S/-G semantics, pinned tips, progress/cancellation and low concurrency. Distinguish messages, changed lines and snapshots.

Measure large/merge-heavy/rename histories and many refs. An external disposable index needs a separate measured proposal covering build/size/invalidation/rebuild. A slow pickaxe call alone does not justify startup indexing.

Amend D147 §4.2 for generation/context memory keys; retain no persistent cache/performance writes. Amend point 6/design §4.1/§4.3 from one process a view to bounded jobs plus held object reads. Git stays authoritative.

### 8. Terminal and Ask Daoris

Bridge/daoris-driver git share typed operations for review/status/detail/history/blame/compare/search and worktree/stage/commit/stash/integration. Settle names through the verb census.

CLI writes print plans and apply tokens/expected state, using window refusal codes. Reject session-originated person presses. 

Ask gets D147's git kind for permitted person-pressed repository acts and screen doors to manual plans. It proposes, never executes or supplies yes. Destructive work gets a door to its plan. Reads remain explicit exemptions.

Keep D198 git-use/exclusions: Ask cannot choose own, manage secrets/access/trust or answer go-aheads. Census every hook/verb. Local routes use opaque tree ids/relative paths. Include EN/中文.

### 9. Proof

Prove row one on the installed window under the brief's load first.

Replay pinned ranges/50 additions, including binary/oversize/Unicode/changing files; scratch stays in-repository.

Time display/list/patches/action readiness separately; record queue/starts/bytes/hits/retries/rendering. Retain ≥30 cold/warm samples per case with median/p95, load, git and installed doors.

Count delayed judgement/object starts too. §1 targets snapshots; acceptance-plan cost needs a separate receipt because it was not measured.

Deterministic tests hold ≤10 ordinary snapshot starts, zero per-addition starts, pinned ids, shared in-flight work, valid zero-start reopen and no record-only invalidation. Show these tests fail when eager/per-file work returns.

Real git fixtures cover divergent/replayed/incomparable bases, detached/landed/gone/not-ours, roots/renames/shallow/partial/caps. Inject event loss/index/ref/context changes, cancellation and EOF. Stale cannot authorise; unknown cannot mean clean. Race write inputs; rehearse abort/recovery.

Use declared machine performance rehearsals for time thresholds. Run relevant gates and installed-window EN/中文 checks; mocks prove no installed timings.

### 10. Rows in order

| Row | Outcome/amendments | Proof |
|---|---|---|
| GITAPP2: fast Review | Split planning; pin inputs; parallel range reads; direct additions; cache/minimal generation watch. D147 point 6/design §4.1–§4.2; append snapshot/plan contract to D164 Review notes | Three timing cases, start ceilings, range equivalence, zero-start reopen, no eager plans, installed Review |
| GITAPP3: coordinator | Migrate desktop consumers; held object pool/context keys/full watch recovery | Census, EOF/cancellation, bounds, shallow/replace/config/overflow; object timings |
| GITAPP4: reading screens | Detail/history/blame/compare/diffs/search, Review links; D147 §2.4–§2.7 under D150 point 3 | Git fixtures, graph continuity, large histories, window/navigation/languages |
| GITAPP5: refs/remotes | Planned branches, named fetch/prune, local tags, exact push/PR; D147 §2.8/§3.1 | Ref/holder races, hooks, access/identity, loopback and installed doors; D198 prerequisites |
| GITAPP6: manual tree | Optional person's tree/Changes; D147 §7, D150 point 3 | Dirty/ignored preservation, occupancy/ownership and terminal parity |
| GITAPP7: index/commits | Stage/commit/amend/stash; D147 §2.8/§3.1 | Patch races, cancelling dirty changes, hooks/authorship, stash conflict/byte recovery |
| GITAPP8: integration | Manual merge/pick/revert/replay/soft-mixed reset/conflict-side selection; D147 §2.8/§3.1 | Crash/abort/recovery, published-work refusals, external-write races, session isolation |

Each row includes terminal/Ask/translation/docs. Reconcile GIT1g/h/i/j/k; retain D198 dependencies. Append amendments; D55/D113/D51 protections stand.

### 11. Rejections, limits and corrections

Reject a serial universal git server, per-file starts, shell chains disguised as one start, unlimited fan-out and taking the reserved person-start place for benchmarks. Reject replacing ReviewRange with the probe, pairwise counts for unions, fetching on open, session timestamps as git versions or forever-fresh ended-tree observations.

Reject libgit2, persistent indexing and automatic tuning here; writes into live session trees; implicit checkout takeover; a source editor; force cleanup; remote history rewriting; generic undo.

I measured nothing and ran no builds/tests/product/probes/remotes. Targets, acceptance cost, blob/old-git/partial-clone behaviour, watches and recovery need proof. Ignored state, actual installation/identities/load and subsequent work are unknown.

Qualifications to the brief:

- §3.5 omits that doors ship only with --git-doors pending transport proof: tools/desktop-publish.mjs:1326-1340 and D198's final note. Built does not mean ordinarily installed/routed.
- §6's “every ref” is too broad. RefWatch.cs:229-242 covers the named heads/remotes/reftable paths, not tags/replace refs; :249-265 limits roots to standing Daoris work/main checkouts.
- LANDING need not run beside the initial diff: DiffPane.tsx:148-163 derives plannable from treeHere, false during initial pending; HANDOFF_PLAN is unconditional at :188. Measure the real dependency trace.
- Twelve/eighteen starts are lower-bound replay figures. ReviewRanges.cs:63-77 adds conditional independent/merged-base work; :101-108 can read another base; WorkAsync adds content proofs. Preserve the brief's qualification.
- “Reads only” has an object-writing exception: ReviewRanges.cs:90-94 runs merge-tree --write-tree. It moves no ref/index/working file but writes objects.
- Whole ids are not context-free: GitReads.cs:492 keys directory/view inputs; :526 excludes shallow repositories only. D147's GIT1b note acknowledges replace refs and blame.ignoreRevsFile outside the key.

Paths: TreesRoutes.cs is under src/Daoris.Desktop/Daoris.Desktop.Modules/Bridge/Routes/; DiffPane.tsx under src/Daoris.Web/src/work/review/. Other tree files are under src/Daoris.Desktop/Daoris.Desktop.Driver/Trees/; ProcessStarts/GitWiring/GitAccessRules under its Platform/.

Read: brief, discovery guidance/router, D50/D55/D113/D147/D150/D191/D198, git/auth designs and cited code. Not read: docs/2026-10-23-git-app-proposals.md, including through git. No file changed.

## Fable (written by the steward's Fable agent from the brief at afbb08079)

Verbatim but for its headings set one level down. Written from the brief alone; it did not read either other section.

> Written 2026-10-23 from the brief at `afbb08079` alone (D191), checked against the main checkout at `f506863e7` with its
> working tree. `Dnn` is `docs/decisions/Dnn.md`; `Driver/` is `src/Daoris.Desktop/Daoris.Desktop.Driver/`, `Modules/` is
> `src/Daoris.Desktop/Daoris.Desktop.Modules/`, `Web/` is `src/Daoris.Web/`. Two read-only probes of my own are in §3 and
> §13; everything else measured is the brief's. Nothing of Daoris was started.

### 0. The answer in ten lines

1. **Review is slow because its reads are serial starts, and a start on this machine is a second of waiting, not of work.**
   My probe (§3): a bare `hostname.exe` from .NET costs 0.7-6 s under today's load, `git rev-parse HEAD` 0.2-1.6 s, eight
   `rev-parse` at once 0.3-1.9 s for all eight. The cost is process creation under a third-party scanner on a loaded
   machine; git itself is not the cost, and the launcher is already bypassed.
2. **The reader**: one `GitReader` per repository (keyed by its common git dir) in the driver library, held by the process
   (the window's loop, or `daoris-driver` for a verb): the layout read from disk, the refs read once and held while their
   files' stamps stand and the ref watch is quiet, every answer keyed by whole ids in one shared `GitReadCache`, starts in
   bounded parallel rounds through the gate of four. No long-lived git process in the first row.
3. **Review's chain becomes three rounds of parallel id-keyed reads**, ≤ 10 starts cold, **0 per untracked file** (its patch
   is written from the file, byte for byte as git writes it), **0 on an open with nothing moved**, and one `diff` after a commit.
4. **Targets on this machine under the brief's load**: 368 files 16.2 s → ≤ 3 s; with 50 untracked 44.1 s → ≤ 3.2 s; 1,771
   files 8.4 s → ≤ 4 s; opened again ≤ 0.1 s; a commit reaches an open review within 3 s of the ref write.
5. **The refs snapshot turns every ref question into an id question**, so D147 §4.2 holds whole: a ref is never a key; the
   ids it resolved to are.
6. **A fact the brief relays does not hold**: Review's `git status` and `git diff HEAD` can write the session tree's index
   (`index.lock`) because Daoris's reads pass no `--no-optional-locks`, which two other readers already do. One argument.
7. **The person's git**: every read of a git app (graph with filters, commit, file history, blame, compare, search, stashes,
   tags, remotes, worktrees, reflog, submodules, LFS pointers) per repository and per tree; acts on refs and remotes under
   D198 (fetch, create, rename, delete, push, tags); and, later, finishing a session's work in its **idle** tree (stage by hunk
   and line, commit as the person, stash, resolve a replay's conflict, cherry-pick, revert, reorder own commits). The
   person's checkout stays theirs (D113, D147 §3.1): read-only, with the command to copy.
8. **Nothing on disk, nothing written into `.git` on Daoris's own**: one narrow amendment offers a per-repository press that
   writes git's own `commit-graph` and the two status settings, as the person's say-so, for repositories ten times this one.
9. **Rows**: 1a Review fast · 1b Review follows the tree · 1c the judgements through the reader · 1d the screens drawn and
   the reads completed · 1e the ref and remote acts · 1f the idle tree's acts · 1g history at scale · 1h the install, seen.
10. **Not built first**: 1f and 1g. **Not built**: libgit2, a cache on disk, Daoris's own history index, a working-tree act in
    the person's checkout, a force push, a delete on origin, line comments.

### 1. The brief's facts, checked against the code

Every `file:line` in the brief that I leaned on was read. What holds, I use without saying so again. What does not:

| Claim | What the code says | Weight |
|---|---|---|
| "Reads only (`git diff`, `git status`); the index is not refreshed to disk" (`Driver/Trees/WorkingTree.cs:317`, relayed by the brief's §3.2 as reads) | `git status` refreshes the index and **writes it back** unless `--no-optional-locks` or `GIT_OPTIONAL_LOCKS=0` (git's own documentation of the variable names `status` as the example); `git diff HEAD` does the same quiet refresh where it found stat-stale files. Review's `status` (`WorkingTree.cs:335`), the offer's (`Driver/Trees/SessionTrees/TreeWorkReads.cs:77`) and `CleanAsync`'s (`WorkingTree.cs:166`) pass neither; `GitWiring.Prompts` (`Driver/Platform/GitWiring.cs:28-34`) sets no `GIT_OPTIONAL_LOCKS`. Two readers already know the rule: `Driver/Conversations/Helpers/HelperCapture.cs:90,104` and `Driver/Help/HelpCheckouts.cs:104`. So a Review open can take `index.lock` in a **running** session's tree for the milliseconds of a write, and an agent's `git add` or `commit` at that instant fails with git's "index.lock exists". The probe measured with `GIT_OPTIONAL_LOCKS=0`, i.e. a status Daoris does not run. | A defect, fixed by one argument in row 1a; it also removes a write Daoris makes into a tree that is the session's (D51) |
| "12 + U starts at the least, 18 with the offer" | By the code: A is 3 + U (`rev-parse --show-toplevel`, `diff … HEAD`, `status`), B is 7-9 (`ReviewRanges.cs:42,55,72,77,104-108,125-126`, plus the line's 0-3), C is 2 (`WorkingTree.cs:302-304`), E is 1, F is at least 6 before the content holds (`TreeWorkReads.cs:40-48`: two `--show-toplevel`, `status`, `rev-parse --verify`, `symbolic-ref`, `log`). So ≥ 13 + U, ≥ 19 + U with the offer. | One more than the brief; immaterial |
| "whether the `commit-graph` carries changed-path Bloom filters" was not measured | `.git/objects/info/commit-graph` here holds a `BIDX` chunk: it carries them (and a `multi-pack-index` is beside the 33 packs). So the brief's history timings (`log --follow` 1.9-3.0 s, blame 1.1-4.4 s) are **with** Bloom filters; a repository without one walks every commit's tree for `log -- <path>` | Changes §9's numbers for other repositories |
| "This repository sets `core.fscache`" | It is set at git's **system** scope (Git for Windows' default), not by this repository; `core.fsmonitor` and `core.untrackedCache` are unset at every scope | Immaterial |
| "each git start costs 0.25-0.8 s now, against about 0.1 s on 2026-10-04" | Holds as measured. The comparison's cause is not git getting slower (§3): D147 §1's 100 ms was twenty starts from a shell (Git Bash finds `mingw64\bin\git.exe` directly, FIX-LOG GITSTART1) on an idle machine; PROC1a (FIX-LOG, 2026-10-15) already measured 193/465/720 ms by the load beside a start, `Process.Start` alone 21-171 ms, from a .NET host | The remedy is parallelism and fewer starts, not a faster git |
| "18 working trees", "28 refs", "8,857 commits" | 17 trees, 27 refs, 8,829 on `HEAD` today | Immaterial |
| "Recovery refs of Daoris's own (D51 rule 7)" (and the design's §3.1) | D51 has no numbered rule 7; the recovery ref is D102's SQUASHTIDY1c note and CLEANUP1 (`ContentHold.KeptAt`, `Driver/Trees/SessionTrees/ContentHolds.cs:25-26`) | A citation to fix in the design |
| `GitReads` "lives as long as the module" | Yes, and one per `GitRoutes` instance (`Modules/Bridge/Routes/GitRoutes.cs:19`); the branch list, Review, the previews and every judgement share none of it. Holds, and is the gap | — |
| Review's starts "share the gate's three background places" | Yes: `ChildProcess.RunAsync` and both streaming readers start as `StartClass.Background` (`Driver/Platform/ChildProcess.cs:41-42,60`, `WorkingTree.cs:1106,1199`), though a person pressed Review. They queue behind the sweep's and the tidy's fan-outs (`RefWatch.AtOnce = 2`, `HelpFacts` rules of two repositories at once) | §4.3 |
| The page re-reads on every move of a live session's record | `Web/src/bridge/sessions/trees.ts:182-184`: every `updated` past the answer's time, so each turn's end runs the whole chain again while the review is open; the head shares the key (`Web/src/work/WorkFrame.tsx:1645`) | §2.4 |

### 2. Review fast (Q1)

#### 2.1 Where the time goes

Of the 44.1 s case, 32.4 s is **one git per untracked file** (`WorkingTree.cs:359-400`: `diff --no-index -- /dev/null <path>`),
and the rest is **13-19 starts one after another**, each a start's latency (§3). The two diffs that carry the content
are 0.7-2.6 s together. Four levers, in the order of what they save:

1. **No git for an untracked file.** An untracked file's patch is the file: git's `--no-index` answer is a header
   (`diff --git a/<p> b/<p>`, `new file mode 100644`, `--- /dev/null`, `+++ b/<p>`, `@@ -0,0 +1,N @@`), each line with `+`, and
   `\ No newline at end of file` where it lacks one. Daoris reads the bytes (the 200 KB bound stays, `UntrackedPatchLimit`),
   applies D111's binary test (a NUL in the first 8,000 bytes, the same rule git's `buffer_is_binary` applies and
   `GitReads/FilePreview.cs` already holds), decodes UTF-8 as `ReadGitAsync` already does, and writes the patch itself. The
   mode is always `100644`: the page never shows it, and on this platform git's own `core.filemode=false` says the same.
   Byte-equality with git's answer over a fixture set is the proof (§11). Saves 32.4 s of 44.1 s; the number of untracked files
   stops mattering.
2. **Rounds of parallel starts instead of a chain.** Everything that does not depend on the review's base starts at once:
   the uncommitted diff, the status, the two merge-bases, the ancestry check. The base's dependants start the moment it is known.
   Three rounds deep at most (§2.2). Saves most of the 16.2 s.
3. **Answers keyed by ids, kept.** The committed range `from..head` (whole ids, or a `merge-tree` tree id, REVIEWBASE3) cannot
   change: kept in the one `GitReadCache` under `(commonDir, "range", from, head)`, with the bound's version in the key.
   A landed review (`LandedReviews.cs:48-66`) is ids throughout once the branch name resolves. The second open of an ended
   session asks git nothing; the second open of a live one asks only for the working tree. Saves the whole chain on re-opens.
4. **Layout and refs from disk, not from git.** `rev-parse --show-toplevel` (asked three times per open), `--git-common-dir`,
   `symbolic-ref HEAD`, `rev-parse --abbrev-ref HEAD` and `rev-parse --verify refs/heads/<name>` are files: `.git` (a folder, or
   a file naming `gitdir:`), `<gitdir>/commondir`, `<gitdir>/HEAD`, a loose ref or `packed-refs`. `GitBranches.GitDirectory`
   (`Driver/Trees/Branches/GitBranches.cs:284-302`) and `LineGuesses` (`Driver/Platform/LineGuesses.cs:25-54`) already read them.
   The git-walks-up hazard (D113 §1) is answered the same way: a folder is a top exactly when it holds its own `.git`. Reftable
   and anything odd fall back to one git call, as `LineGuesses.Stamp` falls back. Saves 5-6 starts per open.

#### 2.2 The new chain

Resolve first, with no process: the tree's layout; the refs snapshot (§5.1), giving `head` as an id, the line's and origin's
copies as ids, the recorded base's standing; the line through `LineGuesses`. Then:

| Round | Started together (each keyed by ids unless marked *tree*) | Depends on |
|---|---|---|
| 1 | `diff --no-optional-locks -c core.quotePath=false --no-color --no-ext-diff -M -z --raw --numstat -p HEAD` (*tree*); `status --no-optional-locks --porcelain=v2 -z --untracked-files=all` (*tree*); `merge-base <head> <line>`; `merge-base <head> <origin/line>`; `merge-base --is-ancestor <recorded> <head>` | nothing |
| 2 | only where two or more candidates: `merge-base --independent <candidates>`; where incomparable (REVIEWBASE3): `merge-tree --write-tree <lineBase> <recordBase>` | round 1's bases |
| 3 | `diff … -M -z --raw --numstat -p <from>..<head>` (REVIEW3's one-call reader, unchanged); `for-each-ref --format=%(ahead-behind:<head>) refs/heads/<line> refs/remotes/origin/<line>` (one call for both counts; the two `rev-list --count` where a git lacks the atom, GIT1a's fallback rule); the offer's `log --oneline <head> --not <tips…>` with the snapshot's tips named (so it is ids, not `--branches --remotes`); the landed proofs' id questions | round 1-2's `from` |

Untracked patches are read from disk while round 1 runs. The answer carries `head` beside `Base`, so the page's key and the
answer agree on what was read (§5.3). Cold: 7-10 starts (round 1 five, round 2 none to two, round 3 three to five), three
rounds deep; the longest round is the longest call in it (the range diff, 1-2.6 s for 368 files), not the sum. Warm, nothing
moved: 2 starts (the tree's two), or 0 with row 1b's tree watch. The ancestry, merge-base, independent and ahead-behind answers
are kept by ids too, so a re-open after a commit costs the tree's two plus one range diff plus one ahead-behind.

What the untracked read must not do: read a path `status` lists with a trailing `/` (a nested repository or an ignored
folder is no file), follow a link, or keep going past the patch budget (`PatchBudget`, `PatchCap` as today, counted as the
committed list counts).

#### 2.3 Targets, this machine, the brief's cases

Measured before, as the brief's probe measured them (serial, this tree, the load named); after, the same probe through
`daoris-driver git review <session> --time` (§10) with the load named beside each number, min/median/p90 of three runs.

| Case | Before (brief) | After, under the brief's load (median) | After, idle machine | Starts |
|---|---|---|---|---|
| 368 files committed, nothing untracked, first open | 16.2 s | ≤ 3 s | ≤ 1.5 s | ≤ 10, three rounds |
| 368 files, 50 untracked, first open | 44.1 s | ≤ 3.2 s | ≤ 1.6 s | ≤ 10; 0 per untracked file |
| 1,771 files committed, first open | 8.4 s | ≤ 4 s | ≤ 2.5 s | ≤ 10 |
| Opened again, nothing moved, live session | 16.2 s after 15 s | ≤ 1.5 s (row 1a: the tree's two starts) · ≤ 0.1 s (row 1b: none) | — | 2 · 0 |
| Opened again, ended or landed session | 16.2 s (once the record moved) | ≤ 0.1 s | — | 0 |
| A commit in a live tree while the review is open | 16.2 s per move of the record | the new answer shown ≤ 3 s after the ref write | — | ≤ 4 |
| The page's paint of 368 file rows | not measured | ≤ 0.5 s from the answer | — | — |

The brief's own "same facts in four starts at once" (0.95 s, 1.05 s, 1.6 s) is the floor these targets sit above, because
the chain keeps the base's dependency (three rounds, not one) and the gate's places are shared.

#### 2.4 Opened again, and a live session moving

- **An ended or landed session**: ids all the way; the page already keeps it forever (`trees.ts:168-171`), the reader keeps it
  across windows and across the page's `gcTime`.
- **A live session, nothing moved**: row 1a reads the tree's two calls again (the page's 15 s freshness stands); row 1b keeps
  the uncommitted answer under a watch of the tree (§6) and answers at once.
- **A live session that committed**: the ref watch hears `refs/heads/daoris/s-…` move (`RefWatch.Hears`,
  `Driver/Trees/RefWatch.cs:229-242`, 250 ms quiet). Today its wake feeds tidying and `help.*`
  (`Driver/Help/HelpFacts.cs:141-142`) and nothing of Review. Row 1b adds: the sessions whose tree is on that branch (the
  layout's `session-branches.json`, `TreeFollows.BranchOfTree`) get the state hub's `diff` scope published
  (`Web/src/bridge/state/stateHub.ts:31`), narrowed to those sessions; an open review refetches and is served the round-3 diff
  the judgement pre-read for it. The page's refetch on every `updated` (`trees.ts:182-184`) is then narrowed to moves the
  `diff` scope did not already cover; a turn's end without a commit re-reads the tree's two calls only.
- **The line moved** (a fetch, D109; a landing): the ref watch's hint again; `from` may change; one more range diff.
- **Stale never shown as current**: §5.3.

### 3. Why each git start got slower, and what to do

**My probe, 2026-10-23, this machine, not idle** (CPU 21-37%, 727 processes: 48 `dotnet`, 9 `claude`, 19 `node`, 20
`Daoris*`, 5 `git`), from Windows PowerShell 5.1 through .NET's `Process.Start` with redirected pipes, as Daoris starts
git, `GIT_OPTIONAL_LOCKS=0`, twelve serial starts each, min / median / max in ms:

| Start | min | median | max |
|---|---|---|---|
| `hostname.exe` (no git, no repository) | 732 | 1,229 | 5,972 |
| `cmd.exe /c exit` | 234 | 1,280 | 3,500 |
| `mingw64\bin\git.exe --version` (no repository) | 269 | 740 | 2,337 |
| `git.exe rev-parse HEAD` in this checkout | 200 | 719 | 1,647 |
| the same through `cmd\git.exe` (the launcher) | 196 | 758 | 3,163 |
| `git.exe merge-base HEAD origin/main` | 199 | 1,082 | 3,217 |
| `git.exe for-each-ref` (27 refs) | 157 | 712 | 2,116 |
| `git.exe symbolic-ref --short -q HEAD` | 99 | 583 | 1,549 |
| eight `rev-parse HEAD` started together, three rounds, all eight done | 330 | 974 | 1,943 |

Read with the brief's numbers and the fix log, four things stand:

1. **The cost is process creation, not git.** A program that opens no repository costs what git costs, and more. Git's own
   work in `rev-parse` is microseconds; `for-each-ref` over 27 refs and `merge-base` through a commit-graph are milliseconds.
2. **It is latency, not CPU.** Eight at once finish in the time of one or two. Something serialised per start is waited on:
   on this machine a third-party real-time protection is registered and on (Windows Security Center lists it enabled;
   Defender's real-time protection is off, as the brief read it), and UIFREEZE1's stack already found .NET's one
   process-start lock held under a slow `CreateProcess` "on a machine whose antivirus hooks process creation" (FIX-LOG
   2026-10-15). A scanner's verdict on each image load (git loads a dozen DLLs) is the kind of wait that overlaps across
   starts and does not across a chain.
3. **The load beside a start multiplies it.** PROC1a measured one fixture git at 193 ms at 40-50% CPU, 465 beside a spiking
   neighbour, 720 at 85%; today ten Claude Code sessions, Codex, fifty dotnet processes and other agents' git in sibling trees
   of this same repository run beside every Review. The tail (seconds) is the scheduler's.
4. **The launcher is not the cause any more** (GITSTART1 bypasses it for the system's git, `GitForWindows.Direct`,
   `Driver/Platform/GitForWindows.cs:112-122`); the two columns are the same within noise. The `-c core.longpaths=true`
   costs nothing measurable.

**What to do about it**, in order of what it is worth:

- **Start fewer**: layout and refs from disk, answers kept by ids, one call where two were (`ahead-behind` for both counts, the
  `--raw --numstat -p` reader already one call). §2 cuts Review from 13-19 + U to ≤ 10 and 0.
- **Start together**: rounds through the gate; the gate already queues asynchronously (PROCSTART2) and `Process.Start` under
  the lock is 21-171 ms of the 200-1,600 (PROC1a), so four in flight is the right width and nothing in Daoris needs changing
  there. The reader bounds itself at three starts in flight per repository, the background's places, so one Review cannot
  hold every place.
- **Do not start at all for object questions**, where a view asks hundreds: a held `cat-file --batch-command` (§4.2) for the
  screens and previews, measured, with the Windows hazard named. Not for Review.
- **What Daoris cannot fix and should say**: a scanner that hooks every process creation costs every tool on the machine. The
  machine log's `review.read` line (§11) carries `starts` and `waitedMs`, so when a start costs a second the line says the
  machine, not Daoris's git. An exclusion for `git.exe` or for the trees is the person's call, said once in a Get started
  step's hint at most; Daoris never changes it.
- **Not a library**: libgit2 would remove the start and keep every other cost D147 §4.3 named; the measured cost moves with
  parallelism, which git's binary gives for free.

### 4. The reader (Q2)

#### 4.1 One reader per repository, in the driver library

`GitReader` (new, `Driver/Trees/GitReads/`), one per repository keyed by its common git dir (a session's linked tree shares
its repository's, as `GitReadCache`'s key already does), handed out by `GitReaders`, one registry per process: the window's
`DriverLoop` holds one and every route and judge in that process reads through it; `daoris-driver` holds one for a verb's life;
a test takes its own with the seams it has today (`WorkingTree.GitRead`, `WorkingTree.Started`, `Runs`, `Answered`,
`ProcessStarts.Within`). A reader holds:

- **the layout** (`Layout`): the top, the git dir, the common dir, each linked tree's folder and `HEAD`, read from disk; a shape
  it cannot read (reftable, an odd `.git` file) is answered by one git call and said in the answer's `commands`;
- **the refs snapshot** (§5.1): one `for-each-ref` in `RefSnapshot.Format`'s shape over `refs/heads`, `refs/remotes`, `refs/tags`,
  plus each tree's `HEAD`; held while valid;
- **the id-keyed cache**: the one `GitReadCache` (64 MiB, LRU) shared by Review, the screens, the previews and the judgements,
  with today's "never kept" rules (a read git did not answer, a shallow repository, a name's commit);
- **in-flight joining**: one read per key under way; a second asker awaits it (what `RouteFlights` does for `LINES`, now per
  git question);
- **the start bound**: at most three of this reader's starts in flight, all `StartClass.Background` as today; a round is the
  set its caller hands over, started as places come;
- **the held process** (§4.2), optional, started on the first object question and ended 30 s after the last.

Every start still goes through `WorkingTree.GitStart` (`WorkingTree.cs:894-930`): Tools' git, `GitWiring.Hand`, the bounds
of `GitBounds`. `GitFacts` (LANDGIT1) and `RefSnapshot` (PROC1d) keep their flow-scoped semantics for the landing's and the
judgement's "asked once within this flow, dropped by this flow's writes"; they consult the reader first for ids and layout,
and the reader never answers a question those classes refuse to keep (a `HEAD`, a ref name, a `status`), because by then it
has resolved them to ids or declined.

**Per area or one?** One, for the id-keyed answers and the refs, because the same ids are asked by Review, the landing's proof
and the commit page; per area would keep three copies and three invalidations. The areas differ only in what they are allowed
to keep (a judgement keeps nothing across its own writes), and that stays with the area.

#### 4.2 The held process: what it answers, what it never does

`git cat-file --batch-command` (git ≥ 2.36; `--batch`/`--batch-check` below it), one per reader, through `GitStart` so it is
Tools' git under the no-prompt environment, started through the gate once (one place for its start, none for its run, as
PROCSTART2 says of every run), fed on stdin, answers read by the object id each answer echoes, so a desynced stream is caught
and the process ended and started again. It answers **object questions only**: a commit's fields and parents, a tree's entries,
a blob's bytes for a preview, an object's existence (`info`). That is the commit page's first call (`log -1` today,
`GitReads.cs:305`), every file preview (`FilePreview`), a landed proof's `cat-file -e` and `rev-parse --verify <id>`, and the
graph page's author and subject lookups where the page later asks beyond what one `log` carried. It never answers a walk
(`log`, `rev-list`, `merge-base`, `blame`, `diff`, `status`): those stay one process each, in rounds.

**The Windows hazard, named**: a process holding pack files open keeps a repack from deleting them, and Git for Windows then
asks the person "Unlink of file … failed. Should I try again?" in their own terminal, GitKraken or Sourcetree. So the held
process lives 30 s past its last question and never through an idle minute; a burst (a review's previews, a commit page, a
graph page's details) shares one start, and nothing holds packs between bursts. Row 1d measures it on this repository with a
`git gc` beside it before the row is accepted. Review and the judgements do not need it; if the measurement shows the hazard
bites, the screens live without it and lose 150-250 ms a preview.

#### 4.3 Bounded parallel, and the gate of four

The gate stays as it is (`Driver/Platform/ProcessStarts.cs:51-57`): four places, three the background's, a person's start first
(D181 point 5). Review's reads keep `StartClass.Background` even though a person pressed, because a person's reserved place is
the agent's and a review's five starts would otherwise hold every place. What changes is above the gate: a reader hands a
round's starts over together, at most three in flight, so a Review waits at the gate at most one start's time per round behind
a sweep's fan-out. The `review.read` line's `waitedMs` (§11) measures that wait on the install; if it shows, a later row gives a
pressed read one place of its own, never the agents'.

#### 4.4 In which process

The driver library, used by the modules in the window's process (where `SESSION_DIFF` and `GIT_*` answer and the loop's
judges run), by `daoris-driver` (the terminal door, D50), and by the tests. Not a process of Daoris's own serving git: a
second implementation behind a protocol, and the gate is per process anyway. The service keeps its one `ls-files` for ignored
paths (`Daoris.Service.Core/Registry/IgnoredPaths.cs:92-114`): a different process with no home, one start per index pass,
reaching no remote; untouched by this design.

### 5. Caching and invalidation (Q3)

#### 5.1 The refs snapshot: kept while the files stand, dropped by the watch

Read once per reader by one `for-each-ref` (and the trees' `HEAD` files), kept with a **stamp**: the write time and size of
`packed-refs`, `HEAD`, each tree's `HEAD`, and every file under `refs/heads`, `refs/remotes` and `refs/tags` (a walk of some tens
of files here; `LineGuesses.Stamp` is the rule, widened). Before any use the stamp is read again; a difference drops the
snapshot and reads it afresh. The ref watch drops it eagerly on its hint for that repository (`RefWatch.JudgeAsync`,
`RefWatch.cs:345-376`), and `RefMoves.Mine` on Daoris's own move. Reftable: no stamp, re-read on every use (one start), as today.
The ref watch's set widens from "repositories holding Daoris's work" (`RefWatch.cs:249-265`) to every registered repository
with a checkout, within `ChangeWatch.MostRoots` (32), those holding work first; past the cap the stamp alone guards.

This is not a cache of answers under a ref name; it is the ref store read once and validated against the files git itself
writes (git renames each ref into place, the event the watch already relies on, `RefWatch.cs:231`). D147 §4.2 is kept whole:
**a ref is never a key; what the snapshot resolved it to is.**

#### 5.2 What is kept, under what key

| Answer | Key (all whole ids) | Kept |
|---|---|---|
| A range's files and patches (Review's committed part, a commit's changes, a compare, a landed branch's changes) | `commonDir, "range", from, to, bound-version` | until evicted (64 MiB LRU) |
| `merge-base`, `--is-ancestor`, `--independent`, `merge-tree --write-tree`, `rev-list --count`, `ahead-behind` of two ids | `commonDir, command, ids…` | until evicted; `--is-ancestor`'s "no" (exit 1) is an answer, as `GitFacts.Answered` holds |
| The commits only on `head` and not on the snapshot's tips (the offer's unlanded count) | `commonDir, "unlanded", head, tips…` (sorted, ≤ 256 as `GraphTips`) | until evicted |
| A graph page, a file's history, a blame, a commit's fields (GIT1b, unchanged) | as today | as today |
| A file's history's **whole id list** at `(commit, path)` (§9) | `commonDir, "history-ids", commit, path` | until evicted |
| A search over history by message, author or path | `commonDir, "search", tips…, query` | until evicted |
| The layout | the folder | until the folder's `.git` stamp changes |
| The refs snapshot | the repository | until its stamp or the watch says otherwise |
| **Never**: `status`, `diff HEAD`, anything the working tree decides; a name's commit on its own; anything from a shallow repository; a read git did not answer | | |

#### 5.3 A stale answer is never shown as current

- Every answer **names the ids it was read at** (`Base` today; `head`, and the tips where they bound the answer), and the page
  keys by them (`git.ts` already does for the screens, D147's GIT1d-b; Review's key gains `head`).
- The refs snapshot is validated against the files **at every use**, so a resolution is never older than the last ref write,
  whatever the watch heard.
- A move the watch hears publishes the `diff` scope for the sessions it touches (row 1b) and the `lines` and the branch list's
  keys for the repository, so an open page asks again at once; a move the watch missed is caught by the stamp at the next
  ask and by the topic's five-minute deadline.
- A judgement's own writes drop its flow's facts before and after (`GitFacts.Ran`, `RefSnapshot.Ran`), unchanged; they also
  call `RefMoves.Mine`, which drops the reader's snapshot at once.
- The page shows a kept answer dimmed while a newer read is in flight (D41's rule, `DiffPane.tsx:137`), and the review's head
  says the commit it reads at.

### 6. Working-tree state (Q4)

Uncommitted and untracked work is read by two calls, started first in round 1, each with `--no-optional-locks` so nothing
Daoris reads writes a tree that is the session's (D51; the defect of §1): `diff … HEAD` for the tracked changes and
`status --porcelain=v2 -z --untracked-files=all` for the rest, both in one machine format; the untracked patches come from
the files (§2.1). Neither is kept by the reader in row 1a: the page's 15 s freshness and the in-flight join are the bound.

Row 1b keeps the uncommitted answer under **a watch of the tree**: one `ChangeWatch` root on the tree's folder for each review
open on a live session (released when the review closes or after fifteen idle minutes; a few at a time, far under the cap),
filtering out `.git` and the tree's ignored folders as `IgnoredPaths` names them, 250 ms quiet, 2 s cap, a loss or an overflow
dropping the answer at once (`ChangeWatch`'s recovery). The kept answer is also stamped by the tree's `index` file (write time
and size): an agent's `git add` or `commit` changes it. A hint, a stamp change, or the ref watch's hint for the tree's branch
drops the answer; the page is told through the `diff` scope and reads again, served from a fresh judgement where one already
ran. This is VS Code's rule (the brief's §5) kept inside D173's events: a hint is a wake, never a value.

**Not done, and why**: `core.fsmonitor` and `core.untrackedCache` write the repository's configuration and (the first) start a
daemon the person did not ask for; given on the command line (`-c core.fsmonitor=true`) they would still start that daemon and
still not serve the person's own git. `status` is one of five parallel calls in round 1 and is not the wall. §9's press offers
both to the person, as their configuration, for a repository where `status` is the wall. Measured here: `status` clean with
ignored `node_modules` was 0.6-3.8 s (the brief); with the tree watch it runs once per change, not once per open.

### 7. What the person gets (Q5)

The organising object stays the branch, then the commit (D55, D147 point 2); git lives in Repositories' Branches and History
tabs (D150 point 3) and in a session's Review (D147 §2.7); the side bar and the panel are unchanged (D118 §3c). **Per
repository** means the person's checkout, read through its refs and objects; **per session tree** means a tree Daoris opened
(D51), read while it stands and acted on only while it is idle (its session ended, or waiting on the person, with no agent
process). An agent uses git itself in its tree; Daoris's reads are not offered to it as tools (it would double the starts and
couple it to Daoris's bounds); the only door an agent shares is D198's, which every remote act of its own already passes.

| Capability (the brief's §5) | Per repository | Per session tree | Where | Row |
|---|---|---|---|---|
| History graph | built; gains filters: branches shown, author, path, since/until (one `log` with those arguments, keyed by tips and the filter) | the branch's page beside its line (built) | History tab; Review's head → *Show in Git* (GIT1i) | 1d |
| Commit details | the route is built (`GitReads.CommitAsync`); drawn: message, author, committer, parents as doors, the refs holding it (never kept), the trace's *who made it*, its changes by `PatchView` | the same | a commit's page | 1d |
| Diff views | side by side, word diff, whitespace (a re-read with `-w` keyed with the option), both computed on the page from the one patch where git's answer is not needed; image diff: no (a binary says so, D111) | the same, in Review | Review, a commit's page | 1d |
| Blame | built; drawn as a gutter, runs grouped, *Before this change* | the same at the tree's `HEAD` | a file row's *Blame*; the preview's *History and blame* | 1d |
| File history | built; drawn, following renames; pages from the kept id list (§9) | the same | a file row's *History* | 1d |
| Compare | built; drawn: two refs resolved once, commits on each side, `A...B` and `A..B` | the tree's branch against its line | a branch's *Compare with…* | 1d |
| Search | messages and authors (`log --all -i --grep`/`--author`, one call, kept by tips and query, 0.7-1.3 s here), paths (`ls-tree -r --name-only <commit>` filtered on the page), content **at a commit** (`git grep -n <commit> -- <paths>`, one call); content **across history** (`-S`/`-G`) as a long read: path-scoped, streamed, cancellable, results shown as they come, never kept (12-14 s here) | the same from the tree's `HEAD` | History tab's search field | 1d (first three), 1g (the pickaxe) |
| Branches | list (built); create, delete (GIT1g); push (GIT1h); **rename** (local, not where any tree has it checked out: `branch -m` writes the ref and its upstream line in the checkout's configuration, said in the plan); track/upstream: no (configuration, D147 rejected `push -u` for the same reason); checkout: no, the command to copy | — | Branches tab | 1e |
| Stash | list and show (`stash list`, `stash show -p <id>` by the stash's commit id) | save, apply, pop, drop on an idle tree | Branches tab (list); Review's head (acts) | 1d (reads), 1f (acts) |
| Staging by hunk and line; composing a commit; amend | no: the checkout is theirs (D113 §5, D147 §3.1) | yes, on an idle tree: stage and unstage by file, hunk and line (`apply --cached` of a patch Daoris composes from the shown hunk, never an editor), commit as the person (D198 point 5's author rule where the repository names none), amend the last commit where no remote holds it (a session branch is never pushed, D147 point 3) | Review's file rows and head | 1f |
| Merge and conflict views | no | yes, on an idle tree after a replay's conflict (D109's replay aborts and names the files): three panes from `merge-tree`'s stages and `cat-file`, resolved on the page, written to the tree and `add`ed, then committed as the person | Review | 1f |
| Rebase | no | reorder, squash, reword and drop the tree's **own** commits since its base (a plan over `rebase -i`'s todo, never beyond the base, aborted whole on conflict); a replay onto the line stays D109's | Review's head | 1f |
| Cherry-pick, revert, reset | no (`reset` moves the checkout's `HEAD`) | cherry-pick a commit onto the tree's branch; revert one of its own; no reset (the tree's commits are its record; a discard is D88's door) | a commit's page → *Pick onto…*; Review | 1f |
| Remotes | list (`remote -v` through `GitRemotes.AddressOf`, D198's one reader); fetch `--prune` (GIT1g); push (GIT1h); pull requests by plugin; add, remove, rename: no here (a repository's remote row is GITAUTH1e's Setup) | — | Branches tab's head; Setup | 1e |
| Tags | list in the snapshot's `refs/tags` (shown on the graph and a commit); create at a commit, delete local (refs); push a tag: D198's *Dangerous* class, offered only where the person's git access answers it | — | a commit's page; Branches tab | 1e |
| Submodules | list (`.gitmodules` and the `160000` entries of one `ls-tree -r <commit>`), each a door to its own repository where registered; update: no (writes the checkout) | the same | a commit's files; Details | 1g |
| LFS | pointer awareness: a blob starting `version https://git-lfs.github.com/spec/` is drawn as *an LFS pointer* with its size, in Review and a commit's page; lock, track: no | the same | Review, commit page | 1d |
| Worktrees | listed (the list's `worktreepath`; `.git/worktrees/*` from disk); Daoris's session trees are the trees; a person's own tree: not now (§13) | — | Branches tab | 1d |
| Reflog, undo | a branch's reflog as a read (`reflog show <branch>`, never kept); Daoris's own undo stays the recovery ref (D102's SQUASHTIDY1c, CLEANUP1) and each act's sentence naming the commit that brings it back | the same | a branch's page | 1d |

Both languages: the chrome's new terms go to D116's check before they are drawn, with git's own Chinese translation as
the source where it has one: *blame* 逐行追溯, *compare* 对比, *history* 历史, *stash* 贮藏, *tag* 标签, *submodule* 子模块,
*cherry-pick* 拣选, *revert* 还原, *rebase* 变基, *stage* 暂存, *hunk* 块, *conflict* 冲突, *reflog* 引用日志, *search* 搜索.
*graph* 提交图, *review* 审阅, *branch* 分支, *tree* 工作树, *fetch* 获取 and *git remote* 远程仓库 are already in the glossary.
Authored content (commit messages, patches, file names) stays as written. Each screen is counted by UX6a's counter.

### 8. Writes (Q6)

Every act is planned, shown with the command it will run and judged again at the commit shown (D147 §3.1); its refusals
are D147's where it has them. Whose tree: **refs and remotes** touch no tree; **tree acts** touch an idle session tree only.
D198: an act that reaches a remote passes the doors and is judged Read / Write / Dangerous there, for the landing and the
person's press alike; a local act passes no door; a commit the person makes carries the identity's `commitsAs` where the
repository names no author (D198 point 5). Undo is said in the act's own sentence.

| Act | Tree touched | Press and plan | Refused when | D198 | Undo |
|---|---|---|---|---|---|
| Fetch (`fetch origin --prune`) | none | one press, per repository or all shown | no origin; a held remote (GITAUTH1a) | Read, at the door; nothing prompts | none needed; prune's removals named |
| Create a branch at a commit | none | a drawer: name, commit | exists; `daoris/`; a name git refuses | — | delete it (below) |
| Rename a local branch | none (its configuration line moves) | plan says the configuration line | checked out in any tree; the line; a session branch; a landed branch (its record names it) | — | rename back |
| Delete a local branch | none | plan; second press where its commits are held by nothing | the line; checked out; a session or landed branch (their own doors) | — | the sentence names the commit; `branch <name> <commit>` |
| Push one branch at a commit | none | press, exactly the commit shown | the line; a session branch; a tree holds it; not a fast-forward | Write at the door; the person's press is the grant, as the landing's is (D198 point 13) | none: a push is outward (D37) |
| Create a tag at a commit; delete a local tag | none | plan | a tag that exists; a tag a remote holds (for delete) | — | the sentence names the commit |
| Push a tag | none | press | — | **Dangerous** (D198 point 8): offered only where git access answers *allow* or *ask*; the door asks | none |
| Stage, unstage (file, hunk, line) | an idle session tree's index | the row's press; the plan is the hunk shown | the session runs or an agent process holds the tree (the session lock, D51); the tree is the person's checkout (never offered) | — | the opposite press |
| Commit as the person; amend own last | the tree | a form: message; amend only where no remote holds it | as above; nothing staged; a remote holds the commit | the author rule (point 5) | amend; the reflog's sentence |
| Stash save / apply / pop / drop | the tree and `refs/stash` | plan names the files | as above; a conflict on apply aborts whole | — | the stash stays until dropped; drop's sentence names its commit |
| Resolve a conflict | the tree (the files named), then `add` | the three panes; *Mark resolved* writes the file | as above; a file not in conflict | — | the replay's abort (D109) |
| Cherry-pick onto the tree's branch; revert one of its own | the tree's branch (a new commit) | plan shows the commit | as above; a conflict aborts whole | — | revert of the new commit |
| Reorder / squash / reword / drop own commits since the base | the tree's branch | a list to reorder, applied as one `rebase -i` with a prepared todo | as above; beyond the base; a conflict aborts whole | — | the recovery ref kept before the rewrite (SQUASHTIDY1c's pattern), named in the sentence |

What stays refused, with the reason: any of the tree acts in the person's checkout (D113 §5: their editor may hold the files;
nothing in Daoris's flow needs it); any tree act while an agent runs there (D51: two writers corrupt one index); a force push
and a delete on origin (D147 §2.8: what others may hold); a tag or branch pushed by a rule (a push is a press, D147 point 4);
checkout and switch (the command to copy; a person who wants a branch on disk has a terminal, D96).

### 9. History at scale (Q7)

On a repository of tens of thousands of commits: a graph page walks until it has 200 in date order (bounded by the page, not
the history); a commit, a compare and a search by message walk what they must in one call each. The three that walk the whole
history per ask are a file's history (`--follow` ignores `--skip`, GIT1b), a blame of a long-lived file, and the pickaxe.

- **Git's commit-graph with changed-path Bloom filters is the index**, and this repository already has one (§1). Daoris's rule
  stands: nothing written into `.git` on its own. **One narrow amendment to D147 §4.2**: a per-repository press on the
  History tab's head, *Speed up history here*, visible only where the repository has no commit-graph or no Bloom filters
  (read from `objects/info/commit-graph`'s chunks, no process), which runs `git commit-graph write --reachable --changed-paths`
  as the person, says what it writes and that git's own `gc` keeps it, with `daoris-driver git speed-up <repository>` as
  its twin, and Ask Daoris's `git` kind may propose it. A second press on the same head offers `core.untrackedCache` and
  `core.fsmonitor` for a repository where `status` is the wall, each named, each a configuration line the person can
  remove. A press is the person's own configuration deciding, through a door that says exactly what it changes.
- **A file's history keeps its id list**: one `log --follow --format=%H` walk at `(commit, path)` keeps the ids (40 bytes each;
  a 2,000-commit history is 80 KB); a page is a slice, read for its fields through the held process or one `log --no-walk
  --stdin`. The third page stops walking from the top.
- **The pickaxe is a long read**, path-scoped, streamed and cancellable, with `--max-count` and the time it has run shown;
  never kept; its sentence offers `git log -S<text> -- <path>` to copy. Content at one commit is `git grep`, which is fast
  and one call.
- **Blame** at scale: `--since`/`-L` are not offered; a blame is bounded by D111 as today and keyed by ids, so a second look
  is free. Where it is slow, the head says so and names the command.
- **Rejected: Daoris's own index of history** (JetBrains' log index). A second record on disk, which D147 §4.2 forbids for a
  reason this design keeps: git's object store is the cache, and a copy diverges. Its gains (filter by author and text across
  all branches in milliseconds) are not what Daoris's person does in Daoris.

### 10. The terminal door and Ask Daoris (Q8)

D50: what the window does, the terminal can. D147 point 5's rule stands for reads: each view names git's own command. Added:

- `daoris-driver git review <session> [--json] [--time]`: Review's answer as the page has it; `--time` prints the stages (the
  rounds, each start's command, wait and run, the untracked count, what was kept) and writes the `review.read` line. This is
  the measurement door (§11).
- `daoris-driver git branches|fetch|branch|rename|delete|push|tag [--plan|--yes]` (GIT1c, GIT1g, GIT1h, with the two new verbs),
  one judge in the driver library for both doors.
- `daoris-driver git tree <session> stage|unstage|commit|amend|stash|resolve|pick|revert|reorder [--plan|--yes]` (row 1f).
- `daoris-driver git speed-up <repository> [--untracked-cache] [--fsmonitor] [--yes]` (row 1g).
- `daoris-driver git identity …`, `git default`, `git use`, `git access` are D198's and unchanged.

Ask Daoris (D110): the reads stay exempt, as `trace` is. A `git` kind carries the ref and remote acts and the speed-up as cards
the person presses (GIT1k, widened); a `git-tree` kind carries the idle tree's acts, proposed only, never applied by Ask
Daoris. `HelpCoverageTests` holds each control to its kind or its exemption.

### 11. What to measure and how (Q9)

**The proof of row 1a**, in three parts, each kept as a gate or a bench:

1. **Counts, load-independent, in the fast half** (`dotnet test … --filter Category!=Process`): through the `GitRead` seam
   (REVIEW3's `TreeDiffTests` pattern) and `WorkingTree.Started`, a review open with 50 untracked files starts **0** git per
   untracked file, **≤ 10** in all, in **≤ 3 rounds** (the seam records each start's begin and end; a round is a set that
   overlaps); opened again with nothing moved: 2 (row 1a) and 0 (row 1b); a landed session: 0. A stand-in whose ref moves
   between the resolve and the read makes the answer name the new ids or be read again, never the old ids as current.
2. **Byte-equality with git, in the Process half** (`Parallel=Git`, four at once, `git.process.runsettings`): over a fixture of
   untracked files (ASCII, UTF-8 with a non-ASCII path, CRLF, no trailing newline, empty, a NUL inside, over 200 KB, a nested
   repository, a link), Daoris's patch equals `git diff --no-index -- /dev/null <path>`'s text for each text file, and says
   binary exactly where git does. The refs snapshot's stamp drops on a loose ref write, a `pack-refs`, a `HEAD` move and a
   linked tree's `HEAD` move, each against real git. `--no-optional-locks`: a status in a tree whose index is stat-stale
   leaves its write time unchanged.
3. **The bench, reported, never failing** (D54's rule for budgets; a tool outside `verify` as the benches are):
   `daoris-driver git review <session> --time` over this repository's three cases (368 files; the same with 50 untracked made
   by the bench; 1,771), three runs each, min/median/p90 of wall, starts, rounds, `waitedMs`, the machine's load beside each
   (CPU %, process counts, as the brief did). Before: the brief's numbers. After: §2.3's targets. Written to FIX-LOG's
   entry for the row with the sha and the load.

**On the install** (row 1h, owner-permitted): Review opened from the press on the install, timed from the press to the first
paint: the page writes `review.painted` through `logEvent` (`Web/src/bridge/log.ts`, as `view.opened` is written) with the
session and the ms; the driver writes `review.read` with `session`, `ms`, `starts`, `rounds`, `waitedMs`, `files`, `untracked`,
`kept` (the ids served from the cache) and `head`; `change.judged` for the `diff` topic names the wake. Three opens before
the republish and three after, under the owner's working load, go in FIX-LOG.

**The regression check that keeps it**: part 1's counts are a gate; a change that adds a serial start to Review's chain or a
git per file turns it red. The bench's medians are read at each release's rehearsal and said in the release note's
measurements line; a rise past 2× the row's number is a row, not a failure.

**Row 1d's extra measurements**: the held process against a `git gc` beside it on this repository (does the person's git ask
to retry?); the graph page, a commit, a blame and the search on this repository and on a public 70,000-commit repository
cloned for the bench (git's own), with and without a commit-graph, before the press and after.

### 12. The rows (Q10), in buildable order

- **GITAPP1a — Review fast** (driver, modules, web). `GitReader` and `GitReaders`: the layout from disk, the refs snapshot
  with its stamp, the one shared `GitReadCache`, in-flight joining, the per-reader bound; Review's chain as §2.2 (the rounds,
  ids throughout, `--no-optional-locks` on every working-tree read of Daoris's, `ahead-behind` for both counts, the offer's
  `log` over the snapshot's tips); untracked patches from disk; the answer carrying `head`; the page's key by `head`; the file
  rows past the first forty collapsed with `PatchView` mounted on open (`DiffFileRow`'s disclosure exists); the `review.read`
  line; `daoris-driver git review --time`. **Proof**: §11 parts 1-3; the bench's before and after in FIX-LOG; `GitReadsTests`
  unchanged; `ProcessStartsCensusTests` naming no new direct start. Amends nothing.
- **GITAPP1b — Review follows the tree** (driver, modules, web; after 1a). The ref watch widened to registered repositories
  within the cap; its hint publishes the `diff` scope for the sessions on the moved branch and pre-reads their round 3; the
  tree watch for open reviews keeping the uncommitted answer; the page's refetch on `updated` narrowed. **Proof**: a fake
  `IChangeSource` moving a ref makes an open review's query invalidate and the next read serve from the cache (0 starts for
  the committed part); a file written under the tree drops the kept answer; opened again with nothing moved: 0 starts; the
  landing, discard, sweep and sync publishes unchanged. Amends nothing (D173's rule: a hint is a wake).
- **GITAPP1c — the judgements through the reader** (driver; after 1a). `help.trees`, `help.work`, `help.sweep`, the tidy, the
  landing's planning, `GitBranches.ListAsync` and `LandedReviews` take layout and refs from the reader and id questions from
  the cache; `GitFacts` and `RefSnapshot` consult it first, keeping their flow-scoped drops. **Proof**: PROC1a's start counts
  per judgement before and after (`rev-parse --verify`, `symbolic-ref`, `worktree list`, `--show-toplevel`, `--git-common-dir`,
  `--abbrev-ref` each to zero in a judgement whose refs stand); `SweepLineProcessTests`' counts; the Process half's wall;
  the install's `change.judged` `ms` for `refs`. Amends nothing.
- **GITAPP1d — the screens drawn, and the reads completed** (web, driver, modules; after 1a; takes GIT1f and GIT1i). The
  commit, file history, blame and compare pages; the graph's filters; search by message, author, path and content at a commit;
  stashes, tags, remotes, worktrees and a branch's reflog as reads; LFS pointers in the patch parser; side-by-side, word diff
  and whitespace on the page; the held `cat-file --batch-command` for previews and commit fields, 30 s idle, measured for the
  pack-hold hazard; the glossary terms through D116's check; stories at 1546 and 680 px in both themes and both languages.
  **Proof**: each screen's story and counter; `GitReadsTests` for each new read's one call and key; the hazard measured; the
  `HelpCoverageTests` rows. Amends D147's design §2.4-§2.6 with what it draws; nothing decided.
- **GITAPP1e — the ref and remote acts** (driver, modules, web; after 1d; takes GIT1g, GIT1h, GIT1k). As D147 §3.1 and §8 of
  the brief's table, plus rename and tags; the plan-and-press judge; the doors' classes; the `git` kind. **Proof**: as GIT1g
  and GIT1h name it (bare-origin fixtures, the pre-push hook running, a push's class judged at the door, a tag push asked);
  the terminal twins' usage. Amends nothing beyond D147's own GIT1g/h notes.
- **GITAPP1f — the person finishes a session's work in its idle tree** (driver, modules, web; after 1d and 1e; a decision,
  amending D147 §2.8 as §7-§8 say). Stage and unstage by file, hunk and line; commit and amend as the person; stash; the
  conflict view after a replay; cherry-pick and revert; reorder own commits since the base; the `git-tree` kind; the
  terminal's `git tree` verbs. **Proof**: the session lock refusing every act while an agent holds the tree (a stand-in
  session running); each act over a real fixture in the Process half; a person's commit landing through the ordinary press
  with its author; the recovery ref before a rewrite.
- **GITAPP1g — history at scale** (driver, web; after 1d; a decision, amending D147 §4.2 by the press). The file history's
  kept id list; the pickaxe as a long read; submodules listed and opened; the *Speed up history here* press with its twin
  and kind. **Proof**: the 70,000-commit bench before and after the press; the press writing exactly the files and lines it
  says and nothing else (a diff of `.git` before and after).
- **GITAPP1h — the install, seen** (the owner; after 1a at least, again after 1d). The three opens before and after; the
  screens on the window in both languages. **Proof**: FIX-LOG's entry; the screenshots.

The first row alone makes Review fast; the second makes it stay fast while a session moves; the third pays the same debt
where the person waits on the look; the fourth is the half of D147 that is built and drawn nowhere.

### 13. What is rejected (Q11)

- **A long-lived git process as the reader's heart.** `cat-file --batch` answers 0.1 ms a question and nothing Review asks is
  an object question; the walks and diffs stay one process each. It is kept small and optional (§4.2) because on Windows a
  process holding packs makes the person's own `git gc` ask to retry.
- **libgit2 / LibGit2Sharp** (D147 §4.3 stands): the start it saves is a latency parallelism already hides; it brings two
  implementations, no hooks, no partial clone, no reftable, and a native library in the install.
- **Daoris's own index of history** (§9): a second record; its gains are not this person's.
- **A cache on disk** (D147 §4.2): git's store is the cache; the in-memory cache survives as long as the process, and a cold
  open's cost is one round of starts.
- **Writing `commit-graph`, `fsmonitor` or the untracked cache on Daoris's own** (D147 §4.2): kept; the press (§9) is the one
  amendment, and the first four rows need none of it.
- **Review's reads as `StartClass.Person`** (§4.3): the reserved place is the agent's; measured first.
- **One reader per area** (§4.1): three copies of the same ids.
- **Keying anything by a ref name** (D147 §4.2): the snapshot resolves, the ids key.
- **A tree watch for every session tree**: only open reviews; a watch per tree would hold a root per session, under node
  module storms, for nobody looking.
- **Checkout, stash, reset, rebase, cherry-pick, revert or staging in the person's checkout** (D113 §5, D147 §3.1): their
  editor holds the files; nothing in Daoris's flow needs it; the command to copy stays.
- **Any tree act while an agent runs there** (D51).
- **A force push, a delete on origin, a push by rule** (D147 §2.8, point 4).
- **A person's own tree per branch** (D147 §7 left it until someone asks): the idle session tree carries every act the flow
  needs; a tree with no session behind it is a second kind of tree with its own lock, cleanup and knowledge rules.
- **Agents reading through Daoris's reader as tools**: they have git; the doors are the one shared door.
- **A separate git-serving process of Daoris's**: a protocol around a library Daoris does not have.
- **Line comments and review threads** (the working surface design §5): Daoris is not a code-review product.
- **Fetching on a timer** (D109): unchanged.
- **Changing the gate's width**: PROC1a measured no gain past four whichever host starts.

### 14. Decisions amended, and what I did not measure

**Amended**: D147's design §2.4-§2.6 (drawn as §7 says; row 1d); D147 §2.8 by row 1f's acts on an idle session tree, with
D51's exclusion and D113 §5 untouched; D147 §4.2 by the per-repository press of row 1g, and by the sentence "a ref is never a
key; the refs snapshot resolves it and the ids key" as a note. D147 point 5's verbs widen (§10). D147's design §3.1 cites
"D51 rule 7" and should cite D102's SQUASHTIDY1c note. D181, D198, D150, D109, D113, D164, D173: unchanged. GIT1f, GIT1g,
GIT1h, GIT1i and GIT1k fold into rows 1d and 1e; GIT1j stands as filed.

**Not measured here**: the install's window (the page's paint of 368 rows; the rows collapsed past forty is a guess at where
the paint goes); Review's `LANDING`, `HANDOFF_PLAN` and `OPINION_GATE` beside it (they share the gate and are read through the
same reader in row 1c, not timed); a tree with hundreds of real untracked files (the fixture makes 50; the read from disk is
linear and bounded by the patch budget); the held process's pack-hold hazard (named, measured in row 1d before it is kept);
the refs stamp on a reftable repository (none here; it falls back to git each use); the tree watch under an agent's
`npm ci` (the cap and the overflow recovery are `ChangeWatch`'s, held by its tests, not seen on a tree); `fsmonitor` and the
untracked cache on this repository (their press writes the person's configuration, so they wait for the person); an idle
machine (every number here is under the load named). My probe's numbers are twelve starts each from Windows PowerShell 5.1's
.NET Framework, whose `Process.Start` holds a lock of its own as .NET 10's does; the brief's probe ran from .NET 10 and read
the same order of cost.
