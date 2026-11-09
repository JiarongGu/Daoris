# A repository's Daoris configuration read once and indexed (REPOCONFIG1)

**Round 3, proposed for both makers' reads before a decision is written** (2026-11-09). The owner finds reading a repository's
Daoris configuration slow and wants it read once and better indexed. Round 1 (§0 to §5) measured every reader (§1, §2, §3) and
proposed one store every reader passes (§4) and the rows that build it (§5). Both makers read it ([the reads](2026-11-09-repo-config-reads.md))
and found seventeen blocking sequences and thirteen readers it missed; round 2 (§6 to §17) answers them by class, and supersedes
round 1 where it differs. Both read round 2 and found seven more and a leak in each census; round 3 (§18 to §28) closes them by
construction, and supersedes round 2 where it differs. Driver paths are under `src/Daoris.Desktop/Daoris.Desktop.Driver/`. It builds on GITLIB1
([its design](2026-11-09-git-library-design.md)): the stamp it designed (its §10.3 and §14.5), which round 2 builds first, and its one
object read (`GitObjectReads`).

## Round 1

*Superseded where round 2 differs: §4.1's keys and indexes by §7 to §10, its views by §11; §4.2 by §14; §4.3 by §10; §4.4 by §13.2;
§4.5's last point by §7; §1's list of readers by §13.1; §5 by §17.*

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

## 6. Round 2: eight classes, each closed by construction

Both reads of round 1 found seventeen blocking sequences, ten Codex's and seven Opus's, and they share members: the stamp is in
both, as are a reader's version and a view's inputs. Read together they are eight classes, each with members the reads did not
name, so round 2 closes each by a construction a missed member cannot pass, held by a census or a test that fails on one. §6.1 names
the classes and their members; §7 to §14 are the constructions; §15 places each finding; §17 re-cuts the rows.

- **One git stamp** of everything a git answer rests on, built first as GITLIB1's own; a kept or saved answer is read only under the
  stamp it was taken under (§7).
- **An answer is present, verified absent or unread**; only a clean parse and a listed absence are kept (§8).
- **A checkout's file is trusted by its stat** only where its bytes were hashed two seconds after its write, and its hash is never a
  blob id (§9).
- **A commit's file is read only at a commit its caller holds**; a tip is resolved first, for the ref the caller names (§10).
- **A view is a function of a key that holds every input**; listings and existence checks stay per ask (§11).
- **The analysis keeps what its commit and stamp fix** and asks the tree for the rest: warm, two starts, three where a top is new (§12).
- **Every reader and writer is named** and held by a source census and a run-time one, each shown red on a planted reader (§13).
- **One weighed bound in memory, one held at admission on disk**, and every shared parse immutable (§14).

### 6.1 The classes

| Class | Its members: the reads', then those found beside them | Closed by | Fails on a missed member |
|---|---|---|---|
| **The stamp** | No history stamp in the code (Opus 1); a replacement's bytes kept under the replaced id (Codex 3, Opus 1); `declaration_at` saved with no stamp (Codex 4). *Found:* `GitReadCache` keyed by the common directory alone, so a replacement or a graft added between two page reads is answered from before it; a re-pointed replacement, which keeps its name, so GITLIB1 §10.3's stamp of names misses it; `core.useReplaceRefs` in the global or the system file; `GIT_REPLACE_REF_BASE` | `GitStamp`, one type, built first; `GitReadCache`'s key and every saved row carry it; no listing kept where a replacement stands; bytes kept only where they hash to their id (§7) | `GitStampTests`: each input moved alone between two questions moves the stamp; `GitKeepersCensusTests` naming the stamp of each holder; row e's restarts |
| **What a read means** | A failure saved as absent (Opus 2); `Read(null)` as empty rules (Codex 10); readers' different size limits (Opus, not blocking). *Found:* `rev-parse --verify --quiet <rev>:<path>`, exit 1 for a missing commit as for a missing path (`GeneratedWriters`, `BacklogMerge`, `GitObjectReads`' named form); a limited `TextAsync` answering a prefix, which the follow (512 KB) and the acceptance (256 KB) parse as a broken file; a link's target text read as the file (Codex 2) | One answer type with three cases; kept only a clean parse and an absence a listing showed (§8) | A store test per cause of *unread*, each asserting nothing kept in memory or on disk |
| **The checkout's race** | A racy stat trusted once it aged (Codex 6, Opus 3); a checkout's id compared with a commit's (Opus 6). *Found:* a write during the read; a link, whose stat does not move with its target; a write time ahead of this clock; the stamp's own files, which need the same rule | `FileStamp` under git's rule, its stat read before and after the bytes; `ContentKey` and `BlobId`, two types no member takes together (§9) | The racy sequences of row c; a member taking both types fails to compile, and the census is red on a planted conversion |
| **Versions per reader** | `LineRules` and `IntegrationAcceptance` at a named commit (Codex 1, Opus 4); `IntegrationOwed` at the tip (Opus, not blocking); a `daoris/` line, which `CountedRefs` leaves out (Codex 5). *Found:* `TogetherAccepts` naming its line short, which git resolves to a tag of that name before the branch; `FilesLooks.CommitAsync` naming `line.Branch` short; `GeneratedWriters` and `BacklogMerge` at `onto` and `source` | A commit's file only at a commit id; `TipOf` for a full ref name the caller hands, read from that ref's own file (§10) | Each moved reader's tests unchanged; the line moving during a replay; `TipOf` refusing a short name |
| **Views and listings** | `FileRules.Of`'s repository selector (Codex 7); listings and existence checks keyed by contents (Codex 9, Opus 5). *Found:* `FileRules.Of`'s signature, a closure over the analysis's hits and ignore-case that no content key names; `RoomCheck` over the tracked list; `HeavyGates.Resolve` over `driver.json`'s mapping | A view a pure function of a key record that holds every input; a listing per ask (§11) | `DeclarationViewsCensusTests` red on a planted input; each view tested against the reader it replaces with each input moved alone |
| **The analysis** | The grep keyed by the tree's tops (Codex 8, Opus 7); ignore-case (both); the walks (Opus 7). *Found:* the worktree list, which a tree added anywhere in the repository moves; `FilesLooks`' commit, a start of its own | Each part keyed by what fixes it: commit and stamp, names and pathspecs, or the tree (§12) | Row d's sequences; the bench's counts |
| **Every reader and writer** | Thirteen readers (Opus); every parser of the gates file (Codex); two writers, the registry rows and the census's exemptions (Opus, not blocking). *Found:* `LayoutGit`, `FilesLooks.CommitAsync`, `IntegrationDeclaration.Declared` (a ninth parse), `FoundFiles.Write` and `PersonFiles.Write` (two more writers), `WorkflowHomeTopic`, `WorkflowTerminal`, `WorkflowsStep`, `BaseHosts`, and the names said in words alone | The list and its exemptions (§13) | `DeclarationReadersCensusTests` (source) and `DeclarationGitCensusTests` (run time), red on five planted readers and on each exemption left out; `DeclarationKeepersCensusTests` red on a parse kept outside the store |
| **Bounds** | Parses and views unweighed, the saved bound held only daily (both); a shared lazy `JsonNode` (Opus, not blocking). *Found:* listing and absence rows unweighed on disk; a tracked list larger than the bound; `DriverConfig`'s mutable collections behind read-only interfaces | One weighed bound in memory, one at admission on disk, immutable kinds (§14) | The admission test counting every row kind; each weight against its allocation; `DeclarationParsesCensusTests` red on a planted mutable member |

### 6.2 How the members were searched

In the code at `0422e84b1b`, class by class, not in the reads. For the stamp: every input git reads for a listing or an object (the
configuration at each level, the replacements by name and target, the grafts, `shallow`, the git, the variables) against every holder
that keeps an answer past one flow (`GitReadCache`, the store, the flow memos' comparisons). For what a read means: every exit code and
every null of `GitObjectReads`, of the `rev-parse` and `ls-tree` calls and of `File`'s throws, at each declaration reader. For the race:
every field of a stat (length, write time, the file's id, its change time, a link) against a write before, during and after the read.
For versions: every caller of the tip and commit readers and the value it hands (`AcceptedAsync(`, `AcceptedTextAsync(`,
`DeclaredTablesAsync(`, `TipAsync(`, `CommitAsync(`). For views: every parameter and closure of each view's function. For the
analysis: every git `FileAnalysis` and `FileGit` start and what it reads. For readers and writers: every literal of a declaration's
name and every reference to a constant naming one (`HeavyGates.FileName`, `LineRegistration.ManifestFile` and `LanesFile`,
`RepositoryFilesReader.FileName`, `GeneratedWriters.DeclarationFile`, `BacklogMerge.Manifest`, `PersonFiles.FileName`,
`FoundFiles.FileName`, `WorkflowStore.Folder`, `KnowledgeConnector.WorkspaceMarker`) under the driver and the modules, each hit read;
and every JSON parse in a file that reads the gates file. For bounds: every field and collection type reachable from a parse.

## 7. The stamp

**`GitStamp` (`Workplace/Git/GitStamp.cs`) is the one stamp of what a git answer rests on, for the folder that asks.** GITLIB1 designed
it (its §10.3 and §14.5) and built only its context part, `GitAskerContext`; the rest is owed alike to `GitReadCache` and to this
store, so it is REPOCONFIG1a's first piece, landed before any of the store, and it is GITLIB1's stamp: one type, its holders listed in
`GitKeepersCensusTests`. Its inputs:

- **the git in use**: its file and write time, as `GitRuns.GitInUse` names it;
- **the configuration**: the system file (its path asked of the git once per git in use), the global file as a start is handed it
  (`GitGlobalFile.Hand`'s, else the person's own and the XDG file), the repository's `config`, the asking tree's `config.worktree`, and
  each file these include (`include.path`, `includeIf`); each `includeIf` evaluated for the asking folder (`gitdir:`) and its branch
  (`onbranch:`, which takes the tree's `HEAD` in only where such an include stands); `core.useReplaceRefs` is in their contents;
- **the history**: every replacement by name and target (the loose files under `refs/replace/`, the `refs/replace/` lines of
  `packed-refs`), `info/grafts` and `shallow`; in a reftable repository `reftable/tables.list`;
- **the variables** a start is handed that change an answer: `GIT_NO_REPLACE_OBJECTS`, `GIT_REPLACE_REF_BASE`, `GIT_DEFAULT_HASH`,
  `GIT_CONFIG_*`.

Each file is read as a `FileStamp` (§9): its bytes hashed again only where its stat moved or was racy, so a warm stamp is stats. The
stamp is a digest of the inputs' contents, never their write times, so it is the same after a restart where nothing moved and differs
where anything did; that digest is what a saved row carries (§14.2). It says one fact of its own, `Replaced`: a replacement in force
(one listed, or `GIT_REPLACE_REF_BASE` set, with neither `core.useReplaceRefs=false` nor `GIT_NO_REPLACE_OBJECTS`).

Its holders: `GitReadCache`, whose key carries the asker's stamp (`GitReads`' `Repository` takes it; a shallow repository still keeps
no history answer); the flow memos' comparison of two folders, through the stamp's context part (`GitAskerContext` goes, D228, its
callers comparing what they compare now); the library's admission, which reads `Replaced` from it, so *a replacement stands* is
judged one way; and the store, where every listing and every saved row is keyed by it and none is kept while `Replaced` holds,
since a replacement can change any object a listing reads, its commit or its tree, and the library admits no question there either.

**Bytes are kept only where they hash to their id.** `cat-file --batch` answers a replaced blob's id with the replacement's bytes, so a
blob read from git is hashed in its repository's hash before it is kept under its id; one that does not hash to it answers its ask
and is kept nowhere (Codex 3, Opus 1). A saved blob is hashed again when it is loaded.

## 8. What a read means

Every ask of the store is answered as one of three:

- **Present**: the entry (at a commit its path, mode, object type and blob id; on disk its `FileStamp`), the content (its `ContentKey`
  and size) and its parse. Only a regular file's blob (mode `100644` or `100755`, type `blob`) is parsed: a link, a folder or a
  submodule is said as what it is, its mode and type kept, so identical bytes committed first as a file and then as a link are a file
  and then a link (Codex 2).
- **Absent**, verified: at a commit only from a listing (`ls-tree --full-tree -z <commit>^{commit} -- <names>`, one start for every
  declaration's name at once) that exited 0 with no entry for the path (Opus 2's rule, as `IntegrationAcceptance.BlobAtAsync` reads
  it); on disk from a stat that finds no file in a folder that stands.
- **Unread**, with its words: the listing failed (a commit or a tree git does not hold, lazy fetch being off); the blob missing after
  `GitObjectReads`' one fetching ask; a size past the store's bound or the reader's limit; a read ended short of its size; bytes that do
  not hash to their id; an IO error; the folder gone; a stat that moved during the read (the bytes answer, §9); a parse that names a
  problem, which is said with the problem.

**Kept: a present content with a clean parse, and an absence at a commit. Never kept:** an unread of any cause, an absence on disk
(its stat is its check), a prefix of a content, and a parse that names a problem, which is made again from the kept bytes at each ask
(milliseconds, and a broken declaration is a moment). So an offline partial clone's follow says *could not read* and keeps nothing,
and the first ask after its promisor answers reads it (Opus 2); no failure becomes empty rules (Codex 10).

**Sizes.** The store reads a blob whole up to its bound, 1 MB (fifty times this repository's largest declaration); a content past it
is read at each ask through the reader's own limit and kept for none, as today. A content within it is kept with its size, and a reader
whose limit is lower (512 KB for the follow, 256 KB for the acceptance, the landed gates and the verification) is answered *unread:
larger than its limit* where the size passes it, never handed the prefix it parses today as a broken file: the one change of words, in
row b with its test.

**Each reader keeps its outcome and gains the words.** A reader maps *unread* to what it says now of a file it cannot read
(`LineDeclarationReader`'s *git could not read its …*, the acceptance's `Unread`), and *absent* as now. `RepositoryFilesReader.AtAsync`,
`GeneratedWriters.AcceptedAsync` and `BacklogMerge.DeclaredTablesAsync`, which today turn every failure into none, keep none for their
callers and keep nothing in the store, and the rule file's says the read's words as a problem (`RepositoryFilesRead.Problems`), so the
Files page says the rule file could not be read at its commit where it said nothing. A listing or a blob read in flight is joined by an
ask of the same key under the same stamp: its answer is a fact of ids, so its moment does not matter (GITLIB1 §10.2's rule is for
answers of a state that moves, which no checkout ask joins).

## 9. The checkout's race

**`FileStamp`** is a file's length, write time, and its id and change time where the system gives them. An entry keeps the stat read
before the bytes (`s1`), the moment `s1` was read (`H`) and the content's key; the stat is read again after the bytes (`s2`).

- **Moved during the read** (`s1 ≠ s2`): the bytes answer this ask and nothing is kept.
- **Trusted only where `H` is at least 2 s past `s1`'s write time**, and never where the write time is ahead of `H` (a share's clock):
  git's rule for a racy index entry, since a second write of one length inside the clock's grain leaves the same stat. Trust is fixed
  when the bytes are hashed: an untrusted entry is read and hashed again at every ask, and only a hash taken 2 s past the write makes a
  trusted one; time passing never promotes an entry (Codex 6, Opus 3). The stat taken before the bytes is the reference, so a write
  after it has a later write time or is caught by `s2`.
- **A link** (its `LinkTarget` set) is read and hashed at every ask: its own stat does not move with its target.

The stamp's files (§7) are `FileStamp`s under the same rule. Daoris's own writes also drop the path's entry before they return (§13.2).

**A checkout's content is a `ContentKey`, SHA-256 of its raw bytes; a commit's is a `BlobId`**, kept only where its bytes hash to it
and mapped to their `ContentKey`. They are two types and no member takes both, so a checkout's file is never compared with a commit's
blob: `core.autocrlf` (Git for Windows' default), `eol` and any clean or smudge filter make a clean file's bytes differ from its blob
(Opus 6). A checkout's file and a blob of the same bytes share one parse through the `ContentKey`, which is right because a parse reads
the bytes alone. `RemoteSync`'s *unmodified* stays git's (`CheckoutReads.UnmodifiedAsync`), reading nothing of the store's.

## 10. Versions per reader

**The store reads a commit's file only at a commit id its caller holds.** A tip is resolved first, by `TipOf(repository, ref)` for the
full ref name the caller hands (`refs/heads/<line>`, or `HEAD` for the asking tree's own), and the caller keeps the commit it resolved
to for its whole judgement, so a line that moves during a replay leaves the replay reading the commit it replays onto (Codex 1,
Opus 4). `TipOf` reads that ref's loose file whole at each ask (41 bytes, as cheap as its stat), else its line of `packed-refs` (the file
by its `FileStamp`, parsed once a version), a symbolic ref followed to a depth of five; it refuses a name that is not a full ref, and
leaves nothing out, so a `daoris/` line is read as any other (Codex 5). A reftable repository asks git, as today. `CountedRefs` stays
the tree walk's (`TreeReadMemo`), and no store entry rests on it. A folder holding its own `.git` whose configuration names no
`core.worktree` is its top, read from the files as `RefFiles` reads them; any other is asked of git, as `IsTopLevelAsync` asks it now.

| Reader | Its version | What it reads |
|---|---|---|
| The registration follow (`LineDeclarationReader`); `LayoutGit`'s declaration entries | `refs/heads/<line>`'s tip, then that commit | manifest, lanes; a link or a folder refused as now; `LayoutGit`'s whole listing and instruction files stay its own |
| `LandedGates`, `WorkflowChoosing`, `PlacePlans`, `PlaceWorld`, `WorkflowRoutes` | `refs/heads/<line>`'s tip, then that commit | gates, and their text by blob id |
| `IntegrationOwed`, `IntegrateChoice`, `TreeScratch`, `IntegrationPromoteReads.LineAsync` | `refs/heads/<line>`'s tip, then that commit | gates, against the accepted blob |
| `IntegrationAcceptance` (`AcceptedTextAsync`, `BlobAtAsync`, `TextAsync`) | the commit its caller names; the frozen or accepted blob by id | gates |
| `LineRules.AcceptedAsync`, from `BranchReplays` (`onto`), `IntegrationResolves` (`lineBase`), `TogetherWatch` (`facts.LineTip`), `TreeLandPlans` (`sync.Onto`), `TogetherAccepts` (its line, now named in full) | the commit named | the gates' line rules |
| `IntegrationPromotes` (`candidate.Base`, `frozen`), `IntegrationPromoteReads.FenceAsync` | the commit named; the frozen blob | gates and their runner (`RunnerOf`) |
| `GeneratedWriters.AcceptedAsync` (`onto`, `lineBase`) | the commit named | the gates' writers, against the accepted blobs |
| `BacklogMerge` (`source`), `LineHolds` (`onto`), `TogetherChecks` (`headTip`), `UnionJoins` (`declaredAt`) | the commit named | the manifest's backlog and decisions folder; the gates' row tables |
| `VerifyExecution` | the candidate's commit | gates: declared gates (`DeclaredGates`), the runner (`VerifyRunner.Of`) |
| `RepositoryFilesReader.AtAsync`; the analysis's gates and manifests (§12) | the commit named; `FilesLooks` names its line's ref in full | the rule file; the gates' local inputs; manifests |
| `HeavyGates` (`Read`, `Resolve`, `Marks`), `HeavyRun`, `HeavyTold`, `ContextReviewWiring`, `OpinionPackets`, `RepositoryIndexes`, `StandingBlocks`, `CodeMapFile` | the tree's file: a session's tree, or the checkout | gates; manifest and indexes; the code map |
| `TreeLocalInputs` | the checkout's file, never the tree's | the gates' local inputs |
| `RegistryOperations`, `RegistryModule`, `LoopGrounds` | the checkout's file | manifest, lanes |
| `RemoteSync` | git's status of the checkout's manifest | nothing of the store's |
| `PersonFiles`, `FoundFiles`, `FoundReads`, `FileAsks`, `WorkflowFiles`, `DriverConfig` | the home's file | the layers, the asks, the workflows, the settings |

A short name, which git resolves through its rules (a tag of the same name before the branch), is named in full by the two callers that pass one
(`TogetherAccepts`' recheck, `FilesLooks.CommitAsync`): a change only where a tag shadows the line, where today's answer is the tag's.

## 11. Views and listings

**A view is a pure function of its key.** Each view is an `IDeclarationView<TKey, TValue>` computed from its key record alone, through
the store's contents by the content keys the record holds. The record holds every input the view depends on: each content key it rests
on, or that content's verified absence; each selector it applies (the repository's name as `PersonFilesRead.For` selects it, the
scope, the repository whose found layer it reads); the analysis's key where a signature's rule fills a field (§12); and the build where
it reads what the build carries (`FileSignatures.Shipped`). No path, no clock, no delegate, and no setting outside a content key: what
`FileRules.Of` takes as a closure over the analysis's hits and ignore-case is named by that analysis's key instead. The views: a
repository's lanes and rooms, and `RoomCheck`'s rooms over the lanes file, the rule file and the tracked list; the gates file's views
(§13.1); the manifest's domain, documents, rooms, backlog and decisions folder; a tree's effective file rules; a workflow by id. A view
over a setting (`HeavyGates.Resolve` over `driver.json`'s mapping) keys `driver.json`'s content and the repository's name. So two
repositories whose rule file, person layer and found layer share ids, and whose names select different parts of the person's layer,
get two views (Codex 7). A view is computed once a key under the store's gate, never inside its value.

**Listings and existence checks stay per ask, and are no view.** `RepositoryIndexes.Find`'s seven folders and its router,
`WorkflowFiles.List`'s folder, `CodeMapFile.Find`'s candidates and `HeavyTold`'s check of the gates file (now a store ask, whose stat is
the check) each enumerate or stat at every ask (the seven folders 1.1 ms warm, measured), and each file they find is read through the
store. So a workflow a separate `daoris` process writes, a file added beside unchanged ones and an index file in one of two trees with
one manifest are each found at the next ask in that tree alone (Codex 9, Opus 5).

`DeclarationViewsCensusTests` reads the views' folder with Roslyn and is red on a view whose computation names a file or directory
API, the clock, the environment, `DriverConfig`, a home layer's reader or a static mutable member, or whose key holds a delegate or a
path; it is shown red on a planted view that reads `PersonFiles.Read`. Each view is tested against the reader it replaces, with each of
that reader's inputs moved alone, Codex 7's sequence among them.

## 12. The analysis

`FileAnalysis.ReadAsync`'s parts, each kept by what fixes it:

| Part | Fixed by | Kept |
|---|---|---|
| The tracked list (`ls-tree -r`) | the stamp and the commit | in memory, weighed (§14.1) |
| The gates, manifests, lockfiles and workspace files at the commit | the store's asks at the commit | as contents (§8) |
| The script search (`FileGit.LinesAsync`) | the stamp, the commit, the names asked (from the tree's tops, sorted) and the pathspecs | in memory, weighed |
| Ignore-case (`core.ignorecase`) | the stamp, whose configuration it is | with the stamp, asked once a stamp |
| The ignored tops (`ls-files --others --ignored`), the worktree list | the tree; the repository's worktrees | asked each analysis: two starts |
| The walks (`FileWalk.Of`, one a top) | the tree's folders | walked each analysis, no start |
| The floor facts | the tracked list and the walks' repositories | made each analysis |

So tree A's search never answers tree B's new top (Opus 7): B asks a name A did not, a new key, git's own search. An ignored output
added at an unchanged `HEAD` (Codex 8) is a new top, so a new name and a new search: its writer is found and it is judged `Output`.
Ignore-case is git's answer for the stamp, never Daoris's reading of git's configuration. The line's commit is `TipOf` the line's ref
(`FilesLooks` names it in full), no start.

**Row d's target, restated.** Warm, the files stage and the Files page each ask the tree for its ignored tops and its worktrees: **two
starts where its tops are as they were, three where a top is new** (the search), from 11 and 10 on this repository. A new commit adds
its tracked list and its declarations' listing, a new content its blob, a new stamp its ignore-case: one start each.

## 13. Every reader and writer

### 13.1 The readers

Every reader of §10's table goes through the store, each moved with its own tests unchanged. Beside them:

- **The parse kinds**, each the one parse of its file, with each of today's parsers a view of it: the gates file's
  (`GatesDeclaration`), whose views are what `HeavyGates.Read`, `TreeLocalInputs.Declared`, `GeneratedWriters`' parse,
  `FileAnalysis.LocalInputs`, `IntegrationDeclaration.Read` and `Declared`, `DeclaredGates.Read`, `VerifyRunner.Of` and
  `IntegrationPromoteReads.RunnerOf` each parse apart today (nine parses of one file; `BacklogMerge.RowTablesOf` reads
  `IntegrationDeclaration.Read`'s); the manifest's
  (`LineRegistration.Compose`'s half, `BacklogMerge.PathOf`, `UnionJoins`' decisions folder, `RepositoryIndexes`'); the lanes file's
  (`LaneRooms.Read`); the rule file's (`RepositoryFilesReader.Read`); the person's and the found layers'; a workflow's; `driver.json`'s.
  `RoomCheck` and `PlacePlans` read views, never text.
- **The caches that go** (D228): `StampedWorkflows`, `TreeReadMemo.Kept.Declared`, `BacklogMerge`'s `_declared` and `TogetherChecks`'
  `declared ??=`, a fourth Opus found.
- **The names in words alone**, which read nothing: `SetupBrief`, `IndexBrief`, `ReconcileBrief`, `HelpWorkflowProposals`,
  `RoomsProposal`, `DriveUsagePlace`, `DriveUsageRecords`, `IntegrationGatesKey`, `WorkflowKey`, `HeavyMappingCommand`,
  `SessionChecksCommand`, `FilesCommand`, `RegistrationFollow`, and the words of `WorkflowChoosing`.

### 13.2 The writers

Each writes through the store's writer as it writes today (atomically, under its lock), and the writer drops the path's entry before
it returns:

- the person's layer: `PersonFiles.Set`, `Remove` and `Write` (the Files page's edits through `FilesEdits`, a rule proposal's settle),
  and `PersonFilesForm.Rewrite`, the form's rewrite inside `PersonFiles.Read`, which the store's read of that layer calls through the
  writer, so the read that follows reads the rewritten file;
- the found layer: `FoundFiles.Write`, through `FoundReads`;
- the asks: `FileAsks`' writes;
- the workflows: `WorkflowFiles.Write`, which the presets and routes `PlaceApply` applies and the workflow edits reach;
- `driver.json`: `DriverConfigDisk.Save`, the one writer its eleven callers reach;
- a checkout's manifest: `RegistryModule`'s declaration editor.

A write anything else makes (a session in its tree, the person's editor, a pull, another `daoris` process) moves the file's stat,
which the next ask reads under §9's rule.

### 13.3 The censuses and the exemptions

**`DeclarationReadersCensusTests`** reads the driver's and the modules' sources with Roslyn. Every expression naming a declaration (its
literal; a constant naming one, all declared once in `DeclarationNames`; or a path helper: `PersonFiles.PathOf`, `FoundFiles.PathOf`,
`WorkflowStore.PathOf`, `driver.json`'s path, the home entries for the asks and the layers) is followed through the locals and fields
it is assigned to within its member, and is red where it reaches a file or directory API (`File`, `Directory`, `FileInfo`,
`DirectoryInfo`, `AtomicFile`, a stream), a git argument list (`WorkingTree.GitAsync`, `GitObjectName.At`, the git service's asks) or
a JSON parse outside the store's folder and the exemptions. An interpolated string handed to none of these is words. It is shown red on
five planted readers: a literal (`File.ReadAllText(Path.Combine(root, "daoris.gates.json"))`), a constant through a local (`var name =
DeclarationNames.Gates;` then `File.Exists(Path.Combine(root, name))`), a path helper (`AtomicFile.ReadText(PersonFiles.PathOf(home))`),
a git read (`GitObjectName.At(commit, DeclarationNames.Manifest)`) and a listing (`Directory.EnumerateFiles` of the workflows' folder);
and red on each exemption left out in turn, `SourceCensus.Problems` saying a member missed and a member gone.

**`DeclarationGitCensusTests`** is its run-time half: over the starts the count tests record at the process gate (`GitStartTally`) in
`LandGitCountTests`, `TidyGitCountTests`, `StartGitCountTests` and the bench's flows, every start whose arguments or question name a
declaration's path was asked by the store, whose starts are tagged when they are built (GITLIB1b's whole start). It is red on a planted
reader that splits a name to pass the source census. **`DeclarationKeepersCensusTests`** is red on a field outside the store whose type
is a parse kind or a view, so a fifth cache is caught where it is declared.

**The exemptions, each with why:**

- `EngineProfile.RecordPath`: a browser profile's own `daoris.json`, Daoris's record beside an engine's files, not a repository's.
- `ServiceHostLocator`: finds the service host's project folder by a `daoris.json` above the running build; an existence walk of the
  workspace, reading nothing.
- `KnowledgeConnector.WorkspaceMarker`, with `BaseHosts`: the same walk, for the knowledge server's build.
- `WorkflowHomeTopic`: the bus's stamp of the workflows folder for its home topic; it publishes a change and reads no content.
- `WorkflowTerminal`: a file the person names on the command line, not the home's workflows.
- `WorkflowsStep` (`Transitional/`): its migration's own reads, deleted with the migration.
- Another process: the service's readers (`RepositoryLayout`, `DaorisLock`, `CheckoutFreshness`, `RegistryImport`, `HostComposition`),
  the devkit's (`GateDeclaration`, `DoctrineGate`) and the CLI's; outside the census's sources, as §4.6 says.

### 13.4 The registry rows

**The registry rows stay the service's answer, asked per request, and are not in the store.** They are the service's record, which a
desktop copy would make a second (D228), and a request to the local service costs milliseconds, not a git start; the bench measured no
cost in them. A request the measures later show dear is a row of its own against the service's routes.

## 14. Bounds

### 14.1 In memory

**One bound, 16 MB, over everything the store keeps**: bytes, parses, views, listings, tracked lists, searches and the parsed
`packed-refs`. Each is weighed by an estimate as `GitReadWeight` weighs an answer (its strings' characters, two bytes each, and a little
for each object), and a test holds each kind's estimate no lighter than the allocation its parse makes on the bench's declarations
(`GC.GetAllocatedBytesForCurrentThread` around it). The least recently used go first, across every kind; nothing heavier than a
quarter of the bound is kept, so one repository's tracked list never empties the store.

### 14.2 Saved

At the desktop store's next step (26 on main at round 1): `declaration_blobs` (blob id, content key, size, bytes, day last used) and
`declaration_at` (repository, stamp digest, commit, path, then mode, type and blob id, or absent, and day last used). **The bound,
64 MB, holds at admission**: each insert, in its own transaction, reads the running weight (one row, written in that transaction),
drops the least recently used rows until the new one fits and adds its weight, a listing or an absence row weighing 256 bytes and a
blob its bytes and that; nothing heavier than a quarter of the bound is saved. The daily eviction is a trim beside it: the rows of a
repository no longer registered and of a stamp none of its trees has now. A row is read only under the asker's stamp digest now (Codex
4); a blob row is hashed against its id when it is loaded; the day last used is written at most once a day a row. Nothing unread is
ever saved (§8).

### 14.3 Shared parses

Every parse kind and view is a record of immutable collections (`ImmutableArray`, `FrozenDictionary`, `FrozenSet`, a compiled
`Regex`), with no setter, no lazy field and no `JsonNode`. `WorkflowFileFound.File`, a lazily filled `JsonNode`, is not shared: the
workflow's kind keeps its graph immutably, and a reader that edits is handed a fresh `JsonNode` parsed from the kept bytes.
`DriverConfig` is built over frozen collections behind its read-only interfaces. `DeclarationParsesCensusTests` reflects every type
reachable from a kind or a view and is red on a mutable member (a settable property, a field not read-only, `List<>`, `Dictionary<>`,
an array, `JsonNode`, `Lazy<>`), shown red on a planted one.

## 15. The reads answered

| Finding | The sequence it closes | Round 2 | Proof (row) |
|---|---|---|---|
| **Source versions** (Codex 1; Opus 4) | Freeze A, the line moves to B, a read of A takes B's rules | A commit's file only at a commit the caller holds; each reader's version named (§10) | The line moving during a replay, on `BranchReplays` and `IntegrationPromotes`; their tests unchanged (b) |
| **Entry metadata** (Codex 2) | Identical bytes committed as a file, then as a link, read as one | Mode and type kept with the entry; only a regular file parsed (§8) | A store unit; the follow's link test (a, b) |
| **Replacements** (Codex 3; Opus 1) | A replacement-enabled tree's bytes reused for one with replacements off, and shared across repositories | Bytes kept only where they hash to their id; no listing kept while a replacement stands; the stamp's context part (§7) | `GitStampTests`; a replaced blob kept nowhere (a) |
| **Restart** (Codex 4; Opus 1) | A row saved, the desktop down, `git replace` or `core.useReplaceRefs=false`, the row answered at the cold start | Every saved row carries the stamp's digest; another digest is never read (§7, §14.2) | Both made while the desktop is down, refused at the cold start (e) |
| **Refs** (Codex 5) | A `daoris/` line kept under `CountedRefs`' version while a session commits there | `TipOf` reads the named ref's own file (§10) | A `daoris/` line moved, read again (a) |
| **Stat aging** (Codex 6; Opus 3) | A young stat trusted once it aged, after a same-length write inside the grain | Trust fixed at the hash: 2 s past the write; stat before and after (§9) | The in-place write asked again at 3 s, the new content read (c) |
| **Layered views** (Codex 7) | Two repositories sharing three ids and differing in personal rules share a view | A view's key holds every input and selector (§11) | The sequence; the views census (d) |
| **The analysis** (Codex 8; Opus 7) | A new ignored output, or tree B's new top, answered by A's search | The search keyed by stamp, commit, names and pathspecs; ignore-case by the stamp; the walks the tree's (§12) | Both sequences, judged `Output` (d) |
| **Discovery** (Codex 9; Opus 5) | A workflow or an index file added beside unchanged ones, missed | Listings and existence checks per ask (§11) | A workflow written by another process listed; an index file found in its tree alone (c) |
| **Failures** (Codex 10; Opus 2) | An offline partial clone's follow saved as absent, *not set up* across restarts | Absent only from a successful listing; nothing unread kept (§8) | The promisor down: said, nothing kept or saved; read once it answers (b, e) |
| **The checkout's id** (Opus 6) | A CRLF checkout compared with its blob, always modified | `ContentKey` and `BlobId` apart; `RemoteSync`'s check git's (§9) | `core.autocrlf=true`: the manifest still unmodified (c) |
| **Readers missed** (Opus) | Thirteen readers outside the store, each a second way | Each placed (§10, §13.1) | The two censuses, red on planted readers (c) |
| **The rows** (both) | Budgets lowered before parity, hits shown and no misses, daily pruning as the bound | Parity first, then the bench; the misses; the bound at admission (§17) | Each row's proof |

**The points that did not block.** Shared parses immutable (§14.3). Each reader's size limit on a kept size (§8). `PersonFilesForm.Rewrite`
and `FileAsks` among the writers (§13.2). Parses and views weighed, and the saved bound held at admission (§14). The registry rows
outside (§13.4). The census's exemptions (§13.3). `IntegrationOwed` at the tip (§10). Row a's warm claim, which a commit or a tip
ask cannot meet, restated (§17). After a restart the follow still runs `FoundReads`' toolchain digest for every repository: processes
reading the machine's tools, not a repository's configuration, so they stay and row e's bench counts them apart.

## 16. Rejected in round 2

- **A stat trusted by its age at the ask** (round 1's): it promotes a racy observation.
- **The refs' version as a line tip's stamp** (round 1's): it leaves `daoris/` lines out, and the named ref's own file is as cheap.
- **A checkout's hash in git's blob form, compared with the commit's**: wrong for a clean file under `core.autocrlf`, `eol` or a filter.
- **Listings in a view's key**: a key of every folder's names is the listing, read anyway.
- **The scripts' text kept by commit and searched per tree** (Codex 8's alternative): a second search beside git's (`-I`'s binary rule,
  `-F`, the pathspecs' matching), where a key of the names asked is git's own answer.
- **Ignore-case read from git's configuration by Daoris**: a second reader of git's configuration, its includes and conditions; it is
  asked once a stamp.
- **Lookups kept under a replacement, stamped by its targets**: correct, but a second rule beside the library's admission for a case
  rarely met.
- **A parse with problems kept**: it costs milliseconds to make again, and a kept one could outlive the moment it was broken in.
- **The registry rows in the store** (§13.4).

## 17. The rows

*Superseded by round 3's rows, §28.* *Supersede §5's.* REPOCONFIG1a lands first; b and c follow a and are apart; d follows b and c; e follows b. In every row each moved
reader's existing tests pass unchanged before a count is lowered, and the bench is run before and after it: the bench measures, the
tests assert.

- **a. The stamp, then the kernel.** First GITLIB1's stamp (§7): `GitStamp` with every input, `GitAskerContext` folded into it,
  `GitReadCache`'s key carrying it, the library's admission reading `Replaced` from it. Proof: `GitAskerContextTests`'
  sequences, `GitReadCacheTests` and the flow memos' tests unchanged; `GitStampTests` moving each input alone between two questions,
  and a same-length rewrite of each file inside the clock's grain, each moving the stamp; a replacement added between two graph reads
  answered as git's; `GitKeepersCensusTests` naming the stamp. Then the kernel: `FileStamp`, `ContentKey` and `BlobId`, the listing at a
  commit, `TipOf`, the checkout and home index, the three answers, the parse kinds (each of today's parsers moved behind its kind,
  unchanged), the memory bound. Proof: a unit test of each key and each invalidation (a stat moved; a racy stat hashed again and never
  promoted; a stat moved during the read; a link; a loose, a packed, a symbolic and a `daoris/` ref moved; a replacement keeping no
  listing; two contexts; two trees of one content; a blob not hashing to its id; each cause of *unread* kept for none; a parse with a
  problem made again; a size past a reader's limit); the weights against the allocations; `DeclarationParsesCensusTests`. The bench's
  store rows added: a warm checkout ask at its stat, a warm commit or tip ask at no start and under 2 ms (the stamp and the ref's
  file).
- **b. The gates file's one parse, and the commit readers.** `GatesDeclaration`, each view equal to the parser it replaces on every
  `daoris.gates.json` blob in this repository's history and the tests' fixtures before that parser is deleted; then §10's tip and commit
  rows moved, `TreeReadMemo.Kept.Declared`, `BacklogMerge`'s `_declared` and `TogetherChecks`' memo removed,
  `DeclarationKeepersCensusTests` added. Proof: each moved reader's tests unchanged; the line moving during a replay; a link of
  identical bytes refused; the offline partial clone's follow said, nothing kept, read once its promisor answers; a missing commit said
  unread, not absent; an oversize manifest said, never parsed as a prefix; `TogetherAccepts` with a tag shadowing its line. Then
  `LandGitCountTests`, `TidyGitCountTests` and `StartGitCountTests` lowered; the bench's line and commit rows at one listing a new
  commit and one read a new content.
- **c. The checkout and home readers, the writers, the censuses.** §10's tree, checkout and home rows, §11's listings, §13.2's writers,
  `StampedWorkflows` removed, `DriverConfig` over frozen collections, `DeclarationReadersCensusTests` and `DeclarationGitCensusTests`
  with their exemptions. Proof: each moved reader's tests unchanged; an in-place write of the same length inside the clock's grain,
  asked again at 3 s, reads the new content; a child process writing the person's layer, read at the next ask; a write during the read
  kept for none; a malformed gates file said, then fixed in place and read; a workflow written by another process listed; an index file
  in one of two trees with one manifest found in that tree alone; `core.autocrlf=true`, the manifest still unmodified; both censuses red
  on their planted readers. The bench's checkout and home rows at a stat each.
- **d. The views and the analysis's commit part.** §11's views with their census and key tests; §12's parts; `FilesLooks` naming its
  line's ref in full. Proof: the analysis's, the files stage's and the Files page's tests unchanged; Codex 7's two repositories; Codex 8's
  output at an unchanged `HEAD` and Opus 7's tree B, each judged `Output`; a `core.ignorecase` change moving the stamp. The bench's
  files-stage and Files page rows warm at two starts where the tops are as they were and three where one is new, from 11 and 10.
- **e. Saved.** §14.2's tables, the bound at admission and the trim. Proof, the misses first: a replacement made, and
  `core.useReplaceRefs=false` set in the shared configuration, each while the desktop is down, refused at the cold start and asked of
  git; the promisor down, nothing saved, and saved once it answers; a row whose stamp differs in one input refused; a blob row whose
  bytes do not hash to its id refused at its load; a fill past the bound holding the weight within it after every insert, listing and
  absence rows counted; the trim. Then the hits: the bench's follow over 20 repositories run twice with the driver made again between,
  0 git starts the second time where no line moved (the top from its files, the tips from the refs' files, listings and blobs from the
  store), the toolchain digest's processes counted apart.

Nothing was measured anew for round 2, and nothing in the product changed.

## Round 3

*Supersedes round 2 where it differs: §7's admission and its saved stamp by §19 and §20; §8's answers and what is kept by §21; §11's and
§13.3's censuses by §23; §12's search key and its row d target by §22; §13.3's `WorkflowHomeTopic` exemption by §24; §14.1's one bound
and §14.2's stamp column by §19 and §25; §17 by §28.*

Both makers read round 2 ([the reads](2026-11-09-repo-config-reads.md), *Codex, round 2* and *Opus (independent), round 2*). Here
Codex's five blocking gaps are **C1** to **C5**, Opus's two are **B1** and **B2**, the leaks each found in a census are *the leaks*, and
the points that did not block are **N1** to **N6**. Round 3 closes each by a construction a member not yet found cannot pass, and shows
each census red on a member planted where round 2's census was green. Written from the code at `bdef71a3e2`.

## 18. In short

- **A commit's facts are proven by their own bytes** (C1, C2). The store admits an entry, an absence or a blob only where the commit,
  its trees and the blob hash to their ids within the read, and looks one up only where no replacement can be in force, judged from
  the ref files alone. Its facts then rest on no stamp, so a stamp that does not bind its start, or returns to what it was, cannot
  carry a wrong one. A holder whose answer its bytes do not prove admits under a fence of stats read before and after (§19).
- **The stamp's files are git's to name** (the leaks). Its candidates are every level git reads and every include named, present or
  absent; git's own list of the files it read is the test's oracle (§20).
- **What is kept is a function of its key; a read's failure is a function of nothing** (B1, C5). A kind's parse takes its bytes alone,
  so `driver.json`'s kept kind is its parse and the staffing is laid over it at each ask; the gates file's syntax is shared and each
  view keeps its own validation (§21).
- **The script search is asked fresh** (C3): its attribute inputs are git's, not a key's (§22).
- **Each census over-approximates and has a run-time half keyed by what is read, never by who reads** (C4, B2, the leaks): names and
  sinks followed to a fixed point over the semantic model; git starts by path and by object id; declaration files held so any other
  open fails; keepers by what an answer flows into; parses by their run-time object graph (§23). The members it finds are each moved
  or exempt (§24).

## 19. A commit's facts, proven by their bytes

Round 2 kept a commit's entries and blobs under the stamp read before the start. A stamp read before does not bind the start (C2), and a
hit by id answers whatever the stamp would now say (C1). Round 3 makes the store's facts about a commit independent of the stamp:

- **The listing hashes what it reads.** The store's listing at commit `C` is one start of `cat-file --batch` fed `C`, `C^{tree}` and,
  for a declaration below the top, each folder on its way (`C:<folder>`). It is admitted only where the commit's bytes hash to `C`, the
  tree's to the id the commit's bytes name, and each folder's to the id its parent's entry names; the entries (name, mode, id) and an
  absence are read from those hashed trees, in the repository's hash. A blob is admitted only where its bytes hash to its id. Every
  id-keyed thing the store keeps or saves (an entry, a verified absence, a blob's bytes) is admitted so and no other way: a fact proven
  by its bytes is a fact of the object store, true in every tree and context where git answers an object with its own bytes. The
  listing replaces round 2's `ls-tree` and costs its one start.
- **Looked up only where no replacement can be in force.** A replacement is the one way git answers an id with other bytes; grafts and
  `shallow` move a commit's parents, never its tree. `GitReplacements.CanBeInForce(common)` holds where a file stands under
  `refs/replace/` at any depth, a `packed-refs` line names a ref under it, `GIT_REPLACE_REF_BASE` is set, or the refs are a reftable. Read
  from the files at each ask, it is `GitLibraryAdmission`'s judgement today, lifted to one member both read (D228). It reads no
  configuration: `core.useReplaceRefs=false` and `GIT_NO_REPLACE_OBJECTS` can only turn a replacement off, so leaving them out can send to
  git an ask the store could have answered, never answer one git would answer otherwise, and no configuration file it misses can make
  it wrong. While it holds, the store's id-keyed parts are passed through, with no lookup and no admission in memory or saved, and the
  reader takes git's answer, as today (C1). The parse by `ContentKey` stays shared: it is keyed by the bytes themselves.
- **A mismatch is git's answer, kept by its bytes alone.** Bytes that do not hash to their id (a replacement added during the read) answer
  the ask as git gave them: present, with their `ContentKey` and no `BlobId`, and nothing id-keyed admitted. That is §7's *answers its
  ask*; §8's *unread* for a mismatch goes (§21).
- **Add a replacement, read, remove it** (C2). No replacement stands at the ask; a session adds `C → C'` before the listing's start; git
  answers `C'`'s bytes for `C`; the replacement is removed. `C'`'s bytes do not hash to `C`, so nothing is admitted, and the reader had
  git's answer of that moment; a restart finds no row. A blob replaced the same way fails its own hash.
- **Saved rows carry no stamp.** `declaration_at` (§14.2) loses its stamp digest. A row is read where no replacement can be in force at the
  ask, so a replacement made while the desktop is down sends the cold start's ask to git, and `core.useReplaceRefs=false` set while it is
  down leaves a row that is still the object's fact. A blob row is hashed against its id at its load, as in round 2.

**A holder whose answer its bytes do not prove admits under a fence.** The tracked list (`ls-tree -r`, which hands back no tree's bytes),
ignore-case and GITLIB1's `GitReadCache` cannot hash their answers to an id. Each reads a fence before its start and again after it: the
stats (length, write time, the file's id and change time) of every input its answer rests on, and for an input absent, of its nearest
folder. The answer is admitted only where the two fences are equal and settled, each write time at least 2 s before the first fence was
read on that volume's clock (§9, N2 in §25). A change that returns an input's content to what it was does not return its stats: a rewrite
moves the file's write time and id, an add and a remove move the folder's. The tracked list's inputs are the replacement gate's files
alone (a commit's tree moves with nothing else, and `-z` output reads no configuration), so it is kept by commit and shared by every tree
of the repository; ignore-case's are the stamp's (§20); `GitReadCache`'s are its stamp's.

## 20. The stamp's files, named by git

After §19 no store fact about a commit rests on the stamp; it keys `GitReadCache`'s answers and ignore-case. Round 2 listed its
configuration files and tested the ones it listed, so a file at a level it did not list, or one absent at the first stamp, moved nothing
(the leaks: Git for Windows' `ProgramData` file, which `GitLibrary.cs:74` already points libgit2 away from, and an include created later).

- **The candidates are git's levels and every include named, present or absent.** A `FileStamp` for each file git would read for the
  asking folder: the system file (the git in use's, or `GIT_CONFIG_SYSTEM`; none under `GIT_CONFIG_NOSYSTEM`); on Windows the `ProgramData`
  file read before it; the global file as a start is handed it (`GitGlobalFile.Hand`'s, else the person's own and the XDG file); the
  repository's `config`; the tree's `config.worktree`; and each file an `include.path` or `includeIf.*.path` names in a present candidate,
  resolved as git resolves it (relative to the including file, `~` expanded) and followed into its own includes. An absent candidate is
  stamped absent, so its creation moves the stamp. A conditional's inputs are stamped whether it matches now or not (`gitdir:` the asking
  folder, `onbranch:` the tree's `HEAD`).
- **Git is the oracle.** `GitStampTests` runs `git config --list --show-origin --show-scope --includes` in the asking folder of each fixture
  after each change, and is red where git names a file that is not a candidate, and where a file git names was created, changed or
  removed and the stamp did not move. It runs against each git Daoris starts (the pinned MinGit and the runner's), so a level is found by
  that git's own answer rather than by a list. Planted, each green under round 2: the `ProgramData` file created after the first stamp;
  the system file under `GIT_CONFIG_SYSTEM` created after it; an include target absent at the first stamp, then created; an include inside
  that include; an `includeIf "gitdir:…"` that a tree added later matches.
- **The variables** stay §7's, read from the process and from each start's handed set.

## 21. What is kept is a function of its key

One rule replaces §8's lists of what is kept: **the store keeps a value only where it is a function of the key it is kept under; a read's
failure is a function of nothing, so it is never kept.** Three things follow.

- **A kind's parse takes its bytes alone.** Every kind is an `IDeclarationKind<T>` whose one member parses the content's bytes, and the
  kinds census (§23) is red on a parse that reaches anything but its argument. `DriverConfigDisk.Load` is not such a function (B1): it
  lays `RoleStaffingPort.Fill` over the file, reading the desktop store's `role_staffing` through a port and keeping the file's staffing
  where that store throws (`RoleStaffingPort.cs:57`), and it sets `SourceFile`, which `Save` compares to choose an edit over a whole write
  (`DriverConfigDisk.cs:80`). The kept kind is `DriverConfigReader.Parse(text)` alone; `DriverConfig.Load` lays `SourceFile` and `Fill`
  over the kept parse at each ask. So the staffing is read at every load, as now; a fallback answers its own load and is kept for none;
  and two homes of equal bytes share the parse and never each other's `SourceFile`.
- **The gates file: one syntax, each view's validation its own** (C5). The kind is the file's syntax: valid JSON as an immutable element
  (a cloned `JsonElement`), or its syntax problem with its line and byte. Each view (§13.1's parsers) validates its own part over it and
  answers its own value or its own problem's words, as its parser does now: valid heavy gates beside a malformed `integration.by` stay
  the heavy view's, as `HeavyGates.Read` reads them, while the integration view refuses, as `IntegrationDeclaration.Read` does. A view's
  problem is a function of the content, so it is kept as its value is. Parity is a table: each of today's parsers is run over every
  `daoris.gates.json` blob in this repository's history, the tests' fixtures and the mixed-validity fixtures, its outcome (a value, or a
  problem's words) recorded, and each view is equal to it cell by cell before that parser is deleted. The mixed-validity fixtures: valid
  gates beside a malformed `integration.by`; a gate with no `run` beside a valid `integration`; one gate's `heavy` not a boolean; a
  malformed writer, a malformed local input and a malformed row-table rule, each beside valid gates; a file that is not JSON; a valid file
  with keys no view reads.
- **The three answers, restated.** *Present*: the content (its `ContentKey` and size), at a commit the entry (mode, type, and the `BlobId`
  §19 proved), the kind's parse, and each view asked. *Absent*: at a commit, a hashed tree holding no entry by the name (§19); on disk, a
  stat that finds no file in a folder that stands, kept for none (its stat is its check). *Unread*: the read failed (a start failed, the
  blob missing after the one fetching ask, a size past the reader's limit, a read ended short, an IO error, the folder gone), kept for
  none in memory or saved. Bytes git answered that are not their id's (§19), and bytes read while their stat moved or was racy (§9), are
  present and answer their ask, kept by their `ContentKey` alone.

## 22. The script search, asked fresh

`git grep -I` (`FileGit.LinesAsync`) judges a file binary by its `diff` attribute (C3), and git reads that attribute from the asking tree's
working `.gitattributes` (the index where a file is missing), `info/attributes`, `core.attributesFile` or its XDG default, and the system
attributes file, `GIT_ATTR_NOSYSTEM` deciding the last. Two trees of one commit, one list of tops and one of pathspecs can then answer two
searches (one marks a writer script `-diff`), and an edit of an attribute file moves the answer. A key of those inputs would be a second
list beside git's, red only where a test thought of each. Running at `--attr-source=<commit>` reads the commit's in-tree attributes in
place of the tree's, which changes the answer, and still reads `info/attributes` and the global and system files. So the search is one
start of every analysis with a name to search for, kept by nothing, and §12's row for it goes; with no name, nothing starts, as now.

**Row d's target, restated.** Warm, the files stage and the Files page each ask the tree for its ignored tops, its worktrees and the
search: **three starts on this repository, two where the tree has no ignored top to look for**, from 11 and 10. A new commit adds its
tracked list and its declarations' listing, a new content its blob, a new stamp its ignore-case: one start each.

## 23. The censuses, each sound by construction

Round 2's censuses each named what they looked for, so a member outside the names passed (C4, B2, the leaks): a name handed to a helper or
returned by a property, a read by id after a whole listing, a cache typed `object`, a `List` behind a read-only interface. Round 3's each
over-approximate, follow every value the semantic model can follow until nothing changes, are red on an edge they cannot follow, and have
a run-time half keyed by what is read: a path, an object id, a held file.

**The source half** (`DeclarationReadersCensusTests`), over the driver's and the modules' semantic models with Roslyn:

- **Names, to a fixed point.** A *name* is a declaration's literal or constant (constant-folded, so `"daoris." + "gates.json"` is one) and
  every value made from a name by `Path.Combine`, `Path.Join`, concatenation, interpolation or formatting. A local, field, property,
  parameter, method's return, collection or delegate bears a name where any name is assigned, returned, passed, added or captured into it;
  a parameter where any call site in the sources passes one; an interface member where any implementation's does. So a path helper
  (`PersonFiles.PathOf`, `DriverConfig.ResolvePath`) is found by what it returns, not listed. The set is iterated until it stops growing.
- **Sinks, summarised the same way.** A sink is a member of the file system's types (`File`, `Directory`, `FileInfo`, `DirectoryInfo`,
  `FileStream`, a reader or writer opened on a path, `File.OpenHandle`, `RandomAccess`, `AtomicFile`), a stat (existence, length, a time,
  attributes), a git argument list or batch question, or a JSON parse. A method one of whose parameters reaches a sink is a sink in that
  parameter, and a delegate made from a sink is a sink, found in the same fixed point.
- **Red** where a name reaches a sink outside the store's folder and the exemptions (§13.3, §24); where a JSON parse's input comes from a
  git start's output outside the store (after row b every committed JSON the driver reads is the store's, the analysis's manifests among
  them, so only parses of git's own output formats are exempt, each named); and where a sink is reached through an edge the model cannot
  follow (`dynamic`, reflection, a delegate from outside the sources).

**The run-time halves**, over the starts and reads the count tests and the bench's flows make:

- **Git, by path and by id** (`DeclarationGitCensusTests`). A start the store did not build is red where its arguments or batch questions
  name a declaration's path, or name an object id a declaration's entry holds at any commit the run's starts named. The census lists those
  entries after the run, through the store's own listing, so the order of the reads does not matter.
- **Files, held** (`DeclarationFileCensusTests`). Over the bench's read flows, every declaration file of their fixtures (the checkouts',
  the trees', the home's) is held open with no sharing; the store's one disk reader reads through the held handles, and any other open of
  one, by any API, helper or library, throws a sharing violation, which the census sees at its first chance
  (`AppDomain.FirstChanceException`) even where the reader swallows it. The fixtures' files are aged and their indexes refreshed first,
  so git's own status opens none of them.

**The other censuses:**

- **Keepers** (`DeclarationKeepersCensusTests`). An *answer* is a value a store member returns, and every value the fixed point makes from
  one. Red on an answer written into a static field, an instance field outside its constructor, an element of a collection a field holds,
  a `??=` target or a `Lazy<>`, whatever the field's type; and in a type that asks the store, red on every field and `??=` the fixed point
  does not prove answer-free, that proof being its only clearance.
- **Parses** (`DeclarationParsesCensusTests`). Round 2's check of declared types stays, and the run-time object graph is added: each kind
  and view made from the bench's files, every fixture and every historical blob is walked by reflection over its values' run-time types,
  red on any object outside a set of immutable types (strings, primitives, enums, `ImmutableArray<>`, `FrozenDictionary<,>`,
  `FrozenSet<>`, a compiled `Regex`, a cloned `JsonElement` as one leaf, and records whose fields are read-only and hold such values), and
  red on a member null or empty in every graph walked, which no walk judged.
- **Views and kinds** (`DeclarationViewsCensusTests`). A view's computation and a kind's parse are followed through every call they reach,
  an interface call to every implementation in the sources; red where any member reached touches a sink, the clock, the environment, a
  port, static mutable state, `DriverConfig` or a home layer's reader. At run time a view reads contents through a handle that answers only
  the content keys its key record holds and throws on any other.
- **The stamp** (`GitStampTests`): git as the oracle (§20).

| Census | Red on, each planted where round 2's census was green |
|---|---|
| Readers, source | a helper taking the path: `Read(DeclarationNames.Gates)` with `Read(path)` calling `File.ReadAllText` in another file (`UnionJoins.ShowAsync`'s shape, and `LoopGrounds`' `Hash`); a property returning the name, read through an interface: `File.Exists(Path.Combine(tree, review.GatesFile))` (`ContextBriefs`'); a path method into a stat helper: `HomeWrites.Stamp(DriverConfig.ResolvePath(home))` (`LimitWaits`'); `FileGit.ShowAsync(root, commit, GeneratedWriters.DeclarationFile)` (`FileAnalysis`'); a delegate made from `File.ReadAllText`; a name split across two literals |
| Readers, git | `ls-tree -r` of the whole tree, then `cat-file` of the manifest's id (`LayoutGit`'s shape) |
| Readers, files | a name built from data at run time, no constant of it in the sources, opened through a stream in a helper whose `catch` swallows the error |
| Keepers | an `IReadOnlyList<Regex>` made from the gates view by a helper and held in a record a field's dictionary keeps (`TreeReadMemo.Kept.Declared`'s shape); an `object` field set from a view; a `??=` of a hash of a store answer (`TreeReadMemo.Home`'s); a static `ConcurrentDictionary<string, object>` in a type that never asks the store, filled with an answer handed to it |
| Parses | an `IReadOnlyList<string>` backed by a `List<string>` at run time (`FileRule.Paths`' and `RepositoryFilesRead.Rules`' shape); an array behind an `IReadOnlyList<>` |
| Views and kinds | a view whose helper in another file calls `PersonFiles.Read`; a kind whose parse calls `RoleStaffingPort.Fill` (`DriverConfigDisk.Load`'s shape); a view asking its contents for a key its record does not hold |
| Stamp | §20's five planted files |

Each is also red on each exemption left out in turn, `SourceCensus.Problems` saying a member missed and a member gone, as round 2's.

## 24. The members found, each moved or exempt

The rules of §23, run over the code by hand at this round's commit, find these beside §13's:

| Member | What it does | Its place |
|---|---|---|
| `ContextBriefs.Gates` (`Context/Events/ContextBriefs.cs:210`) | an existence check of the tree's gates file through `ContextReview.Current.GatesFile`, then `DeclaredGates` | moved (c): the store's tree ask, whose *absent* is the check |
| `LimitWaits.Stamp` (`Course/Chat/LimitWaits.cs:395`) | `driver.json` stamped by length and write time, deciding a skip at `:90` | moved (c): the content key of the `DriverConfig` it is handed, which the store answered under §9's rule |
| `TreeReadMemo.Home` (`Workplace/TreeReadMemo.cs:278`) | `driver.json`'s bytes hashed through `trees.ConfigPath`, kept by `??=` | moved (c): the store's content key, asked each pass, kept nowhere |
| `HomeWatch.ForDriver`'s config topic (`Events/HomeWatch.cs:159`) and any other topic's part over a declaration | the file stamped by length and write time for the bus | moved (c): a part's stamp is the store's version of the file |
| `WorkflowHomeTopic` | each workflow stamped by length and write time for its topic | moved (c), its §13.3 exemption withdrawn: a stamp that decides a publish is a version of the file |
| `LoopGrounds` (`Modules/Loop/LoopGrounds.cs:231`) | the checkout's manifest hashed through a helper for a pipe write's grounds | moved (c): the store's content key of it |
| `UnionJoins` (`Workplace/UnionJoins.cs:245`) | the manifest at `declaredAt` through its own `ShowAsync` | moved (b); its `.gitattributes` read stays its own |
| `FileAnalysis.ReadAsync` (`Workplace/Files/FileAnalysis.cs:31`) | the gates at the commit through `FileGit.ShowAsync` | moved (b) as a commit ask, with the manifests, lockfiles and workspace files; its keeping is d's |
| `LayoutGit` (`Workplace/LayoutGit.cs:35`, `:44`) | the whole tree listed, then each wanted blob read by id | moved (b): the declarations' entries and texts from the store, the rest its own |
| `DirectGateRuns` (`Review/Integration/DirectGateRuns.cs:16`), `IntegrationJudging` (`:59`) | the accepted text they are handed, parsed by `DeclaredGates.Read` | named in §13.1 (N1): the gates kind's views over the handed text's content key (b) |
| `RemoteSync` (`Service/RemoteSync.cs:309`) | git's status of the checkout's manifest (`UnmodifiedAsync`) | exempt: git's judgement of the file, reading no content of the store's (§9) |

## 25. The points that did not block

- **N1.** `DirectGateRuns.cs:16` and `IntegrationJudging.cs:59` join §13.1's readers of the gates file (§24).
- **N2, the volume's clock.** §9 compares a write time with `H`, this machine's clock, which on a share whose clock lags and whose times are
  coarse trusts a racy stat. `H` is the volume's clock: this machine's on a local fixed volume; elsewhere (a network drive, a UNC path) the
  write time of a mark the store touches on that volume after reading `s1`, in the asking tree's git directory beside git's own index, as
  git compares an entry with its index file's write time. Where no mark can be written, no stat on that volume is trusted and each ask
  hashes the bytes (1.4 ms for this repository's four). §19's fences read the same clock.
- **N3, the indexes weighed.** The checkout and home entries (a path's `FileStamp`, `H` and content key), the commit listings, the parsed
  `packed-refs` and the stamps' per-file and per-folder stats are weighed as §14.1 weighs the rest.
- **N4, a bound per kind.** Each kind has its own bound and its own least-recently-used order, so one never evicts another: declarations'
  bytes, parses and views 16 MB; tracked lists 24 MB (this repository's 9,201 paths are about 1.4 MB), none kept past a quarter of it; the
  indexes and stamps 4 MB. The admission test counts each.
- **N5, the found layer in the view's key.** `StoreDependency` and `FilesLooks` take `found.Reading?.Rules ?? reading.Found.Rules`, so a
  tree's effective rules are keyed by the found layer's content key where one is on file, and by the tree's analysis key where none is.
- **N6, row e's count.** After §19 no saved row rests on the stamp, so the cold follow asks git nothing for its system configuration's path;
  where another of the bench's flows asks it (once a git in use and process), that start is counted apart.

## 26. The reads answered (round 3)

| Finding | Answer | Section |
|---|---|---|
| **C1** Blob hits bypass the replacement checks | No lookup and no admission of anything id-keyed, in memory or saved, while a replacement can be in force, judged from the ref files; a mismatch is git's answer, kept by its `ContentKey` alone, so §7 and §8 agree | §19, §21; rows a, e |
| **C2** A stamp captured before does not bind the read | A commit's facts admitted only from bytes hashing to their ids within the read, resting on no stamp; any other holder under a fence of stats before and after, which a change returning the content does not return | §19; rows a, e |
| **C3** The search's key omits attributes | Asked fresh at every analysis; row d's target three starts, two with no ignored top | §22; row d |
| **C4** No general guarantee that a missed member fails | Names and sinks to a fixed point; red on an edge not followed; run-time halves by path, id and held file; keepers by where an answer flows; parses by the run-time graph | §23; rows a, d |
| **C5** Whole-file unread against per-view validation | One shared syntax, each view's validation and problem its own and kept; the parity table and the mixed-validity fixtures before any parser goes | §21; row b |
| **B1** `driver.json`'s load is not a function of its bytes | The kept kind is `DriverConfigReader.Parse` alone; `SourceFile` and `Fill` at each ask; a kind's parse census-held to its bytes | §21, §23; rows a, c |
| **B2** Second ways of reading survive | Names through calls and returns to a fixed point; the helper and the property planted; ids from the store's listings at run time; each member found moved, `RemoteSync.cs:309` exempt | §23, §24; rows c, d |
| **The leaks**: the source half (`LoopGrounds`, `UnionJoins`, `FileAnalysis` through `FileGit.ShowAsync`) | Helpers summarised as sinks; each moved | §23, §24 |
| **The leaks**: the run-time half (`LayoutGit`'s listing then `cat-file`) | Red on a declaration's object id, not only its path | §23 |
| **The leaks**: keepers (`TreeReadMemo.Kept.Declared`) | Red on an answer in any field or `??=`, whatever its type; in a type that asks the store, on every field not proven answer-free | §23 |
| **The leaks**: parses (`FileRule.Paths`, `RepositoryFilesRead.Rules`) | The run-time object graph walked against immutable types | §23 |
| **The leaks**: the stamp (`ProgramData`'s file, includes absent at first) | Every level and include a candidate, present or absent; git's own list as the oracle | §20; row a |
| **N1** to **N6** | Named in §13.1; the volume's clock by a mark; the indexes weighed; a bound per kind; the found layer or the analysis in the key; the stamp's ask counted apart | §24, §25 |
| **The rows** (both) | c's censuses move to d, after every reader has moved; `IDeclarationView`, the kinds and the censuses that hold each move in a; the sequences added | §28 |

## 27. Rejected in round 3

- **The stamp read before a start as its admission's proof** (round 2's): it does not bind the start, and a change that returns leaves it
  equal (C2).
- **A digest compared before and after as the fence**: an add and a remove return it; stats move forward.
- **The stamp's digest on every saved row** (round 2's): once a row is proven by its bytes, a digest refuses only rows still true, and the
  gate is what the cold start must read.
- **A replacement judged with the configuration** (round 2's `Replaced`): a second reader of git's configuration for a gate whose
  configuration-blind answer costs, at worst, a start where a replacement stands.
- **The search keyed by its attribute inputs, or run at `--attr-source`** (§22).
- **A parse with a problem as unread** (round 2's): it changes a view's answer where its own part is valid (C5).
- **Every field of a type that asks the store red with no clearance**: a memo of something else beside a store ask (a tree's walk) would
  each need an exemption; the proof that no answer reaches a field is the clearance.
- **A census of named members** (round 2's): it fails only on the members it names.
- **A file-open hook for the run-time half** (a file-system seam every reader takes, or the system's file events): the first is a second
  way to open a file, the second needs elevation; a held handle makes every other route fail by itself.
- **c after b** (the other order the reads allowed): b and c touch apart readers and can run beside each other; only the readers'
  censuses need both, so they move to d.

## 28. The rows

*Supersede §17's.* REPOCONFIG1a lands first; b and c follow a and are apart; d follows b and c, and carries the readers' censuses,
since they are red on any reader not yet moved; e follows b. In every row each moved reader's existing tests pass unchanged before a count
is lowered, and the bench is run before and after it: the bench measures, the tests assert.

- **a. The stamp, the gate, the kernel, and the censuses that hold each move.** First GITLIB1's stamp (§7, §20): `GitStamp` with every
  candidate, `GitAskerContext` folded into it, `GitReadCache`'s key carrying it and its admission under the fence (§19); and
  `GitReplacements.CanBeInForce`, lifted from the library's admission, which reads it. Proof: `GitAskerContextTests`' sequences,
  `GitReadCacheTests` and the flow memos' tests unchanged; `GitStampTests` with git as the oracle and §20's five planted files, the
  `ProgramData` file created after the first stamp among them, and a same-length rewrite of each file inside the clock's grain; a
  replacement added, a graph read, the replacement removed, the answer refused by its fence; `GitKeepersCensusTests` naming the stamp.
  Then the kernel: `FileStamp` under §9 and the volume's clock; `ContentKey` and `BlobId`; the hashed listing; `TipOf`; the checkout and
  home index; the three answers (§21); `IDeclarationKind` and `IDeclarationView` with the contents handle; each of today's parsers moved
  behind its kind unchanged; a bound per kind. Proof: §17's unit tests of each key and each invalidation, and §19's: a blob kept in memory,
  a replacement of it added, the next ask answered with git's bytes; a commit, its tree and a folder each replaced in turn, nothing
  admitted; `refs/replace/` present beside `core.useReplaceRefs=false`, passed through; a replacement of a commit added after the gate and
  removed after the listing, nothing admitted; a tracked list read across an add and a remove of a replacement, refused by its fence; a
  share's clock lagging by a seam, its racy stat hashed again; the weights against the allocations. With them the censuses that hold each
  later move: `DeclarationKeepersCensusTests`, `DeclarationParsesCensusTests` (declared types and the run-time graph) and
  `DeclarationViewsCensusTests` (views and kinds), each red on §23's planted members. The bench's store rows: a warm checkout ask at its
  stat, a warm commit or tip ask at no start and under 2 ms.
- **b. The gates file's one parse, and the commit readers.** `GatesDeclaration` as §21's syntax with each view's own validation; the parity
  table over every gates blob in this repository's history, the tests' fixtures and §21's mixed-validity fixtures, each parser deleted only
  once its column is equal. Then §10's tip and commit rows and §24's commit members moved (`UnionJoins`, `FileAnalysis`'s commit asks,
  `LayoutGit`'s declarations, `DirectGateRuns`, `IntegrationJudging`); `TreeReadMemo.Kept.Declared`, `BacklogMerge`'s `_declared` and
  `TogetherChecks`' memo removed. Proof: each moved reader's tests unchanged; the mixed-validity fixtures, each view answering its parser's
  cell; the line moving during a replay; a link of identical bytes refused; the offline partial clone's follow said, nothing kept, read once
  its promisor answers; a missing commit said unread, not absent; an oversize manifest said, never parsed as a prefix; `TogetherAccepts`
  with a tag shadowing its line. Then `LandGitCountTests`, `TidyGitCountTests` and `StartGitCountTests` lowered; the bench's line and commit
  rows at one listing a new commit and one read a new content.
- **c. The checkout and home readers, and the writers.** §10's tree, checkout and home rows, §11's listings, §13.2's writers and §24's
  checkout and home members (`ContextBriefs`, `LimitWaits`, `TreeReadMemo.Home`, `HomeWatch`'s parts, `WorkflowHomeTopic`, `LoopGrounds`);
  `StampedWorkflows` removed; `DriverConfig` as §21's kind over frozen collections, `SourceFile` and `Fill` laid at each ask. Proof: each
  moved reader's tests unchanged; B1's three sequences (`role_staffing` changed with `driver.json` untouched, the next load answering the
  new actors; the staffing store throwing once, that load answering the file's staffing and the next the store's; two homes of equal bytes
  sharing one parse, each load and save carrying its own `SourceFile`); an in-place write of the same length inside the clock's grain, asked
  again at 3 s, reading the new content; a child process writing the person's layer, read at the next ask; a write during the read kept for
  none; a malformed gates file said, then fixed in place and read; a workflow written by another process listed and its topic published;
  an index file in one of two trees with one manifest found in that tree alone; `core.autocrlf=true`, the manifest still unmodified. The
  bench's checkout and home rows at a stat each.
- **d. The views, the analysis's kept parts, and the readers' censuses.** §11's views with their key tests and N5's key; §12's parts as §19
  and §22 restate them (the tracked list by commit under its fence, ignore-case by the stamp, the search fresh); `FilesLooks` naming its
  line's ref in full. Then, every reader having moved, `DeclarationReadersCensusTests`, `DeclarationGitCensusTests` and
  `DeclarationFileCensusTests` with their exemptions. Proof: the analysis's, the files stage's and the Files page's tests unchanged; two
  repositories sharing three ids and differing in personal rules, two views; an ignored output added at an unchanged `HEAD`, and tree B's
  new top, each judged `Output`; two trees of one commit, one marking a writer script `-diff`, each judged as git judges it; a
  `core.ignorecase` change moving the stamp; each census red on §23's planted members, B2's helper and property among them, and on each
  exemption left out. The bench's files-stage and Files page rows warm at three starts, two where the tree has no ignored top, from 11
  and 10.
- **e. Saved.** §14.2's tables without the stamp's digest, the bound at admission and the trim. Proof, the misses first: a replacement made
  while the desktop is down, the cold start's ask passed through to git; a blob saved, a replacement of it made while the desktop is down,
  the cold start answering git's bytes; a replacement added, a commit read and the replacement removed, then a restart, no row; the
  promisor down, nothing saved, and saved once it answers; a blob row whose bytes do not hash to its id refused at its load; a fill past
  the bound within it after every insert, each kind counted; the trim. Then the hits: the bench's follow over 20 repositories run twice
  with the driver made again between, 0 git starts the second time where no line moved (the top from its files, the tips from the refs'
  files, listings and blobs from the store), the toolchain digest's processes and any stamp ask counted apart.

Nothing was measured anew for round 3, and nothing in the product changed.
