# Work in place, isolation a workflow's step, and a place that is not a repository (PLACEWORK1b)

**Proposed, for the owner's yes.** The direction is D254: a session works in its department's own place by default, one writer at a
time; isolation for parallel work is a workflow's step; git stays the file control wherever a place uses it; a place need not be a
git repository. Written by Claude Opus 5.5 on 2026-11-10 from main at `d085f3b023`, from the survey
(`docs/archive/2026-11-10-isolation-beyond-git-survey.md`, cited *survey*), whose first ranked candidate it details. Read with
`docs/areas/driving.md` (cited *driving rule n*), `docs/areas/workflows-review-landing.md` (*workflows rule n*),
`docs/areas/git-and-remotes.md`, the tree pool design (`docs/2026-10-26-tree-pool-design.md`, its rooms R5.3 and R5.4), the node kit
(D247) and PROCSTEPS1 (`docs/2026-11-09-process-steps-design.md`, landing as a composition). Paths are under
`src/Daoris.Desktop/Daoris.Desktop.Driver/` unless they start with `Modules/` (`src/Daoris.Desktop/Daoris.Desktop.Modules/`),
`Service/` (`src/Daoris.Service/Daoris.Service.Core/`) or `Web/` (`src/Daoris.Web/src/`). Nothing was built or run. D258 records the
two choices this design makes beyond D254 (§2.1, §4.3).

## 0. In short

- **In place, under a writer's lease.** A session works in its department's folder. The lease is the ledger's: one in-place record
  holds a place while its turn runs, and in a git place also while its last turn left uncommitted work of its own. It is taken and
  released in the ledger's own writes (the open, the move into a running state, the end), so nothing renews it on a clock; a crash is
  concluded by the sweep, which releases it. The queue behind it is the planner's order, read at each look. Daoris's own writes to a
  place (a landing's move of the checkout, a return, an apply) take the place's lock alone and ask the ledger inside it (§2).
- **A manifest at each turn's start and end.** Path, size, time and hash per file (git's status for a git place), kept under the home.
  The start's manifest against the last turn's end says what the person or another program changed meanwhile, and the session is told
  those paths. A text file's content goes to a per-place store, which the review's diff and a return's merge read (§2.6 to §2.9).
- **A place has a kind**: a git repository, a jj repository, a folder, or a system reached through its console. The registration
  records it; the brief, the access setup, the review and the workflow each read it; git's own capabilities stay git's (§3).
- **The isolation step** is the node kind `isolate`, the graph's entry before `claimed`: its options are the isolation kind (`auto`,
  `worktree`, `clone`, `copy`, `workspace`, `plan`), how many sessions at once (`at-once`) and `overlap`. A graph without it runs one
  session at a time in place, and a count above one without isolation cannot be said. The count lives on the step, not on the place
  (§4.3, D258). The return is the place kind's: the landing for git, the node kind `return` for a folder and a jj repository, `apply`
  for a system (§4).
- **A folder's return** merges file by file against the copy's manifest under the lease: one side changed is applied, text changed on
  both is merged three ways, anything else changed on both is the person's choice with both kept, and every write is conditioned on
  the live file's hash (§4.6).
- **A system** takes plan, review and apply: the session reads through the adapter's own read tools and submits a change set; the
  `apply` node runs it under the system's precondition, and a stale plan goes back to the work to be planned again (§5).
- **Migration**: the per-repository trees switch and the line policy's `overlap` become an `isolate` step in the workflows those
  repositories follow, converted once by the release's migration; trees, the pool, the landing and the tidy become what that step and
  its return run. Every shipped graph that lands gains the step, and a fresh home follows the shipped *Work in place* (§6).
- **Rows c to j** (§9): the lease and manifest for git in place; the step and the migration; a folder in place; a folder's copies and
  their return; the copy-on-write clone; systems; jj; the installed window.

## 1. What is asked, and what is true today

### 1.1 The direction

D254's five points: in place by default with one writer; isolation as a workflow's step whose count is that step's option, a place
setting weighed against it; any place a department, git one kind; git kept whole where a place uses it, the non-git system serving
the rest; designed before built, with today's git workflows one choice of the new step. The survey ranked the writer's lease with a
manifest, isolation providers by kind and a merged return first, path claims as a tier inside it, and plan, review and apply for every
console-reached place.

### 1.2 Where git is assumed today

| Where | File | What it assumes |
|---|---|---|
| The trees switch | `Settings/DriverConfig.cs:589-597` (`WithTrees`, `OpensOwnTree`), `driver.json`'s `trees` | A repository opts into a tree per session; read by the planner (`WorkOrders/Domain/RoundConsidering.cs:155`, `:249`), the start (`Loop/Run/Tree/GrowTree.cs:21`, `Loop/Run/Choose/CleanCheckout.cs:13`), a conversation (`Loop/Chat/ChatStarts.cs:11-12`), *Set up…* (`Setup/PlacePlan.cs:219-243`, `:438`) and the places' projection (`Workflows/PlaceProjection.cs:200`) |
| One at a time in the root | driving rule 12; `RoundConsidering.cs` | Without trees, one session per repository; the ledger's tree lock counts a parked record (`Service/Sessions/SessionReads.cs:216-228`, `SessionHolders.cs:25-32`) |
| A clean checkout | `CleanCheckout.cs`, `Workplace/WorkingTree.cs:173-195` | In the root, a start holds on any uncommitted path, and a folder that is not a repository fails `git status` and is held |
| A tree's open | `Workplace/TreeOpening.cs:46`, driving rule 20 | A root that is not a git top is refused |
| Registration | `Workplace/RegistrationWorld.cs:80-85` (`LineDeclarationReader`) | Declarations are read from the line's commit; a folder answers *not a git repository* |
| The start's lock | `Workplace/TreeLock.cs` | Keyed by the checkout's common git directory |
| The brief | `Agents/WorkplacePrompt.cs`, `Agents/TargetPrompt.cs` | A tree, its branch and its commits |
| Review | `Workplace/SessionReviews.cs:111-175` | A diff from the base commit; uncommitted work in the person's checkout is not the session's |
| Landing | `Workplace/TreeMerges.cs:59-63` | Merges only a tree Daoris opened; a root session never lands |
| The workflow | `Defaults/workflows/*.json` | Each of the eight shipped graphs holds a `land`; the fresh home follows `merge-after-accept` (workflows rule 1) |
| `overlap: wait` | `Settings/LinePolicyKey.cs:47`, `:105`, `:170`; `RoundConsidering.cs:145` | A line policy's key, read only where trees run side by side |

## 2. In place by default: the writer's lease and the manifest

### 2.1 What holds the lease

A place is held by **at most one in-place record**, and that record holds the lease:

- a record in a running state (`Starting`, `Running`) whose working folder is the place's own, a conversation's turn included;
- in a git place, also a record whose last turn ended (parked, waiting, completed waiting on a helper) leaving uncommitted changes of
  its own: paths its turn changed (the end's manifest against the start's, §2.8) that differ from `HEAD`. The end's move writes the
  mark `holds_place` on the record with those paths.

A folder has no record beside the place itself, so a folder's lease is a turn's alone. A git line that parks with nothing uncommitted
lets the place go; one that parks with uncommitted work keeps it, said on its row (*holds the checkout: 3 uncommitted paths of its
own*), until its next turn commits them, the person discards them (git's journaled discard, git rule 40), or the person presses *Let
others work here*, after which the next session is told those paths are another line's (§2.8). This is R5.3's rule for a room (a line
that will not go on with uncommitted work keeps its room) applied to the whole checkout.

The passes (helpers, partners, opinions, brainstorms) take no lease: they write nothing in the place, as a co-worker writes no tree of
its lead's. A run whose workflow isolates it takes no lease for its work (it holds a copy, §4) and takes the place's lock for its return.

**The ledger judges it** in the same write that opens a record or moves one into a running state (`Service/Sessions/SessionOpening.cs`,
`SessionAdvances.cs`), through one holder read beside today's (`SessionHolders.PlaceHolderAsync(department)`), as the tree lock is
judged now. So two starts in one look never both take it, and a driver and a terminal verb are judged alike. In-place records stop
being judged by the tree lock (`SessionReads.ActiveForAsync` matches a copy's path, never the place's root); the tree lock keeps
every isolated copy to one line, parked included.

*Weighed and rejected (D258):* a lease held per line until its work is done, which today's tree lock is for the root. A department
would wait on every question a session asks the person, and a folder has no unrecorded state to protect between turns. A timed lease
renewed by a heartbeat was rejected too: every holder is a record whose state the driver already moves, so a clock would add a second
way to say a session stopped.

### 2.2 Renewal and release

Nothing renews it on a clock. The holder is the record's state and mark, and the driver already moves both:

- **Taken** by the open or the move into a running state, refused there with `place-busy` naming the holder.
- **Released** by the move out of a running state (an end, a park, a wait, a stop) in that same write, where no `holds_place` mark is
  written, and by the move that clears the mark (a turn that committed, a discard, *Let others work here*).
- **The holder's liveness** is its process's lineage (`Loop/Session/LineageBrackets.cs`): a record in a running state whose process
  has gone is concluded by the sweep as every cut-off run is (driving rule 26), and its conclusion releases the lease.

### 2.3 The queue behind it

No queue is stored. At each look the snapshot carries every place's holder and every return or apply waiting on a place. The planner
sits each other in-place start with a new `SittingFact.Place` (*`s-1a2b` is working in reports; this starts when its turn ends*), in
driving rule 10's order: words waiting first, then oldest open first. A due return or apply goes before a start: the planner sits
in-place starts while one waits on that place, since it is short and makes the place current for the next turn. The cap (driving
rule 11) is unchanged and bounds every place.

### 2.4 A crash

A driver that dies leaves the holder's record in a running state. On the next start the sweep reads its lineage: a dead process is
concluded cut off, and the conclusion first reads the end's manifest, so the turn's changes are attributed to it and a git line's
uncommitted paths are marked, then releases. A process that outlived the driver keeps the lease until it ends, as it keeps its record.
The carry-on goes back in place in its turn order (driving rule 26), its start's manifest compared with that end.

### 2.5 Daoris's own writes to a place

`TreeLock` stays the short lock of Daoris's own acts and becomes the place's: keyed by the common git directory for a git place (as
now), by the folder's full path for a folder and jj place, by the system's id for a system (`TreeLock.Key`). Every act that writes the
place's files takes it alone and asks the ledger inside it whether a turn holds the place, which is TreeLock's documented pattern for a
replay (`TreeLock.cs:13-17`): a landing's fast-forward of a checkout that has the line out, a line's pull, a promotion, a folder's
return (§4.6) and a system's apply (§5.3). Where a turn holds it, the act waits as a node wait (`place-busy`) and the planner holds
in-place starts behind it (§2.3). A landing that moves only a ref writes no file of the place and takes no lease. Another home on this
machine does not see this home's ledger: a git place's first lease writes a marker in its common git directory naming the home, as
R5.3's break 8 does, and every other home refuses an in-place start there; a folder takes no marker, and the limit is said on its row.

### 2.6 The manifest: what it records, and where

A **manifest** (`RunManifest`) is read at each turn's start and end, under the lease for an in-place turn, and at an isolated copy's
making, which is its return's base.

- **A folder or jj place:** every file under the root but the place's excludes (§3.4): its path relative to the root with `/`, its
  size, its last write time in UTC, its SHA-256 where read, and the manifest's own moment.
- **A git place:** `HEAD`'s commit and branch, and `git status --porcelain=v2 -z --untracked-files=all` (ignored paths never), each
  listed path with its working content's SHA-256. Git's index holds the rest: every path not listed is `HEAD`'s.
- **A system:** none; its plan names the versions it read (§5.1).

It is kept at `<home>/work/places/<workspace>/<department>/manifests/<record>-<start|end>.jsonl.gz`. The session record keeps the two
ids as machine-local facts beside its tree (`SessionKeeps`); no payload carries them. The keeping pass keeps a place's newest 20 and
every manifest a review, a waiting return or a line still open names.

### 2.7 Its cost on a large folder

A manifest is a stat walk (`FileSystemEnumerable`, which opens no file) and hashes only the paths whose size or time moved since the
place's last manifest, or whose time lies within two seconds of it (git's racy-clean rule). A place's first manifest hashes every file;
it is read when the folder is added, shown on its row as it goes, never at a start. Row e measures the walk and the hashes on a
bench folder of 100,000 files and 20 GB and sets two bounds as machine settings, proposed at 250,000 files and five minutes for a first
manifest. A place past either is said too large to follow: it still works in place under the lease, its session is told nothing of
what changed meanwhile, and it takes no copy and no return (§4.4). A git place costs git's status, whose index already caches stat
data, plus the hash of each listed path.

### 2.8 The person's edits meanwhile, and what the session is told

- **Between turns.** A turn's start manifest against the place's last end manifest, of any line, gives the paths added, changed and
  removed outside every session: the person's or another program's. They are kept on the run as `outside`.
- **During a turn.** The end against the start is the turn's change set. A path the harness's own file tools named (the run's tool
  events, where its door reports them) is the session's; any other changed path is *changed while it ran*, shown unattributed on the
  review, since the lease cannot hold the person's own editor.
- **What the session is told**, in its brief (§3.3): *Since the last session here these changed outside Daoris: …* (at most 30 paths,
  then a count), with *read a file again before you change it, and keep what the person changed*. In a git place, also the uncommitted
  paths that are not its own (the person's, or a line's released by *Let others work here*): *leave them out of your commits; commit
  your own by path*. Where the start found such paths, the harness is handed denies for `git add -A`, `git add .`, `git commit -a`
  and `git stash` in each spelling (`Agents/Permissions.cs`, as `no-push` takes push's); a harness that takes no rules is told only.
- **A write over a file being edited** in place is the last writer's. A harness whose edit tool refuses a file changed since its read
  (Claude Code's does) catches most of it; Daoris adds no check in place, and the folder's registration says so.

### 2.9 The content store

A manifest stores each text file whose hash is new to the place: a file of at most 1 MB with no NUL byte in its first 8,000 bytes
(git's test), at `<home>/work/places/<workspace>/<department>/blobs/<sha256>`. The review's diff (§3.5) and a return's three-way merge
(§4.6) read it; nothing else does. A place's store is bounded at 1 GB: past it new blobs are not stored, said, and the review shows
those paths without a diff. A blob goes when no kept manifest names it. A file that is not text is never stored: its review row and its
return use its hash and size alone.

## 3. A place

### 3.1 Its kinds

| Kind | Known by | File control | Isolation kinds | Its return |
|---|---|---|---|---|
| `git` | the root is a git checkout's top (`WorkingTree.IsTopLevelAsync`), a jj repository colocated with it included | git | `worktree`; `room` once TREEPOOL1's row 2 builds it | the landing (`land`, PROCSTEPS1's composition once built) |
| `jj` | `.jj` at the root and no git working tree there | jj | `workspace` | `return`, by jj (§4.5) |
| `folder` | any other folder | the manifest | `clone`, `copy` | `return`, by the manifest (§4.6) |
| `system` | no folder; an adapter plugin names it | the system's own preconditions | `plan` | `apply` (§5) |

A colocated jj repository is a git place: git's working tree is its record, so D254 point 4 keeps git there.

### 3.2 Registration when it is not a repository

`Registration` (`Service/Registry/Registry.cs:55`) gains `Kind` (`git`, `jj`, `folder`, `system`) and, for a system, `System`
(its adapter plugin and the system's own id there, never a path). A folder's and a jj place's root stays machine-local as a
repository's is. The kind is read at each follow from the root: a folder that becomes a git checkout becomes `git` at the next
follow, said; a git place whose root stops being one is held, said, never read as a folder.

- **Adding.** The Projects page's add offers *A folder* (the window's folder picker) and *A system* (a plugin that declares systems,
  §5.4), beside today's import; the terminal's is `daoris repository add <path>` and `daoris repository add --system <plugin> <id>`.
  The person names it, its workspace and its summary. A folder holding a `daoris.json` is read from disk (`FolderDeclarationReader`,
  new), since its working files are its only version; a git place's declarations are still read from its line (`LineDeclarationReader`).
- **What it is.** A folder or a system is registered, not adopted: it holds no doctrine of its own, and its brief and connector come on
  the wire, as for a registered repository with no manifest (the `Registration` remark on addressing). `daoris sync` writes nothing into
  a folder. Its memory is its own, as each registered repository's is.
- **Its workflow.** Adding a folder or a system chooses the shipped *Work in place* for it in the same receipt-kept Apply (a plan of
  one, as *Set up…* chooses), so a workspace's landing workflow never holds it (§3.6).
- **On the window**, the Projects view lists each department with its kind: *repository*, *folder* or *system* (the glossary gains
  `folder` 文件夹, `system` 系统 and `department` 部门, the word for any of them; the glossary's `place` stays Setup's).

### 3.3 What the brief reads

`Agents/PlacePrompt.cs` (new) says the place by kind and mode, and `TargetPrompt` composes it where it composes `WorkplacePrompt`'s
tree words today, which become its git isolated part:

- **git, in place**: the checkout and its branch; it is the one writer; commit as it goes by path; the paths that are not its own.
- **git, isolated**: today's words (its tree, its branch, what lands it).
- **folder, in place**: the folder; it is the one session writing there, and the person may edit beside it; what changed meanwhile
  (§2.8); no git.
- **folder or jj, isolated**: a copy, whose changes come back through a return; a file the person also changes comes to the person.
- **system**: it plans and never changes the system; its read tools; it submits one change set through `system_plan` (§5.1).

### 3.4 What the access setup reads

D184's posture stays per department. The tree guard (`Workplace/TreeGuard.cs`) bounds a session's writes to its working folder: the
place in place, the copy when isolated. Git's denies (`Agents/Permissions.cs`) are handed in a git place alone. A folder declares its
excludes on its registration, defaulting to editors' owner and lock files (`~$*`, `.~lock.*#`), `Thumbs.db`, `desktop.ini` and
`*.tmp`; the manifest and every copy pass them by. The files rules (`daoris.files.json` and the person's `files.json`, driving rule 22)
stay git's: they rule what git does not manage in a tree, and a folder's copy holds nothing the place does not. A system session holds
no write path to the system at all (§5.1).

### 3.5 What review reads

A git place's review is today's. A folder's and a jj place's is the run's change set from its manifests (`PlaceChanges`, new): each
path added, changed or removed with its size and time, a text path's diff from the store's blob to the file's present content, a path
that is not text a row with *Open*, and the paths changed while it ran marked unattributed. It goes out in the review's diff payload
(`Workplace/SessionReviews.cs`, `Modules/Bridge/Routes/TreesRoutes.cs`), so `Web/work/review/DiffPane.tsx` draws it with one change:
a row with no hunks. A system's review is its change set as the adapter renders it (§5.2).

### 3.6 What the workflow reads

Each kind declares the place kinds it serves (`INodeKind.Places`, every kind by default). `land`, `push`, `<plugin>/pull-request`,
`monitor`, `tidy`, `integrate`, `line`, `content.holds` and PROCSTEPS1's `rebase`, `resolve` and `merge` serve `git`; `return` serves
`folder` and `jj`; `apply` serves `system`. The bind refuses a version naming a kind its place does not serve
(`BindingProblem` `place-kind`), so a run of a folder under a workspace's landing workflow holds with a sentence, and the folder
chooses its own. A graph a folder follows ends at its last node (`end`), since `line` observes git's line. A `check` reads
`daoris.gates.json` from a folder's disk where it stands.

### 3.7 What only git offers, and where

A branch, a diff against a line, a history (log, blame, the graph), a push, a pull request, the line's integration, a rebase and its
resolution, recovery refs and the act journal. Each is a step or a page of a git place and is absent elsewhere, never an error: the Git
page and Review's history say *this department is a folder; history is git's* where a folder is open. None is a precondition of
driving a place.

## 4. The isolation step

### 4.1 `isolate`: its kind under the node kit

`Workflows/Kinds/Isolate/IsolateNode.cs` (new), `[NodeKind(NodeKinds.Isolate, NodeGroup.Work)]`, ahead of `claimed` in the picker:

- **Its place in a graph.** At most one; it is the graph's entry and is wired `after` straight into `claimed`. A graph with a `land` or
  a `return` holds one (`land-isolate`, `return-isolate`, refused at the save, `NodeGraphs.Problems` through the kind's own `Problems`);
  a graph with an `apply` may hold one of kind `plan` (§5).
- **Its reach** is observed: the start performs it (§4.4) before the record opens, so it is reached when the run's record names its
  copy, or, where a copy was bound out (§4.4), its words say the run worked in place. Its waits are `at-once` (the count is full) and
  `overlap` (§4.2), said under it on the run's chart.
- **The start reads it.** The planner reads, per department, the isolate step of the version the selection resolves now
  (`Workflows/Domain/WorkflowSelection.cs`) into the snapshot. A start carries that version, and `RunBinding` binds the version the
  start carries rather than the newest at the bind, so a save between the plan and the bind never gives a run a kind it did not start
  with. A run already bound keeps its own version's kind: a carry-on goes back into its copy (driving rule 26).
- **Its title** is *Work apart* (分开工作); the editor's words are the kit's, en and zh.

### 4.2 Its options

| Option | Values | Meaning |
|---|---|---|
| `kind` | `auto` (default), `worktree`, `clone`, `copy`, `workspace`, `plan` | `auto` takes the place kind's first that can make a copy (§4.4); a named kind its place does not serve holds the bind (`place-kind`) |
| `at-once` | `2` to `24`, or `any` (default) | How many of this place's sessions work apart at once; `any` is bounded by the machine's cap alone, as trees are today |
| `overlap` | `go` (default), `wait` | A first start whose declared paths meet earlier work not yet returned in the place waits for its return (today's `overlap: wait`, driving rule 14, moved here) |

### 4.3 The count: on the step, not a place setting

**Settled: the count is the step's option** (D258). A graph with no `isolate` runs one session at a time in place; a graph with one
says how many at once, counted over every session of that place working apart, whatever version bound it. The machine's cap bounds
every place.

*Weighed against:* a place setting beside the workflow (`driver.json`'s place rows, as `trees` is), with the step naming only the kind.
It would let a person keep one workflow for many places and give each its own count. It was rejected: a count above one on a place
whose workflow does not isolate would either be refused at a second door or would quietly mean nothing, and the place's parallelism
would be said in two records that can disagree (D228). A workflow is already chosen per place, so a place wanting another count saves a
copy of its workflow for itself, which the editor's *Save as for this place* does in one press.

### 4.4 The providers

One interface, shaped as the survey's hooks (make a folder, remove it): `Departments/Isolation/IIsolationProvider.cs` (new) with
`Kind`, `Serves(kind of place)`, `Refusal(department)` (why it cannot make a copy here, or none), `MakeAsync(department, line, from)`
returning the copy's folder and its base manifest, `EndedAsync(copy)` and `RemoveAsync(copy)`. `IsolationChoice` (new, pure) takes
`auto` to the first provider of the place's kind whose `Refusal` is none, in this order.

- **`worktree`** (`WorktreeIsolation`, git): today's open (`Workplace/SessionTrees.cs`, `TreeOpening.cs`), the files stage after it
  (driving rule 21), and TREEPOOL1's kept slots behind it where that design's rows switch them on. Its end is `EndJudgement`'s
  (driving rules 47 to 50) and its removal `TreeSetAside`'s.
- **`clone`** (`CloneIsolation`, folder): a copy-on-write clone, file by file: block cloning on ReFS (`FSCTL_DUPLICATE_EXTENTS_TO_FILE`,
  which Windows' copy engine uses there from 24H2, Dev Drive among it), `clonefile` on APFS, `FICLONE` on btrfs and XFS. Only on the
  place's own volume: its copies sit in the home's `work/copies/` where the home lies on that volume, else in a copies folder the person
  names once per volume (the machine setting `copiesFolders`). `Refusal` clones a probe file once per volume and remembers the answer.
  Its cost is near nothing on disk and grows with the count of files (survey), which row g measured
  (`docs/archive/2026-11-11-placework1g-clone-measurements.md`): about 5 KB a file on disk, 1.6 to 2.6 ms a file in time on a Dev Drive,
  refused at once on NTFS.
- **`copy`** (`CopyIsolation`, folder): a full copy into the home's `work/copies/`, bounded by the place's last manifest (the machine
  setting `copyBound`, proposed at 2 GB and 20,000 files). Over the bound it **waits instead**: the start takes the place's lease and
  works in place, said (`started.copy-bound`, *reports is 14 GB, past the copy bound of 2 GB: this session works in place when the
  place is free*); its `isolate` is reached as worked in place and its `return` as nothing to return.
- **`workspace`** (`JjIsolation`, jj): `jj workspace add`, then `jj workspace forget` and the folder's removal; only where Tools resolves
  `jj`.
- **`plan`** (`PlanIsolation`, system): makes no folder; the session's change set is its copy (§5.1).

A folder's or jj place's copy sits at `<copies>/<workspace>/<department>/c-<8 hex>` and is recorded as a tree's line path is, so
`LinePlaces` answers it.

### 4.5 The return

- **git:** the landing, as the graph composes it: `land` today, PROCSTEPS1's `rebase` and `merge` once built. This design adds no git
  return kind.
- **folder:** `Workflows/Kinds/Return/ReturnNode.cs` (new), performed, *Bring back* (带回), its door `ReturnDoor` (new) started once
  per node and tip off the pass, as the land's is. It merges by the manifest (§4.6). Its outcomes are `returned` and `nothing`; it waits
  on `place-busy` and on `both-changed` (the person's choice, §4.6).
- **jj:** the same kind, whose door rebases the workspace's change onto the parent of the place's working-copy commit as a change of its
  own; jj keeps a conflict in the change, which the person resolves in jj. Daoris never squashes into the person's working copy.

### 4.6 A folder's return, file by file

Under the place's lock taken alone (§2.5), with the copy's base manifest `B`, the copy's present manifest `C` and the place's present
manifest `P`, for each path changed in `C` or `P` against `B`:

1. **Changed in the copy alone:** written into the place.
2. **Changed in the place alone:** left.
3. **Changed on both to the same content:** nothing.
4. **Text changed on both** (both sides text and `B`'s blob in the store): merged three ways with `git merge-file` over the three
   contents in the return's folder under the home (`<home>/work/returns/<run>/`). Git is a tool here, not the place's file control: the
   managed git Tools always resolves. A clean merge is written; a conflicted one is case 5.
5. **Anything else changed on both** (not text, a delete against a change, a conflicted merge): **the person's**. The place keeps its
   version, the copy's is kept in the return's folder, and the node waits `both-changed` with the paths. Its card and the review offer per
   path *Keep yours*, *Take the session's* and *Keep both*, which writes the session's beside as `<name> (<line>)<ext>`, through the
   node door (`WORKFLOW_NODE`, as a `human` node's presses go). The node is `returned` once each path is answered.

**Every write is conditioned on the live hash.** The new content is written to a temporary file beside its target, the target is hashed
again, and the replace (`File.Replace`, or a move for a new file) runs only where that hash is still `B`'s (or the path still absent);
otherwise the path becomes case 5. A delete moves the file into the return's folder instead. Every file a return overwrites or deletes is
kept there first, 30 days, so nothing in the place is destroyed. A rename reads as a delete and an add, said.

### 4.7 The copy's end

The look's tidy (`Workplace/AutoTidies.cs`) asks each copy's provider `EndedAsync`: a worktree by `EndJudgement` as today, a folder's or
jj copy once its return is `returned` or `nothing`, or its line is discarded. `RemoveAsync` moves it aside into the home's `.tidied`, as
`TreeSetAside` does, and the keeping pass purges it. A copy whose return waits is kept.

## 5. A place reached through a console

### 5.1 Plan

The session never changes the system. Its start hands it the adapter's own read tools as one of its servers, run by the adapter plugin
with the system's read identity alone, and the connector's `system_plan` tool (`Service.Mcp`, beside `review_ready`), which submits a
**change set**: an ordered list of operations, each naming its resource, the change, and the version it read (an etag, a state serial,
the adapter's hash of the read). The change set is kept on the run (`<home>/workflows/runs/<run>/plan-<n>.json`). A graph with an
`isolate` of kind `plan` lets `at-once` sessions plan side by side, since a plan writes nothing; without one, one plans at a time.

### 5.2 Review

The review shows the change set as the adapter renders it (`system.render`: each resource before and after). The person's Accept and
send-back are a `review`'s, unchanged.

### 5.3 Apply, and a stale plan

`Workflows/Kinds/Apply/ApplyNode.cs` (new), performed, a plugin step as `<plugin>/pull-request` is (`Workflows/Kinds/PluginStep.cs`),
started under the place's lock taken alone, so one applier runs at a time. Its door hands the adapter the change set and the system's
write identity for that act alone, as the git design hands an API token per act (`docs/2026-11-05-git-design.md` §2.3).

- The adapter checks every operation's precondition first where the system can (`system.check`), then applies each under it (an
  `If-Match`, a state serial, a change set the system executes whole).
- **Stale**: a precondition the system refuses ends the apply with `stale`, naming the operations applied and the first refused. It is
  told to the work as a review's send-back is (`NodeRunKeeping`), with each stale resource's present version, and the line goes on and
  plans again from the system as it stands. The run's rounds bound how often (PROCSTEPS1 §2.9).
- `applied` reaches the node; a refusal that is not stale fails it, said in the adapter's words.

### 5.4 The adapter per system

An adapter is a plugin that declares `systems` in its manifest (each with its id, its name and its `preconditions`: `etag`, `serial`,
`changeset` or `none`) and answers four points on the plugin wire: `system.read`, `system.check`, `system.apply` and `system.render`,
plus the read server of §5.1. Its identity is held as the git design holds a host's API token, keyed by the system. The examples gain a
stand-in system (`examples/plugins/stand-in-system/`): an in-memory store with etags, the proof's adapter.

### 5.5 A system with no precondition

An adapter declaring `none` is served by the lease and a read-back: the apply reads each resource just before writing it and compares
it with the version the plan read, a difference being `stale`; it writes and reads back, and a read-back that differs is said
*changed while applying*. The department's row says it is unguarded.

## 6. Migration

### 6.1 What each part of today becomes

| Today | Becomes |
|---|---|
| `driver.json`'s `trees`, `WithTrees`, `OpensOwnTree`, `SET_TREES`, the verb `daoris driver trees` | An `isolate` step (`worktree`, `at-once: any`) in the workflow the repository follows; the key, the door and the verb are gone |
| Driving rule 12 (one at a time in the root) | The lease (§2.1), for every place without the step |
| `CleanCheckout`'s hold on a dirty root | The manifest's report to the session (§2.8); the stage is gone |
| A fresh tree per start (driving rule 20) | The `worktree` provider, made only where the step asks |
| The tree pool (TREEPOOL1) | The `worktree` provider's way to make a copy; its rooms the `room` kind once its row 2 builds them; its readers of `OpensOwnTree` read the step |
| The landing | A git place's return, unchanged, reached only after an `isolate` |
| The look's tidy | Each copy's end by its provider (§4.7); git's proof unchanged |
| The line policy's `overlap: wait` | The step's `overlap` option; the key goes from `linePolicies` and `workspaceLinePolicies` |
| A conversation's own-tree choice (`ownTree` on the start, `ChatStarts.Isolates`) | The conversation's choice of workflow (workflows rule 2's task choice), *Work apart* among the start pane's offers |
| The steward's `trees` act and Ask Daoris's `trees` proposal | The steward's and Ask Daoris's workflow choice for a place |
| *Set up…*'s `SET_TREES` with every landing preset | The preset's shipped graph, which holds the step |
| The eight shipped graphs, each with a `land`; the fresh home's `merge-after-accept` | Each gains an `isolate` entry; a ninth, `in-place` (*Work in place*: the work, then your review), is shipped, and a fresh home follows it |

### 6.2 Converting a home

One release step, `Transitional/Migration/PlaceWorkStep.cs` (new), an entry of `HomeMigration.Steps` after `WorkflowsStep`, copying
`driver.json` and every workflow file to the salvage first (D232 point 2):

1. For each saved workflow, the repositories following it are read from the selection, each with its `trees` and its effective
   `overlap`. Where every one of them had trees on and one `overlap`, the workflow gains a version with the step (`worktree`,
   `at-once: any`, that `overlap`) as its entry.
2. Where they differ, the workflow keeps its present version for those with trees off, and a copy with the step is saved for those with
   trees on (one copy per distinct `overlap`) and chosen as each repository's own (workflows rule 3, the repository's choice replacing
   its workspace's for that kind).
3. A repository with trees off following a graph with a `land`: it ran in its root, where `TreeMerges` refuses every landing, so the
   graph's steps from the `land` on never ran for it. A copy cut before the `land`, its `end` the last node kept, is saved and chosen
   for it.
4. `trees` and every `overlap` are removed from `driver.json`, and the step is recorded. No older reader is kept (D232); the launcher
   refuses an older build on a converted home.

### 6.3 Work under way

A run already bound keeps its version, so a line in a tree goes on in it, lands as today and is tidied as today. A record in the root
on a repository with trees off holds the lease from the first look after the step (§2.1), its uncommitted paths read by the sweep's
manifest as its own. A run bound to a version with a `land` and no `isolate` (a trees-on repository's run, bound before the step) is
read as one whose start isolated: its binding records the kind the start used (`worktree`), written by the step for every open run.

### 6.4 What goes in the same chunks (D228)

- Row c: `CleanCheckout` and its hold sentence; the root's records under the tree lock (`SessionReads.ActiveForAsync`'s root match).
- Row d: `trees` and every door onto it (`DriverConfig.WithTrees`, `OpensOwnTree`, `DriverConfigReader`'s and the writer's key,
  `DriverChoicesCommand`'s `trees` verb, `SET_TREES` in the modules and *Set up…*, `Web/agents/setup/PlaceDriving.tsx`'s switch and
  `Web/projects/setup/` rows, the steward's `trees` act in `Modules/Bridge/StewardTargets.cs`, `StewardRouteAct.cs` and
  `StewardActRows.cs`, `Service/Help/HelpSettingProposalWriter.cs`'s `trees`); `overlap` in `LinePolicyKey`; a conversation's
  `ownTree` (`ChatStarts`, `Modules/Bridge/Routes/ConversationPayloads.cs`, `Web/work/start/NewConversation.tsx`, `useNewStart.ts`);
  the area sentences each changes (driving rules 12, 14, 20; workflows rules 1 and 10).
- Row e: the *not a git repository* refusals at a start where the place is a folder.

## 7. What changes in the code

Test paths are under `src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/` unless they name another root.

| Unit | File | Change | Row |
|---|---|---|---|
| `SessionHolders`, `SessionOpening`, `SessionAdvances`, `SessionReads` | `Service/Sessions/` | `PlaceHolderAsync(department)`; the lease judged in the open and the move into a running state; `holds_place` written by an end's move and cleared by a commit, a discard or *Let others work here*; the tree lock matching copies alone | c |
| `Session`, `SessionSchema` | `Service/Sessions/Records/Session.cs`, `Service/Sessions/SessionSchema.cs` | The record's `holds_place` paths; the manifests' ids machine-local beside the tree | c |
| `RunManifest`, `ManifestReader`, `ManifestStore`, `ContentStore` | `Departments/Manifest/` (new) | The manifest by kind (§2.6), its stat cache and racy rule (§2.7), its files and keeping, the text store (§2.9) | c (git), e (folder) |
| `PlaceChangesSince` | `Departments/Manifest/` (new) | The `outside` paths between turns and the turn's own change set (§2.8) | c |
| `RoundConsidering`, `SittingFact`, `Snapshot` | `WorkOrders/Domain/` | Rule 12's branch reads the holder; `SittingFact.Place`; the snapshot's holders and waiting returns; the count and `overlap` read from the snapshot's isolate step in `PlaceCount` (new, `WorkOrders/Domain/PlaceCount.cs`), one line in `RoundConsidering` | c, d |
| `CleanCheckout` | `Loop/Run/Choose/CleanCheckout.cs` | Gone | c |
| `TreeLock` | `Workplace/TreeLock.cs` | `Key` by place kind (§2.5); the git marker in the common directory | c, e |
| `PlacePrompt` | `Agents/PlacePrompt.cs` (new); `Agents/TargetPrompt.cs`, `Agents/WorkplacePrompt.cs` | The place's words by kind and mode (§3.3); `WorkplacePrompt`'s tree words its git isolated part | c, e, h |
| `Permissions`, `SessionRules` | `Agents/Permissions.cs`, `Loop/Session/SessionRules.cs` | The denies of §2.8 where a git place's start found paths not its own | c |
| `IsolateNode`, `NodeKinds.Isolate` | `Workflows/Kinds/Isolate/` (new), `Workflows/Nodes/NodeContract.cs` | The kind, its options, its save rules and waits (§4.1, §4.2) | d |
| `INodeKind.Places` | `Workflows/Kinds/INodeKind.cs`, each git kind's `<Kind>Node.cs` | The place kinds a kind serves; the bind's `place-kind` (`Workflows/Domain/BindingProblem.cs`) | d, e |
| `GrowTree`, `RunBinding`, `StartIn` | `Loop/Run/Tree/GrowTree.cs`, `Loop/Run/Placed/RunBinding.cs`, `Workplace/StartIn.cs` | The tree stage reads the start's isolate step and asks its provider; the binding binds the version the start carries | d |
| `IIsolationProvider`, `IsolationChoice`, `WorktreeIsolation` | `Departments/Isolation/` (new) | The interface and the git provider over `SessionTrees` | d |
| `DriverConfig`, `DriverConfigReader`, `DriverConfigWriter`, `DriverChoicesCommand` | `Settings/` | `trees` gone (§6.4) | d |
| `LinePolicyKey`, `LandingIntegrate` | `Settings/LinePolicyKey.cs`, `Settings/LandingIntegrate.cs` | `overlap` gone | d |
| `PlacePlan`, `SetupPresets`, `PlaceProjection` | `Setup/PlacePlan.cs`, `Workflows/Domain/SetupPresets.cs`, `Workflows/PlaceProjection.cs` | No `SET_TREES`; a preset's graph holds the step; the projection reads the step | d |
| `ChatStarts`, the conversation's start | `Loop/Chat/ChatStarts.cs`, `Modules/Bridge/Routes/ConversationPayloads.cs`, `Web/work/start/` | The conversation's workflow choice in place of `ownTree` | d |
| The steward's and Ask Daoris's doors | `Modules/Bridge/StewardTargets.cs`, `StewardRouteAct.cs`, `StewardActRows.cs`; `Service/Help/HelpSettingProposalWriter.cs` | `trees` gone; the workflow act serves | d |
| Shipped graphs and the seed | `Defaults/workflows/*.json`, `Defaults/workflows/in-place.json` (new), `Workflows/HomeSeed.cs` | The step in each graph with a `land`; *Work in place*; the fresh home's default | d |
| `PlaceWorkStep` | `Transitional/Migration/PlaceWorkStep.cs` (new), `HomeMigration.Steps` | §6.2 | d |
| The page's place rows | `Web/agents/setup/PlaceDriving.tsx`, `Web/projects/setup/RepositorySetup.tsx`, `setupRows.tsx`, `setupSaid.ts` | The switch gone; the place's workflow names its step and count | d |
| `Registration`, `RegistrationStore`, `RegistryImport` | `Service/Registry/Registry.cs`, `RegistrationStore.cs`, `RegistryImport.cs` | `Kind` and `System`; the import proposes a folder's kind | e, h |
| `FolderDeclarationReader`, the add | `Workplace/RegistrationWorld.cs` beside `LineDeclarationReader`; `Modules/Bridge/Routes/RegistryPersonRoutes.cs`; `Web/projects/` | A folder's `daoris.json` from disk; *A folder* and *A system* on the add; `daoris repository add` | e, h |
| `PlaceChanges` | `Departments/Review/PlaceChanges.cs` (new); `Workplace/SessionReviews.cs`, `Modules/Bridge/Routes/TreesRoutes.cs`, `Web/work/review/DiffPane.tsx`, `diff.ts` | A folder's change set in the review's payload; a row with no hunks | e |
| `TreeGuard` | `Workplace/TreeGuard.cs` | Its root the working folder of any kind | e |
| `CopyIsolation`, `CloneIsolation`, `JjIsolation`, `PlanIsolation` | `Departments/Isolation/` (new) | §4.4 | f, g, i, h |
| `ReturnNode`, `ReturnDoor`, `FolderReturn`, `ReturnRecords` | `Workflows/Kinds/Return/` (new), `Departments/Return/FolderReturn.cs` (new) | §4.5, §4.6 | f, i |
| `AutoTidies` | `Workplace/AutoTidies.cs` | Each copy's end by its provider | f |
| `ApplyNode`, `SystemPlans`, `PluginSystems` | `Workflows/Kinds/Apply/` (new), `Departments/Systems/` (new), `Plugins/` | §5; the manifest's `systems`; the plugin wire's four points | h |
| `system_plan` | `src/Daoris.Service/Daoris.Service.Mcp/` | The connector's submit | h |
| The stand-in system | `examples/plugins/stand-in-system/` (new) | The proof's adapter | h |
| Machine settings | `Settings/List/MachineSettings.cs` | `copyBound`, `copiesFolders`, the manifest's two bounds, each with its doors | e, f, g |
| Words | `Web/locales/en/*.json`, `Web/locales/zh/*.json`, `Web/locales/glossary.json` | The step, the return, the apply, the kinds, the lease's sittings, en and zh | c to h |

`Departments/` is a new subdomain of the driver, beside `Workplace/`, which stays git's; the structure gate's module map gains it in
row c.

## 8. Rejected

- **A place setting for the count** (§4.3, D258): two records for one fact, and a count that a workflow without isolation cannot honour.
- **A lease held per line until done, or renewed on a clock** (§2.1, D258): a department waits on every question to the person; a
  clock is a second way to say a session stopped.
- **The lease in a table of the desktop store**, as the pool's claims are. The ledger already judges every open and move, so a second
  holder record would have to be kept in step with it; the lease in the ledger is taken in the same write as the record moves.
- **A git repository kept behind every folder** (survey candidate 3): large files swell it, documents still do not merge, and a hidden
  repository under the person's folder confuses sync clients and the person. The content store holds text alone, under the home.
- **Path claims as the in-place default** (survey candidate 2): shells, builds and formatters write outside their claims, and an agent
  predicts its paths poorly. For git they are TREEPOOL1's rooms, an isolation kind.
- **One return kind for every place**, `land` included. A git landing's methods, deliveries and integration mean nothing to a folder,
  and a folder's merge means nothing to git; the return is the place kind's.
- **Both versions of a file written beside each other in the place at once.** It leaves the person's folder with files nobody chose; the
  copy's version waits in the home until the person picks.
- **A console place changed by its session directly, under the lease.** A system has no copy to keep and no undo; a reviewed change set
  under the system's own precondition is the one way, and the lease serves only a system with none.
- **Keeping the trees switch beside the step**, or reading `OpensOwnTree` as a fallback where a workflow says nothing (D228).

## 9. The rows, in build order

Each row lands with its proof red first, its area sentences rewritten in place, and nothing it supersedes left standing (§6.4). Row c
needs nothing; d follows c; e follows c; f follows d and e; g follows f; h follows d; i follows f; j is last. TREEPOOL1's rows d to h
read the step once d lands. PROCSTEPS1's conversion of `land` composes after an `isolate` unchanged.

**PLACEWORK1c — the lease and the manifest, in place, in a git repository** (service, driver, web). The ledger's lease (§2.1 to §2.4),
the git manifest and its store (§2.6, §2.9), what the session is told (§2.8), `SittingFact.Place`, TreeLock's question to the ledger
(§2.5); `CleanCheckout` gone. The person sees, in a repository without trees: a second quest sitting *`s-…` is working in reports;
this starts when its turn ends*; a parked session with nothing uncommitted no longer holding it; one with uncommitted work saying so
with *Let others work here*; their own edit between turns named in the next session's first words. Proof: the ledger refusing a second
in-place open and admitting it after the park; `holds_place` written and cleared by a commit, a discard and the press; a cut-off run's
lease released by the sweep with its paths marked; a landing's fast-forward waiting on a running turn; the denies handed where the
person's paths stood; the start's manifest finding an edit made between turns; `HomeSizeBench`'s look held to its budget with the holders
in the snapshot.

**PLACEWORK1d — the isolation step, and the trees switch converted** (driver, modules, service, web). `isolate` with its options, waits
and save rules; `INodeKind.Places` for the git kinds; the start and the binding reading the step; `WorktreeIsolation`; the shipped
graphs and *Work in place*; the migration step; every door of §6.4's row d gone. The person sees the editor's *Work apart* step with
*At once*, a third start sitting *2 work apart in reports at once*, and a repository that had trees before the update working in trees
with its workflow showing the step. Proof: the step converting a home whose workflow's repositories all had trees, one where they
differ (the copy chosen per repository), one with trees off under a `land` (the cut copy), and one with `overlap: wait`, each read back;
a save refusing `land-isolate`; `at-once: 2` holding the third start; `overlap: wait` read from the step; a run bound before the step
going on in its tree and landing; a conversation started with *Work apart*; a fresh home following *Work in place*; a save between the
plan and the bind not changing the run's kind.

**PLACEWORK1e — a folder, worked in place** (service, driver, modules, web). `Kind` on the registration and the add (page and verb);
the folder's manifest and content store; `PlacePrompt`'s folder words; `PlaceChanges` on Review; `place-kind` at the bind; TreeGuard's
root; *Work in place* chosen at the add; the glossary's words. The person sees a folder of documents added on Projects, a quest done in
it, and the changed files on Review with a text file's diff. Proof: the add choosing *Work in place* in one receipt; the bench folder's
first and second manifests timed, the bounds set from them; a folder past them said too large to follow and still worked in place; a
landing workflow holding a folder's run `place-kind`; the excludes passed by; a folder that becomes a git checkout read `git` at the next
follow.

**PLACEWORK1f — a folder's copies and their return** (driver, web). `CopyIsolation` with its bound, `return`, `FolderReturn`, the
`both-changed` card, the copy's end. The person sees two quests in one folder at once, each in a copy, brought back; a file both changed
waiting with *Keep yours*, *Take the session's* and *Keep both*. Proof: §4.6's five cases each; a live hash moved between the check and
the replace sending that path to the person; a delete kept in the return's folder; a return waiting on a running turn and a start held
behind a due return; a place over `copyBound` working in place, said; the copy removed after `returned` and kept while it waits.

**PLACEWORK1g — the copy-on-write clone** (driver). `CloneIsolation`, `copiesFolders`, `auto` choosing it where the probe clones. The
person sees a large folder's sessions work apart with no copy's wait. Proof: a process test on a ReFS volume the runner makes (a Dev
Drive VHD), said skipped where none can be made; clone time per 10,000 files recorded; the probe's refusal on NTFS taking `copy`.

**PLACEWORK1h — a system, planned, reviewed and applied** (driver, service, plugins, web). `Kind: system` and *A system* on the add;
`PluginSystems`; `PlanIsolation`; `system_plan`; `apply`; the stand-in system. The person sees a change set on Review as before and
after, applied after Accept, and a stale plan sent back and planned again. Proof: the stand-in's `node --test`; a run whose etag moved
between the plan and the apply going back with the present versions and applying on its second plan; a `none` system's read-back; two
sessions planning at once under `plan` and their applies one at a time.

**PLACEWORK1i — a jj repository's workspaces** (driver). `JjIsolation` and the jj return, built only after a measurement on Windows of
`jj workspace add` and the rebase on the bench folder. The person sees a jj repository's sessions work apart and come back as changes
of their own. Proof: a process test where `jj` resolves, said skipped where it does not; a conflict kept in the change.

**PLACEWORK1j — the installed window** (the steward, then the owner). The install's repositories with trees carried by the step, their
workflows showing it; a documents folder added and worked in place; a folder's two sessions brought back; the screens in English and
Chinese (the step, the sittings, the add, the return's card).

## 10. Not covered here

- The manifest's bounds and the clone's cost per file are row e's and row g's measurements, not numbers this design rests on.
- Each real system's adapter (a cloud console, a ticket tracker): its read server, its preconditions and its words are its plugin's.
- Formats that merge by structure (a spreadsheet by cell): no general tool exists (survey), so they are the person's; a format-aware
  merger would be a provider of its own.
- Two homes on one machine sharing a folder place see no lease of each other's (§2.5).
