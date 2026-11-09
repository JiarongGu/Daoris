# A repository's Daoris configuration read once and indexed (REPOCONFIG1)

**Proposed for both makers' reads before a decision is written** (2026-11-09). The owner finds reading a repository's Daoris
configuration slow and wants it read once and better indexed. This design measures every reader first (§1, §2, §3), then proposes
one store every reader passes (§4), and the rows that build it, each proved by the bench before and after (§5). Driver paths are
under `src/Daoris.Desktop/Daoris.Desktop.Driver/`. It builds on GITLIB1 (`docs/2026-11-09-git-library-design.md`): its stamp (§10.3),
the asking tree's context (`GitAskerContext`) and the refs' version (`CountedRefs`).

## 0. In short

- **A read at a commit is a git start, and a start is the whole cost.** Reading and parsing the four declarations of this repository
  from its checkout takes 1.4 ms; reading the same four blobs by id takes four starts and 1.3 s on this machine under its agents' load.
  Parsing is never the cost: the dearest parse (the lanes file's rooms) is 3.9 ms.
- **Nothing is read twice because it changed; it is read twice because no reader knows another read it.** Some 40 readers in the
  driver and the modules each read their own file their own way, and three keep a cache of their own (`StampedWorkflows` by write time
  and length, `TreeReadMemo.Kept.Declared` by the refs' version, `BacklogMerge`'s `_declared` for one flow). A landing reads its line's
  gates blob up to four times; the tidy reads it once per held tree after every move of the line; a watch's start reads every
  repository's manifest and lanes at its line, five starts each, 100 for a home of 20. A start runs the whole file analysis again
  (11 starts on this repository, 5.2 s under load) for parts its commit fixes.
- **One store**, `RepositoryDeclarations`: bytes and parse held by content (the git blob id, which a checkout's file is hashed to as
  well), three indexes from where to content (a commit's path, a line's tip, a checkout's file by its stat), the parsed views its
  readers ask by, persisted in the desktop store for a cold start and bounded by size. Every reader goes through it; the three caches
  go (D228).
- **Correct per tree and per commit by construction**: a checkout's file is its path's, a commit's file is its commit's, and only the
  content they resolve to is shared; which version a reader asks for stays the reader's contract and is named in §4.3.

## 1. The readers

Found by the declarations' names and the readers' doors (`Grep` over `src/`, tests left out), on main at `0116d3b02f`. *Checkout* is the
file on disk in a checkout or a session's tree; *commit* is the blob at a commit through git; *home* is a file of the Daoris home.

| File | Reader | From | Asked by |
|---|---|---|---|
| `daoris.json`, `daoris.lanes.json` | `LineDeclarationReader.ReadAsync` (`Workplace/RegistrationWorld.cs`), then `LineRegistration.Compose` | the line's tip: `rev-parse`, `ls-tree`, a blob each | the registration follow: once at a watch's start for every row with a root (`WatchBeside`'s `follow`), the `lines` pass for each line Daoris moved (`MovedLines`), the Repositories page's *Refresh* (`RegistryRoutes`), `TreesWords` |
| `daoris.json`, `daoris.lanes.json` | `RegistryOperations` | checkout | `daoris repository register`, import |
| `daoris.json` | `LayoutFacts.Read` | the line's tip | the set-up press and brief |
| `daoris.json` | `RegistryModule` | checkout, read and written | the Repositories page's declaration editor |
| `daoris.json` | `LoopGrounds` (`declaration`) | checkout, hashed | a pipe write's grounds |
| `daoris.json` | `RemoteSync` | checkout, unmodified or not | each look's sync, per joined repository |
| `daoris.json` | `BacklogMerge`, `UnionJoins` | commit | a landing's replay (row tables, the decisions folder) |
| `daoris.json`, `daoris.gates.json` | `OpinionPackets.RulesIn`, with `RepositoryIndexes.Find` | tree | each second opinion (`OpinionsLook`) |
| indexes beside `daoris.json` | `RepositoryIndexes.Find` | tree | a start's standing facts (`SessionTarget`), Context's blocks, a brainstorm |
| `daoris.files.json` | `RepositoryFilesReader.AtAsync` | commit | `StoreDependencyRules.ReadAsync` (a start's files stage; the pool's take, ignored sort, toolchain and finish), `FilesLooks` (the Files page, `daoris files`) |
| `daoris.files.json`, `daoris.lanes.json` | `RepositoryFilesReader.Read`, `LaneRooms.Read` | handed text | `RoomCheck` |
| the found layer | `FileAnalysis.ReadAsync`, `FoundFiles` | commit and tree; home | the files stage, the Files page, `FoundReads` (a registration, a line's move past a file it reads) |
| the person's layer | `PersonFiles.Read` | home | the files stage, the Files page, each permission judgement (`FileAllows`), the edits |
| `daoris.gates.json` | `HeavyGates.Read`, `Resolve`, `Marks` | checkout | `run-heavy`, `HeavyTold`, the Setup page's checks (`DriverRoutes`), each brief's checks (`ContextReviewWiring`) |
| `daoris.gates.json` | `TreeLocalInputs.Declared` | checkout | a start's files stage |
| `daoris.gates.json` | `IntegrationAcceptance.AcceptedTextAsync`, then `IntegrationDeclaration.Read` | the line's tip | the tidy per held tree (`TreeScratch`, kept in `TreeReadMemo`), a landing (`LineRules`, `IntegrationPromotes`), `IntegrateChoice` |
| `daoris.gates.json` | `GeneratedWriters.AcceptedAsync`, `BacklogMerge.DeclaredTablesAsync`, `IntegrationOwed`, `VerifyExecution` | commit | a landing's replay and resolve; the owed check; a heavy verify |
| `daoris.gates.json` | `LandedGates.ReadAsync`, `TextAsync`; `IntegrationAcceptance.TextAsync` | the line's tip | the Workflows page per repository (`WorkflowRoutes`), `PlaceWorld`, `WorkflowChoosing` at a verification's start |
| `daoris.gates.json` | `FileAnalysis` (`FileGit.ShowAsync`) | commit | inside every analysis |
| workflows | `WorkflowFiles` behind `StampedWorkflows`; `WorkflowStore.List` | home | the `nodes` pass (by stamp), the Workflows page's start, 11 call sites |
| per-repository settings | `DriverConfig.Load` (`driver.json`: lines, heavy mappings, checks, accepted blobs) | home | 136 call sites in 74 files, each a whole read and parse |
| registry rows | `ServiceClient.RegistryAsync`, `RegistrationsAsync` | the service | 79 call sites in 48 files, each a request |

The service reads a checkout's `daoris.json` and `daoris.lock` itself (`RepositoryLayout`, `DaorisLock`, `CheckoutFreshness`) when its
knowledge index refreshes; it is another process and keeps its own index, so it is outside this store (§4.6).

## 2. What each read costs

`RepoConfigBenchProcessTests` (driver tests, `Workplace/`), opt-in: `REPOCONFIG_BENCH=20 dotnet test
src/Daoris.Desktop/Daoris.Desktop.Driver.Tests --no-build --settings src/Daoris.Desktop/process.runsettings --filter
FullyQualifiedName~RepoConfigBenchProcessTests`. It times each reader on this repository (its four declarations: 3,977, 1,800,
19,549 and 11,025 bytes) and on a home of 20 repositories made under `_fixtures/repoconfig-bench/`, each holding the same four files.
*Cold* is the first ask, with its git starts counted at the process gate (`GitStartTally`); *warm* is the median of nine; *pass* is one
warm ask of each of the 20, summed. Measured 2026-11-09 on this machine under its agents' load, when one git start took 180 to 500 ms:
the starts are the figure that holds, the times are the load's.

| Reader | This: cold ms | This: warm ms | This: starts | 20: pass ms | 20: starts |
|---|---|---|---|---|---|
| stamp: the four files' write time and length | 0.67 | 0.03 | 0 | 0.7 | 0 |
| stamp: the asking tree's context (`GitAskerContext`) | 4.20 | 0.26 | 0 | 3.1 | 0 |
| stamp: the refs' version (`CountedRefs`) | 14.27 | 1.73 | 0 | 6.4 | 0 |
| disk: the four files read and parsed | 5.32 | 1.43 | 0 | 44.1 | 0 |
| git: the four blobs read by id | 1561.08 | 1328.69 | 4 | 29361.7 | 80 |
| `LineRegistration.Compose` (manifest and lanes, parsed) | 38.78 | 0.10 | 0 | 6.3 | 0 |
| `LaneRooms.Read` (lanes, parsed) | 58.03 | 3.87 | 0 | 24.9 | 0 |
| `RepositoryFilesReader.Read` (rule file, parsed) | 40.77 | 0.34 | 0 | 10.3 | 0 |
| `HeavyGates.Read` (checkout) | 4.06 | 0.16 | 0 | 7.1 | 0 |
| `HeavyGates.Marks` (checkout) | 6.02 | 0.16 | 0 | 9.5 | 0 |
| `TreeLocalInputs.Declared` (checkout) | 2.36 | 0.16 | 0 | 10.5 | 0 |
| `OpinionPackets.RulesIn` (checkout) | 18.25 | 2.21 | 0 | 14.7 | 0 |
| `RepositoryIndexes.Find` (checkout) | 2.24 | 1.10 | 0 | 5.9 | 0 |
| `PersonFiles.Read` (home) | 1.49 | 0.04 | 0 | 1.0 | 0 |
| `FoundFiles.Read` (home) | 1.61 | 0.04 | 0 | 0.8 | 0 |
| `LineDeclarationReader.ReadAsync` (line: manifest, lanes) | 936.54 | 928.08 | 5 | 29659.4 | 100 |
| `LandedGates.ReadAsync` and `TextAsync` (line: gates) | 8617.93 | 1753.41 | 4 | 23291.0 | 80 |
| `RepositoryFilesReader.AtAsync` (commit: rule file) | 245.67 | 500.47 | 1 | 5738.7 | 20 |
| `FileAnalysis.ReadAsync` (commit: the found layer) | 4776.18 | 4641.08 | 9 | 41966.1 | 100 |
| `StoreDependencyRules.ReadAsync` (a start's files stage) | 3022.66 | 5159.24 | 11 | 52953.2 | 120 |
| `FilesLooks.ReadAsync` (the Files page) | 12390.39 | 5854.49 | 10 | 30613.8 | 120 |

What it says:

- **Every start is the cost.** A blob is one start (`RepositoryFilesReader.AtAsync`); a line's file is four or five (is the folder a
  top, the tip, the listing, the blobs). The same four files from the checkout cost 1.4 ms, and their stat 0.03 ms.
- **The analysis is the dearest reader, and a start runs it whole.** It is 9 starts on this repository and 5 on a fixture (the tracked
  list, the ignored tops, the worktrees, the gates and each manifest it reads). A start's files stage (`StoreDependencyRules`) runs it
  at every start for the tops, the floor and the signatures; once a repository is registered its found layer is on file, and the
  found rules the analysis gives are not the ones taken.
  Of its starts only the ignored tops and the worktrees read the tree; the rest are fixed by the commit.
- **A parse is never the cost**: the dearest is the lanes file's rooms, 3.9 ms warm; the first parse of a kind pays the runtime's
  warm-up (39 to 58 ms) once a process.
- **The stamps are cheap**: a file's stat, the asking tree's context and the refs' version together under 2 ms warm.

## 3. Per look and per page

From the code (§1) and the bench (§2). *Again* is a read of a version the same flow or look has already read.

| Moment | Reads of a repository's configuration | Starts | Again |
|---|---|---|---|
| A look where nothing moved | the workflows by stamp (`nodes`); the tidy's declared output kept under the refs' version; `driver.json` at each door; the sync's manifest check per joined repository | 0 for the declarations | `driver.json`, read whole at every door |
| A watch's start (`follow`), 20 repositories | each row's manifest and lanes at its line, composed; the found layer judged | 100 | every one where nothing moved since the last run |
| The `lines` pass after a landing | the moved line's manifest and lanes; the found layer where the move touched a file it reads | 5, plus an analysis | none |
| The tidy after a line moved, N held trees | the accepted gates blob at the line's tip, once per tree (`TreeScratch`) | 2N | N − 1 |
| A start (`start`), to its spawn | the files stage: the analysis (its own read of the gates blob), the rule file at the head, the found and person layers, the checkout's gates for local inputs; the standing facts' indexes; each brief's checks read the checkout's gates again | 11 on this repository, 6 on a fixture | the whole analysis at every start of the same commit; the gates file, read three ways |
| A landing (squash or merge press) | the line's gates blob for line rules, generated writers, row tables and promotion; the manifest at the declaration's commit | 2 a read (a listing and a blob) | the gates blob, up to three times more |
| The Repositories page | registry rows (a request); per opened row the checks (`HeavyGates.Marks`) and the declaration (`RegistryModule`); *Refresh* is one follow | 0, or 5 on *Refresh* | the manifest, by every route that opens it |
| The Files page, one repository | the line's commit; the analysis; the rule file at that commit; the found and person layers | 10 on this repository, 6 on a fixture, and the line's | the whole analysis at every open of the same commit |
| The Workflows page, one repository | registry rows; the line; the gates blob at its tip and its text; the workflow list | 4, and the line's | the gates blob, twice where it changed |

## 4. The design

### 4.1 One store, by content

`RepositoryDeclarations` (`Workplace/Declarations/`) is the one door through which the driver and the modules read a repository's
declarations (`daoris.json`, `daoris.files.json`, `daoris.gates.json`, `daoris.lanes.json`, the indexes beside them) and the home's
layers over them (the person's and the found file layers, the workflows, `driver.json`). It holds four things:

1. **Content.** Each file's bytes and its parse, keyed by its git blob id. A blob read at a commit carries its id; a checkout's file
   is hashed to the id git would give it (`blob <length>\0` and the bytes, in the repository's hash). So a content is parsed once
   whichever tree, commit or repository holds it, and two trees holding the same file share one parse. Parsing is pure in the text,
   and every kind has one parse: the gates file's one read (`IntegrationDeclaration` widened to the heavy gates, the local inputs,
   the writers and the analysis's keys, which `HeavyGates`, `TreeLocalInputs`, `GeneratedWriters` and `FileAnalysis` each parse
   apart today, five parsers of one file), the manifest's, the lanes file's rooms and problems, the rule file's rules. The
   analysis's inputs fixed by a commit (its tracked list, the gates, the manifests it reads) are content too, kept by the commit.
2. **A commit's path.** `(repository, asker's context, commit, path) → blob id or absent`. A commit id names a fixed tree, so the
   entry is true for as long as the repository's history stamp (GITLIB1 §10.3: `refs/replace/`, `info/grafts`, `shallow`, the git
   and its configuration) stands; the context (`GitAskerContext`) keeps a tree with replacements off from one with them on.
3. **A line's tip.** `(repository, line) → commit`, read from the ref files with no start where they are files (`CountedRefs`), and
   true while the refs' version stands; a reftable repository asks git, as today.
4. **A checkout's file.** `(full path) → (stat, blob id)`, the stat being the length, the write time and the file's id, read at
   every ask (0.03 ms for the four). A stat that moved hashes the bytes again; a new id parses, a known one does not. A stat younger
   than two seconds is never trusted, as git's index treats a racy entry, so two writes inside the clock's grain are not one.

Over them sit the **views** the readers ask by, each computed once per tuple of the contents it rests on and kept with them: a
repository's lanes and rooms, its gates (heavy, light, local inputs, writers, scratch, row tables, line rules), its manifest's
domain, documents and rooms; a tree's effective file rules (`FileRules.Of`, keyed by the rule file's, the person layer's and the
found layer's ids and the floor), so a path's rule and a kind's entries are a lookup; a workflow by id and the list; a repository's
settings in `driver.json`.

### 4.2 Persisted and bounded

In memory the contents are bounded by bytes (16 MB) and dropped least recently used; the indexes by entries per repository. The desktop
store keeps, at its next step (26 on main at writing), `declaration_blobs` (id, kind, bytes, size, last used) and `declaration_at`
(repository, context, commit, path, blob or none), bounded at 64 MB and pruned by last use at the store's daily eviction. The parses are
not kept: at 4 ms at most they are cheaper to make again than to version. A checkout's stats are not kept: a stat is cheaper than its
row. So a cold start knows every commit it read last time: a watch's start that finds every line where it was asks git nothing for
the declarations (the tips from the ref files, the blobs from the store), where today it asks 100 starts for 20 repositories.

### 4.3 Each reader through it, each at the version it asks for

The store changes how often a version is read, never which version a reader reads. Which one is each reader's contract and stays it:

| Reader | Asks the store for |
|---|---|
| The registration follow, `LayoutFacts`, `LandedGates`, `IntegrationAcceptance`, `IntegrateChoice`, `TreeScratch`, `LineRules` | the line's tip |
| `BacklogMerge`, `UnionJoins`, `GeneratedWriters`, `IntegrationOwed`, `VerifyExecution`, `RepositoryFilesReader.AtAsync`, the analysis's gates | a named commit |
| `TreeLocalInputs` | the checkout's file, never the tree's: what leaves the checkout is the checkout's to declare |
| `HeavyGates`, `OpinionPackets`, `RepositoryIndexes`, `ContextReviewWiring` | the tree's (or the checkout's) file |
| `RegistryOperations`, `RegistryModule`, `LoopGrounds`, `RemoteSync` | the checkout's file |
| `PersonFiles`, `FoundFiles`, `WorkflowFiles`, `DriverConfig` | the home's file |

What goes: `StampedWorkflows`, `TreeReadMemo.Kept.Declared`, `BacklogMerge`'s `_declared`, every reader's own `File.ReadAllText`,
`ls-tree` and blob read of a declaration, and four of the five parses of the gates file. A census (`DeclarationReadersCensusTests`) is red on
a declaration's name read, listed or shown anywhere else, as the git censuses are.

### 4.4 Writes

Daoris's own writes go through the store's writer, which writes the file as each writer does today (atomically, under its lock) and
drops the path's entry before it returns, so a read after a write in the same flow never sees the old one whatever the clock's grain:
the person's settle of a rule proposal and the Files page's edits (`PersonFiles`, `FilesEdits`), the found layer (`FoundReads`), a
preset or a route applied (`PlaceApply`, the workflow writes), `driver.json`'s edits (`DriverConfigDisk`) and the declaration editor
(`RegistryModule`). A write anything else makes (a session in its tree, the person's editor, a checkout pulled) moves the stat, which
the next ask reads. A commit, a landing or a fetch never makes a commit's entry wrong: a commit's path is fixed. It moves the refs'
version, which drops the line tips; a replacement, a graft or a deepen moves the history stamp, which drops the repository's commit
paths.

### 4.5 A session's tree, and two trees that differ

- **A session changes its own configuration.** Its edit is in its tree's file, keyed by the tree's path: the tree's next ask sees the
  new stat and the new content, and no other tree, nor the checkout, nor any commit, is touched. When it commits, the commit is a new
  key; the line's entries change only when its work lands and the line's ref moves. A reader that must not take the session's copy
  (`TreeScratch` takes the accepted blob at the line's tip, `TreeLocalInputs` the checkout's) asks for that version, as today.
- **Two trees of one repository differ.** They are two paths, or two commits, so two keys; where their files hold the same bytes they
  resolve to one content and share its parse, which is right because a parse reads the text alone. A view that reads more than one
  file (a tree's effective rules) is keyed by every content it rests on, so two trees whose rule files differ never share a rule.
- **Two trees whose git reads differently** (a `config.worktree` turning replacements off, an `includeIf` on the folder) have two
  contexts, so a commit's path read in one never answers the other.

### 4.6 Rejected

- **A cache per reader**, as the three today. Each keys its own way (write time and length; the refs' version; one flow), so a
  fourth reader would add a fourth; one store is the one way (D228).
- **Write time and length alone** (`StampedWorkflows`): two writes inside the clock's grain of one length read as one.
- **A file watcher** for the checkouts and trees: one per tree for a hundred trees, events lost on overflow, and a stat costs 30 µs.
- **Persisting the parses**: their shape moves with a build and would need a version; the bytes do not, and the parse costs ms.
- **A held `cat-file` per repository** (GITLIB1's held reader, dropped in its round 4): the store saves the starts by not asking.
- **The service's reads in the same store**: the service is another process, and a shared host answers no machine paths; it keeps
  its own index, refreshed as its knowledge store is.
- **A commit's file read from the checkout where the checkout stands clean at that commit**: knowing it is clean is a status start,
  and a racy one; the persisted commit paths answer a cold start without it.

## 5. The rows

Each row's proof is the bench run before and after it, with the bench's rows for the store added in the first.

- **a. The store's kernel**: the content by blob id with its one parse per kind, the three indexes, the stat and the stamps, bounded in
  memory; unit tests of each key and each invalidation (a stat moved, a racy stat, a ref moved, a replacement added, two contexts, two
  trees with one content). Proof: the bench's store rows, a warm ask no dearer than its stat.
- **b. The commit readers moved**: the follow, `LayoutFacts`, `LandedGates`, `IntegrationAcceptance`, `GeneratedWriters`,
  `BacklogMerge`, `UnionJoins`, `IntegrationOwed`, `VerifyExecution`, `RepositoryFilesReader.AtAsync`, the analysis's gates; the gates
  file's one parse; `TreeReadMemo.Kept.Declared` and `BacklogMerge`'s `_declared` removed. Proof: `LandGitCountTests`,
  `TidyGitCountTests` and `StartGitCountTests` held to their lowered budgets; the bench's line and commit rows at one start per new
  version.
- **c. The checkout and home readers moved**: `HeavyGates`, `TreeLocalInputs`, `OpinionPackets`, `RepositoryIndexes`,
  `RegistryOperations`, `RegistryModule`, `LoopGrounds`, `RemoteSync`'s check, `PersonFiles`, `FoundFiles`, the workflows
  (`StampedWorkflows` removed) and `DriverConfig`, with the writers of §4.4; the census. Proof: the census red on a planted read; the
  bench's checkout rows at a stat each.
- **d. The views and the analysis's commit part**: a repository's lanes, gates and manifest views; the analysis's tracked list, gates
  and manifests kept by the commit, so the files stage and the Files page ask the tree for its ignored tops and its worktrees alone; a
  tree's effective rules keyed by its layers, composed once per version. Proof: the bench's files-stage and Files page rows warm at
  two starts on this repository, from 11 and 10.
- **e. Persisted**: store step 26, its bound and its eviction; a cold start's follow. Proof: the bench's follow over 20 repositories,
  run twice with the driver made again between, at 0 starts the second time where no line moved.
