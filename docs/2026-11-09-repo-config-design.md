# A repository's Daoris configuration read once and indexed (REPOCONFIG1)

**Round 2, proposed for both makers' reads before a decision is written** (2026-11-09). The owner finds reading a repository's
Daoris configuration slow and wants it read once and better indexed. Round 1 (§0 to §5) measured every reader (§1, §2, §3) and
proposed one store every reader passes (§4) and the rows that build it (§5). Both makers read it ([the reads](2026-11-09-repo-config-reads.md))
and found seventeen blocking sequences and thirteen readers it missed; round 2 (§6 to §17) answers them by class, and supersedes
round 1 where it differs. Driver paths are under `src/Daoris.Desktop/Daoris.Desktop.Driver/`. It builds on GITLIB1
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
| **Every reader and writer** | Thirteen readers (Opus); every parser of the gates file (Codex); two writers, the registry rows and the census's exemptions (Opus, not blocking). *Found:* `LayoutGit`, `WorkflowChoosing`, `FilesLooks`, `FoundReads`, `IntegrationPromoteReads.FenceAsync`, `VerifyRunner.Of`, `IntegrationDeclaration.Declared`, `TogetherChecks`' memo (a fourth cache), `FoundFiles.Write`, `PersonFiles.Write`, `WorkflowHomeTopic`, `WorkflowTerminal`, `WorkflowsStep`, `BaseHosts`, and the names said in words alone | The list and its exemptions (§13) | `DeclarationReadersCensusTests` (source) and `DeclarationGitCensusTests` (run time), red on five planted readers and on each exemption left out; `DeclarationKeepersCensusTests` red on a parse kept outside the store |
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

- **the git in use**: `GitInUse`'s file;
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
