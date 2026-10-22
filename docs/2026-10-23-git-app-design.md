# Daoris's git, as comprehensive as a git management app, and Review fast: the design (GITAPP1, 2026-10-23)

**Status: proposed (D202), round 3: Review and the reader only.** Round 3, by Claude Opus at `9900d32f7` with round 2 merged, answers
Codex's read of round 2 (`docs/2026-10-23-git-app-reads.md`, "Codex, round 2") in §17. The steward narrowed this round so Review fast
can be accepted and built first: this design and D202 settle the reads; **the person's acts and writes** (the checkout lock, the ref
and remote acts, history edits, stash, staging, the Changes tab, the switch, D198's prerequisites for a push) **moved whole into
GITAPP2** (`docs/2026-10-23-git-writes-design.md`, D204, proposed and read later), which states what round 2 said of them. The
sections below that named the acts point there. What round 2 wrote follows.

**Round 2: after Codex's read and Fable's foundation read of round 1 (D191).** Round 1 was the synthesis,
written by Claude Opus from the brief (`docs/2026-10-23-git-app-brief.md`) and the three sealed proposals
(`docs/2026-10-23-git-app-proposals.md`: Claude's, Codex's through Daoris's helper, Fable's), at `92c7b8ed5`. The two reads of it are
`docs/2026-10-23-git-app-reads.md`: Codex's (29 findings, 22 blocking; keep proposed and revise) and Fable's (B1-B3 blocking, N1-N16
not; accept after them). Round 2, by Claude Opus, checked each finding against the code at `fe4809b75` and against REVIEWLOCK1's
branch (`daoris/s-e276dfc9`, `445720582`..`f80814215`), and takes or answers each in §16. Nothing is built. `Dnn` is
`docs/decisions/Dnn.md`; `Driver/` is `src/Daoris.Desktop/Daoris.Desktop.Driver/`, `Modules/` is
`src/Daoris.Desktop/Daoris.Desktop.Modules/`, `Web/` is `src/Daoris.Web/`. "Claude", "Codex" and "Fable" below name the three
proposals, and §-numbers prefixed C, X and F point into them; "Codex n" and "Fable Bn/Nn" name the reads' findings.

## 0. The answer (round 3)

1. **Review's cost is serial process starts**, and a start on this machine costs 0.2-1.6 s. The likeliest cause, inferred from
   PROC1a's and UIFREEZE1's stacks and two probes rather than proved, is process creation slowed by a real-time scanner and load
   (§1.2). The cure is fewer starts, started together.
2. **Review is a dependency graph of id-keyed reads**, built as data in the driver and held by a test (§3): **depth 2 and at most
   10 starts cold in the ordinary case**, as §3.1 qualifies it exactly; none per untracked file, none for the tree's layout or for a
   guessed line (both read from disk and the refs observation), and none on an open where a valid watch says nothing moved. The graph
   generates every path (the grown base, the line's three sources, the landed and gone reviews, each failure), and the census counts
   each path's starts under the one launch seam in four budgets: the graph, the context read, the watch's establishment and the one
   retry (§3.1). ReviewBase's range semantics are kept whole.
3. **Targets, provisional**, under the brief's load, medians: 16.2 s → ≤ 3 s; 44.1 s → ≤ 3.2 s; 8.4 s → ≤ 4 s; opened again with
   nothing moved ≤ 0.1 s; a live session's commit shown ≤ 3 s after its ref write (§3.4). The graph's depth and start counts are the
   gate; the times are a row's acceptance, measured on the bench through the real gate.
4. **No lock and no index refresh in a tree Daoris does not own**, as REVIEWLOCK1 measured and built it: `git --no-optional-locks`
   before the command, and `-c diff.autoRefreshIndex=false` on a diff against the working tree, its stat-only rows dropped (§1.1).
   This design points to REVIEWLOCK1 and widens its census to the reader's one launch seam; it designs no second rule.
5. **One `GitReader` per common git directory** (§4), one registry per process, every launch through one counting and classifying
   seam, its runs bounded apart from the gate's creation places, one run place reserved for attended reads and the rest handed
   attended first among queued reads, walks limited per reader and per process.
6. **Freshness is an identified, validated observation** (§5): the refs are read between two stamps and a watch generation, and
   **kept only where they can be validated whole**: under the reader's ref watch, with every ref a review rests on read whole at each
   use; a repository with no valid watch keeps nothing and reads its refs again on every use (§5.1). **One head id answers both halves
   of a review**, and a **final barrier** over every returned component validates the refs, status's own `branch.oid`, the tree's
   watch generation and the context before an answer is kept or published (§5.2).
7. **The context is enumerated and watched** (§5.3): the binary and its version, the environment handed to git, every configuration
   file git names and every candidate it would read, every attribute file (`info/attributes`, the effective global and system ones),
   and the repository's shape; read by one counted context read, heard by the reader's watch with a deadline behind it, and carried to
   the page as a context revision through a `git` scope the state hub gains.
8. **Configured programs** (§1.1): no external diff or textconv on an object read, each command given only the controls it accepts;
   `merge-tree` keeps the repository's own merge semantics and is **not run where the configuration defines a merge driver**, which is
   the only way git runs one (§1.1). **Version floors are git's own release notes** (§5.3): `--attr-source` 2.41, `--no-lazy-fetch` 2.45,
   `git var`'s attribute and configuration paths 2.42, each passed as a global option an older git refuses rather than ignores.
9. **Who owns each read's git process** is one table (§4.4). **No held git process** in this design: the screens pay one start per
   question as GIT1b does; the held `cat-file` is deferred to its own design, with the protocol facts Codex found recorded (§4.3).
10. **The person's reads** (§7): what §7.1 lists, which is the capability boundary, the conflict preview among them. **The person's
    acts and writes are GITAPP2's** (D204).
11. **A session's tree is the session's**; the person reads it in full. **Nothing of Daoris's own written into `.git`** by a read:
    objects only where `merge-tree --write-tree` writes them, nothing cached on disk.
12. **Rows** (§12): REVIEWLOCK1 (landed on `main` after this tree's base), then GITAPP1a-i, the launch seam and the bench door,
    **landing alone first**; 1a-ii the reader, the observation and the context; 1a-iii Review's graph; 1a-iv untracked display
    additions; 1a-v Review's page; 1b, 1c, 1d-i to 1d-iii, 1h, 1i.
13. **Kept from round 1, as both reads keep it**: one reader per common directory, ReviewBase's semantics whole, the Accept offer
    and the plans read after the diff, session trees only their sessions own.

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
| A conversation's start takes the tree lock | — | `ChatRunner.cs:439`, `ChatGoOn.cs:442`: only for an isolated tree | Holds (Codex 20); a checkout's conversation takes none today (GITAPP2 §2) |
| The merge landing writes the checkout | — | `TreeLands.cs:162-168` ("the one place Daoris writes into a checkout it did not create") | Holds (Fable N6); GITAPP2 §2's lock covers it |
| A person's push passes the door | round 1 §8 | `GitAccessRules.cs:40-46`: Write only with a grant or `no-push` off | Needs a grant (Fable N7, Codex 26); GITAPP2 §4.3 |
| Tag push waits for GITAUTH1f | round 1 §8 | `docs/2026-10-22-git-auth-design.md` §6.1; `TASKS.md` GITAUTH1g | **GITAUTH1g**, the transport judge (Codex 26, Fable N7) |
| The terminal's writing verbs are the person's | round 1 §9 | `InstanceRoutes.cs:9-21,156-170`: `RouteClass.Person`, `PersonPresses`, `PipeCensusTests` | The existing seam (Codex 27); GITAPP2 §4.3 |
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
| Object diffs (two-id `diff`, `log -p`, `show`) | external diff, textconv (whose cache can write `refs/notes/textconv/*`) | `--no-ext-diff --no-textconv` on each; a textconv'd file shows as git shows it without the conversion (binary, D111), said |
| `blame` | textconv | `--no-textconv`, the one control it takes (`--no-ext-diff` is a diff option `blame` does not accept) |
| `merge-tree --write-tree` (REVIEWBASE3, the conflict preview) | a custom merge driver; a clean or smudge filter where `merge.renormalize` is on | **not run where the context read (§5.3) shows any `merge.<name>.driver` defined, or `merge.renormalize` true beside any `filter.<name>.*`**; otherwise run with the repository's own merge semantics and no `--attr-source` (round 3, Codex R2-6; below) |
| Plain walks and object names (`rev-list`, `merge-base`, `log` without a patch) | none | no flag needed |
| Worktree reads (`status`, `diff <id>` against the tree) | clean filters (LFS's), the fsmonitor hook or daemon the configuration names | run as for any `git status` in that tree: they are the tree's own configuration, and Daoris turns nothing on; said in the read kind's remarks |
| Name and layout reads (`rev-parse`, `worktree list`, `for-each-ref`, `config --list`, `var -l`) | none | no flag needed, and the census never classes them as index writers (Codex 10's converse) |

**Why `merge-tree` is refused rather than suppressed** (Codex R2-6, checked at `ReviewRanges.cs:84-95` and git's own
`gitattributes.adoc:1116-1128,1162-1170`). Round 2's `--attr-source=<empty tree>` was wrong twice: it replaces only the worktree's
`.gitattributes`, so `info/attributes`, the global and system files and `merge.default` still name a driver; and it changes the answer,
since a committed `merge=union` or `merge=binary` rule decides the merged tree. Git runs a custom merge driver only where the
configuration defines it (`gitattributes.adoc:1165`: "The definition of a merge driver is done in the `.git/config` file"); an
attribute or `merge.default` naming an undefined driver falls back to the built-in three-way merge, and the built-in `text`, `binary`
and `union` run no program. So the rule is read where the drivers are defined: the context read's configuration (§5.3) lists every
`merge.*.driver` in effect, from every scope and include. Where one is defined, `merge-tree` is not started: Review reads from the
line's base, which is today's answer when git cannot merge (`ReviewRanges.cs:84-88`), and says *this repository defines a merge driver,
which Daoris does not run*; the conflict preview says *Not previewed*. Where none is, `merge-tree` runs **as today, in the asking tree,
under that tree's attributes** (`ReviewRanges.cs:92` passes no attribute source), so REVIEWBASE3 is unchanged. Its answer therefore
depends on the tree's `.gitattributes`: it is a worktree-context read (§5.3), kept only under the tree watch. The preview says what it
is: *Merges without conflict here* (此处可无冲突合并), a landing merging in the checkout under the checkout's attributes. Fixtures: a
driver that writes a marker, defined once each in the repository's configuration with a committed attribute, with `info/attributes`,
with a global attributes file, and as `merge.default`: none runs, each refused before the start; and a committed `merge=union` gives
the merged base git gives without Daoris.

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
| Working-tree acts | person's checkout, behind a switch | a manual tree of the person's | an idle session's tree | **The person's checkout, behind a switch** (GITAPP2 §2) |
| Ref-only history edits | cherry-pick, revert, rebase by objects | in the manual tree | in the idle tree | **By objects, linear and unpublished, on a branch no tree holds** (GITAPP2 §3) |
| Conflict resolution | three-way view writing a chosen result | whole side or external tool | resolved on the page | **A side per hunk, or the person's editor** (Codex, D55) |
| Agents | nothing new | common reads and their own tree | nothing new | **Nothing new** |
| File history pages | continue from the last id | cache prefixes, prove a cursor | keep the id list | **Keep the id list with each commit's path** (Fable), in 1h |

The rows *Working-tree acts*, *Ref-only history edits* and *Conflict resolution* are GITAPP2's since round 3 (D204), kept here as
the comparison was made. The row *A held process* is deferred since round 3 (§4.3).

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
- **The grown base** (WSR6): `context.Grown.Of(repository, branch)`, an in-memory record read with the branch name above
  (`ReviewRanges.cs:101-108` asks `rev-parse --abbrev-ref HEAD` for the name today; the `HEAD` file answers it).
- **The line** (round 3, Codex R2-1): a line set in the person's choices is read as today (`TreeContext.LineAsync`,
  `CanonicalLine.ResolveAsync`). **Git's guess** at an unset line is no longer up to three starts (`TreeContext.CheckoutLine` is
  `WorkingTree.DefaultBranchAsync`, `WorkingTree.cs:105-124`: `symbolic-ref` of `refs/remotes/origin/HEAD`, then `rev-parse --verify`
  of `main`, then `master`, each a start; `LineGuesses`' stamps keep it only for a main checkout, `LineGuesses.cs:33-48`). It is
  **derived from R's answer**, the same three questions in the same order: R's `%(symref)` of `refs/remotes/origin/HEAD` with
  `refs/remotes/origin/` taken off, as `symbolic-ref --short` and the slash rule give it; else `refs/heads/main`, else
  `refs/heads/master`, where R lists it and its object or its peeled object is a commit (`%(objecttype)`, `%(*objecttype)`), as
  `^{commit}` asks. So the line is a function of R, not a node, and costs no start; a unit test holds the derivation equal to
  `DefaultBranchAsync` over a fixture of each case.

Then the nodes, `h` being the head id the refs observation resolved for the tree's `HEAD` (its file's `ref:` read in R's answer, or
the id a detached `HEAD` holds):

| Node | Read | Kind | Needs | Starts |
|---|---|---|---|---|
| **C** | the context read (§5.3): `git version` once per binary per process, then `git var -l` and `git config --list --show-origin --show-scope -z` | names | nothing; **skipped** while the kept context validates | 0-3 |
| **R** | the refs observation (§5.1): one `for-each-ref` (`%(refname) %(objectname) %(objecttype) %(*objecttype) %(symref)`), between two stamps | names | nothing; **skipped** where a kept observation validates | 0-1 |
| **S** | `git --no-optional-locks status --porcelain=v2 --branch -z --untracked-files=all` | worktree | nothing | 1 |
| **A** | untracked files' attributes: one `check-attr --stdin -z` over all of them (§6.2) | worktree | S, where S lists any | 0-1 |
| **U** | the untracked display additions, read from disk | no process | S, A | 0 |
| **W** | `git --no-optional-locks -c diff.autoRefreshIndex=false diff … h` (the uncommitted diff, **pinned to `h`**) | worktree | R | 1 |
| **B₁, B₂** | `merge-base h <line>`, `merge-base h <origin-line>` | object | R | 0-2 |
| **K** | `merge-base --is-ancestor <recorded> h` | object | R, where the recorded base is an id | 0-1 |
| **K′** | `merge-base --is-ancestor <grown> h`, the grown base (WSR6), **speculative beside K** | object | R, where a grown base is recorded, is an id and differs from the recorded base | 0-1 |
| **N** | `rev-list --count <line-refs…> --not h` and its converse (REVIEWBASE1's unions) | object | R | 0-2 |
| **D₀** | the committed diff `<recorded>..h`, speculative (Fable N2b) | object | R, where the recorded base is an id | 0-1 |
| **I** | `merge-base --independent <candidates>` | object | B₁, B₂, K, K′, where two or more distinct candidates | 0-1 |
| **M** | `merge-tree --write-tree <line-base> <recorded-base>` (REVIEWBASE3), as today; not started where a merge driver is defined (§1.1) | worktree context | I, where incomparable | 0-1 |
| **Σ** | **selection**: the record's base (K, else K′, else none), the candidates, `from` (`ReviewBase.Newest`) and `against` (M), exactly as `ReviewRanges.ReviewRangeAsync` decides them (`ReviewRanges.cs:35-79`) | no process | B₁, B₂, K, K′, I and M where they ran | 0 |
| **D** | the committed diff `<from>..h` (REVIEW3's one-call reader, its caps unchanged) | object | Σ; **skipped where Σ selected the recorded base** and D₀ answered | 0-1 |
| **V** | **the final barrier** (§5.2): every returned component has answered (Σ, the selected D₀ or D, N, S, W, U), then the refs observation, S's `branch.oid` = `h`, the tree's watch generation and the context id are validated together | no process | everything returned | 0 |

**D₀ before Σ.** D₀ may answer before Σ decides whether it is the review's diff. It is an id-keyed read, correct for its own key
(`<recorded>..h`) whatever Σ decides, so it is kept by that key; only V's answer names it as the review's, where Σ selected it.

**The paths are generated, not listed** (round 3, Codex R2-1). `ReviewGraph` is built from the review's conditions: the
observation (kept or not), the line (set, guessed from R, none; one or both refs here), the recorded base (an ancestor, displaced, not
an id), the grown base (none, the same, differing), the candidates Σ finds (one, two comparable, incomparable), untracked files (none,
some), and the context (kept or not). The census test enumerates **every combination**, builds the graph for each, runs it through the
executor with held completions (§10.1), and holds each path's depth and starts to the ceilings below, computed from the data. A path
the table does not name is still generated and held to the general ceiling. **Four budgets, counted apart under the one launch seam
and reported with their total** (Codex R2-2): the **graph** (the table below), the **context** (C: 0 while kept; 2 per repository
context, 3 with the binary's first `git version`; one level before the graph), the **watch** (§6.3's establishment, beside the graph,
never on its path) and the **retry** (V's one re-read, at most the path's own graph again, once).

| Path, cold, the context kept | Depth | Graph starts | Notes |
|---|---|---|---|
| **Ordinary**: own tree; the recorded base an id and an ancestor of `h`; no grown base recorded apart from it; a line set or guessed from R; both line refs here; Σ finds one distinct candidate | **2** | **≤ 9**, **≤ 10** with untracked files | R ∥ S, then W, B₁, B₂, K, N×2, D₀ (and A after S); D₀ is the answer |
| The refs observation kept and valid | 1, **2 with untracked** (S → A) | ≤ 8, ≤ 9 with untracked | R skipped; A still follows S (Codex R2-2) |
| Two or more distinct candidates, comparable | 4 | ≤ 11, ≤ 12 | D₀ wasted, counted; I, Σ, then D |
| Incomparable bases (REVIEWBASE3) | 5 | ≤ 12, ≤ 13 | I, M, Σ, then D; where a merge driver is defined, M is not started and the path is the one above |
| No line (none set, none guessed) | 2 | ≤ 5, ≤ 6 | R, S, W, K, D₀ |
| The recorded base displaced, the grown base an ancestor | 3 | ≤ 11, ≤ 12 | K fails, K′ answered beside it; D₀ wasted; D after Σ |
| The same, two candidates; incomparable | 4; 5 | ≤ 12, ≤ 13; ≤ 13, ≤ 14 | I; I and M |
| The recorded base displaced, no grown base | 3, 4 with two candidates | ≤ 10, ≤ 11; ≤ 11, ≤ 12 | no M (`ReviewBase.Incomparable` needs a record) |
| The recorded base not an id | 3 | ≤ 9, ≤ 10 (K′ where a grown base is) | no K, no D₀ |
| **The general ceiling** | **5** | **≤ 14** | every node once |
| A layout git must name (reftable, unknown) | its path's + 1 | + 1 | one counted `rev-parse` before R |

**The landed and gone reviews** (`LandedReviews.cs:33-129`, `LandedProofs.cs:75-165`), through the reader in 1a-iii, with their own
nodes: X expands an abbreviated recorded `From` (`IsCommitId` takes 7-64 hex digits, `WorkingTree.cs:160-161`); L is
`--is-ancestor <entry tip> <tip>`; F is `--is-ancestor <from> <tip>`; D₀ₗ the diff `<from>..<tip>`, speculative; MBₗ `merge-base <line
ref> <tip>`, speculative; Σₗ selects as `LandedFromAsync` does. The checkout's top is the layout rule (no `IsTopLevelAsync` start);
the branch's tip and the line's forms are R's (no `rev-parse`).

| Landed path | Depth | Starts | Notes |
|---|---|---|---|
| No checkout, not a top, or the record's words not an id and a name | 0 | 0 | from disk and the record |
| Standing, its recorded `From` in the branch's history | 2 | ≤ 6 | R ∥ X, then L, F, D₀ₗ, MBₗ; D₀ₗ is the answer |
| Standing, `From` not held: from where it leaves the line | 3 | ≤ 7 | MBₗ, then D; D₀ₗ wasted |
| Not ours (L fails) | 2 | ≤ 6 | the speculative starts wasted, counted |
| **Gone** (no ref in R, or `GoneAt` recorded): WSR5's proof | 3 | ≤ 11 | R ∥ E (`cat-file -e <tip>^{commit}`); then per line form `--is-ancestor`, `merge-base` and the names `tip..form`, and one `rev-list --count`; then per form the names `base..tip`; Σ_g selects in `ProveAsync`'s order (first merged form, else the first with nothing still differing, else the closest). Today up to 12 serial starts and the line's guess |

**Warm** (Codex R2-2). Every object node is keyed by ids and the context (§5.3), so a second open reads none of them while they
stand.

| Open again | Starts | Depth |
|---|---|---|
| Landed or gone, the observation kept | 0 | 0 |
| Landed or gone, the observation not kept (§5.1) | 1 (R), then any object node whose key moved | 1 |
| Live or ended own tree, its tree watch valid (§6.3) and the observation kept | **0** | 0 |
| The tree watch not valid (never proved, released, lost, overflowed, hinted, or its index moved), the observation kept | S ∥ W, and A where S lists an untracked file: 2-3 | 1-2 |
| Neither valid | R, S, W, and A: 3-4, then object nodes whose keys moved | 2 |
| After a commit | as cold, its object nodes for the new `h` (their keys are new) | 2 |

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
  1a-v the page keeps its 15 s freshness for the validation read; in 1b the tree watch keeps it at 0.
- **A commit or a switch in the tree**: the tree's `HEAD` and its branch's ref move; the reader's watch hears them (§5.4), drops the
  observation, and publishes the review's scope for the sessions whose tree that is. Pre-read and the page's refetch join the same
  in-flight read by its key (§4.1), so they cannot publish two answers.
- **The line moved** (a fetch, D109, a landing): every review in that repository depends on it (§5.4), not only those on the moved
  branch (Codex 16).
- **Claude's pushed slice per open session** (C2) is not taken: the scope and the id-keyed cache give the same effect without a new
  slice contract; it is reconsidered if 1b's measurement shows the refetch's round trip matters.

### 3.4 The targets, provisional

**Round 3 keeps every number provisional**, as the asker's round-3 instruction has it; the context read's first run and the watch's
establishment are reported beside them, never inside the graph's count. **Provisional acceptance targets**, held on the bench (§10.3) and on the installed window (GITAPP1i) under the brief's load, named
beside every number; median over at least ten runs with p90 beside; a ceiling fails a row's acceptance, never a gate (D54's rule for
budgets). **They are not derived**: round 1 called them the arithmetic of three rounds, and Codex 6 showed a permitted trace past five
seconds. What the graph does say is a floor: depth × one start's wait and creation, plus the longest read on the path. The bench
measures the real graph through the shared gate; where a target is missed, the row says which node and which part (wait, creation,
run), and the target is revisited with that evidence rather than the code bent to it.

| Case | Today | First open, median / p90 | Depth, starts | Opened again, nothing moved | A live session's commit |
|---|---|---|---|---|---|
| 368 files committed, nothing untracked | 16.2 s | **≤ 3 s / ≤ 6 s** | 2, ≤ 9 | watch valid: ≤ 0.1 s, 0 starts; else ≤ 1.5 s, 2-4 starts (§3.1's warm table). Landed: ≤ 0.1 s, 0 | shown ≤ 3 s after the ref write (1b) |
| 368 files, 50 untracked | 44.1 s | **≤ 3.2 s / ≤ 6.4 s** | 2, ≤ 10; 0 per untracked file | as above | as above |
| 1,771 files committed | 8.4 s | **≤ 4 s / ≤ 8 s** | 2, ≤ 9 | as above | as above |

The 1,771-file case is **a complete file list with bounded patches**, never the whole 16.6 MB patch (`WorkingTree.cs:271,274,451`;
Codex 6). The page's paint of 368 rows is measured, not assumed: 1a-v writes `review.painted`, and collapses rows past the first
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
named by its ids, and is not kept. **A mutable read** (S, W, A, U, M, and a review's whole answer) adds to its key the tree's identity,
`h`, and the tree's **watch generation at the read's start** (round 3, Codex R2-3): a request made after a hint or a loss carries a
newer generation and never joins a read begun before it. The page keeps its own guard (`Web/src/bridge/sessions/trees.ts:81-104`: a
read a newer one replaced never lays its answer over the newer one's), unchanged.

**What the seam counts** (round 3, Codex R2-1-3): every start of a review, the landed reviews and the context read, including those
today outside Review's chain (`TreeContext.CheckoutLine`'s guesses, `IsTopLevelAsync`, `LineGuesses`), each tagged with its budget
(graph, context, watch, retry); the census fails on a start with no budget or past its budget's ceiling.

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

- **Eight run places** (provisional, measured in 1a-ii): enough for the ordinary path's one level of eight.
- **One reserved, the rest attended first** (round 3, Codex R2-10, as `ProcessStarts.cs:37-42` reasons for the gate): unattended reads
  (a pre-read, a judgement's sweep) hold **at most seven**, so one place is always free of them; a read a person is waiting on (an open
  review, a screen in view) is handed a freed place **before any queued unattended read**. Round 2's "never queues behind unattended
  ones" overclaimed: priority alone cannot take back places already running, and the reserved place is what bounds the wait.
- **Long reads** (a pickaxe, a whole-history walk, a file's history over all commits) hold **at most one place per reader and two
  per process**, so a search never starves a short attended read of the same repository, and several repositories' searches never
  fill the gate's background places together (Codex 7).

Across readers, the gate's background queue stays first-come. `review.read`'s `waitedMs` per start measures whether a pressed review
waits there behind another repository's work; if it does, a later row gives attended reads a priority **inside** the background class,
never the person's reserved place (Fable F4.3). That part of Codex 7 is answered by measurement, not by changing the gate now.

### 4.3 The held object reader: deferred (round 3)

**Deferred out of D202** (round 3, Codex R2-8, the quest's "or that half deferred"). Nothing Review asks is an object question, and
no screen this design builds needs one held: the commit page is two dependent starts (`GitReads.cs:290-313`, §7.1), and the screens
pay one start per question as GIT1b does. A held `cat-file` is its own design when a screen's measured burst needs it. **Round 2's
protocol was wrong, checked against git's own `git-cat-file.adoc`** (`:143-175`, `:294-297`, `:341`, `:362`): `info <oid>` answers
a header alone, `<oid> <type> <size>`, with no body, and only `contents <oid>` is followed by `size` bytes and a newline, so reading a
body after `info` waits for bytes git never sends; `<oid> missing` has neither; a request already written cannot be withdrawn, its
answer stays in the stream to be drained or the process retired; `--batch` (the fallback round 2 named below git 2.36, whose
`--batch-command` is in `RelNotes/2.36.0.adoc:75`) takes object names, not commands, so below 2.36 the held reader is off; and
`cat-file` takes no diff options, its textconv and filters being off unless `--textconv` or `--filters` is given (`:73-79`), so it
needs no control appended. The later design starts from these and from round 2's lifetime and repack notes, which follow as round 2
wrote them.

**Round 2's lifetime and ownership.** The reader owns it: started on the first question, ended **30 s after its last** (Fable F4.2), on the
reader's context or observation changing (a replace ref, a new git), and on the registry's disposal at window exit or the verb's
end, which kills and waits for each. **At most four per process** (Fable N1: a registry is per process, so "across the machine"
could not be held); one per reader.

**The repack hazard** (Fable N1; Codex 19). Every linked tree shares the object store, so an agent's own `commit` or `fetch` in its
tree can run `gc --auto` there, and on Windows a pack a process holds mapped cannot be unlinked: the person's `git gc` prompts "Unlink of
file … failed", a detached auto gc warns and leaves the pack. So the held reader **never starts while `<commondir>/gc.pid` exists and
retires when it appears or the `objects/pack/` listing changes**; its later design measures it beside the person's `git gc` and an auto gc fired
from a linked fixture tree (`git -c gc.auto=1 commit`). If the hazard stands with those rules, the screens live without it and pay
one start a burst. Its integration into 1d's screens waits for that proof (Codex 29).

### 4.4 Who owns each git process

| Process | Owner | Started | Ended | On cancel | On window exit or verb end |
|---|---|---|---|---|---|
| A one-shot read (any node) | its job in the reader, holding a run place | at `Launch` | exit, its output consumed | the job's token, cancelled only when its last waiter goes: `Ending` kills the tree, stderr drained, waited for | the registry's token: killed and waited |
| A long read (pickaxe, walk) | the reader's long place | at `Launch` | exit, or the page closing | streamed results stop; killed and waited; nothing kept | killed and waited |
| The context read (C) and the watch's exclusion reads | the reader, as a one-shot read | at `Launch` | exit | as a one-shot read | killed and waited |
| `daoris-driver git …`'s read verbs | the verb's registry | per the rows above | the verb's end | Ctrl-C cancels its reads | as the window |

Every read is bounded as today: the runner ends git at its `GitBounds` bound whatever the caller asked (`WorkingTree.cs:1022-1059`),
and a read ended so is an unread answer, never kept. An act's process is GITAPP2's (D204), where Codex R2-15 is open.

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
  older stash need not move the tip, Codex 11); and the loose files of the refs an answer rests on. **No walk of every loose ref and
  no folder-time stand-in** (round 3, Codex R2-4, replacing round 2's 256-file bound): a folder's write time moves for a name added to
  that folder, not to a nested one (`refs/heads/team/topic` moves `refs/heads/team`, not `refs/heads`), and no time at all shows a ref
  rewritten in place. The completeness a stamp census could not give comes from the watch, below.
- **What is read whole** (content validation, Codex 11), because it is small and decides the answer: each tree's `HEAD` file, and the
  loose files of the refs a review rests on (the tree's branch, the line here and on origin), 41 bytes each. `packed-refs` is hashed
  where it is at most 1 MiB; past that, its stamp and the watch generation guard it, said.
- **Kept only where it can be validated whole** (round 3, Codex R2-4: "where complete validation is unavailable, do not retain").
  An observation is retained **only while the repository is one of the reader's ref watch roots** (§5.4), that root open and its
  generation unchanged since before the read, with no loss, overflow or resume since: the watch hears every ref write under
  `refs/**`, `packed-refs`, the `HEAD` files and `worktrees/*`, nested or not, in place or not, and the stamps and whole-read contents
  are the check at each use behind it. **A repository with no valid watch** (past the cap of its roots, a root lost or never opened,
  a filesystem whose watch cannot be trusted) **keeps nothing**: its refs are read again on every use, one counted R, depth 1, and
  `review.read` says so. A ref the watch has not yet told of (its debounce is at most `ChangeWatch.Cap`, 2 s) can stand stale for that
  long in a branch list's decorations; never in a review, whose own refs are read whole at each use.
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
Codex 14). **V is the final barrier** (round 3, Codex R2-3): it waits for **every component the answer returns**: Σ's selection,
the selected committed diff (D₀ or D), the counts N, S, W, the untracked display additions U and their attributes A. Then it validates,
together and after all of them: the refs observation (§5.1's stamps, whole-read contents and watch generation); S's `branch.oid` = `h`;
**the tree's watch generation** unchanged since before S, W, A and U started, with no hint, loss or index move since (§6.3); and the
context id (§5.3). Only an answer V passes is kept or published as current. Where any differs, the reader reads the graph once more
(only the nodes whose keys moved), and where the second read moves again it answers with what it read, flagged **changed during
read** with both ids, never a mixture shown as one state. Retries are bounded at one, in the retry budget. A tree with no valid watch
(none established yet, or released) cannot have its uncommitted part validated: its answer is shown *Read at …* and never kept.

**The page's key stays the session** (Codex 14): dirty files and the line's tips change without `h` changing (`queries.ts:146`). The
answer carries its observation (`h`, `from`, the line's tips, the tree's generation), the page draws *Read at … · HEAD …* from it, and
the driver's immutable patch cache is keyed by ids and context. A mutable observation of a session (its tree, its line) and an
immutable answer keyed by ids are kept apart and never joined by the same key.

### 5.3 What is kept, under what key and context

| Answer | Key | Kept |
|---|---|---|
| A range's files and patches (Review's committed part, a commit, a compare, a landed branch) | common dir, view, from, to, options, parser version, context id | until evicted |
| `merge-base`, `--is-ancestor` (its "no" is an answer), `--independent`, `rev-list --count` | common dir, command, ids, context id | until evicted |
| `merge-tree --write-tree` (M, the preview), a worktree-context read (§1.1) | common dir, ids, context id, the tree's identity and watch generation | under the tree watch only (§6.3); a generated tree found missing is a miss (below) |
| The context (C) | the common dir and the tree's identity | until a file it names or would read moves (below) |
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
**`git --attr-source=<to>`** on object reads, so the committed `.gitattributes` come from the commit read rather than from whichever
linked tree asked (git 2.41 and later, below). `--attr-source` replaces only the worktree's `.gitattributes` (`git.adoc:229-232`;
`gitattributes.adoc:70-77`: `info/attributes` first, then the tree's files, then the global and system files), so the rest is the
context's.

**What forms the context id** (round 3, Codex R2-5), by category, each read by the context read C and each named with how its change
is heard:

| Input | Read by | Heard by |
|---|---|---|
| The binary: Tools' git's path and `git version` | the path from `GitWiring`; `git version` once per binary per process | a switch of Tools' git (the `tools` scope, `stateHub.ts`) makes a new binary |
| The environment the seam hands git (`GIT_CONFIG_*`, `GIT_CONFIG_COUNT`/`KEY_n`/`VALUE_n`, `GIT_CONFIG_NOSYSTEM`, `GIT_ATTR_NOSYSTEM`, `HOME`, `XDG_CONFIG_HOME`); the seam sets `GIT_ATTR_SOURCE` to nothing of its own | their values, hashed | fixed for the process's life |
| Configuration: every file `git config --list --show-origin --show-scope -z` names (system, global, the repository's `config`, the tree's `config.worktree` under `extensions.worktreeConfig`, each `include.path` and matched `includeIf` target), and **every candidate it would read though it named none**: `git var -l`'s `GIT_CONFIG_SYSTEM` and each `GIT_CONFIG_GLOBAL` line (two here, the XDG file and the home one), the repository's and the tree's files, and each include's target from its key, present or absent | C's answer, as text, hashed; each file stamped, an absent one stamped absent | inside the common directory, the reader's ref watch (§5.4); outside it, a root per folder holding such a file, non-recursive and filtered to its names, on the same instance; and that topic's deadline (provisional 60 s) re-stamping them with no process |
| Conditional includes: an `includeIf.onbranch:…` key in C's answer makes the configuration depend on the tree's branch; `includeIf.gitdir:…` on the tree | C's answer | where one is present, the tree's identity and `HEAD`'s branch join the context's key, and a `HEAD` move re-reads C |
| Attributes: `info/attributes`, `git var -l`'s `GIT_ATTR_GLOBAL` (the effective `core.attributesFile`) and `GIT_ATTR_SYSTEM` | the files' contents, hashed where at most 1 MiB, else their stamps | as configuration above |
| The repository's shape: `refs/replace/`, `info/grafts`, `shallow`, `objects/info/alternates`; `extensions.partialClone` and any `remote.<name>.promisor` from C's answer | stamps from disk; C's answer | the ref watch; where replace refs, grafts or `shallow` exist, nothing of that repository is kept |

The context id is **the hash of what was read**, not of the stamps: a stamp that moved re-runs C (2 starts), and a context whose
answers are unchanged keeps its id, so a touched file costs no page's answers. `git var -l` listing `GIT_ATTR_*` and
`GIT_CONFIG_*` is git 2.42 (`RelNotes/2.42.0.adoc:16`); below it, the global and system attribute files are not known, and no object
answer of that binary is kept. `--show-scope` is git 2.26 (`RelNotes/2.26.0.adoc:48`).

The context id is **validated before lookup, before joining and at completion**, and it **reaches a page that asks nothing**
(Codex R2-5, checked at `Web/src/bridge/state/stateHub.ts:25-68`, which has no `git` scope today, and `git.ts:262-294`, whose commit,
history and blame queries are kept with `staleTime: Infinity`). 1a-v adds: a `GIT_CONTEXT` read per repository answering its
**context revision** (the id), with no process while C's answer stands; the id-keyed queries carry that revision in their keys and
wait for it; and a `git` scope in the state hub mapped to the repositories' context queries and branch lists. When C's answer
changes, the driver publishes `git` for that repository; an open page's context query is asked again, its revision moves, and every
id-keyed query of the old revision is asked under the new key. A test changes a global attributes file while an open commit page
makes no request, and sees the page re-read under the new revision.

**Two linked trees** (round 3, Codex R2-17). Under `--attr-source=<to>`, an object read at the same ids in two linked trees with the
same context id gives the same answer and **shares one kept answer**; a fixture holds that equality. Their **worktree** reads (S, W, A,
M) answer from each tree's own `.gitattributes` and differ where those differ; they are never kept apart from their tree's watch and
never shared. A tree whose `config.worktree` or `onbranch` include gives it a different context id keeps its own object answers.

**Generated objects** (Codex 19, Fable N10). A `merge-tree` answer is a tree id no ref holds, which the person's `gc` may prune after
two weeks. A read that finds a kept generated object missing is a **miss**: the entry is dropped and the object computed again, never
an answer of absence and never "git could not read". A fixture prunes a kept generated tree and sees it computed again.

**Partial clones** (Codex 13; round 3, Codex R2-7). A local read must never fetch. **The floor is git's own release notes, read in
Tools' git's documentation, not memory**: `git --no-lazy-fetch` is git 2.45 (`RelNotes/2.45.0.adoc:120`), and `git.adoc:183-188` says
it equals `GIT_NO_LAZY_FETCH=1`; round 2's 2.44 was wrong. On git 2.45 and later every read that may touch an object passes
**`git --no-lazy-fetch`** as a global option, and the seam also sets the variable for git's children: the option is the guard, because
a git that does not know it refuses to run (an unknown global option), where an unknown variable is silently ignored and would fetch.
Below 2.45, the option is not passed and, where C shows a partial clone (`extensions.partialClone`, or a remote with `promisor`), every
object-dependent read is **refused before it starts**, said; a repository with no promisor has nothing to fetch. A fixture holds it on
Tools' git: a `--filter=blob:none` clone of a local bare promisor that logs each `upload-pack`, then a two-commit `diff -p` that
**demands a missing blob**: refused, and the log empty. The below-2.45 branch is held by the launch seam's stand-in answering as git
2.44, which must see no option and the read refused; no older git is installed here to run.

**The floors, together**, each from `RelNotes/` in Tools' git's documentation (round 3, Codex R2-7), and each checked against the
binary's `git version` from C before its option is passed:

| Capability | Floor | Where git says so | Below it |
|---|---|---|---|
| `merge-tree --write-tree` (M, the preview) | 2.38 | `2.38.0.adoc:16` | no merged base, the line's base, as `ReviewRanges.cs:84-88` already does |
| `git --attr-source=<tree>` on object reads | 2.41 | `2.41.0.adoc:116`; `merge-tree`'s crash with it fixed in `2.43.0.adoc:257`, which this design no longer needs since M passes none | object answers not kept |
| `git var -l` naming the attribute and configuration files | 2.42 | `2.42.0.adoc:16` | object answers not kept |
| `git --no-lazy-fetch` | 2.45 | `2.45.0.adoc:120`; `git.adoc:183-188` | partial clones' object reads refused |
| `cat-file --batch-command` (deferred, §4.3) | 2.36 | `2.36.0.adoc:75` | no held reader |

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
| `worktrees/` changes | the worktree list | `git` |
| A file the context names or would read moves (§5.3) | the context, read again by C; where its id changed, every kept answer of the repository | `git` (the page's context revision), `diff` |
| A tree's index moves (§6.3) | that tree's uncommitted answer, and its watch's exclusions until proved again | `diff` for its session |
| A watch's loss, overflow, error, cap or the machine's resume | everything that watch guarded | all of the above for its repositories (missed-event reconciliation), and the next use reads afresh |

The state hub carries invalidation keys, not ids (`Web/src/bridge/state/stateHub.ts:31`); the page's next read asks the reader,
which joins any read already in flight for the same key (§4.1).

**Which watch** (Fable N4, Codex 3). The ref watch's current set (repositories holding Daoris's work) keeps its consumers
(`LandingWakes.Moved`, `HelpFacts.Woken`; `DriverWatch.cs:432-438`) unchanged. The reader's roots are a **second `ChangeWatch`
instance**, its hint only dropping observations and publishing scopes: every registered repository with a checkout, **main or linked,
resolved to its common directory and identified by it**, those holding work first, within its own 32; **past the cap nothing of that
repository's refs is kept** (§5.1, round 3: round 2 let the stamp alone guard). It hears `refs/**`, `logs/refs/stash`, `worktrees/*`,
`config`, `config.worktree`, `info/attributes`, `info/grafts` and `shallow` beside what `RefWatch.Hears` hears today, and the context's
folders outside the repository (§5.3), shared by every repository that names them. The tree watches of §6.3 are a **third instance**,
capped at eight attended trees; past it, an open review pays the validation read.

## 6. Working-tree state (Q4)

### 6.1 Read, never written

Two reads, **S** and **W** (§3.1), both under REVIEWLOCK1's rule; status is kept apart from the patch, since staged and unstaged
changes that cancel give an empty patch while the tree is not clean (Codex X4). Unknown is never clean. **fsmonitor and the untracked
cache are not turned on, and no press offers them**: they write the repository's configuration and start a daemon the person did not
ask for. Where the person turns them on, Daoris's `status` is faster with them. Where `review.read` shows a slow status, Review's head
(and GITAPP2's Changes tab) names the command (`git config core.fsmonitor true`) to copy.

### 6.2 Untracked files: a display addition from disk

An untracked file is shown as **a display addition** (Codex 17, taking Codex X4's contract): its path, its lines as added, and its
facts, read from disk within bounds. It is **never a staging patch**: no act applies it (GITAPP2's acts compose their patches from
git's own comparisons). It is **exact**, equal to git's text for the file, for a stated subset; outside it, the row says what it is instead:

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
Codex 15); the person's checkout's Changes tab is GITAPP2's. Released when the view closes or after fifteen idle minutes.

- **Installed before the answer is taken**: the root is watching before S and W start, and a hint during them drops what they read.
- **Exclusions are proved, not assumed** (Codex 15, Fable N13): `.git`, and an ignored folder only where git tracks nothing inside
  it. The driver computes its own list per root (E₁, one counted `ls-files --others --ignored --directory --exclude-standard`, then
  E₂, one `ls-files -z -- <those folders>` that must be empty), never the service's `IgnoredPaths`, which is another process. A change
  to a `.gitignore`, `info/exclude` or the configuration runs both again.
- **The tree's index is watched, and its move revokes the exclusions** (round 3, Codex R2-9). An ignored folder can gain a tracked file
  (`git add -f`), which only the index shows. So the tree watch holds a second root, **the tree's own index file**: `<gitdir>/index`,
  which for a linked tree is `<common dir>/worktrees/<name>/index`, outside the tree's folder; non-recursive, filtered to `index`. Its
  move (a) drops the kept uncommitted answer and publishes `diff` for the session, so an idle open page re-reads rather than waiting
  for its next request; and (b) **revokes every exclusion** until E₂ has run again and found those folders still untracked (one
  counted start); a folder found tracked is watched from then on. Until E₂ answers, every hint under the tree drops the kept answer.
  An index stamp alone never proves the files unchanged (an unstaged edit moves no index), only the watch does.
- **Its budget** (Codex R2-2), the watch budget of §3.1, never on the graph's path: establishing a tree's watch is E₁ then E₂, 2
  starts, depth 2, run beside the first read; an index move costs E₂, 1; an ignore or configuration change E₁ and E₂, 2. **Until the
  exclusions are proved, the answer is shown and not kept**: the first open keeps nothing whose watch was not yet whole.
- **0 starts on reopen only while the watch is valid**: it was installed before the kept answer, its exclusions proved, and it has
  seen no loss, overflow, cap, release, resume or index move since. Otherwise the reopen pays the counted validation read of §3.1's warm
  table: S and W, and **A where S lists an untracked file**, since attributes kept under a watch that lapsed are not trusted: 2-3 starts,
  depth 1-2, said in `review.read`.

## 7. What the person gets (Q5)

### 7.1 The reads, and the capability boundary

**This table is the capability boundary** (Codex 8): what the person gets is what it lists, not "every read a git management app
offers". Rows name where each lives (D150 point 3: Repositories → a repository's tabs; a session's Review) and which row builds it.

| Capability | Per repository | Per session tree | Where | Row |
|---|---|---|---|---|
| A commit | message, author, committer, parents as doors, refs holding it, who made it (D143), its changes in `PatchView`: **two dependent starts**, its fields then its range against the first parent (`GitReads.cs:290-313`; round 3, Codex R2-17) | the same | a commit's page | 1d-i |
| Diff views | inline (built), side by side, word diff, whitespace ignored (a re-read with `-w`, keyed by it); a binary says so (D111) | the same in Review | Review, a commit's page | 1d-i |
| LFS | a pointer drawn as *a file kept by LFS* with its size, never its pointer text | the same | Review, a commit's page | 1d-i |
| History graph | built; filters by branch, author, path, since and until (one `log`, keyed by tips and filter) | the branch beside its line | History tab; Review's *Show in Git* | 1d-ii |
| Blame | a gutter, runs grouped, *Before this change* | at the tree's `h` | a file row's *Blame* | 1d-ii |
| A file's history | following renames; pages as GIT1b reads them today | the same | a file row's *History* | 1d-ii; its kept id list 1h |
| Compare | two refs resolved once; `A...B` and `A..B` | the tree's branch against its line | a branch's *Compare with…* | 1d-ii |
| Search | messages and authors (one `log`, kept); paths at a commit (`ls-tree`); content at a commit (`git grep`) | from the tree's `h` | History tab's search | 1d-ii |
| Stashes, tags, remotes, worktrees, a branch's reflog | `stash list` and a stash's changes by its id; tags on the graph and a commit; `remote -v` through `GitRemotes`; worktrees with the session holding each; `reflog show <branch>`, never kept | — | Branches tab; a branch's page | 1d-iii |
| Conflict preview | — | `merge-tree --write-tree` of `h` onto the line, in the session's tree under its attributes: would it conflict here, in which files and hunks (objects only); *Not previewed* where a merge driver is defined (§1.1) | Review's foot, beside Accept | 1d-iii |
| Content across history | `-S`/`-G` as a long read: path-scoped, streamed, cancellable, never kept | the same | History tab's search | 1h |
| Submodules | listed from `.gitmodules` and `160000` entries, each a door to its own repository where registered | the same | a commit's files | 1h |

**Deferred, said rather than implied** (Codex 8): an image diff; **interactive history editing** (reorder, squash, reword, drop: a
different act from GITAPP2's object rebase, which replays a linear range unchanged); a free three-way editor (D55); a submodule's own
writes; LFS content fetched or shown; staging in a session's tree; signing a commit Daoris writes; a held object reader (§4.3). Each
is a later row if asked. **Every act and write** (stage, commit, stash, switch, resolve, the ref and remote acts, history edits) is
GITAPP2's (D204).

### 7.2 The working-tree acts, the checkout lock and history edits: GITAPP2

Round 2's §7.2 (the person's checkout behind the switch, the checkout lock, the Changes tab's three observations) and §7.3 (history
edits from objects) moved whole to `docs/2026-10-23-git-writes-design.md` §2-§3 in round 3 (D204).

### 7.4 Agents

**Nothing new** (Claude, Fable). An agent runs its own git in its own tree; Daoris's reader is not offered to it as a tool. The terminal
read verbs (§9) read for anyone; the writing verbs are GITAPP2's.

## 8. Writes (Q6)

Round 2's §8 (what every act does, how each write recovers, D198's prerequisites for a push) moved whole to
`docs/2026-10-23-git-writes-design.md` §4 in round 3 (D204), with Codex R2-11 to R2-16 as its open questions. A read writes nothing of
Daoris's own into `.git`; `merge-tree --write-tree` writes objects only (§5.3's generated objects).

## 9. History at scale (Q7), the terminal door and Ask Daoris (Q8)

**History.** No index of Daoris's own, no cache on disk, and **no `commit-graph` press** (Codex X7): this repository already carries one
with changed-path filters (read, Fable N15); the other repositories here are at most a third its size. Where a history read is slow and
the repository has none (`objects/info/`, no process), the History tab's head names `git commit-graph write --reachable
--changed-paths` to copy. **A file's history** keeps its whole id list with each commit's path at `(commit, path)` from one
`log --follow --format=%H --name-status` walk (1h); until then, 1d-ii pages as GIT1b does today (Codex 28 found 1d promising what 1h
builds). **The pickaxe** is a long read (§4.2).

**The terminal door** (D50), one judge in the driver library for both doors:

- `daoris-driver git review <session> [--json] [--time]`: Review's answer; `--time` prints the graph's nodes, each start's command,
  gate wait, creation and run, each start's budget, what was kept, and writes `review.read`. The measurement door, built in 1a-i on
  today's chain before anything else changes (Fable N14).
- `daoris-driver git show|history|blame|compare|search|stashes|tags <repository> …`: the reads, each with its 1d slice.
- The writing verbs are GITAPP2's (`docs/2026-10-23-git-writes-design.md` §4.3).

Names are settled through the verb census (`cli-usage`) in their row. **Ask Daoris** (D110): the reads stay exempt, as `trace` is;
its `git` kind's act cards are GITAPP2's. `HelpCoverageTests` holds each control to its kind or exemption.

## 10. The test plan (Q9)

### 10.1 The graph and the counts, load-independent, the fast half

- **The graph, as data**: a test builds `ReviewGraph` for each path of §3.1's table and asserts its edges, its **depth** and its
  **start ceiling**, computed from the data; the table's numbers are this test's (Codex 5, 28).
- **The executor, with held completions** (Fable N3, Codex 28): through the launch seam (§4.1), a stand-in holds every answer until the
  test releases it. The test counts the starts issued before each release: a node whose inputs have answered is started before the next
  release, and no node is started before its inputs answer. Depth observed equals depth computed; one start more than the ceiling fails,
  and each is shown to fail when a per-file or serial start is put back. Fallbacks and retries (V's one re-read) are counted apart.
- **Every path generated** (round 3, Codex R2-1): the census enumerates every combination of §3.1's conditions, live, landed and gone,
  and holds each to its row's depth and ceiling or the general ceiling; the grown base, the three line sources and each failure are
  among them. Each start carries its budget (graph, context, watch, retry); a start with none, or past its budget, fails.
- **The line from R**: the derivation equals `WorkingTree.DefaultBranchAsync` over origin's `HEAD` set, unset and dangling, `main`,
  `master`, both and neither, and a tag object under `refs/heads/main`.
- **The barrier** (Codex R2-3): with held completions, V publishes nothing while any returned component (Σ, D₀ or D, N, S, W, A, U) is
  held; a hint to the tree's watch released before V makes V read once more; D₀ answering before Σ is kept by its key and named the
  review's diff only where Σ selects it.
- **Pinning**: a stand-in whose `HEAD` moves between R and S makes V see S's `branch.oid` differ, read once more, and flag *changed
  during read* when it moves again; never an answer mixing two heads (Fable B3, Codex 14).
- **Joining**: two waiters on one key start one read; one cancelling leaves the other answered; the last cancelling cancels the job; a
  request after a tree hint does not join the read begun before it (Codex R2-3).
- **Run places**: eight unattended reads never take the reserved place; an attended read arriving behind seven running unattended ones
  starts at once (Codex R2-10).
- **The context reaches an idle page** (Codex R2-5): a mocked bridge whose `git` scope moves the context revision re-asks an open commit
  page's query under the new key with no request from the page; a stand-in binary answering as git 2.40, 2.42 and 2.44 sees each floor's
  option withheld and its fallback taken (Codex R2-7).
- **REVIEWLOCK1's census, widened**: every read kind's flags asserted; `tools/optional-locks.mjs` refusing a worktree `diff` without
  `diff.autoRefreshIndex=false` (Fable B1); a source census refusing git started under `Trees/GitReads/` except through `Launch`.
- `ReviewBaseTests` and `TreeUpdateTests`, unchanged, prove the range's semantics did not move.

### 10.2 Real git, the Process half (`Parallel=Git`)

- **No lock**: REVIEWLOCK1's `ReviewLockTests`, run against the reader's nodes.
- **Configured programs**: a textconv and an external diff configured, neither runs on an object read; `blame` given only
  `--no-textconv`; a marker-writing merge driver defined in the repository's configuration with a committed attribute, with
  `info/attributes`, with a global attributes file, and as `merge.default`: M and the preview refused before starting, the marker
  never written; a committed `merge=union` giving the merged base plain git gives (Codex 10, R2-6).
- **The observation**: drops on a loose ref write, `pack-refs`, a `HEAD` move, a linked tree's `HEAD` move, a `worktree add`, a stash drop
  of an older entry; **a ref moved while a held stand-in `for-each-ref` is still answering** is not kept (Fable B2); a ref rewritten at the
  same length within the racy window is not kept (Codex 11); **a nested ref (`refs/heads/team/topic`) moved in a repository past the
  watch's cap**: nothing was kept, and the next use reads R again and sees it (Codex R2-4).
- **Context**: two linked trees at the same ids with different `.gitattributes` give **equal** object answers under `--attr-source` and
  share one kept answer, while their worktree reads differ and are never shared (Codex R2-17); a global attributes file, an include and
  `config.worktree` each changed re-read C and move the id where the answer changed; a repository with `refs/replace/` or `shallow`
  keeps nothing (Codex 12).
- **Partial clone**: §5.3's fixture, a blob demanded and refused, the promisor asked nothing (Codex 13, R2-7).
- **The display addition**: §6.2's table, a fixture per row (ASCII, UTF-8 with a non-ASCII path, CRLF, no trailing newline, empty, a NUL,
  Latin-1, an attribute with a filter, `eol`, a link, a nested repository, over 200 KiB, past the budget, a file growing during the read),
  the exact subset equal to the oracle (Codex 17, Fable N16).
- **The watch**: a write under a watched tree drops the kept answer; a write inside an ignored folder that holds a tracked file is heard;
  a watch released or lost makes the next reopen pay the validation read, **and a new untracked file appearing while it was lost makes
  that read include A** (Codex 15, R2-2); **`git add -f` of a file in an excluded ignored folder, then an edit of it**: the index move
  revoked the exclusion, E₂ found the folder tracked, and the edit drops the kept answer; **an index-only change** (`git add` of an
  already-edited file) publishes `diff` and an idle open page re-reads (Codex R2-9).
- **A pruned generated tree** recomputed, not answered as absent (Codex 19).

Per the person's standing instruction on this machine, a row runs its own classes filtered and the censuses; the whole halves run in CI
after the merge.

### 10.3 The bench and the window

- **The bench**, reported, never failing: `daoris-driver git review <session> --time` over the three cases on this repository (368 files;
  the same with 50 untracked made by the bench in `_fixtures/`; 1,771), at least ten runs each: median and p90 of wall, depth, starts, and
  per start the gate wait, creation and run, with the machine's load beside. Before: the brief's numbers, through the same verb once 1a-i
  lands its door on today's chain. After: §3.4's targets. Written to the row's fix-log entry with the sha.
- **The window** (GITAPP1i, the owner's): Review opened from the press, timed to first paint by `review.painted`; the driver's
  `review.read` (`session`, `ms`, `depth`, `starts` and each budget's, `waitedMs`, `createdMs`, `files`, `untracked`, `kept`, `head`,
  no path). Three opens
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
| Review's head, a kept answer | Read at {time} · HEAD {id} | 读取于 {time} · HEAD {id} | 1a-v |
| Review's head, a read that moved | Changed while it was read: HEAD {from} → {to} | 读取期间已变化：HEAD {from} → {to} | 1a-v |
| Review's foot while its reads run | Reading… | 正在读取… | 1a-v |
| A file still changing | This file changed while it was read | 读取时该文件仍在变化 | 1a-v |
| An untracked file outside the exact set | Empty file · Not UTF-8 · Shown as stored, not as git would convert it · A link | 空文件 · 非 UTF-8 · 按存储内容显示，未按 git 转换 · 链接 | 1a-v |
| Review's head, no merged base | This repository defines a merge driver, which Daoris does not run | 此仓库定义了合并驱动，Daoris 不会运行它 | 1a-v |
| A commit's page | Commit · Parents · Held by · Made by | 提交 · 父提交 · 包含于 · 由谁创建 | 1d-i |
| Diff views | Inline · Side by side · Word diff · Ignore whitespace | 内联 · 并排 · 按词对比 · 忽略空白 | 1d-i |
| LFS | A file kept by LFS ({size}) | 由 LFS 保存的文件（{size}） | 1d-i |
| A file row | Blame · History · Before this change | 逐行追溯 · 历史 · 此更改之前 | 1d-ii |
| Compare | Compare with… | 对比… | 1d-ii |
| Search | Search messages, authors, paths · Search content (slow) | 搜索说明、作者、路径 · 搜索内容（较慢） | 1d-ii, 1h |
| Branches tab | Stashes · Tags · Remotes · Worktrees · Reflog | 贮藏 · 标签 · 远程仓库 · 工作树 · 引用日志 | 1d-iii |
| Review's foot, the conflict preview | Would conflict here in {n} files / Merges without conflict here / Not previewed: this repository defines a merge driver | 此处将在 {n} 个文件中冲突 / 此处可无冲突合并 / 未预览：此仓库定义了合并驱动 | 1d-iii |
| History's head | To speed this up in your own git: {command} | 如需在你自己的 git 中加速：{command} | 1h |

The acts' surfaces are GITAPP2's (`docs/2026-10-23-git-writes-design.md` §5). *Submodule* (子模块) and *merge driver* (合并驱动) are
the words D116's check settles first: git's own catalogue and the glossary's neighbours are read then. Stories at 1546 and 680 px in both themes and both languages, and UX6a's counter, hold each screen.

## 12. The rows (Q10)

Each row lands alone: its own proof, its terminal verb, bridge, Ask Daoris kind and translations within it (Codex 29, Fable N14).

**Round 3 splits round 2's 1a-i so the first row lands alone** (the quest: "split the rows so the first lands alone"): it changes no
behaviour, adds the seam and the door everything after is measured through, and its proof needs nothing later. Round 2's 1a-ii is
now 1a-v. The act rows (round 2's 1e, 1f, 1g-i to 1g-iv) are GITAPP2's (D204); 1d-iv is deferred (§4.3).

1. **REVIEWLOCK1** (landed on `main` at `808879b72`, after this tree's base). §1.1's rule and census. Nothing in this design rebuilds it.
2. **GITAPP1a-i — the launch seam and the bench door, on today's chain** (driver; after REVIEWLOCK1; **lands first and alone**). One
   `Launch(kind, arguments, budget)` beneath `RunGitAsync`, `ReadGitAsync` and `GitBytesAsync` (`WorkingTree.cs:953,1020,1093,1187`),
   classifying every start by read kind and budget and raising the one counting event, REVIEWLOCK1's flags applied there; §1.1's
   per-command controls asserted per kind; a source census refusing a git start under `Trees/` outside the seam; `daoris-driver git
   review <session> --time` over **today's** chain, printing each start's command, budget, gate wait, creation and run, and writing
   `review.read`. No answer changes. **Proof**: a census test over every read kind's flags; a test counting today's Review at
   `12 + I + M + Uᵣ` through the seam on a fixture of each path (§1's count), the line's guesses and `IsTopLevelAsync` included; the
   before-bench of §10.3 on the three cases, written to the fix log.
3. **GITAPP1a-ii — the reader, the observation and the context** (driver; after 1a-i). `GitReader`, `GitReaders`; the refs observation
   and its retention rule (§5.1) with the reader's ref watch (§5.4); the context read C, its inputs, floors and id (§5.3), its watch
   roots and deadline; the cache keyed by ids and context; joining with the mutable generation; the run places with one reserved
   (§4.2). Review still runs today's chain, reading R and C through the reader. **Proof**: §10.1's joining and run-place cases; §10.2's
   observation, context, configured-program (object reads) and partial-clone cases; the stand-in binaries for the floors.
4. **GITAPP1a-iii — Review's graph** (driver; after 1a-ii). `ReviewGraph`, its executor, Σ and the barrier V (§3.1, §5.2); the line
   from R; K′; the landed and gone subgraphs through the reader (`LandedReviews`, `LandedProofs`); M's merge-driver refusal (§1.1).
   Untracked files still as today (one start each) until 1a-iv. **Proof**: §10.1's generated census, line derivation, barrier and pinning
   cases; `ReviewBaseTests` and `TreeUpdateTests` unchanged; §10.2's merge-driver cases; the bench after.
5. **GITAPP1a-iv — untracked display additions** (driver; after 1a-iii). Nodes A and U (§6.2), none per untracked file. **Proof**:
   §10.2's display-addition fixtures against the oracle; the census's untracked paths at ≤ 10 cold; the 50-untracked bench.
6. **GITAPP1a-v — Review's page on the observation** (modules, web; after 1a-iv). `SESSION_DIFF` answering with its observation; the
   offer and HANDOFF_PLAN after the diff, timed apart; *Read at*, *Changed while it was read*, the merge-driver line; `review.read`
   from the route, `review.painted`; the page's re-read on every `updated` replaced by the 15 s validation read; the `git` scope and
   `GIT_CONTEXT` revision in the state hub and the id-keyed queries (§5.3). **Proof**: the route's counts per path; the page's stories
   in both languages; a mocked bridge proving the offer asked after the diff and the idle page re-read on a context change.
7. **GITAPP1b — Review follows the tree** (after 1a-v). §5.4's dependencies on the reader's watch; §6.3's tree watch for every attended
   tree, its index root, its exclusions proved and revoked, its budget; pre-read joining the page's refetch. **Proof**: a fake change
   source moving the line invalidates every review in the repository; a file written under the tree drops the kept answer; a lost
   watch makes the reopen pay 2-3 starts, A included where a new untracked file appeared; force-add-then-edit and an index-only change
   (§10.2); an idle reopen starts nothing; a commit shown ≤ 3 s on the bench.
8. **GITAPP1c — the judgements through the reader** (after 1a-ii). `help.*`, the tidy, the landing's plan, `GitBranches.ListAsync` and
   the offer take layout and ids from the reader; the offer's count bounded per eligible branch. **Proof**: PROC1a's start counts per
   judgement before and after.
9. **GITAPP1d-i — the commit page and diff views** (after 1a-v; takes GIT1i's commit page). A commit, side by side, word diff,
   whitespace, LFS pointers. **Proof**: `GitReadsTests` holding **each view's own start graph** (the commit page two dependent starts,
   `GitReads.cs:290-313`; a re-read with `-w` one) and its key with the context (Codex R2-17); stories and counters.
10. **GITAPP1d-ii — blame, a file's history, compare, search** (after 1d-i; takes GIT1f). As §7.1; file history paged as today.
    **Proof**: as 1d-i; blame with `blame.ignoreRevsFile` configured unchanged.
11. **GITAPP1d-iii — the Branches tab's reads and the conflict preview** (after 1d-i). Stashes, tags, remotes, worktrees, reflog; the
    conflict preview in Review's foot, refused where a merge driver is defined. **Proof**: as 1d-i; the stash list moving on an older
    drop; the preview's merge-driver cases (§10.2).
12. **GITAPP1h — history at scale** (after 1d-ii). The file history's kept id list, the pickaxe as a long read, submodules, the
    commands to copy. **Proof**: the third page of `TASKS.md`'s history no longer walking from the top (one walk counted); the pickaxe
    cancelled ends its git; two pickaxes in two repositories leave a short read a place.
13. **GITAPP1i — the install, seen** (the owner; after 1a-v, again after 1d-iii). §10.3's window timings and the screens in both
    languages. **Proof**: the fix-log entry and its screenshots.

1a-i makes Review measurable; 1a-ii gives it a reader that knows what it read; 1a-iii and 1a-iv make it fast; 1a-v puts it on the page;
1b keeps it fast while a session moves; 1c pays the same debt where the landing and the judgements wait; 1d draws the read half of D147
that is built and drawn nowhere.

## 13. Fit with D147, D113, D150 and D198

- **D147** is amended, never rewritten, in its read half only (round 3): point 6 and design §4.1 ("one process a view" becomes the
  reader: a held graph of bounded starts and an identified observation; still Tools' git and never a library). **§4.2 stands**, said
  exactly (Fable N10): a read writes no ref, configuration or file on disk of Daoris's own choosing and caches nothing on disk; objects
  only where `merge-tree --write-tree` writes them. Its points 2 and 3, its rejection of checking out in the person's checkout and its
  design §2.8 and §3.1 are what GITAPP2 (D204) would amend; until it is accepted they stand.
- **D113 §5 stands** (Fable N11): it rejects reading the person's checkout for a landed session's review, and the landed review still
  reads objects. Round 1 named it as amended; it is not.
- **D150 stands** in this design: the reads draw in the repository's existing tabs and Review. The Changes tab and the switch are
  GITAPP2's. No Git place returns to the activity bar; the branch, then the commit, stays the organising object (D55).
- **D198 stands**; a read passes no door. Its prerequisites for a push are GITAPP2's.
- **D51, D55, D109, D164, D173, D181 stand.**

## 14. Rejected, by proposal

**From Claude's**: ≤ 4 starts and its 1.5 s target (they drop REVIEWBASE1 and REVIEWBASE3); Review on the person's gate place (§4.2); a
held process for two idle minutes, eight across the machine (§4.3); the generation counter alone (§5.1 keeps a generation, never alone);
a pushed slice per review, this round (§3.3); a tree watch on every running session (§6.3 watches attended trees); the `commit-graph`
press (§9); the file history cursor (§9); a free three-way editor (D55).

*Round 3:* the rejections of a manual tree and of an idle session tree for the acts are GITAPP2's, and are kept here as they were
made.

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
hundreds of real untracked files; the observation on a reftable repository (none here); the tree watch under an agent's dependency
install; `--no-lazy-fetch` against a real partial clone here; `--attr-source` on Tools' git; the gate's `waitedMs` under a sweep's
fan-out; the reader's eight run places; the context read's cost and how often its deadline finds a move; an idle machine; **any git
older than Tools' 2.53**, whose floors are read from git's release notes and held by stand-ins, never run here. Every target is
provisional, set from the brief's and Fable's probes under the load they named; the rows measure the rest before they are accepted.

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

## 17. Round 3: Codex's round-2 read, answered for the read half

Checked by Claude Opus at `9900d32f7` with round 2 merged (its code lines unchanged since `fe4809b75` for every file cited), and git's
floors against the release notes and manual pages shipped with Tools' git 2.53 (`RelNotes/*.adoc`, `git.adoc`, `git-cat-file.adoc`,
`gitattributes.adoc`, `git-var.adoc` in its documentation folder), not memory. Every finding held as Codex stated it. Findings 11-16,
and the round-1 write findings Codex's table marks open or wrong, are GITAPP2's open questions (D204, its design §7), not answered
here, as the steward narrowed this round.

**Codex's round-2 findings** (`docs/2026-10-23-git-app-reads.md`, "Codex, round 2", §2-§3). 17 findings, 14 blocking.

| # | Finding | Checked at | Taken or answered | Where |
|---|---|---|---|---|
| R2-1 | The graph cannot generate its claimed paths: displaced base, grown base, the line's guesses, landed and gone | `ReviewRanges.cs:35-79,101-108`; `TreeContext.cs:75-79` (`CheckoutLine` is `DefaultBranchAsync`); `WorkingTree.cs:105-124,160-161`; `LineGuesses.cs:33-48`; `LandedReviews.cs:33-129`; `LandedProofs.cs:75-165`; `TreeGit.cs:16-33` | Taken: K′ (the grown base, speculative beside K), the line derived from R (three starts become none, held equal to `DefaultBranchAsync`), the branch from the `HEAD` file, Σ as a node; the landed and gone subgraphs with their own nodes, depths and ceilings; the paths **generated** from every combination of conditions, depth 2 claimed only for the qualified ordinary case; D202 point 1 says so | §3.1, §10.1 |
| R2-2 | Warm and watched counts omit A and the watch's own starts | design §3.1, §6.3 | Taken: four budgets (graph, context, watch, retry) reported with their total; A in every validation read where S lists an untracked file (depth 2 even with R kept); the watch's E₁ → E₂ counted apart and never on the graph's path; the lost-watch proof with a new untracked file | §3.1, §6.3, §10.2 |
| R2-3 | V is not a final barrier; joining ignores the mutable generation | `trees.ts:81-104` | Taken: Σ and V as nodes, V over every returned component, validating refs, `branch.oid`, the tree's watch generation and the context together; D₀ kept by its key, named the review's only where Σ selects it; mutable reads keyed with the tree, `h` and the generation at start; the page's replaced-read guard kept | §3.1, §4.1, §5.2 |
| R2-4 | The bounded refs fallback keeps an observation it cannot validate | design §5.1; `ChangeWatch.cs:189` | Taken, Codex's first way: **nothing is retained without a valid ref watch**; the 256-file bound and folder-time stand-in withdrawn; an unwatched repository reads R on every use; the nested-ref fixture past the cap | §5.1, §5.4, §10.2 |
| R2-5 | The context omits inputs and has no detection or delivery | `stateHub.ts:25-68` (no `git` scope); `git.ts:262-294` (`staleTime: Infinity`) | Taken: the inputs by category (binary, environment, every configuration file named or candidate, conditional includes, attribute files, shape), read by C (`git var -l`, `config --list --show-origin --show-scope -z`), the id a hash of what was read; heard by the reader's watch and outside roots with a deadline; a `git` scope and a `GIT_CONTEXT` revision in the page's keys; an idle-page test | §5.3, §5.4, §10.1, §12 row 6 |
| R2-6 | An empty attribute tree neither suppresses every driver nor keeps the semantics | `ReviewRanges.cs:84-95`; `gitattributes.adoc:70-77,1116-1128,1162-1170` | Taken: `--attr-source` withdrawn from `merge-tree`; the repository's own semantics kept (as REVIEWBASE3 runs today); `merge-tree` **not started** where the configuration defines any `merge.*.driver` (the only way git runs one) or `merge.renormalize` beside a filter, read from C; the preview qualified *here*; fixtures for committed, `info`, global and default-driver configurations | §1.1, §3.1, §7.1, §10.2 |
| R2-7 | The floors are wrong | `RelNotes/2.41.0.adoc:116`, `2.43.0.adoc:257`, `2.45.0.adoc:120`, `2.42.0.adoc:16`, `2.38.0.adoc:16`, `2.36.0.adoc:75`, `2.26.0.adoc:48`; `git.adoc:183-188,229-232` | Taken: `--attr-source` 2.41 (round 2 said 2.40); `--no-lazy-fetch` 2.45 (round 2 said 2.44), passed as a global option an older git refuses, the variable beside it; each floor checked against C's `git version`; below each, the stated fallback; a fixture that demands a missing blob; stand-in binaries for the lower versions, none being installed here | §5.3, §10.1, §10.2, §15 |
| R2-8 | The held reader's protocol would deadlock or desynchronise | `git-cat-file.adoc:73-79,143-175,294-297,341,362` | Answered by deferring, as the quest offers: no screen in this half needs a held reader; the protocol facts Codex found (header-only `info`, the body only after `contents`, no withdrawal once written, no commands under `--batch`, no diff options on `cat-file`) recorded for its own later design; row 1d-iv withdrawn from D202 | §4.3, §12 |
| R2-9 | Watch exclusions go unsafe when the index changes | design §6.3 | Taken: the tree's own index file watched (a linked tree's under `worktrees/<name>/`); its move drops the answer, publishes `diff`, and revokes every exclusion until E₂ proves it again; force-add-then-edit and index-only fixtures | §6.3, §5.4, §10.2 |
| R2-10 | Queue priority is described as a reservation | `ProcessStarts.cs:37-42` | Taken: one of the eight run places reserved for attended reads, the rest handed attended first among queued reads; round 2's "never queues behind" withdrawn | §4.2, §10.1 |
| R2-17 | Two acceptance expectations contradict the implementation | `GitReads.cs:290-313` | Taken: object reads in two linked trees under the same context are equal and share a kept answer, worktree reads differ; each view held to its own start graph, the commit page two dependent starts | §5.3, §7.1, §10.2, §12 row 9 |
| R2-11 to R2-16 | The checkout lock, ref transactions, rollback, stash drop, write outcomes, stash before resolve | — | **GITAPP2's open questions** (D204), not answered in this round | GITAPP2 §7 |

**The round-1 findings Codex's round-2 table marks open or wrong** (its §1).

| # | Its verdict | Answered by |
|---|---|---|
| Codex 5, dependency depth | open | R2-1: the generated graph |
| Codex 10, read effects and census | open | R2-6: `merge-tree` refused where a driver is defined; R2-8's per-command controls (`blame`, `cat-file`) |
| Codex 11, authoritative timestamp snapshot | open | R2-4: nothing kept without a valid watch |
| Codex 12, context and page caches | open | R2-5, R2-17 |
| Codex 13, partial clone | wrong | R2-7: the 2.45 floor, the option an older git refuses |
| Codex 14, mixed HEADs and joining | open | R2-3: the barrier and the mutable generation |
| Codex 15, ended trees and watch fallback | open | R2-2, R2-9 |
| Codex 16, invalidation and publication | open | R2-5: detection and delivery of the context; R2-9: the index |
| Codex 18, held process | wrong | R2-8: deferred, its protocol facts recorded |
| Codex 28, census and order | open | R2-1-3 and the split rows: one seam counting every start by budget, landing first and alone as 1a-i |
| Fable N12, the stamp's walk | wrong | R2-4: the bound withdrawn, nothing kept without the watch |
| Codex 20, 22, 24 | taken (with R2-11's remainder), wrong, open | GITAPP2's open questions (D204) |

**Kept as Codex's verdict keeps it**: the shared reader, ordinary-case speculation, separate action readiness (the offer after the
diff), exclusive session ownership. Its foundation list for the read half (the complete graph and census, validation and context
delivery, configured-program suppression) is what this round changes; checkout-writer exclusion and transaction and recovery protocols
are GITAPP2's.
