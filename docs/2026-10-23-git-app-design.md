# Daoris's git, as comprehensive as a git management app, and Review fast: the design (GITAPP1, 2026-10-23)

**Status: proposed (D202), round 2, after Codex's read and Fable's foundation read of round 1 (D191).** Round 1 was the synthesis,
written by Claude Opus from the brief (`docs/2026-10-23-git-app-brief.md`) and the three sealed proposals
(`docs/2026-10-23-git-app-proposals.md`: Claude's, Codex's through Daoris's helper, Fable's), at `92c7b8ed5`. The two reads of it are
`docs/2026-10-23-git-app-reads.md`: Codex's (29 findings, 22 blocking; keep proposed and revise) and Fable's (B1-B3 blocking, N1-N16
not; accept after them). Round 2, by Claude Opus, checked each finding against the code at `fe4809b75` and against REVIEWLOCK1's
branch (`daoris/s-e276dfc9`, `445720582`..`f80814215`), and takes or answers each in §16. Nothing is built. `Dnn` is
`docs/decisions/Dnn.md`; `Driver/` is `src/Daoris.Desktop/Daoris.Desktop.Driver/`, `Modules/` is
`src/Daoris.Desktop/Daoris.Desktop.Modules/`, `Web/` is `src/Daoris.Web/`. "Claude", "Codex" and "Fable" below name the three
proposals, and §-numbers prefixed C, X and F point into them; "Codex n" and "Fable Bn/Nn" name the reads' findings.

## 0. The answer in fourteen lines

1. **Review's cost is serial process starts**, and a start on this machine costs 0.2-1.6 s. The likeliest cause, inferred from
   PROC1a's and UIFREEZE1's stacks and two probes rather than proved, is process creation slowed by a real-time scanner and load
   (§1.2). The cure is fewer starts, started together.
2. **Review is a dependency graph of id-keyed reads**, built as data in the driver and held by a test (§3): **depth 2 and at most
   10 starts cold in the ordinary case**, depth 1 once the refs observation stands, none per untracked file, none for the tree's
   layout, and none on an open where a valid watch says nothing moved. Every other path has its own counted depth and ceiling.
   ReviewBase's range semantics are kept whole.
3. **Targets, provisional**, under the brief's load, medians: 16.2 s → ≤ 3 s; 44.1 s → ≤ 3.2 s; 8.4 s → ≤ 4 s; opened again with
   nothing moved ≤ 0.1 s; a live session's commit shown ≤ 3 s after its ref write (§3.4). The graph's depth and start counts are the
   gate; the times are a row's acceptance, measured on the bench through the real gate.
4. **No lock and no index refresh in a tree Daoris does not own**, as REVIEWLOCK1 measured and built it: `git --no-optional-locks`
   before the command, and `-c diff.autoRefreshIndex=false` on a diff against the working tree, its stat-only rows dropped (§1.1).
   This design points to REVIEWLOCK1 and widens its census to the reader's one launch seam; it designs no second rule.
5. **One `GitReader` per common git directory** (§4), one registry per process, every launch through one counting and classifying
   seam, its runs bounded apart from the gate's creation places, attended reads first and walks limited per reader and per process.
6. **Freshness is an identified, validated observation** (§5): the refs and the tree are read between two stamps and a watch
   generation, kept only where all agree and git's racy-time rule allows, validated again before every use, and named by the ids
   it read. **One head id answers both halves of a review**: the uncommitted diff is taken against that id and checked against
   status's own `branch.oid` afterwards.
7. **Who owns each git process, and how each write recovers**, is one table each (§4.4, §8.2). Ref removals and rewrites reuse the
   judged-delete transaction (`.claude/knowledge/ref-removal-transactions.md`) and its late-checkout recovery.
8. **No held git process for Review**; the screens' held `cat-file`, framed by length, owned and retired by its reader, four per
   process, ended by a repack sign, lands as its own row and only after the repack hazard is measured (§4.3).
9. **The person's git** (§7): the reads §7.1 lists, which is the capability boundary; the ref and remote acts; linear history edits
   from objects on an unpublished branch no tree holds; the working-tree acts in **the person's own checkout, behind a switch on its
   Setup, off by default**, under one checkout lock that the merge landing and a conversation's start also take.
10. **A session's tree is never acted on by the person through Daoris**; its work is reached by words until it lands.
11. **Nothing of Daoris's own written into `.git`**: no ref, configuration or file of its choosing, nothing cached on disk; objects
    where git's own commands write them; the named exceptions (a recovery ref, a renamed branch's section) said where they occur.
12. **D198's prerequisites, corrected** (§8.3): a person's push needs the installed doors, GITAUTH1f and a grant the press writes;
    a tag push waits for GITAUTH1g's transport judge; a terminal writing verb keeps the person's press through `PersonPresses`.
13. **Rows** (§12): REVIEWLOCK1 (built, lands first), then GITAPP1a-i/ii, 1b, 1c, 1d-i to 1d-iv, 1e, 1f, 1g-i to 1g-iv, 1h, 1i,
    each landing alone.
14. **Kept from round 1, as both reads keep it**: one reader per common directory, ReviewBase's semantics whole, the Accept offer
    and the plans read after the diff, session trees only their sessions own, the person's checkout behind a switch off by default.

## 1. What this design checked, and the two facts it accounts for

Each claim a choice below rests on was read against the code at `fe4809b75`; round 2 read again every claim either read questioned.

| Claim | By | Read | Verdict |
|---|---|---|---|
| Review's `status` and `diff` pass no `--no-optional-locks` | Fable F1 | `WorkingTree.cs:330,335`, `TreeWorkReads.cs:77` on `main`; `HelperCapture.cs:104`, `HelpCheckouts.cs:104` pass it | Holds on `main`; **REVIEWLOCK1 is built** on `daoris/s-e276dfc9` (24 sites, `tools/optional-locks.mjs` in `verify`, `ReviewLockTests`), not yet merged (Fable N15) |
| A worktree `diff` honours `--no-optional-locks` | round 1 §1.1 | REVIEWLOCK1's probe on git 2.53 (its fix-log entry) | **Wrong**: it refreshes and writes the index whatever the flag; only `status`, `diff --cached` and a two-object diff honour it (Fable B1) |
| The behind/ahead counts are unions over the line here and on origin | Codex X1 | `ReviewRanges.cs:125-126` | Holds |
| Review's starts today | brief, round 1 "13-19 + U" | `WorkingTree.cs:326-358`, `ReviewRanges.cs:42-126`, `TreesRoutes.cs:70-92` | Corrected (Codex 1): `12 + I + M + Uᵣ` before the offer, I the independence read where candidates differ, M the incomparable merge, Uᵣ the untracked files actually sent to git before the budget stops; the brief's eighteen-call probe was a replay, not a route trace |
| The offer costs "at least six and up to 21 more" | round 1 §3.2 | `TreeWorkReads.cs:40`, `TreeFollows.cs:31,32,64`, `ContentHolds.cs:163,166,177` | Six first, then a comparison per eligible branch: **not bounded** by 21 (Codex 1); counted and reported, never a ceiling, until 1c |
| A page hit costs nothing | round 1 §3.1 | `Web/src/bridge/sessions/trees.ts:168-184` | A fresh live answer and an ended answer read after its last record move start nothing; a stale live answer reruns the whole chain (Codex 1) |
| GITAUTH1c's identities store is unbuilt | brief :155 | `Platform/GitIdentities.cs`, `GitIdentityCommand.cs`, `GitAuthor.cs`; D198's GITAUTH1c note | **Partly built** (Codex 2); the doors' installation is still opt-in at publish (`tools/desktop-publish.mjs:1326-1340`), so a remote act names it as a prerequisite (§8.3) |
| The ref watch hears every ref, over the repositories holding work | brief §6 point 3 | `RefWatch.cs:229-242` hears `HEAD`, `packed-refs`, heads, remotes, reftable, trees' `HEAD`; `:249-265` selects registered **main checkouts with a `.git` folder** holding work; `:291` groups by name | Narrower (Codex 3): not tags, replace refs or the stash's log; a registered linked checkout is omitted; identity is the name, not the common directory. §5.4 widens it by common-directory identity |
| `ChangeWatch.MostRoots` is 32 | §5.1 | `ChangeWatch.cs:189`, **per instance** (`:201`, `:255`) | Holds per watch (Fable N4); §5.4 says which watch holds which roots |
| The gate bounds a run | round 1 §4.2 | `ProcessStarts.cs:29-31`: "Only the start is gated, never the run" | It bounds **creation** only (Codex 6); the reader's run bound is a second scheduler, §4.2 |
| `Person` is the class for a start a person waits on | Claude C2, Fable F4.3 | `ProcessStarts.cs:10-17,215,223`: a reserved place and a queue of its own | Holds; §4.2 keeps it |
| `WorkingTree.Started` counts every git start | round 1 §10.1 | `WorkingTree.cs:953,1020`: raised in `RunGitAsync` only; `ReadGitAsync` (`:1187`) and `GitBytesAsync` (`:1093`) start apart | **No** (Codex 28); §4.1's one seam counts all |
| `blame.ignoreRevsFile` is given on the blame call | round 1 §5.2 | `GitReads.Formats.cs:72-73`: quote-path and encoding only | **Wrong** (Codex 12); §5.3 freezes it |
| The page's commit and detail answers are kept for ever | — | `Web/src/bridge/repositories/git.ts:262` (`staleTime: Infinity`) | Holds; right for an id-keyed answer only when its context is in the key (§5.3) |
| Review's page joins one read per session | — | `trees.ts:81-108`; `queries.ts:146` | Holds; the key stays the session (§5.2) |
| An ended session's own tree is read for its dirty state | — | `TreesRoutes.cs:70` | Holds (Codex 15); an ended tree is mutable and watched alike (§6.3) |
| `session-branches.json` maps branches to sessions | round 1 §3.3 | `SessionBranches.cs:19-20`: `GrownBranch` holds no session | **Wrong** (Fable N5); the session records' tree folder through `TreeFollows.BranchOfTree`, with `GrownBranch.Now` (§5.4) |
| A conversation's start takes the tree lock | — | `ChatRunner.cs:439`, `ChatGoOn.cs:442`: only for an isolated tree | Holds (Codex 20); a checkout's conversation takes none today (§7.2) |
| The merge landing writes the checkout | — | `TreeLands.cs:162-168` ("the one place Daoris writes into a checkout it did not create") | Holds (Fable N6); §7.2's lock covers it |
| A person's push passes the door | round 1 §8 | `GitAccessRules.cs:40-46`: Write only with a grant or `no-push` off | Needs a grant (Fable N7, Codex 26); §8.3 |
| Tag push waits for GITAUTH1f | round 1 §8 | `docs/2026-10-22-git-auth-design.md` §6.1; `TASKS.md` GITAUTH1g | **GITAUTH1g**, the transport judge (Codex 26, Fable N7) |
| The terminal's writing verbs are the person's | round 1 §9 | `InstanceRoutes.cs:9-21,156-170`: `RouteClass.Person`, `PersonPresses`, `PipeCensusTests` | The existing seam (Codex 27); §8.3 |
| The repository's `commit-graph` carries changed-path filters | Fable F1 | its chunk table, `BIDX BDAT`, read without a process (Fable N15) | Holds, read |
| `DAORIS_SESSION_ID` is named by 13 sources | round 1 §1 | 33 files under `src/`, tests included (Fable N15) | Count corrected; immaterial |
| `merge-tree --write-tree` writes objects only | Codex X11 | `ReviewRanges.cs:92` | Objects only, and only under a fixed attribute source (§1.1, Codex 10) |
| LANDING waits for the tree; HANDOFF_PLAN does not | Codex X11 | `Web/src/work/review/DiffPane.tsx:148-163,188` | Holds |

### 1.1 Locks and configured programs (REVIEWLOCK1, pointed to)

**The rule is REVIEWLOCK1's, as it measured and built it** (its fix-log entry and `445720582`): every `status` and `diff` the driver
asks passes **`git --no-optional-locks <command>`**, the option **before** the subcommand (round 1 wrote it after, which git refuses;
Codex 9); and a `diff` against the working tree also passes **`-c diff.autoRefreshIndex=false`**, because git 2.53 refreshes and
writes the index there whatever `--no-optional-locks` says (`builtin/diff.c`'s quiet refresh does not consult it; Fable B1). Unrefreshed,
git lists a file whose stat data alone moved as `M` with no count and no patch, and `RangeSplit` drops it. One worktree diff still
refreshes: `BranchReplays`' conflict listing, inside a replay Daoris makes in its own tree. This design adds no second rule; it
**widens REVIEWLOCK1's census** to the reader: every launch goes through one seam (§4.1) whose read kind decides the flags, a unit test
over every kind asserts them, and `tools/optional-locks.mjs` additionally refuses a `diff` whose arguments are not two ids or
`--cached` without `diff.autoRefreshIndex=false` (Fable B1). REVIEWLOCK1 lands before GITAPP1a-i.

**The read-command effect policy** (Codex 10). A read Daoris runs may still execute a program the repository configures. Each read
kind says which, and the census holds it:

| Kind | Configured programs | Rule |
|---|---|---|
| Object reads (two-id `diff`, `log -p`, `show`, `blame`, `cat-file`) | external diff, textconv (whose cache can write `refs/notes/textconv/*`) | `--no-ext-diff --no-textconv` on every one; a textconv'd file shows as git shows it without the conversion (binary, D111), said |
| `merge-tree --write-tree` (REVIEWBASE3, the conflict preview, 1f) | a merge driver named by attributes | `git --attr-source=<empty tree>` so no driver applies; the merged base is a display base, said; a fixture whose attributes name a driver that writes a marker proves none runs |
| Worktree reads (`status`, `diff <id>` against the tree) | clean filters (LFS's), the fsmonitor hook or daemon the configuration names | run as for any `git status` in that tree: they are the tree's own configuration, and Daoris turns nothing on; said in the read kind's remarks |
| Name and layout reads (`rev-parse`, `worktree list`, `for-each-ref`, `config --get`) | none | no flag needed, and the census never classes them as index writers (Codex 10's converse) |

### 1.2 Process creation under the scanner

Fable's probe (F3) started programs that open no repository (`hostname.exe`, `cmd.exe /c exit`) and found them as slow as git
(median 1.2 s), with eight starts together finishing in the time of one or two; PROC1a separated `Process.Start` from the whole run
(`docs/FIX-LOG.md:5226`), and UIFREEZE1 found the runtime's one lock and a slow `CreateProcess` (`:3374`). **The reading, an inference
the stacks and probes support, not a proof** (Codex 4): each start waits on something serialised per creation, a real-time scanner's
verdict being the kind that overlaps across concurrent starts and not across a chain, and load multiplies it. The proposals' probes
ran on different loads. What follows does not depend on the attribution being whole: fewer starts and fewer dependent steps are
cheaper under any cause.

**Accounted for**:

- **Start fewer.** The layout and refs come from disk and one observation, every answer named by ids is kept, and an untracked file
  costs no start (§3, §6.2).
- **Depend less.** A dependency graph, not a chain (§3). The gate's width stays four: creation is serialised under .NET's one lock
  (`ProcessStarts`' remarks), and PROC1a measured no gain past four.
- **Do not start for object questions**, where a screen asks hundreds: the held `cat-file` of §4.3. Not for Review, which asks none.
- **Measure the parts apart.** The `review.read` line and the bench carry, per start, the gate wait, the creation time and the
  child's run, never one number (Codex 4). An exclusion for git in the scanner is the person's call; Daoris changes nothing of it.
- **The targets are provisional, set under this load** (§3.4).

## 2. How the three compare, in one table

| Question | Claude | Codex | Fable | Chosen |
|---|---|---|---|---|
| Review's starts cold | ≤ 4 at once | ≤ 10, counted per path | ≤ 10, three rounds | **A held dependency graph**: ordinary depth 2, ≤ 10 starts; each path its own depth and ceiling (§3) |
| Review's targets | ≤ 1.5 / 1.5 / 2.5 s | ≤ 4 / 4.5 / 5 s median, p95 twice | ≤ 3 / 3.2 / 4 s median | **Fable's, provisional**, p90 twice; never claimed from arithmetic (§3.4) |
| Range semantics | merge-bases keyed by ids | union counts, candidate order, REVIEWBASE3 kept | `ahead-behind` for both counts | **Codex's**: kept whole |
| Untracked files | from disk, byte-equal to git | from disk, display data | from disk, byte-equal to git | **Codex's display addition**: from disk, exact for a stated subset, never a staging patch (§6.2) |
| The offer and the plans | off the critical path | off; HANDOFF_PLAN not eager | the offer over the snapshot's tips | **Off the critical path** (Claude, Codex) |
| Layout and refs | snapshot by generation | status + refs, two starts | from disk, stamped | **From disk, an identified observation**: stamps before and after, a watch generation, content where small (§5.1) |
| Optional locks | not seen | `status` "disabled optional writes" | found the defect | **REVIEWLOCK1, as built** |
| The reader | `GitRepository` per common dir | one coordinator in the host | `GitReader` per common dir | **`GitReader` per common dir** |
| A held process | `--batch-check`, 2 min idle, ≤ 8 | lazy, ≤ 4, retired idle | screens only, 30 s idle, hazard measured | **Fable's, four per process, framed and owned** (§4.3) |
| Gate class | Person | the reserved place kept | Background | **Background**, attended first inside the reader (§4.2) |
| Working-tree watch | sessions' trees and the checkout while open | attended trees | open reviews only | **Every attended mutable tree**, live or ended, and the checkout while its Changes tab is open (§6.3) |
| fsmonitor, untracked cache | never | never | a press | **Never; the command to copy** |
| `commit-graph` | a press, after measuring | never | a press | **Never; the command to copy** |
| Working-tree acts | person's checkout, behind a switch | a manual tree of the person's | an idle session's tree | **The person's checkout, behind a switch** (§7.2) |
| Ref-only history edits | cherry-pick, revert, rebase by objects | in the manual tree | in the idle tree | **By objects, linear and unpublished, on a branch no tree holds** (§7.3) |
| Conflict resolution | three-way view writing a chosen result | whole side or external tool | resolved on the page | **A side per hunk, or the person's editor** (Codex, D55) |
| Agents | nothing new | common reads and their own tree | nothing new | **Nothing new** |
| File history pages | continue from the last id | cache prefixes, prove a cursor | keep the id list | **Keep the id list with each commit's path** (Fable), in 1h |

## 3. Review fast (Q1)

### 3.1 The graph

Review's reads are a **graph built as data** (`ReviewGraph`, in `Driver/Trees/GitReads/`): each node a read with its kind, the ids
or tree it is keyed by, and the nodes whose answers it needs. An executor starts each node the moment its inputs have answered and
never before, through the reader (§4). The chain changes when each question is asked, never which: ReviewBase's candidate order, the
grown base (WSR6), the replayed bases and REVIEWBASE3's merged base are kept exactly.

With **no process**:

- **The layout**, from disk: the tree's `.git` (a folder, or a file naming `gitdir:`), `<gitdir>/commondir`. A folder is a top
  exactly when it holds its own `.git` (D113 §1's walk-up hazard). Reftable, an unreadable or unknown layout is **one counted
  `rev-parse`**, and where that fails nothing is kept and the answer says so (fail closed, Codex 11).
- **The branch name** (step E): the tree's `HEAD` file.
- **The line**: `LineGuesses`, as today.

Then the nodes, `h` being the head id the refs observation resolved for the tree's `HEAD`:

| Node | Read | Kind | Needs | Starts |
|---|---|---|---|---|
| **R** | the refs observation (§5.1): one `for-each-ref`, between two stamps | names | nothing; **skipped** where a kept observation validates | 0-1 |
| **S** | `git --no-optional-locks status --porcelain=v2 --branch -z --untracked-files=all` | worktree | nothing | 1 |
| **A** | untracked files' attributes: one `check-attr --stdin -z` over all of them (§6.2) | worktree | S, where S lists any | 0-1 |
| **U** | the untracked display additions, read from disk | no process | S, A | 0 |
| **W** | `git --no-optional-locks -c diff.autoRefreshIndex=false diff … h` (the uncommitted diff, **pinned to `h`**) | worktree | R | 1 |
| **B₁, B₂** | `merge-base h <line>`, `merge-base h <origin-line>` | object | R | 0-2 |
| **K** | `merge-base --is-ancestor <recorded> h` | object | R | 0-1 |
| **N** | `rev-list --count <line-refs…> --not h` and its converse (REVIEWBASE1's unions) | object | R | 0-2 |
| **D₀** | the committed diff `<recorded>..h`, speculative (Fable N2b) | object | R, where the recorded base is an id | 0-1 |
| **I** | `merge-base --independent <candidates>` | object | B₁, B₂, K, where two or more distinct candidates | 0-1 |
| **M** | `git --attr-source=<empty tree> merge-tree --write-tree <line-base> <recorded-base>` (REVIEWBASE3) | object | I, where incomparable | 0-1 |
| **D** | the committed diff `<from>..h` (REVIEW3's one-call reader, its caps unchanged) | object | B₁, B₂, K (and I, M where they ran); **skipped where `from` is the recorded base** and D₀ answered | 0-1 |
| **V** | validation: S's `branch.oid` equals `h`; the observation still validates (§5.1) | no process | S, W, D or D₀ | 0 |

**What each path costs, cold** (the census holds each as numbers computed from the graph, §10.1):

| Path | Depth | Starts | Notes |
|---|---|---|---|
| Ordinary: own tree, base recorded and an ancestor, the line configured, both line refs readable, one distinct candidate | **2** | **≤ 9**, **≤ 10** with untracked files | R, then everything else together; D₀ is the answer |
| The refs observation already valid | **1** | ≤ 8, ≤ 9 with untracked | R skipped |
| Two or more distinct candidates | 4 | ≤ 11, ≤ 12 with untracked | D₀ was a wasted start, counted; I, then D |
| Incomparable bases (REVIEWBASE3) | 5 | ≤ 12, ≤ 13 with untracked | I, M, then D |
| No line configured | 2 | ≤ 5, ≤ 6 with untracked | S, W, K, D₀ |
| A displaced or missing recorded base | 3 | ≤ 9, ≤ 10 with untracked | no K, no D₀; D after B₁, B₂ |
| A landed session whose tree is gone (`LandedReviews`, through the reader in 1a-i) | 2 | ≤ 6 | R, `--is-ancestor`, `LandedFromAsync`'s one to three, the diff; 7 today (Codex 1) |
| An older git without an option a node uses | its path's + 1 | counted | the fallback is a node of its own, never an unseen retry |

**Warm.** Every object node is keyed by ids (§5.2), so a second open reads none of them while the ids stand. A **landed or gone**
tree: 0 starts. A **live or ended own tree**: 0 starts while its watch is valid (§6.3); otherwise the validation read **S + W**, 2
starts, depth 1. **After a commit**: R, S, W, and the object nodes for the new `h` (their keys are new), depth 2.

**Round 1's three rounds are replaced** (Codex 5, 28; Fable N2, N3). Round 1 resolved the snapshot before its "round 1", ran
`merge-tree` in the same round as the `--independent` that decides it, and put the union counts after `from` though they need only
the pinned tips; its "three rounds" was a scheduling label, not the depth. Depth is now the longest chain of dependent starts on a
path, a property of the graph's data, and a test holds it.

**Handing a level to the reader** is longest first (Fable N2a): S, W and D₀ before the merge-bases and counts, so the slowest reads
are never in the gate's second wave.

### 3.2 What leaves the critical path

`SESSION_DIFF` answers the review: its patch, its range, its counts and the observation it was read at. **The Accept offer**
(`TreeWorkReads`, `TreeFollows` and the content holds: six starts, then a comparison per eligible branch, not bounded today) becomes
its own read, asked by the foot once the diff has answered, through the same reader. **HANDOFF_PLAN**, asked unconditionally today,
is asked after the diff too; `LANDING` already waits for the tree. The foot says *Reading…* while its reads run, and their starts and
time are reported apart in `review.read`, since a fast patch is not a finished plan (Codex X1). Bounding the offer's count is 1c's.

### 3.3 A live session moving

- **Nothing moved in git**: the page no longer re-reads on every record `updated` (`Web/src/bridge/sessions/trees.ts:182-184`); in
  1a-ii the page keeps its 15 s freshness for the validation read; in 1b the tree watch keeps it at 0.
- **A commit or a switch in the tree**: the tree's `HEAD` and its branch's ref move; the reader's watch hears them (§5.4), drops the
  observation, and publishes the review's scope for the sessions whose tree that is. Pre-read and the page's refetch join the same
  in-flight read by its key (§4.1), so they cannot publish two answers.
- **The line moved** (a fetch, D109, a landing): every review in that repository depends on it (§5.4), not only those on the moved
  branch (Codex 16).
- **Claude's pushed slice per open session** (C2) is not taken: the scope and the id-keyed cache give the same effect without a new
  slice contract; it is reconsidered if 1b's measurement shows the refetch's round trip matters.

### 3.4 The targets, provisional

**Provisional acceptance targets**, held on the bench (§10.3) and on the installed window (GITAPP1i) under the brief's load, named
beside every number; median over at least ten runs with p90 beside; a ceiling fails a row's acceptance, never a gate (D54's rule for
budgets). **They are not derived**: round 1 called them the arithmetic of three rounds, and Codex 6 showed a permitted trace past five
seconds. What the graph does say is a floor: depth × one start's wait and creation, plus the longest read on the path. The bench
measures the real graph through the shared gate; where a target is missed, the row says which node and which part (wait, creation,
run), and the target is revisited with that evidence rather than the code bent to it.

| Case | Today | First open, median / p90 | Depth, starts | Opened again, nothing moved | A live session's commit |
|---|---|---|---|---|---|
| 368 files committed, nothing untracked | 16.2 s | **≤ 3 s / ≤ 6 s** | 2, ≤ 9 | watch valid: ≤ 0.1 s, 0 starts; else ≤ 1.5 s, 2 starts. Landed: ≤ 0.1 s, 0 | shown ≤ 3 s after the ref write (1b) |
| 368 files, 50 untracked | 44.1 s | **≤ 3.2 s / ≤ 6.4 s** | 2, ≤ 10; 0 per untracked file | as above | as above |
| 1,771 files committed | 8.4 s | **≤ 4 s / ≤ 8 s** | 2, ≤ 9 | as above | as above |

The 1,771-file case is **a complete file list with bounded patches**, never the whole 16.6 MB patch (`WorkingTree.cs:271,274,451`;
Codex 6). The page's paint of 368 rows is measured, not assumed: 1a-ii writes `review.painted`, and collapses rows past the first
forty only if the paint is over 0.5 s.

## 4. The reader (Q2)

### 4.1 One per common directory, one seam beneath every launch

**`GitReader`** (new, `Driver/Trees/GitReads/`), one per common git directory, identified by that directory's full path (case-folded
on Windows), so a linked tree shares its repository's; handed out by **`GitReaders`**, one registry per process: the window's loop
holds one and every route and judge in that process reads through it; `daoris-driver` holds one for a verb's life; a test takes its
own. It holds the layout, the refs observation (§5.1), the one shared `GitReadCache` (64 MiB, LRU, memory only), in-flight joining
and its run bound.

**One launch seam** (Codex 28). Every git the reader starts, streamed (`ReadGitAsync`), as bytes (`GitBytesAsync`) or whole
(`RunGitAsync`), goes through `GitReader.Launch(kind, arguments)`, which classifies the read (§1.1), applies its flags, raises the one
counting event (today's `WorkingTree.Started` moves beneath all three runners, so a test counts every start), and passes
`WorkingTree.GitStart` as today: Tools' git, `GitWiring.Hand`, `GitBounds`, the gate. `ProcessStartsCensusTests` names no new direct
start; a source census refuses git started under `Trees/GitReads/` except through `Launch`.

**Joining** is by the read's key, which carries its ids and its context (§5.3), never a name. A joined read has one owner job and
many waiters; a waiter's cancellation drops that waiter alone, and the job is cancelled only when its last waiter has gone (Codex 14).
A completion is kept only if the observation and context it was read under still validate (§5.1); otherwise it answers its waiters,
named by its ids, and is not kept.

`GitFacts` and `RefSnapshot` keep their flow-scoped meaning ("asked once within this flow, dropped by this flow's writes"),
consulting the reader first for layout and ids.

**One, not per area** (Fable F4.1): Review, the landing's proof and the commit page ask the same ids. **Not Codex's one coordinator in
the host** (X2) beyond this: the reader is per repository because its observation is, and the registry is the coordinator.

### 4.2 Two schedulers, said apart

The **gate** (`ProcessStarts`) bounds **creation**: four places, three the background's, one reserved for a person's start; a place
is held from asking to `Process.Start` returning, never for the run (`ProcessStarts.cs:29-31`). It is unchanged, and the reader's
starts stay **`StartClass.Background`**, so a conversation's agent the person just asked for never queues behind git (round 1 §4.4,
kept by both reads).

The **reader's run places** bound **whole reads**: a place is held from the read's launch until its process has exited and its
output is consumed or dropped (Codex 6). Per reader:

- **Eight run places** (provisional, measured in 1a-i): enough for the ordinary path's one level of eight.
- **Attended first**: a read a person is waiting on (an open review, a screen in view, a pressed act's plan) is handed a free place
  before an unattended one (a pre-read, a judgement's sweep); a waiting attended read never queues behind unattended ones in the same
  reader (Codex 7).
- **Long reads** (a pickaxe, a whole-history walk, a file's history over all commits) hold **at most one place per reader and two
  per process**, so a search never starves a short attended read of the same repository, and several repositories' searches never
  fill the gate's background places together (Codex 7).

Across readers, the gate's background queue stays first-come. `review.read`'s `waitedMs` per start measures whether a pressed review
waits there behind another repository's work; if it does, a later row gives attended reads a priority **inside** the background class,
never the person's reserved place (Fable F4.3). That part of Codex 7 is answered by measurement, not by changing the gate now.

### 4.3 The held object reader (the screens only, GITAPP1d-iv)

**Not for Review**: nothing Review asks is an object question. **For the screens**: `cat-file --batch-command` (`--batch` below git
2.36), launched through the seam, answering object questions only (a commit's fields and parents, a tree's entries, a blob for a
preview, an object's existence), never a walk.

**Protocol** (Codex 18). Requests are written one per line (`info <oid>` first; `contents <oid>` only where `info`'s size is within the
screen's bound); answers are read **as bytes**: the header `<oid> <type> <size>\n` or `<oid> missing\n`, then exactly `size` bytes and
one `\n`. A header naming another object, a short read, EOF or a malformed header **retires** the process (killed and waited for); the
next question starts a new one. stderr is drained continuously into a bounded buffer. Each question has its own deadline (`GitBounds`'
object bound); a question cancelled before its answer starts is withdrawn; one cancelled **mid-answer** retires the process, since
skipping an abandoned body cannot be told from reading the next. Questions are serialised by one owner task; a waiter is a
continuation, never a reader of the stream.

**Lifetime and ownership.** The reader owns it: started on the first question, ended **30 s after its last** (Fable F4.2), on the
reader's context or observation changing (a replace ref, a new git), and on the registry's disposal at window exit or the verb's
end, which kills and waits for each. **At most four per process** (Fable N1: a registry is per process, so "across the machine"
could not be held); one per reader.

**The repack hazard** (Fable N1; Codex 19). Every linked tree shares the object store, so an agent's own `commit` or `fetch` in its
tree can run `gc --auto` there, and on Windows a pack a process holds mapped cannot be unlinked: the person's `git gc` prompts "Unlink of
file … failed", a detached auto gc warns and leaves the pack. So the held reader **never starts while `<commondir>/gc.pid` exists and
retires when it appears or the `objects/pack/` listing changes**; 1d-iv measures it beside the person's `git gc` and an auto gc fired
from a linked fixture tree (`git -c gc.auto=1 commit`). If the hazard stands with those rules, the screens live without it and pay
one start a burst. Its integration into 1d's screens waits for that proof (Codex 29).

### 4.4 Who owns each git process

| Process | Owner | Started | Ended | On cancel | On window exit or verb end |
|---|---|---|---|---|---|
| A one-shot read (any node) | its job in the reader, holding a run place | at `Launch` | exit, its output consumed | the job's token, cancelled only when its last waiter goes: `Ending` kills the tree, stderr drained, waited for | the registry's token: killed and waited |
| A long read (pickaxe, walk) | the reader's long place | at `Launch` | exit, or the page closing | streamed results stop; killed and waited; nothing kept | killed and waited |
| The held `cat-file` | the reader | first object question | 30 s idle, a repack sign, a context change, a protocol fault | per question (§4.3) | killed and waited |
| An act's git (`update-ref`, `commit-tree`, `apply`, `commit`, `stash`, `switch`, `fetch`, `push`) | the act's transaction (§8.2) | after the act's re-judgement under its lock | exit | **before its effect step only**; once an effect step starts it runs to exit and its recovery runs | waited for, never killed mid-effect; the window's close waits for a running act, saying so |
| `daoris-driver git …` | the verb's registry | per the rows above | the verb's end | Ctrl-C cancels reads; an act already in its effect step finishes | as the window |

### 4.5 In which process

The driver library, used by the modules in the window's process, by `daoris-driver` (D50) and by the tests. No git-serving process
of Daoris's own. The service's one `ls-files` for ignored paths (`IgnoredPaths`, `Daoris.Service.Core/Registry/IgnoredPaths.cs`) is
the service's and untouched; the driver's watches compute their own (§6.3, Fable N13).

## 5. Freshness, caching and invalidation (Q3)

### 5.1 The refs observation: identified, validated, never atomic

An **observation** is what the reader read of a repository's refs, with what identifies it: the common directory's identity, the
watch generation it was read under, the stamps taken before and after, and the ids it resolved. It is **an identified observation, not
an atomic view of external git** (Codex 11): git can move between any two reads, and the rules below make that seen, never hidden.

- **What is stamped** (each file's write time and size, read with no process): `packed-refs`; the common `HEAD`; each linked tree's
  `HEAD` and the **`worktrees/` folder's listing** (membership, Codex 11); each tree's `commondir` file; `logs/refs/stash` (a dropped
  older stash need not move the tip, Codex 11); and the loose ref files under `refs/heads`, `refs/remotes`, `refs/tags` and
  `refs/stash`, **bounded** (Fable N12): past 256 loose files, the ref folders' own write times stand in for their files, which a
  rename into a folder changes on NTFS and ReFS, and the answer says the stamp was bounded.
- **What is read whole** (content validation, Codex 11), because it is small and decides the answer: each tree's `HEAD` file, and the
  loose files of the refs a review rests on (the tree's branch, the line here and on origin), 41 bytes each. `packed-refs` is hashed
  where it is at most 1 MiB; past that, its stamp and the watch generation guard it, said.
- **Order** (Fable B2): stamp and generation are taken **before** `for-each-ref` starts and again **after** it answers; the observation
  is kept only where both agree, as `LineGuesses.GuessAsync` already does (`LineGuesses.cs:33-37`). Otherwise it answers this read,
  named by its ids, and the next use reads afresh.
- **Racy time**: a stamped file whose write time is within two seconds of the stamp's own reading does not let the observation be kept
  (git's racy-index rule, applied to refs: same length, same coarse time is the case a stamp cannot tell). On a filesystem whose times
  are coarse (exFAT, a network share), nothing is kept.
- **Before every use**, the stamps, the whole-read contents and the generation are read again; any difference reads afresh.
- **Reftable** has no stamp: read on every use, one counted start, said. **An unread layout** fails closed (§3.1).

The ids the observation resolves are what is kept and what keys answers: **a ref is never a key** (D147 §4.2).

### 5.2 One head id for both halves of a review

The review's `h` is the observation's id for the tree's `HEAD`. **W** diffs the working tree against `h`, not the name `HEAD`
(identical semantics for a worktree diff), and **S** runs with `--branch`, whose `# branch.oid` names the `HEAD` git read (Fable B3,
Codex 14). **V** checks, after S, W and the committed diff answered, that S's `branch.oid` equals `h` and the observation still
validates. Where they differ, the reader reads the graph once more (only the nodes whose keys moved), and where the second read moves
again it answers with what it read, flagged **changed during read** with both ids, never a mixture shown as one state. Retries are
bounded at one.

**The page's key stays the session** (Codex 14): dirty files and the line's tips change without `h` changing (`queries.ts:146`). The
answer carries its observation (`h`, `from`, the line's tips, the tree's generation), the page draws *Read at … · HEAD …* from it, and
the driver's immutable patch cache is keyed by ids and context. A mutable observation of a session (its tree, its line) and an
immutable answer keyed by ids are kept apart and never joined by the same key.

### 5.3 What is kept, under what key and context

| Answer | Key | Kept |
|---|---|---|
| A range's files and patches (Review's committed part, a commit, a compare, a landed branch) | common dir, view, from, to, options, parser version, context id | until evicted |
| `merge-base`, `--is-ancestor` (its "no" is an answer), `--independent`, `merge-tree --write-tree`, `rev-list --count` | common dir, command, ids, context id | until evicted; a generated tree found missing is a miss (below) |
| A graph page, a file's history, a blame, a commit's fields (GIT1b) | as today, with options and context id | until evicted |
| A file's history's id list (1h) | common dir, commit, path, context id | until evicted |
| A search by message, author or path | common dir, tips, query, context id | until evicted |
| The layout | the folder | until its `.git` stamp changes, by §5.1's order |
| The refs observation | the common dir | until §5.1 says otherwise |
| **Never** | `status`, a worktree diff, anything the working tree decides, outside §6.3's watched answer; a name alone; a read git did not answer; a pickaxe | |

**Context per read kind** (Codex 12). Every option that changes an answer is **frozen on the call** where git lets it be:
`--no-ext-diff --no-textconv`, `--no-color`, prefixes, `-c core.quotePath=false`, the rename detection and limit
(`-M`, `-c diff.renameLimit=…`), the diff algorithm (`-c diff.algorithm=myers`), `--no-mailmap` (identities shown as written; a
mailmap is a later display choice), `-c blame.ignoreRevsFile=` (round 1 claimed it was given; it is not, `GitReads.Formats.cs:73`), and
**`git --attr-source=<to>`** on object reads, so attributes come from the commit read rather than from whichever linked tree asked
(git 2.40 and later; older, the read is not kept). What cannot be frozen forms the **context id**: Tools' git path and version, and
the stamps of `info/attributes`, the repository's, global and system configuration files and their includes. For a read that depends
on a working tree (worktree configuration under `extensions.worktreeConfig`), the tree's identity joins the key. Where
`refs/replace/`, `info/grafts` or `shallow` exist, nothing of that repository is kept.

The context id is **validated before lookup, before joining and at completion**, and it travels: every answer names it, the page's
id-keyed queries (`Web/src/bridge/repositories/git.ts:262`, kept for ever) carry it in their keys, and a context change publishes the
repository's `git` scope so a kept page answer under the old context is asked again (Codex 12). A fixture of two linked trees at the
same ids with different attributes holds it.

**Generated objects** (Codex 19, Fable N10). A `merge-tree` answer is a tree id no ref holds, which the person's `gc` may prune after
two weeks. A read that finds a kept generated object missing is a **miss**: the entry is dropped and the object computed again, never
an answer of absence and never "git could not read". 1d-iv's repack proof exercises it.

**Partial clones** (Codex 13). A local read must never fetch. Every read passes `GIT_NO_LAZY_FETCH=1`, honoured from git 2.44 (Tools'
git here is 2.53). Where the repository is a partial clone (`extensions.partialClone` or a promisor remote) and the git is older,
object-dependent reads are **refused before they start**, said. A fixture holds it: a partial clone of a local bare promisor that records
every `upload-pack`, a read of a missing blob refused, and the promisor asked nothing. Fetching missing objects is a remote act, through
D198's doors.

### 5.4 What a change invalidates, and who hears it

**Dependencies** (Codex 16): a review depends on its common directory's observation, **its tree's actual `HEAD`** (the file, not the
folder's name), the branch that names, the configured line and the line's tips here and on origin, and its recorded base. A tree whose
agent switched or detached is followed by its `HEAD` file (TREEFOLLOW1, D164); the session ↔ tree ↔ branch map is the session records'
tree folder through `TreeFollows.BranchOfTree`, with `GrownBranch.Now` for a tree its agent moved (Fable N5), never
`session-branches.json`, which names no session.

| Change | Invalidates | Published |
|---|---|---|
| A tree's `HEAD` or its branch's ref moves | that tree's review | `diff` for its session |
| The line here or on origin moves | **every** review in the repository, and its branch list | `diff` for each session there; `lines` |
| Any head, tag or remote ref moves | the branch list, holding-ref decorations, the graph's tips | the repository's `git` scope |
| `logs/refs/stash` moves | the stash list | `git` |
| `worktrees/` changes | the worktree list, and the occupancy every act re-reads | `git` |
| The context id changes | every kept answer of the repository | `git`, `diff` |
| A watch's loss, overflow, error, cap or the machine's resume | everything that watch guarded | all of the above for its repositories (missed-event reconciliation), and the next use reads afresh |

The state hub carries invalidation keys, not ids (`Web/src/bridge/state/stateHub.ts:31`); the page's next read asks the reader,
which joins any read already in flight for the same key (§4.1).

**Which watch** (Fable N4, Codex 3). The ref watch's current set (repositories holding Daoris's work) keeps its consumers
(`LandingWakes.Moved`, `HelpFacts.Woken`; `DriverWatch.cs:432-438`) unchanged. The reader's roots are a **second `ChangeWatch`
instance**, its hint only dropping observations and publishing scopes: every registered repository with a checkout, **main or linked,
resolved to its common directory and identified by it**, those holding work first, within its own 32; past the cap the stamp alone
guards, said. It hears `refs/tags/**`, `logs/refs/stash` and `worktrees/*` beside what `RefWatch.Hears` hears today. The tree watches
of §6.3 are a **third instance**, capped at eight attended trees; past it, an open review pays the validation read.

## 6. Working-tree state (Q4)

### 6.1 Read, never written

Two reads, **S** and **W** (§3.1), both under REVIEWLOCK1's rule; status is kept apart from the patch, since staged and unstaged
changes that cancel give an empty patch while the tree is not clean (Codex X4). Unknown is never clean. **fsmonitor and the untracked
cache are not turned on, and no press offers them**: they write the repository's configuration and start a daemon the person did not
ask for. Where the person turns them on, Daoris's `status` is faster with them. Where `review.read` shows a slow status, Review's head
(and the Changes tab's, 1g-i) names the command (`git config core.fsmonitor true`) to copy.

### 6.2 Untracked files: a display addition from disk

An untracked file is shown as **a display addition** (Codex 17, taking Codex X4's contract): its path, its lines as added, and its
facts, read from disk within bounds. It is **never a staging patch**: no act applies it (§7.2's acts compose their patches from git's
own comparisons). It is **exact**, equal to git's text for the file, for a stated subset; outside it, the row says what it is instead:

| The file | Shown |
|---|---|
| **The exact subset**: a regular file, no attribute among `diff`, `text`, `eol`, `filter`, `binary` set, no NUL in the first 8,000 bytes, valid UTF-8 or ASCII, LF or CRLF as they are | its lines, equal to git's patch text for it |
| Empty | *empty file*, as git prints no hunk |
| A NUL in the first 8,000 bytes, or `binary`/`-diff` set | *binary* (D111), its size |
| Not valid UTF-8 | its lines decoded as `ReadGitAsync` decodes git's output (U+FFFD for an invalid sequence), marked *not UTF-8* (Fable N16) |
| An attribute naming a filter or `eol`/`text` conversion | its size and *shown as stored, not as git would convert it* |
| A link or reparse point | *a link*, its target never read or followed |
| A path `status` lists with a trailing `/` (a nested repository, a folder) | the folder, never read |
| Changing during the read (size or time moved) | read once more, then *This file changed while it was read* |
| Over 200 KiB (`UntrackedPatchLimit`) or past the aggregate budget | listed, not read |

**Attributes cost one start for all the files, none per file**: node A, `git check-attr --stdin -z diff text eol filter binary`
over every untracked path at once. **The bounds stop reading, not only the string** (Codex 17): a file is read at most up to its
per-file limit and what remains of `PatchBudget`, so a large file allocates no more than the budget allows. Git on Windows writes mode
`100644` for these files (`core.fileMode` false); an executable bit is not shown.

**The proof** (§10.2) compares the exact subset with today's oracle as it is written (`WorkingTree.cs:386-387`:
`-c core.quotePath=false diff --no-index --no-color --no-ext-diff --src-prefix=a/ --dst-prefix=b/ -- /dev/null <path>`), at the
decoded text both sides go through, and shows each row of the table above for its fixture.

### 6.3 The tree watch

1b keeps the uncommitted answer (S, W, U) under a `ChangeWatch` root on the tree's folder **for every attended mutable tree**: an own
session tree with Review open, **live or ended** (an ended tree can still hold and gain uncommitted files, `TreesRoutes.cs:70`;
Codex 15), and the person's checkout while its Changes tab is open (1g-i). Released when the view closes or after fifteen idle minutes.

- **Installed before the answer is taken**: the root is watching before S and W start, and a hint during them drops what they read.
- **Exclusions are proved, not assumed** (Codex 15, Fable N13): `.git`, and an ignored folder only where git tracks nothing inside
  it. The driver computes its own list once per root (one counted `ls-files --others --ignored --directory --exclude-standard`, and one
  `ls-files -z -- <those folders>` that must be empty), never the service's `IgnoredPaths`, which is another process. A change to a
  `.gitignore`, `info/exclude` or the configuration rebuilds the list.
- **Stamped also** by the tree's `index` file, by §5.1's order; an index stamp alone never proves the files unchanged (an unstaged edit
  moves no index), only the watch does.
- **0 starts on reopen only while the watch is valid**: it was installed before the kept answer, and has seen no loss, overflow, cap,
  release or resume since. Otherwise the reopen pays the counted validation read (S + W), said in `review.read`.

## 7. What the person gets (Q5)

### 7.1 The reads, and the capability boundary

**This table is the capability boundary** (Codex 8): what the person gets is what it lists, not "every read a git management app
offers". Rows name where each lives (D150 point 3: Repositories → a repository's tabs; a session's Review) and which row builds it.

| Capability | Per repository | Per session tree | Where | Row |
|---|---|---|---|---|
| A commit | message, author, committer, parents as doors, refs holding it, who made it (D143), its changes in `PatchView` | the same | a commit's page | 1d-i |
| Diff views | inline (built), side by side, word diff, whitespace ignored (a re-read with `-w`, keyed by it); a binary says so (D111) | the same in Review | Review, a commit's page | 1d-i |
| LFS | a pointer drawn as *a file kept by LFS* with its size, never its pointer text | the same | Review, a commit's page | 1d-i |
| History graph | built; filters by branch, author, path, since and until (one `log`, keyed by tips and filter) | the branch beside its line | History tab; Review's *Show in Git* | 1d-ii |
| Blame | a gutter, runs grouped, *Before this change* | at the tree's `h` | a file row's *Blame* | 1d-ii |
| A file's history | following renames; pages as GIT1b reads them today | the same | a file row's *History* | 1d-ii; its kept id list 1h |
| Compare | two refs resolved once; `A...B` and `A..B` | the tree's branch against its line | a branch's *Compare with…* | 1d-ii |
| Search | messages and authors (one `log`, kept); paths at a commit (`ls-tree`); content at a commit (`git grep`) | from the tree's `h` | History tab's search | 1d-ii |
| Stashes, tags, remotes, worktrees, a branch's reflog | `stash list` and a stash's changes by its id; tags on the graph and a commit; `remote -v` through `GitRemotes`; worktrees with the session holding each; `reflog show <branch>`, never kept | — | Branches tab; a branch's page | 1d-iii |
| Conflict preview | — | `merge-tree --write-tree` of `h` onto the line: would it conflict, in which files and hunks (objects only) | Review's foot, beside Accept | 1d-iii |
| Content across history | `-S`/`-G` as a long read: path-scoped, streamed, cancellable, never kept | the same | History tab's search | 1h |
| Submodules | listed from `.gitmodules` and `160000` entries, each a door to its own repository where registered | the same | a commit's files | 1h |

**Deferred, said rather than implied** (Codex 8): an image diff; **interactive history editing** (reorder, squash, reword, drop: a
different act from §7.3's object rebase, which replays a linear range unchanged); a free three-way editor (D55); a submodule's own
writes; LFS content fetched or shown; staging in a session's tree; signing a commit Daoris writes. Each is a later row if asked.

### 7.2 Where the working-tree acts live, and the checkout lock

The three proposals answered differently. **Chosen: the person's checkout, behind the switch** *Daoris may change this checkout's
files* (允许 Daoris 修改此检出中的文件), off by default, on the repository's Setup under **Reach** (边界, D150 point 3: the section about
what writes into the repository; Fable N11), its state said on every act's plan. A git management app works where the person's
changes are, beside their editor, and the switch is the person's say-so, made once and visible. **Codex's manual tree is rejected**:
the person's edits are in their checkout, so a separate tree has nothing of theirs to stage. **Fable's idle session tree is
rejected**: the session's work is the session's until it lands (D51, D164), and its agent would find commits its transcript and trace
do not explain. Both reads keep this.

**The checkout lock** (Codex 20, Fable N6). One `CheckoutLock` file per registered checkout, under the home's `locks/`, by
`TreeLock`'s pattern (`Driver/Trees/TreeLock.cs`: the file system's sharing, every Daoris process honouring it, a dead process
letting go with its handles, never waited for). Taken:

- **alone** by every Changes-tab act, for its re-judgement and its effects, and by `TreeLands.MergeAsync`, the merge landing that
  writes the checkout (`TreeLands.cs:162-168`), around its clean-and-on-the-line checks and its merge;
- **shared** by a conversation starting in the checkout (`ChatRunner`, `ChatGoOn`), from choosing the checkout until its record is
  open, as `TreeLock` holds a tree's start today; the open record then names it.

**Under the lock, immediately before any effect**, an act re-reads: the switch; the checkout's identity (its top, its common
directory, that it is the registered checkout and not a session tree, which is refused **independently of the switch**); the ledger's
running or waiting conversations naming it; an operation in progress (`MERGE_HEAD`, `CHERRY_PICK_HEAD`, `REVERT_HEAD`, `rebase-merge/`,
`rebase-apply/`, `BISECT_LOG`); `HEAD` and the index entries the act touches; and the bytes the plan showed. Any difference refuses,
naming it. A landing that finds the lock held is held for this look, as a start that finds a replay is.

**Refused in the checkout**, with the reason: a hard reset, `clean`, a force anything (they lose work with no recovery); a recursive
submodule write; a merge or rebase of the checkout's branch (D109's and the landing's door); any act while a conversation runs there
or a landing holds it.

**The Changes tab reads three observations** (Codex 25), each by REVIEWLOCK1's rule: **HEAD → index** (`diff --cached h`), **index →
worktree** (`diff`, unrefreshed, stat-only rows dropped), and status's untracked and conflicted entries (`u` lines of
`--porcelain=v2`), with each index entry's blob id from status. A file with staged additions and matching unstaged reversals appears in
both comparisons, with both controls. A stage press applies a patch Daoris composes from the index → worktree hunk or line, with
`apply --cached`; an unstage press, from the HEAD → index hunk, with `apply --cached --reverse`; never through an editor, and never
from §6.2's display addition. At the press, the index entry's blob id and the worktree file's bytes must equal those the hunk was
composed from.

### 7.3 History edits with no working tree

Cherry-pick, revert, rebase onto, and rename, applied by one ref transaction at the judged old id (§8.2), on a branch **no tree has
checked out** (the list's `worktreepath`, read before and after the effect), never the line, a session branch or a landed branch.
`git replay` is not used: git's own documentation marks it experimental.

**Supported, precisely** (Codex 23, Fable N9): **a linear range of non-merge commits, none of them a root commit, none published**:
reachable from no remote-tracking ref, as D109's replay refuses to rewrite published history (a force push being refused later does not
make the rewrite acceptable now). Merges in the range, a root commit, and cherry-picking or reverting a merge are refused, said.

- **Pick C onto T**: `merge-tree --write-tree --merge-base=C^ T C` (git 2.40 and later), then `commit-tree` with C's author name, email
  and date and C's message unchanged, the committer being the checkout's configured identity first (D198 point 5).
- **Revert C on T**: `merge-tree --write-tree --merge-base=C T C^`; the message git's own (`Revert "<subject>"`, a blank line, `This
  reverts commit <C>.`).
- **Rebase B..tip onto O**: pick each commit in order onto the last result.
- **A result whose tree equals its parent's** (an empty commit) refuses the act, naming the commit; it is not dropped silently.
- **Signed commits** in the range refuse: Daoris does not re-sign, and a rewrite would drop the signature.
- **Every id is pinned**: the source range, the target tip and the branch's old id are read once, and the transaction verifies them.
- **Cost said**: 2n serial starts for n commits; progress streams with the time; past 50 commits (provisional) the act refuses and
  names the command to copy. A conflict anywhere refuses the whole act, naming files and hunks; nothing has moved, since objects alone
  were written.

### 7.4 Agents

**Nothing new** (Claude, Fable). An agent runs its own git in its own tree; Daoris's reader is not offered to it as a tool. The terminal
read verbs (§9) read for anyone; a writing verb goes through the person's press (§8.3), and refuses where `DAORIS_SESSION_ID` is set, an
extra guard for an ordinary session, never the proof of a person.

## 8. Writes (Q6)

### 8.1 What every act does

Every act is planned, shown with the command it will run, **the hooks it will run** and the effects beyond its row, and judged again
at the ids shown under its lock (D147 §3.1). An act that reaches a remote passes D198's doors (§8.3). Daoris's own writes are
serialised per ref by git's locks and `update-ref`'s old id, and per checkout by §7.2's lock (Codex X6).

**Effects beyond the row, said in the plan** (Codex 21, Fable N8): a commit, amend, switch or stash runs the checkout's hooks
(`pre-commit`, `commit-msg`, `post-commit`, `post-checkout`, a formatter that rewrites files in place), named where the repository has
them, never skipped (`--no-verify` is not offered); and git's auto maintenance, as the person's own `git commit` would. Every
`update-ref` runs a `reference-transaction` hook where one exists. A rename moves the branch's configuration section (§8.2). A
**fetch Daoris runs** passes `-c maintenance.auto=false -c gc.auto=0 -c fetch.writeCommitGraph=false`, so nothing of Daoris's own
choosing is written beyond the remote refs and objects (D147 §4.2). Reflogs are git's and are written as git writes them.

**No `push -u`, no upstream tracking**: they write the checkout's configuration (D147's reason stands).

### 8.2 How each write recovers

**Ref removals and rewrites reuse the judged-delete transaction** (Codex 22, Fable N14; `.claude/knowledge/ref-removal-transactions.md`,
`ContentHolds.DeleteJudgedAsync`): one `update-ref --stdin` transaction carrying a `verify <ref> <commit>` for exactly each ref the
judgement read, at the commit it read it; a `create` of the recovery ref (D102's SQUASHTIDY1c note, CLEANUP1's pattern) in the same
transaction; then the act's own `update`, `create` or `delete` at the judged old id. A refused transaction changes nothing and is
**read, not assumed**: absence is only git's own "missing", any other answer is unread and keeps the recovery ref
(`.claude/knowledge/git-ref-absence.md`; `for-each-ref` can skip a broken ref, so a snapshot's omission is never proof of absence).
**Late checkout** (Codex 22): `update-ref` asks no working tree, so the trees are read before and again after; where a tree checked the
branch out in between, the branch is put back create-only at its old id and the act says so. This **occupancy rule extends to
rewrites and renames**, not deletes alone.

| Act | Effect step | Recovery |
|---|---|---|
| Fetch | git's own fetch | none needed: remote refs only; prune's removals named before and after |
| Create a branch or a local tag | `update-ref --stdin`: `verify <name>` (absent), `create` | delete it |
| Delete a local branch or tag | the judged-delete transaction, with its recovery ref | the recovery ref |
| Rename a local branch | one transaction: `verify` the proof refs, `create` the new name and the recovery ref at the old id, `delete` the old name at it; then `config --rename-section branch.<old> branch.<new>`, its old contents kept in the act's record (the one configuration write, Codex 21); a new reflog, said | rename back; the section restored from the record |
| Pick, revert, rebase onto (§7.3) | objects first, then one transaction: `verify` source and target ids, `create` the recovery ref, `update` the branch from its old id | the recovery ref; nothing moved on a conflict |
| Push one branch | git's push of `<id>:refs/heads/<name>`, no `+` | none (outward), said (D37) |
| Stage, unstage | `apply --cached`, under the checkout lock | the opposite press |
| Commit, amend | `commit`, under the lock; amend first `create`s a recovery ref at the old tip | the recovery ref |
| Stash save | `stash push`, tracked files only unless *Include untracked* is chosen, said | apply it |
| Stash apply | refused unless the checkout is clean and no untracked file the stash writes exists; `stash apply` (never `pop`) | **retained conflicts are the supported outcome** (Codex 24): git has no "abort apply", so the plan says a conflict leaves the conflicted files for the Changes tab's resolve, with the stash kept |
| Stash drop | resolved by the stash's **commit id**, not a moving ordinal: the id at `stash@{n}` re-read under the lock, its commit `create`d as a recovery ref, then dropped; a stash whose message names a `daoris/` branch is a session's and refused, since `refs/stash` is shared across linked trees | the recovery ref |
| Switch branch | refused dirty, mid-operation, or where the branch is checked out elsewhere; `switch <branch>`, never `--force` | switch back |
| Resolve a conflict (1g-iv) | the chosen side or both per hunk written to the file, then `add` | the file's replaced bytes kept beside the operation until it ends |

### 8.3 D198's prerequisites, corrected

- **Every remote act** (fetch, push) needs **the doors installed** on that install: their installation is still opt-in at publish
  (`tools/desktop-publish.mjs:1326-1340`; Codex 2, 26). Where they are not, the act is not offered, and the plan says why. GITAUTH1c's
  identities store is partly built (`GitIdentities.cs`, `GitIdentityCommand.cs`, `GitAuthor.cs`), its remaining proofs standing.
- **A fetch** is a Read at the door.
- **A person's push** is a Write, which the door allows only with a grant or `no-push` off (`GitAccessRules.cs:40-46`). It needs
  **GITAUTH1f** (the person sets Write) and **a grant of its own**: the press writes a `GitGrants` grant for origin's push address,
  handed to the push's process as the landing hands its own (Fable N7), never borrowing a landing's grant (Codex 26). The push's
  refspec carries no `+`, so git's client refuses a non-fast-forward against what the remote advertises; the door, judging Write
  only, cannot itself prove the update is a fast-forward, and the plan does not claim it does.
- **A tag push** is Dangerous, and the door can see a tag update only once **GITAUTH1g**'s transport judge reads each update
  (`docs/2026-10-22-git-auth-design.md` §6.1). Round 1 named GITAUTH1f; **it waits for GITAUTH1g** and is not offered before it.
- **A terminal writing verb keeps the person's press** (Codex 27): each `daoris-driver git` writing verb is a `PersonPresses` route of
  `RouteClass.Person` (`InstanceRoutes.cs:9-21,156-170`): sent to the running window, shown there and run on the person's Confirm
  (RPC1c), the verb saying it waits; with no window answering, the standalone-terminal rule the other person's presses follow applies
  (`trees land --yes` today). `PipeCensusTests` and the verb census hold each. The `DAORIS_SESSION_ID` refusal stays, as an extra
  guard: an absent marker is never taken as proof of a person.

## 9. History at scale (Q7), the terminal door and Ask Daoris (Q8)

**History.** No index of Daoris's own, no cache on disk, and **no `commit-graph` press** (Codex X7): this repository already carries one
with changed-path filters (read, Fable N15); the other repositories here are at most a third its size. Where a history read is slow and
the repository has none (`objects/info/`, no process), the History tab's head names `git commit-graph write --reachable
--changed-paths` to copy. **A file's history** keeps its whole id list with each commit's path at `(commit, path)` from one
`log --follow --format=%H --name-status` walk (1h); until then, 1d-ii pages as GIT1b does today (Codex 28 found 1d promising what 1h
builds). **The pickaxe** is a long read (§4.2).

**The terminal door** (D50), one judge in the driver library for both doors:

- `daoris-driver git review <session> [--json] [--time]`: Review's answer; `--time` prints the graph's nodes, each start's command,
  gate wait, creation and run, what was kept, and writes `review.read`. The measurement door, built in 1a-i before the page changes
  (Fable N14).
- `daoris-driver git show|history|blame|compare|search|stashes|tags <repository> …`: the reads, each with its 1d slice.
- `daoris-driver git branches|fetch|branch|rename|delete|push|tag … [--plan|--yes]` (1e), `pick|revert|rebase … [--plan|--yes]` (1f),
  `checkout <repository> stage|unstage|commit|amend|stash|switch|resolve … [--plan|--yes]` (1g-ii to iv): writing verbs, each a person's
  press (§8.3).

Names are settled through the verb census (`cli-usage`) in their row. **Ask Daoris** (D110): the reads stay exempt, as `trace` is; a
`git` kind proposes the acts as cards the person presses (GIT1k, widened), never applying them; the Setup switch is proposed through
the setting door, never set. `HelpCoverageTests` holds each control to its kind or exemption.

## 10. The test plan (Q9)

### 10.1 The graph and the counts, load-independent, the fast half

- **The graph, as data**: a test builds `ReviewGraph` for each path of §3.1's table and asserts its edges, its **depth** and its
  **start ceiling**, computed from the data; the table's numbers are this test's (Codex 5, 28).
- **The executor, with held completions** (Fable N3, Codex 28): through the launch seam (§4.1), a stand-in holds every answer until the
  test releases it. The test counts the starts issued before each release: a node whose inputs have answered is started before the next
  release, and no node is started before its inputs answer. Depth observed equals depth computed; one start more than the ceiling fails,
  and each is shown to fail when a per-file or serial start is put back. Fallbacks and retries (V's one re-read) are counted apart.
- **Pinning**: a stand-in whose `HEAD` moves between R and S makes V see S's `branch.oid` differ, read once more, and flag *changed
  during read* when it moves again; never an answer mixing two heads (Fable B3, Codex 14).
- **Joining**: two waiters on one key start one read; one cancelling leaves the other answered; the last cancelling cancels the job.
- **REVIEWLOCK1's census, widened**: every read kind's flags asserted; `tools/optional-locks.mjs` refusing a worktree `diff` without
  `diff.autoRefreshIndex=false` (Fable B1); a source census refusing git started under `Trees/GitReads/` except through `Launch`.
- `ReviewBaseTests` and `TreeUpdateTests`, unchanged, prove the range's semantics did not move.

### 10.2 Real git, the Process half (`Parallel=Git`)

- **No lock**: REVIEWLOCK1's `ReviewLockTests`, run against the reader's nodes.
- **Configured programs**: a textconv and an external diff configured, neither runs on an object read; a merge driver named by
  attributes does not run in M (Codex 10).
- **The observation**: drops on a loose ref write, `pack-refs`, a `HEAD` move, a linked tree's `HEAD` move, a `worktree add`, a stash drop
  of an older entry; **a ref moved while a held stand-in `for-each-ref` is still answering** is not kept (Fable B2); a ref rewritten at the
  same length within the racy window is not kept (Codex 11).
- **Context**: two linked trees at the same ids with different attributes answer differently and never share a kept answer; a repository
  with `refs/replace/` or `shallow` keeps nothing (Codex 12).
- **Partial clone**: §5.3's fixture, the promisor asked nothing (Codex 13).
- **The display addition**: §6.2's table, a fixture per row (ASCII, UTF-8 with a non-ASCII path, CRLF, no trailing newline, empty, a NUL,
  Latin-1, an attribute with a filter, `eol`, a link, a nested repository, over 200 KiB, past the budget, a file growing during the read),
  the exact subset equal to the oracle (Codex 17, Fable N16).
- **The watch**: a write under a watched tree drops the kept answer; a write inside an ignored folder that holds a tracked file is heard;
  a watch released or lost makes the next reopen pay the validation read (Codex 15).
- **The held reader (1d-iv)**: page close, window exit, a cancelled creation, a cancel mid-blob (retired, the next answer correct), a
  desynced header; `gc` by the person and an auto gc from a linked tree beside it; a pruned generated tree recomputed (Codex 18, 19).
- **The acts** (1e-1g): bare-origin fixtures for push and fetch, the pre-push hook running; every refusal of §7-§8 on a real fixture;
  each transaction red without each `verify` (`JudgedDeleteGitTests`' pattern); a late checkout put back; a conflict refusing a pick whole;
  a merge, a root commit, a published commit refused; the switch off, a conversation starting, and a merge landing each refusing every
  checkout act; a stash apply's conflict leaving the stash kept; a stash drop by id after another drop moved the ordinals.

Per the person's standing instruction on this machine, a row runs its own classes filtered and the censuses; the whole halves run in CI
after the merge.

### 10.3 The bench and the window

- **The bench**, reported, never failing: `daoris-driver git review <session> --time` over the three cases on this repository (368 files;
  the same with 50 untracked made by the bench in `_fixtures/`; 1,771), at least ten runs each: median and p90 of wall, depth, starts, and
  per start the gate wait, creation and run, with the machine's load beside. Before: the brief's numbers, through the same verb once 1a-i
  lands its door on today's chain. After: §3.4's targets. Written to the row's fix-log entry with the sha.
- **The window** (GITAPP1i, the owner's): Review opened from the press, timed to first paint by `review.painted`; the driver's
  `review.read` (`session`, `ms`, `depth`, `starts`, `waitedMs`, `createdMs`, `files`, `untracked`, `kept`, `head`, no path). Three opens
  before the republish and three after, under the owner's load.
- **Held afterwards**: the graph census is the gate; the bench's medians are read at each release rehearsal, and a rise past twice a
  target is a row, not a failure.

## 11. Surfaces in English and Chinese

Chrome only; commit messages, patches, file and branch names stay as written. Each new term passes D116's check before it is drawn,
git's own Chinese translation being the source where it has one. Existing glossary terms used as they stand: *review* 审阅, *branch*
分支, *tree* 工作树, *history* 历史, *graph* 提交图, *commit* 提交, *fetch* 获取, *push* 推送, *merge* 合并, *line* 主线, *landing*
落地, *git remote* 远程仓库.

| Surface | English | 中文 | Row |
|---|---|---|---|
| Review's head, a kept answer | Read at {time} · HEAD {id} | 读取于 {time} · HEAD {id} | 1a-ii |
| Review's head, a read that moved | Changed while it was read: HEAD {from} → {to} | 读取期间已变化：HEAD {from} → {to} | 1a-ii |
| Review's foot while its reads run | Reading… | 正在读取… | 1a-ii |
| A file still changing | This file changed while it was read | 读取时该文件仍在变化 | 1a-ii |
| An untracked file outside the exact set | Empty file · Not UTF-8 · Shown as stored, not as git would convert it · A link | 空文件 · 非 UTF-8 · 按存储内容显示，未按 git 转换 · 链接 | 1a-ii |
| A commit's page | Commit · Parents · Held by · Made by | 提交 · 父提交 · 包含于 · 由谁创建 | 1d-i |
| Diff views | Inline · Side by side · Word diff · Ignore whitespace | 内联 · 并排 · 按词对比 · 忽略空白 | 1d-i |
| LFS | A file kept by LFS ({size}) | 由 LFS 保存的文件（{size}） | 1d-i |
| A file row | Blame · History · Before this change | 逐行追溯 · 历史 · 此更改之前 | 1d-ii |
| Compare | Compare with… | 对比… | 1d-ii |
| Search | Search messages, authors, paths · Search content (slow) | 搜索说明、作者、路径 · 搜索内容（较慢） | 1d-ii, 1h |
| Branches tab | Stashes · Tags · Remotes · Worktrees · Reflog | 贮藏 · 标签 · 远程仓库 · 工作树 · 引用日志 | 1d-iii |
| Review's foot, the conflict preview | Would conflict in {n} files / Lands without conflict | 将在 {n} 个文件中冲突 / 可无冲突落地 | 1d-iii |
| Ref acts | Rename · Create tag · Delete tag | 重命名 · 创建标签 · 删除标签 | 1e |
| A remote act not offered | Needs Daoris's git doors on this install · Needs push allowed for this repository | 需要在此安装上启用 Daoris 的 git 门 · 需要允许推送到此仓库 | 1e |
| History edits | Pick onto… · Revert · Rebase onto… | 拣选到… · 还原 · 变基到… | 1f |
| A refusal | Conflicts in {files}; nothing was changed | {files} 中有冲突；未做任何更改 | 1f |
| An act's undo | {commit} is kept as {ref} until {date} | {commit} 保留为 {ref}，直至 {date} | 1e-1g |
| An act's plan | Runs these hooks: {hooks} | 将运行以下钩子：{hooks} | 1g-ii |
| Setup's switch | Daoris may change this checkout's files | 允许 Daoris 修改此检出中的文件 | 1g-i |
| The Changes tab | Changes · Staged · Not staged · Stage · Unstage · Commit · Amend | 更改 · 已暂存 · 未暂存 · 暂存 · 取消暂存 · 提交 · 修订 | 1g-i, 1g-ii |
| A hunk and a line | Stage hunk · Stage line | 暂存块 · 暂存行 | 1g-ii |
| Stash and switch | Stash · Apply · Drop · Include untracked · Switch | 贮藏 · 应用 · 丢弃 · 包括未跟踪文件 · 切换 | 1g-iii |
| Resolving | Take ours · Take theirs · Take both · Open in your editor · Mark resolved | 采用我方 · 采用对方 · 两者都要 · 在你的编辑器中打开 · 标记为已解决 | 1g-iv |
| History's head | To speed this up in your own git: {command} | 如需在你自己的 git 中加速：{command} | 1h |

*Submodule* (子模块), *amend* (修订) and *hook* (钩子) are the words D116's check settles first: git's own catalogue and the glossary's
neighbours are read then. Stories at 1546 and 680 px in both themes and both languages, and UX6a's counter, hold each screen.

## 12. The rows (Q10)

Each row lands alone: its own proof, its terminal verb, bridge, Ask Daoris kind and translations within it (Codex 29, Fable N14).

1. **REVIEWLOCK1** (built on `daoris/s-e276dfc9`; lands first). §1.1's rule and census. Nothing in this design rebuilds it.
2. **GITAPP1a-i — the reader and Review's graph, benched** (driver; after REVIEWLOCK1). `GitReader`, `GitReaders`, the launch seam with
   its read kinds and the one counting event; the observation (§5.1-§5.2); the cache with its context (§5.3); joining; the two
   schedulers (§4.2); `ReviewGraph` and its executor; untracked display additions (§6.2); `LandedReviews` through the reader (Codex 28);
   `daoris-driver git review --time`, landed first on today's chain for the before-bench. **Proof**: §10.1 whole; §10.2's lock,
   configured-program, observation, context, partial-clone and display-addition cases; the bench before and after.
3. **GITAPP1a-ii — Review's page on the observation** (modules, web; after 1a-i). `SESSION_DIFF` answering with its observation; the
   offer and HANDOFF_PLAN after the diff, timed apart; *Read at*, *Changed while it was read*; `review.read` from the route,
   `review.painted`; the page's re-read on every `updated` replaced by the 15 s validation read. **Proof**: the route's counts per path;
   the page's stories in both languages; a mocked bridge proving the offer asked after the diff.
4. **GITAPP1b — Review follows the tree** (after 1a-ii). §5.4's second watch and its dependencies; §6.3's tree watch for every attended
   tree; pre-read joining the page's refetch. **Proof**: a fake change source moving the line invalidates every review in the repository;
   a file written under the tree drops the kept answer; a lost watch makes the reopen pay 2 starts; an idle reopen starts nothing; a
   commit shown ≤ 3 s on the bench.
5. **GITAPP1c — the judgements through the reader** (after 1a-i). `help.*`, the tidy, the landing's plan, `GitBranches.ListAsync` and the
   offer take layout and ids from the reader; the offer's count bounded per eligible branch. **Proof**: PROC1a's start counts per
   judgement before and after.
6. **GITAPP1d-i — the commit page and diff views** (after 1a-i; takes GIT1i's commit page). A commit, side by side, word diff, whitespace,
   LFS pointers. **Proof**: `GitReadsTests` per read's one call and key with context; stories and counters.
7. **GITAPP1d-ii — blame, a file's history, compare, search** (after 1d-i; takes GIT1f). As §7.1; file history paged as today. **Proof**:
   as 1d-i; blame with `blame.ignoreRevsFile` configured unchanged.
8. **GITAPP1d-iii — the Branches tab's reads and the conflict preview** (after 1d-i). Stashes, tags, remotes, worktrees, reflog; the
   conflict preview in Review's foot. **Proof**: as 1d-i; the stash list moving on an older drop; the preview's `merge-tree` under the
   empty attribute source.
9. **GITAPP1d-iv — the held object reader** (after 1d-i; integrated into the screens only after its proof). §4.3. **Proof**: §10.2's held
   reader cases, the repack hazard measured and written to the fix log; if it stands, the screens keep one start a burst and the row
   closes saying so.
10. **GITAPP1e — the ref and remote acts** (after 1d-iii; takes GIT1g, GIT1h, GIT1k). Fetch, create, rename and delete a local branch,
    local tags, through §8.2's transactions; **push one branch only once GITAUTH1f is built and the doors are installed** (§8.3), the
    rest landing without it. **Proof**: as GIT1g and GIT1h name it; each transaction red without each `verify`; a late checkout put back;
    the rename's section restored; the person's push refused with no grant and allowed with the press's; the terminal twins as person's
    presses.
11. **GITAPP1f — history edits without a working tree** (after 1e). §7.3. **Proof**: each over real fixtures; a conflict, a merge, a root,
    a published or signed commit, an empty result each refusing with nothing moved; a checked-out branch refused, and one checked out
    between judgement and effect put back.
12. **GITAPP1g-i — the switch, the lock and the Changes tab read** (after 1e). The Setup switch under Reach, off by default; `CheckoutLock`
    taken by `MergeAsync` and a conversation's start in the checkout; the Changes tab's three observations (§7.2) and its watch; **no
    act**. **Proof**: the lock held by each taker refuses the others; the tab reads a staged-and-reverted file in both lists.
13. **GITAPP1g-ii — stage, unstage, commit, amend** (after 1g-i). By file, hunk and line; the plan naming hooks. **Proof**: §10.2's acts;
    the switch off, a conversation starting, a landing holding the lock, and changed index or file bytes each refusing; a formatting
    `pre-commit` hook's rewrite said.
14. **GITAPP1g-iii — switch branch and stash** (after 1g-ii). §8.2's rows. **Proof**: a dirty switch refused; an apply's conflict
    retained with the stash kept; a drop by id; a session's stash refused.
15. **GITAPP1g-iv — resolve a conflict** (after 1g-iii). A side per hunk, the person's editor, mark resolved. **Proof**: the replaced
    bytes kept until the operation ends; a file not in conflict refused.
16. **GITAPP1h — history at scale** (after 1d-ii). The file history's kept id list, the pickaxe as a long read, submodules, the
    commands to copy. **Proof**: the third page of `TASKS.md`'s history no longer walking from the top (one walk counted); the pickaxe
    cancelled ends its git; two pickaxes in two repositories leave a short read a place.
17. **GITAPP1i — the install, seen** (the owner; after 1a-ii, again after 1d-iii and 1g-ii). §10.3's window timings and the screens in
    both languages. **Proof**: the fix-log entry and its screenshots.

1a-i makes Review's reads fast and measurable; 1a-ii puts them on the page; 1b keeps them fast while a session moves; 1c pays the same
debt where the landing and the judgements wait; 1d draws the half of D147 that is built and drawn nowhere.

## 13. Fit with D147, D113, D150 and D198

- **D147** is amended, never rewritten: point 2 and design §2.8 (tags locally, stash, staging, commit, cherry-pick, revert and a linear
  rebase are offered in §7-§8's forms; **§2.8's "merge-conflict editor" exclusion is amended**, since *Take ours/theirs/both* and *Mark
  resolved* write a file, while a free three-way editor stays rejected; checkout's reset, clean, a force and a remote delete stay
  refused; Fable N11); point 3 and design §3.1 (acts may write the person's checkout behind its switch; a history edit writes objects
  and one ref transaction); **the rejection "Checking out in the person's checkout" (`D147.md:58`) and design §3.1's "Checking out is
  not offered"** (`docs/2026-10-04-built-in-git-design.md:136-139`) are amended by the switch (Fable N11); point 6 and design §4.1 ("one
  process a view" becomes the reader: a held graph of bounded starts, a held object reader for the screens, an identified observation;
  still Tools' git and never a library). **§4.2 stands**, said exactly (Fable N10): no ref, configuration or file on disk of Daoris's own
  choosing and nothing cached on disk; objects where git's own commands write them (`merge-tree --write-tree`, `commit-tree`); the named
  exceptions are a recovery ref, a renamed branch's configuration section, and the effects git's hooks and auto maintenance make for an
  act the person pressed in their own checkout. Design §3.1's "D51 rule 7" is corrected to D102's SQUASHTIDY1c note and CLEANUP1.
- **D113 §5 stands** (Fable N11): it rejects reading the person's checkout for a landed session's review, and the landed review still
  reads objects. Round 1 named it as amended; it is not.
- **D150 point 3**: a repository's page gains a **Changes** tab beside Branches and History, shown only where the switch is on; the
  switch lives in Setup's **Reach** section. No Git place returns to the activity bar; the branch, then the commit, stays the
  organising object (D55); there is still no editor or file tree.
- **D198 stands**, its prerequisites said as §8.3 has them: the doors installed, GITAUTH1f and a press's grant for a person's push,
  GITAUTH1g for a tag push. A local act passes no door.
- **D51, D55, D109, D164, D173, D181 stand.**

## 14. Rejected, by proposal

**From Claude's**: ≤ 4 starts and its 1.5 s target (they drop REVIEWBASE1 and REVIEWBASE3); Review on the person's gate place (§4.2); a
held process for two idle minutes, eight across the machine (§4.3); the generation counter alone (§5.1 keeps a generation, never alone);
a pushed slice per review, this round (§3.3); a tree watch on every running session (§6.3 watches attended trees); the `commit-graph`
press (§9); the file history cursor (§9); a free three-way editor (D55).

**From Codex's**: the manual worktree of the person's (§7.2); "four short jobs, two walks" as one global bound above the gate (§4.2 keeps
per-reader places and a per-process walk limit); agents' authorised local operations through Daoris (§7.4). Taken from it: the union
counts and every range path kept, the offer and plans off the patch's path, the context in the key and partial clones, *Read at* rather
than *current*, the refusal list, serialised own writes, the held-process cap, and in round 2 the display addition, the effect policy,
the observation's validation, process ownership, the transactions and the D198 prerequisites.

**From Fable's**: the idle session tree's acts (§7.2); the offer over the snapshot's tips (§3.2); `ahead-behind` for both counts; the
`commit-graph`, fsmonitor and untracked-cache presses; idle-machine targets as a held column. Taken from it: REVIEWLOCK1, the layout and
refs from disk with the stamp and its order, the levels handed longest first and the speculative committed diff, one head id, the
per-reader bound and Background class, the held process's shape and its hazard, the kept file-history ids, the targets' medians, the
bench, the watch for attended trees.

**By all three**: libgit2 or a reader of git's files (D147 §4.3; the stamps and small contents of §5.1 validate git's answers, they do
not replace them); Daoris's own history index; a cache on disk; polling `status` on a clock; a patch per file fetched from git; checking
out in a session's tree; a force push; a delete on origin; a push by rule; line comments; fetching on a timer; changing the gate's width.

## 15. Not measured

The installed window (Review's paint and the press-to-paint time); LANDING, HANDOFF_PLAN and the offer beside the diff; a tree with
hundreds of real untracked files; the held process's repack hazard; the observation on a reftable repository (none here); the tree
watch under an agent's dependency install; `GIT_NO_LAZY_FETCH` against a real partial clone here; `--attr-source` on Tools' git; the
gate's `waitedMs` under a sweep's fan-out; the reader's eight run places; an idle machine. Every target is provisional, set from the
brief's and Fable's probes under the load they named; the rows measure the rest before they are accepted.

## 16. The reads, answered

Each finding was checked by Claude Opus at the lines it cites, at `fe4809b75` and on REVIEWLOCK1's branch. All held. **Taken** means
the design now says what the finding asked, or the stricter of two ways it offered; **answered** means part of it is met another way,
said in the row.

**Codex's read** (`docs/2026-10-23-git-app-reads.md`, read-only, at `92c7b8ed5`). Verdict: keep proposed and revise. 29 findings, 22
blocking.

| # | Finding | Checked at | Taken or answered | Where |
|---|---|---|---|---|
| 1 | "13-19 + U" is neither minimum nor ceiling; the offer is unbounded; page vs driver hits | `WorkingTree.cs:326-358`, `ReviewRanges.cs:42-126`, `TreesRoutes.cs:70-92`, `ContentHolds.cs:163-177`, `trees.ts:168-184`, `LandedReviews.cs` | Taken: today's count `12 + I + M + Uᵣ`, the offer unbounded, page hits apart, the probe a replay | §1, §3.1, §3.2 |
| 2 | GITAUTH1c is partly built; the doors' installation is opt-in | `GitIdentities.cs`, `GitIdentityCommand.cs`, `GitAuthor.cs`, `desktop-publish.mjs:1326-1340` | Taken; the brief is kept as written and corrected here | §1, §8.3 |
| 3 | The ref watch hears less and selects fewer than said | `RefWatch.cs:229-242,249-265,291` | Taken; the brief corrected here; the reader's watch by common-directory identity, linked checkouts included | §1, §5.4 |
| 4 | The scanner is an inference, not an attribution | `FIX-LOG.md:5226,3374` | Taken; gate wait, creation and run measured apart | §0, §1.2, §10.3 |
| 5 | The cold path is deeper than three rounds | `ReviewRanges.cs:72-77` | Taken: a dependency graph with per-path depth; the counts moved beside the merge-bases | §3.1 |
| 6 | The targets do not follow from the arithmetic; the gate bounds creation | `ProcessStarts.cs:29-31`; `WorkingTree.cs:271,274,451` | Taken but for the numbers: kept as **provisional** targets (the asker's instruction), no longer claimed from arithmetic; run places defined apart; the bench through the real gate; 1,771 as a bounded list | §3.4, §4.2 |
| 7 | A per-reader bound does not reserve capacity for Review | `ProcessStarts.cs:192,215,223` | Taken within the reader (attended first, walks one per reader and two per process); answered across readers by `waitedMs` before any gate change | §4.2 |
| 8 | "Every read" exceeds the enumerated contract | design §7.1 | Taken: §7.1 is the boundary; deferred capabilities listed; object rebase told apart from interactive editing | §7.1, §7.3 |
| 9 | The global option is placed after the subcommand | `HelperCapture.cs:104`, `HelpCheckouts.cs:104` | Taken: `git --no-optional-locks <command>`; the census checks placement (as REVIEWLOCK1's does) | §1.1, §3.1 |
| 10 | The census proves less than "no writes"; textconv, filters, drivers | `WorkingTree.cs:166,187,205,335,472`, `TreeWorkReads.cs:77`, `ContentHolds.cs:497`, `TreeSlims.cs:27` | Taken: REVIEWLOCK1 covers the 24 sites; the effect policy per read kind; `--no-textconv`; the empty attribute source for `merge-tree`; fixtures for configured programs; name reads not classed as writers | §1.1, §10.2 |
| 11 | Time plus size cannot make the snapshot authoritative | design §5.1 | Taken: generation and stamps before and after, small contents read whole, the racy-time rule, `worktrees/`, `commondir`, the stash log, reftable counted, fail closed; "an identified observation, not an atomic view" | §5.1 |
| 12 | The cache context is underspecified across linked trees and the page | `GitReads.Formats.cs:73`, `GitReads.cs:492`, `git.ts:262` | Taken: options frozen, `--attr-source`, a context id validated before lookup, joining and completion, the tree's identity where a read depends on it, carried into the page's keys | §5.3 |
| 13 | The partial-clone fallback still fetches | design §5.2 | Taken: `GIT_NO_LAZY_FETCH` proved for the git in use, otherwise reads refused before they start | §5.3 |
| 14 | The snapshot, dirty diff and joining can describe different HEADs | `trees.ts:81-108`, `queries.ts:146` | Taken: W pinned to `h`, S's `branch.oid` checked by V, one bounded re-read, *changed during read*; the page's key stays the session; joining by ids and context with separate waiter cancellation | §4.1, §5.2 |
| 15 | Zero-start freshness lacks a safe fallback | `TreesRoutes.cs:70` | Taken: every attended mutable tree watched, live or ended; installed before the answer; exclusions proved; any loss pays the counted validation read | §6.3 |
| 16 | Invalidation follows too few dependencies | `TreeFollows.cs:152`, `stateHub.ts:31` | Taken: dependencies by the tree's actual `HEAD` and the line; a line move invalidates every review; mutable screen families and missed-event reconciliation named; pre-read and refetch join | §3.3, §5.4 |
| 17 | The hand-built untracked patch is not byte-equal as specified | `WorkingTree.cs:386-387` | Taken: Codex's display addition, exact for a stated subset, never a staging patch; attributes in one start; bounds stop reading | §6.2 |
| 18 | The held process's protocol and lifetime are incomplete | design §4.3 | Taken: framed by length, retired on fault or mid-answer cancel, owned by the reader, four per process | §4.3, §4.4 |
| 19 | Cached generated trees need an object-lifetime rule | `ReviewRanges.cs:92` | Taken: a missing generated object is a miss, recomputed | §5.3 |
| 20 | Checkout permission and conversation exclusion are not atomic | `ChatRunner.cs:439`, `ChatGoOn.cs:442` | Taken: `CheckoutLock`, the conversation's start and the merge landing taking it, everything re-read under it; session trees refused independently | §7.2 |
| 21 | Allowed side effects exceed the `.git` rule | design §8 | Taken: the rename's section named and kept; Daoris's fetch without auto maintenance; hooks, reflogs and maintenance said in plans | §8.1, §8.2, §13 |
| 22 | Old-value CAS loses holder proofs and late checkout | `ContentHolds.cs:373,378`; the two knowledge documents | Taken: the judged-delete transaction reused, absence only on git's "missing", late checkout put back, extended to rewrites and renames | §8.2 |
| 23 | Object history editing lacks semantics | design §7.3 | Taken: linear, unpublished, non-merge, non-root; each base, metadata, empty and signed rule; ids pinned; recovery in the transaction | §7.3, §8.2 |
| 24 | Stash conflict handling promises a transaction git lacks | design §8 | Taken: retained conflicts as the outcome on a clean start; drop by commit id; inclusion said; a session's stash refused | §8.2 |
| 25 | Two calls cannot support the staging controls | design §6.1, §8 | Taken: three observations for Changes; patches from the right comparison; entries and bytes revalidated | §7.2 |
| 26 | The remote acts point to the wrong D198 milestone; a person's push needs a grant | `GitAccessRules.cs:40-75,144`; git-auth design §6.1; `TASKS.md` GITAUTH1g | Taken: tag push on GITAUTH1g; the doors installed; a press's own grant | §8.3, §12 row 10 |
| 27 | The terminal write contract omits the person's press | `InstanceRoutes.cs:9-21,156-170` | Taken: writing verbs are `PersonPresses` routes; the session-marker refusal kept as an extra guard | §7.4, §8.3, §9 |
| 28 | The first row's census and order cannot prove its claims | `WorkingTree.cs:1020,1093,1187` | Taken: one launch seam counting all three runners; depth by held completions; `LandedReviews` moved into 1a-i; 1d-ii pages as today, 1h keeps the ids | §4.1, §10.1, §12 |
| 29 | Rows 1d and 1g are too broad | design §12 | Taken: 1d in four, 1g in four, each landing alone with its doors and words; the held reader proved before integration; the lock before any destructive act | §12 |

**Fable's foundation read** (at `92c7b8ed5` and REVIEWLOCK1's branch). Verdict: accept after B1-B3. Three blocking, sixteen not.

| # | Finding | Checked at | Taken or answered | Where |
|---|---|---|---|---|
| B1 | `--no-optional-locks` is not the no-lock rule for a worktree `diff` | REVIEWLOCK1's fix-log entry and `445720582` | Taken: REVIEWLOCK1's rule stated and pointed to; its census widened for `diff.autoRefreshIndex=false` | §0, §1.1, §10.1 |
| B2 | The stamp has no order or racy rule | `LineGuesses.cs:33-37` | Taken: before and after, the racy-time rule, coarse filesystems keep nothing; the held `for-each-ref` case | §5.1, §10.2 |
| B3 | The uncommitted diff reads symbolic `HEAD` while the range reads the id | `WorkingTree.cs:330` | Taken: one head id for both halves, checked after | §5.2 |
| N1 | The hazard is any auto gc in the shared store; the bound is per process | design §4.3 | Taken: `gc.pid` and the pack listing; auto gc from a linked tree measured; four per process | §4.3 |
| N2 | `status` is the long pole; a level of five is two waves | brief's status timings; `ReviewRanges.cs:63-69` | Taken, both: longest first, and the speculative `<recorded>..h` diff | §3.1 |
| N3 | The round census needs held answers | `WorkingTree.Answered`, `Runs` | Taken | §10.1 |
| N4 | The widened ref watch must not feed today's consumers; `MostRoots` is per watch | `DriverWatch.cs:432-438`, `HelpFacts.cs:141-142`, `ChangeWatch.cs:189,201,255` | Taken: a second watch for the reader, a third for trees, each capped | §5.4 |
| N5 | `session-branches.json` names no session | `SessionBranches.cs:19-20`, `TreeFollows.BranchOfTree` | Taken | §5.4 |
| N6 | The merge landing and the Changes tab need one lock | `TreeLands.cs:162-168,264-298` | Taken: `CheckoutLock`, `MergeAsync` among its takers | §7.2 |
| N7 | The person's push needs a grant at the door | `GitAccessRules.cs:40-46` | Taken | §8.3 |
| N8 | Commit runs hooks, which may write files, and auto gc | design §7.2 | Taken: hooks named in the plan, never skipped | §8.1 |
| N9 | History edits are two serial starts per commit; merges | design §7.3 | Taken: the count, progress, a cap; merges refused | §7.3 |
| N10 | "Nothing written into `.git`" is not whole; pruned kept trees | design §1, §13 | Taken | §0, §5.3, §13 |
| N11 | The amendments' targets | `D147.md:58`, built-in git design §3.1, D150 point 3 | Taken: D113 §5 stands; D147's rejection and design §3.1 amended; the switch under Reach; §2.8's bullet amended | §7.2, §13 |
| N12 | The stamp's walk is unbounded | design §5.1 | Taken: 256 loose files, then the folders' times | §5.1 |
| N13 | `IgnoredPaths` is the service's | `IgnoredPaths.cs:92-114` | Taken: the driver's own list, counted, proved | §6.3 |
| N14 | Split 1a; cite the ref transaction knowledge | design §12 | Taken: 1a-i (driver and bench door) before 1a-ii (page); the knowledge cited for every ref write | §8.2, §12 |
| N15 | REVIEWLOCK1 is built; the Bloom filters read; 33 files | REVIEWLOCK1's branch | Taken | §1 |
| N16 | No non-UTF-8 fixture | `WorkingTree.cs:911-912` | Taken: compared at the decoded text, marked *not UTF-8* | §6.2, §10.2 |
